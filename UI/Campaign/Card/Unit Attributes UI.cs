using Memori.Tooltip;
using TMPro;
using UnityEngine;
using Memori.UI;
using Memori.Localization;

namespace TJ
{
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class UnitAttributesUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text attributeText;
        public TMP_Text AttributeText => attributeText;
        [Tooltip("Set true only on the distinct-colored prefab variant used for the prestige-granted trait.")]
        [SerializeField] private bool isPrestigeVariant;
        public bool IsPrestigeVariant => isPrestigeVariant;
        MemoriTooltipTrigger tooltipTrigger;
        UnitAttribute unitAttribute;
        UnitCondition unitCondition;
        public void Load(UnitAttribute _unitAttribute)
        {
            unitAttribute = _unitAttribute;
            string localizedAttribute = LocalizationManager.Instance.GetText(unitAttribute.ToString());
            // string localizedDescription = LocalizationManager.Instance.GetText(unitAttribute.ToString() + "Desc");

            attributeText.text = localizedAttribute;
            tooltipTrigger = GetComponent<MemoriTooltipTrigger>();
            tooltipTrigger.enabled = false;
            // tooltipTrigger.SetUpToolTip(_description: localizedDescription);
        }
        public void Load(UnitCondition _unitCondition)
        {
            unitCondition = _unitCondition;
            string localizedAttribute = LocalizationManager.Instance.GetText(unitCondition.ToString());
            string localizedDescription = LocalizationManager.Instance.GetText(unitCondition.ToString() + "Desc");

            attributeText.text = localizedAttribute;
            tooltipTrigger = GetComponent<MemoriTooltipTrigger>();
            tooltipTrigger.SetUpToolTip(_description: localizedDescription);
        }
        /// <summary>
        /// A condition with a countdown, e.g. Hunter's Mark: the badge reads "Marked 8s" and the tooltip
        /// description is the condition's Desc string formatted with descriptionArgs. Safe to call
        /// repeatedly - SquadBattleInfo refreshes it on its ammo tick so the seconds stay live.
        /// </summary>
        public void LoadTimed(UnitCondition _unitCondition, float secondsRemaining, params object[] descriptionArgs)
        {
            unitCondition = _unitCondition;
            string localizedAttribute = LocalizationManager.Instance.GetText(unitCondition.ToString());
            string localizedDescription = LocalizationManager.Instance.GetText(unitCondition.ToString() + "Desc");

            attributeText.text = $"{localizedAttribute} {Mathf.CeilToInt(secondsRemaining)}s";
            tooltipTrigger = GetComponent<MemoriTooltipTrigger>();
            tooltipTrigger.SetUpToolTip(_description: string.Format(localizedDescription, descriptionArgs));
        }
        public void SetUpTooltip()
        {
            string localizedAttribute = LocalizationManager.Instance.GetText(unitAttribute.ToString());
            string localizedDescription = LocalizationManager.Instance.GetText(unitAttribute.ToString() + "Desc");
            tooltipTrigger = GetComponent<MemoriTooltipTrigger>();
            tooltipTrigger.enabled = true;
            tooltipTrigger.SetUpToolTip(_title: localizedAttribute, _description: localizedDescription);
        }
    }
}