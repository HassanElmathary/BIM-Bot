import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Undo / Transaction Control Tools + UI Automation + Safe Code Execution
 * Fixes Limitations #8 (No Undo), #9 (No UI Automation), #10 (Risky C#)
 */
export function registerTransactionTools(server: McpServer) {

    // ── Undo / Transaction Control (Limitation #8) ──────────────

    server.tool(
        "undo_last_operation",
        "Undo the last MCP operation. Uses Revit's TransactionGroup rollback to revert the most recent change.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("undo_last_operation", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_checkpoint",
        "Create a named checkpoint (savepoint). All subsequent operations can be rolled back to this point.",
        {
            name: z.string().describe("Checkpoint name for identification, e.g. 'before_mass_edit'"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_checkpoint", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "rollback_to_checkpoint",
        "Roll back all changes made since a named checkpoint. Reverts the model to the checkpoint state.",
        {
            name: z.string().describe("Checkpoint name to roll back to"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("rollback_to_checkpoint", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── UI Automation (Limitation #9) ───────────────────────────

    server.tool(
        "post_command",
        "Execute a Revit PostableCommand by name. This triggers ribbon commands programmatically (e.g., 'CloseInactiveViews', 'PurgeUnused', 'SwitchTo3DView').",
        {
            commandName: z.string().describe("PostableCommand name, e.g. 'CloseInactiveViews', 'PurgeUnused', 'SwitchJoinOrder', 'Undo', 'Redo'"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("post_command", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "list_commands",
        "List all available Revit PostableCommand names that can be used with post_command.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("list_commands", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ── Safe Code Execution (Limitation #10) ────────────────────

    server.tool(
        "preview_code",
        "Compile C# code WITHOUT executing it. Returns compilation success/errors to validate code before running. Use this to check code safety before send_code_to_revit.",
        {
            code: z.string().describe("C# code to validate (same format as send_code_to_revit)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("preview_code", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
