using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// generate_dynamic_schedule — filtered quantity take-offs.
    /// Evaluates structured conditions (parsed from natural language by the
    /// MCP server) against elements of a category, comparing numeric
    /// parameters in Revit's internal units so thresholds like "900mm" work
    /// regardless of the project's display units. Looks up parameters on the
    /// instance first, then on its type (door Width lives on the type).
    /// </summary>
    public static partial class CommandExecutor
    {
        // Common names differ per category (a wall's height is "Unconnected
        // Height"); each alias list is tried in order.
        private static readonly Dictionary<string, string[]> _dynSchedParamAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Height", new[] { "Height", "Unconnected Height", "Head Height" } },
                { "Width",  new[] { "Width", "Rough Width" } },
                { "Length", new[] { "Length", "Cut Length" } },
                { "Level",  new[] { "Level", "Reference Level", "Base Constraint", "Schedule Level" } },
            };

        private static Parameter FindParameterWithAliases(Document doc, Element elem, string name)
        {
            var names = _dynSchedParamAliases.TryGetValue(name, out var aliases)
                ? aliases
                : new[] { name };

            foreach (var n in names)
            {
                foreach (Parameter p in elem.Parameters)
                {
                    if (p.HasValue && string.Equals(p.Definition.Name, n, StringComparison.OrdinalIgnoreCase))
                        return p;
                }
            }

            var typeElem = doc.GetElement(elem.GetTypeId());
            if (typeElem != null)
            {
                foreach (var n in names)
                {
                    foreach (Parameter p in typeElem.Parameters)
                    {
                        if (p.HasValue && string.Equals(p.Definition.Name, n, StringComparison.OrdinalIgnoreCase))
                            return p;
                    }
                }
            }

            return null;
        }

        private static ForgeTypeId UnitTypeIdFromString(string unit)
        {
            switch ((unit ?? "").Trim().ToLowerInvariant())
            {
                case "mm": case "millimeter": case "millimeters": return UnitTypeId.Millimeters;
                case "cm": case "centimeter": case "centimeters": return UnitTypeId.Centimeters;
                case "m": case "meter": case "meters": case "metre": case "metres": return UnitTypeId.Meters;
                case "ft": case "feet": case "foot": return UnitTypeId.Feet;
                case "in": case "inch": case "inches": return UnitTypeId.Inches;
                case "m2": case "m²": case "sqm": return UnitTypeId.SquareMeters;
                case "ft2": case "ft²": case "sqft": return UnitTypeId.SquareFeet;
                case "m3": case "m³": return UnitTypeId.CubicMeters;
                case "ft3": case "ft³": return UnitTypeId.CubicFeet;
                default: return null;
            }
        }

        /// <summary>Levels match loosely: "level 1" matches "Level 1" but not "Level 10".</summary>
        private static bool LevelNameMatches(string actual, string wanted)
        {
            string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant();
            string StripPrefix(string s) => s.StartsWith("level") ? s.Substring(5).Trim() : s;

            var a = Normalize(actual);
            var w = Normalize(wanted);
            if (a == w) return true;
            return StripPrefix(a) == StripPrefix(w);
        }

        private static bool EvaluateScheduleCondition(Document doc, Element elem, JObject cond)
        {
            var paramName = cond["parameter"]?.ToString() ?? "";
            var op = (cond["operator"]?.ToString() ?? "=").Trim();
            var rawValue = cond["value"];
            var unit = cond["unit"]?.ToString();

            var p = FindParameterWithAliases(doc, elem, paramName);
            if (p == null) return false;

            if (op == "levelmatch")
                return LevelNameMatches(p.AsValueString() ?? p.AsString(), rawValue?.ToString());

            if (p.StorageType == StorageType.Double &&
                double.TryParse(rawValue?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var threshold))
            {
                var unitId = UnitTypeIdFromString(unit);
                if (unitId == null)
                {
                    // No unit given — assume the document's display unit for this spec
                    try { unitId = doc.GetUnits().GetFormatOptions(p.Definition.GetDataType()).GetUnitTypeId(); }
                    catch { }
                }

                var thresholdInternal = unitId != null
                    ? UnitUtils.ConvertToInternalUnits(threshold, unitId)
                    : threshold;
                var actual = p.AsDouble();
                const double eps = 1e-9;

                switch (op)
                {
                    case ">": return actual > thresholdInternal + eps;
                    case ">=": return actual >= thresholdInternal - eps;
                    case "<": return actual < thresholdInternal - eps;
                    case "<=": return actual <= thresholdInternal + eps;
                    case "!=": return Math.Abs(actual - thresholdInternal) > 1e-6;
                    default: return Math.Abs(actual - thresholdInternal) <= 1e-6;
                }
            }

            if (p.StorageType == StorageType.Integer &&
                int.TryParse(rawValue?.ToString(), out var intVal))
            {
                var actualInt = p.AsInteger();
                switch (op)
                {
                    case ">": return actualInt > intVal;
                    case ">=": return actualInt >= intVal;
                    case "<": return actualInt < intVal;
                    case "<=": return actualInt <= intVal;
                    case "!=": return actualInt != intVal;
                    default: return actualInt == intVal;
                }
            }

            var actualStr = p.AsValueString() ?? p.AsString() ?? "";
            var valueStr = rawValue?.ToString() ?? "";
            switch (op)
            {
                case "contains": return actualStr.IndexOf(valueStr, StringComparison.OrdinalIgnoreCase) >= 0;
                case "!=": return !string.Equals(actualStr, valueStr, StringComparison.OrdinalIgnoreCase);
                default: return string.Equals(actualStr, valueStr, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static JToken GenerateDynamicSchedule(Document doc, JObject parameters)
        {
            var category = parameters["category"]?.ToString() ?? "";
            var conditions = parameters["conditions"] as JArray ?? new JArray();
            var level = parameters["level"]?.ToString();
            var fields = (parameters["fields"] as JArray)?.Select(f => f.ToString()).ToList()
                         ?? new List<string>();
            var limit = parameters["limit"]?.Value<int>() ?? 0;

            if (string.IsNullOrWhiteSpace(category))
                throw new InvalidOperationException(
                    "A category is required. Pass 'category' or include one in the query (e.g. 'all doors …').");

            var builtInCat = GetBuiltInCategory(category);
            if (builtInCat == BuiltInCategory.INVALID)
                throw new InvalidOperationException($"Unknown category '{category}'.");

            var elements = new FilteredElementCollector(doc)
                .OfCategory(builtInCat)
                .WhereElementIsNotElementType()
                .ToElements();

            if (!string.IsNullOrWhiteSpace(level))
            {
                conditions.Add(new JObject
                {
                    ["parameter"] = "Level",
                    ["operator"] = "levelmatch",
                    ["value"] = level
                });
            }

            // Columns: identity + every parameter a condition references + requested fields
            var columns = new List<string> { "Id", "Name", "Type", "Level" };
            foreach (var c in conditions.OfType<JObject>())
            {
                var pn = c["parameter"]?.ToString();
                if (!string.IsNullOrWhiteSpace(pn) && !columns.Contains(pn, StringComparer.OrdinalIgnoreCase))
                    columns.Add(pn);
            }
            foreach (var f in fields)
            {
                if (!columns.Contains(f, StringComparer.OrdinalIgnoreCase))
                    columns.Add(f);
            }

            var rows = new JArray();
            var matchedIds = new JArray();

            foreach (var elem in elements)
            {
                if (!conditions.OfType<JObject>().All(c => EvaluateScheduleCondition(doc, elem, c)))
                    continue;

                var row = new JObject
                {
                    ["Id"] = elem.Id.Value,
                    ["Name"] = elem.Name,
                    ["Type"] = doc.GetElement(elem.GetTypeId())?.Name ?? ""
                };
                var levelParam = FindParameterWithAliases(doc, elem, "Level");
                row["Level"] = levelParam?.AsValueString() ?? levelParam?.AsString() ?? "";

                foreach (var col in columns.Skip(4))
                {
                    var p = FindParameterWithAliases(doc, elem, col);
                    if (p == null) { row[col] = ""; continue; }

                    if (p.StorageType == StorageType.Double)
                    {
                        // Emit a real number in the document's display units so
                        // spreadsheet totals work (instead of "900 mm" strings)
                        try
                        {
                            var unitId = doc.GetUnits().GetFormatOptions(p.Definition.GetDataType()).GetUnitTypeId();
                            row[col] = Math.Round(UnitUtils.ConvertFromInternalUnits(p.AsDouble(), unitId), 3);
                        }
                        catch { row[col] = p.AsValueString() ?? ""; }
                    }
                    else if (p.StorageType == StorageType.Integer)
                    {
                        row[col] = p.AsInteger();
                    }
                    else
                    {
                        row[col] = p.AsValueString() ?? p.AsString() ?? "";
                    }
                }

                rows.Add(row);
                matchedIds.Add(elem.Id.Value);
                if (limit > 0 && rows.Count >= limit) break;
            }

            return new JObject
            {
                ["count"] = rows.Count,
                ["totalInCategory"] = elements.Count,
                ["category"] = category,
                ["columns"] = new JArray(columns),
                ["rows"] = rows,
                ["matchedIds"] = matchedIds
            };
        }
    }
}
