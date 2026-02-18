using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using ICN_T2.Logic.Level5.Text;
using ICN_T2.Tools;
using ReactiveUI;
using ICN_T2.UI.WPF.ViewModels.Contracts;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;
using ICN_T2.YokaiWatch.Games.YW2.MapTools;
using YW2Logic = ICN_T2.YokaiWatch.Games.YW2.Logic;

namespace ICN_T2.UI.WPF.ViewModels
{
    public class MapEntry
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Id { get; set; } = "";

        public string DisplayName =>
            string.IsNullOrWhiteSpace(Name) || string.Equals(Name, Id, StringComparison.OrdinalIgnoreCase)
                ? Id
                : $"{Name} ({Id})";

        public override string ToString() => DisplayName;
    }

    public class YokaiReference
    {
        public string Name { get; set; } = "";
        public int ParamHash { get; set; }

        public override string ToString() => string.IsNullOrWhiteSpace(Name) ? "Unknown" : Name;
    }

    public sealed class EncounterTableEntry
    {
        public EncounterTableEntry(EncountTable table, string displayName)
        {
            Table = table;
            DisplayName = displayName;
        }

        public EncountTable Table { get; }
        public string DisplayName { get; }

        public override string ToString() => DisplayName;
    }

    public sealed class EncounterSlotCardViewModel : ReactiveObject
    {
        private readonly EncountSlot _slot;
        private readonly IReadOnlyList<YokaiReference> _yokaiNames;
        private readonly Action _onChanged;

        public EncounterSlotCardViewModel(
            EncountSlot slot,
            int slotIndex,
            IReadOnlyList<YokaiReference> yokaiNames,
            Action onChanged)
        {
            _slot = slot;
            SlotIndex = slotIndex;
            _yokaiNames = yokaiNames;
            _onChanged = onChanged;
        }

        public int SlotIndex { get; }

        public int ParamHash
        {
            get => _slot.ParamHash;
            set
            {
                if (_slot.ParamHash == value)
                {
                    return;
                }

                _slot.ParamHash = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(Name));
                _onChanged();
            }
        }

        public string Name =>
            _yokaiNames.FirstOrDefault(x => x.ParamHash == ParamHash)?.Name ?? "Unknown";

        public int Level
        {
            get => GetSlotLevel();
            set
            {
                if (GetSlotLevel() == value)
                {
                    return;
                }

                SetSlotLevel(value);
                this.RaisePropertyChanged();
                _onChanged();
            }
        }

        public int Weight
        {
            get => GetSlotWeight();
            set
            {
                if (GetSlotWeight() == value)
                {
                    return;
                }

                SetSlotWeight(value);
                this.RaisePropertyChanged();
                _onChanged();
            }
        }

        private int GetSlotLevel()
        {
            return _slot switch
            {
                YW2Logic.EncountChara encountChara => encountChara.Level,
                YW2Logic.YokaiSpotChara yokaiSpotChara => yokaiSpotChara.Level,
                _ => _slot.Level
            };
        }

        private void SetSlotLevel(int value)
        {
            switch (_slot)
            {
                case YW2Logic.EncountChara encountChara:
                    encountChara.Level = value;
                    break;
                case YW2Logic.YokaiSpotChara yokaiSpotChara:
                    yokaiSpotChara.Level = value;
                    break;
                default:
                    _slot.Level = value;
                    break;
            }
        }

        private int GetSlotWeight()
        {
            return _slot switch
            {
                YW2Logic.EncountChara encountChara => encountChara.Weight,
                YW2Logic.YokaiSpotChara yokaiSpotChara => yokaiSpotChara.Weight,
                _ => _slot.Weight
            };
        }

        private void SetSlotWeight(int value)
        {
            switch (_slot)
            {
                case YW2Logic.EncountChara encountChara:
                    encountChara.Weight = value;
                    break;
                case YW2Logic.YokaiSpotChara yokaiSpotChara:
                    yokaiSpotChara.Weight = value;
                    break;
                default:
                    _slot.Weight = value;
                    break;
            }
        }
    }

    public class EncounterViewModel : ReactiveObject, IToolSaveParticipant, ISelectiveToolSaveParticipant
    {
        private sealed class EncounterMapState
        {
            public EncountTable[] Tables { get; init; } = Array.Empty<EncountTable>();
            public EncountSlot[] Slots { get; init; } = Array.Empty<EncountSlot>();
            public bool Dirty { get; set; }
        }

        private readonly IGame _game;
        private readonly Dictionary<string, string> _mapNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EncounterMapState> _mapStateCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _systemTextMap = new();

        private MapListParser? _mapListParser;
        private bool _mapNameResolverInitialized;

        private EncountSlot[] _currentSlots = Array.Empty<EncountSlot>();

        private string? _selectedMap;
        private EncounterTableEntry? _selectedTableEntry;
        private string _tableSearchText = "";
        private bool _hasPendingChanges;

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveCommand { get; }

        public ObservableCollection<MapEntry> Maps { get; } = new();
        public ObservableCollection<EncounterTableEntry> TableEntries { get; } = new();
        public ObservableCollection<EncounterTableEntry> FilteredTableEntries { get; } = new();
        public ObservableCollection<EncounterSlotCardViewModel> SlotCards { get; } = new();
        public List<YokaiReference> YokaiNames { get; } = new();

        public string? SelectedMap
        {
            get => _selectedMap;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedMap, value);
                this.RaisePropertyChanged(nameof(SelectedMapDisplayName));
            }
        }

        public string SelectedMapDisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_selectedMap))
                {
                    return "No map selected";
                }

                return _mapNames.TryGetValue(_selectedMap, out var display)
                    ? display
                    : _selectedMap;
            }
        }

        public EncounterTableEntry? SelectedTableEntry
        {
            get => _selectedTableEntry;
            set => this.RaiseAndSetIfChanged(ref _selectedTableEntry, value);
        }

        public string TableSearchText
        {
            get => _tableSearchText;
            set
            {
                this.RaiseAndSetIfChanged(ref _tableSearchText, value);
                ApplyTableFilter();
            }
        }

        public bool HasPendingChanges
        {
            get => _hasPendingChanges;
            private set => this.RaiseAndSetIfChanged(ref _hasPendingChanges, value);
        }

        public string ToolId => "encounter";
        public string ToolDisplayName => "Encounter";

        public EncounterViewModel(IGame game)
        {
            _game = game;
            SaveCommand = ReactiveCommand.Create(() => { SavePendingChanges(); });

            LoadYokaiNames();
            LoadMaps();

            this.WhenAnyValue(x => x.SelectedMap)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Subscribe(x => LoadEncounter(x!));

            this.WhenAnyValue(x => x.SelectedTableEntry)
                .Subscribe(x => UpdateSlotCards(x?.Table));

            if (Maps.Count > 0)
            {
                SelectedMap = Maps[0].Path;
            }
        }

        private void LoadYokaiNames()
        {
            try
            {
                YokaiNames.Clear();

                var charaparams = _game.GetCharaparam();
                var charabases = _game.GetCharacterbase(true);
                if (charaparams == null || charabases == null)
                {
                    return;
                }

                var nameMap = new Dictionary<int, string>();
                if (_game is YW2 yw2)
                {
                    nameMap = yw2.GetCharaNameMap();
                }

                var encounteredNames = new HashSet<string>();
                var nameOccurrences = new Dictionary<string, int>();

                foreach (var charaparam in charaparams)
                {
                    string name = "Unknown";
                    var searchCharabase = charabases.FirstOrDefault(x => x.BaseHash == charaparam.BaseHash);
                    if (searchCharabase != null &&
                        nameMap.TryGetValue(searchCharabase.NameHash, out var mappedName))
                    {
                        name = mappedName;

                        if (encounteredNames.Contains(name))
                        {
                            int count = nameOccurrences.TryGetValue(name, out var existing) ? existing + 1 : 2;
                            nameOccurrences[name] = count;
                            name = $"{name} {count}";
                        }

                        encounteredNames.Add(name);
                    }

                    YokaiNames.Add(new YokaiReference
                    {
                        ParamHash = charaparam.ParamHash,
                        Name = name
                    });
                }

                YokaiNames.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EncounterViewModel] Yokai name load error: {ex.Message}");
            }
        }

        private void LoadMaps()
        {
            Maps.Clear();
            _mapNames.Clear();
            EnsureMapNameResolver();

            string[] filenames = _game.GetMapWhoContainsEncounter();
            var nameCounter = new Dictionary<string, int>();

            for (int i = 0; i < filenames.Length; i++)
            {
                string filename = filenames[i];
                if (string.IsNullOrWhiteSpace(filename))
                {
                    continue;
                }

                if (string.Equals(filename, "yokaispot", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string resolvedName = ResolveMapName(filename);

                if (nameCounter.TryGetValue(resolvedName, out int duplicateCount))
                {
                    duplicateCount++;
                    nameCounter[resolvedName] = duplicateCount;
                    resolvedName = $"{resolvedName} {duplicateCount}";
                }
                else
                {
                    nameCounter[resolvedName] = 1;
                }

                var mapEntry = new MapEntry
                {
                    Path = filename,
                    Name = resolvedName,
                    Id = filename
                };

                _mapNames[filename] = mapEntry.DisplayName;
                Maps.Add(mapEntry);
            }
        }

        private void EnsureMapNameResolver()
        {
            if (_mapNameResolverInitialized)
            {
                return;
            }

            _mapNameResolverInitialized = true;
            _systemTextMap.Clear();

            try
            {
                if (_game.Files != null &&
                    _game.Files.TryGetValue("system_text", out var systemTextGameFile))
                {
                    var vf = systemTextGameFile.GetStream();
                    byte[]? data = vf?.ByteContent ?? vf?.ReadWithoutCaching();
                    if (data != null && data.Length > 0)
                    {
                        var systemText = new T2bþ(data);
                        foreach (var kv in systemText.Texts)
                        {
                            if (kv.Value.Strings != null &&
                                kv.Value.Strings.Count > 0 &&
                                !string.IsNullOrWhiteSpace(kv.Value.Strings[0].Text))
                            {
                                _systemTextMap[kv.Key] = kv.Value.Strings[0].Text;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EncounterViewModel] system_text parse error: {ex.Message}");
            }

            try
            {
                _mapListParser = new MapListParser();
                _mapListParser.ParseAndLoad(MapListParser.GetDefaultMapList());
            }
            catch (Exception ex)
            {
                _mapListParser = null;
                System.Diagnostics.Debug.WriteLine($"[EncounterViewModel] map parser init error: {ex.Message}");
            }
        }

        private string ResolveMapName(string mapId)
        {
            if (string.Equals(mapId, "common_enc", StringComparison.OrdinalIgnoreCase))
            {
                return "Common Encounter";
            }

            int hash = ComputeShiftJisCrc32(mapId);
            if (_systemTextMap.TryGetValue(hash, out string? systemName) &&
                !string.IsNullOrWhiteSpace(systemName))
            {
                return systemName.Trim();
            }

            string? parsedName = _mapListParser?.GetMapName(hash);
            if (!string.IsNullOrWhiteSpace(parsedName))
            {
                return parsedName.Trim();
            }

            return mapId;
        }

        private static int ComputeShiftJisCrc32(string text)
        {
            try
            {
                Encoding shiftJis = Encoding.GetEncoding("Shift-JIS");
                uint hash = Crc32.Compute(shiftJis.GetBytes(text));
                return unchecked((int)hash);
            }
            catch
            {
                uint hash = Crc32.Compute(Encoding.UTF8.GetBytes(text));
                return unchecked((int)hash);
            }
        }

        private bool IsKnownMapKey(string mapKey)
        {
            return Maps.Any(m => string.Equals(m.Path, mapKey, StringComparison.OrdinalIgnoreCase));
        }

        private void LoadEncounter(string mapKey)
        {
            mapKey = mapKey.Trim();
            if (!IsKnownMapKey(mapKey))
            {
                System.Diagnostics.Debug.WriteLine($"[EncounterViewModel] Invalid map key '{mapKey}'.");
                return;
            }

            if (!_mapStateCache.TryGetValue(mapKey, out var state))
            {
                var (tables, slots) = _game.GetMapEncounter(mapKey);
                state = new EncounterMapState
                {
                    Tables = tables ?? Array.Empty<EncountTable>(),
                    Slots = slots ?? Array.Empty<EncountSlot>(),
                    Dirty = false
                };
                _mapStateCache[mapKey] = state;
            }

            _currentSlots = state.Slots;
            RebuildTableEntries(state.Tables);
            RecomputePendingChanges();
        }

        private void RebuildTableEntries(EncountTable[] tables)
        {
            TableEntries.Clear();
            for (int i = 0; i < tables.Length; i++)
            {
                var table = tables[i];
                int validSlotCount = 0;
                if (table.EncountOffsets != null)
                {
                    foreach (int offset in table.EncountOffsets)
                    {
                        if (offset >= 0 &&
                            offset < _currentSlots.Length &&
                            !IsYokaiSpotSlot(_currentSlots[offset]))
                        {
                            validSlotCount++;
                        }
                    }
                }

                string label = $"Table {i + 1} - Slots {validSlotCount}";
                TableEntries.Add(new EncounterTableEntry(table, label));
            }

            ApplyTableFilter();
        }

        private void ApplyTableFilter()
        {
            var previousSelection = SelectedTableEntry;
            string search = TableSearchText?.Trim() ?? string.Empty;

            FilteredTableEntries.Clear();
            foreach (var entry in TableEntries)
            {
                if (string.IsNullOrEmpty(search) ||
                    entry.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    FilteredTableEntries.Add(entry);
                }
            }

            if (previousSelection != null && FilteredTableEntries.Contains(previousSelection))
            {
                SelectedTableEntry = previousSelection;
                return;
            }

            SelectedTableEntry = FilteredTableEntries.FirstOrDefault();
        }

        private void UpdateSlotCards(EncountTable? table)
        {
            SlotCards.Clear();
            if (table?.EncountOffsets == null)
            {
                return;
            }

            foreach (int offset in table.EncountOffsets)
            {
                if (offset < 0 || offset >= _currentSlots.Length)
                {
                    continue;
                }

                EncountSlot slot = _currentSlots[offset];
                if (IsYokaiSpotSlot(slot))
                {
                    continue;
                }

                SlotCards.Add(new EncounterSlotCardViewModel(slot, offset, YokaiNames, MarkCurrentMapDirty));
            }
        }

        private static bool IsYokaiSpotSlot(EncountSlot slot)
        {
            return slot is ICN_T2.YokaiWatch.Games.YW2.Logic.YokaiSpotChara;
        }

        private void MarkCurrentMapDirty()
        {
            if (string.IsNullOrWhiteSpace(_selectedMap))
            {
                return;
            }

            if (_mapStateCache.TryGetValue(_selectedMap, out var state))
            {
                state.Dirty = true;
                RecomputePendingChanges();
            }
        }

        private void RecomputePendingChanges()
        {
            HasPendingChanges = _mapStateCache.Values.Any(x => x.Dirty);
        }

        public bool SavePendingChanges()
        {
            var allDirtyKeys = _mapStateCache
                .Where(x => x.Value.Dirty)
                .Select(x => x.Key)
                .ToArray();
            var result = SavePendingChanges(allDirtyKeys);
            return result.SavedChangeIds.Count > 0;
        }

        public IReadOnlyList<ToolPendingChange> GetPendingChanges()
        {
            return _mapStateCache
                .Where(x => x.Value.Dirty)
                .Select(x =>
                {
                    string display = _mapNames.TryGetValue(x.Key, out var resolvedName)
                        ? resolvedName
                        : x.Key;

                    return new ToolPendingChange(
                        changeId: x.Key,
                        displayName: display,
                        description: $"Map: {x.Key}");
                })
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

            foreach (string mapKey in changeIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!_mapStateCache.TryGetValue(mapKey, out var state))
                {
                    continue;
                }

                if (!state.Dirty)
                {
                    continue;
                }

                try
                {
                    _game.SaveMapEncounter(mapKey, state.Tables, state.Slots);
                    state.Dirty = false;
                    savedIds.Add(mapKey);
                }
                catch (Exception ex)
                {
                    failed[mapKey] = ex.Message;
                    System.Diagnostics.Debug.WriteLine($"[EncounterViewModel] Save failed for map '{mapKey}': {ex.Message}");
                }
            }

            RecomputePendingChanges();
            return new ToolSaveBatchResult(savedIds, failed);
        }

        public void Save()
        {
            SavePendingChanges();
        }
    }
}
