namespace ICN_T2.YokaiWatch.Games.YW2.Logic
{
    public class ICapsuleConfig
    {
        public int CapsuleHash { get; set; }
        public int ItemOrYokaiHash { get; set; }
        public int CapsuleTextHash { get; set; }
    }

    public class CapsuleConfig : ICapsuleConfig
    {
        public new int CapsuleHash { get => base.CapsuleHash; set => base.CapsuleHash = value; }
        /// <summary>추정: 확률 가중치 (Weight). 동일 메달 내 보상 풀에서의 비율/가중치.</summary>
        public int Unk1 { get; set; }
        public new int ItemOrYokaiHash { get => base.ItemOrYokaiHash; set => base.ItemOrYokaiHash = value; }
        public new int CapsuleTextHash { get => base.CapsuleTextHash; set => base.CapsuleTextHash = value; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public int Unk7 { get; set; }
        public int Unk8 { get; set; }
    }
}
