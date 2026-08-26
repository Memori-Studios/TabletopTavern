using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct PlayerSquad : IComponentData { }
public struct EnemySquad : IComponentData { }
public struct DeploymentPhase : IComponentData { }
public struct BattlePhase : IComponentData { }
public struct CameraPositionComponent : IComponentData { public float3 Position; }
public struct BattleHasStarted : IComponentData { }
public struct BattleHasNotEnded : IComponentData { }
public struct BalanceOfPower : IComponentData { public float EnemyMaxHealth; public float EnemyCurrentHealth; public float PlayerMaxHealth; public float PlayerCurrentHealth; }
public struct ArmyLossesTriggeredPlayer : IComponentData { }
public struct ArmyLossesTriggeredEnemy : IComponentData { }
public struct ArmyLossesPenaltyTag: IComponentData, IEnableableComponent { }

public struct CampaignSaveDataHolder : IComponentData { public bool IsCustomBattle; public GearIDsSerialized Gear; public int ActiveHeroID; public Race PlayerHeroRace; public Race EnemyRace; public bool OnlySakuraUnits; }
public struct MainCamera : IComponentData { }
public struct NeedsToBeProcessed : IComponentData { public float Delay; }

/// <summary> Ranged fire mode </summary>
public enum RangedFireMode { Volley, FireAtWill }
public struct RangedFireModeSquadComponent : IComponentData { public RangedFireMode FireMode; public bool SwitchRequested; }
public struct RangedFireModeUnitComponent : IComponentData { public RangedFireMode FireMode; }

/// <summary> Shielded stance </summary>
public enum ShieldedStance { None, Balanced, Defensive }
public struct ShieldedStanceSquadComponent : IComponentData { public ShieldedStance Stance; public bool SwitchRequested; }
public struct ShieldedStanceUnitComponent : IComponentData { public ShieldedStance Stance; }
public struct DefensiveStanceTag : IComponentData, IEnableableComponent { }

public struct MeleeSquad : IComponentData { }
public struct RangedSquad : IComponentData, IEnableableComponent
{
    public float AttackRange;
}
public struct ArtillerySquad : IComponentData { }

/// <summary>
/// The squad's remaining shots, lifted out of RangedSquad so a mage's cast charges can run through
/// the same spend-and-deplete pipeline (RangedSquadRemoveAmmunitionSystem -> SquadRanOutOfAmmoSystem)
/// without a mage having to carry RangedSquad - that component is what pulls a squad into archer
/// targeting, skirmishing and fire modes, none of which apply to a caster.
///
/// Added by EntityWatcher for shooters, artillery and mages alike. Removed by SquadRanOutOfAmmoSystem
/// on depletion, which is what stops the deplete check re-firing forever on an already-converted squad.
/// </summary>
public struct SquadAmmunition : IComponentData { public int Value; }
public struct AmmuntionSpent : IComponentData { public int squadId; }
public struct RanOutOfAmmoTag : IComponentData { }
public enum RangedToMeleeSwitchType { Melee, Ranged }
public struct SwitchToMeleeTag : IComponentData { public RangedToMeleeSwitchType SwitchType; }
public struct RangedSquadSkirmishTag : IComponentData { public float Delay; }
public struct BaseSeparationWeight : IComponentData { public float Value; }
public struct SetDestination : IComponentData
{
    public float3 destinationPosition;
    public float3 squadPosition;
    public float delayRemaining;
}
public struct RotateUnit : IComponentData, IEnableableComponent
{
    public quaternion targetRotation;
}
public struct MeleeUnitTag : IComponentData { }
public struct RangedUnitTag : IComponentData { }
public struct InCombat : IComponentData { }
public struct InMeleeRange : IComponentData, IEnableableComponent { }
public struct DisengageFromCombat : IComponentData, IEnableableComponent { public Entity newTargetSquad; }
public struct CausesTerrorTag : IComponentData { }
public struct StalwartTag : IComponentData { }
public struct ExhaustedTag : IComponentData { }
public struct ChargeSquad : IComponentData { public float ChargeTime; }
public struct ChargeBonus : IComponentData { public float ChargeTime; }
public struct ApplyChargeBonusTag : IComponentData { }
public struct ChargeBonusReductionTag : IComponentData { public float ReductionPercent; }
public struct MonsterTag : IComponentData { public float KnockbackRange; public int KnockbackInitialDamage; }
public struct RetreatingUnit : IComponentData, IEnableableComponent { }

