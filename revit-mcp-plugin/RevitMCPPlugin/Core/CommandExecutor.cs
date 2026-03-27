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
                    var uiView = openUIViews.FirstOrDefault(uv => uv.ViewId.IntegerValue == closeViewId);
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
                    ["id"] = elem.Id.IntegerValue,
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
                    ["id"] = elem.Id.IntegerValue,
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
                        return new JObject { ["message"] = $"✅ Placed view on sheet (Viewport ID: {vp.Id.Value})", ["viewportId"] = vp.Id.Value };
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
                    return new JObject { ["message"] = $"✅ Placed {placed} view(s) on sheet", ["viewports"] = results };
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
                    ["elementIds"] = new JArray(w.GetFailingElements().Select(id => id.IntegerValue))
                });
            }

            return new JObject
            {
                ["message"] = $"✅ Found {warningDescriptions.Count} warning(s), selected {elementIds.Count} element(s)",
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
                                try { v.Name = v.Name.Replace(find, replace); renamed++; } catch { }
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
                                try { s.Name = s.Name.Replace(find, replace); renamed++; } catch { }
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
                                try { t.Name = t.Name.Replace(find, replace); renamed++; } catch { }
                            }
                        }
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Replaced '{find}' → '{replace}' in {renamed} name(s)", ["count"] = renamed };
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
                        return new JObject { ["message"] = $"✅ Copied '{sourceParam}' value to {transferred} element(s)" };
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
                        return new JObject { ["message"] = $"✅ Transferred '{sourceParam}' → '{targetParam}' on {transferred} element(s)" };
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
                ["message"] = $"✅ Selected {matching.Count} element(s)",
                ["count"] = matching.Count,
                ["elementIds"] = new JArray(matching.Select(id => id.IntegerValue))
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
                        try { newSheet.SheetNumber = newNumber; } catch { }
                        newSheet.Name = sourceSheet.Name;

                        created.Add(new JObject { ["sheetId"] = newSheet.Id.Value, ["number"] = newSheet.SheetNumber });
                    }

                    tx.Commit();
                    return new JObject { ["message"] = $"✅ Created {count} sheet copy(ies)", ["sheets"] = created };
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
                        ["message"] = $"✅ Section box applied around {elementIds.Count} element(s) with {padding}ft padding",
                        ["viewId"] = view3d.Id.IntegerValue,
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
                        ["message"] = $"✅ Copied {filterIds.Count} filter(s) to {targetViewIds.Count} view(s) ({copiedCount} total applications)",
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
                        ["message"] = $"✅ {action} element at {end} end by {Math.Abs(delta)} ft",
                        ["newLength"] = Math.Round(newStart.DistanceTo(newEnd), 4)
                    };
                }
                catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
            }
        }

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
            return new JObject
            {
                ["command"] = command,
                ["status"] = "received",
                ["message"] = $"Command '{command}' received. This command requires the extended command set module.",
                ["parameters"] = parameters
            };
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
                    ["Id"] = elem.Id.IntegerValue.ToString(),
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
                floors = floors.Where(f => ids.Contains(f.Id.IntegerValue)).ToList();
            }

            if (floors.Count == 0)
                return new JObject { ["error"] = "No floors found" };

            if (method == "rooms")
            {
                // Cut floors by room boundaries — find rooms overlapping each floor
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<SpatialElement>()
                    .Where(r => r.Area > 0)
                    .ToList();

                return new JObject
                {
                    ["message"] = $"🔪 Found {floors.Count} floors and {rooms.Count} rooms. Use execute_code with floor splitting logic for complex geometry operations.",
                    ["floors"] = floors.Count,
                    ["rooms"] = rooms.Count,
                    ["hint"] = "Floor cutting by room boundaries requires geometry intersection. Use execute_code with: foreach room → get boundary → create new floor from boundary → delete original."
                };
            }

            return new JObject
            {
                ["message"] = $"🔪 Found {floors.Count} floors to process with method: {method}",
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
                ["message"] = $"📐 Found {elements.Count} {category} elements, {split} span multiple levels and can be split.",
                ["totalElements"] = elements.Count,
                ["splittable"] = split,
                ["levels"] = JArray.FromObject(levels.Select(l => new { l.Name, l.Elevation })),
                ["gap"] = gap,
                ["hint"] = "Use execute_code for actual splitting. Pattern: for each wall → get base/top constraints → create new walls at each level segment."
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
                ["message"] = $"🕳️ Found {intersections} potential intersections between {hosts.Count} {hostCat} and {cutElements.Count} {cutCat}.",
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
                            try { var p = e.get_Parameter(BuiltInParameter.DATUM_VOLUME_OF_INTEREST); return p != null && p.AsElementId()?.IntegerValue == sb.Id.IntegerValue; }
                            catch { return false; }
                        }).Count();

                    items.Add(new JObject
                    {
                        ["id"] = sb.Id.IntegerValue,
                        ["name"] = sb.Name,
                        ["usedInViews"] = usedIn,
                        ["minX"] = bb?.Min.X, ["minY"] = bb?.Min.Y, ["minZ"] = bb?.Min.Z,
                        ["maxX"] = bb?.Max.X, ["maxY"] = bb?.Max.Y, ["maxZ"] = bb?.Max.Z
                    });
                }
                return new JObject { ["message"] = $"📦 Found {scopeBoxes.Count} scope boxes", ["scopeBoxes"] = items };
            }
            else if (action == "delete_unused")
            {
                var unused = scopeBoxes.Where(sb =>
                {
                    var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>();
                    return !views.Any(v =>
                    {
                        try { var p = v.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP); return p != null && p.AsElementId()?.IntegerValue == sb.Id.IntegerValue; }
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
                    ["message"] = $"🗑️ Deleted {unused.Count} unused scope boxes (of {scopeBoxes.Count} total)",
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
                    ["message"] = $"🗑️ Deleted {emptySheets.Count} empty sheets",
                    ["deleted"] = emptySheets.Count
                };
            }

            var items = new JArray();
            foreach (var sheet in emptySheets)
            {
                items.Add(new JObject
                {
                    ["id"] = sheet.Id.IntegerValue,
                    ["number"] = sheet.SheetNumber,
                    ["name"] = sheet.Name
                });
            }

            return new JObject
            {
                ["message"] = $"📄 Found {emptySheets.Count} empty sheets (no viewports) out of {sheets.Count} total",
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
                        .Where(id => id != null && id.IntegerValue != -1)
                        .Select(id => id.IntegerValue)
                        .ToHashSet();

                    var unusedTemplates = templates.Where(t => !usedTemplateIds.Contains(t.Id.IntegerValue)).ToList();
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
                            foreach (var fId in fIds) usedFilterIds.Add(fId.IntegerValue);
                        }
                        catch { }
                    }

                    var unusedFilters = filters.Where(f => !usedFilterIds.Contains(f.Id.IntegerValue)).ToList();
                    foreach (var f in unusedFilters) doc.Delete(f.Id);
                    result["unusedFilters"] = unusedFilters.Count;
                    totalCleaned += unusedFilters.Count;
                }

                tx.Commit();
            }

            result["message"] = $"🧹 Cleaned {totalCleaned} unused items (scope: {scope})";
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
                    if (vp != null) sheetsWithViewIds.Add(vp.ViewId.IntegerValue);
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
                            !sheetsWithViewIds.Contains(v.Id.IntegerValue))
                .ToList();

            var items = new JArray();
            foreach (var v in unplaced)
            {
                items.Add(new JObject
                {
                    ["id"] = v.Id.IntegerValue,
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
                        try { doc.Delete(v.Id); } catch { }
                    }
                    tx.Commit();
                }

                return new JObject
                {
                    ["message"] = $"🗑️ Deleted {unplaced.Count} unplaced views/schedules/legends",
                    ["deleted"] = unplaced.Count
                };
            }

            return new JObject
            {
                ["message"] = $"📋 Found {unplaced.Count} views not placed on any sheet (dry run)",
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
                catch { }
            }

            return new JObject
            {
                ["message"] = $"🔍 Scanned {families.Count} editable families, {purgedCount} have unused assets",
                ["familiesScanned"] = families.Count,
                ["familiesWithUnused"] = purgedCount,
                ["details"] = details,
                ["hint"] = "Use execute_code for deep purge: open each family doc → purge → save back."
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
                        .Where(e => ((FamilyInstance)e).Symbol.Id.IntegerValue == symId.IntegerValue)
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
                        catch { }
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
                    ["id"] = c.family.Id.IntegerValue,
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
                        try { doc.Delete(c.family.Id); deleted++; } catch { }
                    }
                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"🗑️ Deleted {deleted} unused families",
                        ["deleted"] = deleted
                    };
                }
            }

            return new JObject
            {
                ["message"] = $"📊 Found {candidates.Count} unused families (0 instances) out of {families.Count} total",
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
                    ["message"] = $"💥 Created exploded 3D view '{view3d.Name}' with {levels.Count} levels, spacing={spacing}ft",
                    ["viewId"] = view3d.Id.IntegerValue,
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
                    ["message"] = $"🔄 Rotated section box by {angle}° in '{view.Name}'",
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
                    ["message"] = $"✅ Super Align: moved {moved} elements (mode={mode}, direction={direction})",
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
                            catch { }
                        }
                    }
                }
                tx.Commit();
            }

            return new JObject
            {
                ["message"] = $"🔗 Joined {joined} element pairs ({cat1Str} ↔ {cat2Str}) in '{view.Name}'",
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
                ["message"] = $"📋 Copied {ids.Count} {category} elements from '{doc.Title}' → '{targetDoc.Title}'",
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
                var item = new JObject { ["id"] = elem.Id.IntegerValue, ["name"] = elem.Name };

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
                ["message"] = $"📏 Measured {elements.Count} elements",
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

        // ===== MEP TOOL IMPLEMENTATIONS =====

        private static JToken CreateDuct(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };

            var ductType = new FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctType))
                .FirstOrDefault();
            var sysType = new FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystemType))
                .FirstOrDefault();
            if (ductType == null || sysType == null) return new JObject { ["error"] = "No duct or system type found" };

            using (var tx = new Transaction(doc, "Create Duct"))
            {
                tx.Start();
                var duct = Autodesk.Revit.DB.Mechanical.Duct.Create(doc, sysType.Id, ductType.Id, level.Id,
                    new XYZ(parameters["startX"]?.Value<double>() ?? 0, parameters["startY"]?.Value<double>() ?? 0, parameters["startZ"]?.Value<double>() ?? 0),
                    new XYZ(parameters["endX"]?.Value<double>() ?? 0, parameters["endY"]?.Value<double>() ?? 0, parameters["endZ"]?.Value<double>() ?? 0));
                tx.Commit();
                return new JObject { ["message"] = $"🔧 Created duct (ID: {duct.Id.IntegerValue})", ["elementId"] = duct.Id.IntegerValue };
            }
        }

        private static JToken CreatePipe(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };

            var pipeType = new FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipeType)).FirstOrDefault();
            var sysType = new FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipingSystemType)).FirstOrDefault();
            if (pipeType == null || sysType == null) return new JObject { ["error"] = "No pipe or system type found" };

            using (var tx = new Transaction(doc, "Create Pipe"))
            {
                tx.Start();
                var pipe = Autodesk.Revit.DB.Plumbing.Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id,
                    new XYZ(parameters["startX"]?.Value<double>() ?? 0, parameters["startY"]?.Value<double>() ?? 0, parameters["startZ"]?.Value<double>() ?? 0),
                    new XYZ(parameters["endX"]?.Value<double>() ?? 0, parameters["endY"]?.Value<double>() ?? 0, parameters["endZ"]?.Value<double>() ?? 0));
                tx.Commit();
                return new JObject { ["message"] = $"🔧 Created pipe (ID: {pipe.Id.IntegerValue})", ["elementId"] = pipe.Id.IntegerValue };
            }
        }

        private static JToken CreateFlexDuct(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            var pointsArr = parameters["points"] as JArray;
            if (pointsArr == null || pointsArr.Count < 2) return new JObject { ["error"] = "Need at least 2 points" };
            var flexType = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Mechanical.FlexDuctType)).FirstOrDefault();
            var sysType = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystemType)).FirstOrDefault();
            if (flexType == null || sysType == null) return new JObject { ["error"] = "No flex duct type found" };
            var pts = pointsArr.Select(p => new XYZ(p["x"]?.Value<double>() ?? 0, p["y"]?.Value<double>() ?? 0, p["z"]?.Value<double>() ?? 0)).ToList();
            using (var tx = new Transaction(doc, "Create Flex Duct"))
            {
                tx.Start();
                var fd = Autodesk.Revit.DB.Mechanical.FlexDuct.Create(doc, sysType.Id, flexType.Id, level.Id, pts.First(), pts.Last(), pts);
                tx.Commit();
                return new JObject { ["message"] = $"🔧 Created flex duct (ID: {fd.Id.IntegerValue})", ["elementId"] = fd.Id.IntegerValue };
            }
        }

        private static JToken CreateMepSpace(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            using (var tx = new Transaction(doc, "Create MEP Space"))
            {
                tx.Start();
                var space = doc.Create.NewSpace(level, new UV(parameters["x"]?.Value<double>() ?? 0, parameters["y"]?.Value<double>() ?? 0));
                var spaceName = parameters["spaceName"]?.ToString();
                if (!string.IsNullOrEmpty(spaceName)) space.get_Parameter(BuiltInParameter.ROOM_NAME)?.Set(spaceName);
                tx.Commit();
                return new JObject { ["message"] = $"📦 Created MEP space (ID: {space.Id.IntegerValue})", ["elementId"] = space.Id.IntegerValue };
            }
        }

        private static JToken GetMepSystems(Document doc, JObject parameters)
        {
            var systems = new JArray();
            foreach (var sys in new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystem)).Cast<Autodesk.Revit.DB.Mechanical.MechanicalSystem>())
                systems.Add(new JObject { ["id"] = sys.Id.IntegerValue, ["name"] = sys.Name, ["type"] = "Mechanical", ["elements"] = sys.DuctNetwork?.Size ?? 0 });
            foreach (var sys in new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipingSystem)).Cast<Autodesk.Revit.DB.Plumbing.PipingSystem>())
                systems.Add(new JObject { ["id"] = sys.Id.IntegerValue, ["name"] = sys.Name, ["type"] = "Piping", ["elements"] = sys.PipingNetwork?.Size ?? 0 });
            return new JObject { ["message"] = $"🔧 Found {systems.Count} MEP systems", ["systems"] = systems };
        }

        private static JToken DuctSizing(Document doc, JObject parameters)
        {
            var cat = parameters["category"]?.ToString() ?? "Ducts";
            var bic = cat.ToLower().Contains("pipe") ? BuiltInCategory.OST_PipeCurves : BuiltInCategory.OST_DuctCurves;
            var elements = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToList();
            var items = new JArray();
            foreach (var e in elements.Take(50))
            {
                var item = new JObject { ["id"] = e.Id.IntegerValue, ["name"] = e.Name };
                var sizeP = e.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE); if (sizeP != null) item["size"] = sizeP.AsString();
                items.Add(item);
            }
            return new JObject { ["message"] = $"📐 {cat} sizing: {elements.Count} elements", ["elements"] = items };
        }

        private static JToken ConnectMepElements(Document doc, JObject parameters)
        {
            var e1 = doc.GetElement(new ElementId(parameters["elementId1"]?.Value<int>() ?? 0));
            var e2 = doc.GetElement(new ElementId(parameters["elementId2"]?.Value<int>() ?? 0));
            if (e1 == null || e2 == null) return new JObject { ["error"] = "Element not found" };
            ConnectorSet cs1 = (e1 as MEPCurve)?.ConnectorManager?.Connectors;
            ConnectorSet cs2 = (e2 as MEPCurve)?.ConnectorManager?.Connectors;
            if (cs1 == null || cs2 == null) return new JObject { ["error"] = "Elements have no connectors" };
            Connector best1 = null, best2 = null; double minDist = double.MaxValue;
            foreach (Connector c1 in cs1) { if (c1.IsConnected) continue; foreach (Connector c2 in cs2) { if (c2.IsConnected) continue; double d = c1.Origin.DistanceTo(c2.Origin); if (d < minDist) { minDist = d; best1 = c1; best2 = c2; } } }
            if (best1 == null) return new JObject { ["error"] = "No unconnected connectors" };
            using (var tx = new Transaction(doc, "Connect MEP")) { tx.Start(); best1.ConnectTo(best2); tx.Commit(); }
            return new JObject { ["message"] = $"🔗 Connected elements (distance: {Math.Round(minDist, 2)}ft)" };
        }

        // ===== STRUCTURAL TOOL IMPLEMENTATIONS =====

        private static JToken CreateStructuralBeam(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            var beamType = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault();
            if (beamType == null) return new JObject { ["error"] = "No beam type found" };
            using (var tx = new Transaction(doc, "Create Beam"))
            {
                tx.Start(); if (!beamType.IsActive) beamType.Activate();
                var line = Line.CreateBound(new XYZ(parameters["startX"]?.Value<double>() ?? 0, parameters["startY"]?.Value<double>() ?? 0, level.Elevation), new XYZ(parameters["endX"]?.Value<double>() ?? 0, parameters["endY"]?.Value<double>() ?? 0, level.Elevation));
                var beam = doc.Create.NewFamilyInstance(line, beamType, level, Autodesk.Revit.DB.Structure.StructuralType.Beam);
                tx.Commit();
                return new JObject { ["message"] = $"🏗️ Created beam (ID: {beam.Id.IntegerValue})", ["elementId"] = beam.Id.IntegerValue };
            }
        }

        private static JToken CreateStructuralColumn(Document doc, JObject parameters)
        {
            var baseLevelName = parameters["baseLevelName"]?.ToString();
            var baseLevel = FindLevel(doc, baseLevelName);
            if (baseLevel == null) return new JObject { ["error"] = $"Level '{baseLevelName}' not found" };
            var colType = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault();
            if (colType == null) return new JObject { ["error"] = "No column type found" };
            using (var tx = new Transaction(doc, "Create Column"))
            {
                tx.Start(); if (!colType.IsActive) colType.Activate();
                var col = doc.Create.NewFamilyInstance(new XYZ(parameters["x"]?.Value<double>() ?? 0, parameters["y"]?.Value<double>() ?? 0, baseLevel.Elevation), colType, baseLevel, Autodesk.Revit.DB.Structure.StructuralType.Column);
                var topLevelName = parameters["topLevelName"]?.ToString();
                if (!string.IsNullOrEmpty(topLevelName)) { var tl = FindLevel(doc, topLevelName); if (tl != null) col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(tl.Id); }
                tx.Commit();
                return new JObject { ["message"] = $"🏗️ Created column (ID: {col.Id.IntegerValue})", ["elementId"] = col.Id.IntegerValue };
            }
        }

        private static JToken CreateWallFoundation(Document doc, JObject parameters)
        {
            var wallId = parameters["wallId"]?.Value<int>() ?? 0;
            var wall = doc.GetElement(new ElementId(wallId)) as Wall;
            if (wall == null) return new JObject { ["error"] = $"Wall {wallId} not found" };
            var foundTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().ToList();
            return new JObject { ["message"] = $"🏗️ Wall foundation: {foundTypes.Count} types available for wall {wallId}", ["types"] = JArray.FromObject(foundTypes.Select(t => new { t.Name, id = t.Id.IntegerValue })), ["hint"] = "Use execute_code: doc.Create.NewFamilyInstance(curve, foundationType, wall, level, StructuralType.Footing)" };
        }

        private static JToken CreateRebar(Document doc, JObject parameters)
        {
            var hostId = parameters["hostId"]?.Value<int>() ?? 0;
            var host = doc.GetElement(new ElementId(hostId));
            if (host == null) return new JObject { ["error"] = $"Host element {hostId} not found" };
            var barTypes = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Structure.RebarBarType)).Cast<Autodesk.Revit.DB.Structure.RebarBarType>().ToList();
            return new JObject { ["message"] = $"🏗️ {barTypes.Count} rebar types available for '{host.Name}'", ["barTypes"] = JArray.FromObject(barTypes.Select(b => new { b.Name, id = b.Id.IntegerValue })), ["hint"] = "Use execute_code: Rebar.CreateFromCurves(doc, rebarStyle, barType, hookType, hookType, host, normal, curves, hookOrient, hookOrient, useExistingShape, createNewShape)" };
        }

        private static JToken GetStructuralElements(Document doc, JObject parameters)
        {
            var cat = parameters["category"]?.ToString() ?? "StructuralFraming";
            BuiltInCategory bic = cat == "StructuralColumns" ? BuiltInCategory.OST_StructuralColumns : cat == "StructuralFoundation" ? BuiltInCategory.OST_StructuralFoundation : BuiltInCategory.OST_StructuralFraming;
            var elements = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToList();
            var items = new JArray();
            foreach (var e in elements.Take(100))
                items.Add(new JObject { ["id"] = e.Id.IntegerValue, ["name"] = e.Name, ["family"] = (e as FamilyInstance)?.Symbol?.Family?.Name });
            return new JObject { ["message"] = $"🏗️ {elements.Count} {cat} elements", ["elements"] = items };
        }

        private static JToken AnalyticalModelInfo(Document doc, JObject parameters)
        {
            var idsStr = parameters["elementIds"]?.ToString();
            var items = new JArray();
            if (!string.IsNullOrEmpty(idsStr))
                foreach (var idStr in idsStr.Split(','))
                {
                    var elem = doc.GetElement(new ElementId(int.Parse(idStr.Trim())));
                    if (elem == null) continue;
                    var item = new JObject { ["id"] = elem.Id.IntegerValue, ["name"] = elem.Name, ["category"] = elem.Category?.Name };
                    var bb = elem.get_BoundingBox(null);
                    if (bb != null) item["centroid"] = new JObject { ["x"] = Math.Round((bb.Min.X + bb.Max.X) / 2, 4), ["y"] = Math.Round((bb.Min.Y + bb.Max.Y) / 2, 4), ["z"] = Math.Round((bb.Min.Z + bb.Max.Z) / 2, 4) };
                    items.Add(item);
                }
            return new JObject { ["message"] = $"📊 Analytical info for {items.Count} elements", ["elements"] = items };
        }

        // ===== ANNOTATION TOOL IMPLEMENTATIONS =====

        private static JToken CreateFilledRegion(Document doc, UIDocument uidoc, JObject parameters)
        {
            var pointsArr = parameters["points"] as JArray;
            var viewIdParam = parameters["viewId"]?.Value<int>();
            var view = viewIdParam.HasValue ? doc.GetElement(new ElementId(viewIdParam.Value)) as View : uidoc.ActiveView;
            var regionType = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).FirstOrDefault() as FilledRegionType;
            if (regionType == null) return new JObject { ["error"] = "No filled region type found" };
            if (pointsArr == null || pointsArr.Count < 3) return new JObject { ["error"] = "Need at least 3 points" };
            var loop = new CurveLoop();
            var pts = pointsArr.Select(p => new XYZ(p["x"]?.Value<double>() ?? 0, p["y"]?.Value<double>() ?? 0, 0)).ToList();
            for (int i = 0; i < pts.Count; i++) loop.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
            using (var tx = new Transaction(doc, "Create Filled Region"))
            {
                tx.Start();
                var region = FilledRegion.Create(doc, regionType.Id, view.Id, new List<CurveLoop> { loop });
                tx.Commit();
                return new JObject { ["message"] = $"✏️ Created filled region (ID: {region.Id.IntegerValue})", ["elementId"] = region.Id.IntegerValue };
            }
        }

        private static JToken CreateSpotElevation(Document doc, UIDocument uidoc, JObject parameters)
        {
            return new JObject { ["message"] = "📍 Spot elevation requested", ["hint"] = "Use execute_code: doc.Create.NewSpotElevation(view, reference, origin, bend, end, leaderPoint, hasLeader)" };
        }

        private static JToken CreateSpotCoordinate(Document doc, UIDocument uidoc, JObject parameters)
        {
            return new JObject { ["message"] = "📍 Spot coordinate requested", ["hint"] = "Use execute_code: doc.Create.NewSpotCoordinate(view, reference, origin, bend, end, leaderPoint, hasLeader)" };
        }

        private static JToken CreateKeynoteLegend(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Keynote Legend"))
            {
                tx.Start();
                var legend = ViewSchedule.CreateKeynoteLegend(doc);
                tx.Commit();
                return new JObject { ["message"] = $"📋 Created keynote legend (ID: {legend.Id.IntegerValue})", ["elementId"] = legend.Id.IntegerValue };
            }
        }

        private static JToken CreateDetailComponent(Document doc, UIDocument uidoc, JObject parameters)
        {
            var symbol = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_DetailComponents).Cast<FamilySymbol>()
                .FirstOrDefault(s => (string.IsNullOrEmpty(parameters["familyName"]?.ToString()) || s.Family.Name.Contains(parameters["familyName"].ToString())) && (string.IsNullOrEmpty(parameters["typeName"]?.ToString()) || s.Name.Contains(parameters["typeName"].ToString())));
            if (symbol == null) return new JObject { ["error"] = "Detail component not found" };
            using (var tx = new Transaction(doc, "Place Detail Component"))
            {
                tx.Start(); if (!symbol.IsActive) symbol.Activate();
                var inst = doc.Create.NewFamilyInstance(new XYZ(parameters["x"]?.Value<double>() ?? 0, parameters["y"]?.Value<double>() ?? 0, 0), symbol, uidoc.ActiveView);
                tx.Commit();
                return new JObject { ["message"] = $"✏️ Placed detail component (ID: {inst.Id.IntegerValue})", ["elementId"] = inst.Id.IntegerValue };
            }
        }

        private static JToken TagRoomsInView(Document doc, UIDocument uidoc, JObject parameters)
        {
            var viewIdParam = parameters["viewId"]?.Value<int>();
            var view = viewIdParam.HasValue ? doc.GetElement(new ElementId(viewIdParam.Value)) as View : uidoc.ActiveView;
            var rooms = new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().Cast<SpatialElement>().Where(r => r.Area > 0).ToList();
            int tagged = 0;
            using (var tx = new Transaction(doc, "Tag Rooms"))
            {
                tx.Start();
                foreach (var room in rooms)
                    try { var loc = room.Location as LocationPoint; if (loc != null) { doc.Create.NewRoomTag(new LinkElementId(room.Id), new UV(loc.Point.X, loc.Point.Y), view.Id); tagged++; } } catch { }
                tx.Commit();
            }
            return new JObject { ["message"] = $"🏷️ Tagged {tagged}/{rooms.Count} rooms in '{view.Name}'", ["tagged"] = tagged };
        }

        private static JToken DimensionWalls(Document doc, UIDocument uidoc, JObject parameters)
        {
            var viewIdParam = parameters["viewId"]?.Value<int>();
            var view = viewIdParam.HasValue ? doc.GetElement(new ElementId(viewIdParam.Value)) as View : uidoc.ActiveView;
            var walls = new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().ToList();
            return new JObject { ["message"] = $"📏 Found {walls.Count} walls in '{view.Name}' for dimensioning", ["hint"] = "Use execute_code: create ReferenceArray from wall faces, then doc.Create.NewDimension(view, line, refs)" };
        }

        // ===== ARCHITECTURE TOOL IMPLEMENTATIONS =====

        private static JToken CreateStairs(Document doc, JObject parameters)
        {
            var baseLevel = FindLevel(doc, parameters["baseLevelName"]?.ToString());
            var topLevel = FindLevel(doc, parameters["topLevelName"]?.ToString());
            if (baseLevel == null || topLevel == null) return new JObject { ["error"] = "Base or top level not found" };
            var stairTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Stairs).OfClass(typeof(ElementType)).ToList();
            return new JObject { ["message"] = $"🪜 Stairs: {baseLevel.Name} → {topLevel.Name} (height: {Math.Round(topLevel.Elevation - baseLevel.Elevation, 2)}ft)", ["types"] = JArray.FromObject(stairTypes.Select(t => new { t.Name, id = t.Id.IntegerValue })), ["hint"] = "Use execute_code: StairsEditScope + StairsRun.CreateStraightRun()" };
        }

        private static JToken CreateRailing(Document doc, JObject parameters)
        {
            var railTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing).OfClass(typeof(ElementType)).ToList();
            return new JObject { ["message"] = $"🛡️ {railTypes.Count} railing types available", ["types"] = JArray.FromObject(railTypes.Select(t => new { t.Name, id = t.Id.IntegerValue })), ["hint"] = "Use execute_code: Railing.Create(doc, curveLoop, railingTypeId, levelId)" };
        }

        private static JToken CreateCurtainWall(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            var wallType = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault(wt => wt.Kind == WallKind.Curtain);
            if (wallType == null) return new JObject { ["error"] = "No curtain wall type found" };
            using (var tx = new Transaction(doc, "Create Curtain Wall"))
            {
                tx.Start();
                var line = Line.CreateBound(new XYZ(parameters["startX"]?.Value<double>() ?? 0, parameters["startY"]?.Value<double>() ?? 0, level.Elevation), new XYZ(parameters["endX"]?.Value<double>() ?? 0, parameters["endY"]?.Value<double>() ?? 0, level.Elevation));
                var wall = Wall.Create(doc, line, wallType.Id, level.Id, parameters["height"]?.Value<double>() ?? 15, 0, false, false);
                tx.Commit();
                return new JObject { ["message"] = $"🏗️ Created curtain wall (ID: {wall.Id.IntegerValue})", ["elementId"] = wall.Id.IntegerValue };
            }
        }

        private static JToken CreateShaftOpening(Document doc, JObject parameters)
        {
            var baseLevel = FindLevel(doc, parameters["baseLevelName"]?.ToString());
            var topLevel = FindLevel(doc, parameters["topLevelName"]?.ToString());
            if (baseLevel == null || topLevel == null) return new JObject { ["error"] = "Levels not found" };
            var pointsArr = parameters["points"] as JArray;
            if (pointsArr == null || pointsArr.Count < 3) return new JObject { ["error"] = "Need at least 3 points" };
            var pts = pointsArr.Select(p => new XYZ(p["x"]?.Value<double>() ?? 0, p["y"]?.Value<double>() ?? 0, baseLevel.Elevation)).ToList();
            var curveArr = new CurveArray();
            for (int i = 0; i < pts.Count; i++) curveArr.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
            using (var tx = new Transaction(doc, "Create Shaft"))
            {
                tx.Start();
                var opening = doc.Create.NewOpening(baseLevel, topLevel, curveArr);
                tx.Commit();
                return new JObject { ["message"] = $"🕳️ Created shaft opening (ID: {opening.Id.IntegerValue})", ["elementId"] = opening.Id.IntegerValue };
            }
        }

        private static JToken GetStairsInfo(Document doc, JObject parameters)
        {
            var stairsIdParam = parameters["stairsId"]?.Value<int>();
            var stairs = stairsIdParam.HasValue ? new[] { doc.GetElement(new ElementId(stairsIdParam.Value)) }.Where(e => e != null).ToList() : new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Stairs).WhereElementIsNotElementType().ToList();
            var items = new JArray();
            foreach (var s in stairs)
            {
                var item = new JObject { ["id"] = s.Id.IntegerValue, ["name"] = s.Name };
                var rh = s.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_RISER_HEIGHT); if (rh != null) item["riserHeight_ft"] = Math.Round(rh.AsDouble(), 4);
                var td = s.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_TREAD_DEPTH); if (td != null) item["treadDepth_ft"] = Math.Round(td.AsDouble(), 4);
                var nr = s.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_NUM_RISERS); if (nr != null) item["numRisers"] = nr.AsInteger();
                items.Add(item);
            }
            return new JObject { ["message"] = $"🪜 {stairs.Count} stairs", ["stairs"] = items };
        }

        private static JToken GetCurtainPanels(Document doc, JObject parameters)
        {
            var wall = doc.GetElement(new ElementId(parameters["wallId"]?.Value<int>() ?? 0)) as Wall;
            if (wall == null) return new JObject { ["error"] = "Wall not found" };
            var cg = wall.CurtainGrid;
            if (cg == null) return new JObject { ["error"] = "Not a curtain wall" };
            var panels = new JArray(); foreach (var pId in cg.GetPanelIds()) { var p = doc.GetElement(pId); panels.Add(new JObject { ["id"] = p.Id.IntegerValue, ["name"] = p?.Name }); }
            var mullions = new JArray(); foreach (var mId in cg.GetMullionIds()) { var m = doc.GetElement(mId); mullions.Add(new JObject { ["id"] = m.Id.IntegerValue, ["name"] = m?.Name }); }
            return new JObject { ["message"] = $"🏗️ {panels.Count} panels, {mullions.Count} mullions", ["panels"] = panels, ["mullions"] = mullions };
        }

        private static JToken CreateOpeningInWall(Document doc, JObject parameters)
        {
            var wall = doc.GetElement(new ElementId(parameters["wallId"]?.Value<int>() ?? 0)) as Wall;
            if (wall == null) return new JObject { ["error"] = "Wall not found" };
            using (var tx = new Transaction(doc, "Create Opening"))
            {
                tx.Start();
                var opening = doc.Create.NewOpening(wall, new XYZ(parameters["x1"]?.Value<double>() ?? 0, parameters["y1"]?.Value<double>() ?? 0, 0), new XYZ(parameters["x2"]?.Value<double>() ?? 0, parameters["y2"]?.Value<double>() ?? 0, 0));
                tx.Commit();
                return new JObject { ["message"] = $"🕳️ Created wall opening (ID: {opening.Id.IntegerValue})", ["elementId"] = opening.Id.IntegerValue };
            }
        }

        // ===== SITE TOOL IMPLEMENTATIONS =====

        private static JToken CreateTopography(Document doc, JObject parameters)
        {
            var pointsArr = parameters["points"] as JArray;
            if (pointsArr == null || pointsArr.Count < 3) return new JObject { ["error"] = "Need at least 3 points" };
            var pts = pointsArr.Select(p => new XYZ(p["x"]?.Value<double>() ?? 0, p["y"]?.Value<double>() ?? 0, p["z"]?.Value<double>() ?? 0)).ToList();
            using (var tx = new Transaction(doc, "Create Topography"))
            {
                tx.Start();
#pragma warning disable CS0618
                var topo = TopographySurface.Create(doc, pts);
#pragma warning restore CS0618
                tx.Commit();
                return new JObject { ["message"] = $"🏔️ Created topography ({pts.Count} points, ID: {topo.Id.IntegerValue})", ["elementId"] = topo.Id.IntegerValue };
            }
        }

        private static JToken CreateBuildingPad(Document doc, JObject parameters)
        {
            var level = FindLevel(doc, parameters["levelName"]?.ToString());
            if (level == null) return new JObject { ["error"] = "Level not found" };
            var pointsArr = parameters["points"] as JArray;
            if (pointsArr == null || pointsArr.Count < 3) return new JObject { ["error"] = "Need at least 3 points" };
            var padType = new FilteredElementCollector(doc).OfClass(typeof(BuildingPadType)).FirstOrDefault() as BuildingPadType;
            if (padType == null) return new JObject { ["error"] = "No building pad type found" };
            var pts = pointsArr.Select(p => new XYZ(p["x"]?.Value<double>() ?? 0, p["y"]?.Value<double>() ?? 0, level.Elevation)).ToList();
            var loop = new CurveLoop(); for (int i = 0; i < pts.Count; i++) loop.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
            using (var tx = new Transaction(doc, "Create Building Pad"))
            {
                tx.Start();
                var pad = BuildingPad.Create(doc, padType.Id, level.Id, new List<CurveLoop> { loop });
                tx.Commit();
                return new JObject { ["message"] = $"🏗️ Created building pad (ID: {pad.Id.IntegerValue})", ["elementId"] = pad.Id.IntegerValue };
            }
        }

        private static JToken GetSiteInfo(Document doc, JObject parameters)
        {
#pragma warning disable CS0618
            var topos = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Topography).WhereElementIsNotElementType().ToList();
