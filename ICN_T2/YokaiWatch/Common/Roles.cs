using System.Collections.Generic;

namespace ICN_T2.YokaiWatch.Common
{
    public static class Roles
    {
        public static readonly Dictionary<int, string> YWB = new()
        {
            {0x00, "Unrole"},
            {0x01, "Fighter"},
            {0x02, "Tank"},
            {0x03, "Healer"},
            {0x04, "Ranger"},
        };
    }
}