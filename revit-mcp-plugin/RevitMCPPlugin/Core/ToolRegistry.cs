using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Core
{
    // ══════════════════════════════════════════════════════════
    //  AUTHORITY BANK — Single source of truth for ALL tools
    //  Add/remove tools HERE; all AI clients auto-update.
    // ══════════════════════════════════════════════════════════

    public enum ToolCategory
    {
        Reading,
        Creating,
        Editing,
        Export,
        QAQC,
        Views,
        Data,
        Integrations,
        ProjectFiles,
        CodeExecution,
        MEP,
        Structural,
        Annotation,
        Architecture,
        Site
    }

    public class ToolParam
    {
        public string Name { get; set; }
        public string Type { get; set; } = "string";  // string, number, integer, boolean
        public string Description { get; set; }
        public bool Required { get; set; }
        public bool IsArray { get; set; }
        public JObject ArrayItemSchema { get; set; } // For array params

        public static ToolParam Opt(string name, string type, string desc) =>
            new ToolParam { Name = name, Type = type, Description = desc };
        public static ToolParam Req(string name, string type, string desc) =>
            new ToolParam { Name = name, Type = type, Description = desc, Required = true };
        public static ToolParam Arr(string name, string desc, JObject itemSchema = null) =>
            new ToolParam { Name = name, Type = "array", Description = desc, Required = true, IsArray = true, ArrayItemSchema = itemSchema };
    }

    public class ToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ToolCategory Category { get; set; }
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public ToolParam[] Parameters { get; set; } = Array.Empty<ToolParam>();
        public bool AlwaysAvailable { get; set; } // For execute_code, always sent to AI
    }

    public static class ToolRegistry
    {
        private static readonly List<ToolDefinition> _tools = new List<ToolDefinition>();
        private static Dictionary<string, string> _keywordCache;
        private static readonly object _lock = new object();

        static ToolRegistry()
        {
            RegisterAll();
        }

        // ── Public API ──

        public static IReadOnlyList<ToolDefinition> All => _tools.AsReadOnly();

        public static ToolDefinition GetByName(string name) =>
            _tools.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        public static List<ToolDefinition> GetByCategory(ToolCategory cat) =>
            _tools.Where(t => t.Category == cat).ToList();

        /// <summary>Auto-generates keyword→tool mapping for Antigravity NLP.</summary>
        public static Dictionary<string, string> GetKeywordMap()
        {
            lock (_lock)
            {
                if (_keywordCache != null) return _keywordCache;
                _keywordCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var tool in _tools)
                {
                    foreach (var kw in tool.Keywords)
                    {
                        if (!_keywordCache.ContainsKey(kw))
                            _keywordCache[kw] = tool.Name;
                    }
                }
                return _keywordCache;
            }
        }

        /// <summary>Gets the tool catalog string for AI system prompts.</summary>
        public static string GetToolCatalog()
        {
            var groups = new Dictionary<ToolCategory, (string emoji, string label)>
            {
                { ToolCategory.Reading, ("📖", "Reading") },
                { ToolCategory.Creating, ("🔨", "Creating") },
                { ToolCategory.Editing, ("✏️", "Editing") },
                { ToolCategory.Export, ("📤", "Export") },
                { ToolCategory.QAQC, ("🔍", "QA/QC") },
                { ToolCategory.Views, ("🖼️", "Views") },
                { ToolCategory.Data, ("💾", "Data") },
                { ToolCategory.Integrations, ("🔗", "Integrations") },
                { ToolCategory.ProjectFiles, ("📁", "Project Files") },
                { ToolCategory.CodeExecution, ("🧠", "Code Execution") },
            };

            var lines = new List<string> { "\n\nAVAILABLE TOOLS:" };
            foreach (var kvp in groups)
            {
                var tools = GetByCategory(kvp.Key);
                if (tools.Count > 0)
                {
                    var names = string.Join(", ", tools.Select(t => t.Name));
                    lines.Add($"{kvp.Value.emoji} {kvp.Value.label}: {names}");
                }
            }
            return string.Join("\n", lines);
        }

        /// <summary>Generates Gemini-format function declarations.</summary>
        public static JArray GetGeminiDeclarations(HashSet<ToolCategory> categories = null)
        {
            var d = new JArray();
            foreach (var tool in _tools)
            {
                if (!tool.AlwaysAvailable && categories != null && !categories.Contains(tool.Category))
                    continue;

                var props = new JObject();
                var required = new JArray();

                foreach (var p in tool.Parameters)
                {
                    if (p.IsArray)
                    {
                        props[p.Name] = new JObject
                        {
                            ["type"] = "array",
                            ["description"] = p.Description,
                            ["items"] = p.ArrayItemSchema ?? new JObject { ["type"] = "string" }
                        };
                    }
                    else
                    {
                        props[p.Name] = new JObject
                        {
                            ["type"] = p.Type,
                            ["description"] = p.Description
                        };
                    }
                    if (p.Required) required.Add(p.Name);
                }

                var fn = new JObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = props,
                        ["required"] = required
                    }
                };

                d.Add(new JObject { ["function_declarations"] = new JArray { fn } });
            }
            return d;
        }

        /// <summary>Category mapping for Gemini group-based tool selection.</summary>
        public static HashSet<ToolCategory> GetRelevantCategories(string message)
        {
            var msg = message.ToLowerInvariant();
            var cats = new HashSet<ToolCategory>();
            cats.Add(ToolCategory.Reading); // Always include

            if (ContainsAny(msg, "create", "add", "new", "make", "place", "build", "room finish",
                "legend", "shared parameter", "convert"))
                cats.Add(ToolCategory.Creating);
            if (ContainsAny(msg, "modify", "move", "delete", "change", "rename", "rotate", "copy",
                "align", "set param", "select", "color", "extend", "shrink", "renumber", "batch",
                "case", "upper", "lower", "title", "inverse", "invert", "update", "parameter"))
                cats.Add(ToolCategory.Editing);
            if (ContainsAny(msg, "export", "pdf", "dwg", "dxf", "ifc", "csv", "import",
                "print", "save", "nwc", "dgn", "image", "png", "jpeg", "dwf",
                "power bi", "powerbi"))
                cats.Add(ToolCategory.Export);
            if (ContainsAny(msg, "check", "audit", "warning", "purge", "clean", "statistic",
                "unused", "issue", "error", "cad", "resolve", "join", "sync"))
                cats.Add(ToolCategory.QAQC);
            if (ContainsAny(msg, "view", "sheet", "section", "elevation", "callout", "template",
                "viewport", "open", "close", "duplicate", "style", "override", "scale",
                "detail level", "crop", "hide", "visibility", "link", "halftone",
                "filter", "workset", "area", "design option"))
                cats.Add(ToolCategory.Views);
            if (ContainsAny(msg, "save", "load", "remember", "note", "stored", "data",
                "snapshot", "history", "preference", "convention", "checklist", "previous",
                "last time", "earlier", "forgot", "recall"))
                cats.Add(ToolCategory.Data);
            if (ContainsAny(msg, "excel", "notion", "google sheets", "integration", "bridge"))
                cats.Add(ToolCategory.Integrations);
            if (ContainsAny(msg, "file", "files", "project file", "boq", "schedule file",
                "analyze file", "read file", "search file"))
                cats.Add(ToolCategory.ProjectFiles);
            if (ContainsAny(msg, "duct", "pipe", "mep", "hvac", "plumbing", "mechanical",
                "air terminal", "fitting", "connector", "space", "electrical", "conduit"))
                cats.Add(ToolCategory.MEP);
            if (ContainsAny(msg, "rebar", "structural", "foundation", "beam", "column",
                "brace", "truss", "analytical", "reinforcement", "connection"))
                cats.Add(ToolCategory.Structural);
            if (ContainsAny(msg, "annotation", "keynote", "filled region", "spot",
                "dimension", "tag", "text note", "detail", "symbol", "label"))
                cats.Add(ToolCategory.Annotation);
            if (ContainsAny(msg, "stair", "railing", "ramp", "curtain",
                "mullion", "panel", "opening", "shaft"))
                cats.Add(ToolCategory.Architecture);
            if (ContainsAny(msg, "topo", "site", "topography", "building pad",
                "property line", "terrain", "grading"))
                cats.Add(ToolCategory.Site);

            // If only reading matched → unclear intent, send all
            if (cats.Count == 1 && !ContainsAny(msg, "get", "show", "list", "what", "which",
                "info", "current", "how many", "tell", "parameter", "family", "room",
                "level", "grid", "schedule", "linked", "selected", "element"))
            {
                foreach (ToolCategory c in Enum.GetValues(typeof(ToolCategory)))
                    cats.Add(c);
            }
            return cats;
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            foreach (var k in keywords)
                if (text.Contains(k)) return true;
            return false;
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL DEFINITIONS — Edit HERE to add/remove authorities
        // ══════════════════════════════════════════════════════════

        private static void RegisterAll()
        {
            // ===================== READING (16 tools) =====================
            R("get_current_view_info", "Active view info", ToolCategory.Reading,
                new[] { "current view", "active view", "view info" });

            R("get_current_view_elements", "Elements in active view", ToolCategory.Reading,
                new[] { "view elements", "elements in view" },
                ToolParam.Opt("category", "string", "Category filter"));

            R("get_selected_elements", "Selected elements", ToolCategory.Reading,
                new[] { "selected", "selection", "what is selected" });

            R("get_elements", "Query elements", ToolCategory.Reading,
                new[] { "elements", "get elements", "find elements", "query" },
                ToolParam.Opt("category", "string", "Category"),
                ToolParam.Opt("typeName", "string", "Type filter"),
                ToolParam.Opt("levelName", "string", "Level filter"));

            R("get_parameters", "Element parameters", ToolCategory.Reading,
                new[] { "parameters", "get parameters", "show parameters", "element parameters" },
                ToolParam.Req("elementId", "integer", "Element ID"));

            R("get_project_info", "Project info", ToolCategory.Reading,
                new[] { "project info", "project details", "project" });

            R("get_views", "List views", ToolCategory.Reading,
                new[] { "views", "list views", "show views", "get views" },
                ToolParam.Opt("type", "string", "Type filter"));

            R("get_sheets", "List sheets", ToolCategory.Reading,
                new[] { "sheets", "list sheets", "show sheets" });

            R("get_levels", "List levels", ToolCategory.Reading,
                new[] { "levels", "list levels", "show levels", "get levels" });

            R("get_grids", "List grids", ToolCategory.Reading,
                new[] { "grids", "list grids", "show grids" });

            R("get_rooms", "List rooms", ToolCategory.Reading,
                new[] { "rooms", "list rooms", "show rooms", "get rooms" });

            R("get_available_family_types", "Available family types", ToolCategory.Reading,
                new[] { "family types", "families", "available types" },
                ToolParam.Opt("category", "string", "Category"));

            R("get_schedules", "List schedules", ToolCategory.Reading,
                new[] { "schedules", "list schedules" });

            R("get_linked_models", "Linked models", ToolCategory.Reading,
                new[] { "linked models", "links", "linked" });

            R("get_warnings", "Model warnings", ToolCategory.Reading,
                new[] { "warnings", "model warnings", "show warnings" });

            R("get_family_info", "Family info", ToolCategory.Reading,
                new[] { "family info", "family details" },
                ToolParam.Opt("category", "string", "Category"),
                ToolParam.Opt("familyName", "string", "Family name"));

            // ===================== CREATING (20 tools) =====================
            R("create_wall", "Create wall", ToolCategory.Creating,
                new[] { "create wall", "add wall", "new wall", "make wall" },
                ToolParam.Req("startX", "number", "Start X (ft)"),
                ToolParam.Req("startY", "number", "Start Y (ft)"),
                ToolParam.Req("endX", "number", "End X (ft)"),
                ToolParam.Req("endY", "number", "End Y (ft)"),
                ToolParam.Req("levelName", "string", "Level"),
                ToolParam.Opt("height", "number", "Height (ft)"));

            R("create_floor", "Create floor from boundary points", ToolCategory.Creating,
                new[] { "create floor", "add floor", "new floor" },
                ToolParam.Arr("points", "Boundary points [{x,y}] in ft", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" } } }),
                ToolParam.Req("levelName", "string", "Level"),
                ToolParam.Opt("typeName", "string", "Floor type"));

            R("create_ceiling", "Create ceiling from boundary points", ToolCategory.Creating,
                new[] { "create ceiling", "add ceiling" },
                ToolParam.Arr("points", "Boundary points [{x,y}] in ft", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" } } }),
                ToolParam.Req("levelName", "string", "Level"),
                ToolParam.Opt("typeName", "string", "Ceiling type"));

            R("create_roof", "Create roof from boundary points", ToolCategory.Creating,
                new[] { "create roof", "add roof" },
                ToolParam.Arr("points", "Boundary points [{x,y}] in ft", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" } } }),
                ToolParam.Req("levelName", "string", "Level"),
                ToolParam.Opt("typeName", "string", "Roof type"));

            R("create_level", "Create level", ToolCategory.Creating,
                new[] { "create level", "add level", "new level" },
                ToolParam.Req("name", "string", "Name"),
                ToolParam.Req("elevation", "number", "Elevation (ft)"));

            R("create_grid", "Create grid", ToolCategory.Creating,
                new[] { "create grid", "add grid", "new grid" },
                ToolParam.Req("startX", "number", "Start X (ft)"),
                ToolParam.Req("startY", "number", "Start Y (ft)"),
                ToolParam.Req("endX", "number", "End X (ft)"),
                ToolParam.Req("endY", "number", "End Y (ft)"),
                ToolParam.Opt("name", "string", "Name"));

            R("create_room", "Create room", ToolCategory.Creating,
                new[] { "create room", "add room", "new room", "place room" },
                ToolParam.Req("x", "number", "X (ft)"),
                ToolParam.Req("y", "number", "Y (ft)"),
                ToolParam.Req("levelName", "string", "Level"),
                ToolParam.Opt("roomName", "string", "Name"),
                ToolParam.Opt("roomNumber", "string", "Number"));

            R("create_sheet", "Create sheet", ToolCategory.Creating,
                new[] { "create sheet", "add sheet", "new sheet" },
                ToolParam.Opt("sheetNumber", "string", "Number"),
                ToolParam.Opt("sheetName", "string", "Name"),
                ToolParam.Opt("titleBlockName", "string", "Title block"));

            R("create_view", "Create a new view", ToolCategory.Creating,
                new[] { "create view", "new view", "open 3d view", "3d view" },
                ToolParam.Req("viewType", "string", "FloorPlan/CeilingPlan/Section/Elevation/ThreeD/Drafting"),
                ToolParam.Opt("levelName", "string", "Level (for plan views)"),
                ToolParam.Opt("name", "string", "View name"));

            R("create_schedule", "Create a schedule view", ToolCategory.Creating,
                new[] { "create schedule", "add schedule", "new schedule" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("name", "string", "Schedule name"),
                ToolParam.Opt("fields", "string", "Parameter names (comma-sep)"));

            R("create_tag", "Place a tag on an element", ToolCategory.Creating,
                new[] { "create tag", "add tag", "tag element" },
                ToolParam.Req("elementId", "integer", "Element ID"),
                ToolParam.Opt("tagType", "string", "Tag type name"),
                ToolParam.Opt("hasLeader", "boolean", "Show leader"));

            R("create_dimension", "Create a dimension between elements", ToolCategory.Creating,
                new[] { "create dimension", "add dimension" },
                ToolParam.Arr("elementIds", "Element IDs to dimension", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("dimensionType", "string", "Dimension type"));

            R("create_text_note", "Place a text note", ToolCategory.Creating,
                new[] { "create text", "add text note", "place text" },
                ToolParam.Req("text", "string", "Text content"),
                ToolParam.Req("x", "number", "X position (ft)"),
                ToolParam.Req("y", "number", "Y position (ft)"));

            R("create_project_parameter", "Create project parameter", ToolCategory.Creating,
                new[] { "create parameter", "add parameter", "new parameter", "project parameter" },
                ToolParam.Req("name", "string", "Name"),
                ToolParam.Arr("categories", "Categories", new JObject { ["type"] = "string" }),
                ToolParam.Opt("type", "string", "Text/Integer/Number/Length/Area/Volume/YesNo"),
                ToolParam.Opt("isInstance", "boolean", "Instance param (default: true)"));

            R("create_point_based_element", "Place point-based family instance", ToolCategory.Creating,
                new[] { "place element", "place family", "point based" },
                ToolParam.Req("familyName", "string", "Family name"),
                ToolParam.Req("typeName", "string", "Type name"),
                ToolParam.Req("x", "number", "X (ft)"),
                ToolParam.Req("y", "number", "Y (ft)"),
                ToolParam.Req("levelName", "string", "Level"));

            R("create_line_based_element", "Place line-based family instance", ToolCategory.Creating,
                new[] { "line based element", "place line family" },
                ToolParam.Req("familyName", "string", "Family name"),
                ToolParam.Req("typeName", "string", "Type name"),
                ToolParam.Req("startX", "number", "Start X (ft)"),
                ToolParam.Req("startY", "number", "Start Y (ft)"),
                ToolParam.Req("endX", "number", "End X (ft)"),
                ToolParam.Req("endY", "number", "End Y (ft)"),
                ToolParam.Req("levelName", "string", "Level"));

            R("room_to_floor", "Create floors from room boundaries", ToolCategory.Creating,
                new[] { "room to floor", "floor from room" },
                ToolParam.Opt("roomIds", "string", "Room IDs (comma-sep, or all)"),
                ToolParam.Opt("floorType", "string", "Floor type name"));

            R("add_shared_parameter", "Add shared parameter to category", ToolCategory.Creating,
                new[] { "shared parameter", "add shared parameter" },
                ToolParam.Req("parameterName", "string", "Parameter name"),
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("groupName", "string", "Group (default: Data)"),
                ToolParam.Opt("paramType", "string", "Text/Number/Length/Area/Volume/YesNo"),
                ToolParam.Opt("isInstance", "boolean", "Instance param (default: true)"));

            R("generate_legend", "Generate legend view (Doors/Windows)", ToolCategory.Creating,
                new[] { "generate legend", "create legend", "legend" },
                ToolParam.Opt("category", "string", "Doors or Windows"),
                ToolParam.Opt("legendName", "string", "Legend name"));

            R("convert_category", "Convert elements to different family/type", ToolCategory.Creating,
                new[] { "convert category", "convert elements", "convert type" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Req("targetFamily", "string", "Target family"),
                ToolParam.Opt("targetType", "string", "Target type"));

            // ===================== EDITING (26 tools) =====================
            R("modify_element", "Modify element parameters", ToolCategory.Editing,
                new[] { "modify element", "edit element", "change element", "update element" },
                ToolParam.Req("elementId", "integer", "Element ID"),
                ToolParam.Arr("modifications", "Parameter changes", new JObject { ["type"] = "object", ["properties"] = new JObject { ["parameterName"] = new JObject { ["type"] = "string" }, ["value"] = new JObject { ["type"] = "string" } }, ["required"] = new JArray("parameterName", "value") }));

            R("move_element", "Move element", ToolCategory.Editing,
                new[] { "move element", "move" },
                ToolParam.Req("elementId", "integer", "Element ID"),
                ToolParam.Req("deltaX", "number", "Delta X (ft)"),
                ToolParam.Req("deltaY", "number", "Delta Y (ft)"),
                ToolParam.Opt("deltaZ", "number", "Delta Z (ft)"));

            R("delete_elements", "Delete elements", ToolCategory.Editing,
                new[] { "delete elements", "remove elements", "delete" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }));

            R("copy_element", "Copy element to new location", ToolCategory.Editing,
                new[] { "copy element", "duplicate element" },
                ToolParam.Req("elementId", "integer", "Element ID"),
                ToolParam.Req("deltaX", "number", "Delta X (ft)"),
                ToolParam.Req("deltaY", "number", "Delta Y (ft)"),
                ToolParam.Opt("deltaZ", "number", "Delta Z (ft)"));

            R("mirror_element", "Mirror element about an axis", ToolCategory.Editing,
                new[] { "mirror element", "mirror" },
                ToolParam.Req("elementId", "integer", "Element ID"),
                ToolParam.Opt("axisStartX", "number", "Axis start X"),
                ToolParam.Opt("axisStartY", "number", "Axis start Y"),
                ToolParam.Opt("axisEndX", "number", "Axis end X"),
                ToolParam.Opt("axisEndY", "number", "Axis end Y"));

            R("select_elements", "Select elements in UI", ToolCategory.Editing,
                new[] { "select elements", "select", "highlight" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }));

            R("bulk_rename_views", "Find/replace in view/sheet names", ToolCategory.Editing,
                new[] { "rename views", "bulk rename", "find replace" },
                ToolParam.Req("find", "string", "Find text"),
                ToolParam.Req("replace", "string", "Replace text"),
                ToolParam.Opt("targetType", "string", "views/sheets/both"));

            R("select_by_filter", "Select elements by filter", ToolCategory.Editing,
                new[] { "select by filter", "filter select" },
                ToolParam.Opt("category", "string", "Category"),
                ToolParam.Opt("familyName", "string", "Family"),
                ToolParam.Opt("typeName", "string", "Type"),
                ToolParam.Opt("levelName", "string", "Level"));

            R("copy_parameter_value", "Copy parameter between elements", ToolCategory.Editing,
                new[] { "copy parameter", "transfer parameter" },
                ToolParam.Req("sourceElementId", "integer", "Source ID"),
                ToolParam.Req("parameterName", "string", "Parameter"),
                ToolParam.Arr("targetElementIds", "Target IDs", new JObject { ["type"] = "integer" }));

            R("color_by_parameter", "Color elements by parameter value", ToolCategory.Editing,
                new[] { "color by parameter", "color elements", "colorize" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Req("parameterName", "string", "Parameter"));

            R("align_elements", "Align elements", ToolCategory.Editing,
                new[] { "align elements", "align" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Req("alignment", "string", "left/right/top/bottom/center-h/center-v"),
                ToolParam.Opt("referenceElementId", "integer", "Reference ID"));

            R("group_elements", "Group elements together", ToolCategory.Editing,
                new[] { "group elements", "group", "create group" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("groupName", "string", "Group name"));

            R("change_type", "Change element type", ToolCategory.Editing,
                new[] { "change type", "switch type" },
                ToolParam.Req("elementId", "integer", "Element ID"),
                ToolParam.Req("newTypeName", "string", "New type name"));

            R("set_workset", "Move elements to workset", ToolCategory.Editing,
                new[] { "set workset", "move to workset" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Req("worksetName", "string", "Workset name"));

            R("renumber_elements", "Renumber by spatial order", ToolCategory.Editing,
                new[] { "renumber", "renumber elements" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("parameterName", "string", "Parameter (default: Mark)"),
                ToolParam.Opt("prefix", "string", "Prefix"),
                ToolParam.Opt("startNumber", "integer", "Start number"));

            R("extend_shrink_element", "Extend/shrink line element", ToolCategory.Editing,
                new[] { "extend element", "shrink element" },
                ToolParam.Req("elementId", "integer", "Element ID"),
                ToolParam.Req("delta", "number", "+extend/-shrink (ft)"),
                ToolParam.Opt("end", "string", "start or end"));

            R("rotate_element", "Rotate element", ToolCategory.Editing,
                new[] { "rotate element", "rotate" },
                ToolParam.Req("elementId", "integer", "Element ID"),
                ToolParam.Req("angle", "number", "Angle (degrees)"));

            R("manage_families", "Batch rename families", ToolCategory.Editing,
                new[] { "manage families", "rename families" },
                ToolParam.Req("action", "string", "rename/add_prefix/add_suffix/find_replace"),
                ToolParam.Opt("category", "string", "Category"),
                ToolParam.Opt("find", "string", "Find"),
                ToolParam.Opt("replace", "string", "Replace"),
                ToolParam.Opt("prefix", "string", "Prefix"),
                ToolParam.Opt("suffix", "string", "Suffix"));

            R("batch_set_parameter", "Set parameter on matching elements", ToolCategory.Editing,
                new[] { "batch set", "set parameter", "batch parameter" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Req("parameterName", "string", "Parameter"),
                ToolParam.Req("value", "string", "Value"),
                ToolParam.Opt("filterParameterName", "string", "Filter parameter"),
                ToolParam.Opt("filterValue", "string", "Filter value"),
                ToolParam.Opt("levelName", "string", "Level filter"));

            R("set_phase", "Set element phase", ToolCategory.Editing,
                new[] { "set phase" },
                ToolParam.Req("elementId", "integer", "Element ID"),
                ToolParam.Req("phaseName", "string", "Phase name"));

            R("set_material", "Set element material", ToolCategory.Editing,
                new[] { "set material", "change material" },
                ToolParam.Req("elementId", "integer", "Element ID"),
                ToolParam.Req("materialName", "string", "Material name"),
                ToolParam.Opt("parameterName", "string", "Material parameter name"));

            R("batch_modify_parameters", "Set parameter on specific elements by ID", ToolCategory.Editing,
                new[] { "batch modify" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Req("parameterName", "string", "Parameter"),
                ToolParam.Req("value", "string", "Value"));

            R("parameter_case_convert", "Convert parameter text case (UPPER/lower/Title)", ToolCategory.Editing,
                new[] { "case convert", "uppercase", "lowercase", "title case" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Req("parameterName", "string", "Parameter"),
                ToolParam.Opt("caseType", "string", "UPPER/lower/Title"));

            R("select_by_workset", "Select all elements on a workset", ToolCategory.Editing,
                new[] { "select by workset" },
                ToolParam.Req("worksetName", "string", "Workset name"));

            R("filter_selection", "Filter current selection by category/level", ToolCategory.Editing,
                new[] { "filter selection" },
                ToolParam.Opt("category", "string", "Category"),
                ToolParam.Opt("levelName", "string", "Level"));

            R("inverse_selection", "Invert current selection in active view", ToolCategory.Editing,
                new[] { "inverse selection", "invert selection" });

            // ===================== EXPORT (14 tools) =====================
            R("export_to_cad", "Export to DWG/DXF", ToolCategory.Export,
                new[] { "export to dwg", "export to dxf", "export to cad", "export cad" },
                ToolParam.Arr("viewIds", "View IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("folder", "string", "Folder"),
                ToolParam.Opt("format", "string", "DWG or DXF"));

            R("export_to_pdf", "Export to PDF", ToolCategory.Export,
                new[] { "export to pdf", "export pdf", "print pdf" },
                ToolParam.Opt("sheetIds", "string", "Sheet IDs (comma-sep)"),
                ToolParam.Opt("viewIds", "string", "View IDs (comma-sep)"),
                ToolParam.Opt("folder", "string", "Folder"),
                ToolParam.Opt("combinePdf", "boolean", "Combine into one PDF"));

            R("export_to_ifc", "Export to IFC", ToolCategory.Export,
                new[] { "export to ifc", "export ifc" },
                ToolParam.Opt("folder", "string", "Folder"),
                ToolParam.Opt("ifcVersion", "string", "IFC2x3 or IFC4"),
                ToolParam.Opt("fileName", "string", "Filename"));

            R("export_to_images", "Export to images", ToolCategory.Export,
                new[] { "export to image", "export images", "export png" },
                ToolParam.Arr("viewIds", "View IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("folder", "string", "Folder"),
                ToolParam.Opt("format", "string", "PNG/JPEG/TIFF/BMP"),
                ToolParam.Opt("resolution", "integer", "DPI"));

            R("export_to_dgn", "Export to DGN", ToolCategory.Export,
                new[] { "export to dgn" },
                ToolParam.Arr("viewIds", "View IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("folder", "string", "Folder"));

            R("export_to_nwc", "Export to NWC", ToolCategory.Export,
                new[] { "export to nwc", "navisworks" },
                ToolParam.Opt("folder", "string", "Folder"),
                ToolParam.Opt("fileName", "string", "Filename"));

            R("export_schedule_data", "Export schedule to CSV", ToolCategory.Export,
                new[] { "export schedule" },
                ToolParam.Opt("scheduleId", "integer", "Schedule ID"),
                ToolParam.Opt("scheduleName", "string", "Schedule name"),
                ToolParam.Opt("folder", "string", "Folder"));

            R("export_parameters_to_csv", "Export parameters to CSV", ToolCategory.Export,
                new[] { "export parameters" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("parameterNames", "string", "Parameters (comma-sep)"),
                ToolParam.Opt("folder", "string", "Folder"),
                ToolParam.Opt("levelName", "string", "Level filter"));

            R("import_parameters_from_csv", "Import parameters from CSV", ToolCategory.Export,
                new[] { "import parameters" },
                ToolParam.Req("filePath", "string", "CSV file path"),
                ToolParam.Opt("dryRun", "boolean", "Preview only"));

            R("export_to_dwf", "Export views to DWF", ToolCategory.Export,
                new[] { "export to dwf" },
                ToolParam.Opt("sheetIds", "string", "Sheet IDs (comma-sep)"),
                ToolParam.Opt("viewIds", "string", "View IDs (comma-sep)"),
                ToolParam.Opt("outputFolder", "string", "Output folder"));

            R("export_to_powerbi", "Export 3D model with geometry to SQLite for Power BI visualization", ToolCategory.Export,
                new[] { "power bi", "powerbi", "3d export", "export to power bi", "sqlite 3d" },
                ToolParam.Opt("exportScope", "string", "currentView or allModel (default: currentView)"),
                ToolParam.Opt("dbPath", "string", "SQLite file path (default: data/RevitMCP_PowerBI.db)"),
                ToolParam.Opt("mode", "string", "new or update (default: new)"),
                ToolParam.Opt("categories", "string", "Category filter (comma-sep, default: all 3D)"));

            // ===================== QA/QC (13 tools) =====================
            R("check_warnings", "Check model warnings", ToolCategory.QAQC,
                new[] { "check warnings", "warnings" });

            R("audit_model", "Audit model", ToolCategory.QAQC,
                new[] { "audit", "audit model" });

            R("get_model_statistics", "Model statistics", ToolCategory.QAQC,
                new[] { "statistics", "stats", "model statistics", "model stats" });

            R("purge_unused", "Purge unused families/types", ToolCategory.QAQC,
                new[] { "purge", "purge unused", "clean up" },
                ToolParam.Opt("category", "string", "Category"));

            R("deep_purge", "Deep multi-pass purge of all unused elements", ToolCategory.QAQC,
                new[] { "deep purge" });

            R("isolate_warnings", "Select warning elements", ToolCategory.QAQC,
                new[] { "isolate warnings" },
                ToolParam.Opt("filter", "string", "Warning text filter"));

            R("purge_cads", "Remove all CAD imports", ToolCategory.QAQC,
                new[] { "purge cads", "remove cad" });

            R("delete_unused_families", "Delete zero-instance families", ToolCategory.QAQC,
                new[] { "delete unused families" },
                ToolParam.Opt("category", "string", "Category"),
                ToolParam.Opt("dryRun", "boolean", "Preview only"));

            R("find_cad_imports", "Find/delete CAD imports", ToolCategory.QAQC,
                new[] { "find cad", "cad imports" },
                ToolParam.Opt("delete", "boolean", "Delete found imports"));

            R("delete_empty_groups", "Delete empty model groups", ToolCategory.QAQC,
                new[] { "delete empty groups" });

            R("resolve_warnings", "List or auto-resolve warnings", ToolCategory.QAQC,
                new[] { "resolve warnings" },
                ToolParam.Opt("action", "string", "list or resolve"),
                ToolParam.Opt("warningType", "string", "Warning text filter"));

            R("auto_join_elements", "Auto-join geometry between categories", ToolCategory.QAQC,
                new[] { "auto join", "join elements" },
                ToolParam.Opt("category1", "string", "First category (default: Walls)"),
                ToolParam.Opt("category2", "string", "Second category (default: Floors)"));

            R("wall_floor_sync", "Sync wall-floor connections", ToolCategory.QAQC,
                new[] { "wall floor sync" },
                ToolParam.Opt("levelName", "string", "Level filter"));

            R("import_data_from_csv", "Import parameter data from CSV", ToolCategory.QAQC,
                new[] { "import from csv", "import csv data" },
                ToolParam.Req("filePath", "string", "CSV file path"),
                ToolParam.Opt("category", "string", "Category"),
                ToolParam.Opt("keyParameter", "string", "Key column (default: Number)"));

            // ===================== VIEWS (22 tools) =====================
            R("duplicate_sheets", "Duplicate sheet", ToolCategory.Views,
                new[] { "duplicate sheet", "copy sheet" },
                ToolParam.Req("sheetId", "integer", "Sheet ID"),
                ToolParam.Opt("count", "integer", "Copies"),
                ToolParam.Opt("suffix", "string", "Suffix"));

            R("auto_section_box", "3D section box around elements", ToolCategory.Views,
                new[] { "section box", "auto section box" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("padding", "number", "Padding (ft)"));

            R("copy_view_filters", "Copy view filters", ToolCategory.Views,
                new[] { "copy filters" },
                ToolParam.Req("sourceViewId", "integer", "Source view ID"),
                ToolParam.Arr("targetViewIds", "Target view IDs", new JObject { ["type"] = "integer" }));

            R("place_views_on_sheet", "Place views on sheet", ToolCategory.Views,
                new[] { "place on sheet", "add to sheet" },
                ToolParam.Req("sheetId", "integer", "Sheet ID"),
                ToolParam.Arr("viewIds", "View IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("startX", "number", "X offset (ft)"),
                ToolParam.Opt("startY", "number", "Y offset (ft)"),
                ToolParam.Opt("spacing", "number", "Spacing (ft)"));

            R("tag_all_in_view", "Tag all elements of a category in view", ToolCategory.Views,
                new[] { "tag all", "tag everything" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("tagType", "string", "Tag type"));

            R("create_elevation_views", "Create room elevations", ToolCategory.Views,
                new[] { "create elevation", "room elevations" },
                ToolParam.Opt("roomIds", "string", "Room IDs (comma-sep)"),
                ToolParam.Opt("levelName", "string", "Level"),
                ToolParam.Opt("viewTemplate", "string", "Template"),
                ToolParam.Opt("scale", "integer", "Scale"));

            R("create_section_views", "Create room sections", ToolCategory.Views,
                new[] { "create section", "room sections" },
                ToolParam.Opt("roomIds", "string", "Room IDs (comma-sep)"),
                ToolParam.Opt("direction", "string", "horizontal/vertical"),
                ToolParam.Opt("viewTemplate", "string", "Template"),
                ToolParam.Opt("scale", "integer", "Scale"));

            R("create_callout_views", "Create room callouts", ToolCategory.Views,
                new[] { "create callout", "room callouts" },
                ToolParam.Opt("roomIds", "string", "Room IDs (comma-sep)"),
                ToolParam.Opt("parentViewId", "integer", "Parent view ID"),
                ToolParam.Opt("viewTemplate", "string", "Template"),
                ToolParam.Opt("scale", "integer", "Scale"));

            R("align_viewports", "Align viewports across sheets", ToolCategory.Views,
                new[] { "align viewports" },
                ToolParam.Req("referenceSheetId", "integer", "Reference sheet ID"),
                ToolParam.Arr("targetSheetIds", "Target sheet IDs", new JObject { ["type"] = "integer" }));

            R("batch_create_sheets", "Create multiple sheets", ToolCategory.Views,
                new[] { "batch sheets", "multiple sheets" },
                ToolParam.Req("startNumber", "string", "Start number"),
                ToolParam.Req("count", "integer", "Count"),
                ToolParam.Opt("namePattern", "string", "Pattern ({n})"),
                ToolParam.Opt("titleBlockName", "string", "Title block"));

            R("duplicate_view", "Duplicate view", ToolCategory.Views,
                new[] { "duplicate view", "copy view" },
                ToolParam.Req("viewId", "integer", "View ID"),
                ToolParam.Opt("count", "integer", "Copies"),
                ToolParam.Opt("duplicateType", "string", "independent/as_dependent/with_detailing"),
                ToolParam.Opt("suffix", "string", "Suffix"));

            R("apply_view_template", "Apply view template", ToolCategory.Views,
                new[] { "apply template", "view template" },
                ToolParam.Arr("viewIds", "View IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Req("templateName", "string", "Template name"));

            R("set_view_properties", "Set view properties", ToolCategory.Views,
                new[] { "view properties", "set scale", "set detail level" },
                ToolParam.Opt("viewId", "integer", "View ID"),
                ToolParam.Opt("scale", "integer", "Scale"),
                ToolParam.Opt("detailLevel", "string", "Coarse/Medium/Fine"),
                ToolParam.Opt("displayStyle", "string", "Wireframe/HiddenLine/Shading/ShadingWithEdges/Realistic"),
                ToolParam.Opt("discipline", "string", "Discipline"),
                ToolParam.Opt("phaseName", "string", "Phase"),
                ToolParam.Opt("viewName", "string", "New name"),
                ToolParam.Opt("showCropBox", "boolean", "Show crop box"));

            R("override_element_in_view", "Graphic overrides in view", ToolCategory.Views,
                new[] { "override element", "graphic override" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("colorR", "integer", "Red (0-255)"),
                ToolParam.Opt("colorG", "integer", "Green (0-255)"),
                ToolParam.Opt("colorB", "integer", "Blue (0-255)"),
                ToolParam.Opt("lineWeight", "integer", "Line weight (1-16)"),
                ToolParam.Opt("transparency", "integer", "Transparency (0-100)"),
                ToolParam.Opt("halftone", "boolean", "Halftone"),
                ToolParam.Opt("visible", "boolean", "Visible"));

            R("modify_object_styles", "Modify category object styles", ToolCategory.Views,
                new[] { "object styles" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("subcategory", "string", "Subcategory"),
                ToolParam.Opt("lineWeight", "integer", "Line weight (1-16)"),
                ToolParam.Opt("colorR", "integer", "Red"),
                ToolParam.Opt("colorG", "integer", "Green"),
                ToolParam.Opt("colorB", "integer", "Blue"));

            R("open_view", "Open view", ToolCategory.Views,
                new[] { "open view" },
                ToolParam.Req("viewId", "integer", "View ID"));

            R("close_view", "Close view", ToolCategory.Views,
                new[] { "close view" },
                ToolParam.Opt("viewId", "integer", "View ID (default: active)"));

            R("set_visibility_graphics", "Show/hide categories or linked models in view", ToolCategory.Views,
                new[] { "visibility graphics", "show category", "hide category" },
                ToolParam.Opt("viewId", "integer", "View ID (default: active)"),
                ToolParam.Opt("category", "string", "Category"),
                ToolParam.Opt("visible", "boolean", "Show/hide"),
                ToolParam.Opt("hideLinks", "boolean", "Hide all Revit links"),
                ToolParam.Opt("linkName", "string", "Specific link name"),
                ToolParam.Opt("halftone", "boolean", "Halftone"),
                ToolParam.Opt("transparency", "integer", "Transparency (0-100)"));

            R("crop_region_sync", "Sync crop regions between views", ToolCategory.Views,
                new[] { "sync crop region" },
                ToolParam.Req("sourceViewId", "integer", "Source view ID"),
                ToolParam.Arr("targetViewIds", "Target view IDs", new JObject { ["type"] = "integer" }));

            R("get_line_styles", "Get available line styles", ToolCategory.Views,
                new[] { "line styles" });

            R("set_line_style", "Set line style on detail lines", ToolCategory.Views,
                new[] { "set line style" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Req("lineStyleName", "string", "Line style name"));

            R("get_phases", "Get project phases", ToolCategory.Views,
                new[] { "phases", "get phases" });

            R("get_materials", "Get project materials", ToolCategory.Views,
                new[] { "materials", "get materials" });

            R("cad_to_lines", "Convert CAD imports to Revit detail lines", ToolCategory.Views,
                new[] { "cad to lines", "convert cad" },
                ToolParam.Opt("deleteAfter", "boolean", "Delete CAD after conversion"),
                ToolParam.Opt("importIds", "string", "CAD import IDs (comma-sep, or all)"));

            // ===================== DATA (12 tools) =====================
            R("save_project_data", "Save JSON data to persist between sessions", ToolCategory.Data,
                new[] { "save data", "save project data", "store data" },
                ToolParam.Req("key", "string", "Data key"),
                ToolParam.Req("data", "string", "Data to save (JSON or text)"));

            R("load_project_data", "Load previously saved project data", ToolCategory.Data,
                new[] { "load data", "load project data" },
                ToolParam.Req("key", "string", "Data key"));

            R("list_project_data", "List all saved data for this project", ToolCategory.Data,
                new[] { "list data", "list project data" });

            R("delete_project_data", "Delete saved data", ToolCategory.Data,
                new[] { "delete data" },
                ToolParam.Req("key", "string", "Data key"));

            R("save_snapshot", "Capture model state (element counts, warnings, rooms, etc.)", ToolCategory.Data,
                new[] { "save snapshot", "snapshot", "capture state" });

            R("create_view_filter", "Create parameter-based view filter", ToolCategory.Data,
                new[] { "create filter", "view filter" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Req("parameterName", "string", "Parameter"),
                ToolParam.Opt("filterName", "string", "Filter name"),
                ToolParam.Opt("ruleType", "string", "equals/contains/greater/less"),
                ToolParam.Opt("value", "string", "Rule value"),
                ToolParam.Opt("applyToView", "boolean", "Apply to active view (default: true)"));

            R("get_worksets", "List worksets in workshared project", ToolCategory.Data,
                new[] { "worksets", "get worksets" });

            R("get_areas", "List areas and area plans", ToolCategory.Data,
                new[] { "areas", "get areas" });

            R("get_design_options", "List design options", ToolCategory.Data,
                new[] { "design options", "get design options" });

            R("reassign_level", "Reassign elements to a different level", ToolCategory.Data,
                new[] { "reassign level" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Req("targetLevel", "string", "Target level name"),
                ToolParam.Opt("maintainOffset", "boolean", "Maintain elevation offset (default: true)"));

            R("batch_modify_thickness", "Modify wall/floor type thickness", ToolCategory.Data,
                new[] { "modify thickness", "change thickness" },
                ToolParam.Req("typeName", "string", "Type name"),
                ToolParam.Req("thickness", "number", "New thickness (ft)"),
                ToolParam.Opt("category", "string", "Walls or Floors"));

            R("copy_from_linked", "Copy elements from linked model", ToolCategory.Data,
                new[] { "copy from linked", "copy from link" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("linkName", "string", "Link name filter"));

            R("snap_beams_to_columns", "Snap beam endpoints to column centerlines", ToolCategory.Data,
                new[] { "snap beams" },
                ToolParam.Opt("tolerance", "number", "Snap tolerance (ft)"));

            R("category_to_workset", "Move category elements to workset", ToolCategory.Data,
                new[] { "category to workset" },
                ToolParam.Arr("mappings", "Category-workset pairs", new JObject { ["type"] = "object", ["properties"] = new JObject { ["category"] = new JObject { ["type"] = "string" }, ["worksetName"] = new JObject { ["type"] = "string" } } }));

            // ===================== INTEGRATIONS (4 tools) =====================
            R("get_integration_status", "Get status of all integrations", ToolCategory.Integrations,
                new[] { "integration status", "integrations" });

            R("export_to_excel_integration", "Export Revit elements to Excel via Data Bridge", ToolCategory.Integrations,
                new[] { "export to excel", "excel integration" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("filePath", "string", "Output .xlsx file path"));

            R("export_to_notion_integration", "Export Revit elements to Notion via Data Bridge", ToolCategory.Integrations,
                new[] { "export to notion", "notion" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("databaseId", "string", "Notion database ID"));

            R("export_to_google_sheets_integration", "Export Revit elements to Google Sheets via Data Bridge", ToolCategory.Integrations,
                new[] { "export to google", "google sheets" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("spreadsheetId", "string", "Google Sheets spreadsheet ID"));

            // ===================== PROJECT FILES (7 tools) =====================
            R("list_project_files", "List all files in the project folder", ToolCategory.ProjectFiles,
                new[] { "project files", "my files", "list files" },
                ToolParam.Opt("filter", "string", "File extension filter (e.g. xlsx)"));

            R("read_project_file", "Read contents of a project file (Excel/CSV/TXT)", ToolCategory.ProjectFiles,
                new[] { "read file", "read excel", "open file", "read boq" },
                ToolParam.Opt("fileName", "string", "File name"),
                ToolParam.Opt("search", "string", "Search term for partial match"));

            R("analyze_project_file", "Analyze a file: detect type, summarize structure", ToolCategory.ProjectFiles,
                new[] { "analyze file", "check file", "analyze excel" },
                ToolParam.Opt("fileName", "string", "File name"),
                ToolParam.Opt("search", "string", "Search term"));

            R("search_project_files", "Search across project files for a keyword", ToolCategory.ProjectFiles,
                new[] { "search files", "find in files" },
                ToolParam.Req("keyword", "string", "Keyword to search for"));

            R("export_elements_to_csv", "Export Revit elements to CSV in project folder", ToolCategory.ProjectFiles,
                new[] { "export to csv", "save to csv", "export csv" },
                ToolParam.Req("category", "string", "Category (e.g. Walls, Rooms)"),
                ToolParam.Opt("fileName", "string", "Output file name"));

            R("export_elements_to_excel", "Export Revit elements to Excel-compatible CSV", ToolCategory.ProjectFiles,
                new[] { "save to excel", "create excel" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("fileName", "string", "Output file name"));

            R("import_from_project_file", "Import data from CSV/Excel in project folder", ToolCategory.ProjectFiles,
                new[] { "import file", "import excel", "import csv" },
                ToolParam.Opt("fileName", "string", "File name"),
                ToolParam.Opt("search", "string", "Search term"));

            // ===================== EXCEL TOOLS (8 tools) =====================
            R("excel_create_workbook", "Create a new Excel .xlsx workbook with sheets and headers", ToolCategory.ProjectFiles,
                new[] { "create workbook", "new excel", "create spreadsheet", "make excel" },
                ToolParam.Opt("fileName", "string", "File name (default: Workbook.xlsx)"),
                ToolParam.Opt("sheetName", "string", "Sheet name (for single sheet)"),
                ToolParam.Opt("headers", "string", "Comma-separated column headers"),
                ToolParam.Arr("sheets", "Sheet definitions [{name,headers:[]}]", new JObject { ["type"] = "object" }));

            R("excel_read_range", "Read cells from Excel range (e.g. Sheet1!A1:D10)", ToolCategory.ProjectFiles,
                new[] { "read excel range", "read cells", "get excel data", "read spreadsheet" },
                ToolParam.Req("fileName", "string", "Excel file name"),
                ToolParam.Opt("sheetName", "string", "Sheet name"),
                ToolParam.Opt("range", "string", "Cell range (e.g. A1:D10)"));

            R("excel_write_cells", "Write data to Excel cells or range", ToolCategory.ProjectFiles,
                new[] { "write excel", "write cells", "update excel", "fill excel" },
                ToolParam.Req("fileName", "string", "Excel file name"),
                ToolParam.Opt("sheetName", "string", "Sheet name"),
                ToolParam.Opt("cells", "string", "Cell→value map as JSON: {\"A1\":\"hello\",\"B2\":42}"),
                ToolParam.Opt("startCell", "string", "Start cell for row data (default: A1)"),
                ToolParam.Arr("data", "Row data as 2D array [[val,val],[val,val]]", new JObject { ["type"] = "array" }));

            R("excel_add_sheet", "Add, rename, or delete Excel sheets", ToolCategory.ProjectFiles,
                new[] { "add sheet", "rename sheet", "delete sheet" },
                ToolParam.Req("fileName", "string", "Excel file name"),
                ToolParam.Opt("action", "string", "add/rename/delete (default: add)"),
                ToolParam.Opt("sheetName", "string", "Sheet name"),
                ToolParam.Opt("newName", "string", "New name (for rename)"));

            R("excel_insert_rows", "Insert or delete rows/columns in Excel", ToolCategory.ProjectFiles,
                new[] { "insert row", "delete row", "insert column", "add row" },
                ToolParam.Req("fileName", "string", "Excel file name"),
                ToolParam.Opt("sheetName", "string", "Sheet name"),
                ToolParam.Opt("action", "string", "insert_row/delete_row/insert_column/delete_column"),
                ToolParam.Opt("position", "integer", "Row/column number"),
                ToolParam.Opt("count", "integer", "Number of rows/columns (default: 1)"));

            R("excel_format_cells", "Format Excel cells (bold, color, borders, number format)", ToolCategory.ProjectFiles,
                new[] { "format excel", "style excel", "bold excel", "color cells" },
                ToolParam.Req("fileName", "string", "Excel file name"),
                ToolParam.Opt("sheetName", "string", "Sheet name"),
                ToolParam.Opt("range", "string", "Cell range (e.g. A1:D1)"),
                ToolParam.Opt("bold", "boolean", "Bold text"),
                ToolParam.Opt("italic", "boolean", "Italic text"),
                ToolParam.Opt("fontSize", "number", "Font size"),
                ToolParam.Opt("fontColor", "string", "Font color (name or #hex)"),
                ToolParam.Opt("backgroundColor", "string", "Background color"),
                ToolParam.Opt("numberFormat", "string", "Number format (e.g. #,##0.00)"),
                ToolParam.Opt("border", "string", "Border style: thin/thick/double"),
                ToolParam.Opt("alignment", "string", "Text alignment: left/center/right"),
                ToolParam.Opt("merge", "boolean", "Merge cells in range"),
                ToolParam.Opt("autoFit", "boolean", "Auto-fit column widths"));

            R("excel_add_formula", "Set Excel formulas on cells", ToolCategory.ProjectFiles,
                new[] { "excel formula", "add formula", "sum formula" },
                ToolParam.Req("fileName", "string", "Excel file name"),
                ToolParam.Opt("sheetName", "string", "Sheet name"),
                ToolParam.Opt("cell", "string", "Cell address (e.g. A5)"),
                ToolParam.Opt("formula", "string", "Formula (e.g. =SUM(A1:A4))"));

            R("excel_get_info", "Get Excel workbook info: sheets, ranges, row/col counts", ToolCategory.ProjectFiles,
                new[] { "excel info", "workbook info", "spreadsheet info" },
                ToolParam.Req("fileName", "string", "Excel file name"));

            // =============== MISSING — Already in CommandExecutor ===============

            R("create_viewport", "Create a viewport on a sheet", ToolCategory.Views,
                new[] { "create viewport", "add viewport" },
                ToolParam.Req("sheetId", "integer", "Sheet ID"),
                ToolParam.Req("viewId", "integer", "View ID"),
                ToolParam.Opt("x", "number", "X position on sheet (ft)"),
                ToolParam.Opt("y", "number", "Y position on sheet (ft)"));

            R("create_room_finishes", "Create wall skirtings and floor finishes from room boundaries", ToolCategory.Creating,
                new[] { "room finishes", "create room finishes", "wall finishes", "floor finishes" },
                ToolParam.Opt("roomIds", "string", "Room IDs (comma-sep, or all)"),
                ToolParam.Opt("wallFinishType", "string", "Wall finish type"),
                ToolParam.Opt("floorFinishType", "string", "Floor finish type"));

            R("add_revision", "Add a revision to the project", ToolCategory.Views,
                new[] { "add revision", "new revision", "revision" },
                ToolParam.Opt("description", "string", "Revision description"),
                ToolParam.Opt("date", "string", "Revision date"),
                ToolParam.Opt("issuedTo", "string", "Issued to"),
                ToolParam.Opt("issuedBy", "string", "Issued by"));

            R("print_sheets", "Print sheets to printer or PDF", ToolCategory.Export,
                new[] { "print sheets", "print", "print to pdf" },
                ToolParam.Opt("sheetIds", "string", "Sheet IDs (comma-sep)"),
                ToolParam.Opt("printerName", "string", "Printer name"));

            R("send_code_to_revit", "Send C# code to Revit for execution via file bridge", ToolCategory.CodeExecution,
                new[] { "send code", "run csharp", "send code to revit" },
                ToolParam.Req("code", "string", "C# code to execute"));

            R("select_by_parameter", "Select elements by parameter value", ToolCategory.Editing,
                new[] { "select by parameter" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Req("parameterName", "string", "Parameter"),
                ToolParam.Req("value", "string", "Value to match"));

            R("bulk_parameter_transfer", "Transfer parameter values between elements", ToolCategory.Editing,
                new[] { "bulk transfer", "parameter transfer" },
                ToolParam.Req("sourceCategory", "string", "Source category"),
                ToolParam.Req("targetCategory", "string", "Target category"),
                ToolParam.Req("parameterName", "string", "Parameter name"));

            R("auto_renumber", "Auto-renumber elements by location in view", ToolCategory.Editing,
                new[] { "auto renumber", "auto number" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("parameterName", "string", "Parameter (default: Number)"),
                ToolParam.Opt("prefix", "string", "Prefix"),
                ToolParam.Opt("direction", "string", "left-right/top-bottom"));

            R("reset_view", "Reset view to default (remove overrides, crop, filters)", ToolCategory.Views,
                new[] { "reset view", "restore view" },
                ToolParam.Opt("viewId", "integer", "View ID (default: active)"));

            R("find_duplicates", "Find duplicate elements in the model", ToolCategory.QAQC,
                new[] { "find duplicates", "duplicates", "duplicate elements" },
                ToolParam.Opt("category", "string", "Category"),
                ToolParam.Opt("tolerance", "number", "Distance tolerance (ft)"));

            R("find_replace_names", "Find and replace in element/family/type names", ToolCategory.Editing,
                new[] { "find replace names" },
                ToolParam.Req("find", "string", "Text to find"),
                ToolParam.Req("replace", "string", "Text to replace with"),
                ToolParam.Opt("scope", "string", "families/types/views/sheets"));

            R("check_room_compliance", "Check rooms for compliance issues (area, name, number)", ToolCategory.QAQC,
                new[] { "room compliance", "check rooms" },
                ToolParam.Opt("levelName", "string", "Level filter"));

            R("check_naming_conventions", "Check if element names follow naming conventions", ToolCategory.QAQC,
                new[] { "naming conventions", "check naming" },
                ToolParam.Opt("category", "string", "Category"),
                ToolParam.Opt("pattern", "string", "Naming pattern to check"));

            R("check_links_status", "Check status of all linked models", ToolCategory.QAQC,
                new[] { "check links", "links status" });

            R("validate_parameters", "Validate required parameters are filled in", ToolCategory.QAQC,
                new[] { "validate parameters", "check parameters" },
                ToolParam.Req("category", "string", "Category"),
                ToolParam.Opt("parameterNames", "string", "Parameters to check (comma-sep)"));

            R("color_elements", "Apply solid color override to elements in active view", ToolCategory.Editing,
                new[] { "color elements", "highlight elements" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("colorR", "integer", "Red (0-255)"),
                ToolParam.Opt("colorG", "integer", "Green (0-255)"),
                ToolParam.Opt("colorB", "integer", "Blue (0-255)"));

            R("export_elements", "Export specific elements to file", ToolCategory.Export,
                new[] { "export elements" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("format", "string", "CSV/JSON"),
                ToolParam.Opt("folder", "string", "Output folder"));

            R("export_manager", "Open export manager — batch export to multiple formats", ToolCategory.Export,
                new[] { "export manager", "batch export" },
                ToolParam.Opt("format", "string", "DWG/PDF/IFC/NWC/DGN"),
                ToolParam.Opt("viewIds", "string", "View IDs (comma-sep)"));

            R("place_view_on_sheet", "Place a single view on a sheet", ToolCategory.Views,
                new[] { "place view on sheet" },
                ToolParam.Req("sheetId", "integer", "Sheet ID"),
                ToolParam.Req("viewId", "integer", "View ID"),
                ToolParam.Opt("x", "number", "X position"),
                ToolParam.Opt("y", "number", "Y position"));

            R("ai_element_filter", "AI-powered element filter — describe what to find in natural language", ToolCategory.Editing,
                new[] { "ai filter", "smart filter", "intelligent filter" },
                ToolParam.Req("description", "string", "Natural language description of elements to find"));

            // =============== NONICA.IO-INSPIRED NEW TOOLS ===============

            R("cut_floors", "Split/cut floors using model lines, curves, or room boundaries", ToolCategory.Creating,
                new[] { "cut floor", "split floor", "cut floors" },
                ToolParam.Opt("floorIds", "string", "Floor IDs (comma-sep, or all)"),
                ToolParam.Opt("method", "string", "lines/rooms/curves"));

            R("split_by_levels", "Split walls/columns at specified levels with gap control", ToolCategory.Editing,
                new[] { "split by levels", "split elements" },
                ToolParam.Req("category", "string", "Category (Walls/Columns)"),
                ToolParam.Opt("levelNames", "string", "Levels to split at (comma-sep)"),
                ToolParam.Opt("gap", "number", "Gap between splits (ft)"));

            R("create_openings", "Create openings in walls/floors for structural/MEP elements", ToolCategory.Creating,
                new[] { "create openings", "add openings", "structural openings" },
                ToolParam.Opt("hostCategory", "string", "Walls/Floors"),
                ToolParam.Opt("cutCategory", "string", "Ducts/Pipes/Structural Framing"),
                ToolParam.Opt("offset", "number", "Offset from edge (ft)"));

            R("manage_scope_boxes", "List, create, or delete scope boxes; find unused ones", ToolCategory.Views,
                new[] { "scope boxes", "manage scope boxes" },
                ToolParam.Opt("action", "string", "list/create/delete_unused"),
                ToolParam.Opt("name", "string", "Scope box name"),
                ToolParam.Opt("levelName", "string", "Level"));

            R("find_empty_sheets", "Find sheets with no viewports placed on them", ToolCategory.QAQC,
                new[] { "empty sheets", "find empty sheets", "unused sheets" },
                ToolParam.Opt("delete", "boolean", "Delete empty sheets"));

            R("clean_unused_templates", "Remove unused view templates, unplaced rooms, and unused filters", ToolCategory.QAQC,
                new[] { "clean templates", "unused templates", "clean unused" },
                ToolParam.Opt("scope", "string", "templates/rooms/filters/all"));

            R("clean_unplaced_views", "Delete views, schedules, and legends not placed on any sheet", ToolCategory.QAQC,
                new[] { "unplaced views", "clean unplaced", "orphan views" },
                ToolParam.Opt("dryRun", "boolean", "Preview only"));

            R("purge_unused_in_families", "Deep clean unused assets inside loaded families", ToolCategory.QAQC,
                new[] { "purge in families", "clean families" },
                ToolParam.Opt("category", "string", "Category filter"));

            R("delete_families_by_size", "Delete heavy families exceeding a file size threshold", ToolCategory.QAQC,
                new[] { "heavy families", "large families", "delete by size" },
                ToolParam.Opt("maxSizeKB", "integer", "Max family size in KB (default: 5000)"),
                ToolParam.Opt("dryRun", "boolean", "Preview only"));

            R("explode_3d_view", "Create exploded 3D view with level-based displacement", ToolCategory.Views,
                new[] { "explode view", "exploded view", "3d explode" },
                ToolParam.Opt("spacing", "number", "Spacing between levels (ft)"));

            R("rotate_section_box", "Rotate section/scope box to align with a selected face or element", ToolCategory.Views,
                new[] { "rotate section box", "align section box" },
                ToolParam.Opt("elementId", "integer", "Reference element ID"),
                ToolParam.Opt("angle", "number", "Rotation angle (degrees)"));

            R("super_align", "Advanced alignment and distribution of elements (by grid, spacing, etc.)", ToolCategory.Editing,
                new[] { "super align", "distribute elements", "smart align" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("mode", "string", "align/distribute/grid"),
                ToolParam.Opt("direction", "string", "horizontal/vertical"),
                ToolParam.Opt("spacing", "number", "Spacing (ft)"));

            R("join_elements_in_view", "Auto-join elements to eliminate unwanted lines in view", ToolCategory.QAQC,
                new[] { "join in view", "join elements in view" },
                ToolParam.Opt("category1", "string", "First category"),
                ToolParam.Opt("category2", "string", "Second category"),
                ToolParam.Opt("viewId", "integer", "View ID (default: active)"));

            R("copy_to_project", "Copy elements and families to another open project", ToolCategory.Data,
                new[] { "copy to project", "transfer to project" },
                ToolParam.Req("targetProject", "string", "Target project name"),
                ToolParam.Opt("category", "string", "Category to copy"),
                ToolParam.Opt("includeStyles", "boolean", "Include object styles"));

            R("measure_elements", "Measure distance, length, or area of selected elements", ToolCategory.Reading,
                new[] { "measure", "distance", "measure elements" },
                ToolParam.Opt("elementIds", "string", "Element IDs (comma-sep)"),
                ToolParam.Opt("type", "string", "distance/length/area"));

            // ===================== MEP (7 tools) =====================
            R("create_duct", "Create a duct between two points", ToolCategory.MEP,
                new[] { "create duct", "add duct", "new duct" },
                ToolParam.Req("startX", "number", "Start X (ft)"),
                ToolParam.Req("startY", "number", "Start Y (ft)"),
                ToolParam.Req("startZ", "number", "Start Z (ft)"),
                ToolParam.Req("endX", "number", "End X (ft)"),
                ToolParam.Req("endY", "number", "End Y (ft)"),
                ToolParam.Req("endZ", "number", "End Z (ft)"),
                ToolParam.Req("levelName", "string", "Level"),
                ToolParam.Opt("ductType", "string", "Duct type name"),
                ToolParam.Opt("systemType", "string", "System type (Supply/Return/Exhaust)"),
                ToolParam.Opt("width", "number", "Width (ft)"),
                ToolParam.Opt("height", "number", "Height (ft)"));

            R("create_pipe", "Create a pipe between two points", ToolCategory.MEP,
                new[] { "create pipe", "add pipe", "new pipe" },
                ToolParam.Req("startX", "number", "Start X (ft)"),
                ToolParam.Req("startY", "number", "Start Y (ft)"),
                ToolParam.Req("startZ", "number", "Start Z (ft)"),
                ToolParam.Req("endX", "number", "End X (ft)"),
                ToolParam.Req("endY", "number", "End Y (ft)"),
                ToolParam.Req("endZ", "number", "End Z (ft)"),
                ToolParam.Req("levelName", "string", "Level"),
                ToolParam.Opt("pipeType", "string", "Pipe type name"),
                ToolParam.Opt("systemType", "string", "System type"),
                ToolParam.Opt("diameter", "number", "Diameter (inches)"));

            R("create_flex_duct", "Create a flexible duct with multiple points", ToolCategory.MEP,
                new[] { "create flex duct", "flex duct" },
                ToolParam.Arr("points", "Path points [{x,y,z}] in ft", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" }, ["z"] = new JObject { ["type"] = "number" } } }),
                ToolParam.Req("levelName", "string", "Level"),
                ToolParam.Opt("ductType", "string", "Flex duct type"));

            R("create_mep_space", "Create an MEP space for load calculation/analysis", ToolCategory.MEP,
                new[] { "create space", "mep space", "add space" },
                ToolParam.Req("x", "number", "X position (ft)"),
                ToolParam.Req("y", "number", "Y position (ft)"),
                ToolParam.Req("levelName", "string", "Level"),
                ToolParam.Opt("spaceName", "string", "Space name"),
                ToolParam.Opt("spaceNumber", "string", "Space number"));

            R("get_mep_systems", "List all MEP systems (duct, pipe, electrical)", ToolCategory.MEP,
                new[] { "mep systems", "mechanical systems", "pipe systems", "duct systems" },
                ToolParam.Opt("systemType", "string", "Supply/Return/Exhaust/DomesticHotWater/DomesticColdWater"));

            R("duct_sizing", "Get or set duct/pipe sizes based on flow rate", ToolCategory.MEP,
                new[] { "duct sizing", "pipe sizing", "size ducts" },
                ToolParam.Opt("elementIds", "string", "Element IDs (comma-sep)"),
                ToolParam.Opt("category", "string", "Ducts/Pipes"));

            R("connect_mep_elements", "Connect two MEP elements at their nearest connectors", ToolCategory.MEP,
                new[] { "connect mep", "connect duct", "connect pipe" },
                ToolParam.Req("elementId1", "integer", "First element ID"),
                ToolParam.Req("elementId2", "integer", "Second element ID"));

            // ===================== STRUCTURAL (6 tools) =====================
            R("create_structural_beam", "Place a structural beam between two points", ToolCategory.Structural,
                new[] { "create beam", "add beam", "place beam", "structural beam" },
                ToolParam.Req("startX", "number", "Start X (ft)"),
                ToolParam.Req("startY", "number", "Start Y (ft)"),
                ToolParam.Req("endX", "number", "End X (ft)"),
                ToolParam.Req("endY", "number", "End Y (ft)"),
                ToolParam.Req("levelName", "string", "Level"),
                ToolParam.Opt("familyName", "string", "Family name"),
                ToolParam.Opt("typeName", "string", "Type name"));

            R("create_structural_column", "Place a structural column at a point", ToolCategory.Structural,
                new[] { "create column", "add column", "place column", "structural column" },
                ToolParam.Req("x", "number", "X position (ft)"),
                ToolParam.Req("y", "number", "Y position (ft)"),
                ToolParam.Req("baseLevelName", "string", "Base level"),
                ToolParam.Opt("topLevelName", "string", "Top level"),
                ToolParam.Opt("familyName", "string", "Family name"),
                ToolParam.Opt("typeName", "string", "Type name"));

            R("create_wall_foundation", "Create a continuous footing under a wall", ToolCategory.Structural,
                new[] { "wall foundation", "create footing", "continuous footing" },
                ToolParam.Req("wallId", "integer", "Wall ID"),
                ToolParam.Opt("typeName", "string", "Foundation type name"));

            R("create_rebar", "Place rebar in a structural host element", ToolCategory.Structural,
                new[] { "create rebar", "add rebar", "reinforcement" },
                ToolParam.Req("hostId", "integer", "Host element ID (wall/column/beam/floor)"),
                ToolParam.Opt("barType", "string", "Rebar bar type"),
                ToolParam.Opt("hookType", "string", "Hook type"),
                ToolParam.Opt("quantity", "integer", "Number of bars"),
                ToolParam.Opt("spacing", "number", "Bar spacing (ft)"));

            R("get_structural_elements", "List structural elements (beams, columns, bracing, foundations)", ToolCategory.Structural,
                new[] { "structural elements", "get beams", "get columns" },
                ToolParam.Opt("category", "string", "StructuralFraming/StructuralColumns/StructuralFoundation"),
                ToolParam.Opt("levelName", "string", "Level filter"));

            R("analytical_model_info", "Get analytical model data for structural elements", ToolCategory.Structural,
                new[] { "analytical model", "structural analysis" },
                ToolParam.Opt("elementIds", "string", "Element IDs (comma-sep)"));

            // ===================== ANNOTATION (7 tools) =====================
            R("create_filled_region", "Create a filled region in a view", ToolCategory.Annotation,
                new[] { "filled region", "create filled region", "hatch region" },
                ToolParam.Arr("points", "Boundary points [{x,y}] in ft", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" } } }),
                ToolParam.Opt("regionType", "string", "Filled region type name"),
                ToolParam.Opt("viewId", "integer", "View ID (default: active)"));

            R("create_spot_elevation", "Place a spot elevation in a view", ToolCategory.Annotation,
                new[] { "spot elevation", "create spot elevation" },
                ToolParam.Req("x", "number", "X position (ft)"),
                ToolParam.Req("y", "number", "Y position (ft)"),
                ToolParam.Opt("viewId", "integer", "View ID (default: active)"));

            R("create_spot_coordinate", "Place a spot coordinate in a view", ToolCategory.Annotation,
                new[] { "spot coordinate", "place coordinate" },
                ToolParam.Req("x", "number", "X position (ft)"),
                ToolParam.Req("y", "number", "Y position (ft)"),
                ToolParam.Opt("viewId", "integer", "View ID (default: active)"));

            R("create_keynote_legend", "Create a keynote legend schedule", ToolCategory.Annotation,
                new[] { "keynote legend", "create keynote" });

            R("create_detail_component", "Place a detail component in a drafting/detail view", ToolCategory.Annotation,
                new[] { "detail component", "place detail", "add detail component" },
                ToolParam.Req("familyName", "string", "Detail component family"),
                ToolParam.Req("typeName", "string", "Type name"),
                ToolParam.Req("x", "number", "X position (ft)"),
                ToolParam.Req("y", "number", "Y position (ft)"));

            R("tag_rooms_in_view", "Auto-tag all rooms in a view with room tags", ToolCategory.Annotation,
                new[] { "tag rooms", "room tags", "auto tag rooms" },
                ToolParam.Opt("viewId", "integer", "View ID (default: active)"),
                ToolParam.Opt("tagType", "string", "Room tag type"));

            R("dimension_walls", "Auto-dimension walls in a view (grid-to-grid or wall-to-wall)", ToolCategory.Annotation,
                new[] { "auto dimension", "dimension walls" },
                ToolParam.Opt("viewId", "integer", "View ID (default: active)"),
                ToolParam.Opt("mode", "string", "grid-to-grid/wall-to-wall/opening"));

            // ===================== ARCHITECTURE (7 tools) =====================
            R("create_stairs", "Create stairs between two levels", ToolCategory.Architecture,
                new[] { "create stairs", "add stairs", "new stairs" },
                ToolParam.Req("baseLevelName", "string", "Base level"),
                ToolParam.Req("topLevelName", "string", "Top level"),
                ToolParam.Opt("x", "number", "Location X (ft)"),
                ToolParam.Opt("y", "number", "Location Y (ft)"),
                ToolParam.Opt("width", "number", "Stair width (ft)"),
                ToolParam.Opt("stairType", "string", "Stair type name"));

            R("create_railing", "Create a railing along a path or on stairs", ToolCategory.Architecture,
                new[] { "create railing", "add railing" },
                ToolParam.Opt("stairsId", "integer", "Stairs ID (auto-place on stairs)"),
                ToolParam.Opt("railingType", "string", "Railing type name"),
                ToolParam.Arr("points", "Path points [{x,y}] in ft (if not on stairs)", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" } } }));

            R("create_curtain_wall", "Create a curtain wall", ToolCategory.Architecture,
                new[] { "curtain wall", "create curtain wall" },
                ToolParam.Req("startX", "number", "Start X (ft)"),
                ToolParam.Req("startY", "number", "Start Y (ft)"),
                ToolParam.Req("endX", "number", "End X (ft)"),
                ToolParam.Req("endY", "number", "End Y (ft)"),
                ToolParam.Req("levelName", "string", "Level"),
                ToolParam.Opt("height", "number", "Height (ft)"),
                ToolParam.Opt("typeName", "string", "Curtain wall type"));

            R("create_shaft_opening", "Create a shaft opening through floors/ceilings", ToolCategory.Architecture,
                new[] { "shaft opening", "create shaft" },
                ToolParam.Arr("points", "Boundary points [{x,y}] in ft", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" } } }),
                ToolParam.Req("baseLevelName", "string", "Base level"),
                ToolParam.Req("topLevelName", "string", "Top level"));

            R("get_stairs_info", "Get stairs info including runs, landings, and riser/tread dimensions", ToolCategory.Architecture,
                new[] { "stairs info", "get stairs" },
                ToolParam.Opt("stairsId", "integer", "Stairs element ID"));

            R("get_curtain_panels", "List curtain wall panels and mullions", ToolCategory.Architecture,
                new[] { "curtain panels", "get panels", "curtain wall info" },
                ToolParam.Req("wallId", "integer", "Curtain wall ID"));

            R("create_opening_in_wall", "Create a rectangular opening in a wall", ToolCategory.Architecture,
                new[] { "wall opening", "create opening in wall" },
                ToolParam.Req("wallId", "integer", "Wall ID"),
                ToolParam.Req("x1", "number", "Lower-left X (ft)"),
                ToolParam.Req("y1", "number", "Lower-left Y (ft)"),
                ToolParam.Req("x2", "number", "Upper-right X (ft)"),
                ToolParam.Req("y2", "number", "Upper-right Y (ft)"));

            // ===================== SITE (3 tools) =====================
            R("create_topography", "Create a topography surface from elevation points", ToolCategory.Site,
                new[] { "create topo", "topography", "terrain", "create topography" },
                ToolParam.Arr("points", "Elevation points [{x,y,z}] in ft", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" }, ["z"] = new JObject { ["type"] = "number" } } }));

            R("create_building_pad", "Create a building pad on topography at a level", ToolCategory.Site,
                new[] { "building pad", "create pad" },
                ToolParam.Arr("points", "Boundary points [{x,y}] in ft", new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" } } }),
                ToolParam.Req("levelName", "string", "Level"));

            R("get_site_info", "Get site topography, building pads, and property lines info", ToolCategory.Site,
                new[] { "site info", "get topo", "topography info" });

            // ===================== GENERAL UTILITIES (10 tools) =====================
            R("pin_elements", "Pin elements to prevent accidental moves", ToolCategory.Editing,
                new[] { "pin elements", "pin", "lock elements" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }));

            R("unpin_elements", "Unpin elements to allow modification", ToolCategory.Editing,
                new[] { "unpin elements", "unpin", "unlock elements" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }));

            R("create_workset", "Create a new workset in workshared project", ToolCategory.Data,
                new[] { "create workset", "new workset" },
                ToolParam.Req("name", "string", "Workset name"));

            R("get_element_history", "Get modification history/info for elements (who edited, when)", ToolCategory.Reading,
                new[] { "element history", "who edited", "last modified" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }));

            R("create_assembly", "Create an assembly from selected elements", ToolCategory.Creating,
                new[] { "create assembly", "assembly" },
                ToolParam.Arr("elementIds", "Element IDs", new JObject { ["type"] = "integer" }),
                ToolParam.Opt("assemblyName", "string", "Assembly name"));

            R("create_fill_pattern", "Create a new fill pattern (hatching)", ToolCategory.Views,
                new[] { "fill pattern", "create pattern", "hatch pattern" },
                ToolParam.Req("name", "string", "Pattern name"),
                ToolParam.Opt("patternType", "string", "Drafting/Model"),
                ToolParam.Opt("angle", "number", "Line angle (degrees)"),
                ToolParam.Opt("spacing", "number", "Line spacing (ft)"));

            R("get_element_geometry", "Get element geometry data (bounding box, volume, centroid)", ToolCategory.Reading,
                new[] { "element geometry", "geometry info", "bounding box" },
                ToolParam.Req("elementId", "integer", "Element ID"));

            R("compare_models", "Compare current model state with a saved snapshot", ToolCategory.QAQC,
                new[] { "compare models", "compare snapshot", "model diff" },
                ToolParam.Opt("snapshotKey", "string", "Saved snapshot key"));

            R("link_revit_model", "Link another Revit model into current project", ToolCategory.Data,
                new[] { "link model", "link revit" },
                ToolParam.Req("filePath", "string", "Path to .rvt file"),
                ToolParam.Opt("positionMode", "string", "Origin-to-Origin/Shared-Coordinates"));

            R("reload_links", "Reload all linked models to latest version", ToolCategory.Data,
                new[] { "reload links", "refresh links" },
                ToolParam.Opt("linkName", "string", "Specific link name (or all)"));

            // ===================== CODE EXECUTION (1 tool, always available) =====================
            R("execute_code", "Execute custom C# code against the Revit API when no built-in tool fits. Has access to __doc__ (Document), __uidoc__ (UIDocument), __uiapp__ (UIApplication). All Revit namespaces pre-imported. Runs inside a transaction. Return a value to send results back.", ToolCategory.CodeExecution,
                new[] { "execute code", "run code", "code" },
                true,
                ToolParam.Req("code", "string", "C# code body"),
                ToolParam.Opt("description", "string", "Brief description"));
        }

        // ── Helper to register a tool ──
        private static void R(string name, string desc, ToolCategory cat, string[] keywords, params ToolParam[] parms) =>
            _tools.Add(new ToolDefinition { Name = name, Description = desc, Category = cat, Keywords = keywords, Parameters = parms });

        private static void R(string name, string desc, ToolCategory cat, string[] keywords, bool alwaysAvailable, params ToolParam[] parms) =>
            _tools.Add(new ToolDefinition { Name = name, Description = desc, Category = cat, Keywords = keywords, Parameters = parms, AlwaysAvailable = alwaysAvailable });
    }
}
