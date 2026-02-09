using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ICN_T2.Tools.IO;
using ICN_T2.Tools;

namespace ICN_T2.Logic.Level5.Binary
{
    /// <summary>
    /// Level-5 CfgBin 파일 포맷 파서/라이터
    /// .NET 8 리팩토링 버전 - 원본의 모든 핵심 로직 포함
    /// </summary>
    public class CfgBin
    {
        #region Properties

        public Encoding Encoding { get; set; } = Encoding.UTF8;
        public List<Entry> Entries { get; set; } = new List<Entry>();
        public Dictionary<int, string> Strings { get; set; } = new Dictionary<int, string>();
        public bool IsModified { get; set; } = false;

        // 원본 데이터 보존 (수정 안됐을 때 인코딩 손실 방지)
        private byte[]? _originalData;

        #endregion

        #region Constructor

        public CfgBin() { }

        #endregion

        #region Open Methods

        public void Open(byte[] data)
        {
            // 원본 데이터 보존
            _originalData = data;
            IsModified = false;

            // [FIX] Simple Encoding Detection (EncodingDetector missing)
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                Encoding = Encoding.UTF8;
            }
            else
            {
                // Default to Shift-JIS for Level-5 (or user preference)
                Encoding = Encoding.GetEncoding("shift_jis");
            }

            using var reader = new BinaryDataReader(data);
            ParseInternal(reader);
        }

        public void Open(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            Open(ms.ToArray());
        }

        private void ParseInternal(BinaryDataReader reader)
        {
            reader.Seek(0x0);
            var header = reader.ReadStruct<CfgBinSupport.Header>();

            // Entries 버퍼 (0x10 ~ StringTableOffset)
            byte[] entriesBuffer = reader.GetSection(0x10, header.StringTableOffset);

            // String Table 버퍼
            byte[] stringTableBuffer = reader.GetSection(
                (uint)header.StringTableOffset,
                header.StringTableLength);

            // 인코딩 자동 감지
            Encoding = EncodingDetector.Detect(stringTableBuffer);

            // String Table 파싱
            Strings = ParseStrings(header.StringTableCount, stringTableBuffer);

            // Key Table 파싱
            long keyTableOffset = RoundUp(header.StringTableOffset + header.StringTableLength, 16);
            reader.Seek((uint)keyTableOffset);
            int keyTableSize = reader.ReadValue<int>();
            byte[] keyTableBlob = reader.GetSection((uint)keyTableOffset, keyTableSize);
            var keyTable = ParseKeyTable(keyTableBlob);

            // Entries 파싱 및 계층 구조 구성
            Entries = ParseEntries(header.EntriesCount, entriesBuffer, keyTable);
        }

        #endregion

        #region Save Methods

        public void Save(string fileName)
        {
            using var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryDataWriter(stream);

            CfgBinSupport.Header header = new()
            {
                EntriesCount = CountEntries(Entries),
                StringTableOffset = 0,
                StringTableLength = 0,
                StringTableCount = Strings.Count
            };

            writer.Seek(0x10);

            foreach (var entry in Entries)
            {
                writer.Write(entry.EncodeEntry());
            }

            writer.WriteAlignment(0x10, 0xFF);
            header.StringTableOffset = (int)writer.Position;

            if (Strings.Count > 0)
            {
                writer.Write(EncodeStrings(Strings));
                header.StringTableLength = (int)writer.Position - header.StringTableOffset;
                writer.WriteAlignment(0x10, 0xFF);
            }

            var uniqueKeys = Entries
                .SelectMany(e => e.GetUniqueKeys())
                .Distinct()
                .ToList();
            writer.Write(EncodeKeyTable(uniqueKeys));

            // t2b footer 작성 (게임 엔진 필수!)
            writer.Write(new byte[] { 0x01, 0x74, 0x32, 0x62, 0xFE });
            writer.Write(new byte[] { 0x01, GetEncodingByte(), 0x00, 0x01 });
            writer.WriteAlignment();

            writer.Seek(0);
            writer.WriteStruct(header);
        }

