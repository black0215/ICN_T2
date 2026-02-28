using ICN_T2.Logic.Level5.Text;
using ICN_T2.UI.WPF.ViewModels.Parsing;
using ICN_T2.UI.WPF.ViewModels.Contracts;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;
using ICN_T2.YokaiWatch.Games.YW2.Logic;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reactive;
using System.Text;
using System.Windows;
using T2bText = ICN_T2.Logic.Level5.Text.T2b\u00FE;

namespace ICN_T2.UI.WPF.ViewModels
{
    public enum MedalGroupFilterMode
    {
        All,
        Reachable,
        Unreachable
    }

    public sealed class CapsuleMachineRouteViewModel : ReactiveObject
    {
        private readonly CapsuleMachineRoute _model;
        private readonly Action _onChanged;

        public CapsuleMachineRouteViewModel(int machineHash, CapsuleMachineRoute model, Action onChanged)
        {
            MachineHash = machineHash;
            _model = model;
            _onChanged = onChanged;
        }

        public int MachineHash { get; }
        public int RouteHash => _model.RouteHash;
        public int CoinHash => _model.CoinHash;

        public int RouteMode
        {
            get => _model.RouteMode;
            set
            {
                if (_model.RouteMode == value) return;
                _model.RouteMode = value;
                this.RaisePropertyChanged();
                _onChanged();
            }
        }

        public int Unk4
        {
            get => _model.Unk4;
            set
            {
                if (_model.Unk4 == value) return;
                _model.Unk4 = value;
                this.RaisePropertyChanged();
                _onChanged();
            }
        }
    }

    public sealed class CapsuleGroupViewModel
    {
        private readonly Func<int, string?>? _coinDisplayNameResolver;
        public CapsuleGroupViewModel(
            CapsuleRateGroup? rateGroup,
            IEnumerable<CapsuleConfigViewModel> items,
            IEnumerable<CapsuleMachineRouteViewModel> machineRoutes,
            bool isReachable,
            bool isUnmappedGroup,
            Func<int, string?>? coinDisplayNameResolver = null)
        {
            RateGroup = rateGroup;
            Items = new ObservableCollection<CapsuleConfigViewModel>(items ?? Enumerable.Empty<CapsuleConfigViewModel>());
            MachineRoutes = new ObservableCollection<CapsuleMachineRouteViewModel>(machineRoutes ?? Enumerable.Empty<CapsuleMachineRouteViewModel>());
            IsReachable = isReachable;
            IsUnmappedGroup = isUnmappedGroup;
            _coinDisplayNameResolver = coinDisplayNameResolver;
        }

        public static CapsuleGroupViewModel CreateUnmapped(IEnumerable<CapsuleConfigViewModel> items)
        {
            return new CapsuleGroupViewModel(
                rateGroup: null,
                items: items,
                machineRoutes: Array.Empty<CapsuleMachineRouteViewModel>(),
                isReachable: false,
                isUnmappedGroup: true);
        }

        public CapsuleRateGroup? RateGroup { get; }
        public ObservableCollection<CapsuleConfigViewModel> Items { get; }
        public ObservableCollection<CapsuleMachineRouteViewModel> MachineRoutes { get; }
        public bool IsReachable { get; }
        public bool IsUnmappedGroup { get; }

        public int CoinHash => RateGroup?.CoinHash ?? 0;
        public int CoinId => RateGroup?.CoinId ?? 0;
        public int MachineReferenceCount => MachineRoutes.Count;
        public bool IsUnreachable => !IsReachable;

