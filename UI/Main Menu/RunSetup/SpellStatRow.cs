using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>One line of the warband spell page's stat list, filled from the spell tooltip's stats.</summary>
    public class SpellStatRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text valueText;

        public void Set(TooltipStat stat)
        {
            icon.sprite = stat.Icon;
            icon.color = stat.IconColor;
            icon.enabled = stat.Icon != null;
            labelText.text = stat.Label;
            valueText.text = stat.Value;
        }
    }
}
