using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using RevitMCPPlugin.Core;
using RevitMCPPlugin.AI;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI.Tools
{
    /// <summary>
    /// Dark-themed WPF window for managing project files next to the .rvt file.
    /// Files can be added, browsed, and sent to the AI for analysis.
    /// </summary>
    public class ProjectFilesWindow : Window
    {
        private readonly string _projectFolder;
        private readonly StackPanel _fileListPanel;
        private readonly TextBlock _statusText;
        private readonly TextBlock _folderPathText;
        private readonly TextBlock _fileCountText;

        private static readonly string FolderName = "RevitMCP_Files";

        public ProjectFilesWindow()
        {
            Title = "Project Files — Revit MCP";
            Width = 620;
            Height = 680;
            MinWidth = 500;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            DarkTheme.Apply(this);

            // Determine project folder
            _projectFolder = GetProjectFilesFolder();

            // Ensure folder exists
            if (!Directory.Exists(_projectFolder))
                Directory.CreateDirectory(_projectFolder);

            // ===== Build UI =====
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });     // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });         // Toolbar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // File list
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });         // Status

            // --- Header ---
            var header = new Border
            {
                Background = DarkTheme.BgHeader,
                Padding = new Thickness(20, 14, 20, 14),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "📁 Project Files",
                FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White
            });
            _folderPathText = new TextBlock
            {
                Text = _projectFolder,
                FontSize = 11, Foreground = DarkTheme.FgDim,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 4, 0, 0),
                ToolTip = _projectFolder
            };
            headerStack.Children.Add(_folderPathText);
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // --- Toolbar ---
            var toolbar = new Border
            {
                Background = DarkTheme.BgCard,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var toolbarGrid = new Grid();
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var addBtn = MakeToolbarButton("➕ Add Files", "Add files to the project folder");
            addBtn.Click += AddFiles_Click;
            Grid.SetColumn(addBtn, 0);
            toolbarGrid.Children.Add(addBtn);

            var openFolderBtn = MakeToolbarButton("📂 Open Folder", "Open folder in Explorer");
            openFolderBtn.Margin = new Thickness(8, 0, 0, 0);
            openFolderBtn.Click += OpenFolder_Click;
            Grid.SetColumn(openFolderBtn, 1);
            toolbarGrid.Children.Add(openFolderBtn);

            var refreshBtn = MakeToolbarButton("🔄 Refresh", "Refresh file list");
            refreshBtn.Click += (s, e) => RefreshFileList();
            Grid.SetColumn(refreshBtn, 3);
            toolbarGrid.Children.Add(refreshBtn);

            toolbar.Child = toolbarGrid;
            Grid.SetRow(toolbar, 1);
            mainGrid.Children.Add(toolbar);

            // --- File List ---
            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(16, 8, 16, 8)
            };
            _fileListPanel = new StackPanel();
            scroller.Content = _fileListPanel;
            Grid.SetRow(scroller, 2);
            mainGrid.Children.Add(scroller);

            // --- Status bar ---
            var statusBar = new Border
            {
                Background = DarkTheme.BgHeader,
                Padding = new Thickness(16, 8, 16, 8),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var statusGrid = new Grid();
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _statusText = new TextBlock
            {
                Text = "Ready",
                FontSize = 11,
                Foreground = DarkTheme.FgGreen,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_statusText, 0);
            statusGrid.Children.Add(_statusText);

            _fileCountText = new TextBlock
            {
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_fileCountText, 1);
            statusGrid.Children.Add(_fileCountText);

            statusBar.Child = statusGrid;
            Grid.SetRow(statusBar, 3);
            mainGrid.Children.Add(statusBar);

            Content = mainGrid;

            Loaded += (s, e) => RefreshFileList();
        }

        /// <summary>
        /// Get the project files folder path (next to the .rvt file).
        /// Falls back to Desktop if the project is unsaved.
        /// </summary>
        private string GetProjectFilesFolder()
        {
            try
            {
                var app = Core.Application.ActiveUIApp;
                var doc = app?.ActiveUIDocument?.Document;
                if (doc != null && !string.IsNullOrEmpty(doc.PathName))
                {
                    var projectDir = Path.GetDirectoryName(doc.PathName);
                    return Path.Combine(projectDir!, FolderName);
                }
            }
            catch (Exception ex) { Logger.LogError("Error determining project files folder", ex); }

            // Fallback for unsaved projects
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                FolderName);
        }

        /// <summary>Refresh the file list from the folder.</summary>
        private void RefreshFileList()
        {
            _fileListPanel.Children.Clear();

            if (!Directory.Exists(_projectFolder))
            {
                Directory.CreateDirectory(_projectFolder);
                _statusText.Text = "Folder created";
            }

            var files = Directory.GetFiles(_projectFolder)
                .OrderByDescending(f => Path.GetExtension(f).Equals(".json", StringComparison.OrdinalIgnoreCase))
                .ThenBy(f => Path.GetFileName(f))
                .ToList();

            if (files.Count == 0)
            {
                _fileListPanel.Children.Add(new TextBlock
                {
                    Text = "No files yet.\nClick \"➕ Add Files\" to add documents for AI analysis.",
                    FontSize = 14,
                    Foreground = DarkTheme.FgDim,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 60, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
                _fileCountText.Text = "0 files";
                return;
            }

            foreach (var file in files)
            {
                _fileListPanel.Children.Add(BuildFileCard(file));
            }

            _fileCountText.Text = $"{files.Count} file(s)";
            _statusText.Text = "Ready";
        }

        /// <summary>Build a styled card for a single file.</summary>
        private Border BuildFileCard(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var ext = Path.GetExtension(filePath).ToLower();
            var fileInfo = new FileInfo(filePath);
            var isJson = ext == ".json";
            var isAnalysis = isJson && fileName.Contains("_analysis");

            // File icon
            string icon = isAnalysis ? "📊" : isJson ? "📋" : ext switch
            {
                ".pdf" => "📕",
                ".xlsx" or ".xls" or ".csv" => "📗",
                ".docx" or ".doc" => "📘",
                ".dwg" or ".dxf" => "📐",
                ".txt" => "📄",
                ".ifc" => "🏗️",
                _ => "📎"
            };

            var card = new Border
            {
                Background = isAnalysis ? DarkTheme.B(0x1A, 0x2A, 0x1A) : DarkTheme.BgCard,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 3, 0, 3),
                BorderBrush = isAnalysis ? DarkTheme.B(0x2D, 0x5A, 0x2D) : DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Effect = DarkTheme.MakeCardShadow()
            };

            var cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Icon
            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 22,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(iconText, 0);
            cardGrid.Children.Add(iconText);

            // Name + meta
            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = fileName,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = isAnalysis ? DarkTheme.FgGreen : Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var sizeStr = fileInfo.Length < 1024 ? $"{fileInfo.Length} B"
                : fileInfo.Length < 1048576 ? $"{fileInfo.Length / 1024.0:F1} KB"
                : $"{fileInfo.Length / 1048576.0:F1} MB";
            nameStack.Children.Add(new TextBlock
            {
                Text = $"{ext.TrimStart('.')} · {sizeStr} · {fileInfo.LastWriteTime:MMM dd HH:mm}",
                FontSize = 11,
                Foreground = DarkTheme.FgDim
            });
            Grid.SetColumn(nameStack, 1);
            cardGrid.Children.Add(nameStack);

            // Analyze button (only for non-JSON files)
            if (!isJson)
            {
                var analyzeBtn = MakeCardButton("🤖 Analyze", DarkTheme.BgAccent);
                analyzeBtn.Click += (s, e) => AnalyzeFile(filePath);
                analyzeBtn.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(analyzeBtn, 2);
                cardGrid.Children.Add(analyzeBtn);
            }
            else
            {
                // For JSON: show "💬 Chat" button to send to AI
                var chatBtn = MakeCardButton("💬 Chat", DarkTheme.B(0x4C, 0xAF, 0x50));
                chatBtn.Click += (s, e) => ChatWithFile(filePath);
                chatBtn.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(chatBtn, 2);
                cardGrid.Children.Add(chatBtn);
            }

            // Delete button
            var deleteBtn = MakeCardButton("🗑️", DarkTheme.B(0x55, 0x35, 0x35));
            deleteBtn.Width = 34;
            deleteBtn.Margin = new Thickness(4, 0, 0, 0);
            deleteBtn.ToolTip = "Delete file";
            deleteBtn.Click += (s, e) => DeleteFile(filePath);
            Grid.SetColumn(deleteBtn, 4);
            cardGrid.Children.Add(deleteBtn);

            // Hover effects
            card.MouseEnter += (s, e) =>
            {
                card.BorderBrush = DarkTheme.BgAccent;
                card.Effect = DarkTheme.MakeGlowShadow(Color.FromRgb(0x00, 0xBF, 0xFF));
            };
            card.MouseLeave += (s, e) =>
            {
                card.BorderBrush = isAnalysis ? DarkTheme.B(0x2D, 0x5A, 0x2D) : DarkTheme.BorderDim;
                card.Effect = DarkTheme.MakeCardShadow();
            };

            card.Child = cardGrid;
            return card;
        }

        // ===== Actions =====

        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Add files to project folder",
                Multiselect = true,
                Filter = "All Files|*.*|Documents|*.pdf;*.docx;*.doc;*.txt;*.xlsx;*.xls;*.csv|" +
                         "CAD Files|*.dwg;*.dxf;*.ifc|JSON|*.json"
            };

            if (ofd.ShowDialog() == true)
            {
                int copied = 0;
                foreach (var file in ofd.FileNames)
                {
                    var destPath = Path.Combine(_projectFolder, Path.GetFileName(file));
                    try
                    {
                        File.Copy(file, destPath, overwrite: true);
                        copied++;
                    }
                    catch (Exception ex)
                    {
                        _statusText.Text = $"Error: {ex.Message}";
                        _statusText.Foreground = DarkTheme.FgRequired;
                    }
                }

                if (copied > 0)
                {
                    _statusText.Text = $"Added {copied} file(s)";
                    _statusText.Foreground = DarkTheme.FgGreen;
                }
                RefreshFileList();
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(_projectFolder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Error: {ex.Message}";
            }
        }

        private async void AnalyzeFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var ext = Path.GetExtension(filePath).ToLower();

            // Read file content
            string content;
            try
            {
                content = ReadFileContent(filePath, ext, fileName);
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Error reading file: {ex.Message}";
                _statusText.Foreground = DarkTheme.FgRequired;
                return;
            }

            // Detect data type from headers/content for better prompt
            var dataHint = DetectDataType(content, fileName);

            // Build a concise, focused prompt
            var prompt = BuildAnalysisPrompt(fileName, content, dataHint, "");

            _statusText.Text = $"⚡ Analyzing '{fileName}' (direct mode)...";
            _statusText.Foreground = DarkTheme.FgGold;

            try
            {
                // Use direct analysis — NO tools, NO history, much faster
                var client = new GeminiClient();
                var result = await System.Threading.Tasks.Task.Run(() => client.AnalyzeDirectAsync(prompt));

                // Show result in chat window
                ChatWindow.OpenWithResult($"📊 Analysis of '{fileName}':\n\n{result}");

                _statusText.Text = $"✅ Analysis of '{fileName}' complete";
                _statusText.Foreground = DarkTheme.FgGreen;
            }
            catch (Exception ex)
            {
                _statusText.Text = $"❌ Analysis failed: {ex.Message}";
                _statusText.Foreground = DarkTheme.FgRequired;
            }
        }

        /// <summary>
        /// Detects the type of construction/BIM data from content and filename.
        /// </summary>
        private string DetectDataType(string content, string fileName)
        {
            var lower = (content + " " + fileName).ToLower();

            if (lower.Contains("level") && (lower.Contains("count") || lower.Contains("quantity")))
                return "element_quantity_by_level";
            if (lower.Contains("boq") || lower.Contains("bill of quant") || lower.Contains("quantity"))
                return "bill_of_quantities";
            if (lower.Contains("schedule") && (lower.Contains("area") || lower.Contains("room")))
                return "area_schedule";
            if (lower.Contains("spec") || lower.Contains("standard") || lower.Contains("requirement"))
                return "specifications";
            if (lower.Contains("wall") || lower.Contains("floor") || lower.Contains("door") || lower.Contains("window"))
                return "element_data";
            if (lower.Contains("cost") || lower.Contains("price") || lower.Contains("budget"))
                return "cost_data";
            if (lower.Contains("phase") || lower.Contains("milestone") || lower.Contains("timeline"))
                return "project_timeline";
            return "general";
        }

        /// <summary>
        /// Builds a detailed, construction-specific prompt based on the data type.
        /// </summary>
        private string BuildAnalysisPrompt(string fileName, string content, string dataType, string analysisName)
        {
            var typeHint = dataType switch
            {
                "element_quantity_by_level" => "Element quantities by level.",
                "bill_of_quantities" => "Bill of Quantities (BOQ).",
                "area_schedule" => "Area/room schedule.",
                "cost_data" => "Cost/budget data.",
                "element_data" => "Revit element data.",
                _ => "Construction/BIM data."
            };

            return $"Analyze this {typeHint}\n" +
                   $"File: {fileName}\n\n" +
                   $"```\n{content}\n```\n\n" +
                   "Provide:\n" +
                   "1. Summary table with key numbers\n" +
                   "2. Key findings (specific numbers, not vague)\n" +
                   "3. Anomalies or issues\n" +
                   "4. Recommended actions\n" +
                   "Be concise. Use markdown tables.";
        }

        /// <summary>
        /// Reads file content as text. Handles Excel (.xls/.xlsx), CSV, TXT natively.
        /// Binary files (PDF, DWG, etc.) get a description placeholder.
        /// </summary>
        private string ReadFileContent(string filePath, string ext, string fileName)
        {
            // Excel (.xlsx and .xls) — use ExcelDataReader
            if (ext == ".xlsx" || ext == ".xls")
            {
                return ReadExcelAsText(filePath);
            }

            // Pure binary files — PDF, CAD, Word
            if (ext == ".pdf" || ext == ".dwg" || ext == ".dxf" || ext == ".ifc" ||
                ext == ".docx" || ext == ".doc")
            {
                var fi = new FileInfo(filePath);
                return $"[Binary file: {fileName}, Size: {fi.Length} bytes, Type: {ext}]\n" +
                       "This is a binary file that cannot be read as text. " +
                       "For full content analysis, convert to .txt, .csv, or .xlsx format.";
            }

            // Text-based files (.txt, .csv, .json, etc.) — read directly
            var content = File.ReadAllText(filePath);
            // Limit to 8K chars for local models (was 30K — too much for 7B)
            var maxChars = 8000;
            if (content.Length > maxChars)
                content = content.Substring(0, maxChars) + "\n... (truncated)";
            return content;
        }

        /// <summary>
        /// Reads an Excel file (.xls or .xlsx) using ExcelDataReader.
        /// Optimized for local AI: limited rows with metadata summary.
        /// </summary>
        private string ReadExcelAsText(string filePath)
        {
            // Required for .NET 4.8 — register the code page provider
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var sb = new System.Text.StringBuilder();
            const int maxRowsPerSheet = 50; // Enough for pattern recognition, not too much for 7B

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
            {
                int sheetNum = 0;
                do
                {
                    sheetNum++;
                    if (sheetNum > 5) { sb.AppendLine("\n... (more sheets omitted)"); break; }

                    var sheetName = reader.Name ?? $"Sheet{sheetNum}";

                    // First pass: read header and count total rows
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

                        // Skip entirely empty rows
                        if (cells.All(c => string.IsNullOrWhiteSpace(c))) { totalRows--; continue; }

                        if (headerRow == null)
                            headerRow = cells; // First non-empty row = header
                        
                        allRows.Add(cells);
                    }

                    // Write sheet metadata
                    sb.AppendLine($"=== {sheetName} ({totalRows} rows, {reader.FieldCount} cols) ===");

                    // Write columns info
                    if (headerRow != null)
                        sb.AppendLine($"Columns: {string.Join(", ", headerRow.Where(h => !string.IsNullOrWhiteSpace(h)))}");

                    // Write limited rows
                    int rowsToWrite = Math.Min(allRows.Count, maxRowsPerSheet);
                    for (int i = 0; i < rowsToWrite; i++)
                    {
                        sb.AppendLine(string.Join(",", allRows[i]));
                    }

                    if (allRows.Count > maxRowsPerSheet)
                        sb.AppendLine($"... ({allRows.Count - maxRowsPerSheet} more rows, showing first {maxRowsPerSheet})");

                    sb.AppendLine();
                } while (reader.NextResult());
            }

            var result = sb.ToString();
            // Same 8K limit as text files
            if (result.Length > 8000)
                result = result.Substring(0, 8000) + "\n... (truncated)";
            return result;
        }

        private void ChatWithFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            string content;
            try
            {
                content = File.ReadAllText(filePath);
                if (content.Length > 15000)
                    content = content.Substring(0, 15000) + "\n... (truncated)";
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Error: {ex.Message}";
                return;
            }

            var prompt = $"Here is the project data file '{fileName}':\n\n```json\n{content}\n```\n\n" +
                         $"This is a previously saved analysis. What would you like me to do with it? " +
                         $"I can apply changes to the Revit model based on this data, update values, or explain the contents.";

            ChatWindow.OpenWithPrompt(prompt);
        }

        private void DeleteFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var result = MessageBox.Show(
                $"Delete '{fileName}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    File.Delete(filePath);
                    _statusText.Text = $"Deleted '{fileName}'";
                    _statusText.Foreground = DarkTheme.FgGreen;
                    RefreshFileList();
                }
                catch (Exception ex)
                {
                    _statusText.Text = $"Error: {ex.Message}";
                    _statusText.Foreground = DarkTheme.FgRequired;
                }
            }
        }

        // ===== UI Helpers =====

        private Button MakeToolbarButton(string text, string tooltip)
        {
            var btn = new Button
            {
                Content = text,
                ToolTip = tooltip,
                FontSize = 13,
                Foreground = Brushes.White,
                Background = DarkTheme.BgCard,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand
            };
            btn.MouseEnter += (s, e) => { btn.Background = DarkTheme.BgAccent; btn.BorderBrush = DarkTheme.BgAccent; };
            btn.MouseLeave += (s, e) => { btn.Background = DarkTheme.BgCard; btn.BorderBrush = DarkTheme.BorderDim; };
            return btn;
        }

        private Button MakeCardButton(string text, Brush bg)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 12,
                Foreground = Brushes.White,
                Background = bg,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5, 10, 5),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var origBg = bg;
            btn.MouseEnter += (s, e) => btn.Opacity = 0.85;
            btn.MouseLeave += (s, e) => btn.Opacity = 1.0;
            return btn;
        }
    }
}
