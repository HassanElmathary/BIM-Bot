using System.Linq;
using DB = Autodesk.Revit.DB;

namespace BIMBotPlugin.Core.Compat
{
    /// <summary>
    /// Bridges the ElementId break between Revit API versions.
    ///
    ///   Revit 2020-2023 : ElementId.IntegerValue (int),  ElementId(int)
    ///   Revit 2024-2025 : both
    ///   Revit 2026+     : ElementId.Value (long),        ElementId(long)
    ///
    /// No member spans the whole range, so every read and construction goes
    /// through here. Always work in <c>long</c> — it is lossless in both
    /// directions for real element ids.
    /// </summary>
    public static class RevitCompat
    {
        /// <summary>The numeric value of an element id, on any supported Revit version.</summary>
        public static long Val(this DB.ElementId id)
        {
            if (id == null) return DB.ElementId.InvalidElementId.RawValue();
            return id.RawValue();
        }

        /// <summary>Builds an ElementId from a numeric value, on any supported Revit version.</summary>
        public static DB.ElementId ToElementId(this long value)
        {
#if REVIT_PRE_2024
            return new DB.ElementId((int)value);
#else
            return new DB.ElementId(value);
#endif
        }

        /// <summary>Convenience overload — most call sites still deal in int.</summary>
        public static DB.ElementId ToElementId(this int value)
        {
#if REVIT_PRE_2024
            return new DB.ElementId(value);
#else
            return new DB.ElementId((long)value);
#endif
        }

        /// <summary>Category ids are built the same way, and change type across versions too.</summary>
        public static DB.ElementId ToElementId(this DB.BuiltInCategory category)
        {
#if REVIT_PRE_2024
            return new DB.ElementId((int)category);
#else
            return new DB.ElementId((long)category);
#endif
        }

        /// <summary>
        /// Parses a string id as produced by <see cref="Val"/>. Returns
        /// InvalidElementId when the text is not a number.
        /// </summary>
        public static DB.ElementId ParseElementId(string text)
        {
            return long.TryParse(text, out var v) ? v.ToElementId() : DB.ElementId.InvalidElementId;
        }

        /// <summary>
        /// Category.BuiltInCategory is Revit 2023+. Earlier versions have to go
        /// through the category id, which carries the same enum value.
        /// </summary>
        public static DB.BuiltInCategory BuiltInCat(this DB.Category category)
        {
            if (category == null) return DB.BuiltInCategory.INVALID;
#if REVIT2020 || REVIT2021 || REVIT2022
            return (DB.BuiltInCategory)category.Id.IntegerValue;
#else
            return category.BuiltInCategory;
#endif
        }

        /// <summary>
        /// Selection.SetReferences is Revit 2023+ and is the only way to select
        /// elements inside links. Earlier versions can only select host elements,
        /// so linked references are dropped rather than silently mis-selected.
        /// </summary>
        public static void SetReferencesCompat(
            this Autodesk.Revit.UI.Selection.Selection selection,
            System.Collections.Generic.IList<DB.Reference> references)
        {
#if REVIT2020 || REVIT2021 || REVIT2022
            // Reference.ElementId is the host-document element — for a linked
            // reference that is the RevitLinkInstance, which is the closest
            // selectable stand-in these versions offer.
            var ids = new System.Collections.Generic.HashSet<DB.ElementId>(
                references
                    .Where(r => r != null && r.ElementId != null
                                && r.ElementId != DB.ElementId.InvalidElementId)
                    .Select(r => r.ElementId));
            selection.SetElementIds(ids);
#else
            selection.SetReferences(references);
#endif
        }

        private static long RawValue(this DB.ElementId id)
        {
#if REVIT_PRE_2024
            return id.IntegerValue;
#else
            return id.Value;
#endif
        }
    }
}
