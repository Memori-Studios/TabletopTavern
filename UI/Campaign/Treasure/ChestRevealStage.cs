using UnityEngine;
using UnityEngine.UI;

namespace TJ.Treasure
{
    /// <summary>
    /// Shows the 3D chest, its rays and particles over the map with no box: the chest camera renders to a transparent
    /// screen-sized texture that a premultiplied overlay draws above the HUD. Lives on the chest camera, so it runs
    /// while TreasurePanel has the chest scene on.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ChestRevealStage : MonoBehaviour
    {
        [Tooltip("The full-screen overlay that shows the chest; on only while the chest scene is.")]
        [SerializeField] private GameObject overlay;
        [SerializeField] private RawImage image;

        Camera chestCamera;
        RenderTexture target;

        private void Awake()
        {
            chestCamera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            Resize();
            if (overlay != null) overlay.SetActive(true);
        }

        private void OnDisable()
        {
            if (overlay != null) overlay.SetActive(false);
            Release();
        }

        private void OnDestroy()
        {
            Release();
        }

        // A window or resolution change mid-reveal would otherwise stretch the chest.
        private void LateUpdate()
        {
            if (target == null || target.width != Screen.width || target.height != Screen.height) Resize();
        }

        void Resize()
        {
            Release();
            // Half float keeps the bloom's HDR and the alpha that post-processing now preserves.
            target = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGBHalf) { name = "Chest Reveal", antiAliasing = 2 };
            target.Create();
            chestCamera.targetTexture = target;
            if (image != null) image.texture = target;
        }

        void Release()
        {
            if (target == null) return;
            if (chestCamera != null && chestCamera.targetTexture == target) chestCamera.targetTexture = null;
            if (image != null && image.texture == target) image.texture = null;
            target.Release();
            Destroy(target);
            target = null;
        }
    }
}
