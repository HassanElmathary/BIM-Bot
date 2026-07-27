import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * Editing Tools — 13 tools for modifying existing elements in Revit
 */
export function registerEditingTools(server: McpServer) {

    // 1. Modify parameter
    server.tool(
        "modify_parameter",
        "Set or modify a parameter value on a Revit element.",
        {
            elementId: z.number().describe("Element ID to modify"),
            parameterName: z.string().describe("Parameter name to modify"),
            value: z.union([z.string(), z.number(), z.boolean()]).describe("New value for the parameter"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("modify_element", {
                        elementId: args.elementId,
                        modifications: [{ parameterName: args.parameterName, value: args.value }]
                    })
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 2. Move element
    server.tool(
        "move_element",
        "Move a Revit element by a translation vector (deltaX, deltaY, deltaZ).",
        {
            elementId: z.number().describe("Element ID to move"),
            deltaX: z.number().describe("Translation in X direction (feet)"),
            deltaY: z.number().describe("Translation in Y direction (feet)"),
            deltaZ: z.number().optional().describe("Translation in Z direction (feet, default: 0)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("move_element", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 3. Rotate element
    server.tool(
        "rotate_element",
        "Rotate a Revit element around a point by a specified angle.",
        {
            elementId: z.number().describe("Element ID to rotate"),
            angle: z.number().describe("Rotation angle in degrees"),
            centerX: z.number().optional().describe("Rotation center X (feet, default: element center)"),
            centerY: z.number().optional().describe("Rotation center Y (feet, default: element center)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("rotate_element", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 4. Copy element
    server.tool(
        "copy_element",
        "Copy a Revit element to a new location.",
        {
            elementId: z.number().describe("Element ID to copy"),
            deltaX: z.number().describe("Offset X from original (feet)"),
            deltaY: z.number().describe("Offset Y from original (feet)"),
            deltaZ: z.number().optional().describe("Offset Z from original (feet, default: 0)"),
            count: z.number().optional().describe("Number of copies (default: 1)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("copy_element", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 5. Delete elements
    server.tool(
        "delete_elements",
        "Delete one or more elements from the Revit model by their IDs.",
        {
            elementIds: z.array(z.number()).describe("Array of element IDs to delete"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("delete_elements", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 6. Mirror element
    server.tool(
        "mirror_element",
        "Mirror a Revit element across a line defined by two points.",
        {
            elementId: z.number().describe("Element ID to mirror"),
            axisStartX: z.number().describe("Mirror axis start X (feet)"),
            axisStartY: z.number().describe("Mirror axis start Y (feet)"),
            axisEndX: z.number().describe("Mirror axis end X (feet)"),
            axisEndY: z.number().describe("Mirror axis end Y (feet)"),
            keepOriginal: z.boolean().optional().describe("Keep original element (default: true)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("mirror_element", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 7. Align elements
    server.tool(
        "align_elements",
        "Align multiple elements to a reference element or position.",
        {
            elementIds: z.array(z.number()).describe("Element IDs to align"),
            referenceElementId: z.number().optional().describe("Reference element ID to align to"),
            alignment: z.enum(["Left", "Right", "Top", "Bottom", "Center", "Middle"]).describe("Alignment direction"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("align_elements", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 8. Group elements
    server.tool(
        "group_elements",
        "Create a group from multiple elements.",
        {
            elementIds: z.array(z.number()).describe("Element IDs to group"),
            groupName: z.string().optional().describe("Name for the new group"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("group_elements", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 9. Change element type
    server.tool(
        "change_type",
        "Change the type of an element (e.g. change a wall from one type to another).",
        {
            elementId: z.number().describe("Element ID"),
            newTypeName: z.string().describe("New type name to apply"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("change_type", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 10. Set workset
    server.tool(
        "set_workset",
        "Move elements to a different workset (for workshared projects).",
        {
            elementIds: z.array(z.number()).describe("Element IDs to move"),
            worksetName: z.string().describe("Target workset name"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("set_workset", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 11. Color elements by parameter
    server.tool(
        "color_elements",
        "Color elements in the current view based on a parameter value. Useful for visual analysis.",
        {
            category: z.string().describe("Category of elements to color, e.g. 'Walls', 'Rooms'"),
            parameterName: z.string().describe("Parameter name to base colors on"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("color_elements", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 12. Batch modify parameters
    server.tool(
        "batch_modify_parameters",
        "Modify a parameter value on multiple elements at once.",
        {
            elementIds: z.array(z.number()).describe("Element IDs to modify"),
            parameterName: z.string().describe("Parameter name to modify"),
            value: z.union([z.string(), z.number(), z.boolean()]).describe("New value"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("batch_modify_parameters", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 13. Conditional bulk parameter editor
    server.tool(
        "parameter_batch_editor",
        "Rule-driven bulk parameter editing. Selects a scope (category, element IDs, current selection), " +
        "keeps only elements matching 'where' conditions, then applies each 'set' edit — a literal value, " +
        "a regex find/replace on the existing value, or a copy from another parameter. " +
        "Example: where Category=Walls and Height > 3 m, set 'Fire Rating' = '2h'. " +
        "Run with dryRun: true first to preview which elements change.",
        {
            category: z.string().optional().describe("Category to scope, e.g. 'Walls'"),
            categories: z.array(z.string()).optional().describe("Multiple categories to scope"),
            elementIds: z.array(z.number()).optional().describe("Explicit element IDs instead of a category"),
            useSelection: z.boolean().optional().describe("Use the current Revit selection as the scope"),
            activeViewOnly: z.boolean().optional().describe("Limit a category scope to the active view"),
            where: z.array(z.object({
                parameterName: z.string().describe("Parameter to test (instance or type parameter)"),
                operator: z.enum([
                    "equals", "notEquals", "contains", "notContains", "startsWith", "endsWith",
                    "matches", "greaterThan", "lessThan", "greaterOrEqual", "lessOrEqual",
                    "isEmpty", "isNotEmpty", "exists", "notExists"
                ]).optional().describe("Comparison operator (default: equals). 'matches' is a regex"),
                value: z.union([z.string(), z.number(), z.boolean()]).optional().describe("Value to compare against"),
                unit: z.string().optional().describe("Unit for numeric compares: mm, cm, m, ft, in, m2, m3, deg. Defaults to the parameter's display unit"),
            })).optional().describe("Conditions an element must meet to be edited. Omit to edit the whole scope"),
            matchMode: z.enum(["all", "any"]).optional().describe("Require all conditions (default) or any"),
            set: z.array(z.object({
                parameterName: z.string().describe("Parameter to write"),
                value: z.union([z.string(), z.number(), z.boolean()]).optional().describe("Literal value to set"),
                unit: z.string().optional().describe("Unit of value for numeric parameters"),
                find: z.string().optional().describe("Regex (or literal) to find in the current value — for renaming"),
                replace: z.string().optional().describe("Replacement for 'find'; supports $1 backreferences"),
                regex: z.boolean().optional().describe("Treat 'find' as a regex (default: true)"),
                fromParameter: z.string().optional().describe("Copy the value from this parameter instead"),
                prefix: z.string().optional().describe("Text prepended to the computed value"),
                suffix: z.string().optional().describe("Text appended to the computed value"),
                applyToType: z.boolean().optional().describe("Allow writing the element's type parameter (affects all instances of that type)"),
            })).describe("Edits applied to every matching element"),
            dryRun: z.boolean().optional().describe("Preview the changes without modifying the model (default: false)"),
            limit: z.number().optional().describe("Maximum number of elements to edit"),
            previewLimit: z.number().optional().describe("Maximum change records returned (default: 25)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("parameter_batch_editor", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
