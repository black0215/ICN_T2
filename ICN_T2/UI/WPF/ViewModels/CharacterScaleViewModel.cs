using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2.Logic;
using ICN_T2.Logic.Level5.Text;
using ICN_T2.UI.WPF.ViewModels.Contracts;

namespace ICN_T2.UI.WPF.ViewModels
{
    public class CharacterScaleWrapper : INotifyPropertyChanged
    {
        private readonly CharScale _model;
        private readonly CharaBase? _baseInfo;
        private readonly string _name;
        private readonly Action<int>? _onChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        public CharacterScaleWrapper(CharScale model, CharaBase? baseInfo, string name, Action<int>? onChanged = null)
        {
            _model = model;
            _baseInfo = baseInfo;
            _name = name;
            _onChanged = onChanged;
        }

        public CharScale Model => _model;
        public CharaBase? BaseInfo => _baseInfo;

        public string Name => _name;
        public int BaseHash => _model.BaseHash;

        // Rank/Tribe Icon for list display
        public BitmapImage? RankIcon => _baseInfo is YokaiCharabase yk ? IconCache.GetRankIcon(yk.Rank) : null;
        public BitmapImage? TribeIcon => _baseInfo is YokaiCharabase yk ? IconCache.GetTribeIcon(yk.Tribe) : null;

        // Scale Properties
        public float Scale1
        {
            get => _model.Scale1;
            set
            {
                if (_model.Scale1 != value)
                {
                    _model.Scale1 = value;
                    OnPropertyChanged();
                    _onChanged?.Invoke(_model.BaseHash);
                }
            }
        }

        public float Scale2
        {
            get => _model.Scale2;
            set
            {
                if (_model.Scale2 != value)
                {
                    _model.Scale2 = value;
                    OnPropertyChanged();
                    _onChanged?.Invoke(_model.BaseHash);
                }
            }
        }

        public float Scale3
        {
            get => _model.Scale3;
            set
            {
                if (_model.Scale3 != value)
                {
                    _model.Scale3 = value;
                    OnPropertyChanged();
                    _onChanged?.Invoke(_model.BaseHash);
                }
            }
        }

        public float Scale4
        {
            get => _model.Scale4;
            set
            {
                if (_model.Scale4 != value)
                {
                    _model.Scale4 = value;
                    OnPropertyChanged();
                    _onChanged?.Invoke(_model.BaseHash);
                }
            }
        }

        public float Scale5
        {
            get => _model.Scale5;
            set
            {
                if (_model.Scale5 != value)
                {
                    _model.Scale5 = value;
                    OnPropertyChanged();
                    _onChanged?.Invoke(_model.BaseHash);
                }
            }
        }

        public float Scale6
        {
            get => _model.Scale6;
            set
            {
                if (_model.Scale6 != value)
                {
                    _model.Scale6 = value;
                    OnPropertyChanged();
                    _onChanged?.Invoke(_model.BaseHash);
                }
            }
        }

