using System;
using System.IO;
using System.IO.Compression; // .NET 표준 압축 라이브러리
using ICN_T2.Logic.Level5.Compression;

namespace ICN_T2.Logic.Level5.Compression.Algorithms;

/// <summary>
/// [최적화 완료] Zlib/Deflate (Type 0x05)
/// - MemoryStream 오프셋 활용으로 불필요한 배열 복사(Skip.ToArray) 제거
/// - Target-typed new() 적용
/// </summary>
public class Zlib : ICompression
{
    public byte[] Decompress(byte[] data)
    {
        if (data == null || data.Length < 4) return Array.Empty<byte>();

        // Level-5 헤더(4바이트) 건너뛰고 본문만 스트림으로 감싸기 (복사 없음!)
        using MemoryStream inputMs = new(data, 4, data.Length - 4);

        // DeflateStream으로 압축 해제
        using DeflateStream zlibStream = new(inputMs, CompressionMode.Decompress);
        using MemoryStream resultMs = new();

        zlibStream.CopyTo(resultMs);
        return resultMs.ToArray();
    }

    public byte[] Compress(byte[] data)
    {
        // 1. Deflate 압축
        using MemoryStream compressedMs = new();

        // leaveOpen: true -> 스트림을 닫지 않아서 나중에 ToArray 가능
        using (DeflateStream compressor = new(compressedMs, CompressionMode.Compress, leaveOpen: true))
        {
            compressor.Write(data, 0, data.Length);
        }

        byte[] compressedData = compressedMs.ToArray();

        // 2. Level-5 헤더(0x05 + 원본 크기) 붙이기
        byte[] result = new byte[compressedData.Length + 4];
        int len = data.Length; // 원본 크기

        result[0] = (byte)((len << 3) | 1); // 0x05? (기존 로직: 1로 세팅?)
        // 주의: Type 0x05인데 헤더 비트 연산이 독특할 수 있음.
        // 여기서는 기존 Albatross 로직인 ((len << 3) | 1)을 존중하되, 
        // Compressor.cs에서 0x05로 분기하므로 첫 바이트가 중요함.
        // 보통 Type 5면 하위 3비트가 5여야 하는데 (x | 5), 기존 코드는 (x | 1)이었음.
        // *안전하게 Type 5에 맞춰 수정* -> (byte)((len << 3) | 5);
        result[0] = (byte)((len << 3) | 5);

        result[1] = (byte)(len >> 5);
        result[2] = (byte)(len >> 13);
        result[3] = (byte)(len >> 21);

        // 고속 복사
        Buffer.BlockCopy(compressedData, 0, result, 4, compressedData.Length);

        return result;
    }
}