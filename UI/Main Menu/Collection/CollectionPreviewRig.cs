using Memori.Utilities;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace TJ.MainMenu
{
    /// <summary>
    /// The Collection's 3D stage: one unit or hero on its faction plinth, rendered by the preview camera into
    /// the stage render texture. Lives in Collection.unity at (0, 5000, 0), away from any map or battle.
    /// </summary>
    public class CollectionPreviewRig : MonoBehaviour
    {
        [SerializeField] private Transform modelHolder;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private GameObject lights;
        [SerializeField] private MMF_Player dropInFeedback;
        [SerializeField] private Material undiscoveredMaterial;
        [Header("Framing")]
        [SerializeField] private float framePadding = 1.12f;
        [SerializeField] private float minHalfHeight = 1f;
        [SerializeField] private float liftFraction = 0.06f;

        private Transform _model;
        private RaceBasePrefab _base;
        private string _loadedKey;
        private string _shownId;
        private int _version;
        private Vector3 _holderRest;

        private void Awake()
        {
            _holderRest = modelHolder.localPosition;
            SetVisible(false);
        }

        public void ShowUnit(UnitName unit, Race race, bool found)
        {
#if UNITY_EDITOR
            // Editor previews every unit in full so art can be checked without a save that owns it.
            found = true;
#endif
            string id = $"unit:{unit}:{found}";
            if (id == _shownId) return;
            _shownId = id;
            RaceBasePrefab basePrefab = TabletopTavernData.Instance.GetRaceData(race).RaceBasePrefab;
            bool bigBase = TabletopTavernData.Instance.GetUnitSizeFromUnitName(unit) != UnitSize.Infantry;
            Load(TabletopTavernData.Instance.GetRecruitmentPrefabKey(unit),
                () => TabletopTavernData.Instance.LoadRecruitmentPrefabAsync(unit), basePrefab, bigBase, found);
        }

        public void ShowHero(Hero hero)
        {
            string id = $"hero:{hero.HeroID}";
            if (id == _shownId) return;
            _shownId = id;
            RaceBasePrefab basePrefab = TabletopTavernData.Instance.GetRaceData(hero.Race).RaceBasePrefab;
            Load(TabletopTavernData.Instance.GetHeroPrefabKey(hero.HeroID),
                () => TabletopTavernData.Instance.LoadHeroPrefabAsync(hero.HeroID), basePrefab, false, true);
        }

        private async void Load(string key, System.Func<System.Threading.Tasks.Task<GameObject>> loader, RaceBasePrefab basePrefab, bool bigBase, bool found)
        {
            int version = ++_version;
            GameObject prefab = await loader();

            // Every successful load owns one reference, so a superseded or orphaned load gives its own back.
            if (this == null || version != _version)
            {
                if (prefab != null && AddressablesManager.HasInstance) AddressablesManager.Instance.Release(key);
                return;
            }
            if (prefab == null) return;

            ClearModel();
            _loadedKey = key;
            Place(prefab, basePrefab, bigBase, found);
        }

        private void Place(GameObject prefab, RaceBasePrefab basePrefab, bool bigBase, bool found)
        {
            int layer = PreviewLayer();

            _base = Instantiate(basePrefab, modelHolder);
            _base.transform.localPosition = Vector3.zero;
            _base.transform.localRotation = Quaternion.identity;
            _base.transform.localScale = bigBase ? new Vector3(2f, 1f, 2f) : Vector3.one;
            _base.SetUp(found, undiscoveredMaterial);

            // Parented after placement, keeping world scale, so a wide plinth never stretches the model.
            _model = Instantiate(prefab, modelHolder).transform;
            _model.localPosition = Vector3.zero;
            _model.localRotation = Quaternion.identity;
            _model.SetParent(_base.transform, true);

            SetLayer(_base.transform, layer);

            // The panel stays usable while Settings has paused a battle, so idles must ignore timeScale.
            foreach (Animator animator in _model.GetComponentsInChildren<Animator>())
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                if (!found) animator.speed = 0f;
            }

            if (!found)
            {
                foreach (Renderer renderer in _model.GetComponentsInChildren<Renderer>())
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = undiscoveredMaterial;
                    renderer.sharedMaterials = materials;
                }
            }

            Frame();
            SetVisible(true);
            dropInFeedback.PlayFeedbacks();
        }

        // Every model fills the stage the same way: a cavalry unit on a wide plinth and a lone hero alike.
        private void Frame()
        {
            bool any = false;
            Bounds bounds = default;
            foreach (Renderer renderer in _base.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if (!any) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                any = true;
            }
            if (!any) return;

            // Measured while the drop-in may still be moving the holder, so shift to where it comes to rest.
            Vector3 rest = modelHolder.parent.TransformPoint(_holderRest);
            Vector3 centre = bounds.center - modelHolder.position + rest;
            float halfHeight = Mathf.Max(bounds.extents.y, minHalfHeight);
            float halfWidth = Mathf.Max(bounds.extents.x, bounds.extents.z);
            centre.y -= halfHeight * liftFraction;

            float verticalHalf = previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            RenderTexture target = previewCamera.targetTexture;
            float aspect = target != null ? (float)target.width / target.height : previewCamera.aspect;
            float horizontalHalf = Mathf.Atan(Mathf.Tan(verticalHalf) * aspect);
            float distance = Mathf.Max(halfHeight * framePadding / Mathf.Tan(verticalHalf), halfWidth * framePadding / Mathf.Tan(horizontalHalf)) + halfWidth;
            previewCamera.transform.position = centre - previewCamera.transform.forward * distance;
        }

        public void Turn(float degrees)
        {
            if (_base == null) return;
            _base.transform.Rotate(0f, degrees, 0f);
        }

        public void Clear()
        {
            _version++;
            _shownId = null;
            ClearModel();
            SetVisible(false);
        }

        private void ClearModel()
        {
            if (_model != null) Destroy(_model.gameObject);
            if (_base != null) Destroy(_base.gameObject);
            _model = null;
            _base = null;
            if (_loadedKey == null) return;
            if (AddressablesManager.HasInstance) AddressablesManager.Instance.Release(_loadedKey);
            _loadedKey = null;
        }

        private void SetVisible(bool visible)
        {
            previewCamera.enabled = visible;
            lights.SetActive(visible);
        }

        // The preview camera renders a single layer; hero prefabs are authored for the tavern and sit on another.
        private int PreviewLayer()
        {
            int mask = previewCamera.cullingMask;
            for (int i = 0; i < 32; i++)
                if ((mask & (1 << i)) != 0) return i;
            return modelHolder.gameObject.layer;
        }

        private static void SetLayer(Transform root, int layer)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        private void OnDestroy()
        {
            _version++;
            if (_loadedKey != null && AddressablesManager.HasInstance) AddressablesManager.Instance.Release(_loadedKey);
            _loadedKey = null;
        }
    }
}
