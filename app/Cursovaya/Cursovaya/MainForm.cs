using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Reflection;
using System.Windows.Forms;

namespace Cursovaya
{

	public partial class MainForm : Form
	{
		private readonly Color purple = Color.FromArgb(96, 52, 255);
		private readonly Color lightPurple = Color.FromArgb(245, 242, 255);
		private readonly Color textColor = Color.FromArgb(35, 40, 65);
		private readonly Color blue = Color.FromArgb(35, 120, 255);
		private readonly Color green = Color.FromArgb(65, 195, 135);

		private readonly List<Control> homeControls = new List<Control>();
		private Panel currentPage = null!;
		private string currentSection = "Главная";
		private string currentUserLogin = "";
		private string currentUserRole = "";
		private bool useExternalAuthorization = false;

		private readonly System.Windows.Forms.Timer activeSessionsTimer = new System.Windows.Forms.Timer();
		private int realtimeTickCounter = 0;

		// Подключение к базе данных Microsoft SQL Server.
		// Если имя сервера другое, замени localhost на своё имя сервера из Visual Studio.
		private readonly string connectionString = @"Server=localhost;Database=KiberPride;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;";

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

		private object ExecuteScalar(string query, params SqlParameter[] parameters)
		{
			using (SqlConnection connection = CreateConnection())
			using (SqlCommand command = new SqlCommand(query, connection))
			{
				if (parameters != null && parameters.Length > 0)
					command.Parameters.AddRange(parameters);

				connection.Open();
				return command.ExecuteScalar();
			}
		}

		private int GetScalarInt(string query, params SqlParameter[] parameters)
		{
			object result = ExecuteScalar(query, parameters);
			if (result == null || result == DBNull.Value)
				return 0;

			return Convert.ToInt32(result);
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

		private string FormatMoney(decimal value)
		{
			return value.ToString("N0") + " ₽";
		}

		private decimal GetScalarDecimal(string query, params SqlParameter[] parameters)
		{
			object result = ExecuteScalar(query, parameters);
			if (result == null || result == DBNull.Value)
				return 0;

			return Convert.ToDecimal(result);
		}

		private string GetSafeString(IDataRecord reader, string columnName)
		{
			object value = reader[columnName];
			return value == null || value == DBNull.Value ? "" : value.ToString();
		}

		private decimal ParseMoney(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return 0;

			value = value.Replace("₽", "").Replace("р", "").Replace("Р", "").Replace(" ", "").Replace(",", ".").Trim();

			decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal result);
			return result;
		}

		private int ParseNumber(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return 0;

			string digits = "";
			foreach (char c in value)
			{
				if (char.IsDigit(c))
					digits += c;
			}

			if (string.IsNullOrWhiteSpace(digits))
				return 0;

			return int.Parse(digits);
		}

		private int GetOrCreateClientId(string login, string fullName = null, string phone = null)
		{
			login = string.IsNullOrWhiteSpace(login) ? "" : login.Trim();
			phone = string.IsNullOrWhiteSpace(phone) ? "" : phone.Trim();

			if (string.IsNullOrWhiteSpace(login) && string.IsNullOrWhiteSpace(phone))
				throw new Exception("Введите логин или телефон клиента.");

			object existingId = null;

			if (!string.IsNullOrWhiteSpace(login))
			{
				existingId = ExecuteScalar(
					"SELECT Id FROM Clients WHERE Login = @Login",
					new SqlParameter("@Login", login));

				if (existingId != null && existingId != DBNull.Value)
				{
					ExecuteNonQuery(
						"UPDATE Clients SET Phone = COALESCE(NULLIF(@Phone,''), Phone), FullName = COALESCE(NULLIF(@FullName,''), FullName) WHERE Id = @Id",
						new SqlParameter("@Phone", phone),
						new SqlParameter("@FullName", (object)(fullName ?? "") ?? ""),
						new SqlParameter("@Id", Convert.ToInt32(existingId)));

					return Convert.ToInt32(existingId);
				}
			}

			if (!string.IsNullOrWhiteSpace(phone))
			{
				existingId = ExecuteScalar(
					"SELECT Id FROM Clients WHERE Phone = @Phone",
					new SqlParameter("@Phone", phone));

				if (existingId != null && existingId != DBNull.Value)
				{
					if (!string.IsNullOrWhiteSpace(login))
					{
						ExecuteNonQuery(
							"UPDATE Clients SET Login = @Login, FullName = COALESCE(NULLIF(@FullName,''), FullName) WHERE Id = @Id AND (Login IS NULL OR Login LIKE 'client_%')",
							new SqlParameter("@Login", login),
							new SqlParameter("@FullName", (object)(fullName ?? login) ?? DBNull.Value),
							new SqlParameter("@Id", Convert.ToInt32(existingId)));
					}

					return Convert.ToInt32(existingId);
				}
			}

			if (string.IsNullOrWhiteSpace(login))
				login = "client_" + DateTime.Now.Ticks;

			ExecuteNonQuery(
				"INSERT INTO Clients (Login, FullName, Phone) VALUES (@Login, @FullName, @Phone)",
				new SqlParameter("@Login", login),
				new SqlParameter("@FullName", (object)(fullName ?? login) ?? DBNull.Value),
				new SqlParameter("@Phone", (object)phone ?? DBNull.Value));

			return GetScalarInt(
				"SELECT Id FROM Clients WHERE Login = @Login",
				new SqlParameter("@Login", login));
		}

		private int GetSubscriptionIdByName(string name)
		{
			return GetScalarInt(
				"SELECT Id FROM Subscriptions WHERE Name = @Name",
				new SqlParameter("@Name", name));
		}

		private void ShowSqlError(Exception ex)
		{
			MessageBox.Show(
				"Ошибка работы с SQL базой:\n\n" + ex.Message,
				"SQL ошибка",
				MessageBoxButtons.OK,
				MessageBoxIcon.Error);
		}


		public MainForm()
		{
			InitializeComponent();

			Load += MainForm_Load;
			Resize += MainForm_Resize;

			btnHome.Click += btnHome_Click;
			btnSubscriptions.Click += btnSubscriptions_Click;
			btnVisits.Click += btnVisits_Click;
			btnTariffs.Click += btnTariffs_Click;
			btnReports.Click += btnReports_Click;
			btnExit.Click += btnExit_Click;

			HideBonusesIfExists();
		}

		public MainForm(string userLogin, string userRole) : this()
		{
			currentUserLogin = userLogin ?? "";
			currentUserRole = userRole ?? "Администратор";
			useExternalAuthorization = true;
		}


		private bool ShowAuthorizationWindow()
		{
			using Form form = new Form
			{
				Text = "Авторизация",
				Size = new Size(460, 400),
				StartPosition = FormStartPosition.CenterScreen,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox = false,
				MinimizeBox = false,
				BackColor = Color.White
			};

			form.Controls.Add(new Label
			{
				Text = "Вход сотрудника",
				Font = new Font("Segoe UI", 18, FontStyle.Bold),
				ForeColor = textColor,
				AutoSize = true,
				Location = new Point(30, 25)
			});

			form.Controls.Add(new Label { Text = "Логин", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 90) });
			TextBox loginBox = new TextBox { Text = "admin", Font = new Font("Segoe UI", 10), Location = new Point(30, 115), Size = new Size(380, 30) };
			form.Controls.Add(loginBox);

