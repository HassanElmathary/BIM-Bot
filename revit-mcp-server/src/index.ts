#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerTools } from "./tools/register.js";

const APP_VERSION = "2.0.0";

const server = new McpServer({
    name: "revit-mcp",
    version: APP_VERSION,
});

async function main() {
    await registerTools(server);
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error(`Revit MCP Server v${APP_VERSION} started successfully`);
}

// Graceful shutdown
process.on("SIGINT", () => {
    console.error("Revit MCP Server shutting down...");
    process.exit(0);
});

process.on("SIGTERM", () => {
    console.error("Revit MCP Server shutting down...");
    process.exit(0);
});

// Prevent crash on unhandled rejection (e.g. Revit connection lost mid-command)
process.on("unhandledRejection", (reason) => {
    console.error("Unhandled rejection:", reason);
    // Don't exit — the MCP server should stay alive
});

main().catch((error) => {
    console.error("Error starting Revit MCP Server:", error);
    process.exit(1);
});
