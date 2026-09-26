using TJ.Map;
using TJ;
using UnityEngine;
using Memori.Utilities;
using UnityEngine.UI;
using Memori.Audio;
using Memori.UI;
using MoreMountains.Feedbacks;
using System.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using Memori.Localization;
using Memori.Notifications;
using Memori.SaveData;

namespace TJ.Treasure
{
    public class TreasurePanel : MapPanel
    {
        [SerializeField] private GameObject _chestScene;
        [SerializeField] private GameObject _itemIconHolder;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button openChestButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button claimGearButton;

        [Header("Gear Info")]
        [SerializeField] private Image gearImage;
        [SerializeField] private TMP_Text gearNameText, gearRarityText, gearDescriptionText;
        [SerializeField] private GameObject newNotificationActive;
        [SerializeField] private MemoriCanvasGroup gearInfoCanvasGroup;

        [Header("Map Node Treasure")]
        [SerializeField] private MemoriCanvasGroup gearRewardCanvasGroup;
        [SerializeField] private Transform gearCardTreasureParent;
        [SerializeField] private GearCardTreasurePanel gearCardTreasurePrefab;
        [SerializeField] private GearCardTreasurePanel consumableCardTreasurePrefab;
        [SerializeField] private GameObject separatorPrefab;
        private GearCardTreasurePanel[] gearCardHordeRewards;
        private GearCardTreasurePanel _consumableCardReward;
        private GameObject _separator;
        private TMP_Text _separatorText;
        [SerializeField] private GameObject gearFullWarning;
        [SerializeField] private MemoriButtonV2 skipGearButton;

        [Header("Shop Rewards")]
        [SerializeField] private MemoriCanvasGroup shopRewardCanvasGroup;

        [Header("Juice")]
        [SerializeField] private MMF_Player openMMFPlayer;
        [SerializeField] private Transform openSizeTransform;
        [SerializeField] private MMF_Player closeMMFPlayer;
        [SerializeField] private MMF_Player rollingCardsMMF;
        [SerializeField] private Animator animator;

        MemoriCanvasGroup memoriCanvasGroup;
        GearID gearItemEnum = new();
        Gear[] allGearList;
        bool continueToSpin = true;

        CampaignSaveManager campaignSaveManager;
        MapSceneUIManager mapSceneUIManager;

        private enum PanelLoadedFrom { Map, Shop };
        PanelLoadedFrom panelLoadedFrom;

        private void Awake()
        {
            memoriCanvasGroup = GetComponent<MemoriCanvasGroup>();
            closeButton.onClick.RemoveAllListeners();
            skipButton.onClick.RemoveAllListeners();
            openChestButton.onClick.RemoveAllListeners();
            claimGearButton.onClick.RemoveAllListeners();

            skipButton.onClick.AddListener(Continue);
            closeButton.onClick.AddListener(Continue);
            openChestButton.onClick.AddListener(LootGearButtonClicked);
            claimGearButton.onClick.AddListener(ClaimGearButtonClicked);

            allGearList = GearData.GetAllGear();
            _chestScene.SetActive(false);
            gearRewardCanvasGroup.CGDisable();
            shopRewardCanvasGroup.CGDisable();
            skipGearButton.gameObject.SetActive(false);
        }
        public void SetUp(CampaignSaveManager _campaignSaveManager, MapSceneUIManager _mapSceneUIManager)
        {
            campaignSaveManager = _campaignSaveManager;
            mapSceneUIManager = _mapSceneUIManager;
        }
        public void LoadTreasurePanelFromMapNode(int count = 3, bool loadConsumable = false)
        {
            panelLoadedFrom = PanelLoadedFrom.Map;

            openSizeTransform.localScale = Vector3.zero;
            memoriCanvasGroup.CGEnable();
            openMMFPlayer.PlayFeedbacks();
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);

            OpenFeedback.PlayFeedbacks();
            // await Task.Delay(500);

            gearCardHordeRewards = new GearCardTreasurePanel[count];
            List<GearID> gearList = campaignSaveManager.DrawRandomGear(count);
            gearFullWarning.SetActive(!campaignSaveManager.CanAquireGear());

            for (int i = 0; i < gearCardHordeRewards.Length; i++)
            {
                gearCardHordeRewards[i] = Instantiate(gearCardTreasurePrefab, gearCardTreasureParent);
                gearCardHordeRewards[i].LoadGearCardReward(gearList[i]);
                gearCardHordeRewards[i].OnGearCardSelected += SelectGearReward;
            }

