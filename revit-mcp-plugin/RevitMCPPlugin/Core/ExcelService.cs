using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// Full Excel control service (read/write/format) using ClosedXML.
    /// Files live in the _ProjectFiles folder next to the .rvt file.
    /// </summary>
    public static class ExcelService
    {
        // ── Create Workbook ──

        /// <summary>Create a new .xlsx workbook with optional sheets and headers.</summary>
        public static JToken CreateWorkbook(string projectFilePath, JObject parameters)
        {
            var folder = ProjectFilesService.GetProjectFilesFolder(projectFilePath);
            var fileName = parameters?["fileName"]?.ToString() ?? "Workbook.xlsx";
            if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                fileName += ".xlsx";

            var filePath = Path.Combine(folder, fileName);

            using (var wb = new XLWorkbook())
            {
                // Parse sheet definitions: either a simple array of names or objects with name+headers
                var sheetsParam = parameters?["sheets"];
                if (sheetsParam is JArray sheetsArr && sheetsArr.Count > 0)
                {
                    foreach (var sheetDef in sheetsArr)
                    {
                        string sheetName;
                        JArray? headers = null;

                        if (sheetDef.Type == JTokenType.String)
                        {
                            sheetName = sheetDef.ToString();
                        }
                        else if (sheetDef is JObject sheetObj)
                        {
                            sheetName = sheetObj["name"]?.ToString() ?? "Sheet";
                            headers = sheetObj["headers"] as JArray;
                        }
                        else continue;

                        var ws = wb.Worksheets.Add(sheetName);

                        if (headers != null)
                        {
                            for (int col = 0; col < headers.Count; col++)
                            {
                                ws.Cell(1, col + 1).Value = headers[col].ToString();
                                ws.Cell(1, col + 1).Style.Font.Bold = true;
                                ws.Cell(1, col + 1).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                            }
                        }
                    }
                }
                else
                {
                    // Default: create one sheet
                    var sheetName = parameters?["sheetName"]?.ToString() ?? "Sheet1";
                    var ws = wb.Worksheets.Add(sheetName);

                    // Optional headers as comma-separated or array
                    var headersParam = parameters?["headers"];
                    if (headersParam != null)
                    {
                        string[] headers;
                        if (headersParam is JArray hArr)
                            headers = hArr.Select(h => h.ToString()).ToArray();
                        else
                            headers = headersParam.ToString().Split(',').Select(h => h.Trim()).ToArray();

                        for (int col = 0; col < headers.Length; col++)
                        {
                            ws.Cell(1, col + 1).Value = headers[col];
                            ws.Cell(1, col + 1).Style.Font.Bold = true;
                            ws.Cell(1, col + 1).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                        }
                    }
                }

                wb.SaveAs(filePath);
            }

            return new JObject
            {
                ["message"] = $"✅ Created Excel workbook: {fileName}",
                ["filePath"] = filePath,
                ["fileName"] = fileName
            };
        }

        // ── Read Range ──

        /// <summary>Read cells from a specific range (e.g. "Sheet1!A1:D10").</summary>
        public static JToken ReadRange(string projectFilePath, JObject parameters)
        {
            var filePath = ResolveFilePath(projectFilePath, parameters);
            if (filePath == null)
                return new JObject { ["error"] = "File not found. Specify fileName." };

            var sheetName = parameters?["sheetName"]?.ToString();
            var range = parameters?["range"]?.ToString(); // e.g. "A1:D10"
            var sheetIndex = parameters?["sheetIndex"]?.Value<int>() ?? 0;

            using (var wb = new XLWorkbook(filePath))
            {
                var ws = !string.IsNullOrEmpty(sheetName)
                    ? wb.Worksheets.FirstOrDefault(w => w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                    : (sheetIndex > 0 && sheetIndex <= wb.Worksheets.Count ? wb.Worksheets.ElementAt(sheetIndex - 1) : wb.Worksheets.First());

                if (ws == null)
                    return new JObject { ["error"] = $"Sheet '{sheetName}' not found" };

                IXLRange xlRange;
                if (!string.IsNullOrEmpty(range))
                {
                    xlRange = ws.Range(range);
                }
                else
                {
                    // Read the used range
                    xlRange = ws.RangeUsed();
                    if (xlRange == null)
                        return new JObject
                        {
                            ["message"] = $"Sheet '{ws.Name}' is empty",
                            ["sheetName"] = ws.Name,
                            ["rows"] = new JArray()
                        };
                }

                var rows = new JArray();
                var headers = new JArray();
                var firstRow = xlRange.FirstRow();
                var lastRow = xlRange.LastRow();

                // First row as headers
                foreach (var cell in firstRow.Cells())
                    headers.Add(cell.GetString());

                // Data rows
                for (int r = firstRow.RowNumber(); r <= lastRow.RowNumber(); r++)
                {
                    var rowObj = new JObject();
                    foreach (var cell in xlRange.Row(r - firstRow.RowNumber() + 1).Cells())
                    {
                        var colIdx = cell.Address.ColumnNumber - xlRange.FirstColumn().ColumnNumber();
                        var header = colIdx < headers.Count ? headers[colIdx].ToString() : $"Col{colIdx + 1}";
                        rowObj[header] = GetCellDisplayValue(cell);
                    }
                    rows.Add(rowObj);
                }

                return new JObject
                {
                    ["message"] = $"Read {rows.Count} rows from {ws.Name}",
                    ["sheetName"] = ws.Name,
                    ["range"] = xlRange.RangeAddress.ToString(),
                    ["rowCount"] = rows.Count,
                    ["columns"] = headers,
                    ["rows"] = rows
                };
            }
        }

        // ── Write Cells ──

        /// <summary>Write data to specific cells or a range.</summary>
        public static JToken WriteCells(string projectFilePath, JObject parameters)
        {
            var filePath = ResolveFilePath(projectFilePath, parameters);
            if (filePath == null)
                return new JObject { ["error"] = "File not found. Specify fileName." };

            var sheetName = parameters?["sheetName"]?.ToString() ?? "Sheet1";
            int written = 0;

            using (var wb = new XLWorkbook(filePath))
            {
                var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                      ?? wb.Worksheets.First();

                // Mode 1: individual cells { "cells": { "A1": "hello", "B2": 42 } }
                var cellsParam = parameters?["cells"] as JObject;
                if (cellsParam != null)
                {
                    foreach (var kvp in cellsParam)
                    {
                        SetCellValue(ws.Cell(kvp.Key), kvp.Value);
                        written++;
                    }
                }

                // Mode 2: rows of data { "startCell": "A2", "data": [[...], [...]] }
                var dataParam = parameters?["data"] as JArray;
                var startCell = parameters?["startCell"]?.ToString() ?? "A1";
                if (dataParam != null)
                {
                    var startAddress = ws.Cell(startCell).Address;
                    int startRow = startAddress.RowNumber;
                    int startCol = startAddress.ColumnNumber;

                    for (int r = 0; r < dataParam.Count; r++)
                    {
                        if (dataParam[r] is JArray rowData)
                        {
                            for (int c = 0; c < rowData.Count; c++)
                            {
                                SetCellValue(ws.Cell(startRow + r, startCol + c), rowData[c]);
                                written++;
                            }
                        }
                    }
                }

                wb.Save();
            }

            return new JObject
            {
                ["message"] = $"✅ Wrote {written} cells to {Path.GetFileName(filePath)}",
                ["cellsWritten"] = written,
                ["fileName"] = Path.GetFileName(filePath)
            };
        }

        // ── Add/Rename/Delete Sheet ──

        /// <summary>Add, rename, or delete sheets in a workbook.</summary>
        public static JToken ManageSheet(string projectFilePath, JObject parameters)
        {
            var filePath = ResolveFilePath(projectFilePath, parameters);
            if (filePath == null)
                return new JObject { ["error"] = "File not found. Specify fileName." };

            var action = parameters?["action"]?.ToString()?.ToLower() ?? "add";
            var sheetName = parameters?["sheetName"]?.ToString();
            var newName = parameters?["newName"]?.ToString();

            using (var wb = new XLWorkbook(filePath))
            {
                switch (action)
                {
                    case "add":
                        if (string.IsNullOrEmpty(sheetName))
                            sheetName = $"Sheet{wb.Worksheets.Count + 1}";
                        wb.Worksheets.Add(sheetName);
                        wb.Save();
                        return new JObject { ["message"] = $"✅ Added sheet '{sheetName}'", ["sheetName"] = sheetName };

                    case "rename":
                        if (string.IsNullOrEmpty(sheetName) || string.IsNullOrEmpty(newName))
                            return new JObject { ["error"] = "Provide sheetName and newName." };
                        var wsRename = wb.Worksheets.FirstOrDefault(w => w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));
                        if (wsRename == null)
                            return new JObject { ["error"] = $"Sheet '{sheetName}' not found." };
                        wsRename.Name = newName;
                        wb.Save();
                        return new JObject { ["message"] = $"✅ Renamed '{sheetName}' → '{newName}'" };

                    case "delete":
                        if (string.IsNullOrEmpty(sheetName))
                            return new JObject { ["error"] = "Provide sheetName to delete." };
                        var wsDel = wb.Worksheets.FirstOrDefault(w => w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));
                        if (wsDel == null)
                            return new JObject { ["error"] = $"Sheet '{sheetName}' not found." };
                        if (wb.Worksheets.Count <= 1)
                            return new JObject { ["error"] = "Cannot delete the only sheet." };
                        wsDel.Delete();
                        wb.Save();
                        return new JObject { ["message"] = $"✅ Deleted sheet '{sheetName}'" };

                    default:
                        return new JObject { ["error"] = $"Unknown action: {action}. Use add/rename/delete." };
                }
            }
        }

        // ── Insert/Delete Rows/Columns ──

        /// <summary>Insert or delete rows/columns.</summary>
        public static JToken InsertRows(string projectFilePath, JObject parameters)
        {
            var filePath = ResolveFilePath(projectFilePath, parameters);
            if (filePath == null)
                return new JObject { ["error"] = "File not found. Specify fileName." };

            var sheetName = parameters?["sheetName"]?.ToString();
            var action = parameters?["action"]?.ToString()?.ToLower() ?? "insert_row";
            var position = parameters?["position"]?.Value<int>() ?? 1;
            var count = parameters?["count"]?.Value<int>() ?? 1;

            using (var wb = new XLWorkbook(filePath))
            {
                var ws = !string.IsNullOrEmpty(sheetName)
                    ? wb.Worksheets.FirstOrDefault(w => w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                    : wb.Worksheets.First();

                if (ws == null)
                    return new JObject { ["error"] = $"Sheet '{sheetName}' not found." };

                string msg;
                switch (action)
                {
                    case "insert_row":
                    case "insert_rows":
                        ws.Row(position).InsertRowsAbove(count);
                        msg = $"✅ Inserted {count} row(s) at row {position}";
                        break;
                    case "delete_row":
                    case "delete_rows":
                        for (int i = 0; i < count; i++)
                            ws.Row(position).Delete();
                        msg = $"✅ Deleted {count} row(s) starting at row {position}";
                        break;
                    case "insert_column":
                    case "insert_columns":
                        ws.Column(position).InsertColumnsBefore(count);
                        msg = $"✅ Inserted {count} column(s) at column {position}";
                        break;
                    case "delete_column":
                    case "delete_columns":
                        for (int i = 0; i < count; i++)
                            ws.Column(position).Delete();
                        msg = $"✅ Deleted {count} column(s) starting at column {position}";
                        break;
                    default:
                        return new JObject { ["error"] = $"Unknown action: {action}. Use insert_row/delete_row/insert_column/delete_column." };
                }

                wb.Save();
                return new JObject { ["message"] = msg, ["fileName"] = Path.GetFileName(filePath) };
            }
        }

        // ── Format Cells ──

        /// <summary>Format cells: bold, colors, borders, number format.</summary>
        public static JToken FormatCells(string projectFilePath, JObject parameters)
        {
            var filePath = ResolveFilePath(projectFilePath, parameters);
            if (filePath == null)
                return new JObject { ["error"] = "File not found. Specify fileName." };

            var sheetName = parameters?["sheetName"]?.ToString();
            var range = parameters?["range"]?.ToString() ?? "A1";
            var bold = parameters?["bold"]?.Value<bool>();
            var italic = parameters?["italic"]?.Value<bool>();
            var fontSize = parameters?["fontSize"]?.Value<double>();
            var fontColor = parameters?["fontColor"]?.ToString();
            var bgColor = parameters?["backgroundColor"]?.ToString();
            var numberFormat = parameters?["numberFormat"]?.ToString();
            var borderStyle = parameters?["border"]?.ToString();
            var alignment = parameters?["alignment"]?.ToString();
            var wrapText = parameters?["wrapText"]?.Value<bool>();
            var merge = parameters?["merge"]?.Value<bool>();
            var autoFit = parameters?["autoFit"]?.Value<bool>();

            using (var wb = new XLWorkbook(filePath))
            {
                var ws = !string.IsNullOrEmpty(sheetName)
                    ? wb.Worksheets.FirstOrDefault(w => w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                    : wb.Worksheets.First();

                if (ws == null)
                    return new JObject { ["error"] = $"Sheet '{sheetName}' not found." };

                var xlRange = ws.Range(range);
                var style = xlRange.Style;

                if (bold.HasValue) style.Font.Bold = bold.Value;
                if (italic.HasValue) style.Font.Italic = italic.Value;
                if (fontSize.HasValue) style.Font.FontSize = fontSize.Value;
                if (!string.IsNullOrEmpty(fontColor)) style.Font.FontColor = ParseColor(fontColor);
                if (!string.IsNullOrEmpty(bgColor)) style.Fill.BackgroundColor = ParseColor(bgColor);
                if (!string.IsNullOrEmpty(numberFormat)) style.NumberFormat.Format = numberFormat;
                if (wrapText.HasValue) style.Alignment.WrapText = wrapText.Value;

                if (!string.IsNullOrEmpty(alignment))
                {
                    switch (alignment.ToLower())
                    {
                        case "left": style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left; break;
                        case "center": style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; break;
                        case "right": style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; break;
                    }
                }

                if (!string.IsNullOrEmpty(borderStyle))
                {
                    var bStyle = borderStyle.ToLower() == "thick" ? XLBorderStyleValues.Thick
                              : borderStyle.ToLower() == "double" ? XLBorderStyleValues.Double
                              : XLBorderStyleValues.Thin;
                    style.Border.OutsideBorder = bStyle;
                    style.Border.InsideBorder = bStyle;
                }

                if (merge == true)
                    xlRange.Merge();

                if (autoFit == true)
                    ws.Columns().AdjustToContents();

                wb.Save();
            }

            return new JObject
            {
                ["message"] = $"✅ Formatted range {range} in {Path.GetFileName(filePath)}",
                ["range"] = range,
                ["fileName"] = Path.GetFileName(filePath)
            };
        }

        // ── Add Formula ──

        /// <summary>Set formulas on cells.</summary>
        public static JToken AddFormula(string projectFilePath, JObject parameters)
        {
            var filePath = ResolveFilePath(projectFilePath, parameters);
            if (filePath == null)
                return new JObject { ["error"] = "File not found. Specify fileName." };

            var sheetName = parameters?["sheetName"]?.ToString();
            int written = 0;

            using (var wb = new XLWorkbook(filePath))
            {
                var ws = !string.IsNullOrEmpty(sheetName)
                    ? wb.Worksheets.FirstOrDefault(w => w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                    : wb.Worksheets.First();

                if (ws == null)
                    return new JObject { ["error"] = $"Sheet '{sheetName}' not found." };

                // Mode 1: single formula { "cell": "A5", "formula": "=SUM(A1:A4)" }
                var cell = parameters?["cell"]?.ToString();
                var formula = parameters?["formula"]?.ToString();
                if (!string.IsNullOrEmpty(cell) && !string.IsNullOrEmpty(formula))
                {
                    if (formula.StartsWith("=")) formula = formula.Substring(1);
                    ws.Cell(cell).FormulaA1 = formula;
                    written++;
                }

                // Mode 2: multiple formulas { "formulas": { "A5": "=SUM(A1:A4)", "B5": "=AVERAGE(B1:B4)" } }
                var formulasParam = parameters?["formulas"] as JObject;
                if (formulasParam != null)
                {
                    foreach (var kvp in formulasParam)
                    {
                        var f = kvp.Value.ToString();
                        if (f.StartsWith("=")) f = f.Substring(1);
                        ws.Cell(kvp.Key).FormulaA1 = f;
                        written++;
                    }
                }

                wb.Save();
            }

            return new JObject
            {
                ["message"] = $"✅ Set {written} formula(s) in {Path.GetFileName(filePath)}",
                ["formulasSet"] = written,
                ["fileName"] = Path.GetFileName(filePath)
            };
        }

        // ── Get Info ──

        /// <summary>Get workbook info: sheet names, used ranges, row/col counts.</summary>
        public static JToken GetInfo(string projectFilePath, JObject parameters)
        {
            var filePath = ResolveFilePath(projectFilePath, parameters);
            if (filePath == null)
                return new JObject { ["error"] = "File not found. Specify fileName." };

            using (var wb = new XLWorkbook(filePath))
            {
                var sheets = new JArray();
                foreach (var ws in wb.Worksheets)
                {
                    var used = ws.RangeUsed();
                    var sheetInfo = new JObject
                    {
                        ["name"] = ws.Name,
                        ["rowCount"] = used?.RowCount() ?? 0,
                        ["columnCount"] = used?.ColumnCount() ?? 0,
                        ["usedRange"] = used?.RangeAddress?.ToString() ?? "(empty)",
                        ["firstCell"] = used?.FirstCell()?.Address?.ToString() ?? "",
                        ["lastCell"] = used?.LastCell()?.Address?.ToString() ?? ""
                    };

                    // Include headers (first row)
                    if (used != null && used.RowCount() > 0)
                    {
                        var headers = new JArray();
                        foreach (var cell in used.FirstRow().Cells())
                        {
                            var val = cell.GetString();
                            if (!string.IsNullOrWhiteSpace(val))
                                headers.Add(val);
                        }
                        sheetInfo["headers"] = headers;
                    }

                    sheets.Add(sheetInfo);
                }

                var fi = new FileInfo(filePath);
                return new JObject
                {
                    ["message"] = $"Workbook info: {Path.GetFileName(filePath)} ({wb.Worksheets.Count} sheets)",
                    ["fileName"] = Path.GetFileName(filePath),
                    ["filePath"] = filePath,
                    ["size"] = FormatSize(fi.Length),
                    ["sheetCount"] = wb.Worksheets.Count,
                    ["sheets"] = sheets
                };
            }
        }

        // ── Helpers ──

        private static string? ResolveFilePath(string projectFilePath, JObject parameters)
        {
            var folder = ProjectFilesService.GetProjectFilesFolder(projectFilePath);
            var fileName = parameters?["fileName"]?.ToString() ?? parameters?["name"]?.ToString();

            if (string.IsNullOrEmpty(fileName)) return null;

            var filePath = Path.Combine(folder, fileName);
            if (File.Exists(filePath)) return filePath;

            // Try partial match
            var match = Directory.GetFiles(folder)
                .FirstOrDefault(f => Path.GetFileName(f).IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0);

            return match;
        }

        private static void SetCellValue(IXLCell cell, JToken? value)
        {
            if (value == null) return;

            switch (value.Type)
            {
                case JTokenType.Integer:
                    cell.Value = value.Value<long>();
                    break;
                case JTokenType.Float:
                    cell.Value = value.Value<double>();
                    break;
                case JTokenType.Boolean:
                    cell.Value = value.Value<bool>();
                    break;
                default:
                    var str = value.ToString();
                    if (str.StartsWith("="))
                    {
                        cell.FormulaA1 = str.Substring(1);
                    }
                    else if (double.TryParse(str, out var num))
                    {
                        cell.Value = num;
                    }
                    else if (DateTime.TryParse(str, out var dt))
                    {
                        cell.Value = dt;
                    }
                    else
                    {
                        cell.Value = str;
                    }
                    break;
            }
        }

        private static string GetCellDisplayValue(IXLCell cell)
        {
            if (cell.HasFormula)
                return $"={cell.FormulaA1}";
            if (cell.Value.IsNumber)
                return cell.GetDouble().ToString("G");
            if (cell.Value.IsDateTime)
                return cell.GetDateTime().ToString("yyyy-MM-dd");
            if (cell.Value.IsBoolean)
                return cell.GetBoolean().ToString();
            return cell.GetString();
        }

        private static XLColor ParseColor(string color)
        {
            var c = color.Trim().ToLowerInvariant();
            switch (c)
            {
                case "red": return XLColor.Red;
                case "blue": return XLColor.Blue;
                case "green": return XLColor.Green;
                case "yellow": return XLColor.Yellow;
                case "orange": return XLColor.Orange;
                case "white": return XLColor.White;
                case "black": return XLColor.Black;
                case "gray": case "grey": return XLColor.Gray;
                case "lightblue": return XLColor.LightBlue;
                case "lightgreen": return XLColor.LightGreen;
                case "lightyellow": return XLColor.LightYellow;
                case "lightgray": case "lightgrey": return XLColor.LightGray;
                case "darkblue": return XLColor.DarkBlue;
                case "darkgreen": return XLColor.DarkGreen;
                case "darkred": return XLColor.DarkRed;
                case "steelblue": return XLColor.SteelBlue;
                case "lightsteelblue": return XLColor.LightSteelBlue;
                case "coral": return XLColor.Coral;
                case "gold": return XLColor.Gold;
                case "purple": return XLColor.Purple;
                case "pink": return XLColor.Pink;
                default:
                    // Try hex color like "#FF5733"
                    if (c.StartsWith("#") && c.Length == 7)
                    {
                        try
                        {
                            int r = Convert.ToInt32(c.Substring(1, 2), 16);
                            int g = Convert.ToInt32(c.Substring(3, 2), 16);
                            int b = Convert.ToInt32(c.Substring(5, 2), 16);
                            return XLColor.FromArgb(r, g, b);
                        }
                        catch { }
                    }
                    return XLColor.Black;
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}
