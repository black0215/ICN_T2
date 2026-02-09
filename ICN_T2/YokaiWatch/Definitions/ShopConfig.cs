namespace ICN_T2.YokaiWatch.Definitions
{
    public class ShopConfig
    {
        public int ShopConfigHash { get; set; }
        public int ItemHash { get; set; }
        public int Price { get; set; }
        public int ShopValidConditionIndex { get; set; }
    }

    public class ShopEntry
    {
        public int Price { get; set; }
        public int Quantity { get; set; }
    }
}
