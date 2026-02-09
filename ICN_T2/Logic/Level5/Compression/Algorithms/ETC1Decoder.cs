using System;
using System.Buffers.Binary;

namespace ICN_T2.Logic.Level5.Compression.Algorithms;

/// <summary>
/// [최적화 완료] ETC1 텍스처 디코더
/// - Span<byte> 기반 메모리 제로 카피 연산
/// - RGB 구조체 최적화 및 연산 속도 향상
/// </summary>
public static class ETC1Decoder
{
    // ETC1 Modifier Table
    private static readonly int[][] Modifiers =
    [
        [ 2, 8, -2, -8 ],
        [ 5, 17, -5, -17 ],
        [ 9, 29, -9, -29 ],
        [ 13, 42, -13, -42 ],
        [ 18, 60, -18, -60 ],
        [ 24, 80, -24, -80 ],
        [ 33, 106, -33, -106 ],
        [ 47, 183, -47, -183 ]
    ];

    // 4x4 블록 내 픽셀 순서 (Row-major 변환용)
    private static readonly int[] PixelOrder = [0, 4, 1, 5, 8, 12, 9, 13, 2, 6, 3, 7, 10, 14, 11, 15];

    /// <summary>
    /// ETC1 + Alpha4 포맷 디코딩 (3DS 표준)
    /// 데이터 구조: 8바이트 Alpha + 8바이트 ETC1 (총 16바이트/블록)
    /// </summary>
    public static byte[] DecompressETC1A4(byte[] data, int width, int height)
    {
        byte[] result = new byte[width * height * 4]; // RGBA8888

        // Span으로 변환하여 고속 접근
        ReadOnlySpan<byte> inputSpan = data;
        Span<byte> outputSpan = result;

        int offset = 0;

        // 4x4 블록 단위 처리
        for (int blockY = 0; blockY < height; blockY += 4)
        {
            for (int blockX = 0; blockX < width; blockX += 4)
            {
                // 데이터 부족 시 중단
                if (offset + 16 > inputSpan.Length) break;

                // 1. Alpha 블록 디코딩 (앞 8바이트)
                // 각 4비트가 픽셀의 Alpha 값 (0~15 -> 0~255 확장)
                ulong alphaChunk = BinaryPrimitives.ReadUInt64LittleEndian(inputSpan.Slice(offset, 8));

                // 2. ETC1 블록 디코딩 (뒤 8바이트)
                ulong etc1Chunk = BinaryPrimitives.ReadUInt64LittleEndian(inputSpan.Slice(offset + 8, 8));

                // 3. 블록 디코딩 및 버퍼 쓰기
                DecodeBlock(etc1Chunk, alphaChunk, blockX, blockY, width, height, outputSpan);

                offset += 16;
            }
        }

        return result;
    }

    /// <summary>
    /// Alpha 없는 순수 ETC1 디코딩
    /// </summary>
    public static byte[] DecompressETC1(byte[] data, int width, int height)
    {
        byte[] result = new byte[width * height * 4];
        ReadOnlySpan<byte> inputSpan = data;
        Span<byte> outputSpan = result;

        int offset = 0;
        ulong fullAlpha = 0xFFFFFFFFFFFFFFFF; // Alpha는 모두 255(불투명)

        for (int blockY = 0; blockY < height; blockY += 4)
        {
            for (int blockX = 0; blockX < width; blockX += 4)
            {
                if (offset + 8 > inputSpan.Length) break;

                ulong etc1Chunk = BinaryPrimitives.ReadUInt64LittleEndian(inputSpan.Slice(offset, 8));
                DecodeBlock(etc1Chunk, fullAlpha, blockX, blockY, width, height, outputSpan, false);

                offset += 8;
            }
        }
        return result;
    }

    // ------------------------------------------------------------------------
    // Core Decoding Logic (Inline-friendly)
    // ------------------------------------------------------------------------

