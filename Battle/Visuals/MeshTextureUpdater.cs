using System.Collections;
using System.Collections.Generic;
using TJ.IrregularGrid;
using UnityEngine;

public class MeshTextureUpdater : MonoBehaviour
{
    public Texture2D[] splatTextures;
    public float splatSize = 0.1f;
    [SerializeField] private GameObject targetObject; // The GameObject whose position we check (e.g., player)
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    // [SerializeField] private Texture2D mainTex, snowTexture;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private GameObject dustCloudPrefab;
    private const int dustCloudPoolSize = 200;
    private const float DustCloudLifetime = 3f;
    private Stack<GameObject> _dustCloudPool = new();
    // Splats are stamped on the GPU into this copy of the ground texture; the CPU never touches pixels.
    private RenderTexture workingTexture;
    // Per-renderer override; writing the RenderTexture into the shared material asset blanks its slot on any save.
    private MaterialPropertyBlock splatPropertyBlock;
    private Material splatStampMaterial;
    private List<Vector3> splatPoints = new List<Vector3>();
    private struct Triangle
    {
        public Vector3 v0, v1, v2;
        public Vector2 uv0, uv1, uv2;
        public Bounds bounds;
    }
    private List<Triangle> triangles;
    // Triangle indices bucketed by XZ cell so a splat only tests the triangles under it.
    private const float TriangleCellSize = 4f;
    private Dictionary<long, List<int>> triangleCells;
    Coroutine splatCoroutine;

    private void Awake()
    {
        splatStampMaterial = new Material(Resources.Load<Shader>("Shaders/TTSplatStamp"));

        for (int i = 0; i < dustCloudPoolSize; i++)
        {
            GameObject go = Instantiate(dustCloudPrefab, transform);
            go.SetActive(false);
            _dustCloudPool.Push(go);
        }
    }

    public void UpdateBattlefieldTexture(Texture2D baseTexture)
    {
        meshFilter = targetObject.GetComponent<MeshFilter>();
        meshRenderer = targetObject.GetComponent<MeshRenderer>();

        ReleaseWorkingTexture();

        try
        {
            workingTexture = new RenderTexture(baseTexture.width, baseTexture.height, 0, RenderTextureFormat.ARGB32)
            {
                name = "Battlefield Splat Texture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                useMipMap = false
            };
            workingTexture.Create();
            Graphics.Blit(baseTexture, workingTexture);
            splatPropertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(splatPropertyBlock);
            splatPropertyBlock.SetTexture("_MainTex", workingTexture);
            meshRenderer.SetPropertyBlock(splatPropertyBlock);
            Debug.Log("Working texture initialized successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create working texture: {e.Message}");
        }

        // Preprocess mesh after setting meshFilter
        PreprocessMesh();
        if (splatCoroutine != null) {
            StopCoroutine(splatCoroutine);
        }
        splatCoroutine = StartCoroutine(ProcessSplats());
    }

    private void ReleaseWorkingTexture()
    {
        if (meshRenderer != null && splatPropertyBlock != null)
        {
            splatPropertyBlock.Clear();
            meshRenderer.SetPropertyBlock(splatPropertyBlock);
        }
        if (workingTexture == null) return;
        workingTexture.Release();
        Destroy(workingTexture);
        workingTexture = null;
    }

