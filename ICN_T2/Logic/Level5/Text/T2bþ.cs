using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ICN_T2.Logic.Level5.Binary; // CfgBin, Entry, EntryType 위치

namespace ICN_T2.Logic.Level5.Text
{
    // 클래스 이름에 특수문자(þ)가 있지만, 원본 호환성을 위해 유지합니다.
    // (발음: T2bThorn, 혹은 T2bText)
    public class T2bþ : CfgBin
    {
        public Dictionary<int, TextConfig> Texts { get; set; }
        public Dictionary<int, TextConfig> Nouns { get; set; }

        public T2bþ()
        {
            Texts = new Dictionary<int, TextConfig>();
            Nouns = new Dictionary<int, TextConfig>();
            // 텍스트 파일은 무조건 UTF-8
            Encoding = Encoding.UTF8;
        }

        public T2bþ(Stream stream) : this()
        {
            Open(stream);
            ParseData();
        }

        public T2bþ(byte[] data) : this()
        {
            Open(data);
            ParseData();
        }

        // XML/Text 로딩 생성자 생략 (필요하면 추가 가능)

        private void ParseData()
        {
            // 1. 화자(Washa) 정보 로드
            int[] faces = Entries
                .Where(x => x.GetName() == "TEXT_WASHA_BEGIN")
                .SelectMany(x => x.Children)
                .Select(x => Convert.ToInt32(x.Variables[1].Value))
                .ToArray();

            // 2. 화자 설정(Config) 로드
            Dictionary<int, int> facesConfig = Entries
                .Where(x => x.GetName() == "TEXT_CONFIG_BEGIN")
                .SelectMany(x => x.Children)
                .ToDictionary(
                    x => Convert.ToInt32(x.Variables[0].Value),
                    y => Convert.ToInt32(y.Variables[2].Value)
                );

            // 3. 텍스트(Texts) 로드
            var textEntries = Entries
                .Where(x => x.GetName() == "TEXT_INFO_BEGIN")
                .SelectMany(x => x.Children);

            foreach (var group in textEntries.GroupBy(x => Convert.ToInt32(x.Variables[0].Value)))
            {
                int crc32 = group.Key;
                int washaID = -1;
                List<StringLevel5> strings = new List<StringLevel5>();

                // 화자 ID 찾기
                if (facesConfig.TryGetValue(crc32, out int configValue))
                {
                    if (configValue != -1 && configValue < faces.Length)
                    {
                        washaID = faces[configValue];
                    }
                }

                // 대사 목록 수집
                foreach (var entry in group)
                {
                    int variance = Convert.ToInt32(entry.Variables[1].Value);
                    // OffsetTextPair 안전하게 꺼내기
                    string text = entry.Variables[2].Value is OffsetTextPair pair ? pair.Text : entry.Variables[2].Value?.ToString();

                    strings.Add(new StringLevel5(variance, text));
                }

                Texts[crc32] = new TextConfig(strings, washaID);
            }

            // 4. 명사(Nouns) 로드
            var nounEntries = Entries
                .Where(x => x.GetName() == "NOUN_INFO_BEGIN")
                .SelectMany(x => x.Children);

            foreach (var group in nounEntries.GroupBy(x => Convert.ToInt32(x.Variables[0].Value)))
            {
                int crc32 = group.Key;
                List<StringLevel5> strings = new List<StringLevel5>();

                foreach (var entry in group)
                {
                    int variance = Convert.ToInt32(entry.Variables[1].Value);
                    // Nouns는 보통 5번째 변수에 텍스트가 있음 (Variables[5])
                    string text = entry.Variables[5].Value is OffsetTextPair pair ? pair.Text : entry.Variables[5].Value?.ToString();

                    strings.Add(new StringLevel5(variance, text));
                }

                Nouns[crc32] = new TextConfig(strings, -1);
            }
        }

        // --------------------------------------------------------------------
        // 저장 로직 (Save)
        // --------------------------------------------------------------------

        public new void Save(string fileName)
        {
            UpdateBeforeSave(true); // IEGO 모드 기본 활성화 (필요시 파라미터화)
            base.Save(fileName);
        }

        public new byte[] Save()
        {
            UpdateBeforeSave(true);
            return base.Save();
        }

        public byte[] Save(bool iego)
        {
            UpdateBeforeSave(iego);
            return base.Save();
        }

        private void UpdateBeforeSave(bool iego)
        {
            // 문자열 테이블 갱신을 위한 준비
            Dictionary<int, string> stringsTable = GetStringsTable();

            if (Texts.Count > 0)
            {
                Entry textEntry = GetTextEntry(stringsTable);
                ReplaceEntry("TEXT_INFO_BEGIN", textEntry);

                if (iego)
                {
                    Entry configEntry = GetTextConfigEntry();
                    Entry washaEntry = GetTextWashaEntry();

                    ReplaceEntry("TEXT_CONFIG_BEGIN", configEntry);
                    ReplaceEntry("TEXT_WASHA_BEGIN", washaEntry);
                }
            }

            if (Nouns.Count > 0)
            {
                Entry nounEntry = GetNounEntry(stringsTable);
                ReplaceEntry("NOUN_INFO_BEGIN", nounEntry);
            }

            // 부모 클래스(CfgBin)의 Strings 갱신
            Strings = stringsTable;
            MarkAsModified();
        }

        // --------------------------------------------------------------------
        // Helpers (Entry 생성)
        // --------------------------------------------------------------------

