using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Batch export of clash images: one red/green, isolated JPEG per clash plus clashes.csv.
    /// Shared by the "Clash Snapshots" ribbon button and the navis_export_clash_snapshots command.
    /// Must run on the Navisworks UI thread.
    /// </summary>
    public static partial class NavisCommandExecutor
    {
        internal const int MaxSnapshotsPerAiCall = 250;

        /// <summary>Names of all clash tests in the active document.</summary>
        internal static List<string> ClashTestNames()
        {
            var doc = ActiveDoc();
            return AllTests(ClashTestsOf(ClashData(doc))).Select(t => t.DisplayName ?? "").ToList();
        }

        /// <summary>Default output folder: Documents\BIM-Bot Clash Snapshots\&lt;test&gt;_&lt;yyyyMMdd_HHmm&gt;.</summary>
        internal static string DefaultSnapshotFolder(string testName) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BIM-Bot Clash Snapshots",
            $"{SafeFileName(testName)}_{DateTime.Now:yyyyMMdd_HHmm}");

        internal static string SafeFileName(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            var clean = new string((name ?? "").Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
            if (clean.Length > 80) clean = clean.Substring(0, 80);
            return clean.Length == 0 ? "clash" : clean;
        }

        /// <summary>"" or "all" = every status; "open" = New + Active; otherwise one status name.</summary>
        private static bool StatusMatches(ClashResult r, string filter)
        {
            var f = (filter ?? "").Trim().ToLowerInvariant();
            if (f == "" || f == "all") return true;
            if (f == "open") return r.Status == ClashResultStatus.New || r.Status == ClashResultStatus.Active;
            return r.Status.ToString().Equals(f, StringComparison.OrdinalIgnoreCase);
        }

        private static string Csv(string? value)
        {
            var v = value ?? "";
            return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        /// <summary>
        /// Saves one image per matching clash (up to maxImages) into folder, plus clashes.csv.
        /// progress(done, total) is called before each image; return false to cancel.
        /// Camera and selection are restored at the end, even on error or cancel.
        /// </summary>
        internal static JObject ExportClashSnapshots(string testName, string statusFilter, string folder,
            int maxImages, int width, int height, Func<int, int, bool>? progress)
        {
            var doc = ActiveDoc();
            if (doc.ActiveView == null) throw new Exception("No active 3D view.");
            var test = FindTest(ClashData(doc), testName);
            if (string.IsNullOrWhiteSpace(folder)) folder = DefaultSnapshotFolder(test.DisplayName ?? testName);
            Directory.CreateDirectory(folder);
            var all = new List<ClashResult>();
            CollectResults(test.Children, all);
            var matching = all.Where(r => StatusMatches(r, statusFilter)).ToList();
            var picked = matching.Take(Math.Max(1, maxImages)).ToList();
            var cameraBefore = doc.CurrentViewpoint.CreateCopy();
            var selectionBefore = new ModelItemCollection();
            selectionBefore.CopyFrom(doc.CurrentSelection.SelectedItems);
            var opts = new JObject { ["width"] = width, ["height"] = height, ["format"] = "jpeg", ["seeThroughSecond"] = true };
            var csv = new StringBuilder();
            csv.AppendLine("index,clash,status,distance,item1,item2,image");
            int saved = 0, failed = 0;
            bool cancelled = false;
            var sw = Stopwatch.StartNew();
            var csvPath = Path.Combine(folder, "clashes.csv");
            try
            {
                for (int i = 0; i < picked.Count; i++)
                {
                    if (progress != null && !progress(i, picked.Count)) { cancelled = true; break; }
                    var r = picked[i];
                    var file = $"{i + 1:0000}_{SafeFileName(r.DisplayName ?? "clash")}.jpg";
                    try
                    {
                        var items = new List<ModelItem>();
                        if (r.Item1 != null) items.Add(r.Item1);
                        if (r.Item2 != null) items.Add(r.Item2);
                        if (items.Count == 0) throw new Exception("clash has no items");
                        var vp = doc.CurrentViewpoint.CreateCopy();
                        vp.ZoomBox(ClashZoomBox(r, items));
                        ApplyViewpoint(doc, vp);
                        var painted = new List<(ModelItem item, Autodesk.Navisworks.Api.Color color)>();
                        if (r.Item1 != null) painted.Add((r.Item1, Red));
                        if (r.Item2 != null) painted.Add((r.Item2, Green));
                        var shot = Highlighted(doc, painted, opts);
                        File.WriteAllBytes(Path.Combine(folder, file), Convert.FromBase64String(shot.Value<string>("base64") ?? ""));
                        saved++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        file = "ERROR: " + ex.Message;
                    }
                    csv.AppendLine(string.Join(",",
                        (i + 1).ToString(CultureInfo.InvariantCulture),
                        Csv(r.DisplayName),
                        Csv(r.Status.ToString()),
                        r.Distance.ToString("0.###", CultureInfo.InvariantCulture),
                        Csv(r.Item1?.DisplayName),
                        Csv(r.Item2?.DisplayName),
                        Csv(file)));
                }
                if (!cancelled) progress?.Invoke(picked.Count, picked.Count);
            }
            finally
            {
                try { ApplyViewpoint(doc, cameraBefore); } catch { }
                try { doc.CurrentSelection.CopyFrom(selectionBefore); } catch { }
                File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(true));
            }
            sw.Stop();
            NavisLogger.Log($"Clash snapshots: test '{test.DisplayName}', {saved} saved, {failed} failed, cancelled={cancelled}, {sw.ElapsedMilliseconds} ms -> {folder}");
            return new JObject
            {
                ["test"] = test.DisplayName ?? "",
                ["folder"] = folder,
                ["csv"] = csvPath,
                ["totalClashes"] = all.Count,
                ["matching"] = matching.Count,
                ["attempted"] = saved + failed,
                ["saved"] = saved,
                ["failed"] = failed,
                ["cancelled"] = cancelled,
                ["truncated"] = matching.Count > picked.Count,
                ["elapsedMs"] = sw.ElapsedMilliseconds,
            };
        }

        /// <summary>Socket command navis_export_clash_snapshots (AI tool). Capped at MaxSnapshotsPerAiCall.</summary>
        private static JObject ExportClashSnapshotsCommand(JObject p)
        {
            var test = Str(p, "test");
            if (string.IsNullOrWhiteSpace(test)) throw new Exception("test is required (name or GUID, from navis_list_clash_tests).");
            var max = Math.Max(1, Math.Min(MaxSnapshotsPerAiCall, Int(p, "maxImages", 100)));
            var w = Math.Max(320, Math.Min(2560, Int(p, "width", 1280)));
            var h = Math.Max(240, Math.Min(1600, Int(p, "height", 720)));
            return ExportClashSnapshots(test, Str(p, "statusFilter", "open"), Str(p, "folder"), max, w, h, null);
        }
    }
}
