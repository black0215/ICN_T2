using Albatross.Yokai_Watch.Logic;

namespace Albatross.Yokai_Watch.Games.YW3.Logic
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
        public int Unk6 { get; set; }

        // Keep serialized layout unchanged while satisfying interface contract.
        int IEncountChara.MaxLevel { get => Unk1; set => Unk1 = value; }
        int IEncountChara.Weight { get => Unk2; set => Unk2 = value; }
    }
}
