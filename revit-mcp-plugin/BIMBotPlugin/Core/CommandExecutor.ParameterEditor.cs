using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Conditional bulk parameter editing — the engine behind parameter_batch_editor.
    /// Selects a scope (categories, explicit IDs, selection or active view), filters it
    /// with parameter conditions, then applies literal / regex / copy edits.
    /// </summary>
    public static partial class CommandExecutor
    {
        private const int PbeDefaultPreview = 25;

        private static JToken ParameterBatchEditor(UIDocument uidoc, Document doc, JObject parameters)
        {
            var setActions = parameters["set"] as JArray;
            if (setActions == null || setActions.Count == 0)
                throw new InvalidOperationException(
                    "'set' is required: an array of { parameterName, value } / { parameterName, find, replace } / { parameterName, fromParameter }");

            var dryRun = parameters["dryRun"]?.Value<bool>() ?? false;
            var matchAny = string.Equals(parameters["matchMode"]?.ToString(), "any", StringComparison.OrdinalIgnoreCase);
            var previewLimit = parameters["previewLimit"]?.Value<int>() ?? PbeDefaultPreview;
            var limit = parameters["limit"]?.Value<int>() ?? 0;
            var conditions = parameters["where"] as JArray;

            var scope = PbeResolveScope(uidoc, doc, parameters);

            var matched = scope.Where(e => PbeMatches(doc, e, conditions, matchAny)).ToList();
            bool truncated = limit > 0 && matched.Count > limit;
            if (truncated) matched = matched.Take(limit).ToList();

            var changes = new JArray();
            var errors = new JArray();
            int edits = 0, elementsEdited = 0, skipped = 0, unchanged = 0;

            Action apply = () =>
            {
                foreach (var elem in matched)
                {
                    bool touched = false;
                    foreach (var token in setActions)
                    {
                        if (!(token is JObject action)) continue;

                        string? error;
                        JObject? change;
                        if (PbeApply(doc, elem, action, dryRun, out change, out error))
                        {
                            edits++;
                            touched = true;
                            if (changes.Count < previewLimit)
                            {
                                change!["elementId"] = elem.Id.Value;
                                change["elementName"] = elem.Name;
                                changes.Add(change);
                            }
                        }
                        else if (error == null)
                        {
                            unchanged++;
                        }
                        else
                        {
                            skipped++;
                            if (errors.Count < previewLimit)
                                errors.Add($"[{elem.Id.Value}] {error}");
                        }
                    }
                    if (touched) elementsEdited++;
                }
            };

            if (dryRun)
            {
                apply();
            }
            else
            {
                using (var tx = new Transaction(doc, "Parameter Batch Edit"))
                {
                    tx.Start();
                    try
                    {
                        apply();
                        tx.Commit();
                    }
                    catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
                }
            }

            var verb = dryRun ? "Would edit" : "Edited";
            var message = $"{(dryRun ? "🔍 [DRY RUN] " : "✅ ")}{verb} {edits} parameter value(s) on {elementsEdited} of {matched.Count} matched element(s) (scope: {scope.Count})";
            if (unchanged > 0) message += $", {unchanged} already correct";
            if (skipped > 0) message += $", {skipped} skipped";
            if (truncated) message += $" — limited to {limit}";

            return new JObject
            {
                ["message"] = message,
                ["dryRun"] = dryRun,
                ["scopeCount"] = scope.Count,
                ["matched"] = matched.Count,
                ["elementsEdited"] = elementsEdited,
                ["edits"] = edits,
                ["unchanged"] = unchanged,
                ["skipped"] = skipped,
                ["truncated"] = truncated,
                ["changes"] = changes,
                ["errors"] = errors
            };
        }

        // ── Scope ────────────────────────────────────────────────────

        private static List<Element> PbeResolveScope(UIDocument uidoc, Document doc, JObject parameters)
        {
            var ids = parameters["elementIds"] as JArray;
            if (ids != null && ids.Count > 0)
            {
                return ids.Select(t => doc.GetElement(new ElementId(t.Value<long>())))
                          .Where(e => e != null)
                          .ToList();
            }

            if (parameters["useSelection"]?.Value<bool>() == true)
            {
                if (uidoc == null) throw new InvalidOperationException("No active UI document for useSelection");
                var sel = uidoc.Selection.GetElementIds();
                if (sel.Count == 0) throw new InvalidOperationException("useSelection was requested but nothing is selected");
                return sel.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();
            }

            var categories = new List<string>();
            if (parameters["categories"] is JArray catArray)
                categories.AddRange(catArray.Select(t => t.ToString()));
            var single = parameters["category"]?.ToString();
            if (!string.IsNullOrEmpty(single)) categories.Add(single!);

            if (categories.Count == 0)
                throw new InvalidOperationException("Provide a scope: 'category'/'categories', 'elementIds', or useSelection=true");

            var activeViewOnly = parameters["activeViewOnly"]?.Value<bool>() ?? false;
            var result = new List<Element>();
            foreach (var name in categories.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var bic = GetBuiltInCategory(name);
                var collector = activeViewOnly
                    ? new FilteredElementCollector(doc, doc.ActiveView.Id)
                    : new FilteredElementCollector(doc);
                result.AddRange(collector.OfCategory(bic).WhereElementIsNotElementType().ToList());
            }
            return result;
        }

        // ── Conditions ───────────────────────────────────────────────

        private static bool PbeMatches(Document doc, Element elem, JArray? conditions, bool matchAny)
        {
            if (conditions == null || conditions.Count == 0) return true;

            bool anyTrue = false;
            foreach (var token in conditions)
            {
                if (!(token is JObject cond)) continue;
                bool ok = PbeEvaluate(doc, elem, cond);
                if (matchAny) { if (ok) anyTrue = true; }
                else if (!ok) return false;
            }
            return matchAny ? anyTrue : true;
        }

        private static bool PbeEvaluate(Document doc, Element elem, JObject cond)
        {
            var pname = cond["parameterName"]?.ToString();
            if (string.IsNullOrEmpty(pname)) return false;

            var op = (cond["operator"]?.ToString() ?? "equals").ToLowerInvariant();
            var expected = cond["value"];
            var unit = cond["unit"]?.ToString();

            // Type parameters are readable for filtering (e.g. a wall type's "Width").
            var p = PbeFindParameter(doc, elem, pname!, includeType: true);
            if (p == null)
                return op == "isempty" || op == "notexists";

            var text = PbeValueString(p) ?? "";

            switch (op)
            {
                case "exists": return true;
                case "notexists": return false;
                case "isempty": return string.IsNullOrWhiteSpace(text);
                case "isnotempty": return !string.IsNullOrWhiteSpace(text);
            }

            // Numeric comparisons. Equality only goes numeric for genuinely numeric
            // parameters — otherwise a text value like "2h" would parse as 2 and
            // wrongly equal "2x".
            bool numericOperator;
            switch (op)
            {
                case "greaterthan": case "lessthan": case "greaterorequal": case "lessorequal":
                case ">": case "<": case ">=": case "<=":
                    numericOperator = true; break;
                default:
                    numericOperator = p.StorageType == StorageType.Double || p.StorageType == StorageType.Integer;
                    break;
            }

            double? left = numericOperator ? PbeNumericValue(p) : null;
            double? right = numericOperator ? PbeExpectedNumber(p, expected, unit) : null;
            if (left.HasValue && right.HasValue)
            {
                switch (op)
                {
                    case "greaterthan": case ">": return left.Value > right.Value;
                    case "lessthan": case "<": return left.Value < right.Value;
                    case "greaterorequal": case ">=": return left.Value >= right.Value;
                    case "lessorequal": case "<=": return left.Value <= right.Value;
                    case "equals": case "=": case "==": return Math.Abs(left.Value - right.Value) < 1e-9;
                    case "notequals": case "!=": return Math.Abs(left.Value - right.Value) >= 1e-9;
                }
            }

            var expectedText = expected?.ToString() ?? "";
            switch (op)
            {
                case "equals": case "=": case "==":
                    return string.Equals(text, expectedText, StringComparison.OrdinalIgnoreCase);
                case "notequals": case "!=":
                    return !string.Equals(text, expectedText, StringComparison.OrdinalIgnoreCase);
                case "contains":
                    return text.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) >= 0;
                case "notcontains":
                    return text.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) < 0;
                case "startswith":
                    return text.StartsWith(expectedText, StringComparison.OrdinalIgnoreCase);
                case "endswith":
                    return text.EndsWith(expectedText, StringComparison.OrdinalIgnoreCase);
                case "matches": case "regex":
                    return Regex.IsMatch(text, expectedText, RegexOptions.IgnoreCase);
                case "greaterthan": case "lessthan": case "greaterorequal": case "lessorequal":
                case ">": case "<": case ">=": case "<=":
                    return false; // non-numeric parameter, numeric operator
                default:
                    throw new InvalidOperationException($"Unknown operator '{op}' in 'where' condition");
            }
        }

        // ── Edits ────────────────────────────────────────────────────

        /// <summary>
        /// Applies one 'set' entry. Returns true when a value changed; false with a null
        /// <paramref name="error"/> means the value was already correct (not a failure).
        /// </summary>
        private static bool PbeApply(Document doc, Element elem, JObject action, bool dryRun, out JObject? change, out string? error)
        {
            change = null;
            error = null;

            var pname = action["parameterName"]?.ToString();
            if (string.IsNullOrEmpty(pname)) { error = "'set' entry is missing parameterName"; return false; }

            var applyToType = action["applyToType"]?.Value<bool>() ?? false;
            var p = PbeFindParameter(doc, elem, pname!, includeType: applyToType);
            if (p == null) { error = $"parameter '{pname}' not found"; return false; }
            if (p.IsReadOnly) { error = $"parameter '{pname}' is read-only"; return false; }

            var oldValue = PbeValueString(p) ?? "";
            string? newText = null;
            JToken? literal = null;

            var find = action["find"]?.ToString();
            var fromParameter = action["fromParameter"]?.ToString();

            if (!string.IsNullOrEmpty(find))
            {
                var replace = action["replace"]?.ToString() ?? "";
                var useRegex = action["regex"]?.Value<bool>() ?? true;
                try
                {
                    newText = useRegex
                        ? Regex.Replace(oldValue, find, replace)
                        : oldValue.Replace(find, replace);
                }
                catch (ArgumentException ex) { error = $"invalid regex '{find}': {ex.Message}"; return false; }

                if (newText == oldValue) return false; // nothing to do, not an error
            }
            else if (!string.IsNullOrEmpty(fromParameter))
            {
                var src = PbeFindParameter(doc, elem, fromParameter!, includeType: true);
                if (src == null) { error = $"source parameter '{fromParameter}' not found"; return false; }
                newText = PbeValueString(src) ?? "";
            }
            else
            {
                literal = action["value"];
                if (literal == null || literal.Type == JTokenType.Null)
                {
                    error = $"'set' entry for '{pname}' needs value, find/replace or fromParameter";
                    return false;
                }
            }

            var prefix = action["prefix"]?.ToString();
            var suffix = action["suffix"]?.ToString();
            if (newText != null && (!string.IsNullOrEmpty(prefix) || !string.IsNullOrEmpty(suffix)))
                newText = $"{prefix}{newText}{suffix}";

            var value = literal ?? JToken.FromObject(newText!);
            var unit = action["unit"]?.ToString();

            string display;
            if (!PbeSetValue(p, value, unit, dryRun, out display, out error))
                return false;

            if (display == oldValue) return false; // no-op

            change = new JObject
            {
                ["parameter"] = pname,
                ["oldValue"] = oldValue,
                ["newValue"] = display
            };
            return true;
        }

        private static bool PbeSetValue(Parameter p, JToken value, string? unit, bool dryRun, out string display, out string? error)
        {
            display = value.ToString();
            error = null;

            switch (p.StorageType)
            {
                case StorageType.String:
                {
                    var text = value.Type == JTokenType.Boolean ? (value.Value<bool>() ? "Yes" : "No") : value.ToString();
                    display = text;
                    if (!dryRun) p.Set(text);
                    return true;
                }

                case StorageType.Integer:
                {
                    int iv;
                    if (value.Type == JTokenType.Boolean) iv = value.Value<bool>() ? 1 : 0;
                    else
                    {
                        var text = value.ToString().Trim();
                        if (string.Equals(text, "Yes", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)) iv = 1;
                        else if (string.Equals(text, "No", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)) iv = 0;
                        else if (!int.TryParse(text, out iv))
                        {
                            error = $"'{text}' is not a valid integer for '{p.Definition.Name}'";
                            return false;
                        }
                    }
                    display = iv.ToString();
                    if (!dryRun) { p.Set(iv); display = p.AsValueString() ?? display; }
                    return true;
                }

                case StorageType.Double:
                {
                    double dv;
                    if (!double.TryParse(PbeNumericText(value.ToString()), System.Globalization.NumberStyles.Any,
                                         System.Globalization.CultureInfo.InvariantCulture, out dv))
                    {
                        error = $"'{value}' is not a valid number for '{p.Definition.Name}'";
                        return false;
                    }

                    double internalValue;
                    try
                    {
                        var unitId = PbeUnitId(unit) ?? p.GetUnitTypeId();
                        internalValue = UnitUtils.ConvertToInternalUnits(dv, unitId);
                    }
                    catch { internalValue = dv; }

                    display = $"{dv}{(string.IsNullOrEmpty(unit) ? "" : " " + unit)}";
                    if (!dryRun) { p.Set(internalValue); display = p.AsValueString() ?? display; }
                    return true;
                }

                case StorageType.ElementId:
                default:
                    error = $"parameter '{p.Definition.Name}' has unsupported storage type {p.StorageType}";
                    return false;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static Parameter? PbeFindParameter(Document doc, Element elem, string name, bool includeType)
        {
            foreach (Parameter p in elem.Parameters)
                if (p.Definition != null && string.Equals(p.Definition.Name, name, StringComparison.OrdinalIgnoreCase))
                    return p;

            if (!includeType) return null;

            var typeElem = doc.GetElement(elem.GetTypeId());
            if (typeElem == null) return null;
            foreach (Parameter p in typeElem.Parameters)
                if (p.Definition != null && string.Equals(p.Definition.Name, name, StringComparison.OrdinalIgnoreCase))
                    return p;
            return null;
        }

        private static string PbeValueString(Parameter p)
        {
            switch (p.StorageType)
            {
                case StorageType.String: return p.AsString() ?? "";
                case StorageType.Integer: return p.AsValueString() ?? p.AsInteger().ToString();
                case StorageType.Double: return p.AsValueString() ?? p.AsDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
                case StorageType.ElementId:
                {
                    var target = p.Element?.Document.GetElement(p.AsElementId());
                    return target?.Name ?? p.AsValueString() ?? "";
                }
                default: return "";
            }
        }

        /// <summary>Raw comparable number for a parameter — internal units for doubles.</summary>
        private static double? PbeNumericValue(Parameter p)
        {
            if (p.StorageType == StorageType.Double) return p.AsDouble();
            if (p.StorageType == StorageType.Integer) return p.AsInteger();
            if (p.StorageType == StorageType.String)
            {
                double parsed;
                if (double.TryParse(PbeNumericText(p.AsString() ?? ""), System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out parsed))
                    return parsed;
            }
            return null;
        }

        /// <summary>
        /// Converts the user's expected value into the same units as PbeNumericValue.
        /// An explicit unit wins; otherwise the parameter's own display unit is assumed,
        /// so "Height > 3" means 3 in whatever the user sees in Revit.
        /// </summary>
        private static double? PbeExpectedNumber(Parameter p, JToken? expected, string? unit)
        {
            if (expected == null) return null;

            var text = expected.ToString();
            // Allow inline units: "3m", "2500 mm"
            if (string.IsNullOrEmpty(unit))
            {
                var m = Regex.Match(text.Trim(), @"^-?[\d.,]+\s*([a-zA-Z²³^0-9]+)$");
                if (m.Success && PbeUnitId(m.Groups[1].Value) != null) unit = m.Groups[1].Value;
            }

            double dv;
            if (!double.TryParse(PbeNumericText(text), System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out dv))
                return null;

            if (p.StorageType != StorageType.Double) return dv;

            try
            {
                var unitId = PbeUnitId(unit) ?? p.GetUnitTypeId();
                return UnitUtils.ConvertToInternalUnits(dv, unitId);
            }
            catch { return dv; }
        }

        private static string PbeNumericText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var m = Regex.Match(text.Trim(), @"-?\d+(\.\d+)?");
            return m.Success ? m.Value : text.Trim();
        }

        private static ForgeTypeId? PbeUnitId(string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return null;
            switch (unit!.Trim().ToLowerInvariant())
            {
                case "mm": case "millimeter": case "millimeters": return UnitTypeId.Millimeters;
                case "cm": case "centimeter": case "centimeters": return UnitTypeId.Centimeters;
                case "m": case "meter": case "meters": return UnitTypeId.Meters;
                case "ft": case "feet": case "foot": return UnitTypeId.Feet;
                case "in": case "inch": case "inches": return UnitTypeId.Inches;
                case "m2": case "m²": case "sqm": return UnitTypeId.SquareMeters;
                case "ft2": case "ft²": case "sqft": return UnitTypeId.SquareFeet;
                case "m3": case "m³": case "cbm": return UnitTypeId.CubicMeters;
                case "ft3": case "ft³": case "cft": return UnitTypeId.CubicFeet;
                case "deg": case "degree": case "degrees": return UnitTypeId.Degrees;
                case "rad": case "radian": case "radians": return UnitTypeId.Radians;
                default: return null;
            }
        }
    }
}
