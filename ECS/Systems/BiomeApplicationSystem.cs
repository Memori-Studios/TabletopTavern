using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using TJ;

partial struct BiomeApplicationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SquadStatsData>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (squad, BattlefieldBonusBufferElement, ApplyBiomeBonus)
            in SystemAPI.Query<
                SquadEntity,
                DynamicBuffer<BattlefieldBonusBufferElement>,
                ApplyBiomeBonusTag
            >())
        {
            float swampSpeedModifier = TabletopTavernConstants.SWAMP_SPEED_MODIFIER;
            if(ApplyBiomeBonus.IgnoresSwamp)
            {
                swampSpeedModifier = 0f;
            }
            
            BattlefieldBonusEnum battlefieldBonusEnum = ApplyBiomeBonus.BattlefieldBonusEnum;
            BattlefieldBonus bonus = new ()
            {
                BattlefieldBonusEnum = battlefieldBonusEnum,
                UnitStat = battlefieldBonusEnum == BattlefieldBonusEnum.Swamp ? UnitStat.Speed : UnitStat.None,
                Team = Team.Neutral,
                Value = battlefieldBonusEnum == BattlefieldBonusEnum.Swamp ? swampSpeedModifier : 0f,
                Applied = false,
                Range = 1000f,
                Guid = ApplyBiomeBonus.Guid,
                OriginationPoint = SystemAPI.GetComponent<LocalTransform>(squad.SelfEntity).Position,
                TargetedUnit = squad.SquadId
            };
            BattlefieldBonusBufferElement.Add(new BattlefieldBonusBufferElement { Value = bonus });

            entityCommandBuffer.RemoveComponent<ApplyBiomeBonusTag>(squad.SelfEntity);
        }
    }
}