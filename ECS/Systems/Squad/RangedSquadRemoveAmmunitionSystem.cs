using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TJ;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
partial struct RangedSquadRemoveAmmunitionSystem : ISystem
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

        // Keyed on SquadAmmunition rather than RangedSquad, so this covers mages too: MageCastSystem
        // adds the same AmmuntionSpent tag an archer's shot does, and a charge is spent here.
        foreach (var (squad, ammunition, entityBuffer) in SystemAPI.Query<
            RefRO<SquadEntity>,
            RefRW<SquadAmmunition>,
            DynamicBuffer<EntityReferenceBufferElement>>())
        {
            //skip this the first time the squad is processed
            for (int i = 0; i < entityBuffer.Length; i++)
            {
                Entity referencedEntity = entityBuffer[i].Entity;
                if(entityManager.HasComponent<AmmuntionSpent>(referencedEntity))
                {
                    entityCommandBuffer.RemoveComponent<AmmuntionSpent>(referencedEntity);
                    ammunition.ValueRW.Value -= 1;
                }
            }
            // A shooter spends a whole volley at once, so where exactly the pool crosses empty is
            // noise and the original < 0 is preserved byte for byte. A mage is one model spending
            // one charge per cast, so that same rule would hand it one cast more than its authored
            // charge count - a third again as many on a 3-charge mage. Hence the separate boundary.
            int depletedAt = entityManager.HasComponent<MageSquad>(squad.ValueRO.SelfEntity) ? 0 : -1;
            //update healthbar ammo count
            if (ammunition.ValueRO.Value <= depletedAt)
            {
                entityCommandBuffer.AddComponent<RanOutOfAmmoTag>(squad.ValueRO.SelfEntity);
            }
        }
    }
}

