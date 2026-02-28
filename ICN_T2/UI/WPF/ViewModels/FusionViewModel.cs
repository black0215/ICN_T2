using ICN_T2.UI.WPF.ViewModels.Contracts;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;
using ReactiveUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ICN_T2.UI.WPF.ViewModels
{
    public class FusionCardViewModel : ReactiveObject
    {
        private ICombineConfig _config;

        public ICombineConfig Config => _config;

        public bool BaseIsItem { get => _config.BaseIsItem; set { _config.BaseIsItem = value; this.RaisePropertyChanged(); UpdateBaseName(); } }
        public bool MaterialIsItem { get => _config.MaterialIsItem; set { _config.MaterialIsItem = value; this.RaisePropertyChanged(); UpdateMaterialName(); } }
        public bool EvolveToIsItem { get => _config.EvolveToIsItem; set { _config.EvolveToIsItem = value; this.RaisePropertyChanged(); UpdateEvolveToName(); } }

        public int BaseHash { get => _config.BaseHash; set { _config.BaseHash = value; this.RaisePropertyChanged(); UpdateBaseName(); } }
        public int MaterialHash { get => _config.MaterialHash; set { _config.MaterialHash = value; this.RaisePropertyChanged(); UpdateMaterialName(); } }
        public int EvolveToHash { get => _config.EvolveToHash; set { _config.EvolveToHash = value; this.RaisePropertyChanged(); UpdateEvolveToName(); } }

        public int OniOrbCost { get => _config.OniOrbCost; set { _config.OniOrbCost = value; this.RaisePropertyChanged(); } }

        private string _baseName = "Unknown";
        public string BaseName { get => _baseName; set => this.RaiseAndSetIfChanged(ref _baseName, value); }

        private string _materialName = "Unknown";
        public string MaterialName { get => _materialName; set => this.RaiseAndSetIfChanged(ref _materialName, value); }

        private string _evolveToName = "Unknown";
        public string EvolveToName { get => _evolveToName; set => this.RaiseAndSetIfChanged(ref _evolveToName, value); }

        // ParamHash → name (Yokai), NameHash → name used for Items
        private IReadOnlyDictionary<int, string> _yokaiByParam;
        private IReadOnlyDictionary<int, string> _itemByNameHash;

        public FusionCardViewModel(ICombineConfig config,
            IReadOnlyDictionary<int, string> yokaiByParam,
            IReadOnlyDictionary<int, string> itemByNameHash)
        {
            _config = config;
            _yokaiByParam = yokaiByParam;
            _itemByNameHash = itemByNameHash;
            UpdateBaseName();
            UpdateMaterialName();
            UpdateEvolveToName();
        }

        private string ResolveName(int hash, bool isItem)
        {
            var dict = isItem ? _itemByNameHash : _yokaiByParam;
            if (dict.TryGetValue(hash, out var name)) return name;
            return $"0x{hash:X8}";
        }

        private void UpdateBaseName() => BaseName = ResolveName(BaseHash, BaseIsItem);
        private void UpdateMaterialName() => MaterialName = ResolveName(MaterialHash, MaterialIsItem);
        private void UpdateEvolveToName() => EvolveToName = ResolveName(EvolveToHash, EvolveToIsItem);
    }

    public class FusionViewModel : ReactiveObject, IToolSaveParticipant
    {
        private IGame _game;

        // ParamHash → name (for Yokai identify in combine_config)
        public Dictionary<int, string> YokaiNames { get; private set; } = new Dictionary<int, string>();
        // NameHash → name (for Items)
        public Dictionary<int, string> ItemNames { get; private set; } = new Dictionary<int, string>();

        public ObservableCollection<KeyValuePair<int, string>> YokaiList { get; private set; } = new ObservableCollection<KeyValuePair<int, string>>();
        public ObservableCollection<KeyValuePair<int, string>> ItemList { get; private set; } = new ObservableCollection<KeyValuePair<int, string>>();

        public ObservableCollection<FusionCardViewModel> Fusions { get; } = new ObservableCollection<FusionCardViewModel>();

        private FusionCardViewModel _selectedFusion;
        public FusionCardViewModel SelectedFusion
        {
            get => _selectedFusion;
            set => this.RaiseAndSetIfChanged(ref _selectedFusion, value);
        }

        public FusionViewModel(IGame game)
        {
            _game = game;
            LoadNames();
            LoadFusions();
        }

        /// <summary>
        /// Builds:
        ///   YokaiNames: ParamHash → name  (combine_config BaseHash == ParamHash)
        ///   ItemNames:  NameHash  → name  (item_text key == NameHash)
        /// </summary>
        private void LoadNames()
        {
            if (_game is YW2 yw2)
            {
                // --- Yokai: ParamHash → name ---
                // Route: GetCharaparam() gives ParamHash + BaseHash.
                //        GetCharacterbase() gives BaseHash + NameHash.
                //        GetCharaNameMap() gives NameHash → name.
                // We join them: ParamHash → BaseHash → NameHash → name.
                var charaNameMap = yw2.GetCharaNameMap();                   // NameHash → name
                var allStats = _game.GetCharaparam();                        // has ParamHash + BaseHash
                var allBases = _game.GetCharacterbase(true);                 // has BaseHash + NameHash

                if (allStats != null && allBases != null && charaNameMap != null)
                {
                    var baseHashToNameHash = allBases.ToDictionary(b => b.BaseHash, b => b.NameHash);

                    foreach (var stat in allStats)
                    {
                        if (baseHashToNameHash.TryGetValue(stat.BaseHash, out var nameHash) &&
                            charaNameMap.TryGetValue(nameHash, out var yokaiName))
                        {
                            YokaiNames[stat.ParamHash] = yokaiName;
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[FusionVM] YokaiNames built: {YokaiNames.Count} entries");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[FusionVM] WARNING: Could not build YokaiNames – one or more sources are null/empty");
                }

                // --- Items: ItemHash → name (combine_config uses ItemHash for items) ---
                var itemNameMap = yw2.GetItemFullNameMap();  // ItemHash → name
                if (itemNameMap != null)
                {
                    ItemNames = itemNameMap;
                    System.Diagnostics.Debug.WriteLine($"[FusionVM] ItemNames built: {ItemNames.Count} entries");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[FusionVM] WARNING: _game is not YW2, name maps not loaded.");
            }

            YokaiList.Clear();
            YokaiList.Add(new KeyValuePair<int, string>(0, "None"));
            foreach (var kvp in YokaiNames.OrderBy(x => x.Value)) YokaiList.Add(kvp);

            ItemList.Clear();
            ItemList.Add(new KeyValuePair<int, string>(0, "None"));
            foreach (var kvp in ItemNames.OrderBy(x => x.Value)) ItemList.Add(kvp);
        }

        private void LoadFusions()
        {
            Fusions.Clear();
            var fusionsList = _game.GetFusions();
            System.Diagnostics.Debug.WriteLine($"[FusionVM] GetFusions returned {fusionsList.Length} entries");
            foreach (var f in fusionsList)
            {
                Fusions.Add(new FusionCardViewModel(f, YokaiNames, ItemNames));
            }
        }

        public bool HasPendingChanges { get; private set; }

        public bool SavePendingChanges()
        {
            try
            {
                var arr = Fusions.Select(x => x.Config).ToArray();
                _game.SaveFusions(arr);
                HasPendingChanges = false;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
