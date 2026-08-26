using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using TJ;

// Append only - ordinals are serialized as stats.unitType in every SquadData asset.
// Mage casts a spell instead of shooting: no ShootAttack, charges instead of ammo, and it
// fights in melee when engaged. See the unit type predicates in TabletopTavernConstants.
public enum UnitType { Melee, Ranged, Hybrid, Artillery, Structure, Mage }
public enum UnitCondition { None, InForest, InCombat, IsCharging, IsTerrified, InSwamp, IsExhausted, IsOutOfAmmo, GarrisonDefender, DefendersResolve }
public enum UnitStat { MeleeAttack, MeleeDefense, WeaponStrength, 
    Accuracy, Range, MissileStrength, HitPoints, None, Speed, 
    Armor, ChargeBonus, Leadership, Ammunition, ChargeImpactDamage }
public enum UnitSize { Infantry, Cavalry, Monstrous, SingleUnit, Artillery }
public enum UnitRarity { Common, Uncommon, Rare, Legendary }
public enum Team { Player, Enemy, Neutral }
public enum FormationType { Triangle, Radial, Box}
public enum GamePhase { SetUp, Deployment, Battle, PostGame }
public enum BattleLayoutType { Normal, PlayerEncircled, EnemyEncircled, EnemyDeferred }
public enum Race { IronLegion, Gruntkin, RavenHost, TaelindorForest, SanguineCourt, SakuraDynasty, DeepstoneHold, DrakosaurBrood, Special } //Olympian Phalanx
//Imperial Edict: First recruitment pack is free upon entering a town
//Endless Hordes: Heal all units by 20% of their max health after winning a battle
//Starlit Guidance: Grants the ability to spend 5<sprite name=GoldSprite> to reveal the outcome of the next dice roll during events
//Elite Ravagers: Sacking towns prestiges two random units
//Raise Dead: After every battle, gain 2 free [Tier 1] units
//Bushido Discipline: If army contains only Sakura Dynasty units, all units gain +20 [Leadership] and +4 [Melee Attack]
//Anvil's Second Strike: Gain a free reroll when collecting gear from chests
//A Feast for Beasts: After sacking a town, heal all units by 50% of their max health
public enum UnitName
{
    PeasantBowmen, LevySwordsmen, FieldPikemen,
    LandsknechtGreatswords, Arbalesters, MilanesePolearms,
    ImperialTemplars, DeepwoodRangers, BlackEagleHalberdiers,
    BorderlandRiders, HearthboundKnights, RoyalCavaliers,

    GoblinRabble, GoblinScrapShooters, DireWolves,
    OrcRavagers, OrcImpalers, OrcStalkers,
    BogmawTroll, ArmoredDirewolves, StonehewerGiant,
    StonegulletEnforcers, FleshshredderFanatics, Direriders,

    ThrallLevy, DriftwoodSkirmishers, SeawindSpears,
    Huskarls, Shieldmaidens, SkogarmadrArchers,
    Berserkers, Valkyries, VarangianGuard, SonsOfFenrir,
    Jomsvikings,

    SylvanArchers, ForestSpirits, AshwoodMilitia,
    AerindelGuard, Duskspears, MistweaverScouts, StarStriders,
    Veilpiercers, Treants, ElarionSentinels, SunspireBladelords,
    VeilkinDrakes,

    UndeadLevies, BoneclatterSpears, FeralHounds, GravestoneImps,
    DeathhavenFiends, Nightriders, BoneshardArchers, CorpseClaws,
    MistWraiths, BlackWardens, Bloodsworn, BloodswornKnights,

    AshigaruSpearmen, RoninWanderers, DaikyuCommoners,
    FootSamurai, NaginataOnna, KunoichiInfiltrators,
    DragonTachi, EmperorsArquebusiers, Hokoshu, KitsuneBlademasters,
    Oni,

    EisenmannRegiment, Ashguard, AngelsOfDeath,

    RiftpickLaborers, CrackshotCrossbows,
    Cragflayers, HelmwallDefenders, DrakefireRiflers, GrimfireGuns,
    ThunderhoofChargers, GrimazulAncients, GlyphstonePhalanx, StormForgedBattery, TheBulwark,
    AbyssalDelveknights, ForgewrathHammers,

    KaiserCannon, BanryuBombardiers, JoseonHwacha, DraugrBoltThrowers, Gobbopult, LorandelStarhurler,
    ArchersOfApollo,

    Meatgrinders, Siegeclaws, Shadelords, NecroticChimerae, GoldenSaru, EmeraldAncient,

