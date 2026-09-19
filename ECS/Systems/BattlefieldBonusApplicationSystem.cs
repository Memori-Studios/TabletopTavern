using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Unity.Mathematics;

partial struct BattlefieldBonusApplicationSystem : ISystem
{
    public Unity.Mathematics.Random random;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // state.RequireForUpdate<BattlePhase>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        double elapsedTime = SystemAPI.Time.ElapsedTime;

        foreach (var (battlefieldBonusApplicator, applicatorEntity) in SystemAPI.Query<RefRW<BattlefieldBonusApplicator>>().WithEntityAccess())
        {
            // Stamp a relative Lifetime (set by e.g. a spell) into an absolute deadline once,
            // so every squad granted this bonus - even ones that join later - expires in sync.
            if (battlefieldBonusApplicator.ValueRO.Lifetime > 0f && battlefieldBonusApplicator.ValueRO.BattlefieldBonus.ExpiresAtTime == 0)
            {
                BattlefieldBonus bonus = battlefieldBonusApplicator.ValueRO.BattlefieldBonus;
                bonus.ExpiresAtTime = elapsedTime + battlefieldBonusApplicator.ValueRO.Lifetime;
                battlefieldBonusApplicator.ValueRW.BattlefieldBonus = bonus;
            }

            if (battlefieldBonusApplicator.ValueRO.BattlefieldBonus.ExpiresAtTime > 0
                && elapsedTime >= battlefieldBonusApplicator.ValueRO.BattlefieldBonus.ExpiresAtTime)
            {
                ecb.DestroyEntity(applicatorEntity);
                continue;
            }

            //only fire if timer is up
            battlefieldBonusApplicator.ValueRW.Timer -= SystemAPI.Time.DeltaTime;
            if (battlefieldBonusApplicator.ValueRO.Timer > 0f)
            {
                continue;
            }
            battlefieldBonusApplicator.ValueRW.Timer = battlefieldBonusApplicator.ValueRO.TimerMax;

            foreach (var (squadMovement2, squadEntity) in SystemAPI.Query<RefRO<SquadMovementComponent>, SquadEntity>())
            {
                //check if target team
                if (battlefieldBonusApplicator.ValueRO.BattlefieldBonus.Team != Team.Neutral && squadEntity.Team != battlefieldBonusApplicator.ValueRO.BattlefieldBonus.Team) continue;

                float distance = math.distance(battlefieldBonusApplicator.ValueRO.BattlefieldBonus.OriginationPoint, squadMovement2.ValueRO.SquadCenter);
                // Debug.Log($"BattlefieldBonusApplicationSystem: Distance between is {distance}");
                if (distance > battlefieldBonusApplicator.ValueRO.BattlefieldBonus.Range)
                {
                    continue;
                }

                //check if has buffer element
                if (!SystemAPI.HasBuffer<BattlefieldBonusBufferElement>(squadEntity.SelfEntity))
                {
                    continue;
                }

                //get bonus buffer
                DynamicBuffer<BattlefieldBonusBufferElement> bonusBuffer = SystemAPI.GetBuffer<BattlefieldBonusBufferElement>(squadEntity.SelfEntity);

                bool hasBonus = false;
                //check if already has bonus 
                foreach (BattlefieldBonusBufferElement bonusBufferElement in bonusBuffer)
                {
                    if (bonusBufferElement.Value.Guid == battlefieldBonusApplicator.ValueRO.BattlefieldBonus.Guid)
                    {
                        hasBonus = true;
                        break;
                    }
                }

                if (hasBonus) continue;

                // Rain only slows large units
                if (battlefieldBonusApplicator.ValueRO.BattlefieldBonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Rain
                    && !SystemAPI.HasComponent<LargeTag>(squadEntity.SelfEntity))
                    continue;

                //apply bonus
                bonusBuffer.Add(new BattlefieldBonusBufferElement { Value = battlefieldBonusApplicator.ValueRO.BattlefieldBonus });

                // A multi-stat spell lands several applicators with the same id; Set refreshes instead of stacking.
                int statusSpellId = battlefieldBonusApplicator.ValueRO.BattlefieldBonus.StatusSpellId;
                if (statusSpellId > 0 && SystemAPI.HasBuffer<SpellStatusBufferElement>(squadEntity.SelfEntity))
                {
                    SpellStatus.Set(SystemAPI.GetBuffer<SpellStatusBufferElement>(squadEntity.SelfEntity),
                        statusSpellId, battlefieldBonusApplicator.ValueRO.BattlefieldBonus.ExpiresAtTime, elapsedTime);
                }
                // Debug.Log($"BattlefieldBonusApplicationSystem: Applied bonus {battlefieldBonusApplicator.ValueRO.BattlefieldBonus.UnitStat} to {squadEntity.SelfEntity}");
            }
        }
    }
}