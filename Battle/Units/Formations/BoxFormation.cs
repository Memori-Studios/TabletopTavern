using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Linq;

public class BoxFormation : MonoBehaviour
{
    [SerializeField] private List<float3> _pointPositions = new();
    public List<float3> PointPositions => _pointPositions;
    private int2 spawnWidthAndDepth;
    public int2 SpawnWidthAndDepth => spawnWidthAndDepth;

    private Dictionary<int, float3> SE_WidthDepthSpreadDict = new();
    private Dictionary<int, int> selectedSquadEntityAndEntitiesCountDict = new();
    private Dictionary<int, UnitType> SE_UnitTypeDict = new();
    private Dictionary<int, float> SE_SquadCenterXDict = new();
    private float3 middleOffset = new(0.5f, 0, 0.5f);
    private float cachedDistance = 50f;
    private float _noise = 0;
    private const float BUFFER_BETWEEN_SQUADS = 4f;

    public float3 GetNoise(float3 pos)
    {
        var noise = Mathf.PerlinNoise(pos.x * _noise, pos.z * _noise);
        return new float3(noise, 0, noise);
    }
    public void GeneratePointPositions()
    {
        _pointPositions.Clear();

        // Step 1: group squad IDs by type priority, then sort within each group by squad center X
        var typeGroups = new SortedDictionary<int, List<int>>();
        for (int i = 0; i < selectedSquadEntityAndEntitiesCountDict.Keys.Count; i++)
        {
            int id = selectedSquadEntityAndEntitiesCountDict.Keys.ElementAt(i);
            if (!SE_WidthDepthSpreadDict.ContainsKey(id)) continue;
            int priority = SE_UnitTypeDict.TryGetValue(id, out var t) ? GetTypePriority(t) : 0;
            if (!typeGroups.ContainsKey(priority)) typeGroups[priority] = new List<int>();
            typeGroups[priority].Add(id);
        }
        bool useSquadOrder = BattleManager.Instance.GamePhase == GamePhase.Deployment
                          || BattleManager.Instance.GamePhase == GamePhase.SetUp;
        List<int> trueOrder = BattleManager.Instance.SquadManager.TrueSquadOrder;
        foreach (List<int> group in typeGroups.Values)
        {
            if (useSquadOrder)
                group.Sort((a, b) => trueOrder.IndexOf(a).CompareTo(trueOrder.IndexOf(b)));
            else
                group.Sort((a, b) =>
                    SE_SquadCenterXDict.GetValueOrDefault(a, 0f)
                        .CompareTo(SE_SquadCenterXDict.GetValueOrDefault(b, 0f)));
        }

        // Step 2: calculate total Z width per type group (for centering)
        var groupZWidths = new Dictionary<int, float>();
        foreach (var kvp in typeGroups)
        {
            float totalWidth = 0f;
            foreach (int id in kvp.Value)
            {
                int w = (int)SE_WidthDepthSpreadDict[id].x;
                float s = SE_WidthDepthSpreadDict[id].z;
                totalWidth += w * s;
            }
            totalWidth += (kvp.Value.Count - 1) * BUFFER_BETWEEN_SQUADS;
            groupZWidths[kvp.Key] = totalWidth;
        }

        float maxGroupZWidth = 0f;
        foreach (var w in groupZWidths.Values)
            if (w > maxGroupZWidth) maxGroupZWidth = w;

        // Step 3: pre-compute each squad's (xOffset, zOffset) from the type-group layout
        var squadOffsets = new Dictionary<int, float2>();
        float xTypeOffset = 0f;
        foreach (var kvp in typeGroups)
        {
            float groupZWidth = groupZWidths[kvp.Key];
            float zGroupOffset = -(maxGroupZWidth - groupZWidth) / 2f;
            float maxGroupDepthWorld = 0f;

            foreach (int SE_Index in kvp.Value)
            {
                int _unitWidth = (int)SE_WidthDepthSpreadDict[SE_Index].x;
                int _unitDepth = (int)SE_WidthDepthSpreadDict[SE_Index].y;
                float _spread  = SE_WidthDepthSpreadDict[SE_Index].z;

                squadOffsets[SE_Index] = new float2(xTypeOffset, zGroupOffset);

                zGroupOffset -= _unitWidth * _spread + BUFFER_BETWEEN_SQUADS;
                float depthWorld = _unitDepth * _spread;
                if (depthWorld > maxGroupDepthWorld) maxGroupDepthWorld = depthWorld;
            }

            xTypeOffset -= maxGroupDepthWorld + BUFFER_BETWEEN_SQUADS;
        }

        // Step 4: emit positions in TrueSquadOrder so the flat index matches entity consumption order
        for (int i = 0; i < selectedSquadEntityAndEntitiesCountDict.Keys.Count; i++)
        {
            int SE_Index = selectedSquadEntityAndEntitiesCountDict.Keys.ElementAt(i);
            if (!SE_WidthDepthSpreadDict.ContainsKey(SE_Index))
            {
                Debug.LogWarning($"SE_WidthDepthSpreadDict does not contain key {SE_Index}");
                continue;
            }
            if (!squadOffsets.ContainsKey(SE_Index)) continue;

            int _unitWidth = (int)SE_WidthDepthSpreadDict[SE_Index].x;
            int _unitDepth = (int)SE_WidthDepthSpreadDict[SE_Index].y;
            float _spread  = SE_WidthDepthSpreadDict[SE_Index].z;
            int _unitCount = selectedSquadEntityAndEntitiesCountDict[SE_Index];
            float2 offset  = squadOffsets[SE_Index];

            if (_unitWidth <= 0 || _unitDepth <= 0 || _unitCount <= 0) continue;

            for (var x = 0; x < _unitDepth; x++)
            {
                int unitsInRow = (x == _unitDepth - 1 && _unitCount % _unitWidth != 0) ? _unitCount % _unitWidth : _unitWidth;
                float rowOffset = (_unitWidth - unitsInRow) * 0.5f;

                for (var z = 0; z < unitsInRow; z++)
                {
                    if (x * _unitWidth + z >= _unitCount) continue;

                    var pos = new float3(-x, 0, -z - rowOffset);
                    pos -= middleOffset;
                    pos += GetNoise(pos);
                    pos *= _spread;
                    pos += new float3(offset.x, 0, offset.y);

                    _pointPositions.Add(pos);
                }
            }
        }

        BattleManager.Instance.PositionDrawer.SetUnitPointsPositions();
    }
    public List<float3> GeneratePositionsForSquad(int2 widthAndDepth, int unitCount, float spread)
    {
        List<float3> positions = new();

        int width = widthAndDepth.x;   // X axis
        int depth = widthAndDepth.y;   // Z axis

        float halfWidth = (width - 1) * 0.5f;
        float halfDepth = (depth - 1) * 0.5f;

        int placed = 0;

        for (int row = 0; row < depth; row++)
        {
            int remaining = unitCount - placed;
            if (remaining <= 0)
                break;

            int unitsInThisRow = math.min(width, remaining);
            float rowOffset = (width - unitsInThisRow) * 0.5f;

            int visualRow = (depth - 1) - row;

            for (int col = 0; col < unitsInThisRow; col++)
            {
                float x = (col + rowOffset) - halfWidth;
                float z = visualRow - halfDepth;

                var pos = new float3(x, 0, z);
                pos += GetNoise(pos);

                positions.Add(pos * spread);
                placed++;
            }
        }

        return positions;
    }

