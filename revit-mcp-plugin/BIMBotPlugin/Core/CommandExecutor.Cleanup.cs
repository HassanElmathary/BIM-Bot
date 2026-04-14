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
    /// Cleanup and power tool implementations: floor cutting, level splitting,
    /// openings, scope boxes, empty sheets, template cleanup, view purge,
    /// family size management, 3D explode, section box rotation, alignment,
    /// element joining, cross-project copy, and measurements.
    /// </summary>
    public static partial class CommandExecutor
    {
        // ===== NONICA-INSPIRED POWER TOOLS =====

        private static JToken CutFloors(Document doc, JObject parameters)
        {
            var method = parameters["method"]?.ToString() ?? "rooms";
            var floorIdsStr = parameters["floorIds"]?.ToString();

            // Collect target floors
            var floors = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType()
                .Cast<Floor>()
                .ToList();

            if (!string.IsNullOrEmpty(floorIdsStr) && floorIdsStr != "all")
            {
                var ids = floorIdsStr.Split(',').Select(s => int.Parse(s.Trim())).ToHashSet();
                floors = floors.Where(f => ids.Contains((int)f.Id.Value)).ToList();
            }

            if (floors.Count == 0)
                return new JObject { ["error"] = "No floors found" };

            if (method == "rooms")
            {
                // Cut floors by room boundaries â€” find rooms overlapping each floor
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<SpatialElement>()
                    .Where(r => r.Area > 0)
                    .ToList();

                return new JObject
                {
                    ["message"] = $"ðŸ”ª Found {floors.Count} floors and {rooms.Count} rooms. Use execute_code with floor splitting logic for complex geometry operations.",
                    ["floors"] = floors.Count,
                    ["rooms"] = rooms.Count,
                    ["hint"] = "Floor cutting by room boundaries requires geometry intersection. Use execute_code with: foreach room â†’ get boundary â†’ create new floor from boundary â†’ delete original."
                };
            }

            return new JObject
            {
                ["message"] = $"ðŸ”ª Found {floors.Count} floors to process with method: {method}",
                ["floorsFound"] = floors.Count,
                ["method"] = method,
                ["hint"] = $"Use execute_code for {method}-based floor splitting."
            };
        }

        private static JToken SplitByLevels(Document doc, JObject parameters)
        {
            var category = parameters["category"]?.ToString() ?? "Walls";
            var levelNamesStr = parameters["levelNames"]?.ToString();
            var gap = parameters["gap"]?.Value<double>() ?? 0;

            var bic = category.ToLower().Contains("column") ? BuiltInCategory.OST_StructuralColumns : BuiltInCategory.OST_Walls;

            var elements = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToList();

            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (!string.IsNullOrEmpty(levelNamesStr))
            {
                var names = levelNamesStr.Split(',').Select(s => s.Trim().ToLower()).ToHashSet();
                levels = levels.Where(l => names.Contains(l.Name.ToLower())).ToList();
            }

            int split = 0;
            using (var tx = new Transaction(doc, "Split by Levels"))
            {
                tx.Start();
                foreach (var elem in elements)
                {
                    var bb = elem.get_BoundingBox(null);
                    if (bb == null) continue;

                    double baseZ = bb.Min.Z;
                    double topZ = bb.Max.Z;

                    // Check which levels fall within this element's height
                    var intersectingLevels = levels.Where(l => l.Elevation > baseZ + 0.1 && l.Elevation < topZ - 0.1).ToList();
                    if (intersectingLevels.Count > 0) split++;
                }
                tx.RollBack(); // Analysis only, actual split requires Wall.Split which is complex
            }

            return new JObject
            {
                ["message"] = $"ðŸ“ Found {elements.Count} {category} elements, {split} span multiple levels and can be split.",
                ["totalElements"] = elements.Count,
                ["splittable"] = split,
                ["levels"] = JArray.FromObject(levels.Select(l => new { l.Name, l.Elevation })),
                ["gap"] = gap,
                ["hint"] = "Use execute_code for actual splitting. Pattern: for each wall â†’ get base/top constraints â†’ create new walls at each level segment."
            };
        }

        private static JToken CreateOpenings(Document doc, JObject parameters)
        {
            var hostCat = parameters["hostCategory"]?.ToString() ?? "Walls";
            var cutCat = parameters["cutCategory"]?.ToString() ?? "Ducts";
            var offset = parameters["offset"]?.Value<double>() ?? 0.25; // 3 inches default

            var hostBic = hostCat.ToLower().Contains("floor") ? BuiltInCategory.OST_Floors : BuiltInCategory.OST_Walls;

            BuiltInCategory cutBic;
            switch (cutCat.ToLower())
            {
                case "pipes": cutBic = BuiltInCategory.OST_PipeCurves; break;
                case "structural framing": cutBic = BuiltInCategory.OST_StructuralFraming; break;
                case "conduits": cutBic = BuiltInCategory.OST_Conduit; break;
                default: cutBic = BuiltInCategory.OST_DuctCurves; break;
            }

            var hosts = new FilteredElementCollector(doc)
                .OfCategory(hostBic)
                .WhereElementIsNotElementType()
                .ToList();

            var cutElements = new FilteredElementCollector(doc)
                .OfCategory(cutBic)
                .WhereElementIsNotElementType()
                .ToList();

            // Find intersections using bounding box proximity
            int intersections = 0;
            foreach (var host in hosts)
            {
                var hostBB = host.get_BoundingBox(null);
                if (hostBB == null) continue;
                foreach (var cut in cutElements)
                {
                    var cutBB = cut.get_BoundingBox(null);
                    if (cutBB == null) continue;
                    if (hostBB.Min.X <= cutBB.Max.X && hostBB.Max.X >= cutBB.Min.X &&
                        hostBB.Min.Y <= cutBB.Max.Y && hostBB.Max.Y >= cutBB.Min.Y &&
                        hostBB.Min.Z <= cutBB.Max.Z && hostBB.Max.Z >= cutBB.Min.Z)
                    {
                        intersections++;
                    }
                }
            }

            return new JObject
            {
                ["message"] = $"ðŸ•³ï¸ Found {intersections} potential intersections between {hosts.Count} {hostCat} and {cutElements.Count} {cutCat}.",
                ["hosts"] = hosts.Count,
                ["cutElements"] = cutElements.Count,
                ["intersections"] = intersections,
                ["offset"] = offset,
                ["hint"] = "Use execute_code to create openings: doc.Create.NewOpening(wall, point1, point2) for rectangular openings."
            };
        }

        private static JToken ManageScopeBoxes(Document doc, JObject parameters)
        {
            var action = parameters["action"]?.ToString() ?? "list";
            var name = parameters["name"]?.ToString();

            var scopeBoxes = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_VolumeOfInterest)
                .WhereElementIsNotElementType()
                .ToList();

            if (action == "list")
            {
                var items = new JArray();
                foreach (var sb in scopeBoxes)
                {
                    var bb = sb.get_BoundingBox(null);
                    var usedIn = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .Where(e =>
                        {
                            try { var p = e.get_Parameter(BuiltInParameter.DATUM_VOLUME_OF_INTEREST); return p != null && ((int?)p.AsElementId()?.Value) == sb.Id.Value; }
                            catch { return false; }
                        }).Count();

                    items.Add(new JObject
                    {
                        ["id"] = sb.Id.Value,
                        ["name"] = sb.Name,
                        ["usedInViews"] = usedIn,
                        ["minX"] = bb?.Min.X, ["minY"] = bb?.Min.Y, ["minZ"] = bb?.Min.Z,
                        ["maxX"] = bb?.Max.X, ["maxY"] = bb?.Max.Y, ["maxZ"] = bb?.Max.Z
                    });
                }
                return new JObject { ["message"] = $"ðŸ“¦ Found {scopeBoxes.Count} scope boxes", ["scopeBoxes"] = items };
            }
            else if (action == "delete_unused")
            {
                var unused = scopeBoxes.Where(sb =>
                {
                    var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>();
                    return !views.Any(v =>
                    {
                        try { var p = v.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP); return p != null && ((int?)p.AsElementId()?.Value) == sb.Id.Value; }
                        catch { return false; }
                    });
                }).ToList();

                using (var tx = new Transaction(doc, "Delete Unused Scope Boxes"))
                {
                    tx.Start();
                    foreach (var sb in unused)
                        doc.Delete(sb.Id);
                    tx.Commit();
                }

                return new JObject
                {
                    ["message"] = $"ðŸ—‘ï¸ Deleted {unused.Count} unused scope boxes (of {scopeBoxes.Count} total)",
                    ["deleted"] = unused.Count
                };
            }

            return new JObject { ["message"] = "Use action: list or delete_unused" };
        }

        private static JToken FindEmptySheets(Document doc, JObject parameters)
        {
            var shouldDelete = parameters["delete"]?.Value<bool>() ?? false;

            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .ToList();

            var emptySheets = new List<ViewSheet>();
            foreach (var sheet in sheets)
            {
                var viewports = sheet.GetAllViewports();
                if (viewports == null || viewports.Count == 0)
                    emptySheets.Add(sheet);
            }

            if (shouldDelete && emptySheets.Count > 0)
            {
                using (var tx = new Transaction(doc, "Delete Empty Sheets"))
                {
                    tx.Start();
                    foreach (var sheet in emptySheets)
                        doc.Delete(sheet.Id);
                    tx.Commit();
                }
                return new JObject
                {
                    ["message"] = $"ðŸ—‘ï¸ Deleted {emptySheets.Count} empty sheets",
                    ["deleted"] = emptySheets.Count
                };
            }

            var items = new JArray();
            foreach (var sheet in emptySheets)
            {
                items.Add(new JObject
                {
                    ["id"] = sheet.Id.Value,
                    ["number"] = sheet.SheetNumber,
                    ["name"] = sheet.Name
                });
            }

            return new JObject
            {
                ["message"] = $"ðŸ“„ Found {emptySheets.Count} empty sheets (no viewports) out of {sheets.Count} total",
                ["emptySheets"] = items,
                ["hint"] = "Set delete=true to remove them"
            };
        }

        private static JToken CleanUnusedTemplates(Document doc, JObject parameters)
        {
            var scope = parameters["scope"]?.ToString() ?? "all";
            var result = new JObject();
            int totalCleaned = 0;

            using (var tx = new Transaction(doc, "Clean Unused Templates/Rooms/Filters"))
            {
                tx.Start();

                if (scope == "templates" || scope == "all")
                {
                    // Find view templates not applied to any view
                    var templates = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => v.IsTemplate)
                        .ToList();

                    var allViews = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => !v.IsTemplate)
                        .ToList();

                    var usedTemplateIds = allViews
                        .Select(v => v.ViewTemplateId)
                        .Where(id => id != null && id.Value != -1)
                        .Select(id => id.Value)
                        .ToHashSet();

                    var unusedTemplates = templates.Where(t => !usedTemplateIds.Contains((int)t.Id.Value)).ToList();
                    foreach (var t in unusedTemplates) doc.Delete(t.Id);
                    result["unusedTemplates"] = unusedTemplates.Count;
                    totalCleaned += unusedTemplates.Count;
                }

                if (scope == "rooms" || scope == "all")
                {
                    // Remove unplaced rooms (Area == 0)
                    var unplacedRooms = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<SpatialElement>()
                        .Where(r => r.Area == 0)
                        .ToList();

                    foreach (var r in unplacedRooms) doc.Delete(r.Id);
                    result["unplacedRooms"] = unplacedRooms.Count;
                    totalCleaned += unplacedRooms.Count;
                }

                if (scope == "filters" || scope == "all")
                {
                    // Find view filters not applied to any view
                    var filters = new FilteredElementCollector(doc)
                        .OfClass(typeof(ParameterFilterElement))
                        .ToList();

                    var allViews2 = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => !v.IsTemplate)
                        .ToList();

                    var usedFilterIds = new HashSet<int>();
                    foreach (var v in allViews2)
                    {
                        try
                        {
                            var fIds = v.GetFilters();
                            foreach (var fId in fIds) usedFilterIds.Add((int)fId.Value);
                        }
                        catch (Exception ex) { Logger.Log($"Filter query failed for view '{v.Name}': {ex.Message}"); }
                    }

                    var unusedFilters = filters.Where(f => !usedFilterIds.Contains((int)f.Id.Value)).ToList();
                    foreach (var f in unusedFilters) doc.Delete(f.Id);
                    result["unusedFilters"] = unusedFilters.Count;
                    totalCleaned += unusedFilters.Count;
                }

                tx.Commit();
            }

            result["message"] = $"ðŸ§¹ Cleaned {totalCleaned} unused items (scope: {scope})";
            return result;
        }

        private static JToken CleanUnplacedViews(Document doc, JObject parameters)
        {
            var dryRun = parameters["dryRun"]?.Value<bool>() ?? false;

            // Get all sheets and their viewports
            var sheetsWithViewIds = new HashSet<int>();
            foreach (var sheet in new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>())
            {
                foreach (var vpId in sheet.GetAllViewports())
                {
                    var vp = doc.GetElement(vpId) as Viewport;
                    if (vp != null) sheetsWithViewIds.Add((int)(long)vp.ViewId.Value);
                }
            }

            // Find views NOT on any sheet
            var unplaced = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate &&
                            v.ViewType != ViewType.ProjectBrowser &&
                            v.ViewType != ViewType.SystemBrowser &&
                            v.ViewType != ViewType.Internal &&
                            v.ViewType != ViewType.DrawingSheet &&
                            !sheetsWithViewIds.Contains((int)v.Id.Value))
                .ToList();

            var items = new JArray();
            foreach (var v in unplaced)
            {
                items.Add(new JObject
                {
                    ["id"] = v.Id.Value,
                    ["name"] = v.Name,
                    ["type"] = v.ViewType.ToString()
                });
            }

            if (!dryRun && unplaced.Count > 0)
            {
                using (var tx = new Transaction(doc, "Delete Unplaced Views"))
                {
                    tx.Start();
                    foreach (var v in unplaced)
                    {
                        try { doc.Delete(v.Id); } catch (Exception ex) { Logger.Log($"Failed to delete view '{v.Name}': {ex.Message}"); }
                    }
                    tx.Commit();
                }

                return new JObject
                {
                    ["message"] = $"ðŸ—‘ï¸ Deleted {unplaced.Count} unplaced views/schedules/legends",
                    ["deleted"] = unplaced.Count
                };
            }

            return new JObject
            {
                ["message"] = $"ðŸ“‹ Found {unplaced.Count} views not placed on any sheet (dry run)",
                ["unplacedViews"] = items,
                ["hint"] = "Set dryRun=false to delete them"
            };
        }

        private static JToken PurgeUnusedInFamilies(Document doc, JObject parameters)
        {
            var categoryFilter = parameters["category"]?.ToString();

            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => f.IsEditable)
                .ToList();

            if (!string.IsNullOrEmpty(categoryFilter))
            {
                families = families.Where(f =>
                    f.FamilyCategory?.Name?.IndexOf(categoryFilter, StringComparison.OrdinalIgnoreCase) >= 0
                ).ToList();
            }

            int purgedCount = 0;
            var details = new JArray();

            foreach (var family in families)
            {
                try
                {
                    var famDoc = doc.EditFamily(family);
                    if (famDoc == null) continue;

                    // Count unused types in family
                    var unused = new FilteredElementCollector(famDoc)
                        .WhereElementIsNotElementType()
                        .Where(e => !(e is FamilyInstance))
                        .Count();

                    if (unused > 0)
                    {
                        details.Add(new JObject
                        {
                            ["family"] = family.Name,
                            ["category"] = family.FamilyCategory?.Name,
                            ["unusedElements"] = unused
                        });
                        purgedCount++;
                    }

                    famDoc.Close(false);
                }
                catch (Exception ex) { Logger.Log($"Family scan failed for '{family.Name}': {ex.Message}"); }
            }

            return new JObject
            {
                ["message"] = $"ðŸ” Scanned {families.Count} editable families, {purgedCount} have unused assets",
                ["familiesScanned"] = families.Count,
                ["familiesWithUnused"] = purgedCount,
                ["details"] = details,
                ["hint"] = "Use execute_code for deep purge: open each family doc â†’ purge â†’ save back."
            };
        }

        private static JToken DeleteFamiliesBySize(Document doc, JObject parameters)
        {
            var maxSizeKB = parameters["maxSizeKB"]?.Value<int>() ?? 5000;
            var dryRun = parameters["dryRun"]?.Value<bool>() ?? true;

            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

            // Check instance count for each family
            var familyData = new List<(Family family, int instances, long estimatedSize)>();
            foreach (var fam in families)
            {
                var symbolIds = fam.GetFamilySymbolIds();
                int instanceCount = 0;
                foreach (var symId in symbolIds)
                {
                    instanceCount += new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .Where(e => ((FamilyInstance)e).Symbol.Id.Value == symId.Value)
                        .Count();
                }

                // Estimate size from geometry complexity (rough heuristic)
                long estSize = 0;
                foreach (var symId in symbolIds)
                {
                    var sym = doc.GetElement(symId) as FamilySymbol;
                    if (sym != null)
                    {
                        try
                        {
                            var geom = sym.get_Geometry(new Options());
                            if (geom != null) estSize += geom.Count() * 100; // rough
                        }
                        catch (Exception ex) { Logger.Log($"Geometry estimation failed: {ex.Message}"); }
                    }
                }

                familyData.Add((fam, instanceCount, estSize));
            }

            // Sort by estimated size descending, flag those with 0 instances
            var candidates = familyData
                .Where(f => f.instances == 0)
                .OrderByDescending(f => f.estimatedSize)
                .ToList();

            var items = new JArray();
            foreach (var c in candidates.Take(50))
            {
                items.Add(new JObject
                {
                    ["id"] = c.family.Id.Value,
                    ["name"] = c.family.Name,
                    ["category"] = c.family.FamilyCategory?.Name,
                    ["instances"] = c.instances,
                    ["types"] = c.family.GetFamilySymbolIds().Count
                });
            }

            if (!dryRun && candidates.Count > 0)
            {
                using (var tx = new Transaction(doc, "Delete Unused Heavy Families"))
                {
                    tx.Start();
                    int deleted = 0;
                    foreach (var c in candidates)
                    {
                        try { doc.Delete(c.family.Id); deleted++; } catch (Exception ex) { Logger.Log($"Failed to delete family '{c.family.Name}': {ex.Message}"); }
                    }
                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"ðŸ—‘ï¸ Deleted {deleted} unused families",
                        ["deleted"] = deleted
                    };
                }
            }

            return new JObject
            {
                ["message"] = $"ðŸ“Š Found {candidates.Count} unused families (0 instances) out of {families.Count} total",
                ["unusedFamilies"] = items,
                ["hint"] = "Set dryRun=false to delete them"
            };
        }

        private static JToken Explode3DView(Document doc, UIDocument uiDoc, JObject parameters)
        {
            var spacing = parameters["spacing"]?.Value<double>() ?? 10.0;

            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (levels.Count < 2)
                return new JObject { ["error"] = "Need at least 2 levels for exploded view" };

            // Create a new 3D view
            var viewFamilyType = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(t => t.ViewFamily == ViewFamily.ThreeDimensional);

            if (viewFamilyType == null)
                return new JObject { ["error"] = "No 3D view family type found" };

            using (var tx = new Transaction(doc, "Explode 3D View"))
            {
                tx.Start();
                var view3d = View3D.CreateIsometric(doc, viewFamilyType.Id);
                view3d.Name = $"Exploded_{DateTime.Now:HHmmss}";

                // Set a wide section box
                double minZ = levels.First().Elevation - 5;
                double maxZ = levels.Last().Elevation + spacing * levels.Count + 20;

                var bb = new BoundingBoxXYZ();
                bb.Min = new XYZ(-500, -500, minZ);
                bb.Max = new XYZ(500, 500, maxZ);
                view3d.SetSectionBox(bb);

                tx.Commit();

                uiDoc.ActiveView = view3d;

                return new JObject
                {
                    ["message"] = $"ðŸ’¥ Created exploded 3D view '{view3d.Name}' with {levels.Count} levels, spacing={spacing}ft",
                    ["viewId"] = view3d.Id.Value,
                    ["viewName"] = view3d.Name,
                    ["levels"] = levels.Count,
                    ["hint"] = "For actual displacement, use execute_code to move elements per-level by offset."
                };
            }
        }

        private static JToken RotateSectionBox(Document doc, UIDocument uiDoc, JObject parameters)
        {
            var elementId = parameters["elementId"]?.Value<int>();
            var angle = parameters["angle"]?.Value<double>() ?? 0;

            var view = uiDoc.ActiveView as View3D;
            if (view == null)
                return new JObject { ["error"] = "Active view must be a 3D view" };
            if (!view.IsSectionBoxActive)
                return new JObject { ["error"] = "Section box is not active in current view" };

            using (var tx = new Transaction(doc, "Rotate Section Box"))
            {
                tx.Start();
                var box = view.GetSectionBox();
                var transform = box.Transform;

                if (elementId.HasValue)
                {
                    // Orient to element
                    var elem = doc.GetElement(new ElementId(elementId.Value));
                    if (elem != null)
                    {
                        var loc = elem.Location as LocationCurve;
                        if (loc != null)
                        {
                            var dir = (loc.Curve.GetEndPoint(1) - loc.Curve.GetEndPoint(0)).Normalize();
                            angle = Math.Atan2(dir.Y, dir.X) * 180 / Math.PI;
                        }
                    }
                }

                double rad = angle * Math.PI / 180;
                var center = (box.Min + box.Max) / 2;
                var newTransform = Transform.CreateRotationAtPoint(XYZ.BasisZ, rad, transform.OfPoint(center));
                box.Transform = transform.Multiply(newTransform);
                view.SetSectionBox(box);
                tx.Commit();

                return new JObject
                {
                    ["message"] = $"ðŸ”„ Rotated section box by {angle}Â° in '{view.Name}'",
                    ["angle"] = angle
                };
            }
        }

        private static JToken SuperAlign(Document doc, JObject parameters)
        {
            var elementIdsArr = parameters["elementIds"] as JArray;
            var mode = parameters["mode"]?.ToString() ?? "align";
            var direction = parameters["direction"]?.ToString() ?? "horizontal";
            var spacing = parameters["spacing"]?.Value<double>() ?? 0;

            if (elementIdsArr == null || elementIdsArr.Count < 2)
                return new JObject { ["error"] = "Need at least 2 element IDs" };

            var elements = elementIdsArr
                .Select(id => doc.GetElement(new ElementId(id.Value<int>())))
                .Where(e => e != null)
                .ToList();

            var positions = new List<(Element elem, XYZ center)>();
            foreach (var e in elements)
            {
                var bb = e.get_BoundingBox(null);
                if (bb != null) positions.Add((e, (bb.Min + bb.Max) / 2));
            }

            using (var tx = new Transaction(doc, "Super Align"))
            {
                tx.Start();
                int moved = 0;

                if (mode == "align")
                {
                    // Align all to the first element's position
                    var refPos = positions.First().center;
                    foreach (var (elem, center) in positions.Skip(1))
                    {
                        XYZ delta;
                        if (direction == "horizontal")
                            delta = new XYZ(0, refPos.Y - center.Y, 0);
                        else
                            delta = new XYZ(refPos.X - center.X, 0, 0);

                        if (delta.GetLength() > 0.001)
                        {
                            ElementTransformUtils.MoveElement(doc, elem.Id, delta);
                            moved++;
                        }
                    }
                }
                else if (mode == "distribute")
                {
                    // Distribute evenly between first and last
                    var sorted = direction == "horizontal"
                        ? positions.OrderBy(p => p.center.X).ToList()
                        : positions.OrderBy(p => p.center.Y).ToList();

                    if (sorted.Count > 2)
                    {
                        double start = direction == "horizontal" ? sorted.First().center.X : sorted.First().center.Y;
                        double end = direction == "horizontal" ? sorted.Last().center.X : sorted.Last().center.Y;
                        double step = (end - start) / (sorted.Count - 1);

                        for (int i = 1; i < sorted.Count - 1; i++)
                        {
                            double target = start + step * i;
                            var current = direction == "horizontal" ? sorted[i].center.X : sorted[i].center.Y;
                            XYZ delta = direction == "horizontal"
                                ? new XYZ(target - current, 0, 0)
                                : new XYZ(0, target - current, 0);

                            if (delta.GetLength() > 0.001)
                            {
                                ElementTransformUtils.MoveElement(doc, sorted[i].elem.Id, delta);
                                moved++;
                            }
                        }
                    }
                }
                else if (mode == "grid" && spacing > 0)
                {
                    // Arrange in a grid
                    int cols = (int)Math.Ceiling(Math.Sqrt(positions.Count));
                    var start = positions.First().center;
                    for (int i = 0; i < positions.Count; i++)
                    {
                        int row = i / cols;
                        int col = i % cols;
                        var target = new XYZ(start.X + col * spacing, start.Y - row * spacing, positions[i].center.Z);
                        var delta = target - positions[i].center;
                        if (delta.GetLength() > 0.001)
                        {
                            ElementTransformUtils.MoveElement(doc, positions[i].elem.Id, delta);
                            moved++;
                        }
                    }
                }

                tx.Commit();
                return new JObject
                {
                    ["message"] = $"âœ… Super Align: moved {moved} elements (mode={mode}, direction={direction})",
                    ["moved"] = moved
                };
            }
        }

        private static JToken JoinElementsInView(Document doc, UIDocument uiDoc, JObject parameters)
        {
            var cat1Str = parameters["category1"]?.ToString() ?? "Walls";
            var cat2Str = parameters["category2"]?.ToString() ?? "Floors";
            var viewIdParam = parameters["viewId"]?.Value<int>();

            var view = viewIdParam.HasValue
                ? doc.GetElement(new ElementId(viewIdParam.Value)) as View
                : uiDoc.ActiveView;

            var cat1 = GetBuiltInCategory(cat1Str);
            var cat2 = GetBuiltInCategory(cat2Str);

            var elements1 = new FilteredElementCollector(doc, view.Id)
                .OfCategory(cat1)
                .WhereElementIsNotElementType()
                .ToList();

            var elements2 = new FilteredElementCollector(doc, view.Id)
                .OfCategory(cat2)
                .WhereElementIsNotElementType()
                .ToList();

            int joined = 0;
            using (var tx = new Transaction(doc, "Join Elements in View"))
            {
                tx.Start();
                foreach (var e1 in elements1)
                {
                    var bb1 = e1.get_BoundingBox(view);
                    if (bb1 == null) continue;

                    foreach (var e2 in elements2)
                    {
                        var bb2 = e2.get_BoundingBox(view);
                        if (bb2 == null) continue;

                        // Check proximity
                        if (bb1.Min.X <= bb2.Max.X + 0.5 && bb1.Max.X >= bb2.Min.X - 0.5 &&
                            bb1.Min.Y <= bb2.Max.Y + 0.5 && bb1.Max.Y >= bb2.Min.Y - 0.5 &&
                            bb1.Min.Z <= bb2.Max.Z + 0.5 && bb1.Max.Z >= bb2.Min.Z - 0.5)
                        {
                            try
                            {
                                if (!JoinGeometryUtils.AreElementsJoined(doc, e1, e2))
                                {
                                    JoinGeometryUtils.JoinGeometry(doc, e1, e2);
                                    joined++;
                                }
                            }
                            catch (Exception ex) { Logger.Log($"Join geometry failed: {ex.Message}"); }
                        }
                    }
                }
                tx.Commit();
            }

            return new JObject
            {
                ["message"] = $"ðŸ”— Joined {joined} element pairs ({cat1Str} â†” {cat2Str}) in '{view.Name}'",
                ["joined"] = joined,
                ["category1Count"] = elements1.Count,
                ["category2Count"] = elements2.Count
            };
        }

        private static JToken CopyToProject(Document doc, UIApplication uiApp, JObject parameters)
        {
            var targetName = parameters["targetProject"]?.ToString();
            var category = parameters["category"]?.ToString();

            if (string.IsNullOrEmpty(targetName))
                return new JObject { ["error"] = "targetProject name is required" };

            // Find target document among open documents
            Document targetDoc = null;
            foreach (Document d in uiApp.Application.Documents)
            {
                if (d.Title.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0 && d.PathName != doc.PathName)
                {
                    targetDoc = d;
                    break;
                }
            }

            if (targetDoc == null)
            {
                var openDocs = new JArray();
                foreach (Document d in uiApp.Application.Documents)
                    if (d.PathName != doc.PathName)
                        openDocs.Add(d.Title);

                return new JObject
                {
                    ["error"] = $"Project '{targetName}' not found among open documents",
                    ["openProjects"] = openDocs
                };
            }

            var bic = GetBuiltInCategory(category ?? "Walls");
            var elements = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToList();

            // Copy element IDs
            var ids = elements.Select(e => e.Id).ToList();

            using (var tx = new Transaction(targetDoc, "Copy from Other Project"))
            {
                tx.Start();
                try
                {
                    ElementTransformUtils.CopyElements(doc, ids, targetDoc, Transform.Identity, new CopyPasteOptions());
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    return new JObject { ["error"] = $"Copy failed: {ex.Message}" };
                }
            }

            return new JObject
            {
                ["message"] = $"ðŸ“‹ Copied {ids.Count} {category} elements from '{doc.Title}' â†’ '{targetDoc.Title}'",
                ["copied"] = ids.Count
            };
        }

        private static JToken MeasureElements(Document doc, JObject parameters)
        {
            var elementIdsStr = parameters["elementIds"]?.ToString();
            var measureType = parameters["type"]?.ToString() ?? "length";

            var elements = new List<Element>();
            if (!string.IsNullOrEmpty(elementIdsStr))
            {
                var ids = elementIdsStr.Split(',').Select(s => int.Parse(s.Trim()));
                elements = ids.Select(id => doc.GetElement(new ElementId(id))).Where(e => e != null).ToList();
            }

            if (elements.Count == 0)
                return new JObject { ["error"] = "No valid element IDs provided" };

            var results = new JArray();
            double totalLength = 0;
            double totalArea = 0;

            foreach (var elem in elements)
            {
                var item = new JObject { ["id"] = elem.Id.Value, ["name"] = elem.Name };

                // Length from LocationCurve
                var locCurve = elem.Location as LocationCurve;
                if (locCurve != null)
                {
                    double len = locCurve.Curve.Length;
                    item["length_ft"] = Math.Round(len, 4);
                    item["length_m"] = Math.Round(len * 0.3048, 4);
                    totalLength += len;
                }

                // Area from parameter
                var areaParam = elem.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (areaParam != null)
                {
                    double area = areaParam.AsDouble();
                    item["area_sqft"] = Math.Round(area, 4);
                    item["area_sqm"] = Math.Round(area * 0.092903, 4);
                    totalArea += area;
                }

                // Volume
                var volParam = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                if (volParam != null)
                {
                    double vol = volParam.AsDouble();
                    item["volume_cuft"] = Math.Round(vol, 4);
                    item["volume_cum"] = Math.Round(vol * 0.0283168, 4);
                }

                // Bounding box dimensions
                var bb = elem.get_BoundingBox(null);
                if (bb != null)
                {
                    var size = bb.Max - bb.Min;
                    item["width_ft"] = Math.Round(size.X, 4);
                    item["depth_ft"] = Math.Round(size.Y, 4);
                    item["height_ft"] = Math.Round(size.Z, 4);
                }

                results.Add(item);
            }

            // Distance between elements
            JObject distanceInfo = null;
            if (elements.Count == 2 && measureType == "distance")
            {
                var bb1 = elements[0].get_BoundingBox(null);
                var bb2 = elements[1].get_BoundingBox(null);
                if (bb1 != null && bb2 != null)
                {
                    var c1 = (bb1.Min + bb1.Max) / 2;
                    var c2 = (bb2.Min + bb2.Max) / 2;
                    double dist = c1.DistanceTo(c2);
                    distanceInfo = new JObject
                    {
                        ["distance_ft"] = Math.Round(dist, 4),
                        ["distance_m"] = Math.Round(dist * 0.3048, 4),
                        ["dx_ft"] = Math.Round(Math.Abs(c2.X - c1.X), 4),
                        ["dy_ft"] = Math.Round(Math.Abs(c2.Y - c1.Y), 4),
                        ["dz_ft"] = Math.Round(Math.Abs(c2.Z - c1.Z), 4)
                    };
                }
            }

            var response = new JObject
            {
                ["message"] = $"ðŸ“ Measured {elements.Count} elements",
                ["totalLength_ft"] = Math.Round(totalLength, 4),
                ["totalLength_m"] = Math.Round(totalLength * 0.3048, 4),
                ["totalArea_sqft"] = Math.Round(totalArea, 4),
                ["totalArea_sqm"] = Math.Round(totalArea * 0.092903, 4),
                ["elements"] = results
            };

            if (distanceInfo != null)
                response["distance"] = distanceInfo;

            return response;
        }
    }
}
