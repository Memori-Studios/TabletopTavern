using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TJ
{
    public class GarrisonWallsGenerator : MonoBehaviour
    {
        [SerializeField] private PositionDrawer positionDrawer;
        [SerializeField] private GarrisonWallsSO editorConfig;

        private Transform wallsParent;
        private int _gateCount = 0;
        [SerializeField] private GarrisonWallsSO _activeConfig;
        private readonly List<(Vector3 pos, int index, GameObject gateGO)> _pendingGates = new();
        private GarrisonConcaveZone _concaveZone;
        public GarrisonConcaveZone ConcaveZone => _concaveZone;

        private struct WallSegmentData
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool isConnector;
        }

        public void PlaceGarrisonWalls(GarrisonWallsSO config)
        {
            if (config == null)
            {
                Debug.LogError("GarrisonWallsGenerator: GarrisonWallsSO is null.");
                return;
            }
            if (config.wallSegmentPrefabs == null || config.wallSegmentPrefabs.Count == 0)
            {
                Debug.LogWarning("GarrisonWallsGenerator: No wall segment prefabs assigned.");
                return;
            }

            _gateCount = 0;
            _activeConfig = config;
            _pendingGates.Clear();
            wallsParent = new GameObject("GarrisonWalls").transform;
            wallsParent.SetParent(transform);

            SpawnBox battleZone = positionDrawer.BattleZone;
            float minX = battleZone.min.x;
            float maxX = battleZone.max.x;
            float wallZ = config.wallLineZ;

            float totalWidth = maxX - minX;
            int totalSegments = Mathf.FloorToInt(totalWidth / config.wallSegmentWidth) + 2;
            if (totalSegments <= 2)
            {
                Debug.LogWarning($"GarrisonWallsGenerator: Battlefield width {totalWidth} too narrow for segment width {config.wallSegmentWidth}.");
                return;
            }

            float usedWidth = (totalSegments - 2) * config.wallSegmentWidth;
            float startX = minX + (totalWidth - usedWidth) / 2f - config.wallSegmentWidth;

            bool flat = !config.useStraightSideWalls && (config.wallInwardDepth <= 0f || totalSegments < 4);
            int lc = totalSegments / 3;
            int mc = totalSegments / 3;
            float segW = config.wallSegmentWidth;
            _concaveZone = new GarrisonConcaveZone
            {
                wallZ           = config.wallLineZ,
                middleZ         = config.useStraightSideWalls ? battleZone.max.z : config.wallLineZ + config.wallInwardDepth,
                leftConnectorX  = startX + lc * segW,
                rightConnectorX = startX + (lc + mc) * segW,
                battleMinX      = minX,
                battleMaxX      = maxX,
                battleMaxZ      = battleZone.max.z,
                isFlat          = flat,
                isDiagonalLayout = config.useStraightSideWalls,
                isConvex         = config.useConvexWalls && !flat,
            };

            List<WallSegmentData> segments = BuildWallSegmentList(
                startX, totalSegments, config.wallSegmentWidth, wallZ, config.wallInwardDepth,
                battleZone.max.z, config.useStraightSideWalls, config.useConvexWalls,
                out List<Vector3> towerPositions);

            HashSet<int> gateIndices = BuildGateIndices(segments, config.numberOfGates, config.gatePrefab != null);

            for (int i = 0; i < segments.Count; i++)
            {
                WallSegmentData seg = segments[i];
                if (gateIndices.Contains(i))
                {
                    if (config.gatePrefab != null)
                        SpawnGateWithEntity(seg.position, config);
                }
                else
                {
                    GameObject prefab = GetRandomWallSegment(config.wallSegmentPrefabs);
                    if (prefab != null)
                        SpawnWallPiece(prefab, seg.position, seg.rotation);
                }
            }

            if (config.towerPrefab != null)
                foreach (Vector3 tp in towerPositions)
                    SpawnWallPiece(config.towerPrefab, tp, WallRotation);
        }

        private static readonly Quaternion WallRotation      = Quaternion.Euler(0f, 180f, 0f);
        private static readonly Quaternion ConnectorRotation = Quaternion.Euler(0f,  90f, 0f);

        private static List<WallSegmentData> BuildWallSegmentList(
            float startX, int totalSegments, float segW, float wallZ, float inwardDepth,
            float battleMaxZ, bool useStraightSideWalls, bool useConvexWalls,
            out List<Vector3> towerPositions)
        {
            towerPositions = new List<Vector3>();
            var result = new List<WallSegmentData>();
            float CenterX(int idx) => startX + idx * segW + segW / 2f;

            if (useStraightSideWalls)
            {
                int sideLeftCount   = totalSegments / 3;
                int sideMidCount    = totalSegments / 3;

                float sideLeftX  = startX + sideLeftCount * segW;
                float sideRightX = startX + (sideLeftCount + sideMidCount) * segW;

                // Front wall (middle section only)
                for (int i = 0; i < sideMidCount; i++)
                    result.Add(new WallSegmentData { position = new Vector3(CenterX(sideLeftCount + i), 0f, wallZ), rotation = WallRotation });

                // Side walls angled 45° outward from each end of the front wall to the back of the battlefield
                const float diag     = 0.7071f;
                float diagStep       = segW * diag;
                float sideStartZ     = wallZ      + diagStep * 0.5f;
                float sideEndZ       = battleMaxZ - diagStep * 0.5f;
                float sideZSpan      = sideEndZ - sideStartZ;
                int   sideCount      = Mathf.Max(2, Mathf.RoundToInt(sideZSpan / diagStep) + 1);
                float sideStep       = sideCount > 1 ? sideZSpan / (sideCount - 1) : 0f;

                var leftDiagRot  = Quaternion.Euler(0f, 225f, 0f); // long axis = NW-SE, face points SW (outward)
                var rightDiagRot = Quaternion.Euler(0f, 135f, 0f); // long axis = NE-SW, face points SE (outward)

                for (int j = 0; j < sideCount; j++)
                {
                    float z    = sideStartZ + j * sideStep;
                    float xOff = diagStep * 0.5f + j * sideStep;
                    result.Add(new WallSegmentData { position = new Vector3(sideLeftX  - xOff, 0f, z), rotation = leftDiagRot,  isConnector = true });
                    result.Add(new WallSegmentData { position = new Vector3(sideRightX + xOff, 0f, z), rotation = rightDiagRot, isConnector = true });
                }

                towerPositions.Add(new Vector3(sideLeftX,  0f, wallZ));
                towerPositions.Add(new Vector3(sideRightX, 0f, wallZ));
                return result;
            }

            if (useConvexWalls && inwardDepth > 0f && totalSegments >= 4)
            {
                int cvLeftCount   = totalSegments / 3;
                int cvMiddleCount = totalSegments / 3;
                int cvRightCount  = totalSegments - cvLeftCount - cvMiddleCount;
                float cvMiddleZ   = wallZ + inwardDepth;

                float cvLeftConnectorX  = startX + cvLeftCount * segW;
                float cvRightConnectorX = startX + (cvLeftCount + cvMiddleCount) * segW;

                float cvConnectorStart = wallZ     + segW / 2f;
                float cvConnectorEnd   = cvMiddleZ - segW / 2f;
                float cvEffectiveDepth = cvConnectorEnd - cvConnectorStart;
                int   cvConnectorCount = Mathf.Max(2, Mathf.RoundToInt(cvEffectiveDepth / segW) + 1);
                float cvConnectorStep  = cvEffectiveDepth / (cvConnectorCount - 1);

                // Left flank — recessed to cvMiddleZ
                for (int i = 0; i < cvLeftCount; i++)
                    result.Add(new WallSegmentData { position = new Vector3(CenterX(i), 0f, cvMiddleZ), rotation = WallRotation });

                // Left connector column
                for (int j = 0; j < cvConnectorCount; j++)
                    result.Add(new WallSegmentData { position = new Vector3(cvLeftConnectorX, 0f, cvConnectorStart + j * cvConnectorStep), rotation = ConnectorRotation, isConnector = true });

                // Middle section — forward at wallZ
                for (int i = 0; i < cvMiddleCount; i++)
                    result.Add(new WallSegmentData { position = new Vector3(CenterX(cvLeftCount + i), 0f, wallZ), rotation = WallRotation });

                // Right connector column
                for (int j = 0; j < cvConnectorCount; j++)
                    result.Add(new WallSegmentData { position = new Vector3(cvRightConnectorX, 0f, cvConnectorStart + j * cvConnectorStep), rotation = ConnectorRotation, isConnector = true });

                // Right flank — recessed to cvMiddleZ
                for (int i = 0; i < cvRightCount; i++)
                    result.Add(new WallSegmentData { position = new Vector3(CenterX(cvLeftCount + cvMiddleCount + i), 0f, cvMiddleZ), rotation = WallRotation });

                // Towers: 2 flank ends + 4 corners
                towerPositions.Add(new Vector3(CenterX(0),                         0f, cvMiddleZ));  // left end
                towerPositions.Add(new Vector3(CenterX(totalSegments - 1),         0f, cvMiddleZ));  // right end
                towerPositions.Add(new Vector3(cvLeftConnectorX,                    0f, cvMiddleZ));  // left outer corner
                towerPositions.Add(new Vector3(cvLeftConnectorX,                    0f, wallZ));      // left inner corner
                towerPositions.Add(new Vector3(cvRightConnectorX,                   0f, wallZ));      // right inner corner
                towerPositions.Add(new Vector3(cvRightConnectorX,                   0f, cvMiddleZ));  // right outer corner
                return result;
            }

            if (inwardDepth <= 0f || totalSegments < 4)
            {
                for (int i = 0; i < totalSegments; i++)
                    result.Add(new WallSegmentData { position = new Vector3(CenterX(i), 0f, wallZ), rotation = WallRotation });
                towerPositions.Add(new Vector3(CenterX(0),                  0f, wallZ));
                towerPositions.Add(new Vector3(CenterX(totalSegments - 1),  0f, wallZ));
                return result;
            }

            // Horizontal sections use all slots — connectors sit at the raw boundary X, no slot reserved
            int leftCount   = totalSegments / 3;
            int middleCount = totalSegments / 3;
            int rightCount  = totalSegments - leftCount - middleCount;
            float middleZ   = wallZ + inwardDepth;

            // Connector X = edge between sections, no centre offset
            float leftConnectorX  = startX + leftCount * segW;
            float rightConnectorX = startX + (leftCount + middleCount) * segW;

            // Connectors fill the gap between horizontal sections; each horizontal segment
            // occupies half a segment width inward, so the effective connector span is reduced by one full segment
            // Connectors span from the inner edge of the flank (wallZ + segW/2) to the inner edge
            // of the middle section (middleZ - segW/2), endpoints inclusive
            float connectorStart = wallZ   + segW / 2f;
            float connectorEnd   = middleZ - segW / 2f;
            float effectiveDepth = connectorEnd - connectorStart;
            int   connectorCount = Mathf.Max(2, Mathf.RoundToInt(effectiveDepth / segW) + 1);
            float connectorStep  = effectiveDepth / (connectorCount - 1);

            // Left flank
            for (int i = 0; i < leftCount; i++)
                result.Add(new WallSegmentData { position = new Vector3(CenterX(i), 0f, wallZ), rotation = WallRotation });

            // Left connector column
            for (int j = 0; j < connectorCount; j++)
                result.Add(new WallSegmentData { position = new Vector3(leftConnectorX, 0f, connectorStart + j * connectorStep), rotation = ConnectorRotation, isConnector = true });

            // Middle section
            for (int i = 0; i < middleCount; i++)
                result.Add(new WallSegmentData { position = new Vector3(CenterX(leftCount + i), 0f, middleZ), rotation = WallRotation });

            // Right connector column
            for (int j = 0; j < connectorCount; j++)
                result.Add(new WallSegmentData { position = new Vector3(rightConnectorX, 0f, connectorStart + j * connectorStep), rotation = ConnectorRotation, isConnector = true });

            // Right flank
            for (int i = 0; i < rightCount; i++)
                result.Add(new WallSegmentData { position = new Vector3(CenterX(leftCount + middleCount + i), 0f, wallZ), rotation = WallRotation });

            // Tower positions: 2 line ends + 4 recession corners (corners sit on connector endpoints)
            towerPositions.Add(new Vector3(CenterX(0),                          0f, wallZ));   // left end
            towerPositions.Add(new Vector3(CenterX(totalSegments - 1),          0f, wallZ));   // right end
            towerPositions.Add(new Vector3(leftConnectorX,                       0f, wallZ));   // left outer corner
            towerPositions.Add(new Vector3(leftConnectorX,                       0f, middleZ)); // left inner corner
            towerPositions.Add(new Vector3(rightConnectorX,                      0f, middleZ)); // right inner corner
            towerPositions.Add(new Vector3(rightConnectorX,                      0f, wallZ));   // right outer corner

            return result;
        }

        private void SpawnWallPiece(GameObject prefab, Vector3 pos, Quaternion rotation)
        {
            float terrainY = SampleTerrainHeight(pos.x, pos.z);
            pos.y = terrainY;
            Instantiate(prefab, pos, rotation, wallsParent);
        }

        private void SpawnGateWithEntity(Vector3 pos, GarrisonWallsSO config)
        {
            float terrainY = SampleTerrainHeight(pos.x, pos.z);
            pos.y = terrainY;
            GameObject gateGO = Instantiate(config.gatePrefab, pos, WallRotation, wallsParent);

            if (!Application.isPlaying) return;

            _pendingGates.Add((pos, _gateCount, gateGO));
            _gateCount++;
        }

        public void SpawnPendingGateSquads()
        {
            foreach (var (pos, index, gateGO) in _pendingGates)
                BattleManager.Instance.ArmySpawnManager.SpawnGateSquad(pos, index, gateGO, _activeConfig);
            _pendingGates.Clear();
        }

        // Same ground layers SquadManager samples; an unmasked ray hits the old gate squad's flag during a regenerate.
        private static int GroundMask => LayerMask.GetMask("Tile", "Water", "Swamp", "Forest", "Path");

        private float SampleTerrainHeight(float x, float z)
        {
            if (Physics.Raycast(new Vector3(x, 100f, z), Vector3.down, out RaycastHit hit, 110f, GroundMask))
                return hit.point.y;
            return 0f;
        }

        private static HashSet<int> BuildGateIndices(List<WallSegmentData> segments, int numberOfGates, bool hasPrefab)
        {
            HashSet<int> indices = new();
            if (!hasPrefab || numberOfGates <= 0) return indices;

            List<int> eligible = new();
            for (int i = 0; i < segments.Count; i++)
                if (!segments[i].isConnector)
                    eligible.Add(i);

            int clamped = Mathf.Min(numberOfGates, eligible.Count - 1);

            if (clamped == 1)
            {
                indices.Add(eligible[eligible.Count / 2]);
                return indices;
            }

            float spacing = (float)eligible.Count / (clamped + 1);
            for (int g = 1; g <= clamped; g++)
            {
                int eligIdx = Mathf.Clamp(Mathf.RoundToInt(spacing * g), 0, eligible.Count - 1);
                bool isMiddle = clamped % 2 == 1 && g == (clamped + 1) / 2;
                bool isFirst  = g == 1;
                bool isLast   = g == clamped;
                if (isMiddle) eligIdx = Mathf.Max(0,                  eligIdx - 1);
                if (isFirst)  eligIdx = Mathf.Max(0,                  eligIdx - 3);
                if (isLast)   eligIdx = Mathf.Min(eligible.Count - 1, eligIdx + 1);
                indices.Add(eligible[eligIdx]);
            }
            return indices;
        }

        public List<Bounds> GetWallColliderBounds()
        {
            List<Bounds> bounds = new();
            if (wallsParent == null) return bounds;
            foreach (Collider col in wallsParent.GetComponentsInChildren<Collider>())
                bounds.Add(col.bounds);
            return bounds;
        }

        private static GameObject GetRandomWallSegment(List<GameObject> prefabs)
        {
            if (prefabs == null || prefabs.Count == 0) return null;
            return prefabs[Random.Range(0, prefabs.Count)];
        }

        public void ClearWalls()
        {
            if (wallsParent != null)
            {
                // Destroy lands at frame end and the next wall set can be placed this frame, so the height raycast would hit the old gates.
                wallsParent.gameObject.SetActive(false);
                Destroy(wallsParent.gameObject);
            }
            wallsParent = null;
            _pendingGates.Clear();
            _gateCount = 0;
        }

#if UNITY_EDITOR
        [ContextMenu("Place Garrison Walls")]
        private void EditorPlaceGarrisonWalls()
        {
            EditorClearGarrisonWalls();
            PlaceGarrisonWalls(editorConfig);
        }

        [ContextMenu("Clear Garrison Walls")]
        private void EditorClearGarrisonWalls()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "GarrisonWalls")
                    DestroyImmediate(child.gameObject);
            }
            wallsParent = null;
            _gateCount = 0;
        }
#endif
    }
}
