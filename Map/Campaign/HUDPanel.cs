using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using Memori.Utilities;
using Memori.SaveData;
using Memori.Audio;
using TJ.Map;
using TJ.Settings;
using System;
using Memori.Tooltip;
using System.Collections;
using Memori.UI;
using Unity.Mathematics;
using MoreMountains.Feedbacks;
using Memori.Input;
using Memori.Localization;
using UnityEngine.InputSystem;
using Memori.Steamworks;

namespace TJ.Map
{
    public class HUDPanel : MonoBehaviour
    {
        [SerializeField] private Button showSettingsButton;
        [SerializeField] private MemoriTooltipTrigger settingsTooltipTrigger;
        [SerializeField] private Button freeCameraButton;
        [SerializeField] private MemoriTooltipTrigger freeCameraTooltipTrigger;
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private GameObject freeCameraOverlay;
        [SerializeField] private Button returnFromFreeCameraButton;
        [SerializeField] private MemoriTooltipTrigger returnFromFreeCameraTooltipTrigger;
        [SerializeField] private TMP_Text returnFromFreeCameraKeyText;

        [Header("Player Company")]
        [SerializeField] private MemoriTooltipTrigger chapterTooltipTrigger;

        [Header("Player Company")]
        [SerializeField] private SquadDisplayCardMenu squadDisplayCardMenuPrefab;
        [SerializeField] private Transform deployedUnitsParent, reserveUnitsParent;
        [SerializeField] private GameObject emptySquadDisplayCardMenuPrefab;

        [Header("Troops Areas")]
        [SerializeField] private RectTransform deployedTroopsArea;
        [SerializeField] private RectTransform reserveTroopsArea;
        [SerializeField] private RectTransform[] troopsIndexAreas;
        public RectTransform DeployedTroopsArea => deployedTroopsArea;
        public RectTransform ReserveTroopsArea => reserveTroopsArea;
        public RectTransform[] TroopsIndexAreas => troopsIndexAreas;
        [SerializeField] private MetaprogressionLockedButton thirdReserveSlotLockedButton;
        [SerializeField] private Image deployedTroopsAreaImage, reserveTroopsAreaImage;
        [SerializeField] private TMP_Text deployedTroopsCountText, reserveTroopsCountText;

        [Header("Popups")]
        [SerializeField] private CanvasGroup disbandSquadConfirmationPopup;
        [SerializeField] private Button disbandSquadButtonConfirm, disbandSquadButtonCancel;
        [SerializeField] private CanvasGroup renameSquadConfirmationPopup;
        [SerializeField] private Button renameSquadButtonConfirm, renameSquadButtonCancel;
        [SerializeField] private TMP_InputField renameSquadInputField;

        [Header("Weather Hover")]
        [SerializeField] private CanvasGroup weatherHoverPanel;
        [SerializeField] private TMP_Text weatherHoverTitle;
        [SerializeField] private TMP_Text weatherHoverDescription;

        [Header("Gold")]
        [SerializeField] private TMP_Text goldAmountText;
        [SerializeField] private MemoriTooltipTrigger goldTooltipTrigger;
        [SerializeField] private MMF_Player goldMMFeedback;

        [Header("Top Row")]
        [SerializeField] private TMP_Text chapterText;
        [SerializeField] private TMP_Text difficultyText, heroNameText, heroRaceText;
        // [SerializeField] private GameObject peasantIcon, squireIcon, knightIcon, baronIcon, dukeIcon, kingIcon, emperorIcon;
        [SerializeField] private MemoriTooltipTrigger difficultyTooltipTrigger, heroNameTooltipTrigger, heroRaceTooltipTrigger;
        [SerializeField] private MMF_Player chapterMMFeedback;

        [Header("Gear")]
        [SerializeField] private GearDisplay[] gearDisplays;

        [Header("Consumables")]
        [SerializeField] private ConsumableUI[] consumableUI;
        public ConsumableUI[] ConsumableUI => consumableUI;
        [SerializeField] private UILineDrawer uILineDrawer;
        public UILineDrawer UILineDrawer => uILineDrawer;
        [SerializeField] private GameObject consumablesBlocker;
        private MemoriTooltipTrigger consumablesBlockerTooltip;

        [Header("SquadBattleInfo")]
        [SerializeField] private SquadBattleInfo squadBattleInfo;

        [Header("Troop Panel for Squad Displays")]
        public Transform DeployedUnitsParent => deployedUnitsParent;
        public Transform ReserveUnitsParent => reserveUnitsParent;
        
        private int deployedTroopsCount, reserveTroopsCount;
        public int DeployedTroopsCount => deployedTroopsCount;
        public int ReserveTroopsCount => reserveTroopsCount;
        public int MaxReserveSlots => campaignSaveManager.MaxReserveSlots;

        [SerializeField] private Animator hudAnimator;
        public Animator HudAnimator => hudAnimator;

