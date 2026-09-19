using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;

namespace TJ
{
    public partial struct ApplyDamageSystem : ISystem
    {
        private Unity.Mathematics.Random _random;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _random = new Unity.Mathematics.Random(1);
            state.RequireForUpdate<BattleHasStarted>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            EntityManager entityManager = state.EntityManager;

            foreach (var (health, maxHealth, damageBuffer, entityTeam, parentSquadEntity, damageReceivingEntity) in SystemAPI.Query<
            RefRW<Health>,
            MaxHitPoints,
            DynamicBuffer<DamageBufferElement>,
            EntityTeam,
            UnitParentEntityTag
            >().WithEntityAccess()
               .WithOptions(EntityQueryOptions.FilterWriteGroup))
            {
                var damageHitPoints = 0;
                var healingHitPoints = 0;
                int maxDamageSquadId = 100;
                int maxDamageAmount = 0;

                Unit unitTakingDamage = SystemAPI.GetComponent<Unit>(damageReceivingEntity);
                bool infantry = SystemAPI.HasComponent<InfantryTag>(damageReceivingEntity);
                bool large = SystemAPI.HasComponent<LargeTag>(damageReceivingEntity);
                bool armored = SystemAPI.HasComponent<ArmoredTag>(damageReceivingEntity);
                bool artillery = SystemAPI.HasComponent<ArtilleryUnit>(damageReceivingEntity);

                bool hasDamageBuffer = SystemAPI.TryGetSingletonBuffer<SquadDamageBufferElement>(out var globalDamageBuffer);

                foreach (var damageElement in damageBuffer)
                {
                    // Debug.Log($"ApplyDamageSystem: Applying damage to {unitTakingDamage.unitName} with {damageElement.AttackStrength} hit points of type {damageElement.DamageType} from {damageElement.DamageSource} antilarget: {damageElement.DamageAttributes}");

                    // Debug.Log($"ApplyDamageSystem: Infantry: {infantry}, Large: {large}, Armored: {armored}");
                    int attackHitPoints = 0;


                    switch (damageElement.DamageType)
                    {
                        case DamageType.Physical:
                            // Ignore friendly fire (Neutral source bypasses team checks entirely - hits everyone)
                            if (damageElement.TeamOfSource != Team.Neutral && entityTeam.Value == damageElement.TeamOfSource) continue;
                            var physicalHitPoints = damageElement.AttackStrength;
                            bool flankAttack =
                                physicalHitPoints > 0 &&
                                damageElement.DamageSource == DamageSource.Melee &&
                                damageElement.FlankAttack;
                            if(flankAttack && entityManager.HasComponent<TakingFlankingDamage>(parentSquadEntity.parentSquadEntity)) {
                                TakingFlankingDamage TakingFlankingDamage = entityManager.GetComponentData<TakingFlankingDamage>(parentSquadEntity.parentSquadEntity);
                                TakingFlankingDamage.RecentlyTookDamage = true;
                                entityManager.SetComponentData(parentSquadEntity.parentSquadEntity, TakingFlankingDamage);
                            }

                            if(damageElement.Flaming && entityManager.HasComponent<TakingFireDamage>(parentSquadEntity.parentSquadEntity))
                            {
                                TakingFireDamage TakingFireDamage = entityManager.GetComponentData<TakingFireDamage>(parentSquadEntity.parentSquadEntity);
                                TakingFireDamage.RecentlyTookDamage = true;
                                entityManager.SetComponentData(parentSquadEntity.parentSquadEntity, TakingFireDamage);
                            }

                            if (SystemAPI.HasComponent<PhysicalDamageMultiplier>(damageReceivingEntity))
                            {
                                var physicalDamageMultiplier = SystemAPI.GetComponent<PhysicalDamageMultiplier>(damageReceivingEntity).Value;
                                physicalHitPoints = (int)(physicalHitPoints * physicalDamageMultiplier);
                            }

                            if(armored) {
                                float armorMitigation = SystemAPI.GetComponent<ArmoredTag>(damageReceivingEntity).ArmorMitigation;
                                bool isArmorPiercing = damageElement.DamageAttributes == DamageAttributes.ArmorPiercing ||
                                                       damageElement.DamageAttributes == DamageAttributes.ArmorPiercingAntiInfantry ||
                                                       damageElement.DamageAttributes == DamageAttributes.ArmorPiercingAntiLarge;
                                if (isArmorPiercing) armorMitigation *= 0.5f;
                                physicalHitPoints -= (int)(physicalHitPoints * armorMitigation);
                            }
                            if(infantry) {
                                if( damageElement.DamageAttributes == DamageAttributes.AntiInfantry ||
                                    damageElement.DamageAttributes == DamageAttributes.ArmorPiercingAntiInfantry) {
                                    physicalHitPoints *= 2;
                                }
                            }
                            if(large) {
                                if( damageElement.DamageAttributes == DamageAttributes.AntiLarge ||
                                    damageElement.DamageAttributes == DamageAttributes.ArmorPiercingAntiLarge) {
                                        // Debug.Log($"ApplyDamageSystem: Large unit hit with anti-large damage, doubling damage.");
                                    physicalHitPoints *= 2;
                                }
                            }

                            if (artillery && damageElement.DamageSource == DamageSource.Melee)
                                physicalHitPoints *= 2;

                            attackHitPoints += physicalHitPoints;
                            break;

                        case DamageType.Magical:

                            // Ignore friendly fire (Neutral source bypasses team checks entirely - hits everyone)
                            if (damageElement.TeamOfSource != Team.Neutral && entityTeam.Value == damageElement.TeamOfSource) continue;
                            var magicHitPoints = damageElement.AttackStrength;
                            if (SystemAPI.HasComponent<MagicalDamageMultiplier>(damageReceivingEntity))
                            {
                                var magicDamageMultiplier = SystemAPI.GetComponent<MagicalDamageMultiplier>(damageReceivingEntity).Value;
                                magicHitPoints = (int)(magicHitPoints * magicDamageMultiplier);
                            }
                            attackHitPoints += magicHitPoints;
                            break;

                        case DamageType.Healing:

                            // Only apply healing if coming from the same team (Neutral source heals everyone)
                            if (damageElement.TeamOfSource != Team.Neutral && entityTeam.Value != damageElement.TeamOfSource) continue;
                            healingHitPoints += damageElement.AttackStrength;
                            break;

                        default:
                            Debug.LogWarning($"Warning: attempting to apply undefined damage type: {damageElement.DamageType}");
                            break;
                    }

                    // One global knob per source, and only Ranged runs the on-hit rolls. The default
                    // warns rather than picking a knob: a source added without a case here used to
                    // inherit the melee quarter silently, which is exactly what spells did until TT-78.
                    switch (damageElement.DamageSource)
                    {
                    case DamageSource.Ranged:
                    {
                        attackHitPoints = (int)(attackHitPoints * TabletopTavernConstants.RANGED_TOTAL_DAMAGE_MODIFIER);

                        if (SystemAPI.HasComponent<MissileResistance>(damageReceivingEntity))
                        {
                            attackHitPoints = (int)(attackHitPoints * SystemAPI.GetComponent<MissileResistance>(damageReceivingEntity).DamageMultiplier);
                        }

                        bool shielded = SystemAPI.HasComponent<Shield>(damageReceivingEntity);
                        float shieldBlockChance = shielded ? SystemAPI.GetComponent<Shield>(damageReceivingEntity).ShieldBlockChance : 0f;
                        bool isInForest = SystemAPI.HasComponent<InForestTag>(damageReceivingEntity);
                        // Debug.Log($"damageElement.FlankAttack = {damageElement.FlankAttack}");
                        if (shielded && damageElement.FlankAttack == false)
                        {
                            if (_random.NextFloat() < shieldBlockChance)
                            {
                                //add a shield block component
                                ecb.AddComponent(damageReceivingEntity, new BlockedArrowTag());
                                attackHitPoints = 0;
                            }
                        }

                        if (isInForest)
                        {
                            // Debug.Log($"ApplyDamageSystem: {damageReceivingEntity.Index} is in forest, applying forest damage reduction");
                            if (_random.NextFloat() > 0.75f)
                            {
                                attackHitPoints = 0;
                            }
                        }
                        break;
                    }
                    case DamageSource.Spell:
                        // Face value. Magical already skipped armour above (direct-damage spells are
                        // fully armour-piercing), and a spell is not an arrow: no shield or forest roll.
                        attackHitPoints = (int)(attackHitPoints * TabletopTavernConstants.SPELL_TOTAL_DAMAGE_MODIFIER);
                        break;
                    case DamageSource.Melee:
                        attackHitPoints = (int)(attackHitPoints * TabletopTavernConstants.MELEE_TOTAL_DAMAGE_MODIFIER);
                        break;
                    default:
                        Debug.LogWarning($"ApplyDamageSystem: no damage modifier for DamageSource {damageElement.DamageSource}; applying it unscaled.");
                        break;
                    }


                    damageHitPoints += attackHitPoints;

                    if (attackHitPoints > 0)
                    {
                        if (attackHitPoints > maxDamageAmount)
                        {
                            maxDamageAmount = attackHitPoints;
                            maxDamageSquadId = damageElement.DamageSourceSquadId;
                        }
                        if (hasDamageBuffer)
                            globalDamageBuffer.Add(new SquadDamageBufferElement { SquadId = damageElement.DamageSourceSquadId, DamageAmount = attackHitPoints });
                    }
                }

                damageBuffer.Clear();


                var totalHitPoints = 0 - (damageHitPoints) + healingHitPoints;
                if (totalHitPoints == 0) continue;

                // Debug.Log($"ApplyDamageSystem: Applying damage to {damageReceivingEntity} with {totalHitPoints}");

                health.ValueRW.Value += totalHitPoints;
                // Clamp at 0 too: an overkilled unit's negative health is summed into the army total for a frame.
                health.ValueRW.Value = math.clamp(health.ValueRO.Value, 0, maxHealth.Value);
                health.ValueRW.onHealthChanged = true;

                if (damageHitPoints > 0 && maxDamageSquadId == 100)
                {
                    Debug.LogError($"ApplyDamageSystem: maxDamageSquadId is 100, this means damage was applied without a valid source. Check if damage is being added to the DamageBuffer without setting the DamageSourceSquadId.");
                }

                if (health.ValueRO.Value <= 0)
                {
                    // Debug.Log($"Unit {damageReceivingEntity} has been slain");
                    ecb.AddComponent(damageReceivingEntity,
                        new UnitRemovedFromSquad { Entity = damageReceivingEntity, SquadId = unitTakingDamage.squadId, KilledBySquadId = maxDamageSquadId });
                } else if(_random.NextFloat() > 0.5f) {
                    //chance to play damage sfx
                    DynamicBuffer<SFXBufferElement> sfxBuffer = SystemAPI.GetBuffer<SFXBufferElement>(damageReceivingEntity);
                    sfxBuffer.Add(new SFXBufferElement { UnitName = unitTakingDamage.unitName, SFXEntityType = Memori.Audio.SFXEntityType.Death, MaxDistance = 30f });
                }

                //blood vfx
                if (SystemAPI.TryGetSingletonBuffer<BloodBufferElement>(out var BloodBufferElement))
                {
                    BloodBufferElement.Add(new BloodBufferElement
                    {
                        Position = SystemAPI.GetComponent<LocalTransform>(damageReceivingEntity).Position
                    });
                }

                // var buffer = EntityManager.GetBuffer<FlankDamageEvent>(parentSquadEntity.parentSquadEntity);
                // buffer.Add(new FlankDamageEvent { Time = SystemAPI.Time.ElapsedTime });
            }
        }
    }
}
