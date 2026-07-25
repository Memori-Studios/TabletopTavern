using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;

namespace TJ.Morale {

    partial struct MoraleSystem : ISystem
    {
        private ComponentLookup<IsTerrified> IsTerrifiedTagLookup;
        private ComponentLookup<HealthLossPercent> HealthLossPercentTagLookup;
        private ComponentLookup<RetreatingNearbyAllies> RetreatingNearbyAlliesTagLookup;
        private ComponentLookup<TakingFlankingDamage> TakingFlankingDamageTagLookup;
        private ComponentLookup<TakingFireDamage> TakingFireDamageTagLookup;
        private ComponentLookup<ArmyLossesPenaltyTag> ArmyLossesPenaltyTagLookup;
        private ComponentLookup<SanguineCourtRaceTag> SanguineCourtRaceTagLookup;
        private ComponentLookup<RallyingTag> RallyingTagLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattlePhase>();
            IsTerrifiedTagLookup = state.GetComponentLookup<IsTerrified>(true);
            HealthLossPercentTagLookup = state.GetComponentLookup<HealthLossPercent>(true);
            RetreatingNearbyAlliesTagLookup = state.GetComponentLookup<RetreatingNearbyAllies>(true);
            TakingFlankingDamageTagLookup = state.GetComponentLookup<TakingFlankingDamage>(true);
            TakingFireDamageTagLookup = state.GetComponentLookup<TakingFireDamage>(true);
            ArmyLossesPenaltyTagLookup = state.GetComponentLookup<ArmyLossesPenaltyTag>(true);
            SanguineCourtRaceTagLookup = state.GetComponentLookup<SanguineCourtRaceTag>(true);
            RallyingTagLookup = state.GetComponentLookup<RallyingTag>(true);
        }

