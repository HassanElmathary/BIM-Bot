using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BIMBotNavisPlugin.Dashboard.Export
{
    /// <summary>
    /// Writes a ready-to-open Power BI template (.pbit) for a Navis dashboard. Package format as
    /// the Revit PbitGenerator (verified against Microsoft-published templates):
    ///   Version, Settings, Metadata, DiagramLayout, DataModelSchema, Report/Layout  UTF-16 LE, no BOM
    ///   [Content_Types].xml                                                        UTF-8
    ///   Report/CustomVisuals/&lt;guid&gt;/                                                the BIM-Bot 3D Viewer
    /// The data folder is baked into the M queries, so the template opens with no prompts.
    /// Power BI Desktop cannot be driven to write a .pbix: open the .pbit, then File > Save as .pbix.
    /// </summary>
    public sealed class NavisPbitGenerator
    {
        /// <summary>BIM-Bot 3D Viewer custom visual GUID (shared with the Revit export; must match pbiviz.json).</summary>
        public const string VisualGuid = "bimBot3DViewer1A2B3C4D";
        private const string PbivizResourceSuffix = VisualGuid + ".pbiviz";

        private static readonly Encoding Utf16NoBom = new UnicodeEncoding(false, false);
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private static readonly string[] RequiredParts =
            { "Version", "Settings", "Metadata", "DiagramLayout", "DataModelSchema", "Report/Layout", "[Content_Types].xml" };

        public void Generate(string pbitPath, string schemaJson, string layoutJson, string description)
        {
            var dir = Path.GetDirectoryName(pbitPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = pbitPath + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);

            using (var stream = new FileStream(tmp, FileMode.CreateNew))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                Entry(zip, "Version", "1.28", Utf16NoBom);
                Entry(zip, "Settings", "{\"Version\":4,\"ReportSettings\":{},\"QueriesSettings\":{}}", Utf16NoBom);
                Entry(zip, "Metadata",
                    "{\"Version\":5,\"AutoCreatedRelationships\":[],\"FileDescription\":" +
                    Newtonsoft.Json.JsonConvert.ToString(description ?? "") +
                    ",\"CreatedFrom\":\"Cloud\",\"CreatedFromRelease\":\"2021.02\"}", Utf16NoBom);
                Entry(zip, "DiagramLayout", "{\"version\":\"1.1.0\",\"diagrams\":[]}", Utf16NoBom);
                Entry(zip, "DataModelSchema", schemaJson, Utf16NoBom);
                Entry(zip, "Report/Layout", layoutJson, Utf16NoBom);
                Entry(zip, "[Content_Types].xml", ContentTypes, Utf8NoBom);
                EmbedViewer(zip);
            }

            Verify(tmp);
            // Replace only after a verified write: a locked or half-written .pbit never replaces a good one.
            if (File.Exists(pbitPath)) File.Delete(pbitPath);
            File.Move(tmp, pbitPath);
        }

        private static void Verify(string path)
        {
            var missing = new List<string>();
            using (var zip = ZipFile.OpenRead(path))
            {
                var names = new HashSet<string>(zip.Entries.Select(e => e.FullName), StringComparer.OrdinalIgnoreCase);
                missing.AddRange(RequiredParts.Where(p => !names.Contains(p)));
                if (!zip.Entries.Any(e => e.FullName.StartsWith($"Report/CustomVisuals/{VisualGuid}/", StringComparison.OrdinalIgnoreCase) && e.Name != ""))
                    missing.Add("3D Viewer custom visual");
            }
            if (missing.Count > 0)
            {
                try { File.Delete(path); } catch { }
                throw new InvalidOperationException("Generated .pbit failed verification — missing: " + string.Join(", ", missing));
            }
        }

        private static void EmbedViewer(ZipArchive pbit)
        {
            var asm = Assembly.GetExecutingAssembly();
            var res = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(PbivizResourceSuffix, StringComparison.OrdinalIgnoreCase))
                      ?? throw new InvalidOperationException("The BIM-Bot 3D Viewer (.pbiviz) is missing from the plugin build.");
            using (var s = asm.GetManifestResourceStream(res)!)
            using (var pbiviz = new ZipArchive(s, ZipArchiveMode.Read))
            {
                foreach (var e in pbiviz.Entries)
                {
                    if (string.IsNullOrEmpty(e.Name)) continue;
                    var target = pbit.CreateEntry($"Report/CustomVisuals/{VisualGuid}/{e.FullName.Replace('\\', '/')}");
                    using (var src = e.Open())
                    using (var dst = target.Open()) src.CopyTo(dst);
                }
            }
        }

        private static void Entry(ZipArchive zip, string path, string content, Encoding enc)
        {
            using (var s = zip.CreateEntry(path).Open())
            {
                var b = enc.GetBytes(content);
                s.Write(b, 0, b.Length);
            }
        }

        private const string ContentTypes =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"json\" ContentType=\"\" />" +
            "<Override PartName=\"/Version\" ContentType=\"\" />" +
            "<Override PartName=\"/DataModelSchema\" ContentType=\"\" />" +
            "<Override PartName=\"/DiagramLayout\" ContentType=\"\" />" +
            "<Override PartName=\"/Report/Layout\" ContentType=\"\" />" +
            "<Override PartName=\"/Settings\" ContentType=\"application/json\" />" +
            "<Override PartName=\"/Metadata\" ContentType=\"application/json\" />" +
            "</Types>";
    }
}
