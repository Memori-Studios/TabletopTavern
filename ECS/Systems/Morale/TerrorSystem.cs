using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

namespace TJ.Morale
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MoraleSystem))]
    public partial struct TerrorApplicationSystem : ISystem
    {
        private ComponentLookup<IsTerrified> _isTerrifiedLookup;
        private ComponentLookup<SquadMovementComponent> _transformLookup;
        private ComponentLookup<CausesTerrorTag> _causesTerrorTagLookup;
        private ComponentLookup<EntityTeam> _entityTeamLookup;
        private EntityQuery _targetQuery; // Units that can be terrified (Sanguine Court immune - default)
        private EntityQuery _targetQueryIncludingSanguine; // Same, but Sanguine Court is affectable (mod disabled their immunity)
        private EntityQuery _terrorQuery; // Units that cause terror

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattlePhase>();
            _isTerrifiedLookup = state.GetComponentLookup<IsTerrified>(false); // Read-write for enabling/disabling
            _transformLookup = state.GetComponentLookup<SquadMovementComponent>(true); // Read-only
            _entityTeamLookup = state.GetComponentLookup<EntityTeam>(true); // Read-only
            _causesTerrorTagLookup = state.GetComponentLookup<CausesTerrorTag>(true); // Read-only

            // Query for potential targets (units with IsTerrified, excluding StalwartTag and static structures)
            _targetQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<SquadEntity>(),
                ComponentType.ReadOnly<SquadMovementComponent>(),
                ComponentType.Exclude<StalwartTag>(),
                ComponentType.Exclude<GarrisonGateSquadTag>(),
                ComponentType.Exclude<SanguineCourtRaceTag>()
            );

            // Same target set but without the Sanguine Court exclusion, used only when a mod turns
            // off their terror immunity via RaceBonusRuleData.SanguineCourt.ImmuneToTerror.
            _targetQueryIncludingSanguine = state.GetEntityQuery(
                ComponentType.ReadOnly<SquadEntity>(),
                ComponentType.ReadOnly<SquadMovementComponent>(),
                ComponentType.Exclude<StalwartTag>(),
                ComponentType.Exclude<GarrisonGateSquadTag>()
            );

            // Query for terror-causing units
            _terrorQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<CausesTerrorTag>(),
                ComponentType.ReadOnly<SquadMovementComponent>()
            );
        }

        // Not [BurstCompile]: reads the managed RaceBonusRuleData.SanguineCourt.ImmuneToTerror
        // toggle to pick the target query. The actual terror work stays in the two jobs below.
        public void OnUpdate(ref SystemState state)
        {
            _isTerrifiedLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _causesTerrorTagLookup.Update(ref state);
            _entityTeamLookup.Update(ref state);

            EntityQuery targetQuery = RaceBonusRuleData.SanguineCourt.ImmuneToTerror ? _targetQuery : _targetQueryIncludingSanguine;

            // Collect terror-causing units
            NativeArray<Entity> terrorEntities = _terrorQuery.ToEntityArray(Allocator.TempJob);
            NativeArray<SquadMovementComponent> terrorTransforms = _terrorQuery.ToComponentDataArray<SquadMovementComponent>(Allocator.TempJob);
            NativeArray<CausesTerrorTag> terrorTags = _terrorQuery.ToComponentDataArray<CausesTerrorTag>(Allocator.TempJob);

            // Collect target units
            NativeArray<Entity> targetEntities = targetQuery.ToEntityArray(Allocator.TempJob);
            NativeArray<SquadMovementComponent> targetTransforms = targetQuery.ToComponentDataArray<SquadMovementComponent>(Allocator.TempJob);

            var enableJob  = new TerrorApplicationJob
            {
                IsTerrifiedLookup = _isTerrifiedLookup,
                TransformLookup = _transformLookup,
                CausesTerrorTagLookup = _causesTerrorTagLookup,
                EntityTeamLookup = _entityTeamLookup,
                TerrorEntities = terrorEntities,
                TerrorTransforms = terrorTransforms,
                TerrorTags = terrorTags,
                TargetEntities = targetEntities,
                TargetTransforms = targetTransforms,
            };
            JobHandle enableHandle = enableJob.Schedule(state.Dependency);

            // Disable job
            var disableJob = new TerrorApplicationJob.DisableTerrifiedJob
            {
                IsTerrifiedLookup = _isTerrifiedLookup,
                EntityTeamLookup = _entityTeamLookup,
                TargetEntities = targetEntities,
                TargetTransforms = targetTransforms,
                TerrorEntities = terrorEntities,
                TerrorTransforms = terrorTransforms,
                TerrorTags = terrorTags
            };
            state.Dependency = disableJob.Schedule(enableHandle);

            // Dispose of temporary arrays
            terrorEntities.Dispose(state.Dependency);
            terrorTransforms.Dispose(state.Dependency);
            terrorTags.Dispose(state.Dependency);
            targetEntities.Dispose(state.Dependency);
            targetTransforms.Dispose(state.Dependency);
        }
    }

    // [BurstCompile]
    public partial struct TerrorApplicationJob : IJobEntity
    {
        public ComponentLookup<IsTerrified> IsTerrifiedLookup;
        [ReadOnly] public ComponentLookup<SquadMovementComponent> TransformLookup;
        [ReadOnly] public ComponentLookup<CausesTerrorTag> CausesTerrorTagLookup;
        [ReadOnly] public ComponentLookup<EntityTeam> EntityTeamLookup;
        [ReadOnly] public NativeArray<Entity> TerrorEntities;
        [ReadOnly] public NativeArray<SquadMovementComponent> TerrorTransforms;
        [ReadOnly] public NativeArray<CausesTerrorTag> TerrorTags;
        [ReadOnly] public NativeArray<Entity> TargetEntities;
        [ReadOnly] public NativeArray<SquadMovementComponent> TargetTransforms;

        void Execute(Entity terrorEntity, in CausesTerrorTag terrorTag, in SquadMovementComponent terrorTransform)
        {
            // Process all target entities
            for (int i = 0; i < TargetEntities.Length; i++)
            {
                Entity targetEntity = TargetEntities[i];
                if (EntityTeamLookup[terrorEntity].Value == EntityTeamLookup[targetEntity].Value) continue; // Skip same team
                
                if (CausesTerrorTagLookup.HasComponent(targetEntity)) continue; // Skip other terror units

                // Debug.Log($"Checking target: {targetEntity}");
                if (terrorEntity != targetEntity) // Skip self
                {
                    SquadMovementComponent targetTransform = TargetTransforms[i];
                    float distance = math.distance(terrorTransform.SquadCenter, targetTransform.SquadCenter);

                    if (!IsTerrifiedLookup.IsComponentEnabled(targetEntity))
                    {
                        // Debug.Log($"not terrified: {targetEntity}");
                        // If within terror radius, enable IsTerrified
                        if (distance <= TabletopTavernConstants.TERROR_RADIUS)
                        {
                            IsTerrifiedLookup.SetComponentEnabled(targetEntity, true);
                            // Debug.Log($"Terror applied to {targetEntity.Index} by {terrorEntity.Index}");
                        }
                    }
                }
            }
        }

        [BurstCompile]
        public struct DisableTerrifiedJob : IJob
        {
            public ComponentLookup<IsTerrified> IsTerrifiedLookup;
            [ReadOnly] public ComponentLookup<EntityTeam> EntityTeamLookup;
            [ReadOnly] public NativeArray<Entity> TargetEntities;
            [ReadOnly] public NativeArray<SquadMovementComponent> TargetTransforms;
            [ReadOnly] public NativeArray<Entity> TerrorEntities;
            [ReadOnly] public NativeArray<SquadMovementComponent> TerrorTransforms;
            [ReadOnly] public NativeArray<CausesTerrorTag> TerrorTags;

            public void Execute()
            {
                // Check each target to see if it's out of range of ALL terror units
                for (int i = 0; i < TargetEntities.Length; i++)
                {
                    Entity targetEntity = TargetEntities[i];
                    SquadMovementComponent targetTransform = TargetTransforms[i];
                    bool isInRangeOfAnyTerror = false;

                    for (int j = 0; j < TerrorEntities.Length; j++)
                    {
                        if(EntityTeamLookup[TerrorEntities[j]].Value == EntityTeamLookup[targetEntity].Value) continue; // Skip same team

                        float distance = math.distance(targetTransform.SquadCenter, TerrorTransforms[j].SquadCenter);
                        // Debug.Log($"Distance from {targetEntity} to {TerrorEntities[j]}: {distance}");
                        if (distance <= TabletopTavernConstants.TERROR_RADIUS)
                        {
                            isInRangeOfAnyTerror = true;
                            break;
                        }
                    }

                    // Disable IsTerrified if not in range of any terror unit
                    if (!isInRangeOfAnyTerror && IsTerrifiedLookup.IsComponentEnabled(targetEntity))
                    {
                        IsTerrifiedLookup.SetComponentEnabled(targetEntity, false);
                        // Debug.Log($"Terror removed from {targetEntity}");
                    }
                }
            }
        }
    }
}