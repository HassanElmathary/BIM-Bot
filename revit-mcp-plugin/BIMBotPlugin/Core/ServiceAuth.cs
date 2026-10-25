using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Shared secret that every request to the socket service must carry.
    ///
    /// The listener is loopback-only, but loopback is not "only BIM-Bot": any
    /// process of any user on the PC, and any web page (a browser POST to
    /// 127.0.0.1:8080 lands here and the skip-to-'{' framing would parse its
    /// body), could otherwise drive Revit — including send_code_to_revit.
    ///
    /// Location: %LOCALAPPDATA%\BIMBot\auth.token. That folder inherits the
    /// user-only ACL of the profile, so a caller able to read the token is
    /// already running as this user. One token per user (not per Revit
    /// process) because the MCP server talks to several listeners — Revit
    /// bridge slaves on other ports and Navisworks — and all of them read this
    /// same file. Whichever side starts first creates it.
    ///
    /// Keep in sync with NavisServiceAuth.cs and revit-mcp-server/src/utils/serviceAuth.ts.
    /// </summary>
    public static class ServiceAuth
    {
        /// <summary>Top-level JSON-RPC field that carries the token.</summary>
        public const string FieldName = "auth";

        /// <summary>JSON-RPC error code returned for a missing or wrong token.</summary>
        public const int UnauthorizedCode = -32001;

        public static string FilePath => Path.Combine(ServiceEndpoint.Directory, "auth.token");

        private static readonly object _lock = new object();
        private static string? _cached;

        /// <summary>The current token, creating the file on first use.</summary>
        public static string Token
        {
            get
            {
                lock (_lock)
                {
                    return _cached ??= LoadOrCreate();
                }
            }
        }

        /// <summary>
        /// True when the request carries the current token. On a mismatch the
        /// file is re-read once: a reinstall or the uninstaller's cleanup may
        /// have replaced it while Revit kept running.
        /// </summary>
        public static bool IsAuthorized(JObject request)
        {
            var presented = request[FieldName]?.ToString();
            if (string.IsNullOrEmpty(presented)) return false;
            if (FixedTimeEquals(presented!, Token)) return true;

            lock (_lock)
            {
                _cached = LoadOrCreate();
                return FixedTimeEquals(presented!, _cached);
            }
        }

        public static JObject UnauthorizedResponse(JObject request) => new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = request["id"]?.ToString() ?? "0",
            ["error"] = new JObject
            {
                ["code"] = UnauthorizedCode,
                ["message"] = "Unauthorized: missing or invalid BIM-Bot auth token. " +
                              "The MCP server and the plugin must come from the same BIM-Bot version — " +
                              "reinstall BIM-Bot and restart your AI app."
            }
        };

        private static string LoadOrCreate()
        {
            var existing = TryRead();
            if (existing != null) return existing;

            Directory.CreateDirectory(ServiceEndpoint.Directory);
            var fresh = NewToken();
            var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, fresh);
            try
            {
                // File.Move fails if the target exists, so two creators racing
                // (another Revit, Navisworks, the MCP server) agree on one token.
                File.Move(temp, FilePath);
                Logger.Log($"Service auth token created → {FilePath}");
                return fresh;
            }
            catch (IOException)
            {
                try { File.Delete(temp); } catch { }
                var winner = TryRead();
                if (winner != null) return winner;

                // The file exists but holds no usable token (empty or hand-edited).
                File.WriteAllText(FilePath, fresh);
                return fresh;
            }
        }

        private static string? TryRead()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var text = File.ReadAllText(FilePath).Trim();
                return text.Length >= 32 ? text : null;
            }
            catch
            {
                return null;
            }
        }

        private static string NewToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // CryptographicOperations.FixedTimeEquals is not available on net47/net48.
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
