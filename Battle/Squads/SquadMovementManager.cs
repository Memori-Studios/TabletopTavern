using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using ProjectDawn.Navigation.Hybrid;
using ProjectDawn.Navigation;
using Unity.Collections;

namespace TJ
{
    public class SquadMovementManager : MonoBehaviour
    {
        [SerializeField] private SquadNavObject squadMovementPrefab;
        Dictionary<Entity, SquadNavObject> squadNavObjects = new ();
        bool battleHasStarted = false;
        bool battleHasEnded = false;

        private void Update()
        {
            if (battleHasEnded) return;
            if (World.DefaultGameObjectInjectionWorld == null) return;
            if (!World.DefaultGameObjectInjectionWorld.IsCreated) return;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery squadEntityQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SquadEntity>()
                .WithNone<GarrisonGateSquadTag>()
                .Build(entityManager);
            NativeArray<Entity> squadEntities = squadEntityQuery.ToEntityArray(Allocator.Temp);

            if (!battleHasStarted)
            {
                EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BattleHasStarted>());
                if (query.CalculateEntityCount() == 0)
                {
                    HandlePreBattleMovement(entityManager, squadEntities);
                    query.Dispose();
                    squadEntityQuery.Dispose();
                    squadEntities.Dispose();
                    return;
                }
                query.Dispose();
                battleHasStarted = true;
            }

