using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using ICN_T2.Controls;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;

namespace ICN_T2.Forms.Controls
{
    public class ModdingMenu : UserControl
    {
        // ==========================================
        // [UI Components]
        // ==========================================
        private Panel _modMenuContentPanel;
        private Panel _textOverlayPanel = null!; // Transparent overlay for drawing text on top (initialized in InitializeUI)
        private Image? _imgCover;      // MenuOpen1.png (front cover)
        private Image? _imgBackground; // MenuOpen2.png (opened book background)

        // ==========================================
        // [Animation State]
        // ==========================================
        private System.Windows.Forms.Timer _modAnimTimer;
        private Stopwatch _animStopwatch;
        private bool _isAnimating = false;
        private bool _isEntrancePhase = true;
        private bool _isFlipPhase = false;
        private bool _isClosing = false;
        private Action? _onCloseComplete;

        private float _scaleFactor = 1.05f;
        private float _shearY = 0f;
        private int _currentCoverWidth;
        private int _offsetX = 0;

        private int _targetCoverWidth = 421;
        private int _targetCoverHeight = 354;

        // Background Position - Left (50), 5px below Changelog (192+30+5 = 227)
        private int _coverFinalX = 50;
        private int _coverFinalY = 227;

        private const float MAX_SHEAR_Y = 0.66f;
        private const int MAX_SWAY_X = 5;

        // Button text data for painting on Panel
        private List<(string text, Point location, Size size)> _buttonTextData = new List<(string, Point, Size)>();

        // ==========================================
        // [Game Instance]
        // ==========================================
        private IGame? _gameInstance;

        public void SetGameInstance(IGame? game)
        {
            _gameInstance = game;
        }

        // ==========================================
        // [Events]
        // ==========================================
        public event EventHandler? RequestBackToProject;
        public Action<string, string>? RequestDescriptionUpdate;

        public ModdingMenu()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
            // [Fix] Removed Dock=Fill to allow manual positioning from Parent
            this.Visible = false;

            InitializeUI();
            InitializeAnimation();
        }

        private void InitializeUI()
        {
            _modMenuContentPanel = new Panel();
            _modMenuContentPanel.Size = new Size(_targetCoverWidth, _targetCoverHeight);
            _modMenuContentPanel.BackColor = Color.Transparent;
            _modMenuContentPanel.BackgroundImageLayout = ImageLayout.Stretch;

            // [Fix] Enable double buffering to prevent overlapping artifacts
            typeof(Panel).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, _modMenuContentPanel, new object[] { true });

            // [NEW] Create transparent overlay Panel for drawing text on top of buttons
            _textOverlayPanel = new Panel();
            _textOverlayPanel.Size = _modMenuContentPanel.Size;
            _textOverlayPanel.Location = Point.Empty; // Same position as content panel within parent
            _textOverlayPanel.BackColor = Color.Transparent;

