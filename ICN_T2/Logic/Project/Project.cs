using System;
using System.IO;
using System.Text.Json.Serialization;

namespace ICN_T2.Logic.Project
{
    public class Project
    {
        public string Name { get; set; } = "New Project";
        public string BaseGamePath { get; set; } = "";
        public string Description { get; set; } = "";
        public string GameVersion { get; set; } = "YW2";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public bool IsBasedOnModded { get; set; } = false;

        [JsonIgnore]
        public bool IsVanilla => !IsBasedOnModded;

        [JsonIgnore]
        public string RootPath { get; set; } = "";

        // Paths based on design document
        [JsonIgnore]
        public string SourcePath => Path.Combine(RootPath, "Source");

        [JsonIgnore]
        public string ChangesPath => Path.Combine(RootPath, "Changes");

        [JsonIgnore]
        public string ExportsPath => Path.Combine(RootPath, "Exports");

        public Project() { }

        public override string ToString() => Name;
    }
}
