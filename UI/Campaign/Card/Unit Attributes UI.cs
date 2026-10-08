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
        // The authored chip width, kept so a pooled chip widened for a long Campaign Trait name can go back.
        float authoredWidth = -1f;
        public void Load(UnitAttribute _unitAttribute)
        {
            if (authoredWidth >= 0f) SetWidth(authoredWidth);
            unitAttribute = _unitAttribute;
            string localizedAttribute = LocalizationManager.Instance.GetText(unitAttribute.ToString());
            // string localizedDescription = LocalizationManager.Instance.GetText(unitAttribute.ToString() + "Desc");

            attributeText.text = localizedAttribute;
            tooltipTrigger = GetComponent<MemoriTooltipTrigger>();
            tooltipTrigger.enabled = false;
            ApplyColorVision();
            // tooltipTrigger.SetUpToolTip(_description: localizedDescription);
            FitWidthToName(localizedAttribute);
        }
        /// <summary>Shows a unit trait that only works on the campaign side as a Campaign Trait chip.</summary>
        public void LoadCampaignTrait(UnitAttribute _unitAttribute)
        {
            unitAttribute = _unitAttribute;
            string traitName = LocalizationManager.Instance.GetText(_unitAttribute.ToString());
            string description = LocalizationManager.Instance.GetText(_unitAttribute.ToString() + "Desc");
            LoadCampaignTrait(traitName, $"{CampaignTraitText.Caption}: {traitName}", KeywordText.ForTooltip(description, _unitAttribute.ToString()));
        }
        /// <summary>Shows a Campaign Trait as a plain trait chip; its rules live in the tooltip.</summary>
        public void LoadCampaignTrait(string traitName, string tooltipTitle, string lines)
        {
            if (authoredWidth >= 0f) SetWidth(authoredWidth);
            attributeText.text = traitName;
            tooltipTrigger = GetComponent<MemoriTooltipTrigger>();
            tooltipTrigger.enabled = true;
            ApplyCampaignLook();
            tooltipTrigger.SetUpToolTip(_title: tooltipTitle, _description: lines);
            FitWidthToName(traitName);
        }
        // A trait name can outgrow the authored chip and wrap into its neighbour; where the parent leaves widths alone, the chip widens instead.
        private void FitWidthToName(string traitName)
        {
            RectTransform chip = (RectTransform)transform;
            if (authoredWidth < 0f) authoredWidth = chip.rect.width;
            float textRoom = ((RectTransform)attributeText.transform).rect.width;
            float needed = attributeText.GetPreferredValues(traitName, 9999f, 9999f).x;
            if (needed <= textRoom) return;
            float maxWidth = transform.parent is RectTransform parentRect ? parentRect.rect.width : authoredWidth;
            SetWidth(Mathf.Min(maxWidth, authoredWidth + needed - textRoom + 2f));
        }
        private void SetWidth(float width) => ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        public void Load(UnitCondition _unitCondition)
        {
            unitCondition = _unitCondition;
            string localizedAttribute = LocalizationManager.Instance.GetText(unitCondition.ToString());
            string localizedDescription = LocalizationManager.Instance.GetText(unitCondition.ToString() + "Desc");

            attributeText.text = localizedAttribute;
            tooltipTrigger = GetComponent<MemoriTooltipTrigger>();
            tooltipTrigger.SetUpToolTip(_description: KeywordText.ForTooltip(localizedDescription, unitCondition.ToString()));
        }
        #region Colorblind Mode

        // The map's Town castle stands in for the "+" on a Campaign Trait chip.
        private const string CampaignIconKey = "Town";

        private Image _icon;
        private Sprite _iconSpriteOff;
        private bool _iconPreserveAspectOff;
        private Color _iconOff, _textOff;
        private bool _colorsCached;

        private void CacheAuthoredLook()
        {
            if (_colorsCached) return;
            _colorsCached = true;
            foreach (Image image in GetComponentsInChildren<Image>(true))
                if (image.name == "Icon") { _icon = image; break; }
            if (_icon != null)
            {
                _iconOff = _icon.color;
                _iconSpriteOff = _icon.sprite;
                _iconPreserveAspectOff = _icon.preserveAspect;
            }
            _textOff = attributeText.color;
        }

        // Only the default trait green pairs with the gold prestige badge; status variants keep their own colours.
        // Sets every value the campaign look changes, so a pooled chip always returns to its authored look.
        private void ApplyColorVision()
        {
            CacheAuthoredLook();
            if (_icon != null)
            {
                _icon.sprite = _iconSpriteOff;
                _icon.preserveAspect = _iconPreserveAspectOff;
                _icon.color = IsTraitGreen(_iconOff) ? ColorVision.Good(_iconOff) : _iconOff;
            }
            attributeText.color = IsTraitGreen(_textOff) ? ColorVision.Good(_textOff) : _textOff;
        }

        // One colour in both modes: Colorblind Mode moves trait green to blue, never to parchment.
        private void ApplyCampaignLook()
        {
            CacheAuthoredLook();
            Color campaign = (Color)ColorData.HexToRgba(ColorData.CampaignTrait);
            if (_icon != null)
            {
                Sprite castle = SpriteData.GetSprite(CampaignIconKey);
                if (castle != null)
                {
                    _icon.sprite = castle;
                    _icon.preserveAspect = true;
                }
                _icon.color = campaign;
            }
            attributeText.color = campaign;
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