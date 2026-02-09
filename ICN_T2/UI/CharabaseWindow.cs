using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ICN_T2.Logic.Level5.Binary;
using ICN_T2.Logic.Level5.Text;
using ICN_T2.Logic.Level5.Image;
using ICN_T2.YokaiWatch.Common;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;

namespace ICN_T2.UI
{
    public class CharabaseWindow : Form
    {
        // ==========================================
        // [Dragging]
        // ==========================================
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        [DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();        // ==========================================
        // [Game Data]
        // ==========================================
        private IGame _game;
        private CharaBase[] _allCharabases;
        private CharaBase[] _filteredCharabases;
        private CharaBase? _selectedChara;

        // Text (이름 매핑)
        private Dictionary<int, string> _nameMap = new();
        private Dictionary<int, string> _descMap = new();

        // ==========================================
        // [Icon Cache]
        // ==========================================
        private Dictionary<int, Image> _tribeIcons = new();   // tribe index -> icon
        private Dictionary<int, Image> _rankIcons = new();     // rank index -> icon
        private Dictionary<int, Image> _tribeIconsSmall = new(); // 리스트용 작은 아이콘
        private Dictionary<int, Image> _rankIconsSmall = new();
        private Image? _legendTag;       // 레전드 태그 이미지
        private Image? _classicTag;      // 클래식 태그 이미지
        private Dictionary<string, Image?> _yokaiIcons = new(); // model name -> face icon
        private Bitmap? FaceIcon; // Medal sprite sheet (face_icon.xi)

        // ==========================================
        // [UI Components]
        // ==========================================
        private Panel _topBar;
        private Label _lblTitle;
        private PictureBox _btnClose;

        // Left: List
        private Panel _listPanel;
        private TextBox _txtSearch;
        private Panel _filterPanel;
        private CheckBox _chkYokai;
        private CheckBox _chkNPC;
        private Panel _listContainer;
        private VScrollBar _scrollBar;

        // Right: Detail
        private Panel _detailPanel;
        private CharabaseDetailPanel _detailView;

        // Bottom
        private Button _btnSave;

        // State
        private int _selectedIndex = -1;
        private int _scrollOffset = 0;
        private const int ROW_HEIGHT = 60; // Increased for better readability

        // Keyboard navigation state
        private System.Diagnostics.Stopwatch? _keyHoldTimer;
        private Keys _lastKey = Keys.None;
        private int _keyRepeatDelay = 0;

        // Colors
        private static readonly Color BG_DARK = Color.FromArgb(24, 24, 28);
        private static readonly Color BG_PANEL = Color.FromArgb(32, 33, 36);
        private static readonly Color BG_ITEM = Color.FromArgb(38, 38, 42);
        private static readonly Color BG_ITEM_HOVER = Color.FromArgb(50, 50, 55);
        private static readonly Color BG_ITEM_SELECTED = Color.FromArgb(55, 65, 85);
        private static readonly Color ACCENT = Color.FromArgb(0, 122, 204);
        private static readonly Color TEXT_PRIMARY = Color.FromArgb(230, 230, 230);
        private static readonly Color TEXT_DIM = Color.FromArgb(150, 150, 155);
        private static readonly Color BORDER = Color.FromArgb(60, 60, 65);

        public CharabaseWindow(IGame game)
        {
            _game = game;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(1000, 650);
            this.BackColor = BG_DARK;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            this.KeyPreview = true; // Enable keyboard events
            this.KeyDown += CharabaseWindow_KeyDown; // Add keyboard handler
            this.KeyUp += CharabaseWindow_KeyUp; // Add key up handler

            LoadIcons();
            BuildUI();
            LoadData();
            // TODO: Add file listing utility after fixing VirtualDirectory API
        }

        private void ListDataMenuFiles()
        {
            try
            {
                if (!(_game is ICN_T2.YokaiWatch.Games.YW2.YW2 yw2)) return;

                var menuFiles = new List<string>();
                string menuPath = "data/menu/";

                System.Diagnostics.Debug.WriteLine($"[FileList] Scanning {menuPath} for .xi files...");

                // Get all files
                var directory = yw2.Game.Directory;
                // TODO: Fix VirtualDirectory API call
                /*
                foreach (var filePath in directory.GetAllFiles())
                {
                    if (filePath.StartsWith(menuPath) && filePath.EndsWith(".xi"))
                    {
                        menuFiles.Add(filePath);
                        System.Diagnostics.Debug.WriteLine($"[FileList] Found: {filePath}");
                    }
                }
                */

                // Write to file
                string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data_menu_files.txt");
                File.WriteAllLines(outputPath, menuFiles);

                System.Diagnostics.Debug.WriteLine($"[FileList] Wrote {menuFiles.Count} files to: {outputPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileList] Error: {ex.Message}");
            }
        }

        // ==========================================
        // [Icon Loading]
        // ==========================================
        private void LoadIcons()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            // Tribe Icons: all_icon_kind01_00.png ~ _11.png -> tribe 0x00 ~ 0x0B
            // tribe 0x00 = "Untribe" (새로 추가된 아이콘)
            string tribePath = Path.Combine(basePath, "Resources", "Tribe Icon");
            if (Directory.Exists(tribePath))
            {
                for (int i = 0; i <= 11; i++) // Start from 0 to include Untribe (0x00)
                {
                    string fileName = $"all_icon_kind01_{i:D2}.png";
                    string fullPath = Path.Combine(tribePath, fileName);
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            var img = Image.FromFile(fullPath);
                            _tribeIcons[i] = img; // Key = tribe value (0-based now)
                            // High-quality resizing to prevent blurriness
                            var smallImg = new Bitmap(18, 18);
                            using (var gfx = Graphics.FromImage(smallImg))
                            {
                                gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                gfx.SmoothingMode = SmoothingMode.HighQuality;
                                gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                gfx.DrawImage(img, 0, 0, 18, 18);
                            }
                            _tribeIconsSmall[i] = smallImg;
                        }
                        catch { }
                    }
                }
            }