        public byte[] Save()
        {
            // 수정 안됐으면 원본 그대로 반환 (인코딩 손실 방지)
            if (!IsModified && _originalData != null)
            {
                return _originalData;
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryDataWriter(stream);

            CfgBinSupport.Header header = new()
            {
                EntriesCount = CountEntries(Entries),
                StringTableOffset = 0,
                StringTableLength = 0,
                StringTableCount = Strings.Count
            };

            writer.Seek(0x10);

            foreach (var entry in Entries)
            {
                writer.Write(entry.EncodeEntry());
            }

            writer.WriteAlignment(0x10, 0xFF);
            header.StringTableOffset = (int)writer.Position;

            if (Strings.Count > 0)
            {
                writer.Write(EncodeStrings(Strings));
                header.StringTableLength = (int)writer.Position - header.StringTableOffset;
                writer.WriteAlignment(0x10, 0xFF);
            }

            var uniqueKeys = Entries
                .SelectMany(e => e.GetUniqueKeys())
                .Distinct()
                .ToList();
            writer.Write(EncodeKeyTable(uniqueKeys));

            // t2b footer 작성 (게임 엔진 필수!)
            writer.Write(new byte[] { 0x01, 0x74, 0x32, 0x62, 0xFE });
            writer.Write(new byte[] { 0x01, GetEncodingByte(), 0x00, 0x01 });
            writer.WriteAlignment();

            writer.Seek(0);
            writer.WriteStruct(header);

            return stream.ToArray();
        }

        #endregion

        #region ReplaceEntry Methods

        public void ReplaceEntry(string entryName, Entry newEntry)
        {
            IsModified = true;

            int entryIndex = Entries.FindIndex(x => x.GetName() == entryName);
            if (entryIndex >= 0)
            {
                Entries[entryIndex] = newEntry;
            }
            else
            {
                Entries.Add(newEntry);
            }
        }

        public void ReplaceEntry<T>(string entryBeginName, string entryName, T[] values) where T : class
        {
            IsModified = true;

            var baseBegin = Entries.FirstOrDefault(x => x.GetName() == entryBeginName);

            if (baseBegin == null)
            {
                // 엔트리 없으면 새로 생성 (원본 동작 유지)
                baseBegin = new Entry(
                    entryBeginName,
                    new List<Variable>(),
                    Encoding ?? Encoding.UTF8);
                baseBegin.Variables.Add(new Variable(EntryType.Int, 0));
                Entries.Add(baseBegin);
            }

            // 개수 업데이트 (Variables[0]가 개수)
            if (baseBegin.Variables.Count > 0)
            {
                baseBegin.Variables[0].Value = values.Length;
            }

            // 기존 Children 초기화 후 새로 추가
            baseBegin.Children.Clear();

            for (int i = 0; i < values.Length; i++)
            {
                var newEntry = new Entry(
                    entryName + i,
                    new List<Variable>(),
                    Encoding ?? Encoding.UTF8);
                newEntry.SetVariablesFromClass(values[i]);
                baseBegin.Children.Add(newEntry);
            }
        }

        /// <summary>
        /// 수정 플래그를 수동으로 설정 (외부에서 직접 Entries 수정 시 호출)
        /// </summary>
        public void MarkAsModified() => IsModified = true;

        #endregion

        #region Parsing Methods

        private Dictionary<int, string> ParseStrings(int stringCount, byte[] stringTableBuffer)
        {
            var result = new Dictionary<int, string>();

            using var reader = new BinaryDataReader(stringTableBuffer);
            for (int i = 0; i < stringCount; i++)
            {
                result.Add((int)reader.Position, reader.ReadString(Encoding));
            }

            return result;
        }

        private Dictionary<uint, string> ParseKeyTable(byte[] buffer)
        {
            var keyTable = new Dictionary<uint, string>();

            using var reader = new BinaryDataReader(buffer);
            var header = reader.ReadStruct<CfgBinSupport.KeyHeader>();
            byte[] keyStringBlob = reader.GetSection(
                (uint)header.KeyStringOffset,
                header.keyStringLength);

            for (int i = 0; i < header.KeyCount; i++)
            {
                uint crc32 = reader.ReadValue<uint>();
                int stringStart = reader.ReadValue<int>();
                int stringEnd = Array.IndexOf(keyStringBlob, (byte)0, stringStart);

                if (stringEnd < 0) stringEnd = keyStringBlob.Length;

                int length = stringEnd - stringStart;
                byte[] stringBuf = new byte[length];
                Array.Copy(keyStringBlob, stringStart, stringBuf, 0, length);

                string key = Encoding.GetString(stringBuf);
                keyTable[crc32] = key;
            }

            return keyTable;
        }

        private List<Entry> ParseEntries(int entriesCount, byte[] entriesBuffer, Dictionary<uint, string> keyTable)
        {
            var temp = new List<Entry>();

            using var reader = new BinaryDataReader(entriesBuffer);

            for (int i = 0; i < entriesCount; i++)
            {
                uint crc32 = reader.ReadValue<uint>();

                if (!keyTable.TryGetValue(crc32, out string? name))
                {
                    name = $"UNKNOWN_{crc32:X8}";
                }

                int paramCount = reader.ReadValue<byte>();
                var paramTypes = new EntryType[paramCount];
                int paramIndex = 0;

                // 파라미터 타입 읽기 (4개씩 1바이트에 패킹)
                // 원본 로직: String=0, Int=1, Float=2
                int typeByteCount = (int)Math.Ceiling(paramCount / 4.0);
                for (int j = 0; j < typeByteCount; j++)
                {
                    byte paramTypeByte = reader.ReadValue<byte>();
                    for (int k = 0; k < 4 && paramIndex < paramCount; k++)
                    {
                        int tag = (paramTypeByte >> (2 * k)) & 3;
                        paramTypes[paramIndex++] = tag switch
                        {
                            0 => EntryType.String,
                            1 => EntryType.Int,
                            2 => EntryType.Float,
                            _ => EntryType.Unknown
                        };
                    }
                }

                // 4바이트 정렬
                if ((Math.Ceiling(paramCount / 4.0) + 1) % 4 != 0)
                {
                    reader.Seek((uint)(reader.Position + 4 - (reader.Position % 4)));
                }

                // 변수 값 읽기
                var variables = new List<Variable>();
                for (int j = 0; j < paramCount; j++)
                {
                    switch (paramTypes[j])
                    {
                        case EntryType.String:
                            int offset = reader.ReadValue<int>();
                            string? text = offset != -1 && Strings.TryGetValue(offset, out var s) ? s : null;
                            variables.Add(new Variable(EntryType.String, new OffsetTextPair(offset, text)));
                            break;

                        case EntryType.Int:
                            variables.Add(new Variable(EntryType.Int, reader.ReadValue<int>()));
                            break;

                        case EntryType.Float:
                            variables.Add(new Variable(EntryType.Float, reader.ReadValue<float>()));
                            break;

                        case EntryType.Unknown:
                        default:
                            variables.Add(new Variable(EntryType.Unknown, reader.ReadValue<int>()));
                            break;
                    }
                }

                temp.Add(new Entry(name, variables, Encoding));
            }

            // Entry 이름 고유화 (같은 이름 여러 개일 때 _0, _1, ... 붙임)
            var nameCounter = new Dictionary<string, int>();
            foreach (var entry in temp)
            {
                string baseName = entry.Name;
                if (!nameCounter.ContainsKey(baseName))
                {
                    nameCounter[baseName] = 0;
                }
                entry.Name = $"{baseName}_{nameCounter[baseName]++}";
            }

            return ProcessEntries(temp);
        }

        /// <summary>
        /// 플랫 Entry 리스트를 계층 구조로 변환
        /// BEG/BEGIN으로 시작하고 END로 끝나는 구조를 트리로 만듦
        /// </summary>
        private List<Entry> ProcessEntries(List<Entry> entries)
        {
            var stack = new List<Entry>();
            var output = new List<Entry>();
            var depth = new Dictionary<string, int>();

            int i = 0;
            while (i < entries.Count)
            {
                string name = entries[i].Name;
                var variables = entries[i].Variables;

                string[] nameParts = name.Split('_');
                // 안전한 인덱싱
                string nodeType = nameParts.Length >= 2 ? nameParts[^2].ToLower() : "";
                string nodeName = nameParts.Length >= 1
                    ? string.Join("_", nameParts.Take(nameParts.Length - 1)).ToLower()
                    : name.ToLower();

                if (nodeType.EndsWith("beg") || nodeType.EndsWith("begin") || nodeType.EndsWith("ptree"))
                {
                    // 새 부모 노드 시작
                    var newNode = new Entry(name, variables, Encoding);

                    if (stack.Count > 0)
                    {
                        stack[^1].Children.Add(newNode);
                    }
                    else
                    {
                        output.Add(newNode);
                    }

                    stack.Add(newNode);
                    depth[name] = stack.Count;
                }
                else if (nodeType.EndsWith("end") || nodeType.EndsWith("_ptree"))
                {
                    // 부모 노드 종료
                    if (stack.Count > 0)
                    {
                        stack[^1].EndTerminator = true;
                    }

                    // 매칭되는 BEGIN 찾기
                    string key = FindMatchingBeginKey(name, depth);

                    if (!string.IsNullOrEmpty(key) && depth.Count > 1)
                    {
                        string[] keys = depth.Keys.ToArray();
                        int keyIndex = Array.IndexOf(keys, key);

                        if (keyIndex > 0)
                        {
                            int currentDepth = depth[key];
                            int previousDepth = depth[keys[keyIndex - 1]];
                            int popCount = currentDepth - previousDepth;

                            for (int j = 0; j < popCount && stack.Count > 0; j++)
                            {
                                stack.RemoveAt(stack.Count - 1);
                            }
                        }
                        depth.Remove(key);
                    }
                    else
                    {
                        if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                        if (!string.IsNullOrEmpty(key)) depth.Remove(key);
                    }
                }
                else if (IsMetadataEntry(nodeName))
                {
                    // 메타데이터 엔트리 (last_update_date_time 등)
                    var newNode = new Entry(name, variables, Encoding) { EndTerminator = true };
                    output.Add(newNode);
                }
                else
                {
                    // 일반 자식 엔트리
                    var newItem = new Entry(name, variables, Encoding);

                    // depth가 비어있을 경우 예외 방지 (원본 [FIX])
                    string entryNameWithMaxDepth = string.Empty;
                    if (depth.Count > 0)
                    {
                        entryNameWithMaxDepth = depth.MaxBy(x => x.Value).Key;
                    }

                    if (entryNameWithMaxDepth.Contains("_LIST_BEG_"))
                    {
                        entryNameWithMaxDepth = entryNameWithMaxDepth.Replace("_LIST_BEG_", "_BEG_");
                    }

                    string[] maxDepthParts = entryNameWithMaxDepth.Split('_');
                    string entryBaseName = maxDepthParts.Length >= 2
                        ? string.Join("_", maxDepthParts.Take(maxDepthParts.Length - 2))
                        : string.Empty;

                    if (!name.StartsWith(entryBaseName))
                    {
                        if (!IsBeginOrPtreeEntry(entryNameWithMaxDepth))
                        {
                            // 스택에서 팝 (원본 [FIX] Stack safety)
                            if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                            if (depth.ContainsKey(entryNameWithMaxDepth)) depth.Remove(entryNameWithMaxDepth);

                            if (stack.Count > 0)
                                stack[^1].Children.Add(newItem);
                            else
                                output.Add(newItem);
                        }
                        else
                        {
                            // 마지막 자식에 추가 (원본 [FIX] Stack safety)
                            if (stack.Count > 0 && stack[^1].Children.Count > 0)
                            {
                                stack[^1].Children[^1].Children.Add(newItem);
                            }
                            else
                            {
                                // Fallback: add to root output if parent structure unclear
                                output.Add(newItem);
                            }

                            stack.Add(newItem);
                            depth[name] = stack.Count;
                        }
                    }
                    else
                    {
                        // 원본 [FIX] Stack가 비어있을 경우 최상위 output에 추가
                        if (stack.Count > 0)
                        {
                            stack[^1].Children.Add(newItem);
                        }
                        else
                        {
                            output.Add(newItem);
                        }
                    }
                }

                i++;
            }

            return output;
        }

        private static string FindMatchingBeginKey(string endName, Dictionary<string, int> depth)
        {
            // _END_ -> _BEG_ 또는 _BEGIN_
            if (depth.ContainsKey(endName.Replace("_END_", "_BEG_")))
            {
                return endName.Replace("_END_", "_BEG_");
            }
            if (depth.ContainsKey(endName.Replace("_END_", "_BEGIN_")))
            {
                return endName.Replace("_END_", "_BEGIN_");
            }
            if (depth.ContainsKey(endName.Replace("_PTREE", "PTREE")))
            {
                return endName.Replace("_PTREE", "PTREE");
            }

            return string.Empty;
        }

        private static bool IsMetadataEntry(string nodeName)
        {
            return nodeName is "last_update_date_time"
                or "last_update_user"
                or "last_update_machine";
        }

        private static bool IsBeginOrPtreeEntry(string name)
        {
            return name.Contains("BEGIN") || name.Contains("BEG") || name.Contains("PTREE");
        }

        #endregion

        #region Encoding Methods

        private byte[] EncodeStrings(Dictionary<int, string> strings)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryDataWriter(ms);

            foreach (var kvp in strings)
            {
                writer.Write(Encoding.GetBytes(kvp.Value));
                writer.Write((byte)0x00);
            }

            return ms.ToArray();
        }

