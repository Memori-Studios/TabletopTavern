using System.Collections.Generic;

namespace TabletopTavern.GpuAnim
{
    /// <summary>
    /// Which units have a visual folder this session, keyed by the unit's enum member name: mod folders, and the game's
    /// own re-baked units shipped as built-in folders. A mod folder wins over a built-in one.
    /// </summary>
    public static class UnitVisualOverrides
    {
        private static readonly Dictionary<string, string> s_folders = new();
        private static readonly Dictionary<string, string> s_builtIn = new();

        /// <summary>Mod folders only.</summary>
        public static int Count => s_folders.Count;

        /// <summary>Later registrations win, which is the mod load order rule.</summary>
        public static void Register(string unitName, string folder)
        {
            s_folders[unitName] = folder;
        }

        public static void RegisterBuiltIn(string unitName, string folder)
        {
            s_builtIn[unitName] = folder;
        }

        public static void Clear()
        {
            s_folders.Clear();
            s_builtIn.Clear();
        }

        public static bool TryGet(string unitName, out string folder) => s_folders.TryGetValue(unitName, out folder);

        public static bool TryGetBuiltIn(string unitName, out string folder) => s_builtIn.TryGetValue(unitName, out folder);

        public static IEnumerable<KeyValuePair<string, string>> All => s_folders;

        public static IEnumerable<KeyValuePair<string, string>> AllBuiltIn => s_builtIn;
    }
}
