using UnityEngine;
using Shapes;
using System.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using Unity.Entities;
using TJ.Shapes;
using Memori.Input;

namespace TJ
{
    public class AttackArrowDrawer : MonoBehaviour
    {
        [System.Serializable] enum ArrowState { Off, Hovered, Selected }
        [System.Serializable] enum ArrowToggleState { ToggledOff, ToggledOn }
        [System.Serializable] enum SquadDestinationType { Movement, Attack }

        [Header("State")]
        [SerializeField] private ArrowState _activeArrowState;
        [SerializeField] private ArrowToggleState _arrowToggleState;
        [SerializeField] private bool isRanged;
        [SerializeField] private SquadDestinationType _squadDestinationType;

        [Header("References")]
        [SerializeField] private GameObject _arrowParent;
        [SerializeField] private Polyline movementLine;
        [SerializeField] private Polyline archerAttackArc;
        [SerializeField] private ShapeRenderer pointTriangle;
        // The footprint a mage's spell will actually cover, drawn at the far end of the arrow.
        // Optional only in the sense that a missing reference is reported rather than thrown;
        // every caster needs it.
        [SerializeField] private Disc blastRadiusRing;

        [Header("Settings")]
        [SerializeField] private Color attackColor, movementColor;
        [SerializeField] private AnimationCurve polylineCurve;

        //Local
        private EntityManager EntityManager;
        private SquadEntity squadEntity;
        private ShapesBloom movementLineBloom, triangleBloom, archerRangeBloom;
        private int archerAttackArcPoints = 20;
        private float polylineHeightMultiplier = 7;
        private Vector3 startPoint;
        private List<Vector3> _destinationPoints = new List<Vector3>();
        private bool isInRangedFire = false;
        private bool _hasValidPath = false;
        private ShapesBloom blastRingBloom;
        private bool _isCaster = false;
        private bool _hasBlastRing = false;
        // Resolving a squad id to its entity is not free (see TryGetHoverPreviewCenter), so the last
        // hovered squad is cached. 0 is "no squad hovered" in UIManager and no real squad carries it.
        private int _previewHoveredSquadId = 0;
        private Entity _previewHoveredEntity = Entity.Null;
        // Lifts the blast ring clear of the ground it is drawn on. Matches the 0.5 the Archer
        // Range Drawer prefab authors on its own ground discs.
        private const float BLAST_RING_GROUND_OFFSET = 0.5f;


        private void Start()
        {
            InputHandler.Instance.OnShowUnitMovement += ToggleArrowSateToToggledOn;
            // InputHandler.Instance.OnCancelShowUnitMovement += ToggleArrowSateToToggledOff;
        }
        private void ToggleArrowSateToToggledOn()
        {
            if(_arrowToggleState == ArrowToggleState.ToggledOff)
                SetArrowToggleState(ArrowToggleState.ToggledOn);
            else
                SetArrowToggleState(ArrowToggleState.ToggledOff);
        }
        // private void ToggleArrowSateToToggledOff()
        // {
        //     SetArrowToggleState(ArrowToggleState.ToggledOff);
        // }

        private void SetArrowState(ArrowState arrowState)
        {
            _activeArrowState = arrowState;
            switch (arrowState)
            {
                case ArrowState.Off:
                    if(_arrowToggleState == ArrowToggleState.ToggledOff)
                    {
                        TurnOffArrow();
                    }
                    break;
                case ArrowState.Hovered:
                    if(_arrowToggleState == ArrowToggleState.ToggledOff)
                    {
                        TurnOnArrow();
                    }
                    break;
                case ArrowState.Selected:
                    if(_arrowToggleState == ArrowToggleState.ToggledOff)
                    {
                        TurnOnArrow();
                    }
                    break;
            }
        }
        private void SetArrowToggleState(ArrowToggleState arrowToggleState)
        {
            _arrowToggleState = arrowToggleState;
            switch (arrowToggleState)
            {
                case ArrowToggleState.ToggledOn:
                    if(_activeArrowState == ArrowState.Off)
                    {
                        TurnOnArrow();
                    }
                    break;
                case ArrowToggleState.ToggledOff:
                    if(_activeArrowState == ArrowState.Off)
                    {
                        TurnOffArrow();
                    }
                    break;
            }
        }
        public void SetUp(SquadEntity _squadEntity)
        {
            EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            squadEntity = _squadEntity;

            if (squadEntity.SquadId < 0)
            {
                Destroy(gameObject);
                return;
            }

            UnitType unitType = TabletopTavernData.Instance.GetUnitTypeFromUnitName(squadEntity.UnitName);
            isRanged = unitType != UnitType.Melee;
            _isCaster = TabletopTavernConstants.Casts(unitType);
            movementLineBloom = movementLine.GetComponent<ShapesBloom>();
            triangleBloom = pointTriangle.GetComponent<ShapesBloom>();
            archerRangeBloom = archerAttackArc.GetComponent<ShapesBloom>();
            gameObject.name = $"AttackArrow_{squadEntity.SquadId}_{squadEntity.UnitName}";

            SetUpBlastRadiusRing();

            SetArrowState(ArrowState.Off);
            SetArrowToggleState(ArrowToggleState.ToggledOff);
            TurnOffRangedFire();
        }

