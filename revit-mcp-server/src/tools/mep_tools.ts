import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

/**
 * MEP System Tools (exposing existing C# handlers + new ones)
 * Fixes Limitation #7 (No MEP System Logic)
 */
export function registerMepTools(server: McpServer) {

    server.tool(
        "create_duct",
        "Create a duct segment between two points. Specify duct type and size.",
        {
            startX: z.number().describe("Start X coordinate in feet"),
            startY: z.number().describe("Start Y coordinate in feet"),
            startZ: z.number().describe("Start Z (elevation) in feet"),
            endX: z.number().describe("End X coordinate"),
            endY: z.number().describe("End Y coordinate"),
            endZ: z.number().describe("End Z coordinate"),
            ductType: z.string().optional().describe("Duct type name (default: first available)"),
            width: z.number().optional().describe("Duct width in inches (for rectangular)"),
            height: z.number().optional().describe("Duct height in inches (for rectangular)"),
            diameter: z.number().optional().describe("Duct diameter in inches (for round)"),
            level: z.string().optional().describe("Level name (default: closest level)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_duct", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_pipe",
        "Create a pipe segment between two points. Specify pipe type and diameter.",
        {
            startX: z.number().describe("Start X coordinate in feet"),
            startY: z.number().describe("Start Y coordinate"),
            startZ: z.number().describe("Start Z (elevation) in feet"),
            endX: z.number().describe("End X coordinate"),
            endY: z.number().describe("End Y coordinate"),
            endZ: z.number().describe("End Z coordinate"),
            pipeType: z.string().optional().describe("Pipe type name (default: first available)"),
            diameter: z.number().optional().describe("Pipe diameter in inches"),
            systemType: z.string().optional().describe("System type: 'SupplyHydronicPiping', 'ReturnHydronicPiping', 'DomesticColdWater', etc."),
            level: z.string().optional().describe("Level name (default: closest level)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_pipe", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_flex_duct",
        "Create a flexible duct from a series of waypoints.",
        {
            points: z.array(z.object({
                x: z.number(), y: z.number(), z: z.number()
            })).describe("Array of waypoints for the flex duct path"),
            ductType: z.string().optional().describe("Flex duct type name"),
            diameter: z.number().optional().describe("Diameter in inches"),
            level: z.string().optional().describe("Level name"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_flex_duct", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_electrical_circuit",
        "Create an electrical circuit from a panel and connected device element IDs.",
        {
            panelId: z.number().describe("Element ID of the electrical panel"),
            deviceIds: z.array(z.number()).describe("Element IDs of devices to add to the circuit"),
            circuitType: z.enum(["Power", "Data", "FireAlarm", "Communication"]).optional().describe("Circuit type (default: 'Power')"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_electrical_circuit", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "connect_mep_elements",
        "Auto-connect two MEP elements by generating appropriate fittings (elbows, tees, transitions).",
        {
            elementId1: z.number().describe("First MEP element ID"),
            elementId2: z.number().describe("Second MEP element ID"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("connect_mep_elements", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "get_mep_systems",
        "Get all MEP systems in the project (duct, pipe, electrical) with element counts and system info.",
        {
            systemType: z.string().optional().describe("Filter by system type: 'Duct', 'Pipe', 'Electrical', or 'All' (default: 'All')"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_mep_systems", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "duct_sizing",
        "Auto-size ducts based on airflow requirements. Calculates optimal duct dimensions for a given CFM.",
        {
            elementIds: z.array(z.number()).optional().describe("Duct element IDs to resize (default: all ducts)"),
            method: z.enum(["EqualFriction", "Velocity", "StaticRegain"]).optional().describe("Sizing method (default: 'EqualFriction')"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("duct_sizing", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "create_mep_space",
        "Create an MEP space at a specified location on a level for HVAC analysis.",
        {
            levelName: z.string().describe("Level name for the space"),
            x: z.number().optional().describe("X coordinate (default: 0)"),
            y: z.number().optional().describe("Y coordinate (default: 0)"),
            name: z.string().optional().describe("Space name"),
            number: z.string().optional().describe("Space number"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("create_mep_space", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
