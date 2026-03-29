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
            return new JObject { ["error"] = "TopographySurface creation is obsolete in Revit 2024+. Use Toposolid instead." };
        }

        private static JToken CreateBuildingPad(Document doc, JObject parameters)
        {
            return new JObject { ["error"] = "BuildingPad creation is obsolete in Revit 2024+. Use Toposolid instead." };
        }

        private static JToken GetSiteInfo(Document doc, JObject parameters)
        {
#pragma warning disable CS0618
            var topos = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Topography).WhereElementIsNotElementType().ToList();
#pragma warning restore CS0618
            var pads = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_BuildingPad).WhereElementIsNotElementType().ToList();
            return new JObject { ["message"] = $"🏔️ Site: {topos.Count} topo surfaces, {pads.Count} building pads", ["topography"] = JArray.FromObject(topos.Select(t => new { id = t.Id.Value, t.Name })), ["buildingPads"] = JArray.FromObject(pads.Select(p => new { id = p.Id.Value, p.Name })) };
        }
    }
}
