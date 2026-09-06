using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    public static partial class CommandExecutor
    {
        private static JToken ValidateIdsSpec(Document doc, JObject parameters)
        {
            var idsXmlContent = parameters["idsXmlContent"]?.ToString();
            var idsFilePath = parameters["idsFilePath"]?.ToString();
            var scope = parameters["scope"]?.ToString() ?? "host";
            var discipline = parameters["discipline"]?.ToString();

            if (string.IsNullOrEmpty(idsXmlContent) && !string.IsNullOrEmpty(idsFilePath))
            {
                idsXmlContent = System.IO.File.ReadAllText(idsFilePath);
            }

            if (string.IsNullOrEmpty(idsXmlContent))
                return new JObject { ["error"] = "No IDS XML content provided." };

            var xml = XDocument.Parse(idsXmlContent);
            var specs = xml.Root?.Element("specifications")?.Elements("specification") ?? Enumerable.Empty<XElement>();

            int totalElementsChecked = 0;
            int compliantCount = 0;
            int nonCompliantCount = 0;
            var violations = new JArray();

            foreach (var spec in specs)
            {
                var entityName = spec.Element("applicability")?.Element("entity")?.Element("name")?.Value;
                if (string.IsNullOrEmpty(entityName)) continue;

                BuiltInCategory bic = MapIfcEntityToCategory(entityName);
                if (bic == BuiltInCategory.INVALID) continue;

                var requirements = spec.Element("requirements")?.Elements("property") ?? Enumerable.Empty<XElement>();
                var elements = new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToList();
                
                foreach (var elem in elements)
                {
                    totalElementsChecked++;
                    bool isCompliant = true;

                    foreach (var req in requirements)
                    {
                        var propName = req.Element("baseName")?.Element("simpleValue")?.Value;
                        if (string.IsNullOrEmpty(propName)) continue;

                        var param = elem.LookupParameter(propName) ?? elem.Document.GetElement(elem.GetTypeId())?.LookupParameter(propName);
                        if (param == null || !param.HasValue || (param.StorageType == StorageType.String && string.IsNullOrEmpty(param.AsString())))
                        {
                            isCompliant = false;
                            violations.Add(new JObject
                            {
                                ["elementId"] = elem.Id.Val(),
                                ["elementName"] = elem.Name,
                                ["category"] = elem.Category?.Name,
                                ["missingParameter"] = propName
                            });
                        }
                    }

                    if (isCompliant) compliantCount++;
                    else nonCompliantCount++;
                }
            }

            return new JObject
            {
                ["message"] = "IDS validation complete.",
                ["totalChecked"] = totalElementsChecked,
                ["compliant"] = compliantCount,
                ["nonCompliant"] = nonCompliantCount,
                ["violations"] = violations
            };
        }

        private static BuiltInCategory MapIfcEntityToCategory(string ifcEntity)
        {
            switch (ifcEntity.ToLowerInvariant())
            {
                case "ifcwall": return BuiltInCategory.OST_Walls;
                case "ifcdoor": return BuiltInCategory.OST_Doors;
                case "ifcwindow": return BuiltInCategory.OST_Windows;
                case "ifccolumn": return BuiltInCategory.OST_StructuralColumns;
                case "ifcslab": return BuiltInCategory.OST_Floors;
                case "ifcroof": return BuiltInCategory.OST_Roofs;
                case "ifcbeam": return BuiltInCategory.OST_StructuralFraming;
                default: return BuiltInCategory.INVALID;
            }
        }
    }
}
