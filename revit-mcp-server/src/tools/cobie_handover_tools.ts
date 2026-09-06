import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCobieHandoverTools(server: McpServer) {
    server.tool(
        "enrich_cross_link_cobie",
        "Enrich cross link COBie.",
        {
            mepCategory: z.string().optional().default("Mechanical Equipment").describe("MEP category"),
            sourceLinkName: z.string().optional().describe("Source link name"),
            targetHostRoomPhase: z.string().optional().describe("Target host room phase")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("enrich_cross_link_cobie", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
