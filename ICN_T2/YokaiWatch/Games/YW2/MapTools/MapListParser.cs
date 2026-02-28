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
[Base Areas]
t100g00	알 수 없음 (YW1 텍스처 전용)
t101g00	진달래 윗마을
t102g00	울창산
t103g00	언덕마을
t104g00	진달래 다운타운
t105g00	심부름 골목
t106g00	산들바람촌
t107g00	진달래 타워
t120g00	알 수 없음 (텍스처 전용)
t121g00	갈매기 포구
t130g00	알 수 없음 (텍스처 전용)
t131g00	산들리 (산들마을)
t132g00	산들역 앞
t200g00	알 수 없음 (텍스처 전용)
t201g00	진달래읍 (과거의 진달래 마을)
t202g00	과거의 울창산
t206g00	과거의 호리병 연못
t230g00	알 수 없음 (텍스처 전용)
t231g00	과거의 산들리
t232g00	과거의 산들역 앞
t301g00	사와 산성 아랫마을
t302g00	사와 산성 내
t303g00	평원진지

[Interiors]
t101i01	민호네 집 1F
t101i02	민호네 집 2F
t101i03	세라네 집 1F
t101i04	세라네 집 2F
t101i21	진달래 빵집
t101i23	에브리마트 진달래 윗마을점
t101i25	진달래 시민회관
t101i27	꿀꿀 은행
t101i29	진달래양 우체국
t101i31	정글 헌터
t101i35	추억 가게
t101i51	진달래 초등학교 1F 남쪽
t101i52	진달래 초등학교 1F 북쪽
t101i53	진달래 초등학교 2F
t101i55	진달래 초등학교 3F
t101i58	진달래 초등학교 옥상
t101i59	진달래 초등학교 체육관
t102i01	빈집
t102i02	빈집 (비어있음)
t102i21	버려진 동굴
t103i01	도균이네 집 1F
t103i02	도균이네 집 2F
t103i03	도균이네 집 3F
t103i21	조기애 시계점
t103i23	달달 막과자
t103i25	에브리마트 언덕마을점
t103i31	정천사
t103i33	기도의 봉우리 터널
t103i35	생기발랄 한방약국
t103i37	진달래 온천 로비
t103i38	진달래 온천 남탕
t103i39	진달래 온천 여탕
t103i51	새장 하우스
t103i53	새장 하우스 숨겨진 방
t103i60~68	방랑장 101호~204호
t103i69	방랑장 VIP룸
t104i21	바닷바람 터널
t104i23	스낵 유키온나
t104i25	아카디아 아케이드
t104i27	냠냠버거
t104i29	돌보미 병원 1F
t104i30	돌보미 병원 2F
t104i31	입시학원
t104i33	에브리마트 진달래 다운타운점
t104i35	카페 샨티
t104i37	진달래 스포츠클럽 1F
t104i38	진달래 스포츠클럽 2F
t104i39	진달래 스포츠클럽 3F
t104i41	배불러 카레
t104i51	진달래 비즈니스 타워 1F
t104i53	진달래 비즈니스 타워 7F
t104i61	진달래 중앙역
t105i20	심부름 골목 상점가
t105i21	서점
t105i23	북풍 라멘
t105i25	에브리마트 심부름 골목점
t105i27	골동품점
t105i29	장난감 가게
t105i31	코인 세탁소
t105i33	옷가게
t105i35	재활용 숍
t105i37	초밥집
t106i01	곰이네 집 1F
t106i02	곰이네 집 2F
t106i05	세미네 집
t106i07	에이미네 집 1F
t106i09	에이미네 집 2F
t106i21	에브리마트 산들바람점
t106i23	트로피 룸
t106i51	등나무 저택 주차장
t106i52	등나무 저택 입구
t107i01	진달래 타워
t107i02	진달래 타워 엘리베이터
t107i03	전망대
t107i80	테스트 역
t120i01	알 수 없음 (열차 내부 텍스처)
t121i01	갈매기 포구 마을회관
t121i03	구멍가게 (갈매기 포구)
t121i10	빈집
t121i20	바다 (배)
t131i01	할머니 댁
t131i03	산들 분교
t131i05	산골 마켓
t201i01	과거의 정천사
t201i03	과거의 조기애 시계점
t201i05	알 수 없음 (미니맵 임시데이터)
t201i10	과거의 상점가
t201i20	과거의 기도의 봉우리 터널
t231i01	닌자의 숲
t231i03	비밀기지
t231i05	과거의 할머니 댁
t302i01	연회장

