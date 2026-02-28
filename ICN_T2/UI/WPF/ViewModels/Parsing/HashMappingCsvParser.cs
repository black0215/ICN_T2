using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ICN_T2.UI.WPF.ViewModels.Parsing
{
    internal static class HashMappingCsvParser
    {
        public static Dictionary<int, string> ParseFile(string path)
        {
            string[] lines = ReadAllLinesWithEncodingFallback(path);
            return ParseLines(lines);
        }

        public static Dictionary<int, string> ParseStream(Stream stream)
        {
            if (stream == null)
            {
                return new Dictionary<int, string>();
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            byte[] raw = ms.ToArray();

            string text;
            try
            {
                text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(raw);
            }
            catch (DecoderFallbackException)
            {
                text = Encoding.Default.GetString(raw);
            }

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            return ParseLines(lines);
        }

        public static Dictionary<int, string> ParseLines(IEnumerable<string> lines)
        {
            var result = new Dictionary<int, string>();
            bool headerSkipped = false;

            foreach (string raw in lines)
            {
                string line = raw?.Trim() ?? "";
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!headerSkipped)
                {
                    headerSkipped = true;
                    if (line.StartsWith("Hash,", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                var cols = ParseCsvLine(line);
                if (cols.Count < 2)
                {
                    continue;
                }

                if (!TryParseHash(cols[0], out int hash))
                {
                    continue;
                }

                string displayName = cols[1].Trim();
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                result[hash] = displayName;
            }

            return result;
        }

        private static string[] ReadAllLinesWithEncodingFallback(string path)
        {
            try
            {
                return File.ReadAllLines(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
            }
            catch (DecoderFallbackException)
            {
                return File.ReadAllLines(path, Encoding.Default);
            }
        }

        private static bool TryParseHash(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string hex = trimmed.Substring(2);
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hexValue))
                {
                    value = unchecked((int)hexValue);
                    return true;
                }

                return false;
            }

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int signedValue))
            {
                value = signedValue;
                return true;
            }

            if (uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint unsignedValue))
            {
                value = unchecked((int)unsignedValue);
                return true;
            }

            if (uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint fallbackHexValue))
            {
                value = unchecked((int)fallbackHexValue);
                return true;
            }

            return false;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            if (line == null)
            {
                return values;
            }

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    values.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(c);
            }

            values.Add(sb.ToString());
            return values;
        }
    }
}
