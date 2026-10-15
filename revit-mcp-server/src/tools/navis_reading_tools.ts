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
        "Search the Navisworks model for items whose property value contains searchText (case-insensitive). " +
        "Returns displayIds like 'p:0/3/12' that navis_select_items, navis_zoom_to_items, navis_hide_items, " +
        "navis_override_color, navis_get_item_properties and the other displayIds tools accept " +
        "(valid until models are removed or reloaded). Each search scans every item on the Navisworks UI thread: " +
        "on large federated models Navisworks freezes for several seconds, so reuse returned displayIds instead of " +
        "re-searching, and use withinSelection to search only under the current selection.",
        {
            searchText: z.string().describe("Text the property value must contain, e.g. 'Walls', 'Level 1', 'Pipe'"),
            searchProperty: z.string().optional().describe(
                "Name (Item>Name, default) | Category (Revit Element>Category, falls back to Item>Type) | " +
                "Class (Item>Type) | Material (Item>Material) | 'Tab>Property', e.g. 'Element>Mark'. " +
                "Use navis_get_item_properties on one item to see its tabs."),
            limit: z.number().optional().describe("Max items to return (default 100); 'count' reports all matches"),
            withinSelection: z.boolean().optional().describe("Search only the current Navisworks selection and its children (much faster on big models)"),
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_search_items", {
                    searchText: args.searchText,
                    searchProperty: args.searchProperty || "Name",
                    limit: args.limit || 100,
                    withinSelection: args.withinSelection ?? false,
                }));
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
        "Select ModelItems in Navisworks by displayIds ('p:...' values from navis_search_items or navis_get_selected).",
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
        "Diagnose the Navisworks connection: checks Navisworks is running, the BIM-Bot plugin is installed, the handshake file, the TCP port and a live ping. Returns a verdict and a fix hint. Safe to call when Navisworks is closed.",
        {},
        async () => {
            try {
                const { diagnoseNavis } = await import("../utils/navisDiagnostics.js");
                const diag = await diagnoseNavis();
                return { content: [{ type: "text", text: JSON.stringify({ target: "navisworks", ...diag }, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );
}
