using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Cursovaya
{
	public partial class Form1 : Form
	{
		private bool isRegisterMode = false;

		private int indicatorX = 0;
		private int targetX = 0;

		private string registeredLogin = "";
		private string registeredPassword = "";

		private ComboBox cmbRole;
		private Label lblRole;

		private readonly string connectionString = @"Server=localhost;Database=KiberPride;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;";

		private readonly Color activeColor1 = Color.FromArgb(96, 52, 255);
		private readonly Color activeColor2 = Color.FromArgb(145, 140, 255);
		private readonly Color borderColor = Color.FromArgb(220, 220, 220);

		private readonly System.Windows.Forms.Timer formAnimationTimer = new System.Windows.Forms.Timer();
		private readonly Stopwatch animationWatch = new Stopwatch();

		private int loginPanelHeight;
		private int registerPanelHeight;

		private int loginButtonTop;
		private int registerButtonTop;

		private int animationStartHeight;
		private int animationTargetHeight;

		private int animationStartButtonTop;
		private int animationTargetButtonTop;

		private int fixedPanelTop;
		private int lastRoundedHeight = 0;

		private const int AnimationDuration = 300;


		private SqlConnection CreateConnection()
		{
			return new SqlConnection(connectionString);
		}

		private void ExecuteNonQuery(string query, params SqlParameter[] parameters)
		{
			using (SqlConnection connection = CreateConnection())
			using (SqlCommand command = new SqlCommand(query, connection))
			{
				if (parameters != null && parameters.Length > 0)
					command.Parameters.AddRange(parameters);

				connection.Open();
				command.ExecuteNonQuery();
			}
		}

		private DataTable ExecuteDataTable(string query, params SqlParameter[] parameters)
		{
			using (SqlConnection connection = CreateConnection())
			using (SqlCommand command = new SqlCommand(query, connection))
			using (SqlDataAdapter adapter = new SqlDataAdapter(command))
			{
				if (parameters != null && parameters.Length > 0)
					command.Parameters.AddRange(parameters);

				DataTable table = new DataTable();
				adapter.Fill(table);
				return table;
			}
		}

		private void EnsureAuthSchema()
		{
			try
			{
				ExecuteNonQuery(@"IF OBJECT_ID(N'dbo.SystemUsers', N'U') IS NULL
					CREATE TABLE dbo.SystemUsers
					(
						Id INT IDENTITY(1,1) PRIMARY KEY,
						Login NVARCHAR(50) NOT NULL UNIQUE,
						PasswordHash NVARCHAR(200) NOT NULL,
						RoleName NVARCHAR(50) NOT NULL DEFAULT N'Администратор',
						IsActive BIT NOT NULL DEFAULT 1,
						CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
					)");

				ExecuteNonQuery(@"IF NOT EXISTS (SELECT 1 FROM dbo.SystemUsers WHERE Login=N'admin')
					INSERT INTO dbo.SystemUsers (Login, PasswordHash, RoleName) VALUES (N'admin', N'admin', N'Старший администратор')");

				ExecuteNonQuery(@"IF NOT EXISTS (SELECT 1 FROM dbo.SystemUsers WHERE Login=N'operator')
					INSERT INTO dbo.SystemUsers (Login, PasswordHash, RoleName) VALUES (N'operator', N'operator', N'Администратор')");
			}
			catch (Exception ex)
			{
				MessageBox.Show("Ошибка подключения к SQL:\n\n" + ex.Message, "SQL ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		public Form1()
		{
			InitializeComponent();

			Load += Form1_Load;
			Resize += Form1_Resize;

			timer1.Interval = 15;
			timer1.Tick += timer1_Tick;

			formAnimationTimer.Interval = 16;
			formAnimationTimer.Tick += FormAnimationTimer_Tick;

			btnLogin.Click += btnLogin_Click;
			btnRegister.Click += btnRegister_Click;
			btnMain.Click += btnMain_Click;
		}

		private Font GetAppFont(float size, FontStyle style = FontStyle.Regular)
		{
			bool exists = FontFamily.Families.Any(f => f.Name == "Evolventa");
			return new Font(exists ? "Evolventa" : "Segoe UI", size, style);
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			WindowState = FormWindowState.Maximized;
			FormBorderStyle = FormBorderStyle.Sizable;
			StartPosition = FormStartPosition.CenterScreen;

			panelIndicator.Visible = false;

			label1.Font = GetAppFont(28, FontStyle.Bold);
			label1.AutoSize = true;

			btnLogin.Font = GetAppFont(16);
			btnRegister.Font = GetAppFont(16);
			btnMain.Font = GetAppFont(16, FontStyle.Bold);

			panelMain.BackColor = Color.White;
			panelTabs.BackColor = Color.White;
			panelLogin.BackColor = Color.White;
			panelPassword.BackColor = Color.White;
			panel1.BackColor = Color.White;

			panelTabs.Paint -= panelTabs_Paint;
			panelTabs.Paint += panelTabs_Paint;

			panelLogin.Paint -= RoundedInputPanel_Paint;
			panelPassword.Paint -= RoundedInputPanel_Paint;
			panel1.Paint -= RoundedInputPanel_Paint;

			panelLogin.Paint += RoundedInputPanel_Paint;
			panelPassword.Paint += RoundedInputPanel_Paint;
			panel1.Paint += RoundedInputPanel_Paint;

			SetupTextBox(txtLogin, panelLogin);
			SetupTextBox(txtPassword, panelPassword);
			SetupTextBox(txtConfirmPassword, panel1);


			EnsureAuthSchema();

			lblRole = new Label
			{
				Text = "Роль сотрудника",
				Font = GetAppFont(10, FontStyle.Bold),
				ForeColor = Color.FromArgb(35, 40, 65),
				AutoSize = true,
				Location = new Point(panel1.Left, panel1.Bottom + 28),
				Visible = false
			};
			panelMain.Controls.Add(lblRole);

			cmbRole = new ComboBox
			{
				Font = GetAppFont(11),
				DropDownStyle = ComboBoxStyle.DropDownList,
				Location = new Point(panel1.Left, panel1.Bottom + 58),
				Size = new Size(panel1.Width, 32),
				Visible = false
			};
			cmbRole.Items.Add("Администратор");
			cmbRole.Items.Add("Старший администратор");
			cmbRole.SelectedIndex = 0;
			panelMain.Controls.Add(cmbRole);

			txtPassword.PasswordChar = '●';
			txtConfirmPassword.PasswordChar = '●';

			btnLogin.Parent = panelTabs;
			btnRegister.Parent = panelTabs;

			int half = panelTabs.Width / 2;

			btnLogin.Location = new Point(0, 0);
			btnLogin.Size = new Size(half, panelTabs.Height);

			btnRegister.Location = new Point(half, 0);
			btnRegister.Size = new Size(panelTabs.Width - half, panelTabs.Height);

			btnLogin.BringToFront();
			btnRegister.BringToFront();

			StyleTabButton(btnLogin);
			StyleTabButton(btnRegister);
			StyleMainButton();

			registerPanelHeight = panelMain.Height + 115;
			registerButtonTop = btnMain.Top + 115;

			int shift = panel1.Height + 25;

			loginPanelHeight = registerPanelHeight - shift;
			loginButtonTop = registerButtonTop - shift;

			panelMain.Height = loginPanelHeight;
			btnMain.Top = loginButtonTop;

			panel1.Visible = false;
			txtConfirmPassword.Visible = false;

			CenterMainPanel();
			fixedPanelTop = panelMain.Top;

			SetAllRoundedRegions();
			CenterTitle();

			ShowLoginMode(false);
		}

		private void SetupTextBox(TextBox textBox, Panel parentPanel)
		{
			textBox.Parent = parentPanel;
			textBox.BorderStyle = BorderStyle.None;
			textBox.BackColor = Color.White;
			textBox.Font = GetAppFont(12);
			textBox.Location = new Point(15, (parentPanel.Height - textBox.Height) / 2);
			textBox.Width = parentPanel.Width - 30;
		}

		private void StyleTabButton(Button btn)
		{
			btn.FlatStyle = FlatStyle.Flat;
			btn.FlatAppearance.BorderSize = 0;
			btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
			btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
			btn.UseVisualStyleBackColor = false;
			btn.BackColor = Color.Transparent;
			btn.Cursor = Cursors.Hand;
			btn.TabStop = false;
		}

		private void StyleMainButton()
		{
			btnMain.FlatStyle = FlatStyle.Flat;
			btnMain.FlatAppearance.BorderSize = 0;
			btnMain.UseVisualStyleBackColor = false;
			btnMain.BackColor = Color.Transparent;
			btnMain.ForeColor = Color.White;
			btnMain.Font = GetAppFont(16, FontStyle.Bold);
			btnMain.Cursor = Cursors.Hand;

			btnMain.Paint -= BtnMain_Paint;
			btnMain.Paint += BtnMain_Paint;
		}

		private void ShowLoginMode(bool animate = true)
		{
			isRegisterMode = false;
			targetX = 0;

			txtLogin.PlaceholderText = "Логин";
			txtPassword.PlaceholderText = "Пароль";

			btnMain.Text = "Войти";

			panel1.Visible = false;
			txtConfirmPassword.Visible = false;
			if (lblRole != null) lblRole.Visible = false;
			if (cmbRole != null) cmbRole.Visible = false;

			UpdateTabTextColors();

			if (animate)
				StartPanelAnimation(loginPanelHeight, loginButtonTop);
			else
			{
				panelMain.Height = loginPanelHeight;
				btnMain.Top = loginButtonTop;
				CenterMainPanel();
				fixedPanelTop = panelMain.Top;
				SetAllRoundedRegions();
			}

			timer1.Start();

			panelTabs.Invalidate();
			btnMain.Invalidate();
		}

		private void ShowRegisterMode(bool animate = true)
		{
			isRegisterMode = true;
			targetX = panelTabs.Width / 2;

			txtLogin.PlaceholderText = "Придумайте логин";
			txtPassword.PlaceholderText = "Придумайте пароль";
			txtConfirmPassword.PlaceholderText = "Подтвердите пароль";

			btnMain.Text = "Зарегистрироваться";

			panel1.Visible = false;
			txtConfirmPassword.Visible = false;
			if (lblRole != null) lblRole.Visible = false;
			if (cmbRole != null) cmbRole.Visible = false;

			UpdateTabTextColors();

			if (animate)
				StartPanelAnimation(registerPanelHeight, registerButtonTop);
			else
			{
				panelMain.Height = registerPanelHeight;
				btnMain.Top = registerButtonTop;

				panel1.Visible = true;
				txtConfirmPassword.Visible = true;
				if (lblRole != null) lblRole.Visible = true;
				if (cmbRole != null) cmbRole.Visible = true;

				CenterMainPanel();
				fixedPanelTop = panelMain.Top;
				SetAllRoundedRegions();
			}

			timer1.Start();

			panelTabs.Invalidate();
			btnMain.Invalidate();
		}

		private void StartPanelAnimation(int targetHeight, int targetButtonTop)
		{
			formAnimationTimer.Stop();

			animationStartHeight = panelMain.Height;
			animationTargetHeight = targetHeight;

			animationStartButtonTop = btnMain.Top;
			animationTargetButtonTop = targetButtonTop;

			fixedPanelTop = panelMain.Top;
			lastRoundedHeight = panelMain.Height;

			animationWatch.Restart();
			formAnimationTimer.Start();
		}

		private void FormAnimationTimer_Tick(object sender, EventArgs e)
		{
			double progress = animationWatch.Elapsed.TotalMilliseconds / AnimationDuration;

			if (progress >= 1)
			{
				progress = 1;
				formAnimationTimer.Stop();
				animationWatch.Stop();
			}

			double eased = EaseIn(progress);

			panelMain.SuspendLayout();

			panelMain.Height = Lerp(animationStartHeight, animationTargetHeight, eased);
			btnMain.Top = Lerp(animationStartButtonTop, animationTargetButtonTop, eased);

			panelMain.Left = (ClientSize.Width - panelMain.Width) / 2;
			panelMain.Top = fixedPanelTop;

			if (isRegisterMode)
			{
				int safeTop = panel1.Top + panel1.Height + 15;

				if (btnMain.Top >= safeTop)
				{
					panel1.Visible = true;
					txtConfirmPassword.Visible = true;
					if (lblRole != null) lblRole.Visible = true;
					if (cmbRole != null) cmbRole.Visible = true;
				}
			}

			CenterTitle();

			panelMain.ResumeLayout(false);

			if (Math.Abs(panelMain.Height - lastRoundedHeight) >= 12 || progress >= 1)
			{
				SetRoundedRegion(panelMain, 25);
				lastRoundedHeight = panelMain.Height;
			}

			panelMain.Invalidate();
			btnMain.Invalidate();

			if (progress >= 1)
			{
				panelMain.Height = animationTargetHeight;
				btnMain.Top = animationTargetButtonTop;

				if (isRegisterMode)
				{
					panel1.Visible = true;
					txtConfirmPassword.Visible = true;
					if (lblRole != null) lblRole.Visible = true;
					if (cmbRole != null) cmbRole.Visible = true;
				}
				else
				{
					panel1.Visible = false;
					txtConfirmPassword.Visible = false;
					if (lblRole != null) lblRole.Visible = false;
					if (cmbRole != null) cmbRole.Visible = false;
				}

				SetAllRoundedRegions();
				CenterTitle();
			}
		}

		private double EaseIn(double t)
		{
			return t * t;
		}

		private int Lerp(int start, int end, double progress)
		{
			return start + (int)Math.Round((end - start) * progress);
		}

		private void UpdateTabTextColors()
		{
			if (isRegisterMode)
			{
				btnLogin.ForeColor = Color.Black;
				btnRegister.ForeColor = Color.White;
			}
			else
			{
				btnLogin.ForeColor = Color.White;
				btnRegister.ForeColor = Color.Black;
			}

			btnLogin.Invalidate();
			btnRegister.Invalidate();
		}

		private void btnLogin_Click(object sender, EventArgs e)
		{
			ShowLoginMode();
		}

		private void btnRegister_Click(object sender, EventArgs e)
		{
			ShowRegisterMode();
		}

		private void btnMain_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrWhiteSpace(txtLogin.Text) ||
				string.IsNullOrWhiteSpace(txtPassword.Text))
			{
				MessageBox.Show("Введите логин и пароль");
				return;
			}

			try
			{
				EnsureAuthSchema();

				if (isRegisterMode)
				{
					if (string.IsNullOrWhiteSpace(txtConfirmPassword.Text))
					{
						MessageBox.Show("Подтвердите пароль");
						return;
					}

					if (txtPassword.Text != txtConfirmPassword.Text)
					{
						MessageBox.Show("Пароли не совпадают");
						return;
					}

					string role = cmbRole?.Text ?? "Администратор";

					ExecuteNonQuery(@"INSERT INTO dbo.SystemUsers (Login, PasswordHash, RoleName, IsActive)
						VALUES (@Login, @Password, @Role, 1)",
						new SqlParameter("@Login", txtLogin.Text.Trim()),
						new SqlParameter("@Password", txtPassword.Text.Trim()),
						new SqlParameter("@Role", role));

					MessageBox.Show("Сотрудник зарегистрирован. Роль: " + role, "Регистрация");

					txtLogin.Clear();
					txtPassword.Clear();
					txtConfirmPassword.Clear();

					ShowLoginMode();
				}
				else
				{
					DataTable user = ExecuteDataTable(@"SELECT TOP 1 Login, RoleName
						FROM dbo.SystemUsers
						WHERE Login=@Login AND PasswordHash=@Password AND ISNULL(IsActive,1)=1",
						new SqlParameter("@Login", txtLogin.Text.Trim()),
						new SqlParameter("@Password", txtPassword.Text.Trim()));

					if (user.Rows.Count == 0)
					{
						MessageBox.Show("Неверный логин или пароль");
						return;
					}

					string login = user.Rows[0]["Login"].ToString();
					string role = user.Rows[0]["RoleName"].ToString();

					MainForm mainForm = new MainForm(login, role);
					mainForm.FormClosed += (s, args) =>
					{
						Show();
						txtPassword.Clear();
						ShowLoginMode(false);
					};
					mainForm.Show();
					Hide();
				}
			}
			catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
			{
				MessageBox.Show("Такой логин уже существует.", "Регистрация");
			}
			catch (Exception ex)
			{
				MessageBox.Show("Ошибка:\n\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void timer1_Tick(object sender, EventArgs e)
		{
			int distance = targetX - indicatorX;

			if (Math.Abs(distance) <= 1)
			{
				indicatorX = targetX;
				timer1.Stop();
				panelTabs.Invalidate();
				return;
			}

			indicatorX += distance / 4;

			if (indicatorX == targetX - distance)
				indicatorX += Math.Sign(distance);

			panelTabs.Invalidate();
		}

		private void panelTabs_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.Clear(Color.White);

			int radius = 14;
			int half = panelTabs.Width / 2;

			Rectangle indicatorRect = new Rectangle(
				indicatorX,
				0,
				half,
				panelTabs.Height - 1
			);

			Rectangle borderRect = new Rectangle(
				0,
				0,
				panelTabs.Width - 1,
				panelTabs.Height - 1
			);

			using (GraphicsPath indicatorPath =
				   GetRoundedRectanglePath(indicatorRect, radius))
			using (LinearGradientBrush brush =
				   new LinearGradientBrush(
					   indicatorRect,
					   activeColor1,
					   activeColor2,
					   0f))
			{
				e.Graphics.FillPath(brush, indicatorPath);
			}

			using (GraphicsPath borderPath =
				   GetRoundedRectanglePath(borderRect, radius))
			using (Pen pen =
				   new Pen(borderColor, 1))
			{
				e.Graphics.DrawPath(pen, borderPath);
			}
		}

		private void RoundedInputPanel_Paint(object sender, PaintEventArgs e)
		{
			Panel panel = sender as Panel;

			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.Clear(Color.White);

			Rectangle rect = new Rectangle(
				0,
				0,
				panel.Width - 1,
				panel.Height - 1
			);

			using (GraphicsPath path =
				   GetRoundedRectanglePath(rect, 12))
			using (Pen pen =
				   new Pen(Color.FromArgb(220, 220, 220), 1))
			{
				e.Graphics.DrawPath(pen, path);
			}
		}

		private void BtnMain_Paint(object sender, PaintEventArgs e)
		{
			Button btn = sender as Button;

			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

			Rectangle rect = new Rectangle(
				0,
				0,
				btn.Width - 1,
				btn.Height - 1
			);

			using (GraphicsPath path =
				   GetRoundedRectanglePath(rect, 18))
			using (LinearGradientBrush brush =
				   new LinearGradientBrush(
					   rect,
					   activeColor1,
					   activeColor2,
					   0f))
			{
				e.Graphics.FillPath(brush, path);
			}

			using (SolidBrush textBrush =
				   new SolidBrush(Color.White))
			using (StringFormat sf =
				   new StringFormat())
			{
				sf.Alignment = StringAlignment.Center;
				sf.LineAlignment = StringAlignment.Center;

				e.Graphics.DrawString(
					btn.Text,
					btn.Font,
					textBrush,
					rect,
					sf);
			}
		}

		private void CenterMainPanel()
		{
			panelMain.Left = (ClientSize.Width - panelMain.Width) / 2;
			panelMain.Top = (ClientSize.Height - panelMain.Height) / 2;
		}

		private void CenterTitle()
		{
			label1.Left = (panelMain.Width - label1.Width) / 2;
		}

		private void Form1_Resize(object sender, EventArgs e)
		{
			if (!formAnimationTimer.Enabled)
				CenterMainPanel();

			fixedPanelTop = panelMain.Top;
			CenterTitle();
		}

		private void SetAllRoundedRegions()
		{
			SetRoundedRegion(panelMain, 25);
			SetRoundedRegion(panelTabs, 14);
			SetRoundedRegion(panelLogin, 12);
			SetRoundedRegion(panelPassword, 12);
			SetRoundedRegion(panel1, 12);
			SetRoundedRegion(btnMain, 18);
		}

		private GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
		{
			GraphicsPath path = new GraphicsPath();

			int diameter = radius * 2;

			path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
			path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
			path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
			path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);

			path.CloseFigure();

			return path;
		}

		private void SetRoundedRegion(Control control, int radius)
		{
			if (control.Width == 0 || control.Height == 0)
				return;

			using (GraphicsPath path = GetRoundedRectanglePath(
				new Rectangle(
					0,
					0,
					control.Width,
					control.Height),
				radius))
			{
				control.Region = new Region(path);
			}
		}

		private void txtLogin_TextChanged(object sender, EventArgs e) { }

		private void txtConfirmPassword_TextChanged(object sender, EventArgs e) { }

		private void panelIndicator_Paint(object sender, PaintEventArgs e) { }

		private void btnLogin_Click_1(object sender, EventArgs e)
		{
			btnLogin_Click(sender, e);
		}

		private void btnRegister_Click_1(object sender, EventArgs e)
		{
			btnRegister_Click(sender, e);
		}
	}
}