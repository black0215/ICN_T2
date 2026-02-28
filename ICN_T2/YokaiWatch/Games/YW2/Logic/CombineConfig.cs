using ICN_T2.YokaiWatch.Definitions;

namespace ICN_T2.YokaiWatch.Games.YW2.Logic
{
    public class CombineConfig : ICombineConfig
    {
        public bool BaseIsItem { get; set; }
        public int BaseHash { get; set; }
        public bool MaterialIsItem { get; set; }
        public int MaterialHash { get; set; }
        public bool EvolveToIsItem { get; set; }
        public int EvolveToHash { get; set; }
        public int CombineConfigHash { get; set; }
        public bool CombineIsItem { get; set; }
        public int OniOrbCost { get; set; }
    }
}
