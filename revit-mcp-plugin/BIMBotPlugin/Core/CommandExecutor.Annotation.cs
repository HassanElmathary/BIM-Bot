using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Annotation tool implementations: filled regions, spot elevations/coords, keynote legends,
    /// detail components, room tagging, wall dimensioning.
    /// </summary>
    public static partial class CommandExecutor
    {
        private static JToken CreateFilledRegion(Document doc, UIDocument uidoc, JObject parameters)
        {
            var pointsArr = parameters["points"] as JArray;
            var viewIdParam = parameters["viewId"]?.Value<int>();
            var view = viewIdParam.HasValue ? doc.GetElement(new ElementId(viewIdParam.Value)) as View : uidoc.ActiveView;
            var regionType = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).FirstOrDefault() as FilledRegionType;
            if (regionType == null) return new JObject { ["error"] = "No filled region type found" };
            if (pointsArr == null || pointsArr.Count < 3) return new JObject { ["error"] = "Need at least 3 points" };
            var loop = new CurveLoop();
            var pts = pointsArr.Select(p => new XYZ(p["x"]?.Value<double>() ?? 0, p["y"]?.Value<double>() ?? 0, 0)).ToList();
            for (int i = 0; i < pts.Count; i++) loop.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
            using (var tx = new Transaction(doc, "Create Filled Region"))
            {
                tx.Start();
                var region = FilledRegion.Create(doc, regionType.Id, view.Id, new List<CurveLoop> { loop });
                tx.Commit();
                return new JObject { ["message"] = $"✏️ Created filled region (ID: {region.Id.Value})", ["elementId"] = region.Id.Value };
            }
        }

        private static JToken CreateSpotElevation(Document doc, UIDocument uidoc, JObject parameters)
        {
            return new JObject { ["message"] = "📍 Spot elevation requested", ["hint"] = "Use execute_code: doc.Create.NewSpotElevation(view, reference, origin, bend, end, leaderPoint, hasLeader)" };
        }

        private static JToken CreateSpotCoordinate(Document doc, UIDocument uidoc, JObject parameters)
        {
            return new JObject { ["message"] = "📍 Spot coordinate requested", ["hint"] = "Use execute_code: doc.Create.NewSpotCoordinate(view, reference, origin, bend, end, leaderPoint, hasLeader)" };
        }

        private static JToken CreateKeynoteLegend(Document doc, JObject parameters)
        {
            using (var tx = new Transaction(doc, "Create Keynote Legend"))
            {
                tx.Start();
                var legend = ViewSchedule.CreateKeynoteLegend(doc);
                tx.Commit();
                return new JObject { ["message"] = $"📋 Created keynote legend (ID: {legend.Id.Value})", ["elementId"] = legend.Id.Value };
            }
        }

        private static JToken CreateDetailComponent(Document doc, UIDocument uidoc, JObject parameters)
        {
            var symbol = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_DetailComponents).Cast<FamilySymbol>()
                .FirstOrDefault(s => (string.IsNullOrEmpty(parameters["familyName"]?.ToString()) || s.Family.Name.Contains(parameters["familyName"].ToString())) && (string.IsNullOrEmpty(parameters["typeName"]?.ToString()) || s.Name.Contains(parameters["typeName"].ToString())));
            if (symbol == null) return new JObject { ["error"] = "Detail component not found" };
            using (var tx = new Transaction(doc, "Place Detail Component"))
            {
                tx.Start(); if (!symbol.IsActive) symbol.Activate();
                var inst = doc.Create.NewFamilyInstance(new XYZ(parameters["x"]?.Value<double>() ?? 0, parameters["y"]?.Value<double>() ?? 0, 0), symbol, uidoc.ActiveView);
                tx.Commit();
                return new JObject { ["message"] = $"✏️ Placed detail component (ID: {inst.Id.Value})", ["elementId"] = inst.Id.Value };
            }
        }

        private static JToken TagRoomsInView(Document doc, UIDocument uidoc, JObject parameters)
        {
            var viewIdParam = parameters["viewId"]?.Value<int>();
            var view = viewIdParam.HasValue ? doc.GetElement(new ElementId(viewIdParam.Value)) as View : uidoc.ActiveView;
            var rooms = new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().Cast<SpatialElement>().Where(r => r.Area > 0).ToList();
            int tagged = 0;
            using (var tx = new Transaction(doc, "Tag Rooms"))
            {
                tx.Start();
                foreach (var room in rooms)
                    try { var loc = room.Location as LocationPoint; if (loc != null) { doc.Create.NewRoomTag(new LinkElementId(room.Id), new UV(loc.Point.X, loc.Point.Y), view.Id); tagged++; } } catch (Exception ex) { Logger.Log($"Room tag failed for '{room.Name}': {ex.Message}"); }
                tx.Commit();
            }
            return new JObject { ["message"] = $"🏷️ Tagged {tagged}/{rooms.Count} rooms in '{view.Name}'", ["tagged"] = tagged };
        }

        private static JToken DimensionWalls(Document doc, UIDocument uidoc, JObject parameters)
        {
            var viewIdParam = parameters["viewId"]?.Value<int>();
            var view = viewIdParam.HasValue ? doc.GetElement(new ElementId(viewIdParam.Value)) as View : uidoc.ActiveView;
            var walls = new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().ToList();
            return new JObject { ["message"] = $"📏 Found {walls.Count} walls in '{view.Name}' for dimensioning", ["hint"] = "Use execute_code: create ReferenceArray from wall faces, then doc.Create.NewDimension(view, line, refs)" };
        }
    }
}
