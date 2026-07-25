using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
using ProjectDawn.Navigation;
using System;

public class BattlefieldBonusAuthoring : MonoBehaviour
{
    public BattlefieldBonus BattlefieldBonus;
    public float TimerMax = 1f;

    public class Baker : Baker<BattlefieldBonusAuthoring> 
    {
        public override void Bake(BattlefieldBonusAuthoring authoring) {
            Entity entity = GetEntity(TransformUsageFlags.WorldSpace);

            AddComponent(entity, new BattlefieldBonusApplicator {
                BattlefieldBonus = authoring.BattlefieldBonus,
                TimerMax = authoring.TimerMax
            });
        }
    }
}
public enum BattlefieldBonusEnum { None, StatueOfBoromid, Rain, Blacksmith, GoblinBarracks, GoblinCamp, ShrineToTheAllMother, Watchtower, ChargeBonus, Forest, Swamp, BloodFrenzy, Rage, Emblazing, Fog, Snow, CrashingHorde, Deathcry, HuntersPatience, KenseiEye, Oathcarved, ApexHunters, 
LesserWeaponStrengthSpell,
Slayer,
LesserWindSpell,
// Append only - these ordinals are serialized by BattlefieldBonusAuthoring and packed into a ulong
// bitmask by BattlefieldBonusAppliedDetectionSystem (1ul << (int)bonusEnum, so a 64 value ceiling).
LesserMoraleSpell,
// Generic per-stat spell bonus. Routes to the default per-unit UnitStat switch in
// BattlefieldBonusSystem; a multi-stat SpellData emits one applicator with this enum per stat pair.
SpellStatBonus
}
public struct BattlefieldBonusApplicator : IComponentData
{
    public BattlefieldBonus BattlefieldBonus;
    public float Timer;
    public float TimerMax;
    // 0 = permanent (world fixtures like shrines/statues never set this). >0 = a relative
    // lifetime in seconds; BattlefieldBonusApplicationSystem converts it to an absolute
    // BattlefieldBonus.ExpiresAtTime once, then self-destroys when that deadline passes.
    public float Lifetime;
}
public struct InForestTag : IComponentData { }
public struct InSwampTag : IComponentData { }
public struct InRainTag : IComponentData { }
public struct InSnowTag : IComponentData { }

public struct RageApplicatorTag : IComponentData { }
public struct RageActiveTag : IComponentData { }
public struct RemoveRageTag : IComponentData { }

public struct SlayerApplicatorTag : IComponentData { }
public struct SlayerActiveTag : IComponentData { }

public struct BloodFrenzyApplicatorTag : IComponentData { }
public struct BloodFrenzyActiveTag : IComponentData { }
public struct RemoveBloodFrenzyTag : IComponentData { }

public struct EmblazerTag : IComponentData { }
public struct ArmorSunderedTag : IComponentData { }
public struct EmblazingApplicatorTag : IComponentData { }
public struct RemoveEmblazingTag : IComponentData { }

public struct RemoveBattlefieldBonusRain : IComponentData { }
public struct RemoveBattlefieldBonusSnow : IComponentData { }
public struct RemoveBattlefieldBonusFog : IComponentData { }

public struct RemoveSwampTag : IComponentData { }
public struct RemoveForestTag : IComponentData { }

public struct ApplyBiomeBonusTag : IComponentData { public BattlefieldBonusEnum BattlefieldBonusEnum; public Guid Guid; }

[System.Serializable] public struct BattlefieldBonus : IComponentData
{
    public UnitStat UnitStat;
    public BattlefieldBonusEnum BattlefieldBonusEnum;
    public Team Team;
    public float Value;
    public Guid Guid;
    public float3 OriginationPoint;
    public float Range;
    public bool Applied;
    public int TargetedUnit;
    // 0 = never expires (default for world fixtures). Otherwise the World.Time.ElapsedTime
    // value at which BattlefieldBonusSystem force-removes this bonus regardless of distance.
    public double ExpiresAtTime;
}
[InternalBufferCapacity(8)] // Optional: set the internal buffer capacity
[System.Serializable] public struct BattlefieldBonusBufferElement : IBufferElementData {
    public BattlefieldBonus Value;
}
public struct BattlefieldBonusSeenMask : IComponentData {
    public ulong SeenMask;
}
[System.Serializable] public struct BattlefieldBonusAppliedBufferElement : IBufferElementData {
    public int SquadId;
    public BattlefieldBonusEnum BonusEnum;
    public UnitStat UnitStat;
    public float Value;
}
