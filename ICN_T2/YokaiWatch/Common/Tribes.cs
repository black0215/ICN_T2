using System.Collections.Generic;

namespace ICN_T2.YokaiWatch.Common
{
    public static class Tribes
    {
        public static readonly Dictionary<int, string> YW1 = new()
        {
            {0x00, "Untribe" }, {0x01, "Brave" }, {0x02, "Mysterious" }, {0x03, "Tough" },
            {0x04, "Charming" }, {0x05, "Heartful" }, {0x06, "Shady" }, {0x07, "Eerie" },
            {0x08, "Slippery" }, {0x09, "Boss" },
        };

        public static readonly Dictionary<int, string> YW2 = new()
        {
            {0x00, "Untribe" }, {0x01, "용맹" }, {0x02, "불가사의" }, {0x03, "호걸" },
            {0x04, "프리티" }, {0x05, "따끈따끈" }, {0x06, "어스름" }, {0x07, "불쾌" },
            {0x08, "뽀로롱" }, {0x09, "마괴" }, {0x0A, "Boss" },
        };

        public static readonly Dictionary<int, string> YW3 = new()
        {
            {0x00, "Untribe" }, {0x01, "Brave" }, {0x02, "Mysterious" }, {0x03, "Tough" },
            {0x04, "Charming" }, {0x05, "Heartful" }, {0x06, "Shady" }, {0x07, "Eerie" },
            {0x08, "Slippery" }, {0x09, "Wicked" }, {0x0A, "Enma" }, {0x0B, "Wandroid" },
            {0x0C, "Boss" },
        };

        public static readonly Dictionary<int, string> YWB = new()
        {
            {0x00, "Boss" }, {0x01, "Brave" }, {0x02, "Mysterious" }, {0x03, "Tough" },
            {0x04, "Charming" }, {0x05, "Heartful" }, {0x06, "Shady" }, {0x07, "Eerie" },
            {0x08, "Slippery" }, {0x09, "Wicked" }, {0x0A, "Enma" },
        };
    }
}