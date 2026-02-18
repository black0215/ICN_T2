namespace Albatross.Yokai_Watch.Logic
{
    public class IEncountTable
    {
        public int EncountConfigHash { get; set; }
        public int[] EncountOffsets { get; set; }
        public int CharaCount { get; set; }
        public int[] Charas { get; set; }
    }

    public interface IEncountChara
    {
        int ParamHash { get; set; }
        int Level { get; set; }
        int MaxLevel { get; set; }
        int Weight { get; set; }
    }
}
