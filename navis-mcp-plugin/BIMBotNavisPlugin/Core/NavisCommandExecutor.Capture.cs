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

            using (var bmp = doc.ActiveView.GenerateImage(ImageGenerationStyle.Scene, w, h, true))
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