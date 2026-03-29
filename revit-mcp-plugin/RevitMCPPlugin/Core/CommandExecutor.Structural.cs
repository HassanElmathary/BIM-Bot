using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Structural tool implementations: beams, columns, foundations, rebar, analytical model.
    /// </summary>
    public static partial class CommandExecutor
    {
        private static JToken CreateStructuralBeam(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            var beamType = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault();
            if (beamType == null) return new JObject { ["error"] = "No beam type found" };
            using (var tx = new Transaction(doc, "Create Beam"))
            {
                tx.Start(); if (!beamType.IsActive) beamType.Activate();
                var line = Line.CreateBound(new XYZ(parameters["startX"]?.Value<double>() ?? 0, parameters["startY"]?.Value<double>() ?? 0, level.Elevation), new XYZ(parameters["endX"]?.Value<double>() ?? 0, parameters["endY"]?.Value<double>() ?? 0, level.Elevation));
                var beam = doc.Create.NewFamilyInstance(line, beamType, level, StructuralType.Beam);
                tx.Commit();
                return new JObject { ["message"] = $"🏗️ Created beam (ID: {beam.Id.Value})", ["elementId"] = beam.Id.Value };
            }
        }

        private static JToken CreateStructuralColumn(Document doc, JObject parameters)
        {
            var baseLevelName = parameters["baseLevelName"]?.ToString();
            var baseLevel = FindLevel(doc, baseLevelName);
            if (baseLevel == null) return new JObject { ["error"] = $"Level '{baseLevelName}' not found" };
            var colType = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault();
            if (colType == null) return new JObject { ["error"] = "No column type found" };
            using (var tx = new Transaction(doc, "Create Column"))
            {
                tx.Start(); if (!colType.IsActive) colType.Activate();
                var col = doc.Create.NewFamilyInstance(new XYZ(parameters["x"]?.Value<double>() ?? 0, parameters["y"]?.Value<double>() ?? 0, baseLevel.Elevation), colType, baseLevel, StructuralType.Column);
                var topLevelName = parameters["topLevelName"]?.ToString();
                if (!string.IsNullOrEmpty(topLevelName)) { var tl = FindLevel(doc, topLevelName); if (tl != null) col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(tl.Id); }
                tx.Commit();
                return new JObject { ["message"] = $"🏗️ Created column (ID: {col.Id.Value})", ["elementId"] = col.Id.Value };
            }
        }

        private static JToken CreateWallFoundation(Document doc, JObject parameters)
        {
            var wallId = parameters["wallId"]?.Value<int>() ?? 0;
            var wall = doc.GetElement(new ElementId(wallId)) as Wall;
            if (wall == null) return new JObject { ["error"] = $"Wall {wallId} not found" };
            var foundTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().ToList();
            return new JObject { ["message"] = $"🏗️ Wall foundation: {foundTypes.Count} types available for wall {wallId}", ["types"] = JArray.FromObject(foundTypes.Select(t => new { t.Name, id = t.Id.Value })), ["hint"] = "Use execute_code: doc.Create.NewFamilyInstance(curve, foundationType, wall, level, StructuralType.Footing)" };
        }

        private static JToken CreateRebar(Document doc, JObject parameters)
        {
            var hostId = parameters["hostId"]?.Value<int>() ?? 0;
            var host = doc.GetElement(new ElementId(hostId));
            if (host == null) return new JObject { ["error"] = $"Host element {hostId} not found" };
            var barTypes = new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>().ToList();
            return new JObject { ["message"] = $"🏗️ {barTypes.Count} rebar types available for '{host.Name}'", ["barTypes"] = JArray.FromObject(barTypes.Select(b => new { b.Name, id = b.Id.Value })), ["hint"] = "Use execute_code: Rebar.CreateFromCurves(doc, rebarStyle, barType, hookType, hookType, host, normal, curves, hookOrient, hookOrient, useExistingShape, createNewShape)" };
        }

        private static JToken GetStructuralElements(Document doc, JObject parameters)
        {
            var cat = parameters["category"]?.ToString() ?? "StructuralFraming";
            BuiltInCategory bic = cat == "StructuralColumns" ? BuiltInCategory.OST_StructuralColumns : cat == "StructuralFoundation" ? BuiltInCategory.OST_StructuralFoundation : BuiltInCategory.OST_StructuralFraming;
            var elements = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToList();
            var items = new JArray();
            foreach (var e in elements.Take(100))
                items.Add(new JObject { ["id"] = e.Id.Value, ["name"] = e.Name, ["family"] = (e as FamilyInstance)?.Symbol?.Family?.Name });
            return new JObject { ["message"] = $"🏗️ {elements.Count} {cat} elements", ["elements"] = items };
        }

        private static JToken AnalyticalModelInfo(Document doc, JObject parameters)
        {
            var idsStr = parameters["elementIds"]?.ToString();
            var items = new JArray();
            if (!string.IsNullOrEmpty(idsStr))
                foreach (var idStr in idsStr.Split(','))
                {
                    var elem = doc.GetElement(new ElementId(int.Parse(idStr.Trim())));
                    if (elem == null) continue;
                    var item = new JObject { ["id"] = elem.Id.Value, ["name"] = elem.Name, ["category"] = elem.Category?.Name };
                    var bb = elem.get_BoundingBox(null);
                    if (bb != null) item["centroid"] = new JObject { ["x"] = Math.Round((bb.Min.X + bb.Max.X) / 2, 4), ["y"] = Math.Round((bb.Min.Y + bb.Max.Y) / 2, 4), ["z"] = Math.Round((bb.Min.Z + bb.Max.Z) / 2, 4) };
                    items.Add(item);
                }
            return new JObject { ["message"] = $"📊 Analytical info for {items.Count} elements", ["elements"] = items };
        }
    }
}
