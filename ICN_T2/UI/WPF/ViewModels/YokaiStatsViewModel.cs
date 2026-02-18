using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ICN_T2.Logic.Level5.Image;
using ICN_T2.UI.WPF.ViewModels.Contracts;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;
using ICN_T2.YokaiWatch.Games.YW2.Logic;
using T2bText = ICN_T2.Logic.Level5.Text.T2bþ;

namespace ICN_T2.UI.WPF.ViewModels
{
    public sealed class HashOption
    {
        public HashOption(int key, string name)
        {
            Key = key;
            Name = string.IsNullOrWhiteSpace(name) ? $"Unknown (0x{key:X8})" : name;
        }

        public int Key { get; }
        public string Name { get; }

        public override string ToString() => Name;
    }

    public class YokaiStatsWrapper : INotifyPropertyChanged
    {
        private readonly YokaiStats _model;
        private readonly CharaBase? _baseInfo;
        private readonly string _name;
        private readonly IGame? _game;
        private BitmapImage? _icon;
        private Task? _iconLoadTask;

        public event PropertyChangedEventHandler? PropertyChanged;

        public YokaiStatsWrapper(YokaiStats model, CharaBase? baseInfo, string name, IGame? game = null)
        {
            _model = model;
            _baseInfo = baseInfo;
            _name = name;
            _game = game;
        }

        public YokaiStats Model => _model;
        public string Name => _name;
        public int BaseHash => _model.BaseHash;
        public int ParamHash => _model.ParamHash;

