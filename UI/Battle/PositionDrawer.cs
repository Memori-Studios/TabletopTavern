using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;
using Shapes;
using TJ.IrregularGrid;
using Unity.VisualScripting;
using TJ.Shapes;
using Memori.Localization;

namespace TJ
{
    public class PositionDrawer : MonoBehaviour
    {
        private BoxFormation formation;
        public BoxFormation Formation => formation;
        [SerializeField] private UnitPrefabPoint unitPointPrefab;
        BoxFormation boxFormation;
        Vector3 mousePosition;
        int unitCount;
        List<UnitPrefabPoint> unitPoints = new();
        // How many pooled points the current preview both activated AND positioned. Points past this
        // index can still be active while holding a stale world position, so nothing may validate or
        // consume them.
        private int activePointCount;
        public int ActivePointCount => activePointCount;
        [SerializeField] private Transform positionsParent;
        [SerializeField] private Transform positionsGrandParent;
        public Transform PositionsParent => positionsParent;
        public Transform PositionsGrandParent => positionsGrandParent;
        [SerializeField] private bool validPositions = true;
        public bool ValidPositions => validPositions;
        [SerializeField] private Quaternion lookRotation = Quaternion.Euler(0, 0, 0);

        [Header("Spawn Zones")]
        [SerializeField]
        private SpawnBox playerDeploymentZone = new()
        {
            min = new float3(-150, 0, -120),
            max = new float3(150, 0, -50)
        };
        [SerializeField]
        private SpawnBox enemyDeploymentZone = new()
        {
            min = new float3(-150, 0, 50),
            max = new float3(150, 0, 120)
        };
        [SerializeField]
        private SpawnBox battleZone = new()
        {
            min = new float3(-185, 0, -150),
            max = new float3(185, 0, 150)
        };
        public SpawnBox PlayerDeploymentZone => playerDeploymentZone;
        public SpawnBox EnemyDeploymentZone => enemyDeploymentZone;
        public SpawnBox BattleZone => battleZone;

        [SerializeField] private Polyline playerDeploymentZoneLine, enemyDeploymentZoneLine, battleZoneLine;
        [SerializeField] private Polyline secondaryPlayerDeploymentZoneLine;
        [SerializeField] private Polyline secondaryEnemyDeploymentZoneLine;
        [SerializeField] private Color validColor, invalidColor;
        [SerializeField] private LayerMask layerMask;
        private string positionError1, positionError2, positionError3, positionErrorBattle;
        private BattleLayoutType _layoutType;
        private SpawnBox _secondaryPlayerDeploymentZone;
        private SpawnBox _secondaryEnemyDeploymentZone;
        private GarrisonConcaveZone _garrisonZone;
        private bool _hasGarrisonZone;

        private void Awake()
        {
            boxFormation = GetComponentInChildren<BoxFormation>();
        }
        private void Start()
        {
            BattleManager.Instance.OnCursorModeChanged += OnCursorModeChanged;
            BattleManager.Instance.OnGamePhaseChanged += OnGamePhaseChanged;
            BattleManager.Instance.OnGateDestroyed += GateDestroyedHandler;
            formation = boxFormation;

            positionError1 = LocalizationManager.Instance.GetText("positionError");
            positionError2 = LocalizationManager.Instance.GetText("positionError2");
            positionError3 = LocalizationManager.Instance.GetText("positionError3");
            positionErrorBattle = LocalizationManager.Instance.GetText("positionErrorBattle");
        }

        /// <summary>
        /// Phase-appropriate copy for a rejected placement. The deployment wording talks about
        /// deployment zones and outriders, which is misleading once the battle has started.
        /// </summary>
        public string PositionErrorMessage =>
            BattleManager.Instance.GamePhase == GamePhase.Deployment ? positionError1 : positionErrorBattle;

