using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
// ✅ [필수] Albatross 대신 ICN_T2 네임스페이스 사용
using ICN_T2.Logic.Level5.Binary;
using ICN_T2.Logic.Level5.Archives.ARC0;
using ICN_T2.Logic.VirtualFileSystem;

namespace ICN_T2.Tools.Debugging
{
    /// <summary>
    /// [최적화 완료] CfgBin 엔진 성능 및 무결성 테스트 생성기
    /// </summary>
    public class CfgBinTestGenerator
    {
        // 외부에서 호출할 수 있도록 Run 메서드로 정의
        public static void Run(string arcPath, string outputDir)
        {
            if (!File.Exists(arcPath))
            {
                Console.WriteLine($"[Error] 입력 파일을 찾을 수 없습니다: {arcPath}");
                return;
            }

            Directory.CreateDirectory(outputDir);

            Console.WriteLine("=== CfgBin Test File Generator (ICN_T2 Engine) ===");
            Console.WriteLine($"Input: {arcPath}");
            Console.WriteLine($"Output: {outputDir}");
            Console.WriteLine();

            try
            {
                // [1/4] ARC0 로드
                Console.WriteLine("[1/4] ARC0 아카이브 로딩 중...");
                using (FileStream fs = File.OpenRead(arcPath))
                {
                    ARC0 arc = new ARC0(fs);

                    // [2/4] chara_param 파일 찾기
                    Console.WriteLine("[2/4] chara_param 데이터 추출 중...");

                    var targetFile = FindCharaParam(arc.Directory);

                    if (targetFile == null)
                    {
                        Console.WriteLine("[Error] chara_param 파일을 찾을 수 없습니다.");
                        return;
                    }

                    Console.WriteLine($"  Found: {targetFile.Name}");

                    // 스트림에서 바이트 배열 추출
                    byte[] originalBytes = ReadStreamFully(targetFile.Stream);

                    // File A: 원본 (Original)
                    string fileA = Path.Combine(outputDir, "A_original.cfg.bin");
                    File.WriteAllBytes(fileA, originalBytes);
                    Console.WriteLine($"  [A] Original 저장 완료 ({originalBytes.Length:N0} bytes)");

                    // [3/4] Round-trip 테스트 (Open -> Save)
                    Console.WriteLine("[3/4] Round-trip (로드 후 즉시 저장) 테스트...");
                    CfgBin cfgRoundtrip = new CfgBin();
                    cfgRoundtrip.Open(originalBytes);

                    byte[] roundtripBytes = cfgRoundtrip.Save();
                    string fileB = Path.Combine(outputDir, "B_roundtrip.cfg.bin");
                    File.WriteAllBytes(fileB, roundtripBytes);
                    Console.WriteLine($"  [B] Round-trip 저장 완료 ({roundtripBytes.Length:N0} bytes)");

                    // [4/4] 데이터 수정 테스트 (Entry 직접 조작)
                    Console.WriteLine("[4/4] 데이터 수정 테스트...");
                    CfgBin cfgModified = new CfgBin();
                    cfgModified.Open(originalBytes);

                    ModifyCharaParamDirectly(cfgModified);

                    byte[] modifiedBytes = cfgModified.Save();
                    string fileC = Path.Combine(outputDir, "C_modified.cfg.bin");
                    File.WriteAllBytes(fileC, modifiedBytes);
                    Console.WriteLine($"  [C] Modified 저장 완료 ({modifiedBytes.Length:N0} bytes)");

                    // 결과 요약
                    Console.WriteLine();
                    Console.WriteLine("=== 생성 완료 및 비교 ===");
                    Console.WriteLine($"A (원본) vs B (재저장): {CompareSize(originalBytes.Length, roundtripBytes.Length)}");
                    Console.WriteLine($"B (재저장) vs C (수정됨): {CompareSize(roundtripBytes.Length, modifiedBytes.Length)}");

                    if (originalBytes.Length == roundtripBytes.Length)
                    {
                        Console.WriteLine("✨ Perfect! 원본과 재저장 파일의 크기가 동일합니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRITICAL ERROR] {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        // --- Helper Logic ---

        private static VirtualFileEntry FindCharaParam(VirtualDirectory root)
        {
            try
            {
                // VirtualDirectory 구조에 맞춰 안전하게 탐색
                if (root.Folders.TryGetValue("data", out var data) &&
                    data.Folders.TryGetValue("res", out var res) &&
                    res.Folders.TryGetValue("character", out var character))
                {
                    var found = character.Files
                        .Where(f => f.Key.StartsWith("chara_param", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => f.Key)
                        .FirstOrDefault();

                    if (found.Value != null)
                    {
                        return new VirtualFileEntry(found.Key, found.Value);
                    }
                }
            }
            catch
            {
                // 무시
            }
            return null;
        }

        private static void ModifyCharaParamDirectly(CfgBin cfg)
        {
            // CHARA_PARAM_INFO_BEGIN 또는 유사한 이름 찾기
            var paramList = cfg.Entries.FirstOrDefault(e => e.GetName().Contains("CHARA_PARAM_INFO"));

            if (paramList != null && paramList.Children.Count > 0)
            {
                // 첫 번째 요괴 데이터
                Entry firstYokai = paramList.Children[0];

                Console.WriteLine($"  Target Yokai Entry: {firstYokai.GetName()}");

                int intCount = 0;
                foreach (var v in firstYokai.Variables)
                {
                    // EntryType.Int 인지 확인 (Type.Int가 아님에 주의)
                    if (v.Type == EntryType.Int)
                    {
                        // 값 수정 (예: 0xD3AEAB7D)
                        int oldValue = Convert.ToInt32(v.Value);
                        v.Value = 0xD3AEAB7D;

                        Console.WriteLine($"  Modified Variable[{intCount}]: {oldValue} -> {v.Value:X8}");
                        cfg.IsModified = true;
                        break;
                    }
                    intCount++;
                }
            }
            else
            {
                Console.WriteLine("  [Warning] CHARA_PARAM_INFO 리스트를 찾을 수 없습니다.");
            }
        }

        private static byte[] ReadStreamFully(Stream input)
        {
            if (input is MemoryStream ms) return ms.ToArray();

            byte[] buffer = new byte[input.Length];
            long pos = input.Position;
            input.Position = 0;
            input.Read(buffer, 0, buffer.Length);
            input.Position = pos;
            return buffer;
        }

        private static string CompareSize(int size1, int size2)
        {
            int diff = size2 - size1;
            if (diff == 0) return "Same size (Perfect Match)";
            return diff > 0 ? $"+{diff:N0} bytes" : $"{diff:N0} bytes";
        }

        private class VirtualFileEntry
        {
            public string Name;
            public Stream Stream;
            public VirtualFileEntry(string name, Stream stream) { Name = name; Stream = stream; }
        }
    }
}