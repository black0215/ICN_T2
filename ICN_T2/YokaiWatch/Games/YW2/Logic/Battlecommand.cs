using ICN_T2.YokaiWatch.Definitions;

namespace ICN_T2.YokaiWatch.Games.YW2.Logic
{
    // Rename class to avoid conflict if needed, or fully qualify base. Base is BattleCommand.
    public class Battlecommand : BattleCommand
    {
        public new int BattleCommandHash { get => base.BattleCommandHash; set => base.BattleCommandHash = value; }
        public new int NameHash { get => base.NameHash; set => base.NameHash = value; }
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public new int PowerMultiplicator { get => base.PowerMultiplicator; set => base.PowerMultiplicator = value; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public new int TextureHash { get => base.TextureHash; set => base.TextureHash = value; }
        public new int SkillConfigHash { get => base.SkillConfigHash; set => base.SkillConfigHash = value; }
        public int Unk7 { get; set; }
        public int Unk8 { get; set; }
        public int Unk9 { get; set; }
        public int Unk10 { get; set; }
        public int Unk11 { get; set; }
        public int Unk12 { get; set; }
        public int Unk13 { get; set; }
        public int Unk14 { get; set; }
    }
}
