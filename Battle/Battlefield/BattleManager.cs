using UnityEngine;
using System;
using System.Collections.Generic;
using Memori.Utilities;
using Memori.Scenes;
using Unity.Entities;
using TJ;
using TJ.Battle;
using TJ.Spells;
using Memori.SaveData;
using Memori.Steamworks;
using Unity.Collections;
using TJ.Map;

// namespace TJ
// {
public class BattleManager : Singleton<BattleManager>
{
    [SerializeField] private UnitSelectionManager unitSelectionManager;
    public UnitSelectionManager UnitSelectionManager => unitSelectionManager;
    [SerializeField] private PositionDrawer positionDrawer;
    public PositionDrawer PositionDrawer => positionDrawer;
    [SerializeField] private UIManager uIManager;
    public UIManager UIManager => uIManager;
    [SerializeField] private CustomBattleUIManager customBattleUIManager;
    public CustomBattleUIManager CustomBattleUIManager => customBattleUIManager;
    [SerializeField] private SpawnManager spawnManager;
    public SpawnManager SpawnManager => spawnManager;
    [SerializeField] private SquadManager squadManager;
    public SquadManager SquadManager => squadManager;
    [SerializeField] private TrailRendererSetup trailRendererSetup;
    public TrailRendererSetup TrailRendererSetup => trailRendererSetup;
    [SerializeField] private UnitDebugSetUp unitDebugSetUp;
    public UnitDebugSetUp UnitDebugSetUp => unitDebugSetUp;
    [SerializeField] private UnitPositioningManager unitPositioningManager;
    public UnitPositioningManager UnitPositioningManager => unitPositioningManager;
    [SerializeField] private ExitBattle exitBattle;
    public ExitBattle ExitBattle => exitBattle;
    [SerializeField] private SpellManager spellManager;
    public SpellManager SpellManager => spellManager;
    [SerializeField] private ArmySpawnManager amySaveDataManager;
    public ArmySpawnManager ArmySpawnManager => amySaveDataManager;
    [SerializeField] private BattleSaveManager battleSaveManager;
    public BattleSaveManager BattleSaveManager => battleSaveManager;
    [SerializeField] private SquadMovementManager squadMovementManager;
    public SquadMovementManager SquadMovementManager => squadMovementManager;
    [SerializeField] private BattlefieldEnvManager battlefieldEnvManager;
    public BattlefieldEnvManager BattlefieldEnvManager => battlefieldEnvManager;
    [SerializeField] private GameSpeedManager gameSpeedManager;
    public GameSpeedManager GameSpeedManager => gameSpeedManager;
    [SerializeField] private GreyCompanyBattlefield greyCompanyBattlefield;
    public GreyCompanyBattlefield GreyCompanyBattlefield => greyCompanyBattlefield;
    [SerializeField] private GearManager gearManager;
    public GearManager GearManager => gearManager;
    [SerializeField] private BattlefieldBiomeBonus battlefieldBiomeBonus;
    public BattlefieldBiomeBonus BattlefieldBiomeBonus => battlefieldBiomeBonus;
    [SerializeField] private BattlefieldBonusManager battlefieldBonusManager;
    public BattlefieldBonusManager BattlefieldBonusManager => battlefieldBonusManager;

    [SerializeField] private BattlefieldTutorial battlefieldTutorial;
    public BattlefieldTutorial BattlefieldTutorial => battlefieldTutorial;
    [SerializeField] private GroupManager groupManager;
    public GroupManager GroupManager => groupManager;
    [SerializeField] private SquadOrderManager squadOrderManager;
    public SquadOrderManager SquadOrderManager => squadOrderManager;
    [SerializeField] private MeshTextureUpdater meshTextureUpdater;
    public MeshTextureUpdater MeshTextureUpdater => meshTextureUpdater;
    [SerializeField] private BattleCleanUpManager battleCleanUpManager;
    public BattleCleanUpManager BattleCleanUpManager => battleCleanUpManager;
    [SerializeField] private EntityWatcher entityWatcher;
    public EntityWatcher EntityWatcher => entityWatcher;
    [SerializeField] private UnitGPUAnimLoader unitGPUAnimLoader;
    public UnitGPUAnimLoader UnitGPUAnimLoader => unitGPUAnimLoader;
    [SerializeField] private EnemyGeneral enemyGeneral;
    public EnemyGeneral EnemyGeneral => enemyGeneral;

    //squad events
    public delegate void SquadBroken(int brokenSquadId);
    public event SquadBroken OnSquadBrokenEvent; 

