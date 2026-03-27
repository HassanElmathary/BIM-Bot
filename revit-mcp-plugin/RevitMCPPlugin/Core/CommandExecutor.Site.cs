using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Site tool implementations: topography, building pads, site info queries.
    /// </summary>
    public static partial class CommandExecutor
    {
        private static JToken CreateTopography(Document doc, JObject parameters)
        {
            var pointsArr = parameters["points"] as JArray;
            if (pointsArr == null || pointsArr.Count < 3) return new JObject { ["error"] = "Need at least 3 points" };
            var pts = pointsArr.Select(p => new XYZ(p["x"]?.Value<double>() ?? 0, p["y"]?.Value<double>() ?? 0, p["z"]?.Value<double>() ?? 0)).ToList();
            using (var tx = new Transaction(doc, "Create Topography"))
            {
                tx.Start();
#pragma warning disable CS0618
                var topo = TopographySurface.Create(doc, pts);
#pragma warning restore CS0618
                tx.Commit();
                return new JObject { ["message"] = $"🏔️ Created topography ({pts.Count} points, ID: {topo.Id.IntegerValue})", ["elementId"] = topo.Id.IntegerValue };
            }
        }

        private static JToken CreateBuildingPad(Document doc, JObject parameters)
        {
            var level = FindLevel(doc, parameters["levelName"]?.ToString());
            if (level == null) return new JObject { ["error"] = "Level not found" };
            var pointsArr = parameters["points"] as JArray;
            if (pointsArr == null || pointsArr.Count < 3) return new JObject { ["error"] = "Need at least 3 points" };
            var padType = new FilteredElementCollector(doc).OfClass(typeof(BuildingPadType)).FirstOrDefault() as BuildingPadType;
            if (padType == null) return new JObject { ["error"] = "No building pad type found" };
            var pts = pointsArr.Select(p => new XYZ(p["x"]?.Value<double>() ?? 0, p["y"]?.Value<double>() ?? 0, level.Elevation)).ToList();
            var loop = new CurveLoop(); for (int i = 0; i < pts.Count; i++) loop.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
            using (var tx = new Transaction(doc, "Create Building Pad"))
            {
                tx.Start();
                var pad = BuildingPad.Create(doc, padType.Id, level.Id, new List<CurveLoop> { loop });
                tx.Commit();
                return new JObject { ["message"] = $"🏗️ Created building pad (ID: {pad.Id.IntegerValue})", ["elementId"] = pad.Id.IntegerValue };
            }
        }

        private static JToken GetSiteInfo(Document doc, JObject parameters)
        {
#pragma warning disable CS0618
            var topos = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Topography).WhereElementIsNotElementType().ToList();
#pragma warning restore CS0618
            var pads = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_BuildingPad).WhereElementIsNotElementType().ToList();
            return new JObject { ["message"] = $"🏔️ Site: {topos.Count} topo surfaces, {pads.Count} building pads", ["topography"] = JArray.FromObject(topos.Select(t => new { id = t.Id.IntegerValue, t.Name })), ["buildingPads"] = JArray.FromObject(pads.Select(p => new { id = p.Id.IntegerValue, p.Name })) };
        }
    }
}
