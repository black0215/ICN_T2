using System;
using System.IO;
using System.Text;
using System.Data;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Collections.Generic;
using Albatross.Tools;
using Albatross.Level5.Text;
using Albatross.Level5.Image;
using Albatross.Yokai_Watch.Logic;
using Albatross.Yokai_Watch.Games;
using Albatross.Yokai_Watch.Common;
using YKW1 = Albatross.Yokai_Watch.Games.YW1.Logic;
using YKW2 = Albatross.Yokai_Watch.Games.YW2.Logic;
using YKW3 = Albatross.Yokai_Watch.Games.YW3.Logic;
using Albatross.Yokai_Watch.Games.YW2; // MapListParser 네임스페이스
using YKWB = Albatross.Yokai_Watch.Games.YWB.Logic;

namespace Albatross.Forms.Encounters
{
    public partial class EncounterWindow : Form
    {
        private IGame GameOpened;

        private List<ICharabase> Charabases;

        private List<ICharaparam> Charaparams;

        private List<IEncountTable> EncountTables;

        private IEncountTable SelectedEncountTable;

        private List<IEncountChara> EncountCharas;

        private T2bþ Charanames;

        private Dictionary<string, string> Mapnames;

        // [ADD] MapListParser 필드 추가 - static 캐시로 변경 (한 번만 로드)
        private static MapListParser _cachedMapParser;

        private bool EncounterDataGridEditInProgress = false;

        private bool IsProcessingCellValueChange = false;

        public EncounterWindow(IGame game)
        {
            GameOpened = game;

            Charabases = new List<ICharabase>();
            Charaparams = new List<ICharaparam>();
            EncountTables = new List<IEncountTable>();
            EncountCharas = new List<IEncountChara>();
            Mapnames = new Dictionary<string, string>();

            InitializeComponent();

            // 검색 기능 이벤트 연결
            searchTextBox.Enter += SearchTextBox_Enter;
            searchTextBox.Leave += SearchTextBox_Leave;
            searchTextBox.TextChanged += SearchTextBox_TextChanged;

            LoadEncounter();
        }

        private string GetSelectedMapKey()
        {
            if (mapListBox.SelectedItem == null) return null;
            return Mapnames.FirstOrDefault(x => x.Value == mapListBox.SelectedItem.ToString()).Key;
        }