#region Knockback
public struct ApplyKnockbackOnContact : IComponentData, IEnableableComponent { public float LifeTime; }
public struct Explosion : IComponentData, IEnableableComponent
{
    public float3 ExplosionPosition;
    public int KnockbackSquadID;
    public Team KnockbackSquadTeam;
    public float KnockbackRange;
    public float KnockbackForce;
    public int KnockbackInitialDamage;
    public float Delay;
}
public struct RequestExplosion : IComponentData 
{
    public int KnockbackSquadID; 
    public Team KnockbackSquadTeam; 
    public float KnockbackRange; 
    public float KnockbackForce;
    public int KnockbackInitialDamage;
}
public struct ResistKnockbackTag : IComponentData { }
public struct ThrowUnit : IComponentData { 
    public float3 HittingEntityLocation; 
    public float3 InitialLocation; 
    public float Force; 
    public int Damage; 
    public int HittingEntitySquad; 
    public Team HittingEntityTeam;
    public float RemainingTime; 
    public float TotalTime;
}
#endregion
public struct RemoveChargeBonusTag : IComponentData { }
/// <summary>
/// Rally the Banners. On a squad, adds BonusImpact to the charge bonus SquadChargeBonusApplicationSystem
/// grants, and exempts that squad from the forest/swamp/rain suppression that otherwise cancels it outright.
/// Added and removed by BattlefieldBonusSystem, so the aura's radius and duration govern its lifetime.
/// </summary>
public struct ChargeEmpoweredTag : IComponentData { public float BonusImpact; }
public struct StartChargeTag : IComponentData { }
public struct PreviousSquadCommandComponent : IComponentData { public SquadCommand Command; }
public struct SquadCommandChangedTag : IComponentData { public SquadCommand OldCommand; public SquadCommand NewCommand; }
public struct RangedMeleeConverter : IComponentData, IEnableableComponent 
{ 
    public bool SwitchToMelee; 
    public Entity BowEntity; 
    public Entity SwordEntity; 
}
public struct FormationNeedsToBeProcessed : IComponentData { public int indexRemoved; public float3 squadPosition; }
public struct FormationEngagedInCombat : IComponentData { public Entity EngagementEntity; public bool WasCharging; }
public struct OnFormationsCollide : IComponentData { public float3 Position; }
public struct OnExplosionShake : IComponentData { public float3 Position; }
public struct FormationEngagedInRangedCombat : IComponentData { }
public struct FormationShapeChanged : IComponentData { }
public struct FindTargets : IComponentData { }
public struct SquadMoveOverrideTag : IComponentData { public float DistanceGoal; }
public struct CancelSquadMoveOverrideTag : IComponentData { }
public struct OpponentRanAwayTag : IComponentData { public float DazedTime; }

// Tracks whether a charging melee squad is making net closing progress toward its target.
// Added/removed on demand by EnemyMeleePursuitWatchdogSystem; present only during an active pursuit.
public struct PursuitProgress : IComponentData
{
    public Entity TrackedTarget;     // target change -> reset progress
    public float  BestDistance;      // min center-to-center distance achieved this pursuit
    public float  TimeSinceImproved; // seconds accumulated without net closing
    public double LastSampleTime;    // throttle bookkeeping
}

// Cooldown blacklist of abandoned kiters, stored on the PURSUER (not global) so different
// enemy squads can still legitimately target the same player squad. Self-prunes on expiry.
[InternalBufferCapacity(4)]
public struct TargetBlacklistElement : IBufferElementData
{
    public Entity Target;
    public double ExpireTime;        // SystemAPI.Time.ElapsedTime when this entry lapses
}
public struct BreakSquadTag : IComponentData { }
public struct BrokenSquadTag : IComponentData { }
public struct CeaseFireTag : IComponentData, IEnableableComponent { }
public struct CeaseFireRequestedTag : IComponentData, IEnableableComponent { }

