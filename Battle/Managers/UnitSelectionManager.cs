using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using Memori.Audio;
using TJ;
using TJ.Map;
using Memori.Input;
using Memori.Notifications;
using TJ.IrregularGrid;
using System.Linq;

public class UnitSelectionManager : MonoBehaviour
{
    public static UnitSelectionManager Instance { get; private set; }

    [Header("Selected Squads")]
    [SerializeField] private List<int> selectedSquadIds = new();
    public List<int> SelectedSquadIds => selectedSquadIds;
    public List<UnitName> SelectedSquadUnitNames;
    public delegate void SelectedSquadsChanged(List<int> _selectedSquadIds);
    public event SelectedSquadsChanged OnSelectedSquadsChanged;
    public Dictionary<int, int> SelectedSquadEntityAndEntitiesCountDict => selectedSquadEntityAndEntitiesCountDict;
    private Dictionary<int, int> selectedSquadEntityAndEntitiesCountDict = new();

    [Header("Hovering Squads")]
    public int PreviousHoveredSquad => previousHoveredSquad;
    private int previousHoveredSquad = 0;
    private int squadToAttack = 0;
    private bool unitsAreSelected;
    private bool EnemySquadsSelected => selectedSquadIds.Count > 0 && selectedSquadIds.All(id => id < 0);
    private bool IsHoveringEnemySquad => previousHoveredSquad < 0;
    private int _lastSelectSFXFrame = -1;
    private float timer = 0;
    private Vector2 hoverStartCursorPosition;
    private bool attackIfNotDragged = false;
    private List<int> selectionAreaHoveredSquads = new();
    public delegate void HoveredSquadsChanged(List<int> _hoveredSquadIds);
    public event HoveredSquadsChanged OnHoverSquadsChanged;

