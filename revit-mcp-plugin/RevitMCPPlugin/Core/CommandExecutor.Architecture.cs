using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Architecture tool implementations: stairs, railings, curtain walls, shaft openings,
    /// wall openings, curtain panel queries.
    /// </summary>
    public static partial class CommandExecutor
    {
        private static JToken CreateStairs(Document doc, JObject parameters)
        {
            var baseLevel = FindLevel(doc, parameters["baseLevelName"]?.ToString());
            var topLevel = FindLevel(doc, parameters["topLevelName"]?.ToString());
            if (baseLevel == null || topLevel == null) return new JObject { ["error"] = "Base or top level not found" };
            var stairTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Stairs).OfClass(typeof(ElementType)).ToList();
            return new JObject { ["message"] = $"🪜 Stairs: {baseLevel.Name} → {topLevel.Name} (height: {Math.Round(topLevel.Elevation - baseLevel.Elevation, 2)}ft)", ["types"] = JArray.FromObject(stairTypes.Select(t => new { t.Name, id = t.Id.Value })), ["hint"] = "Use execute_code: StairsEditScope + StairsRun.CreateStraightRun()" };
        }

        private static JToken CreateRailing(Document doc, JObject parameters)
        {
            var railTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing).OfClass(typeof(ElementType)).ToList();
            return new JObject { ["message"] = $"🛡️ {railTypes.Count} railing types available", ["types"] = JArray.FromObject(railTypes.Select(t => new { t.Name, id = t.Id.Value })), ["hint"] = "Use execute_code: Railing.Create(doc, curveLoop, railingTypeId, levelId)" };
        }

        private static JToken CreateCurtainWall(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            var wallType = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault(wt => wt.Kind == WallKind.Curtain);
            if (wallType == null) return new JObject { ["error"] = "No curtain wall type found" };
            using (var tx = new Transaction(doc, "Create Curtain Wall"))
            {
                tx.Start();
                var line = Line.CreateBound(new XYZ(parameters["startX"]?.Value<double>() ?? 0, parameters["startY"]?.Value<double>() ?? 0, level.Elevation), new XYZ(parameters["endX"]?.Value<double>() ?? 0, parameters["endY"]?.Value<double>() ?? 0, level.Elevation));
                var wall = Wall.Create(doc, line, wallType.Id, level.Id, parameters["height"]?.Value<double>() ?? 15, 0, false, false);
                tx.Commit();
                return new JObject { ["message"] = $"🏗️ Created curtain wall (ID: {wall.Id.Value})", ["elementId"] = wall.Id.Value };
            }
        }

        private static JToken CreateShaftOpening(Document doc, JObject parameters)
        {
            var baseLevel = FindLevel(doc, parameters["baseLevelName"]?.ToString());
            var topLevel = FindLevel(doc, parameters["topLevelName"]?.ToString());
            if (baseLevel == null || topLevel == null) return new JObject { ["error"] = "Levels not found" };
            var pointsArr = parameters["points"] as JArray;
            if (pointsArr == null || pointsArr.Count < 3) return new JObject { ["error"] = "Need at least 3 points" };
            var pts = pointsArr.Select(p => new XYZ(p["x"]?.Value<double>() ?? 0, p["y"]?.Value<double>() ?? 0, baseLevel.Elevation)).ToList();
            var curveArr = new CurveArray();
            for (int i = 0; i < pts.Count; i++) curveArr.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
            using (var tx = new Transaction(doc, "Create Shaft"))
            {
                tx.Start();
                var opening = doc.Create.NewOpening(baseLevel, topLevel, curveArr);
                tx.Commit();
                return new JObject { ["message"] = $"🕳️ Created shaft opening (ID: {opening.Id.Value})", ["elementId"] = opening.Id.Value };
            }
        }

        private static JToken GetStairsInfo(Document doc, JObject parameters)
        {
            var stairsIdParam = parameters["stairsId"]?.Value<int>();
            var stairs = stairsIdParam.HasValue ? new[] { doc.GetElement(new ElementId(stairsIdParam.Value)) }.Where(e => e != null).ToList() : new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Stairs).WhereElementIsNotElementType().ToList();
            var items = new JArray();
            foreach (var s in stairs)
            {
                var item = new JObject { ["id"] = s.Id.Value, ["name"] = s.Name };
                var rh = s.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_RISER_HEIGHT); if (rh != null) item["riserHeight_ft"] = Math.Round(rh.AsDouble(), 4);
                var td = s.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_TREAD_DEPTH); if (td != null) item["treadDepth_ft"] = Math.Round(td.AsDouble(), 4);
                var nr = s.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_NUM_RISERS); if (nr != null) item["numRisers"] = nr.AsInteger();
                items.Add(item);
            }
            return new JObject { ["message"] = $"🪜 {stairs.Count} stairs", ["stairs"] = items };
        }

        private static JToken GetCurtainPanels(Document doc, JObject parameters)
        {
            var wall = doc.GetElement(new ElementId(parameters["wallId"]?.Value<int>() ?? 0)) as Wall;
            if (wall == null) return new JObject { ["error"] = "Wall not found" };
            var cg = wall.CurtainGrid;
            if (cg == null) return new JObject { ["error"] = "Not a curtain wall" };
            var panels = new JArray(); foreach (var pId in cg.GetPanelIds()) { var p = doc.GetElement(pId); panels.Add(new JObject { ["id"] = p.Id.Value, ["name"] = p?.Name }); }
            var mullions = new JArray(); foreach (var mId in cg.GetMullionIds()) { var m = doc.GetElement(mId); mullions.Add(new JObject { ["id"] = m.Id.Value, ["name"] = m?.Name }); }
            return new JObject { ["message"] = $"🏗️ {panels.Count} panels, {mullions.Count} mullions", ["panels"] = panels, ["mullions"] = mullions };
        }

        private static JToken CreateOpeningInWall(Document doc, JObject parameters)
        {
            var wall = doc.GetElement(new ElementId(parameters["wallId"]?.Value<int>() ?? 0)) as Wall;
            if (wall == null) return new JObject { ["error"] = "Wall not found" };
            using (var tx = new Transaction(doc, "Create Opening"))
            {
                tx.Start();
                var opening = doc.Create.NewOpening(wall, new XYZ(parameters["x1"]?.Value<double>() ?? 0, parameters["y1"]?.Value<double>() ?? 0, 0), new XYZ(parameters["x2"]?.Value<double>() ?? 0, parameters["y2"]?.Value<double>() ?? 0, 0));
                tx.Commit();
                return new JObject { ["message"] = $"🕳️ Created wall opening (ID: {opening.Id.Value})", ["elementId"] = opening.Id.Value };
            }
        }
    }
}
