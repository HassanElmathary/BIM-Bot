import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerFamilySanitizerTools(server: McpServer) {
    server.tool(
        "audit_and_clean_family",
        "Audit and clean family.",
        {
            familyFilePath: z.string().optional().describe("Family file path"),
            fix: z.boolean().optional().default(false).describe("Apply fixes flag"),
            maxPolygonCount: z.number().optional().default(10000).describe("Max polygon count")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("audit_and_clean_family", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
