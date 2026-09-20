using UnityEngine;
using TJ;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Memori.SaveData
{
    [System.Serializable] public struct SquadToLoad 
    { 
        public UnitName UnitName;
        public string UniqueID;
        public int UnitPrestige;
        public UnitAttribute PrestigeTrait;

        //Sorting
        public int UnitIndex;
        public bool isEmptySquad;

        // Health
        public int HitPointsPerUnit;
        public int maxUnitCount; 
        public int SquadCurrentHealth;
        public int SquadMaxHealth;

        public SquadToLoad(UnitName _unitName, int _prestige = 0, int _unitIndex = -1, bool _isEmptySquad = false, float _modifiedHealthValueByAmount = 1)
        {
            isEmptySquad = _isEmptySquad;
            UnitName = _unitName;
            UniqueID = System.Guid.NewGuid().ToString();
            UnitPrestige = _prestige;
            PrestigeTrait = UnitAttribute.None;
            UnitIndex = _unitIndex;
            maxUnitCount = TabletopTavernData.Instance.GetBaseUnitCount(_unitName);
            HitPointsPerUnit = TabletopTavernData.Instance.GetHitPointsPerUnit(_unitName);
            SquadMaxHealth = maxUnitCount * HitPointsPerUnit;
            SquadCurrentHealth = (int)(SquadMaxHealth * _modifiedHealthValueByAmount);
        }
    }
    [System.Serializable] public struct SquadBattlePosition
    {
        public string SquadUniqueID;
        public Vector3 Position;
        public Quaternion Rotation;
        public int2 SquadWidthAndDepth;
    }
    [System.Serializable] public class SavedSquadGroup
    {
        public int slotIndex;
        public List<string> squadUniqueIds = new List<string>();
        public bool isLocked;
    }

    [System.Serializable] [CreateAssetMenu(fileName = "ArmySaveData", menuName = "GC_ScriptableObjects/ArmySaveData", order = 1)]
    public class ArmySaveData : ScriptableObject
    {
        // public SquadToLoad[] squadToLoads; 
        public UnitName[] SquadsInArmy; 
    }
}

