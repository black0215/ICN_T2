using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.Logic.Level5.Text;

namespace ICN_T2.UI.WPF.ViewModels
{
    public class YokaiStatsWrapper : INotifyPropertyChanged
    {
        private readonly YokaiStats _model;
        private readonly CharaBase? _baseInfo;
        private readonly string _name;

        public event PropertyChangedEventHandler? PropertyChanged;

        public YokaiStatsWrapper(YokaiStats model, CharaBase? baseInfo, string name)
        {
            _model = model;
            _baseInfo = baseInfo;
            _name = name;
        }

        public YokaiStats Model => _model;
        public string Name => _name;
        public int BaseHash => _model.BaseHash;
        public int ParamHash => _model.ParamHash;

        public BitmapImage? Icon => null; // Placeholder, handled in View

        public BitmapImage? RankIcon => _baseInfo is ICN_T2.YokaiWatch.Games.YW2.Logic.YokaiCharabase yk ? ICN_T2.UI.WPF.ViewModels.IconCache.GetRankIcon(yk.Rank) : null;
        public BitmapImage? TribeIcon => _baseInfo is ICN_T2.YokaiWatch.Games.YW2.Logic.YokaiCharabase yk ? ICN_T2.UI.WPF.ViewModels.IconCache.GetTribeIcon(yk.Tribe) : null;

        // Basic Info
        public int Tribe
        {
            get => _model.Tribe;
            set { if (_model.Tribe != value) { _model.Tribe = value; OnPropertyChanged(); } }
        }

        // Stats (Min/Max)
        public int MinHP { get => _model.MinHP; set { if (_model.MinHP != value) { _model.MinHP = value; OnPropertyChanged(); } } }
        public int MaxHP { get => _model.MaxHP; set { if (_model.MaxHP != value) { _model.MaxHP = value; OnPropertyChanged(); } } }
        public int MinStrength { get => _model.MinStrength; set { if (_model.MinStrength != value) { _model.MinStrength = value; OnPropertyChanged(); } } }
        public int MaxStrength { get => _model.MaxStrength; set { if (_model.MaxStrength != value) { _model.MaxStrength = value; OnPropertyChanged(); } } }
        public int MinSpirit { get => _model.MinSpirit; set { if (_model.MinSpirit != value) { _model.MinSpirit = value; OnPropertyChanged(); } } }
        public int MaxSpirit { get => _model.MaxSpirit; set { if (_model.MaxSpirit != value) { _model.MaxSpirit = value; OnPropertyChanged(); } } }
        public int MinDefense { get => _model.MinDefense; set { if (_model.MinDefense != value) { _model.MinDefense = value; OnPropertyChanged(); } } }
        public int MaxDefense { get => _model.MaxDefense; set { if (_model.MaxDefense != value) { _model.MaxDefense = value; OnPropertyChanged(); } } }
        public int MinSpeed { get => _model.MinSpeed; set { if (_model.MinSpeed != value) { _model.MinSpeed = value; OnPropertyChanged(); } } }
        public int MaxSpeed { get => _model.MaxSpeed; set { if (_model.MaxSpeed != value) { _model.MaxSpeed = value; OnPropertyChanged(); } } }

        // Battle Attributes
        public float AttributeDamageFire { get => _model.AttributeDamageFire; set { if (_model.AttributeDamageFire != value) { _model.AttributeDamageFire = value; OnPropertyChanged(); } } }
        public float AttributeDamageIce { get => _model.AttributeDamageIce; set { if (_model.AttributeDamageIce != value) { _model.AttributeDamageIce = value; OnPropertyChanged(); } } }
        public float AttributeDamageEarth { get => _model.AttributeDamageEarth; set { if (_model.AttributeDamageEarth != value) { _model.AttributeDamageEarth = value; OnPropertyChanged(); } } }
        public float AttributeDamageLigthning { get => _model.AttributeDamageLigthning; set { if (_model.AttributeDamageLigthning != value) { _model.AttributeDamageLigthning = value; OnPropertyChanged(); } } }
        public float AttributeDamageWater { get => _model.AttributeDamageWater; set { if (_model.AttributeDamageWater != value) { _model.AttributeDamageWater = value; OnPropertyChanged(); } } }
        public float AttributeDamageWind { get => _model.AttributeDamageWind; set { if (_model.AttributeDamageWind != value) { _model.AttributeDamageWind = value; OnPropertyChanged(); } } }

