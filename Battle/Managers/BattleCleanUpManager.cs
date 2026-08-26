using UnityEngine;
using System.Collections.Generic;
using Memori.Utilities;
using Memori.Scenes;
using Unity.Scenes;
using Unity.Entities;
using System.Threading.Tasks;
using System;
using TJ;
using TJ.Battle;
using TJ.Spells;
using Memori.SaveData;
using ProjectDawn.Navigation.Hybrid;
using TJ.Map;
using TJ.IrregularGrid;

namespace TJ
{
    public class BattleCleanUpManager : MonoBehaviour
    {
        BattleManager battleManager;
        SquadMovementManager squadMovementManager;
        UIManager uIManager;
        BattlefieldBonusManager battlefieldBonusManager;
        BattleSaveManager battleSaveManager;
        BattlefieldEnvManager battlefieldEnvManager;
        PositionDrawer positionDrawer;
        MinimapScreenshot minimapScreenshot;
        GreyCompanyBattlefield greyCompanyBattlefield;
        CustomBattleUIManager customBattleUIManager;
        MeshTextureUpdater meshTextureUpdater;
        GearManager gearManager;
        SquadManager squadManager;

        public GameObject RegeneratingBattlefieldIndicator;
        [SerializeField] private SubScene subscene;  // Reference to the SubScene object from the editor
        private Entity sceneEntity;
        private bool simulationStarted;
        public bool SimulationStarted => simulationStarted;
        private int seed;
        private DateTime time;
        private bool battlefieldGeneratedAtLeastOnce;

        private void Start()
        {
            battleManager = BattleManager.Instance;
            squadMovementManager = battleManager.SquadMovementManager;
            uIManager = battleManager.UIManager;
            battlefieldBonusManager = battleManager.BattlefieldBonusManager;
            battleSaveManager = battleManager.BattleSaveManager;
            battlefieldEnvManager = battleManager.BattlefieldEnvManager;
            positionDrawer = battleManager.PositionDrawer;
            greyCompanyBattlefield = battleManager.GreyCompanyBattlefield;
            customBattleUIManager = battleManager.CustomBattleUIManager;
            meshTextureUpdater = battleManager.MeshTextureUpdater;
            squadManager = battleManager.SquadManager;
            minimapScreenshot = battleManager.MinimapScreenshot;
            gearManager = battleManager.GearManager;

            SceneHandler.Instance.OnRequestSceneCleanUp += OnRequestSceneCleanUp;
        }
        public async Task LoadOfBattleScene()
        {
#if !UNITY_EDITOR
            Debug.Log($"Loading of battle scene started...");
#endif
            time = DateTime.Now;
            Time.timeScale = 1f;
            while (simulationStarted == false)
            {
                await Task.Yield();
            }

            battleSaveManager.Load();
            uIManager.Load();
            gearManager.LoadAllGear();
            battlefieldEnvManager.LoadBattleConditions();

            bool customBattle = battleSaveManager.IsCustomBattle;
            seed = battleSaveManager.Seed;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity entity = entityManager.CreateEntity(typeof(CampaignSaveDataHolder));
            GearIDsSerialized gearIdsSerialized = SaveDataHandler.GetGearCollected();
            // Debug.Log($"Loaded gear IDs: {gearIdsSerialized.gearID1}, {gearIdsSerialized.gearID2}, {gearIdsSerialized.gearID3}, {gearIdsSerialized.gearID4}, {gearIdsSerialized.gearID5}");
#if !UNITY_EDITOR
            Debug.Log($"is custom battle: {customBattle}");
#endif

            TabletopTavernData.Instance.LoadAndInjectData();

            bool onlySakuraUnits = false;
            if (!customBattle)
            {
                SquadToLoad[] savedArmy = SaveDataHandler.Load().playerArmy;
                onlySakuraUnits = true;
                bool anyValid = false;
                foreach (var s in savedArmy)
                {
                    if (s.UnitIndex == -1) continue;
                    anyValid = true;
                    if (TabletopTavernData.Instance.GetRaceFromUnitName(s.UnitName) != Race.SakuraDynasty)
                    {
                        onlySakuraUnits = false;
                        break;
                    }
                }
                if (!anyValid) onlySakuraUnits = false;
            }

            int activeHeroID = customBattle ? -1 : SaveDataHandler.GetActiveHeroID();
            entityManager.SetComponentData(entity, new CampaignSaveDataHolder
            {
                IsCustomBattle = customBattle,
                Gear = gearIdsSerialized,
                ActiveHeroID = activeHeroID,
                // Lets Systems-assembly ECS code (which can't reference HeroData, a main-assembly
                // type) resolve the active hero's race without calling back into managed code -
                // same pattern as EnemyRace below.
                PlayerHeroRace = activeHeroID == -1 ? Race.Special : HeroData.GetRaceFromHero(activeHeroID),
                EnemyRace = SaveDataHandler.GetEnemyRace(),
                OnlySakuraUnits = onlySakuraUnits
            });


            if (customBattle)
            {
                seed = UnityEngine.Random.Range(0, int.MaxValue);
#if !UNITY_EDITOR
                Debug.Log($"Custom battle: {seed}");
#endif
            }

            bool isRiverCrossing = false;
            if (!customBattle)
            {
                if (!BattleManager.Instance.BattleSaveManager.IsGarrisonBattle)
                    await battlefieldBonusManager.SetUp(seed, positionDrawer.BattleZone);

                greyCompanyBattlefield.GenerateBattlefieldFromCampaign();
            }
            else
            {
                MapRegion _mapRegion = MapThemeManager.Instance.GetMapRegion(0);
                Biome _biome = _mapRegion.possibleBiomes[0].biome;
                isRiverCrossing = _mapRegion.possibleBiomes[0].biome == Biome.River;
#if UNITY_EDITOR
                await battlefieldBonusManager.SetUp(seed, positionDrawer.BattleZone, battlefieldBonusManager.CustomBattleCount);
#endif
                greyCompanyBattlefield.GenerateBattlefieldFromCustomBattle(true, seed, _mapRegion, _biome);
            }

            battleManager.EntityWatcher.SetUp();
            battleManager.EnemyGeneral.SetUp();            
            battleManager.EnemyGeneral.SetRiverCrossing(isRiverCrossing);      
#if SPELLS
            battleManager.SpellManager.LoadSpellManager(); 
#endif     
        }
        public async void NotifyOfBattlefieldGenerationCompletion(MapRegion mapRegion, Biome biome, bool regeneratingBattlefield)
        {
            // Debug.Log($"{(regeneratingBattlefield ? "Regenerating" : "Initial")} generation complete for region: {mapRegion.RegionName}");
            meshTextureUpdater.UpdateBattlefieldTexture(mapRegion.GrassTexture2D);
            
            minimapScreenshot.TakeScreenshot();
            RegeneratingBattlefieldIndicator.SetActive(false);

            bool isRiverCrossing = biome == Biome.River;
            battleManager.EnemyGeneral.SetRiverCrossing(isRiverCrossing);      

            if (battlefieldGeneratedAtLeastOnce) return;

            squadManager.SetUp();
            customBattleUIManager.LoadUI(seed);
            battlefieldGeneratedAtLeastOnce = true;

            greyCompanyBattlefield.SpawnGateSquads();

            if(!BattleManager.Instance.BattleSaveManager.IsCustomBattle)
            {
                await BattleManager.Instance.ArmySpawnManager.LoadBothArmies();
                // Normal campaign battles call AlertOfSceneSetUpComlete internally (after staging),
                // so only call it here for garrison battles that skip the dice roll flow.
                if (BattleManager.Instance.BattleSaveManager.IsGarrisonBattle)
                    SceneHandler.Instance.AlertOfSceneSetUpComlete();
            }
            else
            {
                if (SceneHandler.Instance.EditorLoadCustomBattleSaveData)
                    await BattleManager.Instance.ArmySpawnManager.LoadBothArmies();

                SceneHandler.Instance.AlertOfSceneSetUpComlete();
            }
        }
        public async Task LoadSubscene()
        {
            if (subscene != null)
            {
                if (simulationStarted)
                {
                    Debug.LogWarning("LoadSubscene called while subscene is already loaded. Skipping.");
                    return;
                }
                sceneEntity = Entity.Null;
                sceneEntity = SceneSystem.LoadSceneAsync(World.DefaultGameObjectInjectionWorld.Unmanaged, subscene.SceneGUID);
                // BakingSystem bakingSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<BakingSystem>();

                while (SceneSystem.SceneStreamingState.Loading ==
                    SceneSystem.GetSceneStreamingState(World.DefaultGameObjectInjectionWorld.Unmanaged, sceneEntity))
                {
                    // Debug.Log("Subscene  loading...");
                    await Task.Yield();
                }

                // Debug.Log("Subscene done loading...");
                simulationStarted = true;
                Time.timeScale = 1;
            }
            else
            {
                Debug.LogError("No subscene assigned.");
            }
        }

