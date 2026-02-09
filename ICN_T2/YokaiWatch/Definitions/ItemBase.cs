namespace ICN_T2.YokaiWatch.Definitions
{
    public class ItemBase
    {
        public int ItemHash { get; set; }
        public int NameHash { get; set; }
        public int ItemNumber { get; set; }
        public int MaxQuantity { get; set; }
        public bool CanBeBuy { get; set; }
        public bool CanBeSell { get; set; }
        public int SellPrize { get; set; }
        public int ItemPosX { get; set; }
        public int ItemPosY { get; set; }
        public int DescriptionHash { get; set; }
    }

    public class Consumable : ItemBase
    {
        public int Effect1Hash { get; set; }
        public int Effect2Hash { get; set; }
    }
}
