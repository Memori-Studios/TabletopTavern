using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using GPUECSAnimationBaker.Engine.AnimatorSystem;
using TJ;
using ProjectDawn.Navigation;
using System.Linq;
using Unity.Collections;
using Memori.Steamworks;
using System;
using Memori.Notifications;
using Memori.Localization;
using Memori.Audio;
using Memori.Scenes;
using System.Threading.Tasks;

namespace Memori.SaveData
{
    public class ArmySpawnManager : MonoBehaviour
    {
        [SerializeField] private Transform playerArmyCenter, enemyArmyCenter;
        [SerializeField] private Transform northEncirclementCenter;  // ~(0,0,+85), facing south
        [SerializeField] private Transform southEncirclementCenter;  // ~(0,0,-85), facing north
        [SerializeField] private Transform centerEncirclementCenter; // ~(0,0,0),   facing south
        [SerializeField] private List<Transform> enemyOutriderSpawnPoints;
        List<Vector3> enemyOutriderSpawnPointsShuffled = new();
        Team selectedTeam;
        Unity.Mathematics.Random random;
        Dictionary<string, int> uniqueIDToSquadId = new();
        Dictionary<int, int> squadIdToUnitCount = new();
        Dictionary<int, int> squadIdToInitialUnitCount = new();
        Dictionary<int, int> squadIdKillCounter = new();
        int squadIndex = 0;

        List<SquadToLoad> withdrawnSquads = new();

        // Summoned squads are spawned mid-battle by a spell. They are deliberately given a UnitIndex
        // far above any real army slot (player squadId == UnitIndex + 1, so real ids are 1-10) to
        // guarantee no collision. They are never written back to the campaign save - see
        // MapSquadsToKillsAndWithdrwanSquads, which only ever writes squads present in the loaded roster.
        const int SUMMON_UNIT_INDEX_BASE = 9000;
        int summonsThisBattle;
        readonly HashSet<string> summonedUniqueIds = new();
        readonly HashSet<int> summonedSquadIds = new();
        public bool IsSummonedSquad(int _squadId) => summonedSquadIds.Contains(_squadId);

        readonly float GAP_BETWEEN_SQUADS_X = 30f; // Distance between units
        readonly float GAP_BETWEEN_SQUADS_Z = 20f;  // Distance between front and back rows
        const float PLAYER_STAGING_Z = -165f;
        const float ENEMY_STAGING_Z  =  165f;
        bool enemyArmyContainsOutriders;

        public BattleLayoutType LayoutType { get; private set; }
        public bool PlayerArmyDeployed { get; private set; }
        public bool EnemyArmyDeployed  { get; private set; }
        private SquadToLoad[] _deferredEnemyArmy;
        private SquadToLoad[] _deferredOutriderSquads;

        private void Awake()
        {
            random = new Unity.Mathematics.Random(1);
            BattleManager.Instance.SquadManager.OnSquadUpdated += OnSquadUpdated;
        }

        public async Task ClearBothArmies()
        {
            BattleManager.Instance.UnitSelectionManager.DeselectSquadsBeforeDeletionOrSpawning();
            BattleManager.Instance.SquadManager.DeleteAllSquads();
            BattleManager.Instance.SquadManager.WipeRegisteredSquadData(true);
            BattleManager.Instance.SquadManager.WipeRegisteredSquadData(false);
            // ECS deletions are deferred across multiple frames via ECBs; wait until all squad entities are gone
            await Task.Yield();
            NativeArray<SquadEntity> remaining = BattleManager.Instance.SquadManager.RetrieveAllSquads();
            while (remaining.Length > 0)
            {
                remaining.Dispose();
                await Task.Yield();
                remaining = BattleManager.Instance.SquadManager.RetrieveAllSquads();
            }
            remaining.Dispose();
        }
        public async Task LoadBothArmies()
        {
            Debug.Log($"Loading armies from save files");
            (SquadToLoad[] playerArmy, Dictionary<string, SquadBattlePosition> playerSquadBattlePositions) = BattleManager.Instance.BattleSaveManager.GetArmyFromSaveData(true);
            (SquadToLoad[] enemyArmy, Dictionary<string, SquadBattlePosition> enemySquadBattlePositions) = BattleManager.Instance.BattleSaveManager.GetArmyFromSaveData(false);

            // Summonable units are preloaded alongside both armies - SummonSquad happens mid-battle
            // and cannot afford to stall on an async prefab load.
            IEnumerable<UnitName> unitNames = playerArmy.Select(s => s.UnitName)
                .Concat(enemyArmy.Select(s => s.UnitName))
                .Concat(BattleManager.Instance.SpellManager.GetSummonUnitNames())
                .Distinct();

            bool loaded = false;
            BattleManager.Instance.UnitGPUAnimLoader.PreloadUnitsAsync(unitNames, () => loaded = true);
            while (!loaded) await Task.Yield();

            bool isGarrison = BattleManager.Instance.BattleSaveManager.IsGarrisonBattle;
            bool isCustom   = BattleManager.Instance.BattleSaveManager.IsCustomBattle;

            if (isGarrison || isCustom)
            {
                LayoutType = BattleLayoutType.Normal;
                LoadPlayerArmyFromSaveFiles();
                if (isCustom) LoadEnemyArmyFromSaveFilesCustomBattle();
                else          LoadEnemyArmyFromSaveFiles();
                PlayerArmyDeployed = true;
                EnemyArmyDeployed  = true;
                return;
            }

            // Spawn both armies in a single staging row so they are visible on the battlefield
            // during the dice roll UI. Player goes south, enemy goes north.
            ResetSpawnState();
            selectedTeam = Team.Player;
            AssignSpawnPositionsStagingRow(playerArmy, new Vector3(0f, 0f, PLAYER_STAGING_Z), playerArmyCenter.rotation);
            selectedTeam = Team.Enemy;
            AssignSpawnPositionsStagingRow(enemyArmy, new Vector3(0f, 0f, ENEMY_STAGING_Z), enemyArmyCenter.rotation);

            // Both armies are now visible — open the scene transition so the player can see the battlefield.
            SceneHandler.Instance.AlertOfSceneSetUpComlete();

            // Show dice roll UI
            int roll = await BattleManager.Instance.UIManager.BattleDiceRollPanel.ShowAndRoll();
            LayoutType = roll <= 3 ? BattleLayoutType.EnemyDeferred
                       : BattleLayoutType.Normal;
            BattleManager.Instance.PositionDrawer.SetBattleLayout(LayoutType);

            if (LayoutType == BattleLayoutType.Normal || LayoutType == BattleLayoutType.EnemyDeferred)
            {
                SquadToLoad[] nonOutriders = enemyArmy.Where(s => !TabletopTavernData.Instance.GetSquadStats(s.UnitName).SquadAttributes.Outrider).ToArray();
                SquadToLoad[] outriders    = enemyArmy.Where(s =>  TabletopTavernData.Instance.GetSquadStats(s.UnitName).SquadAttributes.Outrider).ToArray();
                if (outriders.Length > 0) { _deferredOutriderSquads = outriders; enemyArmyContainsOutriders = true; }

                if (LayoutType == BattleLayoutType.Normal)
                {
                    if (nonOutriders.Length > 0) TeleportToNormalFormation(nonOutriders, enemyArmyCenter);
                    EnemyArmyDeployed = true;
                }
                else
                {
                    if (nonOutriders.Length > 0) _deferredEnemyArmy = nonOutriders;
                    EnemyArmyDeployed  = false;
                }

                // Teleport player — use saved positions if available, otherwise normal formation.
                if (playerSquadBattlePositions.Count > 0)
                {
                    List<SquadToLoad> newSquads = new();
                    foreach (SquadToLoad squad in playerArmy)
                    {
                        if (playerSquadBattlePositions.TryGetValue(squad.UniqueID, out SquadBattlePosition saved))
                            TeleportSquadUnits(squad, saved.Position, saved.Rotation, saved.SquadWidthAndDepth);
                        else
                            newSquads.Add(squad);
                    }
                    if (newSquads.Count > 0)
                        TeleportToNormalFormation(newSquads.ToArray(), playerArmyCenter);
                }
                else
                {
                    TeleportToNormalFormation(playerArmy, playerArmyCenter);
                }
                PlayerArmyDeployed = true;

                bool onlySakuraNormal = playerArmy.All(s => TabletopTavernData.Instance.GetRaceFromUnitName(s.UnitName) == Race.SakuraDynasty);
                BattleManager.Instance.AlertNonSakuraUnits(onlySakuraNormal);
                List<SavedSquadGroup> normalGroups = SaveDataHandler.Load().playerSquadGroups;
                if (normalGroups != null && normalGroups.Count > 0)
                    BattleManager.Instance.GroupManager.SetPendingGroups(normalGroups);
                return;
            }

            if (LayoutType == BattleLayoutType.PlayerEncircled)
            {
                TeleportToEncirclementColumns(playerArmy, Vector3.zero, playerArmyCenter.rotation);
                PlayerArmyDeployed = true;

                if (BattleManager.Instance.UIManager.BattleDiceRollPanel.StartBattleRequested)
                {
                    // Deploy enemy to both zones immediately then start battle.
                    int northCount = (enemyArmy.Length + 1) / 2;
                    SquadToLoad[] northHalf = enemyArmy.Take(northCount).ToArray();
                    SquadToLoad[] southHalf = enemyArmy.Skip(northCount).ToArray();
                    if (northHalf.Length > 0) TeleportToNormalFormation(northHalf, enemyArmyCenter);
                    if (southHalf.Length > 0) TeleportToNormalFormation(southHalf, southEncirclementCenter);
                    EnemyArmyDeployed = true;
                    BattleManager.Instance.StartBattle();
                }
                else
                {
                    _deferredEnemyArmy = enemyArmy;
                    EnemyArmyDeployed  = false;
                }
            }
            else // EnemyEncircled
            {
                TeleportToEncirclementColumns(enemyArmy, Vector3.zero, enemyArmyCenter.rotation);
                EnemyArmyDeployed = true;

                // Teleport player — shifted 10 units back to fit the reduced EnemyEncircled zone.
                Vector3 encirclementOffset = new Vector3(0f, 0f, -10f);
                if (playerSquadBattlePositions.Count > 0)
                {
                    List<SquadToLoad> newSquads = new();
                    foreach (SquadToLoad squad in playerArmy)
                    {
                        if (playerSquadBattlePositions.TryGetValue(squad.UniqueID, out SquadBattlePosition saved))
                            TeleportSquadUnits(squad, saved.Position + encirclementOffset, saved.Rotation, saved.SquadWidthAndDepth);
                        else
                            newSquads.Add(squad);
                    }
                    if (newSquads.Count > 0)
                        TeleportToNormalFormation(newSquads.ToArray(), playerArmyCenter, encirclementOffset);
                }
                else
                {
                    TeleportToNormalFormation(playerArmy, playerArmyCenter, encirclementOffset);
                }
                PlayerArmyDeployed = true;
            }

            bool onlySakura = playerArmy.All(s => TabletopTavernData.Instance.GetRaceFromUnitName(s.UnitName) == Race.SakuraDynasty);
            BattleManager.Instance.AlertNonSakuraUnits(onlySakura);

            List<SavedSquadGroup> savedGroups = SaveDataHandler.Load().playerSquadGroups;
            if (savedGroups != null && savedGroups.Count > 0)
                BattleManager.Instance.GroupManager.SetPendingGroups(savedGroups);
        }

