IMPLEMENTATION PLAN — Step 1: Let Claude SEE the Navisworks view (image capture)
Audience: coding agent (Nemotron 3 Ultra in OpenCode). Follow literally. Do not improvise outside the listed files.
Repo root: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP
Branch: feat/navis-1-view-capture

======================================================================
0. BEFORE YOU START
======================================================================
1. Run: git switch feat/navis-1-view-capture
2. Run: git branch --show-current   -> must print feat/navis-1-view-capture
3. Run: git status --short          -> must be empty. If not empty, STOP and report.

======================================================================
1. GOAL
======================================================================
Today every navis_* tool returns text only, so Claude works blind. Add two tools that return
a real image of the Navisworks 3D view as MCP image content, so Claude can look at it:

  navis_capture_view   - capture the current view, optionally after applying a saved
                         viewpoint or zooming to displayIds.
  navis_capture_clash  - zoom to one clash result (both clashing items selected), capture it,
                         and return the image plus both items' details.

======================================================================
2. HARD RULES
======================================================================
1. Only create/edit these files:
   - CREATE navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Capture.cs
   - EDIT   navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs
            (ONLY add two `case` lines to the switch in ExecuteExtended)
   - CREATE revit-mcp-server/src/tools/navis_capture_tools.ts
2. Do not touch any other file, any .csproj, or any Revit file.
3. No new NuGet or npm packages (System.Drawing is already referenced by the .csproj).
4. Match the existing style: 4-space indent; C# partial class NavisCommandExecutor in namespace
   BIMBotNavisPlugin.Core; TS double quotes and ".js" suffix on relative imports.
5. Do NOT deploy the DLL to C:\Program Files. Do NOT commit or push. Do not run git
   reset / checkout -- <file> / stash / clean.
6. The helpers you need ALREADY EXIST in the partial class — reuse them, do not redefine them:
   ActiveDoc(), Str(), Int(), Bool(), FindViewpoint(doc, name), ZoomToItems(JObject),
   ItemsBox(IEnumerable<ModelItem>), ApplyViewpoint(doc, vp), ClashData(doc), FindTest(data, name),
   CollectResults(items, list), ResultJson(ClashResult), ItemJson(ModelItem).

======================================================================
3. TASKS
======================================================================

----- TASK 1 — Create NavisCommandExecutor.Capture.cs with exactly this content -----

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// View capture: renders the active Navisworks 3D view to an image and returns it as
    /// base64 so the MCP server can hand it to the AI as image content.
    /// </summary>
    public static partial class NavisCommandExecutor
    {
        private static JObject CaptureView(JObject p)
        {
            var doc = ActiveDoc();
            if (doc.ActiveView == null) throw new Exception("No active 3D view to capture.");
            var before = doc.CurrentViewpoint.CreateCopy();
            bool moved = false;
            try
            {
                var vpName = Str(p, "viewpoint");
                if (!string.IsNullOrWhiteSpace(vpName))
                {
                    doc.SavedViewpoints.CurrentSavedViewpoint = FindViewpoint(doc, vpName);
                    moved = true;
                }
                if (p["displayIds"] is JArray ids && ids.Count > 0)
                {
                    ZoomToItems(new JObject { ["displayIds"] = ids, ["select"] = Bool(p, "select", true) });
                    moved = true;
                }
                var shot = Snapshot(doc, p);
                if (!string.IsNullOrWhiteSpace(vpName)) shot["viewpoint"] = vpName;
                return shot;
            }
            finally
            {
                if (moved && Bool(p, "restore", false)) ApplyViewpoint(doc, before);
            }
        }

        private static JObject CaptureClash(JObject p)
        {
            var doc = ActiveDoc();
            if (doc.ActiveView == null) throw new Exception("No active 3D view to capture.");
            var data = ClashData(doc);
            var test = FindTest(data, Str(p, "test"));
            var id = Str(p, "clashId");
            if (string.IsNullOrWhiteSpace(id)) throw new Exception("clashId is required (from navis_get_clash_results).");
            var all = new List<ClashResult>();
            CollectResults(test.Children, all);
            var r = all.FirstOrDefault(x =>
                        string.Equals(x.Guid.ToString(), id, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.DisplayName, id, StringComparison.OrdinalIgnoreCase))
                    ?? throw new Exception($"Clash '{id}' not found in test '{test.DisplayName}'.");

            var items = new List<ModelItem>();
            if (r.Item1 != null) items.Add(r.Item1);
            if (r.Item2 != null) items.Add(r.Item2);
            if (items.Count == 0) throw new Exception("This clash result has no items.");

            var coll = new ModelItemCollection();
            coll.AddRange(items);
            doc.CurrentSelection.CopyFrom(coll);
            var vp = doc.CurrentViewpoint.CreateCopy();
            vp.ZoomBox(ItemsBox(items));
            ApplyViewpoint(doc, vp);

            var shot = Snapshot(doc, p);
            shot["test"] = test.DisplayName ?? "";
            shot["clash"] = ResultJson(r);
            shot["item1"] = r.Item1 != null ? (JToken)ItemJson(r.Item1) : JValue.CreateNull();
            shot["item2"] = r.Item2 != null ? (JToken)ItemJson(r.Item2) : JValue.CreateNull();
            return shot;
        }

        /// <summary>Renders the active view. width 320–2560 (default 1280), height 240–1600 (default 720), format jpeg|png.</summary>
        private static JObject Snapshot(Document doc, JObject p)
        {
            int w = Math.Max(320, Math.Min(2560, Int(p, "width", 1280)));
            int h = Math.Max(240, Math.Min(1600, Int(p, "height", 720)));
            bool png = Str(p, "format", "jpeg").Equals("png", StringComparison.OrdinalIgnoreCase);

            using (var bmp = doc.ActiveView.GenerateImage(ImageGenerationStyle.Scene, w, h))
            {
                if (bmp == null) throw new Exception("Navisworks returned no image (is a 3D view open and visible?).");
                using (var ms = new MemoryStream())
                {
                    if (png)
                    {
                        bmp.Save(ms, ImageFormat.Png);
                    }
                    else
                    {
                        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                        using (var ep = new EncoderParameters(1))
                        {
                            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
                            bmp.Save(ms, codec, ep);
                        }
                    }
                    return new JObject
                    {
                        ["mimeType"] = png ? "image/png" : "image/jpeg",
                        ["width"] = bmp.Width,
                        ["height"] = bmp.Height,
                        ["bytes"] = ms.Length,
                        ["base64"] = Convert.ToBase64String(ms.ToArray()),
                    };
                }
            }
        }
    }
}

