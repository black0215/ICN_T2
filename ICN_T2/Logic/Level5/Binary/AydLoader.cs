using ICN_T2.Logic.Level5.Compression; // Level5Decompressor 위치
using ICN_T2.Logic.Level5.Encryption;  // YokaiDecrypter 위치
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ICN_T2.Logic.Level5.Binary;

/// <summary>
/// 가상 파일 정보 (압축/암호화 해제된 결과물)
/// </summary>
public class VirtualFile
{
    public string FileName { get; set; }
    public byte[] Data { get; set; }

    // 확장자 편의 속성 (소문자 변환)
    public string Extension => Path.GetExtension(FileName)?.ToLowerInvariant() ?? "";
}

/// <summary>
/// 리소스 로딩 결과 컨테이너
/// </summary>
public class ResourceData
{
    public List<VirtualFile> Files { get; set; } = new List<VirtualFile>();
}

/// <summary>
/// [최적화 완료] AYD 및 복합 컨테이너 로더
/// - 양파껍질 까기(재귀적 압축/암호화 해제) 로직 구현
/// - Span<byte> 기반 헤더 체크로 성능 향상
/// </summary>
public class AydLoader
{
    // 재귀 깊이 제한 (무한 루프 방지)
    private int _recursionDepth = 0;
    private const int MAX_DEPTH = 20;

    public ResourceData Load(byte[] fileData)
    {
        if (fileData == null || fileData.Length == 0)
            throw new ArgumentException("File data is empty");

        // 무한 루프 방지
        if (_recursionDepth > MAX_DEPTH)
        {
            return new ResourceData
            {
                Files = new List<VirtualFile>
                {
                    new VirtualFile { FileName = "dump_limit_reached.bin", Data = fileData }
                }
            };
        }
        _recursionDepth++;

        try
        {
            // [최적화] Span을 사용하여 메모리 복사 없이 헤더 확인
            ReadOnlySpan<byte> header = fileData.AsSpan();

            // [Step 1] ZIP 파일 확인 (PK..)
            if (IsZipHeader(header))
            {
                try
                {
                    var extracted = Unzip(fileData);
                    if (extracted.Count > 0)
                    {
                        // 압축 푼 내용물도 재귀적으로 검사 (파일 안에 또 파일이 있을 수 있음)
                        return ProcessExtractedFiles(extracted);
                    }
                }
                catch { /* ZIP 파싱 실패 시 다음 단계로 */ }
            }

            // [Step 2] Level-5 압축 확인 (*lnW, LZ10 등)
            if (IsLevel5Header(header))
            {
                try
                {
                    // Level5Decompressor 사용 (Native DLL or Managed)
                    // 만약 Native DLL을 못 쓰면 순수 C# 구현체로 대체 필요
                    byte[] rawData = Compressor.Decompress(fileData);

                    if (rawData.Length > 0)
                    {
                        return Load(rawData); // 재귀 호출
                    }
                }
                catch { /* 압축 해제 실패 시 다음 단계로 */ }
            }

            // [Step 3] GZIP 확인
            if (IsGZipHeader(header))
            {
                try
                {
                    using (var ms = new MemoryStream(fileData))
                    using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
                    using (var outMs = new MemoryStream())
                    {
                        gzip.CopyTo(outMs);
                        return Load(outMs.ToArray());
                    }
                }
                catch { /* GZIP 아님 */ }
            }

            // [Step 4] AES 암호화 확인 (9000번대 파일 등)
            // 파일이 어느 정도 크고(16바이트 이상), 앞선 압축 헤더가 없다면 암호화일 확률 높음
            if (fileData.Length > 16)
            {
                try
                {
                    // 임시 파일명으로 복호화 시도
                    byte[] decrypted = YokaiDecrypter.DecryptFile("temp.ez", fileData);

                    // 복호화가 의미가 있었는지 확인 (데이터가 변했는지)
                    // SequenceEqual은 비용이 들지만 정확성을 위해 필요
                    if (decrypted != null && decrypted.Length > 0 && !decrypted.SequenceEqual(fileData))
                    {
                        return Load(decrypted); // 풀린 데이터로 다시 시도
                    }
                }
                catch { /* 복호화 실패 시 원본 그대로 사용 */ }
            }

            // [Step 5] 더 이상 깔 껍질이 없음 (최종 알맹이)
            return new ResourceData
            {
                Files = new List<VirtualFile>
                {
                    new VirtualFile { FileName = "content.bin", Data = fileData }
                }
            };
        }
        finally
        {
            _recursionDepth--;
        }
    }

    private ResourceData ProcessExtractedFiles(List<VirtualFile> extractedFiles)
    {
        var finalFiles = new List<VirtualFile>();

        foreach (var file in extractedFiles)
        {
            // 확장자 및 헤더 검사를 통해 재귀 로딩 결정
            string ext = file.Extension;

            // 또 까야 하는 컨테이너(.ez, .ayd)이거나, 헤더가 의심스러우면
            if (ext == ".ayd" || ext == ".ez" || ext == ".xpck" || IsLevel5Header(file.Data))
            {
                try
                {
                    var innerData = Load(file.Data);
                    finalFiles.AddRange(innerData.Files);
                }
                catch
                {
                    // 실패하면 그냥 파일 자체를 추가
                    finalFiles.Add(file);
                }
            }
            else
            {
                // 일반 리소스 파일
                finalFiles.Add(file);
            }
        }

        return new ResourceData { Files = finalFiles };
    }

    // ---------------------------------------------------------
    // Header Checks (Zero-Allocation with Span)
    // ---------------------------------------------------------

    private bool IsZipHeader(ReadOnlySpan<byte> data)
    {
        return data.Length >= 4 &&
               data[0] == 0x50 && data[1] == 0x4B && data[2] == 0x03 && data[3] == 0x04;
    }

    private bool IsGZipHeader(ReadOnlySpan<byte> data)
    {
        return data.Length >= 2 &&
               data[0] == 0x1F && data[1] == 0x8B;
    }

    private bool IsLevel5Header(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4) return false;

        // 1. *lnW (압축 헤더)
        if (data[2] == 'n' && data[3] == 'W') return true;

        // 2. 0x10, 0x11 (Raw LZ10)
        if (data[0] == 0x10 || data[0] == 0x11) return true;

        return false;
    }

    // Helper to allow checking array directly
    private bool IsLevel5Header(byte[] data) => IsLevel5Header(data.AsSpan());

    private List<VirtualFile> Unzip(byte[] data)
    {
        var results = new List<VirtualFile>();
        using (var ms = new MemoryStream(data))
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
        {
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                using (var entryStream = entry.Open())
                using (var outputMs = new MemoryStream())
                {
                    entryStream.CopyTo(outputMs);
                    results.Add(new VirtualFile
                    {
                        FileName = entry.FullName,
                        Data = outputMs.ToArray()
                    });
                }
            }
        }
        return results;
    }
}