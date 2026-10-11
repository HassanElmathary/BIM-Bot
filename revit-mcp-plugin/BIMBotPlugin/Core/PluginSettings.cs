using System;
using System.IO;
using Newtonsoft.Json;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// General plugin settings — persisted to disk.
    /// Controls optional features like automatic update checks.
    /// </summary>
    public class PluginSettings
    {
        public bool AutoCheckUpdates { get; set; } = true;

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins", "BIMBot", "plugin-settings.json");

        public void Save()
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static PluginSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonConvert.DeserializeObject<PluginSettings>(File.ReadAllText(SettingsPath))
                           ?? new PluginSettings();
            }
            catch { }
            return new PluginSettings();
        }
    }
}