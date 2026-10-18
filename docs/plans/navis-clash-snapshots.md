IMPLEMENTATION PLAN — "Clash Snapshots" button (Navisworks ribbon) + matching AI tool
Audience: coding agent (Nemotron 3 Ultra in OpenCode). Follow literally. Do not improvise outside the listed files.
Main repo: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP
Branch: feat/navis-clash-snapshots   (created from main at 254cfbc; main already has the step 1 capture code)

======================================================================
0. BEFORE YOU START — WORK IN YOUR OWN WORKTREE
======================================================================
The main repo folder is shared with another session that is working on a different branch.
Do NOT run `git switch` / `git checkout <branch>` in the main repo folder. Instead:

1. cd "C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP"
2. git worktree add C:\bbsnap feat/navis-clash-snapshots
3. cd C:\bbsnap        <- do ALL work, builds and checks in C:\bbsnap
4. git branch --show-current   -> must print feat/navis-clash-snapshots
5. git status --short          -> must be empty. If not, STOP and report.
6. cd revit-mcp-server && npm ci && cd ..     (node_modules is not in git; tsc needs it)

When finished, LEAVE the worktree in place (the reviewer builds/deploys from it). Do not remove it.

======================================================================
1. GOAL
======================================================================
Coordinators today make clash images one by one. Add:

A. A ribbon button "Clash Snapshots" (Navisworks Add-ins tab, next to the existing "BIM-Bot" button).
   Click -> dialog: pick clash test, status filter, output folder, max images, image size -> Start.
   For each clash it saves one JPEG with item1 RED, item2 GREEN and everything else temporarily hidden
   (exactly what navis_capture_clash already does), plus clashes.csv listing every clash and its image.
   Progress bar + Cancel. Camera and selection are put back at the end; colours/visibility are
   restored after every image (the existing Highlighted() helper already does that).
