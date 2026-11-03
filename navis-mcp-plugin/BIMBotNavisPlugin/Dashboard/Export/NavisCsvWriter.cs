using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BIMBotNavisPlugin.Dashboard.Export
{
    /// <summary>
    /// Writes the dashboard data set as UTF-8 CSV files read by the .pbit M queries:
    ///   Elements, Properties, Geometry, CategoryColors, Clashes, ClashElements, ModelInfo.
    /// Column order here must match <see cref="NavisDataModel"/>. MeshJSON is split into
    /// ChunkSize-character chunks (Power BI truncates text at 32,766 characters); the 3D Viewer
    /// re-joins them by ChunkIndex. Every file is always written (header-only when empty) so the
    /// data model never breaks on refresh.
    /// </summary>
    public sealed class NavisCsvWriter
    {
        public const int ChunkSize = 30000;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public sealed class Result
        {
            public string DataFolder = "";
            public int ElementRows, PropertyRows, GeometryRows, ClashRows;
            /// <summary>SHA-256 over every file's content — changes only when the data changes.</summary>
            public string DataHash = "";
        }

        public Result Write(ExportData d, string folder, string dashboardName)
        {
            Directory.CreateDirectory(folder);
            var r = new Result { DataFolder = folder };

            Csv(folder, "Elements.csv", "ElementKey,RevitElementId,InstanceGuid,Model,Discipline,Category,Family,Type,Level,Name",
                d.Elements.OrderBy(e => e.Key).Select(e => Row(I(e.Key), e.RevitElementId, e.InstanceGuid, e.Model, e.Discipline,
                    e.Category, e.Family, e.Type, e.Level, e.Name)), ref r.ElementRows);

            Csv(folder, "Properties.csv", "ElementKey,Tab,Name,Value",
                d.Properties.OrderBy(p => p.Key).Select(p => Row(I(p.Key), p.Tab, p.Name, p.Value)), ref r.PropertyRows);

            Csv(folder, "Geometry.csv", "ElementKey,ChunkIndex,MeshJSON", GeometryRows(d), ref r.GeometryRows);

            int colorRows = 0;
            Csv(folder, "CategoryColors.csv", "Category,R,G,B,ColorHex",
                d.CategoryColors.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(k => Row(k.Key, I(k.Value.R), I(k.Value.G), I(k.Value.B), $"#{k.Value.R:X2}{k.Value.G:X2}{k.Value.B:X2}")),
                ref colorRows);

            Csv(folder, "Clashes.csv", "ClashGuid,Test,ClashName,Status,DistanceM,CenterX,CenterY,CenterZ,Found",
                d.Clashes.Select(c => Row(c.ClashGuid, c.Test, c.ClashName, c.Status, D(c.DistanceM), D(c.CenterX), D(c.CenterY), D(c.CenterZ), c.Found)),
                ref r.ClashRows);

            int linkRows = 0;
            Csv(folder, "ClashElements.csv", "ClashGuid,ElementKey,Side", ClashLinks(d), ref linkRows);

            int infoRows = 0;
            Csv(folder, "ModelInfo.csv", "Key,Value", new[]
            {
                Row("Project", d.ProjectName),
                Row("Dashboard", dashboardName),
                Row("Models", string.Join("; ", d.ModelFiles)),
                Row("ModelUnits", d.Units),
                Row("ExportedBy", "BIM-Bot for Navisworks " + NavisPluginApp.Version),
            }, ref infoRows);

            r.DataHash = HashFolder(folder);
            // Written after hashing: the export time must not make identical data look changed.
            File.WriteAllText(Path.Combine(folder, "ExportedAt.txt"), DateTime.UtcNow.ToString("o"), Utf8);
            return r;
        }

        private static IEnumerable<string> GeometryRows(ExportData d)
        {
            foreach (var kv in d.Meshes)
            {
                var json = kv.Value;
                for (int i = 0, chunk = 0; i < json.Length; i += ChunkSize, chunk++)
                    yield return Row(I(kv.Key), I(chunk), json.Substring(i, Math.Min(ChunkSize, json.Length - i)));
            }
        }

        private static IEnumerable<string> ClashLinks(ExportData d)
        {
            foreach (var c in d.Clashes)
            {
                if (c.KeyA != 0) yield return Row(c.ClashGuid, I(c.KeyA), "A");
                if (c.KeyB != 0 && c.KeyB != c.KeyA) yield return Row(c.ClashGuid, I(c.KeyB), "B");
            }
        }

        private static void Csv(string folder, string file, string header, IEnumerable<string> rows, ref int count)
        {
            using (var w = new StreamWriter(Path.Combine(folder, file), false, Utf8))
            {
                w.NewLine = "\r\n";
                w.WriteLine(header);
                foreach (var row in rows) { w.WriteLine(row); count++; }
            }
        }

        private static string Row(params string[] cells) => string.Join(",", cells.Select(Escape));

        private static string Escape(string? s)
        {
            s ??= "";
            if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        private static string D(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        private static string HashFolder(string folder)
        {
            using (var sha = SHA256.Create())
            {
                foreach (var f in Directory.GetFiles(folder, "*.csv").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    var name = Encoding.UTF8.GetBytes(Path.GetFileName(f));
                    sha.TransformBlock(name, 0, name.Length, null, 0);
                    var bytes = File.ReadAllBytes(f);
                    sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
