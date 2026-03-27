import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Drafting & Line Drawing Tools
 * Fixes Limitation #4 (No Drafting/Detail Line Drawing by Coordinate Array)
 */
export function registerDraftingTools(server: McpServer) {

    server.tool(
        "create_detail_lines",
        "Draw detail lines in a drafting or plan view from an array of coordinate pairs. Supports polylines, rectangles, and free-form shapes.",
        {
            lines: z.array(z.object({
                startX: z.number().describe("Start X coordinate in feet"),
                startY: z.number().describe("Start Y coordinate in feet"),
                endX: z.number().describe("End X coordinate in feet"),
                endY: z.number().describe("End Y coordinate in feet"),
            })).describe("Array of line segments to draw"),
            lineStyle: z.string().optional().describe("Line style name (default: 'Thin Lines')"),
            viewId: z.number().optional().describe("View ID to draw in (default: active view)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_detail_lines", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_model_lines",
        "Draw 3D model lines from an array of coordinate pairs. Lines exist in 3D space and are visible in all views.",
        {
            lines: z.array(z.object({
                startX: z.number(), startY: z.number(), startZ: z.number().optional(),
                endX: z.number(), endY: z.number(), endZ: z.number().optional(),
            })).describe("Array of 3D line segments"),
            lineStyle: z.string().optional().describe("Line style name"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_model_lines", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_detail_arc",
        "Draw a detail arc in a view. Defined by center point, radius, start angle, and end angle.",
        {
            centerX: z.number().describe("Center X coordinate in feet"),
            centerY: z.number().describe("Center Y coordinate in feet"),
            radius: z.number().describe("Arc radius in feet"),
            startAngle: z.number().optional().describe("Start angle in degrees (default: 0)"),
            endAngle: z.number().optional().describe("End angle in degrees (default: 360 for full circle)"),
            lineStyle: z.string().optional().describe("Line style name"),
            viewId: z.number().optional().describe("View ID (default: active view)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_detail_arc", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
