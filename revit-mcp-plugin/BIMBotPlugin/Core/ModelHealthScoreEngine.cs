using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    public static partial class CommandExecutor
    {
        private static JObject ScoreDocument(Document targetDoc, string modelName)
        {
            int warnings = targetDoc.GetWarnings().Count;
            int criticalWarnings = targetDoc.GetWarnings().Count(w => w.GetDescriptionText().ToLower().Contains("not bound") || w.GetDescriptionText().ToLower().Contains("overlap"));
            int moderateWarnings = warnings - criticalWarnings;

            var inPlaceFamilies = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                // Via the compat helper: ElementId.IntegerValue is gone in Revit 2026+.
                .Where(f => f.IsInPlace && f.FamilyCategory?.Id.Val() == (long)BuiltInCategory.OST_GenericModel)
                .Count();

            var importedCad = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .Where(i => !i.IsLinked)
                .Count();

            var unpinnedDatums = 0;
            unpinnedDatums += new FilteredElementCollector(targetDoc).OfClass(typeof(Level)).Cast<Level>().Count(l => !l.Pinned);
            unpinnedDatums += new FilteredElementCollector(targetDoc).OfClass(typeof(Grid)).Cast<Grid>().Count(g => !g.Pinned);
            unpinnedDatums += new FilteredElementCollector(targetDoc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().Count(r => !r.Pinned);

            var unplacedRooms = new FilteredElementCollector(targetDoc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<SpatialElement>()
                .Count(r => r.Area == 0);

            var sheetsWithViewIds = new HashSet<int>();
            foreach (var sheet in new FilteredElementCollector(targetDoc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>())
            {
                foreach (var vpId in sheet.GetAllViewports())
                {
                    var vp = targetDoc.GetElement(vpId) as Viewport;
                    if (vp != null) sheetsWithViewIds.Add((int)vp.ViewId.Val());
                }
            }
            var viewsNotOnSheets = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser && v.ViewType != ViewType.Internal && v.ViewType != ViewType.DrawingSheet)
                .Count(v => !sheetsWithViewIds.Contains((int)v.Id.Val()));

            double score = 100.0;
            score -= (criticalWarnings * 2.0);
            score -= (moderateWarnings * 0.5);
            score -= (inPlaceFamilies * 3.0);
            score -= (importedCad * 5.0);
            score -= (unpinnedDatums * 1.0);
            score -= (unplacedRooms * 0.5);
            score -= (viewsNotOnSheets * 0.1);

            if (score < 0) score = 0;
            if (score > 100) score = 100;

            return new JObject
            {
                ["modelName"] = modelName,
                ["score"] = score,
                ["warnings"] = new JObject
                {
                    ["total"] = warnings,
                    ["critical"] = criticalWarnings,
                    ["moderate"] = moderateWarnings
                },
                ["inPlaceFamilies"] = inPlaceFamilies,
                ["importedCad"] = importedCad,
                ["unpinnedDatums"] = unpinnedDatums,
                ["unplacedRooms"] = unplacedRooms,
                ["viewsNotOnSheets"] = viewsNotOnSheets
            };
        }

        private static JToken AuditFederatedModelHealth(Document doc, JObject parameters)
        {
            var scope = parameters["scope"]?.ToString() ?? "all";
            var linkNames = (parameters["linkNames"] as JArray)?.Select(t => t.ToString()).ToList() ?? new List<string>();
            var severityThreshold = parameters["severityThreshold"]?.Value<int>() ?? 0;

            var results = new JArray();
            
            if (scope == "all" || scope == "host")
            {
                results.Add(ScoreDocument(doc, "Host Model"));
            }

            if (scope == "all" || scope == "links")
            {
                var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
                foreach (var link in links)
                {
                    var linkDoc = link.GetLinkDocument();
                    if (linkDoc == null) continue;
                    
                    if (linkNames.Count > 0 && !linkNames.Contains(link.Name)) continue;
                    
                    results.Add(ScoreDocument(linkDoc, link.Name));
                }
            }

            return new JObject
            {
                ["message"] = "Health scoring complete.",
                ["results"] = results
            };
        }

        private static JToken HealModelIssues(Document doc, JObject parameters)
        {
            var actions = (parameters["actions"] as JArray)?.Select(t => t.ToString()).ToList() ?? new List<string>();
            var dryRun = parameters["dryRun"]?.Value<bool>() ?? false;

            int datumsPinned = 0;
            int cadPurged = 0;
            int roomsCleaned = 0;

            using (var tx = new Transaction(doc, "Heal Model Issues"))
            {
                if (!dryRun) tx.Start();

                if (actions.Contains("pin_datums"))
                {
                    var unpinnedLevels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().Where(l => !l.Pinned).ToList();
                    var unpinnedGrids = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().Where(g => !g.Pinned).ToList();
                    var unpinnedLinks = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().Where(r => !r.Pinned).ToList();
                    
                    datumsPinned = unpinnedLevels.Count + unpinnedGrids.Count + unpinnedLinks.Count;
                    
                    if (!dryRun)
                    {
                        foreach (var l in unpinnedLevels) l.Pinned = true;
                        foreach (var g in unpinnedGrids) g.Pinned = true;
                        foreach (var r in unpinnedLinks) r.Pinned = true;
                    }
                }

                if (actions.Contains("purge_cad_imports"))
                {
                    var importedCad = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).Cast<ImportInstance>().Where(i => !i.IsLinked).ToList();
                    cadPurged = importedCad.Count;
                    
                    if (!dryRun)
                    {
                        foreach (var c in importedCad) doc.Delete(c.Id);
                    }
                }

                if (actions.Contains("clean_unplaced_rooms"))
                {
                    var unplacedRooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().Cast<SpatialElement>().Where(r => r.Area == 0).ToList();
                    roomsCleaned = unplacedRooms.Count;

                    if (!dryRun)
                    {
                        foreach (var r in unplacedRooms) doc.Delete(r.Id);
                    }
                }

                if (!dryRun) tx.Commit();
            }

            return new JObject
            {
                ["dryRun"] = dryRun,
                ["message"] = dryRun ? "Dry run completed. These issues would be fixed." : "Issues fixed successfully.",
                ["fixed"] = new JObject
                {
                    ["datumsPinned"] = datumsPinned,
                    ["cadPurged"] = cadPurged,
                    ["roomsCleaned"] = roomsCleaned
                }
            };
        }

        private static JToken GenerateRemediationReport(Document doc, JObject parameters)
        {
            var linkName = parameters["linkName"]?.ToString();
            var format = parameters["format"]?.ToString() ?? "text";

            Document targetDoc = doc;
            if (!string.IsNullOrEmpty(linkName))
            {
                var link = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().FirstOrDefault(l => l.Name.Contains(linkName));
                if (link != null && link.GetLinkDocument() != null)
                {
                    targetDoc = link.GetLinkDocument();
                }
                else
                {
                    return new JObject { ["error"] = $"Link '{linkName}' not found or not loaded." };
                }
            }

            var scoreData = ScoreDocument(targetDoc, targetDoc.Title);
            var sb = new StringBuilder();
            sb.AppendLine($"Remediation Report for: {targetDoc.Title}");
            sb.AppendLine($"Overall Health Score: {scoreData["score"]}%");
            sb.AppendLine();
            sb.AppendLine("Issues requiring attention:");

            if ((int)scoreData["unpinnedDatums"] > 0)
                sb.AppendLine($"- Pin {(int)scoreData["unpinnedDatums"]} unpinned datum elements (Levels, Grids, Links).");
            if ((int)scoreData["importedCad"] > 0)
                sb.AppendLine($"- Remove {(int)scoreData["importedCad"]} imported CAD files. Use linking instead.");
            if ((int)scoreData["inPlaceFamilies"] > 0)
                sb.AppendLine($"- Replace {(int)scoreData["inPlaceFamilies"]} in-place generic models with loadable families.");
            if ((int)scoreData["unplacedRooms"] > 0)
                sb.AppendLine($"- Delete {(int)scoreData["unplacedRooms"]} unplaced or redundant rooms.");
            if ((int)scoreData["warnings"]["total"] > 0)
                sb.AppendLine($"- Resolve {(int)scoreData["warnings"]["total"]} warnings ({(int)scoreData["warnings"]["critical"]} critical).");
            
            return new JObject
            {
                ["reportText"] = sb.ToString()
            };
        }
    }
}
