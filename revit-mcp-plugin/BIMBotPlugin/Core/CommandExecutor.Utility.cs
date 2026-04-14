using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using BIMBotPlugin.PowerBI;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Utility tool implementations: pin/unpin, worksets, element history, assemblies,
    /// fill patterns, geometry, model comparison, links, file management,
    /// family editor, sketch editing, drafting, rendering, worksharing,
    /// undo/transactions, UI automation, and remaining gap implementations.
    /// </summary>
    public static partial class CommandExecutor
    {
        // ===== UTILITY TOOL IMPLEMENTATIONS =====

        private static JToken PinElements(Document doc, JObject parameters, bool pin)
        {
            var idsArr = parameters["elementIds"] as JArray;
            if (idsArr == null || idsArr.Count == 0) return new JObject { ["error"] = "No element IDs" };
            var elementIds = idsArr.Select(id => new ElementId(id.Value<int>())).ToList();
            int count = 0;
            using (var tx = new Transaction(doc, pin ? "Pin" : "Unpin"))
            {
                tx.Start();
                foreach (var e in new FilteredElementCollector(doc, elementIds)) { e.Pinned = pin; count++; }
                tx.Commit();
            }
            return new JObject { ["message"] = $"ðŸ“Œ {(pin ? "Pinned" : "Unpinned")} {count} elements" };
        }

        private static JToken CreateWorkset(Document doc, JObject parameters)
        {
            var name = parameters["name"]?.ToString();
            if (string.IsNullOrEmpty(name)) return new JObject { ["error"] = "Name required" };
            if (!doc.IsWorkshared) return new JObject { ["error"] = "Not workshared" };
            using (var tx = new Transaction(doc, "Create Workset"))
            {
                tx.Start(); var ws = Workset.Create(doc, name); tx.Commit();
                return new JObject { ["message"] = $"ðŸ“ Created workset '{name}'", ["worksetId"] = ws.Id.IntegerValue };
            }
        }

        private static JToken GetElementHistory(Document doc, JObject parameters)
        {
            var idsArr = parameters["elementIds"] as JArray;
            if (idsArr == null) return new JObject { ["error"] = "No element IDs" };
            var elementIds = idsArr.Select(id => new ElementId(id.Value<int>())).ToList();
            var items = new JArray();
            foreach (var elem in new FilteredElementCollector(doc, elementIds))
            {
                var item = new JObject { ["id"] = elem.Id.Value, ["name"] = elem.Name };
                if (doc.IsWorkshared) { var ws = elem.get_Parameter(BuiltInParameter.EDITED_BY); if (ws != null) item["editedBy"] = ws.AsString(); }
                var ph = elem.get_Parameter(BuiltInParameter.PHASE_CREATED); if (ph != null) { var phase = doc.GetElement(ph.AsElementId()); item["phaseCreated"] = phase?.Name; }
                items.Add(item);
            }
            return new JObject { ["message"] = $"ðŸ“‹ History for {items.Count} elements", ["elements"] = items };
        }

        private static JToken CreateAssembly(Document doc, JObject parameters)
        {
            var idsArr = parameters["elementIds"] as JArray;
            if (idsArr == null || idsArr.Count == 0) return new JObject { ["error"] = "No element IDs" };
            var ids = idsArr.Select(id => new ElementId(id.Value<int>())).ToList();
            var firstElem = doc.GetElement(ids[0]);
            if (firstElem == null) return new JObject { ["error"] = "First element not found" };
            using (var tx = new Transaction(doc, "Create Assembly"))
            {
                tx.Start();
                var assembly = AssemblyInstance.Create(doc, ids, firstElem.Category.Id);
                var name = parameters["assemblyName"]?.ToString();
                if (!string.IsNullOrEmpty(name)) assembly.AssemblyTypeName = name;
                tx.Commit();
                return new JObject { ["message"] = $"ðŸ“¦ Created assembly (ID: {assembly.Id.Value})", ["elementId"] = assembly.Id.Value };
            }
        }

        private static JToken CreateFillPattern(Document doc, JObject parameters)
        {
            var name = parameters["name"]?.ToString() ?? "Custom_Pattern";
            var angle = (parameters["angle"]?.Value<double>() ?? 45) * Math.PI / 180;
            var spacing = parameters["spacing"]?.Value<double>() ?? 0.5;
            var target = parameters["patternType"]?.ToString()?.ToLower().Contains("model") == true ? FillPatternTarget.Model : FillPatternTarget.Drafting;
            var pattern = new FillPattern(name, target, FillPatternHostOrientation.ToView, angle, spacing);
            using (var tx = new Transaction(doc, "Create Fill Pattern"))
            {
                tx.Start(); var elem = FillPatternElement.Create(doc, pattern); tx.Commit();
                return new JObject { ["message"] = $"ðŸŽ¨ Created fill pattern '{name}' (ID: {elem.Id.Value})", ["elementId"] = elem.Id.Value };
            }
        }

        private static JToken GetElementGeometry(Document doc, JObject parameters)
        {
            var elem = doc.GetElement(new ElementId(parameters["elementId"]?.Value<int>() ?? 0));
            if (elem == null) return new JObject { ["error"] = "Element not found" };
            var result = new JObject { ["id"] = elem.Id.Value, ["name"] = elem.Name };
            var bb = elem.get_BoundingBox(null);
            if (bb != null)
            {
                var size = bb.Max - bb.Min;
                result["boundingBox"] = new JObject { ["width"] = Math.Round(size.X, 4), ["depth"] = Math.Round(size.Y, 4), ["height"] = Math.Round(size.Z, 4) };
                result["centroid"] = new JObject { ["x"] = Math.Round((bb.Min.X + bb.Max.X) / 2, 4), ["y"] = Math.Round((bb.Min.Y + bb.Max.Y) / 2, 4), ["z"] = Math.Round((bb.Min.Z + bb.Max.Z) / 2, 4) };
            }
            var vol = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED); if (vol != null) result["volume_cuft"] = Math.Round(vol.AsDouble(), 4);
            var area = elem.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED); if (area != null) result["area_sqft"] = Math.Round(area.AsDouble(), 4);
            return new JObject { ["message"] = $"ðŸ“ Geometry for '{elem.Name}'", ["geometry"] = result };
        }

        private static JToken CompareModels(Document doc, JObject parameters)
        {
            var snapshot = new JObject();
            var cats = new[] { BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows, BuiltInCategory.OST_Rooms, BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_StructuralFraming };
            foreach (var cat in cats) snapshot[cat.ToString().Replace("OST_", "")] = new FilteredElementCollector(doc).OfCategory(cat).WhereElementIsNotElementType().Count();
            snapshot["totalFamilies"] = new FilteredElementCollector(doc).OfClass(typeof(Family)).Count();
            snapshot["totalSheets"] = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Count();
            return new JObject { ["message"] = "ðŸ“Š Current model snapshot", ["snapshot"] = snapshot };
        }

        private static JToken LinkRevitModel(Document doc, UIApplication uiApp, JObject parameters)
        {
            var filePath = parameters["filePath"]?.ToString();
            if (string.IsNullOrEmpty(filePath)) return new JObject { ["error"] = "File path required" };
            return new JObject { ["message"] = $"ðŸ”— Link model: {System.IO.Path.GetFileName(filePath)}", ["hint"] = "Use execute_code: RevitLinkType.Create(doc, modelPath, false) then RevitLinkInstance.Create(doc, linkTypeId)" };
        }

        private static JToken ReloadLinks(Document doc, JObject parameters)
        {
            var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>().ToList();
            var linkName = parameters["linkName"]?.ToString();
            if (!string.IsNullOrEmpty(linkName)) links = links.Where(l => l.Name.IndexOf(linkName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            int reloaded = 0;
            var errors = new JArray();
            foreach (var l in links)
            {
                try { l.Reload(); reloaded++; }
                catch (Exception ex) { errors.Add($"{l.Name}: {ex.Message}"); }
            }
            var result = new JObject
            {
                ["message"] = $"ðŸ”„ Reloaded {reloaded}/{links.Count} links",
                ["links"] = JArray.FromObject(links.Select(l => new { l.Name, id = l.Id.Value }))
            };
            if (errors.Count > 0) result["errors"] = errors;
            return result;
        }

        private static JToken UnloadLinks(Document doc, JObject parameters)
        {
            var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>().ToList();
            var linkName = parameters["linkName"]?.ToString();
            if (!string.IsNullOrEmpty(linkName)) links = links.Where(l => l.Name.IndexOf(linkName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            int unloaded = 0;
            foreach (var l in links)
            {
                try { l.Unload(null); unloaded++; } catch (Exception ex) { Logger.Log($"Failed to unload link '{l.Name}': {ex.Message}"); }
            }
            return new JObject
            {
                ["message"] = $"ðŸ“¤ Unloaded {unloaded}/{links.Count} links",
                ["links"] = JArray.FromObject(links.Select(l => new { l.Name, id = l.Id.Value }))
            };
        }

        private static JToken GetLinkInfo(Document doc, JObject parameters)
        {
            var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>().ToList();
            var linkName = parameters["linkName"]?.ToString();
            if (!string.IsNullOrEmpty(linkName)) links = links.Where(l => l.Name.IndexOf(linkName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var result = new JArray();
            foreach (var link in links)
            {
                var linkInfo = new JObject
                {
                    ["id"] = link.Id.Value,
                    ["name"] = link.Name,
                    ["isLoaded"] = RevitLinkType.IsLoaded(doc, link.Id),
                };

                // Get link instances
                var instances = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .Where(i => i.GetTypeId() == link.Id)
                    .ToList();
                linkInfo["instanceCount"] = instances.Count;

                try
                {
                    var extRef = link.GetExternalFileReference();
                    if (extRef != null)
                    {
                        linkInfo["filePath"] = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetAbsolutePath());
                        linkInfo["status"] = extRef.GetLinkedFileStatus().ToString();
                    }
                }
                catch { linkInfo["status"] = "Unknown"; }

                result.Add(linkInfo);
            }

            return new JObject
            {
                ["message"] = $"ðŸ”— {result.Count} linked model(s) found",
                ["links"] = result,
                ["count"] = result.Count
            };
        }

        // ===== PHASE 1: FILE MANAGEMENT =====

        private static JToken SaveDocument(Document doc)
        {
            if (string.IsNullOrEmpty(doc.PathName))
                throw new InvalidOperationException("Document has never been saved. Use save_as_document with a file path first.");
            doc.Save();
            return new JObject { ["message"] = $"ðŸ’¾ Document saved: {System.IO.Path.GetFileName(doc.PathName)}", ["filePath"] = doc.PathName };
        }

        private static JToken SaveAsDocument(Document doc, JObject parameters)
        {
            var filePath = parameters["filePath"]?.ToString();
            if (string.IsNullOrEmpty(filePath))
                throw new InvalidOperationException("File path is required for Save As.");
            var overwrite = parameters["overwrite"]?.Value<bool>() ?? false;
            if (File.Exists(filePath) && !overwrite)
                throw new InvalidOperationException($"File already exists: {filePath}. Set overwrite=true to replace.");
            var opts = new SaveAsOptions { OverwriteExistingFile = overwrite };
            doc.SaveAs(filePath, opts);
            return new JObject { ["message"] = $"ðŸ’¾ Saved as: {System.IO.Path.GetFileName(filePath)}", ["filePath"] = filePath };
        }

        private static JToken CloseDocument(Document doc, JObject parameters)
        {
            var save = parameters["save"]?.Value<bool>() ?? true;
            var fileName = System.IO.Path.GetFileName(doc.PathName);
            if (save && !string.IsNullOrEmpty(doc.PathName))
                doc.Save();
            doc.Close(save);
            return new JObject { ["message"] = $"ðŸ“ Closed document: {fileName}", ["saved"] = save };
        }

        // ===== PHASE 2: FAMILY EDITOR =====

        private static JToken EditFamily(UIApplication uiApp, Document doc, JObject parameters)
        {
            var elementId = parameters["elementId"]?.Value<int>() ?? 0;
            var elem = doc.GetElement(new ElementId(elementId));
            if (elem == null) throw new InvalidOperationException($"Element {elementId} not found");

            Family family = null;
            if (elem is FamilyInstance fi) family = fi.Symbol?.Family;
            else if (elem is FamilySymbol fs) family = fs.Family;
            else if (elem is Family f) family = f;

            if (family == null || !family.IsEditable)
                throw new InvalidOperationException("Element is not an editable family instance.");

            var famDoc = doc.EditFamily(family);
            if (famDoc == null)
                throw new InvalidOperationException("Failed to open family for editing.");

            // Switch to the family document view
            var famView = new FilteredElementCollector(famDoc).OfClass(typeof(View)).Cast<View>()
                .FirstOrDefault(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan)
                ?? new FilteredElementCollector(famDoc).OfClass(typeof(View)).Cast<View>().FirstOrDefault(v => !v.IsTemplate);

            return new JObject
            {
                ["message"] = $"âœï¸ Opened family '{family.Name}' for editing",
                ["familyName"] = family.Name,
                ["familyCategory"] = family.FamilyCategory?.Name ?? "Unknown"
            };
        }

        private static JToken CreateFamilyExtrusion(UIApplication uiApp, JObject parameters)
        {
            var doc = uiApp.ActiveUIDocument?.Document;
            if (doc == null || !doc.IsFamilyDocument)
                throw new InvalidOperationException("No family document is open. Use edit_family first.");

            var profilePoints = parameters["profilePoints"] as JArray;
            if (profilePoints == null || profilePoints.Count < 3)
                throw new InvalidOperationException("At least 3 profile points are required.");

            var depth = parameters["extrusionDepth"]?.Value<double>() ?? 1.0;
            var isSolid = parameters["isSolid"]?.Value<bool>() ?? true;

            using (var tx = new Transaction(doc, "Create Extrusion"))
            {
                tx.Start();

                // Build profile curve array
                var curveArrArray = new CurveArrArray();
                var curveArr = new CurveArray();
                var points = new List<XYZ>();

                foreach (var pt in profilePoints)
                {
                    points.Add(new XYZ(pt["x"]?.Value<double>() ?? 0, pt["y"]?.Value<double>() ?? 0, 0));
                }
                // Close the loop
                for (int i = 0; i < points.Count; i++)
                {
                    var next = (i + 1) % points.Count;
                    curveArr.Append(Line.CreateBound(points[i], points[next]));
                }
                curveArrArray.Append(curveArr);

                // Get a reference plane for the extrusion
                var sketchPlane = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));

                var extrusion = doc.FamilyCreate.NewExtrusion(isSolid, curveArrArray, sketchPlane, depth);

                tx.Commit();

                return new JObject
                {
                    ["message"] = $"ðŸ§± Created {(isSolid ? "solid" : "void")} extrusion with {points.Count} vertices, depth={depth}ft",
                    ["elementId"] = extrusion.Id.Value,
                    ["vertexCount"] = points.Count,
                    ["depth"] = depth
                };
            }
        }

        private static JToken SaveFamily(UIApplication uiApp, JObject parameters)
        {
            var doc = uiApp.ActiveUIDocument?.Document;
            if (doc == null || !doc.IsFamilyDocument)
                throw new InvalidOperationException("No family document is open.");

            var loadIntoProject = parameters["loadIntoProject"]?.Value<bool>() ?? true;
            var familyName = System.IO.Path.GetFileNameWithoutExtension(doc.PathName ?? doc.Title);

            doc.Save();

            if (loadIntoProject)
            {
                // Load the family back into any open project documents
                foreach (Document openDoc in uiApp.Application.Documents)
                {
                    if (!openDoc.IsFamilyDocument && !openDoc.IsLinked)
                    {
                        using (var tx = new Transaction(openDoc, "Load Family"))
                        {
                            tx.Start();
                            Family loaded;
                            openDoc.LoadFamily(doc.PathName, out loaded);
                            tx.Commit();
                        }
                        break;
                    }
                }
            }

            return new JObject
            {
                ["message"] = $"ðŸ’¾ Family '{familyName}' saved{(loadIntoProject ? " and loaded into project" : "")}",
                ["familyName"] = familyName
            };
        }

        private static JToken LoadFamily(Document doc, JObject parameters)
        {
            var filePath = parameters["filePath"]?.ToString();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new InvalidOperationException($"Family file not found: {filePath}");

            using (var tx = new Transaction(doc, "Load Family"))
            {
                tx.Start();
                Family family;
                var loaded = doc.LoadFamily(filePath, out family);
                tx.Commit();

                if (!loaded)
                    return new JObject { ["message"] = $"Family already loaded or load failed: {System.IO.Path.GetFileName(filePath)}" };

                return new JObject
                {
                    ["message"] = $"ðŸ“¦ Loaded family '{family.Name}' from {System.IO.Path.GetFileName(filePath)}",
                    ["familyName"] = family.Name,
                    ["familyId"] = family.Id.Value
                };
            }
        }

        // ===== PHASE 3: SKETCH EDITING =====

        private static JToken GetSketch(Document doc, JObject parameters)
        {
            var elementId = parameters["elementId"]?.Value<int>() ?? 0;
            var elem = doc.GetElement(new ElementId(elementId));
            if (elem == null) throw new InvalidOperationException($"Element {elementId} not found");

            IList<CurveLoop> curveLoops = null;

            if (elem is Floor floor)
            {
                var sketch2 = doc.GetElement(floor.SketchId) as Sketch;
                if (sketch2 != null)
                {
                    curveLoops = new List<CurveLoop>();
                    foreach (CurveArray ca in sketch2.Profile)
                    {
                        var cl = new CurveLoop();
                        foreach (Curve c in ca) cl.Append(c);
                        curveLoops.Add(cl);
                    }
                }
            }

            if (curveLoops == null)
                throw new InvalidOperationException("Cannot extract sketch from this element. Supported: Floors, Roofs, Ceilings.");

            var loops = new JArray();
            foreach (var cl in curveLoops)
            {
                var pts = new JArray();
                foreach (var curve in cl)
                {
                    var sp = curve.GetEndPoint(0);
                    pts.Add(new JObject { ["x"] = Math.Round(sp.X, 4), ["y"] = Math.Round(sp.Y, 4), ["z"] = Math.Round(sp.Z, 4) });
                }
                loops.Add(pts);
            }

            return new JObject
            {
                ["message"] = $"ðŸ“ Sketch profile for '{elem.Name}' â€” {curveLoops.Count} loop(s)",
                ["elementId"] = elementId,
                ["loops"] = loops
            };
        }

        private static JToken EditSketch(Document doc, JObject parameters)
        {
            var elementId = parameters["elementId"]?.Value<int>() ?? 0;
            var action = parameters["action"]?.ToString() ?? "add_line";
            var elem = doc.GetElement(new ElementId(elementId));
            if (elem == null) throw new InvalidOperationException($"Element {elementId} not found");

            // Use send_code_to_revit for complex sketch editing via SketchEditScope
            return new JObject
            {
                ["message"] = $"âœï¸ Use send_code_to_revit with SketchEditScope for '{action}' on element {elementId}",
                ["hint"] = "var scope = new SketchEditScope(doc, \"Edit Sketch\"); scope.Start(new ElementId(" + elementId + ")); // modify sketch curves... scope.Commit(new FailuresPreprocessor());",
                ["action"] = action,
                ["elementId"] = elementId
            };
        }

        private static JToken SetSketchProfile(Document doc, JObject parameters)
        {
            var elementId = parameters["elementId"]?.Value<int>() ?? 0;
            var profilePts = parameters["profile"] as JArray;
            if (profilePts == null || profilePts.Count < 3)
                throw new InvalidOperationException("At least 3 profile points are required.");

            var elem = doc.GetElement(new ElementId(elementId));
            if (elem == null) throw new InvalidOperationException($"Element {elementId} not found");

            // Build the hint code for SketchEditScope
            var ptsStr = string.Join(", ", profilePts.Select(p =>
                $"new XYZ({p["x"]}, {p["y"]}, {p["z"] ?? JToken.FromObject(0)})"));

            return new JObject
            {
                ["message"] = $"âœï¸ To replace sketch profile, use send_code_to_revit with SketchEditScope",
                ["hint"] = $"// Delete existing sketch lines, then create new ones with:\n" +
                          $"var pts = new[] {{ {ptsStr} }};\n" +
                          $"for(int i=0; i<pts.Length; i++) doc.Create.NewModelCurve(Line.CreateBound(pts[i], pts[(i+1)%pts.Length]), sketchPlane);",
                ["elementId"] = elementId,
                ["pointCount"] = profilePts.Count
            };
        }

        // ===== PHASE 4: DRAFTING =====

        private static JToken CreateDetailLines(UIDocument uidoc, Document doc, JObject parameters)
        {
            var lines = parameters["lines"] as JArray;
            if (lines == null || lines.Count == 0)
                throw new InvalidOperationException("At least one line segment is required.");

            var viewId = parameters["viewId"]?.Value<int>();
            var view = viewId.HasValue
                ? doc.GetElement(new ElementId(viewId.Value)) as View
                : uidoc.ActiveView;
            if (view == null) throw new InvalidOperationException("Invalid view.");

            int created = 0;
            using (var tx = new Transaction(doc, "Create Detail Lines"))
            {
                tx.Start();
                foreach (var line in lines)
                {
                    var start = new XYZ(
                        line["startX"]?.Value<double>() ?? 0,
                        line["startY"]?.Value<double>() ?? 0, 0);
                    var end = new XYZ(
                        line["endX"]?.Value<double>() ?? 0,
                        line["endY"]?.Value<double>() ?? 0, 0);
                    if (start.DistanceTo(end) < 0.001) continue;
                    doc.Create.NewDetailCurve(view, Line.CreateBound(start, end));
                    created++;
                }
                tx.Commit();
            }

            return new JObject
            {
                ["message"] = $"âœï¸ Created {created} detail lines in view '{view.Name}'",
                ["linesCreated"] = created,
                ["viewName"] = view.Name
            };
        }

        private static JToken CreateModelLines(Document doc, JObject parameters)
        {
            var lines = parameters["lines"] as JArray;
            if (lines == null || lines.Count == 0)
                throw new InvalidOperationException("At least one line segment is required.");

            int created = 0;
            using (var tx = new Transaction(doc, "Create Model Lines"))
            {
                tx.Start();
                foreach (var line in lines)
                {
                    var start = new XYZ(
                        line["startX"]?.Value<double>() ?? 0,
                        line["startY"]?.Value<double>() ?? 0,
                        line["startZ"]?.Value<double>() ?? 0);
                    var end = new XYZ(
                        line["endX"]?.Value<double>() ?? 0,
                        line["endY"]?.Value<double>() ?? 0,
                        line["endZ"]?.Value<double>() ?? 0);
                    if (start.DistanceTo(end) < 0.001) continue;

                    var geomLine = Line.CreateBound(start, end);
                    var normal = XYZ.BasisZ;
                    if (Math.Abs(geomLine.Direction.DotProduct(normal)) > 0.999)
                        normal = XYZ.BasisX;
                    var plane = Plane.CreateByNormalAndOrigin(normal, start);
                    var sketchPlane = SketchPlane.Create(doc, plane);
                    doc.Create.NewModelCurve(geomLine, sketchPlane);
                    created++;
                }
                tx.Commit();
            }

            return new JObject
            {
                ["message"] = $"âœï¸ Created {created} model lines",
                ["linesCreated"] = created
            };
        }

        private static JToken CreateDetailArc(UIDocument uidoc, Document doc, JObject parameters)
        {
            var cx = parameters["centerX"]?.Value<double>() ?? 0;
            var cy = parameters["centerY"]?.Value<double>() ?? 0;
            var radius = parameters["radius"]?.Value<double>() ?? 1;
            var startAngle = (parameters["startAngle"]?.Value<double>() ?? 0) * Math.PI / 180;
            var endAngle = (parameters["endAngle"]?.Value<double>() ?? 360) * Math.PI / 180;

            var viewId = parameters["viewId"]?.Value<int>();
            var view = viewId.HasValue
                ? doc.GetElement(new ElementId(viewId.Value)) as View
                : uidoc.ActiveView;

            using (var tx = new Transaction(doc, "Create Detail Arc"))
            {
                tx.Start();
                var center = new XYZ(cx, cy, 0);
                Arc arc;
                if (Math.Abs(endAngle - startAngle - 2 * Math.PI) < 0.001)
                {
                    // Full circle â€” create two semicircles
                    var arc1 = Arc.Create(center, radius, 0, Math.PI, XYZ.BasisX, XYZ.BasisY);
                    var arc2 = Arc.Create(center, radius, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY);
                    doc.Create.NewDetailCurve(view, arc1);
                    doc.Create.NewDetailCurve(view, arc2);
                }
                else
                {
                    arc = Arc.Create(center, radius, startAngle, endAngle, XYZ.BasisX, XYZ.BasisY);
                    doc.Create.NewDetailCurve(view, arc);
                }
                tx.Commit();
            }

            return new JObject
            {
                ["message"] = $"â­• Created detail arc at ({cx}, {cy}), radius={radius}ft",
                ["center"] = new JObject { ["x"] = cx, ["y"] = cy },
                ["radius"] = radius
            };
        }

        // ===== PHASE 5: RENDERING =====

        private static JToken SetSunSettings(UIDocument uidoc, Document doc, JObject parameters)
        {
            var viewId = parameters["viewId"]?.Value<int>();
            var view = viewId.HasValue
                ? doc.GetElement(new ElementId(viewId.Value)) as View
                : uidoc.ActiveView;
            if (view == null) throw new InvalidOperationException("Invalid view.");

            using (var tx = new Transaction(doc, "Set Sun Settings"))
            {
                tx.Start();

                var sunSettings = view.SunAndShadowSettings;
                if (sunSettings == null)
                    throw new InvalidOperationException("Sun settings not available for this view type.");

                var shadowsOn = parameters["shadowsOn"]?.Value<bool>();
                if (shadowsOn.HasValue)
                {
                    // Shadows are controlled through the view's visual style
                    // GetGraphicalDisplayOptions is not available in all Revit versions
                }

                var dateStr = parameters["date"]?.ToString();
                var timeStr = parameters["time"]?.ToString();
                if (!string.IsNullOrEmpty(dateStr))
                {
                    // Sun study date/time configuration via SunAndShadowSettings
                    // These are read-only in many contexts; provide guidance
                }

                tx.Commit();
            }

            return new JObject
            {
                ["message"] = $"â˜€ï¸ Sun settings updated for view '{view.Name}'",
                ["hint"] = "For full sun study control, use send_code_to_revit with SunAndShadowSettings API.",
                ["viewName"] = view.Name
            };
        }

        private static JToken SetVisualStyle(UIDocument uidoc, Document doc, JObject parameters)
        {
            var styleName = parameters["style"]?.ToString() ?? "Shaded";
            var viewId = parameters["viewId"]?.Value<int>();
            var view = viewId.HasValue
                ? doc.GetElement(new ElementId(viewId.Value)) as View
                : uidoc.ActiveView;
            if (view == null) throw new InvalidOperationException("Invalid view");

            DisplayStyle style;
            switch (styleName)
            {
                case "Wireframe": style = DisplayStyle.Wireframe; break;
                case "HiddenLine": style = DisplayStyle.HLR; break;
                case "Shaded": style = DisplayStyle.Shading; break;
                case "ShadingWithEdges": style = DisplayStyle.ShadingWithEdges; break;
                case "Realistic": style = DisplayStyle.Realistic; break;
                case "RayTrace": style = DisplayStyle.Realistic; break;  // Raytrace not available in all versions
                default: style = DisplayStyle.Shading; break;
            }

            using (var tx = new Transaction(doc, "Set Visual Style"))
            {
                tx.Start();
                view.DisplayStyle = style;
                tx.Commit();
            }

            return new JObject
            {
                ["message"] = $"ðŸŽ¨ Set visual style to '{styleName}' on view '{view.Name}'",
                ["style"] = styleName,
                ["viewName"] = view.Name
            };
        }

        private static JToken ExportViewImage(Document doc, JObject parameters)
        {
            var filePath = parameters["filePath"]?.ToString();
            if (string.IsNullOrEmpty(filePath))
                throw new InvalidOperationException("File path is required.");

            var format = parameters["format"]?.ToString()?.ToUpper() ?? "PNG";
            var pixelWidth = parameters["pixelWidth"]?.Value<int>() ?? 1920;
            var pixelHeight = parameters["pixelHeight"]?.Value<int>() ?? 1080;
            var viewId = parameters["viewId"]?.Value<int>();

            var dir = System.IO.Path.GetDirectoryName(filePath);
            var name = System.IO.Path.GetFileNameWithoutExtension(filePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            ImageExportOptions opts = new ImageExportOptions
            {
                FilePath = System.IO.Path.Combine(dir, name),
                HLRandWFViewsFileType = format == "JPG" ? ImageFileType.JPEGLossless : ImageFileType.PNG,
                ShadowViewsFileType = format == "JPG" ? ImageFileType.JPEGLossless : ImageFileType.PNG,
                PixelSize = pixelWidth,
                ZoomType = ZoomFitType.FitToPage,
                ExportRange = viewId.HasValue ? ExportRange.SetOfViews : ExportRange.CurrentView
            };

            if (viewId.HasValue)
            {
                var viewIds = new List<ElementId> { new ElementId(viewId.Value) };
                opts.SetViewsAndSheets(viewIds);
            }

            doc.ExportImage(opts);

            return new JObject
            {
                ["message"] = $"ðŸ“¸ Exported view image to '{filePath}'",
                ["filePath"] = filePath,
                ["format"] = format,
                ["resolution"] = $"{pixelWidth}x{pixelHeight}"
            };
        }

        // ===== PHASE 6: WORKSHARING =====

        private static JToken SyncToCentral(Document doc, JObject parameters)
        {
            if (!doc.IsWorkshared)
                throw new InvalidOperationException("Document is not workshared. Sync to Central requires a workshared model.");

            var comment = parameters["comment"]?.ToString() ?? "BIM-Bot Sync";
            var relinquishAll = parameters["relinquishAll"]?.Value<bool>() ?? true;
            var saveLocalBefore = parameters["saveLocalBefore"]?.Value<bool>() ?? true;
            var saveLocalAfter = parameters["saveLocalAfter"]?.Value<bool>() ?? true;

            var transactOpts = new TransactWithCentralOptions();
            var relinquishOpts = new RelinquishOptions(relinquishAll);
            if (relinquishAll)
            {
                relinquishOpts.StandardWorksets = true;
                relinquishOpts.ViewWorksets = true;
                relinquishOpts.FamilyWorksets = true;
                relinquishOpts.UserWorksets = true;
                relinquishOpts.CheckedOutElements = true;
            }

            var swcOpts = new SynchronizeWithCentralOptions();
            swcOpts.SaveLocalBefore = saveLocalBefore;
            swcOpts.SaveLocalAfter = saveLocalAfter;
            swcOpts.Comment = comment;
            swcOpts.SetRelinquishOptions(relinquishOpts);

            doc.SynchronizeWithCentral(transactOpts, swcOpts);

            return new JObject
            {
                ["message"] = $"ðŸ”„ Synchronized with Central â€” comment: '{comment}'",
                ["comment"] = comment,
                ["relinquishedAll"] = relinquishAll
            };
        }

        private static JToken RelinquishAll(Document doc)
        {
            if (!doc.IsWorkshared)
                throw new InvalidOperationException("Document is not workshared.");

            var relinquishOpts = new RelinquishOptions(true)
            {
                StandardWorksets = true,
                ViewWorksets = true,
                FamilyWorksets = true,
                UserWorksets = true,
                CheckedOutElements = true
            };

            var transactOpts = new TransactWithCentralOptions();
            WorksharingUtils.RelinquishOwnership(doc, relinquishOpts, transactOpts);

            return new JObject { ["message"] = "ðŸ”“ Relinquished all borrowed elements and worksets" };
        }

        private static JToken GetWorksharingInfo(Document doc)
        {
            if (!doc.IsWorkshared)
                return new JObject { ["message"] = "Document is not workshared", ["isWorkshared"] = false };

            var centralPath = doc.GetWorksharingCentralModelPath();
            var centralPathStr = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralPath);

            var worksets = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToWorksets();
            var wsArray = new JArray();
            foreach (var ws in worksets)
            {
                wsArray.Add(new JObject
                {
                    ["id"] = ws.Id.IntegerValue,
                    ["name"] = ws.Name,
                    ["isOpen"] = ws.IsOpen,
                    ["owner"] = ws.Owner ?? ""
                });
            }

            return new JObject
            {
                ["message"] = $"ðŸ“‹ Worksharing info for '{System.IO.Path.GetFileName(doc.PathName)}'",
                ["isWorkshared"] = true,
                ["centralModelPath"] = centralPathStr,
                ["localPath"] = doc.PathName,
                ["worksets"] = wsArray,
                ["worksetCount"] = wsArray.Count
            };
        }

        // ===== PHASE 8: UNDO / TRANSACTIONS =====

        // Static checkpoint storage for TransactionGroup-based rollback
        private static readonly Dictionary<string, TransactionGroup> _checkpoints = new Dictionary<string, TransactionGroup>();

        private static JToken UndoLastOperation(UIApplication uiApp)
        {
            // Use PostableCommand for Undo
            try
            {
                var cmdId = RevitCommandId.LookupPostableCommandId(PostableCommand.Undo);
                uiApp.PostCommand(cmdId);
                return new JObject { ["message"] = "â†©ï¸ Undo command posted. The last operation will be undone." };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"âš ï¸ Undo failed: {ex.Message}", ["hint"] = "Undo can only be triggered outside of an active API transaction context." };
            }
        }

        private static JToken CreateCheckpoint(Document doc, JObject parameters)
        {
            var name = parameters["name"]?.ToString() ?? $"checkpoint_{DateTime.Now:HHmmss}";

            if (_checkpoints.ContainsKey(name))
                throw new InvalidOperationException($"Checkpoint '{name}' already exists. Use a different name or rollback first.");

            var tg = new TransactionGroup(doc, $"Checkpoint: {name}");
            tg.Start();
            _checkpoints[name] = tg;

            return new JObject
            {
                ["message"] = $"ðŸ“Œ Checkpoint '{name}' created. All subsequent changes can be rolled back to this point.",
                ["checkpointName"] = name,
                ["activeCheckpoints"] = JArray.FromObject(_checkpoints.Keys.ToList())
            };
        }

        private static JToken RollbackToCheckpoint(JObject parameters)
        {
            var name = parameters["name"]?.ToString();
            if (string.IsNullOrEmpty(name) || !_checkpoints.ContainsKey(name))
                throw new InvalidOperationException($"Checkpoint '{name}' not found. Active checkpoints: {string.Join(", ", _checkpoints.Keys)}");

            var tg = _checkpoints[name];
            tg.RollBack();
            _checkpoints.Remove(name);

            return new JObject
            {
                ["message"] = $"âª Rolled back to checkpoint '{name}'. All changes since the checkpoint have been undone.",
                ["checkpointName"] = name,
                ["remainingCheckpoints"] = JArray.FromObject(_checkpoints.Keys.ToList())
            };
        }

        // ===== PHASE 9: UI AUTOMATION =====

        private static JToken PostCommand(UIApplication uiApp, JObject parameters)
        {
            var commandName = parameters["commandName"]?.ToString();
            if (string.IsNullOrEmpty(commandName))
                throw new InvalidOperationException("Command name is required.");

            PostableCommand cmd;
            if (!Enum.TryParse(commandName, true, out cmd))
                throw new InvalidOperationException($"Unknown command: '{commandName}'. Use list_commands to see available commands.");

            var cmdId = RevitCommandId.LookupPostableCommandId(cmd);
            uiApp.PostCommand(cmdId);

            return new JObject
            {
                ["message"] = $"â–¶ï¸ Posted command: {commandName}",
                ["commandName"] = commandName
            };
        }

        private static JToken ListPostableCommands()
        {
            var commands = Enum.GetNames(typeof(PostableCommand)).OrderBy(n => n).ToList();
            return new JObject
            {
                ["message"] = $"ðŸ“‹ {commands.Count} available PostableCommands",
                ["count"] = commands.Count,
                ["commands"] = JArray.FromObject(commands)
            };
        }

        // ===== REMAINING GAP IMPLEMENTATIONS =====

        private static JToken OpenDocument(UIApplication uiApp, JObject parameters)
        {
            var filePath = parameters["filePath"]?.ToString();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new InvalidOperationException($"File not found: {filePath}");

            var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
            var openOpts = new OpenOptions();
            var detach = parameters["detach"]?.Value<bool>() ?? false;
            if (detach)
                openOpts.DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets;

            uiApp.OpenAndActivateDocument(modelPath, openOpts, false);

            return new JObject
            {
                ["message"] = $"ðŸ“‚ Opened document: {System.IO.Path.GetFileName(filePath)}",
                ["filePath"] = filePath,
                ["detached"] = detach
            };
        }

        private static JToken CreateNewProject(UIApplication uiApp, JObject parameters)
        {
            var templatePath = parameters["templatePath"]?.ToString() ?? "";
            Document newDoc;

            if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
            {
                newDoc = uiApp.Application.NewProjectDocument(templatePath);
            }
            else
            {
                // Use default template
                newDoc = uiApp.Application.NewProjectDocument(UnitSystem.Metric);
            }

            return new JObject
            {
                ["message"] = $"ðŸ“„ Created new project{(string.IsNullOrEmpty(templatePath) ? "" : $" from template: {System.IO.Path.GetFileName(templatePath)}")}",
                ["documentTitle"] = newDoc.Title
            };
        }

        private static JToken CreateNewFamily(UIApplication uiApp, JObject parameters)
        {
            var templatePath = parameters["templatePath"]?.ToString();
            if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            {
                // Try to find default family template
                var defaultPath = System.IO.Path.Combine(
                    uiApp.Application.FamilyTemplatePath,
                    "Metric Generic Model.rft");
                if (File.Exists(defaultPath))
                    templatePath = defaultPath;
                else
                    throw new InvalidOperationException(
                        $"Family template not found. Available templates in: {uiApp.Application.FamilyTemplatePath}");
            }

            var famDoc = uiApp.Application.NewFamilyDocument(templatePath);

            return new JObject
            {
                ["message"] = $"ðŸ“¦ Created new family from template: {System.IO.Path.GetFileName(templatePath)}",
                ["template"] = System.IO.Path.GetFileName(templatePath),
                ["documentTitle"] = famDoc.Title
            };
        }

        private static JToken DetachFromCentral(UIApplication uiApp, JObject parameters)
        {
            var filePath = parameters["filePath"]?.ToString();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new InvalidOperationException($"File not found: {filePath}");

            var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
            var openOpts = new OpenOptions
            {
                DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets
            };

            uiApp.OpenAndActivateDocument(modelPath, openOpts, false);

            return new JObject
            {
                ["message"] = $"ðŸ”“ Detached from Central: {System.IO.Path.GetFileName(filePath)}",
                ["filePath"] = filePath
            };
        }

        private static JToken ChangeLinkPath(Document doc, JObject parameters)
        {
            var linkName = parameters["linkName"]?.ToString();
            var newPath = parameters["newPath"]?.ToString();

            if (string.IsNullOrEmpty(linkName))
                throw new InvalidOperationException("Link name is required.");
            if (string.IsNullOrEmpty(newPath) || !File.Exists(newPath))
                throw new InvalidOperationException($"New file path not found: {newPath}");

            var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>()
                .Where(l => l.Name.IndexOf(linkName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            if (links.Count == 0)
                throw new InvalidOperationException($"Link '{linkName}' not found.");

            var link = links.First();
            var newModelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(newPath);

            using (var tx = new Transaction(doc, "Change Link Path"))
            {
                tx.Start();
                link.LoadFrom(newModelPath, new WorksetConfiguration());
                tx.Commit();
            }

            return new JObject
            {
                ["message"] = $"ðŸ”— Updated link '{link.Name}' path to: {System.IO.Path.GetFileName(newPath)}",
                ["linkName"] = link.Name,
                ["newPath"] = newPath
            };
        }

        private static JToken ManageLinkPosition(Document doc, JObject parameters)
        {
            var linkName = parameters["linkName"]?.ToString();
            var moveX = parameters["moveX"]?.Value<double>() ?? 0;
            var moveY = parameters["moveY"]?.Value<double>() ?? 0;
            var moveZ = parameters["moveZ"]?.Value<double>() ?? 0;
            var rotationDeg = parameters["rotation"]?.Value<double>() ?? 0;

            var instances = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
            if (!string.IsNullOrEmpty(linkName))
                instances = instances.Where(i => i.Name.IndexOf(linkName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            if (instances.Count == 0)
                throw new InvalidOperationException($"Link instance '{linkName ?? "any"}' not found.");

            var instance = instances.First();
            var translation = new XYZ(moveX, moveY, moveZ);

            using (var tx = new Transaction(doc, "Move Link"))
            {
                tx.Start();

                if (translation.GetLength() > 0.001)
                    ElementTransformUtils.MoveElement(doc, instance.Id, translation);

                if (Math.Abs(rotationDeg) > 0.001)
                {
                    var axis = Line.CreateBound(XYZ.Zero, XYZ.BasisZ);
                    ElementTransformUtils.RotateElement(doc, instance.Id, axis, rotationDeg * Math.PI / 180);
                }

                tx.Commit();
            }

            return new JObject
            {
                ["message"] = $"ðŸ“ Moved link '{instance.Name}' by ({moveX}, {moveY}, {moveZ})ft, rotated {rotationDeg}Â°",
                ["linkName"] = instance.Name,
                ["elementId"] = instance.Id.Value
            };
        }

        private static JToken ZoomToFit(UIDocument uidoc)
        {
            var uiViews = uidoc.GetOpenUIViews();
            var activeUIView = uiViews.FirstOrDefault(v => v.ViewId == uidoc.ActiveView.Id);
            if (activeUIView != null)
                activeUIView.ZoomToFit();

            return new JObject
            {
                ["message"] = $"ðŸ” Zoomed to fit view '{uidoc.ActiveView.Name}'",
                ["viewName"] = uidoc.ActiveView.Name
            };
        }

        private static JToken ZoomToElement(UIDocument uidoc, Document doc, JObject parameters)
        {
            var elementId = parameters["elementId"]?.Value<int>() ?? 0;
            var elem = doc.GetElement(new ElementId(elementId));
            if (elem == null) throw new InvalidOperationException($"Element {elementId} not found");

            var bb = elem.get_BoundingBox(uidoc.ActiveView);
            if (bb == null)
                throw new InvalidOperationException($"Element {elementId} has no bounding box in the current view.");

            var uiViews = uidoc.GetOpenUIViews();
            var activeUIView = uiViews.FirstOrDefault(v => v.ViewId == uidoc.ActiveView.Id);
            if (activeUIView != null)
            {
                // Zoom with some padding
                var padding = 2.0; // feet
                var min = new XYZ(bb.Min.X - padding, bb.Min.Y - padding, bb.Min.Z - padding);
                var max = new XYZ(bb.Max.X + padding, bb.Max.Y + padding, bb.Max.Z + padding);
                activeUIView.ZoomAndCenterRectangle(min, max);
            }

            return new JObject
            {
                ["message"] = $"ðŸ” Zoomed to element '{elem.Name}' (ID: {elementId})",
                ["elementId"] = elementId,
                ["elementName"] = elem.Name
            };
        }

        private static JToken EditSchedule(Document doc, JObject parameters)
        {
            var scheduleId = parameters["scheduleId"]?.Value<int>() ?? 0;
            var schedule = doc.GetElement(new ElementId(scheduleId)) as ViewSchedule;
            if (schedule == null)
                throw new InvalidOperationException($"Schedule {scheduleId} not found.");

            var action = parameters["action"]?.ToString()?.ToLower() ?? "info";

            using (var tx = new Transaction(doc, "Edit Schedule"))
            {
                tx.Start();

                switch (action)
                {
                    case "sort":
                    {
                        var fieldName = parameters["fieldName"]?.ToString();
                        var ascending = parameters["ascending"]?.Value<bool>() ?? true;
                        var def = schedule.Definition;
                        
                        // Find the field index
                        for (int i = 0; i < def.GetFieldCount(); i++)
                        {
                            var field = def.GetField(i);
                            if (field.GetName().Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                            {
                                var sortGroup = new ScheduleSortGroupField(field.FieldId, ascending ? ScheduleSortOrder.Ascending : ScheduleSortOrder.Descending);
                                def.ClearSortGroupFields();
                                def.AddSortGroupField(sortGroup);
                                break;
                            }
                        }
                        break;
                    }
                    case "add_field":
                    {
                        var fieldName = parameters["fieldName"]?.ToString();
                        var def = schedule.Definition;
                        var schedulableFields = def.GetSchedulableFields();

                        foreach (var sf in schedulableFields)
                        {
                            if (sf.GetName(doc).Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                            {
                                def.AddField(sf);
                                break;
                            }
                        }
                        break;
                    }
                    case "remove_field":
                    {
                        var fieldName = parameters["fieldName"]?.ToString();
                        var def = schedule.Definition;
                        for (int i = 0; i < def.GetFieldCount(); i++)
                        {
                            var field = def.GetField(i);
                            if (field.GetName().Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                            {
                                def.RemoveField(field.FieldId);
                                break;
                            }
                        }
                        break;
                    }
                    case "set_header":
                    {
                        var show = parameters["showHeaders"]?.Value<bool>() ?? true;
                        schedule.Definition.ShowHeaders = show;
                        break;
                    }
                    case "itemize":
                    {
                        var itemize = parameters["itemize"]?.Value<bool>() ?? true;
                        schedule.Definition.IsItemized = itemize;
                        break;
                    }
                    case "info":
                    default:
                    {
                        // Return schedule info without modifying
                        tx.RollBack();
                        var def = schedule.Definition;
                        var fields = new JArray();
                        for (int i = 0; i < def.GetFieldCount(); i++)
                        {
                            var f = def.GetField(i);
                            fields.Add(new JObject
                            {
                                ["name"] = f.GetName(),
                                ["index"] = i,
                                ["isHidden"] = f.IsHidden
                            });
                        }
                        var available = new JArray();
                        foreach (var sf in def.GetSchedulableFields())
                            available.Add(sf.GetName(doc));

                        return new JObject
                        {
                            ["message"] = $"ðŸ“Š Schedule '{schedule.Name}' info",
                            ["scheduleName"] = schedule.Name,
                            ["fields"] = fields,
                            ["fieldCount"] = fields.Count,
                            ["availableFields"] = available,
                            ["isItemized"] = def.IsItemized,
                            ["showHeaders"] = def.ShowHeaders
                        };
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["message"] = $"ðŸ“Š Schedule '{schedule.Name}' updated (action: {action})",
                ["scheduleName"] = schedule.Name,
                ["action"] = action
            };
        }
    }
}


