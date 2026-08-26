using Memori.Utilities;
using UnityEngine;
using System.Collections.Generic;
using Memori.SaveData;
using Unity.Entities;
using Unity.Collections;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Threading.Tasks;
using Memori.Metaprogression;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TJ
{
    public class TabletopTavernData : Singleton<TabletopTavernData>
    {
        public Dictionary<UnitName, SquadStats> SquadStatsDictionary = new();
        public Dictionary<UnitName, SquadAssets> SquadAssetsDictionary = new();
        public Dictionary<Race, List<UnitName>> UnitsOfRaceDictionary = new();
        public Dictionary<Race, RaceData> RaceDataDictionary = new();
        private static BlobAssetReference<SquadStatsBlob> _cachedBlobRef;
        private static Entity _singletonEntity = Entity.Null;

        
        [Header("Hero Assets")]
        [SerializeField] private HeroAssetsData _heroAssetsData;

        [Header("Metaprogression")]
        [SerializeField] private MetaprogressionModel _signatureUnitDropChanceMetaprogressionModel, _otherSignatureUnitDropMetaprogressionModel;

        [SerializeField] string googleSheetUrl = "";
        protected override void Awake()
        {
            base.Awake();
            InitializeSquadStats();
        }
        private void InitializeSquadStats()
        {
            LoadStatsFromSOs();
            ApplyModOverrides();
        }
        private void ApplyModOverrides()
        {
            List<string> modFolders = ModLoadOrder.GetEnabledModFolderPathsInOrder();
            ModLoadOrder.SetLoadedSnapshot(modFolders);
            GearData.ClearModifierOverrides();
            ArmyGenerationRuleData.ClearModRules();
            EconomyOverrideLoader.ClearOverrides();
            RaceBonusRuleData.ClearOverrides();
            Memori.Localization.LocalizationOverrides.Clear();
            foreach (string modFolder in modFolders)
            {
                SquadStatsOverrideLoader.ApplyOverridesFromModFolder(modFolder, SquadStatsDictionary, SquadAssetsDictionary, UnitsOfRaceDictionary);
                RaceDataOverrideLoader.ApplyOverridesFromModFolder(modFolder, RaceDataDictionary);
                GearOverrideLoader.ApplyOverridesFromModFolder(modFolder);
                ArmyGenerationRuleOverrideLoader.ApplyOverridesFromModFolder(modFolder);
                EconomyOverrideLoader.ApplyOverridesFromModFolder(modFolder);
                RaceBonusOverrideLoader.ApplyOverridesFromModFolder(modFolder);
                LocalizationOverrideLoader.ApplyOverridesFromModFolder(modFolder);
            }
            HeroData.LoadFromResourcesAndOverrides(modFolders);
            HeroBonusManager.LoadRulesFromResourcesAndOverrides(modFolders);
        }
        private void LoadStatsFromSOs()
        {
            SquadStatsDictionary.Clear();
            SquadAssetsDictionary.Clear();
            RaceDataDictionary.Clear();
            UnitsOfRaceDictionary.Clear();
            SquadData[] allSOs = Resources.LoadAll<SquadData>("SquadData");

            for (int i = 0; i < allSOs.Length; i++)
            {
                SquadStatsDictionary[allSOs[i].stats.unitName] = allSOs[i].stats;
                SquadAssetsDictionary[allSOs[i].stats.unitName] = allSOs[i].assets;

#if !SPELLS
                // Mages belong to the spell system and are not shipped yet, so withhold them from the
                // race roster: recruit lists, the collection panel, race collection achievements and
                // enemy army generation all read UnitsOfRaceDictionary, and dropping out of it is what
                // makes a unit invisible.
                //
                // They deliberately STAY in the stat and asset dictionaries. The stats blob is indexed
                // by UnitName ordinal (LoadAndInjectData sizes it by SquadStatsDictionary.Count and
                // GetStats reads Stats[(int)unitName]), so removing an entry there shifts Count and can
                // leave a live unit reading past the end of the blob.
                if (allSOs[i].stats.unitType == UnitType.Mage) continue;
#endif

                // Populate UnitsOfRaceDictionary
                if (!UnitsOfRaceDictionary.ContainsKey(allSOs[i].assets.race))
                {
                    UnitsOfRaceDictionary[allSOs[i].assets.race] = new List<UnitName>();
                }
                UnitsOfRaceDictionary[allSOs[i].assets.race].Add(allSOs[i].stats.unitName);
            }
            //Sorts units of UnitsOfRaceDictionary by RarityTier ascending
            foreach (var kvp in UnitsOfRaceDictionary)
            {
                kvp.Value.Sort((a, b) => GetUnitTierFromUnitName(a).CompareTo(GetUnitTierFromUnitName(b)));
            }
            RaceData[] allRaceData = Resources.LoadAll<RaceData>("RaceData");
            for (int i = 0; i < allRaceData.Length; i++)
            {
                if (!RaceDataDictionary.ContainsKey(allRaceData[i].Race))
                {
                    RaceDataDictionary[allRaceData[i].Race] = allRaceData[i];
                }
            }
#if UNITY_EDITOR
            CheckForAnyMissingData();
#endif
        }
        #region BlobAsset Injection
        public void LoadAndInjectData()
        {
            // Load your static file (example: assume JSON deserialization to SquadStats[])
            // SquadStats[] allStats = LoadStatsFromFile();  // Your loading logic here

            // --- 1. Dispose previous blob (if any) ---
            if (_cachedBlobRef.IsCreated)
            {
                _cachedBlobRef.Dispose();
                _cachedBlobRef = default;
            }

            // Build BlobAsset
            using var builder = new BlobBuilder(Allocator.Temp);
            ref SquadStatsBlob blob = ref builder.ConstructRoot<SquadStatsBlob>();

            var array = builder.Allocate(ref blob.Stats, SquadStatsDictionary.Count);
            for (int i = 0; i < SquadStatsDictionary.Count; i++)
            {
                if (SquadStatsDictionary.TryGetValue((UnitName)i, out SquadStats stats))
                {
                    array[i] = stats;
                    continue;
                }
                Debug.LogError($"TabletopTavernData: Missing SquadStats for UnitName {(UnitName)i}");
            }

            _cachedBlobRef = builder.CreateBlobAssetReference<SquadStatsBlob>(Allocator.Persistent);

            // Get ECS world and EntityManager
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            if (_singletonEntity != Entity.Null && entityManager.Exists(_singletonEntity))
            {
                entityManager.DestroyEntity(_singletonEntity);
            }

            // Create singleton entity (or reuse if exists)
            _singletonEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(_singletonEntity, new SquadStatsData { StatsBlob = _cachedBlobRef });

            // Optional: Name for debugging
            entityManager.SetName(_singletonEntity, new FixedString64Bytes("SquadStatsSingleton"));
        }
        private void OnDestroy() 
        {
            if (_cachedBlobRef.IsCreated)
            {
                _cachedBlobRef.Dispose();
                _cachedBlobRef = default;
            }
            if (_singletonEntity != Entity.Null)
            {
                if(World.DefaultGameObjectInjectionWorld == null) return;
                var world = World.DefaultGameObjectInjectionWorld;
                var entityManager = world.EntityManager;
                if (entityManager.Exists(_singletonEntity))
                {
                    entityManager.DestroyEntity(_singletonEntity);
                }
                _singletonEntity = Entity.Null;
            }
        }
        #endregion

        public SquadStats GetSquadStats(UnitName unitName)
        {
            return SquadStatsDictionary[unitName];
        }
        public UnitType GetUnitTypeFromUnitName(UnitName _unitName)
        {
            SquadStats squadStats = GetSquadStats(_unitName);
            return squadStats.unitType;
        }
        public bool IsMeleeInfantry(UnitName _unitName)
        {
            SquadStats squadStats = GetSquadStats(_unitName);
            return squadStats.unitType == UnitType.Melee && squadStats.unitSize == UnitSize.Infantry;
        }
        public int GetUnitTierFromUnitName(UnitName _unitName)
        {
            SquadStats squadStats = GetSquadStats(_unitName);
            return (int)squadStats.RarityTier + 1;
        }
        public Race GetRaceFromUnitName(UnitName _unitName)
        {
            SquadAssets squadAssets = SquadAssetsDictionary[_unitName];
            return squadAssets.race;
        }
        public int GetBaseUnitCount(UnitName _unitName)
        {
            SquadStats squadStats = GetSquadStats(_unitName);
            return squadStats.baseUnitCount;
        }
        public int GetHitPointsPerUnit(UnitName _unitName)
        {
            SquadStats squadStats = GetSquadStats(_unitName);
            return squadStats.HitPointsPerUnit;
        }
        public UnitSize GetUnitSizeFromUnitName(UnitName _unitName)
        {
            SquadStats squadStats = GetSquadStats(_unitName);
            return squadStats.unitSize;
        }
        public int GetMaxUnitCountFromUnitName(UnitName _unitName)
        {
            SquadStats squadStats = GetSquadStats(_unitName);
            return squadStats.baseUnitCount;
        }
        public float GetUnitSpreadFromUnitName(UnitName _unitName)
        {
            return GetUnitSizeFromUnitName(_unitName) switch
            {
                UnitSize.Infantry => TabletopTavernConstants.InfantrySpread,
                UnitSize.Cavalry => TabletopTavernConstants.CavalrySpread,
                UnitSize.Monstrous => TabletopTavernConstants.MonsterSpread,
                UnitSize.Artillery => TabletopTavernConstants.ArtillerySpread,
                UnitSize.SingleUnit => TabletopTavernConstants.SingleUnitSpread,
                _ => 0,
            };
        }
        public string GetSquadCurrentUnitCount(SquadToLoad squadToLoad)
        {
            SquadStats squadStats = GetSquadStats(squadToLoad.UnitName);
            int currentUnitCount = (int)(squadToLoad.SquadCurrentHealth / squadStats.HitPointsPerUnit);
            // If squadCurrentHealth is above 0, currentUnitCount is at least 1
            if (squadToLoad.SquadCurrentHealth > 0 && currentUnitCount == 0) currentUnitCount = 1;
            // Guard against drift between the squad's saved HitPointsPerUnit and the live squadStats value (e.g. after a balance change)
            currentUnitCount = Mathf.Min(currentUnitCount, squadToLoad.maxUnitCount);
            return currentUnitCount.ToString();
        }
        public int GetUnitCost(UnitName _unitName)
        {
            int unitTier = GetUnitTierFromUnitName(_unitName);
            return TabletopTavernConstants.GetUnitCost(unitTier);
        }
        public float GetSquadNoise(UnitName _unitName)
        {
            return SquadAssetsDictionary[_unitName].formationDiscipline.UnitFormationNoise;
        }
        public Vector3 GetNoiseFromUnitName(UnitName _unitName, Vector3 pos)
        {
            var noiseMultiplier = SquadAssetsDictionary[_unitName].formationDiscipline.UnitFormationNoise;
            var noise = Mathf.PerlinNoise(pos.x, pos.z);
            noise *= noiseMultiplier * 10;
            return new Vector3(noise + pos.x, 0, noise + pos.z);
        }
        public string GetRecruitmentPrefabKey(UnitName _unitName)
        {
            return SquadAssetsDictionary[_unitName].unitRecruitmentPrefab.AssetGUID;
        }
        public HeroAssetEntry GetHeroAssetEntry(int heroID) => _heroAssetsData.GetByID(heroID);
        public async Task<Sprite> LoadHeroSpriteAsync(int heroID)
        {
            var entry = _heroAssetsData.GetByID(heroID);
            if (entry.Sprite.OperationHandle.IsValid())
            {
                if (!entry.Sprite.IsDone)
                    await entry.Sprite.OperationHandle.Task;
                return entry.Sprite.Asset as Sprite;
            }
            var handle = entry.Sprite.LoadAssetAsync<Sprite>();
            await handle.Task;
            if (handle.Status == AsyncOperationStatus.Failed)
            {
                Debug.LogError($"[TabletopTavernData] Failed to load sprite for hero {heroID}");
                return null;
            }
            return handle.Result;
        }
        public async Task<GameObject> LoadHeroPrefabAsync(int heroID, bool persistent = false)
        {
            HeroAssetEntry entry = _heroAssetsData.GetByID(heroID);
            if (entry == null)
            {
                Debug.LogError($"[TabletopTavernData] No hero asset entry for hero {heroID}");
                return null;
            }
            return await AddressablesManager.Instance.LoadAsync<GameObject>(entry.Prefab, persistent);
        }

        /// <summary>Addressable key for a hero prefab, so a persistent owner can release it explicitly.</summary>
        public string GetHeroPrefabKey(int heroID)
        {
            HeroAssetEntry entry = _heroAssetsData.GetByID(heroID);
            return entry != null ? entry.Prefab.AssetGUID : null;
        }
        public async Task<GameObject> LoadRecruitmentPrefabAsync(UnitName _unitName)
        {
            return await AddressablesManager.Instance.LoadAsync<GameObject>(SquadAssetsDictionary[_unitName].unitRecruitmentPrefab);
        }
        public async Task<GameObject> LoadArtilleryCrewPrefabAsync(UnitName _unitName)
        {
            return await AddressablesManager.Instance.LoadAsync<GameObject>(SquadAssetsDictionary[_unitName].ArtilleryCrewPrefab);
        }
        public Sprite GetUnitIcon(UnitName _unitName)
        {
            return SquadAssetsDictionary[_unitName].unitIcon;
        }
        public Sprite GetSquadTypeIcon(UnitName _unitName)
        {
            return SquadAssetsDictionary[_unitName].squadIcon.iconSprite;
        }
        public Sprite GetSquadTypeFlagSprite(UnitName _unitName)
        {
            return SquadAssetsDictionary[_unitName].squadIcon.FlagSprite;
        }
        public UnitName[] GetUnitsOfRace(Race _race)
        {
            if (UnitsOfRaceDictionary.TryGetValue(_race, out var unitNames))
            {
                return unitNames.ToArray();
            }
            return Array.Empty<UnitName>();
        }
        public List<UnitTier> GetSquadsWithTiersFromRace(Race _race)
        {
            List<UnitTier> allUnits = new();
            UnitName[] unitsOfRace = GetUnitsOfRace(_race);
            foreach (UnitName unitName in unitsOfRace)
            {
                allUnits.Add(new UnitTier { unitName = unitName, tier = (int)SquadStatsDictionary[unitName].RarityTier + 1 });
            }
            return allUnits;
        }
        public AudioClip GetRandomBarkSFX(UnitName _unitName)
        {
            int randomIndex = UnityEngine.Random.Range(0, SquadAssetsDictionary[_unitName].voiceSFX.barkSFX.Length);
            return SquadAssetsDictionary[_unitName].voiceSFX.barkSFX[randomIndex];
        }
        public AudioClip GetRandomChargeSFX(UnitName _unitName)
        {
            int randomIndex = UnityEngine.Random.Range(0, SquadAssetsDictionary[_unitName].voiceSFX.chargeSFX.Length);
            return SquadAssetsDictionary[_unitName].voiceSFX.chargeSFX[randomIndex];
        }
        public AudioClip GetRandomRetreatSFX(UnitName _unitName)
        {
            int randomIndex = UnityEngine.Random.Range(0, SquadAssetsDictionary[_unitName].voiceSFX.retreatSFX.Length);
            return SquadAssetsDictionary[_unitName].voiceSFX.retreatSFX[randomIndex];
        }
        public AssetReferenceGameObject GetArtilleryCrewPrefab(UnitName _unitName)
        {
            return SquadAssetsDictionary[_unitName].ArtilleryCrewPrefab;
        }
        public AudioClip GetBattlefieldAudio(UnitName _unitName, Memori.Audio.SFXEntityType _sfxEntityType)
        {
            SquadAssets squadAssets = SquadAssetsDictionary[_unitName];
            switch (_sfxEntityType)
            {
                case Memori.Audio.SFXEntityType.Idle:
                    int idleIndex = UnityEngine.Random.Range(0, squadAssets.voiceSFX.idleSFX.Length);
                    return squadAssets.voiceSFX.idleSFX[idleIndex];
                case Memori.Audio.SFXEntityType.FireProjectile:
                    int fireIndex = UnityEngine.Random.Range(0, squadAssets.fireProjectileSFX.fireProjectileSFX.Length);
                    return squadAssets.fireProjectileSFX.fireProjectileSFX[fireIndex];
                case Memori.Audio.SFXEntityType.MeleeAttack:
                    int meleeIndex = UnityEngine.Random.Range(0, squadAssets.meleeAttackSFX.meleeAttackSFX.Length);
                    return squadAssets.meleeAttackSFX.meleeAttackSFX[meleeIndex];
                case Memori.Audio.SFXEntityType.Death:
                    int deathIndex = UnityEngine.Random.Range(0, squadAssets.voiceSFX.deathSFX.Length);
                    return squadAssets.voiceSFX.deathSFX[deathIndex];
                default:
                    Debug.LogError($"GetBattlefieldAudio: Unknown SFXEntityType {_sfxEntityType} for Unit {_unitName}");
                    return null;
            }
        }
        public UnitName[] GetSquadsToRecruitBasedOnReputation(int _reputation, int _count, int _seed, int _hero)
        {
            UnityEngine.Random.InitState(_seed);
            List<UnitTier> allUnits = GetSquadsWithTiersFromRace(HeroData.GetRaceFromHero(_hero));
            float[] probabilities = DataTypes.GetTierProbabilities(_reputation);
            List<UnitTier> recruitmentPool = new();

            // Fill recruitment pool based on tier probabilities
            foreach (var unit in allUnits)
            {
                float chance = 0f;
                if (unit.tier == 1) chance = probabilities[0];
                else if (unit.tier == 2) chance = probabilities[1] * (_hero == 1 ? 2 : 1);
                else if (unit.tier == 3) chance = probabilities[2];
                else if (unit.tier == 4) chance = probabilities[3];

                // Add unit to the pool multiple times for weighting
                int weight = Mathf.RoundToInt(chance * 100);
                for (int i = 0; i < weight; i++)
                {
                    recruitmentPool.Add(unit);
                }
            }

            // Randomly select 3 unique units
            List<UnitTier> recruitmentOptions = new();
            while (recruitmentOptions.Count < _count && recruitmentPool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, recruitmentPool.Count);
                UnitTier selectedUnit = recruitmentPool[index];

                if (!recruitmentOptions.Contains(selectedUnit))
                {
                    recruitmentOptions.Add(selectedUnit);
                }

                recruitmentPool.RemoveAll(u => u == selectedUnit);
            }

            UnitName[] recruitableNames = new UnitName[_count];
            for (int i = 0; i < recruitmentOptions.Count; i++)
            {
                recruitableNames[i] = recruitmentOptions[i].unitName;
            }

            return recruitableNames;
        }
        public RaceData GetRaceData(Race _race)
        {
            if (RaceDataDictionary.TryGetValue(_race, out RaceData raceDataList))
            {
                return raceDataList; // Return the first RaceData found for the race
            }
            // Debug.LogError($"GetRaceData: No RaceData found for Race {_race}");
            return null;
        }
        public UnitName[] GetSquadsToRecruitForPack(int _reputation, int _count, int _seed, int _hero, int _cardPackID)
        {
            UnityEngine.Random.InitState(_seed);

            List<UnitTier> allUnits = GetSquadsWithTiersFromRace(HeroData.GetRaceFromHero(_hero));
            // float[] probabilities = DataTypes.GetTierProbabilities(_reputation);
            List<UnitTier> recruitmentPool = new ();


            // Fill recruitment pool based on tier probabilities
            foreach (var unit in allUnits)
            {
                if (unit.tier == 1 && _cardPackID==1) recruitmentPool.Add(unit);
                else if (unit.tier == 2 && _cardPackID==2) recruitmentPool.Add(unit);
                else if (unit.tier == 3 && _cardPackID==3) recruitmentPool.Add(unit);
                else if (unit.tier == 4 && _cardPackID==4) recruitmentPool.Add(unit);
            }

            // Randomly select 3 unique units
            List<UnitTier> recruitmentOptions = new ();
            while (recruitmentOptions.Count < _count && recruitmentPool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, recruitmentPool.Count);
                UnitTier selectedUnit = recruitmentPool[index];

                if (!recruitmentOptions.Contains(selectedUnit))
                {
                    recruitmentOptions.Add(selectedUnit);
                }

                recruitmentPool.RemoveAll(u => u == selectedUnit);
            }
            //get min of either _count or recruitmentOptions.Count
            int minCount = Mathf.Min(_count, recruitmentOptions.Count);
            UnitName[] recruitableNames = new UnitName[minCount];
            for (int i = 0; i < recruitmentOptions.Count; i++)
            {
                recruitableNames[i] = recruitmentOptions[i].unitName;
            }

            if (_cardPackID==4)
            {
                Hero hero = HeroData.GetHeroByID(_hero);
                if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_otherSignatureUnitDropMetaprogressionModel)) 
                {
                    Debug.Log($"Other Signature Unit Drop Metaprogression Unlocked, getting random signature");
                    List<UnitName> heroesOfRace = HeroData.GetSignatureUnitsByRace(hero.Race);
                    recruitableNames = new UnitName[2];
                    recruitableNames[0] = heroesOfRace[0];
                    recruitableNames[1] = heroesOfRace[1];
                } 
                else 
                {
                    recruitableNames = new UnitName[1];
                    recruitableNames[0] = hero.SignatureUnit;
                }
            }

