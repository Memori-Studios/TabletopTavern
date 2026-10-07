using System;
using System.Collections.Generic;
using System.IO;
using MemoriStudios.TJBake;
using MemoriStudios.TJBake.Format;
using TabletopTavern.GpuAnim;
using UnityEngine;

namespace TJ
{
    /// <summary>
    /// Registers each mod's unit_visuals/&lt;UnitName&gt;/ folder as that unit's battle visual and card icon. Only the
    /// manifest is read at boot; the meshes and animation load at battle load.
    /// </summary>
    public static class UnitVisualOverrideLoader
    {
        public const string FolderName = "unit_visuals";
        public const string IconFileName = "icon.png";
        public const int IconSize = 256;
        public const int ShootingSlotCount = 15;

        // Icons outlive a ReloadData because cards already showing one keep the sprite; keyed by file path.
        private static readonly Dictionary<string, Sprite> s_iconsByPath = new();
        private static readonly Dictionary<UnitName, string> s_iconPathByUnit = new();

        #region Boot scan

        /// <summary>Called once per TabletopTavernData.ApplyModOverrides() before the mod loop.</summary>
        public static void ClearOverrides()
        {
            UnitVisualOverrides.Clear();
            s_iconPathByUnit.Clear();
        }

        public static void ApplyOverridesFromModFolder(string modFolderPath, Dictionary<UnitName, SquadStats> stats)
        {
            string root = Path.Combine(modFolderPath, FolderName);
            if (!Directory.Exists(root)) return;
            string modLabel = ModOverrideValidation.GetModLabel(modFolderPath);
            string[] folders;
            try { folders = Directory.GetDirectories(root); }
            catch (Exception e)
            {
                Debug.LogError($"[ModOverride] Unit visuals ({modLabel}): could not list '{root}' ({e.Message}).");
                return;
            }
            Array.Sort(folders, StringComparer.Ordinal);
            int applied = 0;
            foreach (string folder in folders)
            {
                if (TryRegister(folder, modLabel, stats)) applied++;
            }
            if (applied > 0) Debug.Log($"[ModOverride] Unit visuals ({modLabel}): {applied} unit visual(s) registered.");
        }

        private static bool TryRegister(string folder, string modLabel, Dictionary<UnitName, SquadStats> stats)
        {
            string name = Path.GetFileName(folder);
            if (name.StartsWith("_")) return false;
            string context = $"[ModOverride] Unit visuals ({modLabel}) '{name}'";
            if (!TryParseUnitName(name, out UnitName unit) || !stats.TryGetValue(unit, out SquadStats unitStats))
            {
                Debug.LogWarning($"{context}: no unit has this name in this build. Skipping.");
                return false;
            }
            if (unitStats.unitType == UnitType.Artillery || unitStats.unitType == UnitType.Structure || unitStats.unitSize == UnitSize.Artillery)
            {
                Debug.LogWarning($"{context}: artillery and gates cannot take a unit visual yet. Skipping.");
                return false;
            }
            UnitJson json = TJBakeVisualLoader.ReadManifest(folder, out string error);
            if (json == null)
            {
                Debug.LogError($"{context}: {error}. The built-in visual stays.");
                return false;
            }
            string problem = CheckAgainstUnit(json, name, unitStats);
            if (problem != null)
            {
                Debug.LogError($"{context}: {problem}. The built-in visual stays.");
                return false;
            }
            UnitVisualOverrides.Register(name, folder);
            string icon = Path.Combine(folder, IconFileName);
            if (File.Exists(icon)) s_iconPathByUnit[unit] = icon;
            else s_iconPathByUnit.Remove(unit);
            return true;
        }

        // Exact member names only: Enum.TryParse would also accept "3" or a different case.
        private static bool TryParseUnitName(string name, out UnitName unit)
        {
            unit = default;
            if (!Enum.IsDefined(typeof(UnitName), name)) return false;
            unit = (UnitName)Enum.Parse(typeof(UnitName), name);
            return true;
        }

        /// <summary>
        /// The rules that need the unit's stats, which the package cannot know. Null when the folder fits. The bow and sword
        /// rule needs the built-in visual, so UnitGPUAnimLoader checks it at battle load.
        /// </summary>
        public static string CheckAgainstUnit(UnitJson json, string folderName, SquadStats unitStats)
        {
            if (json.kind != "unit") return $"unit.json kind is '{json.kind}', expected 'unit'";
            if (!string.IsNullOrEmpty(json.unitName) && json.unitName != folderName) return $"unit.json unitName '{json.unitName}' does not match the folder name";
            bool shoots = TabletopTavernConstants.Shoots(unitStats.unitType) || TabletopTavernConstants.Casts(unitStats.unitType);
            if (shoots && json.slots.Count < ShootingSlotCount) return $"a {unitStats.unitType} unit needs slots 0 to {ShootingSlotCount - 1}, this has {json.slots.Count}";
            return null;
        }

        #endregion

        #region Icons

        /// <summary>The mod's icon.png for this unit, loaded on first use. False when no enabled mod ships one or it fails to load.</summary>
        public static bool TryGetIcon(UnitName unit, out Sprite sprite)
        {
            sprite = null;
            if (!s_iconPathByUnit.TryGetValue(unit, out string path)) return false;
            if (s_iconsByPath.TryGetValue(path, out sprite)) return sprite != null;
            sprite = LoadIcon(path, unit);
            s_iconsByPath[path] = sprite;
            return sprite != null;
        }

        private static Sprite LoadIcon(string path, UnitName unit)
        {
            Texture2D texture = null;
            try
            {
                if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("the file is over 4 MB");
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = $"{unit} mod icon" };
                if (!texture.LoadImage(File.ReadAllBytes(path), true)) throw new InvalidDataException("it is not a PNG");
                if (texture.width != IconSize || texture.height != IconSize) throw new InvalidDataException($"it is {texture.width}x{texture.height}, not {IconSize}x{IconSize}");
                // UnloadUnusedAssets runs on every state change and would free a sprite only the cards hold.
                texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = texture.name;
                sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return sprite;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ModOverride] Unit visuals: icon '{path}' for {unit} was not used ({e.Message}). The built-in icon stays.");
                // Edit-mode tools read icons too, where Destroy is refused.
                if (texture != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                    else UnityEngine.Object.DestroyImmediate(texture);
                }
                return null;
            }
        }

        #endregion
    }
}
