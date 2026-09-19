using Memori.Tooltip;
using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Memori.Localization;

namespace TJ.Engagement
{
[RequireComponent(typeof(MemoriTooltipTrigger))]
public class PotionRewardsItem : MemoriButtonV2
{
    [SerializeField] private Consumable consumable;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text lableText;
    MemoriTooltipTrigger tooltipTrigger;
    private void Awake() {
        tooltipTrigger = GetComponent<MemoriTooltipTrigger>();
    }
    public void SetUp(ConsumableEnum _consumableEnum)
    {
        consumable = ConsumableData.GetConsumable(_consumableEnum);
        icon.sprite = SpriteData.GetSprite(_consumableEnum.ToString());
        lableText.text= LocalizationManager.Instance.GetText(consumable.ConsumableEnum.ToString()+"Name");
        tooltipTrigger.SetUpToolTip(_description: ConsumableData.FormatDescription(consumable.ConsumableEnum, LocalizationManager.Instance.GetText(consumable.ConsumableEnum.ToString()+"Desc")));
    }
}
}