using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    public static partial class CommandExecutor
    {
        private static JToken DiffLinkedMilestones(Document doc, JObject parameters)
        {
            var linkInstanceName = parameters["linkInstanceName"]?.ToString();
            var baselineSnapshotPath = parameters["baselineSnapshotPath"]?.ToString();
            var saveSnapshot = parameters["saveSnapshot"]?.Value<bool>() ?? false;

            var tgtDoc = string.IsNullOrEmpty(linkInstanceName) ? doc : 
                new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>().FirstOrDefault(l => l.Name.Contains(linkInstanceName))?.GetLinkDocument();

            if (tgtDoc == null) return new JObject { ["error"] = "Target document not found" };

            var elements = new FilteredElementCollector(tgtDoc).WhereElementIsNotElementType()
                .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model && e.get_BoundingBox(null) != null).ToList();

            var currentSnapshot = new JObject
            {
                ["linkName"] = tgtDoc.Title,
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };
            var elemsArr = new JArray();
            foreach(var e in elements)
            {
                var bb = e.get_BoundingBox(null);
                elemsArr.Add(new JObject
                {
                    ["uniqueId"] = e.UniqueId,
                    ["category"] = e.Category.Name,
                    ["bbMinX"] = bb.Min.X, ["bbMinY"] = bb.Min.Y, ["bbMinZ"] = bb.Min.Z,
                    ["bbMaxX"] = bb.Max.X, ["bbMaxY"] = bb.Max.Y, ["bbMaxZ"] = bb.Max.Z
                });
            }
            currentSnapshot["elements"] = elemsArr;

            if (saveSnapshot)
            {
                var savePath = Path.Combine(Path.GetTempPath(), $"snapshot_{tgtDoc.Title}_{DateTime.Now.Ticks}.json");
                File.WriteAllText(savePath, currentSnapshot.ToString());
                currentSnapshot["savedPath"] = savePath;
            }

            if (!string.IsNullOrEmpty(baselineSnapshotPath) && File.Exists(baselineSnapshotPath))
            {
                var baseline = JObject.Parse(File.ReadAllText(baselineSnapshotPath));
                var baselineElems = baseline["elements"] as JArray;
                
                // Group-then-take-first rather than ToDictionary: a snapshot can
                // legitimately repeat a uniqueId (linked instances, corrupt baseline),
                // and ToDictionary would throw ArgumentException on the whole diff.
                var currentDict = elemsArr
                    .GroupBy(k => k["uniqueId"]?.ToString() ?? "")
                    .Where(g => g.Key.Length > 0)
                    .ToDictionary(g => g.Key, g => g.First());
                var baselineDict = (baselineElems ?? new JArray())
                    .GroupBy(k => k["uniqueId"]?.ToString() ?? "")
                    .Where(g => g.Key.Length > 0)
                    .ToDictionary(g => g.Key, g => g.First());

                int added = 0;
                int deleted = 0;
                int modified = 0;

                foreach (var kvp in currentDict)
                {
                    if (!baselineDict.ContainsKey(kvp.Key)) added++;
                    else
                    {
                        var b = baselineDict[kvp.Key];
                        var c = kvp.Value;
                        // Compare all six bounds - checking X alone misses every
                        // pure Y or Z move, and every vertical resize.
                        if (BoundsDiffer(b, c))
                        {
                            modified++;
                        }
                    }
                }

                foreach (var kvp in baselineDict)
                {
                    if (!currentDict.ContainsKey(kvp.Key)) deleted++;
                }

                return new JObject
                {
                    ["added"] = added,
                    ["deleted"] = deleted,
                    ["modified"] = modified,
                    ["totalCurrent"] = currentDict.Count,
                    ["totalBaseline"] = baselineDict.Count
                };
            }

            return currentSnapshot;
        }

        private const double GeometryToleranceFt = 0.01;

        /// <summary>True when any bounding-box bound moved more than the tolerance.</summary>
        private static bool BoundsDiffer(JToken baseline, JToken current)
        {
            foreach (var key in new[] { "bbMinX", "bbMinY", "bbMinZ", "bbMaxX", "bbMaxY", "bbMaxZ" })
            {
                var b = baseline[key]?.Value<double>();
                var c = current[key]?.Value<double>();
                if (b == null || c == null) continue;
                if (Math.Abs(b.Value - c.Value) > GeometryToleranceFt) return true;
            }
            return false;
        }

    }
}
