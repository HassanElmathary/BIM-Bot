using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BIMBotNavisPlugin.Dashboard.Config;
using Newtonsoft.Json;

namespace BIMBotNavisPlugin.Dashboard
{
    /// <summary>The default dashboard (Templates/DefaultDashboard.json, embedded) and the blank starting point.</summary>
    public static class DefaultDashboardTemplate
    {
        public static DashboardConfig CreateDefault(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            var res = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("DefaultDashboard.json", StringComparison.OrdinalIgnoreCase))
                      ?? throw new InvalidOperationException("Default dashboard template is missing from the plugin build.");
            string json;
            using (var s = asm.GetManifestResourceStream(res)!)
            using (var r = new StreamReader(s)) json = r.ReadToEnd();
            var cfg = JsonConvert.DeserializeObject<DashboardConfig>(json, DashboardJson.Settings)!;
            cfg.Id = Guid.NewGuid().ToString("N");
            cfg.Name = name;
            cfg.CreatedUtc = cfg.UpdatedUtc = DateTime.UtcNow;
            cfg.Origin = "default";
            return cfg;
        }

        /// <summary>Blank custom dashboard: one page with the 3D viewer and a title, nothing else.</summary>
        public static DashboardConfig CreateBlank(string name) => new DashboardConfig
        {
            Name = name,
            Origin = "custom",
            Pages =
            {
                new PageSpec
                {
                    Name = "Dashboard",
                    ShowViewer = true,
                    Visuals = { new VisualSpec { Type = "text", Title = "{{PROJECT_NAME}} — " + name } },
                },
            },
        };
    }
}
