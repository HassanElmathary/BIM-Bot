using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BIMBotNavisPlugin.Dashboard
{
    /// <summary>
    /// Where a project's dashboard state lives.
    ///   Saved NWF/NWD:  &lt;file folder&gt;\.bimbot\&lt;file name&gt;.dashboards.json  (travels with the project)
    ///   Unsaved:        %LOCALAPPDATA%\BIMBot\navis-dashboards\&lt;key&gt;\dashboards.json
    ///                   key = hash of the appended model file names, so re-appending the
    ///                   same models finds the same dashboards.
    /// Element keys and default export output follow the same split.
    /// </summary>
    public sealed class DashboardProject
    {
        public string DisplayName { get; }
        public string RegistryPath { get; }
        public string KeyMapPath { get; }
        public string DefaultOutputRoot { get; }
        public bool IsSaved { get; }

        private DashboardProject(string displayName, string registryPath, string keyMapPath, string outputRoot, bool saved)
        {
            DisplayName = displayName;
            RegistryPath = registryPath;
            KeyMapPath = keyMapPath;
            DefaultOutputRoot = outputRoot;
            IsSaved = saved;
        }

        public static DashboardProject Resolve(string documentFile, IEnumerable<string> modelFiles)
        {
            var models = modelFiles.Where(f => !string.IsNullOrWhiteSpace(f))
                                   .Select(f => Path.GetFileName(f).ToLowerInvariant())
                                   .OrderBy(f => f, StringComparer.Ordinal)
                                   .ToList();

            if (!string.IsNullOrWhiteSpace(documentFile) && Path.IsPathRooted(documentFile))
            {
                var dir = Path.GetDirectoryName(documentFile)!;
                var stem = Path.GetFileNameWithoutExtension(documentFile);
                var meta = Path.Combine(dir, ".bimbot");
                return new DashboardProject(
                    stem,
                    Path.Combine(meta, stem + ".dashboards.json"),
                    Path.Combine(meta, stem + ".element-keys.json"),
                    Path.Combine(dir, "BIM-Bot Dashboards"),
                    true);
            }

            var key = models.Count == 0 ? "untitled" : ShortHash(string.Join("|", models));
            var name = models.Count == 0 ? "Untitled" : Path.GetFileNameWithoutExtension(models[0]) + (models.Count > 1 ? $" +{models.Count - 1}" : "");
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BIMBot", "navis-dashboards", key);
            return new DashboardProject(
                name,
                Path.Combine(root, "dashboards.json"),
                Path.Combine(root, "element-keys.json"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BIM-Bot Dashboards", key),
                false);
        }

        internal static string ShortHash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var b = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                return BitConverter.ToString(b, 0, 6).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
