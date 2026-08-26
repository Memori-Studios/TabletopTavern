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
using Memori.Notifications;
using Memori.Tooltip;

namespace TJ.MainMenu
{
    public class CollectionGearCard : MemoriButtonV2, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Tooltip")]
        [SerializeField] private MemoriTooltipTrigger memoriTooltipTrigger;
        // [SerializeField] private CanvasGroup tooltipCanvasGroup;
        // [SerializeField] private TMP_Text gearName;
        // [SerializeField] private TMP_Text gearDescriptionText, gearFlavorText;
        [SerializeField] private Image mouseOverHighlight1; //, mouseOverHighlight2;
        [SerializeField] private Image gearImage, gearRarityImage,  gearRarityTracery1; // gearRarityImage2, gearRarityTracery2,
        [SerializeField] private Animator iconAnimator;

        GearID gearID;
        Gear gear;
        bool isCollected, acknowledged;
        [SerializeField] private GameObject collectionUnacknowledgedIndicator;
        CollectionPanel collectionPanel;
        StartingArmyManager startingGear;

        public void LoadGearCard(GearID _gear, bool _isCollected, bool _acknowledged, CollectionPanel _collectionPanel = null, StartingArmyManager _startingGear = null)
        {
            gearID = _gear;
            gear = GearData.GetGear(gearID);
            isCollected = _isCollected;
            acknowledged = _acknowledged;
            collectionPanel = _collectionPanel;
            startingGear = _startingGear;

            Color rarityColor = ColorData.GetGearRarityColor(gear.GearRarity);
            gearImage.sprite = SpriteData.GetSprite(gear.GearName);

            string gearNameLocalized = LocalizationManager.Instance.GetText(gearID+"Name");
            string gearDescLocalized = LocalizationManager.Instance.GetText(gearID+"Desc");
            gearDescLocalized = string.Format(gearDescLocalized, gear.GearModifierValue);
            string gearFlavorLocalized = LocalizationManager.Instance.GetText(gearID+"Flavor");

            ColorData.XMLTagColorApplicator(ref gearDescLocalized);

            string title = isCollected ? gearNameLocalized : LocalizationManager.Instance.GetText("Gear Not Discoverd");
            string desc = isCollected ? gearDescLocalized: LocalizationManager.Instance.GetText("Obtain in Campaign");
            string flavor = isCollected ? gearFlavorLocalized : "????????";

            gearImage.color = isCollected ? Color.white : new Color(0.1f, 0.1f, 0.1f, 1f);
            
            gearRarityTracery1.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 5f/255f);
            gearRarityImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 25f/255f);

            // tooltipCanvasGroup.CGDisable();

            if(collectionPanel != null) {
                if(!isCollected) {
                    collectionUnacknowledgedIndicator.SetActive(false);
                } else {
                    collectionUnacknowledgedIndicator.SetActive(!acknowledged);
                }
            } else {
                collectionUnacknowledgedIndicator.SetActive(false);
            }

            memoriTooltipTrigger.SetUpToolTip(title, desc, flavor);
        }
        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            // IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
            // tooltipCanvasGroup.CGEnable();
            mouseOverHighlight1.enabled = true;
            // mouseOverHighlight2.enabled = true;
            iconAnimator.SetTrigger("Highlighted");

            if (!acknowledged && isCollected)
            {
                acknowledged = true;
                SaveDataHandler.AcknowledgedGear(gearID);
                collectionUnacknowledgedIndicator.SetActive(false);
                if (collectionPanel != null)
                    collectionPanel.UpdateAcknowledged();
            }
        }
        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            // tooltipCanvasGroup.CGDisable();
            mouseOverHighlight1.enabled = false;
            // mouseOverHighlight2.enabled = false;
            iconAnimator.SetTrigger("Normal");
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            if(startingGear == null) return;

            if(!isCollected) {
                NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("Gear Not Discoverd"));
                return;
            }

            startingGear.SelectGearCard(gearID);
        }
    }
}