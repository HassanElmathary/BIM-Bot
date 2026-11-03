using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Navisworks.Api;
using BIMBotNavisPlugin.Core;
using BIMBotNavisPlugin.Dashboard.Config;

namespace BIMBotNavisPlugin.Dashboard.Export
{
    public sealed class DashboardExportResult
    {
        /// <summary>"created" (first export), "regenerated" (report rebuilt) or "dataOnly" (CSVs refreshed, report untouched).</summary>
        public string Action = "";
        public string PbitPath = "";
        public string DataFolder = "";
        public int Elements, GeometryRows, Properties, Clashes;
        public bool DataChanged, LayoutChanged;
        public string Warning = "";
        public TimeSpan Duration;
    }

    /// <summary>
    /// Runs one dashboard export: extract → (geometry) → CSV → layout → .pbit, then records the
    /// result on the dashboard in the registry. Update-aware:
    ///   live refresh, layout unchanged → only the CSVs are rewritten; the .pbit/.pbix are not
    ///     touched, so every manual edit made in Power BI survives (press Refresh in Power BI).
    ///   layout changed, first export, static snapshot, or force → the .pbit is regenerated.
    /// Must run on the Navisworks main thread.
    /// </summary>
    public static class DashboardExportService
    {
        public static DashboardExportResult Export(Document doc, DashboardProject project, DashboardRegistry registry,
            DashboardConfig cfg, IProgressSink progress, bool force = false)
        {
            var started = DateTime.UtcNow;
            var opt = cfg.Export;
            bool live = !string.Equals(opt.RefreshMode, "static", StringComparison.OrdinalIgnoreCase);

            var root = string.IsNullOrWhiteSpace(opt.OutputFolder) ? project.DefaultOutputRoot : opt.OutputFolder;
            var folder = Path.Combine(root, SafeName(cfg.Name));
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            var dataFolder = live ? Path.Combine(folder, "Data") : Path.Combine(folder, "Data_" + stamp);
            var pbitPath = live ? Path.Combine(folder, SafeName(cfg.Name) + ".pbit") : Path.Combine(folder, $"{SafeName(cfg.Name)}_{stamp}.pbit");

            var keys = new ElementKeyMap(project.KeyMapPath);
            var data = new NavisDataExtractor(doc, opt, keys, progress).Extract(project.DisplayName);
            if (data.Elements.Count == 0) throw new Exception("No model elements found in the chosen data scope.");
            if (opt.IncludeGeometry && cfg.Viewer.Enabled)
                new NavisGeometryExtractor(progress).Extract(data);
            keys.SaveIfChanged();

            progress.Report("Writing CSV files", 1, 1);
            var csv = new NavisCsvWriter().Write(data, dataFolder, cfg.Name);

            var layout = LayoutBuilder.Build(cfg, project.DisplayName, opt.IncludeClashes);
            var layoutHash = Hash(layout);
            var last = cfg.LastExport;

            var result = new DashboardExportResult
            {
                PbitPath = pbitPath,
                DataFolder = dataFolder,
                Elements = data.Elements.Count,
                GeometryRows = csv.GeometryRows,
                Properties = csv.PropertyRows,
                Clashes = csv.ClashRows,
                DataChanged = last == null || last.DataHash != csv.DataHash,
                LayoutChanged = last == null || last.LayoutHash != layoutHash,
            };

            bool rebuild = force || !live || last == null || result.LayoutChanged
                           || !File.Exists(pbitPath)
                           || !string.Equals(last.DataFolder, dataFolder, StringComparison.OrdinalIgnoreCase);
            if (rebuild)
            {
                progress.Report("Building Power BI template", 1, 1);
                new NavisPbitGenerator().Generate(pbitPath, NavisDataModel.BuildSchema(dataFolder), layout,
                    $"BIM-Bot Navisworks dashboard — {cfg.Name}");
                result.Action = last == null || !File.Exists(last.PbitPath) ? "created" : "regenerated";
            }
            else result.Action = "dataOnly";

            if (!string.Equals(opt.Format, "pbit", StringComparison.OrdinalIgnoreCase))
                result.Warning = "Publishing to the Power BI Service is not available yet: the .pbit was written locally.";

            cfg.LastExport = new ExportState
            {
                ExportedUtc = DateTime.UtcNow,
                PbitPath = pbitPath,
                DataFolder = dataFolder,
                DataHash = csv.DataHash,
                LayoutHash = layoutHash,
                ElementCount = data.Elements.Count,
                GeometryRows = csv.GeometryRows,
                ClashCount = csv.ClashRows,
            };
            registry.Save(cfg);

            result.Duration = DateTime.UtcNow - started;
            NavisLogger.Log($"Dashboard '{cfg.Name}' exported ({result.Action}): {result.Elements} elements, {result.GeometryRows} geometry rows, {result.Clashes} clashes in {result.Duration.TotalSeconds:0.0}s");
            return result;
        }

        public static string SafeName(string name)
        {
            var s = Regex.Replace(name ?? "", "[\\\\/:*?\"<>|]", "_").Trim().TrimEnd('.');
            return s == "" ? "Dashboard" : s;
        }

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
