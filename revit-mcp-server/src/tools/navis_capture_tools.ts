import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withNavisConnection } from "../utils/navisConnectionManager.js";

/**
 * Navisworks view capture — returns the 3D view as MCP image content so the AI can see it.
 * Routes to navis_capture_view / navis_capture_clash in the Navisworks plugin (port 8091).
 */

interface Shot {
    mimeType: string;
    base64: string;
    [key: string]: unknown;
}

function toContent(raw: unknown) {
    const shot = raw as Shot;
    if (!shot || typeof shot.base64 !== "string" || !shot.base64) {
        return { content: [{ type: "text" as const, text: `Failed: Navisworks returned no image. Raw: ${JSON.stringify(raw).slice(0, 500)}` }] };
    }
    const { base64, ...meta } = shot;
    return {
        content: [
            { type: "image" as const, data: base64, mimeType: shot.mimeType || "image/jpeg" },
            { type: "text" as const, text: JSON.stringify(meta, null, 2) },
        ],
    };
}

const sizeArgs = {
    width: z.number().optional().describe("Image width in pixels, 320–2560 (default 1280)"),
    height: z.number().optional().describe("Image height in pixels, 240–1600 (default 720)"),
    format: z.enum(["jpeg", "png"]).optional().describe("jpeg (default, smaller) or png"),
};

export function registerNavisCaptureTools(server: McpServer) {

    server.tool(
        "navis_capture_view",
        "Capture an image of the Navisworks 3D view so you can SEE the model. Optionally apply a saved " +
        "viewpoint first, or zoom to displayIds (from navis_search_items) first. Use it to check what a " +
        "clash, section, color override or hide actually looks like. The view stays where it was moved " +
        "unless restore=true.",
        {
            viewpoint: z.string().optional().describe("Saved viewpoint to apply first: its ref from navis_viewpoints_list (e.g. 'vp:12', preferred — names can repeat), or its name or GUID"),
            displayIds: z.array(z.string()).optional().describe("Zoom to these items first; they are painted orange and, by default, everything else is hidden for the capture (see context)"),
            select: z.boolean().optional().describe("Also select the items in Navisworks (default true). Selection is not drawn in the image — the orange colour is."),
            restore: z.boolean().optional().describe("Put the camera back after capturing (default false)"),
            highlight: z.boolean().optional().describe("Paint the displayIds items orange in the image (default true; reset after capture)"),
            context: z.enum(["isolate", "fade", "none"]).optional().describe(
                "With displayIds: isolate (default, fast) = show only those items; fade = everything else see-through " +
                "(slow on big models); none = leave as is. Restored after capture."),
            ...sizeArgs,
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_capture_view", args));
                return toContent(response);
            } catch (e) { return { content: [{ type: "text" as const, text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_capture_clash",
        "Zoom to one clash result, capture an image with item1 in red and item2 in green, with the rest of " +
        "the model hidden by default so walls don't block the view (colours and visibility are restored afterwards), " +
        "and return the image plus both items' details (displayId, name, revitElementId, revitCategory). " +
        "Get test names from navis_list_clash_tests and clashIds from navis_get_clash_results.",
        {
            test: z.string().describe("Clash test name or GUID"),
            clashId: z.string().describe("Clash result GUID (id from navis_get_clash_results) or its name"),
            highlight: z.boolean().optional().describe("Colour item1 red and item2 green in the image (default true; reset after capture)"),
            context: z.enum(["isolate", "fade", "none"]).optional().describe(
                "What to do with the rest of the model so nothing blocks the clash: isolate (default, fast) = show only " +
                "the two items; fade = everything else see-through (shows surroundings, can take a minute on big models); " +
                "none = leave as is. Restored after capture."),
            ...sizeArgs,
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_capture_clash", args));
                return toContent(response);
            } catch (e) { return { content: [{ type: "text" as const, text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );
}