#if REVIT2020
using System.Collections.Generic;

namespace BIMBotPlugin.Core.Compat
{
    /// <summary>
    /// Revit 2020 targets .NET Framework 4.7, which predates
    /// Enumerable.ToHashSet (added in 4.7.2). Guarded to the 2020 band only so
    /// it can never collide with the real one on later targets.
    /// </summary>
    public static class FrameworkCompat
    {
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            return new HashSet<T>(source);
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer)
        {
            return new HashSet<T>(source, comparer);
        }
    }
}
#endif
