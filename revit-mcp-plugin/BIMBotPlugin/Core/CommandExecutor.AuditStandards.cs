using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// audit_model_standards — scans the model against a JSON standard set
    /// (naming conventions, required parameters, object-style line weights,
    /// view-template usage, model health) and reports every non-compliant
    /// element. With fix=true it applies the safe fixes in one transaction:
    /// default parameter values, missing view templates, line weights.
    /// Renames are never automatic (use find_replace_names).
    /// Also backs check_naming_conventions and validate_parameters, which
    /// were previously unimplemented stubs.
    /// </summary>
    public static partial class CommandExecutor
    {
        private static JObject Violation(
            string ruleId, string ruleType, string severity, string message,
            long elementId = 0, string elementName = "", string category = "",
            bool fixable = false)
        {
            return new JObject
            {
                ["ruleId"] = ruleId,
                ["ruleType"] = ruleType,
                ["severity"] = severity,
                ["message"] = message,
                ["elementId"] = elementId,
                ["elementName"] = elementName,
                ["category"] = category,
                ["fixable"] = fixable,
                ["fixed"] = false
            };
        }

        private static IEnumerable<(long Id, string Name, Element Elem)> CollectNamedTargets(Document doc, string target)
        {
            switch ((target ?? "").ToLowerInvariant())
            {
                case "views":
                    return new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                        .Where(v => !v.IsTemplate && v.ViewType != ViewType.Internal && v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser)
                        .Select(v => (v.Id.Value, v.Name, (Element)v));
                case "sheets":
                    return new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                        .Select(s => (s.Id.Value, $"{s.SheetNumber} — {s.Name}", (Element)s));
                case "sheetnumbers":
                    return new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                        .Select(s => (s.Id.Value, s.SheetNumber, (Element)s));
                case "levels":
                    return new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                        .Select(l => (l.Id.Value, l.Name, (Element)l));
                case "families":
                    return new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>()
                        .Select(f => (f.Id.Value, f.Name, (Element)f));
                case "materials":
                    return new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                        .Select(m => (m.Id.Value, m.Name, (Element)m));
                case "worksets":
                    if (!doc.IsWorkshared) return Enumerable.Empty<(long, string, Element)>();
                    return new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset)
                        .Select(w => ((long)w.Id.IntegerValue, w.Name, (Element)null));
                default:
                    return Enumerable.Empty<(long, string, Element)>();
            }
        }

        // Default "bad name" heuristic when no pattern is supplied: copies and
        // Revit auto-names left over from modelling.
        private static readonly Regex _defaultBadNameRe = new Regex(
            @"(copy|^view \d|^section \d+$|^drafting \d|^3d view \d|^elevation \d+ -)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static void CheckNamingRule(Document doc, JObject rule, JArray violations, string ruleId)
        {
            var target = rule["target"]?.ToString() ?? "views";
            var pattern = rule["pattern"]?.ToString();
            var severity = rule["severity"]?.ToString() ?? "warning";
            var desc = rule["description"]?.ToString();

            Regex re = null;
            if (!string.IsNullOrWhiteSpace(pattern))
                re = new Regex(pattern, RegexOptions.IgnoreCase);

            foreach (var (id, name, _) in CollectNamedTargets(doc, target))
            {
                bool bad = re != null ? !re.IsMatch(name) : _defaultBadNameRe.IsMatch(name);
                if (!bad) continue;
                violations.Add(Violation(
                    ruleId, "naming", severity,
                    desc ?? (re != null
                        ? $"Name does not match pattern '{pattern}'"
                        : "Name looks like a leftover copy/auto-name"),
                    id, name, target));
            }
        }

        private static void CheckRequiredParamsRule(Document doc, JObject rule, JArray violations, string ruleId, bool fix)
        {
            var categoryName = rule["category"]?.ToString() ?? "";
            var paramNames = (rule["parameters"] as JArray)?.Select(p => p.ToString()).ToList() ?? new List<string>();
            var severity = rule["severity"]?.ToString() ?? "error";
            var defaultValue = rule["defaultValue"]?.ToString();

            var builtInCat = GetBuiltInCategory(categoryName);
            if (builtInCat == BuiltInCategory.INVALID || paramNames.Count == 0) return;

            var elements = new FilteredElementCollector(doc)
                .OfCategory(builtInCat).WhereElementIsNotElementType().ToElements();

            foreach (var elem in elements)
            {
                foreach (var pn in paramNames)
                {
                    var p = FindParameterWithAliases(doc, elem, pn);
                    var isEmpty = p == null || !p.HasValue ||
                        (p.StorageType == StorageType.String && string.IsNullOrWhiteSpace(p.AsString()));
                    if (!isEmpty) continue;

                    var canFix = fixableParam(p) && !string.IsNullOrEmpty(defaultValue);
                    var v = Violation(ruleId, "requiredParameter", severity,
                        $"Missing required parameter '{pn}'", elem.Id.Value, elem.Name, categoryName, canFix);

                    if (fix && canFix)
                    {
                        try { p.Set(defaultValue); v["fixed"] = true; } catch { }
                    }
                    violations.Add(v);
                }
            }

            bool fixableParam(Parameter p) =>
                p != null && !p.IsReadOnly && p.StorageType == StorageType.String;
        }

        private static void CheckLineWeightRule(Document doc, JObject rule, JArray violations, string ruleId, bool fix)
        {
            var categoryName = rule["category"]?.ToString() ?? "";
            var severity = rule["severity"]?.ToString() ?? "warning";
            var projWeight = rule["projectionWeight"]?.Value<int?>();
            var cutWeight = rule["cutWeight"]?.Value<int?>();

            Category cat = null;
            foreach (Category c in doc.Settings.Categories)
            {
                if (string.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase)) { cat = c; break; }
            }
            if (cat == null) return;

            if (projWeight.HasValue)
            {
                var actual = cat.GetLineWeight(GraphicsStyleType.Projection);
                if (actual != projWeight.Value)
                {
                    var v = Violation(ruleId, "lineWeight", severity,
                        $"Projection line weight is {actual?.ToString() ?? "unset"}, standard requires {projWeight.Value}",
                        cat.Id.Value, categoryName, categoryName, fixable: true);
                    if (fix)
                    {
                        try { cat.SetLineWeight(projWeight.Value, GraphicsStyleType.Projection); v["fixed"] = true; } catch { }
                    }
                    violations.Add(v);
                }
            }
            if (cutWeight.HasValue && cat.IsCuttable)
            {
                var actual = cat.GetLineWeight(GraphicsStyleType.Cut);
                if (actual != cutWeight.Value)
                {
                    var v = Violation(ruleId, "lineWeight", severity,
                        $"Cut line weight is {actual?.ToString() ?? "unset"}, standard requires {cutWeight.Value}",
                        cat.Id.Value, categoryName, categoryName, fixable: true);
                    if (fix)
                    {
                        try { cat.SetLineWeight(cutWeight.Value, GraphicsStyleType.Cut); v["fixed"] = true; } catch { }
                    }
                    violations.Add(v);
                }
            }
        }

        private static void CheckViewRules(Document doc, JObject viewRules, JArray violations, bool fix)
        {
            if (viewRules?["requireViewTemplate"]?.Value<bool>() != true) return;

            var severity = viewRules["severity"]?.ToString() ?? "warning";
            var exempt = (viewRules["exemptPrefixes"] as JArray)?.Select(p => p.ToString()).ToList()
                         ?? new List<string>();
            var defaultTemplateName = viewRules["defaultTemplate"]?.ToString();

            View defaultTemplate = null;
            if (!string.IsNullOrWhiteSpace(defaultTemplateName))
            {
                defaultTemplate = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                    .FirstOrDefault(v => v.IsTemplate &&
                        string.Equals(v.Name, defaultTemplateName, StringComparison.OrdinalIgnoreCase));
            }

            var checkedTypes = new[]
            {
                ViewType.FloorPlan, ViewType.CeilingPlan, ViewType.Section,
                ViewType.Elevation, ViewType.ThreeD, ViewType.AreaPlan
            };

            var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => !v.IsTemplate && checkedTypes.Contains(v.ViewType))
                .Where(v => !exempt.Any(pre => v.Name.StartsWith(pre, StringComparison.OrdinalIgnoreCase)));

            foreach (var view in views)
            {
                if (view.ViewTemplateId != ElementId.InvalidElementId) continue;

                var canFix = defaultTemplate != null;
                var v = Violation("views.requireViewTemplate", "viewTemplate", severity,
                    "View has no view template assigned" +
                    (canFix ? $" (default: '{defaultTemplateName}')" : ""),
                    view.Id.Value, view.Name, view.ViewType.ToString(), canFix);

                if (fix && canFix)
                {
                    try { view.ViewTemplateId = defaultTemplate.Id; v["fixed"] = true; } catch { }
                }
                violations.Add(v);
            }
        }

        private static void CheckHealthRules(Document doc, JObject health, JArray violations)
        {
            if (health == null) return;

            var maxWarnings = health["maxWarnings"]?.Value<int?>();
            if (maxWarnings.HasValue)
            {
                var count = doc.GetWarnings().Count;
                if (count > maxWarnings.Value)
                {
                    violations.Add(Violation("health.maxWarnings", "health", "error",
                        $"Model has {count} warnings (standard allows {maxWarnings.Value}). Use check_warnings / resolve_warnings."));
                }
            }

            if (health["allowCadImports"]?.Value<bool>() == false)
            {
                var imports = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance))
                    .Cast<ImportInstance>().Where(i => !i.IsLinked).ToList();
                foreach (var imp in imports)
                {
                    violations.Add(Violation("health.noCadImports", "health", "error",
                        "Imported (not linked) CAD file in the model. Use find_cad_imports to remove.",
                        imp.Id.Value, imp.Category?.Name ?? "Import", "CAD Import"));
                }
            }

            if (health["allowInPlaceFamilies"]?.Value<bool>() == false)
            {
                var inPlace = new FilteredElementCollector(doc).OfClass(typeof(Family))
                    .Cast<Family>().Where(f => f.IsInPlace).ToList();
                foreach (var fam in inPlace)
                {
                    violations.Add(Violation("health.noInPlaceFamilies", "health", "warning",
                        "In-place family (bloats file size, breaks scheduling).",
                        fam.Id.Value, fam.Name, "Family"));
                }
            }
        }

        private static JToken AuditModelStandards(Document doc, JObject parameters)
        {
            var standards = parameters["standards"] as JObject
                ?? throw new InvalidOperationException("'standards' object is required (the MCP server supplies a default set).");
            var fix = parameters["fix"]?.Value<bool>() ?? false;
            var rules = standards["rules"] as JObject ?? new JObject();
            var violations = new JArray();

            void RunChecks()
            {
                var naming = rules["naming"] as JArray ?? new JArray();
                for (var i = 0; i < naming.Count; i++)
                {
                    if (naming[i] is JObject rule)
                        CheckNamingRule(doc, rule, violations, $"naming[{i}]:{rule["target"]}");
                }

                var reqParams = rules["requiredParameters"] as JArray ?? new JArray();
                for (var i = 0; i < reqParams.Count; i++)
                {
                    if (reqParams[i] is JObject rule)
                        CheckRequiredParamsRule(doc, rule, violations, $"requiredParameters[{i}]:{rule["category"]}", fix);
                }

                var lineWeights = rules["lineWeights"] as JArray ?? new JArray();
                for (var i = 0; i < lineWeights.Count; i++)
                {
                    if (lineWeights[i] is JObject rule)
                        CheckLineWeightRule(doc, rule, violations, $"lineWeights[{i}]:{rule["category"]}", fix);
                }

                CheckViewRules(doc, rules["views"] as JObject, violations, fix);
                CheckHealthRules(doc, rules["health"] as JObject, violations);
            }

            if (fix)
            {
                using (var tx = new Transaction(doc, "BIM-Bot Audit Fixes"))
                {
                    tx.Start();
                    try { RunChecks(); tx.Commit(); }
                    catch { if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack(); throw; }
                }
            }
            else
            {
                RunChecks();
            }

            var errors = violations.Count(v => v["severity"]?.ToString() == "error");
            var warnings = violations.Count - errors;
            var fixedCount = violations.Count(v => v["fixed"]?.Value<bool>() == true);

            return new JObject
            {
                ["standardName"] = standards["name"]?.ToString() ?? "BIM-Bot Standard",
                ["projectName"] = doc.Title,
                ["auditDate"] = DateTime.Now.ToString("o"),
                ["totalViolations"] = violations.Count,
                ["errors"] = errors,
                ["warnings"] = warnings,
                ["fixedCount"] = fixedCount,
                ["fixApplied"] = fix,
                ["violations"] = violations
            };
        }

        // ── Previously-stubbed QA tools, now backed by the audit engine ──

        private static JToken CheckNamingConventionsCmd(Document doc, JObject parameters)
        {
            var category = parameters["category"]?.ToString() ?? "All";
            var pattern = parameters["pattern"]?.ToString();
            var targets = category.Equals("All", StringComparison.OrdinalIgnoreCase)
                ? new[] { "views", "sheets", "families", "levels" }
                : new[] { category.ToLowerInvariant() };

            var violations = new JArray();
            foreach (var target in targets)
            {
                var rule = new JObject { ["target"] = target };
                if (!string.IsNullOrWhiteSpace(pattern)) rule["pattern"] = pattern;
                CheckNamingRule(doc, rule, violations, $"naming:{target}");
            }

            return new JObject
            {
                ["checked"] = string.Join(", ", targets),
                ["pattern"] = pattern ?? "(default: leftover copies / auto-names)",
                ["violationCount"] = violations.Count,
                ["violations"] = violations,
                ["tip"] = "Use find_replace_names to fix names in bulk."
            };
        }

        private static JToken ValidateParametersCmd(Document doc, JObject parameters)
        {
            var rule = new JObject
            {
                ["category"] = parameters["category"]?.ToString() ?? "",
                ["parameters"] = parameters["requiredParameters"] as JArray ?? new JArray()
            };
            var violations = new JArray();
            CheckRequiredParamsRule(doc, rule, violations, $"requiredParameters:{rule["category"]}", fix: false);

            return new JObject
            {
                ["category"] = rule["category"],
                ["violationCount"] = violations.Count,
                ["violations"] = violations,
                ["tip"] = "Use batch_modify_parameters (or audit_model_standards with fix=true and a defaultValue) to fill values in bulk."
            };
        }
    }
}
