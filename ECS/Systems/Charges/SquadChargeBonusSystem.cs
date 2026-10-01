using Unity.Burst;
using Unity.Entities;

/// <summary>
/// Times out the two things a landed charge leaves on a squad: the stat bonus and the Weary rest.
/// </summary>
partial struct SquadChargeBonusSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged)
            .AsParallelWriter();

        state.Dependency = new RemoveChargeBonusJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Ecb       = ecb
        }.ScheduleParallel(state.Dependency);

        state.Dependency = new WearyJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Ecb       = ecb
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
partial struct RemoveChargeBonusJob : IJobEntity
{
    public float DeltaTime;
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute([ChunkIndexInQuery] int sortKey, Entity entity, ref ChargeBonus chargeBonus)
    {
        chargeBonus.ChargeTime += DeltaTime;
        if (chargeBonus.ChargeTime < TabletopTavernConstants.TIME_TO_REMOVE_CHARGE_BONUS) return;

        Ecb.RemoveComponent<ChargeBonus>(sortKey, entity);
        Ecb.AddComponent<RemoveChargeBonusTag>(sortKey, entity);
    }
}

[BurstCompile]
partial struct WearyJob : IJobEntity
{
    public float DeltaTime;
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute([ChunkIndexInQuery] int sortKey, Entity entity, ref WearyTag weary)
    {
        weary.Remaining -= DeltaTime;
        if (weary.Remaining <= 0f) Ecb.RemoveComponent<WearyTag>(sortKey, entity);
    }
}
