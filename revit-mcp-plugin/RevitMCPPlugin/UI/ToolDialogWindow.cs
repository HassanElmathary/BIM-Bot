using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitMCPPlugin.UI.Themes;

namespace RevitMCPPlugin.UI
{
    /// <summary>
    /// Dynamic per-tool parameter dialog. Generates form fields from ToolInfo metadata.
    /// Dark-themed to match the rest of the plugin UI.
    /// </summary>
    public class ToolDialogWindow : Window
    {
        private readonly ToolInfo _tool;
        private readonly Dictionary<string, FrameworkElement> _fields = new Dictionary<string, FrameworkElement>();



        public ToolDialogWindow(ToolInfo tool)
        {
            _tool = tool;
            Title = $"{tool.Icon} {tool.DisplayName}";
            Width = 480;
            MinWidth = 400;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            DarkTheme.Apply(this);

            BuildUI();
        }

        private void BuildUI()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var stack = new StackPanel { Margin = new Thickness(24) };

            // --- Header ---
            var headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            headerStack.Children.Add(new TextBlock
            {
                Text = $"{_tool.Icon} {_tool.DisplayName}",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = _tool.Description,
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(headerStack);

            // Separator
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = DarkTheme.BorderDim,
                Margin = new Thickness(0, 8, 0, 16)
            });

            // --- Parameter fields ---
            if (_tool.Parameters == null || _tool.Parameters.Count == 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "This tool has no configurable parameters.\nClick Run to execute with defaults.",
                    Foreground = DarkTheme.FgDim,
                    FontSize = 13,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 8, 0, 16)
                });
            }
            else
            {
                foreach (var param in _tool.Parameters)
                {
                    // Label
                    var label = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                    label.Children.Add(new TextBlock
                    {
                        Text = param.Label,
                        FontSize = 12,
                        Foreground = DarkTheme.FgLight
                    });
                    if (param.Required)
                    {
                        label.Children.Add(new TextBlock
                        {
                            Text = " *",
                            FontSize = 12,
                            Foreground = DarkTheme.FgRequired
                        });
                    }
                    stack.Children.Add(label);

                    // Field
                    FrameworkElement field;
                    switch (param.Type)
                    {
                        case "bool":
                            var cb = new CheckBox
                            {
                                IsChecked = param.Default == "true",
                            Content = param.Label,
                                Foreground = DarkTheme.FgLight,
                                FontSize = 13,
                                Margin = new Thickness(0, 0, 0, 12)
                            };
                            field = cb;
                            break;

                        case "dropdown":
                            var combo = DarkTheme.MakeComboBox(param.Options?.ToArray(), param.Default);
                            combo.Margin = new Thickness(0, 0, 0, 12);
                            field = combo;
                            break;

                        default: // "text" and "number"
                            var tb = DarkTheme.MakeTextBox(param.Default ?? "", param.Hint);
                            tb.Margin = new Thickness(0, 0, 0, 12);
                            field = tb;
                            break;
                    }

                    _fields[param.Name] = field;
                    if (param.Type != "bool") // bool checkbox already includes label
                        stack.Children.Add(field);
                    else
                        stack.Children.Add(field);
                }
            }

            // --- Buttons ---
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var cancelBtn = DarkTheme.MakeCancelButton();
            cancelBtn.Click += (s, e) => Close();
            btnPanel.Children.Add(cancelBtn);

            var runBtn = DarkTheme.MakePrimaryButton("▶  Run");
            runBtn.Margin = new Thickness(10, 0, 0, 0);
            runBtn.Click += RunBtn_Click;
            btnPanel.Children.Add(runBtn);

            stack.Children.Add(btnPanel);

            scroll.Content = stack;
            Content = scroll;
        }

        private void RunBtn_Click(object sender, RoutedEventArgs e)
        {
            // Validate required fields
            if (_tool.Parameters != null)
            {
                foreach (var param in _tool.Parameters)
                {
                    if (!param.Required) continue;
                    if (!_fields.ContainsKey(param.Name)) continue;
                    var val = GetFieldValue(param.Name);
                    if (string.IsNullOrWhiteSpace(val))
                    {
                        MessageBox.Show(
                            $"\"{param.Label}\" is required.",
                            "Missing field",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            // Build JObject parameters for direct execution
            var jsonParams = new Newtonsoft.Json.Linq.JObject();
            if (_tool.Parameters != null)
            {
                foreach (var param in _tool.Parameters)
                {
                    var val = GetFieldValue(param.Name);
                    if (!string.IsNullOrWhiteSpace(val))
                        jsonParams[param.Name] = val;
                }
            }

            Close();
            Tools.DirectExecutor.RunAsync(_tool.Name, jsonParams, _tool.DisplayName);
        }

        private string GetFieldValue(string paramName)
        {
            if (!_fields.ContainsKey(paramName)) return null;
            var field = _fields[paramName];

            if (field is TextBox tb)
            {
                // Skip placeholder text
                if (tb.Foreground == DarkTheme.FgDim) return null;
                return tb.Text?.Trim();
            }
            if (field is ComboBox cb)
            {
                return (cb.SelectedItem as ComboBoxItem)?.Content?.ToString();
            }
            if (field is CheckBox chk)
            {
                return chk.IsChecked == true ? "true" : "false";
            }
            return null;
        }

        private static SolidColorBrush B(byte r, byte g, byte b)
            => DarkTheme.B(r, g, b);
    }
}
