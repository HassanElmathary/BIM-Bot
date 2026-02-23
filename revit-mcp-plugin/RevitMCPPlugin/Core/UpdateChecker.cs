using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Checks for updates via the GitHub Releases API and supports downloading updates.
    /// </summary>
    public class UpdateChecker
    {
        private const string GITHUB_OWNER = "HassanElmathary";
        private const string GITHUB_REPO = "Revit-MCP";

        private static readonly string API_URL =
            $"https://api.github.com/repos/{GITHUB_OWNER}/{GITHUB_REPO}/releases/latest";

        private static readonly string SkipVersionFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RevitMCP", "skip_version.txt");

        private static readonly HttpClient _httpClient = new HttpClient();

        static UpdateChecker()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RevitMCP-UpdateChecker");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        /// <summary>
        /// Synchronous update check (used by the ribbon button command).
        /// </summary>
        public UpdateInfo CheckForUpdate()
        {
            try
            {
                var json = _httpClient.GetStringAsync(API_URL).Result;
                return ParseRelease(json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Update check failed", ex);
                throw new Exception($"Could not check for updates: {ex.Message}");
            }
        }

        /// <summary>
        /// Async update check (used by the startup background check).
        /// </summary>
        public async Task<UpdateInfo> CheckForUpdateAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(API_URL);
                return ParseRelease(json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Update check failed", ex);
                return new UpdateInfo { UpdateAvailable = false };
            }
        }

        /// <summary>
        /// Downloads the update asset to a temp folder and returns the file path.
        /// </summary>
        public async Task<string> DownloadUpdateAsync(string downloadUrl, string fileName)
        {
            try
            {
                var downloadDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RevitMCP", "Updates");
                Directory.CreateDirectory(downloadDir);

                var filePath = Path.Combine(downloadDir, fileName);

                // Delete old file if it exists
                if (File.Exists(filePath))
                    File.Delete(filePath);

                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var downloadStream = await response.Content.ReadAsStreamAsync())
                    {
                        await downloadStream.CopyToAsync(fileStream);
                    }
                }

                Logger.Log($"Update downloaded to: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Logger.LogError("Update download failed", ex);
                throw new Exception($"Failed to download update: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves a version string so the user won't be notified about it again.
        /// </summary>
        public static void SkipVersion(string version)
        {
            try
            {
                var dir = Path.GetDirectoryName(SkipVersionFile);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(SkipVersionFile, version);
                Logger.Log($"Skipping version: {version}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save skip version", ex);
            }
        }

        /// <summary>
        /// Returns the version the user chose to skip, or empty string if none.
        /// </summary>
        public static string GetSkippedVersion()
        {
            try
            {
                return File.Exists(SkipVersionFile) ? File.ReadAllText(SkipVersionFile).Trim() : "";
            }
            catch
            {
                return "";
            }
        }

        private UpdateInfo ParseRelease(string json)
        {
            var release = JObject.Parse(json);

            var latestTag = release["tag_name"]?.ToString()?.TrimStart('v') ?? "0.0.0";
            var changelog = release["body"]?.ToString() ?? "No changelog available";
            var htmlUrl = release["html_url"]?.ToString() ?? "";

            // Find the .exe or .zip asset for download
            var downloadUrl = htmlUrl;
            string assetFileName = "";
            var assets = release["assets"] as JArray;
            if (assets != null)
            {
                foreach (JObject asset in assets)
                {
                    var name = asset["name"]?.ToString() ?? "";
                    if (name.EndsWith(".exe") || name.EndsWith(".zip") || name.EndsWith(".msi"))
                    {
                        downloadUrl = asset["browser_download_url"]?.ToString() ?? htmlUrl;
                        assetFileName = name;
                        break;
                    }
                }
            }

            var currentVersion = new Version(Application.Version);
            var latestVersion = new Version(latestTag);

            return new UpdateInfo
            {
                UpdateAvailable = latestVersion > currentVersion,
                LatestVersion = $"v{latestTag}",
                Changelog = changelog.Length > 1000 ? changelog.Substring(0, 1000) + "..." : changelog,
                DownloadUrl = downloadUrl,
                AssetFileName = assetFileName,
                ReleaseUrl = htmlUrl
            };
        }
    }

    public class UpdateInfo
    {
        public bool UpdateAvailable { get; set; }
        public string LatestVersion { get; set; } = "";
        public string Changelog { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string AssetFileName { get; set; } = "";
        public string ReleaseUrl { get; set; } = "";
    }
}
