using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Routes MCP commands to their corresponding Revit API implementations.
    /// This is the main command dispatcher for all 55 tools.
    /// </summary>
    public static class CommandExecutor
    {
        public static JToken Execute(UIApplication uiApp, string command, JObject parameters)
        {
            var doc = uiApp.ActiveUIDocument?.Document;
            var uidoc = uiApp.ActiveUIDocument;

            if (doc == null)
                throw new InvalidOperationException("No active document. Please open a Revit project first.");

            switch (command)
            {
                // ===== READING COMMANDS =====
                case "get_current_view_info":
                    return GetCurrentViewInfo(uidoc!);
                case "get_current_view_elements":
                    return GetCurrentViewElements(uidoc!, parameters);
                case "get_selected_elements":
                    return GetSelectedElements(uidoc!);
                case "get_elements":
                    return GetElements(doc, parameters);
                case "get_parameters":
                    return GetParameters(doc, parameters);
                case "get_project_info":
                    return GetProjectInfo(doc);
                case "get_views":
                    return GetViews(doc, parameters);
                case "get_sheets":
                    return GetSheets(doc);
                case "get_levels":
                    return GetLevels(doc);
                case "get_grids":
                    return GetGrids(doc);
                case "get_rooms":
                    return GetRooms(doc);
                case "get_available_family_types":
                    return GetFamilyTypes(doc, parameters);
                case "get_schedules":
                    return GetSchedules(doc);
                case "get_linked_models":
                    return GetLinkedModels(doc);
                case "get_warnings":
                    return GetWarnings(doc);

                // ===== CREATING COMMANDS =====
                case "create_wall":
                    return CreateWall(doc, parameters);
                case "create_level":
                    return CreateLevel(doc, parameters);
                case "create_grid":
                    return CreateGrid(doc, parameters);
                case "create_room":
                    return CreateRoom(doc, parameters);
                case "create_sheet":
                    return CreateSheet(doc, parameters);
                case "create_point_based_element":
                case "create_line_based_element":
                case "create_floor":
                case "create_ceiling":
                case "create_roof":
                case "create_view":
                case "create_schedule":
                case "create_tag":
                case "create_dimension":
                case "create_text_note":
                    return ExecuteGenericCommand(doc, command, parameters);

                // ===== EDITING COMMANDS =====
                case "modify_element":
                    return ModifyElement(doc, parameters);
                case "move_element":
                    return MoveElement(doc, parameters);
                case "delete_elements":
                    return DeleteElements(doc, parameters);
                case "copy_element":
                case "rotate_element":
                case "mirror_element":
                case "align_elements":
                case "group_elements":
                case "change_type":
                case "set_workset":
                case "color_elements":
                case "batch_modify_parameters":
                    return ExecuteGenericCommand(doc, command, parameters);

                // ===== DOCUMENTATION COMMANDS =====
                case "place_view_on_sheet":
                case "create_viewport":
                case "create_legend":
                case "add_revision":
                case "print_sheets":
                case "tag_all_in_view":
                    return ExecuteGenericCommand(doc, command, parameters);

                // ===== QA/QC COMMANDS =====
                case "check_warnings":
                    return GetWarnings(doc);
                case "audit_model":
                    return AuditModel(doc);
                case "check_room_compliance":
                case "check_naming_conventions":
                case "find_duplicates":
                case "purge_unused":
                case "check_links_status":
                case "validate_parameters":
                    return ExecuteGenericCommand(doc, command, parameters);

                // ===== ADVANCED COMMANDS =====
                case "send_code_to_revit":
                    return new JObject { ["message"] = "Code execution is handled by the command set module" };
                case "select_elements":
                    return SelectElements(uidoc!, parameters);
                case "get_model_statistics":
                    return GetModelStatistics(doc);
                case "ai_element_filter":
                case "reset_view":
                    return ExecuteGenericCommand(doc, command, parameters);

                // ===== TOOL WINDOW COMMANDS (Offline) =====
                case "export_manager":
                    return ExportMultiFormat(doc, parameters);
                case "export_to_pdf":
                    return ExportToPdf(doc, parameters);
                case "export_to_images":
                    return ExportToImages(doc, parameters);
                case "export_to_ifc":
                    return ExportToIfc(doc, parameters);
                case "export_to_dgn":
                    return ExportToDgn(doc, parameters);
                case "export_dwg":
                case "export_to_dwg":
                    return ExportToDwg(doc, parameters);
                case "export_to_dwf":
                    return ExportToDwf(doc, parameters);
                case "export_to_nwc":
                    return ExportToNwc(doc, parameters);
                case "export_schedule_data":
                case "export_schedule":
                    return ExportScheduleData(doc, parameters);
                case "export_parameters_to_csv":
                    return ExportParametersToCsv(doc, parameters);
                case "import_parameters_from_csv":
                    return ImportParametersFromCsv(doc, parameters);

                // Family & Parameter tools
                case "manage_families":
                    return ManageFamilies(doc, parameters);
                case "get_family_info":
                    return GetFamilyTypes(doc, parameters);
                case "create_project_parameter":
                    return CreateProjectParameter(doc, parameters);
                case "batch_set_parameter":
                    return BatchSetParameter(doc, parameters);
                case "delete_unused_families":
                    return DeleteUnusedFamilies(doc, parameters);

                // View creation tools
                case "create_elevation_views":
                    return CreateElevationViews(doc, parameters);
                case "create_section_views":
                    return CreateSectionViews(doc, parameters);
                case "create_callout_views":
                    return CreateCalloutViews(doc, parameters);

                // Sheet & View management tools
                case "align_viewports":
                    return AlignViewports(doc, parameters);
                case "batch_create_sheets":
                    return BatchCreateSheets(doc, parameters);
                case "duplicate_view":
                    return DuplicateView(doc, parameters);
                case "apply_view_template":
                    return ApplyViewTemplate(doc, parameters);

                default:
                    throw new InvalidOperationException($"Unknown command: {command}");
            }
        }

        // ===== READING IMPLEMENTATIONS =====

        private static JToken GetCurrentViewInfo(UIDocument uidoc)
        {
            var view = uidoc.ActiveView;
            return new JObject
            {
                ["viewId"] = view.Id.IntegerValue,
                ["viewName"] = view.Name,
                ["viewType"] = view.ViewType.ToString(),
                ["scale"] = view.Scale,
                ["levelName"] = view.GenLevel?.Name ?? "N/A",
                ["isTemplate"] = view.IsTemplate
            };
        }

        private static JToken GetCurrentViewElements(UIDocument uidoc, JObject parameters)
        {
            var collector = new FilteredElementCollector(uidoc.Document, uidoc.ActiveView.Id);
            var category = parameters["category"]?.ToString();

            if (!string.IsNullOrEmpty(category))
            {
                var builtInCat = GetBuiltInCategory(category);
                if (builtInCat != BuiltInCategory.INVALID)
                    collector = collector.OfCategory(builtInCat);
            }

            var elements = collector.WhereElementIsNotElementType().ToElements();
            var result = new JArray();

            foreach (var elem in elements)
            {
                result.Add(new JObject
                {
                    ["id"] = elem.Id.IntegerValue,
                    ["name"] = elem.Name,
                    ["category"] = elem.Category?.Name ?? "Unknown"
                });
            }

            return new JObject { ["elements"] = result, ["count"] = result.Count };
        }

        private static JToken GetSelectedElements(UIDocument uidoc)
        {
            var selected = uidoc.Selection.GetElementIds();
            var result = new JArray();

            foreach (var id in selected)
            {
                var elem = uidoc.Document.GetElement(id);
                if (elem != null)
                {
                    var elemObj = new JObject
                    {
                        ["id"] = elem.Id.IntegerValue,
                        ["name"] = elem.Name,
                        ["category"] = elem.Category?.Name ?? "Unknown"
                    };

                    // Include key parameters
                    var paramsObj = new JObject();
                    foreach (Parameter p in elem.Parameters)
                    {
                        if (p.HasValue)
                        {
                            paramsObj[p.Definition.Name] = p.AsValueString() ?? p.AsString() ?? "";
                        }
                    }
                    elemObj["parameters"] = paramsObj;
                    result.Add(elemObj);
                }
            }

            return new JObject { ["elements"] = result, ["count"] = result.Count };
        }

        private static JToken GetElements(Document doc, JObject parameters)
        {
            var category = parameters["category"]?.ToString() ?? "";
            var includeParams = parameters["includeParameters"]?.Value<bool>() ?? false;

            var collector = new FilteredElementCollector(doc);
            var builtInCat = GetBuiltInCategory(category);

            if (builtInCat != BuiltInCategory.INVALID)
                collector = collector.OfCategory(builtInCat);

            var elements = collector.WhereElementIsNotElementType().ToElements();
            var result = new JArray();

            foreach (var elem in elements.Take(500)) // Limit to 500 to avoid memory issues
            {
                var obj = new JObject
                {
                    ["id"] = elem.Id.IntegerValue,
                    ["name"] = elem.Name,
                    ["category"] = elem.Category?.Name ?? "Unknown"
                };

                if (includeParams)
                {
                    var paramsObj = new JObject();
                    foreach (Parameter p in elem.Parameters)
                    {
                        if (p.HasValue)
                            paramsObj[p.Definition.Name] = p.AsValueString() ?? p.AsString() ?? "";
                    }
                    obj["parameters"] = paramsObj;
                }

                result.Add(obj);
            }

            return new JObject { ["elements"] = result, ["count"] = result.Count };
        }

        private static JToken GetParameters(Document doc, JObject parameters)
        {
            var elementId = parameters["elementId"]?.Value<int>() ?? 0;
            var elem = doc.GetElement(new ElementId(elementId));

            if (elem == null)
                throw new InvalidOperationException($"Element {elementId} not found");

            var result = new JObject();
            var instanceParams = new JObject();
            var typeParams = new JObject();

            foreach (Parameter p in elem.Parameters)
            {
                if (p.HasValue)
                    instanceParams[p.Definition.Name] = p.AsValueString() ?? p.AsString() ?? "";
            }

            var typeElem = doc.GetElement(elem.GetTypeId());
            if (typeElem != null)
            {
                foreach (Parameter p in typeElem.Parameters)
                {
                    if (p.HasValue)
                        typeParams[p.Definition.Name] = p.AsValueString() ?? p.AsString() ?? "";
                }
            }

            result["elementId"] = elementId;
            result["name"] = elem.Name;
            result["category"] = elem.Category?.Name ?? "Unknown";
            result["instanceParameters"] = instanceParams;
            result["typeParameters"] = typeParams;

            return result;
        }

        private static JToken GetProjectInfo(Document doc)
        {
            var info = doc.ProjectInformation;
            return new JObject
            {
                ["projectName"] = info.Name,
                ["projectNumber"] = info.Number,
                ["clientName"] = info.ClientName,
                ["buildingName"] = info.BuildingName,
                ["address"] = info.Address,
                ["status"] = info.Status,
                ["issueDate"] = info.IssueDate,
                ["filePath"] = doc.PathName
            };
        }

        private static JToken GetViews(Document doc, JObject parameters)
        {
            var viewTypeFilter = parameters["viewType"]?.ToString() ?? "";
            var collector = new FilteredElementCollector(doc).OfClass(typeof(View));
            var result = new JArray();

            foreach (View view in collector)
            {
                if (view.IsTemplate) continue;
                if (!string.IsNullOrEmpty(viewTypeFilter) &&
                    !view.ViewType.ToString().Equals(viewTypeFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(new JObject
                {
                    ["id"] = view.Id.IntegerValue,
                    ["name"] = view.Name,
                    ["viewType"] = view.ViewType.ToString(),
                    ["scale"] = view.Scale
                });
            }

            return new JObject { ["views"] = result, ["count"] = result.Count };
        }

        private static JToken GetSheets(Document doc)
        {
            var collector = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet));
            var result = new JArray();

            foreach (ViewSheet sheet in collector)
            {
                var viewIds = sheet.GetAllPlacedViews();
                var views = new JArray();
                foreach (var vid in viewIds)
                {
                    var v = doc.GetElement(vid) as View;
                    if (v != null) views.Add(v.Name);
                }

                result.Add(new JObject
                {
                    ["id"] = sheet.Id.IntegerValue,
                    ["number"] = sheet.SheetNumber,
                    ["name"] = sheet.Name,
                    ["placedViews"] = views
                });
            }

            return new JObject { ["sheets"] = result, ["count"] = result.Count };
        }

        private static JToken GetLevels(Document doc)
        {
            var collector = new FilteredElementCollector(doc).OfClass(typeof(Level));
            var result = new JArray();

            foreach (Level level in collector)
            {
                result.Add(new JObject
                {
                    ["id"] = level.Id.IntegerValue,
                    ["name"] = level.Name,
                    ["elevation"] = Math.Round(level.Elevation, 4)
                });
            }

            return new JObject { ["levels"] = result, ["count"] = result.Count };
        }

        private static JToken GetGrids(Document doc)
        {
            var collector = new FilteredElementCollector(doc).OfClass(typeof(Grid));
            var result = new JArray();

            foreach (Grid grid in collector)
            {
                var curve = grid.Curve;
                result.Add(new JObject
                {
                    ["id"] = grid.Id.IntegerValue,
                    ["name"] = grid.Name,
                    ["isCurved"] = !(curve is Line)
                });
            }

            return new JObject { ["grids"] = result, ["count"] = result.Count };
        }

        private static JToken GetRooms(Document doc)
        {
            var collector = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms);
            var result = new JArray();

            foreach (var elem in collector)
            {
                if (elem is Room room)
                {
                    result.Add(new JObject
                    {
                        ["id"] = room.Id.IntegerValue,
                        ["name"] = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "",
                        ["number"] = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "",
                        ["area"] = Math.Round(room.Area, 2),
                        ["level"] = room.Level?.Name ?? "N/A"
                    });
                }
            }

            return new JObject { ["rooms"] = result, ["count"] = result.Count };
        }

        private static JToken GetFamilyTypes(Document doc, JObject parameters)
        {
            var category = parameters["category"]?.ToString() ?? "";
            var collector = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol));

            if (!string.IsNullOrEmpty(category))
            {
                var builtInCat = GetBuiltInCategory(category);
                if (builtInCat != BuiltInCategory.INVALID)
                    collector = collector.OfCategory(builtInCat);
            }

            var result = new JArray();
            foreach (FamilySymbol symbol in collector)
            {
                result.Add(new JObject
                {
                    ["id"] = symbol.Id.IntegerValue,
                    ["familyName"] = symbol.FamilyName,
                    ["typeName"] = symbol.Name,
                    ["category"] = symbol.Category?.Name ?? ""
                });
            }

            return new JObject { ["familyTypes"] = result, ["count"] = result.Count };
        }

        private static JToken GetSchedules(Document doc)
        {
            var collector = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule));
            var result = new JArray();

            foreach (ViewSchedule schedule in collector)
            {
                if (schedule.IsTitleblockRevisionSchedule) continue;

                result.Add(new JObject
                {
                    ["id"] = schedule.Id.IntegerValue,
                    ["name"] = schedule.Name
                });
            }

            return new JObject { ["schedules"] = result, ["count"] = result.Count };
        }

        private static JToken GetLinkedModels(Document doc)
        {
            var collector = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType));
            var result = new JArray();

            foreach (RevitLinkType linkType in collector)
            {
                result.Add(new JObject
                {
                    ["id"] = linkType.Id.IntegerValue,
                    ["name"] = linkType.Name
                });
            }

            return new JObject { ["linkedModels"] = result, ["count"] = result.Count };
        }

        private static JToken GetWarnings(Document doc)
        {
            var warnings = doc.GetWarnings();
            var result = new JArray();

            foreach (var warning in warnings)
            {
                var elementIds = warning.GetFailingElements().Select(id => id.IntegerValue).ToList();
                result.Add(new JObject
                {
                    ["description"] = warning.GetDescriptionText(),
                    ["severity"] = warning.GetSeverity().ToString(),
                    ["elementIds"] = new JArray(elementIds)
                });
            }

            return new JObject { ["warnings"] = result, ["count"] = result.Count };
        }

        // ===== CREATING IMPLEMENTATIONS =====

        private static JToken CreateWall(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Wall"))
            {
                tx.Start();
                try
                {
                    var startX = parameters["startX"]?.Value<double>() ?? 0;
                    var startY = parameters["startY"]?.Value<double>() ?? 0;
                    var endX = parameters["endX"]?.Value<double>() ?? 0;
                    var endY = parameters["endY"]?.Value<double>() ?? 0;
                    var levelName = parameters["levelName"]?.ToString() ?? "";
                    var height = parameters["height"]?.Value<double>() ?? 10;

                    var level = new FilteredElementCollector(doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));

                    if (level == null)
                        throw new InvalidOperationException($"Level '{levelName}' not found");

                    var start = new XYZ(startX, startY, 0);
                    var end = new XYZ(endX, endY, 0);
                    if (start.DistanceTo(end) < 0.001)
                        throw new InvalidOperationException("Wall start and end points are too close (must be > 0.001 ft apart)");

                    var line = Line.CreateBound(start, end);
                    var wall = Wall.Create(doc, line, level.Id, false);
                    wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.Set(height);

                    tx.Commit();
                    return new JObject
                    {
                        ["elementId"] = wall.Id.IntegerValue,
                        ["message"] = $"Wall created successfully on level '{levelName}'"
                    };
                }
                catch
                {
                    if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                    throw;
                }
            }
        }

        private static JToken CreateLevel(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Level"))
            {
                tx.Start();
                try
                {
                    var name = parameters["name"]?.ToString() ?? "New Level";
                    var elevation = parameters["elevation"]?.Value<double>() ?? 0;

                    // Check for duplicate level name
                    var existing = new FilteredElementCollector(doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                        throw new InvalidOperationException($"Level '{name}' already exists (id: {existing.Id.IntegerValue})");

                    var level = Level.Create(doc, elevation);
                    level.Name = name;

                    tx.Commit();
                    return new JObject
                    {
                        ["elementId"] = level.Id.IntegerValue,
                        ["message"] = $"Level '{name}' created at elevation {elevation}"
                    };
                }
                catch
                {
                    if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                    throw;
                }
            }
        }

        private static JToken CreateGrid(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Grid"))
            {
                tx.Start();
                try
                {
                    var startX = parameters["startX"]?.Value<double>() ?? 0;
                    var startY = parameters["startY"]?.Value<double>() ?? 0;
                    var endX = parameters["endX"]?.Value<double>() ?? 0;
                    var endY = parameters["endY"]?.Value<double>() ?? 0;
                    var name = parameters["name"]?.ToString() ?? "";

                    var start = new XYZ(startX, startY, 0);
                    var end = new XYZ(endX, endY, 0);
                    if (start.DistanceTo(end) < 0.001)
                        throw new InvalidOperationException("Grid start and end points are too close");

                    var line = Line.CreateBound(start, end);
                    var grid = Grid.Create(doc, line);

                    if (!string.IsNullOrEmpty(name))
                        grid.Name = name;

                    tx.Commit();
                    return new JObject
                    {
                        ["elementId"] = grid.Id.IntegerValue,
                        ["message"] = $"Grid '{grid.Name}' created"
                    };
                }
                catch
                {
                    if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                    throw;
                }
            }
        }

        private static JToken CreateRoom(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Room"))
            {
                tx.Start();
                try
                {
                    var x = parameters["x"]?.Value<double>() ?? 0;
                    var y = parameters["y"]?.Value<double>() ?? 0;
                    var levelName = parameters["levelName"]?.ToString() ?? "";

                    var level = new FilteredElementCollector(doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));

                    if (level == null)
                        throw new InvalidOperationException($"Level '{levelName}' not found");

                    var room = doc.Create.NewRoom(level, new UV(x, y));

                    var roomName = parameters["roomName"]?.ToString();
                    if (!string.IsNullOrEmpty(roomName))
                        room.get_Parameter(BuiltInParameter.ROOM_NAME)?.Set(roomName);

                    var roomNumber = parameters["roomNumber"]?.ToString();
                    if (!string.IsNullOrEmpty(roomNumber))
                        room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.Set(roomNumber);

                    tx.Commit();
                    return new JObject
                    {
                        ["elementId"] = room.Id.IntegerValue,
                        ["message"] = $"Room created on level '{levelName}'"
                    };
                }
                catch
                {
                    if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                    throw;
                }
            }
        }

        private static JToken CreateSheet(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Sheet"))
            {
                tx.Start();
                try
                {
                    var titleBlockId = ElementId.InvalidElementId;
                    var titleBlockName = parameters["titleBlockName"]?.ToString();

                    if (!string.IsNullOrEmpty(titleBlockName))
                    {
                        var tb = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilySymbol))
                            .OfCategory(BuiltInCategory.OST_TitleBlocks)
                            .Cast<FamilySymbol>()
                            .FirstOrDefault(s => s.Name.IndexOf(titleBlockName, StringComparison.OrdinalIgnoreCase) >= 0);

                        if (tb != null) titleBlockId = tb.Id;
                    }
                    else
                    {
                        var tb = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilySymbol))
                            .OfCategory(BuiltInCategory.OST_TitleBlocks)
                            .FirstOrDefault();
                        if (tb != null) titleBlockId = tb.Id;
                    }

                    var sheet = ViewSheet.Create(doc, titleBlockId);
                    sheet.SheetNumber = parameters["sheetNumber"]?.ToString() ?? "NEW";
                    sheet.Name = parameters["sheetName"]?.ToString() ?? "New Sheet";

                    tx.Commit();
                    return new JObject
                    {
                        ["elementId"] = sheet.Id.IntegerValue,
                        ["message"] = $"Sheet '{sheet.SheetNumber} - {sheet.Name}' created"
                    };
                }
                catch
                {
                    if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                    throw;
                }
            }
        }

        // ===== EDITING IMPLEMENTATIONS =====

        private static JToken ModifyElement(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Modify Element"))
            {
                tx.Start();
                try
                {
                    var elementId = parameters["elementId"]?.Value<int>() ?? 0;
                    var elem = doc.GetElement(new ElementId(elementId));
                    if (elem == null)
                        throw new InvalidOperationException($"Element {elementId} not found");

                    var modifications = parameters["modifications"] as JArray;
                    int modCount = 0;
                    if (modifications != null)
                    {
                        foreach (JObject mod in modifications)
                        {
                            var paramName = mod["parameterName"]?.ToString() ?? "";
                            var value = mod["value"];

                            foreach (Parameter p in elem.Parameters)
                            {
                                if (p.Definition.Name == paramName && !p.IsReadOnly)
                                {
                                    if (value?.Type == JTokenType.String)
                                        p.Set(value.ToString());
                                    else if (value?.Type == JTokenType.Integer || value?.Type == JTokenType.Float)
                                        p.Set(value.Value<double>());
                                    modCount++;
                                    break;
                                }
                            }
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"Element {elementId} modified ({modCount} parameters updated)" };
                }
                catch
                {
                    if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                    throw;
                }
            }
        }

        private static JToken MoveElement(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Move Element"))
            {
                tx.Start();
                try
                {
                    var elementId = parameters["elementId"]?.Value<int>() ?? 0;
                    var dx = parameters["deltaX"]?.Value<double>() ?? 0;
                    var dy = parameters["deltaY"]?.Value<double>() ?? 0;
                    var dz = parameters["deltaZ"]?.Value<double>() ?? 0;

                    var elem = doc.GetElement(new ElementId(elementId));
                    if (elem == null)
                        throw new InvalidOperationException($"Element {elementId} not found");

                    var translation = new XYZ(dx, dy, dz);
                    if (translation.GetLength() < 1e-9)
                        throw new InvalidOperationException("Move delta is zero — nothing to move");

                    ElementTransformUtils.MoveElement(doc, elem.Id, translation);
                    tx.Commit();
                    return new JObject { ["message"] = $"Element {elementId} moved by ({dx}, {dy}, {dz})" };
                }
                catch
                {
                    if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                    throw;
                }
            }
        }

        private static JToken DeleteElements(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Delete Elements"))
            {
                tx.Start();
                try
                {
                    var ids = (parameters["elementIds"] as JArray)?
                        .Select(id => new ElementId(id.Value<int>()))
                        .ToList() ?? new List<ElementId>();

                    if (ids.Count == 0)
                        throw new InvalidOperationException("No element IDs provided for deletion");

                    // Validate all elements exist before deleting
                    foreach (var id in ids)
                    {
                        if (doc.GetElement(id) == null)
                            throw new InvalidOperationException($"Element {id.IntegerValue} not found");
                    }

                    // doc.Delete accepts ICollection<ElementId>
                    doc.Delete(ids as ICollection<ElementId>);
                    tx.Commit();
                    return new JObject { ["message"] = $"{ids.Count} elements deleted successfully" };
                }
                catch
                {
                    if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                    throw;
                }
            }
        }

        private static JToken SelectElements(UIDocument uidoc, JObject parameters)
        {
            var ids = (parameters["elementIds"] as JArray)?
                .Select(id => new ElementId(id.Value<int>()))
                .ToList() ?? new List<ElementId>();

            uidoc.Selection.SetElementIds(ids);

            return new JObject { ["message"] = $"{ids.Count} elements selected" };
        }

        // ===== QA/QC IMPLEMENTATIONS =====

        private static JToken AuditModel(Document doc)
        {
            var result = new JObject();

            // Count elements by category
            var allElements = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements();
            var categoryCounts = allElements
                .Where(e => e.Category != null)
                .GroupBy(e => e.Category.Name)
                .ToDictionary(g => g.Key, g => g.Count());

            result["totalElements"] = allElements.Count;
            result["categoryCounts"] = JObject.FromObject(categoryCounts);
            result["warningCount"] = doc.GetWarnings().Count();
            result["levelCount"] = new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementCount();
            result["sheetCount"] = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).GetElementCount();
            result["viewCount"] = new FilteredElementCollector(doc).OfClass(typeof(View)).GetElementCount();

            return result;
        }

        private static JToken GetModelStatistics(Document doc)
        {
            return AuditModel(doc);
        }

        // ===== OFFLINE TOOL IMPLEMENTATIONS =====

        private static JToken ExportToPdf(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            // Collect sheets to export
            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .ToList();

            if (sheets.Count == 0)
                return new JObject { ["message"] = "No sheets found in the project." };

            // Use Revit PDF export (Revit 2022+)
            try
            {
                var pdfOptions = new PDFExportOptions();
                pdfOptions.FileName = doc.Title ?? "Export";
                pdfOptions.Combine = parameters?["combine"]?.ToString() == "true";

                var viewIds = sheets.Select(s => s.Id).ToList();
                doc.Export(outputFolder, viewIds, pdfOptions);

                return new JObject
                {
                    ["message"] = $"✅ Exported {sheets.Count} sheet(s) to PDF.\nOutput folder: {outputFolder}",
                    ["count"] = sheets.Count,
                    ["outputFolder"] = outputFolder
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"PDF export error: {ex.Message}\nMake sure a PDF printer is installed." };
            }
        }

        private static JToken ExportToImages(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted)
                .Take(50)
                .ToList();

            int exported = 0;
            foreach (var view in views)
            {
                try
                {
                    var imgOpts = new ImageExportOptions
                    {
                        FilePath = System.IO.Path.Combine(outputFolder, CleanFileName(view.Name)),
                        FitDirection = FitDirectionType.Horizontal,
                        HLRandWFViewsFileType = ImageFileType.PNG,
                        ShadowViewsFileType = ImageFileType.PNG,
                        PixelSize = 2048,
                        ZoomType = ZoomFitType.FitToPage,
                        ExportRange = ExportRange.SetOfViews,
                    };
                    imgOpts.SetViewsAndSheets(new List<ElementId> { view.Id });
                    doc.ExportImage(imgOpts);
                    exported++;
                }
                catch { /* skip views that can't export */ }
            }

            return new JObject
            {
                ["message"] = $"✅ Exported {exported} view(s) as images.\nOutput folder: {outputFolder}",
                ["count"] = exported,
                ["outputFolder"] = outputFolder
            };
        }

        private static JToken ExportToIfc(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            try
            {
                var ifcOpts = new IFCExportOptions();
                var fileName = System.IO.Path.GetFileNameWithoutExtension(doc.Title ?? "Export") + ".ifc";
                using (var t = new Transaction(doc, "Export IFC"))
                {
                    t.Start();
                    doc.Export(outputFolder, fileName, ifcOpts);
                    t.Commit();
                }
                return new JObject
                {
                    ["message"] = $"✅ Exported IFC to: {System.IO.Path.Combine(outputFolder, fileName)}",
                    ["outputFolder"] = outputFolder
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"IFC export error: {ex.Message}" };
            }
        }

        private static JToken ExportToDgn(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            try
            {
                var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.CanBePrinted)
                    .ToList();

                var dgnOpts = new DGNExportOptions();
                var viewIds = views.Select(v => v.Id).ToList();
                var fileName = System.IO.Path.GetFileNameWithoutExtension(doc.Title ?? "Export");
                doc.Export(outputFolder, fileName, viewIds, dgnOpts);

                return new JObject
                {
                    ["message"] = $"✅ Exported {viewIds.Count} view(s) to DGN.\nOutput folder: {outputFolder}",
                    ["count"] = viewIds.Count
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"DGN export error: {ex.Message}" };
            }
        }

        private static JToken ExportToDwg(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            try
            {
                var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.CanBePrinted)
                    .ToList();

                var dwgOpts = new DWGExportOptions();
                int exported = 0;
                foreach (var view in views.Take(100))
                {
                    try
                    {
                        var viewIds = new List<ElementId> { view.Id };
                        var cleanName = CleanFileName(view.Name);
                        doc.Export(outputFolder, cleanName, viewIds, dwgOpts);
                        exported++;
                    }
                    catch { }
                }

                return new JObject
                {
                    ["message"] = $"✅ Exported {exported} view(s) to DWG.\nOutput folder: {outputFolder}",
                    ["count"] = exported
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"DWG export error: {ex.Message}" };
            }
        }

        private static JToken ExportMultiFormat(Document doc, JObject parameters)
        {
            var formatsStr = parameters?["formats"]?.ToString() ?? "PDF";
            var formats = formatsStr.Split(',').Select(f => f.Trim().ToUpper()).Where(f => !string.IsNullOrEmpty(f)).ToList();

            var results = new List<string>();
            foreach (var fmt in formats)
            {
                try
                {
                    JToken result;
                    switch (fmt)
                    {
                        case "PDF":
                            result = ExportToPdf(doc, parameters);
                            break;
                        case "DWG":
                            result = ExportToDwg(doc, parameters);
                            break;
                        case "DGN":
                            result = ExportToDgn(doc, parameters);
                            break;
                        case "DWF":
                            result = ExportToDwf(doc, parameters);
                            break;
                        case "NWC":
                            result = ExportToNwc(doc, parameters);
                            break;
                        case "IFC":
                            result = ExportToIfc(doc, parameters);
                            break;
                        case "IMG":
                            result = ExportToImages(doc, parameters);
                            break;
                        default:
                            results.Add($"⚠️ Unknown format: {fmt}");
                            continue;
                    }
                    var msg = result?["message"]?.ToString() ?? $"Exported {fmt}";
                    results.Add(msg);
                }
                catch (Exception ex)
                {
                    results.Add($"❌ {fmt} error: {ex.Message}");
                }
            }

            return new JObject
            {
                ["message"] = string.Join("\n\n", results),
                ["formats"] = formats.Count
            };
        }

        private static JToken ExportToDwf(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            try
            {
                var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.CanBePrinted)
                    .ToList();

                // Use DWF2DExportOptions which has a simpler Export overload
                var dwfOpts = new DWFXExportOptions();
                dwfOpts.MergedViews = true;
                var fileName = System.IO.Path.GetFileNameWithoutExtension(doc.Title ?? "Export");
                // Export one view at a time to a DWFX file
                int exported = 0;
                foreach (var view in views.Take(50))
                {
                    try
                    {
                        var viewSet = new ViewSet();
                        viewSet.Insert(view);
                        doc.Export(outputFolder, CleanFileName(view.Name), viewSet, dwfOpts);
                        exported++;
                    }
                    catch { }
                }

                return new JObject
                {
                    ["message"] = $"✅ Exported {exported} view(s) to DWF.\nOutput folder: {outputFolder}",
                    ["count"] = exported
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"DWF export error: {ex.Message}" };
            }
        }

        private static JToken ExportToNwc(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            try
            {
                var nwcOpts = new NavisworksExportOptions();
                nwcOpts.ExportScope = NavisworksExportScope.Model;
                var fn = parameters?["fileName"]?.ToString();
                if (!string.IsNullOrWhiteSpace(fn))
                    nwcOpts.Parameters = NavisworksParameters.All;

                var fileName = fn ?? System.IO.Path.GetFileNameWithoutExtension(doc.Title ?? "Export");
                doc.Export(outputFolder, fileName, nwcOpts);

                return new JObject
                {
                    ["message"] = $"✅ Exported NWC to: {System.IO.Path.Combine(outputFolder, fileName + ".nwc")}",
                    ["outputFolder"] = outputFolder
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"NWC export error: {ex.Message}\nNavisworks exporter must be installed." };
            }
        }

        private static JToken ImportParametersFromCsv(Document doc, JObject parameters)
        {
            var filePath = parameters?["file"]?.ToString();
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
                return new JObject { ["message"] = $"CSV file not found: {filePath ?? "(not specified)"}" };

            try
            {
                var lines = System.IO.File.ReadAllLines(filePath);
                if (lines.Length < 2)
                    return new JObject { ["message"] = "CSV file is empty or has no data rows." };

                var headers = lines[0].Split(',');
                int updated = 0, skipped = 0;

                using (var t = new Transaction(doc, "Import Parameters from CSV"))
                {
                    t.Start();
                    for (int row = 1; row < lines.Length; row++)
                    {
                        var vals = lines[row].Split(',');
                        if (vals.Length < 2) continue;

                        // First column = ElementId
                        if (!int.TryParse(vals[0].Trim('"').Trim(), out int elemId)) { skipped++; continue; }
                        var elem = doc.GetElement(new ElementId(elemId));
                        if (elem == null) { skipped++; continue; }

                        for (int col = 1; col < headers.Length && col < vals.Length; col++)
                        {
                            var paramName = headers[col].Trim('"').Trim();
                            var value = vals[col].Trim('"').Trim();
                            if (string.IsNullOrEmpty(paramName)) continue;

                            var p = elem.LookupParameter(paramName);
                            if (p == null || p.IsReadOnly) continue;

                            try
                            {
                                if (p.StorageType == StorageType.String) p.Set(value);
                                else if (p.StorageType == StorageType.Integer && int.TryParse(value, out int iv)) p.Set(iv);
                                else if (p.StorageType == StorageType.Double && double.TryParse(value, out double dv)) p.Set(dv);
                                else p.SetValueString(value);
                                updated++;
                            }
                            catch { skipped++; }
                        }
                    }
                    t.Commit();
                }

                return new JObject
                {
                    ["message"] = $"✅ Imported CSV: {updated} parameter(s) updated, {skipped} skipped.\nFile: {filePath}",
                    ["updated"] = updated,
                    ["skipped"] = skipped
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"CSV import error: {ex.Message}" };
            }
        }

        private static JToken ManageFamilies(Document doc, JObject parameters)
        {
            var action = parameters?["action"]?.ToString() ?? "find_replace";
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

            int modified = 0;
            using (var t = new Transaction(doc, "Manage Families"))
            {
                t.Start();
                foreach (var fam in families)
                {
                    try
                    {
                        string oldName = fam.Name;
                        string newName = oldName;

                        switch (action)
                        {
                            case "find_replace":
                                var find = parameters?["find"]?.ToString() ?? "";
                                var replace = parameters?["replace"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(find) && oldName.Contains(find))
                                {
                                    newName = oldName.Replace(find, replace);
                                }
                                break;
                            case "add_prefix":
                                var prefix = parameters?["prefix"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(prefix))
                                    newName = prefix + oldName;
                                break;
                            case "add_suffix":
                                var suffix = parameters?["suffix"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(suffix))
                                    newName = oldName + suffix;
                                break;
                            case "rename":
                                var rn = parameters?["newName"]?.ToString();
                                if (!string.IsNullOrEmpty(rn))
                                    newName = rn;
                                break;
                        }

                        if (newName != oldName)
                        {
                            fam.Name = newName;
                            modified++;
                        }
                    }
                    catch { }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"✅ Modified {modified} of {families.Count} family name(s) (action: {action}).",
                ["modified"] = modified
            };
        }

        private static JToken CreateProjectParameter(Document doc, JObject parameters)
        {
            var name = parameters?["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(name))
                return new JObject { ["message"] = "Please provide a parameter name." };

            var catNames = parameters?["categories"]?.ToString() ?? "Walls";
            var isInstance = parameters?["isInstance"]?.ToString() == "true";
            var typeStr = parameters?["type"]?.ToString() ?? "Text";
            var groupStr = parameters?["group"]?.ToString() ?? "General";

            try
            {
                // Build category set
                var catSet = new CategorySet();
                foreach (var cn in catNames.Split(','))
                {
                    var bic = GetBuiltInCategory(cn.Trim());
                    if (bic != BuiltInCategory.INVALID)
                    {
                        var cat = doc.Settings.Categories.get_Item(bic);
                        if (cat != null) catSet.Insert(cat);
                    }
                }

                if (catSet.Size == 0)
                    return new JObject { ["message"] = $"No valid categories found in: {catNames}" };

                // Create an external definition file
                var defFile = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "RevitMCP_SharedParams.txt");
                if (!System.IO.File.Exists(defFile))
                    System.IO.File.WriteAllText(defFile, "");

                var app = doc.Application;
                var originalFile = app.SharedParametersFilename;
                app.SharedParametersFilename = defFile;

                var defFileObj = app.OpenSharedParameterFile();
                var groupDef = defFileObj.Groups.get_Item("RevitMCP")
                    ?? defFileObj.Groups.Create("RevitMCP");

                var existingDef = groupDef.Definitions.get_Item(name);
                ExternalDefinition extDef;
                if (existingDef != null)
                {
                    extDef = existingDef as ExternalDefinition;
                }
                else
                {
                    var opts = new ExternalDefinitionCreationOptions(name, SpecTypeId.String.Text);
                    extDef = groupDef.Definitions.Create(opts) as ExternalDefinition;
                }

                // Bind
                var binding = isInstance
                    ? (Binding)app.Create.NewInstanceBinding(catSet)
                    : (Binding)app.Create.NewTypeBinding(catSet);

                using (var t = new Transaction(doc, "Create Project Parameter"))
                {
                    t.Start();
                    doc.ParameterBindings.Insert(extDef, binding);
                    t.Commit();
                }

                app.SharedParametersFilename = originalFile;

                return new JObject
                {
                    ["message"] = $"✅ Created {(isInstance ? "instance" : "type")} parameter '{name}' for {catSet.Size} categories.",
                    ["name"] = name,
                    ["categories"] = catSet.Size
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"Create parameter error: {ex.Message}" };
            }
        }

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
                rooms = rooms.Where(r => ids.Contains(r.Id.IntegerValue)).ToList();
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
                                try { view.Name = $"{room.Name} - {dirs[i]} Elevation"; } catch { }
                                names.Add(view.Name);
                                created++;
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"✅ Created {created} elevation view(s) for {rooms.Count} room(s):\n" +
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
                rooms = rooms.Where(r => ids.Contains(r.Id.IntegerValue)).ToList();
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
                        try { view.Name = $"{room.Name} - Section"; } catch { }
                        names.Add(view.Name);
                        created++;
                    }
                    catch { }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"✅ Created {created} section view(s):\n" + string.Join("\n", names.Take(20)),
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
                rooms = rooms.Where(r => ids.Contains(r.Id.IntegerValue)).ToList();
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
                        try { callout.Name = $"{room.Name} - Callout"; } catch { }
                        names.Add(callout.Name);
                        created++;
                    }
                    catch { }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"✅ Created {created} callout view(s):\n" + string.Join("\n", names.Take(20)),
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
                ["message"] = $"✅ Aligned {aligned} viewport(s) across {targetIds.Count} target sheet(s) to match reference sheet.",
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
                catch { }
            }

            return new JObject
            {
                ["message"] = $"✅ Exported {exported} schedule(s) to CSV.\nOutput folder: {outputFolder}",
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
                ["message"] = $"✅ Exported {elements.Count} {catName} elements with {paramList.Count} parameters.\nSaved to: {filePath}",
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
                ["message"] = $"✅ Created {created.Count} sheet(s):\n" + string.Join("\n", created),
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
                        try { newView.Name = view.Name + suffix + (count > 1 ? $" {i + 1}" : ""); } catch { }
                        created.Add(newView.Name);
                    }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"✅ Duplicated {created.Count} view(s):\n" + string.Join("\n", created),
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
                        string.Join("\n", templates.Select(t => $"  • {t.Name}"))
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
                ["message"] = $"✅ Applied template '{template.Name}' to {applied} view(s).",
                ["count"] = applied
            };
        }

        private static JToken DeleteUnusedFamilies(Document doc, JObject parameters)
        {
            var catFilter = parameters?["category"]?.ToString();

            // Find all family types that have zero instances
            var familySymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();

            if (!string.IsNullOrWhiteSpace(catFilter))
            {
                var bic = GetBuiltInCategory(catFilter);
                if (bic != BuiltInCategory.INVALID)
                    familySymbols = familySymbols.Where(fs => fs.Category?.Id == new ElementId(bic)).ToList();
            }

            var unused = new List<FamilySymbol>();
            foreach (var fs in familySymbols)
            {
                var instances = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .Where(fi => (fi as FamilyInstance)?.Symbol?.Id == fs.Id)
                    .Count();
                if (instances == 0) unused.Add(fs);
            }

            if (unused.Count == 0)
                return new JObject { ["message"] = "No unused family types found." };

            var dryRun = parameters?["dryRun"]?.ToString() != "False" && parameters?["dryRun"]?.ToString() != "false";
            if (dryRun)
            {
                return new JObject
                {
                    ["message"] = $"Found {unused.Count} unused family type(s):\n" +
                        string.Join("\n", unused.Take(30).Select(u => $"  • {u.Family.Name}: {u.Name}")) +
                        (unused.Count > 30 ? $"\n  ... and {unused.Count - 30} more" : "") +
                        "\n\nSet dryRun to false to delete them.",
                    ["count"] = unused.Count
                };
            }

            int deleted = 0;
            using (var t = new Transaction(doc, "Delete Unused Families"))
            {
                t.Start();
                foreach (var fs in unused)
                {
                    try { doc.Delete(fs.Id); deleted++; } catch { }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"✅ Deleted {deleted} unused family type(s).",
                ["count"] = deleted
            };
        }

        private static JToken BatchSetParameter(Document doc, JObject parameters)
        {
            var catName = parameters?["category"]?.ToString() ?? "Walls";
            var paramName = parameters?["parameterName"]?.ToString();
            var value = parameters?["value"]?.ToString();

            if (string.IsNullOrWhiteSpace(paramName) || value == null)
                return new JObject { ["message"] = "Please provide parameterName and value." };

            var bic = GetBuiltInCategory(catName);
            if (bic == BuiltInCategory.INVALID)
                return new JObject { ["message"] = $"Unknown category: {catName}" };

            var elements = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToList();

            // Apply optional filters
            var filterParam = parameters?["filterParameterName"]?.ToString();
            var filterVal = parameters?["filterValue"]?.ToString();
            if (!string.IsNullOrWhiteSpace(filterParam) && !string.IsNullOrWhiteSpace(filterVal))
            {
                elements = elements.Where(e =>
                {
                    var p = e.LookupParameter(filterParam);
                    if (p == null) return false;
                    var pv = p.AsValueString() ?? p.AsString() ?? "";
                    return pv.IndexOf(filterVal, StringComparison.OrdinalIgnoreCase) >= 0;
                }).ToList();
            }

            if (elements.Count == 0)
                return new JObject { ["message"] = $"No matching {catName} elements found." };

            int modified = 0;
            using (var t = new Transaction(doc, "Batch Set Parameter"))
            {
                t.Start();
                foreach (var elem in elements)
                {
                    var p = elem.LookupParameter(paramName);
                    if (p != null && !p.IsReadOnly)
                    {
                        try
                        {
                            if (p.StorageType == StorageType.String) p.Set(value);
                            else if (p.StorageType == StorageType.Integer && int.TryParse(value, out int iv)) p.Set(iv);
                            else if (p.StorageType == StorageType.Double && double.TryParse(value, out double dv)) p.Set(dv);
                            else p.SetValueString(value);
                            modified++;
                        }
                        catch { }
                    }
                }
                t.Commit();
            }

            return new JObject
            {
                ["message"] = $"✅ Set '{paramName}' = '{value}' on {modified} of {elements.Count} {catName} element(s).",
                ["modified"] = modified,
                ["total"] = elements.Count
            };
        }

        // Helper: clean file name
        private static string CleanFileName(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        // Helper: increment sheet number like A101 → A102
        private static string IncrementNumber(string start, int offset)
        {
            if (offset == 0) return start;
            // Find trailing digits
            int i = start.Length - 1;
            while (i >= 0 && char.IsDigit(start[i])) i--;
            var prefix = start.Substring(0, i + 1);
            var numPart = start.Substring(i + 1);
            if (int.TryParse(numPart, out int num))
                return prefix + (num + offset).ToString(new string('0', numPart.Length));
            return start + "_" + offset;
        }

        // ===== GENERIC COMMAND (for commands not yet fully implemented) =====

        private static JToken ExecuteGenericCommand(Document doc, string command, JObject parameters)
        {
            return new JObject
            {
                ["command"] = command,
                ["status"] = "received",
                ["message"] = $"Command '{command}' received. This command requires the extended command set module.",
                ["parameters"] = parameters
            };
        }

        // ===== HELPER METHODS =====

        private static BuiltInCategory GetBuiltInCategory(string categoryName)
        {
            var mapping = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
            {
                ["Walls"] = BuiltInCategory.OST_Walls,
                ["Doors"] = BuiltInCategory.OST_Doors,
                ["Windows"] = BuiltInCategory.OST_Windows,
                ["Floors"] = BuiltInCategory.OST_Floors,
                ["Ceilings"] = BuiltInCategory.OST_Ceilings,
                ["Roofs"] = BuiltInCategory.OST_Roofs,
                ["Columns"] = BuiltInCategory.OST_Columns,
                ["Furniture"] = BuiltInCategory.OST_Furniture,
                ["Rooms"] = BuiltInCategory.OST_Rooms,
                ["Stairs"] = BuiltInCategory.OST_Stairs,
                ["Railings"] = BuiltInCategory.OST_StairsRailing,
                ["Grids"] = BuiltInCategory.OST_Grids,
                ["Levels"] = BuiltInCategory.OST_Levels,
                ["Pipes"] = BuiltInCategory.OST_PipeCurves,
                ["Ducts"] = BuiltInCategory.OST_DuctCurves,
                ["Electrical"] = BuiltInCategory.OST_ElectricalEquipment,
                ["Plumbing"] = BuiltInCategory.OST_PlumbingFixtures,
                ["Mechanical"] = BuiltInCategory.OST_MechanicalEquipment,
            };

            return mapping.TryGetValue(categoryName, out var cat) ? cat : BuiltInCategory.INVALID;
        }
    }
}
