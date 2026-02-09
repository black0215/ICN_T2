using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;

namespace ICN_T2.Logic.VirtualFileSystem;

public class VirtualDirectory
{
    public string Name { get; set; }
    public VirtualDirectory? Parent { get; set; }
    public Dictionary<string, VirtualDirectory> Folders { get; private set; }
    public Dictionary<string, SubMemoryStream> Files { get; private set; }
    public Color Color { get; set; } = Color.Black;

    public VirtualDirectory() : this("") { }

    public VirtualDirectory(string name, VirtualDirectory? parent = null)
    {
        Name = name;
        Parent = parent;
        Folders = new Dictionary<string, VirtualDirectory>(StringComparer.OrdinalIgnoreCase);
        Files = new Dictionary<string, SubMemoryStream>(StringComparer.OrdinalIgnoreCase);
    }

    // -------------------------
    // Folder Methods
    // -------------------------

    public VirtualDirectory? GetFolder(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return Folders.TryGetValue(name, out var folder) ? folder : null;
    }

    public VirtualDirectory GetOrCreateFolder(string name)
    {
        if (Folders.TryGetValue(name, out var folder))
            return folder;

        var newFolder = new VirtualDirectory(name, this);
        Folders[name] = newFolder;
        return newFolder;
    }

    public void AddFolder(VirtualDirectory folder)
    {
        if (!Folders.ContainsKey(folder.Name))
        {
            folder.Parent = this;
            Folders[folder.Name] = folder;
        }
    }

    // -------------------------
    // File Methods
    // -------------------------

    public void AddFile(string name, SubMemoryStream stream)
    {
        Files[name] = stream;
    }

    public SubMemoryStream? GetFile(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return Files.TryGetValue(name, out var stream) ? stream : null;
    }

    // -------------------------
    // Path Navigation
    // -------------------------

    /// <summary>
    /// [호환성 유지] 원본과 동일하게 byte[] 반환 + 자동 로드
    /// </summary>
    public byte[] GetFileFromFullPath(string path)
    {
        var stream = GetFileStreamFromFullPath(path);
        if (stream == null)
            throw new FileNotFoundException(path + " not exist");

        // 원본 동작: ByteContent가 없으면 자동 로드
        if (stream.ByteContent == null)
            stream.Read();

        return stream.ByteContent!;
    }

    /// <summary>
    /// [추가] SubMemoryStream 직접 반환 (null 허용)
    /// </summary>
    public SubMemoryStream? GetFileStreamFromFullPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        path = path.Replace('\\', '/');
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0) return null;

        VirtualDirectory current = this;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var nextFolder = current.GetFolder(parts[i]);
            if (nextFolder == null) return null;
            current = nextFolder;
        }

        return current.GetFile(parts[^1]);
    }

    /// <summary>
    /// [복원] 읽기 전용 접근 - 캐싱하지 않음 (수정 플래그 방지)
    /// </summary>
    public byte[] GetFileDataReadOnly(string path)
    {
        var stream = GetFileStreamFromFullPath(path);
        if (stream == null)
            throw new FileNotFoundException(path + " not exist");

        // 이미 로드된 경우 그대로 반환
        if (stream.ByteContent != null)
            return stream.ByteContent;

        // 캐싱 없이 읽기
        return stream.ReadWithoutCaching();
    }

    /// <summary>
    /// [복원] 파일 존재 여부 확인
    /// </summary>
    public bool FileExists(string path)
    {
        return GetFileStreamFromFullPath(path) != null;
    }

    public VirtualDirectory GetFolderFromFullPathSafe(string path)
    {
        if (string.IsNullOrEmpty(path)) return this;

        path = path.Replace('\\', '/');
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        VirtualDirectory current = this;
        foreach (var part in parts)
        {
            current = current.GetOrCreateFolder(part);
        }

        return current;
    }

    public VirtualDirectory? GetFolderFromFullPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return this;

        path = path.Replace('\\', '/');
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        VirtualDirectory current = this;
        foreach (var part in parts)
        {
            var next = current.GetFolder(part);
            if (next == null) return null;
            current = next;
        }
        return current;
    }

    /// <summary>
    /// [복원] 모든 하위 폴더를 Dictionary로 반환
    /// </summary>
    public Dictionary<string, VirtualDirectory> GetAllFoldersAsDictionnary()
    {
        var result = new Dictionary<string, VirtualDirectory>();
        CollectFolders(result, "");
        return result;
    }

    private void CollectFolders(Dictionary<string, VirtualDirectory> dict, string basePath)
    {
        string currentPath;
        if (string.IsNullOrEmpty(basePath))
            currentPath = Name;
        else if (string.IsNullOrEmpty(Name))
            currentPath = basePath;
        else
            currentPath = basePath + "/" + Name;

        if (!string.IsNullOrEmpty(currentPath))
        {
            string key = currentPath + "/";
            dict.TryAdd(key, this);
        }

        foreach (var folder in Folders.Values)
            folder.CollectFolders(dict, currentPath);
    }

    // -------------------------
    // Utility
    // -------------------------

    public void SortAlphabetically()
    {
        var sortedFolders = Folders.OrderBy(k => k.Key).ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        Folders = sortedFolders;

        var sortedFiles = Files.OrderBy(k => k.Key).ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        Files = sortedFiles;

        foreach (var folder in Folders.Values)
            folder.SortAlphabetically();
    }

    public IEnumerable<string> EnumerateFilePaths(string currentPath = "")
    {
        foreach (var fileKey in Files.Keys)
        {
            yield return string.IsNullOrEmpty(currentPath) ? fileKey : currentPath + "/" + fileKey;
        }

        foreach (var folder in Folders.Values)
        {
            string nextPath = string.IsNullOrEmpty(currentPath) ? folder.Name : currentPath + "/" + folder.Name;
            foreach (var subPath in folder.EnumerateFilePaths(nextPath))
            {
                yield return subPath;
            }
        }
    }

    public List<KeyValuePair<string, SubMemoryStream>> GetFlatFileList()
    {
        var list = new List<KeyValuePair<string, SubMemoryStream>>();
        foreach (var path in EnumerateFilePaths())
        {
            var stream = GetFileStreamFromFullPath(path);
            if (stream != null)
            {
                list.Add(new KeyValuePair<string, SubMemoryStream>(path, stream));
            }
        }
        return list;
    }

    public void Print(int level = 0)
    {
        string indent = new string(' ', level * 2);
        Console.WriteLine(indent + "/" + Name);

        foreach (var f in Files.Keys)
            Console.WriteLine(indent + "  - " + f);

        foreach (var folder in Folders.Values)
            folder.Print(level + 1);
    }
}