using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;
using ICN_T2.YokaiWatch.Games.YW2.Logic;
using ICN_T2.Logic.Level5.Image;
using ICN_T2.Logic.Level5.Text;
using ICN_T2.Logic.Level5.Binary;

namespace ICN_T2.UI.WPF.ViewModels
{
    public enum SortOption
    {
        FileNameOrder,
        Alphabetical,
        HashOrder,
        TribeOrder
    }


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
        public ObservableCollection<FilterItem> FoodOptions { get; private set; } = new();

        // Missing Filter Backing Fields
        private int? _filterRank;
        private int? _filterTribe;
        private bool? _filterIsRare;
        private bool? _filterIsLegend;
        private bool? _filterIsClassic;

        // Filter Lists (Properties)
        public ObservableCollection<FilterItem> RankFilterList { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> TribeFilterList { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> FoodFilterList { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<SortItem> SortOptionList { get; } = new ObservableCollection<SortItem>();
        public ObservableCollection<FilterItem> RankSelectionList { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> TribeSelectionList { get; } = new ObservableCollection<FilterItem>();



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
        public ICommand RevertEditsCommand { get; }
        public ICommand ResetToDefaultCommand { get; }



        // Text Maps
        private Dictionary<int, string> _nameMap = new();
        private Dictionary<int, string> _descMap = new();
        private Dictionary<int, string> _itemNameMap = new();
        private Dictionary<int, string> _charaTextDescMap = new();
        private Dictionary<int, string> _charaDescTextMap = new();
        private static readonly Dictionary<string, BitmapImage> SharedCharacterIconCache = new();
        private static readonly object SharedCharacterIconCacheLock = new();
        private CancellationTokenSource? _iconLoadCts;
        private System.Drawing.Bitmap? _medalFaceIconSheet;
        private bool _medalFaceIconSheetLoaded;
        private string _medalFaceIconSheetSource = "unresolved";
        private bool _medalSheetSummaryLogged;
        private BitmapImage? _selectedMedalIcon;
        private string? _lastMedalCropLogKey;
        private const string TemporaryDecodedMedalSheetPath = @"C:\Users\home\Desktop\RomFS\face_icon_export.png";

        public event PropertyChangedEventHandler? PropertyChanged;

        public CharacterViewModel(IGame game)
        {
            _game = game;
            _allCharacters = new ObservableCollection<CharacterWrapper>();
            FilteredList = new ObservableCollection<CharacterWrapper>();

            // ??ш끽維쀩??癲?????縕?猿녿뎨??            IconCache.Initialize();

            // Initialize Commands
            ToggleTribeSelectorCommand = new RelayCommand(ExecuteToggleTribeSelector);
            ToggleRankSelectorCommand = new RelayCommand(ExecuteToggleRankSelector);
            SelectTribeCommand = new RelayCommand(ExecuteSelectTribe);
            SelectRankCommand = new RelayCommand(ExecuteSelectRank);
            RevertEditsCommand = new RelayCommand(ExecuteRevertEdits, _ => HasSelection);
            ResetToDefaultCommand = new RelayCommand(ExecuteResetToDefault, _ => HasSelection);

            LoadCharaNames();
            LoadItemText(); // Load Item Names for Food

            // ??ш낄援??癲ル슢?꾤땟戮⑤뭄??縕?猿녿뎨??(Move after loading text to use localized strings)
            InitializeFilterLists();
            InitializeSelectionLists();

            LoadCharacters();
            System.Diagnostics.Trace.WriteLine("[CharacterViewModel] Initialized");
        }

        // ...

        private void ExecuteToggleTribeSelector(object? obj)
        {
            if (!IsYokai) return;
            IsTribeSelectorVisible = !IsTribeSelectorVisible;
            if (IsTribeSelectorVisible) IsRankSelectorVisible = false;
        }

        private void ExecuteToggleRankSelector(object? obj)
        {
            if (!IsYokai) return;
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
                    if (_selectedCharacter != null)
                    {
                        _selectedCharacter.PropertyChanged -= SelectedCharacter_PropertyChanged;
                    }

                    _selectedCharacter = value;

                    if (_selectedCharacter != null)
                    {
                        _selectedCharacter.PropertyChanged += SelectedCharacter_PropertyChanged;
                    }

                    UpdateSelectedMedalIcon();
                    OnPropertyChanged();
                    // 嶺뚮ㅄ維獄???됀????熬곣뫁夷???몃폃 ???낆몥??袁⑤콦 ???逾?
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(BaseHashDisplay));
                    OnPropertyChanged(nameof(NameHashDisplay));
                    OnPropertyChanged(nameof(DescriptionHashDisplay));
                    OnPropertyChanged(nameof(BaseHashText));
                    OnPropertyChanged(nameof(NameHashText));
                    OnPropertyChanged(nameof(DescriptionHashText));
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
                    OnPropertyChanged(nameof(SelectedMedalIcon));
                    OnPropertyChanged(nameof(Unk7Display));
                    OnPropertyChanged(nameof(Unk8Display));
                    OnPropertyChanged(nameof(Unk9Display));
                    OnPropertyChanged(nameof(Unk10Display));
                    OnPropertyChanged(nameof(Unk11Display));
                    OnPropertyChanged(nameof(Unk12Display));
                    OnPropertyChanged(nameof(MedalKeyInfo));
                    OnPropertyChanged(nameof(SpecialUnk6));
                    OnPropertyChanged(nameof(SpecialIsRare));
                    OnPropertyChanged(nameof(SpecialIsLegend));
                    OnPropertyChanged(nameof(SpecialIsClassic));
                    OnPropertyChanged(nameof(ShowYokaiSpecialFlags));
                    OnPropertyChanged(nameof(ShowNpcSpecialOnly));

                    // Force command update
                    CommandManager.InvalidateRequerySuggested();

                    System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Selected character changed: {value?.Name ?? "null"}");
                }
            }
        }

        private void ExecuteRevertEdits(object? obj)
        {
            if (_selectedCharacter == null) return;
            var result = System.Windows.MessageBox.Show(
                "현재 변경 사항을 취소하고, 앱 실행(로드) 시점의 값으로 되돌리시겠습니까?",
                "되돌리기 확인",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            _selectedCharacter.Revert();

            // UI Refresh triggers
            OnPropertyChanged(nameof(BaseHashDisplay));
            OnPropertyChanged(nameof(NameHashDisplay));
            OnPropertyChanged(nameof(DescriptionHashDisplay));
            OnPropertyChanged(nameof(BaseHashText));
            OnPropertyChanged(nameof(NameHashText));
            OnPropertyChanged(nameof(DescriptionHashText));
            OnPropertyChanged(nameof(DescriptionDisplay));
            OnPropertyChanged(nameof(SelectedCharacter));
            OnPropertyChanged(nameof(MedalKeyInfo));

            // AllowFilter to refresh the list view if needed (e.g. name changed back)
            AllowFilter();
        }

        private void ExecuteResetToDefault(object? obj)
        {
            // Reset ALL characters to their original state
            var result = System.Windows.MessageBox.Show(
                "모든 캐릭터를 앱 실행(로드) 시점의 값으로 초기화하시겠습니까?\n이 작업은 되돌릴 수 없습니다!",
                "전체 초기화 확인",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            // Revert all characters
            foreach (var character in _allCharacters)
            {
                character.Revert();
            }

            // Refresh the current selection if any
            if (_selectedCharacter != null)
            {
                OnPropertyChanged(nameof(BaseHashDisplay));
                OnPropertyChanged(nameof(NameHashDisplay));
                OnPropertyChanged(nameof(DescriptionHashDisplay));
                OnPropertyChanged(nameof(BaseHashText));
                OnPropertyChanged(nameof(NameHashText));
                OnPropertyChanged(nameof(DescriptionHashText));
                OnPropertyChanged(nameof(DescriptionDisplay));
                OnPropertyChanged(nameof(SelectedCharacter));
                OnPropertyChanged(nameof(MedalKeyInfo));
            }

            // Refresh the filtered list
            AllowFilter();

            System.Windows.MessageBox.Show(
                "모든 캐릭터가 초기화되었습니다.",
                "초기화 완료",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
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

        // === ???ㅳ늾???嶺뚮㉡?€쾮???ш끽維곩ㅇ???紐껎룂 (?袁⑸즴????獄??? ===
        public bool HasSelection => _selectedCharacter != null;

        // ???モ???嶺뚮㉡?€쾮?
        public string BaseHashDisplay => _selectedCharacter != null ? $"0x{_selectedCharacter.Model.BaseHash:X8}" : "";
        public string NameHashDisplay => _selectedCharacter != null ? $"0x{_selectedCharacter.Model.NameHash:X8}" : "";
        public string DescriptionHashDisplay => _selectedCharacter != null ? $"0x{_selectedCharacter.Model.DescriptionHash:X8}" : "";
        public string BaseHashText
        {
            get => _selectedCharacter != null ? $"0x{_selectedCharacter.Model.BaseHash:X8}" : string.Empty;
            set => TryUpdateSelectedHash(HashField.Base, value);
        }

        public string NameHashText
        {
            get => _selectedCharacter != null ? $"0x{_selectedCharacter.Model.NameHash:X8}" : string.Empty;
            set => TryUpdateSelectedHash(HashField.Name, value);
        }

        public string DescriptionHashText
        {
            get => _selectedCharacter != null ? $"0x{_selectedCharacter.Model.DescriptionHash:X8}" : string.Empty;
            set => TryUpdateSelectedHash(HashField.Description, value);
        }
        public string DescriptionDisplay => _selectedCharacter?.Description ?? "";
        public int FileNamePrefix => _selectedCharacter?.Model.FileNamePrefix ?? 0;
        public int FileNameNumber => _selectedCharacter?.Model.FileNameNumber ?? 0;
        public int FileNameVariant => _selectedCharacter?.Model.FileNameVariant ?? 0;

        // 癲ル슢???????ш끽維??
        public int MedalPosX => _selectedCharacter?.Model.MedalPosX ?? -1;
        public int MedalPosY => _selectedCharacter?.Model.MedalPosY ?? -1;

        // ???살씁??Unk
        public string Unk1Display => GetUnkDisplay(1);
        public string Unk2Display => GetUnkDisplay(2);
        public string Unk3Display => GetUnkDisplay(3);
        public string Unk4Display => GetUnkDisplay(4);
        public string Unk5Display => GetUnkDisplay(5);
        public bool Unk6 => GetUnk6();

        // ??釉먮윥?????
        public bool IsYokai => _selectedCharacter?.IsYokai ?? false;

        // ??釉먮윥????ш끽維????ш끽維??
        public int Rank => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk ? yk.Rank : 0;
        public int Tribe => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk ? yk.Tribe : 0;
        public bool IsRare => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk && yk.IsRare;
        public bool IsLegend => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk && yk.IsLegend;
        public bool IsClassic => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk && yk.IsClassic;
        public bool ShowYokaiSpecialFlags => IsYokai;
        public bool ShowNpcSpecialOnly => HasSelection && !IsYokai;

        public bool SpecialUnk6
        {
            get => GetUnk6();
            set
            {
                if (_selectedCharacter == null) return;

                bool changed = false;
                if (_selectedCharacter.Model is YokaiCharabase yk && yk.Unk6 != value)
                {
                    yk.Unk6 = value;
                    changed = true;
                }
                else if (_selectedCharacter.Model is NPCCharabase npc && npc.Unk6 != value)
                {
                    npc.Unk6 = value;
                    changed = true;
                }

                if (!changed) return;

                _selectedCharacter.RefreshProperties();
                OnPropertyChanged(nameof(SpecialUnk6));
                OnPropertyChanged(nameof(Unk6));
            }
        }

        public bool SpecialIsRare
        {
            get => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk && yk.IsRare;
            set
            {
                if (!(_selectedCharacter?.Model is YokaiCharabase yk) || yk.IsRare == value) return;

                yk.IsRare = value;
                _selectedCharacter.RefreshProperties();
                OnPropertyChanged(nameof(SpecialIsRare));
                OnPropertyChanged(nameof(IsRare));
                AllowFilter();
            }
        }

        public bool SpecialIsLegend
        {
            get => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk && yk.IsLegend;
            set
            {
                if (!(_selectedCharacter?.Model is YokaiCharabase yk) || yk.IsLegend == value) return;

                yk.IsLegend = value;
                _selectedCharacter.RefreshProperties();
                OnPropertyChanged(nameof(SpecialIsLegend));
                OnPropertyChanged(nameof(IsLegend));
            }
        }

        public bool SpecialIsClassic
        {
            get => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk && yk.IsClassic;
            set
            {
                if (!(_selectedCharacter?.Model is YokaiCharabase yk) || yk.IsClassic == value) return;

                yk.IsClassic = value;
                _selectedCharacter.RefreshProperties();
                OnPropertyChanged(nameof(SpecialIsClassic));
                OnPropertyChanged(nameof(IsClassic));
            }
        }
        public string FavoriteFoodHashDisplay => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk ? $"0x{yk.FavoriteFoodHash:X8}" : "";
        public string HatedFoodHashDisplay => IsYokai && _selectedCharacter?.Model is YokaiCharabase yk ? $"0x{yk.HatedFoodHash:X8}" : "";

        // Keyword Info: "{FileNamePrefix:D2}, {FileNameNumber:D3}, {FileNameVariant:D2}"
        public string MedalKeyInfo
        {
            get
            {
                if (_selectedCharacter == null) return "";
                return $"{_selectedCharacter.Model.FileNamePrefix:D2}, {_selectedCharacter.Model.FileNameNumber:D3}, {_selectedCharacter.Model.FileNameVariant:D2}";
            }
        }

        // ???ャ뀕???癲??????Β?爰?Rank/Tribe ??ш끽維쀩??(???ㅳ늾?????釉먭숱??
        public BitmapImage? SelectedRankIcon => _selectedCharacter?.RankIcon;
        public BitmapImage? SelectedTribeIcon => _selectedCharacter?.TribeIcon;
        public BitmapImage? SelectedMedalIcon => _selectedMedalIcon;

        private enum HashField
        {
            Base,
            Name,
            Description
        }

        private void TryUpdateSelectedHash(HashField field, string? input)
        {
            if (_selectedCharacter == null) return;
            if (!TryParseHashInput(input, out int parsedHash))
            {
                OnPropertyChanged(field switch
                {
                    HashField.Base => nameof(BaseHashText),
                    HashField.Name => nameof(NameHashText),
                    HashField.Description => nameof(DescriptionHashText),
                    _ => nameof(BaseHashText)
                });
                return;
            }

            bool changed = false;
            switch (field)
            {
                case HashField.Base:
                    if (_selectedCharacter.Model.BaseHash != parsedHash)
                    {
                        _selectedCharacter.Model.BaseHash = parsedHash;
                        changed = true;
                    }
                    break;
                case HashField.Name:
                    if (_selectedCharacter.Model.NameHash != parsedHash)
                    {
                        _selectedCharacter.Model.NameHash = parsedHash;
                        _selectedCharacter.Name = ResolveCharacterName(_selectedCharacter.Model, _selectedCharacter.IsYokai);
                        changed = true;
                    }
                    break;
                case HashField.Description:
                    if (_selectedCharacter.Model.DescriptionHash != parsedHash)
                    {
                        _selectedCharacter.Model.DescriptionHash = parsedHash;
                        _selectedCharacter.Description = ResolveCharacterDescription(_selectedCharacter.Model);
                        changed = true;
                    }
                    break;
            }

            if (!changed) return;

            OnPropertyChanged(nameof(BaseHashDisplay));
            OnPropertyChanged(nameof(NameHashDisplay));
            OnPropertyChanged(nameof(DescriptionHashDisplay));
            OnPropertyChanged(nameof(BaseHashText));
            OnPropertyChanged(nameof(NameHashText));
            OnPropertyChanged(nameof(DescriptionHashText));
            OnPropertyChanged(nameof(DescriptionDisplay));
            OnPropertyChanged(nameof(MedalKeyInfo));

            AllowFilter();
        }

        private static bool TryParseHashInput(string? input, out int parsedHash)
        {
            parsedHash = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string trimmed = input.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(2);
            }

            if (int.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsedHash))
            {
                return true;
            }

            return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedHash);
        }

        // ??釉먮윥????ш끽維??Unk
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
            var totalTimer = System.Diagnostics.Stopwatch.StartNew();
            var sectionTimer = System.Diagnostics.Stopwatch.StartNew();

            _iconLoadCts?.Cancel();
            _iconLoadCts?.Dispose();
            _iconLoadCts = new CancellationTokenSource();
            var iconLoadToken = _iconLoadCts.Token;

            _allCharacters.Clear();
            var wrappers = new List<CharacterWrapper>(1024);

            // Load Yokai
            sectionTimer.Restart();
            var yokais = yw2.GetCharacterbase(true);
            long yokaiFetchMs = sectionTimer.ElapsedMilliseconds;
            sectionTimer.Restart();
            foreach (var chara in yokais)
            {
                string name = ResolveCharacterName(chara, true);
                string desc = ResolveCharacterDescription(chara);
                var wrapper = new CharacterWrapper(chara, true, name, desc, _game);
                wrappers.Add(wrapper);
                _allCharacters.Add(wrapper);
            }
            long yokaiWrapMs = sectionTimer.ElapsedMilliseconds;

            // Load NPC
            sectionTimer.Restart();
            var npcs = yw2.GetCharacterbase(false);
            long npcFetchMs = sectionTimer.ElapsedMilliseconds;
            sectionTimer.Restart();
            foreach (var chara in npcs)
            {
                string name = ResolveCharacterName(chara, false);
                string desc = ResolveCharacterDescription(chara);
                var wrapper = new CharacterWrapper(chara, false, name, desc, _game);
                wrappers.Add(wrapper);
                _allCharacters.Add(wrapper);
            }
            long npcWrapMs = sectionTimer.ElapsedMilliseconds;

            sectionTimer.Restart();
            AllowFilter();
            long filterMs = sectionTimer.ElapsedMilliseconds;

            System.Diagnostics.Trace.WriteLine($"[Perf] CharacterViewModel.LoadCharacters total={totalTimer.ElapsedMilliseconds}ms yokaiFetch={yokaiFetchMs}ms yokaiWrap={yokaiWrapMs}ms npcFetch={npcFetchMs}ms npcWrap={npcWrapMs}ms filter={filterMs}ms totalCount={_allCharacters.Count}");

            _ = LoadCharacterIconsAsync(wrappers, iconLoadToken);
        }

        private async Task LoadCharacterIconsAsync(IReadOnlyList<CharacterWrapper> wrappers, CancellationToken token)
        {
            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                int updated = 0;

                await Task.Run(async () =>
                {
                    var pendingBatch = new List<(CharacterWrapper Wrapper, BitmapImage Icon)>(24);
                    foreach (var wrapper in wrappers)
                    {
                        token.ThrowIfCancellationRequested();

                        var icon = LoadCharacterIcon(wrapper.Model);
                        if (icon == null)
                        {
                            continue;
                        }

                        pendingBatch.Add((wrapper, icon));
                        if (pendingBatch.Count < 24)
                        {
                            continue;
                        }

                        updated += pendingBatch.Count;
                        await ApplyIconBatchAsync(new List<(CharacterWrapper Wrapper, BitmapImage Icon)>(pendingBatch), token);
                        pendingBatch.Clear();
                    }

                    if (pendingBatch.Count > 0)
                    {
                        updated += pendingBatch.Count;
                        await ApplyIconBatchAsync(pendingBatch, token);
                    }
                }, token);

                int cacheCount;
                lock (SharedCharacterIconCacheLock)
                {
                    cacheCount = SharedCharacterIconCache.Count;
                }
                System.Diagnostics.Trace.WriteLine($"[Perf] CharacterViewModel.LoadCharacterIconsAsync total={timer.ElapsedMilliseconds}ms updated={updated} cached={cacheCount}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] LoadCharacterIconsAsync Error: {ex.Message}");
            }
        }

