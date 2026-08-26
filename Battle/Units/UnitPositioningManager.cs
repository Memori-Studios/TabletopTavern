using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using System.Collections.Generic;
using TJ;
using Memori.Audio;
using ProjectDawn.Navigation;
using Memori.Utilities;
using Memori.Notifications;
using Memori.Localization;

public class UnitPositioningManager : MonoBehaviour
{
    PositionDrawer positionDrawer;
    UnitSelectionManager unitSelectionManager;
    Unity.Mathematics.Random random;
    BattleInputManager battleInputManager;

    private void Start()
    {
        positionDrawer = BattleManager.Instance.PositionDrawer;
        unitSelectionManager = BattleManager.Instance.UnitSelectionManager;
        random = new Unity.Mathematics.Random(1);
        battleInputManager = BattleInputManager.Instance;
    }
    private void TeleportUnits(bool _generateNoise)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        quaternion desiredRotation = quaternion.AxisAngle(math.up(), (battleInputManager.Angle+90) * Mathf.Deg2Rad);
        NativeArray<float3> movePositionArray = positionDrawer.UnitPrefabPointPositions().ToNativeArray(Allocator.Temp);
        int unitIndexOffset = 0;

        foreach (KeyValuePair<int, List<Entity>> kvp in unitSelectionManager.GetSelectedSquadIDsAndAllUnits())
        {
            Unit unit = entityManager.GetComponentData<Unit>(kvp.Value[0]);
            SquadEntity squadEntity = BattleManager.Instance.SquadManager.GetSquadEntityFromId(kvp.Key);
            if (squadEntity.SelfEntity != Entity.Null)
            {
                entityManager.GetBuffer<QueuedOrder>(squadEntity.SelfEntity).Clear();
                squadEntity.TargetSquadEntity = Entity.Null;
                entityManager.SetComponentData(squadEntity.SelfEntity, squadEntity);
                if (entityManager.HasComponent<StartChargeTag>(squadEntity.SelfEntity))
                    entityManager.RemoveComponent<StartChargeTag>(squadEntity.SelfEntity);
                if (entityManager.HasComponent<ChargeSquad>(squadEntity.SelfEntity))
                    entityManager.RemoveComponent<ChargeSquad>(squadEntity.SelfEntity);
                if (entityManager.HasComponent<IssueSquadCommand>(squadEntity.SelfEntity))
                    entityManager.RemoveComponent<IssueSquadCommand>(squadEntity.SelfEntity);
            }
            for (int i = 0; i < kvp.Value.Count; i++)
            {
                Entity entity = kvp.Value[i];
                UnitPosition unitPosition = entityManager.GetComponentData<UnitPosition>(entity);
                float3 unitPositionNoise = _generateNoise ? TabletopTavernData.Instance.GetNoiseFromUnitName(unit.unitName, movePositionArray[unitPosition.unitIndex + unitIndexOffset]) : movePositionArray[unitPosition.unitIndex + unitIndexOffset];

                entityManager.SetComponentData(entity, new SetDestination
                {
                    squadPosition = unitPositionNoise,
                    delayRemaining = 0
                });
                entityManager.SetComponentData(entity, new RotateUnit
                {
                    targetRotation = desiredRotation,
                });
                entityManager.SetComponentData(entity, new UnitPosition
                {
                    unitIndex = i
                });
                entityManager.SetComponentData(entity, LocalTransform.FromPositionRotation(unitPositionNoise, desiredRotation));
            }

            int2 widthAndDepth = positionDrawer.Formation.GetWidthAndDepth(kvp.Key);
            BattleManager.Instance.SquadManager.AssignWidthAndDepthToSquad(widthAndDepth, kvp.Key);

            SquadMovementComponent squadMovementComponent = entityManager.GetComponentData<SquadMovementComponent>(unit.squadEntity);
            LocalTransform squadEntityTransform = entityManager.GetComponentData<LocalTransform>(unit.squadEntity);
            squadEntityTransform.Position = GetAverageUnitPositionOfSquad(squadMovementComponent);
            squadMovementComponent.SetRotation(desiredRotation);
            entityManager.SetComponentData(unit.squadEntity, squadEntityTransform);
            entityManager.SetComponentData(unit.squadEntity, squadMovementComponent);

            unitIndexOffset += kvp.Value.Count;
        }
        movePositionArray.Dispose();
        positionDrawer.TurnOff();
        IAudioRequester.Instance.PlaySFX(SFXData.RepositionCommand);
    }
    //rewrote entire function 8/22 to make sure that the destination position matches point prefab
    public void QueueSquadCommand(SquadCommand _squadCommand, bool _addToQueue, int _squadToAttack = 0)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // if (_squadCommand == SquadCommand.Attack && _squadToAttack != 0)
        // {
        //     SquadEntity targetSquadEntity = BattleManager.Instance.SquadManager.GetSquadEntityFromId(_squadToAttack);
        //     if (targetSquadEntity.SelfEntity != Entity.Null && entityManager.HasComponent<GarrisonDefenderComponent>(targetSquadEntity.SelfEntity))
        //     {
        //         bool anyRangedOrArtillery = false;
        //         foreach (int squadId in unitSelectionManager.GetSelectedSquadIDsAndAllUnits().Keys)
        //         {
        //             SquadEntity se = BattleManager.Instance.SquadManager.GetSquadEntityFromId(squadId);
        //             if (se.SelfEntity == Entity.Null) continue;
        //             UnitType ut = TabletopTavernData.Instance.GetUnitTypeFromUnitName(se.UnitName);
        //             if (ut == UnitType.Ranged || ut == UnitType.Artillery)
        //             {
        //                 anyRangedOrArtillery = true;
        //                 break;
        //             }
        //         }
        //
        //         if (!anyRangedOrArtillery)
        //         {
        //             Debug.LogError($"QueueSquadCommand: Cannot attack squad {_squadToAttack} — it is a garrison defender.");
        //             string localizedNotificaiton = LocalizationManager.Instance.GetText("CannotAttackProtectedByWalls");
        //             NotificationManager.Instance.DisplayNotification(localizedNotificaiton);
        //             return;
        //         }
        //     }
        // }

        if (_squadCommand == SquadCommand.Attack)
            IAudioRequester.Instance.PlaySFX(SFXData.AttackCommand);

        var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
        NativeArray<float3> movePositionArray = positionDrawer.UnitPrefabPointPositions().ToNativeArray(Allocator.Temp);

        int unitIndexOffset = 0;
        bool chargeSFXPlayed = false;
        foreach(KeyValuePair<int, List<Entity>> kvp in unitSelectionManager.GetSelectedSquadIDsAndAllUnits())
        {
            // Debug.Log($"Requesting Queueing {_squadCommand} command to squad {kvp.Key} to attack squad {_squadToAttack}");

            SquadEntity squadEntity = BattleManager.Instance.SquadManager.GetSquadEntityFromId(kvp.Key);
            if(squadEntity.SelfEntity == Entity.Null)
            {
                Debug.LogError($"QueueSquadCommand: Could not find squad entity for squad id {kvp.Key}");
                continue;
            }
            QueuedOrder queuedOrder = new ();

            if(_squadCommand == SquadCommand.Move)
            {
                float3 averagePosition = float3.zero;
                quaternion desiredRotation = quaternion.AxisAngle(math.up(), (battleInputManager.Angle + 90) * Mathf.Deg2Rad);

                // The pool was laid out per squad in this same order, but only ActivePointCount points
                // were positioned. Reading past that returns stale coordinates, so clamp instead.
                int availablePoints = Mathf.Clamp(positionDrawer.ActivePointCount - unitIndexOffset, 0, kvp.Value.Count);
                if(availablePoints == 0)
                {
                    Debug.LogWarning($"QueueSquadCommand: no preview points for squad {kvp.Key} (pool has {positionDrawer.ActivePointCount}, offset {unitIndexOffset}) - skipping move");
                    unitIndexOffset += kvp.Value.Count;
                    continue;
                }

                for(int i = 0; i < availablePoints; i++)
                {
                    averagePosition += movePositionArray[i + unitIndexOffset];
                }
                averagePosition /= availablePoints;
                averagePosition = new float3(averagePosition.x, 0, averagePosition.z);
                unitIndexOffset += kvp.Value.Count;

                queuedOrder = new ()
                {
                    Type = QueuedOrderType.Move,
                    Goal = averagePosition,
                    Rotation = desiredRotation,
                    WidthAndDepth = positionDrawer.Formation.GetWidthAndDepth(squadEntity.SquadId)
                };
            }
            else if(_squadCommand == SquadCommand.Attack)
            {
                if (!chargeSFXPlayed)
                {
                    IAudioRequester.Instance.PlaySFX(TabletopTavernData.Instance.GetRandomChargeSFX(squadEntity.UnitName));
                    chargeSFXPlayed = true;
                }
                queuedOrder = new ()
                {
                    Type = QueuedOrderType.Attack,
                    TargetSquadId = _squadToAttack
                };
            }
            else if(_squadCommand == SquadCommand.HaltAndFreeze) 
            {
                // Debug.Log($"Queueing HaltAndFreeze command to squad {squadEntity.SquadId}");

                SquadMovementComponent squadMovementComponent = entityManager.GetComponentData<SquadMovementComponent>(squadEntity.SelfEntity);
                
                queuedOrder = new ()
                {
                    Type = QueuedOrderType.Move,
                    Goal = squadMovementComponent.SquadCenter,
                    Rotation = squadMovementComponent.SquadRotation,
                    WidthAndDepth = positionDrawer.Formation.GetWidthAndDepth(squadEntity.SquadId)
                };
            }

            if(!_addToQueue)
            {
                DynamicBuffer<QueuedOrder> existingQueuedOrders = entityManager.GetBuffer<QueuedOrder>(squadEntity.SelfEntity);
                existingQueuedOrders.Clear();
            }

            //if adding to queue and is attack, make sure not to add duplicate attack orders
            if(_addToQueue && queuedOrder.Type == QueuedOrderType.Attack) 
            {
                DynamicBuffer<QueuedOrder> existingQueuedOrders = entityManager.GetBuffer<QueuedOrder>(squadEntity.SelfEntity);
                bool duplicateFound = false;
                for(int i = 0; i < existingQueuedOrders.Length; i++) 
                {
                    if(existingQueuedOrders[i].Type == QueuedOrderType.Attack && existingQueuedOrders[i].TargetSquadId == queuedOrder.TargetSquadId) 
                    {
                        duplicateFound = true;
                        break;
                    }
                }
                if(!duplicateFound) 
                {
                    Debug.Log($"Adding to back of queue order of type {queuedOrder.Type} to squad {squadEntity.SquadId}");
                    entityCommandBuffer.AppendToBuffer(squadEntity.SelfEntity, queuedOrder);
                }
            }
            else
            {
                // Debug.Log($"Overwriting queued order of type {queuedOrder.Type} to squad {squadEntity.SquadId}");
                entityCommandBuffer.AppendToBuffer(squadEntity.SelfEntity, queuedOrder);
            }
        }

        movePositionArray.Dispose();
        entityCommandBuffer.Playback(entityManager);
        entityCommandBuffer.Dispose();
        positionDrawer.TurnOff();
    }
    public void OrderSquadToDestination(SquadEntity _squadEntity, SquadDestination _squadDestination, EntityCommandBuffer? externalEcb = null)
    {
        // Debug.Log($"OrderSquadToDestination: {_squadEntity.SquadId}");
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (!entityManager.HasComponent<EnemySquad>(_squadEntity.SelfEntity))
            IAudioRequester.Instance.PlaySFX(SFXData.RepositionCommand);
            
        bool ownEcb = !externalEcb.HasValue;
        EntityCommandBuffer entityCommandBuffer = externalEcb ?? new EntityCommandBuffer(Allocator.Temp);
        SquadMovementComponent SquadMovementComponent = entityManager.GetComponentData<SquadMovementComponent>(_squadEntity.SelfEntity);

        _squadEntity.TargetSquadEntity = Entity.Null;
        entityCommandBuffer.SetComponent(_squadEntity.SelfEntity, _squadEntity);

        //check if has component
        if(entityManager.HasComponent<ChargeSquad>(_squadEntity.SelfEntity)) {
            entityCommandBuffer.RemoveComponent<ChargeSquad>(_squadEntity.SelfEntity);
        }
        if (
            entityManager.HasComponent<InCombat>(_squadEntity.SelfEntity) ||
            entityManager.HasComponent<FormationEngagedInRangedCombat>(_squadEntity.SelfEntity))
        {
            entityCommandBuffer.SetComponentEnabled<DisengageFromCombat>(_squadEntity.SelfEntity, true);
        }

        if(!entityManager.HasComponent<SquadMoveOverrideTag>(_squadEntity.SelfEntity))
        {
            entityCommandBuffer.AddComponent(_squadEntity.SelfEntity, new SquadMoveOverrideTag() { DistanceGoal = 0 });
        }

        //actually wtf was this
        // Debug.Log($"SquadMoveOverrideTag already exists on squad {_squadEntity.SquadId}!!!!!!!!!!!!!!");
        List<Entity> squadUnits = BattleManager.Instance.SquadManager.GetEntitiesFromSquad(_squadEntity.SquadId);

        //TODO: THIS IS THE ISSUE

        float3 newCenterPosition = float3.zero;
            int unitCount = squadUnits.Count;


            float3[] movePositionArrayForSquad = new float3[unitCount];
            float3[] currentPositions = new float3[unitCount];

            float spread = TabletopTavernData.Instance.GetUnitSpreadFromUnitName(_squadEntity.UnitName);

            List<float3> entityPositions = BattleManager.Instance.PositionDrawer.Formation.GeneratePositionsForSquad(_squadDestination.WidthAndDepth, unitCount, spread);

            if (entityPositions.Count < unitCount)
            {
                Debug.LogWarning($"[OrderSquadToDestination] Squad {_squadEntity.SquadId} ({_squadEntity.UnitName}): formation grid {_squadDestination.WidthAndDepth.x}x{_squadDestination.WidthAndDepth.y} = {_squadDestination.WidthAndDepth.x * _squadDestination.WidthAndDepth.y} slots < {unitCount} units. Clamping.");
                unitCount = entityPositions.Count;
                movePositionArrayForSquad = new float3[unitCount];
                currentPositions = new float3[unitCount];
            }

            for (int i = 0; i < unitCount; i++) {
                entityPositions[i] = math.mul(_squadDestination.DestinationRotation, entityPositions[i]);
                movePositionArrayForSquad[i] = entityPositions[i] + _squadDestination.DestinationPosition;
                currentPositions[i] = entityManager.Exists(squadUnits[i])
                    ? entityManager.GetComponentData<LocalTransform>(squadUnits[i]).Position
                    : float3.zero;
            }

            int[] assignments = HungarianAlgorithm.AssignPositions(currentPositions, movePositionArrayForSquad);

            for (int i = 0; i < unitCount; i++)
            {
                random = new Unity.Mathematics.Random(random.NextUInt());
                Entity entity = squadUnits[i];
                if (entityManager.Exists(entity) == false) continue;

                float3 newPosition = movePositionArrayForSquad[assignments[i]];

                entityCommandBuffer.SetComponent(entity, new SetDestination
                {
                    destinationPosition = newPosition,
                    squadPosition = newPosition,
                    delayRemaining = random.NextFloat(0.1f, 0.3f)
                });
                newCenterPosition += newPosition;

                entityCommandBuffer.SetComponent(entity, new UnitPosition
                {
                    unitIndex = i
                });

                entityCommandBuffer.SetComponent(entity, new RotateUnit
                {
                    targetRotation = _squadDestination.DestinationRotation,
                });

                entityManager.SetComponentEnabled<MoveOverride>(entity, true);
                entityCommandBuffer.SetComponent(entity, new Target { targetEntity = Entity.Null });
            }

            // Debug.Log($"Assigning width and depth {_squadDestination.WidthAndDepth} to squad {kvp.Key}");
            BattleManager.Instance.SquadManager.AssignWidthAndDepthToSquad(_squadDestination.WidthAndDepth, _squadEntity.SquadId);


        //update squad movement component goal position and rotation
        SquadMovementComponent.GoalPosition = _squadDestination.DestinationPosition;
        quaternion desiredRotation = quaternion.LookRotationSafe(SquadMovementComponent.GoalPosition - SquadMovementComponent.SquadCenter, math.up());
        SquadMovementComponent.SetRotation(desiredRotation);
        entityCommandBuffer.SetComponent(_squadEntity.SelfEntity, SquadMovementComponent);

        if (ownEcb) entityCommandBuffer.Playback(entityManager);
    }
    public void OrderSquadToAttack(SquadEntity squadReceivingAttackOrder, Entity _targetSquadEntity, bool _enemySquad, EntityCommandBuffer? externalEcb = null)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        bool ownEcb = !externalEcb.HasValue;
        var entityCommandBuffer = externalEcb ?? new EntityCommandBuffer(Allocator.Temp);

        if (!entityManager.Exists(_targetSquadEntity))
        {
            if (ownEcb) entityCommandBuffer.Dispose();
            return;
        }

        SquadEntity targetSquad = entityManager.GetComponentData<SquadEntity>(_targetSquadEntity);
        // Debug.Log($"OrderSquadToAttack: {squadReceivingAttackOrder.SquadId} attacking {targetSquad.SquadId}");

        //Check to prevent attacking self
        if (squadReceivingAttackOrder.TargetSquadEntity == squadReceivingAttackOrder.SelfEntity)
        {
            Debug.LogError($"OrderSquadToAttack: Squad {squadReceivingAttackOrder.SquadId} cannot attack itself");
            return;
        }

        if(entityManager.HasComponent<FormationEngagedInRangedCombat>(squadReceivingAttackOrder.SelfEntity))
        {
            if(!entityManager.HasComponent<InCombat>(squadReceivingAttackOrder.SelfEntity))
            {
                DisengageFromCombat disengageFromCombat = entityManager.GetComponentData<DisengageFromCombat>(squadReceivingAttackOrder.SelfEntity);
                disengageFromCombat.newTargetSquad = targetSquad.SelfEntity;
                entityCommandBuffer.SetComponent(squadReceivingAttackOrder.SelfEntity, disengageFromCombat);
                entityCommandBuffer.SetComponentEnabled<DisengageFromCombat>(squadReceivingAttackOrder.SelfEntity, true);
                Debug.Log($"Squad {squadReceivingAttackOrder.SquadId} is in ranged combat, disengaging before new attack");
            }
        }
        if(entityManager.HasComponent<InCombat>(squadReceivingAttackOrder.SelfEntity))
        {
            DisengageFromCombat disengageFromCombat = entityManager.GetComponentData<DisengageFromCombat>(squadReceivingAttackOrder.SelfEntity);
            disengageFromCombat.newTargetSquad = targetSquad.SelfEntity;
            entityCommandBuffer.SetComponent(squadReceivingAttackOrder.SelfEntity, disengageFromCombat);
            entityCommandBuffer.SetComponentEnabled<DisengageFromCombat>(squadReceivingAttackOrder.SelfEntity, true);
            Debug.Log($"Squad {squadReceivingAttackOrder.SquadId} is in combat, disengaging first");
        }

        squadReceivingAttackOrder.TargetSquadEntity = targetSquad.SelfEntity;
        entityCommandBuffer.AddComponent(squadReceivingAttackOrder.SelfEntity, squadReceivingAttackOrder);

        SquadMovementComponent squadMovementComponent = entityManager.GetComponentData<SquadMovementComponent>(squadReceivingAttackOrder.SelfEntity);
        SquadMovementComponent targetsquadMovementComponent = entityManager.GetComponentData<SquadMovementComponent>(targetSquad.SelfEntity);

        //move squadpositions to target squad center
        squadMovementComponent.GoalPosition = targetsquadMovementComponent.SquadCenter;
        entityCommandBuffer.SetComponent(squadReceivingAttackOrder.SelfEntity, squadMovementComponent);

        entityCommandBuffer.AddComponent<StartChargeTag>(squadReceivingAttackOrder.SelfEntity);
        // Debug.Log($"Squad {squadReceivingAttackOrder.SquadId} has been issued an attack order against squad {targetSquad.SquadId}");

        //if it has the move override tag, remove it
        if (entityManager.HasComponent<SquadMoveOverrideTag>(squadReceivingAttackOrder.SelfEntity))
            entityCommandBuffer.AddComponent<CancelSquadMoveOverrideTag>(squadReceivingAttackOrder.SelfEntity);

        if(!_enemySquad)
        {
            if (BattleManager.Instance.GamePhase == GamePhase.Battle)
            {
                entityCommandBuffer.SetComponentEnabled<WaitingForCommand>(squadReceivingAttackOrder.SelfEntity, false);
            }
        }


        entityCommandBuffer.SetComponentEnabled<CeaseFireTag>(squadReceivingAttackOrder.SelfEntity, false);

        SquadOverridesComponent squadOverrides = entityManager.GetComponentData<SquadOverridesComponent>(squadReceivingAttackOrder.SelfEntity);
        if (squadOverrides.CeaseFire)
        {
            squadOverrides.CeaseFire = false;
            entityCommandBuffer.SetComponent(squadReceivingAttackOrder.SelfEntity, squadOverrides);

            if (!_enemySquad)
                BattleManager.Instance.UIManager.RefreshSelectedSquadButtonStates();
        }

        // Debug.Log($"Squad {squad.SquadId} is rotating to face target squad {targetSquadEntity.SquadId}");
        if (ownEcb) entityCommandBuffer.Playback(entityManager);
    }
    public void OrderSquadToWithdraw(SquadEntity _squadEntity, bool skirmishRetreat = false, EntityCommandBuffer? externalEcb = null)
    {
        Debug.Log($"Ordering squad {_squadEntity.SquadId} to withdraw" + (skirmishRetreat ? ": Skirmish Retreat" : ""));
        static float3 GetWithdrawPosition(float3 squadCenter, bool playerSquad, bool skirmishRetreat)
        {
            float3 withdrawPosition = squadCenter;
            int distance = skirmishRetreat ? 20+(int)withdrawPosition.z : TabletopTavernConstants.WITHDRAW_DISTANCE;
            int direction = playerSquad ? -1 : 1;
            withdrawPosition.z = distance * direction;
            return withdrawPosition;
        }

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        bool ownEcb = !externalEcb.HasValue;
        EntityCommandBuffer ecb = externalEcb ?? new EntityCommandBuffer(Allocator.Temp);
        if(!entityManager.HasComponent<SquadMovementComponent>(_squadEntity.SelfEntity))
        {
            Debug.LogError($"OrderSquadToWithdraw: Squad {_squadEntity.SquadId} does not have a SquadMovementComponent");
            return;
        }
        SquadMovementComponent SquadMovementComponent = entityManager.GetComponentData<SquadMovementComponent>(_squadEntity.SelfEntity);

        entityManager.GetBuffer<QueuedOrder>(_squadEntity.SelfEntity).Clear();

        _squadEntity.TargetSquadEntity = Entity.Null;
        if (!skirmishRetreat)
        {
            SquadOverridesComponent squadOverridesComponent = entityManager.GetComponentData<SquadOverridesComponent>(_squadEntity.SelfEntity);
            squadOverridesComponent.GuardMode = true;
            entityManager.SetComponentData(_squadEntity.SelfEntity, squadOverridesComponent);
            ecb.AddComponent(_squadEntity.SelfEntity, new WithdrawSquadTag());
            Debug.Log($"Squad {_squadEntity.SquadId} is retreating");
        }

        entityManager.SetComponentData(_squadEntity.SelfEntity, _squadEntity);

        //check if has component
        if (entityManager.HasComponent<ChargeSquad>(_squadEntity.SelfEntity))
        {
            ecb.RemoveComponent<ChargeSquad>(_squadEntity.SelfEntity);
        }

        ecb.SetComponentEnabled<DisengageFromCombat>(_squadEntity.SelfEntity, true);

        float3 startingPosition = SquadMovementComponent.SquadCenter;
        float3 goalPosition = GetWithdrawPosition(startingPosition, _squadEntity.SquadId > 0, skirmishRetreat);
        ecb.AddComponent(_squadEntity.SelfEntity, new SquadMoveOverrideTag() { DistanceGoal = 0 });
        List<Entity> squadUnits = BattleManager.Instance.SquadManager.GetEntitiesFromSquad(_squadEntity.SquadId);

        //Set squad movement component goal position and rotation to face away from enemy
        SquadMovementComponent.GoalPosition = goalPosition;
        quaternion desiredRotation = quaternion.LookRotationSafe(goalPosition - SquadMovementComponent.SquadCenter, math.up());
        SquadMovementComponent.SetRotation(desiredRotation);
        entityManager.SetComponentData(_squadEntity.SelfEntity, SquadMovementComponent);

        ecb.AddComponent<RecalculatePositionsForUnitsCharging>(_squadEntity.SelfEntity);


        if (!skirmishRetreat)
        {
            IAudioRequester.Instance.PlaySFX(
                TabletopTavernData.Instance.GetRandomRetreatSFX(_squadEntity.UnitName)
            );
        }

        foreach(Entity entity in squadUnits)
        {
            if(entityManager.Exists(entity) == false) continue;

            entityManager.SetComponentData(entity, new Target { targetEntity = Entity.Null });
            ecb.SetComponentEnabled<MoveOverride>(entity, true);
            if(!skirmishRetreat)
                ecb.SetComponentEnabled<RetreatingUnit>(entity, true);
        }

        if (ownEcb) ecb.Playback(entityManager);
    }
    public void OrderSquadToHalt(SquadEntity _squadEntity, EntityCommandBuffer? externalEcb = null)
    {
        // Debug.Log($"Ordering squad {_squadEntity.SquadId} to halt");
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        bool ownEcb = !externalEcb.HasValue;
        var entityCommandBuffer = externalEcb ?? new EntityCommandBuffer(Allocator.Temp);

        entityCommandBuffer.AddComponent(_squadEntity.SelfEntity, new HaltCommandTag { DropTarget = true, FreezePosition = true });
        if (ownEcb) entityCommandBuffer.Playback(entityManager);
    }
    public void OrderSquadToHaltAndFreeze(SquadEntity squadEntity, EntityCommandBuffer? externalEcb = null)
    {
        // Debug.Log($"Ordering squad {squadEntity.SquadId} to halt and freeze");
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        bool ownEcb = !externalEcb.HasValue;
        var entityCommandBuffer = externalEcb ?? new EntityCommandBuffer(Allocator.Temp);

        entityCommandBuffer.AddComponent(squadEntity.SelfEntity, new HaltCommandTag { DropTarget = true, FreezePosition = true });

        //turn on guard mode
        SquadOverridesComponent squadOverridesComponent = entityManager.GetComponentData<SquadOverridesComponent>(squadEntity.SelfEntity);
        squadOverridesComponent.GuardMode = true;
        entityManager.SetComponentData(squadEntity.SelfEntity, squadOverridesComponent);
        if (ownEcb) entityCommandBuffer.Playback(entityManager);
    }
    public void UpdateSquadPositionsOnCharge(Entity _squadEntity)
    {
        // Debug.Log($"Updating squad positions on charge for squad {_squadEntity}");
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        SquadMovementComponent squadEntity = entityManager.GetComponentData<SquadMovementComponent>(_squadEntity);
        DynamicBuffer<EntityReferenceBufferElement> entityBuffer = entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntity.SelfEntity);

        float3 direction = math.normalizesafe(squadEntity.GoalPosition - squadEntity.SquadCenter);
        quaternion rotation = quaternion.LookRotationSafe(direction, math.up());
        // float3 distanceToMove = squadEntity.GoalPosition - squadEntity.SquadCenter;

        for (int i = 0; i < entityBuffer.Length; i++)
        {
            if (!entityManager.Exists(entityBuffer[i].Entity)) continue;

            // Rotate offset to face goal
            float3 rotatedOffset = math.mul(rotation, entityBuffer[i].PositionOffset);
            SetDestination setDestination = entityManager.GetComponentData<SetDestination>(entityBuffer[i].Entity);
            //check for nans
            if (rotatedOffset.x is float.NaN || rotatedOffset.y is float.NaN || rotatedOffset.z is float.NaN)
            {
                rotatedOffset = float3.zero;
                Debug.LogError($"Rotated offset contained NaN values for entity {entityBuffer[i].Entity}!!!!");
            }
            setDestination.squadPosition = squadEntity.GoalPosition + rotatedOffset;
            entityManager.SetComponentData(entityBuffer[i].Entity, setDestination);

            entityManager.SetComponentData(entityBuffer[i].Entity, new RotateUnit { targetRotation = rotation });
            entityManager.SetComponentEnabled<RotateUnit>(entityBuffer[i].Entity, true);
        }
    }
    public void IssueSquadMoveCommand(bool _generateNoise, bool _addToQueue)
    {
        // Debug.Log($"Issuing squad move command with _generateNoise={_generateNoise} and _addToQueue={_addToQueue}");
        if(unitSelectionManager.SelectedSquadIds.Count != 0)
        {
            IAudioRequester.Instance.PlaySFX(
                TabletopTavernData.Instance.GetRandomBarkSFX(
                    unitSelectionManager.SelectedSquadUnitNames[0]
            ));
        }
        switch(BattleManager.Instance.GamePhase) {
            case GamePhase.Deployment:
                TeleportUnits(_generateNoise);
                break;
            case GamePhase.Battle:
                QueueSquadCommand(SquadCommand.Move, _addToQueue);
                break;
        }
    }
    public float3 GetAverageUnitPositionOfSquad(SquadMovementComponent _squadEntity)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        DynamicBuffer<EntityReferenceBufferElement> entityBuffer = entityManager.GetBuffer<EntityReferenceBufferElement>(_squadEntity.SelfEntity);
        float3 averagePosition = float3.zero;
        int liveCount = 0;
        for (int i = 0; i < entityBuffer.Length; i++) {
            if (!entityManager.Exists(entityBuffer[i].Entity)) continue;
            averagePosition += entityManager.GetComponentData<SetDestination>(entityBuffer[i].Entity).squadPosition;
            // averagePosition += entityManager.GetComponentData<LocalTransform>(entityBuffer[i].Entity).Position;
            liveCount++;
        }
        return liveCount > 0 ? averagePosition / liveCount : float3.zero;
    }
}
