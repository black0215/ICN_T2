using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ICN_T2.Logic.Project;

namespace ICN_T2.Forms
{
    public partial class OpenProjectWindow : Form
    {
        public string? SelectedProjectPath { get; private set; }

        // Dragging
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        [DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();

        // UI
        private Panel topPanel;
        private Label lblTitle;
        private Panel headerPanel;
        private Panel listPanel;
        private Button btnOpen;
        private Button btnCancel;

        private List<Project> _projects = new();
        private int _selectedIndex = -1;
        private int _expandedIndex = -1;

        // Row layout
        private const int ROW_HEIGHT = 38;
        private const int DESC_HEIGHT = 60;
        // Adjusted column widths: Name(160), Version(80), Date(100), Modded(70), Desc(50), Path(300)
        private static readonly int[] COL_WIDTHS = { 160, 80, 100, 70, 50, 300 };
        private static readonly string[] COL_HEADERS = { "이름", "버전", "생성 날짜", "타입", "설명", "경로" };

        private string _titleText;
        private string _actionText;
        private ToolTip _toolTip; // Tooltip added

        public OpenProjectWindow(string titleText = "프로젝트 열기", string actionText = "열기")
        {
            _titleText = titleText;
            _actionText = actionText;
            _toolTip = new ToolTip(); // Init tooltip
            _toolTip.AutoPopDelay = 10000;
            _toolTip.InitialDelay = 500;
            _toolTip.ReshowDelay = 500;

            BuildUI();
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            LoadProjects();
        }

        private void TopPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); }
        }

        private void LoadProjects()
        {
            ProjectManager.EnsureProjectsRoot();
            _projects = ProjectManager.GetAvailableProjects();
            _selectedIndex = _projects.Count > 0 ? 0 : -1;
            _expandedIndex = -1;
            RebuildList();
        }

        private void RebuildList()
        {
            listPanel.SuspendLayout();
            listPanel.Controls.Clear();
            _toolTip.RemoveAll(); // Clear previous tooltips

            int y = 0;
            for (int i = 0; i < _projects.Count; i++)
            {
                var proj = _projects[i];
                int idx = i;

                // Row panel
                var row = new Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(listPanel.Width, ROW_HEIGHT),
                    BackColor = (i == _selectedIndex) ? Color.FromArgb(55, 55, 60) : Color.FromArgb(38, 38, 42),
                    Cursor = Cursors.Hand,
                    Tag = idx
                };

                // Enable double buffer
                typeof(Panel).InvokeMember("DoubleBuffered",
                    System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, row, new object[] { true });

                row.Paint += (s, e) => PaintRow(e.Graphics, row, _projects[idx], idx);

                // Click handlers
                row.MouseClick += (s, e) =>
                {
                    // Check if clicked toggle column (index 4 in new layout)
                    // Offset: 160+80+100+70 = 410. Range [410, 460).
                    int toggleX = COL_WIDTHS[0] + COL_WIDTHS[1] + COL_WIDTHS[2] + COL_WIDTHS[3]; // 410
                    if (e.X >= toggleX && e.X < toggleX + COL_WIDTHS[4] && !string.IsNullOrWhiteSpace(proj.Description))
                    {
                        _expandedIndex = (_expandedIndex == idx) ? -1 : idx;
                        RebuildList();
                        return; // Don't select row if toggling description
                    }

                    _selectedIndex = idx;
                    RebuildList();
                };

                row.MouseDoubleClick += (s, e) => ConfirmSelection();

                // Tooltip logic
                row.MouseMove += (s, e) =>
                {
                    // Path column is index 5. Offset: 410 + 50 = 460.
                    int pathX = COL_WIDTHS[0] + COL_WIDTHS[1] + COL_WIDTHS[2] + COL_WIDTHS[3] + COL_WIDTHS[4]; // 460
                    if (e.X >= pathX)
                    {
                        if (_toolTip.GetToolTip(row) != proj.RootPath)
                        {
                            _toolTip.SetToolTip(row, proj.RootPath);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(_toolTip.GetToolTip(row)))
                        {
                            _toolTip.SetToolTip(row, null);
                        }
                    }
                };

                listPanel.Controls.Add(row);
                y += ROW_HEIGHT;

                // Expand description if toggled
                if (i == _expandedIndex && !string.IsNullOrWhiteSpace(proj.Description))
                {
                    var descPanel = new Panel
                    {
                        Location = new Point(0, y),
                        Size = new Size(listPanel.Width, DESC_HEIGHT),
                        BackColor = Color.FromArgb(30, 30, 34)
                    };

                    var lblDesc = new Label
                    {
                        Text = proj.Description,
                        ForeColor = Color.FromArgb(180, 180, 180),
                        Font = new Font("Malgun Gothic", 9f),
                        Location = new Point(16, 6),
                        Size = new Size(listPanel.Width - 32, DESC_HEIGHT - 12),
                        AutoEllipsis = true
                    };
                    descPanel.Controls.Add(lblDesc);
                    listPanel.Controls.Add(descPanel);
                    y += DESC_HEIGHT;
                }

                // Separator
                var sep = new Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(listPanel.Width, 1),
                    BackColor = Color.FromArgb(55, 55, 58)
                };
                listPanel.Controls.Add(sep);
                y += 1;
            }

            listPanel.ResumeLayout();
        }

        private void PaintRow(Graphics g, Panel row, Project proj, int idx)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var font = new Font("SacheonHangGong-Regular", 10f);
            var pathFont = new Font("Malgun Gothic", 8f);
            var brush = new SolidBrush(Color.White);

            int x = 10;
            int cy = (ROW_HEIGHT - font.Height) / 2;

            // Name
            TextRenderer.DrawText(g, proj.Name, font, new Rectangle(x, cy, COL_WIDTHS[0], ROW_HEIGHT), Color.White, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            x += COL_WIDTHS[0];

            // Game Version
            string ver = string.IsNullOrEmpty(proj.GameVersion) ? "-" : proj.GameVersion;
            TextRenderer.DrawText(g, ver, font, new Rectangle(x, cy, COL_WIDTHS[1], ROW_HEIGHT), Color.FromArgb(130, 200, 255), TextFormatFlags.Left);
            x += COL_WIDTHS[1];

            // Created Date
            TextRenderer.DrawText(g, proj.CreatedDate.ToString("yy-MM-dd"), font, new Rectangle(x, cy, COL_WIDTHS[2], ROW_HEIGHT), Color.FromArgb(170, 170, 170), TextFormatFlags.Left);
            x += COL_WIDTHS[2];

            // Modding status
            string modStatus = proj.IsBasedOnModded ? "Mod" : "Pure";
            Color modColor = proj.IsBasedOnModded ? Color.FromArgb(255, 150, 100) : Color.FromArgb(100, 220, 100);
            TextRenderer.DrawText(g, modStatus, font, new Rectangle(x, cy, COL_WIDTHS[3], ROW_HEIGHT), modColor, TextFormatFlags.Left);
            x += COL_WIDTHS[3];

            // Description Toggle (Column 4)
            bool hasDesc = !string.IsNullOrWhiteSpace(proj.Description);
            string toggleText = hasDesc ? (_expandedIndex == idx ? "▲" : "▼") : "-";
            Color toggleColor = hasDesc ? Color.FromArgb(200, 200, 200) : Color.FromArgb(80, 80, 80);
            var toggleRect = new Rectangle(x, 0, COL_WIDTHS[4], ROW_HEIGHT);
            TextRenderer.DrawText(g, toggleText, font, toggleRect, toggleColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            // Toggle click handled in MouseClick above
            x += COL_WIDTHS[4];

            // Path (Column 5)
            string pathText = proj.RootPath;
            TextRenderer.DrawText(g, pathText, pathFont, new Rectangle(x, cy + 2, COL_WIDTHS[5], ROW_HEIGHT), Color.Gray, TextFormatFlags.Left | TextFormatFlags.PathEllipsis);
            x += COL_WIDTHS[5];

            // Selection indicator
            if (idx == _selectedIndex)
            {
                using (var pen = new Pen(Color.FromArgb(0, 122, 204), 2))
                    g.DrawLine(pen, 0, 0, 0, ROW_HEIGHT);
            }

            font.Dispose();
            pathFont.Dispose();
            brush.Dispose();

        }

        private void ConfirmSelection()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _projects.Count)
            {
                MessageBox.Show("프로젝트를 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedProjectPath = _projects[_selectedIndex].RootPath;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BuildUI()
        {
            this.ClientSize = new Size(760, 480); // Increased width
            this.BackColor = Color.FromArgb(32, 33, 36);
            this.StartPosition = FormStartPosition.CenterParent;

            // Top Panel
            topPanel = new Panel
            {
                BackColor = Color.FromArgb(30, 30, 30),
                Dock = DockStyle.Top,
                Height = 40
            };
            topPanel.MouseDown += TopPanel_MouseDown;

            lblTitle = new Label
            {
                Text = _titleText,
                Font = new Font("SacheonHangGong-Regular", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(12, 9)
            };
            lblTitle.MouseDown += TopPanel_MouseDown;
            topPanel.Controls.Add(lblTitle);

            // Header Panel (table header)
            headerPanel = new Panel
            {
                Location = new Point(0, 40),
                Size = new Size(560, 30),
                BackColor = Color.FromArgb(25, 25, 28)
            };

            typeof(Panel).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, headerPanel, new object[] { true });

            headerPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                var font = new Font("SacheonHangGong-Regular", 9f, FontStyle.Bold);
                int x = 10;
                int cy = (30 - font.Height) / 2;
                for (int i = 0; i < COL_HEADERS.Length; i++)
                {
                    var flags = (i == COL_HEADERS.Length - 1) ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;
                    TextRenderer.DrawText(g, COL_HEADERS[i], font, new Rectangle(x, cy, COL_WIDTHS[i], 30), Color.FromArgb(140, 140, 145), flags);
                    x += COL_WIDTHS[i];
                }
                // Bottom border
                using (var pen = new Pen(Color.FromArgb(60, 60, 65)))
                    g.DrawLine(pen, 0, 29, headerPanel.Width, 29);
                font.Dispose();
            };

            // List Panel (scrollable)
            listPanel = new Panel
            {
                Location = new Point(0, 70),
                Size = new Size(560, 350),
                AutoScroll = true,
                BackColor = Color.FromArgb(35, 35, 38)
            };

            // Buttons
            btnOpen = new Button
            {
                Text = _actionText,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("SacheonHangGong-Regular", 11F, FontStyle.Bold),
                Location = new Point(330, 430),
                Size = new Size(100, 38),
                Cursor = Cursors.Hand
            };
            btnOpen.FlatAppearance.BorderSize = 0;
            btnOpen.Click += (s, e) => ConfirmSelection();

            btnCancel = new Button
            {
                Text = "취소",
                BackColor = Color.FromArgb(65, 65, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("SacheonHangGong-Regular", 11F, FontStyle.Bold),
                Location = new Point(440, 430),
                Size = new Size(100, 38),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(btnCancel);
            this.Controls.Add(btnOpen);
            this.Controls.Add(listPanel);
            this.Controls.Add(headerPanel);
            this.Controls.Add(topPanel);

            // Border
            this.Paint += (s, e) =>
            {
                using (var p = new Pen(Color.FromArgb(70, 70, 75)))
                    e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            };
        }
    }
}
