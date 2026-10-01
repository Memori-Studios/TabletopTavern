using Memori.Tooltip;
using Memori.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>One base stat in the unit detail: icon, name, value and a bar against the game-wide maximum.</summary>
    public class CollectionStatRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text statName;
        [SerializeField] private TMP_Text value;
        [SerializeField] private RectTransform barFill;
        [SerializeField] private MemoriTooltipTrigger tooltip;
        // Optional: rows built before the compare feature have no delta label.
        [SerializeField] private TMP_Text delta;

        public void Set(Sprite sprite, Color tint, string name, float amount, float fraction, string description)
        {
            icon.sprite = sprite;
            icon.color = tint;
            statName.text = name;
            value.text = Mathf.RoundToInt(amount).ToString();
            barFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            tooltip.SetUpToolTip(name, description);
            SetDelta(0f);
        }

        /// <summary>This stat minus the same stat on the kept unit. Zero clears it.</summary>
        public void SetDelta(float difference)
        {
            if (delta == null) return;
            int rounded = Mathf.RoundToInt(difference);
            delta.text = rounded > 0 ? $"+{rounded}" : rounded < 0 ? rounded.ToString() : string.Empty;
            delta.color = rounded > 0
                ? ColorVision.Good((Color)ColorData.HexToRgba(ColorData.Green))
                : ColorVision.Bad((Color)ColorData.HexToRgba(ColorData.Error));
        }
    }
}
