using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using ICN_T2.Tools; // Crc32
using ICN_T2.Logic.VirtualFileSystem; // VirtualDirectory

namespace ICN_T2.Logic.Level5.Archives.ARC0;

/// <summary>
/// [최적화 완료] ARC0 무결성 테스터 (Round-trip Test)
/// - 저장 후 재로드하여 데이터 손실 여부 검증
/// - CRC32 기반 초고속 비교
/// </summary>
public static class ARC0IntegrityTester
{
    public static bool Test(ARC0 original, string tempSavePath, out string report)
    {
        StringBuilder log = new(); // Target-typed new
        bool success = true;

        log.AppendLine("========================================");
        log.AppendLine("[ARC0 Integrity Test] 시작");
        log.AppendLine("========================================");

        try
        {
            // 1️⃣ 원본 구조 수집
            log.AppendLine("[1] 원본 파일 분석 중...");
            var originalFiles = GetAllFiles(original.Directory);
            log.AppendLine($" - 원본 파일 수: {originalFiles.Count:N0}개");

            // 2️⃣ 임시 저장
            log.AppendLine("[2] 저장 테스트 중...");
            original.Save(tempSavePath);
            FileInfo fi = new(tempSavePath);
            log.AppendLine($" - 저장 완료: {tempSavePath} ({fi.Length:N0} bytes)");

            // 3️⃣ 재로드 (Round-trip)
            log.AppendLine("[3] 재로드 및 검증 중...");
            using (FileStream fs = File.OpenRead(tempSavePath))
            {
                ARC0 reloaded = new(fs); // ARC0 클래스가 있다고 가정
                var reloadedFiles = GetAllFiles(reloaded.Directory);
                log.AppendLine($" - 재로드 파일 수: {reloadedFiles.Count:N0}개");

                // 4️⃣ 검증 시작

                // A. 개수 비교
                if (originalFiles.Count != reloadedFiles.Count)
                {
                    log.AppendLine($"❌ [FAIL] 파일 개수 불일치! (원본: {originalFiles.Count} vs 재로드: {reloadedFiles.Count})");
                    success = false;
                }

                // B. 내용 비교 (CRC32)
                int checkCount = 0;
                foreach (var kv in originalFiles)
                {
                    string path = kv.Key;
                    var originalStream = kv.Value;

                    if (!reloadedFiles.TryGetValue(path, out var reloadedStream))
                    {
                        log.AppendLine($"❌ [FAIL] 파일 누락: {path}");
                        success = false;
                        continue;
                    }

                    // 크기 비교
                    if (originalStream.Length != reloadedStream.Length)
                    {
                        log.AppendLine($"❌ [FAIL] 크기 불일치: {path} (원본: {originalStream.Length} vs 재로드: {reloadedStream.Length})");
                        success = false;
                        continue;
                    }

                    // CRC32 해시 비교 (데이터 내용 정밀 검사)
                    // SubMemoryStream 최적화: 메모리에 로드되지 않았다면 스트림에서 읽음
                    uint crcOrig = ComputeCrc(originalStream);
                    uint crcReload = ComputeCrc(reloadedStream);

                    if (crcOrig != crcReload)
                    {
                        log.AppendLine($"❌ [FAIL] 데이터 변조됨: {path}");
                        log.AppendLine($"   Original CRC: {crcOrig:X8}");
                        log.AppendLine($"   Reloaded CRC: {crcReload:X8}");
                        success = false;
                    }

                    checkCount++;
                }
                log.AppendLine($" - {checkCount}개 파일 정밀 검사 완료");
            }

            // 테스트 후 임시 파일 삭제 (성공 시에만, 혹은 옵션)
            // if (success && File.Exists(tempSavePath)) File.Delete(tempSavePath);
        }
        catch (Exception ex)
        {
            log.AppendLine($"💥 [CRITICAL ERROR] 테스트 중 예외 발생: {ex.Message}");
            log.AppendLine(ex.StackTrace);
            success = false;
        }

        log.AppendLine("========================================");
        log.AppendLine(success ? "🎉 무결성 테스트 통과 (Perfect)" : "🔥 무결성 테스트 실패 (Corrupted)");
        log.AppendLine("========================================");

        report = log.ToString();
        return success;
    }

    // --- Helpers ---

    private static uint ComputeCrc(SubMemoryStream stream)
    {
        // 1. 이미 메모리에 있다면 즉시 계산 (가장 빠름)
        if (stream.ByteContent != null)
        {
            return Crc32.Compute(stream.ByteContent);
        }

        // 2. 스트림 상태라면 읽어서 계산
        byte[] buffer = new byte[stream.Length];
        stream.Position = 0;
        stream.Read(buffer, 0, buffer.Length); // SubMemoryStream.Read가 최적화되어 있음
        return Crc32.Compute(buffer);
    }

    private static Dictionary<string, SubMemoryStream> GetAllFiles(VirtualDirectory root)
    {
        // StringComparer.OrdinalIgnoreCase로 대소문자 문제 방지
        var result = new Dictionary<string, SubMemoryStream>(StringComparer.OrdinalIgnoreCase);
        CollectFilesRecursive(root, "", result);
        return result;
    }

    private static void CollectFilesRecursive(VirtualDirectory dir, string currentPath, Dictionary<string, SubMemoryStream> result)
    {
        // 파일 수집
        foreach (var kv in dir.Files)
        {
            string fullPath = string.IsNullOrEmpty(currentPath) ? kv.Key : currentPath + "/" + kv.Key;
            result[fullPath] = kv.Value;
        }

        // 폴더 재귀
        foreach (var subDir in dir.Folders.Values) // Dictionary<string, VirtualDirectory>로 변경되었으므로 Values 사용
        {
            string nextPath = string.IsNullOrEmpty(currentPath) ? subDir.Name : currentPath + "/" + subDir.Name;
            CollectFilesRecursive(subDir, nextPath, result);
        }
    }
}