    private PositionDrawer positionDrawer;
    private SquadManager squadManager;
    private UnitPositioningManager unitPositioningManager;
    private BiomeCollider cachedBiomeCollider;
    private BattlefieldBonusGameObject cachedBattlefieldBonus;
    private BattleInputManager battleInputManager;

    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        positionDrawer = BattleManager.Instance.PositionDrawer;
        squadManager = BattleManager.Instance.SquadManager;
        unitPositioningManager = BattleManager.Instance.UnitPositioningManager;
        selectedSquadEntityAndEntitiesCountDict = new Dictionary<int, int>();
        battleInputManager = BattleInputManager.Instance;
        OnSelectedSquadsChanged += OnSelectedSquadsChangedHandler;
        squadManager.OnDestroyedSquad += RemoveSquadFromSelection;
        BattleManager.Instance.OnSquadBrokenEvent += RemoveSquadFromSelection;
    }
    private void Update()
    {
        if (battleInputManager.IsRearrangingSquads) return;

        if (SettingsManager.Instance.SettingsPanelOpen) return;

        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            battleInputManager.HandleSelectionArea();
            return;
        }

        if (BattleInputManager.Instance.AddingToSelectedUnits && battleInputManager.CursorMode == CursorMode.Free) BattleManager.Instance.SetCursorMode(CursorMode.UnitsSelected);

        if (battleInputManager.LeftClickDownThisFrame)
        {
            if (battleInputManager.CursorMode == CursorMode.SpawnSquad)
            {
                positionDrawer.TurnOff();
                BattleManager.Instance.SetCursorMode(CursorMode.Free);
                BattleManager.Instance.UIManager.HideSpawnErrorMessage();
            }
        }

        if (battleInputManager.CursorMode == CursorMode.SpawnSquad)
        {
            battleInputManager.HandleSpawningSquadCursorMode();
            return;
        }

        if (battleInputManager.CursorMode == CursorMode.CastSpell)
        {
            //hover detection still needed so Squad-targeted spells know what's under the cursor,
            //but selection-area clicks must not run while a spell is armed (SpellManager owns the click)
            battleInputManager.HandleHoverSquad();
            return;
        }

        if (battleInputManager.RepositioningSelectedUnits)
        {
            HoverSquad(0, true);
            HandleSelectedUnits();
            return;
        }

        battleInputManager.HandleSelectionArea();

        battleInputManager.HandleHoverSquad();

        if (battleInputManager.CursorMode.Equals(CursorMode.UnitsSelected) &&
            IsHoveringEnemySquad &&
            !EnemySquadsSelected &&
            BattleManager.Instance.ArmySpawnManager.EnemyArmyDeployed &&
            !IsOutriderSquadDuringDeployment(previousHoveredSquad) &&
            battleInputManager.RightClickDownThisFrame)
        {
            attackIfNotDragged = true;
            squadToAttack = previousHoveredSquad;
            battleInputManager.SetInitialMousePositions(MouseWorldPosition.Instance.GetWorldPosition());
            battleInputManager.SetMinimumDistanceFromInitialClickHit(false);
            return;
        }
        else if(attackIfNotDragged && battleInputManager.MinimumDistanceFromInitialClickHit)
        {
            attackIfNotDragged = false;
        }
        else if(attackIfNotDragged && battleInputManager.RightClickHeldDown)
        {
            HandleWaitForDrag();
            return;
        }

        if (attackIfNotDragged && battleInputManager.RightClickUpThisFrame)
        {
            attackIfNotDragged = false;
            if(squadToAttack == 0)
            {
                //TODO: fix this
                Debug.LogError($"Squad to attack is 0, cannot issue attack command");
                return;
            }
            unitPositioningManager.QueueSquadCommand(SquadCommand.Attack, InputHandler.Instance.QueueOrder, squadToAttack);
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.GiveAttackOrders);
            return;
        }

        HandleSelectedUnits();

        HandleHoverBattlefieldBonus();
    }


    private void HandleWaitForDrag()
    {
        if (battleInputManager.MinimumDistanceFromInitialClick())
        {
            if (!battleInputManager.MinimumDistanceFromInitialClickHit)
            {
                RefreshSelectedUnitCounts();
                BattleManager.Instance.SetCursorMode(CursorMode.MouseDown);
                positionDrawer.MovePositionToMouse(battleInputManager.InitialMouseWorldPosition);
                positionDrawer.TurnOn(
                    battleInputManager.InitialMouseWorldPosition,
                    selectedSquadEntityAndEntitiesCountDict
                );
                battleInputManager.SetMinimumDistanceFromInitialClickHit(true);
                battleInputManager.SetInitialSquadPosition(true);
            }
        }
    }

    #region Positioning Selected Units
    private void HandleSelectedUnits()
    {
        if (!unitsAreSelected) return;
        if (EnemySquadsSelected) return;

        Team team = EnemySquadsSelected ? Team.Enemy : Team.Player;

        //check if all selected units are outriders

        bool outrider = true;
        foreach (UnitName squadUnitName in SelectedSquadUnitNames)
        {
            SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(squadUnitName);

            //Unbound by Chivalry: All units gain the [Outrider] ability
            if(HeroBonusManager.Instance.ActiveHeroID == 9)
            {
                squadStats.SquadAttributes.Outrider = true;
            }

            if (!squadStats.SquadAttributes.Outrider)
            {
                outrider = false;
                break;
            }
        }

        BattleManager.Instance.PositionDrawer.ConfirmValidityOfPositions(team, outrider);

        if (InputHandler.Instance.RepositioningSelectedUnits && battleInputManager.CursorMode != CursorMode.Reposition && !battleInputManager.RepositionCancelled)
        {
            BattleManager.Instance.SetCursorMode(CursorMode.Reposition);
            return;
        }
        else if (!InputHandler.Instance.RepositioningSelectedUnits && battleInputManager.CursorMode == CursorMode.Reposition)
        {
            battleInputManager.StopRotation();
            BattleManager.Instance.SetCursorMode(CursorMode.UnitsSelected);
            positionDrawer.TurnOff();
            return;
        }
        else if (battleInputManager.RepositioningSelectedUnits && battleInputManager.CursorMode.Equals(CursorMode.Reposition))
        {
            battleInputManager.HandleRepositionUnits();
            return;
        }


        if (battleInputManager.LeftClickDownThisFrame && !battleInputManager.AddingToSelectedUnits)
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            positionDrawer.TurnOff();
            DeselectAllSquadsForNewSelectionArea();
            return;
        }

        if (battleInputManager.RightClickDownThisFrame && !battleInputManager.RepositioningSelectedUnits)
        {
            HandleRotateFormation();
        }

        if (battleInputManager.RightClickUpThisFrame && battleInputManager.CursorMode.Equals(CursorMode.MouseDown))
        {
            BattleManager.Instance.SetCursorMode(CursorMode.UnitsSelected);

            if (!BattleManager.Instance.PositionDrawer.ValidPositions)
            {
                //the order is dropped here, so say why - failing silently reads as unresponsive input
                NotificationManager.Instance.ErrorNotification(BattleManager.Instance.PositionDrawer.PositionErrorMessage);
                positionDrawer.TurnOff();
                return;
            }

            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.RepositionUnit);
            unitPositioningManager.IssueSquadMoveCommand(true, InputHandler.Instance.QueueOrder);
        }

        if (battleInputManager.CursorMode.Equals(CursorMode.MouseDown))
        {
            if (!battleInputManager.SettingInitialSquadPosition)
            {
                battleInputManager.SetInitialSquadPosition(true);
                // Debug.Log($"Minimum distance from initial click not hit, holding previous rotation");
                HoldPreviousRotation();
                positionDrawer.Formation.GeneratePointPositions();
                positionDrawer.MovePositionToMouse(GetMousePositionOffsetByFormationCenter());
                // Debug.Log($"Moving position to mouse: {GetMousePositionOffsetByFormationCenter()}");
            }

            if (battleInputManager.MinimumDistanceFromInitialClick(battleInputManager.ReformSelectionRotationDeadzone))
            {
                if (!battleInputManager.MinimumDistanceFromInitialClickHit)
                {
                    // Debug.Log($"Minimum distance from initial click hit, moving position to mouse");
                    battleInputManager.SetMinimumDistanceFromInitialClickHit(true);
                    //reset the position parent to the mouse position
                    positionDrawer.MovePositionToMouse(battleInputManager.InitialMouseWorldPosition);
                }

                // Debug.Log($"Minimum distance from initial click hit, rotating formation to mouse");

                battleInputManager.RotateFormationToMouse();
                battleInputManager.CalculateMouseDraggedDistance();
            }
        }
    }
    private void HandleRotateFormation()
    {
        RefreshSelectedUnitCounts();
        battleInputManager.SetAngle(-90f);
        positionDrawer.SetLookRotation(Quaternion.Euler(0, battleInputManager.Angle, 0));

        BattleManager.Instance.SetCursorMode(CursorMode.MouseDown);
        battleInputManager.SetInitialMousePositions(MouseWorldPosition.Instance.GetWorldPosition());

        //need to update the unit counts here
        positionDrawer.TurnOn(
            MouseWorldPosition.Instance.GetWorldPosition(),
            selectedSquadEntityAndEntitiesCountDict
        );
    }
    #endregion

    #region Selection
    public void AttemptSquadSelect(int _squadId, bool _addingToSelection)
    {
        if (BattleManager.Instance.GamePhase == GamePhase.SetUp) return;
        // Debug.Log($"AttemptSquadSelect: {_squadId} _addingToSelection: {_addingToSelection}");
        if (_squadId < 0 && selectedSquadIds.Any(id => id > 0)) return;
        if (_squadId > 0 && _addingToSelection && selectedSquadIds.Any(id => id < 0)) return;

        //return if nothing changes
        if (selectedSquadIds.Contains(_squadId) == _addingToSelection) return;

        SelectSquad(_squadId, _addingToSelection);
    }
    /// <summary>
    /// Gets the selected units grouped by their squad IDs.
    /// </summary>
    /// <returns></returns>
    //TODO: fix this maybe, needed for teleport formation and repositioning
    public Dictionary<int, List<Entity>> GetSelectedSquadIDsAndAllUnits()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        List<int> _selectedSquadIds = selectedSquadIds;

        Dictionary<int, List<Entity>> unitsInSelectedSquads = new();

        for (int i = 0; i < _selectedSquadIds.Count; i++)
        {
            unitsInSelectedSquads.Add(_selectedSquadIds[i], squadManager.GetEntitiesFromSquad(_selectedSquadIds[i]));
        }

        // sort values of selected Entity Dict by unit index
        foreach (KeyValuePair<int, List<Entity>> kvp in unitsInSelectedSquads)
        {
            kvp.Value.Sort((a, b) =>
            {
                if (!entityManager.Exists(a) || !entityManager.Exists(b)) return 0;
                UnitPosition unitPositionA = entityManager.GetComponentData<UnitPosition>(a);
                UnitPosition unitPositionB = entityManager.GetComponentData<UnitPosition>(b);
                return unitPositionA.unitIndex.CompareTo(unitPositionB.unitIndex);
            });
        }

        return unitsInSelectedSquads;
    }
    public List<Entity> GetAllUnitPositionsForSquadID(int squadId)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var units = squadManager.GetEntitiesFromSquad(squadId)?
            .Where(e => em.Exists(e) && em.HasComponent<UnitPosition>(e))
            .OrderBy(e => em.GetComponentData<UnitPosition>(e).unitIndex)
            .ToList() 
            ?? new List<Entity>();

        return units;
    }
    public List<SquadEntity> GetSelectedSquadEntities()
    {
        // Debug.Log($"GetSelectedSquadEntities");
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery entityQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<SquadEntity>()
            .WithAbsent<BrokenSquadTag>()
            .Build(entityManager)
        ;

        NativeArray<Entity> entityArray = entityQuery.ToEntityArray(Allocator.Temp);
        List<SquadEntity> selectedSquadEntities = new();
        for (int i = 0; i < entityArray.Length; i++)
        {
            SquadEntity squadEntity = entityManager.GetComponentData<SquadEntity>(entityArray[i]);
            if (squadEntity.IsSelected)
            {
                selectedSquadEntities.Add(squadEntity);
            }
        }
        entityArray.Dispose();
        entityQuery.Dispose();
        return selectedSquadEntities;
    }
    public void SquadCardSelected(int _squadId)
    {
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (shiftHeld && selectedSquadIds.Count > 0)
        {
            SelectSquadRange(_squadId);
            return;
        }

        if (!battleInputManager.AddingToSelectedUnits)
        {
            ClearSelectedSquads();
            AttemptSquadSelect(_squadId, true);
            return;
        }

        ToggleSquadSelect(_squadId);
    }
    private void SelectSquadRange(int _squadId)
    {
        List<int> trueOrder = squadManager.TrueSquadOrder;
        int clickedIndex = trueOrder.IndexOf(_squadId);
        if (clickedIndex < 0) return;

        int minIndex = int.MaxValue;
        int maxIndex = int.MinValue;
        foreach (int id in selectedSquadIds)
        {
            int idx = trueOrder.IndexOf(id);
            if (idx < 0) continue;
            if (idx < minIndex) minIndex = idx;
            if (idx > maxIndex) maxIndex = idx;
        }

        if (minIndex == int.MaxValue)
        {
            selectedSquadIds.Add(_squadId);
            SortSelectedSquads();
            return;
        }

        int rangeMin = Mathf.Min(clickedIndex, minIndex);
        int rangeMax = Mathf.Max(clickedIndex, maxIndex);

        selectedSquadIds.Clear();
        for (int i = rangeMin; i <= rangeMax; i++)
            selectedSquadIds.Add(trueOrder[i]);

        SortSelectedSquads();
    }
    public void ToggleSquadSelect(int squadId)
    {
        bool isSelected = selectedSquadIds.Contains(squadId);
        AttemptSquadSelect(squadId, !isSelected);
    }
    public void SelectSquad(int _squadId, bool _addingToSelection)
    {
        // Debug.Log($"SelectSquad: {_squadId} _addingToSelection: {_addingToSelection}");
        if (_addingToSelection)
        {
            if (!selectedSquadIds.Contains(_squadId))
                selectedSquadIds.Add(_squadId);
        }
        else
        {
            if (selectedSquadIds.Contains(_squadId))
                selectedSquadIds.Remove(_squadId);
        }
        SortSelectedSquads();
    }
    public void SelectSquads(List<int> _selectedSquadIds)
    {
        if (battleInputManager.AddingToSelectedUnits)
        {
            for (int i = 0; i < _selectedSquadIds.Count; i++)
            {
                if (!selectedSquadIds.Contains(_selectedSquadIds[i]))
                {
                    selectedSquadIds.Add(_selectedSquadIds[i]);
                }
            }
        }
        else
        {
            selectedSquadIds = new List<int>(_selectedSquadIds);
        }

        SortSelectedSquads();
    }
    public void SelectAllPlayerSquads()
    {
        using var playerSquads = squadManager.RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());
        List<int> allPlayerSquadIds = new List<int>(playerSquads.Length);
        foreach (var squad in playerSquads)
            allPlayerSquadIds.Add(squad.SquadId);
        SelectSquads(allPlayerSquadIds);
    }
    public void SelectAllPlayerSquadsWithName(UnitName unitName)
    {
        using var playerSquads = squadManager.RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());
        List<int> matchingIds = new(playerSquads.Length);
        foreach (var squad in playerSquads)
        {
            if (squad.UnitName == unitName)
                matchingIds.Add(squad.SquadId);
        }
        SelectSquads(matchingIds);
    }
    public void ClearSelectedSquads()
    {
        selectedSquadIds.Clear();
    }
    public void DeselectAllSquadsForNewSelectionArea()
    {
        DeselectAllSquads();
    }
    public void DeselectSquadsBeforeDeletionOrSpawning()
    {
        DeselectAllSquads();
    }
    private void DeselectAllSquads()
    {
        // Debug.Log($"Deselect all squads");
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery entityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Selected, Hovered>().Build(entityManager);
        NativeArray<Entity> entityArray = entityQuery.ToEntityArray(Allocator.Temp);
        //deslect all units
        NativeArray<Selected> selectedArray = entityQuery.ToComponentDataArray<Selected>(Allocator.Temp);
        NativeArray<Hovered> hoveredArray = entityQuery.ToComponentDataArray<Hovered>(Allocator.Temp);

        for (int i = 0; i < entityArray.Length; i++)
        {
            entityManager.SetComponentEnabled<Selected>(entityArray[i], false);

            Selected selected = selectedArray[i];
            selected.onDeselected = true;
            entityManager.SetComponentData(entityArray[i], selected);

            Hovered hovered = hoveredArray[i];
            hovered.onDeselected = true;
            entityManager.SetComponentData(entityArray[i], hovered);
        }

        selectedSquadIds.Clear();
        selectedSquadEntityAndEntitiesCountDict.Clear();
        SortSelectedSquads();
        squadManager.DeselectAllSquadEntities();

        unitsAreSelected = false;
        // OnUnitsSelectedChanged?.Invoke();
        if (battleInputManager.CursorMode != CursorMode.SpawnSquad) BattleManager.Instance.SetCursorMode(CursorMode.Free);
        entityQuery.Dispose();
    }
    public void SelectSquadsByGroup(List<int> _selectedSquadIds)
    {
        if (!battleInputManager.AddingToSelectedUnits)
        {
            // DeselectAllSquads();
            ClearSelectedSquads();
        }

        // Debug.Log($"Select squads by group {_selectedSquadIds.Count}");
        // SelectSquads already added every id and fired OnSelectedSquadsChanged, which is what syncs
        // the ECS Selected components — a follow-up AttemptSquadSelect pass short-circuits on every id.
        SelectSquads(_selectedSquadIds);
    }
    #endregion

    #region Helpers
    public List<SetDestination> GetSelectedUnitsPositions()
    {
        List<SetDestination> positions = new();
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        Dictionary<int, List<Entity>> selectedUnits = GetSelectedSquadIDsAndAllUnits();
        List<Entity> selectedEntities = new();
        foreach (KeyValuePair<int, List<Entity>> kvp in selectedUnits)
        {
            // Debug.Log($"Squad {kvp.Key} has {kvp.Value.Count} units");
            selectedEntities.AddRange(kvp.Value);
        }

        foreach (Entity entity in selectedEntities)
        {
            SetDestination setDestination = entityManager.GetComponentData<SetDestination>(entity);
            positions.Add(setDestination);
        }

        return positions;
    }
    public List<SetDestination> GetSelectedUnitsRepositionPositions()
    {
        List<SetDestination> positions = new();
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        Dictionary<int, List<Entity>> selectedUnits = GetSelectedSquadIDsAndAllUnits();

        EntityQuery squadQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<SquadEntity, SquadMovementComponent>()
            .Build(entityManager);
        NativeArray<Entity> squadEntityArray = squadQuery.ToEntityArray(Allocator.Temp);
        Dictionary<int, float3> squadOffsets = new();
        for (int i = 0; i < squadEntityArray.Length; i++)
        {
            SquadEntity squadEntityData = entityManager.GetComponentData<SquadEntity>(squadEntityArray[i]);
            SquadMovementComponent squadMovement = entityManager.GetComponentData<SquadMovementComponent>(squadEntityArray[i]);
            squadOffsets[squadEntityData.SquadId] = squadMovement.SquadCenter - squadMovement.GoalPosition;
        }
        squadEntityArray.Dispose();

        foreach (KeyValuePair<int, List<Entity>> kvp in selectedUnits)
        {
            float3 offset = squadOffsets.TryGetValue(kvp.Key, out float3 o) ? o : float3.zero;
            foreach (Entity entity in kvp.Value)
            {
                SetDestination setDestination = entityManager.GetComponentData<SetDestination>(entity);
                setDestination.destinationPosition += offset;
                positions.Add(setDestination);
            }
        }

        return positions;
    }
    public float3 GetFormationCenterPoint()
    {
        List<SetDestination> selectedUnitPositions = GetSelectedUnitsPositions();
        List<float3> selectedUnitPositions2 = new();
        foreach (SetDestination setDestination in selectedUnitPositions)
        {
            selectedUnitPositions2.Add(setDestination.destinationPosition);
        }
        float3 formationCenterPoint = float3.zero;
        selectedUnitPositions2.ForEach(position => formationCenterPoint += position);
        return formationCenterPoint / selectedUnitPositions.Count;
    }
    private void HoldPreviousRotation()
    {
        battleInputManager.SetAngle(-90f);// Default angle if no rotation is applied
        positionDrawer.PositionsParent.rotation = Quaternion.Euler(0, battleInputManager.Angle, 0);
    }
    private void GetSelectedUnitsCount(Dictionary<int, float3> SEwidthAndDepth, Dictionary<int, UnitType> SEunitTypes, bool silent = false)
    {
        // Debug.Log($"GetSelectedUnitsCount");
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery entityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Selected>().Build(entityManager);

        NativeArray<Entity> entityArray = entityQuery.ToEntityArray(Allocator.Temp);
        unitsAreSelected = entityArray.Length > 0;
        // selectedUnitsCount = entityArray.Length;
        Dictionary<int, List<Entity>> selectedSquadEntityAndEntitiesDict = new();

        for (int i = 0; i < entityArray.Length; i++)
        {
            Entity entity = entityArray[i];
            Unit unit = entityManager.GetComponentData<Unit>(entity);

            if (!selectedSquadEntityAndEntitiesDict.ContainsKey(unit.squadId))
            {
                selectedSquadEntityAndEntitiesDict[unit.squadId] = new List<Entity>();
            }

            selectedSquadEntityAndEntitiesDict[unit.squadId].Add(entity);
        }

        selectedSquadEntityAndEntitiesCountDict.Clear();
        //sort the dictionary by trueSquadOrder
        Dictionary<int, List<Entity>> sortedSelectedSquadEntityAndEntitiesDict = new();
        List<int> trueSquadOrder = squadManager.TrueSquadOrder;

        foreach (var squadId in trueSquadOrder)
        {
            if (selectedSquadEntityAndEntitiesDict.TryGetValue(squadId, out var entities))
            {
                sortedSelectedSquadEntityAndEntitiesDict[squadId] = entities;
            }
        }

        foreach (KeyValuePair<int, List<Entity>> selectedSquadEntityAndEntities in selectedSquadEntityAndEntitiesDict)
        {
            selectedSquadEntityAndEntitiesCountDict[selectedSquadEntityAndEntities.Key] = selectedSquadEntityAndEntities.Value.Count;

            //sort the selected entities by unit index
            selectedSquadEntityAndEntities.Value.Sort((a, b) =>
            {
                UnitPosition unitPositionA = entityManager.GetComponentData<UnitPosition>(a);
                UnitPosition unitPositionB = entityManager.GetComponentData<UnitPosition>(b);
                return unitPositionA.unitIndex.CompareTo(unitPositionB.unitIndex);
            });
        }

        positionDrawer.Formation.SetUnitCounts(selectedSquadEntityAndEntitiesCountDict);
        positionDrawer.Formation.SetUnitTypes(SEunitTypes);
        positionDrawer.Formation.SetWidthAndDepthDict(SEwidthAndDepth);

        if (battleInputManager.CursorMode != CursorMode.SpawnSquad)
        {
            BattleManager.Instance.SetCursorMode(unitsAreSelected ? CursorMode.UnitsSelected : CursorMode.Free);
        }
        entityArray.Dispose();
        entityQuery.Dispose();
    }
    /// <summary>
    /// Rebuilds the cached per-squad selected-unit counts from live entities and re-syncs the
    /// formation. GetSelectedUnitsCount only runs on selection change, so battlefield casualties
    /// leave the cache over-counting, which desyncs the position drawer's point pool from the units
    /// it represents. Call this before any pool layout so the preview matches reality.
    /// Key order is preserved so the formation layout order does not shift mid-selection.
    /// </summary>
    public void RefreshSelectedUnitCounts()
    {
        if (selectedSquadEntityAndEntitiesCountDict.Count == 0) return;

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery entityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Selected>().Build(entityManager);
        NativeArray<Entity> entityArray = entityQuery.ToEntityArray(Allocator.Temp);

        Dictionary<int, int> liveCounts = new();
        for (int i = 0; i < entityArray.Length; i++)
        {
            int squadId = entityManager.GetComponentData<Unit>(entityArray[i]).squadId;
            liveCounts.TryGetValue(squadId, out int count);
            liveCounts[squadId] = count + 1;
        }
        entityArray.Dispose();
        entityQuery.Dispose();

        bool changed = false;
        Dictionary<int, int> rebuilt = new();
        foreach (int squadId in selectedSquadEntityAndEntitiesCountDict.Keys)
        {
            int live = liveCounts.TryGetValue(squadId, out int c) ? c : 0;
            if (live != selectedSquadEntityAndEntitiesCountDict[squadId]) changed = true;
            if (live > 0) rebuilt[squadId] = live;
        }
        if (!changed) return;

        selectedSquadEntityAndEntitiesCountDict.Clear();
        foreach (KeyValuePair<int, int> kvp in rebuilt)
        {
            selectedSquadEntityAndEntitiesCountDict[kvp.Key] = kvp.Value;
        }
        positionDrawer.Formation.SetUnitCounts(selectedSquadEntityAndEntitiesCountDict);
    }
    private void SetSelectedSquadAngle(Quaternion rotation)
    {
        Vector3 euler = rotation.eulerAngles;
        battleInputManager.SetAngle(euler.y - 90f);
        // Debug.Log($"Set selected squad angle: {angle}");
        positionDrawer.SetLookRotation(rotation);
    }
    public bool HasCursorMovedSinceHover(float minPixels = 5f)
    {
        return Vector2.Distance(Input.mousePosition, hoverStartCursorPosition) >= minPixels;
    }
    public Vector3 GetMousePositionOffsetByFormationCenter(bool _isPlayerTeam = true)
    {
        float3 formationCenterPoint = float3.zero;
        List<float3> positions = BattleManager.Instance.PositionDrawer.Formation.PointPositions;

        //get average x position of the selected units
        foreach (float3 position in positions)
        {
            formationCenterPoint += position;
        }
        formationCenterPoint /= positions.Count;
        formationCenterPoint.z *= _isPlayerTeam ? 1 : -1;

        //offset the mouse position by the formation center point
        Vector3 mousePosition = MouseWorldPosition.Instance.GetWorldPosition();
        // Debug.Log($"Mouse position: {mousePosition}, Formation center point: {formationCenterPoint}");
        mousePosition += new Vector3(formationCenterPoint.z, 0, 0);
        return mousePosition;
    }
    #endregion

    private bool IsOutriderSquadDuringDeployment(int squadId)
    {
        if (BattleManager.Instance.GamePhase != GamePhase.Deployment) return false;
        EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = em.CreateEntityQuery(ComponentType.ReadOnly<Unit>());
        using NativeArray<Unit> units = query.ToComponentDataArray<Unit>(Allocator.Temp);
        UnitName unitName = default;
        bool found = false;
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i].squadId != squadId) continue;
            unitName = units[i].unitName;
            found = true;
            break;
        }
        query.Dispose();
        return found && TabletopTavernData.Instance.GetSquadStats(unitName).SquadAttributes.Outrider;
    }

    #region Hovering
    private bool SameSquadHovered(List<int> _hoveredSquadIds)
    {
        if (selectionAreaHoveredSquads.Count != _hoveredSquadIds.Count) return false;

        for (int i = 0; i < selectionAreaHoveredSquads.Count; i++)
        {
            if (selectionAreaHoveredSquads[i] != _hoveredSquadIds[i]) return false;
        }
        return true;
    }
    public void HoverSquadsOnSelectionArea(List<int> _squadIds)
    {
        if (SameSquadHovered(_squadIds)) return;
        selectionAreaHoveredSquads = _squadIds;
        OnHoverSquadsChanged?.Invoke(selectionAreaHoveredSquads);

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadEntity>());
        using var squadEntities = query.ToEntityArray(Allocator.TempJob);
        Dictionary<int, float3> SEwidthAndDepth = new();

        foreach (var squadEntity in squadEntities)
        {
            SquadEntity squad = entityManager.GetComponentData<SquadEntity>(squadEntity);
            List<Entity> unitsInSquad = squadManager.GetEntitiesFromSquad(squad.SquadId);
            bool isHovered = selectionAreaHoveredSquads.Contains(squad.SquadId);

            for (int i = 0; i < unitsInSquad.Count; i++)
            {
                Hovered hovered = entityManager.GetComponentData<Hovered>(unitsInSquad[i]);

                if (isHovered)
                    hovered.onHover = true;
                else
                    hovered.onUnhover = true;

                entityManager.SetComponentData(unitsInSquad[i], hovered);
            }

            BattleManager.Instance.UIManager.DisplayHoveredSquadUI(squad.SquadId, isHovered);

            if (BattleManager.Instance.SquadManager.SquadRangeDrawers.ContainsKey(squad.SquadId))
            {
                if (isHovered)
                {
                    BattleManager.Instance.SquadManager.SquadRangeDrawers[squad.SquadId].TurnOn();
                }
                else
                {
                    BattleManager.Instance.SquadManager.SquadRangeDrawers[squad.SquadId].TurnOff();
                }
            }

            entityManager.SetComponentData(squadEntity, squad);
        }
        query.Dispose();
    }
    public void HoverSquad(int _hoveredSquadId, bool playerSquad)
    {
        if (BattleManager.Instance.GamePhase == GamePhase.PostGame) return;

        timer += Time.deltaTime;
        if (_hoveredSquadId == previousHoveredSquad && timer < 0.5f) return;

        timer = 0;

        if (battleInputManager.IsRearrangingSquads) return;

        //deselect all units
        if (_hoveredSquadId == 0)
        {
            List<Entity> squadEntities = squadManager.GetEntitiesFromSquad(previousHoveredSquad);
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (squadEntities != null)
            {
                for (int i = 0; i < squadEntities.Count; i++)
                {
                    Hovered hovered = entityManager.GetComponentData<Hovered>(squadEntities[i]);
                    hovered.onUnhover = true;
                    entityManager.SetComponentData(squadEntities[i], hovered);
                }
            }

            if (playerSquad)
            {
                BattleManager.Instance.UIManager.DisplayHoveredSquadUI(previousHoveredSquad, false);
            }
            else
            {
                // Runtime.Instance.UIManager.HoverEnemySquad(_selectedSquadId, false);
            }

            if (BattleManager.Instance.SquadManager.SquadRangeDrawers.ContainsKey(previousHoveredSquad) &&
                !selectedSquadIds.Contains(previousHoveredSquad))
            {
                BattleManager.Instance.SquadManager.SquadRangeDrawers[previousHoveredSquad].TurnOff();
            }

            if (BattleManager.Instance.SquadManager.GateRangeDrawers.TryGetValue(previousHoveredSquad, out var prevGateDrawer) && prevGateDrawer != null)
            {
                prevGateDrawer.TurnOff();
            }

        }
        else
        {
            List<Entity> squadEntities = squadManager.GetEntitiesFromSquad(_hoveredSquadId);
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            hoverStartCursorPosition = Input.mousePosition;

            for (int i = 0; i < squadEntities.Count; i++)
            {
                Hovered hovered = entityManager.GetComponentData<Hovered>(squadEntities[i]);
                hovered.onHover = true;
                entityManager.SetComponentData(squadEntities[i], hovered);
            }

            if (playerSquad)
            {
                BattleManager.Instance.UIManager.DisplayHoveredSquadUI(_hoveredSquadId, true);
            }
            else
            {
                // Runtime.Instance.UIManager.HoverEnemySquad(_selectedSquadId, true);

            }

            if (BattleManager.Instance.SquadManager.SquadRangeDrawers.ContainsKey(_hoveredSquadId))
            {
                BattleManager.Instance.SquadManager.SquadRangeDrawers[_hoveredSquadId].TurnOn();
            }

            if (BattleManager.Instance.SquadManager.GateRangeDrawers.TryGetValue(_hoveredSquadId, out var gateDrawer) && gateDrawer != null)
            {
                gateDrawer.TurnOn();
            }
        }

        if (_hoveredSquadId == previousHoveredSquad) return;

        previousHoveredSquad = _hoveredSquadId;
        if (_hoveredSquadId != 0)
        {
            SquadEntity hoveredSquad = squadManager.GetSquad(_hoveredSquadId);
            if (hoveredSquad.SquadId == 0)
            {
                BattleManager.Instance.UIManager.SetNoSquadHovered();
                return;
            }
            BattleManager.Instance.UIManager.SetSquadHovered(hoveredSquad, playerSquad);
        }
        else
        {
            BattleManager.Instance.UIManager.SetNoSquadHovered();
        }

        //if hoversquad is an enemy squad, enemy has deployed, and not an outrider during deployment, show the attack cursor
        if (!playerSquad && BattleManager.Instance.ArmySpawnManager.EnemyArmyDeployed && !IsOutriderSquadDuringDeployment(previousHoveredSquad))
        {
            battleInputManager.ChangeCursorToAttack();
        }
        else
        {
            battleInputManager.ResetCursor();
        }
        OnHoverSquadsChanged?.Invoke(new List<int> { previousHoveredSquad });
    }
    private void HandleHoverBattlefieldBonus()
    {
        if (Camera.main == null) return;

        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out UnityEngine.RaycastHit hitInfo, Mathf.Infinity, ~(1 << TabletopTavernConstants.SQUAD_FLAG_LAYER)))
        {
            if (hitInfo.collider.gameObject.layer.Equals(TabletopTavernConstants.BATTLEFIELD_BONUS_LAYER))
            {
                // Debug.Log($"Hovering battlefield bonus {hitInfo.collider.gameObject.name}");
                BattlefieldBonusGameObject battlefieldBonus = hitInfo.collider.GetComponentInParent<BattlefieldBonusGameObject>();
                if (battlefieldBonus != cachedBattlefieldBonus)
                {
                    BattleManager.Instance.UIManager.DisplayBonus(battlefieldBonus.BattlefieldBonus);
                    cachedBattlefieldBonus = battlefieldBonus;
                    cachedBiomeCollider = null;
                }
                return;
            }
            else if (hitInfo.collider.gameObject.layer.Equals(TabletopTavernConstants.SWAMP_LAYER))
            {
                BattlefieldBonus battlefieldBonus = new BattlefieldBonus
                {
                    BattlefieldBonusEnum = BattlefieldBonusEnum.Swamp,
                    Value = TabletopTavernConstants.SWAMP_SPEED_MODIFIER,
                    UnitStat = UnitStat.Speed,
                    Team = Team.Neutral,
                    OriginationPoint = hitInfo.collider.transform.position,
                    Guid = Guid.NewGuid(),
                    Range = 0f,
                    Applied = true
                };

                BiomeCollider newBiome = hitInfo.collider.gameObject.GetComponent<BiomeChild>().CombinedMesh.GetComponent<BiomeCollider>();
                if (cachedBiomeCollider != null && cachedBiomeCollider != newBiome)
                {
                    if (cachedBiomeCollider != null)
                    {
                        cachedBiomeCollider.StopOutlineGlow();
                    }
                }

                if (cachedBiomeCollider == newBiome) return;

                cachedBiomeCollider = newBiome;
                cachedBiomeCollider.StartOutlineGlow();
                BattleManager.Instance.UIManager.DisplayBonus(battlefieldBonus);
                cachedBattlefieldBonus = null;
                return;
            }
            else if (hitInfo.collider.gameObject.layer.Equals(TabletopTavernConstants.FOREST_LAYER))
            {
                BattlefieldBonus battlefieldBonus = new BattlefieldBonus
                {
                    BattlefieldBonusEnum = BattlefieldBonusEnum.Forest,
                    Value = 0,
                    UnitStat = UnitStat.None,
                    Team = Team.Neutral,
                    OriginationPoint = hitInfo.collider.transform.position,
                    Guid = Guid.NewGuid(),
                    Range = 0f,
                    Applied = true
                };

                BiomeCollider newBiome = hitInfo.collider.gameObject.GetComponent<BiomeChild>().CombinedMesh.GetComponent<BiomeCollider>();
                if (cachedBiomeCollider != null && cachedBiomeCollider != newBiome)
                {
                    if (cachedBiomeCollider != null)
                    {
                        cachedBiomeCollider.StopOutlineGlow();
                    }
                }

                if (cachedBiomeCollider == newBiome) return;

                cachedBiomeCollider = newBiome;
                cachedBiomeCollider.StartOutlineGlow();
                BattleManager.Instance.UIManager.DisplayBonus(battlefieldBonus);
                cachedBattlefieldBonus = null;
                return;
            }
        }

        if (cachedBiomeCollider != null)
        {
            cachedBiomeCollider.StopOutlineGlow();
            BattleManager.Instance.UIManager.HideBonus();
            cachedBiomeCollider = null;
        }

        if (cachedBattlefieldBonus != null)
        {
            BattleManager.Instance.UIManager.HideBonus();
            cachedBattlefieldBonus = null;
        }
    }
    #endregion

    #region Event Handlers
    public void OnSelectedSquadsChangedHandler(List<int> _selectedSquadIds)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadEntity>());
        using var squadEntities = query.ToEntityArray(Allocator.TempJob);
        Dictionary<int, float3> SEwidthAndDepth = new();
        Dictionary<int, UnitType> SEunitTypes = new();
        Dictionary<int, float> SEsquadCenterX = new();
        SelectedSquadUnitNames = new();

        //sort squadEntities by true squad order
        List<Entity> sortedSquadEntities = new();
        for (int i = 0; i < squadManager.TrueSquadOrder.Count; i++)
        {
            for (int j = 0; j < squadEntities.Length; j++)
            {
                SquadEntity squad = entityManager.GetComponentData<SquadEntity>(squadEntities[j]);
                if (squad.SquadId == squadManager.TrueSquadOrder[i])
                {
                    sortedSquadEntities.Add(squadEntities[j]);
                    break;
                }
            }
        }

        foreach (var squadEntity in sortedSquadEntities)
        {
            SquadEntity squad = entityManager.GetComponentData<SquadEntity>(squadEntity);
            List<Entity> unitsInSquad = squadManager.GetEntitiesFromSquad(squad.SquadId);
            bool isSelected = _selectedSquadIds.Contains(squad.SquadId);

            squad.IsSelected = isSelected;

            if (isSelected)
                SelectedSquadUnitNames.Add(squad.UnitName);

            for (int i = 0; i < unitsInSquad.Count; i++)
            {
                entityManager.SetComponentEnabled<Selected>(unitsInSquad[i], isSelected);
                Selected selected = entityManager.GetComponentData<Selected>(unitsInSquad[i]);

                if (isSelected)
                    selected.onSelected = true;
                else
                    selected.onDeselected = true;

                entityManager.SetComponentData(unitsInSquad[i], selected);

                Hovered hovered = entityManager.GetComponentData<Hovered>(unitsInSquad[i]);

                if (isSelected)
                    hovered.onSelected = true;
                else
                    hovered.onDeselected = true;

                entityManager.SetComponentData(unitsInSquad[i], hovered);
            }

            entityManager.SetComponentData(squadEntity, squad);

            if (BattleManager.Instance.SquadManager.SquadRangeDrawers.ContainsKey(squad.SquadId))
            {
                if (!isSelected)
                    BattleManager.Instance.SquadManager.SquadRangeDrawers[squad.SquadId].TurnOff();
                else if (isSelected)
                    BattleManager.Instance.SquadManager.SquadRangeDrawers[squad.SquadId].TurnOn();
            }

            if (!isSelected) continue;

            SquadMovementComponent squadMovementComponent = entityManager.GetComponentData<SquadMovementComponent>(squad.SelfEntity);
            int2 widthAndDepth = squadMovementComponent.SquadWidthAndDepth;
            UnitSize unitSize = TabletopTavernData.Instance.GetUnitSizeFromUnitName(squad.UnitName);
            float spread = TabletopTavernConstants.GetSpread(unitSize);

            SEwidthAndDepth[squad.SquadId] = new float3(widthAndDepth.x, widthAndDepth.y, spread);
            SEsquadCenterX[squad.SquadId] = squadMovementComponent.SquadCenter.x;
            SquadOverridesComponent overrides = entityManager.GetComponentData<SquadOverridesComponent>(squad.SelfEntity);
            SEunitTypes[squad.SquadId] = overrides.UnitType;
            SetSelectedSquadAngle(squadMovementComponent.SquadRotation);
        }
        //sort SEwidthAndDepth by true squad order
        Dictionary<int, float3> sortedSEwidthAndDepth = new();

        foreach (var squadId in squadManager.TrueSquadOrder)
        {
            if (SEwidthAndDepth.TryGetValue(squadId, out var widthDepth))
            {
                sortedSEwidthAndDepth[squadId] = widthDepth;
            }
        }
        // Debug.Log($"Selected squads width and depth: {string.Join(", ", sortedSEwidthAndDepth.Select(kvp => $"{kvp.Key}: ({kvp.Value.x}, {kvp.Value.y}, {kvp.Value.z})"))}");
        SEwidthAndDepth = sortedSEwidthAndDepth;
        positionDrawer.Formation.SetSquadCenterXDict(SEsquadCenterX);
        GetSelectedUnitsCount(SEwidthAndDepth, SEunitTypes);

        if (_selectedSquadIds.Count > 0 && Time.frameCount != _lastSelectSFXFrame)
        {
            _lastSelectSFXFrame = Time.frameCount;
            IAudioRequester.Instance.PlaySFX("select-squad");
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.SelectUnit);
        }
        // positionDrawer.Formation.CalculateUnitDepthAndWidth(0);

        query.Dispose();
    }
    private void RemoveSquadFromSelection(int _squadId)
    {
        if (selectedSquadIds.Contains(_squadId))
        {
            selectedSquadIds.Remove(_squadId);
            SortSelectedSquads();
        }
    }
    #endregion
    private void SortSelectedSquads()
    {
        List<int> unsortedSquadIds = new(selectedSquadIds);
        selectedSquadIds.Clear();
        for(int i = 0; i < squadManager.TrueSquadOrder.Count; i++)
        {
            if(unsortedSquadIds.Contains(squadManager.TrueSquadOrder[i]))
            {
                selectedSquadIds.Add(squadManager.TrueSquadOrder[i]);
            }
        }

        // squad IDs not in TrueSquadOrder (e.g. enemy squad IDs) go at the end
        for(int i = 0; i < unsortedSquadIds.Count; i++)
        {
            if(!selectedSquadIds.Contains(unsortedSquadIds[i]))
                selectedSquadIds.Add(unsortedSquadIds[i]);
        }

        OnSelectedSquadsChanged?.Invoke(selectedSquadIds);
    }

    public void OnDestroy()
    {
        squadManager.OnDestroyedSquad -= RemoveSquadFromSelection;
        OnSelectedSquadsChanged -= OnSelectedSquadsChangedHandler;
        if(BattleManager.HasInstance)
            BattleManager.Instance.OnSquadBrokenEvent -= RemoveSquadFromSelection;
    }
}