    KoboldBrawlers, ScalebowKobolds, DireRaptors,
    VenomtailArchers, Brutes, Redhorns, RaptorRiders, TriceraPlatform,
    BlackDragon, Leviathans, StegoplateGuard, BloodCarnos,
    Kaiju, ObsidianScales,

    Gate,

    // Append only, and never without its SquadData asset in the same change: the stats blob is
    // sized by SquadStatsDictionary.Count but indexed by this ordinal, so a name with no asset
    // reads past the end of the BlobArray inside Burst rather than logging anything.
    BishopOfIron,
}
[System.Serializable] public struct SquadSpawnData {
    public int squadId;
    public UnitType unitType;
    public UnitName unitName;
    public Team Team;
    public List<float3> entityPositions;
    public List<int> unitIndiciesList;
    public int2 widthAndDepth;
    public quaternion squadRotation;
}
[System.Serializable] public struct SquadSaveData {
    public List<SquadSpawnData> squads;
    public List<SquadSpawnData> enemies;
}
[System.Serializable] public struct UnitFormationNoise {
    public UnitName unitName;
    [Range(0, 1)] public float noise;
}
[System.Serializable] public struct SpawnBox {
    public float3 min;
    public float3 max;
}
public struct GarrisonConcaveZone
{
    public float wallZ;          // Z of the flank wall segments
    public float middleZ;        // Z of the recessed middle section (= wallZ when flat)
    public float leftConnectorX; // X boundary between left flank and middle
    public float rightConnectorX;// X boundary between middle and right flank
    public float battleMinX;     // left edge of the battle zone
    public float battleMaxX;     // right edge of the battle zone
    public float battleMaxZ;     // back edge of the battle zone (enemy far side)
    public bool isFlat;            // true when inwardDepth <= 0 or wall too narrow for sections
    public bool isDiagonalLayout;  // true for village layout: narrow front wall + 45° outward side walls
    public bool isConvex;          // true for convex layout: middle forward at wallZ, flanks recessed to middleZ

    public bool IsInsideEnemyZone(float x, float z)
    {
        if (isFlat) return z >= wallZ;
        if (isDiagonalLayout)
        {
            if (z < wallZ) return false;
            float depth = z - wallZ;
            return x >= leftConnectorX - depth && x <= rightConnectorX + depth;
        }
        bool inMiddle = x > leftConnectorX && x < rightConnectorX;
        if (isConvex) return z >= (inMiddle ? wallZ : middleZ);
        return z >= (inMiddle ? middleZ : wallZ);
    }

    public float GetSectionZ(float x)
    {
        if (isFlat) return wallZ;
        bool inMiddle = x > leftConnectorX && x < rightConnectorX;
        if (isConvex) return inMiddle ? wallZ : middleZ;
        return inMiddle ? middleZ : wallZ;
    }

    public (float minX, float maxX) GetSectionXRange(float x)
    {
        if (isFlat) return (battleMinX, battleMaxX);
        if (x <= leftConnectorX) return (battleMinX, leftConnectorX);
        if (x >= rightConnectorX) return (rightConnectorX, battleMaxX);
        return (leftConnectorX, rightConnectorX);
    }
}
[System.Serializable] public struct SquadBounds
{
    public float3 bottomLeft;
    public float3 bottomRight;
    public float3 topLeft;
    public float3 topRight;
}
public struct UnitStatBonus
{
    public UnitStat UnitStat;
    public string BonusName;
    public float Value;
    public UnitStatBonus(UnitStat _unitStat, string _bonusName, float _value)
    {
        UnitStat = _unitStat;
        BonusName = _bonusName;
        Value = _value;
    }
}
public enum UnlockCondition { None, NotAvailableInDemo, DiscordExclusive, NewsletterExclusive, HeroCompletion }
public struct UnitAttributeBonus
{
    public UnitAttribute UnitAttribute;
    public string BonusName;
    public float Value;
    public UnitAttributeBonus(UnitAttribute _unitStat, string _bonusName, float _value)
    {
        UnitAttribute = _unitStat;
        BonusName = _bonusName;
        Value = _value;
    }
}
// Data-driven replacement for HeroBonusManager's hardcoded switch statements. FilterKind picks
// which fields of BonusCondition are meaningful - covers every condition shape actually used
// across the 16 heroes and the Sakura faction bonus: unconditional, rarity tier, an exact unit
// list, a small tag registry (see BonusTagRegistry), unit type/size lists, and enemy race.
public enum BonusFilterKind { Unconditional, RarityTier, UnitName, UnitTag, UnitType, UnitSize, EnemyRace }
public enum BonusMagnitudeKind { Flat, PercentOfCurrentValue }

