using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BIMBotNavisPlugin.Dashboard.Config;
using Newtonsoft.Json;

namespace BIMBotNavisPlugin.Dashboard
{
    /// <summary>
    /// Lightweight per-project dashboard registry: one JSON file holding every dashboard
    /// definition (see <see cref="DashboardProject"/> for its location). Powers the
    /// "Update Existing Dashboard" list. Writes are atomic (temp file + replace) so a crash
    /// mid-save never leaves a truncated registry.
    /// </summary>
    public sealed class DashboardRegistry
    {
        private class RegistryFile
        {
            public int SchemaVersion { get; set; } = 1;
            public List<DashboardConfig> Dashboards { get; set; } = new List<DashboardConfig>();
        }

        private static JsonSerializerSettings JsonSettings => DashboardJson.Settings;

        public string Path { get; }

        public DashboardRegistry(string registryPath) { Path = registryPath; }

        public List<DashboardConfig> List() =>
            Load().Dashboards.OrderByDescending(d => d.UpdatedUtc).ToList();

        /// <summary>Find by id, or by name (case-insensitive). Null when absent.</summary>
        public DashboardConfig? Get(string idOrName)
        {
            if (string.IsNullOrWhiteSpace(idOrName)) return null;
            var all = Load().Dashboards;
            return all.FirstOrDefault(d => string.Equals(d.Id, idOrName, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(d => string.Equals(d.Name, idOrName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Insert or overwrite (by Id). Stamps UpdatedUtc.</summary>
        public DashboardConfig Save(DashboardConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.Name)) throw new Exception("Dashboard name is required.");
            var file = Load();
            var clash = file.Dashboards.FirstOrDefault(d =>
                d.Id != config.Id && string.Equals(d.Name, config.Name, StringComparison.OrdinalIgnoreCase));
            if (clash != null) throw new Exception($"A dashboard named '{config.Name}' already exists. Choose another name.");

            config.UpdatedUtc = DateTime.UtcNow;
            var i = file.Dashboards.FindIndex(d => d.Id == config.Id);
            if (i >= 0) file.Dashboards[i] = config; else file.Dashboards.Add(config);
            Write(file);
            return config;
        }

        /// <summary>Save a copy under a new id and name; the original is untouched.</summary>
        public DashboardConfig SaveAsCopy(DashboardConfig config, string newName)
        {
            var copy = config.Clone();
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Name = string.IsNullOrWhiteSpace(newName) ? UniqueName(config.Name + " (copy)") : newName;
            copy.CreatedUtc = DateTime.UtcNow;
            copy.LastExport = null; // a copy exports to its own files
            return Save(copy);
        }

        public bool Delete(string idOrName)
        {
            var file = Load();
            var target = Get(idOrName);
            if (target == null) return false;
            file.Dashboards.RemoveAll(d => d.Id == target.Id);
            Write(file);
            return true;
        }

        public string UniqueName(string baseName)
        {
            var names = new HashSet<string>(Load().Dashboards.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
            if (!names.Contains(baseName)) return baseName;
            for (int n = 2; ; n++)
                if (!names.Contains($"{baseName} {n}")) return $"{baseName} {n}";
        }

        private RegistryFile Load()
        {
            if (!File.Exists(Path)) return new RegistryFile();
            try
            {
                return JsonConvert.DeserializeObject<RegistryFile>(File.ReadAllText(Path), JsonSettings) ?? new RegistryFile();
            }
            catch (Exception ex)
            {
                throw new Exception($"The dashboard registry '{Path}' is unreadable ({ex.Message}). Fix or move the file, then retry.");
            }
        }

        private void Write(RegistryFile file) => AtomicFile.WriteAllText(Path, JsonConvert.SerializeObject(file, JsonSettings));
    }

    internal static class AtomicFile
    {
        public static void WriteAllText(string path, string text)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new System.Text.UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}
