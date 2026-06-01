namespace Cursovaya
{
	partial class Form1
	{
		private System.ComponentModel.IContainer components = null;

		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            panelMain = new Panel();
            panelIndicator = new Panel();
            panelTabs = new Panel();
            btnRegister = new Button();
            btnLogin = new Button();
            panelLogin = new Panel();
            txtLogin = new TextBox();
            panel1 = new Panel();
            txtConfirmPassword = new TextBox();
            panelPassword = new Panel();
            txtPassword = new TextBox();
            btnMain = new Button();
            label1 = new Label();
            timer1 = new System.Windows.Forms.Timer(components);
            panelMain.SuspendLayout();
            panelTabs.SuspendLayout();
            panelLogin.SuspendLayout();
            panel1.SuspendLayout();
            panelPassword.SuspendLayout();
            SuspendLayout();
            // 
            // panelMain
            // 
            panelMain.BackColor = Color.White;
            panelMain.Controls.Add(panelIndicator);
            panelMain.Controls.Add(panelTabs);
            panelMain.Controls.Add(panelLogin);
            panelMain.Controls.Add(panel1);
            panelMain.Controls.Add(panelPassword);
            panelMain.Controls.Add(btnMain);
            panelMain.Controls.Add(label1);
            panelMain.Location = new Point(497, 21);
            panelMain.Name = "panelMain";
            panelMain.Size = new Size(556, 690);
            panelMain.TabIndex = 0;
            // 
            // panelIndicator
            // 
            panelIndicator.BackColor = Color.MediumSeaGreen;
            panelIndicator.BackgroundImage = (Image)resources.GetObject("panelIndicator.BackgroundImage");
            panelIndicator.Location = new Point(25, 143);
            panelIndicator.Name = "panelIndicator";
            panelIndicator.Size = new Size(250, 70);
            panelIndicator.TabIndex = 0;
            panelIndicator.Paint += panelIndicator_Paint;
            // 
            // panelTabs
            // 
            panelTabs.Controls.Add(btnRegister);
            panelTabs.Controls.Add(btnLogin);
            panelTabs.Location = new Point(25, 143);
            panelTabs.Name = "panelTabs";
            panelTabs.Size = new Size(500, 70);
            panelTabs.TabIndex = 1;
            // 
            // btnRegister
            // 
            btnRegister.FlatStyle = FlatStyle.Flat;
            btnRegister.Font = new Font("Evolventa", 24F);
            btnRegister.Location = new Point(248, 0);
            btnRegister.Name = "btnRegister";
            btnRegister.Size = new Size(251, 70);
            btnRegister.TabIndex = 1;
            btnRegister.Text = "Регистрация";
            btnRegister.UseVisualStyleBackColor = true;
            btnRegister.Click += btnRegister_Click_1;
            // 
            // btnLogin
            // 
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.Font = new Font("Evolventa", 24F);
            btnLogin.Location = new Point(0, 0);
            btnLogin.Name = "btnLogin";
            btnLogin.Size = new Size(251, 70);
            btnLogin.TabIndex = 0;
            btnLogin.Text = "Войти";
            btnLogin.UseVisualStyleBackColor = true;
            btnLogin.Click += btnLogin_Click_1;
            // 
            // panelLogin
            // 
            panelLogin.Controls.Add(txtLogin);
            panelLogin.Location = new Point(27, 269);
            panelLogin.Name = "panelLogin";
            panelLogin.Size = new Size(500, 50);
            panelLogin.TabIndex = 7;
            // 
            // txtLogin
            // 
            txtLogin.BorderStyle = BorderStyle.FixedSingle;
            txtLogin.Font = new Font("Evolventa", 24F);
            txtLogin.Location = new Point(0, 0);
            txtLogin.Name = "txtLogin";
            txtLogin.PlaceholderText = "  Логин";
            txtLogin.Size = new Size(500, 50);
            txtLogin.TabIndex = 3;
            txtLogin.TextChanged += txtLogin_TextChanged;
            // 
            // panel1
            // 
            panel1.Controls.Add(txtConfirmPassword);
            panel1.Location = new Point(27, 439);
            panel1.Name = "panel1";
            panel1.Size = new Size(500, 50);
            panel1.TabIndex = 9;
            // 
            // txtConfirmPassword
            // 
            txtConfirmPassword.BorderStyle = BorderStyle.FixedSingle;
            txtConfirmPassword.Font = new Font("Evolventa", 24F);
            txtConfirmPassword.Location = new Point(0, 0);
            txtConfirmPassword.Name = "txtConfirmPassword";
            txtConfirmPassword.PasswordChar = '●';
            txtConfirmPassword.PlaceholderText = "  Подтвердите пароль";
            txtConfirmPassword.Size = new Size(500, 50);
            txtConfirmPassword.TabIndex = 6;
            txtConfirmPassword.Visible = false;
            txtConfirmPassword.TextChanged += txtConfirmPassword_TextChanged;
            // 
            // panelPassword
            // 
            panelPassword.Controls.Add(txtPassword);
            panelPassword.Location = new Point(27, 361);
            panelPassword.Name = "panelPassword";
            panelPassword.Size = new Size(500, 50);
            panelPassword.TabIndex = 8;
            // 
            // txtPassword
            // 
            txtPassword.BorderStyle = BorderStyle.None;
            txtPassword.Font = new Font("Evolventa", 24F);
            txtPassword.Location = new Point(0, 0);
            txtPassword.Name = "txtPassword";
            txtPassword.PlaceholderText = "  Пароль";
            txtPassword.Size = new Size(500, 43);
            txtPassword.TabIndex = 4;
            // 
            // btnMain
            // 
            btnMain.FlatStyle = FlatStyle.Flat;
            btnMain.Font = new Font("Evolventa", 24F, FontStyle.Bold);
            btnMain.ForeColor = Color.GhostWhite;
            btnMain.Image = (Image)resources.GetObject("btnMain.Image");
            btnMain.Location = new Point(25, 537);
            btnMain.Name = "btnMain";
            btnMain.Size = new Size(498, 70);
            btnMain.TabIndex = 5;
            btnMain.Text = "Войти";
            btnMain.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Evolventa", 36F, FontStyle.Bold);
            label1.ForeColor = Color.BlueViolet;
            label1.Location = new Point(107, 32);
            label1.Name = "label1";
            label1.Size = new Size(343, 64);
            label1.TabIndex = 2;
            label1.Text = "Авторизация";
            // 
            // timer1
            // 
            timer1.Interval = 10;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackgroundImage = (Image)resources.GetObject("$this.BackgroundImage");
            ClientSize = new Size(1435, 735);
            Controls.Add(panelMain);
            FormBorderStyle = FormBorderStyle.None;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Авторизация";
            panelMain.ResumeLayout(false);
            panelMain.PerformLayout();
            panelTabs.ResumeLayout(false);
            panelLogin.ResumeLayout(false);
            panelLogin.PerformLayout();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            panelPassword.ResumeLayout(false);
            panelPassword.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel panelMain;
		private Panel panelTabs;
		private Panel panelIndicator;
		private Label label1;
		private Button btnLogin;
		private Button btnRegister;
		private TextBox txtLogin;
		private TextBox txtPassword;
		private Button btnMain;
		private System.Windows.Forms.Timer timer1;
        private TextBox txtConfirmPassword;
        private Panel panelLogin;
        private Panel panelPassword;
        private Panel panel1;
    }
}