#region HitPoints
public struct EntityTeam : IComponentData { public Team Value; }
public struct MaxHitPoints : IComponentData { public int Value; }
public enum DamageType { Physical, Magical, Healing}
public enum DamageSource { Melee, Ranged }
public enum DamageAttributes { None, ArmorPiercing, AntiInfantry, AntiLarge, ArmorPiercingAntiInfantry, ArmorPiercingAntiLarge }
public struct LargeTag : IComponentData { }
public struct InfantryTag : IComponentData { }
public struct MonsterousSquadTag : IComponentData { }
public struct ArmoredTag : IComponentData { public float ArmorMitigation; }
public struct MeleeAttack : IComponentData {
    public float timer;
    public float timerMax;
    /// <summary>
    /// The amount of damage a melee hit will inflict
    /// </summary>
    public int WeaponStrength;
    /// <summary>
    /// The chance to inflict a successful hit in combat
    /// </summary>
    public int MeleeAttackValue;
    public bool onAttack;
}
public struct MeleeDefense : IComponentData { public int Value; }
public struct SquadStateComponent : IComponentData { 
    public int MaxHealthValue; 
    public int CurrentHealthValue;
    public int ChargesRemaining;
    public bool IsFlanked; 
}
public struct ArmorPiercingTag : IComponentData { }
public struct AntiInfantryTag : IComponentData { }
public struct AntiLargeTag : IComponentData { }
public struct BracedTag : IComponentData, IEnableableComponent { }
public struct BackStabbersTag : IComponentData { }
/// <summary> Steady Aim: waives the Fire-at-Will accuracy penalty in RangedUnitAttackSystem. </summary>
public struct SteadyAimTag : IComponentData { }

public struct Shield : IComponentData { public Entity shieldEntity; public float ShieldBlockChance; }
public struct Cavalry : IComponentData { public Entity riderEntity; public UnitName unitName; }
public struct ArtilleryUnit : IComponentData { public int SquadID; public int ExplosionDamage; public float ExplosionRange; public float ExplosionForce; }
public struct GarrisonGateUnit : IComponentData { public Entity squadEntity; }
public struct GarrisonGateSquadTag : IComponentData { public int GateIndex; }
public struct GarrisonDefenderComponent : IComponentData { public int GateIndex; }
public struct DefendersResolveComponent : IComponentData { }
public struct SetUpGarrisonGateSquad : IComponentData, IEnableableComponent { }
public struct GateFiringPoints : IComponentData { public Unity.Mathematics.float3 PointA; public Unity.Mathematics.float3 PointB; public bool UsePointA; public Unity.Mathematics.float2 ForwardXZ; }
public struct BlockedArrowTag : IComponentData { }

public struct PhysicalDamageMultiplier : IComponentData { public float Value; }
public struct MagicalDamageMultiplier : IComponentData { public float Value; }
// Fraction of non-artillery ranged damage actually received (e.g. 0.25 = 75% reduction)
public struct MissileResistance : IComponentData { public float DamageMultiplier; }

//https://www.youtube.com/live/SWXYpWtJZ5k?si=324vvhPSOYOtWox2&t=4513 
// if this component exists, the apply damage system will be applied in a completely different way
[WriteGroup(typeof(Health))]
public struct IgnoreDamageMultiplicationTag : IComponentData {}
#endregion


public struct SimulationRate : IComponentData {
    public bool UseFixedRate;
    public float FixedTimeStep;
    public float TimeScale;
    public float UnscaledDeltaTime;
    public bool Update;
}
public struct UnitRemovedFromSquad : IComponentData
{
    public Entity Entity;    // The unit entity to be removed from the squad
    public int SquadId;      // The ID of the squad it belongs to
    public bool DeleteCorpse;
    public int KilledBySquadId;
}
// public struct DamageRecievedFrom : IComponentData { public int SquadId; }
public struct SquadKillTag : IComponentData { public int SquadId; }
public enum SquadCommand { None, Move, Attack, Halt, Withdraw, Retreat, HaltAndFreeze }
public enum UnitState { Spawn, Idle, Moving, OnCharge, Charge, OnEngage, OnEngageRanged, InCombat, OnDisengage, Broken, Dead }
public struct SquadEntityGameObjectsProcessingNeeded : IComponentData { }
public struct IssueSquadCommand : IComponentData
{
    public SquadCommand SquadCommand;
    public Entity NewTargetSquad;
}
public struct WaitingForCommand : IComponentData, IEnableableComponent { }
public struct JustFollowingOrders : IComponentData, IEnableableComponent { }
public struct RecalculatePositionsForUnitsCharging : IComponentData { }
public struct ArcherRangeUpdated : IComponentData { }
public struct TargetSquadDestroyed : IComponentData { }
public struct SquadDestroyed : IComponentData { public int SquadId; }

public struct HaltCommandTag : IComponentData { public bool DropTarget; public bool FreezePosition; }
public struct DeleteSquadTag : IComponentData { }
public struct WithdrawSquadTag : IComponentData { }
public struct WithdrawCompleteTag : IComponentData { }
public struct UnitPrestigeSetUpTag : IComponentData { public int PrestigeLevel; public UnitAttribute GrantedTrait; }
public struct UnitStatsSetUpTag : IComponentData { public int HealthOverride; }
public struct UnitParentEntityTag : IComponentData { public Entity parentSquadEntity; }

