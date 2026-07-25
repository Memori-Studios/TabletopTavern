using System;
using System.IO;
using UnityEngine;

namespace TJ
{
    // Each race block below is sparse and string-typed, same pattern as unit_overrides.json - a
    // modder specifies only the passive values they want to change; anything omitted keeps its
    // shipped value. Writes into RaceBonusRuleData (Components assembly), which the per-race ECS
    // bonus systems read live. Only magnitudes/caps/thresholds/durations are tunable; the trigger
    // logic of each passive stays hardcoded.
    [Serializable]
    public struct CrashingHordeOverrideEntry
    {
        public string weaponStrengthPerStack;
        public string maxStacks;
        public string healthThreshold;
        public string updateInterval;
    }

    [Serializable]
    public struct ApexHuntersOverrideEntry
    {
        public string weaponStrengthPerStack;
        public string maxStacks;
        public string updateInterval;
    }

    [Serializable]
    public struct HuntersPatienceOverrideEntry
    {
        public string rangedBonusPerTick;
        public string meleeBonusPerTick;
        public string rangedBonusCap;
        public string meleeBonusCap;
        public string updateInterval;
    }

    [Serializable]
    public struct KenseiEyeOverrideEntry
    {
        public string meleeAttackPerStage;
        public string secondsPerStage;
        public string maxStages;
        public string updateInterval;
    }

    [Serializable]
    public struct OathcarvedOverrideEntry
    {
        public string weaponStrengthPerDeath;
    }

    [Serializable]
    public struct IronResolveOverrideEntry
    {
        public string clampDurationSeconds;
    }

    [Serializable]
    public struct DeathcryOverrideEntry
    {
        public string meleeAttackBonus;
        public string durationSeconds;
    }

    [Serializable]
    public struct SanguineCourtOverrideEntry
    {
        public string immuneToFlankMorale;
        public string immuneToTerror;
        public string immuneToRetreatingAlliesMorale;
    }

    [Serializable]
    public class RaceBonusOverrideFile
    {
        public CrashingHordeOverrideEntry gruntkin;
        public ApexHuntersOverrideEntry drakosaurBrood;
        public HuntersPatienceOverrideEntry taelindorForest;
        public KenseiEyeOverrideEntry sakuraDynasty;
        public OathcarvedOverrideEntry deepstoneHold;
        public IronResolveOverrideEntry ironLegion;
        public DeathcryOverrideEntry ravenHost;
        public SanguineCourtOverrideEntry sanguineCourt;
    }

    public static class RaceBonusOverrideLoader
    {
        public const string FileName = "race_bonus_overrides.json";

        public static void ApplyOverridesFromModFolder(string modFolderPath)
        {
            string path = Path.Combine(modFolderPath, FileName);
            string modLabel = ModOverrideValidation.GetModLabel(modFolderPath);

            ModOverrideValidation.TryLoadFile(path,
                () => ApplyJson(File.ReadAllText(path), modLabel),
                $"RaceBonus ({modLabel})");
        }

        private static void ApplyJson(string json, string modLabel)
        {
            var file = JsonUtility.FromJson<RaceBonusOverrideFile>(json);
            if (file == null) return;

            ApplyGruntkin(file.gruntkin, $"RaceBonus ({modLabel}) gruntkin");
            ApplyDrakosaur(file.drakosaurBrood, $"RaceBonus ({modLabel}) drakosaurBrood");
            ApplyTaelindor(file.taelindorForest, $"RaceBonus ({modLabel}) taelindorForest");
            ApplySakura(file.sakuraDynasty, $"RaceBonus ({modLabel}) sakuraDynasty");
            ApplyDeepstone(file.deepstoneHold, $"RaceBonus ({modLabel}) deepstoneHold");
            ApplyIronLegion(file.ironLegion, $"RaceBonus ({modLabel}) ironLegion");
            ApplyRavenHost(file.ravenHost, $"RaceBonus ({modLabel}) ravenHost");
            ApplySanguineCourt(file.sanguineCourt, $"RaceBonus ({modLabel}) sanguineCourt");

            Debug.Log($"[ModOverride] RaceBonus ({modLabel}): applied race passive overrides.");
        }

