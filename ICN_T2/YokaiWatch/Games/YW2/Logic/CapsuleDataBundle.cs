using System.Collections.Generic;

namespace ICN_T2.YokaiWatch.Games.YW2.Logic
{
    public sealed class CapsuleDataBundle
    {
        public List<CapsuleConfig> ItemInfos { get; set; } = new List<CapsuleConfig>();
        public List<CapsuleRateGroup> RateGroups { get; set; } = new List<CapsuleRateGroup>();
        public List<CapsuleMachineGroup> MachineGroups { get; set; } = new List<CapsuleMachineGroup>();
    }

    public sealed class CapsuleRateGroup
    {
        public int CoinHash { get; set; }
        public int CoinId { get; set; }
        public List<CapsuleRateSlot> Slots { get; set; } = new List<CapsuleRateSlot>();
    }

    public sealed class CapsuleRateSlot
    {
        public int CapsuleHash { get; set; }
        public int RateWeight { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
    }

    public sealed class CapsuleMachineGroup
    {
        public int MachineHash { get; set; }
        public List<CapsuleMachineRoute> Routes { get; set; } = new List<CapsuleMachineRoute>();
    }

    public sealed class CapsuleMachineRoute
    {
        public int RouteHash { get; set; }
        public int RouteMode { get; set; }
        public int CoinHash { get; set; }
        public int Unk4 { get; set; }
    }
}
