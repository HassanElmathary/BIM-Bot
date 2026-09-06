using DB = Autodesk.Revit.DB;

namespace BIMBotPlugin.Core.Compat
{
    /// <summary>
    /// The unit identifiers the plugin uses, resolved per Revit band.
    /// On 2021+ these are <c>UnitTypeId</c> members; on 2020 the equivalent
    /// <c>DisplayUnitType</c> enum values.
    /// </summary>
    public static class BbUnits
    {
#if REVIT_PRE_2021
        public static BbUnitId Millimeters => DB.DisplayUnitType.DUT_MILLIMETERS;
        public static BbUnitId Centimeters => DB.DisplayUnitType.DUT_CENTIMETERS;
        public static BbUnitId Meters => DB.DisplayUnitType.DUT_METERS;
        public static BbUnitId Feet => DB.DisplayUnitType.DUT_DECIMAL_FEET;
        public static BbUnitId Inches => DB.DisplayUnitType.DUT_DECIMAL_INCHES;
        public static BbUnitId SquareMeters => DB.DisplayUnitType.DUT_SQUARE_METERS;
        public static BbUnitId SquareFeet => DB.DisplayUnitType.DUT_SQUARE_FEET;
        public static BbUnitId CubicMeters => DB.DisplayUnitType.DUT_CUBIC_METERS;
        public static BbUnitId CubicFeet => DB.DisplayUnitType.DUT_CUBIC_FEET;
        public static BbUnitId Degrees => DB.DisplayUnitType.DUT_DECIMAL_DEGREES;
        public static BbUnitId Radians => DB.DisplayUnitType.DUT_RADIANS;
#else
        public static BbUnitId Millimeters => DB.UnitTypeId.Millimeters;
        public static BbUnitId Centimeters => DB.UnitTypeId.Centimeters;
        public static BbUnitId Meters => DB.UnitTypeId.Meters;
        public static BbUnitId Feet => DB.UnitTypeId.Feet;
        public static BbUnitId Inches => DB.UnitTypeId.Inches;
        public static BbUnitId SquareMeters => DB.UnitTypeId.SquareMeters;
        public static BbUnitId SquareFeet => DB.UnitTypeId.SquareFeet;
        public static BbUnitId CubicMeters => DB.UnitTypeId.CubicMeters;
        public static BbUnitId CubicFeet => DB.UnitTypeId.CubicFeet;
        public static BbUnitId Degrees => DB.UnitTypeId.Degrees;
        public static BbUnitId Radians => DB.UnitTypeId.Radians;
#endif

        /// <summary>
        /// The document's configured length unit. Revit 2020 exposes it as
        /// FormatOptions.DisplayUnits; 2021+ as FormatOptions.GetUnitTypeId().
        /// </summary>
        public static BbUnitId GetLengthUnit(DB.Document doc)
        {
#if REVIT_PRE_2021
            return doc.GetUnits().GetFormatOptions(DB.UnitType.UT_Length).DisplayUnits;
#else
            return doc.GetUnits().GetFormatOptions(DB.SpecTypeId.Length).GetUnitTypeId();
#endif
        }

        /// <summary>
        /// Unit conversion that tolerates "no unit". The nullable form differs
        /// per band (nullable enum on 2020, nullable reference on 2021+), so the
        /// unwrapping lives here rather than at every call site.
        /// </summary>
        public static double ToInternal(double value, BbUnitId? unit)
        {
#if REVIT_PRE_2021
            return unit.HasValue ? DB.UnitUtils.ConvertToInternalUnits(value, unit.Value) : value;
#else
            return unit != null ? DB.UnitUtils.ConvertToInternalUnits(value, unit) : value;
#endif
        }

        /// <inheritdoc cref="ToInternal"/>
        public static double FromInternal(double value, BbUnitId? unit)
        {
#if REVIT_PRE_2021
            return unit.HasValue ? DB.UnitUtils.ConvertFromInternalUnits(value, unit.Value) : value;
#else
            return unit != null ? DB.UnitUtils.ConvertFromInternalUnits(value, unit) : value;
#endif
        }

        /// <summary>
        /// Equality that works for both backing types — ForgeTypeId is a class
        /// whose == is reference-ish, DisplayUnitType is an enum.
        /// </summary>
        public static bool Is(BbUnitId a, BbUnitId b)
        {
#if REVIT_PRE_2021
            return a == b;
#else
            return a != null && b != null && a.TypeId == b.TypeId;
#endif
        }
    }

    /// <summary>
    /// The parameter specs the plugin uses, resolved per Revit band.
    /// On 2021+ these are <c>SpecTypeId</c> members; on 2020 the equivalent
    /// <c>ParameterType</c> enum values.
    /// </summary>
    public static class BbSpecs
    {
#if REVIT_PRE_2022
        public static BbSpecId Text => DB.ParameterType.Text;
        public static BbSpecId Integer => DB.ParameterType.Integer;
        public static BbSpecId Length => DB.ParameterType.Length;
        public static BbSpecId Area => DB.ParameterType.Area;
        public static BbSpecId Volume => DB.ParameterType.Volume;
        public static BbSpecId Angle => DB.ParameterType.Angle;
        public static BbSpecId YesNo => DB.ParameterType.YesNo;
#else
        public static BbSpecId Text => DB.SpecTypeId.String.Text;
        public static BbSpecId Integer => DB.SpecTypeId.Int.Integer;
        public static BbSpecId Length => DB.SpecTypeId.Length;
        public static BbSpecId Area => DB.SpecTypeId.Area;
        public static BbSpecId Volume => DB.SpecTypeId.Volume;
        public static BbSpecId Angle => DB.SpecTypeId.Angle;
        public static BbSpecId YesNo => DB.SpecTypeId.Boolean.YesNo;
#endif
    }
}
