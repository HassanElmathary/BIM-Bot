using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// View and Sheet tool implementations: elevation/section/callout creation,
    /// viewport alignment, batch sheet creation, view duplication, view templates,
    /// view filters, sheet duplication, section boxes, and extend/shrink.
    /// </summary>
    public static partial class CommandExecutor
    {
        private static JToken CreateElevationViews(Document doc, JObject parameters)
        {
            var scaleStr = parameters?["scale"]?.ToString() ?? "100";
            if (!int.TryParse(scaleStr, out int scale)) scale = 100;

            // Get rooms
            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<SpatialElement>()
                .Where(r => r.Area > 0)
                .ToList();

            var levelName = parameters?["levelName"]?.ToString();
            if (!string.IsNullOrWhiteSpace(levelName))
                rooms = rooms.Where(r => r.Level?.Name?.IndexOf(levelName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var roomIdsStr = parameters?["roomIds"]?.ToString();
            if (!string.IsNullOrWhiteSpace(roomIdsStr))
            {
                var ids = roomIdsStr.Split(',').Select(s => s.Trim()).Where(s => int.TryParse(s, out _)).Select(s => int.Parse(s)).ToHashSet();
                rooms = rooms.Where(r => ids.Contains((int)r.Id.Value)).ToList();
            }

            if (rooms.Count == 0)
                return new JObject { ["message"] = "No rooms found matching the criteria." };

            // Find a default floor plan view family type for elevation markers
            var vft = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Elevation);

            if (vft == null)
                return new JObject { ["message"] = "No Elevation ViewFamilyType found in the project." };

            int created = 0;
            var names = new List<string>();

            using (var t = new Transaction(doc, "Create Elevation Views"))
            {
                t.Start();
                foreach (var room in rooms)
                {
                    try
                    {
                        var center = (room.Location as LocationPoint)?.Point;
                        if (center == null) continue;

                        var marker = ElevationMarker.CreateElevationMarker(doc, vft.Id, center, scale);
                        // Create 4 elevation views (N, S, E, W)
                        for (int i = 0; i < 4; i++)
                        {
                            try
                            {
                                var view = marker.CreateElevation(doc, doc.ActiveView.Id, i);
                                view.Scale = scale;
                                var dirs = new[] { "North", "South", "East", "West" };
                                try { view.Name = $"{room.Name} - {dirs[i]} Elevation"; } catch { /* name conflict is OK */ }
                                names.Add(view.Name);
                                created++;
                            }
                            catch (Exception ex) { Logger.Log($"Elevation creation failed: {ex.Message}"); }
                        }
                    }
                    catch (Exception ex) { Logger.Log($"Elevation marker failed for room '{room.Name}': {ex.Message}"); }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"âœ… Created {created} elevation view(s) for {rooms.Count} room(s):\n" +
                    string.Join("\n", names.Take(20)) +
                    (names.Count > 20 ? $"\n... and {names.Count - 20} more" : ""),
                ["count"] = created
            };
        }

        private static JToken CreateSectionViews(Document doc, JObject parameters)
        {
            var scaleStr = parameters?["scale"]?.ToString() ?? "50";
            if (!int.TryParse(scaleStr, out int scale)) scale = 50;
            var direction = parameters?["direction"]?.ToString() ?? "horizontal";

            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<SpatialElement>()
                .Where(r => r.Area > 0)
                .ToList();

            var roomIdsStr = parameters?["roomIds"]?.ToString();
            if (!string.IsNullOrWhiteSpace(roomIdsStr))
            {
                var ids = roomIdsStr.Split(',').Select(s => s.Trim()).Where(s => int.TryParse(s, out _)).Select(s => int.Parse(s)).ToHashSet();
                rooms = rooms.Where(r => ids.Contains((int)r.Id.Value)).ToList();
            }

            if (rooms.Count == 0)
                return new JObject { ["message"] = "No rooms found matching the criteria." };

            var vft = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Section);

            if (vft == null)
                return new JObject { ["message"] = "No Section ViewFamilyType found." };

            int created = 0;
            var names = new List<string>();

            using (var t = new Transaction(doc, "Create Section Views"))
            {
                t.Start();
                foreach (var room in rooms)
                {
                    try
                    {
                        var bb = room.get_BoundingBox(null);
                        if (bb == null) continue;

                        var center = (bb.Min + bb.Max) / 2;
                        var halfW = (bb.Max.X - bb.Min.X) / 2 + 1;
                        var halfH = (bb.Max.Z - bb.Min.Z) / 2 + 1;
                        var halfD = (bb.Max.Y - bb.Min.Y) / 2 + 1;

                        var sectionDir = direction == "vertical" ? XYZ.BasisX : XYZ.BasisY;
                        var upDir = XYZ.BasisZ;
                        var viewDir = sectionDir.CrossProduct(upDir);

                        var tf = Transform.Identity;
                        tf.Origin = center;
                        tf.BasisX = sectionDir;
                        tf.BasisY = upDir;
                        tf.BasisZ = viewDir;

                        var sectionBox = new BoundingBoxXYZ();
                        sectionBox.Transform = tf;
                        sectionBox.Min = new XYZ(-halfW, -halfH, -halfD);
                        sectionBox.Max = new XYZ(halfW, halfH, halfD);

                        var view = ViewSection.CreateSection(doc, vft.Id, sectionBox);
                        view.Scale = scale;
                        try { view.Name = $"{room.Name} - Section"; } catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
                        names.Add(view.Name);
                        created++;
                    }
                    catch (Exception ex) { Logger.Log($"Section creation failed for room '{room.Name}': {ex.Message}"); }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"âœ… Created {created} section view(s):\n" + string.Join("\n", names.Take(20)),
                ["count"] = created
            };
        }

        private static JToken CreateCalloutViews(Document doc, JObject parameters)
        {
            var scaleStr = parameters?["scale"]?.ToString() ?? "20";
            if (!int.TryParse(scaleStr, out int scale)) scale = 20;

            var parentViewIdStr = parameters?["parentViewId"]?.ToString();
            View parentView = null;

            if (!string.IsNullOrWhiteSpace(parentViewIdStr) && int.TryParse(parentViewIdStr, out int pvId))
                parentView = doc.GetElement(new ElementId(pvId)) as View;

            if (parentView == null)
            {
                // Use active view or first floor plan
                parentView = doc.ActiveView;
                if (parentView == null || parentView.IsTemplate)
                {
                    parentView = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewPlan))
                        .Cast<ViewPlan>()
                        .FirstOrDefault(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan);
                }
            }

            if (parentView == null)
                return new JObject { ["message"] = "No parent view found. Please provide parentViewId." };

            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<SpatialElement>()
                .Where(r => r.Area > 0)
                .ToList();

            var roomIdsStr = parameters?["roomIds"]?.ToString();
            if (!string.IsNullOrWhiteSpace(roomIdsStr))
            {
                var ids = roomIdsStr.Split(',').Select(s => s.Trim()).Where(s => int.TryParse(s, out _)).Select(s => int.Parse(s)).ToHashSet();
                rooms = rooms.Where(r => ids.Contains((int)r.Id.Value)).ToList();
            }

            if (rooms.Count == 0)
                return new JObject { ["message"] = "No rooms found matching the criteria." };

            var vft = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.FloorPlan);

            if (vft == null)
                return new JObject { ["message"] = "No FloorPlan ViewFamilyType found." };

            int created = 0;
            var names = new List<string>();

            using (var t = new Transaction(doc, "Create Callout Views"))
            {
                t.Start();
                foreach (var room in rooms)
                {
                    try
                    {
                        var bb = room.get_BoundingBox(null);
                        if (bb == null) continue;

                        var offset = 0.5; // 0.5 feet offset
                        var min = new XYZ(bb.Min.X - offset, bb.Min.Y - offset, bb.Min.Z);
                        var max = new XYZ(bb.Max.X + offset, bb.Max.Y + offset, bb.Max.Z);

                        var callout = ViewSection.CreateCallout(doc, parentView.Id, vft.Id, min, max);
                        callout.Scale = scale;
                        try { callout.Name = $"{room.Name} - Callout"; } catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
                        names.Add(callout.Name);
                        created++;
                    }
                    catch (Exception ex) { Logger.Log($"Callout creation failed for room '{room.Name}': {ex.Message}"); }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"âœ… Created {created} callout view(s):\n" + string.Join("\n", names.Take(20)),
                ["count"] = created
            };
        }

        private static JToken AlignViewports(Document doc, JObject parameters)
        {
            var refSheetIdStr = parameters?["referenceSheetId"]?.ToString();
            var tgtSheetIdsStr = parameters?["targetSheetIds"]?.ToString();

            if (string.IsNullOrWhiteSpace(refSheetIdStr))
                return new JObject { ["message"] = "Please provide referenceSheetId." };
            if (string.IsNullOrWhiteSpace(tgtSheetIdsStr))
                return new JObject { ["message"] = "Please provide targetSheetIds (comma-separated)." };

            if (!int.TryParse(refSheetIdStr.Trim(), out int refId))
                return new JObject { ["message"] = $"Invalid referenceSheetId: {refSheetIdStr}" };

            var refSheet = doc.GetElement(new ElementId(refId)) as ViewSheet;
            if (refSheet == null)
                return new JObject { ["message"] = $"Reference sheet not found with ID: {refSheetIdStr}" };

            // Get reference viewport positions by view name
            var refViewports = new FilteredElementCollector(doc, refSheet.Id)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            var refPositions = new Dictionary<string, XYZ>();
            foreach (var vp in refViewports)
            {
                var viewName = doc.GetElement(vp.ViewId)?.Name ?? "";
                refPositions[viewName] = vp.GetBoxCenter();
            }

            // Parse target sheet IDs
            var targetIds = tgtSheetIdsStr.Split(',')
                .Select(s => s.Trim())
                .Where(s => int.TryParse(s, out _))
                .Select(s => new ElementId(int.Parse(s)))
                .ToList();

            int aligned = 0;
            using (var t = new Transaction(doc, "Align Viewports"))
            {
                t.Start();
                foreach (var tid in targetIds)
                {
                    var sheet = doc.GetElement(tid) as ViewSheet;
                    if (sheet == null) continue;

                    var viewports = new FilteredElementCollector(doc, sheet.Id)
                        .OfClass(typeof(Viewport))
                        .Cast<Viewport>()
                        .ToList();

                    foreach (var vp in viewports)
                    {
                        var viewName = doc.GetElement(vp.ViewId)?.Name ?? "";
                        if (refPositions.TryGetValue(viewName, out XYZ refPos))
                        {
                            vp.SetBoxCenter(refPos);
                            aligned++;
                        }
                    }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"âœ… Aligned {aligned} viewport(s) across {targetIds.Count} target sheet(s) to match reference sheet.",
                ["aligned"] = aligned
            };
        }

        private static JToken ExportScheduleData(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            var schedules = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => !s.IsTitleblockRevisionSchedule)
                .ToList();

            var scheduleName = parameters?["schedule"]?.ToString();
            if (!string.IsNullOrWhiteSpace(scheduleName))
                schedules = schedules.Where(s => s.Name.IndexOf(scheduleName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            int exported = 0;
            foreach (var schedule in schedules)
            {
                try
                {
                    var opts = new ViewScheduleExportOptions();
                    var fileName = CleanFileName(schedule.Name) + ".csv";
                    schedule.Export(outputFolder, fileName, opts);
                    exported++;
                }
                catch (Exception ex) { Logger.Log($"Schedule export failed for '{schedule.Name}': {ex.Message}"); }
            }

            return new JObject
            {
                ["message"] = $"âœ… Exported {exported} schedule(s) to CSV.\nOutput folder: {outputFolder}",
                ["count"] = exported
            };
        }

        private static JToken ExportParametersToCsv(Document doc, JObject parameters)
        {
            var catName = parameters?["category"]?.ToString() ?? "Walls";
            var bic = GetBuiltInCategory(catName);
            if (bic == BuiltInCategory.INVALID)
                return new JObject { ["message"] = $"Unknown category: {catName}" };

            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            var elements = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToList();

            if (elements.Count == 0)
                return new JObject { ["message"] = $"No {catName} elements found." };

            // Collect all parameter names
            var allParams = new HashSet<string>();
            foreach (var elem in elements.Take(10))
                foreach (Parameter p in elem.Parameters)
                    if (p.Definition != null) allParams.Add(p.Definition.Name);

            var paramList = allParams.OrderBy(n => n).ToList();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("ElementId," + string.Join(",", paramList.Select(p => $"\"{p}\"")));

            foreach (var elem in elements)
            {
                var values = new List<string> { elem.Id.ToString() };
                foreach (var pn in paramList)
                {
                    var p = elem.LookupParameter(pn);
                    values.Add($"\"{(p?.HasValue == true ? p.AsValueString() ?? p.AsString() ?? "" : "")}\"");
                }
                sb.AppendLine(string.Join(",", values));
            }

            var filePath = System.IO.Path.Combine(outputFolder, $"{catName}_Parameters.csv");
            System.IO.File.WriteAllText(filePath, sb.ToString());

            return new JObject
            {
                ["message"] = $"âœ… Exported {elements.Count} {catName} elements with {paramList.Count} parameters.\nSaved to: {filePath}",
                ["count"] = elements.Count,
                ["file"] = filePath
            };
        }

        private static JToken BatchCreateSheets(Document doc, JObject parameters)
        {
            var countStr = parameters?["count"]?.ToString() ?? "5";
            if (!int.TryParse(countStr, out int count)) count = 5;
            var startNum = parameters?["startNumber"]?.ToString() ?? "A101";
            var namePattern = parameters?["namePattern"]?.ToString() ?? "Sheet {n}";
            var titleBlockName = parameters?["titleBlockName"]?.ToString();

            // Find title block
            var titleBlocks = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .ToList();

            ElementId tbId = titleBlocks.Count > 0 ? titleBlocks[0].Id : ElementId.InvalidElementId;
            if (!string.IsNullOrWhiteSpace(titleBlockName))
            {
                var match = titleBlocks.FirstOrDefault(t => t.Name.IndexOf(titleBlockName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null) tbId = match.Id;
            }

            var created = new List<string>();
            using (var t = new Transaction(doc, "Batch Create Sheets"))
            {
                t.Start();
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var sheet = ViewSheet.Create(doc, tbId);
                        var num = IncrementNumber(startNum, i);
                        sheet.SheetNumber = num;
                        sheet.Name = namePattern.Replace("{n}", (i + 1).ToString());
                        created.Add($"{num} - {sheet.Name}");
                    }
                    catch (Exception ex) { created.Add($"Error: {ex.Message}"); }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"âœ… Created {created.Count} sheet(s):\n" + string.Join("\n", created),
                ["count"] = created.Count
            };
        }

        private static JToken DuplicateView(Document doc, JObject parameters)
        {
            var viewIdStr = parameters?["viewId"]?.ToString();
            if (string.IsNullOrWhiteSpace(viewIdStr))
                return new JObject { ["message"] = "Please provide a viewId." };

            if (!int.TryParse(viewIdStr, out int id))
                return new JObject { ["message"] = $"Invalid viewId: {viewIdStr}" };

            var view = doc.GetElement(new ElementId(id)) as View;
            if (view == null)
                return new JObject { ["message"] = $"View not found with ID: {viewIdStr}" };

            var countStr = parameters?["count"]?.ToString() ?? "1";
            if (!int.TryParse(countStr, out int count)) count = 1;

            var dupType = parameters?["duplicateType"]?.ToString() ?? "with_detailing";
            ViewDuplicateOption option;
            switch (dupType)
            {
                case "independent": option = ViewDuplicateOption.Duplicate; break;
                case "as_dependent": option = ViewDuplicateOption.AsDependent; break;
                default: option = ViewDuplicateOption.WithDetailing; break;
            }

            var suffix = parameters?["suffix"]?.ToString() ?? " - Copy";
            var created = new List<string>();

            using (var t = new Transaction(doc, "Duplicate View"))
            {
                t.Start();
                for (int i = 0; i < count; i++)
                {
                    var newId = view.Duplicate(option);
                    var newView = doc.GetElement(newId) as View;
                    if (newView != null)
                    {
                        try { newView.Name = view.Name + suffix + (count > 1 ? $" {i + 1}" : ""); } catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
                        created.Add(newView.Name);
                    }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"âœ… Duplicated {created.Count} view(s):\n" + string.Join("\n", created),
                ["count"] = created.Count
            };
        }

        private static JToken ApplyViewTemplate(Document doc, JObject parameters)
        {
            var templateName = parameters?["templateName"]?.ToString();
            var viewIdsStr = parameters?["viewIds"]?.ToString();

            if (string.IsNullOrWhiteSpace(templateName))
                return new JObject { ["message"] = "Please provide a templateName." };

            // Find the view template
            var templates = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .ToList();

            var template = templates.FirstOrDefault(t => t.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase))
                ?? templates.FirstOrDefault(t => t.Name.IndexOf(templateName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (template == null)
                return new JObject
                {
                    ["message"] = $"Template '{templateName}' not found.\nAvailable templates:\n" +
                        string.Join("\n", templates.Select(t => $"  â€¢ {t.Name}"))
                };

            // Parse view IDs
            var ids = new List<ElementId>();
            if (!string.IsNullOrWhiteSpace(viewIdsStr))
            {
                foreach (var s in viewIdsStr.Split(','))
                    if (int.TryParse(s.Trim(), out int vid))
                        ids.Add(new ElementId(vid));
            }

            if (ids.Count == 0)
                return new JObject { ["message"] = "Please provide viewIds (comma-separated element IDs)." };

            int applied = 0;
            using (var t = new Transaction(doc, "Apply View Template"))
            {
                t.Start();
                foreach (var vid in ids)
                {
                    var view = doc.GetElement(vid) as View;
                    if (view != null && !view.IsTemplate)
                    {
                        view.ViewTemplateId = template.Id;
                        applied++;
                    }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"âœ… Applied template '{template.Name}' to {applied} view(s).",
                ["count"] = applied
            };
        }

        // ===== AI-DECLARED VIEW/SHEET TOOLS =====

        // ===== NEW IMPLEMENTATIONS FOR AI-DECLARED TOOLS =====

        private static JToken PlaceViewsOnSheet(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Place Views on Sheet"))
            {
                tx.Start();
                try
                {
                    var sheetId = parameters["sheetId"]?.Value<long>() ?? 0;
                    var viewIds = parameters["viewIds"] as JArray;
                    var viewId = parameters["viewId"]?.Value<long>() ?? 0;
                    var startX = parameters["startX"]?.Value<double>() ?? 1.0;
                    var startY = parameters["startY"]?.Value<double>() ?? 1.0;
                    var spacing = parameters["spacing"]?.Value<double>() ?? 1.0;
                    var x = parameters["x"]?.Value<double>() ?? startX;
                    var y = parameters["y"]?.Value<double>() ?? startY;

                    // Allow sheetNumber as alternative
                    if (sheetId == 0 && parameters["sheetNumber"] != null)
                    {
                        var sheetNum = parameters["sheetNumber"].ToString();
                        var sheet = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().FirstOrDefault(s => s.SheetNumber == sheetNum);
                        if (sheet != null) sheetId = sheet.Id.Value;
                    }

                    if (sheetId == 0) throw new InvalidOperationException("sheetId (or sheetNumber) is required");

                    // Single view placement (backward compat with place_view_on_sheet)
                    if (viewIds == null || viewIds.Count == 0)
                    {
                        if (viewId == 0) throw new InvalidOperationException("viewId or viewIds required");
                        var vp = Viewport.Create(doc, new ElementId(sheetId), new ElementId(viewId), new XYZ(x, y, 0));
                        tx.Commit();
                        return new JObject { ["message"] = $"âœ… Placed view on sheet (Viewport ID: {vp.Id.Value})", ["viewportId"] = vp.Id.Value };
                    }
     
                    // Multiple views
                    int placed = 0;
                    var results = new JArray();
                    foreach (var vid in viewIds)
                    {
                        var id = vid.Value<long>();
                        try
                        {
                            var vp = Viewport.Create(doc, new ElementId(sheetId), new ElementId(id), new XYZ(startX + placed * spacing, startY, 0));
                            results.Add(new JObject { ["viewId"] = id, ["viewportId"] = vp.Id.Value });
                            placed++;
                        }
                        catch { /* skip views that can't be placed */ }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"âœ… Placed {placed} view(s) on sheet", ["viewports"] = results };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken IsolateWarnings(UIDocument uidoc, Document doc, JObject parameters)
        {
            var filter = parameters["filter"]?.ToString();
            var warnings = doc.GetWarnings();
            var elementIds = new HashSet<ElementId>();

            foreach (var warning in warnings)
            {
                if (!string.IsNullOrEmpty(filter) &&
                    !warning.GetDescriptionText().IndexOf(filter, StringComparison.OrdinalIgnoreCase).Equals(-1) == false &&
                    !warning.GetDescriptionText().ToLower().Contains(filter.ToLower()))
                    continue;

                foreach (var id in warning.GetFailingElements())
                    elementIds.Add(id);
            }

            if (elementIds.Count > 0)
                uidoc.Selection.SetElementIds(elementIds.ToList());

            var warningDescriptions = new JArray();
            foreach (var w in warnings)
            {
                if (!string.IsNullOrEmpty(filter) && !w.GetDescriptionText().ToLower().Contains(filter.ToLower()))
                    continue;
                warningDescriptions.Add(new JObject
                {
                    ["description"] = w.GetDescriptionText(),
                    ["severity"] = w.GetSeverity().ToString(),
                    ["elementIds"] = new JArray(w.GetFailingElements().Select(id => id.Value))
                });
            }

            return new JObject
            {
                ["message"] = $"âœ… Found {warningDescriptions.Count} warning(s), selected {elementIds.Count} element(s)",
                ["warnings"] = warningDescriptions,
                ["selectedCount"] = elementIds.Count
            };
        }

        private static JToken BulkRenameViews(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Bulk Rename Views"))
            {
                tx.Start();
                try
                {
                    var find = parameters["find"]?.ToString();
                    var replace = parameters["replace"]?.ToString() ?? "";
                    var targetType = parameters["targetType"]?.ToString()?.ToLower() ?? parameters["scope"]?.ToString() ?? "both";

                    if (string.IsNullOrEmpty(find))
                        throw new InvalidOperationException("'find' text is required");

                    int renamed = 0;

                    if (targetType == "views" || targetType == "both" || targetType == "all" || targetType == "Views" || targetType == "All")
                    {
                        var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate && !(v is ViewSheet)).ToList();
                        foreach (var v in views)
                        {
                            if (v.Name.Contains(find))
                            {
                                try { v.Name = v.Name.Replace(find, replace); renamed++; } catch (Exception ex) { Logger.Log($"Rename view failed: {ex.Message}"); }
                            }
                        }
                    }

                    if (targetType == "sheets" || targetType == "both" || targetType == "all" || targetType == "Sheets" || targetType == "All")
                    {
                        var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().ToList();
                        foreach (var s in sheets)
                        {
                            if (s.Name.Contains(find))
                            {
                                try { s.Name = s.Name.Replace(find, replace); renamed++; } catch (Exception ex) { Logger.Log($"Rename sheet failed: {ex.Message}"); }
                            }
                        }
                    }

                    // Also support "Types" scope for backward compat with find_replace_names
                    if (targetType == "types" || targetType == "Types")
                    {
                        var allTypes = new FilteredElementCollector(doc).WhereElementIsElementType().ToList();
                        foreach (var t in allTypes)
                        {
                            if (t.Name.Contains(find))
                            {
                                try { t.Name = t.Name.Replace(find, replace); renamed++; } catch (Exception ex) { Logger.Log($"Rename type failed: {ex.Message}"); }
                            }
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"âœ… Replaced '{find}' â†’ '{replace}' in {renamed} name(s)", ["count"] = renamed };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CopyParameterValue(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Copy Parameter Value"))
            {
                tx.Start();
                try
                {
                    var sourceId = parameters["sourceElementId"]?.Value<long>() ?? 0;
                    var paramName = parameters["parameterName"]?.ToString();
                    var targetIds = parameters["targetElementIds"] as JArray;

                    // Also support bulk_parameter_transfer params
                    var sourceParam = parameters["sourceParameter"]?.ToString() ?? paramName;
                    var targetParam = parameters["targetParameter"]?.ToString() ?? paramName;

                    if (string.IsNullOrEmpty(sourceParam))
                        throw new InvalidOperationException("parameterName (or sourceParameter) is required");

                    // If source element ID provided, copy from it to targets
                    if (sourceId > 0 && targetIds != null)
                    {
                        var sourceElem = doc.GetElement(new ElementId(sourceId));
                        if (sourceElem == null) throw new InvalidOperationException($"Source element {sourceId} not found");

                        string sourceValue = null;
                        foreach (Parameter p in sourceElem.Parameters)
                        {
                            if (p.Definition.Name == sourceParam)
                            {
                                sourceValue = p.AsValueString() ?? p.AsString() ?? "";
                                break;
                            }
                        }
                        if (sourceValue == null) throw new InvalidOperationException($"Parameter '{sourceParam}' not found on source element");

                        int transferred = 0;
                        foreach (var tid in targetIds)
                        {
                            var targetElem = doc.GetElement(new ElementId(tid.Value<long>()));
                            if (targetElem == null) continue;
                            foreach (Parameter p in targetElem.Parameters)
                            {
                                if (p.Definition.Name == targetParam && !p.IsReadOnly)
                                {
                                    if (p.StorageType == StorageType.String) { p.Set(sourceValue); transferred++; }
                                    else if (p.StorageType == StorageType.Double && double.TryParse(sourceValue, out double d)) { p.Set(d); transferred++; }
                                    else if (p.StorageType == StorageType.Integer && int.TryParse(sourceValue, out int i)) { p.Set(i); transferred++; }
                                    break;
                                }
                            }
                        }

                        tx.Commit();
                        return new JObject { ["message"] = $"âœ… Copied '{sourceParam}' value to {transferred} element(s)" };
                    }
                    else
                    {
                        // Fallback: bulk transfer within a category
                        var categoryName = parameters["category"]?.ToString();
                        if (string.IsNullOrEmpty(categoryName)) throw new InvalidOperationException("Either sourceElementId+targetElementIds or category is required");

                        var bic = GetBuiltInCategory(categoryName);
                        var elements = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToList();

                        int transferred = 0;
                        foreach (var elem in elements)
                        {
                            Parameter src = null, tgt = null;
                            foreach (Parameter p in elem.Parameters)
                            {
                                if (p.Definition.Name == sourceParam) src = p;
                                if (p.Definition.Name == targetParam) tgt = p;
                            }
                            if (src != null && tgt != null && !tgt.IsReadOnly)
                            {
                                var val = src.AsValueString() ?? src.AsString() ?? "";
                                if (tgt.StorageType == StorageType.String) { tgt.Set(val); transferred++; }
                                else if (tgt.StorageType == StorageType.Double && double.TryParse(val, out double d)) { tgt.Set(d); transferred++; }
                                else if (tgt.StorageType == StorageType.Integer && int.TryParse(val, out int i)) { tgt.Set(i); transferred++; }
                            }
                        }

                        tx.Commit();
                        return new JObject { ["message"] = $"âœ… Transferred '{sourceParam}' â†’ '{targetParam}' on {transferred} element(s)" };
                    }
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken SelectByFilter(UIDocument uidoc, Document doc, JObject parameters)
        {
            var categoryName = parameters["category"]?.ToString();
            var familyName = parameters["familyName"]?.ToString();
            var typeName = parameters["typeName"]?.ToString();
            var levelName = parameters["levelName"]?.ToString();

            var collector = new FilteredElementCollector(doc);
            if (!string.IsNullOrEmpty(categoryName))
            {
                var bic = GetBuiltInCategory(categoryName);
                if (bic != BuiltInCategory.INVALID)
                    collector = collector.OfCategory(bic);
            }
            var elements = collector.WhereElementIsNotElementType().ToList();

            var matching = new List<ElementId>();
            foreach (var elem in elements)
            {
                if (!string.IsNullOrEmpty(familyName))
                {
                    var famParam = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM);
                    var famVal = famParam?.AsValueString() ?? "";
                    if (!famVal.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                        !famVal.Contains(familyName))
                        continue;
                }

                if (!string.IsNullOrEmpty(typeName))
                {
                    var typeId = elem.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        var type = doc.GetElement(typeId);
                        if (type != null && !type.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) &&
                            !type.Name.Contains(typeName))
                            continue;
                    }
                }

                if (!string.IsNullOrEmpty(levelName))
                {
                    var lvlParam = elem.get_Parameter(BuiltInParameter.LEVEL_PARAM) ??
                                   elem.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                    var lvlVal = lvlParam?.AsValueString() ?? "";
                    if (!lvlVal.Equals(levelName, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                matching.Add(elem.Id);
            }

            uidoc.Selection.SetElementIds(matching);
            return new JObject
            {
                ["message"] = $"âœ… Selected {matching.Count} element(s)",
                ["count"] = matching.Count,
                ["elementIds"] = new JArray(matching.Select(id => id.Value))
            };
        }

        private static JToken DuplicateSheets(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Duplicate Sheet"))
            {
                tx.Start();
                try
                {
                    var sheetId = parameters["sheetId"]?.Value<long>() ?? 0;
                    var count = parameters["count"]?.Value<int>() ?? 1;
                    var suffix = parameters["suffix"]?.ToString() ?? " - Copy";

                    var sourceSheet = doc.GetElement(new ElementId(sheetId)) as ViewSheet;
                    if (sourceSheet == null) throw new InvalidOperationException($"Sheet {sheetId} not found");

                    // Get the title block from the source sheet
                    var titleBlocks = new FilteredElementCollector(doc, sourceSheet.Id)
                        .OfCategory(BuiltInCategory.OST_TitleBlocks)
                        .WhereElementIsNotElementType()
                        .ToList();
                    var tbTypeId = titleBlocks.FirstOrDefault()?.GetTypeId() ?? ElementId.InvalidElementId;

                    var created = new JArray();
                    for (int i = 0; i < count; i++)
                    {
                        var newSheet = ViewSheet.Create(doc, tbTypeId);
                        var newNumber = sourceSheet.SheetNumber + suffix + (count > 1 ? $" {i + 1}" : "");
                        try { newSheet.SheetNumber = newNumber; } catch (Exception ex) { Logger.Log($"Sheet number assignment failed: {ex.Message}"); }
                        newSheet.Name = sourceSheet.Name;

                        created.Add(new JObject { ["sheetId"] = newSheet.Id.Value, ["number"] = newSheet.SheetNumber });
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"âœ… Created {count} sheet copy(ies)", ["sheets"] = created };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken AutoSectionBox(UIDocument uidoc, Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Auto Section Box"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    var padding = parameters["padding"]?.Value<double>() ?? 2.0;

                    if (elementIds == null || elementIds.Count == 0)
                        throw new InvalidOperationException("elementIds are required");

                    // Calculate bounding box around all elements
                    double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                    double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

                    foreach (var eid in elementIds)
                    {
                        var elem = doc.GetElement(new ElementId(eid.Value<long>()));
                        if (elem == null) continue;
                        var bb = elem.get_BoundingBox(null);
                        if (bb == null) continue;
                        if (bb.Min.X < minX) minX = bb.Min.X;
                        if (bb.Min.Y < minY) minY = bb.Min.Y;
                        if (bb.Min.Z < minZ) minZ = bb.Min.Z;
                        if (bb.Max.X > maxX) maxX = bb.Max.X;
                        if (bb.Max.Y > maxY) maxY = bb.Max.Y;
                        if (bb.Max.Z > maxZ) maxZ = bb.Max.Z;
                    }

                    if (minX == double.MaxValue)
                        throw new InvalidOperationException("No valid bounding boxes found for specified elements");

                    // Get or create a 3D view
                    var view3d = uidoc.ActiveView as View3D;
                    if (view3d == null)
                    {
                        var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                            .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);
                        if (vft != null)
                        {
                            view3d = View3D.CreateIsometric(doc, vft.Id);
                            view3d.Name = "AI Section Box";
                        }
                    }
                    if (view3d == null) throw new InvalidOperationException("Cannot create or find a 3D view");

                    // Apply section box with padding
                    var sectionBox = new BoundingBoxXYZ
                    {
                        Min = new XYZ(minX - padding, minY - padding, minZ - padding),
                        Max = new XYZ(maxX + padding, maxY + padding, maxZ + padding)
                    };
                    view3d.SetSectionBox(sectionBox);
                    uidoc.ActiveView = view3d;

                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"âœ… Section box applied around {elementIds.Count} element(s) with {padding}ft padding",
                        ["viewId"] = view3d.Id.Value,
                        ["viewName"] = view3d.Name
                    };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CopyViewFilters(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Copy View Filters"))
            {
                tx.Start();
                try
                {
                    var sourceViewId = parameters["sourceViewId"]?.Value<long>() ?? 0;
                    var targetViewIds = parameters["targetViewIds"] as JArray;

                    var sourceView = doc.GetElement(new ElementId(sourceViewId)) as View;
                    if (sourceView == null) throw new InvalidOperationException($"Source view {sourceViewId} not found");
                    if (targetViewIds == null || targetViewIds.Count == 0) throw new InvalidOperationException("targetViewIds required");

                    var filterIds = sourceView.GetFilters();
                    int copiedCount = 0;

                    foreach (var tvid in targetViewIds)
                    {
                        var targetView = doc.GetElement(new ElementId(tvid.Value<long>())) as View;
                        if (targetView == null) continue;

                        foreach (var filterId in filterIds)
                        {
                            try
                            {
                                var overrides = sourceView.GetFilterOverrides(filterId);
                                var visibility = sourceView.GetFilterVisibility(filterId);
                                // Remove existing filter if present, then add
                                if (targetView.GetFilters().Contains(filterId))
                                    targetView.RemoveFilter(filterId);
                                targetView.AddFilter(filterId);
                                targetView.SetFilterOverrides(filterId, overrides);
                                targetView.SetFilterVisibility(filterId, visibility);
                                copiedCount++;
                            }
                            catch { /* skip filters that can't be applied */ }
                        }
                    }

                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"âœ… Copied {filterIds.Count} filter(s) to {targetViewIds.Count} view(s) ({copiedCount} total applications)",
                        ["filtersCopied"] = filterIds.Count,
                        ["viewsUpdated"] = targetViewIds.Count
                    };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken ExtendShrinkElement(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Extend/Shrink Element"))
            {
                tx.Start();
                try
                {
                    var elementId = parameters["elementId"]?.Value<long>() ?? 0;
                    var delta = parameters["delta"]?.Value<double>() ?? 0;
                    var end = parameters["end"]?.ToString()?.ToLower() ?? "end";

                    var elem = doc.GetElement(new ElementId(elementId));
                    if (elem == null) throw new InvalidOperationException($"Element {elementId} not found");

                    // Try to get location curve
                    var locCurve = elem.Location as LocationCurve;
                    if (locCurve == null) throw new InvalidOperationException("Element does not have a line-based location");

                    var curve = locCurve.Curve;
                    if (!(curve is Line line)) throw new InvalidOperationException("Element curve is not a straight line");

                    var direction = (line.GetEndPoint(1) - line.GetEndPoint(0)).Normalize();
                    XYZ newStart = line.GetEndPoint(0);
                    XYZ newEnd = line.GetEndPoint(1);

                    if (end == "start")
                        newStart = newStart - direction * delta;
                    else
                        newEnd = newEnd + direction * delta;

                    if (newStart.DistanceTo(newEnd) < 0.01)
                        throw new InvalidOperationException("Resulting element would be too short");

                    locCurve.Curve = Line.CreateBound(newStart, newEnd);

                    tx.Commit();
                    var action = delta >= 0 ? "Extended" : "Shrunk";
                    return new JObject
                    {
                        ["message"] = $"âœ… {action} element at {end} end by {Math.Abs(delta)} ft",
                        ["newLength"] = Math.Round(newStart.DistanceTo(newEnd), 4)
                    };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

    }
}
