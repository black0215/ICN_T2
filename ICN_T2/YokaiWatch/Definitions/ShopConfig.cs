namespace ICN_T2.YokaiWatch.Definitions
{
    public class ShopConfig
    {
        public int ShopConfigHash { get; set; }
        public int ItemHash { get; set; }
        public int Price { get; set; }
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public int ShopValidConditionIndex { get; set; }
        // public object Unk7 { get; set; } // May vary, ignore for pure reading if not strict
    }

    public class ShopValidCondition
    {
        public int Price { get; set; }
        // public object Condition { get; set; } 
    }

    public class ShopEntry
    {
        public int Price { get; set; }
        public int Quantity { get; set; }
    }
}
