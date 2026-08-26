using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;
using TJ;
using TJ.Map;
using Memori.Input;
using Unity.Mathematics;
using Memori.Notifications;
using Memori.Audio;

public enum CursorMode {Free, MouseDown, UnitsSelected, Reposition, SpawnSquad, CastSpell, QuickCastMenu, PostGame }
public class BattleInputManager : MonoBehaviour
{
    public static BattleInputManager Instance { get; private set; }
    
    public event EventHandler OnSelectionAreaStart;
    public event EventHandler OnSelectionAreaEnd;
    private Vector2 selectionStartMousePosition;

    [SerializeField] private Texture2D attackCursor;
    [SerializeField] private Texture2D rotateCursor;
    [SerializeField] private LayerMask flagLayerMask;
    private Vector2 hotSpot = Vector2.zero; // Point on the cursor texture to be used as the click point
    Vector3 initialMouseScreenPosition, initialMouseWorldPosition;
    public Vector3 InitialMouseWorldPosition => initialMouseWorldPosition;

    [SerializeField] float sphereRadius = 1f;
    [SerializeField] int steps = 3;

    private bool isRearrangingSquads;
    public bool IsRearrangingSquads => isRearrangingSquads;

    [SerializeField] private float angle;
    public float Angle => angle;
    public void SetAngle(float newAngle) => angle = newAngle;
    [SerializeField] public bool rotateRepositioningFormation = false;
    [SerializeField] bool minimumDistanceFromInitialClickHit;
    public bool MinimumDistanceFromInitialClickHit => minimumDistanceFromInitialClickHit;
    public void SetMinimumDistanceFromInitialClickHit(bool value) => minimumDistanceFromInitialClickHit = value;
    [SerializeField] private float reformSelectionRotationDeadzone = 15f;
    public float ReformSelectionRotationDeadzone => reformSelectionRotationDeadzone;
    [SerializeField] bool settingInitialSquadPosition;
    public bool SettingInitialSquadPosition => settingInitialSquadPosition;
    public void SetInitialSquadPosition(bool value) => settingInitialSquadPosition = value;

    [SerializeField] bool addingToSelectedUnits = false, repositioningSelectedUnits = false;
    private bool repositionCancelled = false;

    private float _lastBattlefieldClickTime = -1f;
    private int _lastBattlefieldClickedSquadId = 0;
    private const float DoubleClickTime = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool debug = false;
    [SerializeField] Vector3 mousePositionDebug;
    [SerializeField] Vector3 InitialMouseWorldPositionDebug;
    [SerializeField] Transform initialMouseWorldPositionDebug;
    [SerializeField] Transform currentMouseWorldPositionDebug;


    public bool RepositioningSelectedUnits => repositioningSelectedUnits;
    public bool RepositionCancelled => repositionCancelled;
    public bool AddingToSelectedUnits => addingToSelectedUnits;
    public bool LeftClickDownThisFrame => _leftClickDownThisFrame;
    public bool RightClickDownThisFrame => _rightClickDownThisFrame;
    public bool RightClickUpThisFrame => _rightClickUpThisFrame;
    public bool LeftClickUpThisFrame => _leftClickUpThisFrame;
    public bool LeftClickHeldDown => _leftClickHeldDown;
    public bool RightClickHeldDown => _rightClickHeldDown;
    public CursorMode CursorMode => cursorMode;

    private bool _leftClickDownThisFrame => Input.GetMouseButtonDown(0);
    private bool _rightClickDownThisFrame => Input.GetMouseButtonDown(1);
    private bool _rightClickUpThisFrame => Input.GetMouseButtonUp(1);
    private bool _leftClickUpThisFrame => Input.GetMouseButtonUp(0);
    private bool _leftClickHeldDown => Input.GetMouseButton(0);
    private bool _rightClickHeldDown => Input.GetMouseButton(1);
    private SpawnManager spawnManager;
    private PositionDrawer positionDrawer;
    private UnitPositioningManager unitPositioningManager;
    private UnitSelectionManager unitSelectionManager;
    private CursorMode cursorMode;

