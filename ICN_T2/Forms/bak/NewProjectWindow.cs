using ReaLTaiizor.Controls;
using ICN_T2.Logic.Project;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ICN_T2.Forms
{
    public partial class NewProjectWindow : Form
    {
        public Project? CreatedProject { get; private set; }

        // Dragging Logic
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public NewProjectWindow()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
        }

        private void TopPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Base Game Folder (Vanilla)";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void RbType_CheckedChanged(object sender, EventArgs e)
        {
            // Both Modded and Vanilla require a Base Game Path now.
            // We just ensure the controls are enabled.
            txtPath.Enabled = true;
            btnBrowse.Enabled = true;
            txtPath.ForeColor = Color.White;
        }

        private void TxtName_TextChanged(object sender, EventArgs e)
        {
            string projectsRoot = ProjectManager.ProjectsRoot;
            string projectName = string.IsNullOrWhiteSpace(txtName.Text) ? "..." : txtName.Text;
            lblSaveInfo.Text = $"Save Location: {System.IO.Path.Combine(projectsRoot, projectName)}";
        }

        private void BtnCreate_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("프로젝트 이름을 입력해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Enforce path for BOTH Modded and Vanilla
                if (string.IsNullOrWhiteSpace(txtPath.Text))
                {
                    MessageBox.Show("원본 게임 파일(romFs) 폴더 위치를 지정해주세요.\n(예: sample 폴더)", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Path is always required now
                string path = txtPath.Text;

                string gameVersion = cmbGameVersion.SelectedItem?.ToString() ?? "YW2";
                CreatedProject = ProjectManager.CreateProject(txtName.Text, path, txtDesc.Text, gameVersion);

                MessageBox.Show($"프로젝트 '{CreatedProject.Name}'이(가) 성공적으로 생성되었습니다!", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create project:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void lblTitle_Click(object sender, EventArgs e)
        {

        }
    }
}
