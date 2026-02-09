using ICN_T2.Logic.Level5.Archives.ARC0; // ARC0
using ICN_T2.Logic.Level5.Binary;       // CfgBin
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
            string type = isYokai ? "Yokai" : "NPC";
            System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] Loading {type} characterbase...");

            byte[]? data = GetFileContent("chara_base", "chara_base");
            if (data == null)
            {
                System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] ERROR: Failed to load chara_base.cfg.bin");
                return new CharaBase[0];
            }

            System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] Loaded chara_base.cfg.bin ({data.Length} bytes)");

            CfgBin cfg = new CfgBin();
            cfg.Open(data);
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

            System.Diagnostics.Debug.WriteLine($"[YW2.GetCharacterbase] Converted to {result.Length} {type} objects");

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
            if (Files.ContainsKey("chara_param"))
            {
                var gf = Files["chara_param"];
                var vf = gf.GetStream(); // Use GetStream()

                CfgBin cfg = new CfgBin();
                cfg.Open(vf.ByteContent ?? vf.ReadWithoutCaching());

                cfg.ReplaceEntry<Charaparam>("CHARA_PARAM_INFO_BEGIN", "CHARA_PARAM_INFO_", charaparams.Cast<Charaparam>().ToArray());
                vf.ByteContent = cfg.Save();

                if (CurrentProject != null)
                {
                    SaveToProject("chara_param", vf.ByteContent);
                }
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

        // [Refactored] CharScale
        public CharScale[] GetCharascale()
        {
            byte[]? data = GetFileContent("chara_base", "chara_base");
            if (data == null) return new CharScale[0];

            CfgBin cfg = new CfgBin(); cfg.Open(data);
            return cfg.Entries
                .Where(x => x.GetName() == "CHARA_SCALE_INFO_LIST_BEG")
                .SelectMany(x => x.Children)
                .Select(x => x.ToClass<CharScale>())
                .ToArray();
        }

        public void SaveCharascale(CharScale[] scales)
        {
            // Similar logic...
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
            return new BattleCommand[0];
        }

        public string[] GetMapWhoContainsEncounter() => new string[0];
        public (Definitions.EncountTable[], Definitions.EncountSlot[]) GetMapEncounter(string mapName) => (new Definitions.EncountTable[0], new Definitions.EncountSlot[0]);
        public void SaveMapEncounter(string mapName, Definitions.EncountTable[] encountTables, Definitions.EncountSlot[] encountCharas) { }
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