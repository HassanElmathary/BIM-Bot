import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { parseElementQuery } from "../utils/nl-filter.js";

/**
 * Advanced Tools — Code execution, AI filter, and model reset
 */
export function registerAdvancedTools(server: McpServer) {

    // 1. Send code to Revit
    server.tool(
        "send_code_to_revit",
        "Send C# code to execute directly in Revit. The code runs in the context of the Revit API with access to the current Document and UIApplication. Use for advanced operations not covered by other tools.",
        {
            code: z.string().describe("C# code to execute in Revit. Has access to: UIApplication uiApp, Document doc, and all Revit API namespaces."),
            description: z.string().optional().describe("Human-readable description of what the code does"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("send_code_to_revit", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 2. AI element filter — natural language → structured conditions,
    // evaluated by the generate_dynamic_schedule command (numeric,
    // unit-aware, instance+type parameters)
    server.tool(
        "ai_element_filter",
        "Use natural language to filter elements. Describe what you want to find and the system will translate it to Revit filters, e.g. 'all doors wider than 900mm in Level 1'. Returns matching elements with their IDs (use select_elements to highlight them).",
        {
            query: z.string().describe("Natural language description of elements to find, e.g. 'all walls taller than 3 m'"),
            category: z.string().optional().describe("Optional category hint to narrow the search"),
        },
        async (args) => {
            try {
                const parsed = parseElementQuery(args.query);
                const category = args.category ?? parsed.category;
                if (!category) {
                    return { content: [{ type: "text", text:
                        "Could not detect a category in the query. Mention one (doors, walls, rooms, …) or pass the category argument." }] };
                }

                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("generate_dynamic_schedule", {
                        category,
                        conditions: parsed.conditions,
                        level: parsed.level,
                        fields: [],
                        limit: 0,
                    })
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 3. Reset model view
    server.tool(
        "reset_view",
        "Reset the current view to default settings (zoom extents, clear overrides).",
        {
            clearOverrides: z.boolean().optional().describe("Clear all graphic overrides (default: false)"),
            zoomExtents: z.boolean().optional().describe("Zoom to fit all elements (default: true)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("reset_view", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 4. Select elements
    server.tool(
        "select_elements",
        "Select specific elements in Revit by their IDs.",
        {
            elementIds: z.array(z.number()).describe("Element IDs to select"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("select_elements", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 5. Get model statistics
    server.tool(
        "get_model_statistics",
        "Get comprehensive model statistics: element counts by category, total elements, file size, warnings count, etc.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_model_statistics", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
