using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using ICN_T2.Logic.Level5.Image;
using ICN_T2.YokaiWatch.Definitions;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;

namespace ICN_T2.UI.WPF.ViewModels
{
    public static class IconCache
    {
        private static readonly Dictionary<int, BitmapImage> _rankIcons = new();
        private static readonly Dictionary<int, BitmapImage> _tribeIcons = new();
        private static readonly Dictionary<string, BitmapImage> _foodIcons = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, BitmapImage> _yokaiIcons = new();
        private static bool _initialized;

        static IconCache()
        {
            Initialize();
        }

        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;

                string rankPath = Path.Combine(basePath, "Resources", "Rank Icon");
                if (Directory.Exists(rankPath))
                {
                    LoadRank(rankPath, 0, "Rank_E.png");
                    LoadRank(rankPath, 1, "Rank_D.png");
                    LoadRank(rankPath, 2, "Rank_C.png");
                    LoadRank(rankPath, 3, "Rank_B.png");
                    LoadRank(rankPath, 4, "Rank_A.png");
                    LoadRank(rankPath, 5, "Rank_S.png");
                }

                string tribePath = Path.Combine(basePath, "Resources", "Tribe Icon");
                if (Directory.Exists(tribePath))
                {
                    for (int i = 0; i <= 11; i++)
                    {
                        LoadTribe(tribePath, i, $"all_icon_kind01_{i:D2}.png");
                    }
                }

                string foodPath = Path.Combine(basePath, "Resources", "Food Icon");
                if (Directory.Exists(foodPath))
                {
                    foreach (string file in Directory.GetFiles(foodPath, "*.png"))
                    {
                        string stem = Path.GetFileNameWithoutExtension(file);
                        LoadFood(file, stem);
                    }
                }
            }
            catch
            {
                // ignored
            }
            finally
            {
                _initialized = true;
            }
        }

        private static void LoadRank(string dir, int key, string file)
        {
            string path = Path.Combine(dir, file);
            if (File.Exists(path)) _rankIcons[key] = LoadBitmap(path);
        }

        private static void LoadTribe(string dir, int key, string file)
        {
            string path = Path.Combine(dir, file);
            if (File.Exists(path)) _tribeIcons[key] = LoadBitmap(path);
        }

        private static void LoadFood(string filePath, string key)
        {
            if (!File.Exists(filePath)) return;

            var bitmap = LoadBitmap(filePath);
            string normalized = NormalizeFoodKey(key);
            string withSpaces = key.Replace("_", " ");

            _foodIcons[key] = bitmap;
            _foodIcons[normalized] = bitmap;
            _foodIcons[withSpaces] = bitmap;
            if (normalized.EndsWith("s", StringComparison.Ordinal))
            {
                _foodIcons[normalized[..^1]] = bitmap;
            }
        }

        public static BitmapImage? GetRankIcon(int rank) => _rankIcons.TryGetValue(rank, out var value) ? value : null;
        public static BitmapImage? GetTribeIcon(int tribe) => _tribeIcons.TryGetValue(tribe, out var value) ? value : null;

        public static BitmapImage? GetFoodIcon(string foodName)
        {
            if (string.IsNullOrWhiteSpace(foodName)) return null;

            if (_foodIcons.TryGetValue(foodName, out var exact))
            {
                return exact;
            }

            string normalized = NormalizeFoodKey(foodName);
            if (_foodIcons.TryGetValue(normalized, out var normalizedMatch))
            {
                return normalizedMatch;
            }

            if (normalized.EndsWith("s", StringComparison.Ordinal) &&
                _foodIcons.TryGetValue(normalized[..^1], out var singularMatch))
            {
                return singularMatch;
            }

            if (_foodIcons.TryGetValue(normalized + "s", out var pluralMatch))
            {
                return pluralMatch;
            }

            return null;
        }

        private static string NormalizeFoodKey(string name)
        {
            var chars = name.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
            return new string(chars);
        }

        public static BitmapImage? GetYokaiIcon(IGame game, CharaBase baseInfo)
        {
            if (game == null || baseInfo == null) return null;

            try
            {
                // 1. Prefix Check
                int prefix = baseInfo.FileNamePrefix;
                if (!GameSupport.PrefixLetter.ContainsKey(prefix) || GameSupport.PrefixLetter[prefix] == '?')
                {
                    return null;
                }

                // 2. Get Model Name
                string modelName = GameSupport.GetFileModelText(prefix, baseInfo.FileNameNumber, baseInfo.FileNameVariant);
                if (string.IsNullOrWhiteSpace(modelName)) return null;

                // 3. Check Cache
                if (_yokaiIcons.TryGetValue(modelName, out var cachedIcon))
                {
                    return cachedIcon;
                }

                // 4. Load Icon from Game Files
                System.Drawing.Image? iconImage = LoadYokaiIconFromGame(game, modelName);

                if (iconImage != null)
                {
                    var bitmap = ToBitmapImage(iconImage);
                    if (bitmap != null)
                    {
                        _yokaiIcons.TryAdd(modelName, bitmap);
                        return bitmap;
                    }
                }

                // Fallback: Try external PNG
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string pngName = $"face_icon.{baseInfo.FileNamePrefix:D2}.{baseInfo.FileNameNumber:D3}.{baseInfo.FileNameVariant:D2}.png";
                string pngPath = Path.Combine(basePath, pngName);

                if (File.Exists(pngPath))
                {
                    var bitmap = LoadBitmap(pngPath);
                    if (bitmap != null)
                    {
                        _yokaiIcons.TryAdd(modelName, bitmap); // Cache fallback too? Assuming modelName is unique enough
                        return bitmap;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static System.Drawing.Image? LoadYokaiIconFromGame(IGame game, string modelName)
        {
            if (game == null || game.Files == null || !game.Files.ContainsKey("face_icon")) return null;

            try
            {
                var file = game.Files["face_icon"];
                string fullPath = $"{file.Path}/{modelName}.xi";

                if (game is YW2 yw2 && yw2.Game != null && yw2.Game.Directory != null)
                {
                    if (!yw2.Game.Directory.FileExists(fullPath)) return null;

                    var vf = yw2.Game.Directory.GetFileStreamFromFullPath(fullPath);
                    if (vf != null)
                    {
                        byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                        if (data != null && data.Length > 0)
                        {
                            return IMGC.ToBitmap(data);
                        }
                    }
                }
            }
            catch
            {
                // Silently fail
            }

            return null;
        }

        private static BitmapImage? ToBitmapImage(System.Drawing.Image? bitmap)
        {
            if (bitmap == null) return null;
            try
            {
                using (var memory = new MemoryStream())
                {
                    bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    return bitmapImage;
                }
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage LoadBitmap(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
