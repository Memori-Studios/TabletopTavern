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
using TJ.Spells;

namespace TJ.Map
{
    public class HUDPanel : MonoBehaviour
    {
        [Header("Legend")]
        [SerializeField] private MemoriCanvasGroup legendSection;
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
        [SerializeField] private RectTransform disbandPopupBody;
        [SerializeField] private RectTransform renamePopupBody;

        [Header("Weather Hover")]
        [SerializeField] private CanvasGroup weatherHoverPanel;
        [SerializeField] private TMP_Text weatherHoverTitle;
        [SerializeField] private TMP_Text weatherHoverDescription;
        // A March node's host and Twists, above the army at the lower centre. Null on an older scene.
        [SerializeField] private CanvasGroup rogueHostHoverPanel;
        [SerializeField] private TMP_Text rogueHostHoverTitle;
        [SerializeField] private TMP_Text rogueHostHoverDescription;

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

        [Header("Spells")]
        // Display only: the run's loadout on the map's copy of the hotbar. Buttons are disabled, tooltips stay on.
        [SerializeField] private SpellCastButton[] spellCastButtons;

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
            renameSquadButtonCancel.onClick.AddListener(() => { ClosePopup(renameSquadConfirmationPopup); mapSceneUIManager.MapSceneManager.SetMapInput(true); });
            // testAquireConsumableButton.onClick.AddListener(() => CampaignManager.Instance.CampaignSaveManager.AquireConsumable(ConsumableData.GetRandomConsumable(campaignSaveManager.GetCampaignRandom())));

            campaignSaveManager.OnChapterCompleted += UpdateChapterText;
            CampaignManager.Instance.GoldManager.OnGoldAmountChanged += OnGoldChanged;
            campaignSaveManager.OnUnitHealthChanged += ArmyHealthChanged;
            campaignSaveManager.OnGearChanged += ReloadGear;
            campaignSaveManager.OnArmyStructureChanged += ArmyStructureChanged;
            campaignSaveManager.OnConsumablesChanged += ReloadConsumables;
            campaignSaveManager.OnOrdealsChanged -= OrdealsChanged;
            campaignSaveManager.OnOrdealsChanged += OrdealsChanged;
            InputHandler.Instance.SecondaryActionPressed += SecondaryAction;
            InputHandler.Instance.OnToggleFreeCameraMode += ToggleFreeCameraMode;

            ReloadGear();
            ReloadConsumables();
            ReloadSpells();
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

            ShowChapter(campaignSaveManager.SaveData.activeMapLayer);

            settingsTooltipTrigger.SetUpToolTip(_title: LocalizationManager.Instance.GetText("Settings"));
            RefreshFreeCameraPrompts();
            InputDevices.Changed -= RefreshFreeCameraPrompts;
            InputDevices.Changed += RefreshFreeCameraPrompts;
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
            List<string> allPreviousModifiers = DifficultyData.GetAllDifficultyModifiersUpToLevel(difficulty);

