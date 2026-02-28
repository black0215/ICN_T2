using System;

namespace ICN_T2.YokaiWatch.Definitions
{
    public interface IItableDataMore
    {
        int ItemHash { get; set; }
        int Quantity { get; set; }
        int Percentage { get; set; }
        int Unk1 { get; set; }
        int Unk2 { get; set; }
    }

    public class ItableDataMore : IItableDataMore
    {
        public int ItemHash { get; set; }
        public int Quantity { get; set; }
        public int Percentage { get; set; }
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
}