			form.Controls.Add(new Label { Text = "Пароль", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 155) });
			TextBox passwordBox = new TextBox { Text = "admin", UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10), Location = new Point(30, 180), Size = new Size(380, 30) };
			form.Controls.Add(passwordBox);

			Label hint = new Label
			{
				Text = "Роли: Администратор — продажи, клиенты, экспорт отчётов.\nСтарший администратор — полный доступ.",
				Font = new Font("Segoe UI", 9),
				ForeColor = Color.FromArgb(70, 75, 110),
				AutoSize = false,
				Location = new Point(30, 220),
				Size = new Size(390, 45)
			};
			form.Controls.Add(hint);

			bool success = false;
			Button loginButton = CreateDialogButton("Войти", 30, 285, purple, Color.White);
			loginButton.Click += (s, e) =>
			{
				try
				{
					DataTable user = ExecuteDataTable(@"SELECT TOP 1 Login, RoleName FROM dbo.SystemUsers WHERE Login=@Login AND PasswordHash=@Password AND ISNULL(IsActive,1)=1",
						new SqlParameter("@Login", loginBox.Text.Trim()),
						new SqlParameter("@Password", passwordBox.Text.Trim()));

					if (user.Rows.Count > 0)
					{
						currentUserLogin = user.Rows[0]["Login"].ToString();
						currentUserRole = user.Rows[0]["RoleName"].ToString();
						success = true;
						form.Close();
					}
					else
					{
						MessageBox.Show("Неверный логин или пароль.", "Авторизация");
					}
				}
				catch (Exception ex)
				{
					ShowSqlError(ex);
				}
			};
			form.Controls.Add(loginButton);

			Button registerButton = CreateDialogButton("Регистрация", 145, 285, lightPurple, purple);
			registerButton.Click += (s, e) => ShowRegisterSystemUserWindow();
			form.Controls.Add(registerButton);

			Button cancelButton = CreateDialogButton("Выход", 285, 285, lightPurple, purple);
			cancelButton.Click += (s, e) => form.Close();
			form.Controls.Add(cancelButton);

			form.AcceptButton = loginButton;
			form.ShowDialog(this);
			return success;
		}

		private void ShowRegisterSystemUserWindow()
		{
			using Form form = new Form
			{
				Text = "Регистрация сотрудника",
				Size = new Size(460, 470),
				StartPosition = FormStartPosition.CenterParent,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox = false,
				MinimizeBox = false,
				BackColor = Color.White
			};

			form.Controls.Add(new Label { Text = "Регистрация сотрудника", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 25) });
			form.Controls.Add(new Label { Text = "Логин", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 85) });
			TextBox loginBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 110), Size = new Size(380, 30) };
			form.Controls.Add(loginBox);

			form.Controls.Add(new Label { Text = "Пароль", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 150) });
			TextBox passwordBox = new TextBox { UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10), Location = new Point(30, 175), Size = new Size(380, 30) };
			form.Controls.Add(passwordBox);

			form.Controls.Add(new Label { Text = "Повторите пароль", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 215) });
			TextBox confirmBox = new TextBox { UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10), Location = new Point(30, 240), Size = new Size(380, 30) };
			form.Controls.Add(confirmBox);

			form.Controls.Add(new Label { Text = "Должность", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 280) });
			ComboBox roleBox = new ComboBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 305), Size = new Size(380, 30), DropDownStyle = ComboBoxStyle.DropDownList };
			roleBox.Items.Add("Администратор");
			roleBox.Items.Add("Старший администратор");
			roleBox.SelectedIndex = 0;
			form.Controls.Add(roleBox);

			Button saveButton = CreateDialogButton("Зарегистрировать", 30, 375, purple, Color.White);
			saveButton.Size = new Size(180, 40);
			saveButton.Click += (s, e) =>
			{
				try
				{
					if (string.IsNullOrWhiteSpace(loginBox.Text) || string.IsNullOrWhiteSpace(passwordBox.Text))
					{
						MessageBox.Show("Введите логин и пароль.");
						return;
					}
					if (passwordBox.Text != confirmBox.Text)
					{
						MessageBox.Show("Пароли не совпадают.");
						return;
					}
					int exists = GetScalarInt("SELECT COUNT(*) FROM dbo.SystemUsers WHERE Login=@Login", new SqlParameter("@Login", loginBox.Text.Trim()));
					if (exists > 0)
					{
						MessageBox.Show("Сотрудник с таким логином уже существует.");
						return;
					}

					ExecuteNonQuery(@"INSERT INTO dbo.SystemUsers (Login, PasswordHash, RoleName, IsActive) VALUES (@Login, @Password, @Role, 1)",
						new SqlParameter("@Login", loginBox.Text.Trim()),
						new SqlParameter("@Password", passwordBox.Text.Trim()),
						new SqlParameter("@Role", roleBox.Text));

					MessageBox.Show("Сотрудник зарегистрирован.", "Готово");
					form.Close();
				}
				catch (Exception ex)
				{
					ShowSqlError(ex);
				}
			};
			form.Controls.Add(saveButton);

			Button cancelButton = CreateDialogButton("Отмена", 245, 375, lightPurple, purple);
			cancelButton.Click += (s, e) => form.Close();
			form.Controls.Add(cancelButton);
			form.ShowDialog(this);
		}

		private bool IsSeniorAdmin()
		{
			return string.Equals(currentUserRole, "Старший администратор", StringComparison.OrdinalIgnoreCase);
		}

		private void ShowAccessDenied()
		{
			MessageBox.Show("Недостаточно прав. Это действие доступно только старшему администратору.", "Ограничение доступа", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}

		private void ApplyRoleAccess()
		{
			bool senior = IsSeniorAdmin();
			btnHome.Enabled = true;
			btnSubscriptions.Enabled = true;
			btnReports.Enabled = true;
			btnVisits.Enabled = senior;
			btnTariffs.Enabled = senior;

			btnVisits.ForeColor = senior ? textColor : Color.Gray;
			btnTariffs.ForeColor = senior ? textColor : Color.Gray;
			Text = $"KiberPride - Система управления ({currentUserRole})";
		}

		private void MainForm_Load(object sender, EventArgs e)
		{
			WindowState = FormWindowState.Maximized;
			StartPosition = FormStartPosition.CenterScreen;

			EnableDoubleBuffer(this);
			EnableDoubleBuffer(panelContent);
			EnableDoubleBuffer(panelMenu);

			panelContent.Paint -= panelContent_Paint;
			panelContent.Paint += panelContent_Paint;

			HideBonusesIfExists();
			SaveHomeControls();
			StyleControls(this);
			SetupQuickAccessClicks();
			EnsureDatabaseSchema();
			SeedDefaultData();

			if (!useExternalAuthorization)
			{
				if (!ShowAuthorizationWindow())
				{
					BeginInvoke(new Action(Close));
					return;
				}
			}

			ApplyRoleAccess();
			SetupActiveSessionsTimer();
			ShowHomePage();
			SetActiveButton(btnHome);
		}

		private void MainForm_Resize(object sender, EventArgs e)
		{
			panelContent.Invalidate();

			if (currentSection == "Главная")
				LayoutHomePage();
			else
				RebuildCurrentSection();
		}


		private void EnsureDatabaseSchema()
		{
			try
			{
				ExecuteNonQuery("IF COL_LENGTH('dbo.Clients', 'BalanceMoney') IS NULL ALTER TABLE dbo.Clients ADD BalanceMoney DECIMAL(10,2) NOT NULL DEFAULT 0");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Clients', 'BonusBalance') IS NULL ALTER TABLE dbo.Clients ADD BonusBalance INT NOT NULL DEFAULT 0");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Clients', 'RemainingMinutes') IS NULL ALTER TABLE dbo.Clients ADD RemainingMinutes INT NOT NULL DEFAULT 0");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Clients', 'RemainingSeconds') IS NULL ALTER TABLE dbo.Clients ADD RemainingSeconds INT NOT NULL DEFAULT 0");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Clients', 'IsDeleted') IS NULL ALTER TABLE dbo.Clients ADD IsDeleted BIT NOT NULL DEFAULT 0");
				ExecuteNonQuery("UPDATE dbo.Clients SET RemainingSeconds = RemainingMinutes * 60 WHERE ISNULL(RemainingSeconds,0)=0 AND ISNULL(RemainingMinutes,0)>0");

				ExecuteNonQuery("IF COL_LENGTH('dbo.Tariffs', 'IsDeleted') IS NULL ALTER TABLE dbo.Tariffs ADD IsDeleted BIT NOT NULL DEFAULT 0");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Subscriptions', 'IsDeleted') IS NULL ALTER TABLE dbo.Subscriptions ADD IsDeleted BIT NOT NULL DEFAULT 0");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Visits', 'ComputerId') IS NULL ALTER TABLE dbo.Visits ADD ComputerId INT NULL");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Visits', 'EndTime') IS NULL ALTER TABLE dbo.Visits ADD EndTime DATETIME NULL");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Visits', 'Status') IS NULL ALTER TABLE dbo.Visits ADD Status NVARCHAR(50) NOT NULL DEFAULT N'Активно'");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Sales', 'ComputerId') IS NULL ALTER TABLE dbo.Sales ADD ComputerId INT NULL");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Sales', 'PaymentType') IS NULL ALTER TABLE dbo.Sales ADD PaymentType NVARCHAR(50) NULL");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Sales', 'MinutesAdded') IS NULL ALTER TABLE dbo.Sales ADD MinutesAdded INT NOT NULL DEFAULT 0");
				ExecuteNonQuery("IF COL_LENGTH('dbo.Sales', 'Comment') IS NULL ALTER TABLE dbo.Sales ADD Comment NVARCHAR(200) NULL");

				ExecuteNonQuery("IF OBJECT_ID(N'dbo.Statuses', N'U') IS NULL CREATE TABLE dbo.Statuses (Id INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(50) NOT NULL UNIQUE)");
				ExecuteNonQuery("IF OBJECT_ID(N'dbo.SystemUsers', N'U') IS NULL CREATE TABLE dbo.SystemUsers (Id INT IDENTITY(1,1) PRIMARY KEY, Login NVARCHAR(50) NOT NULL UNIQUE, PasswordHash NVARCHAR(200) NOT NULL, RoleName NVARCHAR(50) NOT NULL DEFAULT N'Администратор', IsActive BIT NOT NULL DEFAULT 1, CreatedAt DATETIME NOT NULL DEFAULT GETDATE())");
				ExecuteNonQuery("IF OBJECT_ID(N'dbo.SubscriptionTypes', N'U') IS NULL CREATE TABLE dbo.SubscriptionTypes (Id INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(100) NOT NULL UNIQUE, Description NVARCHAR(200) NULL)");
				ExecuteNonQuery("IF OBJECT_ID(N'dbo.ClientOperations', N'U') IS NULL CREATE TABLE dbo.ClientOperations (Id INT IDENTITY(1,1) PRIMARY KEY, ClientId INT NOT NULL, OperationDate DATETIME NOT NULL DEFAULT GETDATE(), OperationType NVARCHAR(100) NOT NULL, AmountMoney DECIMAL(10,2) NOT NULL DEFAULT 0, AmountBonus INT NOT NULL DEFAULT 0, MinutesChanged INT NOT NULL DEFAULT 0, Comment NVARCHAR(300) NULL)");
				ExecuteNonQuery("IF OBJECT_ID(N'dbo.ClientSessions', N'U') IS NULL CREATE TABLE dbo.ClientSessions (Id INT IDENTITY(1,1) PRIMARY KEY, ClientId INT NOT NULL, ComputerId INT NULL, VisitId INT NULL, StartedAt DATETIME NOT NULL DEFAULT GETDATE(), EndAt DATETIME NULL, RemainingSeconds INT NOT NULL DEFAULT 0, Status NVARCHAR(50) NOT NULL DEFAULT N'Активно')");
				ExecuteNonQuery("IF OBJECT_ID(N'dbo.Computers', N'U') IS NULL CREATE TABLE dbo.Computers (Id INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(50) NOT NULL, IsActive BIT NOT NULL DEFAULT 1)");

				ExecuteNonQuery("IF NOT EXISTS (SELECT 1 FROM dbo.Statuses WHERE Name=N'Активно') INSERT INTO dbo.Statuses (Name) VALUES (N'Активно'),(N'Завершено'),(N'Отменено')");
				ExecuteNonQuery("IF NOT EXISTS (SELECT 1 FROM dbo.SystemUsers WHERE Login=N'admin') INSERT INTO dbo.SystemUsers (Login, PasswordHash, RoleName) VALUES (N'admin', N'admin', N'Старший администратор')");
				ExecuteNonQuery("UPDATE dbo.SystemUsers SET RoleName=N'Старший администратор' WHERE Login=N'admin'");
				ExecuteNonQuery("IF NOT EXISTS (SELECT 1 FROM dbo.SystemUsers WHERE Login=N'operator') INSERT INTO dbo.SystemUsers (Login, PasswordHash, RoleName) VALUES (N'operator', N'operator', N'Администратор')");
				ExecuteNonQuery("IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionTypes WHERE Name=N'Почасовой') INSERT INTO dbo.SubscriptionTypes (Name, Description) VALUES (N'Почасовой',N'Оплата по тарифу'),(N'Абонемент',N'Пакет часов на срок')");
				ExecuteNonQuery("IF NOT EXISTS (SELECT 1 FROM dbo.Computers) INSERT INTO dbo.Computers (Name, IsActive) VALUES (N'ПК-1',1),(N'ПК-2',1),(N'ПК-3',1),(N'ПК-4',1),(N'ПК-5',1),(N'ПК-6',1),(N'ПК-7',1),(N'ПК-8',1),(N'ПК-9',1),(N'ПК-10',1)");
			}
			catch
			{
				// Если база ещё не создана, приложение продолжит запуск.
			}
		}

		private void SetupActiveSessionsTimer()
		{
			activeSessionsTimer.Interval = 1000;
			activeSessionsTimer.Tick -= ActiveSessionsTimer_Tick;
			activeSessionsTimer.Tick += ActiveSessionsTimer_Tick;
			activeSessionsTimer.Start();
		}

		private void ActiveSessionsTimer_Tick(object? sender, EventArgs e)
		{
			try
			{
				realtimeTickCounter++;
				ExecuteNonQuery(@"UPDATE dbo.ClientSessions
					SET RemainingSeconds = CASE WHEN RemainingSeconds > 0 THEN RemainingSeconds - 1 ELSE 0 END,
						Status = CASE WHEN RemainingSeconds <= 1 THEN N'Завершено' ELSE Status END,
						EndAt = CASE WHEN RemainingSeconds <= 1 AND EndAt IS NULL THEN GETDATE() ELSE EndAt END
					WHERE Status = N'Активно'");

				ExecuteNonQuery(@"UPDATE dbo.Clients
					SET RemainingSeconds = ISNULL(s.TotalSeconds,0),
						RemainingMinutes = CEILING(CONVERT(float, ISNULL(s.TotalSeconds,0)) / 60.0)
					FROM dbo.Clients c
					OUTER APPLY (
						SELECT SUM(RemainingSeconds) AS TotalSeconds
						FROM dbo.ClientSessions cs
						WHERE cs.ClientId = c.Id AND cs.Status = N'Активно'
					) s");

				ExecuteNonQuery("UPDATE dbo.Visits SET Status = N'Завершено' WHERE Status = N'Активно' AND EndTime IS NOT NULL AND EndTime <= GETDATE()");
			}
			catch
			{
				// Таймер не перерисовывает страницы, поэтому интерфейс не мерцает.
			}
		}


		private string FormatSeconds(int seconds)
		{
			if (seconds < 0)
				seconds = 0;

			TimeSpan time = TimeSpan.FromSeconds(seconds);
			return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
		}

		private void AddClientOperation(int clientId, string operationType, decimal money, int bonus, int minutes, string comment)
		{
			try
			{
				ExecuteNonQuery(@"INSERT INTO dbo.ClientOperations (ClientId, OperationType, AmountMoney, AmountBonus, MinutesChanged, Comment)
					VALUES (@ClientId, @OperationType, @Money, @Bonus, @Minutes, @Comment)",
					new SqlParameter("@ClientId", clientId),
					new SqlParameter("@OperationType", operationType),
					new SqlParameter("@Money", money),
					new SqlParameter("@Bonus", bonus),
					new SqlParameter("@Minutes", minutes),
					new SqlParameter("@Comment", comment));
			}
			catch { }
		}
		private void HideBonusesIfExists()
		{
			foreach (Control control in Controls.Find("btnBonuses", true))
				control.Visible = false;

			foreach (Control control in Controls.Find("cardBonuses", true))
				control.Visible = false;

			foreach (Control control in Controls.Find("quickBonuses", true))
				control.Visible = false;
		}

		private void SaveHomeControls()
		{
			homeControls.Clear();

			foreach (Control control in panelContent.Controls)
				homeControls.Add(control);
		}

		private void ShowHomePage()
		{
			currentSection = "Главная";

			if (currentPage != null)
			{
				panelContent.Controls.Remove(currentPage);
				currentPage.Dispose();
				currentPage = null!;
			}

			foreach (Control control in homeControls)
			{
				control.Visible = true;
				control.BringToFront();
			}

			HideBonusesIfExists();
			LayoutHomePage();
			panelContent.Invalidate();
		}

		private void LayoutHomePage()
		{
			int margin = 45;
			int gap = 25;
			int contentWidth = Math.Max(900, panelContent.ClientSize.Width - margin * 2);

			lblTitle.Text = "Главная";
			lblTitle.BackColor = Color.Transparent;
			lblTitle.Location = new Point(margin, 35);

			lblSubText.Text = "Абонементов за месяц";
			lblVisitsText.Text = "Абонементов сегодня";

			try
			{
				lblSubValue.Text = GetScalarInt("SELECT COUNT(*) FROM Sales WHERE SubscriptionId IS NOT NULL AND MONTH(SaleDate)=MONTH(GETDATE()) AND YEAR(SaleDate)=YEAR(GETDATE())").ToString();
				lblVisitsValue.Text = GetScalarInt("SELECT COUNT(*) FROM Sales WHERE SubscriptionId IS NOT NULL AND CAST(SaleDate AS date)=CAST(GETDATE() AS date)").ToString();
			}
			catch
			{
				lblSubValue.Text = "0";
				lblVisitsValue.Text = "0";
			}

			int cardTop = 135;
			int cardWidth = (contentWidth - gap) / 2;
			int cardHeight = 125;

			cardSubscriptions.SetBounds(margin, cardTop, cardWidth, cardHeight);
			cardVisits.SetBounds(margin + cardWidth + gap, cardTop, cardWidth, cardHeight);

			LayoutCardText(cardSubscriptions, lblSubText, lblSubValue);
			LayoutCardText(cardVisits, lblVisitsText, lblVisitsValue);

			int quickTop = cardTop + cardHeight + 45;
			panelQuickAccess.SetBounds(margin, quickTop, contentWidth, 220);

			LayoutQuickAccessItems();
			LayoutHomeStatsAndBalanceButton(margin, panelQuickAccess.Bottom + 25, contentWidth, gap);
			RoundHomeControls();
			panelContent.AutoScroll = true;
			panelContent.AutoScrollMinSize = new Size(panelContent.ClientSize.Width, Math.Max(panelContent.ClientSize.Height + 1, panelContent.Controls.Find("homeClientsListPanel", true).Length > 0 ? panelContent.Controls.Find("homeClientsListPanel", true)[0].Bottom + 80 : panelContent.ClientSize.Height + 1));
		}

		private void LayoutHomeStatsAndBalanceButton(int margin, int top, int contentWidth, int gap)
		{
			int cardWidth = (contentWidth - gap) / 2;
			int cardHeight = 105;
			int secondRowTop = top + cardHeight + gap;

			string clientsCount = "0";
			string monthIncome = "0 ₽";
			string monthVisits = "0";

			try
			{
				clientsCount = GetScalarInt("SELECT COUNT(*) FROM Clients WHERE ISNULL(IsDeleted,0)=0").ToString();
				monthIncome = FormatMoney(GetScalarDecimal("SELECT ISNULL(SUM(Amount),0) FROM Sales WHERE CAST(SaleDate AS date)=CAST(GETDATE() AS date)"));
				monthVisits = GetScalarInt("SELECT COUNT(*) FROM Visits WHERE CAST(StartTime AS date)=CAST(GETDATE() AS date)").ToString();
			}
			catch { }

			Panel clientsCard = CreateOrUpdateHomeStatCard("homeCardClientsSession", "Всего клиентов", clientsCount, margin, top, cardWidth, cardHeight, purple);
			Panel incomeCard = CreateOrUpdateHomeStatCard("homeCardMonthIncome", "Доход за сегодня", monthIncome, margin + cardWidth + gap, top, cardWidth, cardHeight, green);
			Panel visitsCard = CreateOrUpdateHomeStatCard("homeCardMonthVisits", "Посещений сегодня", monthVisits, margin, secondRowTop, cardWidth, cardHeight, blue);
			Button balanceButton = CreateOrUpdateBalanceButton("btnTopUpBalance", "Пополнить баланс", margin + cardWidth + gap, secondRowTop, cardWidth, cardHeight);

			int clientPanelTop = secondRowTop + cardHeight + gap;
			Panel clientsPanel = CreateOrUpdateHomeClientsPanel("homeClientsListPanel", margin, clientPanelTop, contentWidth, 290);

			clientsCard.BringToFront();
			incomeCard.BringToFront();
			visitsCard.BringToFront();
			balanceButton.BringToFront();
			clientsPanel.BringToFront();
		}

		private Panel CreateOrUpdateHomeClientsPanel(string name, int x, int y, int width, int height)
		{
			Panel panel = null;
			foreach (Control control in panelContent.Controls)
			{
				if (control is Panel existing && existing.Name == name)
				{
					panel = existing;
					break;
				}
			}

			if (panel == null)
			{
				panel = new Panel { Name = name, BackColor = Color.White };
				panelContent.Controls.Add(panel);

				panel.Controls.Add(new Label
				{
					Text = "Клиенты",
					Font = new Font("Segoe UI", 14, FontStyle.Bold),
					ForeColor = textColor,
					AutoSize = true,
					Location = new Point(25, 18)
				});

				TextBox searchBox = new TextBox { Name = "search", Font = new Font("Segoe UI", 10), ForeColor = Color.Gray, Text = "Поиск по логину, имени или телефону", Location = new Point(25, 55), Size = new Size(350, 30) };
				panel.Controls.Add(searchBox);

				Button addButton = CreateDialogButton("Добавить клиента", 390, 52, purple, Color.White);
				addButton.Size = new Size(170, 34);
				addButton.Click += (s, e) => { ShowAddClientWindow(); LoadHomeClientsGrid(panel, searchBox.Text); LayoutHomePage(); };
				panel.Controls.Add(addButton);

				Button viewButton = CreateDialogButton("Просмотр клиентов", 575, 52, lightPurple, purple);
				viewButton.Size = new Size(180, 34);
				viewButton.Click += (s, e) => ShowClientsWindow();
				panel.Controls.Add(viewButton);

				DataGridView grid = new DataGridView
				{
					Name = "grid",
					Location = new Point(25, 100),
					BackgroundColor = Color.White,
					BorderStyle = BorderStyle.None,
					AllowUserToAddRows = false,
					AllowUserToDeleteRows = false,
					ReadOnly = true,
					RowHeadersVisible = false,
					AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
					SelectionMode = DataGridViewSelectionMode.FullRowSelect,
					ScrollBars = ScrollBars.Both
				};
				grid.CellDoubleClick += (s, e) =>
				{
					if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Cells["Id"].Value != null)
					{
						ShowClientDetailsWindow(Convert.ToInt32(grid.Rows[e.RowIndex].Cells["Id"].Value));
						LoadHomeClientsGrid(panel, searchBox.Text);
					}
				};
				panel.Controls.Add(grid);

				searchBox.GotFocus += (s, e) => { if (searchBox.Text == "Поиск по логину, имени или телефону") searchBox.Text = ""; };
				searchBox.TextChanged += (s, e) => LoadHomeClientsGrid(panel, searchBox.Text);
			}

			panel.Visible = true;
			panel.SetBounds(x, y, width, height);
			SetRoundedRegion(panel, 18);

			Control[] grids = panel.Controls.Find("grid", true);
			if (grids.Length > 0)
				grids[0].Size = new Size(width - 50, height - 125);

			Control[] searches = panel.Controls.Find("search", true);
			string searchText = searches.Length > 0 ? searches[0].Text : "";
			LoadHomeClientsGrid(panel, searchText);

			return panel;
		}

		private void LoadHomeClientsGrid(Panel panel, string searchText)
		{
			try
			{
				Control[] grids = panel.Controls.Find("grid", true);
				if (grids.Length == 0 || grids[0] is not DataGridView grid)
					return;

				bool placeholder = string.IsNullOrWhiteSpace(searchText) || searchText == "Поиск по логину, имени или телефону";
				DataTable table = ExecuteDataTable(@"SELECT Id, Login AS [Логин], FullName AS [Имя], Phone AS [Телефон],
					BalanceMoney AS [Деньги], BonusBalance AS [Бонусы], RemainingSeconds AS [Осталось]
					FROM Clients
					WHERE ISNULL(IsDeleted,0)=0
					  AND (@Search='' OR Login LIKE @Like OR FullName LIKE @Like OR Phone LIKE @Like)
					ORDER BY Id DESC",
					new SqlParameter("@Search", placeholder ? "" : searchText.Trim()),
					new SqlParameter("@Like", "%" + (placeholder ? "" : searchText.Trim()) + "%"));

				table.Columns.Add("Остаток времени", typeof(string));
				foreach (DataRow row in table.Rows)
					row["Остаток времени"] = FormatSeconds(Convert.ToInt32(row["Осталось"]));
				table.Columns.Remove("Осталось");

				grid.DataSource = table;
				if (grid.Columns.Contains("Id")) grid.Columns["Id"].Visible = false;
			}
			catch { }
		}


		private Panel CreateOrUpdateHomeStatCard(string name, string title, string value, int x, int y, int width, int height, Color accent)
		{
			Panel card = null;

			foreach (Control control in panelContent.Controls)
			{
				if (control is Panel panel && panel.Name == name)
				{
					card = panel;
					break;
				}
			}

			if (card == null)
			{
				card = new Panel
				{
					Name = name,
					BackColor = Color.White
				};

				Panel dot = new Panel
				{
					Name = "dot",
					BackColor = accent,
					Location = new Point(25, 28),
					Size = new Size(42, 42)
				};
				SetRoundedRegion(dot, 21);
				card.Controls.Add(dot);

				card.Controls.Add(new Label
				{
					Name = "title",
					Font = new Font("Segoe UI", 10, FontStyle.Bold),
					ForeColor = Color.FromArgb(70, 75, 110),
					AutoSize = true,
					Location = new Point(85, 22)
				});

				card.Controls.Add(new Label
				{
					Name = "value",
					Font = new Font("Segoe UI", 22, FontStyle.Bold),
					ForeColor = Color.FromArgb(35, 40, 65),
					AutoSize = true,
					Location = new Point(85, 47)
				});

				panelContent.Controls.Add(card);
			}

			card.Visible = true;
			card.SetBounds(x, y, width, height);
			card.BackColor = Color.White;
			SetRoundedRegion(card, 18);

			foreach (Control child in card.Controls)
			{
				if (child.Name == "title")
					child.Text = title;
				else if (child.Name == "value")
					child.Text = value;
				else if (child.Name == "dot")
				{
					child.BackColor = accent;
					SetRoundedRegion(child, 21);
				}
			}

			return card;
		}

		private Button CreateOrUpdateBalanceButton(string name, string text, int x, int y, int width, int height)
		{
			Button button = null;

			foreach (Control control in panelContent.Controls)
			{
				if (control is Button existingButton && existingButton.Name == name)
				{
					button = existingButton;
					break;
				}
			}

			if (button == null)
			{
				button = new Button
				{
					Name = name,
					Text = text,
					Font = new Font("Segoe UI", 12, FontStyle.Bold),
					ForeColor = purple,
					BackColor = Color.White,
					FlatStyle = FlatStyle.Flat,
					Cursor = Cursors.Hand
				};
				button.FlatAppearance.BorderSize = 0;
				button.Click += (s, e) => ShowTopUpBalanceWindow();
				panelContent.Controls.Add(button);
			}

			button.Visible = true;
			button.Text = text;
			button.SetBounds(x, y, width, height);
			button.BackColor = Color.White;
			button.ForeColor = purple;
			SetRoundedRegion(button, 18);
			return button;
		}

		private Button CreateOrUpdateClientsButton(string name, string text, int x, int y, int width, int height)
		{
			Button button = null;

			foreach (Control control in panelContent.Controls)
			{
				if (control is Button existingButton && existingButton.Name == name)
				{
					button = existingButton;
					break;
				}
			}

			if (button == null)
			{
				button = new Button
				{
					Name = name,
					Text = text,
					Font = new Font("Segoe UI", 12, FontStyle.Bold),
					ForeColor = purple,
					BackColor = Color.White,
					FlatStyle = FlatStyle.Flat,
					Cursor = Cursors.Hand
				};
				button.FlatAppearance.BorderSize = 0;
				button.Click += (s, e) => ShowClientsWindow();
				panelContent.Controls.Add(button);
			}

			button.Visible = true;
			button.Text = text;
			button.SetBounds(x, y, width, height);
			button.BackColor = Color.White;
			button.ForeColor = purple;
			SetRoundedRegion(button, 18);
			return button;
		}

		private void LayoutCardText(Panel card, Label textLabel, Label valueLabel)
		{
			card.BackColor = Color.White;
			textLabel.BackColor = Color.Transparent;
			valueLabel.BackColor = Color.Transparent;
			textLabel.Location = new Point(25, 25);
			valueLabel.Location = new Point(25, 60);
		}

		private void LayoutQuickAccessItems()
		{
			Panel[] items =
			{
				quickSubscriptions,
				quickVisits,
				quickTariffs,
				quickReports
			};

			int itemWidth = 160;
			int itemHeight = 105;
			int totalItemsWidth = itemWidth * items.Length;
			int freeSpace = panelQuickAccess.Width - totalItemsWidth;
			int gap = Math.Max(20, freeSpace / (items.Length + 1));

			int startX = gap;
			int y = 70;

			for (int i = 0; i < items.Length; i++)
			{
				Panel item = items[i];
				item.Visible = true;
				item.Size = new Size(itemWidth, itemHeight);
				item.Location = new Point(startX + i * (itemWidth + gap), y);
				item.BackColor = Color.GhostWhite;
				item.Cursor = Cursors.Hand;
				item.Controls.Clear();

				SetRoundedRegion(item, 18);
			}

			AddQuickAccessIcon(quickSubscriptions, "Frame 9.png", "Абонементы", btnSubscriptions_Click);
			AddQuickAccessIcon(quickVisits, "Frame 11.png", "Посещения", btnVisits_Click);
			AddQuickAccessIcon(quickTariffs, "Frame 13.png", "Тарифы", btnTariffs_Click);
			AddQuickAccessIcon(quickReports, "Frame 14.png", "Отчёты", btnReports_Click);
		}

		private void AddQuickAccessIcon(Panel panel, string imageFileName, string title, EventHandler clickHandler)
		{
			PictureBox icon = new PictureBox
			{
				Size = new Size(58, 58),
				Location = new Point((panel.Width - 58) / 2, 10),
				SizeMode = PictureBoxSizeMode.Zoom,
				BackColor = Color.Transparent,
				Cursor = Cursors.Hand
			};

			string imagePath = ResolveImagePath(imageFileName);

			if (System.IO.File.Exists(imagePath))
			{
				using (System.IO.FileStream stream = new System.IO.FileStream(imagePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
				{
					icon.Image = Image.FromStream(stream);
				}
			}

			Label label = new Label
			{
				Text = title,
				Font = new Font("Segoe UI", 10, FontStyle.Bold),
				ForeColor = textColor,
				BackColor = Color.Transparent,
				AutoSize = false,
				TextAlign = ContentAlignment.MiddleCenter,
				Size = new Size(panel.Width, 25),
				Location = new Point(0, 74),
				Cursor = Cursors.Hand
			};

			panel.Click -= clickHandler;
			panel.Click += clickHandler;

			icon.Click -= clickHandler;
			icon.Click += clickHandler;

			label.Click -= clickHandler;
			label.Click += clickHandler;

			panel.Controls.Add(icon);
			panel.Controls.Add(label);
		}

		private string ResolveImagePath(string imageFileName)
		{
			string startupImagesPath = System.IO.Path.Combine(Application.StartupPath, "Images", imageFileName);
			if (System.IO.File.Exists(startupImagesPath))
				return startupImagesPath;

			string currentDirectoryImagesPath = System.IO.Path.Combine(Environment.CurrentDirectory, "Images", imageFileName);
			if (System.IO.File.Exists(currentDirectoryImagesPath))
				return currentDirectoryImagesPath;

			System.IO.DirectoryInfo? directory = new System.IO.DirectoryInfo(Application.StartupPath);

			while (directory != null)
			{
				string imagesPath = System.IO.Path.Combine(directory.FullName, "Images", imageFileName);
				if (System.IO.File.Exists(imagesPath))
					return imagesPath;

				directory = directory.Parent;
			}

			return startupImagesPath;
		}

		private void CenterChildrenInQuickPanel(Panel panel)
		{
			foreach (Control child in panel.Controls)
			{
				child.BackColor = Color.Transparent;

				if (child is PictureBox picture)
				{
					picture.SizeMode = PictureBoxSizeMode.Zoom;
					picture.Size = new Size(panel.Width, 68);
					picture.Location = new Point(0, 8);
				}

				if (child is Label label)
				{
					label.AutoSize = true;
					label.Top = 78;
					label.Left = (panel.Width - label.Width) / 2;
				}
			}
		}

		private void RoundHomeControls()
		{
			SetRoundedRegion(panelContent, 24);
			SetRoundedRegion(btnHome, 12);
			SetRoundedRegion(btnSubscriptions, 12);
			SetRoundedRegion(btnVisits, 12);
			SetRoundedRegion(btnTariffs, 12);
			SetRoundedRegion(btnReports, 12);
			SetRoundedRegion(btnExit, 12);

			SetRoundedRegion(cardSubscriptions, 18);
			SetRoundedRegion(cardVisits, 18);
			SetRoundedRegion(panelQuickAccess, 18);

			SetRoundedRegion(quickSubscriptions, 18);
			SetRoundedRegion(quickVisits, 18);
			SetRoundedRegion(quickTariffs, 18);
			SetRoundedRegion(quickReports, 18);
		}

		private void SetupQuickAccessClicks()
		{
			AddClickToAll(quickSubscriptions, btnSubscriptions_Click);
			AddClickToAll(quickVisits, btnVisits_Click);
			AddClickToAll(quickTariffs, btnTariffs_Click);
			AddClickToAll(quickReports, btnReports_Click);
		}

		private void AddClickToAll(Control control, EventHandler handler)
		{
			control.Cursor = Cursors.Hand;
			control.Click -= handler;
			control.Click += handler;

			foreach (Control child in control.Controls)
			{
				child.Cursor = Cursors.Hand;
				child.Click -= handler;
				child.Click += handler;
			}
		}

		private void RebuildCurrentSection()
		{
			switch (currentSection)
			{
				case "Абонементы":
					ShowSubscriptionsPage();
					break;

				case "Посещения":
					ShowVisitsPage();
					break;

				case "Тарифы":
					ShowTariffsPage();
					break;

				case "Отчёты":
					ShowReportsPage();
					break;
			}
		}

		private void SeedDefaultData()
		{
			try
			{
				if (GetScalarInt("SELECT COUNT(*) FROM Tariffs") == 0)
				{
					ExecuteNonQuery("INSERT INTO Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'Стандарт', 170, 60, N'Стандартный тариф')");
					ExecuteNonQuery("INSERT INTO Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'VIP', 210, 60, N'VIP тариф')");
					ExecuteNonQuery("INSERT INTO Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'HomeVIP', 230, 60, N'Домашний VIP тариф')");
					ExecuteNonQuery("INSERT INTO Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'DUO', 400, 60, N'Тариф для двоих')");
					ExecuteNonQuery("INSERT INTO Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'TRIO', 210, 60, N'Тариф для троих')");
					ExecuteNonQuery("INSERT INTO Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'SOLO', 250, 60, N'Индивидуальный тариф')");
				}

				if (GetScalarInt("SELECT COUNT(*) FROM Subscriptions") == 0)
				{
					ExecuteNonQuery("INSERT INTO Subscriptions (Name, Price, DurationDays, HoursCount, Description) VALUES (N'Абонемент на 5 часов', 1500, 30, 5, N'Пакет на 5 часов')");
					ExecuteNonQuery("INSERT INTO Subscriptions (Name, Price, DurationDays, HoursCount, Description) VALUES (N'Абонемент на 3 часа', 1000, 30, 3, N'Пакет на 3 часа')");
					ExecuteNonQuery("INSERT INTO Subscriptions (Name, Price, DurationDays, HoursCount, Description) VALUES (N'Ночной', 2000, 30, 8, N'Ночной абонемент')");
					ExecuteNonQuery("INSERT INTO Subscriptions (Name, Price, DurationDays, HoursCount, Description) VALUES (N'Дневной', 1800, 30, 8, N'Дневной абонемент')");
				}
			}
			catch
			{
				// Если таблицы ещё не созданы, приложение всё равно запустится.
			}
		}

		private void ShowClientsWindow()
		{
			Form form = new Form
			{
				Text = "Клиенты",
				Size = new Size(1050, 650),
				StartPosition = FormStartPosition.CenterParent,
				BackColor = Color.White
			};

			form.Controls.Add(new Label { Text = "Зарегистрированные клиенты", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(25, 20) });

			TextBox searchBox = new TextBox { Font = new Font("Segoe UI", 10), ForeColor = Color.Gray, Text = "Поиск по логину, имени или телефону", Location = new Point(25, 65), Size = new Size(320, 30) };
			form.Controls.Add(searchBox);

			Button addButton = CreateDialogButton("Добавить клиента", 370, 62, purple, Color.White);
			addButton.Size = new Size(170, 34);
			form.Controls.Add(addButton);

			Button purchasesButton = CreateDialogButton("Покупки абонементов", 555, 62, lightPurple, purple);
			purchasesButton.Size = new Size(200, 34);
			purchasesButton.Click += (s, e) => ShowIssuedSubscriptionsManagerWindow();
			form.Controls.Add(purchasesButton);

			DataGridView grid = new DataGridView
			{
				Location = new Point(25, 110),
				Size = new Size(990, 470),
				BackgroundColor = Color.White,
				BorderStyle = BorderStyle.FixedSingle,
				AllowUserToAddRows = false,
				AllowUserToDeleteRows = false,
				ReadOnly = true,
				RowHeadersVisible = false,
				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
				SelectionMode = DataGridViewSelectionMode.FullRowSelect,
				ScrollBars = ScrollBars.Both
			};
			form.Controls.Add(grid);

			Action loadClients = () =>
			{
				try
				{
					bool placeholder = string.IsNullOrWhiteSpace(searchBox.Text) || searchBox.Text == "Поиск по логину, имени или телефону";
					DataTable table = ExecuteDataTable(@"SELECT Id, Login AS [Логин], FullName AS [Имя], Phone AS [Телефон],
						BalanceMoney AS [Деньги], BonusBalance AS [Бонусы], RemainingSeconds AS [Осталось]
						FROM Clients
						WHERE ISNULL(IsDeleted,0)=0 AND (@Search='' OR Login LIKE @Like OR FullName LIKE @Like OR Phone LIKE @Like)
						ORDER BY Id DESC",
						new SqlParameter("@Search", placeholder ? "" : searchBox.Text.Trim()),
						new SqlParameter("@Like", "%" + (placeholder ? "" : searchBox.Text.Trim()) + "%"));
					table.Columns.Add("Остаток времени", typeof(string));
					foreach (DataRow row in table.Rows)
						row["Остаток времени"] = FormatSeconds(Convert.ToInt32(row["Осталось"]));
					table.Columns.Remove("Осталось");
					grid.DataSource = table;
					if (grid.Columns.Contains("Id")) grid.Columns["Id"].Visible = false;
				}
				catch (Exception ex) { ShowSqlError(ex); }
			};

			searchBox.GotFocus += (s, e) => { if (searchBox.Text == "Поиск по логину, имени или телефону") searchBox.Text = ""; };
			searchBox.TextChanged += (s, e) => loadClients();
			addButton.Click += (s, e) => { ShowAddClientWindow(); loadClients(); LayoutHomePage(); };
			grid.CellDoubleClick += (s, e) =>
			{
				if (e.RowIndex < 0 || grid.Rows[e.RowIndex].Cells["Id"].Value == null) return;
				ShowClientDetailsWindow(Convert.ToInt32(grid.Rows[e.RowIndex].Cells["Id"].Value));
				loadClients();
				LayoutHomePage();
			};

			loadClients();
			form.ShowDialog(this);
		}


		private void ShowAddClientWindow()
		{
			Form form = new Form
			{
				Text = "Добавить клиента",
				Size = new Size(430, 450),
				StartPosition = FormStartPosition.CenterParent,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox = false,
				MinimizeBox = false,
				BackColor = Color.White
			};

			form.Controls.Add(new Label { Text = "Добавить клиента", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 25) });
			form.Controls.Add(new Label { Text = "Логин", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 95) });
			TextBox loginBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 120), Size = new Size(340, 30) };
			form.Controls.Add(loginBox);
			form.Controls.Add(new Label { Text = "Имя", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 165) });
			TextBox nameBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 190), Size = new Size(340, 30) };
			form.Controls.Add(nameBox);
			form.Controls.Add(new Label { Text = "Телефон", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 235) });
			TextBox phoneBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 260), Size = new Size(340, 30) };
			form.Controls.Add(phoneBox);

			Button saveButton = CreateDialogButton("Добавить", 30, 345, purple, Color.White);
			saveButton.Click += (s, e) =>
			{
				try
				{
					GetOrCreateClientId(loginBox.Text.Trim(), nameBox.Text.Trim(), phoneBox.Text.Trim());
					MessageBox.Show("Клиент добавлен.", "Готово");
					form.Close();
				}
				catch (Exception ex)
				{
					ShowSqlError(ex);
				}
			};
			form.Controls.Add(saveButton);
			Button cancelButton = CreateDialogButton("Отмена", 205, 345, lightPurple, purple);
			cancelButton.Click += (s, e) => form.Close();
			form.Controls.Add(cancelButton);
			form.ShowDialog(this);
		}

		private void ShowClientDetailsWindow(int clientId)
		{
			try
			{
				DataTable table = ExecuteDataTable("SELECT Login, FullName, Phone, BalanceMoney, BonusBalance, RemainingMinutes, RemainingSeconds, CreatedAt FROM Clients WHERE Id=@Id", new SqlParameter("@Id", clientId));
				if (table.Rows.Count == 0) return;
				DataRow row = table.Rows[0];

				Form form = new Form
				{
					Text = "Карточка клиента",
					Size = new Size(520, 670),
					StartPosition = FormStartPosition.CenterParent,
					FormBorderStyle = FormBorderStyle.FixedDialog,
					MaximizeBox = false,
					MinimizeBox = false,
					BackColor = Color.White
				};

				form.Controls.Add(new Label { Text = "Клиент", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 25) });
				form.Controls.Add(new Label { Text = "Остаток времени: " + FormatSeconds(Convert.ToInt32(row["RemainingSeconds"])), Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = purple, AutoSize = true, Location = new Point(30, 60) });

				form.Controls.Add(new Label { Text = "Логин", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 95) });
				TextBox loginBox = new TextBox { Text = row["Login"].ToString(), Font = new Font("Segoe UI", 10), Location = new Point(30, 120), Size = new Size(430, 30) };
				form.Controls.Add(loginBox);
				form.Controls.Add(new Label { Text = "Имя", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 155) });
				TextBox nameBox = new TextBox { Text = row["FullName"].ToString(), Font = new Font("Segoe UI", 10), Location = new Point(30, 180), Size = new Size(430, 30) };
				form.Controls.Add(nameBox);
				form.Controls.Add(new Label { Text = "Телефон", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 215) });
				TextBox phoneBox = new TextBox { Text = row["Phone"].ToString(), Font = new Font("Segoe UI", 10), Location = new Point(30, 240), Size = new Size(430, 30) };
				form.Controls.Add(phoneBox);
				form.Controls.Add(new Label { Text = "Деньги", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 275) });
				NumericUpDown moneyBox = new NumericUpDown { DecimalPlaces = 2, Maximum = 1000000, Minimum = 0, Value = Convert.ToDecimal(row["BalanceMoney"]), Font = new Font("Segoe UI", 10), Location = new Point(30, 300), Size = new Size(430, 30) };
				form.Controls.Add(moneyBox);
				form.Controls.Add(new Label { Text = "Бонусы", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 335) });
				NumericUpDown bonusBox = new NumericUpDown { Maximum = 1000000, Minimum = 0, Value = Convert.ToDecimal(row["BonusBalance"]), Font = new Font("Segoe UI", 10), Location = new Point(30, 360), Size = new Size(430, 30) };
				form.Controls.Add(bonusBox);
				form.Controls.Add(new Label { Text = "Остаток времени, минут", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 395) });
				NumericUpDown minutesBox = new NumericUpDown { Maximum = 1000000, Minimum = 0, Value = Convert.ToDecimal(row["RemainingMinutes"]), Font = new Font("Segoe UI", 10), Location = new Point(30, 420), Size = new Size(430, 30) };
				form.Controls.Add(minutesBox);

				if (!IsSeniorAdmin())
				{
					loginBox.ReadOnly = true;
					nameBox.ReadOnly = true;
					phoneBox.ReadOnly = true;
					moneyBox.Enabled = false;
					bonusBox.Enabled = false;
					minutesBox.Enabled = false;
				}

				Button historyButton = CreateDialogButton("История операций", 30, 470, lightPurple, purple);
				historyButton.Size = new Size(200, 38);
				historyButton.Click += (s, e) => ShowClientOperationsWindow(clientId);
				form.Controls.Add(historyButton);

				Button bonusButton = CreateDialogButton("Бонусный счёт", 260, 470, lightPurple, purple);
				bonusButton.Size = new Size(200, 38);
				bonusButton.Click += (s, e) => ShowBonusAccountWindow(clientId);
				form.Controls.Add(bonusButton);

				Button saveButton = CreateDialogButton("Сохранить", 30, 540, purple, Color.White);
				saveButton.Click += (s, e) =>
				{
					try
					{
						if (string.IsNullOrWhiteSpace(loginBox.Text)) { MessageBox.Show("Введите логин клиента."); return; }
						ExecuteNonQuery(@"UPDATE Clients SET Login=@Login, FullName=@FullName, Phone=@Phone, BalanceMoney=@Money, BonusBalance=@Bonus, RemainingMinutes=@Minutes, RemainingSeconds=@Seconds WHERE Id=@Id",
							new SqlParameter("@Login", loginBox.Text.Trim()),
							new SqlParameter("@FullName", nameBox.Text.Trim()),
							new SqlParameter("@Phone", phoneBox.Text.Trim()),
							new SqlParameter("@Money", moneyBox.Value),
							new SqlParameter("@Bonus", Convert.ToInt32(bonusBox.Value)),
							new SqlParameter("@Minutes", Convert.ToInt32(minutesBox.Value)),
							new SqlParameter("@Seconds", Convert.ToInt32(minutesBox.Value) * 60),
							new SqlParameter("@Id", clientId));
						AddClientOperation(clientId, "Редактирование клиента", 0, 0, 0, "Изменены данные клиента");
						MessageBox.Show("Данные клиента сохранены.", "Готово");
						form.Close();
					}
					catch (Exception ex) { ShowSqlError(ex); }
				};
				saveButton.Visible = IsSeniorAdmin();
				form.Controls.Add(saveButton);

				Button deleteButton = CreateDialogButton("Удалить", 180, 540, Color.FromArgb(255, 235, 235), Color.DarkRed);
				deleteButton.Click += (s, e) =>
				{
					if (MessageBox.Show("Удалить клиента?", "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
					{
						ExecuteNonQuery("UPDATE Clients SET IsDeleted=1 WHERE Id=@Id", new SqlParameter("@Id", clientId));
						form.Close();
					}
				};
				deleteButton.Visible = IsSeniorAdmin();
				form.Controls.Add(deleteButton);

				Button closeButton = CreateDialogButton("Закрыть", 330, 540, lightPurple, purple);
				closeButton.Click += (s, e) => form.Close();
				form.Controls.Add(closeButton);

				form.ShowDialog(this);
			}
			catch (Exception ex) { ShowSqlError(ex); }
		}



		private void ShowClientOperationsWindow(int clientId)
		{
			Form form = new Form { Text = "История операций клиента", Size = new Size(900, 560), StartPosition = FormStartPosition.CenterParent, BackColor = Color.White };
			form.Controls.Add(new Label { Text = "История операций клиента", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(25, 20) });
			DataGridView grid = CreateReportGrid(25, 75, 830, 420);
			grid.Columns.Add("Date", "Дата");
			grid.Columns.Add("Type", "Операция");
			grid.Columns.Add("Money", "Деньги");
			grid.Columns.Add("Bonus", "Бонусы");
			grid.Columns.Add("Minutes", "Минуты");
			grid.Columns.Add("Comment", "Комментарий");
			try
			{
				DataTable table = ExecuteDataTable(@"SELECT OperationDate, OperationType, AmountMoney, AmountBonus, MinutesChanged, Comment FROM ClientOperations WHERE ClientId=@ClientId ORDER BY OperationDate DESC", new SqlParameter("@ClientId", clientId));
				foreach (DataRow row in table.Rows)
					grid.Rows.Add(Convert.ToDateTime(row["OperationDate"]).ToString("dd.MM.yyyy HH:mm"), row["OperationType"].ToString(), FormatMoney(Convert.ToDecimal(row["AmountMoney"])), row["AmountBonus"].ToString(), row["MinutesChanged"].ToString(), row["Comment"].ToString());
			}
			catch (Exception ex) { ShowSqlError(ex); }
			form.Controls.Add(grid);
			form.ShowDialog(this);
		}

		private void ShowBonusAccountWindow(int clientId)
		{
			Form form = new Form { Text = "Бонусный счёт", Size = new Size(820, 540), StartPosition = FormStartPosition.CenterParent, BackColor = Color.White };
			int bonus = GetScalarInt("SELECT ISNULL(BonusBalance,0) FROM Clients WHERE Id=@Id", new SqlParameter("@Id", clientId));
			form.Controls.Add(new Label { Text = "Бонусный счёт: " + bonus + " бонусов", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(25, 20) });
			DataGridView grid = CreateReportGrid(25, 75, 750, 390);
			grid.Columns.Add("Date", "Дата");
			grid.Columns.Add("Reason", "Операция");
			grid.Columns.Add("Amount", "Бонусы");
			grid.Columns.Add("Type", "Тип");
			try
			{
				DataTable table = ExecuteDataTable(@"SELECT OperationDate, Reason, Amount, OperationType FROM BonusOperations WHERE ClientId=@ClientId ORDER BY OperationDate DESC", new SqlParameter("@ClientId", clientId));
				foreach (DataRow row in table.Rows)
					grid.Rows.Add(Convert.ToDateTime(row["OperationDate"]).ToString("dd.MM.yyyy HH:mm"), row["Reason"].ToString(), row["Amount"].ToString(), row["OperationType"].ToString());
			}
			catch (Exception ex) { ShowSqlError(ex); }
			form.Controls.Add(grid);
			form.ShowDialog(this);
		}

		private void ShowIssuedSubscriptionsManagerWindow()
		{
			Form form = new Form { Text = "Покупки абонементов", Size = new Size(950, 580), StartPosition = FormStartPosition.CenterParent, BackColor = Color.White };
			form.Controls.Add(new Label { Text = "Покупки абонементов", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(25, 20) });
			Button addButton = CreateDialogButton("Пробить абонемент", 700, 25, purple, Color.White);
			addButton.Size = new Size(180, 38);
			form.Controls.Add(addButton);
			DataGridView grid = CreateReportGrid(25, 80, 875, 420);
			grid.Columns.Add("Id", "Id");
			grid.Columns.Add("Client", "Клиент");
			grid.Columns.Add("Subscription", "Абонемент");
			grid.Columns.Add("Issued", "Дата покупки");
			grid.Columns.Add("Until", "Действует до");
			grid.Columns.Add("Computer", "Компьютер");
			grid.Columns["Id"].Visible = false;
			form.Controls.Add(grid);
			Action load = () =>
			{
				grid.Rows.Clear();
				try
				{
					DataTable table = ExecuteDataTable(@"SELECT i.Id, c.Login, s.Name AS SubscriptionName, i.IssuedAt, i.ValidUntil, ISNULL(pc.Name,'') AS ComputerName
						FROM IssuedSubscriptions i INNER JOIN Clients c ON c.Id=i.ClientId INNER JOIN Subscriptions s ON s.Id=i.SubscriptionId LEFT JOIN Computers pc ON pc.Id=i.ComputerId ORDER BY i.IssuedAt DESC");
					foreach (DataRow row in table.Rows)
						grid.Rows.Add(row["Id"], row["Login"], row["SubscriptionName"], Convert.ToDateTime(row["IssuedAt"]).ToString("dd.MM.yyyy HH:mm"), Convert.ToDateTime(row["ValidUntil"]).ToString("dd.MM.yyyy"), row["ComputerName"]);
				}
				catch (Exception ex) { ShowSqlError(ex); }
			};
			addButton.Click += (s, e) => { ShowIssueSubscriptionWindow(); load(); };
			grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) { ShowIssuedSubscriptionEditWindow(Convert.ToInt32(grid.Rows[e.RowIndex].Cells["Id"].Value)); load(); } };
			load();
			form.ShowDialog(this);
		}

		private void ShowIssuedSubscriptionEditWindow(int issuedId)
		{
			DataTable table = ExecuteDataTable(@"SELECT i.Id, i.ValidUntil, c.Login, s.Name AS SubscriptionName FROM IssuedSubscriptions i INNER JOIN Clients c ON c.Id=i.ClientId INNER JOIN Subscriptions s ON s.Id=i.SubscriptionId WHERE i.Id=@Id", new SqlParameter("@Id", issuedId));
			if (table.Rows.Count == 0) return;
			DataRow row = table.Rows[0];
			Form form = new Form { Text = "Редактировать покупку", Size = new Size(430, 300), StartPosition = FormStartPosition.CenterParent, BackColor = Color.White, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
			form.Controls.Add(new Label { Text = "Покупка абонемента", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 25) });
			form.Controls.Add(new Label { Text = "Клиент: " + row["Login"], Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 85) });
			form.Controls.Add(new Label { Text = "Абонемент: " + row["SubscriptionName"], Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 115) });
			form.Controls.Add(new Label { Text = "Действует до", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 150) });
			DateTimePicker until = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = Convert.ToDateTime(row["ValidUntil"]), Location = new Point(30, 175), Size = new Size(340, 30) };
			form.Controls.Add(until);
			Button save = CreateDialogButton("Сохранить", 30, 225, purple, Color.White);
			save.Click += (s, e) => { ExecuteNonQuery("UPDATE IssuedSubscriptions SET ValidUntil=@Until WHERE Id=@Id", new SqlParameter("@Until", until.Value.Date), new SqlParameter("@Id", issuedId)); form.Close(); };
			form.Controls.Add(save);
			Button del = CreateDialogButton("Удалить", 210, 225, Color.FromArgb(255, 235, 235), Color.DarkRed);
			del.Click += (s, e) => { if (MessageBox.Show("Удалить покупку абонемента?", "Удаление", MessageBoxButtons.YesNo) == DialogResult.Yes) { ExecuteNonQuery("DELETE FROM IssuedSubscriptions WHERE Id=@Id", new SqlParameter("@Id", issuedId)); form.Close(); } };
			form.Controls.Add(del);
			form.ShowDialog(this);
		}

		private void ShowTopUpBalanceWindow()
		{
			Form form = new Form
			{
				Text = "Пополнить баланс",
				Size = new Size(520, 720),
				StartPosition = FormStartPosition.CenterParent,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox = false,
				MinimizeBox = false,
				BackColor = Color.White
			};

			form.Controls.Add(new Label
			{
				Text = "Пополнить баланс",
				Font = new Font("Segoe UI", 18, FontStyle.Bold),
				ForeColor = textColor,
				AutoSize = true,
				Location = new Point(30, 25)
			});

			form.Controls.Add(new Label { Text = "Искать клиента по", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 80) });
			ComboBox searchTypeBox = new ComboBox
			{
				Font = new Font("Segoe UI", 10),
				Location = new Point(30, 105),
				Size = new Size(440, 30),
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			searchTypeBox.Items.Add("По логину");
			searchTypeBox.Items.Add("По телефону");
			searchTypeBox.SelectedIndex = 0;
			form.Controls.Add(searchTypeBox);

			Label clientLabel = new Label { Text = "Логин клиента", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 145) };
			form.Controls.Add(clientLabel);
			TextBox clientBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 170), Size = new Size(440, 30) };
			form.Controls.Add(clientBox);

			form.Controls.Add(new Label { Text = "Имя клиента (если новый)", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 210) });
			TextBox nameBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 235), Size = new Size(440, 30) };
			form.Controls.Add(nameBox);

			form.Controls.Add(new Label { Text = "Тип пополнения", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 275) });
			ComboBox typeBox = new ComboBox
			{
				Font = new Font("Segoe UI", 10),
				Location = new Point(30, 300),
				Size = new Size(440, 30),
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			typeBox.Items.Add("Деньгами");
			typeBox.Items.Add("Бонусами");
			typeBox.SelectedIndex = 0;
			form.Controls.Add(typeBox);

			form.Controls.Add(new Label { Text = "Тариф", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 340) });
			ComboBox tariffBox = new ComboBox
			{
				Font = new Font("Segoe UI", 10),
				Location = new Point(30, 365),
				Size = new Size(440, 30),
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			form.Controls.Add(tariffBox);

			try
			{
				DataTable tariffs = ExecuteDataTable("SELECT Id, Name, Price, DurationMinutes FROM Tariffs WHERE ISNULL(IsDeleted,0)=0 ORDER BY Name");
				tariffBox.DataSource = tariffs;
				tariffBox.DisplayMember = "Name";
				tariffBox.ValueMember = "Id";
			}
			catch { }

			form.Controls.Add(new Label { Text = "Номер компьютера", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 405) });
			ComboBox computerBox = new ComboBox
			{
				Font = new Font("Segoe UI", 10),
				Location = new Point(30, 430),
				Size = new Size(440, 30),
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			form.Controls.Add(computerBox);
			try
			{
				DataTable computers = ExecuteDataTable("SELECT Id, Name FROM Computers WHERE ISNULL(IsActive,1)=1 ORDER BY Id");
				computerBox.DataSource = computers;
				computerBox.DisplayMember = "Name";
				computerBox.ValueMember = "Id";
			}
			catch { }

			form.Controls.Add(new Label { Text = "Сумма / количество бонусов", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 470) });
			NumericUpDown amountBox = new NumericUpDown
			{
				Font = new Font("Segoe UI", 10),
				Location = new Point(30, 495),
				Size = new Size(440, 30),
				Minimum = 0,
				Maximum = 1000000,
				Increment = 10
			};
			form.Controls.Add(amountBox);

			Label timeLabel = new Label
			{
				Text = "Будет начислено времени: 0 мин.",
				Font = new Font("Segoe UI", 10, FontStyle.Bold),
				ForeColor = Color.FromArgb(70, 75, 110),
				AutoSize = true,
				Location = new Point(30, 532)
			};
			form.Controls.Add(timeLabel);

			Action updateTime = () =>
			{
				try
				{
					if (tariffBox.SelectedItem is DataRowView row)
					{
						decimal price = Convert.ToDecimal(row["Price"]);
						int duration = Convert.ToInt32(row["DurationMinutes"]);
						decimal amount = amountBox.Value;
						int minutes = price <= 0 ? 0 : Convert.ToInt32(Math.Floor((amount / price) * duration));
						timeLabel.Text = "Будет начислено времени: " + minutes + " мин.";
					}
				}
				catch
				{
					timeLabel.Text = "Будет начислено времени: 0 мин.";
				}
			};

			amountBox.ValueChanged += (s, e) => updateTime();
			tariffBox.SelectedIndexChanged += (s, e) => updateTime();
			searchTypeBox.SelectedIndexChanged += (s, e) =>
			{
				clientLabel.Text = searchTypeBox.Text == "По телефону" ? "Телефон клиента" : "Логин клиента";
				clientBox.Clear();
			};

			form.Controls.Add(new Label { Text = "Комментарий", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 560) });
			TextBox commentBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 585), Size = new Size(440, 30) };
			form.Controls.Add(commentBox);

			Button saveButton = CreateDialogButton("Пополнить", 30, 635, purple, Color.White);
			saveButton.Click += (s, e) =>
			{
				try
				{
					if (string.IsNullOrWhiteSpace(clientBox.Text))
					{
						MessageBox.Show(searchTypeBox.Text == "По телефону" ? "Введите телефон клиента." : "Введите логин клиента.");
						return;
					}

					if (amountBox.Value <= 0)
					{
						MessageBox.Show("Введите сумму больше 0.");
						return;
					}

					if (!(tariffBox.SelectedItem is DataRowView selectedTariff))
					{
						MessageBox.Show("Выберите тариф.");
						return;
					}

					int tariffId = Convert.ToInt32(selectedTariff["Id"]);
					string tariffName = selectedTariff["Name"].ToString();
					decimal tariffPrice = Convert.ToDecimal(selectedTariff["Price"]);
					int tariffDuration = Convert.ToInt32(selectedTariff["DurationMinutes"]);
					decimal amount = amountBox.Value;
					int minutes = tariffPrice <= 0 ? 0 : Convert.ToInt32(Math.Floor((amount / tariffPrice) * tariffDuration));

					int? computerId = null;
					if (computerBox.SelectedItem is DataRowView selectedComputer)
						computerId = Convert.ToInt32(selectedComputer["Id"]);

					string login = searchTypeBox.Text == "По логину" ? clientBox.Text.Trim() : "";
					string phone = searchTypeBox.Text == "По телефону" ? clientBox.Text.Trim() : "";
					string fullName = string.IsNullOrWhiteSpace(nameBox.Text) ? login : nameBox.Text.Trim();
					int clientId = GetOrCreateClientId(login, fullName, phone);
					string comment = string.IsNullOrWhiteSpace(commentBox.Text) ? "Пополнение по тарифу " + tariffName : commentBox.Text.Trim();

					if (typeBox.Text == "Бонусами")
					{
						ExecuteNonQuery("UPDATE Clients SET BonusBalance = ISNULL(BonusBalance, 0) + @Amount, RemainingMinutes = ISNULL(RemainingMinutes, 0) + @Minutes, RemainingSeconds = ISNULL(RemainingSeconds, 0) + @Seconds WHERE Id = @ClientId",
							new SqlParameter("@Amount", Convert.ToInt32(amount)),
							new SqlParameter("@Minutes", minutes),
							new SqlParameter("@Seconds", minutes * 60),
							new SqlParameter("@ClientId", clientId));

						ExecuteNonQuery("INSERT INTO BonusOperations (ClientId, Reason, Amount, OperationType) VALUES (@ClientId, @Reason, @Amount, @Type)",
							new SqlParameter("@ClientId", clientId),
							new SqlParameter("@Reason", comment),
							new SqlParameter("@Amount", Convert.ToInt32(amount)),
							new SqlParameter("@Type", "Пополнение"));
					}
					else
					{
						ExecuteNonQuery("UPDATE Clients SET BalanceMoney = ISNULL(BalanceMoney, 0) + @Amount, RemainingMinutes = ISNULL(RemainingMinutes, 0) + @Minutes, RemainingSeconds = ISNULL(RemainingSeconds, 0) + @Seconds WHERE Id = @ClientId",
							new SqlParameter("@Amount", amount),
							new SqlParameter("@Minutes", minutes),
							new SqlParameter("@Seconds", minutes * 60),
							new SqlParameter("@ClientId", clientId));
					}

					ExecuteNonQuery("INSERT INTO Sales (ClientId, TariffId, ComputerId, SaleDate, Amount, PaymentType, MinutesAdded, Comment) VALUES (@ClientId, @TariffId, @ComputerId, GETDATE(), @Amount, @PaymentType, @MinutesAdded, @Comment)",
						new SqlParameter("@ClientId", clientId),
						new SqlParameter("@TariffId", tariffId),
						new SqlParameter("@ComputerId", (object?)computerId ?? DBNull.Value),
						new SqlParameter("@Amount", amount),
						new SqlParameter("@PaymentType", typeBox.Text),
						new SqlParameter("@MinutesAdded", minutes),
						new SqlParameter("@Comment", comment));

					ExecuteNonQuery("INSERT INTO Visits (ClientId, ComputerId, TariffId, StartTime, EndTime, Status) VALUES (@ClientId, @ComputerId, @TariffId, GETDATE(), DATEADD(MINUTE, @Minutes, GETDATE()), N'Активно')",
						new SqlParameter("@ClientId", clientId),
						new SqlParameter("@ComputerId", (object?)computerId ?? DBNull.Value),
						new SqlParameter("@TariffId", tariffId),
						new SqlParameter("@Minutes", minutes));

					object visitIdObj = ExecuteScalar("SELECT TOP 1 Id FROM Visits WHERE ClientId=@ClientId ORDER BY Id DESC", new SqlParameter("@ClientId", clientId));
					int visitId = visitIdObj == null || visitIdObj == DBNull.Value ? 0 : Convert.ToInt32(visitIdObj);
					ExecuteNonQuery("INSERT INTO ClientSessions (ClientId, ComputerId, VisitId, StartedAt, EndAt, RemainingSeconds, Status) VALUES (@ClientId, @ComputerId, @VisitId, GETDATE(), DATEADD(SECOND, @Seconds, GETDATE()), @Seconds, N'Активно')",
						new SqlParameter("@ClientId", clientId),
						new SqlParameter("@ComputerId", (object?)computerId ?? DBNull.Value),
						new SqlParameter("@VisitId", visitId == 0 ? (object)DBNull.Value : visitId),
						new SqlParameter("@Seconds", minutes * 60));
					AddClientOperation(clientId, typeBox.Text == "Бонусами" ? "Пополнение бонусами" : "Пополнение деньгами", typeBox.Text == "Бонусами" ? 0 : amount, typeBox.Text == "Бонусами" ? Convert.ToInt32(amount) : 0, minutes, comment);

					MessageBox.Show("Баланс пополнен. Время начислено: " + minutes + " мин. Данные добавлены в клиенты, посещения и отчёты.", "Готово");
					form.Close();
					ShowHomePage();
				}
				catch (Exception ex)
				{
					ShowSqlError(ex);
				}
			};
			form.Controls.Add(saveButton);

			Button cancelButton = CreateDialogButton("Отмена", 305, 635, lightPurple, purple);
			cancelButton.Click += (s, e) => form.Close();
			form.Controls.Add(cancelButton);

			updateTime();
			form.ShowDialog(this);
		}

		private void ShowSubscriptionsPage()
		{
			currentSection = "Абонементы";
			HideHomeControls();
			CreateEmptyCurrentPage();
			currentPage.AutoScroll = true;

			int margin = 45;
			int gap = 22;
			int width = GetContentWidth(margin);

			currentPage.Controls.Add(CreatePageTitle("Абонементы", margin, 35));
			currentPage.Controls.Add(CreateSubtitle("Управление абонементами клиентов", margin, 88));

			Button addButton = CreatePurpleButton("+  Добавить абонемент", margin + width - 210, 50, 210, 42);
			addButton.BackColor = Color.FromArgb(86, 42, 245);
			addButton.Click += (s, e) => ShowAddSubscriptionWindow();
			addButton.Visible = IsSeniorAdmin();
			currentPage.Controls.Add(addButton);

			int cardTop = 140;
			int cardWidth = (width - gap * 2) / 3;
			int cardHeight = 135;
			string totalSubs = "0";
			string activeSubs = "0";
			try
			{
				totalSubs = GetScalarInt("SELECT COUNT(*) FROM Subscriptions WHERE ISNULL(IsDeleted,0)=0").ToString();
				activeSubs = GetScalarInt("SELECT COUNT(*) FROM IssuedSubscriptions WHERE ValidUntil >= GETDATE()").ToString();
			}
			catch { }

			currentPage.Controls.Add(CreateTopCard("Всего абонементов", totalSubs, "Всего в системе", margin, cardTop, cardWidth, cardHeight, purple));
			currentPage.Controls.Add(CreateTopCard("Активных абонементов", activeSubs, "Сейчас действуют в клубе", margin + cardWidth + gap, cardTop, cardWidth, cardHeight, green));

			Button issueButton = CreatePurpleButton("Пробить клиенту абонемент", margin + (cardWidth + gap) * 2, cardTop, cardWidth, cardHeight);
			issueButton.Font = new Font("Segoe UI", 13, FontStyle.Bold);
			issueButton.BackColor = Color.FromArgb(86, 42, 245);
			issueButton.Click += (s, e) => ShowIssueSubscriptionWindow();
			currentPage.Controls.Add(issueButton);
			SetRoundedRegion(issueButton, 18);

			Panel listPanel = CreateWhitePanel(margin, cardTop + cardHeight + 25, width, 500);
			listPanel.AutoScroll = true;
			currentPage.Controls.Add(listPanel);
			AddSearchAndSort(listPanel, "Поиск абонемента...", false);

			int rowTop = 92;
			int rowHeight = 80;
			int rowWidth = listPanel.Width - 45;
			try
			{
				DataTable table = ExecuteDataTable("SELECT Name, Description, DurationDays, Price FROM Subscriptions WHERE ISNULL(IsDeleted,0)=0 ORDER BY Name");
				int i = 0;
				foreach (DataRow row in table.Rows)
				{
					Color back = i % 2 == 0 ? Color.FromArgb(235, 245, 255) : Color.FromArgb(242, 235, 255);
					Color accent = i % 2 == 0 ? blue : purple;
					listPanel.Controls.Add(CreateRow(row["Name"].ToString(), row["Description"].ToString(), "Срок действия", row["DurationDays"].ToString() + " дней", "Цена", FormatMoney(Convert.ToDecimal(row["Price"])), rowTop + (rowHeight + 10) * i, rowWidth, back, accent, false, true));
					i++;
				}
			}
			catch { }

			Panel issuedPanel = CreateWhitePanel(margin, listPanel.Bottom + 25, width, 430);
			issuedPanel.AutoScroll = true;
			currentPage.Controls.Add(issuedPanel);
			issuedPanel.Controls.Add(new Label { Text = "Пробитые абонементы", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(25, 22) });

			DataGridView issuedGrid = CreateReportGrid(25, 70, issuedPanel.Width - 50, issuedPanel.Height - 100);
			issuedGrid.Columns.Add("Subscription", "Абонемент");
			issuedGrid.Columns.Add("Date", "Дата");
			issuedGrid.Columns.Add("Time", "Время");
			issuedGrid.Columns.Add("Until", "Действует до");
			issuedGrid.Columns.Add("Client", "Клиент / логин");
			issuedGrid.Columns.Add("Computer", "Компьютер");

			try
			{
				DataTable issuedTable = ExecuteDataTable(@"SELECT s.Name AS SubscriptionName, i.IssuedAt, i.ValidUntil, c.Login AS ClientLogin, ISNULL(pc.Name, '') AS ComputerName FROM IssuedSubscriptions i INNER JOIN Subscriptions s ON s.Id = i.SubscriptionId INNER JOIN Clients c ON c.Id = i.ClientId LEFT JOIN Computers pc ON pc.Id = i.ComputerId ORDER BY i.IssuedAt DESC");
				foreach (DataRow row in issuedTable.Rows)
				{
					DateTime issuedAt = Convert.ToDateTime(row["IssuedAt"]);
					DateTime validUntil = Convert.ToDateTime(row["ValidUntil"]);
					issuedGrid.Rows.Add(row["SubscriptionName"].ToString(), issuedAt.ToString("dd.MM.yyyy"), issuedAt.ToString("HH:mm"), validUntil.ToString("dd.MM.yyyy"), row["ClientLogin"].ToString(), row["ComputerName"].ToString());
				}
			}
			catch { }

			issuedPanel.Controls.Add(issuedGrid);
			currentPage.AutoScrollMinSize = new Size(width + margin * 2, issuedPanel.Bottom + 100);
		}

		private void ShowAddSubscriptionWindow()
		{
			if (!IsSeniorAdmin()) { ShowAccessDenied(); return; }

			Form form = new Form
			{
				Text = "Добавить абонемент",
				Size = new Size(450, 500),
				StartPosition = FormStartPosition.CenterParent,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox = false,
				MinimizeBox = false,
				BackColor = Color.White
			};

			form.Controls.Add(new Label
			{
				Text = "Добавить абонемент",
				Font = new Font("Segoe UI", 18, FontStyle.Bold),
				ForeColor = textColor,
				AutoSize = true,
				Location = new Point(30, 25)
			});

			form.Controls.Add(new Label { Text = "Название", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 85) });
			TextBox nameBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 110), Size = new Size(370, 30) };
			form.Controls.Add(nameBox);

			form.Controls.Add(new Label { Text = "Цена", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 150) });
			TextBox priceBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 175), Size = new Size(370, 30) };
			form.Controls.Add(priceBox);

			form.Controls.Add(new Label { Text = "Срок действия в днях", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 215) });
			TextBox durationBox = new TextBox { Text = "30", Font = new Font("Segoe UI", 10), Location = new Point(30, 240), Size = new Size(370, 30) };
			form.Controls.Add(durationBox);

			form.Controls.Add(new Label { Text = "Количество часов", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 280) });
			TextBox hoursBox = new TextBox { Text = "1", Font = new Font("Segoe UI", 10), Location = new Point(30, 305), Size = new Size(370, 30) };
			form.Controls.Add(hoursBox);

			Button saveButton = CreateDialogButton("Добавить", 30, 400, purple, Color.White);
			saveButton.Click += (s, e) =>
			{
				try
				{
					if (string.IsNullOrWhiteSpace(nameBox.Text))
					{
						MessageBox.Show("Введите название абонемента.");
						return;
					}

					decimal price = ParseMoney(priceBox.Text);
					int durationDays = ParseNumber(durationBox.Text);
					int hoursCount = ParseNumber(hoursBox.Text);

					if (durationDays <= 0)
						durationDays = 30;

					if (hoursCount <= 0)
						hoursCount = 1;

					ExecuteNonQuery(
						"INSERT INTO Subscriptions (Name, Price, DurationDays, HoursCount, Description) VALUES (@Name, @Price, @DurationDays, @HoursCount, @Description)",
						new SqlParameter("@Name", nameBox.Text.Trim()),
						new SqlParameter("@Price", price),
						new SqlParameter("@DurationDays", durationDays),
						new SqlParameter("@HoursCount", hoursCount),
						new SqlParameter("@Description", "Абонемент"));

					MessageBox.Show("Абонемент успешно добавлен в базу.", "Готово");
					form.Close();
					ShowSubscriptionsPage();
				}
				catch (Exception ex)
				{
					ShowSqlError(ex);
				}
			};
			form.Controls.Add(saveButton);

			Button cancelButton = CreateDialogButton("Отмена", 225, 400, lightPurple, purple);
			cancelButton.Click += (s, e) => form.Close();
			form.Controls.Add(cancelButton);

			form.ShowDialog(this);
		}


		private void ShowIssueSubscriptionWindow()
		{
			Form form = new Form
			{
				Text = "Пробить клиенту абонемент",
				Size = new Size(460, 460),
				StartPosition = FormStartPosition.CenterParent,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox = false,
				MinimizeBox = false,
				BackColor = Color.White
			};

			form.Controls.Add(new Label { Text = "Пробить абонемент", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 25) });
			form.Controls.Add(new Label { Text = "Клиент", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 85) });
			TextBox clientBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 110), Size = new Size(380, 30) };
			form.Controls.Add(clientBox);
			form.Controls.Add(new Label { Text = "Телефон", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 155) });
			TextBox phoneBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 180), Size = new Size(380, 30) };
			form.Controls.Add(phoneBox);
			form.Controls.Add(new Label { Text = "Абонемент", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 225) });

			ComboBox subBox = new ComboBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 250), Size = new Size(380, 30), DropDownStyle = ComboBoxStyle.DropDownList };
			try
			{
				DataTable subs = ExecuteDataTable("SELECT Id, Name, Price, DurationDays, HoursCount FROM Subscriptions WHERE ISNULL(IsDeleted,0)=0 ORDER BY Name");
				subBox.DataSource = subs;
				subBox.DisplayMember = "Name";
				subBox.ValueMember = "Id";
			}
			catch { }
			form.Controls.Add(subBox);

			Button saveButton = CreateDialogButton("Пробить", 30, 335, purple, Color.White);
			saveButton.Click += (s, e) =>
			{
				try
				{
					if (string.IsNullOrWhiteSpace(clientBox.Text))
					{
						MessageBox.Show("Введите клиента.");
						return;
					}
					if (!(subBox.SelectedItem is DataRowView selectedSub))
					{
						MessageBox.Show("Выберите абонемент.");
						return;
					}
					int clientId = GetOrCreateClientId(clientBox.Text.Trim(), clientBox.Text.Trim(), phoneBox.Text.Trim());
					int subscriptionId = Convert.ToInt32(selectedSub["Id"]);
					decimal price = Convert.ToDecimal(selectedSub["Price"]);
					int durationDays = Convert.ToInt32(selectedSub["DurationDays"]);
					int hoursCount = selectedSub.Row.Table.Columns.Contains("HoursCount") ? Convert.ToInt32(selectedSub["HoursCount"]) : 0;
					int minutesAdded = Math.Max(0, hoursCount * 60);

					decimal currentBalance = GetScalarDecimal("SELECT ISNULL(BalanceMoney,0) FROM Clients WHERE Id = @ClientId", new SqlParameter("@ClientId", clientId));
					if (currentBalance < price)
					{
						MessageBox.Show("Недостаточно средств на балансе клиента. Сначала пополните баланс.");
						return;
					}

					ExecuteNonQuery("UPDATE Clients SET BalanceMoney = BalanceMoney - @Price, RemainingMinutes = ISNULL(RemainingMinutes,0) + @Minutes, RemainingSeconds = ISNULL(RemainingSeconds,0) + @Seconds WHERE Id = @ClientId",
						new SqlParameter("@Price", price),
						new SqlParameter("@Minutes", minutesAdded),
						new SqlParameter("@Seconds", minutesAdded * 60),
						new SqlParameter("@ClientId", clientId));

					ExecuteNonQuery("INSERT INTO IssuedSubscriptions (ClientId, SubscriptionId, IssuedAt, ValidUntil) VALUES (@ClientId, @SubscriptionId, GETDATE(), DATEADD(DAY, @Days, GETDATE()))",
						new SqlParameter("@ClientId", clientId),
						new SqlParameter("@SubscriptionId", subscriptionId),
						new SqlParameter("@Days", durationDays));

					ExecuteNonQuery("INSERT INTO Sales (ClientId, SubscriptionId, SaleDate, Amount, PaymentType, MinutesAdded, Comment) VALUES (@ClientId, @SubscriptionId, GETDATE(), @Amount, N'Абонемент', @MinutesAdded, N'Пробит абонемент')",
						new SqlParameter("@ClientId", clientId),
						new SqlParameter("@SubscriptionId", subscriptionId),
						new SqlParameter("@Amount", price),
						new SqlParameter("@MinutesAdded", minutesAdded));

					ExecuteNonQuery("INSERT INTO Visits (ClientId, SubscriptionId, StartTime, EndTime, Status) VALUES (@ClientId, @SubscriptionId, GETDATE(), DATEADD(MINUTE, @Minutes, GETDATE()), N'Активно')",
						new SqlParameter("@ClientId", clientId),
						new SqlParameter("@SubscriptionId", subscriptionId),
						new SqlParameter("@Minutes", minutesAdded));

					object visitIdObj = ExecuteScalar("SELECT TOP 1 Id FROM Visits WHERE ClientId=@ClientId ORDER BY Id DESC", new SqlParameter("@ClientId", clientId));
					int visitId = visitIdObj == null || visitIdObj == DBNull.Value ? 0 : Convert.ToInt32(visitIdObj);
					ExecuteNonQuery("INSERT INTO ClientSessions (ClientId, VisitId, StartedAt, EndAt, RemainingSeconds, Status) VALUES (@ClientId, @VisitId, GETDATE(), DATEADD(SECOND, @Seconds, GETDATE()), @Seconds, N'Активно')",
						new SqlParameter("@ClientId", clientId),
						new SqlParameter("@VisitId", visitId == 0 ? (object)DBNull.Value : visitId),
						new SqlParameter("@Seconds", minutesAdded * 60));
					AddClientOperation(clientId, "Покупка абонемента", -price, 0, minutesAdded, "Пробит абонемент");

					MessageBox.Show("Абонемент успешно пробит. С баланса списано: " + FormatMoney(price) + ". Начислено времени: " + minutesAdded + " мин.", "Готово");
					form.Close();
					ShowSubscriptionsPage();
				}
				catch (Exception ex)
				{
					ShowSqlError(ex);
				}
			};
			form.Controls.Add(saveButton);

			Button cancelButton = CreateDialogButton("Отмена", 245, 335, lightPurple, purple);
			cancelButton.Click += (s, e) => form.Close();
			form.Controls.Add(cancelButton);
			form.ShowDialog(this);
		}

		private void ShowAddTariffWindow()
		{
			if (!IsSeniorAdmin()) { ShowAccessDenied(); return; }

			Form form = new Form
			{
				Text = "Добавить тариф",
				Size = new Size(450, 500),
				StartPosition = FormStartPosition.CenterParent,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox = false,
				MinimizeBox = false,
				BackColor = Color.White
			};

			form.Controls.Add(new Label
			{
				Text = "Добавить тариф",
				Font = new Font("Segoe UI", 18, FontStyle.Bold),
				ForeColor = textColor,
				AutoSize = true,
				Location = new Point(30, 25)
			});

			form.Controls.Add(new Label { Text = "Название", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 85) });
			TextBox nameBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 110), Size = new Size(370, 30) };
			form.Controls.Add(nameBox);

			form.Controls.Add(new Label { Text = "Цена", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 150) });
			TextBox priceBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 175), Size = new Size(370, 30) };
			form.Controls.Add(priceBox);

			form.Controls.Add(new Label { Text = "Длительность в минутах", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 215) });
			TextBox durationBox = new TextBox { Text = "60", Font = new Font("Segoe UI", 10), Location = new Point(30, 240), Size = new Size(370, 30) };
			form.Controls.Add(durationBox);

			Button saveButton = CreateDialogButton("Добавить", 30, 400, purple, Color.White);
			saveButton.Click += (s, e) =>
			{
				try
				{
					if (string.IsNullOrWhiteSpace(nameBox.Text))
					{
						MessageBox.Show("Введите название тарифа.");
						return;
					}

					decimal price = ParseMoney(priceBox.Text);
					int durationMinutes = ParseNumber(durationBox.Text);
					if (durationMinutes <= 0)
						durationMinutes = 60;

					ExecuteNonQuery(
						"INSERT INTO Tariffs (Name, Price, DurationMinutes, Description) VALUES (@Name, @Price, @DurationMinutes, @Description)",
						new SqlParameter("@Name", nameBox.Text.Trim()),
						new SqlParameter("@Price", price),
						new SqlParameter("@DurationMinutes", durationMinutes),
						new SqlParameter("@Description", "Тариф"));

					MessageBox.Show("Тариф успешно добавлен в базу.", "Готово");
					form.Close();
					ShowTariffsPage();
				}
				catch (Exception ex)
				{
					ShowSqlError(ex);
				}
			};
			form.Controls.Add(saveButton);

			Button cancelButton = CreateDialogButton("Отмена", 225, 400, lightPurple, purple);
			cancelButton.Click += (s, e) => form.Close();
			form.Controls.Add(cancelButton);

			form.ShowDialog(this);
		}

		private void ShowTariffsPage()
		{
			currentSection = "Тарифы";
			HideHomeControls();
			CreateEmptyCurrentPage();

			int margin = 45;
			int gap = 22;
			int width = GetContentWidth(margin);

			currentPage.Controls.Add(CreatePageTitle("Тарифы", margin, 35));
			currentPage.Controls.Add(CreateSubtitle("Управление тарифами клуба", margin, 88));

			Button addTariffButton = CreatePurpleButton("+  Добавить тариф", margin + width - 185, 50, 185, 42);
			addTariffButton.BackColor = Color.FromArgb(86, 42, 245);
			addTariffButton.Click += (s, e) => ShowAddTariffWindow();
			currentPage.Controls.Add(addTariffButton);

			int cardTop = 140;
			int cardWidth = (width - gap) / 2;
			int cardHeight = 135;
			string popular = "VIP";
			string avg = "0 ₽";
			try
			{
				object pop = ExecuteScalar("SELECT TOP 1 Name FROM Tariffs WHERE ISNULL(IsDeleted,0)=0 ORDER BY Id");
				if (pop != null && pop != DBNull.Value) popular = pop.ToString();
				avg = FormatMoney(GetScalarDecimal("SELECT ISNULL(AVG(Price),0) FROM Tariffs WHERE ISNULL(IsDeleted,0)=0"));
			}
			catch { }

			currentPage.Controls.Add(CreateTopCard("Популярный тариф", popular, "Чаще всего выбирают", margin, cardTop, cardWidth, cardHeight, blue));
			currentPage.Controls.Add(CreateTopCard("Средняя цена", avg, "По всем тарифам", margin + cardWidth + gap, cardTop, cardWidth, cardHeight, green));

			Panel listPanel = CreateWhitePanel(margin, cardTop + cardHeight + 25, width, 520);
			listPanel.AutoScroll = true;
			currentPage.Controls.Add(listPanel);
			AddSearchAndSort(listPanel, "Поиск тарифа...", false);

			int rowTop = 100;
			int rowHeight = 95;
			int rowWidth = listPanel.Width - 45;
			try
			{
				DataTable table = ExecuteDataTable("SELECT Name, Description, DurationMinutes, Price FROM Tariffs WHERE ISNULL(IsDeleted,0)=0 ORDER BY Name");
				int i = 0;
				foreach (DataRow row in table.Rows)
				{
					Color back = i % 3 == 0 ? Color.FromArgb(235, 245, 255) : (i % 3 == 1 ? Color.FromArgb(242, 235, 255) : Color.FromArgb(255, 245, 232));
					Color accent = i % 3 == 0 ? blue : (i % 3 == 1 ? purple : Color.FromArgb(255, 130, 0));
					int minutes = Convert.ToInt32(row["DurationMinutes"]);
					string duration = minutes >= 60 && minutes % 60 == 0 ? (minutes / 60).ToString() + " час" : minutes.ToString() + " мин";
					listPanel.Controls.Add(CreateRow(row["Name"].ToString(), row["Description"].ToString(), "Длительность", duration, "Стоимость", FormatMoney(Convert.ToDecimal(row["Price"])), rowTop + (rowHeight + 15) * i, rowWidth, back, accent, false, true));
					i++;
				}
			}
			catch { }
		}

		private void ShowVisitsPage()
		{
			currentSection = "Посещения";
			HideHomeControls();
			CreateEmptyCurrentPage();

			int margin = 45;
			int gap = 22;
			int width = GetContentWidth(margin);
			currentPage.Controls.Add(CreatePageTitle("Посещения", margin, 35));

			Button addVisitButton = CreatePurpleButton("+  Отметить посещение", margin + width - 220, 50, 220, 42);
			addVisitButton.Click += (s, e) => { ShowVisitEditWindow(null); ShowVisitsPage(); };
			currentPage.Controls.Add(addVisitButton);

			int cardTop = 125;
			int cardWidth = (width - gap * 2) / 3;
			int cardHeight = 115;
			string today = "0";
			string month = "0";
			try
			{
				today = GetScalarInt("SELECT COUNT(*) FROM Visits WHERE CAST(StartTime AS date)=CAST(GETDATE() AS date)").ToString();
				month = GetScalarInt("SELECT COUNT(*) FROM Visits WHERE MONTH(StartTime)=MONTH(GETDATE()) AND YEAR(StartTime)=YEAR(GETDATE())").ToString();
			}
			catch { }

			currentPage.Controls.Add(CreateTopCard("Сегодня", today, "За текущий день", margin, cardTop, cardWidth, cardHeight, purple));
			currentPage.Controls.Add(CreateTopCard("В этом месяце", month, "За месяц", margin + cardWidth + gap, cardTop, cardWidth, cardHeight, blue));

			Button periodButton = CreatePurpleButton("Выбрать период посещений", margin + (cardWidth + gap) * 2, cardTop, cardWidth, cardHeight);
			periodButton.Font = new Font("Segoe UI", 13, FontStyle.Bold);
			periodButton.BackColor = Color.FromArgb(86, 42, 245);
			periodButton.Click += (s, e) => ShowVisitsPeriodWindow();
			currentPage.Controls.Add(periodButton);
			SetRoundedRegion(periodButton, 18);

			Panel tablePanel = CreateWhitePanel(margin, cardTop + cardHeight + 25, width, Math.Max(520, panelContent.ClientSize.Height - (cardTop + cardHeight + 90)));
			tablePanel.AutoScroll = true;
			currentPage.Controls.Add(tablePanel);
			tablePanel.Controls.Add(new Label { Text = "Список посещений", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(25, 22) });
			TextBox searchBox = new TextBox { Text = "Поиск по гостю или телефону", Font = new Font("Segoe UI", 10), ForeColor = Color.Gray, Location = new Point(25, 65), Size = new Size(300, 30) };
			tablePanel.Controls.Add(searchBox);

			DataGridView grid = CreateReportGrid(25, 110, tablePanel.Width - 50, tablePanel.Height - 140);
			grid.Columns.Add("Id", "Id");
			grid.Columns.Add("Date", "Дата");
			grid.Columns.Add("Time", "Время");
			grid.Columns.Add("Guest", "Гость");
			grid.Columns.Add("Phone", "Телефон");
			grid.Columns.Add("Computer", "Компьютер");
			grid.Columns.Add("Service", "Услуга");
			grid.Columns.Add("Status", "Статус");
			grid.Columns.Add("Remaining", "Осталось");
			grid.Columns["Id"].Visible = false;

			Action loadVisits = () =>
			{
				grid.Rows.Clear();
				try
				{
					bool placeholder = string.IsNullOrWhiteSpace(searchBox.Text) || searchBox.Text == "Поиск по гостю или телефону";
					DataTable table = ExecuteDataTable(@"SELECT v.Id, v.StartTime, v.EndTime, c.Login, c.Phone, ISNULL(pc.Name,'') AS ComputerName, ISNULL(t.Name, s.Name) AS ServiceName, v.Status
						FROM Visits v INNER JOIN Clients c ON c.Id = v.ClientId
						LEFT JOIN Computers pc ON pc.Id = v.ComputerId
						LEFT JOIN Tariffs t ON t.Id = v.TariffId
						LEFT JOIN Subscriptions s ON s.Id = v.SubscriptionId
						WHERE (@Search='' OR c.Login LIKE @Like OR c.Phone LIKE @Like)
						ORDER BY v.StartTime DESC",
						new SqlParameter("@Search", placeholder ? "" : searchBox.Text.Trim()),
						new SqlParameter("@Like", "%" + (placeholder ? "" : searchBox.Text.Trim()) + "%"));
					foreach (DataRow row in table.Rows)
					{
						DateTime start = Convert.ToDateTime(row["StartTime"]);
						DateTime? end = row["EndTime"] == DBNull.Value ? null : Convert.ToDateTime(row["EndTime"]);
						int remaining = end.HasValue ? Math.Max(0, Convert.ToInt32((end.Value - DateTime.Now).TotalSeconds)) : 0;
						grid.Rows.Add(row["Id"], start.ToString("dd.MM.yyyy"), start.ToString("HH:mm"), row["Login"], row["Phone"], row["ComputerName"], row["ServiceName"], row["Status"], FormatSeconds(remaining));
					}
				}
				catch (Exception ex) { ShowSqlError(ex); }
			};

			searchBox.GotFocus += (s, e) => { if (searchBox.Text == "Поиск по гостю или телефону") searchBox.Text = ""; };
			searchBox.TextChanged += (s, e) => loadVisits();
			grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) { ShowVisitEditWindow(Convert.ToInt32(grid.Rows[e.RowIndex].Cells["Id"].Value)); loadVisits(); } };
			tablePanel.Controls.Add(grid);
			loadVisits();
		}



		private void ShowVisitEditWindow(int? visitId)
		{
			Form form = new Form { Text = visitId.HasValue ? "Редактировать посещение" : "Отметить посещение", Size = new Size(500, 560), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Color.White };
			form.Controls.Add(new Label { Text = visitId.HasValue ? "Редактировать посещение" : "Отметить посещение", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 25) });
			form.Controls.Add(new Label { Text = "Логин клиента", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 85) });
			TextBox loginBox = new TextBox { Font = new Font("Segoe UI", 10), Location = new Point(30, 110), Size = new Size(420, 30) };
			form.Controls.Add(loginBox);
			form.Controls.Add(new Label { Text = "Компьютер", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 150) });
			ComboBox computerBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(30, 175), Size = new Size(420, 30) };
			try { DataTable computers = ExecuteDataTable("SELECT Id, Name FROM Computers WHERE ISNULL(IsActive,1)=1 ORDER BY Id"); computerBox.DataSource = computers; computerBox.DisplayMember = "Name"; computerBox.ValueMember = "Id"; } catch { }
			form.Controls.Add(computerBox);
			form.Controls.Add(new Label { Text = "Тариф", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 215) });
			ComboBox tariffBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(30, 240), Size = new Size(420, 30) };
			try { DataTable tariffs = ExecuteDataTable("SELECT Id, Name, DurationMinutes FROM Tariffs WHERE ISNULL(IsDeleted,0)=0 ORDER BY Name"); tariffBox.DataSource = tariffs; tariffBox.DisplayMember = "Name"; tariffBox.ValueMember = "Id"; } catch { }
			form.Controls.Add(tariffBox);
			form.Controls.Add(new Label { Text = "Дата и время начала", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 280) });
			DateTimePicker startPicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy HH:mm", Value = DateTime.Now, Location = new Point(30, 305), Size = new Size(420, 30) };
			form.Controls.Add(startPicker);
			form.Controls.Add(new Label { Text = "Статус", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 345) });
			ComboBox statusBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(30, 370), Size = new Size(420, 30) };
			statusBox.Items.AddRange(new object[] { "Активно", "Завершено", "Отменено" });
			statusBox.SelectedIndex = 0;
			form.Controls.Add(statusBox);

			if (visitId.HasValue)
			{
				DataTable row = ExecuteDataTable(@"SELECT v.ClientId, c.Login, v.ComputerId, v.TariffId, v.StartTime, v.Status FROM Visits v INNER JOIN Clients c ON c.Id=v.ClientId WHERE v.Id=@Id", new SqlParameter("@Id", visitId.Value));
				if (row.Rows.Count > 0)
				{
					loginBox.Text = row.Rows[0]["Login"].ToString();
					startPicker.Value = Convert.ToDateTime(row.Rows[0]["StartTime"]);
					statusBox.Text = row.Rows[0]["Status"].ToString();
					if (row.Rows[0]["ComputerId"] != DBNull.Value) computerBox.SelectedValue = Convert.ToInt32(row.Rows[0]["ComputerId"]);
					if (row.Rows[0]["TariffId"] != DBNull.Value) tariffBox.SelectedValue = Convert.ToInt32(row.Rows[0]["TariffId"]);
				}
			}

			Button save = CreateDialogButton("Сохранить", 30, 450, purple, Color.White);
			save.Click += (s, e) =>
			{
				try
				{
					if (string.IsNullOrWhiteSpace(loginBox.Text)) { MessageBox.Show("Введите логин клиента."); return; }
					int clientId = GetOrCreateClientId(loginBox.Text.Trim(), loginBox.Text.Trim(), null);
					int? computerId = computerBox.SelectedItem is DataRowView pc ? Convert.ToInt32(pc["Id"]) : null;
					int? tariffId = tariffBox.SelectedItem is DataRowView tr ? Convert.ToInt32(tr["Id"]) : null;
					int minutes = tariffBox.SelectedItem is DataRowView tr2 ? Convert.ToInt32(tr2["DurationMinutes"]) : 60;
					if (visitId.HasValue)
						ExecuteNonQuery("UPDATE Visits SET ClientId=@ClientId, ComputerId=@ComputerId, TariffId=@TariffId, StartTime=@Start, EndTime=DATEADD(MINUTE,@Minutes,@Start), Status=@Status WHERE Id=@Id", new SqlParameter("@ClientId", clientId), new SqlParameter("@ComputerId", (object?)computerId ?? DBNull.Value), new SqlParameter("@TariffId", (object?)tariffId ?? DBNull.Value), new SqlParameter("@Start", startPicker.Value), new SqlParameter("@Minutes", minutes), new SqlParameter("@Status", statusBox.Text), new SqlParameter("@Id", visitId.Value));
					else
						ExecuteNonQuery("INSERT INTO Visits (ClientId, ComputerId, TariffId, StartTime, EndTime, Status) VALUES (@ClientId,@ComputerId,@TariffId,@Start,DATEADD(MINUTE,@Minutes,@Start),@Status)", new SqlParameter("@ClientId", clientId), new SqlParameter("@ComputerId", (object?)computerId ?? DBNull.Value), new SqlParameter("@TariffId", (object?)tariffId ?? DBNull.Value), new SqlParameter("@Start", startPicker.Value), new SqlParameter("@Minutes", minutes), new SqlParameter("@Status", statusBox.Text));
					AddClientOperation(clientId, visitId.HasValue ? "Редактирование посещения" : "Отметка посещения", 0, 0, 0, "Посещение сохранено");
					form.Close();
				}
				catch (Exception ex) { ShowSqlError(ex); }
			};
			form.Controls.Add(save);
			if (visitId.HasValue)
			{
				Button del = CreateDialogButton("Удалить", 205, 450, Color.FromArgb(255, 235, 235), Color.DarkRed);
				del.Click += (s, e) => { if (MessageBox.Show("Удалить посещение?", "Удаление", MessageBoxButtons.YesNo) == DialogResult.Yes) { ExecuteNonQuery("DELETE FROM Visits WHERE Id=@Id", new SqlParameter("@Id", visitId.Value)); form.Close(); } };
				form.Controls.Add(del);
			}
			Button close = CreateDialogButton("Отмена", visitId.HasValue ? 320 : 205, 450, lightPurple, purple);
			close.Click += (s, e) => form.Close();
			form.Controls.Add(close);
			form.ShowDialog(this);
		}

		private void ShowVisitsPeriodWindow()
		{
			Form form = new Form
			{
				Text = "Период посещений",
				Size = new Size(430, 420),
				StartPosition = FormStartPosition.CenterParent,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox = false,
				MinimizeBox = false,
				BackColor = Color.White
			};

			form.Controls.Add(new Label { Text = "Выберите период", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 25) });
			form.Controls.Add(new Label { Text = "С какого числа", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 85) });
			DateTimePicker fromPicker = new DateTimePicker { Font = new Font("Segoe UI", 10), Location = new Point(30, 110), Size = new Size(350, 30), Format = DateTimePickerFormat.Short };
			form.Controls.Add(fromPicker);
			form.Controls.Add(new Label { Text = "По какое число", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 150) });
			DateTimePicker toPicker = new DateTimePicker { Font = new Font("Segoe UI", 10), Location = new Point(30, 175), Size = new Size(350, 30), Format = DateTimePickerFormat.Short };
			form.Controls.Add(toPicker);

			Button showButton = CreateDialogButton("Показать", 30, 320, purple, Color.White);
			showButton.Click += (s, e) =>
			{
				try
				{
					int count = GetScalarInt("SELECT COUNT(*) FROM Visits WHERE StartTime >= @FromDate AND StartTime < DATEADD(DAY,1,@ToDate)",
						new SqlParameter("@FromDate", fromPicker.Value.Date),
						new SqlParameter("@ToDate", toPicker.Value.Date));
					MessageBox.Show($"Количество посещений за период с {fromPicker.Value:dd.MM.yyyy} по {toPicker.Value:dd.MM.yyyy}: {count}", "Посещения за период");
				}
				catch (Exception ex)
				{
					ShowSqlError(ex);
				}
			};
			form.Controls.Add(showButton);
			Button cancelButton = CreateDialogButton("Закрыть", 225, 320, lightPurple, purple);
			cancelButton.Click += (s, e) => form.Close();
			form.Controls.Add(cancelButton);
			form.ShowDialog(this);
		}

		private Button CreateDialogButton(string text, int x, int y, Color back, Color fore)
		{
			Button button = new Button
			{
				Text = text,
				Font = new Font("Segoe UI", 11, FontStyle.Bold),
				ForeColor = fore,
				BackColor = back,
				FlatStyle = FlatStyle.Flat,
				Location = new Point(x, y),
				Size = new Size(165, 40)
			};

			button.FlatAppearance.BorderSize = 0;
			return button;
		}

		private void ShowEditWindow(Panel row, string currentName, string currentDuration, string currentPrice)
		{
			if (!IsSeniorAdmin()) { ShowAccessDenied(); return; }

			Form form = new Form
			{
				Text = "Редактирование",
				Size = new Size(450, 500),
				StartPosition = FormStartPosition.CenterParent,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox = false,
				MinimizeBox = false,
				BackColor = Color.White
			};

			form.Controls.Add(new Label
			{
				Text = "Редактировать",
				Font = new Font("Segoe UI", 18, FontStyle.Bold),
				ForeColor = textColor,
				AutoSize = true,
				Location = new Point(30, 25)
			});

			form.Controls.Add(new Label { Text = "Изменить название", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 85) });
			TextBox nameBox = new TextBox { Text = currentName, Font = new Font("Segoe UI", 10), Location = new Point(30, 110), Size = new Size(370, 30) };
			form.Controls.Add(nameBox);

			form.Controls.Add(new Label { Text = "Изменить цену", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 150) });
			TextBox priceBox = new TextBox { Text = currentPrice, Font = new Font("Segoe UI", 10), Location = new Point(30, 175), Size = new Size(370, 30) };
			form.Controls.Add(priceBox);

			form.Controls.Add(new Label { Text = "Изменить длительность", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(30, 215) });
			TextBox durationBox = new TextBox { Text = currentDuration, Font = new Font("Segoe UI", 10), Location = new Point(30, 240), Size = new Size(370, 30) };
			form.Controls.Add(durationBox);

			Button saveButton = CreateDialogButton("Сохранить", 30, 400, purple, Color.White);
			saveButton.Click += (s, e) =>
			{
				try
				{
					decimal price = ParseMoney(priceBox.Text);
					int duration = ParseNumber(durationBox.Text);
					if (duration <= 0)
						duration = 1;

					int updatedTariffs = 0;
					using (SqlConnection connection = CreateConnection())
					using (SqlCommand command = new SqlCommand("UPDATE Tariffs SET Name = @NewName, Price = @Price, DurationMinutes = @Duration WHERE Name = @OldName", connection))
					{
						command.Parameters.AddWithValue("@NewName", nameBox.Text.Trim());
						command.Parameters.AddWithValue("@Price", price);
						command.Parameters.AddWithValue("@Duration", duration * 60);
						command.Parameters.AddWithValue("@OldName", currentName);
						connection.Open();
						updatedTariffs = command.ExecuteNonQuery();
					}

					int updatedSubscriptions = 0;
					using (SqlConnection connection = CreateConnection())
					using (SqlCommand command = new SqlCommand("UPDATE Subscriptions SET Name = @NewName, Price = @Price, DurationDays = @Duration WHERE Name = @OldName", connection))
					{
						command.Parameters.AddWithValue("@NewName", nameBox.Text.Trim());
						command.Parameters.AddWithValue("@Price", price);
						command.Parameters.AddWithValue("@Duration", duration);
						command.Parameters.AddWithValue("@OldName", currentName);
						connection.Open();
						updatedSubscriptions = command.ExecuteNonQuery();
					}

					if (updatedTariffs == 0 && updatedSubscriptions == 0)
						MessageBox.Show("Запись не найдена в базе. Если это тестовая строка интерфейса, сначала добавьте её через кнопку добавления.");
					else
						MessageBox.Show("Изменения успешно сохранены.", "Готово");

					form.Close();
					RebuildCurrentSection();
				}
				catch (Exception ex)
				{
					ShowSqlError(ex);
				}
			};
			form.Controls.Add(saveButton);

			Button cancelButton = CreateDialogButton("Отмена", 225, 400, lightPurple, purple);
			cancelButton.Click += (s, e) => form.Close();
			form.Controls.Add(cancelButton);

			form.ShowDialog(this);
		}

		private void ShowRowMenu(Panel row, string name, string duration, string price, Control anchor)
		{
			ContextMenuStrip menu = new ContextMenuStrip();

			ToolStripMenuItem editItem = new ToolStripMenuItem("Редактировать");
			editItem.Click += (s, e) => ShowEditWindow(row, name, duration, price);

			ToolStripMenuItem deleteItem = new ToolStripMenuItem("Удалить");
			deleteItem.Click += (s, e) =>
			{
				if (!IsSeniorAdmin()) { ShowAccessDenied(); return; }

				DialogResult result = MessageBox.Show(
					$"Удалить «{name}»?",
					"Удаление",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Question
				);

				if (result == DialogResult.Yes)
				{
					try
					{
						if (currentSection == "Тарифы")
						{
							ExecuteNonQuery("UPDATE Tariffs SET IsDeleted = 1 WHERE Name = @Name", new SqlParameter("@Name", name));
						}
						else if (currentSection == "Абонементы")
						{
							ExecuteNonQuery("UPDATE Subscriptions SET IsDeleted = 1 WHERE Name = @Name", new SqlParameter("@Name", name));
						}

						row.Parent?.Controls.Remove(row);
						row.Dispose();
					}
					catch (Exception ex)
					{
						ShowSqlError(ex);
					}
				}
			};

			menu.Items.Add(editItem);
			menu.Items.Add(deleteItem);
			menu.Show(anchor, new Point(0, anchor.Height));
		}

		private Panel CreateTopCard(string title, string value, string subtitle, int x, int y, int width, int height, Color color)
		{
			Panel card = new Panel
			{
				BackColor = Color.White,
				Location = new Point(x, y),
				Size = new Size(width, height)
			};
			SetRoundedRegion(card, 18);

			int iconY = Math.Max(20, (height - 58) / 2);
			int textX = 105;
			int titleY = Math.Max(18, height / 2 - 40);
			int valueY = Math.Max(42, height / 2 - 15);
			int subtitleY = Math.Max(78, height / 2 + 25);

			Panel icon = new Panel
			{
				BackColor = color,
				Location = new Point(28, iconY),
				Size = new Size(58, 58)
			};
			SetRoundedRegion(icon, 29);
			card.Controls.Add(icon);

			card.Controls.Add(new Label
			{
				Text = title,
				Font = new Font("Segoe UI", 10, FontStyle.Bold),
				ForeColor = Color.FromArgb(70, 75, 110),
				AutoSize = true,
				Location = new Point(textX, titleY)
			});

			card.Controls.Add(new Label
			{
				Text = value,
				Font = new Font("Segoe UI", 22, FontStyle.Bold),
				ForeColor = Color.FromArgb(60, 65, 130),
				AutoSize = true,
				Location = new Point(textX, valueY)
			});

			card.Controls.Add(new Label
			{
				Text = subtitle,
				Font = new Font("Segoe UI", 9),
				ForeColor = Color.FromArgb(70, 75, 110),
				AutoSize = true,
				Location = new Point(textX, subtitleY)
			});

			return card;
		}

		private Panel CreateRow(
			string name,
			string type,
			string title1,
			string value1,
			string title2,
			string value2,
			int y,
			int width,
			Color iconBack,
			Color accent,
			bool showStatus,
			bool useThreeDotsMenu)
		{
			Panel row = new Panel
			{
				BackColor = Color.White,
				Location = new Point(12, y),
				Size = new Size(width, 80)
			};
			SetRoundedRegion(row, 14);

			Panel icon = new Panel
			{
				BackColor = iconBack,
				Location = new Point(18, 14),
				Size = new Size(52, 52)
			};
			SetRoundedRegion(icon, 8);
			row.Controls.Add(icon);

			row.Controls.Add(new Label
			{
				Text = name,
				Font = new Font("Segoe UI", 13, FontStyle.Bold),
				ForeColor = Color.Black,
				AutoSize = true,
				Location = new Point(95, 18)
			});

			row.Controls.Add(new Label
			{
				Text = type,
				Font = new Font("Segoe UI", 9),
				ForeColor = accent,
				BackColor = iconBack,
				AutoSize = true,
				Location = new Point(95, 48),
				Padding = new Padding(8, 3, 8, 3)
			});

			int col1 = width / 2 - 70;
			int col2 = width / 2 + 170;

			row.Controls.Add(CreateSmallTitle(title1, col1, 18));
			row.Controls.Add(CreateSmallValue(value1, col1, 46));
			row.Controls.Add(CreateSmallTitle(title2, col2, 18));
			row.Controls.Add(CreateSmallValue(value2, col2, 46));

			Button moreButton = new Button
			{
				Text = "...",
				Font = new Font("Segoe UI", 14, FontStyle.Bold),
				ForeColor = Color.FromArgb(70, 75, 110),
				BackColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Size = new Size(45, 34),
				Location = new Point(width - 55, 23)
			};
			moreButton.FlatAppearance.BorderSize = 0;

			if (useThreeDotsMenu)
				moreButton.Click += (s, e) => ShowRowMenu(row, name, value1, value2, moreButton);

			row.Controls.Add(moreButton);

			return row;
		}

		private void AddSearchAndSort(Panel listPanel, string searchText, bool showStatusFilter)
		{
			TextBox searchBox = new TextBox
			{
				Text = searchText,
				Font = new Font("Segoe UI", 10),
				ForeColor = Color.Gray,
				Location = new Point(25, 28),
				Size = new Size(330, 32)
			};
			listPanel.Controls.Add(searchBox);

			Label sortLabel = CreateSmallTitle("Сортировка", listPanel.Width - 205, 18);
			listPanel.Controls.Add(sortLabel);

			ComboBox sortBox = new ComboBox
			{
				Font = new Font("Segoe UI", 10),
				ForeColor = Color.FromArgb(70, 75, 110),
				DropDownStyle = ComboBoxStyle.DropDownList,
				Location = new Point(listPanel.Width - 205, 40),
				Size = new Size(180, 30)
			};
			sortBox.Items.Add("По названию");
			sortBox.SelectedIndex = 0;
			listPanel.Controls.Add(sortBox);
		}

		private Button CreatePurpleButton(string text, int x, int y, int width, int height)
		{
			Button button = new Button
			{
				Text = text,
				Font = new Font("Segoe UI", 10, FontStyle.Bold),
				ForeColor = Color.White,
				BackColor = purple,
				FlatStyle = FlatStyle.Flat,
				Size = new Size(width, height),
				Location = new Point(x, y)
			};

			button.FlatAppearance.BorderSize = 0;
			SetRoundedRegion(button, 10);
			return button;
		}

		private Label CreateSmallTitle(string text, int x, int y)
		{
			return new Label
			{
				Text = text,
				Font = new Font("Segoe UI", 9, FontStyle.Bold),
				ForeColor = Color.FromArgb(70, 75, 110),
				AutoSize = true,
				Location = new Point(x, y)
			};
		}

		private Label CreateSmallValue(string text, int x, int y)
		{
			return new Label
			{
				Text = text,
				Font = new Font("Segoe UI", 10, FontStyle.Bold),
				ForeColor = Color.FromArgb(50, 55, 95),
				AutoSize = true,
				Location = new Point(x, y)
			};
		}



		private void ShowReportsPage()
		{
			currentSection = "Отчёты";
			HideHomeControls();
			CreateEmptyCurrentPage();
			currentPage.AutoScroll = true;

			int margin = 45;
			int gap = 22;
			int width = GetContentWidth(margin);
			currentPage.Controls.Add(CreatePageTitle("Отчёты за сегодня", margin, 35));
			currentPage.Controls.Add(CreateSubtitle("Статистика только за сегодняшний день: " + DateTime.Now.ToString("dd.MM.yyyy"), margin, 88));

			Button printButton = CreatePurpleButton("Печать отчёта", margin + width - 180, 50, 180, 42);
			printButton.BackColor = Color.FromArgb(86, 42, 245);
			printButton.Click += (s, e) => PrintReport();
			currentPage.Controls.Add(printButton);

			Button exportButton = CreatePurpleButton("Экспорт в Excel", margin + width - 375, 50, 180, 42);
			exportButton.BackColor = Color.FromArgb(86, 42, 245);
			exportButton.Click += (s, e) => ExportReportToExcel();
			currentPage.Controls.Add(exportButton);

			int cardTop = 140;
			int cardWidth = (width - gap * 2) / 3;
			int cardHeight = 125;
			string bonusCount = "0";
			string salesCount = "0";
			string salesSum = "0 ₽";
			try
			{
				bonusCount = GetScalarInt("SELECT ISNULL(SUM(Amount),0) FROM BonusOperations WHERE CAST(OperationDate AS date)=CAST(GETDATE() AS date)").ToString();
				salesCount = GetScalarInt("SELECT COUNT(*) FROM Sales WHERE CAST(SaleDate AS date)=CAST(GETDATE() AS date)").ToString();
				salesSum = FormatMoney(GetScalarDecimal("SELECT ISNULL(SUM(Amount),0) FROM Sales WHERE CAST(SaleDate AS date)=CAST(GETDATE() AS date)"));
			}
			catch { }

			currentPage.Controls.Add(CreateTopCard("Бонусов сегодня", bonusCount, "Только за сегодня", margin, cardTop, cardWidth, cardHeight, purple));
			currentPage.Controls.Add(CreateTopCard("Продаж сегодня", salesCount, "Только за сегодня", margin + cardWidth + gap, cardTop, cardWidth, cardHeight, blue));
			currentPage.Controls.Add(CreateTopCard("Сумма сегодня", salesSum, "Только за сегодня", margin + (cardWidth + gap) * 2, cardTop, cardWidth, cardHeight, green));

			Panel clientsPanel = CreateWhitePanel(margin, cardTop + cardHeight + 25, width, 300);
			clientsPanel.AutoScroll = true;
			currentPage.Controls.Add(clientsPanel);
			clientsPanel.Controls.Add(new Label { Text = "Клиенты с остатками абонементов", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(25, 22) });
			DataGridView clientsGrid = CreateReportGrid(25, 65, clientsPanel.Width - 50, clientsPanel.Height - 95);
			clientsGrid.Columns.Add("Client", "Клиент / логин");
			clientsGrid.Columns.Add("Phone", "Телефон");
			clientsGrid.Columns.Add("Money", "Деньги");
			clientsGrid.Columns.Add("Bonus", "Бонусы");
			clientsGrid.Columns.Add("Time", "Остаток времени");
			try
			{
				DataTable clients = ExecuteDataTable(@"SELECT Login, Phone, BalanceMoney, BonusBalance, RemainingSeconds FROM Clients WHERE ISNULL(IsDeleted,0)=0 ORDER BY Login");
				foreach (DataRow row in clients.Rows)
					clientsGrid.Rows.Add(row["Login"].ToString(), row["Phone"].ToString(), FormatMoney(Convert.ToDecimal(row["BalanceMoney"])), row["BonusBalance"].ToString(), FormatSeconds(Convert.ToInt32(row["RemainingSeconds"])));
			}
			catch { }
			clientsPanel.Controls.Add(clientsGrid);

			Panel bonusPanel = CreateWhitePanel(margin, clientsPanel.Bottom + 25, width, 360);
			bonusPanel.AutoScroll = true;
			currentPage.Controls.Add(bonusPanel);
			bonusPanel.Controls.Add(new Label { Text = "Бонусные операции за сегодня", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(25, 22) });
			DataGridView bonusesGrid = CreateReportGrid(25, 65, bonusPanel.Width - 50, bonusPanel.Height - 95);
			bonusesGrid.Columns.Add("Client", "Клиент / логин");
			bonusesGrid.Columns.Add("Reason", "Операция");
			bonusesGrid.Columns.Add("Amount", "Бонусы");
			bonusesGrid.Columns.Add("Total", "Всего бонусов");
			try
			{
				DataTable bonuses = ExecuteDataTable(@"SELECT c.Login, b.Reason, b.Amount, c.BonusBalance FROM BonusOperations b INNER JOIN Clients c ON c.Id=b.ClientId WHERE CAST(b.OperationDate AS date)=CAST(GETDATE() AS date) ORDER BY b.OperationDate DESC");
				foreach (DataRow row in bonuses.Rows)
					bonusesGrid.Rows.Add(row["Login"].ToString(), row["Reason"].ToString(), row["Amount"].ToString(), row["BonusBalance"].ToString());
			}
			catch { }
			bonusPanel.Controls.Add(bonusesGrid);

			Panel salesPanel = CreateWhitePanel(margin, bonusPanel.Bottom + 25, width, 380);
			salesPanel.AutoScroll = true;
			currentPage.Controls.Add(salesPanel);
			salesPanel.Controls.Add(new Label { Text = "Продажи и пополнения за сегодня", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = textColor, AutoSize = true, Location = new Point(25, 22) });
			DataGridView salesGrid = CreateReportGrid(25, 65, salesPanel.Width - 50, salesPanel.Height - 95);
			salesGrid.Columns.Add("Client", "Клиент / логин");
			salesGrid.Columns.Add("Service", "Тариф / Абонемент");
			salesGrid.Columns.Add("Payment", "Тип оплаты");
			salesGrid.Columns.Add("Minutes", "Время");
			salesGrid.Columns.Add("Sum", "Сумма");
			try
			{
				DataTable sales = ExecuteDataTable(@"SELECT c.Login, ISNULL(t.Name, sub.Name) AS ServiceName, ISNULL(s.PaymentType,'') AS PaymentType, ISNULL(s.MinutesAdded,0) AS MinutesAdded, s.Amount FROM Sales s INNER JOIN Clients c ON c.Id=s.ClientId LEFT JOIN Tariffs t ON t.Id=s.TariffId LEFT JOIN Subscriptions sub ON sub.Id=s.SubscriptionId WHERE CAST(s.SaleDate AS date)=CAST(GETDATE() AS date) ORDER BY s.SaleDate DESC");
				foreach (DataRow row in sales.Rows)
					salesGrid.Rows.Add(row["Login"].ToString(), row["ServiceName"].ToString(), row["PaymentType"].ToString(), row["MinutesAdded"].ToString() + " мин.", FormatMoney(Convert.ToDecimal(row["Amount"])));
			}
			catch { }
			salesPanel.Controls.Add(salesGrid);

			currentPage.AutoScrollMinSize = new Size(width + margin * 2, salesPanel.Bottom + 90);
		}

		private DataGridView CreateReportGrid(int x, int y, int width, int height)
		{
			DataGridView grid = new DataGridView
			{
				Location = new Point(x, y),
				Size = new Size(width, height),
				BackgroundColor = Color.White,
				BorderStyle = BorderStyle.None,
				AllowUserToAddRows = false,
				AllowUserToDeleteRows = false,
				ReadOnly = true,
				RowHeadersVisible = false,
				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
				SelectionMode = DataGridViewSelectionMode.FullRowSelect,
				GridColor = Color.FromArgb(230, 230, 240),
				Font = new Font("Segoe UI", 9),
				ColumnHeadersHeight = 35,
				RowTemplate = { Height = 32 },
				ScrollBars = ScrollBars.Both
			};

			grid.EnableHeadersVisualStyles = false;
			grid.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
			grid.ColumnHeadersDefaultCellStyle.ForeColor = textColor;
			grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
			grid.DefaultCellStyle.SelectionBackColor = lightPurple;
			grid.DefaultCellStyle.SelectionForeColor = textColor;

			return grid;
		}


		private void ExportReportToExcel()
		{
			using (SaveFileDialog dialog = new SaveFileDialog())
			{
				dialog.Title = "Экспорт отчёта за сегодня в Excel";
				dialog.Filter = "Excel CSV файл (*.csv)|*.csv";
				dialog.FileName = "Отчёт_KiberPride_" + DateTime.Now.ToString("dd_MM_yyyy") + ".csv";
				if (dialog.ShowDialog(this) != DialogResult.OK)
					return;

				List<string> lines = new List<string>();
				lines.Add("Отчёт KiberPride за сегодня");
				lines.Add("Дата экспорта;" + DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
				lines.Add("");
				lines.Add("Клиенты с остатками");
				lines.Add("Клиент / логин;Телефон;Деньги;Бонусы;Остаток времени");
				try
				{
					DataTable clients = ExecuteDataTable(@"SELECT Login, Phone, BalanceMoney, BonusBalance, RemainingSeconds FROM Clients WHERE ISNULL(IsDeleted,0)=0 ORDER BY Login");
					foreach (DataRow row in clients.Rows)
						lines.Add(string.Format("{0};{1};{2};{3};{4}", row["Login"], row["Phone"], row["BalanceMoney"], row["BonusBalance"], FormatSeconds(Convert.ToInt32(row["RemainingSeconds"]))));
				}
				catch { }

				lines.Add("");
				lines.Add("Бонусные операции за сегодня");
				lines.Add("Клиент / логин;Операция;Бонусы;Всего бонусов");
				try
				{
					DataTable bonuses = ExecuteDataTable(@"SELECT c.Login, b.Reason, b.Amount, c.BonusBalance FROM BonusOperations b INNER JOIN Clients c ON c.Id=b.ClientId WHERE CAST(b.OperationDate AS date)=CAST(GETDATE() AS date) ORDER BY b.OperationDate DESC");
					foreach (DataRow row in bonuses.Rows)
						lines.Add(string.Format("{0};{1};{2};{3}", row["Login"], row["Reason"], row["Amount"], row["BonusBalance"]));
				}
				catch { }

				lines.Add("");
				lines.Add("Продажи и пополнения за сегодня");
				lines.Add("Клиент / логин;Тариф / Абонемент;Тип оплаты;Время;Сумма");
				try
				{
					DataTable sales = ExecuteDataTable(@"SELECT c.Login, ISNULL(t.Name, sub.Name) AS ServiceName, ISNULL(s.PaymentType,'') AS PaymentType, ISNULL(s.MinutesAdded,0) AS MinutesAdded, s.Amount FROM Sales s INNER JOIN Clients c ON c.Id=s.ClientId LEFT JOIN Tariffs t ON t.Id=s.TariffId LEFT JOIN Subscriptions sub ON sub.Id=s.SubscriptionId WHERE CAST(s.SaleDate AS date)=CAST(GETDATE() AS date) ORDER BY s.SaleDate DESC");
					foreach (DataRow row in sales.Rows)
						lines.Add(string.Format("{0};{1};{2};{3} мин.;{4}", row["Login"], row["ServiceName"], row["PaymentType"], row["MinutesAdded"], row["Amount"]));
				}
				catch { }

				System.IO.File.WriteAllLines(dialog.FileName, lines, System.Text.Encoding.UTF8);
				MessageBox.Show("Отчёт за сегодня экспортирован. Файл CSV открывается в Excel.", "Экспорт в Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		private void PrintReport()
		{
			PrintDocument document = new PrintDocument();
			document.DocumentName = "Отчёт KiberPride";

			document.PrintPage += (s, e) =>
			{
				Font titleFont = new Font("Segoe UI", 20, FontStyle.Bold);
				Font headerFont = new Font("Segoe UI", 13, FontStyle.Bold);
				Font textFont = new Font("Segoe UI", 10);

				int x = 60;
				int y = 60;
				e.Graphics.DrawString("Отчёт KiberPride", titleFont, Brushes.Black, x, y);
				y += 45;
				e.Graphics.DrawString($"Дата печати: {DateTime.Now:dd.MM.yyyy HH:mm}", textFont, Brushes.Black, x, y);
				y += 40;

				try
				{
					int bonusTotal = GetScalarInt("SELECT ISNULL(SUM(Amount),0) FROM BonusOperations");
					int visitsCount = GetScalarInt("SELECT COUNT(*) FROM Visits");
					decimal salesTotal = GetScalarDecimal("SELECT ISNULL(SUM(Amount),0) FROM Sales");
					e.Graphics.DrawString("Итоги", headerFont, Brushes.Black, x, y);
					y += 28;
					e.Graphics.DrawString("Начислено бонусов: " + bonusTotal, textFont, Brushes.Black, x, y); y += 24;
					e.Graphics.DrawString("Посещений: " + visitsCount, textFont, Brushes.Black, x, y); y += 24;
					e.Graphics.DrawString("Сумма продаж: " + FormatMoney(salesTotal), textFont, Brushes.Black, x, y); y += 40;
				}
				catch { }

				e.Graphics.DrawString("Подробные данные смотрите в разделе Отчёты или экспортируйте в Excel.", textFont, Brushes.Black, x, y);

				titleFont.Dispose();
				headerFont.Dispose();
				textFont.Dispose();
			};

			using (PrintPreviewDialog preview = new PrintPreviewDialog())
			{
				preview.Document = document;
				preview.Width = 1000;
				preview.Height = 700;
				preview.ShowDialog(this);
			}
		}

		private void ShowSimplePage(string titleText, string subtitleText)
		{
			currentSection = titleText;
			HideHomeControls();
			CreateEmptyCurrentPage();

			int margin = 45;
			int width = GetContentWidth(margin);

			currentPage.Controls.Add(CreatePageTitle(titleText, margin, 35));
			currentPage.Controls.Add(CreateSubtitle(subtitleText, margin, 88));

			Panel card = CreateWhitePanel(margin, 150, width, 200);

			card.Controls.Add(new Label
			{
				Text = titleText,
				Font = new Font("Segoe UI", 18, FontStyle.Bold),
				ForeColor = textColor,
				AutoSize = true,
				Location = new Point(30, 30)
			});

			card.Controls.Add(new Label
			{
				Text = "Раздел готов к дальнейшему наполнению.",
				Font = new Font("Segoe UI", 11),
				ForeColor = Color.FromArgb(60, 65, 95),
				AutoSize = true,
				Location = new Point(30, 80)
			});

			currentPage.Controls.Add(card);
		}

		private Label CreatePageTitle(string text, int x, int y)
		{
			return new Label
			{
				Text = text,
				Font = new Font("Segoe UI", 28, FontStyle.Bold),
				ForeColor = textColor,
				AutoSize = true,
				BackColor = Color.Transparent,
				Location = new Point(x, y)
			};
		}

		private Label CreateSubtitle(string text, int x, int y)
		{
			return new Label
			{
				Text = text,
				Font = new Font("Segoe UI", 11),
				ForeColor = Color.FromArgb(70, 75, 110),
				AutoSize = true,
				BackColor = Color.Transparent,
				Location = new Point(x, y)
			};
		}

		private Panel CreateWhitePanel(int x, int y, int width, int height)
		{
			Panel panel = new Panel
			{
				BackColor = Color.White,
				Location = new Point(x, y),
				Size = new Size(width, height)
			};

			SetRoundedRegion(panel, 18);
			return panel;
		}

		private int GetContentWidth(int margin)
		{
			return Math.Max(1000, panelContent.ClientSize.Width - margin * 2);
		}

		private void HideHomeControls()
		{
			foreach (Control control in homeControls)
				control.Visible = false;

			foreach (Control control in panelContent.Controls)
			{
				if (control.Name == "homeCardClientsSession" ||
					control.Name == "homeCardMonthIncome" ||
					control.Name == "homeCardMonthVisits" ||
					control.Name == "homeClientsListPanel" ||
					control.Name == "btnTopUpBalance" ||
					control.Name == "btnViewClients" ||
					control.Name == "btnTopUpBonuses")
				{
					control.Visible = false;
				}
			}
		}

		private void CreateEmptyCurrentPage()
		{
			if (currentPage != null)
			{
				panelContent.Controls.Remove(currentPage);
				currentPage.Dispose();
			}

			panelContent.AutoScroll = false;
			panelContent.AutoScrollMinSize = Size.Empty;

			currentPage = new Panel
			{
				Location = new Point(0, 0),
				Size = panelContent.ClientSize,
				BackColor = Color.Transparent
			};

			panelContent.Controls.Add(currentPage);
			currentPage.BringToFront();
		}

		private void btnHome_Click(object sender, EventArgs e)
		{
			ShowHomePage();
			SetActiveButton(btnHome);
		}

		private void btnSubscriptions_Click(object sender, EventArgs e)
		{
			ShowSubscriptionsPage();
			SetActiveButton(btnSubscriptions);
		}

		private void btnVisits_Click(object sender, EventArgs e)
		{
			if (!IsSeniorAdmin()) { ShowAccessDenied(); return; }
			ShowVisitsPage();
			SetActiveButton(btnVisits);
		}

		private void btnTariffs_Click(object sender, EventArgs e)
		{
			if (!IsSeniorAdmin()) { ShowAccessDenied(); return; }
			ShowTariffsPage();
			SetActiveButton(btnTariffs);
		}

		private void btnReports_Click(object sender, EventArgs e)
		{
			ShowReportsPage();
			SetActiveButton(btnReports);
		}

		private void btnExit_Click(object sender, EventArgs e)
		{
			activeSessionsTimer.Stop();

			if (useExternalAuthorization)
			{
				Close();
				return;
			}

			currentUserLogin = "";
			currentUserRole = "";

			if (ShowAuthorizationWindow())
			{
				ApplyRoleAccess();
				SetupActiveSessionsTimer();
				ShowHomePage();
				SetActiveButton(btnHome);
			}
			else
			{
				Close();
			}
		}

		private void btnExit_Click_1(object sender, EventArgs e)
		{
			btnExit_Click(sender, e);
		}

		private void pictureBox1_Click(object sender, EventArgs e)
		{
			btnTariffs_Click(sender, e);
		}

		private void pictureBox3_Click(object sender, EventArgs e)
		{
			btnTariffs_Click(sender, e);
		}

		private void label3_Click(object sender, EventArgs e)
		{
			btnVisits_Click(sender, e);
		}

		private void label4_Click(object sender, EventArgs e)
		{
			btnTariffs_Click(sender, e);
		}

		private void SetActiveButton(Button activeButton)
		{
			Button[] buttons =
			{
				btnHome,
				btnSubscriptions,
				btnVisits,
				btnTariffs,
				btnReports
			};

			foreach (Button button in buttons)
			{
				button.BackColor = Color.White;
				button.ForeColor = textColor;
				button.FlatStyle = FlatStyle.Flat;
				button.FlatAppearance.BorderSize = 0;
			}

			activeButton.BackColor = lightPurple;
			activeButton.ForeColor = purple;

			if (!IsSeniorAdmin())
			{
				btnVisits.Enabled = false;
				btnTariffs.Enabled = false;
				btnVisits.ForeColor = Color.Gray;
				btnTariffs.ForeColor = Color.Gray;
			}
		}

		private void StyleControls(Control parent)
		{
			foreach (Control control in parent.Controls)
			{
				if (control is Button button)
				{
					button.FlatStyle = FlatStyle.Flat;
					button.FlatAppearance.BorderSize = 0;
					button.Font = new Font("Segoe UI", 10, FontStyle.Regular);
					button.Cursor = Cursors.Hand;
				}

				if (control is Label label)
					label.BackColor = Color.Transparent;

				StyleControls(control);
			}
		}

		private void EnableDoubleBuffer(Control control)
		{
			typeof(Control).GetProperty(
				"DoubleBuffered",
				BindingFlags.NonPublic | BindingFlags.Instance
			)?.SetValue(control, true, null);
		}

		private void SetRoundedRegion(Control control, int radius)
		{
			if (control.Width <= 0 || control.Height <= 0)
				return;

			GraphicsPath path = new GraphicsPath();
			int diameter = radius * 2;

			path.AddArc(0, 0, diameter, diameter, 180, 90);
			path.AddArc(control.Width - diameter, 0, diameter, diameter, 270, 90);
			path.AddArc(control.Width - diameter, control.Height - diameter, diameter, diameter, 0, 90);
			path.AddArc(0, control.Height - diameter, diameter, diameter, 90, 90);
			path.CloseFigure();

			control.Region = new Region(path);
		}

		private void panelContent_Paint(object sender, PaintEventArgs e)
		{
			if (panelContent.ClientRectangle.Width <= 0 || panelContent.ClientRectangle.Height <= 0)
				return;

			using LinearGradientBrush brush = new LinearGradientBrush(
				panelContent.ClientRectangle,
				Color.FromArgb(140, 245, 230),
				purple,
				LinearGradientMode.Horizontal);

			e.Graphics.FillRectangle(brush, panelContent.ClientRectangle);
		}

		private void btnSubscriptions_Click_1(object sender, EventArgs e)
		{

		}

		private void btnExit_Click_2(object sender, EventArgs e)
		{

		}

		private void label1_Click(object sender, EventArgs e)
		{

		}

		private void picSubscriptions_Click(object sender, EventArgs e)
		{

			btnSubscriptions_Click(sender, e);
		}

		private void picVisits_Click(object sender, EventArgs e)
		{
			btnVisits_Click(sender, e);
		}

		private void picTariffs_Click(object sender, EventArgs e)
		{
			btnTariffs_Click(sender, e);
		}

		private void picReports_Click(object sender, EventArgs e)
		{
			btnReports_Click(sender, e);
		}
	}
}