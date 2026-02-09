using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ICN_T2.YokaiWatch.Common;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2.Logic;
using ICN_T2.YokaiWatch.Games.YW2; // Added for YW2 type
using ICN_T2.Logic.Level5.Image; // Added for IMGC
using System.IO; // Added for Path

namespace ICN_T2.UI
{
    /// <summary>
    /// Right-side detail panel for editing CharaBase properties.
    /// Supports both YokaiCharabase and NPCCharabase concrete types.
    /// </summary>
    public class CharabaseDetailPanel : Panel
    {
        private IGame _game;
        private CharaBase? _currentChara;
        private Panel _scrollContent;

        // --- Field Controls ---
        // Identity (공통)
        private HexField _fldBaseHash;
        private NumericField _fldFileNamePrefix;
        private NumericField _fldFileNameNumber;
        private NumericField _fldFileNameVariant;
        private HexField _fldNameHash;

        // 공통 Unk (바이너리 순서: NameHash 다음)
        private HexField _fldUnk1;
        private HexField _fldUnk2;
        private HexField _fldUnk3;
        private HexField _fldUnk4;
        private HexField _fldUnk5;

        // 공통
        private HexField _fldDescriptionHash;
        private NumericField _fldMedalPosX;
        private NumericField _fldMedalPosY;
        private CheckField _fldUnk6;

        // Yokai 전용
        private ComboField _fldRank;
        private CheckField _fldIsRare;
        private CheckField _fldIsLegend;
        private ComboField _fldFavoriteFoodHash;
        private ComboField _fldHatedFoodHash;
        private HexField _fldUnk7;
        private HexField _fldUnk8;
        private HexField _fldUnk9;
        private HexField _fldUnk10;
        private HexField _fldUnk11;
        private ComboField _fldTribe;
        private CheckField _fldIsClassic;
        private HexField _fldUnk12;

        private Dictionary<int, string> _nameMap;
        private Dictionary<int, string> _descMap;
        private Label _lblNameDisplay = null!;
        private Label _lblDescDisplay = null!;

        private PictureBox _picFaceIcon;
        private Dictionary<string, Image?> _yokaiIconCache = new Dictionary<string, Image?>();

        // Medal icon display
        private PictureBox _picMedalIcon;
        private Bitmap? _faceIconSheet; // Cached sprite sheet from face_icon.xi
        private bool _faceIconSheetLoaded;
        private Bitmap? FaceIcon; // Medal sprite sheet reference from CharabaseWindow

        // Section panels (for visibility toggling)
        private Panel _secYokaiOnly;
        private List<Control> _yokaiOnlyControls = new();
        private Panel _secCommonUnk;

        // Colors
        private static readonly Color BG_DARK = Color.FromArgb(24, 24, 28);
        private static readonly Color SECTION_ACCENT = Color.FromArgb(80, 140, 220);
        private static readonly Color SECTION_YOKAI = Color.FromArgb(200, 140, 80);
        private static readonly Color TEXT_DIM = Color.FromArgb(140, 140, 145);

        private Label _lblEmpty;

        public CharabaseDetailPanel(IGame game, Bitmap? faceIcon, Dictionary<int, string> nameMap, Dictionary<int, string> descMap)
        {
            _game = game;
            FaceIcon = faceIcon;
            _nameMap = nameMap;
            _descMap = descMap;
            this.BackColor = BG_DARK;
            this.AutoScroll = true;

            EnableDoubleBuffer(this);

            _scrollContent = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                BackColor = BG_DARK,
                Location = new Point(0, 0),
                Width = 650
            };
            this.Controls.Add(_scrollContent);

            _lblEmpty = new Label
            {
                Text = "캐릭터를 선택하세요",
                ForeColor = TEXT_DIM,
                Font = new Font("SacheonHangGong-Regular", 14F),
                AutoSize = true,
                Location = new Point(200, 200)
            };
            _scrollContent.Controls.Add(_lblEmpty);

            BuildFields();
        }

