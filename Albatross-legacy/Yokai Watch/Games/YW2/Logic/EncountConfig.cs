using Albatross.Yokai_Watch.Logic;

namespace Albatross.Yokai_Watch.Games.YW2.Logic
{
    public class EncountTable : IEncountTable
    {
        public new int EncountConfigHash { get => base.EncountConfigHash; set => base.EncountConfigHash = value; }
        public new int[] EncountOffsets { get => base.EncountOffsets; set => base.EncountOffsets = value; }
        public int[] UnkBlock = new int[23];

        public EncountTable()
        {
            EncountOffsets = new int[6];
        }
    }

    public class EncountChara : IEncountChara
    {
        public int ParamHash { get; set; }
        public int Level { get; set; }
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }

        // Keep serialized layout unchanged while satisfying interface contract.
        int IEncountChara.MaxLevel { get => Unk1; set => Unk1 = value; }
        int IEncountChara.Weight { get => Unk2; set => Unk2 = value; }
    }

    public class YokaiSpotChara : IEncountChara
    {
        // 9-variable layout used by yokaispot encounter entries.
        public int ParamHash { get; set; } // 0
        public int Unk1 { get; set; }      // 1
        public int Unk2 { get; set; }      // 2
        public int Unk3 { get; set; }      // 3 (MaxLevel)
        public int Unk4 { get; set; }      // 4
        public float Unk5 { get; set; }    // 5
        public int Level { get; set; }     // 6
        public int Unk7 { get; set; }      // 7
        public int Unk8 { get; set; }      // 8 (Weight)

        int IEncountChara.MaxLevel { get => Unk3; set => Unk3 = value; }
        int IEncountChara.Weight { get => Unk8; set => Unk8 = value; }
    }
}