Compile notes (change only what the note says):
- If `ZoomToItems` reads a different key than "displayIds", open ItemsOrSelection() in
  NavisCommandExecutor.Extended.cs, see which key it reads, and use that key in CaptureView.
- If `View.GenerateImage(ImageGenerationStyle, int, int)` does not compile against the Navisworks
  2024 API, STOP and report the exact compiler error. Do not invent another API.
- If `Encoder` / `Image` names are ambiguous, fully qualify them with System.Drawing.Imaging / System.Drawing.

----- TASK 2 — Register the commands -----
In NavisCommandExecutor.Extended.cs, inside `switch (method)` of `ExecuteExtended`, add these two
lines right after the `case "navis_view_preset": ...` line:

                case "navis_capture_view": return CaptureView(p);
                case "navis_capture_clash": return CaptureClash(p);

Change nothing else in that file.

----- TASK 3 — Create revit-mcp-server/src/tools/navis_capture_tools.ts with exactly this content -----

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
            viewpoint: z.string().optional().describe("Saved viewpoint name or GUID to apply before capturing"),
            displayIds: z.array(z.string()).optional().describe("Zoom to these items before capturing"),
            select: z.boolean().optional().describe("Select the displayIds items so they are highlighted (default true)"),
            restore: z.boolean().optional().describe("Put the camera back after capturing (default false)"),
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
        "Zoom to one clash result (both clashing items selected and highlighted), capture an image of it, " +
        "and return the image plus both items' details (displayId, name, revitElementId, revitCategory). " +
        "Get test names from navis_list_clash_tests and clashIds from navis_get_clash_results.",
        {
            test: z.string().describe("Clash test name or GUID"),
            clashId: z.string().describe("Clash result GUID (id from navis_get_clash_results) or its name"),
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

(The server auto-registers every *_tools.ts file in src/tools that exports a register* function —
no other wiring is needed.)

======================================================================
4. BUILD (both must pass with 0 errors)
======================================================================
  cd revit-mcp-server && npm run build
  dotnet build navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.csproj -c Release

If a build fails for a reason not covered by the compile notes, STOP and report the exact error.

======================================================================
5. SELF-CHECKS
======================================================================
  A. git status --short
     Expect exactly: the 2 new files (??) and 1 modified file (M NavisCommandExecutor.Extended.cs).
  B. git diff navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs
     Expect: exactly 2 added lines, nothing removed.
  C. grep -n "registerNavisCaptureTools" revit-mcp-server/build/tools/navis_capture_tools.js
     Expect: one match.

(The reviewer will deploy and test live: navis_capture_view returns a visible image of the model;
navis_capture_clash zooms to a clash and returns an image with both items.)

======================================================================
6. REPORT BACK
======================================================================
1. Files created/changed.
2. Last ~15 lines of each build.
3. Output of self-checks A, B, C.
4. Anything you could not do exactly as written, and why.
