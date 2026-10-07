using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;

namespace TJ
{
public partial struct SaddleSetUpSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
    }
    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
        var entityManager = state.EntityManager;

        // Build rider prefab lookup from loaded UnitGPUAnimPrefabs
        var riderLookup = new Dictionary<UnitName, Entity>();
        foreach (var prefabs in SystemAPI.Query<UnitGPUAnimPrefabs>())
        {
            if (prefabs.HasRider)
                riderLookup[prefabs.unitName] = prefabs.riderEntity;
        }

        foreach (var (Saddle, RiderEntity) in SystemAPI
            .Query<SaddleSetUpEntity>()
            .WithEntityAccess())
        {
            // Get the parent of the spawned entity
            if (!SystemAPI.HasComponent<Parent>(RiderEntity)){
                continue;
            }

            var parentComponent = SystemAPI.GetComponent<Parent>(RiderEntity);
            Entity parentEntity = parentComponent.Value;

            if (!SystemAPI.HasComponent<Parent>(parentEntity)){
                continue;
            }

            var grandparentComponent = SystemAPI.GetComponent<Parent>(parentEntity);
            Entity grandparentEntity = grandparentComponent.Value;
            // Debug.Log($"ShieldEntity: Found grandparent entity {grandparentEntity}");
            if(!SystemAPI.HasComponent<Cavalry>(grandparentEntity)) {
                continue;
            }

            Cavalry cavalryComp = SystemAPI.GetComponent<Cavalry>(grandparentEntity);
            if (!riderLookup.TryGetValue(cavalryComp.unitName, out Entity riderPrefab)) continue;
            Entity childEntity = entityManager.Instantiate(riderPrefab);
            cavalryComp.riderEntity = childEntity;

            ecb.SetComponent(grandparentEntity, cavalryComp);
            ecb.AddComponent(childEntity, new Parent { Value = RiderEntity });
            ecb.RemoveComponent<SaddleSetUpEntity>(RiderEntity);
        }
        
        ecb.Playback(state.EntityManager);
    }
}
}
