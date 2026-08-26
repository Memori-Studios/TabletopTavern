using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using TJ;
using Memori.SaveData;
using System;
using Memori.Scenes;
using ProjectDawn.Navigation;
using Memori.Input;
using TJ.Battle;
using TJ.Map;
using TJ.Morale;
using Memori.Notifications;
using System.Threading.Tasks;
using Memori.Localization;

public class SquadManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject flagPrefab;
    [SerializeField] private Material raceFlagBaseMaterial;

    [Header("Formation Drawers")]
    [SerializeField] private ArcherRangeDrawer archerRangeDrawerPrefab;
    
    Dictionary<int, ArcherRangeDrawer> squadRangeDrawers = new ();
    public Dictionary<int, ArcherRangeDrawer> SquadRangeDrawers => squadRangeDrawers;
    private bool rangesVisible = false;

    Dictionary<int, GarrisonGateRangeDrawer> gateRangeDrawers = new();
    public Dictionary<int, GarrisonGateRangeDrawer> GateRangeDrawers => gateRangeDrawers;
    int playerSquadsRegistered = 0;
    int enemySquadsRegistered = 0;
    private EntityManager entityManager;
    private EndSimulationEntityCommandBufferSystem ecbSystem;
    public bool LoadFromLocalSave = false;
    [SerializeField] private List<int> trueSquadOrder = new();
    public List<int> TrueSquadOrder => trueSquadOrder;

    public delegate void SquadUpdated(int _squadId, float2 _unitCount);
    public event SquadUpdated OnSquadUpdated;
    public delegate void SquadAmmoUpdated(int _squadId, int _currentAmmo);
    [SerializeField] private Dictionary<int, int> unitPrestigeDict = new ();
    public Dictionary<int, int> UnitPrestigeDict => unitPrestigeDict;
    private Dictionary<int, UnitAttribute> squadPrestigeTraitDict = new ();

    public List<int> TerrifiedSquadIds = new ();
    public delegate void TerrifiedSquadsChanged(List<int> _squadId);
    public event TerrifiedSquadsChanged OnTerrifiedSquadsChanged;

    private List<int> chargingSquadIds = new();
    public delegate void ChargingSquadsChanged(List<int> _squadId);
    public event ChargingSquadsChanged OnChargingSquadsChanged;

    public delegate void DestroyedSquad(int _squadId);
    public event DestroyedSquad OnDestroyedSquad;

    public delegate void BattlefieldBonusApplied(int _squadId, BattlefieldBonusEnum _bonus, UnitStat _stat, float _value);
    public event BattlefieldBonusApplied OnBattlefieldBonusApplied;
    public List<GameObject> stuffToDestroy = new();

    private void Start()
    {
        InputHandler.Instance.OnShowUnitMovement += ToggleAllRanges;
        BattleManager.Instance.SquadOrderManager.OnSquadOrderChanged += OnSquadOrderChanged;
    }

    [ContextMenu("Get all unit count")]
    public void GetAllUnitCount()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadEntity>(), ComponentType.ReadOnly<EntityReferenceBufferElement>());
        using var squadEntities = query.ToEntityArray(Allocator.TempJob);
        int totalUnitCount = 0;
        foreach (var squadEntity in squadEntities)
        {
            var squad = entityManager.GetComponentData<SquadEntity>(squadEntity);
            var entityBuffer = entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntity);
            totalUnitCount += entityBuffer.Length;
        }
        Debug.Log($"Total unit count: {totalUnitCount}");
        query.Dispose();
    }
    public void SetUp() 
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        ecbSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        ecbSystem.AddJobHandleForProducer(default);
        squadRangeDrawers = new ();

        bool autoCharge = PlayerPrefs.GetInt("autoCharge", 0) == 1;
        bool guardMode = PlayerPrefs.GetInt("defaultSquadGuardMode", 1) == 1;
        Debug.Log($"AutoCharge : [{autoCharge}], GuardMode : [{guardMode}]");
    }
    public int GetSquadUnitCount(int _squadId)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SquadEntity>(),
            ComponentType.ReadOnly<EntityReferenceBufferElement>()
        );

        using var squadEntities = query.ToEntityArray(Allocator.TempJob);
        foreach (var squadEntity in squadEntities)
        {
            var squad = entityManager.GetComponentData<SquadEntity>(squadEntity);
            if (squad.SquadId != _squadId) continue;

            var entityBuffer = entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntity);
            query.Dispose();
            // Debug.Log($"Squad has {entityBuffer.Length} entities");
            return entityBuffer.Length;
        }
        query.Dispose();
        return 0;
    }
    public NativeArray<SquadEntity> RetrieveSquadEntities(ComponentType requiredComponent)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SquadEntity>(),
            requiredComponent
        );
        NativeArray<SquadEntity> squadEntities = query.ToComponentDataArray<SquadEntity>(Allocator.TempJob);
        query.Dispose();
        return squadEntities;
    }
    public NativeArray<SquadMovementComponent> RetrieveSquadsMovement(ComponentType requiredComponent)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SquadMovementComponent>(),
            requiredComponent
        );
        NativeArray<SquadMovementComponent> squadEntities = query.ToComponentDataArray<SquadMovementComponent>(Allocator.TempJob);
        query.Dispose();
        return squadEntities;
    }
    public NativeArray<SquadOverridesComponent> RetrievePlayerSquadOverrideComponents()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SquadOverridesComponent>(), 
            ComponentType.ReadOnly<PlayerSquad>()
        );
        NativeArray<SquadOverridesComponent> squadOverrides = query.ToComponentDataArray<SquadOverridesComponent>(Allocator.TempJob);
        query.Dispose();
        return squadOverrides;
    }
    public NativeArray<SquadOverridesComponent> RetrieveEnemySquadOverrideComponents()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SquadOverridesComponent>(), 
            ComponentType.ReadOnly<EnemySquad>()
        );
        NativeArray<SquadOverridesComponent> squadOverrides = query.ToComponentDataArray<SquadOverridesComponent>(Allocator.TempJob);
        query.Dispose();
        return squadOverrides;
    }

    public NativeArray<SquadEntity> RetrieveAllSquads()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SquadEntity>()
        );
        NativeArray<SquadEntity> squadEntityArray = query.ToComponentDataArray<SquadEntity>(Allocator.Temp);
        query.Dispose();
        return squadEntityArray;
    }
    public bool CheckIfSquadExists(int _squadId)
    {
        using var allSquads = RetrieveAllSquads();
        foreach (var squad in allSquads) {
            if (squad.SquadId == _squadId) {
                return true;
            }
        }
        return false;
    }
    public SquadEntity GetSquad(int squadId)
    {
        NativeArray<SquadEntity> playerSquads = RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());
        NativeArray<SquadEntity> enemySquads = RetrieveSquadEntities(ComponentType.ReadOnly<EnemySquad>());
        foreach (var squad in playerSquads) {
            if (squad.SquadId == squadId) {
                SquadEntity squadE = squad;
                playerSquads.Dispose();
                enemySquads.Dispose();
                return squadE;
            }
        }
        foreach (var squad in enemySquads) {
            if (squad.SquadId == squadId) {
                SquadEntity squadE = squad;
                playerSquads.Dispose();
                enemySquads.Dispose();
                return squadE;
            }
        }

        playerSquads.Dispose();
        enemySquads.Dispose();
        return new (){
            SquadId = 0,
        };
    }
    public List<Entity> GetEntitiesFromSquad(int squadId)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SquadEntity>(), 
            ComponentType.ReadOnly<EntityReferenceBufferElement>()
        );

        List<Entity> entities = new ();
        using (var squadEntities = query.ToEntityArray(AllocatorManager.TempJob))
        {
            foreach (var squadEntity in squadEntities)
            {
                SquadEntity squad = entityManager.GetComponentData<SquadEntity>(squadEntity);
                if(squad.SquadId != squadId) continue;
                var entityBuffer = entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntity);
                
                for (int i = 0; i < entityBuffer.Length; i++)
                {
                    if(entityManager.Exists(entityBuffer[i].Entity)){
                        entities.Add(entityBuffer[i].Entity);
                    } else {
                        entityBuffer.RemoveAt(i);
                        // Debug.Log($"Entity {entityBuffer[i].Entity} does not exist");
                    }
                }
            }
        }
        query.Dispose();
        return entities;
    }
    public SquadEntity GetSquadEntityFromId(int _squadId, bool silentlyFail = false)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadEntity>());
        NativeArray<SquadEntity> squadEntities = query.ToComponentDataArray<SquadEntity>(Allocator.Temp);

        foreach (SquadEntity squadEntity in squadEntities) {
            if (squadEntity.SquadId == _squadId) {
                query.Dispose();
                return squadEntity;
            }
        }
        query.Dispose();
        if (!silentlyFail) {
            UnityEngine.Debug.LogError($"Squad with id {_squadId} not found");
        }
        return new SquadEntity();
    }
    public void DeselectAllSquadEntities()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadEntity>());
        using var squadEntities = query.ToEntityArray(Allocator.TempJob);

        foreach (var squadEntity in squadEntities)
        {
            SquadEntity squad = entityManager.GetComponentData<SquadEntity>(squadEntity);
            if (squad.IsSelected) {
                squad.IsSelected = false;
                entityManager.SetComponentData(squadEntity, squad);
            }

            foreach (var squadRangeDrawer in squadRangeDrawers) {
                squadRangeDrawer.Value.TurnOff();
                // Debug.Log($"Turned off squad range drawer for squad {squadRangeDrawer.Key}");
            }

            if(squadRangeDrawers.ContainsKey(squad.SquadId)) {
                squadRangeDrawers[squad.SquadId].TurnOff();
            }
        }
        query.Dispose();
        BattleManager.Instance.UIManager.DeselectSquadEntitiesUI();
    }
    
    #region Register Squad
    public void RegisterSquad(List<Entity> _entities, SquadSpawnData _enemyData, int _spawnPrestige, string _uniqueID, int _healthOverride = -1, UnitAttribute _prestigeTrait = UnitAttribute.None)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EntitiesReferences>());
        EntitiesReferences entitiesReferences = query.GetSingleton<EntitiesReferences>();

        EntityQuery query2 = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CampaignSaveDataHolder>());
        CampaignSaveDataHolder campaignSaveDataHolder = query2.GetSingleton<CampaignSaveDataHolder>();

        SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(_enemyData.unitName);
        if (_prestigeTrait != UnitAttribute.None) {
            TabletopTavernConstants.SetAttribute(ref squadStats.SquadAttributes, _prestigeTrait);
        }

        //Override health for structures
        if (squadStats.unitType == UnitType.Structure)
        {
            squadStats.HitPointsPerUnit = _healthOverride > 0 ? _healthOverride : squadStats.HitPointsPerUnit;
        }

        int squadId = 0;
        bool guardMode = false;
        if(_enemyData.Team == Team.Player) 
        {
            if(BattleManager.Instance.BattleSaveManager.IsCustomBattle)
            {
                squadId = playerSquadsRegistered + 1;
            }
            else
            {
                squadId = _enemyData.squadId;
            }
            playerSquadsRegistered++;
            guardMode = PlayerPrefs.GetInt("defaultSquadGuardMode", 1) == 1;
        } else {
            squadId = (enemySquadsRegistered + 1) * -1;
            enemySquadsRegistered++;
#if UNITY_EDITOR
            if(SceneHandler.Instance.LockEnemiesToGuardMode) {
                guardMode = true;
            }
#endif
        }

        Entity squadEntity = entityManager.CreateEntity();

        //get average postion of all entities
        float3 averagePosition = new (0, 0, 0);
        for (int i = 0; i < _enemyData.entityPositions.Count; i++) {
            averagePosition += _enemyData.entityPositions[i];
        }
        averagePosition /= _entities.Count;
        int initialSquadSize = squadStats.baseUnitCount;
        int maxHealth = squadStats.HitPointsPerUnit * initialSquadSize;
        int currentHealth = _entities.Count * squadStats.HitPointsPerUnit;
        // Debug.Log($"Registering squad {squadId} with {initialSquadSize} entities and {maxHealth} max health");
        float leadership = squadStats.Leadership;
        // Mages take Leadership prestige alongside melee units: a one-model squad breaks easily,
        // and Leadership plus Range is the whole of what prestige buys a caster.
        if (TabletopTavernConstants.FightsInMelee(squadStats.unitType)
            || TabletopTavernConstants.Casts(squadStats.unitType)) {
            leadership += _spawnPrestige * TabletopTavernConstants.PRESTIGE_BONUS;
        }
        var ecb = ecbSystem.CreateCommandBuffer();

        //hero stuff - Leadership magnitudes and attribute grants now come from HeroBonusManager's
        // rule data instead of hardcoded per-hero checks, so mods can change them. The Sakura
        // Dynasty mono-race army gate (OnlySakuraUnits) is unchanged - not yet generalized to
        // other races.
        if(_enemyData.Team == Team.Player && campaignSaveDataHolder.ActiveHeroID != -1)
        {
            foreach (var bonus in HeroBonusManager.GetHeroStatBonus(UnitStat.Leadership, squadStats.unitName, campaignSaveDataHolder.ActiveHeroID, leadership))
                leadership += bonus.Value;
            if (campaignSaveDataHolder.OnlySakuraUnits)
                foreach (var bonus in HeroBonusManager.GetFactionBonusForHero(UnitStat.Leadership, campaignSaveDataHolder.ActiveHeroID))
                    leadership += bonus.Value;

            foreach (var attributeBonus in HeroBonusManager.GetHeroAttributeBonus(squadStats.unitName, campaignSaveDataHolder.ActiveHeroID))
            {
                switch (attributeBonus.UnitAttribute)
                {
                    case UnitAttribute.Terrifying: ecb.AddComponent<CausesTerrorTag>(squadEntity); break;
                    case UnitAttribute.Stalwart: ecb.AddComponent<StalwartTag>(squadEntity); break;
                    case UnitAttribute.Rage: ecb.AddComponent<RageApplicatorTag>(squadEntity); break;
                    // Outrider is applied contextually in UnitSelectionManager (a deployment-time
                    // position-validity check), not as a persistent squad tag - intentionally not
                    // handled here.
                }
            }
        }

        // Debug.Log($"Registering squad {squadId} with {_entities.Count} entities, {initialSquadSize} initial size, {maxHealth} max health, and {leadership} leadership");
        ecb.AddComponent(squadEntity, new SquadEntity
        {
            SelfEntity = squadEntity,
            Team = _enemyData.Team,
            SquadId = squadId,
            UnitName = _enemyData.unitName,
            initialSquadSize = initialSquadSize,
        });

        bool isShielded = squadStats.SquadAttributes.StandardShields || squadStats.SquadAttributes.HeavyShields;// || squadStats.SquadAttributes.TowerShields;

        ecb.AddComponent(squadEntity, new SquadOverridesComponent {
            SquadId = squadId,
            GuardMode = guardMode,
            UnitType = squadStats.unitType,
            ShieldedStance = isShielded ? ShieldedStance.Balanced : ShieldedStance.None,
            AutoTarget = true,
            MeleeMode = false,
        });

        ecb.AddComponent(squadEntity, new LocalTransform { 
            Position = averagePosition, 
            Rotation = _enemyData.squadRotation, 
            Scale = 1
        });

        ecb.AddComponent(squadEntity, new SquadTargettingComponent {
            UpdateTargetDestinationRefreshRate = 0.2f
        });
        SquadMovementComponent squadMovementComponent = new SquadMovementComponent(){ 
            SelfEntity = squadEntity,
            GoalPosition = averagePosition,
        };
        squadMovementComponent.SetWidthAndDepth(new int2(_enemyData.widthAndDepth.x, _enemyData.widthAndDepth.y));
        squadMovementComponent.SetRotation(_enemyData.squadRotation);

        ecb.AddComponent(squadEntity, squadMovementComponent);

        // Debug.Log($"Registering squad {squadId} with {maxHealth} max health, and {currentHealth} current health");

        ecb.AddComponent(squadEntity, new SquadStateComponent
        {
            MaxHealthValue = maxHealth,
            CurrentHealthValue = currentHealth,
            ChargesRemaining = squadStats.ChargeCount,
            IsFlanked = false,
        });

        ecb.AddComponent<WaitingForCommand>(squadEntity);
        if (_enemyData.Team == Team.Player)
        {
            bool waitForCommand = PlayerPrefs.GetInt("autoCharge", 0) == 0;
            ecb.SetComponentEnabled<WaitingForCommand>(squadEntity, waitForCommand);
        }
        else
        {
            ecb.SetComponentEnabled<WaitingForCommand>(squadEntity, true);
        }
        
        // add a new componet that is diabled by default
        ecb.AddComponent(squadEntity, new DestroyEntityTag { });
        ecb.SetComponentEnabled<DestroyEntityTag>(squadEntity, false);

        if (squadStats.SquadAttributes.Stalwart)
        {
            ecb.AddComponent<StalwartTag>(squadEntity);
        }
        if(squadStats.SquadAttributes.Terrifying) {
            ecb.AddComponent<CausesTerrorTag>(squadEntity);
        }
        if (squadStats.unitSize != UnitSize.Infantry && 
            squadStats.unitSize != UnitSize.Artillery && 
            squadStats.unitType != UnitType.Structure)
        {
            ecb.AddComponent<LargeTag>(squadEntity);
        }
        if ((squadStats.unitSize == UnitSize.Monstrous || squadStats.unitSize == UnitSize.SingleUnit) && squadStats.unitType != UnitType.Structure)
        {
            ecb.AddComponent<MonsterousSquadTag>(squadEntity);
        }

        if (squadStats.SquadAttributes.AntiLarge)
        {
            ecb.AddComponent<AntiLargeTag>(squadEntity);
        }

        if (squadStats.SquadAttributes.BloodFrenzy)
        {
            ecb.AddComponent<BloodFrenzyApplicatorTag>(squadEntity);
        }
        if (squadStats.SquadAttributes.Rage)
        {
            ecb.AddComponent<RageApplicatorTag>(squadEntity);
        }

        // Swarm Breaker (Hero 13, Cragflayers -> Rage) is now applied by the consolidated hero
        // attribute-grant loop above, alongside the other hero-granted attributes.

        if (squadStats.SquadAttributes.Emblazing ||
            squadStats.SquadAttributes.ArmorSundering
        ) {
            ecb.AddComponent<EmblazerTag>(squadEntity);
        }
        if (squadStats.SquadAttributes.MonsterSlayer)
        {
            ecb.AddComponent<SlayerApplicatorTag>(squadEntity);
        }
        if(squadStats.SquadAttributes.BackStabbers)
        {
            ecb.AddComponent<BackStabbersTag>(squadEntity);
        }

        //Race passive
        Race squadRace = TabletopTavernData.Instance.GetRaceFromUnitName(_enemyData.unitName);
        switch (squadRace)
        {
            case Race.IronLegion:
                ecb.AddComponent<IronLegionRaceTag>(squadEntity);
                ecb.AddComponent(squadEntity, new IronResolveComponent());
                break;
            case Race.Gruntkin:
                ecb.AddComponent<GruntkinRaceTag>(squadEntity);
                ecb.AddComponent(squadEntity, new CrashingHordeComponent());
                break;
            case Race.RavenHost:
                ecb.AddComponent<RavenHostRaceTag>(squadEntity);
                ecb.AddComponent(squadEntity, new DeathcryComponent());
                break;
            case Race.TaelindorForest:
                ecb.AddComponent<TaelindorForestRaceTag>(squadEntity);
                ecb.AddComponent(squadEntity, new HuntersPatienceComponent
                {
                    IsRanged = squadStats.unitType == UnitType.Ranged
                });
                break;
            case Race.SanguineCourt:
                ecb.AddComponent<SanguineCourtRaceTag>(squadEntity);
                break;
            case Race.SakuraDynasty:
                ecb.AddComponent<SakuraDynastyRaceTag>(squadEntity);
                ecb.AddComponent(squadEntity, new KenseiEyeComponent());
                break;
            case Race.DeepstoneHold:
                ecb.AddComponent<DeepstoneHoldRaceTag>(squadEntity);
                ecb.AddComponent(squadEntity, new OathcarvedComponent());
                break;
            case Race.DrakosaurBrood:
                ecb.AddComponent<DrakosaurBroodRaceTag>(squadEntity);
                ecb.AddComponent(squadEntity, new ApexHuntersComponent());
                break;
        }

        GearIDsSerialized gear = campaignSaveDataHolder.Gear;

        bool Contains(GearID gearID) {
                if(gear.gearID1 == gearID || gear.gearID2 == gearID || gear.gearID3 == gearID || gear.gearID4 == gearID) return true;
                return false;
            }

        if(!campaignSaveDataHolder.IsCustomBattle && _enemyData.Team == Team.Player)
        {
            if (Contains(GearID.DiamondTippedArrows) && squadStats.unitType == UnitType.Ranged)
            {
                ecb.AddComponent<ArmorPiercingTag>(squadEntity);
            }
            if(Contains(GearID.Turkey) && squadStats.unitType == UnitType.Ranged)
            {
                ecb.AddComponent<AntiLargeTag>(squadEntity);
            }
            if(Contains(GearID.HeavyWeapons) && squadStats.RarityTier == UnitRarity.Rare)
            {
                ecb.AddComponent<ArmorPiercingTag>(squadEntity);
            }
            if(Contains(GearID.QuantitativeEasingPolicy))
            {
                leadership += 10;
            }
        } 

        #region Morale
        ecb.AddComponent(squadEntity, new MoraleComponent
        {
            MoraleState = 0,
            MaxMorale = leadership,
            CurrentMorale = leadership,
            MoraleThreshold = 5f,
        });
        ecb.AddComponent<IsTerrified>(squadEntity);
        ecb.SetComponentEnabled<IsTerrified>(squadEntity, false);
        #endregion

        ecb.AddComponent(squadEntity, new EntityTeam {Value = _enemyData.Team});

        ecb.AddComponent(squadEntity, new NeedsToBeProcessed { });

        //flanking components
        ecb.AddComponent(squadEntity, new IsFlanking { });
        ecb.SetComponentEnabled<IsFlanking>(squadEntity, false);
        ecb.AddComponent(squadEntity, new DisengageFromCombat { });
        ecb.SetComponentEnabled<DisengageFromCombat>(squadEntity, false);

        if(_enemyData.Team==Team.Enemy && TabletopTavernData.Instance.GetUnitSizeFromUnitName(_enemyData.unitName) == UnitSize.Cavalry) {
            ecb.AddComponent(squadEntity, new CavalryFlankingTag {});
            // Debug.Log($"Added CavalryFlankingTag to squad {_enemyData.unitName} ({squadId})");
        }

        DynamicBuffer<EntityReferenceBufferElement> entityBuffer = ecbSystem.CreateCommandBuffer().AddBuffer<EntityReferenceBufferElement>(squadEntity);
        
        //morale components
        ecb.AddComponent(squadEntity, new PreviousHealth { Value = currentHealth });
        ecb.AddComponent<HealthLossPercent>(squadEntity);
        ecb.AddComponent<PreviousDamageDealt>(squadEntity);
        ecb.AddBuffer<HealthLossEvent>(squadEntity);
        ecb.AddBuffer<DamageDealtEvent>(squadEntity);
        ecb.AddComponent(squadEntity, new SquadDamageComponent { SquadId = squadId });
        ecb.AddComponent<RetreatingNearbyAllies>(squadEntity);
        ecb.SetComponentEnabled<RetreatingNearbyAllies>(squadEntity, false);
        ecb.AddComponent<TakingFlankingDamage>(squadEntity);
        ecb.SetComponentEnabled<TakingFlankingDamage>(squadEntity, false);
        ecb.AddComponent<TakingFireDamage>(squadEntity);
        ecb.SetComponentEnabled<TakingFireDamage>(squadEntity, false);
        ecb.AddComponent<HasTakenDamage>(squadEntity);
        ecb.SetComponentEnabled<HasTakenDamage>(squadEntity, false);
        ecb.AddComponent<ArmyLossesPenaltyTag>(squadEntity);
        ecb.SetComponentEnabled<ArmyLossesPenaltyTag>(squadEntity, false);

        ecbSystem.CreateCommandBuffer().AddBuffer<HealthLossEvent>(squadEntity);

        ecb.AddComponent<JustFollowingOrders>(squadEntity);
        ecb.SetComponentEnabled<JustFollowingOrders>(squadEntity, false);

        ecb.AddComponent<BracedTag>(squadEntity);
        ecb.SetComponentEnabled<BracedTag>(squadEntity, false);

        ecb.AddComponent<DefensiveStanceTag>(squadEntity);
        ecb.SetComponentEnabled<DefensiveStanceTag>(squadEntity, false);

        //getting speed 
        float speed = squadStats.Speed;

        for (int i = 0; i < _entities.Count; i++)
        {

            Entity debugEntity = entityManager.Instantiate(_enemyData.Team == Team.Player ? entitiesReferences.debugPlayerUnitPositionPrefab : entitiesReferences.debugEnemyUnitPositionPrefab);
            float scale = 1f;
#if !UNITY_EDITOR
                scale = 0f;
#endif
            ecb.AddComponent(debugEntity, new LocalTransform()
            {
                Position = entityManager.GetComponentData<LocalTransform>(_entities[i]).Position,
                Rotation = quaternion.identity,
                Scale = scale
            });

            float3 positionOffset = _enemyData.entityPositions[i] - averagePosition;

            entityBuffer.Add(new EntityReferenceBufferElement
            {
                Entity = _entities[i],
                PositionOffset = positionOffset,
                DebugEntity = debugEntity
            });

            Entity unitEntity = _entities[i];
            AgentLocomotion agentLocomotion = entityManager.GetComponentData<AgentLocomotion>(unitEntity);
            agentLocomotion.Speed = speed / 10f;
            agentLocomotion.Acceleration = (speed + 10) / 10f;
            entityManager.SetComponentData(unitEntity, agentLocomotion);

            if (_spawnPrestige > 0) ecb.AddComponent(_entities[i], new UnitPrestigeSetUpTag { PrestigeLevel = _spawnPrestige, GrantedTrait = _prestigeTrait });

            if(squadStats.unitType == UnitType.Structure)
            {
                ecb.AddComponent(_entities[i], new UnitStatsSetUpTag { HealthOverride = currentHealth });
            }
            else
            {
                ecb.AddComponent(_entities[i], new UnitStatsSetUpTag { });
            }
            ecb.AddComponent(_entities[i], new UnitParentEntityTag { parentSquadEntity = squadEntity });

            // add a new componet that is diabled by default
            ecb.AddComponent(squadEntity, new DestroyEntityTag { });
            ecb.SetComponentEnabled<DestroyEntityTag>(squadEntity, false);

            if (squadStats.SquadAttributes.FlamingAmmo)
            {
                ecb.AddComponent<FlamingRangedAttackTag>(unitEntity);
            }
        }

        if (unitPrestigeDict.ContainsKey(squadId)) unitPrestigeDict[squadId] = _spawnPrestige;
        else unitPrestigeDict.Add(squadId, _spawnPrestige);

        if (squadPrestigeTraitDict.ContainsKey(squadId)) squadPrestigeTraitDict[squadId] = _prestigeTrait;
        else squadPrestigeTraitDict.Add(squadId, _prestigeTrait);

        BattleManager.Instance.ArmySpawnManager.RecordSquadUniqueID(_uniqueID, squadId, _entities.Count);
        // Debug.Log($"Registered squad {squadId} with {_entities.Count} entities and {_spawnPrestige} prestige");

        ecb.AddBuffer<QueuedOrder>(squadEntity);
        ecb.AddComponent<CompleteQueuedOrderTag>(squadEntity);
        ecb.SetComponentEnabled<CompleteQueuedOrderTag>(squadEntity, false);

        ecb.AddComponent<CeaseFireTag>(squadEntity);
        ecb.SetComponentEnabled<CeaseFireTag>(squadEntity, false);

        ecb.AddComponent<CeaseFireRequestedTag>(squadEntity);
        ecb.SetComponentEnabled<CeaseFireRequestedTag>(squadEntity, false);

        if (squadStats.unitType == UnitType.Structure)
        {
            int gateIndex = 0;
            if (_uniqueID.StartsWith("Gate_")) int.TryParse(_uniqueID[5..], out gateIndex);
            ecb.AddComponent(squadEntity, new GarrisonGateSquadTag { GateIndex = gateIndex });
            ecb.AddComponent<SetUpGarrisonGateSquad>(squadEntity);
            ecb.SetComponentEnabled<SetUpGarrisonGateSquad>(squadEntity, true);
        }

