using Unity.Burst;
using Unity.Entities;
using UnityEngine;

partial struct UnitPrestigeSystemSetUpSystem : ISystem
{
    // [BurstCompile]
    public void OnUpdate(ref SystemState state) {

        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (
            MeleeAttack,
            MeleeDefense,
            UnitPrestigeSetUpTag,
            unit,
            entity
        ) in SystemAPI.Query<
            RefRW<MeleeAttack>,
            RefRW<MeleeDefense>,
            RefRO<UnitPrestigeSetUpTag>,
            RefRO<Unit>
        >().WithEntityAccess()) {

            // Hybrids carry RangedFireModeUnitComponent too, so the component check alone no longer
            // separates a pure shooter from a unit that shoots but takes melee prestige.
            bool isPureRanged = !TabletopTavernConstants.FightsInMelee(unit.ValueRO.unitType)
                             && entityManager.HasComponent<RangedFireModeUnitComponent>(entity);
            if(TabletopTavernConstants.Casts(unit.ValueRO.unitType)) {
                // Range only. A placed AoE has no to-hit roll, so Accuracy would be dead weight
                // here, and melee stats are not what a caster is prestiged for. Leadership is
                // granted in SquadManager and charges in EntityWatcher, matching where those two
                // are already handled for every other unit type.
                // This branch has to come first: isPureRanged is a conjunction, and a mage has no
                // RangedFireModeUnitComponent, so it reads false and would otherwise fall into the
                // melee else below and quietly collect MeleeAttack and MeleeDefense instead.
                MageCast mageCast = entityManager.GetComponentData<MageCast>(entity);
                mageCast.Range += TabletopTavernConstants.PRESTIGE_BONUS * UnitPrestigeSetUpTag.ValueRO.PrestigeLevel;
                entityManager.SetComponentData(entity, mageCast);
            } else if(isPureRanged) {
                ShootAttack ShootAttack = entityManager.GetComponentData<ShootAttack>(entity);
                ShootAttack.Range += TabletopTavernConstants.PRESTIGE_BONUS * UnitPrestigeSetUpTag.ValueRO.PrestigeLevel;
                ShootAttack.Accuracy += TabletopTavernConstants.PRESTIGE_BONUS * UnitPrestigeSetUpTag.ValueRO.PrestigeLevel;

                entityManager.SetComponentData(entity, ShootAttack);
            } else {
                MeleeAttack.ValueRW.MeleeAttackValue += TabletopTavernConstants.PRESTIGE_BONUS * UnitPrestigeSetUpTag.ValueRO.PrestigeLevel;
                MeleeDefense.ValueRW.Value += TabletopTavernConstants.PRESTIGE_BONUS * UnitPrestigeSetUpTag.ValueRO.PrestigeLevel;
            }

            // Granted trait tags (ArmorPiercingTag/AntiInfantryTag/AntiLargeTag) and gear bonuses that
            // key off them (e.g. Glaives) are now applied in UnitSetUpSystem, which merges GrantedTrait
            // into SquadAttributes before computing gear/attribute bonuses. Doing it here instead was too
            // late: this system only runs once MeleeAttack exists (i.e. after UnitSetUpSystem already
            // baked WeaponStrength), so gear bonuses gated on the granted trait were silently dropped.

            entityCommandBuffer.RemoveComponent<UnitPrestigeSetUpTag>(entity);
        }
    }
}