        public void SetBattleLayout(BattleLayoutType layoutType)
        {
            _layoutType = layoutType;
            switch (layoutType)
            {
                case BattleLayoutType.PlayerEncircled:
                    playerDeploymentZone = new SpawnBox { min = new float3(-70, 0, -40), max = new float3(70, 0, 40) };
                    _secondaryEnemyDeploymentZone = new SpawnBox { min = new float3(-150, 0, -120), max = new float3(150, 0, -50) };
                    break;
                case BattleLayoutType.EnemyEncircled:
                    enemyDeploymentZone  = new SpawnBox { min = new float3(-70, 0, -40),   max = new float3(70, 0, 40) };
                    playerDeploymentZone = new SpawnBox { min = new float3(-150, 0, -120), max = new float3(150, 0, -60) };
                    _secondaryPlayerDeploymentZone = new SpawnBox { min = new float3(-150, 0, 60), max = new float3(150, 0, 120) };
                    break;
            }
            DrawZones();
        }

        [ContextMenu("Draw Zones")]
        public void DrawZones(bool isGarrison = false, GarrisonConcaveZone zone = default)
        {
            _hasGarrisonZone = isGarrison;
            if (isGarrison) _garrisonZone = zone;

            // layerMask = LayerMask.GetMask("Tile", "Water");
            //draw 4 points of the box
            Vector3[] points = new Vector3[80];
            //make 20 points along each side
            for (int i = 0; i < 20; i++)
            {
                Vector3 pointA = new Vector3(math.lerp(playerDeploymentZone.max.x, playerDeploymentZone.min.x, i / 20f), 0, playerDeploymentZone.min.z);
                points[i] = GetPointOnTerrain(pointA);
                Vector3 pointB = new Vector3(playerDeploymentZone.min.x, 0, math.lerp(playerDeploymentZone.min.z, playerDeploymentZone.max.z, i / 20f));
                points[i + 20] = GetPointOnTerrain(pointB);
                Vector3 pointC = new Vector3(math.lerp(playerDeploymentZone.min.x, playerDeploymentZone.max.x, i / 20f), 0, playerDeploymentZone.max.z);
                points[i + 40] = GetPointOnTerrain(pointC);
                Vector3 pointD = new Vector3(playerDeploymentZone.max.x, 0, math.lerp(playerDeploymentZone.max.z, playerDeploymentZone.min.z, i / 20f));
                points[i + 60] = GetPointOnTerrain(pointD);
            }
            playerDeploymentZoneLine.SetPoints(points);
            playerDeploymentZoneLine.gameObject.SetActive(true);

            if (isGarrison && zone.isDiagonalLayout)
            {
                // 4-sided trapezoid tracing the village layout: narrow front wall + 45° outward side walls.
                // Expanded 3 units outward so the line renders outside the wall geometry.
                const float o = 3f;
                float lcX      = zone.leftConnectorX  - o;
                float rcX      = zone.rightConnectorX + o;
                float wZ       = zone.wallZ    - o;
                float bkZ      = zone.battleMaxZ + o;
                float diagDepth = bkZ - wZ;
                float leftEndX  = lcX - diagDepth;
                float rightEndX = rcX + diagDepth;

                points = new Vector3[80];
                for (int i = 0; i < 20; i++)
                {
                    float t = i / 20f;
                    points[i]      = GetPointOnTerrain(new Vector3(math.lerp(lcX,       rcX,       t), 0, wZ));
                    points[20 + i] = GetPointOnTerrain(new Vector3(math.lerp(rcX,       rightEndX, t), 0, math.lerp(wZ, bkZ, t)));
                    points[40 + i] = GetPointOnTerrain(new Vector3(math.lerp(rightEndX, leftEndX,  t), 0, bkZ));
                    points[60 + i] = GetPointOnTerrain(new Vector3(math.lerp(leftEndX,  lcX,       t), 0, math.lerp(bkZ, wZ, t)));
                }
                enemyDeploymentZoneLine.SetPoints(points);
                enemyDeploymentZoneLine.gameObject.SetActive(true);
            }
            else if (isGarrison && !zone.isFlat && zone.isConvex)
            {
                // 8-segment convex closed zone: middle bumps forward, flanks recessed.
                // Expanded 3 units outward so the line renders outside the wall geometry.
                const float o = 3f;
                float minX = zone.battleMinX      - o;
                float maxX = zone.battleMaxX      + o;
                float lcX  = zone.leftConnectorX  - o;
                float rcX  = zone.rightConnectorX + o;
                float wZ   = zone.wallZ   - o;
                float mZ   = zone.middleZ - o;
                float bkZ  = zone.battleMaxZ + o;

                points = new Vector3[160];
                for (int i = 0; i < 20; i++)
                {
                    float t = i / 20f;
                    points[i]       = GetPointOnTerrain(new Vector3(math.lerp(lcX, rcX,  t), 0, wZ));
                    points[20 + i]  = GetPointOnTerrain(new Vector3(rcX, 0, math.lerp(wZ, mZ, t)));
                    points[40 + i]  = GetPointOnTerrain(new Vector3(math.lerp(rcX, maxX, t), 0, mZ));
                    points[60 + i]  = GetPointOnTerrain(new Vector3(maxX, 0, math.lerp(mZ, bkZ, t)));
                    points[80 + i]  = GetPointOnTerrain(new Vector3(math.lerp(maxX, minX, t), 0, bkZ));
                    points[100 + i] = GetPointOnTerrain(new Vector3(minX, 0, math.lerp(bkZ, mZ, t)));
                    points[120 + i] = GetPointOnTerrain(new Vector3(math.lerp(minX, lcX, t), 0, mZ));
                    points[140 + i] = GetPointOnTerrain(new Vector3(lcX, 0, math.lerp(mZ, wZ, t)));
                }
                enemyDeploymentZoneLine.SetPoints(points);
                enemyDeploymentZoneLine.gameObject.SetActive(true);
            }
            else if (isGarrison && !zone.isFlat)
            {
                // 8-segment concave closed zone matching the wall's U-shape.
                // Expanded 3 units outward so the line renders outside the wall geometry.
                const float o = 3f;
                float minX = zone.battleMinX      - o;
                float maxX = zone.battleMaxX      + o;
                float lcX  = zone.leftConnectorX  + o;
                float rcX  = zone.rightConnectorX - o;
                float wZ   = zone.wallZ   - o;
                float mZ   = zone.middleZ - o;
                float bkZ  = zone.battleMaxZ + o;

                points = new Vector3[160];
                for (int i = 0; i < 20; i++)
                {
                    float t = i / 20f;
                    points[i]       = GetPointOnTerrain(new Vector3(math.lerp(minX, lcX,  t), 0, wZ));
                    points[20 + i]  = GetPointOnTerrain(new Vector3(lcX, 0, math.lerp(wZ, mZ, t)));
                    points[40 + i]  = GetPointOnTerrain(new Vector3(math.lerp(lcX, rcX, t), 0, mZ));
                    points[60 + i]  = GetPointOnTerrain(new Vector3(rcX, 0, math.lerp(mZ, wZ, t)));
                    points[80 + i]  = GetPointOnTerrain(new Vector3(math.lerp(rcX, maxX, t), 0, wZ));
                    points[100 + i] = GetPointOnTerrain(new Vector3(maxX, 0, math.lerp(wZ, bkZ, t)));
                    points[120 + i] = GetPointOnTerrain(new Vector3(math.lerp(maxX, minX, t), 0, bkZ));
                    points[140 + i] = GetPointOnTerrain(new Vector3(minX, 0, math.lerp(bkZ, wZ, t)));
                }
                enemyDeploymentZoneLine.SetPoints(points);
                enemyDeploymentZoneLine.gameObject.SetActive(true);
            }
            else if (isGarrison)
            {
                // Flat garrison wall: rectangle expanded 3 units outward on all sides.
                const float o = 3f;
                float minX = zone.battleMinX  - o;
                float maxX = zone.battleMaxX  + o;
                float wZ   = zone.wallZ       - o;
                float bkZ  = zone.battleMaxZ  + o;

                points = new Vector3[80];
                for (int i = 0; i < 20; i++)
                {
                    float t = i / 20f;
                    points[i]      = GetPointOnTerrain(new Vector3(math.lerp(maxX, minX, t), 0, wZ));
                    points[20 + i] = GetPointOnTerrain(new Vector3(minX, 0, math.lerp(wZ, bkZ, t)));
                    points[40 + i] = GetPointOnTerrain(new Vector3(math.lerp(minX, maxX, t), 0, bkZ));
                    points[60 + i] = GetPointOnTerrain(new Vector3(maxX, 0, math.lerp(bkZ, wZ, t)));
                }
                enemyDeploymentZoneLine.SetPoints(points);
                enemyDeploymentZoneLine.gameObject.SetActive(true);
            }
            else
            {
                points = new Vector3[80];
                //make 20 points along each side
                for (int i = 0; i < 20; i++)
                {
                    Vector3 pointA = new Vector3(math.lerp(enemyDeploymentZone.max.x, enemyDeploymentZone.min.x, i / 20f), 0, enemyDeploymentZone.min.z);
                    points[i] = GetPointOnTerrain(pointA);
                    Vector3 pointB = new Vector3(enemyDeploymentZone.min.x, 0, math.lerp(enemyDeploymentZone.min.z, enemyDeploymentZone.max.z, i / 20f));
                    points[i + 20] = GetPointOnTerrain(pointB);
                    Vector3 pointC = new Vector3(math.lerp(enemyDeploymentZone.min.x, enemyDeploymentZone.max.x, i / 20f), 0, enemyDeploymentZone.max.z);
                    points[i + 40] = GetPointOnTerrain(pointC);
                    Vector3 pointD = new Vector3(enemyDeploymentZone.max.x, 0, math.lerp(enemyDeploymentZone.max.z, enemyDeploymentZone.min.z, i / 20f));
                    points[i + 60] = GetPointOnTerrain(pointD);
                }
                enemyDeploymentZoneLine.SetPoints(points);
                enemyDeploymentZoneLine.gameObject.SetActive(true);
            }

            points = new Vector3[80];
            //make 20 points along each side
            for (int i = 0; i < 20; i++)
            {
                Vector3 pointA = new Vector3(math.lerp(battleZone.max.x, battleZone.min.x, i / 20f), 0, battleZone.min.z);
                points[i] = GetPointOnTerrain(pointA);
                Vector3 pointB = new Vector3(battleZone.min.x, 0, math.lerp(battleZone.min.z, battleZone.max.z, i / 20f));
                points[i + 20] = GetPointOnTerrain(pointB);
                Vector3 pointC = new Vector3(math.lerp(battleZone.min.x, battleZone.max.x, i / 20f), 0, battleZone.max.z);
                points[i + 40] = GetPointOnTerrain(pointC);
                Vector3 pointD = new Vector3(battleZone.max.x, 0, math.lerp(battleZone.max.z, battleZone.min.z, i / 20f));
                points[i + 60] = GetPointOnTerrain(pointD);
            }
            battleZoneLine.SetPoints(points);
            battleZoneLine.gameObject.SetActive(true);

            if (secondaryPlayerDeploymentZoneLine != null)
            {
                if (_layoutType == BattleLayoutType.EnemyEncircled)
                {
                    points = new Vector3[80];
                    for (int i = 0; i < 20; i++)
                    {
                        Vector3 pointA = new Vector3(math.lerp(_secondaryPlayerDeploymentZone.max.x, _secondaryPlayerDeploymentZone.min.x, i / 20f), 0, _secondaryPlayerDeploymentZone.min.z);
                        points[i] = GetPointOnTerrain(pointA);
                        Vector3 pointB = new Vector3(_secondaryPlayerDeploymentZone.min.x, 0, math.lerp(_secondaryPlayerDeploymentZone.min.z, _secondaryPlayerDeploymentZone.max.z, i / 20f));
                        points[i + 20] = GetPointOnTerrain(pointB);
                        Vector3 pointC = new Vector3(math.lerp(_secondaryPlayerDeploymentZone.min.x, _secondaryPlayerDeploymentZone.max.x, i / 20f), 0, _secondaryPlayerDeploymentZone.max.z);
                        points[i + 40] = GetPointOnTerrain(pointC);
                        Vector3 pointD = new Vector3(_secondaryPlayerDeploymentZone.max.x, 0, math.lerp(_secondaryPlayerDeploymentZone.max.z, _secondaryPlayerDeploymentZone.min.z, i / 20f));
                        points[i + 60] = GetPointOnTerrain(pointD);
                    }
                    secondaryPlayerDeploymentZoneLine.SetPoints(points);
                    secondaryPlayerDeploymentZoneLine.gameObject.SetActive(true);
                }
                else
                {
                    secondaryPlayerDeploymentZoneLine.gameObject.SetActive(false);
                }
            }

            if (secondaryEnemyDeploymentZoneLine != null)
            {
                if (_layoutType == BattleLayoutType.PlayerEncircled)
                {
                    points = new Vector3[80];
                    for (int i = 0; i < 20; i++)
                    {
                        Vector3 pointA = new Vector3(math.lerp(_secondaryEnemyDeploymentZone.max.x, _secondaryEnemyDeploymentZone.min.x, i / 20f), 0, _secondaryEnemyDeploymentZone.min.z);
                        points[i]      = GetPointOnTerrain(pointA);
                        Vector3 pointB = new Vector3(_secondaryEnemyDeploymentZone.min.x, 0, math.lerp(_secondaryEnemyDeploymentZone.min.z, _secondaryEnemyDeploymentZone.max.z, i / 20f));
                        points[i + 20] = GetPointOnTerrain(pointB);
                        Vector3 pointC = new Vector3(math.lerp(_secondaryEnemyDeploymentZone.min.x, _secondaryEnemyDeploymentZone.max.x, i / 20f), 0, _secondaryEnemyDeploymentZone.max.z);
                        points[i + 40] = GetPointOnTerrain(pointC);
                        Vector3 pointD = new Vector3(_secondaryEnemyDeploymentZone.max.x, 0, math.lerp(_secondaryEnemyDeploymentZone.max.z, _secondaryEnemyDeploymentZone.min.z, i / 20f));
                        points[i + 60] = GetPointOnTerrain(pointD);
                    }
                    secondaryEnemyDeploymentZoneLine.SetPoints(points);
                    secondaryEnemyDeploymentZoneLine.gameObject.SetActive(true);
                }
                else
                {
                    secondaryEnemyDeploymentZoneLine.gameObject.SetActive(false);
                }
            }
        }
        public void DrawBiomes()
        {
            BiomeColliderRef[] biomeCenters = FindObjectsByType<BiomeColliderRef>(FindObjectsSortMode.None);
            for (int i = 0; i < biomeCenters.Length; i++)
            {
                BiomeCollider biomeCollider = biomeCenters[i].AddComponent<BiomeCollider>();

                biomeCollider.outlines.Add(biomeCollider.gameObject.AddComponent<QuickOutline.Outline>());
                //add outline to swamp center child as well
                foreach (Transform child in biomeCollider.transform)
                {
                    biomeCollider.outlines.Add(child.gameObject.AddComponent<QuickOutline.Outline>());
                }
                biomeCollider.SetUp(biomeCenters[i].biome);
            }
        }
        private Vector3 GetPointOnTerrain(Vector3 _origionalPoint)
        {
            //raycast down at point 
            // layerMask = LayerMask.NameToLayer("Tile", "Water");
            if (Physics.Raycast(new Vector3(_origionalPoint.x, 20, _origionalPoint.z), Vector3.down, out RaycastHit hit, 22, layerMask))
            {//~LayerMask.NameToLayer("Tile")
                return hit.point;
            }
            else
            {
                Debug.LogError("No terrain found");
                return _origionalPoint;
            }
        }
        public bool IsPositionInsideBox(float3 position, SpawnBox box)
        {
            return position.x >= box.min.x && position.x <= box.max.x &&
                position.z >= box.min.z && position.z <= box.max.z;
        }

