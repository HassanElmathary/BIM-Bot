IMPLEMENTATION PLAN — Step 2f: Manual bridge session — Revit "Join Bridge" button (SLAVE 01, 02, …)
Audience: coding agent (Nemotron in OpenCode). Follow literally. Do not improvise outside the listed files.
Repo root: C:\Users\hassa\Documents\OneDrive\01-me\Revit MCP
Branch: feat/navis-2-revit-bridge

IMPORTANT: read THIS file from disk with your file-read tool. Every code block is between a
"----- BEGIN <name> -----" and "----- END <name> -----" line; copy what is between them character for character.

======================================================================
0. BEFORE YOU START
======================================================================
1. git branch --show-current   -> must print feat/navis-2-revit-bridge (do NOT switch branches)
2. git status --short          -> nothing may be MODIFIED (M). Untracked report .md files are fine.
3. Step 2e must already be in the code:
     ls navis-mcp-plugin/BIMBotNavisPlugin/Core/NavisBridgeSession.cs   -> must exist
   If it does not, STOP and report "step 2e is missing".

======================================================================
1. WHAT WE ARE BUILDING
======================================================================
Step 2e added the Navisworks "BIM-Bot Bridge" button: it makes that Navisworks the MASTER and writes
%LOCALAPPDATA%\BIMBot\bridge-session.json with the master's models (the NWCs inside the federated NWD).

This step adds a Revit ribbon button BIM-Bot > "Join Bridge". In the active Revit document:
- No session running             -> tells the user to start the bridge in Navisworks first.
- This document is already a slave -> shows its slot/model and offers to leave.
- Otherwise                       -> makes sure the BIM-Bot service runs (so the slave has a port), shows a
                                     small picker with the master's models (best name match pre-selected),
                                     and adds a slave entry with the lowest free slot "01".."99":
  { "slot": "02", "pid": <Revit process id>, "port": <this Revit's BIM-Bot port>,
    "documentTitle": "<doc.Title>", "documentPath": "<doc.PathName>", "model": "<picked NWC name>",
    "joinedUtc": "<UTC ISO>" }
A slave is ONE Revit document, so two files in one Revit window are two slaves (same pid/port, different
documentTitle). When Revit closes, its slaves are removed.

======================================================================
2. HARD RULES
======================================================================
1. Only create/edit (all under revit-mcp-plugin/BIMBotPlugin/):
   - CREATE Core/BridgeSessionFile.cs
   - CREATE UI/BridgeModelPickerForm.cs
   - CREATE Commands/BridgeJoinCommand.cs
   - EDIT   Core/Application.cs   (Task 4: add one ribbon button; Task 5: one line in OnShutdown)
2. Touch nothing else. No new packages (Newtonsoft.Json and WinForms are already referenced).
   Do not deploy. Do not commit or push. No git reset/stash/clean/checkout.
3. The plugin targets several Revit versions; the code below uses only APIs that exist in all of them.
   Do not add `#if` blocks.

======================================================================
3. TASKS
======================================================================

----- TASK 1 — Create revit-mcp-plugin/BIMBotPlugin/Core/BridgeSessionFile.cs -----

