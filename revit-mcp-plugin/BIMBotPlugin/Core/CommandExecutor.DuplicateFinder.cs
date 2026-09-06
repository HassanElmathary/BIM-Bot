using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Duplicate detection — the engine behind find_duplicates. Runs one or more
    /// detectors over a scope and returns groups of elements that duplicate each
    /// other: coincident geometry, exact family copies, identical parameter
    /// signatures, or clashing room/area numbers.
    ///
    /// Reporting is the default. Selecting is reversible. Deleting requires an
    /// explicit confirm token and always keeps one element per group.
    /// </summary>
    public static partial class CommandExecutor
    {
        private const double FdDefaultTolerance = 0.01;   // feet
        private const int FdDefaultPreview = 50;
        private const string FdSep = "\u0001";

        private static readonly string[] FdAllModes = { "geometry", "identical", "parameters", "roomNumber" };

        private static JToken FindDuplicates(UIDocument uidoc, Document doc, JObject parameters)
        {
            var modes = FdResolveModes(parameters);
            var tolerance = parameters["tolerance"]?.Value<double>() ?? FdDefaultTolerance;
            if (tolerance < 0) throw new InvalidOperationException("'tolerance' must not be negative");

            var action = (parameters["action"]?.ToString() ?? "report").ToLowerInvariant();
            if (action != "report" && action != "select" && action != "delete")
                throw new InvalidOperationException("'action' must be report (default), select or delete");

            var previewLimit = parameters["previewLimit"]?.Value<int>() ?? FdDefaultPreview;
            var scope = FdResolveScope(uidoc, doc, parameters, modes);

            var groups = new List<FdGroup>();
            foreach (var mode in modes)
            {
                switch (mode)
                {
                    case "geometry":
                        groups.AddRange(FdByGeometry(doc, scope, tolerance, strict: false));
                        break;
                    case "identical":
                        groups.AddRange(FdByGeometry(doc, scope, tolerance, strict: true));
                        break;
                    case "parameters":
                        groups.AddRange(FdByParameters(doc, scope, parameters));
                        break;
                    case "roomNumber":
                        groups.AddRange(FdByRoomNumber(scope, parameters));
                        break;
                }
            }

            // An element can surface in several modes. Keepers win over extras
            // everywhere, so nothing another group relies on gets deleted.
            var keepers = new HashSet<ElementId>(groups.Select(g => g.KeepId));
            var extras = groups.SelectMany(g => g.Members.Select(m => m.Id))
                               .Where(id => !keepers.Contains(id))
                               .Distinct()
                               .ToList();

            var result = new JObject
            {
                ["modes"] = new JArray(modes.Cast<object>().ToArray()),
                ["tolerance"] = tolerance,
                ["action"] = action,
                ["scopeCount"] = scope.Count,
                ["groupCount"] = groups.Count,
                ["duplicateCount"] = extras.Count,
                ["groups"] = new JArray(groups.Take(previewLimit).Select(g => g.ToJson(doc)).Cast<object>().ToArray()),
                ["truncated"] = groups.Count > previewLimit
            };

            var modeList = string.Join(", ", modes);
            var message = groups.Count == 0
                ? $"✅ No duplicates found in {scope.Count} element(s) [{modeList}]"
                : $"⚠️ {groups.Count} duplicate group(s) covering {extras.Count} redundant element(s) in {scope.Count} scoped element(s) [{modeList}]";

            if (groups.Count > 0 && action == "select")
            {
                if (uidoc == null) throw new InvalidOperationException("No active UI document — cannot select");
                uidoc.Selection.SetElementIds(extras);
                result["selected"] = extras.Count;
                message += $" — selected the {extras.Count} redundant element(s) in Revit; the keeper of each group is left unselected";
            }
            else if (groups.Count > 0 && action == "delete")
            {
                var confirm = parameters["confirm"]?.ToString() ?? "";
                if (!string.Equals(confirm, "DELETE", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Refusing to delete {extras.Count} element(s) without confirmation. " +
                        "Review the reported groups first, then re-run with confirm=\"DELETE\". " +
                        "Deletion cannot be undone through this tool.");

                List<string> errors;
                var deleted = FdDelete(doc, extras, out errors);

                result["deleted"] = deleted.Count;
                result["deletedIds"] = new JArray(deleted.Select(id => (object)id.Val()).ToArray());
                result["errors"] = new JArray(errors.Take(previewLimit).Cast<object>().ToArray());

                message += $" — deleted {deleted.Count} element(s)";
                if (deleted.Count > extras.Count)
                    message += $" (includes {deleted.Count - extras.Count} hosted/dependent element(s) Revit removed alongside them)";
                if (errors.Count > 0) message += $", {errors.Count} could not be deleted";
            }

            result["message"] = message;
            return result;
        }

        // ── Setup ────────────────────────────────────────────────────

        private static List<string> FdResolveModes(JObject parameters)
        {
            var raw = new List<string>();
            if (parameters["modes"] is JArray arr) raw.AddRange(arr.Select(t => t.ToString()));
            var single = parameters["mode"]?.ToString();
            if (!string.IsNullOrWhiteSpace(single)) raw.Add(single!);
            if (raw.Count == 0) raw.Add("geometry");

            var modes = new List<string>();
            foreach (var entry in raw)
            {
                if (string.Equals(entry, "all", StringComparison.OrdinalIgnoreCase))
                {
                    modes.AddRange(FdAllModes);
                    continue;
                }
                var match = FdAllModes.FirstOrDefault(m => string.Equals(m, entry, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    throw new InvalidOperationException(
                        $"Unknown mode '{entry}'. Valid modes: {string.Join(", ", FdAllModes)}, all");
                modes.Add(match);
            }
            return modes.Distinct().ToList();
        }

        private static List<Element> FdResolveScope(UIDocument uidoc, Document doc, JObject parameters, List<string> modes)
        {
            bool hasScope = parameters["category"] != null
                         || parameters["categories"] != null
                         || parameters["elementIds"] != null
                         || parameters["useSelection"]?.Value<bool>() == true;

            if (hasScope) return PbeResolveScope(uidoc, doc, parameters);

            // roomNumber is the one detector with an obvious default scope.
            if (modes.All(m => m == "roomNumber"))
            {
                var cats = new[] { BuiltInCategory.OST_Rooms, BuiltInCategory.OST_Areas, BuiltInCategory.OST_MEPSpaces };
                return cats.SelectMany(bic => new FilteredElementCollector(doc)
                                                  .OfCategory(bic)
                                                  .WhereElementIsNotElementType()
                                                  .ToList())
                           .ToList();
            }

            throw new InvalidOperationException(
                "Provide a scope: 'category'/'categories', 'elementIds', or useSelection=true. " +
                "Sweeping every category at once is too slow to be useful — check one category at a time.");
        }

        // ── Detector: coincident geometry ─────────────────────────────

        /// <summary>
        /// Groups elements of the same category and type whose geometry sits in the
        /// same place within <paramref name="tolerance"/>. With
        /// <paramref name="strict"/>, family instances must also share host, level
        /// and flip state — the signature of a paste-in-place copy.
        /// </summary>
        private static List<FdGroup> FdByGeometry(Document doc, List<Element> scope, double tolerance, bool strict)
        {
            var groups = new List<FdGroup>();
            var buckets = new Dictionary<string, List<KeyValuePair<Element, FdGeom>>>();

            foreach (var elem in scope)
            {
                var geom = FdGeometryOf(elem);
                if (geom == null) continue;

                var key = FdBucketKey(elem, geom.Kind, strict);
                List<KeyValuePair<Element, FdGeom>>? list;
                if (!buckets.TryGetValue(key, out list))
                    buckets[key] = list = new List<KeyValuePair<Element, FdGeom>>();
                list.Add(new KeyValuePair<Element, FdGeom>(elem, geom));
            }

            foreach (var bucket in buckets.Values)
            {
                if (bucket.Count < 2) continue;

                // Sweep on X so each element only compares against near neighbours.
                var sorted = bucket.OrderBy(b => b.Value.Points[0].X).ToList();
                var taken = new bool[sorted.Count];

                for (int i = 0; i < sorted.Count; i++)
                {
                    if (taken[i]) continue;
                    var seed = sorted[i];
                    List<Element>? matches = null;

                    for (int j = i + 1; j < sorted.Count; j++)
                    {
                        if (sorted[j].Value.Points[0].X - seed.Value.Points[0].X > tolerance) break;
                        if (taken[j]) continue;
                        if (!FdGeomEquals(seed.Value, sorted[j].Value, tolerance)) continue;

                        taken[j] = true;
                        if (matches == null) matches = new List<Element> { seed.Key };
                        matches.Add(sorted[j].Key);
                    }

                    if (matches == null) continue;
                    taken[i] = true;
                    groups.Add(new FdGroup(strict ? "identical" : "geometry", FdDescribe(seed.Value), matches));
                }
            }

            return groups;
        }

        private static string FdBucketKey(Element elem, string kind, bool strict)
        {
            var key = string.Join(FdSep,
                (elem.Category?.Id.Val() ?? 0).ToString(CultureInfo.InvariantCulture),
                elem.GetTypeId().Val().ToString(CultureInfo.InvariantCulture),
                kind);
            if (!strict) return key;

            var fi = elem as FamilyInstance;
            if (fi != null)
            {
                key += FdSep + (fi.Host == null ? "-1" : fi.Host.Id.Val().ToString(CultureInfo.InvariantCulture))
                     + FdSep + (fi.FacingFlipped ? "1" : "0")
                     + (fi.HandFlipped ? "1" : "0")
                     + (fi.Mirrored ? "1" : "0");
            }

            ElementId levelId;
            try { levelId = elem.LevelId; } catch { levelId = ElementId.InvalidElementId; }
            return key + FdSep + levelId.Val().ToString(CultureInfo.InvariantCulture);
        }

        private sealed class FdGeom
        {
            public string Kind = "";
            public List<XYZ> Points = new List<XYZ>();
        }

        private static FdGeom? FdGeometryOf(Element elem)
        {
            var lp = elem.Location as LocationPoint;
            if (lp != null)
                return new FdGeom { Kind = "point", Points = { lp.Point } };

            var lc = elem.Location as LocationCurve;
            if (lc != null && lc.Curve != null && lc.Curve.IsBound)
            {
                var a = lc.Curve.GetEndPoint(0);
                var b = lc.Curve.GetEndPoint(1);
                // Order endpoints so a wall drawn right-to-left still matches the
                // same wall drawn left-to-right.
                var ordered = FdCompare(a, b) <= 0
                    ? new List<XYZ> { a, b }
                    : new List<XYZ> { b, a };
                return new FdGeom { Kind = "curve", Points = ordered };
            }

            // Everything else (floors, roofs, closed-loop sketches) falls back to its
            // bounding box, which captures position and size together.
            var box = elem.get_BoundingBox(null);
            if (box == null) return null;
            return new FdGeom { Kind = "bbox", Points = { box.Min, box.Max } };
        }

        private static int FdCompare(XYZ a, XYZ b)
        {
            int c = a.X.CompareTo(b.X);
            if (c != 0) return c;
            c = a.Y.CompareTo(b.Y);
            return c != 0 ? c : a.Z.CompareTo(b.Z);
        }

        private static bool FdGeomEquals(FdGeom a, FdGeom b, double tolerance)
        {
            if (a.Kind != b.Kind || a.Points.Count != b.Points.Count) return false;
            for (int i = 0; i < a.Points.Count; i++)
                if (a.Points[i].DistanceTo(b.Points[i]) > tolerance) return false;
            return true;
        }

        private static string FdDescribe(FdGeom geom)
        {
            Func<XYZ, string> p = v => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", v.X, v.Y, v.Z);
            if (geom.Kind == "point") return "at " + p(geom.Points[0]);
            if (geom.Kind == "curve") return "from " + p(geom.Points[0]) + " to " + p(geom.Points[1]);
            return "bbox " + p(geom.Points[0]) + " to " + p(geom.Points[1]);
        }

        // ── Detector: identical parameter signature ───────────────────

        private static List<FdGroup> FdByParameters(Document doc, List<Element> scope, JObject parameters)
        {
            var names = (parameters["parameters"] as JArray)?.Select(t => t.ToString())
                                                            .Where(s => !string.IsNullOrWhiteSpace(s))
                                                            .ToList();
            if (names == null || names.Count == 0)
                throw new InvalidOperationException(
                    "Mode 'parameters' needs a 'parameters' array naming the values that form the signature, e.g. [\"Mark\"]");

            var ignoreEmpty = parameters["ignoreEmpty"]?.Value<bool>() ?? true;
            var buckets = new Dictionary<string, FdBucket>(StringComparer.OrdinalIgnoreCase);

            foreach (var elem in scope)
            {
                var values = new List<string>();
                bool allEmpty = true;

                foreach (var name in names)
                {
                    var p = PbeFindParameter(doc, elem, name, includeType: true);
                    var text = p == null ? "" : (PbeValueString(p) ?? "");
                    if (!string.IsNullOrWhiteSpace(text)) allEmpty = false;
                    values.Add(text.Trim());
                }

                // Blank Marks on every element aren't duplicates, just unfilled data.
                if (allEmpty && ignoreEmpty) continue;

                var key = FdKey(elem, values);
                FdBucket? bucket;
                if (!buckets.TryGetValue(key, out bucket))
                    buckets[key] = bucket = new FdBucket(
                        string.Join(", ", names.Zip(values, (n, v) => n + "='" + v + "'")));
                bucket.Members.Add(elem);
            }

            return FdHarvest(buckets, "parameters");
        }

        // ── Detector: clashing room / area numbers ────────────────────

        private static List<FdGroup> FdByRoomNumber(List<Element> scope, JObject parameters)
        {
            var byName = parameters["matchRoomName"]?.Value<bool>() ?? false;
            var buckets = new Dictionary<string, FdBucket>(StringComparer.OrdinalIgnoreCase);

            foreach (var elem in scope)
            {
                var number = FdRoomNumber(elem);
                if (string.IsNullOrWhiteSpace(number)) continue;

                var values = new List<string> { number!.Trim() };
                var label = "Number='" + number!.Trim() + "'";

                if (byName)
                {
                    var nameParam = elem.get_Parameter(BuiltInParameter.ROOM_NAME);
                    var roomName = ((nameParam == null ? null : nameParam.AsString()) ?? elem.Name ?? "").Trim();
                    values.Add(roomName);
                    label += ", Name='" + roomName + "'";
                }

                var key = FdKey(elem, values);
                FdBucket? bucket;
                if (!buckets.TryGetValue(key, out bucket))
                    buckets[key] = bucket = new FdBucket(label);
                bucket.Members.Add(elem);
            }

            return FdHarvest(buckets, "roomNumber");
        }

        private static string? FdRoomNumber(Element elem)
        {
            var p = elem.get_Parameter(BuiltInParameter.ROOM_NUMBER);
            if (p != null) return p.AsString();
            // Areas and spaces expose their number through a different built-in.
            var lookup = elem.LookupParameter("Number");
            return lookup == null ? null : lookup.AsString();
        }

        // ── Bucketing helpers ────────────────────────────────────────

        private sealed class FdBucket
        {
            public string Label { get; }
            public List<Element> Members { get; }

            public FdBucket(string label)
            {
                Label = label;
                Members = new List<Element>();
            }
        }

        /// <summary>
        /// Builds a collision-safe grouping key. Values are joined with a control
        /// character so a value containing the separator can't merge two buckets.
        /// </summary>
        private static string FdKey(Element elem, IEnumerable<string> values)
        {
            return (elem.Category?.Id.Val() ?? 0).ToString(CultureInfo.InvariantCulture)
                 + FdSep + string.Join(FdSep, values);
        }

        private static List<FdGroup> FdHarvest(Dictionary<string, FdBucket> buckets, string mode)
        {
            return buckets.Values.Where(b => b.Members.Count > 1)
                                 .Select(b => new FdGroup(mode, b.Label, b.Members))
                                 .ToList();
        }

        // ── Deletion ─────────────────────────────────────────────────

        private static List<ElementId> FdDelete(Document doc, List<ElementId> ids, out List<string> errors)
        {
            var deleted = new List<ElementId>();
            var errorList = new List<string>();

            using (var tx = new Transaction(doc, "Delete Duplicate Elements"))
            {
                tx.Start();
                try
                {
                    foreach (var id in ids)
                    {
                        // A previous delete may have taken this one with it — hosted
                        // families go when their host does.
                        if (doc.GetElement(id) == null) continue;
                        try
                        {
                            deleted.AddRange(doc.Delete(id));
                        }
                        catch (Exception ex)
                        {
                            errorList.Add("[" + id.Val() + "] " + ex.Message);
                        }
                    }
                    tx.Commit();
                }
                catch
                {
                    if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                    throw;
                }
            }

            errors = errorList;
            return deleted.Distinct().ToList();
        }

        // ── Result shaping ───────────────────────────────────────────

        private sealed class FdGroup
        {
            public string Mode { get; }
            public string Key { get; }
            public List<Element> Members { get; }
            public ElementId KeepId { get; }

            public FdGroup(string mode, string key, List<Element> members)
            {
                Mode = mode;
                Key = key;
                // Keep the lowest ID — the original, in creation order.
                Members = members.OrderBy(e => e.Id.Val()).ToList();
                KeepId = Members[0].Id;
            }

            public JObject ToJson(Document doc)
            {
                var elements = new JArray();
                foreach (var e in Members)
                {
                    var typeElem = doc.GetElement(e.GetTypeId()) as ElementType;
                    string levelName = "";
                    try
                    {
                        var lvl = doc.GetElement(e.LevelId);
                        levelName = lvl == null ? "" : (lvl.Name ?? "");
                    }
                    catch { /* not every element carries a level */ }

                    elements.Add(new JObject
                    {
                        ["elementId"] = e.Id.Val(),
                        ["name"] = e.Name ?? "",
                        ["category"] = e.Category?.Name ?? "",
                        ["typeName"] = typeElem == null ? "" : (typeElem.Name ?? ""),
                        ["level"] = levelName,
                        ["keep"] = e.Id == KeepId
                    });
                }

                return new JObject
                {
                    ["mode"] = Mode,
                    ["match"] = Key,
                    ["count"] = Members.Count,
                    ["keepElementId"] = KeepId.Val(),
                    ["elements"] = elements
                };
            }
        }
    }
}
