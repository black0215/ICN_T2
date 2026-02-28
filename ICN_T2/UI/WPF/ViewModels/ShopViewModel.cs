using ICN_T2.UI.WPF.ViewModels.Contracts;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.Logic.Level5.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using T2bText = ICN_T2.Logic.Level5.Text.T2bþ;
using System.Linq;
using System;

namespace ICN_T2.UI.WPF.ViewModels
{


    public class ShopItemViewModel : ReactiveObject
    {
        private readonly ShopConfig _config;
        private readonly Action _onChanged;
        private readonly ShopValidCondition _condition;

        public ShopItemViewModel(ShopConfig config, ShopValidCondition condition, Action onChanged)
        {
            _config = config;
            _condition = condition;
            _onChanged = onChanged;
        }

        public int ItemHash
        {
            get => _config.ItemHash;
            set { _config.ItemHash = value; this.RaisePropertyChanged(); _onChanged(); }
        }

        public int Price
        {
            get => _config.Price;
            set
            {
                _config.Price = value;
                if (_condition != null)
                {
                    _condition.Price = value;
                }
                this.RaisePropertyChanged();
                _onChanged();
            }
        }
    }

    public class ShopFileViewModel : ReactiveObject
    {
        private readonly IGame _game;
        private readonly string _shopName;
        private readonly Action _onChanged;

        public string DisplayName => _shopName;
        public ObservableCollection<ShopItemViewModel> Items { get; } = new ObservableCollection<ShopItemViewModel>();

        // We hold the raw arrays to save them back easily
        public ShopConfig[] RawConfigs { get; private set; }
        public ShopValidCondition[] RawConditions { get; private set; }

        public ShopFileViewModel(IGame game, string shopName, Action onChanged)
        {
            _game = game;
            _shopName = shopName;
            _onChanged = onChanged;
        }

        public void Load()
        {
            var data = _game.GetShop(_shopName);
            RawConfigs = data.Item1 ?? Array.Empty<ShopConfig>();
            RawConditions = data.Item2 ?? Array.Empty<ShopValidCondition>();

            Items.Clear();
            foreach (var cfg in RawConfigs)
            {
                ShopValidCondition cond = null;
                if (cfg.ShopValidConditionIndex >= 0 && cfg.ShopValidConditionIndex < RawConditions.Length)
                {
                    cond = RawConditions[cfg.ShopValidConditionIndex];
                }
                Items.Add(new ShopItemViewModel(cfg, cond, _onChanged));
            }
        }

        public void Save()
        {
            _game.SaveShop(_shopName, RawConfigs, RawConditions);
        }
    }

    public class ShopViewModel : ReactiveObject, IToolSaveParticipant
    {
        private readonly IGame _game;
        private bool _isDirty;
        private T2bText? _itemNames;

        public ObservableCollection<ShopFileViewModel> Shops { get; } = new ObservableCollection<ShopFileViewModel>();

        private ShopFileViewModel _selectedShop;
        public ShopFileViewModel SelectedShop
        {
            get => _selectedShop;
            set
            {
                if (_selectedShop != value)
                {
                    _selectedShop = value;
                    if (_selectedShop != null && _selectedShop.Items.Count == 0)
                    {
                        _selectedShop.Load();
                    }
                    this.RaisePropertyChanged();
                }
            }
        }

        public ObservableCollection<HashOption> ItemOptions { get; } = new ObservableCollection<HashOption>();

        public bool HasPendingChanges => _isDirty;

        public ShopViewModel(IGame game)
        {
            _game = game;
            LoadData();
        }

        private void LoadData()
        {
            _itemNames = LoadTextResource("item_text");

            ItemOptions.Clear();
            var items = _game.GetItems("all") ?? Array.Empty<ItemBase>();

            // Re-use an empty option so users can clear a slot if allowed, or just provide known items
            foreach (var it in items)
            {
                ItemOptions.Add(new HashOption(it.ItemHash, ResolveItemDisplayName(it)));
            }

            Shops.Clear();
            if (_game.Files != null && _game.Files.ContainsKey("shop"))
            {
                var folderPath = _game.Files["shop"].Path;
                if (_game.Game != null && _game.Game.Directory != null)
                {
                    var shopFolder = _game.Game.Directory.GetFolderFromFullPathSafe(folderPath);
                    if (shopFolder != null && shopFolder.Files != null)
                    {
                        var shopKeys = shopFolder.Files.Keys.Where(x => x.StartsWith("shop_shp")).OrderBy(x => x).ToList();
                        foreach (var key in shopKeys)
                        {
                            Shops.Add(new ShopFileViewModel(_game, key, OnShopChanged));
                        }
                    }
                }
            }
        }

        private void OnShopChanged()
        {
            _isDirty = true;
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShopViewModel] Failed to load text resource '{key}': {ex.Message}");
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
            if (TryResolveNoun(_itemNames, item.NameHash, out var name))
                return name;

            return $"0x{item.ItemHash:X8}";
        }

        public bool SavePendingChanges()
        {
            try
            {
                foreach (var shop in Shops)
                {
                    // Only save shops that have been loaded
                    if (shop.Items.Count > 0)
                    {
                        shop.Save();
                    }
                }
                _isDirty = false;
                this.RaisePropertyChanged(nameof(HasPendingChanges));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShopVM] Save error: {ex.Message}");
                return false;
            }
        }
    }
}
