using System;
using System.Windows;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPPlugin.Core;

namespace RevitMCPPlugin.UI.Tools
{
    /// <summary>
    /// Executes Revit commands directly via ExternalEventManager — no AI or internet needed.
    /// Used by all tool windows to replace ChatWindow.OpenWithPrompt.
    /// </summary>
    public static class DirectExecutor
    {
        /// <summary>
        /// Execute a command directly through Revit's external event system.
        /// Shows a success/error dialog when complete.
        /// </summary>
        public static async void RunAsync(string commandName, JObject parameters, string friendlyName = null)
        {
            var displayName = friendlyName ?? commandName;
            var eventMgr = Core.Application.EventManagerInstance;

            if (eventMgr == null)
            {
                MessageBox.Show(
                    "MCP Event Manager is not initialized.\nPlease start the MCP Service first.",
                    "Service Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var result = await eventMgr.ExecuteCommandAsync(commandName, parameters);
                var msg = result?["message"]?.ToString()
                    ?? result?.ToString(Newtonsoft.Json.Formatting.Indented)
                    ?? "Command completed successfully.";

                // Truncate for display
                if (msg.Length > 1500) msg = msg.Substring(0, 1500) + "\n\n... (output truncated)";

                TaskDialog.Show($"✅ {displayName}", msg);
            }
            catch (TimeoutException)
            {
                TaskDialog.Show($"⏱️ {displayName}",
                    "The command timed out. Revit may be busy with another operation.\n" +
                    "Please try again when Revit is idle.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show($"❌ {displayName}",
                    $"Command failed:\n{ex.Message}\n\n" +
                    "This tool may not be fully implemented yet for offline use.\n" +
                    "You can still use it via AI Chat when connected.");
            }
        }

        /// <summary>
        /// Simple helper to build a JObject from key-value pairs.
        /// </summary>
        public static JObject Params(params (string key, object value)[] pairs)
        {
            var obj = new JObject();
            foreach (var (key, value) in pairs)
            {
                if (value == null) continue;
                if (value is string s && string.IsNullOrWhiteSpace(s)) continue;
                if (value is bool b) obj[key] = b;
                else if (value is int i) obj[key] = i;
                else if (value is double d) obj[key] = d;
                else obj[key] = value.ToString();
            }
            return obj;
        }
    }
}
