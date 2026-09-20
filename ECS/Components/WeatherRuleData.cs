// Tunable magnitudes for the three battlefield weathers. The base values below are a 1:1
// transcription of the constants that used to live in TabletopTavernConstants and inline in
// BattlefieldBonusSystem / AutoResolveBattleManager. Mod overrides (weather_overrides.json, see
// WeatherOverrideLoader in the main assembly) sparsely patch these at load time.
//
// Lives in the Components assembly so BattlefieldBonusSystem (Systems assembly) can read it, like
// RaceBonusRuleData. Only the numbers are data-driven; which effect each weather applies stays
// hardcoded in its system branch.
public static class WeatherRuleData
{
    // Rain: large units move at LargeUnitSpeedModifier x speed and, while RemovesChargeBonus is
    // true, get no charge bonus. The auto-resolve simulation instead scales every squad's accuracy
    // by AutoResolveAccuracyModifier.
    public struct RainConfig
    {
        public float LargeUnitSpeedModifier;
        public bool RemovesChargeBonus;
        public float AutoResolveAccuracyModifier;
    }

    // Snow: every squad's max and current morale shift by MoralePenalty (negative lowers it).
    public struct SnowConfig
    {
        public float MoralePenalty;
    }

    // Fog: ranged units keep AccuracyModifier x accuracy and RangeModifier x range.
    public struct FogConfig
    {
        public float AccuracyModifier;
        public float RangeModifier;
    }

    public static RainConfig Rain;
    public static SnowConfig Snow;
    public static FogConfig Fog;

    static WeatherRuleData()
    {
        ResetToDefaults();
    }

    // Called once per TabletopTavernData.ApplyModOverrides() before the mod loop, so a removed or
    // disabled mod cleanly reverts to the shipped values instead of leaking last session's overrides.
    public static void ClearOverrides()
    {
        ResetToDefaults();
    }

    private static void ResetToDefaults()
    {
        Rain = new RainConfig { LargeUnitSpeedModifier = 0.5f, RemovesChargeBonus = true, AutoResolveAccuracyModifier = 0.5f };
        Snow = new SnowConfig { MoralePenalty = -10f };
        Fog = new FogConfig { AccuracyModifier = 0.5f, RangeModifier = 0.5f };
    }
}
