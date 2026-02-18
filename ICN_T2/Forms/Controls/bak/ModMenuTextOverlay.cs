using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ICN_T2.Forms.Controls
{
    /// <summary>
    /// Transparent overlay control for rendering text with outline on top of other controls
    /// </summary>
    public class TextOverlay : Control
    {
        public TextOverlay()
        {
            // Enable transparency and double buffering
            this.SetStyle(
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint,
                true);

            this.BackColor = Color.Transparent;
            this.ForeColor = Color.White;
            this.Font = new Font("Malgun Gothic", 11F, FontStyle.Bold);

            // Make click-through
            this.Enabled = false;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Don't paint background - keep fully transparent
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (string.IsNullOrEmpty(this.Text))
                return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var path = new GraphicsPath())
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                path.AddString(
                    this.Text,
                    this.Font.FontFamily,
                    (int)this.Font.Style,
                    this.Font.Size,
                    this.ClientRectangle,
                    sf);

                // Draw outline (black)
                using (var pen = new Pen(Color.Black, 3) { LineJoin = LineJoin.Round })
                {
                    g.DrawPath(pen, path);
                }

                // Fill text (white or custom ForeColor)
                using (var brush = new SolidBrush(this.ForeColor))
                {
                    g.FillPath(brush, path);
                }
            }
        }
    }
}
