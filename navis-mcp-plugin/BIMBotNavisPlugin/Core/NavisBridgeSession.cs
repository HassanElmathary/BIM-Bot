using System;
using System.Diagnostics;
using System.IO;
using Autodesk.Navisworks.Api;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Manual bridge session: this Navisworks is the MASTER, Revit documents join as SLAVES.
    /// Shared through %LocalAppData%\BIMBot\bridge-session.json. Navisworks creates it from the
    /// "BIM-Bot Bridge" button and lists the models of the open (federated) file; Revit's
    /// "Join Bridge" button adds slaves; the MCP server reads it to route clash elements.
    /// </summary>
    public static class NavisBridgeSession
    {
        public static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BIMBot", "bridge-session.json");

        private static int Pid => Process.GetCurrentProcess().Id;

        public static JObject? Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return JObject.Parse(File.ReadAllText(FilePath));
            }
            catch { return null; }
        }

        public static bool IsActiveForThisProcess() => Read()?["master"]?["pid"]?.Value<int>() == Pid;

        /// <summary>Starts a new session with this Navisworks as master (replaces any previous session).</summary>
        public static JObject Start(Document doc)
        {
            var models = ListModels(doc);
            var session = new JObject
            {
                ["version"] = 1,
                ["sessionId"] = Guid.NewGuid().ToString("N").Substring(0, 12),
                ["startedUtc"] = DateTime.UtcNow.ToString("o"),
                ["master"] = new JObject
                {
                    ["pid"] = Pid,
                    ["port"] = NavisPluginApp.ActivePort,
                    ["file"] = Path.GetFileName(doc.FileName ?? ""),
                    ["models"] = models,
                },
                ["slaves"] = new JArray(),
            };
            Write(session);
            NavisLogger.Log($"Bridge session {session["sessionId"]} started as master ({models.Count} models)");
            return session;
        }

        /// <summary>Ends the session if this Navisworks owns it.</summary>
        public static void Stop()
        {
            try
            {
                if (!IsActiveForThisProcess()) return;
                File.Delete(FilePath);
                NavisLogger.Log("Bridge session stopped");
            }
            catch { }
        }

        /// <summary>
        /// Discipline models: the file nodes (Item > Internal Type = LcOaPartition) directly under each
        /// model root — the NWCs inside a federated NWD — or the model root itself when it has none.
        /// </summary>
        public static JArray ListModels(Document doc)
        {
            var arr = new JArray();
            foreach (Model m in doc.Models)
            {
                var root = m.RootItem;
                if (root == null) continue;
                bool any = false;
                foreach (ModelItem child in root.Children)
                {
                    if (Prop(child, "Item", "Internal Type") != "LcOaPartition") continue;
                    arr.Add(ModelJson(child));
                    any = true;
                }
                if (!any) arr.Add(ModelJson(root));
            }
            return arr;
        }

        private static JObject ModelJson(ModelItem file)
        {
            var src = Prop(file, "Item", "Source File Name");
            var name = file.DisplayName ?? "";
            if (name == "" && src != "") name = Path.GetFileName(src);
            return new JObject
            {
                ["name"] = name,
                ["revitFile"] = src.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(src) : "",
            };
        }

        private static string Prop(ModelItem item, string tab, string property)
        {
            try
            {
                return item.PropertyCategories.FindPropertyByDisplayName(tab, property)?.Value?.ToDisplayString() ?? "";
            }
            catch { return ""; }
        }

        private static void Write(JObject session)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, session.ToString());
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }
    }
}
