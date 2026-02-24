import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Project Settings Tools — 9 tools for modifying project-level settings in Revit
 */
export function registerSettingsTools(server: McpServer) {

    // 1. Modify Object Styles
    server.tool(
        "modify_object_styles",
        "Modify the default line weight and color for a Revit category (Object Styles). Affects all elements of that category in the project.",
        {
            category: z.string().describe("Category name, e.g. 'Walls', 'Doors', 'Furniture'"),
            subcategory: z.string().optional().describe("Subcategory name (optional)"),
            lineWeight: z.number().optional().describe("Projection line weight (1-16)"),
            colorR: z.number().optional().describe("Line color Red (0-255)"),
            colorG: z.number().optional().describe("Line color Green (0-255)"),
            colorB: z.number().optional().describe("Line color Blue (0-255)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("modify_object_styles", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 2. Set Phase
    server.tool(
        "set_phase",
        "Assign a construction phase (e.g. Existing, New Construction, Demolition) to elements.",
        {
            elementIds: z.array(z.number()).describe("Element IDs to set phase on"),
            phaseName: z.string().describe("Phase name, e.g. 'Existing', 'New Construction'"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("set_phase", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 3. Get Phases
    server.tool(
        "get_phases",
        "List all construction phases available in the Revit project.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_phases", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 4. Get Materials
    server.tool(
        "get_materials",
        "List all materials available in the Revit project with their colors and transparency.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_materials", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 5. Set Material
    server.tool(
        "set_material",
        "Assign a material to elements. The material must already exist in the project.",
        {
            elementIds: z.array(z.number()).describe("Element IDs to assign material to"),
            materialName: z.string().describe("Name of the material to assign"),
            parameterName: z.string().optional().describe("Specific material parameter to set (default: auto-detect)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("set_material", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 6. Set View Properties
    server.tool(
        "set_view_properties",
        "Modify view settings such as scale, detail level, visual style, discipline, phase, name, and crop box. Works on the active view or a specific view by ID.",
        {
            viewId: z.number().optional().describe("View ID (default: active view)"),
            scale: z.number().optional().describe("View scale denominator, e.g. 100 for 1:100"),
            detailLevel: z.enum(["Coarse", "Medium", "Fine"]).optional().describe("View detail level"),
            displayStyle: z.enum(["Wireframe", "HiddenLine", "Shading", "ShadingWithEdges", "Realistic"]).optional().describe("Visual style / display style"),
            discipline: z.enum(["Architectural", "Structural", "Mechanical", "Electrical", "Plumbing", "Coordination"]).optional().describe("View discipline (floor plans only)"),
            phaseName: z.string().optional().describe("Phase to show in this view"),
            viewName: z.string().optional().describe("New name for the view"),
            showCropBox: z.boolean().optional().describe("Enable/disable crop box"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("set_view_properties", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 7. Override Element in View
    server.tool(
        "override_element_in_view",
        "Apply graphic overrides to specific elements in the current view. Can change color, line weight, transparency, halftone, or hide elements.",
        {
            elementIds: z.array(z.number()).describe("Element IDs to override"),
            colorR: z.number().optional().describe("Override color Red (0-255)"),
            colorG: z.number().optional().describe("Override color Green (0-255)"),
            colorB: z.number().optional().describe("Override color Blue (0-255)"),
            lineWeight: z.number().optional().describe("Override line weight (1-16)"),
            transparency: z.number().optional().describe("Surface transparency (0-100)"),
            halftone: z.boolean().optional().describe("Apply halftone effect"),
            visible: z.boolean().optional().describe("Set to false to hide elements in view"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("override_element_in_view", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 8. Get Line Styles
    server.tool(
        "get_line_styles",
        "List all line styles available in the Revit project with their line weight and color.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_line_styles", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 9. Set Line Style
    server.tool(
        "set_line_style",
        "Change the line style of detail lines or model lines in the project.",
        {
            elementIds: z.array(z.number()).describe("IDs of line elements to change"),
            lineStyleName: z.string().describe("Name of the target line style"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("set_line_style", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
