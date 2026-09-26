using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;
using Memori.Notifications;
using Memori.Audio;
using Memori.Localization;
using QuickOutline;
using UnityEngine.UI;

namespace TJ.Shop
{
    public class CardPack : MonoBehaviour
    {
        [SerializeField] private CardPackData cardPackData;
        [SerializeField] private ShopPriceCanvas shopPriceCanvas;
        [SerializeField] private ShopItemInfoCanvas shopItemInfoCanvas;
        [SerializeField] bool gearPack;
        [SerializeField] private MMF_Player hoverFeedback;
        [SerializeField] private MMF_Player purchaseFeedback;
        [SerializeField] private MMF_Player hoverOutFeedback;
        [SerializeField] private MMF_Player spawnInFeedback;
        [SerializeField] private GameObject highlightGO;
        [SerializeField] private Image cardImage;
        [SerializeField] private QuickOutline.Outline outline;

        ShopPanel shopPanel;
        int cost, _discount;
        public void SetUp(CardPackData _cardPackData, ShopPanel _shopPanel, int discount)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ShopItem);
            shopPanel = _shopPanel;
            spawnInFeedback.PlayFeedbacks();
            cardPackData = _cardPackData;
            _discount = discount;
            
            gearPack = cardPackData.packID == 0 ? true : false;

            Color color = cardPackData.packID == 1 ? ColorData.HexToRgba(ColorData.Tier1) :
                            cardPackData.packID == 2 ? ColorData.HexToRgba(ColorData.Tier2) :
                            cardPackData.packID == 3 ? ColorData.HexToRgba(ColorData.Tier3) :
                            cardPackData.packID == 4 ? ColorData.HexToRgba(ColorData.Tier4) :
                            Color.white;
            cardImage.color = color;
            
            string packNameLocalized = LocalizationManager.Instance.GetText(cardPackData.packName);
            string packDescriptionLocalized = LocalizationManager.Instance.GetText(cardPackData.packDescription);
            RefreshPrice();
            shopItemInfoCanvas.SetUp(packNameLocalized, packDescriptionLocalized);
        }
        private IEnumerator OutlinePulse()
        {
            float startWidth = outline.OutlineWidth;
            float endWidth = outline.OutlineWidth / 2f;
            
            while (this.gameObject.activeSelf)
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 1f;
                    outline.OutlineWidth = Mathf.Lerp(startWidth, endWidth, t);
                    yield return null;
                }
                t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 1f;
                    outline.OutlineWidth = Mathf.Lerp(endWidth, startWidth, t);
                    yield return null;
                }
            }
        }
        public void HoverPack(bool _hover)
        {
            if (_hover)
            {
                highlightGO.SetActive(true);
                hoverOutFeedback.StopFeedbacks();
                hoverFeedback.PlayFeedbacks();
                IAudioRequester.Instance.PlaySFX(SFXData.ShopItem);

                if (gearPack)
                {
                    outline.enabled = true;
                }
            }
            else
            {
                highlightGO.SetActive(false);
                hoverFeedback.StopFeedbacks();
                hoverOutFeedback.PlayFeedbacks();
                if (gearPack)
                {
                    outline.enabled = false;
                }
            }
        }
        public void AttemptPurchase()
        {
            if(!CampaignManager.Instance.GoldManager.CheckIfCanAfford(cost)) {
                NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("notEnoughGold"));
                shopPanel.RenableShopPanel();
                return;
            }

            shopPanel.PurchasePack(cardPackData, cost);
        }
        public void RefreshPrice()
        {
            cost = cardPackData.packPrice;
            int activeHeroID = HeroBonusManager.Instance.ActiveHeroID;
            switch(cardPackData.packID)
            {
                case 0:
                {
                    if (CampaignManager.Instance.GearManager.CheckForGear(GearID.PrivateeringPapers))
                    {
                        if (shopPanel != null) {
                            if(shopPanel.GearPacksPurchased == 0) {
                                cost = 0;
                                shopPriceCanvas.SetUp(cost.ToString());
                                return;
                            }
                        }
                    }
                    break;
                }
                case 1:
                    if (CampaignManager.Instance.GearManager.CheckForGear(GearID.CommonBuilder)) {
                        cost -= GearData.GetGear(GearID.CommonBuilder).GearModifierValue;
                    }
                    break;
                case 2:
                    if(CampaignManager.Instance.GearManager.CheckForGear(GearID.UncommonBuilder)) cost -= GearData.GetGear(GearID.UncommonBuilder).GearModifierValue;
                    break;
                case 3:
                    if(CampaignManager.Instance.GearManager.CheckForGear(GearID.RareBuilder)) cost -= GearData.GetGear(GearID.RareBuilder).GearModifierValue;
                    break;
                case 4:
                    cost += CampaignManager.Instance.CampaignSaveManager.SaveData.signatureUnitPacksPurchased * 25;
                    break;
            }

            //Drums in the Deep: Pack cost Reduced to 5 for all Common Packs
            if(activeHeroID == 3 && cardPackData.packID == 1)
            {
                cost = 5;
            }

            cost -= _discount;
            shopPriceCanvas.SetUp(cost.ToString());
        }
    }
}