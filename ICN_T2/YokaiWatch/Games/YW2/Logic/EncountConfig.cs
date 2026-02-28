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

    /// <summary>
    /// common_enc ENCOUNT_CHARA 슬롯 (7 variables).
    /// File order: v[0]=ParamHash, v[1]=Level, v[2]=Weight, v[3..6]=Unk2-5.
    /// ToClass&lt;T&gt;가 DeclaredOnly 프로퍼티를 MetadataToken 순서로 매핑하므로,
    /// Unk 필드를 먼저 선언하고 Weight/MaxLevel alias를 마지막에 둬야
    /// v[2]→Unk1(Weight) 매핑이 올바르게 이뤄진다.
    /// </summary>
    public class EncountChara : EncountSlot
    {
        public new int ParamHash { get => base.ParamHash; set => base.ParamHash = value; }
        public new int Level { get => base.Level; set => base.Level = value; }
        // Unk 필드 먼저 선언 (v[2..6] 직접 매핑)
        public int Unk1 { get; set; }   // v[2] = Weight
        public int Unk2 { get; set; }   // v[3]
        public int Unk3 { get; set; }   // v[4]
        public int Unk4 { get; set; }   // v[5]
        public int Unk5 { get; set; }   // v[6]
        // alias 프로퍼티 — Unk 뒤에 선언해야 ToClass 이중 덮어쓰기 방지
        public new int Weight { get => Unk1; set => Unk1 = value; }
        public new int MaxLevel { get => Unk2; set => Unk2 = value; }
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
