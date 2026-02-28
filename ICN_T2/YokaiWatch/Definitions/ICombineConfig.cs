namespace ICN_T2.YokaiWatch.Definitions
{
    public interface ICombineConfig
    {
        bool BaseIsItem { get; set; }
        int BaseHash { get; set; }
        bool MaterialIsItem { get; set; }
        int MaterialHash { get; set; }
        bool EvolveToIsItem { get; set; }
        int EvolveToHash { get; set; }
        int CombineConfigHash { get; set; }
        bool CombineIsItem { get; set; }
        int OniOrbCost { get; set; }
    }
}
