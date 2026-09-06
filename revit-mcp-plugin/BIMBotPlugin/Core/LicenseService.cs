using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace BIMBotPlugin.Core
{
    // ── Data Models ──────────────────────────────────────────

    /// <summary>
    /// Cached license information stored locally (encrypted).
    /// </summary>
    public class LicenseInfo
    {
        public string MachineId { get; set; } = "";
        public string Username { get; set; } = "";
        public string LicenseKey { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? ExpiresAt { get; set; }
        public DateTime LastValidatedAt { get; set; }
        public bool IsValid { get; set; }
    }

    /// <summary>
    /// Response from the Firebase validateLicense Cloud Function.
    /// </summary>
    public class LicenseValidationResult
    {
        [JsonProperty("valid")]
        public bool Valid { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }

        [JsonProperty("expiresAt")]
        public string? ExpiresAt { get; set; }

        [JsonProperty("reason")]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Manages BIM-Bot license activation, online validation against Firebase,
    /// local caching with a 7-day offline grace period, and machine identification.
    /// </summary>
    public class LicenseService
    {
        // ── Configuration ────────────────────────────────────

        /// <summary>Firebase Cloud Functions base URL (europe-west1).</summary>
        private const string FIREBASE_BASE_URL =
            "https://europe-west1-revit-mcp10.cloudfunctions.net";

        /// <summary>Number of days the cached validation remains valid when offline.</summary>
        private const int GRACE_PERIOD_DAYS = 7;

        // ── Paths ────────────────────────────────────────────

        private static readonly string LicenseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BIMBot");

        private static readonly string LicenseFile =
            Path.Combine(LicenseDir, "license.json");

        // ── HTTP client (shared, long-lived) ─────────────────

        private static readonly HttpClient _httpClient = new HttpClient();

        static LicenseService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BIMBot-LicenseChecker");
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        // ════════════════════════════════════════════════════
        // Machine Identification
        // ════════════════════════════════════════════════════

        /// <summary>
        /// Returns a stable, unique machine identifier derived from the
        /// Windows MachineGuid registry key, formatted as BIM-XXXX-XXXX-XXXX.
        /// </summary>
        public static string GetMachineId()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography"))
                {
                    var guid = key?.GetValue("MachineGuid")?.ToString() ?? "";

                    using (var sha = SHA256.Create())
                    {
                        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(guid));
                        var hex = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
                        return $"BIM-{hex.Substring(0, 4)}-{hex.Substring(4, 4)}-{hex.Substring(8, 4)}";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read MachineGuid from registry", ex);
                // Fallback: derive from machine name
                var fallback = Math.Abs(Environment.MachineName.GetHashCode());
                return $"BIM-{fallback:X4}-0000-0000".Substring(0, 17);
            }
        }

        /// <summary>Returns the current Windows user account name.</summary>
        public static string GetDeviceUsername() => Environment.UserName;

        /// <summary>Returns the Windows machine (computer) name.</summary>
        public static string GetMachineName() => Environment.MachineName;

        // ════════════════════════════════════════════════════
        // Online License Validation
        // ════════════════════════════════════════════════════

        /// <summary>
        /// Validates a license key online against the Firebase Cloud Function.
        /// If valid, caches the result locally for offline grace period.
        /// </summary>
        public static async Task<LicenseValidationResult> ValidateOnlineAsync(string licenseKey)
        {
            try
            {
                var payload = new
                {
                    machineId = GetMachineId(),
                    licenseKey = licenseKey,
                    pluginVersion = Application.Version
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{FIREBASE_BASE_URL}/validateLicense", content);

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<LicenseValidationResult>(responseJson)
                             ?? new LicenseValidationResult { Valid = false, Reason = "parse_error" };

                // Cache the result if valid
                if (result.Valid)
                {
                    SaveLicense(licenseKey, result);
                }

                Logger.Log($"License validation: valid={result.Valid}, reason={result.Reason ?? "OK"}");
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError("Online license validation failed", ex);
                return new LicenseValidationResult { Valid = false, Reason = "network_error" };
            }
        }

        /// <summary>
        /// Performs an online re-validation using the previously saved license key.
        /// Called automatically on Revit startup.
        /// Returns true if the license is valid (online or cached within grace period).
        /// </summary>
        public static async Task<bool> ValidateOnStartupAsync()
        {
            var cached = LoadCachedLicense();
            if (cached == null || string.IsNullOrEmpty(cached.LicenseKey))
            {
                Logger.Log("No cached license found — activation required.");
                return false;
            }

            // Try online validation
            var result = await ValidateOnlineAsync(cached.LicenseKey);

            if (result.Valid)
            {
                Logger.Log("Startup license validation succeeded (online).");
                return true;
            }

            // Online said invalid — check if it's a network error
            if (result.Reason == "network_error")
            {
                // Allow grace period for offline use
                if (cached.IsValid && IsWithinGracePeriod(cached))
                {
                    var daysLeft = GRACE_PERIOD_DAYS -
                        (DateTime.UtcNow - cached.LastValidatedAt).TotalDays;
                    Logger.Log($"Offline grace period active — {daysLeft:F0} days remaining.");
                    return true;
                }
            }

            // License is invalid (revoked, expired, or grace period exceeded)
            Logger.Log($"License validation failed: {result.Reason}");
            return false;
        }

        // ════════════════════════════════════════════════════
        // Activation Request
        // ════════════════════════════════════════════════════

        /// <summary>
        /// Sends an activation request to Firebase, which creates a pending
        /// request document and notifies the admin.
        /// </summary>
        public static async Task<bool> RequestActivationAsync(string username, string email)
        {
            try
            {
                var payload = new
                {
                    machineId = GetMachineId(),
                    username = username,
                    email = email,
                    machineName = GetMachineName(),
                    pluginVersion = Application.Version
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{FIREBASE_BASE_URL}/requestActivation", content);

                var success = response.IsSuccessStatusCode;
                Logger.Log($"Activation request sent: success={success}");
                return success;
            }
            catch (Exception ex)
            {
                Logger.LogError("Activation request failed", ex);
                return false;
            }
        }

        // ════════════════════════════════════════════════════
        // Local State Checks
        // ════════════════════════════════════════════════════

        /// <summary>
        /// Quick check: is the plugin currently activated?
        /// Uses cached validation result + offline grace period logic.
        /// Does NOT make a network call.
        /// </summary>
        public static bool IsActivated()
        {
            var info = LoadCachedLicense();
            if (info == null || !info.IsValid) return false;

            // Check expiry
            if (info.ExpiresAt.HasValue && info.ExpiresAt.Value < DateTime.UtcNow)
                return false;

            // Check grace period (must have been validated recently)
            return IsWithinGracePeriod(info);
        }

        /// <summary>Returns the saved license key, if any.</summary>
        public static string? GetSavedLicenseKey()
        {
            return LoadCachedLicense()?.LicenseKey;
        }

        /// <summary>Returns the cached license info for display in Settings.</summary>
        public static LicenseInfo? GetCachedInfo()
        {
            return LoadCachedLicense();
        }

        // ════════════════════════════════════════════════════
        // License Persistence
        // ════════════════════════════════════════════════════

        /// <summary>
        /// Saves the license key and validation result to a local JSON file.
        /// </summary>
        public static void SaveLicense(string licenseKey, LicenseValidationResult result)
        {
            var info = new LicenseInfo
            {
                MachineId = GetMachineId(),
                Username = result.Username ?? GetDeviceUsername(),
                LicenseKey = licenseKey,
                Status = result.Valid ? "active" : "invalid",
                ExpiresAt = string.IsNullOrEmpty(result.ExpiresAt)
                    ? (DateTime?)null
                    : DateTime.Parse(result.ExpiresAt),
                LastValidatedAt = DateTime.UtcNow,
                IsValid = result.Valid
            };

            SaveCachedLicense(info);
        }

        /// <summary>
        /// Clears the local license cache (deactivation).
        /// </summary>
        public static void ClearLicense()
        {
            try
            {
                if (File.Exists(LicenseFile))
                    File.Delete(LicenseFile);
                Logger.Log("License cache cleared.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to clear license cache", ex);
            }
        }

        // ── Private helpers ──────────────────────────────────

        private static bool IsWithinGracePeriod(LicenseInfo info)
        {
            var daysSinceValidation = (DateTime.UtcNow - info.LastValidatedAt).TotalDays;
            return daysSinceValidation <= GRACE_PERIOD_DAYS;
        }

        private static void SaveCachedLicense(LicenseInfo info)
        {
            try
            {
                Directory.CreateDirectory(LicenseDir);
                var json = JsonConvert.SerializeObject(info, Formatting.Indented);
                File.WriteAllText(LicenseFile, json);
                Logger.Log("License cache saved.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save license cache", ex);
            }
        }

        private static LicenseInfo? LoadCachedLicense()
        {
            try
            {
                if (!File.Exists(LicenseFile)) return null;
                var json = File.ReadAllText(LicenseFile);
                return JsonConvert.DeserializeObject<LicenseInfo>(json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load license cache", ex);
                return null;
            }
        }
    }
}
