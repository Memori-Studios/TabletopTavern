using UnityEngine;

namespace TJ
{
    /// <summary>
    /// Shrinks a fixed-size panel so it fits inside its canvas, or its parent, when the UI Scale setting leaves less room than the panel's design size.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIFitToCanvas : MonoBehaviour
    {
        [SerializeField] private Vector2 designSize = new(1920f, 1080f);
        [SerializeField] private bool fitToParent;
        // Space other elements own at every scale (the main menu button columns beside the logo), taken off the reference before fitting.
        [SerializeField] private Vector2 reservedSize;
        // Floor so a reserved size larger than the canvas never collapses or flips the panel.
        [SerializeField] private float minScale = 0.5f;

        RectTransform rect;
        RectTransform reference;
        Vector2 lastReferenceSize;
        // The authored scale is part of the design (the keybinding table is authored at 4K and sits at 0.5), so the fit multiplies it.
        Vector3 baseScale;

        void Awake()
        {
            rect = transform as RectTransform;
            baseScale = rect.localScale;
        }

        void OnEnable()
        {
            lastReferenceSize = Vector2.zero;
            Fit();
        }

        // The reference only changes size on a UI Scale or aspect change, so a size compare per frame is cheap.
        void LateUpdate()
        {
            Fit();
        }

        void Fit()
        {
            if (reference == null)
            {
                if (fitToParent)
                {
                    reference = rect.parent as RectTransform;
                }
                else
                {
                    Canvas canvas = GetComponentInParent<Canvas>();
                    if (canvas != null) reference = canvas.rootCanvas.transform as RectTransform;
                }
                if (reference == null) return;
            }
            Vector2 referenceSize = reference.rect.size;
            if (referenceSize == lastReferenceSize) return;
            lastReferenceSize = referenceSize;

            Vector2 available = referenceSize - reservedSize;
            float scale = Mathf.Clamp(Mathf.Min(available.x / designSize.x, available.y / designSize.y), minScale, 1f);
            rect.localScale = new Vector3(baseScale.x * scale, baseScale.y * scale, baseScale.z);
        }
    }
}