        public BitmapImage? Icon
        {
            get
            {
                if (_icon == null && _iconLoadTask == null && _baseInfo != null && _game != null)
                {
                    _iconLoadTask = Task.Run(() => IconCache.GetYokaiIcon(_game, _baseInfo))
                        .ContinueWith(t =>
                        {
                            _icon = t.Result;
                            OnPropertyChanged(nameof(Icon));
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                return _icon;
            }
        }

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
        public int ExperienceCurve { get => _model.ExperienceCurve; set { if (_model.ExperienceCurve != value) { _model.ExperienceCurve = value; OnPropertyChanged(); } } }
        public int EquipmentSlotsAmount { get => _model.EquipmentSlotsAmount; set { if (_model.EquipmentSlotsAmount != value) { _model.EquipmentSlotsAmount = value; OnPropertyChanged(); } } }
        public int FavoriteDonut { get => _model.FavoriteDonut; set { if (_model.FavoriteDonut != value) { _model.FavoriteDonut = value; OnPropertyChanged(); } } }
        public int Drop1Hash { get => _model.Drop1Hash; set { if (_model.Drop1Hash != value) { _model.Drop1Hash = value; OnPropertyChanged(); } } }
        public int Drop1Rate { get => _model.Drop1Rate; set { if (_model.Drop1Rate != value) { _model.Drop1Rate = value; OnPropertyChanged(); } } }
        public int Drop2Hash { get => _model.Drop2Hash; set { if (_model.Drop2Hash != value) { _model.Drop2Hash = value; OnPropertyChanged(); } } }
        public int Drop2Rate { get => _model.Drop2Rate; set { if (_model.Drop2Rate != value) { _model.Drop2Rate = value; OnPropertyChanged(); } } }

        // Skill Hashes / Rates
        public int AttackHash { get => _model.AttackHash; set { if (_model.AttackHash != value) { _model.AttackHash = value; OnPropertyChanged(); } } }
        public float AttackPercentage { get => _model.AttackPercentage; set { if (_model.AttackPercentage != value) { _model.AttackPercentage = value; OnPropertyChanged(); } } }
        public int TechniqueHash { get => _model.TechniqueHash; set { if (_model.TechniqueHash != value) { _model.TechniqueHash = value; OnPropertyChanged(); } } }
        public float TechniquePercentage { get => _model.TechniquePercentage; set { if (_model.TechniquePercentage != value) { _model.TechniquePercentage = value; OnPropertyChanged(); } } }
        public int InspiritHash { get => _model.InspiritHash; set { if (_model.InspiritHash != value) { _model.InspiritHash = value; OnPropertyChanged(); } } }
        public float InspiritPercentage { get => _model.InspiritPercentage; set { if (_model.InspiritPercentage != value) { _model.InspiritPercentage = value; OnPropertyChanged(); } } }
        public int GuardHash { get => _model.GuardHash; set { if (_model.GuardHash != value) { _model.GuardHash = value; OnPropertyChanged(); } } }
        public float GuardPercentage { get => _model.GuardPercentage; set { if (_model.GuardPercentage != value) { _model.GuardPercentage = value; OnPropertyChanged(); } } }
        public int SoultimateHash { get => _model.SoultimateHash; set { if (_model.SoultimateHash != value) { _model.SoultimateHash = value; OnPropertyChanged(); } } }
        public int AbilityHash { get => _model.AbilityHash; set { if (_model.AbilityHash != value) { _model.AbilityHash = value; OnPropertyChanged(); } } }
        public int ScoutableHash { get => _model.ScoutableHash; set { if (_model.ScoutableHash != value) { _model.ScoutableHash = value; OnPropertyChanged(); } } }

        // Quotes / Medalium
        public int Quote1 { get => _model.Quote1; set { if (_model.Quote1 != value) { _model.Quote1 = value; OnPropertyChanged(); } } }
        public int Quote2 { get => _model.Quote2; set { if (_model.Quote2 != value) { _model.Quote2 = value; OnPropertyChanged(); } } }
        public int Quote3 { get => _model.Quote3; set { if (_model.Quote3 != value) { _model.Quote3 = value; OnPropertyChanged(); } } }
        public int BefriendQuote { get => _model.BefriendQuote; set { if (_model.BefriendQuote != value) { _model.BefriendQuote = value; OnPropertyChanged(); } } }
        public int EvolveOffset { get => _model.EvolveOffset; set { if (_model.EvolveOffset != value) { _model.EvolveOffset = value; OnPropertyChanged(); } } }
        public int MedaliumOffset { get => _model.MedaliumOffset; set { if (_model.MedaliumOffset != value) { _model.MedaliumOffset = value; OnPropertyChanged(); } } }
        public bool ShowInMedalium { get => _model.ShowInMedalium; set { if (_model.ShowInMedalium != value) { _model.ShowInMedalium = value; OnPropertyChanged(); } } }


        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class YokaiStatsViewModel : INotifyPropertyChanged, IToolSaveParticipant, ISelectiveToolSaveParticipant
    {
        private readonly IGame _game;
        private readonly ObservableCollection<YokaiStatsWrapper> _allStats;
        private YokaiStatsWrapper? _selectedStats;
        private string _searchText = "";
        private readonly Dictionary<int, string> _nameMap = new();
        private readonly Dictionary<int, YokaiStats> _committedSnapshotByParamHash = new();
        private readonly Dictionary<int, YokaiStatsWrapper> _wrapperByParamHash = new();
        private readonly HashSet<int> _dirtyParamHashes = new();
        private static readonly Dictionary<Type, PropertyInfo[]> ModelPropertyCache = new();
        private bool _hasPendingChanges;

        private readonly List<BattleCommand> _battleCommands = new();
        private readonly List<ItemBase> _items = new();
        private readonly List<AbilityConfig> _abilities = new();

        private T2bText? _itemNames;
        private T2bText? _skillNames;
        private T2bText? _battleCommandNames;
        private T2bText? _abilityNames;
        private T2bText? _addmemberNames;

        public event PropertyChangedEventHandler? PropertyChanged;
        public ICommand SaveChangesCommand { get; }

        public bool HasPendingChanges
        {
            get => _hasPendingChanges;
            private set
            {
                if (_hasPendingChanges == value)
                {
                    return;
                }

                _hasPendingChanges = value;
                OnPropertyChanged();
            }
        }

        public string ToolId => "yokai_stats";
        public string ToolDisplayName => "Yokai Stats";

        public YokaiStatsViewModel(IGame game)
        {
            _game = game;
            _allStats = new ObservableCollection<YokaiStatsWrapper>();
            FilteredList = new ObservableCollection<YokaiStatsWrapper>();

            BattleCommandOptions = new ObservableCollection<HashOption>();
            AbilityOptions = new ObservableCollection<HashOption>();
            ItemOptions = new ObservableCollection<HashOption>();
            ScoutOptions = new ObservableCollection<HashOption>();

            SaveChangesCommand = new RelayCommand(ExecuteSaveChanges);

            LoadData();
        }

        public ObservableCollection<YokaiStatsWrapper> FilteredList { get; }
        public ObservableCollection<HashOption> BattleCommandOptions { get; }
        public ObservableCollection<HashOption> AbilityOptions { get; }
        public ObservableCollection<HashOption> ItemOptions { get; }
        public ObservableCollection<HashOption> ScoutOptions { get; }

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
                if (_selectedStats == value) return;

                if (_selectedStats != null)
                    _selectedStats.PropertyChanged -= SelectedStats_PropertyChanged;

                _selectedStats = value;

                if (_selectedStats != null)
                    _selectedStats.PropertyChanged += SelectedStats_PropertyChanged;

                OnPropertyChanged();
                RaiseSelectedDerivedProperties();
            }
        }

        public int? SelectedAttackOptionKey
        {
            get => GetSelectedOptionKey(SelectedStats?.AttackHash, BattleCommandOptions);
            set => SetSelectedOptionKey(value, x => x.AttackHash, (x, v) => x.AttackHash = v, nameof(SelectedAttackOptionKey));
        }

        public int? SelectedTechniqueOptionKey
        {
            get => GetSelectedOptionKey(SelectedStats?.TechniqueHash, BattleCommandOptions);
            set => SetSelectedOptionKey(value, x => x.TechniqueHash, (x, v) => x.TechniqueHash = v, nameof(SelectedTechniqueOptionKey));
        }

        public int? SelectedInspiritOptionKey
        {
            get => GetSelectedOptionKey(SelectedStats?.InspiritHash, BattleCommandOptions);
            set => SetSelectedOptionKey(value, x => x.InspiritHash, (x, v) => x.InspiritHash = v, nameof(SelectedInspiritOptionKey));
        }

        public int? SelectedGuardOptionKey
        {
            get => GetSelectedOptionKey(SelectedStats?.GuardHash, BattleCommandOptions);
            set => SetSelectedOptionKey(value, x => x.GuardHash, (x, v) => x.GuardHash = v, nameof(SelectedGuardOptionKey));
        }

        public int? SelectedSoultimateOptionKey
        {
            get => GetSelectedOptionKey(SelectedStats?.SoultimateHash, BattleCommandOptions);
            set => SetSelectedOptionKey(value, x => x.SoultimateHash, (x, v) => x.SoultimateHash = v, nameof(SelectedSoultimateOptionKey));
        }

        public int? SelectedAbilityOptionKey
        {
            get => GetSelectedOptionKey(SelectedStats?.AbilityHash, AbilityOptions);
            set => SetSelectedOptionKey(value, x => x.AbilityHash, (x, v) => x.AbilityHash = v, nameof(SelectedAbilityOptionKey));
        }

        public int? SelectedDrop1OptionKey
        {
            get => GetSelectedOptionKey(SelectedStats?.Drop1Hash, ItemOptions);
            set => SetSelectedOptionKey(value, x => x.Drop1Hash, (x, v) => x.Drop1Hash = v, nameof(SelectedDrop1OptionKey));
        }

        public int? SelectedDrop2OptionKey
        {
            get => GetSelectedOptionKey(SelectedStats?.Drop2Hash, ItemOptions);
            set => SetSelectedOptionKey(value, x => x.Drop2Hash, (x, v) => x.Drop2Hash = v, nameof(SelectedDrop2OptionKey));
        }

        public int? SelectedScoutOptionKey
        {
            get => GetSelectedOptionKey(SelectedStats?.ScoutableHash, ScoutOptions);
            set => SetSelectedOptionKey(value, x => x.ScoutableHash, (x, v) => x.ScoutableHash = v, nameof(SelectedScoutOptionKey));
        }

        public int AttackPower => GetBattleCommandPower(SelectedStats?.AttackHash);
        public int TechniquePower => GetBattleCommandPower(SelectedStats?.TechniqueHash);
        public int SoultimatePower => GetBattleCommandPower(SelectedStats?.SoultimateHash);

        public string Quote1Text => ResolveQuotePreview(_battleCommandNames, SelectedStats?.Quote1 ?? 0);
        public string Quote2Text => ResolveQuotePreview(_battleCommandNames, SelectedStats?.Quote2 ?? 0);
        public string Quote3Text => ResolveQuotePreview(_battleCommandNames, SelectedStats?.Quote3 ?? 0);
        public string BefriendQuoteText => ResolveQuotePreview(_addmemberNames, SelectedStats?.BefriendQuote ?? 0);

        private void LoadData()
        {
            LoadCharaNames();
            LoadLegacyMappingResources();
            BuildOptionCollections();

            var bases = _game.GetCharacterbase(true).Concat(_game.GetCharacterbase(false));
            var baseMap = new Dictionary<int, CharaBase>();
            foreach (var b in bases)
            {
                if (!baseMap.ContainsKey(b.BaseHash))
                    baseMap[b.BaseHash] = b;
            }

            var stats = _game.GetCharaparam();
            _allStats.Clear();
            _wrapperByParamHash.Clear();
            _committedSnapshotByParamHash.Clear();
            _dirtyParamHashes.Clear();
            HasPendingChanges = false;

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

                var wrapper = new YokaiStatsWrapper(s, baseInfo, name, _game);
                wrapper.PropertyChanged += AnyStatsWrapper_PropertyChanged;
                _allStats.Add(wrapper);
                _wrapperByParamHash[s.ParamHash] = wrapper;
                _committedSnapshotByParamHash[s.ParamHash] = CloneStatsModel(s);
            }

            ApplyFilter();
        }

        private void LoadLegacyMappingResources()
        {
            _battleCommands.Clear();
            _items.Clear();
            _abilities.Clear();

            try
            {
                _battleCommands.AddRange(_game.GetBattleCommands() ?? Array.Empty<BattleCommand>());
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[YokaiStatsViewModel] Failed to load BattleCommands: {ex.Message}");
            }

            try
            {
                _items.AddRange(_game.GetItems("all") ?? Array.Empty<ItemBase>());
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[YokaiStatsViewModel] Failed to load Items: {ex.Message}");
            }

            try
            {
                _abilities.AddRange(_game.GetAbilities() ?? Array.Empty<AbilityConfig>());
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[YokaiStatsViewModel] Failed to load Abilities: {ex.Message}");
            }

            _itemNames = LoadTextResource("item_text");
            _skillNames = LoadTextResource("skill_text");
            _battleCommandNames = LoadTextResource("battle_text");
            _abilityNames = LoadTextResource("chara_ability_text");
            _addmemberNames = LoadTextResource("addmembermenu_text");
        }

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
                Trace.WriteLine($"[YokaiStatsViewModel] Failed to load text resource '{key}': {ex.Message}");
                return null;
            }
        }

