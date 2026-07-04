using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BIMBotPlugin.UI.Themes;

namespace BIMBotPlugin.UI.Tools
{
    /// <summary>
    /// Navisworks clash report viewer — parses an HTML clash report exported
    /// from Navisworks Clash Detective and surfaces each clash pair with
    /// Revit action buttons (Zoom, Select, Isolate).
    /// </summary>
    public class ClashDetectionWindow : Window
    {
        // ── Controls ──
        private readonly TextBox _filePathBox;
        private readonly StackPanel _resultsPanel;
        private readonly TextBlock _summaryText;
        private readonly TextBlock _statusText;
        private Button _selectAllBtn;
        private Button _exportBtn;

        // ── Parsed data ──
        private readonly List<NavisClash> _clashes = new List<NavisClash>();
        private string _reportDir = "";

        public ClashDetectionWindow()
        {
            Title = "⚡ Clash Report Viewer";
            Width = 960; Height = 680; MinWidth = 780; MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // Header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // File picker
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // Summary bar
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Results
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // Footer

            // ── Row 0: Header ──
            var header = DarkTheme.MakeGradientHeader("⚡ Clash Report Viewer",
                "Load a Navisworks HTML clash report to review and act on intersections");
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ── Row 1: File picker ──
            var pickerBorder = new Border
            {
                Background = DarkTheme.BgCard,
                Padding = new Thickness(20, 14, 20, 14),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var pickerRow = new DockPanel { LastChildFill = true };

            var browseBtn = new Button
            {
                Content = "📂 Browse",
                Background = DarkTheme.BgAccent,
                Foreground = DarkTheme.FgWhite,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 6, 14, 6),
                FontSize = 12,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            browseBtn.MouseEnter += (s, e) => browseBtn.Background = DarkTheme.BgAccentHover;
            browseBtn.MouseLeave += (s, e) => browseBtn.Background = DarkTheme.BgAccent;
            browseBtn.Click += OnBrowse;
            DockPanel.SetDock(browseBtn, Dock.Left);
            pickerRow.Children.Add(browseBtn);

            var loadBtn = new Button
            {
                Content = "⚡ Load Report",
                Background = DarkTheme.BgCard,
                Foreground = DarkTheme.FgLight,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 6, 14, 6),
                FontSize = 12,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            loadBtn.MouseEnter += (s, e) => loadBtn.Background = DarkTheme.BgCardHover;
            loadBtn.MouseLeave += (s, e) => loadBtn.Background = DarkTheme.BgCard;
            loadBtn.Click += OnLoadReport;
            DockPanel.SetDock(loadBtn, Dock.Left);
            pickerRow.Children.Add(loadBtn);

            _filePathBox = new TextBox
            {
                Background = DarkTheme.BgInput ?? DarkTheme.BgCard,
                Foreground = DarkTheme.FgLight,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsReadOnly = false
            };
            pickerRow.Children.Add(_filePathBox);

            pickerBorder.Child = pickerRow;
            Grid.SetRow(pickerBorder, 1);
            root.Children.Add(pickerBorder);

            // ── Row 2: Summary bar ──
            var summaryBorder = new Border
            {
                Background = DarkTheme.BgCard,
                Padding = new Thickness(20, 10, 20, 10),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            _summaryText = new TextBlock
            {
                Text = "No report loaded — browse to a Navisworks HTML clash report",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            summaryBorder.Child = _summaryText;
            Grid.SetRow(summaryBorder, 2);
            root.Children.Add(summaryBorder);

            // ── Row 3: Results scroll ──
            var resultsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(20, 14, 20, 14)
            };
            _resultsPanel = new StackPanel();
            _resultsPanel.Children.Add(new TextBlock
            {
                Text = "Load a report to see clash details here.",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 60, 0, 0)
            });
            resultsScroll.Content = _resultsPanel;
            Grid.SetRow(resultsScroll, 3);
            root.Children.Add(resultsScroll);

            // ── Row 4: Footer ──
            var footer = new Border
            {
                Background = DarkTheme.BgFooter,
                Padding = new Thickness(20, 12, 20, 12),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var footerDock = new DockPanel();

            _statusText = new TextBlock
            {
                Text = "Ready",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_statusText, Dock.Left);
            footerDock.Children.Add(_statusText);

            var footerBtns = new StackPanel { Orientation = Orientation.Horizontal };

            _selectAllBtn = MakeFooterButton("☑ Select All in Revit", enabled: false);
            _selectAllBtn.Click += (s, e) => SelectAllInRevit();
            _selectAllBtn.Margin = new Thickness(0, 0, 8, 0);

            _exportBtn = MakeFooterButton("📄 Export CSV", enabled: false);
            _exportBtn.Click += (s, e) => ExportCsv();

            var closeBtn = new Button
            {
                Content = "Close",
                Background = DarkTheme.BgCard,
                Foreground = DarkTheme.FgLight,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 6, 16, 6),
                FontSize = 12,
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0)
            };
            closeBtn.Click += (s, e) => Close();

            footerBtns.Children.Add(_selectAllBtn);
            footerBtns.Children.Add(_exportBtn);
            footerBtns.Children.Add(closeBtn);
            DockPanel.SetDock(footerBtns, Dock.Right);
            footerDock.Children.Add(footerBtns);

            footer.Child = footerDock;
            Grid.SetRow(footer, 4);
            root.Children.Add(footer);

            Content = root;
        }

