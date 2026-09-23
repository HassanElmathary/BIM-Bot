import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withNavisConnection } from "../utils/navisConnectionManager.js";

/**
 * Navisworks Reading + Viewpoint tools — ADDITIVE-ONLY.
 * All methods are `navis_*` and route to the Navisworks plugin (default port 8091).
 * No existing Revit tool is modified.
 */
export function registerNavisReadingTools(server: McpServer) {

    server.tool(
        "navis_get_model_info",
        "Get Navisworks Manage model info: file name, units, model count, saved viewpoint count. Requires Navisworks Manage with BIM-Bot service ON (port 8091).",
        {},
        async () => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_get_model_info", {}));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_search_items",
        "Search the Navisworks model tree for items by name/class/property (uses Navisworks Search API).",
        {
            searchText: z.string().describe("Text to search for, e.g. 'Level 1', 'Walls', 'Pipe'"),
            searchProperty: z.string().optional().describe("Property to match: Name (default), Class, Category, Material"),
            limit: z.number().optional().describe("Max items to return (default 100)"),
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) =>
                    c.sendCommand("navis_search_items", { searchText: args.searchText, searchProperty: args.searchProperty || "Name", limit: args.limit || 100 }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_get_selected",
        "Get currently selected ModelItems in Navisworks.",
        {},
        async () => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_get_selected", {}));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_select_items",
        "Select ModelItems in Navisworks by display IDs.",
        { displayIds: z.array(z.string()).describe("Navisworks ModelItem display IDs") },
        async (args) => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_select_items", { displayIds: args.displayIds }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_viewpoints_list",
        "List saved viewpoints in the Navisworks document.",
        {},
        async () => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_viewpoints_list", {}));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_viewpoint_apply",
        "Apply a saved viewpoint by name or GUID.",
        { viewpoint: z.string().describe("Viewpoint name or GUID") },
        async (args) => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_viewpoint_apply", { viewpoint: args.viewpoint }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_viewpoint_save",
        "Save the current Navisworks view as a named viewpoint.",
        { name: z.string().describe("New viewpoint name") },
        async (args) => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_viewpoint_save", { name: args.name }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_get_selection_sets",
        "List selection sets and search sets in the Navisworks document.",
        {},
        async () => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_get_selection_sets", {}));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_connection_status",
        "Check Navisworks connection status (handshake file, host, port). Does not require Navisworks to be running.",
        {},
        async () => {
            try {
                const { resolveNavisEndpoint } = await import("../utils/navisEndpoint.js");
                const ep = resolveNavisEndpoint();
                return { content: [{ type: "text", text: JSON.stringify({ target: "navisworks", ...ep }, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );
}
