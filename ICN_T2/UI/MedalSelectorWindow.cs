using System;
using System.Drawing;
using System.Windows.Forms;

namespace Albatross.Forms.Characters
{
    public class MedalSelectorWindow : Form
    {
        public int SelectedX = -1;
        public int SelectedY = -1;

        private int _medalSize;
        private PictureBox _faceIconPictureBox;

        public MedalSelectorWindow(Bitmap faceIcon, int medalSize)
        {
            _medalSize = medalSize;
            InitializeComponent(faceIcon);
        }

        private void InitializeComponent(Bitmap faceIcon)
        {
            this.Text = "Select Medal";
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.StartPosition = FormStartPosition.CenterParent;

            _faceIconPictureBox = new PictureBox();
            _faceIconPictureBox.Image = faceIcon;
            _faceIconPictureBox.SizeMode = PictureBoxSizeMode.AutoSize;
            _faceIconPictureBox.Cursor = Cursors.Hand;
            _faceIconPictureBox.MouseClick += FaceIconPictureBox_MouseClick;

            // Use a panel with auto-scroll in case the image is larger than the screen
            Panel scrollPanel = new Panel();
            scrollPanel.Dock = DockStyle.Fill;
            scrollPanel.AutoScroll = true;
            scrollPanel.Controls.Add(_faceIconPictureBox);

            this.Controls.Add(scrollPanel);

            // Calculate desired size, but cap it at screen size
            int desiredWidth = Math.Min(faceIcon.Width + 40, Screen.PrimaryScreen.WorkingArea.Width - 100);
            int desiredHeight = Math.Min(faceIcon.Height + 60, Screen.PrimaryScreen.WorkingArea.Height - 100);

            this.Size = new Size(desiredWidth, desiredHeight);
        }

        private void FaceIconPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            // Calculate grid position
            SelectedX = e.X / _medalSize;
            SelectedY = e.Y / _medalSize;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
