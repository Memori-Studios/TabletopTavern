using System;
using System.Collections.Generic;
using System.IO;
using Memori.Localization;
using UnityEngine;

namespace TJ
{
    [Serializable]
    public struct LocalizationOverrideEntryDto
    {
        public string locale;
        public string key;
        public string text;
    }

    [Serializable]
    public class LocalizationOverrideFile
    {
        public List<LocalizationOverrideEntryDto> overrides = new();
    }

    // Loads a mod's localization_overrides.json and registers each (locale, key) -> text entry with
    // LocalizationOverrides (in the Memori.Localization assembly). Later mods in load order win, same
    // as every other override loader, because Set overwrites an existing (locale, key).
    //
    // Unlike a nested "{ locale: { key: text } }" shape, this uses a flat array of entries because
    // Unity's JsonUtility can't deserialize dictionaries with dynamic keys - matching the array-of-
    // structs pattern the other override files already use.
    public static class LocalizationOverrideLoader
    {
        public const string FileName = "localization_overrides.json";

        // Unlike the other loaders there's no "current game data" to round-trip here - the full
        // string table lives in Unity Localization assets, not a synchronously-enumerable source -
        // so the template is a small illustrative stub rather than a dump of every key.
        public static string ExportTemplate()
        {
            var file = new LocalizationOverrideFile();
            file.overrides.Add(new LocalizationOverrideEntryDto { locale = "en", key = "heroBonusTitle2", text = "Inspiring Presence" });
            // Paired with the heroName1 key visible in the exported hero_overrides.json template, to
            // show that renaming a hero is a text override rather than a hero_overrides.json edit.
            file.overrides.Add(new LocalizationOverrideEntryDto { locale = "en", key = "heroName1", text = "Edric the Unyielding" });
            return JsonUtility.ToJson(file, true);
        }

        public static void ApplyOverridesFromModFolder(string modFolderPath)
        {
            string path = Path.Combine(modFolderPath, FileName);
            string modLabel = ModOverrideValidation.GetModLabel(modFolderPath);

            ModOverrideValidation.TryLoadFile(path,
                () => ApplyJson(File.ReadAllText(path), modLabel),
                $"LocalizationOverrides ({modLabel})");
        }

        private static void ApplyJson(string json, string modLabel)
        {
            var file = JsonUtility.FromJson<LocalizationOverrideFile>(json);
            if (file == null || file.overrides == null) return;

            int applied = 0;
            foreach (var dto in file.overrides)
            {
                if (string.IsNullOrEmpty(dto.locale))
                {
                    Debug.LogWarning($"[ModOverride] LocalizationOverrides ({modLabel}): entry for key '{dto.key}' has no locale, skipping.");
                    continue;
                }
                if (string.IsNullOrEmpty(dto.key))
                {
                    Debug.LogWarning($"[ModOverride] LocalizationOverrides ({modLabel}): entry with no key, skipping.");
                    continue;
                }

                LocalizationOverrides.Set(dto.locale, dto.key, dto.text);
                applied++;
            }

            Debug.Log($"[ModOverride] LocalizationOverrides ({modLabel}): applied {applied} override(s).");
        }
    }
}
