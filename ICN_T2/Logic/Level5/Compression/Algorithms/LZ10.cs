using System;
using System.Collections.Generic;
using System.IO;
using ICN_T2.Logic.Level5.Compression;

namespace ICN_T2.Logic.Level5.Compression.Algorithms;

/// <summary>
/// LZ10 압축/해제 (Level-5 커스텀 헤더 포맷)
/// Compressor.Decompress()에서 전체 데이터(헤더 포함)를 넘겨받으므로
/// 첫 4바이트(Level-5 커스텀 헤더)를 건너뛰고 디코딩합니다.
/// 최종 크기 절단은 Compressor 측에서 처리합니다.
/// </summary>
public class LZ10 : ICompression
{
    public byte[] Decompress(byte[] data)
    {
        if (data == null || data.Length < 5) return Array.Empty<byte>();

        // Level-5 커스텀 헤더 4바이트를 건너뛰고 시작 (레거시 동일)
        int p = 4;
        int op = 0;
        int mask = 0;
        int flag = 0;

        List<byte> output = new List<byte>();

        while (p < data.Length)
        {
            if (mask == 0)
            {
                flag = data[p];
                p += 1;
                mask = 0x80;
            }

            if ((flag & mask) == 0)
            {
                // 비압축: 1바이트 그대로 복사
                if (p + 1 > data.Length)
                    break;

                output.Add(data[p]);
                p += 1;
                op += 1;
            }
            else
            {
                // 압축: (Length, Disp) 튜플 (2바이트)
                if (p + 2 > data.Length)
                    break;

                int dat = data[p] << 8 | data[p + 1];
                p += 2;
                int pos = (dat & 0x0FFF) + 1;
                int length = (dat >> 12) + 3;

                for (int i = 0; i < length; i++)
                {
                    if (op - pos >= 0)
                    {
                        output.Add((byte)(op - pos < output.Count ? output[op - pos] : 0));
                        op += 1;
                    }
                }
            }

            mask >>= 1;
        }

        return output.ToArray();
    }

    public byte[] Compress(byte[] data)
    {
        if (data == null || data.Length == 0) return Array.Empty<byte>();

        using MemoryStream ms = new();

        // Level-5 커스텀 헤더 작성 (method=1 in lower 3 bits)
        int len = data.Length;
        ms.WriteByte((byte)((byte)(len << 3) | 1));
        ms.WriteByte((byte)(len >> 5));
        ms.WriteByte((byte)(len >> 13));
        ms.WriteByte((byte)(len >> 21));

        int inPos = 0;
        while (inPos < len)
        {
            byte flags = 0;
            long flagPos = ms.Position;
            ms.WriteByte(0);

            byte[] buffer = new byte[16];
            int bufferPos = 0;

            for (int i = 0; i < 8; i++)
            {
                if (inPos >= len) break;

                int bestLen = 0;
                int bestDisp = 0;

                int maxDist = Math.Min(inPos, 4096);
                int maxLen = Math.Min(len - inPos, 18);

                if (maxLen >= 3)
                {
                    for (int dist = 1; dist <= maxDist; dist++)
                    {
                        int matchLen = 0;
                        while (matchLen < maxLen && data[inPos + matchLen] == data[inPos - dist + matchLen])
                            matchLen++;

                        if (matchLen > bestLen)
                        {
                            bestLen = matchLen;
                            bestDisp = dist;
                            if (bestLen == maxLen) break;
                        }
                    }
                }

                if (bestLen >= 3)
                {
                    flags |= (byte)(0x80 >> i);

                    int encodedLen = bestLen - 3;
                    int encodedDisp = bestDisp - 1;

                    buffer[bufferPos++] = (byte)(((encodedLen << 4) & 0xF0) | ((encodedDisp >> 8) & 0x0F));
                    buffer[bufferPos++] = (byte)(encodedDisp & 0xFF);

                    inPos += bestLen;
                }
                else
                {
                    buffer[bufferPos++] = data[inPos++];
                }
            }

            long currentPos = ms.Position;
            ms.Position = flagPos;
            ms.WriteByte(flags);
            ms.Position = currentPos;
            ms.Write(buffer, 0, bufferPos);
        }

        return ms.ToArray();
    }
}
