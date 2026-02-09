using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ICN_T2.Logic.Level5.Compression.Algorithms;

/// <summary>
/// Level-5 Huffman 디코더 (레거시 로직 기반)
/// Compressor.Decompress()에서 전체 데이터(헤더 포함)를 넘겨받으므로
/// 첫 4바이트(Level-5 커스텀 헤더)를 읽어 크기를 파싱하고, 이후 트리+데이터를 디코딩합니다.
/// </summary>
public static class HuffmanDecoder
{
    public static byte[] Decompress(ReadOnlySpan<byte> data, int bitDepth)
    {
        if (data.Length < 4) return Array.Empty<byte>();

        // Level-5 커스텀 헤더에서 크기 파싱
        int decompressedSize = (data[0] >> 3) | (data[1] << 5) | (data[2] << 13) | (data[3] << 21);

        // 헤더 이후부터 스트림으로 읽기 (레거시와 동일한 BinaryReader 방식)
        using var ms = new MemoryStream(data.Slice(4).ToArray());
        using var br = new BinaryReader(ms, Encoding.ASCII, true);

        int resultLen = decompressedSize * 8 / bitDepth;
        byte[] result = new byte[resultLen];

        var treeSize = br.ReadByte();
        var treeRoot = br.ReadByte();
        var treeBuffer = br.ReadBytes(treeSize * 2);

        // 레거시와 동일한 디코딩 루프: next는 누적되고, leaf에서 리셋됨
        for (int i = 0, code = 0, next = 0, pos = treeRoot, resultPos = 0; resultPos < result.Length; i++)
        {
            if (i % 32 == 0)
            {
                if (ms.Position + 4 > ms.Length) break;
                code = br.ReadInt32();
            }

            next += ((pos & 0x3F) << 1) + 2;
            var direction = (code >> (31 - (i % 32))) % 2 == 0 ? 2 : 1;
            var leaf = (pos >> 5 >> direction) % 2 != 0;

            if (next - direction < 0 || next - direction >= treeBuffer.Length) break;

            pos = treeBuffer[next - direction];
            if (leaf)
            {
                result[resultPos++] = (byte)pos;
                pos = treeRoot;
                next = 0;
            }
        }

        // 결과 조합
        if (bitDepth == 8)
        {
            return result;
        }
        else
        {
            // 4-bit 모드: LowNibbleFirst (레거시 기본값)
            var combinedData = new byte[decompressedSize];
            for (int j = 0; j < decompressedSize && 2 * j + 1 < result.Length; j++)
            {
                combinedData[j] = (byte)(result[2 * j] | (result[2 * j + 1] << 4));
            }
            return combinedData;
        }
    }
}