            // Enable double buffering for overlay
            typeof(Panel).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, _textOverlayPanel, new object[] { true });

            // Make overlay click-through
            _textOverlayPanel.Enabled = false;

            // Draw text on overlay (will be on top of buttons)
            _textOverlayPanel.Paint += DrawButtonTexts;

            string resPath = GetResourcePath();

            // Load both images
            if (File.Exists(Path.Combine(resPath, "MenuOpen2.png")))
                try
                {
                    _imgBackground = Image.FromFile(Path.Combine(resPath, "MenuOpen2.png"));
                    _modMenuContentPanel.BackgroundImage = _imgBackground;
                }
                catch { }

            if (File.Exists(Path.Combine(resPath, "MenuOpen1.png")))
                try { _imgCover = Image.FromFile(Path.Combine(resPath, "MenuOpen1.png")); } catch { }

            this.Controls.Add(_modMenuContentPanel);
        }

        // Draw all button texts directly on Panel
        private void DrawButtonTexts(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Font textFont = new Font("Malgun Gothic", 11F, FontStyle.Bold))
            {
                foreach (var (text, location, size) in _buttonTextData)
                {
                    using (var path = new GraphicsPath())
                    {
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };

                        var rect = new Rectangle(location, size);
                        path.AddString(text, textFont.FontFamily, (int)textFont.Style, textFont.Size, rect, sf);

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
        }

        private void InitializeAnimation()
        {
            _modAnimTimer = new System.Windows.Forms.Timer();
            _modAnimTimer.Interval = 4; // ~240 FPS
            _modAnimTimer.Tick += ModAnimationTick;
            _animStopwatch = new Stopwatch();
        }

        // ==========================================
        // [Paint Override]
        // ==========================================
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_isAnimating && _imgCover != null)
            {
                Graphics g = e.Graphics;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int cx = _coverFinalX + (_targetCoverWidth / 2);
                int cy = _coverFinalY + (_targetCoverHeight / 2);

                if (_isEntrancePhase)
                {
                    // Scale from center (105% -> 100%)
                    g.TranslateTransform(cx, cy);
                    g.ScaleTransform(_scaleFactor, _scaleFactor);
                    g.DrawImage(_imgCover, -_targetCoverWidth / 2, -_targetCoverHeight / 2, _targetCoverWidth, _targetCoverHeight);
                    g.ResetTransform();
                }
                else if (_isFlipPhase)
                {
                    // Sync Background Sway
                    if (_imgBackground != null)
                    {
                        g.DrawImage(_imgBackground, _coverFinalX + _offsetX, _coverFinalY, _targetCoverWidth, _targetCoverHeight);
                    }

                    // Diagonal Flip
                    g.ResetTransform();
                    Matrix matrix = new Matrix();

                    matrix.Translate(_coverFinalX + _offsetX, _coverFinalY);
                    matrix.Shear(0, -_shearY);

                    g.Transform = matrix;

                    Rectangle destRect = new Rectangle(0, 0, _currentCoverWidth, _targetCoverHeight);
                    g.DrawImage(_imgCover, destRect);

                    // Shadow
                    int alpha = (int)(_shearY * 480);
                    if (alpha > 150) alpha = 150;
                    using (Brush shadow = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                        g.FillRectangle(shadow, destRect);

                    g.ResetTransform();
                }
            }
        }

        public void Open(string projectName)
        {
            this.Visible = true;
            _modMenuContentPanel.Visible = false;

            StartModAnimation();
            CreateModMenuButtons(projectName);
        }

        public void Close(Action? onComplete = null)
        {
            if (_isClosing) return;

            _onCloseComplete = onComplete;
            _isClosing = true;
            _isAnimating = true;
            _isFlipPhase = true;
            _isEntrancePhase = false;

            // Hide content panel, start reverse flip
            _modMenuContentPanel.Visible = false;
            _currentCoverWidth = 0;
            _shearY = MAX_SHEAR_Y;
            _offsetX = 0;

            _animStopwatch.Restart();
            _modAnimTimer.Start();
        }

        private void StartModAnimation()
        {
            _isAnimating = true;
            _isEntrancePhase = true;
            _isFlipPhase = false;
            _scaleFactor = 1.05f;
            _shearY = 0f;
            _currentCoverWidth = _targetCoverWidth;
            _offsetX = 0;

            _modMenuContentPanel.Dock = DockStyle.None;
            _modMenuContentPanel.Location = new Point(_coverFinalX, _coverFinalY);
            _modMenuContentPanel.Visible = false;
            this.Controls.Add(_modMenuContentPanel);

            // Add overlay panel on top of content panel
            _modMenuContentPanel.Controls.Add(_textOverlayPanel);
            _textOverlayPanel.BringToFront();

            _animStopwatch.Restart();
            _modAnimTimer.Start();
        }

        private void ModAnimationTick(object? sender, EventArgs e)
        {
            long elapsedMs = _animStopwatch.ElapsedMilliseconds;

            if (_isClosing)
            {
                // === Closing Animation (Reverse) ===

                // Phase 1: Reverse Flip (Book Close) - 0 to 450ms
                if (elapsedMs < 450)
                {
                    _isEntrancePhase = false;
                    _isFlipPhase = true;

                    float flipProgress = (float)elapsedMs / 450f;
                    if (flipProgress > 1.0f) flipProgress = 1.0f;

                    // Expand width back (0 -> full)
                    _currentCoverWidth = (int)(_targetCoverWidth * flipProgress);

                    // Shear decreases (MAX -> 0)
                    _shearY = MAX_SHEAR_Y * (1.0f - flipProgress);

                    // Sway Logic (reverse)
                    if (flipProgress <= 0.5f)
                    {
                        _offsetX = -(int)(MAX_SWAY_X * (flipProgress * 2));
                    }
                    else
                    {
                        _offsetX = -(int)(MAX_SWAY_X * (2 - flipProgress * 2));
                    }

                    this.Invalidate();
                }
                // Phase 2: Reverse Entrance (Scale Up + Fade) - 450 to 600ms
                else if (elapsedMs < 600)
                {
                    _isFlipPhase = false;
                    _isEntrancePhase = true;

                    float progress = (float)(elapsedMs - 450) / 150f;
                    if (progress > 1.0f) progress = 1.0f;

                    // Scale up (100% -> 105%)
                    _scaleFactor = 1.0f + (0.05f * progress);

                    this.Invalidate();
                }
                else
                {
                    // Close animation done
                    _isFlipPhase = false;
                    _isEntrancePhase = false;
                    _isAnimating = false;
                    _isClosing = false;
                    _modAnimTimer.Stop();
                    _animStopwatch.Stop();

                    this.Visible = false;
                    this.Invalidate();

                    _onCloseComplete?.Invoke();
                    _onCloseComplete = null;
                }
            }
            else
            {
                // === Opening Animation ===

                // Phase 1: Entrance (Scale Down) - 0 to 150ms
                if (elapsedMs < 150)
                {
                    _isEntrancePhase = true;
                    _isFlipPhase = false;

                    float progress = (float)elapsedMs / 150f;
                    _scaleFactor = 1.05f - (0.05f * progress);

                    this.Invalidate();
                }
                // Phase 2: Flip (Book Open) - 150 to 600ms (Duration 450ms)
                else if (elapsedMs < 600)
                {
                    _isEntrancePhase = false;
                    _isFlipPhase = true;

                    float flipProgress = (float)(elapsedMs - 150) / 450f;
                    if (flipProgress > 1.0f) flipProgress = 1.0f;

                    // Shrink width
                    _currentCoverWidth = (int)(_targetCoverWidth * (1.0f - flipProgress));

                    // Shear
                    _shearY = MAX_SHEAR_Y * flipProgress;

                    // Sway Logic
                    if (flipProgress <= 0.5f)
                    {
                        _offsetX = -(int)(MAX_SWAY_X * (flipProgress * 2));
                    }
                    else
                    {
                        _offsetX = -(int)(MAX_SWAY_X * (2 - flipProgress * 2));
                    }

                    this.Invalidate();
                }
                else
                {
                    // Animation Done
                    _isFlipPhase = false;
                    _isAnimating = false;
                    _modMenuContentPanel.Visible = true;

                    _currentCoverWidth = 0;
                    _offsetX = 0;
                    _modAnimTimer.Stop();
                    _animStopwatch.Stop();
                    this.Invalidate();
                }
            }
        }

        private void CreateModMenuButtons(string projectName)
        {
            _modMenuContentPanel.Controls.Clear();

            // Back button with image
            string backImagePath = Path.Combine(Application.StartupPath, "Resources", "UI_icon", "Back.png");
            var btnBack = new PictureBox
            {
                Size = new Size(60, 60),
                Location = new Point(5, 0),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            if (File.Exists(backImagePath))
            {
                btnBack.Image = Image.FromFile(backImagePath);
            }

            btnBack.Click += (s, e) => RequestBackToProject?.Invoke(this, EventArgs.Empty);
            _modMenuContentPanel.Controls.Add(btnBack);

            // 캐릭터 기본정보(Character Info) 로드 요구사항 (Charabase 기준):
            // - yw2_a.fa: chara_base (캐릭터 목록/기본데이터), face_icon (얼굴 아이콘)
            // - yw2_lg_ko.fa: chara_text (이름 매핑, chara_text_ko.cfg.bin)
            var features = new List<(string Name, string EngTitle, string Desc)>
            {
                ("캐릭터\n기본정보", "Character Info", "Edit basic info like Name, Money, Time played."),
                ("캐릭터\n비율", "Model Scale", "Adjust the scale/size of the player character."),
                ("요괴\n능력치", "Yo-kai Stats", "Modify IVs, EVs, and base stats for Yo-kai."),
                ("인카운터", "Encounter Editor", "Change wild Yo-kai spawns in maps."),
                ("상점", "Shop Editor", "Edit items sold in various shops."),
                ("아이템", "Item Editor", "Modify item properties."),
                ("퀘스트", "Quest Editor", "Edit quest requirements and rewards."),
                ("대화", "Text Editor", "Modify game dialogues."),
                ("배틀", "Battle Config", "Edit battle parameters."),
                ("맵", "Map Editor", "View and edit map entities."),
                ("전체 저장", "Full Save", "Export the complete yw2_a.fa archive to /Export folder."),
                ("설정", "Settings", "Tool configuration.") // 12th Button
            };

            // [Fix Phase 15] 2nd Final Adjustment
            // StartX: 60 (Unchanged)
            // StartY: 35 -> 27 (Up 8px)
            // CellW: 75 (Unchanged)
            // GapY: 30 -> 25 (Reduce 5px)

            float gridStartX = 45.2f; // [Phase 32] Shifted Right +0.2px (45 -> 45.2)
            int gridStartY = 8;  // [Phase 31] Shifted Down +2px (6 -> 8)
                                 // [Phase 29] Reduced by 10% from previous size
                                 // Previous 136x101 -> New 122x91

            int cellW = 122;
            int cellH = 91;

            float gapX = -41.8f; // [Phase 31] Gap X Reduced 0.3px (-41.5 -> -41.8)
            float gapY = -13.8f; // 77.2 - 91 = -13.8

            // Resource Path for Icons
            string tribePath = Path.Combine(Application.StartupPath, "Resources", "Tribe");
            if (!Directory.Exists(tribePath))
            {
                // Fallback dev path
                string devPath = @"C:\Users\home\Desktop\ICN_T2\ICN_T2\Resources\Tribe";
                if (Directory.Exists(devPath)) tribePath = devPath;
            }

            // Iterate up to 12 items (Index 0 to 11), enabling the Settings button.
            for (int i = 0; i < features.Count && i < 12; i++)
            {
                var feat = features[i];
                AnimMenuButton btn = new AnimMenuButton();
                btn.Text = feat.Name; // Text is now rendered in button's OnPaint

                // Image Loading (1-based index)
                int iconIndex = i + 1;

                try
                {
                    string bgPath = Path.Combine(tribePath, $"icon_bag{iconIndex}.png");
                    string aFile = Path.Combine(tribePath, $"icon_a{iconIndex}.png");
                    string bFile = Path.Combine(tribePath, $"icon_b{iconIndex}.png");

                    if (File.Exists(bgPath)) btn.BgImage = Image.FromFile(bgPath);
                    if (File.Exists(aFile)) btn.FgNormal = Image.FromFile(aFile);
                    if (File.Exists(bFile)) btn.FgHover = Image.FromFile(bFile);
                }
                catch { /* Ignore missing files */ }

                // Default Style if no image
                if (btn.BgImage == null)
                {
                    btn.BackColor = Color.FromArgb(40, 40, 40);
                }
                else
                {
                    btn.BackColor = Color.Transparent;
                }

                // Custom Scale & Offset per User Request (Phase 35)
                switch (iconIndex)
                {
                    case 1:
                    case 2:
                        btn.IconScale = 1.05f;
                        btn.IconOffsetX = 0.2f;
                        btn.TextFontSizeAdjust = -1.0f; // -1pt font size
                        btn.TextOffsetY = -2.0f; // -2px up
                        break;
                    case 3:
                        btn.IconScale = 1.0f;
                        btn.IconOffsetX = 0.2f;
                        btn.TextFontSizeAdjust = -1.0f; // -1pt font size
                        btn.TextOffsetY = -2.0f; // -2px up
                        break;
                    case 7:
                        btn.IconScale = 1.05f;
                        btn.IconOffsetX = 0.2f;
                        break;
                    case 6:
                    case 9:
                    case 10:
                        btn.IconScale = 1.0f;
                        btn.IconOffsetX = 0.2f;
                        break;
                    case 4:
                    case 5:
                    case 8:
                        btn.IconScale = 1.05f;
                        btn.IconOffsetX = 0.0f;
                        break;
                    default:
                        btn.IconScale = 1.0f;
                        btn.IconOffsetX = 0.0f;
                        break;
                }

                // Position Calculation
                int col = i % 4;
                int row = i / 4;
                float x = gridStartX + col * (cellW + gapX);
                float y = gridStartY + row * (cellH + gapY);

                btn.Size = new Size(cellW, cellH);
                btn.Location = new Point((int)Math.Round(x), (int)Math.Round(y));

                // Important: Update region AFTER setting size
                btn.UpdateRegion();

                btn.MouseEnter += (s, e) =>
                {
                    RequestDescriptionUpdate?.Invoke(feat.EngTitle.ToUpper(), feat.Desc);
                };

                // Feature button click handlers
                int featureIndex = i;
                btn.Click += (s, e) => OnFeatureButtonClick(featureIndex);

                _modMenuContentPanel.Controls.Add(btn);
            }
        }

        private void OnFeatureButtonClick(int index)
        {
            System.Diagnostics.Debug.WriteLine($"[ModdingMenu] Feature button clicked: index={index}");

            if (_gameInstance == null)
            {
                System.Diagnostics.Debug.WriteLine("[ModdingMenu] ERROR: Game instance is null");
                MessageBox.Show("게임 인스턴스가 로드되지 않았습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ModdingMenu] Game instance present: {_gameInstance.Name}");

            switch (index)
            {
                case 0: // 캐릭터 기본정보 — yw2_a.fa + yw2_lg_ko.fa 로드 필요
                    System.Diagnostics.Debug.WriteLine("[ModdingMenu] Opening CharabaseWindow...");
                    if (_gameInstance is ICN_T2.YokaiWatch.Games.YW2.YW2 yw2ForChara)
                    {
                        if (yw2ForChara.Language == null)
                        {
                            System.Diagnostics.Debug.WriteLine("[ModdingMenu] WARNING: yw2_lg_ko.fa not loaded - character names may be missing.");
                            MessageBox.Show(
                                "언어 아카이브(yw2_lg_ko.fa)가 로드되지 않았습니다.\n캐릭터 이름이 표시되지 않을 수 있습니다.\n\nBase Game Path에 yw2_lg_ko.fa가 있는지 확인하세요.",
                                "캐릭터 기본정보",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                    using (var window = new ICN_T2.UI.CharabaseWindow(_gameInstance))
                    {
                        window.ShowDialog();
                    }
                    System.Diagnostics.Debug.WriteLine("[ModdingMenu] CharabaseWindow closed");
                    break;
                case 10: // 전체 저장 via YW2.SaveFullArchive
                    try
                    {
                        if (_gameInstance is YW2 yw2)
                        {
                            string exportDir = yw2.CurrentProject?.ExportsPath ?? Path.Combine(Application.StartupPath, "Exports");
                            string exportPath = Path.Combine(exportDir, "yw2_a.fa");

                            if (MessageBox.Show($"전체 아카이브를 저장하시겠습니까?\n대상 경로: {exportPath}\n\n(시간이 다소 소요될 수 있습니다)", "전체 저장 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            {
                                Cursor.Current = Cursors.WaitCursor;
                                yw2.SaveFullArchive(exportPath);
                                Cursor.Current = Cursors.Default;
                                MessageBox.Show("저장이 완료되었습니다!", "성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show("현재 게임 인스턴스가 YW2가 아닙니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        Cursor.Current = Cursors.Default;
                        MessageBox.Show($"저장 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    break;
                case 11: // Settings -> Modern UI Prototype
                    using (var webWindow = new ICN_T2.UI.ModdingWindow_Web())
                    {
                        webWindow.ShowDialog();
                    }
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine($"[ModdingMenu] Feature {index} not yet implemented");
                    MessageBox.Show("준비 중입니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }

        private string GetResourcePath()
        {
            string resPath = Path.Combine(Application.StartupPath, "Resources", "modMenu");
            if (!Directory.Exists(resPath))
            {
                string sourcePath = @"C:\Users\home\Desktop\ICN_T2\ICN_T2\Resources\modMenu";
                if (Directory.Exists(sourcePath)) resPath = sourcePath;
            }
            return resPath;
        }
    }
}