        public void LeaveBattleLoadMap()
        {
            UnloadSubscene();
            SceneHandler.Instance.SwitchGameState(GameStateEnum.Map);
            // await Task.Delay(1000);
        }
        public void LeaveBattleLoadMainMenu()
        {
            SceneHandler.Instance.RequestSceneCleanUpFunction(GameStateEnum.MainMenu);
        }
        public void OnRequestSceneCleanUp()
        {
            if (SceneHandler.Instance.CurrentGameState == GameStateEnum.Map) return;
            // squadMovementManager.CleanUp();
            // uIManager.CleanUp();
            UnloadSubscene();
            SceneHandler.Instance.SceneCleanUpComplete();
        }
        public void UnloadSubscene()
        {
            battleManager.EntityWatcher.TearDown();
            battleManager.EnemyGeneral.TearDown();

            if (sceneEntity == Entity.Null)
            {
                Debug.LogError("No subscene loaded.");
                return;
            }

            if (!simulationStarted) return;

            try
            {
                squadManager.CleanUpScene();
                TutorialManager.Instance.TurnOff();
                squadMovementManager.CleanUp();
                uIManager.CleanUp();
                battlefieldBonusManager.CleanUp();
                BattleManager.Instance.GroupManager.CleanUp();
                Cursor.SetCursor(null, Vector2.zero, UnityEngine.CursorMode.Auto);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in squadManager.CleanUpScene(): {e}");
            }

            try
            {
                World world = World.DefaultGameObjectInjectionWorld;
                world.EntityManager.CompleteAllTrackedJobs();

                // Unload subscene BEFORE any world disposal
                SceneSystem.UnloadScene(world.Unmanaged, subscene.SceneGUID, SceneSystem.UnloadParameters.DestroyMetaEntities);

                world.Dispose();

                world = new World("DefaultWorld", WorldFlags.Game);
                World.DefaultGameObjectInjectionWorld = world;

                IReadOnlyList<Type> systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
                ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error while unloading scene: {e}");
            }

            simulationStarted = false;
            battlefieldGeneratedAtLeastOnce = false;
            AddressablesManager.Instance.ReleaseAll();
            Resources.UnloadUnusedAssets();
        }
        public void OnDestroy()
        {
            if(!SceneHandler.HasInstance) return;
            SceneHandler.Instance.OnRequestSceneCleanUp -= OnRequestSceneCleanUp;
            if (simulationStarted)
                UnloadSubscene();
        }
    }
}