        public List<float3> UnitPrefabPointPositions()
        {
            List<float3> positions = new();
            for (int i = 0; i < unitPoints.Count; i++)
            {
                positions.Add(unitPoints[i].transform.position);
            }
            return positions;
        }
        public void TurnOn(Vector3 _position, Dictionary<int, int> selectedSquadEntityAndEntitiesCountDict)
        {
            // Debug.Log($"Turning on position drawer at position: {_position}");
            SetMousePosition(_position);
            unitCount = 0;
            foreach (KeyValuePair<int, int> pair in selectedSquadEntityAndEntitiesCountDict)
            {
                unitCount += pair.Value;
            }

            // Debug.Log($"Turning on position drawer with lookRotation: {lookRotation}");
            positionsParent.SetLocalPositionAndRotation(mousePosition, lookRotation);

            MakePool();
            formation.SetUnitCounts(selectedSquadEntityAndEntitiesCountDict);
            // formation.CalculateUnitDepthAndWidth(formation.CachedDistance);
        }
        public void TurnOff()
        {
            // Debug.Log($"Turning off position drawer");
            activePointCount = 0;
            foreach (UnitPrefabPoint point in unitPoints)
            {
                point.gameObject.SetActive(false);
            }
        }
        public void PreviewSpawnFormation(Vector3 _position, int _unitCount, float _spread)
        {
            SetMousePosition(_position);
            unitCount = _unitCount;

            positionsParent.SetLocalPositionAndRotation(mousePosition, lookRotation);
            // Debug.Log($"settings rotation to {lookRotation}");

            MakePool();
            formation.CalculateUnitDepthAndWidthForSpawn(unitCount, _spread);
        }
        public void PreviewMoveFormation(Vector3 _position, List<SetDestination> _positions)
        {
            //nothing left alive to preview - showing the previous layout would just validate stale points
            if (_positions == null || _positions.Count == 0)
            {
                TurnOff();
                return;
            }

            SetMousePosition(_position);

            // Size the pool off the live destination list, never off the cached selection counts.
            // Battlefield casualties shrink the live list but not the cache, and any point activated
            // without being positioned keeps a stale world position that then fails the zone check
            // and silently kills the whole order.
            unitCount = _positions.Count;

            positionsParent.SetPositionAndRotation(mousePosition, positionsParent.rotation);

            MakePool();

            //reposition unitPoints to be at each entity's position
            int i = 0;
            foreach (SetDestination pos in _positions)
            {
                if (i >= unitCount) break;
                // Debug.Log($"  Point[{i}] destinationPosition={pos.destinationPosition}  squadPosition={pos.squadPosition}");
                unitPoints[i].transform.position = pos.destinationPosition;
                i++;
            }
            activePointCount = i;
        }
        public void MovePositionToMouse(Vector3 _position, bool overrideRotation = false)
        {
            SetMousePosition(_position);

            if (overrideRotation)
            {
                positionsParent.SetLocalPositionAndRotation(mousePosition, positionsParent.rotation);
            }
            else
            {
                positionsParent.SetLocalPositionAndRotation(mousePosition, lookRotation);
            }
        }
        public void ParentTheParented(Vector3 _formationCenter)
        {
            positionsGrandParent.SetLocalPositionAndRotation(_formationCenter, positionsParent.rotation);
            positionsParent.SetParent(positionsGrandParent);
        }
        public void UnParentTheParented()
        {
            positionsParent.SetParent(this.transform);
        }
        public void SetUnitPointsPositions()
        {
            // Debug.Log($"Setting unit point positions at mouse position: {mousePosition}");
            int i = 0;
            foreach (Vector3 pos in formation.PointPositions)
            {
                if (i >= unitCount) break;
                unitPoints[i].transform.localPosition = mousePosition + pos - positionsParent.localPosition;
                i++;
            }
            activePointCount = i;
        }
        public void MakePoolOnSpawn()
        {
            unitCount = 84;
            // mousePosition = formation.PointPositions[0];
            MakePool();
            foreach (UnitPrefabPoint point in unitPoints)
            {
                point.gameObject.SetActive(false);
            }
            SetUnitPointsPositions();
            // positionsParent.SetLocalPositionAndRotation(mousePosition, lookRotation);
        }
        private void MakePool()
        {
            activePointCount = unitCount;
            foreach (UnitPrefabPoint point in unitPoints)
            {
                point.gameObject.SetActive(false);
            }

            for (int i = 0; i < unitCount; i++)
            {
                if (i >= unitPoints.Count)
                {
                    unitPoints.Add(Instantiate(unitPointPrefab, positionsParent));
                }
                unitPoints[i].gameObject.SetActive(true);
                unitPoints[i].SelectedShape.GetComponent<ShapesBloom>().Bloom(validColor);
            }
        }
        public void ConfirmValidityOfPositions(Team _team, bool _outrider)
        {
            bool preBattleOutrider = _outrider && BattleManager.Instance.GamePhase == GamePhase.Deployment;
            bool GarrisonBattleWithWallsStillStanding = !BattleManager.Instance.AnyGateBreached && BattleManager.Instance.BattleSaveManager.IsGarrisonBattle;
            bool garrisonBattlePhase = GarrisonBattleWithWallsStillStanding && BattleManager.Instance.GamePhase != GamePhase.Deployment;

            if (preBattleOutrider || garrisonBattlePhase)
            {
                // Only points this preview actually positioned are meaningful - see activePointCount.
                for (int i = 0; i < activePointCount && i < unitPoints.Count; i++)
                {
                    UnitPrefabPoint point = unitPoints[i];
                    if (!point.gameObject.activeSelf) continue;

                    Vector3 pos = point.transform.position;

                    bool inEnemyZone;
                    if (_hasGarrisonZone && _team == Team.Player && (garrisonBattlePhase || preBattleOutrider))
                        inEnemyZone = _garrisonZone.IsInsideEnemyZone(pos.x, pos.z);
                    else
                    {
                        SpawnBox enemyBox = _team != Team.Player ? playerDeploymentZone : enemyDeploymentZone;
                        inEnemyZone = IsPositionInsideBox(pos, enemyBox);
                    }

                    if (inEnemyZone)
                    {
                        if (validPositions)
                        {
                            ColorPoints(invalidColor);
                            validPositions = false;
                            BattleManager.Instance.UIManager.ShowPositionError(true, positionError3);
                        }
                        return;
                    }
                    if (!IsPositionInsideBox(pos, battleZone))
                    {
                        if (validPositions)
                        {
                            ColorPoints(invalidColor);
                            validPositions = false;
                            BattleManager.Instance.UIManager.ShowPositionError(true, positionError2);
                        }
                        return;
                    }
                }

                if (!validPositions)
                {
                    ColorPoints(validColor);
                    validPositions = true;
                    BattleManager.Instance.UIManager.ShowPositionError(false, "");
                }
                return;
            }

            // Debug.Log($"Confirming validity of positions for team {_team}");
            SpawnBox box = _team == Team.Player ? playerDeploymentZone : enemyDeploymentZone;
            
            //allow movement in enemy deployment zone during battle phase
            box = BattleManager.Instance.GamePhase == GamePhase.Deployment ? box : battleZone;

            // Only points this preview actually positioned are meaningful - see activePointCount.
            for (int i = 0; i < activePointCount && i < unitPoints.Count; i++)
            {
                UnitPrefabPoint point = unitPoints[i];
                if (point.gameObject.activeSelf)
                {
                    bool inPrimary = IsPositionInsideBox(point.transform.position, box);
                    bool inSecondary = _layoutType == BattleLayoutType.EnemyEncircled
                                   && _team == Team.Player
                                   && BattleManager.Instance.GamePhase == GamePhase.Deployment
                                   && IsPositionInsideBox(point.transform.position, _secondaryPlayerDeploymentZone);
                    if (!inPrimary && !inSecondary)
                    {
                        if (validPositions)
                        {
                            ColorPoints(invalidColor);
                            validPositions = false;
                            BattleManager.Instance.UIManager.ShowPositionError(true, PositionErrorMessage);
                            Debug.Log($"Order rejected - point {i} of {activePointCount} at {point.transform.position} is outside the allowed area during {BattleManager.Instance.GamePhase}");
                        }
                        return;
                    }
                }
            }

            if (!validPositions)
            {
                // Debug.Log("Valid positions");
                ColorPoints(validColor);
                validPositions = true;
                BattleManager.Instance.UIManager.ShowPositionError(false, "");
            }
        }
        private void ColorPoints(Color color)
        {
            foreach (UnitPrefabPoint point in unitPoints)
            {
                // point.SelectedShape.Color = color;
                // point.SpawningShape.Color = color;
                point.SelectedShape.GetComponent<ShapesBloom>().Bloom(color);
            }
        }
        private void OnCursorModeChanged(CursorMode _cursorMode)
        {
            if (_cursorMode != CursorMode.SpawnSquad) return;

            bool isPlayerSpawn = BattleManager.Instance.SpawnManager.Team == Team.Player;
            // Debug.Log($"Cursor Mode Changed to: {_cursorMode}. Is Player Spawn: {isPlayerSpawn}");
            SetLookRotation(Quaternion.Euler(0, 90f * (isPlayerSpawn ? -1 : 1), 0));
            positionsParent.SetLocalPositionAndRotation(mousePosition, lookRotation);
        }
        private void OnGamePhaseChanged(GamePhase _gamePhase)
        {
            Debug.Log($"Game Phase Changed to: {_gamePhase}");
            switch (_gamePhase)
            {
                case GamePhase.Deployment:
                    playerDeploymentZoneLine.gameObject.SetActive(true);
                    enemyDeploymentZoneLine.gameObject.SetActive(true);
                    break;
                case GamePhase.Battle:
                    playerDeploymentZoneLine.gameObject.SetActive(false);
                    if (secondaryPlayerDeploymentZoneLine != null)
                        secondaryPlayerDeploymentZoneLine.gameObject.SetActive(false);
                    if (secondaryEnemyDeploymentZoneLine != null)
                        secondaryEnemyDeploymentZoneLine.gameObject.SetActive(false);
                    if (!BattleManager.Instance.BattleSaveManager.IsGarrisonBattle)
                    {
                        enemyDeploymentZoneLine.gameObject.SetActive(false);
                    }
                    break;
            }
        }
        public void GateDestroyedHandler(int gateIndex)
        {
            enemyDeploymentZoneLine.gameObject.SetActive(false);
        }
        public void SetLookRotation(Quaternion _lookRotation)
        {
            // Debug.Log($"Setting look rotation to: {_lookRotation.eulerAngles}");
            lookRotation = _lookRotation;
        }
        private void SetMousePosition(Vector3 _position)
        {
            // Debug.Log($"Setting mouse position to: {_position}");
            //make sure none of the values are NaN
            if (float.IsNaN(_position.x))
            {
                _position.x = 0;
            }
            if (float.IsNaN(_position.y))
            {
                _position.y = 0;
            }
            if (float.IsNaN(_position.z))
            {
                _position.z = 0;
            }
            mousePosition = _position;
        }
        private void OnDestroy() 
        {
            if(BattleManager.HasInstance)
            {
                BattleManager.Instance.OnCursorModeChanged -= OnCursorModeChanged;
                BattleManager.Instance.OnGamePhaseChanged -= OnGamePhaseChanged;
                BattleManager.Instance.OnGateDestroyed -= GateDestroyedHandler;
            }
        }
    }
}