            foreach (string modifier in allPreviousModifiers)
            {
                additionalModifiersDesc += "- " + LocalizationManager.Instance.GetText(modifier) + "\n";
            }
            string difficultLevelTitleLocalized = LocalizationManager.Instance.GetText("Difficulty");
            difficultyTooltipTrigger.SetUpToolTip(_title: $"{difficultLevelTitleLocalized}: {difficultyLocalized}", _description: additionalModifiersDesc);
        }
        // An Ordeal can switch off gear, consumables, a faction passive or a spell slot, and change the gold loss per turn.
        private void OrdealsChanged()
        {
            UpdateHeroNameAndRace();
            ReloadGear();
            ReloadConsumables();
            ReloadSpells();
            OnGoldChanged(campaignSaveManager.SaveData.goldAmount);
        }
        private void UpdateHeroNameAndRace()
        {
            Hero hero = HeroData.GetHeroByID(campaignSaveManager.SaveData.heroID);
            string heroRace = HeroData.GetRaceFromHero(CampaignManager.Instance.CampaignSaveManager.GetHeroID()).ToString();
            string heroRaceLocalized = LocalizationManager.Instance.GetText(heroRace);
            string heroNameLocalized = LocalizationManager.Instance.GetText(hero.HeroName);
            heroNameText.text = heroNameLocalized;
            heroRaceText.text = heroRaceLocalized;

            string heroBonusText1string = HeroBonusText.Get(hero, 0);
            string heroBonusText2string = HeroBonusText.Get(hero, 1);
            string raceBonusTextstring = KeywordText.ForTooltip(LocalizationManager.Instance.GetText(hero.Race+ "BonusDescription"));
            heroBonusText1string += "\n" + heroBonusText2string;
            if (campaignSaveManager.SaveData.heroID == CampaignSaveManager.SKRIX_HERO_ID)
            {
                string koboldProgress = string.Format(LocalizationManager.Instance.GetText("KoboldBonusProgress"),
                    campaignSaveManager.CountKoboldUnits(), CampaignSaveManager.SKRIX_KOBOLD_THRESHOLD);
                heroBonusText1string += "\n" + koboldProgress;
            }
            heroNameTooltipTrigger.SetUpToolTip(_title: heroNameLocalized, _description: KeywordText.ForTooltip(heroBonusText1string));

            OrdealId countering = OrdealRegistry.CounteringOrdeal(campaignSaveManager.SaveData.ActiveOrdeals, hero.Race);
            heroRaceText.alpha = countering != OrdealId.None ? 0.5f : 1f;
            heroRaceTooltipTrigger.SetUpToolTip(new TooltipContent
            {
                Title = heroRaceLocalized,
                Body = raceBonusTextstring,
                Footer = countering != OrdealId.None ? OrdealRegistry.InactiveNote(countering) : "",
            });
        }
        public void ArmyStructureChanged()
        {
            // Debug.Log($"Army structure changed");
            RefreshTroopsPanel();
            UpdateHeroNameAndRace(); // Kobold count in the hero tooltip
            // Silent: CloseAllPopUps plays the close sound, which then played on every move and merge.
            CloseNonSquadPopUps();
            DeselectAllCards();
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
        // Public so a rejected card drop can ask for the rebuild it would otherwise have got for free from
        // OnArmyStructureChanged (see SquadDisplayCardMenu.OnEndDrag).
        public void RefreshTroopsPanel()
        {
            // Debug.Log($"Refreshing troops panel");

            // Resolved before the destroy sweeps below, not after. RecordGameOver deletes the campaign save
            // (CampaignSaveManager.saveData = null) while this panel is still on screen underneath the
            // game-over panel, and reading playerArmy after clearing the cards would wipe the panel and
            // then throw on the way to refilling it.
            SquadToLoad[] playerSquads = campaignSaveManager.SaveData?.playerArmy;
            if (playerSquads == null) return;

            // A rejected drop rebuilds without going through ArmyStructureChanged; a selected card left in
            // selectedCards past its Destroy wedges every later DeselectAllCards.
            DeselectAllCards();
            playerSquadsCards = new List<SquadDisplayCardMenu>();
            foreach (Transform child in deployedUnitsParent) Destroy(child.gameObject);
            foreach (Transform child in reserveUnitsParent) Destroy(child.gameObject);
            emptySquadCards = new();

            deployedTroopsCount = 0;
            reserveTroopsCount = 0;
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
                squadDisplayCardMenu.EnableQuickMoveTooltip();
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
        public void PulseReservePennants()
        {
            foreach (SquadDisplayCardMenu card in playerSquadsCards)
                if (card != null) card.PulseReservePennant();
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
                // With a card selected, the panel keeps showing it and the hovered squad hangs off its right.
                SquadDisplayCardMenu selected = selectedCards.Count > 0 ? selectedCards[0] : null;
                if (selected != null && selected.transform != _squadCardTransform)
                {
                    squadBattleInfo.SetUpCampaign(selected.GetSquadToLoad(), selected.CardTeam);
                    squadBattleInfo.ShowComparison(panel => panel.SetUpCampaign(squad, team));
                }
                else
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
            // A destroyed card throws on SelectSquad before Clear() runs, so it would stay selected forever.
            selectedCards.RemoveAll(c => c == null);
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
                {
                    if (canMerge)
                        c.SetMergeAvailable(CampaignSaveManager.MergeChangesArmy(selectedCards.ConvertAll(s => s.GetSquadToLoad())));
                    c.SetOptionsVisibility(true, false, canMerge, canPrestigeMulti);
                }
                else
                    c.SetOptionsVisibility(false, false);
            }
            // Optional tip: never cut into a step chain the player is working through, like reorder and disband.
            if (isSingle && !TutorialManager.Instance.IsShowingStep)
                TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.RenameSquad });
        }
        public void UpdateChapterText(int _chapter)
        {
            ShowChapter(_chapter);
            chapterMMFeedback.PlayFeedbacks();
        }
        // Story acts read "act - chapter". The March has no acts: it reads the battle the player is on.
        private void ShowChapter(int _chapter)
        {
            Memori.SaveData.CampaignSaveData run = campaignSaveManager.SaveData;
            if (run.InMarch)
            {
                int battle = MarchRules.CurrentBattle(run);
                int untilWarlord = MarchRules.WARLORD_EVERY - (battle - 1) % MarchRules.WARLORD_EVERY - 1;
                string title = string.Format(LocalizationManager.Instance.GetText("marchBattle"), battle);
                title += "\n" + (untilWarlord == 0
                    ? LocalizationManager.Instance.GetText("marchWarlordNow")
                    : string.Format(LocalizationManager.Instance.GetText("marchWarlordIn"), untilWarlord));
                chapterText.text = battle.ToString();
                chapterTooltipTrigger.SetUpToolTip(_title: title);
                return;
            }
            chapterText.text = $"{run.bookNumber} - {_chapter + 1}";
            chapterTooltipTrigger.SetUpToolTip(_title: GetChapterTooltipTitle(run.bookNumber, _chapter));
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
        private void ReloadSpells()
        {
            if (spellCastButtons == null) return;
            SpellData[] spells = SaveDataHandler.GetCampaignSpells();
            for (int i = 0; i < spellCastButtons.Length; i++)
            {
                if (spellCastButtons[i] == null) continue;
                SpellData spellData = spells != null && i < spells.Length ? spells[i] : null;
                // Hotkey 0 renders no digit: there is nothing to press on the map.
                spellCastButtons[i].LoadSpellUI(spellData, null, 0);
                spellCastButtons[i].SetLocked(Memori.SaveData.SaveDataHandler.IsCampaignSlotLocked(i));
                spellCastButtons[i].SetReadOnly();
            }
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
            OpenPopup(disbandSquadConfirmationPopup, disbandPopupBody);
            disbandSquadConfirmationPopup.GetComponentInChildren<SettingsToggle>().OverrideToggleFromSettings();
        }
        public void HideDisbandSquadConfirmation()
        {
            ClosePopup(disbandSquadConfirmationPopup);
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
                OpenPopup(disbandSquadConfirmationPopup, disbandPopupBody);
                disbandSquadConfirmationPopup.GetComponentInChildren<SettingsToggle>().OverrideToggleFromSettings();
                IAudioRequester.Instance.PlaySFX(SFXData.DisbandSquad);
            }
        }
        public void MergeSelectedSquads()
        {
            List<string> guids = new();
            SquadDisplayCardMenu survivor = null;
            foreach (SquadDisplayCardMenu card in selectedCards)
            {
                guids.Add(card.UniqueID);
                // MergeSquads fills the squad in the lowest slot first, so that one survives.
                if (survivor == null || card.GetSquadToLoad().UnitIndex < survivor.GetSquadToLoad().UnitIndex) survivor = card;
            }
            if (survivor == null) return;
            string survivorGuid = survivor.UniqueID;
            int unitsBefore = UnitCount(survivor.GetSquadToLoad());
            campaignSaveManager.MergeSquads(guids);
            LandAfterRebuild(survivorGuid, true, unitsBefore);
        }
        public void GiveRenameSquadPrompt(string _guID)
        {
            renameSquadGUID = _guID;
            renameSquadInputField.text = campaignSaveManager.GetUnitNameOrUnitNameOverride(_guID);
            OpenPopup(renameSquadConfirmationPopup, renamePopupBody);
            mapSceneUIManager.MapSceneManager.SetMapInput(false);
            renameSquadInputField.ActivateInputField();
            renameSquadInputField.Select();
        }
        public void RenameSquad()
        {
            campaignSaveManager.RenameSquad(renameSquadGUID, renameSquadInputField.text);
            ClosePopup(renameSquadConfirmationPopup);
            mapSceneUIManager.MapSceneManager.SetMapInput(true);
        }
        public void MoveUnit(string _guID, int _index)
        {
            campaignSaveManager.MoveUnitToIndex(_guID, _index);
            LandAfterRebuild(_guID, false);
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.ReorderUnits);
        }
        public void ShiftUnit(string _guID, int _index)
        {
            campaignSaveManager.ShiftUnitToIndex(_guID, _index);
            LandAfterRebuild(_guID, false);
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
            // No army to place into once the run has ended and the save was deleted - report the region as
            // full so callers reject the drop instead of dereferencing it.
            SquadToLoad[] playerArmy = campaignSaveManager.SaveData?.playerArmy;
            if (playerArmy == null) return -1;

            Transform parent = _deployedRegion ? deployedUnitsParent : reserveUnitsParent;
            int regionBase = _deployedRegion ? 0 : 10;
            int capacity = _deployedRegion ? 10 : campaignSaveManager.MaxReserveSlots;
            // Clamp to the actual playerArmy length in case MaxReserveSlots was just unlocked
            // mid-run and the save array hasn't been expanded yet (see CampaignSaveManager.EnsureArmyCapacity).
            capacity = Mathf.Min(capacity, playerArmy.Length - regionBase);

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
        // A full army gives up its most wounded squad, full reserves their healthiest. Health is a share of max, or the
        // squads with the smallest health pool would always be picked.
        public SquadDisplayCardMenu PickQuickMoveSwapTarget(bool _deployedRegion)
        {
            SquadDisplayCardMenu pick = null;
            float pickShare = 0f;
            foreach (SquadDisplayCardMenu card in playerSquadsCards)
            {
                if (card.InReserve == _deployedRegion) continue;
                SquadToLoad cardSquad = card.GetSquadToLoad();
                float share = cardSquad.SquadMaxHealth > 0 ? (float)cardSquad.SquadCurrentHealth / cardSquad.SquadMaxHealth : 0f;
                bool better = _deployedRegion ? share < pickShare : share > pickShare;
                if (pick == null || better)
                {
                    pick = card;
                    pickShare = share;
                }
            }
            return pick;
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
        // Called once from GameOverPanel.RecordGameOver, which has just deleted the campaign save. The map
        // and this panel stay on screen underneath the game-over panel, so the cards have to stop taking
        // input: every mutation path (drag, prestige, disband, rename, consumables) reads
        // CampaignSaveManager.SaveData, which is now null. Nothing done after the run ends could persist
        // anyway. Deliberately one-way - nothing unlocks these, the scene is torn down on exit to menu.
        public void LockForRunEnd()
        {
            LockCards(true);
            ShowConsumablesBlocker();
            // The node legend has nothing to explain once the run is over, and it covers the game-over buttons on small screens.
            if (legendSection != null) legendSection.CGDisable();
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
            int shownGold = int.TryParse(goldAmountText.text, out int parsedGold) ? parsedGold : _goldAmount;
            if (!_goldShown)
            {
                // The first value on load arrives without ticks.
                _goldShown = true;
                rollGoldCoroutine = StartCoroutine(MemoriUI.RollTextCoroutine(shownGold, _goldAmount, goldAmountText));
            }
            else
            {
                bool gain = _goldAmount > shownGold;
                rollGoldCoroutine = StartCoroutine(UIJuice.CountTo(goldAmountText, shownGold, _goldAmount, GoldCountTime, v => v.ToString(), gain ? PlayCoinTick : null));
                if (_goldAmount < shownGold) FlashGoldLoss();
            }

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

            string description = $"+{CampaignManager.Instance.GoldManager.GetTotalInterest()} <sprite name=GoldSprite> {interestLocalized}";
            int ordealLoss = OrdealRegistry.GoldLostPerTurn(campaignSaveManager.SaveData);
            if (ordealLoss > 0)
                description += $"\n<color={ColorData.Negative}>-{ordealLoss}</color> <sprite name=GoldSprite> {LocalizationManager.Instance.GetText("OrdealGoldLossPerTurn")}";
            goldTooltipTrigger.SetUpToolTip(_description: description, _flavorText: flavorText);

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
        // A right-click that marks a map node is not a dismiss, and would play the close sound over the mark's own.
        private void SecondaryAction()
        {
            if (mapSceneUIManager.MapSceneManager.CanMarkHoveredNode) return;
            CloseAllPopUps();
        }
        public void CloseAllPopUps()
        {
            CloseNonSquadPopUps();
            DeselectAllCards();
            IAudioRequester.Instance.PlaySFX(SFXData.ClosePopUp);
        }
        private void Update()
        {
            if (!global::Memori.Input.GameCursor.GetButtonDown(0)) return;
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

        // Null on a pad, which leaves free camera through its on-screen button.
        private static string FreeCameraKey() => InputGlyphs.For(InputHandler.Instance.GameControls.Battle.ToggleFreeCameraMode);

        private void RefreshFreeCameraPrompts()
        {
            string freeCamKey = FreeCameraKey();
            string mode = LocalizationManager.Instance.GetText("FreeCameraMode");
            string exit = LocalizationManager.Instance.GetText("exitButton");
            freeCameraTooltipTrigger.SetUpToolTip(_title: freeCamKey == null ? mode : $"{mode} [{freeCamKey}]");
            returnFromFreeCameraTooltipTrigger.SetUpToolTip(_title: freeCamKey == null ? $"{exit} {mode}" : $"{exit} {mode} [{freeCamKey}]");
            returnFromFreeCameraKeyText.text = freeCamKey == null ? $"{exit} {mode}" : $"{exit} {mode} - [{freeCamKey}]";
        }

        public void ShowFreeCameraTip()
        {
            TutorialManager.Instance.LoadTooltip(TutorialData.FreeCamera, freeCameraButton.transform, CalloutSide.TowardCenter, FreeCameraKey() ?? LocalizationManager.Instance.GetText("InputOnScreen"));
        }

        private void EnterFreeCameraMode()
        {
            if (hudCanvas == null) return;

            // The callout lives on the Tutorial Canvas, which the HUD hide below does not reach.
            TutorialManager.Instance.CloseTooltip();

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

        #region Juice
        private const float GoldCountTime = 0.35f;
        private const float GoldFlashTime = 0.45f;
        private bool _goldShown;
        private Color _goldRestColor;
        private bool _goldRestKnown;
        private Coroutine _goldFlash;

        private static void PlayCoinTick() => IAudioRequester.Instance.PlaySFX(SFXData.CoinClink);

        // A loss flashes the bad colour once; buying already plays its own sound.
        private void FlashGoldLoss()
        {
            if (!_goldRestKnown) { _goldRestColor = goldAmountText.color; _goldRestKnown = true; }
            if (_goldFlash != null) StopCoroutine(_goldFlash);
            _goldFlash = StartCoroutine(FlashGold());
        }

        private IEnumerator FlashGold()
        {
            Color bad = ColorVision.Bad(Color.red);
            for (float t = 0f; t < 1f; t += Mathf.Min(Time.unscaledDeltaTime, UIJuice.MaxStep) / GoldFlashTime)
            {
                goldAmountText.color = Color.Lerp(bad, _goldRestColor, UIJuice.EaseOutCubic(t));
                yield return null;
            }
            goldAmountText.color = _goldRestColor;
            _goldFlash = null;
        }

        // A move or merge rebuilds the whole bar, sometimes twice in one frame; the card lands on the next frame.
        private void LandAfterRebuild(string _guid, bool _merge, int _unitsBefore = 0)
        {
            if (string.IsNullOrEmpty(_guid) || !isActiveAndEnabled) return;
            StartCoroutine(LandNextFrame(_guid, _merge, _unitsBefore));
        }

        // A merge shows the troops that joined with the card's own green +N, the heal pop's language.
        private IEnumerator LandNextFrame(string _guid, bool _merge, int _unitsBefore)
        {
            yield return null;
            if (playerSquadsCards == null) yield break;
            SquadDisplayCardMenu card = playerSquadsCards.Find(c => c != null && c.UniqueID == _guid);
            if (card == null) yield break;
            card.PlayLanded();
            IAudioRequester.Instance.PlaySFX(_merge ? SFXData.UpgradeUnlock : SFXData.TinyClick);
            if (!_merge) yield break;
            int joined = UnitCount(card.GetSquadToLoad()) - _unitsBefore;
            if (joined > 0) card.ShowHealthRecoveryJuice(joined);
        }

        private static int UnitCount(SquadToLoad _squad) =>
            int.TryParse(TabletopTavernData.Instance.GetSquadCurrentUnitCount(_squad), out int count) ? count : 0;

        private readonly Dictionary<CanvasGroup, Coroutine> _popupMotion = new();

        private void OpenPopup(CanvasGroup _popup, RectTransform _body)
        {
            StopPopupMotion(_popup);
            _popup.CGEnable();
            _popupMotion[_popup] = StartCoroutine(UIJuice.Open(_popup, _body));
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
        }

        // Runs on every army change too, so a pop-up that is already shut stays silent.
        private void ClosePopup(CanvasGroup _popup)
        {
            StopPopupMotion(_popup);
            if (_popup.alpha <= 0f)
            {
                _popup.CGDisable();
                return;
            }
            _popup.interactable = false;
            _popup.blocksRaycasts = false;
            IAudioRequester.Instance.PlaySFX(SFXData.ClosePopUp);
            _popupMotion[_popup] = StartCoroutine(FadeOutPopup(_popup));
        }

        private IEnumerator FadeOutPopup(CanvasGroup _popup)
        {
            yield return UIJuice.Close(_popup);
            _popup.CGDisable();
            _popupMotion.Remove(_popup);
        }

        private void StopPopupMotion(CanvasGroup _popup)
        {
            if (!_popupMotion.TryGetValue(_popup, out Coroutine motion)) return;
            if (motion != null) StopCoroutine(motion);
            _popupMotion.Remove(_popup);
        }
        #endregion
        public void OnDestroy()
        {
            InputDevices.Changed -= RefreshFreeCameraPrompts;
            if (InputHandler.HasInstance) {
                InputHandler.Instance.SecondaryActionPressed -= SecondaryAction;
                InputHandler.Instance.OnToggleFreeCameraMode -= ToggleFreeCameraMode;
            }
            // The free camera callout sits on the persistent Tutorial Canvas and would follow the player into battle.
            if (TutorialManager.HasInstance)
                TutorialManager.Instance.CloseTooltip();

            if (campaignSaveManager == null) return;
            campaignSaveManager.OnChapterCompleted -= UpdateChapterText;
            if(CampaignManager.HasInstance && CampaignManager.Instance.GoldManager != null)
                CampaignManager.Instance.GoldManager.OnGoldAmountChanged -= OnGoldChanged;

            campaignSaveManager.OnUnitHealthChanged -= ArmyHealthChanged;
            campaignSaveManager.OnGearChanged -= ReloadGear;
            campaignSaveManager.OnArmyStructureChanged -= ArmyStructureChanged;
            campaignSaveManager.OnConsumablesChanged -= ReloadConsumables;
            campaignSaveManager.OnOrdealsChanged -= OrdealsChanged;
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
            weatherHoverDescription.text = $"<color={ColorData.Tier1}>{WeatherInfo.GetDescription(weather)}</color>";
            weatherHoverPanel.CGEnable();
        }
        /// <summary>A March node on hover: the host's faction and what each Twist does, in its own panel above the army.</summary>
        public void ShowRogueHostHover(Race race, bool warlord, List<OrdealId> twists, bool show)
        {
            if (rogueHostHoverPanel == null) return;
            if (!show)
            {
                rogueHostHoverPanel.CGDisable();
                return;
            }

            string host = LocalizationManager.Instance.GetText(warlord ? "marchWarlord" : "marchRogueHost");
            rogueHostHoverTitle.text = $"<color={ColorData.Primary}>{host}</color> {LocalizationManager.Instance.GetText(race.ToString())}";

            List<string> lines = new();
            foreach (OrdealId twist in twists)
            {
                OrdealDefinition definition = OrdealRegistry.Get(twist);
                string description = KeywordText.Render(LocalizationManager.Instance.GetText(definition.TwistDescriptionKey), false);
                lines.Add($"<color={ColorData.Error}>{LocalizationManager.Instance.GetText(definition.NameKey)}</color> <color={ColorData.Tier1}>{description}</color>");
            }
            rogueHostHoverDescription.text = string.Join("\n", lines);
            // The panel sizes to its text through layout; rebuild now so it never shows a frame at the last node's size.
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)rogueHostHoverPanel.transform);
            rogueHostHoverPanel.CGEnable();
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