using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2.Logic;
using ICN_T2.Logic.Level5.Text;

namespace ICN_T2.UI.WPF.ViewModels
{
    public class CharacterScaleWrapper : INotifyPropertyChanged
    {
        private readonly CharScale _model;
        private readonly CharaBase? _baseInfo;
        private readonly string _name;

        public event PropertyChangedEventHandler? PropertyChanged;

        public CharacterScaleWrapper(CharScale model, CharaBase? baseInfo, string name)
        {
            _model = model;
            _baseInfo = baseInfo;
            _name = name;
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
            set { if (_model.Scale1 != value) { _model.Scale1 = value; OnPropertyChanged(); } }
        }

        public float Scale2
        {
            get => _model.Scale2;
            set { if (_model.Scale2 != value) { _model.Scale2 = value; OnPropertyChanged(); } }
        }

        public float Scale3
        {
            get => _model.Scale3;
            set { if (_model.Scale3 != value) { _model.Scale3 = value; OnPropertyChanged(); } }
        }

        public float Scale4
        {
            get => _model.Scale4;
            set { if (_model.Scale4 != value) { _model.Scale4 = value; OnPropertyChanged(); } }
        }

        public float Scale5
        {
            get => _model.Scale5;
            set { if (_model.Scale5 != value) { _model.Scale5 = value; OnPropertyChanged(); } }
        }

        public float Scale6
        {
            get => _model.Scale6;
            set { if (_model.Scale6 != value) { _model.Scale6 = value; OnPropertyChanged(); } }
        }

        // Check if Scale7 exists on the model type (it's in base CharScale but maybe not in derived YW2 Charascale if new keyword used incorrectly?)
        // The search result showed Charascale : CharScale with new properties for 1-6. Scale7 was in base.
        public float Scale7
        {
            get => _model.Scale7;
            set { if (_model.Scale7 != value) { _model.Scale7 = value; OnPropertyChanged(); } }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CharacterScaleViewModel : INotifyPropertyChanged
    {
        private readonly IGame _game;
        private ObservableCollection<CharacterScaleWrapper> _allScales;
        private CharacterScaleWrapper? _selectedScale;
        private string _searchText = "";
        private Dictionary<int, string> _nameMap = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public ICommand SaveChangesCommand { get; }

        public CharacterScaleViewModel(IGame game)
        {
            _game = game;
            _allScales = new ObservableCollection<CharacterScaleWrapper>();
            FilteredList = new ObservableCollection<CharacterScaleWrapper>();
            SaveChangesCommand = new RelayCommand(ExecuteSaveChanges);

            LoadData();
        }

        private void LoadData()
        {
            // 1. Load Names
            LoadCharaNames();

            // 2. Load Base Info Map
            var bases = _game.GetCharacterbase(true).Concat(_game.GetCharacterbase(false));
            var baseMap = new Dictionary<int, CharaBase>();
            foreach (var b in bases)
            {
                if (!baseMap.ContainsKey(b.BaseHash))
                    baseMap[b.BaseHash] = b;
            }

            // 3. Load Scales
            var scales = _game.GetCharascale();
            _allScales.Clear();

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

                _allScales.Add(new CharacterScaleWrapper(s, baseInfo, name));
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
        }

        private void ExecuteSaveChanges(object? obj)
        {
            var scales = _allScales.Select(w => w.Model).ToArray();
            _game.SaveCharascale(scales);
            System.Diagnostics.Trace.WriteLine("[CharacterScaleViewModel] Saved changes.");
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
