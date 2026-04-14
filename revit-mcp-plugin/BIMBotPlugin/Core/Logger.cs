using System;
using System.IO;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Simple file-based logger for the MCP plugin.
    /// </summary>
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BIMBot", "logs"
        );

        private static readonly string LogFile = Path.Combine(
            LogDir, $"bim-bot-{DateTime.Now:yyyy-MM-dd}.log"
        );

        static Logger()
        {
            try
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);
            }
            catch { }
        }

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss}] INFO  {message}\n");
            }
            catch { }
        }

        public static void LogError(string message, Exception? ex = null)
        {
            try
            {
                var text = $"[{DateTime.Now:HH:mm:ss}] ERROR {message}";
                if (ex != null) text += $"\n  {ex.GetType().Name}: {ex.Message}\n  {ex.StackTrace}";
                File.AppendAllText(LogFile, text + "\n");
            }
            catch { }
        }
    }
}