        private static async Task ApplyIconBatchAsync(List<(CharacterWrapper Wrapper, BitmapImage Icon)> batch, CancellationToken token)
        {
            if (batch.Count == 0)
            {
                return;
            }

            if (System.Windows.Application.Current?.Dispatcher == null || System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                foreach (var item in batch)
                {
                    item.Wrapper.Icon = item.Icon;
                }
                return;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var item in batch)
                {
                    item.Wrapper.Icon = item.Icon;
                }
            }, DispatcherPriority.Background, token);
        }

        private string ResolveCharacterName(CharaBase chara, bool isYokai)
        {
            if (_nameMap.TryGetValue(chara.NameHash, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }

            return isYokai
                ? $"(이름 미등록: {chara.NameHash:X8})"
                : $"(NPC: {chara.NameHash:X8})";
        }

        private string ResolveCharacterDescription(CharaBase chara)
        {
            // Legacy priority: YW3 uses chara_desc_text first, then chara_text.Texts.
            if (_game.Name == "Yo-Kai Watch 3" &&
                _charaDescTextMap.TryGetValue(chara.DescriptionHash, out var descFromYw3) &&
                !string.IsNullOrWhiteSpace(descFromYw3))
            {
                return descFromYw3;
            }

            if (_charaTextDescMap.TryGetValue(chara.DescriptionHash, out var descFromCommon) &&
                !string.IsNullOrWhiteSpace(descFromCommon))
            {
                return descFromCommon;
            }

            if (_descMap.TryGetValue(chara.DescriptionHash, out var fallback))
            {
                return fallback;
            }

            return string.Empty;
        }

        private BitmapImage? LoadCharacterIcon(CharaBase chara)
        {
            string cacheKey = $"{chara.FileNamePrefix:D2}.{chara.FileNameNumber:D3}.{chara.FileNameVariant:D2}";
            lock (SharedCharacterIconCacheLock)
            {
                if (SharedCharacterIconCache.TryGetValue(cacheKey, out var cached))
                {
                    return cached;
                }
            }

            BitmapImage? loaded = TryLoadCharacterIconFromGame(chara) ?? TryLoadCharacterIconFromExternalPng(chara);
            if (loaded != null)
            {
                lock (SharedCharacterIconCacheLock)
                {
                    SharedCharacterIconCache[cacheKey] = loaded;
                }
            }

            return loaded;
        }

        private BitmapImage? TryLoadCharacterIconFromGame(CharaBase chara)
        {
            if (_game?.Files == null || !_game.Files.TryGetValue("face_icon", out var faceIconFile))
            {
                return null;
            }

            if (!GameSupport.PrefixLetter.TryGetValue(chara.FileNamePrefix, out char prefixLetter) || prefixLetter == '?')
            {
                return null;
            }

            if (faceIconFile.File?.Directory == null || string.IsNullOrWhiteSpace(faceIconFile.Path))
            {
                return null;
            }

            try
            {
                string modelName = GameSupport.GetFileModelText(chara.FileNamePrefix, chara.FileNameNumber, chara.FileNameVariant);
                string fullPath = $"{faceIconFile.Path}/{modelName}.xi";
                var directory = faceIconFile.File.Directory;
                if (!directory.FileExists(fullPath))
                {
                    return null;
                }

                var vf = directory.GetFileStreamFromFullPath(fullPath);
                byte[]? data = vf?.ByteContent ?? vf?.ReadWithoutCaching();
                if (data == null || data.Length == 0)
                {
                    return null;
                }

                using var bitmap = IMGC.ToBitmap(data);
                return ToBitmapImage(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] LoadCharacterIcon failed: {ex.Message}");
                return null;
            }
        }

        private static BitmapImage? TryLoadCharacterIconFromExternalPng(CharaBase chara)
        {
            string modelName = GameSupport.GetFileModelText(chara.FileNamePrefix, chara.FileNameNumber, chara.FileNameVariant);
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string formattedName = $"face_icon.{chara.FileNamePrefix:D2}.{chara.FileNameNumber:D3}.{chara.FileNameVariant:D2}.png";

            string[] candidatePaths =
            {
                Path.Combine(basePath, formattedName),
                Path.Combine(basePath, $"{modelName}.png"),
                Path.Combine(basePath, "Resources", "face_icon", formattedName),
                Path.Combine(basePath, "Resources", "Face Icon", formattedName),
                Path.Combine(basePath, "Resources", "face_icon", $"{modelName}.png"),
                Path.Combine(basePath, "Resources", "Face Icon", $"{modelName}.png")
            };

            foreach (string path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    return LoadBitmapFromPath(path);
                }
            }

            return null;
        }

        private static BitmapImage? ToBitmapImage(System.Drawing.Image? image)
        {
            if (image == null) return null;

            try
            {
                using var memory = new MemoryStream();
                image.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage? LoadBitmapFromPath(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private void SelectedCharacter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CharacterWrapper.MedalPosX) ||
                e.PropertyName == nameof(CharacterWrapper.MedalPosY))
            {
                UpdateSelectedMedalIcon();
                OnPropertyChanged(nameof(MedalKeyInfo));
            }
            else if (e.PropertyName == nameof(CharacterWrapper.FileNameVariant))
            {
                OnPropertyChanged(nameof(MedalKeyInfo));
            }
        }

        private void UpdateSelectedMedalIcon()
        {
            BitmapImage? next = null;
            var selected = _selectedCharacter;
            if (selected != null)
            {
                int posX = selected.Model.MedalPosX;
                int posY = selected.Model.MedalPosY;
                int size = ResolveMedalIconSize();
                System.Diagnostics.Trace.WriteLine(
                    $"[MedalIcon] update char={selected.Name}, file={selected.Model.FileNamePrefix:D2}.{selected.Model.FileNameNumber:D3}.{selected.Model.FileNameVariant:D2}, pos=({posX},{posY}), size={size}, sheetSource={_medalFaceIconSheetSource}");

                using var medalBitmap = CropMedalIcon(posX, posY, size);
                if (medalBitmap != null)
                {
                    next = ToBitmapImage(medalBitmap);
                }
            }

            if (!ReferenceEquals(_selectedMedalIcon, next))
            {
                _selectedMedalIcon = next;
                OnPropertyChanged(nameof(SelectedMedalIcon));
            }
        }

        private int ResolveMedalIconSize()
        {
            // Legacy behavior:
            // - Yo-Kai Watch 3 uses 32x32 medal cells
            // - Yo-Kai Watch 1/2/Busters use 44x44 medal cells
            if (_game.Name.Contains("Yo-Kai Watch 3", StringComparison.OrdinalIgnoreCase))
            {
                return 32;
            }

            return 44;
        }

        private static bool IsPngSignature(byte[] data)
        {
            return data.Length >= 8 &&
                   data[0] == 0x89 &&
                   data[1] == 0x50 &&
                   data[2] == 0x4E &&
                   data[3] == 0x47 &&
                   data[4] == 0x0D &&
                   data[5] == 0x0A &&
                   data[6] == 0x1A &&
                   data[7] == 0x0A;
        }

        private static void LogMedalSheetBytes(string source, byte[] data)
        {
            int previewLength = Math.Min(16, data.Length);
            string headerHex = previewLength > 0
                ? BitConverter.ToString(data, 0, previewLength)
                : "empty";
            string asciiPreview = previewLength > 0
                ? new string(data.Take(previewLength).Select(b => b >= 32 && b <= 126 ? (char)b : '.').ToArray())
                : string.Empty;

            System.Diagnostics.Trace.WriteLine(
                $"[MedalSheet] source={source}, bytes={data.Length}, header={headerHex}, ascii={asciiPreview}, isPng={IsPngSignature(data)}");
        }

        private void LogMedalSheetSummaryIfNeeded()
        {
            if (_medalSheetSummaryLogged || _medalFaceIconSheet == null)
            {
                return;
            }

            _medalSheetSummaryLogged = true;
            System.Diagnostics.Trace.WriteLine(
                $"[MedalSheet] active source={_medalFaceIconSheetSource}, size={_medalFaceIconSheet.Width}x{_medalFaceIconSheet.Height}, pixelFormat={_medalFaceIconSheet.PixelFormat}");
        }

        private System.Drawing.Bitmap? DecodeMedalSheetWithDiagnostics(byte[] data, string source)
        {
            LogMedalSheetBytes(source, data);

            try
            {
                var decoded = IMGC.ToBitmap(data);
                if (decoded != null)
                {
                    _medalFaceIconSheetSource = source;
                    System.Diagnostics.Trace.WriteLine(
                        $"[MedalSheet] IMGC decode success ({source}) -> {decoded.Width}x{decoded.Height}, pixelFormat={decoded.PixelFormat}");
                    return decoded;
                }

                System.Diagnostics.Trace.WriteLine($"[MedalSheet] IMGC decode returned null ({source})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[MedalSheet] IMGC decode exception ({source}): {ex.Message}");
            }

            if (IsPngSignature(data))
            {
                try
                {
                    using var ms = new MemoryStream(data);
                    using var png = new System.Drawing.Bitmap(ms);
                    var cloned = new System.Drawing.Bitmap(png);
                    _medalFaceIconSheetSource = source;
                    System.Diagnostics.Trace.WriteLine(
                        $"[MedalSheet] PNG fallback decode success ({source}) -> {cloned.Width}x{cloned.Height}, pixelFormat={cloned.PixelFormat}");
                    return cloned;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[MedalSheet] PNG fallback decode failed ({source}): {ex.Message}");
                }
            }

            return null;
        }

        private System.Drawing.Bitmap? GetMedalFaceIconSheet()
        {
            if (_medalFaceIconSheetLoaded)
            {
                LogMedalSheetSummaryIfNeeded();
                return _medalFaceIconSheet;
            }

            _medalFaceIconSheetLoaded = true;

            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string projectRoot = Path.Combine(basePath, "..", "..", "..");

                // [Priority 0] Temporary decoded PNG (user-provided emergency path)
                if (File.Exists(TemporaryDecodedMedalSheetPath))
                {
                    try
                    {
                        byte[] pngBytes = File.ReadAllBytes(TemporaryDecodedMedalSheetPath);
                        _medalFaceIconSheet = DecodeMedalSheetWithDiagnostics(pngBytes, TemporaryDecodedMedalSheetPath);
                        if (_medalFaceIconSheet != null)
                        {
                            System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Loaded temporary decoded medal sheet: {TemporaryDecodedMedalSheetPath}");
                            LogMedalSheetSummaryIfNeeded();
                            return _medalFaceIconSheet;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Failed to load temporary decoded medal sheet: {ex.Message}");
                    }
                }

                // [Priority 1] Files dictionary (legacy Charabase behavior)
                if (_game?.Files != null &&
                    _game.Files.TryGetValue("face_icon", out var faceIconFile) &&
                    faceIconFile.File?.Directory != null &&
                    !string.IsNullOrWhiteSpace(faceIconFile.Path))
                {
                    try
                    {
                        string fullPath = faceIconFile.Path.EndsWith(".xi", StringComparison.OrdinalIgnoreCase)
                            ? faceIconFile.Path
                            : $"{faceIconFile.Path}/face_icon.xi";
                        System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Trying face_icon dictionary path: {fullPath}");
                        byte[] data = faceIconFile.File.Directory.GetFileFromFullPath(fullPath);
                        if (data.Length > 0)
                        {
                            _medalFaceIconSheet = DecodeMedalSheetWithDiagnostics(data, fullPath);
                            if (_medalFaceIconSheet != null)
                            {
                                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Medal sheet loaded from dictionary path: {fullPath}");
                                LogMedalSheetSummaryIfNeeded();
                                return _medalFaceIconSheet;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Dictionary face_icon load failed: {ex.Message}");
                    }
                }

                // [Priority 2] YW2 Legacy path (CharabaseDetailPanel logic)
                // data/menu/face_icon.xi
                if (_game is YW2 yw2 && yw2.Game?.Directory != null)
                {
                    const string legacyPath = "data/menu/face_icon.xi";
                    if (yw2.Game.Directory.FileExists(legacyPath))
                    {
                        byte[] data = yw2.Game.Directory.GetFileFromFullPath(legacyPath);
                        if (data.Length > 0)
                        {
                            _medalFaceIconSheet = DecodeMedalSheetWithDiagnostics(data, legacyPath);
                            if (_medalFaceIconSheet != null)
                            {
                                System.Diagnostics.Trace.WriteLine("[CharacterViewModel] Medal sheet loaded from YW2 legacy path: data/menu/face_icon.xi");
                                LogMedalSheetSummaryIfNeeded();
                                return _medalFaceIconSheet;
                            }
                        }
                    }
                }

                // [Priority 3] Optional external PNG overrides (modding convenience)
                string[] pngCandidates =
                {
                    Path.Combine(basePath, "face_icon.00.png"),
                    Path.Combine(basePath, "face_icon_export.png"),
                    Path.Combine(projectRoot, "face_icon.00.png"),
                    Path.Combine(projectRoot, "face_icon_export.png"),
                    Path.Combine(basePath, "face_icon.png"),
                    Path.Combine(projectRoot, "face_icon.png")
                };

                foreach (var pngPath in pngCandidates)
                {
                    if (File.Exists(pngPath))
                    {
                        try
                        {
                            byte[] pngBytes = File.ReadAllBytes(pngPath);
                            _medalFaceIconSheet = DecodeMedalSheetWithDiagnostics(pngBytes, pngPath);
                            if (_medalFaceIconSheet != null)
                            {
                                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Loaded external medal sheet: {pngPath}");
                                LogMedalSheetSummaryIfNeeded();
                                return _medalFaceIconSheet;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Failed to load PNG {pngPath}: {ex.Message}");
                        }
                    }
                }

                // [Priority 4] Local XI Fallback (Absolute last resort)
                string[] xiCandidates =
                {
                    Path.Combine(basePath, "face_icon.xi"),
                    Path.Combine(projectRoot, "face_icon.xi")
                };

                foreach (var xiPath in xiCandidates)
                {
                    if (!File.Exists(xiPath)) continue;

                    byte[] data = File.ReadAllBytes(xiPath);
                    if (data.Length <= 0) continue;

                    _medalFaceIconSheet = DecodeMedalSheetWithDiagnostics(data, xiPath);
                    if (_medalFaceIconSheet != null)
                    {
                        System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Medal sheet loaded from local xi fallback: {xiPath}");
                        LogMedalSheetSummaryIfNeeded();
                        return _medalFaceIconSheet;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] GetMedalFaceIconSheet failed: {ex.Message}");
            }

            if (_medalFaceIconSheet == null)
            {
                System.Diagnostics.Trace.WriteLine("[MedalSheet] No valid medal sheet could be loaded.");
            }

            return _medalFaceIconSheet;
        }

        private System.Drawing.Bitmap? CropMedalIcon(int posX, int posY, int size)
        {
            if (posX < 0 || posY < 0 || size <= 0)
            {
                return null;
            }

            var sheet = GetMedalFaceIconSheet();
            if (sheet == null)
            {
                return null;
            }

            var cropRect = new System.Drawing.Rectangle(posX * size, posY * size, size, size);
            string cropKey = $"{sheet.Width}x{sheet.Height}|{posX},{posY}|{size}";
            if (!string.Equals(_lastMedalCropLogKey, cropKey, StringComparison.Ordinal))
            {
                _lastMedalCropLogKey = cropKey;
                System.Diagnostics.Trace.WriteLine(
                    $"[MedalCrop] sheet={sheet.Width}x{sheet.Height}, pos=({posX},{posY}), size={size}, rect=({cropRect.X},{cropRect.Y},{cropRect.Width},{cropRect.Height})");
            }

            if (cropRect.X >= sheet.Width || cropRect.Y >= sheet.Height)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[MedalCrop] Out of bounds: rect origin ({cropRect.X},{cropRect.Y}) beyond sheet {sheet.Width}x{sheet.Height}");
                return new System.Drawing.Bitmap(size, size);
            }

            cropRect.Intersect(new System.Drawing.Rectangle(0, 0, sheet.Width, sheet.Height));
            if (cropRect.Width <= 0 || cropRect.Height <= 0)
            {
                return new System.Drawing.Bitmap(size, size);
            }

            var result = new System.Drawing.Bitmap(cropRect.Width, cropRect.Height);
            using (var g = System.Drawing.Graphics.FromImage(result))
            {
                g.DrawImage(sheet, new System.Drawing.Rectangle(0, 0, cropRect.Width, cropRect.Height), cropRect, System.Drawing.GraphicsUnit.Pixel);
            }

            int sampleX = Math.Min(result.Width - 1, Math.Max(0, result.Width / 2));
            int sampleY = Math.Min(result.Height - 1, Math.Max(0, result.Height / 2));
            var sample = result.GetPixel(sampleX, sampleY);
            System.Diagnostics.Trace.WriteLine(
                $"[MedalCrop] result={result.Width}x{result.Height}, centerPixel=ARGB({sample.A},{sample.R},{sample.G},{sample.B})");

            return result;
        }
        private bool _filterIsYokaiChecked = true;
        private bool _filterIsNPCChecked = false;
        private bool _filterHashSearch = false;
        private int _filterFavoriteFood = -1;
        private int _filterHatedFood = -1;
        private SortOption _sortOrder = SortOption.FileNameOrder;

        // ???ャ뀕?????ш낄援????ш끽維쀩??(?熬곸쥓嫄???λ궚嶺뚮Ĳ?뉓짆??袁⑸즴????
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
                var previousSelected = _selectedCharacter;

                var result = _allCharacters.Where(c =>
                {
                    // ?怨멸텭??沃섅뀙??關履????ш낄援??(??釉먮윥??NPC 癲ル슪???띿물筌먯쥙??춯癒?걞獒?
                    if (c.IsYokai && !_filterIsYokaiChecked) return false;
                    if (!c.IsYokai && !_filterIsNPCChecked) return false;

                    // ????몄릇???濡ろ떟???                    if (!string.IsNullOrEmpty(query))
                    {
                        // ???⑤８六??濡ろ떟???(癲ル슪???띿물???筌?諭븝┼???筌????
                        if (_filterHashSearch)
                        {
                            // ???⑤８六??濡ろ떟???癲ル슢?꾤땟????????query?????⑤８六?????⑤똾留?
                            if (!c.Model.BaseHash.ToString("X8").ToLower().Contains(query) &&
                                !c.Model.NameHash.ToString("X8").ToLower().Contains(query))
                                return false;
                        }
                        else // ???⑥ロ떘 ????몄릇???濡ろ떟???(?????
                        {
                            if (!c.Name.ToLower().Contains(query))
                                return false;
                        }
                    }

                    // Rank ??ш낄援??
                    if (_filterRank.HasValue && c.IsYokai && c.Model is YokaiCharabase yk)
                    {
                        if (yk.Rank != _filterRank.Value)
                            return false;
                    }

                    // Tribe ??ш낄援??
                    if (_filterTribe.HasValue && c.IsYokai && c.Model is YokaiCharabase yk2)
                    {
                        if (yk2.Tribe != _filterTribe.Value)
                            return false;
                    }

                    // Rarity ??ш낄援??
                    if (_filterIsRare.HasValue && c.IsYokai && c.Model is YokaiCharabase yk3)
                    {
                        if (yk3.IsRare != _filterIsRare.Value)
                            return false;
                    }

                    // Favorite Food ??ш낄援??
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
                        sortedResult = result.OrderBy(c => GetFileNamePrefixSortKey(c.Model.FileNamePrefix))
                                             .ThenBy(c => c.Model.FileNameNumber)
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

                // ?域밸Ŧ遊얕짆??????녿ぅ??熬곣뫀肄?
                var sortedList = sortedResult.ToList();

                FilteredList.Clear();
                foreach (var item in sortedList)
                {
                    FilteredList.Add(item);
                }

                CharacterWrapper? nextSelection = null;
                if (sortedList.Count > 0)
                {
                    nextSelection = (previousSelected != null && sortedList.Contains(previousSelected))
                        ? previousSelected
                        : sortedList[0];
                }

                if (!ReferenceEquals(_selectedCharacter, nextSelection))
                {
                    SelectedCharacter = nextSelection;
                }

                if (nextSelection == null)
                {
                    IsTribeSelectorVisible = false;
                    IsRankSelectorVisible = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] AllowFilter Error: {ex.Message}");
            }
        }

        /// <summary>메달(파일명) 정렬용: 02, 05 프리픽스는 06 다음에 나열</summary>
        private static double GetFileNamePrefixSortKey(int prefix)
        {
            if (prefix == 2) return 6.2;  // 02 -> 06 다음
            if (prefix == 5) return 6.5;  // 05 -> 02 다음
            return prefix;
        }

        private string GetFoodResourceKey(int id) => id switch
        {
            0x01 => "Rice Balls",
            0x02 => "Bread",
            0x03 => "Candy",
            0x04 => "Milk",
            0x05 => "Juice",
            0x06 => "Hamburgers",
            0x07 => "Ramen",
            0x08 => "Sushi",
            0x09 => "Chinese Food",
            0x0B => "Vegetables",
            0x0C => "Meat",
            0x0D => "Seafood",
            0x0E => "Curry",
            0x0F => "Sweets",
            0x10 => "Oden Stew",
            0x11 => "Soba",
            0x12 => "Snacks",
            0x13 => "Chocobars",
            0x14 => "Ice Cream",
            0x15 => "Donut",
            0x16 => "Pizza",
            0x17 => "Hot Dog",
            0x18 => "Pasta",
            0x19 => "Tempura",
            0x1A => "Mega Tasty Bar",
            0x1C => "Sukiyaki",
            _ => "no_food"
        };

        private void LoadFoodList()
        {
            FoodOptions.Clear();
            if (_game?.FoodsType == null) return;

            foreach (var kv in _game.FoodsType)
            {
                string displayName = ResolveFoodDisplayName(kv.Key, kv.Value);

                // [FIX] Use ID-based resource key for icon lookup, since values might be localized
                string resourceKey = GetFoodResourceKey(kv.Key);
                var icon = IconCache.GetFoodIcon(resourceKey) ?? IconCache.GetFoodIcon("no_food");

                FoodOptions.Add(new FilterItem(kv.Key, displayName, icon));
            }

            var sorted = FoodOptions.OrderBy(x => x.DisplayName).ToList();
            FoodOptions.Clear();
            foreach (var item in sorted)
            {
                FoodOptions.Add(item);
            }
        }



        private void InitializeSelectionLists()
        {
            RankSelectionList.Clear();
            TribeSelectionList.Clear();

            for (int rank = 0; rank <= 5; rank++)
            {
                string rankName = rank switch
                {
                    0 => "E",
                    1 => "D",
                    2 => "C",
                    3 => "B",
                    4 => "A",
                    5 => "S",
                    _ => "?"
                };
                RankSelectionList.Add(new FilterItem(rank, $"랭크 {rankName}", IconCache.GetRankIcon(rank)));
            }

            foreach (var tribe in _game.Tribes.OrderBy(kv => kv.Key))
            {
                TribeSelectionList.Add(new FilterItem(tribe.Key, tribe.Value, IconCache.GetTribeIcon(tribe.Key)));
            }
        }

        private void InitializeFilterLists()
        {
            RankFilterList.Clear();
            TribeFilterList.Clear();
            FoodFilterList.Clear();
            SortOptionList.Clear();

            // Ranks
            RankFilterList.Add(new FilterItem(null, "전체"));
            for (int i = 0; i <= 5; i++) // E to S (YW2 standard?)
            {
                // Assuming standard rank mapping: 0=E, 1=D, 2=C, 3=B, 4=A, 5=S
                string r = i switch { 0 => "E", 1 => "D", 2 => "C", 3 => "B", 4 => "A", 5 => "S", _ => "?" };
                RankFilterList.Add(new FilterItem(i, $"랭크 {r}", IconCache.GetRankIcon(i)));
            }

            // Tribes
            TribeFilterList.Add(new FilterItem(null, "전체"));
            for (int i = 1; i <= 8; i++) // Standard Tribes
            {
                // 1=Brave, 2=Mysterious, 3=Tough, 4=Charming, 5=Heartful, 6=Shady, 7=Eerie, 8=Slippery
                // We will use IconCache for names if possible, or generic
                TribeFilterList.Add(new FilterItem(i, $"종족 {i}", IconCache.GetTribeIcon(i)));
            }
            TribeFilterList.Add(new FilterItem(0, "종족 없음", IconCache.GetTribeIcon(0))); // Kaima? or None

            // Sort Options
            SortOptionList.Add(new SortItem(SortOption.FileNameOrder, "파일명순"));
            SortOptionList.Add(new SortItem(SortOption.Alphabetical, "이름순"));
            SortOptionList.Add(new SortItem(SortOption.HashOrder, "해시순"));
            SortOptionList.Add(new SortItem(SortOption.TribeOrder, "종족순"));

            SelectedRankFilter = RankFilterList.FirstOrDefault();
            SelectedTribeFilter = TribeFilterList.FirstOrDefault();
            SelectedSortOption = SortOptionList.FirstOrDefault();

            // Populate Food Options
            LoadFoodList();
        }

        private void LoadCharaNames()
        {
            _nameMap.Clear();
            _descMap.Clear();
            _charaTextDescMap.Clear();

            try
            {
                // chara_text 로드 (yw2_lg_ko.fa에서)
                if (_game.Files == null || !_game.Files.ContainsKey("chara_text"))
                {
                    System.Diagnostics.Trace.WriteLine("[CharacterViewModel] chara_text not found in Files dictionary");
                    return;
                }

                var gf = _game.Files["chara_text"];
                var vf = gf.GetStream();
                if (vf == null)
                {
                    System.Diagnostics.Trace.WriteLine("[CharacterViewModel] chara_text stream is null");
                    return;
                }

                byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                if (data == null || data.Length == 0)
                {
                    System.Diagnostics.Trace.WriteLine("[CharacterViewModel] chara_text data is empty");
                    return;
                }

                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] chara_text loaded: {data.Length} bytes");

                // T2bþ로 파싱
                var textObj = new T2bþ(data);
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] T2bþ parsed: Nouns={textObj.Nouns.Count}, Texts={textObj.Texts.Count}");

                // Nouns 딕셔너리에서 이름 매핑 (hash -> name)
                foreach (var kv in textObj.Nouns)
                {
                    if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrEmpty(kv.Value.Strings[0].Text))
                    {
                        _nameMap[kv.Key] = kv.Value.Strings[0].Text;
                    }
                }

                // Texts 딕셔너리에서 설명 매핑 (hash -> desc)
                foreach (var kv in textObj.Texts)
                {
                    if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrEmpty(kv.Value.Strings[0].Text))
                    {
                        _charaTextDescMap[kv.Key] = kv.Value.Strings[0].Text;
                    }
                }

                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Name map: {_nameMap.Count} entries, Desc map: {_charaTextDescMap.Count} entries");

                // T2bþ 파싱 결과가 비어있으면 CfgBin 수동 파싱 폴백
                if (_nameMap.Count == 0)
                {
                    System.Diagnostics.Trace.WriteLine("[CharacterViewModel] Nouns empty, trying manual CfgBin parse...");
                    ManualParseCharaText(data);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Failed to load chara_text: {ex.Message}");
            }
        }

        private void ManualParseCharaText(byte[] data)
        {
            try
            {
                var cfg = new CfgBin();
                cfg.Open(data);

                string[] targets = { "NOUN_INFO_BEGIN", "TEXT_INFO_BEGIN" };
                var cfgEntries = cfg.Entries.Where(x => targets.Contains(x.GetName())).ToList();
                if (cfgEntries.Count == 0 && cfg.Entries.Count > 0)
                    cfgEntries = cfg.Entries.Where(x => x.Children.Count > 10).ToList();

                foreach (var entry in cfgEntries)
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
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Manual parse: {_nameMap.Count} entries");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Manual parse failed: {ex.Message}");
            }
        }

        private void LoadItemText()
        {
            _itemNameMap.Clear();

            try
            {
                if (_game.Files == null || !_game.Files.ContainsKey("item_text"))
                {
                    System.Diagnostics.Trace.WriteLine("[CharacterViewModel] item_text not found in Files dictionary");
                    return;
                }

                var gf = _game.Files["item_text"];
                var vf = gf.GetStream();
                if (vf == null) return;

                byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                if (data == null || data.Length == 0) return;

                var textObj = new T2bþ(data);

                foreach (var kv in textObj.Nouns)
                {
                    if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrEmpty(kv.Value.Strings[0].Text))
                    {
                        _itemNameMap[kv.Key] = kv.Value.Strings[0].Text;
                    }
                }

                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Item name map: {_itemNameMap.Count} entries");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterViewModel] Failed to load item_text: {ex.Message}");
            }
        }

        private string ResolveFoodDisplayName(int hash, string fallbackName)
        {
            // [FIX] Prioritize fallbackName (from FoodsType.cs) because the user manually localized it to Korean.
            // If we look up _itemNameMap first, it might return the English name from the game files.
            if (!string.IsNullOrWhiteSpace(fallbackName))
            {
                return fallbackName;
            }

            if (_itemNameMap.TryGetValue(hash, out var itemName) && !string.IsNullOrWhiteSpace(itemName))
            {
                return itemName;
            }

            return $"알 수 없는 음식 (0x{hash:X8})";
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

        private readonly IGame _game;
        public CharaBase Model { get; }
        public bool IsYokai { get; }
        public int Id => Model.BaseHash;

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _description;
        public string Description
        {
            get
            {
                if (_description == null) return "";
                // Handle escaped newlines and ensure CRLF/CR becomes LF only for consistency
                return _description.Replace("\\n", "\n").Replace("\\r", "").Replace("\r", "");
            }
            set
            {
                if (_description != value)
                {
                    // Normalize to LF only on save to prevent accumulation of CRs
                    _description = value?.Replace("\r", "");
                    OnPropertyChanged();
                }
            }
        }

        private BitmapImage? _icon;
        public BitmapImage? Icon
        {
            get => _icon;
            set
            {
                if (!ReferenceEquals(_icon, value))
                {
                    _icon = value;
                    OnPropertyChanged();
                }
            }
        }
        public string Tribe => IsYokai && Model is YokaiCharabase yk ? $"종족 {yk.Tribe}" : "NPC";
        public string Rank => IsYokai && Model is YokaiCharabase yk ? $"랭크 {yk.Rank}" : "-";
        public bool IsRare => IsYokai && Model is YokaiCharabase yk && yk.IsRare;
        public bool IsLegend => IsYokai && Model is YokaiCharabase yk && yk.IsLegend;

        public int MedalPosX
        {
            get => Model.MedalPosX;
            set
            {
                if (Model.MedalPosX != value)
                {
                    Model.MedalPosX = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MedalPosY
        {
            get => Model.MedalPosY;
            set
            {
                if (Model.MedalPosY != value)
                {
                    Model.MedalPosY = value;
                    OnPropertyChanged();
                }
            }
        }

        public int FileNameVariant
        {
            get => Model.FileNameVariant;
            set
            {
                if (Model.FileNameVariant != value)
                {
                    Model.FileNameVariant = value;
                    OnPropertyChanged();
                }
            }
        }

        public int FavoriteFoodHash
        {
            get => IsYokai && Model is YokaiCharabase yk ? yk.FavoriteFoodHash : 0;
            set
            {
                if (IsYokai && Model is YokaiCharabase yk && yk.FavoriteFoodHash != value)
                {
                    yk.FavoriteFoodHash = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FavoriteFood));
                }
            }
        }

        public int HatedFoodHash
        {
            get => IsYokai && Model is YokaiCharabase yk ? yk.HatedFoodHash : 0;
            set
            {
                if (IsYokai && Model is YokaiCharabase yk && yk.HatedFoodHash != value)
                {
                    yk.HatedFoodHash = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HatedFood));
                }
            }
        }

        public string FavoriteFood
        {
            get
            {
                if (!IsYokai || Model is not YokaiCharabase yk) return "-";
                if (_game.FoodsType != null && _game.FoodsType.TryGetValue(yk.FavoriteFoodHash, out var foodName))
                    return foodName;
                return $"알 수 없는 음식 (0x{yk.FavoriteFoodHash:X8})";
            }
        }

        public string HatedFood
        {
            get
            {
                if (!IsYokai || Model is not YokaiCharabase yk) return "-";
                if (_game.FoodsType != null && _game.FoodsType.TryGetValue(yk.HatedFoodHash, out var foodName))
                    return foodName;
                return $"알 수 없는 음식 (0x{yk.HatedFoodHash:X8})";
            }
        }

        // Backup fields for Revert
        private readonly CharaBase _originalModel;
        private readonly string _originalNameStr;
        private readonly string _originalDescriptionStr;

        public CharacterWrapper(CharaBase model, bool isYokai, string name, string description, IGame game, BitmapImage? icon = null)
        {
            Model = model;
            IsYokai = isYokai;
            _name = name;
            _description = description;
            _game = game;
            _icon = icon;

            // Deep Copy Backup
            _originalModel = CloneModel(model);
            _originalNameStr = name;
            _originalDescriptionStr = description;
        }

        public void Revert()
        {
            // Restore Model Properies
            CopyModel(_originalModel, Model);

            // Restore Wrapper Properties
            Name = _originalNameStr;
            Description = _originalDescriptionStr;

            // Refresh UI
            RefreshProperties();
        }

        private CharaBase CloneModel(CharaBase source)
        {
            if (source is YokaiCharabase ykSource)
            {
                var ykDest = new YokaiCharabase();
                CopyModel(ykSource, ykDest);
                return ykDest;
            }
            else if (source is NPCCharabase npcSource)
            {
                var npcDest = new NPCCharabase();
                CopyModel(npcSource, npcDest);
                return npcDest;
            }
            return source; // Should not happen
        }

        private void CopyModel(CharaBase source, CharaBase dest)
        {
            dest.BaseHash = source.BaseHash;
            dest.FileNamePrefix = source.FileNamePrefix;
            dest.FileNameNumber = source.FileNameNumber;
            dest.FileNameVariant = source.FileNameVariant;
            dest.NameHash = source.NameHash;
            dest.DescriptionHash = source.DescriptionHash;
            dest.MedalPosX = source.MedalPosX;
            dest.MedalPosY = source.MedalPosY;

            if (source is YokaiCharabase ykSource && dest is YokaiCharabase ykDest)
            {
                ykDest.Unk1 = ykSource.Unk1;
                ykDest.Unk2 = ykSource.Unk2;
                ykDest.Unk3 = ykSource.Unk3;
                ykDest.Unk4 = ykSource.Unk4;
                ykDest.Unk5 = ykSource.Unk5;
                ykDest.Unk6 = ykSource.Unk6;
                ykDest.Rank = ykSource.Rank;
                ykDest.IsRare = ykSource.IsRare;
                ykDest.IsLegend = ykSource.IsLegend;
                ykDest.FavoriteFoodHash = ykSource.FavoriteFoodHash;
                ykDest.HatedFoodHash = ykSource.HatedFoodHash;
                ykDest.Unk7 = ykSource.Unk7;
                ykDest.Unk8 = ykSource.Unk8;
                ykDest.Unk9 = ykSource.Unk9;
                ykDest.Unk10 = ykSource.Unk10;
                ykDest.Unk11 = ykSource.Unk11;
                ykDest.Tribe = ykSource.Tribe;
                ykDest.IsClassic = ykSource.IsClassic;
                ykDest.Unk12 = ykSource.Unk12;
            }
            else if (source is NPCCharabase npcSource && dest is NPCCharabase npcDest)
            {
                npcDest.Unk1 = npcSource.Unk1;
                npcDest.Unk2 = npcSource.Unk2;
                npcDest.Unk3 = npcSource.Unk3;
                npcDest.Unk4 = npcSource.Unk4;
                npcDest.Unk5 = npcSource.Unk5;
                npcDest.Unk6 = npcSource.Unk6;
            }
        }

        public string RankIconPath { get; private set; } = "";
        public string TribeIconPath { get; private set; } = "";
        public string FavoriteFoodIconPath { get; private set; } = "";
        public BitmapImage? RankIcon => IconCache.GetRankIcon(IsYokai && Model is YokaiCharabase yk ? yk.Rank : -1);
        public BitmapImage? TribeIcon => IconCache.GetTribeIcon(IsYokai && Model is YokaiCharabase yk ? yk.Tribe : -1);

        public void RefreshProperties()
        {
            OnPropertyChanged(nameof(Tribe));
            OnPropertyChanged(nameof(Rank));
            OnPropertyChanged(nameof(IsRare));
            OnPropertyChanged(nameof(IsLegend));
            OnPropertyChanged(nameof(RankIcon));
            OnPropertyChanged(nameof(TribeIcon));
            OnPropertyChanged(nameof(FavoriteFood));
            OnPropertyChanged(nameof(HatedFood));
        }
    }
    // Icon Helper
    // ??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??
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

