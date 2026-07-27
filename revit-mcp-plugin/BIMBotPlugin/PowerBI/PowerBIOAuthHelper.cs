using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.AI
{
    /// <summary>
    /// Azure AD OAuth 2.0 + PKCE flow for Power BI Embedded.
    /// Modeled on GoogleOAuthHelper — same token storage pattern.
    /// </summary>
    public class PowerBIOAuthHelper
    {
        private const int RedirectPort = 3848;
        private const string RedirectUri = "http://localhost:3848/callback";

        private static readonly string[] Scopes = new[]
        {
            "https://analysis.windows.net/powerbi/api/.default",
            "offline_access",
            "openid",
            "profile",
            "email"
        };

        private static string TokenFilePath => Path.Combine(
            GetMcpServerConfigDir(), "powerbi-tokens.json");

        // ═══════════════════ Credentials ═══════════════════

        public static (string clientId, string tenantId) LoadCredentials()
        {
            var envPath = FindEnvFile();
            if (string.IsNullOrEmpty(envPath) || !File.Exists(envPath))
                return ("", "");

            string clientId = "", tenantId = "common";
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("POWERBI_CLIENT_ID="))
                    clientId = trimmed.Substring("POWERBI_CLIENT_ID=".Length).Trim();
                else if (trimmed.StartsWith("POWERBI_TENANT_ID="))
                    tenantId = trimmed.Substring("POWERBI_TENANT_ID=".Length).Trim();
            }

            if (string.IsNullOrWhiteSpace(tenantId)) tenantId = "common";
            return (clientId, tenantId);
        }

        public static bool HasValidCredentials()
        {
            var (id, _) = LoadCredentials();
            return !string.IsNullOrWhiteSpace(id) && !id.Contains("your_");
        }

        // ═══════════════════ Sign In ═══════════════════

        public static async Task<string> SignInAsync()
        {
            var (clientId, tenantId) = LoadCredentials();

            if (string.IsNullOrWhiteSpace(clientId) || clientId.Contains("your_"))
                throw new InvalidOperationException(
                    "POWERBI_CLIENT_ID is not configured.\n\n" +
                    "1. Go to https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps\n" +
                    "2. Create an App Registration (Desktop app type)\n" +
                    "3. Add redirect URI: http://localhost:3848/callback\n" +
                    "4. Add API permissions: Power BI Service > Report.Read.All, Workspace.Read.All\n" +
                    "5. Copy Application (client) ID to POWERBI_CLIENT_ID in .env");

            // PKCE: generate code_verifier and code_challenge
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = ComputeCodeChallenge(codeVerifier);

            var authUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize"
                + $"?client_id={Uri.EscapeDataString(clientId)}"
                + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
                + "&response_type=code"
                + $"&scope={Uri.EscapeDataString(string.Join(" ", Scopes))}"
                + $"&code_challenge={Uri.EscapeDataString(codeChallenge)}"
                + "&code_challenge_method=S256"
                + "&prompt=select_account";

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{RedirectPort}/");
            listener.Start();

            try
            {
                Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

                var contextTask = listener.GetContextAsync();
                var completed = await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromMinutes(5)));

                if (completed != contextTask)
                    throw new TimeoutException("Sign-in timed out after 5 minutes.");

                var context = await contextTask;
                var request = context.Request;
                var response = context.Response;

                var code = request.QueryString["code"];
                var error = request.QueryString["error"];
                var errorDesc = request.QueryString["error_description"];

                if (!string.IsNullOrEmpty(error))
                {
                    SendResponse(response, 400, "Sign-in Failed", $"Azure AD returned: {error}\n{errorDesc}");
                    throw new Exception($"Azure AD sign-in failed: {error} — {errorDesc}");
                }

                if (string.IsNullOrEmpty(code))
                {
                    SendResponse(response, 400, "Sign-in Failed", "No authorization code received.");
                    throw new Exception("No authorization code received from Azure AD.");
                }

                var tokens = await ExchangeCodeForTokens(code, clientId, tenantId, codeVerifier);
                var email = await GetUserEmail(tokens["access_token"]?.ToString() ?? "");

                SaveTokens(tokens);

                var settings = IntegrationSettings.Load();
                settings.PowerBISignedIn = true;
                settings.PowerBIEmail = email ?? "";
                settings.PowerBIEnabled = true;
                settings.Save();

                SendResponse(response, 200, "Signed In!",
                    $"Connected as: {email}\n\nYou can close this window.");

                return email;
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }

        // ═══════════════════ Sign Out ═══════════════════

        public static void SignOut()
        {
            try { if (File.Exists(TokenFilePath)) File.Delete(TokenFilePath); } catch { }

            var settings = IntegrationSettings.Load();
            settings.PowerBISignedIn = false;
            settings.PowerBIEmail = "";
            settings.Save();
        }

        // ═══════════════════ Token Management ═══════════════════

        public static bool IsSignedIn()
        {
            try
            {
                if (!File.Exists(TokenFilePath)) return false;
                var data = JObject.Parse(File.ReadAllText(TokenFilePath));
                // Check if we have a refresh token (access token may be expired, but that's ok)
                return !string.IsNullOrEmpty(data["refresh_token"]?.ToString());
            }
            catch { return false; }
        }

        public static async Task<string> GetAccessTokenAsync()
        {
            if (!File.Exists(TokenFilePath))
                throw new InvalidOperationException("Not signed in to Power BI. Please sign in first.");

            var data = JObject.Parse(File.ReadAllText(TokenFilePath));
            var expiresAt = data["expires_at"]?.Value<long>() ?? 0;
            var accessToken = data["access_token"]?.ToString();

            // If token is still valid (with 60s buffer), return it
            if (expiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60_000
                && !string.IsNullOrEmpty(accessToken))
                return accessToken;

            // Refresh the token
            var refreshToken = data["refresh_token"]?.ToString();
            if (string.IsNullOrEmpty(refreshToken))
                throw new InvalidOperationException("Refresh token expired. Please sign in again.");

            var (clientId, tenantId) = LoadCredentials();
            var newTokens = await RefreshTokenAsync(refreshToken, clientId, tenantId);
            SaveTokens(newTokens);

            return newTokens["access_token"]?.ToString()
                ?? throw new InvalidOperationException("Failed to refresh access token.");
        }

        // ═══════════════════ Private Helpers ═══════════════════

        private static async Task<JObject> ExchangeCodeForTokens(
            string code, string clientId, string tenantId, string codeVerifier)
        {
            using (var client = new HttpClient())
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "code", code },
                    { "client_id", clientId },
                    { "redirect_uri", RedirectUri },
                    { "grant_type", "authorization_code" },
                    { "code_verifier", codeVerifier }
                });

                var resp = await client.PostAsync(
                    $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token", content);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"Token exchange failed: {body}");

                var data = JObject.Parse(body);
                var expiresIn = data["expires_in"]?.Value<int>() ?? 3600;

                return new JObject
                {
                    ["access_token"] = data["access_token"],
                    ["refresh_token"] = data["refresh_token"],
                    ["expires_at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresIn * 1000L),
                    ["token_type"] = data["token_type"],
                    ["scope"] = data["scope"]
                };
            }
        }

        private static async Task<JObject> RefreshTokenAsync(string refreshToken, string clientId, string tenantId)
        {
            using (var client = new HttpClient())
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "refresh_token", refreshToken },
                    { "grant_type", "refresh_token" },
                    { "scope", string.Join(" ", Scopes) }
                });

                var resp = await client.PostAsync(
                    $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token", content);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    // Refresh token may have expired — clear sign-in state
                    SignOut();
                    throw new Exception($"Token refresh failed. Please sign in again. Details: {body}");
                }

                var data = JObject.Parse(body);
                var expiresIn = data["expires_in"]?.Value<int>() ?? 3600;

                return new JObject
                {
                    ["access_token"] = data["access_token"],
                    ["refresh_token"] = data["refresh_token"] ?? refreshToken,
                    ["expires_at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresIn * 1000L),
                    ["token_type"] = data["token_type"],
                    ["scope"] = data["scope"]
                };
            }
        }

        private static async Task<string> GetUserEmail(string accessToken)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    var resp = await client.GetStringAsync("https://graph.microsoft.com/v1.0/me");
                    var data = JObject.Parse(resp);
                    return data["mail"]?.ToString() ?? data["userPrincipalName"]?.ToString() ?? "";
                }
            }
            catch { return ""; }
        }

        private static void SaveTokens(JObject tokens)
        {
            var dir = Path.GetDirectoryName(TokenFilePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(TokenFilePath, tokens.ToString(Formatting.Indented));
        }

        // ── PKCE helpers ──

        private static string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string ComputeCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
                return Base64UrlEncode(hash);
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        // ── Response HTML ──

        private static void SendResponse(HttpListenerResponse response, int statusCode, string title, string message)
        {
            var icon = statusCode == 200 ? "✅" : "❌";
            var html = $@"
<html>
<body style=""font-family:'Segoe UI',sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;background:linear-gradient(135deg,#2563EB 0%,#1E40AF 100%);"">
  <div style=""text-align:center;background:white;padding:40px;border-radius:16px;box-shadow:0 20px 60px rgba(0,0,0,0.3);max-width:420px;"">
    <div style=""font-size:64px;margin-bottom:16px;"">{icon}</div>
    <h1 style=""color:#333;margin:0 0 8px 0;"">{title}</h1>
    <p style=""color:#666;white-space:pre-line;"">{System.Net.WebUtility.HtmlEncode(message)}</p>
    <p style=""color:#999;font-size:14px;"">You can close this window.</p>
  </div>
  <script>setTimeout(()=>window.close(),5000)</script>
</body>
</html>";

            response.StatusCode = statusCode;
            response.ContentType = "text/html; charset=utf-8";
            var buffer = Encoding.UTF8.GetBytes(html);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        // ── Shared path helpers (same pattern as GoogleOAuthHelper) ──

        private static string GetMcpServerConfigDir()
        {
            var envPath = FindEnvFile();
            if (!string.IsNullOrEmpty(envPath))
                return Path.Combine(Path.GetDirectoryName(envPath)!, "config");

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", "Addins", "BIMBot");
        }

        private static string FindEnvFile()
        {
            // Walk up from the plugin DLL: covers both the installed layout
            // ({app}\plugin\<tfm>\BIMBotPlugin.dll → {app}\server\.env) and a
            // dev clone (…\bin\Release\<tfm>\BIMBotPlugin.dll →
            // <repo>\revit-mcp-server\.env), wherever the repo or install
            // dir lives.
            try
            {
                var dir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                for (var i = 0; !string.IsNullOrEmpty(dir) && i < 7; i++)
                {
                    var installed = Path.Combine(dir, "server", ".env");
                    if (File.Exists(installed)) return installed;

                    var devClone = Path.Combine(dir, "revit-mcp-server", ".env");
                    if (File.Exists(devClone)) return devClone;

                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch { }

            return "";
        }
    }
}
