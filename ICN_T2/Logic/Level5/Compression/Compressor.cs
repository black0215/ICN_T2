using ICN_T2.Logic.Level5.Compression.Algorithms;
using System;

namespace ICN_T2.Logic.Level5.Compression;

/// <summary>
/// Level-5 압축 알고리즘 팩토리
/// 헤더 구조: 하위 3비트 = 압축 방식, 나머지 비트 = 압축 해제 크기
/// </summary>
public static class Compressor
{
    /// <summary>
    /// 압축 방식 ID에 맞는 구현체를 반환합니다.
    /// </summary>
    public static ICompression GetCompression(uint method)
    {
        return method switch
        {
            0 => new NoCompression(),
            1 => new LZ10(),
            2 => new Huffman(4),
            3 => new Huffman(8),
            4 => new RLE(),
            5 => new Zlib(),
            _ => throw new NotSupportedException($"지원하지 않는 압축 방식입니다: {method}")
        };
    }

    /// <summary>
    /// Level-5 커스텀 헤더를 분석하여 자동으로 압축을 해제합니다.
    /// 레거시(Albatross)와 동일한 동작을 보장합니다.
    /// </summary>
    public static byte[] Decompress(byte[] data)
    {
        if (data == null || data.Length < 4) return Array.Empty<byte>();

        // Level-5 커스텀 헤더 파싱
        int size = (data[0] >> 3) | (data[1] << 5) | (data[2] << 13) | (data[3] << 21);
        uint methodIndex = (uint)data[0] & 0x7;

        ICompression method = GetCompression(methodIndex);

        // 압축 해제 후 size만큼 절단 (레거시의 .Take(size).ToArray()와 동일)
        byte[] result = method.Decompress(data);

        if (result.Length > size && size > 0)
        {
            return result.AsSpan(0, size).ToArray();
        }

        return result;
    }
}