    [Header("Cameras")]
    [SerializeField] private Canvas battleCanvas;
    [SerializeField] private Canvas secondaryBattleCanvas;
    [SerializeField] private BattleCamera battleCameraScript;
    public BattleCamera BattleCameraScript => battleCameraScript;
    [SerializeField] private Camera battleCamera, tavernCamera;
    public Camera BattleCamera => battleCamera;
    AudioListener battleAudioListener;
    [SerializeField] private GameObject lighting;
    [SerializeField] private CameraShaker cameraShaker;
    public CameraShaker CameraShaker => cameraShaker;
    [SerializeField] private MinimapScreenshot minimapScreenshot;
    public MinimapScreenshot MinimapScreenshot => minimapScreenshot;
    public event Action<int> OnGateDestroyed;
    private readonly HashSet<int> _breachedGateIndices = new();
    public bool IsGateBreached(int gateIndex) => _breachedGateIndices.Contains(gateIndex);
    public bool AnyGateBreached => _breachedGateIndices.Count > 0;
    public void NotifyGateDestroyed(int gateIndex)
    {
        UnityEngine.Debug.Log($"BattleManager: NotifyGateDestroyed: gateIndex={gateIndex}");
        _breachedGateIndices.Add(gateIndex);
        OnGateDestroyed?.Invoke(gateIndex);

        // "Siegebreaker" - every gate in this garrison breached. GateCount is 0 outside garrison battles.
        int gateCount = ArmySpawnManager.GateCount;
        if (gateCount > 0 && _breachedGateIndices.Count >= gateCount)
            SteamAchievements.Unlock(AchievementId.Siegebreaker);
    }

    private bool _onlySakuraUnits;
    public bool OnlySakuraUnits => _onlySakuraUnits;

    [Header("Misc")]
    [SerializeField] private CursorMode cursorMode;
    public CursorMode CursorMode => cursorMode;
    public delegate void CursorModeChanged(CursorMode _cursorMode);
    public event CursorModeChanged OnCursorModeChanged;
    [SerializeField] private GamePhase gamePhase;
    public GamePhase GamePhase => gamePhase;
    public delegate void GamePhaseChanged(GamePhase _gamePhase);
    public event GamePhaseChanged OnGamePhaseChanged;
    [SerializeField] private bool debug;
    public bool Debug => debug;
    public int UnitsToSpawnPrestige;
    private bool playerWon;
    public bool PlayerWon => playerWon;
    [SerializeField] private Team selectedTeam;
    public Team SelectedTeam => selectedTeam;

    protected override void Awake()
    {
        base.Awake();
        UnityEngine.Debug.Log($"BattleManager instantiated!\n{System.Environment.StackTrace}");
    }

    private void Start()
    {
        if (battleCamera != null)
            battleAudioListener = battleCamera.GetComponent<AudioListener>();
        SceneHandler.Instance.OnGameStateChanged += OnGameStateChanged;
    }
    private async void OnGameStateChanged(GameStateEnum gameStateEnum)
    {
        // Debug.Log($"BattleManager: OnGameStateChanged: {gameStateEnum}");
        battleCamera.enabled = gameStateEnum.Equals(GameStateEnum.Battle);
        tavernCamera.enabled = gameStateEnum.Equals(GameStateEnum.Battle);
        if (battleAudioListener != null)
            battleAudioListener.enabled = gameStateEnum.Equals(GameStateEnum.Battle);
        battleCanvas.enabled = gameStateEnum.Equals(GameStateEnum.Battle);
        secondaryBattleCanvas.enabled = gameStateEnum.Equals(GameStateEnum.Battle);
        lighting.SetActive(gameStateEnum.Equals(GameStateEnum.Battle));
        unitSelectionManager.enabled = gameStateEnum.Equals(GameStateEnum.Battle);

        if(gameStateEnum.Equals(GameStateEnum.Battle)) {
            SetGamePhase(GamePhase.SetUp);
            await battleCleanUpManager.LoadSubscene();
            await battleCleanUpManager.LoadOfBattleScene();
            battlefieldTutorial.HandleTutorialStuff();
            StartDeployment();
        }
    }

