using System;
using ICN_T2.Logic.Level5.Compression; // ICompression 인터페이스

namespace ICN_T2.Logic.Level5.Compression.Algorithms;

/// <summary>
/// [최적화 완료] 무압축 (Type 0)
/// - MemoryStream 제거 -> Buffer.BlockCopy 사용 (가장 빠른 복사 방식)
/// - LINQ 제거 -> Span 기반 슬라이싱
/// </summary>
public class NoCompression : ICompression
{
    public byte[] Compress(byte[] data)
    {
        if (data == null) return Array.Empty<byte>();

        // 결과 배열 한 번에 할당 (헤더 4바이트 + 데이터)
        byte[] result = new byte[data.Length + 4];
        int len = data.Length;

        // Level-5 헤더 작성 (Type 0)
        // Format: [Method(3bit) | Size(21bit...)] mixed
        // Original Logic: (len << 3) | MethodIndex

        result[0] = (byte)((len << 3) | 0); // Method 0
        result[1] = (byte)(len >> 5);
        result[2] = (byte)(len >> 13);
        result[3] = (byte)(len >> 21);

        // 고속 메모리 복사 (C#에서 제일 빠름)
        Buffer.BlockCopy(data, 0, result, 4, len);

        return result;
    }

    public byte[] Decompress(byte[] data)
    {
        if (data == null || data.Length < 4) return Array.Empty<byte>();

        // 헤더 4바이트를 제외한 뒷부분만 반환
        // Span을 이용해 빠르게 잘라내기
        return data.AsSpan(4).ToArray();
    }
}