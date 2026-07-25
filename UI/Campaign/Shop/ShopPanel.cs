using TJ.Map;
using UnityEngine;
using Memori.Utilities;
using System.Collections.Generic;
using UnityEngine.UI;
using Memori.Audio;
using Memori.SaveData;
using Memori.Notifications;
using TMPro;
using System.Threading.Tasks;
using Memori.Scenes;
using Memori.Input;
using TJ.Recruit;
using Memori.Tooltip;
using Memori.Steamworks;
using TJ.Treasure;
using Memori.Metaprogression;
using Memori.Localization;
namespace TJ.Shop
{
[System.Serializable] public class ShopSaveData
{
    public int shopSeed;
    public int packPurchasedCount;
    public List<ConsumableEnum> shopConsumables;
    public List<ConsumableEnum> shopConsumablesPurchased;
}
    public class ShopPanel : MapPanel
    {
        [SerializeField] private CardPack gearPack, cardPack1, cardPack2, cardPack3, cardPack4;
        [SerializeField] private ShopConsumable consumablePrefab;
        [SerializeField] private Transform consumableTransform1, consumableTransform2;
        [SerializeField] private Button closeButton;
        private List<ShopConsumable> shopConsumables = new List<ShopConsumable>();
        [SerializeField] private RecruitPanel recruitPanel;
        [SerializeField] private CardPack hoveredCardPack;
        [SerializeField] private ShopConsumable hoveredConsumable;
        [SerializeField] private Animator bartenderAnimator;
        [SerializeField] private Animator specialBartenderAnimator;
        [SerializeField] private CoinDispenser coinDispenser;

        [Header("Metaprogression")]
        [SerializeField] private MetaprogressionModel _gainGoldOnShopEnterMetaprogressionModel;
        [SerializeField] private MetaprogressionModel _pack1CostMetaprogressionModel, _pack2CostMetaprogressionModel, _pack3CostMetaprogressionModel, _gearPackCostMetaprogressionModel, _consumableDiscountMetaprogressionModel;

        private CampaignSaveManager campaignSaveManager;
        private MapSceneUIManager mapSceneUIManager;
        private TreasurePanel treasurePanel;
        private ShopSaveData shopSaveData;
        private MemoriCanvasGroup shopCanvasGroup;
        private bool shopSceneActive = false;
        private bool freeCameraMode = false;
        public void SetFreeCameraMode(bool _enabled) { freeCameraMode = _enabled; }
        private Outline[] outlines;
        private int consumablesPurchased = 0;
        public int ConsumablesPurchased => consumablesPurchased;
        private int gearPacksPurchased = 0;
        public int GearPacksPurchased => gearPacksPurchased;

        private void Awake()
        {
            shopCanvasGroup = GetComponent<MemoriCanvasGroup>();
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CompleteShopInteraction);
            outlines = GetComponentsInChildren<Outline>();
        }
        public void SetUp(CampaignSaveManager _campaignSaveManager, MapSceneUIManager _mapSceneUIManager)
        {
            campaignSaveManager = _campaignSaveManager;
            mapSceneUIManager = _mapSceneUIManager;
            treasurePanel = mapSceneUIManager.TreasurePanel;
            DisableShopPanel();
            InputHandler.Instance.PrimaryActionPerformed += LeftClick;
            campaignSaveManager.OnGearChanged += OnGearChanged;
        }
        public async void LoadShopPanel()
        {
            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_gainGoldOnShopEnterMetaprogressionModel))
            {
                string localizedString = LocalizationManager.Instance.GetText("Shop");
                CampaignManager.Instance.GoldManager.ModifyGold(_gainGoldOnShopEnterMetaprogressionModel.NodeValue, localizedString);
            }
            consumablesPurchased = 0;
            gearPacksPurchased = 0;
            Task cameraTask = CampaignManager.Instance.MapCamera.EnterShopScene();

#if WINTER_UPDATE
                specialBartenderAnimator.CrossFade("Welcome", 0.1f);
#else
                bartenderAnimator.CrossFade("Welcome", 0.1f);
