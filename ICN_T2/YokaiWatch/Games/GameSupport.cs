using System;
using System.Linq;
using System.Collections.Generic;
using ICN_T2.Logic.Level5.Text; // T2bþ
using ICN_T2.Logic.VirtualFileSystem; // VirtualDirectory

namespace ICN_T2.YokaiWatch.Games
{
    public static class GameSupport
    {
        public static Dictionary<int, char> PrefixLetter = new Dictionary<int, char>()
        {
            {0, 'c'}, {1, '?'}, {2, '?'}, {3, '?'}, {4, '?'},
            {5, 'x'}, {6, 'y'}, {7, 'z'}, {8, '?'}, {12, '?'}, {17, '?'},
        };

        private static string FormatVariant(int x)
        {
            if (x > -1 && x < 10) return $"0{x}0";
            else if (x > 9 && x < 100) return $"{x}0";
            else if (x > 99 && x < 1000)
            {
                char[] charArray = x.ToString().ToCharArray();
                Array.Reverse(charArray);
                return new string(charArray);
            }
            throw new FormatException("Format non valide");
        }

        private static int ParseVariant(string str)
        {
            if (str.Length == 4 && str[0] == '0' && str[3] == '0') return int.Parse(str[1].ToString());
            else if (str.Length == 3 && str[2] == '0') return int.Parse(str.Substring(0, 2));
            else if (str.Length == 3)
            {
                char[] charArray = str.ToCharArray();
                Array.Reverse(charArray);
                return int.Parse(new string(charArray));
            }
            throw new FormatException("Format non valide");
        }

        public static string GetFileModelText(int prefix, int number, int variant)
        {
            return PrefixLetter[prefix] + number.ToString("D3") + FormatVariant(variant);
        }

        public static (int, int, int) GetFileModelValue(string text)
        {
            int prefixIndex = PrefixLetter.FirstOrDefault(x => x.Value == text[0]).Key;
            int number = int.Parse(text.Substring(1, 3));
            int variant = ParseVariant(text.Substring(4, 3));

            return (prefixIndex, number, variant);
        }

        public static void SaveTextFile(GameFile fileName, T2bþ fileData)
        {
            if (fileName == null || fileData == null) return;
            if (fileName.File == null || fileName.File.Directory == null) return;

            try
            {
                // 경로 정규화
                string normalizedPath = (fileName.Path ?? "").Replace("\\", "/").Trim('/');

                // 폴더와 파일명 분리
                string folderPath = "";
                string targetFileName = normalizedPath;
                int lastSlash = normalizedPath.LastIndexOf('/');

                if (lastSlash >= 0)
                {
                    folderPath = normalizedPath.Substring(0, lastSlash);
                    targetFileName = normalizedPath.Substring(lastSlash + 1);
                }

                // 폴더 찾기
                VirtualDirectory dir = string.IsNullOrEmpty(folderPath)
                    ? fileName.File.Directory
                    : fileName.File.Directory.GetFolderFromFullPath(folderPath);

                if (dir != null && dir.Files.ContainsKey(targetFileName))
                {
                    // 저장 및 VirtualFileSystem 업데이트
                    byte[] savedData = fileData.Save(false);
                    dir.Files[targetFileName].ByteContent = savedData;

                    // 디버그 로그 (필요시 활성화)
                    // Console.WriteLine($"[GameSupport] Saved {targetFileName} ({savedData.Length} bytes)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameSupport] SaveTextFile Error: {ex.Message}");
            }
        }

        public static T GetLogic<T>() where T : class, new()
        {
            return new T();
        }
    }
}