    public void SetUnitCounts(Dictionary<int, int> _selectedSquadEntityAndEntitiesCountDict)
    {
        selectedSquadEntityAndEntitiesCountDict.Clear();
        for (int i = 0; i < BattleManager.Instance.SquadManager.TrueSquadOrder.Count; i++)
        {
            int squadId = BattleManager.Instance.SquadManager.TrueSquadOrder[i];
            if (_selectedSquadEntityAndEntitiesCountDict.ContainsKey(squadId))
            {
                int count = _selectedSquadEntityAndEntitiesCountDict[squadId];
                selectedSquadEntityAndEntitiesCountDict.Add(squadId, count);
            }
        }
    }
    public void CalculateUnitDepthAndWidthForSpawn(int unitCount, float _spread)
    {
        // Debug.Log($"CalculateUnitDepthAndWidthForSpawn: {unitCount}, {_spread}");
        selectedSquadEntityAndEntitiesCountDict.Clear();
        selectedSquadEntityAndEntitiesCountDict.Add(0, unitCount);
        SE_UnitTypeDict.Clear();

        int width = _spread switch
        {
            TabletopTavernConstants.InfantrySpread => unitCount > 48 ? 14 : 12,
            TabletopTavernConstants.CavalrySpread => 8,
            TabletopTavernConstants.MonsterSpread => unitCount > 8 ? 6 : 4,
            TabletopTavernConstants.ArtillerySpread => 3,
            TabletopTavernConstants.SingleUnitSpread => 1,
            _ => 0,
        };
        int depth = width > 0 ? Mathf.CeilToInt((float)unitCount / width) : 0;
        spawnWidthAndDepth = new int2(width, depth);

        SE_WidthDepthSpreadDict.Clear();
        SE_WidthDepthSpreadDict.Add(0, new float3(SpawnWidthAndDepth.x, SpawnWidthAndDepth.y, _spread));

        GeneratePointPositions();
    }
    public void CalculateUnitDepthAndWidth(float _distance)
    {
        int selectedSquadCount = selectedSquadEntityAndEntitiesCountDict.Count;
        if(selectedSquadCount == 0) return;

        cachedDistance = _distance;

        // Build per-type-group squad count so distance is split by row, not total
        var typeGroupSizes = new Dictionary<int, int>();
        foreach (int id in selectedSquadEntityAndEntitiesCountDict.Keys)
        {
            if (!SE_WidthDepthSpreadDict.ContainsKey(id)) continue;
            int priority = SE_UnitTypeDict.TryGetValue(id, out var t) ? GetTypePriority(t) : 0;
            typeGroupSizes[priority] = typeGroupSizes.TryGetValue(priority, out int c) ? c + 1 : 1;
        }

        foreach (KeyValuePair<int, int> pair in selectedSquadEntityAndEntitiesCountDict)
        {
            int unitCount = pair.Value;
            if (unitCount <= 0) continue;
            if (!SE_WidthDepthSpreadDict.ContainsKey(pair.Key)) continue;
            float _spread = SE_WidthDepthSpreadDict[pair.Key].z;

            int priority = SE_UnitTypeDict.TryGetValue(pair.Key, out var ut) ? GetTypePriority(ut) : 0;
            int groupSize = typeGroupSizes.TryGetValue(priority, out int gs) ? gs : 1;
            float perSquadDistance = (_distance - BUFFER_BETWEEN_SQUADS * (groupSize - 1)) / groupSize;

            int width = 1;

            if(perSquadDistance < _spread)
            {
                width = 1;
            }
            else
            {
                width = Mathf.CeilToInt(perSquadDistance / _spread);
            }

            if (_spread == TabletopTavernConstants.InfantrySpread || _spread == TabletopTavernConstants.CavalrySpread || _spread == TabletopTavernConstants.MonsterSpread)
            {
                if (width > unitCount / 2)
                    width = unitCount / 2;

                if (width < 4)
                    width = 4;
            }
            else if (_spread == TabletopTavernConstants.ArtillerySpread)
            {
                if (width > unitCount / 3)
                    width = unitCount / 3;

                if (width < 3)
                    width = 3;
            }
            else if (_spread == TabletopTavernConstants.SingleUnitSpread)
            {
                width = 1;
            }

            if (width > unitCount)
                width = unitCount;

            int depth = Mathf.CeilToInt((float)unitCount / (float)width);

            SE_WidthDepthSpreadDict[pair.Key] = new float3(width, depth, _spread);
        }

        GeneratePointPositions();
    }
    public void SetWidthAndDepthDict(Dictionary<int, float3> _SE_WidthDepthDict)
    {
        SE_WidthDepthSpreadDict = _SE_WidthDepthDict;
        foreach (KeyValuePair<int, float3> pair in SE_WidthDepthSpreadDict)
        {
            // Debug.Log($"SetWidthAndDepthDict: Squad {pair.Key} has width {pair.Value.x}, depth {pair.Value.y}, spread {pair.Value.z}");
        }
        GeneratePointPositions();
    }
    public void SetUnitTypes(Dictionary<int, UnitType> unitTypeDict)
    {
        SE_UnitTypeDict = unitTypeDict;
    }
    public void SetSquadCenterXDict(Dictionary<int, float> squadCenterXDict)
    {
        SE_SquadCenterXDict = squadCenterXDict;
    }
    // Hybrid gets its own rank between the melee line and the archers.
    // Mage shares the artillery rank rather than taking one behind it: its cast range is only
    // archer-equivalent, so a rank further back would leave it out of range exactly when the
    // archers are in range, and it would walk itself forward out of the back line. If mage range
    // is ever pushed above an archer's, moving it to 4 becomes the correct call.
    private static int GetTypePriority(UnitType t) => t switch
    {
        UnitType.Melee     => 0,
        UnitType.Hybrid    => 1,
        UnitType.Ranged    => 2,
        UnitType.Artillery => 3,
        UnitType.Mage      => 3,
        _                  => 0,
    };
    public int2 GetWidthAndDepth(int id)
    {
        if (!SE_WidthDepthSpreadDict.ContainsKey(id))
        {
            Debug.LogWarning($"GetWidthAndDepth: no entry for squad {id}, returning 1x1");
            return new int2(1, 1);
        }
        return new int2((int)SE_WidthDepthSpreadDict[id].x, (int)SE_WidthDepthSpreadDict[id].y);
    }
}