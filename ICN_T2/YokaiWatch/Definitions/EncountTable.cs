namespace ICN_T2.YokaiWatch.Definitions
{
    public class EncountTable
    {
        public int EncountConfigHash { get; set; }
        public int[] EncountOffsets { get; set; } = [];
        public int CharaCount { get; set; }
        public int[] Charas { get; set; } = [];
    }
}
