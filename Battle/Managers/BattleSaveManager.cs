using UnityEngine;
using System.Collections.Generic;
using Memori.SaveData;
using Memori.Scenes;
using TJ.Town;

namespace TJ.Battle
{
    public class BattleSaveManager : MonoBehaviour
    {
        [SerializeField] private bool editorCustomBattle;
        private bool isCustomBattle;
        public bool IsCustomBattle => isCustomBattle;
        private bool isGarrisonBattle;
        public bool IsGarrisonBattle => isGarrisonBattle;
        // A custom battle has no town, so its wall size lives here; -1 means no walls.
        private int customGarrison = -1;
        public int CustomGarrison => customGarrison;
        public TownSize GarrisonTownSize => isCustomBattle ? (TownSize)customGarrison : SaveDataHandler.Load().townData.townSize;
        private int seed;
        public int Seed => seed;
        public int EnemiesToSpawn;
        public int PlayerSquadsToSpawn;
        
        public void Load()
        {
            CampaignSaveData saveData = SaveDataHandler.Load();
            seed = saveData.seed + saveData.GetSelectedNodeIndex();

            // Both dev-boot custom paths must mark the player save, or weather and SpellTestMode read a stale campaign flag.
            if (SceneHandler.Instance.EditorOverride == SceneHandler.EditorOverrides.TavernBattle
                && !SceneHandler.Instance.EditorLoadCampaignBattle)
            {
                editorCustomBattle = true;
                SetCustomBattle();
            }
            isCustomBattle = SceneHandler.Instance.EditorLoadCustomBattleSaveData || SaveDataHandler.LoadPlayerSaveData().customBattle;
            isGarrisonBattle = saveData.townData != null && saveData.townData.townInteractionStatus == TownInteractionStatus.GarrisonBattleStarted;

            if (isCustomBattle) SetCustomGarrison(SaveDataHandler.LoadCustomBattleSaveData().customBattleGarrison);
        }
        public void SetCustomGarrison(int townSize)
        {
            customGarrison = townSize;
            isGarrisonBattle = townSize >= 0;
        }
        public (SquadToLoad[], Dictionary<string, SquadBattlePosition>) GetArmyFromSaveData(bool requestingPlayerArmy)
        {
            CampaignSaveData saveData = SaveDataHandler.Load();
            SquadToLoad[] armyToLoad = null;
            List<SquadBattlePosition> squadBattlePositionsSaved = new ();

            if(isCustomBattle)
            {
                CustomBattleSaveData customBattleData = SaveDataHandler.LoadCustomBattleSaveData();
                if(requestingPlayerArmy)
                {
                    armyToLoad = customBattleData.playerCustomBattleArmy;
                    squadBattlePositionsSaved = customBattleData.playerCustomBattleSquadBattlePositions;
                }
                else
                {
                    armyToLoad = customBattleData.enemyCustomBattleArmy;
                    squadBattlePositionsSaved = customBattleData.enemyCustomBattleSquadBattlePositions;
                }
            }
            else
            {
                if(requestingPlayerArmy)
                {
                    armyToLoad = saveData.playerArmy;
                    squadBattlePositionsSaved = saveData.playerSquadBattlePositions;
                }
                else
                {
                    armyToLoad = saveData.enemyArmy;
                    squadBattlePositionsSaved = new ();
                }
            }
            
            List<SquadToLoad> temp = new ();
            if(armyToLoad == null) return (temp.ToArray(), new ());//for custom battle screwing up

            if(!requestingPlayerArmy)
            {
                for (int i = 0; i < armyToLoad.Length; i++) {
                    temp.Add(armyToLoad[i]);
                }
            } 
            else 
            {
                for (int i = 0; i < armyToLoad.Length; i++) {
                    // Custom battles have no reserves; a campaign army also brings its goblin reserves.
                    bool fights = isCustomBattle
                        ? armyToLoad[i].UnitIndex != -1 && armyToLoad[i].UnitIndex < 10
                        : SaveDataHandler.TakesTheField(armyToLoad[i]);
                    if (fights) {
                        temp.Add(armyToLoad[i]);
                    }
                }
            }
            
            //create dictionary of squad battle positions with UniqueID as key
            Dictionary<string, SquadBattlePosition> playerSquadBattlePositions = new ();

            foreach (SquadBattlePosition position in squadBattlePositionsSaved)
            {
                playerSquadBattlePositions[position.SquadUniqueID] = position;
            }

            if(requestingPlayerArmy)
            {
                PlayerSquadsToSpawn = temp.Count;
            }
            else
            {
                EnemiesToSpawn = temp.Count * -1;
            }
            return (temp.ToArray(), playerSquadBattlePositions);
        }
        [ContextMenu("Set Custom Battle")]  
        public void SetCustomBattle()
        {
            PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
            saveData.customBattle = editorCustomBattle;
            SaveDataHandler.SavePlayerSaveData(saveData);
        }
    }
}