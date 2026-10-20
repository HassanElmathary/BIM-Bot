using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Revit side of the manual bridge session (%LocalAppData%\BIMBot\bridge-session.json).
    /// The Navisworks "BIM-Bot Bridge" button creates the session (master); the Revit "Join Bridge"
    /// button adds this Revit document as a slave (slot 01, 02, …) or removes it.
    /// </summary>
    public static class BridgeSessionFile
    {
        public static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BIMBot", "bridge-session.json");

        private static int Pid => Process.GetCurrentProcess().Id;

        /// <summary>The session if a live Navisworks master owns it; otherwise null.</summary>
        public static JObject? ReadLive()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var s = JObject.Parse(File.ReadAllText(FilePath));
                var masterPid = s["master"]?["pid"]?.Value<int>() ?? 0;
                return ProcessAlive(masterPid) ? s : null;
            }
            catch
            {
                return null;
            }
        }

        public static List<string> Models(JObject session) =>
            (session["master"]?["models"] as JArray)?
                .Select(m => m["name"]?.ToString() ?? "")
                .Where(n => n != "")
                .ToList() ?? new List<string>();

        /// <summary>This Revit process's slave entry for the document, or null.</summary>
        public static JObject? FindSlave(JObject session, string documentTitle) =>
            (session["slaves"] as JArray)?
                .OfType<JObject>()
                .FirstOrDefault(x => x["pid"]?.Value<int>() == Pid
                    && string.Equals(x["documentTitle"]?.ToString(), documentTitle, StringComparison.OrdinalIgnoreCase));

        /// <summary>Lowest slot 01..99 not used by a live slave.</summary>
        public static string NextSlot(JObject session)
        {
            var used = new HashSet<string>(LiveSlaves(session).Select(x => x["slot"]?.ToString() ?? ""));
            return Enumerable.Range(1, 99).Select(i => i.ToString("00")).First(s => !used.Contains(s));
        }

        /// <summary>Adds this document as a slave (dropping slaves of exited Revit processes) and returns its slot.</summary>
        public static string Join(JObject session, string documentTitle, string documentPath, string model, int port)
        {
            var slaves = new JArray(LiveSlaves(session));
            var slot = NextSlot(session);
            slaves.Add(new JObject
            {
                ["slot"] = slot,
                ["pid"] = Pid,
                ["port"] = port,
                ["documentTitle"] = documentTitle,
                ["documentPath"] = documentPath,
                ["model"] = model,
                ["joinedUtc"] = DateTime.UtcNow.ToString("o"),
            });
            session["slaves"] = slaves;
            Write(session);
            Logger.Log($"Bridge: '{documentTitle}' joined as slave {slot} -> {model}");
            return slot;
        }

        public static void Leave(JObject session, string documentTitle)
        {
            var me = FindSlave(session, documentTitle);
            if (me == null) return;
            me.Remove();
            Write(session);
            Logger.Log($"Bridge: '{documentTitle}' left");
        }

        /// <summary>Removes every slave of this Revit process (called on shutdown). Never throws.</summary>
        public static void LeaveAllForThisProcess()
        {
            try
            {
                var s = ReadLive();
                if (s == null || !(s["slaves"] is JArray arr)) return;
                var mine = arr.OfType<JObject>().Where(x => x["pid"]?.Value<int>() == Pid).ToList();
                if (mine.Count == 0) return;
                foreach (var x in mine) x.Remove();
                Write(s);
            }
            catch { }
        }

        /// <summary>Best model for a document title: same name without extension, else one name is a prefix of the other.</summary>
        public static string? BestMatch(string documentTitle, IEnumerable<string> models)
        {
            var list = models.ToList();
            var t = Key(documentTitle);
            if (t == "") return null;
            return list.FirstOrDefault(m => Key(m) == t)
                ?? list.FirstOrDefault(m =>
                {
                    var k = Key(m);
                    return k != "" && (t.StartsWith(k, StringComparison.Ordinal) || k.StartsWith(t, StringComparison.Ordinal));
                });
        }

        private static string Key(string name) =>
            Regex.Replace(Path.GetFileName(name ?? "").Trim(), @"\.(nwc|nwd|nwf|rvt)$", "", RegexOptions.IgnoreCase).ToLowerInvariant();

        private static IEnumerable<JObject> LiveSlaves(JObject session) =>
            (session["slaves"] as JArray)?.OfType<JObject>().Where(x => ProcessAlive(x["pid"]?.Value<int>() ?? 0)).ToList()
            ?? new List<JObject>();

        private static bool ProcessAlive(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                using (var p = Process.GetProcessById(pid)) return !p.HasExited;
            }
            catch
            {
                return false;
            }
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
