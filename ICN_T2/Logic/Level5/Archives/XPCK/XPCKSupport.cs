using System;
using System.Runtime.InteropServices;

namespace ICN_T2.Logic.Level5.Archives.XPCK;

public static class XPCKSupport
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Header
    {
        public uint Magic; // XPCK
        public byte Fc1;   // File Count 1
        public byte Fc2;   // File Count 2 (High bits)

        // 원본: tmp1~6 (Offset/Size >> 2 된 값들)
        public ushort InfoOffsetShifted;
        public ushort NameTableOffsetShifted;
        public ushort DataOffsetShifted;
        public ushort InfoSizeShifted;
        public ushort NameTableSizeShifted;
        public uint DataSizeShifted;

        // Helper Properties (실제 바이트 단위 값)
        public int FileCount => (Fc2 & 0xF) << 8 | Fc1;
        public long InfoOffset => (long)InfoOffsetShifted << 2;
        public long NameTableOffset => (long)NameTableOffsetShifted << 2;
        public long DataOffset => (long)DataOffsetShifted << 2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FileEntry
    {
        public uint Crc32;
        public ushort NameOffset;

        // 24-bit Offset & Size split
        // Low 16 bits
        public ushort OffsetLow;
        public ushort SizeLow;

        // High 8 bits (Offsets are >> 2 encoded in standard XPCK, but here logic is complex)
        public byte OffsetHigh;
        public byte SizeHigh;

        // Helper to reconstruct full values
        public long GetFileOffset()
        {
            // XPCK Offset Logic: (High << 16 | Low) << 2
            uint val = ((uint)OffsetHigh << 16) | OffsetLow;
            return (long)val << 2;
        }

        public int GetFileSize()
        {
            return (SizeHigh << 16) | SizeLow;
        }
    }

    /// <summary>
    /// Level-5 특유의 파일 카운트 인코딩 방식
    /// </summary>
    public static void EncodeFileCount(int count, out byte fc1, out byte fc2)
    {
        if (count < 256)
        {
            fc1 = (byte)count;

            // Power of 2 logic (Legacy behavior preserved)
            int f2 = 1;
            while (f2 <= count) f2 *= 2;

            // Log2 calculation trick
            int coefficient = (int)Math.Log(f2, 2) * 10;
            // Hex conversion emulation (e.g. 10 -> 0x10 = 16)
            // This logic is weird but matches original behavior
            fc2 = (byte)((coefficient / 10) * 16 + (coefficient % 10));
            // Simplified: The original code did `Convert.ToInt32("0x" + coefficient, 16)`
            // If coefficient is 40, "0x40" -> 64.
            // Let's stick to the exact original logic safely:
            fc2 = Convert.ToByte(Convert.ToInt32($"0x{coefficient}", 16));
        }
        else
        {
            fc1 = (byte)(count & 0xFF);
            fc2 = (byte)((count >> 8) & 0xFF);
        }
    }
}