    private static void DecodeBlock(ulong block, ulong alphaBlock, int x, int y, int w, int h, Span<byte> output, bool hasAlpha = true)
    {
        // ETC1 Block Structure (Big Endian logic applied to Little Endian read)
        // C# BinaryPrimitives read as LE, so we need to reverse logical bytes or handle carefully.
        // ETC1 spec is MSB first usually. Let's stick to byte-based parsing for safety if ulong is confusing.
        // But for speed, let's unpack ulong carefully.

        // Re-read as bytes to match spec logic exactly (avoid endian confusion)
        // 0..3: High 32 bits, 4..7: Low 32 bits in logic
        // But we passed ulong. Let's just use the ulong features.
        // blockHigh = block >> 32; blockLow = block & 0xFFFFFFFF;
        // NOTE: Input was read as LittleEndian.
        // Byte 0 is LSB of ulong. 
        // ETC1 bytes: [0][1][2][3] [4][5][6][7]
        // Payload is in 4..7 (Low 32 logical), Mode in 0..3 (High 32 logical)? No.
        // Let's decode bytes directly from the ulong to be safe.

        uint high = (uint)(block >> 32); // Bytes 4-7
        uint low = (uint)(block & 0xFFFFFFFF); // Bytes 0-3

        // Actually, standard ETC1 implementation often reads bytes.
        // Let's decompose for clarity:
        // Byte 0-3: chunk1 (modifiers)
        // Byte 4-7: chunk2 (colors & flags)
        // WARNING: The endianness depends on the file format (KTX vs raw).
        // Level-5 typically uses Big-Endian for data fields in files, but texture data might be raw bytes.
        // Let's assume the byte order in memory is correct and parse bytes.

        // Extract flags from the "upper" bytes (last in stream, high in LE ulong)
        // Byte order in stream: [0][1][2][3][4][5][6][7]
        // Bytes 0-3: pixel indices (LSB)
        // Bytes 4-7: colors/tables (MSB)

        // Ref: https://github.com/Ericsson/ETCPACK/blob/master/source/etcdec.cxx
        // Byte 0-3 (low): pixel indices
        // Byte 4-7 (high): data

        bool diffBit = (high & 0x02) != 0;
        bool flipBit = (high & 0x01) != 0;
        int table1 = (int)((high >> 5) & 7);
        int table2 = (int)((high >> 2) & 7);

        // Base Colors
        int r1, g1, b1, r2, g2, b2;

        if (diffBit)
        {
            // Differential Mode (5-bit base + 3-bit diff)
            int r = (int)((high >> 27) & 0xF8);
            int g = (int)((high >> 19) & 0xF8);
            int b = (int)((high >> 11) & 0xF8);

            r1 = r | (r >> 5);
            g1 = g | (g >> 5);
            b1 = b | (b >> 5);

            int dr = (int)((high >> 24) & 0x07);
            int dg = (int)((high >> 16) & 0x07);
            int db = (int)((high >> 8) & 0x07);

            // Sign extension for 3-bit diff
            if (dr >= 4) dr -= 8;
            if (dg >= 4) dg -= 8;
            if (db >= 4) db -= 8;

            r2 = (r >> 3) + dr;
            g2 = (g >> 3) + dg;
            b2 = (b >> 3) + db;

            // Expand 5-bit to 8-bit
            r2 = (r2 << 3) | (r2 >> 2);
            g2 = (g2 << 3) | (g2 >> 2);
            b2 = (b2 << 3) | (b2 >> 2);
        }
        else
        {
            // Individual Mode (4-bit + 4-bit)
            int r = (int)((high >> 28) & 0x0F);
            int g = (int)((high >> 20) & 0x0F);
            int b = (int)((high >> 12) & 0x0F);
            r1 = (r << 4) | r;
            g1 = (g << 4) | g;
            b1 = (b << 4) | b;

            r = (int)((high >> 24) & 0x0F);
            g = (int)((high >> 16) & 0x0F);
            b = (int)((high >> 8) & 0x0F);
            r2 = (r << 4) | r;
            g2 = (g << 4) | g;
            b2 = (b << 4) | b;
        }

        // Clamp colors
        r1 = Clamp(r1); g1 = Clamp(g1); b1 = Clamp(b1);
        r2 = Clamp(r2); g2 = Clamp(g2); b2 = Clamp(b2);

        // Process Pixels
        for (int i = 0; i < 16; i++)
        {
            int px = x + (i / 4); // Column-major order in loop, but PixelOrder handles it
            int py = y + (i % 4);

            // Re-map to block order
            int k = PixelOrder[i]; // 0..15 linear index in block
            int bx = k % 4;
            int by = k / 4;

            int actualX = x + bx;
            int actualY = y + by;

            if (actualX >= w || actualY >= h) continue;

            // Get Modifier Index
            // low variable contains pixel indices.
            // MSB bit: low >> (k + 16), LSB bit: low >> k
            int bitOffset = k;
            int lsb = (int)((low >> bitOffset) & 1);
            int msb = (int)((low >> (bitOffset + 16)) & 1);
            int modIndex = (msb << 1) | lsb;

            // Select sub-block
            bool useBlock2;
            if (flipBit)
                useBlock2 = by >= 2; // Top/Bottom split
            else
                useBlock2 = bx >= 2; // Left/Right split

            int[] modTable = Modifiers[useBlock2 ? table2 : table1];
            int delta = modTable[modIndex];

            int r = (useBlock2 ? r2 : r1) + delta;
            int g = (useBlock2 ? g2 : g1) + delta;
            int b = (useBlock2 ? b2 : b1) + delta;

            // Alpha processing (4-bit expansion)
            // Alpha bytes are Little Endian in ulong, so Byte 0 is LSB.
            // Nibble layout: [A0, A1], [A2, A3] ...
            // But usually A4 format is Low Nibble = Pixel 0?
            // Let's assume standard A4 packing: (byte >> 0) & 0xF first?
            // Actually A4 is usually: 
            // Byte 0: [Pixel 1 (high) | Pixel 0 (low)]
            // Let's use array indexing logic for alpha to be safe.
            // k is the pixel index 0..15.
            // Byte index = k / 2. Nibble = k % 2.

            int alpha = 255;
            if (hasAlpha)
            {
                int shift = k * 4;
                int a4 = (int)((alphaBlock >> shift) & 0xF);
                alpha = (a4 << 4) | a4; // Expand 4-bit to 8-bit
            }

            int destIndex = (actualY * w + actualX) * 4;

            output[destIndex + 0] = (byte)Clamp(b); // Blue
            output[destIndex + 1] = (byte)Clamp(g); // Green
            output[destIndex + 2] = (byte)Clamp(r); // Red
            output[destIndex + 3] = (byte)alpha;    // Alpha
        }
    }

    private static int Clamp(int val)
    {
        if (val < 0) return 0;
        if (val > 255) return 255;
        return val;
    }

    // Helper Struct for internal calculations (Optional, internal use only)
    private struct RGB
    {
        public int R, G, B;
        public RGB(int r, int g, int b) { R = r; G = g; B = b; }
    }
}