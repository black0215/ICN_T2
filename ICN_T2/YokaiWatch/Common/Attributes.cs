using System.Collections.Generic;

namespace ICN_T2.YokaiWatch.Common
{
    public static class Attributes
    {
        public static readonly Dictionary<int, string> YW = new()
        {
            {0x00, "Untype" }, {0x01, "Fire" }, {0x02, "Water" }, {0x03, "Lightning" },
            {0x04, "Earth" }, {0x05, "Ice" }, {0x06, "Wind" }, {0x07, "Drain" },
            {0x08, "Strong Attack" }, {0x09, "Restoration" },
        };
    }
}