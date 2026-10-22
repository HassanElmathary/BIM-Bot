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
        private static readonly Autodesk.Navisworks.Api.Color Red = new Autodesk.Navisworks.Api.Color(1, 0, 0);
        private static readonly Autodesk.Navisworks.Api.Color Green = new Autodesk.Navisworks.Api.Color(0, 0.75, 0);
        private static readonly Autodesk.Navisworks.Api.Color Orange = new Autodesk.Navisworks.Api.Color(1, 0.5, 0);

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
                var ids = Ids(p);
                var items = ids.Count > 0 ? ResolveItems(doc, ids) : new List<ModelItem>();
                if (items.Count > 0)
                {
                    if (Bool(p, "select", true))
                    {
                        var coll = new ModelItemCollection();
                        coll.AddRange(items);
                        doc.CurrentSelection.CopyFrom(coll);
                    }
                    var vp = doc.CurrentViewpoint.CreateCopy();
                    vp.ZoomBox(ItemsBox(items));
                    ApplyViewpoint(doc, vp);
                    moved = true;
                }
                var shot = items.Count > 0
                    ? Highlighted(doc, items.Select(i => (i, Orange)).ToList(), p)
                    : Snapshot(doc, p);
                if (items.Count > 0) shot["items"] = new JArray(items.Select(i => (JToken)ItemJson(i)));
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
            vp.ZoomBox(ClashZoomBox(r, items));
            ApplyViewpoint(doc, vp);

            var painted = new List<(ModelItem item, Autodesk.Navisworks.Api.Color color)>();
            if (r.Item1 != null) painted.Add((r.Item1, Red));
            if (r.Item2 != null) painted.Add((r.Item2, Green));
            if (p["seeThroughSecond"] == null) p["seeThroughSecond"] = true;
            var shot = Highlighted(doc, painted, p);
            shot["test"] = test.DisplayName ?? "";
            shot["clash"] = ResultJson(r);
            shot["item1"] = r.Item1 != null ? (JToken)ItemJson(r.Item1) : JValue.CreateNull();
            shot["item2"] = r.Item2 != null ? (JToken)ItemJson(r.Item2) : JValue.CreateNull();
            return shot;
        }

        /// <summary>
        /// Captures with each item painted its colour (highlight, default true). context decides what
        /// happens to the rest of the model so walls in front don't hide the items:
        ///   "isolate" (default) — temporarily hide everything except the items (fast);
        ///   "fade" — make everything else see-through (shows surroundings, slow on big models);
        ///   "none" — leave the rest as is.
        /// Colours are temporary overrides and hidden items are unhidden afterwards, so the user's
        /// own colours and hide state are untouched.
        /// </summary>
        private static JObject Highlighted(Document doc, List<(ModelItem item, Autodesk.Navisworks.Api.Color color)> painted, JObject p)
        {
            bool highlight = Bool(p, "highlight", true);
            var context = Str(p, "context", "isolate").Trim().ToLowerInvariant();
            if (!highlight || painted.Count == 0) return Snapshot(doc, p);
            var keep = painted.Select(x => x.item).ToList();
            var touched = new List<ModelItem>();
            var hidden = new List<ModelItem>();
            try
            {
                if (context == "fade")
                {
                    var roots = doc.Models.RootItems.ToList();
                    doc.Models.OverrideTemporaryTransparency(roots, 0.85);
                    touched.AddRange(roots);
                }
                else if (context == "isolate")
                {
                    hidden = OthersAlongPaths(doc, keep);
                    if (hidden.Count > 0) doc.Models.SetHidden(hidden, true);
                }
                foreach (var (item, color) in painted) Paint(doc, item, color, touched);
                if (painted.Count == 2 && Bool(p, "seeThroughSecond", false))
                    doc.Models.OverrideTemporaryTransparency(new[] { painted[1].item }, 0.5);
                var shot = Snapshot(doc, p);
                shot["context"] = context;
                return shot;
            }
            finally
            {
                if (hidden.Count > 0) doc.Models.SetHidden(hidden, false);
                if (touched.Count > 0) doc.Models.ResetTemporaryMaterials(touched);
            }
        }

        /// <summary>
        /// The smallest set of currently visible items whose hiding leaves only the kept items
        /// visible: every sibling off the path from each model root down to a kept item.
        /// </summary>
        private static List<ModelItem> OthersAlongPaths(Document doc, List<ModelItem> keep)
        {
            var onPath = new HashSet<ModelItem>();
            foreach (var k in keep)
                for (var cur = k; cur != null; cur = cur.Parent) onPath.Add(cur);
            var keepSet = new HashSet<ModelItem>(keep);
            var result = new List<ModelItem>();

            void Visit(ModelItem node)
            {
                if (keepSet.Contains(node)) return;           // keep it and everything under it
                foreach (ModelItem child in node.Children)
                {
                    if (onPath.Contains(child)) Visit(child);
                    else if (!child.IsHidden) result.Add(child);
                }
            }

            foreach (ModelItem root in doc.Models.RootItems)
            {
                if (onPath.Contains(root)) Visit(root);
                else if (!root.IsHidden) result.Add(root);
            }
            return result;
        }

        /// <summary>
        /// Box to zoom to for a clash: Clash Detective's own ViewBounds, else the clash's
        /// BoundingBox, else the box around both items (old behaviour).
        /// </summary>
        private static BoundingBox3D ClashZoomBox(ClashResult r, List<ModelItem> items)
        {
            try
            {
                var vb = r.ViewBounds;
                if (vb != null && !vb.IsEmpty) return vb;
            }
            catch { }
            try
            {
                var bb = r.BoundingBox;
                if (bb != null && !bb.IsEmpty) return bb;
            }
            catch { }
            return ItemsBox(items);
        }

        private static void Paint(Document doc, ModelItem item, Autodesk.Navisworks.Api.Color color, List<ModelItem> touched)
        {
            var one = new[] { item };
            doc.Models.OverrideTemporaryTransparency(one, 0);
            doc.Models.OverrideTemporaryColor(one, color);
            touched.Add(item);
        }

        /// <summary>Renders the active view. width 320–2560 (default 1280), height 240–1600 (default 720), format jpeg|png.</summary>
        private static JObject Snapshot(Document doc, JObject p)
        {
            int w = Math.Max(320, Math.Min(2560, Int(p, "width", 1280)));
            int h = Math.Max(240, Math.Min(1600, Int(p, "height", 720)));
            bool png = Str(p, "format", "jpeg").Equals("png", StringComparison.OrdinalIgnoreCase);

            using (var bmp = RenderView(doc.ActiveView, w, h))
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