    public void OnDestroy()
    {
        if(!SceneHandler.HasInstance) return;

        SceneHandler.Instance.OnGameStateChanged -= OnGameStateChanged;
    }
    public void BreakSquad(SquadEntity _squadEntity)
    {
        // Logged because a break is permanent and silently makes the squad uncommandable: the
        // only player-facing sign is the broken overlay on its card, so bug reports of "my unit
        // ran off and I couldn't control it" need this line to be distinguishable from a bug.
        UnityEngine.Debug.Log($"[Morale] Squad {_squadEntity.SquadId} ({_squadEntity.UnitName}, {_squadEntity.Team}) broke and is withdrawing - it can no longer be commanded");
        unitPositioningManager.OrderSquadToWithdraw(_squadEntity);
        uIManager.MarkSquadAsBroken(_squadEntity.SquadId, true);
        OnSquadBrokenEvent?.Invoke(_squadEntity.SquadId);
    }
    public void ConcedeDefeat()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BattlePhase>());
        
        //check if query has any entities
        if(query.CalculateEntityCount() != 0) {
            Entity entity = query.GetSingletonEntity();
            ecb.RemoveComponent<BattlePhase>(entity);
        }

        Entity battleEndEntity = entityManager.CreateEntity();
        ecb.AddComponent(battleEndEntity, new BattleOver { 
            PlayerWon = false 
        });

        query.Dispose();
        ecb.Playback(entityManager);
        ecb.Dispose();
    }
    public void AlertNonSakuraUnits(bool onlySakuraUnits)
    {
        _onlySakuraUnits = onlySakuraUnits;
    }

    public void StartDeployment()
    {
        _breachedGateIndices.Clear();
        // Garrison and custom battles skip the dice roll, so Deployment fires here.
        // Normal campaign battles set Deployment from BattleDiceRollPanel when Continue is clicked.
        if (battleSaveManager.IsGarrisonBattle || battleSaveManager.IsCustomBattle)
            SetGamePhase(GamePhase.Deployment);

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var camQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<CameraPositionComponent>());
        int existing = camQuery.CalculateEntityCount();
        if (existing == 0)
        {
            entityManager.CreateEntity(ComponentType.ReadWrite<CameraPositionComponent>());
            // UnityEngine.Debug.Log("[BattleManager] CameraPositionComponent entity created.");
        }
        else
        {
            UnityEngine.Debug.Log($"[BattleManager] StartDeployment called but {existing} CameraPositionComponent entity/entities already exist — skipping creation.");
        }
        camQuery.Dispose();
    }
    public void SetCursorMode(CursorMode _cursorMode)
    {
        if (cursorMode == _cursorMode) return;
        cursorMode = _cursorMode;
        OnCursorModeChanged?.Invoke(_cursorMode);
    }
    public void SetGamePhase(GamePhase _gamePhase)
    {
        gamePhase = _gamePhase;
        OnGamePhaseChanged?.Invoke(gamePhase);
    }
    public void StartBattle()
    {
        SetGamePhase(GamePhase.Battle);

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        entityManager.CreateEntity(typeof(BattlePhase));
        entityManager.CreateEntity(typeof(BattleHasStarted));

        Entity bloodBufferSingletonEntity = entityManager.CreateEntity();
        entityManager.AddBuffer<BloodBufferElement>(bloodBufferSingletonEntity);

        Entity dustCloudBufferSingletonEntity = entityManager.CreateEntity();
        entityManager.AddBuffer<DustCloudBufferElement>(dustCloudBufferSingletonEntity);

        Entity squadDamageBufferSingletonEntity = entityManager.CreateEntity();
        entityManager.AddBuffer<SquadDamageBufferElement>(squadDamageBufferSingletonEntity);

        Entity battlefieldBonusAppliedBufferSingletonEntity = entityManager.CreateEntity();
        entityManager.AddBuffer<BattlefieldBonusAppliedBufferElement>(battlefieldBonusAppliedBufferSingletonEntity);

#if SPELLS
        // Mage casting rides the same define as the rest of the spell system. Gating it here rather
        // than at the EntityWatcher drain is deliberate: MageCastSystem bails when this singleton is
        // absent, so a build without SPELLS never accumulates cast requests nothing would consume.
        Entity mageCastRequestBufferSingletonEntity = entityManager.CreateEntity();
        entityManager.AddBuffer<MageCastRequestBufferElement>(mageCastRequestBufferSingletonEntity);
#endif

        amySaveDataManager.SpawnDeferredEnemy();
        amySaveDataManager.NotifyOutridersIfPresent();

        // if (findTargets) entityManager.CreateEntity(typeof(FindTargets));

        TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.ChangeBattleSpeed);
        TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.StartBattle);

        var squadQuery = entityManager.CreateEntityQuery(typeof(SquadEntity), typeof(EntityReferenceBufferElement));
        using var squadEntities = squadQuery.ToEntityArray(Allocator.Temp);
        string squadInfo = "Player Squads in battle:\n";
        string enemySquadInfo = "\nEnemy Squads in battle:\n";
        foreach (var squadEntity in squadEntities)
        {
            SquadEntity squad = entityManager.GetComponentData<SquadEntity>(squadEntity);
            if (squad.Team == Team.Player)
            {
                squadInfo += $"{squad.SquadId} : {squad.UnitName}\n";
                if (entityManager.HasComponent<WaitingForCommand>(squadEntity) &&
                    entityManager.IsComponentEnabled<WaitingForCommand>(squadEntity) &&
                    entityManager.HasBuffer<QueuedOrder>(squadEntity))
                {
                    var orders = entityManager.GetBuffer<QueuedOrder>(squadEntity, true);
                    for (int i = 0; i < orders.Length; i++)
                    {
                        if (orders[i].Type == QueuedOrderType.Attack)
                        {
                            entityManager.SetComponentEnabled<WaitingForCommand>(squadEntity, false);
                            break;
                        }
                    }
                }
            }
            else
                enemySquadInfo += $"{squad.SquadId} : {squad.UnitName}\n";
        }
        squadQuery.Dispose();
#if !UNITY_EDITOR
        UnityEngine.Debug.Log(squadInfo + enemySquadInfo);
#endif
    }
    public void EndBattle(bool _playerWon)
    {
        playerWon = _playerWon;
        UnityEngine.Debug.Log($"Battle Ended. Player Won: {_playerWon}");
        Memori.Audio.IAudioRequester.Instance.SwitchToPostBattleMusic(_playerWon);
        SetCursorMode(CursorMode.PostGame);
        SetGamePhase(GamePhase.PostGame);
        GameSpeedManager.LockEndOfBattleSpeed();
    }
    public void SetSelectedTeam(Team team)
    {
        selectedTeam = team;
    }
}