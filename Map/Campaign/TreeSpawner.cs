using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using Memori.Utilities;
using TJ.IrregularGrid;
using System.Threading.Tasks;

namespace TJ.Map
{   
    [System.Serializable] public struct TreeData
{
    public List<Vector2> outerVertices;
    public int failsAllowed;
    public float pointsRadius;
}
    [ExecuteAlways]
    public class TreeSpawner : MonoBehaviour
    {
        [SerializeField] private Mesh[] treePrefabs;
        [SerializeField] private int maxTrees = 10;
        [SerializeField] private TreeData treeData;
        [SerializeField] private HelperFunctions.NoiseMapSettings treeNoise;
        [SerializeField] private float treeThreshold;
        [SerializeField] private int TableTopLayer;
        [SerializeField] private Transform terrainDetailsParent;
        [SerializeField] private Transform treeParent;
        [SerializeField] private Vector2 treeRegionSize;

        // Trees are GPU-instanced rather than a GameObject each: one draw per mesh and material pair.
        private readonly List<SpawnedTree> trees = new();
        private readonly List<TreeBatch> batches = new();
        private Mesh baseMesh;
        private Material baseMaterial;
        private ShadowCastingMode baseShadowCasting;
        private bool baseReceiveShadows;
        private int baseLayer;
        private bool missingInstancingReported;

        private const int MaxInstancesPerDraw = 1023;

        private struct SpawnedTree
        {
            public Vector3 position;
            public Mesh mesh;
            public Material material;
            public Matrix4x4 treeMatrix;
            public Matrix4x4 baseMatrix;
            public bool hasBase;
        }

        private class TreeBatch
        {
            public Mesh mesh;
            public RenderParams renderParams;
            public readonly List<Matrix4x4> building = new();
            public Matrix4x4[] matrices;
        }