        [Header("Legend")]
        [SerializeField] private GameObject legendGO;
        public GameObject LegendGO => legendGO;
        [SerializeField] private MapLabel skirmishLabel;
        [SerializeField] private MapLabel eventLabel;
        [SerializeField] private MapLabel shopLabel;
        [SerializeField] private MapLabel townLabel;
        [SerializeField] private MapLabel treasureLabel;
        [SerializeField] private MapLabel unknownLabel;
        [SerializeField] private MapLabel tavernLabel;
        [SerializeField] private MapLabel campfireLabel;
        // public MapLabel SkirmishLabel => skirmishLabel;
        // public MapLabel EventLabel => eventLabel;
        // public MapLabel ShopLabel => shopLabel;
        // public MapLabel TownLabel => townLabel;
        // public MapLabel TreasureLabel => treasureLabel;
        // public MapLabel UnknownLabel => unknownLabel;

        // [Header("Testing")]
        // [SerializeField] private Button testAquireConsumableButton;

        CampaignSaveManager campaignSaveManager;
        MapSceneUIManager mapSceneUIManager;
        List<Canvas> hudChildCanvases = new();
        bool isFreeCameraMode = false;
        List<SquadDisplayCardMenu> playerSquadsCards;
        public List<SquadDisplayCardMenu> PlayerSquadsCards => playerSquadsCards;
        List<string> pendingDisbandGuids = new();
        string renameSquadGUID;
        Coroutine rollGoldCoroutine;
        List<SquadDisplayCardMenu> selectedCards = new();
        public IReadOnlyList<SquadDisplayCardMenu> SelectedCards => selectedCards;
        List<GameObject> emptySquadCards = new();
        int hoveredSquadIndex;
        public int HoveredSquadIndex => hoveredSquadIndex;