        private static void ApplyGruntkin(CrashingHordeOverrideEntry e, string context)
        {
            var c = RaceBonusRuleData.CrashingHorde;
            if (ModOverrideValidation.TryParseIntOrWarn(e.weaponStrengthPerStack, "weaponStrengthPerStack", context, out int wpn)) c.WeaponStrengthPerStack = wpn;
            if (ModOverrideValidation.TryParseIntOrWarn(e.maxStacks, "maxStacks", context, out int stacks)) c.MaxStacks = stacks;
            if (TryPositiveFloat(e.healthThreshold, "healthThreshold", context, out float hp)) c.HealthThreshold = hp;
            if (TryPositiveFloat(e.updateInterval, "updateInterval", context, out float interval)) c.UpdateInterval = interval;
            RaceBonusRuleData.CrashingHorde = c;
        }

        private static void ApplyDrakosaur(ApexHuntersOverrideEntry e, string context)
        {
            var c = RaceBonusRuleData.ApexHunters;
            if (ModOverrideValidation.TryParseIntOrWarn(e.weaponStrengthPerStack, "weaponStrengthPerStack", context, out int wpn)) c.WeaponStrengthPerStack = wpn;
            if (ModOverrideValidation.TryParseIntOrWarn(e.maxStacks, "maxStacks", context, out int stacks)) c.MaxStacks = stacks;
            if (TryPositiveFloat(e.updateInterval, "updateInterval", context, out float interval)) c.UpdateInterval = interval;
            RaceBonusRuleData.ApexHunters = c;
        }

        private static void ApplyTaelindor(HuntersPatienceOverrideEntry e, string context)
        {
            var c = RaceBonusRuleData.HuntersPatience;
            if (ModOverrideValidation.TryParseIntOrWarn(e.rangedBonusPerTick, "rangedBonusPerTick", context, out int rpt)) c.RangedBonusPerTick = rpt;
            if (ModOverrideValidation.TryParseIntOrWarn(e.meleeBonusPerTick, "meleeBonusPerTick", context, out int mpt)) c.MeleeBonusPerTick = mpt;
            if (ModOverrideValidation.TryParseIntOrWarn(e.rangedBonusCap, "rangedBonusCap", context, out int rcap)) c.RangedBonusCap = rcap;
            if (ModOverrideValidation.TryParseIntOrWarn(e.meleeBonusCap, "meleeBonusCap", context, out int mcap)) c.MeleeBonusCap = mcap;
            if (TryPositiveFloat(e.updateInterval, "updateInterval", context, out float interval)) c.UpdateInterval = interval;
            RaceBonusRuleData.HuntersPatience = c;
        }

        private static void ApplySakura(KenseiEyeOverrideEntry e, string context)
        {
            var c = RaceBonusRuleData.KenseiEye;
            if (ModOverrideValidation.TryParseIntOrWarn(e.meleeAttackPerStage, "meleeAttackPerStage", context, out int atk)) c.MeleeAttackPerStage = atk;
            if (TryPositiveFloat(e.secondsPerStage, "secondsPerStage", context, out float sps)) c.SecondsPerStage = sps;
            if (ModOverrideValidation.TryParseIntOrWarn(e.maxStages, "maxStages", context, out int stages)) c.MaxStages = stages;
            if (TryPositiveFloat(e.updateInterval, "updateInterval", context, out float interval)) c.UpdateInterval = interval;
            RaceBonusRuleData.KenseiEye = c;
        }

        private static void ApplyDeepstone(OathcarvedOverrideEntry e, string context)
        {
            var c = RaceBonusRuleData.Oathcarved;
            if (ModOverrideValidation.TryParseIntOrWarn(e.weaponStrengthPerDeath, "weaponStrengthPerDeath", context, out int wpn)) c.WeaponStrengthPerDeath = wpn;
            RaceBonusRuleData.Oathcarved = c;
        }

        private static void ApplyIronLegion(IronResolveOverrideEntry e, string context)
        {
            var c = RaceBonusRuleData.IronResolve;
            if (TryPositiveFloat(e.clampDurationSeconds, "clampDurationSeconds", context, out float dur)) c.ClampDurationSeconds = dur;
            RaceBonusRuleData.IronResolve = c;
        }

        private static void ApplyRavenHost(DeathcryOverrideEntry e, string context)
        {
            var c = RaceBonusRuleData.Deathcry;
            if (ModOverrideValidation.TryParseIntOrWarn(e.meleeAttackBonus, "meleeAttackBonus", context, out int atk)) c.MeleeAttackBonus = atk;
            if (TryPositiveFloat(e.durationSeconds, "durationSeconds", context, out float dur)) c.DurationSeconds = dur;
            RaceBonusRuleData.Deathcry = c;
        }