        private void BuildOptionCollections()
        {
            BattleCommandOptions.Clear();
            AbilityOptions.Clear();
            ItemOptions.Clear();
            ScoutOptions.Clear();

            BattleCommandOptions.Add(new HashOption(0, "(None)"));
            AbilityOptions.Add(new HashOption(0, "(None)"));
            ItemOptions.Add(new HashOption(0, "(None)"));
            ScoutOptions.Add(new HashOption(0, "(None)"));

            HashSet<int> seenBattle = new HashSet<int>();
            for (int i = 0; i < _battleCommands.Count; i++)
            {
                var command = _battleCommands[i];
                if (!seenBattle.Add(command.BattleCommandHash)) continue;
                BattleCommandOptions.Add(new HashOption(command.BattleCommandHash, ResolveBattleCommandDisplayName(command, i)));
            }

            HashSet<int> seenAbility = new HashSet<int>();
            for (int i = 0; i < _abilities.Count; i++)
            {
                var ability = _abilities[i];
                if (!seenAbility.Add(ability.CharaabilityConfigHash)) continue;
                AbilityOptions.Add(new HashOption(ability.CharaabilityConfigHash, ResolveAbilityDisplayName(ability, i)));
            }

            HashSet<int> seenItems = new HashSet<int>();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (!seenItems.Add(item.ItemHash)) continue;
                ItemOptions.Add(new HashOption(item.ItemHash, ResolveItemDisplayName(item, i)));
            }

