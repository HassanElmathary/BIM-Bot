import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Extended Tools — Filling remaining capability gaps
 * Covers: file operations, family creation, link management, view zoom, schedules,
 * and exposes C# tools that existed but had no MCP definitions.
 */
export function registerExtendedTools(server: McpServer) {

    // ── File Operations (Remaining) ─────────────────────────────

    server.tool(
        "open_document",
        "Open a .rvt Revit file. Optionally detach from central.",
        {
            filePath: z.string().describe("Full path to the .rvt file"),
            detach: z.boolean().optional().describe("Detach from central when opening (default: false)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("open_document", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_new_project",
        "Create a new Revit project. Optionally specify a template file.",
        {
            templatePath: z.string().optional().describe("Path to .rte template file. If omitted, uses default metric template."),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_new_project", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_new_family",
        "Create a new family (.rfa) from a template. Opens the family editor.",
        {
            templatePath: z.string().optional().describe("Path to .rft family template file. If omitted, uses default Generic Model template."),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_new_family", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "detach_from_central",
        "Open a workshared .rvt file detached from its central model.",
        {
            filePath: z.string().describe("Full path to the workshared .rvt file"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("detach_from_central", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Linked Files (Remaining) ────────────────────────────────

    server.tool(
        "change_link_path",
        "Change the file path of a linked Revit model. Useful when links are broken or moved.",
        {
            linkName: z.string().describe("Name of the linked model (partial match)"),
            newPath: z.string().describe("New file path for the linked model"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("change_link_path", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "manage_link_position",
        "Move or rotate a linked model instance in the project.",
        {
            linkName: z.string().optional().describe("Name of the link instance (partial match)"),
            moveX: z.number().optional().describe("Move X distance in feet"),
            moveY: z.number().optional().describe("Move Y distance in feet"),
            moveZ: z.number().optional().describe("Move Z distance in feet"),
            rotation: z.number().optional().describe("Rotation angle in degrees around Z axis"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("manage_link_position", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── View Navigation ─────────────────────────────────────────

    server.tool(
        "zoom_to_fit",
        "Zoom the active view to fit all visible content. Equivalent to pressing 'ZF' in Revit.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("zoom_to_fit", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "zoom_to_element",
        "Zoom the active view to center on a specific element with padding.",
        {
            elementId: z.number().describe("Element ID to zoom to"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("zoom_to_element", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Schedule Editing ────────────────────────────────────────

    server.tool(
        "edit_schedule",
        "Edit a schedule's structure: add/remove fields, sort, toggle itemization, show/hide headers. Use action='info' to get field details first.",
        {
            scheduleId: z.number().describe("Element ID of the schedule"),
            action: z.enum(["info", "sort", "add_field", "remove_field", "set_header", "itemize"]).describe("Action to perform"),
            fieldName: z.string().optional().describe("Field name (for sort, add_field, remove_field)"),
            ascending: z.boolean().optional().describe("Sort ascending (for sort action, default: true)"),
            showHeaders: z.boolean().optional().describe("Show column headers (for set_header action)"),
            itemize: z.boolean().optional().describe("Itemize every instance (for itemize action)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("edit_schedule", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Exposing Hidden C# Tools ────────────────────────────────

    server.tool(
        "create_stairs",
        "Create stairs between two levels by defining location, width, and stair configuration.",
        {
            baseLevelName: z.string().describe("Base level name"),
            topLevelName: z.string().describe("Top level name"),
            x: z.number().optional().describe("X location in feet"),
            y: z.number().optional().describe("Y location in feet"),
            width: z.number().optional().describe("Stair width in feet (default: 3)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_stairs", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_railing",
        "Create a railing along a path or attached to stairs.",
        {
            stairId: z.number().optional().describe("Element ID of stairs to attach railing to"),
            railingType: z.string().optional().describe("Railing type name"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_railing", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_curtain_wall",
        "Create a curtain wall with configurable grid pattern.",
        {
            startX: z.number().describe("Start X in feet"),
            startY: z.number().describe("Start Y in feet"),
            endX: z.number().describe("End X in feet"),
            endY: z.number().describe("End Y in feet"),
            levelName: z.string().optional().describe("Level name"),
            height: z.number().optional().describe("Wall height in feet"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_curtain_wall", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_filled_region",
        "Create a filled region in a view from boundary points.",
        {
            points: z.array(z.object({ x: z.number(), y: z.number() })).describe("Boundary points forming a closed loop"),
            fillPatternName: z.string().optional().describe("Fill pattern name"),
            viewId: z.number().optional().describe("View ID (default: active view)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_filled_region", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_elevation_views",
        "Create elevation views facing a room or area.",
        {
            roomId: z.number().optional().describe("Room ID to create elevations around"),
            x: z.number().optional().describe("X location for elevation marker"),
            y: z.number().optional().describe("Y location for elevation marker"),
            levelName: z.string().optional().describe("Level name"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_elevation_views", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_section_views",
        "Create section views through elements or at specified locations.",
        {
            elementId: z.number().optional().describe("Element ID to section through"),
            startX: z.number().optional(),
            startY: z.number().optional(),
            endX: z.number().optional(),
            endY: z.number().optional(),
            depth: z.number().optional().describe("Section view depth in feet"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_section_views", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_spot_elevation",
        "Create a spot elevation annotation in the current view.",
        {
            x: z.number().describe("X coordinate in feet"),
            y: z.number().describe("Y coordinate in feet"),
            z: z.number().optional().describe("Z coordinate (default: pick from model)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_spot_elevation", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
