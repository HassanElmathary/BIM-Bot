using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Manages per-project JSON data storage.
    /// Each Revit project gets its own folder under %APPDATA%/RevitMCP/ProjectData/{ProjectHash}/
    /// The AI can save/load/list/delete JSON data files to persist information between sessions.
    /// </summary>
    public static class ProjectDataService
    {
        private static readonly string BaseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RevitMCP", "ProjectData");

        /// <summary>
        /// Get the data folder for a specific project.
        /// Creates the folder if it doesn't exist.
        /// </summary>
        public static string GetProjectFolder(string projectFilePath)
        {
            var hash = GetProjectHash(projectFilePath);
            var folder = Path.Combine(BaseFolder, hash);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            // Save a metadata file with the original project path for reference
            var metaFile = Path.Combine(folder, "_project_info.json");
            if (!File.Exists(metaFile))
            {
                var meta = new JObject
                {
                    ["projectPath"] = projectFilePath,
                    ["projectName"] = Path.GetFileNameWithoutExtension(projectFilePath),
                    ["createdAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                File.WriteAllText(metaFile, meta.ToString(Formatting.Indented));
            }

            return folder;
        }

        /// <summary>
        /// Save JSON data with a key name.
        /// </summary>
        public static void SaveData(string projectFilePath, string key, JToken data)
        {
            var folder = GetProjectFolder(projectFilePath);
            var safeKey = SanitizeKey(key);
            var filePath = Path.Combine(folder, safeKey + ".json");

            var wrapper = new JObject
            {
                ["key"] = key,
                ["savedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["data"] = data
            };

            File.WriteAllText(filePath, wrapper.ToString(Formatting.Indented));
        }

        /// <summary>
        /// Load JSON data by key.
        /// </summary>
        public static JToken? LoadData(string projectFilePath, string key)
        {
            var folder = GetProjectFolder(projectFilePath);
            var safeKey = SanitizeKey(key);
            var filePath = Path.Combine(folder, safeKey + ".json");

            if (!File.Exists(filePath))
                return null;

            var content = File.ReadAllText(filePath);
            var wrapper = JObject.Parse(content);
            return wrapper["data"];
        }

        /// <summary>
        /// List all saved data keys for a project.
        /// </summary>
        public static List<DataFileInfo> ListData(string projectFilePath)
        {
            var folder = GetProjectFolder(projectFilePath);
            var files = Directory.GetFiles(folder, "*.json")
                .Where(f => !Path.GetFileName(f).StartsWith("_")) // Skip metadata files
                .ToList();

            var result = new List<DataFileInfo>();
            foreach (var file in files)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    var wrapper = JObject.Parse(content);
                    result.Add(new DataFileInfo
                    {
                        Key = wrapper["key"]?.ToString() ?? Path.GetFileNameWithoutExtension(file),
                        SavedAt = wrapper["savedAt"]?.ToString() ?? "",
                        FilePath = file,
                        SizeBytes = new FileInfo(file).Length
                    });
                }
                catch
                {
                    result.Add(new DataFileInfo
                    {
                        Key = Path.GetFileNameWithoutExtension(file),
                        SavedAt = "unknown",
                        FilePath = file,
                        SizeBytes = new FileInfo(file).Length
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Delete a saved data file by key.
        /// </summary>
        public static bool DeleteData(string projectFilePath, string key)
        {
            var folder = GetProjectFolder(projectFilePath);
            var safeKey = SanitizeKey(key);
            var filePath = Path.Combine(folder, safeKey + ".json");

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get project data folder path for display.
        /// </summary>
        public static string GetFolderPath(string projectFilePath)
        {
            return GetProjectFolder(projectFilePath);
        }

        /// <summary>
        /// Create a safe hash from the project file path.
        /// </summary>
        private static string GetProjectHash(string projectFilePath)
        {
            if (string.IsNullOrEmpty(projectFilePath))
                return "untitled_project";

            // Use project file name + a short hash for uniqueness
            var fileName = Path.GetFileNameWithoutExtension(projectFilePath);
            var safeName = SanitizeKey(fileName);

            // Add a short hash to handle files with same name in different folders
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(projectFilePath.ToLower()));
                var shortHash = BitConverter.ToString(hash).Replace("-", "").Substring(0, 8).ToLower();
                return $"{safeName}_{shortHash}";
            }
        }

        /// <summary>
        /// Sanitize a key for use as a filename.
        /// </summary>
        private static string SanitizeKey(string key)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(key.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return sanitized.Replace(' ', '_').ToLower().Trim('_');
        }
    }

    public class DataFileInfo
    {
        public string Key { get; set; } = "";
        public string SavedAt { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long SizeBytes { get; set; }
    }
}
