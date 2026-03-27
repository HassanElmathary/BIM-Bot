/**
 * Power BI Tools — MCP tools for exporting Revit 3D model to Power BI via SQLite.
 *
 * Tools:
 *  1. export_to_powerbi — Export 3D model + geometry to SQLite for Power BI
 */

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerPowerBITools(server: McpServer) {

    server.tool(
        "export_to_powerbi",
        "Export Revit 3D model with tessellated geometry to a local SQLite database for Power BI visualization. " +
        "Creates Elements, Parameters, Geometry (MeshJSON), and CategoryColors tables. " +
        "Use exportScope='currentView' (default) to export only visible elements, or 'allModel' for everything. " +
        "The SQLite file can be loaded into Power BI Desktop via ODBC/SQLite connector and used with the RevitMCPViewer custom visual.",
        {
            exportScope: z.enum(["currentView", "allModel"]).optional().describe(
                "Export scope: 'currentView' (default, exports visible elements in active 3D view) or 'allModel' (uses default 3D view, exports all)"
            ),
            dbPath: z.string().optional().describe(
                "Full path for the SQLite .db file. Default: data/RevitMCP_PowerBI.db (next to the .rvt file)"
            ),
            mode: z.enum(["new", "update"]).optional().describe(
                "Export mode: 'new' (default, drops and recreates tables) or 'update' (upserts changed elements)"
            ),
            categories: z.string().optional().describe(
                "Comma-separated category filter, e.g. 'Walls,Floors,Doors'. Default: all 3D categories"
            ),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("export_to_powerbi", {
                        exportScope: args.exportScope || "currentView",
                        dbPath: args.dbPath,
                        mode: args.mode || "new",
                        categories: args.categories,
                    })
                ) as any;

                // Response comes from C# CommandExecutor with summary fields
                const text = [
                    response.message || "✅ Export complete",
                    `📁 Database: ${response.dbPath}`,
                    `📊 Elements: ${response.elementCount} | Geometry: ${response.geometryCount}`,
                    `🔺 Vertices: ${response.totalVertices?.toLocaleString()} | Triangles: ${response.totalTriangles?.toLocaleString()}`,
                    `📦 File size: ${response.fileSize}`,
                    `🏷️ Categories: ${(response.categories || []).join(", ")}`,
                    `⚙️ Mode: ${response.mode} | Scope: ${response.exportScope} | View: ${response.exportView}`,
                    "",
                    "Next steps:",
                    "1. Open Power BI Desktop → Get Data → ODBC → connect to the .db file",
                    "2. Import tables: Elements, Geometry, CategoryColors",
                    "3. Import the RevitMCPViewer custom visual (.pbiviz)",
                    "4. Drag ElementId, Category, MeshJSON into the visual",
                ].join("\n");

                return {
                    content: [{ type: "text" as const, text }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text" as const,
                        text: `Power BI export failed: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );
}