        #region Caster

        // SpellRadius applies at the target, not at the caster, which is why this is drawn at the
        // arrow's far end rather than as a second ring around the mage - a ring on the caster would
        // read as a minimum range or a self-aura. ArcherRangeDrawer.ApplyCasterShape declines to
        // draw it there for exactly that reason.
        private void SetUpBlastRadiusRing()
        {
            if (blastRadiusRing == null)
            {
                if (_isCaster)
                    Debug.LogError($"AttackArrowDrawer: {squadEntity.UnitName} is a caster but the Attack Arrow prefab has no blastRadiusRing assigned - its spell footprint will not be drawn.");
                return;
            }

            if (!_isCaster)
            {
                blastRadiusRing.gameObject.SetActive(false);
                return;
            }

            TJ.Spells.SpellData mageSpell = TabletopTavernData.Instance.SquadAssetsDictionary[squadEntity.UnitName].mageSpell;
            float blastRadius = mageSpell == null ? 0f : mageSpell.SpellRadius;

            // A single-target spell, or a caster whose SquadData has no mageSpell at all (which
            // EntityWatcher already logs), has no footprint worth drawing - a zero-radius ring would
            // just be a dot sitting on the target.
            _hasBlastRing = blastRadius > 0f;
            if (_hasBlastRing)
            {
                blastRadiusRing.Radius = blastRadius;
                blastRingBloom = blastRadiusRing.GetComponent<ShapesBloom>();
            }

            blastRadiusRing.gameObject.SetActive(false);
        }

        // A mage never receives FormationEngagedInRangedCombat: MageSquadChargeSystem deliberately
        // withholds it, because SquadEngageInCombatSystem consumes that tag by nulling
        // TargetSquadEntity - right for an archer, which re-acquires every volley, and wrong for a
        // mage, which would drop the target it just crossed the field for. So the casting state is
        // derived here instead, from the same conditions MageCastSystem gates a real cast on, minus
        // its cooldown timer, which would strobe the arc on and off once per cast.
        private bool TryGetCastTargetCenter(out Vector3 targetCenter)
        {
            targetCenter = Vector3.zero;
            Entity self = squadEntity.SelfEntity;

            // SquadRanOutOfAmmoSystem strips MageSquad when the last charge is spent and converts
            // the squad to a melee body, so its presence is also the "still a caster" test.
            if (!EntityManager.HasComponent<MageSquad>(self)) return false;
            if (EntityManager.HasComponent<InCombat>(self)) return false;
            if (EntityManager.HasComponent<CeaseFireTag>(self) && EntityManager.IsComponentEnabled<CeaseFireTag>(self)) return false;

            // Read live rather than off the cached squadEntity copy, which was snapshotted at SetUp
            // and never carries a target.
            Entity target = EntityManager.GetComponentData<SquadEntity>(self).TargetSquadEntity;
            if (!EntityManager.Exists(target)) return false;
            if (EntityManager.HasComponent<BrokenSquadTag>(target)) return false;
            if (!EntityManager.HasComponent<SquadMovementComponent>(target)) return false;

            // The target's centre is exactly the Position MageCastSystem puts in its cast request, so
            // the ring lands where the spell will. Taken from the target rather than from the arrow's
            // own end point, which is the last queued order's goal and may be a Move the player
            // stacked after the attack.
            float3 center = EntityManager.GetComponentData<SquadMovementComponent>(target).SquadCenter;
            float3 selfCenter = EntityManager.GetComponentData<SquadMovementComponent>(self).SquadCenter;
            if (math.distance(selfCenter, center) > EntityManager.GetComponentData<MageSquad>(self).AttackRange) return false;

            targetCenter = center;
            return true;
        }

