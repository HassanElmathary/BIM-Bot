using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BIMBotPlugin.UI.Themes;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.UI.Tools
{
    /// <summary>
    /// Premium dark-themed WPF window for geometric clash detection between
    /// Revit element categories. Uses ElementIntersectsElementFilter via the
    /// backend clash_detection command and displays interactive results.
    /// </summary>
    public class ClashDetectionWindow : Window
    {
        // ── Controls ──
        private readonly ComboBox _cat1Combo;
        private readonly ComboBox _cat2Combo;
        private readonly Slider _toleranceSlider;
        private readonly TextBlock _toleranceLabel;
        private readonly TextBox _maxResultsBox;
        private readonly ComboBox _levelCombo;
        private readonly StackPanel _resultsPanel;
        private readonly TextBlock _summaryText;
        private readonly TextBlock _statusText;
        private readonly Button _runBtn;
        private Button _selectAllBtn;
        private Button _exportBtn;
        private readonly List<JToken> _lastClashes = new List<JToken>();

        // ── Category list ──
        private static readonly string[] Categories =
        {
            "Walls", "Structural Columns", "Structural Framing",
            "Pipes", "Ducts", "Cable Trays", "Conduits",
            "Floors", "Mechanical Equipment", "Plumbing Fixtures",
            "Electrical Equipment", "Ceilings", "Roofs"
        };

        public ClashDetectionWindow()
        {
            Title = "⚡ Clash Detection";
            Width = 920; Height = 640; MinWidth = 780; MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DarkTheme.Apply(this);

            // ── Root 3-row grid ──
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── Row 0: Header ──
            var header = DarkTheme.MakeGradientHeader("⚡ Clash Detection",
                "Detect geometric intersections between element categories");
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ── Row 1: Body — 2-column layout ──
            var body = new Grid { Margin = new Thickness(20, 12, 20, 12) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ──── Left Column: Settings ────
            var leftScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var leftStack = new StackPanel();

            // Category 1
            var cat1Panel = new StackPanel();
            cat1Panel.Children.Add(DarkTheme.MakeLabel("Category 1", required: true));
            _cat1Combo = DarkTheme.MakeComboBox(Categories, "Structural Columns");
            cat1Panel.Children.Add(_cat1Combo);
            leftStack.Children.Add(DarkTheme.MakeGroupBox("Source Category", cat1Panel));

            // Category 2
            var cat2Panel = new StackPanel();
            cat2Panel.Children.Add(DarkTheme.MakeLabel("Category 2", required: true));
            _cat2Combo = DarkTheme.MakeComboBox(Categories, "Pipes");
            cat2Panel.Children.Add(_cat2Combo);
            leftStack.Children.Add(DarkTheme.MakeGroupBox("Target Category", cat2Panel));

            // Tolerance slider
            var tolPanel = new StackPanel();
            _toleranceLabel = new TextBlock
            {
                Text = "0.0 ft",
                FontSize = 12, Foreground = DarkTheme.FgGold,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 4)
            };
            tolPanel.Children.Add(_toleranceLabel);
            _toleranceSlider = DarkTheme.MakeSlider(0, 1.0, 0, 0.1);
            _toleranceSlider.Width = double.NaN; // stretch
            _toleranceSlider.HorizontalAlignment = HorizontalAlignment.Stretch;
            _toleranceSlider.ValueChanged += (s, e) =>
                _toleranceLabel.Text = $"{_toleranceSlider.Value:F1} ft";
            tolPanel.Children.Add(_toleranceSlider);
            leftStack.Children.Add(DarkTheme.MakeGroupBox("Tolerance (Bounding Box Expansion)", tolPanel));

            // Max Results
            var maxPanel = new StackPanel();
            maxPanel.Children.Add(DarkTheme.MakeLabel("Maximum results to return"));
            _maxResultsBox = DarkTheme.MakeTextBox("100");
            maxPanel.Children.Add(_maxResultsBox);
            leftStack.Children.Add(DarkTheme.MakeGroupBox("Max Results", maxPanel));

            // Level filter
            var levelPanel = new StackPanel();
            levelPanel.Children.Add(DarkTheme.MakeLabel("Filter by level (optional)"));
            _levelCombo = DarkTheme.MakeComboBox(new[] { "(All Levels)" });
            levelPanel.Children.Add(_levelCombo);
            leftStack.Children.Add(DarkTheme.MakeGroupBox("Level Filter", levelPanel));

            // Quick Checks
            leftStack.Children.Add(DarkTheme.MakeSectionHeader("⚡ Quick Checks", DarkTheme.FgGold));
            var presetsStack = new StackPanel();
            AddPresetButton(presetsStack, "🔩 Structure vs MEP",
                "Structural Columns", "Pipes");
            AddPresetButton(presetsStack, "🧱 Walls vs Pipes",
                "Walls", "Pipes");
            AddPresetButton(presetsStack, "📐 Beams vs Ducts",
                "Structural Framing", "Ducts");
            AddPresetButton(presetsStack, "🔌 Cables vs Ducts",
                "Cable Trays", "Ducts");
            leftStack.Children.Add(presetsStack);

            leftScroll.Content = leftStack;
            Grid.SetColumn(leftScroll, 0);
            body.Children.Add(leftScroll);

            // ──── Right Column: Results ────
            var rightStack = new StackPanel();

            // Summary card
            var summaryBorder = new Border
            {
                Background = DarkTheme.BgCard,
                CornerRadius = DarkTheme.CardRadius,
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 12),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1)
            };
            var summaryStack = new StackPanel();
            summaryStack.Children.Add(new TextBlock
            {
                Text = "Results Summary",
                FontSize = 14, FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgWhite
            });
            _summaryText = new TextBlock
            {
                Text = "Run a clash check to see results",
                FontSize = 12, Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            summaryStack.Children.Add(_summaryText);
            summaryBorder.Child = summaryStack;
            rightStack.Children.Add(summaryBorder);

            // Results list
            rightStack.Children.Add(new TextBlock
            {
                Text = "Clash Details",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 0, 0, 6)
            });
            var resultsBorder = new Border
            {
                Background = DarkTheme.BgCard,
                CornerRadius = DarkTheme.CardRadius,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                MaxHeight = 340
            };
            var resultsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(8)
            };
            _resultsPanel = new StackPanel();

            // Placeholder message
            _resultsPanel.Children.Add(new TextBlock
            {
                Text = "No clashes detected yet.\nSelect categories and click \"Run Clash Check\" to begin.",
                FontSize = 12, Foreground = DarkTheme.FgDim,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(20, 40, 20, 40)
            });

            resultsScroll.Content = _resultsPanel;
            resultsBorder.Child = resultsScroll;
            rightStack.Children.Add(resultsBorder);

            Grid.SetColumn(rightStack, 2);
            body.Children.Add(rightStack);

            Grid.SetRow(body, 1);
            root.Children.Add(body);

            // ── Row 2: Footer ──
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
                FontSize = 11, Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_statusText, Dock.Left);
            footerDock.Children.Add(_statusText);

            // Extra action buttons (left of Run)
            var extraBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 0) };

            var selectAllBtn = new Button
            {
                Content = "☑ Select All",
                Background = DarkTheme.BgCard,
                Foreground = DarkTheme.FgLight,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0),
                IsEnabled = false
            };
            selectAllBtn.MouseEnter += (s, e) => { if (selectAllBtn.IsEnabled) selectAllBtn.Background = DarkTheme.BgCardHover; };
            selectAllBtn.MouseLeave += (s, e) => selectAllBtn.Background = DarkTheme.BgCard;
            selectAllBtn.Click += (s, e) => SelectAllClashing();
            _selectAllBtn = selectAllBtn;

            var exportBtn = new Button
            {
                Content = "📄 Export Report",
                Background = DarkTheme.BgCard,
                Foreground = DarkTheme.FgLight,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
                Cursor = Cursors.Hand,
                IsEnabled = false
            };
            exportBtn.MouseEnter += (s, e) => { if (exportBtn.IsEnabled) exportBtn.Background = DarkTheme.BgCardHover; };
            exportBtn.MouseLeave += (s, e) => exportBtn.Background = DarkTheme.BgCard;
            exportBtn.Click += (s, e) => ExportReport();
            _exportBtn = exportBtn;

            extraBtns.Children.Add(selectAllBtn);
            extraBtns.Children.Add(exportBtn);
            DockPanel.SetDock(extraBtns, Dock.Right);
            footerDock.Children.Add(extraBtns);

            Button cancelBtn;
            var btnPanel = DarkTheme.MakeButtonPanel("🔍 Run Clash Check", out cancelBtn, out _runBtn);
            cancelBtn.Click += (s, e) => Close();
            _runBtn.Click += OnRunClashCheck;
            DockPanel.SetDock(btnPanel, Dock.Right);
            footerDock.Children.Add(btnPanel);

            footer.Child = footerDock;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Content = root;
        }

        // ════════════════════════════════════════════════════════════════
        //  PRESET BUTTONS
        // ════════════════════════════════════════════════════════════════

        private void AddPresetButton(StackPanel container, string label, string cat1, string cat2)
        {
            var btn = new Button
            {
                Content = label,
                Background = DarkTheme.BgCard,
                Foreground = DarkTheme.FgLight,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                FontSize = 12,
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 4)
            };
            btn.MouseEnter += (s, e) => btn.Background = DarkTheme.BgCardHover;
            btn.MouseLeave += (s, e) => btn.Background = DarkTheme.BgCard;
            btn.Click += (s, e) =>
            {
                SelectComboItem(_cat1Combo, cat1);
                SelectComboItem(_cat2Combo, cat2);
            };
            container.Children.Add(btn);
        }

        private void SelectComboItem(ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var item = combo.Items[i] as ComboBoxItem;
                if (item != null && item.Content?.ToString() == value)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  RUN CLASH DETECTION
        // ════════════════════════════════════════════════════════════════

        private void OnRunClashCheck(object sender, RoutedEventArgs e)
        {
            var cat1 = (_cat1Combo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var cat2 = (_cat2Combo.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrEmpty(cat1) || string.IsNullOrEmpty(cat2))
            {
                MessageBox.Show("Please select both categories.", "Missing Input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cat1 == cat2)
            {
                MessageBox.Show("Please select two different categories.", "Same Category",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int maxResults = 100;
            int.TryParse(_maxResultsBox.Text, out maxResults);
            if (maxResults <= 0) maxResults = 100;

            var tolerance = _toleranceSlider.Value;

            var levelFilter = (_levelCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (levelFilter == "(All Levels)") levelFilter = null;

            _statusText.Text = "⏳ Running clash detection...";
            _statusText.Foreground = DarkTheme.FgGold;
            _runBtn.IsEnabled = false;

            var parameters = DirectExecutor.Params(
                ("category1", cat1),
                ("category2", cat2),
                ("tolerance", tolerance),
                ("maxResults", maxResults),
                ("levelName", levelFilter)
            );

            RunClashDetectionAsync(parameters, cat1, cat2);
        }

        private async void RunClashDetectionAsync(JObject parameters, string cat1, string cat2)
        {
            var eventMgr = Core.Application.EventManagerInstance;

            if (eventMgr == null)
            {
                MessageBox.Show("BIM-Bot service is not initialized.\nPlease start BIM-Bot first.",
                    "Service Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                _statusText.Text = "❌ Service not ready";
                _statusText.Foreground = DarkTheme.FgWarning;
                _runBtn.IsEnabled = true;
                return;
            }

            try
            {
                var result = await eventMgr.ExecuteCommandAsync("clash_detection", parameters);
                DisplayResults(result, cat1, cat2);
            }
            catch (TimeoutException)
            {
                _statusText.Text = "⏱️ Timed out — Revit may be busy";
                _statusText.Foreground = DarkTheme.FgWarning;
            }
            catch (Exception ex)
            {
                _statusText.Text = $"❌ {ex.Message}";
                _statusText.Foreground = DarkTheme.FgWarning;
            }
            finally
            {
                _runBtn.IsEnabled = true;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  DISPLAY RESULTS
        // ════════════════════════════════════════════════════════════════

        private void DisplayResults(JToken result, string cat1, string cat2)
        {
            if (result == null)
            {
                _statusText.Text = "⚠️ No response from backend";
                _statusText.Foreground = DarkTheme.FgWarning;
                return;
            }

            var totalClashes = result["totalClashes"]?.Value<int>() ?? 0;
            var clashes = result["clashes"] as JArray ?? new JArray();
            var elapsed = result["elapsed"]?.ToString() ?? "";

            _lastClashes.Clear();
            foreach (var c in clashes) _lastClashes.Add(c);
            var hasResults = _lastClashes.Count > 0;
            _selectAllBtn.IsEnabled = hasResults;
            _exportBtn.IsEnabled = hasResults;

            // Update summary
            if (totalClashes == 0)
            {
                _summaryText.Text = $"✅ No clashes found between {cat1} and {cat2}";
                _summaryText.Foreground = DarkTheme.FgGreen;
                _statusText.Text = $"✅ Clean — 0 clashes {elapsed}";
                _statusText.Foreground = DarkTheme.FgGreen;
            }
            else
            {
                _summaryText.Text = $"⚠️ {totalClashes} clash(es) found between {cat1} and {cat2}";
                _summaryText.Foreground = DarkTheme.FgWarning;
                _statusText.Text = $"⚠️ {totalClashes} clashes detected {elapsed}";
                _statusText.Foreground = DarkTheme.FgGold;
            }

            // Build result cards
            _resultsPanel.Children.Clear();

            if (clashes.Count == 0 && totalClashes == 0)
            {
                _resultsPanel.Children.Add(new TextBlock
                {
                    Text = "No geometric intersections found.\nThe selected categories are clear.",
                    FontSize = 12, Foreground = DarkTheme.FgGreen,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(20, 40, 20, 40)
                });
                return;
            }

            int idx = 0;
            foreach (var clash in clashes)
            {
                idx++;
                var card = MakeClashCard(idx, clash);
                _resultsPanel.Children.Add(card);
            }

            if (totalClashes > clashes.Count)
            {
                _resultsPanel.Children.Add(new TextBlock
                {
                    Text = $"... and {totalClashes - clashes.Count} more (increase Max Results to see all)",
                    FontSize = 11, Foreground = DarkTheme.FgDim,
                    FontStyle = FontStyles.Italic,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 4)
                });
            }
        }

        private Border MakeClashCard(int index, JToken clash)
        {
            var e1Name = clash["element1Name"]?.ToString() ?? "Unknown";
            var e2Name = clash["element2Name"]?.ToString() ?? "Unknown";
            var e1Id = clash["element1Id"]?.Value<long>() ?? 0;
            var e2Id = clash["element2Id"]?.Value<long>() ?? 0;
            var location = clash["location"]?.ToString() ?? "";

            var cardStack = new StackPanel();

            // Clash title
            cardStack.Children.Add(new TextBlock
            {
                Text = $"Clash #{index}",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgGold
            });

            // Element 1
            cardStack.Children.Add(new TextBlock
            {
                Text = $"① {e1Name}  (ID: {e1Id})",
                FontSize = 11, Foreground = DarkTheme.FgLight,
                Margin = new Thickness(0, 4, 0, 0)
            });

            // Element 2
            cardStack.Children.Add(new TextBlock
            {
                Text = $"② {e2Name}  (ID: {e2Id})",
                FontSize = 11, Foreground = DarkTheme.FgLight,
                Margin = new Thickness(0, 2, 0, 0)
            });

            // Location
            if (!string.IsNullOrEmpty(location))
            {
                cardStack.Children.Add(new TextBlock
                {
                    Text = $"📍 {location}",
                    FontSize = 10, Foreground = DarkTheme.FgDim,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            // Zoom buttons
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0)
            };

            var zoom1 = MakeZoomButton("🔍 ①", e1Id);
            var zoom2 = MakeZoomButton("🔍 ②", e2Id);
            zoom2.Margin = new Thickness(4, 0, 0, 0);

            var selectBoth = new Button
            {
                Content = "☑ Select Both",
                Background = DarkTheme.BgAccent,
                Foreground = DarkTheme.FgWhite,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 10,
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0)
            };
            selectBoth.MouseEnter += (s, e) => selectBoth.Background = DarkTheme.BgAccentHover;
            selectBoth.MouseLeave += (s, e) => selectBoth.Background = DarkTheme.BgAccent;
            selectBoth.Click += (s, e) =>
            {
                DirectExecutor.RunAsync("select_elements",
                    DirectExecutor.Params(("elementIds", $"{e1Id},{e2Id}")),
                    "Select Clash Elements");
            };

            var isolateBtn = new Button
            {
                Content = "🔦 Isolate",
                Background = DarkTheme.BgCard,
                Foreground = DarkTheme.FgLight,
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 10,
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0)
            };
            isolateBtn.MouseEnter += (s, e) => isolateBtn.Background = DarkTheme.BgCardHover;
            isolateBtn.MouseLeave += (s, e) => isolateBtn.Background = DarkTheme.BgCard;
            isolateBtn.Click += (s, e) =>
            {
                DirectExecutor.RunAsync("select_elements",
                    DirectExecutor.Params(("elementIds", $"{e1Id},{e2Id}"), ("isolate", true)),
                    "Isolate Clash Elements");
            };

            btnRow.Children.Add(zoom1);
            btnRow.Children.Add(zoom2);
            btnRow.Children.Add(selectBoth);
            btnRow.Children.Add(isolateBtn);
            cardStack.Children.Add(btnRow);

            return new Border
            {
                Background = DarkTheme.BgCardHover,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 6),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(1),
                Child = cardStack
            };
        }

        // ════════════════════════════════════════════════════════════════
        //  SELECT ALL / EXPORT
        // ════════════════════════════════════════════════════════════════

        private void SelectAllClashing()
        {
            if (_lastClashes.Count == 0) return;

            var ids = new HashSet<long>();
            foreach (var clash in _lastClashes)
            {
                var id1 = clash["element1Id"]?.Value<long>() ?? 0;
                var id2 = clash["element2Id"]?.Value<long>() ?? 0;
                if (id1 != 0) ids.Add(id1);
                if (id2 != 0) ids.Add(id2);
            }

            DirectExecutor.RunAsync("select_elements",
                DirectExecutor.Params(("elementIds", string.Join(",", ids))),
                "Select All Clashing Elements");

            _statusText.Text = $"☑ {ids.Count} clashing elements selected";
            _statusText.Foreground = DarkTheme.FgGold;
        }

        private void ExportReport()
        {
            if (_lastClashes.Count == 0) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Clash Report",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"ClashReport_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Clash #,Element 1 ID,Element 1 Name,Element 2 ID,Element 2 Name,Location");

                int idx = 0;
                foreach (var clash in _lastClashes)
                {
                    idx++;
                    var e1Id   = clash["element1Id"]?.Value<long>() ?? 0;
                    var e1Name = clash["element1Name"]?.ToString() ?? "";
                    var e2Id   = clash["element2Id"]?.Value<long>() ?? 0;
                    var e2Name = clash["element2Name"]?.ToString() ?? "";
                    var loc    = clash["location"]?.ToString() ?? "";
                    sb.AppendLine($"{idx},{e1Id},\"{e1Name}\",{e2Id},\"{e2Name}\",\"{loc}\"");
                }

                System.IO.File.WriteAllText(dlg.FileName, sb.ToString());
                _statusText.Text = $"📄 Report saved — {_lastClashes.Count} clashes";
                _statusText.Foreground = DarkTheme.FgGreen;
            }
            catch (Exception ex)
            {
                _statusText.Text = $"❌ Export failed: {ex.Message}";
                _statusText.Foreground = DarkTheme.FgWarning;
            }
        }

        private Button MakeZoomButton(string text, long elementId)
        {
            var btn = new Button
            {
                Content = text,
                Background = DarkTheme.BgAccent,
                Foreground = DarkTheme.FgWhite,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 10,
                Cursor = Cursors.Hand
            };
            btn.MouseEnter += (s, e) => btn.Background = DarkTheme.BgAccentHover;
            btn.MouseLeave += (s, e) => btn.Background = DarkTheme.BgAccent;
            btn.Click += (s, e) =>
            {
                DirectExecutor.RunAsync("zoom_to_element",
                    DirectExecutor.Params(("elementId", (int)elementId)),
                    "Zoom to Element");
            };
            return btn;
        }
    }
}