        public void SetUp(CampaignSaveManager _campaignSaveManager, MapSceneUIManager _mapSceneUIManager)
        {
            campaignSaveManager = _campaignSaveManager;
            mapSceneUIManager = _mapSceneUIManager;
            showSettingsButton.onClick.AddListener(() => SettingsManager.Instance.OpenSettingsPanel());
            freeCameraButton.onClick.AddListener(EnterFreeCameraMode);
            returnFromFreeCameraButton.onClick.AddListener(ExitFreeCameraMode);

            disbandSquadButtonConfirm.onClick.AddListener(() => DisbandPendingSquads());
            disbandSquadButtonCancel.onClick.AddListener(() => HideDisbandSquadConfirmation());
            renameSquadButtonConfirm.onClick.AddListener(() => RenameSquad());
            renameSquadButtonCancel.onClick.AddListener(() => { renameSquadConfirmationPopup.CGDisable(); mapSceneUIManager.MapSceneManager.SetMapInput(true); });
            // testAquireConsumableButton.onClick.AddListener(() => CampaignManager.Instance.CampaignSaveManager.AquireConsumable(ConsumableData.GetRandomConsumable()));

            campaignSaveManager.OnChapterCompleted += UpdateChapterText;
            CampaignManager.Instance.GoldManager.OnGoldAmountChanged += OnGoldChanged;
            campaignSaveManager.OnUnitHealthChanged += ArmyHealthChanged;
            campaignSaveManager.OnGearChanged += ReloadGear;
            campaignSaveManager.OnArmyStructureChanged += ArmyStructureChanged;
            campaignSaveManager.OnConsumablesChanged += ReloadConsumables;
            InputHandler.Instance.SecondaryActionPressed += CloseAllPopUps;
            InputHandler.Instance.OnToggleFreeCameraMode += ToggleFreeCameraMode;

            ReloadGear();
            ReloadConsumables();
            ArmyStructureChanged();
            DeselectAllCards();

            skirmishLabel.SetUp(NodeType.Skirmish);
            eventLabel.SetUp(NodeType.Event);
            shopLabel.SetUp(NodeType.Shop);
            townLabel.SetUp(NodeType.Town);
            treasureLabel.SetUp(NodeType.Treasure);
            unknownLabel.SetUp(NodeType.Skirmish, true);
            if (tavernLabel != null) tavernLabel.SetUp(NodeType.Games);
            if (campfireLabel != null) campfireLabel.SetUp(NodeType.Campfire);
            SetUpDifficultyTooltip();
            UpdateHeroNameAndRace();
            legendGO.SetActive(true);

            chapterText.text = $"{campaignSaveManager.SaveData.bookNumber} - {campaignSaveManager.SaveData.activeMapLayer + 1}";
            chapterTooltipTrigger.SetUpToolTip(_title: GetChapterTooltipTitle(campaignSaveManager.SaveData.bookNumber, campaignSaveManager.SaveData.activeMapLayer));

            settingsTooltipTrigger.SetUpToolTip(_title: LocalizationManager.Instance.GetText("Settings"));
            string freeCamKey = InputControlPath.ToHumanReadableString(
                InputHandler.Instance.GameControls.Battle.ToggleFreeCameraMode.bindings[0].effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
            freeCameraTooltipTrigger.SetUpToolTip(_title: $"{LocalizationManager.Instance.GetText("FreeCameraMode")} [{freeCamKey}]");
            returnFromFreeCameraTooltipTrigger.SetUpToolTip(_title: $"{LocalizationManager.Instance.GetText("exitButton")} {LocalizationManager.Instance.GetText("FreeCameraMode")} [{freeCamKey}]");
            returnFromFreeCameraKeyText.text = $"{LocalizationManager.Instance.GetText("exitButton")} {LocalizationManager.Instance.GetText("FreeCameraMode")} - [{freeCamKey}]";
            consumablesBlocker.SetActive(false);
            freeCameraOverlay.SetActive(false);
        }
        private void SetUpDifficultyTooltip()
        {
            TT_Difficulty difficulty = CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel;
            DifficultyLevel difficultyData = DifficultyData.GetDifficultyLevelData(difficulty);

            string difficultyLocalized = LocalizationManager.Instance.GetText(difficultyData.difficultyName);
            difficultyText.text = difficultyLocalized;
            // peasantIcon.SetActive(false);
            // squireIcon.SetActive(false);
            // knightIcon.SetActive(false);
            // baronIcon.SetActive(false);
            // dukeIcon.SetActive(false);
            // kingIcon.SetActive(false);
            // emperorIcon.SetActive(false);

            string additionalModifiersDesc = "";
            List<string> allPreviousModifiers = DifficultyData.GetAllDifficultyModifiersBeforeLevel(difficulty+1);

            foreach (string modifier in allPreviousModifiers)
            {
                additionalModifiersDesc += "- " + LocalizationManager.Instance.GetText(modifier) + "\n";
            }
            string difficultLevelTitleLocalized = LocalizationManager.Instance.GetText("Difficulty");
            difficultyTooltipTrigger.SetUpToolTip(_title: $"{difficultLevelTitleLocalized}: {difficultyLocalized}", _description: additionalModifiersDesc);
        }
        private void UpdateHeroNameAndRace()
        {
            Hero hero = HeroData.GetHeroByID(campaignSaveManager.SaveData.heroID);
            string heroRace = HeroData.GetRaceFromHero(CampaignManager.Instance.CampaignSaveManager.GetHeroID()).ToString();
            string heroRaceLocalized = LocalizationManager.Instance.GetText(heroRace);
            string heroNameLocalized = LocalizationManager.Instance.GetText(hero.HeroName);
            heroNameText.text = heroNameLocalized;
            heroRaceText.text = heroRaceLocalized;

            string heroBonusText1string = LocalizationManager.Instance.GetText(hero.HeroBonusDescription[0].Replace("heroBonusDescription", "heroBonusTitle")) + ": " + LocalizationManager.Instance.GetText(hero.HeroBonusDescription[0]);
            string heroBonusText2string = LocalizationManager.Instance.GetText(hero.HeroBonusDescription[1].Replace("heroBonusDescription", "heroBonusTitle")) + ": " + LocalizationManager.Instance.GetText(hero.HeroBonusDescription[1]);
            string raceBonusTextstring = LocalizationManager.Instance.GetText(hero.Race+ "BonusDescription");
            ColorData.XMLTagColorApplicator(ref heroBonusText1string);
            ColorData.XMLTagColorApplicator(ref heroBonusText2string);
            ColorData.XMLTagColorApplicator(ref raceBonusTextstring);
            heroBonusText1string += "\n" + heroBonusText2string;
            heroNameTooltipTrigger.SetUpToolTip(_title: heroNameLocalized, _description: heroBonusText1string);
            heroRaceTooltipTrigger.SetUpToolTip(_title: heroRaceLocalized, _description: raceBonusTextstring);
        }
        public void ArmyStructureChanged()
        {
            // Debug.Log($"Army structure changed");
            RefreshTroopsPanel();
            CloseAllPopUps();
            squadBattleInfo.InvalidateSnapshotCache();
            squadBattleInfo.Unhover();
        }
        public void ArmyHealthChanged()
        {
            Debug.Log($"Army health changed");
            SquadToLoad[] playerSquadsSaveData = campaignSaveManager.SaveData.playerArmy;
            if(playerSquadsCards == null) return;
            for (int i = 0; i < playerSquadsCards.Count; i++)
            {
                //match squad card with save data by SquadId
                for (int j = 0; j < playerSquadsSaveData.Length; j++)
                {
                    if (playerSquadsCards[i].UniqueID == playerSquadsSaveData[j].UniqueID)
                    {
                        playerSquadsCards[i].UpdateUnitCount(playerSquadsSaveData[j]);
                        break;
                    }
                }
            }
        }
        private void RefreshTroopsPanel()
        {
            // Debug.Log($"Refreshing troops panel");
            playerSquadsCards = new List<SquadDisplayCardMenu>();
            foreach (Transform child in deployedUnitsParent) Destroy(child.gameObject);
            foreach (Transform child in reserveUnitsParent) Destroy(child.gameObject);
            emptySquadCards = new();

            SquadToLoad[] playerSquads = CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy;
            deployedTroopsCount = 0;
            reserveTroopsCount = 0;
            if(playerSquads == null) return;
            int maxArmySize = 10 + campaignSaveManager.MaxReserveSlots;
            for (int i = 0; i < playerSquads.Length && i < maxArmySize; i++)
            {
                bool isDeployed = i < 10;
                Transform unitParentTransform = isDeployed ? deployedUnitsParent : reserveUnitsParent;

                if (playerSquads[i].UnitIndex == -1)
                {
                    GameObject newEmptyCard = Instantiate(emptySquadDisplayCardMenuPrefab, unitParentTransform);
                    newEmptyCard.name = $"Empty Squad Card {i}";
                    emptySquadCards.Add(newEmptyCard);
                    continue;
                }
                // Debug.Log($"index {i} - loading squad {playerSquads[i].UnitName} (ID: {playerSquads[i].UnitIndex})");

                SquadDisplayCardMenu squadDisplayCardMenu = Instantiate(squadDisplayCardMenuPrefab, unitParentTransform);
                squadDisplayCardMenu.SetUp(playerSquads[i], !isDeployed, this);
                playerSquadsCards.Add(squadDisplayCardMenu);

                if (isDeployed) deployedTroopsCount++;
                else reserveTroopsCount++;
            }
            string deployedLocalized = LocalizationManager.Instance.GetText("Deployed");
            string reserveLocalized = LocalizationManager.Instance.GetText("Reserve");

            deployedTroopsCountText.text = $"{deployedLocalized} {deployedTroopsCount}/10";
            reserveTroopsCountText.text = $"{reserveLocalized} {reserveTroopsCount}/{campaignSaveManager.MaxReserveSlots}";
            deployedTroopsAreaImage.enabled = false;
            reserveTroopsAreaImage.enabled = false;
            if (thirdReserveSlotLockedButton != null) thirdReserveSlotLockedButton.CheckLockedState();

            if(deployedTroopsCount + reserveTroopsCount == 10 + campaignSaveManager.MaxReserveSlots) SteamAchievements.Unlock(AchievementId.FullArmy);
        }
        public void HoverSquad(SquadToLoad squad, bool _hovered, Transform _squadCardTransform)
        {
            Team team = Team.Player;
            if (mapSceneUIManager.EngagementPanel.EnemyArmyParent == _squadCardTransform.parent || mapSceneUIManager.TownPanel.GarrisonTroopTransform == _squadCardTransform.parent)
            {
                team = Team.Enemy;
            }

            if (_hovered)
            {
                squadBattleInfo.SetUpCampaign(squad, team);
                hoveredSquadIndex = squad.UnitIndex;
            }
            else if (selectedCards.Count > 0)
            {
                squadBattleInfo.SetUpCampaign(selectedCards[0].GetSquadToLoad(), selectedCards[0].CardTeam);
            }
            else
            {
                squadBattleInfo.Unhover();
                hoveredSquadIndex = -1;
            }
        }
        public void SelectSingleCard(SquadDisplayCardMenu card)
        {
            foreach (SquadDisplayCardMenu c in selectedCards)
                c.SelectSquad(false);
            foreach (SquadDisplayCardMenu c in playerSquadsCards)
                c.SetOptionsVisibility(false, false);
            selectedCards.Clear();

            if (card == null)
            {
                squadBattleInfo.Unhover();
                return;
            }

            selectedCards.Add(card);
            card.SelectSquad(true);
            UpdateSelectionOptions();
            squadBattleInfo.SetUpCampaign(card.GetSquadToLoad(), card.CardTeam);
        }
        public void ToggleCardInSelection(SquadDisplayCardMenu card)
        {
            if (selectedCards.Contains(card))
            {
                selectedCards.Remove(card);
                card.SelectSquad(false);
                card.SetOptionsVisibility(false, false);
            }
            else
            {
                selectedCards.Add(card);
                card.SelectSquad(true);
            }

            UpdateSelectionOptions();

            if (selectedCards.Count > 0)
                squadBattleInfo.SetUpCampaign(selectedCards[^1].GetSquadToLoad(), Team.Player);
            else
                squadBattleInfo.Unhover();
        }
        public void DeselectAllCards()
        {
            if (playerSquadsCards == null) return;
            foreach (SquadDisplayCardMenu c in selectedCards)
                c.SelectSquad(false);
            foreach (SquadDisplayCardMenu c in playerSquadsCards)
                c.SetOptionsVisibility(false, false);
            selectedCards.Clear();
            squadBattleInfo.Unhover();
        }
        private void UpdateSelectionOptions()
        {
            bool isSingle = selectedCards.Count == 1;
            bool canMerge = selectedCards.Count >= 2
                && selectedCards.TrueForAll(c => c.GetSquadToLoad().UnitName == selectedCards[0].GetSquadToLoad().UnitName)
                && selectedCards.TrueForAll(c => c.GetSquadToLoad().UnitPrestige == selectedCards[0].GetSquadToLoad().UnitPrestige);

            bool canPrestigeMulti = selectedCards.Count == 3
                && selectedCards.TrueForAll(c => c.GetSquadToLoad().UnitName == selectedCards[0].GetSquadToLoad().UnitName)
                && selectedCards.TrueForAll(c => c.GetSquadToLoad().UnitPrestige == selectedCards[0].GetSquadToLoad().UnitPrestige)
                && selectedCards.TrueForAll(c => c.GetSquadToLoad().SquadCurrentHealth > 0)
                && selectedCards[0].GetSquadToLoad().UnitPrestige < 2;

            SquadDisplayCardMenu mostRecent = selectedCards.Count > 0 ? selectedCards[^1] : null;
            foreach (SquadDisplayCardMenu c in playerSquadsCards)
            {
                if (!selectedCards.Contains(c))
                {
                    c.SetOptionsVisibility(false, false);
                    continue;
                }
                if (isSingle)
                    c.SetOptionsVisibility(true, true);
                else if (c == mostRecent)
                    c.SetOptionsVisibility(true, false, canMerge, canPrestigeMulti);
                else
                    c.SetOptionsVisibility(false, false);
            }
        }
        public void UpdateChapterText(int _chapter)
        {
            chapterText.text = $"{campaignSaveManager.SaveData.bookNumber} - {_chapter + 1}";
            chapterTooltipTrigger.SetUpToolTip(_title: GetChapterTooltipTitle(campaignSaveManager.SaveData.bookNumber, _chapter));
            chapterMMFeedback.PlayFeedbacks();
        }
        private string GetChapterTooltipTitle(int bookNumber, int chapter)
        {
            string actLocalized = LocalizationManager.Instance.GetText("Act");
            string chapterLocalized = LocalizationManager.Instance.GetText("Chapter");
            return $"{actLocalized} {bookNumber} - {chapterLocalized} {chapter + 1}";
        }
        private void ReloadGear()
        {
            List<GearID> gearNames = campaignSaveManager.SaveData.Gear;

            for (int i = 0; i < gearDisplays.Length; i++)
                gearDisplays[i].UnloadGearDisplay();

            for (int i = 0; i < gearNames.Count; i++)
                gearDisplays[i].LoadGearDisplay(gearNames[i]);

            for (int i = 0; i < gearDisplays.Length; i++)
            {
                if(gearDisplays[i].GetComponentInChildren<MetaprogressionLockedButton>() != null) {
                    gearDisplays[i].GetComponentInChildren<MetaprogressionLockedButton>().CheckLockedState();
                }
            }

            CampaignManager.Instance.ArmyJuiceManager.GearReloaded(gearDisplays);
        }
        private void ReloadConsumables()
        {
            // Debug.Log($"Reloading consumables");
            List<ConsumableEnum> consumableNames = CampaignManager.Instance.CampaignSaveManager.SaveData.consumables;
            for (int i = 0; i < consumableUI.Length; i++)
            {
                consumableUI[i].UnloadConsumableUI();

                if(consumableUI[i].GetComponentInChildren<MetaprogressionLockedButton>() != null) {
                    consumableUI[i].GetComponentInChildren<MetaprogressionLockedButton>().CheckLockedState();
                }
            }

            for (int i = 0; i < consumableNames.Count; i++){
                consumableUI[i].LoadConsumableUI(consumableNames[i]);
            }

            CampaignManager.Instance.ArmyJuiceManager.ConsumableReloaded(consumableUI);
        }
        public void ShowDisbandSquadConfirmation(string _guID)
        {
            pendingDisbandGuids = new List<string> { _guID };
            disbandSquadConfirmationPopup.CGEnable();
            disbandSquadConfirmationPopup.GetComponentInChildren<SettingsToggle>().OverrideToggleFromSettings();
        }
        public void HideDisbandSquadConfirmation()
        {
            disbandSquadConfirmationPopup.CGDisable();
        }
        public void DisbandSquad(string _guID)
        {
            campaignSaveManager.DisbandSquad(_guID);
            IAudioRequester.Instance.PlaySFX(SFXData.DisbandSquad);
            HideDisbandSquadConfirmation();
        }
        private void DisbandPendingSquads()
        {
            campaignSaveManager.DisbandMultipleSquads(pendingDisbandGuids);
            IAudioRequester.Instance.PlaySFX(SFXData.DisbandSquad);
            HideDisbandSquadConfirmation();
        }
        public void AttemptDisbandSelectedSquads()
        {
            if (selectedCards.Count == 0) return;
            List<string> guids = new();
            foreach (SquadDisplayCardMenu card in selectedCards)
                guids.Add(card.UniqueID);

            if (PlayerPrefs.GetInt("DisbandSquadConfirmation", 0) == 1)
            {
                pendingDisbandGuids = guids;
                DisbandPendingSquads();
            }
            else
            {
                pendingDisbandGuids = guids;
                disbandSquadConfirmationPopup.CGEnable();
                disbandSquadConfirmationPopup.GetComponentInChildren<SettingsToggle>().OverrideToggleFromSettings();
                IAudioRequester.Instance.PlaySFX(SFXData.DisbandSquad);
            }
        }
        public void MergeSelectedSquads()
        {
            List<string> guids = new();
            foreach (SquadDisplayCardMenu card in selectedCards)
                guids.Add(card.UniqueID);
            campaignSaveManager.MergeSquads(guids);
        }
        public void GiveRenameSquadPrompt(string _guID)
        {
            renameSquadGUID = _guID;
            renameSquadInputField.text = campaignSaveManager.GetUnitNameOrUnitNameOverride(_guID);
            renameSquadConfirmationPopup.CGEnable();
            mapSceneUIManager.MapSceneManager.SetMapInput(false);
        }
        public void RenameSquad()
        {
            campaignSaveManager.RenameSquad(renameSquadGUID, renameSquadInputField.text);
            renameSquadConfirmationPopup.CGDisable();
            mapSceneUIManager.MapSceneManager.SetMapInput(true);
        }
        public void MoveUnit(string _guID, int _index)
        {
            campaignSaveManager.MoveUnitToIndex(_guID, _index);
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.ReorderUnits);
        }
        public void ShiftUnit(string _guID, int _index)
        {
            campaignSaveManager.ShiftUnitToIndex(_guID, _index);
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.ReorderUnits);
        }
        // Bounds-check (not raycast) lookup so the boosted-sorting-order dragged card can't occlude its own hover target.
        public SquadDisplayCardMenu FindRealCardUnderScreenPoint(Vector2 _screenPoint, SquadDisplayCardMenu _exclude)
        {
            Transform[] parents = { deployedUnitsParent, reserveUnitsParent };
            foreach (Transform parent in parents)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    RectTransform childRect = parent.GetChild(i) as RectTransform;
                    SquadDisplayCardMenu card = parent.GetChild(i).GetComponent<SquadDisplayCardMenu>();
                    if (card == null || card == _exclude || childRect == null) continue;
                    if (RectTransformUtility.RectangleContainsScreenPoint(childRect, _screenPoint))
                        return card;
                }
            }
            return null;
        }
        // Returns the playerArmy index a card would land on if appended to the end of the packed region, or -1 if the region is full.
        public int GetFirstEmptySlotIndex(bool _deployedRegion, SquadDisplayCardMenu _exclude)
        {
            Transform parent = _deployedRegion ? deployedUnitsParent : reserveUnitsParent;
            int regionBase = _deployedRegion ? 0 : 10;
            int capacity = _deployedRegion ? 10 : campaignSaveManager.MaxReserveSlots;
            // Clamp to the actual playerArmy length in case MaxReserveSlots was just unlocked
            // mid-run and the save array hasn't been expanded yet (see CampaignSaveManager.EnsureArmyCapacity).
            capacity = Mathf.Min(capacity, campaignSaveManager.SaveData.playerArmy.Length - regionBase);

            int realCount = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                SquadDisplayCardMenu card = parent.GetChild(i).GetComponent<SquadDisplayCardMenu>();
                if (card != null && card != _exclude) realCount++;
            }

            if (realCount >= capacity) return -1;
            return regionBase + realCount;
        }
        public bool RegionHasRoom(bool _deployedRegion, SquadDisplayCardMenu _exclude)
        {
            return GetFirstEmptySlotIndex(_deployedRegion, _exclude) >= 0;
        }
        public void HighlightDeployedTroopsArea(bool _highlight)
        {
            deployedTroopsAreaImage.enabled = _highlight;

            if (_highlight) IAudioRequester.Instance.PlaySFX(SFXData.HoveredDepoyedTroops);
        }
        public void HighlightReserveTroopsArea(bool _highlight)
        {
            reserveTroopsAreaImage.enabled = _highlight;

            if (_highlight) IAudioRequester.Instance.PlaySFX(SFXData.HoveredReserveTroops);
        }
        public void LockCards(bool _lock)
        {
            foreach (SquadDisplayCardMenu squadDisplayCardMenu in playerSquadsCards)
            {
                squadDisplayCardMenu.LockCard(_lock);
            }
        }
        public void PrestigeUnit(string _guID)
        {
            CampaignManager.Instance.ArmyJuiceManager.UpdateSquadOnChange(new ArmyJuice {
                uniqueID = _guID,
                armyJuiceEnum = ArmyJuiceEnum.Prestige,
            });

            bool isMultiSelectPrestige = selectedCards.Count == 3
                && selectedCards.TrueForAll(c => c.GetSquadToLoad().UnitName == selectedCards[0].GetSquadToLoad().UnitName)
                && selectedCards.TrueForAll(c => c.GetSquadToLoad().UnitPrestige == selectedCards[0].GetSquadToLoad().UnitPrestige)
                && selectedCards.TrueForAll(c => c.GetSquadToLoad().SquadCurrentHealth > 0)
                && selectedCards[0].GetSquadToLoad().UnitPrestige < 2;

            if (isMultiSelectPrestige)
            {
                List<string> consumeUIDs = new();
                foreach (SquadDisplayCardMenu c in selectedCards)
                {
                    if (c.GetSquadToLoad().UniqueID != _guID)
                        consumeUIDs.Add(c.GetSquadToLoad().UniqueID);
                }
                campaignSaveManager.PrestigeAndCombineSpecificUnits(_guID, consumeUIDs[0], consumeUIDs[1]);
            }
            else
            {
                campaignSaveManager.PrestigeAndCombineUnits(_guID);
            }

            IAudioRequester.Instance.PlaySFX(SFXData.PrestigeUnit);
            mapSceneUIManager.TryDrainPendingPrestigeChoices();
        }
        public void CheckForPrestigeAvailability(PrestigeUnitButton _prestigeUnitButton, UnitName _unitName, int _unitLevel)
        {
            bool isAvailable = campaignSaveManager.CheckForPrestigeAvailability(_unitName, _unitLevel);
            _prestigeUnitButton.SetPrestigeAvailability(isAvailable);

            if (isAvailable)
            {
                // IAudioRequester.Instance.PlaySFX(SFXData.PrestigeAvailable);
                TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.PrestigeUnit });
            }
        }
        public void OnGoldChanged(int _goldAmount)
        {
            goldMMFeedback.StopFeedbacks();
            goldMMFeedback.PlayFeedbacks();
            if (rollGoldCoroutine != null) StopCoroutine(rollGoldCoroutine);
            rollGoldCoroutine = StartCoroutine(MemoriUI.RollTextCoroutine(float.Parse(goldAmountText.text), _goldAmount, goldAmountText));

            string earnedLocalized = LocalizationManager.Instance.GetText("earned interest per");
            string bonusLocalized = LocalizationManager.Instance.GetText("bonus interest from Omen of Famine");
            string ironBankLocalized = LocalizationManager.Instance.GetText("interest bonus from Iron Bank");
            string interestLocalized = LocalizationManager.Instance.GetText("Interest at turn end");
            string maxLocalized = LocalizationManager.Instance.GetText("Max");

            string flavorText = $"(+{CampaignManager.Instance.GoldManager.GetBaseInterest()}) 1 <sprite name=GoldSprite> {earnedLocalized} {CampaignManager.Instance.CampaignSaveManager.GoldRequiredToGenerateInterest} <sprite name=GoldSprite> ({maxLocalized} {CampaignManager.Instance.GoldManager.GetMaxInterest()})";

            int bonusFromOmenOfFamine = 0;
            if (CampaignManager.Instance.GearManager.CheckForGear(GearID.OmenofFamine))
            {
                List<GearID> gearIDs = campaignSaveManager.SaveData.Gear;
                bonusFromOmenOfFamine = 2 * (campaignSaveManager.MaxGear - gearIDs.Count);
                flavorText += $"\n(+{bonusFromOmenOfFamine}) <sprite name=GoldSprite> {bonusLocalized}";
            }

            if (CampaignManager.Instance.GearManager.CheckForGear(GearID.IronBank))
            {
                flavorText += $"\n(+{CampaignManager.Instance.GoldManager.GetBaseInterest() + bonusFromOmenOfFamine}) <sprite name=GoldSprite> {ironBankLocalized}";
            }

            goldTooltipTrigger.SetUpToolTip(_description: $"+{CampaignManager.Instance.GoldManager.GetTotalInterest()} <sprite name=GoldSprite> {interestLocalized}", _flavorText: flavorText);

            ReloadGear();
        }
        public void HideZeroHealthSquads()
        {
            for (int i = 0; i < playerSquadsCards.Count; i++)
            {
                playerSquadsCards[i].HideDeadSquads();
            }
            campaignSaveManager.ReorderUnits();
        }
        public string GetGuidFormHoveredUnit(int _index)
        {
            for (int i = 0; i < playerSquadsCards.Count; i++)
            {
                if (playerSquadsCards[i].SquadId == _index)
                {
                    return playerSquadsCards[i].UniqueID;
                }
            }
            Debug.LogError($"GetGuidFormHoveredUnit({_index}) - No squad found");
            return null;
        }
        public void ShowConsumablesBlocker()
        {
            if (consumablesBlocker == null) return;
            if (consumablesBlockerTooltip == null)
                consumablesBlockerTooltip = consumablesBlocker.GetComponent<MemoriTooltipTrigger>();
            if (consumablesBlockerTooltip != null)
            {
                string lockedText = LocalizationManager.Instance.GetText("Locked");
                consumablesBlockerTooltip.SetUpToolTip(_description: lockedText);
            }
            consumablesBlocker.SetActive(true);
        }
        public void HideConsumablesBlocker()
        {
            if (consumablesBlocker != null) consumablesBlocker.SetActive(false);
        }
        public void CloseNonSquadPopUps()
        {
            foreach (ConsumableUI consumableUI in consumableUI)
                consumableUI.CloseConsumableOptions();
            foreach (GearDisplay gearDisplay in gearDisplays)
                gearDisplay.CloseGearSellTag();
            HideDisbandSquadConfirmation();
        }
        public void CloseAllPopUps()
        {
            CloseNonSquadPopUps();
            DeselectAllCards();
            IAudioRequester.Instance.PlaySFX(SFXData.ClosePopUp);
        }
        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (selectedCards.Count == 0) return;

            PointerEventData pointerData = new(EventSystem.current) { position = Input.mousePosition };
            List<RaycastResult> results = new();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (RaycastResult result in results)
            {
                if (result.gameObject.GetComponentInParent<SquadDisplayCardMenu>() != null)
                    return;
            }

            DeselectAllCards();
        }
        private void ToggleFreeCameraMode()
        {
            if (isFreeCameraMode) ExitFreeCameraMode();
            else EnterFreeCameraMode();
        }

        private void EnterFreeCameraMode()
        {
            if (hudCanvas == null) return;

            hudChildCanvases.Clear();
            foreach (Canvas c in hudCanvas.GetComponentsInChildren<Canvas>(true))
                if (c != hudCanvas) hudChildCanvases.Add(c);

            CampaignManager.Instance.MapCamera.SaveFreeCameraState();
            mapSceneUIManager.MapSceneManager.SetMapInput(false);
            mapSceneUIManager.ShopPanel.SetFreeCameraMode(true);
            hudCanvas.enabled = false;
            foreach (Canvas c in hudChildCanvases) c.enabled = false;
            freeCameraOverlay.SetActive(true);
            isFreeCameraMode = true;
        }

        public void ExitFreeCameraMode()
        {
            if (hudCanvas == null) return;

            hudCanvas.enabled = true;
            foreach (Canvas c in hudChildCanvases) 
            {
                if(c != null)
                    c.enabled = true;
            }
            freeCameraOverlay.SetActive(false);
            CampaignManager.Instance.MapCamera.RestoreFreeCameraState();
            mapSceneUIManager.MapSceneManager.SetMapInput(true);
            mapSceneUIManager.ShopPanel.SetFreeCameraMode(false);
            isFreeCameraMode = false;
        }

        public void OnDestroy()
        {
            if (InputHandler.HasInstance) {
                InputHandler.Instance.SecondaryActionPressed -= CloseAllPopUps;
                InputHandler.Instance.OnToggleFreeCameraMode -= ToggleFreeCameraMode;
            }

            if (campaignSaveManager == null) return;
            campaignSaveManager.OnChapterCompleted -= UpdateChapterText;
            if(CampaignManager.HasInstance && CampaignManager.Instance.GoldManager != null)
                CampaignManager.Instance.GoldManager.OnGoldAmountChanged -= OnGoldChanged;

            campaignSaveManager.OnUnitHealthChanged -= ArmyHealthChanged;
            campaignSaveManager.OnGearChanged -= ReloadGear;
            campaignSaveManager.OnArmyStructureChanged -= ArmyStructureChanged;
            campaignSaveManager.OnConsumablesChanged -= ReloadConsumables;
        }
        public void DestroyEmptySquadCards()
        {
            foreach (GameObject emptySquadCard in emptySquadCards)
            {
                Destroy(emptySquadCard);
            }
            emptySquadCards.Clear();
        }
        public void MarkUnitAsJustUsedConsumable(int _unitIndex)
        {
            for (int i = 0; i < playerSquadsCards.Count; i++)
            {
                if (playerSquadsCards[i].SquadId == _unitIndex)
                {
                    playerSquadsCards[i].UseConsumable();
                    break;
                }
            }
        }
        public void ShowHoveredNodeText(NodeType nodeType, bool surprise, bool _hover)
        {
            if (surprise) unknownLabel.HoverUI(_hover);
            else
            {
                switch (nodeType)
                {
                    case NodeType.Skirmish:
                        skirmishLabel.HoverUI(_hover);
                        break;
                    case NodeType.Event:
                        eventLabel.HoverUI(_hover);
                        break;
                    case NodeType.Shop:
                        shopLabel.HoverUI(_hover);
                        break;
                    case NodeType.Town:
                        townLabel.HoverUI(_hover);
                        break;
                    // case NodeType.Warband:
                    //     unknownLabel.HoverUI(_hover);
                    //     break;
                    case NodeType.Treasure:
                        treasureLabel.HoverUI(_hover);
                        break;
                    case NodeType.Games:
                        if (tavernLabel != null) tavernLabel.HoverUI(_hover);
                        break;
                    case NodeType.Campfire:
                        if (campfireLabel != null) campfireLabel.HoverUI(_hover);
                        break;
                }
            }
        }
        public void ShowWeatherHover(Weather weather, bool show)
        {
            if (weatherHoverPanel == null) return;

            if (!show || weather == Weather.ClearSkies)
            {
                weatherHoverPanel.CGDisable();
                return;
            }

            string weatherWord = LocalizationManager.Instance.GetText("Weather");
            string weatherName = LocalizationManager.Instance.GetText(weather.ToString());
            string title = $"<color={ColorData.Primary}>{weatherWord}</color> {weatherName}";
            weatherHoverTitle.text = title;
            weatherHoverDescription.text = $"<color={ColorData.Tier1}>{LocalizationManager.Instance.GetText(weather.ToString() + "Desc")}</color>";
            weatherHoverPanel.CGEnable();
        }
        public void DisplayJuiceOnSquad(ArmyJuice _armyJuice)
        {
            for (int i = 0; i < playerSquadsCards.Count; i++)
            {
                if (playerSquadsCards[i].UniqueID == _armyJuice.uniqueID)
                {
                    if (_armyJuice.armyJuiceEnum == ArmyJuiceEnum.Health)
                    {
                        Debug.Log($"DisplayJuiceOnSquad({playerSquadsCards[i].UniqueID}) - {_armyJuice.value}");
                        playerSquadsCards[i].ShowHealthRecoveryJuice(_armyJuice.value);
                    }
                    else if (_armyJuice.armyJuiceEnum == ArmyJuiceEnum.Prestige)
                    {
                        playerSquadsCards[i].ShowPrestigeJuice(playerSquadsCards[i].SquadPrestige);
                    }
                    else if (_armyJuice.armyJuiceEnum == ArmyJuiceEnum.SpawnIn)
                    {
                        playerSquadsCards[i].SpawnInJuice(true);
                    }
                }
            }
        }
    }
}