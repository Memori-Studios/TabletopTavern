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
        [Tooltip("The pack's model, shaken on a refused purchase; the hover feedbacks move the root.")]
        [SerializeField] private Transform packModel;
        Coroutine refuseShake;

        ShopPanel shopPanel;
        int cost, _discount;
        bool soldOut;
        public int PackId => cardPackData.packID;
        public int Cost => cost;
        public bool SoldOut => soldOut;
        public void SetUp(CardPackData _cardPackData, ShopPanel _shopPanel, int discount, bool playSound = true)
        {
            if (playSound) IAudioRequester.Instance.PlaySFX(SFXData.ShopItem);
            shopPanel = _shopPanel;
            soldOut = false;
            shopPriceCanvas.SetSoldOut(false);
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
        public void MarkSoldOut()
        {
            soldOut = true;
            shopPriceCanvas.SetSoldOut(true);
            cardImage.color *= new Color(0.5f, 0.5f, 0.5f, 1f);
            shopItemInfoCanvas.AppendLine($"<color={ColorData.Error}>{LocalizationManager.Instance.GetText("OrdealSoldOut")}</color>");
        }
        private void ShakeRefused()
        {
            if (refuseShake != null || !isActiveAndEnabled) return;
            refuseShake = StartCoroutine(Shake());
            IEnumerator Shake() { yield return ShopRefusal.ShakeModel(packModel); refuseShake = null; }
        }
        public void AttemptPurchase()
        {
            if (soldOut) {
                NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("OrdealSoldOutNotice"));
                shopPriceCanvas.Refuse();
                ShakeRefused();
                shopPanel.RenableShopPanel();
                return;
            }
            if(!CampaignManager.Instance.GoldManager.CheckIfCanAfford(cost)) {
                NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("notEnoughGold"));
                shopPriceCanvas.Refuse();
                ShakeRefused();
                shopPanel.RenableShopPanel();
                return;
            }

            shopPanel.PurchasePack(cardPackData, cost);
        }
        public void RefreshPrice()
        {
            cost = cardPackData.packPrice;
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

            // After every set price above, so a set price is not free of the rise.
            if (CampaignManager.Instance.CampaignSaveManager.SaveData.HasOrdeal(OrdealId.IronCoffers)) cost += OrdealRegistry.IRON_COFFERS_PRICE_RISE;

            cost -= _discount;
            shopPriceCanvas.SetUp(cost.ToString());
        }
    }
}