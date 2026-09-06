import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerClashBcfTools(server: McpServer) {
    server.tool(
        "run_smart_clash_triage",
        "Run smart clash triage.",
        {
            sourceCategory: z.string().describe("Source category"),
            targetCategory: z.string().describe("Target category"),
            sourceLink: z.string().optional().describe("Source link name or 'Host'"),
            targetLink: z.string().optional().describe("Target link name"),
            levelName: z.string().optional().describe("Level name filter"),
            toleranceMm: z.number().optional().default(25).describe("Tolerance in mm")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("run_smart_clash_triage", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "export_bcf_issues",
        "Export BCF issues.",
        {
            issues: z.array(z.object({
                title: z.string(),
                description: z.string(),
                elementIds: z.array(z.number())
            })).describe("List of issues to export"),
            outputFilePath: z.string().describe("Output file path"),
            bcfVersion: z.string().optional().default("2.1").describe("BCF version ('2.1' or '3.0')")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("export_bcf_issues", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
