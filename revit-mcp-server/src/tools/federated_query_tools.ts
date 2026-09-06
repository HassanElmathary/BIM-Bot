import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerFederatedQueryTools(server: McpServer) {
    server.tool(
        "get_federation_summary",
        "Returns dashboard of host + all links.",
        {},
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_federation_summary", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "query_across_links",
        "Cross-model element query.",
        {
            category: z.string().optional().describe("Element category"),
            linkNames: z.array(z.string()).optional().describe("Names of links to query"),
            includeHost: z.boolean().optional().default(true).describe("Include host model"),
            includeParameters: z.boolean().optional().describe("Include all parameters"),
            parameterFilter: z.string().optional().describe("Filter string, e.g. 'Fire Rating=2 HR'"),
            offset: z.number().optional().describe("Pagination offset"),
            limit: z.number().optional().default(200).describe("Pagination limit")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("query_across_links", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "get_link_element_details",
        "Full parameter dump of a linked element.",
        {
            namespacedId: z.string().describe("Namespaced ID, e.g. 'STR-Link:12345'")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("get_link_element_details", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "compare_link_levels",
        "Detect level name/elevation mismatches across all models.",
        {},
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("compare_link_levels", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    server.tool(
        "find_cross_link_spatial_containment",
        "Maps linked equipment to host rooms.",
        {
            equipmentCategory: z.string().optional().default("Mechanical Equipment").describe("Equipment category"),
            sourceLinkName: z.string().optional().describe("Source link name")
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("find_cross_link_spatial_containment", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