        public string DisplayName => IsUnmappedGroup
            ? "Unmapped item slots"
            : (_coinDisplayNameResolver?.Invoke(CoinHash) ?? $"Hash: 0x{CoinHash:X8}");
        public string SummaryLine => IsUnmappedGroup
            ? "State: Unmapped"
            : $"State: {(IsReachable ? "Reachable" : "Unreachable")}";
        public void RecalculateRates()
        {
            double totalWeight = Items
                .Where(x => x.HasRateSlot)
                .Sum(x => Math.Max(0, x.RateWeightRaw));

            foreach (var slot in Items)
            {
                if (!slot.HasRateSlot || totalWeight <= 0)
                {
                    slot.SetRatePercent(0);
                }
                else
                {
                    double percent = (Math.Max(0, slot.RateWeightRaw) / totalWeight) * 100.0;
                    slot.SetRatePercent(percent);
                }
            }
        }

        public override string ToString() => DisplayName;
    }

    public class CrankakaiViewModel : ReactiveObject, IToolSaveParticipant
    {
        private readonly IGame _game;
        private readonly YW2? _yw2Game;
        private bool _isDirty;
        private T2bText? _itemNames;
        private T2bText? _yokaiNames;
        private readonly Dictionary<int, string> _userHashNameMap = new Dictionary<int, string>();
        private CapsuleDataBundle _bundle = new CapsuleDataBundle();
        private MedalGroupFilterMode _groupFilterMode = MedalGroupFilterMode.All;
        private string _medalSearchText = "";

        public Dictionary<int, string> HashToNameMap { get; } = new Dictionary<int, string>();
        public ObservableCollection<CapsuleGroupViewModel> MedalGroups { get; } = new ObservableCollection<CapsuleGroupViewModel>();
        public ObservableCollection<CapsuleGroupViewModel> FilteredMedalGroups { get; } = new ObservableCollection<CapsuleGroupViewModel>();
        public ObservableCollection<CapsuleConfigViewModel> SlotCards { get; } = new ObservableCollection<CapsuleConfigViewModel>();
        public ObservableCollection<CapsuleMachineRouteViewModel> SelectedMachineRoutes { get; } = new ObservableCollection<CapsuleMachineRouteViewModel>();
        public ObservableCollection<HashOption> RewardOptions { get; } = new ObservableCollection<HashOption>();

