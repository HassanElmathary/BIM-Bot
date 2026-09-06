import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerIdsValidationTools(server: McpServer) {
    server.tool(
        "validate_ids_spec",
        "Validate IDS spec against model.",
        {
            idsXmlContent: z.string().optional().describe("IDS XML Content"),
            idsFilePath: z.string().optional().describe("IDS file path"),
            scope: z.string().optional().describe("Scope: 'host_only', 'selected_links', 'all'"),
            discipline: z.string().optional().describe("Discipline to validate")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("validate_ids_spec", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "audit_iso19650_naming",
        "Audit ISO19650 naming convention.",
        {
            targetType: z.string().describe("Target type: 'sheets'|'views'|'worksets'|'families'|'levels'|'links'"),
            pattern: z.string().optional().describe("Optional override regex pattern"),
            includeLinks: z.boolean().optional().describe("Include links in audit")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("audit_iso19650_naming", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
