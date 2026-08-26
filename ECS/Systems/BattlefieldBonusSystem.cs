using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectDawn.Navigation;
using TJ.Morale;

partial struct BattlefieldBonusSystem : ISystem
{
    // Remove tag lookups (read-only — only checking presence)
    private ComponentLookup<RemoveBattlefieldBonusRain> _removeRainLookup;
    private ComponentLookup<RemoveBattlefieldBonusSnow> _removeSnowLookup;
    private ComponentLookup<RemoveBattlefieldBonusFog> _removeFogLookup;
    private ComponentLookup<RemoveChargeBonusTag> _removeChargeLookup;
    private ComponentLookup<RemoveSwampTag> _removeSwampLookup;
    private ComponentLookup<RemoveForestTag> _removeForestLookup;
    private ComponentLookup<RemoveBloodFrenzyTag> _removeBloodFrenzyLookup;
    private ComponentLookup<RemoveRageTag> _removeRageLookup;
    private ComponentLookup<RemoveEmblazingTag> _removeEmblazingLookup;
    // Squad state lookups (read-only)
    private ComponentLookup<InSwampTag> _inSwampLookup;
    private ComponentLookup<InRainTag> _inRainLookup;
    private ComponentLookup<InSnowTag> _inSnowLookup;
    private ComponentLookup<InForestTag> _inForestLookup;
    private ComponentLookup<RallyingTag> _rallyingLookup;
    private ComponentLookup<ChargeEmpoweredTag> _chargeEmpoweredLookup;
    private ComponentLookup<LargeTag> _largeTagLookup;
    private ComponentLookup<LocalTransform> _existsLookup;
    // Unit + squad component writes
    private ComponentLookup<AgentLocomotion> _agentLocomotionLookup;
    private ComponentLookup<MeleeAttack> _meleeAttackLookup;
    private ComponentLookup<MeleeDefense> _meleeDefenseLookup;
    private ComponentLookup<ShootAttack> _shootAttackLookup;
    private ComponentLookup<ArmoredTag> _armoredTagLookup;
    private ComponentLookup<MoraleComponent> _moraleComponentLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SquadStatsData>();
        _removeRainLookup        = state.GetComponentLookup<RemoveBattlefieldBonusRain>(true);
        _removeSnowLookup        = state.GetComponentLookup<RemoveBattlefieldBonusSnow>(true);
        _removeFogLookup         = state.GetComponentLookup<RemoveBattlefieldBonusFog>(true);
        _removeChargeLookup      = state.GetComponentLookup<RemoveChargeBonusTag>(true);
        _removeSwampLookup       = state.GetComponentLookup<RemoveSwampTag>(true);
        _removeForestLookup      = state.GetComponentLookup<RemoveForestTag>(true);
        _removeBloodFrenzyLookup = state.GetComponentLookup<RemoveBloodFrenzyTag>(true);
        _removeRageLookup        = state.GetComponentLookup<RemoveRageTag>(true);
        _removeEmblazingLookup   = state.GetComponentLookup<RemoveEmblazingTag>(true);
        _inSwampLookup           = state.GetComponentLookup<InSwampTag>(true);
        _inRainLookup            = state.GetComponentLookup<InRainTag>(true);
        _inSnowLookup            = state.GetComponentLookup<InSnowTag>(true);
        _inForestLookup          = state.GetComponentLookup<InForestTag>(true);
        _rallyingLookup          = state.GetComponentLookup<RallyingTag>(true);
        _chargeEmpoweredLookup   = state.GetComponentLookup<ChargeEmpoweredTag>(true);
        _largeTagLookup          = state.GetComponentLookup<LargeTag>(true);
        _existsLookup            = state.GetComponentLookup<LocalTransform>(true);
        _agentLocomotionLookup   = state.GetComponentLookup<AgentLocomotion>(false);
        _meleeAttackLookup       = state.GetComponentLookup<MeleeAttack>(false);
        _meleeDefenseLookup      = state.GetComponentLookup<MeleeDefense>(false);
        _shootAttackLookup       = state.GetComponentLookup<ShootAttack>(false);
        _armoredTagLookup        = state.GetComponentLookup<ArmoredTag>(false);
        _moraleComponentLookup   = state.GetComponentLookup<MoraleComponent>(false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _removeRainLookup.Update(ref state);
        _removeSnowLookup.Update(ref state);
        _removeFogLookup.Update(ref state);
        _removeChargeLookup.Update(ref state);
        _removeSwampLookup.Update(ref state);
        _removeForestLookup.Update(ref state);
        _removeBloodFrenzyLookup.Update(ref state);
        _removeRageLookup.Update(ref state);
        _removeEmblazingLookup.Update(ref state);
        _inSwampLookup.Update(ref state);
        _inRainLookup.Update(ref state);
        _inSnowLookup.Update(ref state);
        _inForestLookup.Update(ref state);
        _rallyingLookup.Update(ref state);
        _chargeEmpoweredLookup.Update(ref state);
        _largeTagLookup.Update(ref state);
        _existsLookup.Update(ref state);
        _agentLocomotionLookup.Update(ref state);
        _meleeAttackLookup.Update(ref state);
        _meleeDefenseLookup.Update(ref state);
        _shootAttackLookup.Update(ref state);
        _armoredTagLookup.Update(ref state);
        _moraleComponentLookup.Update(ref state);

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged)
            .AsParallelWriter();

