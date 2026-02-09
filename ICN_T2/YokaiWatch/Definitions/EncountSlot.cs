namespace ICN_T2.YokaiWatch.Definitions
{
    public class EncountSlot
    {
        public int ParamHash { get; set; }  // 어떤 요괴인가
        public int Level { get; set; }      // 최소 레벨
        public int MaxLevel { get; set; }   // 최대 레벨
        public int Weight { get; set; }     // 등장 확률(가중치)
    }
}