        // Not [BurstCompile]: reads the managed RaceBonusRuleData.SanguineCourt.ImmuneToFlankMorale
        // toggle on the main thread to thread it into the (still-Bursted) MoraleUpdateJob. Only the
        // scheduling wrapper is de-Bursted - the per-entity morale math stays in the Bursted job.
        public void OnUpdate(ref SystemState state)
        {
            IsTerrifiedTagLookup.Update(ref state);
            HealthLossPercentTagLookup.Update(ref state);
            RetreatingNearbyAlliesTagLookup.Update(ref state);
            TakingFlankingDamageTagLookup.Update(ref state);
            TakingFireDamageTagLookup.Update(ref state);
            ArmyLossesPenaltyTagLookup.Update(ref state);
            SanguineCourtRaceTagLookup.Update(ref state);
            RallyingTagLookup.Update(ref state);

            state.Dependency = new MoraleUpdateJob {
                DeltaTime = SystemAPI.Time.DeltaTime,
                IsTerrifiedTagLookup = IsTerrifiedTagLookup,
                HealthLossPercentTagLookup = HealthLossPercentTagLookup,
                RetreatingNearbyAlliesTagLookup = RetreatingNearbyAlliesTagLookup,
                TakingFlankingDamageTagLookup = TakingFlankingDamageTagLookup,
                TakingFireDamageTagLookup = TakingFireDamageTagLookup,
                ArmyLossesPenaltyTagLookup = ArmyLossesPenaltyTagLookup,
                SanguineCourtRaceTagLookup = SanguineCourtRaceTagLookup,
                RallyingTagLookup = RallyingTagLookup,
                SanguineImmuneToFlank = RaceBonusRuleData.SanguineCourt.ImmuneToFlankMorale
            }.ScheduleParallel(state.Dependency);
        }
    }

    [WithAbsent(typeof(BrokenSquadTag))]
    [WithAbsent(typeof(GarrisonGateSquadTag))]
    public partial struct MoraleUpdateJob : IJobEntity
    {
        public float DeltaTime;
        [ReadOnly] public ComponentLookup<IsTerrified> IsTerrifiedTagLookup;
        [ReadOnly] public ComponentLookup<HealthLossPercent> HealthLossPercentTagLookup;
        [ReadOnly] public ComponentLookup<RetreatingNearbyAllies> RetreatingNearbyAlliesTagLookup;
        [ReadOnly] public ComponentLookup<TakingFlankingDamage> TakingFlankingDamageTagLookup;
        [ReadOnly] public ComponentLookup<TakingFireDamage> TakingFireDamageTagLookup;
        [ReadOnly] public ComponentLookup<ArmyLossesPenaltyTag> ArmyLossesPenaltyTagLookup;
        [ReadOnly] public ComponentLookup<SanguineCourtRaceTag> SanguineCourtRaceTagLookup;
        [ReadOnly] public ComponentLookup<RallyingTag> RallyingTagLookup;
        public bool SanguineImmuneToFlank;
        public const float RECENT_HEALTH_LOSS_PENALTY = 0.075f;
        public const float NO_RECENT_HEALTH_LOSS_REGENERATION = 2f;
        public const float MORALE_TOTAL_HEAL_DEPLETION_PENALTY = 0.75f;
        public const float MORALE_FLANK_PENALTY = 1f;
        public const float MORALE_THREAT_PENALTY = 0.2f;
        public const float MORALE_FIRE_DAMAGE_PENALTY = 0.25f;
        public const float MORALE_RETREATING_ALLIES_PENALTY = 0.2f;
        public const float MORALE_ARMY_LOSSES_PENALTY = 1.75f;
        public const float MORALE_WINNING_BONUS = 0.5f;
        public const float MORALE_LOSING_PENALTY = -0.01f;
        public const float WAVERING_THRESHOLD = 0.45f;

        void Execute(Entity entity, ref MoraleComponent morale, ref SquadStateComponent squadStateComponent)
        {
            bool isTerrified = IsTerrifiedTagLookup.IsComponentEnabled(entity);
            float healthLossPercent = HealthLossPercentTagLookup[entity].Value;
            // UnityEngine.Debug.Log($"Health Loss Percent for Entity {entity.Index}: {healthLossPercent}%");
            float healthPercentage = (float)squadStateComponent.CurrentHealthValue / (float)squadStateComponent.MaxHealthValue;
            bool hasRetreatingAllies = RetreatingNearbyAlliesTagLookup.IsComponentEnabled(entity);
            bool takingFlankingDamage = TakingFlankingDamageTagLookup.IsComponentEnabled(entity);
            bool takingFireDamage = TakingFireDamageTagLookup.IsComponentEnabled(entity);
            bool isWinning = HealthLossPercentTagLookup[entity].CombatStatus == CombatStatus.Winning;
            bool isLosing = HealthLossPercentTagLookup[entity].CombatStatus == CombatStatus.Losing;
            bool armyLosses = ArmyLossesPenaltyTagLookup.IsComponentEnabled(entity);
            // Debug.Log($"squad {entity.Index} armyLosses: {armyLosses}");

            float totalModifier = 0f;

            if(healthLossPercent > 0f) {
                totalModifier -= RECENT_HEALTH_LOSS_PENALTY * healthLossPercent; // Recent casualties impact
            } else {
                totalModifier += NO_RECENT_HEALTH_LOSS_REGENERATION; // No recent casualties = morale regen
            }
            
            totalModifier -= MORALE_TOTAL_HEAL_DEPLETION_PENALTY * (1f - healthPercentage);  // More casualties = bigger penalty
            totalModifier -= MORALE_FLANK_PENALTY * (takingFlankingDamage && !(SanguineImmuneToFlank && SanguineCourtRaceTagLookup.HasComponent(entity)) ? 1f : 0f); // Flanked penalty (Sanguine Court immune unless a mod disables it)
            totalModifier -= MORALE_RETREATING_ALLIES_PENALTY * (hasRetreatingAllies ? 1f : 0f); // Nearby allies retreating penalty
            totalModifier -= MORALE_THREAT_PENALTY * (isTerrified ? 1f : 0f); // Terrified penalty
            totalModifier -= MORALE_ARMY_LOSSES_PENALTY * (armyLosses ? 1f : 0f); // Army losses penalty
            totalModifier += MORALE_WINNING_BONUS * (isWinning ? 1f : 0f); // Winning bonus
            totalModifier += MORALE_LOSING_PENALTY * (isLosing ? 1f : 0f); // Losing penalty
            totalModifier -= MORALE_FIRE_DAMAGE_PENALTY * (takingFireDamage ? 1f : 0f); // Taking fire damage penalty

            // Rally spell aura - present only while the squad stands inside the spell's radius.
            // Added before the global modifier below so it scales like every other term.
            if(RallyingTagLookup.HasComponent(entity)) {
                totalModifier += RallyingTagLookup[entity].MoralePerSecond;
            }

            totalModifier *= TabletopTavernConstants.MORALE_LOSS_MODIFIER;

            // Debug.Log($"totalModifier: {totalModifier}");

            morale.CurrentMorale = Mathf.Clamp(morale.CurrentMorale + totalModifier * DeltaTime, 0f, morale.MaxMorale);

            if (morale.CurrentMorale <= morale.MoraleThreshold)
            {
                morale.MoraleState = 2; // Broken
            }
            else if (morale.CurrentMorale <= morale.MaxMorale * WAVERING_THRESHOLD)
            {
                morale.MoraleState = 1; // Wavering
            }
            else
            {
                morale.MoraleState = 0; // Steady
            }
        }
    }
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MoraleSystem))]
    public partial struct BrokenSquadTaggingSystem : ISystem
    {
        private EntityQuery _squadQuery;

        public void OnCreate(ref SystemState state)
        {
            _squadQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<MoraleComponent>(),
                ComponentType.Exclude<BrokenSquadTag>()
            );
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var job = new AddBrokenSquadTagJob {
                ECB = entityCommandBuffer
            };

            state.Dependency = job.ScheduleParallel(_squadQuery, state.Dependency);
        }
    }
    [BurstCompile]
    public partial struct AddBrokenSquadTagJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;

        void Execute(Entity entity, [EntityIndexInQuery] int sortKey, ref MoraleComponent morale)
        {
            if (morale.MoraleState == 2) {
                ECB.AddComponent<BreakSquadTag>(sortKey, entity);
                ECB.AddComponent<AlertNearbyUnitsOfBreakingTag>(sortKey, entity);
            }
        }
    }
}