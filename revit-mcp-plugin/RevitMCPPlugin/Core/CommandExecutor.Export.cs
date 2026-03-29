using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Export/Import tool implementations: PDF, DWG, DWF, DGN, IFC, NWC, images,
    /// schedule data, parameters CSV, multi-format export manager.
    /// </summary>
    public static partial class CommandExecutor
    {
        // ===== OFFLINE TOOL IMPLEMENTATIONS =====

        private static JToken ExportToPdf(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            // Collect views/sheets to export â€” respect selection from Export Manager
            var viewIds = new List<ElementId>();

            var sheetIdStr = parameters?["sheetIds"]?.ToString();
            if (!string.IsNullOrWhiteSpace(sheetIdStr))
            {
                foreach (var idStr in sheetIdStr.Split(','))
                {
                    if (long.TryParse(idStr.Trim(), out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is ViewSheet vs && !vs.IsPlaceholder)
                            viewIds.Add(vs.Id);
                    }
                }
            }

            var viewIdStr = parameters?["viewIds"]?.ToString();
            if (!string.IsNullOrWhiteSpace(viewIdStr))
            {
                foreach (var idStr in viewIdStr.Split(','))
                {
                    if (long.TryParse(idStr.Trim(), out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is View v && !v.IsTemplate && v.CanBePrinted)
                            viewIds.Add(v.Id);
                    }
                }
            }

            // Fallback: if no specific selection, export all sheets
            if (viewIds.Count == 0)
            {
                var allSheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsPlaceholder)
                    .ToList();
                if (allSheets.Count == 0)
                    return new JObject { ["message"] = "No sheets found in the project." };
                viewIds = allSheets.Select(s => s.Id).ToList();
            }

            // Use Revit PDF export (Revit 2022+)
            try
            {
                var pdfOptions = new PDFExportOptions();
                pdfOptions.FileName = doc.Title ?? "Export";

                // Read format settings from parameters
                pdfOptions.Combine = parameters?["combine"]?.ToString() == "true";

                var rasterQuality = parameters?["rasterQuality"]?.ToString();
                if (!string.IsNullOrWhiteSpace(rasterQuality))
                {
                    switch (rasterQuality.ToLower())
                    {
                        case "low": pdfOptions.RasterQuality = RasterQualityType.Low; break;
                        case "medium": pdfOptions.RasterQuality = RasterQualityType.Medium; break;
                        case "high": pdfOptions.RasterQuality = RasterQualityType.High; break;
                    }
                }

                var colorMode = parameters?["color"]?.ToString();
                if (!string.IsNullOrWhiteSpace(colorMode))
                {
                    switch (colorMode.ToLower())
                    {
                        case "color": pdfOptions.ColorDepth = ColorDepthType.Color; break;
                        case "grayscale": pdfOptions.ColorDepth = ColorDepthType.GrayScale; break;
                        case "black & white": pdfOptions.ColorDepth = ColorDepthType.BlackLine; break;
                    }
                }

                // Hidden line processing (HiddenLineViewsExportAs not available in Revit 2025)

                // Hide options
                if (parameters?["hideScopeBox"]?.ToString() == "true")
                    pdfOptions.HideScopeBoxes = true;
                if (parameters?["hideRefPlane"]?.ToString() == "true")
                    pdfOptions.HideReferencePlane = true;
                if (parameters?["hideCropBoundary"]?.ToString() == "true")
                    pdfOptions.HideCropBoundaries = true;

                // Paper placement
                var placement = parameters?["paperPlacement"]?.ToString();
                if (placement == "center")
                    pdfOptions.PaperPlacement = PaperPlacementType.Center;
                else if (placement == "offset")
                    pdfOptions.PaperPlacement = PaperPlacementType.LowerLeft;

                // Zoom
                var zoom = parameters?["zoom"]?.ToString();
                if (zoom == "fitToPage")
                    pdfOptions.ZoomType = ZoomType.FitToPage;
                else if (zoom == "zoom")
                    pdfOptions.ZoomType = ZoomType.Zoom;

                doc.Export(outputFolder, viewIds, pdfOptions);

                return new JObject
                {
                    ["message"] = $"âœ… Exported {viewIds.Count} view/sheet(s) to PDF.\nOutput folder: {outputFolder}",
                    ["count"] = viewIds.Count,
                    ["outputFolder"] = outputFolder
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"PDF export error: {ex.Message}\nMake sure a PDF printer is installed." };
            }
        }

        private static JToken ExportToImages(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            // Respect selection from Export Manager
            var selectedIds = new List<ElementId>();
            var sheetIdStr = parameters?["sheetIds"]?.ToString();
            if (!string.IsNullOrWhiteSpace(sheetIdStr))
            {
                foreach (var idStr in sheetIdStr.Split(','))
                    if (long.TryParse(idStr.Trim(), out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is ViewSheet) selectedIds.Add(elem.Id);
                    }
            }
            var viewIdStr = parameters?["viewIds"]?.ToString();
            if (!string.IsNullOrWhiteSpace(viewIdStr))
            {
                foreach (var idStr in viewIdStr.Split(','))
                    if (long.TryParse(idStr.Trim(), out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is View v && !v.IsTemplate && v.CanBePrinted) selectedIds.Add(v.Id);
                    }
            }

            // Fallback: all printable views
            if (selectedIds.Count == 0)
            {
                selectedIds = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.CanBePrinted)
                    .Take(50)
                    .Select(v => v.Id)
                    .ToList();
            }

            int exported = 0;
            foreach (var vid in selectedIds.Take(50))
            {
                try
                {
                    var view = doc.GetElement(vid) as View;
                    if (view == null) continue;
                    var imgOpts = new ImageExportOptions
                    {
                        FilePath = System.IO.Path.Combine(outputFolder, CleanFileName(view.Name)),
                        FitDirection = FitDirectionType.Horizontal,
                        HLRandWFViewsFileType = ImageFileType.PNG,
                        ShadowViewsFileType = ImageFileType.PNG,
                        PixelSize = 2048,
                        ZoomType = ZoomFitType.FitToPage,
                        ExportRange = ExportRange.SetOfViews,
                    };
                    imgOpts.SetViewsAndSheets(new List<ElementId> { vid });
                    doc.ExportImage(imgOpts);
                    exported++;
                }
                catch (Exception ex) { Logger.Log($"Image export skipped for view: {ex.Message}"); }
            }

            return new JObject
            {
                ["message"] = $"âœ… Exported {exported} view(s) as images.\nOutput folder: {outputFolder}",
                ["count"] = exported,
                ["outputFolder"] = outputFolder
            };
        }

        private static JToken ExportToIfc(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            try
            {
                var ifcOpts = new IFCExportOptions();

                // If a specific view is selected, export only that view
                var viewIdStr = parameters?["viewIds"]?.ToString();
                var sheetIdStr = parameters?["sheetIds"]?.ToString();
                var idStr = !string.IsNullOrWhiteSpace(sheetIdStr) ? sheetIdStr : viewIdStr;
                if (!string.IsNullOrWhiteSpace(idStr))
                {
                    var firstId = idStr.Split(',').FirstOrDefault()?.Trim();
                    if (long.TryParse(firstId, out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is View v)
                            ifcOpts.FilterViewId = v.Id;
                    }
                }

                var fileName = System.IO.Path.GetFileNameWithoutExtension(doc.Title ?? "Export") + ".ifc";
                using (var t = new Transaction(doc, "Export IFC"))
                {
                    t.Start();
                    doc.Export(outputFolder, fileName, ifcOpts);
                    t.Commit();
                }
                return new JObject
                {
                    ["message"] = $"âœ… Exported IFC to: {System.IO.Path.Combine(outputFolder, fileName)}",
                    ["outputFolder"] = outputFolder
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"IFC export error: {ex.Message}" };
            }
        }

        private static JToken ExportToDgn(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            try
            {
                // Respect selection from Export Manager
                var viewIds = new List<ElementId>();
                var sheetIdStr = parameters?["sheetIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(sheetIdStr))
                {
                    foreach (var idStr in sheetIdStr.Split(','))
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is ViewSheet) viewIds.Add(elem.Id);
                        }
                }
                var viewIdStr = parameters?["viewIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(viewIdStr))
                {
                    foreach (var idStr in viewIdStr.Split(','))
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is View v && !v.IsTemplate && v.CanBePrinted) viewIds.Add(v.Id);
                        }
                }

                // Fallback: all printable views
                if (viewIds.Count == 0)
                {
                    viewIds = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => !v.IsTemplate && v.CanBePrinted)
                        .Select(v => v.Id)
                        .ToList();
                }

                var dgnOpts = new DGNExportOptions();
                var fileName = System.IO.Path.GetFileNameWithoutExtension(doc.Title ?? "Export");
                doc.Export(outputFolder, fileName, viewIds, dgnOpts);

                return new JObject
                {
                    ["message"] = $"âœ… Exported {viewIds.Count} view(s) to DGN.\nOutput folder: {outputFolder}",
                    ["count"] = viewIds.Count
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"DGN export error: {ex.Message}" };
            }
        }

        private static JToken ExportToDwg(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            try
            {
                // Collect specified views/sheets, or fall back to active view only
                var viewIds = new List<ElementId>();

                var viewIdStr = parameters?["viewIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(viewIdStr))
                {
                    foreach (var idStr in viewIdStr.Split(','))
                    {
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is View v && !v.IsTemplate && v.CanBePrinted)
                                viewIds.Add(v.Id);
                        }
                    }
                }

                var sheetIdStr = parameters?["sheetIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(sheetIdStr))
                {
                    foreach (var idStr in sheetIdStr.Split(','))
                    {
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is ViewSheet)
                                viewIds.Add(elem.Id);
                        }
                    }
                }

                // If no specific views provided, use active view
                if (viewIds.Count == 0)
                {
                    var activeView = doc.ActiveView;
                    if (activeView != null && !activeView.IsTemplate && activeView.CanBePrinted)
                        viewIds.Add(activeView.Id);
                }

                if (viewIds.Count == 0)
                    return new JObject { ["message"] = "âš  No exportable views found." };

                var dwgOpts = new DWGExportOptions();

                // Read hide options from parameters (default true)
                dwgOpts.HideScopeBox = parameters?["hideScopeBox"]?.ToString() != "false";
                dwgOpts.HideReferencePlane = parameters?["hideRefPlane"]?.ToString() != "false";

                int exported = 0;
                foreach (var vid in viewIds.Take(50))
                {
                    try
                    {
                        var view = doc.GetElement(vid) as View;
                        if (view == null) continue;
                        var ids = new List<ElementId> { vid };
                        var cleanName = CleanFileName(view.Name);

                        doc.Export(outputFolder, cleanName, ids, dwgOpts);

                        // NOTE: Revit generates companion files (.tif, .jpg, .png) as raster image
                        // references alongside the DWG. These MUST be kept or images won't display.
                        // Only clean up PCP plot config files.
                        var mainDwg = System.IO.Path.Combine(outputFolder, cleanName + ".dwg");
                        foreach (var file in System.IO.Directory.GetFiles(outputFolder, cleanName + ".pcp"))
                        {
                            try { System.IO.File.Delete(file); } catch (Exception ex) { Logger.Log($"Error: {ex.Message}"); }
                        }

                        exported++;
                    }
                    catch (Exception ex) { Logger.Log($"DWG export failed for view: {ex.Message}"); }
                }

                return new JObject
                {
                    ["message"] = $"âœ… Exported {exported} view(s) to DWG.\nOutput folder: {outputFolder}",
                    ["count"] = exported
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"DWG export error: {ex.Message}" };
            }
        }

        private static JToken ExportMultiFormat(Document doc, JObject parameters)
        {
            var formatsStr = parameters?["formats"]?.ToString() ?? "PDF";
            var formats = formatsStr.Split(',').Select(f => f.Trim().ToUpper()).Where(f => !string.IsNullOrEmpty(f)).ToList();

            var results = new List<string>();
            foreach (var fmt in formats)
            {
                try
                {
                    JToken result;
                    switch (fmt)
                    {
                        case "PDF":
                            result = ExportToPdf(doc, parameters);
                            break;
                        case "DWG":
                            result = ExportToDwg(doc, parameters);
                            break;
                        case "DGN":
                            result = ExportToDgn(doc, parameters);
                            break;
                        case "DWF":
                            result = ExportToDwf(doc, parameters);
                            break;
                        case "NWC":
                            result = ExportToNwc(doc, parameters);
                            break;
                        case "IFC":
                            result = ExportToIfc(doc, parameters);
                            break;
                        case "IMG":
                            result = ExportToImages(doc, parameters);
                            break;
                        default:
                            results.Add($"âš ï¸ Unknown format: {fmt}");
                            continue;
                    }
                    var msg = result?["message"]?.ToString() ?? $"Exported {fmt}";
                    results.Add(msg);
                }
                catch (Exception ex)
                {
                    results.Add($"âŒ {fmt} error: {ex.Message}");
                }
            }

            return new JObject
            {
                ["message"] = string.Join("\n\n", results),
                ["formats"] = formats.Count
            };
        }

        private static JToken ExportToDwf(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            try
            {
                // Respect selection from Export Manager
                var selectedIds = new List<ElementId>();
                var sheetIdStr = parameters?["sheetIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(sheetIdStr))
                {
                    foreach (var idStr in sheetIdStr.Split(','))
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is ViewSheet) selectedIds.Add(elem.Id);
                        }
                }
                var viewIdStr = parameters?["viewIds"]?.ToString();
                if (!string.IsNullOrWhiteSpace(viewIdStr))
                {
                    foreach (var idStr in viewIdStr.Split(','))
                        if (long.TryParse(idStr.Trim(), out var id))
                        {
                            var elem = doc.GetElement(new ElementId(id));
                            if (elem is View v && !v.IsTemplate && v.CanBePrinted) selectedIds.Add(v.Id);
                        }
                }

                // Fallback: all printable views
                if (selectedIds.Count == 0)
                {
                    selectedIds = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => !v.IsTemplate && v.CanBePrinted)
                        .Select(v => v.Id)
                        .ToList();
                }

                var dwfOpts = new DWFXExportOptions();
                dwfOpts.MergedViews = true;
                int exported = 0;
                foreach (var vid in selectedIds.Take(50))
                {
                    try
                    {
                        var view = doc.GetElement(vid) as View;
                        if (view == null) continue;
                        var viewSet = new ViewSet();
                        viewSet.Insert(view);
                        doc.Export(outputFolder, CleanFileName(view.Name), viewSet, dwfOpts);
                        exported++;
                    }
                    catch (Exception ex) { Logger.Log($"DWF export failed for view: {ex.Message}"); }
                }

                return new JObject
                {
                    ["message"] = $"âœ… Exported {exported} view(s) to DWF.\nOutput folder: {outputFolder}",
                    ["count"] = exported
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"DWF export error: {ex.Message}" };
            }
        }

        private static JToken ExportToNwc(Document doc, JObject parameters)
        {
            var outputFolder = parameters?["outputFolder"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitExport");
            System.IO.Directory.CreateDirectory(outputFolder);

            try
            {
                var nwcOpts = new NavisworksExportOptions();
                nwcOpts.Parameters = NavisworksParameters.All;

                // If a specific view is selected, export just that view
                var viewIdStr = parameters?["viewIds"]?.ToString();
                var sheetIdStr = parameters?["sheetIds"]?.ToString();
                var idStr = !string.IsNullOrWhiteSpace(sheetIdStr) ? sheetIdStr : viewIdStr;
                if (!string.IsNullOrWhiteSpace(idStr))
                {
                    var firstId = idStr.Split(',').FirstOrDefault()?.Trim();
                    if (long.TryParse(firstId, out var id))
                    {
                        var elem = doc.GetElement(new ElementId(id));
                        if (elem is View v)
                        {
                            nwcOpts.ExportScope = NavisworksExportScope.View;
                            nwcOpts.ViewId = v.Id;
                        }
                    }
                }
                else
                {
                    nwcOpts.ExportScope = NavisworksExportScope.Model;
                }

                var fn = parameters?["fileName"]?.ToString();
                var fileName = fn ?? System.IO.Path.GetFileNameWithoutExtension(doc.Title ?? "Export");
                doc.Export(outputFolder, fileName, nwcOpts);

                return new JObject
                {
                    ["message"] = $"âœ… Exported NWC to: {System.IO.Path.Combine(outputFolder, fileName + ".nwc")}",
                    ["outputFolder"] = outputFolder
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"NWC export error: {ex.Message}\nNavisworks exporter must be installed." };
            }
        }

        private static JToken ImportParametersFromCsv(Document doc, JObject parameters)
        {
            var filePath = parameters?["file"]?.ToString();
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
                return new JObject { ["message"] = $"CSV file not found: {filePath ?? "(not specified)"}" };

            try
            {
                var lines = System.IO.File.ReadAllLines(filePath);
                if (lines.Length < 2)
                    return new JObject { ["message"] = "CSV file is empty or has no data rows." };

                var headers = lines[0].Split(',');
                int updated = 0, skipped = 0;

                using (var t = new Transaction(doc, "Import Parameters from CSV"))
                {
                    t.Start();
                    for (int row = 1; row < lines.Length; row++)
                    {
                        var vals = lines[row].Split(',');
                        if (vals.Length < 2) continue;

                        // First column = ElementId
                        if (!int.TryParse(vals[0].Trim('"').Trim(), out int elemId)) { skipped++; continue; }
                        var elem = doc.GetElement(new ElementId(elemId));
                        if (elem == null) { skipped++; continue; }

                        for (int col = 1; col < headers.Length && col < vals.Length; col++)
                        {
                            var paramName = headers[col].Trim('"').Trim();
                            var value = vals[col].Trim('"').Trim();
                            if (string.IsNullOrEmpty(paramName)) continue;

                            var p = elem.LookupParameter(paramName);
                            if (p == null || p.IsReadOnly) continue;

                            try
                            {
                                if (p.StorageType == StorageType.String) p.Set(value);
                                else if (p.StorageType == StorageType.Integer && int.TryParse(value, out int iv)) p.Set(iv);
                                else if (p.StorageType == StorageType.Double && double.TryParse(value, out double dv)) p.Set(dv);
                                else p.SetValueString(value);
                                updated++;
                            }
                            catch (Exception ex) { Logger.Log($"CSV import skipped col '{headers[col]}' on element {elemId}: {ex.Message}"); skipped++; }
                        }
                    }
                    t.Commit();
                }

                return new JObject
                {
                    ["message"] = $"âœ… Imported CSV: {updated} parameter(s) updated, {skipped} skipped.\nFile: {filePath}",
                    ["updated"] = updated,
                    ["skipped"] = skipped
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["message"] = $"CSV import error: {ex.Message}" };
            }
        }
    }
}
