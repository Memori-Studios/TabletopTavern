// using Unity.Burst;
// using Unity.Entities;
// using Unity.Transforms;
// using UnityEngine;
// 
// namespace TJ
// {
// public partial struct ArtilleryCrewSetUpSystem : ISystem
// {
//     [BurstCompile]
//     public void OnCreate(ref SystemState state)
//     {
//         state.RequireForUpdate<EntitiesReferences>();
//     }
//     // [BurstCompile]
//     public void OnUpdate(ref SystemState state)
//     {
//         var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
//         var entityManager = state.EntityManager;

//         EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EntitiesReferences>());
//         EntitiesReferences entitiesReferences = query.GetSingleton<EntitiesReferences>();
            
//         foreach (var (Saddle, RiderEntity) in SystemAPI
//             .Query<ArtilleryCrewSetUpEntity>()
//             .WithEntityAccess())
//         {
//             // Get the parent of the spawned entity
//             if (!SystemAPI.HasComponent<Parent>(RiderEntity)){
//                 continue;
//             }

//             var parentComponent = SystemAPI.GetComponent<Parent>(RiderEntity);
//             Entity parentEntity = parentComponent.Value;

//             if (!SystemAPI.HasComponent<Parent>(parentEntity)){
//                 continue;
//             }

//             var grandparentComponent = SystemAPI.GetComponent<Parent>(parentEntity);
//             Entity grandparentEntity = grandparentComponent.Value;
//             // Debug.Log($"ShieldEntity: Found grandparent entity {grandparentEntity}");
//             if(!SystemAPI.HasComponent<Artillery>(grandparentEntity)) {
//                 Debug.Log($"Entity {grandparentEntity} is not an Artillery unit yet.");
//                 continue;
//             }

//             // Cavalry cavalryComp = SystemAPI.GetComponent<Cavalry>(grandparentEntity);
//             Entity childEntity = entityManager.Instantiate(entitiesReferences.GetCavalryEntityFromUnitName(cavalryComp.unitName));
//             cavalryComp.riderEntity = childEntity;

//             ecb.SetComponent(grandparentEntity, cavalryComp);
//             ecb.AddComponent(childEntity, new Parent { Value = RiderEntity });
//             ecb.RemoveComponent<SaddleSetUpEntity>(RiderEntity);
//         }
        
//         ecb.Playback(state.EntityManager);
//         query.Dispose();
//     }
// }
// }