----- BEGIN BridgeSessionFile.cs -----
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// Revit side of the manual bridge session (%LocalAppData%\BIMBot\bridge-session.json).
    /// The Navisworks "BIM-Bot Bridge" button creates the session (master); the Revit "Join Bridge"
    /// button adds this Revit document as a slave (slot 01, 02, …) or removes it.
    /// </summary>
    public static class BridgeSessionFile
    {
        public static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BIMBot", "bridge-session.json");

        private static int Pid => Process.GetCurrentProcess().Id;

        /// <summary>The session if a live Navisworks master owns it; otherwise null.</summary>
        public static JObject? ReadLive()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var s = JObject.Parse(File.ReadAllText(FilePath));
                var masterPid = s["master"]?["pid"]?.Value<int>() ?? 0;
                return ProcessAlive(masterPid) ? s : null;
            }
            catch
            {
                return null;
            }
        }

        public static List<string> Models(JObject session) =>
            (session["master"]?["models"] as JArray)?
                .Select(m => m["name"]?.ToString() ?? "")
                .Where(n => n != "")
                .ToList() ?? new List<string>();

        /// <summary>This Revit process's slave entry for the document, or null.</summary>
        public static JObject? FindSlave(JObject session, string documentTitle) =>
            (session["slaves"] as JArray)?
                .OfType<JObject>()
                .FirstOrDefault(x => x["pid"]?.Value<int>() == Pid
                    && string.Equals(x["documentTitle"]?.ToString(), documentTitle, StringComparison.OrdinalIgnoreCase));

        /// <summary>Lowest slot 01..99 not used by a live slave.</summary>
        public static string NextSlot(JObject session)
        {
            var used = new HashSet<string>(LiveSlaves(session).Select(x => x["slot"]?.ToString() ?? ""));
            return Enumerable.Range(1, 99).Select(i => i.ToString("00")).First(s => !used.Contains(s));
        }

        /// <summary>Adds this document as a slave (dropping slaves of exited Revit processes) and returns its slot.</summary>
        public static string Join(JObject session, string documentTitle, string documentPath, string model, int port)
        {
            var slaves = new JArray(LiveSlaves(session));
            var slot = NextSlot(session);
            slaves.Add(new JObject
            {
                ["slot"] = slot,
                ["pid"] = Pid,
                ["port"] = port,
                ["documentTitle"] = documentTitle,
                ["documentPath"] = documentPath,
                ["model"] = model,
                ["joinedUtc"] = DateTime.UtcNow.ToString("o"),
            });
            session["slaves"] = slaves;
            Write(session);
            Logger.Log($"Bridge: '{documentTitle}' joined as slave {slot} -> {model}");
            return slot;
        }

        public static void Leave(JObject session, string documentTitle)
        {
            var me = FindSlave(session, documentTitle);
            if (me == null) return;
            me.Remove();
            Write(session);
            Logger.Log($"Bridge: '{documentTitle}' left");
        }

        /// <summary>Removes every slave of this Revit process (called on shutdown). Never throws.</summary>
        public static void LeaveAllForThisProcess()
        {
            try
            {
                var s = ReadLive();
                if (s == null || !(s["slaves"] is JArray arr)) return;
                var mine = arr.OfType<JObject>().Where(x => x["pid"]?.Value<int>() == Pid).ToList();
                if (mine.Count == 0) return;
                foreach (var x in mine) x.Remove();
                Write(s);
            }
            catch { }
        }

        /// <summary>Best model for a document title: same name without extension, else one name is a prefix of the other.</summary>
        public static string? BestMatch(string documentTitle, IEnumerable<string> models)
        {
            var list = models.ToList();
            var t = Key(documentTitle);
            if (t == "") return null;
            return list.FirstOrDefault(m => Key(m) == t)
                ?? list.FirstOrDefault(m =>
                {
                    var k = Key(m);
                    return k != "" && (t.StartsWith(k, StringComparison.Ordinal) || k.StartsWith(t, StringComparison.Ordinal));
                });
        }

        private static string Key(string name) =>
            Regex.Replace(Path.GetFileName(name ?? "").Trim(), @"\.(nwc|nwd|nwf|rvt)$", "", RegexOptions.IgnoreCase).ToLowerInvariant();

        private static IEnumerable<JObject> LiveSlaves(JObject session) =>
            (session["slaves"] as JArray)?.OfType<JObject>().Where(x => ProcessAlive(x["pid"]?.Value<int>() ?? 0)).ToList()
            ?? new List<JObject>();

        private static bool ProcessAlive(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                using (var p = Process.GetProcessById(pid)) return !p.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static void Write(JObject session)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, session.ToString());
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }
    }
}
----- END BridgeSessionFile.cs -----

