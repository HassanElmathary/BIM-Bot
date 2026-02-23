using System.Collections.Generic;

namespace RevitMCPPlugin.UI
{
    /// <summary>
    /// Defines a single parameter for a tool dialog.
    /// </summary>
    public class ToolParam
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public string Type { get; set; } // "text", "number", "bool", "dropdown"
        public bool Required { get; set; }
        public string Default { get; set; }
        public string[] Options { get; set; } // for dropdown type
        public string Hint { get; set; } // placeholder text

        public ToolParam(string name, string label, string type,
            bool required = false, string def = null, string[] options = null, string hint = null)
        {
            Name = name; Label = label; Type = type;
            Required = required; Default = def;
            Options = options; Hint = hint;
        }
    }

    /// <summary>
    /// Defines a tool with its display info and parameter list.
    /// </summary>
    public class ToolInfo
    {
        public string Name { get; set; }        // e.g. "export_to_pdf"
        public string DisplayName { get; set; } // e.g. "Export to PDF"
        public string Description { get; set; } // short description
        public string Icon { get; set; }        // emoji icon
        public string Category { get; set; }    // category name
        public List<ToolParam> Parameters { get; set; } = new List<ToolParam>();

        public ToolInfo(string name, string displayName, string desc, string icon, string category)
        {
            Name = name; DisplayName = displayName; Description = desc;
            Icon = icon; Category = category;
        }
    }

    /// <summary>
    /// Static catalog of all available tools with parameter definitions.
    /// </summary>
    public static class ToolCatalog
    {
        public static List<ToolInfo> GetAll()
        {
            return new List<ToolInfo>
            {
                // ===== EXPORT TOOLS =====
                new ToolInfo("export_manager", "Export Manager", "Unified batch export — PDF, DWG, DWF, DGN, IFC, NWC, Images", "📦", "Export")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("formats", "Export Formats", "text", hint: "PDF, DWG, DWF, DGN, IFC, NWC, IMG"),
                        new ToolParam("folder", "Output Folder", "text", hint: @"e.g. C:\Export"),
                        new ToolParam("sheetIds", "Sheet IDs", "text", hint: "Comma-separated IDs (empty = all sheets)"),
                        new ToolParam("viewIds", "View IDs", "text", hint: "Comma-separated view IDs")
                    }
                },
                new ToolInfo("export_schedule_data", "Export Schedule", "Export a Revit schedule to CSV file", "📊", "Export")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("scheduleId", "Schedule ID", "number", hint: "Element ID of the schedule"),
                        new ToolParam("scheduleName", "Schedule Name", "text", hint: "Or find by name"),
                        new ToolParam("folder", "Output Folder", "text", hint: @"e.g. C:\Export")
                    }
                },
                new ToolInfo("export_parameters_to_csv", "Export Params to CSV", "Export element parameters for bulk editing", "📋", "Export")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("category", "Category", "dropdown", required: true, def: "Walls",
                            options: new[] { "Walls", "Doors", "Windows", "Rooms", "Floors", "Columns", "Beams", "Pipes", "Ducts" }),
                        new ToolParam("parameterNames", "Parameter Names", "text", hint: "Comma-separated (empty = all)"),
                        new ToolParam("folder", "Output Folder", "text", hint: @"e.g. C:\Export"),
                        new ToolParam("levelName", "Filter by Level", "text", hint: "Level name (optional)")
                    }
                },
                new ToolInfo("import_parameters_from_csv", "Import Params from CSV", "Import & update parameters from CSV file", "📥", "Export")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("filePath", "CSV File Path", "text", required: true, hint: @"e.g. C:\data.csv"),
                        new ToolParam("dryRun", "Dry Run (preview only)", "bool", def: "false")
                    }
                },

                // ===== FAMILY & PARAMETER TOOLS =====
                new ToolInfo("manage_families", "Manage Families", "Rename, add prefix/suffix, or find & replace in family names", "📦", "Family & Parameters")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("action", "Action", "dropdown", required: true,
                            options: new[] { "rename", "add_prefix", "add_suffix", "find_replace" }),
                        new ToolParam("category", "Category Filter", "text", hint: "e.g. Doors, Windows"),
                        new ToolParam("find", "Find Text", "text", hint: "Text to search for"),
                        new ToolParam("replace", "Replace Text", "text", hint: "Replacement text"),
                        new ToolParam("prefix", "Prefix", "text", hint: "Prefix to add"),
                        new ToolParam("suffix", "Suffix", "text", hint: "Suffix to add")
                    }
                },
                new ToolInfo("get_family_info", "Family Info", "View loaded families: types, instance counts", "ℹ️", "Family & Parameters")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("category", "Category Filter", "text", hint: "e.g. Doors, Windows (optional)"),
                        new ToolParam("familyName", "Family Name", "text", hint: "Specific family (optional)")
                    }
                },
                new ToolInfo("create_project_parameter", "Create Parameter", "Create a new project parameter on categories", "➕", "Family & Parameters")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("name", "Parameter Name", "text", required: true, hint: "e.g. Room_Code"),
                        new ToolParam("categories", "Categories", "text", required: true, hint: "Comma-separated: Walls, Doors, Rooms"),
                        new ToolParam("type", "Parameter Type", "dropdown", def: "Text",
                            options: new[] { "Text", "Integer", "Number", "Length", "Area", "Volume", "YesNo" }),
                        new ToolParam("isInstance", "Instance Parameter", "bool", def: "true")
                    }
                },
                new ToolInfo("batch_set_parameter", "Batch Set Parameter", "Set a parameter value on all matching elements", "✏️", "Family & Parameters")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("category", "Category", "dropdown", required: true, def: "Walls",
                            options: new[] { "Walls", "Doors", "Windows", "Rooms", "Floors", "Columns", "Beams", "Pipes", "Ducts" }),
                        new ToolParam("parameterName", "Parameter Name", "text", required: true, hint: "Parameter to set"),
                        new ToolParam("value", "Value", "text", required: true, hint: "Value to apply"),
                        new ToolParam("filterParameterName", "Filter by Parameter", "text", hint: "Only where this param..."),
                        new ToolParam("filterValue", "Filter Value", "text", hint: "...equals this value"),
                        new ToolParam("levelName", "Filter by Level", "text", hint: "Level name (optional)")
                    }
                },
                new ToolInfo("delete_unused_families", "Delete Unused Families", "Find and remove families with zero placed instances", "🗑️", "Family & Parameters")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("category", "Category Filter", "text", hint: "e.g. Doors (optional)"),
                        new ToolParam("dryRun", "Dry Run (list only)", "bool", def: "true")
                    }
                },

                // ===== QUICKVIEWS =====
                new ToolInfo("create_elevation_views", "Elevation Views", "Auto-generate interior elevations for rooms", "🏠", "QuickViews")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("roomIds", "Room IDs", "text", hint: "Comma-separated (empty = all rooms)"),
                        new ToolParam("levelName", "Filter by Level", "text", hint: "Level name (optional)"),
                        new ToolParam("viewTemplate", "View Template", "text", hint: "Template name (optional)"),
                        new ToolParam("scale", "Scale", "number", def: "50", hint: "e.g. 50")
                    }
                },
                new ToolInfo("create_section_views", "Section Views", "Generate section views through rooms", "✂️", "QuickViews")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("roomIds", "Room IDs", "text", hint: "Comma-separated (optional)"),
                        new ToolParam("direction", "Direction", "dropdown", def: "horizontal", options: new[] { "horizontal", "vertical" }),
                        new ToolParam("viewTemplate", "View Template", "text", hint: "Template name (optional)"),
                        new ToolParam("scale", "Scale", "number", def: "50", hint: "e.g. 50")
                    }
                },
                new ToolInfo("create_callout_views", "Callout Views", "Generate callout views for rooms", "🔍", "QuickViews")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("roomIds", "Room IDs", "text", hint: "Comma-separated (optional)"),
                        new ToolParam("parentViewId", "Parent View ID", "number", hint: "Parent view element ID"),
                        new ToolParam("viewTemplate", "View Template", "text", hint: "Template name (optional)"),
                        new ToolParam("scale", "Scale", "number", def: "20", hint: "e.g. 20")
                    }
                },

                // ===== VIEW & SHEET MANAGEMENT =====
                new ToolInfo("align_viewports", "Align Viewports", "Align viewport positions across multiple sheets", "📏", "View & Sheet")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("referenceSheetId", "Reference Sheet ID", "number", required: true, hint: "Sheet to copy alignment from"),
                        new ToolParam("targetSheetIds", "Target Sheet IDs", "text", required: true, hint: "Comma-separated target sheet IDs")
                    }
                },
                new ToolInfo("batch_create_sheets", "Batch Create Sheets", "Create multiple sheets with auto-incrementing numbers", "📑", "View & Sheet")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("startNumber", "Start Number", "text", required: true, hint: "e.g. A101"),
                        new ToolParam("count", "Count", "number", required: true, def: "5", hint: "Number of sheets"),
                        new ToolParam("namePattern", "Name Pattern", "text", hint: "Use {n} for number, e.g. Floor Plan {n}"),
                        new ToolParam("titleBlockName", "Title Block", "text", hint: "Title block family name")
                    }
                },
                new ToolInfo("duplicate_view", "Duplicate View", "Duplicate views with various options", "📋", "View & Sheet")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("viewId", "View ID", "number", required: true, hint: "View element ID"),
                        new ToolParam("count", "Number of Copies", "number", def: "1"),
                        new ToolParam("duplicateType", "Duplicate Type", "dropdown", def: "with_detailing",
                            options: new[] { "independent", "as_dependent", "with_detailing" }),
                        new ToolParam("suffix", "Name Suffix", "text", def: " - Copy", hint: "Appended to view name")
                    }
                },
                new ToolInfo("apply_view_template", "Apply View Template", "Apply a view template to one or more views", "🎨", "View & Sheet")
                {
                    Parameters = new List<ToolParam>
                    {
                        new ToolParam("viewIds", "View IDs", "text", required: true, hint: "Comma-separated view IDs"),
                        new ToolParam("templateName", "Template Name", "text", required: true, hint: "View template name")
                    }
                }
            };
        }
    }
}
