using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TJ
{
    // Every field is a string, parsed with the same "absent = no override, present-but-garbage =
    // warn and skip" rules as the CSV loader - avoids relying on JsonUtility's partial-overwrite
    // behavior for enum/int fields, which can't distinguish "absent" from "explicit default".
    [Serializable]
    public struct HeroOverrideEntry
    {
        public string heroID;
        public string heroName;
        public string heroDescription;
        public string[] heroBonusDescription;
        public string heroPrefabName;
        public string unlockCondition;
        public string demoUnlockCondition;
        public string startingGold;
        public string signatureUnit;
        public string[] startingArmyUnits;
    }

    [Serializable]
    public class HeroOverrideFile
    {
        public List<HeroOverrideEntry> overrides = new();
    }

    // HeroID and Race are not overridable: HeroID is the lookup key, and campaign generation
    // assumes a stable hero-to-race pairing.
    public static class HeroOverrideLoader
    {
        public const string FileName = "hero_overrides.json";

        public static void ApplyOverridesFromModFolder(string modFolderPath, Dictionary<int, Hero> heroesByID)
        {
            string path = Path.Combine(modFolderPath, FileName);
            string modLabel = ModOverrideValidation.GetModLabel(modFolderPath);

            ModOverrideValidation.TryLoadFile(path,
                () => ApplyJson(File.ReadAllText(path), modLabel, heroesByID),
                $"HeroData ({modLabel})");
        }

        private static void ApplyJson(string json, string modLabel, Dictionary<int, Hero> heroesByID)
        {
            var file = JsonUtility.FromJson<HeroOverrideFile>(json);
            if (file?.overrides == null) return;

            int applied = 0;
            foreach (var entry in file.overrides)
            {
                string context = $"HeroData ({modLabel}) entry '{entry.heroID}'";
                if (string.IsNullOrEmpty(entry.heroID) || !int.TryParse(entry.heroID, out int heroID))
                {
                    Debug.LogWarning($"[ModOverride] {context}: missing or invalid heroID, skipping.");
                    continue;
                }
                if (!heroesByID.TryGetValue(heroID, out Hero hero))
                {
                    Debug.LogWarning($"[ModOverride] {context}: no hero found for HeroID {heroID}, skipping.");
                    continue;
                }

                if (entry.heroName != null) hero.HeroName = entry.heroName;
                if (entry.heroDescription != null) hero.HeroDescription = entry.heroDescription;
                if (entry.heroBonusDescription != null && ValidateBonusDescriptions(entry.heroBonusDescription, context))
                    hero.HeroBonusDescription = entry.heroBonusDescription;
                if (entry.heroPrefabName != null) hero.HeroPrefabName = entry.heroPrefabName;
                if (ModOverrideValidation.TryParseEnumOrWarn(entry.unlockCondition, "unlockCondition", context, out UnlockCondition uc)) hero.UnlockCondition = uc;
                if (ModOverrideValidation.TryParseEnumOrWarn(entry.demoUnlockCondition, "demoUnlockCondition", context, out UnlockCondition duc)) hero.DemoUnlockCondition = duc;
                if (!string.IsNullOrEmpty(entry.startingGold) && int.TryParse(entry.startingGold, out int gold)) hero.StartingGold = gold;
                if (ModOverrideValidation.TryParseEnumOrWarn(entry.signatureUnit, "signatureUnit", context, out UnitName su)) hero.SignatureUnit = su;

                if (entry.startingArmyUnits != null)
                {
                    var parsedArmy = new List<UnitName>();
                    foreach (var unitStr in entry.startingArmyUnits)
                    {
                        if (Enum.TryParse(unitStr, out UnitName unit)) parsedArmy.Add(unit);
                        else Debug.LogWarning($"[ModOverride] {context}: unknown unit '{unitStr}' in startingArmyUnits, skipping that entry.");
                    }
                    if (parsedArmy.Count > 0) hero.StartingArmyUnits = parsedArmy.ToArray();
                }

                heroesByID[heroID] = hero;
                applied++;
            }

            Debug.Log($"[ModOverride] HeroData ({modLabel}): applied overrides for {applied} hero(es).");
        }

        // Every consumer reads exactly two bonus lines (see HeroBonusText), so a short array or an
        // empty key would surface as a blank bonus line rather than the mod's intent. Rejecting the
        // whole field keeps the hero's original keys, matching the warn-and-skip contract the rest
        // of the loaders use for bad values.
        private const int RequiredBonusDescriptionCount = 2;

        private static bool ValidateBonusDescriptions(string[] descriptions, string context)
        {
            if (descriptions.Length != RequiredBonusDescriptionCount)
            {
                Debug.LogWarning($"[ModOverride] {context}: heroBonusDescription must have exactly {RequiredBonusDescriptionCount} entries (found {descriptions.Length}), ignoring that field.");
                return false;
            }

            for (int i = 0; i < descriptions.Length; i++)
            {
                if (string.IsNullOrEmpty(descriptions[i]))
                {
                    Debug.LogWarning($"[ModOverride] {context}: heroBonusDescription entry {i} is empty, ignoring that field.");
                    return false;
                }
            }

            return true;
        }

        public static string ExportTemplate(Dictionary<int, Hero> heroesByID)
        {
            var file = new HeroOverrideFile();
            foreach (var hero in heroesByID.Values.OrderBy(h => h.HeroID))
            {
                file.overrides.Add(new HeroOverrideEntry
                {
                    heroID = hero.HeroID.ToString(),
                    heroName = hero.HeroName,
                    heroDescription = hero.HeroDescription,
                    heroBonusDescription = hero.HeroBonusDescription,
                    heroPrefabName = hero.HeroPrefabName,
                    unlockCondition = hero.UnlockCondition.ToString(),
                    demoUnlockCondition = hero.DemoUnlockCondition.ToString(),
                    startingGold = hero.StartingGold.ToString(),
                    signatureUnit = hero.SignatureUnit.ToString(),
                    startingArmyUnits = Array.ConvertAll(hero.StartingArmyUnits ?? Array.Empty<UnitName>(), u => u.ToString())
                });
            }
            return JsonUtility.ToJson(file, true);
        }
    }
}
