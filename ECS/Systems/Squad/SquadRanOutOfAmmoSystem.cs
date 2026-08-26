using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TJ;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(RangedSquadRemoveAmmunitionSystem))]
partial struct SquadRanOutOfAmmoSystem : ISystem
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

        // Squads updating on entity destroyed
        foreach (var (squad, rangedSquad, queuedOrders, entityBuffer) in SystemAPI.Query<
            RefRO<SquadEntity>,
            RefRW<RangedSquad>,
            DynamicBuffer<QueuedOrder>,
            DynamicBuffer<EntityReferenceBufferElement>>().WithPresent<RanOutOfAmmoTag>().WithAbsent<GarrisonGateSquadTag>())
        {
            entityCommandBuffer.RemoveComponent<RanOutOfAmmoTag>(squad.ValueRO.SelfEntity);
            entityCommandBuffer.RemoveComponent<RangedSquad>(squad.ValueRO.SelfEntity);
            entityCommandBuffer.RemoveComponent<SquadAmmunition>(squad.ValueRO.SelfEntity);
            entityCommandBuffer.AddComponent<MeleeSquad>(squad.ValueRO.SelfEntity);

            if(entityManager.HasComponent<RangedSquadSkirmishTag>(squad.ValueRO.SelfEntity)) {
                entityCommandBuffer.RemoveComponent<RangedSquadSkirmishTag>(squad.ValueRO.SelfEntity);
            }

            if(entityManager.HasComponent<FormationEngagedInRangedCombat>(squad.ValueRO.SelfEntity)) {
                entityCommandBuffer.RemoveComponent<FormationEngagedInRangedCombat>(squad.ValueRO.SelfEntity);
            }

            queuedOrders.Clear();

            // Debug.Log($"SquadRanOutOfAmmoSystem: Squad {squad.ValueRO.SquadId} has ran out of ammo!");

            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity entity = entityBuffer[i].Entity;

                if (entityManager.HasComponent<RangedMeleeConverter>(entity))
                {
                    RangedMeleeConverter rangedMeleeConverter = entityManager.GetComponentData<RangedMeleeConverter>(entity);
                    rangedMeleeConverter.SwitchToMelee = true;
                    entityManager.SetComponentData(entity, rangedMeleeConverter);
                    entityCommandBuffer.SetComponentEnabled<RangedMeleeConverter>(entity, true);
                }
                if(entityManager.HasComponent<ShootAttack>(entity)) {
                    entityCommandBuffer.RemoveComponent<ShootAttack>(entity);
                }
            }
        }

        #region Mage

        // A spent mage converts to a melee body the same way a spent archer does, but deliberately
        // NOT through the loop above. A mage has no ShootAttack to strip and no RangedMeleeConverter
        // to trip - it never swapped a weapon, its MeleeAttack was there the whole time. And its
        // QueuedOrder buffer holds the player's attack orders, which it should keep following on
        // foot rather than have silently cleared out from under them.
        foreach (var (squad, entityBuffer) in SystemAPI.Query<
            RefRO<SquadEntity>,
            DynamicBuffer<EntityReferenceBufferElement>>().WithAll<MageSquad>().WithPresent<RanOutOfAmmoTag>())
        {
            entityCommandBuffer.RemoveComponent<RanOutOfAmmoTag>(squad.ValueRO.SelfEntity);
            entityCommandBuffer.RemoveComponent<MageSquad>(squad.ValueRO.SelfEntity);
            entityCommandBuffer.RemoveComponent<SquadAmmunition>(squad.ValueRO.SelfEntity);
            entityCommandBuffer.AddComponent<MeleeSquad>(squad.ValueRO.SelfEntity);

            // Removing MageCast is what actually stops the casting: MageCastSystem reads its Timer
            // off the first unit in the buffer, so without this the squad would keep queueing casts
            // against a pool that no longer exists. One-way, like the archer conversion.
            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity entity = entityBuffer[i].Entity;

                if (entityManager.HasComponent<MageCast>(entity))
                {
                    entityCommandBuffer.RemoveComponent<MageCast>(entity);
                }
            }
        }

        #endregion
    }
}

