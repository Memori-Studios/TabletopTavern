using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using TJ;

partial struct SquadChargeBonusApplicationSystem : ISystem
{

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        state.RequireForUpdate<SquadStatsData>();
    }
    // Not Burst-compiled: applying hero ChargeBonus rules calls into HeroBonusManager (managed
    // collections, LocalizationManager). Runs once per charge-start event per squad (gated by
    // ApplyChargeBonusTag, removed after processing) - not a per-frame hot path.
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        CampaignSaveDataHolder campaignSaveDataHolder = SystemAPI.GetSingleton<CampaignSaveDataHolder>();
        var statsData = SystemAPI.GetSingleton<SquadStatsData>();
        ref var statsBlob = ref statsData.StatsBlob.Value; 
        
        foreach (var (squad, BattlefieldBonusBufferElement, ApplyChargeBonusTag) 
            in SystemAPI.Query<
                SquadEntity,
                DynamicBuffer<BattlefieldBonusBufferElement>,
                ApplyChargeBonusTag
            >())
        {
            entityCommandBuffer.RemoveComponent<ApplyChargeBonusTag>(squad.SelfEntity);
            // Debug.Log($"SquadChargeBonusApplicationSystem: applying charge bonus to squad {squad.SquadId}");

            // Rally the Banners: carries the squad through terrain that would otherwise cancel the
            // charge outright, and adds to the impact. Read before the suppression check so the
            // exemption applies.
            bool empowered = SystemAPI.HasComponent<ChargeEmpoweredTag>(squad.SelfEntity);

            if (!empowered &&
               (SystemAPI.HasComponent<InForestTag>(squad.SelfEntity) ||
                SystemAPI.HasComponent<InSwampTag>(squad.SelfEntity) ||
                SystemAPI.HasComponent<InRainTag>(squad.SelfEntity)))
            {
                Debug.LogWarning($"SquadChargeBonusApplicationSystem: Squad {squad.SquadId} is in forest or swamp or rain, dont apply charge bonus.");
                continue;
            }

            if (squad.TargetSquadEntity != Entity.Null && SystemAPI.HasComponent<GarrisonGateSquadTag>(squad.TargetSquadEntity))
            {
                Debug.LogWarning($"SquadChargeBonusApplicationSystem: Squad {squad.SquadId} is charging a gate, dont apply charge bonus.");
                continue;
            }

            SquadStats squadStats = statsBlob.GetStats(squad.UnitName);
            int bonus = squadStats.ChargeBonus;
            if (squad.Team == Team.Player && campaignSaveDataHolder.ActiveHeroID != -1)
            {
                // Rule data lives in HeroBonusRuleData (Components assembly) - HeroBonusManager
                // itself (main assembly) isn't visible from here. No FactionBonusRule targets
                // ChargeBonus today, so unlike UnitSetUpSystem this doesn't need the Sakura check.
                bonus += (int)HeroBonusRuleEvaluator.SumHeroStatBonus(UnitStat.ChargeBonus, squad.UnitName, campaignSaveDataHolder.ActiveHeroID, squadStats, campaignSaveDataHolder.EnemyRace, bonus);
            }

            if (empowered)
                bonus += (int)SystemAPI.GetComponent<ChargeEmpoweredTag>(squad.SelfEntity).BonusImpact;

            //apply bonus
            BattlefieldBonusBufferElement.Add(new BattlefieldBonusBufferElement { 
                Value = new BattlefieldBonus { 
                    UnitStat = UnitStat.MeleeAttack, 
                    BattlefieldBonusEnum = BattlefieldBonusEnum.ChargeBonus,
                    Team = Team.Neutral,
                    Value = bonus,
                    Range = Mathf.Infinity,
            } });
            BattlefieldBonusBufferElement.Add(new BattlefieldBonusBufferElement { 
                Value = new BattlefieldBonus { 
                    UnitStat = UnitStat.WeaponStrength, 
                    BattlefieldBonusEnum = BattlefieldBonusEnum.ChargeBonus,
                    Team = Team.Neutral,
                    Value = bonus,
                    Range = Mathf.Infinity,
            } });
        }     
    }
}

