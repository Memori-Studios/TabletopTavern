using System.Collections.Generic;

namespace TabletopTavern.GpuAnim
{
    /// <summary>Which units have a visual folder overriding the built-in bake this session, keyed by the unit's enum member name.</summary>
    public static class UnitVisualOverrides
    {
        private static readonly Dictionary<string, string> s_folders = new();

        public static int Count => s_folders.Count;

        /// <summary>Later registrations win, which is the mod load order rule.</summary>
        public static void Register(string unitName, string folder)
        {
            s_folders[unitName] = folder;
        }

        public static void Clear() => s_folders.Clear();

        public static bool TryGet(string unitName, out string folder) => s_folders.TryGetValue(unitName, out folder);

        public static IEnumerable<KeyValuePair<string, string>> All => s_folders;
    }
}
