using System.Runtime.InteropServices;

namespace ICN_T2.Logic.Level5.Archives.ARC0
{
    public class ARC0Support
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header
        {
            public uint Magic; // 0x30435241 "ARC0"
            public int DirectoryEntriesOffset;
            public int DirectoryHashOffset;
            public int FileEntriesOffset;
            public int NameOffset;
            public int DataOffset;
            public short DirectoryEntriesCount;
            public short DirectoryHashCount;
            public int FileEntriesCount;
            public int TableChunkSize; // 메타데이터 영역 전체 크기
            public int Zero1;

            // Hashes?
            public uint Unk2;
            public uint Unk3;
            public uint Unk4;
            public uint Unk5;

            public int DirectoryCount; // 보통 DirectoryEntriesCount와 같음
            public int FileCount;      // 보통 FileEntriesCount와 같음
            public uint Unk7;
            public int Zero2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DirectoryEntry
        {
            public uint Crc32;
            public ushort FirstDirectoryIndex;
            public short DirectoryCount;
            public ushort FirstFileIndex;
            public short FileCount;
            public int FileNameStartOffset;
            public int DirectoryNameStartOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct FileEntry
        {
            public uint Crc32;
            public uint NameOffsetInFolder;
            public uint FileOffset; // DataOffset 기준 상대 경로
            public uint FileSize;
        }
    }
}