#if APOLLO
            recruitableNames[UnityEngine.Random.Range(0, recruitableNames.Length)] = UnitName.ArchersOfApollo;
#endif

            return recruitableNames;
        }

        public UnitName[] GetRandomSquadsFromRecruitmentPool(List<UnitTier> recruitmentPool, int _count, int _seed) 
        {
            UnityEngine.Random.InitState(_seed);
            // Randomly select 3 unique units
            List<UnitTier> recruitmentOptions = new ();
            while (recruitmentOptions.Count < _count && recruitmentPool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, recruitmentPool.Count);
                UnitTier selectedUnit = recruitmentPool[index];

                if (!recruitmentOptions.Contains(selectedUnit))
                    recruitmentOptions.Add(selectedUnit);

                recruitmentPool.RemoveAll(u => u == selectedUnit);
            }
            //if recruitmentOptions.Count is less than _count, fill the rest of recruitableNames with % or recruitmentOptions
            UnitName[] recruitableNames = new UnitName[_count];
            for (int i = 0; i < _count; i++) {
                recruitableNames[i] = recruitmentOptions[i % recruitmentOptions.Count].unitName;
            }
            return recruitableNames;
        }
        public UnitName[] GetSquadsToRecruitForRaceTown(Race _race, int _count, int _seed, TownSize _townsize)
        {
            List<UnitTier> recruitmentPool = GetSquadsWithTiersFromRace(_race);

            if(_townsize == TownSize.Village) recruitmentPool.RemoveAll(u => u.tier >= 2);
            else if(_townsize == TownSize.Castle) recruitmentPool.RemoveAll(u => u.tier >= 3);
            else if(_townsize == TownSize.City) recruitmentPool.RemoveAll(u => u.tier >= 4);

            return GetRandomSquadsFromRecruitmentPool(recruitmentPool, _count, _seed);
        }

        public const float COMMON_REMOVAL_CHANCE_FOR_UNCOMMON_PACKS = 0.95f;
        public UnitName[] GetSquadsToRecruitForRaceBattle(Race _race, int _count, int _seed, UnitRarity _unitRarity)
        {
            UnityEngine.Random.InitState(_seed);
            List<UnitTier> recruitmentPool = GetSquadsWithTiersFromRace(_race);

            if(_unitRarity == UnitRarity.Common)
            {
                //75% chance to remove tier 2, 95% chance to remove tier 3 and 100% chance to remove tier  4
                recruitmentPool.RemoveAll(u => u.tier >= 3);
                recruitmentPool.RemoveAll(u => u.tier == 2 && 
                    UnityEngine.Random.value < COMMON_REMOVAL_CHANCE_FOR_UNCOMMON_PACKS);
                Debug.Log($"unitpool count after common filtering: {recruitmentPool.Count}");
            }
            else if(_unitRarity == UnitRarity.Uncommon) 
            {
                //100% chance to remove tier 1, 75% chance to remove tier 3 and 100% chance to remove tier  4
                recruitmentPool.RemoveAll(u => u.tier >= 4);
                recruitmentPool.RemoveAll(u => u.tier == 1);
                recruitmentPool.RemoveAll(u => u.tier == 3 && 
                    UnityEngine.Random.value < COMMON_REMOVAL_CHANCE_FOR_UNCOMMON_PACKS);
                Debug.Log($"unitpool count after uncommon filtering: {recruitmentPool.Count}");
            }
            else if(_unitRarity == UnitRarity.Rare) 
            {
                //for rare units on horde fights
                recruitmentPool.RemoveAll(u => u.tier != 3);
                Debug.Log($"unitpool count after rare filtering: {recruitmentPool.Count}");
            }

            if (recruitmentPool.Count == 0)
                recruitmentPool = GetSquadsWithTiersFromRace(_race);

            return GetRandomSquadsFromRecruitmentPool(recruitmentPool, _count, _seed);
        }

        public List<UnitAttribute> GetUnitAttributesForDisplay(UnitName _unitName)
        {
            List<UnitAttribute> unitAttributes = new List<UnitAttribute>();

            SquadStats squadStats = GetSquadStats(_unitName);

            if (squadStats.SquadAttributes.ArmorPiercing) unitAttributes.Add(UnitAttribute.ArmorPiercing);
            if (squadStats.SquadAttributes.AntiInfantry) unitAttributes.Add(UnitAttribute.AntiInfantry);
            if (squadStats.SquadAttributes.AntiLarge) unitAttributes.Add(UnitAttribute.AntiLarge);
            if (squadStats.SquadAttributes.StandardShields) unitAttributes.Add(UnitAttribute.StandardShields);
            if (squadStats.Armor >= 80) unitAttributes.Add(UnitAttribute.Armored);
            if (squadStats.unitSize != UnitSize.Infantry && squadStats.unitSize != UnitSize.Artillery && squadStats.unitType != UnitType.Structure) unitAttributes.Add(UnitAttribute.Large);
            if (squadStats.SquadAttributes.Terrifying) unitAttributes.Add(UnitAttribute.Terrifying);
            if (squadStats.SquadAttributes.Stalwart) unitAttributes.Add(UnitAttribute.Stalwart);
            if (squadStats.SquadAttributes.Outrider) unitAttributes.Add(UnitAttribute.Outrider);
            if (squadStats.SquadAttributes.SwampCreature) unitAttributes.Add(UnitAttribute.SwampCreature);
            if (squadStats.SquadAttributes.ForestDweller) unitAttributes.Add(UnitAttribute.ForestDweller);
            if (squadStats.SquadAttributes.Ethereal) unitAttributes.Add(UnitAttribute.Ethereal);
            if (squadStats.SquadAttributes.ChickenFlight) unitAttributes.Add(UnitAttribute.ChickenFlight);
            if (squadStats.SquadAttributes.BloodFrenzy) unitAttributes.Add(UnitAttribute.BloodFrenzy);
            if (squadStats.SquadAttributes.Rage) unitAttributes.Add(UnitAttribute.Rage);
            if (squadStats.SquadAttributes.Emblazing) unitAttributes.Add(UnitAttribute.Emblazing);
            if (squadStats.SquadAttributes.Unstoppable) unitAttributes.Add(UnitAttribute.Unstoppable);
            if (squadStats.SquadAttributes.HeavyShields) unitAttributes.Add(UnitAttribute.HeavyShields);
            if (squadStats.SquadAttributes.ThrowingAxes) unitAttributes.Add(UnitAttribute.ThrowingAxes);
            if (squadStats.SquadAttributes.ArmorSundering) unitAttributes.Add(UnitAttribute.ArmorSundering);
            if (squadStats.SquadAttributes.MonsterSlayer) unitAttributes.Add(UnitAttribute.MonsterSlayer);
            // if (squadStats.SquadAttributes.TowerShields) unitAttributes.Add(UnitAttribute.TowerShields);
            if( squadStats.SquadAttributes.ForgefuryTempering) unitAttributes.Add(UnitAttribute.ForgefuryTempering);
            if( squadStats.SquadAttributes.FlamingAmmo) unitAttributes.Add(UnitAttribute.FlamingAmmo);
            if (squadStats.SquadAttributes.ThickScales) unitAttributes.Add(UnitAttribute.ThickScales);
            if( squadStats.SquadAttributes.BackStabbers) unitAttributes.Add(UnitAttribute.BackStabbers);
            if( squadStats.SquadAttributes.DragonsHoard) unitAttributes.Add(UnitAttribute.DragonsHoard);
            if (squadStats.SquadAttributes.ShotDiscipline) unitAttributes.Add(UnitAttribute.ShotDiscipline);
            if (squadStats.SquadAttributes.Overdraw) unitAttributes.Add(UnitAttribute.Overdraw);
            if (squadStats.SquadAttributes.SteadyAim) unitAttributes.Add(UnitAttribute.SteadyAim);
            if (squadStats.SquadAttributes.Demolisher) unitAttributes.Add(UnitAttribute.Demolisher);
            if (squadStats.SquadAttributes.PowderReserves) unitAttributes.Add(UnitAttribute.PowderReserves);
            if (squadStats.SquadAttributes.DeepQuivers) unitAttributes.Add(UnitAttribute.DeepQuivers);
            return unitAttributes;
        }

        public List<UnitStatValue> GetUnitStatsForDisplay(UnitName _unitName)
        {
            List<UnitStatValue> unitStats = new List<UnitStatValue>();

            SquadStats squadStats = GetSquadStats(_unitName);

            unitStats.Add(new UnitStatValue(UnitStat.MeleeAttack, squadStats.MeleeAttack));
            unitStats.Add(new UnitStatValue(UnitStat.MeleeDefense, squadStats.MeleeDefense));
            unitStats.Add(new UnitStatValue(UnitStat.WeaponStrength, squadStats.WeaponStrength));
            unitStats.Add(new UnitStatValue(UnitStat.Armor, squadStats.Armor));
            unitStats.Add(new UnitStatValue(UnitStat.Speed, squadStats.Speed));
            unitStats.Add(new UnitStatValue(UnitStat.Leadership, squadStats.Leadership));

            unitStats.Add(new UnitStatValue(UnitStat.ChargeBonus, squadStats.ChargeBonus));
            if (squadStats.ChargeImactDamage > 0) unitStats.Add(new UnitStatValue(UnitStat.ChargeImpactDamage, squadStats.ChargeImactDamage));
            
            if (TabletopTavernConstants.Casts(squadStats.unitType))
            {
                // Range and charges only. A mage's spell is placed rather than aimed and fires no
                // projectile, so Accuracy and MissileStrength would render as numbers on the card
                // that nothing reads. Ammunition is the charge count.
                unitStats.Add(new UnitStatValue(UnitStat.Range, squadStats.BaseRange));
                unitStats.Add(new UnitStatValue(UnitStat.Ammunition, squadStats.Ammunition));
            }
            else if (squadStats.unitType != UnitType.Melee)
            {
                unitStats.Add(new UnitStatValue(UnitStat.Range, squadStats.BaseRange));
                unitStats.Add(new UnitStatValue(UnitStat.Accuracy, squadStats.attackAccuracy));
                unitStats.Add(new UnitStatValue(UnitStat.MissileStrength, squadStats.MissileStrength));
                unitStats.Add(new UnitStatValue(UnitStat.Ammunition, squadStats.Ammunition));
            }

            return unitStats;
        }
        public bool IgnoresSwamp(UnitName unitName)
        {
            SquadStats squadStats = GetSquadStats(unitName);
            return
                (squadStats.SquadAttributes.SwampCreature ||
                squadStats.SquadAttributes.Ethereal ||
                squadStats.SquadAttributes.ChickenFlight);
        }
        public bool IsForestDweller(UnitName unitName)
        {
            SquadStats squadStats = GetSquadStats(unitName);
            return squadStats.SquadAttributes.ForestDweller;
        }
        public Race GenerateRaceForMap(int bookNumber, int seed, Race activeRace)
        {
            List<Race> allRaces = new ();

            switch (bookNumber)
            {
                case 1:
                    allRaces.Add(Race.Gruntkin);
                    allRaces.Add(Race.SanguineCourt);
                    break;
                case 2:
                    allRaces.Add(Race.IronLegion);
                    allRaces.Add(Race.SakuraDynasty);
                    allRaces.Add(Race.RavenHost);
                    break;
                case 3:
                    allRaces.Add(Race.DrakosaurBrood);
                    allRaces.Add(Race.DeepstoneHold);
                    allRaces.Add(Race.TaelindorForest);
                    break;
                default:
                    Debug.LogError($"GenerateRaceForMap: Invalid bookNumber {bookNumber}");
                    break;
            }

            if(allRaces.Contains(activeRace)) allRaces.Remove(activeRace);

            if (allRaces.Count == 0)
            {
                Debug.LogError($"GenerateRaceForMap: no enemy races left for bookNumber {bookNumber}, returning {activeRace}");
                return activeRace;
            }

            // Seeding is scoped: this is a pure query, so it restores the global
            // UnityEngine.Random state instead of leaving it seeded for whatever draws next.
            UnityEngine.Random.State priorState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);
            int randomIndex = UnityEngine.Random.Range(0, allRaces.Count);
            UnityEngine.Random.state = priorState;
            return allRaces[randomIndex];
        }
        private Hero GetHeroForRace(Race race, int seed)
        {
            // Count-driven rather than the hardcoded 2 this used to assume: the hero list is
            // mod-extensible, so a race can have any number of heroes. Identical draw to the
            // old Range(0, 2) for the shipped two-heroes-per-race roster, so existing campaign
            // seeds keep the same enemy general.
            List<Hero> heroes = HeroData.GetHeroesByRace(race);
            if (heroes.Count == 0)
            {
                Debug.LogError($"GetHeroForRace: no heroes found for race {race}, falling back to the default hero");
                return HeroData.GetHeroByID(HeroData.DefaultHeroID);
            }

            UnityEngine.Random.State priorState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);
            int randomIndex = UnityEngine.Random.Range(0, heroes.Count);
            UnityEngine.Random.state = priorState;
            return heroes[randomIndex];
        }
        public Hero GetEnemyHeroForCampaign(int heroID, int bookNumber, int seed, bool justGetRandomHero = false)
        {
            if(justGetRandomHero)
                return HeroData.GetRandomHero();
                
            Race heroRace = HeroData.GetRaceFromHero(heroID);
            Race race = GenerateRaceForMap(bookNumber, seed, heroRace);
            return GetHeroForRace(race, seed);
        }

