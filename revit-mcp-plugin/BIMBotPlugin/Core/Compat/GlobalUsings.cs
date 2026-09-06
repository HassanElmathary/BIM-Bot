// Revit 2021 replaced the enum-based units/spec API with ForgeTypeId. Revit 2020
// has no ForgeTypeId at all, so the *types* differ, not just the constants —
// aliases let the call sites stay identical across every band.
//
//   Revit 2020  : DisplayUnitType (units), ParameterType (specs)
//   Revit 2021+ : ForgeTypeId for both
//
// Constants live in BbUnits / BbSpecs (Core\Compat\UnitsCompat.cs).

// Makes the ElementId extension methods (Val/ToElementId) visible everywhere
// without touching the using block of all 92 source files.
global using BIMBotPlugin.Core.Compat;

// Units and specs moved to ForgeTypeId in *different* releases, so they need
// separate boundaries:
//   units : DisplayUnitType  -> ForgeTypeId in 2021
//   specs : ParameterType    -> ForgeTypeId in 2022
//           (2021 has SpecTypeId but no SpecTypeId.String/.Boolean, and
//            ExternalDefinitionCreationOptions still only takes ParameterType)
#if REVIT_PRE_2021
global using BbUnitId = Autodesk.Revit.DB.DisplayUnitType;
#else
global using BbUnitId = Autodesk.Revit.DB.ForgeTypeId;
#endif

#if REVIT_PRE_2022
global using BbSpecId = Autodesk.Revit.DB.ParameterType;
#else
global using BbSpecId = Autodesk.Revit.DB.ForgeTypeId;
#endif
