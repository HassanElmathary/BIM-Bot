import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import path from "path";
import os from "os";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { parseElementQuery, describeFilter, FilterCondition } from "../utils/nl-filter.js";
import { exportToExcel } from "../integrations/excel-client.js";
import { exportToCsv } from "../integrations/csv-client.js";

/** Response shape of the C# generate_dynamic_schedule command */
interface DynamicScheduleResponse {
    count: number;
    totalInCategory: number;
    category: string;
    columns: string[];
    rows: Record<string, unknown>[];
    matchedIds: number[];
}

/**
 * Documentation Tools — sheets, views, schedules, exports
 */
export function registerDocumentationTools(server: McpServer) {

    // 0. Dynamic schedule with natural-language filtering + Excel/CSV export
    server.tool(
        "generate_dynamic_schedule",
        "Generate a filtered quantity take-off from natural language and export it to Excel or CSV, " +
        "e.g. \"all doors wider than 900mm in Level 1\". Filters compare real parameter values in the " +
        "requested units (mm/cm/m/ft/in, areas, volumes), looking at both instance and type parameters. " +
        "For precise control pass structured `filters` instead of (or as well as) the query.",
        {
            query: z.string().optional().describe(
                "Natural language filter, e.g. 'doors wider than 900mm in Level 1' or 'rooms with area over 20 m2'"
            ),
            category: z.string().optional().describe(
                "Revit category (Doors, Windows, Walls, Rooms, …). Overrides the category detected in the query."
            ),
            filters: z.array(z.object({
                parameter: z.string().describe("Parameter name, e.g. 'Width', 'Fire Rating'"),
                operator: z.enum([">", ">=", "<", "<=", "=", "!=", "contains"]).describe("Comparison operator"),
                value: z.union([z.number(), z.string()]).describe("Value to compare against"),
                unit: z.string().optional().describe("Unit for numeric values: mm, cm, m, ft, in, m2, ft2, m3, ft3"),
            })).optional().describe(
                "Structured filter conditions. If provided, these are used instead of parsing the query."
            ),
            level: z.string().optional().describe("Only include elements on this level, e.g. 'Level 1'"),
            fields: z.array(z.string()).optional().describe(
                "Extra parameter columns to include, e.g. ['Fire Rating', 'Cost']. Id/Name/Type/Level and filtered parameters are always included."
            ),
            exportFormat: z.enum(["xlsx", "csv", "none"]).optional().describe(
                "Export format (default: xlsx). 'none' returns the data inline without writing a file."
            ),
            filePath: z.string().optional().describe(
                "Output file path. Default: Desktop/schedule-<category>-<timestamp>.<ext>"
            ),
            includeTotals: z.boolean().optional().describe(
                "Append a TOTAL row summing numeric columns (default: true)"
            ),
            limit: z.number().optional().describe("Maximum rows (default: unlimited)"),
        },
        async (args) => {
            try {
                const parsed = args.query ? parseElementQuery(args.query) : { conditions: [] as FilterCondition[] };
                const category = args.category ?? parsed.category;
                if (!category) {
                    return { content: [{ type: "text" as const, text:
                        "Could not determine a category. Pass `category` explicitly or mention one in the query (e.g. 'all doors …')." }] };
                }
                const conditions = args.filters && args.filters.length > 0 ? args.filters : parsed.conditions;
                const level = args.level ?? parsed.level;

                const response = (await withRevitConnection(async (client) =>
                    client.sendCommand("generate_dynamic_schedule", {
                        category,
                        conditions,
                        level,
                        fields: args.fields ?? [],
                        limit: args.limit ?? 0,
                    })
                )) as DynamicScheduleResponse;

                const filterDesc = describeFilter(category, conditions as FilterCondition[], level);
                const summary = `${response.count} of ${response.totalInCategory} ${category} matched: ${filterDesc}`;

                if (response.count === 0) {
                    return { content: [{ type: "text" as const, text:
                        `No matches — ${summary}.\nCheck parameter names/units, or pass structured \`filters\`.` }] };
                }

                const format = args.exportFormat ?? "xlsx";
                if (format === "none") {
                    const preview = response.rows.slice(0, 50);
                    return { content: [{ type: "text" as const, text:
                        `✅ ${summary}\n\n${JSON.stringify({ columns: response.columns, rows: preview, matchedIds: response.matchedIds }, null, 2)}` +
                        (response.rows.length > 50 ? `\n… ${response.rows.length - 50} more rows (use exportFormat xlsx/csv for the full set)` : "") }] };
                }

                const filePath = args.filePath ?? path.join(
                    os.homedir(), "Desktop",
                    `schedule-${category.toLowerCase().replace(/\s+/g, "-")}-${Date.now()}.${format}`
                );
                const includeTotals = args.includeTotals !== false;
                const outputPath = format === "csv"
                    ? exportToCsv(response.rows, { filePath, totalsRow: includeTotals })
                    : await exportToExcel(response.rows, { filePath, sheetName: category, totalsRow: includeTotals });

                return { content: [{ type: "text" as const, text:
                    `✅ ${summary}\n📄 Exported to: ${outputPath}\nColumns: ${response.columns.join(", ")}` +
                    `\n(matched element IDs available — use select_elements to highlight them in Revit)` }] };
            } catch (error) {
                return { content: [{ type: "text" as const, text:
                    `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 1. Place view on sheet
    server.tool(
        "place_view_on_sheet",
        "Place a view onto a sheet as a viewport.",
        {
            sheetId: z.number().describe("Sheet element ID"),
            viewId: z.number().describe("View element ID to place"),
            x: z.number().optional().describe("Viewport X position on the sheet (feet, default: center)"),
            y: z.number().optional().describe("Viewport Y position on the sheet (feet, default: center)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("place_view_on_sheet", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 2. Create viewport
    server.tool(
        "create_viewport",
        "Create a viewport on a sheet with specific settings.",
        {
            sheetNumber: z.string().describe("Sheet number to place on"),
            viewName: z.string().describe("View name to place"),
            x: z.number().optional().describe("X position on sheet"),
            y: z.number().optional().describe("Y position on sheet"),
            scale: z.number().optional().describe("Viewport scale override"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_viewport", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 3. Export schedules to CSV
    server.tool(
        "export_schedule",
        "Export a schedule's data as structured text (CSV format).",
        {
            scheduleId: z.number().optional().describe("Schedule element ID (if omitted, lists all schedules)"),
            scheduleName: z.string().optional().describe("Schedule name (alternative to ID)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("export_schedule", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 4. Create legend view
    server.tool(
        "create_legend",
        "Create a new legend view in the project.",
        {
            legendName: z.string().describe("Name for the legend view"),
            scale: z.number().optional().describe("Legend scale (default: 1:100)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_legend", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 5. Add revision
    server.tool(
        "add_revision",
        "Add a new revision to the project.",
        {
            date: z.string().describe("Revision date, e.g. '2025-02-20'"),
            description: z.string().describe("Revision description"),
            issuedBy: z.string().optional().describe("Issued by name"),
            issuedTo: z.string().optional().describe("Issued to name"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("add_revision", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 6. Print/export sheets
    server.tool(
        "print_sheets",
        "Print or export selected sheets to PDF.",
        {
            sheetNumbers: z.array(z.string()).optional().describe("Sheet numbers to print (default: all sheets)"),
            outputPath: z.string().optional().describe("Output folder path for PDF files"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("print_sheets", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 7. Export DWG
    server.tool(
        "export_dwg",
        "Export views or sheets to DWG format.",
        {
            viewIds: z.array(z.number()).optional().describe("View IDs to export"),
            sheetIds: z.array(z.number()).optional().describe("Sheet IDs to export"),
            outputPath: z.string().describe("Output directory path"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("export_dwg", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 8. Tag all elements in view
    server.tool(
        "tag_all_in_view",
        "Automatically tag all elements of a specific category in the current view.",
        {
            category: z.string().describe("Category to tag, e.g. 'Walls', 'Doors', 'Windows', 'Rooms'"),
            tagType: z.string().optional().describe("Tag family type name"),
            withLeader: z.boolean().optional().describe("Show leader lines (default: false)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("tag_all_in_view", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