        public void ClearTrees()
        {
            void CleanUpTrees()
            {
                trees.Clear();
                RebuildBatches();

                // Maps generated before instancing saved a GameObject per tree under the tree parent.
                if (treeParent != null)
                {
                    for (int i = treeParent.childCount - 1; i >= 0; i--)
                        DestroyImmediate(treeParent.GetChild(i).gameObject);
                }
            }

            void DestroyAdditionalGameObjects()
            {
                if (terrainDetailsParent != null)
                {
                    for (int i = terrainDetailsParent.childCount - 1; i >= 0; i--)
                    {
                        if (terrainDetailsParent.GetChild(i) != null)
                            DestroyImmediate(terrainDetailsParent.GetChild(i).gameObject);
                    }
                }
            }
            CleanUpTrees();
            DestroyAdditionalGameObjects();
        }
        public async Task SpawnTrees(MapRegion mapRegion)
        {
            ClearTrees();

            if (treeData.outerVertices == null || treeData.outerVertices.Count == 0)
            {
                Debug.LogError($"No trees to spawn found");
                return;
            }

            if (mapRegion.additionalGameObject != null && mapRegion.additionalGameObject.RuntimeKeyIsValid())
            {
                GameObject prefab = await AddressablesManager.Instance.LoadAsync<GameObject>(mapRegion.additionalGameObject);
                if (prefab != null)
                    Instantiate(prefab, terrainDetailsParent);
                else
                    Debug.LogWarning($"[TreeSpawner] Failed to load additionalGameObject for region {mapRegion}; skipping.");
            }

            if (!mapRegion.spawnTrees)
            {
                Debug.Log($"Map theme {mapRegion} does not support trees.");
                return;
            }

            //spawn additional verticies
            List<Vector2> newPoints = PoissonDiscSampling.GeneratePoints(treeData.pointsRadius, treeRegionSize, treeData.failsAllowed, maxTrees);

            //get the center of the new points
            Vector2 newPointsCenter = newPoints.Aggregate(Vector2.zero, (acc, v) => acc + v) / newPoints.Count;

            //move the new points to the center of the square
            for (int i = 0; i < newPoints.Count; i++)
                newPoints[i] -= newPointsCenter;

            treeNoise.points = newPoints;
            float[] noiseMap = HelperFunctions.GenerateNoiseMap(treeNoise);

            List<Vector2> pointsToRemove = new();
            for (int i = 0; i < newPoints.Count; i++)
            {
                if (noiseMap[i] < treeThreshold)
                    pointsToRemove.Add(newPoints[i]);
            }

            //remove all points that are below the threshold
            foreach (Vector2 point in pointsToRemove)
                newPoints.Remove(point);

            List<Vector3> newPoints3D = new();

            //filter out all points that are not within the outer vertices
            // Debug.Log($"Points: {newPoints.Count}");
            for (int i = newPoints.Count - 1; i >= 0; i--)
            {
                Vector3 castPoint = new(newPoints[i].x + treeParent.position.x, treeParent.position.y + 0.5f, newPoints[i].y + treeParent.position.z);
                //testing
                // GameObject t = Instantiate(treeBase, castPoint, Quaternion.identity);
                // t.transform.parent = treeParent;
                // trees.Add(t.transform);

                if (Physics.Raycast(castPoint, Vector3.down, out RaycastHit hit, 1))
                {

                    if (hit.collider.gameObject.layer == TableTopLayer)
                    {
                        newPoints3D.Add(hit.point);
                        // newPoints.RemoveAt(i);
                    }
                    else
                    {
                        // newPoints[i] = new Vector2(hit.point.x, hit.point.z);
                        // if (Physics.OverlapSphere(hit.point, 0.15f).Any(x => x.gameObject.layer != LayerMask.NameToLayer("Tile"))) {
                        //     Debug.Log($"Hit something else nearby");
                        //     newPoints.RemoveAt(i);
                        //     // continue;
                        // }
                    }
                }
                else
                {
                    // Debug.Log($"Hit nothing");
                    newPoints.RemoveAt(i);
                }
            }

            bool hasBase = SetUpTreeBase(mapRegion);
            Quaternion baseRotation = hasBase ? mapRegion.TreeBase.transform.localRotation : Quaternion.identity;
            Matrix4x4 parentMatrix = treeParent.localToWorldMatrix;
            Quaternion parentRotationInverse = Quaternion.Inverse(treeParent.rotation);

            for (int i = 0; i < newPoints3D.Count; i++)
            {
                // Same seeded draws in the same order as the old GameObject trees, so saved maps do not change.
                Mesh mesh = treePrefabs[SeededRandom.Range(0, treePrefabs.Length)];
                Material material = mapRegion.treeMaterials[SeededRandom.Range(0, mapRegion.treeMaterials.Count)];
                Quaternion rotation = Quaternion.Euler(0, SeededRandom.Range(0, 360), 0);
                Vector3 localScale = new Vector3(0.6f, 0.6f, 0.6f) * SeededRandom.Range(0.80f, 1.20f);

                // Composed like a child Transform: world position and rotation, local scale under the tree parent.
                Matrix4x4 treeMatrix = parentMatrix * Matrix4x4.TRS(treeParent.InverseTransformPoint(newPoints3D[i]), parentRotationInverse * rotation, localScale);
                trees.Add(new SpawnedTree
                {
                    position = newPoints3D[i],
                    mesh = mesh,
                    material = material,
                    treeMatrix = treeMatrix,
                    baseMatrix = treeMatrix * Matrix4x4.TRS(Vector3.zero, baseRotation, new Vector3(0.5f, 0.15f, 0.5f)),
                    hasBase = hasBase,
                });
            }
            RebuildBatches();

            return;
        }
        public Task PruneTrees()
        {
            if (trees.Count == 0) return Task.CompletedTask;

            //remove all trees that are blocking paths
            for (int i = trees.Count - 1; i >= 0; i--)
            {
                Vector3 position = trees[i].position;
                Vector3 castPoint = new(position.x, position.y + 0.5f, position.z);

                if (Physics.Raycast(castPoint, Vector3.down, out RaycastHit hit, 1) && hit.collider.gameObject.layer != TableTopLayer)
                    trees.RemoveAt(i);
            }
            RebuildBatches();
            return Task.CompletedTask;
        }

