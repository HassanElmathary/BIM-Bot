using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace BIMBotPlugin.Core
{
    public static class LinkDocumentResolver
    {
        // Returns all loaded link instances with their documents and transforms
        public static IEnumerable<(RevitLinkInstance Instance, Document Doc, Transform Transform, string LinkName)>
            GetLoadedLinks(Document hostDoc, string[] filterByName = null)
        {
            var links = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>();

            foreach (var link in links)
            {
                var linkDoc = link.GetLinkDocument();
                if (linkDoc == null) continue; // Skip unloaded

                var linkName = Path.GetFileNameWithoutExtension(linkDoc.Title ?? link.Name);

                // Apply name filter if provided
                if (filterByName != null && filterByName.Length > 0)
                {
                    bool match = filterByName.Any(f =>
                        linkName.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!match) continue;
                }

                yield return (link, linkDoc, link.GetTotalTransform(), linkName);
            }
        }

        // Get a summary of all links (loaded and unloaded) with their status
        public static List<LinkSummary> GetAllLinkSummaries(Document hostDoc)
        {
            var result = new List<LinkSummary>();
            var linkInstances = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            var linkTypes = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(RevitLinkType))
                .Cast<RevitLinkType>()
                .ToList();

            foreach (var linkType in linkTypes)
            {
                var instances = linkInstances.Where(i => i.GetTypeId() == linkType.Id).ToList();
                var firstInstance = instances.FirstOrDefault();
                var linkDoc = firstInstance?.GetLinkDocument();
                var isLoaded = linkDoc != null;

                var summary = new LinkSummary
                {
                    LinkTypeId = linkType.Id.Val(),
                    LinkName = Path.GetFileNameWithoutExtension(linkType.Name),
                    FullName = linkType.Name,
                    IsLoaded = isLoaded,
                    InstanceCount = instances.Count,
                    FilePath = linkType.IsNestedLink ? "(Nested)" : GetExternalFilePath(linkType),
                    IsNested = linkType.IsNestedLink
                };

                if (isLoaded && linkDoc != null)
                {
                    // Count elements by main categories
                    summary.ElementCount = new FilteredElementCollector(linkDoc)
                        .WhereElementIsNotElementType()
                        .GetElementCount();
                    summary.WarningCount = linkDoc.GetWarnings().Count;
                    summary.LevelCount = new FilteredElementCollector(linkDoc)
                        .OfClass(typeof(Level)).GetElementCount();
                }

                result.Add(summary);
            }
            return result;
        }

        // Resolve a namespaced ID like "LinkName:12345" or "Host:12345"
        public static (Document Doc, Element Elem, string SourceName)? ResolveNamespacedId(
            Document hostDoc, string namespacedId)
        {
            if (string.IsNullOrWhiteSpace(namespacedId)) return null;

            var parts = namespacedId.Split(new[] { ':' }, 2);
            if (parts.Length != 2 || !long.TryParse(parts[1], out var idVal)) return null;

            var sourceName = parts[0].Trim();
            var elementId = idVal.ToElementId();

            if (sourceName.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                var elem = hostDoc.GetElement(elementId);
                return elem != null ? (hostDoc, elem, "Host") : ((Document, Element, string)?)null;
            }

            // Search in links
            foreach (var (instance, doc, transform, linkName) in GetLoadedLinks(hostDoc))
            {
                if (linkName.IndexOf(sourceName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var elem = doc.GetElement(elementId);
                    return elem != null ? (doc, elem, linkName) : ((Document, Element, string)?)null;
                }
            }
            return null;
        }

        // Get the external file path from a RevitLinkType
        private static string GetExternalFilePath(RevitLinkType linkType)
        {
            try
            {
                var reference = linkType.GetExternalFileReference();
                if (reference != null && reference.GetLinkedFileStatus() != LinkedFileStatus.Invalid)
                {
                    var modelPath = reference.GetAbsolutePath();
                    if (modelPath != null)
                        return ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
                }
            }
            catch { }
            return "(Unknown)";
        }
    }

    public class LinkSummary
    {
        public long LinkTypeId { get; set; }
        public string LinkName { get; set; }
        public string FullName { get; set; }
        public bool IsLoaded { get; set; }
        public int InstanceCount { get; set; }
        public string FilePath { get; set; }
        public bool IsNested { get; set; }
        public int ElementCount { get; set; }
        public int WarningCount { get; set; }
        public int LevelCount { get; set; }
    }
}
