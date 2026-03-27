import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Rendering & View Settings Tools
 * Fixes Limitation #5 (No Rendering Control)
 */
export function registerRenderingTools(server: McpServer) {

    server.tool(
        "set_sun_settings",
        "Configure sun position and shadow settings for the active 3D view. Controls sun azimuth, altitude, and shadow on/off.",
        {
            azimuth: z.number().optional().describe("Sun azimuth angle in degrees (0-360, 0=North)"),
            altitude: z.number().optional().describe("Sun altitude angle in degrees (0-90)"),
            shadowsOn: z.boolean().optional().describe("Enable or disable shadows"),
            date: z.string().optional().describe("Date for sun study in YYYY-MM-DD format"),
            time: z.string().optional().describe("Time for sun study in HH:MM format (24h)"),
            viewId: z.number().optional().describe("View ID (default: active view)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("set_sun_settings", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "set_visual_style",
        "Set the visual/display style of a view (Wireframe, Hidden Line, Shaded, Consistent Colors, Realistic, RayTrace).",
        {
            style: z.enum(["Wireframe", "HiddenLine", "Shaded", "ShadingWithEdges", "Realistic", "RayTrace"]).describe("Display style to apply"),
            viewId: z.number().optional().describe("View ID (default: active view)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("set_visual_style", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "export_view_image",
        "Export the current or specified view as a PNG/JPG image at a given resolution.",
        {
            filePath: z.string().describe("Output file path (e.g. 'C:\\\\Output\\\\view.png')"),
            format: z.enum(["PNG", "JPG", "BMP", "TIFF"]).optional().describe("Image format (default: 'PNG')"),
            resolution: z.number().optional().describe("Image resolution/DPI (default: 150)"),
            pixelWidth: z.number().optional().describe("Image width in pixels (default: 1920)"),
            pixelHeight: z.number().optional().describe("Image height in pixels (default: 1080)"),
            viewId: z.number().optional().describe("View ID to export (default: active view)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("export_view_image", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
