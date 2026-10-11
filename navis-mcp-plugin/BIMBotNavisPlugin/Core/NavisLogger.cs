using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>Append-only file logger (never throws — logging must not break the host).</summary>
    public static class NavisLogger
    {
        private static string LogPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BIMBot", "logs", "navis-plugin.log");

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        public static void LogError(string message, Exception? ex = null)
        {
            Log($"ERROR: {message}" + (ex != null ? $" — {ex.GetType().Name}: {ex.Message}" : ""));
        }
    }
}
