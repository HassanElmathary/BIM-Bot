using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace BIMBotNavisPlugin.Dashboard.Export
{
    /// <summary>One exported model element (a row of Elements.csv).</summary>
    public sealed class ElementRow
    {
        public int Key;
        public string RevitElementId = "";
        public string InstanceGuid = "";
        public string Model = "";
        public string Discipline = "";
        public string Category = "";
        public string Family = "";
        public string Type = "";
        public string Level = "";
        public string Name = "";
        /// <summary>The Navisworks node the element was taken from (not exported).</summary>
        public ModelItem Item = null!;
    }

    public sealed class PropertyRow
    {
        public int Key;
        public string Tab = "";
        public string Name = "";
        public string Value = "";
    }

    public sealed class ClashRow
    {
        public string ClashGuid = "";
        public string Test = "";
        public string ClashName = "";
        public string Status = "";
        public double DistanceM;
        public double CenterX, CenterY, CenterZ;
        public string Found = "";
        public int KeyA;
        public int KeyB;
    }

    /// <summary>Everything one export writes to CSV.</summary>
    public sealed class ExportData
    {
        public string ProjectName = "";
        public string Units = "";
        public double ToMeters = 1.0;
        public List<string> ModelFiles = new List<string>();
        public List<ElementRow> Elements = new List<ElementRow>();
        public List<PropertyRow> Properties = new List<PropertyRow>();
        public List<ClashRow> Clashes = new List<ClashRow>();
        /// <summary>ElementKey → MeshJSON (one string per element, chunked by the CSV writer).</summary>
        public SortedDictionary<int, string> Meshes = new SortedDictionary<int, string>();
        public Dictionary<string, (int R, int G, int B)> CategoryColors =
            new Dictionary<string, (int R, int G, int B)>(StringComparer.OrdinalIgnoreCase);
    }
}
