using System;
using System.IO;
using Newtonsoft.Json;

namespace BIMBotPlugin.AI
{
    /// <summary>
    /// Settings for external integrations — persisted to disk.
    /// Controls which integrations are enabled and stores their credentials.
    /// </summary>
    public class IntegrationSettings
    {
        // ── Excel ──
        public bool ExcelEnabled { get; set; } = true;

        // ── Notion ──
        public bool NotionEnabled { get; set; } = false;
        public string NotionApiKey { get; set; } = "";
        public string NotionDatabaseId { get; set; } = "";

        // ── Google Sheets ──
        public bool GoogleSheetsEnabled { get; set; } = false;
        public bool GoogleSignedIn { get; set; } = false;
        public string GoogleEmail { get; set; } = "";
        public string GoogleSheetsCredentialsPath { get; set; } = "";
        public string GoogleSheetsSpreadsheetId { get; set; } = "";

        // ── SQLite ──
        public bool SqliteEnabled { get; set; } = true;

        // ── Ollama ──
        public bool OllamaEnabled { get; set; } = false;
        public string OllamaUrl { get; set; } = "http://localhost:11434";
        public string OllamaModel { get; set; } = "qwen2.5:7b-instruct-q4_K_M";

        // ── Persistence ──
        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins", "BIMBot", "integration-settings.json");

        public void Save()
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static IntegrationSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonConvert.DeserializeObject<IntegrationSettings>(File.ReadAllText(SettingsPath))
                           ?? new IntegrationSettings();
            }
            catch { }
            return new IntegrationSettings();
        }

        // ── Validation helpers ──
        public bool IsNotionConfigured =>
            NotionEnabled && !string.IsNullOrWhiteSpace(NotionApiKey) && NotionApiKey != "your_notion_integration_token";

        public bool IsGoogleSheetsConfigured =>
            GoogleSheetsEnabled && (GoogleSignedIn || !string.IsNullOrWhiteSpace(GoogleSheetsCredentialsPath));

        public bool IsOllamaConfigured =>
            OllamaEnabled && !string.IsNullOrWhiteSpace(OllamaUrl);
    }
}
