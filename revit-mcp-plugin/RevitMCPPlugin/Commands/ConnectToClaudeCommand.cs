using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPPlugin.AI;
using RevitMCPPlugin.Core;

namespace RevitMCPPlugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ConnectToClaudeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // 1. Check if we have an API key configured in Revit MCP
                var settings = GeminiSettings.Load();
                var apiKey = settings.ApiKey;
                
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    TaskDialog.Show("Connect to Claude", "No API Key found.\n\nPlease go to Revit MCP -> Settings first and configure your Google Gemini API Key. Claude needs this key to power the tools.");
                    return Result.Cancelled;
                }

                // 2. Find Claude config path
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var claudeConfigDir = Path.Combine(appData, "Claude");
                var claudeConfigPath = Path.Combine(claudeConfigDir, "claude_desktop_config.json");

                // 3. Find Node.js and Server Index.js paths
                var pluginDllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                var appDir = Path.GetDirectoryName(pluginDllDir) ?? ""; // One level up from 'plugin'
                
                var nodePath = Path.Combine(appDir, "nodejs", "node.exe");
                var indexPath = Path.Combine(appDir, "server", "build", "index.js");

                // If running in Dev environment (e.g. revit-mcp-plugin\RevitMCPPlugin\bin\Release\net48)
                if (!File.Exists(indexPath))
                {
                    // For dev, assume node is in PATH and try finding the server directory relatively
                    nodePath = "node";
                    
                    var devRootDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(pluginDllDir))));
                    if (devRootDir != null)
                    {
                        var devIndexPath = Path.Combine(devRootDir, "revit-mcp-server", "build", "index.js");
                        if (File.Exists(devIndexPath))
                        {
                            indexPath = devIndexPath;
                        }
                    }
                }

                if (!File.Exists(indexPath) && nodePath != "node")
                {
                    TaskDialog.Show("Connect to Claude", $"Could not locate the MCP Server automatically.\n\nExpected it at: {indexPath}\n\nYou can manually add it to Claude's config.");
                    return Result.Failed;
                }

                // 4. Update or create the Claude JSON config
                if (!Directory.Exists(claudeConfigDir))
                {
                    Directory.CreateDirectory(claudeConfigDir);
                }

                JObject config;
                if (File.Exists(claudeConfigPath))
                {
                    try
                    {
                        var json = File.ReadAllText(claudeConfigPath);
                        config = JObject.Parse(json);
                    }
                    catch
                    {
                        config = new JObject();
                    }
                }
                else
                {
                    config = new JObject();
                }

                var mcpServers = config["mcpServers"] as JObject;
                if (mcpServers == null)
                {
                    mcpServers = new JObject();
                    config["mcpServers"] = mcpServers;
                }

                // Create the revit-mcp entry
                var revitMcp = new JObject
                {
                    ["command"] = nodePath,
                    ["args"] = new JArray { indexPath },
                    ["env"] = new JObject
                    {
                        ["GOOGLE_API_KEY"] = apiKey
                    }
                };

                mcpServers["revit-mcp"] = revitMcp;

                // Save config
                File.WriteAllText(claudeConfigPath, config.ToString(Newtonsoft.Json.Formatting.Indented));

                TaskDialog.Show("Connect to Claude", 
                    "✅ Claude Desktop has been successfully configured to use Revit MCP!\n\n" +
                    "To apply the changes:\n" +
                    "1. Completely restart the Claude Desktop App (File -> Exit, then open again)\n" +
                    "2. Start the MCP Service in Revit\n" +
                    "3. Ask Claude to 'Use the Revit MCP to list the walls in the model'.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Connect to Claude Error", $"Failed to configure Claude Desktop:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
