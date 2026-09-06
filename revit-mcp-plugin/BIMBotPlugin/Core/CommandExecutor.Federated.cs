using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    public static partial class CommandExecutor
    {
        // ===== FEDERATED QUERY COMMANDS =====

        private static JToken GetFederationSummary(Document doc)
        {
            var summaries = LinkDocumentResolver.GetAllLinkSummaries(doc);
            var hostInfo = new JObject
            {
                ["name"] = Path.GetFileNameWithoutExtension(doc.Title ?? "Host"),
                ["filePath"] = doc.PathName,
                ["isWorkshared"] = doc.IsWorkshared,
                ["elementCount"] = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType().GetElementCount(),
                ["warningCount"] = doc.GetWarnings().Count,
                ["levelCount"] = new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementCount()
            };

            var linksArr = new JArray();
            int totalLinkedElements = 0;
            int totalLinkedWarnings = 0;
            int loadedCount = 0;
            int unloadedCount = 0;

            foreach (var s in summaries)
            {
                linksArr.Add(new JObject
                {
                    ["linkName"] = s.LinkName,
                    ["fullName"] = s.FullName,
                    ["isLoaded"] = s.IsLoaded,
                    ["isNested"] = s.IsNested,
                    ["instanceCount"] = s.InstanceCount,
                    ["filePath"] = s.FilePath,
                    ["elementCount"] = s.ElementCount,
                    ["warningCount"] = s.WarningCount,
                    ["levelCount"] = s.LevelCount
                });
                if (s.IsLoaded) { loadedCount++; totalLinkedElements += s.ElementCount; totalLinkedWarnings += s.WarningCount; }
                else { unloadedCount++; }
            }

            return new JObject
            {
                ["host"] = hostInfo,
                ["links"] = linksArr,
                ["summary"] = new JObject
                {
                    ["totalLinks"] = summaries.Count,
                    ["loadedLinks"] = loadedCount,
                    ["unloadedLinks"] = unloadedCount,
                    ["totalHostElements"] = hostInfo["elementCount"],
                    ["totalLinkedElements"] = totalLinkedElements,
                    ["totalFederatedElements"] = (int)hostInfo["elementCount"] + totalLinkedElements,
                    ["totalHostWarnings"] = hostInfo["warningCount"],
                    ["totalLinkedWarnings"] = totalLinkedWarnings
                }
            };
        }

        private static JToken QueryAcrossLinks(Document doc, JObject parameters)
        {
            var category = parameters["category"]?.ToString() ?? "";
            var includeHost = parameters["includeHost"]?.Value<bool>() ?? true;
            var linkNames = parameters["linkNames"]?.ToObject<string[]>();
            var offset = parameters["offset"]?.Value<int>() ?? 0;
            var limit = parameters["limit"]?.Value<int>() ?? 200;
            var includeParams = parameters["includeParameters"]?.Value<bool>() ?? false;
            var paramFilter = parameters["parameterFilter"]?.ToString();

            var builtInCat = GetBuiltInCategory(category);
            var scope = includeHost ? FederatedScope.AllLinks : FederatedScope.LinksOnly;
            if (linkNames != null && linkNames.Length > 0 && includeHost)
                scope = FederatedScope.AllLinks; // Will filter links by name but include host

            var allElements = FederatedCollector.Collect(doc, builtInCat, scope, linkNames);

            // Apply parameter filter if specified (e.g. "Fire Rating=2 HR")
            if (!string.IsNullOrEmpty(paramFilter))
            {
                var filterParts = paramFilter.Split(new[] { '=' }, 2);
                if (filterParts.Length == 2)
                {
                    var filterParamName = filterParts[0].Trim();
                    var filterValue = filterParts[1].Trim();
                    allElements = allElements.Where(fe =>
                    {
                        var p = fe.Element.LookupParameter(filterParamName);
                        if (p == null || !p.HasValue) return false;
                        var val = p.AsValueString() ?? p.AsString() ?? "";
                        return val.IndexOf(filterValue, StringComparison.OrdinalIgnoreCase) >= 0;
                    });
                }
            }

            // If not including host but filtering by linkNames, apply link filter
            if (!includeHost && linkNames != null && linkNames.Length > 0)
            {
                allElements = allElements.Where(fe =>
                    linkNames.Any(ln => fe.SourceModel.IndexOf(ln, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            var counted = allElements.ToList();
            var totalCount = counted.Count;
            var subset = counted.Skip(offset).Take(limit);

            var result = new JArray();
            foreach (var fe in subset)
            {
                var obj = new JObject
                {
                    ["namespacedId"] = fe.NamespacedId,
                    ["id"] = fe.Element.Id.Val(),
                    ["name"] = fe.Element.Name,
                    ["category"] = fe.Element.Category?.Name ?? "Unknown",
                    ["sourceModel"] = fe.SourceModel,
                    ["isFromLink"] = fe.IsFromLink
                };

                if (includeParams)
                {
                    var paramsObj = new JObject();
                    foreach (Parameter p in fe.Element.Parameters)
                    {
                        if (p.HasValue)
                            paramsObj[p.Definition.Name] = p.AsValueString() ?? p.AsString() ?? "";
                    }
                    obj["parameters"] = paramsObj;
                }

                result.Add(obj);
            }

            return new JObject
            {
                ["totalCount"] = totalCount,
                ["count"] = result.Count,
                ["offset"] = offset,
                ["limit"] = limit,
                ["hasMore"] = (offset + result.Count) < totalCount,
                ["elements"] = result
            };
        }

        private static JToken GetLinkElementDetails(Document doc, JObject parameters)
        {
            var namespacedId = parameters["namespacedId"]?.ToString();
            if (string.IsNullOrWhiteSpace(namespacedId))
                throw new InvalidOperationException("namespacedId is required (format: 'LinkName:ElementId' or 'Host:ElementId')");

            var resolved = LinkDocumentResolver.ResolveNamespacedId(doc, namespacedId);
            if (resolved == null)
                throw new InvalidOperationException($"Element not found: {namespacedId}");

            var (resolvedDoc, elem, sourceName) = resolved.Value;
            var result = new JObject
            {
                ["namespacedId"] = namespacedId,
                ["elementId"] = elem.Id.Val(),
                ["sourceModel"] = sourceName,
                ["name"] = elem.Name,
                ["category"] = elem.Category?.Name ?? "Unknown"
            };

            // Instance parameters
            var instanceParams = new JObject();
            foreach (Parameter p in elem.Parameters)
            {
                if (p.HasValue)
                    instanceParams[p.Definition.Name] = p.AsValueString() ?? p.AsString() ?? "";
            }
            result["instanceParameters"] = instanceParams;

            // Type parameters
            var typeElem = resolvedDoc.GetElement(elem.GetTypeId());
            if (typeElem != null)
            {
                var typeParams = new JObject();
                foreach (Parameter p in typeElem.Parameters)
                {
                    if (p.HasValue)
                        typeParams[p.Definition.Name] = p.AsValueString() ?? p.AsString() ?? "";
                }
                result["typeParameters"] = typeParams;
                result["familyName"] = (typeElem as FamilySymbol)?.FamilyName ?? typeElem.Name;
                result["typeName"] = typeElem.Name;
            }

            // Location info
            if (elem.Location is LocationPoint lp)
            {
                result["location"] = new JObject
                {
                    ["type"] = "point",
                    ["x"] = Math.Round(lp.Point.X, 4),
                    ["y"] = Math.Round(lp.Point.Y, 4),
                    ["z"] = Math.Round(lp.Point.Z, 4)
                };
            }
            else if (elem.Location is LocationCurve lc)
            {
                var start = lc.Curve.GetEndPoint(0);
                var end = lc.Curve.GetEndPoint(1);
                result["location"] = new JObject
                {
                    ["type"] = "curve",
                    ["startX"] = Math.Round(start.X, 4), ["startY"] = Math.Round(start.Y, 4), ["startZ"] = Math.Round(start.Z, 4),
                    ["endX"] = Math.Round(end.X, 4), ["endY"] = Math.Round(end.Y, 4), ["endZ"] = Math.Round(end.Z, 4),
                    ["length"] = Math.Round(lc.Curve.Length, 4)
                };
            }

            return result;
        }

        private static JToken CompareLinkLevels(Document doc)
        {
            var allLevels = FederatedCollector.CollectByClass(doc, typeof(Level), FederatedScope.AllLinks)
                .Select(fe => new
                {
                    fe.SourceModel,
                    Level = (Level)fe.Element,
                    fe.NamespacedId
                })
                .ToList();

            // Group by level name to find mismatches
            var grouped = allLevels.GroupBy(l => l.Level.Name, StringComparer.OrdinalIgnoreCase);
            var result = new JArray();
            var mismatches = new JArray();

            foreach (var group in grouped.OrderBy(g => g.First().Level.Elevation))
            {
                var entries = group.ToList();
                var elevations = entries.Select(e => Math.Round(e.Level.Elevation, 4)).Distinct().ToList();
                var hasMismatch = elevations.Count > 1;

                var levelObj = new JObject
                {
                    ["levelName"] = group.Key,
                    ["presentInModels"] = new JArray(entries.Select(e => e.SourceModel).Distinct()),
                    ["elevations"] = new JArray(entries.Select(e => new JObject
                    {
                        ["sourceModel"] = e.SourceModel,
                        ["elevation"] = Math.Round(e.Level.Elevation, 4)
                    })),
                    ["hasMismatch"] = hasMismatch
                };
                result.Add(levelObj);
                if (hasMismatch) mismatches.Add(levelObj);
            }

            // Find levels that exist in some models but not others
            var allModelNames = allLevels.Select(l => l.SourceModel).Distinct().ToList();
            var missingLevels = new JArray();
            foreach (var group in grouped)
            {
                var presentIn = group.Select(e => e.SourceModel).Distinct().ToList();
                var missingFrom = allModelNames.Except(presentIn).ToList();
                if (missingFrom.Count > 0)
                {
                    missingLevels.Add(new JObject
                    {
                        ["levelName"] = group.Key,
                        ["presentIn"] = new JArray(presentIn),
                        ["missingFrom"] = new JArray(missingFrom)
                    });
                }
            }

            return new JObject
            {
                ["levels"] = result,
                ["totalLevelNames"] = result.Count,
                ["elevationMismatches"] = mismatches,
                ["mismatchCount"] = mismatches.Count,
                ["missingLevels"] = missingLevels,
                ["missingCount"] = missingLevels.Count,
                ["modelsCompared"] = new JArray(allModelNames)
            };
        }

        private static JToken FindCrossLinkSpatialContainment(Document doc, JObject parameters)
        {
            var equipCategory = parameters["equipmentCategory"]?.ToString() ?? "Mechanical Equipment";
            var sourceLinkName = parameters["sourceLinkName"]?.ToString();

            // Get rooms from host
            var hostRooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<Room>()
                .Where(r => r.Area > 0) // Only placed rooms
                .ToList();

            if (hostRooms.Count == 0)
                return new JObject { ["error"] = "No placed rooms found in host model", ["count"] = 0 };

            // Get equipment from links
            var builtInCat = GetBuiltInCategory(equipCategory);
            var scope = string.IsNullOrEmpty(sourceLinkName) ? FederatedScope.LinksOnly : FederatedScope.SelectedLinks;
            var linkNames = string.IsNullOrEmpty(sourceLinkName) ? null : new[] { sourceLinkName };

            var linkedEquipment = FederatedCollector.Collect(doc, builtInCat, scope, linkNames).ToList();

            var result = new JArray();
            int matched = 0, unmatched = 0;

            foreach (var fe in linkedEquipment)
            {
                XYZ point = null;
                if (fe.Element.Location is LocationPoint lp)
                    point = fe.WorldTransform.OfPoint(lp.Point);
                else if (fe.Element.Location is LocationCurve lc)
                    point = fe.WorldTransform.OfPoint(lc.Curve.Evaluate(0.5, true));

                if (point == null) { unmatched++; continue; }

                // Find containing room
                Room containingRoom = null;
                foreach (var room in hostRooms)
                {
                    if (room.IsPointInRoom(point))
                    {
                        containingRoom = room;
                        break;
                    }
                }

                var entry = new JObject
                {
                    ["equipmentNamespacedId"] = fe.NamespacedId,
                    ["equipmentName"] = fe.Element.Name,
                    ["sourceLink"] = fe.SourceModel,
                    ["category"] = fe.Element.Category?.Name ?? equipCategory
                };

                if (containingRoom != null)
                {
                    entry["roomName"] = containingRoom.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                    entry["roomNumber"] = containingRoom.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "";
                    entry["roomLevel"] = containingRoom.Level?.Name ?? "";
                    entry["contained"] = true;
                    matched++;
                }
                else
                {
                    entry["contained"] = false;
                    entry["worldPosition"] = new JObject
                    {
                        ["x"] = Math.Round(point.X, 4),
                        ["y"] = Math.Round(point.Y, 4),
                        ["z"] = Math.Round(point.Z, 4)
                    };
                    unmatched++;
                }

                result.Add(entry);
            }

            return new JObject
            {
                ["totalEquipment"] = linkedEquipment.Count,
                ["containedInRoom"] = matched,
                ["notContained"] = unmatched,
                ["results"] = result
            };
        }
    }
}
