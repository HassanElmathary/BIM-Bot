using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using BIMBotNavisPlugin.Core;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.UI
{
    /// <summary>
    /// Dialog for the "Clash Snapshots" button: choose test, status, folder, limit and size, then
    /// export red/green isolated images with a progress bar and Cancel. Runs on the UI thread and
    /// pumps messages between images (Application.DoEvents) so the bar repaints and Cancel works.
    /// </summary>
    public class ClashSnapshotsForm : Form
    {
        private static readonly string[] StatusLabels = { "Open (New + Active)", "All", "New", "Active", "Reviewed", "Approved", "Resolved" };
        private static readonly string[] StatusFilters = { "open", "all", "New", "Active", "Reviewed", "Approved", "Resolved" };
        private static readonly string[] SizeLabels = { "1280 x 720", "1920 x 1080", "960 x 540" };
        private static readonly int[,] Sizes = { { 1280, 720 }, { 1920, 1080 }, { 960, 540 } };

        private readonly ComboBox _test = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
        private readonly ComboBox _status = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly TextBox _folder = new TextBox { Width = 300 };
        private readonly Button _browse = new Button { Text = "Browse...", Width = 75 };
        private readonly NumericUpDown _max = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = 500, Width = 90 };
        private readonly ComboBox _size = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        private readonly ProgressBar _bar = new ProgressBar { Width = 380, Height = 18 };
        private readonly Label _progress = new Label { AutoSize = true, Text = "Ready." };
        private readonly Button _start = new Button { Text = "Start", Width = 90 };
        private readonly Button _cancel = new Button { Text = "Close", Width = 90 };
        private bool _running;
        private bool _cancelRequested;

        public ClashSnapshotsForm(List<string> tests)
        {
            Text = "BIM-Bot — Clash Snapshots";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);
            foreach (var t in tests) _test.Items.Add(t);
            if (_test.Items.Count > 0) _test.SelectedIndex = 0;
            _status.Items.AddRange(StatusLabels);
            _status.SelectedIndex = 0;
            _size.Items.AddRange(SizeLabels);
            _size.SelectedIndex = 0;
            _folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BIM-Bot Clash Snapshots");
            var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            AddRow(grid, "Clash test:", _test);
            AddRow(grid, "Clashes:", _status);
            var folderRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            folderRow.Controls.Add(_folder);
            folderRow.Controls.Add(_browse);
            AddRow(grid, "Save to folder:", folderRow);
            AddRow(grid, "Max images:", _max);
            AddRow(grid, "Image size:", _size);
            AddRow(grid, "", _bar);
            AddRow(grid, "", _progress);
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            buttons.Controls.Add(_cancel);
            buttons.Controls.Add(_start);
            AddRow(grid, "", buttons);
            Controls.Add(grid);
            AcceptButton = _start;
            _browse.Click += (s, e) => Browse();
            _start.Click += (s, e) => Run();
            _cancel.Click += (s, e) => { if (_running) _cancelRequested = true; else Close(); };
            FormClosing += (s, e) => { if (_running) { _cancelRequested = true; e.Cancel = true; } };
        }

        private static void AddRow(TableLayoutPanel grid, string label, Control control)
        {
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) });
            grid.Controls.Add(control);
        }

        private void Browse()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Folder for the clash images", SelectedPath = _folder.Text })
                if (dlg.ShowDialog(this) == DialogResult.OK) _folder.Text = dlg.SelectedPath;
        }

        private void SetInputsEnabled(bool on)
        {
            _test.Enabled = _status.Enabled = _folder.Enabled = _browse.Enabled = _max.Enabled = _size.Enabled = _start.Enabled = on;
            _cancel.Text = on ? "Close" : "Cancel";
        }

        private void Run()
        {
            if (_test.SelectedItem == null) return;
            var test = _test.SelectedItem.ToString();
            var root = string.IsNullOrWhiteSpace(_folder.Text)
                ? Path.GetDirectoryName(NavisCommandExecutor.DefaultSnapshotFolder(test))!
                : _folder.Text.Trim();
            var folder = Path.Combine(root, $"{NavisCommandExecutor.SafeFileName(test)}_{DateTime.Now:yyyyMMdd_HHmm}");
            int w = Sizes[_size.SelectedIndex, 0], h = Sizes[_size.SelectedIndex, 1];
            _running = true;
            _cancelRequested = false;
            SetInputsEnabled(false);
            JObject? result = null;
            try
            {
                result = NavisCommandExecutor.ExportClashSnapshots(test, StatusFilters[_status.SelectedIndex], folder,
                    (int)_max.Value, w, h, (done, total) =>
                    {
                        _bar.Maximum = Math.Max(1, total);
                        _bar.Value = Math.Min(done, _bar.Maximum);
                        _progress.Text = $"Saving image {Math.Min(done + 1, total)} of {total}...";
                        Application.DoEvents();
                        return !_cancelRequested;
                    });
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Clash Snapshots failed", ex);
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _running = false;
                SetInputsEnabled(true);
            }
            if (result == null) return;
            var saved = result.Value<int>("saved");
            var failed = result.Value<int>("failed");
            var matching = result.Value<int>("matching");
            var msg = (result.Value<bool>("cancelled") ? "Cancelled.\n\n" : "") +
                      $"Saved {saved} image(s) of {matching} matching clash(es)" +
                      (failed > 0 ? $", {failed} failed (see clashes.csv)" : "") +
                      (result.Value<bool>("truncated") ? $".\nOnly the first {(int)_max.Value} were exported (Max images)." : ".") +
                      $"\n\nFolder:\n{folder}\n\nOpen the folder now?";
            _progress.Text = $"Done: {saved} saved.";
            if (MessageBox.Show(this, msg, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try { Process.Start("explorer.exe", "\"" + folder + "\""); } catch { }
            }
        }
    }
}