    private void Awake()
    {
        Instance = this;
#if !UNITY_EDITOR
        debug = false;
#endif
    }
    private void Start()
    {
        spawnManager = BattleManager.Instance.SpawnManager;
        positionDrawer = BattleManager.Instance.PositionDrawer;
        unitPositioningManager = BattleManager.Instance.UnitPositioningManager;
        BattleManager.Instance.OnCursorModeChanged += OnCursorModeChanged;
        InputHandler.Instance.OnAddUnitsToSelection += AddUnitsToSelection;
        InputHandler.Instance.OnAddUnitsToSelectionCanceled += CancelAddingUnitsToSelection;
        unitSelectionManager = UnitSelectionManager.Instance;
        initialMouseWorldPositionDebug.gameObject.SetActive(debug);
        currentMouseWorldPositionDebug.gameObject.SetActive(debug);
    }
    private void Update()
    {
        if(debug)
        {
            initialMouseWorldPositionDebug.position = initialMouseWorldPosition;
            InitialMouseWorldPositionDebug = initialMouseWorldPosition;
            mousePositionDebug = MouseWorldPosition.Instance.GetWorldPosition();
            currentMouseWorldPositionDebug.position = MouseWorldPosition.Instance.GetWorldPosition();
        }

        repositioningSelectedUnits = InputHandler.Instance.RepositioningSelectedUnits;
        if (!InputHandler.Instance.RepositioningSelectedUnits) repositionCancelled = false;
    }
    private void AddUnitsToSelection()
    {
        addingToSelectedUnits = true;
    }
    private void CancelAddingUnitsToSelection()
    {
        addingToSelectedUnits = false;
    }

