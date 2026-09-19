using UnityEngine;
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
        [SerializeField] private List<Transform> trees;
        [SerializeField] private Vector2 treeRegionSize;

        public void ClearTrees()
        {
            void CleanUpTrees()
            {
                if (trees != null && trees.Count > 0)
                {
                    for (int i = trees.Count - 1; i >= 0; i--)
                    {
                        if (trees[i] != null)
                            DestroyImmediate(trees[i].gameObject);
                        else
                            trees.RemoveAt(i);
                    }
                }
                trees = new();
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

            for (int i = 0; i < newPoints3D.Count; i++)
            {
                GameObject tree = new();
                tree.transform.parent = treeParent;
                tree.name = "Tree " + i;
                tree.transform.position = newPoints3D[i];
                tree.AddComponent<MeshFilter>().sharedMesh = treePrefabs[SeededRandom.Range(0, treePrefabs.Length)];
                tree.AddComponent<MeshRenderer>().sharedMaterial = mapRegion.treeMaterials[SeededRandom.Range(0, mapRegion.treeMaterials.Count)];
                tree.transform.rotation = Quaternion.Euler(0, SeededRandom.Range(0, 360), 0);
                tree.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                tree.transform.localScale *= SeededRandom.Range(0.80f, 1.20f);
                trees.Add(tree.transform);
                if (mapRegion.TreeBase == null)
                {
                    Debug.LogWarning($"[TreeSpawner] mapRegion.TreeBase is null for region {mapRegion}; skipping tree base.");
                    continue;
                }
                GameObject treeBaseInstance = Instantiate(mapRegion.TreeBase, tree.transform);
                treeBaseInstance.transform.localPosition = new Vector3(0, 0, 0);
                treeBaseInstance.transform.localScale = new Vector3(0.5f, 0.15f, 0.5f);
            }

            return;
        }
        public Task PruneTrees()
        {
            if(trees==null || trees.Count == 0) return Task.CompletedTask;

            //remove all trees that are blocking paths
            for (int i = 0; i < trees.Count; i++)
            {
                Vector3 castPoint = new (trees[i].position.x, trees[i].position.y+0.5f, trees[i].position.z);

                if (Physics.Raycast(castPoint, Vector3.down, out RaycastHit hit, 1)) {
                    if (hit.collider.gameObject.layer != TableTopLayer) {
                        if(Application.isPlaying) {
                            Destroy(trees[i].gameObject);
                        } else {
                            DestroyImmediate(trees[i].gameObject);
                        }
                        trees.RemoveAt(i);
                        i--;
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}