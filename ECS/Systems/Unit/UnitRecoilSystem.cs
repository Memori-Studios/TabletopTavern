using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Plays the short lunge and hit recoil on a unit's model. Only the animator child moves, so
/// navigation, collision and melee range never see it.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
partial struct UnitRecoilSystem : ISystem
{
    ComponentLookup<LocalTransform> m_TransformLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        m_TransformLookup = state.GetComponentLookup<LocalTransform>();
        state.RequireForUpdate<UnitRecoil>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_TransformLookup.Update(ref state);
        new RecoilJob
        {
            TransformLookup = m_TransformLookup,
            DeltaTime = SystemAPI.Time.DeltaTime,
        }.Schedule();
    }

    [BurstCompile]
    partial struct RecoilJob : IJobEntity
    {
        public ComponentLookup<LocalTransform> TransformLookup;
        public float DeltaTime;

        public void Execute(ref UnitRecoil recoil, in AnimationDataHolder holder)
        {
            if (recoil.LungeIn > 0f)
            {
                recoil.LungeIn -= DeltaTime;
                if (recoil.LungeIn <= 0f)
                {
                    recoil.LungeIn = 0f;
                    recoil.Kick(new float3(0f, 0f, TabletopTavernConstants.RECOIL_LUNGE_DISTANCE), 0f, TabletopTavernConstants.RECOIL_LUNGE_TIME);
                }
            }
            if (recoil.Timer <= 0f) return;

            Entity model = holder.gpuEcsAnimatorEntity;
            if (!TransformLookup.HasComponent(model))
            {
                recoil.Timer = 0f;
                return;
            }

            LocalTransform transform = TransformLookup[model];
            // The model is at rest whenever no recoil is running, so its first pose is the one to return to.
            if (!recoil.HasBase)
            {
                recoil.BasePosition = transform.Position;
                recoil.BaseRotation = transform.Rotation;
                recoil.HasBase = true;
            }

            recoil.Timer -= DeltaTime;
            float amount = 0f;
            if (recoil.Timer > 0f && recoil.Duration > 0f)
            {
                // Out fast over the first quarter, back slowly over the rest.
                float progress = 1f - recoil.Timer / recoil.Duration;
                amount = progress < 0.25f ? progress / 0.25f : 1f - (progress - 0.25f) / 0.75f;
                amount = math.smoothstep(0f, 1f, amount);
            }

            transform.Position = recoil.BasePosition + recoil.Offset * amount;
            transform.Rotation = math.mul(recoil.BaseRotation, quaternion.RotateX(recoil.Tilt * amount));
            TransformLookup[model] = transform;
        }
    }
}
