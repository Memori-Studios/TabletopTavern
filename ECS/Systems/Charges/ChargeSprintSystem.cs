using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using ProjectDawn.Navigation;

/// <summary>
/// The sprint is the charge. An attacking melee squad speeds up once it is close to its target, and
/// ChargeSquad.ChargeTime counts the seconds it has sprinted. MeleeSquadChargeSystem reads that at contact.
/// Speed is scaled by a multiply/divide pair so swamp, rain and Shieldwall math keeps working underneath.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct ChargeSprintSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    // Not Burst-compiled: it reads the managed WeatherRuleData statics a mod may have patched.
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool rainStopsCharge = WeatherRuleData.Rain.RemovesChargeBonus;

        foreach (var (squad, movement, chargeSquad, unitBuffer, entity) in SystemAPI.Query<
            RefRO<SquadEntity>,
            RefRO<SquadMovementComponent>,
            RefRW<ChargeSquad>,
            DynamicBuffer<EntityReferenceBufferElement>>()
            .WithAll<MeleeSquad>()
            .WithNone<InCombat, BrokenSquadTag, CavalryFlankingTag>()
            .WithNone<WearyTag, GarrisonGateSquadTag>()
            .WithEntityAccess())
        {
            bool sprinting = SystemAPI.HasComponent<SprintingTag>(entity);
            bool marked = SystemAPI.HasComponent<EmpoweredChargeTag>(entity);

            // Rally the Banners marks the attack while the squad is inside the circle, so it still counts at contact.
            if (!marked && SystemAPI.HasComponent<ChargeEmpoweredTag>(entity))
            {
                ecb.AddComponent(entity, new EmpoweredChargeTag { BonusImpact = SystemAPI.GetComponent<ChargeEmpoweredTag>(entity).BonusImpact });
                marked = true;
            }

            Entity target = squad.ValueRO.TargetSquadEntity;
            bool canSprint = target != Entity.Null
                && SystemAPI.HasComponent<SquadMovementComponent>(target)
                && !SystemAPI.HasComponent<GarrisonGateSquadTag>(target);

            if (canSprint && !marked)
            {
                canSprint = !SystemAPI.HasComponent<InForestTag>(entity)
                    && !SystemAPI.HasComponent<InSwampTag>(entity)
                    && !(rainStopsCharge && SystemAPI.HasComponent<InRainTag>(entity));
            }

            if (canSprint)
            {
                SquadMovementComponent targetMovement = SystemAPI.GetComponent<SquadMovementComponent>(target);
                float gap = math.distance(movement.ValueRO.SquadCenter, targetMovement.SquadCenter)
                    - movement.ValueRO.BoundsRadius - targetMovement.BoundsRadius;
                canSprint = gap <= TabletopTavernConstants.CHARGE_SPRINT_RANGE;
            }

            if (canSprint)
            {
                if (!sprinting)
                {
                    ScaleSpeed(ref state, unitBuffer, TabletopTavernConstants.CHARGE_SPRINT_SPEED_MULT);
                    ecb.AddComponent<SprintingTag>(entity);
                }
                chargeSquad.ValueRW.ChargeTime += deltaTime;
            }
            else
            {
                chargeSquad.ValueRW.ChargeTime = 0f;
                if (sprinting)
                {
                    ScaleSpeed(ref state, unitBuffer, 1f / TabletopTavernConstants.CHARGE_SPRINT_SPEED_MULT);
                    ecb.RemoveComponent<SprintingTag>(entity);
                }
            }
        }

        // Every squad the loop above skips: the attack ended, it made contact, broke, or went weary.
        foreach (var (unitBuffer, entity) in SystemAPI.Query<DynamicBuffer<EntityReferenceBufferElement>>()
            .WithAll<SprintingTag>()
            .WithEntityAccess())
        {
            bool stillCharging = SystemAPI.HasComponent<ChargeSquad>(entity)
                && SystemAPI.HasComponent<MeleeSquad>(entity)
                && !SystemAPI.HasComponent<InCombat>(entity)
                && !SystemAPI.HasComponent<BrokenSquadTag>(entity)
                && !SystemAPI.HasComponent<CavalryFlankingTag>(entity)
                && !SystemAPI.HasComponent<WearyTag>(entity)
                && !SystemAPI.HasComponent<GarrisonGateSquadTag>(entity);
            if (stillCharging) continue;

            // A landed charge keeps its speed for a moment so the ranks behind arrive running.
            if (SystemAPI.HasComponent<SprintFollowThrough>(entity) && !SystemAPI.HasComponent<BrokenSquadTag>(entity))
            {
                RefRW<SprintFollowThrough> followThrough = SystemAPI.GetComponentRW<SprintFollowThrough>(entity);
                followThrough.ValueRW.Remaining -= deltaTime;
                if (followThrough.ValueRO.Remaining > 0f) continue;
            }
            if (SystemAPI.HasComponent<SprintFollowThrough>(entity)) ecb.RemoveComponent<SprintFollowThrough>(entity);

            ScaleSpeed(ref state, unitBuffer, 1f / TabletopTavernConstants.CHARGE_SPRINT_SPEED_MULT);
            ecb.RemoveComponent<SprintingTag>(entity);
        }

        // An attack that ended without contact drops its Rally the Banners mark.
        foreach (var (squad, entity) in SystemAPI.Query<RefRO<SquadEntity>>()
            .WithAll<EmpoweredChargeTag>()
            .WithNone<ChargeSquad, StartChargeTag, FormationEngagedInCombat>()
            .WithEntityAccess())
        {
            ecb.RemoveComponent<EmpoweredChargeTag>(entity);
        }
    }

    void ScaleSpeed(ref SystemState state, DynamicBuffer<EntityReferenceBufferElement> units, float factor)
    {
        for (int i = 0; i < units.Length; i++)
        {
            Entity unit = units[i].Entity;
            if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<AgentLocomotion>(unit)) continue;
            RefRW<AgentLocomotion> loc = SystemAPI.GetComponentRW<AgentLocomotion>(unit);
            loc.ValueRW.Speed *= factor;
            loc.ValueRW.Acceleration *= factor;
        }
    }
}
