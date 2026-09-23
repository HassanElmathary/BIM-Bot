using System;
using System.IO;
using Newtonsoft.Json;

namespace BIMBotNavisPlugin.AI
{
    /// <summary>
    /// Same provider list as Revit GeminiSettings (gemini/deepseek/perplexity/
    /// openrouter/ollama/cerebras/groq/openai) — user pastes the SAME API key.
    /// Stored separately so Revit settings are never touched:
    ///   %AppData%\Autodesk\Navisworks\Addins\BIMBot\navis-settings.json
    /// </summary>
    public class NavisAISettings
    {
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "gemini-2.0-flash";
        public string Provider { get; set; } = "gemini";

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Navisworks", "Addins", "BIMBot", "navis-settings.json");

        public void Save()
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static NavisAISettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonConvert.DeserializeObject<NavisAISettings>(File.ReadAllText(SettingsPath))
                           ?? new NavisAISettings();
            }
            catch { }
            return new NavisAISettings();
        }

        public bool IsConfigured =>
            Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(ApiKey);

        public static string[] Providers { get; } =
            new[] { "gemini", "openai", "deepseek", "perplexity", "openrouter", "groq", "cerebras", "ollama" };
    }
}