        private bool SetUpTreeBase(MapRegion mapRegion)
        {
            baseMesh = null;
            baseMaterial = null;
            if (mapRegion.TreeBase == null)
            {
                Debug.LogWarning($"[TreeSpawner] mapRegion.TreeBase is null for region {mapRegion}; skipping tree bases.");
                return false;
            }

            MeshFilter filter = mapRegion.TreeBase.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer renderer = mapRegion.TreeBase.GetComponentInChildren<MeshRenderer>(true);
            if (filter == null || renderer == null || filter.sharedMesh == null || renderer.sharedMaterial == null)
            {
                Debug.LogError($"[TreeSpawner] Tree base {mapRegion.TreeBase.name} needs a MeshFilter with a mesh and a MeshRenderer with a material.");
                return false;
            }

            baseMesh = filter.sharedMesh;
            baseMaterial = renderer.sharedMaterial;
            baseShadowCasting = renderer.shadowCastingMode;
            baseReceiveShadows = renderer.receiveShadows;
            baseLayer = mapRegion.TreeBase.layer;
            return true;
        }

        #region Instanced rendering
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering -= DrawTrees;
            RenderPipelineManager.beginCameraRendering += DrawTrees;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= DrawTrees;
        }

        // Drawn per camera so the trees also show in the Scene view when a map is generated outside Play Mode.
        private void DrawTrees(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType == CameraType.Preview) return;

            foreach (TreeBatch batch in batches)
            {
                RenderParams renderParams = batch.renderParams;
                renderParams.camera = camera;
                for (int start = 0; start < batch.matrices.Length; start += MaxInstancesPerDraw)
                {
                    int count = Mathf.Min(MaxInstancesPerDraw, batch.matrices.Length - start);
                    Graphics.RenderMeshInstanced(renderParams, batch.mesh, 0, batch.matrices, count, start);
                }
            }
        }

        private void RebuildBatches()
        {
            batches.Clear();
            var byMeshAndMaterial = new Dictionary<(Mesh, Material), TreeBatch>();
            foreach (SpawnedTree tree in trees)
            {
                AddInstance(byMeshAndMaterial, tree.mesh, tree.material, 0, ShadowCastingMode.On, true, tree.treeMatrix);
                if (tree.hasBase)
                    AddInstance(byMeshAndMaterial, baseMesh, baseMaterial, baseLayer, baseShadowCasting, baseReceiveShadows, tree.baseMatrix);
            }

            foreach (TreeBatch batch in byMeshAndMaterial.Values)
            {
                batch.matrices = batch.building.ToArray();
                batch.renderParams.worldBounds = InstanceBounds(batch.mesh, batch.matrices);
                batches.Add(batch);
            }
        }

        private void AddInstance(Dictionary<(Mesh, Material), TreeBatch> byMeshAndMaterial, Mesh mesh, Material material, int layer, ShadowCastingMode shadowCasting, bool receiveShadows, Matrix4x4 matrix)
        {
            if (mesh == null || material == null) return;
            if (!material.enableInstancing)
            {
                if (!missingInstancingReported)
                    Debug.LogError($"[TreeSpawner] Material {material.name} needs Enable GPU Instancing ticked; trees using it are not drawn.");
                missingInstancingReported = true;
                return;
            }

            if (!byMeshAndMaterial.TryGetValue((mesh, material), out TreeBatch batch))
            {
                batch = new TreeBatch
                {
                    mesh = mesh,
                    renderParams = new RenderParams(material)
                    {
                        layer = layer,
                        shadowCastingMode = shadowCasting,
                        receiveShadows = receiveShadows,
                        lightProbeUsage = LightProbeUsage.BlendProbes,
                        reflectionProbeUsage = ReflectionProbeUsage.BlendProbes,
                    },
                };
                byMeshAndMaterial.Add((mesh, material), batch);
            }
            batch.building.Add(matrix);
        }

        // A batch is culled as one box, so the box must hold every instance in it.
        private static Bounds InstanceBounds(Mesh mesh, Matrix4x4[] matrices)
        {
            Bounds local = mesh.bounds;
            Bounds bounds = new(matrices[0].MultiplyPoint3x4(local.center), Vector3.zero);
            foreach (Matrix4x4 matrix in matrices)
            {
                Vector3 scale = matrix.lossyScale;
                float radius = local.extents.magnitude * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                bounds.Encapsulate(new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.one * (2f * radius)));
            }
            return bounds;
        }
        #endregion
    }
}