        private void ResetSpawnState()
        {
            withdrawnSquads            = new();
            squadIdKillCounter         = new();
            squadIdToUnitCount         = new();
            squadIdToInitialUnitCount  = new();
            uniqueIDToSquadId          = new();
            enemyArmyContainsOutriders = false;
            squadIndex                 = 0;
            summonsThisBattle          = 0;
            summonedUniqueIds.Clear();
            summonedSquadIds.Clear();
            PlayerArmyDeployed         = false;
            EnemyArmyDeployed          = false;
            _deferredEnemyArmy         = null;
            _deferredOutriderSquads    = null;
        }


        private void AssignSpawnPositionsStagingRow(SquadToLoad[] squads, Vector3 centerPosition, Quaternion facing)
        {
            if (squads == null || squads.Length == 0) return;
            const int maxPerRow = 10;
            Vector3 right   = (facing * Vector3.right).normalized;
            Vector3 forward = (facing * Vector3.forward).normalized;
            int index = 0;
            int row = 0;
            while (index < squads.Length)
            {
                int count = Mathf.Min(maxPerRow, squads.Length - index);
                float totalWidth = (count - 1) * GAP_BETWEEN_SQUADS_X;
                Vector3 rowCenter = centerPosition - forward * (row * GAP_BETWEEN_SQUADS_Z);
                Vector3 startPos  = rowCenter - right * (totalWidth / 2f);
                for (int i = 0; i < count; i++)
                {
                    Vector3 spawnPos = startPos + right * (i * GAP_BETWEEN_SQUADS_X);
                    SpawnSquadFromSaveData(squads[index], facing, spawnPos, squadIndex++);
                    index++;
                }
                row++;
            }
        }

        #region Campaign Save Data
        public void LoadPlayerArmyFromSaveFiles()
        {
            // Debug.Log($"Loading armies from save files for campaign");
            (SquadToLoad[] playerArmy, Dictionary<string, SquadBattlePosition> playerSquadBattlePositions) = BattleManager.Instance.BattleSaveManager.GetArmyFromSaveData(true);


            withdrawnSquads = new();
            squadIdKillCounter = new();
            squadIdToUnitCount = new();
            squadIdToInitialUnitCount = new();
            uniqueIDToSquadId = new();

            selectedTeam = Team.Player;
        bool isGarrisonBattle = BattleManager.Instance.BattleSaveManager.IsGarrisonBattle;

#if UNITY_EDITOR
            if (BattleManager.Instance.BattleSaveManager.IsCustomBattle)
            {
                if (playerArmy != null && playerArmy.Length != 0)
                    AssignSpawnPositionsNormalBattle(playerArmy, playerArmyCenter);
            }
            else
#endif
            if(playerSquadBattlePositions.Count == 0 || isGarrisonBattle)
            {
                if (playerArmy != null && playerArmy.Length != 0) {
                    AssignSpawnPositionsNormalBattle(playerArmy, playerArmyCenter);
                }
            }
            else
            {
                List<SquadToLoad> newSquads = new();
                for (int i = 0; i < playerArmy.Length; i++)
                {
                    //check playerSquadBattlePositions for matching UniqueID
                    if (playerSquadBattlePositions.TryGetValue(playerArmy[i].UniqueID, out SquadBattlePosition battlePosition))
                    {
                        SpawnSquadFromSaveData(playerArmy[i], battlePosition.Rotation, battlePosition.Position, i, battlePosition.SquadWidthAndDepth);
                    }
                    else
                    {
                        newSquads.Add(playerArmy[i]);
                    }
                }
                // Spread new squads evenly across the back of the deployment zone
                if (newSquads.Count > 0)
                {
                    Vector3 backCenter = playerArmyCenter.position - 2.5f * GAP_BETWEEN_SQUADS_Z * playerArmyCenter.forward;
                    float totalWidth = (newSquads.Count - 1) * GAP_BETWEEN_SQUADS_X;
                    Vector3 startPos = backCenter - playerArmyCenter.right * (totalWidth / 2f);
                    for (int i = 0; i < newSquads.Count; i++)
                    {
                        Vector3 spawnPos = startPos + playerArmyCenter.right * (i * GAP_BETWEEN_SQUADS_X);
                        SpawnSquadFromSaveData(newSquads[i], playerArmyCenter.rotation, spawnPos, i);
                    }
                }
            }

            //check if all units are sakura dynasty, and if not custom battle alert battlemanager
            if(!BattleManager.Instance.BattleSaveManager.IsCustomBattle)
            {
                bool onlySakura = playerArmy.All(s => TabletopTavernData.Instance.GetRaceFromUnitName(s.UnitName) == Race.SakuraDynasty);
                BattleManager.Instance.AlertNonSakuraUnits(onlySakura);
            }

            bool isCustomBattle = BattleManager.Instance.BattleSaveManager.IsCustomBattle;
            List<SavedSquadGroup> savedGroups = isCustomBattle
                ? SaveDataHandler.LoadCustomBattleSaveData().playerCustomBattleSquadGroups
                : SaveDataHandler.Load().playerSquadGroups;
            if (savedGroups != null && savedGroups.Count > 0)
                BattleManager.Instance.GroupManager.SetPendingGroups(savedGroups);
        }
        public void LoadEnemyArmyFromSaveFilesCustomBattle()
        {
            // Debug.Log($"Loading armies from save files");
            (SquadToLoad[] enemyArmy, Dictionary<string, SquadBattlePosition> playerSquadBattlePositions)= BattleManager.Instance.BattleSaveManager.GetArmyFromSaveData(false);

            withdrawnSquads = new();
            selectedTeam = Team.Enemy;
            ShuffleEnemyOutriderSpawnPoints();

#if UNITY_EDITOR
            if (enemyArmy != null && enemyArmy.Length != 0)
                // AssignSpawnPositionsNormalBattle(enemyArmy, enemyArmyCenter);
                AssignSpawnPositionsGarrisionBattle(enemyArmy, enemyArmyCenter);
#else
            if(playerSquadBattlePositions.Count == 0)
            {
                if (enemyArmy != null && enemyArmy.Length != 0) {
                    AssignSpawnPositionsNormalBattle(enemyArmy, enemyArmyCenter);
                }
            }
            else
            {
                List<SquadToLoad> newEnemySquads = new();
                for (int i = 0; i < enemyArmy.Length; i++)
                {
                    //check playerSquadBattlePositions for matching UniqueID
                    if (playerSquadBattlePositions.TryGetValue(enemyArmy[i].UniqueID, out SquadBattlePosition battlePosition))
                    {
                        SpawnSquadFromSaveData(enemyArmy[i], battlePosition.Rotation, battlePosition.Position, i, battlePosition.SquadWidthAndDepth);
                    }
                    else
                    {
                        newEnemySquads.Add(enemyArmy[i]);
                    }
                }
                if (newEnemySquads.Count > 0)
                    AssignSpawnPositionsNormalBattle(newEnemySquads.ToArray(), enemyArmyCenter);
            }
#endif
        }
        public void LoadEnemyArmyFromSaveFiles()
        {
            SquadToLoad[] enemyArmy = BattleManager.Instance.BattleSaveManager.GetArmyFromSaveData(false).Item1;
            if (enemyArmy == null || enemyArmy.Length == 0)
            {
                Debug.Log("No enemy army found in save data, skipping enemy spawn");
                return;
            }

            ShuffleEnemyOutriderSpawnPoints();
            selectedTeam = Team.Enemy;
            bool isGarrisonBattle = BattleManager.Instance.BattleSaveManager.IsGarrisonBattle;
            if (isGarrisonBattle)
            {
                AssignSpawnPositionsGarrisionBattle(enemyArmy, enemyArmyCenter);
            }
            else
            {
                AssignSpawnPositionsNormalBattle(enemyArmy, enemyArmyCenter);
            }
        }
        private void ShuffleEnemyOutriderSpawnPoints()
        {
            for (int i = 0; i < enemyOutriderSpawnPoints.Count; i++)
            {
                int j = random.NextInt(0, enemyOutriderSpawnPoints.Count);
                Transform temp = enemyOutriderSpawnPoints[i];
                enemyOutriderSpawnPoints[i] = enemyOutriderSpawnPoints[j];
                enemyOutriderSpawnPoints[j] = temp;
            }
            enemyOutriderSpawnPointsShuffled = enemyOutriderSpawnPoints.Select(x => x.position).ToList();
        }

