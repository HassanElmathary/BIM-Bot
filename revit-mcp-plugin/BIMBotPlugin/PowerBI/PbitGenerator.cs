using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.PowerBI
{
    /// <summary>
    /// Generates a ready-to-open Power BI template (.pbit) wired to the CSV
    /// files produced by <see cref="PowerBICsvWriter"/>.
    ///
    /// The .pbit is a zip package (verified against Microsoft-published
    /// templates) containing:
    ///   [Content_Types].xml            OPC content types (UTF-8)
    ///   Version, Settings, Metadata,
    ///   DiagramLayout, DataModelSchema UTF-16 LE JSON, no BOM
    ///   Report/Layout                  UTF-16 LE JSON, no BOM (legacy format —
    ///                                  opens in every Power BI Desktop version)
    ///   Report/CustomVisuals/&lt;guid&gt;/   the BIM-Bot 3D Viewer visual, unzipped
    ///
    /// The exported data folder path is baked into the M partition queries, so
    /// double-clicking the .pbit loads the model with zero prompts.
    /// </summary>
    public class PbitGenerator
    {
        /// <summary>GUID of the BIM-Bot 3D Viewer custom visual (must match pbiviz.json).</summary>
        public const string VisualGuid = "bimBot3DViewer1A2B3C4D";

        private const string PbivizResourceSuffix = ".pbiviz";
        private const string SchemaResourceSuffix = "DataModelSchema.template.json";
        private const string LayoutResourceSuffix = "ReportLayout.template.json";

        // UTF-16 LE without BOM — required encoding for pbit text parts.
        private static readonly Encoding Utf16NoBom = new UnicodeEncoding(false, false);
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Generate the .pbit file at <paramref name="pbitPath"/> pointing at
        /// CSVs in <paramref name="dataFolder"/>.
        /// </summary>
        public void Generate(string pbitPath, string dataFolder, string projectName)
        {
            var schema = LoadTemplate(SchemaResourceSuffix)
                .Replace("{{MODEL_GUID}}", Guid.NewGuid().ToString())
                .Replace("{{DATA_FOLDER}}", JsonEscape(dataFolder.TrimEnd('\\')));

            var layout = BuildLayout(projectName);

            var dir = Path.GetDirectoryName(pbitPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(pbitPath)) File.Delete(pbitPath);

            using (var stream = new FileStream(pbitPath, FileMode.CreateNew))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(zip, "Version", "1.28", Utf16NoBom);
                WriteEntry(zip, "Settings",
                    "{\"Version\":4,\"ReportSettings\":{},\"QueriesSettings\":{}}", Utf16NoBom);
                WriteEntry(zip, "Metadata",
                    "{\"Version\":5,\"AutoCreatedRelationships\":[],\"FileDescription\":\"BIM-Bot 3D Model Dashboard\",\"CreatedFrom\":\"Cloud\",\"CreatedFromRelease\":\"2021.02\"}",
                    Utf16NoBom);
                WriteEntry(zip, "DiagramLayout",
                    "{\"version\":\"1.1.0\",\"diagrams\":[]}", Utf16NoBom);
                WriteEntry(zip, "DataModelSchema", schema, Utf16NoBom);
                WriteEntry(zip, "Report/Layout", layout, Utf16NoBom);
                WriteEntry(zip, "[Content_Types].xml", BuildContentTypes(), Utf8NoBom);

                EmbedCustomVisual(zip);
            }
        }

        // ═══════════════════════════════════════════════════
        //  Report Layout
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// The template stores visual configs as readable JSON objects; the
        /// legacy Layout format requires config/filters as *stringified* JSON.
        /// Parse the template, then serialize those nested documents.
        /// </summary>
        private string BuildLayout(string projectName)
        {
            var raw = LoadTemplate(LayoutResourceSuffix)
                .Replace("{{VISUAL_GUID}}", VisualGuid)
                .Replace("{{PROJECT_NAME}}", JsonEscape(projectName));

            var layout = JObject.Parse(raw);

            StringifyProperty(layout, "config");
            StringifyProperty(layout, "filters");

            foreach (var section in layout["sections"] as JArray ?? new JArray())
            {
                StringifyProperty((JObject)section, "config");
                StringifyProperty((JObject)section, "filters");

                foreach (var vc in section["visualContainers"] as JArray ?? new JArray())
                {
                    StringifyProperty((JObject)vc, "config");
                    StringifyProperty((JObject)vc, "filters");
                }
            }

            return layout.ToString(Formatting.None);
        }

        /// <summary>Replace an object/array property with its JSON string form.</summary>
        private static void StringifyProperty(JObject obj, string name)
        {
            var token = obj[name];
            if (token == null || token.Type == JTokenType.String) return;
            obj[name] = token.ToString(Formatting.None);
        }

        // ═══════════════════════════════════════════════════
        //  Custom visual embedding
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Unzip the embedded .pbiviz (package.json + resources/*.pbiviz.json)
        /// into Report/CustomVisuals/&lt;guid&gt;/ inside the pbit.
        /// </summary>
        private void EmbedCustomVisual(ZipArchive pbit)
        {
            using (var pbivizStream = OpenResource(PbivizResourceSuffix))
            using (var pbiviz = new ZipArchive(pbivizStream, ZipArchiveMode.Read))
            {
                foreach (var entry in pbiviz.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // skip folder entries

                    var targetPath = $"Report/CustomVisuals/{VisualGuid}/{entry.FullName.Replace('\\', '/')}";
                    var target = pbit.CreateEntry(targetPath);
                    using (var src = entry.Open())
                    using (var dst = target.Open())
                    {
                        src.CopyTo(dst);
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  Package parts
        // ═══════════════════════════════════════════════════

        private static string BuildContentTypes()
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
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

        private static void WriteEntry(ZipArchive zip, string path, string content, Encoding encoding)
        {
            var entry = zip.CreateEntry(path);
            using (var stream = entry.Open())
            {
                var bytes = encoding.GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        // ═══════════════════════════════════════════════════
        //  Embedded resources
        // ═══════════════════════════════════════════════════

        private static string LoadTemplate(string suffix)
        {
            using (var stream = OpenResource(suffix))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static Stream OpenResource(string suffix)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

            if (name == null)
                throw new InvalidOperationException(
                    $"Embedded resource '*{suffix}' not found. The plugin build is missing the Power BI template assets.");

            return assembly.GetManifestResourceStream(name);
        }

        /// <summary>Escape a string for direct insertion inside a JSON string literal.</summary>
        private static string JsonEscape(string value)
        {
            var quoted = JsonConvert.ToString(value ?? "");
            return quoted.Substring(1, quoted.Length - 2);
        }
    }
}
