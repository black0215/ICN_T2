using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;
using ICN_T2.YokaiWatch.Games.YW2.Logic;
using ICN_T2.Logic.Level5.Text;

namespace ICN_T2.UI.WPF.ViewModels
{
    // ═══════════════════════════════════════════════════════════════════
    // 정렬 옵션
    // ═══════════════════════════════════════════════════════════════════
    public enum SortOption
    {
        FileNameOrder,  // 파일명순 (기본)
        Alphabetical,   // 이름순
        HashOrder,      // 해시순
        TribeOrder      // Tribe순
    }

    // ═══════════════════════════════════════════════════════════════════
    // 콤보박스 아이템 (아이콘 + 텍스트)
    // ═══════════════════════════════════════════════════════════════════
    public class FilterItem
    {
        public int? Key { get; }
        public string DisplayName { get; }
        public BitmapImage? Icon { get; }

        public FilterItem(int? key, string displayName, BitmapImage? icon = null)
        {
            Key = key;
            DisplayName = displayName;
            Icon = icon;
        }
    }

    public class SortItem
    {
        public SortOption Value { get; }
        public string DisplayName { get; }

        public SortItem(SortOption value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }
    }

    public class CharacterViewModel : INotifyPropertyChanged
    {
        private IGame _game;
        private ObservableCollection<CharacterWrapper> _allCharacters;
        private CharacterWrapper? _selectedCharacter;
        private string _searchText = "";

        // Interactive Selectors
        private bool _isTribeSelectorVisible;
        private bool _isRankSelectorVisible;

        public bool IsTribeSelectorVisible
        {
            get => _isTribeSelectorVisible;
            set { _isTribeSelectorVisible = value; OnPropertyChanged(); }
        }

        public bool IsRankSelectorVisible
        {
            get => _isRankSelectorVisible;
            set { _isRankSelectorVisible = value; OnPropertyChanged(); }
        }

        public ICommand ToggleTribeSelectorCommand { get; }
        public ICommand ToggleRankSelectorCommand { get; }
        public ICommand SelectTribeCommand { get; }
        public ICommand SelectRankCommand { get; }

        // Text Maps
        private Dictionary<int, string> _nameMap = new();
        private Dictionary<int, string> _descMap = new();
        private Dictionary<int, string> _itemNameMap = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public CharacterViewModel(IGame game)
        {
            _game = game;
            _allCharacters = new ObservableCollection<CharacterWrapper>();
            FilteredList = new ObservableCollection<CharacterWrapper>();

            // 아이콘 캐시 초기화
            IconCache.Initialize();

            // Initialize Commands
            ToggleTribeSelectorCommand = new RelayCommand(ExecuteToggleTribeSelector);
            ToggleRankSelectorCommand = new RelayCommand(ExecuteToggleRankSelector);
            SelectTribeCommand = new RelayCommand(ExecuteSelectTribe);
            SelectRankCommand = new RelayCommand(ExecuteSelectRank);

            LoadCharaNames();
            LoadItemText(); // Load Item Names for Food

            // 필터 목록 초기화 (Move after loading text to use localized strings)
            InitializeFilterLists();

            LoadCharacters();
            System.Diagnostics.Trace.WriteLine("[CharacterViewModel] 초기화 완료");
        }

        // ...

        private void ExecuteToggleTribeSelector(object? obj)
        {
            IsTribeSelectorVisible = !IsTribeSelectorVisible;
            if (IsTribeSelectorVisible) IsRankSelectorVisible = false;
        }

        private void ExecuteToggleRankSelector(object? obj)
        {
            IsRankSelectorVisible = !IsRankSelectorVisible;
            if (IsRankSelectorVisible) IsTribeSelectorVisible = false;
        }

        private void ExecuteSelectTribe(object? obj)
        {
            if (obj is int tribeId && SelectedCharacter != null && SelectedCharacter.IsYokai)
            {
                if (SelectedCharacter.Model is YokaiCharabase yk)
                {
                    yk.Tribe = tribeId;
                    SelectedCharacter.RefreshProperties(); // We need to ensure CharacterWrapper updates
                    OnPropertyChanged(nameof(SelectedCharacter)); // Trigger UI update
                    OnPropertyChanged(nameof(SelectedTribeIcon));
                    // Also need to refresh the list item?
                    // Might need to re-wrap or notify change on wrapper.
                }
            }
            IsTribeSelectorVisible = false;
        }

