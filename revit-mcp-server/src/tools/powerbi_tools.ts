/**
 * Power BI Tools — MCP tools for exporting the Revit 3D model to Power BI.
 *
 * Tools:
 *  1. export_to_powerbi — Export 3D model + parameters to a ready-to-open
 *     Power BI dashboard (.pbit with the BIM-Bot 3D Viewer embedded), or
 *     to SQLite (legacy format).
 */

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerPowerBITools(server: McpServer) {

    server.tool(
        "export_to_powerbi",
        "Export the Revit 3D model with tessellated geometry and all element parameters to Power BI. " +
        "Default format 'pbit' produces a self-contained dashboard: CSV data files plus a ready-to-open " +
        ".pbit template with the BIM-Bot 3D Viewer custom visual embedded and pre-wired — double-click it " +
        "and Power BI Desktop shows the interactive 3D model with slicers for Category, Level, Family, and " +
        "every parameter (cross-filtering works in both directions). " +
        "Format 'sqlite' writes the legacy SQLite database instead (requires an ODBC driver in Power BI). " +
        "Use exportScope='currentView' (default) to export only visible elements, or 'allModel' for everything.",
        {
            exportScope: z.enum(["currentView", "allModel"]).optional().describe(
                "Export scope: 'currentView' (default, exports visible elements in active 3D view) or 'allModel' (uses default 3D view, exports all)"
            ),
            format: z.enum(["pbit", "sqlite"]).optional().describe(
                "Output format: 'pbit' (default, CSV data + ready-to-open .pbit dashboard) or 'sqlite' (legacy .db)"
            ),
            outputFolder: z.string().optional().describe(
                "Output folder for the pbit format. Default: <ProjectName>_PowerBI next to the .rvt file"
            ),
            dbPath: z.string().optional().describe(
                "sqlite format only: full path for the .db file. Default: data/BIMBot_PowerBI.db (next to the .rvt file)"
            ),
            mode: z.enum(["new", "update"]).optional().describe(
                "sqlite format only: 'new' (default, drops and recreates tables) or 'update' (upserts changed elements)"
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
                        format: args.format || "pbit",
                        outputFolder: args.outputFolder,
                        dbPath: args.dbPath,
                        mode: args.mode || "new",
                        categories: args.categories,
                    })
                ) as {
                    message?: string;
                    format?: string;
                    pbitPath?: string;
                    dataFolder?: string;
                    dbPath?: string;
                    elementCount?: number;
                    geometryCount?: number;
                    parameterCount?: number;
                    chunkCount?: number;
                    totalVertices?: number;
                    totalTriangles?: number;
                    fileSize?: string;
                    categories?: string[];
                    mode?: string;
                    exportScope?: string;
                    exportView?: string;
                };

                const lines = [
                    response.message || "✅ Export complete",
                    "",
                    `📊 Elements: ${response.elementCount} | Geometry: ${response.geometryCount} | Parameters: ${response.parameterCount}`,
                    `🔺 Vertices: ${response.totalVertices?.toLocaleString()} | Triangles: ${response.totalTriangles?.toLocaleString()}`,
                    `📦 Data size: ${response.fileSize}`,
                    `🏷️ Categories: ${(response.categories || []).join(", ")}`,
                    `⚙️ Scope: ${response.exportScope} | View: ${response.exportView}`,
                ];

                if (response.format === "sqlite") {
                    lines.push(
                        `📁 Database: ${response.dbPath}`,
                        "",
                        "Next steps:",
                        "1. Open Power BI Desktop → Get Data → ODBC → connect to the .db file",
                        "2. Import tables: Elements, Geometry, CategoryColors",
                        "3. Import the BIM-Bot Viewer custom visual (.pbiviz)",
                        "4. Drag ElementId, ChunkIndex, Category, MeshJSON into the visual",
                    );
                } else {
                    lines.push(
                        `📁 Dashboard: ${response.pbitPath}`,
                        `📁 Data: ${response.dataFolder}`,
                        "",
                        "Done — double-click the .pbit file to open the interactive 3D dashboard in Power BI Desktop.",
                        "The 3D viewer, slicers (Category, Level, Family, Parameters), and KPI cards are already wired up.",
                    );
                }

                return {
                    content: [{ type: "text" as const, text: lines.join("\n") }],
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
