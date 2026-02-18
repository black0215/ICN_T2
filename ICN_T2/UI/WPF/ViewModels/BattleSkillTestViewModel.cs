using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2.Logic;
using T2bText = ICN_T2.Logic.Level5.Text.T2bþ;

namespace ICN_T2.UI.WPF.ViewModels
{
    public class BattleSkillTestViewModel : INotifyPropertyChanged
    {
        private readonly IGame _game;
        private string _searchText = string.Empty;
        private ObservableCollection<BattleCommandTestWrapper> _skills;

        public event PropertyChangedEventHandler? PropertyChanged;

        public BattleSkillTestViewModel(IGame game)
        {
            _game = game;
            _skills = new ObservableCollection<BattleCommandTestWrapper>();
            FilteredSkills = new ObservableCollection<BattleCommandTestWrapper>();
            LoadData();
        }

        public ObservableCollection<BattleCommandTestWrapper> FilteredSkills { get; }

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

        private void LoadData()
        {
            var skills = _game.GetBattleCommands();
            var battleText = LoadTextResource("battle_text");

            _skills.Clear();
            if (skills == null) return;

            foreach (var skill in skills)
            {
                string name = "Unknown";
                if (battleText != null && battleText.Nouns.TryGetValue(skill.NameHash, out var cfg) && cfg.Strings.Count > 0)
                {
                    name = cfg.Strings[0].Text;
                }

                // Cast to Logic.Battlecommand to access Unk fields if possible, or use reflection/dynamic if properties are hidden
                // Since I reverted BattleCommand.cs, I need to cast to the Logic class which has Unk fields
                var logicSkill = skill as Battlecommand;

                if (logicSkill != null)
                {
                    _skills.Add(new BattleCommandTestWrapper(logicSkill, name));
                }
                else
                {
                    // Fallback if casting fails (shouldn't if GetBattleCommands returns logic types)
                    // But wait, GetBattleCommands returns Definitions.BattleCommand array.
                    // YW2.cs LINQ casts to BattleCommand (base). The runtime type should be Battlecommand (logic).
                    _skills.Add(new BattleCommandTestWrapper(skill, name));
                }
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            FilteredSkills.Clear();
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                foreach (var skill in _skills) FilteredSkills.Add(skill);
            }
            else
            {
                foreach (var skill in _skills.Where(x => x.Name.Contains(_searchText) || x.Hash.ToString("X8").Contains(_searchText.ToUpper())))
                {
                    FilteredSkills.Add(skill);
                }
            }
        }

        private T2bText? LoadTextResource(string key)
        {
            if (_game.Files == null || !_game.Files.TryGetValue(key, out var file)) return null;
            var vf = file.GetStream();
            if (vf == null) return null;
            byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
            if (data == null || data.Length == 0) return null;
            return new T2bText(data);
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class BattleCommandTestWrapper
    {
        private readonly BattleCommand _model;
        private readonly Battlecommand? _logicModel;

        public string Name { get; }
        public int Hash => _model.BattleCommandHash;
        public int PowerMultiplicator => _model.PowerMultiplicator;
        public int TextureHash => _model.TextureHash;
        public int SkillConfigHash => _model.SkillConfigHash;

        // Logic specific fields
        public int Unk1 => _logicModel?.Unk1 ?? 0;
        public int Unk2 => _logicModel?.Unk2 ?? 0;
        public int Unk3 => _logicModel?.Unk3 ?? 0;
        public int Unk4 => _logicModel?.Unk4 ?? 0;
        public int Unk5 => _logicModel?.Unk5 ?? 0;
        public int Unk6 => _logicModel?.Unk6 ?? 0;
        public int Unk7 => _logicModel?.Unk7 ?? 0;
        public int Unk8 => _logicModel?.Unk8 ?? 0;
        public int Unk9 => _logicModel?.Unk9 ?? 0;
        public int Unk10 => _logicModel?.Unk10 ?? 0;
        public int Unk11 => _logicModel?.Unk11 ?? 0;
        public int Unk12 => _logicModel?.Unk12 ?? 0;
        public int Unk13 => _logicModel?.Unk13 ?? 0;
        public int Unk14 => _logicModel?.Unk14 ?? 0;

        public BattleCommandTestWrapper(BattleCommand model, string name)
        {
            _model = model;
            _logicModel = model as Battlecommand;
            Name = name;
        }
    }
}
