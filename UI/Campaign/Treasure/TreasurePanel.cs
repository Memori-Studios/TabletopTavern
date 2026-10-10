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
        [SerializeField] private ChoicePanelView view;
        [SerializeField] private GameObject gearFullWarning;
        [SerializeField] private MemoriButtonV2 skipGearButton;
        // The item art overhangs the card's diamond mount a little, as the old rows did.
        const float ItemIconSize = 84f;
        const int PickedHoldMs = 600;
        bool rewardPicked;

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
        // This opening's offer and pick for the node log; sent by Continue, where every opening ends.
        Dictionary<string, object> treasureLog;
        // Town loot only: told once, when the panel closes, whether a gear item was taken.
        System.Action<bool> townLootClosed;
        bool gearTaken;

        private void Awake()
        {
            memoriCanvasGroup = GetComponent<MemoriCanvasGroup>();
            closeButton.ClearClickListeners();
            skipButton.ClearClickListeners();
            openChestButton.ClearClickListeners();
            claimGearButton.ClearClickListeners();

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

            townLootClosed = null;
            ShowGearChoices(campaignSaveManager.DrawRandomGear(count), loadConsumable, count);
        }
        // A sacked town's loot. One item opens the chest; more than one is a pick, as on a Treasure node.
        public void LoadTreasurePanelFromTown(List<GearID> gearList, System.Action<bool> onClosed)
        {
            if (gearList.Count == 1)
            {
                LoadTreasurePanelFromShop(gearList[0]);
            }
            else
            {
                panelLoadedFrom = PanelLoadedFrom.Shop;

                openSizeTransform.localScale = Vector3.zero;
                memoriCanvasGroup.CGEnable();
                openMMFPlayer.PlayFeedbacks();
                IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);

                ShowGearChoices(gearList, false, 0);
            }
            townLootClosed = onClosed;
        }
        private void ShowGearChoices(List<GearID> gearList, bool loadConsumable, int consumableSeedOffset)
        {
            gearTaken = false;
            bool gearFull = !campaignSaveManager.CanAquireGear();
            TabletopTavern.Analytics.NodeLog.Try("treasure offer", () => BeginTreasureLog(gearList, gearFull));
            // The count line shows a full bag in red now; the banner's old warning icon would sit on top of it.
            gearFullWarning.SetActive(false);
            rewardPicked = false;

            view.Clear();
            string takeLabel = Text("choiceClickToTake");
            string takenLabel = Text("choiceTaken");
            for (int i = 0; i < gearList.Count; i++)
            {
                GearID gearID = gearList[i];
                Gear gear = GearData.GetGear(gearID);
                ChoiceCardView card = view.AddCard();
                string description = string.Format(Text(gearID + "Desc"), gear.GearModifierValue);
                card.Load(SpriteData.GetSprite(gear.GearName), Text(gearID + "Name"), Text(gear.GearRarity.ToString()),
                    ColorData.GetGearRarityColor(gear.GearRarity), description, takeLabel, takenLabel);
                card.ShowArtIcon(ItemIconSize);
                card.SetNew(!SaveDataHandler.GetGearIDsCollected().Contains((int)gearID));
                if (gearFull) card.SetBlocked(Text("NoRoomForGear"));
                card.Chosen += chosen => SelectGearReward(gearID, chosen);
            }

            view.ShowWide(loadConsumable);
            if (loadConsumable)
            {
                int bookNumber = campaignSaveManager.SaveData.bookNumber;
                ConsumableEnum randomConsumable = ConsumableData.GetWeightedConsumable(bookNumber, campaignSaveManager.GetSeededRandom() + consumableSeedOffset);
                if (treasureLog != null) treasureLog["cons"] = randomConsumable.ToString();
                ChoiceCardView card = view.AddWideCard();
                string description = CampaignManager.Instance.ConsumableManager.GetConsumableDescription(randomConsumable);
                card.Load(SpriteData.GetSprite(randomConsumable.ToString()), Text(randomConsumable + "Name"), null, Color.clear,
                    description, takeLabel, takenLabel);
                card.ShowArtIcon(ItemIconSize * 0.8f);
                if (!campaignSaveManager.HasRoomForConsumable()) card.SetBlocked(Text("NoRoomForConsumable"));
                card.Chosen += chosen => SelectConsumableReward(randomConsumable, chosen);
            }

            view.SetCounts(CountLine());
            view.WireNavigation();
            view.PlayOpen();
            view.FocusFirstCard();

            skipGearButton.Button.ClearClickListeners();
            skipGearButton.Button.onClick.AddListener(Continue);
            skipGearButton.gameObject.SetActive(true);

            gearRewardCanvasGroup.FadeInAsync(0.25f);
        }
        private static string Text(string key) => LocalizationManager.Instance.GetText(key);
        // "Gear 1 / 5 · Consumables 1 / 3", a full count in the bad colour so a full bag shows before the click.
        private string CountLine()
        {
            CampaignSaveData save = campaignSaveManager.SaveData;
            string Count(int held, int max)
            {
                string colour = held >= max ? ColorData.Negative : "#ECE6D8";
                return $"<color={colour}>{held} / {max}</color>";
            }
            return $"{Text("treasureGear")}  {Count(save.Gear.Count, campaignSaveManager.MaxGear)}   <color=#6C777B>·</color>   "
                + $"{Text("treasureConsumables")}  {Count(save.consumables.Count, campaignSaveManager.ConsumableCapacity)}";
        }
        private void SelectGearReward(GearID gearID, ChoiceCardView chosen)
        {
            if (rewardPicked) return;
            if (!campaignSaveManager.CanAquireGear())
            {
                NotificationManager.Instance.ErrorNotification(Text("No space for gear"));
                if (chosen != null) chosen.Deny();
                return;
            }

            rewardPicked = true;
            gearTaken = true;
            if (treasureLog != null) treasureLog["pick"] = gearID.ToString();
            campaignSaveManager.AquireGear(gearID);
            ShowPicked(chosen);
        }
        private void SelectConsumableReward(ConsumableEnum consumableEnum, ChoiceCardView chosen)
        {
            if (rewardPicked) return;
            if (!campaignSaveManager.HasRoomForConsumable())
            {
                NotificationManager.Instance.ErrorNotification(Text("NoRoomForConsumable"));
                if (chosen != null) chosen.Deny();
                return;
            }

            rewardPicked = true;
            if (treasureLog != null) treasureLog["pick"] = consumableEnum.ToString();
            campaignSaveManager.AquireConsumable(consumableEnum);
            ShowPicked(chosen);
        }
        // Saved already; the others pop away, the chosen card turns gold and holds, then the panel fades.
        private async void ShowPicked(ChoiceCardView chosen)
        {
            skipGearButton.gameObject.SetActive(false);
            bool firstPop = true;
            foreach (ChoiceCardView card in view.Cards)
            {
                if (card == chosen) continue;
                card.PopAway(firstPop);
                firstPop = false;
            }
            if (view.WideCard != null && view.WideCard != chosen) view.WideCard.PopAway(firstPop);
            chosen.ShowPicked();
            await Task.Delay(PickedHoldMs);
            if (this == null) return;
            CloseGearPanel();
        }
        private async void CloseGearPanel()
        {
            gearRewardCanvasGroup.FadeOutAsync(0.25f);
            await Task.Delay(250);
            if (this == null) return;
            Continue();
        }
        public void LoadTreasurePanelFromShop(GearID _gearItemEnum)
        {
            panelLoadedFrom = PanelLoadedFrom.Shop;
            townLootClosed = null;
            gearTaken = false;
            gearItemEnum = _gearItemEnum;
            TabletopTavern.Analytics.NodeLog.Try("treasure offer", () => BeginTreasureLog(new List<GearID> { _gearItemEnum }, !campaignSaveManager.CanAquireGear()));
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
        private void BeginTreasureLog(List<GearID> gearList, bool gearFull)
        {
            var gear = new List<string>();
            foreach (GearID id in gearList) gear.Add(id.ToString());
            treasureLog = new Dictionary<string, object>
            {
                { "from", panelLoadedFrom.ToString() },
                { "gear", gear },
                { "cons", null },
                { "barFull", gearFull },
                { "pick", "skip" },
            };
        }
        public void Continue()
        {
            if (treasureLog != null)
            {
                Dictionary<string, object> entry = treasureLog;
                TabletopTavern.Analytics.NodeLog.Try("treasure pick", () => TabletopTavern.Analytics.NodeLog.Add("treasure", entry));
                treasureLog = null;
            }
            shopRewardCanvasGroup.CGDisable();
            gearRewardCanvasGroup.CGDisable();

            if (panelLoadedFrom == PanelLoadedFrom.Map)
            {
                ResetCards();
                mapSceneUIManager.CompleteLayerAction();
            }
            else if (panelLoadedFrom == PanelLoadedFrom.Shop)
            {
                ResetCards();
                skipGearButton.gameObject.SetActive(false);
                mapSceneUIManager.ShopPanel.RenableShopPanel();
                ClosePanel();
            }

            System.Action<bool> closed = townLootClosed;
            townLootClosed = null;
            closed?.Invoke(gearTaken);
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
            if (treasureLog != null) treasureLog["pick"] = gearItemEnum.ToString();
            gearTaken = true;
            campaignSaveManager.AquireGear(gearItemEnum);
            Continue();
        }
        private void ResetCards()
        {
            view.Clear();
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