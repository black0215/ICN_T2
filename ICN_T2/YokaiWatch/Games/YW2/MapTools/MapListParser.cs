using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICN_T2.Tools;

namespace ICN_T2.YokaiWatch.Games.YW2.MapTools
{
    /// <summary>
    /// 맵 파일명 리스트를 파싱하여 CRC32 해시로 변환하고 맵 이름과 매핑합니다.
    /// common_enc의 433개 테이블 해시를 실제 맵 이름으로 변환하는데 사용됩니다.
    /// </summary>
    public class MapListParser
    {
        /// <summary>
        /// CRC32 해시 → 맵 이름 매핑 딕셔너리
        /// </summary>
        public Dictionary<uint, string> MapHashMap { get; private set; }

        /// <summary>
        /// 파일명 → 맵 이름 매핑 딕셔너리 (디버깅용)
        /// </summary>
        public Dictionary<string, string> FileNameMap { get; private set; }

        public MapListParser()
        {
            MapHashMap = new Dictionary<uint, string>();
            FileNameMap = new Dictionary<string, string>();
        }

        /// <summary>
        /// Raw 텍스트 데이터를 파싱하여 해시 맵 생성
        /// </summary>
        /// <param name="rawText">맵 리스트 텍스트 (파일명 + 맵 이름)</param>
        public void ParseAndLoad(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                Console.WriteLine("[MapListParser] Warning: Empty input text");
                return;
            }

            string[] lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int parsedCount = 0;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                // 섹션 헤더 스킵 ([Big Maps], [Dungeons] 등)
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    continue;

                // 빈 줄 스킵
                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;

                // 파싱: "t101g00 Uptown Springdale" 형태
                if (TryParseMapLine(trimmedLine, out string fileId, out string mapName))
                {
                    // CRC32 해시 계산 (Shift-JIS)
                    uint hash = CalculateCrc32ShiftJIS(fileId);

                    // 딕셔너리에 추가 (중복 체크)
                    if (!MapHashMap.ContainsKey(hash))
                    {
                        MapHashMap.Add(hash, mapName);
                        FileNameMap.Add(fileId, mapName);
                        parsedCount++;
                    }
                    else
                    {
                        Console.WriteLine($"[MapListParser] Warning: Duplicate hash for {fileId} (0x{hash:X8})");
                    }
                }
            }

