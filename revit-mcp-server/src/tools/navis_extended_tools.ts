import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withNavisConnection } from "../utils/navisConnectionManager.js";

/**
 * Navisworks extended tools — ADDITIVE-ONLY.
 * View/display settings, camera, section planes, TimeLiner, clash-test authoring,
 * measurements, redlines, comments, selection/search sets, user properties and
 * runtime C# execution. All route to the Navisworks plugin (default port 8091).
 * Coordinates and distances are in the document's model units (see navis_get_camera.units).
 */

const xyz = z.array(z.number()).length(3);
const rgb = z.array(z.number()).length(3).describe("[r,g,b] as 0-1 or 0-255");
const ids = z.array(z.string()).optional().describe("ModelItem displayIds from navis_search_items (default: current selection)");
const clashSelection = z.object({
    selectionSets: z.array(z.string()).optional().describe("Selection/search set names or GUIDs"),
    displayIds: z.array(z.string()).optional().describe("ModelItem displayIds"),
    useCurrentSelection: z.boolean().optional(),
    selfIntersect: z.boolean().optional(),
});

async function send(method: string, params: Record<string, unknown>) {
    try {
        const response = await withNavisConnection((c) => c.sendCommand(method, params));
        return { content: [{ type: "text" as const, text: JSON.stringify(response, null, 2) }] };
    } catch (e) {
        return { content: [{ type: "text" as const, text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] };
    }
}

export function registerNavisExtendedTools(server: McpServer) {

    // ── view / display ──

    server.tool(
        "navis_set_view_style",
        "Change Navisworks display settings for the current view: render style, lighting and camera projection.",
        {
            renderStyle: z.enum(["FullRender", "Preview", "Shaded", "Wireframe", "HiddenLine"]).optional(),
            lighting: z.enum(["None", "SceneLights", "Headlight", "FullLights"]).optional(),
            projection: z.enum(["Perspective", "Orthographic"]).optional(),
        },
        async (args) => send("navis_set_view_style", args)
    );

    server.tool(
        "navis_set_background",
        "Set the Navisworks viewport background: plain (1 color), graduated (top, bottom) or horizon (sky, horizon sky, horizon ground, ground).",
        {
            mode: z.enum(["plain", "graduated", "horizon"]).optional().describe("Default plain"),
            colors: z.array(rgb).min(1).max(4),
        },
        async (args) => send("navis_set_background", args)
    );

    server.tool(
        "navis_get_grids",
        "List Navisworks grid systems, their levels and the current grid render mode.",
        {},
        async () => send("navis_get_grids", {})
    );

    server.tool(
        "navis_set_grids",
        "Change grids & levels display: active grid system, which levels are shown (render mode) or lock to a level.",
        {
            activeSystem: z.string().optional().describe("Grid system name (see navis_get_grids)"),
            renderMode: z.enum(["AboveAndBelow", "Above", "Below", "All", "Locked"]).optional(),
            lockedLevel: z.string().optional().describe("Level name to lock grids to (sets renderMode=Locked)"),
        },
        async (args) => send("navis_set_grids", args)
    );

    server.tool(
        "navis_section",
        "Section planes / section box in the current view. action=box fits a section box around items (or min/max); get returns the clipping JSON; set applies raw clipping JSON (edit what get returns for planes); clear turns sectioning off.",
        {
            action: z.enum(["get", "set", "box", "clear"]),
            displayIds: ids,
            min: xyz.optional().describe("Box min corner (instead of items)"),
            max: xyz.optional().describe("Box max corner"),
            padding: z.number().optional().describe("Extra margin around the box, model units"),
            json: z.any().optional().describe("Clipping-plane JSON for action=set"),
        },
        async (args) => send("navis_section", args)
    );

    // ── camera ──

    server.tool(
        "navis_get_camera",
        "Get the current Navisworks camera: position, projection, field of view, model units and full camera JSON.",
        {},
        async () => send("navis_get_camera", {})
    );

    server.tool(
        "navis_set_camera",
        "Move the Navisworks camera: position, look-at target, world up, vertical field of view (radians), projection, or a full camera JSON from navis_get_camera.",
        {
            position: xyz.optional(),
            target: xyz.optional().describe("Point to look at"),
            up: xyz.optional().describe("World up vector"),
            verticalFieldOfView: z.number().optional(),
            projection: z.enum(["Perspective", "Orthographic"]).optional(),
            camera: z.any().optional().describe("Camera JSON (as returned by navis_get_camera.camera)"),
        },
        async (args) => send("navis_set_camera", args)
    );

    server.tool(
        "navis_zoom_to_items",
        "Zoom the Navisworks view to fit items (by displayIds or the current selection) and optionally select them.",
        { displayIds: ids, select: z.boolean().optional().describe("Also select the items (default true)") },
        async (args) => send("navis_zoom_to_items", args)
    );

    server.tool(
        "navis_view_preset",
        "Orient the Navisworks camera to a standard view of the whole model.",
        { preset: z.enum(["top", "bottom", "front", "back", "left", "right", "iso", "home"]) },
        async (args) => send("navis_view_preset", args)
    );

    // ── TimeLiner (4D) ──

    const taskFields = {
        name: z.string().optional(),
        displayId: z.string().optional().describe("Task ID column"),
        plannedStart: z.string().optional().describe("ISO date, e.g. 2026-03-01"),
        plannedEnd: z.string().optional(),
        actualStart: z.string().optional(),
        actualEnd: z.string().optional(),
        taskType: z.string().optional().describe("Construct / Demolish / Temporary (or a custom type)"),
        progressPercent: z.number().optional(),
        materialCost: z.number().optional(),
        laborCost: z.number().optional(),
        selectionSet: z.string().optional().describe("Attach a selection/search set"),
        displayIds: z.array(z.string()).optional().describe("Attach explicit ModelItems"),
        attachCurrentSelection: z.boolean().optional(),
    };

    server.tool(
        "navis_timeliner_list_tasks",
        "List TimeLiner (4D) tasks with dates, type, status, progress and attached items; also returns available task types.",
        { limit: z.number().optional().describe("Default 500") },
        async (args) => send("navis_timeliner_list_tasks", args)
    );

    server.tool(
        "navis_timeliner_add_task",
        "Add a TimeLiner task (optionally under a parent task) with dates, type and attached model items/sets.",
        { ...taskFields, name: z.string(), parent: z.string().optional().describe("Parent task name or ID") },
        async (args) => send("navis_timeliner_add_task", args)
    );

    server.tool(
        "navis_timeliner_update_task",
        "Edit a TimeLiner task: rename, change dates/type/progress/costs, or re-attach items.",
        { task: z.string().describe("Task name or ID"), ...taskFields },
        async (args) => send("navis_timeliner_update_task", args)
    );

    server.tool(
        "navis_timeliner_delete_task",
        "Delete a TimeLiner task (and its child tasks).",
        { task: z.string().describe("Task name or ID") },
        async (args) => send("navis_timeliner_delete_task", args)
    );

    // ── clash test authoring ──

    server.tool(
        "navis_create_clash_test",
        "Create a Clash Detective test from selection/search sets or items, with type and tolerance (model units); optionally run it.",
        {
            name: z.string(),
            selectionA: clashSelection,
            selectionB: clashSelection,
            testType: z.enum(["Hard", "HardConservative", "Clearance", "Duplicates"]).optional().describe("Default Hard"),
            tolerance: z.number().optional().describe("Model units"),
            mergeComposites: z.boolean().optional(),
            run: z.boolean().optional().describe("Run immediately"),
        },
        async (args) => send("navis_create_clash_test", args)
    );

    server.tool(
        "navis_update_clash_test",
        "Edit an existing clash test: rename, change type/tolerance, or replace selection A/B.",
        {
            test: z.string().describe("Clash test name or GUID"),
            newName: z.string().optional(),
            selectionA: clashSelection.optional(),
            selectionB: clashSelection.optional(),
            testType: z.enum(["Hard", "HardConservative", "Clearance", "Duplicates"]).optional(),
            tolerance: z.number().optional(),
            mergeComposites: z.boolean().optional(),
        },
        async (args) => send("navis_update_clash_test", args)
    );

    server.tool(
        "navis_delete_clash_test",
        "Delete a Clash Detective test and its results.",
        { test: z.string().describe("Clash test name or GUID") },
        async (args) => send("navis_delete_clash_test", args)
    );

    server.tool(
        "navis_edit_clash_results",
        "Assign clash results, set description/approved-by, or add a comment to them.",
        {
            test: z.string(),
            clashIds: z.array(z.string()),
            assignedTo: z.string().optional(),
            description: z.string().optional(),
            approvedBy: z.string().optional(),
            comment: z.string().optional(),
            author: z.string().optional(),
        },
        async (args) => send("navis_edit_clash_results", args)
    );

    // ── measurements / redlines / comments ──

    server.tool(
        "navis_measure",
        "Measure in Navisworks: distance between two items (center-to-center plus bounding-box clearance), between two points, or read the user's current Measure-tool result when no args are given.",
        {
            displayIdA: z.string().optional(),
            displayIdB: z.string().optional(),
            pointA: xyz.optional(),
            pointB: xyz.optional(),
        },
        async (args) => send("navis_measure", args)
    );

    server.tool(
        "navis_get_redlines",
        "Get the redline markups (text, lines, clouds…) on the current Navisworks view as JSON.",
        {},
        async () => send("navis_get_redlines", {})
    );

    server.tool(
        "navis_set_redlines",
        "Replace the redlines on the current view with JSON (start from navis_get_redlines output). Optionally save the view + redlines as a viewpoint.",
        {
            json: z.any().describe("Redline JSON"),
            saveViewpointAs: z.string().optional(),
        },
        async (args) => send("navis_set_redlines", args)
    );

    server.tool(
        "navis_add_comment",
        "Add a comment to a saved viewpoint, selection set, or clash result.",
        {
            target: z.enum(["viewpoint", "selectionSet", "clashResult"]),
            id: z.string().describe("Name or GUID of the viewpoint / set / clash result"),
            test: z.string().optional().describe("Clash test (required for clashResult)"),
            body: z.string(),
            status: z.enum(["New", "Active", "Approved", "Resolved"]).optional(),
            author: z.string().optional(),
        },
        async (args) => send("navis_add_comment", args)
    );

    server.tool(
        "navis_list_comments",
        "List comments on a saved viewpoint, selection set, or clash result.",
        {
            target: z.enum(["viewpoint", "selectionSet", "clashResult"]),
            id: z.string(),
            test: z.string().optional(),
        },
        async (args) => send("navis_list_comments", args)
    );

    // ── selection / search sets & properties ──

    server.tool(
        "navis_create_selection_set",
        "Save a static selection set from displayIds or the current selection.",
        { name: z.string(), displayIds: ids },
        async (args) => send("navis_create_selection_set", args)
    );

    server.tool(
        "navis_create_search_set",
        "Create a dynamic search set (Find Items). Conditions are ANDed, e.g. [{category:'Element', property:'Category', operator:'equals', value:'Ducts'}].",
        {
            name: z.string(),
            conditions: z.array(z.object({
                category: z.string().optional().describe("Property tab, e.g. Item, Element, Revit Type (default Item)"),
                property: z.string().optional().describe("Property name (default Name)"),
                operator: z.enum(["contains", "equals", "notEquals", "wildcard", "exists"]).optional(),
                value: z.string().optional(),
                ignoreCase: z.boolean().optional(),
            })).min(1),
        },
        async (args) => send("navis_create_search_set", args)
    );

    server.tool(
        "navis_manage_selection_set",
        "Delete, rename, select, or list the items of a selection/search set.",
        {
            set: z.string().describe("Set name or GUID"),
            action: z.enum(["delete", "rename", "select", "items"]),
            newName: z.string().optional(),
            limit: z.number().optional(),
        },
        async (args) => send("navis_manage_selection_set", args)
    );

    server.tool(
        "navis_get_item_properties",
        "Read all property tabs/values of items (Selection Inspector / Properties window).",
        {
            displayIds: ids,
            category: z.string().optional().describe("Only this property tab"),
            limit: z.number().optional().describe("Max items (default 20)"),
        },
        async (args) => send("navis_get_item_properties", args)
    );

    server.tool(
        "navis_set_user_properties",
        "Add or edit user-defined item properties on a custom tab (keeps other keys; a null value deletes a key). remove=true drops the whole tab.",
        {
            displayIds: ids,
            tabName: z.string().optional().describe("Default 'BIM-Bot'"),
            properties: z.record(z.union([z.string(), z.number(), z.boolean(), z.null()])).optional(),
            remove: z.boolean().optional(),
        },
        async (args) => send("navis_set_user_properties", args)
    );

    // ── code ──

    server.tool(
        "navis_execute_code",
        "Compile and run a C# method body inside Navisworks (C# 5 syntax, .NET Framework). Variable 'doc' is the active Autodesk.Navisworks.Api.Document; usings for Api, Clash and Timeliner are included. 'return' a value to get it back. Use for anything the other navis_* tools don't cover.",
        {
            code: z.string().describe("C# statements, e.g. return doc.Models.Count;"),
            description: z.string().optional(),
        },
        async (args) => send("navis_execute_code", args)
    );
}
