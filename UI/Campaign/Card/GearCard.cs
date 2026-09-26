using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TJ.Shop;
using Memori.Notifications;
using Memori.Audio;
using UnityEngine.EventSystems;
using MoreMountains.Feedbacks;
using Memori.Localization;
using TJ.Recruit;
using Memori.Core;
using Memori.SaveData;

namespace TJ
{
[RequireComponent(typeof(MemoriButtonV2))]
public class GearCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text gearName;
    [SerializeField] private TMP_Text gearDescriptionText, gearFlavorText;//gearRarity, gearTypeText, 
    [SerializeField] private Image gearImage, gearRarityImage, gearRarityTracery1, gearRarityTracery2;
    [SerializeField] private Image mouseOverHighlight1, mouseOverHighlight2;
    [SerializeField] private GearModifierUI gearModifierUIPrefab;
    [SerializeField] private Transform gearModifierUIParent;
       
    [SerializeField] private GameObject newNotificationActive;

    [Header("Purchase")]
    [SerializeField] private MMF_Player purchaseMMF;

    GearID gearID;
    public GearID GearID => gearID;
    Gear gear;
    public Gear Gear => gear;
    MemoriButtonV2 memoriButton;
    CardPanel cardPanel;
    bool isShop;
    RecruitPanel recruitPanel;
    int cost;
    public int Cost => cost;
    public void LoadGearCardReward(GearID _gear, CardPanel _cardPanel)
    {
        LoadGearCard(_gear);
        isShop = false;
        cardPanel = _cardPanel;
        memoriButton = GetComponent<MemoriButtonV2>();
        memoriButton.Button.onClick.RemoveAllListeners();
        memoriButton.Button.onClick.AddListener(SelectGearCard);
    }
    public void LoadGearCardShop(GearID _gear, RecruitPanel _recruitPanel)
    {
        recruitPanel = _recruitPanel;
        LoadGearCard(_gear);

        isShop = true;
        memoriButton.Button.onClick.RemoveAllListeners();
        memoriButton.Button.onClick.AddListener(SelectGearCard);
    }
    public void LoadGearCard(GearID _gear)
    {
        memoriButton = GetComponent<MemoriButtonV2>();
        gearID = _gear;
        if(gearID == GearID.None) {
            LoadEmptyGearCard();
            return;
        }

        gear = GearData.GetGear(gearID);

        string gearNameLocalized = LocalizationManager.Instance.GetText(gearID+"Name");
        string gearDescLocalized = LocalizationManager.Instance.GetText(gearID+"Desc");
        gearDescLocalized = string.Format(gearDescLocalized, gear.GearModifierValue);
        string gearFlavorLocalized = LocalizationManager.Instance.GetText(gearID+"Flavor");
        if(gearName != null)
        {
            gearName.text = gearNameLocalized;
            KeywordText.Show(gearDescriptionText, $"{ColorData.GearRarityLabel(gear.GearRarity)}\n{KeywordText.Render(gearDescLocalized)}");
            gearFlavorText.text = gearFlavorLocalized;
        }

        Color rarityColor = ColorData.GetGearRarityColor(gear.GearRarity);
        gearRarityTracery1.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 5f/255f);
        gearRarityTracery2.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 5f/255f);
        gearRarityImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 25f/255f);

        gearImage.sprite = SpriteData.GetSprite(gear.GearName);
        gearImage.enabled = true;
        for(int i = 0; i < gearModifierUIParent.childCount; i++) {
            Destroy(gearModifierUIParent.GetChild(i).gameObject);
        }

        bool isNew = !SaveDataHandler.GetGearIDsCollected().Contains((int)gearID);
        if (newNotificationActive != null) newNotificationActive.SetActive(isNew);

        if(gear.GearModifierValue == 0) return;

        GearModifierUI gearModifierUI = Instantiate(gearModifierUIPrefab, gearModifierUIParent);
        gearModifierUI.LoadGearModifierUI(gear);
    }
    public void LoadEmptyGearCard()
    {
        string gearNameLocalized = LocalizationManager.Instance.GetText("None");
        if(gearName != null)
        {
            gearName.text = gearNameLocalized;
            gearDescriptionText.text = "";
            gearFlavorText.text = "";
        }
        gearImage.enabled = false;
        if (newNotificationActive != null) newNotificationActive.SetActive(false);
    }
    public void SelectGearCard()
    {
        memoriButton.OnMouseDown();
        if (isShop) {
            recruitPanel.AttemptToPurchaseGear(this);
        } else {
            if(!CampaignManager.Instance.CampaignSaveManager.CanAquireGear()){
                string errorLocalized = LocalizationManager.Instance.GetText("No space for gear");
                NotificationManager.Instance.ErrorNotification(errorLocalized);
                return;
            }
            cardPanel.SelectCard(gearID);
        }
    }
    public void PlayPurchaseFeedbacks()
    {
        purchaseMMF.PlayFeedbacks();
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        mouseOverHighlight1.enabled = true;
        mouseOverHighlight2.enabled = true;
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        mouseOverHighlight1.enabled = false;
        mouseOverHighlight2.enabled = false;
    }
        public void DarkenCard()
        {
            //this is triggered on all cards that are not selected, should get every text and image and set it to it's current color but slightly darker
            Color darkenColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            Image[] images = GetComponentsInChildren<Image>();
            for (int i = 0; i < images.Length; i++)
            {
                //if color is black, skip it
                if (images[i].color == Color.black) continue;
                images[i].color = images[i].color * darkenColor;
            }
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].color = texts[i].color * darkenColor;
            }
        }
    }
}