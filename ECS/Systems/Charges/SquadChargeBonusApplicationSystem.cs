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
    // collections, LocalizationManager). Runs once per landed charge per squad (gated by
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

            SquadStats squadStats = statsBlob.GetStats(squad.UnitName);
            int bonus = squadStats.ChargeBonus;
            BattleHeroContext hero = BattleHeroContext.For(campaignSaveDataHolder, squad.Team);
            if (hero.HasHero)
            {
                // Rule data lives in HeroBonusRuleData (Components assembly) - HeroBonusManager
                // itself (main assembly) isn't visible from here.
                float heroBonus = HeroBonusRuleEvaluator.SumHeroStatBonus(UnitStat.ChargeBonus, squad.UnitName, hero.HeroID, squadStats, hero.EnemyRace, bonus);
                if (hero.OnlySakuraUnits)
                    heroBonus += HeroBonusRuleEvaluator.SumFactionStatBonus(UnitStat.ChargeBonus, hero.HeroRace, bonus);
                bonus += (int)heroBonus;
            }

            // Rally the Banners adds flat; a side or rear charge then scales the whole bonus.
            bonus = (int)((bonus + ApplyChargeBonusTag.FlatBonus) * ApplyChargeBonusTag.Multiplier);

            // Blunted Charge: the player's whole charge bonus is halved, Rally the Banners included.
            if (squad.Team == Team.Player && !campaignSaveDataHolder.IsCustomBattle && OrdealMask.Has(campaignSaveDataHolder.OrdealMask, OrdealId.BluntedCharge))
                bonus /= 2;

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

