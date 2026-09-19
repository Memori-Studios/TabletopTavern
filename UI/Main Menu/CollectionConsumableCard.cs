using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TJ.Shop;
using Memori.Utilities;
using Memori.Audio;
using UnityEngine.EventSystems;
using MoreMountains.Feedbacks;
using Memori.SaveData;
using Memori.Localization;

namespace TJ.MainMenu
{
public class CollectionConsumableCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip")]
    [SerializeField] private CanvasGroup tooltipCanvasGroup;
    [SerializeField] private TMP_Text gearName;
    [SerializeField] private TMP_Text gearDescriptionText, gearFlavorText;

    [SerializeField] private Image gearImage, gearRarityImage, gearRarityImage2, gearRarityTracery1, gearRarityTracery2;
    [SerializeField] private Image mouseOverHighlight1, mouseOverHighlight2;

    ConsumableEnum consumableEnum;
    Consumable consumable;
    bool isCollected, acknowledged;
    [SerializeField] private GameObject collectionUnacknowledgedIndicator;
    CollectionPanel collectionPanel;

    public void LoadConsumableCard(ConsumableEnum _consumableEnum, bool _isCollected, bool _acknowledged, CollectionPanel _collectionPanel)
    {
        consumableEnum = _consumableEnum;
        consumable = ConsumableData.GetConsumable(consumableEnum);
        isCollected = _isCollected;
        acknowledged = _acknowledged;
        collectionPanel = _collectionPanel;

        gearImage.sprite = SpriteData.GetSprite(consumable.ConsumableEnum.ToString());

        gearName.text = isCollected ? LocalizationManager.Instance.GetText(consumable.ConsumableEnum.ToString()+"Name") : "Not Discoverd";

        gearDescriptionText.text = isCollected ? ConsumableData.FormatDescription(consumable.ConsumableEnum, LocalizationManager.Instance.GetText(consumable.ConsumableEnum.ToString()+"Desc")) : "Obtain in Campaign";
        gearImage.color = isCollected ? Color.white : new Color(0.1f, 0.1f, 0.1f, 1f);
        gearFlavorText.text = "";
        
        // gearRarityTracery1.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 5f/255f);
        // gearRarityTracery2.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 5f/255f);
        // gearRarityImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 25f/255f);
        // gearRarityImage2.color = isCollected ? new Color(rarityColor.r, rarityColor.g, rarityColor.b, 25f/255f) : new Color(0.1f, 0.1f, 0.1f, 1f);

        tooltipCanvasGroup.CGDisable();

        if(!isCollected) collectionUnacknowledgedIndicator.SetActive(false);
        else
        collectionUnacknowledgedIndicator.SetActive(!acknowledged);
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
        tooltipCanvasGroup.CGEnable();
        mouseOverHighlight1.enabled = true;
        mouseOverHighlight2.enabled = true;

        if(!acknowledged && isCollected)
        {
            acknowledged = true;
            SaveDataHandler.AcknowledgedPotion(consumableEnum);
            collectionUnacknowledgedIndicator.SetActive(false);
            collectionPanel.UpdateAcknowledged();
        }
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        // IAudioRequester.Instance.PlaySFX(SFXData.TinyClick);
        tooltipCanvasGroup.CGDisable();
        mouseOverHighlight1.enabled = false;
        mouseOverHighlight2.enabled = false;
    }
}
}