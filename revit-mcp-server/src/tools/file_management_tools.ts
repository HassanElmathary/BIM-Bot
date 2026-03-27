import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * File Management & Worksharing Tools
 * Fixes Limitation #1 (No Direct File Access) and #6 (No Worksharing / Sync to Central)
 */
export function registerFileManagementTools(server: McpServer) {

    // ── File Operations ─────────────────────────────────────────

    server.tool(
        "save_document",
        "Save the currently active Revit document. Equivalent to Ctrl+S.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("save_document", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "save_as_document",
        "Save the active document to a new file path (.rvt). Creates a copy without closing the current document.",
        {
            filePath: z.string().describe("Full path for the new file, e.g. 'C:\\\\Projects\\\\MyProject_backup.rvt'"),
            overwrite: z.boolean().optional().describe("Overwrite if file exists (default: false)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("save_as_document", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "close_document",
        "Close the active Revit document. Optionally save before closing.",
        {
            save: z.boolean().optional().describe("Save the document before closing (default: true)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("close_document", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Family Editor ───────────────────────────────────────────

    server.tool(
        "edit_family",
        "Open a family for editing. Takes an element ID of a family instance, opens the family document for editing.",
        {
            elementId: z.number().describe("Element ID of a family instance to edit its family"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("edit_family", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_family_extrusion",
        "Create a solid extrusion in the currently open family document. Defines a rectangular or polygonal profile and extrudes it.",
        {
            profilePoints: z.array(z.object({
                x: z.number().describe("X coordinate in feet"),
                y: z.number().describe("Y coordinate in feet"),
            })).describe("Array of 2D profile points defining the extrusion shape (closed loop)"),
            extrusionDepth: z.number().optional().describe("Extrusion depth in feet (default: 1.0)"),
            isSolid: z.boolean().optional().describe("True for solid, false for void (default: true)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_family_extrusion", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "save_family",
        "Save the currently open family document and optionally load it back into the project.",
        {
            loadIntoProject: z.boolean().optional().describe("Load the family back into the host project after saving (default: true)"),
            overwriteParameters: z.boolean().optional().describe("Overwrite parameter values in the project (default: false)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("save_family", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "load_family",
        "Load a .rfa family file from disk into the current Revit project.",
        {
            filePath: z.string().describe("Full path to the .rfa family file"),
            overwriteExisting: z.boolean().optional().describe("Overwrite if family already loaded (default: false)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("load_family", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Worksharing ─────────────────────────────────────────────

    server.tool(
        "sync_to_central",
        "Synchronize the local model with the central model. Equivalent to Revit's 'Synchronize with Central' command.",
        {
            comment: z.string().optional().describe("Sync comment for the log"),
            relinquishAll: z.boolean().optional().describe("Relinquish all borrowed elements after sync (default: true)"),
            saveLocalBefore: z.boolean().optional().describe("Save the local file before syncing (default: true)"),
            saveLocalAfter: z.boolean().optional().describe("Save the local file after syncing (default: true)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("sync_to_central", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "relinquish_all",
        "Relinquish all borrowed elements and worksets without syncing. Frees up elements for other users.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("relinquish_all", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "get_worksharing_info",
        "Get current worksharing status: central model path, local changes, borrowed elements, workset ownership, and last sync time.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_worksharing_info", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
