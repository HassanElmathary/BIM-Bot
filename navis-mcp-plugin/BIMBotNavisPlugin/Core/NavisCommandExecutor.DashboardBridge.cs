using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Internal access for the Dashboard feature to helpers that already live in the executor
    /// (Revit id lookup, tree-node classification, property text, clash traversal), so the
    /// dashboard code reuses exactly the rules the clash/Revit bridge tools use.
    /// </summary>
    public static partial class NavisCommandExecutor
    {
        internal static Document DashActiveDoc() => ActiveDoc();
        internal static string DashRevitId(ModelItem m) => RevitIdOf(m);
        internal static string DashInternalType(ModelItem m) => InternalType(m);
        internal static string DashPropText(ModelItem m, string tab, string prop) => PropText(m, tab, prop);
        internal static string DashItemRef(Document doc, ModelItem m) => ItemRef(doc, m);
        internal static DocumentClashTests DashClashData(Document doc) => ClashData(doc);
        internal static SavedItemCollection DashClashTestsRoot(DocumentClashTests data) => ClashTestsOf(data);
        internal static void DashCollectResults(SavedItemCollection items, List<ClashResult> results) => CollectResults(items, results);
        internal static List<ClashTest> DashAllTests(DocumentClashTests data) => AllTests(ClashTestsOf(data));
        internal static SelectionSet DashFindSet(Document doc, string idOrName) => FindSet(doc, idOrName);
    }
}
