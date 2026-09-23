import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withNavisConnection } from "../utils/navisConnectionManager.js";

/**
 * Navisworks Clash Detective tools — ADDITIVE-ONLY, Manage only.
 * Simulate edition returns a graceful "requires Navisworks Manage" error from the plugin.
 */
export function registerNavisClashTools(server: McpServer) {

    server.tool(
        "navis_list_clash_tests",
        "List Clash Detective tests in Navisworks Manage (name, GUID, status, clash count).",
        {},
        async () => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_list_clash_tests", {}));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_run_clash_test",
        "Run a Clash Detective test by name or GUID in Navisworks Manage.",
        {
            test: z.string().describe("Clash test name or GUID"),
            testType: z.enum(["Hard", "HardConservative", "Duplicates", "Clearance"]).optional().describe("Override test type"),
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) =>
                    c.sendCommand("navis_run_clash_test", { test: args.test, testType: args.testType || "" }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_get_clash_results",
        "Get clash results for a test (up to limit). Each result includes IDs, status, distance, and involved ModelItems.",
        {
            test: z.string().describe("Clash test name or GUID"),
            limit: z.number().optional().describe("Max results (default 100)"),
            statusFilter: z.string().optional().describe("Filter: New/Active/Reviewed/Approved/Resolved (default all)"),
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) =>
                    c.sendCommand("navis_get_clash_results", { test: args.test, limit: args.limit || 100, statusFilter: args.statusFilter || "" }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_set_clash_status",
        "Set status (New/Active/Reviewed/Approved/Resolved) on clash results.",
        {
            test: z.string().describe("Clash test name or GUID"),
            clashIds: z.array(z.string()).describe("Clash result IDs"),
            status: z.enum(["New", "Active", "Reviewed", "Approved", "Resolved"]).describe("New status"),
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) =>
                    c.sendCommand("navis_set_clash_status", { test: args.test, clashIds: args.clashIds, status: args.status }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_export_clash_report",
        "Export a clash test report to HTML/XML in Navisworks Manage.",
        {
            test: z.string().describe("Clash test name or GUID"),
            folder: z.string().optional().describe("Output folder (default: next to the NWD)"),
            format: z.enum(["HTML", "XML"]).optional().describe("Report format (default HTML)"),
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) =>
                    c.sendCommand("navis_export_clash_report", { test: args.test, folder: args.folder || "", format: args.format || "HTML" }));
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text", text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );
}