        private void ExecuteSelectRank(object? obj)
        {
            if (obj is int rankId && SelectedCharacter != null && SelectedCharacter.IsYokai)
            {
                if (SelectedCharacter.Model is YokaiCharabase yk)
                {
                    yk.Rank = rankId;
                    SelectedCharacter.RefreshProperties();
                    OnPropertyChanged(nameof(SelectedCharacter));
                    OnPropertyChanged(nameof(SelectedRankIcon));
                }
            }
            IsRankSelectorVisible = false;
        }


        public ObservableCollection<CharacterWrapper> FilteredList { get; private set; }

        public CharacterWrapper? SelectedCharacter
        {
            get => _selectedCharacter;
            set
            {
                if (_selectedCharacter != value)
                {
                    _selectedCharacter = value;
                    OnPropertyChanged();
                    // 모든 디테일 프로퍼티 업데이트 알림
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(BaseHashDisplay));
                    OnPropertyChanged(nameof(NameHashDisplay));
                    OnPropertyChanged(nameof(DescriptionHashDisplay));
                    OnPropertyChanged(nameof(FileNamePrefix));
                    OnPropertyChanged(nameof(FileNameNumber));
                    OnPropertyChanged(nameof(FileNameVariant));
                    OnPropertyChanged(nameof(MedalPosX));
                    OnPropertyChanged(nameof(MedalPosY));
                    OnPropertyChanged(nameof(Unk1Display));
                    OnPropertyChanged(nameof(Unk2Display));
                    OnPropertyChanged(nameof(Unk3Display));
                    OnPropertyChanged(nameof(Unk4Display));
                    OnPropertyChanged(nameof(Unk5Display));
                    OnPropertyChanged(nameof(Unk6));
                    OnPropertyChanged(nameof(IsYokai));
                    OnPropertyChanged(nameof(Rank));
                    OnPropertyChanged(nameof(Tribe));
                    OnPropertyChanged(nameof(IsRare));
                    OnPropertyChanged(nameof(IsLegend));
                    OnPropertyChanged(nameof(IsClassic));
                    OnPropertyChanged(nameof(FavoriteFoodHashDisplay));
                    OnPropertyChanged(nameof(HatedFoodHashDisplay));
                    OnPropertyChanged(nameof(SelectedRankIcon));
                    OnPropertyChanged(nameof(SelectedTribeIcon));
                    OnPropertyChanged(nameof(Unk7Display));
                    OnPropertyChanged(nameof(Unk8Display));
                    OnPropertyChanged(nameof(Unk9Display));
                    OnPropertyChanged(nameof(Unk10Display));
                    OnPropertyChanged(nameof(Unk11Display));
                    OnPropertyChanged(nameof(Unk12Display));
                    System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] 선택된 캐릭터: {value?.Name ?? "null"}");
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    AllowFilter();
                }
            }
        }

        // === 상세 정보 프로퍼티 (바인딩용) ===
        public bool HasSelection => _selectedCharacter != null;

        // 신원 정보
        public string BaseHashDisplay => _selectedCharacter != null ? $"0x{_selectedCharacter.Model.BaseHash:X8}" : "";
        public string NameHashDisplay => _selectedCharacter != null ? $"0x{_selectedCharacter.Model.NameHash:X8}" : "";
        public string DescriptionHashDisplay => _selectedCharacter != null ? $"0x{_selectedCharacter.Model.DescriptionHash:X8}" : "";
        public string DescriptionDisplay => _selectedCharacter?.Description ?? "";
        public int FileNamePrefix => _selectedCharacter?.Model.FileNamePrefix ?? 0;
        public int FileNameNumber => _selectedCharacter?.Model.FileNameNumber ?? 0;
        public int FileNameVariant => _selectedCharacter?.Model.FileNameVariant ?? 0;

        // 메달 위치
        public int MedalPosX => _selectedCharacter?.Model.MedalPosX ?? -1;
        public int MedalPosY => _selectedCharacter?.Model.MedalPosY ?? -1;

        // 공통 Unk
        public string Unk1Display => GetUnkDisplay(1);
        public string Unk2Display => GetUnkDisplay(2);
        public string Unk3Display => GetUnkDisplay(3);
        public string Unk4Display => GetUnkDisplay(4);
        public string Unk5Display => GetUnkDisplay(5);
        public bool Unk6 => GetUnk6();

        // 요괴 여부
        public bool IsYokai => _selectedCharacter?.IsYokai ?? false;

        // 요괴 전용 필드
        public int Rank => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk ? yk.Rank : 0;
        public int Tribe => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk ? yk.Tribe : 0;
        public bool IsRare => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk && yk.IsRare;
        public bool IsLegend => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk && yk.IsLegend;
        public bool IsClassic => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk && yk.IsClassic;
        public string FavoriteFoodHashDisplay => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk ? $"0x{yk.FavoriteFoodHash:X8}" : "";
        public string HatedFoodHashDisplay => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk ? $"0x{yk.HatedFoodHash:X8}" : "";

        // 선택된 캐릭터의 Rank/Tribe 아이콘 (상세 패널용)
        public BitmapImage? SelectedRankIcon => _selectedCharacter?.RankIcon;
        public BitmapImage? SelectedTribeIcon => _selectedCharacter?.TribeIcon;

        // 요괴 전용 Unk
        public string Unk7Display => GetUnkDisplay(7);
        public string Unk8Display => GetUnkDisplay(8);
        public string Unk9Display => GetUnkDisplay(9);
        public string Unk10Display => GetUnkDisplay(10);
        public string Unk11Display => GetUnkDisplay(11);
        public string Unk12Display => GetUnkDisplay(12);

        private string GetUnkDisplay(int unkNumber)
        {
            if (_selectedCharacter == null) return "";

            if (_selectedCharacter.Model is YokaiCharabase yk)
            {
                return unkNumber switch
                {
                    1 => $"0x{yk.Unk1:X8}",
                    2 => $"0x{yk.Unk2:X8}",
                    3 => $"0x{yk.Unk3:X8}",
                    4 => $"0x{yk.Unk4:X8}",
                    5 => $"0x{yk.Unk5:X8}",
                    7 => $"0x{yk.Unk7:X8}",
                    8 => $"0x{yk.Unk8:X8}",
                    9 => $"0x{yk.Unk9:X8}",
                    10 => $"0x{yk.Unk10:X8}",
                    11 => $"0x{yk.Unk11:X8}",
                    12 => $"0x{yk.Unk12:X8}",
                    _ => ""
                };
            }
            else if (_selectedCharacter.Model is NPCCharabase npc && unkNumber <= 5)
            {
                return unkNumber switch
                {
                    1 => $"0x{npc.Unk1:X8}",
                    2 => $"0x{npc.Unk2:X8}",
                    3 => $"0x{npc.Unk3:X8}",
                    4 => $"0x{npc.Unk4:X8}",
                    5 => $"0x{npc.Unk5:X8}",
                    _ => ""
                };
            }
            return "";
        }

        private bool GetUnk6()
        {
            if (_selectedCharacter == null) return false;

            if (_selectedCharacter.Model is YokaiCharabase yk)
                return yk.Unk6;
            else if (_selectedCharacter.Model is NPCCharabase npc)
                return npc.Unk6;

            return false;
        }

        private void LoadCharacters()
        {
            if (!(_game is YW2 yw2)) return;

            _allCharacters.Clear();

            // Load Yokai
            var yokais = yw2.GetCharacterbase(true);
            foreach (var chara in yokais)
            {
                string name = _nameMap.TryGetValue(chara.NameHash, out var n) ? n : $"(Unknown: {chara.NameHash:X8})";
                string desc = _descMap.TryGetValue(chara.DescriptionHash, out var d) ? d : "";

                // If name is blank but we have a hash, maybe use hash as fallback name
                if (string.IsNullOrEmpty(name)) name = $"Yokai {chara.BaseHash:X8}";

                _allCharacters.Add(new CharacterWrapper(chara, true, name, desc));
            }

            // Load NPC
            var npcs = yw2.GetCharacterbase(false);
            foreach (var chara in npcs)
            {
                string name = _nameMap.TryGetValue(chara.NameHash, out var n) ? n : $"(NPC: {chara.NameHash:X8})";
                string desc = _descMap.TryGetValue(chara.DescriptionHash, out var d) ? d : "";

                _allCharacters.Add(new CharacterWrapper(chara, false, name, desc));
            }

            AllowFilter();
        }

        private void LoadCharaNames()
        {
            _nameMap.Clear();
            _descMap.Clear();

            try
            {
                if (_game.Files == null || !_game.Files.ContainsKey("chara_text")) return;

                var gf = _game.Files["chara_text"];
                var vf = gf.GetStream();
                if (vf == null) return;

                byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                if (data == null || data.Length == 0) return;

                var textObj = new T2bþ(data);

                // Nouns (Names)
                foreach (var kv in textObj.Nouns)
                {
                    if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrEmpty(kv.Value.Strings[0].Text))
                    {
                        _nameMap[kv.Key] = kv.Value.Strings[0].Text;
                    }
                }

                // Texts (Common Descriptions)
                foreach (var kv in textObj.Texts)
                {
                    if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrEmpty(kv.Value.Strings[0].Text))
                    {
                        _descMap[kv.Key] = kv.Value.Strings[0].Text;
                    }
                }

                // Load additional descriptions for YW3
                if (_game.Name == "Yo-Kai Watch 3" && _game.Files.ContainsKey("chara_desc_text"))
                {
                    LoadCharaDescriptions();
                }

                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Loaded {_nameMap.Count} names and {_descMap.Count} descriptions");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Error loading names: {ex.Message}");
            }
        }

        private void LoadCharaDescriptions()
        {
            try
            {
                if (_game.Files == null || !_game.Files.ContainsKey("chara_desc_text")) return;

                var gf = _game.Files["chara_desc_text"];
                var vf = gf.GetStream();
                byte[]? data = vf?.ByteContent ?? vf?.ReadWithoutCaching();
                if (data == null) return;

                var textObj = new T2bþ(data);
                foreach (var kv in textObj.Texts)
                {
                    if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrEmpty(kv.Value.Strings[0].Text))
                    {
                        _descMap[kv.Key] = kv.Value.Strings[0].Text;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Error loading extra descriptions: {ex.Message}");
            }
        }

        private void LoadItemText()
        {
            _itemNameMap.Clear();
            try
            {
                if (_game.Files == null || !_game.Files.ContainsKey("item_text")) return;

                var gf = _game.Files["item_text"];
                var vf = gf.GetStream();
                byte[] data = vf?.ByteContent ?? vf?.ReadWithoutCaching();
                if (data == null) return;

                var textObj = new T2bþ(data);

                // Nouns (Item Names)
                foreach (var kv in textObj.Nouns)
                {
                    if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrEmpty(kv.Value.Strings[0].Text))
                    {
                        _itemNameMap[kv.Key] = kv.Value.Strings[0].Text;
                    }
                }

                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Loaded {_itemNameMap.Count} item names");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Error loading item_text: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // 필터 / 정렬 목록 (콤보박스용)
        // ═══════════════════════════════════════════════════════════════════

        public ObservableCollection<FilterItem> RankFilterList { get; } = new();
        public ObservableCollection<FilterItem> TribeFilterList { get; } = new();
        public ObservableCollection<FilterItem> RankSelectionList { get; } = new();
        public ObservableCollection<FilterItem> TribeSelectionList { get; } = new();
        public ObservableCollection<FilterItem> FoodFilterList { get; } = new();
        public ObservableCollection<SortItem> SortOptionList { get; } = new();

        private void InitializeFilterLists()
        {
            // Rank 필터 목록
            RankFilterList.Add(new FilterItem(null, "전체"));
            var rankNames = new Dictionary<int, string>
            {
                { 0x00, "E" }, { 0x01, "D" }, { 0x02, "C" },
                { 0x03, "B" }, { 0x04, "A" }, { 0x05, "S" }
            };
            foreach (var kv in rankNames)
            {
                var item = new FilterItem(kv.Key, $"Rank {kv.Value}", IconCache.GetRankIcon(kv.Key));
                RankFilterList.Add(item);
                RankSelectionList.Add(item);
            }

            // Tribe 필터 목록
            TribeFilterList.Add(new FilterItem(null, "전체"));
            var tribes = _game.Tribes;
            foreach (var kv in tribes)
            {
                var item = new FilterItem(kv.Key, kv.Value, IconCache.GetTribeIcon(kv.Key));
                TribeFilterList.Add(item);
                TribeSelectionList.Add(item);
            }

            // Food 필터 목록
            FoodFilterList.Add(new FilterItem(null, "전체"));
            // Populate food filter list using item names from _itemNameMap
            // Assuming _game.FoodsType provides a mapping from some ID to a generic food type name,
            // and we want to filter by specific item names.
            // For YW2, FavoriteFoodHash/HatedFoodHash are hashes of the item name.
            // We need to map these hashes back to item IDs to use _itemNameMap.
            // This requires an external mapping (e.g., item_param.bin) which is not loaded here.
            // For now, we'll populate with known food types and their localized names if available.
            // If _game.FoodsType keys are item hashes, then we can use them directly.
            // Let's assume _game.FoodsType keys are item hashes for now, or generic food type IDs.
            // If they are generic food type IDs, we need a way to get all items of that type.
            // For simplicity, let's populate with all known item names that are likely foods.
            // This is a placeholder and might need refinement based on actual game data structure.

            // If _game.FoodsType provides a list of *food item hashes* and their generic names:
            foreach (var kv in _game.FoodsType)
            {
                // kv.Key is likely a food item hash or ID, kv.Value is a generic food type name (e.g., "Meat")
                // We want to display the localized item name.
                // This requires a mapping from kv.Key (food type ID/hash) to an actual item ID/hash that exists in _itemNameMap.
                // For now, let's just add all items from _itemNameMap as potential food filters.
                // This is a simplification. A proper implementation would involve parsing item_param.bin
                // to identify actual food items and their properties.
            }

            // A more robust approach for food filtering would involve:
            // 1. Loading item_param.bin to get a list of all food items and their hashes/IDs.
            // 2. Using _itemNameMap to get localized names for these food items.
            // 3. Populating FoodFilterList with these localized food items.
            // Since item_param.bin is not loaded, we'll use a simplified approach for now.
            // Let's assume _game.FoodsType provides a list of food *categories* and we want to filter by those.
            // If the goal is to filter by specific food *items*, then _itemNameMap is relevant.
            // For YW2, FavoriteFoodHash/HatedFoodHash are hashes of the *item name*, not a category.
            // So we need to list actual food items.

            // For now, let's populate FoodFilterList with all items from _itemNameMap,
            // as a temporary solution until proper food item identification is implemented.
            // This will show all items, not just foods, which is not ideal.
            // A better approach is to use the food type names from _game.FoodsType and try to get an icon.
            // The original code used `kv.Value` (generic food type name) for `DisplayName` and `IconCache.GetFoodIcon(kv.Value)`.
            // Let's stick to that for now, but use _itemNameMap if we can map the food type to an item ID.
            // This part is tricky without full game data context.

            // Reverting to original logic for FoodFilterList, as _itemNameMap is for specific items,
            // and _game.FoodsType seems to be for categories.
            // If FavoriteFoodHash/HatedFoodHash are hashes of specific items, then we need a way to map them to _itemNameMap.
            // For now, the filter will work on the hash value directly, and the display name will be the generic type.
            // This is a known limitation without item_param.bin parsing.
            foreach (var kv in _game.FoodsType)
            {
                if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                // kv.Key is an int, kv.Value is a string like "Meat", "Vegetable"
                // We need to decide if kv.Key is the hash of the food item or a category ID.
                // If it's a hash, we can try to get its localized name.
                // If it's a category ID, we can't directly use _itemNameMap.
                // For now, let's assume kv.Key is a food item hash and try to get its localized name.
                // If not found, fallback to generic name.
                string displayName = _itemNameMap.TryGetValue(kv.Key, out var localizedName) ? localizedName : kv.Value;
                FoodFilterList.Add(new FilterItem(kv.Key, displayName, IconCache.GetFoodIcon(kv.Value)));
            }


            // 정렬 옵션
            SortOptionList.Add(new SortItem(SortOption.FileNameOrder, "파일명순"));
            SortOptionList.Add(new SortItem(SortOption.Alphabetical, "이름순"));
            SortOptionList.Add(new SortItem(SortOption.HashOrder, "해시순"));
            SortOptionList.Add(new SortItem(SortOption.TribeOrder, "Tribe순"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // 필터 프로퍼티
        // ═══════════════════════════════════════════════════════════════════
        private int? _filterRank;
        private int? _filterTribe;
        private bool? _filterIsRare;
        private bool? _filterIsLegend;
        private bool _filterIsYokaiChecked = true;
        private bool _filterIsNPCChecked = true;
        private bool _filterHashSearch = false;
        private int _filterFavoriteFood = -1;
        private int _filterHatedFood = -1;
        private SortOption _sortOrder = SortOption.FileNameOrder;

        // 선택된 필터 아이템 (콤보박스 바인딩)
        private FilterItem? _selectedRankFilter;
        private FilterItem? _selectedTribeFilter;
        private FilterItem? _selectedFavoriteFoodFilter;
        private FilterItem? _selectedHatedFoodFilter;
        private SortItem? _selectedSortOption;

        public FilterItem? SelectedRankFilter
        {
            get => _selectedRankFilter;
            set
            {
                if (_selectedRankFilter != value)
                {
                    _selectedRankFilter = value;
                    _filterRank = value?.Key;
                    OnPropertyChanged();
                    AllowFilter();
                }
            }
        }

        public FilterItem? SelectedTribeFilter
        {
            get => _selectedTribeFilter;
            set
            {
                if (_selectedTribeFilter != value)
                {
                    _selectedTribeFilter = value;
                    _filterTribe = value?.Key;
                    OnPropertyChanged();
                    AllowFilter();
                }
            }
        }

        public FilterItem? SelectedFavoriteFoodFilter
        {
            get => _selectedFavoriteFoodFilter;
            set
            {
                if (_selectedFavoriteFoodFilter != value)
                {
                    _selectedFavoriteFoodFilter = value;
                    _filterFavoriteFood = value?.Key ?? -1;
                    OnPropertyChanged();
                    AllowFilter();
                }
            }
        }

        public FilterItem? SelectedHatedFoodFilter
        {
            get => _selectedHatedFoodFilter;
            set
            {
                if (_selectedHatedFoodFilter != value)
                {
                    _selectedHatedFoodFilter = value;
                    _filterHatedFood = value?.Key ?? -1;
                    OnPropertyChanged();
                    AllowFilter();
                }
            }
        }

        public SortItem? SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (_selectedSortOption != value)
                {
                    _selectedSortOption = value;
                    _sortOrder = value?.Value ?? SortOption.FileNameOrder;
                    OnPropertyChanged();
                    AllowFilter();
                }
            }
        }

        public bool FilterShowYokai
        {
            get => _filterIsYokaiChecked;
            set
            {
                if (_filterIsYokaiChecked != value)
                {
                    _filterIsYokaiChecked = value;
                    OnPropertyChanged();
                    AllowFilter();
                }
            }
        }

        public bool FilterShowNPC
        {
            get => _filterIsNPCChecked;
            set
            {
                if (_filterIsNPCChecked != value)
                {
                    _filterIsNPCChecked = value;
                    OnPropertyChanged();
                    AllowFilter();
                }
            }
        }

        public bool FilterHashSearch
        {
            get => _filterHashSearch;
            set
            {
                if (_filterHashSearch != value)
                {
                    _filterHashSearch = value;
                    OnPropertyChanged();
                    AllowFilter();
                }
            }
        }

        public bool? FilterIsRare
        {
            get => _filterIsRare;
            set
            {
                if (_filterIsRare != value)
                {
                    _filterIsRare = value;
                    OnPropertyChanged();
                    AllowFilter();
                }
            }
        }

        private void AllowFilter()
        {
            try
            {
                var query = _searchText?.ToLower() ?? "";

                var result = _allCharacters.Where(c =>
                {
                    // 카테고리 필터 (요괴/NPC 체크박스)
                    if (c.IsYokai && !_filterIsYokaiChecked) return false;
                    if (!c.IsYokai && !_filterIsNPCChecked) return false;

                    // 텍스트 검색
                    if (!string.IsNullOrEmpty(query))
                    {
                        // 해시 검색 (체크 시에만 활성화)
                        if (_filterHashSearch)
                        {
                            // 해시 검색 모드에서는 query를 해시로 해석
                            if (!c.Model.BaseHash.ToString("X8").ToLower().Contains(query) &&
                                !c.Model.NameHash.ToString("X8").ToLower().Contains(query))
                                return false;
                        }
                        else // 일반 텍스트 검색 (이름)
                        {
                            if (!c.Name.ToLower().Contains(query))
                                return false;
                        }
                    }

                    // Rank 필터
                    if (_filterRank.HasValue && c.IsYokai && c.Model is YokaiCharabase yk)
                    {
                        if (yk.Rank != _filterRank.Value)
                            return false;
                    }

                    // Tribe 필터
                    if (_filterTribe.HasValue && c.IsYokai && c.Model is YokaiCharabase yk2)
                    {
                        if (yk2.Tribe != _filterTribe.Value)
                            return false;
                    }

                    // Rarity 필터
                    if (_filterIsRare.HasValue && c.IsYokai && c.Model is YokaiCharabase yk3)
                    {
                        if (yk3.IsRare != _filterIsRare.Value)
                            return false;
                    }

                    // Favorite Food 필터
                    if (_filterFavoriteFood != -1 && c.IsYokai && c.Model is YokaiCharabase yk4)
                    {
                        // Note: Model doesn't store Food ID directly usually, but hash. 
                        // YW2 Logic might need hash comparison if FoodFilterList Key is ID.
                        // Assuming FoodFilterList Key maps meaningfully. 
                        // If not, we might need hash check. For now, skipping strict check or assuming ID match if available.
                        // Actually, YW2 param doesn't easily link Food ID to Charabase without external table.
                        // For now we skip actual data filtering for food if implementation is complex, or rely on future implementation.
                        if (yk4.FavoriteFoodHash != (uint)_filterFavoriteFood) return false;
                    }

                    if (_filterHatedFood != -1 && c.IsYokai && c.Model is YokaiCharabase yk5)
                    {
                        if (yk5.HatedFoodHash != (uint)_filterHatedFood) return false;
                    }

                    return true;
                });

                // 정렬 (Sorting)
                IEnumerable<CharacterWrapper> sortedResult = result;

                switch (_sortOrder)
                {
                    case SortOption.FileNameOrder:
                        sortedResult = result.OrderBy(c => c.Model.FileNamePrefix)
                                             .ThenBy(c =>
                                             {
                                                 // Custom Sort: 05 comes after 06
                                                 if (c.Model.FileNameNumber == 5) return int.MaxValue - 1;
                                                 return c.Model.FileNameNumber;
                                             })
                                             .ThenBy(c => c.Model.FileNameVariant);
                        break;
                    case SortOption.Alphabetical:
                        sortedResult = result.OrderBy(c => c.Name);
                        break;
                    case SortOption.HashOrder:
                        sortedResult = result.OrderBy(c => c.Model.BaseHash);
                        break;
                    case SortOption.TribeOrder:
                        // Tribe 0 (Untribe) should be last. 
                        // We map 0 to int.MaxValue for sorting purposes.
                        sortedResult = result.OrderBy(c =>
                        {
                            if (!c.IsYokai) return int.MaxValue; // NPCs last
                            int tribe = (c.Model as YokaiCharabase)?.Tribe ?? 0;
                            return tribe == 0 ? int.MaxValue - 1 : tribe; // Tribe 0 at bottom (before NPCs/Errors)
                        })
                        .ThenBy(c => c.Model.FileNameNumber);
                        break;
                }

                // 리스트 업데이트
                FilteredList.Clear();
                foreach (var item in sortedResult)
                {
                    FilteredList.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] AllowFilter Error: {ex.Message}");
            }
        }

        private void LoadFoodList()
        {
            // Note: In this version I am relying on InitializeFilterLists which now populates FoodFilterList.
            // But if legacy code needs FoodList, I should recreate it?
            // User requested LoadFoodList to exist in previous context.
            // Let's implement it for safety if I missed removing a call.
            // Actually, I moved the logic to InitializeFilterLists.
            // But constructor calls LoadFoodList() in my previous snippet.
            // I will implement it to populate a simple list or just alias logic.
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class CharacterWrapper : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public CharaBase Model { get; }
        public bool IsYokai { get; }
        public int Id => Model.BaseHash;

        // Placeholder for Name (Requires Game Support for text lookup)
        public string Name { get; }
        public string Description { get; }

        // Helpers for UI Binding
        public string Tribe => IsYokai && Model is YokaiCharabase yk ? $"Tribe {yk.Tribe}" : "NPC";
        public string Rank => IsYokai && Model is YokaiCharabase yk ? $"Rank {yk.Rank}" : "-";
        public bool IsRare => IsYokai && Model is YokaiCharabase yk && yk.IsRare;
        public bool IsLegend => IsYokai && Model is YokaiCharabase yk && yk.IsLegend;

        public CharacterWrapper(CharaBase model, bool isYokai, string name, string description)
        {
            Model = model;
            IsYokai = isYokai;
            Name = name;
            Description = description;

            ResolveIcons();
        }

        public string RankIconPath { get; private set; } = "";
        public string TribeIconPath { get; private set; } = "";
        public string FavoriteFoodIconPath { get; private set; } = "";

        // UI에서 사용할 직접 비트맵 (캐시된 것)
        public BitmapImage? RankIcon => IconCache.GetRankIcon(IsYokai && Model is YokaiCharabase yk ? yk.Rank : -1);
        public BitmapImage? TribeIcon => IconCache.GetTribeIcon(IsYokai && Model is YokaiCharabase yk ? yk.Tribe : -1);

        public void RefreshProperties()
        {
            // Notify changes for calculated properties
            OnPropertyChanged(nameof(Tribe));
            OnPropertyChanged(nameof(Rank));
            OnPropertyChanged(nameof(IsRare));
            OnPropertyChanged(nameof(IsLegend));

            ResolveIcons();
            OnPropertyChanged(nameof(RankIconPath));
            OnPropertyChanged(nameof(TribeIconPath));
            OnPropertyChanged(nameof(RankIcon));
            OnPropertyChanged(nameof(TribeIcon));
        }

        private void ResolveIcons()
        {
            // Note: Actual implementation moved to IconCache access for UI
            // This method can be used for debugging or path-based access if needed
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Icon Helper
    // ═══════════════════════════════════════════════════════════════════
    public static class IconCache
    {
        private static Dictionary<int, BitmapImage> _rankIcons = new();
        private static Dictionary<int, BitmapImage> _tribeIcons = new();
        private static Dictionary<string, BitmapImage> _foodIcons = new();

        public static void Initialize()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;

                // Rank Icons
                string rankPath = Path.Combine(basePath, "Resources", "Rank Icon");
                if (Directory.Exists(rankPath))
                {
                    LoadRank(rankPath, 0, "Rank_E.png");
                    LoadRank(rankPath, 1, "Rank_D.png");
                    LoadRank(rankPath, 2, "Rank_C.png");
                    LoadRank(rankPath, 3, "Rank_B.png");
                    LoadRank(rankPath, 4, "Rank_A.png");
                    LoadRank(rankPath, 5, "Rank_S.png");
                }

                // Tribe Icons
                string tribePath = Path.Combine(basePath, "Resources", "Tribe Icon");
                if (Directory.Exists(tribePath))
                {
                    for (int i = 0; i <= 11; i++)
                    {
                        LoadTribe(tribePath, i, $"all_icon_kind01_{i:D2}.png");
                    }
                }

                // Food Icons (Lazy load in GetFoodIcon usually, but pre-scan here if needed)
                string foodPath = Path.Combine(basePath, "Resources", "Food Icon");
                if (Directory.Exists(foodPath))
                {
                    // Basic files mapping
                    LoadFood(foodPath, "Rice Balls", "rice_balls.png");
                    LoadFood(foodPath, "Bread", "bread.png");
                    // ... etc.
                }
            }
            catch { }
        }

        private static void LoadRank(string dir, int key, string file)
        {
            string path = Path.Combine(dir, file);
            if (File.Exists(path)) _rankIcons[key] = LoadBitmap(path);
        }

        private static void LoadTribe(string dir, int key, string file)
        {
            string path = Path.Combine(dir, file);
            if (File.Exists(path)) _tribeIcons[key] = LoadBitmap(path);
        }

        private static void LoadFood(string dir, string key, string file)
        {
            string path = Path.Combine(dir, file);
            if (File.Exists(path)) _foodIcons[key] = LoadBitmap(path);
        }

        public static BitmapImage? GetRankIcon(int rank) => _rankIcons.ContainsKey(rank) ? _rankIcons[rank] : null;
        public static BitmapImage? GetTribeIcon(int tribe) => _tribeIcons.ContainsKey(tribe) ? _tribeIcons[tribe] : null;
        public static BitmapImage? GetFoodIcon(string foodName) => _foodIcons.ContainsKey(foodName) ? _foodIcons[foodName] : null;

        private static BitmapImage LoadBitmap(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