        private Entry GetTextEntry(Dictionary<int, string> stringsTable)
        {
            // TEXT_INFO_BEGIN_0 (Root)
            int totalCount = Texts.Values.Sum(list => list.Strings.Count);
            Entry root = new Entry("TEXT_INFO_BEGIN_0", new List<Variable> { new Variable(EntryType.Int, totalCount) }, Encoding)
            {
                EndTerminator = true
            };

            foreach (var kvp in Texts)
            {
                for (int i = 0; i < kvp.Value.Strings.Count; i++)
                {
                    var val = kvp.Value.Strings[i];
                    // 문자열 오프셋 찾기
                    int offset = stringsTable.FirstOrDefault(x => x.Value == val.Text).Key;

                    Entry item = new Entry($"TEXT_INFO_{i}", new List<Variable>
                    {
                        new Variable(EntryType.Int, kvp.Key),     // CRC32
                        new Variable(EntryType.Int, i),           // Variance Index
                        new Variable(EntryType.String, new OffsetTextPair(offset, val.Text)), // Text
                        new Variable(EntryType.Int, 0)            // Padding/Unk
                    }, Encoding);

                    root.Children.Add(item);
                }
            }
            return root;
        }

        private Entry GetTextConfigEntry()
        {
            int count = Texts.Count;
            Entry root = new Entry("TEXT_CONFIG_BEGIN_0", new List<Variable> { new Variable(EntryType.Int, count) }, Encoding)
            {
                EndTerminator = true
            };

            // 화자 ID 리스트 추출 (중복 제거)
            List<int> washas = Texts.Where(x => x.Value.WashaID != -1).Select(x => x.Value.WashaID).Distinct().ToList();

            int index = 0;
            foreach (var kvp in Texts)
            {
                Entry item = new Entry($"TEXT_CONFIG_{index}", new List<Variable>
                {
                    new Variable(EntryType.Int, kvp.Key), // CRC32
                    new Variable(EntryType.Int, kvp.Value.Strings.Count),
                    new Variable(EntryType.Int, washas.IndexOf(kvp.Value.WashaID)) // 화자 인덱스
                }, Encoding);

                root.Children.Add(item);
                index++;
            }
            return root;
        }

        private Entry GetTextWashaEntry()
        {
            int[] washas = Texts.Where(x => x.Value.WashaID != -1).Select(x => x.Value.WashaID).Distinct().ToArray();
            Entry root = new Entry("TEXT_WASHA_BEGIN_0", new List<Variable> { new Variable(EntryType.Int, washas.Length) }, Encoding)
            {
                EndTerminator = true
            };

            for (int i = 0; i < washas.Length; i++)
            {
                Entry item = new Entry($"TEXT_WASHA_{i}", new List<Variable>
                {
                    new Variable(EntryType.Int, i),
                    new Variable(EntryType.Int, washas[i])
                }, Encoding);

                root.Children.Add(item);
            }
            return root;
        }

        private Entry GetNounEntry(Dictionary<int, string> stringsTable)
        {
            int totalCount = Nouns.Values.Sum(list => list.Strings.Count);
            Entry root = new Entry("NOUN_INFO_BEGIN_0", new List<Variable> { new Variable(EntryType.Int, totalCount) }, Encoding)
            {
                EndTerminator = true
            };

            foreach (var kvp in Nouns)
            {
                for (int i = 0; i < kvp.Value.Strings.Count; i++)
                {
                    var val = kvp.Value.Strings[i];
                    int offset = stringsTable.FirstOrDefault(x => x.Value == val.Text).Key;

                    // NOUN_INFO는 구조가 큼 (변수 14개)
                    Entry item = new Entry($"NOUN_INFO_{i}", new List<Variable>
                    {
                        new Variable(EntryType.Int, kvp.Key), // CRC32
                        new Variable(EntryType.Int, i),       // Variance
                        new Variable(EntryType.String, new OffsetTextPair(-1, null)), // Empty
                        new Variable(EntryType.String, new OffsetTextPair(-1, null)),
                        new Variable(EntryType.String, new OffsetTextPair(-1, null)),
                        new Variable(EntryType.String, new OffsetTextPair(offset, val.Text)), // Actual Text (Var 5)
                        new Variable(EntryType.String, new OffsetTextPair(-1, null)),
                        new Variable(EntryType.String, new OffsetTextPair(-1, null)),
                        new Variable(EntryType.String, new OffsetTextPair(-1, null)),
                        new Variable(EntryType.String, new OffsetTextPair(-1, null)),
                        new Variable(EntryType.Int, 0),
                        new Variable(EntryType.Int, 0),
                        new Variable(EntryType.Int, 0),
                        new Variable(EntryType.Int, 0),
                    }, Encoding);

                    root.Children.Add(item);
                }
            }
            return root;
        }

        private Dictionary<int, string> GetStringsTable()
        {
            // 모든 텍스트 수집 및 중복 제거
            var allStrings = Texts.Values.SelectMany(x => x.Strings).Select(s => s.Text)
                .Concat(Nouns.Values.SelectMany(x => x.Strings).Select(s => s.Text))
                .Where(s => s != null)
                .Distinct();

            Dictionary<int, string> table = new Dictionary<int, string>();
            int offset = 0;

            foreach (string text in allStrings)
            {
                table.Add(offset, text);
                offset += Encoding.GetByteCount(text) + 1; // +1 for Null Terminator
            }
            return table;
        }
    }
}