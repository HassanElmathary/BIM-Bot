using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;

namespace BIMBotPlugin.Core.Compat
{
    /// <summary>
    /// Document.GetUnusedElements() is Revit 2024+. Earlier versions expose no
    /// equivalent API at all, so this falls back to a manual sweep.
    ///
    /// The fallback is deliberately conservative — it only reports element types
    /// whose "unused" status can be established with certainty from the model
    /// (no instances placed, not referenced by a view). It will therefore purge
    /// less than Revit's own Purge Unused on 2020-2023. Purging too little is
    /// recoverable; deleting something still in use is not.
    /// </summary>
    public static class PurgeCompat
    {
        public static ICollection<DB.ElementId> GetUnusedElementIds(DB.Document doc)
        {
#if REVIT_PRE_2024
            return CollectUnused(doc);
#else
            return doc.GetUnusedElements(new HashSet<DB.ElementId>());
#endif
        }

        /// <summary>True when this band uses the reduced fallback sweep.</summary>
        public static bool IsConservativeSweep =>
#if REVIT_PRE_2024
            true;
#else
            false;
#endif

#if REVIT_PRE_2024
        private static ICollection<DB.ElementId> CollectUnused(DB.Document doc)
        {
            var unused = new List<DB.ElementId>();

            // Family symbols with no placed instances (skip in-place families —
            // their symbol and instance are inseparable).
            var usedSymbolIds = new HashSet<DB.ElementId>(
                new DB.FilteredElementCollector(doc)
                    .OfClass(typeof(DB.FamilyInstance))
                    .Cast<DB.FamilyInstance>()
                    .Where(fi => fi.Symbol != null)
                    .Select(fi => fi.Symbol.Id));

            unused.AddRange(new DB.FilteredElementCollector(doc)
                .OfClass(typeof(DB.FamilySymbol))
                .Cast<DB.FamilySymbol>()
                .Where(fs => fs.Family != null && !fs.Family.IsInPlace)
                .Where(fs => !usedSymbolIds.Contains(fs.Id))
                .Select(fs => fs.Id));

            // Group types with no placed groups.
            var usedGroupTypeIds = new HashSet<DB.ElementId>(
                new DB.FilteredElementCollector(doc)
                    .OfClass(typeof(DB.Group))
                    .Cast<DB.Group>()
                    .Where(g => g.GroupType != null)
                    .Select(g => g.GroupType.Id));

            unused.AddRange(new DB.FilteredElementCollector(doc)
                .OfClass(typeof(DB.GroupType))
                .Cast<DB.GroupType>()
                .Where(gt => !usedGroupTypeIds.Contains(gt.Id))
                .Select(gt => gt.Id));

            // View filters not applied to any view.
            var views = new DB.FilteredElementCollector(doc)
                .OfClass(typeof(DB.View))
                .Cast<DB.View>()
                .Where(v => !v.IsTemplate)
                .ToList();

            var usedFilterIds = new HashSet<DB.ElementId>();
            foreach (var view in views)
            {
                try
                {
                    foreach (var id in view.GetFilters()) usedFilterIds.Add(id);
                }
                catch { /* view type does not support filters */ }
            }

            unused.AddRange(new DB.FilteredElementCollector(doc)
                .OfClass(typeof(DB.ParameterFilterElement))
                .Where(f => !usedFilterIds.Contains(f.Id))
                .Select(f => f.Id));

            // View templates not assigned to any view.
            var usedTemplateIds = new HashSet<DB.ElementId>(
                views.Select(v => v.ViewTemplateId)
                     .Where(id => id != null && id != DB.ElementId.InvalidElementId));

            unused.AddRange(new DB.FilteredElementCollector(doc)
                .OfClass(typeof(DB.View))
                .Cast<DB.View>()
                .Where(v => v.IsTemplate && !usedTemplateIds.Contains(v.Id))
                .Select(v => v.Id));

            return unused;
        }
#endif
    }
}
