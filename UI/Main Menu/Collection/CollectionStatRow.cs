using Memori.Tooltip;
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

        public void Set(Sprite sprite, Color tint, string name, float amount, float fraction, string description)
        {
            icon.sprite = sprite;
            icon.color = tint;
            statName.text = name;
            value.text = Mathf.RoundToInt(amount).ToString();
            barFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            tooltip.SetUpToolTip(name, description);
        }
    }
}
