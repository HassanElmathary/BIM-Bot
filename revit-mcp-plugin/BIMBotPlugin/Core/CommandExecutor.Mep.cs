using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
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
        /// <summary>
        /// Minimum length Revit will accept for a linear MEP run. Below this the
        /// creation APIs throw "Points cannot be coincident", which surfaces to the
        /// user as an opaque Revit exception instead of a usable message.
        /// </summary>
        private const double MinRunLengthFt = 0.01;

        /// <summary>
        /// Reads startX/Y/Z and endX/Y/Z and rejects a degenerate run up front.
        /// Returns false and fills <paramref name="error"/> when the run is too short.
        /// </summary>
        private static bool TryReadRunEndpoints(JObject parameters, string what,
            out XYZ start, out XYZ end, out JObject? error)
        {
            start = new XYZ(
                parameters["startX"]?.Value<double>() ?? 0,
                parameters["startY"]?.Value<double>() ?? 0,
                parameters["startZ"]?.Value<double>() ?? 0);
            end = new XYZ(
                parameters["endX"]?.Value<double>() ?? 0,
                parameters["endY"]?.Value<double>() ?? 0,
                parameters["endZ"]?.Value<double>() ?? 0);

            var length = start.DistanceTo(end);
            if (length < MinRunLengthFt)
            {
                error = new JObject
                {
                    ["error"] = $"Cannot create {what}: start and end points are coincident "
                              + $"(distance {length:0.####} ft, minimum {MinRunLengthFt} ft).",
                    ["hint"] = "Supply distinct startX/startY/startZ and endX/endY/endZ values."
                };
                return false;
            }

            error = null;
            return true;
        }

        private static JToken CreateDuct(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            if (!TryReadRunEndpoints(parameters, "duct", out var start, out var end, out var runError))
                return runError!;
            var ductType = new FilteredElementCollector(doc).OfClass(typeof(DuctType)).FirstOrDefault();
            var sysType = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).FirstOrDefault();
            if (ductType == null || sysType == null) return new JObject { ["error"] = "No duct or system type found" };
            using (var tx = new Transaction(doc, "Create Duct"))
            {
                tx.Start();
                var duct = Duct.Create(doc, sysType.Id, ductType.Id, level.Id, start, end);
                tx.Commit();
                return new JObject { ["message"] = $"🔧 Created duct (ID: {duct.Id.Val()})", ["elementId"] = duct.Id.Val() };
            }
        }

        private static JToken CreatePipe(Document doc, JObject parameters)
        {
            var levelName = parameters["levelName"]?.ToString();
            var level = FindLevel(doc, levelName);
            if (level == null) return new JObject { ["error"] = $"Level '{levelName}' not found" };
            if (!TryReadRunEndpoints(parameters, "pipe", out var start, out var end, out var runError))
                return runError!;
            var pipeType = new FilteredElementCollector(doc).OfClass(typeof(PipeType)).FirstOrDefault();
            var sysType = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).FirstOrDefault();
            if (pipeType == null || sysType == null) return new JObject { ["error"] = "No pipe or system type found" };
            using (var tx = new Transaction(doc, "Create Pipe"))
            {
                tx.Start();
                var pipe = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, start, end);
                tx.Commit();
                return new JObject { ["message"] = $"🔧 Created pipe (ID: {pipe.Id.Val()})", ["elementId"] = pipe.Id.Val() };
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
                return new JObject { ["message"] = $"🔧 Created flex duct (ID: {fd.Id.Val()})", ["elementId"] = fd.Id.Val() };
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
                return new JObject { ["message"] = $"📦 Created MEP space (ID: {space.Id.Val()})", ["elementId"] = space.Id.Val() };
            }
        }

        private static JToken GetMepSystems(Document doc, JObject parameters)
        {
            var systems = new JArray();
            foreach (var sys in new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystem)).Cast<MechanicalSystem>())
                systems.Add(new JObject { ["id"] = sys.Id.Val(), ["name"] = sys.Name, ["type"] = "Mechanical", ["elements"] = sys.DuctNetwork?.Size ?? 0 });
            foreach (var sys in new FilteredElementCollector(doc).OfClass(typeof(PipingSystem)).Cast<PipingSystem>())
                systems.Add(new JObject { ["id"] = sys.Id.Val(), ["name"] = sys.Name, ["type"] = "Piping", ["elements"] = sys.PipingNetwork?.Size ?? 0 });
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
                var item = new JObject { ["id"] = e.Id.Val(), ["name"] = e.Name };
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

        private static JToken CreateElectricalCircuit(Document doc, JObject parameters)
        {
            var panelIdVal = parameters["panelId"]?.Value<int>() ?? 0;
            var deviceIdsArr = parameters["deviceIds"] as JArray;
            var circuitTypeStr = parameters["circuitType"]?.ToString() ?? "Power";

            if (deviceIdsArr == null || deviceIdsArr.Count == 0)
                return new JObject { ["error"] = "At least one device ID is required." };

            var deviceIds = deviceIdsArr
                .Select(id => new ElementId(id.Value<int>()))
                .Where(id => doc.GetElement(id) != null)
                .ToList();

            if (deviceIds.Count == 0)
                return new JObject { ["error"] = "No valid device elements found." };

            ElectricalSystemType sysType = ElectricalSystemType.PowerCircuit;
            if (Enum.TryParse<ElectricalSystemType>(circuitTypeStr, true, out var parsedType))
            {
                sysType = parsedType;
            }

            using (var tx = new Transaction(doc, "Create Electrical Circuit"))
            {
                tx.Start();
                try
                {
                    var circuit = ElectricalSystem.Create(doc, (IList<ElementId>)deviceIds, sysType);
                    if (panelIdVal > 0)
                    {
                        var panelElem = doc.GetElement(new ElementId(panelIdVal)) as FamilyInstance;
                        if (panelElem != null)
                        {
                            circuit.SelectPanel(panelElem);
                        }
                    }
                    tx.Commit();
                    return new JObject
                    {
                        ["message"] = $"⚡ Created electrical circuit (ID: {circuit.Id.Val()})",
                        ["circuitId"] = circuit.Id.Val(),
                        ["circuitNumber"] = circuit.CircuitNumber ?? "",
                        ["systemType"] = circuit.SystemType.ToString()
                    };
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    return new JObject { ["error"] = $"Failed to create electrical circuit: {ex.Message}" };
                }
            }
        }
    }
}
