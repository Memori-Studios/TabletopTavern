using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

// Sibling of RangedSquadChargeSystem and MeleeSquadChargeSystem: the system that consumes a mage
// squad's ChargeSquad tag and walks it into casting range.
//
// Without this a mage never moved. StartChargeSystem is type-agnostic, so an attack order gave the
// mage ChargeSquad like anything else, but the two existing charge systems require RangedSquad and
// MeleeSquad respectively, so nothing picked the mage up. It sat where it spawned, and because
// MageSquadFindTargetSystem carries .WithNone<ChargeSquad>() the tag it could never shed also locked
// it out of ever re-targeting - it would keep its first target for the whole battle.
//
// Halting is what ends the charge: SquadHaltCommandSystem removes ChargeSquad whenever HaltCommandTag
// is present, and leaves TargetSquadEntity alone unless DropTarget is set.
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct MageSquadChargeSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        // Mage squad moving into casting range.
        foreach (var (squad, squadTargetting, squadMovement, mageSquad, squadOverrides, entityBuffer, chargeSquad) in SystemAPI.Query<
            RefRW<SquadEntity>,
            RefRW<SquadTargettingComponent>,
            RefRW<SquadMovementComponent>,
            RefRO<MageSquad>,
            SquadOverridesComponent,
            DynamicBuffer<EntityReferenceBufferElement>,
            ChargeSquad>
        ().WithAbsent<InCombat, BrokenSquadTag>()
        .WithNone<CeaseFireTag>()
        )
        {
            if (!squadOverrides.AutoTarget && !entityManager.Exists(squad.ValueRO.TargetSquadEntity)) continue;

            // SquadTargetingSystem re-enables this on a ~0.2s cadence, which is what throttles this
            // whole system. Same pacing the archer and melee charge systems run on.
            entityCommandBuffer.SetComponentEnabled<SquadTargettingComponent>(squad.ValueRO.SelfEntity, false);

            if (entityManager.HasComponent<SquadMoveOverrideTag>(squad.ValueRO.SelfEntity)) continue;

            if (!entityManager.Exists(squad.ValueRO.TargetSquadEntity))
            {
                entityCommandBuffer.AddComponent<HaltCommandTag>(squad.ValueRO.SelfEntity);
                continue;
            }

            if (entityManager.HasComponent<BrokenSquadTag>(squad.ValueRO.TargetSquadEntity))
            {
                entityCommandBuffer.AddComponent<HaltCommandTag>(squad.ValueRO.SelfEntity);
                continue;
            }

            SquadMovementComponent targetSquad = entityManager.GetComponentData<SquadMovementComponent>(squad.ValueRO.TargetSquadEntity);
            float distance = math.distance(targetSquad.SquadCenter, squadMovement.ValueRO.SquadCenter);
            quaternion directionToTarget = quaternion.LookRotationSafe(targetSquad.SquadCenter - squadMovement.ValueRO.SquadCenter, math.up());

            squadMovement.ValueRW.SetRotation(directionToTarget);

            // Same squad-center-to-squad-center measure MageCastSystem gates on, so the frame the
            // mage stops is the frame it can cast.
            if (distance < mageSquad.ValueRO.AttackRange)
            {
                if (!entityManager.HasComponent<HaltCommandTag>(squad.ValueRO.SelfEntity))
                {
                    entityCommandBuffer.AddComponent(squad.ValueRO.SelfEntity, new HaltCommandTag() { FreezePosition = true });
                }

                // Deliberately NOT FormationEngagedInRangedCombat, which is the one place this
                // diverges from the archer version. SquadEngageInCombatSystem consumes that tag by
                // nulling TargetSquadEntity, on the assumption that RangedSquadFindTargetSystem
                // re-acquires every volley. MageSquadFindTargetSystem does the opposite - it holds a
                // target until it dies or breaks - so the mage would drop the target it just walked
                // across the field for, and a manually-targeted mage (AutoTarget off) would go idle
                // permanently. Cost of leaving it off: no "firing" card icon and no ranged-fire
                // attack arrow state for mages.
                for (int i = 0; i < entityBuffer.Length; i++)
                {
                    Entity entity = entityBuffer[i].Entity;
                    if (!entityManager.Exists(entity)) continue;

                    // OnEngageRanged is the standing-and-shooting posture: it enables RotateUnit so
                    // RotateUnitSystem keeps the mage facing its target, and drops AgentSeparation.
                    // It sets the unit's state enum to InCombat but does NOT add the InCombat
                    // component - only OnEngage does that - so MageCastSystem's InCombat bail is
                    // untouched and the mage keeps casting.
                    Unit unit = entityManager.GetComponentData<Unit>(entity);
                    unit.unitState = UnitState.OnEngageRanged;
                    entityManager.SetComponentData(entity, unit);
                }
            }
            else
            {
                squadMovement.ValueRW.GoalPosition = targetSquad.SquadCenter;
                entityCommandBuffer.AddComponent<RecalculatePositionsForUnitsCharging>(squad.ValueRO.SelfEntity);
            }
        }
    }
}