[Dungeons]
t001d01	신선 온천 입구
t001d02	게라게라 랜드 입구
t001d03	먹음직 파크 입구
t001d05	신선 온천
t001d06	게라게라 랜드
t001d07	먹음직 파크
t001d08	요괴가부키 극장
t001d09	요괴가부키 극장 지하
t001d41	요마계 입구
t001d42	요마계
t001d43	거짓말쟁이 산
t001d44	불량배의 길
t001d45	배고픔의 고개
t100d00	사쿠라 마을 지하 수도
t101d01	그늘진 뒷골목
t101d02	으슥한 수로
t101d02c	으슥한 수로 (복사본)
t101d03	고양이 뒷골목
t101d03c	고양이 뒷골목 (복사본)
t101d05	황량한 골목
t101d05c	황량한 골목 (복사본)
t102d01	울창산 등산로
t102d01c	울창산 등산로 (복사본)
t102d02	울창산 정상
t102d03	점보 미끄럼틀
t102d03c	점보 미끄럼틀 (복사본)
t102d31	폐터널 서쪽
t102d32	폐터널 동쪽
t103d01	트럭 뒷골목
t103d03	숨겨진 샛길
t103d03c	숨겨진 샛길 (복사본)
t103d11	비밀의 샛길
t103d11c	비밀의 샛길 (복사본)
t103d31	폐가 본채
t103d33	폐가 별채
t103d35	폐가 본채 다락방
t103d36	폐가 별채 다락방
t103d41~48	무겐 지옥 (무한지옥) 1~8계층
t103d49	무겐 지옥 반성실
t103d50~55	아미다 극락 1~6계층
t103d56	무겐 지옥 반성실 입구
t104d01	학원 지름길
t104d03	스낵바 뒷골목
t104d03c	스낵바 뒷골목 (복사본)
t104d05	배달 구역
t104d11	진달래 비즈니스 타워 4F
t104d13	진달래 비즈니스 타워 13F
t104d33	공사장 3F
t105d01	상가 좁은 골목
t105d01c	상가 좁은 골목 (복사본)
t105d11~26	어은장 (안논 단지)
t105d41	폐병원 1F
t105d43	폐병원 2F
t105d45	폐병원 3F
t105d47	폐병원 지하
t105d48	폐병원 지하 연구실
t106d11	험난한 길 (요괴워치 1)
t106d11c	수수께끼의 길 - 서비스 도로
t106d31	호리병 연못 박물관 1F
t106d32	호리병 연못 박물관 2F
t106d33	호리병 연못 박물관 금고
t106d41	게이트 룸
t106d43	크리에이터 존
t106d45~49	깡통차기 존 1F~출구
t106d51~54	신호등 존 1F~출구
t106d56~59	오답 존 1F~출구
t106d61~64	퀴즈 룸 1F~출구
t121d01	소금물 동굴
t121d03	숨겨진 작업장
t121d03c	숨겨진 작업장 (복사본)
t121d05	미사용 강 맵
t121d11	해변 동굴
t121d21	수수께끼의 길 - 루트 1
t121d23	수수께끼의 길 - 루트 4
t121d25	수수께끼의 길 - 경사로
t121d27	수수께끼의 길 - 고고 정션
t121d27a	수수께끼의 길 - 고고 정션
t121d27b	수수께끼의 길 - 고고 정션
t121d29	수수께끼의 길 - 종점
t121d31	금빛 고속도로 - 입구
t121d33	금빛 고속도로 - 교차로
t121d35	금빛 고속도로 - 종점
t121d41	C-1 그랑프리 Y
t131d01	논두렁 길
t131d01c	논두렁 길 (복사본)
t131d02	노코 마을
t131d03	큰바위 얼굴
t131d03c	큰바위 얼굴 (복사본)
t131d04	매미 계곡
t131d05	미들턴 산 정상
t131d20	영원 터널 (엥엔 터널)
t131d32~35	영원 터널 최종 구역
t131d36	불길한 방
t132d01	역 앞 골목길
t132d01c	역 앞 골목길 (복사본)
t200d00	가마솥 평원
t201d01	여우신사 길
t201d01c	여우신사 길 (복사본)
t201d03	우물 길
t201d11	과거 진달래 철공소
t201d21	석양 공장 정문
t201d23	석양 공장 제1용광로
t231d01	과거의 논두렁 길
t231d03	과거의 큰바위 얼굴
t231d04	과거의 매미 계곡
t231d05	과거의 미들턴 산 정상
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