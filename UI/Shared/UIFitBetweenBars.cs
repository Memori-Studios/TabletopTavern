using UnityEngine;

namespace TJ
{
    /// <summary>
    /// Keeps a map panel inside the band between the top bar and the army bar: it moves first and shrinks only when taller than the band.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIFitBetweenBars : MonoBehaviour
    {
        // The army bar's top is 271 above the screen bottom and its loss badges reach about 281.
        [SerializeField] private float bottomClearance = 285f;
        // The visible top bar is 50 tall, but only its corner boxes; a panel clear of them may rise nearly to the edge.
        [SerializeField] private float topClearance = 55f;
        [SerializeField] private float topClearanceBetweenCorners = 10f;
        [SerializeField] private float cornerWidth = 350f;
        [SerializeField] private float minScale = 0.6f;

        RectTransform rect;
        Vector2 restPosition;
        Vector3 baseScale;
        Vector4 fittedFor;

        void Awake()
        {
            rect = (RectTransform)transform;
            restPosition = rect.anchoredPosition;
            baseScale = rect.localScale;
        }

        void OnEnable()
        {
            fittedFor = Vector4.zero;
        }

        // Layout groups resize the panel as its state changes, so the size is part of what is compared each frame.
        void LateUpdate()
        {
            RectTransform parent = rect.parent as RectTransform;
            if (parent == null) return;
            Rect area = parent.rect;
            Vector2 size = new(rect.rect.width * baseScale.x, rect.rect.height * baseScale.y);
            var key = new Vector4(area.width, area.height, size.x, size.y);
            if (key == fittedFor || size.y <= 0f) return;
            fittedFor = key;

            float scale = ScaleFor(area, size, topClearanceBetweenCorners);
            float top = topClearanceBetweenCorners;
            if (size.x * scale > area.width - 2f * cornerWidth)
            {
                scale = ScaleFor(area, size, topClearance);
                top = topClearance;
            }

            float height = size.y * scale;
            float pivotY = Mathf.Lerp(area.yMin, area.yMax, rect.anchorMin.y) + restPosition.y;
            float bottom = pivotY - rect.pivot.y * height;
            float bandMin = area.yMin + bottomClearance;
            float bandMax = area.yMax - top;
            float shift = 0f;
            if (bottom < bandMin) shift = bandMin - bottom;
            else if (bottom + height > bandMax) shift = Mathf.Max(bandMin - bottom, bandMax - (bottom + height));

            rect.anchoredPosition = restPosition + new Vector2(0f, shift);
            rect.localScale = new Vector3(baseScale.x * scale, baseScale.y * scale, baseScale.z);
        }

        float ScaleFor(Rect area, Vector2 size, float top)
        {
            float band = area.height - bottomClearance - top;
            return Mathf.Clamp(Mathf.Min(band / size.y, area.width / size.x, 1f), minScale, 1f);
        }
    }
}
