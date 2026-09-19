using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using GPUECSAnimationBaker.Engine.AnimatorSystem;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateBefore(typeof(KillUnitSystem))]
[BurstCompile]
public partial class ProcessUnitDeathSystem : SystemBase
{
    private Unity.Mathematics.Random _random;
    private EntityQuery _squadQuery;

    [BurstCompile]
    protected override void OnCreate()
    {
        base.OnCreate();
        _random = new Unity.Mathematics.Random(1);
        _squadQuery = GetEntityQuery(
            ComponentType.ReadOnly<SquadEntity>(),
            ComponentType.ReadOnly<EntityReferenceBufferElement>()
        );
    }
    // [BurstCompile]
    protected override void OnUpdate()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityCommandBuffer ecbDelete = new EntityCommandBuffer(Allocator.Temp);

        Entities
            .WithNone<ThrowUnit, RemoveArtilleryTag>()
            .WithStructuralChanges()
            .ForEach((Entity entity, in UnitRemovedFromSquad removedUnit) =>
        {
            using var squadEntities = _squadQuery.ToEntityArray(Allocator.Temp);
            // Whether some squad buffer held this unit. If none did (squad already destroyed, or the
            // entry was stripped by SquadRemoveUnitSystem first), nothing below adds KillUnitTag, and
            // the unit used to be left alive at 0 HP with UnitRemovedFromSquad removed: a zombie that
            // stays in the world forever. Such a unit is killed outright after the loop.
            bool removedFromSquad = false;
            foreach (var squadEntity in squadEntities)
            {
                //make sure component exists
                if(!entityManager.HasComponent<SquadEntity>(squadEntity)) continue;

                var squad = entityManager.GetComponentData<SquadEntity>(squadEntity);
                if (squad.SquadId != removedUnit.SquadId) continue;

                // Get the buffer and remove the entity from it
                var entityBuffer = entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntity);
                for (int i = 0; i < entityBuffer.Length; i++)
                {
                    if (entityBuffer[i].Entity == removedUnit.Entity)
                    {
                        removedFromSquad = true;
                        Entity debugEntity = entityBuffer[i].DebugEntity;
                        entityBuffer.RemoveAt(i);

                        //make sure AnimationDataHolder component exists
                        if(!entityManager.HasComponent<AnimationDataHolder>(removedUnit.Entity)) {
                            ecbDelete.AddComponent<KillUnitTag>(removedUnit.Entity);
                            ecbDelete.AddComponent<KillUnitTag>(debugEntity);
                            break;
                        }
                        AnimationDataHolder animationDataHolder = entityManager.GetComponentData<AnimationDataHolder>(removedUnit.Entity);
                        Entity childEntity = animationDataHolder.gpuEcsAnimatorEntity;

                        if(removedUnit.DeleteCorpse)
                        {
                            ecbDelete.AddComponent<KillUnitTag>(childEntity);
                            ecbDelete.AddComponent<KillUnitTag>(removedUnit.Entity);
                            ecbDelete.AddComponent<KillUnitTag>(debugEntity);
                            //check if cavalry
                            if(entityManager.HasComponent<Cavalry>(removedUnit.Entity)) {
                                Cavalry cavalry = entityManager.GetComponentData<Cavalry>(removedUnit.Entity);
                                ecbDelete.AddComponent<KillUnitTag>(cavalry.riderEntity);
                            }
                            break;
                        }

                        //make sure GpuEcsAnimatorControlComponent exists. The unit is already out of the
                        //buffer at this point, so it must still be killed or it leaks as an orphan.
                        if(!entityManager.HasComponent<GpuEcsAnimatorControlComponent>(animationDataHolder.gpuEcsAnimatorEntity)) {
                            Debug.LogError($"Entity {animationDataHolder.gpuEcsAnimatorEntity} does not have GpuEcsAnimatorControlComponent component.");
                            if (entityManager.Exists(childEntity)) ecbDelete.AddComponent<KillUnitTag>(childEntity);
                            ecbDelete.AddComponent<KillUnitTag>(removedUnit.Entity);
                            ecbDelete.AddComponent<KillUnitTag>(debugEntity);
                            break;
                        }

                        GpuEcsAnimatorControlComponent controlComp = entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(animationDataHolder.gpuEcsAnimatorEntity);
                        controlComp.transitionSpeed = 0f;
                        int deathAnimationId = _random.NextInt(0, 3);
                        controlComp.animatorInfo.animationID = deathAnimationId switch {
                            0 => animationDataHolder.deathAnimationId1,
                            1 => animationDataHolder.deathAnimationId2,
                            2 => animationDataHolder.deathAnimationId3,
                            _ => animationDataHolder.deathAnimationId1,
                        };

                        ecbDelete.SetComponent(childEntity, controlComp);
                        ecbDelete.AddComponent<UnitOutlineClearTag>(childEntity);

                        ecbDelete.AddComponent<KillUnitTag>(removedUnit.Entity);
                        ecbDelete.AddComponent<KillUnitTag>(debugEntity);

                        // break;

                        //if cavalry, play rider death animation
                        if(SystemAPI.HasComponent<Cavalry>(removedUnit.Entity)) {
                            Cavalry cavalry = SystemAPI.GetComponent<Cavalry>(removedUnit.Entity);
                            RefRW<GpuEcsAnimatorControlComponent> controlComp2 = SystemAPI.GetComponentRW<GpuEcsAnimatorControlComponent>(cavalry.riderEntity);
                            controlComp2.ValueRW.animatorInfo.animationID = TabletopTavernConstants.CAVALRY_DEATH_ANIMATION_ID;
                            controlComp2.ValueRW.transitionSpeed = 0f;
                        }

                        if(removedUnit.KilledBySquadId != 100)
                        {
                            Entity SquadKillTagEntity = ecbDelete.CreateEntity();
                            ecbDelete.AddComponent<SquadKillTag>(SquadKillTagEntity, new SquadKillTag { SquadId = removedUnit.KilledBySquadId});
                        }
                    }
                }

                if(!removedFromSquad) continue;
                if(!entityManager.HasComponent<SetDestination>(removedUnit.Entity)) continue;

                SetDestination setDestination = entityManager.GetComponentData<SetDestination>(removedUnit.Entity);

                if(!entityManager.HasComponent<UnitPosition>(removedUnit.Entity)) continue;

                UnitPosition unit = entityManager.GetComponentData<UnitPosition>(removedUnit.Entity);

                //TODO Might be an error look into
                // Guard against double-add when two units from the same squad die in the same frame
                if (!entityManager.HasComponent<FormationNeedsToBeProcessed>(squad.SelfEntity))
                    entityManager.AddComponent<FormationNeedsToBeProcessed>(squad.SelfEntity);
                entityManager.SetComponentData(squad.SelfEntity, new FormationNeedsToBeProcessed
                {
                    squadPosition = setDestination.squadPosition,
                    indexRemoved = unit.unitIndex
                });
            }

            if (!removedFromSquad)
            {
                Debug.LogWarning($"ProcessUnitDeathSystem: dead unit {entity} (squad {removedUnit.SquadId}) was in no squad buffer, killing it directly.");
                if (entityManager.HasComponent<AnimationDataHolder>(entity))
                {
                    Entity orphanChild = entityManager.GetComponentData<AnimationDataHolder>(entity).gpuEcsAnimatorEntity;
                    if (entityManager.Exists(orphanChild)) ecbDelete.AddComponent<KillUnitTag>(orphanChild);
                }
                if (entityManager.HasComponent<Cavalry>(entity))
                {
                    Entity rider = entityManager.GetComponentData<Cavalry>(entity).riderEntity;
                    if (entityManager.Exists(rider)) ecbDelete.AddComponent<KillUnitTag>(rider);
                }
                ecbDelete.AddComponent<KillUnitTag>(entity);
            }

            // After processing, remove the UnitRemovedFromSquad component
            entityManager.RemoveComponent<UnitRemovedFromSquad>(entity);

        }).Run();

        ecbDelete.Playback(entityManager);
        ecbDelete.Dispose();
    }
}