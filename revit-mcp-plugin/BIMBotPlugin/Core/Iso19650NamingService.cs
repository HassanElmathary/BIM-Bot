using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    public static partial class CommandExecutor
    {
        private static JToken AuditIso19650Naming(Document doc, JObject parameters)
        {
            var targetType = parameters["targetType"]?.ToString() ?? "views";
            var patternStr = parameters["pattern"]?.ToString();
            var includeLinks = parameters["includeLinks"]?.Value<bool>() ?? false;

            if (string.IsNullOrEmpty(patternStr))
            {
                switch (targetType.ToLowerInvariant())
                {
                    case "sheets":
                        patternStr = @"^[A-Z]{2,4}-[A-Z0-9]{2,4}-\d{2,4}$";
                        break;
                    case "views":
                        patternStr = @"^(?!.*Copy)(?!.*\([0-9]+\)).*$";
                        break;
                    case "families":
                        patternStr = @"^(?!.*Copy)(?!.*[A-Za-z0-9]+[ \-_][0-9]+$).*$"; 
                        break;
                    default:
                        patternStr = @".*";
                        break;
                }
            }

            var regex = new Regex(patternStr, RegexOptions.IgnoreCase);
            var violations = new JArray();

            IEnumerable<Element> elementsToAudit = new List<Element>();
            
            if (targetType == "sheets")
            {
                elementsToAudit = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>();
            }
            else if (targetType == "views")
            {
                elementsToAudit = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate);
            }
            else if (targetType == "families")
            {
                elementsToAudit = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>();
            }
            else if (targetType == "levels")
            {
                elementsToAudit = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>();
            }
            else if (targetType == "links")
            {
                elementsToAudit = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>();
            }

            foreach (var elem in elementsToAudit)
            {
                string nameToCheck = elem.Name;
                if (elem is ViewSheet vs) nameToCheck = vs.SheetNumber;

                if (!regex.IsMatch(nameToCheck))
                {
                    violations.Add(new JObject
                    {
                        ["elementId"] = elem.Id.Val(),
                        ["name"] = elem.Name,
                        ["checkedValue"] = nameToCheck,
                        ["category"] = elem.Category?.Name ?? targetType
                    });
                }
            }

            return new JObject
            {
                ["targetType"] = targetType,
                ["pattern"] = patternStr,
                ["totalChecked"] = elementsToAudit.Count(),
                ["violationCount"] = violations.Count,
                ["violations"] = violations
            };
        }
    }
}
