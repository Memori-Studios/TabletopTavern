using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using MoreMountains.Feedbacks;
using Memori.Notifications;
using Memori.Audio;
using Memori.Localization;
using Memori.Steamworks;
using QuickOutline;

namespace TJ.Shop
{
[System.Serializable] public struct ConsumableEnumToGameObject
{
    public ConsumableEnum consumableEnum;
    public GameObject consumableIconPrefab;
}
public class ShopConsumable : MonoBehaviour
{
    [SerializeField] private ConsumableEnum consumableType;
    [SerializeField] private int consumablePrice;
    [SerializeField] private ShopPriceCanvas shopPriceCanvas;
    [SerializeField] private ShopItemInfoCanvas shopItemInfoCanvas;
    [SerializeField] private List<ConsumableEnumToGameObject> consumableEnumToGameObjectList;
    [SerializeField] private Transform consumableGOParentTransform;
    [SerializeField] private MMF_Player hoverFeedback;
    [SerializeField] private MMF_Player purchaseFeedback;
    [SerializeField] private MMF_Player hoverOutFeedback;
    [SerializeField] private MMF_Player spawnInFeedback;

    private Transform consumableGameObjectTransform;
    Outline outline;
    ShopPanel shopPanel;
    // The price the shop passed in: the base cost minus any Renown discount, before difficulty and gear.
    int basePrice;
    bool soldOut;
    public ConsumableEnum ConsumableType => consumableType;
    public int Price => consumablePrice;
    public bool SoldOut => soldOut;

    public void SetUp(ConsumableEnum _consumableType, int _consumablePrice, ShopPanel _shopPanel, bool playSound = true)
    {
        void CreateConsumableGameObject() {
            foreach (var consumableEnumToGameObject in consumableEnumToGameObjectList) {
                if (consumableEnumToGameObject.consumableEnum == consumableType) {
                    Instantiate(consumableEnumToGameObject.consumableIconPrefab, consumableGOParentTransform);
                    break;
                }
            }
        }
        consumableType = _consumableType;
        basePrice = _consumablePrice;
        shopPanel = _shopPanel;
        consumablePrice = CurrentPrice();
        spawnInFeedback.PlayFeedbacks();
        CreateConsumableGameObject();
        outline = GetComponentInChildren<Outline>();
        shopPriceCanvas.SetUp(consumablePrice.ToString());
        StartCoroutine(OutlinePulse());
        if (playSound) IAudioRequester.Instance.PlaySFX(SFXData.Drink);

        string consumableNameLocalized = LocalizationManager.Instance.GetText(_consumableType.ToString()+"Name");
        string consumableDescriptionLocalized = CampaignManager.Instance.ConsumableManager.GetConsumableDescription(_consumableType);
        shopItemInfoCanvas.SetUp(consumableNameLocalized, consumableDescriptionLocalized);
    }
    private IEnumerator OutlinePulse()
    {
        float startWidth = outline.OutlineWidth;
        float endWidth = outline.OutlineWidth / 2f;
        
        while (true)
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
    public void HoverConsumable(bool _hover)
    {
        if (_hover)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.Drink);
            hoverOutFeedback.StopFeedbacks();
            hoverFeedback.PlayFeedbacks();
        }
        else
        {
            hoverFeedback.StopFeedbacks();
            hoverOutFeedback.PlayFeedbacks();
        }
    }
    public void MarkSoldOut()
    {
        soldOut = true;
        if (shopPriceCanvas != null) shopPriceCanvas.SetSoldOut(true);
        shopItemInfoCanvas.AppendLine($"<color={ColorData.Error}>{LocalizationManager.Instance.GetText("OrdealSoldOut")}</color>");
    }
    private Coroutine refuseShake;
    private void ShakeRefused()
    {
        if (refuseShake != null || !isActiveAndEnabled) return;
        refuseShake = StartCoroutine(Shake());
        IEnumerator Shake() { yield return ShopRefusal.ShakeModel(consumableGOParentTransform); refuseShake = null; }
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
        if(!CampaignManager.Instance.GoldManager.CheckIfCanAfford(consumablePrice)) {
            NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("notEnoughGold"));
            shopPriceCanvas.Refuse();
            ShakeRefused();
            shopPanel.RenableShopPanel();
            return;
        }

        if(!CampaignManager.Instance.CampaignSaveManager.HasRoomForConsumable()){
            NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("noRoomForConsumable"));
            shopPriceCanvas.Refuse();
            ShakeRefused();
            shopPanel.RenableShopPanel();
            return;
        }
        SteamAchievements.AddStat(SteamStatId.ShopPurchases, 1);
        CampaignManager.Instance.CampaignSaveManager.RegisterShopPurchase();
        string localizedString = LocalizationManager.Instance.GetText($"{consumableType}Name");
        CampaignManager.Instance.GoldManager.ModifyGold(-consumablePrice, localizedString);
        int paid = consumablePrice;
        TabletopTavern.Analytics.NodeLog.Try("shop buy", () => TabletopTavern.Analytics.NodeLog.Add("buys",
            new Dictionary<string, object> { { "k", "cons" }, { "id", consumableType.ToString() }, { "price", paid } }));
        shopPanel.ConsumablePurchased();
        
        CampaignManager.Instance.CampaignSaveManager.AquireConsumable(consumableType);
        IAudioRequester.Instance.PlaySFX(SFXData.Purchase);
        purchaseFeedback.PlayFeedbacks();
    }
    public void ReEnableCollider()
    {
        // MeshCollider meshCollider = consumableGameObjectTransform.GetComponentInChildren<MeshCollider>();
        // meshCollider.enabled = false;
        // meshCollider.enabled = true;
        Physics.SyncTransforms();
    }
    public void TurnOff()
    {
        Destroy(gameObject);
        shopPanel.RenableShopPanel();
    }
    // The first price and every refresh use one rule, so a purchase can never change the other item's price.
    private int CurrentPrice()
    {
        // Cookie and Fowl Card makes only the first consumable of each shop visit free.
        if (shopPanel.ConsumablesPurchased == 0 && CampaignManager.Instance.GearManager.CheckForGear(GearID.CookieAndFowlCard)) return 0;
        if (CampaignManager.Instance.CampaignSaveManager.SaveData.HasOrdeal(OrdealId.IronCoffers)) return basePrice + OrdealRegistry.IRON_COFFERS_PRICE_RISE;
        return basePrice;
    }
    public void RefreshPrices()
    {
        consumablePrice = CurrentPrice();
        if(shopPriceCanvas != null)
            shopPriceCanvas.SetUp(consumablePrice.ToString());
    }
}
}