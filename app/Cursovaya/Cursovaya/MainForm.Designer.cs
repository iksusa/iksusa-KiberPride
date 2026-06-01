using System;
using System.Drawing;
using System.Windows.Forms;

namespace Cursovaya
{
	partial class MainForm
	{
		private System.ComponentModel.IContainer components = null;

		private Panel panelMenu;
		private Panel panelContent;

		private Button btnHome;
		private Button btnSubscriptions;
		private Button btnVisits;
		private Button btnTariffs;
		private Button btnReports;
		private Button btnExit;

		private Label lblLogo;
		private Label lblTitle;

		private Panel cardSubscriptions;
		private Label lblSubText;
		private Label lblSubValue;

		private Panel cardVisits;
		private Label lblVisitsText;
		private Label lblVisitsValue;

		private Panel panelQuickAccess;

		private Panel quickSubscriptions;
		private Panel quickTariffs;
		private Panel quickReports;

		private Label lblQuickTitle;

		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}

			base.Dispose(disposing);
		}

        private void InitializeComponent()
        {
            panelMenu = new Panel();
            lblLogo = new Label();
            btnHome = new Button();
            btnSubscriptions = new Button();
            btnVisits = new Button();
            btnTariffs = new Button();
            btnReports = new Button();
            btnExit = new Button();
            panelContent = new Panel();
            lblTitle = new Label();
            cardSubscriptions = new Panel();
            lblSubText = new Label();
            lblSubValue = new Label();
            cardVisits = new Panel();
            lblVisitsText = new Label();
            lblVisitsValue = new Label();
            panelQuickAccess = new Panel();
            lblQuickTitle = new Label();
            quickSubscriptions = new Panel();
            quickVisits = new Panel();
            quickTariffs = new Panel();
            quickReports = new Panel();
            panelMenu.SuspendLayout();
            panelContent.SuspendLayout();
            cardSubscriptions.SuspendLayout();
            cardVisits.SuspendLayout();
            panelQuickAccess.SuspendLayout();
            SuspendLayout();
            // 
            // panelMenu
            // 
            panelMenu.BackColor = Color.WhiteSmoke;
            panelMenu.Controls.Add(lblLogo);
            panelMenu.Controls.Add(btnHome);
            panelMenu.Controls.Add(btnSubscriptions);
            panelMenu.Controls.Add(btnVisits);
            panelMenu.Controls.Add(btnTariffs);
            panelMenu.Controls.Add(btnReports);
            panelMenu.Controls.Add(btnExit);
            panelMenu.Dock = DockStyle.Left;
            panelMenu.Location = new Point(0, 0);
            panelMenu.Name = "panelMenu";
            panelMenu.Size = new Size(220, 900);
            panelMenu.TabIndex = 0;
            // 
            // lblLogo
            // 
            lblLogo.AutoSize = true;
            lblLogo.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            lblLogo.ForeColor = Color.MediumSlateBlue;
            lblLogo.Location = new Point(30, 30);
            lblLogo.Name = "lblLogo";
            lblLogo.Size = new Size(154, 37);
            lblLogo.TabIndex = 0;
            lblLogo.Text = "KiberPride";
            // 
            // btnHome
            // 
            btnHome.BackColor = Color.WhiteSmoke;
            btnHome.FlatAppearance.BorderSize = 0;
            btnHome.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 230, 255);
            btnHome.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 242, 255);
            btnHome.FlatStyle = FlatStyle.Flat;
            btnHome.Font = new Font("Segoe UI", 10F);
            btnHome.Location = new Point(20, 100);
            btnHome.Name = "btnHome";
            btnHome.Padding = new Padding(8, 0, 0, 0);
            btnHome.Size = new Size(190, 38);
            btnHome.TabIndex = 1;
            btnHome.Text = "Главная";
            btnHome.TextAlign = ContentAlignment.MiddleLeft;
            btnHome.UseVisualStyleBackColor = false;
            // 
            // btnSubscriptions
            // 
            btnSubscriptions.BackColor = Color.WhiteSmoke;
            btnSubscriptions.FlatAppearance.BorderSize = 0;
            btnSubscriptions.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 230, 255);
            btnSubscriptions.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 242, 255);
            btnSubscriptions.FlatStyle = FlatStyle.Flat;
            btnSubscriptions.Font = new Font("Segoe UI", 10F);
            btnSubscriptions.Location = new Point(20, 150);
            btnSubscriptions.Name = "btnSubscriptions";
            btnSubscriptions.Padding = new Padding(8, 0, 0, 0);
            btnSubscriptions.Size = new Size(190, 38);
            btnSubscriptions.TabIndex = 2;
            btnSubscriptions.Text = "Абонементы";
            btnSubscriptions.TextAlign = ContentAlignment.MiddleLeft;
            btnSubscriptions.UseVisualStyleBackColor = false;
            btnSubscriptions.Click += btnSubscriptions_Click_1;
            // 
            // btnVisits
            // 
            btnVisits.BackColor = Color.WhiteSmoke;
            btnVisits.FlatAppearance.BorderSize = 0;
            btnVisits.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 230, 255);
            btnVisits.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 242, 255);
            btnVisits.FlatStyle = FlatStyle.Flat;
            btnVisits.Font = new Font("Segoe UI", 10F);
            btnVisits.Location = new Point(20, 200);
            btnVisits.Name = "btnVisits";
            btnVisits.Padding = new Padding(8, 0, 0, 0);
            btnVisits.Size = new Size(190, 38);
            btnVisits.TabIndex = 3;
            btnVisits.Text = "Посещения";
            btnVisits.TextAlign = ContentAlignment.MiddleLeft;
            btnVisits.UseVisualStyleBackColor = false;
            // 
            // btnTariffs
            // 
            btnTariffs.BackColor = Color.WhiteSmoke;
            btnTariffs.FlatAppearance.BorderSize = 0;
            btnTariffs.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 230, 255);
            btnTariffs.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 242, 255);
            btnTariffs.FlatStyle = FlatStyle.Flat;
            btnTariffs.Font = new Font("Segoe UI", 10F);
            btnTariffs.Location = new Point(20, 250);
            btnTariffs.Name = "btnTariffs";
            btnTariffs.Padding = new Padding(8, 0, 0, 0);
            btnTariffs.Size = new Size(190, 38);
            btnTariffs.TabIndex = 4;
            btnTariffs.Text = "Тарифы";
            btnTariffs.TextAlign = ContentAlignment.MiddleLeft;
            btnTariffs.UseVisualStyleBackColor = false;
            // 
            // btnReports
            // 
            btnReports.BackColor = Color.WhiteSmoke;
            btnReports.FlatAppearance.BorderSize = 0;
            btnReports.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 230, 255);
            btnReports.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 242, 255);
            btnReports.FlatStyle = FlatStyle.Flat;
            btnReports.Font = new Font("Segoe UI", 10F);
            btnReports.Location = new Point(20, 300);
            btnReports.Name = "btnReports";
            btnReports.Padding = new Padding(8, 0, 0, 0);
            btnReports.Size = new Size(190, 38);
            btnReports.TabIndex = 5;
            btnReports.Text = "Отчёты";
            btnReports.TextAlign = ContentAlignment.MiddleLeft;
            btnReports.UseVisualStyleBackColor = false;
            // 
            // btnExit
            // 
            btnExit.BackColor = Color.WhiteSmoke;
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 230, 255);
            btnExit.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 242, 255);
            btnExit.FlatStyle = FlatStyle.Flat;
            btnExit.Font = new Font("Segoe UI", 10F);
            btnExit.Location = new Point(12, 850);
            btnExit.Name = "btnExit";
            btnExit.Padding = new Padding(8, 0, 0, 0);
            btnExit.Size = new Size(190, 38);
            btnExit.TabIndex = 6;
            btnExit.Text = "Выход";
            btnExit.TextAlign = ContentAlignment.MiddleLeft;
            btnExit.UseVisualStyleBackColor = false;
            btnExit.Click += btnExit_Click_2;
            // 
            // panelContent
            // 
            panelContent.BackColor = Color.White;
            panelContent.Controls.Add(lblTitle);
            panelContent.Controls.Add(cardSubscriptions);
            panelContent.Controls.Add(cardVisits);
            panelContent.Controls.Add(panelQuickAccess);
            panelContent.Dock = DockStyle.Fill;
            panelContent.Location = new Point(220, 0);
            panelContent.Name = "panelContent";
            panelContent.Size = new Size(1180, 900);
            panelContent.TabIndex = 1;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 28F, FontStyle.Bold);
            lblTitle.Location = new Point(30, 30);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(170, 51);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Главная";
            // 
            // cardSubscriptions
            // 
            cardSubscriptions.BackColor = Color.White;
            cardSubscriptions.Controls.Add(lblSubText);
            cardSubscriptions.Controls.Add(lblSubValue);
            cardSubscriptions.Location = new Point(30, 100);
            cardSubscriptions.Name = "cardSubscriptions";
            cardSubscriptions.Size = new Size(300, 120);
            cardSubscriptions.TabIndex = 1;
            // 
            // lblSubText
            // 
            lblSubText.AutoSize = true;
            lblSubText.Font = new Font("Segoe UI", 11F);
            lblSubText.Location = new Point(20, 25);
            lblSubText.Name = "lblSubText";
            lblSubText.Size = new Size(105, 20);
            lblSubText.TabIndex = 0;
            lblSubText.Text = "Абонементов";
            // 
            // lblSubValue
            // 
            lblSubValue.AutoSize = true;
            lblSubValue.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
            lblSubValue.Location = new Point(20, 55);
            lblSubValue.Name = "lblSubValue";
            lblSubValue.Size = new Size(38, 45);
            lblSubValue.TabIndex = 1;
            lblSubValue.Text = "0";
            // 
            // cardVisits
            // 
            cardVisits.BackColor = Color.White;
            cardVisits.Controls.Add(lblVisitsText);
            cardVisits.Controls.Add(lblVisitsValue);
            cardVisits.Location = new Point(350, 100);
            cardVisits.Name = "cardVisits";
            cardVisits.Size = new Size(300, 120);
            cardVisits.TabIndex = 2;
            // 
            // lblVisitsText
            // 
            lblVisitsText.AutoSize = true;
            lblVisitsText.Font = new Font("Segoe UI", 11F);
            lblVisitsText.Location = new Point(20, 25);
            lblVisitsText.Name = "lblVisitsText";
            lblVisitsText.Size = new Size(66, 20);
            lblVisitsText.TabIndex = 0;
            lblVisitsText.Text = "Сегодня";
            // 
            // lblVisitsValue
            // 
            lblVisitsValue.AutoSize = true;
            lblVisitsValue.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
            lblVisitsValue.Location = new Point(20, 55);
            lblVisitsValue.Name = "lblVisitsValue";
            lblVisitsValue.Size = new Size(38, 45);
            lblVisitsValue.TabIndex = 1;
            lblVisitsValue.Text = "0";
            // 
            // panelQuickAccess
            // 
            panelQuickAccess.BackColor = Color.White;
            panelQuickAccess.Controls.Add(lblQuickTitle);
            panelQuickAccess.Controls.Add(quickSubscriptions);
            panelQuickAccess.Controls.Add(quickVisits);
            panelQuickAccess.Controls.Add(quickTariffs);
            panelQuickAccess.Controls.Add(quickReports);
            panelQuickAccess.Location = new Point(30, 250);
            panelQuickAccess.Name = "panelQuickAccess";
            panelQuickAccess.Size = new Size(1000, 220);
            panelQuickAccess.TabIndex = 3;
            // 
            // lblQuickTitle
            // 
            lblQuickTitle.AutoSize = true;
            lblQuickTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblQuickTitle.Location = new Point(20, 20);
            lblQuickTitle.Name = "lblQuickTitle";
            lblQuickTitle.Size = new Size(165, 25);
            lblQuickTitle.TabIndex = 0;
            lblQuickTitle.Text = "Быстрый доступ";
            // 
            // quickSubscriptions
            // 
            quickSubscriptions.BackColor = Color.GhostWhite;
            quickSubscriptions.Location = new Point(20, 60);
            quickSubscriptions.Name = "quickSubscriptions";
            quickSubscriptions.Size = new Size(160, 105);
            quickSubscriptions.TabIndex = 1;
            // 
            // quickVisits
            // 
            quickVisits.BackColor = Color.GhostWhite;
            quickVisits.Location = new Point(200, 60);
            quickVisits.Name = "quickVisits";
            quickVisits.Size = new Size(160, 105);
            quickVisits.TabIndex = 2;
            // 
            // quickTariffs
            // 
            quickTariffs.BackColor = Color.GhostWhite;
            quickTariffs.Location = new Point(380, 60);
            quickTariffs.Name = "quickTariffs";
            quickTariffs.Size = new Size(160, 105);
            quickTariffs.TabIndex = 3;
            // 
            // quickReports
            // 
            quickReports.BackColor = Color.GhostWhite;
            quickReports.Location = new Point(560, 60);
            quickReports.Name = "quickReports";
            quickReports.Size = new Size(160, 105);
            quickReports.TabIndex = 4;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1400, 900);
            Controls.Add(panelContent);
            Controls.Add(panelMenu);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "KiberPride - Система управления";
            panelMenu.ResumeLayout(false);
            panelMenu.PerformLayout();
            panelContent.ResumeLayout(false);
            panelContent.PerformLayout();
            cardSubscriptions.ResumeLayout(false);
            cardSubscriptions.PerformLayout();
            cardVisits.ResumeLayout(false);
            cardVisits.PerformLayout();
            panelQuickAccess.ResumeLayout(false);
            panelQuickAccess.PerformLayout();
            ResumeLayout(false);
        }
        private Panel quickVisits;
    }
}
