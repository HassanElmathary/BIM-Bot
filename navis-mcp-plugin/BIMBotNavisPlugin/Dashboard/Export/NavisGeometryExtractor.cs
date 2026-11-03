using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;
using ComBridge = Autodesk.Navisworks.Api.ComApi.ComApiBridge;
using NwCom = Autodesk.Navisworks.Api.Interop.ComApi;

namespace BIMBotNavisPlugin.Dashboard.Export
{
    /// <summary>
    /// Triangulates each exported element through the COM API (GenerateSimplePrimitives) and
    /// writes it as the compact MeshJSON the BIM-Bot 3D Viewer reads: {"v":[x,y,z,...],"f":[i,j,k,...]}
    /// in metres, world coordinates, Z up — the same format as the Revit export.
    /// Vertices are welded per element at 1 mm so shared edges are not duplicated.
    /// Must run on the Navisworks main thread.
    /// </summary>
    public sealed class NavisGeometryExtractor
    {
        /// <summary>Elements above this triangle count are decimated by skipping triangles (keeps the CSV usable).</summary>
        public int MaxTrianglesPerElement { get; set; } = 60000;

        private readonly IProgressSink _progress;

        public NavisGeometryExtractor(IProgressSink progress) { _progress = progress; }

        public void Extract(ExportData data)
        {
            int i = 0, n = data.Elements.Count;
            foreach (var e in data.Elements)
            {
                if ((++i % 50 == 0 || i == n) && _progress.Report($"Triangulating geometry ({i:N0} / {n:N0})", i, n))
                    throw new OperationCanceledException();
                try
                {
                    var json = MeshOf(e.Item, data.ToMeters);
                    if (json != null) data.Meshes[e.Key] = json;
                }
                catch (Exception ex)
                {
                    Core.NavisLogger.Log($"Dashboard: geometry skipped for element {e.Key} ({e.Name}) — {ex.Message}");
                }
            }
        }

        private string? MeshOf(ModelItem element, double toMeters)
        {
            var collector = new TriangleCollector(toMeters, MaxTrianglesPerElement);
            foreach (var geomItem in element.DescendantsAndSelf.Where(x => x.HasGeometry))
            {
                var path = (NwCom.InwOaPath3)ComBridge.ToInwOaPath(geomItem);
                var target = ToIntArray(path.ArrayData);
                foreach (NwCom.InwOaFragment3 frag in path.Fragments())
                {
                    // A path's fragments include every instance sharing the geometry: keep only ours.
                    var fragPath = ToIntArray(((NwCom.InwOaPath3)frag.path).ArrayData);
                    if (!fragPath.SequenceEqual(target)) continue;

                    collector.Matrix = ToDoubleArray(((NwCom.InwLTransform3f3)frag.GetLocalToWorldMatrix()).Matrix);
                    frag.GenerateSimplePrimitives(NwCom.nwEVertexProperty.eNORMAL, collector);
                    if (collector.Full) break;
                }
                if (collector.Full) break;
            }
            return collector.TriangleCount == 0 ? null : collector.ToJson();
        }

        private static int[] ToIntArray(object o)
        {
            var a = (Array)o;
            var r = new int[a.Length];
            int lb = a.GetLowerBound(0);
            for (int i = 0; i < a.Length; i++) r[i] = Convert.ToInt32(a.GetValue(lb + i), CultureInfo.InvariantCulture);
            return r;
        }

        private static double[] ToDoubleArray(object o)
        {
            var a = (Array)o;
            var r = new double[a.Length];
            int lb = a.GetLowerBound(0);
            for (int i = 0; i < a.Length; i++) r[i] = Convert.ToDouble(a.GetValue(lb + i), CultureInfo.InvariantCulture);
            return r;
        }

        /// <summary>COM callback: receives triangles in fragment-local space, stores welded world-space metres.</summary>
        private sealed class TriangleCollector : NwCom.InwSimplePrimitivesCB
        {
            private readonly double _scale;
            private readonly int _maxTriangles;
            private readonly List<double> _v = new List<double>();
            private readonly List<int> _f = new List<int>();
            private readonly Dictionary<(long, long, long), int> _weld = new Dictionary<(long, long, long), int>();

            /// <summary>Local→world 4×4, row-major with translation in [12..14] (Navisworks convention).</summary>
            public double[] Matrix = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

            public TriangleCollector(double toMeters, int maxTriangles)
            {
                _scale = toMeters;
                _maxTriangles = maxTriangles;
            }

            public int TriangleCount => _f.Count / 3;
            public bool Full => TriangleCount >= _maxTriangles;

            public void Triangle(NwCom.InwSimpleVertex v1, NwCom.InwSimpleVertex v2, NwCom.InwSimpleVertex v3)
            {
                if (Full) return;
                int a = Index(v1), b = Index(v2), c = Index(v3);
                if (a == b || b == c || a == c) return; // degenerate after welding
                _f.Add(a); _f.Add(b); _f.Add(c);
            }

            public void Line(NwCom.InwSimpleVertex v1, NwCom.InwSimpleVertex v2) { }
            public void Point(NwCom.InwSimpleVertex v1) { }
            public void SnapPoint(NwCom.InwSimpleVertex v1) { }

            private int Index(NwCom.InwSimpleVertex v)
            {
                var c = (Array)v.coord;
                int lb = c.GetLowerBound(0);
                double x = Convert.ToDouble(c.GetValue(lb)), y = Convert.ToDouble(c.GetValue(lb + 1)), z = Convert.ToDouble(c.GetValue(lb + 2));
                var m = Matrix;
                double wx = (x * m[0] + y * m[4] + z * m[8] + m[12]) * _scale;
                double wy = (x * m[1] + y * m[5] + z * m[9] + m[13]) * _scale;
                double wz = (x * m[2] + y * m[6] + z * m[10] + m[14]) * _scale;
                var key = ((long)Math.Round(wx * 1000), (long)Math.Round(wy * 1000), (long)Math.Round(wz * 1000));
                if (_weld.TryGetValue(key, out var idx)) return idx;
                idx = _v.Count / 3;
                _v.Add(key.Item1 / 1000.0); _v.Add(key.Item2 / 1000.0); _v.Add(key.Item3 / 1000.0);
                _weld[key] = idx;
                return idx;
            }

            public string ToJson()
            {
                var sb = new StringBuilder(_v.Count * 8 + _f.Count * 6 + 16);
                sb.Append("{\"v\":[");
                for (int i = 0; i < _v.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(_v[i].ToString("0.###", CultureInfo.InvariantCulture));
                }
                sb.Append("],\"f\":[");
                for (int i = 0; i < _f.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(_f[i].ToString(CultureInfo.InvariantCulture));
                }
                sb.Append("]}");
                return sb.ToString();
            }
        }
    }
}
