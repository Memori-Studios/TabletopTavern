using Memori.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using Memori.Audio;
using TMPro;
using Memori.Localization;

namespace TJ.Town
{
public class TownInfoTooltip : MemoriButtonV2
{
    [SerializeField] private GameObject tooltip;
    [SerializeField] private TMP_Text descText;
    private void Awake()
    {
        tooltip.SetActive(false);

        string descriptionLocalized = LocalizationManager.Instance.GetText("townDescription");
        string garrisonLocalized = LocalizationManager.Instance.GetText("Garrison");

        string bountyLocalized = LocalizationManager.Instance.GetText("Bounty For Sacking");
        // string merchantDistrictLocalized = LocalizationManager.Instance.GetText("Merchant District");
        // string recruitmentOptionsLocalized = LocalizationManager.Instance.GetText("Recruitment Options");
        string garrisonUnitsLocalized = LocalizationManager.Instance.GetText("Garrison Units");

        string villageLocalized = LocalizationManager.Instance.GetText("Village");
        string castleLocalized = LocalizationManager.Instance.GetText("Castle");
        string cityLocalized = LocalizationManager.Instance.GetText("City");


        // string lightLocalized = LocalizationManager.Instance.GetText("Light");
        // string strongLocalized = LocalizationManager.Instance.GetText("Strong");
        // string eliteLocalized = LocalizationManager.Instance.GetText("Elite");

        // string poorLocalized = LocalizationManager.Instance.GetText("Poor");
        // string middleclassLocalized = LocalizationManager.Instance.GetText("Middleclass");
        // string prosperousLocalized = LocalizationManager.Instance.GetText("Prosperous");
        // string goldLocalized = LocalizationManager.Instance.GetText("Gold");

        // string smallLocalized = LocalizationManager.Instance.GetText("Small");
        // string mediumLocalized = LocalizationManager.Instance.GetText("Medium");
        // string largeLocalized = LocalizationManager.Instance.GetText("Large");
        // string gearItemsLocalized = LocalizationManager.Instance.GetText("Gear Items in Shop");
        // string noConsumablesLocalized = LocalizationManager.Instance.GetText("No Consumables");

        // string weakLocalized = LocalizationManager.Instance.GetText("Weak");
        // string moderateLocalized = LocalizationManager.Instance.GetText("Moderate");
        // string strongLocalized2 = LocalizationManager.Instance.GetText("Strong");
        // string unitsAvailableLocalized = LocalizationManager.Instance.GetText("UnitsAvailable");

        string description = "";
        description += descriptionLocalized;

        description += $"\n\n{garrisonLocalized}:<color {ColorData.Tier1}></color>";
        description += $"\n<color {ColorData.Tier1}>{villageLocalized}: 4 {garrisonUnitsLocalized}</color>";
        description += $"\n<color {ColorData.Tier2}>{castleLocalized}: 6 {garrisonUnitsLocalized}</color>";
        description += $"\n<color {ColorData.Tier3}>{cityLocalized}: 7 {garrisonUnitsLocalized}</color>";

        description += $"\n\n{bountyLocalized}:<color {ColorData.Tier1}></color>";
        description += $"\n<color {ColorData.Tier1}>{villageLocalized}: 4-6 </color><sprite name=GoldSprite>";
        description += $"\n<color {ColorData.Tier2}>{castleLocalized}: 9-11 </color><sprite name=GoldSprite>";
        description += $"\n<color {ColorData.Tier3}>{cityLocalized}: 14-16 </color><sprite name=GoldSprite>";

        // description += $"\n\n{merchantDistrictLocalized}:<color {ColorData.Tier1}></color>";
        // description += $"\n<color {ColorData.Tier1}>{smallLocalized}: 3 {gearItemsLocalized} ({noConsumablesLocalized})</color>";
        // description += $"\n<color {ColorData.Tier2}>{mediumLocalized}: 5 {gearItemsLocalized}</color>";
        // description += $"\n<color {ColorData.Tier3}>{largeLocalized}: 7 {gearItemsLocalized}</color>";

        // description += $"\n\n{recruitmentOptionsLocalized}:<color {ColorData.Tier1}></color>";
        // description += $"\n<color {ColorData.Tier1}>{weakLocalized}: 1 {unitsAvailableLocalized}</color>";
        // description += $"\n<color {ColorData.Tier2}>{moderateLocalized}: 2 {unitsAvailableLocalized}</color>";
        // description += $"\n<color {ColorData.Tier3}>{strongLocalized2}: 3 {unitsAvailableLocalized}</color>";

        descText.text = description;
    }
    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        tooltip.SetActive(true);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        tooltip.SetActive(false);
    }
}
}