            if (loadConsumable)
            {
                _separator = Instantiate(separatorPrefab, gearCardTreasureParent);
                _separatorText = _separator.GetComponentInChildren<TMP_Text>();
                string separatorLocalized = "~ " + LocalizationManager.Instance.GetText("orConsumable") + " ~";
                _separatorText.text = separatorLocalized;

                int bookNumber = campaignSaveManager.SaveData.bookNumber;
                ConsumableEnum randomConsumable = ConsumableData.GetWeightedConsumable(bookNumber, campaignSaveManager.GetSeededRandom() + count);
                _consumableCardReward = Instantiate(consumableCardTreasurePrefab, gearCardTreasureParent);
                _consumableCardReward.LoadConsumableCardReward(randomConsumable);
                _consumableCardReward.OnConsumableCardSelected += SelectConsumableReward;
            }

            skipGearButton.Button.onClick.RemoveAllListeners();
            skipGearButton.Button.onClick.AddListener(Continue);
            skipGearButton.gameObject.SetActive(true);

            gearRewardCanvasGroup.FadeInAsync(0.25f);
        }
        private void SelectGearReward(GearID gearID)
        {
            if (!campaignSaveManager.CanAquireGear())
            {
                string errorLocalized = LocalizationManager.Instance.GetText("No space for gear");
                NotificationManager.Instance.ErrorNotification(errorLocalized);
                return;
            }

            for (int i = 0; i < gearCardHordeRewards.Length; i++)
            {
                gearCardHordeRewards[i].OnGearCardSelected -= SelectGearReward;
                gearCardHordeRewards[i].NotifyOfSelection(gearID);
            }

            if (_consumableCardReward != null)
            {
                _consumableCardReward.OnConsumableCardSelected -= SelectConsumableReward;
                _consumableCardReward.DarkenCard();
            }

            campaignSaveManager.AquireGear(gearID);
            skipGearButton.gameObject.SetActive(false);
            CloseGearPanel();
        }
        private void SelectConsumableReward(ConsumableEnum consumableEnum)
        {
            if (!campaignSaveManager.HasRoomForConsumable())
            {
                string errorLocalized = LocalizationManager.Instance.GetText("NoRoomForConsumable");
                NotificationManager.Instance.ErrorNotification(errorLocalized);
                return;
            }

            for (int i = 0; i < gearCardHordeRewards.Length; i++)
            {
                gearCardHordeRewards[i].OnGearCardSelected -= SelectGearReward;
                gearCardHordeRewards[i].DarkenCard();
            }

            _consumableCardReward.OnConsumableCardSelected -= SelectConsumableReward;
            _consumableCardReward.PlayPurchaseFeedbacks();

            campaignSaveManager.AquireConsumable(consumableEnum);
            skipGearButton.gameObject.SetActive(false);
            CloseGearPanel();
        }
        private async void CloseGearPanel()
        {
            gearRewardCanvasGroup.FadeOutAsync(0.25f);
            await Task.Delay(250);
            Continue();
        }
        public void LoadTreasurePanelFromShop(GearID _gearItemEnum)
        {
            panelLoadedFrom = PanelLoadedFrom.Shop;
            gearItemEnum = _gearItemEnum;
            shopRewardCanvasGroup.CGEnable();
            LoadTreasurePanel();
        }
        private async void LoadTreasurePanel()
        {
            newNotificationActive.SetActive(false);
            _itemIconHolder.SetActive(false);
            _chestScene.SetActive(true);
            gearNameText.text = "";
            gearRarityText.text = "";
            gearDescriptionText.text = "";

            skipButton.gameObject.SetActive(false);
            closeButton.gameObject.SetActive(false);
            claimGearButton.gameObject.SetActive(false);
            openChestButton.gameObject.SetActive(false);

            openSizeTransform.localScale = Vector3.zero;
            memoriCanvasGroup.CGEnable();
            openMMFPlayer.PlayFeedbacks();
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
            gearInfoCanvasGroup.CGDisable();

            animator.speed = 0;
            await Task.Delay(500);
            animator.speed = 1;
            openChestButton.gameObject.SetActive(true);
            skipButton.gameObject.SetActive(true);
        }
        public void LootGearButtonClicked()
        {
            openChestButton.gameObject.SetActive(false);
            Debug.Log($"Loot gear button clicked, loading card panel with {string.Join(", ", gearItemEnum)}");
            animator.SetBool("OpenChest", true);
            closeButton.gameObject.SetActive(false);
        }
        public void Continue()
        {
            shopRewardCanvasGroup.CGDisable();
            gearRewardCanvasGroup.CGDisable();

            if (panelLoadedFrom == PanelLoadedFrom.Map)
            {
                ResetCards();
                mapSceneUIManager.CompleteLayerAction();
            }
            else if (panelLoadedFrom == PanelLoadedFrom.Shop)
            {
                mapSceneUIManager.ShopPanel.RenableShopPanel();
                ClosePanel();
            }
        }
        public void Skip()
        {
            closeButton.gameObject.SetActive(true);
        }
        public void OnCardSelected()
        {
            openChestButton.gameObject.SetActive(false);
            closeButton.gameObject.SetActive(true);
        }
        public void ChestOpen()
        {
            ChestOpeningUIElement();
        }
        public async void ChestOpeningUIElement()
        {
            bool skipChestAnimations = PlayerPrefs.GetInt("skipChestAnimations", 0) == 1;
            if (skipChestAnimations)
            {
                rollingCardsMMF.DurationMultiplier = 0.1f;
                rollingCardsMMF.PlayFeedbacks();
                CompleteSpin();
                return;
            }

            rollingCardsMMF.DurationMultiplier = 1f;
            IAudioRequester.Instance.PlaySFX(SFXData.ChestBeginOpen);
            await Task.Delay(250);

            async Task RollGearCards()
            {
                rollingCardsMMF.PlayFeedbacks();
                continueToSpin = true;
                void RollCard()
                {
                    //get random gear from the list
                    Gear randomGear = allGearList[Random.Range(0, allGearList.Length)];

                    //get the icon
                    gearImage.sprite = SpriteData.GetSprite(randomGear.GearName);
                }

                while (continueToSpin)
                {
                    RollCard();
                    await Task.Delay(50);
                }
            }

            await RollGearCards();

            skipButton.gameObject.SetActive(true);
        }
        public void CompleteSpin()
        {
            continueToSpin = false;
            animator.SetBool("FinalJuice", true);
            gearImage.sprite = SpriteData.GetSprite(GearData.GetGear(gearItemEnum).GearName);
            Gear gearItem = GearData.GetGear(gearItemEnum);
            gearNameText.text = LocalizationManager.Instance.GetText(gearItemEnum + "Name");
            gearRarityText.text = LocalizationManager.Instance.GetText(gearItem.GearRarity.ToString());
            gearRarityText.color = ColorData.GetGearRarityColor(gearItem.GearRarity);
            string descriptionLocalized = LocalizationManager.Instance.GetText(gearItemEnum + "Desc");
            descriptionLocalized = string.Format(descriptionLocalized, gearItem.GearModifierValue);

            KeywordText.Apply(gearDescriptionText, descriptionLocalized);
            IAudioRequester.Instance.PlaySFX(SFXData.ChestOpen);
            claimGearButton.gameObject.SetActive(true);
            gearInfoCanvasGroup.FadeInAsync(1);

            bool isNew = !SaveDataHandler.GetGearIDsCollected().Contains((int)gearItemEnum);
            if (newNotificationActive != null) newNotificationActive.SetActive(isNew);
        }
        public void ClaimGearButtonClicked()
        {
            if (!CampaignManager.Instance.CampaignSaveManager.CanAquireGear())
            {
                string errorLocalized = LocalizationManager.Instance.GetText("No space for gear");
                NotificationManager.Instance.ErrorNotification(errorLocalized);
                return;
            }

            IAudioRequester.Instance.PlaySFX(SFXData.SelectCard);
            campaignSaveManager.AquireGear(gearItemEnum);
            Continue();
        }
        private void ResetCards()
        {
            for (int i = 0; i < gearCardHordeRewards.Length; i++)
            {
                if (gearCardHordeRewards[i] != null)
                {
                    gearCardHordeRewards[i].OnGearCardSelected -= SelectGearReward;
                    Destroy(gearCardHordeRewards[i].gameObject);
                    gearCardHordeRewards[i] = null;
                }
            }

            if (_separator != null)
            {
                Destroy(_separator);
                _separator = null;
            }

            if (_consumableCardReward != null)
            {
                _consumableCardReward.OnConsumableCardSelected -= SelectConsumableReward;
                Destroy(_consumableCardReward.gameObject);
                _consumableCardReward = null;
            }
        }
        public override void ClosePanel()
        {
            CloseFeedback();
            Debug.Log("[Map] Closing TreasurePanel");
            closeMMFPlayer.PlayFeedbacks();
            memoriCanvasGroup.FadeOutAsync();
            _chestScene.SetActive(false);
            animator.SetBool("OpenChest", false);
            animator.SetBool("FinalJuice", false);
        }
    }
}