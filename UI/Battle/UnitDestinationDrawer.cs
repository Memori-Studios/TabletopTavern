using UnityEngine;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Memori.Input;

namespace TJ
{
    public class UnitDestinationDrawer : MonoBehaviour
    {
        [SerializeField] private GameObject _unitDestinationPrefab;
        [SerializeField] private Color _playerColor, _invalidColor;
        private List<GameObject> _unitDestinationPool = new();
        private SquadManager _squadManager;
        private EntityManager _entityManager;
        private bool _isDisplaying;

        private void Start()
        {
            InputHandler.Instance.OnShowUnitMovement += DisplayUnitDestinations;
            // InputHandler.Instance.OnCancelShowUnitMovement += HideUnitDestinations;
            _squadManager = BattleManager.Instance.SquadManager;
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        private void Update()
        {
            if (!_isDisplaying) return;
            if (BattleMarkers.Hidden) TurnOffAllUnitDestinations();
            else RefreshUnitDestinations();
        }

        private void DisplayUnitDestinations()
        {
            _isDisplaying = !_isDisplaying;
            if (_isDisplaying)
                RefreshUnitDestinations();
            else
                TurnOffAllUnitDestinations();

        }

        private void RefreshUnitDestinations()
        {
            TurnOffAllUnitDestinations();

            NativeArray<SquadEntity> playerSquadEntities = _squadManager.RetrieveSquadEntities(ComponentType.ReadOnly<PlayerSquad>());
            NativeArray<SquadMovementComponent> playerSquadMovements = _squadManager.RetrieveSquadsMovement(ComponentType.ReadOnly<PlayerSquad>());

            for (int i = 0; i < playerSquadEntities.Length; i++)
            {
                SquadEntity squadEntity = playerSquadEntities[i];
                SquadMovementComponent squadMovement = playerSquadMovements[i];

                if (squadEntity.SquadCommand == SquadCommand.None) continue;
                if (!_entityManager.Exists(squadEntity.SelfEntity)) continue;
                if (!_entityManager.HasBuffer<EntityReferenceBufferElement>(squadEntity.SelfEntity)) continue;

                DynamicBuffer<EntityReferenceBufferElement> entityBuffer = _entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntity.SelfEntity);

                quaternion rotation;
                if (squadEntity.SquadCommand == SquadCommand.Move &&
                    _entityManager.HasBuffer<QueuedOrder>(squadEntity.SelfEntity))
                {
                    DynamicBuffer<QueuedOrder> orders = _entityManager.GetBuffer<QueuedOrder>(squadEntity.SelfEntity);
                    quaternion moveRotation = squadMovement.SquadRotation;
                    for (int j = 0; j < orders.Length; j++)
                    {
                        if (orders[j].Type == QueuedOrderType.Move)
                        {
                            moveRotation = orders[j].Rotation;
                            break;
                        }
                    }
                    rotation = moveRotation;
                }
                else
                {
                    rotation = squadMovement.SquadRotation;
                }

                for (int k = 0; k < entityBuffer.Length; k++)
                {
                    float3 rotatedOffset = math.mul(rotation, entityBuffer[k].PositionOffset);
                    Vector3 worldPosition = (Vector3)(squadMovement.GoalPosition + rotatedOffset) + Vector3.up * 0.1f;

                    GameObject unitDestination = GetDestinationPrefabFromPool();
                    unitDestination.transform.position = worldPosition;
                }
            }

            playerSquadEntities.Dispose();
            playerSquadMovements.Dispose();
        }

        #region Pooling
        private GameObject GetDestinationPrefabFromPool()
        {
            foreach (var unitDestination in _unitDestinationPool)
            {
                if (!unitDestination.activeInHierarchy)
                {
                    unitDestination.SetActive(true);
                    return unitDestination;
                }
            }
            GameObject newUnitDestination = Instantiate(_unitDestinationPrefab, Vector3.zero, Quaternion.identity);
            _unitDestinationPool.Add(newUnitDestination);
            return newUnitDestination;
        }
        private void ClearUnitDestinationPool()
        {
            foreach (var unitDestination in _unitDestinationPool)
            {
                Destroy(unitDestination);
            }
            _unitDestinationPool.Clear();
        }
        #endregion

        private void TurnOffAllUnitDestinations()
        {
            foreach (var unitDestination in _unitDestinationPool)
            {
                unitDestination.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (InputHandler.HasInstance)
            {
                InputHandler.Instance.OnShowUnitMovement -= DisplayUnitDestinations;
            }
            ClearUnitDestinationPool();
        }
    }
}
