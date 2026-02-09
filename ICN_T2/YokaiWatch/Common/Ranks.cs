using System.Collections.Generic;

namespace ICN_T2.YokaiWatch.Common
{
    public static class Ranks
    {
        public static readonly Dictionary<int, string> YW = new()
        {
            {0x00, "E"},
            {0x01, "D"},
            {0x02, "C"},
            {0x03, "B"},
            {0x04, "A"},
            {0x05, "S"},
            {0x0F, "Unrank"},
        };
    }
}