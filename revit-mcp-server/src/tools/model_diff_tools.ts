import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerModelDiffTools(server: McpServer) {
    server.tool(
        "diff_linked_milestones",
        "Diff linked milestones.",
        {
            linkInstanceName: z.string().describe("Link instance name"),
            baselineSnapshotPath: z.string().optional().describe("Baseline snapshot path"),
            saveSnapshot: z.boolean().optional().default(false).describe("Save snapshot flag")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("diff_linked_milestones", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
