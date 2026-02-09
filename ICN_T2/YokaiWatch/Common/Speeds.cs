using System.Collections.Generic;

namespace ICN_T2.YokaiWatch.Common
{
    public static class Speeds
    {
        public static readonly Dictionary<int, string> YWB = new()
        {
            {0x00, "Normal" },
            {0x01, "Fast" },
            {0x02, "Slow" },
        };
    }
}