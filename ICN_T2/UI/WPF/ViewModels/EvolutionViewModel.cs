using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using ICN_T2.Logic.Level5.Text;
using ICN_T2.UI.WPF.ViewModels.Contracts;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;
using ReactiveUI;

namespace ICN_T2.UI.WPF.ViewModels
{
    public sealed class YokaiEvolutionCardViewModel : ReactiveObject
    {
        private readonly YokaiStats _stats;
        private readonly Action _onChanged;

        public YokaiEvolutionCardViewModel(YokaiStats stats, string name, Action onChanged)
        {
            _stats = stats;
            Name = name;
            _onChanged = onChanged;
        }

        public string Name { get; }
        public int ParamHash => _stats.ParamHash;

        public string DisplayName =>
            string.IsNullOrWhiteSpace(Name)
                ? $"Unknown (0x{ParamHash:X8})"
                : $"{Name} (0x{ParamHash:X8})";

        public int EvolveParam
        {
            get => _stats.EvolveParam;
            set
            {
                if (_stats.EvolveParam != value)
                {
                    _stats.EvolveParam = value;
                    this.RaisePropertyChanged();
                    _onChanged();
                }
            }
        }

        public int EvolveLevel
        {
            get => _stats.EvolveLevel;
            set
            {
                if (_stats.EvolveLevel != value)
                {
                    _stats.EvolveLevel = value;
                    this.RaisePropertyChanged();
                    _onChanged();
                }
            }
        }

        public int EvolveCost
        {
            get => _stats.EvolveCost;
            set
            {
                if (_stats.EvolveCost != value)
                {
                    _stats.EvolveCost = value;
                    this.RaisePropertyChanged();
                    _onChanged();
                }
            }
        }
    }

    public class EvolutionViewModel : ReactiveObject, IToolSaveParticipant
    {
        private readonly IGame _game;
        private bool _isDirty;
        private YokaiStats[] _allStats = Array.Empty<YokaiStats>();
        private CharaBase[] _allBases = Array.Empty<CharaBase>();

        public EvolutionViewModel(IGame game)
        {
            _game = game;
            Evolutions = new ObservableCollection<YokaiEvolutionCardViewModel>();
            TargetYokaiList = new ObservableCollection<YokaiReference>();
            LoadData();
        }

        public ObservableCollection<YokaiEvolutionCardViewModel> Evolutions { get; }
        public ObservableCollection<YokaiReference> TargetYokaiList { get; }

        private YokaiEvolutionCardViewModel? _selectedYokai;
        public YokaiEvolutionCardViewModel? SelectedYokai
        {
            get => _selectedYokai;
            set => this.RaiseAndSetIfChanged(ref _selectedYokai, value);
        }

        private void LoadData()
        {
            _allStats = _game.GetCharaparam();
            if (_allStats == null || _allStats.Length == 0) return;

            _allBases = _game.GetCharacterbase(true) ?? Array.Empty<CharaBase>();

            // Link existing evolution
            Evolution[] evos = _game.GetCharaevolution() ?? Array.Empty<Evolution>();
            foreach (var stat in _allStats.Where(x => x.EvolveOffset != -1))
            {
                if (stat.EvolveOffset >= 0 && stat.EvolveOffset < evos.Length)
                {
                    var evo = evos[stat.EvolveOffset];
                    stat.EvolveParam = evo.ParamHash;
                    stat.EvolveLevel = evo.Level;
                    stat.EvolveCost = evo.Cost;
                }
            }

            var charaNames = LoadYokaiNames();

            TargetYokaiList.Clear();
            TargetYokaiList.Add(new YokaiReference { Name = "None (No Evolution)", ParamHash = 0x00 });

            Evolutions.Clear();

            foreach (var stat in _allStats)
            {
                var charabase = _allBases.FirstOrDefault(x => x.BaseHash == stat.BaseHash);
                string tribeName = charabase != null && _game.Tribes.ContainsKey(charabase.Tribe) ? _game.Tribes[charabase.Tribe] : "";

                bool isPlayable = (stat.ScoutableHash != 0x00 && stat.ShowInMedalium)
                                  || (stat.ScoutableHash == 0x00 && stat.ShowInMedalium && tribeName != "Boss" && tribeName != "Untribe");

                string name = "Unknown";
                if (charabase != null && charaNames.TryGetValue(charabase.NameHash, out var val))
                {
                    name = val;
                }

                var reference = new YokaiReference { Name = name, ParamHash = stat.ParamHash };
                TargetYokaiList.Add(reference);

                if (isPlayable)
                {
                    var card = new YokaiEvolutionCardViewModel(stat, name, () => _isDirty = true);
                    Evolutions.Add(card);
                }
            }
        }

        private Dictionary<int, string> LoadYokaiNames()
        {
            var names = new Dictionary<int, string>();

            if (_game is YW2 yw2)
            {
                var charaNameMap = yw2.GetCharaNameMap();
                foreach (var kvp in charaNameMap)
                {
                    names[kvp.Key] = kvp.Value;
                }
            }

            return names;
        }

        public bool SavePendingChanges()
        {
            if (!_isDirty) return true;

            List<Evolution> newEvolutions = new List<Evolution>();

            foreach (var stat in _allStats)
            {
                stat.EvolveOffset = -1; // Reset
            }

            foreach (var stat in _allStats.Where(x => x.EvolveParam != 0x00).ToArray())
            {
                Evolution newEvo = new Evolution
                {
                    ParamHash = stat.EvolveParam,
                    Level = stat.EvolveLevel,
                    Cost = stat.EvolveCost
                };

                stat.EvolveOffset = newEvolutions.Count;
                newEvolutions.Add(newEvo);
            }

            _game.SaveCharaparam(_allStats);
            _game.SaveCharaevolution(newEvolutions.ToArray());
            _isDirty = false;
            return true;
        }

        public bool HasPendingChanges => _isDirty;
    }
}
