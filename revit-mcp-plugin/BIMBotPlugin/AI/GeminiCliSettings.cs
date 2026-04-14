using System;
using System.IO;
using Newtonsoft.Json;

namespace BIMBotPlugin.AI
{
    /// <summary>
    /// Settings for the Gemini CLI integration — persisted to disk.
    /// Stores the API key, CLI mode toggle, and prerequisite paths.
    /// </summary>
    public class GeminiCliSettings
    {
        /// <summary>Google AI Studio API key for Gemini CLI.</summary>
        public string GeminiApiKey { get; set; } = "";

        /// <summary>Whether to use Gemini CLI mode (true) or the HTTP API mode (false).</summary>
        public bool UseGeminiCli { get; set; } = false;

        /// <summary>Auto-detected or manually set path to node.exe.</summary>
        public string NodePath { get; set; } = "";

        /// <summary>Path to the gemini CLI executable (npx or global install).</summary>
        public string GeminiCliPath { get; set; } = "";

        /// <summary>Whether to auto-install the CLI if missing.</summary>
        public bool AutoInstallCli { get; set; } = true;

        /// <summary>Custom MCP server path override. Empty = auto-detect.</summary>
        public string McpServerPath { get; set; } = "";

        // ── Persistence ──
        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins", "BIMBot", "gemini-cli-settings.json");

        public void Save()
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static GeminiCliSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonConvert.DeserializeObject<GeminiCliSettings>(File.ReadAllText(SettingsPath))
                           ?? new GeminiCliSettings();
            }
            catch { }
            return new GeminiCliSettings();
        }

        /// <summary>Whether the CLI mode has a valid API key configured.</summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(GeminiApiKey);
    }
}
