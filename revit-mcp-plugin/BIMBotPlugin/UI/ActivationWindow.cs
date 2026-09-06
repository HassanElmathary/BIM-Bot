using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BIMBotPlugin.Core;
using BIMBotPlugin.UI.Themes;

namespace BIMBotPlugin.UI
{
    /// <summary>
    /// Activation dialog for BIM-Bot license activation.
    /// Displays Device Username and Machine ID, allows the user to send
    /// an activation request to the admin, and enter the activation key.
    /// Styled with DarkTheme for consistency with the rest of the plugin.
    /// </summary>
    public class ActivationWindow : Window
    {
        private readonly TextBox _licenseKeyBox;
        private readonly TextBox _emailBox;
        private readonly TextBlock _statusLabel;
        private readonly Button _activateBtn;
        private readonly Button _sendRequestBtn;

        public ActivationWindow()
        {
            Title = "Activate BIM-Bot";
            Width = 520;
            Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            DarkTheme.Apply(this);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var stack = new StackPanel { Margin = new Thickness(28) };

            // ── Header ──────────────────────────────────────
            stack.Children.Add(new TextBlock
            {
                Text = "🔑 Activate BIM-Bot",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = DarkTheme.FgLight,
                Margin = new Thickness(0, 0, 0, 4)
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"BIM-Bot v{BIMBotPlugin.Core.Application.Version}",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 0, 0, 20)
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Enter your activation key to unlock all BIM-Bot tools.\n" +
                       "If you don't have a key, send an activation request to the admin.",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });

            // ── Device Info Section ─────────────────────────
            var deviceInfoBox = DarkTheme.MakeGroupBox("📋 Device Information", BuildDeviceInfoPanel());
            stack.Children.Add(deviceInfoBox);

            // ── Request Activation Section ──────────────────
            stack.Children.Add(new TextBlock
            {
                Text = "Step 1: Send Activation Request",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgLight,
                Margin = new Thickness(0, 20, 0, 8)
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Send your device info to the admin to receive an activation key.",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Email input - the backend requires it, and it is where the key is sent.
            stack.Children.Add(new TextBlock
            {
                Text = "Your email address (the activation key is sent here):",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                Margin = new Thickness(0, 0, 0, 4)
            });

            _emailBox = DarkTheme.MakeTextBox("", "you@company.com");
            _emailBox.FontSize = 13;
            _emailBox.Margin = new Thickness(0, 0, 0, 8);
            stack.Children.Add(_emailBox);

            // Send request button row
            var requestBtnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };

            _sendRequestBtn = MakeActionButton("📧 Send Request to Admin", DarkTheme.BgAccent);
            _sendRequestBtn.Click += OnSendRequest;
            requestBtnPanel.Children.Add(_sendRequestBtn);

            var copyBtn = MakeActionButton("📋 Copy Info", new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)));
            copyBtn.Margin = new Thickness(8, 0, 0, 0);
            copyBtn.Click += OnCopyInfo;
            requestBtnPanel.Children.Add(copyBtn);

            stack.Children.Add(requestBtnPanel);

            // ── Activation Key Input Section ────────────────
            stack.Children.Add(new TextBlock
            {
                Text = "Step 2: Enter Activation Key",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgLight,
                Margin = new Thickness(0, 16, 0, 8)
            });

            stack.Children.Add(DarkTheme.MakeLabel("Activation Key", required: true));