    public void SetInitialMousePositions(Vector3 worldPosition)
    {
        initialMouseScreenPosition = Input.mousePosition;
        initialMouseWorldPosition = worldPosition;
    }
    public void SetInitialMouseWorldPosition(Vector3 worldPosition)
    {
        initialMouseWorldPosition = worldPosition;
    }
    public void ChangeCursorToAttack()
    {
        Cursor.SetCursor(attackCursor, hotSpot, UnityEngine.CursorMode.Auto);
    }
    public void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, UnityEngine.CursorMode.Auto);
    }
    public bool MinimumDistanceFromInitialClick(float minimumDistance = 150f)
    {
        return Vector3.Distance(initialMouseScreenPosition, Input.mousePosition) > minimumDistance;
    }
    bool selectionAreaStarted = false;
    public void HandleSelectionArea()
    {
        if (BattleManager.Instance.GamePhase == GamePhase.SetUp) return;
        if (_leftClickDownThisFrame)
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            { //check if mouse is over UI
                // Debug.Log($"Clicked on UI, not starting selection area");
                return;
            }

            selectionStartMousePosition = Input.mousePosition;
            OnSelectionAreaStart?.Invoke(this, EventArgs.Empty);
            selectionAreaStarted = true;

            if (cursorMode.Equals(CursorMode.MouseDown))
            {
                positionDrawer.TurnOff();
            }

        }
        
        if (_leftClickUpThisFrame && selectionAreaStarted)
        {
            selectionAreaStarted = false;
            // OnSelectionAreaEnd?.Invoke(this, EventArgs.Empty);

            // Debug.Log($"Selection area end");
            // if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            // { //check if mouse is over UI
            //     return;
            // }

            HandleSelectionAreaEnded();
        }
    }
    public void HandleSpawningSquadCursorMode()
    {
        BattleManager.Instance.PositionDrawer.ConfirmValidityOfPositions(spawnManager.Team, spawnManager.Outrider);

        void SpawnSquad()
        {
            spawnManager.SpawnSquad();
            positionDrawer.TurnOff();
            BattleManager.Instance.UIManager.HideSpawnErrorMessage();
        }
        void RotateSquad()
        {
            if (!MinimumDistanceFromInitialClick()) return;

            RotateFormationToMouse();
            CalculateMouseDraggedDistance();
        }
        void ConfirmPositionsSetRotation()
        {
            SetInitialMousePositions(positionDrawer.PositionsParent.position);
        }
        void PositionSquad()
        {
            positionDrawer.MovePositionToMouse(unitSelectionManager.GetMousePositionOffsetByFormationCenter(spawnManager.Team == Team.Player));
        }

        if (_leftClickDownThisFrame)
        {
            positionDrawer.TurnOff();
            cursorMode = CursorMode.Free;
            IAudioRequester.Instance.PlaySFX(SFXData.ActionFailed);
        }
        else if (_rightClickUpThisFrame)
        {
            if (BattleManager.Instance.PositionDrawer.ValidPositions)
            {
                SpawnSquad();
            }
            else
            {
                positionDrawer.TurnOff();
                // Debug.Log($"Invalid positions");
                BattleManager.Instance.UIManager.BroadcastSpawnError();
                BattleManager.Instance.UIManager.HideSpawnErrorMessage();
                BattleManager.Instance.SetCursorMode(CursorMode.Free);
            }
        }
        else if (!_rightClickHeldDown)
        {
            PositionSquad();
        }
        else if (_rightClickDownThisFrame)
        {
            ConfirmPositionsSetRotation();
        }
        else
        {
            RotateSquad();
        }
    }
    public void HandleRepositionUnits()
    {
        // Debug.Log($" Handle repo");
        if (_leftClickHeldDown && _rightClickDownThisFrame)
        {
            RotatingSelectedUnits(false);
            positionDrawer.TurnOff();
            repositionCancelled = true;
            BattleManager.Instance.SetCursorMode(CursorMode.Free);
            // Debug.Log($"reposition canceled");
            // return;
        }

        //if left click is held down, move the selected units to the mouse position
        if (_leftClickDownThisFrame)
        {
            RotatingSelectedUnits(false);

            //casualties since the last selection change would otherwise leave the cached counts high
            unitSelectionManager.RefreshSelectedUnitCounts();

            List<SetDestination> livePositions = unitSelectionManager.GetSelectedUnitsRepositionPositions();
            List<SetDestination> positions = livePositions;
            if (BattleManager.Instance.GroupManager.AreSelectedSquadsInLockedGroup(
                    unitSelectionManager.SelectedSquadIds, out TJ.Battle.SquadGroup lockedGroup))
            {
                //a locked layout that no longer matches the live squads would preview stale points
                if (lockedGroup.LockedPositions != null && lockedGroup.LockedPositions.Count == livePositions.Count)
                    positions = lockedGroup.LockedPositions;
            }

            positionDrawer.PreviewMoveFormation(
                unitSelectionManager.GetMousePositionOffsetByFormationCenter(),
                positions
            );
        }
        else if (_leftClickHeldDown && !rotateRepositioningFormation)
        {
            positionDrawer.MovePositionToMouse(
                unitSelectionManager.GetMousePositionOffsetByFormationCenter(),
                true
            );
        }
        else if (_leftClickUpThisFrame)
        {
            if (!BattleManager.Instance.PositionDrawer.ValidPositions)
            {
                positionDrawer.TurnOff();
                // Debug.Log($"Invalid positions");
                NotificationManager.Instance.ErrorNotification(BattleManager.Instance.PositionDrawer.PositionErrorMessage);
                BattleManager.Instance.SetCursorMode(CursorMode.Free);
                return;
            }
            if (BattleManager.Instance.CursorMode == CursorMode.Reposition)
            {
                if(rotateRepositioningFormation)
                {
                    RotatingSelectedUnits(false);
                }
                unitPositioningManager.IssueSquadMoveCommand(false, InputHandler.Instance.QueueOrder);
            }
            return;
        }
        else if (!_leftClickHeldDown)
        {
            // Debug.Log($"No left click held down");
            return;
        }

        //if control, rotate the selected units to face the mouse position
        if (InputHandler.Instance.ControlInput && !rotateRepositioningFormation)
        {
            RotatingSelectedUnits(true);
        }
        else if (InputHandler.Instance.ControlInput && rotateRepositioningFormation)
        {
            RotatePositionToGrandParent();
        }
        else if (_rightClickUpThisFrame)
        {
            RotatingSelectedUnits(false);
            if (!BattleManager.Instance.PositionDrawer.ValidPositions)
            {
                positionDrawer.TurnOff();
                BattleManager.Instance.SetCursorMode(CursorMode.Free);
                return;
            }
            // unitPositioningManager.HandleSetUnitsPositionAndRotation();
        }
    }
    private void RotatingSelectedUnits(bool _turnOn)
    {
        // Debug.Log($"RotatingSelectedUnits: {_turnOn}");
        rotateRepositioningFormation = _turnOn;
        if (rotateRepositioningFormation)
        {
            float3 offset = unitSelectionManager.GetFormationCenterPoint() - (float3)MouseWorldPosition.Instance.GetWorldPosition();
            SetInitialMouseWorldPosition(unitSelectionManager.GetFormationCenterPoint() - offset);
            Cursor.SetCursor(rotateCursor, hotSpot, UnityEngine.CursorMode.Auto);

            positionDrawer.ParentTheParented(initialMouseWorldPosition);
        }
        else
        {
            // Debug.Log($"Stopped RotatingSelectedUnits");
            positionDrawer.UnParentTheParented();
            ResetCursor();
        }
    }
    public void StopRotation() => RotatingSelectedUnits(false);
    private void OnCursorModeChanged(CursorMode _cursorMode)
    {
        cursorMode = _cursorMode;
        // Debug.Log($"Cursor mode changed to: {cursorMode}");

        if (cursorMode == CursorMode.SpawnSquad)
        {
            OnSelectionAreaEnd?.Invoke(this, EventArgs.Empty);
            unitSelectionManager.DeselectSquadsBeforeDeletionOrSpawning();
            int selectedUnitsCount = BattleManager.Instance.SpawnManager.UnitCount;
            UnitSize unitSize = TabletopTavernData.Instance.GetUnitSizeFromUnitName(BattleManager.Instance.SpawnManager.UnitName);
            float spread = TabletopTavernConstants.GetSpread(unitSize);
            positionDrawer.PreviewSpawnFormation(MouseWorldPosition.Instance.GetWorldPosition(), selectedUnitsCount, spread);
        }
        // else if (cursorMode == CursorMode.CastSpell)
        // {
        //     OnSelectionAreaEnd?.Invoke(this, EventArgs.Empty);
        //     unitSelectionManager.DeselectAllSquads();
        // }
        else if (cursorMode == CursorMode.MouseDown)
        {
            minimumDistanceFromInitialClickHit = false;
            settingInitialSquadPosition = false;
        }
    }
    public void RotateFormationToMouse()
    {
        // Debug.Log($"RotateFormationToMouse");
        Vector3 direction = InitialMouseWorldPosition - MouseWorldPosition.Instance.GetWorldPosition();
        angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        positionDrawer.PositionsParent.rotation = Quaternion.Euler(0, angle, 0);
    }
    public void RotatePositionToGrandParent()
    {
        // Debug.Log($"RotateFormationToGrandParent");
        Vector3 direction = InitialMouseWorldPosition - MouseWorldPosition.Instance.GetWorldPosition();
        angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        positionDrawer.PositionsGrandParent.rotation = Quaternion.Euler(0, angle, 0);
    }
    public void SelectionAreaEnded()
    {
        OnSelectionAreaEnd?.Invoke(this, EventArgs.Empty);
    }
    public void CalculateMouseDraggedDistance()
    {
        float distance = Vector3.Distance(InitialMouseWorldPosition, MouseWorldPosition.Instance.GetWorldPosition());
        // Debug.Log($"Mouse dragged distance: {distance}");
        positionDrawer.Formation.CalculateUnitDepthAndWidth(distance);
    }
    private bool IsMultipleSelection()
    {
        Rect selectionAreaRect = GetSelectionAreaRect();
        float selectionAreaSize = selectionAreaRect.width + selectionAreaRect.height;
        float multipleSelectionSizeMin = 60f;
        return selectionAreaSize > multipleSelectionSizeMin;
    }

    public Rect GetSelectionAreaRect()
    {
        Vector2 selectionEndMousePosition = Input.mousePosition;

        Vector2 lowerLeftCorner = new Vector2(
            Mathf.Min(selectionStartMousePosition.x, selectionEndMousePosition.x),
            Mathf.Min(selectionStartMousePosition.y, selectionEndMousePosition.y));

        Vector2 upperRightCorner = new Vector2(
            Mathf.Max(selectionStartMousePosition.x, selectionEndMousePosition.x),
            Mathf.Max(selectionStartMousePosition.y, selectionEndMousePosition.y));

        return new Rect(
            lowerLeftCorner.x,
            lowerLeftCorner.y,
            upperRightCorner.x - lowerLeftCorner.x,
            upperRightCorner.y - lowerLeftCorner.y
        );
    }
    public void SetRearrangingSquads(bool _isRearrangingSquads)
    {
        isRearrangingSquads = _isRearrangingSquads;

        if (isRearrangingSquads)
        {
            BattleManager.Instance.UIManager.SetNoSquadHovered();
            SelectionAreaEnded();
            return;
        }
    }
    public void HandleSelectionAreaEnded()
    {
        // Debug.Log($"HandleUnitSelection");
        Vector2 selectionEndMousePosition = Input.mousePosition;
        //check if over a unit card

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery entityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Selected>().Build(entityManager);

        NativeArray<Entity> entityArray = entityQuery.ToEntityArray(Allocator.Temp);

        if (!AddingToSelectedUnits)
        {
            // Debug.Log($"here");
            unitSelectionManager.DeselectAllSquadsForNewSelectionArea();
        }

        if (IsMultipleSelection())
        {
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.SelectMultipleUnits);

            entityQuery.Dispose();
            entityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<LocalTransform, Unit>().WithPresent<Selected>().Build(entityManager);
            entityArray = entityQuery.ToEntityArray(Allocator.Temp);
            NativeArray<LocalTransform> localTransformArray = entityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            List<int> squadsToSelect = new();

            for (int i = 0; i < localTransformArray.Length; i++)
            {
                LocalTransform unitLocalTransform = localTransformArray[i];
                Vector2 unitScreenPosition = Camera.main.WorldToScreenPoint(unitLocalTransform.Position);

                if (GetSelectionAreaRect().Contains(unitScreenPosition))
                {
                    Unit unit = entityManager.GetComponentData<Unit>(entityArray[i]);
                    if (CanThisTeamBeSelected(unit.Team))
                    {
                        if (!squadsToSelect.Contains(unit.squadId)) squadsToSelect.Add(unit.squadId);
                    }
                }
            }

            squadsToSelect = RemoveBrokenSquads(squadsToSelect);
            unitSelectionManager.SelectSquads(squadsToSelect);
            entityQuery.Dispose();
        }
        else
        {
            entityQuery.Dispose();
            UnityEngine.Ray cameraRay = Camera.main.ScreenPointToRay(Input.mousePosition);

            //try to hit flag
            if (Physics.Raycast(cameraRay, out UnityEngine.RaycastHit hitInfo, Mathf.Infinity, flagLayerMask))
            {
                if (hitInfo.collider.CompareTag("SquadFlag"))
                {
                    // Debug.Log($"hit squad flag");
                    // Handle hit on squad flag here
                    SquadFlagGameObject squadFlag = hitInfo.collider.GetComponent<SquadFlagGameObject>();
                    if (CheckAndRecordBattlefieldDoubleClick(squadFlag.SquadId) && squadFlag.SquadId > 0)
                    {
                        UnitName unitName = BattleManager.Instance.SquadManager.GetSquad(squadFlag.SquadId).UnitName;
                        unitSelectionManager.SelectAllPlayerSquadsWithName(unitName);
                    }
                    else if (AddingToSelectedUnits)
                    {
                        unitSelectionManager.ToggleSquadSelect(squadFlag.SquadId);
                    }
                    else
                    {
                        unitSelectionManager.AttemptSquadSelect(squadFlag.SquadId, true);
                    }
                }
            }
            //try to hit unit
            else
            {
                // Single select
                entityQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
                PhysicsWorldSingleton physicsWorldSingleton = entityQuery.GetSingleton<PhysicsWorldSingleton>();
                CollisionWorld collisionWorld = physicsWorldSingleton.CollisionWorld;
                if (Camera.main == null) { entityQuery.Dispose(); return; }

                // Set the radius for the spherecast simulation
                int numRays = 8; // Number of rays to simulate the spherecast
                float rayDistance = 9999f;

                // Define a filter for collisions
                CollisionFilter filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = 1u << TabletopTavernConstants.UNITS_LAYER,
                    GroupIndex = 0,
                };

                // Cast multiple rays in a circle pattern around the original ray
                bool hit = false;
                Unity.Physics.RaycastHit raycastHit = new Unity.Physics.RaycastHit();

                for (int j = 1; j <= steps; j++)
                {
                    for (int i = 0; i < numRays; i++)
                    {
                        float angle = (i / (float)numRays) * Mathf.PI * 2;
                        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * sphereRadius * j;

                        RaycastInput raycastInput = new RaycastInput
                        {
                            Start = cameraRay.GetPoint(0f) + offset,
                            End = cameraRay.GetPoint(rayDistance) + offset,
                            Filter = filter
                        };

                        if (collisionWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit tempHit))
                        {
                            hit = true;
                            raycastHit = tempHit; // Store the hit data
                            break; // Exit loop on the first hit
                        }
                    }
                }

                if (hit)
                {
                    // Debug.Log($"hit unit");
                    if (entityManager.HasComponent<Unit>(raycastHit.Entity) && entityManager.HasComponent<Selected>(raycastHit.Entity))
                    {
                        // Hit a Unit
                        Unit unit = entityManager.GetComponentData<Unit>(raycastHit.Entity);
                        if (CanThisTeamBeSelected(unit.Team))
                        {
                            if(entityManager.HasComponent<RetreatingUnit>(raycastHit.Entity))
                            {
                                //check if unit is broken
                                if (!entityManager.IsComponentEnabled<RetreatingUnit>(raycastHit.Entity))
                                {
                                    if (CheckAndRecordBattlefieldDoubleClick(unit.squadId))
                                    {
                                        UnitName unitName = BattleManager.Instance.SquadManager.GetSquad(unit.squadId).UnitName;
                                        unitSelectionManager.SelectAllPlayerSquadsWithName(unitName);
                                    }
                                    else if (AddingToSelectedUnits)
                                    {
                                        unitSelectionManager.ToggleSquadSelect(unit.squadId);
                                    }
                                    else
                                    {
                                        List<int> previouslySelectedSquads = unitSelectionManager.GetSelectedSquadEntities().ConvertAll(squad => squad.SquadId);
                                        previouslySelectedSquads.Add(unit.squadId);
                                        unitSelectionManager.SelectSquads(previouslySelectedSquads);
                                        unitSelectionManager.AttemptSquadSelect(unit.squadId, true);
                                    }
                                }
                            }
                        }
                        else if (unit.squadId < 0)
                        {
                            unitSelectionManager.AttemptSquadSelect(unit.squadId, true);
                        }
                    }
                }
                entityQuery.Dispose();
            }
        }
        SelectionAreaEnded();
    }
    private bool CheckAndRecordBattlefieldDoubleClick(int squadId)
    {
        bool isDouble = (Time.time - _lastBattlefieldClickTime) <= DoubleClickTime && _lastBattlefieldClickedSquadId == squadId;
        _lastBattlefieldClickTime = Time.time;
        _lastBattlefieldClickedSquadId = squadId;
        return isDouble;
    }
    public List<int> RemoveBrokenSquads(List<int> squadIds)
    {
        List<int> nonBrokenSquads = new();
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery entityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<SquadEntity>().WithNone<BrokenSquadTag>().Build(entityManager);
        NativeArray<Entity> entityArray = entityQuery.ToEntityArray(Allocator.Temp);
        NativeArray<SquadEntity> selectedArray = entityQuery.ToComponentDataArray<SquadEntity>(Allocator.Temp);

        for (int i = 0; i < entityArray.Length; i++)
        {
            if (squadIds.Contains(selectedArray[i].SquadId))
            {
                nonBrokenSquads.Add(selectedArray[i].SquadId);
            }
        }
        entityQuery.Dispose();
        selectedArray.Dispose();
        entityArray.Dispose();
        return nonBrokenSquads;
    }
    public void HandleHoverSquad()
    {
        if (Camera.main == null) return;

        if (LeftClickHeldDown)
        {
            if (IsMultipleSelection())
            {
                EntityManager entityManager2 = World.DefaultGameObjectInjectionWorld.EntityManager;
                EntityQuery entityQuery2 = new EntityQueryBuilder(Allocator.Temp).WithAll<LocalTransform, Unit>().WithPresent<Selected>().WithAbsent<BrokenSquadTag>().Build(entityManager2);
                NativeArray<Entity> entityArray = entityQuery2.ToEntityArray(Allocator.Temp);
                NativeArray<LocalTransform> localTransformArray = entityQuery2.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                List<int> squadsInSelectionArea = new();

                for (int i = 0; i < localTransformArray.Length; i++)
                {
                    LocalTransform unitLocalTransform = localTransformArray[i];
                    Vector2 unitScreenPosition = Camera.main.WorldToScreenPoint(unitLocalTransform.Position);

                    if (GetSelectionAreaRect().Contains(unitScreenPosition))// Unit is inside the selection area
                    {
                        Unit unit = entityManager2.GetComponentData<Unit>(entityArray[i]);
                        if (CanThisTeamBeSelected(unit.Team))
                        {
                            if (!squadsInSelectionArea.Contains(unit.squadId)) squadsInSelectionArea.Add(unit.squadId);
                        }
                    }
                }
                entityQuery2.Dispose();
                unitSelectionManager.HoverSquadsOnSelectionArea(squadsInSelectionArea);
            }
            return;
        }

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery entityQuery = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        PhysicsWorldSingleton physicsWorldSingleton = entityQuery.GetSingleton<PhysicsWorldSingleton>();
        CollisionWorld collisionWorld = physicsWorldSingleton.CollisionWorld;
        UnityEngine.Ray cameraRay = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Set the radius for the spherecast simulation
        int numRays = 8; // Number of rays to simulate the spherecast
        float rayDistance = 9999f;

        // Define a filter for collisions
        CollisionFilter filter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = 1u << TabletopTavernConstants.UNITS_LAYER,
            GroupIndex = 0,
        };

        // Cast multiple rays in a circle pattern around the original ray
        bool hit = false;
        Unity.Physics.RaycastHit raycastHit = new Unity.Physics.RaycastHit();
        for (int j = 1; j <= steps; j++)
        {
            for (int i = 0; i < numRays; i++)
            {
                float angle = (i / (float)numRays) * Mathf.PI * 2;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * sphereRadius * j;

                RaycastInput raycastInput = new RaycastInput
                {
                    Start = cameraRay.GetPoint(0f) + offset,
                    End = cameraRay.GetPoint(rayDistance) + offset,
                    Filter = filter
                };

                if (collisionWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit tempHit))
                {
                    hit = true;
                    raycastHit = tempHit; // Store the hit data
                    break; // Exit loop on the first hit
                }
            }
        }

        int newHoverSquadId = 0;
        bool hitPlayerTeam = true;
        if (hit)
        {
            // Debug.Log($"Hit detected: {raycastHit.Entity}");
            if (entityManager.HasComponent<Unit>(raycastHit.Entity) && entityManager.HasComponent<Selected>(raycastHit.Entity))
            {
                // Hit a Unit
                Unit unit = entityManager.GetComponentData<Unit>(raycastHit.Entity);
                hitPlayerTeam = unit.Team == Team.Player;
                newHoverSquadId = unit.squadId;
            }
        }

        if (Physics.Raycast(cameraRay, out UnityEngine.RaycastHit hitInfo, Mathf.Infinity, flagLayerMask))
        {
            //if the tag is "SquadFlag" then set the newHoverSquadId to the squadId of the squad that the flag belongs to
            if (hitInfo.collider.CompareTag("SquadFlag"))
            {
                if (hitInfo.collider.TryGetComponent(out SquadFlagGameObject squadFlag))
                {
                    newHoverSquadId = squadFlag.SquadId;
                    hitPlayerTeam = newHoverSquadId > 0;
                }
            }
        }

        // If right-click just went down and the raycast missed, preserve the previous enemy hover
        // for one frame so the attack block in UnitSelectionManager can still fire.
        if (_rightClickDownThisFrame && newHoverSquadId == 0 && unitSelectionManager.PreviousHoveredSquad < 0)
        {
            entityQuery.Dispose();
            return;
        }

        if (newHoverSquadId != 0 && newHoverSquadId != unitSelectionManager.PreviousHoveredSquad)
        {//fix for hitting one unit immediately after another
            unitSelectionManager.HoverSquad(0, hitPlayerTeam);
        }

        unitSelectionManager.HoverSquad(newHoverSquadId, hitPlayerTeam);
        entityQuery.Dispose();
    }
    private bool CanThisTeamBeSelected(Team team) => team == Team.Player;
    private void OnDestroy() {
        if (BattleManager.HasInstance)
        {
            BattleManager.Instance.OnCursorModeChanged -= OnCursorModeChanged;
        }
        if (InputHandler.HasInstance)
        {
            InputHandler.Instance.OnAddUnitsToSelection -= AddUnitsToSelection;
            InputHandler.Instance.OnAddUnitsToSelectionCanceled -= CancelAddingUnitsToSelection;
        }
    }
}