        public byte[] EncodeKeyTable(List<string> keyList)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryDataWriter(stream);

            // Key strings 크기 계산
            uint keyStringsSize = 0;
            foreach (var key in keyList)
            {
                keyStringsSize += (uint)Encoding.GetByteCount(key) + 1;
            }

            var header = new CfgBinSupport.KeyHeader
            {
                KeyCount = keyList.Count,
                keyStringLength = (int)keyStringsSize
            };

            // 0x10부터 Key Entry 작성
            writer.Seek(0x10);

            int stringOffset = 0;
            foreach (var key in keyList)
            {
                uint crc32 = Crc32.Compute(Encoding.GetBytes(key));
                writer.Write(crc32);
                writer.Write(stringOffset);
                stringOffset += Encoding.GetByteCount(key) + 1;
            }

            writer.WriteAlignment(0x10, 0xFF);

            // Key strings 오프셋 기록
            header.KeyStringOffset = (int)writer.Position;

            // Key strings 작성
            foreach (var key in keyList)
            {
                byte[] stringBytes = Encoding.GetBytes(key);
                writer.Write(stringBytes);
                writer.Write((byte)0); // Null-terminator
            }

            writer.WriteAlignment(0x10, 0xFF);
            header.KeyLength = (int)writer.Position;