        private void BuildFields()
        {
            int y = 10;
            int leftX = 16;
            int rightX = 340;
            int fieldW = 290;
            int smallW = 140;

            // ====== Section: Identity (공통) ======
            AddSectionHeader("신원 정보 (Identity)", SECTION_ACCENT, ref y);

            _fldBaseHash = new HexField("Base Hash", leftX, y, fieldW);
            _fldNameHash = new HexField("Name Hash", rightX, y, fieldW);
            _scrollContent.Controls.AddRange(new Control[] { _fldBaseHash, _fldNameHash });
            y += 52;

            _fldDescriptionHash = new HexField("Description Hash", leftX, y, fieldW);
            _scrollContent.Controls.Add(_fldDescriptionHash);

            _lblNameDisplay = new Label
            {
                Location = new Point(rightX, y - 20),
                Size = new Size(fieldW, 20),
                ForeColor = SECTION_ACCENT,
                Font = new Font("Malgun Gothic", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft,
                Text = ""
            };
            _scrollContent.Controls.Add(_lblNameDisplay);

            // [NEW] Face Icon (Name Hash 아래 중앙)
            _picFaceIcon = new PictureBox
            {
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Location = new Point(rightX + (fieldW - 64) / 2, y + 2), // Center under Name Hash
                BackColor = Color.Transparent
            };
            _scrollContent.Controls.Add(_picFaceIcon);

            _lblDescDisplay = new Label
            {
                Location = new Point(leftX, y + 52),
                Size = new Size(fieldW + rightX - leftX, 40),
                ForeColor = TEXT_DIM,
                Font = new Font("Malgun Gothic", 9F),
                Text = "",
                AutoEllipsis = true
            };
            _scrollContent.Controls.Add(_lblDescDisplay);

            y += 80; // Increased spacing for 64px icon

            _fldFileNamePrefix = new NumericField("File Prefix", leftX, y, smallW);
            _fldFileNamePrefix.ValueChanged += (s, e) => UpdateFaceIcon();
            _fldFileNameNumber = new NumericField("File Number", leftX + 155, y, smallW);
            _fldFileNameNumber.ValueChanged += (s, e) => UpdateFaceIcon();
            _fldFileNameVariant = new NumericField("Variant", rightX, y, smallW);
            _fldFileNameVariant.ValueChanged += (s, e) => UpdateFaceIcon();
            _scrollContent.Controls.AddRange(new Control[] { _fldFileNamePrefix, _fldFileNameNumber, _fldFileNameVariant });
            y += 60;

            // ====== Section: Unknown (공통 Unk1~5, Unk6) ======
            AddSectionHeader("미확인 데이터 (Unknown - 공통)", SECTION_ACCENT, ref y);

            _fldUnk1 = new HexField("Unk1", leftX, y, smallW);
            _fldUnk2 = new HexField("Unk2", leftX + 155, y, smallW);
            _fldUnk3 = new HexField("Unk3", rightX, y, smallW);
            _scrollContent.Controls.AddRange(new Control[] { _fldUnk1, _fldUnk2, _fldUnk3 });
            y += 52;

            _fldUnk4 = new HexField("Unk4", leftX, y, smallW);
            _fldUnk5 = new HexField("Unk5", leftX + 155, y, smallW);
            _fldUnk6 = new CheckField("Unk6", rightX, y);
            _scrollContent.Controls.AddRange(new Control[] { _fldUnk4, _fldUnk5, _fldUnk6 });
            y += 56;

            // ====== Section: Medal Position (공통) ======
            AddSectionHeader("메달 위치 (Medal Position)", SECTION_ACCENT, ref y);

            _fldMedalPosX = new NumericField("Medal X", leftX, y, smallW, -999, 999);
            _fldMedalPosY = new NumericField("Medal Y", leftX + 155, y, smallW, -999, 999);
            _scrollContent.Controls.AddRange(new Control[] { _fldMedalPosX, _fldMedalPosY });

            // Add medal icon PictureBox (44x44)
            _picMedalIcon = new PictureBox
            {
                Size = new Size(44, 44),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Location = new Point(rightX + (fieldW - 44) / 2, y), // Center on right side
                BackColor = Color.Transparent,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            _picMedalIcon.Click += PicMedalIcon_Click;

            // Context Menu for Export/Replace
            ContextMenuStrip ctx = new ContextMenuStrip();
            ToolStripMenuItem exportItem = new ToolStripMenuItem("Export FaceIcon Image (.png)");
            exportItem.Click += (s, e) => ExportFaceIcon();
            ToolStripMenuItem replaceItem = new ToolStripMenuItem("Replace FaceIcon Image (Convert PNG to .xi)");
            replaceItem.Click += (s, e) => ReplaceFaceIcon();

            ctx.Items.Add(exportItem);
            ctx.Items.Add(replaceItem);
            _picMedalIcon.ContextMenuStrip = ctx;

            _scrollContent.Controls.Add(_picMedalIcon);

            // Wire event handlers for medal position changes
            _fldMedalPosX.ValueChanged += (s, e) => UpdateMedalIcon();
            _fldMedalPosY.ValueChanged += (s, e) => UpdateMedalIcon();

            y += 60;

            // ====== Section: Yokai Only ======
            AddSectionHeader("요괴 전용 (Yokai Only)", SECTION_YOKAI, ref y);
            int yokaiStartY = y;

            _fldIsRare = new CheckField("레어", leftX, y);
            _fldIsLegend = new CheckField("레전드", leftX + 150, y);
            _fldIsClassic = new CheckField("클래식", leftX + 300, y);
            _scrollContent.Controls.AddRange(new Control[] { _fldIsRare, _fldIsLegend, _fldIsClassic });
            _yokaiOnlyControls.AddRange(new Control[] { _fldIsRare, _fldIsLegend, _fldIsClassic });
            y += 34;

            var rankItems = Ranks.YW.Select(kv => new ComboItem(kv.Key, kv.Value)).ToArray();
            _fldRank = new ComboField("등급 (Rank)", leftX, y, fieldW, rankItems);
            var tribeItems = _game.Tribes.Select(kv => new ComboItem(kv.Key, kv.Value)).ToArray();
            _fldTribe = new ComboField("종족 (Tribe)", rightX, y, fieldW, tribeItems);
            _scrollContent.Controls.AddRange(new Control[] { _fldRank, _fldTribe });
            _yokaiOnlyControls.AddRange(new Control[] { _fldRank, _fldTribe });
            y += 56;

            var foodItems = _game.FoodsType.Select(kv => new ComboItem(kv.Key, kv.Value)).ToArray();
            _fldFavoriteFoodHash = new ComboField("좋아하는 음식", leftX, y, fieldW, foodItems);
            _fldHatedFoodHash = new ComboField("싫어하는 음식", rightX, y, fieldW, foodItems);
            _scrollContent.Controls.AddRange(new Control[] { _fldFavoriteFoodHash, _fldHatedFoodHash });
            _yokaiOnlyControls.AddRange(new Control[] { _fldFavoriteFoodHash, _fldHatedFoodHash });
            y += 56;

            // Yokai Unk7~12
            AddSectionHeader("미확인 데이터 (Unknown - 요괴 전용)", SECTION_YOKAI, ref y);

            _fldUnk7 = new HexField("Unk7", leftX, y, smallW);
            _fldUnk8 = new HexField("Unk8", leftX + 155, y, smallW);
            _fldUnk9 = new HexField("Unk9", rightX, y, smallW);
            _scrollContent.Controls.AddRange(new Control[] { _fldUnk7, _fldUnk8, _fldUnk9 });
            _yokaiOnlyControls.AddRange(new Control[] { _fldUnk7, _fldUnk8, _fldUnk9 });
            y += 52;

            _fldUnk10 = new HexField("Unk10", leftX, y, smallW);
            _fldUnk11 = new HexField("Unk11", leftX + 155, y, smallW);
            _fldUnk12 = new HexField("Unk12", rightX, y, smallW);
            _scrollContent.Controls.AddRange(new Control[] { _fldUnk10, _fldUnk11, _fldUnk12 });
            _yokaiOnlyControls.AddRange(new Control[] { _fldUnk10, _fldUnk11, _fldUnk12 });
            y += 60;

            // Final padding
            _scrollContent.Height = y + 20;

            SetFieldsVisible(false);
        }

        private void AddSectionHeader(string title, Color accentColor, ref int y)
        {
            var panel = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(660, 28),
                BackColor = Color.Transparent
            };
            EnableDoubleBuffer(panel);

            panel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(accentColor, 2))
                    g.DrawLine(pen, 16, 24, 640, 24);
                using (var font = new Font("SacheonHangGong-Regular", 10F, FontStyle.Bold))
                    TextRenderer.DrawText(g, title, font, new Point(16, 4), accentColor);
            };