#if UNITY_EDITOR
        entityManager.SetName(squadEntity, $"Squad Entity {_enemyData.unitName} {squadId} ({_enemyData.Team})");
#endif

        query.Dispose();
        query2.Dispose();
    }
    #endregion

    public void CreateArcherRangeDrawer(SquadEntity squadEntity)
    {
        ArcherRangeDrawer archerRangeDrawer = Instantiate(archerRangeDrawerPrefab, Vector3.zero, Quaternion.identity);
        archerRangeDrawer.SetUp(squadEntity);
        archerRangeDrawer.TurnOff();
        squadRangeDrawers.Add(squadEntity.SquadId, archerRangeDrawer);
    }
    public void UpdateArcherRangeDrawer(SquadEntity squadEntity)
    {
        // Debug.Log($"Updating archer range drawer for squad {squadEntity.SquadId}");
        if (squadRangeDrawers.ContainsKey(squadEntity.SquadId))
        {
            squadRangeDrawers[squadEntity.SquadId].Recalculate();
        }
        // Debug.Log($"Completed archer range drawer for squad {squadEntity.SquadId}");
    }
    public void RemoveArcherRangeDrawer(int squadId)
    {
        if(squadRangeDrawers.ContainsKey(squadId)) {
            Destroy(squadRangeDrawers[squadId].gameObject);
            squadRangeDrawers.Remove(squadId);
        }
    }

    public void RegisterGateRangeDrawer(int squadId, GarrisonGateRangeDrawer drawer)
    {
        if (!gateRangeDrawers.ContainsKey(squadId))
            gateRangeDrawers.Add(squadId, drawer);
    }

    public void RemoveGateRangeDrawer(int squadId)
    {
        gateRangeDrawers.Remove(squadId);
    }
    
    #region Save Data
    public void SaveFormation()
    {
        bool isCustomBattle = BattleManager.Instance.BattleSaveManager.IsCustomBattle;
        SaveFormation(
            savingPlayer: true,
            isCustomBattle: isCustomBattle
        );

        if(isCustomBattle) //also save enemy formation
        {
            SaveFormation(
                savingPlayer: false, 
                isCustomBattle: true
            );
        }
    }
    public void SaveFormation(bool savingPlayer, bool isCustomBattle)
    {
        CampaignSaveData saveData = SaveDataHandler.Load();
        CustomBattleSaveData customBattleData = SaveDataHandler.LoadCustomBattleSaveData();

        ComponentType squadTeamComponent = savingPlayer ? ComponentType.ReadOnly<PlayerSquad>() : ComponentType.ReadOnly<EnemySquad>();
        using var playerSquadEntities = RetrieveSquadEntities(squadTeamComponent);
        using var playerSquadsMovement = RetrieveSquadsMovement(squadTeamComponent);

        List<SquadBattlePosition> battlePositions = new ();
        
        int i = 0;
        foreach (SquadEntity squad in playerSquadEntities)
        {
            // Summoned squads have no slot in the saved roster, so persisting a battle position
            // keyed to their GUID would leave an entry nothing can ever resolve.
            if(BattleManager.Instance.ArmySpawnManager.IsSummonedSquad(squad.SquadId)) continue;

            Vector3 position = new ();
            Quaternion rotation = new ();
            int2 squadWidthAndDepth = new (0, 0);
            foreach (SquadMovementComponent squadMovement in playerSquadsMovement)
            {
                if(squadMovement.SelfEntity == squad.SelfEntity){
                    position = squadMovement.GoalPosition;
                    rotation = squadMovement.SquadRotation;
                    squadWidthAndDepth = squadMovement.SquadWidthAndDepth;
                    break;
                }
            }

            string squadUniqueID = BattleManager.Instance.ArmySpawnManager.GetUnitUniqueIDFromSquadID(squad.SquadId);

            SquadBattlePosition battlePosition = new ()
            {
                SquadUniqueID = squadUniqueID,
                Position = position,
                Rotation = rotation,
                SquadWidthAndDepth = squadWidthAndDepth,
            };
            battlePositions.Add(battlePosition);
            i++;
        }

        List<SavedSquadGroup> savedGroups = new();
        if (savingPlayer)
        {
            SquadGroup[] squadGroups = BattleManager.Instance.GroupManager.SquadGroups;
            for (int j = 0; j < squadGroups.Length; j++)
            {
                if (squadGroups[j].squadIds.Count == 0) continue;
                SavedSquadGroup savedGroup = new() { slotIndex = j };
                foreach (int squadId in squadGroups[j].squadIds)
                {
                    // Same reason as the battle positions above - a summoned squad's GUID would fail
                    // to resolve back to a squad on the next load.
                    if (BattleManager.Instance.ArmySpawnManager.IsSummonedSquad(squadId)) continue;

                    string uniqueId = BattleManager.Instance.ArmySpawnManager.GetUnitUniqueIDFromSquadID(squadId);
                    if (!string.IsNullOrEmpty(uniqueId))
                        savedGroup.squadUniqueIds.Add(uniqueId);
                }
                savedGroups.Add(savedGroup);
            }
        }

        if(savingPlayer)
        {
            if(isCustomBattle)
            {
                // Unlike the campaign branch below, this one builds the saved army from the live
                // squads, so summons must be filtered out here or they would persist into the
                // custom battle's roster and come back as real units next load.
                var playerArmyList = new List<SquadToLoad>();
                for (int j = 0; j < playerSquadEntities.Length; j++)
                {
                    if (BattleManager.Instance.ArmySpawnManager.IsSummonedSquad(playerSquadEntities[j].SquadId)) continue;

                    playerArmyList.Add(new SquadToLoad(
                        playerSquadEntities[j].UnitName,
                        _prestige: GetSquadPrestige(playerSquadEntities[j].SquadId),
                        playerArmyList.Count
                    )
                    {
                        UniqueID = BattleManager.Instance.ArmySpawnManager.GetUnitUniqueIDFromSquadID(playerSquadEntities[j].SquadId)
                    });
                }
                customBattleData.playerCustomBattleArmy = playerArmyList.ToArray();
                customBattleData.playerCustomBattleSquadBattlePositions = battlePositions;
                customBattleData.playerCustomBattleSquadGroups = savedGroups;
                SaveDataHandler.SaveCustomBattleSaveData(customBattleData);
            }
            else
            {
                (SquadToLoad[] orderOfSquadsInBattle, Dictionary<string, SquadBattlePosition> battlePositionsDictionary) = BattleManager.Instance.BattleSaveManager.GetArmyFromSaveData(savingPlayer);

                Array.Sort(orderOfSquadsInBattle, (a, b) => 
                {
                    int squadIdA = BattleManager.Instance.ArmySpawnManager.GetSquadIDFromUnitUniqueID(a.UniqueID);
                    int squadIdB = BattleManager.Instance.ArmySpawnManager.GetSquadIDFromUnitUniqueID(b.UniqueID);
                    int indexA = trueSquadOrder.IndexOf(squadIdA);
                    int indexB = trueSquadOrder.IndexOf(squadIdB);
                    return indexA.CompareTo(indexB);
                });
                
                SquadToLoad[] playerSquadToLoads = new SquadToLoad[playerSquadEntities.Length];
                for (int j = 0; j < playerSquadEntities.Length; j++)
                {
                    playerSquadToLoads[j] = new(
                        playerSquadEntities[j].UnitName,
                        _prestige: GetSquadPrestige(playerSquadEntities[j].SquadId),
                        j
                    )
                    {
                        UniqueID = BattleManager.Instance.ArmySpawnManager.GetUnitUniqueIDFromSquadID(playerSquadEntities[j].SquadId)
                    };
                }

                //overwrite player army with current formation
                for(int j = 0; j < orderOfSquadsInBattle.Length; j++)
                {
                    saveData.playerArmy[j] = orderOfSquadsInBattle[j];
                    saveData.playerArmy[j].UnitIndex = j;
                }

                saveData.playerSquadBattlePositions = battlePositions;
                saveData.playerSquadGroups = savedGroups;
                SaveDataHandler.SaveCampaign(saveData);
            }
        }
        else
        {
            if(isCustomBattle)
            {
                var enemyArmyList = new List<SquadToLoad>();
                for (int j = 0; j < playerSquadEntities.Length; j++)
                {
                    if (playerSquadEntities[j].UnitName == UnitName.Gate) continue;
                    enemyArmyList.Add(new SquadToLoad(
                        playerSquadEntities[j].UnitName,
                        _prestige: GetSquadPrestige(playerSquadEntities[j].SquadId),
                        enemyArmyList.Count
                    )
                    {
                        UniqueID = BattleManager.Instance.ArmySpawnManager.GetUnitUniqueIDFromSquadID(playerSquadEntities[j].SquadId)
                    });
                }
                customBattleData.enemyCustomBattleArmy = enemyArmyList.ToArray();
                customBattleData.enemyCustomBattleSquadBattlePositions = battlePositions;
                SaveDataHandler.SaveCustomBattleSaveData(customBattleData);
            }
        }
        string formationSavedLocalized = LocalizationManager.Instance.GetText("formationSaved");
        NotificationManager.Instance.DisplayNotification(formationSavedLocalized);
    }
    [ContextMenu("Clear Squad Data")]
    public void ClearSquadData()
    {
        SquadSaveData squadSaveData = Memori.Utilities.JSONFileHandler.GetSaveData<SquadSaveData>("squadSaveData.json");
        squadSaveData.squads = new List<SquadSpawnData>();
        Memori.Utilities.JSONFileHandler.SaveToJSON(squadSaveData, "squadSaveData.json");
    }
    [ContextMenu("Clear Enemy Data")]
    public void ClearEnemyData()
    {
        SquadSaveData squadSaveData = Memori.Utilities.JSONFileHandler.GetSaveData<SquadSaveData>("squadSaveData.json");
        squadSaveData.enemies = new List<SquadSpawnData>();
        Memori.Utilities.JSONFileHandler.SaveToJSON(squadSaveData, "squadSaveData.json");
    }
    [ContextMenu("Open Save Folder")]
    public void OpenSaveFolder()
    {
        Memori.Utilities.JSONFileHandler.OpenSaveFolder();
    }
    #endregion
    public void AssignWidthAndDepthToSquad(int2 _unitWidthAndDepth, int _squadId)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        using var allSquads = RetrieveAllSquads();
        foreach (var squadEntity in allSquads)
        {
            if(squadEntity.SquadId != _squadId) continue;

            SquadMovementComponent squad = entityManager.GetComponentData<SquadMovementComponent>(squadEntity.SelfEntity);
            SquadEntity squadEntityro = entityManager.GetComponentData<SquadEntity>(squadEntity.SelfEntity);
            // Debug.Log($"current width and depth: {squad.SquadWidthAndDepth}");
            squad.SetWidthAndDepth(new int2(_unitWidthAndDepth.x, _unitWidthAndDepth.y));
            // Debug.Log($"new width and depth: {squad.SquadWidthAndDepth}");
            entityManager.SetComponentData(squadEntity.SelfEntity, squad);
            
            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            ecb.AddComponent(squadEntity.SelfEntity, new FormationShapeChanged {});

            UpdateArcherRangeDrawer(squadEntityro);
        }
    }
    #region Squad Battle Commands
    public void SetGuardMode(bool _guardMode)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        NativeArray<SquadEntity> playerSquads = RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());

        foreach (var squadEntity in playerSquads)
        {
            if (!BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId)) continue;

            SquadOverridesComponent squad = entityManager.GetComponentData<SquadOverridesComponent>(squadEntity.SelfEntity);
            squad.GuardMode = _guardMode;
            entityManager.SetComponentData(squadEntity.SelfEntity, squad);
        }
        playerSquads.Dispose();
    }
    public void SetAutoTarget(bool autoTarget)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        NativeArray<SquadEntity> playerSquads = RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());

        foreach (var squadEntity in playerSquads)
        {
            if (!BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId)) continue;

            SquadOverridesComponent squad = entityManager.GetComponentData<SquadOverridesComponent>(squadEntity.SelfEntity);
            if (squad.UnitType == UnitType.Melee) continue;
            
            squad.AutoTarget = autoTarget;
            entityManager.SetComponentData(squadEntity.SelfEntity, squad);
        }
        playerSquads.Dispose();
    }
    public void SetMeleeMode(bool _meleeMode)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        NativeArray<SquadEntity> playerSquads = RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());
        EntityCommandBuffer entityCommandBuffer = ecbSystem.CreateCommandBuffer();

        foreach (var squadEntity in playerSquads)
        {
            if (!BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId)) continue;

            SquadOverridesComponent squad = entityManager.GetComponentData<SquadOverridesComponent>(squadEntity.SelfEntity);
            if (squad.UnitType == UnitType.Melee) continue;

            squad.MeleeMode = _meleeMode;
            // Debug.Log($"Updated melee mode for squad {squadEntity.SquadId} to {_meleeMode}");

            BattleManager.Instance.UIManager.UpdateAttackArrowToMelee(squadEntity.SquadId, _meleeMode);

            entityManager.SetComponentData(squadEntity.SelfEntity, squad);
            entityCommandBuffer.AddComponent(squadEntity.SelfEntity, new SwitchToMeleeTag { SwitchType = _meleeMode ? RangedToMeleeSwitchType.Melee : RangedToMeleeSwitchType.Ranged });

            if (squadRangeDrawers.ContainsKey(squadEntity.SquadId))
            {
                squadRangeDrawers[squadEntity.SquadId].SwitchToMelee(_meleeMode);
            }
        }
        playerSquads.Dispose();
    }
    public void SetVolleyFire()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        NativeArray<SquadEntity> playerSquads = RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());

        foreach (var squadEntity in playerSquads)
        {
            if (!BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId)) continue;
            if (!entityManager.HasComponent<RangedFireModeSquadComponent>(squadEntity.SelfEntity)) continue;

            RangedFireModeSquadComponent rangedFireMode = entityManager.GetComponentData<RangedFireModeSquadComponent>(squadEntity.SelfEntity);
            rangedFireMode.FireMode = RangedFireMode.Volley;
            rangedFireMode.SwitchRequested = true;
            
            entityManager.SetComponentData(squadEntity.SelfEntity, rangedFireMode);

            if (!entityManager.HasComponent<SquadOverridesComponent>(squadEntity.SelfEntity)) continue;

            SquadOverridesComponent squadOverrides = entityManager.GetComponentData<SquadOverridesComponent>(squadEntity.SelfEntity);
            squadOverrides.FireMode = RangedFireMode.Volley;

            entityManager.SetComponentData(squadEntity.SelfEntity, squadOverrides);
        }
        playerSquads.Dispose();
    }
    public void SetFireAtWill()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        NativeArray<SquadEntity> playerSquads = RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());

        foreach (var squadEntity in playerSquads)
        {
            if (!BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId)) continue;
            if (!entityManager.HasComponent<RangedFireModeSquadComponent>(squadEntity.SelfEntity)) continue;

            RangedFireModeSquadComponent rangedFireMode = entityManager.GetComponentData<RangedFireModeSquadComponent>(squadEntity.SelfEntity);
            rangedFireMode.FireMode = RangedFireMode.FireAtWill;
            rangedFireMode.SwitchRequested = true;
            
            entityManager.SetComponentData(squadEntity.SelfEntity, rangedFireMode);

            if (!entityManager.HasComponent<SquadOverridesComponent>(squadEntity.SelfEntity)) continue;

            SquadOverridesComponent squadOverrides = entityManager.GetComponentData<SquadOverridesComponent>(squadEntity.SelfEntity);
            squadOverrides.FireMode = RangedFireMode.FireAtWill;

            entityManager.SetComponentData(squadEntity.SelfEntity, squadOverrides);
        }
        playerSquads.Dispose();
    }
    public void SetBalancedStance()
    {
        Debug.Log($"Setting balanced stance...");
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        NativeArray<SquadEntity> playerSquads = RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());

        foreach (var squadEntity in playerSquads)
        {
            if (!BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId)) continue;
            if (!entityManager.HasComponent<ShieldedStanceSquadComponent>(squadEntity.SelfEntity)) continue;

            ShieldedStanceSquadComponent shieldedStance = entityManager.GetComponentData<ShieldedStanceSquadComponent>(squadEntity.SelfEntity);
            shieldedStance.Stance = ShieldedStance.Balanced;
            shieldedStance.SwitchRequested = true;
            
            entityManager.SetComponentData(squadEntity.SelfEntity, shieldedStance);

            if (!entityManager.HasComponent<SquadOverridesComponent>(squadEntity.SelfEntity)) continue;

            SquadOverridesComponent squadOverrides = entityManager.GetComponentData<SquadOverridesComponent>(squadEntity.SelfEntity);
            squadOverrides.ShieldedStance = ShieldedStance.Balanced;

            entityManager.SetComponentData(squadEntity.SelfEntity, squadOverrides);
        }
        playerSquads.Dispose();
    }
    public void SetDefensiveStance()
    {
        Debug.Log($"Setting defensive stance...");
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        NativeArray<SquadEntity> playerSquads = RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());

        foreach (var squadEntity in playerSquads)
        {
            if (!BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId)) continue;
            if (!entityManager.HasComponent<ShieldedStanceSquadComponent>(squadEntity.SelfEntity)) continue;
            if (!entityManager.HasComponent<SquadOverridesComponent>(squadEntity.SelfEntity)) continue;

            SetDefensiveStancForEntity(squadEntity);
        }
        playerSquads.Dispose();
    }
    public void SetDefensiveStancForEntity(SquadEntity squadEntity)
    {
        ShieldedStanceSquadComponent shieldedStance = entityManager.GetComponentData<ShieldedStanceSquadComponent>(squadEntity.SelfEntity);
        if(shieldedStance.Stance == ShieldedStance.None) return;
        SquadOverridesComponent squadOverrides = entityManager.GetComponentData<SquadOverridesComponent>(squadEntity.SelfEntity);

        shieldedStance.Stance = ShieldedStance.Defensive;
        shieldedStance.SwitchRequested = true;
        squadOverrides.ShieldedStance = ShieldedStance.Defensive;

        entityManager.SetComponentData(squadEntity.SelfEntity, shieldedStance);
        entityManager.SetComponentData(squadEntity.SelfEntity, squadOverrides);
    }
    public void CeaseFire()
    {
        Debug.Log($"Setting cease fire...");
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        NativeArray<SquadEntity> playerSquads = RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());

        foreach (var squadEntity in playerSquads)
        {
            if (!BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId)) continue;

            entityManager.SetComponentEnabled<CeaseFireRequestedTag>(squadEntity.SelfEntity, true);

            SquadOverridesComponent squadOverrides = entityManager.GetComponentData<SquadOverridesComponent>(squadEntity.SelfEntity);
            squadOverrides.CeaseFire = true;
            entityManager.SetComponentData(squadEntity.SelfEntity, squadOverrides);
        }
        playerSquads.Dispose();
    }
    #endregion
    public SquadSFXManager SetUpSquadFlag(SquadEntity _squadEntity, Team _team, int _squadId, MoraleComponent _moraleComponent)
    {
        Material GetFlagMaterial(Team team, SquadStats _squadStats, int ActiveHeroID, bool isCustomBattle, Material raceFlagBaseMaterial)
        {
            Race race = TabletopTavernData.Instance.GetRaceFromUnitName(_squadStats.unitName);
            if (!isCustomBattle && team == Team.Player)
            {
                race = HeroData.GetHeroByID(ActiveHeroID).Race;
            }
            RaceData raceData = TabletopTavernData.Instance.GetRaceData(race);
            
            // Step 2: Create a new instance of the material
            Material materialInstance = new (raceFlagBaseMaterial);
            Sprite selectedSprite = TabletopTavernData.Instance.GetSquadTypeFlagSprite(_squadStats.unitName);

            if (selectedSprite != null)
            {
                materialInstance.SetTexture("_IconSprite", selectedSprite.texture);
            }

            materialInstance.SetColor("_PrimaryColor", raceData.PrimaryColor);
            materialInstance.SetColor("_SecondaryColor", raceData.SecondaryColor);
            materialInstance.SetColor("_OutlineColor", raceData.AccentColor);

            return materialInstance;
        }
        
        UnitSize unitSize = TabletopTavernData.Instance.GetUnitSizeFromUnitName(_squadEntity.UnitName);
        GameObject flagInstance = Instantiate(flagPrefab, this.transform);
        SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(_squadEntity.UnitName);
        Material flagMaterial = GetFlagMaterial(
            _team,
            squadStats,
            HeroBonusManager.Instance.ActiveHeroID,
            BattleManager.Instance.BattleSaveManager.IsCustomBattle,
            raceFlagBaseMaterial
        );
        int ammunition = squadStats.Ammunition;
        // Prestige charges are folded in for mages only, so the bar's max matches what EntityWatcher
        // actually put in SquadAmmunition. Deliberately not extended to archers and artillery: their
        // bars have always been authored off the base value, and a mage's pool is small enough
        // (3 charges, +1 per prestige level) that the same discrepancy would be a third of the bar.
        if (TabletopTavernConstants.Casts(squadStats.unitType))
            ammunition += TabletopTavernConstants.PRESTIGE_AMMO_BONUS_MAGE * GetSquadPrestige(_squadId);
        // Hero-granted ammunition bonuses (e.g. Bertha/14 Supply Lines) now come from
        // HeroBonusManager's rule data, same source as EntityWatcher's real Ammunition value.
        if (HeroBonusManager.Instance.ActiveHeroID != -1)
        {
            foreach (var bonus in HeroBonusManager.GetHeroStatBonus(UnitStat.Ammunition, _squadEntity.UnitName, HeroBonusManager.Instance.ActiveHeroID, ammunition))
                ammunition += (int)bonus.Value;
        }
        SquadFlagGameObject flag = flagInstance.GetComponent<SquadFlagGameObject>();
        flag.SetUp(flagMaterial, _squadId, unitSize, _moraleComponent, _squadEntity.SelfEntity, ammunition, _squadEntity.UnitName);
        stuffToDestroy.Add(flagInstance);
        return flag.SFXManager;
    }
    public void OnSquadUpdatedEvent(int _squadId, float2 _unitCount)
    {
        OnSquadUpdated?.Invoke(_squadId, _unitCount);
    }
    public void CleanUpScene()
    {
        for (int i = 0; i < stuffToDestroy.Count; i++)
        {
            if (stuffToDestroy[i] == null) continue;
            Destroy(stuffToDestroy[i]);
        }
        stuffToDestroy.Clear();
        foreach (var squadRangeDrawer in squadRangeDrawers)
        {
            if (squadRangeDrawer.Value != null)
            {
                Destroy(squadRangeDrawer.Value.gameObject);
            }
        }
        squadRangeDrawers.Clear();
        gateRangeDrawers.Clear();
    }
    private float3 GetPointOnTerrain(float3 _origionalPoint)
    {
        //raycast down at point 
        if (Physics.Raycast(new Vector3(_origionalPoint.x, 10, _origionalPoint.z), Vector3.down, out RaycastHit hit, 11, ~LayerMask.NameToLayer("Tile"))) {
            return hit.point;
        } else {
            Debug.LogError("No terrain found");
            return _origionalPoint;
        }
    }
    public void DeleteAllSquads()
    {
        var ecb = ecbSystem.CreateCommandBuffer();
        NativeArray<SquadEntity> allSquads = RetrieveAllSquads();
        foreach (var squadEntity in allSquads)
            ecb.AddComponent(squadEntity.SelfEntity, new DeleteSquadTag {});
        allSquads.Dispose();
    }
    public void DeleteSelectedSquads()
    {
        NativeArray<SquadEntity> allSquads = RetrieveAllSquads();
        foreach (var squadEntity in allSquads)
        {
            if(!BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId)) continue;

            // UnRegisterSquad(squadEntity.SquadId);
            Debug.Log($"Deleting squad in custom battle {squadEntity.SquadId}");

            var ecb = ecbSystem.CreateCommandBuffer();
            ecb.AddComponent(squadEntity.SelfEntity, new DeleteSquadTag {});

            Team team = entityManager.HasComponent<PlayerSquad>(squadEntity.SelfEntity) ? Team.Player : Team.Enemy;
            // if(team == Team.Player) playerSquadsRegistered--;
            // else enemySquadsRegistered--;
        }
        allSquads.Dispose();
    }
    public void WithdrawSquad(int _squadId)
    {
        BattleManager.Instance.ArmySpawnManager.WithdrawSquad(_squadId, GetSquadUnitCount(_squadId));
        NativeArray<SquadEntity> allSquads = RetrieveAllSquads();
        var ecb = ecbSystem.CreateCommandBuffer();
        foreach (var squadEntity in allSquads)
        {
            if (squadEntity.SquadId != _squadId) continue;

            ecb.AddComponent(squadEntity.SelfEntity, new DeleteSquadTag { });

            // Team team = entityManager.HasComponent<PlayerSquad>(squadEntity.SelfEntity) ? Team.Player : Team.Enemy;
            // if(team == Team.Player) playerSquadsRegistered--;
            // else enemySquadsRegistered--;
        }
        allSquads.Dispose();
    }
    public void BreakSelectedSquads()
    {
        NativeArray<SquadEntity> allSquads = RetrieveAllSquads();
        var ecb = ecbSystem.CreateCommandBuffer();
        foreach (var squadEntity in allSquads)
        {
            if (!BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId)) continue;

            ecb.AddComponent(squadEntity.SelfEntity, new BreakSquadTag { });
        }
        
        allSquads.Dispose();
    }
    public void WipeRegisteredSquadData(bool _playerSquads)
    {
        if (_playerSquads) playerSquadsRegistered = 0;
        else enemySquadsRegistered = 0;
    }
    public int GetSquadPrestige(int _squadId)
    {
        if(unitPrestigeDict.ContainsKey(_squadId)) return unitPrestigeDict[_squadId];
        return 0;
    }
    public UnitAttribute GetSquadPrestigeTrait(int _squadId)
    {
        if(squadPrestigeTraitDict.ContainsKey(_squadId)) return squadPrestigeTraitDict[_squadId];
        return UnitAttribute.None;
    }
    public void ToggleAllRanges()
    {
        rangesVisible = !rangesVisible;
        using var allSquadsToggle = RetrieveAllSquads();
        foreach (var squadEntity in allSquadsToggle)
        {
            if (squadRangeDrawers.ContainsKey(squadEntity.SquadId))
                squadRangeDrawers[squadEntity.SquadId].ShowAllRanges(rangesVisible);
        }
    }
    public void ShowAllRanges()
    {
        using var allSquadsShow = RetrieveAllSquads();
        foreach (var squadEntity in allSquadsShow)
        {
            if(squadRangeDrawers.ContainsKey(squadEntity.SquadId)) {
                squadRangeDrawers[squadEntity.SquadId].ShowAllRanges(true);
            }
        }
    }
    public void HideAllRanges()
    {
        using var allSquadsHide = RetrieveAllSquads();
        foreach (var squadEntity in allSquadsHide)
        {
            if(squadRangeDrawers.ContainsKey(squadEntity.SquadId)) {
                squadRangeDrawers[squadEntity.SquadId].ShowAllRanges(false);
            }
        }
    }
    public void TerrifiedSquads(List<int> squadIds)
    {
        bool listChanged = false;
        //check if different squad ids are in the list
        foreach (var squadId in squadIds) {
            if(!TerrifiedSquadIds.Contains(squadId)) {
                TerrifiedSquadIds.Add(squadId);
                listChanged = true;
            }
        }
        //check if squad ids are in the list and remove them
        for (int i = 0; i < TerrifiedSquadIds.Count; i++) {
            if(!squadIds.Contains(TerrifiedSquadIds[i])) {
                TerrifiedSquadIds.RemoveAt(i);
                i--;
                listChanged = true;
            }
        }

        if(listChanged) {
            OnTerrifiedSquadsChanged?.Invoke(TerrifiedSquadIds);
        }
    }
    public void ChargingSquads(List<int> squadIds)
    {
        bool listChanged = false;
        foreach (var squadId in squadIds)
        {
            if (!chargingSquadIds.Contains(squadId)) { chargingSquadIds.Add(squadId); listChanged = true; }
        }
        for (int i = chargingSquadIds.Count - 1; i >= 0; i--)
        {
            if (!squadIds.Contains(chargingSquadIds[i])) { chargingSquadIds.RemoveAt(i); listChanged = true; }
        }
        if (listChanged) OnChargingSquadsChanged?.Invoke(chargingSquadIds);
    }
    public void DestroyedSquads(List<int> squadIds)
    {
        foreach (var squadId in squadIds) {
            OnDestroyedSquad?.Invoke(squadId);
            Debug.Log($"squad {squadId} destroyed");
        }
    }
    public void RaiseBattlefieldBonusApplied(int squadId, BattlefieldBonusEnum bonus, UnitStat stat, float value)
    {
        Debug.Log($"[BattlefieldBonusApplied] squad {squadId}: {bonus} ({stat} +{value})");
        OnBattlefieldBonusApplied?.Invoke(squadId, bonus, stat, value);
    }
    public void OnDestroy()
    {
        if (InputHandler.HasInstance)
            InputHandler.Instance.OnShowUnitMovement -= ToggleAllRanges;
        if(BattleManager.HasInstance && BattleManager.Instance.SquadOrderManager != null)
            BattleManager.Instance.SquadOrderManager.OnSquadOrderChanged -= OnSquadOrderChanged;
    }
    private void OnSquadOrderChanged(IReadOnlyList<int> newOrder)
    {
        trueSquadOrder = new List<int>(newOrder);
    }
    public List<int> GetTrueSquadOrder()
    {
        return trueSquadOrder;
    }
}
