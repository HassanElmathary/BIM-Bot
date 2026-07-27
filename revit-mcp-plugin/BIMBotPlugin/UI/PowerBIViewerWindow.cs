using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BIMBotPlugin.AI;
using BIMBotPlugin.UI.Themes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.UI
{
    public class PowerBIViewerWindow : Window
    {
        private WebView2 _webView;
        private TextBox _urlBox;
        private ComboBox _workspaceCombo;
        private ComboBox _reportCombo;
        private Button _loadBtn;
        private TextBlock _statusText;
        private Border _toolbarBorder;
        private StackPanel _authPanel;
        private StackPanel _publicPanel;
        private bool _isAuthenticated;
        private JArray _workspaces;
        private JArray _reports;

        private readonly string _initialWorkspaceId;
        private readonly string _initialReportId;
        private readonly string _initialPublicUrl;

        public PowerBIViewerWindow(string workspaceId = null, string reportId = null, string publicUrl = null)
        {
            _initialWorkspaceId = workspaceId;
            _initialReportId = reportId;
            _initialPublicUrl = publicUrl;

            Title = "Power BI Report — BIM-Bot";
            Width = 1280;
            Height = 820;
            MinWidth = 800;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            DarkTheme.Apply(this);

            BuildUI();
            Loaded += async (s, e) => await InitializeAsync();
        }

        private void BuildUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 0: Header
            var header = DarkTheme.MakeGradientHeader("📊 Power BI Report",
                "Interactive dashboard embedded from Power BI Service", titleSize: 18);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // Row 1: Toolbar
            _toolbarBorder = BuildToolbar();
            Grid.SetRow(_toolbarBorder, 1);
            root.Children.Add(_toolbarBorder);

            // Row 2: WebView2
            _webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 0x1A, 0x1B, 0x1E)
            };
            Grid.SetRow(_webView, 2);
            root.Children.Add(_webView);

            // Row 3: Status footer
            var footer = BuildFooter();
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            Content = root;
        }

        private Border BuildToolbar()
        {
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(12, 0, 12, 0)
            };

            // --- Authenticated panel (workspace/report combos) ---
            _authPanel = new StackPanel { Orientation = Orientation.Horizontal };

            _authPanel.Children.Add(new TextBlock
            {
                Text = "Workspace:",
                Foreground = DarkTheme.FgLight,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 12
            });

            _workspaceCombo = DarkTheme.MakeComboBox(new string[0]);
            _workspaceCombo.Width = 220;
            _workspaceCombo.Margin = new Thickness(0, 0, 12, 0);
            _workspaceCombo.SelectionChanged += async (s, e) => await OnWorkspaceChanged();
            _authPanel.Children.Add(_workspaceCombo);

            _authPanel.Children.Add(new TextBlock
            {
                Text = "Report:",
                Foreground = DarkTheme.FgLight,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 12
            });

            _reportCombo = DarkTheme.MakeComboBox(new string[0]);
            _reportCombo.Width = 260;
            _reportCombo.Margin = new Thickness(0, 0, 12, 0);
            _authPanel.Children.Add(_reportCombo);

            toolbar.Children.Add(_authPanel);

            // --- Public URL panel ---
            _publicPanel = new StackPanel { Orientation = Orientation.Horizontal };

            _publicPanel.Children.Add(new TextBlock
            {
                Text = "URL:",
                Foreground = DarkTheme.FgLight,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 12
            });

            _urlBox = DarkTheme.MakeTextBox("", "Paste Power BI embed URL...");
            _urlBox.Width = 520;
            _urlBox.Margin = new Thickness(0, 0, 12, 0);
            _urlBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) LoadReport(); };
            _publicPanel.Children.Add(_urlBox);

            toolbar.Children.Add(_publicPanel);

            // --- Shared buttons ---
            _loadBtn = DarkTheme.MakePrimaryButton("Load");
            _loadBtn.Padding = new Thickness(16, 6, 16, 6);
            _loadBtn.FontSize = 12;
            _loadBtn.Click += (s, e) => LoadReport();
            toolbar.Children.Add(_loadBtn);

            var refreshBtn = DarkTheme.MakeCancelButton("Refresh");
            refreshBtn.Padding = new Thickness(12, 6, 12, 6);
            refreshBtn.FontSize = 12;
            refreshBtn.Margin = new Thickness(6, 0, 0, 0);
            refreshBtn.Click += (s, e) => _webView?.Reload();
            toolbar.Children.Add(refreshBtn);

            var fullscreenBtn = DarkTheme.MakeCancelButton("Fullscreen");
            fullscreenBtn.Padding = new Thickness(12, 6, 12, 6);
            fullscreenBtn.FontSize = 12;
            fullscreenBtn.Margin = new Thickness(6, 0, 0, 0);
            fullscreenBtn.Click += (s, e) => ToggleFullscreen();
            toolbar.Children.Add(fullscreenBtn);

            return new Border
            {
                Background = DarkTheme.BgCard,
                Padding = new Thickness(0, 8, 0, 8),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = toolbar
            };
        }

        private Border BuildFooter()
        {
            _statusText = new TextBlock
            {
                Text = "Initializing...",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            };

            return new Border
            {
                Background = DarkTheme.BgFooter,
                Padding = new Thickness(16, 6, 16, 6),
                BorderBrush = DarkTheme.BorderDim,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = _statusText
            };
        }

        private async Task InitializeAsync()
        {
            try
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Autodesk", "Revit", "Addins", "BIMBot", "WebView2Data");

                if (!Directory.Exists(userDataFolder))
                    Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                _isAuthenticated = PowerBIOAuthHelper.IsSignedIn();
                UpdateMode();

                if (!string.IsNullOrWhiteSpace(_initialPublicUrl))
                {
                    SetUrlBoxText(_initialPublicUrl);
                    LoadReport();
                }
                else if (_isAuthenticated &&
                         !string.IsNullOrWhiteSpace(_initialWorkspaceId) &&
                         !string.IsNullOrWhiteSpace(_initialReportId))
                {
                    await LoadWorkspacesAsync();
                    await LoadAuthenticatedReport(_initialWorkspaceId, _initialReportId);
                }
                else if (_isAuthenticated)
                {
                    await LoadWorkspacesAsync();
                    SetStatus("Select a workspace and report, or paste a public URL.");
                }
                else
                {
                    var settings = IntegrationSettings.Load();
                    if (!string.IsNullOrWhiteSpace(settings.PowerBIPublicUrl))
                    {
                        SetUrlBoxText(settings.PowerBIPublicUrl);
                        LoadReport();
                    }
                    else
                    {
                        SetStatus("Paste a Power BI 'Publish to Web' URL to get started. Sign in via Integrations Settings for full access.");
                    }
                }
            }
            catch (WebView2RuntimeNotFoundException)
            {
                ShowWebView2InstallPrompt();
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
            }
        }

        private void UpdateMode()
        {
            _authPanel.Visibility = _isAuthenticated ? Visibility.Visible : Visibility.Collapsed;
            _publicPanel.Visibility = Visibility.Visible;

            if (_isAuthenticated)
            {
                var settings = IntegrationSettings.Load();
                SetStatus($"Signed in as: {settings.PowerBIEmail}");
            }
            else
            {
                SetStatus("Public URL mode — sign in via Integrations Settings for workspace access.");
            }
        }

        private async Task LoadWorkspacesAsync()
        {
            try
            {
                SetStatus("Loading workspaces...");
                var token = await PowerBIOAuthHelper.GetAccessTokenAsync();
                _workspaces = await PowerBIApiClient.GetWorkspacesAsync(token);

                _workspaceCombo.Items.Clear();
                foreach (var ws in _workspaces)
                {
                    var item = new ComboBoxItem
                    {
                        Content = ws["name"]?.ToString() ?? "Unnamed",
                        Tag = ws["id"]?.ToString(),
                        Background = DarkTheme.BgCard,
                        Foreground = DarkTheme.FgWhite,
                        Padding = new Thickness(8, 6, 8, 6)
                    };

                    if (ws["id"]?.ToString() == _initialWorkspaceId)
                        item.IsSelected = true;

                    _workspaceCombo.Items.Add(item);
                }

                if (_workspaceCombo.SelectedIndex < 0 && _workspaceCombo.Items.Count > 0)
                    _workspaceCombo.SelectedIndex = 0;

                SetStatus($"Loaded {_workspaces.Count} workspace(s).");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load workspaces: {ex.Message}");
            }
        }

        private async Task OnWorkspaceChanged()
        {
            if (_workspaceCombo.SelectedItem is ComboBoxItem selected && selected.Tag is string wsId)
            {
                try
                {
                    SetStatus("Loading reports...");
                    var token = await PowerBIOAuthHelper.GetAccessTokenAsync();
                    _reports = await PowerBIApiClient.GetReportsAsync(token, wsId);

                    _reportCombo.Items.Clear();
                    foreach (var rpt in _reports)
                    {
                        var item = new ComboBoxItem
                        {
                            Content = rpt["name"]?.ToString() ?? "Unnamed",
                            Tag = rpt["id"]?.ToString(),
                            Background = DarkTheme.BgCard,
                            Foreground = DarkTheme.FgWhite,
                            Padding = new Thickness(8, 6, 8, 6)
                        };

                        if (rpt["id"]?.ToString() == _initialReportId)
                            item.IsSelected = true;

                        _reportCombo.Items.Add(item);
                    }

                    if (_reportCombo.SelectedIndex < 0 && _reportCombo.Items.Count > 0)
                        _reportCombo.SelectedIndex = 0;

                    SetStatus($"Loaded {_reports.Count} report(s).");
                }
                catch (Exception ex)
                {
                    SetStatus($"Failed to load reports: {ex.Message}");
                }
            }
        }

        private void LoadReport()
        {
            var urlText = GetUrlBoxText();

            if (!string.IsNullOrWhiteSpace(urlText) && Uri.TryCreate(urlText, UriKind.Absolute, out var uri))
            {
                SetStatus($"Loading: {uri.Host}...");
                _webView.Source = uri;
                return;
            }

            if (_isAuthenticated &&
                _workspaceCombo.SelectedItem is ComboBoxItem wsItem && wsItem.Tag is string wsId &&
                _reportCombo.SelectedItem is ComboBoxItem rptItem && rptItem.Tag is string rptId)
            {
                _ = LoadAuthenticatedReport(wsId, rptId);
                return;
            }

            SetStatus("Enter a URL or select a workspace/report.");
        }

        private async Task LoadAuthenticatedReport(string workspaceId, string reportId)
        {
            try
            {
                SetStatus("Generating embed token...");
                var token = await PowerBIOAuthHelper.GetAccessTokenAsync();
                var embedInfo = await PowerBIApiClient.GetEmbedInfoAsync(token, workspaceId, reportId);

                var embedToken = embedInfo["embedToken"]?.ToString();
                var embedUrl = embedInfo["embedUrl"]?.ToString();

                if (string.IsNullOrEmpty(embedToken) || string.IsNullOrEmpty(embedUrl))
                {
                    SetStatus("Failed to get embed token. Check your Power BI Pro license.");
                    return;
                }

                var html = GetEmbedHtml(embedToken, embedUrl, reportId);
                _webView.NavigateToString(html);
                SetStatus($"Report loaded — {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (msg.Contains("403") || msg.Contains("Forbidden"))
                    msg = "Power BI Pro or Premium Per User license required for embedding. Use 'Publish to Web' for public reports.";
                SetStatus($"Embed failed: {msg}");
            }
        }

        private static string GetEmbedHtml(string embedToken, string embedUrl, string reportId)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
  body {{ margin:0; padding:0; overflow:hidden; background:#1A1B1E; }}
  #reportContainer {{ width:100vw; height:100vh; }}
  .loading {{ display:flex; align-items:center; justify-content:center; height:100vh;
              color:#C1C2C5; font-family:'Segoe UI',sans-serif; font-size:16px; }}
</style>
<script src=""https://cdn.jsdelivr.net/npm/powerbi-client@2.23.1/dist/powerbi.min.js""></script>
</head>
<body>
<div id=""reportContainer""><div class=""loading"">Loading Power BI report...</div></div>
<script>
  var models = window['powerbi-client'].models;
  var config = {{
    type: 'report',
    tokenType: models.TokenType.Embed,
    accessToken: '{EscapeJs(embedToken)}',
    embedUrl: '{EscapeJs(embedUrl)}',
    id: '{EscapeJs(reportId)}',
    permissions: models.Permissions.Read,
    settings: {{
      panes: {{ filters: {{ visible: true }}, pageNavigation: {{ visible: true }} }},
      background: models.BackgroundType.Transparent
    }}
  }};
  var container = document.getElementById('reportContainer');
  var report = powerbi.embed(container, config);
  report.on('loaded', function() {{
    window.chrome.webview.postMessage(JSON.stringify({{ type:'loaded' }}));
  }});
  report.on('error', function(event) {{
    window.chrome.webview.postMessage(JSON.stringify({{ type:'error', message:event.detail.message }}));
  }});
</script>
</body>
</html>";
        }

        private static string EscapeJs(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var msg = JObject.Parse(e.WebMessageAsJson ?? e.TryGetWebMessageAsString());
                var type = msg["type"]?.ToString();

                Dispatcher.Invoke(() =>
                {
                    if (type == "loaded")
                        SetStatus($"Report loaded — {DateTime.Now:HH:mm:ss}");
                    else if (type == "error")
                        SetStatus($"Report error: {msg["message"]}");
                });
            }
            catch { }
        }

        private void ToggleFullscreen()
        {
            if (WindowStyle == WindowStyle.None)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
                _toolbarBorder.Visibility = Visibility.Visible;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                _toolbarBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void SetStatus(string text)
        {
            if (_statusText != null)
                _statusText.Text = text;
        }

        private string GetUrlBoxText()
        {
            if (_urlBox == null) return "";
            return _urlBox.Foreground == DarkTheme.FgDim ? "" : _urlBox.Text;
        }

        private void SetUrlBoxText(string url)
        {
            if (_urlBox == null) return;
            _urlBox.Text = url;
            _urlBox.Foreground = DarkTheme.FgWhite;
        }

        private void ShowWebView2InstallPrompt()
        {
            var result = MessageBox.Show(
                "The WebView2 Runtime is required to display Power BI reports inside Revit.\n\n" +
                "It is included with Windows 11 and recent Windows 10 updates.\n\n" +
                "Would you like to download the WebView2 Runtime installer?",
                "WebView2 Runtime Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
            }

            Close();
        }
    }
}
