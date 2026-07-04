using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BIMBotPlugin.PowerBI
{
    /// <summary>
    /// Writes extracted Revit element data and 3D geometry to CSV files that
    /// Power BI imports natively (no ODBC driver required).
    ///
    /// Geometry MeshJSON is split into chunks of at most <see cref="ChunkSize"/>
    /// characters because the Power BI data model truncates text columns at
    /// 32,766 characters. The BIM-Bot 3D Viewer visual reassembles the chunks
    /// by (ElementId, ChunkIndex).
    /// </summary>
    public class PowerBICsvWriter
    {
        /// <summary>Max characters per MeshJSON chunk (Power BI text limit is 32,766).</summary>
        public const int ChunkSize = 30000;

        public class CsvExportResult
        {
            public string DataFolder { get; set; }
            public int ElementCount { get; set; }
            public int ParameterCount { get; set; }
            public int GeometryCount { get; set; }
            public int ChunkCount { get; set; }
            public long TotalBytes { get; set; }
        }

        /// <summary>
        /// Write all export data as CSV files into <paramref name="dataFolder"/>:
        /// Elements.csv, Parameters.csv, Geometry.csv, CategoryColors.csv, ModelInfo.csv.
        /// </summary>
        public CsvExportResult Write(
            List<ElementExportData> elements,
            Dictionary<int, MeshData> meshData,
            Dictionary<string, (int R, int G, int B)> categoryColors,
            Dictionary<string, string> metadata,
            string dataFolder)
        {
            Directory.CreateDirectory(dataFolder);

            var result = new CsvExportResult { DataFolder = dataFolder };

            // ── Elements.csv ──
            var elementsPath = Path.Combine(dataFolder, "Elements.csv");
            using (var w = NewWriter(elementsPath))
            {
                w.WriteLine("ElementId,Category,FamilyName,TypeName,LevelName,Mark");
                foreach (var e in elements)
                {
                    w.Write(e.ElementId);
                    w.Write(',');
                    w.Write(Escape(e.Category));
                    w.Write(',');
                    w.Write(Escape(e.FamilyName));
                    w.Write(',');
                    w.Write(Escape(e.TypeName));
                    w.Write(',');
                    w.Write(Escape(e.LevelName));
                    w.Write(',');
                    w.WriteLine(Escape(e.Mark));
                    result.ElementCount++;
                }
            }

            // ── Parameters.csv ──
            var paramsPath = Path.Combine(dataFolder, "Parameters.csv");
            using (var w = NewWriter(paramsPath))
            {
                w.WriteLine("ElementId,ParamName,ParamValue");
                foreach (var e in elements)
                {
                    foreach (var kvp in e.Parameters)
                    {
                        w.Write(e.ElementId);
                        w.Write(',');
                        w.Write(Escape(kvp.Key));
                        w.Write(',');
                        w.WriteLine(Escape(kvp.Value));
                        result.ParameterCount++;
                    }
                }
            }

            // ── Geometry.csv (chunked MeshJSON) ──
            var geometryPath = Path.Combine(dataFolder, "Geometry.csv");
            using (var w = NewWriter(geometryPath))
            {
                w.WriteLine("ElementId,ChunkIndex,MeshJSON");
                foreach (var kvp in meshData)
                {
                    var json = kvp.Value.ToJson();
                    int chunkIndex = 0;
                    for (int offset = 0; offset < json.Length; offset += ChunkSize)
                    {
                        var chunk = json.Substring(offset, Math.Min(ChunkSize, json.Length - offset));
                        w.Write(kvp.Key);
                        w.Write(',');
                        w.Write(chunkIndex);
                        w.Write(',');
                        w.WriteLine(Escape(chunk));
                        chunkIndex++;
                        result.ChunkCount++;
                    }
                    result.GeometryCount++;
                }
            }

            // ── CategoryColors.csv ──
            var colorsPath = Path.Combine(dataFolder, "CategoryColors.csv");
            using (var w = NewWriter(colorsPath))
            {
                w.WriteLine("Category,R,G,B,ColorHex");
                foreach (var kvp in categoryColors)
                {
                    w.Write(Escape(kvp.Key));
                    w.Write(',');
                    w.Write(kvp.Value.R);
                    w.Write(',');
                    w.Write(kvp.Value.G);
                    w.Write(',');
                    w.Write(kvp.Value.B);
                    w.Write(',');
                    w.WriteLine($"#{kvp.Value.R:X2}{kvp.Value.G:X2}{kvp.Value.B:X2}");
                }
            }

            // ── ModelInfo.csv ──
            var infoPath = Path.Combine(dataFolder, "ModelInfo.csv");
            using (var w = NewWriter(infoPath))
            {
                w.WriteLine("Key,Value");
                foreach (var kvp in metadata)
                {
                    w.Write(Escape(kvp.Key));
                    w.Write(',');
                    w.WriteLine(Escape(kvp.Value));
                }
            }

            foreach (var file in new[] { elementsPath, paramsPath, geometryPath, colorsPath, infoPath })
                result.TotalBytes += new FileInfo(file).Length;

            return result;
        }

        private static StreamWriter NewWriter(string path)
        {
            // UTF-8 with BOM so Power BI auto-detects the encoding.
            return new StreamWriter(path, false, new UTF8Encoding(true));
        }

        /// <summary>RFC 4180 escaping: quote fields containing comma, quote, or newline.</summary>
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!needsQuotes) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
