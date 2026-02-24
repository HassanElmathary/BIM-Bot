import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Power Tools — 17 advanced automation tools for Revit
 */
export function registerPowerTools(server: McpServer) {

    // ── Geometry ─────────────────────────────────────────────────

    server.tool(
        "auto_join_elements",
        "Automatically join geometry between two categories (e.g. Walls↔Floors, Walls↔Columns). Processes all intersecting elements.",
        {
            category1: z.string().optional().describe("First category (default: 'Walls')"),
            category2: z.string().optional().describe("Second category (default: 'Floors')"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("auto_join_elements", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "reassign_level",
        "Move elements to a different level while preserving their vertical position (offset adjustment).",
        {
            elementIds: z.array(z.number()).describe("Element IDs to reassign"),
            targetLevel: z.string().describe("Target level name"),
            maintainOffset: z.boolean().optional().describe("Maintain absolute position by adjusting offset (default: true)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("reassign_level", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "batch_modify_thickness",
        "Modify wall or slab thickness by scaling compound structure layers proportionally.",
        {
            category: z.string().optional().describe("Category: 'Walls' or 'Floors' (default: 'Walls')"),
            typeName: z.string().describe("Type name to modify"),
            thickness: z.number().describe("New total thickness in feet"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("batch_modify_thickness", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "room_to_floor",
        "Generate floor elements from room boundaries. Creates one floor per room using the room's boundary curves.",
        {
            roomIds: z.array(z.number()).optional().describe("Specific room IDs (default: all rooms with area > 0)"),
            floorType: z.string().optional().describe("Floor type name (default: first available)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("room_to_floor", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Data & Parameters ────────────────────────────────────────

    server.tool(
        "find_replace_names",
        "Global find-and-replace in element type names, view names, or sheet names.",
        {
            find: z.string().describe("Text to find"),
            replace: z.string().optional().describe("Replacement text (default: empty/remove)"),
            scope: z.enum(["Types", "Views", "Sheets", "All"]).optional().describe("Where to search (default: 'Types')"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("find_replace_names", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "parameter_case_convert",
        "Convert a text parameter's value to UPPER, lower, or Title case for all elements in a category.",
        {
            category: z.string().describe("Category name"),
            parameterName: z.string().describe("Parameter name to convert"),
            caseType: z.enum(["UPPER", "lower", "Title"]).optional().describe("Target case (default: 'Title')"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("parameter_case_convert", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "bulk_parameter_transfer",
        "Copy values from one parameter to another for all elements in a category (e.g. sync Comments to Mark).",
        {
            category: z.string().describe("Category name"),
            sourceParameter: z.string().describe("Source parameter name to read from"),
            targetParameter: z.string().describe("Target parameter name to write to"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("bulk_parameter_transfer", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "auto_renumber",
        "Sequentially renumber elements (rooms, doors, etc.) sorted by location or name.",
        {
            category: z.string().optional().describe("Category (default: 'Rooms')"),
            prefix: z.string().optional().describe("Number prefix, e.g. 'A-' for 'A-101'"),
            startNumber: z.number().optional().describe("Starting number (default: 1)"),
            sortBy: z.enum(["Location", "Name"]).optional().describe("Sort order (default: 'Location')"),
            parameterName: z.string().optional().describe("Parameter to set (default: 'Number')"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("auto_renumber", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Views & Documentation ────────────────────────────────────

    server.tool(
        "batch_create_sheets",
        "Create multiple sheets at once from a list of sheet numbers and names.",
        {
            sheets: z.array(z.object({
                number: z.string().describe("Sheet number, e.g. 'A101'"),
                name: z.string().optional().describe("Sheet name"),
            })).describe("Array of sheets to create"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("batch_create_sheets", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "align_viewports",
        "Align all viewports on a sheet horizontally, vertically, or to the same center.",
        {
            sheetId: z.number().describe("Sheet element ID"),
            alignment: z.enum(["Horizontal", "Vertical", "Center"]).optional().describe("Alignment type (default: 'Center')"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("align_viewports", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Project Cleanup ──────────────────────────────────────────

    server.tool(
        "deep_purge",
        "Multi-pass deep purge of all unused elements (families, types, line styles, view filters, etc.).",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("deep_purge", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "delete_empty_groups",
        "Find and delete all empty groups and unused group types in the project.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("delete_empty_groups", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "find_cad_imports",
        "Locate all CAD imports in the project. Optionally delete non-linked imports.",
        {
            delete: z.boolean().optional().describe("Set to true to delete non-linked CAD imports"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("find_cad_imports", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Selection & Filtering ────────────────────────────────────

    server.tool(
        "select_by_parameter",
        "Select all elements where a specific parameter matches a value.",
        {
            category: z.string().describe("Category to search in"),
            parameterName: z.string().describe("Parameter name to filter by"),
            value: z.string().optional().describe("Value to match (partial match supported). Omit to select all elements that have this parameter."),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("select_by_parameter", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "select_by_workset",
        "Select all elements on a specific workset.",
        {
            worksetName: z.string().describe("Workset name to select from"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("select_by_workset", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "filter_selection",
        "Filter the current selection to keep only elements matching a category and/or level.",
        {
            category: z.string().optional().describe("Keep only elements of this category"),
            levelName: z.string().optional().describe("Keep only elements on this level"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("filter_selection", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "category_to_workset",
        "Automatically migrate all elements of specified categories to designated worksets.",
        {
            mappings: z.array(z.object({
                category: z.string().describe("Category name"),
                worksetName: z.string().describe("Target workset name"),
            })).describe("Category-to-workset mappings"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("category_to_workset", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Advanced Tools ───────────────────────────────────────────

    server.tool(
        "inverse_selection",
        "Invert the current selection — deselect what's selected and select everything else visible in the view.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("inverse_selection", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "copy_from_linked",
        "Copy elements from a linked Revit model into the current document.",
        {
            category: z.string().describe("Category of elements to copy (e.g. 'Walls', 'Doors')"),
            linkName: z.string().optional().describe("Name of the linked model (partial match). Default: first link."),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("copy_from_linked", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "crop_region_sync",
        "Copy the crop region from one master view to multiple target views.",
        {
            sourceViewId: z.number().describe("Source view ID to copy crop from"),
            targetViewIds: z.array(z.number()).describe("Target view IDs to apply the crop to"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("crop_region_sync", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "apply_view_template",
        "Apply a view template to one or more views. Lists available templates if the specified one is not found.",
        {
            templateName: z.string().describe("Name of the view template to apply"),
            viewIds: z.array(z.number()).optional().describe("View IDs to apply to (default: active view)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("apply_view_template", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "resolve_warnings",
        "List or auto-resolve Revit warnings. Can fix duplicate marks, unenclosed rooms, and overlapping room separations.",
        {
            action: z.enum(["list", "resolve"]).optional().describe("'list' to view warnings, 'resolve' to auto-fix (default: 'list')"),
            warningType: z.string().optional().describe("Filter to only resolve warnings containing this text"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("resolve_warnings", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "wall_floor_sync",
        "Auto-join all walls and floors that intersect, ensuring proper boundary connections.",
        {
            levelName: z.string().optional().describe("Limit to a specific level (default: all levels)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("wall_floor_sync", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Final Four Tools ─────────────────────────────────────────

    server.tool(
        "snap_beams_to_columns",
        "Snap beam endpoints to the nearest column centerlines. Fixes misaligned structural framing.",
        {
            tolerance: z.number().optional().describe("Max snap distance in feet (default: 2.0)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("snap_beams_to_columns", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "convert_category",
        "Convert elements to a different family/category. Deletes old elements and places new ones at the same location.",
        {
            elementIds: z.array(z.number()).describe("Element IDs to convert"),
            targetFamily: z.string().describe("Target family name"),
            targetType: z.string().optional().describe("Target type name (default: first type in family)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("convert_category", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "add_shared_parameter",
        "Inject a shared parameter into a Revit category. Creates the shared parameter file automatically if needed.",
        {
            parameterName: z.string().describe("Name of the parameter to add"),
            category: z.string().describe("Category to add parameter to"),
            groupName: z.string().optional().describe("Parameter group name (default: 'Data')"),
            paramType: z.enum(["Text", "Number", "Integer", "Length", "Area", "Volume", "Angle", "YesNo"]).optional().describe("Parameter type (default: 'Text')"),
            isInstance: z.boolean().optional().describe("Instance parameter (true) or Type parameter (false). Default: true"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("add_shared_parameter", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "import_data_from_csv",
        "Import data from a CSV file into Revit element parameters. CSV header row = parameter names. Matches elements by a key parameter (e.g. room Number).",
        {
            filePath: z.string().describe("Absolute path to the CSV file"),
            category: z.string().optional().describe("Category to match elements in"),
            keyParameter: z.string().optional().describe("Parameter to match CSV rows to elements (default: 'Number'). Use 'Id' for element IDs."),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("import_data_from_csv", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Last Two Missing Tools ───────────────────────────────────

    server.tool(
        "generate_legend",
        "Generate a door or window legend — creates a drafting view with a table listing all unique types, dimensions, and counts.",
        {
            category: z.enum(["Doors", "Windows"]).optional().describe("Category to generate legend for (default: 'Doors')"),
            legendName: z.string().optional().describe("Name for the legend view"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("generate_legend", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "cad_to_lines",
        "Convert CAD imports to native Revit detail lines, arcs, and polylines. Optionally delete the CAD import after conversion.",
        {
            importIds: z.array(z.number()).optional().describe("Specific CAD import IDs to convert (default: all non-linked imports)"),
            deleteAfter: z.boolean().optional().describe("Delete the CAD import after conversion (default: false)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("cad_to_lines", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
