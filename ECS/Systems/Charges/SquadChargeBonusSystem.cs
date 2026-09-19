using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

partial struct SquadChargeBonusSystem : ISystem
{
    private ComponentLookup<InCombat> _inCombatLookup;
    private ComponentLookup<SquadEntity> _squadEntityLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        state.RequireForUpdate<SquadStatsData>();
        _inCombatLookup    = state.GetComponentLookup<InCombat>(true);
        _squadEntityLookup = state.GetComponentLookup<SquadEntity>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _inCombatLookup.Update(ref state);
        _squadEntityLookup.Update(ref state);

        var statsData = SystemAPI.GetSingleton<SquadStatsData>();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged)
            .AsParallelWriter();

        state.Dependency = new ApplyChargeBonusJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Ecb = ecb
        }.ScheduleParallel(state.Dependency);

        state.Dependency = new RemoveChargeBonusJob
        {
            DeltaTime          = SystemAPI.Time.DeltaTime,
            StatsBlob          = statsData.StatsBlob,
            InCombatLookup     = _inCombatLookup,
            SquadEntityLookup  = _squadEntityLookup,
            Ecb                = ecb
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithNone(typeof(ChargeBonus))]
[WithNone(typeof(ExhaustedTag))]
[WithNone(typeof(GarrisonGateSquadTag))]
partial struct ApplyChargeBonusJob : IJobEntity
{
    public float DeltaTime;
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute([ChunkIndexInQuery] int sortKey, ref SquadEntity squad, ref ChargeSquad chargeSquad)
    {
        chargeSquad.ChargeTime += DeltaTime;
        if (chargeSquad.ChargeTime < TabletopTavernConstants.TIME_REQUIRED_FOR_CHARGE_BONUS) return;

        chargeSquad.ChargeTime = 0;
        Ecb.AddComponent<ChargeBonus>(sortKey, squad.SelfEntity);
        Ecb.AddComponent<ApplyChargeBonusTag>(sortKey, squad.SelfEntity);
    }
}

[BurstCompile]
[WithNone(typeof(ChargeSquad))]
partial struct RemoveChargeBonusJob : IJobEntity
{
    public float DeltaTime;
    public BlobAssetReference<SquadStatsBlob> StatsBlob;
    [ReadOnly] public ComponentLookup<InCombat>    InCombatLookup;
    [ReadOnly] public ComponentLookup<SquadEntity> SquadEntityLookup;
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute([ChunkIndexInQuery] int sortKey, Entity entity,
        in SquadEntity squad, ref ChargeBonus chargeBonus)
    {
        chargeBonus.ChargeTime += DeltaTime;

        if (InCombatLookup.HasComponent(entity) && squad.TargetSquadEntity != Entity.Null &&
            SquadEntityLookup.HasComponent(squad.TargetSquadEntity))
        {
            SquadEntity targetSquad = SquadEntityLookup[squad.TargetSquadEntity];
            SquadStats targetSquadStats = StatsBlob.Value.GetStats(targetSquad.UnitName);
            if (targetSquadStats.SquadAttributes.AntiLarge)
            {
                chargeBonus.ChargeTime = 0;
                Ecb.RemoveComponent<ChargeBonus>(sortKey, squad.SelfEntity);
                Ecb.AddComponent<RemoveChargeBonusTag>(sortKey, squad.SelfEntity);
                return;
            }
        }

        if (chargeBonus.ChargeTime >= TabletopTavernConstants.TIME_TO_REMOVE_CHARGE_BONUS)
        {
            chargeBonus.ChargeTime = 0;
            Ecb.RemoveComponent<ChargeBonus>(sortKey, squad.SelfEntity);
            Ecb.AddComponent<RemoveChargeBonusTag>(sortKey, squad.SelfEntity);
        }
    }
}