        private void SaveEncounterWithGuard()
        {
            string mapKey = GetSelectedMapKey();
            if (string.IsNullOrEmpty(mapKey)) return;

            try
            {
                GameOpened.SaveMapEncounter(mapKey, EncountTables.ToArray(), EncountCharas.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EncounterWindow] Save Error: {ex.Message}");
                MessageBox.Show(
                    "인카운터 데이터 저장 중 오류가 발생했습니다.\n" + ex.Message,
                    "Encounter Save Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private string GetNames(int nameHash)
        {
            return Charanames.Nouns.TryGetValue(nameHash, out var noun) && noun.Strings.Count > 0
                        ? noun.Strings[0].Text
                        : "Undefined";
        }

        private string[] GetNames(ICharabase[] charabases)
        {
            return charabases
                .Select((charabase, index) =>
                {
                    return Charanames.Nouns.TryGetValue(charabase.NameHash, out var noun) && noun.Strings.Count > 0
                        ? noun.Strings[0].Text
                        : "Name " + index;
                })
                .ToArray();
        }

        private string[] GetNames(ICharaparam[] charaparams)
        {
            HashSet<string> encounteredNames = new HashSet<string>();
            Dictionary<string, int> nameOccurrences = new Dictionary<string, int>();

            return charaparams
                .Select((charaparam, index) =>
                {
                    var searchCharabase = Charabases.FirstOrDefault(x => x.BaseHash == charaparam.BaseHash);

                    if (searchCharabase != null && Charanames.Nouns.TryGetValue(searchCharabase.NameHash, out var noun) && noun.Strings.Count > 0)
                    {
                        string name = noun.Strings[0].Text;

                        // 이름 중복 처리
                        if (encounteredNames.Contains(name))
                        {
                            int occurrences = nameOccurrences.TryGetValue(name, out int count) ? count + 1 : 2;
                            nameOccurrences[name] = occurrences;
                            name += " " + occurrences;
                        }

                        encounteredNames.Add(name);

                        return name;
                    }

                    return "Param " + index;
                })
                .ToArray();
        }

        private void LoadEncounter()
        {
            // Reset form
            mapListBox.Items.Clear();
            ((DataGridViewComboBoxColumn)encounterDataGridView.Columns[1]).Items.Clear();

            // Get resources
            Charabases.AddRange(GameOpened.GetCharacterbase(true));
            Charabases.AddRange(GameOpened.GetCharacterbase(false)); // NPCs 추가
            Charaparams.AddRange(GameOpened.GetCharaparam());

            // [ADD] 보스 캐릭터 베이스 로드 (매칭용)
            if (GameOpened is Albatross.Yokai_Watch.Games.YW2.YW2 yw2_typed)
            {
                var bosses = yw2_typed.GetBosses();
                if (bosses != null) Charabases.AddRange(bosses);
            }

            // [ADD] MapListParser 초기화 및 데이터 로드 (로제타 스톤) - 한 번만 로드
            if (_cachedMapParser == null)
            {
                try
                {
                    _cachedMapParser = new MapListParser();
                    _cachedMapParser.ParseAndLoad(MapListParser.GetDefaultMapList());
                    Console.WriteLine($"[EncounterWindow] MapParser loaded: {_cachedMapParser.MapHashMap.Count} entries");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EncounterWindow] MapParser Load Error: {ex.Message}");
                }
            }

            // Get names
            Charanames = new T2bþ(GameOpened.Files["chara_text"].File.Directory.GetFileFromFullPath(GameOpened.Files["chara_text"].Path));
            T2bþ systemtext = new T2bþ(GameOpened.Files["system_text"].File.Directory.GetFileFromFullPath(GameOpened.Files["system_text"].Path));

            // Prepare combobox
            ((DataGridViewComboBoxColumn)encounterDataGridView.Columns[1]).Items.AddRange(GetNames(Charaparams.ToArray()));

            // Get folder who contains encounter data          
            string[] filenames = GameOpened.GetMapWhoContainsEncounter();
            Dictionary<string, int> nameCounter = new Dictionary<string, int>();

            for (int i = 0; i < filenames.Length; i++)
            {
                // [ENHANCED] 맵 이름 표시 로직 개선 (한글 우선)
                string mapName;
                string filename = filenames[i];

                if (filename == "common_enc")
                {
                    mapName = "고정 이벤트 전투 (common_enc)";
                }
                else
                {
                    // 1. System text로 시도 (기존 로직 - 한글판에서 실제 이름을 찾음)
                    uint hash = Crc32.Compute(Encoding.GetEncoding("Shift-JIS").GetBytes(filename));
                    int crc32AsInt = (int)hash;
                    string translatedName = systemtext.Texts.TryGetValue(crc32AsInt, out var text) && text.Strings.Count > 0
                         ? text.Strings[0].Text
                         : null;

                    if (!string.IsNullOrEmpty(translatedName))
                    {
                        mapName = $"{translatedName} ({filename})";
                    }
                    else
                    {
                        // 2. MapParser로 시도 (내장 리스트 - 영어 fallback)
                        string parsedName = _cachedMapParser?.GetMapName((int)hash);
                        if (!string.IsNullOrEmpty(parsedName))
                        {
                            mapName = $"{parsedName} ({filename})";
                        }
                        else
                        {
                            mapName = filename;
                        }
                    }
                }

                if (nameCounter.ContainsKey(mapName))
                {
                    nameCounter[mapName]++;
                    mapName += " " + nameCounter[mapName];
                }
                else
                {
                    nameCounter.Add(mapName, 1);
                }

                Mapnames.Add(filenames[i], mapName);
            }

            mapListBox.Items.AddRange(Mapnames.Values.ToArray());

            if (mapListBox.Items.Count > 0)
            {
                // Auto-select common_enc if available
                var commonEncEntry = Mapnames.FirstOrDefault(x => x.Key.Contains("common_enc"));
                if (commonEncEntry.Value != null)
                {
                    int index = mapListBox.Items.IndexOf(commonEncEntry.Value);
                    mapListBox.SelectedIndex = (index != -1) ? index : 0;
                }
                else
                {
                    mapListBox.SelectedIndex = 0;
                }
            }

            // 콤보박스 너비 확장
            tableFlatComboBox.DropDownWidth = 400;
        }

        // =================================================================================
        // [FIXED] 맵 선택 시 -> 캐릭터 기반으로 테이블 이름 표시
        // =================================================================================
        private void MapListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (mapListBox.SelectedIndex == -1) return;

            encounterDataGridView.Rows.Clear();
            encounterDataGridView.Enabled = false;
            tableFlatComboBox.SelectedIndex = -1;
            tableFlatComboBox.Text = "";

            // Get encounter data
            string selectedMapKey = GetSelectedMapKey();
            if (string.IsNullOrEmpty(selectedMapKey)) return;

            (IEncountTable[], IEncountChara[]) encounterData;
            try
            {
                encounterData = GameOpened.GetMapEncounter(selectedMapKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EncounterWindow] Load Error: {ex.Message}");
                MessageBox.Show(
                    "인카운터 데이터 로드 중 오류가 발생했습니다.\n" + ex.Message,
                    "Encounter Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                mapGroupBox.Enabled = false;
                return;
            }
            EncountTables = encounterData.Item1.ToList();

            if (encounterData.Item2 != null)
            {
                EncountCharas = encounterData.Item2.ToList();
            }

            // [FIX] 캐릭터 정보를 기반으로 테이블 목록 채우기
            tableFlatComboBox.Items.Clear();
            tableFlatComboBox.Items.AddRange(
                EncountTables.Select((table, index) =>
                {
                    // 1. 오프셋을 이용해 이 테이블에 속한 실제 캐릭터들을 가져옴 (가장 안전한 방법)
                    var tableCharas = new List<IEncountChara>();
                    if (table.EncountOffsets != null)
                    {
                        foreach (int offset in table.EncountOffsets)
                        {
                            if (offset != -1 && offset < EncountCharas.Count)
                            {
                                tableCharas.Add(EncountCharas[offset]);
                            }
                        }
                    }

                    if (tableCharas.Count > 0)
                    {
                        // 2. 첫 번째 캐릭터의 이름 찾기
                        var firstChara = tableCharas[0];
                        var charaParam = Charaparams.FirstOrDefault(p => p.ParamHash == firstChara.ParamHash);
                        var charaBase = charaParam != null
                            ? Charabases.FirstOrDefault(b => b.BaseHash == charaParam.BaseHash)
                            : null;

                        string firstName = "???";
                        if (charaBase != null && Charanames.Nouns.TryGetValue(charaBase.NameHash, out var noun) && noun.Strings.Count > 0)
                        {
                            firstName = noun.Strings[0].Text;
                        }

                        // 3. 레벨 범위 및 요괴 종류 수 계산
                        int minLevel = tableCharas.Min(c => c.Level);
                        int maxLevel = tableCharas.Max(c => c.MaxLevel);

                        // 예: Table 1: 지바냥... (Lv.5-10, 3 types)
                        return $"Table {index + 1}: {firstName}... (Lv.{minLevel}-{maxLevel}, {tableCharas.Count} types)";
                    }

                    // 캐릭터가 없는 경우
                    return $"Table {index + 1}: (Empty) [0x{table.EncountConfigHash:X8}]";
                }).ToArray()
            );

            // Load first table
            if (tableFlatComboBox.Items.Count > 0)
            {
                tableFlatComboBox.SelectedIndex = 0;
            }

            mapGroupBox.Enabled = true;
        }

        // [ENHANCED] 9변수 통합 추출 헬퍼 메서드
        private (int level, int maxLevel, int weight, int unk1, int unk2, int unk4, float unk5, int unk7) ExtractAllVariables(IEncountChara chara)
        {
            // YokaiSpotChara (9변수 전용)
            if (chara is YKW2.YokaiSpotChara spot)
            {
                return (spot.Unk3, spot.Level, spot.Unk8, spot.Unk1, spot.Unk2, spot.Unk4, spot.Unk5, spot.Unk7);
            }

            // Standard EncountChara (리플렉션으로 확인)
            if (chara is YKW2.EncountChara enc)
            {
                var type = enc.GetType();
                int unk3 = type.GetProperty("Unk3")?.GetValue(enc) as int? ?? 0;

                float unk4Float = 0f;
                var unk4Prop = type.GetProperty("Unk4");
                if (unk4Prop != null)
                {
                    object unk4Obj = unk4Prop.GetValue(enc);
                    if (unk4Obj is float f) unk4Float = f;
                    else if (unk4Obj is int i) unk4Float = i;
                }

                int unk5 = 0;
                var unk5Prop = type.GetProperty("Unk5");
                if (unk5Prop != null)
                {
                    object unk5Obj = unk5Prop.GetValue(enc);
                    if (unk5Obj is int i) unk5 = i;
                }

                return (
                    enc.Unk1,
                    enc.Level,
                    enc.Unk2,
                    enc.Unk1,
                    enc.Unk2,
                    unk3,
                    unk4Float,
                    unk5
                );
            }

            // Fallback
            return (chara.Level, chara.MaxLevel, chara.Weight, 0, 0, 0, 0f, 0);
        }

        // =================================================================================
        // 테이블 선택 시 -> 그리드에 데이터 표시 (9변수 적용)
        // =================================================================================
        private void TableFlatComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tableFlatComboBox.SelectedIndex == -1) return;

            encounterDataGridView.Rows.Clear();
            SelectedEncountTable = EncountTables[tableFlatComboBox.SelectedIndex];

            // Hash 표시 (MapParser가 있다면 맵 이름도 시도, 없으면 해시만)
            int hash = SelectedEncountTable.EncountConfigHash;
            string mapName = _cachedMapParser?.GetMapName(hash);
            hashTextBox.Text = !string.IsNullOrEmpty(mapName) ? $"0x{hash:X8} - {mapName}" : $"0x{hash:X8}";

            if (GameOpened.Name == "Yo-Kai Watch Blaster")
            {
                // Blaster Logic (Keep as is)
                for (int i = 0; i < SelectedEncountTable.CharaCount; i++)
                {
                    DataGridViewComboBoxColumn comboBox = (DataGridViewComboBoxColumn)encounterDataGridView.Columns[1];
                    int paramHash = SelectedEncountTable.Charas[0 + i * 3];
                    int level = SelectedEncountTable.Charas[1 + i * 3];

                    if (paramHash != 0x00)
                    {
                        ICharaparam charaparam = Charaparams.FirstOrDefault(x => x.ParamHash == paramHash);
                        ICharabase charabase = Charabases.FirstOrDefault(x => x.BaseHash == charaparam.BaseHash);

                        Bitmap yokaiPicture = GetYokaiImage(charabase);
                        encounterDataGridView.Rows.Add(yokaiPicture, comboBox.Items[Charaparams.IndexOf(charaparam)], 0, level, 0, 0, 0, 0, 0f, 0);
                    }
                    else
                    {
                        encounterDataGridView.Rows.Add(null, null, 0, level, 0, 0, 0, 0, 0f, 0);
                    }
                }
            }
            else
            {
                // [ENHANCED] Standard & Yokai Spot Logic
                for (int i = 0; i < SelectedEncountTable.EncountOffsets.Count(); i++)
                {
                    DataGridViewComboBoxColumn comboBox = (DataGridViewComboBoxColumn)encounterDataGridView.Columns[1];
                    int charaIndex = SelectedEncountTable.EncountOffsets[i];

                    if (charaIndex != -1 && charaIndex < EncountCharas.Count)
                    {
                        var currentChara = EncountCharas[charaIndex];
                        ICharaparam charaparam = Charaparams.FirstOrDefault(x => x.ParamHash == currentChara.ParamHash);
                        ICharabase charabase = null;
                        if (charaparam != null) charabase = Charabases.FirstOrDefault(x => x.BaseHash == charaparam.BaseHash);

                        Bitmap yokaiPicture = GetYokaiImage(charabase);
                        int comboIndex = (charaparam != null) ? Charaparams.IndexOf(charaparam) : -1;

                        // 매칭되는 요괴가 없을 경우 처리
                        object cellValue = null;
                        if (comboIndex != -1)
                        {
                            cellValue = comboBox.Items[comboIndex];
                        }
                        else if (currentChara.ParamHash != 0)
                        {
                            // 목록에는 없지만 해시가 있는 경우 (Unknown)
                            string unknownLabel = $"Unknown (0x{currentChara.ParamHash:X8})";
                            if (!comboBox.Items.Contains(unknownLabel))
                            {
                                comboBox.Items.Add(unknownLabel);
                            }
                            cellValue = unknownLabel;
                            Console.WriteLine($"[LoadEncounter] Unknown Hash: 0x{currentChara.ParamHash:X8}");
                        }

                        // 9변수 값 추출
                        var vars = ExtractAllVariables(currentChara);

                        encounterDataGridView.Rows.Add(
                            yokaiPicture,
                            cellValue,
                            vars.maxLevel,   // Col 2
                            vars.level,      // Col 3
                            vars.weight,     // Col 4
                            vars.unk1,       // Col 5
                            vars.unk2,       // Col 6
                            vars.unk4,       // Col 7 (Unk3/4 mapping depends on logic class)
                            vars.unk5,       // Col 8 (Float)
                            vars.unk7        // Col 9
                        );
                    }
                    else
                    {
                        encounterDataGridView.Rows.Add(null, null, 0, 0, 0, 0, 0, 0, 0f, 0);
                    }
                }
            }

            encounterDataGridView.Enabled = true;
        }