        state.Dependency = new BattlefieldBonusJob
        {
            ElapsedTime             = SystemAPI.Time.ElapsedTime,
            RemoveRainLookup        = _removeRainLookup,
            RemoveSnowLookup        = _removeSnowLookup,
            RemoveFogLookup         = _removeFogLookup,
            RemoveChargeLookup      = _removeChargeLookup,
            RemoveSwampLookup       = _removeSwampLookup,
            RemoveForestLookup      = _removeForestLookup,
            RemoveBloodFrenzyLookup = _removeBloodFrenzyLookup,
            RemoveRageLookup        = _removeRageLookup,
            RemoveEmblazingLookup   = _removeEmblazingLookup,
            InSwampLookup           = _inSwampLookup,
            InRainLookup            = _inRainLookup,
            InSnowLookup            = _inSnowLookup,
            InForestLookup          = _inForestLookup,
            RallyingLookup          = _rallyingLookup,
            ChargeEmpoweredLookup   = _chargeEmpoweredLookup,
            LargeTagLookup          = _largeTagLookup,
            ExistsLookup            = _existsLookup,
            AgentLocomotionLookup   = _agentLocomotionLookup,
            MeleeAttackLookup       = _meleeAttackLookup,
            MeleeDefenseLookup      = _meleeDefenseLookup,
            ShootAttackLookup       = _shootAttackLookup,
            ArmoredTagLookup        = _armoredTagLookup,
            MoraleComponentLookup   = _moraleComponentLookup,
            Ecb                     = ecb
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
partial struct BattlefieldBonusJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<RemoveBattlefieldBonusRain>  RemoveRainLookup;
    [ReadOnly] public ComponentLookup<RemoveBattlefieldBonusSnow>  RemoveSnowLookup;
    [ReadOnly] public ComponentLookup<RemoveBattlefieldBonusFog>   RemoveFogLookup;
    [ReadOnly] public ComponentLookup<RemoveChargeBonusTag>        RemoveChargeLookup;
    [ReadOnly] public ComponentLookup<RemoveSwampTag>              RemoveSwampLookup;
    [ReadOnly] public ComponentLookup<RemoveForestTag>             RemoveForestLookup;
    [ReadOnly] public ComponentLookup<RemoveBloodFrenzyTag>        RemoveBloodFrenzyLookup;
    [ReadOnly] public ComponentLookup<RemoveRageTag>               RemoveRageLookup;
    [ReadOnly] public ComponentLookup<RemoveEmblazingTag>          RemoveEmblazingLookup;
    [ReadOnly] public ComponentLookup<InSwampTag>                  InSwampLookup;
    [ReadOnly] public ComponentLookup<InRainTag>                   InRainLookup;
    [ReadOnly] public ComponentLookup<InSnowTag>                   InSnowLookup;
    [ReadOnly] public ComponentLookup<InForestTag>                 InForestLookup;
    [ReadOnly] public ComponentLookup<RallyingTag>                 RallyingLookup;
    [ReadOnly] public ComponentLookup<ChargeEmpoweredTag>          ChargeEmpoweredLookup;
    [ReadOnly] public ComponentLookup<LargeTag>                    LargeTagLookup;
    [ReadOnly] public ComponentLookup<LocalTransform>              ExistsLookup;
    [NativeDisableParallelForRestriction] public ComponentLookup<AgentLocomotion> AgentLocomotionLookup;
    [NativeDisableParallelForRestriction] public ComponentLookup<MeleeAttack>     MeleeAttackLookup;
    [NativeDisableParallelForRestriction] public ComponentLookup<MeleeDefense>    MeleeDefenseLookup;
    [NativeDisableParallelForRestriction] public ComponentLookup<ShootAttack>     ShootAttackLookup;
    [NativeDisableParallelForRestriction] public ComponentLookup<ArmoredTag>      ArmoredTagLookup;
    [NativeDisableParallelForRestriction] public ComponentLookup<MoraleComponent> MoraleComponentLookup;
    [ReadOnly] public double ElapsedTime;
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute([ChunkIndexInQuery] int sortKey, Entity entity,
        in SquadMovementComponent squadMovement,
        in SquadEntity squad,
        DynamicBuffer<BattlefieldBonusBufferElement> bonusBuffer,
        DynamicBuffer<EntityReferenceBufferElement> entityBuffer)
    {
        bool hasRemoveRain        = RemoveRainLookup.HasComponent(entity);
        bool hasRemoveSnow        = RemoveSnowLookup.HasComponent(entity);
        bool hasRemoveFog         = RemoveFogLookup.HasComponent(entity);
        bool hasRemoveCharge      = RemoveChargeLookup.HasComponent(entity);
        bool hasRemoveSwamp       = RemoveSwampLookup.HasComponent(entity);
        bool hasRemoveForest      = RemoveForestLookup.HasComponent(entity);
        bool hasRemoveBloodFrenzy = RemoveBloodFrenzyLookup.HasComponent(entity);
        bool hasRemoveRage        = RemoveRageLookup.HasComponent(entity);
        bool hasRemoveEmblazing   = RemoveEmblazingLookup.HasComponent(entity);

        for (int i = 0; i < bonusBuffer.Length; i++)
        {
            BattlefieldBonus bonus = bonusBuffer[i].Value;
            if (bonus.TargetedUnit != 0 && bonus.TargetedUnit != squad.SquadId) continue;

            if (hasRemoveRain && bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Rain)
            {
                Ecb.RemoveComponent<InRainTag>(sortKey, entity);
                for (int j = 0; j < entityBuffer.Length; j++)
                {
                    Entity unitEntity = entityBuffer[j].Entity;
                    if (!ExistsLookup.HasComponent(unitEntity)) continue;
                    var loc = AgentLocomotionLookup[unitEntity];
                    loc.Speed /= TabletopTavernConstants.RAIN_SPEED_MODIFIER;
                    loc.Acceleration /= TabletopTavernConstants.RAIN_SPEED_MODIFIER;
                    AgentLocomotionLookup[unitEntity] = loc;
                    Ecb.RemoveComponent<InRainTag>(sortKey, unitEntity);
                }
                bonusBuffer.RemoveAt(i--);
                continue;
            }

            if (hasRemoveSnow && bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Snow)
            {
                var morale = MoraleComponentLookup[entity];
                morale.MaxMorale -= TabletopTavernConstants.SNOW_MORALE_PENALTY;
                morale.CurrentMorale -= TabletopTavernConstants.SNOW_MORALE_PENALTY;
                MoraleComponentLookup[entity] = morale;
                Ecb.RemoveComponent<InSnowTag>(sortKey, entity);
                bonusBuffer.RemoveAt(i--);
                continue;
            }

            if (hasRemoveFog && bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Fog)
            {
                for (int j = 0; j < entityBuffer.Length; j++)
                {
                    Entity unitEntity = entityBuffer[j].Entity;
                    if (!ExistsLookup.HasComponent(unitEntity)) continue;
                    switch (bonus.UnitStat)
                    {
                        case UnitStat.Accuracy:
                            if (ShootAttackLookup.HasComponent(unitEntity))
                            {
                                var sa = ShootAttackLookup[unitEntity];
                                sa.Accuracy += (int)bonus.Value;
                                ShootAttackLookup[unitEntity] = sa;
                            }
                            break;
                        case UnitStat.Range:
                            if (ShootAttackLookup.HasComponent(unitEntity))
                            {
                                var sa = ShootAttackLookup[unitEntity];
                                sa.Range += (int)bonus.Value;
                                ShootAttackLookup[unitEntity] = sa;
                            }
                            break;
                    }
                }
                bonusBuffer.RemoveAt(i--);
                continue;
            }

            if (hasRemoveCharge && bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.ChargeBonus)
            {
                if (bonus.Applied)
                {
                    for (int j = 0; j < entityBuffer.Length; j++)
                    {
                        Entity unitEntity = entityBuffer[j].Entity;
                        if (!ExistsLookup.HasComponent(unitEntity)) continue;
                        switch (bonus.UnitStat)
                        {
                            case UnitStat.WeaponStrength:
                                if (MeleeAttackLookup.HasComponent(unitEntity))
                                {
                                    var ma = MeleeAttackLookup[unitEntity];
                                    ma.WeaponStrength -= (int)bonus.Value;
                                    MeleeAttackLookup[unitEntity] = ma;
                                }
                                break;
                            case UnitStat.MeleeAttack:
                                if (MeleeAttackLookup.HasComponent(unitEntity))
                                {
                                    var ma = MeleeAttackLookup[unitEntity];
                                    ma.MeleeAttackValue -= (int)bonus.Value;
                                    MeleeAttackLookup[unitEntity] = ma;
                                }
                                break;
                        }
                    }
                }
                bonusBuffer.RemoveAt(i--);
                continue;
            }

            // Only the buffer element is dropped here. InSwampTag/InForestTag removal is unconditional
            // and lives at the end of Execute - see the note there for why it cannot be tied to
            // finding a matching element.
            if (hasRemoveSwamp && bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Swamp)
            {
                bonusBuffer.RemoveAt(i--);
                continue;
            }

            if (hasRemoveForest && bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Forest)
            {
                bonusBuffer.RemoveAt(i--);
                continue;
            }

            if (hasRemoveBloodFrenzy && bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.BloodFrenzy)
            {
                Ecb.RemoveComponent<RemoveBloodFrenzyTag>(sortKey, entity);
                for (int j = 0; j < entityBuffer.Length; j++)
                {
                    Entity unitEntity = entityBuffer[j].Entity;
                    if (!ExistsLookup.HasComponent(unitEntity)) continue;
                    if (bonus.UnitStat == UnitStat.WeaponStrength && MeleeAttackLookup.HasComponent(unitEntity))
                    {
                        var ma = MeleeAttackLookup[unitEntity];
                        ma.WeaponStrength -= (int)bonus.Value;
                        MeleeAttackLookup[unitEntity] = ma;
                    }
                    else if (bonus.UnitStat == UnitStat.Speed && AgentLocomotionLookup.HasComponent(unitEntity))
                    {
                        var loc = AgentLocomotionLookup[unitEntity];
                        loc.Speed -= bonus.Value;
                        loc.Acceleration -= bonus.Value;
                        AgentLocomotionLookup[unitEntity] = loc;
                    }
                }
                bonusBuffer.RemoveAt(i--);
                continue;
            }

            if (hasRemoveRage && bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Rage)
            {
                Ecb.RemoveComponent<RemoveRageTag>(sortKey, entity);
                for (int j = 0; j < entityBuffer.Length; j++)
                {
                    Entity unitEntity = entityBuffer[j].Entity;
                    if (!ExistsLookup.HasComponent(unitEntity)) continue;
                    if (bonus.UnitStat == UnitStat.MeleeAttack && MeleeAttackLookup.HasComponent(unitEntity))
                    {
                        var ma = MeleeAttackLookup[unitEntity];
                        ma.WeaponStrength -= (int)bonus.Value;
                        MeleeAttackLookup[unitEntity] = ma;
                    }
                }
                bonusBuffer.RemoveAt(i--);
                continue;
            }

            if (hasRemoveEmblazing && bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Emblazing)
            {
                Ecb.RemoveComponent<RemoveEmblazingTag>(sortKey, entity);
                for (int j = 0; j < entityBuffer.Length; j++)
                {
                    Entity unitEntity = entityBuffer[j].Entity;
                    if (!ExistsLookup.HasComponent(unitEntity)) continue;
                    if (bonus.UnitStat == UnitStat.Armor && ArmoredTagLookup.HasComponent(unitEntity))
                    {
                        var at = ArmoredTagLookup[unitEntity];
                        at.ArmorMitigation -= bonus.Value;
                        ArmoredTagLookup[unitEntity] = at;
                    }
                }
                bonusBuffer.RemoveAt(i--);
                continue;
            }

            if (!bonus.Applied)
            {
                if (bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Forest)
                {
                    if (bonus.TargetedUnit == squad.SquadId && !InSwampLookup.HasComponent(entity))
                    {
                        Ecb.AddComponent<InForestTag>(sortKey, entity);
                        Ecb.AddComponent<RemoveChargeBonusTag>(sortKey, entity);
                        for (int j = 0; j < entityBuffer.Length; j++)
                        {
                            Entity unitEntity = entityBuffer[j].Entity;
                            if (ExistsLookup.HasComponent(unitEntity))
                                Ecb.AddComponent<InForestTag>(sortKey, unitEntity);
                        }
                        bonus.Applied = true;
                        bonusBuffer.RemoveAt(i--);
                        bonusBuffer.Add(new BattlefieldBonusBufferElement { Value = bonus });
                        continue;
                    }
                }
                else if (bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Swamp)
                {
                    if (bonus.TargetedUnit == squad.SquadId && !InSwampLookup.HasComponent(entity))
                    {
                        Ecb.AddComponent<InSwampTag>(sortKey, entity);
                        Ecb.AddComponent<RemoveChargeBonusTag>(sortKey, entity);
                        for (int j = 0; j < entityBuffer.Length; j++)
                        {
                            Entity unitEntity = entityBuffer[j].Entity;
                            if (ExistsLookup.HasComponent(unitEntity))
                                Ecb.AddComponent<InSwampTag>(sortKey, unitEntity);
                        }
                        bonus.Applied = true;
                        bonusBuffer.RemoveAt(i--);
                        bonusBuffer.Add(new BattlefieldBonusBufferElement { Value = bonus });
                        continue;
                    }
                }
                else if (bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Rain)
                {
                    bonus.Applied = true;
                    bonusBuffer.RemoveAt(i--);
                    if (!InRainLookup.HasComponent(entity) && LargeTagLookup.HasComponent(entity))
                    {
                        Ecb.AddComponent<InRainTag>(sortKey, entity);
                        Ecb.AddComponent<RemoveChargeBonusTag>(sortKey, entity);
                        for (int j = 0; j < entityBuffer.Length; j++)
                        {
                            Entity unitEntity = entityBuffer[j].Entity;
                            if (!ExistsLookup.HasComponent(unitEntity)) continue;
                            Ecb.AddComponent<InRainTag>(sortKey, unitEntity);
                            var loc = AgentLocomotionLookup[unitEntity];
                            loc.Speed *= TabletopTavernConstants.RAIN_SPEED_MODIFIER;
                            loc.Acceleration *= TabletopTavernConstants.RAIN_SPEED_MODIFIER;
                            AgentLocomotionLookup[unitEntity] = loc;
                        }
                        bonusBuffer.Add(new BattlefieldBonusBufferElement { Value = bonus });
                    }
                }
                else if (bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Snow)
                {
                    bonus.Applied = true;
                    bonusBuffer.RemoveAt(i--);
                    bonusBuffer.Add(new BattlefieldBonusBufferElement { Value = bonus });
                    if (!InSnowLookup.HasComponent(entity))
                    {
                        Ecb.AddComponent<InSnowTag>(sortKey, entity);
                        var morale = MoraleComponentLookup[entity];
                        morale.MaxMorale += TabletopTavernConstants.SNOW_MORALE_PENALTY;
                        morale.CurrentMorale += TabletopTavernConstants.SNOW_MORALE_PENALTY;
                        MoraleComponentLookup[entity] = morale;
                    }
                }
                else if (bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.LesserMoraleSpell)
                {
                    // Squad-level, like Snow above - morale lives on the squad entity, not its units,
                    // so this cannot be a case in the per-unit UnitStat switch below. The tag carries a
                    // regen rate that MoraleUpdateJob adds each frame; removal is handled in the
                    // distance/duration block further down and needs no reversal arithmetic.
                    bonus.Applied = true;
                    bonusBuffer.RemoveAt(i--);
                    bonusBuffer.Add(new BattlefieldBonusBufferElement { Value = bonus });
                    if (!RallyingLookup.HasComponent(entity))
                    {
                        Ecb.AddComponent(sortKey, entity, new RallyingTag { MoralePerSecond = bonus.Value });
                    }
                }
                else if (bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.RallyTheBanners)
                {
                    // Squad-level like LesserMoraleSpell above. The tag is inert until the squad actually
                    // charges - SquadChargeBonusApplicationSystem reads it then, so there is no stat to
                    // reverse here and removal in the distance/duration block just drops the tag.
                    bonus.Applied = true;
                    bonusBuffer.RemoveAt(i--);
                    bonusBuffer.Add(new BattlefieldBonusBufferElement { Value = bonus });
                    if (!ChargeEmpoweredLookup.HasComponent(entity))
                    {
                        Ecb.AddComponent(sortKey, entity, new ChargeEmpoweredTag { BonusImpact = bonus.Value });
                    }
                }
                else if (bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Fog)
                {
                    bonus.Applied = true;
                    bonusBuffer.RemoveAt(i--);
                    for (int j = 0; j < entityBuffer.Length; j++)
                    {
                        Entity unitEntity = entityBuffer[j].Entity;
                        if (!ExistsLookup.HasComponent(unitEntity)) continue;
                        switch (bonus.UnitStat)
                        {
                            case UnitStat.Accuracy:
                                if (ShootAttackLookup.HasComponent(unitEntity))
                                {
                                    var sa = ShootAttackLookup[unitEntity];
                                    int reduction = sa.Accuracy - (int)(sa.Accuracy * 0.5f);
                                    bonus.Value = reduction;
                                    sa.Accuracy -= reduction;
                                    ShootAttackLookup[unitEntity] = sa;
                                }
                                break;
                            case UnitStat.Range:
                                if (ShootAttackLookup.HasComponent(unitEntity))
                                {
                                    var sa = ShootAttackLookup[unitEntity];
                                    int reduction = (int)sa.Range - (int)(sa.Range * 0.5f);
                                    bonus.Value = reduction;
                                    sa.Range -= reduction;
                                    ShootAttackLookup[unitEntity] = sa;
                                }
                                break;
                        }
                    }
                    bonusBuffer.Add(new BattlefieldBonusBufferElement { Value = bonus });
                }
                else
                {
                    bonus.Applied = true;
                    bonusBuffer.RemoveAt(i--);
                    bonusBuffer.Add(new BattlefieldBonusBufferElement { Value = bonus });
                    for (int j = 0; j < entityBuffer.Length; j++)
                    {
                        Entity unitEntity = entityBuffer[j].Entity;
                        if (!ExistsLookup.HasComponent(unitEntity)) continue;
                        switch (bonus.UnitStat)
                        {
                            case UnitStat.MeleeAttack:
                                if (MeleeAttackLookup.HasComponent(unitEntity))
                                {
                                    var ma = MeleeAttackLookup[unitEntity];
                                    ma.MeleeAttackValue += (int)bonus.Value;
                                    MeleeAttackLookup[unitEntity] = ma;
                                }
                                break;
                            case UnitStat.MeleeDefense:
                                if (MeleeDefenseLookup.HasComponent(unitEntity))
                                {
                                    var md = MeleeDefenseLookup[unitEntity];
                                    md.Value += (int)bonus.Value;
                                    MeleeDefenseLookup[unitEntity] = md;
                                }
                                break;
                            case UnitStat.WeaponStrength:
                                if (MeleeAttackLookup.HasComponent(unitEntity))
                                {
                                    var ma = MeleeAttackLookup[unitEntity];
                                    ma.WeaponStrength += (int)bonus.Value;
                                    MeleeAttackLookup[unitEntity] = ma;
                                }
                                break;
                            case UnitStat.Speed:
                                if (AgentLocomotionLookup.HasComponent(unitEntity))
                                {
                                    var loc = AgentLocomotionLookup[unitEntity];
                                    loc.Speed += bonus.Value;
                                    loc.Acceleration += bonus.Value;
                                    AgentLocomotionLookup[unitEntity] = loc;
                                }
                                break;
                            case UnitStat.Accuracy:
                                if (ShootAttackLookup.HasComponent(unitEntity))
                                {
                                    var sa = ShootAttackLookup[unitEntity];
                                    sa.Accuracy += (int)bonus.Value;
                                    ShootAttackLookup[unitEntity] = sa;
                                }
                                break;
                            case UnitStat.Range:
                                if (ShootAttackLookup.HasComponent(unitEntity))
                                {
                                    var sa = ShootAttackLookup[unitEntity];
                                    sa.Range += (int)bonus.Value;
                                    ShootAttackLookup[unitEntity] = sa;
                                }
                                break;
                            case UnitStat.Armor:
                                if (ArmoredTagLookup.HasComponent(unitEntity))
                                {
                                    var at = ArmoredTagLookup[unitEntity];
                                    at.ArmorMitigation += bonus.Value;
                                    ArmoredTagLookup[unitEntity] = at;
                                }
                                break;
                        }
                    }
                }
            }

            // Distance- or duration-based removal
            float distance = math.distance(squadMovement.SquadCenter, bonus.OriginationPoint);
            bool expired = bonus.ExpiresAtTime > 0 && ElapsedTime >= bonus.ExpiresAtTime;
            if (distance - 5 > bonus.Range || expired)
            {
                if (bonus.Applied)
                {
                    if (bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Forest && InForestLookup.HasComponent(entity))
                        Ecb.RemoveComponent<InForestTag>(sortKey, entity);

                    // Squad left the radius or the spell expired - drop the regen tag. The per-unit
                    // loop below no-ops for this bonus since there is no UnitStat.Leadership case.
                    if (bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.LesserMoraleSpell)
                        Ecb.RemoveComponent<RallyingTag>(sortKey, entity);

                    // Same shape as the regen tag above - no UnitStat.ChargeBonus case exists in the
                    // per-unit loop below, so dropping the tag is the whole reversal. Any charge bonus
                    // already granted stays until RemoveChargeBonusTag strips it, which is correct:
                    // the banner empowered that charge, and expiry should not claw it back mid-impact.
                    if (bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.RallyTheBanners)
                        Ecb.RemoveComponent<ChargeEmpoweredTag>(sortKey, entity);

                    for (int j = 0; j < entityBuffer.Length; j++)
                    {
                        Entity unitEntity = entityBuffer[j].Entity;
                        if (!ExistsLookup.HasComponent(unitEntity)) continue;

                        if (bonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Forest && InForestLookup.HasComponent(entity))
                        {
                            Ecb.RemoveComponent<InForestTag>(sortKey, unitEntity);
                            continue;
                        }

                        switch (bonus.UnitStat)
                        {
                            case UnitStat.MeleeAttack:
                                if (MeleeAttackLookup.HasComponent(unitEntity))
                                {
                                    var ma = MeleeAttackLookup[unitEntity];
                                    ma.MeleeAttackValue -= (int)bonus.Value;
                                    MeleeAttackLookup[unitEntity] = ma;
                                }
                                break;
                            case UnitStat.MeleeDefense:
                                if (MeleeDefenseLookup.HasComponent(unitEntity))
                                {
                                    var md = MeleeDefenseLookup[unitEntity];
                                    md.Value -= (int)bonus.Value;
                                    MeleeDefenseLookup[unitEntity] = md;
                                }
                                break;
                            case UnitStat.WeaponStrength:
                                if (MeleeAttackLookup.HasComponent(unitEntity))
                                {
                                    var ma = MeleeAttackLookup[unitEntity];
                                    ma.WeaponStrength -= (int)bonus.Value;
                                    MeleeAttackLookup[unitEntity] = ma;
                                }
                                break;
                            case UnitStat.Accuracy:
                                if (ShootAttackLookup.HasComponent(unitEntity))
                                {
                                    var sa = ShootAttackLookup[unitEntity];
                                    sa.Accuracy -= (int)bonus.Value;
                                    ShootAttackLookup[unitEntity] = sa;
                                }
                                break;
                            case UnitStat.Range:
                                if (ShootAttackLookup.HasComponent(unitEntity))
                                {
                                    var sa = ShootAttackLookup[unitEntity];
                                    sa.Range -= (int)bonus.Value;
                                    ShootAttackLookup[unitEntity] = sa;
                                }
                                break;
                            // Mirror the Speed/Armor apply cases above so timed debuffs (Sunder,
                            // Shieldwall's -speed) reverse when they expire or the squad leaves range.
                            // Without these the reduction leaked permanently - Emblazing dodged it via
                            // its own dedicated RemoveEmblazing path, so nothing hit this gap before.
                            case UnitStat.Speed:
                                if (AgentLocomotionLookup.HasComponent(unitEntity))
                                {
                                    var loc = AgentLocomotionLookup[unitEntity];
                                    loc.Speed -= bonus.Value;
                                    loc.Acceleration -= bonus.Value;
                                    AgentLocomotionLookup[unitEntity] = loc;
                                }
                                break;
                            case UnitStat.Armor:
                                if (ArmoredTagLookup.HasComponent(unitEntity))
                                {
                                    var at = ArmoredTagLookup[unitEntity];
                                    at.ArmorMitigation -= bonus.Value;
                                    ArmoredTagLookup[unitEntity] = at;
                                }
                                break;
                        }
                    }
                }
                bonusBuffer.RemoveAt(i--);
            }
        }

        #region Biome tag removal
        // Deliberately NOT gated on finding a matching element in bonusBuffer. RemoveSwampTag /
        // RemoveForestTag are consumed every frame they exist (just below), but the apply side is a
        // 1-2 frame pipeline: BattlefieldBiomeDetector -> ApplyBiomeBonusTag -> BiomeApplicationSystem
        // adds the element -> this system adds the tag via the EndSimulation ECB. A squad that leaves
        // the biome inside that window had its remove signal swallowed by the element not being in the
        // buffer yet, stranding InSwampTag permanently: the detector has already flipped detectedBiome
        // by then and never re-issues. Removing a component the entity does not have is a no-op, so
        // running this whenever the remove tag is present is safe and idempotent.
        if (hasRemoveSwamp)
        {
            Ecb.RemoveComponent<InSwampTag>(sortKey, entity);
            for (int j = 0; j < entityBuffer.Length; j++)
            {
                Entity unitEntity = entityBuffer[j].Entity;
                if (ExistsLookup.HasComponent(unitEntity))
                    Ecb.RemoveComponent<InSwampTag>(sortKey, unitEntity);
            }
        }

        if (hasRemoveForest)
        {
            Ecb.RemoveComponent<InForestTag>(sortKey, entity);
            for (int j = 0; j < entityBuffer.Length; j++)
            {
                Entity unitEntity = entityBuffer[j].Entity;
                if (ExistsLookup.HasComponent(unitEntity))
                    Ecb.RemoveComponent<InForestTag>(sortKey, unitEntity);
            }
        }
        #endregion

        if (hasRemoveRain)   Ecb.RemoveComponent<RemoveBattlefieldBonusRain>(sortKey, entity);
        if (hasRemoveFog)    Ecb.RemoveComponent<RemoveBattlefieldBonusFog>(sortKey, entity);
        if (hasRemoveSnow)   Ecb.RemoveComponent<RemoveBattlefieldBonusSnow>(sortKey, entity);
        if (hasRemoveCharge) Ecb.RemoveComponent<RemoveChargeBonusTag>(sortKey, entity);
        if (hasRemoveSwamp)  Ecb.RemoveComponent<RemoveSwampTag>(sortKey, entity);
        if (hasRemoveForest) Ecb.RemoveComponent<RemoveForestTag>(sortKey, entity);
    }
}
