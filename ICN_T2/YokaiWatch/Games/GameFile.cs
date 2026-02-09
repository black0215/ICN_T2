using ICN_T2.Logic.Level5.Archives.ARC0;
using ICN_T2.Logic.VirtualFileSystem; // For SubMemoryStream
using System.IO;

namespace ICN_T2.YokaiWatch.Games
{
    public class GameFile
    {
        public ARC0? File; // The container (can be null if physical)
        public string? Path; // Path inside container or identification
        public string? PhysicalPath; // Absolute path on disk (if loose)
        public bool Exists;

        public GameFile()
        {
            // Default
        }

        public GameFile(ARC0 file, string path)
        {
            File = file;
            Path = path;
            Exists = true;
        }

        public GameFile(string physicalPath)
        {
            PhysicalPath = physicalPath;
            Path = System.IO.Path.GetFileName(physicalPath); // Fallback name
            Exists = true;
        }

        public GameFile(string physicalPath, string internalPath)
        {
            PhysicalPath = physicalPath;
            Path = internalPath;
            Exists = true;
        }

        public SubMemoryStream? GetStream()
        {
            if (!string.IsNullOrEmpty(PhysicalPath) && System.IO.File.Exists(PhysicalPath))
            {
                byte[] data = System.IO.File.ReadAllBytes(PhysicalPath);
                return new SubMemoryStream(data);
            }
            else if (File != null && File.Directory != null && Path != null)
            {
                return File.Directory.GetFileStreamFromFullPath(Path);
            }
            return null;
        }
    }
}