[System.Serializable]
public struct BonusCondition
{
    public BonusFilterKind FilterKind;
    public UnitRarity RequiredRarityTier;
    public UnitName[] UnitNames;
    public string Tag;
    public UnitType[] UnitTypes;
    public UnitSize[] UnitSizes;
    public Race RequiredEnemyRace;

    public bool Matches(UnitName requestingUnit, SquadStats stats, Race enemyRace) => FilterKind switch
    {
        BonusFilterKind.Unconditional => true,
        BonusFilterKind.RarityTier => stats.RarityTier == RequiredRarityTier,
        BonusFilterKind.UnitName => UnitNames != null && System.Array.IndexOf(UnitNames, requestingUnit) >= 0,
        BonusFilterKind.UnitTag => BonusTagRegistry.Evaluate(Tag, requestingUnit, stats),
        BonusFilterKind.UnitType => UnitTypes != null && System.Array.IndexOf(UnitTypes, stats.unitType) >= 0,
        BonusFilterKind.UnitSize => UnitSizes != null && System.Array.IndexOf(UnitSizes, stats.unitSize) >= 0,
        BonusFilterKind.EnemyRace => enemyRace == RequiredEnemyRace,
        _ => false,
    };
}

// Small registry for filter kinds that need named logic rather than a plain value comparison
// (e.g. "is a goblin unit" spans several UnitNames, "is melee infantry" is a type+size compound).
// Takes SquadStats alongside UnitName so predicates never need to look up data themselves -
// keeps this usable from Components, which can't reference the main-assembly data registry.
public static class BonusTagRegistry
{
    private static readonly Dictionary<string, System.Func<UnitName, SquadStats, bool>> _tags = new()
    {
        { "Goblin", (unitName, stats) => TabletopTavernConstants.IsAGoblinUnit(unitName) },
        // Hybrids count: they hold the melee line, so Hero 5's Melee Infantry bonus applies to them.
        { "MeleeInfantry", (unitName, stats) => TabletopTavernConstants.FightsInMelee(stats.unitType) && stats.unitSize == UnitSize.Infantry },
    };

    public static bool Evaluate(string tag, UnitName unitName, SquadStats stats)
    {
        if (!string.IsNullOrEmpty(tag) && _tags.TryGetValue(tag, out var predicate)) return predicate(unitName, stats);
        Debug.LogWarning($"[BonusTagRegistry] Unknown tag '{tag}', treating as non-match.");
        return false;
    }
}

[System.Serializable]
public struct HeroStatBonusRule
{
    public int HeroID;
    public string LocalizationKey;
    public BonusCondition Condition;
    public UnitStat Stat;
    public BonusMagnitudeKind MagnitudeKind;
    public float Value;
}

[System.Serializable]
public struct HeroAttributeBonusRule
{
    public int HeroID;
    public string LocalizationKey;
    public BonusCondition Condition;
    public UnitAttribute GrantedAttribute;
}

// No per-unit BonusCondition: GetFactionBonus(UnitStat) itself takes no unit/army parameter today.
[System.Serializable]
public struct FactionBonusRule
{
    public Race Race;
    public string LocalizationKey;
    public UnitStat Stat;
    public BonusMagnitudeKind MagnitudeKind;
    public float Value;
}

public struct UnitStatValue
{
    public UnitStat unitStat; 
    public float Value;
    public UnitStatValue(UnitStat _unitStat, float _value)
    {
        unitStat = _unitStat;
        Value = _value;
    }
}
[System.Serializable]
public class UnitTier
{
    public UnitName unitName;
    public int tier; // 1 for low tier, 2 for mid-tier, 3 for high tier
}
public enum EngagementType { Skirmish, Horde };
public static class DataTypes
{
    public static float[] GetTierProbabilities(int reputation)
    {
        if (reputation <= 33) return new float[] { 0.7f, 0.25f, 0.05f, 0f };
        else if (reputation <= 66) return new float[] { 0.45f, 0.45f, 0.1f, 0f };
        else if (reputation <= 90) return new float[] { 0.1f, 0.7f, 0.2f, 0f };
        else if (reputation <= 100) return new float[] { 0.05f, 0.25f, 0.5f, 0.2f };
        else
        {
            Debug.LogError($"GetTierProbabilities: Invalid reputation value: {reputation}");
            return new float[] { };
        }
    }
    public static int GetFormationWidthFromUnitCount(int unitCount)
    {
        return unitCount switch
        {
            > 48 => 14,
            > 24 => 12,
            > 8 => 8,
            > 3 => 4,
            3 => 3,
            1 => 1,
            _ => 69,
        };
    }
}