        // Drops
        public int Money { get => _model.Money; set { if (_model.Money != value) { _model.Money = value; OnPropertyChanged(); } } }
        public int Experience { get => _model.Experience; set { if (_model.Experience != value) { _model.Experience = value; OnPropertyChanged(); } } }
        public int Drop1Hash { get => _model.Drop1Hash; set { if (_model.Drop1Hash != value) { _model.Drop1Hash = value; OnPropertyChanged(); } } }
        public int Drop1Rate { get => _model.Drop1Rate; set { if (_model.Drop1Rate != value) { _model.Drop1Rate = value; OnPropertyChanged(); } } }
        public int Drop2Hash { get => _model.Drop2Hash; set { if (_model.Drop2Hash != value) { _model.Drop2Hash = value; OnPropertyChanged(); } } }
        public int Drop2Rate { get => _model.Drop2Rate; set { if (_model.Drop2Rate != value) { _model.Drop2Rate = value; OnPropertyChanged(); } } }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class YokaiStatsViewModel : INotifyPropertyChanged
    {
        private readonly IGame _game;
        private ObservableCollection<YokaiStatsWrapper> _allStats;
        private YokaiStatsWrapper? _selectedStats;
        private string _searchText = "";
        private Dictionary<int, string> _nameMap = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        public ICommand SaveChangesCommand { get; }

        public YokaiStatsViewModel(IGame game)
        {
            _game = game;
            _allStats = new ObservableCollection<YokaiStatsWrapper>();
            FilteredList = new ObservableCollection<YokaiStatsWrapper>();
            SaveChangesCommand = new RelayCommand(ExecuteSaveChanges);

            LoadData();
        }

        private void LoadData()
        {
            LoadCharaNames();

            var bases = _game.GetCharacterbase(true).Concat(_game.GetCharacterbase(false));
            var baseMap = new Dictionary<int, CharaBase>();
            foreach (var b in bases)
            {
                if (!baseMap.ContainsKey(b.BaseHash))
                    baseMap[b.BaseHash] = b;
            }

            var stats = _game.GetCharaparam();
            _allStats.Clear();

            foreach (var s in stats)
            {
                baseMap.TryGetValue(s.BaseHash, out var baseInfo);
                
                string name = "Unknown";
                if (baseInfo != null && _nameMap.TryGetValue(baseInfo.NameHash, out var mappedName))
                {
                    name = mappedName;
                }
                else if (_nameMap.TryGetValue(s.BaseHash, out var directName))
                {
                    name = directName;
                }
                else
                {
                    name = $"Unknown ({s.BaseHash:X8})";
                }

                _allStats.Add(new YokaiStatsWrapper(s, baseInfo, name));
            }

            ApplyFilter();
        }

        private void LoadCharaNames()
        {
            _nameMap.Clear();
            try
            {
                if (_game.Files == null || !_game.Files.ContainsKey("chara_text")) return;

                var gf = _game.Files["chara_text"];
                var vf = gf.GetStream();
                if (vf == null) return;

                byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                if (data == null || data.Length == 0) return;

                var textObj = new T2bþ(data);

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
                System.Diagnostics.Trace.WriteLine($"[YokaiStatsViewModel] Error loading names: {ex.Message}");
            }
        }

        public ObservableCollection<YokaiStatsWrapper> FilteredList { get; private set; }

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

        public YokaiStatsWrapper? SelectedStats
        {
            get => _selectedStats;
            set
            {
                if (_selectedStats != value)
                {
                    _selectedStats = value;
                    OnPropertyChanged();
                }
            }
        }

        private void ApplyFilter()
        {
            FilteredList.Clear();
            var searchLower = _searchText.ToLower();

            foreach (var item in _allStats)
            {
                if (string.IsNullOrWhiteSpace(_searchText))
                {
                    FilteredList.Add(item);
                    continue;
                }

                if (item.Name.ToLower().Contains(searchLower) || 
                    item.BaseHash.ToString("X8").ToLower().Contains(searchLower) ||
                    item.ParamHash.ToString("X8").ToLower().Contains(searchLower))
                {
                    FilteredList.Add(item);
                }
            }
        }

        private void ExecuteSaveChanges(object? obj)
        {
            var stats = _allStats.Select(w => w.Model).ToArray();
            _game.SaveCharaparam(stats);
            System.Diagnostics.Trace.WriteLine("[YokaiStatsViewModel] Saved changes.");
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