        public void NotifyOutridersIfPresent()
        {
            if (!enemyArmyContainsOutriders) return;
            string outriderMessageLocalized = LocalizationManager.Instance.GetText("outrider_spotted_message");
            NotificationManager.Instance.DisplayNotification(outriderMessageLocalized);
            IAudioRequester.Instance.PlaySFX(SFXData.OutridersSpotted);
        }

        public string GetUnitUniqueIDFromSquadID(int _squadId)
        {
            foreach(var kvp in uniqueIDToSquadId) {
                if(kvp.Value == _squadId) {
                    return kvp.Key;
                }
            }
            Debug.LogError($"No squad with id {_squadId} found");
            return new Guid().ToString();
        }
        public int GetSquadIDFromUnitUniqueID(string _unitUniqueID)
        {
            if(uniqueIDToSquadId.TryGetValue(_unitUniqueID, out int squadId)) {
                return squadId;
            }
            Debug.LogError($"No squad with uniqueID {_unitUniqueID} found");
            return 0;
        }
        public void MapSquadsToKillsAndWithdrwanSquads(bool _playerWonBattle)
        {
            SquadToLoad[] playerArmyLoaded = BattleManager.Instance.BattleSaveManager.GetArmyFromSaveData(true).Item1;
            SquadToLoad[] enemyArmyLoaded = BattleManager.Instance.BattleSaveManager.GetArmyFromSaveData(false).Item1;

            List<SquadKillsStored> squadGUIDKillCounter = new();
            for (int i = 0; i < playerArmyLoaded.Length; i++)
            {
                if (uniqueIDToSquadId.TryGetValue(playerArmyLoaded[i].UniqueID, out int squadId))
                {
                    int killCount = 0;
                    if (squadIdKillCounter.TryGetValue(squadId, out killCount))
                    {
                        squadIdKillCounter[squadId] = killCount;
                    }
                    else
                    {
                        squadIdKillCounter[squadId] = 0;
                    }
                    squadGUIDKillCounter.Add(new SquadKillsStored { SquadGUID = playerArmyLoaded[i].UniqueID, Kills = killCount });
                    SteamAchievements.AddStat(SteamStatId.UnitKills, killCount);

                    if(withdrawnSquads.Any(s => s.UniqueID == playerArmyLoaded[i].UniqueID)) {
                        // Debug.Log($"Skipping withdrawn squad {playerArmyLoaded[i].UniqueID} from save");
                        continue;
                    }
                    int hitPoints = TabletopTavernData.Instance.GetHitPointsPerUnit(playerArmyLoaded[i].UnitName);
                    // Debug.Log($"getting squad {playerArmyLoaded[i].UniqueID} with id {squadId} and {squadIdToUnitCount[squadId]*hitPoints} units");
                    playerArmyLoaded[i].SquadCurrentHealth = squadIdToUnitCount[squadId] * hitPoints;
                    
                }
            }
            
            for (int i = 0; i < withdrawnSquads.Count; i++)
            {
                for (int j = 0; j < playerArmyLoaded.Length; j++)
                {
                    if (playerArmyLoaded[j].UniqueID == withdrawnSquads[i].UniqueID)
                    {
                        Debug.Log($"updating health of withdrawn squad with current health {withdrawnSquads[i].SquadCurrentHealth}");
                        // int hitPoints = TabletopTavernData.Instance.GetHitPointsPerUnit(playerArmyLoaded[j].UnitName);
                        playerArmyLoaded[j].SquadCurrentHealth = withdrawnSquads[i].SquadCurrentHealth;
                        break;
                    }
                }
            }
            
            for (int i = 0; i < enemyArmyLoaded.Length; i++)
            {
                if (uniqueIDToSquadId.TryGetValue(enemyArmyLoaded[i].UniqueID, out int squadId))
                {
                    // Debug.Log($"Squad {enemyArmyLoaded[i].UniqueID} has {squadIdToUnitCount[squadId]} units");
                    int hitPoints = TabletopTavernData.Instance.GetHitPointsPerUnit(enemyArmyLoaded[i].UnitName);
                    enemyArmyLoaded[i].SquadCurrentHealth = squadIdToUnitCount[squadId] * hitPoints;
                    int killCount = 0;
                    if (squadIdKillCounter.TryGetValue(squadId, out killCount))
                    {
                        squadIdKillCounter[squadId] = killCount;
                    }
                    else
                    {
                        squadIdKillCounter[squadId] = 0;
                    }
                    squadGUIDKillCounter.Add(new SquadKillsStored { SquadGUID = enemyArmyLoaded[i].UniqueID, Kills = killCount });
                }
            }

            // Every squad's SquadCurrentHealth is now settled (normal combat, mercy-kill wipe, or withdrawal
            // all funnel into the writes above). Losses are the exact starting roster size (captured once
            // at spawn, never re-derived from health) minus the ending unit count - no hitPoints rounding
            // involved, so a single dead unit can never be lost to a fractional-health starting value.
            List<SquadLossesStored> squadGUIDLossCounter = new();
            void AddLossEntry(SquadToLoad squad)
            {
                if (!uniqueIDToSquadId.TryGetValue(squad.UniqueID, out int squadId)) return;
                if (!squadIdToInitialUnitCount.TryGetValue(squadId, out int initialUnits)) return;
                int hitPoints = TabletopTavernData.Instance.GetHitPointsPerUnit(squad.UnitName);
                if (hitPoints <= 0) return;
                int endingUnits = squad.SquadCurrentHealth / hitPoints;
                squadGUIDLossCounter.Add(new SquadLossesStored { SquadGUID = squad.UniqueID, Losses = Mathf.Max(0, initialUnits - endingUnits) });
            }
            foreach (SquadToLoad squad in playerArmyLoaded) AddLossEntry(squad);
            foreach (SquadToLoad squad in enemyArmyLoaded) AddLossEntry(squad);

            SaveDataHandler.SaveSquadsPostBattle(playerArmyLoaded, enemyArmyLoaded, _playerWonBattle, squadGUIDKillCounter, squadGUIDLossCounter);
            Debug.Log($"Saved post-battle squad data for {( _playerWonBattle ? "player" : "enemy")} with {squadGUIDKillCounter.Count} entries");
        }
        #endregion
        private void TeleportToEncirclementColumns(SquadToLoad[] squads, Vector3 center, Quaternion baseRotation)
        {
            const int numColumns      = 5;
            const int depthPerColumn  = 2;
            Quaternion rotation = baseRotation * Quaternion.Euler(0f, 90f, 0f);

            float halfWidth = (numColumns - 1)     * GAP_BETWEEN_SQUADS_Z * 0.5f;
            float halfDepth = (depthPerColumn - 1) * GAP_BETWEEN_SQUADS_X * 0.5f;

            int index = 0;
            for (int col = 0; col < numColumns && index < squads.Length; col++)
            {
                float x = center.x - halfWidth + col * GAP_BETWEEN_SQUADS_Z;
                for (int row = 0; row < depthPerColumn && index < squads.Length; row++)
                {
                    float z = center.z + halfDepth - row * GAP_BETWEEN_SQUADS_X;
                    TeleportSquadUnits(squads[index++], new Vector3(x, center.y, z), rotation);
                }
            }
        }