        // Position only - the prefab authors the flat 90 degree rotation and every parent up to the
        // root sits at identity, so local and world agree.
        private void PositionBlastRadiusRing(Vector3 impactPoint)
        {
            blastRadiusRing.transform.position = impactPoint + Vector3.up * BLAST_RING_GROUND_OFFSET;
        }

        // The single owner of the blast ring's visibility, position and colour. Nothing else may
        // touch it: TurnOnRangedFire / TurnOffRangedFire / RecalculateArrowPath all used to, and any
        // one of them would clobber a preview shown while the arrow itself is off.
        //
        // The ring is parented to the prefab root rather than to Arrow Parent for the same reason -
        // Arrow Parent is deactivated wholesale whenever the arrow is hidden.
        private void UpdateBlastRadiusRing(bool isCasting, Vector3 castTargetCenter)
        {
            if (!_hasBlastRing) return;

            // A live cast outranks a preview: where the spell is actually going matters more than
            // where one would go.
            Vector3 impactPoint = castTargetCenter;
            bool show = isCasting || TryGetHoverPreviewCenter(out impactPoint);

            blastRadiusRing.gameObject.SetActive(show);
            if (!show) return;

            PositionBlastRadiusRing(impactPoint);
            blastRingBloom.SetColor(attackColor);
            blastRingBloom.Bloom();
        }

        // The footprint is also worth seeing BEFORE an order exists: a player deciding which squad
        // to send a mage at wants to know what a cast would cover. Shown on the hovered enemy
        // whenever this caster is selected.
        private bool TryGetHoverPreviewCenter(out Vector3 center)
        {
            center = Vector3.zero;

            if (!BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId)) return false;

            // Negative ids are the enemy, 0 is nothing hovered. The right-click that issues a cast
            // order only accepts an enemy squad, so previewing on a friendly would advertise an
            // order the player cannot actually give.
            int hoveredSquadId = BattleManager.Instance.UIManager.HoveredSquadId;
            if (hoveredSquadId >= 0) return false;

            // A spent caster has had MageSquad stripped by SquadRanOutOfAmmoSystem and can cast
            // nothing, so it should stop offering a preview even though it is still a Mage by type.
            if (!EntityManager.HasComponent<MageSquad>(squadEntity.SelfEntity)) return false;

            // GetSquadEntityFromId builds an EntityQuery and a NativeArray on every call, so it is
            // paid only when the hovered squad changes, not once per frame per selected mage.
            if (hoveredSquadId != _previewHoveredSquadId)
            {
                _previewHoveredSquadId = hoveredSquadId;
                _previewHoveredEntity = BattleManager.Instance.SquadManager.GetSquadEntityFromId(hoveredSquadId, true).SelfEntity;
            }

            if (!EntityManager.Exists(_previewHoveredEntity)) return false;
            if (!EntityManager.HasComponent<SquadMovementComponent>(_previewHoveredEntity)) return false;

            center = EntityManager.GetComponentData<SquadMovementComponent>(_previewHoveredEntity).SquadCenter;
            return true;
        }

        #endregion

