using ICN_T2.Logic.Level5.Archives.ARC0; // ARC0
using ICN_T2.Logic.Level5.Binary;       // CfgBin
using ICN_T2.Logic.Level5.Archives.XPCK; // XPCK
using ICN_T2.Logic.Level5.Text;         // T2bþ
using ICN_T2.Logic.VirtualFileSystem;   // VirtualDirectory
using ICN_T2.Tools;
using ICN_T2.YokaiWatch.Common;         // Tribes, Ranks etc.
using ICN_T2.YokaiWatch.Definitions;    // CharaBase, YokaiStats etc.
using ICN_T2.Logic.Project;             // Project Class
using ICN_T2.YokaiWatch.Games.YW2.Logic; // YokaiCharabase, NPCCharabase etc.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ICN_T2.YokaiWatch.Games.YW2
{
    using YW2Logic = ICN_T2.YokaiWatch.Games.YW2.Logic;
    public class YW2 : IGame
    {
        public string Name => "Yo-Kai Watch 2";
        public Dictionary<int, string> Tribes => ICN_T2.YokaiWatch.Common.Tribes.YW2;
        public Dictionary<int, string> FoodsType => ICN_T2.YokaiWatch.Common.FoodsType.YW2;
        public Dictionary<int, string> ScoutablesType => ICN_T2.YokaiWatch.Common.ScoutablesType.YW2;

        public ARC0 Game { get; set; }
        public ARC0 Language { get; set; }
        public string LanguageCode { get; set; }
        public string RomfsPath { get; private set; }
        public Project? CurrentProject { get; set; } // Added Project Context
        public Dictionary<string, GameFile> Files { get; set; }

        private Dictionary<string, string> _filePathCache = new Dictionary<string, string>();
        private readonly object _characterbaseCacheLock = new object();
        private CharaBase[]? _cachedYokaiCharacterbase;
        private CharaBase[]? _cachedNpcCharacterbase;

        // Cached character name map (hash -> name) to avoid re-parsing chara_text
        private Dictionary<int, string>? _cachedCharaNameMap;

        // Constructor overload for Project Mode
        public YW2(Project project, string language = "ko")
        {
            try
            {
                CurrentProject = project;
                LanguageCode = language;

                // [TEMPORARY] Pure Project 지원 비활성화 - BaseGamePath 필수
                if (string.IsNullOrEmpty(project.BaseGamePath))
                {
                    System.Diagnostics.Debug.WriteLine("[YW2] ERROR: Pure Project is not supported yet");
                    System.Diagnostics.Debug.WriteLine("[YW2] BaseGamePath is required for Modded Projects");
                    throw new InvalidOperationException(
                        "Pure Project는 아직 지원되지 않습니다.\n\n" +
                        "프로젝트 설정에서 'Base Game Path'를 설정하여 Modded Project로 전환하세요.\n\n" +
                        "Base Game Path는 순정 게임 파일이 있는 폴더입니다 (yw2_a.fa, yw2_lg_ko.fa 포함)."
                    );
                }

                System.Diagnostics.Debug.WriteLine("[YW2] Modded Project detected - using BaseGamePath");
                RomfsPath = project.BaseGamePath;
                System.Diagnostics.Debug.WriteLine($"[YW2] RomfsPath set to: {RomfsPath}");

                // Initialize archives if files exist
                LoadGameArchives();
                InitializeFiles();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YW2] Constructor Exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[YW2] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public YW2(string romfsPath, string language)
        {
            try
            {
                RomfsPath = romfsPath;
                LanguageCode = language;

                System.Diagnostics.Debug.WriteLine($"[YW2] Constructor called. RomfsPath: {RomfsPath}, Language: {LanguageCode}");

                LoadGameArchives();
                InitializeFiles();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YW2] Constructor Exception: {ex}");
                throw;
            }
        }

        private void LoadGameArchives()
        {
            // 1. Load main game file
            string gamePath = Path.Combine(RomfsPath, "yw2_a.fa");
            if (File.Exists(gamePath))
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(gamePath);
                    System.Diagnostics.Debug.WriteLine($"[YW2] Found game file: {gamePath} ({fileInfo.Length} bytes)");

                    if (fileInfo.Length == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YW2] ERROR: Game file is empty (0 bytes)");
                        throw new InvalidDataException("Game file is empty");
                    }

                    Game = new ARC0(new FileStream(gamePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    System.Diagnostics.Debug.WriteLine($"[YW2] Game archive loaded successfully");
                }
                catch (EndOfStreamException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YW2] ERROR: Failed to read game archive - file may be corrupted or incomplete");
                    System.Diagnostics.Debug.WriteLine($"[YW2] Exception: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[YW2] Stack trace: {ex.StackTrace}");
                    throw new InvalidDataException($"Game archive file is corrupted or incomplete: {gamePath}", ex);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YW2] ERROR: Failed to load game archive: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[YW2] Stack trace: {ex.StackTrace}");
                    throw;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[YW2] WARNING: Game file not found: {gamePath}");
            }

            // 2. Load language file (yw2_lg_ko.fa 등) — 캐릭터 기본정보에서 chara_text(이름) 로드에 필수
            string langFilePath = Path.Combine(RomfsPath, "yw2_lg_" + LanguageCode + ".fa");
            if (File.Exists(langFilePath))
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(langFilePath);
                    System.Diagnostics.Debug.WriteLine($"[YW2] Found language file: {langFilePath} ({fileInfo.Length} bytes)");

                    if (fileInfo.Length == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YW2] ERROR: Language file is empty (0 bytes)");
                        throw new InvalidDataException("Language file is empty");
                    }

                    Language = new ARC0(new FileStream(langFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    System.Diagnostics.Debug.WriteLine($"[YW2] Language archive loaded successfully");
                }
                catch (EndOfStreamException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YW2] ERROR: Failed to read language archive - file may be corrupted or incomplete");
                    System.Diagnostics.Debug.WriteLine($"[YW2] Exception: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[YW2] Stack trace: {ex.StackTrace}");
                    throw new InvalidDataException($"Language archive file is corrupted or incomplete: {langFilePath}", ex);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YW2] ERROR: Failed to load language archive: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[YW2] Stack trace: {ex.StackTrace}");
                    throw;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[YW2] WARNING: Language file not found: {langFilePath}");
                System.Diagnostics.Debug.WriteLine("[YW2] 캐릭터 기본정보(Character Info) 사용 시 yw2_lg_ko.fa 로드 필요 (chara_text)");
            }
        }

        private void InitializeFiles()
        {
            _filePathCache.Clear();
            if (Files == null) Files = new Dictionary<string, GameFile>();
            else Files.Clear();

            if (Game == null || Game.Directory == null) return;

            string langSuffix = "_" + (string.IsNullOrEmpty(LanguageCode) ? "ko" : LanguageCode) + ".cfg.bin";
            // 캐릭터 기본정보 로드 요구: chara_text ← yw2_lg_ko.fa (이름 매핑)
            var textFiles = new[]
            {
                "chara_text", "item_text", "battle_text", "skill_text",
                "chara_ability_text", "system_text", "addmembermenu_text"
            };

            // 텍스트 파일 검색 — 레거시와 동일하게 Language 아카이브(yw2_lg_ko.fa) 재귀 검색 우선
            foreach (var key in textFiles)
            {
                string fileName = key + langSuffix;

                // 1. Language 아카이브 재귀 검색 (최우선)
                if (Language != null)
                {
                    string langPath = SearchFileInDirectory(Language.Directory, fileName);
                    if (langPath != null)
                    {
                        Files[key] = new GameFile(Language, langPath);
                        System.Diagnostics.Debug.WriteLine($"[YW2] InitFiles: {key} -> Language:{langPath}");
                        continue;
                    }
                }

                // 2. Game 아카이브 재귀 검색 (폴백)
                string gamePath = SearchFileInDirectory(Game.Directory, fileName);
                if (gamePath != null)
                {
                    Files[key] = new GameFile(Game, gamePath);
                    System.Diagnostics.Debug.WriteLine($"[YW2] InitFiles: {key} -> Game:{gamePath}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[YW2] InitFiles: {key} -> NOT FOUND ({fileName})");
                }
            }

            // Config 파일 검색 (Vanilla) — 레거시와 동일하게 StartsWith 매칭
            var paramFiles = new[] { "chara_base", "chara_param", "skill_config", "item_config", "chara_ability" };
            foreach (var key in paramFiles)
            {
                // 1. data/res/param 폴더에서 StartsWith 검색
                var paramFolder = Game.Directory.GetFolderFromFullPathSafe("data/res/param");
                string matchedFile = paramFolder.Files.Keys.FirstOrDefault(
                    f => f.StartsWith(key, StringComparison.OrdinalIgnoreCase));

                if (matchedFile != null)
                {
                    Files[key] = new GameFile(Game, "data/res/param/" + matchedFile);
                    System.Diagnostics.Debug.WriteLine($"[YW2] InitFiles: {key} -> data/res/param/{matchedFile}");
                }
                else
                {
                    // 2. 재귀 검색 (StartsWith 방식)
                    string foundPath = SearchFileByPrefix(Game.Directory, key);
                    if (foundPath != null)
                    {
                        Files[key] = new GameFile(Game, foundPath);
                        System.Diagnostics.Debug.WriteLine($"[YW2] InitFiles: {key} -> {foundPath} (recursive)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[YW2] InitFiles: {key} -> NOT FOUND");
                    }
                }
            }

            // [FIX] Face Icon 폴더 등록 — 레거시와 동일하게 폴더명 "face_icon"을 재귀 탐색
            string faceIconFound = FindFolderPath(Game.Directory, "face_icon");
            if (!string.IsNullOrEmpty(faceIconFound))
            {
                Files["face_icon"] = new GameFile(Game, faceIconFound);
                System.Diagnostics.Debug.WriteLine($"[YW2] InitFiles: face_icon -> {faceIconFound}");
                // 디버그: 폴더 내 파일 목록 출력
                var faceDir = Game.Directory.GetFolderFromFullPathSafe(faceIconFound);
                if (faceDir != null)
                {
                    int count = faceDir.Files?.Count ?? 0;
                    System.Diagnostics.Debug.WriteLine($"[YW2] face_icon folder has {count} files");
                    if (count > 0 && count <= 10)
                    {
                        foreach (var f in faceDir.Files)
                            System.Diagnostics.Debug.WriteLine($"[YW2]   - {f.Key}");
                    }
                    else if (count > 10)
                    {
                        int shown = 0;
                        foreach (var f in faceDir.Files)
                        {
                            System.Diagnostics.Debug.WriteLine($"[YW2]   - {f.Key}");
                            if (++shown >= 5) break;
                        }
                        System.Diagnostics.Debug.WriteLine($"[YW2]   ... and {count - 5} more");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[YW2] InitFiles: face_icon folder NOT FOUND in archive");
            }

            // [Interceptor] Project Overrides
            // Check if any of these files exist in Project Changes Path
            if (CurrentProject != null && !string.IsNullOrEmpty(CurrentProject.ChangesPath))
            {
                // Helper to check and override
                CheckAndOverride(textFiles, ".cfg.bin"); // Suffix logic is messy for text files due to lang code

                // Re-iterate text files with correct suffix
                foreach (var key in textFiles)
                {
                    string fileName = key + langSuffix;

                    if (Files.ContainsKey(key))
                    {
                        // We know the internal path, e.g. "data/res/text/chara_text_ko.cfg.bin"
                        string? internalPath = Files[key].Path;
                        if (internalPath != null)
                        {
                            string overridePath = Path.Combine(CurrentProject.ChangesPath, internalPath);
                            if (File.Exists(overridePath))
                            {
                                Files[key] = new GameFile(overridePath, internalPath ?? "");
                                System.Diagnostics.Debug.WriteLine($"[YW2] Overriding {key} with {overridePath}");
                            }
                        }
                    }
                }

                foreach (var key in paramFiles)
                {
                    string fileName = key + ".cfg.bin";
                    if (Files.ContainsKey(key))
                    {
                        string? internalPath = Files[key].Path;
                        if (internalPath != null)
                        {
                            string overridePath = Path.Combine(CurrentProject.ChangesPath, internalPath);
                            if (File.Exists(overridePath))
                            {
                                Files[key] = new GameFile(overridePath, internalPath ?? "");
                                System.Diagnostics.Debug.WriteLine($"[YW2] Overriding {key} with {overridePath}");
                            }
                        }
                    }
                }
            }
        }

        private void CheckAndOverride(string[] keys, string suffix)
        {
            // Placeholder if needed logic extracted
        }

        // 재귀 파일 검색 (StartsWith 방식 — 레거시 FindFileRecursiveWithPath와 동일)
        private string SearchFileByPrefix(VirtualDirectory dir, string prefix, string currentPath = "")
        {
            if (dir == null) return null;

            // 캐시 체크
            if (string.IsNullOrEmpty(currentPath) && _filePathCache.ContainsKey(prefix))
                return _filePathCache[prefix];

            // 현재 폴더의 파일 중 prefix로 시작하는 것 검색
            var matchedFile = dir.Files.Keys.FirstOrDefault(
                f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            if (matchedFile != null)
            {
                string resultPath = string.IsNullOrEmpty(currentPath) ? matchedFile : currentPath + "/" + matchedFile;
                if (!_filePathCache.ContainsKey(prefix))
                {
                    _filePathCache[prefix] = resultPath;
                    System.Diagnostics.Debug.WriteLine($"[YW2] File Found (prefix): {prefix} -> {resultPath}");
                }
                return resultPath;
            }

            foreach (var subDir in dir.Folders)
            {
                string subPath = string.IsNullOrEmpty(currentPath) ? subDir.Key : currentPath + "/" + subDir.Key;
                string found = SearchFileByPrefix(subDir.Value, prefix, subPath);
                if (found != null)
                {
                    if (!_filePathCache.ContainsKey(prefix))
                        _filePathCache[prefix] = found;
                    return found;
                }
            }
            return null;
        }

        // 재귀 파일 검색 (정확한 파일명)
        private string SearchFileInDirectory(VirtualDirectory dir, string targetFileName, string currentPath = "")
        {
            if (dir == null) return null;

            // [Cache Check] 캐시에 경로가 있다면 즉시 반환
            if (string.IsNullOrEmpty(currentPath) && _filePathCache.ContainsKey(targetFileName))
            {
                // System.Diagnostics.Debug.WriteLine($"[YW2] Cache Hit: {targetFileName} -> {_filePathCache[targetFileName]}");
                return _filePathCache[targetFileName];
            }

            if (dir.Files.ContainsKey(targetFileName))
            {
                string resultPath = string.IsNullOrEmpty(currentPath) ? targetFileName : currentPath + "/" + targetFileName;

                // [Cache Save] 찾은 경로를 캐시에 저장
                if (!_filePathCache.ContainsKey(targetFileName))
                {
                    _filePathCache[targetFileName] = resultPath;
                    System.Diagnostics.Debug.WriteLine($"[YW2] File Found & Cached: {targetFileName} at {resultPath}");
                }
                return resultPath;
            }

            foreach (var subDir in dir.Folders)
            {
                string subPath = string.IsNullOrEmpty(currentPath) ? subDir.Key : currentPath + "/" + subDir.Key;
                string found = SearchFileInDirectory(subDir.Value, targetFileName, subPath);

                if (found != null)
                {
                    // 재귀 호출에서 찾았을 경우에도 캐시 저장
                    if (!_filePathCache.ContainsKey(targetFileName))
                    {
                        _filePathCache[targetFileName] = found;
                    }
                    return found;
                }
            }
            return null;
        }

        // 재귀 폴더 검색 (폴더명 기반 — 레거시 FindFolderPath와 동일)
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
                    if (subDir.Value == null) continue;

                    string subPath = string.IsNullOrEmpty(currentPath)
                        ? (subDir.Key ?? "")
                        : currentPath + "/" + (subDir.Key ?? "");

                    if (!string.IsNullOrEmpty(subDir.Key) && subDir.Key.Equals(targetFolderName, StringComparison.OrdinalIgnoreCase))
                    {
                        return subPath;
                    }

                    string found = FindFolderPath(subDir.Value, targetFolderName, subPath);
                    if (!string.IsNullOrEmpty(found))
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        public void Save(Action<int, int, string>? progressCallback = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[YW2] Save process started.");
                progressCallback?.Invoke(0, 100, "Initialize Save Process...");

                string tempPath = Path.Combine(Path.GetDirectoryName(RomfsPath), "temp");
                if (!Directory.Exists(tempPath)) Directory.CreateDirectory(tempPath);

                string tempGameFile = Path.Combine(tempPath, "yw2_a.fa");
                string destGameFile = Path.Combine(RomfsPath, "yw2_a.fa");

                // Step 1: Game Archive 저장
                progressCallback?.Invoke(10, 100, "Saving Game Archive...");
                System.Diagnostics.Debug.WriteLine($"[YW2] Saving Game Archive to temporary path: {tempGameFile}");

                // ARC0.Save가 콜백을 지원하지 않더라도, 여기서 단계를 나눔으로써 진행 상황을 알림
                Game.Save(tempGameFile);
                System.Diagnostics.Debug.WriteLine("[YW2] Game Archive saved.");

                // Step 2: Language Archive 저장
                if (Language != null)
                {
                    progressCallback?.Invoke(50, 100, "Saving Language Archive...");
                    string tempLangFile = Path.Combine(tempPath, "yw2_lg_" + LanguageCode + ".fa");
                    string destLangFile = Path.Combine(RomfsPath, "yw2_lg_" + LanguageCode + ".fa");

                    System.Diagnostics.Debug.WriteLine($"[YW2] Saving Language Archive to: {tempLangFile}");
                    Language.Save(tempLangFile);

                    Language.Close();
                    if (File.Exists(destLangFile)) File.Delete(destLangFile);
                    File.Move(tempLangFile, destLangFile);

                    // 언어 파일 재로드
                    Language = new ARC0(new FileStream(destLangFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    System.Diagnostics.Debug.WriteLine("[YW2] Language Archive updated and reloaded.");
                }

                // Step 3: Game Archive 이동 및 재로드
                progressCallback?.Invoke(90, 100, "Finalizing...");

                Game.Close();
                if (File.Exists(destGameFile)) File.Delete(destGameFile);
                File.Move(tempGameFile, destGameFile);

                Game = new ARC0(new FileStream(destGameFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                InitializeFiles();

                progressCallback?.Invoke(100, 100, "Save Complete.");
                System.Diagnostics.Debug.WriteLine("[YW2] Save process completed successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YW2] Error during Save: {ex.Message}");
                throw;
            }
        }

        public void SaveFullArchive(string exportPath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[YW2] SaveFullArchive called. Target: {exportPath}");

                // 1. Sync project changes to archive before saving
                SyncProjectChangesToArchive();

                string dir = Path.GetDirectoryName(exportPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // 2. ARC0 Save
                Game.Save(exportPath);
                System.Diagnostics.Debug.WriteLine("[YW2] Full archive saved successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YW2] SaveFullArchive Error: {ex.Message}");
                throw;
            }
        }

        private void SyncProjectChangesToArchive()
        {
            if (CurrentProject == null || string.IsNullOrEmpty(CurrentProject.ChangesPath)) return;

            string changesDir = CurrentProject.ChangesPath;
            if (!Directory.Exists(changesDir)) return;

            System.Diagnostics.Debug.WriteLine("[YW2] Syncing project changes to archive...");

            // Method 1: Sync from Files dictionary (explicitly tracked files)
            foreach (var gf in Files.Values)
            {
                if (!string.IsNullOrEmpty(gf.PhysicalPath) && !string.IsNullOrEmpty(gf.Path))
                {
                    var sms = Game.Directory.GetFileStreamFromFullPath(gf.Path);
                    if (sms != null)
                    {
                        sms.ByteContent = File.ReadAllBytes(gf.PhysicalPath);
                        System.Diagnostics.Debug.WriteLine($"[YW2] Synced tracked file: {gf.Path}");
                    }
                }
            }

            // Method 2: Sync from Changes folder (all files in the directory structure)
            var diskFiles = Directory.GetFiles(changesDir, "*", SearchOption.AllDirectories);
            foreach (var file in diskFiles)
            {
                string relPath = Path.GetRelativePath(changesDir, file).Replace('\\', '/');

                var sms = Game.Directory.GetFileStreamFromFullPath(relPath);
                if (sms != null)
                {
                    // Update if ByteContent is null (wasn't handled by Method 1)
                    if (sms.ByteContent == null)
                    {
                        sms.ByteContent = File.ReadAllBytes(file);
                        System.Diagnostics.Debug.WriteLine($"[YW2] Synced loose file: {relPath}");
                    }
                }
                else
                {
                    // Fuzzy matching for versioned files like chara_base_0.04c.cfg.bin
                    string dirName = Path.GetDirectoryName(relPath)?.Replace('\\', '/') ?? "";
                    string fileName = Path.GetFileName(relPath);

                    if (fileName.Contains("_"))
                    {
                        var folder = Game.Directory.GetFolderFromFullPathSafe(dirName);
                        if (folder != null)
                        {
                            // Try to match prefix before '_'
                            string prefix = fileName.Split('_')[0];
                            var targetFileName = folder.Files.Keys.FirstOrDefault(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                            if (targetFileName != null)
                            {
                                var targetSms = folder.GetFile(targetFileName);
                                if (targetSms != null && targetSms.ByteContent == null)
                                {
                                    targetSms.ByteContent = File.ReadAllBytes(file);
                                    System.Diagnostics.Debug.WriteLine($"[YW2] Synced fuzzy matched file: {fileName} -> {targetFileName}");
                                }
                            }
                        }
                    }
                }
            }
        }



        // --- Cached Name Map (Performance) ---

        /// <summary>
        /// Returns a cached dictionary mapping CRC32 hash -> character name.
        /// Parses chara_text only once, reuses on subsequent calls.
        /// </summary>
        public Dictionary<int, string> GetCharaNameMap()
        {
            if (_cachedCharaNameMap != null)
            {
                System.Diagnostics.Debug.WriteLine($"[Perf] YW2.GetCharaNameMap cache-hit: {_cachedCharaNameMap.Count} entries");
                return _cachedCharaNameMap;
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();
            var nameMap = new Dictionary<int, string>();

            try
            {
                if (Files == null || !Files.ContainsKey("chara_text"))
                {
                    System.Diagnostics.Debug.WriteLine("[YW2.GetCharaNameMap] chara_text not found in Files");
                    _cachedCharaNameMap = nameMap;
                    return nameMap;
                }

                var gf = Files["chara_text"];
                var vf = gf.GetStream();
                if (vf == null)
                {
                    _cachedCharaNameMap = nameMap;
                    return nameMap;
                }

                byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                if (data == null || data.Length == 0)
                {
                    _cachedCharaNameMap = nameMap;
                    return nameMap;
                }

                var textObj = new T2bþ(data);

                foreach (var kv in textObj.Nouns)
                {
                    if (kv.Value.Strings != null && kv.Value.Strings.Count > 0 && !string.IsNullOrEmpty(kv.Value.Strings[0].Text))
                    {
                        nameMap[kv.Key] = kv.Value.Strings[0].Text;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[Perf] YW2.GetCharaNameMap: parsed {nameMap.Count} names in {timer.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YW2.GetCharaNameMap] Error: {ex.Message}");
            }

            _cachedCharaNameMap = nameMap;
            return nameMap;
        }

        // --- Logic Implementation (IGame) ---

        // Helper to find file content (레거시와 동일: StartsWith 방식 폴백)
        private byte[]? GetFileContent(string key, string searchPrefix)
        {
            // 1. Files 딕셔너리에서 검색
            if (Files.ContainsKey(key))
            {
                var file = Files[key];
                var vf = file.GetStream();
                if (vf != null) return vf.ByteContent ?? vf.ReadWithoutCaching();
            }

            // 2. Fallback: 재귀 StartsWith 검색 (레거시 FindFileRecursiveWithPath와 동일)
            string foundPath = SearchFileByPrefix(Game.Directory, searchPrefix);
            if (foundPath != null)
            {
                var vf = Game.Directory.GetFileStreamFromFullPath(foundPath);
                if (vf != null) return vf.ByteContent ?? vf.ReadWithoutCaching();
            }
            return null;
        }

        // [Refactored] CharaBase — 구체 클래스(YokaiCharabase/NPCCharabase)로 변환
        public CharaBase[] GetCharacterbase(bool isYokai)
        {
            lock (_characterbaseCacheLock)
            {
                if (isYokai && _cachedYokaiCharacterbase != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Perf] YW2.GetCharacterbase(Yokai) cache-hit count={_cachedYokaiCharacterbase.Length}");
                    return _cachedYokaiCharacterbase;
                }

                if (!isYokai && _cachedNpcCharacterbase != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Perf] YW2.GetCharacterbase(NPC) cache-hit count={_cachedNpcCharacterbase.Length}");
                    return _cachedNpcCharacterbase;
                }
            }

            string type = isYokai ? "Yokai" : "NPC";
            System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] Loading {type} characterbase...");
            var totalTimer = System.Diagnostics.Stopwatch.StartNew();
            var sectionTimer = System.Diagnostics.Stopwatch.StartNew();

            byte[]? data = GetFileContent("chara_base", "chara_base");
            long fileLoadMs = sectionTimer.ElapsedMilliseconds;
            if (data == null)
            {
                System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] ERROR: Failed to load chara_base.cfg.bin");
                return new CharaBase[0];
            }

            System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] Loaded chara_base.cfg.bin ({data.Length} bytes)");

            CfgBin cfg = new CfgBin();
            sectionTimer.Restart();
            cfg.Open(data);
            long cfgOpenMs = sectionTimer.ElapsedMilliseconds;
            System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] CfgBin opened, {cfg.Entries.Count} entries found");

            string entryName = isYokai ? "CHARA_BASE_YOKAI_INFO_BEGIN" : "CHARA_BASE_INFO_BEGIN";
            System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] Looking for entry: {entryName}");

            var entry = cfg.Entries.FirstOrDefault(x => x.GetName() == entryName)
                     ?? cfg.Entries.FirstOrDefault(x => x.GetName().Contains("INFO_BEGIN"));
            if (entry == null)
            {
                System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] ERROR: Entry '{entryName}' not found");
                return new CharaBase[0];
            }

            System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] Found entry '{entry.GetName()}' with {entry.Children.Count} children");

            sectionTimer.Restart();
            var result = entry.Children.Select(x =>
            {
                try
                {
                    if (isYokai)
                    {
                        var y = x.ToClass<YokaiCharabase>();
                        // 수동 보정: Variables[0]=BaseHash, Variables[4]=NameHash
                        if (x.Variables.Count > 4)
                        {
                            y.BaseHash = (int)x.Variables[0].Value;
                            y.NameHash = (int)x.Variables[4].Value;
                        }
                        return (CharaBase)y;
                    }
                    else
                    {
                        return (CharaBase)x.ToClass<NPCCharabase>();
                    }
                }
                catch { return null; }
            }).Where(x => x != null).ToArray()!;
            long convertMs = sectionTimer.ElapsedMilliseconds;

            System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] Converted to {result.Length} {type} objects");
            System.Diagnostics.Debug.WriteLine($"[Perf] YW2.GetCharacterbase({type}) fileLoad={fileLoadMs}ms cfgOpen={cfgOpenMs}ms convert={convertMs}ms total={totalTimer.ElapsedMilliseconds}ms");

            lock (_characterbaseCacheLock)
            {
                if (isYokai)
                    _cachedYokaiCharacterbase = result;
                else
                    _cachedNpcCharacterbase = result;
            }

            return result;
        }

        public void SaveCharaBase(CharaBase[] charabases)
        {
            System.Diagnostics.Debug.WriteLine($"[YW2] SaveCharaBase called. Total entries: {charabases.Length}");

            // 구체 클래스로 분리 — DeclaredOnly 직렬화를 위해 OfType 사용
            NPCCharabase[] npc = charabases.OfType<NPCCharabase>().ToArray();
            YokaiCharabase[] yokai = charabases.OfType<YokaiCharabase>().ToArray();

            System.Diagnostics.Debug.WriteLine($"[YW2] Split CharaBase -> Yokai: {yokai.Length}, NPC: {npc.Length}");

            if (Files.ContainsKey("chara_base"))
            {
                var gf = Files["chara_base"];
                var vf = gf.GetStream(); // Use GetStream()

                if (vf == null)
                {
                    System.Diagnostics.Debug.WriteLine("[YW2] Error: chara_base file not found in virtual directory.");
                    return;
                }

                CfgBin cfg = new CfgBin();
                cfg.Open(vf.ByteContent ?? vf.ReadWithoutCaching());

                // NPC 저장 (구체 클래스로 직렬화)
                if (npc.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine("[YW2] Updating CHARA_BASE_INFO_...");
                    cfg.ReplaceEntry("CHARA_BASE_INFO_BEGIN", "CHARA_BASE_INFO_", npc);
                }

                // Yokai 저장 (구체 클래스로 직렬화)
                if (yokai.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine("[YW2] Updating CHARA_BASE_YOKAI_INFO_...");
                    cfg.ReplaceEntry("CHARA_BASE_YOKAI_INFO_BEGIN", "CHARA_BASE_YOKAI_INFO_", yokai);
                }

                // Update in-memory stream content
                // If this is a Physical File (Project Override), modification logic needs to be careful.
                vf.ByteContent = cfg.Save();
                System.Diagnostics.Debug.WriteLine("[YW2] chara_base.cfg.bin updated in memory.");

                // If Project Mode, Auto-Save to loose file?
                if (CurrentProject != null)
                {
                    SaveToProject("chara_base", vf.ByteContent);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[YW2] Warning: 'chara_base' key not found in Files dictionary.");
            }

            lock (_characterbaseCacheLock)
            {
                _cachedYokaiCharacterbase = null;
                _cachedNpcCharacterbase = null;
            }
        }

        private void SaveToProject(string key, byte[] data)
        {
            if (Files.ContainsKey(key))
            {
                var gf = Files[key];
                // If already physical, overwrite. If ARC0, create physical in Changes.
                string targetPath;
                if (!string.IsNullOrEmpty(gf.PhysicalPath))
                {
                    targetPath = gf.PhysicalPath;
                }
                else
                {
                    // Create Overriding file
                    string internalPath = gf.Path; // e.g. "data/res/param/chara_base.cfg.bin"
                    targetPath = Path.Combine(CurrentProject.ChangesPath, internalPath);

                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                    // Update GameFile to point to this new physical file
                    Files[key] = new GameFile(targetPath, gf.Path ?? "");
                }

                File.WriteAllBytes(targetPath, data);
                System.Diagnostics.Debug.WriteLine($"[YW2] Saved {key} to Project Changes: {targetPath}");
            }
        }

        // [Refactored] Charaparam -> YokaiStats (Mapping) — YW2 Logic Charaparam 필드 순서로 바이너리 매핑
        public YokaiStats[] GetCharaparam()
        {
            byte[]? data = GetFileContent("chara_param", "chara_param");
            if (data == null) return new YokaiStats[0];

            CfgBin cfg = new CfgBin(); cfg.Open(data);
            return cfg.Entries
                .Where(x => x.GetName() == "CHARA_PARAM_INFO_BEGIN")
                .SelectMany(x => x.Children)
                .Select(x => x.ToClass<Charaparam>())
                .ToArray();
        }

        public void SaveCharaparam(YokaiStats[] charaparams)
        {
            if (charaparams == null)
            {
                throw new ArgumentNullException(nameof(charaparams));
            }

            if (!Files.ContainsKey("chara_param"))
            {
                throw new FileNotFoundException("'chara_param' key not found in Files.");
            }

            var gf = Files["chara_param"];
            var vf = gf.GetStream();
            if (vf == null)
            {
                throw new FileNotFoundException("Unable to resolve 'chara_param' stream.");
            }

            CfgBin cfg = new CfgBin();
            cfg.Open(vf.ReadWithoutCaching());

            var concrete = charaparams.Select(x => x as Charaparam ?? new Charaparam
            {
                ParamHash = x.ParamHash,
                BaseHash = x.BaseHash,
                Tribe = x.Tribe,
                MinHP = x.MinHP,
                MinStrength = x.MinStrength,
                MinSpirit = x.MinSpirit,
                MinDefense = x.MinDefense,
                MinSpeed = x.MinSpeed,
                MaxHP = x.MaxHP,
                MaxStrength = x.MaxStrength,
                MaxSpirit = x.MaxSpirit,
                MaxDefense = x.MaxDefense,
                MaxSpeed = x.MaxSpeed,
                AttackHash = x.AttackHash,
                TechniqueHash = x.TechniqueHash,
                InspiritHash = x.InspiritHash,
                AttributeDamageFire = x.AttributeDamageFire,
                AttributeDamageIce = x.AttributeDamageIce,
                AttributeDamageEarth = x.AttributeDamageEarth,
                AttributeDamageLigthning = x.AttributeDamageLigthning,
                AttributeDamageWater = x.AttributeDamageWater,
                AttributeDamageWind = x.AttributeDamageWind,
                SoultimateHash = x.SoultimateHash,
                AbilityHash = x.AbilityHash,
                Money = x.Money,
                Experience = x.Experience,
                Drop1Hash = x.Drop1Hash,
                Drop1Rate = x.Drop1Rate,
                Drop2Hash = x.Drop2Hash,
                Drop2Rate = x.Drop2Rate,
                ExperienceCurve = x.ExperienceCurve,
                Quote1 = x.Quote1,
                Quote2 = x.Quote2,
                Quote3 = x.Quote3,
                BefriendQuote = x.BefriendQuote,
                EvolveOffset = x.EvolveOffset,
                EvolveParam = x.EvolveParam,
                EvolveLevel = x.EvolveLevel,
                EvolveCost = x.EvolveCost,
                MedaliumOffset = x.MedaliumOffset,
                ShowInMedalium = x.ShowInMedalium,
                ScoutableHash = x.ScoutableHash,
                FavoriteDonut = x.FavoriteDonut,
                Speed = x.Speed,
                Strongest = x.Strongest,
                Weakness = x.Weakness,
                CanFuse = x.CanFuse,
                WaitTime = x.WaitTime,
                EquipmentSlotsAmount = x.EquipmentSlotsAmount,
                BattleType = x.BattleType,
                Attitude = x.Attitude,
                AttackPercentage = x.AttackPercentage,
                TechniquePercentage = x.TechniquePercentage,
                InspiritPercentage = x.InspiritPercentage,
                GuardHash = x.GuardHash,
                GuardPercentage = x.GuardPercentage,
                BlasterSkill = x.BlasterSkill,
                BlasterAttack = x.BlasterAttack,
                BlasterSoultimate = x.BlasterSoultimate,
                BlasterMoveSlot1 = x.BlasterMoveSlot1,
                BlasterEarnLevelMoveSlot1 = x.BlasterEarnLevelMoveSlot1,
                BlasterMoveSlot2 = x.BlasterMoveSlot2,
                BlasterEarnLevelMoveSlot2 = x.BlasterEarnLevelMoveSlot2,
                BlasterMoveSlot3 = x.BlasterMoveSlot3,
                BlasterEarnLevelMoveSlot3 = x.BlasterEarnLevelMoveSlot3,
                BlasterMoveSlot4 = x.BlasterMoveSlot4,
                BlasterEarnLevelMoveSlot4 = x.BlasterEarnLevelMoveSlot4,
                DropOniOrbRate = x.DropOniOrbRate,
                DropOniOrb = x.DropOniOrb
            }).ToArray();

            cfg.ReplaceEntry("CHARA_PARAM_INFO_BEGIN", "CHARA_PARAM_INFO_", concrete);
            vf.ByteContent = cfg.Save();

            if (CurrentProject != null)
            {
                SaveToProject("chara_param", vf.ByteContent);
            }
        }

        // [Refactored] Evolution
        public Evolution[] GetCharaevolution()
        {
            byte[]? data = GetFileContent("chara_param", "chara_param"); // Evolution is usually in chara_param
            if (data == null) return new Evolution[0];

            CfgBin cfg = new CfgBin(); cfg.Open(data);
            return cfg.Entries
                .Where(x => x.GetName() == "CHARA_EVOLVE_INFO_BEGIN")
                .SelectMany(x => x.Children)
                .Select(x => x.ToClass<Evolution>())
                .ToArray();
        }

        public void SaveCharaevolution(Evolution[] evos)
        {
            if (Files.ContainsKey("chara_param"))
            {
                var gf = Files["chara_param"];
                var vf = gf.GetStream();

                CfgBin cfg = new CfgBin();
                cfg.Open(vf.ByteContent ?? vf.ReadWithoutCaching());

                cfg.ReplaceEntry("CHARA_EVOLVE_INFO_BEGIN", "CHARA_EVOLVE_INFO_", evos);
                vf.ByteContent = cfg.Save();

                if (CurrentProject != null)
                {
                    SaveToProject("chara_param", vf.ByteContent);
                }
            }
        }

        // [Refactored] CharScale - loads from separate chara_scale file
        public CharScale[] GetCharascale()
        {
            System.Diagnostics.Debug.WriteLine("[YW2.GetCharascale] Starting...");

            try
            {
                // CharScale is in a separate file, not in chara_base!
                var characterFolder = Game?.Directory?.GetFolderFromFullPathSafe("data/res/character");
                if (characterFolder == null)
                {
                    System.Diagnostics.Debug.WriteLine("[YW2.GetCharascale] ERROR: character folder not found");
                    return new CharScale[0];
                }

                var scaleFile = characterFolder.Files.Keys
                    .Where(x => x.StartsWith("chara_scale", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x)
                    .FirstOrDefault();

                if (scaleFile == null)
                {
                    System.Diagnostics.Debug.WriteLine("[YW2.GetCharascale] ERROR: No chara_scale file found");
                    System.Diagnostics.Debug.WriteLine($"[YW2.GetCharascale] Available files: {string.Join(", ", characterFolder.Files.Keys.Take(10))}");
                    return new CharScale[0];
                }

                System.Diagnostics.Debug.WriteLine($"[YW2.GetCharascale] Found file: {scaleFile}");

                byte[] data = characterFolder.GetFileDataReadOnly(scaleFile);
                if (data == null || data.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[YW2.GetCharascale] ERROR: file is empty");
                    return new CharScale[0];
                }

                CfgBin cfg = new CfgBin();
                cfg.Open(data);

                System.Diagnostics.Debug.WriteLine($"[YW2.GetCharascale] CfgBin opened, {cfg.Entries.Count} entries");

                var result = cfg.Entries
                    .Where(x => x.GetName() == "CHARA_SCALE_INFO_LIST_BEG")
                    .SelectMany(x => x.Children)
                    .Select(x => x.ToClass<CharScale>())
                    .ToArray();

                System.Diagnostics.Debug.WriteLine($"[YW2.GetCharascale] Returning {result.Length} objects");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YW2.GetCharascale] Exception: {ex.Message}");
                return new CharScale[0];
            }
        }

        public void SaveCharascale(CharScale[] scales)
        {
            if (scales == null)
            {
                throw new ArgumentNullException(nameof(scales));
            }

            var characterFolder = Game?.Directory?.GetFolderFromFullPath("data/res/character");
            if (characterFolder == null)
            {
                throw new DirectoryNotFoundException("Character folder '/data/res/character' not found.");
            }

            string? latestScaleFile = characterFolder.Files.Keys
                .Where(x => x.StartsWith("chara_scale", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(latestScaleFile))
            {
                throw new FileNotFoundException("No chara_scale file found under '/data/res/character'.");
            }

            byte[] sourceData = characterFolder.GetFileDataReadOnly(latestScaleFile);
            var cfg = new CfgBin();
            cfg.Open(sourceData);

            var concrete = scales.Select(x => x as Charascale ?? new Charascale
            {
                BaseHash = x.BaseHash,
                Scale1 = x.Scale1,
                Scale2 = x.Scale2,
                Scale3 = x.Scale3,
                Scale4 = x.Scale4,
                Scale5 = x.Scale5,
                Scale6 = x.Scale6,
                Scale7 = x.Scale7
            }).ToArray();

            cfg.ReplaceEntry("CHARA_SCALE_INFO_LIST_BEG", "CHARA_SCALE_INFO_", concrete);
            byte[] savedData = cfg.Save();

            var stream = characterFolder.GetFile(latestScaleFile);
            if (stream == null)
            {
                throw new FileNotFoundException($"Failed to resolve stream for '{latestScaleFile}'.");
            }

            stream.ByteContent = savedData;

            if (CurrentProject != null && CurrentProject.ChangesPath != null)
            {
                string relativePath = Path.Combine("data", "res", "character", latestScaleFile).Replace('\\', '/');
                string targetPath = Path.Combine(CurrentProject.ChangesPath, relativePath);
                string? targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                File.WriteAllBytes(targetPath, savedData);
            }
        }

        public ItemBase[] GetItems(string itemType)
        {
            byte[]? data = GetFileContent("item_config", "item_config");
            if (data == null) return new ItemBase[0];

            CfgBin cfg = new CfgBin(); cfg.Open(data);

            string[] targets = { "ITEM_EQUIPMENT_BEGIN", "ITEM_SOUL_BEGIN", "ITEM_CONSUME_BEGIN", "ITEM_IMPORTANT_BEGIN", "ITEM_CREATURE_BEGIN" };
            return cfg.Entries
                .Where(x => targets.Contains(x.GetName()))
                .SelectMany(x => x.Children)
                .Select(x => x.ToClass<ItemBase>())
                .ToArray();
        }

        public AbilityConfig[] GetAbilities()
        {
            byte[]? data = GetFileContent("chara_ability", "chara_ability");
            if (data == null) return new AbilityConfig[0];

            CfgBin cfg = new CfgBin(); cfg.Open(data);
            return cfg.Entries
                .Where(x => x.GetName().Contains("ABILITY_CONFIG"))
                .SelectMany(x => x.Children)
                .Select(x => x.ToClass<AbilityConfig>())
                .ToArray();
        }

        public SkillConfig[] GetSkills()
        {
            byte[]? data = GetFileContent("skill_config", "skill_config");
            if (data == null) return new SkillConfig[0];

            CfgBin cfg = new CfgBin(); cfg.Open(data);
            return cfg.Entries
                .Where(x => x.GetName().Contains("SKILL_CONFIG"))
                .SelectMany(x => x.Children)
                .Select(x => x.ToClass<SkillConfig>())
                .ToArray();
        }

        public BattleCommand[] GetBattleCommands()
        {
            try
            {
                if (Game?.Directory == null)
                {
                    System.Diagnostics.Trace.WriteLine("[YW2.GetBattleCommands] Game directory is null.");
                    return new BattleCommand[0];
                }

                byte[]? data = null;
                string loadedPath = "";

                // DFS Scanner (Legacy Behavior)
                // Stops at the first folder that contains a battle_command file.
                Action<VirtualDirectory> scanner = null;
                scanner = (d) =>
                {
                    // Check for file in current folder
                    var k = d.Files.Keys
                        .Where(x => x.StartsWith("battle_command", StringComparison.OrdinalIgnoreCase) &&
                                   !x.Contains("link", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(x => x) // Legacy used Max(), so we take the last one alphabetically in this folder
                        .FirstOrDefault();

                    if (k != null)
                    {
                        var file = d.GetFile(k);
                        if (file != null)
                        {
                            data = file.ByteContent ?? file.ReadWithoutCaching();
                            loadedPath = d.Name + "/" + k;
                            return; // Stop searching once found
                        }
                    }

                    // Recurse into subfolders if not found yet
                    // Note: VirtualDirectory.Folders values are the sub-directories.
                    foreach (var s in d.Folders.Values)
                    {
                        if (data == null) scanner(s);
                    }
                };

                scanner(Game.Directory);

                if (data == null)
                {
                    System.Diagnostics.Trace.WriteLine("[YW2.GetBattleCommands] No battle_command file found.");
                    return new BattleCommand[0];
                }

                System.Diagnostics.Debug.WriteLine($"[YW2.GetBattleCommands] Loaded file: {loadedPath}");

                CfgBin cfg = new CfgBin();
                cfg.Open(data);

                return cfg.Entries
                    .Where(x => x.GetName() == "BATTLE_COMMAND_INFO_BEGIN")
                    .SelectMany(x => x.Children)
                    .Select(x => (BattleCommand)x.ToClass<Battlecommand>())
                    .ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[YW2.GetBattleCommands] Failed: {ex.Message}");
                return new BattleCommand[0];
            }
        }

        private sealed class EncounterFileContext
        {
            public VirtualDirectory ContainerFolder { get; set; } = null!;
            public string EncounterFileName { get; set; } = "";
            public bool IsPck { get; set; }
            public string PckFileName { get; set; } = "";
            public XPCK? PckArchive { get; set; }
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public string SaveRelativePath { get; set; } = "";
            public string MapKey { get; set; } = "";
        }

        private static readonly string[] EncounterRootCandidates = new[] { "data/res/map", "data/map" };

        private void LogEncounter(string stage, string mapKey, string message)
        {
            string line = $"[Encounter][{stage}][{mapKey}] {message}";
            System.Diagnostics.Debug.WriteLine(line);

            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "encounter.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {line}{Environment.NewLine}");
            }
            catch
            {
                // Ignore file logging failures.
            }
        }

        private static bool IsEncounterConfigFileName(string mapKey, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string lower = fileName.ToLowerInvariant();
            string mapLower = (mapKey ?? "").ToLowerInvariant();

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

        private static ICN_T2.Logic.Level5.Binary.Entry? FindEncounterSlotBeginEntry(CfgBin cfg)
        {
            var direct = cfg.Entries.FirstOrDefault(x => x.GetName() == "ENCOUNT_CHARA_BEGIN");
            if (direct != null)
            {
                return direct;
            }

            var yokaiSpotNamed = cfg.Entries.FirstOrDefault(x =>
                x.GetName().IndexOf("YS_YOKAI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.GetName().IndexOf("YOKAISPOT", StringComparison.OrdinalIgnoreCase) >= 0);
            if (yokaiSpotNamed != null)
            {
                return yokaiSpotNamed;
            }

            return cfg.Entries.FirstOrDefault(x =>
                x.GetName().IndexOf("CHARA_BEGIN", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsYokaiSpotSlotEntry(ICN_T2.Logic.Level5.Binary.Entry slotEntry, string mapKey)
        {
            if (slotEntry.GetName().IndexOf("YS_YOKAI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                slotEntry.GetName().IndexOf("YOKAISPOT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (mapKey.Equals("yokaispot", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return slotEntry.Children.Any(c => c.Variables.Count >= 9);
        }

        private static string ResolveEntryPrefix(ICN_T2.Logic.Level5.Binary.Entry beginEntry, string fallbackPrefix)
        {
            if (beginEntry.Children.Count > 0)
            {
                string childName = beginEntry.Children[0].GetName();
                int end = childName.Length - 1;
                while (end >= 0 && char.IsDigit(childName[end]))
                {
                    end--;
                }

                if (end + 1 < childName.Length)
                {
                    return childName.Substring(0, end + 1);
                }
            }

            return fallbackPrefix;
        }

        private static void ValidateEncounterConfig(CfgBin cfg, string mapKey)
        {
            if (cfg.Entries == null || cfg.Entries.Count == 0)
            {
                throw new InvalidDataException("Encounter config is empty.");
            }

            var tableEntry = cfg.Entries.FirstOrDefault(x => x.GetName() == "ENCOUNT_TABLE_BEGIN");
            if (tableEntry == null)
            {
                throw new InvalidDataException("Missing ENCOUNT_TABLE_BEGIN.");
            }

            var slotEntry = FindEncounterSlotBeginEntry(cfg);
            if (slotEntry == null)
            {
                throw new InvalidDataException("Missing encounter slot begin entry.");
            }

            bool expectsYokaiSpot = mapKey.Equals("yokaispot", StringComparison.OrdinalIgnoreCase);
            bool isYokaiSpot = IsYokaiSpotSlotEntry(slotEntry, mapKey);

            if (expectsYokaiSpot && !isYokaiSpot)
            {
                throw new InvalidDataException("YokaiSpot schema expected but not found.");
            }

            if (isYokaiSpot && slotEntry.Children.Any(c => c.Variables.Count < 9))
            {
                throw new InvalidDataException("YokaiSpot entry has invalid variable count (expected 9).");
            }
        }

        private void SaveEncounterToProject(string relativePath, byte[] data)
        {
            if (CurrentProject == null || string.IsNullOrWhiteSpace(CurrentProject.ChangesPath))
            {
                return;
            }

            string normalized = relativePath.Replace('\\', '/').TrimStart('/');
            string targetPath = Path.Combine(CurrentProject.ChangesPath, normalized);
            string? targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            File.WriteAllBytes(targetPath, data);
            System.Diagnostics.Debug.WriteLine($"[YW2] Encounter override saved: {targetPath}");
        }

        private bool TryResolveEncounterFile(string mapKey, out EncounterFileContext? context)
        {
            context = null;
            if (string.IsNullOrWhiteSpace(mapKey))
            {
                LogEncounter("resolve", "(empty)", "Map key is empty.");
                return false;
            }

            mapKey = mapKey.Trim();
            LogEncounter("resolve", mapKey, $"start roots=[{string.Join(", ", EncounterRootCandidates)}]");

            // Backward compatibility: explicit "x.pck/y.cfg.bin" path key.
            if (mapKey.Contains(".pck/", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = mapKey.Split(new[] { ".pck/" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    string pckPath = parts[0] + ".pck";
                    string internalPath = parts[1];
                    var pckStream = Game.Directory.GetFileStreamFromFullPath(pckPath.TrimStart('/'));
                    if (pckStream != null)
                    {
                        byte[] pckData = pckStream.ByteContent ?? pckStream.ReadWithoutCaching();
                        XPCK archive = new XPCK(pckData);
                        var internalFile = archive.Directory.GetFile(internalPath);
                        if (internalFile != null)
                        {
                            byte[] data = internalFile.ByteContent ?? internalFile.ReadWithoutCaching();
                            string relPckPath = pckPath.TrimStart('/');
                            string? folderPath = Path.GetDirectoryName(relPckPath)?.Replace('\\', '/');
                            if (!string.IsNullOrEmpty(folderPath))
                            {
                                var container = Game.Directory.GetFolderFromFullPath(folderPath);
                                if (container != null)
                                {
                                    context = new EncounterFileContext
                                    {
                                        ContainerFolder = container,
                                        EncounterFileName = internalPath,
                                        IsPck = true,
                                        PckFileName = Path.GetFileName(relPckPath),
                                        PckArchive = archive,
                                        Data = data,
                                        SaveRelativePath = relPckPath,
                                        MapKey = mapKey
                                    };
                                    LogEncounter("resolve", mapKey, $"resolved explicit pck path '{relPckPath}' -> '{internalPath}'");
                                    return true;
                                }

                                LogEncounter("resolve", mapKey, $"explicit pck path container folder missing: '{folderPath}'");
                            }

                            LogEncounter("resolve", mapKey, $"explicit pck path has invalid folder: '{relPckPath}'");
                        }

                        LogEncounter("resolve", mapKey, $"explicit pck path missing internal file '{internalPath}'");
                    }

                    LogEncounter("resolve", mapKey, $"explicit pck stream not found: '{pckPath}'");
                }
                else
                {
                    LogEncounter("resolve", mapKey, "explicit pck key format invalid.");
                }
            }

            foreach (string root in EncounterRootCandidates)
            {
                var rootFolder = Game.Directory.GetFolderFromFullPath(root);
                if (rootFolder == null)
                {
                    LogEncounter("resolve", mapKey, $"root not found: '{root}'");
                    continue;
                }

                var mapFolder = rootFolder.GetFolder(mapKey);
                if (mapFolder != null)
                {
                    string? directFile = mapFolder.Files.Keys
                        .Where(f => IsEncounterConfigFileName(mapKey, f))
                        .OrderByDescending(f => f)
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(directFile))
                    {
                        context = new EncounterFileContext
                        {
                            ContainerFolder = mapFolder,
                            EncounterFileName = directFile,
                            IsPck = false,
                            Data = mapFolder.GetFileDataReadOnly(directFile),
                            SaveRelativePath = $"{root}/{mapKey}/{directFile}",
                            MapKey = mapKey
                        };
                        LogEncounter("resolve", mapKey, $"resolved direct file '{root}/{mapKey}/{directFile}'");
                        return true;
                    }

                    var pckNames = mapFolder.Files.Keys
                        .Where(f => f.EndsWith(".pck", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    if (pckNames.Length == 0)
                    {
                        LogEncounter("resolve", mapKey, $"no pck files in '{root}/{mapKey}'");
                    }

                    foreach (string pckName in pckNames)
                    {
                        try
                        {
                            byte[] pckData = mapFolder.GetFileDataReadOnly(pckName);
                            XPCK archive = new XPCK(pckData);
                            string? encounterFile = archive.Directory.Files.Keys
                                .Where(f => IsEncounterConfigFileName(mapKey, f))
                                .OrderByDescending(f => f)
                                .FirstOrDefault();

                            if (!string.IsNullOrEmpty(encounterFile))
                            {
                                var encounterSms = archive.Directory.GetFile(encounterFile);
                                if (encounterSms != null)
                                {
                                    context = new EncounterFileContext
                                    {
                                        ContainerFolder = mapFolder,
                                        EncounterFileName = encounterFile,
                                        IsPck = true,
                                        PckFileName = pckName,
                                        PckArchive = archive,
                                        Data = encounterSms.ByteContent ?? encounterSms.ReadWithoutCaching(),
                                        SaveRelativePath = $"{root}/{mapKey}/{pckName}",
                                        MapKey = mapKey
                                    };
                                    LogEncounter("resolve", mapKey, $"resolved pck file '{root}/{mapKey}/{pckName}' -> '{encounterFile}'");
                                    return true;
                                }
                            }
                            else
                            {
                                LogEncounter("resolve", mapKey, $"pck '{pckName}' has no matching encounter cfg");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogEncounter("resolve", mapKey, $"Failed reading pck '{pckName}': {ex.Message}");
                        }
                    }
                }
                else
                {
                    LogEncounter("resolve", mapKey, $"map folder not found under root '{root}'");
                }

                string? rootFile = rootFolder.Files.Keys
                    .Where(f => IsEncounterConfigFileName(mapKey, f))
                    .OrderByDescending(f => f)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(rootFile))
                {
                    context = new EncounterFileContext
                    {
                        ContainerFolder = rootFolder,
                        EncounterFileName = rootFile,
                        IsPck = false,
                        Data = rootFolder.GetFileDataReadOnly(rootFile),
                        SaveRelativePath = $"{root}/{rootFile}",
                        MapKey = mapKey
                    };
                    LogEncounter("resolve", mapKey, $"resolved root-level file '{root}/{rootFile}'");
                    return true;
                }
            }

            LogEncounter("resolve", mapKey, "encounter config not found in any candidate location.");
            return false;
        }

        public string[] GetMapWhoContainsEncounter()
        {
            var maps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in EncounterRootCandidates)
            {
                var rootFolder = Game.Directory.GetFolderFromFullPath(root);
                if (rootFolder == null)
                {
                    continue;
                }

                foreach (var folder in rootFolder.Folders.Values)
                {
                    bool hasDirect = folder.Files.Keys.Any(f => IsEncounterConfigFileName(folder.Name, f));
                    bool hasPckEncounter = false;

                    if (!hasDirect)
                    {
                        foreach (string pckName in folder.Files.Keys.Where(f => f.EndsWith(".pck", StringComparison.OrdinalIgnoreCase)))
                        {
                            try
                            {
                                byte[] pckData = folder.GetFileDataReadOnly(pckName);
                                XPCK archive = new XPCK(pckData);
                                if (archive.Directory.Files.Keys.Any(f => IsEncounterConfigFileName(folder.Name, f)))
                                {
                                    hasPckEncounter = true;
                                    break;
                                }
                            }
                            catch
                            {
                                // Keep scanning other files.
                            }
                        }
                    }

                    if (hasDirect || hasPckEncounter)
                    {
                        maps.Add(folder.Name);
                    }
                }

                foreach (string fileName in rootFolder.Files.Keys)
                {
                    if (fileName.IndexOf("yokaispot", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        maps.Add("yokaispot");
                        continue;
                    }

                    int idx = fileName.IndexOf("_enc_", StringComparison.OrdinalIgnoreCase);
                    if (idx > 0)
                    {
                        maps.Add(fileName.Substring(0, idx));
                    }
                }
            }

            return maps.OrderBy(x => x).ToArray();
        }

        public (Definitions.EncountTable[], Definitions.EncountSlot[]) GetMapEncounter(string mapName)
        {
            try
            {
                LogEncounter("load-start", mapName, "loading encounter");

                if (!TryResolveEncounterFile(mapName, out var ctx) || ctx == null)
                {
                    throw new FileNotFoundException(
                        $"Encounter config not found for map '{mapName}'. roots=[{string.Join(", ", EncounterRootCandidates)}]");
                }

                LogEncounter("load-resolve", mapName, ctx.IsPck
                    ? $"using pck '{ctx.PckFileName}' file '{ctx.EncounterFileName}'"
                    : $"using file '{ctx.EncounterFileName}'");

                var cfg = new CfgBin();
                cfg.Open(ctx.Data);
                ValidateEncounterConfig(cfg, mapName);

                var tableBegin = cfg.Entries.First(x => x.GetName() == "ENCOUNT_TABLE_BEGIN");
                var slotBegin = FindEncounterSlotBeginEntry(cfg)!;
                bool isYokaiSpot = IsYokaiSpotSlotEntry(slotBegin, mapName);

                var tables = tableBegin.Children
                    .Select(x => (Definitions.EncountTable)x.ToClass<YW2Logic.EncountTable>())
                    .ToArray();

                Definitions.EncountSlot[] slots = isYokaiSpot
                    ? slotBegin.Children.Select(x => (Definitions.EncountSlot)x.ToClass<YW2Logic.YokaiSpotChara>()).ToArray()
                    : slotBegin.Children.Select(x => (Definitions.EncountSlot)x.ToClass<YW2Logic.EncountChara>()).ToArray();

                foreach (var table in tables)
                {
                    if (table.EncountOffsets == null || table.EncountOffsets.Length == 0)
                    {
                        table.EncountOffsets = new int[6];
                    }

                    for (int i = 0; i < table.EncountOffsets.Length; i++)
                    {
                        if (table.EncountOffsets[i] < -1 || table.EncountOffsets[i] >= slots.Length)
                        {
                            table.EncountOffsets[i] = -1;
                        }
                    }
                }

                LogEncounter("load-finish", mapName, $"tables={tables.Length}, slots={slots.Length}, yokaispot={isYokaiSpot}");
                return (tables, slots);
            }
            catch (Exception ex)
            {
                LogEncounter("load-error", mapName, $"{ex.GetType().Name}: {ex.Message}");
                return (Array.Empty<Definitions.EncountTable>(), Array.Empty<Definitions.EncountSlot>());
            }
        }

        private (Definitions.EncountTable[], Definitions.EncountSlot[]) ParsePTree(byte[] data)
        {
            var cfg = new CfgBin();
            cfg.Open(data);

            var tables = new List<Definitions.EncountTable>();
            var slots = new List<Definitions.EncountSlot>();

            // Find the root PTREE entry
            var root = cfg.Entries.FirstOrDefault(x => x.GetName() == "PTREE");
            if (root == null) return (new Definitions.EncountTable[0], new Definitions.EncountSlot[0]);

            // Strategy: Flatten ALL values (Ints and Hash of Strings) from the PTREE structure
            var allValues = new List<int>();
            Action<ICN_T2.Logic.Level5.Binary.Entry> collectValues = null;
            collectValues = (e) =>
            {
                if (e.GetName().Contains("PTVAL"))
                {
                    foreach (var v in e.Variables)
                    {
                        if (v.Value is int i)
                        {
                            allValues.Add(i);
                        }
                        else if (v.Value is long l)
                        {
                            allValues.Add((int)l);
                        }
                        else if (v.Value is short s)
                        {
                            allValues.Add((int)s);
                        }
                        else if (v.Value is byte b)
                        {
                            allValues.Add((int)b);
                        }
                        else if (v.Value is string str)
                        {
                            // Convert string to CRC32 hash (assumed to be ParamHash/NameHash)
                            if (!string.IsNullOrEmpty(str))
                            {
                                allValues.Add((int)ICN_T2.Tools.Crc32.Compute(System.Text.Encoding.GetEncoding("Shift-JIS").GetBytes(str)));
                            }
                        }
                        else if (v.Value is ICN_T2.Logic.Level5.Binary.OffsetTextPair pair)
                        {
                            // Convert OffsetTextPair content to CRC32 hash
                            if (!string.IsNullOrEmpty(pair.Text))
                            {
                                allValues.Add((int)ICN_T2.Tools.Crc32.Compute(System.Text.Encoding.GetEncoding("Shift-JIS").GetBytes(pair.Text)));
                            }
                        }
                    }
                }
                foreach (var c in e.Children) collectValues(c);
            };

            collectValues(root);

            System.Diagnostics.Debug.WriteLine($"[YW2] Flattened PTREE values (mixed): {allValues.Count}");
            if (allValues.Count > 0 && allValues.Count < 50)
            {
                System.Diagnostics.Debug.WriteLine($"[YW2] Values (mixed): {string.Join(", ", allValues)}");
            }

            if (allValues.Count == 0) return (new Definitions.EncountTable[0], new Definitions.EncountSlot[0]);

            // Create ONE dummy table
            var table = new YW2Logic.EncountTable();
            table.EncountConfigHash = allValues.Count > 0 ? allValues[0] : 0;
            tables.Add(table);

            // Heuristic Parsing with mixed values (Ints and Hashes)

            // Expected Pattern:
            // [EncountHash (StringHash)], [Unk], [Level (Int)], ...

            // Let's assume the Hash is large (CRC32 usually represents big numbers as int) or string-derived.

            for (int i = 0; i < allValues.Count - 2; i++)
            {
                int val1 = allValues[i];   // Potential Hash
                int val2 = allValues[i + 1]; // Potential Level or Unk
                int val3 = allValues[i + 2]; // Potential Level?

                // Identify if val1 looks like a Hash (non-small integer, often negative in signed int)
                // And check if val2 or val3 is a reasonable Level (1-99).

                // Usually Hash is not small (e.g. > 1000 or < -1000).
                // Level is 1-99.

                bool isHash = (val1 > 1000 || val1 < -1000);
                bool isLevel2 = (val2 > 0 && val2 <= 99);
                bool isLevel3 = (val3 > 0 && val3 <= 99);

                if (isHash && (isLevel2 || isLevel3))
                {
                    var slot = new YW2Logic.EncountChara();
                    slot.ParamHash = val1;
                    slot.Level = isLevel2 ? val2 : val3;

                    int offset = i * 0x10;
                    slot.Offset = offset;

                    // Link to table
                    bool linked = false;
                    for (int k = 0; k < table.EncountOffsets.Length; k++)
                    {
                        if (table.EncountOffsets[k] == 0)
                        {
                            table.EncountOffsets[k] = offset;
                            linked = true;
                            break;
                        }
                    }
                    if (!linked)
                    {
                        // Expand if needed (though fixed size 6 limit in logic)
                        // Just overwrite last one cyclicly to show valid linkage for testing
                        table.EncountOffsets[table.EncountOffsets.Length - 1] = offset;
                    }

                    slots.Add(slot);

                    // Advance stride
                    // If we matched Hash, Level... it's likely part of a block.
                    i += isLevel2 ? 1 : 2;
                }
            }

            return (tables.ToArray(), slots.ToArray());
        }

        private void LogEntryRecursive(ICN_T2.Logic.Level5.Binary.Entry entry, string indent)
        {
            if (indent.Length > 100) return; // Prevent too deep recursion logging

            string extraInfo = "";
            if (entry.GetName().Contains("PTVAL"))
            {
                extraInfo = " Vars: " + string.Join(", ", entry.Variables.Select(v => v.Value));
            }

            System.Diagnostics.Debug.WriteLine($"[YW2] {indent}Entry: {entry.GetName()}, Children: {entry.Children.Count}{extraInfo}");
            foreach (var child in entry.Children)
            {
                LogEntryRecursive(child, indent + "  ");
            }
        }

        public void SaveMapEncounter(string mapName, Definitions.EncountTable[] encountTables, Definitions.EncountSlot[] encountCharas)
        {
            try
            {
                LogEncounter("save-start", mapName, $"tables={encountTables?.Length ?? 0}, slots={encountCharas?.Length ?? 0}");

                if (!TryResolveEncounterFile(mapName, out var ctx) || ctx == null)
                {
                    throw new FileNotFoundException(
                        $"Encounter config not found for map '{mapName}'. roots=[{string.Join(", ", EncounterRootCandidates)}]");
                }

                LogEncounter("save-resolve", mapName, ctx.IsPck
                    ? $"using pck '{ctx.PckFileName}' file '{ctx.EncounterFileName}'"
                    : $"using file '{ctx.EncounterFileName}'");

                var cfg = new CfgBin();
                cfg.Open(ctx.Data);
                ValidateEncounterConfig(cfg, mapName);

                var tableBegin = cfg.Entries.First(x => x.GetName() == "ENCOUNT_TABLE_BEGIN");
                var slotBegin = FindEncounterSlotBeginEntry(cfg)!;
                bool isYokaiSpot = IsYokaiSpotSlotEntry(slotBegin, mapName);

                var concreteTables = (encountTables ?? Array.Empty<Definitions.EncountTable>())
                    .Select(t => new YW2Logic.EncountTable
                    {
                        EncountConfigHash = t?.EncountConfigHash ?? 0,
                        EncountOffsets = t?.EncountOffsets ?? new int[6]
                    })
                    .ToArray();

                cfg.ReplaceEntry(tableBegin.GetName(), ResolveEntryPrefix(tableBegin, "ENCOUNT_TABLE_INFO_"), concreteTables);

                if (isYokaiSpot)
                {
                    var concreteSlots = (encountCharas ?? Array.Empty<Definitions.EncountSlot>())
                        .Select(s =>
                        {
                            if (s is YW2Logic.YokaiSpotChara ys) return ys;
                            return new YW2Logic.YokaiSpotChara
                            {
                                ParamHash = s?.ParamHash ?? 0,
                                Level = s?.Level ?? 0,
                                Unk3 = s?.MaxLevel ?? 0,
                                Unk8 = s?.Weight ?? 0
                            };
                        })
                        .ToArray();

                    cfg.ReplaceEntry(slotBegin.GetName(), ResolveEntryPrefix(slotBegin, "YS_YOKAI_INFO_"), concreteSlots);
                }
                else
                {
                    var concreteSlots = (encountCharas ?? Array.Empty<Definitions.EncountSlot>())
                        .Select(s =>
                        {
                            if (s is YW2Logic.EncountChara ec) return ec;
                            return new YW2Logic.EncountChara
                            {
                                ParamHash = s?.ParamHash ?? 0,
                                Level = s?.Level ?? 0,
                                Unk1 = s?.MaxLevel ?? 0,
                                Unk2 = s?.Weight ?? 0
                            };
                        })
                        .ToArray();

                    cfg.ReplaceEntry(slotBegin.GetName(), ResolveEntryPrefix(slotBegin, "ENCOUNT_CHARA_INFO_"), concreteSlots);
                }

                byte[] newData = cfg.Save();
                if (ctx.IsPck)
                {
                    var encounterFile = ctx.PckArchive!.Directory.GetFile(ctx.EncounterFileName);
                    if (encounterFile == null)
                    {
                        throw new InvalidDataException($"Encounter file '{ctx.EncounterFileName}' missing in pck.");
                    }

                    encounterFile.ByteContent = newData;
                    byte[] newPckData = ctx.PckArchive.Save();

                    var pckStream = ctx.ContainerFolder.GetFile(ctx.PckFileName);
                    if (pckStream == null)
                    {
                        throw new FileNotFoundException($"PCK file '{ctx.PckFileName}' not found in container.");
                    }

                    pckStream.ByteContent = newPckData;
                    SaveEncounterToProject(ctx.SaveRelativePath, newPckData);
                }
                else
                {
                    var target = ctx.ContainerFolder.GetFile(ctx.EncounterFileName);
                    if (target == null)
                    {
                        throw new FileNotFoundException($"Encounter file '{ctx.EncounterFileName}' not found.");
                    }

                    target.ByteContent = newData;
                    SaveEncounterToProject(ctx.SaveRelativePath, newData);
                }

                LogEncounter("save-finish", mapName, "save complete");
            }
            catch (Exception ex)
            {
                LogEncounter("save-error", mapName, $"{ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        public (ShopConfig[], ShopConfig[]) GetShop(string shopName) => (new ShopConfig[0], new ShopConfig[0]);
        public void SaveShop(string shopName, ShopConfig[] shopConfigs, ShopConfig[] shopValidConditions) { }

        public YokaiStats[] GetBattleCharaparam() => new YokaiStats[0];
        public void SaveBattleCharaparam(YokaiStats[] battleCharaparams) { }
        public BustersStats[] GetHackslashCharaparam() => new BustersStats[0];
        public void SaveHackslashCharaparam(BustersStats[] hackslashCharaparams) { }
        public BustersAbility[] GetHackslashAbilities() => new BustersAbility[0];
        public BustersSkill[] GetHackslashSkills() => new BustersSkill[0];
        public OniTimeSkill[] GetOrgetimeTechnics() => new OniTimeSkill[0];
    }
}