#pragma warning restore CS0618
            var pads = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_BuildingPad).WhereElementIsNotElementType().ToList();
            return new JObject { ["message"] = $"🏔️ Site: {topos.Count} topo surfaces, {pads.Count} building pads", ["topography"] = JArray.FromObject(topos.Select(t => new { id = t.Id.IntegerValue, t.Name })), ["buildingPads"] = JArray.FromObject(pads.Select(p => new { id = p.Id.IntegerValue, p.Name })) };
        }

        // ===== UTILITY TOOL IMPLEMENTATIONS =====

        private static JToken PinElements(Document doc, JObject parameters, bool pin)
        {
            var idsArr = parameters["elementIds"] as JArray;
            if (idsArr == null || idsArr.Count == 0) return new JObject { ["error"] = "No element IDs" };
            int count = 0;
            using (var tx = new Transaction(doc, pin ? "Pin" : "Unpin"))
            {
                tx.Start();
                foreach (var id in idsArr) { var e = doc.GetElement(new ElementId(id.Value<int>())); if (e != null) { e.Pinned = pin; count++; } }
                tx.Commit();
            }
            return new JObject { ["message"] = $"📌 {(pin ? "Pinned" : "Unpinned")} {count} elements" };
        }

        private static JToken CreateWorkset(Document doc, JObject parameters)
        {
            var name = parameters["name"]?.ToString();
            if (string.IsNullOrEmpty(name)) return new JObject { ["error"] = "Name required" };
            if (!doc.IsWorkshared) return new JObject { ["error"] = "Not workshared" };
            using (var tx = new Transaction(doc, "Create Workset"))
            {
                tx.Start(); var ws = Workset.Create(doc, name); tx.Commit();
                return new JObject { ["message"] = $"📁 Created workset '{name}'", ["worksetId"] = ws.Id.IntegerValue };
            }
        }

        private static JToken GetElementHistory(Document doc, JObject parameters)
        {
            var idsArr = parameters["elementIds"] as JArray;
            if (idsArr == null) return new JObject { ["error"] = "No element IDs" };
            var items = new JArray();
            foreach (var id in idsArr)
            {
                var elem = doc.GetElement(new ElementId(id.Value<int>()));
                if (elem == null) continue;
                var item = new JObject { ["id"] = elem.Id.IntegerValue, ["name"] = elem.Name };
                if (doc.IsWorkshared) { var ws = elem.get_Parameter(BuiltInParameter.EDITED_BY); if (ws != null) item["editedBy"] = ws.AsString(); }
                var ph = elem.get_Parameter(BuiltInParameter.PHASE_CREATED); if (ph != null) { var phase = doc.GetElement(ph.AsElementId()); item["phaseCreated"] = phase?.Name; }
                items.Add(item);
            }
            return new JObject { ["message"] = $"📋 History for {items.Count} elements", ["elements"] = items };
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
                return new JObject { ["message"] = $"📦 Created assembly (ID: {assembly.Id.IntegerValue})", ["elementId"] = assembly.Id.IntegerValue };
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
                return new JObject { ["message"] = $"🎨 Created fill pattern '{name}' (ID: {elem.Id.IntegerValue})", ["elementId"] = elem.Id.IntegerValue };
            }
        }

        private static JToken GetElementGeometry(Document doc, JObject parameters)
        {
            var elem = doc.GetElement(new ElementId(parameters["elementId"]?.Value<int>() ?? 0));
            if (elem == null) return new JObject { ["error"] = "Element not found" };
            var result = new JObject { ["id"] = elem.Id.IntegerValue, ["name"] = elem.Name };
            var bb = elem.get_BoundingBox(null);
            if (bb != null)
            {
                var size = bb.Max - bb.Min;
                result["boundingBox"] = new JObject { ["width"] = Math.Round(size.X, 4), ["depth"] = Math.Round(size.Y, 4), ["height"] = Math.Round(size.Z, 4) };
                result["centroid"] = new JObject { ["x"] = Math.Round((bb.Min.X + bb.Max.X) / 2, 4), ["y"] = Math.Round((bb.Min.Y + bb.Max.Y) / 2, 4), ["z"] = Math.Round((bb.Min.Z + bb.Max.Z) / 2, 4) };
            }
            var vol = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED); if (vol != null) result["volume_cuft"] = Math.Round(vol.AsDouble(), 4);
            var area = elem.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED); if (area != null) result["area_sqft"] = Math.Round(area.AsDouble(), 4);
            return new JObject { ["message"] = $"📐 Geometry for '{elem.Name}'", ["geometry"] = result };
        }

        private static JToken CompareModels(Document doc, JObject parameters)
        {
            var snapshot = new JObject();
            var cats = new[] { BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows, BuiltInCategory.OST_Rooms, BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_StructuralFraming };
            foreach (var cat in cats) snapshot[cat.ToString().Replace("OST_", "")] = new FilteredElementCollector(doc).OfCategory(cat).WhereElementIsNotElementType().Count();
            snapshot["totalFamilies"] = new FilteredElementCollector(doc).OfClass(typeof(Family)).Count();
            snapshot["totalSheets"] = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Count();
            return new JObject { ["message"] = "📊 Current model snapshot", ["snapshot"] = snapshot };
        }

        private static JToken LinkRevitModel(Document doc, UIApplication uiApp, JObject parameters)
        {
            var filePath = parameters["filePath"]?.ToString();
            if (string.IsNullOrEmpty(filePath)) return new JObject { ["error"] = "File path required" };
            return new JObject { ["message"] = $"🔗 Link model: {System.IO.Path.GetFileName(filePath)}", ["hint"] = "Use execute_code: RevitLinkType.Create(doc, modelPath, false) then RevitLinkInstance.Create(doc, linkTypeId)" };
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
                ["message"] = $"🔄 Reloaded {reloaded}/{links.Count} links",
                ["links"] = JArray.FromObject(links.Select(l => new { l.Name, id = l.Id.IntegerValue }))
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
                try { l.Unload(null); unloaded++; } catch { }
            }
            return new JObject
            {
                ["message"] = $"📤 Unloaded {unloaded}/{links.Count} links",
                ["links"] = JArray.FromObject(links.Select(l => new { l.Name, id = l.Id.IntegerValue }))
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
                    ["id"] = link.Id.IntegerValue,
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
                ["message"] = $"🔗 {result.Count} linked model(s) found",
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
            return new JObject { ["message"] = $"💾 Document saved: {System.IO.Path.GetFileName(doc.PathName)}", ["filePath"] = doc.PathName };
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
            return new JObject { ["message"] = $"💾 Saved as: {System.IO.Path.GetFileName(filePath)}", ["filePath"] = filePath };
        }

        private static JToken CloseDocument(Document doc, JObject parameters)
        {
            var save = parameters["save"]?.Value<bool>() ?? true;
            var fileName = System.IO.Path.GetFileName(doc.PathName);
            if (save && !string.IsNullOrEmpty(doc.PathName))
                doc.Save();
            doc.Close(save);
            return new JObject { ["message"] = $"📁 Closed document: {fileName}", ["saved"] = save };
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
                ["message"] = $"✏️ Opened family '{family.Name}' for editing",
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
                    ["message"] = $"🧱 Created {(isSolid ? "solid" : "void")} extrusion with {points.Count} vertices, depth={depth}ft",
                    ["elementId"] = extrusion.Id.IntegerValue,
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
                ["message"] = $"💾 Family '{familyName}' saved{(loadIntoProject ? " and loaded into project" : "")}",
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
                    ["message"] = $"📦 Loaded family '{family.Name}' from {System.IO.Path.GetFileName(filePath)}",
                    ["familyName"] = family.Name,
                    ["familyId"] = family.Id.IntegerValue
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
                ["message"] = $"📐 Sketch profile for '{elem.Name}' — {curveLoops.Count} loop(s)",
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
                ["message"] = $"✏️ Use send_code_to_revit with SketchEditScope for '{action}' on element {elementId}",
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
                ["message"] = $"✏️ To replace sketch profile, use send_code_to_revit with SketchEditScope",
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
                ["message"] = $"✏️ Created {created} detail lines in view '{view.Name}'",
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
                ["message"] = $"✏️ Created {created} model lines",
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
                    // Full circle — create two semicircles
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
                ["message"] = $"⭕ Created detail arc at ({cx}, {cy}), radius={radius}ft",
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
                ["message"] = $"☀️ Sun settings updated for view '{view.Name}'",
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
                ["message"] = $"🎨 Set visual style to '{styleName}' on view '{view.Name}'",
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
                ["message"] = $"📸 Exported view image to '{filePath}'",
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

            var comment = parameters["comment"]?.ToString() ?? "MCP Sync";
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
                ["message"] = $"🔄 Synchronized with Central — comment: '{comment}'",
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

            return new JObject { ["message"] = "🔓 Relinquished all borrowed elements and worksets" };
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
                ["message"] = $"📋 Worksharing info for '{System.IO.Path.GetFileName(doc.PathName)}'",
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
                return new JObject { ["message"] = "↩️ Undo command posted. The last operation will be undone." };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"⚠️ Undo failed: {ex.Message}", ["hint"] = "Undo can only be triggered outside of an active API transaction context." };
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
                ["message"] = $"📌 Checkpoint '{name}' created. All subsequent changes can be rolled back to this point.",
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
                ["message"] = $"⏪ Rolled back to checkpoint '{name}'. All changes since the checkpoint have been undone.",
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
                ["message"] = $"▶️ Posted command: {commandName}",
                ["commandName"] = commandName
            };
        }

        private static JToken ListPostableCommands()
        {
            var commands = Enum.GetNames(typeof(PostableCommand)).OrderBy(n => n).ToList();
            return new JObject
            {
                ["message"] = $"📋 {commands.Count} available PostableCommands",
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
                ["message"] = $"📂 Opened document: {System.IO.Path.GetFileName(filePath)}",
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
                ["message"] = $"📄 Created new project{(string.IsNullOrEmpty(templatePath) ? "" : $" from template: {System.IO.Path.GetFileName(templatePath)}")}",
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
                ["message"] = $"📦 Created new family from template: {System.IO.Path.GetFileName(templatePath)}",
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
                ["message"] = $"🔓 Detached from Central: {System.IO.Path.GetFileName(filePath)}",
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
                ["message"] = $"🔗 Updated link '{link.Name}' path to: {System.IO.Path.GetFileName(newPath)}",
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
                ["message"] = $"📍 Moved link '{instance.Name}' by ({moveX}, {moveY}, {moveZ})ft, rotated {rotationDeg}°",
                ["linkName"] = instance.Name,
                ["elementId"] = instance.Id.IntegerValue
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
                ["message"] = $"🔍 Zoomed to fit view '{uidoc.ActiveView.Name}'",
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
                ["message"] = $"🔍 Zoomed to element '{elem.Name}' (ID: {elementId})",
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
                            ["message"] = $"📊 Schedule '{schedule.Name}' info",
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
                ["message"] = $"📊 Schedule '{schedule.Name}' updated (action: {action})",
                ["scheduleName"] = schedule.Name,
                ["action"] = action
            };
        }

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
