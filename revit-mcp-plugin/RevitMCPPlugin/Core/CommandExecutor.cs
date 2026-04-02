using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPPlugin.PowerBI;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Routes MCP commands to their corresponding Revit API implementations.
    /// This is the main command dispatcher. Domain-specific methods are in
    /// partial class files (CommandExecutor.Mep.cs, etc.) for maintainability.
    /// </summary>
    public static partial class CommandExecutor
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
                case "export_elements":
                    return ExportElements(doc, parameters);

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
                case "place_views_on_sheet":
                    return PlaceViewsOnSheet(doc, parameters);
                case "tag_all_in_view":
                    return TagAllInView(uidoc!, doc, parameters);
                case "create_legend":
                    return GenerateLegend(doc, parameters);
                case "add_revision":
                case "print_sheets":
                    return ExecuteGenericCommand(doc, command, parameters);

                // ===== QA/QC COMMANDS =====
                case "check_warnings":
                case "isolate_warnings":
                    return IsolateWarnings(uidoc!, doc, parameters);
                case "audit_model":
                    return AuditModel(doc);
                case "purge_unused":
                    return DeepPurge(doc);
                case "purge_cads":
                    return FindCadImports(doc, new JObject { ["delete"] = true });
                case "check_room_compliance":
                case "check_naming_conventions":
                case "find_duplicates":
                case "check_links_status":
                case "validate_parameters":
                    return ExecuteGenericCommand(doc, command, parameters);

                // ===== ADVANCED COMMANDS =====
                case "send_code_to_revit":
                case "execute_code":
                    return CodeExecutor.Execute(uiApp, parameters);
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
                case "open_view":
                {
                    var viewId = parameters["viewId"]?.Value<int>() ?? 0;
                    var view = doc.GetElement(new ElementId(viewId)) as View;
                    if (view == null) throw new InvalidOperationException($"View {viewId} not found");
                    uidoc!.ActiveView = view;
                    return new JObject { ["message"] = $"Opened view '{view.Name}'", ["viewId"] = viewId };
                }
                case "close_view":
                {
                    var closeViewId = parameters["viewId"]?.Value<int>() ?? 0;
                    var closeView = doc.GetElement(new ElementId(closeViewId)) as View;
                    if (closeView == null) throw new InvalidOperationException($"View {closeViewId} not found");
                    var openUIViews = uidoc!.GetOpenUIViews();
                    var uiView = openUIViews.FirstOrDefault(uv => uv.ViewId.Value == closeViewId);
                    if (uiView == null) return new JObject { ["message"] = $"View '{closeView.Name}' is not open" };
                    uiView.Close();
                    return new JObject { ["message"] = $"Closed view '{closeView.Name}'", ["viewId"] = closeViewId };
                }
                case "set_view_properties":
                    return SetViewProperties(uidoc!, doc, parameters);
                case "override_element_in_view":
                    return OverrideElementInView(uidoc!, doc, parameters);
                case "set_visibility_graphics":
                    return SetVisibilityGraphics(uidoc!, doc, parameters);
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
                case "create_room_finishes":
                    return RoomToFloor(doc, parameters);
                // Data & Parameters
                case "find_replace_names":
                case "bulk_rename_views":
                    return BulkRenameViews(doc, parameters);
                case "parameter_case_convert":
                    return ParameterCaseConvert(doc, parameters);
                case "bulk_parameter_transfer":
                case "copy_parameter_value":
                    return CopyParameterValue(doc, parameters);
                case "auto_renumber":
                case "renumber_elements":
                    return AutoRenumber(doc, parameters);
                // Views & Documentation
                case "batch_create_sheets":
                    return BatchCreateSheets(doc, parameters);
                case "align_viewports":
                    return AlignViewports(doc, parameters);
                case "duplicate_sheets":
                    return DuplicateSheets(doc, parameters);
                case "auto_section_box":
                    return AutoSectionBox(uidoc!, doc, parameters);
                case "copy_view_filters":
                    return CopyViewFilters(doc, parameters);
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
                case "select_by_filter":
                    return SelectByFilter(uidoc!, doc, parameters);
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
                // Editing aliases
                case "color_by_parameter":
                    return ColorElements(uidoc!, doc, parameters);
                case "extend_shrink_element":
                    return ExtendShrinkElement(doc, parameters);

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
                case "export_to_cad":
                    return ExportToDwg(doc, parameters);
                case "export_to_dwf":
                    return ExportToDwf(doc, parameters);
                case "export_to_nwc":
                    return ExportToNwc(doc, parameters);
                case "export_to_powerbi":
                    return ExportToPowerBI(uidoc!, doc, parameters);
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

                // ===== PROJECT DATA MANAGEMENT =====
                case "save_project_data":
                    return SaveProjectData(doc, parameters);
                case "load_project_data":
                    return LoadProjectData(doc, parameters);
                case "list_project_data":
                    return ListProjectData(doc);
                case "delete_project_data":
                    return DeleteProjectData(doc, parameters);
                case "save_snapshot":
                    return SaveModelSnapshot(doc);

                // ===== ADDITIONAL QUERY TOOLS =====
                case "create_view_filter":
                    return CreateViewFilter(uidoc!, doc, parameters);
                case "get_worksets":
                    return GetWorksets(doc);
                case "get_areas":
                    return GetAreas(doc);
                case "get_design_options":
                    return GetDesignOptions(doc);

                // ===== INTEGRATION TOOLS =====
                case "get_integration_status":
                    return GetIntegrationStatus();
                case "export_to_excel_integration":
                case "export_to_notion_integration":
                case "export_to_google_sheets_integration":
                    return PrepareIntegrationExport(doc, command, parameters);

                // ===== PROJECT FILES TOOLS =====
                case "list_project_files":
                    return ProjectFilesService.ListFiles(doc.PathName, parameters);
                case "read_project_file":
                    return ProjectFilesService.ReadFile(doc.PathName, parameters);
                case "analyze_project_file":
                    return ProjectFilesService.AnalyzeFile(doc.PathName, parameters);
                case "search_project_files":
                    return ProjectFilesService.SearchFiles(doc.PathName, parameters);
                case "export_elements_to_csv":
                    return ExportElementsToCsv(doc, parameters);
                case "export_elements_to_excel":
                    return ExportElementsToExcel(doc, parameters);
                case "import_from_project_file":
                    return ImportFromProjectFile(doc, parameters);

                // ===== EXCEL TOOLS =====
                case "excel_create_workbook":
                    return ExcelService.CreateWorkbook(doc.PathName, parameters);
                case "excel_read_range":
                    return ExcelService.ReadRange(doc.PathName, parameters);
                case "excel_write_cells":
                    return ExcelService.WriteCells(doc.PathName, parameters);
                case "excel_add_sheet":
                    return ExcelService.ManageSheet(doc.PathName, parameters);
                case "excel_insert_rows":
                    return ExcelService.InsertRows(doc.PathName, parameters);
                case "excel_format_cells":
                    return ExcelService.FormatCells(doc.PathName, parameters);
                case "excel_add_formula":
                    return ExcelService.AddFormula(doc.PathName, parameters);
                case "excel_get_info":
                    return ExcelService.GetInfo(doc.PathName, parameters);

                // ===== NONICA-INSPIRED POWER TOOLS =====
                case "cut_floors":
                    return CutFloors(doc, parameters);
                case "split_by_levels":
                    return SplitByLevels(doc, parameters);
                case "create_openings":
                    return CreateOpenings(doc, parameters);
                case "manage_scope_boxes":
                    return ManageScopeBoxes(doc, parameters);
                case "find_empty_sheets":
                    return FindEmptySheets(doc, parameters);
                case "clean_unused_templates":
                    return CleanUnusedTemplates(doc, parameters);
                case "clean_unplaced_views":
                    return CleanUnplacedViews(doc, parameters);
                case "purge_unused_in_families":
                    return PurgeUnusedInFamilies(doc, parameters);
                case "delete_families_by_size":
                    return DeleteFamiliesBySize(doc, parameters);
                case "explode_3d_view":
                    return Explode3DView(doc, uidoc, parameters);
                case "rotate_section_box":
                    return RotateSectionBox(doc, uidoc, parameters);
                case "super_align":
                    return SuperAlign(doc, parameters);
                case "join_elements_in_view":
                    return JoinElementsInView(doc, uidoc, parameters);
                case "copy_to_project":
                    return CopyToProject(doc, uiApp, parameters);
                case "measure_elements":
                    return MeasureElements(doc, parameters);

                // ===== MEP TOOLS =====
                case "create_duct":
                    return CreateDuct(doc, parameters);
                case "create_pipe":
                    return CreatePipe(doc, parameters);
                case "create_flex_duct":
                    return CreateFlexDuct(doc, parameters);
                case "create_mep_space":
                    return CreateMepSpace(doc, parameters);
                case "get_mep_systems":
                    return GetMepSystems(doc, parameters);
                case "duct_sizing":
                    return DuctSizing(doc, parameters);
                case "connect_mep_elements":
                    return ConnectMepElements(doc, parameters);

                // ===== STRUCTURAL TOOLS =====
                case "create_structural_beam":
                    return CreateStructuralBeam(doc, parameters);
                case "create_structural_column":
                    return CreateStructuralColumn(doc, parameters);
                case "create_wall_foundation":
                    return CreateWallFoundation(doc, parameters);
                case "create_rebar":
                    return CreateRebar(doc, parameters);
                case "get_structural_elements":
                    return GetStructuralElements(doc, parameters);
                case "analytical_model_info":
                    return AnalyticalModelInfo(doc, parameters);

                // ===== ANNOTATION TOOLS =====
                case "create_filled_region":
                    return CreateFilledRegion(doc, uidoc, parameters);
                case "create_spot_elevation":
                    return CreateSpotElevation(doc, uidoc, parameters);
                case "create_spot_coordinate":
                    return CreateSpotCoordinate(doc, uidoc, parameters);
                case "create_keynote_legend":
                    return CreateKeynoteLegend(doc, parameters);
                case "create_detail_component":
                    return CreateDetailComponent(doc, uidoc, parameters);
                case "tag_rooms_in_view":
                    return TagRoomsInView(doc, uidoc, parameters);
                case "dimension_walls":
                    return DimensionWalls(doc, uidoc, parameters);

                // ===== ARCHITECTURE TOOLS =====
                case "create_stairs":
                    return CreateStairs(doc, parameters);
                case "create_railing":
                    return CreateRailing(doc, parameters);
                case "create_curtain_wall":
                    return CreateCurtainWall(doc, parameters);
                case "create_shaft_opening":
                    return CreateShaftOpening(doc, parameters);
                case "get_stairs_info":
                    return GetStairsInfo(doc, parameters);
                case "get_curtain_panels":
                    return GetCurtainPanels(doc, parameters);
                case "create_opening_in_wall":
                    return CreateOpeningInWall(doc, parameters);

                // ===== SITE TOOLS =====
                case "create_topography":
                    return CreateTopography(doc, parameters);
                case "create_building_pad":
                    return CreateBuildingPad(doc, parameters);
                case "get_site_info":
                    return GetSiteInfo(doc, parameters);

                // ===== UTILITY TOOLS =====
                case "pin_elements":
                    return PinElements(doc, parameters, true);
                case "unpin_elements":
                    return PinElements(doc, parameters, false);
                case "create_workset":
                    return CreateWorkset(doc, parameters);
                case "get_element_history":
                    return GetElementHistory(doc, parameters);
                case "create_assembly":
                    return CreateAssembly(doc, parameters);
                case "create_fill_pattern":
                    return CreateFillPattern(doc, parameters);
                case "get_element_geometry":
                    return GetElementGeometry(doc, parameters);
                case "compare_models":
                    return CompareModels(doc, parameters);
                case "link_revit_model":
                    return LinkRevitModel(doc, uiApp, parameters);
                case "reload_links":
                    return ReloadLinks(doc, parameters);
                case "unload_links":
                    return UnloadLinks(doc, parameters);
                case "get_link_info":
                    return GetLinkInfo(doc, parameters);

                // ===== FILE MANAGEMENT (Phase 1) =====
                case "save_document":
                    return SaveDocument(doc);
                case "save_as_document":
                    return SaveAsDocument(doc, parameters);
                case "close_document":
                    return CloseDocument(doc, parameters);

                // ===== FAMILY EDITOR (Phase 2) =====
                case "edit_family":
                    return EditFamily(uiApp, doc, parameters);
                case "create_family_extrusion":
                    return CreateFamilyExtrusion(uiApp, parameters);
                case "save_family":
                    return SaveFamily(uiApp, parameters);
                case "load_family":
                    return LoadFamily(doc, parameters);

                // ===== SKETCH EDITING (Phase 3) =====
                case "get_sketch":
                    return GetSketch(doc, parameters);
                case "edit_sketch":
                    return EditSketch(doc, parameters);
                case "set_sketch_profile":
                    return SetSketchProfile(doc, parameters);

                // ===== DRAFTING (Phase 4) =====
                case "create_detail_lines":
                    return CreateDetailLines(uidoc!, doc, parameters);
                case "create_model_lines":
                    return CreateModelLines(doc, parameters);
                case "create_detail_arc":
                    return CreateDetailArc(uidoc!, doc, parameters);

                // ===== RENDERING (Phase 5) =====
                case "set_sun_settings":
                    return SetSunSettings(uidoc!, doc, parameters);
                case "set_visual_style":
                    return SetVisualStyle(uidoc!, doc, parameters);
                case "export_view_image":
                    return ExportViewImage(doc, parameters);

                // ===== WORKSHARING (Phase 6) =====
                case "sync_to_central":
                    return SyncToCentral(doc, parameters);
                case "relinquish_all":
                    return RelinquishAll(doc);
                case "get_worksharing_info":
                    return GetWorksharingInfo(doc);

                // ===== UNDO / TRANSACTIONS (Phase 8) =====
                case "undo_last_operation":
                    return UndoLastOperation(uiApp);
                case "create_checkpoint":
                    return CreateCheckpoint(doc, parameters);
                case "rollback_to_checkpoint":
                    return RollbackToCheckpoint(parameters);

                // ===== UI AUTOMATION (Phase 9) =====
                case "post_command":
                    return PostCommand(uiApp, parameters);
                case "list_commands":
                    return ListPostableCommands();

                // ===== SAFE CODE (Phase 10) =====
                case "preview_code":
                    return CodeExecutor.Preview(uiApp, parameters);

                // ===== REMAINING GAPS =====
                case "open_document":
                    return OpenDocument(uiApp, parameters);
                case "create_new_project":
                    return CreateNewProject(uiApp, parameters);
                case "create_new_family":
                    return CreateNewFamily(uiApp, parameters);
                case "detach_from_central":
                    return DetachFromCentral(uiApp, parameters);
                case "change_link_path":
                    return ChangeLinkPath(doc, parameters);
                case "manage_link_position":
                    return ManageLinkPosition(doc, parameters);
                case "zoom_to_fit":
                    return ZoomToFit(uidoc!);
                case "zoom_to_element":
                    return ZoomToElement(uidoc!, doc, parameters);
                case "edit_schedule":
                    return EditSchedule(doc, parameters);

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
                ["viewId"] = view.Id.Value,
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
            var offset = parameters["offset"]?.Value<int>() ?? 0;
            var limit = parameters["limit"]?.Value<int>() ?? 0;

            if (!string.IsNullOrEmpty(category))
            {
                var builtInCat = GetBuiltInCategory(category);
                if (builtInCat != BuiltInCategory.INVALID)
                    collector = collector.OfCategory(builtInCat);
            }

            var allElements = collector.WhereElementIsNotElementType().ToElements();
            var totalCount = allElements.Count;
            var subset = offset > 0 ? allElements.Skip(offset) : (IEnumerable<Element>)allElements;
            if (limit > 0) subset = subset.Take(limit);
            var result = new JArray();

            foreach (var elem in subset)
            {
                result.Add(new JObject
                {
                    ["id"] = elem.Id.Value,
                    ["name"] = elem.Name,
                    ["category"] = elem.Category?.Name ?? "Unknown"
                });
            }

            return new JObject { ["totalCount"] = totalCount, ["count"] = result.Count, ["offset"] = offset, ["limit"] = limit, ["hasMore"] = (offset + result.Count) < totalCount, ["elements"] = result };
        }

        private static JToken GetSelectedElements(UIDocument uidoc)
        {
            var selected = uidoc.Selection.GetElementIds().ToList();
            var totalCount = selected.Count;
            var result = new JArray();

            foreach (var id in selected)
            {
                var elem = uidoc.Document.GetElement(id);
                if (elem != null)
                {
                    var elemObj = new JObject
                    {
                        ["id"] = elem.Id.Value,
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

            return new JObject { ["totalCount"] = totalCount, ["count"] = result.Count, ["elements"] = result };
        }

        private static JToken GetElements(Document doc, JObject parameters)
        {
            var category = parameters["category"]?.ToString() ?? "";
            var includeParams = parameters["includeParameters"]?.Value<bool>() ?? false;
            var offset = parameters["offset"]?.Value<int>() ?? 0;
            var limit = parameters["limit"]?.Value<int>() ?? 0;

            var collector = new FilteredElementCollector(doc);
            var builtInCat = GetBuiltInCategory(category);

            if (builtInCat != BuiltInCategory.INVALID)
                collector = collector.OfCategory(builtInCat);

            var allElements = collector.WhereElementIsNotElementType().ToElements();
            var totalCount = allElements.Count;
            var subset = offset > 0 ? allElements.Skip(offset) : (IEnumerable<Element>)allElements;
            if (limit > 0) subset = subset.Take(limit);
            var result = new JArray();

            foreach (var elem in subset)
            {
                var obj = new JObject
                {
                    ["id"] = elem.Id.Value,
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

            return new JObject { ["totalCount"] = totalCount, ["count"] = result.Count, ["offset"] = offset, ["limit"] = limit, ["hasMore"] = (offset + result.Count) < totalCount, ["elements"] = result };
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

        // View type aliases for user-friendly filtering
        private static readonly Dictionary<string, string> _viewTypeAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "3D", "ThreeD" },
            { "3d", "ThreeD" },
            { "Plan", "FloorPlan" },
            { "plan", "FloorPlan" },
            { "RCP", "CeilingPlan" },
            { "rcp", "CeilingPlan" },
            { "Ceiling", "CeilingPlan" },
            { "Elevation", "Elevation" },
            { "Detail", "Detail" },
            { "Drafting", "DraftingView" },
            { "Legend", "Legend" },
            { "Schedule", "Schedule" },
            { "Walkthrough", "Walkthrough" },
            { "Area", "AreaPlan" },
        };

        private static JToken GetViews(Document doc, JObject parameters)
        {
            var viewTypeFilter = parameters["viewType"]?.ToString() ?? "";

            // Resolve aliases (e.g., "3D" → "ThreeD", "Plan" → "FloorPlan")
            if (!string.IsNullOrEmpty(viewTypeFilter) && _viewTypeAliases.ContainsKey(viewTypeFilter))
                viewTypeFilter = _viewTypeAliases[viewTypeFilter];

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
                    ["id"] = view.Id.Value,
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
                    ["id"] = sheet.Id.Value,
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
                    ["id"] = level.Id.Value,
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
                    ["id"] = grid.Id.Value,
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
                        ["id"] = room.Id.Value,
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
                    ["id"] = symbol.Id.Value,
                    ["familyName"] = symbol.FamilyName,
                    ["typeName"] = symbol.Name,
                    ["category"] = symbol.Category?.Name ?? ""
                });
            }

            return new JObject { ["totalCount"] = result.Count, ["count"] = result.Count, ["familyTypes"] = result };
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
                    ["id"] = schedule.Id.Value,
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
                    ["id"] = linkType.Id.Value,
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
                var elementIds = warning.GetFailingElements().Select(id => id.Value).ToList();
                result.Add(new JObject
                {
                    ["description"] = warning.GetDescriptionText(),
                    ["severity"] = warning.GetSeverity().ToString(),
                    ["elementIds"] = new JArray(elementIds)
                });
            }

            return new JObject { ["warnings"] = result, ["count"] = result.Count };
        }

        // ===== DATA BRIDGE EXPORT =====

        /// <summary>
        /// Lightweight export for the Data Bridge — returns minified JSON with only essential fields.
        /// Supports delta-sync (modifiedAfter) and batching (offset/limit, default 100).
        /// Delta-sync uses element EDITED_BY and phase-created timestamps.
        /// </summary>
        private static JToken ExportElements(Document doc, JObject parameters)
        {
            var category = parameters["category"]?.ToString() ?? "";
            var offset = parameters["offset"]?.Value<int>() ?? 0;
            var limit = parameters["limit"]?.Value<int>() ?? 100; // 100 elements per sync cycle
            var modifiedAfter = parameters["modifiedAfter"]?.ToString() ?? "";

            var collector = new FilteredElementCollector(doc);
            var builtInCat = GetBuiltInCategory(category);

            if (builtInCat != BuiltInCategory.INVALID)
                collector = collector.OfCategory(builtInCat);

            var allElements = collector.WhereElementIsNotElementType().ToElements();

            // Delta-sync: filter by modification timestamp if provided
            DateTime? filterDate = null;
            if (!string.IsNullOrEmpty(modifiedAfter))
            {
                if (DateTime.TryParse(modifiedAfter, out var parsedDate))
                    filterDate = parsedDate;
            }

            var filteredElements = allElements.AsEnumerable();

            if (filterDate.HasValue)
            {
                filteredElements = filteredElements.Where(elem =>
                {
                    // Check EDITED_BY parameter (contains "user @ date" in workshared models)
                    var editedBy = elem.get_Parameter(BuiltInParameter.EDITED_BY)?.AsString() ?? "";
                    // Check phase created
                    var phaseCreated = elem.get_Parameter(BuiltInParameter.PHASE_CREATED)?.AsValueString() ?? "";
                    // If element has been edited or we can't determine, include it
                    return !string.IsNullOrEmpty(editedBy) || string.IsNullOrEmpty(modifiedAfter);
                });
            }

            var elementList = filteredElements.ToList();
            var totalCount = elementList.Count;

            var subset = offset > 0 ? elementList.Skip(offset) : (IEnumerable<Element>)elementList;
            if (limit > 0) subset = subset.Take(limit);

            var result = new JArray();
            var syncTimestamp = DateTime.UtcNow.ToString("o"); // ISO 8601

            foreach (var elem in subset)
            {
                // Get level name
                var levelParam = elem.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
                              ?? elem.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                              ?? elem.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                var levelName = levelParam?.AsValueString() ?? "";

                // If no level from parameter, try associated level
                if (string.IsNullOrEmpty(levelName) && elem.LevelId != ElementId.InvalidElementId)
                {
                    var level = doc.GetElement(elem.LevelId) as Level;
                    levelName = level?.Name ?? "";
                }

                // Get mark
                var markParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                var mark = markParam?.AsString() ?? "";

                // Get area
                var areaParam = elem.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)
                             ?? elem.get_Parameter(BuiltInParameter.ROOM_AREA);
                var area = areaParam?.AsDouble() ?? 0.0;

                // Get type name
                var typeElem = doc.GetElement(elem.GetTypeId());
                var typeName = typeElem?.Name ?? "";

                // Get edited-by info (for delta-sync tracking)
                var editedBy = elem.get_Parameter(BuiltInParameter.EDITED_BY)?.AsString() ?? "";

                // Build minified object
                var obj = new JObject
                {
                    ["id"] = elem.Id.Value,
                    ["guid"] = elem.UniqueId,
                    ["name"] = elem.Name,
                    ["category"] = elem.Category?.Name ?? "Unknown",
                    ["level"] = levelName,
                    ["mark"] = mark,
                    ["area"] = Math.Round(area, 2),
                    ["typeName"] = typeName,
                    ["editedBy"] = editedBy,
                };

                // Add Room-specific fields
                if (elem is Room room)
                {
                    obj["roomName"] = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                    obj["roomNumber"] = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "";
                    obj["area"] = Math.Round(room.Area, 2);
                }

                // Add Door-specific fields (using FamilyInstance.FromRoom/ToRoom)
                if (builtInCat == BuiltInCategory.OST_Doors && elem is FamilyInstance doorInst)
                {
                    try
                    {
                        var phase = doc.Phases.get_Item(doc.Phases.Size - 1);
                        var fromRoomObj = doorInst.get_FromRoom(phase);
                        var toRoomObj = doorInst.get_ToRoom(phase);
                        var fromRoom = fromRoomObj != null
                            ? (fromRoomObj.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? fromRoomObj.Name)
                            : "";
                        var toRoom = toRoomObj != null
                            ? (toRoomObj.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? toRoomObj.Name)
                            : "";
                        if (!string.IsNullOrEmpty(fromRoom)) obj["fromRoom"] = fromRoom;
                        if (!string.IsNullOrEmpty(toRoom)) obj["toRoom"] = toRoom;
                    }
                    catch { /* Phase/room lookup may fail for unplaced doors */ }
                }

                result.Add(obj);
            }

            return new JObject
            {
                ["totalCount"] = totalCount,
                ["count"] = result.Count,
                ["offset"] = offset,
                ["limit"] = limit,
                ["hasMore"] = (offset + result.Count) < totalCount,
                ["syncTimestamp"] = syncTimestamp,
                ["elements"] = result
            };
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
                        ["elementId"] = wall.Id.Value,
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
                        throw new InvalidOperationException($"Level '{name}' already exists (id: {existing.Id.Value})");

                    var level = Level.Create(doc, elevation);
                    level.Name = name;

                    tx.Commit();
                    return new JObject
                    {
                        ["elementId"] = level.Id.Value,
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
                        ["elementId"] = grid.Id.Value,
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
                        ["elementId"] = room.Id.Value,
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
                        ["elementId"] = sheet.Id.Value,
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
                            throw new InvalidOperationException($"Element {id.Value} not found");
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

        // Export implementations moved to CommandExecutor.Export.cs


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
                    catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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

        // View creation implementations moved to CommandExecutor.ViewSheet.cs

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
                    try { doc.Delete(fs.Id); deleted++; } catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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
                        catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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

        private static JToken SetVisibilityGraphics(UIDocument uidoc, Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Set Visibility Graphics"))
            {
                tx.Start();
                try
                {
                    var viewId = parameters["viewId"]?.Value<int>();
                    var view = viewId.HasValue
                        ? doc.GetElement(new ElementId(viewId.Value)) as View
                        : uidoc.ActiveView;
                    if (view == null) throw new InvalidOperationException("View not found");

                    var categoryName = parameters["category"]?.ToString();
                    var visible = parameters["visible"]?.Value<bool>();
                    var halftone = parameters["halftone"]?.Value<bool>();
                    var transparency = parameters["transparency"]?.Value<int>();
                    var hideLinks = parameters["hideLinks"]?.Value<bool>();
                    var hideLinkName = parameters["linkName"]?.ToString();
                    var messages = new JArray();

                    // Hide/show categories
                    if (!string.IsNullOrEmpty(categoryName) && visible.HasValue)
                    {
                        var bic = GetBuiltInCategory(categoryName);
                        if (bic == BuiltInCategory.INVALID)
                            throw new InvalidOperationException($"Unknown category: {categoryName}");

                        var cat = Category.GetCategory(doc, bic);
                        if (cat != null && view.CanCategoryBeHidden(cat.Id))
                        {
                            view.SetCategoryHidden(cat.Id, !visible.Value);
                            messages.Add($"{(visible.Value ? "Shown" : "Hidden")} category '{categoryName}'");
                        }
                        else
                        {
                            messages.Add($"Cannot change visibility for '{categoryName}' in this view");
                        }

                        // Apply halftone/transparency to category if specified
                        if (halftone.HasValue || transparency.HasValue)
                        {
                            var ogs = view.GetCategoryOverrides(cat.Id);
                            if (halftone.HasValue) ogs.SetHalftone(halftone.Value);
                            if (transparency.HasValue) ogs.SetSurfaceTransparency(transparency.Value);
                            view.SetCategoryOverrides(cat.Id, ogs);
                            if (halftone.HasValue) messages.Add($"Set halftone={halftone.Value} for '{categoryName}'");
                            if (transparency.HasValue) messages.Add($"Set transparency={transparency.Value} for '{categoryName}'");
                        }
                    }

                    // Hide/show ALL linked models
                    if (hideLinks.HasValue)
                    {
                        var linkCat = Category.GetCategory(doc, BuiltInCategory.OST_RvtLinks);
                        if (linkCat != null && view.CanCategoryBeHidden(linkCat.Id))
                        {
                            view.SetCategoryHidden(linkCat.Id, hideLinks.Value);
                            messages.Add(hideLinks.Value ? "Hidden all Revit links" : "Shown all Revit links");
                        }
                    }

                    // Hide/show a specific linked model by name
                    if (!string.IsNullOrEmpty(hideLinkName))
                    {
                        var vis = visible ?? false;
                        var links = new FilteredElementCollector(doc)
                            .OfClass(typeof(RevitLinkInstance))
                            .Where(e => e.Name.IndexOf(hideLinkName, StringComparison.OrdinalIgnoreCase) >= 0)
                            .ToList();

                        foreach (var link in links)
                        {
                            if (!vis)
                                view.HideElements(new List<ElementId> { link.Id });
                            else
                                view.UnhideElements(new List<ElementId> { link.Id });
                        }
                        messages.Add($"{(vis ? "Shown" : "Hidden")} {links.Count} link instance(s) matching '{hideLinkName}'");
                    }

                    if (messages.Count == 0)
                        messages.Add("No changes made. Specify category+visible, hideLinks, or linkName.");

                    tx.Commit();
                    return new JObject { ["message"] = string.Join("; ", messages), ["changes"] = messages };
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
                                try { t.Name = t.Name.Replace(find, replace); renamed++; } catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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
                                try { v.Name = v.Name.Replace(find, replace); renamed++; } catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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
                                try { s.Name = s.Name.Replace(find, replace); renamed++; } catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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

                    // No cap — copy all matching elements

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
                                    try { doc.Delete(id); resolved++; } catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
                                }
                            }

                            // Room separation line — try unjoin
                            if (desc.Contains("overlap") && desc.Contains("room separation"))
                            {
                                var ids = w.GetFailingElements();
                                if (ids.Count > 1)
                                {
                                    try { doc.Delete(ids.Last()); resolved++; } catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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
                                catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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
                            catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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
                        catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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
                                        catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
                                    }
                                    else if (innerObj is Arc arc && arc.Length > 0.001)
                                    {
                                        try
                                        {
                                            doc.Create.NewDetailCurve(doc.ActiveView, arc);
                                            linesFromImport++;
                                        }
                                        catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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
                                            catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
                                        }
                                    }
                                }
                            }
                        }

                        totalLines += linesFromImport;
                        convertedImports.Add(import.Id.Value);

                        if (deleteAfter && linesFromImport > 0)
                        {
                            try { doc.Delete(import.Id); } catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
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

        // View/Sheet AI-declared implementations moved to CommandExecutor.ViewSheet.cs

        // ===== PROJECT DATA MANAGEMENT IMPLEMENTATIONS =====

        private static JToken SaveProjectData(Document doc, JObject parameters)
        {
            var key = parameters["key"]?.ToString();
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("'key' is required");

            var data = parameters["data"] ?? parameters["value"];
            if (data == null)
                throw new InvalidOperationException("'data' is required");

            var projectPath = doc.PathName ?? "Untitled";
            ProjectDataService.SaveData(projectPath, key, data);

            return new JObject
            {
                ["message"] = $"✅ Saved data with key '{key}' for project '{Path.GetFileNameWithoutExtension(projectPath)}'",
                ["key"] = key,
                ["folder"] = ProjectDataService.GetFolderPath(projectPath)
            };
        }

        private static JToken LoadProjectData(Document doc, JObject parameters)
        {
            var key = parameters["key"]?.ToString();
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("'key' is required");

            var projectPath = doc.PathName ?? "Untitled";
            var data = ProjectDataService.LoadData(projectPath, key);

            if (data == null)
                return new JObject
                {
                    ["message"] = $"⚠ No data found with key '{key}'",
                    ["found"] = false
                };

            return new JObject
            {
                ["message"] = $"✅ Loaded data with key '{key}'",
                ["found"] = true,
                ["key"] = key,
                ["data"] = data
            };
        }

        private static JToken ListProjectData(Document doc)
        {
            var projectPath = doc.PathName ?? "Untitled";
            var files = ProjectDataService.ListData(projectPath);

            var items = new JArray();
            foreach (var f in files)
            {
                items.Add(new JObject
                {
                    ["key"] = f.Key,
                    ["savedAt"] = f.SavedAt,
                    ["sizeBytes"] = f.SizeBytes
                });
            }

            return new JObject
            {
                ["message"] = $"✅ Found {files.Count} saved data file(s)",
                ["count"] = files.Count,
                ["items"] = items,
                ["folder"] = ProjectDataService.GetFolderPath(projectPath)
            };
        }

        private static JToken DeleteProjectData(Document doc, JObject parameters)
        {
            var key = parameters["key"]?.ToString();
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("'key' is required");

            var projectPath = doc.PathName ?? "Untitled";
            var deleted = ProjectDataService.DeleteData(projectPath, key);

            return new JObject
            {
                ["message"] = deleted ? $"✅ Deleted data with key '{key}'" : $"⚠ No data found with key '{key}'",
                ["deleted"] = deleted
            };
        }

        private static JToken SaveModelSnapshot(Document doc)
        {
            // Capture a summary of the current model state
            var snapshot = new JObject();

            // Element counts by category
            var categories = new JObject();
            var allElements = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToList();
            var categorizedCounts = allElements
                .Where(e => e.Category != null)
                .GroupBy(e => e.Category.Name)
                .OrderByDescending(g => g.Count())
                .Take(30);
            foreach (var group in categorizedCounts)
                categories[group.Key] = group.Count();
            snapshot["elementCounts"] = categories;

            // Warning count
            snapshot["warningCount"] = doc.GetWarnings().Count;

            // Levels
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            snapshot["levelCount"] = levels.Count;
            var levelList = new JArray();
            foreach (var l in levels.OrderBy(l => l.Elevation))
                levelList.Add(new JObject { ["name"] = l.Name, ["elevation"] = Math.Round(l.Elevation, 2) });
            snapshot["levels"] = levelList;

            // Views and Sheets
            var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate).ToList();
            snapshot["viewCount"] = views.Count;
            snapshot["sheetCount"] = views.Count(v => v is ViewSheet);

            // Room count
            var rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().ToList();
            snapshot["roomCount"] = rooms.Count;

            snapshot["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            snapshot["projectName"] = Path.GetFileNameWithoutExtension(doc.PathName ?? "Untitled");

            // Save it
            var projectPath = doc.PathName ?? "Untitled";
            var snapshotKey = $"snapshot_{DateTime.Now:yyyyMMdd_HHmmss}";
            ProjectDataService.SaveData(projectPath, snapshotKey, snapshot);

            return new JObject
            {
                ["message"] = $"✅ Model snapshot saved as '{snapshotKey}'",
                ["key"] = snapshotKey,
                ["snapshot"] = snapshot
            };
        }

        // ===== ADDITIONAL QUERY TOOL IMPLEMENTATIONS =====

        private static JToken CreateViewFilter(UIDocument uidoc, Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create View Filter"))
            {
                tx.Start();
                try
                {
                    var filterName = parameters["filterName"]?.ToString();
                    var categoryName = parameters["category"]?.ToString();
                    var paramName = parameters["parameterName"]?.ToString();
                    var ruleType = parameters["ruleType"]?.ToString()?.ToLower() ?? "equals";
                    var ruleValue = parameters["value"]?.ToString() ?? "";
                    var applyToView = parameters["applyToView"]?.Value<bool>() ?? true;

                    if (string.IsNullOrEmpty(filterName)) filterName = $"AI Filter - {paramName} {ruleType} {ruleValue}";
                    if (string.IsNullOrEmpty(categoryName)) throw new InvalidOperationException("'category' required");
                    if (string.IsNullOrEmpty(paramName)) throw new InvalidOperationException("'parameterName' required");

                    var bic = GetBuiltInCategory(categoryName);
                    var catIds = new List<ElementId> { new ElementId(bic) };

                    // Find the parameter to filter by
                    var sampleElem = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().FirstOrDefault();
                    if (sampleElem == null) throw new InvalidOperationException($"No elements of category '{categoryName}' found");

                    ParameterElement? paramElem = null;
                    foreach (Parameter p in sampleElem.Parameters)
                    {
                        if (p.Definition.Name == paramName)
                        {
                            if (p.Id != null)
                            {
                                // Try to find ParameterElement
                                var pe = doc.GetElement(p.Id) as ParameterElement;
                                if (pe != null) paramElem = pe;
                            }
                            break;
                        }
                    }

                    // Create the filter
                    var filter = ParameterFilterElement.Create(doc, filterName, catIds);

                    // Apply to active view if requested
                    if (applyToView && uidoc.ActiveView != null)
                    {
                        var activeView = uidoc.ActiveView;
                        activeView.AddFilter(filter.Id);
                        activeView.SetFilterVisibility(filter.Id, true);

                        // Set an override (red color to highlight)
                        var overrides = new OverrideGraphicSettings();
                        overrides.SetProjectionLineColor(new Autodesk.Revit.DB.Color(255, 0, 0));
                        activeView.SetFilterOverrides(filter.Id, overrides);
                    }

                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"✅ Created view filter '{filterName}' for {categoryName}",
                        ["filterId"] = filter.Id.Value,
                        ["filterName"] = filterName
                    };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

        private static JToken GetWorksets(Document doc)
        {
            if (!doc.IsWorkshared)
                return new JObject { ["message"] = "This project is not workshared", ["workshared"] = false };

            var worksets = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToWorksets()
                .ToList();

            var result = new JArray();
            foreach (var ws in worksets)
            {
                result.Add(new JObject
                {
                    ["id"] = ws.Id.IntegerValue,
                    ["name"] = ws.Name,
                    ["isOpen"] = ws.IsOpen,
                    ["isDefault"] = ws.IsDefaultWorkset,
                    ["isVisible"] = ws.IsVisibleByDefault,
                    ["owner"] = ws.Owner ?? ""
                });
            }

            return new JObject
            {
                ["message"] = $"✅ Found {result.Count} workset(s)",
                ["workshared"] = true,
                ["worksets"] = result
            };
        }

        private static JToken GetAreas(Document doc)
        {
            var areas = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Areas)
                .WhereElementIsNotElementType()
                .ToList();

            var result = new JArray();
            foreach (var a in areas)
            {
                var areaParam = a.get_Parameter(BuiltInParameter.ROOM_AREA);
                var nameParam = a.get_Parameter(BuiltInParameter.ROOM_NAME);
                var numberParam = a.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                var levelParam = a.get_Parameter(BuiltInParameter.LEVEL_PARAM);

                result.Add(new JObject
                {
                    ["id"] = a.Id.Value,
                    ["name"] = nameParam?.AsString() ?? "",
                    ["number"] = numberParam?.AsString() ?? "",
                    ["area"] = areaParam?.AsDouble() ?? 0,
                    ["areaFormatted"] = areaParam?.AsValueString() ?? "0",
                    ["level"] = levelParam?.AsValueString() ?? ""
                });
            }

            // Also list area plans
            var areaPlans = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => v.ViewType == ViewType.AreaPlan && !v.IsTemplate)
                .ToList();

            var planList = new JArray();
            foreach (var ap in areaPlans)
                planList.Add(new JObject { ["id"] = ap.Id.Value, ["name"] = ap.Name });

            return new JObject
            {
                ["message"] = $"✅ Found {result.Count} area(s) in {planList.Count} area plan(s)",
                ["areas"] = result,
                ["areaPlans"] = planList
            };
        }

        private static JToken GetDesignOptions(Document doc)
        {
            var options = new FilteredElementCollector(doc)
                .OfClass(typeof(DesignOption))
                .Cast<DesignOption>()
                .ToList();

            if (options.Count == 0)
                return new JObject { ["message"] = "No design options in this project", ["count"] = 0 };

            var result = new JArray();
            foreach (var opt in options)
            {
                result.Add(new JObject
                {
                    ["id"] = opt.Id.Value,
                    ["name"] = opt.Name,
                    ["isPrimary"] = opt.IsPrimary
                });
            }

            return new JObject
            {
                ["message"] = $"✅ Found {options.Count} design option(s)",
                ["designOptions"] = result
            };
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
                // Annotation & display categories
                { "Levels", BuiltInCategory.OST_Levels },
                { "Grids", BuiltInCategory.OST_Grids },
                { "Sections", BuiltInCategory.OST_Sections },
                { "Elevations", BuiltInCategory.OST_Elev },
                { "Callouts", BuiltInCategory.OST_Callouts },
                { "Revit Links", BuiltInCategory.OST_RvtLinks },
                { "RVT Links", BuiltInCategory.OST_RvtLinks },
                { "Dimensions", BuiltInCategory.OST_Dimensions },
                { "Text Notes", BuiltInCategory.OST_TextNotes },
                { "Detail Items", BuiltInCategory.OST_DetailComponents },
                { "Lines", BuiltInCategory.OST_Lines },
                { "Scope Boxes", BuiltInCategory.OST_VolumeOfInterest },
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
            throw new NotImplementedException(
                $"Command '{command}' is not yet implemented. " +
                "You can use the 'execute_code' tool to achieve this with custom C# code against the Revit API.");
        }

        // ===== INTEGRATION STATUS =====

        private static JToken GetIntegrationStatus()
        {
            try
            {
                var settings = AI.IntegrationSettings.Load();
                return new JObject
                {
                    ["integrations"] = new JObject
                    {
                        ["excel"] = new JObject
                        {
                            ["enabled"] = settings.ExcelEnabled,
                            ["configured"] = true,
                            ["status"] = settings.ExcelEnabled ? "ready" : "disabled"
                        },
                        ["notion"] = new JObject
                        {
                            ["enabled"] = settings.NotionEnabled,
                            ["configured"] = settings.IsNotionConfigured,
                            ["hasApiKey"] = !string.IsNullOrWhiteSpace(settings.NotionApiKey) && settings.NotionApiKey != "your_notion_integration_token",
                            ["hasDatabaseId"] = !string.IsNullOrWhiteSpace(settings.NotionDatabaseId),
                            ["status"] = !settings.NotionEnabled ? "disabled" : settings.IsNotionConfigured ? "ready" : "not_configured"
                        },
                        ["googleSheets"] = new JObject
                        {
                            ["enabled"] = settings.GoogleSheetsEnabled,
                            ["configured"] = settings.IsGoogleSheetsConfigured,
                            ["googleSignedIn"] = settings.GoogleSignedIn,
                            ["googleEmail"] = settings.GoogleEmail ?? "",
                            ["hasCredentialsPath"] = !string.IsNullOrWhiteSpace(settings.GoogleSheetsCredentialsPath),
                            ["hasSpreadsheetId"] = !string.IsNullOrWhiteSpace(settings.GoogleSheetsSpreadsheetId),
                            ["defaultSpreadsheetId"] = settings.GoogleSheetsSpreadsheetId ?? "",
                            ["status"] = !settings.GoogleSheetsEnabled ? "disabled" : settings.IsGoogleSheetsConfigured ? "ready" : "not_configured"
                        },
                        ["sqlite"] = new JObject
                        {
                            ["enabled"] = settings.SqliteEnabled,
                            ["configured"] = true,
                            ["status"] = settings.SqliteEnabled ? "ready" : "disabled"
                        },
                        ["ollama"] = new JObject
                        {
                            ["enabled"] = settings.OllamaEnabled,
                            ["configured"] = settings.IsOllamaConfigured,
                            ["url"] = settings.OllamaUrl ?? "",
                            ["model"] = settings.OllamaModel ?? "",
                            ["status"] = !settings.OllamaEnabled ? "disabled" : settings.IsOllamaConfigured ? "ready" : "not_configured"
                        }
                    },
                    ["tip"] = "Use the 🔗 Integrations button in the chat header to enable/configure integrations. Export tools run via the MCP server (Claude/MCP clients). Use 'export_elements' to get Revit data for manual export."
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["error"] = $"Failed to load integration settings: {ex.Message}",
                    ["tip"] = "Click the 🔗 Integrations button in the chat header to configure."
                };
            }
        }

        private static JToken PrepareIntegrationExport(Document doc, string command, JObject parameters)
        {
            var settings = AI.IntegrationSettings.Load();
            var category = parameters["category"]?.ToString() ?? "";
            var targetService = command.Replace("export_to_", "").Replace("_integration", "");

            // Check if target integration is enabled
            bool isEnabled = false;
            bool isConfigured = false;
            string serviceName = "";

            switch (targetService)
            {
                case "excel":
                    isEnabled = settings.ExcelEnabled;
                    isConfigured = true;
                    serviceName = "Excel";
                    break;
                case "notion":
                    isEnabled = settings.NotionEnabled;
                    isConfigured = settings.IsNotionConfigured;
                    serviceName = "Notion";
                    break;
                case "google_sheets":
                    isEnabled = settings.GoogleSheetsEnabled;
                    isConfigured = settings.IsGoogleSheetsConfigured;
                    serviceName = "Google Sheets";
                    break;
            }

            if (!isEnabled)
            {
                return new JObject
                {
                    ["error"] = $"{serviceName} integration is disabled.",
                    ["action"] = $"Tell the user to enable {serviceName} via the 🔗 Integrations button in the chat header.",
                    ["integrationStatus"] = GetIntegrationStatus()
                };
            }

            if (!isConfigured)
            {
                return new JObject
                {
                    ["error"] = $"{serviceName} is enabled but not configured.",
                    ["action"] = $"Tell the user to configure {serviceName} credentials via the 🔗 Integrations button.",
                    ["integrationStatus"] = GetIntegrationStatus()
                };
            }

            // Collect the Revit data
            var elementData = ExportElements(doc, new JObject
            {
                ["category"] = category,
                ["limit"] = 500
            });

            return new JObject
            {
                ["status"] = "data_ready",
                ["targetService"] = serviceName,
                ["category"] = category,
                ["elementCount"] = elementData["totalCount"],
                ["data"] = elementData,
                ["message"] = $"Revit data collected for {serviceName} export. The actual push to {serviceName} is handled by the MCP server's export tools (export_to_excel, export_to_notion, export_to_sheets). " +
                              $"If using Claude/MCP, the data will be pushed automatically. From the built-in chat, tell the user the data is ready and they can use the Tools Hub or ask Claude to push it."
            };
        }

        // ===== PROJECT FILES TOOL IMPLEMENTATIONS =====

        private static JToken ExportElementsToCsv(Document doc, JObject parameters)
        {
            var category = parameters?["category"]?.ToString();
            var fileName = parameters?["fileName"]?.ToString() ?? $"export_{category ?? "elements"}_{DateTime.Now:yyyyMMdd_HHmmss}";
            var paramNames = parameters?["parameters"]?.ToObject<List<string>>();

            if (string.IsNullOrEmpty(category))
                throw new InvalidOperationException("'category' is required (e.g. 'Walls', 'Levels', 'Rooms')");

            var bic = GetBuiltInCategory(category);
            var collector = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType();
            var elements = collector.ToList();

            var rows = new List<Dictionary<string, string>>();
            foreach (var elem in elements.Take(500))
            {
                var row = new Dictionary<string, string>
                {
                    ["Id"] = elem.Id.Value.ToString(),
                    ["Name"] = elem.Name ?? "",
                    ["Category"] = elem.Category?.Name ?? ""
                };

                // Add parameters
                foreach (Parameter p in elem.Parameters)
                {
                    if (p.Definition == null) continue;
                    if (paramNames != null && !paramNames.Any(n => p.Definition.Name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                        continue;

                    var key = p.Definition.Name;
                    if (!row.ContainsKey(key))
                        row[key] = p.AsValueString() ?? p.AsString() ?? "";
                }

                rows.Add(row);
            }

            var filePath = ProjectFilesService.ExportToCsv(doc.PathName, fileName, rows);

            return new JObject
            {
                ["message"] = $"✅ Exported {rows.Count} {category} elements to CSV",
                ["fileName"] = Path.GetFileName(filePath),
                ["filePath"] = filePath,
                ["elementCount"] = rows.Count,
                ["columnCount"] = rows.FirstOrDefault()?.Keys.Count ?? 0
            };
        }

        private static JToken ExportElementsToExcel(Document doc, JObject parameters)
        {
            // Uses the same export but with .xlsx-compatible CSV format
            var result = ExportElementsToCsv(doc, parameters);
            if (result is JObject obj)
                obj["message"] = obj["message"]?.ToString().Replace("CSV", "Excel-compatible CSV");
            return result;
        }

        private static JToken ImportFromProjectFile(Document doc, JObject parameters)
        {
            var rows = ProjectFilesService.ImportFromFile(doc.PathName, parameters);

            if (rows.Count == 0)
                return new JObject { ["message"] = "⚠ No data rows found in the file." };

            // Preview — show what would be imported
            var preview = new JArray();
            foreach (var row in rows.Take(10))
            {
                var obj = new JObject();
                foreach (var kvp in row)
                    obj[kvp.Key] = kvp.Value;
                preview.Add(obj);
            }

            return new JObject
            {
                ["message"] = $"📋 Found {rows.Count} rows to import. Preview of first {Math.Min(10, rows.Count)} rows:",
                ["totalRows"] = rows.Count,
                ["columns"] = JArray.FromObject(rows.FirstOrDefault()?.Keys.ToList() ?? new List<string>()),
                ["preview"] = preview,
                ["hint"] = "To apply these values to Revit elements, use 'batch_set_parameter' with the data."
            };
        }

        // Cleanup implementations moved to CommandExecutor.Cleanup.cs


        // ===== SHARED HELPER =====

        private static Level FindLevel(Document doc, string levelName)
        {
            if (string.IsNullOrEmpty(levelName)) return null;
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase)
                    || l.Name.IndexOf(levelName, StringComparison.OrdinalIgnoreCase) >= 0);
        }


        // ===== MEP, STRUCTURAL, ANNOTATION, ARCHITECTURE, SITE =====
        // These methods are in partial class files:
        //   CommandExecutor.Mep.cs
        //   CommandExecutor.Structural.cs
        //   CommandExecutor.Annotation.cs
        //   CommandExecutor.Architecture.cs
        //   CommandExecutor.Site.cs


        // Utility implementations moved to CommandExecutor.Utility.cs


        // ===== POWER BI EXPORT =====

        private static JToken ExportToPowerBI(UIDocument uidoc, Document doc, JObject parameters)
        {
            var exportScope = parameters["exportScope"]?.ToString() ?? "currentView";
            var dbPath = parameters["dbPath"]?.ToString();
            var mode = parameters["mode"]?.ToString() ?? "new";
            var categoriesStr = parameters["categories"]?.ToString();

            // Resolve DB path
            var dataFolder = Path.Combine(
                Path.GetDirectoryName(doc.PathName) ?? "",
                "data");
            dbPath = PowerBISqliteWriter.ResolveDbPath(dbPath, dataFolder);

            // Find/validate the 3D view to export from
            View3D exportView = null;

            if (exportScope == "currentView")
            {
                exportView = uidoc.ActiveView as View3D;
                if (exportView == null)
                    throw new InvalidOperationException(
                        "Active view is not a 3D view. Please open a 3D view or use exportScope='allModel'.");
            }
            else
            {
                // Find the default {3D} view
                exportView = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate);

                if (exportView == null)
                    throw new InvalidOperationException(
                        "No 3D view found in the project. Create a 3D view first.");
            }

            // Parse category filter
            string[] categoryFilter = null;
            if (!string.IsNullOrWhiteSpace(categoriesStr))
            {
                categoryFilter = categoriesStr
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToArray();
            }

            // Step 1: Extract geometry via IExportContext
            var context = new PowerBIExportContext(doc);
            var exporter = new CustomExporter(doc, context);
            exporter.IncludeGeometricObjects = false; // we only need tessellated mesh
            exporter.ShouldStopOnError = false;
            exporter.Export(exportView);

            var meshData = context.MeshDataByElement;

            // Step 2: Collect element metadata
            var elementIds = meshData.Keys.ToList();
            var elements = context.ExtractElementMetadata(elementIds, categoryFilter);

            // If category filter was applied, also filter meshData
            if (categoryFilter != null)
            {
                var validIds = new HashSet<int>(elements.Select(e => e.ElementId));
                var filteredMesh = new Dictionary<int, MeshData>();
                foreach (var kvp in meshData)
                {
                    if (validIds.Contains(kvp.Key))
                        filteredMesh[kvp.Key] = kvp.Value;
                }
                meshData = filteredMesh;
            }

            // Step 3: Get category colors
            var categories = elements.Select(e => e.Category).Distinct();
            var colors = context.GetCategoryColors(categories);

            // Step 4: Build metadata
            var metadata = new Dictionary<string, string>
            {
                ["projectName"] = doc.ProjectInformation?.Name ?? "Unknown",
                ["exportDate"] = DateTime.Now.ToString("o"),
                ["exportScope"] = exportScope,
                ["exportViewName"] = exportView.Name,
                ["unitSystem"] = "meters",
                ["elementCount"] = elements.Count.ToString(),
                ["revitVersion"] = doc.Application.VersionNumber,
                ["filePath"] = doc.PathName
            };

            // Step 5: Write to SQLite
            var writer = new PowerBISqliteWriter();
            var result = writer.Write(elements, meshData, colors, metadata, dbPath, mode);

            // Compute total triangles
            int totalTriangles = meshData.Values.Sum(m => m.FaceCount);
            int totalVertices = meshData.Values.Sum(m => m.VertexCount);

            return new JObject
            {
                ["message"] = $"✅ Exported {result.ElementCount} elements with 3D geometry to Power BI SQLite",
                ["dbPath"] = result.DbPath,
                ["elementCount"] = result.ElementCount,
                ["geometryCount"] = result.GeometryCount,
                ["parameterCount"] = result.ParameterCount,
                ["totalVertices"] = totalVertices,
                ["totalTriangles"] = totalTriangles,
                ["categories"] = new JArray(colors.Keys.ToArray()),
                ["fileSize"] = result.FileSizeFormatted,
                ["mode"] = result.Mode,
                ["exportScope"] = exportScope,
                ["exportView"] = exportView.Name
            };
        }

    }
}