B. The same export as an AI tool: navis_export_clash_snapshots (capped at 250 images per call so it
   finishes inside the server's 300 s socket timeout; about 1 s per image).

Both A and B call ONE shared method, ExportClashSnapshots(), so there is a single implementation.

======================================================================
2. HARD RULES
======================================================================
1. Only create/edit these files (paths relative to C:\bbsnap):
   - CREATE navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Snapshots.cs
   - CREATE navis-mcp-plugin/BIMBotNavisPlugin/UI/ClashSnapshotsForm.cs
   - CREATE navis-mcp-plugin/BIMBotNavisPlugin/ClashSnapshotsPlugin.cs
   - EDIT   navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs  (add ONE case line)
   - EDIT   revit-mcp-server/src/tools/navis_capture_tools.ts                        (add ONE tool)
2. Touch nothing else (no .csproj change is needed: the project is SDK-style and includes every .cs).
3. No new NuGet/npm packages. Do not deploy the DLL. Do not commit or push.
   No git reset / stash / clean / checkout -- <file>.
4. Reuse existing helpers in the partial class NavisCommandExecutor — do NOT redefine them:
   ActiveDoc, Str, Int, ClashData, AllTests, FindTest, CollectResults, ItemsBox, ApplyViewpoint,
   Highlighted(doc, List<(ModelItem item, Color color)>, JObject), Red, Green (colour fields in Capture.cs).
5. Style: 4-space indent, C# namespace BIMBotNavisPlugin.Core / BIMBotNavisPlugin.UI, TS double quotes.

======================================================================
3. TASKS
======================================================================

----- TASK 1 — Create Core/NavisCommandExecutor.Snapshots.cs with exactly this content -----

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Batch export of clash images: one red/green, isolated JPEG per clash plus clashes.csv.
    /// Shared by the "Clash Snapshots" ribbon button and the navis_export_clash_snapshots command.
    /// Must run on the Navisworks UI thread.
    /// </summary>
    public static partial class NavisCommandExecutor
    {
        internal const int MaxSnapshotsPerAiCall = 250;

        /// <summary>Names of all clash tests in the active document.</summary>
        internal static List<string> ClashTestNames()
        {
            var doc = ActiveDoc();
            return AllTests(ClashData(doc).Tests).Select(t => t.DisplayName ?? "").ToList();
        }

        /// <summary>Default output folder: Documents\BIM-Bot Clash Snapshots\&lt;test&gt;_&lt;yyyyMMdd_HHmm&gt;.</summary>
        internal static string DefaultSnapshotFolder(string testName) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BIM-Bot Clash Snapshots",
            $"{SafeFileName(testName)}_{DateTime.Now:yyyyMMdd_HHmm}");

        internal static string SafeFileName(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            var clean = new string((name ?? "").Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
            if (clean.Length > 80) clean = clean.Substring(0, 80);
            return clean.Length == 0 ? "clash" : clean;
        }

        /// <summary>"" or "all" = every status; "open" = New + Active; otherwise one status name.</summary>
        private static bool StatusMatches(ClashResult r, string filter)
        {
            var f = (filter ?? "").Trim().ToLowerInvariant();
            if (f == "" || f == "all") return true;
            if (f == "open") return r.Status == ClashResultStatus.New || r.Status == ClashResultStatus.Active;
            return r.Status.ToString().Equals(f, StringComparison.OrdinalIgnoreCase);
        }

        private static string Csv(string? value)
        {
            var v = value ?? "";
            return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        /// <summary>
        /// Saves one image per matching clash (up to maxImages) into folder, plus clashes.csv.
        /// progress(done, total) is called before each image; return false to cancel.
        /// Camera and selection are restored at the end, even on error or cancel.
        /// </summary>
        internal static JObject ExportClashSnapshots(string testName, string statusFilter, string folder,
            int maxImages, int width, int height, Func<int, int, bool>? progress)
        {
            var doc = ActiveDoc();
            if (doc.ActiveView == null) throw new Exception("No active 3D view.");
            var test = FindTest(ClashData(doc), testName);
            if (string.IsNullOrWhiteSpace(folder)) folder = DefaultSnapshotFolder(test.DisplayName ?? testName);
            Directory.CreateDirectory(folder);

            var all = new List<ClashResult>();
            CollectResults(test.Children, all);
            var matching = all.Where(r => StatusMatches(r, statusFilter)).ToList();
            var picked = matching.Take(Math.Max(1, maxImages)).ToList();

            var cameraBefore = doc.CurrentViewpoint.CreateCopy();
            var selectionBefore = new ModelItemCollection();
            selectionBefore.CopyFrom(doc.CurrentSelection.SelectedItems);
            var opts = new JObject { ["width"] = width, ["height"] = height, ["format"] = "jpeg" };

            var csv = new StringBuilder();
            csv.AppendLine("index,clash,status,distance,item1,item2,image");
            int saved = 0, failed = 0;
            bool cancelled = false;
            var sw = Stopwatch.StartNew();
            var csvPath = Path.Combine(folder, "clashes.csv");
            try
            {
                for (int i = 0; i < picked.Count; i++)
                {
                    if (progress != null && !progress(i, picked.Count)) { cancelled = true; break; }
                    var r = picked[i];
                    var file = $"{i + 1:0000}_{SafeFileName(r.DisplayName ?? "clash")}.jpg";
                    try
                    {
                        var items = new List<ModelItem>();
                        if (r.Item1 != null) items.Add(r.Item1);
                        if (r.Item2 != null) items.Add(r.Item2);
                        if (items.Count == 0) throw new Exception("clash has no items");

                        var vp = doc.CurrentViewpoint.CreateCopy();
                        vp.ZoomBox(ItemsBox(items));
                        ApplyViewpoint(doc, vp);

                        var painted = new List<(ModelItem item, Autodesk.Navisworks.Api.Color color)>();
                        if (r.Item1 != null) painted.Add((r.Item1, Red));
                        if (r.Item2 != null) painted.Add((r.Item2, Green));
                        var shot = Highlighted(doc, painted, opts);
                        File.WriteAllBytes(Path.Combine(folder, file), Convert.FromBase64String(shot.Value<string>("base64") ?? ""));
                        saved++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        file = "ERROR: " + ex.Message;
                    }
                    csv.AppendLine(string.Join(",",
                        (i + 1).ToString(CultureInfo.InvariantCulture),
                        Csv(r.DisplayName),
                        Csv(r.Status.ToString()),
                        r.Distance.ToString("0.###", CultureInfo.InvariantCulture),
                        Csv(r.Item1?.DisplayName),
                        Csv(r.Item2?.DisplayName),
                        Csv(file)));
                }
                if (!cancelled) progress?.Invoke(picked.Count, picked.Count);
            }
            finally
            {
                try { ApplyViewpoint(doc, cameraBefore); } catch { }
                try { doc.CurrentSelection.CopyFrom(selectionBefore); } catch { }
                File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(true));
            }
            sw.Stop();
            NavisLogger.Log($"Clash snapshots: test '{test.DisplayName}', {saved} saved, {failed} failed, cancelled={cancelled}, {sw.ElapsedMilliseconds} ms -> {folder}");
            return new JObject
            {
                ["test"] = test.DisplayName ?? "",
                ["folder"] = folder,
                ["csv"] = csvPath,
                ["totalClashes"] = all.Count,
                ["matching"] = matching.Count,
                ["attempted"] = saved + failed,
                ["saved"] = saved,
                ["failed"] = failed,
                ["cancelled"] = cancelled,
                ["truncated"] = matching.Count > picked.Count,
                ["elapsedMs"] = sw.ElapsedMilliseconds,
            };
        }

        /// <summary>Socket command navis_export_clash_snapshots (AI tool). Capped at MaxSnapshotsPerAiCall.</summary>
        private static JObject ExportClashSnapshotsCommand(JObject p)
        {
            var test = Str(p, "test");
            if (string.IsNullOrWhiteSpace(test)) throw new Exception("test is required (name or GUID, from navis_list_clash_tests).");
            var max = Math.Max(1, Math.Min(MaxSnapshotsPerAiCall, Int(p, "maxImages", 100)));
            var w = Math.Max(320, Math.Min(2560, Int(p, "width", 1280)));
            var h = Math.Max(240, Math.Min(1600, Int(p, "height", 720)));
            return ExportClashSnapshots(test, Str(p, "statusFilter", "open"), Str(p, "folder"), max, w, h, null);
        }
    }
}

