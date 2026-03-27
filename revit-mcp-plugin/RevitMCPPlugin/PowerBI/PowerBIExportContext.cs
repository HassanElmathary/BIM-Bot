using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Newtonsoft.Json;

namespace RevitMCPPlugin.PowerBI
{
    /// <summary>
    /// Compact mesh data for a single Revit element.
    /// Stored as flat arrays for minimal JSON size.
    /// </summary>
    public class MeshData
    {
        /// <summary>Flat vertex array: [x1,y1,z1, x2,y2,z2, ...]</summary>
        public List<double> Vertices { get; set; } = new List<double>();

        /// <summary>Flat face index array: [i1,i2,i3, i4,i5,i6, ...]</summary>
        public List<int> Faces { get; set; } = new List<int>();

        public int VertexCount => Vertices.Count / 3;
        public int FaceCount => Faces.Count / 3;

        /// <summary>Serialize to compact JSON: {"v":[...],"f":[...]}</summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"v\":[");
            for (int i = 0; i < Vertices.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Math.Round(Vertices[i], 4));
            }
            sb.Append("],\"f\":[");
            for (int i = 0; i < Faces.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Faces[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Element metadata extracted alongside geometry.
    /// </summary>
    public class ElementExportData
    {
        public int ElementId { get; set; }
        public string Category { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string LevelName { get; set; }
        public string Mark { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// IExportContext implementation that walks Revit geometry and extracts
    /// tessellated mesh data (vertices + face indices) per element.
    /// Converts from Revit internal units (feet) to meters.
    /// </summary>
    public class PowerBIExportContext : IExportContext
    {
        private const double FeetToMeters = 0.3048;

        private readonly Document _doc;
        private int _currentElementId = -1;
        private MeshData _currentMesh;
        private int _vertexOffset; // track vertex offset across multiple polymeshes per element

        /// <summary>Output: ElementId → MeshData</summary>
        public Dictionary<int, MeshData> MeshDataByElement { get; } = new Dictionary<int, MeshData>();

        /// <summary>Set to true to cancel the export</summary>
        public bool Cancel { get; set; }

        public PowerBIExportContext(Document doc)
        {
            _doc = doc;
        }

        public bool Start()
        {
            // Called at the beginning of the export process
            return true;
        }

        public void Finish()
        {
            // Called at the end of the export process
            FinalizeCurrentElement();
        }

        public bool IsCanceled()
        {
            return Cancel;
        }

        public RenderNodeAction OnViewBegin(ViewNode node)
        {
            return RenderNodeAction.Proceed;
        }

        public void OnViewEnd(ElementId elementId)
        {
        }

        public RenderNodeAction OnElementBegin(ElementId elementId)
        {
            FinalizeCurrentElement();

            _currentElementId = elementId.IntegerValue;
            _currentMesh = new MeshData();
            _vertexOffset = 0;

            return RenderNodeAction.Proceed;
        }

        public void OnElementEnd(ElementId elementId)
        {
            // Element will be finalized on next OnElementBegin or Finish
        }

        public RenderNodeAction OnInstanceBegin(InstanceNode node)
        {
            return RenderNodeAction.Proceed;
        }

        public void OnInstanceEnd(InstanceNode node)
        {
        }

        public RenderNodeAction OnLinkBegin(LinkNode node)
        {
            // Skip linked models — export only the host model
            return RenderNodeAction.Skip;
        }

        public void OnLinkEnd(LinkNode node)
        {
        }

        public RenderNodeAction OnFaceBegin(FaceNode node)
        {
            return RenderNodeAction.Proceed;
        }

        public void OnFaceEnd(FaceNode node)
        {
        }

        public void OnRPC(RPCNode node)
        {
        }

        public void OnLight(LightNode node)
        {
        }

        public void OnMaterial(MaterialNode node)
        {
        }

        public void OnPolymesh(PolymeshTopology polymesh)
        {
            if (_currentMesh == null) return;

            // Extract vertices (convert feet → meters)
            var points = polymesh.GetPoints();
            foreach (var pt in points)
            {
                _currentMesh.Vertices.Add(Math.Round(pt.X * FeetToMeters, 4));
                _currentMesh.Vertices.Add(Math.Round(pt.Y * FeetToMeters, 4));
                _currentMesh.Vertices.Add(Math.Round(pt.Z * FeetToMeters, 4));
            }

            // Extract face indices (offset by current vertex count)
            var facets = polymesh.GetFacets();
            foreach (var facet in facets)
            {
                _currentMesh.Faces.Add(facet.V1 + _vertexOffset);
                _currentMesh.Faces.Add(facet.V2 + _vertexOffset);
                _currentMesh.Faces.Add(facet.V3 + _vertexOffset);
            }

            _vertexOffset += points.Count;
        }

        private void FinalizeCurrentElement()
        {
            if (_currentElementId > 0 && _currentMesh != null && _currentMesh.VertexCount > 0)
            {
                if (MeshDataByElement.ContainsKey(_currentElementId))
                {
                    // Merge with existing mesh data (multiple solids per element)
                    var existing = MeshDataByElement[_currentElementId];
                    int offset = existing.VertexCount;
                    existing.Vertices.AddRange(_currentMesh.Vertices);
                    foreach (var idx in _currentMesh.Faces)
                    {
                        existing.Faces.Add(idx + offset);
                    }
                }
                else
                {
                    MeshDataByElement[_currentElementId] = _currentMesh;
                }
            }

            _currentElementId = -1;
            _currentMesh = null;
            _vertexOffset = 0;
        }

        /// <summary>
        /// Extract element metadata from the document for all elements that have geometry.
        /// Call this AFTER the export has finished to collect metadata.
        /// </summary>
        public List<ElementExportData> ExtractElementMetadata(
            IEnumerable<int> elementIds,
            IEnumerable<string> categoryFilter = null)
        {
            var result = new List<ElementExportData>();
            var catSet = categoryFilter != null
                ? new HashSet<string>(categoryFilter, StringComparer.OrdinalIgnoreCase)
                : null;

            foreach (var id in elementIds)
            {
                var elem = _doc.GetElement(new ElementId(id));
                if (elem == null) continue;

                var category = elem.Category?.Name ?? "Unknown";
                if (catSet != null && !catSet.Contains(category)) continue;

                var data = new ElementExportData
                {
                    ElementId = id,
                    Category = category,
                    FamilyName = GetFamilyName(elem),
                    TypeName = GetTypeName(elem),
                    LevelName = GetLevelName(elem),
                    Mark = GetParameterValue(elem, BuiltInParameter.ALL_MODEL_MARK)
                };

                // Collect instance parameters
                foreach (Parameter param in elem.Parameters)
                {
                    if (param.HasValue && param.Definition != null)
                    {
                        var val = param.AsValueString() ?? param.AsString();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            data.Parameters[param.Definition.Name] = val;
                        }
                    }
                }

                result.Add(data);
            }

            return result;
        }

        /// <summary>
        /// Get a mapping of Category → Revit category color (RGB).
        /// Falls back to a default palette if no color is defined.
        /// </summary>
        public Dictionary<string, (int R, int G, int B)> GetCategoryColors(IEnumerable<string> categories)
        {
            var colors = new Dictionary<string, (int, int, int)>();
            var defaultPalette = new (int R, int G, int B)[]
            {
                (100, 149, 237), // Cornflower blue (Walls)
                (169, 169, 169), // Dark gray (Floors)
                (144, 238, 144), // Light green (Roofs)
                (255, 218, 185), // Peach (Doors)
                (173, 216, 230), // Light blue (Windows)
                (221, 160, 221), // Plum (Furniture)
                (255, 228, 181), // Moccasin (Ceilings)
                (188, 143, 143), // Rosy brown (Columns)
                (152, 251, 152), // Pale green (Structural)
                (135, 206, 235), // Sky blue (Generic)
            };

            int paletteIdx = 0;
            foreach (var cat in categories.Distinct())
            {
                // Try to get Revit's native category color
                var builtInCat = _doc.Settings.Categories
                    .OfType<Category>()
                    .FirstOrDefault(c => c.Name.Equals(cat, StringComparison.OrdinalIgnoreCase));

                if (builtInCat?.LineColor != null && builtInCat.LineColor.IsValid)
                {
                    colors[cat] = (builtInCat.LineColor.Red, builtInCat.LineColor.Green, builtInCat.LineColor.Blue);
                }
                else
                {
                    var fallback = defaultPalette[paletteIdx % defaultPalette.Length];
                    colors[cat] = fallback;
                    paletteIdx++;
                }
            }

            return colors;
        }

        // ── Helpers ──

        private string GetFamilyName(Element elem)
        {
            if (elem is FamilyInstance fi)
                return fi.Symbol?.Family?.Name ?? "";
            var typeId = elem.GetTypeId();
            if (typeId != null && typeId != ElementId.InvalidElementId)
            {
                var type = _doc.GetElement(typeId);
                if (type is FamilySymbol fs)
                    return fs.Family?.Name ?? "";
            }
            return "";
        }

        private string GetTypeName(Element elem)
        {
            var typeId = elem.GetTypeId();
            if (typeId != null && typeId != ElementId.InvalidElementId)
            {
                var type = _doc.GetElement(typeId);
                return type?.Name ?? "";
            }
            return "";
        }

        private string GetLevelName(Element elem)
        {
            var levelId = elem.LevelId;
            if (levelId != null && levelId != ElementId.InvalidElementId)
            {
                var level = _doc.GetElement(levelId) as Level;
                return level?.Name ?? "";
            }

            // Try parameter
            var val = GetParameterValue(elem, BuiltInParameter.SCHEDULE_LEVEL_PARAM);
            if (!string.IsNullOrEmpty(val)) return val;
            val = GetParameterValue(elem, BuiltInParameter.LEVEL_PARAM);
            return val ?? "";
        }

        private string GetParameterValue(Element elem, BuiltInParameter bip)
        {
            try
            {
                var param = elem.get_Parameter(bip);
                if (param != null && param.HasValue)
                    return param.AsValueString() ?? param.AsString() ?? "";
            }
            catch { }
            return "";
        }
    }
}
