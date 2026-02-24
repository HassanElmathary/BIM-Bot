import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Reading Tools — 14 tools for querying Revit model data
 */
export function registerReadingTools(server: McpServer) {

    // 1. Get current view info
    server.tool(
        "get_current_view_info",
        "Get detailed information about the currently active Revit view, including view type, name, scale, and properties.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_current_view_info", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 2. Get current view elements
    server.tool(
        "get_current_view_elements",
        "Get all elements visible in the current Revit view. Optionally filter by category.",
        { category: z.string().optional().describe("Optional category filter, e.g. 'Walls', 'Doors', 'Windows'") },
        async ({ category }) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_current_view_elements", { category: category || "" })
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 3. Get selected elements
    server.tool(
        "get_selected_elements",
        "Get details of all currently selected elements in Revit, including their IDs, categories, and parameters.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_selected_elements", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 4. Get elements by category
    server.tool(
        "get_elements",
        "Get all elements of a specific category from the Revit model. Use for querying walls, doors, windows, floors, etc.",
        {
            category: z.string().describe("Revit category name, e.g. 'Walls', 'Doors', 'Windows', 'Floors', 'Ceilings', 'Roofs'"),
            includeParameters: z.boolean().optional().describe("Include all parameters for each element (default: false)")
        },
        async ({ category, includeParameters }) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_elements", { category, includeParameters: includeParameters || false })
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 5. Get element parameters
    server.tool(
        "get_parameters",
        "Get all parameters (instance and type) for a specific element by its ID.",
        { elementId: z.number().describe("The Revit element ID") },
        async ({ elementId }) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_parameters", { elementId })
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 6. Get project info
    server.tool(
        "get_project_info",
        "Get general project information including project name, number, address, client, status, and other metadata.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_project_info", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 7. Get views
    server.tool(
        "get_views",
        "Get a list of all views in the Revit project. Optionally filter by view type.",
        {
            viewType: z.string().optional().describe("Optional filter: 'FloorPlan', 'CeilingPlan', 'Section', 'Elevation', '3D', 'Schedule', 'Drafting', 'Legend'")
        },
        async ({ viewType }) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_views", { viewType: viewType || "" })
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 8. Get sheets
    server.tool(
        "get_sheets",
        "Get all sheets in the Revit project, including sheet name, number, and placed views.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_sheets", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 9. Get levels
    server.tool(
        "get_levels",
        "Get all levels in the Revit project with their names and elevations.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_levels", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 10. Get grids
    server.tool(
        "get_grids",
        "Get all grids in the Revit project with their names, directions, and positions.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_grids", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 11. Get rooms
    server.tool(
        "get_rooms",
        "Get all rooms in the Revit project with their names, numbers, areas, and boundary information.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_rooms", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 12. Get families
    server.tool(
        "get_families",
        "Get available family types in the Revit project. Optionally filter by category.",
        {
            category: z.string().optional().describe("Optional category filter, e.g. 'Doors', 'Windows', 'Furniture'")
        },
        async ({ category }) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_available_family_types", { category: category || "" })
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 13. Get schedules
    server.tool(
        "get_schedules",
        "Get all schedules in the Revit project with their names, fields, and data.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_schedules", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 14. Get linked models
    server.tool(
        "get_linked_models",
        "Get information about all linked Revit models in the current project.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_linked_models", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 15. Get warnings
    server.tool(
        "get_warnings",
        "Get all active warnings/errors in the Revit model for quality control.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_warnings", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
        // 16. Open View
    server.tool(
        "open_view",
        "Open a specific view in the Revit UI so the user can see it.",
        {
            viewId: z.number().describe("The Element ID of the view to open")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("open_view", args)
                );
                return { content: [{ type: "text", text: `Opened view successfully: ${JSON.stringify(response)}` }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 17. Close View
    server.tool(
        "close_view",
        "Close a specific view in the Revit UI.",
        {
            viewId: z.number().optional().describe("The Element ID of the view to close. If not provided, it attempts to close the active view.")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("close_view", args)
                );
                return { content: [{ type: "text", text: `Closed view successfully: ${JSON.stringify(response)}` }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