            HandleSquadMovement(entityManager, squadEntities);
            DetermineSquadFlanking(entityManager, squadEntities);
            squadEntityQuery.Dispose();
            squadEntities.Dispose();
        }
        public void ReceiveSquadEntity(Entity entity, float _squadStatsSpeed, int _squadID)
        {
            SquadNavObject squadMovementObject = Instantiate(squadMovementPrefab);
            squadMovementObject.SetUp(_squadID);
            squadNavObjects.Add(entity, squadMovementObject);
        }
        private void HandleSquadMovement(EntityManager entityManager, NativeArray<Entity> squadEntities)
        {
            for (int i = 0; i < squadEntities.Length; i++)
            {
                if(!squadNavObjects.TryGetValue(squadEntities[i], out SquadNavObject squadMovementObject))
                {
                    // A squad spawned mid-battle (e.g. summoned by a spell) carries SquadEntity for a
                    // frame or two before its nav object exists: RegisterSquad queues NeedsToBeProcessed
                    // on an ECB, SquadEntitySetUpSystem swaps that for SquadEntityGameObjectsProcessingNeeded
                    // on another, and only then does EntityWatcher call ReceiveSquadEntity. Stay quiet
                    // while that handshake is in flight - a missing entry outside it is still a real bug.
                    bool awaitingSetUp = entityManager.HasComponent<NeedsToBeProcessed>(squadEntities[i])
                                      || entityManager.HasComponent<SquadEntityGameObjectsProcessingNeeded>(squadEntities[i]);
                    if(!awaitingSetUp)
                        Debug.LogError($"SquadNavObject dictionary does not contain entity: {squadEntities[i]}");
                    continue;
                }
                SquadMovementComponent squadEntityMovementComponent = entityManager.GetComponentData<SquadMovementComponent>(squadEntities[i]);
                squadMovementObject.UpdateNavObject(squadEntityMovementComponent);
            }
        }
        private void DetermineSquadFlanking(EntityManager entityManager, NativeArray<Entity> squadEntities)
        {
            for (int i = 0; i < squadEntities.Length; i++)
            {
                if (entityManager.HasComponent<InCombat>(squadEntities[i]) || entityManager.HasComponent<FormationEngagedInRangedCombat>(squadEntities[i]))
                {
                    SquadEntity squadEntityData = entityManager.GetComponentData<SquadEntity>(squadEntities[i]);
                    if (squadEntityData.TargetSquadEntity == Entity.Null) continue;

                    if (squadNavObjects.TryGetValue(squadEntityData.TargetSquadEntity, out SquadNavObject targetSquadNavObject))
                    {
                        if (entityManager.HasBuffer<EntityReferenceBufferElement>(squadEntityData.TargetSquadEntity) &&
                            entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntityData.TargetSquadEntity).Length <= 1)
                            continue;

                        bool isFlanking = squadNavObjects[squadEntities[i]].IsFlanking(targetSquadNavObject);

                        if (isFlanking)
                        {
                            if (entityManager.GetComponentData<IsFlanking>(squadEntities[i]).TargetFlankedSquadEntity != squadEntityData.TargetSquadEntity)
                            {
                                var isFlankingComponent = entityManager.GetComponentData<IsFlanking>(squadEntities[i]);

                                isFlankingComponent.TargetFlankedSquadEntity = squadEntityData.TargetSquadEntity;

                                if (!entityManager.IsComponentEnabled<IsFlanking>(squadEntities[i]))
                                {
                                    isFlankingComponent.TargetFlankedSquadEntity = squadEntityData.TargetSquadEntity;
                                    entityManager.SetComponentData(squadEntities[i], isFlankingComponent);
                                    entityManager.SetComponentEnabled<IsFlanking>(squadEntities[i], isFlanking);
                                    // Debug.Log($"started flank!");
                                }
                            }
                        }
                        else
                        {
                            if(entityManager.GetComponentData<IsFlanking>(squadEntities[i]).TargetFlankedSquadEntity == squadEntityData.TargetSquadEntity)
                            {
                                var isFlankingComponent = entityManager.GetComponentData<IsFlanking>(squadEntities[i]);
                                isFlankingComponent.TargetFlankedSquadEntity = Entity.Null;
                                entityManager.SetComponentData(squadEntities[i], isFlankingComponent);
                            }
                        }
                    }
                }
            }
        }
        private void HandlePreBattleMovement(EntityManager entityManager, NativeArray<Entity> squadEntities)
        {
            void UpdateSquadMovementBeforeBattle(Entity entity)
            {
                SquadMovementComponent squadEntity = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<SquadMovementComponent>(entity);
                // squadEntity.CurrentNavMeshPosition = squadEntity.SquadCenter;
                squadEntity.GoalPosition = squadEntity.SquadCenter;
                entityManager.SetComponentData(entity, squadEntity);
                if(squadNavObjects.TryGetValue(entity, out SquadNavObject squadNavObject))
                {
                    squadNavObject.transform.SetPositionAndRotation(squadEntity.SquadCenter, squadEntity.SquadRotation);
                }
            }

            if (battleHasStarted)
            {
                CleanUp();
                return;
            }

            for (int i = 0; i < squadEntities.Length; i++)
            {
                UpdateSquadMovementBeforeBattle(squadEntities[i]);
            }
        }
        public void CleanUp()
        {
            // Debug.Log($"Cleaning up squad movement objects");
            battleHasEnded = true;

            foreach (var navObject in squadNavObjects.Values)
            {
                Destroy(navObject.gameObject);
            }
            squadNavObjects.Clear();
        }
        public void EnteringSwamp(Entity entity, bool isEntering)
        {
            // Debug.Log($"EnteringSwamp: {entity}");

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // The old guard here bailed out when the squad had no SquadNavObject yet, but nothing in
            // this method uses the nav object (the locomotion writes that did are commented out below).
            // All it did was skip the speed math for squads still finishing setup, leaving the *= and
            // the /= unpaired so the slow never came off. Only the buffer needs checking.
            if (!entityManager.HasBuffer<EntityReferenceBufferElement>(entity)) return;

            var entityBuffer = entityManager.GetBuffer<EntityReferenceBufferElement>(entity);

            //AgentAuthoring agent = squadNavObjects[entity].GetComponent<AgentAuthoring>();                
            // var locomotion = agent.EntityLocomotion;

            if (isEntering)
            {
                // locomotion.Speed *= TabletopTavernConstants.SWAMP_SPEED_MODIFIER;
                // locomotion.Acceleration *= TabletopTavernConstants.SWAMP_SPEED_MODIFIER;
                // agent.EntityLocomotion = locomotion;

                for (int j = 0; j < entityBuffer.Length; j++)
                {
                    Entity unitEntity = entityBuffer[j].Entity;
                    if (entityManager.Exists(unitEntity))
                    {
                        AgentLocomotion agentLocomotion = entityManager.GetComponentData<AgentLocomotion>(unitEntity);
                        agentLocomotion.Speed *= TabletopTavernConstants.SWAMP_SPEED_MODIFIER;
                        agentLocomotion.Acceleration *= TabletopTavernConstants.SWAMP_SPEED_MODIFIER;
                        entityManager.SetComponentData(unitEntity, agentLocomotion);
                    }
                }
            }
            else
            {
                // locomotion.Speed /= TabletopTavernConstants.SWAMP_SPEED_MODIFIER;
                // locomotion.Acceleration /= TabletopTavernConstants.SWAMP_SPEED_MODIFIER;
                // agent.EntityLocomotion = locomotion;

                for (int j = 0; j < entityBuffer.Length; j++)
                {
                    Entity unitEntity = entityBuffer[j].Entity;
                    if (entityManager.Exists(unitEntity))
                    {
                        AgentLocomotion agentLocomotion = entityManager.GetComponentData<AgentLocomotion>(unitEntity);
                        agentLocomotion.Speed /= TabletopTavernConstants.SWAMP_SPEED_MODIFIER;
                        agentLocomotion.Acceleration /= TabletopTavernConstants.SWAMP_SPEED_MODIFIER;
                        entityManager.SetComponentData(unitEntity, agentLocomotion);
                    }
                }
            }
        }
    }
}