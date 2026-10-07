using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
partial struct ChargingUnitDustSystem : ISystem
{
    private float _timer;
    private Random _random;
    private const float Interval = 0.5f;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DustCloudBufferElement>();
        _random = Random.CreateFromIndex(0);
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _timer += SystemAPI.Time.DeltaTime;
        if (_timer < Interval) return;
        _timer = 0f;

        var dustBuffer = SystemAPI.GetSingletonBuffer<DustCloudBufferElement>();

        foreach (var (unit, transform) in
            SystemAPI.Query<RefRO<Unit>, RefRO<LocalTransform>>())
        {
            if (unit.ValueRO.unitState != UnitState.Charge) continue;
            bool sprinting = SystemAPI.HasComponent<SprintingTag>(unit.ValueRO.squadEntity);
            float chance = sprinting ? TabletopTavernConstants.DUST_CHANCE_SPRINT : TabletopTavernConstants.DUST_CHANCE_APPROACH;
            if (_random.NextFloat() > chance) continue;

            dustBuffer.Add(new DustCloudBufferElement { Position = transform.ValueRO.Position });
        }
    }
}
