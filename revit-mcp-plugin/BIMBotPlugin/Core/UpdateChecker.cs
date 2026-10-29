using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
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
        /// <summary>
        /// Blocking GET for the synchronous ribbon path. Task.Run moves the await
        /// off the caller's synchronization context - awaiting .Result directly on
        /// Revit's UI thread deadlocks when the continuation tries to post back to
        /// a thread that is blocked waiting for it.
        /// </summary>
        private static string GetStringBlocking(string url)
        {
            return Task.Run(() => _httpClient.GetStringAsync(url)).GetAwaiter().GetResult();
        }

        public UpdateInfo CheckForUpdate()
        {
            try
            {
                // Try lightweight manifest first
                var manifestResult = CheckFromManifestSync();
                if (manifestResult != null)
                    return manifestResult;

                // Fall back to full GitHub Releases API
                var json = GetStringBlocking(API_URL);
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
                    var json = GetStringBlocking(url);
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
                var sha256 = NormalizeSha256(manifest["sha256"]?.ToString());

                var currentVersion = new Version(Application.Version);
                var latestVersion = new Version(latestTag);

                return new UpdateInfo
                {
                    UpdateAvailable = latestVersion > currentVersion,
                    LatestVersion = $"v{latestTag}",
                    Changelog = changelog.Length > 1000 ? changelog.Substring(0, 1000) + "..." : changelog,
                    DownloadUrl = downloadUrl,
                    AssetFileName = assetFileName,
                    ReleaseUrl = releaseUrl,
                    Sha256 = sha256
                };
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to parse manifest JSON: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Downloads the update asset, verifies its SHA-256 and returns the file
        /// path. Throws (and deletes the file) when no checksum is published or
        /// the file does not match, so the caller never runs an installer it
        /// could not verify.
        /// </summary>
        public async Task<string> DownloadUpdateAsync(UpdateInfo info)
        {
            try
            {
                // Gather the checksums BEFORE downloading: the manifest (website /
                // raw GitHub) and GitHub's own per-asset digest. Two independent
                // sources mean tampering with one of them alone is caught.
                var expected = new List<KeyValuePair<string, string>>();
                if (!string.IsNullOrEmpty(info.Sha256))
                    expected.Add(new KeyValuePair<string, string>("version manifest", info.Sha256));
                var githubDigest = await FetchGitHubAssetDigestAsync(info.LatestVersion, info.AssetFileName);
                if (!string.IsNullOrEmpty(githubDigest))
                    expected.Add(new KeyValuePair<string, string>("GitHub release", githubDigest!));

                if (expected.Count == 0)
                    throw new UpdateVerificationException(
                        "No SHA-256 checksum is published for this update, so it was not installed. " +
                        "Download it from the GitHub release page instead.");

                var filePath = await DownloadFileAsync(info.DownloadUrl, info.AssetFileName);

                var actual = ComputeSha256(filePath);
                foreach (var e in expected)
                {
                    if (!string.Equals(actual, e.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(filePath); } catch { }
                        Logger.LogError($"Update checksum mismatch ({e.Key}): expected {e.Value}, got {actual}");
                        throw new UpdateVerificationException(
                            $"The downloaded file does not match the {e.Key} checksum, so it was deleted and not installed. " +
                            "Try again later, or download it from the GitHub release page.");
                    }
                }

                Logger.Log($"Update verified (SHA-256 {actual}) against: {string.Join(", ", expected.ConvertAll(e => e.Key))}");
                return filePath;
            }
            catch (UpdateVerificationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("Update download failed", ex);
                throw new Exception($"Failed to download update: {ex.Message}");
            }
        }

        private async Task<string> DownloadFileAsync(string downloadUrl, string fileName)
        {
            {
                var downloadDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BIMBot", "Updates");
                Directory.CreateDirectory(downloadDir);

                // fileName comes from the release manifest; GetFileName strips any
                // directory component so a crafted name cannot escape the folder.
                var safeName = Path.GetFileName(fileName);
                if (string.IsNullOrWhiteSpace(safeName))
                    throw new InvalidOperationException($"Invalid update file name: '{fileName}'");

                var filePath = Path.Combine(downloadDir, safeName);

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

                // Remove the Mark-of-the-Web (Zone.Identifier) so Windows doesn't
                // show SmartScreen warnings or block a standard user from running it.
                TryUnblockFile(filePath);

                Logger.Log($"Update downloaded to: {filePath}");
                return filePath;
            }
        }

        /// <summary>
        /// GitHub publishes a "sha256:..." digest for every release asset. Returns
        /// null when the release, the asset or the digest is not available (rate
        /// limit, offline) -- the manifest checksum may still cover it.
        /// </summary>
        private async Task<string?> FetchGitHubAssetDigestAsync(string latestVersion, string fileName)
        {
            try
            {
                var tag = "v" + latestVersion.TrimStart('v');
                var url = $"https://api.github.com/repos/{GITHUB_OWNER}/{GITHUB_REPO}/releases/tags/{tag}";
                var release = JObject.Parse(await _httpClient.GetStringAsync(url));
                if (release["assets"] is JArray assets)
                {
                    foreach (JObject asset in assets)
                    {
                        if (string.Equals(asset["name"]?.ToString(), fileName, StringComparison.OrdinalIgnoreCase))
                            return NormalizeSha256(asset["digest"]?.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not read GitHub asset digest (non-fatal): {ex.Message}");
            }
            return null;
        }

        /// <summary>Accepts "abc..." or "sha256:abc..."; returns 64 lowercase hex chars, or "" if invalid.</summary>
        private static string NormalizeSha256(string? value)
        {
            var v = (value ?? "").Trim();
            if (v.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) v = v.Substring(7);
            v = v.ToLowerInvariant();
            if (v.Length != 64) return "";
            foreach (var c in v)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return "";
            return v;
        }

        private static string ComputeSha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Deletes the Zone.Identifier alternate data stream left by browsers /
        /// HttpClient downloads. Best-effort: failures are logged, never thrown,
        /// because the installer runs fine even when still "blocked".
        /// </summary>
        private static void TryUnblockFile(string filePath)
        {
            try
            {
                // Deleting "<file>:Zone.Identifier" removes the Mark-of-the-Web.
                File.Delete(filePath + ":Zone.Identifier");
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not unblock downloaded file (non-fatal): {ex.Message}");
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
            string sha256 = "";
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
                        sha256 = NormalizeSha256(asset["digest"]?.ToString());
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
                ReleaseUrl = htmlUrl,
                Sha256 = sha256
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
        /// <summary>Expected SHA-256 of the asset (64 hex chars), or "" when the source published none.</summary>
        public string Sha256 { get; set; } = "";
    }

    /// <summary>The update could not be verified; its message is shown to the user as-is.</summary>
    public class UpdateVerificationException : Exception
    {
        public UpdateVerificationException(string message) : base(message) { }
    }
}
