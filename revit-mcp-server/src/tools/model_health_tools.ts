import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerModelHealthTools(server: McpServer) {
    server.tool(
        "audit_federated_model_health",
        "Audit federated model health.",
        {
            scope: z.string().optional().default("all").describe("Scope: 'host_only', 'selected_links', 'all'"),
            linkNames: z.array(z.string()).optional().describe("Selected link names"),
            severityThreshold: z.string().optional().describe("Severity threshold: 'critical', 'moderate', 'all'")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("audit_federated_model_health", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "heal_model_issues",
        "Heal model issues like unplaced rooms or CAD imports.",
        {
            actions: z.array(z.string()).describe("Actions: 'pin_datums', 'purge_cad_imports', 'clean_unplaced_rooms'"),
            dryRun: z.boolean().optional().default(false).describe("Dry run mode")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("heal_model_issues", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "generate_remediation_report",
        "Generate a remediation report.",
        {
            linkName: z.string().describe("Link name"),
            format: z.string().optional().default("txt").describe("Format: 'txt', 'csv'")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("generate_remediation_report", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