        private static void ApplySanguineCourt(SanguineCourtOverrideEntry e, string context)
        {
            var c = RaceBonusRuleData.SanguineCourt;
            if (ModOverrideValidation.TryParseBoolOrWarn(e.immuneToFlankMorale, "immuneToFlankMorale", context, out bool flank)) c.ImmuneToFlankMorale = flank;
            if (ModOverrideValidation.TryParseBoolOrWarn(e.immuneToTerror, "immuneToTerror", context, out bool terror)) c.ImmuneToTerror = terror;
            if (ModOverrideValidation.TryParseBoolOrWarn(e.immuneToRetreatingAlliesMorale, "immuneToRetreatingAlliesMorale", context, out bool retreat)) c.ImmuneToRetreatingAlliesMorale = retreat;
            RaceBonusRuleData.SanguineCourt = c;
        }

        // Floats used as timers/divisors/thresholds must stay positive, otherwise a mod could stall
        // a system in a per-frame loop or divide by zero. Absent/empty is "no override" (silent).
        private static bool TryPositiveFloat(string raw, string field, string context, out float value)
        {
            if (!ModOverrideValidation.TryParseFloatOrWarn(raw, field, context, out value)) return false;
            if (value > 0f) return true;
            Debug.LogWarning($"[ModOverride] {context}: {field} must be positive, ignoring value {value}.");
            return false;
        }

        // Exports the current (default) values as a complete starting point a modder trims down.
        public static string ExportTemplate()
        {
            var file = new RaceBonusOverrideFile
            {
                gruntkin = new CrashingHordeOverrideEntry
                {
                    weaponStrengthPerStack = RaceBonusRuleData.CrashingHorde.WeaponStrengthPerStack.ToString(),
                    maxStacks = RaceBonusRuleData.CrashingHorde.MaxStacks.ToString(),
                    healthThreshold = F(RaceBonusRuleData.CrashingHorde.HealthThreshold),
                    updateInterval = F(RaceBonusRuleData.CrashingHorde.UpdateInterval),
                },
                drakosaurBrood = new ApexHuntersOverrideEntry
                {
                    weaponStrengthPerStack = RaceBonusRuleData.ApexHunters.WeaponStrengthPerStack.ToString(),
                    maxStacks = RaceBonusRuleData.ApexHunters.MaxStacks.ToString(),
                    updateInterval = F(RaceBonusRuleData.ApexHunters.UpdateInterval),
                },
                taelindorForest = new HuntersPatienceOverrideEntry
                {
                    rangedBonusPerTick = RaceBonusRuleData.HuntersPatience.RangedBonusPerTick.ToString(),
                    meleeBonusPerTick = RaceBonusRuleData.HuntersPatience.MeleeBonusPerTick.ToString(),
                    rangedBonusCap = RaceBonusRuleData.HuntersPatience.RangedBonusCap.ToString(),
                    meleeBonusCap = RaceBonusRuleData.HuntersPatience.MeleeBonusCap.ToString(),
                    updateInterval = F(RaceBonusRuleData.HuntersPatience.UpdateInterval),
                },
                sakuraDynasty = new KenseiEyeOverrideEntry
                {
                    meleeAttackPerStage = RaceBonusRuleData.KenseiEye.MeleeAttackPerStage.ToString(),
                    secondsPerStage = F(RaceBonusRuleData.KenseiEye.SecondsPerStage),
                    maxStages = RaceBonusRuleData.KenseiEye.MaxStages.ToString(),
                    updateInterval = F(RaceBonusRuleData.KenseiEye.UpdateInterval),
                },
                deepstoneHold = new OathcarvedOverrideEntry
                {
                    weaponStrengthPerDeath = RaceBonusRuleData.Oathcarved.WeaponStrengthPerDeath.ToString(),
                },
                ironLegion = new IronResolveOverrideEntry
                {
                    clampDurationSeconds = F(RaceBonusRuleData.IronResolve.ClampDurationSeconds),
                },
                ravenHost = new DeathcryOverrideEntry
                {
                    meleeAttackBonus = RaceBonusRuleData.Deathcry.MeleeAttackBonus.ToString(),
                    durationSeconds = F(RaceBonusRuleData.Deathcry.DurationSeconds),
                },
                sanguineCourt = new SanguineCourtOverrideEntry
                {
                    immuneToFlankMorale = RaceBonusRuleData.SanguineCourt.ImmuneToFlankMorale.ToString(),
                    immuneToTerror = RaceBonusRuleData.SanguineCourt.ImmuneToTerror.ToString(),
                    immuneToRetreatingAlliesMorale = RaceBonusRuleData.SanguineCourt.ImmuneToRetreatingAlliesMorale.ToString(),
                },
            };

            return JsonUtility.ToJson(file, true);
        }

        private static string F(float value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
