using ICN_T2.Logic.Level5.Compression;
using System;

namespace ICN_T2.Logic.Level5.Compression.Algorithms;

/// <summary>
/// Level-5 Huffman 압축 (4bit / 8bit)
/// </summary>
public class Huffman : ICompression
{
    private readonly int _bitDepth;

    /// <param name="bitDepth">4 또는 8 (비트 깊이)</param>
    public Huffman(int bitDepth)
    {
        _bitDepth = bitDepth;
    }

    public byte[] Compress(byte[] data)
    {
        throw new NotImplementedException("Huffman 압축은 아직 구현되지 않았습니다.");
    }

    public byte[] Decompress(byte[] data)
    {
        // 스트림 생성 없이 Span을 넘겨서 바로 처리
        return HuffmanDecoder.Decompress(data, _bitDepth);
    }
}