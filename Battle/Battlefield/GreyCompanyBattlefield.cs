using UnityEngine;
using TJ.IrregularGrid;
using Memori.SaveData;
using System.Collections.Generic;
using System;
using Memori.Notifications;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TJ
{
    [Serializable] public struct BattleFieldPreset {
        [Serializable] public enum TimeOfDay { Noon, Evening }
        public MapRegion mapRegion;
        public Race race;
        public Biome biome;
        public TimeOfDay timeOfDay;
        public Weather weather;
        public int seed;
        public bool useRandomSeed;
    }
    public class GreyCompanyBattlefield : MonoBehaviour
    {
        [Header("Active Battlefield Parameters")]
        [SerializeField] private BattlefieldParameters battlefieldParameters;
        [SerializeField] private BattleFieldPreset battleFieldPreset;

        [Header("Preset Variables")]
        [SerializeField] private IslandGenerator islandGenerator;
        [SerializeField] private PositionDrawer positionDrawer;
        [SerializeField] private BattlefieldEnvManager battlefieldEnvManager;
        [SerializeField] private BattlefieldParameters OpenFieldSO, ForestSO, RiverCrossingSO, SwampSO, PlainsSO;  //VillageSO, RuinsSO, MountainSO;

        [Header("Garrison")]
        [SerializeField] private GarrisonWallsGenerator garrisonWallsGenerator;
        [SerializeField] private GarrisonWallsSO villageWallsSO;
        [SerializeField] private GarrisonWallsSO castleWallsSO;
        [SerializeField] private GarrisonWallsSO cityWallsSO;
        public void SpawnGateSquads() => garrisonWallsGenerator.SpawnPendingGateSquads();
        private MapRegion _mapRegion;
        bool regeneratingBattlefield = false;
        private BattlefieldGenerationParameters generationParameters;
        private readonly List<string> riverCrossingSeeds = new () {
            "257530", "946751", "289029"
        };
        public void GenerateBattlefieldFromCampaign()
        {
            battleFieldPreset.useRandomSeed = false;
            battleFieldPreset = SaveDataHandler.Load().battleFieldPreset;
            _mapRegion = TabletopTavernData.Instance.GetRaceData(battleFieldPreset.race).MapRegion;
            if (BattleManager.Instance.BattleSaveManager.IsGarrisonBattle)
                battleFieldPreset.biome = Biome.Plains;
            Debug.Log($"Campaign loaded battlefield preset: {battleFieldPreset.biome}");
            GenerateBattlefield();
        }
        public int GenerateBattlefieldFromCustomBattle(bool useRandomSeed, int seed, MapRegion mapRegion, Biome biome, bool _regeneratingBattlefield = false)
        {
            _mapRegion = mapRegion;
            battleFieldPreset.useRandomSeed = useRandomSeed;
            battleFieldPreset.seed = seed;
            battleFieldPreset.biome = biome;
            regeneratingBattlefield = _regeneratingBattlefield;
            
            // Debug.Log($"Custom Battle loaded mapRegion: {_mapRegion.RegionName} preset: {battleFieldPreset.biome}");
            GenerateBattlefield();
            return battleFieldPreset.seed;
        }
        public void GenerateBattlefield()
        {
            void HandleSeed()
            {
                if (battleFieldPreset.useRandomSeed)
                {
                    generationParameters.seed = UnityEngine.Random.Range(0, 1000000);
                    battleFieldPreset.seed = generationParameters.seed;
                }
                else
                {
                    Debug.Log($"Using provided seed: {battleFieldPreset.seed}");
                    generationParameters.seed = battleFieldPreset.seed;
                }
            }

            void HandleBiomeSpecifics()
            {
                switch (battleFieldPreset.biome)
                {
                    case Biome.Plains:
                        // Handle OpenField specifics
                        battleFieldPreset.useRandomSeed = true;
                        break;
                    case Biome.Forest:
                        // Handle Forest specifics
                        battleFieldPreset.useRandomSeed = true;
                        break;
                    case Biome.River:
                        battleFieldPreset.seed = int.Parse(riverCrossingSeeds[UnityEngine.Random.Range(0, riverCrossingSeeds.Count)]);
                        battleFieldPreset.useRandomSeed = false;
                        break;
                    case Biome.Swamp:
                        // Handle Swamp specifics
                        battleFieldPreset.useRandomSeed = true;
                        break;
                    default:
                        Debug.LogError($"Biome {battleFieldPreset.biome} not recognized, defaulting to OpenField.");
                        battleFieldPreset.useRandomSeed = true;
                        break;
                }
            }

            if (Application.isPlaying)
            {
                // Walls from the last generation would carve the new NavMesh and their gate squads would outlive them.
                garrisonWallsGenerator.ClearWalls();
                BattleManager.Instance.ArmySpawnManager.ClearGates();
            }

            LoadBattlefieldParameters();
            HandleBiomeSpecifics();
            HandleSeed();

            // Debug.Log($"Generating battlefield with seed: {generationParameters.seed} and biome: {battleFieldPreset.biome}");

            islandGenerator.OnIslandGenerated += OnIslandGenerated;
            IslandGenerator.BiomeAssets biomeAssets = new ()
            {
                GrassMaterial = _mapRegion.grassMaterial,
                TreeObjects = _mapRegion.TreeObjectsAddressable,
                ScatterGenericObjects = _mapRegion.ScatterObjectsAddressable,
                RiverMaterial = _mapRegion.RiverMaterial,
                ForestGrassMaterial = _mapRegion.ForestMaterial,
                ForestTreeObjects = _mapRegion.ForestTreeObjectsAddressable,
                RiverObjects = _mapRegion.RiverObjectsAddressable
            };
            islandGenerator.CreateIsland(generationParameters, biomeAssets);

            battlefieldEnvManager.LoadTimeOfDay(battleFieldPreset.timeOfDay);
        }

        public void LoadBattlefieldParameters()
        {
            // Load battlefield parameters
            switch (battleFieldPreset.biome)
            {
                case Biome.Plains:
                    battlefieldParameters = OpenFieldSO;
                    break;
                case Biome.Forest:
                    battlefieldParameters = ForestSO;
                    break;
                case Biome.River:
                    battlefieldParameters = RiverCrossingSO;
                    break;
                case Biome.Swamp:
                    battlefieldParameters = SwampSO;
                    break;
                default:
                    Debug.LogError($"Biome {battleFieldPreset.biome} not recognized, defaulting to OpenField.");
                    battlefieldParameters = OpenFieldSO;
                    break;
            }
            generationParameters = battlefieldParameters.battlefieldParameters;
        }
#if UNITY_EDITOR
        public void SaveBattlefieldParameters()
        {
            battlefieldParameters.battlefieldParameters = generationParameters;
            EditorUtility.SetDirty(battlefieldParameters);
            AssetDatabase.SaveAssets();
        }
#endif
        public async void OnIslandGenerated(bool success)
        {
            // Debug.Log($"Island generation callback received. Success: {success}");
            if(!success)
            {
                NotificationManager.Instance.ErrorNotification("Generation failed, try another seed.");
                islandGenerator.OnIslandGenerated -= OnIslandGenerated;
                return;
            }
            islandGenerator.OnIslandGenerated -= OnIslandGenerated;
#if !UNITY_EDITOR
            Debug.Log($"Island Generated");
#endif
            bool isGarrison = Application.isPlaying && BattleManager.Instance.BattleSaveManager.IsGarrisonBattle;

            await islandGenerator.CreateBiomes();
            if (isGarrison)
            {
                TownSize townSize = BattleManager.Instance.BattleSaveManager.GarrisonTownSize;
                GarrisonWallsSO wallsSO = townSize switch
                {
                    TownSize.Village => villageWallsSO,
                    TownSize.Castle  => castleWallsSO,
                    TownSize.City    => cityWallsSO,
                    _                => villageWallsSO
                };
                garrisonWallsGenerator.PlaceGarrisonWalls(wallsSO);
                islandGenerator.SetScatterExclusionBounds(garrisonWallsGenerator.GetWallColliderBounds());
            }
#if !UNITY_EDITOR
            await islandGenerator.SpawnScatterTerrain();
#endif
            if (!isGarrison)
                islandGenerator.AddNavmeshObstacles();
            islandGenerator.CreateNavMesh();
            islandGenerator.CleanUpData();

            positionDrawer.DrawZones(isGarrison, isGarrison ? garrisonWallsGenerator.ConcaveZone : default);
            positionDrawer.DrawBiomes();
            
            if(Application.isPlaying)
            {
                BattleManager.Instance.BattleCleanUpManager.NotifyOfBattlefieldGenerationCompletion(_mapRegion, battleFieldPreset.biome, regeneratingBattlefield);
            }
        }
    }
}
// 984977
// 553364
// 520057
// 854966
// 638732
// 692187
// 79333
// 363731
// 57981
// 61859
// 56164
// 627760
// 449630
// 419983
// 44874