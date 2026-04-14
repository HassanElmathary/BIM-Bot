using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Provides project-file operations (list, read, search, export) for the Antigravity chat.
    /// Files live next to the .rvt file in a "_ProjectFiles" folder.
    /// </summary>
    public static class ProjectFilesService
    {
        private const string FolderName = "_ProjectFiles";

        // ── Folder ──

        /// <summary>Get the project files folder (next to the .rvt file).</summary>
        public static string GetProjectFilesFolder(string projectFilePath)
        {
            if (!string.IsNullOrEmpty(projectFilePath))
            {
                var dir = Path.GetDirectoryName(projectFilePath);
                if (dir != null)
                {
                    var folder = Path.Combine(dir, FolderName);
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    return folder;
                }
            }
            // Fallback
            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), FolderName);
            if (!Directory.Exists(fallback))
                Directory.CreateDirectory(fallback);
            return fallback;
        }

        // ── List ──

        /// <summary>List all files in the project files folder.</summary>
        public static JToken ListFiles(string projectFilePath, JObject parameters)
        {
            var folder = GetProjectFilesFolder(projectFilePath);
            var filter = parameters?["filter"]?.ToString(); // e.g. "xlsx", "csv"

            var files = Directory.GetFiles(folder)
                .Select(f => new FileInfo(f))
                .Where(f => !f.Name.StartsWith("_") && !f.Name.StartsWith("."))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            if (!string.IsNullOrEmpty(filter))
            {
                var ext = filter.StartsWith(".") ? filter : "." + filter;
                files = files.Where(f => f.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var result = new JArray();
            foreach (var f in files)
            {
                result.Add(new JObject
                {
                    ["name"] = f.Name,
                    ["extension"] = f.Extension.ToLower(),
                    ["size"] = FormatSize(f.Length),
                    ["sizeBytes"] = f.Length,
                    ["modified"] = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                    ["fullPath"] = f.FullName
                });
            }

            return new JObject
            {
                ["message"] = $"Found {result.Count} file(s) in project folder",
                ["folder"] = folder,
                ["count"] = result.Count,
                ["files"] = result
            };
        }

        // ── Read ──

        /// <summary>Read a project file's contents as text.</summary>
        public static JToken ReadFile(string projectFilePath, JObject parameters)
        {
            var folder = GetProjectFilesFolder(projectFilePath);
            var fileName = parameters?["fileName"]?.ToString() ?? parameters?["name"]?.ToString();

            if (string.IsNullOrEmpty(fileName))
            {
                // Try partial match
                var search = parameters?["search"]?.ToString();
                if (!string.IsNullOrEmpty(search))
                {
                    var match = Directory.GetFiles(folder)
                        .FirstOrDefault(f => Path.GetFileName(f).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (match != null)
                        fileName = Path.GetFileName(match);
                }

                if (string.IsNullOrEmpty(fileName))
                    return new JObject { ["error"] = "Please specify a fileName or search term." };
            }

            // Find the file (exact or partial match)
            var filePath = Path.Combine(folder, fileName);
            if (!File.Exists(filePath))
            {
                // Try partial match
                var match = Directory.GetFiles(folder)
                    .FirstOrDefault(f => Path.GetFileName(f).IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null)
                    filePath = match;
                else
                    return new JObject { ["error"] = $"File not found: {fileName}" };
            }

            var ext = Path.GetExtension(filePath).ToLower();
            var content = ReadFileContent(filePath, ext);

            return new JObject
            {
                ["message"] = $"Read file: {Path.GetFileName(filePath)}",
                ["fileName"] = Path.GetFileName(filePath),
                ["extension"] = ext,
                ["size"] = FormatSize(new FileInfo(filePath).Length),
                ["content"] = content
            };
        }

        // ── Analyze ──

        /// <summary>Analyze a file: detect data type, summarize structure.</summary>
        public static JToken AnalyzeFile(string projectFilePath, JObject parameters)
        {
            var readResult = ReadFile(projectFilePath, parameters);
            if (readResult["error"] != null) return readResult;

            var content = readResult["content"]?.ToString() ?? "";
            var fileName = readResult["fileName"]?.ToString() ?? "";
            var ext = readResult["extension"]?.ToString() ?? "";

            var dataType = DetectDataType(content, fileName);
            var lines = content.Split('\n');
            var wordCount = content.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

            var analysis = new JObject
            {
                ["message"] = $"Analysis of {fileName}",
                ["fileName"] = fileName,
                ["extension"] = ext,
                ["dataType"] = dataType,
                ["lineCount"] = lines.Length,
                ["wordCount"] = wordCount,
                ["preview"] = content.Length > 2000 ? content.Substring(0, 2000) + "\n... (truncated)" : content
            };

            // For Excel/CSV, extract column info
            if (ext == ".xlsx" || ext == ".xls" || ext == ".csv")
            {
                var firstLine = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("==="));
                if (firstLine != null && firstLine.StartsWith("Columns:"))
                    analysis["columns"] = firstLine;
            }

            return analysis;
        }

        // ── Search ──

        /// <summary>Search across project files for a keyword.</summary>
        public static JToken SearchFiles(string projectFilePath, JObject parameters)
        {
            var folder = GetProjectFilesFolder(projectFilePath);
            var keyword = parameters?["keyword"]?.ToString() ?? parameters?["query"]?.ToString() ?? parameters?["search"]?.ToString();

            if (string.IsNullOrEmpty(keyword))
                return new JObject { ["error"] = "Please specify a keyword to search for." };

            var results = new JArray();
            var files = Directory.GetFiles(folder)
                .Where(f => !Path.GetFileName(f).StartsWith("_"))
                .ToList();

            foreach (var file in files)
            {
                try
                {
                    var ext = Path.GetExtension(file).ToLower();
                    var content = ReadFileContent(file, ext);

                    if (content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Find matching lines
                        var matchLines = content.Split('\n')
                            .Select((line, idx) => new { line, idx })
                            .Where(x => x.line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                            .Take(5)
                            .Select(x => $"Line {x.idx + 1}: {x.line.Trim()}")
                            .ToList();

                        results.Add(new JObject
                        {
                            ["fileName"] = Path.GetFileName(file),
                            ["matchCount"] = content.Split(new[] { keyword }, StringSplitOptions.None).Length - 1,
                            ["matches"] = JArray.FromObject(matchLines)
                        });
                    }
                }
                catch { /* skip unreadable files */ }
            }

            return new JObject
            {
                ["message"] = $"Found '{keyword}' in {results.Count} file(s)",
                ["keyword"] = keyword,
                ["count"] = results.Count,
                ["results"] = results
            };
        }

        // ── Export Elements to CSV ──

        /// <summary>Export Revit element data to a CSV file in the project folder.</summary>
        public static string ExportToCsv(string projectFilePath, string fileName, List<Dictionary<string, string>> rows)
        {
            var folder = GetProjectFilesFolder(projectFilePath);
            if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                fileName += ".csv";

            var filePath = Path.Combine(folder, fileName);

            if (rows.Count == 0)
            {
                File.WriteAllText(filePath, "No data");
                return filePath;
            }

            // Collect all unique columns
            var columns = rows.SelectMany(r => r.Keys).Distinct().ToList();

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", columns.Select(EscapeCsv)));

            foreach (var row in rows)
            {
                var values = columns.Select(c => row.ContainsKey(c) ? EscapeCsv(row[c]) : "");
                sb.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }

        // ── Import from File ──

        /// <summary>Read data from a project file as structured rows.</summary>
        public static List<Dictionary<string, string>> ImportFromFile(string projectFilePath, JObject parameters)
        {
            var readResult = ReadFile(projectFilePath, parameters);
            if (readResult["error"] != null)
                return new List<Dictionary<string, string>>();

            var content = readResult["content"]?.ToString() ?? "";
            var ext = readResult["extension"]?.ToString() ?? "";

            // Parse CSV-like content into rows
            var lines = content.Split('\n')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Where(l => !l.StartsWith("===") && !l.StartsWith("Columns:") && !l.StartsWith("..."))
                .ToList();

            if (lines.Count < 2)
                return new List<Dictionary<string, string>>();

            var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToList();
            var rows = new List<Dictionary<string, string>>();

            for (int i = 1; i < lines.Count; i++)
            {
                var values = lines[i].Split(',').Select(v => v.Trim().Trim('"')).ToList();
                var row = new Dictionary<string, string>();
                for (int j = 0; j < Math.Min(headers.Count, values.Count); j++)
                {
                    if (!string.IsNullOrWhiteSpace(headers[j]))
                        row[headers[j]] = values[j];
                }
                if (row.Count > 0)
                    rows.Add(row);
            }

            return rows;
        }

        // ── Helpers ──

        private static string ReadFileContent(string filePath, string ext)
        {
            if (ext == ".xlsx" || ext == ".xls")
                return ReadExcelAsText(filePath);

            if (ext == ".pdf" || ext == ".dwg" || ext == ".dxf" || ext == ".ifc" ||
                ext == ".docx" || ext == ".doc")
            {
                var fi = new FileInfo(filePath);
                return $"[Binary file: {fi.Name}, Size: {fi.Length} bytes, Type: {ext}]\n" +
                       "Binary files cannot be read as text. Convert to .csv or .xlsx.";
            }

            var content = File.ReadAllText(filePath);
            if (content.Length > 15000)
                content = content.Substring(0, 15000) + "\n... (truncated)";
            return content;
        }

        private static string ReadExcelAsText(string filePath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var sb = new StringBuilder();
            const int maxRowsPerSheet = 100;

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
            {
                int sheetNum = 0;
                do
                {
                    sheetNum++;
                    if (sheetNum > 5) { sb.AppendLine("\n... (more sheets omitted)"); break; }

                    var sheetName = reader.Name ?? $"Sheet{sheetNum}";
                    var allRows = new List<string[]>();
                    string[] headerRow = null;
                    int totalRows = 0;

                    while (reader.Read())
                    {
                        totalRows++;
                        var cells = new string[reader.FieldCount];
                        for (int col = 0; col < reader.FieldCount; col++)
                        {
                            var val = reader.GetValue(col);
                            if (val == null)
                                cells[col] = "";
                            else if (val is DateTime dt)
                                cells[col] = dt.ToString("yyyy-MM-dd");
                            else if (val is double d)
                                cells[col] = d.ToString("G");
                            else
                                cells[col] = val.ToString()!.Replace(",", ";");
                        }

                        if (cells.All(c => string.IsNullOrWhiteSpace(c))) { totalRows--; continue; }

                        if (headerRow == null)
                            headerRow = cells;

                        allRows.Add(cells);
                    }

                    sb.AppendLine($"=== {sheetName} ({totalRows} rows, {reader.FieldCount} cols) ===");

                    if (headerRow != null)
                        sb.AppendLine($"Columns: {string.Join(", ", headerRow.Where(h => !string.IsNullOrWhiteSpace(h)))}");

                    int rowsToWrite = Math.Min(allRows.Count, maxRowsPerSheet);
                    for (int i = 0; i < rowsToWrite; i++)
                        sb.AppendLine(string.Join(",", allRows[i]));

                    if (allRows.Count > maxRowsPerSheet)
                        sb.AppendLine($"... ({allRows.Count - maxRowsPerSheet} more rows)");

                    sb.AppendLine();
                } while (reader.NextResult());
            }

            var result = sb.ToString();
            if (result.Length > 15000)
                result = result.Substring(0, 15000) + "\n... (truncated)";
            return result;
        }

        private static string DetectDataType(string content, string fileName)
        {
            var lower = (content + " " + fileName).ToLowerInvariant();

            if (lower.Contains("boq") || lower.Contains("bill of quantities") || lower.Contains("quantity"))
                return "Bill of Quantities (BOQ)";
            if (lower.Contains("schedule") || lower.Contains("timeline") || lower.Contains("milestone"))
                return "Schedule / Timeline";
            if (lower.Contains("specification") || lower.Contains("spec"))
                return "Specifications";
            if (lower.Contains("cost") || lower.Contains("price") || lower.Contains("budget") || lower.Contains("estimate"))
                return "Cost Estimate";
            if (lower.Contains("material") || lower.Contains("procurement"))
                return "Material List";
            if (lower.Contains("rfi") || lower.Contains("request for information"))
                return "RFI";
            if (lower.Contains("submittal"))
                return "Submittal";
            if (lower.Contains("punch") || lower.Contains("snag"))
                return "Punch List / Snag List";
            if (lower.Contains("door") || lower.Contains("window") || lower.Contains("hardware"))
                return "Door/Window Schedule";
            if (lower.Contains("finish") || lower.Contains("paint") || lower.Contains("flooring"))
                return "Finish Schedule";
            if (lower.Contains("steel") || lower.Contains("rebar") || lower.Contains("reinforcement"))
                return "Structural Data";
            if (lower.Contains("mep") || lower.Contains("hvac") || lower.Contains("plumbing") || lower.Contains("electrical"))
                return "MEP Data";
            if (lower.Contains("area") || lower.Contains("sqm") || lower.Contains("sqft") || lower.Contains("square"))
                return "Area Schedule";
            if (lower.Contains("elevation") || lower.Contains("level") || lower.Contains("floor"))
                return "Level/Elevation Data";

            return "General Data";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
