using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Shared secret every request to the Navisworks socket service must carry.
    /// COPIED from Revit BIMBotPlugin.Core.ServiceAuth — same file
    /// (%LocalAppData%\BIMBot\auth.token), same "auth" field, same error code,
    /// because the MCP server sends one per-user token to every listener.
    /// Without it any local process or web page could drive Navisworks.
    /// </summary>
    public static class NavisServiceAuth
    {
        public const string FieldName = "auth";
        public const int UnauthorizedCode = -32001;

        private static string Directory_ => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BIMBot");

        public static string FilePath => Path.Combine(Directory_, "auth.token");

        private static readonly object _lock = new object();
        private static string? _cached;

        public static string Token
        {
            get { lock (_lock) { return _cached ??= LoadOrCreate(); } }
        }

        /// <summary>True when the request carries the current token; re-reads the file once on a mismatch.</summary>
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

            Directory.CreateDirectory(Directory_);
            var fresh = NewToken();
            var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, fresh);
            try
            {
                File.Move(temp, FilePath); // fails if another creator won the race
                return fresh;
            }
            catch (IOException)
            {
                try { File.Delete(temp); } catch { }
                var winner = TryRead();
                if (winner != null) return winner;
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
            catch { return null; }
        }

        private static string NewToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