        // Check if Scale7 exists on the model type (it's in base CharScale but maybe not in derived YW2 Charascale if new keyword used incorrectly?)
        // The search result showed Charascale : CharScale with new properties for 1-6. Scale7 was in base.
        public float Scale7
        {
            get => _model.Scale7;
            set
            {
                if (_model.Scale7 != value)
                {
                    _model.Scale7 = value;
                    OnPropertyChanged();
                    _onChanged?.Invoke(_model.BaseHash);
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CharacterScaleViewModel : INotifyPropertyChanged, IToolSaveParticipant, ISelectiveToolSaveParticipant
    {
        private readonly IGame _game;
        private ObservableCollection<CharacterScaleWrapper> _allScales;
        private CharacterScaleWrapper? _selectedScale;
        private string _searchText = "";
        private Dictionary<int, string> _nameMap = new();
        private readonly Dictionary<int, CharScale> _committedSnapshotByBaseHash = new();
        private readonly HashSet<int> _dirtyBaseHashes = new();
        private bool _hasPendingChanges;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ICommand SaveChangesCommand { get; }

        public bool HasPendingChanges
        {
            get => _hasPendingChanges;
            private set
            {
                if (_hasPendingChanges != value)
                {
                    _hasPendingChanges = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ToolId => "char_scale";
        public string ToolDisplayName => "Character Scale";

        public CharacterScaleViewModel(IGame game)
        {
            _game = game;
            _allScales = new ObservableCollection<CharacterScaleWrapper>();
            FilteredList = new ObservableCollection<CharacterScaleWrapper>();
            SaveChangesCommand = new RelayCommand(ExecuteSaveChanges);

            try
            {
                LoadData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterScaleViewModel] Error loading data: {ex.Message}");
                // Ensure list is not null even if loading fails
                if (_allScales == null) _allScales = new ObservableCollection<CharacterScaleWrapper>();
                if (FilteredList == null) FilteredList = new ObservableCollection<CharacterScaleWrapper>();
            }
        }

        private void LoadData()
        {
            System.Diagnostics.Trace.WriteLine("[CharacterScaleViewModel] LoadData 시작");

            // 1. Load Names
            LoadCharaNames();
            System.Diagnostics.Trace.WriteLine($"[CharacterScaleViewModel] Name map loaded: {_nameMap.Count} entries");

            // 2. Load Base Info Map
            var bases = _game.GetCharacterbase(true).Concat(_game.GetCharacterbase(false));
            var baseMap = new Dictionary<int, CharaBase>();
            foreach (var b in bases)
            {
                if (!baseMap.ContainsKey(b.BaseHash))
                    baseMap[b.BaseHash] = b;
            }
            System.Diagnostics.Trace.WriteLine($"[CharacterScaleViewModel] Base map created: {baseMap.Count} entries");

            // 3. Load Scales
            var scales = _game.GetCharascale();
            System.Diagnostics.Trace.WriteLine($"[CharacterScaleViewModel] GetCharascale returned: {scales.Length} scales");
            _allScales.Clear();
            _committedSnapshotByBaseHash.Clear();
            _dirtyBaseHashes.Clear();
            HasPendingChanges = false;

            foreach (var s in scales)
            {
                baseMap.TryGetValue(s.BaseHash, out var baseInfo);

                string name = "Unknown";
                if (baseInfo != null && _nameMap.TryGetValue(baseInfo.NameHash, out var mappedName))
                {
                    name = mappedName;
                }
                else if (_nameMap.TryGetValue(s.BaseHash, out var directName)) // Sometimes hash matches directly?
                {
                    name = directName;
                }
                else
                {
                    name = $"Unknown ({s.BaseHash:X8})";
                }

                _allScales.Add(new CharacterScaleWrapper(s, baseInfo, name, MarkDirty));
                _committedSnapshotByBaseHash[s.BaseHash] = CloneScale(s);
            }
            System.Diagnostics.Trace.WriteLine($"[CharacterScaleViewModel] _allScales populated: {_allScales.Count} items");

            ApplyFilter();
            System.Diagnostics.Trace.WriteLine("[CharacterScaleViewModel] LoadData 완료");
        }

        private void LoadCharaNames()
        {
            _nameMap.Clear();
            try
            {
                // Try cached name map from YW2 (avoids re-parsing chara_text)
                if (_game is ICN_T2.YokaiWatch.Games.YW2.YW2 yw2)
                {
                    _nameMap = new Dictionary<int, string>(yw2.GetCharaNameMap());
                    return;
                }

                // Fallback: parse chara_text directly
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CharacterScaleViewModel] Error loading names: {ex.Message}");
            }
        }

        public ObservableCollection<CharacterScaleWrapper> FilteredList { get; private set; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    ApplyFilter();
                }
            }
        }

        public CharacterScaleWrapper? SelectedScale
        {
            get => _selectedScale;
            set
            {
                if (_selectedScale != value)
                {
                    _selectedScale = value;
                    OnPropertyChanged();
                }
            }
        }

        private void ApplyFilter()
        {
            System.Diagnostics.Trace.WriteLine($"[CharacterScaleViewModel] ApplyFilter 시작: _allScales={_allScales.Count}, searchText='{_searchText}'");

            var previousSelection = _selectedScale;
            FilteredList.Clear();
            var searchLower = _searchText.ToLower();

            foreach (var item in _allScales)
            {
                if (string.IsNullOrWhiteSpace(_searchText))
                {
                    FilteredList.Add(item);
                    continue;
                }

                if (item.Name.ToLower().Contains(searchLower) ||
                    item.BaseHash.ToString("X8").ToLower().Contains(searchLower))
                {
                    FilteredList.Add(item);
                }
            }

            System.Diagnostics.Trace.WriteLine($"[CharacterScaleViewModel] FilteredList populated: {FilteredList.Count} items");

            if (previousSelection != null && FilteredList.Contains(previousSelection))
            {
                if (!ReferenceEquals(_selectedScale, previousSelection))
                {
                    SelectedScale = previousSelection;
                }
                System.Diagnostics.Trace.WriteLine($"[CharacterScaleViewModel] Previous selection restored: {previousSelection.Name}");
                return;
            }

            if (FilteredList.Count > 0)
            {
                SelectedScale = FilteredList[0];
                System.Diagnostics.Trace.WriteLine($"[CharacterScaleViewModel] Auto-selected first item: {FilteredList[0].Name}");
            }
            else
            {
                SelectedScale = null;
                System.Diagnostics.Trace.WriteLine("[CharacterScaleViewModel] No items to select, SelectedScale = null");
            }
        }

        private void ExecuteSaveChanges(object? obj)
        {
            SavePendingChanges();
        }

        private void MarkDirty(int baseHash)
        {
            if (!_committedSnapshotByBaseHash.TryGetValue(baseHash, out var snapshot))
            {
                _dirtyBaseHashes.Add(baseHash);
                HasPendingChanges = _dirtyBaseHashes.Count > 0;
                return;
            }

            var wrapper = _allScales.FirstOrDefault(x => x.BaseHash == baseHash);
            if (wrapper == null)
            {
                return;
            }

            if (ScaleEquals(wrapper.Model, snapshot))
            {
                _dirtyBaseHashes.Remove(baseHash);
            }
            else
            {
                _dirtyBaseHashes.Add(baseHash);
            }

            HasPendingChanges = _dirtyBaseHashes.Count > 0;
        }

        public bool SavePendingChanges()
        {
            if (!HasPendingChanges)
            {
                return false;
            }

            var allDirtyIds = _dirtyBaseHashes
                .Select(x => x.ToString("X8"))
                .ToArray();
            var result = SavePendingChanges(allDirtyIds);
            return result.SavedChangeIds.Count > 0;
        }

        public IReadOnlyList<ToolPendingChange> GetPendingChanges()
        {
            return _allScales
                .Where(x => _dirtyBaseHashes.Contains(x.BaseHash))
                .Select(x => new ToolPendingChange(
                    changeId: x.BaseHash.ToString("X8"),
                    displayName: x.Name,
                    description: $"BaseHash: 0x{x.BaseHash:X8}"))
                .ToArray();
        }

        public ToolSaveBatchResult SavePendingChanges(IReadOnlyCollection<string> changeIds)
        {
            var savedIds = new List<string>();
            var failed = new Dictionary<string, string>();
            if (changeIds == null || changeIds.Count == 0)
            {
                return new ToolSaveBatchResult(savedIds, failed);
            }

            var requestedHashes = new HashSet<int>();
            foreach (string rawId in changeIds)
            {
                if (int.TryParse(rawId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
                {
                    requestedHashes.Add(hex);
                    continue;
                }

                if (int.TryParse(rawId, out int dec))
                {
                    requestedHashes.Add(dec);
                }
            }

            if (requestedHashes.Count == 0)
            {
                return new ToolSaveBatchResult(savedIds, failed);
            }

            var toSave = new CharScale[_allScales.Count];
            for (int i = 0; i < _allScales.Count; i++)
            {
                var wrapper = _allScales[i];
                bool selected = requestedHashes.Contains(wrapper.BaseHash);
                if (selected)
                {
                    toSave[i] = CloneScale(wrapper.Model);
                }
                else if (_committedSnapshotByBaseHash.TryGetValue(wrapper.BaseHash, out var snapshot))
                {
                    toSave[i] = CloneScale(snapshot);
                }
                else
                {
                    toSave[i] = CloneScale(wrapper.Model);
                }
            }

            try
            {
                _game.SaveCharascale(toSave);

                foreach (int hash in requestedHashes)
                {
                    var wrapper = _allScales.FirstOrDefault(x => x.BaseHash == hash);
                    if (wrapper == null)
                    {
                        continue;
                    }

                    _committedSnapshotByBaseHash[hash] = CloneScale(wrapper.Model);
                    _dirtyBaseHashes.Remove(hash);
                    savedIds.Add(hash.ToString("X8"));
                }

                HasPendingChanges = _dirtyBaseHashes.Count > 0;
                System.Diagnostics.Trace.WriteLine($"[CharacterScaleViewModel] Saved selected changes: {savedIds.Count}");
            }
            catch (Exception ex)
            {
                string reason = ex.Message;
                foreach (int hash in requestedHashes)
                {
                    failed[hash.ToString("X8")] = reason;
                }
            }

            return new ToolSaveBatchResult(savedIds, failed);
        }

        private static CharScale CloneScale(CharScale source)
        {
            return new CharScale
            {
                BaseHash = source.BaseHash,
                Scale1 = source.Scale1,
                Scale2 = source.Scale2,
                Scale3 = source.Scale3,
                Scale4 = source.Scale4,
                Scale5 = source.Scale5,
                Scale6 = source.Scale6,
                Scale7 = source.Scale7
            };
        }

        private static bool ScaleEquals(CharScale left, CharScale right)
        {
            return left.BaseHash == right.BaseHash &&
                   left.Scale1.Equals(right.Scale1) &&
                   left.Scale2.Equals(right.Scale2) &&
                   left.Scale3.Equals(right.Scale3) &&
                   left.Scale4.Equals(right.Scale4) &&
                   left.Scale5.Equals(right.Scale5) &&
                   left.Scale6.Equals(right.Scale6) &&
                   left.Scale7.Equals(right.Scale7);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
