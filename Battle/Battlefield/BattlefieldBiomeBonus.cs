using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using TJ;

public class BattlefieldBiomeBonus : MonoBehaviour
{
    public void ApplyBonus(BattlefieldBonusEnum biome, Entity squadEntity)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        bool ignoresSwamp = TabletopTavernData.IgnoresSwamp(BattleAttributes(squadEntity));
        ecb.AddComponent(squadEntity, new ApplyBiomeBonusTag { BattlefieldBonusEnum = biome, Guid = System.Guid.NewGuid(), IgnoresSwamp = ignoresSwamp });
        if(biome == BattlefieldBonusEnum.Swamp)
        {
            if (!ignoresSwamp)
            {
                BattleManager.Instance.SquadMovementManager.EnteringSwamp(squadEntity, true);
            }
        }
        else if (biome == BattlefieldBonusEnum.Forest)
        {
            EnteringForest(squadEntity, true);
        }

        ecb.Playback(entityManager);
        ecb.Dispose();
    }
    public void RemoveBonus(BattlefieldBonusEnum biome, Entity squadEntity)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityCommandBuffer ecb = new(Allocator.Temp);

        if (biome == BattlefieldBonusEnum.Swamp)
        {
            ecb.AddComponent<RemoveSwampTag>(squadEntity);
            if (!TabletopTavernData.IgnoresSwamp(BattleAttributes(squadEntity)))
            {
                BattleManager.Instance.SquadMovementManager.EnteringSwamp(squadEntity, false);
            }
        }
        else if (biome == BattlefieldBonusEnum.Forest)
        {
            ecb.AddComponent<RemoveForestTag>(squadEntity);
            EnteringForest(squadEntity, false);
        }

        ecb.Playback(entityManager);
        ecb.Dispose();
    }
    public void EnteringForest(Entity entity, bool isEntering)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (!BattleAttributes(entity).ForestDweller) return;

        var entityBuffer = entityManager.GetBuffer<EntityReferenceBufferElement>(entity);

        if (isEntering)
        {
            for (int j = 0; j < entityBuffer.Length; j++)
            {
                Entity unitEntity = entityBuffer[j].Entity;
                if (entityManager.Exists(unitEntity))
                {
                    if(entityManager.HasComponent<MeleeAttack>(unitEntity))
                    {
                        MeleeAttack MeleeAttack = entityManager.GetComponentData<MeleeAttack>(unitEntity);
                        MeleeAttack.MeleeAttackValue += 5;
                        entityManager.SetComponentData(unitEntity, MeleeAttack);
                    }
                    if(entityManager.HasComponent<ShootAttack>(unitEntity))
                    {
                        ShootAttack ShootAttack = entityManager.GetComponentData<ShootAttack>(unitEntity);
                        ShootAttack.damageAmount += 5;
                        entityManager.SetComponentData(unitEntity, ShootAttack);
                    }
                }
            }
        }
        else
        {
            for (int j = 0; j < entityBuffer.Length; j++)
            {
                Entity unitEntity = entityBuffer[j].Entity;
                if (entityManager.Exists(unitEntity))
                {
                    if (entityManager.HasComponent<MeleeAttack>(unitEntity))
                    {
                        MeleeAttack MeleeAttack = entityManager.GetComponentData<MeleeAttack>(unitEntity);
                        MeleeAttack.MeleeAttackValue -= 5;
                        entityManager.SetComponentData(unitEntity, MeleeAttack);
                    }
                    if (entityManager.HasComponent<ShootAttack>(unitEntity))
                    {
                        ShootAttack ShootAttack = entityManager.GetComponentData<ShootAttack>(unitEntity);
                        ShootAttack.damageAmount -= 5;
                        entityManager.SetComponentData(unitEntity, ShootAttack);
                    }
                }
            }
        }
    }
    private static SquadAttributes BattleAttributes(Entity squadEntity)
    {
        SquadEntity squad = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<SquadEntity>(squadEntity);
        return BattleManager.Instance.SquadManager.GetBattleSquadAttributes(squad.UnitName, squad.SquadId);
    }
}