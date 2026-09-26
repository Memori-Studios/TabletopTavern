using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>A small labelled chip: a unit trait, a rarity or a lock state, with an optional tooltip.</summary>
    public class CollectionChip : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private TMP_Text label;
        [SerializeField] private MemoriTooltipTrigger tooltip;
        [SerializeField, Range(0f, 1f)] private float fillAlpha = 0.12f;
        [SerializeField, Range(0f, 1f)] private float borderAlpha = 0.55f;

        public void Set(string text, Color colour, string tooltipTitle = null, string tooltipBody = null)
        {
            label.text = text;
            label.color = colour;
            background.color = new Color(colour.r, colour.g, colour.b, fillAlpha);
            border.color = new Color(colour.r, colour.g, colour.b, borderAlpha);
            bool hasTooltip = !string.IsNullOrEmpty(tooltipBody);
            tooltip.enabled = hasTooltip;
            background.raycastTarget = hasTooltip;
            if (hasTooltip) tooltip.SetUpToolTip(tooltipTitle ?? string.Empty, tooltipBody);
        }
    }
}
