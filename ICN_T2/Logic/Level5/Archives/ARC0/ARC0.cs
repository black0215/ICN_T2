using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using ICN_T2.Tools;
using ICN_T2.Tools.IO;
using ICN_T2.Logic.VirtualFileSystem;
using ICN_T2.Logic.Level5.Compression;
using ICN_T2.Logic.Level5.Compression.Algorithms;

namespace ICN_T2.Logic.Level5.Archives.ARC0
{
    public class ARC0 : IArchive
    {
        public string Name => "ARC0";

        public VirtualDirectory Directory { get; set; } = new VirtualDirectory("/");
        public Stream? BaseStream;
        public ARC0Support.Header Header;

        public ARC0()
        {
            Directory = new VirtualDirectory("/");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public ARC0(Stream stream) : this()
        {
            BaseStream = stream;
            Directory = Open();
        }

        public ARC0(byte[] data) : this()
        {
            BaseStream = new MemoryStream(data);
            Directory = Open();
        }

        // ============================================================
        // OPEN
        // ============================================================

        public VirtualDirectory Open()
        {
            if (BaseStream == null) return new VirtualDirectory("/");

            Console.WriteLine("============== ARC0.Open() 시작 ==============");
            VirtualDirectory root = new VirtualDirectory("/");
            BinaryDataReader data = new BinaryDataReader(BaseStream);

            Header = data.ReadStruct<ARC0Support.Header>();
            Console.WriteLine($"[ARC0] Magic: 0x{Header.Magic:X8}, DirCount: {Header.DirectoryEntriesCount}, FileCount: {Header.FileEntriesCount}");
            Console.WriteLine($"[ARC0] DirEntriesOffset: 0x{Header.DirectoryEntriesOffset:X}, DirHashOffset: 0x{Header.DirectoryHashOffset:X}");
            Console.WriteLine($"[ARC0] FileEntriesOffset: 0x{Header.FileEntriesOffset:X}, NameOffset: 0x{Header.NameOffset:X}, DataOffset: 0x{Header.DataOffset:X}");

            if (Header.Magic != 0x30435241) throw new InvalidDataException("Invalid ARC0 Magic");

            // 1. Directory Entries
            data.Seek((uint)Header.DirectoryEntriesOffset);
            int dirSectionSize = Header.DirectoryHashOffset - Header.DirectoryEntriesOffset;
            Console.WriteLine($"[ARC0] Dir section size: {dirSectionSize} bytes");
            var dirEntries = DecompressBlockTo<ARC0Support.DirectoryEntry>(
                data.GetSection(dirSectionSize),
                Header.DirectoryEntriesCount
            );
            Console.WriteLine($"[ARC0] Decompressed {dirEntries.Length} directory entries");

            // 2. File Entries
            data.Seek((uint)Header.FileEntriesOffset);
            int fileSectionSize = Header.NameOffset - Header.FileEntriesOffset;
            Console.WriteLine($"[ARC0] File section size: {fileSectionSize} bytes");
            var fileEntries = DecompressBlockTo<ARC0Support.FileEntry>(
                data.GetSection(fileSectionSize),
                Header.FileEntriesCount
            );
            Console.WriteLine($"[ARC0] Decompressed {fileEntries.Length} file entries");

            // 3. Name Table
            data.Seek((uint)Header.NameOffset);
            int nameTableSize = Header.DataOffset - Header.NameOffset;
            Console.WriteLine($"[ARC0] Name table compressed size: {nameTableSize} bytes");
            var nameTable = Compressor.Decompress(data.GetSection(nameTableSize));
            Console.WriteLine($"[ARC0] Name table decompressed size: {nameTable.Length} bytes");
            BinaryDataReader names = new BinaryDataReader(nameTable);

            // 4. 구조 매핑
            Encoding encoding = Encoding.UTF8;

            foreach (var dir in dirEntries)
            {
                names.Seek((uint)dir.DirectoryNameStartOffset);
                string dirName = NormalizePath(names.ReadString(encoding));

                VirtualDirectory folder = string.IsNullOrEmpty(dirName)
                    ? root
                    : root.GetFolderFromFullPathSafe(dirName);

                var files = fileEntries
                    .Skip(dir.FirstFileIndex)
                    .Take(dir.FileCount);

                foreach (var file in files)
                {
                    names.Seek((uint)(dir.FileNameStartOffset + file.NameOffsetInFolder));
                    string fileName = names.ReadString(encoding);

                    folder.AddFile(
                        fileName,
                        new SubMemoryStream(
                            BaseStream,
                            Header.DataOffset + file.FileOffset,
                            file.FileSize
                        )
                    );
                }
            }

            // 디버그: 최종 구조 출력
            int totalFiles = CountFilesRecursive(root);
            int totalFolders = CountFoldersRecursive(root);
            Console.WriteLine($"[ARC0] Tree built: {totalFolders} folders, {totalFiles} files");
            Console.WriteLine("=== ARC0.Open() 완료 ===");
            return root;
        }

        // ============================================================
        // SAVE
        // ============================================================

        public void Save(string fileName)
        {
            Save(fileName, null);
        }

        public void Save(string fileName, Action<int, int, string>? progressCallback = null)
        {
            Console.WriteLine("\n============== ARC0.Save() 시작 ==============");
            Encoding sjis = Encoding.GetEncoding("Shift-JIS");

            using (FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            using (BinaryDataWriter writer = new BinaryDataWriter(stream))
            {
                if (Directory == null) return;

                var folders = GetAllFoldersAsDictionary(Directory)
                    .OrderBy(f => Crc32.Compute(sjis.GetBytes(NormalizePath(f.Key) + "/")))
                    .ToList();

                var dirEntries = new List<ARC0Support.DirectoryEntry>();
                var fileEntries = new List<ARC0Support.FileEntry>();
                var fileMap = new Dictionary<ARC0Support.FileEntry, SubMemoryStream>();
                var nameTable = new List<byte>();

                nameTable.Add(0);

                int fileIndex = 0;
                uint dataOffsetCursor = 0;

                var rootDir = this.Directory;
                folders.RemoveAll(k => string.IsNullOrEmpty(k.Key) || k.Value == rootDir);

                int rootFileCount = 0;
                int rootFileNameStartOffset = nameTable.Count;

                if (rootDir.Files.Count > 0)
                {
                    var sortedRootFiles = rootDir.Files.OrderBy(f => Crc32.Compute(sjis.GetBytes(f.Key)));
                    rootFileCount = sortedRootFiles.Count();

                    foreach (var file in sortedRootFiles)
                    {
                        int fileNameOffset = nameTable.Count - rootFileNameStartOffset;
                        nameTable.AddRange(sjis.GetBytes(file.Key + '\0'));

                        var entry = new ARC0Support.FileEntry
                        {
                            Crc32 = Crc32.Compute(sjis.GetBytes(file.Key)),
                            NameOffsetInFolder = (uint)fileNameOffset,
                            FileOffset = dataOffsetCursor,
                            FileSize = (uint)(file.Value.ByteContent?.Length ?? file.Value.Size)
                        };

                        fileEntries.Add(entry);
                        fileMap.Add(entry, file.Value);
                        dataOffsetCursor = (uint)((dataOffsetCursor + entry.FileSize + 3) & ~3);
                        fileIndex++;
                    }
                }

                foreach (var kv in folders)
                {
                    string dirPath = NormalizePath(kv.Key);
                    string saveDirPath = dirPath + (dirPath.EndsWith("/") ? "" : "/");
                    var dir = kv.Value;

                    int dirNameOffset = nameTable.Count;
                    nameTable.AddRange(sjis.GetBytes(saveDirPath + '\0'));

                    int currentFileNameStartOffset = nameTable.Count;

                    var dirEntry = new ARC0Support.DirectoryEntry
                    {
                        Crc32 = Crc32.Compute(sjis.GetBytes(saveDirPath)),
                        FirstFileIndex = (ushort)fileIndex,
                        FileCount = (short)dir.Files.Count,
                        DirectoryCount = (short)dir.Folders.Count,
                        DirectoryNameStartOffset = dirNameOffset,
                        FileNameStartOffset = currentFileNameStartOffset
                    };

                    var sortedFiles = dir.Files.OrderBy(f => Crc32.Compute(sjis.GetBytes(f.Key)));

                    foreach (var file in sortedFiles)
                    {
                        int fileNameOffset = nameTable.Count - currentFileNameStartOffset;
                        nameTable.AddRange(sjis.GetBytes(file.Key + '\0'));

                        var entry = new ARC0Support.FileEntry
                        {
                            Crc32 = Crc32.Compute(sjis.GetBytes(file.Key)),
                            NameOffsetInFolder = (uint)fileNameOffset,
                            FileOffset = dataOffsetCursor,
                            FileSize = (uint)(file.Value.ByteContent?.Length ?? file.Value.Size)
                        };

                        fileEntries.Add(entry);
                        fileMap.Add(entry, file.Value);
                        dataOffsetCursor = (uint)((dataOffsetCursor + entry.FileSize + 3) & ~3);
                        fileIndex++;
                    }

                    dirEntries.Add(dirEntry);
                }

                var rootDirEntry = new ARC0Support.DirectoryEntry
                {
                    Crc32 = 0xFFFFFFFF,
                    FirstFileIndex = 0,
                    FileCount = (short)rootFileCount,
                    DirectoryCount = 0,
                    DirectoryNameStartOffset = 0,
                    FileNameStartOffset = rootFileNameStartOffset
                };
                dirEntries.Add(rootDirEntry);

                int directoryIndex = 0;
                dirEntries = dirEntries.OrderBy(x => x.Crc32).Select(x =>
                {
                    x.FirstDirectoryIndex = (ushort)directoryIndex;
                    directoryIndex += x.DirectoryCount;
                    return x;
                }).ToList();

                writer.Seek(0x48);

                long dirOffset = writer.Position;
                writer.Write(CompressBlockTo(dirEntries.ToArray(), new LZ10()));
                writer.WriteAlignment(4);

                long dirHashOffset = writer.Position;
                writer.Write(CompressBlockTo(dirEntries.Select(d => d.Crc32).ToArray(), new LZ10()));
                writer.WriteAlignment(4);

                long fileOffset = writer.Position;
                writer.Write(CompressBlockTo(fileEntries.ToArray(), new LZ10()));
                writer.WriteAlignment(4);

                long nameOffset = writer.Position;
                writer.Write(CompressBlockTo(nameTable.ToArray(), new LZ10()));
                writer.WriteAlignment(4);

                writer.WriteAlignment(4);

                long dataOffset = writer.Position;

                for (int i = 0; i < fileEntries.Count; i++)
                {
                    var f = fileEntries[i];
                    var sms = fileMap[f];

                    writer.BaseStream.Position = dataOffset + f.FileOffset;
                    sms.CopyTo(stream);

                    if (progressCallback != null) progressCallback(i + 1, fileEntries.Count, "Saving...");
                }

                Header.Magic = 0x30435241;
                Header.DirectoryEntriesOffset = (int)dirOffset;
                Header.DirectoryHashOffset = (int)dirHashOffset;
                Header.FileEntriesOffset = (int)fileOffset;
                Header.NameOffset = (int)nameOffset;
                Header.DataOffset = (int)dataOffset;
                Header.DirectoryEntriesCount = (short)dirEntries.Count;
                Header.DirectoryHashCount = (short)dirEntries.Count;
                Header.FileEntriesCount = fileEntries.Count;
                Header.DirectoryCount = dirEntries.Count;
                Header.FileCount = fileEntries.Count;
                Header.TableChunkSize = (int)(dirEntries.Count * 20 + dirEntries.Count * 4 +
                                              fileEntries.Count * 16 + nameTable.Count + 0x20 + 3) & ~3;

                writer.Seek(0);
                writer.WriteStruct(Header);

                Console.WriteLine("=== ARC0.Save() 완료 ===");
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        // ✅ [중요] 제네릭 제약 조건 (where T : struct) 추가!
        private T[] DecompressBlockTo<T>(byte[] data, int count) where T : struct
        {
            BinaryDataReader reader = new BinaryDataReader(Compressor.Decompress(data));
            return reader.ReadMultipleStruct<T>(count);
        }

        // ✅ [중요] 제네릭 제약 조건 (where T : struct) 추가!
        private byte[] CompressBlockTo<T>(T[] data, ICompression compression) where T : struct
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryDataWriter w = new BinaryDataWriter(ms);
                w.WriteMultipleStruct(data);
                return compression.Compress(ms.ToArray());
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return path.Replace('\\', '/').Replace("//", "/").Trim('/');
        }

        private Dictionary<string, VirtualDirectory> GetAllFoldersAsDictionary(VirtualDirectory root, string currentPath = "")
        {
            var result = new Dictionary<string, VirtualDirectory>();

            if (!string.IsNullOrEmpty(currentPath))
            {
                result[currentPath] = root;
            }

            foreach (var subDir in root.Folders)
            {
                string nextPath = string.IsNullOrEmpty(currentPath) ? subDir.Key : currentPath + "/" + subDir.Key;
                var subResults = GetAllFoldersAsDictionary(subDir.Value, nextPath);

                foreach (var kv in subResults)
                {
                    result[kv.Key] = kv.Value;
                }
            }
            return result;
        }

        private int CountFilesRecursive(VirtualDirectory dir)
        {
            int count = dir.Files.Count;
            foreach (var sub in dir.Folders.Values)
                count += CountFilesRecursive(sub);
            return count;
        }

        private int CountFoldersRecursive(VirtualDirectory dir)
        {
            int count = dir.Folders.Count;
            foreach (var sub in dir.Folders.Values)
                count += CountFoldersRecursive(sub);
            return count;
        }

        public void Close()
        {
            BaseStream?.Dispose();
            BaseStream = null;
            Directory = new VirtualDirectory("/");
        }

        public void Dispose()
        {
            Close();
        }
    }
}