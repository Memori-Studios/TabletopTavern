using System.Collections.Generic;
using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ
{
    /// <summary>
    /// One row of the mage spell card's stat list: icon, label, value and a bar, in the same rhythm as
    /// <see cref="UnitStatUI"/> so the spell card reads like the unit card above it.
    ///
    /// Kept separate from UnitStatUI on purpose. That class is keyed on <see cref="UnitStat"/> for its
    /// sprite lookup, its gear and prestige bonus maths and its battlefield-bonus tooltip section, none
    /// of which a spell stat has. This one is told everything it shows.
    ///
    /// The charges row swaps the bar for a segmented strip (one cell per charge), which is the reading
    /// the flag's charge bar already gives - a 2/3 bar says "two thirds", three cells say "two of three".
    /// </summary>
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class SpellStatRowUI : MonoBehaviour
    {
        [SerializeField] private Image statImage;
        [SerializeField] private TMP_Text statNameText;
        [SerializeField] private TMP_Text statScoreText;
        [SerializeField] private Slider valueBar;

        [Header("Charges")]
        // Optional. Without them LoadCharges falls back to the bar.
        [SerializeField] private RectTransform chargeSegmentsParent;
        [SerializeField] private Image chargeSegmentTemplate;
        // Optional frame drawn over each cell, so an empty charge still shows as a socket.
        [SerializeField] private RectTransform chargeOutlinesParent;
        [SerializeField] private Image chargeOutlineTemplate;
        [SerializeField] private Color chargeFullColor = new(0.831f, 0.608f, 0.224f, 1f);
        [SerializeField] private Color chargeEmptyColor = new(0.149f, 0.169f, 0.180f, 1f);

        private readonly List<Image> segments = new();
        private readonly List<Image> outlines = new();
        private MemoriTooltipTrigger tooltipTrigger;

        public void Load(Sprite icon, Color iconColor, string label, string valueText, float fill01, string tooltipTitle, string tooltipDescription)
        {
            SetCommon(icon, iconColor, label, valueText, tooltipTitle, tooltipDescription);

            valueBar.gameObject.SetActive(true);
            valueBar.value = Mathf.Clamp01(fill01);
            if(chargeSegmentsParent != null) chargeSegmentsParent.gameObject.SetActive(false);
        }

        public void LoadCharges(Sprite icon, Color iconColor, string label, int current, int max, string tooltipTitle, string tooltipDescription)
        {
            SetCommon(icon, iconColor, label, current.ToString(), tooltipTitle, tooltipDescription);

            if(chargeSegmentsParent == null || chargeSegmentTemplate == null)
            {
                valueBar.gameObject.SetActive(true);
                valueBar.value = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
                return;
            }

            valueBar.gameObject.SetActive(false);
            chargeSegmentsParent.gameObject.SetActive(true);
            EnsureCells(segments, chargeSegmentsParent, chargeSegmentTemplate, max);

            if(chargeOutlinesParent != null && chargeOutlineTemplate != null)
            {
                chargeOutlinesParent.gameObject.SetActive(true);
                EnsureCells(outlines, chargeOutlinesParent, chargeOutlineTemplate, max);
            }

            SetChargeCount(current);
        }

        /// <summary>
        /// Grows a cell list to <paramref name="count"/>, adopting clones already under the parent
        /// first - the lists are not serialized, so a row saved with cells in it (an Editor preview)
        /// would otherwise get a second set stacked on the first.
        /// </summary>
        private static void EnsureCells(List<Image> cells, RectTransform parent, Image template, int count)
        {
            if(cells.Count == 0)
            {
                foreach(Transform child in parent)
                {
                    Image image = child.GetComponent<Image>();
                    if(image != null && image != template) cells.Add(image);
                }
            }
            while(cells.Count < count)
            {
                Image cell = Instantiate(template, parent);
                cell.gameObject.SetActive(true);
                cells.Add(cell);
            }
            for(int i = 0; i < cells.Count; i++) cells[i].gameObject.SetActive(i < count);
        }

        /// <summary>Live refresh for the charges row: recolours the cells, touches nothing else.</summary>
        public void SetChargeCount(int current)
        {
            statScoreText.text = current.ToString();
            for(int i = 0; i < segments.Count; i++)
                segments[i].color = i < current ? chargeFullColor : chargeEmptyColor;
        }

        private void SetCommon(Sprite icon, Color iconColor, string label, string valueText, string tooltipTitle, string tooltipDescription)
        {
            if(tooltipTrigger == null) tooltipTrigger = GetComponent<MemoriTooltipTrigger>();

            statImage.sprite = icon;
            statImage.color = iconColor;
            statNameText.text = label;
            statScoreText.text = valueText;
            tooltipTrigger.SetUpToolTip(_title: tooltipTitle, _description: tooltipDescription);
        }
    }
}
