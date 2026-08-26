using Unity.Entities;

[System.Serializable]
public struct SquadStats
{
    public UnitName unitName;
    public UnitType unitType;
    public UnitSize unitSize;
    public int MeleeAttack;
    public int MeleeDefense;
    public int HitPointsPerUnit;
    public int WeaponStrength;
    public float Speed;
    public float Leadership;
    public int Armor;

    // Charge
    public int ChargeBonus;
    public int ChargeImactDamage;
    public int ChargeCount;

    // Ranged
    public float BaseRange;
    public float attackAccuracy;
    public int MissileStrength;

    //Artillery
    public int ExplosionDamage;
    public float ExplosionRange;
    public float ExplosionForce;

    public UnitRarity RarityTier;
    public int baseUnitCount;
    public float attackCooldown;
    public int Ammunition;
    public float rateOfFire;

    public SquadAttributes SquadAttributes;
}
[System.Serializable]
public struct SquadAttributes
{
    public bool None;
    public bool StandardShields;
    public bool ArmorPiercing;
    public bool AntiInfantry;
    public bool AntiLarge;
    public bool Terrifying;
    public bool Stalwart;
    public bool Outrider;
    public bool SwampCreature;
    public bool ForestDweller;
    public bool ChickenFlight;
    public bool Ethereal;
    public bool BloodFrenzy;
    public bool Rage;
    public bool Emblazing;
    public bool Unstoppable;
    public bool HeavyShields;
    public bool ThrowingAxes;
    public bool ArmorSundering;
    public bool MonsterSlayer;
    public bool ForgefuryTempering;
    public bool FlamingAmmo;
    public bool DragonsHoard;
    public bool BackStabbers;
    public bool ThickScales;
    public bool ShotDiscipline;
    public bool Overdraw;
    public bool SteadyAim;
    public bool Demolisher;
    public bool PowderReserves;
    public bool DeepQuivers;
}

// Append only - ordinals are serialized into save data as SquadToLoad.PrestigeTrait.
public enum UnitAttribute { None, ArmorPiercing, AntiInfantry, AntiLarge, Infantry, Large, StandardShields, Armored, Terrifying, Stalwart, Ethereal, SwampCreature, ForestDweller, Outrider, ChickenFlight, BloodFrenzy, Rage, Emblazing, Unstoppable, HeavyShields, ThrowingAxes, ArmorSundering, MonsterSlayer, ForgefuryTempering, TowerShields, FlamingAmmo, IsOnFire, DragonsHoard, BackStabbers, ThickScales, ShotDiscipline, Overdraw, SteadyAim, Demolisher, PowderReserves, DeepQuivers }
[System.Serializable]
public struct UnitAttributeSerialized
{
    public bool None;
    public bool ArmorPiercing;
    public bool AntiInfantry;
    public bool AntiLarge;
    public bool Infantry;
    public bool Large;
    public bool Armored;
}
public struct SquadStatsData : IComponentData
{
    public BlobAssetReference<SquadStatsBlob> StatsBlob;
}

public struct SquadStatsBlob
{
    public BlobArray<SquadStats> Stats;

    public SquadStats GetStats(UnitName unitName)
    {
        return Stats[(int)unitName];
    }
}
