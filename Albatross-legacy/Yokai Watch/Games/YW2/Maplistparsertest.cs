using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Albatross.Yokai_Watch.Games.YW2;
using Albatross.Yokai_Watch.Logic;

namespace Albatross.Tests
{
    /// <summary>
    /// MapListParser 테스트 및 사용 예시
    /// </summary>
    public class MapListParserTest
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== MapListParser Test ===\n");

            // 테스트 1: 기본 파싱
            Test1_BasicParsing();

            // 테스트 2: CRC32 해시 검증
            Test2_HashVerification();

            // 테스트 3: 실제 common_enc 시뮬레이션
            Test3_CommonEncSimulation();

            Console.WriteLine("\n=== All Tests Complete ===");
            Console.ReadKey();
        }

        /// <summary>
        /// 테스트 1: 기본 파싱 기능
        /// </summary>
        static void Test1_BasicParsing()
        {
            Console.WriteLine("--- Test 1: Basic Parsing ---");

            MapListParser parser = new MapListParser();
            parser.ParseAndLoad(MapListParser.GetDefaultMapList());

            Console.WriteLine($"Loaded entries: {parser.MapHashMap.Count}");
            Console.WriteLine($"File name entries: {parser.FileNameMap.Count}");

            // 샘플 조회
            if (parser.FileNameMap.ContainsKey("t101g00"))
            {
                Console.WriteLine("✓ t101g00 found: " + parser.FileNameMap["t101g00"]);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// 테스트 2: CRC32 해시 검증
        /// </summary>
        static void Test2_HashVerification()
        {
            Console.WriteLine("--- Test 2: CRC32 Hash Verification ---");

            MapListParser parser = new MapListParser();
            parser.ParseAndLoad(MapListParser.GetDefaultMapList());

            // 수동 해시 계산 예시
            string testId = "t101g00";
            Encoding shiftJis = Encoding.GetEncoding("Shift-JIS");
            byte[] bytes = shiftJis.GetBytes(testId);
            uint expectedHash = Albatross.Tools.Crc32.Compute(bytes);

            Console.WriteLine($"File ID: {testId}");
            Console.WriteLine($"Calculated Hash: 0x{expectedHash:X8}");

            string mapName = parser.GetMapName(expectedHash);
            if (mapName != null)
            {
                Console.WriteLine($"✓ Map Name: {mapName}");
            }
            else
            {
                Console.WriteLine("✗ Map name not found!");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// 테스트 3: common_enc 시뮬레이션
        /// </summary>
        static void Test3_CommonEncSimulation()
        {
            Console.WriteLine("--- Test 3: common_enc Simulation ---");

            MapListParser parser = new MapListParser();
            parser.ParseAndLoad(MapListParser.GetDefaultMapList());

            // 가상의 EncountTable 시뮬레이션
            var simulatedTables = new[]
            {
                new { TableIndex = 1, Hash = CalculateHash("t101g00") },
                new { TableIndex = 2, Hash = CalculateHash("t102g00") },
                new { TableIndex = 3, Hash = CalculateHash("t103g00") },
                new { TableIndex = 4, Hash = 0x12345678u },  // Unknown hash
            };

            Console.WriteLine("Simulated EncountTables:");
            foreach (var table in simulatedTables)
            {
                string mapName = parser.GetMapName(table.Hash);
                string displayName = mapName ?? "Unknown Map";

                Console.WriteLine($"  Table {table.TableIndex}: 0x{table.Hash:X8} → {displayName}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// 헬퍼: CRC32 해시 계산
        /// </summary>
        static uint CalculateHash(string text)
        {
            Encoding shiftJis = Encoding.GetEncoding("Shift-JIS");
            byte[] bytes = shiftJis.GetBytes(text);
            return Albatross.Tools.Crc32.Compute(bytes);
        }
    }

    /// <summary>
    /// 실전 사용 예시: EncounterWindow 통합
    /// </summary>
    public class RealWorldExample
    {
        private static MapListParser _cachedMapParser;
        private IEncountTable selectedTable;

        public void Initialize()
        {
            // 1. MapParser 초기화
            if (_cachedMapParser == null)
            {
                _cachedMapParser = new MapListParser();
                _cachedMapParser.ParseAndLoad(MapListParser.GetDefaultMapList());
            }


            // 2. 데이터 로드 (3가지 방법)

            // 방법 A: 내장 샘플
            _cachedMapParser.ParseAndLoad(MapListParser.GetDefaultMapList());

            // 방법 B: 외부 파일
            // if (System.IO.File.Exists("data/map_list.txt"))
            //     mapParser.LoadFromFile("data/map_list.txt");

            // 방법 C: 큰 리스트 (433개) - 문자열로 직접 제공
            // mapParser.ParseAndLoad(GetFullMapList433());
        }

        public void OnTableSelected(IEncountTable table)
        {
            selectedTable = table;

            int hash = table.EncountConfigHash;
            string mapName = _cachedMapParser.GetMapName(hash);

            // UI 업데이트
            UpdateUI(hash, mapName);
        }

        private void UpdateUI(int hash, string mapName)
        {
            if (!string.IsNullOrEmpty(mapName))
            {
                Console.WriteLine($"[UI] Hash: 0x{hash:X8} - {mapName}");
                // hashTextBox.Text = $"0x{hash:X8} - {mapName}";
                // locationLabel.Text = $"위치: {mapName}";
            }
            else
            {
                Console.WriteLine($"[UI] Hash: 0x{hash:X8} (Unknown)");
                // hashTextBox.Text = $"0x{hash:X8} (Unknown)";
                // locationLabel.Text = "위치: 알 수 없음";
            }
        }

        /// <summary>
        /// 433개 전체 맵 리스트 (실제로는 외부 파일 또는 리소스에서 로드 권장)
        /// </summary>
        private string GetFullMapList433()
        {
            // TODO: 실제 433개 리스트를 여기에 포함하거나 외부 파일로 관리
            return @"[Big Maps]
t100g00 Unknown
t101g00 Uptown Springdale
t102g00 Mt. Wildwood
... (전체 433개)
";
        }
    }

    /// <summary>
    /// 고급 기능: 맵 검색 및 필터링
    /// </summary>
    public class AdvancedFeatures
    {
        private MapListParser mapParser;
        private List<IEncountTable> encountTables;

        // [추가됨] 생성자를 통해 데이터를 주입받도록 수정
        public AdvancedFeatures(MapListParser parser, List<IEncountTable> tables)
        {
            this.mapParser = parser;
            this.encountTables = tables;
        }

        public void SearchByMapName(string query)
        {
            Console.WriteLine($"\n[Search] Query: '{query}'");

            var results = new List<(int index, int hash, string mapName)>();

            for (int i = 0; i < encountTables.Count; i++)
            {
                int hash = encountTables[i].EncountConfigHash;
                string mapName = mapParser.GetMapName(hash);

                if (!string.IsNullOrEmpty(mapName) &&
                    mapName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    results.Add((i, hash, mapName));
                }
            }

            Console.WriteLine($"[Search] Found {results.Count} matches:");
            foreach (var result in results)
            {
                Console.WriteLine($"  Table {result.index + 1}: 0x{result.hash:X8} → {result.mapName}");
            }
        }

        public void GroupByMapType()
        {
            Console.WriteLine("\n[Group] Grouping tables by map type:");

            var groups = new Dictionary<string, List<(int hash, string name)>>();

            foreach (var table in encountTables)
            {
                int hash = table.EncountConfigHash;
                string mapName = mapParser.GetMapName(hash);

                if (string.IsNullOrEmpty(mapName))
                    continue;

                // 카테고리 추출 (예: "Paradise Springs Level 1" → "Paradise Springs")
                string category = ExtractCategory(mapName);

                if (!groups.ContainsKey(category))
                    groups[category] = new List<(int, string)>();

                groups[category].Add((hash, mapName));
            }

            foreach (var group in groups.ToList().OrderBy(g => g.Key))
            {
                Console.WriteLine($"\n[{group.Key}] ({group.Value.Count} tables)");
                foreach (var item in group.Value)
                {
                    Console.WriteLine($"  - 0x{item.hash:X8}: {item.name}");
                }
            }
        }

        private string ExtractCategory(string mapName)
        {
            // 간단한 카테고리 추출: "Level", "Entrance" 등 제거
            string[] removeWords = { "Level", "Entrance", "1F", "2F", "3F" };
            string category = mapName;

            foreach (string word in removeWords)
            {
                int index = category.IndexOf(word);
                if (index > 0)
                {
                    category = category.Substring(0, index).Trim();
                    break;
                }
            }

            return category;
        }

        public void ExportToCSV(string outputPath)
        {
            Console.WriteLine($"\n[Export] Exporting to CSV: {outputPath}");

            using (var writer = new System.IO.StreamWriter(outputPath, false, Encoding.UTF8))
            {
                // 헤더
                writer.WriteLine("TableIndex,Hash,MapName");

                // 데이터
                for (int i = 0; i < encountTables.Count; i++)
                {
                    int hash = encountTables[i].EncountConfigHash;
                    string mapName = mapParser.GetMapName(hash) ?? "Unknown";

                    // CSV 이스케이프 (쉼표 포함 시 따옴표 처리)
                    if (mapName.Contains(","))
                        mapName = $"\"{mapName}\"";

                    writer.WriteLine($"{i + 1},0x{hash:X8},{mapName}");
                }
            }

            Console.WriteLine($"[Export] Complete! {encountTables.Count} entries exported.");
        }
    }
}