#if UNITY_EDITOR
        private void CheckForAnyMissingData()
        {
            foreach (var kvp in SquadStatsDictionary)
            {
                UnitName unitName = kvp.Key;
                SquadStats stats = kvp.Value;

                if (!SquadAssetsDictionary.ContainsKey(unitName))
                {
                    Debug.LogError($"Missing SquadAssets for UnitName: {unitName}");
                    continue;
                }

                SquadAssets assets = SquadAssetsDictionary[unitName];

                if (assets.unitIcon == null)
                {
                    Debug.LogError($"Missing unitIcon in SquadAssets for UnitName: {unitName}");
                }
                if (assets.squadIcon.iconSprite == null)
                {
                    Debug.LogError($"Missing squadIcon in SquadAssets for UnitName: {unitName}");
                }
                if (!assets.unitRecruitmentPrefab.RuntimeKeyIsValid())
                {
                    Debug.LogError($"Missing unitRecruitmentPrefab in SquadAssets for UnitName: {unitName}");
                }
                if (assets.formationDiscipline == null)
                {
                    Debug.LogError($"Missing formationDiscipline in SquadAssets for UnitName: {unitName}");
                }
            }
            foreach (var kvp in SquadAssetsDictionary)
            {
                UnitName unitName = kvp.Key;
                if (!SquadStatsDictionary.ContainsKey(unitName))
                {
                    Debug.LogError($"Missing SquadStats for UnitName: {unitName}");
                }
                //check to make sure each value in squad assets exists
                foreach (var field in typeof(SquadAssets).GetFields())
                {
                    var value = field.GetValue(kvp.Value);
                    if (value == null)
                    {
                        //if fire projectile and the unit does not shoot, skip
                        if (field.Name == "fireProjectileSFX" && !TabletopTavernConstants.FightsAtRange(GetUnitTypeFromUnitName(unitName)))
                        {
                            continue;
                        }
                        if (field.Name == "ArtilleryCrewPrefab" && GetUnitTypeFromUnitName(unitName) != UnitType.Artillery)
                        {
                            continue;
                        }
                        //the spell a mage casts, so every other unit type leaves it empty
                        if (field.Name == "mageSpell" && !TabletopTavernConstants.Casts(GetUnitTypeFromUnitName(unitName)))
                        {
                            continue;
                        }
                        Debug.LogError($"Missing {field.Name} in SquadAssets for UnitName: {unitName}");
                    }
                }
            }
        }
        [ContextMenu("Create Squad SOs")]
        private void CreateSquadSOs()
        {
            // Ensure the dictionary is populated (works if LoadStatsFromFile() is editor-safe)
            if (SquadStatsDictionary.Count == 0)
            {
                InitializeSquadStats();
            }

            // Create folder if it doesn't exist
            string folderPath = "Assets/SquadData";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "SquadData");
            }

            foreach (var kvp in SquadStatsDictionary)
            {
                SquadData so = ScriptableObject.CreateInstance<SquadData>();
                so.stats = kvp.Value;
                so.assets = new SquadAssets(); // Default assets; you can fill these manually later

                string assetPath = $"{folderPath}/{kvp.Key}.asset";
                AssetDatabase.CreateAsset(so, assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("SquadData ScriptableObjects created successfully.");
        }

        [ContextMenu("Upload to Google Sheets")]
        private void UploadToGoogleSheets()
        {
            if (SquadStatsDictionary.Count == 0)
            {
                InitializeSquadStats();
            }

            if (string.IsNullOrEmpty(googleSheetUrl))
            {
                Debug.LogError("Google Sheet URL is not set in the inspector.");
                return;
            }

            StartCoroutine(SendToSheets());
        }
#endif

        #region Mod Template Export
        [ContextMenu("Export Unit Stats Overrides JSON")]
        public void ExportUnitOverridesTemplate()
        {
            if (SquadStatsDictionary.Count == 0) InitializeSquadStats();

            ModLoadOrder.EnsureModsDirectoryExists();
            string templateFolder = System.IO.Path.Combine(ModLoadOrder.ModsRootPath, "_Template");
            System.IO.Directory.CreateDirectory(templateFolder);
            string path = System.IO.Path.Combine(templateFolder, SquadStatsOverrideLoader.FileName);

            System.IO.File.WriteAllText(path, SquadStatsOverrideLoader.ExportTemplate(SquadStatsDictionary, SquadAssetsDictionary));
            Debug.Log($"[ModOverride] Exported {SquadStatsDictionary.Count} units to {path}");
        }
        #endregion

      private IEnumerator SendToSheets()
    {
        // Build the data: headers + rows (use List<List<string>> for better JsonUtility compatibility)
        List<List<string>> values = new List<List<string>>();

        // Header row
        values.Add(new List<string>
        {
            "Unit Name", "Unit Type", "Unit Size", "Melee Attack", "Melee Defense", "Hit Points Per Unit",
            "Weapon Strength", "Speed", "Leadership", "Armor", "Charge Bonus", "Charge Impact Damage",
            "Charge Count", "Base Range", "Attack Accuracy", "Missile Strength", "Rarity Tier",
            "Base Unit Count", "Attack Cooldown", "Ammunition",
            "None", "Standard Shields", "Armor Piercing", "Anti Infantry", "Anti Large", "Terrifying",
            "Stalwart", "Outrider", "Swamp Creature", "Forest Dweller", "Chicken Flight", "Ethereal",
            "Blood Frenzy", "Rage", "Emblazing", "Unstoppable", "Heavy Shields", "Armor Sundering", "Monster Slayer", "Thick Scales"
        });

        // Data rows (sorted by unitName for consistency, optional)
        var sortedStats = new SortedDictionary<UnitName, SquadStats>(SquadStatsDictionary);
            foreach (var kvp in sortedStats)
            {
                var stats = kvp.Value;
                var attr = stats.SquadAttributes;

                values.Add(new List<string>
            {
                stats.unitName.ToString(),
                stats.unitType.ToString(),
                stats.unitSize.ToString(),
                stats.MeleeAttack.ToString(),
                stats.MeleeDefense.ToString(),
                stats.HitPointsPerUnit.ToString(),
                stats.WeaponStrength.ToString(),
                stats.Speed.ToString(),
                stats.Leadership.ToString(),
                stats.Armor.ToString(),
                stats.ChargeBonus.ToString(),
                stats.ChargeImactDamage.ToString(), // Note: Typo in field name, assuming it's 'Impact'
                stats.ChargeCount.ToString(),
                stats.BaseRange.ToString(),
                stats.attackAccuracy.ToString(),
                stats.MissileStrength.ToString(),
                stats.RarityTier.ToString(),
                stats.baseUnitCount.ToString(),
                stats.attackCooldown.ToString(),
                stats.Ammunition.ToString(),
                attr.None ? "Yes" : "No",
                attr.StandardShields ? "Yes" : "No",
                attr.ArmorPiercing ? "Yes" : "No",
                attr.AntiInfantry ? "Yes" : "No",
                attr.AntiLarge ? "Yes" : "No",
                attr.Terrifying ? "Yes" : "No",
                attr.Stalwart ? "Yes" : "No",
                attr.Outrider ? "Yes" : "No",
                attr.SwampCreature ? "Yes" : "No",
                attr.ForestDweller ? "Yes" : "No",
                attr.ChickenFlight ? "Yes" : "No",
                attr.Ethereal ? "Yes" : "No",
                attr.BloodFrenzy ? "Yes" : "No",
                attr.Rage ? "Yes" : "No",
                attr.Emblazing ? "Yes" : "No",
                attr.Unstoppable ? "Yes" : "No",
                attr.HeavyShields ? "Yes" : "No",
                attr.ArmorSundering ? "Yes" : "No",
                attr.MonsterSlayer ? "Yes" : "No",
                attr.FlamingAmmo ? "Yes" : "No",
                attr.ThickScales ? "Yes" : "No",
            });
            }
        //log the data being sent
        Debug.Log("Sending data to Google Sheets: " + values.Count + " rows.");

       // Manually build the JSON string to avoid JsonUtility limitations
        System.Text.StringBuilder jsonBuilder = new System.Text.StringBuilder();
        jsonBuilder.Append("{\"values\":[");
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) jsonBuilder.Append(",");
            jsonBuilder.Append("[");
            for (int j = 0; j < values[i].Count; j++)
            {
                if (j > 0) jsonBuilder.Append(",");
                string escaped = values[i][j].Replace("\\", "\\\\").Replace("\"", "\\\"");
                jsonBuilder.Append("\"" + escaped + "\"");
            }
            jsonBuilder.Append("]");
        }
        jsonBuilder.Append("]}");
        string json = jsonBuilder.ToString();
        Debug.Log("Sending JSON: " + json); // Log for debugging

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm(googleSheetUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Upload error: " + www.error + " | Response: " + www.downloadHandler.text);
            }
            else
            {
                Debug.Log("Upload successful: " + www.downloadHandler.text);
            }
        }
    }
    }
}