            // Rank Icons: Rank_E.png ~ Rank_S.png -> rank 0x00 ~ 0x05
            string rankPath = Path.Combine(basePath, "Resources", "Rank Icon");
            if (Directory.Exists(rankPath))
            {
                var rankMapping = new Dictionary<int, string>
                {
                    { 0x00, "Rank_E.png" }, { 0x01, "Rank_D.png" }, { 0x02, "Rank_C.png" },
                    { 0x03, "Rank_B.png" }, { 0x04, "Rank_A.png" }, { 0x05, "Rank_S.png" }
                };

                foreach (var kv in rankMapping)
                {
                    string fullPath = Path.Combine(rankPath, kv.Value);
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            var img = Image.FromFile(fullPath);
                            _rankIcons[kv.Key] = img;
                            // High-quality resizing to prevent blurriness
                            var smallImg = new Bitmap(24, 24);
                            using (var gfx = Graphics.FromImage(smallImg))
                            {
                                gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                gfx.SmoothingMode = SmoothingMode.HighQuality;
                                gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                gfx.DrawImage(img, 0, 0, 24, 24);
                            }
                            _rankIconsSmall[kv.Key] = smallImg;
                        }
                        catch { }
                    }
                }
            }

            // Legend & Classic Tag Images
            string tagPath = Path.Combine(basePath, "Resources", "Tribe");
            if (Directory.Exists(tagPath))
            {
                string legendPath = Path.Combine(tagPath, "icon_legend.png");
                if (File.Exists(legendPath))
                {
                    try { _legendTag = Image.FromFile(legendPath); }
                    catch { }
                }

                string classicPath = Path.Combine(tagPath, "icon_classic.png");
                if (File.Exists(classicPath))
                {
                    try { _classicTag = Image.FromFile(classicPath); }
                    catch { }
                }
            }

            // Load FaceIcon (medal sprite sheet)
            try
            {
                if (_game is ICN_T2.YokaiWatch.Games.YW2.YW2 yw2)
                {
                    // Try PNG first (priority) - try multiple locations
                    string pngPath = Path.Combine(basePath, "face_icon.00.png");

                    // Try project root (3 levels up from bin/Debug/net8.0-windows)
                    if (!File.Exists(pngPath))
                    {
                        string projectRoot = Path.Combine(basePath, "..", "..", "..");
                        pngPath = Path.Combine(projectRoot, "face_icon.00.png");
                    }

                    if (File.Exists(pngPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[FaceIcon] Loading PNG (priority): {pngPath}");
                        FaceIcon = new Bitmap(pngPath);
                        System.Diagnostics.Debug.WriteLine($"[FaceIcon] PNG loaded: {FaceIcon.Width}x{FaceIcon.Height}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[FaceIcon] PNG not found, trying xi file...");

                        // 1. Try local extracted .xi file (User's test case)
                        string localXiPath = Path.Combine(basePath, "face_icon.xi");
                        if (!File.Exists(localXiPath))
                        {
                            // Check project root
                            string projectRoot = Path.Combine(basePath, "..", "..", "..");
                            string projectXiPath = Path.Combine(projectRoot, "face_icon.xi");
                            if (File.Exists(projectXiPath)) localXiPath = projectXiPath;
                        }

                        if (File.Exists(localXiPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[FaceIcon] Loading local XI file: {localXiPath}");
                            try
                            {
                                byte[] data = File.ReadAllBytes(localXiPath);
                                FaceIcon = IMGC.ToBitmap(data);
                                if (FaceIcon != null)
                                    System.Diagnostics.Debug.WriteLine($"[FaceIcon] Loaded local XI: {FaceIcon.Width}x{FaceIcon.Height}");
                                else
                                    System.Diagnostics.Debug.WriteLine($"[FaceIcon] Local XI decoding failed (IMGC.ToBitmap returned null)");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[FaceIcon] Error loading local XI: {ex.Message}");
                            }
                        }

                        // 2. Fallback to xi file from archive (if local not found or failed)
                        if (FaceIcon == null)
                        {
                            string faceIconPath = "data/menu/face_icon/face_icon.xi";
                            System.Diagnostics.Debug.WriteLine($"[FaceIcon] Attempting to load from archive: {faceIconPath}");

                            if (yw2.Game.Directory.FileExists(faceIconPath))
                            {
                                var vf = yw2.Game.Directory.GetFileStreamFromFullPath(faceIconPath);
                                if (vf != null)
                                {
                                    byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                                    if (data != null && data.Length > 0)
                                    {
                                        FaceIcon = IMGC.ToBitmap(data);
                                        if (FaceIcon != null)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[FaceIcon] Loaded from archive: {FaceIcon.Width}x{FaceIcon.Height}");
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[FaceIcon] IMGC.ToBitmap returned null - decoding failed");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[FaceIcon] xi file not found in archive or locally");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaceIcon] Error loading: {ex.Message}");
            }
        }

        // ==========================================
        // [Icon Loading Helpers]
        // ==========================================
        private Image? LoadYokaiIcon(string modelName)
        {
            // Check cache first
            if (_yokaiIcons.ContainsKey(modelName))
                return _yokaiIcons[modelName];

            // Try to load from face_icon file
            try
            {
                if (_game.Files.ContainsKey("face_icon"))
                {
                    var file = _game.Files["face_icon"];
                    if (file.File == null || file.File.Directory == null)
                    {
                        _yokaiIcons[modelName] = null;
                        return null;
                    }

                    string iconPath = file.Path + "/" + modelName + ".xi";

                    // FileExists 체크를 먼저 수행 (레거시와 동일)
                    if (!file.File.Directory.FileExists(iconPath))
                    {
                        _yokaiIcons[modelName] = null;
                        return null;
                    }

                    var vf = file.File.Directory.GetFileStreamFromFullPath(iconPath);
                    if (vf == null)
                    {
                        _yokaiIcons[modelName] = null;
                        return null;
                    }

                    byte[] imageData = vf.ByteContent ?? vf.ReadWithoutCaching();
                    if (imageData == null || imageData.Length == 0)
                    {
                        _yokaiIcons[modelName] = null;
                        return null;
                    }

                    Image? icon = IMGC.ToBitmap(imageData);
                    _yokaiIcons[modelName] = icon;
                    return icon;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaceIcon] Failed to load {modelName}: {ex.Message}");
                _yokaiIcons[modelName] = null;
            }

            return null;
        }

        // ==========================================
        // [UI Construction]
        // ==========================================
        private void BuildUI()
        {
            // --- Top Bar ---
            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(20, 20, 22)
            };
            _topBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };

            _lblTitle = new Label
            {
                Text = "캐릭터 기본정보 에디터",
                Font = new Font("SacheonHangGong-Regular", 11F, FontStyle.Bold),
                ForeColor = TEXT_PRIMARY,
                AutoSize = true,
                Location = new Point(12, 8)
            };
            _lblTitle.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };

            _btnClose = new PictureBox
            {
                Size = new Size(36, 36),
                BackColor = Color.Transparent,
                Image = null, // Will be loaded below
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand,
                Dock = DockStyle.Right,
                Padding = new Padding(4) // Add some padding to the image
            };

            string backImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "UI_icon", "Back.png");
            if (File.Exists(backImagePath))
            {
                try { _btnClose.Image = Image.FromFile(backImagePath); } catch { }
            }

            // Add hover effect
            _btnClose.MouseEnter += (s, ev) => _btnClose.BackColor = Color.FromArgb(70, 70, 75);
            _btnClose.MouseLeave += (s, ev) => _btnClose.BackColor = Color.Transparent;

            _btnClose.Click += (s, e) => this.Close();

            _topBar.Controls.Add(_lblTitle);
            _topBar.Controls.Add(_btnClose);
            this.Controls.Add(_topBar);

            // --- Left Panel (Character List) ---
            _listPanel = new Panel
            {
                Location = new Point(0, 36),
                Size = new Size(320, this.ClientSize.Height - 36),
                BackColor = BG_PANEL
            };

            // Search Box
            _txtSearch = new TextBox
            {
                Location = new Point(8, 8),
                Size = new Size(304, 28),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = TEXT_PRIMARY,
                Font = new Font("Malgun Gothic", 10F),
                BorderStyle = BorderStyle.FixedSingle
            };
            _txtSearch.TextChanged += (s, e) => ApplyFilter();
            _txtSearch.GotFocus += (s, e) => { if (_txtSearch.Text == "검색...") { _txtSearch.Text = ""; _txtSearch.ForeColor = TEXT_PRIMARY; } };
            _txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_txtSearch.Text)) { _txtSearch.Text = "검색..."; _txtSearch.ForeColor = TEXT_DIM; } };
            _txtSearch.Text = "검색...";
            _txtSearch.ForeColor = TEXT_DIM;
            _listPanel.Controls.Add(_txtSearch);

            // Filter Panel
            _filterPanel = new Panel
            {
                Location = new Point(8, 40),
                Size = new Size(304, 28),
                BackColor = Color.Transparent
            };

            _chkYokai = new CheckBox
            {
                Text = "요괴",
                Checked = true,
                Location = new Point(0, 2),
                AutoSize = true,
                ForeColor = TEXT_PRIMARY,
                Font = new Font("Malgun Gothic", 9F)
            };
            _chkYokai.CheckedChanged += (s, e) => ApplyFilter();

            _chkNPC = new CheckBox
            {
                Text = "NPC",
                Checked = true,
                Location = new Point(80, 2),
                AutoSize = true,
                ForeColor = TEXT_PRIMARY,
                Font = new Font("Malgun Gothic", 9F)
            };
            _chkNPC.CheckedChanged += (s, e) => ApplyFilter();

            _filterPanel.Controls.Add(_chkYokai);
            _filterPanel.Controls.Add(_chkNPC);
            _listPanel.Controls.Add(_filterPanel);

            // ScrollBar
            _scrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Minimum = 0,
                Maximum = 0,
                SmallChange = 1,
                LargeChange = 5,
                Width = 14
            };
            _scrollBar.ValueChanged += (s, e) =>
            {
                _scrollOffset = _scrollBar.Value;
                _listContainer.Invalidate();
            };

            // List Container (custom drawn)
            _listContainer = new Panel
            {
                Location = new Point(0, 72),
                Size = new Size(320 - _scrollBar.Width, _listPanel.Height - 72),
                BackColor = BG_ITEM
            };
            EnableDoubleBuffer(_listContainer);
            _listContainer.Paint += ListContainer_Paint;
            _listContainer.MouseClick += ListContainer_MouseClick;
            _listContainer.MouseDoubleClick += ListContainer_MouseClick;
            _listContainer.MouseWheel += ListContainer_MouseWheel;

            // ScrollBar (같은 영역에 오른쪽 배치)
            var scrollBarPanel = new Panel
            {
                Location = new Point(320 - _scrollBar.Width, 72),
                Size = new Size(_scrollBar.Width, _listPanel.Height - 72),
                BackColor = BG_PANEL
            };
            scrollBarPanel.Controls.Add(_scrollBar);

            _listPanel.Controls.Add(_listContainer);
            _listPanel.Controls.Add(scrollBarPanel);

            this.Controls.Add(_listPanel);

            // --- Right Panel (Detail Editor) ---
            _detailPanel = new Panel
            {
                Location = new Point(320, 36),
                Size = new Size(this.ClientSize.Width - 320, this.ClientSize.Height - 36 - 50),
                BackColor = BG_DARK
            };

            _detailView = new CharabaseDetailPanel(_game, FaceIcon, _nameMap, _descMap);
            _detailView.Dock = DockStyle.Fill;
            _detailPanel.Controls.Add(_detailView);

            this.Controls.Add(_detailPanel);

            // --- Bottom Bar (Save) ---
            var bottomBar = new Panel
            {
                Location = new Point(320, this.ClientSize.Height - 50),
                Size = new Size(this.ClientSize.Width - 320, 50),
                BackColor = Color.FromArgb(28, 28, 32)
            };

            _btnSave = new Button
            {
                Text = "저장",
                Size = new Size(100, 36),
                Location = new Point(bottomBar.Width - 120, 7),
                BackColor = ACCENT,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("SacheonHangGong-Regular", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += BtnSave_Click;
            bottomBar.Controls.Add(_btnSave);

            this.Controls.Add(bottomBar);

            // --- Border ---
            this.Paint += (s, e) =>
            {
                using (var p = new Pen(BORDER))
                    e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            };
        }

        // ==========================================
        // [Data Loading]
        // 캐릭터 기본정보 로드 요구사항 (ModdingMenu/Charabase 기준):
        // - yw2_a.fa: chara_base (캐릭터 목록/기본데이터), face_icon (얼굴 아이콘)
        // - yw2_lg_ko.fa: chara_text (이름 매핑, chara_text_ko.cfg.bin) — 반드시 로드 필요
        // ==========================================
        private void LoadData()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[CharabaseWindow] Starting data load...");
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Game Name: {_game.Name}");

                // yw2_lg_ko.fa → chara_text 필수 (이름 표시)
                if (_game.Files != null && !_game.Files.ContainsKey("chara_text"))
                {
                    System.Diagnostics.Debug.WriteLine("[CharabaseWindow] chara_text not found - ensure yw2_lg_ko.fa is loaded.");
                }

                // 1. 캐릭터 이름 텍스트 로드 (chara_text from yw2_lg_ko.fa)
                LoadCharaNames();

                // 2. 캐릭터 데이터 로드
                System.Diagnostics.Debug.WriteLine("[CharabaseWindow] Loading Yokai characterbase...");
                var yokai = _game.GetCharacterbase(true);
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Loaded {yokai.Length} Yokai characters");

                System.Diagnostics.Debug.WriteLine("[CharabaseWindow] Loading NPC characterbase...");
                var npc = _game.GetCharacterbase(false);
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Loaded {npc.Length} NPC characters");

                // 3. 캐릭터 설명 텍스트 로드 (YW3 전용)
                if (_game.Name == "Yo-Kai Watch 3" && _game.Files.ContainsKey("chara_desc_text"))
                {
                    LoadCharaDescriptions();
                }

                _allCharabases = yokai.Concat(npc).ToArray();
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Total characters loaded: {_allCharabases.Length}");
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Name map entries: {_nameMap.Count}");

                ApplyFilter();
                System.Diagnostics.Debug.WriteLine("[CharabaseWindow] Data load complete!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] ERROR during data load: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Stack trace: {ex.StackTrace}");
                _allCharabases = new CharaBase[0];
                _filteredCharabases = new CharaBase[0];
                MessageBox.Show($"캐릭터 데이터 로드 실패:\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadCharaNames()
        {
            _nameMap.Clear();

            try
            {
                // Files 딕셔너리에서 chara_text 로드
                if (_game.Files == null || !_game.Files.ContainsKey("chara_text"))
                {
                    System.Diagnostics.Debug.WriteLine("[CharabaseWindow] chara_text not found in Files dictionary");
                    return;
                }

                var gf = _game.Files["chara_text"];
                var vf = gf.GetStream();
                if (vf == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CharabaseWindow] chara_text stream is null");
                    return;
                }

                byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                if (data == null || data.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[CharabaseWindow] chara_text data is empty");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] chara_text loaded: {data.Length} bytes");

                // T2bþ로 파싱
                var textObj = new T2bþ(data);
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] T2bþ parsed: Nouns={textObj.Nouns.Count}, Texts={textObj.Texts.Count}");

                // Nouns 딕셔너리에서 이름 매핑 (hash -> name)
                foreach (var kv in textObj.Nouns)
                {
                    if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrEmpty(kv.Value.Strings[0].Text))
                    {
                        _nameMap[kv.Key] = kv.Value.Strings[0].Text;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Name map built: {_nameMap.Count} entries");

                // Texts 딕셔너리에서 설명 매핑 (hash -> desc) - YW2 등에서 공통 사용
                foreach (var kv in textObj.Texts)
                {
                    if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrEmpty(kv.Value.Strings[0].Text))
                    {
                        _descMap[kv.Key] = kv.Value.Strings[0].Text;
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Description map built: {_descMap.Count} entries");

                // T2bþ 파싱 실패 시 수동 파싱 폴백
                if (_nameMap.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[CharabaseWindow] Nouns empty, trying manual parse...");
                    ManualParseText(data);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Failed to load chara_text: {ex.Message}");

                // 폴백: 수동 파싱 시도
                try
                {
                    if (_game.Files != null && _game.Files.ContainsKey("chara_text"))
                    {
                        var gf = _game.Files["chara_text"];
                        var vf = gf.GetStream();
                        byte[] data = vf?.ByteContent ?? vf?.ReadWithoutCaching();
                        if (data != null) ManualParseText(data);
                    }
                }
                catch { }
            }
        }

        private void LoadCharaDescriptions()
        {
            try
            {
                if (_game.Files == null || !_game.Files.ContainsKey("chara_desc_text")) return;

                var gf = _game.Files["chara_desc_text"];
                var vf = gf.GetStream();
                byte[] data = vf?.ByteContent ?? vf?.ReadWithoutCaching();
                if (data == null) return;

                var textObj = new T2bþ(data);
                foreach (var kv in textObj.Texts)
                {
                    if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrEmpty(kv.Value.Strings[0].Text))
                    {
                        _descMap[kv.Key] = kv.Value.Strings[0].Text;
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Additional descriptions loaded: {textObj.Texts.Count} entries");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Failed to load chara_desc_text: {ex.Message}");
            }
        }

        /// <summary>
        /// 레거시 ManualParseText와 동일 — T2bþ 파싱 실패 시 CfgBin에서 직접 텍스트 추출
        /// </summary>
        private void ManualParseText(byte[] data)
        {
            try
            {
                CfgBin cfg = new CfgBin();
                cfg.Open(data);

                string[] targets = { "NOUN_INFO_BEGIN", "TEXT_INFO_BEGIN" };
                var entries = cfg.Entries.Where(x => targets.Contains(x.GetName())).ToList();
                if (entries.Count == 0 && cfg.Entries.Count > 0)
                    entries = cfg.Entries.Where(x => x.Children.Count > 10).ToList();

                foreach (var entry in entries)
                {
                    foreach (var child in entry.Children)
                    {
                        if (child.Variables.Count > 0)
                        {
                            int crc = Convert.ToInt32(child.Variables[0].Value);
                            string txt = "";
                            foreach (var v in child.Variables)
                            {
                                if (v.Value is OffsetTextPair p && !string.IsNullOrEmpty(p.Text))
                                { txt = p.Text; break; }
                                else if (v.Value is string s && !string.IsNullOrEmpty(s))
                                { txt = s; break; }
                            }
                            if (string.IsNullOrEmpty(txt)) continue;

                            if (!_nameMap.ContainsKey(crc))
                                _nameMap[crc] = txt;
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Manual parse: {_nameMap.Count} entries");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CharabaseWindow] Manual parse failed: {ex.Message}");
            }
        }

        // ==========================================
        // [Name Resolution]
        // ==========================================
        private string GetCharaName(CharaBase c)
        {
            // NameHash로 이름 검색
            if (_nameMap.TryGetValue(c.NameHash, out string name))
                return name;

            // BaseHash로도 시도
            if (_nameMap.TryGetValue(c.BaseHash, out string name2))
                return name2;

            // 폴백: 해시값 표시
            return $"0x{c.BaseHash:X8}";
        }

        private string GetCharaDisplayName(CharaBase c)
        {
            string name = GetCharaName(c);
            return name;
        }

        // ==========================================
        // [Filtering]
        // ==========================================
        private void ApplyFilter()
        {
            if (_allCharabases == null) return;

            bool showYokai = _chkYokai.Checked;
            bool showNPC = _chkNPC.Checked;
            string searchText = (_txtSearch.Text == "검색..." || string.IsNullOrWhiteSpace(_txtSearch.Text)) ? "" : _txtSearch.Text.ToLower();

            _filteredCharabases = _allCharabases.Where(c =>
            {
                if (c.IsYokai && !showYokai) return false;
                if (!c.IsYokai && !showNPC) return false;

                if (!string.IsNullOrEmpty(searchText))
                {
                    string display = GetCharaDisplayName(c).ToLower();
                    string hash = c.BaseHash.ToString("X8").ToLower();
                    if (!display.Contains(searchText) && !hash.Contains(searchText))
                        return false;
                }

                return true;
            }).ToArray();

            _selectedIndex = _filteredCharabases.Length > 0 ? 0 : -1;
            _scrollOffset = 0;

            // 스크롤바 업데이트
            UpdateScrollBar();

            _listContainer.Invalidate();

            if (_selectedIndex >= 0)
                SelectCharacter(_filteredCharabases[_selectedIndex]);
            else
                _detailView.ClearDetail();
        }

        private void UpdateScrollBar()
        {
            int visibleRows = _listContainer.Height / ROW_HEIGHT;
            int totalRows = _filteredCharabases?.Length ?? 0;

            if (totalRows <= visibleRows)
            {
                _scrollBar.Maximum = 0;
                _scrollBar.Enabled = false;
            }
            else
            {
                _scrollBar.Enabled = true;
                _scrollBar.Maximum = totalRows - visibleRows + _scrollBar.LargeChange - 1;
                _scrollBar.Value = Math.Min(_scrollBar.Value, Math.Max(0, totalRows - visibleRows));
            }
        }

        // ==========================================
        // [List Rendering]
        // ==========================================
        private void ListContainer_Paint(object? sender, PaintEventArgs e)
        {
            if (_filteredCharabases == null) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int visibleRows = _listContainer.Height / ROW_HEIGHT + 1;
            int startIdx = _scrollOffset;
            int endIdx = Math.Min(startIdx + visibleRows, _filteredCharabases.Length);

            using var fontName = new Font("Malgun Gothic", 10f, FontStyle.Bold);
            using var fontSub = new Font("Malgun Gothic", 7.5f);
            using var fontTribe = new Font("Malgun Gothic", 10f, FontStyle.Bold); // Reduced by 1pt
            using var fontHash = new Font("Consolas", 7.5f);
            using var fontFlag = new Font("Malgun Gothic", 9f, FontStyle.Bold);

            for (int i = startIdx; i < endIdx; i++)
            {
                var c = _filteredCharabases[i];
                int y = (i - _scrollOffset) * ROW_HEIGHT;

                // Background
                Color bgColor = (i == _selectedIndex) ? BG_ITEM_SELECTED : (i % 2 == 0 ? BG_ITEM : Color.FromArgb(35, 35, 39));
                using (var brush = new SolidBrush(bgColor))
                using (var bgBrush = new SolidBrush(bgColor))
                    g.FillRectangle(bgBrush, 0, y, _listContainer.Width, ROW_HEIGHT);

                // Selection indicator (왼쪽 파란 바)
                if (i == _selectedIndex)
                {
                    using (var pen = new Pen(ACCENT, 3))
                        g.DrawLine(pen, 1, y, 1, y + ROW_HEIGHT);
                }

                int xOffset = 8;

                // 종족 아이콘 & 종족 이름 (side by side)
                // Special case: if prefix == 5, always show "보스" in red
                if (c.IsYokai && c.FileNamePrefix == 5)
                {
                    // Prefix 5 = Boss, show boss icon and "보스" label
                    if (_tribeIconsSmall.ContainsKey(0))
                    {
                        // Draw boss icon (all_icon_kind01_00.png)
                        g.DrawImage(_tribeIconsSmall[0], xOffset, y + 5, 18, 18);
                    }
                    string bossLabel = "보스";
                    Color bossColor = Color.FromArgb(223, 0, 0);
                    TextRenderer.DrawText(g, bossLabel, fontTribe, new Rectangle(xOffset + 20, y + 2, 90, 24), bossColor, TextFormatFlags.Left);
                }
                else if (c.IsYokai && _tribeIconsSmall.ContainsKey(c.Tribe))
                {
                    // Draw icon
                    g.DrawImage(_tribeIconsSmall[c.Tribe], xOffset, y + 5, 18, 18);

                    // Draw tribe name next to icon
                    if (_game.Tribes.ContainsKey(c.Tribe))
                    {
                        string tribeName = _game.Tribes[c.Tribe];
                        // Tribe 0x00 (Untribe) uses default TEXT_DIM color
                        Color tribeColor = (c.Tribe == 0x00) ? TEXT_DIM : GetTribeColor(c.Tribe);
                        TextRenderer.DrawText(g, tribeName, fontTribe, new Rectangle(xOffset + 15, y + 4, 90, 24), tribeColor, TextFormatFlags.Left);
                    }
                }
                else if (c.IsYokai && _game.Tribes.ContainsKey(c.Tribe))
                {
                    // No icon, just text (for missing icon)
                    string tribeName = _game.Tribes[c.Tribe];
                    // Tribe 0x00 (Untribe) uses default TEXT_DIM color
                    Color tribeColor = (c.Tribe == 0x00) ? TEXT_DIM : GetTribeColor(c.Tribe);
                    TextRenderer.DrawText(g, tribeName, fontTribe, new Rectangle(xOffset, y + 2, 90, 24), tribeColor, TextFormatFlags.Left);
                }
                else
                {
                    // 아이콘 없으면 텍스트 뱃지 (NPC)
                    string badge = c.IsYokai ? "요괴" : "NPC";
                    Color badgeColor = c.IsYokai ? Color.FromArgb(100, 180, 255) : Color.FromArgb(180, 180, 100);
                    TextRenderer.DrawText(g, badge, fontSub, new Rectangle(xOffset, y + 2, 36, 16), badgeColor, TextFormatFlags.Left);
                }

                // Rank Icon + Legend/Classic Icons (오른쪽 상단)
                // Draw from right to left: Rank -> Classic -> Legend
                int iconX = _listContainer.Width - 32;

                // Rank Icon (draw first, rightmost position)
                if (c.IsYokai && _rankIconsSmall.ContainsKey(c.Rank))
                {
                    g.DrawImage(_rankIconsSmall[c.Rank], iconX, y + 4, 24, 24);
                }
                else if (c.IsYokai)
                {
                    string rankStr = Ranks.YW.ContainsKey(c.Rank) ? Ranks.YW[c.Rank] : "?";
                    Color rankColor = GetRankColor(c.Rank);
                    TextRenderer.DrawText(g, rankStr, fontSub, new Rectangle(iconX + 2, y + 2, 24, 16), rankColor, TextFormatFlags.Right);
                }
                iconX -= 28; // Move left for next icon

                // Classic tag (text flag "C")
                if (c.IsClassic)
                {
                    TextRenderer.DrawText(g, "C", fontFlag, new Rectangle(iconX, y + 3, 24, 20), Color.FromArgb(255, 200, 100), TextFormatFlags.HorizontalCenter);
                    iconX -= 28; // Move left for next icon
                }


                // Legend tag (text flag "L")
                if (c.IsLegend)
                {
                    TextRenderer.DrawText(g, "L", fontFlag, new Rectangle(iconX, y + 3, 24, 20), Color.FromArgb(255, 200, 100), TextFormatFlags.HorizontalCenter);
                }

                // 캐릭터 이름 (한글 이름 또는 해시)
                string charaName = GetCharaName(c);
                TextRenderer.DrawText(g, charaName, fontName, new Rectangle(8, y + 24, _listContainer.Width - 90, 20), TEXT_PRIMARY, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                // 해시 ID (이름이 해시가 아닐 때만 표시) / Variant 라벨
                if (!charaName.StartsWith("0x"))
                {
                    // Variant에 따른 라벨 표시
                    string variantLabel = "";
                    if (c.FileNameVariant > 0 && c.FileNameVariant < 6)
                    {
                        variantLabel = "이격";
                    }
                    else if (c.FileNameVariant == 60)
                    {
                        variantLabel = "지명수배";
                    }

                    if (!string.IsNullOrEmpty(variantLabel))
                    {
                        // Variant 라벨 표시 (랭크 아이콘 밑)
                        TextRenderer.DrawText(g, variantLabel, fontHash, new Rectangle(_listContainer.Width - 85, y + 28, 78, 14), Color.FromArgb(255, 180, 100), TextFormatFlags.Right);
                    }
                    else
                    {
                        // 기본: 해시 ID 표시
                        string hashStr = $"0x{c.BaseHash:X8}";
                        TextRenderer.DrawText(g, hashStr, fontHash, new Rectangle(_listContainer.Width - 85, y + 28, 78, 14), TEXT_DIM, TextFormatFlags.Right);
                    }
                }


                // Rare flag (text) - moved to top right area
                if (c.IsRare)
                {
                    string rareText = "R";
                    TextRenderer.DrawText(g, rareText, fontFlag, new Rectangle(_listContainer.Width - 85, y + 3, 20, 18), Color.FromArgb(255, 200, 100), TextFormatFlags.Right);
                }

                // Separator
                using (var pen = new Pen(Color.FromArgb(45, 45, 50)))
                    g.DrawLine(pen, 0, y + ROW_HEIGHT - 1, _listContainer.Width, y + ROW_HEIGHT - 1);
            }

            // Count label
            string countText = $"{_filteredCharabases.Length}개 / 전체 {_allCharabases?.Length ?? 0}개";
            TextRenderer.DrawText(g, countText, fontSub,
                new Rectangle(0, _listContainer.Height - 20, _listContainer.Width - 8, 20),
                TEXT_DIM, TextFormatFlags.Right | TextFormatFlags.Bottom);
        }

        // ==========================================
        // [List Interaction]
        // ==========================================
        private void ListContainer_MouseClick(object? sender, MouseEventArgs e)
        {
            if (_filteredCharabases == null) return;

            int clickedIndex = _scrollOffset + (e.Y / ROW_HEIGHT);
            if (clickedIndex >= 0 && clickedIndex < _filteredCharabases.Length)
            {
                _selectedIndex = clickedIndex;
                _listContainer.Invalidate();
                SelectCharacter(_filteredCharabases[_selectedIndex]);
            }
        }

        private void ListContainer_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (_filteredCharabases == null) return;

            int visibleRows = _listContainer.Height / ROW_HEIGHT;
            int maxScroll = Math.Max(0, _filteredCharabases.Length - visibleRows);
            _scrollOffset -= e.Delta / 120 * 3;
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, maxScroll));

            // 스크롤바 동기화
            if (_scrollBar.Enabled && _scrollBar.Maximum > 0)
                _scrollBar.Value = Math.Min(_scrollOffset, _scrollBar.Maximum - _scrollBar.LargeChange + 1);

            _listContainer.Invalidate();
        }

        private void SelectCharacter(CharaBase chara)
        {
            _selectedChara = chara;
            _detailView.LoadCharacter(chara);
        }

        // ==========================================
        // [Save]
        // ==========================================
        private void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_selectedChara != null)
                    _detailView.ApplyChanges();

                _game.SaveCharaBase(_allCharabases);
                MessageBox.Show("캐릭터 기본정보가 저장되었습니다.", "저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 실패:\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==========================================
        // [Color Helpers]
        // ==========================================
        private static Color GetTribeColor(int tribe)
        {
            return tribe switch
            {
                0x00 => Color.FromArgb(223, 0, 0),   // Untribe - Red
                0x01 => Color.FromArgb(255, 73, 64),   // 용맹족 - Red
                0x02 => Color.FromArgb(244, 231, 45),  // 불가사의 - Yellow
                0x03 => Color.FromArgb(255, 133, 44),  // 호걸족 - Orange
                0x04 => Color.FromArgb(255, 128, 181), // 프리티족 - Pink
                0x05 => Color.FromArgb(66, 226, 131),  // 따끈따끈 - Green
                0x06 => Color.FromArgb(90, 158, 255),  // 어스름 - Blue
                0x07 => Color.FromArgb(197, 76, 241),  // 불쾌 - Purple
                0x08 => Color.FromArgb(52, 210, 241),  // 뽀로롱 - Cyan
                0x09 => Color.FromArgb(91, 89, 121),   // 마괴 - Gray
                0x0A => Color.FromArgb(255, 73, 64),   // Default
                _ => TEXT_DIM
            };
        }

        // ==========================================
        // [Keyboard Navigation]
        // ==========================================
        private void CharabaseWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_filteredCharabases == null || _filteredCharabases.Length == 0)
                return;

            // Check if this is a new key or continuing from previous
            if (_lastKey != e.KeyCode)
            {
                _lastKey = e.KeyCode;
                _keyHoldTimer = System.Diagnostics.Stopwatch.StartNew();
                _keyRepeatDelay = 0;
            }

            // Calculate delay based on hold time
            long holdTime = _keyHoldTimer?.ElapsedMilliseconds ?? 0;
            int delay;

            if (holdTime < 3000) // 0-3 seconds: slow
            {
                delay = 200; // 5 moves per second
            }
            else if (holdTime < 8000) // 3-8 seconds: accelerate
            {
                // Gradual acceleration from 200ms to 50ms
                float progress = (holdTime - 3000f) / 5000f; // 0.0 to 1.0
                delay = (int)(200 - (150 * progress)); // 200ms -> 50ms
            }
            else // 8+ seconds: max speed
            {
                delay = 50; // 20 moves per second
            }

            // Check if enough time has passed since last move
            if (_keyRepeatDelay > delay)
            {
                _keyRepeatDelay = 0;

                switch (e.KeyCode)
                {
                    case Keys.Up:
                        if (_selectedIndex > 0)
                        {
                            _selectedIndex--;
                            EnsureVisible(_selectedIndex);
                            _listContainer.Invalidate();
                            SelectCharacter(_filteredCharabases[_selectedIndex]);
                        }
                        e.Handled = true;
                        break;

                    case Keys.Down:
                        if (_selectedIndex < _filteredCharabases.Length - 1)
                        {
                            _selectedIndex++;
                            EnsureVisible(_selectedIndex);
                            _listContainer.Invalidate();
                            SelectCharacter(_filteredCharabases[_selectedIndex]);
                        }
                        e.Handled = true;
                        break;

                    case Keys.Enter:
                        if (_selectedIndex >= 0 && _selectedIndex < _filteredCharabases.Length)
                        {
                            SelectCharacter(_filteredCharabases[_selectedIndex]);
                        }
                        e.Handled = true;
                        break;
                }
            }
            else
            {
                _keyRepeatDelay += 16; // ~60fps check rate
            }
        }

        private void CharabaseWindow_KeyUp(object? sender, KeyEventArgs e)
        {
            // Reset timer when key is released
            if (_lastKey == e.KeyCode)
            {
                _lastKey = Keys.None;
                _keyHoldTimer?.Stop();
                _keyHoldTimer = null;
                _keyRepeatDelay = 0;
            }
        }

        // ==========================================
        // [Scroll Helper]
        // ==========================================
        private void EnsureVisible(int index)
        {
            int visibleRows = _listContainer.Height / ROW_HEIGHT;

            // Scroll up if needed
            if (index < _scrollOffset)
            {
                _scrollOffset = index;
                UpdateScrollBar();
            }
            // Scroll down if needed
            else if (index >= _scrollOffset + visibleRows)
            {
                _scrollOffset = index - visibleRows + 1;
                UpdateScrollBar();
            }
        }

        private static Color GetRankColor(int rank)
        {
            return rank switch
            {
                0x00 => Color.FromArgb(150, 150, 150),
                0x01 => Color.FromArgb(130, 200, 130),
                0x02 => Color.FromArgb(100, 180, 255),
                0x03 => Color.FromArgb(200, 130, 255),
                0x04 => Color.FromArgb(255, 180, 80),
                0x05 => Color.FromArgb(255, 220, 50),
                _ => TEXT_DIM
            };
        }

        private static void EnableDoubleBuffer(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var img in _tribeIcons.Values) img?.Dispose();
                foreach (var img in _rankIcons.Values) img?.Dispose();
                foreach (var img in _tribeIconsSmall.Values) img?.Dispose();
                foreach (var img in _rankIconsSmall.Values) img?.Dispose();
                foreach (var img in _yokaiIcons.Values) img?.Dispose(); // Yokai face icons
                _legendTag?.Dispose();
                _classicTag?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