        // 이미지 로드 헬퍼
        private Bitmap GetYokaiImage(ICharabase charabase)
        {
            if (charabase == null) return null;
            try
            {
                string fileName = GameSupport.GetFileModelText(charabase.FileNamePrefix, charabase.FileNameNumber, charabase.FileNameVariant);
                string fullPath = GameOpened.Files["face_icon"].Path + "/" + fileName + ".xi";
                byte[] imageData = GameOpened.Files["face_icon"].File.Directory.GetFileFromFullPath(fullPath);
                return IMGC.ToBitmap(imageData);
            }
            catch { }
            return null;
        }

        private void EncounterDataGridView_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (encounterDataGridView.IsCurrentCellDirty)
            {
                if (!EncounterDataGridEditInProgress && !IsProcessingCellValueChange)
                {
                    EncounterDataGridEditInProgress = true;
                    encounterDataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    encounterDataGridView.EndEdit();
                }
            }
        }

        // [ENHANCED] 9변수 값 업데이트 헬퍼
        private void UpdateCharaVariable(IEncountChara chara, int columnIndex, string valueStr)
        {
            // YokaiSpotChara
            if (chara is YKW2.YokaiSpotChara spot)
            {
                switch (columnIndex)
                {
                    case 2: if (int.TryParse(valueStr, out int v2)) spot.Unk3 = v2; break;
                    case 3: if (int.TryParse(valueStr, out int v3)) spot.Level = v3; break;
                    case 4: if (int.TryParse(valueStr, out int v4)) spot.Unk8 = v4; break;
                    case 5: if (int.TryParse(valueStr, out int v5)) spot.Unk1 = v5; break;
                    case 6: if (int.TryParse(valueStr, out int v6)) spot.Unk2 = v6; break;
                    case 7: if (int.TryParse(valueStr, out int v7)) spot.Unk4 = v7; break;
                    case 8: if (float.TryParse(valueStr, out float v8)) spot.Unk5 = v8; break;
                    case 9: if (int.TryParse(valueStr, out int v9)) spot.Unk7 = v9; break;
                }
                return;
            }

            // Standard EncountChara
            if (chara is YKW2.EncountChara enc)
            {
                var type = enc.GetType();
                switch (columnIndex)
                {
                    case 2: if (int.TryParse(valueStr, out int v2)) enc.Unk1 = v2; break;
                    case 3: if (int.TryParse(valueStr, out int v3)) enc.Level = v3; break;
                    case 4: if (int.TryParse(valueStr, out int v4)) enc.Unk2 = v4; break;
                    case 5: if (int.TryParse(valueStr, out int v5)) enc.Unk1 = v5; break;
                    case 6: if (int.TryParse(valueStr, out int v6)) enc.Unk2 = v6; break;
                    case 7:
                        var prop7 = type.GetProperty("Unk3");
                        if (prop7 != null && int.TryParse(valueStr, out int v7)) prop7.SetValue(enc, v7);
                        break;
                    case 8:
                        var prop8 = type.GetProperty("Unk4");
                        if (prop8 != null)
                        {
                            if (prop8.PropertyType == typeof(float))
                            {
                                if (float.TryParse(valueStr, out float v8f)) prop8.SetValue(enc, v8f);
                            }
                            else if (prop8.PropertyType == typeof(int))
                            {
                                if (int.TryParse(valueStr, out int v8i)) prop8.SetValue(enc, v8i);
                            }
                        }
                        break;
                    case 9:
                        var prop9 = type.GetProperty("Unk5");
                        if (prop9 != null)
                        {
                            if (prop9.PropertyType == typeof(int))
                            {
                                if (int.TryParse(valueStr, out int v9i)) prop9.SetValue(enc, v9i);
                            }
                            else if (prop9.PropertyType == typeof(float))
                            {
                                if (float.TryParse(valueStr, out float v9f)) prop9.SetValue(enc, v9f);
                            }
                        }
                        break;
                }
            }
        }

