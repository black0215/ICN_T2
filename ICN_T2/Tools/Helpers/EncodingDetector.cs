using System;
using System.Text;

namespace ICN_T2.Tools
{
    public static class EncodingDetector
    {
        public static Encoding Detect(byte[] data)
        {
            // BOM check for UTF-8
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                return Encoding.UTF8;
            }

            // Heuristic: Check if data is valid UTF-8
            // Korean text files (.cfg.bin) often use UTF-8 without BOM
            if (IsLikelyUtf8(data))
            {
                return Encoding.UTF8;
            }

            // Default to Shift-JIS for Level-5 games (Japanese)
            try
            {
                return Encoding.GetEncoding("shift_jis");
            }
            catch
            {
                return Encoding.UTF8; // Fallback if Shift-JIS not available
            }
        }

        /// <summary>
        /// Heuristic check to determine if byte array is likely UTF-8 encoded text.
        /// Checks for valid UTF-8 multi-byte sequences and Korean Hangul characters (U+AC00-U+D7AF).
        /// </summary>
        private static bool IsLikelyUtf8(byte[] data)
        {
            int validUtf8Sequences = 0;
            int totalBytes = Math.Min(data.Length, 1024); // Check first 1KB

            for (int i = 0; i < totalBytes; i++)
            {
                byte b = data[i];

                // Single-byte ASCII (0x00-0x7F)
                if (b <= 0x7F)
                {
                    continue;
                }

                // Multi-byte UTF-8 sequence
                int bytesToRead = 0;

                // 2-byte sequence (110xxxxx 10xxxxxx)
                if ((b & 0xE0) == 0xC0)
                {
                    bytesToRead = 1;
                }
                // 3-byte sequence (1110xxxx 10xxxxxx 10xxxxxx) - Korean Hangul is here
                else if ((b & 0xF0) == 0xE0)
                {
                    bytesToRead = 2;
                }
                // 4-byte sequence (11110xxx 10xxxxxx 10xxxxxx 10xxxxxx)
                else if ((b & 0xF8) == 0xF0)
                {
                    bytesToRead = 3;
                }
                else
                {
                    // Invalid UTF-8 start byte
                    return false;
                }

                // Validate continuation bytes (10xxxxxx)
                for (int j = 1; j <= bytesToRead; j++)
                {
                    if (i + j >= totalBytes)
                    {
                        return false; // Incomplete sequence at end
                    }

                    if ((data[i + j] & 0xC0) != 0x80)
                    {
                        return false; // Invalid continuation byte
                    }
                }

                // Valid UTF-8 sequence detected
                validUtf8Sequences++;

                // Check if this is Korean Hangul (U+AC00-U+D7AF)
                // In UTF-8: EA B0 80 to ED 9E AF
                if (bytesToRead == 2 &&
                    ((b == 0xEA && data[i + 1] >= 0xB0) ||
                     (b == 0xEB) ||
                     (b == 0xEC) ||
                     (b == 0xED && data[i + 1] <= 0x9E)))
                {
                    // Found Korean character - very likely UTF-8
                    return true;
                }

                i += bytesToRead; // Skip continuation bytes
            }

            // If we found valid UTF-8 sequences and no errors, likely UTF-8
            return validUtf8Sequences > 0;
        }
    }
}
