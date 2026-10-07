using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TJ
{
    [Serializable]
    public struct ModListEntry
    {
        public string folder;
        public bool enabled;
    }

    [Serializable]
    public class ModListFile
    {
        public List<ModListEntry> loadOrder = new();
    }

    public static class ModLoadOrder
    {
        public const string ModsFolderName = "Mods";
        public const string ModListFileName = "modlist.json";
        public const string ModManifestFileName = "mod.json";

        private static string _modsRootOverride;
        public static string ModsRootPath => _modsRootOverride ?? Path.Combine(Application.persistentDataPath, ModsFolderName);

        /// <summary>Tests and harnesses point the mod folder away from the player's real mods. Null restores the default.</summary>
        public static void SetModsRoot(string root)
        {
            _modsRootOverride = string.IsNullOrEmpty(root) ? null : root;
        }

        // Folder names actually applied by TabletopTavernData.ApplyModOverrides() at boot, frozen
        // for the session. Enable/reorder edits made afterward in the mod list UI change
        // modlist.json immediately but don't take effect until restart, so this snapshot - not a
        // fresh GetEnabledModFolderPathsInOrder() call - is what "loaded this session" must compare
        // against.
        public static ISet<string> LoadedFolderNamesThisSession { get; private set; } = new HashSet<string>();

        public static void SetLoadedSnapshot(List<string> folderPaths)
        {
            LoadedFolderNamesThisSession = new HashSet<string>(folderPaths.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
        }

        // Ordered, enabled-only mod folders (absolute paths). Known folders (already listed in
        // modlist.json) load in their saved order; any folder found on disk that isn't listed yet
        // (freshly synced from Steam Workshop, or manually dropped in) is enabled by default and
        // appended alphabetically - so a new mod works immediately without requiring a mod-list UI
        // visit first.
        public static List<string> GetEnabledModFolderPathsInOrder()
        {
            var result = new List<string>();
            string modsRoot = ModsRootPath;
            if (!Directory.Exists(modsRoot)) return result;

            var knownFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string modListPath = Path.Combine(modsRoot, ModListFileName);
            if (File.Exists(modListPath))
            {
                ModListFile modList = null;
                try
                {
                    modList = JsonUtility.FromJson<ModListFile>(File.ReadAllText(modListPath));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ModLoadOrder] Failed to parse {modListPath}: {e.Message}.");
                }

                if (modList?.loadOrder != null)
                {
                    foreach (var entry in modList.loadOrder)
                    {
                        if (string.IsNullOrWhiteSpace(entry.folder)) continue;
                        knownFolders.Add(entry.folder);

                        if (!entry.enabled) continue;
                        string folderPath = Path.Combine(modsRoot, entry.folder);
                        if (Directory.Exists(folderPath))
                        {
                            result.Add(folderPath);
                        }
                        else
                        {
                            Debug.LogWarning($"[ModLoadOrder] modlist.json references missing folder '{entry.folder}', skipping.");
                        }
                    }
                }
            }

            var newFolders = Directory.GetDirectories(modsRoot)
                .Select(Path.GetFileName)
                .Where(IsRealModFolder)
                .Where(name => !knownFolders.Contains(name))
                .Where(name => File.Exists(Path.Combine(modsRoot, name, ModManifestFileName)))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

            foreach (string name in newFolders)
                result.Add(Path.Combine(modsRoot, name));

            return result;
        }

        // Folders prefixed with "_" are reserved (e.g. "_Template", ModTemplateExporter's output)
        // and are never treated as an active mod, even if they contain a mod.json.
        public static bool IsRealModFolder(string folderName) => !string.IsNullOrEmpty(folderName) && !folderName.StartsWith("_");

        public static void EnsureModsDirectoryExists()
        {
            Directory.CreateDirectory(ModsRootPath);
        }
    }
}
