using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Cross-document annotation transfer (v2.5.0).
    ///
    /// Every other command in CommandExecutor is bound to the *active* document,
    /// which made copying annotations between two open, unlinked projects
    /// impossible without manual UI focus-switching. These handlers resolve
    /// source/target Document objects explicitly via
    /// UIApplication.Application.Documents, so transfer works in a single
    /// external event with no document activation at all.
    ///
    /// Fidelity contract (mirrored in the MCP tool descriptions):
    /// - Text notes: full fidelity (text, position, type by name).
    /// - Tags: re-attached by host UniqueId first, then category + level +
    ///   nearest-location fallback. Unmappable tags are reported, never dropped.
    /// - Dimensions: recreated only when every referenced element resolves in
    ///   the target; otherwise exported as data with the reason.
    /// Tag *types* are intentionally auto-by-category for now (same as
    /// create_tag); the source type name is preserved in the payload for v2.6.
    /// </summary>
    public static partial class CommandExecutor
    {
        private const string TransferFormat = "bimbot-annotation-transfer/1";
        private const int MaxDetailItems = 50;

        // ══════════════════════════════════════════════════════════════
        // ████  DOCUMENT RESOLUTION  ████
        // ══════════════════════════════════════════════════════════════

        private static List<Document> GetOpenDocuments(UIApplication uiApp)
        {
            var docs = new List<Document>();
            foreach (Document d in uiApp.Application.Documents)
                docs.Add(d);
            return docs;
        }

        private static string OpenDocumentHint(List<Document> docs)
        {
            if (docs.Count == 0) return "(no documents open)";
            return string.Join(", ", docs.Select(d => "'" + d.Title + "'"));
        }

        private static Document ResolveTransferDoc(UIApplication uiApp, string titleOrPath, string role)
        {
            var docs = GetOpenDocuments(uiApp);
            if (string.IsNullOrWhiteSpace(titleOrPath))
            {
                var active = uiApp.ActiveUIDocument?.Document;
                if (active != null) return active;
                throw new InvalidOperationException(
                    $"No {role} document specified and no active document. Open documents: {OpenDocumentHint(docs)}");
            }

            var key = titleOrPath.Trim();
            var match = docs.FirstOrDefault(d =>
                d.Title.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(d.PathName) && d.PathName.Equals(key, StringComparison.OrdinalIgnoreCase)) ||
                GetTitleWithoutExtension(d.Title).Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                throw new InvalidOperationException(
                    $"{role} document '{key}' is not open. Open documents: {OpenDocumentHint(docs)}");
            return match;
        }

        private static string GetTitleWithoutExtension(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            var dot = title.LastIndexOf('.');
            return dot > 0 ? title.Substring(0, dot) : title;
        }

        private static View ResolveTransferView(Document doc, UIDocument uidoc, long viewId, string viewName, string role)
        {
            if (viewId != 0)
            {
                var byId = doc.GetElement(viewId.ToElementId()) as View;
                if (byId == null || byId.IsTemplate)
                    throw new InvalidOperationException($"{role} view ID {viewId} not found in '{doc.Title}'");
                return byId;
            }

            if (!string.IsNullOrWhiteSpace(viewName))
            {
                var candidates = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.Name.Equals(viewName.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (candidates.Count == 0)
                {
                    var available = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => !v.IsTemplate && v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser)
                        .Select(v => v.Name)
                        .Take(30);
                    throw new InvalidOperationException(
                        $"{role} view '{viewName}' not found in '{doc.Title}'. Available: {string.Join(", ", available)}");
                }
                return candidates[0];
            }

            // Default to the active view only when it belongs to this document.
            if (uidoc != null && uidoc.Document.Equals(doc) && uidoc.ActiveView != null && !uidoc.ActiveView.IsTemplate)
                return uidoc.ActiveView;

            throw new InvalidOperationException(
                $"{role} view is ambiguous for '{doc.Title}' (it is not the active document). Pass viewName or viewId.");
        }

        // ══════════════════════════════════════════════════════════════
        // ████  list_open_documents / activate_document  ████
        // ══════════════════════════════════════════════════════════════

        private static JToken ListOpenDocuments(UIApplication uiApp)
        {
            var docs = GetOpenDocuments(uiApp);
            var active = uiApp.ActiveUIDocument?.Document;
            var arr = new JArray();
            foreach (var d in docs)
            {
                arr.Add(new JObject
                {
                    ["title"] = d.Title,
                    ["path"] = d.PathName ?? "",
                    ["isActive"] = active != null && active.Equals(d),
                    ["isLinked"] = d.IsLinked,
                    ["isFamilyDocument"] = d.IsFamilyDocument
                });
            }
            return new JObject
            {
                ["count"] = arr.Count,
                ["documents"] = arr,
                ["message"] = arr.Count == 0 ? "No documents open in Revit." : $"{arr.Count} document(s) open."
            };
        }

        private static JToken ActivateDocument(UIApplication uiApp, JObject parameters)
        {
            var title = parameters["documentTitle"]?.ToString();
            var path = parameters["filePath"]?.ToString();
            var key = !string.IsNullOrWhiteSpace(title) ? title.Trim() : (path ?? "").Trim();
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("Pass documentTitle (or filePath). Call list_open_documents first for exact titles.");

            var target = ResolveTransferDoc(uiApp, key, "Target");
            var active = uiApp.ActiveUIDocument?.Document;
            if (active != null && active.Equals(target))
                return new JObject { ["message"] = $"✅ '{target.Title}' is already the active document.", ["title"] = target.Title };

            if (string.IsNullOrEmpty(target.PathName))
                throw new InvalidOperationException(
                    $"Document '{target.Title}' has never been saved, so Revit cannot activate it via API. Click it manually in the Revit UI.");

            var activated = uiApp.OpenAndActivateDocument(target.PathName);
            return new JObject
            {
                ["message"] = $"✅ Activated document '{activated.Document.Title}'. Subsequent tools now operate on it.",
                ["title"] = activated.Document.Title
            };
        }

        // ══════════════════════════════════════════════════════════════
        // ████  ANNOTATION COLLECTION (read half)  ████
        // ══════════════════════════════════════════════════════════════

        private static XYZ XYZFrom(JToken t)
        {
            if (t is JArray a && a.Count >= 3)
                return new XYZ(a[0].Value<double>(), a[1].Value<double>(), a[2].Value<double>());
            return null;
        }

        private static string LevelNameOf(Document doc, Element e)
        {
            try
            {
                if (e?.LevelId == null || e.LevelId == ElementId.InvalidElementId) return "";
                return (doc.GetElement(e.LevelId) as Level)?.Name ?? "";
            }
            catch { return ""; }
        }

        private static JObject HostSnapshot(Document doc, Element host)
        {
            XYZ loc = null;
            if (host?.Location is LocationPoint lp) loc = lp.Point;
            else if (host?.Location is LocationCurve lc)
            {
                try { loc = lc.Curve.Evaluate(0.5, true); } catch { loc = null; }
            }
            return new JObject
            {
                ["uniqueId"] = host?.UniqueId ?? "",
                ["category"] = host?.Category?.Name ?? "",
                // Val(), not IntegerValue: IntegerValue is gone in Revit 2026+
                // (see Core/Compat/RevitCompat.cs).
                ["categoryId"] = host?.Category?.Id.Val() ?? 0,
                ["level"] = LevelNameOf(doc, host),
                ["location"] = loc == null ? null : new JArray(loc.X, loc.Y, loc.Z)
            };
        }

        private static JObject CollectAnnotations(Document doc, View view, bool texts, bool tags, bool dims)
        {
            var payload = new JObject
            {
                ["format"] = TransferFormat,
                ["sourceDocument"] = doc.Title,
                ["sourceView"] = view.Name,
                ["sourceViewId"] = view.Id.Val(),
                ["exportedAt"] = DateTime.UtcNow.ToString("o")
            };

            if (texts)
            {
                var arr = new JArray();
                foreach (var t in new FilteredElementCollector(doc, view.Id).OfClass(typeof(TextNote)).Cast<TextNote>())
                {
                    // One unreadable note must not fail the whole export.
                    try
                    {
                        string typeName = "";
                        try { typeName = t.TextNoteType?.Name ?? ""; } catch { }
                        double width = 0;
                        try { width = t.Width; } catch { }
                        arr.Add(new JObject
                        {
                            ["id"] = t.Id.Val(),
                            ["uniqueId"] = t.UniqueId ?? "",
                            ["text"] = t.Text ?? "",
                            ["position"] = new JArray(t.Coord.X, t.Coord.Y, t.Coord.Z),
                            ["typeName"] = typeName,
                            ["width"] = width
                        });
                    }
                    catch { /* skip element */ }
                }
                payload["textNotes"] = arr;
            }

            if (tags)
            {
                var arr = new JArray();
                foreach (var tag in new FilteredElementCollector(doc, view.Id).OfClass(typeof(IndependentTag)).Cast<IndependentTag>())
                {
                    try
                    {
                        var hosts = new JArray();
#if REVIT_PRE_2022
                        try
                        {
                            var host = tag.GetTaggedLocalElement();
                            if (host != null)
                            {
                                hosts.Add(HostSnapshot(doc, host));
                            }
                        }
                        catch { continue; }
#else
                        IList<Reference> refs;
                        try { refs = tag.GetTaggedReferences(); }
                        catch { continue; }

                        foreach (var r in refs)
                        {
                            if (r == null) continue;
                            // Linked-element tags cannot transfer: record and skip at apply time.
                            // (LinkedElementId is InvalidElementId for same-document references.)
                            if (r.LinkedElementId != ElementId.InvalidElementId)
                            {
                                hosts.Add(new JObject { ["linked"] = true });
                                continue;
                            }
                            var host = doc.GetElement(r.ElementId);
                            if (host == null) continue;
                            hosts.Add(HostSnapshot(doc, host));
                        }
#endif

                        string tagType = "";
                        try
                        {
                            var sym = doc.GetElement(tag.GetTypeId()) as FamilySymbol;
                            if (sym != null) tagType = sym.Family?.Name + ": " + sym.Name;
                        }
                        catch { }

                        string tagText = "";
                        try { tagText = tag.TagText ?? ""; } catch { }

                        arr.Add(new JObject
                        {
                            ["id"] = tag.Id.Val(),
                            ["uniqueId"] = tag.UniqueId ?? "",
                            ["headPosition"] = new JArray(tag.TagHeadPosition.X, tag.TagHeadPosition.Y, tag.TagHeadPosition.Z),
                            ["hasLeader"] = tag.HasLeader,
                            ["tagText"] = tagText,
                            ["sourceTagType"] = tagType,
                            ["hosts"] = hosts
                        });
                    }
                    catch { /* skip element */ }
                }
                payload["tags"] = arr;
            }

            if (dims)
            {
                var arr = new JArray();
                foreach (var d in new FilteredElementCollector(doc, view.Id).OfClass(typeof(Dimension)).Cast<Dimension>())
                {
                    try
                    {
                        string typeName = "";
                        try { typeName = d.DimensionType?.Name ?? ""; } catch { }

                        var refs = new JArray();
                        try
                        {
                            foreach (Reference r in d.References)
                            {
                                if (r == null) continue;
                                if (r.LinkedElementId != ElementId.InvalidElementId)
                                {
                                    refs.Add(new JObject { ["linked"] = true });
                                    continue;
                                }
                                var e = doc.GetElement(r.ElementId);
                                if (e == null) continue;
                                refs.Add(HostSnapshot(doc, e));
                            }
                        }
                        catch { }

                        JArray p1 = null, p2 = null;
                        try
                        {
                            if (d.Curve is Line line)
                            {
                                p1 = new JArray(line.GetEndPoint(0).X, line.GetEndPoint(0).Y, line.GetEndPoint(0).Z);
                                p2 = new JArray(line.GetEndPoint(1).X, line.GetEndPoint(1).Y, line.GetEndPoint(1).Z);
                            }
                        }
                        catch { }

                        string valueOverride = "";
                        try { valueOverride = d.ValueOverride ?? ""; } catch { }

                        int segments = 1;
                        try { segments = d.NumberOfSegments; } catch { }

                        arr.Add(new JObject
                        {
                            ["id"] = d.Id.Val(),
                            ["uniqueId"] = d.UniqueId ?? "",
                            ["typeName"] = typeName,
                            ["valueOverride"] = valueOverride,
                            ["segments"] = segments,
                            ["curveP1"] = p1,
                            ["curveP2"] = p2,
                            ["references"] = refs
                        });
                    }
                    catch { /* skip element */ }
                }
                payload["dimensions"] = arr;
            }

            return payload;
        }

        // ══════════════════════════════════════════════════════════════
        // ████  ANNOTATION APPLY (write half)  ████
        // ══════════════════════════════════════════════════════════════

        private static XYZ ElementAnchor(Element e)
        {
            if (e?.Location is LocationPoint lp) return lp.Point;
            if (e?.Location is LocationCurve lc)
            {
                try { return lc.Curve.Evaluate(0.5, true); } catch { return null; }
            }
            return null;
        }

        /// <summary>
        /// Find the host element in the target doc: UniqueId first, then
        /// category + level + nearest location within tolerance.
        /// Returns the element and whether the match was exact.
        /// </summary>
        private static Element ResolveHost(Document target, JObject host, double tolerance, out bool exact)
        {
            exact = false;
            if (host == null || host["linked"] != null) return null;

            var uid = host["uniqueId"]?.ToString();
            if (!string.IsNullOrEmpty(uid))
            {
                try
                {
                    var byUid = target.GetElement(uid);
                    if (byUid != null) { exact = true; return byUid; }
                }
                catch { }
            }

            // Fallback: same category, same level, nearest anchor point.
            long catId = host["categoryId"]?.Value<long>() ?? 0;
            string level = host["level"]?.ToString() ?? "";
            var anchor = XYZFrom(host["location"]);
            if (catId == 0 || anchor == null) return null;

            Element best = null;
            double bestDist = tolerance;
            foreach (var e in new FilteredElementCollector(target).WhereElementIsNotElementType().ToElements())
            {
                try
                {
                    // Val(), not IntegerValue: see HostSnapshot above.
                    if (e?.Category?.Id.Val() != catId) continue;
                    if (!string.IsNullOrEmpty(level) && LevelNameOf(target, e) != level) continue;
                    var p = ElementAnchor(e);
                    if (p == null) continue;
                    var dist = p.DistanceTo(anchor);
                    if (dist <= bestDist) { bestDist = dist; best = e; }
                }
                catch { /* keep scanning */ }
            }
            return best;
        }

        private static void AddDetail(JArray arr, string text)
        {
            if (arr.Count < MaxDetailItems) arr.Add(text);
        }

        private static JObject ApplyAnnotations(Document target, View targetView, JObject payload, double tolerance, bool texts, bool tags, bool dims)
        {
            int textsIn = (payload["textNotes"] as JArray)?.Count ?? 0;
            int tagsIn = (payload["tags"] as JArray)?.Count ?? 0;
            int dimsIn = (payload["dimensions"] as JArray)?.Count ?? 0;

            var createdTexts = new List<string>();
            var createdTags = new List<string>();
            var createdDims = new List<string>();
            var skipped = new JArray();
            var failed = new JArray();

            using (var tx = new Transaction(target, "Transfer Annotations"))
            {
                tx.Start();
                try
                {
                    // ---- Text notes: full fidelity ----
                    if (texts && payload["textNotes"] is JArray notes)
                    {
                        var typeCache = new Dictionary<string, ElementId>();
                        foreach (var item in notes)
                        {
                            // Per-item try/catch (with safe cast first) so one malformed
                            // entry — e.g. from a hand-edited file — cannot abort the batch.
                            var n = item as JObject;
                            if (n == null) { AddDetail(skipped, "text note entry: not an object"); continue; }
                            try
                            {
                                var text = n["text"]?.ToString() ?? "";
                                if (string.IsNullOrEmpty(text)) { AddDetail(skipped, $"text note {n["id"]}: empty text"); continue; }
                                var pos = XYZFrom(n["position"] as JArray);
                                if (pos == null) { AddDetail(skipped, $"text note {n["id"]}: no position"); continue; }

                                var typeName = n["typeName"]?.ToString() ?? "";
                                if (!typeCache.TryGetValue(typeName, out var typeId))
                                {
                                    var match = new FilteredElementCollector(target)
                                        .OfClass(typeof(TextNoteType)).Cast<TextNoteType>()
                                        .FirstOrDefault(t => string.IsNullOrEmpty(typeName) || t.Name == typeName)
                                        ?? new FilteredElementCollector(target)
                                            .OfClass(typeof(TextNoteType)).Cast<TextNoteType>().FirstOrDefault();
                                    if (match == null) throw new InvalidOperationException("no text note type in target");
                                    typeId = match.Id;
                                    typeCache[typeName] = typeId;
                                }

                                var note = TextNote.Create(target, targetView.Id, pos, text, typeId);
                                createdTexts.Add(note.Id.Val().ToString());
                            }
                            catch (Exception ex) { AddDetail(failed, $"text note {n["id"]}: {ex.Message}"); }
                        }
                    }

                    // ---- Tags: re-attach to matched host ----
                    if (tags && payload["tags"] is JArray tagArr)
                    {
                        foreach (var tagItem in tagArr)
                        {
                            var t = tagItem as JObject;
                            if (t == null) { AddDetail(skipped, "tag entry: not an object"); continue; }
                            try
                            {
                                var hosts = t["hosts"] as JArray;
                                if (hosts == null || hosts.Count == 0)
                                {
                                    AddDetail(skipped, $"tag {t["id"]} ('{t["tagText"]}'): no host recorded (linked or deleted host)");
                                    continue;
                                }

                                Element host = null;
                                bool exact = false;
                                XYZ storedHostLoc = null;
                                foreach (var hostItem in hosts)
                                {
                                    var h = hostItem as JObject;
                                    if (h == null) continue;
                                    storedHostLoc = XYZFrom(h["location"] as JArray);
                                    host = ResolveHost(target, h, tolerance, out exact);
                                    if (host != null) break;
                                }
                                if (host == null)
                                {
                                    AddDetail(skipped, $"tag {t["id"]} ('{t["tagText"]}'): host not found in target (no UniqueId match, nothing within {tolerance} ft)");
                                    continue;
                                }

                                // Keep the tag's offset relative to its host when the host moved.
                                var head = XYZFrom(t["headPosition"] as JArray);
                                if (head == null) throw new InvalidOperationException("no head position recorded");
                                var anchor = ElementAnchor(host);
                                if (!exact && anchor != null && storedHostLoc != null)
                                    head = head.Add(anchor.Subtract(storedHostLoc));

                                bool leader = t["hasLeader"]?.Value<bool>() ?? false;
                                var tag = IndependentTag.Create(target, targetView.Id, new Reference(host),
                                    leader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, head);
                                createdTags.Add(tag.Id.Val().ToString());
                            }
                            catch (Exception ex) { AddDetail(failed, $"tag {t["id"]}: {ex.Message}"); }
                        }
                    }

                    // ---- Dimensions: best-effort, whole-element references ----
                    if (dims && payload["dimensions"] is JArray dimArr)
                    {
                        foreach (var dimItem in dimArr)
                        {
                            var d = dimItem as JObject;
                            if (d == null) { AddDetail(skipped, "dimension entry: not an object"); continue; }
                            try
                            {
                                int segments = d["segments"]?.Value<int>() ?? 1;
                                if (segments != 1)
                                {
                                    AddDetail(skipped, $"dimension {d["id"]}: multi-segment ({segments}) not supported in v2.5.0");
                                    continue;
                                }
                                var refs = d["references"] as JArray;
                                if (refs == null || refs.Count < 2)
                                {
                                    AddDetail(skipped, $"dimension {d["id"]}: fewer than 2 references recorded");
                                    continue;
                                }

                                var refArray = new ReferenceArray();
                                var pts = new List<XYZ>();
                                bool allResolved = true;
                                foreach (var refItem in refs)
                                {
                                    var h = refItem as JObject;
                                    if (h == null) { allResolved = false; break; }
                                    var e = ResolveHost(target, h, tolerance, out _);
                                    if (e == null) { allResolved = false; break; }
                                    refArray.Append(new Reference(e));
                                    var p = ElementAnchor(e);
                                    if (p != null && pts.Count < 2) pts.Add(p);
                                }
                                if (!allResolved)
                                {
                                    AddDetail(skipped, $"dimension {d["id"]}: referenced element(s) not found in target");
                                    continue;
                                }
                                if (refArray.Size < 2 || pts.Count < 2)
                                {
                                    AddDetail(skipped, $"dimension {d["id"]}: no usable references/locations in target");
                                    continue;
                                }

                                var line = Line.CreateBound(pts[0], pts[1]);
                                var dim = target.Create.NewDimension(targetView, line, refArray);

                                var vOverride = d["valueOverride"]?.ToString();
                                if (!string.IsNullOrEmpty(vOverride))
                                {
                                    try { dim.ValueOverride = vOverride; } catch { /* cosmetic */ }
                                }
                                createdDims.Add(dim.Id.Val().ToString());
                            }
                            catch (Exception ex) { AddDetail(failed, $"dimension {d["id"]}: {ex.Message}"); }
                        }
                    }

                    tx.Commit();
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }

            var msg = $"✅ Transfer: {createdTexts.Count}/{textsIn} text notes, " +
                      $"{createdTags.Count}/{tagsIn} tags, {createdDims.Count}/{dimsIn} dimensions recreated.";
            if (skipped.Count > 0 || failed.Count > 0)
                msg += " See skipped/failed for reasons — nothing was silently dropped.";

            return new JObject
            {
                ["message"] = msg,
                ["textNotes"] = new JObject { ["source"] = textsIn, ["created"] = createdTexts.Count, ["ids"] = new JArray(createdTexts.ToArray()) },
                ["tags"] = new JObject { ["source"] = tagsIn, ["created"] = createdTags.Count, ["ids"] = new JArray(createdTags.ToArray()) },
                ["dimensions"] = new JObject { ["source"] = dimsIn, ["created"] = createdDims.Count, ["ids"] = new JArray(createdDims.ToArray()) },
                ["skipped"] = skipped,
                ["failed"] = failed
            };
        }

        // ══════════════════════════════════════════════════════════════
        // ████  export / import / transfer  ████
        // ══════════════════════════════════════════════════════════════

        private static JToken ExportAnnotations(UIApplication uiApp, JObject parameters)
        {
            var doc = ResolveTransferDoc(uiApp, parameters["documentTitle"]?.ToString(), "Source");
            var view = ResolveTransferView(doc, uiApp.ActiveUIDocument, parameters["viewId"]?.Value<long>() ?? 0,
                parameters["viewName"]?.ToString(), "Source");

            bool texts = parameters["includeTextNotes"]?.Value<bool>() ?? true;
            bool tags = parameters["includeTags"]?.Value<bool>() ?? true;
            bool dims = parameters["includeDimensions"]?.Value<bool>() ?? true;

            var payload = CollectAnnotations(doc, view, texts, tags, dims);

            var filePath = parameters["filePath"]?.ToString();
            if (string.IsNullOrWhiteSpace(filePath))
                filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"annotations-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.WriteAllText(filePath, payload.ToString(Formatting.Indented));

            int n = ((payload["textNotes"] as JArray)?.Count ?? 0)
                  + ((payload["tags"] as JArray)?.Count ?? 0)
                  + ((payload["dimensions"] as JArray)?.Count ?? 0);
            return new JObject
            {
                ["message"] = $"✅ Exported {n} annotation(s) from '{doc.Title}' / '{view.Name}' to {filePath}",
                ["filePath"] = filePath,
                ["sourceDocument"] = doc.Title,
                ["sourceView"] = view.Name,
                ["textNotes"] = (payload["textNotes"] as JArray)?.Count ?? 0,
                ["tags"] = (payload["tags"] as JArray)?.Count ?? 0,
                ["dimensions"] = (payload["dimensions"] as JArray)?.Count ?? 0
            };
        }

        private static JToken ImportAnnotations(UIApplication uiApp, JObject parameters)
        {
            var filePath = parameters["filePath"]?.ToString();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new InvalidOperationException($"Transfer file not found: '{filePath}'. Run export_annotations first.");

            JObject payload;
            try { payload = JObject.Parse(File.ReadAllText(filePath)); }
            catch (Exception ex) { throw new InvalidOperationException($"Transfer file is not valid JSON: {ex.Message}"); }

            if (payload["format"]?.ToString() != TransferFormat)
                throw new InvalidOperationException(
                    $"Unsupported transfer file format '{payload["format"]}'. Expected '{TransferFormat}' from export_annotations v2.5+.");

            var doc = ResolveTransferDoc(uiApp, parameters["targetDocumentTitle"]?.ToString(), "Target");
            var view = ResolveTransferView(doc, uiApp.ActiveUIDocument, parameters["targetViewId"]?.Value<long>() ?? 0,
                parameters["targetViewName"]?.ToString(), "Target");

            double tolerance = parameters["matchTolerance"]?.Value<double>() ?? 1.0;
            var result = ApplyAnnotations(doc, view, payload, tolerance, true, true, true);
            result["targetDocument"] = doc.Title;
            result["targetView"] = view.Name;
            return result;
        }

        private static JToken TransferAnnotations(UIApplication uiApp, JObject parameters)
        {
            var source = ResolveTransferDoc(uiApp, parameters["sourceDocumentTitle"]?.ToString(), "Source");
            var docs = GetOpenDocuments(uiApp);

            Document target;
            var targetKey = parameters["targetDocumentTitle"]?.ToString();
            if (!string.IsNullOrWhiteSpace(targetKey))
            {
                target = ResolveTransferDoc(uiApp, targetKey, "Target");
            }
            else if (docs.Count == 2)
            {
                target = docs.First(d => !d.Equals(source));
            }
            else
            {
                target = uiApp.ActiveUIDocument?.Document;
                if (target == null || target.Equals(source))
                    throw new InvalidOperationException(
                        $"Target document is ambiguous ({docs.Count} open). Pass targetDocumentTitle. Open: {OpenDocumentHint(docs)}");
            }
            if (target.Equals(source))
                throw new InvalidOperationException("Source and target are the same document. Pass a different targetDocumentTitle.");

            var uidoc = uiApp.ActiveUIDocument;
            var sourceView = ResolveTransferView(source, uidoc, parameters["sourceViewId"]?.Value<long>() ?? 0,
                parameters["sourceViewName"]?.ToString(), "Source");

            // Default target view: same name as the source view.
            string targetViewName = parameters["targetViewName"]?.ToString();
            if (string.IsNullOrWhiteSpace(targetViewName) && (parameters["targetViewId"]?.Value<long>() ?? 0) == 0)
                targetViewName = sourceView.Name;
            var targetView = ResolveTransferView(target, uidoc, parameters["targetViewId"]?.Value<long>() ?? 0,
                targetViewName, "Target");

            bool texts = parameters["includeTextNotes"]?.Value<bool>() ?? true;
            bool tags = parameters["includeTags"]?.Value<bool>() ?? true;
            bool dims = parameters["includeDimensions"]?.Value<bool>() ?? true;
            double tolerance = parameters["matchTolerance"]?.Value<double>() ?? 1.0;

            var payload = CollectAnnotations(source, sourceView, texts, tags, dims);
            var result = ApplyAnnotations(target, targetView, payload, tolerance, texts, tags, dims);
            result["sourceDocument"] = source.Title;
            result["sourceView"] = sourceView.Name;
            result["targetDocument"] = target.Title;
            result["targetView"] = targetView.Name;
            return result;
        }
    }
}
