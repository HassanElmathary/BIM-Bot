import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Sketch Editing Tools
 * Fixes Limitation #3 (No Sketch/Geometry Editing)
 */
export function registerSketchTools(server: McpServer) {

    server.tool(
        "get_sketch",
        "Get the sketch profile (boundary curves) of a floor, roof, or ceiling element. Returns the curve loops as coordinate arrays.",
        {
            elementId: z.number().describe("Element ID of the floor, roof, or ceiling"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_sketch", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "edit_sketch",
        "Modify the sketch profile of a floor, roof, or ceiling. Can add lines, remove lines, or move vertices within the sketch.",
        {
            elementId: z.number().describe("Element ID of the floor, roof, or ceiling to edit"),
            action: z.enum(["add_line", "remove_line", "move_vertex"]).describe("Type of edit to perform"),
            startPoint: z.object({
                x: z.number(), y: z.number(), z: z.number().optional()
            }).optional().describe("Start point for add_line, or original position for move_vertex"),
            endPoint: z.object({
                x: z.number(), y: z.number(), z: z.number().optional()
            }).optional().describe("End point for add_line, or new position for move_vertex"),
            lineIndex: z.number().optional().describe("Line index to remove (for remove_line action)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("edit_sketch", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "set_sketch_profile",
        "Replace the entire sketch profile of a floor, roof, or ceiling with new boundary curves. The new profile must form a closed loop.",
        {
            elementId: z.number().describe("Element ID of the floor, roof, or ceiling"),
            profile: z.array(z.object({
                x: z.number().describe("X coordinate in feet"),
                y: z.number().describe("Y coordinate in feet"),
                z: z.number().optional().describe("Z coordinate in feet (default: element's level elevation)"),
            })).describe("Array of points forming the new closed boundary profile"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("set_sketch_profile", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
