using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Albatross.Tools;
using Albatross.Level5.Text;
using Albatross.Level5.Text.Logic;
using Albatross.Level5.Archive.ARC0;
using Albatross.Level5.Binary;
using Albatross.Yokai_Watch.Games.YW2.Logic;
using Albatross.Yokai_Watch.Logic;
using Albatross.Level5.Binary.Logic;
using Albatross.Level5.Archive;
using Albatross.Level5.Archive.XPCK;

namespace Albatross.Yokai_Watch.Games.YW2
{
    public class YW2 : IGame
    {
        public string Name => "Yo-Kai Watch 2";
        public Dictionary<int, string> Tribes => Common.Tribes.YW2;
        public Dictionary<int, string> FoodsType => Common.FoodsType.YW2;
        public Dictionary<int, string> ScoutablesType => Common.ScoutablesType.YW2;
        public ARC0 Game { get; set; }
        public ARC0 Language { get; set; }
        public string LanguageCode { get; set; }
        public string RomfsPath { get; private set; }
        public Dictionary<string, GameFile> Files { get; set; }
        private Dictionary<string, string> _filePathCache = new Dictionary<string, string>();

        public YW2(string romfsPath, string language)
        {
            try
            {
                RomfsPath = romfsPath;
                LanguageCode = language;

                System.Diagnostics.Debug.WriteLine($"[YW2] Constructor called. RomfsPath: {RomfsPath}, Language: {LanguageCode}");

                // 1. 메인 게임 파일 로드
                string gamePath = RomfsPath + @"\yw2_a.fa";
                if (File.Exists(gamePath))
                {
                    Game = new ARC0(new FileStream(gamePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    System.Diagnostics.Debug.WriteLine("[YW2] Game loaded.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[YW2] Error: Game file not found at {gamePath}");
                }

                // 2. 언어 파일 로드
                string langFilePath = RomfsPath + @"\yw2_lg_" + LanguageCode + ".fa";
                if (File.Exists(langFilePath))
                {
                    Language = new ARC0(new FileStream(langFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    System.Diagnostics.Debug.WriteLine("[YW2] Language loaded.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[YW2] Warning: Language file not found at {langFilePath}");
                }

                // ✅ 파일 목록 초기화 (생성자에서 호출)
                InitializeFiles();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YW2] Constructor Exception: {ex}");
                throw;
            }
        }


        private void InitializeFiles()
        {
            _filePathCache.Clear();

            // ===== 디렉토리 구조 출력 (진단용) =====
            System.Diagnostics.Debug.WriteLine("\n========== GAME DIRECTORY STRUCTURE ==========");
            PrintDirectoryStructure(Game.Directory, "", 0, 3); // 3단계까지만

            if (Language != null)
            {
                System.Diagnostics.Debug.WriteLine("\n========== LANGUAGE DIRECTORY STRUCTURE ==========");
                PrintDirectoryStructure(Language.Directory, "", 0, 3); // 3단계까지만
            }
            System.Diagnostics.Debug.WriteLine("==============================================\n");

            InitializeFilesInternal();
        }


        private void InitializeFilesInternal()
        {
            System.Diagnostics.Debug.WriteLine("[YW2] InitializeFilesInternal started.");
            Files = new Dictionary<string, GameFile>();

            if (Game == null || Game.Directory == null)
            {
                System.Diagnostics.Debug.WriteLine("[YW2] Error: Game archive or Directory is null.");
                return;
            }

            string langSuffix = "_" + (string.IsNullOrEmpty(LanguageCode) ? "ko" : LanguageCode) + ".cfg.bin";
            var textFiles = new[]
            {
                "chara_text",
                "item_text",
                "battle_text",
                "skill_text",
                "chara_ability_text",
                "system_text",
                "addmembermenu_text"
            };

            // Text files - Language 우선, Game fallback
            foreach (var key in textFiles)
            {
                string fileName = key + langSuffix;
                bool found = false;

                // Try Language archive first
                if (Language != null)
                {
                    try
                    {
                        var textFolder = SafeGetFolder(Language.Directory, new[] { "data", "res", "text" });
                        if (textFolder != null && textFolder.Files != null && textFolder.Files.ContainsKey(fileName))
                        {
                            string knownPath = "data/res/text/" + fileName;
                            Files[key] = new GameFile(Language, knownPath);
                            System.Diagnostics.Debug.WriteLine($"[YW2] ✓ Found {key} in Language");
                            found = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YW2] Language check error for {key}: {ex.Message}");
                    }
                }

                // Try Game archive
                if (!found)
                {
                    try
                    {
                        var textFolder = SafeGetFolder(Game.Directory, new[] { "data", "res", "text" });
                        if (textFolder != null && textFolder.Files != null && textFolder.Files.ContainsKey(fileName))
                        {
                            string knownPath = "data/res/text/" + fileName;
                            Files[key] = new GameFile(Game, knownPath);
                            System.Diagnostics.Debug.WriteLine($"[YW2] ✓ Found {key} in Game");
                            found = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YW2] Game check error for {key}: {ex.Message}");
                    }
                }

                // Fallback: Recursive search
                if (!found)
                {
                    System.Diagnostics.Debug.WriteLine($"[YW2] Searching recursively for {fileName}...");
                    string foundPath = SearchFileInDirectory(Language?.Directory, fileName)
                                    ?? SearchFileInDirectory(Game.Directory, fileName);

                    if (foundPath != null)
                    {
                        var archive = SearchFileInDirectory(Language?.Directory, fileName) != null ? Language : Game;
                        Files[key] = new GameFile(archive, foundPath);
                        System.Diagnostics.Debug.WriteLine($"[YW2] ✓ Found {key} via search: {foundPath}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[YW2] ✗ Failed to find {key}");
                    }
                }
            }

            // Config files
            var paramFiles = new[]
            {
                "chara_base",
                "chara_param",
                "skill_config",
                "item_config"
            };

            foreach (var key in paramFiles)
            {
                string fileName = key + ".cfg.bin";
                bool found = false;

                try
                {
                    var paramFolder = SafeGetFolder(Game.Directory, new[] { "data", "res", "param" });
                    if (paramFolder != null && paramFolder.Files != null && paramFolder.Files.ContainsKey(fileName))
                    {
                        string knownPath = "data/res/param/" + fileName;
                        Files[key] = new GameFile(Game, knownPath);
                        System.Diagnostics.Debug.WriteLine($"[YW2] ✓ Found {key}");
                        found = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YW2] Param check error for {key}: {ex.Message}");
                }

                if (!found)
                {
                    System.Diagnostics.Debug.WriteLine($"[YW2] Searching recursively for {fileName}...");
                    string foundPath = SearchFileInDirectory(Game.Directory, fileName);
                    if (foundPath != null)
                    {
                        Files[key] = new GameFile(Game, foundPath);
                        System.Diagnostics.Debug.WriteLine($"[YW2] ✓ Found {key} via search: {foundPath}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[YW2] ✗ Failed to find {key}");
                    }
                }
            }

            // Resource folders
            var resourceFolders = new Dictionary<string, string[]>
            {
                { "face_icon", new[] { "data", "menu", "face_icon" } },
                { "item_icon", new[] { "data", "menu", "item_icon" } },
                { "model", new[] { "data", "res", "character" } },
                { "map_encounter", new[] { "data", "res", "map" } },
                { "battle_encounter", new[] { "data", "res", "battle" } }
            };

            foreach (var kvp in resourceFolders)
            {
                try
                {
                    var folder = SafeGetFolder(Game.Directory, kvp.Value);
                    if (folder != null)
                    {
                        string knownPath = string.Join("/", kvp.Value);
                        Files[kvp.Key] = new GameFile(Game, knownPath);
                        System.Diagnostics.Debug.WriteLine($"[YW2] ✓ Found folder {kvp.Key}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[YW2] ✗ Folder {kvp.Key} not found");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YW2] Folder error {kvp.Key}: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine("[YW2] InitializeFilesInternal completed.");
        }

        // 안전한 폴더 검색 메서드 (.NET 10 호환)
        private VirtualDirectory SafeGetFolder(VirtualDirectory root, string[] pathParts)
        {
            if (root == null || pathParts == null || pathParts.Length == 0)
                return null;

            VirtualDirectory current = root;

            foreach (string part in pathParts)
            {
                if (current.Folders == null)
                    return null;

                VirtualDirectory next = null;
                foreach (var folder in current.Folders)
                {
                    if (folder != null &&
                        !string.IsNullOrEmpty(folder.Name) &&
                        folder.Name.Equals(part, StringComparison.OrdinalIgnoreCase))
                    {
                        next = folder;
                        break;
                    }
                }

                if (next == null)
                    return null;

                current = next;
            }

            return current;
        }



        private VirtualDirectory FindFolderContainsPath(VirtualDirectory dir, string keyword)
        {
            if (dir == null) return null;

            if (!string.IsNullOrEmpty(dir.Name) && dir.Name.Contains(keyword))
                return dir;

            foreach (var sub in dir.Folders)
            {
                var found = FindFolderContainsPath(sub, keyword);
                if (found != null) return found;
            }

            return null;
        }


        // ✅ [신규] 재귀적으로 파일 검색
        private string SearchFileInDirectory(VirtualDirectory dir, string targetFileName, string currentPath = "")
        {
            if (dir == null) return null;

            // 1. 현재 폴더의 Files에서 검색
            if (dir.Files != null)
            {
                // 정확한 매칭
                if (dir.Files.ContainsKey(targetFileName))
                {
                    string path = string.IsNullOrEmpty(currentPath) ? targetFileName : currentPath + "/" + targetFileName;
                    return path;
                }

                // 끝부분 매칭
                foreach (var key in dir.Files.Keys)
                {
                    if (key.EndsWith(targetFileName))
                    {
                        return key;
                    }
                }

                // 부분 매칭
                foreach (var key in dir.Files.Keys)
                {
                    if (key.Contains(targetFileName))
                    {
                        return key;
                    }
                }
            }

            // 2. 하위 폴더 재귀 검색
            if (dir.Folders != null)
            {
                foreach (var subDir in dir.Folders)
                {
                    if (subDir == null) continue;

                    string subPath = string.IsNullOrEmpty(currentPath)
                        ? (subDir.Name ?? "")
                        : currentPath + "/" + (subDir.Name ?? "");

                    string found = SearchFileInDirectory(subDir, targetFileName, subPath);
                    if (!string.IsNullOrEmpty(found))
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        // FindFolderPath는 기존 코드 유지
        private string FindFolderPath(VirtualDirectory dir, string targetFolderName, string currentPath = "")
        {
            if (dir == null) return null;

            if (!string.IsNullOrEmpty(dir.Name) && dir.Name.Equals(targetFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return currentPath;
            }

            if (dir.Folders != null)
            {
                foreach (var subDir in dir.Folders)
                {
                    if (subDir == null) continue;

                    string subPath = string.IsNullOrEmpty(currentPath)
                        ? (subDir.Name ?? "")
                        : currentPath + "/" + (subDir.Name ?? "");

                    if (!string.IsNullOrEmpty(subDir.Name) && subDir.Name.Equals(targetFolderName, StringComparison.OrdinalIgnoreCase))
                    {
                        return subPath;
                    }

                    string found = FindFolderPath(subDir, targetFolderName, subPath);
                    if (!string.IsNullOrEmpty(found))
                    {
                        return found;
                    }
                }
            }

            return null;
        }



        public void Save(Action<int, int, string> progressCallback = null)
        {
            string tempPath = Path.Combine(Path.GetDirectoryName(RomfsPath), "temp");
            if (!Directory.Exists(tempPath)) Directory.CreateDirectory(tempPath);

            string tempGameFile = Path.Combine(tempPath, "yw2_a.fa");
            string destGameFile = RomfsPath + @"\yw2_a.fa";

            Game.Save(tempGameFile, progressCallback);

            if (Language != null)
            {
                string tempLangFile = Path.Combine(tempPath, "yw2_lg_" + LanguageCode + ".fa");
                string destLangFile = RomfsPath + @"\yw2_lg_" + LanguageCode + ".fa";
                Language.Save(tempLangFile, progressCallback);

                Language.Close();
                if (File.Exists(destLangFile)) File.Delete(destLangFile);
                File.Move(tempLangFile, destLangFile);
                Language = new ARC0(new FileStream(destLangFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            }

            Game.Close();
            if (File.Exists(destGameFile)) File.Delete(destGameFile);
            File.Move(tempGameFile, destGameFile);

            Game = new ARC0(new FileStream(destGameFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            InitializeFiles();
        }

        private byte[] FindFileRecursive(VirtualDirectory dir, string targetName)
        {
            if (_filePathCache.TryGetValue(targetName, out string cachedPath))
            {
                try
                {
                    var file = dir.Files.ContainsKey(cachedPath) ? dir.Files[cachedPath] : null;
                    if (file != null)
                    {
                        return file.ByteContent ?? file.ReadWithoutCaching();
                    }
                }
                catch
                {
                    _filePathCache.Remove(targetName);
                }
            }

            return FindFileRecursiveUncached(dir, targetName);
        }

        private byte[] FindFileRecursiveUncached(VirtualDirectory dir, string targetName)
        {
            foreach (var fileKey in dir.Files.Keys)
            {
                if (fileKey.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                    fileKey.EndsWith("/" + targetName, StringComparison.OrdinalIgnoreCase) ||
                    (targetName.Length > 5 && fileKey.Contains(targetName)))
                {
                    var file = dir.Files[fileKey];
                    _filePathCache[targetName] = fileKey;
                    if (file.ByteContent != null)
                        return file.ByteContent;
                    return file.ReadWithoutCaching();
                }
            }
            foreach (var sub in dir.Folders)
            {
                byte[] found = FindFileRecursiveUncached(sub, targetName);
                if (found != null) return found;
            }
            return null;
        }

        public T2bþ GetTextObject(string textFile)
        {
            T2bþ result = new T2bþ();
            result.Encoding = System.Text.Encoding.UTF8;

            byte[] fileData = null;
            string loadedFrom = "";

            if (Files.ContainsKey(textFile))
            {
                try
                {
                    var gf = Files[textFile];
                    string cleanPath = gf.Path.Replace("\\", "/").Trim('/');
                    fileData = gf.File.Directory.GetFileDataReadOnly(cleanPath);
                    loadedFrom = $"Files[{textFile}] → {cleanPath}";
                }
                catch (Exception)
                {
                    // Fallback to FindFileRecursive
                }
            }

            if (fileData == null)
            {
                string targetName = Path.GetFileName(textFile);
                if (!targetName.Contains("."))
                {
                    targetName += "_" + (string.IsNullOrEmpty(LanguageCode) ? "ko" : LanguageCode) + ".cfg.bin";
                }

                if (Language != null)
                {
                    fileData = FindFileRecursive(Language.Directory, targetName);
                    if (fileData != null) loadedFrom = $"Language Archive → FindFileRecursive({targetName})";
                }

                if (fileData == null)
                {
                    fileData = FindFileRecursive(Game.Directory, targetName);
                    if (fileData != null) loadedFrom = $"Game Archive → FindFileRecursive({targetName})";
                }

                if (fileData == null && targetName.Contains("_"))
                {
                    string originalName = targetName.Split('_')[0] + ".cfg.bin";
                    fileData = FindFileRecursive(Game.Directory, originalName);
                    if (fileData != null) loadedFrom = $"Game Archive → FindFileRecursive({originalName})";
                }
            }

            if (fileData == null) return result;

            try
            {
                result.Open(fileData);

                if (result.Texts.Count == 0 && result.Nouns.Count == 0)
                {
                    ManualParseText(fileData, result);
                }
            }
            catch (Exception)
            {
                ManualParseText(fileData, result);
            }

            return result;
        }

        private void ManualParseText(byte[] data, T2bþ container)
        {
            try
            {
                CfgBin cfg = new CfgBin();
                cfg.Open(data);

                string[] targets = { "NOUN_INFO_BEGIN", "TEXT_INFO_BEGIN", "ITEM_TEXT_INFO_BEGIN", "BATTLE_TEXT_INFO_BEGIN", "SKILL_TEXT_INFO_BEGIN", "CHARA_ABILITY_TEXT_INFO_BEGIN", "SYSTEM_TEXT_INFO_BEGIN", "ADD_MEMBER_MENU_TEXT_INFO_BEGIN" };
                var entries = cfg.Entries.Where(x => targets.Contains(x.GetName())).ToList();
                if (entries.Count == 0 && cfg.Entries.Count > 0) entries = cfg.Entries.Where(x => x.Children.Count > 10).ToList();

                foreach (var entry in entries)
                {
                    foreach (var child in entry.Children)
                    {
                        if (child.Variables.Count > 0)
                        {
                            int crc = Convert.ToInt32(child.Variables[0].Value);
                            string txt = "";
                            foreach (var v in child.Variables)
                            {
                                if (v.Value is OffsetTextPair p && !string.IsNullOrEmpty(p.Text)) { txt = p.Text; break; }
                                else if (v.Value is string s && !string.IsNullOrEmpty(s)) { txt = s; break; }
                            }
                            if (string.IsNullOrEmpty(txt)) continue;

                            var config = new TextConfig(new List<StringLevel5> { new StringLevel5(0, txt) }, -1);
                            if (!container.Nouns.ContainsKey(crc)) container.Nouns.Add(crc, config);
                            if (!container.Texts.ContainsKey(crc)) container.Texts.Add(crc, config);
                        }
                    }
                }
            }
            catch { }
        }

        // file path와 content를 함께 반환하는 헬퍼 메서드
        private (string Path, byte[] Content) FindFileRecursiveWithPath(VirtualDirectory dir, string targetName)
        {
            foreach (var fileKey in dir.Files.Keys)
            {
                // 경로 구분자로 나눈 마지막 부분(파일명)만 추출
                string fileName = fileKey;
                int lastSlash = fileName.LastIndexOf('/');
                if (lastSlash >= 0) fileName = fileName.Substring(lastSlash + 1);

                // 엄격한 매칭: 정확히 일치하거나, 파일명이 targetName으로 시작해야 함
                if (fileName.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    var file = dir.Files[fileKey];
                    // [FIX] Use ReadWithoutCaching to avoid marking file as modified
                    byte[] content = file.ByteContent ?? file.ReadWithoutCaching();
                    return (fileKey, content);
                }
            }
            foreach (var sub in dir.Folders)
            {
                var found = FindFileRecursiveWithPath(sub, targetName);
                if (found.Content != null) return found;
            }
            return (null, null);
        }

        // 로깅을 포함한 파일 검색 헬퍼
        private byte[] FindFileWithLog(string callerName, string searchKeyword)
        {
            System.Diagnostics.Debug.WriteLine($"\n[{callerName}] 불러올 파일 검색 중: \"{searchKeyword}\"...");
            var found = FindFileRecursiveWithPath(Game.Directory, searchKeyword);

            if (found.Content == null)
            {
                System.Diagnostics.Debug.WriteLine($"[{callerName}] ❌ 파일을 찾지 못했습니다.");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"[{callerName}] ✅ 실제 로드된 파일: \"{found.Path}\" ({found.Content.Length:N0} bytes)");
            return found.Content;
        }

        public ICharaabilityConfig[] GetAbilities()
        {
            byte[] data = FindFileWithLog("GetAbilities", "chara_ability");
            if (data == null) return new ICharaabilityConfig[0];

            CfgBin cfg = new CfgBin();
            cfg.Open(data);

            return cfg.Entries
                .Where(x => x.GetName() == "CHARA_ABILITY_CONFIG_INFO_LIST_BEG")
                .SelectMany(x => x.Children)
                .Where(x => x.GetName() == "CHARA_ABILITY_CONFIG_INFO")
                .Select(x =>
                {
                    var ab = x.ToClass<CharaabilityConfig>();
                    if (x.Variables.Count > 0) ab.CharaabilityConfigHash = Convert.ToInt32(x.Variables[0].Value);
                    return (ICharaabilityConfig)ab;
                })
                .ToArray();
        }

        public IBattleCommand[] GetBattleCommands()
        {
            System.Diagnostics.Debug.WriteLine("\n[GetBattleCommands] 불러올 파일 검색 중: \"battle_command\"...");

            byte[] data = null;
            string loadedPath = "";

            Action<VirtualDirectory> scanner = null;
            scanner = (d) =>
            {
                var k = d.Files.Keys
                    .Where(x => x.StartsWith("battle_command") && !x.Contains("link"))
                    .Max();
                if (k != null)
                {
                    // [FIX] Use read-only access to avoid marking file as modified
                    data = d.GetFileDataReadOnly(k);
                    loadedPath = k; // 경로 저장 (실제로는 파일명만 키로 저장됨, 필요시 수정 가능하나 여기선 키값 사용)
                    // VirtualDirectory 구조상 k가 전체 경로일 수도, 파일명일 수도 있음. 
                    // GetFileFromFullPath가 동작하므로 k는 유효한 경로.
                    return;
                }
                foreach (var s in d.Folders) if (data == null) scanner(s);
            };
            scanner(Game.Directory);

            if (data == null)
            {
                System.Diagnostics.Debug.WriteLine("[GetBattleCommands] ❌ 파일을 찾지 못했습니다.");
                return new IBattleCommand[0];
            }

            System.Diagnostics.Debug.WriteLine($"[GetBattleCommands] ✅ 실제 로드된 파일: \"{loadedPath}\" ({data.Length:N0} bytes)");

            CfgBin cfg = new CfgBin();
            cfg.Open(data);
            return cfg.Entries.Where(x => x.GetName() == "BATTLE_COMMAND_INFO_BEGIN")
                .SelectMany(x => x.Children).Select(x => (IBattleCommand)x.ToClass<Battlecommand>()).ToArray();
        }

        public ICharabase[] GetCharacterbase(bool isYokai)
        {
            byte[] data = FindFileWithLog("GetCharacterbase", "chara_base");
            if (data == null) return new ICharabase[0];
            CfgBin f = new CfgBin(); f.Open(data);
            string b = isYokai ? "CHARA_BASE_YOKAI_INFO_BEGIN" : "CHARA_BASE_INFO_BEGIN";
            var e = f.Entries.FirstOrDefault(x => x.GetName() == b) ?? f.Entries.FirstOrDefault(x => x.GetName().Contains("INFO_BEGIN"));
            if (e == null) return new ICharabase[0];
            return e.Children.Select(x =>
            {
                try
                {
                    if (isYokai)
                    {
                        var y = x.ToClass<YokaiCharabase>();
                        if (x.Variables.Count > 4) { y.BaseHash = (int)x.Variables[0].Value; y.NameHash = (int)x.Variables[4].Value; }
                        return (ICharabase)y;
                    }
                    return (ICharabase)x.ToClass<NPCCharabase>();
                }
                catch { return null; }
            }).Where(x => x != null).ToArray();
        }

        public ICharaparam[] GetCharaparam()
        {
            byte[] data = FindFileWithLog("GetCharaparam", "chara_param");
            if (data == null) return new ICharaparam[0];
            CfgBin f = new CfgBin(); f.Open(data);
            return f.Entries.Where(x => x.GetName() == "CHARA_PARAM_INFO_BEGIN").SelectMany(x => x.Children).Select(x => x.ToClass<Charaparam>()).ToArray();
        }

        public ICharabase[] GetBosses()
        {
            byte[] data = FindFileWithLog("GetBosses", "chara_param");
            if (data == null) return new ICharabase[0];
            CfgBin f = new CfgBin(); f.Open(data);
            return f.Entries.Where(x => x.GetName() == "BOSS_PARAM_INFO_BEGIN")
                .SelectMany(x => x.Children)
                .Select(x =>
                {
                    try
                    {
                        var param = x.ToClass<Charaparam>();
                        // Construct a basic YokaiCharabase from the param info to allow it to be added to Charabases list
                        return (ICharabase)new YokaiCharabase
                        {
                            BaseHash = param.BaseHash,
                            FileNamePrefix = 0, // Default or unknown
                            FileNameNumber = 0,
                            FileNameVariant = 0
                        };
                    }
                    catch { return null; }
                })
                .Where(x => x != null)
                .ToArray();
        }

        public void SaveCharaparam(ICharaparam[] p)
        {
            // This method is kept for backward compatibility but now does nothing
            // Actual saving happens in SaveCharaevolution which saves both sections
        }

        public IItem[] GetItems(string type)
        {
            byte[] data = FindFileWithLog("GetItems", "item_config");
            if (data == null) return new IItem[0];
            CfgBin f = new CfgBin(); f.Open(data);
            if (type == "all")
            {
                string[] t = { "ITEM_EQUIPMENT_BEGIN", "ITEM_SOUL_BEGIN", "ITEM_CONSUME_BEGIN", "ITEM_IMPORTANT_BEGIN", "ITEM_CREATURE_BEGIN" };
                return f.Entries.Where(x => t.Contains(x.GetName())).SelectMany(x => x.Children).Select(x => x.ToClass<Item>()).ToArray();
            }
            return new IItem[0];
        }

        public ICharaevolve[] GetCharaevolution()
        {
            byte[] data = FindFileWithLog("GetCharaevolution", "chara_param");
            if (data == null) return new ICharaevolve[0];
            CfgBin f = new CfgBin(); f.Open(data);
            return f.Entries.Where(x => x.GetName() == "CHARA_EVOLVE_INFO_BEGIN").SelectMany(x => x.Children).Select(x => x.ToClass<Charaevolve>()).ToArray();
        }

        public void SaveCharaBase(ICharabase[] c)
        {
            NPCCharabase[] n = c.OfType<NPCCharabase>().ToArray();
            YokaiCharabase[] y = c.OfType<YokaiCharabase>().ToArray();
            VirtualDirectory td = null; string fn = "";
            Action<VirtualDirectory> sc = null;
            sc = (d) =>
            {
                var k = d.Files.Keys.FirstOrDefault(x => x.StartsWith("chara_base"));
                if (k != null) { fn = k; td = d; return; }
                foreach (var s in d.Folders) if (td == null) sc(s);
            };
            sc(Game.Directory);
            if (td == null) return;
            CfgBin f = new CfgBin();
            // [FIX] Use read-only access for initial load
            f.Open(td.GetFileDataReadOnly(fn));
            f.ReplaceEntry("CHARA_BASE_INFO_BEGIN", "CHARA_BASE_INFO_", n);
            f.ReplaceEntry("CHARA_BASE_YOKAI_INFO_BEGIN", "CHARA_BASE_YOKAI_INFO_", y);
            td.Files[fn].ByteContent = f.Save();
        }

        private class EncounterFileContext
        {
            public VirtualDirectory MapFolder;
            public string EncounterFileName;
            public bool IsPck;
            public string PckFileName;
            public XPCK PckArchive;
            public byte[] Data;
        }

        private string GetEncounterRootPath()
        {
            if (Files != null && Files.ContainsKey("map_encounter") && !string.IsNullOrWhiteSpace(Files["map_encounter"].Path))
            {
                return Files["map_encounter"].Path.Replace("\\", "/").Trim('/');
            }

            return "data/res/map";
        }

        private static bool IsEncounterConfigFileName(string mapName, string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            string lower = fileName.ToLowerInvariant();
            string mapLower = (mapName ?? "").ToLowerInvariant();

            if (lower.Contains("_enc_pos"))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mapLower) && lower.StartsWith(mapLower + "_enc_"))
            {
                return true;
            }

            if (mapLower == "common_enc" && lower.StartsWith("common_enc"))
            {
                return true;
            }

            if (mapLower == "yokaispot" && (lower.Contains("yokaispot") || lower.Contains("ys_yokai")))
            {
                return true;
            }

            return false;
        }

        private void LogEncounter(string stage, string mapName, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Encounter:{stage}] [{mapName}] {message}";
            Console.WriteLine(line);
            System.Diagnostics.Debug.WriteLine(line);

            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "encounter.log");
                File.AppendAllText(logPath, line + Environment.NewLine);
            }
            catch
            {
                // Ignore file logging failures.
            }
        }

        private bool TryResolveEncounterFile(string mapName, out EncounterFileContext context)
        {
            context = null;

            try
            {
                string rootPath = GetEncounterRootPath();
                VirtualDirectory mapRoot = Game.Directory.GetFolderFromFullPath(rootPath);
                VirtualDirectory mapFolder = mapRoot.GetFolder(mapName);
                if (mapFolder == null)
                {
                    LogEncounter("resolve", mapName, $"Map folder not found under '{rootPath}'.");
                    return false;
                }

                // 1) Direct encounter cfg in map folder.
                string directFile = mapFolder.Files.Keys
                    .Where(x => IsEncounterConfigFileName(mapName, x))
                    .OrderByDescending(x => x)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(directFile))
                {
                    context = new EncounterFileContext
                    {
                        MapFolder = mapFolder,
                        EncounterFileName = directFile,
                        IsPck = false,
                        Data = mapFolder.GetFileDataReadOnly(directFile)
                    };
                    return true;
                }

                // 2) Encounter cfg in map pck.
                string pckName = mapFolder.Files.Keys
                    .FirstOrDefault(x => x.Equals(mapName + ".pck", StringComparison.OrdinalIgnoreCase))
                    ?? mapFolder.Files.Keys.FirstOrDefault(x => x.EndsWith(".pck", StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(pckName))
                {
                    LogEncounter("resolve", mapName, "No direct encounter file or pck found.");
                    return false;
                }

                byte[] pckData = mapFolder.GetFileDataReadOnly(pckName);
                XPCK pck = new XPCK(pckData);

                string pckEncounterFile = pck.Directory.Files.Keys
                    .Where(x => IsEncounterConfigFileName(mapName, x))
                    .OrderByDescending(x => x)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(pckEncounterFile))
                {
                    LogEncounter("resolve", mapName, $"PCK '{pckName}' does not contain encounter config.");
                    return false;
                }

                context = new EncounterFileContext
                {
                    MapFolder = mapFolder,
                    EncounterFileName = pckEncounterFile,
                    IsPck = true,
                    PckFileName = pckName,
                    PckArchive = pck,
                    Data = pck.Directory.GetFileFromFullPath(pckEncounterFile)
                };

                return true;
            }
            catch (Exception ex)
            {
                LogEncounter("resolve", mapName, $"Failed: {ex.Message}");
                return false;
            }
        }

        private static Entry FindEncounterSlotBeginEntry(CfgBin cfg)
        {
            if (cfg == null) return null;

            Entry exact = cfg.Entries.FirstOrDefault(x => x.GetName() == "ENCOUNT_CHARA_BEGIN");
            if (exact != null) return exact;

            Entry yokaiSpotNamed = cfg.Entries.FirstOrDefault(x =>
                x.GetName().IndexOf("YS_YOKAI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.GetName().IndexOf("YOKAISPOT", StringComparison.OrdinalIgnoreCase) >= 0);
            if (yokaiSpotNamed != null) return yokaiSpotNamed;

            return cfg.Entries.FirstOrDefault(x =>
                x.GetName().IndexOf("CHARA_BEGIN", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsYokaiSpotSlotEntry(Entry slotEntry, string mapName)
        {
            if (slotEntry == null) return false;

            if (slotEntry.GetName().IndexOf("YS_YOKAI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                slotEntry.GetName().IndexOf("YOKAISPOT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (mapName != null && mapName.Equals("yokaispot", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return slotEntry.Children.Any(c => c.Variables.Count >= 9);
        }

        private static string ResolveEntryPrefix(Entry beginEntry, string fallbackPrefix)
        {
            if (beginEntry != null && beginEntry.Children.Count > 0)
            {
                string childName = beginEntry.Children[0].GetName();
                int lastDigit = childName.Length - 1;
                while (lastDigit >= 0 && char.IsDigit(childName[lastDigit]))
                {
                    lastDigit--;
                }

                if (lastDigit + 1 < childName.Length)
                {
                    return childName.Substring(0, lastDigit + 1);
                }
            }

            return fallbackPrefix;
        }

        private static void ValidateEncounterConfig(CfgBin cfg, string mapName)
        {
            if (cfg == null || cfg.Entries == null || cfg.Entries.Count == 0)
            {
                throw new InvalidDataException("Encounter config is empty.");
            }

            Entry tableEntry = cfg.Entries.FirstOrDefault(x => x.GetName() == "ENCOUNT_TABLE_BEGIN");
            if (tableEntry == null)
            {
                throw new InvalidDataException("Missing ENCOUNT_TABLE_BEGIN.");
            }

            Entry slotEntry = FindEncounterSlotBeginEntry(cfg);
            if (slotEntry == null)
            {
                throw new InvalidDataException("Missing encounter slot begin entry.");
            }

            bool expectsYokaiSpot = mapName != null && mapName.Equals("yokaispot", StringComparison.OrdinalIgnoreCase);
            bool isYokaiSpot = IsYokaiSpotSlotEntry(slotEntry, mapName);

            if (expectsYokaiSpot && !isYokaiSpot)
            {
                throw new InvalidDataException("YokaiSpot map does not contain YokaiSpot slot schema.");
            }

            if (isYokaiSpot && slotEntry.Children.Any(c => c.Variables.Count < 9))
            {
                throw new InvalidDataException("YokaiSpot slot schema is incomplete (expected 9 variables).");
            }
        }

        private static byte[] SaveXpckToBytes(XPCK archive)
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                archive.Save(tempFile, null);
                return File.ReadAllBytes(tempFile);
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        public string[] GetMapWhoContainsEncounter()
        {
            List<string> mapNames = new List<string>();

            try
            {
                string rootPath = GetEncounterRootPath();
                VirtualDirectory mapRoot = Game.Directory.GetFolderFromFullPath(rootPath);

                foreach (var folder in mapRoot.Folders)
                {
                    bool hasDirect = folder.Files.Keys.Any(x => IsEncounterConfigFileName(folder.Name, x));
                    bool hasPck = false;

                    if (!hasDirect)
                    {
                        string pckName = folder.Files.Keys
                            .FirstOrDefault(x => x.Equals(folder.Name + ".pck", StringComparison.OrdinalIgnoreCase))
                            ?? folder.Files.Keys.FirstOrDefault(x => x.EndsWith(".pck", StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(pckName))
                        {
                            try
                            {
                                byte[] pckData = folder.GetFileDataReadOnly(pckName);
                                XPCK pck = new XPCK(pckData);
                                hasPck = pck.Directory.Files.Keys.Any(x => IsEncounterConfigFileName(folder.Name, x));
                            }
                            catch (Exception ex)
                            {
                                LogEncounter("scan", folder.Name, $"PCK scan error: {ex.Message}");
                            }
                        }
                    }

                    if (hasDirect || hasPck)
                    {
                        mapNames.Add(folder.Name);
                    }
                }

                // Fallback for rare layouts with direct files at root.
                foreach (string fileName in mapRoot.Files.Keys)
                {
                    if (fileName.Contains("_enc_"))
                    {
                        int idx = fileName.IndexOf("_enc_", StringComparison.OrdinalIgnoreCase);
                        if (idx > 0)
                        {
                            mapNames.Add(fileName.Substring(0, idx));
                        }
                    }
                    else if (fileName.IndexOf("yokaispot", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mapNames.Add("yokaispot");
                    }
                }
            }
            catch (Exception ex)
            {
                LogEncounter("scan", "global", $"Failed: {ex.Message}");
            }

            return mapNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToArray();
        }

        public (IEncountTable[], IEncountChara[]) GetMapEncounter(string mapName)
        {
            LogEncounter("load-start", mapName, "Loading encounter data.");

            EncounterFileContext context;
            if (!TryResolveEncounterFile(mapName, out context) || context == null || context.Data == null)
            {
                throw new FileNotFoundException($"Encounter file not found for map '{mapName}'.");
            }

            LogEncounter("file-resolve", mapName, context.IsPck
                ? $"Resolved in PCK '{context.PckFileName}' as '{context.EncounterFileName}'."
                : $"Resolved as direct file '{context.EncounterFileName}'.");

            CfgBin cfg = new CfgBin();
            cfg.Open(context.Data);

            ValidateEncounterConfig(cfg, mapName);

            Entry tableEntry = cfg.Entries.First(x => x.GetName() == "ENCOUNT_TABLE_BEGIN");
            Entry slotEntry = FindEncounterSlotBeginEntry(cfg);
            bool isYokaiSpot = IsYokaiSpotSlotEntry(slotEntry, mapName);

            IEncountTable[] tables = tableEntry.Children
                .Select(x => (IEncountTable)x.ToClass<EncountTable>())
                .ToArray();

            IEncountChara[] charas;
            if (isYokaiSpot)
            {
                charas = slotEntry.Children
                    .Select(x => (IEncountChara)x.ToClass<YokaiSpotChara>())
                    .ToArray();
            }
            else
            {
                charas = slotEntry.Children
                    .Select(x => (IEncountChara)x.ToClass<EncountChara>())
                    .ToArray();
            }

            // Keep offsets safe even if source data is partially broken.
            foreach (var table in tables)
            {
                if (table.EncountOffsets == null)
                {
                    table.EncountOffsets = new int[6];
                }

                for (int i = 0; i < table.EncountOffsets.Length; i++)
                {
                    if (table.EncountOffsets[i] < -1 || table.EncountOffsets[i] >= charas.Length)
                    {
                        table.EncountOffsets[i] = -1;
                    }
                }
            }

            LogEncounter("load-finish", mapName, $"Loaded {tables.Length} tables / {charas.Length} slots (YokaiSpot={isYokaiSpot}).");
            return (tables, charas);
        }

        public void SaveMapEncounter(string mapName, IEncountTable[] encountTables, IEncountChara[] encountCharas)
        {
            LogEncounter("save-start", mapName, $"Saving {encountTables?.Length ?? 0} tables / {encountCharas?.Length ?? 0} slots.");

            EncounterFileContext context;
            if (!TryResolveEncounterFile(mapName, out context) || context == null || context.Data == null)
            {
                throw new FileNotFoundException($"Encounter file not found for map '{mapName}'.");
            }

            CfgBin cfg = new CfgBin();
            cfg.Open(context.Data);
            ValidateEncounterConfig(cfg, mapName);

            Entry tableBegin = cfg.Entries.First(x => x.GetName() == "ENCOUNT_TABLE_BEGIN");
            Entry slotBegin = FindEncounterSlotBeginEntry(cfg);
            bool isYokaiSpot = IsYokaiSpotSlotEntry(slotBegin, mapName);

            EncountTable[] concreteTables = (encountTables ?? new IEncountTable[0])
                .Select(x =>
                {
                    var t = x as EncountTable;
                    if (t != null) return t;

                    return new EncountTable
                    {
                        EncountConfigHash = x?.EncountConfigHash ?? 0,
                        EncountOffsets = x?.EncountOffsets ?? new int[6]
                    };
                })
                .ToArray();

            cfg.ReplaceEntry(
                tableBegin.GetName(),
                ResolveEntryPrefix(tableBegin, "ENCOUNT_TABLE_INFO_"),
                concreteTables);

            if (isYokaiSpot)
            {
                YokaiSpotChara[] concreteSpot = (encountCharas ?? new IEncountChara[0])
                    .Select(x =>
                    {
                        var s = x as YokaiSpotChara;
                        if (s != null) return s;

                        return new YokaiSpotChara
                        {
                            ParamHash = x?.ParamHash ?? 0,
                            Level = x?.Level ?? 0,
                            Unk3 = x?.MaxLevel ?? 0,
                            Unk8 = x?.Weight ?? 0
                        };
                    })
                    .ToArray();

                cfg.ReplaceEntry(
                    slotBegin.GetName(),
                    ResolveEntryPrefix(slotBegin, "YS_YOKAI_INFO_"),
                    concreteSpot);
            }
            else
            {
                EncountChara[] concreteStandard = (encountCharas ?? new IEncountChara[0])
                    .Select(x =>
                    {
                        var c = x as EncountChara;
                        if (c != null) return c;

                        return new EncountChara
                        {
                            ParamHash = x?.ParamHash ?? 0,
                            Level = x?.Level ?? 0,
                            Unk1 = x?.MaxLevel ?? 0,
                            Unk2 = x?.Weight ?? 0
                        };
                    })
                    .ToArray();

                cfg.ReplaceEntry(
                    slotBegin.GetName(),
                    ResolveEntryPrefix(slotBegin, "ENCOUNT_CHARA_INFO_"),
                    concreteStandard);
            }

            byte[] newData = cfg.Save();
            if (context.IsPck)
            {
                context.PckArchive.Directory.Files[context.EncounterFileName].ByteContent = newData;
                byte[] newPckData = SaveXpckToBytes(context.PckArchive);
                context.MapFolder.Files[context.PckFileName].ByteContent = newPckData;
            }
            else
            {
                context.MapFolder.Files[context.EncounterFileName].ByteContent = newData;
            }

            LogEncounter("save-finish", mapName, "Save completed.");
        }
        public (IShopConfig[], IShopValidCondition[]) GetShop(string s) => (null, null);
        public void SaveShop(string s, IShopConfig[] c, IShopValidCondition[] v) { }
        public void SaveCharaevolution(ICharaevolve[] charaevolutions)
        {
            // This method is kept for backward compatibility but now does nothing
            // Actual saving happens in SaveCharaparamAndEvolution
        }

        public void SaveCharaparamAndEvolution(ICharaparam[] charaparams, ICharaevolve[] charaevolutions)
        {

            if (charaparams == null && charaevolutions == null) return;

            Charaparam[] formatCharaparams = charaparams?.OfType<Charaparam>().ToArray();
            Charaevolve[] formatCharaevolutions = charaevolutions?.OfType<Charaevolve>().ToArray();

            VirtualDirectory characterFolder = Game.Directory.GetFolderFromFullPath("data/res/character");
            string lastCharaparam = characterFolder.Files.Keys
                .Where(x => x.StartsWith("chara_param"))
                .Max();

            CfgBin charaparamFile = new CfgBin();
            // [FIX] Use read-only access to avoid marking file as modified
            charaparamFile.Open(characterFolder.GetFileDataReadOnly(lastCharaparam));

            // [FIX] Save BOTH sections in single operation to prevent double-save corruption
            if (formatCharaparams != null && formatCharaparams.Length > 0)
            {
                charaparamFile.ReplaceEntry("CHARA_PARAM_INFO_BEGIN", "CHARA_PARAM_INFO_", formatCharaparams);
            }

            if (formatCharaevolutions != null && formatCharaevolutions.Length > 0)
            {
                charaparamFile.ReplaceEntry("CHARA_EVOLVE_INFO_BEGIN", "CHARA_EVOLVE_INFO_", formatCharaevolutions);
            }


            characterFolder.Files[lastCharaparam].ByteContent = charaparamFile.Save();
        }
        public void SaveCharascale(ICharascale[] charascales)
        {
            Charascale[] formatCharascales = charascales.OfType<Charascale>().ToArray();

            VirtualDirectory characterFolder = Game.Directory.GetFolderFromFullPath("data/res/character");
            string lastCharascale = characterFolder.Files.Keys
                .Where(x => x.StartsWith("chara_scale"))
                .Max();

            CfgBin charaScaleFile = new CfgBin();
            // [FIX] Use read-only access for initial load to avoid accidental marking
            charaScaleFile.Open(characterFolder.GetFileDataReadOnly(lastCharascale));

            charaScaleFile.ReplaceEntry("CHARA_SCALE_INFO_LIST_BEG", "CHARA_SCALE_INFO_", formatCharascales);

            characterFolder.Files[lastCharascale].ByteContent = charaScaleFile.Save();
        }

        public ICharascale[] GetCharascale()
        {
            VirtualDirectory characterFolder = Game.Directory.GetFolderFromFullPath("data/res/character");
            string lastCharascale = characterFolder.Files.Keys
                .Where(x => x.StartsWith("chara_scale"))
                .Max();

            CfgBin charaScaleFile = new CfgBin();
            // [FIX] Use read-only access
            charaScaleFile.Open(characterFolder.GetFileDataReadOnly(lastCharascale));

            return charaScaleFile.Entries
                .Where(x => x.GetName() == "CHARA_SCALE_INFO_LIST_BEG")
                .SelectMany(x => x.Children)
                .Select(x => x.ToClass<Charascale>())
                .ToArray();
        }
        public IOrgetimeTechnic[] GetOrgetimeTechnics() => new IOrgetimeTechnic[0];
        public IHackslashTechnic[] GetHackslashSkills() => null;
        public IHackslashCharaabilityConfig[] GetHackslashAbilities() => null;
        public void SaveHackslashCharaparam(IHackslashCharaparam[] h) { }
        public IHackslashCharaparam[] GetHackslashCharaparam() => null;
        public void SaveBattleCharaparam(IBattleCharaparam[] b) { }
        public IBattleCharaparam[] GetBattleCharaparam() => null;
        public ISkillconfig[] GetSkills() => null;

        // 디렉토리 구조 출력 메서드 (진단용)
        private void PrintDirectoryStructure(VirtualDirectory dir, string indent, int depth, int maxDepth)
        {
            if (dir == null || depth > maxDepth) return;

            string dirName = string.IsNullOrEmpty(dir.Name) ? "[ROOT]" : dir.Name;
            System.Diagnostics.Debug.WriteLine($"{indent}📁 {dirName}");

            // 파일 출력 (처음 10개만)
            if (dir.Files != null && dir.Files.Count > 0)
            {
                int fileCount = 0;
                foreach (var fileName in dir.Files.Keys)
                {
                    if (fileCount++ < 10)
                        System.Diagnostics.Debug.WriteLine($"{indent}  📄 {fileName}");
                }
                if (dir.Files.Count > 10)
                    System.Diagnostics.Debug.WriteLine($"{indent}  ... ({dir.Files.Count - 10} more files)");
            }

            // 하위 폴더 출력
            if (dir.Folders != null)
            {
                foreach (var subDir in dir.Folders)
                {
                    if (subDir != null)
                        PrintDirectoryStructure(subDir, indent + "  ", depth + 1, maxDepth);
                }
            }
        }

    }
}