    private void PreprocessMesh()
    {
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("MeshFilter or mesh is not assigned! Call UpdateBattlefieldTexture first.");
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        int[] indices = mesh.triangles;

        if (uvs == null || uvs.Length == 0)
        {
            Debug.LogError("Mesh has no UVs! Splats will not appear correctly.");
            return;
        }

        triangles = new List<Triangle>();
        triangleCells = new Dictionary<long, List<int>>();
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 v0 = vertices[indices[i]];
            Vector3 v1 = vertices[indices[i + 1]];
            Vector3 v2 = vertices[indices[i + 2]];
            Bounds bounds = new Bounds(v0, Vector3.zero);
            bounds.Encapsulate(v1);
            bounds.Encapsulate(v2);
            bounds.Expand(0.1f);
            triangles.Add(new Triangle
            {
                v0 = v0, v1 = v1, v2 = v2,
                uv0 = uvs[indices[i]], uv1 = uvs[indices[i + 1]], uv2 = uvs[indices[i + 2]],
                bounds = bounds
            });
            int triangleIndex = triangles.Count - 1;
            int minX = CellCoord(bounds.min.x), maxX = CellCoord(bounds.max.x);
            int minZ = CellCoord(bounds.min.z), maxZ = CellCoord(bounds.max.z);
            for (int cx = minX; cx <= maxX; cx++)
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    long key = CellKey(cx, cz);
                    if (!triangleCells.TryGetValue(key, out List<int> cell))
                    {
                        cell = new List<int>();
                        triangleCells[key] = cell;
                    }
                    cell.Add(triangleIndex);
                }
        }
    }

    private static int CellCoord(float v) => Mathf.FloorToInt(v / TriangleCellSize);
    private static long CellKey(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

    public void ApplySplatAtPoint(Vector3 worldPoint, Vector2? overrideUV = null)
    {
        splatPoints.Add(worldPoint);
        // Debug.Log($"Added splat point at {worldPoint}. Override UV: {overrideUV}. Total splat points: {splatPoints.Count}");
    }
    public void ExplosionAtPoint(Vector3 worldPoint)
    {
        // Debug.Log($"Explosion at point");
        Instantiate(explosionPrefab, worldPoint, Quaternion.identity);
    }
    public void SpawnDustCloudAt(Vector3 worldPoint)
    {
        if (_dustCloudPool.Count == 0) return;

        // Debug.Log($"Spawning dust cloud at {worldPoint}. Pool size before spawn: {_dustCloudPool.Count}");
        GameObject go = _dustCloudPool.Pop();
        go.transform.position = worldPoint;
        go.SetActive(true);
        StartCoroutine(ReturnToDustPool(go));
    }
    private IEnumerator ReturnToDustPool(GameObject go)
    {
        yield return new WaitForSeconds(DustCloudLifetime);
        go.SetActive(false);
        _dustCloudPool.Push(go);
    }

    private IEnumerator ProcessSplats()
    {
        while (true)
        {
            if (splatPoints.Count > 0 && workingTexture != null)
            {
                int pointsToProcess = Mathf.Min(splatPoints.Count, 5);
                RenderTexture previous = RenderTexture.active;
                Graphics.SetRenderTarget(workingTexture);
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, workingTexture.width, 0, workingTexture.height);
                for (int i = 0; i < pointsToProcess; i++)
                {
                    StampSplat(splatPoints[i]);
                }
                GL.PopMatrix();
                RenderTexture.active = previous;
                splatPoints.RemoveRange(0, pointsToProcess);
            }
            yield return null;
        }
    }

    // Caller has the working texture bound and a pixel-space GL matrix loaded.
    private void StampSplat(Vector3 worldPoint)
    {
        if (meshFilter == null || splatTextures.Length == 0) return;

        worldPoint += new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector2 uv = GetUVAtPoint(localPoint);
        uv.x = Mathf.Clamp01(uv.x);
        uv.y = Mathf.Clamp01(uv.y);

        float centerX = uv.x * workingTexture.width;
        float centerY = uv.y * workingTexture.height;
        float randomSize = Random.Range(0.75f, 1.25f);

        // Same size rule as the old CPU stamp so the splats look the same.
        int referenceTexSize = Mathf.Min(splatTextures[0].width, splatTextures[0].height);
        int splatPixelSize = Mathf.FloorToInt(splatSize * workingTexture.width * randomSize);
        splatPixelSize = Mathf.Clamp(splatPixelSize, 16, referenceTexSize) / 10;
        float half = splatPixelSize * 0.5f;

        Texture2D splatTexture = splatTextures[Random.Range(0, splatTextures.Length)];
        splatStampMaterial.mainTexture = splatTexture;
        splatStampMaterial.SetPass(0);
        GL.Begin(GL.QUADS);
        GL.Color(Color.white);
        GL.TexCoord2(0f, 0f); GL.Vertex3(centerX - half, centerY - half, 0f);
        GL.TexCoord2(0f, 1f); GL.Vertex3(centerX - half, centerY + half, 0f);
        GL.TexCoord2(1f, 1f); GL.Vertex3(centerX + half, centerY + half, 0f);
        GL.TexCoord2(1f, 0f); GL.Vertex3(centerX + half, centerY - half, 0f);
        GL.End();
    }

    private Vector2 GetUVAtPoint(Vector3 localPoint)
    {
        float minDistance = float.MaxValue;
        Vector2 closestUV = Vector2.zero;
        bool foundValidTriangle = false;

        if (triangles == null || triangles.Count == 0)
        {
            Debug.LogWarning("No triangles preprocessed! Ensure PreprocessMesh is called.");
            return closestUV;
        }

        List<int> candidates = null;
        if (triangleCells != null) triangleCells.TryGetValue(CellKey(CellCoord(localPoint.x), CellCoord(localPoint.z)), out candidates);
        int candidateCount = candidates == null ? 0 : candidates.Count;
        for (int c = 0; c < candidateCount; c++)
        {
            Triangle tri = triangles[candidates[c]];
            if (!tri.bounds.Contains(localPoint)) continue;

            Vector3 pointOnTriangle = ClosestPointOnTriangle(tri.v0, tri.v1, tri.v2, localPoint);
            float distance = Vector3.SqrMagnitude(localPoint - pointOnTriangle);

            if (distance < minDistance)
            {
                minDistance = distance;
                foundValidTriangle = true;

                Vector3 edge0 = tri.v1 - tri.v0;
                Vector3 edge1 = tri.v2 - tri.v0;
                Vector3 v0ToPoint = pointOnTriangle - tri.v0;

                float d00 = Vector3.Dot(edge0, edge0);
                float d01 = Vector3.Dot(edge0, edge1);
                float d11 = Vector3.Dot(edge1, edge1);
                float d20 = Vector3.Dot(v0ToPoint, edge0);
                float d21 = Vector3.Dot(v0ToPoint, edge1);
                float denom = d00 * d11 - d01 * d01;

                if (Mathf.Abs(denom) < 1e-6f)
                {
                    Debug.LogWarning("Degenerate triangle detected.");
                    continue;
                }

                float v = (d11 * d20 - d01 * d21) / denom;
                float w = (d00 * d21 - d01 * d20) / denom;
                float u = 1.0f - v - w;

                closestUV = u * tri.uv0 + v * tri.uv1 + w * tri.uv2;
                // Debug.Log($"Barycentric: u={u}, v={v}, w={w}, Triangle UVs: ({tri.uv0.x}, {tri.uv0.y}), ({tri.uv1.x}, {tri.uv1.y}), ({tri.uv2.x}, {tri.uv2.y}) -> Computed: ({closestUV.x}, {closestUV.y})");

                // Normalize UVs to [0,1]
                closestUV.x = (closestUV.x - Mathf.Floor(closestUV.x)) % 1.0f;
                closestUV.y = (closestUV.y - Mathf.Floor(closestUV.y)) % 1.0f;
                if (closestUV.x < 0) closestUV.x += 1.0f;
                if (closestUV.y < 0) closestUV.y += 1.0f;
            }
        }

        if (!foundValidTriangle)
        {
            #if UNITY_EDITOR
            Debug.LogWarning($"No valid triangle found for local point {localPoint}. Using fallback UV.");
            #endif
            closestUV = new Vector2(localPoint.x * 0.5f + 0.5f, localPoint.z * 0.5f + 0.5f);
            closestUV.x = Mathf.Clamp01(closestUV.x);
            closestUV.y = Mathf.Clamp01(closestUV.y);
        }

        return closestUV;
    }

    private Vector3 ClosestPointOnTriangle(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 point)
    {
        Vector3 edge0 = v1 - v0;
        Vector3 edge1 = v2 - v0;
        Vector3 v0ToPoint = point - v0;

        float d00 = Vector3.Dot(edge0, edge0);
        float d01 = Vector3.Dot(edge0, edge1);
        float d11 = Vector3.Dot(edge1, edge1);
        float d20 = Vector3.Dot(v0ToPoint, edge0);
        float d21 = Vector3.Dot(v0ToPoint, edge1);
        float denom = d00 * d11 - d01 * d01;

        if (Mathf.Abs(denom) < 1e-6f)
        {
            return v0;
        }

        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1.0f - v - w;

        if (u >= 0 && v >= 0 && w >= 0)
        {
            return v0 + v * edge0 + w * edge1;
        }

        Vector3 closest = v0;
        float minDist = Vector3.SqrMagnitude(point - v0);

        Vector3[] edges = { edge0, v1 - v2, v2 - v0 };
        Vector3[] starts = { v0, v1, v2 };
        for (int i = 0; i < 3; i++)
        {
            Vector3 start = starts[i];
            Vector3 edge = edges[i];
            float t = Mathf.Clamp01(Vector3.Dot(point - start, edge) / Vector3.Dot(edge, edge));
            Vector3 candidate = start + t * edge;
            float dist = Vector3.SqrMagnitude(point - candidate);
            if (dist < minDist)
            {
                minDist = dist;
                closest = candidate;
            }
        }

        return closest;
    }

    // #if UNITY_EDITOR
    // private void Update()
    // {
    //     if (Input.GetMouseButtonDown(0))
    //     {
    //         Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    //         RaycastHit hit;
    //         if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == meshFilter.gameObject)
    //         {
    //             // Debug.Log($"hit at {hit.point}, UV: {hit.textureCoord}");
    //             ApplySplatAtPoint(hit.point, hit.textureCoord);
    //         }
    //     }
    //     // else if (targetObject != null && Input.GetKeyDown(KeyCode.Space))
    //     // {
    //     //     Vector3 closestPoint = FindClosestPointOnMesh(targetObject.transform.position);
    //     //     ApplySplatAtPoint(closestPoint);
    //     // }
    // }
    // #endif

    // private Vector3 FindClosestPointOnMesh(Vector3 targetPosition)
    // {
    //     Vector3 localTargetPos = transform.InverseTransformPoint(targetPosition);
    //     Vector3 closestPoint = Vector3.zero;
    //     float minDistance = float.MaxValue;

    //     if (triangles == null || triangles.Count == 0)
    //     {
    //         Debug.LogWarning("No triangles available for closest point calculation.");
    //         return targetPosition;
    //     }

    //     foreach (var tri in triangles)
    //     {
    //         Vector3 pointOnTriangle = ClosestPointOnTriangle(tri.v0, tri.v1, tri.v2, localTargetPos);
    //         float distance = Vector3.SqrMagnitude(localTargetPos - pointOnTriangle);
    //         if (distance < minDistance)
    //         {
    //             minDistance = distance;
    //             closestPoint = pointOnTriangle;
    //         }
    //     }

    //     Debug.Log($"Closest point: {transform.TransformPoint(closestPoint)} for target: {targetPosition}");
    //     return transform.TransformPoint(closestPoint);
    // }

    private void OnDestroy()
    {
        ReleaseWorkingTexture();
        if (splatStampMaterial != null) Destroy(splatStampMaterial);
        _dustCloudPool.Clear();
    }
}