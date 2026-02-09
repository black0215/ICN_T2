using ICN_T2.YokaiWatch.Definitions;

namespace ICN_T2.YokaiWatch.Games.YW2.Logic
{
    public class Charaevolve : Evolution
    {
        public new int Level { get => base.Level; set => base.Level = value; }
        public new int ParamHash { get => base.ParamHash; set => base.ParamHash = value; }
    }
}
