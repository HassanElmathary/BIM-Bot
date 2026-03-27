/**
 * Export Tools — MCP tools for exporting Revit data to external platforms.
 *
 * Tools:
 *  1.  export_to_excel              — Write elements to .xlsx file
 *  2.  export_to_notion             — Sync elements to a Notion database
 *  3.  export_to_google_sheets      — Push elements to Google Sheets
 *  4.  export_to_sqlite             — Save a local snapshot to SQLite
 *  5.  get_sqlite_snapshots         — List saved snapshots
 *  6.  ai_map_fields                — Use local AI (Ollama) to map fields
 *  7.  ollama_chat                  — Chat with local AI as BIM Data Architect
 *  8.  export_revit_data            — Lightweight metadata-only export via C# ExportElements
 *  9.  read_notion_database         — Read-back from a Notion database
 *  10. read_google_sheets           — Read-back from Google Sheets
 *  11. compare_snapshots            — Delta-sync between SQLite snapshots
 *  12. ai_analyze_data              — Ollama-powered BIM data analysis
 *  13. google_signin                — Sign in with Google (OAuth 2.0)
 *  14. list_google_spreadsheets     — List user's Google Sheets files
 */

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { exportToExcel } from "../integrations/excel-client.js";
import { syncToNotion } from "../integrations/notion-client.js";
import { updateSheets, readSheets, listSpreadsheets, isOAuthConnected, resetSheetsClient, getAuthMode } from "../integrations/sheets-client.js";
import { queryNotionDatabase } from "../integrations/notion-client.js";
import { saveSnapshot, getSnapshots, getSnapshotData } from "../integrations/sqlite-client.js";
import { getOllamaService } from "../ai/ollama-service.js";
import { GoogleAuth } from "../auth/google-oauth.js";

/**
 * Fetch elements from Revit by category via the existing socket connection.
 * Returns an array of element objects.
 */
async function fetchRevitElements(
    category: string,
    includeParameters: boolean = true
): Promise<Record<string, unknown>[]> {
    const response = (await withRevitConnection(async (client) =>
        client.sendCommand("get_elements", {
            category,
            includeParameters,
            offset: 0,
            limit: 0, // All elements
        })
    )) as any;

    // The response may have an "elements" array or be an array directly
    if (Array.isArray(response)) return response;
    if (response?.elements && Array.isArray(response.elements)) return response.elements;
    if (response && typeof response === "object") return [response];
    return [];
}

/**
 * Fetch elements in batches using Revit's pagination (stream buffer).
 * Automatically iterates with offset/limit until all data is fetched.
 * Uses the C# ExportElements command for lightweight metadata-only export.
 * Default batch: 100 elements to prevent Revit UI thread locking.
 */
async function fetchRevitElementsBatched(
    category: string,
    batchSize: number = 100,
    modifiedAfter?: string
): Promise<Record<string, unknown>[]> {
    const allElements: Record<string, unknown>[] = [];
    let offset = 0;
    let hasMore = true;

    while (hasMore) {
        const params: Record<string, unknown> = {
            category,
            offset,
            limit: batchSize,
        };
        if (modifiedAfter) params.modifiedAfter = modifiedAfter;

        const response = (await withRevitConnection(async (client) =>
            client.sendCommand("export_elements", params)
        )) as any;

        const elements = response?.elements;
        if (Array.isArray(elements) && elements.length > 0) {
            allElements.push(...elements);
        }

        hasMore = response?.hasMore === true;
        offset += batchSize;

        // Safety: prevent infinite loop
        if (offset > 100000) break;
    }

    return allElements;
}

