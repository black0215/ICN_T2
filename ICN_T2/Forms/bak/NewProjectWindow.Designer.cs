using ReaLTaiizor.Controls;
using System.Drawing;
using System.Windows.Forms;

namespace ICN_T2.Forms
{
    partial class NewProjectWindow
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private System.Windows.Forms.Panel topPanel;
        private System.Windows.Forms.Label lblTitle;
        private NightControlBox controlBox;

        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.Label lblPath;
        private System.Windows.Forms.TextBox txtPath;
        private System.Windows.Forms.Button btnBrowse; // Changed to Standard Button
        private System.Windows.Forms.Label lblDesc;
        private System.Windows.Forms.TextBox txtDesc;
        private System.Windows.Forms.Button btnCreate; // Changed to Standard Button
        private System.Windows.Forms.Button btnCancel; // Changed to Standard Button
        private System.Windows.Forms.Label lblSaveInfo; // New Label

        private System.Windows.Forms.Label lblType;
        private System.Windows.Forms.RadioButton rbVanilla;
        private System.Windows.Forms.RadioButton rbModded;
        private System.Windows.Forms.Label lblGameVersion;
        private System.Windows.Forms.ComboBox cmbGameVersion;

        private void InitializeComponent()
        {
            topPanel = new System.Windows.Forms.Panel();
            controlBox = new NightControlBox();
            lblTitle = new Label();
            lblType = new Label();
            rbVanilla = new System.Windows.Forms.RadioButton();
            rbModded = new System.Windows.Forms.RadioButton();
            lblName = new Label();
            txtName = new TextBox();
            lblPath = new Label();
            txtPath = new TextBox();
            btnBrowse = new System.Windows.Forms.Button();
            lblDesc = new Label();
            txtDesc = new TextBox();
            lblGameVersion = new Label();
            cmbGameVersion = new System.Windows.Forms.ComboBox();
            btnCreate = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            lblSaveInfo = new Label();
            topPanel.SuspendLayout();
            SuspendLayout();
            // 
            // topPanel
            // 
            topPanel.BackColor = Color.FromArgb(45, 45, 48);
            topPanel.Controls.Add(controlBox);
            topPanel.Controls.Add(lblTitle);
            topPanel.Dock = DockStyle.Top;
            topPanel.Location = new Point(0, 0);
            topPanel.Name = "topPanel";
            topPanel.Size = new Size(400, 31);
            topPanel.TabIndex = 13;
            topPanel.MouseDown += TopPanel_MouseDown;
            // 
            // controlBox
            // 
            controlBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            controlBox.BackColor = Color.Transparent;
            controlBox.CloseHoverColor = Color.FromArgb(199, 80, 80);
            controlBox.CloseHoverForeColor = Color.White;
            controlBox.DefaultLocation = true;
            controlBox.DisableMaximizeColor = Color.FromArgb(105, 105, 105);
            controlBox.DisableMinimizeColor = Color.FromArgb(105, 105, 105);
            controlBox.EnableCloseColor = Color.FromArgb(160, 160, 160);
            controlBox.EnableMaximizeButton = false;
            controlBox.EnableMaximizeColor = Color.FromArgb(160, 160, 160);
            controlBox.EnableMinimizeButton = false;
            controlBox.EnableMinimizeColor = Color.FromArgb(160, 160, 160);
            controlBox.Location = new Point(261, 0);
            controlBox.MaximizeHoverColor = Color.FromArgb(15, 255, 255, 255);
            controlBox.MaximizeHoverForeColor = Color.White;
            controlBox.MinimizeHoverColor = Color.FromArgb(15, 255, 255, 255);
            controlBox.MinimizeHoverForeColor = Color.White;
            controlBox.Name = "controlBox";
            controlBox.Size = new Size(139, 31);
            controlBox.TabIndex = 0;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblTitle.ForeColor = Color.LightGray;
            lblTitle.Location = new Point(12, 8);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(109, 15);
            lblTitle.TabIndex = 1;
            lblTitle.Text = "신규 프로젝트 생성";
            lblTitle.Click += lblTitle_Click;
            lblTitle.MouseDown += TopPanel_MouseDown;
            // 
            // lblType
            // 
            lblType.AutoSize = true;
            lblType.ForeColor = Color.White;
            lblType.Location = new Point(23, 50);
            lblType.Name = "lblType";
            lblType.Size = new Size(76, 15);
            lblType.TabIndex = 0;
            lblType.Text = "Project Type:";
            // 
            // rbVanilla
            // 
            rbVanilla.AutoSize = true;
            rbVanilla.Checked = true;
            rbVanilla.ForeColor = Color.White;
            rbVanilla.Location = new Point(23, 70);
            rbVanilla.Name = "rbVanilla";
            rbVanilla.Size = new Size(138, 19);
            rbVanilla.TabIndex = 1;
            rbVanilla.TabStop = true;
            rbVanilla.Text = "Pure Project (Vanilla)";
            rbVanilla.UseVisualStyleBackColor = true;
            rbVanilla.CheckedChanged += RbType_CheckedChanged;
            // 
            // rbModded
            // 
            rbModded.AutoSize = true;
            rbModded.ForeColor = Color.White;
            rbModded.Location = new Point(180, 70);
            rbModded.Name = "rbModded";
            rbModded.Size = new Size(111, 19);
            rbModded.TabIndex = 2;
            rbModded.Text = "Modded Project";
            rbModded.UseVisualStyleBackColor = true;
            rbModded.CheckedChanged += RbType_CheckedChanged;
            // 
            // lblName
            // 
            lblName.AutoSize = true;
            lblName.ForeColor = Color.White;
            lblName.Location = new Point(23, 100);
            lblName.Name = "lblName";
            lblName.Size = new Size(83, 15);
            lblName.TabIndex = 12;
            lblName.Text = "Project Name:";
            // 
            // txtName
            // 
            txtName.BackColor = Color.FromArgb(40, 40, 40);
            txtName.BorderStyle = BorderStyle.FixedSingle;
            txtName.ForeColor = Color.White;
            txtName.Location = new Point(23, 120);
            txtName.Name = "txtName";
            txtName.Size = new Size(350, 23);
            txtName.TabIndex = 11;
            txtName.TextChanged += TxtName_TextChanged;
            // 
            // lblPath
            // 
            lblPath.AutoSize = true;
            lblPath.ForeColor = Color.White;
            lblPath.Location = new Point(23, 175);
            lblPath.Name = "lblPath";
            lblPath.Size = new Size(181, 15);
            lblPath.TabIndex = 10;
            lblPath.Text = "Base Game Path (Modded only):";
            // 
            // txtPath
            // 
            txtPath.BackColor = Color.FromArgb(40, 40, 40);
            txtPath.BorderStyle = BorderStyle.FixedSingle;
            txtPath.Enabled = false;
            txtPath.ForeColor = Color.Gray;
            txtPath.Location = new Point(23, 195);
            txtPath.Name = "txtPath";
            txtPath.ReadOnly = true;
            txtPath.Size = new Size(270, 23);
            txtPath.TabIndex = 9;
            // 
            // btnBrowse
            // 
            btnBrowse.BackColor = Color.FromArgb(60, 60, 60);
            btnBrowse.Enabled = false;
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.FlatStyle = FlatStyle.Flat;
            btnBrowse.ForeColor = Color.Gray;
            btnBrowse.Location = new Point(298, 195);
            btnBrowse.Name = "btnBrowse";
            btnBrowse.Size = new Size(75, 23);
            btnBrowse.TabIndex = 8;
            btnBrowse.Text = "...";
            btnBrowse.UseVisualStyleBackColor = false;
            btnBrowse.Click += BtnBrowse_Click;
            // 
            // lblDesc
            // 
            lblDesc.AutoSize = true;
            lblDesc.ForeColor = Color.White;
            lblDesc.Location = new Point(23, 230);
            lblDesc.Name = "lblDesc";
            lblDesc.Size = new Size(34, 15);
            lblDesc.TabIndex = 7;
            lblDesc.Text = "설명:";
            // 
            // txtDesc
            // 
            txtDesc.BackColor = Color.FromArgb(40, 40, 40);
            txtDesc.BorderStyle = BorderStyle.FixedSingle;
            txtDesc.ForeColor = Color.White;
            txtDesc.Location = new Point(23, 250);
            txtDesc.Multiline = true;
            txtDesc.Name = "txtDesc";
            txtDesc.Size = new Size(350, 60);
            txtDesc.TabIndex = 6;
            //
            // lblGameVersion
            //
            lblGameVersion.AutoSize = true;
            lblGameVersion.ForeColor = Color.White;
            lblGameVersion.Location = new Point(23, 320);
            lblGameVersion.Name = "lblGameVersion";
            lblGameVersion.Size = new Size(83, 15);
            lblGameVersion.TabIndex = 14;
            lblGameVersion.Text = "Game Version:";
            //
            // cmbGameVersion
            //
            cmbGameVersion.BackColor = Color.FromArgb(40, 40, 40);
            cmbGameVersion.ForeColor = Color.White;
            cmbGameVersion.FlatStyle = FlatStyle.Flat;
            cmbGameVersion.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbGameVersion.Location = new Point(23, 340);
            cmbGameVersion.Name = "cmbGameVersion";
            cmbGameVersion.Size = new Size(350, 23);
            cmbGameVersion.TabIndex = 15;
            cmbGameVersion.Items.AddRange(new object[] {
                "YW1",
                "YW2",
                "YW3",
                "BS",
                "BS2"
            });
            cmbGameVersion.SelectedIndex = 1; // Default: YW2
            //
            // btnCreate
            //
            btnCreate.BackColor = Color.FromArgb(60, 60, 60);
            btnCreate.FlatAppearance.BorderSize = 0;
            btnCreate.FlatStyle = FlatStyle.Flat;
            btnCreate.ForeColor = Color.White;
            btnCreate.Location = new Point(195, 380);
            btnCreate.Name = "btnCreate";
            btnCreate.Size = new Size(85, 30);
            btnCreate.TabIndex = 5;
            btnCreate.Text = "생성";
            btnCreate.UseVisualStyleBackColor = false;
            btnCreate.Click += BtnCreate_Click;
            //
            // btnCancel
            //
            btnCancel.BackColor = Color.FromArgb(60, 60, 60);
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.ForeColor = Color.White;
            btnCancel.Location = new Point(290, 380);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(85, 30);
            btnCancel.TabIndex = 4;
            btnCancel.Text = "취소";
            btnCancel.UseVisualStyleBackColor = false;
            btnCancel.Click += BtnCancel_Click;
            // 
            // lblSaveInfo
            // 
            lblSaveInfo.AutoSize = true;
            lblSaveInfo.Font = new Font("Segoe UI", 8F);
            lblSaveInfo.ForeColor = Color.Gray;
            lblSaveInfo.Location = new Point(23, 146);
            lblSaveInfo.Name = "lblSaveInfo";
            lblSaveInfo.Size = new Size(92, 13);
            lblSaveInfo.TabIndex = 3;
            lblSaveInfo.Text = "Save Location: ...";
            // 
            // NewProjectWindow
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(32, 33, 36);
            ClientSize = new Size(400, 425);
            Controls.Add(lblType);
            Controls.Add(rbVanilla);
            Controls.Add(rbModded);
            Controls.Add(lblSaveInfo);
            Controls.Add(cmbGameVersion);
            Controls.Add(lblGameVersion);
            Controls.Add(btnCancel);
            Controls.Add(btnCreate);
            Controls.Add(txtDesc);
            Controls.Add(lblDesc);
            Controls.Add(btnBrowse);
            Controls.Add(txtPath);
            Controls.Add(lblPath);
            Controls.Add(txtName);
            Controls.Add(lblName);
            Controls.Add(topPanel);
            FormBorderStyle = FormBorderStyle.None;
            Name = "NewProjectWindow";
            Text = "Create New Project";
            topPanel.ResumeLayout(false);
            topPanel.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
