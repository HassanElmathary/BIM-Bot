using System;
using System.Drawing;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Every Navisworks API difference between 2020 and 2027 lives here, so the rest of the plugin
    /// is the same source for every year. Each year is built separately (-p:NavisYear=&lt;year&gt;);
    /// the csproj defines these symbols from NavisYear (verified against each year's API DLLs):
    ///   NAVIS_NO_IMAGE          2020        View.GenerateImage does not exist
    ///   NAVIS_IMAGE_SECTIONING  2024+       GenerateImage(style, w, h, enableSectioning)
    ///   NAVIS_ASSIGNEE          2026+       clash AssignedTo / ApprovedBy / status edits take an Assignee
    ///   NAVIS_COMMENT_ASSIGNEE  2027+       Comment.Author is an Assignee
    /// DocumentClashTests.Tests, TestsAddCopy(test) and TestsRemove(test) are gone in 2027; their
    /// replacements (Value.TestsRoot, the parent overloads) exist in every year, so those need no symbol.
    /// </summary>
    public static partial class NavisCommandExecutor
    {
        // ── clash tests ──

        /// <summary>Top-level clash tests and folders (DocumentClashTests.Tests was removed in 2027).</summary>
        private static SavedItemCollection ClashTestsOf(DocumentClashTests data) => data.Value.TestsRoot.Children;

        private static void AddClashTest(DocumentClashTests data, ClashTest test) =>
            data.TestsAddCopy(data.Value.TestsRoot, test);

        private static void RemoveClashTest(DocumentClashTests data, ClashTest test) =>
            data.TestsRemove(test.Parent ?? data.Value.TestsRoot, test);

        // ── clash result people ──

        private static void SetResultAssignedTo(DocumentClashTests data, IClashResult result, string who)
        {
#if NAVIS_ASSIGNEE
            data.TestsEditResultAssignedTo(result, new Assignee(who));
#else
            data.TestsEditResultAssignedTo(result, who);
#endif
        }

        private static void SetResultApprovedBy(DocumentClashTests data, IClashResult result, string who)
        {
#if NAVIS_ASSIGNEE
            data.TestsEditResultApprovedBy(result, new Assignee(who));
#else
            data.TestsEditResultApprovedBy(result, who);
#endif
        }

        private static void SetResultStatus(DocumentClashTests data, IClashResult result, ClashResultStatus status)
        {
#if NAVIS_ASSIGNEE
            // 2026+ records who changed the status.
            data.TestsEditResultStatus(result, status, new Assignee(Environment.UserName));
#else
            data.TestsEditResultStatus(result, status);
#endif
        }

        // ── comments ──

        private static string CommentAuthor(Comment c)
        {
#if NAVIS_COMMENT_ASSIGNEE
            return c.Author?.DisplayName ?? "";
#else
            return c.Author ?? "";
#endif
        }

        private static Comment NewComment(string body, CommentStatus status, string author)
        {
#if NAVIS_COMMENT_ASSIGNEE
            return new Comment(body, status, new Assignee(author));
#else
            return new Comment(body, status, author);
#endif
        }

        // ── view images ──

        /// <summary>Renders the active view (sections honoured on 2024+ only — older APIs have no switch).</summary>
        private static Bitmap? RenderView(View view, int width, int height)
        {
#if NAVIS_NO_IMAGE
            throw new Exception("Capturing the view needs Navisworks Manage 2021 or newer: the 2020 API has no image generation.");
#elif NAVIS_IMAGE_SECTIONING
            return view.GenerateImage(ImageGenerationStyle.Scene, width, height, true);
#else
            return view.GenerateImage(ImageGenerationStyle.Scene, width, height);
#endif
        }
    }
}