            Console.WriteLine($"[MapListParser] Loaded {parsedCount} map entries");
        }

        /// <summary>
        /// 파일에서 맵 리스트 로드
        /// </summary>
        /// <param name="filePath">맵 리스트 파일 경로</param>
        public void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[MapListParser] Error: File not found - {filePath}");
                return;
            }

            string rawText = File.ReadAllText(filePath, Encoding.UTF8);
            ParseAndLoad(rawText);
        }

        /// <summary>
        /// 한 줄을 파싱하여 파일명과 맵 이름 분리
        /// </summary>
        /// <param name="line">파싱할 텍스트 라인</param>
        /// <param name="fileId">출력: 파일명 (예: t101g00)</param>
        /// <param name="mapName">출력: 맵 이름 (예: Uptown Springdale)</param>
        /// <returns>파싱 성공 여부</returns>
        private bool TryParseMapLine(string line, out string? fileId, out string? mapName)
        {
            fileId = null;
            mapName = null;

            // 1. 't'로 시작하는지 확인 (최소 길이 7: t101g00)
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("t") || line.Length < 7)
                return false;

            // 2. 공백(스페이스나 탭)을 기준으로 나눔 (최대 2개로 분할)
            // 예: "t101g00 Uptown" -> ["t101g00", "Uptown"]
            // 예: "t001b01"        -> ["t001b01"]
            string[] parts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0) return false;

            // 3. 파일 ID 추출
            fileId = parts[0].Trim();

            // 4. 맵 이름 추출
            if (parts.Length > 1)
            {
                // 뒤에 설명이 있는 경우
                mapName = parts[1].Trim();
            }
            else
            {
                // 설명이 없는 경우 (예: t001b01) -> 파일명을 이름으로 사용
                mapName = fileId;
                // 혹은 mapName = $"{fileId} (Unknown)"; 처럼 표시하고 싶으면 이렇게 수정
            }

            return true;
        }

        /// <summary>
        /// Shift-JIS 인코딩으로 문자열의 CRC32 해시 계산
        /// </summary>
        /// <param name="text">해시할 텍스트</param>
        /// <returns>CRC32 해시값 (uint)</returns>
        private uint CalculateCrc32ShiftJIS(string text)
        {
            // Shift-JIS 인코딩 (코드 페이지 932)
            Encoding shiftJis = Encoding.GetEncoding("Shift-JIS");
            byte[] bytes = shiftJis.GetBytes(text);

            // CRC32 계산 (Albatross.Tools.Crc32 사용)
            uint hash = Crc32.Compute(bytes);

            return hash;
        }

        /// <summary>
        /// 해시값으로 맵 이름 조회
        /// </summary>
        /// <param name="hash">CRC32 해시값</param>
        /// <returns>맵 이름 (없으면 null)</returns>
        public string? GetMapName(uint hash)
        {
            if (MapHashMap.TryGetValue(hash, out string? mapName))
                return mapName;

            return null;
        }

        /// <summary>
        /// 해시값으로 맵 이름 조회 (int 오버로드)
        /// </summary>
        public string? GetMapName(int hashInt)
        {
            uint hash = (uint)hashInt;
            return GetMapName(hash);
        }

        /// <summary>
        /// 디버그용: 모든 매핑 출력
        /// </summary>
        public void PrintAllMappings()
        {
            Console.WriteLine($"\n=== Map Hash Mappings ({MapHashMap.Count} entries) ===");

            foreach (var kvp in FileNameMap.OrderBy(x => x.Key))
            {
                string fileId = kvp.Key;
                string mapName = kvp.Value;
                uint hash = CalculateCrc32ShiftJIS(fileId);

                Console.WriteLine($"{fileId} → 0x{hash:X8} → {mapName}");
            }
        }

        /// <summary>
        /// 내장 기본 맵 리스트 (샘플 - 실제 프로덕션에서는 외부 파일 사용 권장)
        /// </summary>
        public static string GetDefaultMapList()
        {
            return @"[Big Maps]
t100g00	Unknown (Only textures inside, carried over from YW1)
t101g00	Uptown Springdale
t102g00	Mt. Wildwood
t103g00	Blossom Heights
t104g00	Downtown Springdale
t105g00	Shopper's Row
t106g00	Breezy Hills
t107g00	Excellent Tower
t120g00	Unknown (Only textures inside)
t121g00	San Fantastico
t130g00	Unknown (Only textures inside)
t131g00	Harrisville
t132g00	Harrisville Station Plaza
t200g00	Unknown (Only textures inside)
t201g00	Old Springdale
t202g00	Old Mount Wildwood
t206g00	Old Gourd Pond
t230g00	Unknown (Only textures inside)
t231g00	Old Harrisville
t232g00	Old Harrisville Station Plaza
t301g00	Sawayama Castle Town
t302g00	Inside Sawayama Castle
t303g00	Sekigahara Stronghold
[Interiors]
t101i01	Nate's house F1
t101i02	Nate's house F2
t101i03	Katie's House F1
t101i04	Katie's House F2
t101i21	Banter Bakery
t101i23	Everymart Uptown
t101i25	Springdale Community Center
t101i27	Piggleston Bank
t101i29	Lambert Post Office
t101i31	Jungle Hunter
t101i35	Memory Store
t101i51	Springdale Elementary 1F South
t101i52	Springdale Elementary 1F North
t101i53	Springdale Elementary 2F
t101i55	Springdale Elementary 3F
t101i58	Springdale Elementary Roof
t101i59	Springdale Elementary Gym
t102i01	Deserted House
t102i02	Deserted House (Empty?)
t102i21	Mt. Wildwood Cave
t103i01	Bernstein House 1F
t103i02	Bernstein House 2F
t103i03	Bernstein House 3F
t103i21	Timers & More
t103i23	Candy Stop
t103i25	Everymart Blossom Heights
t103i31	Shoten Temple
t103i33	Prayer's Peak Tunnel
t103i35	Chloro-Phil Good
t103i37	Springdale Hot Springs Lobby
t103i38	Springdale Hot Springs Men
t103i39	Springdale Hot Springs Female
t103i51	Byrd House
t103i53	Byrd House Hidden Room
t103i60	Wayfarer Manor 101
t103i61	Wayfarer Manor 102
t103i62	Wayfarer Manor 103
t103i63	Wayfarer Manor 104
t103i64	Wayfarer Manor 105
t103i65	Wayfarer Manor 201
t103i66	Wayfarer Manor 202
t103i67	Wayfarer Manor 203
t103i68	Wayfarer Manor 204
t103i69	Wayfarer Manor VIP Room
t104i21	Seabreeze Tunnel
t104i23	Frostia's Place
t104i25	Arcadia Arcade
t104i27	Nom Burguer
t104i29	Fortune Hospital 1F
t104i30	Fortune Hospital 2F
t104i31	Foundation Academy
t104i33	Everymart Downtown Springdale
t104i35	Café Shanista
t104i37	Springdale Sports Club 1F
t104i38	Springdale Sports Club 2F
t104i39	Springdale Sports Club 3F
t104i41	Belly Buster Curry
t104i51	Springdale Business Tower 1F
t104i53	Springdale Business Tower 7F
t104i61	Springdale Central Station
t105i20	Springdale Flower Road
t105i21	Settle In Bookstore
t105i23	North Wind Ramen
t105i25	Everymart Shopper's Row
t105i27	Sun Pavilion
t105i29	Toys iZ We
t105i31	Mary's Coin Laundry
t105i33	Superior Style
t105i35	Whatta Find
t105i37	Sushi Springdale
t106i01	Archer House 1F
t106i02	Archer House 2F
t106i05	Stone House
t106i07	Amy's House 1F
t106i09	Amy's House 2F
t106i21	Everymart Breezy Hills
t106i23	Trophy Room
t106i51	Wisteria Gardens Parking
t106i52	Wisteria Gardens Entrance
t107i01	Excellent Tower
t107i02	Excellent Tower Elevator
t107i03	Observation Deck
t107i80	Test Station
t120i01	Unknown (Textures inside are from a train)
t121i01	Rolling Waves Meeting Hall
t121i03	Rusty's Mart
t121i10	Deserted House
t121i20	Sea (Boat)
t131i01	Grandma's House
t131i03	Harrisville School
t131i05	Mountain Market
t201i01	Old Shoten Temple
t201i03	Old Timers & More
t201i05	Unknown (Placeholder Minimap)
t201i10	Galleria Boulevard
t201i20	Old Prayer's Peak Tunnel
t231i01	Ninja Forest
t231i03	Secret Base
t231i05	Old Grandma's House
t302i01	Banquet Hall
[Dungeons]
t001d01	Paradise Springs Entrance
t001d02	Gera Gera Land Entrance
t001d03	Wolfit Down Entrance
t001d05	Paradise Springs
t001d06	Gera Gera Land
t001d07	Wolfit Down
t001d08	Kaibuki Theater
t001d09	Kaibuki Theater Basement
t001d41	Yo-kai world Entrance
t001d42	Yo-kai world
t001d43	Liar Mountain
t001d44	Hooligan Road
t001d45	Hungry Pass
t100d00	Underground Waterway
t101d01	Shady Back Alley
t101d02	Lonely Waterway
t101d02c	Lonely Waterway (Copy?)
t101d03	The Catwalk
t101d03c	The Catwalk (Copy?)
t101d05	Desolate Lane
t101d05c	Desolate Lane (Copy?)
t102d01	Mt. Wildwood Trail
t102d01c	Mt. Wildwood Trail (Copy?)
t102d02	Mt. Wildwood Summit
t102d03	Jumbo Slider
t102d03c	Jumbo Slider (Copy?)
t102d31	Abandoned Tunnel West
t102d32	Abandoned Tunnel East
t103d01	Trucked Away Lot
t103d03	Hidden Side Street
t103d03c	Hidden Side Street (Copy?)
t103d11	Secret Byway
t103d11c	Secret Byway (Copy?)
t103d31	Old Mansion Main House
t103d33	Old Mansion Side House
t103d35	Old Mansion Main House Attic
t103d36	Old Mansion Side House Attic
t103d41	Infinite Inferno 1st Circle
t103d42	Infinite Inferno 2nd Circle
t103d43	Infinite Inferno 3rd Circle
t103d44	Infinite Inferno 4th Circle
t103d45	Infinite Inferno 5th Circle
t103d46	Infinite Inferno 6th Circle
t103d47	Infinite Inferno 7th Circle
t103d48	Infinite Inferno 8th Circle
t103d49	Infinite Inferno Time-Out Room
t103d50	Divine paradise 1F
t103d51	Divine paradise 2F
t103d52	Divine paradise 3F
t103d53	Divine paradise 4F
t103d54	Divine paradise 5F
t103d55	Divine paradise 6F
t103d56	Infinite Inferno Time-Out Room Entrance
t104d01	Academy Shortcut
t104d03	Behind Frostia's Place
t104d03c	Behind Frostia's Place (Copy?)
t104d05	Delivery Bay
t104d11	Springdale Business Tower 4F
t104d13	Springdale Business Tower 13F
t104d33	Construction Site 3F
t105d01	Shopping Street Narrows
t105d01c	Shopping Street Narrows (Copy)
t105d11	Tranquility Apts.
t105d12	Tranquility Apts.
t105d13	Tranquility Apts.
t105d14	Tranquility Apts.
t105d15	Tranquility Apts.
t105d16	Tranquility Apts.
t105d17	Tranquility Apts.
t105d18	Tranquility Apts.
t105d19	Tranquility Apts.
t105d20	Tranquility Apts.
t105d21	Tranquility Apts.
t105d22	Tranquility Apts.
t105d23	Tranquility Apts.
t105d24	Tranquility Apts.
t105d25	Tranquility Apts.
t105d26	Tranquility Apts.
t105d41	Nocturne Hospital 1F
t105d43	Nocturne Hospital 2F
t105d45	Nocturne Hospital 3F
t105d47	Nocturne Hospital Basement
t105d48	Nocturne Hospital Basement Lab
t106d11	Rugged Path (Yo-kai Watch 1)
t106d11c	Mystery Way - Service Road
t106d31	Gourd Pond Museum 1F
t106d32	Gourd Pond Museum 2F
t106d33	Gourd Pond Museum Vault
t106d41	Gate Room
t106d43	Creator Zone
t106d45	Can-Kicking Zone 1F
t106d46	Can-Kicking Zone 2F
t106d47	Can-Kicking Zone 3F
t106d49	Can-Kicking Zone Exit
t106d51	Traffic-Light Zone 1F
t106d52	Traffic-Light Zone 2F
t106d53	Traffic-Light Zone 3F
t106d54	Traffic-Light Zone Exit
t106d56	Compunzer's Zone 1F
t106d57	Compunzer's Zone 2F
t106d58	Compunzer's Zone 3F
t106d59	Compunzer's Zone Exit
t106d61	Quiz Room 1F
t106d62	Quiz Room 2F
t106d63	Quiz Room 3F
t106d64	Quiz Room Exit
t121d01	Briny Grotto
t121d03	Hidden Workshop
t121d03c	Hidden Workshop (Copy?)
t121d05	Unused river map (Has a placeholder minimap, altmost empty)
t121d11	Seaside Cave
t121d21	Mystery Way - Route 1
t121d23	Mystery Way - Route 4
t121d25	Mystery Way - Ramp
t121d27	Mystery Way - Gogo Junction
t121d27a	Mystery Way - Gogo Junction
t121d27b	Mystery Way - Gogo Junction
t121d29	Mystery Way - End Point
t121d31	Gold-Gleaming Highway - Entrance
t121d33	Gold-Gleaming Highway - Interchange
t121d35	Gold-Gleaming Highway - End Point
t121d41	C-1 Grand Prix Y
t131d01	Rice Paddy Path
t131d01c	Rice Paddy Path (Copy?)
t131d02	Nokotopia
t131d03	Fullface Rock
t131d03c	Fullface Rock (Copy?)
t131d04	Cicada Canyon
t131d05	Mt. Middleton Summit
t131d20	Infinite Tunnel
t131d32	Infinite Tunnel Final Zone
t131d33	Infinite Tunnel Final Zone
t131d34	Infinite Tunnel Final Zone
t131d35	Infinite Tunnel Final Zone
t131d36	Neighfarious Room
t132d01	Alley off the Plaza
t132d01c	Alley off the Plaza (Copy?)
t200d00	Flatpot Plains
t201d01	Fox Shrine Road
t201d01c	Fox Shrine Road (Copy)
t201d03	Well Road
t201d11	Old Springdale Ironworks
t201d21	Sunset Manufacturing Co. Main Gate
t201d23	Sunset Manufacturing Co. Furnace 1
t231d01	Old Rice Paddy
t231d03	Old Fullface Rock
t231d04	Old Cicada Canyon
t231d05	Old Mt. Middleton Summit
[Battle Backgrounds]
t001b01	
t001b02	
t001b03	
t001b11	
t001b12	
t001b13	
t001b15	
t001b31	
t001b32	
t001b33	
t001b35	
t001b37	
t001b39	
t001b41	
t001b43	
t002b01	
t100b01	
t100b02	
t100b02a	
t100b02b	
t100b03	
t100b04	
t100b05	
t100b06	
t100b07	
t100b21	
t100b21a	
t100b21b	
t100b22	
t100b22c	
t100b23	
t100b23c	
t100b24	
t100b24c	
t100b27	
t100b29	
t100b41	
t100b51	
t100b52	
t100b53	
t100b55	
t100b57	
t100b59	
t100b61	
t100b62	
t100b63	
t100b64	
t100b65	
t100b66	
t100b67	
t100b68	
t100b69	
t100b70	
t100b71	
t100b72	
t100b73	
t100b74	
t100b75	
t100b76	
t100b77	
t100b78	
t100b79	
t100b80	
t100b81	
t100b82	
t101b01	
t101b02	
t101b03	
t101b21	
t101b23	
t101b25	
t101b27	
t101b28	
t102b01	
t102b02	
t102b03	
t102b11	
t102b21	
t102b23	
t103b01	
t103b03	
t103b05	
t103b07	
t103b09	
t103b21	
t103b23	
t103b31	
t103b33	
t103b35	
t103b37	
t103b39	
t104b01	
t104b03	
t104b21	
t104b23	
t104b25	
t105b01	
t105b03	
t105b21	
t105b23	
t105b25	
t106b01	
t106b21	
t106b23	
t106b31	
t106b32	
t106b33	
t106b35	
t106b37	
t106b39	
t106b41	
t106b43	
t107b01	
t107b03	
t121b01	
t121b03	
t121b03c	
t121b21	
t121b23	
t121b31	
t121b33	
t121b35	
t121b37	
t121b39	
t131b01	
t131b21	
t131b21c	
t131b23	
t131b33	
t132b01	
t132b03	
t132b03c	
t200b01	
t200b02	
t200b41	
t201b01	
t201b02	
t201b03	
t201b03c	
t201b21	
t201b23	
t201b31	
t201b33	
t201b35	
t202b01	
t231b01	
t231b21	
t231b23	
t232b01	Harrisville Station Plaza Past Battle bg
t300b01	Grass (Feudal era)
t301b01	Sawayama Castle Town Battle bg
Watch Maps
t100w011	
t100w021	
t100w031	
t100w041	
t100w051	
t100w061	
t100w071	
t100w081	
t100w091	
t100w101	
t100w121	
t100w131	
t100w141	
t100w151	
t100w161	
t100w171	
t100w181	
t100w191	
t100w201	
t100w211	
t102w011	
t107w011	
t121w011
t131w011	
t200w011	
t200w021	
t200w031	
t200w061	
t200w121	
t200w131	
t200w151	
t200w161	
t200w191	
t200w201	
t300w011	
t300w021	
t300w061	
t300w131	
t300w151	
[Train Stations]
t001s00	Hexpress
t001s01	Paradise Springs Station
t001s02	Gera Gera Land Station
t001s03	Wolfit Down Station
t002s00	Happy-Go-Lucky Express
t002s01	Bucklebreaker Station
t100s00	Regular trains
t100s01	Springdale Central Station
t100s02	Green Street Station
t100s03	Hibarly Hills Station
t100s04	Petal Peak Station
t100s05	Factory Row Station
t100s06	Sweet Meadow Station
t100s07	Fortune Place Station
t100s08	Skybridge Station
t100s09	Dreamers Field Station
t100s10	Ridgemont Station
t100s11	Bayside Station
t100s12	San Fantastico Station
t100s13	Greenfield Station
t100s14	Temple Park Station
t100s15	Dingle Falls Station
t100s16	Harrisville Station
t100s17	Spring Station
t100s18	Sunshine Station
t100s19	Little Haven Station
t100s20	Scarfit Down Station
t100s21	Cherry Hill Station
t100s22	Whimsy Valley Station
[Train Interiors]
t001t01	
t001t03	
t001t04	
t002t03	
t002t04	
t100t00	
t100t01	
t100t02	
t100t03	
t100t04	
t100t05	
t100t06	
t100t07	
";
        }
    }
}