----- TASK 2 — Create revit-mcp-plugin/BIMBotPlugin/UI/BridgeModelPickerForm.cs -----

----- BEGIN BridgeModelPickerForm.cs -----
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BIMBotPlugin.UI
{
    /// <summary>Small dialog: which model of the Navisworks master is this Revit document?</summary>
    public class BridgeModelPickerForm : Form
    {
        private readonly ComboBox _models = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 560,
        };

        public string? SelectedModel => _models.SelectedItem as string;

        public BridgeModelPickerForm(string documentTitle, string slotPreview, IList<string> models, string? preselect)
        {
            Text = "BIM-Bot Bridge — Join as slave";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var label = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                Text = "Revit document:  " + documentTitle + "\n" +
                       "Joins as:  Slave " + slotPreview + "\n\n" +
                       "Which model in the Navisworks master is this document?",
            };

            foreach (var m in models) _models.Items.Add(m);
            if (preselect != null && _models.Items.Contains(preselect)) _models.SelectedItem = preselect;
            else if (_models.Items.Count > 0) _models.SelectedIndex = 0;

            var ok = new Button { Text = "Join", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            layout.Controls.Add(label);
            layout.Controls.Add(_models);
            layout.Controls.Add(buttons);
            Controls.Add(layout);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
----- END BridgeModelPickerForm.cs -----

----- TASK 3 — Create revit-mcp-plugin/BIMBotPlugin/Commands/BridgeJoinCommand.cs -----

----- BEGIN BridgeJoinCommand.cs -----
using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMBotPlugin.Core;
using BIMBotPlugin.UI;
using WinForms = System.Windows.Forms;

namespace BIMBotPlugin.Commands
{
    /// <summary>
    /// "Join Bridge" ribbon button: joins the active Revit document to the BIM-Bot bridge as a slave of
    /// the Navisworks master (slot 01, 02, …), or leaves it when it already joined.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class BridgeJoinCommand : IExternalCommand
    {
        private const string Title = "BIM-Bot Bridge";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument?.Document;
                if (doc == null || doc.IsFamilyDocument)
                {
                    TaskDialog.Show(Title, "Open the project model you want to link to Navisworks first.");
                    return Result.Cancelled;
                }

                var session = BridgeSessionFile.ReadLive();
                if (session == null)
                {
                    TaskDialog.Show(Title, "No bridge is running.\n\nIn Navisworks, open the federated model and click " +
                        "Add-ins > BIM-Bot Bridge (that Navisworks becomes the master). Then click Join Bridge here.");
                    return Result.Cancelled;
                }

                var docTitle = doc.Title;
                var mine = BridgeSessionFile.FindSlave(session, docTitle);
                if (mine != null)
                {
                    var leave = new TaskDialog(Title)
                    {
                        MainInstruction = "This model is Slave " + mine["slot"],
                        MainContent = "Linked to: " + mine["model"] + "\n\nLeave the bridge?",
                        CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                        DefaultButton = TaskDialogResult.No,
                    };
                    if (leave.Show() == TaskDialogResult.Yes)
                    {
                        BridgeSessionFile.Leave(session, docTitle);
                        TaskDialog.Show(Title, "'" + docTitle + "' left the bridge.");
                    }
                    return Result.Succeeded;
                }

                if (!Core.Application.IsServiceRunning) Core.Application.StartService(uiApp);
                var port = Core.Application.SocketServiceInstance?.Port ?? 0;
                if (port <= 0)
                {
                    TaskDialog.Show(Title, "The BIM-Bot service is not running in this Revit, so it cannot join. " +
                        "Click Start BIM-Bot and try again.");
                    return Result.Failed;
                }

                var models = BridgeSessionFile.Models(session);
                if (models.Count == 0)
                {
                    TaskDialog.Show(Title, "The Navisworks master lists no models. Restart the bridge in Navisworks.");
                    return Result.Failed;
                }

                using (var form = new BridgeModelPickerForm(docTitle, BridgeSessionFile.NextSlot(session), models,
                           BridgeSessionFile.BestMatch(docTitle, models)))
                {
                    if (form.ShowDialog() != WinForms.DialogResult.OK || form.SelectedModel == null) return Result.Cancelled;
                    var slot = BridgeSessionFile.Join(session, docTitle, doc.PathName ?? "", form.SelectedModel, port);
                    TaskDialog.Show(Title, "'" + docTitle + "' is now Slave " + slot + ".\nLinked to: " + form.SelectedModel +
                        "\n\nAsk Claude to take a Navisworks clash to Revit.");
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Bridge join failed", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
----- END BridgeJoinCommand.cs -----

----- TASK 4 — Core/Application.cs: add the ribbon button -----
Directly BELOW the line

                corePanel.AddItem(projectFilesData);

insert:

----- BEGIN ribbon-button -----

                // --- Join Bridge (manual Navisworks master / Revit slave pairing) ---
                var bridgeData = new PushButtonData("MCPBridgeJoin", "Join\nBridge", asm,
                    "BIMBotPlugin.Commands.BridgeJoinCommand")
                {
                    ToolTip = "Join this model to the BIM-Bot bridge as a slave of the Navisworks master " +
                              "(start the bridge in Navisworks first: Add-ins > BIM-Bot Bridge). Click again to leave.",
                    LargeImage = RibbonIcons.ProjectFiles(32),
                    Image = RibbonIcons.ProjectFiles(16)
                };
                corePanel.AddItem(bridgeData);
----- END ribbon-button -----

----- TASK 5 — Core/Application.cs: leave the bridge when Revit closes -----
In OnShutdown, find the FIRST occurrence of

                _socketService?.Stop();

(the one inside OnShutdown, right before `Logger.Log("BIM-Bot Plugin shut down");`) and insert directly ABOVE it:

----- BEGIN shutdown-line -----
                BridgeSessionFile.LeaveAllForThisProcess();
----- END shutdown-line -----

Change nothing else in Application.cs.

======================================================================
4. BUILD (both bands must pass with 0 errors)
======================================================================
  dotnet build revit-mcp-plugin/BIMBotPlugin/BIMBotPlugin.csproj -c Release -p:RevitVersion=2023
  dotnet build revit-mcp-plugin/BIMBotPlugin/BIMBotPlugin.csproj -c Release -p:RevitVersion=2026

(2023 builds net48, 2026 builds net8.0-windows.) If a build fails, STOP and report the exact errors.
Compile notes (change only what the note says):
- If `Application` is ambiguous in BridgeJoinCommand.cs, it is already written as Core.Application — keep it.
- If File.Replace fails to compile, use: File.Copy(tmp, FilePath, true); File.Delete(tmp);
- If the build reports duplicate definitions in files ending "-HassanElmthary.cs" or " - Copy.cs", those are
  OneDrive conflict copies: STOP and report them, do not delete anything.

======================================================================
5. SELF-CHECKS
======================================================================
  A. git status --short -> new: Core/BridgeSessionFile.cs, UI/BridgeModelPickerForm.cs,
     Commands/BridgeJoinCommand.cs ; modified: Core/Application.cs. Nothing else.
  B. git diff revit-mcp-plugin/BIMBotPlugin/Core/Application.cs -> only additions (the button block + 1 line)
  C. grep -n "MCPBridgeJoin\|LeaveAllForThisProcess" revit-mcp-plugin/BIMBotPlugin/Core/Application.cs -> 2 matches

======================================================================
6. REPORT BACK
======================================================================
Write STEP2F_BRIDGE_SLAVE_REPORT.md in the repo root: 1. files, 2. last ~10 lines of BOTH builds,
3. self-checks A–C, 4. anything not done exactly as written, and why.