        private void Update()
        {
            bool PassesSanityChecks()
            {
                if (BattleManager.Instance.GamePhase == GamePhase.PostGame) return false;

                if (!EntityManager.Exists(squadEntity.SelfEntity))
                {
                    Destroy(gameObject);
                    return false;
                }

                if (!EntityManager.HasComponent<SquadMovementComponent>(squadEntity.SelfEntity))
                {
                    Debug.Log($"wtf {squadEntity.SelfEntity}");
                    return false;
                }

                if (EntityManager.HasComponent<BrokenSquadTag>(squadEntity.SelfEntity))
                {
                    Destroy(gameObject);
                    return false;
                }

                return true;
            }
            
            if (!PassesSanityChecks()) return;

            // Resolved before the no-orders early-out below, because an unordered mage while the
            // player hovers an enemy is exactly the case the blast preview exists for. It is also
            // why UpdateBlastRadiusRing is the sole owner of the ring's visibility: the arrow's own
            // on/off path cannot express "no arrow, but still show the footprint".
            bool isCasting = false;
            Vector3 castTargetCenter = Vector3.zero;
            if (_isCaster)
            {
                isCasting = TryGetCastTargetCenter(out castTargetCenter);
                UpdateBlastRadiusRing(isCasting, castTargetCenter);
            }

            DynamicBuffer<QueuedOrder> queuedOrders = EntityManager.GetBuffer<QueuedOrder>(squadEntity.SelfEntity);
            if (queuedOrders.Length == 0)
            {
                // Debug.Log($"[AttackArrow] Squad {squadEntity.SquadId}: SquadCommand is None → turning off");
                if(_activeArrowState != ArrowState.Off)
                {
                    _activeArrowState = ArrowState.Off;
                    if (_arrowToggleState == ArrowToggleState.ToggledOff)
                        TurnOffArrow();
                }
                _hasValidPath = false;
                pointTriangle.gameObject.SetActive(false);
                return;
            }
            if (_arrowToggleState == ArrowToggleState.ToggledOn)
                TurnOnArrow();

            SquadMovementComponent squadMovementComponent = EntityManager.GetComponentData<SquadMovementComponent>(squadEntity.SelfEntity);
            startPoint = squadMovementComponent.SquadCenter;
            _destinationPoints.Clear();
            if(queuedOrders.Length > 0)
            {
                _squadDestinationType = SquadDestinationType.Movement;
                foreach(QueuedOrder currentOrder in queuedOrders)
                {
                    if(currentOrder.Type == QueuedOrderType.Move)
                    {
                        _destinationPoints.Add(currentOrder.Goal);
                    }
                    else if (currentOrder.Type == QueuedOrderType.Attack)
                    {
                        //get squad to attack position from squad manager
                        int targetSquadId = currentOrder.TargetSquadId;
                        SquadEntity targetSquadEntity = BattleManager.Instance.SquadManager.GetSquadEntityFromId(targetSquadId, true);
                        if(targetSquadEntity.SelfEntity == Entity.Null)
                        {
                            // Debug.Log($"[AttackArrow] Squad {squadEntity.SquadId}: Attack order target ID {targetSquadId} not found → turning off");
                            if(_activeArrowState != ArrowState.Off)
                                SetArrowState(ArrowState.Off);

                            return;
                        }

                        if(EntityManager.Exists(targetSquadEntity.SelfEntity) && EntityManager.HasComponent<SquadMovementComponent>(targetSquadEntity.SelfEntity))
                        {
                            SquadMovementComponent targetSquadMovementComponent = EntityManager.GetComponentData<SquadMovementComponent>(targetSquadEntity.SelfEntity);
                            _destinationPoints.Add(targetSquadMovementComponent.SquadCenter);
                        }
                        else
                        {
                            // Debug.Log($"[AttackArrow] Squad {squadEntity.SquadId}: Attack target {targetSquadId} exists={EntityManager.Exists(targetSquadEntity.SelfEntity)} but missing SquadMovementComponent → no destination added");
                        }
                        _squadDestinationType = SquadDestinationType.Attack;
                    }
                }
            }
            else
            {
                // Debug.Log($"[AttackArrow] Squad {squadEntity.SquadId}: No queued orders (SquadCommand={squadCommand}) → turning off");
                if(_activeArrowState != ArrowState.Off)
                    SetArrowState(ArrowState.Off);
                return;
            }

            // Applied ahead of RecalculateArrowPath, unlike the archer branch further down, so the
            // arc is right on the frame casting starts instead of a frame later. isCasting itself
            // was resolved at the top of Update, above the no-orders early-out.
            if (_isCaster)
            {
                if (isCasting && !isInRangedFire) TurnOnRangedFire();
                else if (!isCasting && isInRangedFire) TurnOffRangedFire();
            }

            SetArrowColor();
            RecalculateArrowPath();

            bool isSelected = BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Contains(squadEntity.SquadId);
            bool isHovered = BattleManager.Instance.UIManager.HoveredSquadId == squadEntity.SquadId;
            // Debug.Log($"[AttackArrow] Squad {squadEntity.SquadId}: isSelected={isSelected} isHovered={isHovered}");

            ArrowState desiredState = ArrowState.Off;
            if (isSelected) desiredState = ArrowState.Selected;
            else if (isHovered) desiredState = ArrowState.Hovered;

            // Debug.Log($"[AttackArrow] Squad {squadEntity.SquadId}: cmd={squadCommand} selected={isSelected} hovered={isHovered} activeState={_activeArrowState} desiredState={desiredState} toggle={_arrowToggleState}");

            if (_activeArrowState != desiredState)
            {
                SetArrowState(desiredState);
                // Debug.Log($"[AttackArrow] Squad {squadEntity.SquadId}: Arrow state changed to {desiredState}");
            }

            // A caster drives its own firing state above; it can never hold this tag, but keeping
            // it out of this branch means a future change to who gets the tag cannot double-drive it.
            if(isRanged && !_isCaster)
            {
                if(!isInRangedFire && EntityManager.HasComponent<FormationEngagedInRangedCombat>(squadEntity.SelfEntity))
                {
                    TurnOnRangedFire();
                }
                else if (!EntityManager.HasComponent<FormationEngagedInRangedCombat>(squadEntity.SelfEntity) && isInRangedFire)
                {
                    TurnOffRangedFire();
                }
            }
        }
        public void SwitchToMelee(bool _toMelee)
        {
            // A caster has no melee mode. SquadManager.SetMeleeMode skips only UnitType.Melee, so
            // a mage caught in a mixed selection would have its firing state latched off here and
            // stop drawing its cast arc for the rest of the battle. The out-of-charges call from
            // SquadFlagGameObject is covered for free: TryGetCastTargetCenter reads MageSquad live,
            // and SquadRanOutOfAmmoSystem has already stripped it by then.
            if (_isCaster) return;

            isRanged = !_toMelee;
            if (_toMelee && isInRangedFire)
            {
                TurnOffRangedFire();
                TurnOffArrow();
                return;
            }

            TurnOffArrow();
            TurnOnArrow();
        }
        private void TurnOnArrow()
        {
            if(_arrowParent == null) return;

            _arrowParent.SetActive(true);
            // Debug.Log($"[AttackArrow] Squad {squadEntity.SquadId}: Arrow turned ON");
        }
        private void TurnOffArrow()
        {
            if(_arrowParent == null) return;
            
            _arrowParent.SetActive(false);
            // Debug.Log($"[AttackArrow] Squad {squadEntity.SquadId}: Arrow turned OFF");
        }
        private void TurnOnRangedFire()
        {
            isInRangedFire = true;
            movementLine.gameObject.SetActive(false);
            archerAttackArc.gameObject.SetActive(true);
            pointTriangle.gameObject.SetActive(_hasValidPath);
        }
        private void TurnOffRangedFire()
        {
            isInRangedFire = false;
            movementLine.gameObject.SetActive(true);
            archerAttackArc.gameObject.SetActive(false);
            pointTriangle.gameObject.SetActive(_hasValidPath);
        }
        private void RecalculateArrowPath()
        {
            if (_destinationPoints.Count == 0) return;

            //this needs to be changed to support multiple destination points
            Vector3[] points = new Vector3[_destinationPoints.Count + 1];
            points[0] = startPoint;
            for (int i = 0; i < _destinationPoints.Count; i++)
            {
                points[i + 1] = _destinationPoints[i];
            }
            movementLine.SetPoints(points);
            movementLine.UpdateMesh(true);
            
            pointTriangle.transform.position = points[^1] - (points[^1] - points[^2]).normalized;
            pointTriangle.transform.LookAt(points[^1] + (points[^1] - points[^2]));
            // pointTriangle.transform.Rotate(0, 180, 0);
            _hasValidPath = true;
            pointTriangle.gameObject.SetActive(true);

            if (isInRangedFire)
            {
                DrawArcherArc(points[0] + Vector3.up * 4, (points[^1] - (points[^1] - points[^2]).normalized * 3) + Vector3.up * 4);
            } 
        }
        private void DrawArcherArc(Vector3 start, Vector3 end)
        {
            //polyline will have archerAttackArcPoints points from start to end
            Vector3[] points = new Vector3[archerAttackArcPoints];
            for (int i = 0; i < archerAttackArcPoints; i++)
            {
                float t = i / (archerAttackArcPoints - 1f);
                points[i] = Vector3.Lerp(start, end, t);
                points[i].y += polylineCurve.Evaluate(t) * polylineHeightMultiplier;
            }
            archerAttackArc.SetPoints(points);
            archerAttackArc.UpdateMesh(true);

            //move to the 19th point and look at the 20th point
            pointTriangle.transform.position = points[19];
            pointTriangle.transform.LookAt(points[19] + (points[19] - points[18]));
            // pointTriangle.transform.Rotate(0, 180, 0);
        }
        private void SetArrowColor()
        {
            Color color = (_squadDestinationType == SquadDestinationType.Attack) ? attackColor : movementColor;
            movementLineBloom.SetColor(color);
            triangleBloom.SetColor(color);
            archerRangeBloom.SetColor(color);

            movementLineBloom.Bloom();
            triangleBloom.Bloom();
            archerRangeBloom.Bloom();
        }
        private void OnDestroy()
        {
            if (InputHandler.HasInstance)
            {
                InputHandler.Instance.OnShowUnitMovement -= ToggleArrowSateToToggledOn;
                // InputHandler.Instance.OnCancelShowUnitMovement -= ToggleArrowSateToToggledOff;
            }
        }
    }
}