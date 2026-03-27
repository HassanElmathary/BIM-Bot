using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.AI
{
    /// <summary>
    /// Handles Google OAuth 2.0 flow directly from the WPF plugin.
    /// Opens the browser for sign-in and listens for the callback on localhost.
    /// Saves tokens so the MCP server can use them too.
    /// </summary>
    public class GoogleOAuthHelper
    {
        private const int RedirectPort = 3847;
        private const string RedirectUri = "http://localhost:3847/callback";

        private static readonly string[] Scopes = new[]
        {
            "https://www.googleapis.com/auth/spreadsheets",
            "https://www.googleapis.com/auth/drive.readonly",
            "https://www.googleapis.com/auth/userinfo.email",
            "https://www.googleapis.com/auth/userinfo.profile"
        };

        // Token file — same location as the MCP server uses
        private static string TokenFilePath => Path.Combine(
            GetMcpServerConfigDir(), "google-tokens.json");

        /// <summary>
        /// Read GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET from the MCP server's .env file.
        /// </summary>
        public static (string clientId, string clientSecret) LoadCredentials()
        {
            var envPath = FindEnvFile();
            if (string.IsNullOrEmpty(envPath) || !File.Exists(envPath))
                return ("", "");

            string clientId = "", clientSecret = "";
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("GOOGLE_CLIENT_ID="))
                    clientId = trimmed.Substring("GOOGLE_CLIENT_ID=".Length).Trim();
                else if (trimmed.StartsWith("GOOGLE_CLIENT_SECRET="))
                    clientSecret = trimmed.Substring("GOOGLE_CLIENT_SECRET=".Length).Trim();
            }

            return (clientId, clientSecret);
        }

        /// <summary>
        /// Check if valid credentials are configured (not placeholder values).
        /// </summary>
        public static bool HasValidCredentials()
        {
            var (id, secret) = LoadCredentials();
            return !string.IsNullOrWhiteSpace(id)
                && !id.Contains("your_oauth")
                && !string.IsNullOrWhiteSpace(secret)
                && !secret.Contains("your_oauth");
        }

        /// <summary>
        /// Perform the full OAuth flow: open browser → listen for callback → exchange code → save tokens.
        /// Returns the user's email on success, or null on failure.
        /// </summary>
        public static async Task<string> SignInAsync()
        {
            var (clientId, clientSecret) = LoadCredentials();

            if (string.IsNullOrWhiteSpace(clientId) || clientId.Contains("your_oauth"))
                throw new InvalidOperationException(
                    "GOOGLE_CLIENT_ID is not configured.\n\n" +
                    "1. Go to https://console.cloud.google.com/apis/credentials\n" +
                    "2. Create an OAuth 2.0 Client ID (Desktop app type)\n" +
                    "3. Copy the Client ID and Client Secret\n" +
                    "4. Paste them in the MCP server's .env file");

            // Build the Google OAuth consent URL
            var authUrl = "https://accounts.google.com/o/oauth2/v2/auth"
                + $"?client_id={Uri.EscapeDataString(clientId)}"
                + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
                + "&response_type=code"
                + $"&scope={Uri.EscapeDataString(string.Join(" ", Scopes))}"
                + "&access_type=offline"
                + "&prompt=consent";

            // Start local HTTP listener for the callback
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{RedirectPort}/");
            listener.Start();

            try
            {
                // Open browser for Google sign-in
                Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

                // Wait for the callback (timeout: 5 minutes)
                var contextTask = listener.GetContextAsync();
                var completed = await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromMinutes(5)));

                if (completed != contextTask)
                    throw new TimeoutException("Sign-in timed out after 5 minutes.");

                var context = await contextTask;
                var request = context.Request;
                var response = context.Response;

                // Extract the authorization code
                var code = request.QueryString["code"];
                var error = request.QueryString["error"];

                if (!string.IsNullOrEmpty(error))
                {
                    SendResponse(response, 400, "❌ Sign-in Failed", $"Google returned error: {error}");
                    throw new Exception($"Google sign-in failed: {error}");
                }

                if (string.IsNullOrEmpty(code))
                {
                    SendResponse(response, 400, "❌ Sign-in Failed", "No authorization code received.");
                    throw new Exception("No authorization code received from Google.");
                }

                // Exchange code for tokens
                var tokens = await ExchangeCodeForTokens(code, clientId, clientSecret);

                // Get user email
                var email = await GetUserEmail(tokens["access_token"]?.ToString() ?? "");

                // Save tokens
                SaveTokens(tokens);

                // Update integration settings
                var settings = IntegrationSettings.Load();
                settings.GoogleSignedIn = true;
                settings.GoogleEmail = email ?? "";
                settings.GoogleSheetsEnabled = true;
                settings.Save();

                SendResponse(response, 200, "✅ Signed In!", 
                    $"Connected as: {email}\n\nYou can close this window.");

                return email;
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }

        /// <summary>
        /// Sign out — remove stored tokens.
        /// </summary>
        public static void SignOut()
        {
            try
            {
                if (File.Exists(TokenFilePath))
                    File.Delete(TokenFilePath);
            }
            catch { }

            var settings = IntegrationSettings.Load();
            settings.GoogleSignedIn = false;
            settings.GoogleEmail = "";
            settings.Save();
        }

        /// <summary>
        /// Check if tokens exist and are still valid.
        /// </summary>
        public static bool IsSignedIn()
        {
            try
            {
                if (!File.Exists(TokenFilePath)) return false;
                var data = JObject.Parse(File.ReadAllText(TokenFilePath));
                var expiresAt = data["expires_at"]?.Value<long>() ?? 0;
                return expiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            catch { return false; }
        }

        // ═══════════════════ Private Helpers ═══════════════════

        private static async Task<JObject> ExchangeCodeForTokens(string code, string clientId, string clientSecret)
        {
            using (var client = new HttpClient())
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "code", code },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "redirect_uri", RedirectUri },
                    { "grant_type", "authorization_code" }
                });

                var resp = await client.PostAsync("https://oauth2.googleapis.com/token", content);
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

        private static async Task<string> GetUserEmail(string accessToken)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    var resp = await client.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo");
                    var data = JObject.Parse(resp);
                    return data["email"]?.ToString() ?? "";
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

        private static void SendResponse(HttpListenerResponse response, int statusCode, string title, string message)
        {
            var html = $@"
<html>
<body style=""font-family:'Segoe UI',sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);"">
  <div style=""text-align:center;background:white;padding:40px;border-radius:16px;box-shadow:0 20px 60px rgba(0,0,0,0.3);"">
    <div style=""font-size:64px;margin-bottom:16px;"">{(statusCode == 200 ? "✅" : "❌")}</div>
    <h1 style=""color:#333;margin:0 0 8px 0;"">{title}</h1>
    <p style=""color:#666;"">{message}</p>
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

        private static string GetMcpServerConfigDir()
        {
            // MCP server config is at: revit-mcp-server/config/
            var envPath = FindEnvFile();
            if (!string.IsNullOrEmpty(envPath))
                return Path.Combine(Path.GetDirectoryName(envPath)!, "config");

            // Fallback
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", "Addins", "RevitMCP");
        }

        private static string FindEnvFile()
        {
            // Try common locations for the .env file
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "revit-mcp-server", ".env"),
                @"c:\Users\hassa\OneDrive\01-me\Revit MCP\revit-mcp-server\.env",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "OneDrive", "01-me", "Revit MCP", "revit-mcp-server", ".env"),
            };

            foreach (var path in candidates)
            {
                try
                {
                    var resolved = Path.GetFullPath(path);
                    if (File.Exists(resolved)) return resolved;
                }
                catch { }
            }

            return "";
        }
    }
}
