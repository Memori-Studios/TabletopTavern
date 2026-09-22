using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace TJ
{
    [Serializable]
    public struct BonusConditionDto
    {
        public string filterKind;
        public string requiredRarityTier;
        public string[] unitNames;
        public string tag;
        public string[] unitTypes;
        public string[] unitSizes;
        public string requiredEnemyRace;
    }

    [Serializable]
    public struct HeroStatBonusRuleDto
    {
        public string heroID;
        public string localizationKey;
        public BonusConditionDto condition;
        public string stat;
        public string magnitudeKind;
        public string value;
    }

    [Serializable]
    public struct HeroAttributeBonusRuleDto
    {
        public string heroID;
        public string localizationKey;
        public BonusConditionDto condition;
        public string grantedAttribute;
    }

    [Serializable]
    public struct FactionBonusRuleDto
    {
        public string race;
        public string localizationKey;
        public string stat;
        public string magnitudeKind;
        public string value;
    }

    [Serializable]
    public class HeroBonusRuleOverrideFile
    {
        public List<HeroStatBonusRuleDto> statRules = new();
        public List<HeroAttributeBonusRuleDto> attributeRules = new();
        public List<FactionBonusRuleDto> factionRules = new();
    }

    // A mod's rule list for a given HeroID (stat/attribute rules) or Race (faction rules) replaces
    // the *entire* prior rule set for that key - not a field-level merge, since a hero can have a
    // variable number of rules rather than one fixed record.
    public static class HeroBonusRuleOverrideLoader
    {
        public const string FileName = "hero_bonus_rules.json";

        public static void ApplyOverridesFromModFolder(string modFolderPath, List<HeroStatBonusRule> statRules, List<HeroAttributeBonusRule> attributeRules, List<FactionBonusRule> factionRules)
        {
            string path = Path.Combine(modFolderPath, FileName);
            string modLabel = ModOverrideValidation.GetModLabel(modFolderPath);

            ModOverrideValidation.TryLoadFile(path,
                () => ApplyJson(File.ReadAllText(path), modLabel, statRules, attributeRules, factionRules),
                $"HeroBonusRules ({modLabel})");
        }

        // Exports the vanilla base rules (not whatever's currently loaded, which could already
        // include another mod's overrides) as a starting template for authoring new mods.
        public static string ExportTemplate()
        {
            var file = new HeroBonusRuleOverrideFile();

            foreach (var rule in HeroBonusRuleData.BaseStatRules)
            {
                file.statRules.Add(new HeroStatBonusRuleDto
                {
                    heroID = rule.HeroID.ToString(),
                    localizationKey = rule.LocalizationKey,
                    condition = ToDto(rule.Condition),
                    stat = rule.Stat.ToString(),
                    magnitudeKind = rule.MagnitudeKind.ToString(),
                    value = rule.Value.ToString(CultureInfo.InvariantCulture)
                });
            }

            foreach (var rule in HeroBonusRuleData.BaseAttributeRules)
            {
                file.attributeRules.Add(new HeroAttributeBonusRuleDto
                {
                    heroID = rule.HeroID.ToString(),
                    localizationKey = rule.LocalizationKey,
                    condition = ToDto(rule.Condition),
                    grantedAttribute = rule.GrantedAttribute.ToString()
                });
            }

            foreach (var rule in HeroBonusRuleData.BaseFactionRules)
            {
                file.factionRules.Add(new FactionBonusRuleDto
                {
                    race = rule.Race.ToString(),
                    localizationKey = rule.LocalizationKey,
                    stat = rule.Stat.ToString(),
                    magnitudeKind = rule.MagnitudeKind.ToString(),
                    value = rule.Value.ToString(CultureInfo.InvariantCulture)
                });
            }

            return JsonUtility.ToJson(file, true);
        }

        private static BonusConditionDto ToDto(BonusCondition condition) => new BonusConditionDto
        {
            filterKind = condition.FilterKind.ToString(),
            requiredRarityTier = condition.FilterKind == BonusFilterKind.RarityTier ? condition.RequiredRarityTier.ToString() : null,
            unitNames = condition.FilterKind == BonusFilterKind.UnitName ? Array.ConvertAll(condition.UnitNames, u => u.ToString()) : null,
            tag = condition.FilterKind == BonusFilterKind.UnitTag ? condition.Tag : null,
            unitTypes = condition.FilterKind == BonusFilterKind.UnitType ? Array.ConvertAll(condition.UnitTypes, t => t.ToString()) : null,
            unitSizes = condition.FilterKind == BonusFilterKind.UnitSize ? Array.ConvertAll(condition.UnitSizes, s => s.ToString()) : null,
            requiredEnemyRace = condition.FilterKind == BonusFilterKind.EnemyRace ? condition.RequiredEnemyRace.ToString() : null,
        };

        private static void ApplyJson(string json, string modLabel, List<HeroStatBonusRule> statRules, List<HeroAttributeBonusRule> attributeRules, List<FactionBonusRule> factionRules)
        {
            var file = JsonUtility.FromJson<HeroBonusRuleOverrideFile>(json);
            if (file == null) return;

            ApplyStatRules(file.statRules, modLabel, statRules);
            ApplyAttributeRules(file.attributeRules, modLabel, attributeRules);
            ApplyFactionRules(file.factionRules, modLabel, factionRules);
        }

        private static void ApplyStatRules(List<HeroStatBonusRuleDto> dtos, string modLabel, List<HeroStatBonusRule> statRules)
        {
            if (dtos == null || dtos.Count == 0) return;

            var heroIDsTouched = new HashSet<int>();
            var newRules = new List<HeroStatBonusRule>();
            foreach (var dto in dtos)
            {
                string context = $"HeroBonusRules ({modLabel}) statRule heroID '{dto.heroID}'";
                if (!TryParseHeroID(dto.heroID, context, out int heroID)) continue;
                if (!ModOverrideValidation.TryParseEnumOrWarn(dto.stat, "stat", context, out UnitStat stat))
                {
                    Debug.LogWarning($"[ModOverride] {context}: missing or unknown stat, skipping.");
                    continue;
                }
                if (!TryParseCondition(dto.condition, context, out BonusCondition condition)) continue;
                if (!TryParseValue(dto.value, context, out float value)) continue;
                BonusMagnitudeKind magnitudeKind = ParseMagnitudeKind(dto.magnitudeKind, context);
                // BaseUnitCount is applied when a squad is recruited and written into the save, so it
                // cannot depend on the enemy and a percent of a save-time value would drift from the card.
                if (stat == UnitStat.BaseUnitCount && condition.FilterKind == BonusFilterKind.EnemyRace)
                {
                    Debug.LogWarning($"[ModOverride] {context}: BaseUnitCount cannot use an EnemyRace condition, skipping.");
                    continue;
                }
                if (stat == UnitStat.BaseUnitCount && magnitudeKind != BonusMagnitudeKind.Flat)
                {
                    Debug.LogWarning($"[ModOverride] {context}: BaseUnitCount must use magnitudeKind Flat, skipping.");
                    continue;
                }

                heroIDsTouched.Add(heroID);
                newRules.Add(new HeroStatBonusRule { HeroID = heroID, LocalizationKey = dto.localizationKey, Condition = condition, Stat = stat, MagnitudeKind = magnitudeKind, Value = value });
            }

            statRules.RemoveAll(r => heroIDsTouched.Contains(r.HeroID));
            statRules.AddRange(newRules);
            Debug.Log($"[ModOverride] HeroBonusRules ({modLabel}): replaced stat rules for {heroIDsTouched.Count} hero(es) with {newRules.Count} rule(s).");
        }

        private static void ApplyAttributeRules(List<HeroAttributeBonusRuleDto> dtos, string modLabel, List<HeroAttributeBonusRule> attributeRules)
        {
            if (dtos == null || dtos.Count == 0) return;

            var heroIDsTouched = new HashSet<int>();
            var newRules = new List<HeroAttributeBonusRule>();
            foreach (var dto in dtos)
            {
                string context = $"HeroBonusRules ({modLabel}) attributeRule heroID '{dto.heroID}'";
                if (!TryParseHeroID(dto.heroID, context, out int heroID)) continue;
                if (!ModOverrideValidation.TryParseEnumOrWarn(dto.grantedAttribute, "grantedAttribute", context, out UnitAttribute attribute))
                {
                    Debug.LogWarning($"[ModOverride] {context}: missing or unknown grantedAttribute, skipping.");
                    continue;
                }
                if (!TabletopTavernConstants.HasBackingField(attribute))
                {
                    Debug.LogWarning($"[ModOverride] {context}: '{attribute}' has no SquadAttributes backing field, skipping.");
                    continue;
                }
                if (!TryParseCondition(dto.condition, context, out BonusCondition condition)) continue;

                heroIDsTouched.Add(heroID);
                newRules.Add(new HeroAttributeBonusRule { HeroID = heroID, LocalizationKey = dto.localizationKey, Condition = condition, GrantedAttribute = attribute });
            }

            attributeRules.RemoveAll(r => heroIDsTouched.Contains(r.HeroID));
            attributeRules.AddRange(newRules);
            Debug.Log($"[ModOverride] HeroBonusRules ({modLabel}): replaced attribute rules for {heroIDsTouched.Count} hero(es) with {newRules.Count} rule(s).");
        }

        private static void ApplyFactionRules(List<FactionBonusRuleDto> dtos, string modLabel, List<FactionBonusRule> factionRules)
        {
            if (dtos == null || dtos.Count == 0) return;

            var racesTouched = new HashSet<Race>();
            var newRules = new List<FactionBonusRule>();
            foreach (var dto in dtos)
            {
                string context = $"HeroBonusRules ({modLabel}) factionRule race '{dto.race}'";
                if (!ModOverrideValidation.TryParseEnumOrWarn(dto.race, "race", context, out Race race))
                {
                    Debug.LogWarning($"[ModOverride] {context}: missing or unknown race, skipping.");
                    continue;
                }
                if (!ModOverrideValidation.TryParseEnumOrWarn(dto.stat, "stat", context, out UnitStat stat))
                {
                    Debug.LogWarning($"[ModOverride] {context}: missing or unknown stat, skipping.");
                    continue;
                }
                if (!TryParseValue(dto.value, context, out float value)) continue;
                BonusMagnitudeKind magnitudeKind = ParseMagnitudeKind(dto.magnitudeKind, context);
                // The faction gate depends on army composition, which changes after recruitment.
                if (stat == UnitStat.BaseUnitCount)
                {
                    Debug.LogWarning($"[ModOverride] {context}: BaseUnitCount is not available as a faction rule, skipping.");
                    continue;
                }

                racesTouched.Add(race);
                newRules.Add(new FactionBonusRule { Race = race, LocalizationKey = dto.localizationKey, Stat = stat, MagnitudeKind = magnitudeKind, Value = value });
            }

            factionRules.RemoveAll(r => racesTouched.Contains(r.Race));
            factionRules.AddRange(newRules);
            Debug.Log($"[ModOverride] HeroBonusRules ({modLabel}): replaced faction rules for {racesTouched.Count} race(s) with {newRules.Count} rule(s).");
        }

        private static bool TryParseHeroID(string raw, string context, out int heroID)
        {
            heroID = 0;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out heroID)) return true;
            Debug.LogWarning($"[ModOverride] {context}: missing or invalid heroID, skipping.");
            return false;
        }

        private static bool TryParseValue(string raw, string context, out float value)
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            Debug.LogWarning($"[ModOverride] {context}: missing or invalid value '{raw}', skipping.");
            return false;
        }

        private static BonusMagnitudeKind ParseMagnitudeKind(string raw, string context)
        {
            if (string.IsNullOrEmpty(raw)) return BonusMagnitudeKind.Flat;
            ModOverrideValidation.TryParseEnumOrWarn(raw, "magnitudeKind", context, out BonusMagnitudeKind kind);
            return kind;
        }

        private static bool TryParseCondition(BonusConditionDto dto, string context, out BonusCondition condition)
        {
            condition = default;
            if (!ModOverrideValidation.TryParseEnumOrWarn(dto.filterKind, "filterKind", context, out BonusFilterKind kind))
            {
                Debug.LogWarning($"[ModOverride] {context}: missing or unknown condition.filterKind, skipping rule.");
                return false;
            }

            condition.FilterKind = kind;
            switch (kind)
            {
                case BonusFilterKind.RarityTier:
                    if (!ModOverrideValidation.TryParseEnumOrWarn(dto.requiredRarityTier, "requiredRarityTier", context, out UnitRarity tier)) return false;
                    condition.RequiredRarityTier = tier;
                    break;
                case BonusFilterKind.UnitName:
                    condition.UnitNames = ParseEnumArray<UnitName>(dto.unitNames, "unitNames", context);
                    if (condition.UnitNames.Length == 0) return false;
                    break;
                case BonusFilterKind.UnitTag:
                    if (string.IsNullOrEmpty(dto.tag))
                    {
                        Debug.LogWarning($"[ModOverride] {context}: UnitTag filter with no tag, skipping rule.");
                        return false;
                    }
                    condition.Tag = dto.tag;
                    break;
                case BonusFilterKind.UnitType:
                    condition.UnitTypes = ParseEnumArray<UnitType>(dto.unitTypes, "unitTypes", context);
                    if (condition.UnitTypes.Length == 0) return false;
                    break;
                case BonusFilterKind.UnitSize:
                    condition.UnitSizes = ParseEnumArray<UnitSize>(dto.unitSizes, "unitSizes", context);
                    if (condition.UnitSizes.Length == 0) return false;
                    break;
                case BonusFilterKind.EnemyRace:
                    if (!ModOverrideValidation.TryParseEnumOrWarn(dto.requiredEnemyRace, "requiredEnemyRace", context, out Race race)) return false;
                    condition.RequiredEnemyRace = race;
                    break;
            }

            return true;
        }

        private static TEnum[] ParseEnumArray<TEnum>(string[] raw, string fieldName, string context) where TEnum : struct
        {
            if (raw == null) return Array.Empty<TEnum>();
            var result = new List<TEnum>();
            foreach (var s in raw)
            {
                if (Enum.TryParse(s, out TEnum value)) result.Add(value);
                else Debug.LogWarning($"[ModOverride] {context}: unknown {fieldName} value '{s}', skipping that entry.");
            }
            return result.ToArray();
        }
    }
}
