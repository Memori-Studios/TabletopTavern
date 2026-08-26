using System;
using System.Globalization;
using Memori.Localization;

// Builds a race's passive description with the current (possibly mod-overridden) RaceBonusRuleData
// values injected into the localized template's {0}/{1}/... placeholders, so the displayed numbers
// always match what the passive actually does - including after a race_bonus_overrides.json mod
// changes them. See RaceBonusOverrideLoader / RaceBonusRuleData for the values themselves.
public static class RacePassiveInfo
{
    // Returns the localized passive description for a race with its live numbers filled in.
    // Falls back to the raw localized string whenever the template has no {0} placeholder - a
    // locale not yet converted to placeholders, or Sanguine Court's placeholder-free immunity
    // text - so an un-migrated string simply shows as-is instead of erroring.
    public static string GetDescription(Race race)
    {
        string template = LocalizationManager.Instance.GetText(race.ToString() + "PassiveDescription");
        if (string.IsNullOrEmpty(template) || !template.Contains("{0}")) return template;

        try
        {
            return string.Format(template, GetArgs(race));
        }
        catch (FormatException)
        {
            // A malformed template (bad placeholder index, stray brace from a mod's localization
            // override, etc.) must never throw into the UI - show the raw string instead.
            return template;
        }
    }

    // The argument order for each race matches the placeholder order in its
    // <Race>PassiveDescription localization string. "Max" values are derived here rather than
    // stored, so they track the per-stack value and cap together.
    private static object[] GetArgs(Race race)
    {
        switch (race)
        {
            case Race.IronLegion:
                return new object[] { Fmt(RaceBonusRuleData.IronResolve.ClampDurationSeconds) };

            case Race.Gruntkin:
            {
                var c = RaceBonusRuleData.CrashingHorde;
                return new object[] { c.WeaponStrengthPerStack, Fmt(c.HealthThreshold * 100f), c.MaxStacks * c.WeaponStrengthPerStack };
            }

            case Race.RavenHost:
            {
                var c = RaceBonusRuleData.Deathcry;
                return new object[] { c.MeleeAttackBonus, Fmt(c.DurationSeconds) };
            }

            case Race.TaelindorForest:
            {
                var c = RaceBonusRuleData.HuntersPatience;
                return new object[] { c.RangedBonusCap, c.MeleeBonusCap };
            }

            case Race.SakuraDynasty:
            {
                var c = RaceBonusRuleData.KenseiEye;
                return new object[] { c.MeleeAttackPerStage, Fmt(c.SecondsPerStage), c.MaxStages * c.MeleeAttackPerStage };
            }

            case Race.DeepstoneHold:
                return new object[] { RaceBonusRuleData.Oathcarved.WeaponStrengthPerDeath };

            case Race.DrakosaurBrood:
            {
                var c = RaceBonusRuleData.ApexHunters;
                return new object[] { c.WeaponStrengthPerStack, c.MaxStacks * c.WeaponStrengthPerStack };
            }

            default:
                return Array.Empty<object>();
        }
    }

    // Whole numbers render without a trailing ".0"; a fractional value a mod might set (a 0.5s
    // tick, a 12.5s duration) keeps its decimals. Invariant culture keeps the separator a ".".
    private static string Fmt(float value)
    {
        return value.ToString(value % 1f == 0f ? "0" : "0.##", CultureInfo.InvariantCulture);
    }
}