        // ════════════════════════════════════════════════════════════════
        //  FILE BROWSE + LOAD
        // ════════════════════════════════════════════════════════════════

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Navisworks Clash Report",
                Filter = "HTML files (*.html;*.htm)|*.html;*.htm|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                _filePathBox.Text = dlg.FileName;
                OnLoadReport(sender, e);
            }
        }

        private void OnLoadReport(object sender, RoutedEventArgs e)
        {
            var path = _filePathBox.Text?.Trim();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _statusText.Text = "❌ File not found";
                _statusText.Foreground = DarkTheme.FgWarning;
                return;
            }

            try
            {
                _reportDir = Path.GetDirectoryName(path) ?? "";
                var html = File.ReadAllText(path);
                _clashes.Clear();
                _clashes.AddRange(ParseNavisHtml(html));
                RenderClashes();
            }
            catch (Exception ex)
            {
                _statusText.Text = $"❌ Parse error: {ex.Message}";
                _statusText.Foreground = DarkTheme.FgWarning;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  NAVISWORKS HTML PARSER
        //  Format: each clash is a <div class="viewpoint"> block containing
        //  <span class="namevaluepair"> with <span class="name"> / <span class="value">
        //  Element IDs appear after <h4 class="clashobject">Item 1</h4> etc.
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extracts all div blocks with a given class name, handling nested divs correctly.
        /// A simple regex with .*? stops at the first closing div — this walks depth instead.
        /// </summary>
        private static List<(string testSet, string block)> ExtractDivBlocks(string html, string className)
        {
            var results = new List<(string, string)>();
            var openTag = new Regex($@"<div[^>]*class=[""'][^""']*\b{Regex.Escape(className)}\b[^""']*[""'][^>]*>",
                RegexOptions.IgnoreCase);
            var anyDiv  = new Regex(@"<(/?)div\b", RegexOptions.IgnoreCase);

            int pos = 0;
            while (pos < html.Length)
            {
                var m = openTag.Match(html, pos);
                if (!m.Success) break;

                int start = m.Index + m.Length;
                int depth = 1;
                var scan = anyDiv.Match(html, start);
                int end = html.Length;
                while (scan.Success && depth > 0)
                {
                    if (scan.Groups[1].Value == "/") depth--;
                    else depth++;
                    if (depth > 0) scan = anyDiv.Match(html, scan.Index + scan.Length);
                    else end = scan.Index;
                }
                results.Add(("", html.Substring(start, end - start)));
                pos = end;
            }
            return results;
        }

        private static List<NavisClash> ParseNavisHtml(string html)
        {
            var results = new List<NavisClash>();

            string StripTags(string s) => Regex.Replace(s ?? "", "<[^>]+>", " ").Trim();

            string GetField(string block, string fieldName)
            {
                var m = Regex.Match(block,
                    $@"<span[^>]*class=[""']name[""'][^>]*>\s*{Regex.Escape(fieldName)}\*?\s*</span>\s*<span[^>]*class=[""']value[""'][^>]*>(.*?)</span>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);
                return m.Success ? StripTags(m.Groups[1].Value) : "";
            }

            // ── Strategy 1: animation > viewpoint hierarchy ──
            var animBlocks = ExtractDivBlocks(html, "animation");
            foreach (var (_, animBlock) in animBlocks)
            {
                var testSetName = StripTags(
                    Regex.Match(animBlock, @"<h3>(.*?)</h3>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                        .Groups[1].Value);

                var vpBlocks = ExtractDivBlocks(animBlock, "viewpoint");
                foreach (var (_, vpBlock) in vpBlocks)
                {
                    var clash = ParseClashBlock(vpBlock, GetField, testSetName);
                    if (clash != null) results.Add(clash);
                }

                // Some reports put clashgroups inside animation with no viewpoint wrapper
                if (vpBlocks.Count == 0)
                {
                    var cgBlocks = ExtractDivBlocks(animBlock, "clashgroup");
                    foreach (var (_, cgBlock) in cgBlocks)
                    {
                        var clash = ParseClashBlock(cgBlock, GetField, testSetName);
                        if (clash != null) results.Add(clash);
                    }
                }
            }

            // ── Strategy 2: bare viewpoints at top level ──
            if (results.Count == 0)
            {
                var vpBlocks = ExtractDivBlocks(html, "viewpoint");
                foreach (var (_, vpBlock) in vpBlocks)
                {
                    var clash = ParseClashBlock(vpBlock, GetField, "");
                    if (clash != null) results.Add(clash);
                }
            }

            return results;
        }

        private static NavisClash ParseClashBlock(
            string block,
            Func<string, string, string> getField,
            string testSet)
        {
            string StripTags(string s) => Regex.Replace(s ?? "", "<[^>]+>", " ").Trim();

            var name     = getField(block, "Name");
            var status   = getField(block, "Status");
            var distance = getField(block, "Distance");
            var grid     = getField(block, "Grid Location");

            if (string.IsNullOrEmpty(name)) return null;

            // Element IDs: first split the block into per-item sections, then find
            // "Element ID" anywhere within each section (Navisworks often puts other
            // fields like "Item" or "Layer" before "Element ID", so we cannot require
            // it to be the first namevaluepair after the <h4>).
            var itemSectionPattern = new Regex(
                @"<h4[^>]*class=[""']clashobject[""'][^>]*>Item\s*(\d+)\*?</h4>(.*?)(?=<h4[^>]*class=[""']clashobject|$)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var elementIdPattern = new Regex(
                @"<span[^>]*class=[""']name[""'][^>]*>\s*(?:Revit\s+)?Element\s+ID\*?\s*</span>\s*" +
                @"<span[^>]*class=[""']value[""'][^>]*>(\d+)</span>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            long id1 = 0, id2 = 0;
            foreach (Match m in itemSectionPattern.Matches(block))
            {
                var itemNum     = m.Groups[1].Value;
                var itemContent = m.Groups[2].Value;
                var idMatch     = elementIdPattern.Match(itemContent);
                if (!idMatch.Success) continue;
                var idVal = long.Parse(idMatch.Groups[1].Value);
                if (itemNum == "1") id1 = idVal;
                else if (itemNum == "2") id2 = idVal;
            }

            // Extract first image src from the viewpoint block
            var imgMatch = Regex.Match(block, @"<img[^>]+src=[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);
            var imgSrc = imgMatch.Success ? imgMatch.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar) : "";

            return new NavisClash
            {
                TestSet   = testSet,
                Name      = name,
                Status    = status,
                Distance  = distance,
                Grid      = grid,
                ImagePath = imgSrc,
                Id1       = id1,
                Id2       = id2
            };
        }

        // ════════════════════════════════════════════════════════════════
        //  RENDER RESULTS
        // ════════════════════════════════════════════════════════════════

        private void RenderClashes()
        {
            _resultsPanel.Children.Clear();

            if (_clashes.Count == 0)
            {
                _summaryText.Text = "⚠️ No clashes found in this report — check the file format";
                _summaryText.Foreground = DarkTheme.FgWarning;
                _statusText.Text = "0 clashes parsed";
                _statusText.Foreground = DarkTheme.FgDim;
                _selectAllBtn.IsEnabled = false;
                _exportBtn.IsEnabled = false;
                return;
            }

            var activeCount = _clashes.Count(c => !c.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase));
            _summaryText.Text = $"⚠️ {_clashes.Count} clash(es)  •  {activeCount} active  •  {_clashes.Count - activeCount} resolved";
            _summaryText.Foreground = activeCount > 0 ? DarkTheme.FgWarning : DarkTheme.FgGreen;
            _statusText.Text = $"✅ {_clashes.Count} clashes parsed";
            _statusText.Foreground = DarkTheme.FgGreen;
            _selectAllBtn.IsEnabled = true;
            _exportBtn.IsEnabled = true;

            // Group by test set
            var groups = _clashes.GroupBy(c => c.TestSet).ToList();
            int globalIdx = 0;
            foreach (var grp in groups)
            {
                if (!string.IsNullOrEmpty(grp.Key))
                {
                    _resultsPanel.Children.Add(new TextBlock
                    {
                        Text = $"📋 {grp.Key}  ({grp.Count()} clashes)",
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        Foreground = DarkTheme.FgLight,
                        Margin = new Thickness(0, 12, 0, 6)
                    });
                }

                foreach (var clash in grp)
                {
                    globalIdx++;
                    _resultsPanel.Children.Add(BuildClashCard(globalIdx, clash));
                }
            }
        }

        private Border BuildClashCard(int index, NavisClash clash)
        {
            var isResolved = clash.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase);
            var accentColor = isResolved ? DarkTheme.FgGreen : DarkTheme.FgGold;

            // Outer row: image on left, details on right
            var outerRow = new Grid();
            outerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            outerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ── Thumbnail ──
            if (!string.IsNullOrEmpty(clash.ImagePath))
            {
                var fullImg = Path.IsPathRooted(clash.ImagePath)
                    ? clash.ImagePath
                    : Path.Combine(_reportDir, clash.ImagePath);

                if (File.Exists(fullImg))
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(fullImg, UriKind.Absolute);
                        bmp.DecodePixelWidth = 130;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();

                        var img = new Image
                        {
                            Source = bmp,
                            Width = 130,
                            Height = 100,
                            Stretch = Stretch.UniformToFill,
                            Margin = new Thickness(0, 0, 12, 0),
                            VerticalAlignment = VerticalAlignment.Top
                        };
                        // Clip to rounded rect
                        img.Clip = new RectangleGeometry(new Rect(0, 0, 130, 100), 6, 6);
                        Grid.SetColumn(img, 0);
                        outerRow.Children.Add(img);
                    }
                    catch { /* skip broken images silently */ }
                }
            }

            var stack = new StackPanel();
            Grid.SetColumn(stack, 1);
            outerRow.Children.Add(stack);

            // ── Title row ──
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(new TextBlock
            {
                Text = $"#{index}  {clash.Name}",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = accentColor,
                VerticalAlignment = VerticalAlignment.Center
            });

            if (!string.IsNullOrEmpty(clash.Status))
            {
                var badge = new Border
                {
                    Background = isResolved ? DarkTheme.FgGreen : DarkTheme.BgAccent,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock { Text = clash.Status, FontSize = 10, Foreground = DarkTheme.FgWhite };
                titleRow.Children.Add(badge);
            }

            if (!string.IsNullOrEmpty(clash.Distance))
                titleRow.Children.Add(new TextBlock
                {
                    Text = $"   {clash.Distance}",
                    FontSize = 10,
                    Foreground = DarkTheme.FgDim,
                    VerticalAlignment = VerticalAlignment.Center
                });

            stack.Children.Add(titleRow);

            // ── Element ID rows ──
            var idsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

            idsRow.Children.Add(new TextBlock { Text = "① ID:", FontSize = 11, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center });
            idsRow.Children.Add(new TextBlock
            {
                Text = clash.Id1 != 0 ? clash.Id1.ToString() : "—",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = clash.Id1 != 0 ? DarkTheme.FgLight : DarkTheme.FgDim,
                Margin = new Thickness(4, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            idsRow.Children.Add(new TextBlock { Text = "② ID:", FontSize = 11, Foreground = DarkTheme.FgDim, VerticalAlignment = VerticalAlignment.Center });
            idsRow.Children.Add(new TextBlock
            {
                Text = clash.Id2 != 0 ? clash.Id2.ToString() : "—",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = clash.Id2 != 0 ? DarkTheme.FgLight : DarkTheme.FgDim,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            stack.Children.Add(idsRow);

            if (!string.IsNullOrEmpty(clash.Grid))
                stack.Children.Add(new TextBlock
                {
                    Text = $"📍 {clash.Grid}",
                    FontSize = 10,
                    Foreground = DarkTheme.FgDim,
                    Margin = new Thickness(0, 3, 0, 0)
                });

            // ── Action buttons ──
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };

            if (clash.Id1 != 0) btnRow.Children.Add(MakeActionBtn("🔍 Zoom ①", () =>
                DirectExecutor.RunAsync("zoom_to_element",
                    DirectExecutor.Params(("elementId", clash.Id1)), "Zoom to Element")));

            if (clash.Id2 != 0) btnRow.Children.Add(MakeActionBtn("🔍 Zoom ②", () =>
                DirectExecutor.RunAsync("zoom_to_element",
                    DirectExecutor.Params(("elementId", clash.Id2)), "Zoom to Element"), margin: 4));

            if (clash.Id1 != 0 && clash.Id2 != 0)
            {
                btnRow.Children.Add(MakeActionBtn("☑ Select Both", () =>
                {
                    var p = new Newtonsoft.Json.Linq.JObject();
                    p["elementIds"] = new Newtonsoft.Json.Linq.JArray(clash.Id1, clash.Id2);
                    DirectExecutor.RunAsync("select_elements", p, "Select Both Elements");
                }, margin: 4, accent: true));

                btnRow.Children.Add(MakeActionBtn("🔦 Isolate", () =>
                {
                    var p = new Newtonsoft.Json.Linq.JObject();
                    p["elementIds"] = new Newtonsoft.Json.Linq.JArray(clash.Id1, clash.Id2);
                    p["isolate"] = true;
                    DirectExecutor.RunAsync("select_elements", p, "Isolate Elements");
                }, margin: 4));
            }

            stack.Children.Add(btnRow);

            return new Border
            {
                Background = DarkTheme.BgCardHover,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 6),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Child = outerRow
            };
        }

        private static Button MakeActionBtn(string text, Action onClick, int margin = 0, bool accent = false)
        {
            var btn = new Button
            {
                Content = text,
                Background = accent ? DarkTheme.BgAccent : DarkTheme.BgCard,
                Foreground = accent ? DarkTheme.FgWhite : DarkTheme.FgLight,
                BorderBrush = accent ? null : DarkTheme.BorderDim,
                BorderThickness = accent ? new Thickness(0) : new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 10,
                Cursor = Cursors.Hand,
                Margin = new Thickness(margin, 0, 0, 0)
            };
            btn.MouseEnter += (s, e) => btn.Background = accent ? DarkTheme.BgAccentHover : DarkTheme.BgCardHover;
            btn.MouseLeave += (s, e) => btn.Background = accent ? DarkTheme.BgAccent : DarkTheme.BgCard;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private static Button MakeFooterButton(string text, bool enabled)
        {
            var btn = new Button
            {
                Content = text,
                Background = DarkTheme.BgCard,
                Foreground = DarkTheme.FgLight,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 6, 14, 6),
                FontSize = 12,
                Cursor = Cursors.Hand,
                IsEnabled = enabled
            };
            btn.MouseEnter += (s, e) => { if (btn.IsEnabled) btn.Background = DarkTheme.BgCardHover; };
            btn.MouseLeave += (s, e) => btn.Background = DarkTheme.BgCard;
            return btn;
        }

        // ════════════════════════════════════════════════════════════════
        //  FOOTER ACTIONS
        // ════════════════════════════════════════════════════════════════

        private void SelectAllInRevit()
        {
            var ids = _clashes
                .SelectMany(c => new[] { c.Id1, c.Id2 })
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                _statusText.Text = "⚠️ No Revit element IDs found in report";
                _statusText.Foreground = DarkTheme.FgWarning;
                return;
            }

            var p = new Newtonsoft.Json.Linq.JObject();
            p["elementIds"] = new Newtonsoft.Json.Linq.JArray(ids.Cast<object>().ToArray());
            DirectExecutor.RunAsync("select_elements", p, "Select All Clashing Elements");

            _statusText.Text = $"☑ {ids.Count} elements selected in Revit";
            _statusText.Foreground = DarkTheme.FgGold;
        }

        private void ExportCsv()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Clash Report as CSV",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"ClashReport_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Clash #,Test Set,Name,Status,Distance,Element 1 ID,Element 2 ID,Grid Location");
                int idx = 0;
                foreach (var c in _clashes)
                {
                    idx++;
                    sb.AppendLine($"{idx},\"{c.TestSet}\",\"{c.Name}\",\"{c.Status}\",\"{c.Distance}\",{c.Id1},{c.Id2},\"{c.Grid}\"");
                }
                File.WriteAllText(dlg.FileName, sb.ToString());
                _statusText.Text = $"📄 CSV saved — {_clashes.Count} clashes";
                _statusText.Foreground = DarkTheme.FgGreen;
            }
            catch (Exception ex)
            {
                _statusText.Text = $"❌ Export failed: {ex.Message}";
                _statusText.Foreground = DarkTheme.FgWarning;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  DATA MODEL
        // ════════════════════════════════════════════════════════════════

        private class NavisClash
        {
            public string TestSet   { get; set; } = "";
            public string Name      { get; set; } = "";
            public string Status    { get; set; } = "";
            public string Distance  { get; set; } = "";
            public string Grid      { get; set; } = "";
            public string ImagePath { get; set; } = "";
            public long   Id1       { get; set; }
            public long   Id2       { get; set; }
        }
    }
}