            _scrollContent.Controls.Add(panel);
            y += 34;
        }

        public void LoadCharacter(CharaBase chara)
        {
            _currentChara = chara;
            _lblEmpty.Visible = false;
            SetFieldsVisible(true);

            // Identity (공통)
            _fldBaseHash.Value = chara.BaseHash;
            _fldNameHash.Value = chara.NameHash;
            _fldDescriptionHash.Value = chara.DescriptionHash;
            _fldFileNamePrefix.Value = chara.FileNamePrefix;
            _fldFileNameNumber.Value = chara.FileNameNumber;
            _fldFileNameVariant.Value = chara.FileNameVariant;

            // Resolved Text Display
            if (_nameMap.TryGetValue(chara.NameHash, out string name))
                _lblNameDisplay.Text = name;
            else
                _lblNameDisplay.Text = "(알 수 없는 이름)";

            if (_descMap.TryGetValue(chara.DescriptionHash, out string desc))
                _lblDescDisplay.Text = desc.Replace("\\n", Environment.NewLine).Replace("\n", Environment.NewLine);
            else
                _lblDescDisplay.Text = "(설명 없음)";

            UpdateFaceIcon();

            // Medal
            _fldMedalPosX.Value = chara.MedalPosX;
            _fldMedalPosY.Value = chara.MedalPosY;
            UpdateMedalIcon();

            if (chara is YokaiCharabase yk)
            {
                // 공통 Unk
                _fldUnk1.Value = yk.Unk1;
                _fldUnk2.Value = yk.Unk2;
                _fldUnk3.Value = yk.Unk3;
                _fldUnk4.Value = yk.Unk4;
                _fldUnk5.Value = yk.Unk5;
                _fldUnk6.Checked = yk.Unk6;

                // Yokai 전용
                _fldRank.SelectedKey = yk.Rank;
                _fldTribe.SelectedKey = yk.Tribe;
                _fldIsRare.Checked = yk.IsRare;
                _fldIsLegend.Checked = yk.IsLegend;
                _fldIsClassic.Checked = yk.IsClassic;
                _fldFavoriteFoodHash.SelectedKey = yk.FavoriteFoodHash;
                _fldHatedFoodHash.SelectedKey = yk.HatedFoodHash;
                _fldUnk7.Value = yk.Unk7;
                _fldUnk8.Value = yk.Unk8;
                _fldUnk9.Value = yk.Unk9;
                _fldUnk10.Value = yk.Unk10;
                _fldUnk11.Value = yk.Unk11;
                _fldUnk12.Value = yk.Unk12;

                SetYokaiFieldsVisible(true);
            }
            else if (chara is NPCCharabase npc)
            {
                _fldUnk1.Value = npc.Unk1;
                _fldUnk2.Value = npc.Unk2;
                _fldUnk3.Value = npc.Unk3;
                _fldUnk4.Value = npc.Unk4;
                _fldUnk5.Value = npc.Unk5;
                _fldUnk6.Checked = npc.Unk6;

                SetYokaiFieldsVisible(false);
            }

            _scrollContent.Invalidate(true);
        }

        public void ClearDetail()
        {
            _currentChara = null;
            _lblEmpty.Visible = true;
            SetFieldsVisible(false);
        }

        public void ApplyChanges()
        {
            if (_currentChara == null) return;

            // Identity (공통 — base class에 직접)
            _currentChara.BaseHash = _fldBaseHash.Value;
            _currentChara.NameHash = _fldNameHash.Value;
            _currentChara.DescriptionHash = _fldDescriptionHash.Value;
            _currentChara.FileNamePrefix = _fldFileNamePrefix.Value;
            _currentChara.FileNameNumber = _fldFileNameNumber.Value; // Added update
            _currentChara.FileNameVariant = _fldFileNameVariant.Value; // Added update

            // Only update icon if values changed (optimized by calling explicitly on change, but here for safety)
            // UpdateFaceIcon(); // Optional, but expensive to reload every time?

            _currentChara.FileNameNumber = _fldFileNameNumber.Value;
            _currentChara.FileNameVariant = _fldFileNameVariant.Value;
            _currentChara.MedalPosX = _fldMedalPosX.Value;
            _currentChara.MedalPosY = _fldMedalPosY.Value;

            if (_currentChara is YokaiCharabase yk)
            {
                yk.Unk1 = _fldUnk1.Value;
                yk.Unk2 = _fldUnk2.Value;
                yk.Unk3 = _fldUnk3.Value;
                yk.Unk4 = _fldUnk4.Value;
                yk.Unk5 = _fldUnk5.Value;
                yk.Unk6 = _fldUnk6.Checked;

                yk.Rank = _fldRank.SelectedKey;
                yk.Tribe = _fldTribe.SelectedKey;
                yk.IsRare = _fldIsRare.Checked;
                yk.IsLegend = _fldIsLegend.Checked;
                yk.IsClassic = _fldIsClassic.Checked;
                yk.FavoriteFoodHash = _fldFavoriteFoodHash.SelectedKey;
                yk.HatedFoodHash = _fldHatedFoodHash.SelectedKey;
                yk.Unk7 = _fldUnk7.Value;
                yk.Unk8 = _fldUnk8.Value;
                yk.Unk9 = _fldUnk9.Value;
                yk.Unk10 = _fldUnk10.Value;
                yk.Unk11 = _fldUnk11.Value;
                yk.Unk12 = _fldUnk12.Value;
            }
            else if (_currentChara is NPCCharabase npc)
            {
                npc.Unk1 = _fldUnk1.Value;
                npc.Unk2 = _fldUnk2.Value;
                npc.Unk3 = _fldUnk3.Value;
                npc.Unk4 = _fldUnk4.Value;
                npc.Unk5 = _fldUnk5.Value;
                npc.Unk6 = _fldUnk6.Checked;
            }
        }

        private void SetFieldsVisible(bool visible)
        {
            foreach (Control c in _scrollContent.Controls)
            {
                if (c != _lblEmpty) c.Visible = visible;
            }
        }

        private void SetYokaiFieldsVisible(bool visible)
        {
            foreach (var c in _yokaiOnlyControls)
                c.Visible = visible;
        }

        private static void EnableDoubleBuffer(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        private void UpdateFaceIcon()
        {
            if (_picFaceIcon == null) return;
            if (_currentChara == null)
            {
                _picFaceIcon.Image = null;
                return;
            }
            int prefix = _fldFileNamePrefix.Value;
            int number = _fldFileNameNumber.Value;
            int variant = _fldFileNameVariant.Value;

            // PrefixLetter에 해당 prefix가 없거나 '?'이면 아이콘 로드 생략
            if (!GameSupport.PrefixLetter.ContainsKey(prefix) || GameSupport.PrefixLetter[prefix] == '?')
            {
                _picFaceIcon.Image = null;
                return;
            }

            try
            {
                string modelName = GameSupport.GetFileModelText(prefix, number, variant);
                _picFaceIcon.Image = LoadYokaiIcon(modelName);
            }
            catch
            {
                _picFaceIcon.Image = null;
            }
        }

        private Image? LoadYokaiIcon(string modelName)
        {
            if (_yokaiIconCache.ContainsKey(modelName))
                return _yokaiIconCache[modelName];

            try
            {
                if (_game.Files.ContainsKey("face_icon"))
                {
                    var file = _game.Files["face_icon"];
                    string fullPath = $"{file.Path}/{modelName}.xi";
                    System.Diagnostics.Debug.WriteLine($"[FaceIcon] Trying: {fullPath}");

                    byte[]? data = null;
                    if (_game is YW2 yw2 && yw2.Game != null && yw2.Game.Directory != null)
                    {
                        // FileExists 체크를 먼저 수행 (레거시와 동일)
                        if (!yw2.Game.Directory.FileExists(fullPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[FaceIcon] NOT FOUND: {fullPath}");
                            _yokaiIconCache[modelName] = null;
                            return null;
                        }

                        var vf = yw2.Game.Directory.GetFileStreamFromFullPath(fullPath);
                        if (vf != null)
                        {
                            data = vf.ByteContent ?? vf.ReadWithoutCaching();
                            System.Diagnostics.Debug.WriteLine($"[FaceIcon] Loaded {modelName}: {data?.Length ?? 0} bytes, first4={(data != null && data.Length >= 4 ? $"0x{data[0]:X2}{data[1]:X2}{data[2]:X2}{data[3]:X2}" : "N/A")}");
                        }
                    }

                    if (data != null && data.Length > 0)
                    {
                        Image? icon = IMGC.ToBitmap(data);
                        System.Diagnostics.Debug.WriteLine($"[FaceIcon] IMGC result for {modelName}: {(icon != null ? $"{icon.Width}x{icon.Height}" : "null")}");
                        _yokaiIconCache[modelName] = icon;
                        return icon;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaceIcon] Failed to load {modelName}: {ex.Message}");
            }

            _yokaiIconCache[modelName] = null;
            return null;
        }

        private Bitmap? GetFaceIconSheet()
        {
            if (_faceIconSheetLoaded) return _faceIconSheet;

            _faceIconSheetLoaded = true;

            try
            {
                if (!(_game is YW2 yw2)) return null;

                // Load face_icon.xi sprite sheet (contains medal icons)
                string fullPath = "data/menu/face_icon.xi";

                System.Diagnostics.Debug.WriteLine($"[MedalIcon] Attempting to load: {fullPath}");

                if (!yw2.Game.Directory.FileExists(fullPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[MedalIcon] NOT FOUND in archive: {fullPath}");

                    // Try PNG fallback from extracted file  
                    string pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "face_icon.00.png");
                    if (File.Exists(pngPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[MedalIcon] Loading PNG fallback: {pngPath}");
                        _faceIconSheet = new Bitmap(pngPath);
                        System.Diagnostics.Debug.WriteLine($"[MedalIcon] PNG loaded: {_faceIconSheet.Width}x{_faceIconSheet.Height}");
                        return _faceIconSheet;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[MedalIcon] PNG fallback not found: {pngPath}");
                    }

                    return null;
                }

                var vf = yw2.Game.Directory.GetFileStreamFromFullPath(fullPath);
                if (vf == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[MedalIcon] GetFileStreamFromFullPath returned null");
                    return null;
                }

                byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                if (data == null || data.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[MedalIcon] No data loaded");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"[MedalIcon] Loaded {data.Length} bytes, first 4 bytes: {(data.Length >= 4 ? $"0x{data[0]:X2}{data[1]:X2}{data[2]:X2}{data[3]:X2}" : "N/A")}");

                _faceIconSheet = IMGC.ToBitmap(data);

                if (_faceIconSheet == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[MedalIcon] IMGC.ToBitmap returned null!");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MedalIcon] Successfully loaded sprite sheet: {_faceIconSheet.Width}x{_faceIconSheet.Height}");
                }

                return _faceIconSheet;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MedalIcon] Exception loading face_icon sheet: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[MedalIcon] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private Bitmap? CropMedal(int posX, int posY, int size = 44)
        {
            System.Diagnostics.Debug.WriteLine($"[MedalIcon] CropMedal called with posX={posX}, posY={posY}, size={size}");

            if (posX < 0 || posY < 0)
            {
                System.Diagnostics.Debug.WriteLine($"[MedalIcon] Invalid position: ({posX}, {posY})");
                return null;
            }

            var sheet = FaceIcon;
            if (sheet == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MedalIcon] FaceIcon is null");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"[MedalIcon] Sprite sheet size: {sheet.Width}x{sheet.Height}");

            try
            {
                int x = posX * size;
                int y = posY * size;

                System.Diagnostics.Debug.WriteLine($"[MedalIcon] Calculated pixel position: ({x}, {y})");

                if (x + size > sheet.Width || y + size > sheet.Height)
                {
                    System.Diagnostics.Debug.WriteLine($"[MedalIcon] Out of bounds! x+size={x + size} > width={sheet.Width} or y+size={y + size} > height={sheet.Height}");
                    return null;
                }

                Rectangle cropRect = new Rectangle(x, y, size, size);
                Bitmap cropped = new Bitmap(size, size);

                using (Graphics g = Graphics.FromImage(cropped))
                {
                    g.DrawImage(sheet, new Rectangle(0, 0, size, size), cropRect, GraphicsUnit.Pixel);
                }

                System.Diagnostics.Debug.WriteLine($"[MedalIcon] Successfully cropped medal icon");
                return cropped;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MedalIcon] Exception in CropMedal: {ex.Message}");
                return null;
            }
        }

        private void PicMedalIcon_Click(object sender, EventArgs e)
        {
            if (FaceIcon == null) return;

            int medalSize = 44;
            if (_game.Name == "Yo-Kai Watch 3")
            {
                medalSize = 32;
            }

            using (var selector = new Albatross.Forms.Characters.MedalSelectorWindow(FaceIcon, medalSize))
            {
                if (selector.ShowDialog() == DialogResult.OK)
                {
                    if (selector.SelectedX > -1 && selector.SelectedY > -1)
                    {
                        if (_fldMedalPosX != null) _fldMedalPosX.Value = selector.SelectedX;
                        if (_fldMedalPosY != null) _fldMedalPosY.Value = selector.SelectedY;

                        // Force update if needed, though ValueChanged should handle it
                    }
                }
            }
        }

        private void ExportFaceIcon()
        {
            if (FaceIcon == null)
            {
                MessageBox.Show("No FaceIcon loaded to export.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "PNG Image|*.png";
                sfd.FileName = "face_icon_export.png";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        FaceIcon.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                        MessageBox.Show("Exported successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error exporting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ReplaceFaceIcon()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Image Files|*.png;*.bmp;*.jpg;*.jpeg";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (Bitmap newIcon = new Bitmap(ofd.FileName))
                        {
                            // Convert to XI
                            byte[]? xiData = IMGC.FromBitmap(newIcon);

                            if (xiData != null)
                            {
                                using (SaveFileDialog sfd = new SaveFileDialog())
                                {
                                    sfd.Filter = "Level5 Image (.xi)|*.xi";
                                    sfd.FileName = "face_icon.xi";
                                    if (sfd.ShowDialog() == DialogResult.OK)
                                    {
                                        File.WriteAllBytes(sfd.FileName, xiData);
                                        MessageBox.Show($"Converted and saved to {sfd.FileName}\n\nTo see changes in-game/tool, place this file in the appropriate directory.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                                        // Update in-memory for preview (clean view)
                                        FaceIcon = (Bitmap)newIcon.Clone();
                                        UpdateMedalIcon(); // Refresh current view
                                    }
                                }
                            }
                            else
                            {
                                MessageBox.Show("Failed to convert image to .xi format.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error replacing icon: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void UpdateMedalIcon()
        {
            System.Diagnostics.Debug.WriteLine($"[MedalIcon] UpdateMedalIcon called");

            if (_currentChara == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MedalIcon] No current character");
                _picMedalIcon.Image?.Dispose();
                _picMedalIcon.Image = null;
                return;
            }

            int posX = _currentChara.MedalPosX;
            int posY = _currentChara.MedalPosY;

            System.Diagnostics.Debug.WriteLine($"[MedalIcon] Character medal position: ({posX}, {posY})");

            if (posX == -1 || posY == -1)
            {
                System.Diagnostics.Debug.WriteLine($"[MedalIcon] Invalid medal position, clearing icon");
                _picMedalIcon.Image?.Dispose();
                _picMedalIcon.Image = null;
                return;
            }

            var medalBitmap = CropMedal(posX, posY);
            if (medalBitmap != null)
            {
                _picMedalIcon.Image?.Dispose();
                _picMedalIcon.Image = medalBitmap;
                System.Diagnostics.Debug.WriteLine($"[MedalIcon] Medal icon updated successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MedalIcon] CropMedal returned null");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var img in _yokaiIconCache.Values) img?.Dispose();
                _yokaiIconCache.Clear();
                _faceIconSheet?.Dispose();
                _picMedalIcon?.Image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ==========================================
    // [Custom Field Controls]
    // ==========================================

    public class ComboItem
    {
        public int Key { get; set; }
        public string Display { get; set; }
        public ComboItem(int key, string display) { Key = key; Display = display; }
        public override string ToString() => $"{Display} (0x{Key:X2})";
    }

    public class HexField : Panel
    {
        private Label _label;
        private TextBox _textBox;

        private static readonly Color BG_FIELD = Color.FromArgb(42, 42, 48);
        private static readonly Color TEXT_LABEL = Color.FromArgb(170, 170, 175);
        private static readonly Color TEXT_PRIMARY = Color.FromArgb(230, 230, 230);

        public int Value
        {
            get
            {
                string text = _textBox.Text.Trim();
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    text = text.Substring(2);
                if (int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out int result))
                    return result;
                return 0;
            }
            set
            {
                _textBox.Text = $"0x{value:X8}";
            }
        }

        public HexField(string labelText, int x, int y, int width)
        {
            this.Location = new Point(x, y);
            this.Size = new Size(width, 48);
            this.BackColor = Color.Transparent;

            _label = new Label
            {
                Text = labelText,
                ForeColor = TEXT_LABEL,
                Font = new Font("Malgun Gothic", 8.5F),
                AutoSize = true,
                Location = new Point(0, 0)
            };

            _textBox = new TextBox
            {
                Location = new Point(0, 18),
                Size = new Size(width, 26),
                BackColor = BG_FIELD,
                ForeColor = TEXT_PRIMARY,
                Font = new Font("Consolas", 10F),
                BorderStyle = BorderStyle.FixedSingle
            };

            this.Controls.Add(_label);
            this.Controls.Add(_textBox);
        }
    }

    public class NumericField : Panel
    {
        private Label _label;
        private NumericUpDown _numeric;

        private static readonly Color BG_FIELD = Color.FromArgb(42, 42, 48);
        private static readonly Color TEXT_LABEL = Color.FromArgb(170, 170, 175);
        private static readonly Color TEXT_PRIMARY = Color.FromArgb(230, 230, 230);

        public int Value
        {
            get => (int)_numeric.Value;
            set => _numeric.Value = Math.Max(_numeric.Minimum, Math.Min(_numeric.Maximum, value));
        }

        public event EventHandler ValueChanged
        {
            add => _numeric.ValueChanged += value;
            remove => _numeric.ValueChanged -= value;
        }

        public NumericField(string labelText, int x, int y, int width, int min = 0, int max = int.MaxValue)
        {
            this.Location = new Point(x, y);
            this.Size = new Size(width, 48);
            this.BackColor = Color.Transparent;

            _label = new Label
            {
                Text = labelText,
                ForeColor = TEXT_LABEL,
                Font = new Font("Malgun Gothic", 8.5F),
                AutoSize = true,
                Location = new Point(0, 0)
            };

            _numeric = new NumericUpDown
            {
                Location = new Point(0, 18),
                Size = new Size(width, 26),
                BackColor = BG_FIELD,
                ForeColor = TEXT_PRIMARY,
                Font = new Font("Consolas", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                Minimum = min,
                Maximum = max
            };

            this.Controls.Add(_label);
            this.Controls.Add(_numeric);
        }
    }

    public class ComboField : Panel
    {
        private Label _label;
        private ComboBox _combo;
        private ComboItem[] _items;

        private static readonly Color BG_FIELD = Color.FromArgb(42, 42, 48);
        private static readonly Color TEXT_LABEL = Color.FromArgb(170, 170, 175);
        private static readonly Color TEXT_PRIMARY = Color.FromArgb(230, 230, 230);

        public int SelectedKey
        {
            get
            {
                if (_combo.SelectedItem is ComboItem ci) return ci.Key;
                return 0;
            }
            set
            {
                for (int i = 0; i < _items.Length; i++)
                {
                    if (_items[i].Key == value)
                    {
                        _combo.SelectedIndex = i;
                        return;
                    }
                }
                if (_combo.Items.Count > 0) _combo.SelectedIndex = 0;
            }
        }

        public ComboField(string labelText, int x, int y, int width, ComboItem[] items)
        {
            _items = items;
            this.Location = new Point(x, y);
            this.Size = new Size(width, 48);
            this.BackColor = Color.Transparent;

            _label = new Label
            {
                Text = labelText,
                ForeColor = TEXT_LABEL,
                Font = new Font("Malgun Gothic", 8.5F),
                AutoSize = true,
                Location = new Point(0, 0)
            };

            _combo = new ComboBox
            {
                Location = new Point(0, 18),
                Size = new Size(width, 26),
                BackColor = BG_FIELD,
                ForeColor = TEXT_PRIMARY,
                Font = new Font("Malgun Gothic", 9.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            _combo.Items.AddRange(items);

            this.Controls.Add(_label);
            this.Controls.Add(_combo);
        }
    }

    public class CheckField : Panel
    {
        private CheckBox _check;

        private static readonly Color TEXT_PRIMARY = Color.FromArgb(230, 230, 230);

        public bool Checked
        {
            get => _check.Checked;
            set => _check.Checked = value;
        }

        public CheckField(string labelText, int x, int y)
        {
            this.Location = new Point(x, y);
            this.Size = new Size(140, 28);
            this.BackColor = Color.Transparent;

            _check = new CheckBox
            {
                Text = labelText,
                ForeColor = TEXT_PRIMARY,
                Font = new Font("Malgun Gothic", 9F),
                AutoSize = true,
                Location = new Point(0, 2)
            };

            this.Controls.Add(_check);
        }
    }
}
