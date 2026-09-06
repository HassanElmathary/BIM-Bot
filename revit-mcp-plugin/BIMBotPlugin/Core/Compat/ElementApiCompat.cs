using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;

namespace BIMBotPlugin.Core.Compat
{
    /// <summary>
    /// Element-creation and parameter APIs that Autodesk introduced in Revit 2022.
    /// Where an equivalent existed before (floors), this routes to it. Where the
    /// API simply did not exist (ceilings, floor sketches), the call throws
    /// <see cref="NotSupportedOnThisRevitException"/> so the tool reports a clear
    /// reason instead of crashing with MissingMethodException.
    /// </summary>
    public static class ElementApiCompat
    {
        /// <summary>The Revit release this build targets, for user-facing messages.</summary>
        public const string BandName =
#if REVIT2020
            "2020";
#elif REVIT2021
            "2021";
#elif REVIT2022
            "2022";
#elif REVIT2023
            "2023";
#elif REVIT2024
            "2024";
#elif REVIT2025
            "2025";
#elif REVIT2026
            "2026";
#else
            "2027";
#endif

        // ── Parameter data type ───────────────────────────────────────────
        // Definition.GetDataType() is 2022+; before that the spec is exposed as
        // Definition.ParameterType.
        public static BbSpecId DataTypeOf(this DB.Definition definition)
        {
#if REVIT_PRE_2022
            return definition.ParameterType;
#else
            return definition.GetDataType();
#endif
        }

        /// <summary>
        /// The document's display unit for a parameter's own spec. Returns null
        /// when the spec has no unit (text, yes/no) or cannot be resolved.
        /// </summary>
        public static BbUnitId? GetDisplayUnitFor(DB.Document doc, DB.Parameter p)
        {
            try
            {
#if REVIT_PRE_2021
                return doc.GetUnits().GetFormatOptions(p.Definition.UnitType).DisplayUnits;
#elif REVIT_PRE_2022
                // 2021 has ForgeTypeId units but no Definition.GetDataType yet.
                return doc.GetUnits().GetFormatOptions(p.Definition.GetSpecTypeId()).GetUnitTypeId();
#else
                return doc.GetUnits().GetFormatOptions(p.Definition.GetDataType()).GetUnitTypeId();
#endif
            }
            catch { return null; }
        }

        /// <summary>
        /// A parameter's own display unit. Parameter.GetUnitTypeId() is 2021+;
        /// Revit 2020 exposes Parameter.DisplayUnitType.
        /// </summary>
        public static BbUnitId UnitOf(this DB.Parameter parameter)
        {
#if REVIT_PRE_2021
            return parameter.DisplayUnitType;
#else
            return parameter.GetUnitTypeId();
#endif
        }

        // ── Parameter group ───────────────────────────────────────────────
        // GroupTypeId is 2022+; earlier releases use the BuiltInParameterGroup enum.
        public static void InsertBinding(
            DB.Document doc, DB.Definition definition, DB.Binding binding)
        {
#if REVIT_PRE_2022
            doc.ParameterBindings.Insert(definition, binding, DB.BuiltInParameterGroup.PG_DATA);
#else
            doc.ParameterBindings.Insert(definition, binding, DB.GroupTypeId.Data);
#endif
        }

        // ── Floors ────────────────────────────────────────────────────────
        // Floor.Create is 2022+. Revit 2020/2021 have Document.Create.NewFloor,
        // which takes a CurveArray profile rather than CurveLoops.
        public static DB.Floor CreateFloor(
            DB.Document doc, IList<DB.CurveLoop> profile, DB.ElementId floorTypeId, DB.ElementId levelId)
        {
#if REVIT_PRE_2022
            var array = new DB.CurveArray();
            foreach (var curve in profile.SelectMany(loop => loop))
                array.Append(curve);

            var floorType = doc.GetElement(floorTypeId) as DB.FloorType;
            var level = doc.GetElement(levelId) as DB.Level;
            if (floorType == null || level == null)
                throw new NotSupportedOnThisRevitException(
                    "Creating a floor needs a valid floor type and level.");

            return doc.Create.NewFloor(array, floorType, level, false);
#else
            return DB.Floor.Create(doc, profile, floorTypeId, levelId);
#endif
        }

        /// <summary>
        /// The sketch behind a floor. Floor.SketchId is 2022+; there is no
        /// equivalent before that, so sketch-editing tools cannot run.
        /// </summary>
        public static DB.Sketch GetFloorSketch(DB.Document doc, DB.Floor floor)
        {
#if REVIT_PRE_2022
            throw new NotSupportedOnThisRevitException(
                "Reading a floor's sketch requires Revit 2022 or newer (Floor.SketchId).");
#else
            return doc.GetElement(floor.SketchId) as DB.Sketch;
#endif
        }

        // ── Ceilings ──────────────────────────────────────────────────────
        // Ceiling.Create is 2022+. Revit 2020/2021 expose no ceiling-creation
        // API at all — this cannot be worked around.
        public static DB.Ceiling CreateCeiling(
            DB.Document doc, IList<DB.CurveLoop> profile, DB.ElementId ceilingTypeId, DB.ElementId levelId)
        {
#if REVIT_PRE_2022
            throw new NotSupportedOnThisRevitException(
                "Creating ceilings requires Revit 2022 or newer — the Revit "
                + BandName + " API has no ceiling-creation method.");
#else
            return DB.Ceiling.Create(doc, profile, ceilingTypeId, levelId);
#endif
        }

        // ── PDF export ────────────────────────────────────────────────────
        /// <summary>
        /// PDFExportOptions is public from Revit 2022 (it exists but is internal
        /// in 2021, and is absent in 2020). Older versions must print to a PDF
        /// printer via PrintManager instead.
        /// </summary>
        public static bool HasNativePdfExport =>
#if REVIT_PRE_2022
            false;
#else
            true;
#endif
    }

    /// <summary>
    /// Raised when a tool depends on a Revit API that does not exist in the
    /// version this build targets. Callers turn it into a plain message.
    /// </summary>
    public class NotSupportedOnThisRevitException : Exception
    {
        public NotSupportedOnThisRevitException(string message) : base(message) { }
    }
}