        private void EncounterDataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (EncounterDataGridEditInProgress && !IsProcessingCellValueChange)
            {
                IsProcessingCellValueChange = true;

                int paramHash = 0;
                int level = 0;

                // 1. 콤보박스 변경 시 처리
                if (e.ColumnIndex == 1)
                {
                    DataGridViewComboBoxColumn comboBoxColumn = (DataGridViewComboBoxColumn)encounterDataGridView.Columns[1];
                    DataGridViewComboBoxCell comboBoxCell = (DataGridViewComboBoxCell)encounterDataGridView.Rows[e.RowIndex].Cells[comboBoxColumn.DisplayIndex];
                    object selectedObject = comboBoxCell.FormattedValue;

                    if (selectedObject != null)
                    {
                        int selectedIndex = comboBoxColumn.Items.IndexOf(selectedObject);
                        ICharaparam charaparam = Charaparams[selectedIndex];
                        ICharabase charabase = Charabases.FirstOrDefault(x => x.BaseHash == charaparam.BaseHash);

                        Bitmap yokaiPicture = GetYokaiImage(charabase);

                        paramHash = charaparam.ParamHash;
                        level = 1;

                        encounterDataGridView.Rows[e.RowIndex].Cells[0].Value = yokaiPicture;
                        encounterDataGridView.Rows[e.RowIndex].Cells[3].Value = 1;
                    }
                }

                // 2. 레벨 값 변경 확인
                if (e.ColumnIndex == 3)
                {
                    try { level = Convert.ToInt32(encounterDataGridView.Rows[e.RowIndex].Cells[3].Value); } catch { }
                }

                // 3. 기존 데이터 업데이트 또는 신규 추가
                int existingOffset = SelectedEncountTable.EncountOffsets[e.RowIndex];
                if (existingOffset >= 0 && existingOffset < EncountCharas.Count)
                {
                    // Update Existing
                    int charaIndex = existingOffset;
                    var currentChara = EncountCharas[charaIndex];

                    if (e.ColumnIndex == 1) currentChara.ParamHash = paramHash;
                    else if (e.ColumnIndex == 2)
                    {
                        int maxLevel;
                        if (int.TryParse(encounterDataGridView.Rows[e.RowIndex].Cells[2].Value?.ToString(), out maxLevel))
                        {
                            currentChara.MaxLevel = maxLevel;
                        }
                    }
                    else if (e.ColumnIndex == 3) currentChara.Level = level;
                    else if (e.ColumnIndex >= 3 && e.ColumnIndex <= 9)
                    {
                        string valueStr = encounterDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "0";
                        UpdateCharaVariable(currentChara, e.ColumnIndex, valueStr);
                    }

                    // Refresh List (UI 갱신)
                    TableFlatComboBox_SelectedIndexChanged(sender, e);
                }
                else
                {
                    // [ENHANCED] Add New (Context-Aware)
                    IEncountChara encountChara = null;

                    if (GameOpened.Name == "Yo-Kai Watch 2")
                    {
                        if (EncountCharas.Any(c => c is YKW2.YokaiSpotChara))
                        {
                            encountChara = new YKW2.YokaiSpotChara { ParamHash = paramHash, Level = level, Unk3 = 1, Unk8 = 50, Unk1 = 1143187229, Unk5 = 2.5f };
                        }
                        else
                        {
                            encountChara = GameSupport.GetLogic<YKW2.EncountChara>();
                            if (encountChara != null) { encountChara.ParamHash = paramHash; encountChara.Level = level; encountChara.MaxLevel = 1; encountChara.Weight = 50; }
                        }
                    }
                    else if (GameOpened.Name == "Yo-Kai Watch 1")
                    {
                        encountChara = GameSupport.GetLogic<YKW1.EncountChara>();
                        if (encountChara != null) { encountChara.ParamHash = paramHash; encountChara.Level = level; encountChara.MaxLevel = 1; encountChara.Weight = 50; }
                    }

                    if (encountChara != null)
                    {
                        // 입력된 값 반영
                        int val;
                        if (e.ColumnIndex == 2 && int.TryParse(encounterDataGridView.Rows[e.RowIndex].Cells[2].Value?.ToString(), out val)) encountChara.MaxLevel = val;
                        if (e.ColumnIndex == 3 && int.TryParse(encounterDataGridView.Rows[e.RowIndex].Cells[3].Value?.ToString(), out val)) encountChara.Level = val;
                        if (e.ColumnIndex == 4 && int.TryParse(encounterDataGridView.Rows[e.RowIndex].Cells[4].Value?.ToString(), out val)) encountChara.Weight = val;

                        EncountCharas.Add(encountChara);
                        SelectedEncountTable.EncountOffsets[e.RowIndex] = EncountCharas.Count() - 1;
                    }
                }

                // Save
                SaveEncounterWithGuard();

                EncounterDataGridEditInProgress = false;
                IsProcessingCellValueChange = false;
            }
        }

        private void EncounterDataGridView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hti = encounterDataGridView.HitTest(e.X, e.Y);
                if (hti.RowIndex >= 0)
                {
                    encounterDataGridView.ClearSelection();
                    encounterDataGridView.Rows[hti.RowIndex].Selected = true;
                    Point point = encounterDataGridView.PointToScreen(new Point(e.X, e.Y));
                    charaContextMenuStrip.Show(point);
                }
            }
        }

        private void RemoveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (encounterDataGridView.SelectedRows.Count == 0) return;

            int encountCharaIndex = SelectedEncountTable.EncountOffsets[encounterDataGridView.SelectedRows[0].Index];

            if (encountCharaIndex != 0 && encountCharaIndex < EncountCharas.Count)
            {
                EncountCharas.RemoveAt(encountCharaIndex);

                if (EncountCharas.Count > 0)
                {
                    for (int i = 0; i < EncountTables.Count; i++)
                    {
                        for (int j = 0; j < EncountTables[i].EncountOffsets.Length; j++)
                        {
                            if (EncountTables[i].EncountOffsets[j] == encountCharaIndex)
                            {
                                EncountTables[i].EncountOffsets[j] = -1;
                            }
                            else if (EncountTables[i].EncountOffsets[j] > encountCharaIndex)
                            {
                                EncountTables[i].EncountOffsets[j] -= 1;
                            }
                        }
                    }
                }
            }

            TableFlatComboBox_SelectedIndexChanged(sender, e);
        }

        // [FIXED] 테이블 추가 시 -> 캐릭터 기반 이름으로 UI 갱신
        private void AddToolStripMenuItem_Click(object sender, EventArgs e)
        {
            IEncountTable newTable = null;
            switch (GameOpened.Name)
            {
                case "Yo-Kai Watch 1": newTable = GameSupport.GetLogic<YKW1.EncountTable>(); break;
                case "Yo-Kai Watch 2": newTable = GameSupport.GetLogic<YKW2.EncountTable>(); break;
            }

            if (newTable == null) return;

            EncountTables.Add(newTable);
            SaveEncounterWithGuard();

            // 콤보박스 갱신 (MapListBox_SelectedIndexChanged와 동일한 로직 사용)
            tableFlatComboBox.Items.Clear();
            tableFlatComboBox.Items.AddRange(
                EncountTables.Select((table, index) =>
                {
                    var tableCharas = new List<IEncountChara>();
                    if (table.EncountOffsets != null)
                    {
                        foreach (int offset in table.EncountOffsets)
                        {
                            if (offset != -1 && offset < EncountCharas.Count)
                            {
                                tableCharas.Add(EncountCharas[offset]);
                            }
                        }
                    }

                    if (tableCharas.Count > 0)
                    {
                        var firstChara = tableCharas[0];
                        var charaParam = Charaparams.FirstOrDefault(p => p.ParamHash == firstChara.ParamHash);
                        var charaBase = charaParam != null ? Charabases.FirstOrDefault(b => b.BaseHash == charaParam.BaseHash) : null;

                        string firstName = "???";
                        if (charaBase != null && Charanames.Nouns.TryGetValue(charaBase.NameHash, out var noun) && noun.Strings.Count > 0)
                        {
                            firstName = noun.Strings[0].Text;
                        }

                        int minLevel = tableCharas.Min(c => c.Level);
                        int maxLevel = tableCharas.Max(c => c.MaxLevel);
                        return $"Table {index + 1}: {firstName}... (Lv.{minLevel}-{maxLevel}, {tableCharas.Count} types)";
                    }
                    return $"Table {index + 1}: (Empty) [0x{table.EncountConfigHash:X8}]";
                }).ToArray()
            );

            tableFlatComboBox.SelectedIndex = EncountTables.Count - 1;
        }

        private void AddToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (tableFlatComboBox.SelectedIndex == -1) return;

            IEncountChara newChara = null;

            if (GameOpened.Name == "Yo-Kai Watch 2")
            {
                if (EncountCharas.Any(c => c is YKW2.YokaiSpotChara))
                {
                    newChara = new YKW2.YokaiSpotChara { ParamHash = 0x01140028, Level = 1, Unk3 = 1, Unk8 = 50, Unk1 = 1143187229, Unk5 = 2.5f };
                }
                else
                {
                    newChara = GameSupport.GetLogic<YKW2.EncountChara>();
                    if (newChara != null) { newChara.ParamHash = 0x01140028; newChara.Level = 1; newChara.MaxLevel = 1; newChara.Weight = 50; }
                }
            }
            else if (GameOpened.Name == "Yo-Kai Watch 1")
            {
                newChara = GameSupport.GetLogic<YKW1.EncountChara>();
                if (newChara != null) { newChara.ParamHash = 0x01140028; newChara.Level = 1; newChara.MaxLevel = 1; newChara.Weight = 50; }
            }

            if (newChara == null) return;

            EncountCharas.Add(newChara);
            int newCharaIndex = EncountCharas.Count - 1;

            List<int> offsets = SelectedEncountTable.EncountOffsets.ToList();
            offsets.Add(newCharaIndex);
            SelectedEncountTable.EncountOffsets = offsets.ToArray();

            TableFlatComboBox_SelectedIndexChanged(sender, e);
        }

        private void RemoveToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (tableFlatComboBox.Items.Count > 0)
            {
                EncountTables.RemoveAt(tableFlatComboBox.SelectedIndex);
                SaveEncounterWithGuard();
                MapListBox_SelectedIndexChanged(sender, e);
            }
        }

        private void EncounterWindow_Load(object sender, EventArgs e) { }

        // [ENHANCED] Search Function
        private void SearchTextBox_Enter(object sender, EventArgs e)
        {
            if (searchTextBox.Text == "검색...") { searchTextBox.Text = ""; searchTextBox.ForeColor = Color.White; }
        }

        private void SearchTextBox_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(searchTextBox.Text)) { searchTextBox.Text = "검색..."; searchTextBox.ForeColor = Color.Gray; }
        }

        private void SearchTextBox_TextChanged(object sender, EventArgs e)
        {
            string searchQuery = searchTextBox.Text;
            if (string.IsNullOrWhiteSpace(searchQuery) || searchQuery == "검색...")
            {
                foreach (DataGridViewRow row in encounterDataGridView.Rows) row.Visible = true;
                return;
            }

            foreach (DataGridViewRow row in encounterDataGridView.Rows)
            {
                if (row.Cells[1].Value != null)
                {
                    string charaName = row.Cells[1].Value.ToString();
                    row.Visible = charaName.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                else { row.Visible = false; }
            }
        }
    }
}
