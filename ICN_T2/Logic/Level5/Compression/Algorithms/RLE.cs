using System;
using System.Collections.Generic;
using System.IO;
using ICN_T2.Logic.Level5.Compression;

namespace ICN_T2.Logic.Level5.Compression.Algorithms;

/// <summary>
/// RLE 압축/해제 (Level-5 커스텀 헤더 포맷)
/// Compressor.Decompress()에서 전체 데이터(헤더 포함)를 넘겨받으므로
/// 첫 바이트(메소드 인덱스 포함)를 건너뛰고, 바이트 1-3에서 크기를 읽습니다.
/// 최종 크기 절단은 Compressor 측에서 처리합니다.
/// </summary>
public class RLE : ICompression
{
    public byte[] Decompress(byte[] data)
    {
        if (data == null || data.Length < 4) return Array.Empty<byte>();

        int p = 0;

        // 첫 바이트 건너뛰기 (Level-5 헤더의 메소드+사이즈 혼합 바이트)
        p++;

        // 크기 읽기: 바이트 1, 2, 3 (레거시 동일)
        int decompressedSize = (data[p++] & 0xFF)
                | ((data[p++] & 0xFF) << 8)
                | ((data[p++] & 0xFF) << 16);

        if (decompressedSize == 0 && p < data.Length)
        {
            decompressedSize |= ((data[p++] & 0xFF) << 24);
        }

        List<byte> output = new List<byte>();
        long readBytes = p;

        while (p < data.Length)
        {
            int flag = (byte)data[p++];
            readBytes++;

            bool compressed = (flag & 0x80) > 0;
            int length = flag & 0x7F;

            if (compressed)
                length += 3;
            else
                length += 1;

            if (compressed)
            {
                int val = (byte)data[p++];
                readBytes++;

                byte bval = (byte)val;
                for (int i = 0; i < length; i++)
                {
                    output.Add(bval);
                }
            }
            else
            {
                int tryReadLength = length;
                if (readBytes + length > data.Length)
                    tryReadLength = (int)(data.Length - readBytes);

                readBytes += tryReadLength;

                for (int i = 0; i < tryReadLength; i++)
                {
                    output.Add((byte)(data[p++] & 0xFF));
                }
            }
        }

        return output.ToArray();
    }

    public byte[] Compress(byte[] data)
    {
        if (data == null || data.Length == 0) return Array.Empty<byte>();

        using MemoryStream ms = new();

        // Level-5 커스텀 헤더 (method=4 in lower 3 bits)
        int len = data.Length;
        ms.WriteByte((byte)((byte)(len << 3) | 4));
        ms.WriteByte((byte)(len >> 5));
        ms.WriteByte((byte)(len >> 13));
        ms.WriteByte((byte)(len >> 21));

        int inPos = 0;
        while (inPos < len)
        {
            int runLen = 1;
            while (inPos + runLen < len && runLen < 130 && data[inPos] == data[inPos + runLen])
                runLen++;

            if (runLen >= 3)
            {
                ms.WriteByte((byte)((runLen - 3) | 0x80));
                ms.WriteByte(data[inPos]);
                inPos += runLen;
            }
            else
            {
                int rawLen = 0;
                while (inPos + rawLen < len && rawLen < 128)
                {
                    if (inPos + rawLen + 2 < len &&
                        data[inPos + rawLen] == data[inPos + rawLen + 1] &&
                        data[inPos + rawLen] == data[inPos + rawLen + 2])
                        break;

                    rawLen++;
                }

                ms.WriteByte((byte)(rawLen - 1));
                ms.Write(data, inPos, rawLen);
                inPos += rawLen;
            }
        }

        return ms.ToArray();
    }
}
