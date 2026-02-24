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
                    return CreatePointBasedElement(doc, parameters);
                case "create_line_based_element":
                    return CreateLineBasedElement(doc, parameters);
                case "create_floor":
                    return CreateFloor(doc, parameters);
                case "create_ceiling":
                    return CreateCeiling(doc, parameters);
                case "create_roof":
                    return CreateRoof(doc, parameters);
                case "create_view":
                    return CreateView(doc, parameters);
                case "create_schedule":
                    return CreateSchedule(doc, parameters);
                case "create_tag":
                    return CreateTag(uidoc!, doc, parameters);
                case "create_dimension":
                    return CreateDimensionCmd(uidoc!, doc, parameters);
                case "create_text_note":
                    return CreateTextNote(uidoc!, doc, parameters);

                // ===== EDITING COMMANDS =====
                case "modify_element":
                    return ModifyElement(doc, parameters);
                case "move_element":
                    return MoveElement(doc, parameters);
                case "delete_elements":
                    return DeleteElements(doc, parameters);
                case "copy_element":
                    return CopyElement(doc, parameters);
                case "rotate_element":
                    return RotateElement(doc, parameters);
                case "mirror_element":
                    return MirrorElement(doc, parameters);
                case "align_elements":
                    return AlignElements(doc, parameters);
                case "group_elements":
                    return GroupElements(doc, parameters);
                case "change_type":
                    return ChangeType(doc, parameters);
                case "set_workset":
                    return SetWorkset(doc, parameters);
                case "color_elements":
                    return ColorElements(uidoc!, doc, parameters);
                case "batch_modify_parameters":
                    return BatchModifyParameters(doc, parameters);

                // ===== DOCUMENTATION COMMANDS =====
                case "place_view_on_sheet":
                case "create_viewport":
                    return PlaceViewOnSheet(doc, parameters);
                case "tag_all_in_view":
                    return TagAllInView(uidoc!, doc, parameters);
                case "create_legend":
                case "add_revision":
                case "print_sheets":
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

                // ===== PROJECT SETTINGS COMMANDS =====
                case "modify_object_styles":
                    return ModifyObjectStyles(doc, parameters);
                case "set_phase":
                    return SetPhase(doc, parameters);
                case "get_phases":
                    return GetPhases(doc);
                case "get_materials":
                    return GetMaterials(doc);
                case "set_material":
                    return SetMaterial(doc, parameters);
                case "get_views":
                    return GetViews(doc, parameters);
                case "open_view":
                    return OpenView(uidoc!, doc, parameters);
                case "close_view":
                    return CloseView(uidoc!, doc, parameters);
                case "set_view_properties":
                    return SetViewProperties(uidoc!, doc, parameters);
                case "override_element_in_view":
                    return OverrideElementInView(uidoc!, doc, parameters);
                case "get_line_styles":
                    return GetLineStyles(doc);
                case "set_line_style":
                    return SetLineStyle(doc, parameters);

                // ===== POWER TOOLS =====
                // Geometry
                case "auto_join_elements":
                    return AutoJoinElements(doc, parameters);
                case "reassign_level":
                    return ReassignLevel(doc, parameters);
                case "batch_modify_thickness":
                    return BatchModifyThickness(doc, parameters);
                case "room_to_floor":
                    return RoomToFloor(doc, parameters);
                // Data & Parameters
                case "find_replace_names":
                    return FindReplaceNames(doc, parameters);
                case "parameter_case_convert":
                    return ParameterCaseConvert(doc, parameters);
                case "bulk_parameter_transfer":
                    return BulkParameterTransfer(doc, parameters);
                case "auto_renumber":
                    return AutoRenumber(doc, parameters);
                // Views & Documentation
                case "batch_create_sheets":
                    return BatchCreateSheets(doc, parameters);
                case "align_viewports":
                    return AlignViewports(doc, parameters);
                // Project Cleanup
                case "deep_purge":
                    return DeepPurge(doc);
                case "delete_empty_groups":
                    return DeleteEmptyGroups(doc);
                case "find_cad_imports":
                    return FindCadImports(doc, parameters);
                // Selection & Filtering
                case "select_by_parameter":
                    return SelectByParameter(uidoc!, doc, parameters);
                case "select_by_workset":
                    return SelectByWorkset(uidoc!, doc, parameters);
                case "filter_selection":
                    return FilterSelection(uidoc!, doc, parameters);
                case "category_to_workset":
                    return CategoryToWorkset(doc, parameters);
                case "inverse_selection":
                    return InverseSelection(uidoc!, doc);
                case "copy_from_linked":
                    return CopyFromLinked(doc, parameters);
                case "crop_region_sync":
                    return CropRegionSync(doc, parameters);
                case "apply_view_template":
                    return ApplyViewTemplate(uidoc!, doc, parameters);
                case "resolve_warnings":
                    return ResolveWarnings(doc, parameters);
                case "wall_floor_sync":
                    return WallFloorSync(doc, parameters);
                case "snap_beams_to_columns":
                    return SnapBeamsToColumns(doc, parameters);
                case "convert_category":
                    return ConvertCategory(doc, parameters);
                case "add_shared_parameter":
                    return AddSharedParameter(doc, uiApp, parameters);
                case "import_data_from_csv":
                    return ImportDataFromCsv(doc, parameters);
                case "generate_legend":
                    return GenerateLegend(doc, parameters);
                case "cad_to_lines":
                    return CadToLines(doc, parameters);

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
                case "duplicate_view":
                    return DuplicateView(doc, parameters);

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

                            // Handle view-specific properties that aren't accessible via elem.Parameters
                            if (elem is View view)
                            {
                                bool handled = false;
                                if (paramName.Equals("Detail Level", StringComparison.OrdinalIgnoreCase) ||
                                    paramName.Equals("DetailLevel", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (Enum.TryParse<ViewDetailLevel>(value?.ToString(), true, out var vdl))
                                    { view.DetailLevel = vdl; modCount++; handled = true; }
                                }
                                else if (paramName.Equals("Visual Style", StringComparison.OrdinalIgnoreCase) ||
                                         paramName.Equals("DisplayStyle", StringComparison.OrdinalIgnoreCase) ||
                                         paramName.Equals("Display Style", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (Enum.TryParse<DisplayStyle>(value?.ToString(), true, out var ds))
                                    { view.DisplayStyle = ds; modCount++; handled = true; }
                                }
                                if (handled) continue;
                            }

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

            // Collect views/sheets to export — respect selection from Export Manager
            var viewIds = new List<ElementId>();

            var sheetIdStr = parameters?["sheetIds"]?.ToString();
            if (!string.IsNullOrWhiteSpace(sheetIdStr))
            {
                foreach (var idStr in sheetIdStr.Split(','))
                {
                    if (long.TryParse(idStr.Trim(), out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is ViewSheet vs && !vs.IsPlaceholder)
                            viewIds.Add(vs.Id);
                    }
                }
            }

            var viewIdStr = parameters?["viewIds"]?.ToString();
            if (!string.IsNullOrWhiteSpace(viewIdStr))
            {
                foreach (var idStr in viewIdStr.Split(','))
                {
                    if (long.TryParse(idStr.Trim(), out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is View v && !v.IsTemplate && v.CanBePrinted)
                            viewIds.Add(v.Id);
                    }
                }
            }

            // Fallback: if no specific selection, export all sheets
            if (viewIds.Count == 0)
            {
                var allSheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsPlaceholder)
                    .ToList();
                if (allSheets.Count == 0)
                    return new JObject { ["message"] = "No sheets found in the project." };
                viewIds = allSheets.Select(s => s.Id).ToList();
            }

            // Use Revit PDF export (Revit 2022+)
            try
            {
                var pdfOptions = new PDFExportOptions();
                pdfOptions.FileName = doc.Title ?? "Export";

                // Read format settings from parameters
                pdfOptions.Combine = parameters?["combine"]?.ToString() == "true";

                var rasterQuality = parameters?["rasterQuality"]?.ToString();
                if (!string.IsNullOrWhiteSpace(rasterQuality))
                {
                    switch (rasterQuality.ToLower())
                    {
                        case "low": pdfOptions.RasterQuality = RasterQualityType.Low; break;
                        case "medium": pdfOptions.RasterQuality = RasterQualityType.Medium; break;
                        case "high": pdfOptions.RasterQuality = RasterQualityType.High; break;
                    }
                }

                var colorMode = parameters?["color"]?.ToString();
                if (!string.IsNullOrWhiteSpace(colorMode))
                {
                    switch (colorMode.ToLower())
                    {
                        case "color": pdfOptions.ColorDepth = ColorDepthType.Color; break;
                        case "grayscale": pdfOptions.ColorDepth = ColorDepthType.GrayScale; break;
                        case "black & white": pdfOptions.ColorDepth = ColorDepthType.BlackLine; break;
                    }
                }

                // Hidden line processing (HiddenLineViewsExportAs not available in Revit 2025)

                // Hide options
                if (parameters?["hideScopeBox"]?.ToString() == "true")
                    pdfOptions.HideScopeBoxes = true;
                if (parameters?["hideRefPlane"]?.ToString() == "true")
                    pdfOptions.HideReferencePlane = true;
                if (parameters?["hideCropBoundary"]?.ToString() == "true")
                    pdfOptions.HideCropBoundaries = true;

                // Paper placement
                var placement = parameters?["paperPlacement"]?.ToString();
                if (placement == "center")
                    pdfOptions.PaperPlacement = PaperPlacementType.Center;
                else if (placement == "offset")
                    pdfOptions.PaperPlacement = PaperPlacementType.LowerLeft;

                // Zoom
                var zoom = parameters?["zoom"]?.ToString();
                if (zoom == "fitToPage")
                    pdfOptions.ZoomType = ZoomType.FitToPage;
                else if (zoom == "zoom")
                    pdfOptions.ZoomType = ZoomType.Zoom;

                doc.Export(outputFolder, viewIds, pdfOptions);

                return new JObject
                {
                    ["message"] = $"✅ Exported {viewIds.Count} view/sheet(s) to PDF.\nOutput folder: {outputFolder}",
                    ["count"] = viewIds.Count,
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

            // Respect selection from Export Manager
            var selectedIds = new List<ElementId>();
            var sheetIdStr = parameters?["sheetIds"]?.ToString();
            if (!string.IsNullOrWhiteSpace(sheetIdStr))
            {
                foreach (var idStr in sheetIdStr.Split(','))
                    if (long.TryParse(idStr.Trim(), out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is ViewSheet) selectedIds.Add(elem.Id);
                    }
            }
            var viewIdStr = parameters?["viewIds"]?.ToString();
            if (!string.IsNullOrWhiteSpace(viewIdStr))
            {
                foreach (var idStr in viewIdStr.Split(','))
                    if (long.TryParse(idStr.Trim(), out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is View v && !v.IsTemplate && v.CanBePrinted) selectedIds.Add(v.Id);
                    }
            }

            // Fallback: all printable views
            if (selectedIds.Count == 0)
            {
                selectedIds = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.CanBePrinted)
                    .Take(50)
                    .Select(v => v.Id)
                    .ToList();
            }

            int exported = 0;
            foreach (var vid in selectedIds.Take(50))
            {
                try
                {
                    var view = doc.GetElement(vid) as View;
                    if (view == null) continue;
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
                    imgOpts.SetViewsAndSheets(new List<ElementId> { vid });
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

                // If a specific view is selected, export only that view
                var viewIdStr = parameters?["viewIds"]?.ToString();
                var sheetIdStr = parameters?["sheetIds"]?.ToString();
                var idStr = !string.IsNullOrWhiteSpace(sheetIdStr) ? sheetIdStr : viewIdStr;
                if (!string.IsNullOrWhiteSpace(idStr))
                {
                    var firstId = idStr.Split(',').FirstOrDefault()?.Trim();
                    if (long.TryParse(firstId, out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is View v)
                            ifcOpts.FilterViewId = v.Id;
                    }
                }

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
                // Respect selection from Export Manager
                var viewIds = new List<ElementId>();
                var sheetIdStr = parameters?["sheetIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(sheetIdStr))
                {
                    foreach (var idStr in sheetIdStr.Split(','))
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is ViewSheet) viewIds.Add(elem.Id);
                        }
                }
                var viewIdStr = parameters?["viewIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(viewIdStr))
                {
                    foreach (var idStr in viewIdStr.Split(','))
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is View v && !v.IsTemplate && v.CanBePrinted) viewIds.Add(v.Id);
                        }
                }

                // Fallback: all printable views
                if (viewIds.Count == 0)
                {
                    viewIds = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => !v.IsTemplate && v.CanBePrinted)
                        .Select(v => v.Id)
                        .ToList();
                }

                var dgnOpts = new DGNExportOptions();
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
                // Collect specified views/sheets, or fall back to active view only
                var viewIds = new List<ElementId>();

                var viewIdStr = parameters?["viewIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(viewIdStr))
                {
                    foreach (var idStr in viewIdStr.Split(','))
                    {
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is View v && !v.IsTemplate && v.CanBePrinted)
                                viewIds.Add(v.Id);
                        }
                    }
                }

                var sheetIdStr = parameters?["sheetIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(sheetIdStr))
                {
                    foreach (var idStr in sheetIdStr.Split(','))
                    {
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is ViewSheet)
                                viewIds.Add(elem.Id);
                        }
                    }
                }

                // If no specific views provided, use active view
                if (viewIds.Count == 0)
                {
                    var activeView = doc.ActiveView;
                    if (activeView != null && !activeView.IsTemplate && activeView.CanBePrinted)
                        viewIds.Add(activeView.Id);
                }

                if (viewIds.Count == 0)
                    return new JObject { ["message"] = "⚠ No exportable views found." };

                var dwgOpts = new DWGExportOptions();

                // Read hide options from parameters (default true)
                dwgOpts.HideScopeBox = parameters?["hideScopeBox"]?.ToString() != "false";
                dwgOpts.HideReferencePlane = parameters?["hideRefPlane"]?.ToString() != "false";

                int exported = 0;
                foreach (var vid in viewIds.Take(50))
                {
                    try
                    {
                        var view = doc.GetElement(vid) as View;
                        if (view == null) continue;
                        var ids = new List<ElementId> { vid };
                        var cleanName = CleanFileName(view.Name);

                        doc.Export(outputFolder, cleanName, ids, dwgOpts);

                        // NOTE: Revit generates companion files (.tif, .jpg, .png) as raster image
                        // references alongside the DWG. These MUST be kept or images won't display.
                        // Only clean up PCP plot config files.
                        var mainDwg = System.IO.Path.Combine(outputFolder, cleanName + ".dwg");
                        foreach (var file in System.IO.Directory.GetFiles(outputFolder, cleanName + ".pcp"))
                        {
                            try { System.IO.File.Delete(file); } catch { }
                        }

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
                // Respect selection from Export Manager
                var selectedIds = new List<ElementId>();
                var sheetIdStr = parameters?["sheetIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(sheetIdStr))
                {
                    foreach (var idStr in sheetIdStr.Split(','))
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is ViewSheet) selectedIds.Add(elem.Id);
                        }
                }
                var viewIdStr = parameters?["viewIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(viewIdStr))
                {
                    foreach (var idStr in viewIdStr.Split(','))
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is View v && !v.IsTemplate && v.CanBePrinted) selectedIds.Add(v.Id);
                        }
                }

                // Fallback: all printable views
                if (selectedIds.Count == 0)
                {
                    selectedIds = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => !v.IsTemplate && v.CanBePrinted)
                        .Select(v => v.Id)
                        .ToList();
                }

                var dwfOpts = new DWFXExportOptions();
                dwfOpts.MergedViews = true;
                int exported = 0;
                foreach (var vid in selectedIds.Take(50))
                {
                    try
                    {
                        var view = doc.GetElement(vid) as View;
                        if (view == null) continue;
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
                nwcOpts.Parameters = NavisworksParameters.All;

                // If a specific view is selected, export just that view
                var viewIdStr = parameters?["viewIds"]?.ToString();
                var sheetIdStr = parameters?["sheetIds"]?.ToString();
                var idStr = !string.IsNullOrWhiteSpace(sheetIdStr) ? sheetIdStr : viewIdStr;
                if (!string.IsNullOrWhiteSpace(idStr))
                {
                    var firstId = idStr.Split(',').FirstOrDefault()?.Trim();
                    if (long.TryParse(firstId, out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is View v)
                        {
                            nwcOpts.ExportScope = NavisworksExportScope.View;
                            nwcOpts.ViewId = v.Id;
                        }
                    }
                }
                else
                {
                    nwcOpts.ExportScope = NavisworksExportScope.Model;
                }

                var fn = parameters?["fileName"]?.ToString();
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

        // ══════════════════════════════════════════════════════════════
        // ████  EDITING COMMANDS  ████
        // ══════════════════════════════════════════════════════════════

        private static JToken CopyElement(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Copy Element"))
            {
                tx.Start();
                try
                {
                    var elementId = parameters["elementId"]?.Value<long>() ?? 0;
                    var dx = parameters["deltaX"]?.Value<double>() ?? 0;
                    var dy = parameters["deltaY"]?.Value<double>() ?? 0;
                    var dz = parameters["deltaZ"]?.Value<double>() ?? 0;
                    var count = parameters["count"]?.Value<int>() ?? 1;

                    var elem = doc.GetElement(new ElementId(elementId));
                    if (elem == null) throw new InvalidOperationException($"Element {elementId} not found");

                    var translation = new XYZ(dx, dy, dz);
                    var allCopied = new List<long>();
                    for (int i = 0; i < count; i++)
                    {
                        var offset = translation * (i + 1);
                        var copied = ElementTransformUtils.CopyElement(doc, elem.Id, offset);
                        foreach (var id in copied) allCopied.Add(id.Value);
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Copied element {count} time(s). New IDs: {string.Join(", ", allCopied)}", ["newElementIds"] = new JArray(allCopied) };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken RotateElement(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Rotate Element"))
            {
                tx.Start();
                try
                {
                    var elementId = parameters["elementId"]?.Value<long>() ?? 0;
                    var angle = parameters["angle"]?.Value<double>() ?? 0;
                    var elem = doc.GetElement(new ElementId(elementId));
                    if (elem == null) throw new InvalidOperationException($"Element {elementId} not found");

                    var bb = elem.get_BoundingBox(null);
                    var center = bb != null ? (bb.Min + bb.Max) / 2 : XYZ.Zero;
                    var cx = parameters["centerX"]?.Value<double>() ?? center.X;
                    var cy = parameters["centerY"]?.Value<double>() ?? center.Y;

                    var axis = Line.CreateBound(new XYZ(cx, cy, center.Z), new XYZ(cx, cy, center.Z + 1));
                    ElementTransformUtils.RotateElement(doc, elem.Id, axis, angle * Math.PI / 180.0);

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Rotated element {elementId} by {angle}°" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken MirrorElement(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Mirror Element"))
            {
                tx.Start();
                try
                {
                    var elementId = parameters["elementId"]?.Value<long>() ?? 0;
                    var ax1X = parameters["axisStartX"]?.Value<double>() ?? 0;
                    var ax1Y = parameters["axisStartY"]?.Value<double>() ?? 0;
                    var ax2X = parameters["axisEndX"]?.Value<double>() ?? 10;
                    var ax2Y = parameters["axisEndY"]?.Value<double>() ?? 0;
                    var keep = parameters["keepOriginal"]?.Value<bool>() ?? true;

                    var elem = doc.GetElement(new ElementId(elementId));
                    if (elem == null) throw new InvalidOperationException($"Element {elementId} not found");

                    var dir = new XYZ(ax2X - ax1X, ax2Y - ax1Y, 0).Normalize();
                    var normal = new XYZ(-dir.Y, dir.X, 0);
                    var plane = Plane.CreateByNormalAndOrigin(normal, new XYZ(ax1X, ax1Y, 0));

                    if (keep)
                        ElementTransformUtils.MirrorElements(doc, new List<ElementId> { elem.Id }, plane, true);
                    else
                        ElementTransformUtils.MirrorElements(doc, new List<ElementId> { elem.Id }, plane, false);

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Mirrored element {elementId}" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken ChangeType(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Change Type"))
            {
                tx.Start();
                try
                {
                    var elementId = parameters["elementId"]?.Value<long>() ?? 0;
                    var newTypeName = parameters["newTypeName"]?.ToString();
                    var elem = doc.GetElement(new ElementId(elementId));
                    if (elem == null) throw new InvalidOperationException($"Element {elementId} not found");

                    // Find the new type by name
                    var currentTypeId = elem.GetTypeId();
                    var currentType = doc.GetElement(currentTypeId);
                    if (currentType == null) throw new InvalidOperationException("Element has no type");

                    var category = currentType.Category;
                    ElementId newTypeId = null;
                    var collector = new FilteredElementCollector(doc).OfClass(currentType.GetType());
                    foreach (var t in collector)
                    {
                        if (t.Name == newTypeName)
                        {
                            newTypeId = t.Id;
                            break;
                        }
                    }

                    if (newTypeId == null)
                        throw new InvalidOperationException($"Type '{newTypeName}' not found. Available types: {string.Join(", ", collector.Select(t => t.Name).Take(20))}");

                    elem.ChangeTypeId(newTypeId);
                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Changed type of element {elementId} to '{newTypeName}'" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken ColorElements(UIDocument uidoc, Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Color Elements"))
            {
                tx.Start();
                try
                {
                    var category = parameters["category"]?.ToString();
                    var paramName = parameters["parameterName"]?.ToString();
                    var view = uidoc.ActiveView;

                    var cat = GetBuiltInCategory(category);
                    var elements = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .ToList();

                    // Group by parameter value and assign colors
                    var groups = new Dictionary<string, List<Element>>();
                    foreach (var elem in elements)
                    {
                        string val = "(none)";
                        foreach (Parameter p in elem.Parameters)
                        {
                            if (p.Definition.Name == paramName)
                            {
                                val = p.AsValueString() ?? p.AsString() ?? "(empty)";
                                break;
                            }
                        }
                        if (!groups.ContainsKey(val)) groups[val] = new List<Element>();
                        groups[val].Add(elem);
                    }

                    var colors = new[] {
                        new Color(255, 99, 71), new Color(60, 179, 113), new Color(65, 105, 225),
                        new Color(255, 165, 0), new Color(148, 103, 189), new Color(255, 215, 0),
                        new Color(0, 206, 209), new Color(255, 105, 180), new Color(139, 69, 19),
                        new Color(128, 128, 0)
                    };

                    int ci = 0;
                    foreach (var kvp in groups)
                    {
                        var color = colors[ci % colors.Length];
                        var ogs = new OverrideGraphicSettings();
                        ogs.SetProjectionLineColor(color);
                        ogs.SetSurfaceForegroundPatternColor(color);
                        ogs.SetSurfaceForegroundPatternVisible(true);

                        // Try to find and set a solid fill pattern
                        var solidFill = new FilteredElementCollector(doc)
                            .OfClass(typeof(FillPatternElement))
                            .Cast<FillPatternElement>()
                            .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);
                        if (solidFill != null)
                            ogs.SetSurfaceForegroundPatternId(solidFill.Id);

                        foreach (var elem in kvp.Value)
                            view.SetElementOverrides(elem.Id, ogs);
                        ci++;
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Colored {elements.Count} element(s) by '{paramName}' ({groups.Count} unique values)", ["groups"] = groups.Count };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken BatchModifyParameters(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Batch Modify Parameters"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    var paramName = parameters["parameterName"]?.ToString();
                    var value = parameters["value"];
                    if (elementIds == null || string.IsNullOrEmpty(paramName))
                        throw new InvalidOperationException("elementIds and parameterName are required");

                    int modified = 0;
                    foreach (var idToken in elementIds)
                    {
                        var elem = doc.GetElement(new ElementId(idToken.Value<long>()));
                        if (elem == null) continue;
                        foreach (Parameter p in elem.Parameters)
                        {
                            if (p.Definition.Name == paramName && !p.IsReadOnly)
                            {
                                if (value?.Type == JTokenType.String) p.Set(value.ToString());
                                else if (value?.Type == JTokenType.Integer || value?.Type == JTokenType.Float) p.Set(value.Value<double>());
                                modified++;
                                break;
                            }
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Modified '{paramName}' on {modified} element(s)", ["count"] = modified };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken GroupElements(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Group Elements"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    var groupName = parameters["groupName"]?.ToString();
                    if (elementIds == null || elementIds.Count == 0)
                        throw new InvalidOperationException("elementIds is required");

                    var ids = elementIds.Select(id => new ElementId(id.Value<long>())).ToList();
                    var group = doc.Create.NewGroup(ids);
                    if (!string.IsNullOrEmpty(groupName))
                        group.GroupType.Name = groupName;

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Grouped {ids.Count} element(s). Group ID: {group.Id.Value}", ["groupId"] = group.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken AlignElements(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Align Elements"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    var refId = parameters["referenceElementId"]?.Value<long>();
                    var alignment = parameters["alignment"]?.ToString() ?? "Left";
                    if (elementIds == null || elementIds.Count == 0)
                        throw new InvalidOperationException("elementIds is required");

                    var ids = elementIds.Select(id => new ElementId(id.Value<long>())).ToList();

                    // Get reference bounding box
                    BoundingBoxXYZ refBB = null;
                    if (refId.HasValue)
                    {
                        var refElem = doc.GetElement(new ElementId(refId.Value));
                        refBB = refElem?.get_BoundingBox(null);
                    }
                    if (refBB == null)
                    {
                        var firstElem = doc.GetElement(ids[0]);
                        refBB = firstElem?.get_BoundingBox(null);
                    }
                    if (refBB == null) throw new InvalidOperationException("Cannot determine reference position");

                    int moved = 0;
                    foreach (var eid in ids)
                    {
                        var elem = doc.GetElement(eid);
                        if (elem == null) continue;
                        var bb = elem.get_BoundingBox(null);
                        if (bb == null) continue;

                        XYZ delta = XYZ.Zero;
                        switch (alignment)
                        {
                            case "Left": delta = new XYZ(refBB.Min.X - bb.Min.X, 0, 0); break;
                            case "Right": delta = new XYZ(refBB.Max.X - bb.Max.X, 0, 0); break;
                            case "Top": delta = new XYZ(0, refBB.Max.Y - bb.Max.Y, 0); break;
                            case "Bottom": delta = new XYZ(0, refBB.Min.Y - bb.Min.Y, 0); break;
                            case "Center": delta = new XYZ((refBB.Min.X + refBB.Max.X) / 2 - (bb.Min.X + bb.Max.X) / 2, 0, 0); break;
                            case "Middle": delta = new XYZ(0, (refBB.Min.Y + refBB.Max.Y) / 2 - (bb.Min.Y + bb.Max.Y) / 2, 0); break;
                        }

                        if (!delta.IsZeroLength())
                        {
                            ElementTransformUtils.MoveElement(doc, eid, delta);
                            moved++;
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Aligned {moved} element(s) to {alignment}" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken SetWorkset(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Set Workset"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    var worksetName = parameters["worksetName"]?.ToString();
                    if (elementIds == null || string.IsNullOrEmpty(worksetName))
                        throw new InvalidOperationException("elementIds and worksetName are required");

                    if (!doc.IsWorkshared)
                        throw new InvalidOperationException("Document is not workshared");

                    // Find workset by name
                    var worksets = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToWorksets();
                    var targetWorkset = worksets.FirstOrDefault(w => w.Name == worksetName);
                    if (targetWorkset == null)
                        throw new InvalidOperationException($"Workset '{worksetName}' not found. Available: {string.Join(", ", worksets.Select(w => w.Name))}");

                    int modified = 0;
                    foreach (var idToken in elementIds)
                    {
                        var elem = doc.GetElement(new ElementId(idToken.Value<long>()));
                        if (elem == null) continue;
                        var wsParam = elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                        if (wsParam != null && !wsParam.IsReadOnly)
                        {
                            wsParam.Set(targetWorkset.Id.IntegerValue);
                            modified++;
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Moved {modified} element(s) to workset '{worksetName}'" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ████  CREATING COMMANDS  ████
        // ══════════════════════════════════════════════════════════════

        private static JToken CreatePointBasedElement(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Point-Based Element"))
            {
                tx.Start();
                try
                {
                    var familyName = parameters["familyName"]?.ToString();
                    var typeName = parameters["typeName"]?.ToString();
                    var x = parameters["x"]?.Value<double>() ?? 0;
                    var y = parameters["y"]?.Value<double>() ?? 0;
                    var z = parameters["z"]?.Value<double>() ?? 0;
                    var levelName = parameters["levelName"]?.ToString();
                    var hostId = parameters["hostElementId"]?.Value<long>();

                    // Find family symbol
                    var symbol = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(fs => fs.Family.Name == familyName && fs.Name == typeName);
                    if (symbol == null)
                        throw new InvalidOperationException($"Family type '{familyName}: {typeName}' not found");

                    if (!symbol.IsActive) symbol.Activate();

                    // Find level
                    Level level = null;
                    if (!string.IsNullOrEmpty(levelName))
                        level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => l.Name == levelName);
                    if (level == null)
                        level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => Math.Abs(l.Elevation - z)).First();

                    var point = new XYZ(x, y, z);
                    FamilyInstance instance;

                    if (hostId.HasValue)
                    {
                        var host = doc.GetElement(new ElementId(hostId.Value));
                        if (host != null)
                            instance = doc.Create.NewFamilyInstance(point, symbol, host, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        else
                            instance = doc.Create.NewFamilyInstance(point, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    }
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(point, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Created {familyName}: {typeName} (ID: {instance.Id.Value})", ["elementId"] = instance.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CreateLineBasedElement(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Line-Based Element"))
            {
                tx.Start();
                try
                {
                    var familyName = parameters["familyName"]?.ToString();
                    var typeName = parameters["typeName"]?.ToString();
                    var sx = parameters["startX"]?.Value<double>() ?? 0;
                    var sy = parameters["startY"]?.Value<double>() ?? 0;
                    var sz = parameters["startZ"]?.Value<double>() ?? 0;
                    var ex = parameters["endX"]?.Value<double>() ?? 0;
                    var ey = parameters["endY"]?.Value<double>() ?? 0;
                    var ez = parameters["endZ"]?.Value<double>() ?? 0;
                    var levelName = parameters["levelName"]?.ToString();

                    var symbol = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(fs => fs.Family.Name == familyName && fs.Name == typeName);
                    if (symbol == null)
                        throw new InvalidOperationException($"Family type '{familyName}: {typeName}' not found");
                    if (!symbol.IsActive) symbol.Activate();

                    Level level = null;
                    if (!string.IsNullOrEmpty(levelName))
                        level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => l.Name == levelName);
                    if (level == null)
                        level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => Math.Abs(l.Elevation - sz)).First();

                    var line = Line.CreateBound(new XYZ(sx, sy, sz), new XYZ(ex, ey, ez));
                    var instance = doc.Create.NewFamilyInstance(line, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.Beam);

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Created {familyName}: {typeName} (ID: {instance.Id.Value})", ["elementId"] = instance.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CreateFloor(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Floor"))
            {
                tx.Start();
                try
                {
                    var points = parameters["points"] as JArray;
                    var levelName = parameters["levelName"]?.ToString();
                    var floorTypeName = parameters["floorType"]?.ToString();

                    if (points == null || points.Count < 3)
                        throw new InvalidOperationException("At least 3 boundary points required");

                    var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => l.Name == levelName);
                    if (level == null) throw new InvalidOperationException($"Level '{levelName}' not found");

                    var floorType = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>()
                        .FirstOrDefault(ft => !string.IsNullOrEmpty(floorTypeName) ? ft.Name == floorTypeName : true);
                    if (floorType == null) throw new InvalidOperationException("No floor type available");

                    var curveLoop = new CurveLoop();
                    for (int i = 0; i < points.Count; i++)
                    {
                        var p1 = points[i]; var p2 = points[(i + 1) % points.Count];
                        curveLoop.Append(Line.CreateBound(
                            new XYZ(p1["x"].Value<double>(), p1["y"].Value<double>(), level.Elevation),
                            new XYZ(p2["x"].Value<double>(), p2["y"].Value<double>(), level.Elevation)));
                    }

                    var floor = Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorType.Id, level.Id);

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Created floor (ID: {floor.Id.Value})", ["elementId"] = floor.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CreateCeiling(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Ceiling"))
            {
                tx.Start();
                try
                {
                    var points = parameters["points"] as JArray;
                    var levelName = parameters["levelName"]?.ToString();
                    var ceilingTypeName = parameters["ceilingType"]?.ToString();

                    if (points == null || points.Count < 3)
                        throw new InvalidOperationException("At least 3 boundary points required");

                    var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => l.Name == levelName);
                    if (level == null) throw new InvalidOperationException($"Level '{levelName}' not found");

                    var ceilingType = new FilteredElementCollector(doc).OfClass(typeof(CeilingType)).Cast<CeilingType>()
                        .FirstOrDefault(ct => !string.IsNullOrEmpty(ceilingTypeName) ? ct.Name == ceilingTypeName : true);
                    if (ceilingType == null) throw new InvalidOperationException("No ceiling type available");

                    var curveLoop = new CurveLoop();
                    for (int i = 0; i < points.Count; i++)
                    {
                        var p1 = points[i]; var p2 = points[(i + 1) % points.Count];
                        curveLoop.Append(Line.CreateBound(
                            new XYZ(p1["x"].Value<double>(), p1["y"].Value<double>(), level.Elevation),
                            new XYZ(p2["x"].Value<double>(), p2["y"].Value<double>(), level.Elevation)));
                    }

                    var ceiling = Ceiling.Create(doc, new List<CurveLoop> { curveLoop }, ceilingType.Id, level.Id);

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Created ceiling (ID: {ceiling.Id.Value})", ["elementId"] = ceiling.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CreateRoof(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Roof"))
            {
                tx.Start();
                try
                {
                    var points = parameters["points"] as JArray;
                    var levelName = parameters["levelName"]?.ToString();
                    var roofTypeName = parameters["roofType"]?.ToString();
                    var slope = parameters["slope"]?.Value<double>() ?? 0;

                    if (points == null || points.Count < 3)
                        throw new InvalidOperationException("At least 3 boundary points required");

                    var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => l.Name == levelName);
                    if (level == null) throw new InvalidOperationException($"Level '{levelName}' not found");

                    var roofType = new FilteredElementCollector(doc).OfClass(typeof(RoofType)).Cast<RoofType>()
                        .FirstOrDefault(rt => !string.IsNullOrEmpty(roofTypeName) ? rt.Name == roofTypeName : true);
                    if (roofType == null) throw new InvalidOperationException("No roof type available");

                    var ca = new CurveArray();
                    for (int i = 0; i < points.Count; i++)
                    {
                        var p1 = points[i]; var p2 = points[(i + 1) % points.Count];
                        ca.Append(Line.CreateBound(
                            new XYZ(p1["x"].Value<double>(), p1["y"].Value<double>(), level.Elevation),
                            new XYZ(p2["x"].Value<double>(), p2["y"].Value<double>(), level.Elevation)));
                    }

                    var modelCurves = new ModelCurveArray();
                    var roof = doc.Create.NewFootPrintRoof(ca, level, roofType, out modelCurves);

                    if (slope > 0 && modelCurves != null)
                    {
                        foreach (ModelCurve mc in modelCurves)
                        {
                            roof.set_DefinesSlope(mc, true);
                            roof.set_SlopeAngle(mc, slope * Math.PI / 180.0);
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Created roof (ID: {roof.Id.Value})", ["elementId"] = roof.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CreateView(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create View"))
            {
                tx.Start();
                try
                {
                    var viewType = parameters["viewType"]?.ToString() ?? "FloorPlan";
                    var levelName = parameters["levelName"]?.ToString();
                    var viewName = parameters["viewName"]?.ToString();

                    Element newView = null;

                    switch (viewType)
                    {
                        case "FloorPlan":
                        case "CeilingPlan":
                        {
                            var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => l.Name == levelName);
                            if (level == null) throw new InvalidOperationException($"Level '{levelName}' not found");

                            var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                                .FirstOrDefault(v => v.ViewFamily == (viewType == "CeilingPlan" ? ViewFamily.CeilingPlan : ViewFamily.FloorPlan));
                            if (vft == null) throw new InvalidOperationException($"No {viewType} view family type found");

                            var plan = ViewPlan.Create(doc, vft.Id, level.Id);
                            if (!string.IsNullOrEmpty(viewName)) plan.Name = viewName;
                            newView = plan;
                            break;
                        }
                        case "3D":
                        {
                            var vft3d = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);
                            if (vft3d == null) throw new InvalidOperationException("No 3D view family type found");

                            var v3d = View3D.CreateIsometric(doc, vft3d.Id);
                            if (!string.IsNullOrEmpty(viewName)) v3d.Name = viewName;
                            newView = v3d;
                            break;
                        }
                        case "Drafting":
                        {
                            var vftD = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Drafting);
                            if (vftD == null) throw new InvalidOperationException("No Drafting view family type found");

                            var drafting = ViewDrafting.Create(doc, vftD.Id);
                            if (!string.IsNullOrEmpty(viewName)) drafting.Name = viewName;
                            newView = drafting;
                            break;
                        }
                        default:
                            throw new InvalidOperationException($"View type '{viewType}' — use create_section_views or create_elevation_views for Section/Elevation");
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Created {viewType} view (ID: {newView.Id.Value})", ["elementId"] = newView.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CreateSchedule(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Schedule"))
            {
                tx.Start();
                try
                {
                    var categoryName = parameters["category"]?.ToString();
                    var scheduleName = parameters["scheduleName"]?.ToString() ?? "New Schedule";
                    var fields = parameters["fields"] as JArray;

                    var cat = GetBuiltInCategory(categoryName);
                    var catId = new ElementId(cat);

                    var schedule = ViewSchedule.CreateSchedule(doc, catId);
                    schedule.Name = scheduleName;

                    // Add fields
                    if (fields != null)
                    {
                        var schedulableDefs = schedule.Definition.GetSchedulableFields();
                        foreach (var fieldName in fields)
                        {
                            var sf = schedulableDefs.FirstOrDefault(d => d.GetName(doc) == fieldName.ToString());
                            if (sf != null)
                                schedule.Definition.AddField(sf);
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Created schedule '{scheduleName}' (ID: {schedule.Id.Value})", ["elementId"] = schedule.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CreateTag(UIDocument uidoc, Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Tag"))
            {
                tx.Start();
                try
                {
                    var elementId = parameters["elementId"]?.Value<long>() ?? 0;
                    var offsetX = parameters["offsetX"]?.Value<double>() ?? 0;
                    var offsetY = parameters["offsetY"]?.Value<double>() ?? 0;
                    var withLeader = parameters["withLeader"]?.Value<bool>() ?? false;

                    var elem = doc.GetElement(new ElementId(elementId));
                    if (elem == null) throw new InvalidOperationException($"Element {elementId} not found");

                    var view = uidoc.ActiveView;
                    var bb = elem.get_BoundingBox(view);
                    if (bb == null) throw new InvalidOperationException("Element has no bounding box in current view");

                    var center = (bb.Min + bb.Max) / 2;
                    var tagPoint = new XYZ(center.X + offsetX, center.Y + offsetY, center.Z);

                    var tagRef = new Reference(elem);
                    var tag = IndependentTag.Create(doc, view.Id, tagRef, withLeader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, tagPoint);

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Tagged element {elementId} (Tag ID: {tag.Id.Value})", ["tagId"] = tag.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CreateDimensionCmd(UIDocument uidoc, Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Dimension"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    if (elementIds == null || elementIds.Count < 2)
                        throw new InvalidOperationException("At least 2 element IDs required");

                    var view = uidoc.ActiveView;
                    var refArray = new ReferenceArray();
                    XYZ p1 = null, p2 = null;

                    foreach (var idToken in elementIds)
                    {
                        var elem = doc.GetElement(new ElementId(idToken.Value<long>()));
                        if (elem == null) continue;

                        // Try to get a reference from the element
                        if (elem.Location is LocationPoint lp)
                        {
                            refArray.Append(new Reference(elem));
                            if (p1 == null) p1 = lp.Point;
                            else p2 = lp.Point;
                        }
                        else if (elem.Location is LocationCurve lc)
                        {
                            refArray.Append(new Reference(elem));
                            if (p1 == null) p1 = lc.Curve.GetEndPoint(0);
                            else p2 = lc.Curve.GetEndPoint(1);
                        }
                    }

                    if (refArray.Size < 2 || p1 == null || p2 == null)
                        throw new InvalidOperationException("Could not get references from elements");

                    var dimLine = Line.CreateBound(p1, p2);
                    var dim = doc.Create.NewDimension(view, dimLine, refArray);

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Created dimension (ID: {dim.Id.Value})", ["elementId"] = dim.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CreateTextNote(UIDocument uidoc, Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Text Note"))
            {
                tx.Start();
                try
                {
                    var text = parameters["text"]?.ToString() ?? "";
                    var x = parameters["x"]?.Value<double>() ?? 0;
                    var y = parameters["y"]?.Value<double>() ?? 0;
                    var textTypeName = parameters["textType"]?.ToString();

                    var view = uidoc.ActiveView;

                    var textTypeId = new FilteredElementCollector(doc)
                        .OfClass(typeof(TextNoteType))
                        .Cast<TextNoteType>()
                        .FirstOrDefault(t => string.IsNullOrEmpty(textTypeName) || t.Name == textTypeName)?.Id;

                    if (textTypeId == null) throw new InvalidOperationException("No text note type available");

                    var note = TextNote.Create(doc, view.Id, new XYZ(x, y, 0), text, textTypeId);

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Created text note (ID: {note.Id.Value})", ["elementId"] = note.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ████  DOCUMENTATION COMMANDS  ████
        // ══════════════════════════════════════════════════════════════

        private static JToken PlaceViewOnSheet(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Place View on Sheet"))
            {
                tx.Start();
                try
                {
                    var viewId = parameters["viewId"]?.Value<long>() ?? 0;
                    var sheetId = parameters["sheetId"]?.Value<long>() ?? 0;
                    var x = parameters["x"]?.Value<double>() ?? 0;
                    var y = parameters["y"]?.Value<double>() ?? 0;

                    // Allow sheetNumber+viewName as alternatives
                    if (sheetId == 0 && parameters["sheetNumber"] != null)
                    {
                        var sheetNum = parameters["sheetNumber"].ToString();
                        var sheet = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().FirstOrDefault(s => s.SheetNumber == sheetNum);
                        if (sheet != null) sheetId = sheet.Id.Value;
                    }

                    if (viewId == 0) throw new InvalidOperationException("viewId is required");
                    if (sheetId == 0) throw new InvalidOperationException("sheetId (or sheetNumber) is required");

                    var viewport = Viewport.Create(doc, new ElementId(sheetId), new ElementId(viewId), new XYZ(x, y, 0));

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Placed view on sheet (Viewport ID: {viewport.Id.Value})", ["viewportId"] = viewport.Id.Value };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken TagAllInView(UIDocument uidoc, Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Tag All In View"))
            {
                tx.Start();
                try
                {
                    var view = uidoc.ActiveView;
                    var categoryName = parameters["category"]?.ToString();
                    var withLeader = parameters["withLeader"]?.Value<bool>() ?? false;

                    var cat = GetBuiltInCategory(categoryName);
                    var elements = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .ToList();

                    int tagged = 0;
                    foreach (var elem in elements)
                    {
                        try
                        {
                            var bb = elem.get_BoundingBox(view);
                            if (bb == null) continue;
                            var center = (bb.Min + bb.Max) / 2;
                            var tagRef = new Reference(elem);
                            IndependentTag.Create(doc, view.Id, tagRef, withLeader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, center);
                            tagged++;
                        }
                        catch { /* skip elements that can't be tagged */ }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Tagged {tagged} of {elements.Count} {categoryName} element(s)", ["count"] = tagged };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ████  PROJECT SETTINGS COMMANDS  ████
        // ══════════════════════════════════════════════════════════════

        private static JToken ModifyObjectStyles(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Modify Object Styles"))
            {
                tx.Start();
                try
                {
                    var categoryName = parameters["category"]?.ToString();
                    var lineWeight = parameters["lineWeight"]?.Value<int>();
                    var colorR = parameters["colorR"]?.Value<int>();
                    var colorG = parameters["colorG"]?.Value<int>();
                    var colorB = parameters["colorB"]?.Value<int>();
                    var subcategoryName = parameters["subcategory"]?.ToString();

                    var bic = GetBuiltInCategory(categoryName);
                    var cat = doc.Settings.Categories.get_Item(bic);
                    if (cat == null) throw new InvalidOperationException($"Category '{categoryName}' not found in project");

                    Category target = cat;
                    if (!string.IsNullOrEmpty(subcategoryName))
                    {
                        target = cat.SubCategories.Cast<Category>().FirstOrDefault(sc => sc.Name == subcategoryName);
                        if (target == null) throw new InvalidOperationException($"Subcategory '{subcategoryName}' not found");
                    }

                    if (lineWeight.HasValue)
                        target.SetLineWeight(lineWeight.Value, GraphicsStyleType.Projection);

                    if (colorR.HasValue && colorG.HasValue && colorB.HasValue)
                        target.LineColor = new Color((byte)colorR.Value, (byte)colorG.Value, (byte)colorB.Value);

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Modified object styles for '{categoryName}'" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken SetPhase(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Set Phase"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    var phaseName = parameters["phaseName"]?.ToString();

                    if (elementIds == null || string.IsNullOrEmpty(phaseName))
                        throw new InvalidOperationException("elementIds and phaseName are required");

                    // Find phase by name
                    Phase targetPhase = null;
                    foreach (Phase ph in doc.Phases)
                    {
                        if (ph.Name == phaseName) { targetPhase = ph; break; }
                    }
                    if (targetPhase == null)
                    {
                        var names = new List<string>();
                        foreach (Phase ph in doc.Phases) names.Add(ph.Name);
                        throw new InvalidOperationException($"Phase '{phaseName}' not found. Available: {string.Join(", ", names)}");
                    }

                    int modified = 0;
                    foreach (var idToken in elementIds)
                    {
                        var elem = doc.GetElement(new ElementId(idToken.Value<long>()));
                        if (elem == null) continue;

                        // Try phase created
                        var phCreated = elem.get_Parameter(BuiltInParameter.PHASE_CREATED);
                        if (phCreated != null && !phCreated.IsReadOnly)
                        {
                            phCreated.Set(targetPhase.Id);
                            modified++;
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Set phase to '{phaseName}' on {modified} element(s)", ["count"] = modified };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken GetPhases(Document doc)
        {
            var result = new JArray();
            foreach (Phase ph in doc.Phases)
            {
                result.Add(new JObject
                {
                    ["id"] = ph.Id.Value,
                    ["name"] = ph.Name
                });
            }
            return new JObject { ["phases"] = result, ["count"] = result.Count };
        }

        private static JToken GetMaterials(Document doc)
        {
            var materials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .OrderBy(m => m.Name)
                .Take(200)
                .Select(m => new JObject
                {
                    ["id"] = m.Id.Value,
                    ["name"] = m.Name,
                    ["color"] = m.Color != null && m.Color.IsValid ? $"#{m.Color.Red:X2}{m.Color.Green:X2}{m.Color.Blue:X2}" : "(none)",
                    ["transparency"] = m.Transparency
                });

            var arr = new JArray(materials);
            return new JObject { ["materials"] = arr, ["count"] = arr.Count };
        }

        private static JToken SetMaterial(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Set Material"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    var materialName = parameters["materialName"]?.ToString();
                    var paramName = parameters["parameterName"]?.ToString();

                    if (elementIds == null || string.IsNullOrEmpty(materialName))
                        throw new InvalidOperationException("elementIds and materialName are required");

                    // Find material
                    var mat = new FilteredElementCollector(doc)
                        .OfClass(typeof(Material))
                        .Cast<Material>()
                        .FirstOrDefault(m => m.Name == materialName);
                    if (mat == null)
                        throw new InvalidOperationException($"Material '{materialName}' not found");

                    int modified = 0;
                    foreach (var idToken in elementIds)
                    {
                        var elem = doc.GetElement(new ElementId(idToken.Value<long>()));
                        if (elem == null) continue;

                        // Try specific parameter name first, then common material parameters
                        bool set = false;
                        var paramNames = !string.IsNullOrEmpty(paramName)
                            ? new[] { paramName }
                            : new[] { "Material", "Structural Material", "Interior Finish", "Exterior Finish" };

                        foreach (var pn in paramNames)
                        {
                            foreach (Parameter p in elem.Parameters)
                            {
                                if (p.Definition.Name == pn && !p.IsReadOnly && p.StorageType == StorageType.ElementId)
                                {
                                    p.Set(mat.Id);
                                    set = true;
                                    modified++;
                                    break;
                                }
                            }
                            if (set) break;
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Set material '{materialName}' on {modified} element(s)" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken SetViewProperties(UIDocument uidoc, Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Set View Properties"))
            {
                tx.Start();
                try
                {
                    var viewId = parameters["viewId"]?.Value<long>();
                    View view;
                    if (viewId.HasValue)
                        view = doc.GetElement(new ElementId(viewId.Value)) as View;
                    else
                        view = uidoc.ActiveView;

                    if (view == null) throw new InvalidOperationException("View not found");

                    var changes = new List<string>();

                    // Scale
                    var scale = parameters["scale"]?.Value<int>();
                    if (scale.HasValue)
                    {
                        view.Scale = scale.Value;
                        changes.Add($"Scale → 1:{scale.Value}");
                    }

                    // Detail Level
                    var detailLevel = parameters["detailLevel"]?.ToString();
                    if (!string.IsNullOrEmpty(detailLevel))
                    {
                        if (Enum.TryParse<ViewDetailLevel>(detailLevel, true, out var vdl))
                        {
                            view.DetailLevel = vdl;
                            changes.Add($"Detail Level → {vdl}");
                        }
                    }

                    // Visual Style / Display Style
                    var displayStyle = parameters["displayStyle"]?.ToString() ?? parameters["visualStyle"]?.ToString();
                    if (!string.IsNullOrEmpty(displayStyle))
                    {
                        if (Enum.TryParse<DisplayStyle>(displayStyle, true, out var ds))
                        {
                            view.DisplayStyle = ds;
                            changes.Add($"Display Style → {ds}");
                        }
                    }

                    // Discipline
                    var discipline = parameters["discipline"]?.ToString();
                    if (!string.IsNullOrEmpty(discipline) && view is ViewPlan viewPlan)
                    {
                        if (Enum.TryParse<ViewDiscipline>(discipline, true, out var vd))
                        {
                            viewPlan.Discipline = vd;
                            changes.Add($"Discipline → {vd}");
                        }
                    }

                    // Phase
                    var phaseName = parameters["phaseName"]?.ToString();
                    if (!string.IsNullOrEmpty(phaseName))
                    {
                        foreach (Phase ph in doc.Phases)
                        {
                            if (ph.Name == phaseName)
                            {
                                var phaseParam = view.get_Parameter(BuiltInParameter.VIEW_PHASE);
                                if (phaseParam != null && !phaseParam.IsReadOnly)
                                {
                                    phaseParam.Set(ph.Id);
                                    changes.Add($"Phase → {phaseName}");
                                }
                                break;
                            }
                        }
                    }

                    // View Name
                    var viewName = parameters["viewName"]?.ToString();
                    if (!string.IsNullOrEmpty(viewName))
                    {
                        view.Name = viewName;
                        changes.Add($"Name → {viewName}");
                    }

                    // Crop Box
                    var showCropBox = parameters["showCropBox"]?.Value<bool>();
                    if (showCropBox.HasValue)
                    {
                        view.CropBoxActive = showCropBox.Value;
                        view.CropBoxVisible = showCropBox.Value;
                        changes.Add($"Crop Box → {(showCropBox.Value ? "On" : "Off")}");
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Updated view: {string.Join(", ", changes)}", ["changes"] = changes.Count };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken OverrideElementInView(UIDocument uidoc, Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Override Element In View"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    var colorR = parameters["colorR"]?.Value<int>() ?? parameters["r"]?.Value<int>();
                    var colorG = parameters["colorG"]?.Value<int>() ?? parameters["g"]?.Value<int>();
                    var colorB = parameters["colorB"]?.Value<int>() ?? parameters["b"]?.Value<int>();
                    var lineWeight = parameters["lineWeight"]?.Value<int>();
                    var transparency = parameters["transparency"]?.Value<int>();
                    var halfTone = parameters["halftone"]?.Value<bool>();
                    var visible = parameters["visible"]?.Value<bool>();

                    if (elementIds == null || elementIds.Count == 0)
                        throw new InvalidOperationException("elementIds is required");

                    var view = uidoc.ActiveView;
                    var ogs = new OverrideGraphicSettings();

                    if (colorR.HasValue && colorG.HasValue && colorB.HasValue)
                    {
                        var color = new Color((byte)colorR.Value, (byte)colorG.Value, (byte)colorB.Value);
                        ogs.SetProjectionLineColor(color);
                        ogs.SetSurfaceForegroundPatternColor(color);
                        ogs.SetSurfaceForegroundPatternVisible(true);

                        var solidFill = new FilteredElementCollector(doc)
                            .OfClass(typeof(FillPatternElement))
                            .Cast<FillPatternElement>()
                            .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);
                        if (solidFill != null)
                            ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                    }

                    if (lineWeight.HasValue)
                        ogs.SetProjectionLineWeight(lineWeight.Value);
                    if (transparency.HasValue)
                        ogs.SetSurfaceTransparency(transparency.Value);
                    if (halfTone.HasValue)
                        ogs.SetHalftone(halfTone.Value);

                    int count = 0;
                    foreach (var idToken in elementIds)
                    {
                        var eid = new ElementId(idToken.Value<long>());
                        if (visible.HasValue && !visible.Value)
                            view.HideElements(new List<ElementId> { eid });
                        else
                            view.SetElementOverrides(eid, ogs);
                        count++;
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Applied graphic overrides to {count} element(s)", ["count"] = count };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken GetLineStyles(Document doc)
        {
            var result = new JArray();
            var linesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            if (linesCat != null)
            {
                foreach (Category subCat in linesCat.SubCategories)
                {
                    var gs = subCat.GetGraphicsStyle(GraphicsStyleType.Projection);
                    result.Add(new JObject
                    {
                        ["id"] = gs?.Id.Value ?? -1,
                        ["name"] = subCat.Name,
                        ["lineWeight"] = subCat.GetLineWeight(GraphicsStyleType.Projection) ?? -1,
                        ["color"] = subCat.LineColor != null && subCat.LineColor.IsValid
                            ? $"#{subCat.LineColor.Red:X2}{subCat.LineColor.Green:X2}{subCat.LineColor.Blue:X2}" : "(default)"
                    });
                }
            }
            return new JObject { ["lineStyles"] = result, ["count"] = result.Count };
        }

        private static JToken SetLineStyle(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Set Line Style"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    var lineStyleName = parameters["lineStyleName"]?.ToString();

                    if (elementIds == null || string.IsNullOrEmpty(lineStyleName))
                        throw new InvalidOperationException("elementIds and lineStyleName are required");

                    // Find line style
                    var linesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                    GraphicsStyle targetStyle = null;
                    foreach (Category subCat in linesCat.SubCategories)
                    {
                        if (subCat.Name == lineStyleName)
                        {
                            targetStyle = subCat.GetGraphicsStyle(GraphicsStyleType.Projection);
                            break;
                        }
                    }
                    if (targetStyle == null)
                        throw new InvalidOperationException($"Line style '{lineStyleName}' not found");

                    int modified = 0;
                    foreach (var idToken in elementIds)
                    {
                        var elem = doc.GetElement(new ElementId(idToken.Value<long>()));
                        if (elem is CurveElement ce)
                        {
                            ce.LineStyle = targetStyle;
                            modified++;
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Set line style '{lineStyleName}' on {modified} element(s)" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ████  POWER TOOLS  ████
        // ══════════════════════════════════════════════════════════════

        // ── Geometry ─────────────────────────────────────────────────

        private static JToken AutoJoinElements(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Auto Join Elements"))
            {
                tx.Start();
                try
                {
                    var cat1 = parameters["category1"]?.ToString() ?? "Walls";
                    var cat2 = parameters["category2"]?.ToString() ?? "Floors";

                    var bic1 = GetBuiltInCategory(cat1);
                    var bic2 = GetBuiltInCategory(cat2);

                    var elems1 = new FilteredElementCollector(doc).OfCategory(bic1).WhereElementIsNotElementType().ToList();
                    var elems2 = new FilteredElementCollector(doc).OfCategory(bic2).WhereElementIsNotElementType().ToList();

                    int joined = 0;
                    foreach (var e1 in elems1)
                    {
                        foreach (var e2 in elems2)
                        {
                            try
                            {
                                if (!JoinGeometryUtils.AreElementsJoined(doc, e1, e2))
                                {
                                    JoinGeometryUtils.JoinGeometry(doc, e1, e2);
                                    joined++;
                                }
                            }
                            catch { /* skip incompatible pairs */ }
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Joined {joined} element pairs ({cat1} ↔ {cat2})", ["count"] = joined };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken ReassignLevel(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Reassign Level"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    var targetLevelName = parameters["targetLevel"]?.ToString();
                    var maintainOffset = parameters["maintainOffset"]?.Value<bool>() ?? true;

                    if (elementIds == null || string.IsNullOrEmpty(targetLevelName))
                        throw new InvalidOperationException("elementIds and targetLevel are required");

                    var targetLevel = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => l.Name == targetLevelName);
                    if (targetLevel == null) throw new InvalidOperationException($"Level '{targetLevelName}' not found");

                    int modified = 0;
                    foreach (var idToken in elementIds)
                    {
                        var elem = doc.GetElement(new ElementId(idToken.Value<long>()));
                        if (elem == null) continue;

                        var levelParam = elem.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                            ?? elem.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                            ?? elem.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);

                        if (levelParam != null && !levelParam.IsReadOnly)
                        {
                            if (maintainOffset)
                            {
                                var oldLevelId = levelParam.AsElementId();
                                var oldLevel = doc.GetElement(oldLevelId) as Level;
                                if (oldLevel != null)
                                {
                                    var offsetParam = elem.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)
                                        ?? elem.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                                    if (offsetParam != null && !offsetParam.IsReadOnly)
                                    {
                                        var currentOffset = offsetParam.AsDouble();
                                        offsetParam.Set(currentOffset + oldLevel.Elevation - targetLevel.Elevation);
                                    }
                                }
                            }
                            levelParam.Set(targetLevel.Id);
                            modified++;
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Reassigned {modified} element(s) to '{targetLevelName}'" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken BatchModifyThickness(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Batch Modify Thickness"))
            {
                tx.Start();
                try
                {
                    var categoryName = parameters["category"]?.ToString() ?? "Walls";
                    var typeName = parameters["typeName"]?.ToString();
                    var thickness = parameters["thickness"]?.Value<double>();

                    if (string.IsNullOrEmpty(typeName) || !thickness.HasValue)
                        throw new InvalidOperationException("typeName and thickness are required");

                    // Find the type and modify its compound structure
                    var bic = GetBuiltInCategory(categoryName);
                    Element typeElem = null;

                    if (categoryName.Equals("Walls", StringComparison.OrdinalIgnoreCase))
                    {
                        typeElem = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>()
                            .FirstOrDefault(t => t.Name == typeName);
                    }
                    else if (categoryName.Equals("Floors", StringComparison.OrdinalIgnoreCase))
                    {
                        typeElem = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>()
                            .FirstOrDefault(t => t.Name == typeName);
                    }

                    if (typeElem == null)
                        throw new InvalidOperationException($"Type '{typeName}' not found in {categoryName}");

                    // Try to set the width parameter
                    var widthParam = typeElem.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
                    if (widthParam != null && !widthParam.IsReadOnly)
                    {
                        widthParam.Set(thickness.Value);
                        tx.Commit();
                        return new JObject { ["message"] = $"✅ Set {typeName} thickness to {thickness.Value} feet" };
                    }

                    // For compound types, scale layers proportionally
                    if (typeElem is WallType wt && wt.GetCompoundStructure() != null)
                    {
                        var cs = wt.GetCompoundStructure();
                        var currentWidth = cs.GetWidth();
                        var scale = thickness.Value / currentWidth;
                        for (int i = 0; i < cs.LayerCount; i++)
                            cs.SetLayerWidth(i, cs.GetLayerWidth(i) * scale);
                        wt.SetCompoundStructure(cs);
                        tx.Commit();
                        return new JObject { ["message"] = $"✅ Scaled {typeName} compound layers to {thickness.Value} feet total" };
                    }

                    tx.RollBack();
                    return new JObject { ["message"] = $"⚠️ Could not modify thickness for type '{typeName}'" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken RoomToFloor(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Room to Floor"))
            {
                tx.Start();
                try
                {
                    var roomIds = parameters["roomIds"] as JArray;
                    var floorTypeName = parameters["floorType"]?.ToString();

                    IList<Room> rooms;
                    if (roomIds != null)
                    {
                        rooms = roomIds.Select(id => doc.GetElement(new ElementId(id.Value<long>())) as Room).Where(r => r != null).ToList();
                    }
                    else
                    {
                        rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().Cast<Room>().Where(r => r.Area > 0).ToList();
                    }

                    var floorType = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>()
                        .FirstOrDefault(ft => !string.IsNullOrEmpty(floorTypeName) ? ft.Name == floorTypeName : true);
                    if (floorType == null) throw new InvalidOperationException("No floor type available");

                    int created = 0;
                    foreach (var room in rooms)
                    {
                        try
                        {
                            var options = new SpatialElementBoundaryOptions();
                            var boundaries = room.GetBoundarySegments(options);
                            if (boundaries == null || boundaries.Count == 0) continue;

                            var curveLoop = new CurveLoop();
                            foreach (var seg in boundaries[0])
                                curveLoop.Append(seg.GetCurve());

                            var levelId = room.LevelId;
                            Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorType.Id, levelId);
                            created++;
                        }
                        catch { /* skip rooms that fail */ }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Created {created} floor(s) from room boundaries", ["count"] = created };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        // ── Data & Parameters ────────────────────────────────────────

        private static JToken FindReplaceNames(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Find Replace Names"))
            {
                tx.Start();
                try
                {
                    var find = parameters["find"]?.ToString();
                    var replace = parameters["replace"]?.ToString() ?? "";
                    var scope = parameters["scope"]?.ToString() ?? "Types";

                    if (string.IsNullOrEmpty(find))
                        throw new InvalidOperationException("'find' text is required");

                    int renamed = 0;

                    if (scope == "Types" || scope == "All")
                    {
                        var allTypes = new FilteredElementCollector(doc).WhereElementIsElementType().ToList();
                        foreach (var t in allTypes)
                        {
                            if (t.Name.Contains(find))
                            {
                                try { t.Name = t.Name.Replace(find, replace); renamed++; } catch { }
                            }
                        }
                    }

                    if (scope == "Views" || scope == "All")
                    {
                        var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate).ToList();
                        foreach (var v in views)
                        {
                            if (v.Name.Contains(find))
                            {
                                try { v.Name = v.Name.Replace(find, replace); renamed++; } catch { }
                            }
                        }
                    }

                    if (scope == "Sheets" || scope == "All")
                    {
                        var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().ToList();
                        foreach (var s in sheets)
                        {
                            if (s.Name.Contains(find))
                            {
                                try { s.Name = s.Name.Replace(find, replace); renamed++; } catch { }
                            }
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Replaced '{find}' → '{replace}' in {renamed} name(s)", ["count"] = renamed };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken ParameterCaseConvert(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Parameter Case Convert"))
            {
                tx.Start();
                try
                {
                    var categoryName = parameters["category"]?.ToString();
                    var paramName = parameters["parameterName"]?.ToString();
                    var caseType = parameters["caseType"]?.ToString() ?? "Title";

                    if (string.IsNullOrEmpty(paramName))
                        throw new InvalidOperationException("parameterName is required");

                    var bic = GetBuiltInCategory(categoryName);
                    var elements = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToList();

                    int modified = 0;
                    foreach (var elem in elements)
                    {
                        foreach (Parameter p in elem.Parameters)
                        {
                            if (p.Definition.Name == paramName && !p.IsReadOnly && p.StorageType == StorageType.String)
                            {
                                var val = p.AsString();
                                if (string.IsNullOrEmpty(val)) break;
                                string newVal;
                                switch (caseType)
                                {
                                    case "UPPER": newVal = val.ToUpper(); break;
                                    case "lower": newVal = val.ToLower(); break;
                                    case "Title": newVal = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(val.ToLower()); break;
                                    default: newVal = val; break;
                                }
                                if (newVal != val) { p.Set(newVal); modified++; }
                                break;
                            }
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Converted '{paramName}' to {caseType} case on {modified} element(s)" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken BulkParameterTransfer(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Bulk Parameter Transfer"))
            {
                tx.Start();
                try
                {
                    var categoryName = parameters["category"]?.ToString();
                    var sourceParam = parameters["sourceParameter"]?.ToString();
                    var targetParam = parameters["targetParameter"]?.ToString();

                    if (string.IsNullOrEmpty(sourceParam) || string.IsNullOrEmpty(targetParam))
                        throw new InvalidOperationException("sourceParameter and targetParameter are required");

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
                    return new JObject { ["message"] = $"✅ Transferred '{sourceParam}' → '{targetParam}' on {transferred} element(s)" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken AutoRenumber(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Auto Renumber"))
            {
                tx.Start();
                try
                {
                    var categoryName = parameters["category"]?.ToString() ?? "Rooms";
                    var prefix = parameters["prefix"]?.ToString() ?? "";
                    var startNumber = parameters["startNumber"]?.Value<int>() ?? 1;
                    var sortBy = parameters["sortBy"]?.ToString() ?? "Location";
                    var paramName = parameters["parameterName"]?.ToString() ?? "Number";

                    var bic = GetBuiltInCategory(categoryName);
                    var elements = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToList();

                    // Sort by location
                    if (sortBy == "Location")
                    {
                        elements = elements.OrderBy(e =>
                        {
                            var bb = e.get_BoundingBox(null);
                            if (bb == null) return 0.0;
                            return bb.Min.Y * 10000 + bb.Min.X;
                        }).ToList();
                    }
                    else if (sortBy == "Name")
                    {
                        elements = elements.OrderBy(e => e.Name).ToList();
                    }

                    int numbered = 0;
                    foreach (var elem in elements)
                    {
                        foreach (Parameter p in elem.Parameters)
                        {
                            if (p.Definition.Name == paramName && !p.IsReadOnly && p.StorageType == StorageType.String)
                            {
                                p.Set($"{prefix}{startNumber + numbered}");
                                numbered++;
                                break;
                            }
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Renumbered {numbered} {categoryName} ({prefix}{startNumber} → {prefix}{startNumber + numbered - 1})" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        // ── Project Cleanup ──────────────────────────────────────────

        private static JToken DeepPurge(Document doc)
        {
            int totalPurged = 0;
            int passes = 0;

            // Multiple passes since purging one item may free others
            while (passes < 5)
            {
                var purgable = doc.GetUnusedElements(new HashSet<ElementId>());
                if (purgable == null || purgable.Count == 0) break;

                using (var tx = new Transaction(doc, $"Deep Purge Pass {passes + 1}"))
                {
                    tx.Start();
                    try
                    {
                        doc.Delete(purgable);
                        totalPurged += purgable.Count;
                        tx.Commit();
                    }
                    catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); break; }
                }
                passes++;
            }

            return new JObject { ["message"] = $"✅ Purged {totalPurged} unused element(s) in {passes} pass(es)", ["purged"] = totalPurged };
        }

        private static JToken DeleteEmptyGroups(Document doc)
        {
            using (var tx = new Transaction(doc, "Delete Empty Groups"))
            {
                tx.Start();
                try
                {
                    var groups = new FilteredElementCollector(doc).OfClass(typeof(Group)).Cast<Group>().ToList();
                    var emptyIds = new List<ElementId>();

                    foreach (var g in groups)
                    {
                        var members = g.GetMemberIds();
                        if (members == null || members.Count == 0)
                            emptyIds.Add(g.Id);
                    }

                    // Also find unused group types
                    var groupTypes = new FilteredElementCollector(doc).OfClass(typeof(GroupType)).Cast<GroupType>().ToList();
                    foreach (var gt in groupTypes)
                    {
                        var instances = new FilteredElementCollector(doc).OfClass(typeof(Group)).Cast<Group>().Where(g => g.GroupType.Id == gt.Id).ToList();
                        if (instances.Count == 0)
                            emptyIds.Add(gt.Id);
                    }

                    if (emptyIds.Count > 0)
                        doc.Delete(emptyIds);

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Deleted {emptyIds.Count} empty group(s)/type(s)", ["count"] = emptyIds.Count };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken FindCadImports(Document doc, JObject parameters)
        {
            var deleteFound = parameters["delete"]?.Value<bool>() ?? false;

            var imports = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).Cast<ImportInstance>().ToList();

            var result = new JArray();
            foreach (var imp in imports)
            {
                var bb = imp.get_BoundingBox(null);
                result.Add(new JObject
                {
                    ["id"] = imp.Id.Value,
                    ["name"] = imp.Name,
                    ["isLinked"] = imp.IsLinked,
                    ["pinned"] = imp.Pinned,
                    ["visible"] = imp.get_BoundingBox(null) != null
                });
            }

            if (deleteFound && imports.Count > 0)
            {
                using (var tx = new Transaction(doc, "Delete CAD Imports"))
                {
                    tx.Start();
                    doc.Delete(imports.Where(i => !i.IsLinked).Select(i => i.Id).ToList());
                    tx.Commit();
                }
                return new JObject { ["message"] = $"✅ Found {imports.Count} CAD import(s), deleted {imports.Count(i => !i.IsLinked)} non-linked", ["imports"] = result };
            }

            return new JObject { ["message"] = $"Found {imports.Count} CAD import(s)", ["imports"] = result, ["count"] = imports.Count };
        }

        // ── Selection & Filtering ────────────────────────────────────

        private static JToken SelectByParameter(UIDocument uidoc, Document doc, JObject parameters)
        {
            var categoryName = parameters["category"]?.ToString();
            var paramName = parameters["parameterName"]?.ToString();
            var paramValue = parameters["value"]?.ToString();

            if (string.IsNullOrEmpty(paramName))
                throw new InvalidOperationException("parameterName is required");

            var bic = GetBuiltInCategory(categoryName);
            var elements = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToList();

            var matching = new List<ElementId>();
            foreach (var elem in elements)
            {
                foreach (Parameter p in elem.Parameters)
                {
                    if (p.Definition.Name == paramName)
                    {
                        var val = p.AsValueString() ?? p.AsString() ?? "";
                        if (paramValue == null || val == paramValue || val.Contains(paramValue))
                        {
                            matching.Add(elem.Id);
                        }
                        break;
                    }
                }
            }

            uidoc.Selection.SetElementIds(matching);
            return new JObject
            {
                ["message"] = $"✅ Selected {matching.Count} element(s) where '{paramName}' = '{paramValue}'",
                ["count"] = matching.Count,
                ["elementIds"] = new JArray(matching.Select(id => id.Value))
            };
        }

        private static JToken SelectByWorkset(UIDocument uidoc, Document doc, JObject parameters)
        {
            var worksetName = parameters["worksetName"]?.ToString();
            if (string.IsNullOrEmpty(worksetName))
                throw new InvalidOperationException("worksetName is required");

            if (!doc.IsWorkshared)
                throw new InvalidOperationException("Document is not workshared");

            var worksets = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToWorksets();
            var targetWorkset = worksets.FirstOrDefault(w => w.Name == worksetName);
            if (targetWorkset == null)
                throw new InvalidOperationException($"Workset '{worksetName}' not found");

            var wsFilter = new ElementWorksetFilter(targetWorkset.Id);
            var matching = new FilteredElementCollector(doc).WherePasses(wsFilter).WhereElementIsNotElementType().Select(e => e.Id).ToList();

            uidoc.Selection.SetElementIds(matching);
            return new JObject
            {
                ["message"] = $"✅ Selected {matching.Count} element(s) on workset '{worksetName}'",
                ["count"] = matching.Count
            };
        }

        private static JToken FilterSelection(UIDocument uidoc, Document doc, JObject parameters)
        {
            var categoryName = parameters["category"]?.ToString();
            var levelName = parameters["levelName"]?.ToString();

            var currentSelection = uidoc.Selection.GetElementIds().ToList();
            if (currentSelection.Count == 0)
                return new JObject { ["message"] = "⚠️ No elements currently selected" };

            var filtered = new List<ElementId>();
            foreach (var eid in currentSelection)
            {
                var elem = doc.GetElement(eid);
                if (elem == null) continue;

                bool matchCategory = true, matchLevel = true;

                if (!string.IsNullOrEmpty(categoryName))
                {
                    var bic = GetBuiltInCategory(categoryName);
                    matchCategory = elem.Category?.BuiltInCategory == bic;
                }

                if (!string.IsNullOrEmpty(levelName))
                {
                    var levelParam = elem.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                        ?? elem.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                        ?? elem.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                    if (levelParam != null)
                    {
                        var lvl = doc.GetElement(levelParam.AsElementId()) as Level;
                        matchLevel = lvl?.Name == levelName;
                    }
                }

                if (matchCategory && matchLevel)
                    filtered.Add(eid);
            }

            uidoc.Selection.SetElementIds(filtered);
            return new JObject
            {
                ["message"] = $"✅ Filtered selection: {filtered.Count} of {currentSelection.Count} element(s) match",
                ["count"] = filtered.Count,
                ["original"] = currentSelection.Count
            };
        }

        private static JToken CategoryToWorkset(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Category to Workset"))
            {
                tx.Start();
                try
                {
                    var mappings = parameters["mappings"] as JArray;
                    if (mappings == null)
                        throw new InvalidOperationException("'mappings' array required with {category, worksetName} objects");

                    if (!doc.IsWorkshared)
                        throw new InvalidOperationException("Document is not workshared");

                    var worksets = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToWorksets().ToDictionary(w => w.Name);

                    int totalMoved = 0;
                    foreach (var mapping in mappings)
                    {
                        var catName = mapping["category"]?.ToString();
                        var wsName = mapping["worksetName"]?.ToString();
                        if (string.IsNullOrEmpty(catName) || string.IsNullOrEmpty(wsName)) continue;

                        if (!worksets.TryGetValue(wsName, out var ws))
                            continue;

                        var bic = GetBuiltInCategory(catName);
                        var elements = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToList();

                        foreach (var elem in elements)
                        {
                            var wsParam = elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                            if (wsParam != null && !wsParam.IsReadOnly)
                            {
                                wsParam.Set(ws.Id.IntegerValue);
                                totalMoved++;
                            }
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Migrated {totalMoved} element(s) to worksets", ["count"] = totalMoved };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ████  REMAINING ADVANCED TOOLS  ████
        // ══════════════════════════════════════════════════════════════

        private static JToken InverseSelection(UIDocument uidoc, Document doc)
        {
            var currentIds = uidoc.Selection.GetElementIds();
            var allVisible = new FilteredElementCollector(doc, uidoc.ActiveView.Id)
                .WhereElementIsNotElementType()
                .Select(e => e.Id)
                .ToList();

            var inverse = allVisible.Where(id => !currentIds.Contains(id)).ToList();
            uidoc.Selection.SetElementIds(inverse);

            return new JObject
            {
                ["message"] = $"✅ Inverted selection: {currentIds.Count} → {inverse.Count} element(s)",
                ["previousCount"] = currentIds.Count,
                ["newCount"] = inverse.Count
            };
        }

        private static JToken CopyFromLinked(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Copy From Linked"))
            {
                tx.Start();
                try
                {
                    var categoryName = parameters["category"]?.ToString();
                    var linkName = parameters["linkName"]?.ToString();

                    // Find linked instance
                    var links = new FilteredElementCollector(doc)
                        .OfClass(typeof(RevitLinkInstance))
                        .Cast<RevitLinkInstance>()
                        .ToList();

                    RevitLinkInstance targetLink = null;
                    if (!string.IsNullOrEmpty(linkName))
                        targetLink = links.FirstOrDefault(l => l.Name.Contains(linkName));
                    else if (links.Count > 0)
                        targetLink = links[0];

                    if (targetLink == null)
                    {
                        var names = links.Select(l => l.Name);
                        throw new InvalidOperationException($"Linked model not found. Available: {string.Join(", ", names)}");
                    }

                    var linkDoc = targetLink.GetLinkDocument();
                    if (linkDoc == null)
                        throw new InvalidOperationException("Linked document is not loaded");

                    var transform = targetLink.GetTotalTransform();

                    // Collect elements from linked doc
                    IList<ElementId> sourceIds;
                    if (!string.IsNullOrEmpty(categoryName))
                    {
                        var bic = GetBuiltInCategory(categoryName);
                        sourceIds = new FilteredElementCollector(linkDoc)
                            .OfCategory(bic)
                            .WhereElementIsNotElementType()
                            .Select(e => e.Id)
                            .ToList();
                    }
                    else
                    {
                        throw new InvalidOperationException("'category' is required to filter elements to copy");
                    }

                    if (sourceIds.Count == 0)
                        throw new InvalidOperationException($"No {categoryName} elements found in linked model");

                    // Cap at reasonable number
                    if (sourceIds.Count > 500)
                        sourceIds = sourceIds.Take(500).ToList();

                    var copied = ElementTransformUtils.CopyElements(linkDoc, sourceIds, doc, transform, new CopyPasteOptions());

                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"✅ Copied {copied.Count} {categoryName} element(s) from '{targetLink.Name}'",
                        ["count"] = copied.Count
                    };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CropRegionSync(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Crop Region Sync"))
            {
                tx.Start();
                try
                {
                    var sourceViewId = parameters["sourceViewId"]?.Value<long>() ?? 0;
                    var targetViewIds = parameters["targetViewIds"] as JArray;

                    if (sourceViewId == 0 || targetViewIds == null)
                        throw new InvalidOperationException("sourceViewId and targetViewIds are required");

                    var sourceView = doc.GetElement(new ElementId(sourceViewId)) as View;
                    if (sourceView == null) throw new InvalidOperationException("Source view not found");
                    if (!sourceView.CropBoxActive) throw new InvalidOperationException("Source view has no active crop box");

                    var cropBox = sourceView.CropBox;
                    int synced = 0;

                    foreach (var idToken in targetViewIds)
                    {
                        var targetView = doc.GetElement(new ElementId(idToken.Value<long>())) as View;
                        if (targetView == null) continue;

                        targetView.CropBoxActive = true;
                        targetView.CropBox = cropBox;
                        targetView.CropBoxVisible = sourceView.CropBoxVisible;
                        synced++;
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Synced crop region to {synced} view(s)" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken ApplyViewTemplate(UIDocument uidoc, Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Apply View Template"))
            {
                tx.Start();
                try
                {
                    var templateName = parameters["templateName"]?.ToString();
                    var viewIds = parameters["viewIds"] as JArray;

                    if (string.IsNullOrEmpty(templateName))
                        throw new InvalidOperationException("templateName is required");

                    // Find template
                    var template = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .FirstOrDefault(v => v.IsTemplate && v.Name == templateName);

                    if (template == null)
                    {
                        var templates = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => v.IsTemplate).Select(v => v.Name).Take(20);
                        throw new InvalidOperationException($"Template '{templateName}' not found. Available: {string.Join(", ", templates)}");
                    }

                    IList<View> targetViews;
                    if (viewIds != null)
                    {
                        targetViews = viewIds.Select(id => doc.GetElement(new ElementId(id.Value<long>())) as View).Where(v => v != null && !v.IsTemplate).ToList();
                    }
                    else
                    {
                        targetViews = new List<View> { uidoc.ActiveView };
                    }

                    int applied = 0;
                    foreach (var view in targetViews)
                    {
                        view.ViewTemplateId = template.Id;
                        applied++;
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Applied template '{templateName}' to {applied} view(s)" };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken ResolveWarnings(Document doc, JObject parameters)
        {
            var action = parameters["action"]?.ToString() ?? "list";
            var warningType = parameters["warningType"]?.ToString();

            var warnings = doc.GetWarnings();
            var warningList = new JArray();

            var grouped = new Dictionary<string, List<FailureMessage>>();
            foreach (var w in warnings)
            {
                var desc = w.GetDescriptionText();
                if (!grouped.ContainsKey(desc)) grouped[desc] = new List<FailureMessage>();
                grouped[desc].Add(w);
            }

            foreach (var kvp in grouped)
            {
                warningList.Add(new JObject
                {
                    ["description"] = kvp.Key,
                    ["count"] = kvp.Value.Count,
                    ["elementIds"] = new JArray(kvp.Value.SelectMany(w => w.GetFailingElements()).Select(id => id.Value).Distinct().Take(20))
                });
            }

            if (action == "list")
            {
                return new JObject
                {
                    ["message"] = $"Found {warnings.Count} warning(s) in {grouped.Count} group(s)",
                    ["warnings"] = warningList,
                    ["totalCount"] = warnings.Count
                };
            }

            // Auto-resolve: handle common fixable warnings
            if (action == "resolve")
            {
                int resolved = 0;
                using (var tx = new Transaction(doc, "Resolve Warnings"))
                {
                    tx.Start();
                    try
                    {
                        foreach (var w in warnings)
                        {
                            var desc = w.GetDescriptionText().ToLower();
                            if (!string.IsNullOrEmpty(warningType) && !desc.Contains(warningType.ToLower())) continue;

                            // Duplicate Mark values — clear duplicate marks
                            if (desc.Contains("duplicate") && desc.Contains("mark"))
                            {
                                var ids = w.GetFailingElements();
                                if (ids.Count > 1)
                                {
                                    for (int i = 1; i < ids.Count; i++)
                                    {
                                        var elem = doc.GetElement(ids.ElementAt(i));
                                        if (elem == null) continue;
                                        var markParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                                        if (markParam != null && !markParam.IsReadOnly)
                                        {
                                            markParam.Set(markParam.AsString() + "_" + (i + 1));
                                            resolved++;
                                        }
                                    }
                                }
                            }

                            // Room not enclosed — delete unenclosed rooms
                            if (desc.Contains("room") && (desc.Contains("not enclosed") || desc.Contains("not bounding")))
                            {
                                var ids = w.GetFailingElements();
                                foreach (var id in ids)
                                {
                                    try { doc.Delete(id); resolved++; } catch { }
                                }
                            }

                            // Room separation line — try unjoin
                            if (desc.Contains("overlap") && desc.Contains("room separation"))
                            {
                                var ids = w.GetFailingElements();
                                if (ids.Count > 1)
                                {
                                    try { doc.Delete(ids.Last()); resolved++; } catch { }
                                }
                            }
                        }

                        tx.Commit();
                    }
                    catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); }
                }

                return new JObject { ["message"] = $"✅ Resolved {resolved} warning(s)", ["resolved"] = resolved, ["remaining"] = warnings.Count - resolved };
            }

            return new JObject { ["message"] = "Use action='list' or action='resolve'", ["warnings"] = warningList };
        }

        private static JToken WallFloorSync(Document doc, JObject parameters)
        {
            // This command finds walls and floors on the same level and ensures floors extend to wall faces
            var levelName = parameters["levelName"]?.ToString();

            Level level = null;
            if (!string.IsNullOrEmpty(levelName))
                level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => l.Name == levelName);

            var walls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().Cast<Wall>().ToList();
            var floors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType().ToList();

            if (level != null)
            {
                walls = walls.Where(w =>
                {
                    var lp = w.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                    return lp != null && lp.AsElementId() == level.Id;
                }).ToList();
            }

            // Auto-join walls and floors that intersect
            int joined = 0;
            using (var tx = new Transaction(doc, "Wall Floor Sync"))
            {
                tx.Start();
                try
                {
                    foreach (var wall in walls)
                    {
                        var wallBB = wall.get_BoundingBox(null);
                        if (wallBB == null) continue;

                        foreach (var floor in floors)
                        {
                            var floorBB = floor.get_BoundingBox(null);
                            if (floorBB == null) continue;

                            // Check if bounding boxes overlap
                            if (wallBB.Max.X >= floorBB.Min.X && wallBB.Min.X <= floorBB.Max.X &&
                                wallBB.Max.Y >= floorBB.Min.Y && wallBB.Min.Y <= floorBB.Max.Y &&
                                wallBB.Max.Z >= floorBB.Min.Z && wallBB.Min.Z <= floorBB.Max.Z)
                            {
                                try
                                {
                                    if (!JoinGeometryUtils.AreElementsJoined(doc, wall, floor))
                                    {
                                        JoinGeometryUtils.JoinGeometry(doc, wall, floor);
                                        joined++;
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    tx.Commit();
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); }
            }

            return new JObject
            {
                ["message"] = $"✅ Synced {joined} wall-floor connection(s)" + (level != null ? $" on {levelName}" : ""),
                ["joined"] = joined,
                ["wallCount"] = walls.Count,
                ["floorCount"] = floors.Count
            };
        }

        // ══════════════════════════════════════════════════════════════
        // ████  FINAL FOUR TOOLS  ████
        // ══════════════════════════════════════════════════════════════

        private static JToken SnapBeamsToColumns(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Snap Beams to Columns"))
            {
                tx.Start();
                try
                {
                    var tolerance = parameters["tolerance"]?.Value<double>() ?? 2.0; // feet

                    var columns = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_StructuralColumns)
                        .WhereElementIsNotElementType()
                        .ToList();

                    var beams = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_StructuralFraming)
                        .WhereElementIsNotElementType()
                        .ToList();

                    // Get column centerpoints
                    var colCenters = new List<(ElementId id, XYZ point)>();
                    foreach (var col in columns)
                    {
                        if (col.Location is LocationPoint lp)
                            colCenters.Add((col.Id, lp.Point));
                    }

                    int snapped = 0;
                    foreach (var beam in beams)
                    {
                        if (!(beam.Location is LocationCurve lc)) continue;
                        var curve = lc.Curve;
                        var start = curve.GetEndPoint(0);
                        var end = curve.GetEndPoint(1);
                        bool modified = false;

                        // Find nearest column for start point
                        XYZ newStart = start, newEnd = end;
                        foreach (var (colId, colPt) in colCenters)
                        {
                            var dStart = new XYZ(start.X - colPt.X, start.Y - colPt.Y, 0).GetLength();
                            var dEnd = new XYZ(end.X - colPt.X, end.Y - colPt.Y, 0).GetLength();

                            if (dStart < tolerance && dStart > 0.001)
                            {
                                newStart = new XYZ(colPt.X, colPt.Y, start.Z);
                                modified = true;
                            }
                            if (dEnd < tolerance && dEnd > 0.001)
                            {
                                newEnd = new XYZ(colPt.X, colPt.Y, end.Z);
                                modified = true;
                            }
                        }

                        if (modified)
                        {
                            try
                            {
                                lc.Curve = Line.CreateBound(newStart, newEnd);
                                snapped++;
                            }
                            catch { }
                        }
                    }

                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"✅ Snapped {snapped} beam(s) to column centerlines (tolerance: {tolerance} ft)",
                        ["snapped"] = snapped,
                        ["beamCount"] = beams.Count,
                        ["columnCount"] = columns.Count
                    };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken ConvertCategory(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Convert Category"))
            {
                tx.Start();
                try
                {
                    var elementIds = parameters["elementIds"] as JArray;
                    var targetFamily = parameters["targetFamily"]?.ToString();
                    var targetType = parameters["targetType"]?.ToString();

                    if (elementIds == null || string.IsNullOrEmpty(targetFamily))
                        throw new InvalidOperationException("elementIds and targetFamily are required");

                    // Find target family symbol
                    var allSymbols = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .Where(fs => fs.FamilyName == targetFamily);

                    FamilySymbol targetSymbol;
                    if (!string.IsNullOrEmpty(targetType))
                        targetSymbol = allSymbols.FirstOrDefault(fs => fs.Name == targetType);
                    else
                        targetSymbol = allSymbols.FirstOrDefault();

                    if (targetSymbol == null)
                    {
                        var available = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                            .Select(fs => $"{fs.FamilyName}: {fs.Name}").Take(20);
                        throw new InvalidOperationException($"Family '{targetFamily}' not found. Some available: {string.Join(", ", available)}");
                    }

                    if (!targetSymbol.IsActive) targetSymbol.Activate();

                    int converted = 0;
                    var newIds = new List<long>();

                    foreach (var idToken in elementIds)
                    {
                        var oldElem = doc.GetElement(new ElementId(idToken.Value<long>()));
                        if (oldElem == null) continue;

                        // Get position from old element
                        XYZ position = null;
                        Level level = null;

                        if (oldElem.Location is LocationPoint lp)
                            position = lp.Point;
                        else if (oldElem.Location is LocationCurve lc)
                            position = lc.Curve.GetEndPoint(0);
                        else
                        {
                            var bb = oldElem.get_BoundingBox(null);
                            if (bb != null) position = (bb.Min + bb.Max) / 2;
                        }

                        if (position == null) continue;

                        // Get level
                        var lvlParam = oldElem.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                            ?? oldElem.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                        if (lvlParam != null)
                            level = doc.GetElement(lvlParam.AsElementId()) as Level;
                        if (level == null)
                            level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => Math.Abs(l.Elevation - position.Z)).FirstOrDefault();

                        if (level == null) continue;

                        // Create new element and delete old
                        try
                        {
                            var newInst = doc.Create.NewFamilyInstance(position, targetSymbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            newIds.Add(newInst.Id.Value);
                            doc.Delete(oldElem.Id);
                            converted++;
                        }
                        catch { }
                    }

                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"✅ Converted {converted} element(s) to {targetFamily}",
                        ["newElementIds"] = new JArray(newIds)
                    };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken AddSharedParameter(Document doc, UIApplication uiApp, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Add Shared Parameter"))
            {
                tx.Start();
                try
                {
                    var paramName = parameters["parameterName"]?.ToString();
                    var categoryName = parameters["category"]?.ToString();
                    var groupName = parameters["groupName"]?.ToString() ?? "Data";
                    var paramType = parameters["paramType"]?.ToString() ?? "Text";
                    var isInstance = parameters["isInstance"]?.Value<bool>() ?? true;

                    if (string.IsNullOrEmpty(paramName) || string.IsNullOrEmpty(categoryName))
                        throw new InvalidOperationException("parameterName and category are required");

                    var bic = GetBuiltInCategory(categoryName);
                    var cat = doc.Settings.Categories.get_Item(bic);
                    if (cat == null) throw new InvalidOperationException($"Category '{categoryName}' not found");

                    var catSet = new CategorySet();
                    catSet.Insert(cat);

                    // Get or create shared parameter file
                    var app = uiApp.Application;
                    var defFile = app.OpenSharedParameterFile();
                    if (defFile == null)
                    {
                        // Create a temp shared parameter file
                        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RevitMCP_SharedParams.txt");
                        if (!System.IO.File.Exists(tempPath))
                            System.IO.File.WriteAllText(tempPath, "");
                        app.SharedParametersFilename = tempPath;
                        defFile = app.OpenSharedParameterFile();
                    }

                    if (defFile == null)
                        throw new InvalidOperationException("Cannot open or create shared parameter file");

                    // Get or create group
                    var group = defFile.Groups.get_Item(groupName);
                    if (group == null)
                        group = defFile.Groups.Create(groupName);

                    // Check if parameter already exists
                    var existingDef = group.Definitions.get_Item(paramName);
                    ExternalDefinition extDef;

                    if (existingDef != null)
                    {
                        extDef = existingDef as ExternalDefinition;
                    }
                    else
                    {
                        // Determine ForgeTypeId from string
                        var specTypeId = SpecTypeId.String.Text; // default
                        switch (paramType.ToLower())
                        {
                            case "number": case "integer": specTypeId = SpecTypeId.Int.Integer; break;
                            case "length": specTypeId = SpecTypeId.Length; break;
                            case "area": specTypeId = SpecTypeId.Area; break;
                            case "volume": specTypeId = SpecTypeId.Volume; break;
                            case "angle": specTypeId = SpecTypeId.Angle; break;
                            case "yesno": case "boolean": specTypeId = SpecTypeId.Boolean.YesNo; break;
                            default: specTypeId = SpecTypeId.String.Text; break;
                        }

                        var opts = new ExternalDefinitionCreationOptions(paramName, specTypeId);
                        extDef = group.Definitions.Create(opts) as ExternalDefinition;
                    }

                    if (extDef == null)
                        throw new InvalidOperationException("Failed to create parameter definition");

                    // Add binding
                    var binding = isInstance
                        ? (Binding)uiApp.Application.Create.NewInstanceBinding(catSet)
                        : (Binding)uiApp.Application.Create.NewTypeBinding(catSet);

                    var paramGroup = GroupTypeId.Data;
                    doc.ParameterBindings.Insert(extDef, binding, paramGroup);

                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"✅ Added shared parameter '{paramName}' ({paramType}) to {categoryName} ({(isInstance ? "instance" : "type")})",
                        ["parameterName"] = paramName
                    };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken ImportDataFromCsv(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Import Data from CSV"))
            {
                tx.Start();
                try
                {
                    var filePath = parameters["filePath"]?.ToString();
                    var categoryName = parameters["category"]?.ToString();
                    var keyParameter = parameters["keyParameter"]?.ToString() ?? "Number";

                    if (string.IsNullOrEmpty(filePath))
                        throw new InvalidOperationException("filePath is required");

                    if (!System.IO.File.Exists(filePath))
                        throw new InvalidOperationException($"File not found: {filePath}");

                    var lines = System.IO.File.ReadAllLines(filePath);
                    if (lines.Length < 2)
                        throw new InvalidOperationException("CSV must have a header row and at least one data row");

                    var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();

                    // Get elements
                    IList<Element> elements;
                    if (!string.IsNullOrEmpty(categoryName))
                    {
                        var bic = GetBuiltInCategory(categoryName);
                        elements = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToList();
                    }
                    else
                    {
                        elements = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToList();
                    }

                    // Build lookup by key parameter
                    var lookup = new Dictionary<string, Element>();
                    foreach (var elem in elements)
                    {
                        // Try Element ID as key
                        if (keyParameter == "Id" || keyParameter == "ElementId")
                        {
                            lookup[elem.Id.Value.ToString()] = elem;
                            continue;
                        }

                        foreach (Parameter p in elem.Parameters)
                        {
                            if (p.Definition.Name == keyParameter)
                            {
                                var val = p.AsString() ?? p.AsValueString() ?? "";
                                if (!string.IsNullOrEmpty(val))
                                    lookup[val] = elem;
                                break;
                            }
                        }
                    }

                    int updated = 0, skipped = 0;
                    for (int row = 1; row < lines.Length; row++)
                    {
                        var values = lines[row].Split(',').Select(v => v.Trim().Trim('"')).ToArray();
                        if (values.Length < 2) continue;

                        // Find key column index
                        int keyCol = Array.IndexOf(headers, keyParameter);
                        if (keyCol < 0) keyCol = 0;

                        var key = values[keyCol];
                        if (!lookup.TryGetValue(key, out var elem)) { skipped++; continue; }

                        // Set other columns as parameters
                        for (int col = 0; col < Math.Min(headers.Length, values.Length); col++)
                        {
                            if (col == keyCol) continue;
                            var hdr = headers[col];
                            var val = values[col];

                            foreach (Parameter p in elem.Parameters)
                            {
                                if (p.Definition.Name == hdr && !p.IsReadOnly)
                                {
                                    switch (p.StorageType)
                                    {
                                        case StorageType.String: p.Set(val); break;
                                        case StorageType.Double:
                                            if (double.TryParse(val, out double d)) p.Set(d);
                                            break;
                                        case StorageType.Integer:
                                            if (int.TryParse(val, out int i)) p.Set(i);
                                            break;
                                    }
                                    updated++;
                                    break;
                                }
                            }
                        }
                    }

                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"✅ Imported CSV: updated {updated} parameter value(s), skipped {skipped} unmatched row(s)",
                        ["updated"] = updated,
                        ["skipped"] = skipped,
                        ["totalRows"] = lines.Length - 1
                    };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ████  FINAL TWO MISSING TOOLS  ████
        // ══════════════════════════════════════════════════════════════

        private static JToken GenerateLegend(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Generate Legend"))
            {
                tx.Start();
                try
                {
                    var categoryName = parameters["category"]?.ToString() ?? "Doors";
                    var legendName = parameters["legendName"]?.ToString() ?? $"{categoryName} Legend";

                    var bic = categoryName.Equals("Windows", StringComparison.OrdinalIgnoreCase)
                        ? BuiltInCategory.OST_Windows
                        : BuiltInCategory.OST_Doors;

                    // Collect unique types
                    var elements = new FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .ToList();

                    var typeInfos = new Dictionary<string, JObject>();
                    foreach (var inst in elements)
                    {
                        var sym = inst.Symbol;
                        var key = $"{sym.FamilyName}: {sym.Name}";
                        if (typeInfos.ContainsKey(key)) continue;

                        double width = 0, height = 0;
                        var wParam = sym.get_Parameter(BuiltInParameter.DOOR_WIDTH)
                            ?? sym.get_Parameter(BuiltInParameter.WINDOW_WIDTH)
                            ?? sym.get_Parameter(BuiltInParameter.CASEWORK_WIDTH);
                        var hParam = sym.get_Parameter(BuiltInParameter.DOOR_HEIGHT)
                            ?? sym.get_Parameter(BuiltInParameter.WINDOW_HEIGHT)
                            ?? sym.get_Parameter(BuiltInParameter.GENERIC_HEIGHT);

                        if (wParam != null) width = wParam.AsDouble();
                        if (hParam != null) height = hParam.AsDouble();

                        int count = elements.Count(e => e.Symbol.Id == sym.Id);

                        typeInfos[key] = new JObject
                        {
                            ["family"] = sym.FamilyName,
                            ["type"] = sym.Name,
                            ["width"] = Math.Round(width * 304.8) + " mm",
                            ["height"] = Math.Round(height * 304.8) + " mm",
                            ["count"] = count
                        };
                    }

                    // Create a drafting view for the legend
                    var viewFamilyType = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Drafting);

                    if (viewFamilyType == null)
                        throw new InvalidOperationException("No drafting view family type found");

                    var legendView = ViewDrafting.Create(doc, viewFamilyType.Id);
                    legendView.Name = legendName;
                    legendView.Scale = 50;

                    // Create text notes as a simple table
                    var textTypeId = new FilteredElementCollector(doc)
                        .OfClass(typeof(TextNoteType))
                        .FirstElementId();

                    double yPos = 0;
                    double rowHeight = 0.02; // ~6mm at 1:50

                    // Header row
                    var header = $"No.    Family               Type                 Width       Height      Count";
                    TextNote.Create(doc, legendView.Id, new XYZ(0, yPos, 0), header, textTypeId);
                    yPos -= rowHeight * 1.5;

                    int idx = 1;
                    foreach (var kvp in typeInfos)
                    {
                        var info = kvp.Value;
                        var line = $"{idx,-6} {info["family"]?.ToString(),-20} {info["type"]?.ToString(),-20} {info["width"],-11} {info["height"],-11} {info["count"]}";
                        TextNote.Create(doc, legendView.Id, new XYZ(0, yPos, 0), line, textTypeId);
                        yPos -= rowHeight;
                        idx++;
                    }

                    tx.Commit();

                    var result = new JObject
                    {
                        ["message"] = $"✅ Created '{legendName}' with {typeInfos.Count} {categoryName} type(s)",
                        ["viewName"] = legendName,
                        ["viewId"] = legendView.Id.Value,
                        ["types"] = new JArray(typeInfos.Values)
                    };
                    return result;
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken CadToLines(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "CAD to Lines"))
            {
                tx.Start();
                try
                {
                    var deleteAfter = parameters["deleteAfter"]?.Value<bool>() ?? false;
                    var importIds = parameters["importIds"] as JArray;

                    // Find CAD imports
                    var imports = new FilteredElementCollector(doc)
                        .OfClass(typeof(ImportInstance))
                        .Cast<ImportInstance>()
                        .Where(i => !i.IsLinked)
                        .ToList();

                    if (importIds != null)
                    {
                        var idSet = new HashSet<long>(importIds.Select(id => id.Value<long>()));
                        imports = imports.Where(i => idSet.Contains(i.Id.Value)).ToList();
                    }

                    if (imports.Count == 0)
                        throw new InvalidOperationException("No CAD imports found to convert");

                    // Get the active view for placing detail lines
                    var activeViewId = doc.ActiveView.Id;
                    int totalLines = 0;
                    var convertedImports = new List<long>();

                    // Find a line style
                    var defaultLineStyle = doc.Settings.Categories
                        .get_Item(BuiltInCategory.OST_Lines)
                        .SubCategories
                        .Cast<Category>()
                        .FirstOrDefault(c => c.Name.Contains("Thin") || c.Name.Contains("Medium"))
                        ?? doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines)
                            .SubCategories.Cast<Category>().FirstOrDefault();

                    foreach (var import in imports)
                    {
                        var geomElem = import.get_Geometry(new Options());
                        if (geomElem == null) continue;

                        int linesFromImport = 0;
                        var transform = import.GetTransform();

                        foreach (var geomObj in geomElem)
                        {
                            if (geomObj is GeometryInstance gi)
                            {
                                var symbolGeom = gi.GetInstanceGeometry();
                                foreach (var innerObj in symbolGeom)
                                {
                                    if (innerObj is Line line && line.Length > 0.001)
                                    {
                                        try
                                        {
                                            var detailLine = doc.Create.NewDetailCurve(doc.ActiveView, line);
                                            if (defaultLineStyle != null)
                                                detailLine.LineStyle = defaultLineStyle.GetGraphicsStyle(GraphicsStyleType.Projection);
                                            linesFromImport++;
                                        }
                                        catch { }
                                    }
                                    else if (innerObj is Arc arc && arc.Length > 0.001)
                                    {
                                        try
                                        {
                                            doc.Create.NewDetailCurve(doc.ActiveView, arc);
                                            linesFromImport++;
                                        }
                                        catch { }
                                    }
                                    else if (innerObj is PolyLine polyLine)
                                    {
                                        var coords = polyLine.GetCoordinates();
                                        for (int i = 0; i < coords.Count - 1; i++)
                                        {
                                            try
                                            {
                                                var seg = Line.CreateBound(coords[i], coords[i + 1]);
                                                if (seg.Length > 0.001)
                                                {
                                                    doc.Create.NewDetailCurve(doc.ActiveView, seg);
                                                    linesFromImport++;
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }

                        totalLines += linesFromImport;
                        convertedImports.Add(import.Id.Value);

                        if (deleteAfter && linesFromImport > 0)
                        {
                            try { doc.Delete(import.Id); } catch { }
                        }
                    }

                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"✅ Converted {imports.Count} CAD import(s) → {totalLines} detail line(s)" + (deleteAfter ? " (originals deleted)" : ""),
                        ["convertedImports"] = new JArray(convertedImports),
                        ["linesCreated"] = totalLines
                    };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        // ===== HELPER: Map category name string to BuiltInCategory =====
        private static BuiltInCategory GetBuiltInCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName))
                throw new InvalidOperationException("Category name is required");

            var map = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
            {
                { "Walls", BuiltInCategory.OST_Walls },
                { "Doors", BuiltInCategory.OST_Doors },
                { "Windows", BuiltInCategory.OST_Windows },
                { "Floors", BuiltInCategory.OST_Floors },
                { "Ceilings", BuiltInCategory.OST_Ceilings },
                { "Roofs", BuiltInCategory.OST_Roofs },
                { "Rooms", BuiltInCategory.OST_Rooms },
                { "Columns", BuiltInCategory.OST_Columns },
                { "Structural Columns", BuiltInCategory.OST_StructuralColumns },
                { "Structural Framing", BuiltInCategory.OST_StructuralFraming },
                { "Furniture", BuiltInCategory.OST_Furniture },
                { "Plumbing Fixtures", BuiltInCategory.OST_PlumbingFixtures },
                { "Mechanical Equipment", BuiltInCategory.OST_MechanicalEquipment },
                { "Electrical Equipment", BuiltInCategory.OST_ElectricalEquipment },
                { "Electrical Fixtures", BuiltInCategory.OST_ElectricalFixtures },
                { "Lighting Fixtures", BuiltInCategory.OST_LightingFixtures },
                { "Generic Models", BuiltInCategory.OST_GenericModel },
                { "Stairs", BuiltInCategory.OST_Stairs },
                { "Railings", BuiltInCategory.OST_StairsRailing },
                { "Curtain Panels", BuiltInCategory.OST_CurtainWallPanels },
                { "Curtain Wall Mullions", BuiltInCategory.OST_CurtainWallMullions },
                { "Casework", BuiltInCategory.OST_Casework },
                { "Specialty Equipment", BuiltInCategory.OST_SpecialityEquipment },
                { "Pipes", BuiltInCategory.OST_PipeCurves },
                { "Ducts", BuiltInCategory.OST_DuctCurves },
                { "Cable Trays", BuiltInCategory.OST_CableTray },
                { "Conduits", BuiltInCategory.OST_Conduit },
                { "Parking", BuiltInCategory.OST_Parking },
                { "Site", BuiltInCategory.OST_Site },
                { "Topography", BuiltInCategory.OST_Topography },
                { "Areas", BuiltInCategory.OST_Areas },
                { "Mass", BuiltInCategory.OST_Mass },
                { "Structural Foundations", BuiltInCategory.OST_StructuralFoundation },
            };

            if (map.TryGetValue(categoryName, out var bic))
                return bic;

            // Try to parse as BuiltInCategory enum directly
            if (Enum.TryParse<BuiltInCategory>(categoryName, true, out var parsed))
                return parsed;
            if (Enum.TryParse<BuiltInCategory>("OST_" + categoryName.Replace(" ", ""), true, out var parsed2))
                return parsed2;

            throw new InvalidOperationException($"Unknown category '{categoryName}'. Common categories: {string.Join(", ", map.Keys.Take(15))}");
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

    }
}
