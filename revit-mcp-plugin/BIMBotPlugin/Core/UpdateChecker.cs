using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Checks for updates via a lightweight version manifest (website → GitHub raw)
    /// with fallback to the full GitHub Releases API. Supports downloading updates.
    /// </summary>
    public class UpdateChecker
    {
        private const string GITHUB_OWNER = "HassanElmathary";
        private const string GITHUB_REPO = "BIM-Bot";

        private static readonly string API_URL =
            $"https://api.github.com/repos/{GITHUB_OWNER}/{GITHUB_REPO}/releases/latest";

        /// <summary>
        /// Lightweight version manifest URLs, tried in order.
        /// 1. Website (fast, no rate limit)
        /// 2. Raw GitHub (fallback, no API rate limit)
        /// </summary>
        private static readonly string[] VERSION_JSON_URLS =
        {
            "https://elmthary.space/version.json",
            $"https://raw.githubusercontent.com/{GITHUB_OWNER}/{GITHUB_REPO}/main/version.json"
        };

        private static readonly string SkipVersionFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BIMBot", "skip_version.txt");

        private static readonly HttpClient _httpClient = new HttpClient();

        static UpdateChecker()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BIMBot-UpdateChecker");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        /// <summary>
        /// Synchronous update check (used by the ribbon button command).
        /// Tries the lightweight manifest first, falls back to GitHub Releases API.
        /// </summary>
        public UpdateInfo CheckForUpdate()
        {
            try
            {
                // Try lightweight manifest first
                var manifestResult = CheckFromManifestSync();
                if (manifestResult != null)
                    return manifestResult;

                // Fall back to full GitHub Releases API
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
        /// Tries the lightweight manifest first, falls back to GitHub Releases API.
        /// </summary>
        public async Task<UpdateInfo> CheckForUpdateAsync()
        {
            try
            {
                // Try lightweight manifest first
                var manifestResult = await CheckFromManifestAsync();
                if (manifestResult != null)
                    return manifestResult;

                // Fall back to full GitHub Releases API
                Logger.Log("Manifest check failed, falling back to GitHub Releases API...");
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
        /// Tries each VERSION_JSON_URL in order (sync). Returns null if all fail.
        /// </summary>
        private UpdateInfo CheckFromManifestSync()
        {
            foreach (var url in VERSION_JSON_URLS)
            {
                try
                {
                    Logger.Log($"Checking update manifest: {url}");
                    var json = _httpClient.GetStringAsync(url).Result;
                    var result = ParseManifest(json);
                    if (result != null)
                    {
                        Logger.Log($"Update manifest parsed from {url} — latest: {result.LatestVersion}");
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Manifest fetch failed ({url}): {ex.Message}");
                }
            }
            return null;
        }

        /// <summary>
        /// Tries each VERSION_JSON_URL in order (async). Returns null if all fail.
        /// </summary>
        private async Task<UpdateInfo> CheckFromManifestAsync()
        {
            foreach (var url in VERSION_JSON_URLS)
            {
                try
                {
                    Logger.Log($"Checking update manifest: {url}");
                    var json = await _httpClient.GetStringAsync(url);
                    var result = ParseManifest(json);
                    if (result != null)
                    {
                        Logger.Log($"Update manifest parsed from {url} — latest: {result.LatestVersion}");
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Manifest fetch failed ({url}): {ex.Message}");
                }
            }
            return null;
        }

        /// <summary>
        /// Parses the lightweight version.json manifest.
        /// </summary>
        private UpdateInfo ParseManifest(string json)
        {
            try
            {
                var manifest = JObject.Parse(json);

                var latestTag = manifest["version"]?.ToString()?.TrimStart('v') ?? "";
                if (string.IsNullOrEmpty(latestTag))
                    return null;

                var changelog = manifest["changelog"]?.ToString() ?? "No changelog available";
                var releaseUrl = manifest["releaseUrl"]?.ToString() ?? "";
                var downloadUrl = manifest["downloadUrl"]?.ToString() ?? releaseUrl;
                var assetFileName = manifest["assetFileName"]?.ToString() ?? "";

                var currentVersion = new Version(Application.Version);
                var latestVersion = new Version(latestTag);

                return new UpdateInfo
                {
                    UpdateAvailable = latestVersion > currentVersion,
                    LatestVersion = $"v{latestTag}",
                    Changelog = changelog.Length > 1000 ? changelog.Substring(0, 1000) + "..." : changelog,
                    DownloadUrl = downloadUrl,
                    AssetFileName = assetFileName,
                    ReleaseUrl = releaseUrl
                };
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to parse manifest JSON: {ex.Message}");
                return null;
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
                    "BIMBot", "Updates");
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
