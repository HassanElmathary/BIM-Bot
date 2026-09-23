using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Publishes where the Navisworks service landed so the MCP server can find it.
    /// Separate file from Revit: %LocalAppData%\BIMBot\service-navis.json
    /// (Revit keeps using service.json — untouched).
    /// </summary>
    public static class NavisServiceEndpoint
    {
        private static string HandshakePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BIMBot", "service-navis.json");

        public static void Publish(int port)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(HandshakePath)!);
                var payload = new JObject
                {
                    ["host"] = "127.0.0.1",
                    ["port"] = port,
                    ["pid"] = Process.GetCurrentProcess().Id,
                    ["product"] = "navisworks",
                    ["version"] = NavisPluginApp.Version,
                    ["updated"] = DateTime.UtcNow.ToString("o"),
                };
                File.WriteAllText(HandshakePath, payload.ToString());
            }
            catch { }
        }

        public static void Clear()
        {
            try { if (File.Exists(HandshakePath)) File.Delete(HandshakePath); } catch { }
        }
    }
}
