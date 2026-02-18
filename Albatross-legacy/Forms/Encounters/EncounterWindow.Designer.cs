namespace Albatross.Forms.Encounters
{
    partial class EncounterWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EncounterWindow));
            this.mapListBox = new System.Windows.Forms.ListBox();
            this.searchTextBox = new System.Windows.Forms.TextBox();
            this.encounterDataGridView = new System.Windows.Forms.DataGridView();
            this.hashTextBox = new System.Windows.Forms.TextBox();
            this.hashLabel = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.mapGroupBox = new System.Windows.Forms.GroupBox();
            this.tableFlatComboBox = new Albatross.UI.FlatComboBox();
            this.tableContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.charaContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.removeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.Column3 = new System.Windows.Forms.DataGridViewImageColumn();
            this.Column1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnUnk1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnUnk2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnUnk4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnUnk5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnUnk7 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.encounterDataGridView)).BeginInit();
            this.mapGroupBox.SuspendLayout();
            this.tableContextMenuStrip.SuspendLayout();
            this.charaContextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // mapListBox
            // 
            this.mapListBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            this.mapListBox.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.mapListBox.FormattingEnabled = true;
            this.mapListBox.ItemHeight = 12;
            this.mapListBox.Location = new System.Drawing.Point(14, 35);
            this.mapListBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.mapListBox.Name = "mapListBox";
            this.mapListBox.Size = new System.Drawing.Size(476, 328);
            this.mapListBox.Sorted = true;
            this.mapListBox.TabIndex = 9;
            this.mapListBox.SelectedIndexChanged += new System.EventHandler(this.MapListBox_SelectedIndexChanged);
            // 
            // searchTextBox
            // 
            this.searchTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            this.searchTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.searchTextBox.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.searchTextBox.Location = new System.Drawing.Point(14, 11);
            this.searchTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.searchTextBox.Name = "searchTextBox";
            this.searchTextBox.Size = new System.Drawing.Size(476, 14);
            this.searchTextBox.TabIndex = 8;
            this.searchTextBox.Text = "검색...";
            // 
            // encounterDataGridView
            // 
            this.encounterDataGridView.AllowUserToAddRows = false;
            this.encounterDataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.encounterDataGridView.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.encounterDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.encounterDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.encounterDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column3,
            this.Column1,
            this.Column2,
            this.Column4,
            this.Column5,
            this.ColumnUnk1,
            this.ColumnUnk2,
            this.ColumnUnk4,
            this.ColumnUnk5,
            this.ColumnUnk7});
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.encounterDataGridView.DefaultCellStyle = dataGridViewCellStyle3;
            this.encounterDataGridView.Enabled = false;
            this.encounterDataGridView.EnableHeadersVisualStyles = false;
            this.encounterDataGridView.GridColor = System.Drawing.SystemColors.ControlDarkDark;
            this.encounterDataGridView.Location = new System.Drawing.Point(23, 64);
            this.encounterDataGridView.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.encounterDataGridView.Name = "encounterDataGridView";
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.encounterDataGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.encounterDataGridView.RowHeadersVisible = false;
            dataGridViewCellStyle5.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            this.encounterDataGridView.RowsDefaultCellStyle = dataGridViewCellStyle5;
            this.encounterDataGridView.Size = new System.Drawing.Size(650, 257);
            this.encounterDataGridView.TabIndex = 49;
            this.encounterDataGridView.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.EncounterDataGridView_CellValueChanged);
            this.encounterDataGridView.CurrentCellDirtyStateChanged += new System.EventHandler(this.EncounterDataGridView_CurrentCellDirtyStateChanged);
            this.encounterDataGridView.MouseDown += new System.Windows.Forms.MouseEventHandler(this.EncounterDataGridView_MouseDown);
            // 
            // hashTextBox
            // 
            this.hashTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            this.hashTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.hashTextBox.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.hashTextBox.Location = new System.Drawing.Point(362, 30);
            this.hashTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.hashTextBox.Name = "hashTextBox";
            this.hashTextBox.ReadOnly = true;
            this.hashTextBox.Size = new System.Drawing.Size(132, 14);
            this.hashTextBox.TabIndex = 61;
            // 
            // hashLabel
            // 
            this.hashLabel.AutoSize = true;
            this.hashLabel.Enabled = false;
            this.hashLabel.Location = new System.Drawing.Point(317, 30);
            this.hashLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.hashLabel.Name = "hashLabel";
            this.hashLabel.Size = new System.Drawing.Size(34, 12);
            this.hashLabel.TabIndex = 60;
            this.hashLabel.Text = "Hash";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(20, 30);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(37, 12);
            this.label6.TabIndex = 59;
            this.label6.Text = "Table";
            // 
            // mapGroupBox
            // 
            this.mapGroupBox.Controls.Add(this.encounterDataGridView);
            this.mapGroupBox.Controls.Add(this.hashTextBox);
            this.mapGroupBox.Controls.Add(this.label6);
            this.mapGroupBox.Controls.Add(this.hashLabel);
            this.mapGroupBox.Controls.Add(this.tableFlatComboBox);
            this.mapGroupBox.Enabled = false;
            this.mapGroupBox.ForeColor = System.Drawing.Color.White;
            this.mapGroupBox.Location = new System.Drawing.Point(500, 30);
            this.mapGroupBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.mapGroupBox.Name = "mapGroupBox";
            this.mapGroupBox.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.mapGroupBox.Size = new System.Drawing.Size(690, 334);
            this.mapGroupBox.TabIndex = 63;
            this.mapGroupBox.TabStop = false;
            this.mapGroupBox.Text = "지도";
            // 
            // tableFlatComboBox
            // 
            this.tableFlatComboBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            this.tableFlatComboBox.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(54)))), ((int)(((byte)(54)))), ((int)(((byte)(54)))));
            this.tableFlatComboBox.ButtonColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(88)))), ((int)(((byte)(88)))));
            this.tableFlatComboBox.ContextMenuStrip = this.tableContextMenuStrip;
            this.tableFlatComboBox.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.tableFlatComboBox.FormattingEnabled = true;
            this.tableFlatComboBox.Location = new System.Drawing.Point(69, 27);
            this.tableFlatComboBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.tableFlatComboBox.Name = "tableFlatComboBox";
            this.tableFlatComboBox.Size = new System.Drawing.Size(166, 20);
            this.tableFlatComboBox.TabIndex = 62;
            this.tableFlatComboBox.SelectedIndexChanged += new System.EventHandler(this.TableFlatComboBox_SelectedIndexChanged);
            // 
            // tableContextMenuStrip
            // 
            this.tableContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addToolStripMenuItem,
            this.removeToolStripMenuItem1});
            this.tableContextMenuStrip.Name = "tableContextMenuStrip";
            this.tableContextMenuStrip.Size = new System.Drawing.Size(118, 48);
            // 
            // addToolStripMenuItem
            // 
            this.addToolStripMenuItem.Name = "addToolStripMenuItem";
            this.addToolStripMenuItem.Size = new System.Drawing.Size(117, 22);
            this.addToolStripMenuItem.Text = "Add";
            this.addToolStripMenuItem.Click += new System.EventHandler(this.AddToolStripMenuItem_Click);
            // 
            // removeToolStripMenuItem1
            // 
            this.removeToolStripMenuItem1.Name = "removeToolStripMenuItem1";
            this.removeToolStripMenuItem1.Size = new System.Drawing.Size(117, 22);
            this.removeToolStripMenuItem1.Text = "Remove";
            this.removeToolStripMenuItem1.Click += new System.EventHandler(this.RemoveToolStripMenuItem1_Click);
            // 
            // charaContextMenuStrip
            // 
            this.charaContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addToolStripMenuItem1,
            this.removeToolStripMenuItem});
            this.charaContextMenuStrip.Name = "charaContextMenuStrip";
            this.charaContextMenuStrip.Size = new System.Drawing.Size(119, 48);
            // 
            // addToolStripMenuItem1
            // 
            this.addToolStripMenuItem1.Name = "addToolStripMenuItem1";
            this.addToolStripMenuItem1.Size = new System.Drawing.Size(118, 22);
            this.addToolStripMenuItem1.Text = "Add";
            this.addToolStripMenuItem1.Click += new System.EventHandler(this.AddToolStripMenuItem1_Click);
            // 
            // removeToolStripMenuItem
            // 
            this.removeToolStripMenuItem.Name = "removeToolStripMenuItem";
            this.removeToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
            this.removeToolStripMenuItem.Text = "Remove";
            this.removeToolStripMenuItem.Click += new System.EventHandler(this.RemoveToolStripMenuItem_Click);
            // 
            // Column3
            // 
            this.Column3.HeaderText = "사진";
            this.Column3.Name = "Column3";
            // 
            // Column1
            // 
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            this.Column1.DefaultCellStyle = dataGridViewCellStyle2;
            this.Column1.HeaderText = "요괴";
            this.Column1.Name = "Column1";
            this.Column1.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.Column1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.Column1.Width = 200;
            // 
            // Column2
            // 
            this.Column2.HeaderText = "최대 레벨";
            this.Column2.Name = "Column2";
            //
            // Column4
            // 
            this.Column4.HeaderText = "최소 레벨";
            this.Column4.Name = "Column4";
            // 
            // Column5
            // 
            this.Column5.HeaderText = "확률";
            this.Column5.Name = "Column5";
            // 
            // ColumnUnk1
            // 
            this.ColumnUnk1.HeaderText = "Min Level (Unk1)";
            this.ColumnUnk1.Name = "ColumnUnk1";
            // 
            // ColumnUnk2
            // 
            this.ColumnUnk2.HeaderText = "Probability (Unk2)";
            this.ColumnUnk2.Name = "ColumnUnk2";
            // 
            // ColumnUnk4
            // 
            this.ColumnUnk4.HeaderText = "Unk4";
            this.ColumnUnk4.Name = "ColumnUnk4";
            // 
            // ColumnUnk5
            // 
            this.ColumnUnk5.HeaderText = "Unk5 (Float)";
            this.ColumnUnk5.Name = "ColumnUnk5";
            // 
            // ColumnUnk7
            // 
            this.ColumnUnk7.HeaderText = "Unk7";
            this.ColumnUnk7.Name = "ColumnUnk7";
            // 
            // EncounterWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(54)))), ((int)(((byte)(54)))), ((int)(((byte)(54)))));
            this.ClientSize = new System.Drawing.Size(1200, 390);
            this.Controls.Add(this.mapGroupBox);
            this.Controls.Add(this.mapListBox);
            this.Controls.Add(this.searchTextBox);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "EncounterWindow";
            this.Text = "Encounter";
            this.Load += new System.EventHandler(this.EncounterWindow_Load);
            ((System.ComponentModel.ISupportInitialize)(this.encounterDataGridView)).EndInit();
            this.mapGroupBox.ResumeLayout(false);
            this.mapGroupBox.PerformLayout();
            this.tableContextMenuStrip.ResumeLayout(false);
            this.charaContextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ListBox mapListBox;
        private System.Windows.Forms.TextBox searchTextBox;
        private System.Windows.Forms.DataGridView encounterDataGridView;
        private System.Windows.Forms.TextBox hashTextBox;
        private System.Windows.Forms.Label hashLabel;
        private System.Windows.Forms.Label label6;
        private UI.FlatComboBox tableFlatComboBox;
        private System.Windows.Forms.GroupBox mapGroupBox;
        private System.Windows.Forms.ContextMenuStrip charaContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem addToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem removeToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip tableContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem addToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeToolStripMenuItem1;
        private System.Windows.Forms.DataGridViewImageColumn Column3;
        private System.Windows.Forms.DataGridViewComboBoxColumn Column1;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column2;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column4;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column5;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnUnk1;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnUnk2;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnUnk4;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnUnk5;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnUnk7;
    }
}