            foreach (var kv in _game.ScoutablesType)
            {
                ScoutOptions.Add(new HashOption(kv.Key, kv.Value));
            }
        }

        private string ResolveBattleCommandDisplayName(BattleCommand command, int index)
        {
            if (TryResolveNoun(_battleCommandNames, command.NameHash, out var name))
                return name;

            if (TryResolveNoun(_skillNames, command.NameHash, out name))
                return name;

            if (TryResolveNoun(_itemNames, command.NameHash, out name))
                return name;

            return $"Battle Command {index}";
        }

        private string ResolveItemDisplayName(ItemBase item, int index)
        {
            if (TryResolveNoun(_itemNames, item.NameHash, out var name))
                return name;

            return $"Item {index}";
        }

        private string ResolveAbilityDisplayName(AbilityConfig ability, int index)
        {
            if (TryResolveNoun(_abilityNames, ability.NameHash, out var name))
                return name;

            return $"Ability {index}";
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

        private void LoadCharaNames()
        {
            _nameMap.Clear();
            var textObj = LoadTextResource("chara_text");
            if (textObj == null) return;

            foreach (var kv in textObj.Nouns)
            {
                if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrWhiteSpace(kv.Value.Strings[0].Text))
                {
                    _nameMap[kv.Key] = kv.Value.Strings[0].Text;
                }
            }
        }

        private static string ResolveQuotePreview(T2bText? textObj, int hash)
        {
            if (textObj?.Texts != null &&
                textObj.Texts.TryGetValue(hash, out var cfg) &&
                cfg.Strings != null &&
                cfg.Strings.Count > 0 &&
                !string.IsNullOrWhiteSpace(cfg.Strings[0].Text))
            {
                return cfg.Strings[0].Text.Replace("\\n", Environment.NewLine);
            }

            return $"Unknown (0x{hash:X8})";
        }

        private int? GetSelectedOptionKey(int? sourceKey, IEnumerable<HashOption> options)
        {
            if (sourceKey == null) return null;
            return options.Any(x => x.Key == sourceKey.Value) ? sourceKey : null;
        }

        private int GetBattleCommandPower(int? hash)
        {
            if (hash == null || hash == 0) return 0;
            var cmd = _battleCommands.FirstOrDefault(x => x.BattleCommandHash == hash);
            return cmd?.PowerMultiplicator ?? 0;
        }

        private void SetSelectedOptionKey(int? newValue, Func<YokaiStatsWrapper, int> getter, Action<YokaiStatsWrapper, int> setter, string propertyName)
        {
            if (SelectedStats == null) return;

            int resolved = newValue ?? 0;
            if (getter(SelectedStats) == resolved) return;

            setter(SelectedStats, resolved);
            OnPropertyChanged(propertyName);
        }

        private void SelectedStats_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(YokaiStatsWrapper.AttackHash):
                    OnPropertyChanged(nameof(SelectedAttackOptionKey));
                    OnPropertyChanged(nameof(AttackPower));
                    break;
                case nameof(YokaiStatsWrapper.TechniqueHash):
                    OnPropertyChanged(nameof(SelectedTechniqueOptionKey));
                    OnPropertyChanged(nameof(TechniquePower));
                    break;
                case nameof(YokaiStatsWrapper.InspiritHash):
                    OnPropertyChanged(nameof(SelectedInspiritOptionKey));
                    break;
                case nameof(YokaiStatsWrapper.GuardHash):
                    OnPropertyChanged(nameof(SelectedGuardOptionKey));
                    break;
                case nameof(YokaiStatsWrapper.SoultimateHash):
                    OnPropertyChanged(nameof(SelectedSoultimateOptionKey));
                    OnPropertyChanged(nameof(SoultimatePower));
                    break;
                case nameof(YokaiStatsWrapper.AbilityHash):
                    OnPropertyChanged(nameof(SelectedAbilityOptionKey));
                    break;
                case nameof(YokaiStatsWrapper.Drop1Hash):
                    OnPropertyChanged(nameof(SelectedDrop1OptionKey));
                    break;
                case nameof(YokaiStatsWrapper.Drop2Hash):
                    OnPropertyChanged(nameof(SelectedDrop2OptionKey));
                    break;
                case nameof(YokaiStatsWrapper.ScoutableHash):
                    OnPropertyChanged(nameof(SelectedScoutOptionKey));
                    break;
                case nameof(YokaiStatsWrapper.Quote1):
                    OnPropertyChanged(nameof(Quote1Text));
                    break;
                case nameof(YokaiStatsWrapper.Quote2):
                    OnPropertyChanged(nameof(Quote2Text));
                    break;
                case nameof(YokaiStatsWrapper.Quote3):
                    OnPropertyChanged(nameof(Quote3Text));
                    break;
                case nameof(YokaiStatsWrapper.BefriendQuote):
                    OnPropertyChanged(nameof(BefriendQuoteText));
                    break;
            }
        }

        private void RaiseSelectedDerivedProperties()
        {
            OnPropertyChanged(nameof(SelectedAttackOptionKey));
            OnPropertyChanged(nameof(SelectedTechniqueOptionKey));
            OnPropertyChanged(nameof(SelectedInspiritOptionKey));
            OnPropertyChanged(nameof(SelectedGuardOptionKey));
            OnPropertyChanged(nameof(SelectedSoultimateOptionKey));
            OnPropertyChanged(nameof(SelectedAbilityOptionKey));

            OnPropertyChanged(nameof(AttackPower));
            OnPropertyChanged(nameof(TechniquePower));
            OnPropertyChanged(nameof(SoultimatePower));
            OnPropertyChanged(nameof(SelectedDrop1OptionKey));
            OnPropertyChanged(nameof(SelectedDrop2OptionKey));
            OnPropertyChanged(nameof(SelectedScoutOptionKey));

            OnPropertyChanged(nameof(Quote1Text));
            OnPropertyChanged(nameof(Quote2Text));
            OnPropertyChanged(nameof(Quote3Text));
            OnPropertyChanged(nameof(BefriendQuoteText));
        }

        private void ApplyFilter()
        {
            var previousSelection = _selectedStats;
            FilteredList.Clear();
            string searchLower = _searchText.ToLowerInvariant();

            foreach (var item in _allStats)
            {
                if (string.IsNullOrWhiteSpace(_searchText))
                {
                    FilteredList.Add(item);
                    continue;
                }

                if (item.Name.ToLowerInvariant().Contains(searchLower) ||
                    item.BaseHash.ToString("X8").ToLowerInvariant().Contains(searchLower) ||
                    item.ParamHash.ToString("X8").ToLowerInvariant().Contains(searchLower))
                {
                    FilteredList.Add(item);
                }
            }

            if (previousSelection != null && FilteredList.Contains(previousSelection))
                return;

            SelectedStats = FilteredList.FirstOrDefault();
        }

        private void ExecuteSaveChanges(object? obj)
        {
            SavePendingChanges();
        }

        public IReadOnlyList<ToolPendingChange> GetPendingChanges()
        {
            return _allStats
                .Where(x => _dirtyParamHashes.Contains(x.ParamHash))
                .Select(x => new ToolPendingChange(
                    changeId: x.ParamHash.ToString("X8"),
                    displayName: x.Name,
                    description: $"ParamHash: 0x{x.ParamHash:X8}"))
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

            var requested = new HashSet<int>();
            foreach (string raw in changeIds)
            {
                if (int.TryParse(raw, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int hex))
                {
                    requested.Add(hex);
                    continue;
                }

                if (int.TryParse(raw, out int dec))
                {
                    requested.Add(dec);
                }
            }

            if (requested.Count == 0)
            {
                return new ToolSaveBatchResult(savedIds, failed);
            }

            var toSave = new YokaiStats[_allStats.Count];
            for (int i = 0; i < _allStats.Count; i++)
            {
                var wrapper = _allStats[i];
                if (requested.Contains(wrapper.ParamHash))
                {
                    toSave[i] = CloneStatsModel(wrapper.Model);
                }
                else if (_committedSnapshotByParamHash.TryGetValue(wrapper.ParamHash, out var snapshot))
                {
                    toSave[i] = CloneStatsModel(snapshot);
                }
                else
                {
                    toSave[i] = CloneStatsModel(wrapper.Model);
                }
            }

            try
            {
                _game.SaveCharaparam(toSave);
                foreach (int paramHash in requested)
                {
                    if (!_wrapperByParamHash.TryGetValue(paramHash, out var wrapper))
                    {
                        continue;
                    }

                    _committedSnapshotByParamHash[paramHash] = CloneStatsModel(wrapper.Model);
                    _dirtyParamHashes.Remove(paramHash);
                    savedIds.Add(paramHash.ToString("X8"));
                }

                HasPendingChanges = _dirtyParamHashes.Count > 0;
                Trace.WriteLine($"[YokaiStatsViewModel] Saved selected changes: {savedIds.Count}");
            }
            catch (Exception ex)
            {
                string reason = ex.Message;
                foreach (int paramHash in requested)
                {
                    failed[paramHash.ToString("X8")] = reason;
                }
            }

            return new ToolSaveBatchResult(savedIds, failed);
        }

        public bool SavePendingChanges()
        {
            if (!HasPendingChanges)
            {
                return false;
            }

            var allDirty = _dirtyParamHashes.Select(x => x.ToString("X8")).ToArray();
            var result = SavePendingChanges(allDirty);
            return result.SavedChangeIds.Count > 0;
        }

        private void AnyStatsWrapper_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not YokaiStatsWrapper wrapper)
            {
                return;
            }

            if (string.Equals(e.PropertyName, nameof(YokaiStatsWrapper.Icon), StringComparison.Ordinal))
            {
                return;
            }

            UpdateDirtyStateForParamHash(wrapper.ParamHash);
        }

        private void UpdateDirtyStateForParamHash(int paramHash)
        {
            if (!_wrapperByParamHash.TryGetValue(paramHash, out var wrapper))
            {
                return;
            }

            if (!_committedSnapshotByParamHash.TryGetValue(paramHash, out var snapshot))
            {
                _dirtyParamHashes.Add(paramHash);
                HasPendingChanges = _dirtyParamHashes.Count > 0;
                return;
            }

            if (StatsEqual(wrapper.Model, snapshot))
            {
                _dirtyParamHashes.Remove(paramHash);
            }
            else
            {
                _dirtyParamHashes.Add(paramHash);
            }

            HasPendingChanges = _dirtyParamHashes.Count > 0;
        }

        private static YokaiStats CloneStatsModel(YokaiStats source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var sourceType = source.GetType();
            var clone = (YokaiStats)(Activator.CreateInstance(sourceType) ?? new YokaiStats());
            foreach (PropertyInfo property in GetModelProperties(sourceType))
            {
                object? value = property.GetValue(source);
                property.SetValue(clone, value);
            }

            return clone;
        }

        private static bool StatsEqual(YokaiStats left, YokaiStats right)
        {
            if (left.GetType() != right.GetType())
            {
                return false;
            }

            foreach (PropertyInfo property in GetModelProperties(left.GetType()))
            {
                object? leftValue = property.GetValue(left);
                object? rightValue = property.GetValue(right);
                if (!Equals(leftValue, rightValue))
                {
                    return false;
                }
            }

            return true;
        }

        private static PropertyInfo[] GetModelProperties(Type modelType)
        {
            lock (ModelPropertyCache)
            {
                if (ModelPropertyCache.TryGetValue(modelType, out var cached))
                {
                    return cached;
                }

                var props = modelType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                    .ToArray();
                ModelPropertyCache[modelType] = props;
                return props;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