export function registerExportTools(server: McpServer) {

    // ─────────────────────────────────────────────
    // 1. Export to Excel
    // ─────────────────────────────────────────────
    server.tool(
        "export_to_excel",
        "Export Revit elements of a specific category to an Excel (.xlsx) file on your local machine. " +
        "Creates a formatted spreadsheet with headers, styling, and auto-width columns.",
        {
            category: z.string().describe(
                "Revit category to export, e.g. 'Walls', 'Doors', 'Windows', 'Rooms', 'Floors'"
            ),
            filePath: z.string().optional().describe(
                "Full file path for the Excel file. Default: Desktop/revit-export-<timestamp>.xlsx"
            ),
            sheetName: z.string().optional().describe(
                "Name of the worksheet (default: 'Revit Data')"
            ),
            includeParameters: z.boolean().optional().describe(
                "Include all element parameters (default: true)"
            ),
        },
        async (args) => {
            try {
                const elements = await fetchRevitElements(
                    args.category,
                    args.includeParameters !== false
                );

                if (elements.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: `No ${args.category} elements found in the Revit model.`,
                        }],
                    };
                }

                const outputPath = await exportToExcel(elements, {
                    filePath: args.filePath,
                    sheetName: args.sheetName || args.category,
                });

                return {
                    content: [{
                        type: "text",
                        text: `✅ Exported ${elements.length} ${args.category} elements to Excel.\nFile: ${outputPath}`,
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Failed to export to Excel: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 2. Export to Notion
    // ─────────────────────────────────────────────
    server.tool(
        "export_to_notion",
        "Sync Revit elements to a Notion database. Creates one page per element. " +
        "Requires NOTION_API_KEY in .env and the database must be shared with your integration.",
        {
            category: z.string().describe(
                "Revit category to export, e.g. 'Doors', 'Rooms', 'Walls'"
            ),
            databaseId: z.string().describe(
                "The Notion database ID (32-char hex, from the database URL)"
            ),
            fieldMapping: z.record(z.string()).optional().describe(
                'Optional mapping: { "Notion Property Name": "revit_field_name" }. If omitted, fields are auto-mapped by name.'
            ),
            includeParameters: z.boolean().optional().describe(
                "Include all element parameters (default: true)"
            ),
        },
        async (args) => {
            try {
                const elements = await fetchRevitElements(
                    args.category,
                    args.includeParameters !== false
                );

                if (elements.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: `No ${args.category} elements found in the Revit model.`,
                        }],
                    };
                }

                const result = await syncToNotion(elements, {
                    databaseId: args.databaseId,
                    fieldMapping: args.fieldMapping,
                });

                return {
                    content: [{
                        type: "text",
                        text:
                            `✅ Synced ${args.category} to Notion.\n` +
                            `Created: ${result.created} pages\n` +
                            `Failed: ${result.failed}\n` +
                            (result.errors.length > 0
                                ? `Errors:\n${result.errors.slice(0, 5).join("\n")}`
                                : ""),
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Failed to sync to Notion: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 3. Export to Google Sheets
    // ─────────────────────────────────────────────
    server.tool(
        "export_to_google_sheets",
        "Push Revit elements to a Google Sheets spreadsheet. " +
        "Requires GOOGLE_SHEETS_CREDENTIALS_PATH in .env (Service Account JSON key).",
        {
            category: z.string().describe(
                "Revit category to export, e.g. 'Doors', 'Rooms', 'Walls'"
            ),
            spreadsheetId: z.string().describe(
                "The Google Sheets spreadsheet ID (from the URL)"
            ),
            range: z.string().optional().describe(
                "Target range (default: 'Sheet1!A1')"
            ),
            clearFirst: z.boolean().optional().describe(
                "Clear existing data in the sheet before writing (default: false)"
            ),
            includeParameters: z.boolean().optional().describe(
                "Include all element parameters (default: true)"
            ),
        },
        async (args) => {
            try {
                const elements = await fetchRevitElements(
                    args.category,
                    args.includeParameters !== false
                );

                if (elements.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: `No ${args.category} elements found in the Revit model.`,
                        }],
                    };
                }

                const result = await updateSheets(elements, {
                    spreadsheetId: args.spreadsheetId,
                    range: args.range,
                    clearFirst: args.clearFirst,
                });

                return {
                    content: [{
                        type: "text",
                        text:
                            `✅ Exported ${elements.length} ${args.category} to Google Sheets.\n` +
                            `Range: ${result.updatedRange}\n` +
                            `Rows: ${result.updatedRows}, Columns: ${result.updatedColumns}\n` +
                            `URL: ${result.spreadsheetUrl}`,
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Failed to export to Google Sheets: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 4. Export to SQLite
    // ─────────────────────────────────────────────
    server.tool(
        "export_to_sqlite",
        "Save a snapshot of Revit elements to a local SQLite database. " +
        "Each export creates a timestamped snapshot for delta-sync comparison.",
        {
            category: z.string().describe(
                "Revit category to export, e.g. 'Doors', 'Rooms', 'Walls'"
            ),
            tableName: z.string().optional().describe(
                "Custom table name (default: uses category name)"
            ),
            includeParameters: z.boolean().optional().describe(
                "Include all element parameters (default: true)"
            ),
        },
        async (args) => {
            try {
                const elements = await fetchRevitElements(
                    args.category,
                    args.includeParameters !== false
                );

                if (elements.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: `No ${args.category} elements found in the Revit model.`,
                        }],
                    };
                }

                const result = saveSnapshot(elements, args.category, args.tableName);

                return {
                    content: [{
                        type: "text",
                        text:
                            `✅ Saved ${result.rowsInserted} ${args.category} to SQLite.\n` +
                            `Table: ${result.tableName}\n` +
                            `Snapshot ID: ${result.snapshotId}\n` +
                            `Database: ${result.dbPath}`,
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Failed to save to SQLite: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 5. Get SQLite snapshots
    // ─────────────────────────────────────────────
    server.tool(
        "get_sqlite_snapshots",
        "List all saved snapshots for a category in the local SQLite database.",
        {
            tableName: z.string().describe(
                "Table name to query (e.g. 'doors', 'walls', 'rooms')"
            ),
        },
        async (args) => {
            try {
                const snapshots = getSnapshots(args.tableName);
                if (snapshots.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: `No snapshots found for '${args.tableName}'.`,
                        }],
                    };
                }
                return {
                    content: [{
                        type: "text",
                        text: JSON.stringify(snapshots, null, 2),
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Failed: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 6. AI Field Mapping (Ollama)
    // ─────────────────────────────────────────────
    server.tool(
        "ai_map_fields",
        "Use a local AI (Ollama / Qwen 2.5 7b) to intelligently map Revit element fields to target database headers. " +
        "Ollama must be running locally (ollama serve).",
        {
            category: z.string().describe(
                "Revit category to analyze, e.g. 'Doors', 'Rooms'"
            ),
            targetHeaders: z.array(z.string()).describe(
                "Target column headers to map to, e.g. ['Door ID', 'Floor', 'Size', 'Material']"
            ),
        },
        async (args) => {
            try {
                const ollama = getOllamaService();

                // Check if Ollama is running
                const available = await ollama.isAvailable();
                if (!available) {
                    return {
                        content: [{
                            type: "text",
                            text:
                                "❌ Ollama is not running. Start it with: ollama serve\n" +
                                "Then pull the model: ollama pull qwen2.5:7b-instruct-q4_K_M",
                        }],
                    };
                }

                // Fetch sample data from Revit
                const elements = await fetchRevitElements(args.category, true);

                if (elements.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: `No ${args.category} elements found in the Revit model.`,
                        }],
                    };
                }

                // Get field names from the data
                const fieldSet = new Set<string>();
                for (const el of elements.slice(0, 5)) {
                    for (const key of Object.keys(el)) {
                        fieldSet.add(key);
                    }
                }
                const revitFields = Array.from(fieldSet);

                // Ask AI for mapping
                const mapping = await ollama.mapFields(
                    revitFields,
                    args.targetHeaders,
                    elements.slice(0, 3) // Send 3 sample elements for context
                );

                return {
                    content: [{
                        type: "text",
                        text:
                            `🤖 AI Field Mapping (${args.category} → Target Headers)\n\n` +
                            `Revit fields found: ${revitFields.length}\n` +
                            `Elements sampled: ${Math.min(elements.length, 3)}\n\n` +
                            `Mapping:\n${mapping}`,
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `AI mapping failed: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 7. Ollama Chat (BIM Data Architect)
    // ─────────────────────────────────────────────
    server.tool(
        "ollama_chat",
        "Chat with a local AI (Ollama / Qwen 2.5 7b) acting as a BIM Data Architect. " +
        "Maintains conversation history. Ollama must be running locally.",
        {
            message: z.string().describe("Your message or question for the local AI"),
            model: z.string().optional().describe(
                "Ollama model name (default: qwen2.5:7b-instruct-q4_K_M)"
            ),
        },
        async (args) => {
            try {
                const ollama = getOllamaService();

                if (args.model) ollama.setModel(args.model);

                const available = await ollama.isAvailable();
                if (!available) {
                    return {
                        content: [{
                            type: "text",
                            text:
                                "❌ Ollama is not running. Start it with: ollama serve\n" +
                                "Then pull the model: ollama pull qwen2.5:7b-instruct-q4_K_M",
                        }],
                    };
                }

                const response = await ollama.chat(args.message);
                return { content: [{ type: "text", text: response }] };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Ollama error: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 8. Export Revit Data (Lightweight / Data Bridge)
    // ─────────────────────────────────────────────
    server.tool(
        "export_revit_data",
        "Lightweight metadata-only export of Revit elements using batched streaming (100 per cycle). " +
        "Returns minified data (id, guid, name, category, level, mark, area, typeName, editedBy) " +
        "without loading full parameters. Supports delta-sync via modifiedAfter. Ideal for large models.",
        {
            category: z.string().describe(
                "Revit category to export, e.g. 'Walls', 'Doors', 'Windows', 'Rooms', 'Columns'"
            ),
            batchSize: z.number().optional().describe(
                "Elements per batch (default: 100). Max recommended: 100 to prevent Revit UI freezing."
            ),
            modifiedAfter: z.string().optional().describe(
                "ISO 8601 timestamp — only export elements modified after this date (delta-sync)"
            ),
        },
        async (args) => {
            try {
                const elements = await fetchRevitElementsBatched(
                    args.category,
                    args.batchSize || 100,
                    args.modifiedAfter
                );

                if (elements.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: `No ${args.category} elements found in the Revit model.`,
                        }],
                    };
                }

                return {
                    content: [{
                        type: "text",
                        text: JSON.stringify({
                            category: args.category,
                            totalElements: elements.length,
                            fields: Object.keys(elements[0] || {}),
                            data: elements,
                        }, null, 2),
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Export failed: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 9. Read Notion Database
    // ─────────────────────────────────────────────
    server.tool(
        "read_notion_database",
        "Read pages from a Notion database. Use this to verify synced data or pull external data back. " +
        "Requires NOTION_API_KEY in .env.",
        {
            databaseId: z.string().describe(
                "The Notion database ID (32-char hex, from the database URL)"
            ),
            pageSize: z.number().optional().describe(
                "Max pages to retrieve (default: 100)"
            ),
        },
        async (args) => {
            try {
                const pages = await queryNotionDatabase(
                    args.databaseId,
                    args.pageSize || 100
                );

                return {
                    content: [{
                        type: "text",
                        text: JSON.stringify({
                            databaseId: args.databaseId,
                            pageCount: pages.length,
                            pages,
                        }, null, 2),
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Notion read failed: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 10. Read Google Sheets
    // ─────────────────────────────────────────────
    server.tool(
        "read_google_sheets",
        "Read data from a Google Sheets spreadsheet. Use this to verify synced data or pull external data. " +
        "Requires GOOGLE_SHEETS_CREDENTIALS_PATH in .env.",
        {
            spreadsheetId: z.string().describe(
                "The Google Sheets spreadsheet ID (from the URL)"
            ),
            range: z.string().optional().describe(
                "Range to read (default: 'Sheet1'). Examples: 'Sheet1!A1:F50', 'Data'"
            ),
        },
        async (args) => {
            try {
                const rows = await readSheets(
                    args.spreadsheetId,
                    args.range || "Sheet1"
                );

                return {
                    content: [{
                        type: "text",
                        text: JSON.stringify({
                            spreadsheetId: args.spreadsheetId,
                            range: args.range || "Sheet1",
                            rowCount: rows.length,
                            headers: rows.length > 0 ? rows[0] : [],
                            data: rows,
                        }, null, 2),
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Google Sheets read failed: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 11. Compare SQLite Snapshots (Delta-Sync)
    // ─────────────────────────────────────────────
    server.tool(
        "compare_snapshots",
        "Compare two SQLite snapshots of Revit data to find added, removed, and changed elements. " +
        "Useful for tracking design changes between sync sessions.",
        {
            tableName: z.string().describe(
                "Table name to compare (e.g. 'doors', 'walls', 'rooms')"
            ),
            olderSnapshotId: z.number().describe(
                "The older (baseline) snapshot ID"
            ),
            newerSnapshotId: z.number().describe(
                "The newer (current) snapshot ID"
            ),
        },
        async (args) => {
            try {
                const olderData = getSnapshotData(args.tableName, args.olderSnapshotId);
                const newerData = getSnapshotData(args.tableName, args.newerSnapshotId);

                if (olderData.length === 0 && newerData.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: `No data found for snapshots ${args.olderSnapshotId} and ${args.newerSnapshotId} in '${args.tableName}'.`,
                        }],
                    };
                }

                // Build lookup by element id (or guid if available)
                const keyField = olderData[0]?.["guid"] ? "guid" : "id";
                const olderMap = new Map<string, Record<string, unknown>>();
                const newerMap = new Map<string, Record<string, unknown>>();

                for (const item of olderData) {
                    const key = String(item[keyField] || "");
                    if (key) olderMap.set(key, item);
                }
                for (const item of newerData) {
                    const key = String(item[keyField] || "");
                    if (key) newerMap.set(key, item);
                }

                // Find added, removed, changed
                const added: Record<string, unknown>[] = [];
                const removed: Record<string, unknown>[] = [];
                const changed: { key: string; changes: Record<string, { old: unknown; new: unknown }> }[] = [];

                for (const [key, newItem] of newerMap) {
                    if (!olderMap.has(key)) {
                        added.push(newItem);
                    } else {
                        const oldItem = olderMap.get(key)!;
                        const diffs: Record<string, { old: unknown; new: unknown }> = {};
                        for (const field of Object.keys(newItem)) {
                            if (String(newItem[field]) !== String(oldItem[field])) {
                                diffs[field] = { old: oldItem[field], new: newItem[field] };
                            }
                        }
                        if (Object.keys(diffs).length > 0) {
                            changed.push({ key, changes: diffs });
                        }
                    }
                }

                for (const [key, oldItem] of olderMap) {
                    if (!newerMap.has(key)) {
                        removed.push(oldItem);
                    }
                }

                return {
                    content: [{
                        type: "text",
                        text: JSON.stringify({
                            tableName: args.tableName,
                            olderSnapshot: args.olderSnapshotId,
                            newerSnapshot: args.newerSnapshotId,
                            summary: {
                                added: added.length,
                                removed: removed.length,
                                changed: changed.length,
                                unchanged: newerMap.size - added.length - changed.length,
                            },
                            added: added.slice(0, 50),
                            removed: removed.slice(0, 50),
                            changed: changed.slice(0, 50),
                        }, null, 2),
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Snapshot comparison failed: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 12. AI Analyze Data (Ollama)
    // ─────────────────────────────────────────────
    server.tool(
        "ai_analyze_data",
        "Use a local AI (Ollama / Qwen 2.5 7b) to analyze Revit element data and provide BIM insights. " +
        "Analyzes patterns, detects anomalies, suggests optimizations. Ollama must be running locally.",
        {
            category: z.string().describe(
                "Revit category to analyze, e.g. 'Doors', 'Rooms', 'Walls'"
            ),
            analysisType: z.enum([
                "summary",
                "anomalies",
                "cost_estimate",
                "compliance",
                "optimization",
            ]).optional().describe(
                "Type of analysis: 'summary' (default), 'anomalies', 'cost_estimate', 'compliance', 'optimization'"
            ),
            maxElements: z.number().optional().describe(
                "Max elements to send to AI (default: 20, to fit in context window)"
            ),
        },
        async (args) => {
            try {
                const ollama = getOllamaService();

                const available = await ollama.isAvailable();
                if (!available) {
                    return {
                        content: [{
                            type: "text",
                            text:
                                "❌ Ollama is not running. Start it with: ollama serve\n" +
                                "Then pull the model: ollama pull qwen2.5:7b-instruct-q4_K_M",
                        }],
                    };
                }

                // Fetch elements
                const elements = await fetchRevitElements(args.category, true);

                if (elements.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: `No ${args.category} elements found in the Revit model.`,
                        }],
                    };
                }

                const maxElements = args.maxElements || 20;
                const sample = elements.slice(0, maxElements);
                const analysisType = args.analysisType || "summary";

                const response = await ollama.analyzeData(
                    sample,
                    args.category,
                    analysisType
                );

                return {
                    content: [{
                        type: "text",
                        text:
                            `🤖 AI Analysis — ${args.category} (${analysisType})\n\n` +
                            `Elements analyzed: ${sample.length} of ${elements.length}\n\n` +
                            response,
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `AI analysis failed: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 13. Delta-Sync (Automated Pipeline)
    // ─────────────────────────────────────────────
    server.tool(
        "delta_sync",
        "One-click delta-sync: exports current Revit data → saves to SQLite snapshot → compares with previous snapshot → reports added/removed/changed elements. " +
        "Only exports elements modified since the last sync (if previous snapshot exists).",
        {
            category: z.string().describe(
                "Revit category to sync, e.g. 'Walls', 'Doors', 'Rooms'"
            ),
        },
        async (args) => {
            try {
                // Step 1: Check for existing snapshots
                const tableName = args.category.toLowerCase().replace(/[^a-z0-9_]/g, "_");
                let previousSnapshots: { id: number; category: string; element_count: number; created_at: string }[] = [];
                try {
                    previousSnapshots = getSnapshots(tableName);
                } catch {
                    // Table doesn't exist yet — first sync
                }

                // Step 2: Export current data from Revit (batched at 100)
                const elements = await fetchRevitElementsBatched(args.category, 100);

                if (elements.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: `No ${args.category} elements found in the Revit model. Nothing to sync.`,
                        }],
                    };
                }

                // Step 3: Save new snapshot
                const snapshot = saveSnapshot(elements, args.category);

                // Step 4: Compare with previous (if exists)
                let deltaReport = "First sync — no previous data to compare against.";

                if (previousSnapshots.length > 0) {
                    const prevId = previousSnapshots[0].id;
                    const prevData = getSnapshotData(tableName, prevId);

                    // Build diff
                    const keyField = elements[0]?.["guid"] ? "guid" : "id";
                    const oldMap = new Map<string, Record<string, unknown>>();
                    const newMap = new Map<string, Record<string, unknown>>();

                    for (const item of prevData) {
                        const key = String(item[keyField] || "");
                        if (key) oldMap.set(key, item);
                    }
                    for (const item of elements) {
                        const key = String(item[keyField] || "");
                        if (key) newMap.set(key, item);
                    }

                    let added = 0, removed = 0, changed = 0;
                    const changes: string[] = [];

                    for (const [key, newItem] of newMap) {
                        if (!oldMap.has(key)) {
                            added++;
                            changes.push(`  + Added: ${newItem["name"] || key}`);
                        } else {
                            const oldItem = oldMap.get(key)!;
                            const diffs: string[] = [];
                            for (const field of Object.keys(newItem)) {
                                if (String(newItem[field]) !== String(oldItem[field])) {
                                    diffs.push(`${field}: "${oldItem[field]}" → "${newItem[field]}"`);
                                }
                            }
                            if (diffs.length > 0) {
                                changed++;
                                changes.push(`  ✎ Changed: ${newItem["name"] || key} (${diffs.join(", ")})`);
                            }
                        }
                    }

                    for (const [key, oldItem] of oldMap) {
                        if (!newMap.has(key)) {
                            removed++;
                            changes.push(`  - Removed: ${oldItem["name"] || key}`);
                        }
                    }

                    const unchanged = newMap.size - added - changed;
                    deltaReport =
                        `Delta vs Snapshot #${prevId} (${previousSnapshots[0].created_at}):\n` +
                        `  Added: ${added}, Removed: ${removed}, Changed: ${changed}, Unchanged: ${unchanged}\n` +
                        (changes.length > 0 ? `\nDetails:\n${changes.slice(0, 30).join("\n")}` : "\n  No changes detected.");
                }

                return {
                    content: [{
                        type: "text",
                        text:
                            `✅ Delta-Sync Complete — ${args.category}\n\n` +
                            `Exported: ${elements.length} elements\n` +
                            `Snapshot: #${snapshot.snapshotId} (${snapshot.tableName})\n` +
                            `Database: ${snapshot.dbPath}\n\n` +
                            deltaReport,
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Delta-sync failed: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 14. AI Summarize & Push (Test Pipeline)
    // ─────────────────────────────────────────────
    server.tool(
        "ai_summarize_and_push",
        "Test pipeline: Ask Ollama (Qwen 2.5 7b) to summarize Revit data (e.g. Room count) " +
        "and push the result to Google Sheets or Excel. Ollama must be running locally.",
        {
            category: z.string().describe(
                "Revit category to summarize, e.g. 'Rooms', 'Doors', 'Walls'"
            ),
            pushTo: z.enum(["excel", "google_sheets"]).optional().describe(
                "Where to push the summary (default: 'excel')"
            ),
            spreadsheetId: z.string().optional().describe(
                "Google Sheets spreadsheet ID (required if pushTo = 'google_sheets')"
            ),
            filePath: z.string().optional().describe(
                "Excel file path (optional, defaults to Desktop)"
            ),
        },
        async (args) => {
            try {
                const ollama = getOllamaService();

                const available = await ollama.isAvailable();
                if (!available) {
                    return {
                        content: [{
                            type: "text",
                            text:
                                "❌ Ollama is not running. Start it with: ollama serve\n" +
                                "Then pull the model: ollama pull qwen2.5:7b-instruct-q4_K_M",
                        }],
                    };
                }

                // Step 1: Fetch data
                const elements = await fetchRevitElementsBatched(args.category, 100);

                if (elements.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: `No ${args.category} elements found in the Revit model.`,
                        }],
                    };
                }

                // Step 2: Ask Qwen to summarize
                const sample = elements.slice(0, 20);
                const summaryPrompt =
                    `Summarize this Revit ${args.category} data for a project report:\n` +
                    `Total count: ${elements.length}\n` +
                    `Sample data:\n\`\`\`json\n${JSON.stringify(sample, null, 2)}\n\`\`\`\n\n` +
                    `Provide:\n` +
                    `1. Total ${args.category} count\n` +
                    `2. Breakdown by level\n` +
                    `3. Breakdown by type\n` +
                    `4. Any notable patterns\n` +
                    `Keep it concise (under 200 words).`;

                const summary = await ollama.generate(summaryPrompt);

                // Step 3: Push to target
                const pushTo = args.pushTo || "excel";
                let pushResult = "";

                // Prepare summary data as a table row
                const summaryData = [{
                    "Category": args.category,
                    "Total Count": elements.length,
                    "Sync Date": new Date().toISOString(),
                    "AI Summary": summary.substring(0, 500),
                }];

                if (pushTo === "google_sheets" && args.spreadsheetId) {
                    const sheetsResult = await updateSheets(summaryData, {
                        spreadsheetId: args.spreadsheetId,
                        range: "Sheet1!A1",
                        clearFirst: true,
                    });
                    pushResult = `Pushed to Google Sheets: ${sheetsResult.spreadsheetUrl}`;
                } else {
                    const excelPath = await exportToExcel(summaryData, {
                        filePath: args.filePath,
                        sheetName: `${args.category} Summary`,
                    });
                    pushResult = `Saved to Excel: ${excelPath}`;
                }

                return {
                    content: [{
                        type: "text",
                        text:
                            `🤖 AI Summary & Push — ${args.category}\n\n` +
                            `${summary}\n\n` +
                            `📊 ${pushResult}`,
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Summary & push failed: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 13. Google Sign-In (OAuth 2.0)
    // ─────────────────────────────────────────────
    server.tool(
        "google_signin",
        "Sign in with Google to connect Google Sheets. Opens a browser window for authentication. " +
        "After sign-in, you can export/import data to/from your Google Sheets directly.",
        {},
        async () => {
            try {
                // Check if already connected
                if (isOAuthConnected()) {
                    return {
                        content: [{
                            type: "text",
                            text: "✅ Already signed in to Google. Use list_google_spreadsheets to see your files.",
                        }],
                    };
                }

                const auth = new GoogleAuth();
                await auth.authorize();

                // Reset cached clients so they pick up new tokens
                resetSheetsClient();

                return {
                    content: [{
                        type: "text",
                        text: "✅ Successfully signed in to Google!\n" +
                            "You can now export data to Google Sheets and browse your spreadsheets.\n" +
                            "Use list_google_spreadsheets to see your files.",
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Google sign-in failed: ${error instanceof Error ? error.message : String(error)}\n\n` +
                            `Make sure GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET are set in .env\n` +
                            `Get them at: https://console.cloud.google.com/apis/credentials`,
                    }],
                };
            }
        }
    );

    // ─────────────────────────────────────────────
    // 14. List Google Spreadsheets
    // ─────────────────────────────────────────────
    server.tool(
        "list_google_spreadsheets",
        "List your Google Sheets spreadsheets. Requires Google sign-in first (google_signin). " +
        "Returns spreadsheet names, IDs, and last modified times.",
        {
            maxResults: z.number().optional().describe(
                "Maximum number of spreadsheets to return (default: 20)"
            ),
        },
        async (args) => {
            try {
                if (!isOAuthConnected()) {
                    return {
                        content: [{
                            type: "text",
                            text: "❌ Not signed in to Google. Use google_signin first.",
                        }],
                    };
                }

                const spreadsheets = await listSpreadsheets(args.maxResults || 20);

                if (spreadsheets.length === 0) {
                    return {
                        content: [{
                            type: "text",
                            text: "No spreadsheets found in your Google Drive.",
                        }],
                    };
                }

                return {
                    content: [{
                        type: "text",
                        text: JSON.stringify({
                            authMode: getAuthMode(),
                            count: spreadsheets.length,
                            spreadsheets,
                        }, null, 2),
                    }],
                };
            } catch (error) {
                return {
                    content: [{
                        type: "text",
                        text: `Failed to list spreadsheets: ${error instanceof Error ? error.message : String(error)}`,
                    }],
                };
            }
        }
    );
}