            _licenseKeyBox = DarkTheme.MakeTextBox("", "Paste your activation key here...");
            _licenseKeyBox.FontFamily = new FontFamily("Consolas");
            _licenseKeyBox.FontSize = 14;
            _licenseKeyBox.Margin = new Thickness(0, 0, 0, 8);
            // Enable Enter key to activate
            _licenseKeyBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) OnActivate(s, e);
            };
            stack.Children.Add(_licenseKeyBox);

            // ── Status Label ────────────────────────────────
            _statusLabel = new TextBlock
            {
                Text = "",
                FontSize = 11,
                Foreground = DarkTheme.FgDim,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(_statusLabel);

            // ── Buttons ─────────────────────────────────────
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var cancelBtn = DarkTheme.MakeCancelButton();
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            btnPanel.Children.Add(cancelBtn);

            _activateBtn = DarkTheme.MakePrimaryButton("🚀 Activate Now");
            _activateBtn.Margin = new Thickness(10, 0, 0, 0);
            _activateBtn.Click += OnActivate;
            btnPanel.Children.Add(_activateBtn);

            stack.Children.Add(btnPanel);

            scroll.Content = stack;
            Content = scroll;
        }

        // ════════════════════════════════════════════════════
        // Device Info Panel Builder
        // ════════════════════════════════════════════════════

        private UIElement BuildDeviceInfoPanel()
        {
            var panel = new StackPanel();

            // Device Username
            var usernameRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            usernameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            usernameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var usernameLabel = new TextBlock
            {
                Text = "Username:",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(usernameLabel, 0);
            usernameRow.Children.Add(usernameLabel);

            var usernameValue = new TextBlock
            {
                Text = LicenseService.GetDeviceUsername(),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgWhite,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(usernameValue, 1);
            usernameRow.Children.Add(usernameValue);
            panel.Children.Add(usernameRow);

            // Machine Name
            var machineRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            machineRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            machineRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var machineLabel = new TextBlock
            {
                Text = "Machine:",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(machineLabel, 0);
            machineRow.Children.Add(machineLabel);

            var machineValue = new TextBlock
            {
                Text = LicenseService.GetMachineName(),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = DarkTheme.FgWhite,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(machineValue, 1);
            machineRow.Children.Add(machineValue);
            panel.Children.Add(machineRow);

            // Machine ID
            var idRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            idRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            idRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var idLabel = new TextBlock
            {
                Text = "Machine ID:",
                FontSize = 12,
                Foreground = DarkTheme.FgDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(idLabel, 0);
            idRow.Children.Add(idLabel);

            var idValue = new TextBlock
            {
                Text = LicenseService.GetMachineId(),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), // Amber
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(idValue, 1);
            idRow.Children.Add(idValue);
            panel.Children.Add(idRow);

            return panel;
        }

        // ════════════════════════════════════════════════════
        // Event Handlers
        // ════════════════════════════════════════════════════

        /// <summary>
        /// Cheap sanity check only - the backend is the real validator. This just
        /// stops obviously empty or malformed input reaching a 400 response.
        /// </summary>
        private static bool IsPlausibleEmail(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (value.IndexOf(' ') >= 0) return false;
            var at = value.IndexOf('@');
            if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1) return false;
            var domain = value.Substring(at + 1);
            return domain.Contains(".") && !domain.StartsWith(".") && !domain.EndsWith(".");
        }

        /// <summary>
        /// Sends an activation request email and/or Firebase request.
        /// Opens the default email client with pre-formatted data as primary action,
        /// and also sends a background request to Firebase.
        /// </summary>
        private async void OnSendRequest(object sender, RoutedEventArgs e)
        {
            var email = _emailBox.Text?.Trim() ?? "";
            if (!IsPlausibleEmail(email))
            {
                SetStatus("⚠️ Enter a valid email address so the admin can send your key.",
                    new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)));
                _emailBox.Focus();
                return;
            }

            SetStatus("Sending activation request...", DarkTheme.FgDim);
            _sendRequestBtn.IsEnabled = false;

            try
            {
                var machineId = LicenseService.GetMachineId();
                var username = LicenseService.GetDeviceUsername();
                var machineName = LicenseService.GetMachineName();

                // 1. Open email client with pre-formatted request
                var subject = Uri.EscapeDataString($"BIM-Bot Activation Request — {username}");
                var body = Uri.EscapeDataString(
                    $"BIM-Bot Activation Request\n" +
                    $"========================\n\n" +
                    $"Username:       {username}\n" +
                    $"Email:          {email}\n" +
                    $"Machine Name:   {machineName}\n" +
                    $"Machine ID:     {machineId}\n" +
                    $"Plugin Version: {BIMBotPlugin.Core.Application.Version}\n\n" +
                    $"Please send me the activation key for this machine.\n" +
                    $"Thank you!");

                var mailtoUrl = $"mailto:hassan.elmathary@gmail.com?subject={subject}&body={body}";

                try
                {
                    Process.Start(new ProcessStartInfo(mailtoUrl) { UseShellExecute = true });
                }
                catch (Exception mailEx)
                {
                    Logger.LogError("Failed to open email client", mailEx);
                }

                // 2. Also send to Firebase as backup
                var firebaseResult = await LicenseService.RequestActivationAsync(username, email);

                SetStatus(
                    "✅ Request sent! Check your email client.\n" +
                    "The admin will reply with your activation key.",
                    new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81))); // Green
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Failed to send request: {ex.Message}",
                    new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44))); // Red
            }
            finally
            {
                _sendRequestBtn.IsEnabled = true;
            }
        }

        /// <summary>Copies the device info to clipboard.</summary>
        private void OnCopyInfo(object sender, RoutedEventArgs e)
        {
            var info =
                $"BIM-Bot Activation Request\n" +
                $"Username:     {LicenseService.GetDeviceUsername()}\n" +
                $"Machine Name: {LicenseService.GetMachineName()}\n" +
                $"Machine ID:   {LicenseService.GetMachineId()}\n" +
                $"Version:      {BIMBotPlugin.Core.Application.Version}";

            try
            {
                Clipboard.SetText(info);
                SetStatus("📋 Device info copied to clipboard!",
                    new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)));
            }
            catch
            {
                SetStatus("❌ Failed to copy to clipboard.",
                    new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)));
            }
        }

        /// <summary>
        /// Validates the entered activation key against Firebase online.
        /// On success, saves the key and closes the dialog.
        /// </summary>
        private async void OnActivate(object sender, RoutedEventArgs e)
        {
            var key = _licenseKeyBox.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(key))
            {
                SetStatus("⚠️ Please enter your activation key.",
                    new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)));
                return;
            }

            SetStatus("🔄 Validating activation key online...", DarkTheme.FgDim);
            _activateBtn.IsEnabled = false;
            _licenseKeyBox.IsEnabled = false;

            try
            {
                var result = await LicenseService.ValidateOnlineAsync(key);

                if (result.Valid)
                {
                    SetStatus($"✅ Activation successful! Welcome, {result.Username ?? LicenseService.GetDeviceUsername()}!",
                        new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)));

                    // Brief pause so the user sees the success message
                    await Task.Delay(1200);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    string errorMsg = result.Reason switch
                    {
                        "not_found" => "❌ No license found for this machine. Please send an activation request first.",
                        "invalid_key" => "❌ Invalid activation key. Please check and try again.",
                        "expired" => "❌ Your license has expired. Please contact the admin for renewal.",
                        "revoked" => "❌ Your license has been revoked. Please contact the admin.",
                        "network_error" => "❌ Cannot reach the activation server. Please check your internet connection.",
                        _ => $"❌ Activation failed: {result.Reason}"
                    };

                    SetStatus(errorMsg, new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)));
                }
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Activation error: {ex.Message}",
                    new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)));
            }
            finally
            {
                _activateBtn.IsEnabled = true;
                _licenseKeyBox.IsEnabled = true;
            }
        }

        // ── Helpers ──────────────────────────────────────────

        private void SetStatus(string text, Brush foreground)
        {
            _statusLabel.Text = text;
            _statusLabel.Foreground = foreground;
        }

        private static Button MakeActionButton(string text, Brush background)
        {
            return new Button
            {
                Content = text,
                Background = background,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 8, 14, 8),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }
    }
}
