import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withNavisConnection } from "../utils/navisConnectionManager.js";

/**
 * Navisworks federation + appearance tools — ADDITIVE-ONLY.
 * Covers the Revit→NWC→Navis loop without touching any Revit tool.
 */
export function registerNavisFederationTools(server: McpServer) {

    server.tool(
        "navis_append_file",
        "Append a model file (NWC/NWD/NWF/RVT/IFC) into the federated Navisworks document. Use after Revit export_to_nwc.",
        { filePath: z.string().describe("Full path to the file to append") },
        async (args) => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_append_file", { filePath: args.filePath }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_export_nwd",
        "Save/publish the federated Navisworks document as NWD (or NWF).",
        {
            filePath: z.string().optional().describe("Output path (default: overwrite current NWF with same-name NWD)"),
            includeProperties: z.boolean().optional().describe("Embed properties (default true)"),
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) =>
                    c.sendCommand("navis_export_nwd", { filePath: args.filePath || "", includeProperties: args.includeProperties ?? true }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_override_color",
        "Apply a temporary color/transparency override to ModelItems (by display ID).",
        {
            displayIds: z.array(z.string()).describe("ModelItem display IDs"),
            r: z.number().describe("Red 0-1"), g: z.number().describe("Green 0-1"), b: z.number().describe("Blue 0-1"),
            transparency: z.number().optional().describe("0 opaque – 1 transparent (default 0)"),
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) =>
                    c.sendCommand("navis_override_color", { displayIds: args.displayIds, r: args.r, g: args.g, b: args.b, transparency: args.transparency || 0 }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_hide_items",
        "Hide ModelItems (by display ID). Pass unhide=true to unhide.",
        {
            displayIds: z.array(z.string()).describe("ModelItem display IDs"),
            unhide: z.boolean().optional().describe("Unhide instead of hide"),
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) =>
                    c.sendCommand("navis_hide_items", { displayIds: args.displayIds, unhide: args.unhide || false }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );
}
