using System;
using System.IO;
using Newtonsoft.Json;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Handshake file that tells the MCP server which TCP port the Revit-side
    /// service actually bound to.
    ///
    /// Port 8080 is heavily contested (IIS Express, Docker, Jenkins, countless
    /// dev tools). When it is taken, SocketService falls back to the next free
    /// port — but the MCP server used to dial 8080 unconditionally, so every
    /// tool call either timed out or, worse, talked to the unrelated process
    /// squatting on 8080. This file is how the two halves agree on a port.
    ///
    /// Location: %LOCALAPPDATA%\BIMBot\service.json — per-user, which is right:
    /// Revit and the MCP server (launched by Claude Desktop) run as the same user.
    /// </summary>
    public static class ServiceEndpoint
    {
        public static string Directory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BIMBot");

        public static string FilePath => Path.Combine(Directory, "service.json");

        private class Payload
        {
            [JsonProperty("port")] public int Port { get; set; }
            [JsonProperty("pid")] public int Pid { get; set; }
            [JsonProperty("host")] public string Host { get; set; } = "127.0.0.1";
            [JsonProperty("pluginVersion")] public string PluginVersion { get; set; } = "";
            [JsonProperty("startedAt")] public string StartedAt { get; set; } = "";
        }

        /// <summary>
        /// Publish the live endpoint. Called right after the listener binds.
        /// Never throws — a failed handshake write must not stop the service
        /// (the server still falls back to 8080).
        /// </summary>
        public static void Publish(int port, string pluginVersion)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                var payload = new Payload
                {
                    Port = port,
                    Pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                    Host = "127.0.0.1",
                    PluginVersion = pluginVersion,
                    StartedAt = DateTime.UtcNow.ToString("o")
                };
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(payload, Formatting.Indented));
                Logger.Log($"Service endpoint published: 127.0.0.1:{port} → {FilePath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to publish service endpoint (non-critical)", ex);
            }
        }

        /// <summary>Remove the handshake file when the service stops.</summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to clear service endpoint (non-critical)", ex);
            }
        }
    }
}
