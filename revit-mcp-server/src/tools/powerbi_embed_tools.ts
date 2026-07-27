/**
 * Power BI Embed Tools — MCP tools for viewing Power BI reports inside Revit.
 *
 * Tools:
 *  1. show_powerbi_report — Open an interactive Power BI report in a WebView2
 *     window inside Revit. Supports both authenticated Azure AD embedding and
 *     public "Publish to Web" URLs.
 */

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerPowerBIEmbedTools(server: McpServer) {

    server.tool(
        "show_powerbi_report",
        "Open an interactive Power BI report viewer inside Revit. " +
        "Two modes: (1) Paste a public 'Publish to Web' URL — no setup needed. " +
        "(2) Provide workspaceId + reportId for authenticated embedding via Azure AD " +
        "(requires POWERBI_CLIENT_ID in .env and user sign-in via Integrations Settings). " +
        "If no arguments are provided, opens the viewer with the user's saved settings.",
        {
            workspaceId: z.string().optional().describe(
                "Power BI workspace (group) ID for authenticated embedding"
            ),
            reportId: z.string().optional().describe(
                "Power BI report ID for authenticated embedding"
            ),
            publicUrl: z.string().optional().describe(
                "Public 'Publish to Web' embed URL from Power BI Service — no Azure AD setup needed"
            ),
        },
        async (args) => {
            try {
                if (!args.publicUrl && args.workspaceId && !args.reportId) {
                    return {
                        content: [{
                            type: "text" as const,
                            text: "When using authenticated mode, both workspaceId and reportId are required."
                        }]
                    };
                }

                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("show_powerbi_report", {
                        workspaceId: args.workspaceId,
                        reportId: args.reportId,
                        publicUrl: args.publicUrl,
                    })
                ) as {
                    message?: string;
                    mode?: string;
                    error?: string;
                };

                if (response.error) {
                    return {
                        content: [{
                            type: "text" as const,
                            text: `Power BI viewer error: ${response.error}`
                        }]
                    };
                }

                const lines = [
                    response.message || "✅ Power BI viewer opened",
                    "",
                    `Mode: ${response.mode === "publicUrl" ? "Public URL (no authentication)" : "Authenticated (Azure AD)"}`,
                ];

                if (!args.publicUrl && !args.workspaceId) {
                    lines.push(
                        "",
                        "The viewer opened with saved settings. To open a specific report:",
                        "• Public URL: show_powerbi_report(publicUrl='https://app.powerbi.com/view?r=...')",
                        "• Authenticated: show_powerbi_report(workspaceId='...', reportId='...')",
                    );
                }

                return {
                    content: [{ type: "text" as const, text: lines.join("\n") }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text" as const,
                        text: `Failed to open Power BI viewer: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );
}
