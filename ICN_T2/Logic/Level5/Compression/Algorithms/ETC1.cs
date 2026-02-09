using ICN_T2.Logic.Level5.Compression.Algorithms;
using ICN_T2.Logic.Level5.Compression; // ICompression 인터페이스
using System;

namespace ICN_T2.Logic.Level5.Compression.Algorithms;

/// <summary>
/// ETC1 (Ericsson Texture Compression) 래퍼
/// 주로 3DS 게임의 텍스처 처리에 사용됩니다.
/// </summary>
public class ETC1 : ICompression
{
    private readonly int _width;
    private readonly int _height;
    private readonly bool _hasAlpha;

    public ETC1(bool hasAlpha, int width, int height)
    {
        _hasAlpha = hasAlpha;
        _width = width;
        _height = height;
    }

    public byte[] Compress(byte[] data)
    {
        throw new NotImplementedException("ETC1 압축 기능은 아직 구현되지 않았습니다.");
    }

    public byte[] Decompress(byte[] data)
    {
        if (_hasAlpha)
        {
            // ETC1 + Alpha4 (Level-5 3DS 포맷)
            return ETC1Decoder.DecompressETC1A4(data, _width, _height);
        }
        else
        {
            // 표준 ETC1 (필요 시 구현 확장)
            // 여기서는 일단 Alpha가 없는 경우도 ETC1 로직을 타거나 예외 처리
            // 보통 Level-5 텍스처는 Alpha가 포함된 경우가 많음
            return ETC1Decoder.DecompressETC1(data, _width, _height);
        }
    }
}