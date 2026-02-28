using ICN_T2.UI.WPF.ViewModels.Contracts;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using ICN_T2.Tools;
using ICN_T2.Logic.Level5.Text;
using T2bText = ICN_T2.Logic.Level5.Text.T2bþ;
using ICN_T2.YokaiWatch.Games.YW2.MapTools;

namespace ICN_T2.UI.WPF.ViewModels
{
    public class TreasureBoxItemViewModel : ReactiveObject
    {
        private readonly ItableDataMore _config;
        private readonly Action _onChanged;

        public TreasureBoxItemViewModel(ItableDataMore config, int index, Action onChanged)
        {
            _config = config;
            Index = index;
            _onChanged = onChanged;
        }

        public int Index { get; set; }

        public int ItemHash
        {
            get => _config.ItemHash;
            set { _config.ItemHash = value; this.RaisePropertyChanged(); _onChanged(); }
        }

        public int Quantity
        {
            get => _config.Quantity;
            set { _config.Quantity = value; this.RaisePropertyChanged(); _onChanged(); }
        }

        public int Percentage
        {
            get => _config.Percentage;
            set { _config.Percentage = value; this.RaisePropertyChanged(); _onChanged(); }
        }

        public int Unk1
        {
            get => _config.Unk1;
            set { _config.Unk1 = value; this.RaisePropertyChanged(); _onChanged(); }
        }

        public int Unk2
        {
            get => _config.Unk2;
            set { _config.Unk2 = value; this.RaisePropertyChanged(); _onChanged(); }
        }
    }

    public class MapTreasureBoxViewModel : ReactiveObject
    {
        public override string ToString() => MapName ?? RawMapName ?? base.ToString();
        private readonly IGame _game;
        private readonly Action _onChanged;

        public string MapName { get; set; }
        public string RawMapName { get; }
        public ObservableCollection<TreasureBoxItemViewModel> Boxes { get; } = new ObservableCollection<TreasureBoxItemViewModel>();
        public ItableDataMore[] RawData { get; private set; }

        public MapTreasureBoxViewModel(IGame game, string mapName, Action onChanged)
        {
            _game = game;
            RawMapName = mapName;
            MapName = mapName;
            _onChanged = onChanged;
        }

        public void Load()
        {
            RawData = _game.GetTreasureBox(RawMapName) ?? Array.Empty<ItableDataMore>();
            Boxes.Clear();
            for (int i = 0; i < RawData.Length; i++)
            {
                Boxes.Add(new TreasureBoxItemViewModel(RawData[i], i, _onChanged));
            }
        }

        public void Save()
        {
            if (RawData != null)
            {
                _game.SaveTreasureBox(RawMapName, RawData);
            }
        }
    }

    public class TreasureBoxViewModel : ReactiveObject, IToolSaveParticipant
    {
        private readonly IGame _game;
        private bool _isDirty;
        private T2bText? _itemNames;

        public ObservableCollection<MapTreasureBoxViewModel> Maps { get; } = new ObservableCollection<MapTreasureBoxViewModel>();
        public ObservableCollection<HashOption> ItemOptions { get; } = new ObservableCollection<HashOption>();

        private MapTreasureBoxViewModel _selectedMap;
        public MapTreasureBoxViewModel SelectedMap
        {
            get => _selectedMap;
            set
            {
                if (_selectedMap != value)
                {
                    _selectedMap = value;
                    if (_selectedMap != null && _selectedMap.Boxes.Count == 0)
                    {
                        _selectedMap.Load();
                    }
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool HasPendingChanges => _isDirty;

        public TreasureBoxViewModel(IGame game)
        {
            _game = game;
            LoadData();
        }

        private void LoadData()
        {
            _itemNames = LoadTextResource("item_text");

            ItemOptions.Clear();
            var items = _game.GetItems("all") ?? Array.Empty<ItemBase>();

            ItemOptions.Add(new HashOption(0x00, "None / Empty"));
            foreach (var item in items)
            {
                ItemOptions.Add(new HashOption(item.ItemHash, ResolveItemDisplayName(item)));
            }

            Maps.Clear();
            string[] mapNames = _game.GetMapWhoContainsTreasureBoxes() ?? Array.Empty<string>();

            // Load Map Names from Parser
            var mapListParser = new MapListParser();
            try
            {
                mapListParser.ParseAndLoad(MapListParser.GetDefaultMapList());
            }
            catch { }

            var tempMapViewModels = new List<MapTreasureBoxViewModel>();
            foreach (string mapName in mapNames)
            {
                string displayMapName = mapName;
                try
                {
                    Encoding shiftJis = Encoding.GetEncoding("Shift-JIS");
                    uint hash = Crc32.Compute(shiftJis.GetBytes(mapName));
                    string parsedName = mapListParser.GetMapName(unchecked((int)hash));
                    if (!string.IsNullOrWhiteSpace(parsedName))
                    {
                        displayMapName = $"{parsedName.Trim()} ({mapName})";
                    }
                }
                catch { }

                var mapVm = new MapTreasureBoxViewModel(_game, mapName, OnDataChanged);
                // Temporarily store the parsed map name in the MapName property if needed,
                // however MapTreasureBoxViewModel is currently constructed with 'mapName'.
                // To display it properly we should update the MapName property to hold the display string.
                mapVm.MapName = displayMapName;
                tempMapViewModels.Add(mapVm);
            }

            foreach (var sortedVm in tempMapViewModels.OrderBy(vm => vm.ToString()))
            {
                Maps.Add(sortedVm);
            }
            if (Maps.Any())
            {
                SelectedMap = Maps.First();
            }
        }

        private void OnDataChanged()
        {
            _isDirty = true;
            this.RaisePropertyChanged(nameof(HasPendingChanges));
        }

        public bool SavePendingChanges()
        {
            try
            {
                if (!_isDirty) return true;
                foreach (var map in Maps)
                {
                    if (map.Boxes.Count > 0)
                    {
                        map.Save();
                    }
                }
                _isDirty = false;
                this.RaisePropertyChanged(nameof(HasPendingChanges));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TreasureBoxVM] Save error: {ex.Message}");
                return false;
            }
        }

        public void RevertChanges()
        {
            LoadData();
            _isDirty = false;
            this.RaisePropertyChanged(nameof(HasPendingChanges));
        }

        // T2bText parser logic copied from ShopViewModel / YokaiStatsViewModel
        private T2bText? LoadTextResource(string key)
        {
            try
            {
                if (_game.Files == null || !_game.Files.TryGetValue(key, out var file))
                    return null;

                var vf = file.GetStream();
                if (vf == null) return null;

                byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                if (data == null || data.Length == 0) return null;

                return new T2bText(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TreasureBoxVM] Failed to load text resource '{key}': {ex.Message}");
                return null;
            }
        }

        private static bool TryResolveNoun(T2bText? textObj, int hash, out string name)
        {
            name = string.Empty;
            if (textObj?.Nouns == null) return false;

            if (!textObj.Nouns.TryGetValue(hash, out var cfg) || cfg.Strings == null || cfg.Strings.Count == 0)
                return false;

            var first = cfg.Strings[0].Text;
            if (string.IsNullOrWhiteSpace(first))
                return false;

            name = first;
            return true;
        }

        private string ResolveItemDisplayName(ItemBase item)
        {
            if (TryResolveNoun(_itemNames, item.NameHash, out string name))
            {
                return name;
            }
            return "Unknown (0x" + item.ItemHash.ToString("X8") + ")";
        }
    }
}
