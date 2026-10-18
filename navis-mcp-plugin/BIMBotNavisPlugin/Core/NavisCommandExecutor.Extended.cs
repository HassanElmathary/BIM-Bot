using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Autodesk.Navisworks.Api.Timeliner;
using Newtonsoft.Json.Linq;
using ComBridge = Autodesk.Navisworks.Api.ComApi.ComApiBridge;
using NwCom = Autodesk.Navisworks.Api.Interop.ComApi;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Extended navis_* commands: view/display settings, camera, section planes,
    /// TimeLiner, clash-test authoring, measurements, redlines, comments,
    /// selection/search sets, user-defined properties and runtime C# execution.
    /// Same contract as the core executor: main thread, clean exception messages.
    /// </summary>
    public static partial class NavisCommandExecutor
    {
        private static JObject? ExecuteExtended(string method, JObject p)
        {
            switch (method)
            {
                // view / display
                case "navis_set_view_style": return SetViewStyle(p);
                case "navis_set_background": return SetBackground(p);
                case "navis_get_grids": return GetGrids();
                case "navis_set_grids": return SetGrids(p);
                case "navis_section": return Section(p);
                // camera
                case "navis_get_camera": return GetCamera();
                case "navis_set_camera": return SetCamera(p);
                case "navis_zoom_to_items": return ZoomToItems(p);
                case "navis_view_preset": return ViewPreset(p);
                case "navis_capture_view": return CaptureView(p);
                case "navis_capture_clash": return CaptureClash(p);
                case "navis_export_clash_snapshots": return ExportClashSnapshotsCommand(p);
                // TimeLiner
                case "navis_timeliner_list_tasks": return TimelinerList(p);
                case "navis_timeliner_add_task": return TimelinerAdd(p);
                case "navis_timeliner_update_task": return TimelinerUpdate(p);
                case "navis_timeliner_delete_task": return TimelinerDelete(p);
                // clash authoring
                case "navis_create_clash_test": return ClashCreate(p);
                case "navis_update_clash_test": return ClashUpdate(p);
                case "navis_delete_clash_test": return ClashDelete(p);
                case "navis_edit_clash_results": return ClashEditResults(p);
                // measure / redline / comments
                case "navis_measure": return Measure(p);
                case "navis_get_redlines": return GetRedlines();
                case "navis_set_redlines": return SetRedlines(p);
                case "navis_add_comment": return AddComment(p);
                case "navis_list_comments": return ListComments(p);
                // sets / properties
                case "navis_create_selection_set": return CreateSelectionSet(p);
                case "navis_create_search_set": return CreateSearchSet(p);
                case "navis_manage_selection_set": return ManageSelectionSet(p);
                case "navis_get_item_properties": return GetItemProperties(p);
                case "navis_set_user_properties": return SetUserProperties(p);
                // code
                case "navis_execute_code": return ExecuteCode(p);
                default: return null;
            }
        }

        // ── shared helpers ──

        private static List<string> Ids(JObject p, string name = "displayIds") =>
            p[name]?.Values<string>().Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList()
            ?? new List<string>();

        /// <summary>Accepts 0-1 or 0-255 channels.</summary>
        private static Color ColorFrom(JToken? t, string what)
        {
            if (t is not JArray a || a.Count < 3) throw new Exception($"{what} must be [r,g,b].");
            var c = a.Take(3).Select(v => v.Value<double>()).ToArray();
            if (c.Any(v => v > 1)) c = c.Select(v => v / 255.0).ToArray();
            return new Color(c[0], c[1], c[2]);
        }

        private static Point3D PointFrom(JToken? t, string what)
        {
            if (t is not JArray a || a.Count < 3) throw new Exception($"{what} must be [x,y,z].");
            return new Point3D(a[0].Value<double>(), a[1].Value<double>(), a[2].Value<double>());
        }

        private static JArray PointJson(Point3D pt) => new JArray(pt.X, pt.Y, pt.Z);

        private static BoundingBox3D ItemsBox(IEnumerable<ModelItem> items)
        {
            BoundingBox3D? box = null;
            foreach (var item in items)
            {
                var b = item.BoundingBox();
                if (b == null || b.IsEmpty) continue;
                box = box == null ? b : box.Extend(b);
            }
            if (box == null) throw new Exception("The given items have no geometry/bounding box.");
            return box;
        }

        /// <summary>displayIds, or the current selection when none are given.</summary>
        private static List<ModelItem> ItemsOrSelection(Document doc, JObject p)
        {
            var ids = Ids(p);
            if (ids.Count > 0) return ResolveItems(doc, ids);
            var sel = doc.CurrentSelection.SelectedItems.Cast<ModelItem>().ToList();
            if (sel.Count == 0) throw new Exception("Pass displayIds or select items in Navisworks first.");
            return sel;
        }

        private static void ApplyViewpoint(Document doc, Viewpoint vp) => doc.CurrentViewpoint.CopyFrom(vp);

        private static IEnumerable<(SavedItem item, GroupItem? parent, int index)> WalkSaved(SavedItemCollection items, GroupItem? parent)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                yield return (item, parent, i);
                if (item is GroupItem g)
                    foreach (var child in WalkSaved(g.Children, g)) yield return child;
            }
        }

        private static T FindSaved<T>(SavedItemCollection root, string idOrName, string kind, string hint) where T : SavedItem
        {
            foreach (var (item, _, _) in WalkSaved(root, null))
            {
                if (item is T t && (string.Equals(t.DisplayName, idOrName, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(t.Guid.ToString(), idOrName, StringComparison.OrdinalIgnoreCase)))
                    return t;
            }
            throw new Exception($"{kind} '{idOrName}' not found. {hint}");
        }

        private static SelectionSet FindSet(Document doc, string idOrName) =>
            FindSaved<SelectionSet>(doc.SelectionSets.Value, idOrName, "Selection/search set", "Use navis_get_selection_sets.");

        private static JArray CommentsJson(CommentCollection? comments)
        {
            var arr = new JArray();
            if (comments == null) return arr;
            foreach (Comment c in comments)
                arr.Add(new JObject
                {
                    ["id"] = c.Id,
                    ["author"] = c.Author ?? "",
                    ["status"] = c.Status.ToString(),
                    ["date"] = c.CreationDate.ToString("o"),
                    ["body"] = c.Body ?? "",
                });
            return arr;
        }

        // ── view / display ──

        private static JObject SetViewStyle(JObject p)
        {
            var doc = ActiveDoc();
            var vp = doc.CurrentViewpoint.CreateCopy();
            var changed = new JObject();
            var rs = Str(p, "renderStyle");
            if (rs != "")
            {
                if (!Enum.TryParse(rs, true, out ViewpointRenderStyle style))
                    throw new Exception("renderStyle must be FullRender, Preview, Shaded, Wireframe or HiddenLine.");
                vp.RenderStyle = style;
                changed["renderStyle"] = style.ToString();
            }
            var li = Str(p, "lighting");
            if (li != "")
            {
                if (!Enum.TryParse(li, true, out ViewpointLighting lighting))
                    throw new Exception("lighting must be None, SceneLights, Headlight or FullLights.");
                vp.Lighting = lighting;
                changed["lighting"] = lighting.ToString();
            }
            var pr = Str(p, "projection");
            if (pr != "")
            {
                if (!Enum.TryParse(pr, true, out ViewpointProjection proj))
                    throw new Exception("projection must be Perspective or Orthographic.");
                vp.Projection = proj;
                changed["projection"] = proj.ToString();
            }
            if (changed.Count == 0) throw new Exception("Pass at least one of renderStyle, lighting, projection.");
            ApplyViewpoint(doc, vp);
            return new JObject { ["applied"] = changed };
        }

        private static JObject SetBackground(JObject p)
        {
            var doc = ActiveDoc();
            var mode = Str(p, "mode", "plain").ToLowerInvariant();
            var colors = p["colors"] as JArray ?? throw new Exception("colors is required: [[r,g,b], ...].");
            switch (mode)
            {
                case "plain":
                    doc.SetPlainBackground(ColorFrom(colors[0], "colors[0]"));
                    break;
                case "graduated":
                    if (colors.Count < 2) throw new Exception("graduated needs 2 colors (top, bottom).");
                    doc.SetGraduatedBackground(ColorFrom(colors[0], "colors[0]"), ColorFrom(colors[1], "colors[1]"));
                    break;
                case "horizon":
                    if (colors.Count < 4) throw new Exception("horizon needs 4 colors (sky, horizon sky, horizon ground, ground).");
                    doc.SetHorizonBackground(ColorFrom(colors[0], "colors[0]"), ColorFrom(colors[1], "colors[1]"),
                        ColorFrom(colors[2], "colors[2]"), ColorFrom(colors[3], "colors[3]"));
                    break;
                default: throw new Exception("mode must be plain, graduated or horizon.");
            }
            return new JObject { ["background"] = mode };
        }

        private static JObject GetGrids()
        {
            var doc = ActiveDoc();
            var grids = doc.Grids;
            var systems = new JArray();
            foreach (GridSystem s in grids.Systems)
            {
                systems.Add(new JObject
                {
                    ["name"] = s.DisplayName ?? "",
                    ["levels"] = new JArray(s.Levels.Cast<GridLevel>().Select(l =>
                        new JObject { ["name"] = l.DisplayName ?? "", ["elevation"] = l.Elevation })),
                    ["lockedLevel"] = s.LockedLevel?.DisplayName,
                });
            }
            return new JObject
            {
                ["renderMode"] = grids.RenderMode.ToString(),
                ["activeSystem"] = grids.ActiveSystem?.DisplayName,
                ["systems"] = systems,
            };
        }

        private static JObject SetGrids(JObject p)
        {
            var doc = ActiveDoc();
            var grids = doc.Grids;
            var changed = new JObject();
            var sysName = Str(p, "activeSystem");
            if (sysName != "")
            {
                var sys = grids.Systems.Cast<GridSystem>().FirstOrDefault(s =>
                    string.Equals(s.DisplayName, sysName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new Exception($"Grid system '{sysName}' not found. Use navis_get_grids.");
                grids.ActiveSystem = sys;
                changed["activeSystem"] = sys.DisplayName;
            }
            var mode = Str(p, "renderMode");
            if (mode != "")
            {
                if (!Enum.TryParse(mode, true, out GridsRenderMode rm))
                    throw new Exception("renderMode must be AboveAndBelow, Above, Below, All or Locked.");
                grids.RenderMode = rm;
                changed["renderMode"] = rm.ToString();
            }
            var lvl = Str(p, "lockedLevel");
            if (lvl != "")
            {
                var sys = grids.ActiveSystem ?? throw new Exception("No active grid system.");
                var level = sys.Levels.Cast<GridLevel>().FirstOrDefault(l =>
                    string.Equals(l.DisplayName, lvl, StringComparison.OrdinalIgnoreCase))
                    ?? throw new Exception($"Level '{lvl}' not found in grid system '{sys.DisplayName}'.");
                grids.SetSystemLockedLevel(sys, level);
                grids.RenderMode = GridsRenderMode.Locked;
                changed["lockedLevel"] = level.DisplayName;
            }
            if (changed.Count == 0) throw new Exception("Pass activeSystem, renderMode and/or lockedLevel.");
            return new JObject
            {
                ["applied"] = changed,
                ["note"] = "Grid on/off visibility is a View-tab toggle with no API; this sets which system/levels are shown.",
            };
        }

        /// <summary>
        /// Section planes / section box via the View clipping-plane JSON API.
        /// action: get | set (raw json) | box (fit a section box to items or min/max) | clear.
        /// </summary>
        private static JObject Section(JObject p)
        {
            var doc = ActiveDoc();
            var view = doc.ActiveView ?? throw new Exception("No active view.");
            var action = Str(p, "action", "get").ToLowerInvariant();
            switch (action)
            {
                case "get":
                    return new JObject { ["clippingPlanes"] = JToken.Parse(view.GetClippingPlanes()) };
                case "set":
                {
                    var json = p["json"] ?? throw new Exception("json is required for action=set (see action=get for the format).");
                    var text = json.Type == JTokenType.String ? json.Value<string>()! : json.ToString(Newtonsoft.Json.Formatting.None);
                    if (!view.TrySetClippingPlanes(text))
                        throw new Exception("Navisworks rejected the clipping-plane JSON. Call action=get to see the expected shape.");
                    return new JObject { ["set"] = true };
                }
                case "box":
                {
                    BoundingBox3D box;
                    if (p["min"] != null && p["max"] != null)
                        box = new BoundingBox3D(PointFrom(p["min"], "min"), PointFrom(p["max"], "max"));
                    else
                        box = ItemsBox(ItemsOrSelection(doc, p));
                    var pad = Num(p, "padding", 0);
                    var json = new JObject
                    {
                        ["Type"] = "ClipPlaneSet",
                        ["Version"] = 1,
                        ["OrientedBox"] = new JObject
                        {
                            ["Type"] = "OrientedBox3D",
                            ["Version"] = 1,
                            ["Box"] = new JArray(
                                new JArray(box.Min.X - pad, box.Min.Y - pad, box.Min.Z - pad),
                                new JArray(box.Max.X + pad, box.Max.Y + pad, box.Max.Z + pad)),
                            ["Rotation"] = new JArray(0, 0, 0),
                        },
                        ["Enabled"] = true,
                    };
                    if (!view.TrySetClippingPlanes(json.ToString(Newtonsoft.Json.Formatting.None)))
                        throw new Exception("Navisworks rejected the section box. Use action=get to inspect the current format.");
                    return new JObject { ["sectionBox"] = new JObject { ["min"] = PointJson(box.Min), ["max"] = PointJson(box.Max), ["padding"] = pad } };
                }
                case "clear":
                {
                    var cur = JObject.Parse(view.GetClippingPlanes());
                    cur["Enabled"] = false;
                    view.TrySetClippingPlanes(cur.ToString(Newtonsoft.Json.Formatting.None));
                    return new JObject { ["sectioning"] = "disabled" };
                }
                default: throw new Exception("action must be get, set, box or clear.");
            }
        }

        // ── camera ──

        private static JObject GetCamera()
        {
            var doc = ActiveDoc();
            var vp = doc.CurrentViewpoint.CreateCopy();
            return new JObject
            {
                ["position"] = PointJson(vp.Position),
                ["projection"] = vp.Projection.ToString(),
                ["renderStyle"] = vp.HasRenderStyle ? vp.RenderStyle.ToString() : null,
                ["lighting"] = vp.HasLighting ? vp.Lighting.ToString() : null,
                ["focalDistance"] = vp.HasFocalDistance ? vp.FocalDistance : (double?)null,
                ["verticalFieldOfView"] = vp.HeightField,
                ["units"] = doc.Units.ToString(),
                ["camera"] = JToken.Parse(vp.GetCamera()),
            };
        }

        private static JObject SetCamera(JObject p)
        {
            var doc = ActiveDoc();
            var vp = doc.CurrentViewpoint.CreateCopy();
            if (p["camera"] != null)
            {
                var c = p["camera"]!;
                var text = c.Type == JTokenType.String ? c.Value<string>()! : c.ToString(Newtonsoft.Json.Formatting.None);
                if (!vp.TrySetCamera(text)) throw new Exception("Invalid camera JSON (use navis_get_camera for the shape).");
            }
            if (p["position"] != null) vp.Position = PointFrom(p["position"], "position");
            if (p["up"] != null)
            {
                var u = PointFrom(p["up"], "up");
                vp.WorldUpVector = new UnitVector3D(u.X, u.Y, u.Z);
            }
            if (p["target"] != null) vp.PointAt(PointFrom(p["target"], "target"));
            if (p["verticalFieldOfView"] != null) vp.HeightField = Num(p, "verticalFieldOfView");
            var pr = Str(p, "projection");
            if (pr != "" && Enum.TryParse(pr, true, out ViewpointProjection proj)) vp.Projection = proj;
            ApplyViewpoint(doc, vp);
            return new JObject { ["position"] = PointJson(vp.Position), ["projection"] = vp.Projection.ToString() };
        }

        private static JObject ZoomToItems(JObject p)
        {
            var doc = ActiveDoc();
            var items = ItemsOrSelection(doc, p);
            var box = ItemsBox(items);
            if (Bool(p, "select", true)) doc.CurrentSelection.CopyFrom(items);
            var vp = doc.CurrentViewpoint.CreateCopy();
            vp.ZoomBox(box);
            ApplyViewpoint(doc, vp);
            return new JObject { ["zoomedTo"] = items.Count, ["center"] = PointJson(box.Center) };
        }

        private static JObject ViewPreset(JObject p)
        {
            var doc = ActiveDoc();
            var preset = Str(p, "preset").ToLowerInvariant();
            if (preset == "home")
            {
                ApplyViewpoint(doc, doc.HomeView);
                return new JObject { ["preset"] = "home" };
            }
            if (preset == "iso")
            {
                doc.ActiveView.LookFromFrontRightTop();
                return new JObject { ["preset"] = "iso" };
            }
            Vector3D up = doc.UpVector, front = doc.FrontVector, right = doc.RightVector;
            Vector3D Neg(Vector3D v) => new Vector3D(-v.X, -v.Y, -v.Z);
            // dir = direction the camera looks; upDir = screen up
            (Vector3D dir, Vector3D upDir) = preset switch
            {
                "top" => (Neg(up), front),
                "bottom" => (up, Neg(front)),
                "front" => (Neg(front), up),
                "back" => (front, up),
                "left" => (right, up),
                "right" => (Neg(right), up),
                _ => throw new Exception("preset must be top, bottom, front, back, left, right, iso or home."),
            };
            var vp = doc.CurrentViewpoint.CreateCopy();
            vp.AlignDirection(dir);
            vp.AlignUp(upDir);
            var box = doc.GetBoundingBox(false);
            if (box != null && !box.IsEmpty) vp.ZoomBox(box);
            ApplyViewpoint(doc, vp);
            return new JObject { ["preset"] = preset };
        }

        // ── TimeLiner ──

        private static DocumentTimeliner Timeliner(Document doc)
        {
            try { return doc.GetTimeliner(); }
            catch (Exception ex) when (ex is FileNotFoundException || ex is TypeLoadException || ex is MissingMethodException)
            {
                throw new Exception("TimeLiner is not available in this Navisworks edition.", ex);
            }
        }

        private static (TimelinerTask task, GroupItem parent, int index) FindTask(DocumentTimeliner tl, string idOrName)
        {
            foreach (var (item, parent, index) in WalkSaved(tl.Tasks, tl.TasksRoot))
            {
                if (item is TimelinerTask t && (string.Equals(t.DisplayName, idOrName, StringComparison.OrdinalIgnoreCase)
                                               || string.Equals(t.DisplayId, idOrName, StringComparison.OrdinalIgnoreCase)))
                    return (t, parent ?? tl.TasksRoot, index);
            }
            throw new Exception($"TimeLiner task '{idOrName}' not found. Use navis_timeliner_list_tasks.");
        }

        private static DateTime? DateOrNull(JObject p, string name)
        {
            var s = Str(p, name);
            if (s == "") return null;
            if (!DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeLocal, out var d))
                throw new Exception($"{name} '{s}' is not a valid date (use ISO, e.g. 2026-03-01).");
            return d;
        }

        private static void ApplyTaskFields(Document doc, TimelinerTask t, JObject p)
        {
            var name = Str(p, "name");
            if (name != "") t.DisplayName = name;
            if (p["displayId"] != null) t.DisplayId = Str(p, "displayId");
            if (p["plannedStart"] != null) t.PlannedStartDate = DateOrNull(p, "plannedStart");
            if (p["plannedEnd"] != null) t.PlannedEndDate = DateOrNull(p, "plannedEnd");
            if (p["actualStart"] != null) t.ActualStartDate = DateOrNull(p, "actualStart");
            if (p["actualEnd"] != null) t.ActualEndDate = DateOrNull(p, "actualEnd");
            if (p["taskType"] != null) t.SimulationTaskTypeName = Str(p, "taskType");
            if (p["progressPercent"] != null) t.ProgressPercent = Num(p, "progressPercent");
            if (p["materialCost"] != null) t.MaterialCost = Num(p, "materialCost");
            if (p["laborCost"] != null) t.LaborCost = Num(p, "laborCost");

            var setName = Str(p, "selectionSet");
            var ids = Ids(p);
            if (setName != "")
            {
                var sources = new SelectionSourceCollection();
                sources.Add(doc.SelectionSets.CreateSelectionSource(FindSet(doc, setName)));
                t.Selection.CopyFrom(sources);
            }
            else if (ids.Count > 0)
            {
                var coll = new ModelItemCollection();
                coll.AddRange(ResolveItems(doc, ids));
                t.Selection.CopyFrom(coll);
            }
            else if (Bool(p, "attachCurrentSelection"))
            {
                t.Selection.CopyFrom(doc.CurrentSelection.SelectedItems);
            }
        }

        private static JObject TaskJson(TimelinerTask t, int depth) => new JObject
        {
            ["name"] = t.DisplayName ?? "",
            ["displayId"] = t.DisplayId ?? "",
            ["depth"] = depth,
            ["taskType"] = t.SimulationTaskTypeName ?? "",
            ["plannedStart"] = t.PlannedStartDate?.ToString("yyyy-MM-dd"),
            ["plannedEnd"] = t.PlannedEndDate?.ToString("yyyy-MM-dd"),
            ["actualStart"] = t.ActualStartDate?.ToString("yyyy-MM-dd"),
            ["actualEnd"] = t.ActualEndDate?.ToString("yyyy-MM-dd"),
            ["progressPercent"] = t.ProgressPercent,
            ["status"] = t.TaskStatus.ToString(),
            ["attached"] = t.Selection == null || t.Selection.IsClear ? "" : t.Selection.DisplayString,
            ["childCount"] = t.Children.Count,
        };

        private static JObject TimelinerList(JObject p)
        {
            var doc = ActiveDoc();
            var tl = Timeliner(doc);
            var limit = Int(p, "limit", 500);
            var arr = new JArray();
            int total = 0;
            void Recurse(SavedItemCollection items, int depth)
            {
                foreach (SavedItem item in items)
                {
                    if (item is not TimelinerTask t) continue;
                    total++;
                    if (arr.Count < limit) arr.Add(TaskJson(t, depth));
                    Recurse(t.Children, depth + 1);
                }
            }
            Recurse(tl.Tasks, 0);
            var types = new JArray(tl.SimulationTaskTypes.Cast<SavedItem>().Select(s => s.DisplayName));
            return new JObject { ["total"] = total, ["returned"] = arr.Count, ["taskTypes"] = types, ["tasks"] = arr };
        }

        private static JObject TimelinerAdd(JObject p)
        {
            var doc = ActiveDoc();
            var tl = Timeliner(doc);
            if (Str(p, "name") == "") throw new Exception("name is required.");
            var t = new TimelinerTask();
            if (p["taskType"] == null) t.SimulationTaskTypeName = "Construct";
            ApplyTaskFields(doc, t, p);
            var parentName = Str(p, "parent");
            if (parentName != "")
            {
                var (parent, _, _) = FindTask(tl, parentName);
                tl.TaskAddCopy(parent, t);
            }
            else tl.TaskAddCopy(t);
            return new JObject { ["added"] = t.DisplayName, ["parent"] = parentName };
        }

        private static JObject TimelinerUpdate(JObject p)
        {
            var doc = ActiveDoc();
            var tl = Timeliner(doc);
            var (task, parent, index) = FindTask(tl, Str(p, "task"));
            var copy = task.CreateCopy();
            ApplyTaskFields(doc, copy, p);
            tl.TaskEdit(parent, index, copy);
            return new JObject { ["updated"] = copy.DisplayName };
        }

        private static JObject TimelinerDelete(JObject p)
        {
            var doc = ActiveDoc();
            var tl = Timeliner(doc);
            var (task, parent, index) = FindTask(tl, Str(p, "task"));
            tl.TaskRemoveAt(parent, index);
            return new JObject { ["deleted"] = task.DisplayName };
        }

        // ── clash test authoring ──

        /// <summary>{ selectionSets: [...] } or { displayIds: [...] } or { useCurrentSelection: true }</summary>
        private static void FillClashSelection(Document doc, ClashSelection target, JToken? spec, string label)
        {
            if (spec is not JObject s) throw new Exception($"{label} is required: {{ selectionSets: [...] }} or {{ displayIds: [...] }}.");
            var sets = Ids(s, "selectionSets");
            var ids = Ids(s);
            if (sets.Count > 0)
            {
                var sources = new SelectionSourceCollection();
                foreach (var name in sets) sources.Add(doc.SelectionSets.CreateSelectionSource(FindSet(doc, name)));
                target.Selection.CopyFrom(sources);
            }
            else if (ids.Count > 0)
            {
                var coll = new ModelItemCollection();
                coll.AddRange(ResolveItems(doc, ids));
                target.Selection.CopyFrom(coll);
            }
            else if (Bool(s, "useCurrentSelection"))
            {
                target.Selection.CopyFrom(doc.CurrentSelection.SelectedItems);
            }
            else throw new Exception($"{label} needs selectionSets, displayIds or useCurrentSelection.");
            if (s["selfIntersect"] != null) target.SelfIntersect = Bool(s, "selfIntersect");
        }

        private static void ApplyTestFields(Document doc, ClashTest t, JObject p, bool requireSelections)
        {
            var type = Str(p, "testType");
            if (type != "")
            {
                if (!Enum.TryParse(type, true, out ClashTestType tt))
                    throw new Exception("testType must be Hard, HardConservative, Clearance or Duplicates.");
                t.TestType = tt;
            }
            if (p["tolerance"] != null) t.Tolerance = Num(p, "tolerance");
            if (p["mergeComposites"] != null) t.MergeComposites = Bool(p, "mergeComposites");
            if (requireSelections || p["selectionA"] != null) FillClashSelection(doc, t.SelectionA, p["selectionA"], "selectionA");
            if (requireSelections || p["selectionB"] != null) FillClashSelection(doc, t.SelectionB, p["selectionB"], "selectionB");
        }

        private static JObject ClashCreate(JObject p)
        {
            var doc = ActiveDoc();
            var data = ClashData(doc);
            var name = Str(p, "name");
            if (name == "") throw new Exception("name is required.");
            var t = new ClashTest { DisplayName = name, TestType = ClashTestType.Hard };
            ApplyTestFields(doc, t, p, requireSelections: true);
            data.TestsAddCopy(t);
            var created = FindTest(data, name);
            if (Bool(p, "run")) data.TestsRunTest(created);
            var results = new List<ClashResult>();
            CollectResults(created.Children, results);
            return new JObject
            {
                ["created"] = created.DisplayName,
                ["guid"] = created.Guid.ToString(),
                ["testType"] = created.TestType.ToString(),
                ["tolerance"] = created.Tolerance,
                ["toleranceUnits"] = doc.Units.ToString(),
                ["ran"] = Bool(p, "run"),
                ["resultCount"] = results.Count,
            };
        }

        private static JObject ClashUpdate(JObject p)
        {
            var doc = ActiveDoc();
            var data = ClashData(doc);
            var test = FindTest(data, Str(p, "test"));
            var copy = (ClashTest)test.CreateCopy();
            ApplyTestFields(doc, copy, p, requireSelections: false);
            data.TestsEditTestFromCopy(test, copy);
            var newName = Str(p, "newName");
            if (newName != "") data.TestsEditDisplayName(test, newName);
            return new JObject { ["updated"] = newName != "" ? newName : test.DisplayName };
        }

        private static JObject ClashDelete(JObject p)
        {
            var doc = ActiveDoc();
            var data = ClashData(doc);
            var test = FindTest(data, Str(p, "test"));
            var name = test.DisplayName;
            data.TestsRemove(test);
            return new JObject { ["deleted"] = name };
        }

        private static JObject ClashEditResults(JObject p)
        {
            var doc = ActiveDoc();
            var data = ClashData(doc);
            var test = FindTest(data, Str(p, "test"));
            var ids = new HashSet<string>(Ids(p, "clashIds"), StringComparer.OrdinalIgnoreCase);
            if (ids.Count == 0) throw new Exception("clashIds is required (non-empty).");
            var all = new List<ClashResult>();
            CollectResults(test.Children, all);
            int updated = 0;
            foreach (var r in all.Where(r => ids.Contains(r.Guid.ToString())))
            {
                if (p["assignedTo"] != null) data.TestsEditResultAssignedTo(r, Str(p, "assignedTo"));
                if (p["description"] != null) data.TestsEditResultDescription(r, Str(p, "description"));
                if (p["approvedBy"] != null) data.TestsEditResultApprovedBy(r, Str(p, "approvedBy"));
                var body = Str(p, "comment");
                if (body != "")
                {
                    var comments = new CommentCollection(r.Comments ?? new CommentCollection());
                    comments.Add(new Comment(body, CommentStatus.New, Str(p, "author", Environment.UserName)));
                    data.TestsEditResultComments(r, comments);
                }
                updated++;
            }
            return new JObject { ["test"] = test.DisplayName ?? "", ["updated"] = updated, ["requested"] = ids.Count };
        }

        // ── measure / redlines / comments ──

        private static JObject Measure(JObject p)
        {
            var doc = ActiveDoc();
            var a = p["displayIdA"]?.Value<string>();
            var b = p["displayIdB"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b) && p["pointA"] == null)
            {
                // read whatever the user measured with the Measure tool
                var m = doc.CurrentMeasurement;
                if (!m.HasMeasurement) throw new Exception("No current measurement. Pass displayIdA/displayIdB or pointA/pointB.");
                return new JObject
                {
                    ["source"] = "measureTool",
                    ["type"] = m.MeasurementType.ToString(),
                    ["value"] = m.MeasurementValue,
                    ["firstPoint"] = m.HasFirstPoint ? PointJson(m.FirstPoint) : null,
                    ["endPoint"] = m.HasEndPoint ? PointJson(m.EndPoint) : null,
                    ["units"] = doc.Units.ToString(),
                };
            }
            if (p["pointA"] != null && p["pointB"] != null)
            {
                var pa = PointFrom(p["pointA"], "pointA");
                var pb = PointFrom(p["pointB"], "pointB");
                return DistanceJson(doc, pa, pb, null);
            }
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                throw new Exception("Pass both displayIdA and displayIdB (or pointA and pointB).");
            var items = ResolveItems(doc, new[] { a!, b! });
            var boxA = ItemsBox(new[] { items[0] });
            var boxB = ItemsBox(new[] { items[1] });
            // gap between boxes on each axis (0 when they overlap on that axis)
            double Gap(double minA, double maxA, double minB, double maxB) => Math.Max(0, Math.Max(minB - maxA, minA - maxB));
            var gx = Gap(boxA.Min.X, boxA.Max.X, boxB.Min.X, boxB.Max.X);
            var gy = Gap(boxA.Min.Y, boxA.Max.Y, boxB.Min.Y, boxB.Max.Y);
            var gz = Gap(boxA.Min.Z, boxA.Max.Z, boxB.Min.Z, boxB.Max.Z);
            var result = DistanceJson(doc, boxA.Center, boxB.Center, Math.Sqrt(gx * gx + gy * gy + gz * gz));
            result["itemA"] = items[0].DisplayName;
            result["itemB"] = items[1].DisplayName;
            return result;
        }

        private static JObject DistanceJson(Document doc, Point3D a, Point3D b, double? clearance)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
            var o = new JObject
            {
                ["pointA"] = PointJson(a),
                ["pointB"] = PointJson(b),
                ["distance"] = Math.Sqrt(dx * dx + dy * dy + dz * dz),
                ["dx"] = dx, ["dy"] = dy, ["dz"] = dz,
                ["units"] = doc.Units.ToString(),
            };
            if (clearance.HasValue)
            {
                o["distanceMeaning"] = "center-to-center of bounding boxes";
                o["boundingBoxClearance"] = clearance.Value;
            }
            return o;
        }

        private static JObject GetRedlines()
        {
            var doc = ActiveDoc();
            var view = doc.ActiveView ?? throw new Exception("No active view.");
            return new JObject { ["redlines"] = JToken.Parse(view.GetRedlines()) };
        }

        private static JObject SetRedlines(JObject p)
        {
            var doc = ActiveDoc();
            var view = doc.ActiveView ?? throw new Exception("No active view.");
            var json = p["json"] ?? throw new Exception("json is required (use navis_get_redlines to see the format).");
            var text = json.Type == JTokenType.String ? json.Value<string>()! : json.ToString(Newtonsoft.Json.Formatting.None);
            if (!view.TrySetRedlines(text))
                throw new Exception("Navisworks rejected the redline JSON. Call navis_get_redlines to see the expected shape.");
            var saveAs = Str(p, "saveViewpointAs");
            if (saveAs != "")
            {
                var svp = new SavedViewpoint(view.CreateViewpointCopy()) { DisplayName = saveAs };
                doc.SavedViewpoints.AddCopy(svp);
            }
            return new JObject { ["set"] = true, ["savedViewpoint"] = saveAs };
        }

        private static JObject AddComment(JObject p)
        {
            var doc = ActiveDoc();
            var body = Str(p, "body");
            if (body == "") throw new Exception("body is required.");
            var statusStr = Str(p, "status", "New");
            if (!Enum.TryParse(statusStr, true, out CommentStatus status))
                throw new Exception("status must be New, Active, Approved or Resolved.");
            var comment = new Comment(body, status, Str(p, "author", Environment.UserName));
            var target = Str(p, "target").ToLowerInvariant();
            var id = Str(p, "id");
            switch (target)
            {
                case "viewpoint":
                    doc.SavedViewpoints.AddComment(FindViewpoint(doc, id), comment);
                    break;
                case "selectionset":
                    doc.SelectionSets.AddComment(FindSet(doc, id), comment);
                    break;
                case "clashresult":
                {
                    var data = ClashData(doc);
                    var test = FindTest(data, Str(p, "test"));
                    var all = new List<ClashResult>();
                    CollectResults(test.Children, all);
                    var r = all.FirstOrDefault(x => string.Equals(x.Guid.ToString(), id, StringComparison.OrdinalIgnoreCase)
                                                    || string.Equals(x.DisplayName, id, StringComparison.OrdinalIgnoreCase))
                            ?? throw new Exception($"Clash result '{id}' not found in test '{test.DisplayName}'.");
                    var comments = new CommentCollection(r.Comments ?? new CommentCollection());
                    comments.Add(comment);
                    data.TestsEditResultComments(r, comments);
                    break;
                }
                default: throw new Exception("target must be viewpoint, selectionSet or clashResult.");
            }
            return new JObject { ["added"] = true, ["target"] = target, ["id"] = id };
        }

        private static JObject ListComments(JObject p)
        {
            var doc = ActiveDoc();
            var target = Str(p, "target").ToLowerInvariant();
            var id = Str(p, "id");
            CommentCollection? comments = target switch
            {
                "viewpoint" => FindViewpoint(doc, id).Comments,
                "selectionset" => FindSet(doc, id).Comments,
                "clashresult" => ClashResultFor(doc, Str(p, "test"), id).Comments,
                _ => throw new Exception("target must be viewpoint, selectionSet or clashResult."),
            };
            var arr = CommentsJson(comments);
            return new JObject { ["target"] = target, ["id"] = id, ["count"] = arr.Count, ["comments"] = arr };
        }

        private static ClashResult ClashResultFor(Document doc, string testName, string id)
        {
            var test = FindTest(ClashData(doc), testName);
            var all = new List<ClashResult>();
            CollectResults(test.Children, all);
            return all.FirstOrDefault(x => string.Equals(x.Guid.ToString(), id, StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(x.DisplayName, id, StringComparison.OrdinalIgnoreCase))
                   ?? throw new Exception($"Clash result '{id}' not found in test '{test.DisplayName}'.");
        }

        // ── selection / search sets ──

        private static JObject CreateSelectionSet(JObject p)
        {
            var doc = ActiveDoc();
            var name = Str(p, "name");
            if (name == "") throw new Exception("name is required.");
            var items = ItemsOrSelection(doc, p);
            var coll = new ModelItemCollection();
            coll.AddRange(items);
            var set = new SelectionSet(coll) { DisplayName = name };
            doc.SelectionSets.AddCopy(set);
            return new JObject { ["created"] = name, ["itemCount"] = items.Count };
        }

        /// <summary>
        /// conditions: [{ category, property, operator: contains|equals|notEquals|wildcard|exists, value }]
        /// All conditions are ANDed (Find Items default).
        /// </summary>
        private static JObject CreateSearchSet(JObject p)
        {
            var doc = ActiveDoc();
            var name = Str(p, "name");
            if (name == "") throw new Exception("name is required.");
            var conds = p["conditions"] as JArray;
            if (conds == null || conds.Count == 0) throw new Exception("conditions is required (non-empty).");
            var search = new Search();
            search.Selection.SelectAll();
            search.Locations = SearchLocations.DescendantsAndSelf;
            foreach (var ct in conds)
            {
                if (ct is not JObject c) continue;
                var cat = Str(c, "category", "Item");
                var prop = Str(c, "property", "Name");
                var op = Str(c, "operator", "contains").ToLowerInvariant();
                var val = Str(c, "value");
                var baseCond = SearchCondition.HasPropertyByDisplayName(cat, prop);
                SearchCondition cond = op switch
                {
                    "exists" => baseCond,
                    "contains" => baseCond.DisplayStringContains(val),
                    "wildcard" => baseCond.DisplayStringWildcard(val),
                    "equals" => baseCond.EqualValue(VariantData.FromDisplayString(val)),
                    "notequals" => baseCond.EqualValue(VariantData.FromDisplayString(val)).Negate(),
                    _ => throw new Exception($"Unknown operator '{op}'. Use contains, equals, notEquals, wildcard or exists."),
                };
                if (Bool(c, "ignoreCase", true) && op != "exists") cond = cond.IgnoreStringValueCase();
                search.SearchConditions.Add(cond);
            }
            int count = search.FindAll(doc, false).Count;
            var set = new SelectionSet(search) { DisplayName = name };
            doc.SelectionSets.AddCopy(set);
            return new JObject { ["created"] = name, ["matchCount"] = count, ["kind"] = "search set (dynamic)" };
        }

        private static JObject ManageSelectionSet(JObject p)
        {
            var doc = ActiveDoc();
            var set = FindSet(doc, Str(p, "set"));
            var action = Str(p, "action").ToLowerInvariant();
            var name = set.DisplayName;
            switch (action)
            {
                case "delete":
                    doc.SelectionSets.Remove(set);
                    return new JObject { ["deleted"] = name };
                case "rename":
                {
                    var newName = Str(p, "newName");
                    if (newName == "") throw new Exception("newName is required for rename.");
                    doc.SelectionSets.EditDisplayName(set, newName);
                    return new JObject { ["renamed"] = name, ["to"] = newName };
                }
                case "select":
                {
                    var items = set.GetSelectedItems(doc);
                    doc.CurrentSelection.CopyFrom(items);
                    return new JObject { ["selected"] = items.Count, ["set"] = name };
                }
                case "items":
                {
                    var items = set.GetSelectedItems(doc);
                    var limit = Int(p, "limit", 100);
                    var arr = new JArray(items.Cast<ModelItem>().Take(limit).Select(ItemJson));
                    return new JObject { ["set"] = name, ["count"] = items.Count, ["returned"] = arr.Count, ["items"] = arr };
                }
                default: throw new Exception("action must be delete, rename, select or items.");
            }
        }

        // ── properties ──

        private static JObject GetItemProperties(JObject p)
        {
            var doc = ActiveDoc();
            var items = ItemsOrSelection(doc, p);
            var catFilter = Str(p, "category");
            var arr = new JArray();
            foreach (var item in items.Take(Int(p, "limit", 20)))
            {
                var cats = new JObject();
                foreach (PropertyCategory cat in item.PropertyCategories)
                {
                    if (catFilter != "" && !string.Equals(cat.DisplayName, catFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    var props = new JObject();
                    foreach (DataProperty dp in cat.Properties)
                    {
                        string v;
                        try { v = dp.Value?.ToDisplayString() ?? ""; } catch { v = ""; }
                        props[dp.DisplayName ?? dp.Name ?? "?"] = v;
                    }
                    cats[cat.DisplayName ?? cat.Name ?? "?"] = props;
                }
                var o = ItemJson(item);
                o["properties"] = cats;
                arr.Add(o);
            }
            return new JObject { ["count"] = arr.Count, ["items"] = arr };
        }

        /// <summary>
        /// Writes a user-defined property tab (the "Add/Edit User Data" feature) through the COM API.
        /// Existing properties on the tab are kept unless overwritten; pass remove:true to drop the tab.
        /// </summary>
        private static JObject SetUserProperties(JObject p)
        {
            var doc = ActiveDoc();
            var tab = Str(p, "tabName", "BIM-Bot");
            var remove = Bool(p, "remove");
            var props = p["properties"] as JObject;
            if (!remove && (props == null || props.Count == 0)) throw new Exception("properties is required: { \"Key\": \"Value\" }.");
            var items = ItemsOrSelection(doc, p);
            var state = ComBridge.State;
            int done = 0;
            foreach (var item in items)
            {
                var path = ComBridge.ToInwOaPath(item);
                var node = (NwCom.InwGUIPropertyNode2)state.GetGUIPropertyNode(path, true);

                // locate an existing user tab with this name (user tabs are 1-based in order)
                int userIndex = 0, found = 0;
                var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (NwCom.InwGUIAttribute2 attr in node.GUIAttributes())
                {
                    if (!attr.UserDefined) continue;
                    userIndex++;
                    if (!string.Equals(attr.ClassUserName, tab, StringComparison.OrdinalIgnoreCase)) continue;
                    found = userIndex;
                    foreach (NwCom.InwOaProperty op in attr.Properties())
                        existing[op.UserName ?? op.name] = op.value;
                    break;
                }

                if (remove)
                {
                    if (found > 0) { node.RemoveUserDefined(found); done++; }
                    continue;
                }

                foreach (var kv in props!) existing[kv.Key] = kv.Value?.Type == JTokenType.Null ? null : kv.Value?.ToString();
                var vec = (NwCom.InwOaPropertyVec)state.ObjectFactory(NwCom.nwEObjectType.eObjectType_nwOaPropertyVec, null, null);
                foreach (var kv in existing)
                {
                    if (kv.Value == null) continue; // null value deletes the key
                    var prop = (NwCom.InwOaProperty)state.ObjectFactory(NwCom.nwEObjectType.eObjectType_nwOaProperty, null, null);
                    prop.name = kv.Key;
                    prop.UserName = kv.Key;
                    prop.value = kv.Value;
                    vec.Properties().Add(prop);
                }
                node.SetUserDefined(found, tab, tab.Replace(" ", "_"), vec);
                done++;
            }
            return new JObject { [remove ? "removedFrom" : "updated"] = done, ["tab"] = tab };
        }

        // ── runtime C# ──

        private static readonly string[] _blacklistedPatterns =
        {
            "Process.Start", "File.Delete", "Directory.Delete",
            "Environment.Exit", "Registry.", "WebClient",
            "HttpClient", "System.Net.", "System.Diagnostics.Process",
        };

        /// <summary>
        /// Compiles a C# method body with CodeDom (built into .NET Framework, C# 5 syntax)
        /// and runs it on the Navisworks main thread. Variables available: doc (Document).
        /// Return a value to get it back as text. Same pattern blacklist as send_code_to_revit.
        /// </summary>
        private static JObject ExecuteCode(JObject p)
        {
            var code = Str(p, "code");
            if (string.IsNullOrWhiteSpace(code)) throw new Exception("code is required.");
            var blocked = _blacklistedPatterns.FirstOrDefault(b => code.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0);
            if (blocked != null)
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = $"Security violation: execution blocked. Code contains disallowed pattern '{blocked}'.",
                };
            var doc = ActiveDoc();
            bool hasReturn = code.IndexOf("return", StringComparison.Ordinal) >= 0;
            var source = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Autodesk.Navisworks.Api.Timeliner;
public static class DynamicNavisCode
{
    public static object Run(Document doc)
    {
#line 1
" + code + (hasReturn ? "" : "\nreturn null;") + @"
    }
}";
            var opts = new CompilerParameters { GenerateInMemory = true, GenerateExecutable = false };
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location)) continue;
                    var n = asm.GetName().Name;
                    if (n.StartsWith("Autodesk.Navisworks", StringComparison.Ordinal) || n == "System" || n == "System.Core"
                        || n == "mscorlib" || n == "System.Xml" || n == "System.Xml.Linq" || n == "System.Drawing" || n == "Newtonsoft.Json")
                        if (!opts.ReferencedAssemblies.Contains(asm.Location)) opts.ReferencedAssemblies.Add(asm.Location);
                }
                catch { }
            }
            CompilerResults compiled;
            using (var provider = new Microsoft.CSharp.CSharpCodeProvider())
                compiled = provider.CompileAssemblyFromSource(opts, source);
            if (compiled.Errors.HasErrors)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = "Compilation failed",
                    ["errors"] = new JArray(compiled.Errors.Cast<CompilerError>().Where(e => !e.IsWarning)
                        .Select(e => $"Line {e.Line}: {e.ErrorText}")),
                    ["hint"] = "C# 5 syntax only (no $\"\" interpolation, no ?. operator). 'doc' is the active Navisworks Document.",
                };
            }
            try
            {
                var run = compiled.CompiledAssembly.GetType("DynamicNavisCode")!.GetMethod("Run")!;
                var result = run.Invoke(null, new object[] { doc });
                NavisLogger.Log($"navis_execute_code: {Str(p, "description", "AI-generated code")}");
                return new JObject
                {
                    ["success"] = true,
                    ["description"] = Str(p, "description"),
                    ["result"] = result == null ? "Code executed successfully (no return value)"
                        : result is JToken jt ? jt : JToken.FromObject(result is string || result.GetType().IsPrimitive ? result : result.ToString()),
                };
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                return new JObject { ["success"] = false, ["error"] = $"Runtime error: {inner.Message}" };
            }
        }
    }
}
