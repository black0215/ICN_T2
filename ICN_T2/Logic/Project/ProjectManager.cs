using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ICN_T2.Logic.Project
{
    public static class ProjectManager
    {
        public static string ProjectsRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ICN_T2_Projects");

        public static void EnsureProjectsRoot()
        {
            if (!Directory.Exists(ProjectsRoot))
            {
                Directory.CreateDirectory(ProjectsRoot);
            }
        }

        public static Project CreateProject(string name, string baseGamePath, string description = "", string gameVersion = "YW2")
        {
            // 1. Validation
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Project name cannot be empty.");
            if (!string.IsNullOrEmpty(baseGamePath) && !Directory.Exists(baseGamePath)) throw new DirectoryNotFoundException("Base game path not found.");

            string safeName = SanitizeProjectName(name);
            string projectPath = Path.Combine(ProjectsRoot, safeName);

            if (Directory.Exists(projectPath)) throw new InvalidOperationException($"Project '{name}' already exists.");

            // 2. Structure Creation
            Directory.CreateDirectory(projectPath);
            Directory.CreateDirectory(Path.Combine(projectPath, "Source"));
            Directory.CreateDirectory(Path.Combine(projectPath, "Changes"));
            Directory.CreateDirectory(Path.Combine(projectPath, "Exports"));

            // 3. Project Instance
            var project = new Project
            {
                Name = name,
                BaseGamePath = baseGamePath,
                Description = description,
                GameVersion = gameVersion,
                CreatedDate = DateTime.Now,
                LastModified = DateTime.Now,
                RootPath = projectPath,
                IsBasedOnModded = !IsBaseGameVanilla(baseGamePath)
            };

            // 4. Save
            SaveProjectConfig(project);

            return project;
        }

        public static Project LoadProject(string projectFolder)
        {
            string jsonPath = Path.Combine(projectFolder, "project.json");
            if (!File.Exists(jsonPath)) throw new FileNotFoundException("project.json not found.");

            string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
            var project = JsonSerializer.Deserialize<Project>(json);
            if (project == null) throw new JsonException("Failed to deserialize project.json");

            project.RootPath = projectFolder;
            return project;
        }

        public static void SaveProjectConfig(Project project)
        {
            project.LastModified = DateTime.Now;
            string jsonPath = Path.Combine(project.RootPath, "project.json");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string json = JsonSerializer.Serialize(project, options);
            File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
        }

        public static List<Project> GetAvailableProjects()
        {
            var list = new List<Project>();
            if (!Directory.Exists(ProjectsRoot)) return list;

            foreach (var dir in Directory.GetDirectories(ProjectsRoot))
            {
                try
                {
                    var p = LoadProject(dir);
                    list.Add(p);
                }
                catch
                {
                    // Ignore invalid folders
                }
            }
            return list;
        }

        public static void DeleteProject(string projectFolder)
        {
            if (!Directory.Exists(projectFolder))
                throw new DirectoryNotFoundException("프로젝트 폴더를 찾을 수 없습니다.");

            // Verify it's actually inside our projects root
            string fullPath = Path.GetFullPath(projectFolder);
            string fullRoot = Path.GetFullPath(ProjectsRoot);
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("프로젝트 루트 외부의 폴더는 삭제할 수 없습니다.");

            Directory.Delete(projectFolder, true);
        }

        public static string SanitizeProjectName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        public static bool IsBaseGameVanilla(string baseGamePath)
        {
            // TODO: Implement Hash Database / Checksum Verification logic here.
            // For now, assume true (Vanilla) unless we implement the scanner.
            return true;
        }

        public static void ImportBaseGameFile(Project project, string relativePath)
        {
            // Logic for importing "Modified" base files into Project/Source
            string sourceFile = Path.Combine(project.BaseGamePath, relativePath);
            string destFile = Path.Combine(project.SourcePath, relativePath);

            if (File.Exists(sourceFile))
            {
                string? destDir = Path.GetDirectoryName(destFile);
                if (destDir != null && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(sourceFile, destFile, true);
            }
        }
    }
}
