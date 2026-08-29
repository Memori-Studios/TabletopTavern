using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using TJ;
using Unity.Mathematics;
using Unity.Transforms;
using Memori.Audio;
using ProjectDawn.Navigation;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
partial struct UnitSetUpSystem : ISystem
{
    private Unity.Mathematics.Random _random;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CampaignSaveDataHolder>();
        state.RequireForUpdate<SquadStatsData>();
        _random = new Unity.Mathematics.Random(1);
    }

    // Not Burst-compiled: the hero-bonus block below calls into HeroBonusManager, which uses
    // managed collections and LocalizationManager, and the gear block calls GearData.GetGear()
    // (a Dictionary lookup, for mod-overridden GearModifierValue). This system runs once per
    // soldier entity at spawn time (gated by UnitStatsSetUpTag, removed after processing) - not
    // a per-frame hot path, so the cost of leaving this unBursted is negligible.
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        CampaignSaveDataHolder campaignSaveDataHolder = SystemAPI.GetSingleton<CampaignSaveDataHolder>();
        EntitiesReferences entitiesReferences = SystemAPI.GetSingleton<EntitiesReferences>();
        var statsData = SystemAPI.GetSingleton<SquadStatsData>();
        ref var statsBlob = ref statsData.StatsBlob.Value; 
        // Read once per update rather than per unit. Safe to call here: this system is deliberately
        // not Burst-compiled (see the note above OnUpdate).
        RangedFireMode playerDefaultFireMode = TabletopTavernConstants.GetPlayerDefaultFireMode();

        foreach (var ( 
            unit,
            UnitStatsSetUpTag,
            entity
        ) in SystemAPI.Query<
            RefRO<Unit>,
            RefRO<UnitStatsSetUpTag>
        >().WithEntityAccess()) {
            
            SquadStats squadStats = statsBlob.GetStats(unit.ValueRO.unitName);

            // Prestige-granted trait must be merged before any gear/attribute check below runs,
            // since gear like Glaives keys its WeaponStrength bonus off squadStats.SquadAttributes.
            if (entityManager.HasComponent<UnitPrestigeSetUpTag>(entity))
            {
                UnitAttribute grantedTrait = entityManager.GetComponentData<UnitPrestigeSetUpTag>(entity).GrantedTrait;
                if (grantedTrait != UnitAttribute.None)
                    TabletopTavernConstants.SetAttribute(ref squadStats.SquadAttributes, grantedTrait);
            }

            UnitAttributeSerialized unitAttributes = new();

            unitAttributes.ArmorPiercing = squadStats.SquadAttributes.ArmorPiercing;
            unitAttributes.AntiInfantry = squadStats.SquadAttributes.AntiInfantry;
            unitAttributes.AntiLarge = squadStats.SquadAttributes.AntiLarge;
            unitAttributes.Armored = squadStats.Armor > 0;
            unitAttributes.Large = squadStats.unitSize != UnitSize.Infantry;

            Team team = unit.ValueRO.Team;

            int meleeAttack = squadStats.MeleeAttack;
            int meleeDefense = squadStats.MeleeDefense;
            float accuracy = squadStats.attackAccuracy;
            float range = squadStats.BaseRange;
            int maxHitPoints = squadStats.HitPointsPerUnit;
            if(squadStats.unitType == UnitType.Structure) {
                maxHitPoints = UnitStatsSetUpTag.ValueRO.HealthOverride;
            }

            entityCommandBuffer.RemoveComponent<UnitStatsSetUpTag>(entity);

            float GetShieldBlockChance() {
                if (squadStats.SquadAttributes.StandardShields)
                {
                    return 0.35f; // 35% base block chance for shielded units
                }
                else if (squadStats.SquadAttributes.HeavyShields)
                {
                    return 0.70f; // 70% base block chance for heavy shielded units
                }
                // else if (squadStats.SquadAttributes.TowerShields)
                // {
                //     return 1f; // 100% block chance for tower shielded units
                // }

                return 0;
            }
            float shieldBlockChance = GetShieldBlockChance();
            int missileStrength = squadStats.MissileStrength;
            int weaponStrength = squadStats.WeaponStrength;
            float armor = squadStats.Armor;
            
            GearIDsSerialized gear = campaignSaveDataHolder.Gear;

            bool Contains(GearID gearID) {
                if(gear.gearID1 == gearID || gear.gearID2 == gearID || gear.gearID3 == gearID || gear.gearID4 == gearID) return true;
                return false;
            }

            //hero stuff - bonus magnitudes and conditions now come from HeroBonusRuleData (plus
            // any mod overrides, applied by HeroBonusManager in the main assembly and shared here
            // via HeroBonusRuleEvaluator) instead of a hardcoded switch, so mods can change them
            // and the values can't drift from what the UI shows. HeroBonusManager itself can't be
            // referenced from this assembly (TabletopTavern.Core.Systems doesn't reference the
            // main TabletopTavern.Core assembly), hence the separate evaluator. This system covers
            // exactly the stats it's responsible for: Leadership is applied in SquadManager,
            // ChargeBonus in SquadChargeBonusApplicationSystem, Ammunition in EntityWatcher. The
            // Sakura Dynasty mono-race army gate (OnlySakuraUnits) is unchanged - not yet
            // generalized to other races.
            if(campaignSaveDataHolder.ActiveHeroID != -1 && team == Team.Player)
            {
                float SumHeroBonus(UnitStat stat, float currentValue)
                {
                    float total = HeroBonusRuleEvaluator.SumHeroStatBonus(stat, unit.ValueRO.unitName, campaignSaveDataHolder.ActiveHeroID, squadStats, campaignSaveDataHolder.EnemyRace, currentValue);
                    if (campaignSaveDataHolder.OnlySakuraUnits)
                        total += HeroBonusRuleEvaluator.SumFactionStatBonus(stat, campaignSaveDataHolder.PlayerHeroRace, currentValue);
                    return total;
                }

                meleeAttack += (int)SumHeroBonus(UnitStat.MeleeAttack, meleeAttack);
                meleeDefense += (int)SumHeroBonus(UnitStat.MeleeDefense, meleeDefense);
                accuracy += SumHeroBonus(UnitStat.Accuracy, accuracy);
                weaponStrength += (int)SumHeroBonus(UnitStat.WeaponStrength, weaponStrength);
                armor += SumHeroBonus(UnitStat.Armor, armor);
                range += SumHeroBonus(UnitStat.Range, range);
                missileStrength += (int)SumHeroBonus(UnitStat.MissileStrength, missileStrength);
            }

            if(!campaignSaveDataHolder.IsCustomBattle && team == Team.Player)
            {
                if(Contains(GearID.DiamondTippedArrows) && squadStats.unitType == UnitType.Ranged) 
                    unitAttributes.ArmorPiercing = true;
                if(Contains(GearID.Turkey) && squadStats.unitType == UnitType.Ranged) 
                    unitAttributes.AntiLarge = true;
                if(Contains(GearID.HeavyWeapons) && squadStats.RarityTier == UnitRarity.Rare) 
                    squadStats.SquadAttributes.ArmorPiercing = true; 

                if(Contains(GearID.ArmingSwords) && TabletopTavernConstants.FightsInMelee(squadStats.unitType))
                    meleeAttack += GearData.GetGear(GearID.ArmingSwords).GearModifierValue;
                if(Contains(GearID.BucklerShields) && (squadStats.SquadAttributes.StandardShields || squadStats.SquadAttributes.HeavyShields))
                    meleeDefense += GearData.GetGear(GearID.BucklerShields).GearModifierValue;
                if(Contains(GearID.TowerShields) && (squadStats.SquadAttributes.StandardShields || squadStats.SquadAttributes.HeavyShields))
                    shieldBlockChance = (float)GearData.GetGear(GearID.TowerShields).GearModifierValue/100f;
                if(Contains(GearID.Longbows) && squadStats.unitType == UnitType.Ranged)
                    range += GearData.GetGear(GearID.Longbows).GearModifierValue;
                if(Contains(GearID.Glaives) && squadStats.SquadAttributes.AntiLarge)
                    weaponStrength += GearData.GetGear(GearID.Glaives).GearModifierValue;
                if(Contains(GearID.TexanBBQ) && TabletopTavernConstants.FightsInMelee(squadStats.unitType))
                    weaponStrength += GearData.GetGear(GearID.TexanBBQ).GearModifierValue;
                if(Contains(GearID.BallisticCharts))
                    accuracy += GearData.GetGear(GearID.BallisticCharts).GearModifierValue;
                if(Contains(GearID.ConscriptionOrders) && squadStats.RarityTier == UnitRarity.Common) {
                    int conscriptionOrdersModifier = GearData.GetGear(GearID.ConscriptionOrders).GearModifierValue;
                    meleeAttack += conscriptionOrdersModifier;
                    meleeDefense += conscriptionOrdersModifier;
                }
                if(Contains(GearID.JoustingLances) && squadStats.unitSize == UnitSize.Cavalry)
                {
                    weaponStrength += GearData.GetGear(GearID.JoustingLances).GearModifierValue;
                }
                if(Contains(GearID.GnomishArmorers) && squadStats.RarityTier == UnitRarity.Rare)
                    meleeDefense += GearData.GetGear(GearID.GnomishArmorers).GearModifierValue;
                if(Contains(GearID.WellHonedAxes) && unitAttributes.ArmorPiercing) //must be after diamond tipped arrows
                    meleeAttack += GearData.GetGear(GearID.WellHonedAxes).GearModifierValue;
                if(Contains(GearID.RavensEye) && squadStats.RarityTier != UnitRarity.Common && squadStats.unitType == UnitType.Ranged)
                    accuracy += GearData.GetGear(GearID.RavensEye).GearModifierValue;
                if(Contains(GearID.RingoftheElvenKing) && squadStats.unitType == UnitType.Ranged)
                    missileStrength += GearData.GetGear(GearID.RingoftheElvenKing).GearModifierValue;
            } 

            entityCommandBuffer.AddComponent(entity, new EntityTeam {
                Value = team
            });

            if(TabletopTavernConstants.Casts(squadStats.unitType))
            {
                // Mage. Deliberately ahead of the "!= Melee" branch below, which would otherwise
                // catch it and hand it a ShootAttack, a RangedUnitTag and a RangedMeleeConverter
                // it has no use for. A mage's reach and cadence live on MageCast instead, and that
                // is what prestige Range writes to since there is no ShootAttack.Range to raise.
                // MeleeAttack is still added unconditionally below, so it defends itself in melee.
                entityCommandBuffer.AddComponent(entity, new MageCast {
                    Range = range,
                    Cooldown = squadStats.rateOfFire,
                    Timer = squadStats.rateOfFire,
                });
            }
            else if(squadStats.unitType != UnitType.Melee)
            {
                float rangeMultiplier = 1;
                float accuracyMultiplier = 1;

                // Shooter traits. squadStats.SquadAttributes already has any prestige-granted
                // trait merged in above, so these cover both innate and granted cases.
                if (squadStats.SquadAttributes.Overdraw)
                    rangeMultiplier *= TabletopTavernConstants.OVERDRAW_RANGE_MULTIPLIER;

                LocalToWorld localTransform = SystemAPI.GetComponent<LocalToWorld>(entity);
                float timerMax = squadStats.unitType == UnitType.Artillery || squadStats.unitType == UnitType.Structure ? squadStats.rateOfFire : TabletopTavernConstants.RANGED_ATTACK_COOLDOWN;
                if (squadStats.SquadAttributes.ShotDiscipline)
                    timerMax *= TabletopTavernConstants.SHOT_DISCIPLINE_RELOAD_MULTIPLIER;

                // Waives the Fire-at-Will accuracy penalty; read per shot in RangedUnitAttackSystem.
                if (squadStats.SquadAttributes.SteadyAim)
                    entityCommandBuffer.AddComponent<SteadyAimTag>(entity);

                entityCommandBuffer.AddComponent<RangedMeleeConverter>(entity);
                entityCommandBuffer.AddComponent<RangedUnitTag>(entity);
                bool isArtillery = squadStats.unitType == UnitType.Artillery;
                bool requestingFireProjectile = entityManager.HasComponent<FlamingRangedAttackTag>(entity);
                entityCommandBuffer.AddComponent(entity, new ShootAttack {
                    timer = 0,
                    timerMax = timerMax,
                    damageAmount = missileStrength,
                    Range = range * rangeMultiplier,
                    Accuracy = (int)(accuracy * accuracyMultiplier),
                    ProjectileEntity = entitiesReferences.GetProjectileEntityForUnitName(unit.ValueRO.unitName, requestingFireProjectile),
                    shootAnimationDelay = isArtillery ? 0.25f : 0.5f,
                });

                // RangedUnitAttackSystem's query requires this component, so anything meant to fire
                // must have it. Structures get theirs from ArmySpawnManager instead.
                if(TabletopTavernConstants.FightsAtRange(squadStats.unitType) || squadStats.unitType == UnitType.Artillery) {
                    // The player's default only reaches FightsAtRange units, because those are the
                    // only ones the Volley / Fire at Will buttons are offered for. RangedUnitAttackSystem
                    // honours FireAtWill on anything carrying this component, so an artillery piece
                    // started in it could never be switched back out.
                    RangedFireMode fireMode = team == Team.Player && TabletopTavernConstants.FightsAtRange(squadStats.unitType)
                        ? playerDefaultFireMode
                        : RangedFireMode.Volley;
                    entityCommandBuffer.AddComponent(entity, new RangedFireModeUnitComponent { FireMode = fireMode });
                }

            } else {
                entityCommandBuffer.AddComponent<MeleeUnitTag>(entity);
            }
  
            entityCommandBuffer.AddComponent(entity, new MeleeAttack {
                timerMax = squadStats.attackCooldown,
                MeleeAttackValue = meleeAttack,
                WeaponStrength = weaponStrength,
                timer = _random.NextFloat(0f, squadStats.attackCooldown)
            });

            entityCommandBuffer.AddComponent(entity, new MaxHitPoints
            {
                Value = maxHitPoints
            });
            if(entityManager.HasComponent<ModifyHealthOnSpawn>(entity)) {
                int healthValue = entityManager.GetComponentData<ModifyHealthOnSpawn>(entity).Value;
                entityCommandBuffer.AddComponent(entity, new Health {
                    Value = healthValue,
                    onHealthChanged = true,
                });
            } else {
                entityCommandBuffer.AddComponent(entity, new Health {
                    Value = maxHitPoints,
                    onHealthChanged = true,
                });
            }
  
            entityCommandBuffer.AddBuffer<DamageBufferElement>(entity);
            entityCommandBuffer.AddBuffer<SFXBufferElement>(entity);
            

            //Tags for damage multipliers
            if(squadStats.unitSize == UnitSize.Monstrous || squadStats.unitSize == UnitSize.Cavalry || squadStats.unitSize == UnitSize.SingleUnit) {
                if(squadStats.unitSize != UnitSize.Cavalry && squadStats.unitType != UnitType.Structure) {
                    entityCommandBuffer.AddComponent(entity, new MonsterTag { KnockbackRange = squadStats.ExplosionRange, KnockbackInitialDamage = squadStats.ExplosionDamage });
                }
                if(squadStats.unitType != UnitType.Structure) {
                    entityCommandBuffer.AddComponent<LargeTag>(entity);
                }
            }
            if(squadStats.unitSize == UnitSize.Infantry || squadStats.unitSize == UnitSize.Artillery) {
                entityCommandBuffer.AddComponent<InfantryTag>(entity);
            }

            if(squadStats.SquadAttributes.StandardShields || squadStats.SquadAttributes.HeavyShields)//|| squadStats.SquadAttributes.TowerShields
            {
                entityCommandBuffer.AddComponent(entity, new Shield {
                    ShieldBlockChance = shieldBlockChance,
                });
                entityCommandBuffer.AddComponent(entity, new ShieldedStanceUnitComponent { Stance = ShieldedStance.Balanced });
            }
            else
            {
                entityCommandBuffer.AddComponent(entity, new ShieldedStanceUnitComponent { Stance = ShieldedStance.None });
                
            }
            
            entityCommandBuffer.AddComponent(entity, new MeleeDefense {
                Value = meleeDefense
            });

            //has rider
            if( unit.ValueRO.unitName == UnitName.BorderlandRiders || 
                unit.ValueRO.unitName == UnitName.HearthboundKnights || 
                unit.ValueRO.unitName == UnitName.RoyalCavaliers ||
                unit.ValueRO.unitName == UnitName.StarStriders ||
                unit.ValueRO.unitName == UnitName.VeilkinDrakes ||
                unit.ValueRO.unitName == UnitName.Direriders ||
                unit.ValueRO.unitName == UnitName.Nightriders ||
                unit.ValueRO.unitName == UnitName.BloodswornKnights ||
                unit.ValueRO.unitName == UnitName.DragonTachi ||
                unit.ValueRO.unitName == UnitName.AngelsOfDeath ||
                unit.ValueRO.unitName == UnitName.ThunderhoofChargers ||
                unit.ValueRO.unitName == UnitName.RaptorRiders
            ) {
                entityCommandBuffer.AddComponent(entity, new Cavalry {
                    unitName = unit.ValueRO.unitName
                });
            }

            // entityCommandBuffer.AddComponent<DamageRecievedFrom>(entity, new DamageRecievedFrom { SquadId = 99 });
            // entityCommandBuffer.AddComponent<CatchUpTag>(entity);

            // entityCommandBuffer.AddComponent(entity, new SFXIDHolder
            // {
            //     deathSFXID = DataTypes.GetDeathSFXID(unit.ValueRO.unitName),
            //     idleSFXID = DataTypes.GetIdleSFXID(unit.ValueRO.unitName),
            // });

            float sightRange = squadStats.unitSize switch
            {
                UnitSize.SingleUnit => TabletopTavernConstants.MELEE_DETECTION_RANGE * 2f,
                UnitSize.Monstrous  => TabletopTavernConstants.MELEE_DETECTION_RANGE * 1.5f,
                UnitSize.Cavalry    => TabletopTavernConstants.MELEE_DETECTION_RANGE * 1.5f,
                _                   => TabletopTavernConstants.MELEE_DETECTION_RANGE,
            };

            entityCommandBuffer.AddComponent(entity, new UnitFindTarget {
                sightRange = sightRange,
                TargetTeam = team == Team.Player ? Team.Enemy : Team.Player,
                timerMax = 0.5f,
                TargetedSquadEntity = Entity.Null
            });
            entityCommandBuffer.SetComponentEnabled<UnitFindTarget>(entity, false);

            Color hoveredColor = team == Team.Player ? TabletopTavernConstants.PLAYER_TRIANGLE_COLOR: TabletopTavernConstants.ENEMY_TRIANGLE_COLOR;
            Color selectedColor = team == Team.Player ? TabletopTavernConstants.PLAYER_TRIANGLE_COLOR : TabletopTavernConstants.ENEMY_TRIANGLE_COLOR;

            entityCommandBuffer.AddComponent(entity, new TriangleEntity { 
                hoverColor = new Vector4(hoveredColor.r, hoveredColor.g, hoveredColor.b, TabletopTavernConstants.TRIANGLE_HOVER_BLOOM),
                selectedColor = new Vector4(selectedColor.r, selectedColor.g, selectedColor.b, TabletopTavernConstants.TRIANGLE_SELECTED_BLOOM),
                disabledColor = TabletopTavernConstants.DISABLED_TRIANGLE_COLOR,
                activeColor = TabletopTavernConstants.DISABLED_TRIANGLE_COLOR,
            });
            entityCommandBuffer.SetComponentEnabled<TriangleEntity>(entity, true);

            // if(team != team.Player) continue;

            // BattleFieldPreset.Weather weather = SaveDataHandler.Load().battleFieldPreset.weather;

            if(unitAttributes.ArmorPiercing) {
                entityCommandBuffer.AddComponent<ArmorPiercingTag>(entity);
            }
            if(unitAttributes.AntiInfantry) {
                entityCommandBuffer.AddComponent<AntiInfantryTag>(entity);
            }
            if(unitAttributes.AntiLarge) {
                entityCommandBuffer.AddComponent<AntiLargeTag>(entity);
            }
            if(squadStats.SquadAttributes.BackStabbers) {
                entityCommandBuffer.AddComponent<BackStabbersTag>(entity);
            }
            if (unitAttributes.Armored)
            {
                entityCommandBuffer.AddComponent(entity, new ArmoredTag
                {
                    ArmorMitigation = armor / (armor + 100f)
                });
            }
            if (squadStats.SquadAttributes.Ethereal)
            {
                entityCommandBuffer.AddComponent(entity, new PhysicalDamageMultiplier { Value = 0.5f });
                entityCommandBuffer.AddComponent(entity, new MagicalDamageMultiplier { Value = 0.5f });
            }
            if (squadStats.SquadAttributes.ThickScales)
            {
                entityCommandBuffer.AddComponent(entity, new MissileResistance { DamageMultiplier = 0.75f });
            }

            if (squadStats.unitType == UnitType.Artillery)
            {
                // Demolisher scales the blast itself (damage and radius), leaving knockback force
                // alone so shots don't start flinging units clean off the battlefield.
                float explosionMultiplier = squadStats.SquadAttributes.Demolisher
                    ? TabletopTavernConstants.DEMOLISHER_EXPLOSION_MULTIPLIER
                    : 1f;

                entityCommandBuffer.AddComponent(entity, new ArtilleryUnit
                {
                    SquadID = unit.ValueRO.squadId,
                    ExplosionDamage = (int)(squadStats.ExplosionDamage * explosionMultiplier),
                    ExplosionRange = squadStats.ExplosionRange * explosionMultiplier,
                    ExplosionForce = squadStats.ExplosionForce
                });
                entityCommandBuffer.AddComponent(entity, new ResistKnockbackTag { });
            }
            else if (squadStats.unitSize == UnitSize.SingleUnit)
            {
                entityCommandBuffer.AddComponent(entity, new ResistKnockbackTag { });
            }
            else if (squadStats.unitSize == UnitSize.Monstrous)
            {
                entityCommandBuffer.AddComponent(entity, new ResistKnockbackTag { });
            }

            // Size-based separation: larger units have low Weight (barely yield) and wide Radius
            // (detect smaller units early). Smaller units have high Weight (move away quickly).
            // Net effect: large units push through small ones without needing reciprocal force logic.
            // Separation.Radius must be > AgentShape.Radius (0.75) so the spatial query finds
            // neighbours before they are already overlapping (two touching infantry are 1.5 apart).
            // Large units only separate from other large units (Layer1/Layer2), not from infantry
            // (NavigationLayers.Default). Infantry keeps Everything so they still yield to large
            // units when both are moving — the weight asymmetry (0.2-0.7 vs 1.5) ensures infantry
            // scatter away while the large unit holds its course.
            AgentSeparation separation = squadStats.unitSize switch
            {
                UnitSize.SingleUnit => new AgentSeparation { Radius = 2.5f, Weight = 0.2f, Layers = NavigationLayers.Layer1 | NavigationLayers.Layer2 },
                UnitSize.Monstrous  => new AgentSeparation { Radius = 2.0f, Weight = 0.3f, Layers = NavigationLayers.Layer1 | NavigationLayers.Layer2 },
                UnitSize.Cavalry    => new AgentSeparation { Radius = 1.8f, Weight = 0.7f, Layers = NavigationLayers.Layer1 | NavigationLayers.Layer2 },
                UnitSize.Artillery  => new AgentSeparation { Radius = 1.6f, Weight = 1.2f, Layers = NavigationLayers.Everything },
                _                   => new AgentSeparation { Radius = 1.6f, Weight = 1.5f, Layers = NavigationLayers.Everything }, // Infantry
            };
            if (squadStats.unitType == UnitType.Structure)
            {
                // separation.Radius = 10f;
                if (entityManager.HasComponent<AgentShape>(entity))
                {
                    AgentShape agentShape = entityManager.GetComponentData<AgentShape>(entity);
                    agentShape.Radius = 2.5f;
                    entityCommandBuffer.SetComponent(entity, agentShape);
                }
            }
            entityCommandBuffer.SetComponent(entity, separation);
            entityCommandBuffer.AddComponent(entity, new BaseSeparationWeight { Value = separation.Weight });
            // AgentSonarAvoid.Radius on the prefab is 0.25 — smaller than AgentShape.Radius (0.75),
            // so sonar steering fires after units are already overlapping. Set it to match the shape
            // radius so steering kicks in as soon as another unit enters personal space.
            if (entityManager.HasComponent<AgentSonarAvoid>(entity))
            {
                AgentSonarAvoid sonarAvoid = entityManager.GetComponentData<AgentSonarAvoid>(entity);
                sonarAvoid.Radius = squadStats.unitSize switch
                {
                    UnitSize.SingleUnit => 2.0f,
                    UnitSize.Monstrous  => 1.5f,
                    UnitSize.Cavalry    => 1.2f,
                    _                   => 0.85f, // Infantry / Artillery
                };
                // Large units ignore infantry (Default layer) so sonar does not steer them around
                // infantry walls — LargeUnitPushSystem handles the actual physical displacement.
                sonarAvoid.Layers = squadStats.unitSize switch
                {
                    UnitSize.SingleUnit or UnitSize.Monstrous or UnitSize.Cavalry =>
                        NavigationLayers.Layer1 | NavigationLayers.Layer2,
                    _ => NavigationLayers.Everything,
                };
                entityCommandBuffer.SetComponent(entity, sonarAvoid);
            }

            // Assign each unit to a navigation layer by size so spatial queries can discriminate.
            // Infantry/Artillery → Default (bit 0), Cavalry → Layer1 (bit 1), Large → Layer2 (bit 2).
            if (entityManager.HasComponent<Agent>(entity))
            {
                Agent agent = entityManager.GetComponentData<Agent>(entity);
                agent.Layers = squadStats.unitSize switch
                {
                    UnitSize.Cavalry    => NavigationLayers.Layer1,
                    UnitSize.Monstrous  => NavigationLayers.Layer2,
                    UnitSize.SingleUnit => NavigationLayers.Layer2,
                    _                   => NavigationLayers.Default, // Infantry, Artillery
                };
                entityManager.SetComponentData(entity, agent);
            }

            // Large units only hard-collide with other large units (Layer1/Layer2).
            // Infantry is displaced by LargeUnitPushSystem instead, not AgentCollider.
            if (entityManager.HasComponent<AgentCollider>(entity))
            {
                AgentCollider collider = entityManager.GetComponentData<AgentCollider>(entity);
                collider.Layers = squadStats.unitSize switch
                {
                    UnitSize.Cavalry    => NavigationLayers.Layer1 | NavigationLayers.Layer2,
                    UnitSize.Monstrous  => NavigationLayers.Layer1 | NavigationLayers.Layer2,
                    UnitSize.SingleUnit => NavigationLayers.Layer1 | NavigationLayers.Layer2,
                    _                   => NavigationLayers.Default,
                };
                entityCommandBuffer.SetComponent(entity, collider);
            }

            entityCommandBuffer.AddComponent(entity, new RetreatingUnit { });
            entityCommandBuffer.SetComponentEnabled<RetreatingUnit>(entity, false);

            entityCommandBuffer.AddComponent<ApplyKnockbackOnContact>(entity);
            entityCommandBuffer.SetComponentEnabled<ApplyKnockbackOnContact>(entity, false);

            
// #if UNITY_EDITOR
            // entityManager.SetName(entity, $"Unit - {entityManager.GetName(unit.ValueRO.squadEntity)}");
// #endif
        }
    }
}