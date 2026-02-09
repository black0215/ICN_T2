using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using ICN_T2.Tools; // Crc32
using ICN_T2.Tools.IO; // BinaryDataReader/Writer
using ICN_T2.Logic.VirtualFileSystem;
using ICN_T2.Logic.Level5.Compression;
using ICN_T2.Logic.Level5.Compression.Algorithms; // LZ10

namespace ICN_T2.Logic.Level5.Archives.XPCK;

public class XPCK
{
    public VirtualDirectory Directory { get; private set; }

    public XPCK()
    {
        Directory = new VirtualDirectory("/");
    }

    public XPCK(Stream stream)
    {
        Directory = new VirtualDirectory("/");
        Open(stream);
    }

    public XPCK(byte[] data)
    {
        Directory = new VirtualDirectory("/");
        using (MemoryStream ms = new MemoryStream(data))
        {
            Open(ms);
        }
    }

    private void Open(Stream stream)
    {
        using (BinaryDataReader reader = new BinaryDataReader(stream))
        {
            // 1. 헤더 읽기
            var header = reader.ReadStruct<XPCKSupport.Header>();
            if (header.Magic != 0x4B435058) // XPCK
                throw new InvalidDataException("Invalid XPCK Magic");

            // 2. 파일 정보 읽기
            reader.Seek((uint)header.InfoOffset);
            var entries = reader.ReadMultipleStruct<XPCKSupport.FileEntry>(header.FileCount);

            // 3. 파일명 테이블 읽기 (LZ10 압축됨)
            reader.Seek((uint)header.NameTableOffset);
            // 압축된 이름 테이블 읽기 (InfoOffset 전까지가 테이블 크기라고 가정하거나 헤더 사이즈 이용)
            // 헤더에 NameTableSize가 있으니 그걸 씁니다. (SizeShifted << 2)
            // 하지만 정확한 압축 크기는 계산해야 함. 보통 (DataOffset - NameTableOffset)
            int compressedNameSize = (int)(header.DataOffset - header.NameTableOffset);
            byte[] compressedNames = reader.GetSection((uint)header.NameTableOffset, compressedNameSize);

            // 이름 테이블 압축 해제
            byte[] nameTable = Compressor.Decompress(compressedNames);

            // 4. 파일 생성
            using (BinaryDataReader nameReader = new BinaryDataReader(nameTable))
            {
                Encoding sjis = Encoding.GetEncoding("Shift-JIS");

                foreach (var entry in entries)
                {
                    nameReader.Seek(entry.NameOffset);
                    string fileName = nameReader.ReadString(sjis);

                    long fileOffset = header.DataOffset + entry.GetFileOffset();
                    int fileSize = entry.GetFileSize();

                    // SubMemoryStream으로 데이터 매핑
                    Directory.AddFile(fileName, new SubMemoryStream(stream, fileOffset, fileSize));
                }
            }
        }

        // 보기 좋게 정렬
        Directory.SortAlphabetically();
    }

