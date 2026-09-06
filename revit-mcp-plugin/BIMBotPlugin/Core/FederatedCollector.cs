using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace BIMBotPlugin.Core
{
    public enum FederatedScope
    {
        HostOnly,
        SelectedLinks,
        AllLinks,
        LinksOnly  // Only linked models, skip host
    }

    public static class FederatedCollector
    {
        /// Collects elements from host + linked documents based on scope.
        /// Uses native Revit fast filters (C++ side) before marshaling to .NET.
        public static IEnumerable<FederatedElement> Collect(
            Document hostDoc,
            BuiltInCategory category,
            FederatedScope scope = FederatedScope.HostOnly,
            string[] linkNames = null,
            bool elementTypesOnly = false)
        {
            // Host document
            if (scope != FederatedScope.LinksOnly)
            {
                var hostCollector = new FilteredElementCollector(hostDoc);
                if (category != BuiltInCategory.INVALID)
                    hostCollector = hostCollector.OfCategory(category);

                var filtered = elementTypesOnly
                    ? hostCollector.WhereElementIsElementType()
                    : hostCollector.WhereElementIsNotElementType();

                foreach (var elem in filtered)
                {
                    yield return new FederatedElement
                    {
                        Element = elem,
                        SourceModel = "Host",
                        NamespacedId = $"Host:{elem.Id.Val()}",
                        WorldTransform = Transform.Identity,
                        IsFromLink = false,
                        SourceDocument = hostDoc
                    };
                }
            }

            // Linked documents
            if (scope == FederatedScope.HostOnly) yield break;

            var filterNames = (scope == FederatedScope.SelectedLinks) ? linkNames : null;
            foreach (var (instance, linkDoc, transform, linkName) in
                LinkDocumentResolver.GetLoadedLinks(hostDoc, filterNames))
            {
                var linkCollector = new FilteredElementCollector(linkDoc);
                if (category != BuiltInCategory.INVALID)
                    linkCollector = linkCollector.OfCategory(category);

                var linkFiltered = elementTypesOnly
                    ? linkCollector.WhereElementIsElementType()
                    : linkCollector.WhereElementIsNotElementType();

                foreach (var elem in linkFiltered)
                {
                    yield return new FederatedElement
                    {
                        Element = elem,
                        SourceModel = linkName,
                        NamespacedId = $"{linkName}:{elem.Id.Val()}",
                        WorldTransform = transform,
                        IsFromLink = true,
                        SourceDocument = linkDoc
                    };
                }
            }
        }

        /// Collect elements by Class type (e.g., typeof(Level), typeof(Wall))
        public static IEnumerable<FederatedElement> CollectByClass(
            Document hostDoc,
            Type classType,
            FederatedScope scope = FederatedScope.HostOnly,
            string[] linkNames = null)
        {
            if (scope != FederatedScope.LinksOnly)
            {
                foreach (var elem in new FilteredElementCollector(hostDoc).OfClass(classType))
                {
                    yield return new FederatedElement
                    {
                        Element = elem,
                        SourceModel = "Host",
                        NamespacedId = $"Host:{elem.Id.Val()}",
                        WorldTransform = Transform.Identity,
                        IsFromLink = false,
                        SourceDocument = hostDoc
                    };
                }
            }

            if (scope == FederatedScope.HostOnly) yield break;

            var filterNames = (scope == FederatedScope.SelectedLinks) ? linkNames : null;
            foreach (var (instance, linkDoc, transform, linkName) in
                LinkDocumentResolver.GetLoadedLinks(hostDoc, filterNames))
            {
                foreach (var elem in new FilteredElementCollector(linkDoc).OfClass(classType))
                {
                    yield return new FederatedElement
                    {
                        Element = elem,
                        SourceModel = linkName,
                        NamespacedId = $"{linkName}:{elem.Id.Val()}",
                        WorldTransform = transform,
                        IsFromLink = true,
                        SourceDocument = linkDoc
                    };
                }
            }
        }

        /// Helper: Determine the FederatedScope from request parameters
        public static FederatedScope ParseScope(bool? includeLinks, string[] linkNames)
        {
            if (includeLinks != true) return FederatedScope.HostOnly;
            if (linkNames != null && linkNames.Length > 0) return FederatedScope.SelectedLinks;
            return FederatedScope.AllLinks;
        }
    }
}
