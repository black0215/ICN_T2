using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace ICN_T2.Controls
{
    public class AnimMenuButton : Control
    {
        private System.Windows.Forms.Timer _animTimer;

        // Image Overlays
        public Image? BgImage { get; set; }      // icon_bagN
        public Image? FgNormal { get; set; }     // icon_aN
        public Image? FgHover { get; set; }      // icon_bN

        private bool _isHovering = false;

        public AnimMenuButton()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
            this.Cursor = Cursors.Hand;
            this.ForeColor = Color.White;
            this.Font = new Font("Malgun Gothic", 11, FontStyle.Bold);

            _animTimer = new System.Windows.Forms.Timer();
            _animTimer.Interval = 10;
            _animTimer.Tick += AnimationTick;
            _animTimer.Start();
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

        // Update the region
        public void UpdateRegion()
        {
            if (BgImage != null)
            {
                // Use icon-shaped region (no text extension needed anymore)
                this.Region = GetFilledRegionFromImage(BgImage);
            }
            else
            {
                this.Region = new Region(ClientRectangle);
            }
        }


        private Region GetExtendedRegionFromImage(Image img)
        {
            // [Fix] Safety check: If control has no size yet, return blank region to avoid GDI+ "Parameter is not valid" error
            if (this.Width <= 0 || this.Height <= 0) return new Region(ClientRectangle);

            GraphicsPath path = new GraphicsPath();

            // Create a bitmap to analyze transparency
            // Note: We must match the drawing logic's scaling exactly
            using (Bitmap bmp = new Bitmap(this.Width, this.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    // Use same clearing and fitting logic as OnPaint
                    g.Clear(Color.Transparent);

                    int fitSize = (int)(Math.Min(this.Width, this.Height) * 0.95f);
                    int bagX = (this.Width - fitSize) / 2;
                    int bagY = (this.Height - fitSize) / 2;

                    g.DrawImage(img, bagX, bagY, fitSize, fitSize);
                }

                // Scanline algorithm: Fill holes in each row
                // Also extend to full width for lower portion (text area)
                int textStartY = (int)(this.Height * 0.60f); // Start extending region at 60% height

                for (int y = 0; y < bmp.Height; y++)
                {
                    int firstX = -1;
                    int lastX = -1;

                    // Find min and max X for this row
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        if (bmp.GetPixel(x, y).A > 10) // Threshold
                        {
                            if (firstX == -1) firstX = x;
                            lastX = x;
                        }
                    }

                    // If we found pixels OR we're in the text area, add a rect
                    if (firstX != -1)
                    {
                        path.AddRectangle(new Rectangle(firstX, y, lastX - firstX + 1, 1));
                    }
                    else if (y >= textStartY)
                    {
                        // Text area: add full-width rect even if icon is transparent here
                        path.AddRectangle(new Rectangle(0, y, bmp.Width, 1));
                    }
                }
            }
            // If path is empty, fallback to full rect
            if (path.PointCount == 0) return new Region(ClientRectangle);
            return new Region(path);
        }

        private Region GetFilledRegionFromImage(Image img)
        {
            // [Fix] Safety check: If control has no size yet, return blank region to avoid GDI+ "Parameter is not valid" error
            if (this.Width <= 0 || this.Height <= 0) return new Region(ClientRectangle);

            GraphicsPath path = new GraphicsPath();

            // Create a bitmap to analyze transparency
            // Note: We must match the drawing logic's scaling exactly
            using (Bitmap bmp = new Bitmap(this.Width, this.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    // Use same clearing and fitting logic as OnPaint
                    g.Clear(Color.Transparent);

                    int fitSize = (int)(Math.Min(this.Width, this.Height) * 0.95f);
                    int bagX = (this.Width - fitSize) / 2;
                    int bagY = (this.Height - fitSize) / 2;

                    g.DrawImage(img, bagX, bagY, fitSize, fitSize);
                }

                // Scanline algorithm: Fill holes in each row
                for (int y = 0; y < bmp.Height; y++)
                {
                    int firstX = -1;
                    int lastX = -1;

                    // Find min and max X for this row
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        if (bmp.GetPixel(x, y).A > 10) // Threshold
                        {
                            if (firstX == -1) firstX = x;
                            lastX = x;
                        }
                    }

                    // If we found any pixels in this row, add a rect spanning from first to last
                    if (firstX != -1)
                    {
                        // +1 to width to include the last pixel
                        path.AddRectangle(new Rectangle(firstX, y, lastX - firstX + 1, 1));
                    }
                }
            }
            // If path is empty, fallback to full rect
            if (path.PointCount == 0) return new Region(ClientRectangle);
            return new Region(path);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovering = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovering = false; Invalidate(); }

        private void AnimationTick(object? sender, EventArgs e)
        {
            // Keep background transparent when using images
            if (BgImage != null && this.BackColor != Color.Transparent)
                this.BackColor = Color.Transparent;
        }

        // Custom Icon Adjustments
        public float IconScale { get; set; } = 1.0f;
        public float IconOffsetX { get; set; } = 0.0f;

        // Custom Text Adjustments
        public float TextFontSizeAdjust { get; set; } = 0.0f; // Font size adjustment in points
        public float TextOffsetY { get; set; } = 0.0f; // Vertical offset in pixels

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Paint parent's background image (MenuOpen2.png) to avoid overlapping
            if (this.Parent is Panel panel && panel.BackgroundImage != null)
            {
                // Calculate where we are relative to parent
                var g = e.Graphics;
                var destRect = new Rectangle(-this.Left, -this.Top, panel.Width, panel.Height);
                g.DrawImage(panel.BackgroundImage, destRect);
            }
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            if (BgImage != null)
            {
                // Draw icon with text
                float fitSize = Math.Min(ClientRectangle.Width, ClientRectangle.Height) * 0.95f;
                float bagX = (ClientRectangle.Width - fitSize) / 2f;
                float bagY = (ClientRectangle.Height - fitSize) / 2f;

                // Layer 1: Bag
                g.DrawImage(BgImage, bagX, bagY, fitSize, fitSize);

                // Layer 2: Icons
                float baseIconSize = fitSize * 0.5f * IconScale;
                float centerX = bagX + fitSize / 2.0f + IconOffsetX;
                float centerY = bagY + fitSize / 2.0f;

                Image? overlay = _isHovering ? (FgHover ?? FgNormal) : FgNormal;

                if (overlay != null)
                {
                    float currentSize = baseIconSize;
                    if (_isHovering)
                    {
                        currentSize *= 1.15f;
                    }

                    float drawX = centerX - currentSize / 2.0f;
                    float drawY = centerY - currentSize / 2.0f;

                    g.DrawImage(overlay, drawX, drawY, currentSize, currentSize);
                }

                // Layer 3: Text (drawn AFTER icon, so it appears on top)
                if (!string.IsNullOrEmpty(this.Text))
                {
                    float textTopY = bagY + (fitSize * 0.50f) + TextOffsetY; // Apply vertical offset
                    float fontSize = 11F + TextFontSizeAdjust; // Apply font size adjustment

                    using (Font textFont = new Font("Malgun Gothic", fontSize, FontStyle.Bold))
                    using (var path = new GraphicsPath())
                    {
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };

                        var rect = new RectangleF(0, textTopY, ClientRectangle.Width, 50);
                        path.AddString(this.Text, textFont.FontFamily, (int)textFont.Style, textFont.Size, rect, sf);

                        // Draw outline (black)
                        using (var pen = new Pen(Color.Black, 3) { LineJoin = LineJoin.Round })
                        {
                            g.DrawPath(pen, path);
                        }

                        // Fill text (white)
                        using (var brush = new SolidBrush(Color.White))
                        {
                            g.FillPath(brush, path);
                        }
                    }
                }
            }
            else
            {
                base.OnPaint(pevent);
            }
        }
    }
}