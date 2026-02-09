using ICN_T2.YokaiWatch.Definitions;

namespace ICN_T2.YokaiWatch.Games.YW2.Logic
{
    public class EncountTable : ICN_T2.YokaiWatch.Definitions.EncountTable
    {
        public new int EncountConfigHash { get => base.EncountConfigHash; set => base.EncountConfigHash = value; }
        public new int[] EncountOffsets { get => base.EncountOffsets; set => base.EncountOffsets = value; }
        public int[] UnkBlock = new int[23];

        public EncountTable()
        {
            EncountOffsets = new int[6];
        }
    }

    public class EncountChara : EncountSlot
    {
        public new int ParamHash { get => base.ParamHash; set => base.ParamHash = value; }
        public new int Level { get => base.Level; set => base.Level = value; }
        public new int MaxLevel { get => Unk1; set => Unk1 = value; }
        public new int Weight { get => Unk2; set => Unk2 = value; }
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
    }

    public class YokaiSpotChara : EncountSlot
    {
        // 9 Variables Structure from YS_YOKAI
        public new int ParamHash { get => base.ParamHash; set => base.ParamHash = value; }      // 0
        public int Unk1 { get; set; }           // 1
        public int Unk2 { get; set; }           // 2
        public int Unk3 { get; set; }           // 3
        public int Unk4 { get; set; }           // 4
        public float Unk5 { get; set; }         // 5 (Float)
        public new int Level { get => base.Level; set => base.Level = value; }          // 6
        public int Unk7 { get; set; }           // 7
        public int Unk8 { get; set; }           // 8

        // Interface Mapping
        public new int MaxLevel { get => Unk3; set => Unk3 = value; }
        public new int Weight { get => Unk8; set => Unk8 = value; }
    }
}
