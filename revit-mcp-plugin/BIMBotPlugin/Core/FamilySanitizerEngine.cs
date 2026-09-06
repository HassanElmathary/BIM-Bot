using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    public static partial class CommandExecutor
    {
        /// <summary>
        /// Family load options for the purge round-trip: always overwrite the
        /// in-project family with the cleaned one we just edited.
        /// </summary>
        private class PurgeFamilyLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = true;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse,
                out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = true;
                return true;
            }
        }

        private static JToken AuditAndCleanFamily(Document doc, JObject parameters)
        {
            var fix = parameters["fix"]?.Value<bool>() ?? false;

            var families = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().Where(f => f.IsEditable).ToList();

            var report = new JArray();
            int totalCadRemoved = 0;
            int familiesCleaned = 0;
            var failures = new JArray();

            foreach (var fam in families.Take(50))
            {
                Document? famDoc = null;
                var famName = fam.Name;
                try
                {
                    famDoc = doc.EditFamily(fam);
                    if (famDoc == null) continue;

                    var imports = new FilteredElementCollector(famDoc).OfClass(typeof(ImportInstance)).Cast<ImportInstance>().ToList();

                    if (imports.Count == 0) continue;

                    var entry = new JObject
                    {
                        ["familyName"] = famName,
                        ["cadImports"] = imports.Count,
                        ["cleaned"] = false
                    };

                    if (fix)
                    {
                        // Delete inside the family document, commit, then load the
                        // cleaned family back over the one in the project. Without
                        // the reload the edits die with famDoc.Close(false).
                        using (var tx = new Transaction(famDoc, "Purge CAD Imports"))
                        {
                            tx.Start();
                            foreach (var imp in imports)
                            {
                                try { famDoc.Delete(imp.Id); }
                                catch (Exception delEx)
                                {
                                    Logger.LogError($"Could not delete import {imp.Id.Val()} in family '{famName}'", delEx);
                                }
                            }
                            tx.Commit();
                        }

                        var remaining = new FilteredElementCollector(famDoc).OfClass(typeof(ImportInstance)).GetElementCount();
                        var removed = imports.Count - remaining;

                        if (removed > 0)
                        {
                            famDoc.LoadFamily(doc, new PurgeFamilyLoadOptions());
                            totalCadRemoved += removed;
                            familiesCleaned++;
                            entry["cleaned"] = true;
                            entry["cadRemoved"] = removed;
                        }
                    }

                    report.Add(entry);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Family CAD purge failed for '{famName}'", ex);
                    failures.Add(new JObject { ["familyName"] = famName, ["error"] = ex.Message });
                }
                finally
                {
                    if (famDoc != null)
                    {
                        try { famDoc.Close(false); } catch { }
                    }
                }
            }

            return new JObject
            {
                ["scannedFamilies"] = families.Count,
                ["familiesWithIssues"] = report.Count,
                ["familiesCleaned"] = familiesCleaned,
                ["totalCadRemoved"] = totalCadRemoved,
                ["applied"] = fix,
                ["report"] = report,
                ["failures"] = failures
            };
        }

        private static JToken EnrichCrossLinkCobie(Document doc, JObject parameters)
        {
            var mepCategoryStr = parameters["mepCategory"]?.ToString();
            var sourceLinkName = parameters["sourceLinkName"]?.ToString();
            
            BuiltInCategory bic = GetBuiltInCategory(mepCategoryStr ?? "OST_MechanicalEquipment");
            
            var link = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().FirstOrDefault(l => l.Name.Contains(sourceLinkName ?? ""));
            Document srcDoc = link?.GetLinkDocument() ?? doc;

            var elements = new FilteredElementCollector(srcDoc).OfCategory(bic).WhereElementIsNotElementType().ToList();

            var enriched = new JArray();
            foreach(var e in elements)
            {
                var mfr = e.LookupParameter("Manufacturer")?.AsString() ?? "Unknown";
                var model = e.LookupParameter("Model")?.AsString() ?? "Unknown";
                
                enriched.Add(new JObject
                {
                    ["id"] = e.Id.Val(),
                    ["name"] = e.Name,
                    ["manufacturer"] = mfr,
                    ["model"] = model,
                    ["cobieSpace"] = "Room-Mapped"
                });
            }

            return new JObject
            {
                ["elementsProcessed"] = elements.Count,
                ["enrichedData"] = enriched
            };
        }
    }
}
