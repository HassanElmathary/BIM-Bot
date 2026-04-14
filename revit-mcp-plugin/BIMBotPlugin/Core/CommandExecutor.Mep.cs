using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// MEP tool implementations: ducts, pipes, flex ducts, MEP spaces, systems, sizing, connections.
    /// </summary>
    public static partial class CommandExecutor
    {
        private static JToken CreateDuct(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            var ductType = new FilteredElementCollector(doc).OfClass(typeof(DuctType)).FirstOrDefault();
            var sysType = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).FirstOrDefault();
            if (ductType == null || sysType == null) return new JObject { ["error"] = "No duct or system type found" };
            using (var tx = new Transaction(doc, "Create Duct"))
            {
                tx.Start();
                var duct = Duct.Create(doc, sysType.Id, ductType.Id, level.Id,
                    new XYZ(parameters["startX"]?.Value<double>() ?? 0, parameters["startY"]?.Value<double>() ?? 0, parameters["startZ"]?.Value<double>() ?? 0),
                    new XYZ(parameters["endX"]?.Value<double>() ?? 0, parameters["endY"]?.Value<double>() ?? 0, parameters["endZ"]?.Value<double>() ?? 0));
                tx.Commit();
                return new JObject { ["message"] = $"🔧 Created duct (ID: {duct.Id.Value})", ["elementId"] = duct.Id.Value };
            }
        }

        private static JToken CreatePipe(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            var pipeType = new FilteredElementCollector(doc).OfClass(typeof(PipeType)).FirstOrDefault();
            var sysType = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).FirstOrDefault();
            if (pipeType == null || sysType == null) return new JObject { ["error"] = "No pipe or system type found" };
            using (var tx = new Transaction(doc, "Create Pipe"))
            {
                tx.Start();
                var pipe = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id,
                    new XYZ(parameters["startX"]?.Value<double>() ?? 0, parameters["startY"]?.Value<double>() ?? 0, parameters["startZ"]?.Value<double>() ?? 0),
                    new XYZ(parameters["endX"]?.Value<double>() ?? 0, parameters["endY"]?.Value<double>() ?? 0, parameters["endZ"]?.Value<double>() ?? 0));
                tx.Commit();
                return new JObject { ["message"] = $"🔧 Created pipe (ID: {pipe.Id.Value})", ["elementId"] = pipe.Id.Value };
            }
        }

        private static JToken CreateFlexDuct(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            var pointsArr = parameters["points"] as JArray;
            if (pointsArr == null || pointsArr.Count < 2) return new JObject { ["error"] = "Need at least 2 points" };
            var flexType = new FilteredElementCollector(doc).OfClass(typeof(FlexDuctType)).FirstOrDefault();
            var sysType = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).FirstOrDefault();
            if (flexType == null || sysType == null) return new JObject { ["error"] = "No flex duct type found" };
            var pts = pointsArr.Select(p => new XYZ(p["x"]?.Value<double>() ?? 0, p["y"]?.Value<double>() ?? 0, p["z"]?.Value<double>() ?? 0)).ToList();
            using (var tx = new Transaction(doc, "Create Flex Duct"))
            {
                tx.Start();
                var fd = FlexDuct.Create(doc, sysType.Id, flexType.Id, level.Id, pts.First(), pts.Last(), pts);
                tx.Commit();
                return new JObject { ["message"] = $"🔧 Created flex duct (ID: {fd.Id.Value})", ["elementId"] = fd.Id.Value };
            }
        }

        private static JToken CreateMepSpace(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            using (var tx = new Transaction(doc, "Create MEP Space"))
            {
                tx.Start();
                var space = doc.Create.NewSpace(level, new UV(parameters["x"]?.Value<double>() ?? 0, parameters["y"]?.Value<double>() ?? 0));
                var spaceName = parameters["spaceName"]?.ToString();
                if (!string.IsNullOrEmpty(spaceName)) space.get_Parameter(BuiltInParameter.ROOM_NAME)?.Set(spaceName);
                tx.Commit();
                return new JObject { ["message"] = $"📦 Created MEP space (ID: {space.Id.Value})", ["elementId"] = space.Id.Value };
            }
        }

        private static JToken GetMepSystems(Document doc, JObject parameters)
        {
            var systems = new JArray();
            foreach (var sys in new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystem)).Cast<MechanicalSystem>())
                systems.Add(new JObject { ["id"] = sys.Id.Value, ["name"] = sys.Name, ["type"] = "Mechanical", ["elements"] = sys.DuctNetwork?.Size ?? 0 });
            foreach (var sys in new FilteredElementCollector(doc).OfClass(typeof(PipingSystem)).Cast<PipingSystem>())
                systems.Add(new JObject { ["id"] = sys.Id.Value, ["name"] = sys.Name, ["type"] = "Piping", ["elements"] = sys.PipingNetwork?.Size ?? 0 });
            return new JObject { ["message"] = $"🔧 Found {systems.Count} MEP systems", ["systems"] = systems };
        }

        private static JToken DuctSizing(Document doc, JObject parameters)
        {
            var cat = parameters["category"]?.ToString() ?? "Ducts";
            var bic = cat.ToLower().Contains("pipe") ? BuiltInCategory.OST_PipeCurves : BuiltInCategory.OST_DuctCurves;
            var elements = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToList();
            var items = new JArray();
            foreach (var e in elements.Take(50))
            {
                var item = new JObject { ["id"] = e.Id.Value, ["name"] = e.Name };
                var sizeP = e.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE); if (sizeP != null) item["size"] = sizeP.AsString();
                items.Add(item);
            }
            return new JObject { ["message"] = $"📐 {cat} sizing: {elements.Count} elements", ["elements"] = items };
        }

        private static JToken ConnectMepElements(Document doc, JObject parameters)
        {
            var e1 = doc.GetElement(new ElementId(parameters["elementId1"]?.Value<int>() ?? 0));
            var e2 = doc.GetElement(new ElementId(parameters["elementId2"]?.Value<int>() ?? 0));
            if (e1 == null || e2 == null) return new JObject { ["error"] = "Element not found" };
            ConnectorSet cs1 = (e1 as MEPCurve)?.ConnectorManager?.Connectors;
            ConnectorSet cs2 = (e2 as MEPCurve)?.ConnectorManager?.Connectors;
            if (cs1 == null || cs2 == null) return new JObject { ["error"] = "Elements have no connectors" };
            Connector best1 = null, best2 = null; double minDist = double.MaxValue;
            foreach (Connector c1 in cs1) { if (c1.IsConnected) continue; foreach (Connector c2 in cs2) { if (c2.IsConnected) continue; double d = c1.Origin.DistanceTo(c2.Origin); if (d < minDist) { minDist = d; best1 = c1; best2 = c2; } } }
            if (best1 == null) return new JObject { ["error"] = "No unconnected connectors" };
            using (var tx = new Transaction(doc, "Connect MEP")) { tx.Start(); best1.ConnectTo(best2); tx.Commit(); }
            return new JObject { ["message"] = $"🔗 Connected elements (distance: {Math.Round(minDist, 2)}ft)" };
        }
    }
}
