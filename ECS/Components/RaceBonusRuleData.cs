// Tunable magnitudes/caps/thresholds/durations for every race's battlefield passive.
// The base values below are a 1:1 transcription of the constants that used to live inline in
// each race-bonus ECS system (ECS/Systems/Squad/Bonuses/*, plus the SanguineCourt immunity
// checks in the Morale/Terror systems). Mod overrides (race_bonus_overrides.json, see
// RaceBonusOverrideLoader in the main assembly) sparsely patch these at load time.
//
// Lives in the Components assembly - not alongside the loader in the main assembly - so the
// Burst-adjacent Systems-assembly bonus systems can read it directly, exactly like
// HeroBonusRuleData. Only the *numbers* of each passive are data-driven; the trigger logic and
// shape of each mechanic stay hardcoded in their systems (a mod can rebalance a passive, not
// invent a new one).
public static class RaceBonusRuleData
{
    // Gruntkin - "Crashing Horde": each other living Gruntkin squad above HealthThreshold grants
    // WeaponStrengthPerStack, up to MaxStacks. Re-evaluated every UpdateInterval seconds.
    public struct CrashingHordeConfig
    {
        public int WeaponStrengthPerStack;
        public int MaxStacks;
        public float HealthThreshold; // fraction of max HP a squad must exceed to count
        public float UpdateInterval;
    }

    // DrakosaurBrood - "Pack Instinct": WeaponStrengthPerStack for each other Drakosaur squad
    // attacking the same target, up to MaxStacks. Re-evaluated every UpdateInterval seconds.
    public struct ApexHuntersConfig
    {
        public int WeaponStrengthPerStack;
        public int MaxStacks;
        public float UpdateInterval;
    }

    // TaelindorForest - "Hunter's Patience": while stationary, ranged squads accrue
    // RangedBonusPerTick Accuracy up to RangedBonusCap; melee squads accrue MeleeBonusPerTick
    // WeaponStrength up to MeleeBonusCap. Moving/charging zeroes the accrued bonus.
    public struct HuntersPatienceConfig
    {
        public int RangedBonusPerTick;
        public int MeleeBonusPerTick;
        public int RangedBonusCap;
        public int MeleeBonusCap;
        public float UpdateInterval;
    }

    // SakuraDynasty - "Kensei's Eye": while in continuous melee combat, +MeleeAttackPerStage every
    // SecondsPerStage, up to MaxStages. Leaving combat resets to zero.
    public struct KenseiEyeConfig
    {
        public int MeleeAttackPerStage;
        public float SecondsPerStage;
        public int MaxStages;
        public float UpdateInterval;
    }

    // DeepstoneHold - "Oathcarved": every unit death in the squad permanently grants surviving
    // units WeaponStrengthPerDeath. Stacks indefinitely.
    public struct OathcarvedConfig
    {
        public int WeaponStrengthPerDeath;
    }

    // IronLegion - "Iron Resolve": the first time a squad reaches Wavering, morale is pinned there
    // for ClampDurationSeconds. (The pin target itself is the Wavering state boundary and is not
    // moddable - changing it would break the "hold just above Broken" invariant.)
    public struct IronResolveConfig
    {
        public float ClampDurationSeconds;
    }

    // RavenHost - "Deathcry": when a RavenHost squad falls, surviving same-team RavenHost squads
    // gain MeleeAttackBonus for DurationSeconds (re-triggerable, refreshes the timer, no stacking).
    public struct DeathcryConfig
    {
        public int MeleeAttackBonus;
        public float DurationSeconds;
    }

    // SanguineCourt - immunities rather than magnitudes. Each toggle, when true (default),
    // suppresses one morale penalty for Sanguine Court squads. Set false to remove the immunity.
    public struct SanguineCourtConfig
    {
        public bool ImmuneToFlankMorale;
        public bool ImmuneToTerror;
        public bool ImmuneToRetreatingAlliesMorale;
    }

    public static CrashingHordeConfig CrashingHorde;
    public static ApexHuntersConfig ApexHunters;
    public static HuntersPatienceConfig HuntersPatience;
    public static KenseiEyeConfig KenseiEye;
    public static OathcarvedConfig Oathcarved;
    public static IronResolveConfig IronResolve;
    public static DeathcryConfig Deathcry;
    public static SanguineCourtConfig SanguineCourt;

    static RaceBonusRuleData()
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
        CrashingHorde = new CrashingHordeConfig { WeaponStrengthPerStack = 5, MaxStacks = 4, HealthThreshold = 0.5f, UpdateInterval = 1f };
        ApexHunters = new ApexHuntersConfig { WeaponStrengthPerStack = 8, MaxStacks = 2, UpdateInterval = 0.5f };
        HuntersPatience = new HuntersPatienceConfig { RangedBonusPerTick = 3, MeleeBonusPerTick = 2, RangedBonusCap = 20, MeleeBonusCap = 12, UpdateInterval = 1f };
        KenseiEye = new KenseiEyeConfig { MeleeAttackPerStage = 5, SecondsPerStage = 10f, MaxStages = 3, UpdateInterval = 1f };
        Oathcarved = new OathcarvedConfig { WeaponStrengthPerDeath = 2 };
        IronResolve = new IronResolveConfig { ClampDurationSeconds = 10f };
        Deathcry = new DeathcryConfig { MeleeAttackBonus = 20, DurationSeconds = 20f };
        SanguineCourt = new SanguineCourtConfig { ImmuneToFlankMorale = true, ImmuneToTerror = true, ImmuneToRetreatingAlliesMorale = true };
    }
}