#endif
            shopSceneActive = true;

            await Task.Delay(800);
            OpenFeedback.PlayFeedbacks();
            IAudioRequester.Instance.PlaySFX(SFXData.MerchantGreeting);

            shopCanvasGroup.FadeInAsync();

            async Task SpawnInItems()
            {
                int gearPackDiscount = 0;
                if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_gearPackCostMetaprogressionModel))
                {
                    gearPackDiscount  = _gearPackCostMetaprogressionModel.NodeValue;
                }
                gearPack.gameObject.SetActive(true);
                gearPack.SetUp(CardPackDataInfo.GearPack, this, gearPackDiscount);
                await Task.Delay(100);

                int pack1Discount = 0;
                if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_pack1CostMetaprogressionModel))
                {
                    pack1Discount  = _pack1CostMetaprogressionModel.NodeValue;
                }
                cardPack1.gameObject.SetActive(true);
                cardPack1.SetUp(CardPackDataInfo.CardPack1, this, pack1Discount);
                await Task.Delay(100);

                int pack2Discount = 0;
                if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_pack2CostMetaprogressionModel))
                {
                    pack2Discount  = _pack2CostMetaprogressionModel.NodeValue;
                }
                cardPack2.gameObject.SetActive(true);
                cardPack2.SetUp(CardPackDataInfo.CardPack2, this, pack2Discount);
                await Task.Delay(100);

                int pack3Discount = 0;
                if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_pack3CostMetaprogressionModel))
                {
                    pack3Discount  = _pack3CostMetaprogressionModel.NodeValue;
                }
                cardPack3.gameObject.SetActive(true);
                cardPack3.SetUp(CardPackDataInfo.CardPack3, this, pack3Discount);
                await Task.Delay(100);

                int pack4Discount = 0;
                cardPack4.gameObject.SetActive(true);
                cardPack4.SetUp(CardPackDataInfo.CardPack4, this, pack4Discount);
                await Task.Delay(100);

                int consumableDiscount = 0;
                if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_consumableDiscountMetaprogressionModel))
                {
                    consumableDiscount  = _consumableDiscountMetaprogressionModel.NodeValue;
                }

                shopConsumables.Clear();
                for (int i = 0; i < 2; i++)
                {
                    ConsumableEnum consumable = ConsumableData.GetRandomConsumable();
                    while (shopSaveData.shopConsumables.Contains(consumable))
                    {
                        consumable = ConsumableData.GetRandomConsumable();
                    }
                    shopSaveData.shopConsumables.Add(consumable);

                    ShopConsumable consumableUI = Instantiate(consumablePrefab, i == 0 ? consumableTransform1 : consumableTransform2);
                    Consumable consumableData = ConsumableData.GetConsumable(consumable);
                    consumableUI.SetUp(consumable, ConsumableData.ConsumableCost(consumableData.ConsumableRarity) - consumableDiscount, this);
                    shopConsumables.Add(consumableUI);
                    await Task.Delay(100);
                }
                outlines = GetComponentsInChildren<Outline>();
            }

            shopSaveData = new ShopSaveData()
            {
                shopSeed = campaignSaveManager.GetSeededRandom(),
                packPurchasedCount = 0,
                shopConsumables = new List<ConsumableEnum>(),
                shopConsumablesPurchased = new List<ConsumableEnum>()
            };

            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);

            
            
            await Task.WhenAll(cameraTask, SpawnInItems());

            RenableShopPanel();
        }
        public void Update()
        {
            if (
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() ||
                SettingsManager.Instance.SettingsPanelOpen ||
                !shopSceneActive ||
                freeCameraMode
            )
            {
                if (hoveredConsumable != null)
                {
                    hoveredConsumable.HoverConsumable(false);
                    hoveredConsumable = null;
                }
                if (hoveredCardPack != null)
                {
                    hoveredCardPack.HoverPack(false);
                    hoveredCardPack = null;
                }
                return;
            }

            //if mouse hits node, hover it
            Ray ray = CampaignManager.Instance.MapCamera.ShopCamera.ScreenPointToRay(InputHandler.Instance.MousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.transform.GetComponent<CardPack>())
                {
                    CardPack hoveredPack = hit.transform.GetComponent<CardPack>();
                    if (hoveredCardPack != null && hoveredCardPack != hoveredPack)
                    {
                        hoveredCardPack.HoverPack(false);
                    }
                    if (hoveredCardPack == hoveredPack)
                    {
                        return;
                    }
                    hoveredCardPack = hoveredPack;
                    hoveredCardPack.HoverPack(true);
                }
                else
                {
                    if (hoveredCardPack != null)
                    {
                        hoveredCardPack.HoverPack(false);
                        hoveredCardPack = null;
                    }
                }

                //check tag of hit for consumable
                if (hit.transform.CompareTag("ShopConsumable"))
                {
                    ShopConsumable hoveredItem = hit.transform.GetComponentInParent<ShopConsumable>();
                    if (hoveredConsumable != null && hoveredConsumable != hoveredItem)
                    {
                        hoveredConsumable.HoverConsumable(false);
                    }
                    if (hoveredConsumable == hoveredItem)
                    {
                        return;
                    }
                    hoveredConsumable = hoveredItem;
                    hoveredConsumable.HoverConsumable(true);
                }
                else
                {
                    if (hoveredConsumable != null)
                    {
                        hoveredConsumable.HoverConsumable(false);
                        hoveredConsumable = null;
                    }
                }
            }
            else
            {
                if (hoveredCardPack != null)
                {
                    hoveredCardPack.HoverPack(false);
                    hoveredCardPack = null;
                }
                if (hoveredConsumable != null)
                {
                    hoveredConsumable.HoverConsumable(false);
                    hoveredConsumable = null;
                }
            }
        }
        public void LeftClick()
        {
            if (!shopSceneActive) return;

            if (hoveredCardPack != null)
            {
                DisableShopPanel();
                hoveredCardPack.AttemptPurchase();
            }

            if (hoveredConsumable != null)
            {
                DisableShopPanel();
                hoveredConsumable.AttemptPurchase();
            }
        }
        public void OnPurchaseComplete()
        {
            RenableShopPanel();
        }
        public void CompleteShopInteraction()
        {
            mapSceneUIManager.CompleteLayerAction();
        }
        public override async void ClosePanel()
        {
            CloseFeedback();
            Debug.Log("[Map] Closing ShopPanel");
            DisableShopPanel();
            campaignSaveManager.ClearSoldGear();
            IAudioRequester.Instance.PlaySFX(SFXData.Continue);
            IAudioRequester.Instance.PlaySFX(SFXData.MerchantGoodbye);
            SceneHandler.Instance.TranstionCameras(
                CampaignManager.Instance.MapCamera.ShopCamera,
                CampaignManager.Instance.MapCamera.MapCameraInstance
            );
            await Task.Delay(500);

            for (int i = 0; i < shopConsumables.Count; i++)
            {
                if (shopConsumables[i] == null) continue;
                Destroy(shopConsumables[i].gameObject);
            }
            shopConsumables.Clear();
            gearPack.gameObject.SetActive(false);
            cardPack1.gameObject.SetActive(false);
            cardPack2.gameObject.SetActive(false);
            cardPack3.gameObject.SetActive(false);
            cardPack4.gameObject.SetActive(false);

            shopCanvasGroup.CGDisable();
        }
        public void OnDestroy()
        {
            if (InputHandler.HasInstance)
                InputHandler.Instance.PrimaryActionPerformed -= LeftClick;
        }
        public void DisableShopPanel()
        {
            shopSceneActive = false;
            closeButton.gameObject.SetActive(false);
            EnableOutlines(false);
            coinDispenser.ClearCoinsOnShopLeave();
        }
        public void RenableShopPanel()
        {
            shopSceneActive = true;
            closeButton.gameObject.SetActive(true);
            TooltipManager.Instance.HideTooltip();
            EnableOutlines(true);
        }
        private void EnableOutlines(bool _enable)
        {
            for (int i = 0; i < outlines.Length; i++)
            {
                if (outlines[i] == null) continue;
                outlines[i].enabled = _enable;
            }
        }
        public async void PurchasePack(CardPackData _cardPackData, int _cost)
        {
            string localizedString = LocalizationManager.Instance.GetText("Shop");
            CampaignManager.Instance.GoldManager.ModifyGold(-_cost, localizedString);
            IAudioRequester.Instance.PlaySFX(SFXData.Purchase);
            
            coinDispenser.DispenseCoin(_cost);

            if (_cardPackData.packID == 0)
            {
                List<GearID> gearList = campaignSaveManager.DrawRandomGear(1, true);
                campaignSaveManager.SaveRecruitableGear(gearList.ToArray());
                campaignSaveManager.IncrementRerollCount(1);
                if (gearList.Count == 0) return;
                treasurePanel.LoadTreasurePanelFromShop(gearList[0]);
            }
            else
            {
                recruitPanel.LoadRecruitPanelFromShop(_cardPackData);
            }

            if(_cardPackData.packID == 4)
            {
                TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.SignatureUnitPacks });
                campaignSaveManager.IncrementSignatureUnitPackPurchaseCount();
                cardPack4.RefreshPrice();
            }

            SteamAchievements.AddStat(SteamStatId.ShopPurchases, 1);
            campaignSaveManager.RegisterShopPurchase();
            EnableOutlines(false);
            if (_cardPackData.packID == 0)
            {
                gearPacksPurchased++;
                gearPack.RefreshPrice();
            }
        }
        public void ConsumablePurchased()
        {
            consumablesPurchased++;
            for (int i = 0; i < shopConsumables.Count; i++)
            {
                shopConsumables[i].RefreshPrices();
            }
        }
        public void OnGearChanged()
        {
            cardPack1.RefreshPrice();
            cardPack2.RefreshPrice();
            cardPack3.RefreshPrice();
            gearPack.RefreshPrice();
        }
    }
}