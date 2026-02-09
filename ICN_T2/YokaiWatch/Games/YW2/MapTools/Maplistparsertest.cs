using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICN_T2.YokaiWatch.Games.YW2.Logic;
using ICN_T2.YokaiWatch.Definitions;

using ICN_T2.YokaiWatch.Games.YW2.MapTools;

using EncountTable = ICN_T2.YokaiWatch.Games.YW2.Logic.EncountTable;

namespace ICN_T2.Tests
{
    /// <summary>
    /// MapListParser 테스트 및 사용 예시
    /// </summary>
    public class MapListParserTest
    {
        public static void Run(string[] args)
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

        // ... (rest of tests) ...

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
            uint expectedHash = ICN_T2.Tools.Crc32.Compute(bytes);

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
            return ICN_T2.Tools.Crc32.Compute(bytes);
        }
    }

    /// <summary>
    /// 실전 사용 예시: EncounterWindow 통합
    /// </summary>
    public class RealWorldExample
    {
        private static MapListParser _cachedMapParser;
        private EncountTable selectedTable;

        public void Initialize()
        {
            // 1. MapParser 초기화
            if (_cachedMapParser == null)
            {
                _cachedMapParser = new MapListParser();
                _cachedMapParser.ParseAndLoad(MapListParser.GetDefaultMapList());
            }

            // ... (rest is same)
            // 방법 A: 내장 샘플
            _cachedMapParser.ParseAndLoad(MapListParser.GetDefaultMapList());
        }

        public void OnTableSelected(EncountTable table)
        {
            selectedTable = table;

            int hash = table.EncountConfigHash;
            string mapName = _cachedMapParser.GetMapName(hash);

            // UI 업데이트
            UpdateUI(hash, mapName);
        }

        private void UpdateUI(int hash, string mapName)
        {
            // ...
        }

        /// <summary>
        /// 433개 전체 맵 리스트 (실제로는 외부 파일 또는 리소스에서 로드 권장)
        /// </summary>
        private string GetFullMapList433()
        {
            return "";
        }
    }

    /// <summary>
    /// 고급 기능: 맵 검색 및 필터링
    /// </summary>
    public class AdvancedFeatures
    {
        private MapListParser mapParser;
        private List<EncountTable> encountTables;

        // [추가됨] 생성자를 통해 데이터를 주입받도록 수정
        public AdvancedFeatures(MapListParser parser, List<EncountTable> tables)
        {
            this.mapParser = parser;
            this.encountTables = tables;
        }

        // ... (rest is same) ...
        // Need to ensure SearchByMapName uses mapParser correctly

        public void SearchByMapName(string query)
        {
            // ...
            var results = new System.Collections.Generic.List<(int index, int hash, string mapName)>();
            for (int i = 0; i < encountTables.Count; i++)
            {
                int hash = encountTables[i].EncountConfigHash;
                string mapName = mapParser.GetMapName(hash);
                // ...
            }
            // ...
        }
    }
}