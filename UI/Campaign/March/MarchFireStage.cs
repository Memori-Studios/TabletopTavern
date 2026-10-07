using UnityEngine;
using UnityEngine.UI;

namespace TJ.March
{
    /// <summary>
    /// The fire behind the March On button: a 3D fire far off the map, filmed by its own camera into a texture this
    /// image draws. The camera and texture exist only while the image is on.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class MarchFireStage : MonoBehaviour
    {
        [SerializeField] private Camera stageCamera;
        [SerializeField] private Vector2Int resolution = new(768, 432);

        RawImage image;
        RenderTexture target;

        private void Awake()
        {
            image = GetComponent<RawImage>();
        }

        private void OnEnable()
        {
            Release();
            // Half float keeps the alpha the premultiplied overlay blends with.
            target = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGBHalf) { name = "March Fire", antiAliasing = 2 };
            target.Create();
            stageCamera.targetTexture = target;
            image.texture = target;
            stageCamera.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (stageCamera != null) stageCamera.gameObject.SetActive(false);
            Release();
        }

        private void OnDestroy()
        {
            Release();
        }

        void Release()
        {
            if (target == null) return;
            if (stageCamera != null && stageCamera.targetTexture == target) stageCamera.targetTexture = null;
            if (image != null && image.texture == target) image.texture = null;
            target.Release();
            Destroy(target);
            target = null;
        }
    }
}