        private void TeleportToNormalFormation(SquadToLoad[] squads, Transform spawnPointTransform, Vector3 positionOffset = default)
        {
            List<SquadToLoad> meleeUnits   = new();
            List<SquadToLoad> rangedUnits  = new();
            List<SquadToLoad> cavalryUnits = new();
            List<SquadToLoad> largeUnits   = new();

            foreach (SquadToLoad squad in squads)
            {
                UnitSize size = TabletopTavernData.Instance.GetUnitSizeFromUnitName(squad.UnitName);
                if (size == UnitSize.Cavalry)        cavalryUnits.Add(squad);
                else if (size == UnitSize.Monstrous) largeUnits.Add(squad);
                else switch (TabletopTavernData.Instance.GetUnitTypeFromUnitName(squad.UnitName))
                {
                    case UnitType.Melee:     meleeUnits.Add(squad);  break;
                    case UnitType.Hybrid:    meleeUnits.Add(squad);  break;
                    case UnitType.Ranged:    rangedUnits.Add(squad); break;
                    case UnitType.Artillery: rangedUnits.Add(squad); break;
                }
            }

            SquadToLoad[] frontRow  = new SquadToLoad[5];
            SquadToLoad[] middleRow = new SquadToLoad[5];
            SquadToLoad[] backRow   = new SquadToLoad[5];

            int[] centerOut = new int[] { 2, 1, 3, 0, 4 };

            HashSet<SquadToLoad> used = new();
            int meleeUsed = 0, rangedUsed = 0, cavalryUsed = 0, largeUsed = 0;

            for (int i = 0; i < centerOut.Length && meleeUsed < meleeUnits.Count; i++)
            { frontRow[centerOut[i]] = meleeUnits[meleeUsed]; used.Add(meleeUnits[meleeUsed++]); }

            for (int i = 0; i < centerOut.Length && largeUsed < largeUnits.Count; i++)
            { middleRow[centerOut[i]] = largeUnits[largeUsed]; used.Add(largeUnits[largeUsed++]); }

            List<int> midAvail = new();
            for (int pos = 0; pos < 5; pos++) if (middleRow[pos].UniqueID == null) midAvail.Add(pos);
            midAvail.Sort((a, b) => Mathf.Abs(a - 2).CompareTo(Mathf.Abs(b - 2)));
            for (int i = 0; i < midAvail.Count && rangedUsed < rangedUnits.Count; i++)
            { middleRow[midAvail[i]] = rangedUnits[rangedUsed]; used.Add(rangedUnits[rangedUsed++]); }
            for (int pos = 0; pos < 5 && meleeUsed < meleeUnits.Count; pos++)
            { if (middleRow[pos].UniqueID != null) continue; middleRow[pos] = meleeUnits[meleeUsed]; used.Add(meleeUnits[meleeUsed++]); }

            for (int i = 0; i < centerOut.Length && cavalryUsed < cavalryUnits.Count; i++)
            { backRow[centerOut[i]] = cavalryUnits[cavalryUsed]; used.Add(cavalryUnits[cavalryUsed++]); }

            List<int> backAvail = new();
            for (int pos = 0; pos < 5; pos++) if (backRow[pos].UniqueID == null) backAvail.Add(pos);
            backAvail.Sort((a, b) => Mathf.Abs(b - 2).CompareTo(Mathf.Abs(a - 2)));
            for (int i = 0; i < backAvail.Count && rangedUsed < rangedUnits.Count; i++)
            { backRow[backAvail[i]] = rangedUnits[rangedUsed]; used.Add(rangedUnits[rangedUsed++]); }

            List<SquadToLoad> remaining = new();
            remaining.AddRange(meleeUnits.Skip(meleeUsed).Where(u => !used.Contains(u)));
            remaining.AddRange(largeUnits.Skip(largeUsed).Where(u => !used.Contains(u)));
            remaining.AddRange(rangedUnits.Skip(rangedUsed).Where(u => !used.Contains(u)));
            remaining.AddRange(cavalryUnits.Skip(cavalryUsed).Where(u => !used.Contains(u)));
            int remIdx = 0;
            foreach (SquadToLoad[] row in new[] { frontRow, middleRow, backRow })
                for (int i = 0; i < 5 && remIdx < remaining.Count; i++)
                    if (row[i].UniqueID == null) { row[i] = remaining[remIdx]; used.Add(remaining[remIdx++]); }

            Vector3 origin   = spawnPointTransform.position + positionOffset;
            Quaternion facing = spawnPointTransform.rotation;
            Vector3 right    = spawnPointTransform.right;
            Vector3 fwd      = spawnPointTransform.forward;

            TeleportCenteredRow(frontRow,  origin,                        right, facing);
            TeleportCenteredRow(middleRow, origin - fwd * GAP_BETWEEN_SQUADS_Z,     right, facing);
            TeleportCenteredRow(backRow,   origin - fwd * GAP_BETWEEN_SQUADS_Z * 2, right, facing);
        }

