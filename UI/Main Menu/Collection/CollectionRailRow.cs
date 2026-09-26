using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>One entry in the Collection's left rail: a label, a found count and a thin progress bar.</summary>
    public class CollectionRailRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image accent;
        [SerializeField] private Image marker;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text count;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private Image progressImage;
        [SerializeField] private GameObject newDot;

        [SerializeField] private Color idleText = new(0.81f, 0.78f, 0.72f, 1f);
        [SerializeField] private Color activeText = new(0.91f, 0.75f, 0.42f, 1f);
        [SerializeField] private Color idleCount = new(0.56f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color activeCount = new(0.93f, 0.9f, 0.85f, 1f);
        [SerializeField] private Color hoverFill = new(0.15f, 0.21f, 0.22f, 1f);
        [SerializeField] private Color activeFill = new(0.16f, 0.23f, 0.24f, 1f);
        [SerializeField] private Color progressPartial = new(0.44f, 0.39f, 0.27f, 1f);
        [SerializeField] private Color progressFull = new(0.55f, 0.48f, 0.27f, 1f);

        public Button Button => button;

        private bool _active, _hovered;

        public void SetUp(string text, Sprite sprite, Color markerColor, bool diamond)
        {
            label.text = text;
            marker.sprite = sprite;
            marker.color = markerColor;
            marker.preserveAspect = !diamond;
            marker.rectTransform.localEulerAngles = new Vector3(0f, 0f, diamond ? 45f : 0f);
            marker.rectTransform.sizeDelta = diamond ? new Vector2(12f, 12f) : new Vector2(22f, 22f);
            Refresh();
        }

        public void SetProgress(int found, int total)
        {
            count.text = $"{found}/{total}";
            float fraction = total > 0 ? (float)found / total : 0f;
            progressFill.anchorMax = new Vector2(fraction, 1f);
            progressImage.color = found >= total ? progressFull : progressPartial;
        }

        public void SetNew(bool isNew) => newDot.SetActive(isNew);

        public void SetActive(bool active)
        {
            _active = active;
            Refresh();
        }

        private void Refresh()
        {
            background.color = _active ? activeFill : _hovered ? hoverFill : Color.clear;
            accent.enabled = _active;
            label.color = _active ? activeText : idleText;
            count.color = _active ? activeCount : idleCount;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh();
        }
    }
}