        private CapsuleGroupViewModel? _selectedMedalGroup;
        public CapsuleGroupViewModel? SelectedMedalGroup
        {
            get => _selectedMedalGroup;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedMedalGroup, value);
                this.RaisePropertyChanged(nameof(HasSelectedMedalGroup));
                UpdateSelectionCollections(value);
            }
        }

        public bool HasSelectedMedalGroup => SelectedMedalGroup != null;
        public bool IsYW2Mode => _yw2Game != null;
        public bool IsNotYW2Mode => !IsYW2Mode;
        public string UnsupportedMessage => "Crank-a-kai ���� ������ ���� YW2������ �����˴ϴ�.";

        public string MedalSearchText
        {
            get => _medalSearchText;
            set
            {
                this.RaiseAndSetIfChanged(ref _medalSearchText, value);
                ApplyMedalFilter();
            }
        }

        public bool IsFilterAll
        {
            get => _groupFilterMode == MedalGroupFilterMode.All;
            set
            {
                if (value)
                {
                    SetFilterMode(MedalGroupFilterMode.All);
                }
            }
        }

        public bool IsFilterReachable
        {
            get => _groupFilterMode == MedalGroupFilterMode.Reachable;
            set
            {
                if (value)
                {
                    SetFilterMode(MedalGroupFilterMode.Reachable);
                }
            }
        }

        public bool IsFilterUnreachable
        {
            get => _groupFilterMode == MedalGroupFilterMode.Unreachable;
            set
            {
                if (value)
                {
                    SetFilterMode(MedalGroupFilterMode.Unreachable);
                }
            }
        }

        public ReactiveCommand<Unit, Unit> RevertChangesCommand { get; }
        public bool HasPendingChanges => _isDirty;

        public CrankakaiViewModel(IGame game)
        {
            _game = game;
            _yw2Game = game as YW2;
            RevertChangesCommand = ReactiveCommand.Create(RevertChanges);
            LoadData();
        }

        public string ResolveRewardName(int hash)
        {
            if (HashToNameMap.TryGetValue(hash, out string? name))
            {
                return name;
            }

            return $"Unknown (0x{hash:X8})";
        }

        private void SetFilterMode(MedalGroupFilterMode mode)
        {
            if (_groupFilterMode == mode) return;
            _groupFilterMode = mode;
            this.RaisePropertyChanged(nameof(IsFilterAll));
            this.RaisePropertyChanged(nameof(IsFilterReachable));
            this.RaisePropertyChanged(nameof(IsFilterUnreachable));
            ApplyMedalFilter();
        }

        private void UpdateSelectionCollections(CapsuleGroupViewModel? group)
        {
            SlotCards.Clear();
            SelectedMachineRoutes.Clear();

            if (group == null) return;

            foreach (var item in group.Items)
            {
                SlotCards.Add(item);
            }

            foreach (var route in group.MachineRoutes)
            {
                SelectedMachineRoutes.Add(route);
            }
        }

        private void ApplyMedalFilter()
        {
            FilteredMedalGroups.Clear();

            string q = (MedalSearchText ?? "").Trim().ToLowerInvariant();
            foreach (var group in MedalGroups)
            {
                if (_groupFilterMode == MedalGroupFilterMode.Reachable && !group.IsReachable)
                {
                    continue;
                }

                if (_groupFilterMode == MedalGroupFilterMode.Unreachable && group.IsReachable)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(q))
                {
                    string searchText = $"{group.DisplayName} {group.SummaryLine} 0x{group.CoinHash:X8}";
                    if (!searchText.ToLowerInvariant().Contains(q))
                    {
                        continue;
                    }
                }

                FilteredMedalGroups.Add(group);
            }

            if (SelectedMedalGroup == null || !FilteredMedalGroups.Contains(SelectedMedalGroup))
            {
                SelectedMedalGroup = FilteredMedalGroups.FirstOrDefault();
            }
        }

        private void LoadData()
        {
            HashToNameMap.Clear();
            _userHashNameMap.Clear();
            RewardOptions.Clear();
            MedalGroups.Clear();
            FilteredMedalGroups.Clear();
            SlotCards.Clear();
            SelectedMachineRoutes.Clear();
            SelectedMedalGroup = null;

            BuildRewardOptions();
            ApplyUserHashMappingsFromCsv();

            if (_yw2Game == null)
            {
                return;
            }

            _bundle = _yw2Game.GetCapsuleDataBundle() ?? new CapsuleDataBundle();
            BuildMedalGroups();
            ApplyMedalFilter();
        }

        private void BuildRewardOptions()
        {
            _itemNames = LoadTextResource("item_text");
            _yokaiNames = LoadTextResource("chara_text");

            var seen = new HashSet<int>();
            AddRewardOption(0x00, "None / Empty", seen);

            var items = _game.GetItems("all") ?? Array.Empty<ItemBase>();
            foreach (var item in items)
            {
                string name = ResolveName(_itemNames, item.NameHash, "Item", item.ItemHash);
                AddRewardOption(item.ItemHash, name, seen);
            }

            var charaparams = _game.GetCharaparam() ?? Array.Empty<YokaiStats>();
            var charabases = _game.GetCharacterbase(true) ?? Array.Empty<CharaBase>();
            var charabaseByHash = charabases
                .GroupBy(x => x.BaseHash)
                .ToDictionary(x => x.Key, x => x.First());

            foreach (var param in charaparams)
            {
                if (!charabaseByHash.TryGetValue(param.BaseHash, out var charabase))
                {
                    continue;
                }

                string name = ResolveName(_yokaiNames, charabase.NameHash, "Yokai", param.ParamHash);
                AddRewardOption(param.ParamHash, name, seen);
            }
        }

        private void AddRewardOption(int hash, string name, HashSet<int> seen)
        {
            HashToNameMap[hash] = name;
            if (!seen.Add(hash))
            {
                return;
            }

            RewardOptions.Add(new HashOption(hash, name));
        }

        private void ApplyUserHashMappingsFromCsv()
        {
            string[] lines = LoadUserHashMappingLines(out string sourceLabel);
            if (lines.Length == 0)
            {
                return;
            }

            try
            {
                var parsed = HashMappingCsvParser.ParseLines(lines);
                int appliedCount = 0;

                foreach (var pair in parsed)
                {
                    _userHashNameMap[pair.Key] = pair.Value;
                    UpsertRewardOption(pair.Key, pair.Value);
                    appliedCount++;
                }

                System.Diagnostics.Debug.WriteLine($"[CrankakaiVM] Applied {appliedCount} user hash mappings from '{sourceLabel}'.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CrankakaiVM] Failed to load user hash mappings: {ex.Message}");
            }
        }
        private void UpsertRewardOption(int hash, string name)
        {
            HashToNameMap[hash] = name;

            for (int i = 0; i < RewardOptions.Count; i++)
            {
                if (RewardOptions[i].Key != hash)
                {
                    continue;
                }

                if (!string.Equals(RewardOptions[i].Name, name, StringComparison.Ordinal))
                {
                    RewardOptions[i] = new HashOption(hash, name);
                }
                return;
            }

            RewardOptions.Add(new HashOption(hash, name));
        }

        private static string? FindUserHashMappingCsvPath()
        {
            string baseDir = AppContext.BaseDirectory;
            string currentDir = Environment.CurrentDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "Resources", "hash_mapping_user.csv"),
                Path.Combine(baseDir, "hash_mapping_user.csv"),
                Path.Combine(currentDir, "Resources", "hash_mapping_user.csv"),
                Path.Combine(currentDir, "ICN_T2", "Resources", "hash_mapping_user.csv"),
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string[] LoadUserHashMappingLines(out string sourceLabel)
        {
            string? csvPath = FindUserHashMappingCsvPath();
            if (!string.IsNullOrWhiteSpace(csvPath) && File.Exists(csvPath))
            {
                sourceLabel = csvPath;
                return ReadAllLinesWithEncodingFallback(csvPath);
            }

            var assembly = typeof(CrankakaiViewModel).Assembly;
            const string preferredResourceName = "ICN_T2.Resources.hash_mapping_user.csv";
            string? resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(x => string.Equals(x, preferredResourceName, StringComparison.Ordinal))
                ?? assembly.GetManifestResourceNames()
                    .FirstOrDefault(x => x.EndsWith(".Resources.hash_mapping_user.csv", StringComparison.Ordinal));

            if (string.IsNullOrWhiteSpace(resourceName))
            {
                sourceLabel = "none";
                return Array.Empty<string>();
            }

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                sourceLabel = "none";
                return Array.Empty<string>();
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            sourceLabel = $"embedded:{resourceName}";
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                lines.Add(reader.ReadLine() ?? string.Empty);
            }

            return lines.ToArray();
        }

        private static string[] ReadAllLinesWithEncodingFallback(string path)
        {
            try
            {
                return File.ReadAllLines(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
            }
            catch (DecoderFallbackException)
            {
                return File.ReadAllLines(path, Encoding.Default);
            }
        }

        private static bool TryParseHash(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string hex = trimmed.Substring(2);
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hexValue))
                {
                    value = unchecked((int)hexValue);
                    return true;
                }

                return false;
            }

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int signedValue))
            {
                value = signedValue;
                return true;
            }

            if (uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint unsignedValue))
            {
                value = unchecked((int)unsignedValue);
                return true;
            }

            if (uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint fallbackHexValue))
            {
                value = unchecked((int)fallbackHexValue);
                return true;
            }

            return false;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            if (line == null)
            {
                return values;
            }

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    values.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(c);
            }

            values.Add(sb.ToString());
            return values;
        }

        private void BuildMedalGroups()
        {
            var itemInfos = _bundle.ItemInfos ?? new List<CapsuleConfig>();
            var rateGroups = _bundle.RateGroups ?? new List<CapsuleRateGroup>();
            var machineGroups = _bundle.MachineGroups ?? new List<CapsuleMachineGroup>();

            var itemByCapsuleHash = new Dictionary<int, CapsuleConfig>();
            foreach (var item in itemInfos)
            {
                if (!itemByCapsuleHash.ContainsKey(item.CapsuleHash))
                {
                    itemByCapsuleHash[item.CapsuleHash] = item;
                }
            }

            var routeRefsByCoinHash = new Dictionary<int, List<(int MachineHash, CapsuleMachineRoute Route)>>();
            foreach (var machineGroup in machineGroups)
            {
                foreach (var route in machineGroup.Routes ?? new List<CapsuleMachineRoute>())
                {
                    if (!routeRefsByCoinHash.TryGetValue(route.CoinHash, out var refs))
                    {
                        refs = new List<(int MachineHash, CapsuleMachineRoute Route)>();
                        routeRefsByCoinHash[route.CoinHash] = refs;
                    }

                    refs.Add((machineGroup.MachineHash, route));
                }
            }

            var groups = new List<CapsuleGroupViewModel>();
            var referencedCapsuleHashes = new HashSet<int>();

            foreach (var rateGroup in rateGroups)
            {
                var slotViewModels = new List<CapsuleConfigViewModel>();
                foreach (var rateSlot in rateGroup.Slots ?? new List<CapsuleRateSlot>())
                {
                    referencedCapsuleHashes.Add(rateSlot.CapsuleHash);
                    bool hasLinkedItemInfo = itemByCapsuleHash.TryGetValue(rateSlot.CapsuleHash, out var itemInfo);
                    itemInfo ??= new CapsuleConfig { CapsuleHash = rateSlot.CapsuleHash };

                    slotViewModels.Add(new CapsuleConfigViewModel(itemInfo, rateSlot, this, OnDataChanged, hasLinkedItemInfo));
                }

                var routeModels = routeRefsByCoinHash.TryGetValue(rateGroup.CoinHash, out var refs)
                    ? refs.Select(x => new CapsuleMachineRouteViewModel(x.MachineHash, x.Route, OnDataChanged)).ToList()
                    : new List<CapsuleMachineRouteViewModel>();

                bool reachable = routeModels.Count > 0;
                var group = new CapsuleGroupViewModel(rateGroup, slotViewModels, routeModels, reachable, isUnmappedGroup: false, coinDisplayNameResolver: ResolveCoinDisplayName);
                foreach (var slot in slotViewModels)
                {
                    slot.AttachGroup(group);
                }

                group.RecalculateRates();
                groups.Add(group);
            }

            var unmappedSlots = itemInfos
                .Where(x => !referencedCapsuleHashes.Contains(x.CapsuleHash))
                .Select(x => new CapsuleConfigViewModel(x, null, this, OnDataChanged, hasLinkedItemInfo: true))
                .ToList();

            if (unmappedSlots.Count > 0)
            {
                var unmappedGroup = CapsuleGroupViewModel.CreateUnmapped(unmappedSlots);
                foreach (var slot in unmappedSlots)
                {
                    slot.AttachGroup(unmappedGroup);
                }

                unmappedGroup.RecalculateRates();
                groups.Add(unmappedGroup);
            }

            foreach (var group in groups
                .OrderBy(x => x.IsUnmappedGroup ? 1 : 0)
                .ThenBy(x => x.CoinId)
                .ThenBy(x => x.CoinHash))
            {
                MedalGroups.Add(group);
            }
        }
        private string? ResolveCoinDisplayName(int hash)
        {
            if (_userHashNameMap.TryGetValue(hash, out string? name) &&
                !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            if (HashToNameMap.TryGetValue(hash, out string? mappedName) &&
                !string.IsNullOrWhiteSpace(mappedName))
            {
                return mappedName;
            }

            if (TryResolveNoun(_itemNames, hash, out string itemName))
            {
                return $"[Item] {itemName}";
            }

            if (TryResolveNoun(_yokaiNames, hash, out string yokaiName))
            {
                return $"[Yokai] {yokaiName}";
            }

            return null;
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
                if (_yw2Game == null) return true;

                var validationError = ValidateBeforeSave();
                if (!string.IsNullOrEmpty(validationError))
                {
                    System.Windows.MessageBox.Show(validationError, "Crank-a-kai Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                _yw2Game.SaveCapsuleDataBundle(_bundle);

                _isDirty = false;
                this.RaisePropertyChanged(nameof(HasPendingChanges));
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Crank-a-kai Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"[CrankakaiVM] Save error: {ex.Message}");
                return false;
            }
        }

        private string? ValidateBeforeSave()
        {
            var itemHashes = new HashSet<int>((_bundle.ItemInfos ?? new List<CapsuleConfig>()).Select(x => x.CapsuleHash));
            var rateHashes = new HashSet<int>((_bundle.RateGroups ?? new List<CapsuleRateGroup>()).Select(x => x.CoinHash));

            foreach (var rateGroup in _bundle.RateGroups ?? new List<CapsuleRateGroup>())
            {
                foreach (var slot in rateGroup.Slots ?? new List<CapsuleRateSlot>())
                {
                    if (!itemHashes.Contains(slot.CapsuleHash))
                    {
                        return $"���� �ߴ�: RateSlot CapsuleHash(0x{slot.CapsuleHash:X8})�� ItemInfos�� �����ϴ�.";
                    }
                }
            }

            foreach (var machineGroup in _bundle.MachineGroups ?? new List<CapsuleMachineGroup>())
            {
                foreach (var route in machineGroup.Routes ?? new List<CapsuleMachineRoute>())
                {
                    if (!rateHashes.Contains(route.CoinHash))
                    {
                        return $"���� �ߴ�: MachineRoute CoinHash(0x{route.CoinHash:X8})�� RateGroups�� �����ϴ�.";
                    }
                }
            }

            return null;
        }

        public void RevertChanges()
        {
            LoadData();
            _isDirty = false;
            this.RaisePropertyChanged(nameof(HasPendingChanges));
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
            catch
            {
                return null;
            }
        }

        private static string ResolveName(T2bText? textObj, int hash, string type, int idHash)
        {
            if (TryResolveNoun(textObj, hash, out string resolved))
            {
                return $"[{type}] {resolved}";
            }

            return $"[{type}] Unknown (0x{idHash:X8})";
        }

        private static bool TryResolveNoun(T2bText? textObj, int hash, out string name)
        {
            name = string.Empty;
            if (textObj?.Nouns == null ||
                !textObj.Nouns.TryGetValue(hash, out var cfg) ||
                cfg?.Strings == null ||
                cfg.Strings.Count == 0)
            {
                return false;
            }

            string first = cfg.Strings[0].Text;
            if (string.IsNullOrWhiteSpace(first))
            {
                return false;
            }

            name = first;
            return true;
        }
    }

    public class CapsuleConfigViewModel : ReactiveObject
    {
        private readonly CapsuleConfig _itemModel;
        private readonly CapsuleRateSlot? _rateModel;
        private readonly CrankakaiViewModel _parent;
        private readonly Action _onChanged;
        private readonly bool _hasLinkedItemInfo;
        private CapsuleGroupViewModel? _group;
        private double _ratePercent;

        public CapsuleConfigViewModel(
            CapsuleConfig itemModel,
            CapsuleRateSlot? rateModel,
            CrankakaiViewModel parent,
            Action onChanged,
            bool hasLinkedItemInfo)
        {
            _itemModel = itemModel;
            _rateModel = rateModel;
            _parent = parent;
            _onChanged = onChanged;
            _hasLinkedItemInfo = hasLinkedItemInfo;
        }

        public void AttachGroup(CapsuleGroupViewModel group)
        {
            _group = group;
            this.RaisePropertyChanged(nameof(IsReachable));
            this.RaisePropertyChanged(nameof(SlotStatus));
        }

        public void SetRatePercent(double percent)
        {
            if (Math.Abs(_ratePercent - percent) < 0.0001) return;
            _ratePercent = percent;
            this.RaisePropertyChanged(nameof(RatePercent));
            this.RaisePropertyChanged(nameof(RatePercentText));
        }

        public bool HasRateSlot => _rateModel != null;
        public bool CanEditRateWeight => _rateModel != null;
        public bool HasLinkedItemInfo => _hasLinkedItemInfo;
        public bool CanEditItemFields => _hasLinkedItemInfo;
        public bool IsReachable => _group?.IsReachable ?? false;
        public bool IsUnmappedSlot => !HasRateSlot;
        public int CapsuleHash => _rateModel?.CapsuleHash ?? _itemModel.CapsuleHash;
        public string CapsuleHashText => $"0x{CapsuleHash:X8}";
        public string CapsuleCapsuleId => CapsuleHash.ToString("X8");
        public string CapsuleTextId => _itemModel.CapsuleTextHash.ToString("X8");

        public int ItemOrYokaiHash
        {
            get => _itemModel.ItemOrYokaiHash;
            set
            {
                if (!_hasLinkedItemInfo || _itemModel.ItemOrYokaiHash == value) return;
                _itemModel.ItemOrYokaiHash = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(DisplayName));
                _onChanged();
            }
        }

        public string DisplayName => _parent.ResolveRewardName(ItemOrYokaiHash);

        public int RateWeightRaw
        {
            get => _rateModel?.RateWeight ?? 0;
            set
            {
                if (_rateModel == null || _rateModel.RateWeight == value) return;
                _rateModel.RateWeight = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(Weight));
                _group?.RecalculateRates();
                _onChanged();
            }
        }

        public int Weight
        {
            get => RateWeightRaw;
            set => RateWeightRaw = value;
        }

        public double RatePercent => _ratePercent;
        public string RatePercentText => HasRateSlot ? $"{RatePercent:F2}%" : "-";

        public int Unk1
        {
            get => _itemModel.Unk1;
            set
            {
                if (!_hasLinkedItemInfo || _itemModel.Unk1 == value) return;
                _itemModel.Unk1 = value;
                this.RaisePropertyChanged();
                _onChanged();
            }
        }

        public int Unk2
        {
            get => _itemModel.Unk2;
            set
            {
                if (!_hasLinkedItemInfo || _itemModel.Unk2 == value) return;
                _itemModel.Unk2 = value;
                this.RaisePropertyChanged();
                _onChanged();
            }
        }

        public int Unk3
        {
            get => _itemModel.Unk3;
            set
            {
                if (!_hasLinkedItemInfo || _itemModel.Unk3 == value) return;
                _itemModel.Unk3 = value;
                this.RaisePropertyChanged();
                _onChanged();
            }
        }

        public int Unk4
        {
            get => _itemModel.Unk4;
            set
            {
                if (!_hasLinkedItemInfo || _itemModel.Unk4 == value) return;
                _itemModel.Unk4 = value;
                this.RaisePropertyChanged();
                _onChanged();
            }
        }

        public string SlotStatus
        {
            get
            {
                if (IsUnmappedSlot) return "�̸���";
                if (!HasLinkedItemInfo) return "������ ����";
                return IsReachable ? "�ǻ��" : "�̻��";
            }
        }
    }
}



