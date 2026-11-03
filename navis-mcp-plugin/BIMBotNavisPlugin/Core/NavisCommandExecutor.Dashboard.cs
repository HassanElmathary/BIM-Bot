using System;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using BIMBotNavisPlugin.Dashboard;
using BIMBotNavisPlugin.Dashboard.Export;
using BIMBotNavisPlugin.Dashboard.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// navis_dashboard_* commands — the same operations as the Dashboard panel, so an AI client
    /// can list, create, edit and export Power BI dashboards. Configs travel as the registry's
    /// JSON (DashboardConfig).
    /// </summary>
    public static partial class NavisCommandExecutor
    {
        private static JObject? ExecuteDashboard(string method, JObject p)
        {
            switch (method)
            {
                case "navis_dashboard_catalog": return DashboardCatalog();
                case "navis_dashboard_list": return DashboardList();
                case "navis_dashboard_get": return DashboardGet(p);
                case "navis_dashboard_create": return DashboardCreate(p);
                case "navis_dashboard_update": return DashboardUpdate(p);
                case "navis_dashboard_delete": return DashboardDelete(p);
                case "navis_dashboard_export": return DashboardExport(p);
                default: return null;
            }
        }

        /// <summary>Registry location for the active document (see DashboardProject).</summary>
        internal static DashboardProject DashboardProjectOf(Document doc)
        {
            var models = doc.Models.Cast<Model>()
                .Select(m => !string.IsNullOrWhiteSpace(m.SourceFileName) ? m.SourceFileName : m.FileName);
            return DashboardProject.Resolve(doc.FileName ?? "", models);
        }

        private static (DashboardProject project, DashboardRegistry registry) DashboardContext()
        {
            var project = DashboardProjectOf(ActiveDoc());
            return (project, new DashboardRegistry(project.RegistryPath));
        }

        private static DashboardConfig FindDashboard(DashboardRegistry registry, JObject p) =>
            registry.Get(Str(p, "dashboard"))
            ?? throw new Exception($"Dashboard '{Str(p, "dashboard")}' not found. Use navis_dashboard_list.");

        private static JObject DashboardJsonOf(DashboardConfig cfg) =>
            JObject.Parse(JsonConvert.SerializeObject(cfg, DashboardJson.Settings));

        private static JObject DashboardCatalog() => new JObject
        {
            ["visuals"] = new JArray(VisualCatalog.Entries.Select(e => new JObject
            {
                ["type"] = e.Key,
                ["name"] = e.DisplayName,
                ["group"] = e.Group,
                ["roles"] = new JArray(e.Roles.Select(r => new JObject
                {
                    ["role"] = r.Name, ["name"] = r.DisplayName, ["kind"] = r.Kind.ToString(), ["multiple"] = r.Multiple,
                })),
            })),
            ["fields"] = new JArray(NavisDataModel.Fields().Select(f => new JObject
            {
                ["field"] = f.field, ["measure"] = f.isMeasure, ["table"] = f.table,
            })),
            ["note"] = "The 3D viewer is not a catalog visual: toggle Viewer.Enabled and each page's ShowViewer.",
        };

        private static JObject DashboardList()
        {
            var (project, registry) = DashboardContext();
            return new JObject
            {
                ["project"] = project.DisplayName,
                ["registry"] = project.RegistryPath,
                ["projectSaved"] = project.IsSaved,
                ["dashboards"] = new JArray(registry.List().Select(d => new JObject
                {
                    ["id"] = d.Id,
                    ["name"] = d.Name,
                    ["description"] = d.Description,
                    ["origin"] = d.Origin,
                    ["created"] = d.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    ["modified"] = d.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    ["pages"] = d.Pages.Count,
                    ["visuals"] = d.Pages.Sum(pg => pg.Visuals.Count),
                    ["viewer"] = d.Viewer.Enabled,
                    ["lastExport"] = d.LastExport == null ? null : new JObject
                    {
                        ["when"] = d.LastExport.ExportedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                        ["pbit"] = d.LastExport.PbitPath,
                    },
                })),
            };
        }

        private static JObject DashboardGet(JObject p)
        {
            var (_, registry) = DashboardContext();
            return new JObject { ["dashboard"] = DashboardJsonOf(FindDashboard(registry, p)) };
        }

        private static JObject DashboardCreate(JObject p)
        {
            var (_, registry) = DashboardContext();
            var template = Str(p, "template", "default").ToLowerInvariant();
            var name = Str(p, "name");
            if (string.IsNullOrWhiteSpace(name)) name = registry.UniqueName(template == "default" ? "Default Dashboard" : "Custom Dashboard");

            DashboardConfig cfg;
            if (p["config"] is JObject supplied)
            {
                cfg = supplied.ToObject<DashboardConfig>(JsonSerializer.Create(DashboardJson.Settings))!;
                cfg.Id = Guid.NewGuid().ToString("N");
                cfg.Name = name;
                cfg.CreatedUtc = DateTime.UtcNow;
                cfg.LastExport = null;
            }
            else cfg = template == "blank" || template == "custom"
                ? DefaultDashboardTemplate.CreateBlank(name)
                : DefaultDashboardTemplate.CreateDefault(name);
            if (Str(p, "description") != "") cfg.Description = Str(p, "description");

            ValidateDashboard(cfg);
            registry.Save(cfg);
            return new JObject { ["created"] = true, ["dashboard"] = DashboardJsonOf(cfg) };
        }

        /// <summary>{ dashboard, config (full DashboardConfig JSON), saveAsCopy?, newName? }</summary>
        private static JObject DashboardUpdate(JObject p)
        {
            var (_, registry) = DashboardContext();
            var existing = FindDashboard(registry, p);
            if (!(p["config"] is JObject supplied)) throw new Exception("config is required (the full dashboard JSON from navis_dashboard_get, edited).");
            var cfg = supplied.ToObject<DashboardConfig>(JsonSerializer.Create(DashboardJson.Settings))!;
            ValidateDashboard(cfg);

            if (Bool(p, "saveAsCopy"))
            {
                cfg.Id = existing.Id; // SaveAsCopy assigns the new id
                var copy = registry.SaveAsCopy(cfg, Str(p, "newName"));
                return new JObject { ["savedAsCopy"] = true, ["dashboard"] = DashboardJsonOf(copy) };
            }
            cfg.Id = existing.Id;
            cfg.CreatedUtc = existing.CreatedUtc;
            cfg.LastExport = existing.LastExport; // export state belongs to the files, not the edit
            registry.Save(cfg);
            return new JObject { ["updated"] = true, ["dashboard"] = DashboardJsonOf(cfg) };
        }

        private static JObject DashboardDelete(JObject p)
        {
            var (_, registry) = DashboardContext();
            var cfg = FindDashboard(registry, p);
            registry.Delete(cfg.Id);
            return new JObject { ["deleted"] = cfg.Name, ["note"] = "Exported .pbit and data files were left on disk." };
        }

        /// <summary>{ dashboard, force?, scope?, selectionSet?, refreshMode?, outputFolder?, includeGeometry?, includeClashes? }</summary>
        private static JObject DashboardExport(JObject p)
        {
            var doc = ActiveDoc();
            var project = DashboardProjectOf(doc);
            var registry = new DashboardRegistry(project.RegistryPath);
            var cfg = FindDashboard(registry, p);
            var o = cfg.Export;
            if (Str(p, "scope") != "") o.Scope = Str(p, "scope");
            if (Str(p, "selectionSet") != "") o.SelectionSet = Str(p, "selectionSet");
            if (Str(p, "refreshMode") != "") o.RefreshMode = Str(p, "refreshMode");
            if (Str(p, "outputFolder") != "") o.OutputFolder = Str(p, "outputFolder");
            if (p["includeGeometry"] != null) o.IncludeGeometry = Bool(p, "includeGeometry");
            if (p["includeClashes"] != null) o.IncludeClashes = Bool(p, "includeClashes");

            DashboardExportResult r;
            using (var progress = new NavisProgress($"BIM-Bot — exporting dashboard '{cfg.Name}'"))
            {
                try { r = DashboardExportService.Export(doc, project, registry, cfg, progress, Bool(p, "force")); }
                catch (OperationCanceledException) { throw new Exception("Dashboard export cancelled."); }
            }
            var res = new JObject
            {
                ["dashboard"] = cfg.Name,
                ["action"] = r.Action,
                ["pbit"] = r.PbitPath,
                ["dataFolder"] = r.DataFolder,
                ["elements"] = r.Elements,
                ["geometryRows"] = r.GeometryRows,
                ["properties"] = r.Properties,
                ["clashes"] = r.Clashes,
                ["dataChanged"] = r.DataChanged,
                ["layoutChanged"] = r.LayoutChanged,
                ["seconds"] = Math.Round(r.Duration.TotalSeconds, 1),
                ["next"] = r.Action == "dataOnly"
                    ? "Report unchanged: press Refresh in Power BI to load the new data."
                    : "Open the .pbit in Power BI Desktop, then File > Save as .pbix.",
            };
            if (r.Warning != "") res["warning"] = r.Warning;
            return res;
        }

        /// <summary>Fails early (with the visual named) on unknown types and malformed fields.</summary>
        internal static void ValidateDashboard(DashboardConfig cfg)
        {
            if (string.IsNullOrWhiteSpace(cfg.Name)) throw new Exception("Dashboard name is required.");
            if (cfg.Pages.Count == 0) throw new Exception("A dashboard needs at least one page.");
            var known = NavisDataModel.Fields().Select(f => f.field).ToList();
            foreach (var page in cfg.Pages)
                foreach (var v in page.Visuals)
                {
                    VisualCatalog.Get(v.Type);
                    foreach (var f in v.Fields.Values.SelectMany(x => x))
                    {
                        LayoutBuilder.Parse(f, out var table, out var name, out var isMeasure);
                        var canonical = isMeasure ? $"{table}.[{name}]" : $"{table}.{name}";
                        if (!known.Contains(canonical))
                            throw new Exception($"Visual '{v.Title}' on page '{page.Name}' uses unknown field '{f}'. Use navis_dashboard_catalog for the field list.");
                    }
                }
        }
    }
}
