using Memori.Tooltip;
using TMPro;
using UnityEngine;
using Memori.UI;
using Memori.Localization;
using Memori.Utilities;
using UnityEngine.UI;

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
            ApplyColorVision();
            // tooltipTrigger.SetUpToolTip(_description: localizedDescription);
        }
        public void Load(UnitCondition _unitCondition)
        {
            unitCondition = _unitCondition;
            string localizedAttribute = LocalizationManager.Instance.GetText(unitCondition.ToString());
            string localizedDescription = LocalizationManager.Instance.GetText(unitCondition.ToString() + "Desc");

            attributeText.text = localizedAttribute;
            tooltipTrigger = GetComponent<MemoriTooltipTrigger>();
            tooltipTrigger.SetUpToolTip(_description: KeywordText.ForTooltip(localizedDescription, unitCondition.ToString()));
        }
        /// <summary>
        /// A lasting spell on the squad, e.g. Dread: the badge reads "Dread 8s" (just the name for a zone,
        /// which re-stamps its entry every tick), the icon is the spell's in its race colour, and the tooltip
        /// is the spell's own description. Safe to call repeatedly - SquadBattleInfo refreshes it on its ammo tick.
        /// </summary>
        public void LoadSpell(TJ.Spells.SpellData spell, float secondsRemaining, bool showCountdown)
        {
            string localizedName = LocalizationManager.Instance.GetText(spell.Spell.ToString());
            attributeText.text = showCountdown ? $"{localizedName} {Mathf.CeilToInt(secondsRemaining)}s" : localizedName;
            tooltipTrigger = GetComponent<MemoriTooltipTrigger>();
            tooltipTrigger.enabled = true;
            tooltipTrigger.SetUpToolTip(_title: localizedName, _description: KeywordText.ForTooltip(spell.GetLocalizedSpellDescription()));

            if (_icon == null)
                foreach (Image image in GetComponentsInChildren<Image>(true))
                    if (image.name == "Icon") { _icon = image; break; }
            if (_icon != null)
            {
                _icon.sprite = spell.SpellSprite;
                _icon.color = ColorData.GetRaceDisplayColor(spell.Race);
            }
        }
        #region Colorblind Mode

        private Image _icon;
        private Color _iconOff, _textOff;
        private bool _colorsCached;

        // Only the default trait green pairs with the gold prestige badge; status variants keep their own colours.
        private void ApplyColorVision()
        {
            if (!_colorsCached)
            {
                _colorsCached = true;
                foreach (Image image in GetComponentsInChildren<Image>(true))
                    if (image.name == "Icon") { _icon = image; break; }
                if (_icon != null) _iconOff = _icon.color;
                _textOff = attributeText.color;
            }
            if (_icon != null && IsTraitGreen(_iconOff)) _icon.color = ColorVision.Good(_iconOff);
            if (IsTraitGreen(_textOff)) attributeText.color = ColorVision.Good(_textOff);
        }

        private static bool IsTraitGreen(Color c) => Mathf.Abs(c.r - 0.26f) < 0.03f && Mathf.Abs(c.g - 0.96f) < 0.03f && Mathf.Abs(c.b - 0.42f) < 0.03f;

        #endregion
        public void SetUpTooltip()
        {
            string localizedAttribute = LocalizationManager.Instance.GetText(unitAttribute.ToString());
            string localizedDescription = LocalizationManager.Instance.GetText(unitAttribute.ToString() + "Desc");
            tooltipTrigger = GetComponent<MemoriTooltipTrigger>();
            tooltipTrigger.enabled = true;
            tooltipTrigger.SetUpToolTip(_title: localizedAttribute, _description: KeywordText.ForTooltip(localizedDescription, unitAttribute.ToString()));
        }
    }
}