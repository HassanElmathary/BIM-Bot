import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import path from "path";
import os from "os";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Transfer Tools — move annotations between open Revit documents.
 *
 * Every other command in BIM-Bot is bound to the *active* document, which made
 * cross-document annotation transfer impossible without the user manually
 * switching focus in the Revit UI. These tools close that gap:
 *
 *   list_open_documents  — see every open document (title, path, active/linked)
 *   activate_document    — switch the active document via the API
 *   export_annotations   — serialize a view's annotations to a neutral JSON file
 *   import_annotations   — recreate annotations from such a file in any open doc
 *   transfer_annotations — one-step transfer between two open docs, no UI needed
 *
 * Fidelity notes (also surfaced in each tool description so agents set
 * expectations correctly):
 * - Text notes transfer at full fidelity (text, position, type).
 * - Tags are re-attached to the matching host element: first by UniqueId
 *   (works when the target derives from the source), then by category + level
 *   + nearest location within `matchTolerance`. Unmappable tags are reported,
 *   never silently dropped.
 * - Dimensions are recreated only when every referenced element resolves in
 *   the target document; otherwise they are exported as data with the reason.
 */
export function registerTransferTools(server: McpServer) {

    // 1. List open documents
    server.tool(
        "list_open_documents",
        "List all documents currently open in Revit: title, file path, whether it " +
        "is the active document, and whether it is linked. Use this before any " +
        "cross-document work to get exact document titles. Works even with no " +
        "active document.",
        {},
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("list_open_documents", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 2. Activate document
    server.tool(
        "activate_document",
        "Make one of the already-open Revit documents the active document, so " +
        "subsequent tools operate on it. This replaces the manual step of " +
        "clicking between Project A and Project B in the Revit UI.",
        {
            documentTitle: z.string().optional().describe(
                "Title of the open document as shown by list_open_documents (e.g. 'ProjectB.rvt')"
            ),
            filePath: z.string().optional().describe(
                "Full file path of the open document (alternative to documentTitle)"
            ),
        },
        async (args) => {
            try {
                if (!args.documentTitle && !args.filePath) {
                    return { content: [{ type: "text", text:
                        "Pass `documentTitle` (or `filePath`). Call list_open_documents first to get exact titles." }] };
                }
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("activate_document", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 3. Export annotations
    server.tool(
        "export_annotations",
        "Export annotations (text notes, tags, dimensions) from a view to a neutral " +
        "JSON transfer file that import_annotations can recreate in another " +
        "document. Tags carry their host element's identity for re-attachment; " +
        "dimensions carry their referenced elements.",
        {
            viewId: z.number().optional().describe("Source view element ID (default: active view)"),
            viewName: z.string().optional().describe("Source view name (alternative to viewId)"),
            documentTitle: z.string().optional().describe(
                "Source document title (default: active document)"
            ),
            includeTextNotes: z.boolean().optional().describe("Include text notes (default: true)"),
            includeTags: z.boolean().optional().describe("Include tags (default: true)"),
            includeDimensions: z.boolean().optional().describe("Include dimensions (default: true)"),
            filePath: z.string().optional().describe(
                "Output JSON path. Default: Desktop/annotations-<timestamp>.json"
            ),
        },
        async (args) => {
            try {
                const filePath = args.filePath ?? path.join(
                    os.homedir(), "Desktop", `annotations-${Date.now()}.json`
                );
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("export_annotations", { ...args, filePath })
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 4. Import annotations
    server.tool(
        "import_annotations",
        "Recreate annotations from an export_annotations JSON file in a target " +
        "view/document. Text notes are recreated exactly. Tags are re-attached " +
        "to the matching host element (UniqueId first, then category + level + " +
        "nearest location); hosts that cannot be matched are listed as skipped. " +
        "Dimensions are recreated only when all referenced elements resolve in " +
        "the target. Returns created / skipped / failed counts with reasons.",
        {
            filePath: z.string().describe("Path to the JSON transfer file from export_annotations"),
            targetDocumentTitle: z.string().optional().describe(
                "Target document title (default: active document)"
            ),
            targetViewId: z.number().optional().describe("Target view element ID"),
            targetViewName: z.string().optional().describe(
                "Target view name, e.g. 'Level 1' (alternative to targetViewId; required when the target document is not active)"
            ),
            matchTolerance: z.number().optional().describe(
                "Fallback host-matching tolerance in feet (default: 1.0)"
            ),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("import_annotations", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 5. Transfer annotations (one step)
    server.tool(
        "transfer_annotations",
        "Copy annotations directly between two open, unlinked Revit documents in " +
        "one step — no linking, no manual focus switching, no intermediate file. " +
        "Reads text notes, tags and dimensions from the source view and recreates " +
        "them in the target view. Same fidelity rules as import_annotations: text " +
        "notes exact, tags re-attached by host matching, dimensions best-effort. " +
        "The response reports exactly what transferred and what did not, with reasons.",
        {
            sourceDocumentTitle: z.string().optional().describe(
                "Source document title (default: active document)"
            ),
            sourceViewId: z.number().optional().describe("Source view element ID"),
            sourceViewName: z.string().optional().describe(
                "Source view name (required when the source document is not active)"
            ),
            targetDocumentTitle: z.string().optional().describe(
                "Target document title (default: the other open document when exactly two are open)"
            ),
            targetViewId: z.number().optional().describe("Target view element ID"),
            targetViewName: z.string().optional().describe(
                "Target view name, e.g. 'Level 1' (required when the target document is not active; " +
                "defaults to the view with the same name as the source view)"
            ),
            includeTextNotes: z.boolean().optional().describe("Include text notes (default: true)"),
            includeTags: z.boolean().optional().describe("Include tags (default: true)"),
            includeDimensions: z.boolean().optional().describe("Include dimensions (default: true)"),
            matchTolerance: z.number().optional().describe(
                "Fallback host-matching tolerance in feet (default: 1.0)"
            ),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("transfer_annotations", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