public struct SpellEntity : IComponentData {
    public Entity Entity;
    public DamageBufferElement DamageBufferElement;
    public float SpellRadius;
    public float3 SpellPosition;
    public bool IsOneOff;
    public float SpellForce;
    public float RemainingDuration;
    public Entity TargetSquadEntity; // Entity.Null unless lock-on, in which case SpellSystem re-resolves live position each tick
    // Persistent-spell throttle (Healing Grove HoT, Venomous Bite DoT). TickInterval == 0 keeps the
    // original every-frame application; > 0 applies the effect once per that many seconds. TickTimer
    // counts down between applications and is ignored entirely when IsOneOff is true.
    public float TickInterval;
    public float TickTimer;
}
// public struct UnitHitBySpell : IComponentData { public float3 SpellPosition; public float SpellForce; public float3 InitHitLocation;}
public struct BattleOver : IComponentData {public bool PlayerWon; }

#region Buffers
[InternalBufferCapacity(1)]
[System.Serializable] public struct SFXBufferElement : IBufferElementData {
    public UnitName UnitName;
    public Memori.Audio.SFXEntityType SFXEntityType;
    public float MaxDistance;
}
[System.Serializable] public struct BloodBufferElement : IBufferElementData {
    public float3 Position;
    public bool IsExplosion;
}
[System.Serializable] public struct DustCloudBufferElement : IBufferElementData {
    public float3 Position;
}
[System.Serializable] public struct SquadDamageBufferElement : IBufferElementData {
    public int SquadId;
    public int DamageAmount;
}

[InternalBufferCapacity(1)]
[System.Serializable] public struct DamageBufferElement : IBufferElementData {
    public DamageType DamageType;
    public DamageSource DamageSource;
    public Team TeamOfSource;
    public int AttackStrength;
    public int DamageSourceSquadId;
    public DamageAttributes DamageAttributes;
    public bool FlankAttack;
    public bool Flaming;
    public bool SourceIsArtillery;
}
#endregion
public struct CavalryFlankingTag : IComponentData { }
public struct Unit : IComponentData {
    public Team Team;
    public UnitType unitType;
    public UnitName unitName;
    public UnitState unitState;
    public int squadId;
    public Entity squadEntity;
}
public struct UnitPosition : IComponentData
{
    public int unitIndex;
    public Entity supportingEntity;
}
public struct DestroyEntityTag : IComponentData, IEnableableComponent { }
public struct IsFlanking : IComponentData, IEnableableComponent  { public Entity TargetFlankedSquadEntity; }
public struct DealFlankingDamageTag : IComponentData, IEnableableComponent { }
public struct TakingFlankingDamage : IComponentData, IEnableableComponent { public float LifeTime; public bool RecentlyTookDamage; }
public struct FlamingRangedAttackTag : IComponentData, IEnableableComponent { }
public struct TakingFireDamage: IComponentData, IEnableableComponent { public float LifeTime; public bool RecentlyTookDamage; }
public struct HasTakenDamage : IComponentData, IEnableableComponent { }
public struct SquadDestination : IComponentData {
    public float3 DestinationPosition;
    public quaternion DestinationRotation;
    public int TargetSquadId;
    public int2 WidthAndDepth;
}
public enum QueuedOrderType : byte { Move = 0, Attack = 1 }
public enum QueuedOrderStatus : byte { Pending = 0, InProgress = 1 }
public struct QueuedOrder : IBufferElementData
{
    public QueuedOrderType Type;
    public QueuedOrderStatus Status;
    public float3 Goal;
    public quaternion Rotation;
    public int TargetSquadId;
    public int2 WidthAndDepth;
    public static QueuedOrder Move(float3 goal, quaternion rotation)
    {
        return new QueuedOrder
        {
            Type = QueuedOrderType.Move,
            Status = QueuedOrderStatus.Pending,
            Goal = goal,
            Rotation = rotation,
            TargetSquadId = 0
        };
    }
    public static QueuedOrder Attack(int targetSquadId)
    {
        return new QueuedOrder
        {
            Type = QueuedOrderType.Attack,
            Status = QueuedOrderStatus.Pending,
            Goal = default,
            Rotation = default,
            TargetSquadId = targetSquadId
        };
    }
}
public struct CompleteQueuedOrderTag : IComponentData, IEnableableComponent { }
public struct UpdatedSquadUnitCount : IComponentData { public int SquadId; public int2 UnitCount; }