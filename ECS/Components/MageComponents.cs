using Unity.Entities;
using Unity.Mathematics;

// Global namespace to sit alongside RangedSquad / MeleeSquad / ArtillerySquad in
// GreyCompanyComponents.cs, which every squad system references unqualified.

#region Unit level

/// <summary>
/// Per-unit cast state for UnitType.Mage, the caster's counterpart to ShootAttack.
/// A mage has no ShootAttack at all, so this is where its reach and cadence live - and
/// therefore what prestige Range writes to. Seeded in UnitSetUpSystem from SquadStats.BaseRange
/// and SquadStats.rateOfFire.
/// </summary>
public struct MageCast : IComponentData
{
    /// <summary>Cast range in world units. From SquadStats.BaseRange, same source ShootAttack.Range uses.</summary>
    public float Range;

    /// <summary>Seconds between casts. From SquadStats.rateOfFire, reused rather than adding a
    /// SquadStats field: it already means "seconds between shots" for artillery and structures,
    /// and SquadStatsOverrideLoader already exposes it to mods.</summary>
    public float Cooldown;

    /// <summary>Counts down to zero, then a cast fires and it resets to Cooldown. Starts at
    /// Cooldown so a mage does not open the battle with an instant cast the way archers now do -
    /// the wind-up is the tell that makes a mage reactable.</summary>
    public float Timer;
}

#endregion

#region Squad level

/// <summary>
/// How a mage squad picks what to cast on. Set from the mage's SpellData in EntityWatcher, because
/// a SpellData is a managed ScriptableObject that ECS cannot read. Nine factions will eventually
/// carry nine different spells - buffs, debuffs and nukes - so a single hardcoded targeting rule
/// would be wrong for most of them.
/// </summary>
public enum MageTargetPriority
{
    /// <summary>Closest enemy squad. The safe default and what an unset SpellData resolves to.</summary>
    NearestEnemy,

    /// <summary>Enemy squad with the most models. An AoE wants a fat target, so this is what makes
    /// an offensive mage look like it is aiming rather than just firing at whatever is closest.</summary>
    DensestEnemyCluster,

    /// <summary>Friendly squad nearest the enemy line. For buff casters (Gruntkin's Frenzy Brew).</summary>
    FriendlyNearestEnemy,

    /// <summary>Enemy squad currently charging, falling back to nearest. For interrupts
    /// (Drakosaur's Primal Quake).</summary>
    ChargingEnemy,
}

/// <summary>
/// Squad-level marker for a mage squad, the counterpart to RangedSquad / MeleeSquad / ArtillerySquad.
/// Deliberately NOT RangedSquad: keeping mages out of that component is what stops them inheriting
/// skirmishing, fire modes, archer targeting and the enemy archer AI by accident.
/// </summary>
public struct MageSquad : IComponentData
{
    /// <summary>Mirrored from the squad's unit MageCast.Range by MageSquadRangeSystem, exactly as
    /// RangedSquadRangeSystem mirrors ShootAttack.Range into RangedSquad.AttackRange. That is what
    /// carries prestige Range up to the squad level where targeting reads it.</summary>
    public float AttackRange;

    public MageTargetPriority TargetPriority;
}

#endregion

#region Event stream

/// <summary>
/// One requested cast, appended by MageCastSystem and drained once per frame by EntityWatcher, which
/// instantiates the managed ActiveSpell prefab. Same singleton-buffer idiom as
/// BattlefieldBonusAppliedBufferElement and SquadDamageBufferElement; the buffer is created in
/// BattleManager.StartBattle().
///
/// This exists because the effect lives in a managed ScriptableObject and a MonoBehaviour prefab,
/// neither of which an ISystem can touch.
/// </summary>
public struct MageCastRequestBufferElement : IBufferElementData
{
    /// <summary>Caster's squad id. Carried through to DamageBufferElement.DamageSourceSquadId so a
    /// mage kill is credited rather than landing on the "no killer" sentinel.</summary>
    public int SquadId;

    /// <summary>Caster's unit name, so the drain can resolve which SpellData to instantiate without
    /// having to map a squad id back to a unit.</summary>
    public UnitName UnitName;

    /// <summary>Caster's team. Carried through to DamageBufferElement.TeamOfSource, which is what
    /// makes an enemy mage damage the player instead of its own army.</summary>
    public Team TeamOfSource;

    /// <summary>World position to cast at, resolved at request time.</summary>
    public float3 Position;

    /// <summary>Target squad for SpellTargetingType.Squad spells, so the effect tracks the squad
    /// through warmup. Entity.Null for World-targeted spells.</summary>
    public Entity TargetSquadEntity;
}

#endregion