        private void TeleportCenteredRow(SquadToLoad[] row, Vector3 rowCenter, Vector3 rightDirection, Quaternion facing)
        {
            if (row.Count(s => s.UniqueID != null) == 0) return;
            rightDirection = rightDirection.normalized;

            int centeredCount = 0;
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i].UniqueID == null) continue;
                bool isCav = TabletopTavernData.Instance.GetUnitSizeFromUnitName(row[i].UnitName) == UnitSize.Cavalry;
                if (!(isCav && (i == 0 || i == 4))) centeredCount++;
            }

            float totalWidth  = centeredCount > 1 ? (centeredCount - 1) * GAP_BETWEEN_SQUADS_X : 0f;
            Vector3 startOff  = -rightDirection * (totalWidth / 2f);
            float maxWidth    = 4 * GAP_BETWEEN_SQUADS_X;
            Vector3 leftFlank  = -rightDirection * (maxWidth / 2f);
            Vector3 rightFlank =  rightDirection * (maxWidth / 2f);

            int j = 0;
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i].UniqueID == null) continue;
                bool isCav = TabletopTavernData.Instance.GetUnitSizeFromUnitName(row[i].UnitName) == UnitSize.Cavalry;
                Vector3 pos;
                if (isCav && (i == 0 || i == 4))
                    pos = rowCenter + (i == 0 ? leftFlank : rightFlank);
                else
                { pos = rowCenter + startOff + rightDirection * j * GAP_BETWEEN_SQUADS_X; j++; }
                TeleportSquadUnits(row[i], pos, facing);
            }
        }

        private void TeleportSquadUnits(SquadToLoad squad, Vector3 spawnCenter, Quaternion rotation, int2 widthAndDepth = default)
        {
            if (!uniqueIDToSquadId.TryGetValue(squad.UniqueID, out int squadId)) return;
            if (!squadIdToUnitCount.TryGetValue(squadId, out int unitCount)) return;

            if (widthAndDepth.x == 0) widthAndDepth = CalculateWidthAndDepth(unitCount, squad.UnitName);

            float spread = TabletopTavernData.Instance.GetUnitSpreadFromUnitName(squad.UnitName);
            List<float3> positions = BattleManager.Instance.PositionDrawer.Formation
                .GeneratePositionsForSquad(widthAndDepth, unitCount, spread);

            for (int i = 0; i < positions.Count; i++)
            {
                positions[i] = math.mul(rotation, positions[i]);
                positions[i] = TabletopTavernData.Instance.GetNoiseFromUnitName(squad.UnitName, positions[i]);
                positions[i] += (float3)spawnCenter;
            }

            EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = em.CreateEntityQuery(
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<SetDestination>(),
                ComponentType.ReadOnly<Unit>(),
                ComponentType.ReadOnly<UnitPosition>());

            using NativeArray<Entity>       entities      = query.ToEntityArray(Allocator.Temp);
            using NativeArray<Unit>         units         = query.ToComponentDataArray<Unit>(Allocator.Temp);
            using NativeArray<UnitPosition> unitPositions = query.ToComponentDataArray<UnitPosition>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (units[i].squadId != squadId) continue;
                int idx = unitPositions[i].unitIndex;
                if (idx >= positions.Count) continue;

                float3 newPos = GetPointOnTerrain(positions[idx]);

                LocalTransform lt = em.GetComponentData<LocalTransform>(entities[i]);
                lt.Position = newPos;
                lt.Rotation = rotation;
                em.SetComponentData(entities[i], lt);

                SetDestination sd = em.GetComponentData<SetDestination>(entities[i]);
                sd.destinationPosition = newPos;
                sd.squadPosition       = newPos;
                em.SetComponentData(entities[i], sd);

                em.SetComponentData(entities[i], new RotateUnit { targetRotation = rotation });
                em.SetComponentEnabled<RotateUnit>(entities[i], true);
            }

            query.Dispose();
        }

        // Iltharion's Starstep: blink a live squad to an arbitrary point mid-battle. Reuses the exact
        // battle-spawn teleport recipe (regenerate the formation at the destination, then set each unit's
        // LocalTransform + SetDestination + RotateUnit) so nav agents resettle the same way they do at
        // spawn. Facing is preserved from the squad's current orientation.
        public void TeleportSquadToPoint(int squadId, Vector3 center)
        {
            if (!squadIdToUnitCount.TryGetValue(squadId, out int unitCount)) return;

            SquadEntity squadEntity = BattleManager.Instance.SquadManager.GetSquad(squadId);
            if (squadEntity.SelfEntity == Entity.Null) return;
            UnitName unitName = squadEntity.UnitName;

            int2 widthAndDepth = CalculateWidthAndDepth(unitCount, unitName);
            float spread = TabletopTavernData.Instance.GetUnitSpreadFromUnitName(unitName);
            List<float3> positions = BattleManager.Instance.PositionDrawer.Formation
                .GeneratePositionsForSquad(widthAndDepth, unitCount, spread);

            EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = em.CreateEntityQuery(
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<SetDestination>(),
                ComponentType.ReadOnly<Unit>(),
                ComponentType.ReadOnly<UnitPosition>());

            using NativeArray<Entity>       entities      = query.ToEntityArray(Allocator.Temp);
            using NativeArray<Unit>         units         = query.ToComponentDataArray<Unit>(Allocator.Temp);
            using NativeArray<UnitPosition> unitPositions = query.ToComponentDataArray<UnitPosition>(Allocator.Temp);

            // Preserve the squad's current facing (read from any one of its units).
            Quaternion rotation = Quaternion.identity;
            for (int i = 0; i < entities.Length; i++)
                if (units[i].squadId == squadId) { rotation = em.GetComponentData<LocalTransform>(entities[i]).Rotation; break; }

            for (int i = 0; i < positions.Count; i++)
            {
                positions[i] = math.mul(rotation, positions[i]);
                positions[i] = TabletopTavernData.Instance.GetNoiseFromUnitName(unitName, positions[i]);
                positions[i] += (float3)center;
            }

            for (int i = 0; i < entities.Length; i++)
            {
                if (units[i].squadId != squadId) continue;
                int idx = unitPositions[i].unitIndex;
                if (idx >= positions.Count) continue;

                float3 newPos = GetPointOnTerrain(positions[idx]);

                LocalTransform lt = em.GetComponentData<LocalTransform>(entities[i]);
                lt.Position = newPos;
                lt.Rotation = rotation;
                em.SetComponentData(entities[i], lt);

                SetDestination sd = em.GetComponentData<SetDestination>(entities[i]);
                sd.destinationPosition = newPos;
                sd.squadPosition       = newPos;
                em.SetComponentData(entities[i], sd);

                em.SetComponentData(entities[i], new RotateUnit { targetRotation = rotation });
                em.SetComponentEnabled<RotateUnit>(entities[i], true);
            }

            query.Dispose();
        }

        public void NotifyEnemyDeployed() => EnemyArmyDeployed = true;

        public void SpawnDeferredEnemy()
        {
            if (_deferredEnemyArmy != null && _deferredEnemyArmy.Length > 0)
            {
                if (LayoutType == BattleLayoutType.PlayerEncircled)
                {
                    int northCount = (_deferredEnemyArmy.Length + 1) / 2;
                    SquadToLoad[] northHalf = _deferredEnemyArmy.Take(northCount).ToArray();
                    SquadToLoad[] southHalf = _deferredEnemyArmy.Skip(northCount).ToArray();
                    if (northHalf.Length > 0) TeleportToNormalFormation(northHalf, enemyArmyCenter);
                    if (southHalf.Length > 0) TeleportToNormalFormation(southHalf, southEncirclementCenter);
                }
                else
                {
                    TeleportToNormalFormation(_deferredEnemyArmy, enemyArmyCenter);
                }
                _deferredEnemyArmy = null;
                EnemyArmyDeployed  = true;
            }

            if (_deferredOutriderSquads != null && _deferredOutriderSquads.Length > 0)
            {
                ShuffleEnemyOutriderSpawnPoints();
                TeleportOutridersToSpawnPoints(_deferredOutriderSquads);
                _deferredOutriderSquads = null;
            }
        }

        private void TeleportOutridersToSpawnPoints(SquadToLoad[] outriders)
        {
            for (int i = 0; i < outriders.Length && i < enemyOutriderSpawnPoints.Count; i++)
                TeleportSquadUnits(outriders[i], enemyOutriderSpawnPoints[i].position, enemyOutriderSpawnPoints[i].rotation);
        }
        private void AssignSpawnPositionsGarrisionBattle(SquadToLoad[] squadsToLoad, Transform spawnPointTransform)
        {
            if (_gateGameObjects.Count == 0)
            {
                Debug.LogWarning("Garrison battle has no gate game objects, falling back to normal spawn.");
                AssignSpawnPositionsNormalBattle(squadsToLoad, spawnPointTransform);
                return;
            }

            var sortedGates = _gateGameObjects
                .OrderBy(kv => kv.Value.transform.position.x)
                .Select(kv => (gateIdx: kv.Key, pos: kv.Value.transform.position))
                .ToList();

            int gateCount = sortedGates.Count;
            List<List<SquadToLoad>> perGate = new(gateCount);
            for (int i = 0; i < gateCount; i++)
                perGate.Add(new List<SquadToLoad>());
            for (int i = 0; i < squadsToLoad.Length; i++)
                perGate[i % gateCount].Add(squadsToLoad[i]);

            const float depthPerRank = 15f;
            // Half-gap cleared at the gate center for the flanking formation (4+ squads total)
            const float flankGap = 15f;
            squadIndex = 0;
            Quaternion facing = spawnPointTransform.rotation;

            _gateDefenderUniqueIds.Clear();
            for (int g = 0; g < gateCount; g++)
            {
                int gateIdx = sortedGates[g].gateIdx;
                Vector3 gatePos = sortedGates[g].pos;
                List<SquadToLoad> assigned = perGate[g];
                _gateDefenderUniqueIds[gateIdx] = new List<string>();

                if (squadsToLoad.Length < 4)
                {
                    for (int rank = 0; rank < assigned.Count; rank++)
                    {
                        _gateDefenderUniqueIds[gateIdx].Add(assigned[rank].UniqueID);
                        Vector3 spawnPos = gatePos + new Vector3(0f, 0f, depthPerRank * (rank + 1));
                        SpawnSquadFromSaveData(assigned[rank], facing, spawnPos, squadIndex++);
                    }
                }
                else
                {
                    // First 2 per gate go inside with the flanking formation.
                    // Remaining squads spawn outside the wall directly in front of the gate.
                    List<SquadToLoad> inside  = assigned.Count <= 2 ? assigned : assigned.GetRange(0, 2);
                    List<SquadToLoad> outside = assigned.Count <= 2 ? new List<SquadToLoad>() : assigned.GetRange(2, assigned.Count - 2);

                    int leftCount = inside.Count / 2;
                    for (int i = 0; i < inside.Count; i++)
                    {
                        _gateDefenderUniqueIds[gateIdx].Add(inside[i].UniqueID);
                        Vector3 spawnPos;
                        if (i < leftCount)
                        {
                            spawnPos = gatePos + new Vector3(-(flankGap + i * GAP_BETWEEN_SQUADS_X), 0f, depthPerRank);
                        }
                        else
                        {
                            int rightIdx = i - leftCount;
                            spawnPos = gatePos + new Vector3(flankGap + rightIdx * GAP_BETWEEN_SQUADS_X, 0f, depthPerRank);
                        }
                        SpawnSquadFromSaveData(inside[i], facing, spawnPos, squadIndex++);
                    }

                    const int maxOutsideRowWidth = 3;
                    int outsideRowIndex = 0;
                    for (int i = 0; i < outside.Count; i += maxOutsideRowWidth)
                    {
                        int rowCount = Mathf.Min(maxOutsideRowWidth, outside.Count - i);
                        float rowWidth = (rowCount - 1) * GAP_BETWEEN_SQUADS_X;
                        float rowStartX = -rowWidth / 2f;
                        float rowZ = depthPerRank * (outsideRowIndex + 2) + 5f;

                        for (int j = 0; j < rowCount; j++)
                        {
                            SquadToLoad squad = outside[i + j];
                            _gateDefenderUniqueIds[gateIdx].Add(squad.UniqueID);
                            Vector3 spawnPos = gatePos + new Vector3(rowStartX + j * GAP_BETWEEN_SQUADS_X, 0f, rowZ);
                            SpawnSquadFromSaveData(squad, facing, spawnPos, squadIndex++);
                        }

                        outsideRowIndex++;
                    }
                }
            }
        }
        private void AssignSpawnPositionsNormalBattle(SquadToLoad[] squadsToLoad, Transform spawnPointTransform) 
        {
            // Categorize units without overlap
            List<SquadToLoad> meleeUnits   = new();
            List<SquadToLoad> rangedUnits  = new();
            List<SquadToLoad> cavalryUnits = new();
            List<SquadToLoad> largeUnits   = new(); // Monstrous, non-cavalry

            foreach (SquadToLoad squad in squadsToLoad)
            {
                UnitSize size = TabletopTavernData.Instance.GetUnitSizeFromUnitName(squad.UnitName);
                if (size == UnitSize.Cavalry)
                {
                    cavalryUnits.Add(squad);
                }
                else if (size == UnitSize.Monstrous)
                {
                    largeUnits.Add(squad);
                }
                else
                {
                    switch (TabletopTavernData.Instance.GetUnitTypeFromUnitName(squad.UnitName))
                    {
                        case UnitType.Melee: meleeUnits.Add(squad); break;
                        case UnitType.Hybrid: meleeUnits.Add(squad); break;
                        case UnitType.Ranged: rangedUnits.Add(squad); break;
                        case UnitType.Artillery: rangedUnits.Add(squad); break;
                    }
                }
            }

            // Distribute units into rows (max 5 per row, total 15 slots)
            SquadToLoad[] frontRow  = new SquadToLoad[5];
            SquadToLoad[] middleRow = new SquadToLoad[5];
            SquadToLoad[] backRow   = new SquadToLoad[5];

            int meleeCount   = meleeUnits.Count;
            int rangedCount  = rangedUnits.Count;
            int cavalryCount = cavalryUnits.Count;
            int largeCount   = largeUnits.Count;

            HashSet<SquadToLoad> usedUnits = new();
            int[] centerOutPositions = new int[] { 2, 1, 3, 0, 4 }; // center first
            int[] flankPositions     = new int[] { 0, 4, 1, 3, 2 }; // outer flanks first

            // --- Front row: melee in center ---
            int meleeUsed = 0;
            for (int i = 0; i < centerOutPositions.Length && meleeUsed < meleeCount; i++)
            {
                frontRow[centerOutPositions[i]] = meleeUnits[meleeUsed];
                usedUnits.Add(meleeUnits[meleeUsed++]);
            }

            // --- Middle row: large units in center, ranged outside, overflow melee in remaining ---
            int largeUsed = 0;
            for (int i = 0; i < centerOutPositions.Length && largeUsed < largeCount; i++)
            {
                middleRow[centerOutPositions[i]] = largeUnits[largeUsed];
                usedUnits.Add(largeUnits[largeUsed++]);
            }

            List<int> middleAvailableSlots = new();
            for (int pos = 0; pos < 5; pos++)
                if (middleRow[pos].UniqueID == null) middleAvailableSlots.Add(pos);
            middleAvailableSlots.Sort((a, b) => Mathf.Abs(a - 2).CompareTo(Mathf.Abs(b - 2))); // center-first so ranged fills inward after large

            int rangedUsed = 0;
            for (int i = 0; i < middleAvailableSlots.Count && rangedUsed < rangedCount; i++)
            {
                middleRow[middleAvailableSlots[i]] = rangedUnits[rangedUsed];
                usedUnits.Add(rangedUnits[rangedUsed++]);
            }

            // overflow melee fills any remaining slots
            for (int pos = 0; pos < 5 && meleeUsed < meleeCount; pos++)
            {
                if (middleRow[pos].UniqueID != null) continue;
                middleRow[pos] = meleeUnits[meleeUsed];
                usedUnits.Add(meleeUnits[meleeUsed++]);
            }

            // --- Back row: cavalry in center, remaining ranged on flanks ---
            int cavalryUsed = 0;
            for (int i = 0; i < centerOutPositions.Length && cavalryUsed < cavalryCount; i++)
            {
                backRow[centerOutPositions[i]] = cavalryUnits[cavalryUsed];
                usedUnits.Add(cavalryUnits[cavalryUsed++]);
            }

            List<int> backAvailableSlots = new();
            for (int pos = 0; pos < 5; pos++)
                if (backRow[pos].UniqueID == null) backAvailableSlots.Add(pos);
            backAvailableSlots.Sort((a, b) => Mathf.Abs(b - 2).CompareTo(Mathf.Abs(a - 2))); // outer first

            for (int i = 0; i < backAvailableSlots.Count && rangedUsed < rangedCount; i++)
            {
                backRow[backAvailableSlots[i]] = rangedUnits[rangedUsed];
                usedUnits.Add(rangedUnits[rangedUsed++]);
            }

            // --- Fill remaining units into any open slots ---
            List<SquadToLoad> remainingUnits = new();
            remainingUnits.AddRange(meleeUnits.Skip(meleeUsed).Where(u => !usedUnits.Contains(u)));
            remainingUnits.AddRange(largeUnits.Skip(largeUsed).Where(u => !usedUnits.Contains(u)));
            remainingUnits.AddRange(rangedUnits.Skip(rangedUsed).Where(u => !usedUnits.Contains(u)));
            remainingUnits.AddRange(cavalryUnits.Skip(cavalryUsed).Where(u => !usedUnits.Contains(u)));

            int remainingIndex = 0;
            foreach (SquadToLoad[] row in new[] { frontRow, middleRow, backRow })
            {
                for (int i = 0; i < 5 && remainingIndex < remainingUnits.Count; i++)
                {
                    if (row[i].UniqueID == null)
                    {
                        row[i] = remainingUnits[remainingIndex];
                        usedUnits.Add(remainingUnits[remainingIndex++]);
                    }
                }
            }

            if (usedUnits.Count < squadsToLoad.Length)
                Debug.LogError($"Not all units were placed in formation. Placed {usedUnits.Count} out of {squadsToLoad.Length}.");

            Vector3 spawnCenter     = spawnPointTransform.position;
            Quaternion facingDirection = spawnPointTransform.rotation;
            Vector3 rightDirection  = spawnPointTransform.right;
            squadIndex = 0;

            SpawnCenteredRow(frontRow,  spawnCenter, rightDirection, facingDirection);
            SpawnCenteredRow(middleRow, spawnCenter - spawnPointTransform.forward * GAP_BETWEEN_SQUADS_Z,     rightDirection, facingDirection);
            SpawnCenteredRow(backRow,   spawnCenter - spawnPointTransform.forward * GAP_BETWEEN_SQUADS_Z * 2, rightDirection, facingDirection);
        }
        private void SpawnCenteredRow(SquadToLoad[] row, Vector3 rowCenter, Vector3 rightDirection, Quaternion facingDirection)
        {
            int unitCount = row.Count(s => s.UniqueID != null);
            if (unitCount == 0) return;

            rightDirection = rightDirection.normalized;

            // Count units excluding cavalry at indices 0 and 4 for centering
            int centeredUnitCount = 0;
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i].UniqueID == null) continue;
                bool isCavalry = TabletopTavernData.Instance.GetUnitSizeFromUnitName(row[i].UnitName) == UnitSize.Cavalry;
                if (!(isCavalry && (i == 0 || i == 4)))
                {
                    centeredUnitCount++;
                }
            }

            // Calculate total width and offset for centered units only
            float totalWidth = centeredUnitCount > 1 ? (centeredUnitCount - 1) * GAP_BETWEEN_SQUADS_X : 0f;
            Vector3 startOffset = -rightDirection * (totalWidth / 2f);

            // Define max flank positions as if the row were 5 units wide
            const int maxRowSize = 5; // Assuming indices 0-4
            float maxWidth = (maxRowSize - 1) * GAP_BETWEEN_SQUADS_X;
            Vector3 leftFlankOffset = -rightDirection * (maxWidth / 2f);
            Vector3 rightFlankOffset = rightDirection * (maxWidth / 2f);

            int j = 0;
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i].UniqueID == null) continue;

                Vector3 spawnPosition;
                bool isCavalry = TabletopTavernData.Instance.GetUnitSizeFromUnitName(row[i].UnitName) == UnitSize.Cavalry;

                    // Special handling for cavalry at indices 0 or 4
                    if (isCavalry && (i == 0 || i == 4))
                    {
                        spawnPosition = rowCenter + (i == 0 ? leftFlankOffset : rightFlankOffset);
                    }
                    else
                    {
                        spawnPosition = rowCenter + startOffset + rightDirection * j * GAP_BETWEEN_SQUADS_X;
                        j++; // Increment only for centered units
                    }

                SpawnSquadFromSaveData(row[i], facingDirection, spawnPosition, squadIndex);
                squadIndex++;
            }
        }
        #region Summoning
        /// <summary>
        /// Spawns a friendly squad mid-battle at the given point, at the unit's normal base unit count.
        /// The squad behaves like any other player squad until killed, but is never persisted: it
        /// carries a fresh GUID that matches no slot in the saved roster, and the post-battle write-back
        /// only ever updates squads it can find in that roster.
        /// </summary>
        public void SummonSquad(UnitName _unitName, Vector3 _position)
        {
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // SpawnSquadFromSaveData dereferences UnitGPUAnimPrefabs.Find(...).Value directly, so a
            // missing preload would throw rather than fail softly. Summoned units are preloaded
            // alongside both armies in LoadBothArmies.
            if(UnitGPUAnimPrefabs.Find(entityManager, _unitName) == null)
            {
                Debug.LogError($"[ArmySpawnManager] Cannot summon {_unitName}, GPU anim prefabs were not preloaded for this battle.");
                return;
            }

            // Feed the same high index to both UnitIndex and _spawnIndex: GetSpawnIndexForPlayer uses
            // UnitIndex in campaign battles and _spawnIndex in custom battles, so this lands on the
            // same squadId either way.
            SquadToLoad summon = new SquadToLoad(_unitName) { UnitIndex = SUMMON_UNIT_INDEX_BASE + summonsThisBattle };
            summonedUniqueIds.Add(summon.UniqueID);
            summonsThisBattle++;

            Team previousTeam = selectedTeam;
            selectedTeam = Team.Player;
            SpawnSquadFromSaveData(summon, playerArmyCenter.rotation, _position, summon.UnitIndex);
            selectedTeam = previousTeam;
        }
        #endregion

        public void SpawnSquadFromSaveData(SquadToLoad squadToLoad, Quaternion rotation, Vector3 spawnPointTransform, int _spawnIndex, int2 widthAndDepth = default)
        {
            // Debug.Log($"Spawning squad {squadToLoad.UnitName} at {spawnPointTransform} for team {selectedTeam}");
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            // EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EntitiesReferences>());
            if(query.CalculateEntityCount() == 0) {
                Debug.LogError("No EntitiesReferences found in the scene. Please add one to load squad data.");
                query.Dispose();
                return;
            }
            EntitiesReferences entitiesReferences = query.GetSingleton<EntitiesReferences>();
            
            int GetSpawnIndexForPlayer()
            {
                int index = squadToLoad.UnitIndex+1;
                if (BattleManager.Instance.BattleSaveManager.IsCustomBattle)
                {
                    index = _spawnIndex + 1;
                }
                return index;
            }
                
            int squadId = selectedTeam == Team.Player ? GetSpawnIndexForPlayer() : -_spawnIndex -1;
            // Debug.Log($"Spawning squad {squadToLoad.UnitName} with id {squadId}");

            List<Entity> entities = new ();
            UnitType unitType = TabletopTavernData.Instance.GetUnitTypeFromUnitName(squadToLoad.UnitName);

            int unitCount = selectedTeam == Team.Player ? (int)(squadToLoad.SquadCurrentHealth * (float)squadToLoad.maxUnitCount / squadToLoad.SquadMaxHealth) : squadToLoad.maxUnitCount;

            unitCount = Mathf.Max(unitCount, 1);

            if (widthAndDepth.x == 0 && widthAndDepth.y == 0)
            {
                widthAndDepth = CalculateWidthAndDepth(unitCount, squadToLoad.UnitName);
            }
            
            //for when units have healed above former width and depth capacity
            int maxUnitsForFormation = widthAndDepth.x * widthAndDepth.y;
            if(unitCount > maxUnitsForFormation)
            {
                //increase the depth to accommodate more units
                int additionalDepth = Mathf.CeilToInt((unitCount - maxUnitsForFormation) / (float)widthAndDepth.x);
                widthAndDepth.y += additionalDepth;
            }

            float spread = TabletopTavernData.Instance.GetUnitSpreadFromUnitName(squadToLoad.UnitName);
            
            List<float3> entityPositions = BattleManager.Instance.PositionDrawer.Formation.GeneratePositionsForSquad(widthAndDepth, unitCount, spread);
            BattleManager.Instance.PositionDrawer.MakePoolOnSpawn();

            SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(squadToLoad.UnitName);

            if (unitType == UnitType.Structure)
            {
                squadStats.HitPointsPerUnit = squadToLoad.HitPointsPerUnit;
            }

            //offset the entity positions by the enemy spawn point and rotate them
            bool isEnemyOutriders = TabletopTavernData.Instance.GetSquadStats(squadToLoad.UnitName).SquadAttributes.Outrider && selectedTeam == Team.Enemy && enemyOutriderSpawnPointsShuffled.Count > 0;
            
            if(BattleManager.Instance.BattleSaveManager.IsGarrisonBattle)
                isEnemyOutriders = false;

            if(isEnemyOutriders && !enemyArmyContainsOutriders)
            {
                enemyArmyContainsOutriders = true;
            }

            //check if there are any squads within x distance of the spawn point
            bool isNearOtherSquads = true;
            int attempts = 4;
            while(isNearOtherSquads && attempts > 0)
            {
                isNearOtherSquads = false;
                if (selectedTeam == Team.Player)
                {
                    using var playerSquads = BattleManager.Instance.SquadManager.RetrieveSquadsMovement(ComponentType.ReadOnly<PlayerSquad>());
                    foreach (var squad in playerSquads)
                    {
                        float distance = math.distance(spawnPointTransform, squad.SquadCenter);
                        // Debug.Log($"Distance to squad is {distance}");
                        if (distance < 10f) // Adjust threshold as needed
                        {
                            isNearOtherSquads = true;
                            break;
                        }
                    }
                }
                if(isNearOtherSquads)
                {
                    // Debug.Log($"Spawn point too close to other squads, adjusting position and retrying. Attempts left: {attempts}");
                    spawnPointTransform -= new Vector3(0, 0, GAP_BETWEEN_SQUADS_Z/2);
                }
                attempts--;
            }
  
            for (int i = 0; i < entityPositions.Count; i++)
            {
                if (isEnemyOutriders)
                {
                    //rotate the rotation 180 degrees for outriders
                    Quaternion newRotation = math.mul(rotation, quaternion.EulerXYZ(new float3(0, math.radians(180), 0)));
                    entityPositions[i] = math.mul(newRotation, entityPositions[i]);

                    //add noise 
                    entityPositions[i] = TabletopTavernData.Instance.GetNoiseFromUnitName(squadToLoad.UnitName, entityPositions[i]);
                    entityPositions[i] += (float3)enemyOutriderSpawnPointsShuffled[0];

                    continue;
                }
                entityPositions[i] = math.mul(rotation, entityPositions[i]);

                //add noise 
                entityPositions[i] = TabletopTavernData.Instance.GetNoiseFromUnitName(squadToLoad.UnitName, entityPositions[i]);
                entityPositions[i] += (float3)spawnPointTransform;
            }

            if(isEnemyOutriders)
            {
                enemyOutriderSpawnPointsShuffled.RemoveAt(0);
            }

            SquadSpawnData squadData = new (){
                squadId = squadId,
                unitType = unitType,
                unitName = squadToLoad.UnitName,
                widthAndDepth = widthAndDepth,
                entityPositions = entityPositions,
                squadRotation = rotation,
                Team = selectedTeam,
            };

            for(int i = 0; i < unitCount && i < entityPositions.Count; i++)
            {
                Entity entity = entityManager.Instantiate(entitiesReferences.basePlayerUnitPrefabEntity);
                float3 correctPosition = GetPointOnTerrain(squadData.entityPositions[i]);

                entityManager.SetComponentData(entity, new LocalTransform {
                    Position =  correctPosition,
                    Rotation = rotation,
                    Scale = 1
                });
                entityManager.SetComponentData(entity, new SetDestination {
                    destinationPosition = correctPosition,
                    squadPosition = correctPosition,
                });
                entityManager.SetComponentData(entity, new RotateUnit { targetRotation = rotation });
                entityManager.AddComponentData(entity, new Unit { 
                    squadId = squadData.squadId, 
                    Team = selectedTeam,
                    unitName = squadData.unitName,
                    unitType = squadData.unitType
                });
                entityManager.AddComponentData(entity, new UnitPosition { 
                    unitIndex = i
                });
                
                if(squadStats.unitSize == UnitSize.SingleUnit && selectedTeam == Team.Player )
                {
                    ModifyHealthOnSpawn modifyHealthOnSpawn = new ModifyHealthOnSpawn { Value = squadToLoad.SquadCurrentHealth };
                    entityManager.AddComponentData(entity, modifyHealthOnSpawn);
                }
                
                AgentLocomotion agentLocomotion = entityManager.GetComponentData<AgentLocomotion>(entity);
                agentLocomotion.Speed = squadStats.Speed/10f;
                entityManager.SetComponentData(entity, agentLocomotion);

                if (unitType != UnitType.Structure)
                {
                    int randomIndex = random.NextInt(0, 9);
                    UnitGPUAnimPrefabs unitAnims = UnitGPUAnimPrefabs.Find(entityManager, squadData.unitName).Value;
                    Entity gpuAnimPrefab = unitAnims.Get(randomIndex);
                    Entity childEntity = entityManager.Instantiate(gpuAnimPrefab);
                    entityManager.AddComponentData(childEntity, new Parent { Value = entity });

                    AnimationDataHolder dat = entityManager.GetComponentData<AnimationDataHolder>(entity);
                    dat.gpuEcsAnimatorEntity = childEntity;
                    bool isDwarf = TabletopTavernData.Instance.GetRaceFromUnitName(squadData.unitName) == Race.DeepstoneHold;
                    dat.RunSpeedThreshold = isDwarf ? 1f : 2f;
                    dat.WalkSpeedThreshold = isDwarf ? 0.5f : 1f;
                    entityManager.SetComponentData(entity, dat);
                    GpuEcsAnimatorControlComponent controlComp = entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(
                        dat.gpuEcsAnimatorEntity);
                    controlComp.transitionSpeed = 0.5f;
                    entityManager.SetComponentData(dat.gpuEcsAnimatorEntity, controlComp);

                    BattleManager.Instance.UnitDebugSetUp.SetUpPositionDebug(entity, entityManager);
                }
                else
                {
                    // Structure: instantiate the artillery GPU anim dummy so all animation
                    // system lookups (GetComponentRW<GpuEcsAnimatorControlComponent>) have a
                    // valid entity instead of Entity.Null
                    Entity dummyAnimEntity = entityManager.Instantiate(entitiesReferences.artilleryGPUAnim);
                    entityManager.AddComponentData(dummyAnimEntity, new Parent { Value = entity });

                    AnimationDataHolder dat = entityManager.GetComponentData<AnimationDataHolder>(entity);
                    dat.gpuEcsAnimatorEntity = dummyAnimEntity;
                    entityManager.SetComponentData(entity, dat);
                    
                    entityManager.AddComponentData(entity, new GarrisonGateUnit());
                    entityManager.AddComponentData(entity, new MissileResistance { DamageMultiplier = 0.5f });

                    Entity arrowPrefab = entitiesReferences.GetProjectileEntityForUnitName(UnitName.Gate, false);
                    entityManager.AddComponentData(entity, new ShootAttack {
                        timer = 0f,
                        timerMax = squadStats.attackCooldown,
                        damageAmount = squadStats.MissileStrength,
                        Range = squadStats.BaseRange,
                        Accuracy = (int)squadStats.attackAccuracy,
                        ProjectileEntity = arrowPrefab
                    });
                    entityManager.AddComponentData(entity, new Target { targetEntity = Entity.Null });
                    entityManager.AddComponentData(entity, new RangedFireModeUnitComponent { FireMode = RangedFireMode.FireAtWill });
                    entityManager.AddBuffer<SFXBufferElement>(entity);
                }

                // entityManager.SetComponentEnabled<IdleAnimations>(entity, true);

                entities.Add(entity);
            }
        
            BattleManager.Instance.SquadManager.RegisterSquad(entities, squadData, squadToLoad.UnitPrestige, squadToLoad.UniqueID, squadToLoad.HitPointsPerUnit, squadToLoad.PrestigeTrait);

            query.Dispose();
        }
        private readonly Dictionary<int, GameObject> _gateGameObjects = new();
        private readonly Dictionary<int, List<string>> _gateDefenderUniqueIds = new();
        public int GetGateIndexForDefenderSquad(int squadId)
        {
            // Reverse-lookup: find which uniqueID maps to this squadId, then check gate lists
            string uniqueId = null;
            foreach (var kv in uniqueIDToSquadId)
                if (kv.Value == squadId) { uniqueId = kv.Key; break; }
            if (uniqueId == null) return -1;

            foreach (var kv in _gateDefenderUniqueIds)
                if (kv.Value.Contains(uniqueId)) return kv.Key;
            return -1;
        }
        public List<int> GetDefenderSquadIdsForGate(int gateIndex)
        {
            if (!_gateDefenderUniqueIds.TryGetValue(gateIndex, out var uniqueIds)) return new();
            var result = new List<int>();
            foreach (var uid in uniqueIds)
                if (uniqueIDToSquadId.TryGetValue(uid, out int squadId)) result.Add(squadId);
            return result;
        }
       [SerializeField]  private GarrisonWallsSO _pendingGateWallsData;

        public GameObject GetGateGameObject(int gateIndex) =>
            _gateGameObjects.TryGetValue(gateIndex, out GameObject go) ? go : null;

        public void SpawnGateSquad(Vector3 position, int gateIndex, GameObject gateGO, GarrisonWallsSO wallsData)
        {
            _gateGameObjects[gateIndex] = gateGO;
            _pendingGateWallsData = wallsData;
            selectedTeam = Team.Enemy;
            SquadToLoad squadToLoad = new SquadToLoad(UnitName.Gate) { UniqueID = $"Gate_{gateIndex}" };
            squadToLoad.HitPointsPerUnit = wallsData.gateStartingHealth;
            // Debug.Log($"Spawning gate squad for gate {gateIndex} with health {wallsData.gateStartingHealth}");
            // Offset into -9001, -9002, … range to avoid collision with normal enemy squad IDs
            SpawnSquadFromSaveData(squadToLoad, Quaternion.Euler(0f, 180f, 0f), position, 9000 + gateIndex);
        }

        public void RecordSquadUniqueID(string uniqueID, int squadId, int unitCount)
        {
            // The final squadId is not predictable at summon time - RegisterSquad overrides it in
            // custom battles - so the summon is matched here by its GUID instead.
            if(summonedUniqueIds.Contains(uniqueID)) summonedSquadIds.Add(squadId);

            if(!uniqueIDToSquadId.ContainsKey(uniqueID)){
                uniqueIDToSquadId.Add(uniqueID, squadId);
            } else {
                uniqueIDToSquadId[uniqueID] = squadId;
            }

            if(!squadIdToUnitCount.ContainsKey(squadId)) {
                squadIdToUnitCount.Add(squadId, unitCount);
            }
            else {
                squadIdToUnitCount[squadId] = unitCount;
            }

            // First registration this battle only - this is the true starting roster size,
            // used later to compute losses without re-deriving unit count from health.
            if (!squadIdToInitialUnitCount.ContainsKey(squadId)) {
                squadIdToInitialUnitCount.Add(squadId, unitCount);
            }
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
        private void OnSquadUpdated(int _squadId, float2 _squadSize)
        {
            squadIdToUnitCount[_squadId] = (int)_squadSize.x;
            // Debug.Log($"Squad {_squadId} has been updated with unit count {_squadSize.x}");
        }
        public void RecordSquadKill(int _squadId)
        {
            if(squadIdKillCounter.TryGetValue(_squadId, out int killCount)) {
                squadIdKillCounter[_squadId] = killCount + 1;
            } else {
                squadIdKillCounter[_squadId] = 1;
            }
    
            BattleManager.Instance.UIManager.SquadBattleInfo.UpdateSquadKillCount();
        }
        public int GetSquadKillCount(int _squadId)
        {
            return squadIdKillCounter.ContainsKey(_squadId) ? squadIdKillCounter[_squadId] : 0;
        }
        public void WithdrawSquad(int squadIndex, int _squadUnitCount)
        {
            if(BattleManager.Instance.BattleSaveManager.IsCustomBattle) return;

            SquadToLoad[] playerArmy= BattleManager.Instance.BattleSaveManager.GetArmyFromSaveData(true).Item1;
            string squadGUID  = GetUnitUniqueIDFromSquadID(squadIndex);
            // Debug.Log($"Withdrawing squad {squadGUID} with current unit count {_squadUnitCount}");

            for (int i = 0; i < playerArmy.Length; i++) {
                if (playerArmy[i].UniqueID == squadGUID) {
                    //current health based on units alive
                    playerArmy[i].SquadCurrentHealth = (int)((float)_squadUnitCount / (float)playerArmy[i].maxUnitCount * (float)playerArmy[i].SquadMaxHealth);
                    Debug.Log($"Recording mid battle withdrawn squad {playerArmy[i].UnitName} with current health count {playerArmy[i].SquadCurrentHealth}");
                    withdrawnSquads.Add(playerArmy[i]);
                    return;
                }
            }
        }
        public void IssueGarrisonReformOrders(List<Entity> squadEntities)
        {
            if (squadEntities == null || squadEntities.Count == 0) return;

            Debug.Log($"[ArmySpawnManager] IssueGarrisonReformOrders: {squadEntities.Count} squad(s), enemyArmyCenter={enemyArmyCenter.position}");
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            List<SquadEntity> meleeSquads   = new();
            List<SquadEntity> rangedSquads  = new();
            List<SquadEntity> cavalrySquads = new();
            List<SquadEntity> largeSquads   = new();

            foreach (Entity entity in squadEntities)
            {
                if (!entityManager.Exists(entity)) continue;
                SquadEntity squadData = entityManager.GetComponentData<SquadEntity>(entity);
                UnitSize size = TabletopTavernData.Instance.GetUnitSizeFromUnitName(squadData.UnitName);
                if (size == UnitSize.Cavalry)   { cavalrySquads.Add(squadData); continue; }
                if (size == UnitSize.Monstrous) { largeSquads.Add(squadData);   continue; }
                switch (TabletopTavernData.Instance.GetUnitTypeFromUnitName(squadData.UnitName))
                {
                    case UnitType.Melee:     meleeSquads.Add(squadData);  break;
                    case UnitType.Hybrid:    meleeSquads.Add(squadData);  break;
                    case UnitType.Ranged:    rangedSquads.Add(squadData); break;
                    case UnitType.Artillery: rangedSquads.Add(squadData); break;
                }
            }

            SquadEntity?[] frontRow  = new SquadEntity?[5];
            SquadEntity?[] middleRow = new SquadEntity?[5];
            SquadEntity?[] backRow   = new SquadEntity?[5];

            int[] centerOut = { 2, 1, 3, 0, 4 };
            int meleeUsed = 0, rangedUsed = 0, cavalryUsed = 0, largeUsed = 0;

            for (int i = 0; i < centerOut.Length && meleeUsed   < meleeSquads.Count;   i++) frontRow[centerOut[i]]  = meleeSquads[meleeUsed++];
            for (int i = 0; i < centerOut.Length && largeUsed   < largeSquads.Count;   i++) middleRow[centerOut[i]] = largeSquads[largeUsed++];

            List<int> midAvail = new();
            for (int pos = 0; pos < 5; pos++) if (middleRow[pos] == null) midAvail.Add(pos);
            midAvail.Sort((a, b) => Mathf.Abs(a - 2).CompareTo(Mathf.Abs(b - 2)));
            for (int i = 0; i < midAvail.Count && rangedUsed < rangedSquads.Count; i++) middleRow[midAvail[i]] = rangedSquads[rangedUsed++];
            for (int pos = 0; pos < 5 && meleeUsed < meleeSquads.Count; pos++)
            { if (middleRow[pos] != null) continue; middleRow[pos] = meleeSquads[meleeUsed++]; }

            for (int i = 0; i < centerOut.Length && cavalryUsed < cavalrySquads.Count; i++) backRow[centerOut[i]] = cavalrySquads[cavalryUsed++];

            List<int> backAvail = new();
            for (int pos = 0; pos < 5; pos++) if (backRow[pos] == null) backAvail.Add(pos);
            backAvail.Sort((a, b) => Mathf.Abs(b - 2).CompareTo(Mathf.Abs(a - 2)));
            for (int i = 0; i < backAvail.Count && rangedUsed < rangedSquads.Count; i++) backRow[backAvail[i]] = rangedSquads[rangedUsed++];

            List<SquadEntity> remaining = new();
            remaining.AddRange(meleeSquads.Skip(meleeUsed));
            remaining.AddRange(largeSquads.Skip(largeUsed));
            remaining.AddRange(rangedSquads.Skip(rangedUsed));
            remaining.AddRange(cavalrySquads.Skip(cavalryUsed));
            int remIdx = 0;
            foreach (SquadEntity?[] row in new[] { frontRow, middleRow, backRow })
                for (int i = 0; i < 5 && remIdx < remaining.Count; i++)
                    if (row[i] == null) row[i] = remaining[remIdx++];

            Vector3 origin  = enemyArmyCenter.position - enemyArmyCenter.forward * 50f;
            Vector3 right   = enemyArmyCenter.right;
            Vector3 fwd     = enemyArmyCenter.forward;
            Quaternion facing = enemyArmyCenter.rotation;

            Debug.Log($"[ArmySpawnManager] Reform rows: melee={meleeSquads.Count} ranged={rangedSquads.Count} cav={cavalrySquads.Count} large={largeSquads.Count}");
            IssueReformRow(frontRow,  origin,                                    right, facing, "front");
            IssueReformRow(middleRow, origin - fwd * GAP_BETWEEN_SQUADS_Z,       right, facing, "middle");
            IssueReformRow(backRow,   origin - fwd * (GAP_BETWEEN_SQUADS_Z * 2), right, facing, "back");
        }

        private void IssueReformRow(SquadEntity?[] row, Vector3 rowCenter, Vector3 rightDir, Quaternion facing, string rowName = "")
        {
            if (row.All(r => r == null)) { Debug.Log($"[ArmySpawnManager] Reform row '{rowName}': empty, skipping"); return; }

            rightDir = rightDir.normalized;
            int centeredCount = 0;
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] == null) continue;
                bool isCav = TabletopTavernData.Instance.GetUnitSizeFromUnitName(row[i].Value.UnitName) == UnitSize.Cavalry;
                if (!(isCav && (i == 0 || i == 4))) centeredCount++;
            }

            float totalWidth  = centeredCount > 1 ? (centeredCount - 1) * GAP_BETWEEN_SQUADS_X : 0f;
            Vector3 startOff  = -rightDir * (totalWidth / 2f);
            Vector3 leftFlank  = -rightDir * (4f * GAP_BETWEEN_SQUADS_X / 2f);
            Vector3 rightFlank =  rightDir * (4f * GAP_BETWEEN_SQUADS_X / 2f);

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int j = 0;
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] == null) continue;
                SquadEntity squadData = row[i].Value;
                bool isCav = TabletopTavernData.Instance.GetUnitSizeFromUnitName(squadData.UnitName) == UnitSize.Cavalry;

                Vector3 slotPos;
                if (isCav && (i == 0 || i == 4))
                    slotPos = rowCenter + (i == 0 ? leftFlank : rightFlank);
                else
                { slotPos = rowCenter + startOff + rightDir * (j * GAP_BETWEEN_SQUADS_X); j++; }

                int unitCount = entityManager.GetBuffer<EntityReferenceBufferElement>(squadData.SelfEntity).Length;
                int2 widthAndDepth = CalculateWidthAndDepth(unitCount, squadData.UnitName);
                entityManager.GetBuffer<QueuedOrder>(squadData.SelfEntity).Clear();
                QueuedOrder queuedOrder = new()
                {
                    Type          = QueuedOrderType.Move,
                    Goal          = (float3)slotPos,
                    Rotation      = (quaternion)facing,
                    WidthAndDepth = widthAndDepth
                };
                ecb.AppendToBuffer(squadData.SelfEntity, queuedOrder);
                Debug.Log($"[ArmySpawnManager] Reform row '{rowName}': squad {squadData.SquadId} ({squadData.UnitName}) -> {slotPos}");
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }

        static int2 CalculateWidthAndDepth(int _unitCount, UnitName _unitName) {
            int unitCount = TabletopTavernData.Instance.GetMaxUnitCountFromUnitName(_unitName);
            int formationWidth = DataTypes.GetFormationWidthFromUnitCount(unitCount);

            int2 widthAndDepth = new (formationWidth, _unitCount / formationWidth);
            if(_unitCount % formationWidth != 0) widthAndDepth.y++;
            return widthAndDepth;
        }
        private void OnDestroy()
        {
            if(!BattleManager.HasInstance) return;
            BattleManager.Instance.SquadManager.OnSquadUpdated -= OnSquadUpdated;
        }
    }
}