    public void Save(string outputParam)
    {
        using (FileStream fs = new FileStream(outputParam, FileMode.Create, FileAccess.Write))
        using (BinaryDataWriter writer = new BinaryDataWriter(fs))
        {
            Encoding sjis = Encoding.GetEncoding("Shift-JIS");

            // CRC32 기준 정렬 (XPCK 표준)
            var sortedFiles = Directory.GetFlatFileList()
                .Select(f => new { Name = f.Key, Data = f.Value, Crc = Crc32.Compute(sjis.GetBytes(f.Key)) })
                .OrderBy(x => x.Crc)
                .ToList();

            // 1. 이름 테이블 생성 (메모리)
            List<byte> nameTableBuilder = new List<byte>();
            Dictionary<string, ushort> nameOffsets = new Dictionary<string, ushort>();

            foreach (var file in sortedFiles)
            {
                nameOffsets[file.Name] = (ushort)nameTableBuilder.Count;
                nameTableBuilder.AddRange(sjis.GetBytes(file.Name));
                nameTableBuilder.Add(0x00); // Null Terminator
            }

            // 이름 테이블 압축 (LZ10)
            byte[] compressedNames = new LZ10().Compress(nameTableBuilder.ToArray());

            // 2. 오프셋 계산 및 헤더 예약
            // XPCK 구조: [Header(0x14)] [FileEntries(Count*12)] [Align] [NameTable] [Align] [Data]

            long headerSize = 0x14;
            long infoOffset = headerSize; // 헤더 바로 뒤
            long infoSize = sortedFiles.Count * 12; // 12 bytes per entry

            long nameTableOffset = infoOffset + infoSize;
            // 4바이트 정렬
            if (nameTableOffset % 4 != 0) nameTableOffset += (4 - (nameTableOffset % 4));

            long dataOffset = nameTableOffset + compressedNames.Length;
            // 16바이트 정렬 (데이터 시작 전)
            if (dataOffset % 16 != 0) dataOffset += (16 - (dataOffset % 16));

            // 3. 헤더 작성
            XPCKSupport.Header header = new XPCKSupport.Header();
            header.Magic = 0x4B435058; // XPCK
            XPCKSupport.EncodeFileCount(sortedFiles.Count, out header.Fc1, out header.Fc2);

            header.InfoOffsetShifted = (ushort)(infoOffset >> 2);
            header.NameTableOffsetShifted = (ushort)(nameTableOffset >> 2);
            header.DataOffsetShifted = (ushort)(dataOffset >> 2);

            header.InfoSizeShifted = (ushort)(infoSize >> 2);
            header.NameTableSizeShifted = (ushort)(compressedNames.Length >> 2);

            // 데이터 사이즈는 나중에 계산

            writer.WriteStruct(header);

            // 4. File Info 작성 (임시 - 나중에 오프셋 채우러 돌아옴)
            writer.Seek((uint)infoOffset);
            long fileInfoStart = writer.Position;
            writer.Write(new byte[infoSize]); // 0으로 채움
            writer.WriteAlignment(4); // Align

            // 5. Name Table 작성
            writer.Seek((uint)nameTableOffset);
            writer.Write(compressedNames);
            writer.WriteAlignment(16); // Align

            // 6. Data 작성
            writer.Seek((uint)dataOffset);
            long dataStart = writer.Position;

            List<XPCKSupport.FileEntry> entries = new List<XPCKSupport.FileEntry>();

            foreach (var file in sortedFiles)
            {
                // 현재 상대 오프셋 (DataOffset 기준)
                long relativeOffset = writer.Position - dataStart;

                // 데이터 쓰기
                file.Data.CopyTo(fs);

                // 정렬 (각 파일 끝난 후 4바이트 or 16바이트? 보통 XPCK 파일 간 정렬은 16바이트)
                writer.WriteAlignment(16);

                // Entry 생성
                long fileSize = file.Data.Length;

                // Offset >> 2 Shifted
                uint shiftedOffset = (uint)(relativeOffset >> 2);

                XPCKSupport.FileEntry entry = new XPCKSupport.FileEntry();
                entry.Crc32 = file.Crc;
                entry.NameOffset = nameOffsets[file.Name];

                entry.OffsetLow = (ushort)(shiftedOffset & 0xFFFF);
                entry.OffsetHigh = (byte)((shiftedOffset >> 16) & 0xFF);

                entry.SizeLow = (ushort)(fileSize & 0xFFFF);
                entry.SizeHigh = (byte)((fileSize >> 16) & 0xFF);

                entries.Add(entry);
            }

            long dataEnd = writer.Position;
            header.DataSizeShifted = (uint)((dataEnd - dataStart) >> 2);

            // 7. 헤더 & Info 업데이트
            writer.Seek(0);
            writer.WriteStruct(header);

            writer.Seek((uint)infoOffset);
            foreach (var entry in entries)
            {
                writer.WriteStruct(entry); // 이제 빠르고 안전하게 씀
            }
        }
    }
}