Compile notes (change only what the note says):
- If ModelItemCollection.CopyFrom does not accept doc.CurrentSelection.SelectedItems, use
  `selectionBefore.AddRange(doc.CurrentSelection.SelectedItems);` instead.
- If r.Distance is not a double, use `Convert.ToDouble(r.Distance)`.

----- TASK 2 — Create UI/ClashSnapshotsForm.cs with exactly this content -----

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
        private static readonly string[] StatusLabels =
            { "Open (New + Active)", "All", "New", "Active", "Reviewed", "Approved", "Resolved" };
        private static readonly string[] StatusFilters =
            { "open", "all", "New", "Active", "Reviewed", "Approved", "Resolved" };
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

Compile note: if `Path.GetDirectoryName(...)!` (null-forgiving `!`) is rejected, remove the `!`.

----- TASK 3 — Create ClashSnapshotsPlugin.cs (project root, next to BIMBotNavisPlugin.cs) with exactly this content -----

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using BIMBotNavisPlugin.Core;
using BIMBotNavisPlugin.UI;

namespace BIMBotNavisPlugin
{
    /// <summary>
    /// Ribbon button (Add-ins tab): exports a red/green, isolated image of every clash in a test
    /// plus clashes.csv. Works without any AI client connected.
    /// </summary>
    [Plugin("BIMBotClashSnapshots", "BIMB",
        DisplayName = "Clash Snapshots",
        ToolTip = "Save a red/green image of every clash in a clash test, plus a CSV list, to a folder.")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class ClashSnapshotsPlugin : AddInPlugin
    {
        public override int Execute(string[] parameters)
        {
            const string title = "BIM-Bot — Clash Snapshots";
            try
            {
                List<string> tests;
                try
                {
                    tests = NavisCommandExecutor.ClashTestNames();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 0;
                }
                if (tests.Count == 0)
                {
                    MessageBox.Show("This model has no clash tests. Create and run one in Clash Detective first.",
                        title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                using (var form = new ClashSnapshotsForm(tests)) form.ShowDialog();
                return 0;
            }
            catch (Exception ex)
            {
                NavisLogger.LogError("Clash Snapshots button failed", ex);
                MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}

----- TASK 4 — Register the AI command -----
In Core/NavisCommandExecutor.Extended.cs, inside `switch (method)` of ExecuteExtended, add this line
directly AFTER the line `case "navis_capture_clash": return CaptureClash(p);`:

                case "navis_export_clash_snapshots": return ExportClashSnapshotsCommand(p);

Change nothing else in that file.

----- TASK 5 — Add the AI tool to revit-mcp-server/src/tools/navis_capture_tools.ts -----
Inside registerNavisCaptureTools, directly BEFORE its final closing `}`, add:

    server.tool(
        "navis_export_clash_snapshots",
        "Save a red/green, isolated JPEG of every clash in a clash test to a folder, plus clashes.csv " +
        "(clash, status, distance, both items, image file). About 1 s per image; max 250 images per call — " +
        "for more, tell the user to use the 'Clash Snapshots' button in Navisworks (Add-ins tab). " +
        "Default folder: Documents\\BIM-Bot Clash Snapshots\\<test>_<date>.",
        {
            test: z.string().describe("Clash test name or GUID (from navis_list_clash_tests)"),
            statusFilter: z.string().optional().describe("open (default: New + Active) | all | New | Active | Reviewed | Approved | Resolved"),
            maxImages: z.number().optional().describe("Max images, 1–250 (default 100)"),
            folder: z.string().optional().describe("Output folder (default: Documents\\BIM-Bot Clash Snapshots\\<test>_<date>)"),
            width: z.number().optional().describe("Image width in pixels, 320–2560 (default 1280)"),
            height: z.number().optional().describe("Image height in pixels, 240–1600 (default 720)"),
        },
        async (args) => {
            try {
                const response = await withNavisConnection((c) => c.sendCommand("navis_export_clash_snapshots", args));
                return { content: [{ type: "text" as const, text: JSON.stringify(response, null, 2) }] };
            } catch (e) { return { content: [{ type: "text" as const, text: `Failed: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

======================================================================
4. BUILD (in C:\bbsnap, both must pass with 0 errors)
======================================================================
  cd C:\bbsnap\revit-mcp-server && npm run build
  cd C:\bbsnap && dotnet build navis-mcp-plugin/BIMBotNavisPlugin/BIMBotNavisPlugin.csproj -c Release
If a build fails for a reason not covered by a compile note, STOP and report the exact error.

======================================================================
5. SELF-CHECKS (in C:\bbsnap)
======================================================================
  A. git status --short
     -> 3 new files (Snapshots.cs, UI/ClashSnapshotsForm.cs, ClashSnapshotsPlugin.cs)
        + exactly 2 modified (NavisCommandExecutor.Extended.cs, navis_capture_tools.ts)
  B. git diff navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisCommandExecutor.Extended.cs -> exactly 1 added line
  C. findstr /n /c:"BIMBotClashSnapshots" navis-mcp-plugin\BIMBotNavisPlugin\ClashSnapshotsPlugin.cs -> 1 match
  D. findstr /n /c:"navis_export_clash_snapshots" revit-mcp-server\build\tools\navis_capture_tools.js -> at least 1 match
  E. In the MAIN repo folder run `git worktree list` -> shows C:\bbsnap on [feat/navis-clash-snapshots]

(The reviewer will deploy from C:\bbsnap and test live: the button appears in the Add-ins tab, the
dialog lists the clash tests, a 10-image export produces red/green images + clashes.csv, Cancel
stops it, and the camera/selection are restored afterwards.)

======================================================================
6. REPORT BACK
======================================================================
Write the report to C:\bbsnap\clash-snapshots-report.md (NOT in the main repo folder) containing:
1. Files created/changed. 2. Last ~15 lines of each build. 3. Self-checks A–E.
4. Anything you could not do exactly as written, and why.
