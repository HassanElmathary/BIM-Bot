import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withNavisConnection } from "../utils/navisConnectionManager.js";

/**
 * Navisworks → Power BI dashboards (v2.8.0). Same operations as the plugin's Dashboard panel:
 * a per-project registry of dashboard definitions, exported as a .pbit wired to CSV data with the
 * BIM-Bot 3D Viewer bound to every visual (click an element → the page filters; filter data → the
 * model dims). Routes to navis_dashboard_* in the Navisworks plugin (port 8091).
 */

async function call(command: string, args: Record<string, unknown>) {
    try {
        const response = await withNavisConnection((c) => c.sendCommand(command, args));
        return { content: [{ type: "text" as const, text: JSON.stringify(response, null, 2) }] };
    } catch (e) {
        return { content: [{ type: "text" as const, text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] };
    }
}

const dashboardRef = z.string().describe("Dashboard id or name (from navis_dashboard_list)");

export function registerNavisDashboardTools(server: McpServer) {

    server.tool(
        "navis_dashboard_catalog",
        "List the visual types a Power BI dashboard can contain (with their field roles) and every bindable " +
        "field: 'Table.Column' or 'Table.[Measure]' from Elements, Properties and Clashes. Use before building " +
        "or editing a dashboard config.",
        {},
        async () => call("navis_dashboard_catalog", {})
    );

    server.tool(
        "navis_dashboard_list",
        "List the Power BI dashboards saved for the open Navisworks project (name, created, modified, pages, " +
        "visuals, last export). The registry is stored next to the NWF/NWD in .bimbot\\.",
        {},
        async () => call("navis_dashboard_list", {})
    );

    server.tool(
        "navis_dashboard_get",
        "Get one dashboard's full configuration JSON (pages, visuals, 3D viewer settings, export options). " +
        "Edit it and pass it back to navis_dashboard_update.",
        { dashboard: dashboardRef },
        async (args) => call("navis_dashboard_get", args)
    );

    server.tool(
        "navis_dashboard_create",
        "Create a dashboard definition in the project registry (does not export). template 'default' = Model " +
        "Overview + Clash Coordination pages with the interactive 3D model, KPI cards, slicers and charts; " +
        "'blank' = one page with the 3D viewer and a title. Or pass a full config (see navis_dashboard_catalog).",
        {
            name: z.string().optional().describe("Dashboard name (default: 'Default Dashboard' / 'Custom Dashboard', made unique)"),
            template: z.enum(["default", "blank"]).optional().describe("Starting point (default 'default'). Ignored when config is given."),
            description: z.string().optional(),
            config: z.record(z.any()).optional().describe("Full dashboard config JSON (same shape as navis_dashboard_get returns)"),
        },
        async (args) => call("navis_dashboard_create", args)
    );

    server.tool(
        "navis_dashboard_update",
        "Save an edited dashboard config: overwrite it, or saveAsCopy=true to keep the original and save a new " +
        "dashboard. Get the current config with navis_dashboard_get first and send the whole object back.",
        {
            dashboard: dashboardRef,
            config: z.record(z.any()).describe("The full edited config JSON"),
            saveAsCopy: z.boolean().optional().describe("Save as a new dashboard instead of overwriting (default false)"),
            newName: z.string().optional().describe("Name of the copy (with saveAsCopy)"),
        },
        async (args) => call("navis_dashboard_update", args)
    );

    server.tool(
        "navis_dashboard_delete",
        "Remove a dashboard from the project registry. Exported .pbit and data files are left on disk.",
        { dashboard: dashboardRef },
        async (args) => call("navis_dashboard_delete", args)
    );

    server.tool(
        "navis_dashboard_export",
        "Export a dashboard to Power BI: reads elements, properties, clash results and (optionally) triangulated " +
        "geometry from the open model, writes CSVs and a .pbit with the interactive 3D model. Update-aware: in live " +
        "mode, when only the data changed the .pbit is left alone (manual Power BI edits survive — press Refresh); " +
        "when the layout changed the .pbit is regenerated. Large models can take several minutes.",
        {
            dashboard: dashboardRef,
            scope: z.enum(["full", "selection"]).optional().describe("full model (default) or the current selection / selectionSet"),
            selectionSet: z.string().optional().describe("Selection or search set to export when scope = selection"),
            refreshMode: z.enum(["live", "static"]).optional().describe("live = fixed data folder that Refresh reloads (default); static = dated snapshot"),
            outputFolder: z.string().optional().describe("Root output folder (default: 'BIM-Bot Dashboards' next to the NWF)"),
            includeGeometry: z.boolean().optional().describe("Export 3D geometry for the viewer (default true)"),
            includeClashes: z.boolean().optional().describe("Export Clash Detective results (default true)"),
            force: z.boolean().optional().describe("Regenerate the .pbit even if the layout is unchanged (overwrites manual Power BI edits)"),
        },
        async (args) => call("navis_dashboard_export", args)
    );
}
