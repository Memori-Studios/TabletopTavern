using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
using Unity.Collections;

namespace TJ
{
    public partial struct QueuedOrderSystem : ISystem
    {
        private EntityQuery _nonBrokenSquadQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CameraPositionComponent>();
            _nonBrokenSquadQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SquadEntity>()
                .WithNone<BrokenSquadTag>()
                .Build(ref state);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entityCommandBuffer = new EntityCommandBuffer(state.WorldUpdateAllocator);
            EntityManager entityManager = state.EntityManager;

            // Build SquadId -> SelfEntity map once per frame instead of querying inside the loop
            var allSquadData = _nonBrokenSquadQuery.ToComponentDataArray<SquadEntity>(Allocator.Temp);
            var squadById = new NativeHashMap<int, Entity>(allSquadData.Length, Allocator.Temp);
            foreach (var sq in allSquadData)
                squadById.TryAdd(sq.SquadId, sq.SelfEntity);
            allSquadData.Dispose();

            foreach (var (squadEntity, queuedOrderBuffer, entity) in SystemAPI.Query<
                SquadEntity,
                DynamicBuffer<QueuedOrder>>()
                .WithEntityAccess())
            {
                if (queuedOrderBuffer.Length == 0) continue;

                var order = queuedOrderBuffer[0];

                if(order.Status == QueuedOrderStatus.Pending)
                {
                    // Debug.Log($"Processing queued order of type {order.Type} for entity ");

                    if(order.Type == QueuedOrderType.Move)
                    {
                        SquadDestination squadDestination = new ()
                        {
                            DestinationPosition = order.Goal,
                            DestinationRotation = order.Rotation,
                            TargetSquadId = 0,
                            WidthAndDepth = order.WidthAndDepth,
                            SpeedCap = order.SpeedCap
                        };

                        entityCommandBuffer.AddComponent(squadEntity.SelfEntity, squadDestination);

                        entityCommandBuffer.AddComponent(squadEntity.SelfEntity, new IssueSquadCommand() {
                            SquadCommand = SquadCommand.Move,
                            NewTargetSquad = default
                        });
                    }
                    else if(order.Type == QueuedOrderType.Attack)
                    {
                        squadById.TryGetValue(order.TargetSquadId, out Entity TargetSquadEntity);
                        if(TargetSquadEntity != Entity.Null)
                        {
                            entityCommandBuffer.AddComponent(squadEntity.SelfEntity, new IssueSquadCommand() {
                                SquadCommand = SquadCommand.Attack,
                                NewTargetSquad = TargetSquadEntity
                            });
                        }
                    }
                    order.Status = QueuedOrderStatus.InProgress;
                    queuedOrderBuffer.ElementAt(0) = order;
                }

                if(order.Status == QueuedOrderStatus.InProgress && order.Type == QueuedOrderType.Attack )
                {
                    bool targetSquadStillExists = squadById.ContainsKey(order.TargetSquadId);

                    if(!targetSquadStillExists)
                    {
                        // Debug.Log($"Target squad {order.TargetSquadId} no longer exists, marking queued order as complete");
                        entityCommandBuffer.SetComponentEnabled<CompleteQueuedOrderTag>(squadEntity.SelfEntity, true);
                    }
                }

                if(entityManager.IsComponentEnabled<CompleteQueuedOrderTag>(squadEntity.SelfEntity))
                {
                    // Debug.Log($"Removing completed queued order of type {order.Type} from entity {squadEntity.SquadId}");
                    queuedOrderBuffer.RemoveAt(0);
                    entityCommandBuffer.SetComponentEnabled<CompleteQueuedOrderTag>(squadEntity.SelfEntity, false);
                }
            }

            squadById.Dispose();
            entityCommandBuffer.Playback(state.EntityManager);
        }
    }
}
