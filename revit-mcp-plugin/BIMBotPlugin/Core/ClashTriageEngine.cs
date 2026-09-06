using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    public static partial class CommandExecutor
    {
        private static JToken RunSmartClashTriage(Document doc, JObject parameters)
        {
            var sourceCategoryStr = parameters["sourceCategory"]?.ToString();
            var targetCategoryStr = parameters["targetCategory"]?.ToString();
            var sourceLink = parameters["sourceLink"]?.ToString();
            var targetLink = parameters["targetLink"]?.ToString();
            var levelName = parameters["levelName"]?.ToString();
            var toleranceMm = parameters["toleranceMm"]?.Value<double>() ?? 0;
            var toleranceFt = toleranceMm / 304.8;

            BuiltInCategory srcBic = GetBuiltInCategory(sourceCategoryStr ?? "");
            BuiltInCategory tgtBic = GetBuiltInCategory(targetCategoryStr ?? "");

            var srcDoc = GetDocumentFromLink(doc, sourceLink) ?? doc;
            var tgtDoc = GetDocumentFromLink(doc, targetLink) ?? doc;

            var srcTransform = GetLinkTransform(doc, sourceLink);
            var tgtTransform = GetLinkTransform(doc, targetLink);

            var srcElements = new FilteredElementCollector(srcDoc).OfCategory(srcBic).WhereElementIsNotElementType().ToList();
            var tgtElements = new FilteredElementCollector(tgtDoc).OfCategory(tgtBic).WhereElementIsNotElementType().ToList();

            if (!string.IsNullOrEmpty(levelName))
            {
                srcElements = srcElements.Where(e => e.LevelId != ElementId.InvalidElementId && srcDoc.GetElement(e.LevelId)?.Name == levelName).ToList();
                tgtElements = tgtElements.Where(e => e.LevelId != ElementId.InvalidElementId && tgtDoc.GetElement(e.LevelId)?.Name == levelName).ToList();
            }

            var clashes = new List<JObject>();

            foreach (var src in srcElements)
            {
                var srcBbOrig = src.get_BoundingBox(null);
                if (srcBbOrig == null) continue;
                
                var srcBb = TransformBoundingBox(srcBbOrig, srcTransform);
                ExpandBoundingBox(srcBb, toleranceFt);

                foreach (var tgt in tgtElements)
                {
                    if (srcDoc.Equals(tgtDoc) && src.Id == tgt.Id) continue;

                    var tgtBbOrig = tgt.get_BoundingBox(null);
                    if (tgtBbOrig == null) continue;

                    var tgtBb = TransformBoundingBox(tgtBbOrig, tgtTransform);

                    if (CheckAABBIntersection(srcBb, tgtBb))
                    {
                        var center = (srcBb.Min + srcBb.Max) / 2;
                        clashes.Add(new JObject
                        {
                            ["sourceId"] = src.Id.Val(),
                            ["targetId"] = tgt.Id.Val(),
                            ["x"] = center.X,
                            ["y"] = center.Y,
                            ["z"] = center.Z
                        });
                    }
                }
            }

            var clusters = new JArray();
            var processed = new HashSet<int>();
            for (int i = 0; i < clashes.Count; i++)
            {
                if (processed.Contains(i)) continue;
                
                var cluster = new JArray();
                cluster.Add(clashes[i]);
                processed.Add(i);

                for (int j = i + 1; j < clashes.Count; j++)
                {
                    if (processed.Contains(j)) continue;
                    
                    var p1 = new XYZ(clashes[i]["x"].Value<double>(), clashes[i]["y"].Value<double>(), clashes[i]["z"].Value<double>());
                    var p2 = new XYZ(clashes[j]["x"].Value<double>(), clashes[j]["y"].Value<double>(), clashes[j]["z"].Value<double>());
                    
                    if (p1.DistanceTo(p2) <= 3.0)
                    {
                        cluster.Add(clashes[j]);
                        processed.Add(j);
                    }
                }

                clusters.Add(new JObject
                {
                    ["clashCount"] = cluster.Count,
                    ["clashes"] = cluster,
                    ["clusterCenter"] = new JObject { ["x"] = clashes[i]["x"], ["y"] = clashes[i]["y"], ["z"] = clashes[i]["z"] }
                });
            }

            return new JObject
            {
                ["totalClashes"] = clashes.Count,
                ["clustersFound"] = clusters.Count,
                ["clusters"] = clusters
            };
        }

        private static JToken ExportBcfIssues(Document doc, JObject parameters)
        {
            var issues = parameters["issues"] as JArray;
            var outputFilePath = parameters["outputFilePath"]?.ToString() ?? Path.Combine(Path.GetTempPath(), "clashes.bcfzip");
            var bcfVersion = parameters["bcfVersion"]?.ToString() ?? "2.1";

            if (File.Exists(outputFilePath)) File.Delete(outputFilePath);

            using (var zipStream = new FileStream(outputFilePath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var verEntry = archive.CreateEntry("bcf.version");
                using (var writer = new StreamWriter(verEntry.Open()))
                {
                    writer.Write($"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Version VersionId=\"{bcfVersion}\" />");
                }

                if (issues != null)
                {
                    int i = 1;
                    foreach (var issue in issues)
                    {
                        var guid = Guid.NewGuid().ToString();
                        
                        var markupEntry = archive.CreateEntry($"{guid}/markup.bcf");
                        using (var writer = new StreamWriter(markupEntry.Open()))
                        {
                            writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Markup><Topic Guid=""{guid}""><Title>Clash Issue {i}</Title><CreationDate>{DateTime.UtcNow:O}</CreationDate></Topic></Markup>");
                        }

                        var viewpointEntry = archive.CreateEntry($"{guid}/viewpoint.bcfv");
                        using (var writer = new StreamWriter(viewpointEntry.Open()))
                        {
                            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?><VisualizationInfo><PerspectiveCamera><CameraViewPoint><X>0</X><Y>0</Y><Z>0</Z></CameraViewPoint><CameraDirection><X>0</X><Y>0</Y><Z>-1</Z></CameraDirection><CameraUpVector><X>0</X><Y>1</Y><Z>0</Z></CameraUpVector><FieldOfView>60</FieldOfView></PerspectiveCamera></VisualizationInfo>");
                        }
                        i++;
                    }
                }
            }

            return new JObject { ["bcfExportPath"] = outputFilePath };
        }

        private static Document GetDocumentFromLink(Document hostDoc, string linkName)
        {
            if (string.IsNullOrEmpty(linkName)) return null;
            var link = new FilteredElementCollector(hostDoc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().FirstOrDefault(l => l.Name.Contains(linkName));
            return link?.GetLinkDocument();
        }
        
        private static Transform GetLinkTransform(Document hostDoc, string linkName)
        {
            if (string.IsNullOrEmpty(linkName)) return Transform.Identity;
            var link = new FilteredElementCollector(hostDoc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().FirstOrDefault(l => l.Name.Contains(linkName));
            return link?.GetTotalTransform() ?? Transform.Identity;
        }

        /// <summary>
        /// Axis-aligned box of the transformed element. Transforming only Min and
        /// Max is wrong for any rotated or mirrored link: the transformed Min can
        /// land above the transformed Max on an axis, which silently inverts the
        /// box and makes every intersection test miss. All eight corners have to
        /// go through the transform, then re-bound.
        /// </summary>
        private static BoundingBoxXYZ TransformBoundingBox(BoundingBoxXYZ bb, Transform t)
        {
            // Always return a copy - callers mutate the result via ExpandBoundingBox,
            // and bb belongs to the element.
            var corners = new[]
            {
                new XYZ(bb.Min.X, bb.Min.Y, bb.Min.Z),
                new XYZ(bb.Min.X, bb.Min.Y, bb.Max.Z),
                new XYZ(bb.Min.X, bb.Max.Y, bb.Min.Z),
                new XYZ(bb.Min.X, bb.Max.Y, bb.Max.Z),
                new XYZ(bb.Max.X, bb.Min.Y, bb.Min.Z),
                new XYZ(bb.Max.X, bb.Min.Y, bb.Max.Z),
                new XYZ(bb.Max.X, bb.Max.Y, bb.Min.Z),
                new XYZ(bb.Max.X, bb.Max.Y, bb.Max.Z),
            };

            if (!t.IsIdentity)
            {
                for (int i = 0; i < corners.Length; i++)
                    corners[i] = t.OfPoint(corners[i]);
            }

            double minX = corners[0].X, minY = corners[0].Y, minZ = corners[0].Z;
            double maxX = minX, maxY = minY, maxZ = minZ;
            foreach (var c in corners)
            {
                if (c.X < minX) minX = c.X; else if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y; else if (c.Y > maxY) maxY = c.Y;
                if (c.Z < minZ) minZ = c.Z; else if (c.Z > maxZ) maxZ = c.Z;
            }

            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ)
            };
        }

        private static void ExpandBoundingBox(BoundingBoxXYZ bb, double amount)
        {
            bb.Min = new XYZ(bb.Min.X - amount, bb.Min.Y - amount, bb.Min.Z - amount);
            bb.Max = new XYZ(bb.Max.X + amount, bb.Max.Y + amount, bb.Max.Z + amount);
        }

        private static bool CheckAABBIntersection(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            return a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
                   a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
                   a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
        }
    }
}