            // Header 작성 (맨 앞)
            writer.Seek(0x00);
            writer.WriteStruct(header);

            return stream.ToArray();
        }

        private byte GetEncodingByte()
        {
            // SHIFT-JIS면 0, 아니면 1 (UTF-8)
            if (Encoding != null && Encoding.CodePage == 932) // SHIFT-JIS code page
            {
                return 0;
            }
            return 1;
        }

        #endregion

        #region Utility Methods

        private static long RoundUp(int n, int alignment)
        {
            return ((n + alignment - 1) / alignment) * alignment;
        }

        public int CountEntries(List<Entry> entries)
        {
            int total = 0;
            foreach (var entry in entries)
            {
                total += entry.Count();
            }
            return total;
        }

        #endregion
    }

    #region Support Structures

    /// <summary>
    /// CfgBin 파일 포맷의 헤더 및 지원 구조체들
    /// </summary>
    public static class CfgBinSupport
    {
        /// <summary>
        /// CfgBin 메인 헤더 (0x10 바이트)
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header
        {
            public int EntriesCount;        // 0x00: 총 Entry 개수
            public int StringTableOffset;   // 0x04: String Table 시작 오프셋
            public int StringTableLength;   // 0x08: String Table 길이
            public int StringTableCount;    // 0x0C: String 개수
        }

        /// <summary>
        /// Key Table 헤더 (0x10 바이트)
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct KeyHeader
        {
            public int KeyLength;           // 0x00: 전체 Key Table 길이
            public int KeyCount;            // 0x04: Key 개수
            public int KeyStringOffset;     // 0x08: Key 문자열 시작 오프셋
            public int keyStringLength;     // 0x0C: Key 문자열 총 길이
        }
    }

    /// <summary>
    /// String 타입 변수용 오프셋-텍스트 쌍
    /// </summary>
    public class OffsetTextPair
    {
        public int Offset { get; set; }
        public string? Text { get; set; }

        public OffsetTextPair(int offset, string? text)
        {
            Offset = offset;
            Text = text;
        }
    }

    #endregion
}