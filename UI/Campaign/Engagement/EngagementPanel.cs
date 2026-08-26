using System.Collections.Generic;
using Memori.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TJ.Map;
using Memori.SaveData;
using Memori.Tooltip;
using Memori.Audio;
using Memori.Notifications;
using Memori.UI;
using TJ.IrregularGrid;
using Memori.Localization;
using System.Threading.Tasks;
using MoreMountains.Feedbacks;
using Memori.Metaprogression;
using TJ.Recruit;
using System.Linq;

namespace TJ.Engagement
{

    public class EngagementPanel : MapPanel
    {
        [Header("Pre Battle Options")]
        [SerializeField] private MemoriCanvasGroup battleOptionsCanvasGroup;
        [SerializeField] private TMP_Text enemyArmyText;
        [SerializeField] private Transform enemyArmyParent;
        public Transform EnemyArmyParent => enemyArmyParent;
        [SerializeField] private SquadDisplayCardMenu squadDisplayCardMenuPrefab;
        [SerializeField] private MemoriCanvasGroup enemyArmyCanvasGroup;
        [SerializeField] private Button autoResolveButton, startBattleButton;
        [SerializeField] private AutoResolvePreview autoResolvePreview;
        [SerializeField] private MemoriTooltipTrigger autoResolveTooltipTrigger, manuallyFightTooltipTrigger;
        [SerializeField] private AutoResolveBattleManager autoResolveBattleManager;
        [SerializeField] private TMPSpawnScaler battlefieldWeatherText;
        [SerializeField] private MemoriTooltipTrigger battlefieldWeatherTooltip;
        [SerializeField] private TMP_Text battlefieldBiomeText;
        [SerializeField] private TMP_Text autoresolveResultText;
        [SerializeField] private Button heavensongButton;
        [SerializeField] private TMP_Text heavensongButtonText;
        [SerializeField] private MemoriTooltipTrigger heavensongTooltip;

        [Header("Post Battle")]
        [SerializeField] private MemoriCanvasGroup postBattleTotalCanvasGroup;
        [SerializeField] private Button claimRewardsButton;

        [Header("Constant Choice Gold Rewards")]
        [SerializeField] private Button goldRewardButton;
        [SerializeField] private TMP_Text goldRewardText;
        int goldRewardAmount;

        [Header("Constant Choice Consumable Rewards")]
        [SerializeField] private Button claimConsumableButton;
        [SerializeField] private MemoriTooltipTrigger consumableTooltip;
        [SerializeField] private TMP_Text consumableText;
        [SerializeField] private Image consumableIcon;

        [Header("Constant Choice Recruit Unit Rewards")]
        [SerializeField] private Button recruitUnitButton;
        [SerializeField] private MemoriTooltipTrigger recruitUnitTooltip;
        [SerializeField] private TMP_Text recruitUnitText;
        [SerializeField] private Image recruitUnitIcon;

        [Header("Optional Rewards")]
        [SerializeField] private MemoriCanvasGroup postBattleChoicesCanvasGroup;

        #region Ransom Captives
        [SerializeField] private Button ransomCaptivesButton; //bonus gold
        [SerializeField] private TMP_Text ransomAmountText;
        #endregion

        #region Conscript Survivors
        [SerializeField] private EngagementRewardButton conscriptSurvivorsButtonScript;
        [SerializeField] private SquadDisplayCardMenu squadDisplayCardConscript;
        [SerializeField] private Sprite conscriptSurvivorsImageSprite;
        [SerializeField] private Image conscriptSurvivorsImage;
        [SerializeField] private Button conscriptSurvivorsButton; // enemy pack
        [SerializeField] private MemoriTooltipTrigger conscriptSurvivorsTooltip;
        private UnitName[] conscriptedUnitNames;
        #endregion

        #region Raise Dead
        [SerializeField] private EngagementRewardButton raiseDeadButton;
        [SerializeField] private SquadDisplayCardMenu raiseDeadCard1, raiseDeadCard2, raiseDeadCard3;
        #endregion

        #region Consume Survivors
        [SerializeField] private Button consumeSurvivorsButton;
        #endregion

        #region Loot Battlefield
        [SerializeField] private Button lootBattlefieldButton;
        GearID gearID;
        [SerializeField] private Image lootBattlefieldImage;
        [SerializeField] private TMP_Text lootBattlefieldText;
        [SerializeField] private GameObject lootBattlefieldGearFull;
        [SerializeField] private MemoriTooltipTrigger lootBattlefieldTooltip;
        #endregion

        #region Purge The Blight
        [SerializeField] private Button purgeTheBlightButton;
        #endregion

        #region Hour of Destiny
        [SerializeField] private Button hourOfDestinyButton;
        #endregion

        #region Forbidden Rituals
        [SerializeField] private Button forbiddenRitualsButton;
        [SerializeField] private ConsumableEnum _generatedConsumbale;
        [SerializeField] private Image _generatedConsumbaleImage;
        [SerializeField] private TMP_Text _generatedConsumbaleText;
        [SerializeField] private MemoriTooltipTrigger _generatedConsumbaleTooltip;
        #endregion

        [Header("Run Lost")]
        [SerializeField] private MemoriCanvasGroup runLostCanvasGroup;

        [Header("End Battle")]
        [SerializeField] private GameObject endBattlePanel;
        [SerializeField] private Button endRunButton, lootTownButton, continueButton;
        [SerializeField] private TMP_Text battleOutcomeText, battleVictoryOrDefeatText;
        [SerializeField] private MemoriCanvasGroup endBattleCanvasGroup;

        [Header("Juice")]
        [SerializeField] private MMF_Player openMMFPlayer;
        [SerializeField] private MMF_Player closeMMFPlayer;

        [Header("Metaprogression")]
        [SerializeField] private MetaprogressionModel _postBattleConsumableMetaprogressionModel;
        [SerializeField] private MetaprogressionModel _postBattleGoldMetaprogressionModel, _postBattleHealthMetaprogressionModel, _postBattleRecruitMetaprogressionModel;

        private CampaignSaveManager campaignSaveManager;
        private MemoriCanvasGroup engagementPanelCanvasGroup;
        private MapSceneUIManager mapSceneUIManager;
        private RecruitPanel recruitPanel;
        private int ransomAmount;
        private EngagementType engagementType;
        private List<SquadDisplayCardMenu> enemySquadsCards;
        private MemoriTooltipTrigger startBattleTooltipTrigger;
        private ConsumableEnum consumableEnum;
        private bool autoResolved;
        private bool garrisonFight;
        // private UnitName squadToConscriptName;
        private bool generateConsumable;
        private UnitRarity recruitsRarity;
        private bool postBattleChoicesClaimed;
        private bool autoResolveResult;
        private bool isLoadingEnemyCompany;
        private bool recruitButtonHiddenByConscript;
        private bool raiseDeadHiddenByRecruit;
        List<UnitName> raiseDeadUnitList = new ();
        private Dictionary<Weather, string> _cachedWeatherNames;

        // Watchdog against a stalled LoadEngagement/ShowEngagementResult chain. Both methods hide/lock
        // the panel, then rely on a run of "await Task.Delay(...)" continuations to unlock it later. If a
        // continuation never resumes (observed after alt-tabbing during the post-battle map reload, likely
        // an OS focus/fullscreen-transition hiccup dropping the SynchronizationContext callback), the panel
        // is left permanently visible-but-non-interactable with no options shown. _engagementRunId lets a
        // stale continuation detect it's been superseded and bail out instead of double-applying rewards.
        private int _engagementRunId;
        private bool _engagementRunComplete;
        private int _engagementRetryCount;
        private const float ENGAGEMENT_WATCHDOG_TIMEOUT = 5f;
        private const int ENGAGEMENT_MAX_RETRIES = 2;

        // A run can also finish "successfully" and still strand the player: ShowEngagementResult activates
        // the action buttons, but the canvas group carrying them is faded in by a fire-and-forget async Task
        // that sets blocksRaycasts before it starts lerping alpha. A dropped continuation there leaves an
        // invisible click-eating screen with the enemy cards (own sorted canvas) as the only responsive
        // thing. This flag marks "results are on screen" so the watchdog can sanity-check visibility.
        private bool _showedEngagementResult;

        #region SetUp
        public void SetUp(CampaignSaveManager _campaignSaveManager, MapSceneUIManager _mapSceneUIManager)
        {
            mapSceneUIManager = _mapSceneUIManager;
            campaignSaveManager = _campaignSaveManager;
            engagementPanelCanvasGroup = GetComponent<MemoriCanvasGroup>();
            recruitPanel = mapSceneUIManager.RecruitPanel;

            autoResolveButton.onClick.AddListener(AutoResolveButtonClicked);
            startBattleButton.onClick.AddListener(StartBattleButtonClicked);

            postBattleTotalCanvasGroup.CGDisable();
            claimConsumableButton.onClick.AddListener(ClaimConsumableButtonClicked);
            goldRewardButton.onClick.AddListener(ClaimGoldRewardButtonClicked);
            recruitUnitButton.onClick.AddListener(ClaimRecruitUnitButtonClicked);

            ransomCaptivesButton.onClick.AddListener(RansomCaptivesButtonClicked);
            conscriptSurvivorsButton.onClick.AddListener(ConscriptSurvivorsButtonClicked);
            // restArmyButton.onClick.AddListener(RestArmyButtonClicked);
            postBattleChoicesCanvasGroup.CGDisable();
            claimRewardsButton.onClick.AddListener(ShowPostBattleOptions);

            endRunButton.onClick.AddListener(LoseRun);
            startBattleTooltipTrigger = startBattleButton.GetComponent<MemoriTooltipTrigger>();
            campaignSaveManager.OnArmyStructureChanged += OnArmyStructureChanged;
            lootTownButton.onClick.AddListener(() => CompleteEngagement(true));
            lootTownButton.gameObject.SetActive(false);
            continueButton.onClick.AddListener(() => CompleteEngagement(false));
            continueButton.gameObject.SetActive(false);
            conscriptSurvivorsButtonScript.SetUp(this);
            conscriptSurvivorsButtonScript.enabled = false;
            squadDisplayCardConscript.gameObject.SetActive(false);
            claimRewardsButton.gameObject.SetActive(false);

            string localizedAutoResolveDescription = LocalizationManager.Instance.GetText("AutoResolveDesc");
            string localizedManuallyFightDescription = LocalizationManager.Instance.GetText("ManuallyFightDesc");
            autoResolveTooltipTrigger.SetUpToolTip(_description: localizedAutoResolveDescription, _delay: 0.5f);
            manuallyFightTooltipTrigger.SetUpToolTip(_description: localizedManuallyFightDescription, _delay: 0.5f);
            autoResolveButton.interactable = false;
            startBattleButton.interactable = false;

            TurnOffAllOptionalRewards();
            consumeSurvivorsButton.onClick.AddListener(() => ConsumeCaptivesButtonClicked());
            lootBattlefieldButton.onClick.AddListener(() => LootBattlefieldButtonClicked());
            purgeTheBlightButton.onClick.AddListener(() => PurgeTheBlightButtonClicked());
            forbiddenRitualsButton.onClick.AddListener(() => ForbiddenRitualsButtonClicked());
            hourOfDestinyButton.onClick.AddListener(() => HourOfDestinyButtonClicked());

            heavensongButton.onClick.AddListener(HeavensongButtonClicked);
            heavensongButtonText.text = LocalizationManager.Instance.GetText("Reroll") + " " + LocalizationManager.Instance.GetText("Weather");
            heavensongTooltip.SetUpToolTip(LocalizationManager.Instance.GetText("Campaign Bonus"), LocalizationManager.Instance.GetText("TaelindorForestBonusDescription"));

            //DifficultyMod 20
            if (campaignSaveManager.SaveData.difficultyLevel == TT_Difficulty.Godking)
            {
                autoResolveButton.interactable = false;
                autoResolveButton.gameObject.SetActive(false);
            }

            //DifficultyMod 3
            if (campaignSaveManager.SaveData.difficultyLevel < TT_Difficulty.Squire)
            {
                autoResolvePreview.SetUp(this);
            }
        }
        private void TurnOffAllOptionalRewards()
        {
            conscriptSurvivorsButton.gameObject.SetActive(false);
            conscriptSurvivorsButtonScript.enabled = false;

            raiseDeadButton.enabled = false;
            raiseDeadButton.SetUp(this);
            raiseDeadCard1.gameObject.SetActive(false);
            raiseDeadCard2.gameObject.SetActive(false);
            raiseDeadCard3.gameObject.SetActive(false);

            consumeSurvivorsButton.gameObject.SetActive(false);
            lootBattlefieldButton.gameObject.SetActive(false);
            purgeTheBlightButton.gameObject.SetActive(false);
            forbiddenRitualsButton.gameObject.SetActive(false);
            hourOfDestinyButton.gameObject.SetActive(false);
        }
        public void LoadEngagementPanelFromTown()
        {
            garrisonFight = true;
            string townGarrisonLocalized = LocalizationManager.Instance.GetText("TownGarrison");
            enemyArmyText.text = townGarrisonLocalized;
            bool isNewGarrisonBattle = !campaignSaveManager.SaveData.battleCompleted;
            campaignSaveManager.StartGarrisonBattle();
            if (isNewGarrisonBattle)
                IAudioRequester.Instance.SetGarrisonBattleTheme((int)campaignSaveManager.SaveData.townData.townRace);
            IAudioRequester.Instance.PlaySFX(SFXData.SelectToBattle);
            _engagementRetryCount = 0; // fresh, player-driven entry - not a watchdog retry
            LoadEngagement();
        }
        public void LoadEngagementPanel(NodeType _nodeType)
        {
            garrisonFight = false;
            engagementType = _nodeType switch
            {
                NodeType.Skirmish => EngagementType.Skirmish,
                NodeType.Horde => EngagementType.Horde,
                _ => EngagementType.Skirmish,
            };
            enemyArmyText.text = LocalizationManager.Instance.GetText(engagementType.ToString());

            _engagementRetryCount = 0; // fresh, player-driven entry - not a watchdog retry
            LoadEngagement();
        }
        public async void LoadEngagement()
        {
            int runId = ++_engagementRunId;
            _engagementRunComplete = false;
            _showedEngagementResult = false;
            StartCoroutine(EngagementLoadWatchdog(runId));

            TurnOffAllOptionalRewards();
            recruitButtonHiddenByConscript = false;
            raiseDeadHiddenByRecruit = false;

            autoResolveButton.interactable = false;
            startBattleButton.interactable = false;
            autoResolveTooltipTrigger.enabled = false;
            startBattleTooltipTrigger.enabled = false;
            continueButton.enabled = true;

            battleOptionsCanvasGroup.CGDisable();
            claimConsumableButton.gameObject.SetActive(false);
            goldRewardButton.gameObject.SetActive(false);
            raiseDeadButton.gameObject.SetActive(false);
            recruitUnitButton.gameObject.SetActive(false);
            consumeSurvivorsButton.gameObject.SetActive(false);
            purgeTheBlightButton.gameObject.SetActive(false);
            autoresolveResultText.text = "";
            heavensongButton.gameObject.SetActive(false);

            StartCoroutine(CampaignManager.Instance.MapCamera.LerpFocusedOnNodeVolume(0.5f, 0.25f));
            continueButton.gameObject.SetActive(false);
            engagementPanelCanvasGroup.CGEnable();
            enemyArmyCanvasGroup.CGEnable();
            engagementPanelCanvasGroup.canvasGroup.interactable = false;
            openMMFPlayer.PlayFeedbacks();
            await Task.Delay(500);
            if (runId != _engagementRunId) return; // superseded by a watchdog retry while we waited

            // Debug.Log($"campaignSaveManager.SaveData.battleCompleted: {campaignSaveManager.SaveData.battleCompleted}");

            bool isPostBattleResult = campaignSaveManager.SaveData.battleCompleted;
            if (isPostBattleResult)
            {
                // Debug.Log($"battle completed");
                await LoadEnemyCompany(true, runId);
                if (runId != _engagementRunId) return; // superseded while loading enemy cards
                ShowEngagementResult(runId);
            }
            else
            {
                int heroID = campaignSaveManager.SaveData.heroID;
                Race heroRace = HeroData.GetRaceFromHero(heroID);
                Race race = TabletopTavernData.Instance.GenerateRaceForMap(campaignSaveManager.SaveData.bookNumber, campaignSaveManager.SaveData.seed, heroRace);

                //get race strength
                // RaceStrengthTier raceStrengthTier = TabletopTavernData.Instance.GetRaceData(race).RaceStrengthTier;

                //check here to get race
                List<UnitTier> unitsPool = TabletopTavernData.Instance.GetSquadsWithTiersFromRace(race);

                async Task GenerateEnemyArmy()
                {
                    SquadToLoad[] enemyArmy;
                    if (garrisonFight)
                    {
                        enemyArmy = campaignSaveManager.SaveData.townData.townGarrisonUnits;
                    }
                    else
                    {
                        int battlesFought = campaignSaveManager.SaveData.BattlesFought;
                        Debug.Log($"[battle generation] battles fought: {battlesFought}");

                        //DifficultyMod 7
                        if (campaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Squire)
                        {
                            battlesFought += 1;
                        }

                        //DifficultyMod 19
                        if (campaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Godking)
                        {
                            battlesFought += 1;
                        }

                        //DifficultyMod 10
                        bool enemyPrestigeEligible = campaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Duke;
                        //DifficultyMod 14
                        bool enemyPrestigeEnhanced = campaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Emperor;

                        enemyArmy = ArmyCreator.GenerateEnemyArmy(
                            campaignSaveManager.SaveData.bookNumber,
                            battlesFought,
                            campaignSaveManager.GetSeededRandom(),
                            engagementType == EngagementType.Horde,
                            unitsPool,
                            //DifficultyMod 6
                            campaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Knight,
                            enemyPrestigeEligible,
                            enemyPrestigeEnhanced
                        );

                        if (CampaignManager.Instance.GearManager.CheckForGear(GearID.BearSpray))
                        {
                            enemyArmy = ArmyCreator.ReplaceMonsterUnits(enemyArmy, campaignSaveManager.GetSeededRandom(), unitsPool);
                        }
                    }
                    
                    if (!garrisonFight
                        && campaignSaveManager.SaveData.bookNumber == 1
                        && heroRace == Race.Gruntkin
                        && engagementType != EngagementType.Horde
                        && enemyArmy.Length > 1)
                    {
                        enemyArmy = enemyArmy.Take(enemyArmy.Length - 1).ToArray();
                    }

                    campaignSaveManager.SaveEnemyArmy(enemyArmy);
                    await LoadEnemyCompany(true, runId);
                    autoResolveBattleManager.Load(garrisonFight);
                }

                void GenerateBattlefield()
                {
                    System.Random engagementRandom = campaignSaveManager.GetCampaignRandom();
                    MapRegion mapRegion = MapThemeManager.Instance.GetMapRegion(race);
                    int campaignSeed = CampaignManager.Instance.CampaignSaveManager.SaveData.seed;
                    int bookNum = CampaignManager.Instance.CampaignSaveManager.SaveData.bookNumber;
                    MapNodeData nodeData = CampaignManager.Instance.MapSceneUIManager.MapSceneManager.SelectedNodeData;
                    Weather weather = CampaignSaveManager.GenerateNodeWeather(nodeData.index, campaignSeed, bookNum, mapRegion);
                    Biome biome = CampaignSaveManager.GenerateNodeBiome(nodeData.index, campaignSeed, bookNum, mapRegion);
                    if (garrisonFight) biome = Biome.Plains;

                    // Debug.Log($"Selected Biome: {biome}, Weather: {weather}");

                    // override weather if player has specific gear
                    if (weather == Weather.Rain && CampaignManager.Instance.GearManager.CheckForGear(GearID.BraceletoftheSunGoddess)) weather = Weather.ClearSkies;

                    string localizedWeather = LocalizationManager.Instance.GetText(weather.ToString());
                    battlefieldWeatherText.SetText(localizedWeather);

                    battlefieldWeatherTooltip.gameObject.SetActive(weather != Weather.ClearSkies);
                    string localizedDescription = LocalizationManager.Instance.GetText(weather.ToString() + "Desc");
                    string tooltipMessage = weather == Weather.ClearSkies ? "" : localizedDescription;
                    battlefieldWeatherTooltip.SetUpToolTip(localizedWeather, tooltipMessage);

                    if (weather == Weather.Rain)
                        TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.RainWeather });

                    // Display only - the preset still stores Biome.Plains. A garrison is a walled fight
                    // rather than one of the four biomes, and calling it Plains told the player nothing.
                    battlefieldBiomeText.text = LocalizationManager.Instance.GetText(garrisonFight ? "Garrison" : biome.ToString());

                    campaignSaveManager.SaveBattlefieldPreset(new BattleFieldPreset()
                    {
                        mapRegion = mapRegion,
                        race = race,
                        biome = biome,
                        weather = weather,
                        timeOfDay = BattleFieldPreset.TimeOfDay.Noon,
                        seed = campaignSaveManager.SaveData.seed,
                        useRandomSeed = false
                    });
                }

                battleOptionsCanvasGroup.FadeInAsync();
                postBattleChoicesCanvasGroup.CGDisable();
                endBattlePanel.SetActive(false);
                runLostCanvasGroup.CGDisable();
                GenerateBattlefield();
                await GenerateEnemyArmy();
                if (runId != _engagementRunId) return; // superseded by a watchdog retry
                bool isTaelindorHero = heroID == 7 || heroID == 8;
                heavensongButton.gameObject.SetActive(isTaelindorHero);
                if(isTaelindorHero)
                {
                    MapRegion region = campaignSaveManager.SaveData.battleFieldPreset.mapRegion;
                    _cachedWeatherNames = new Dictionary<Weather, string>();
                    foreach (var w in region.possibleWeathers)
                        _cachedWeatherNames[w.weather] = LocalizationManager.Instance.GetText(w.weather.ToString());
                }
            }
            if(campaignSaveManager.SaveData != null)
            {
                if (campaignSaveManager.SaveData.difficultyLevel < TT_Difficulty.Squire)
                {
                    autoResolvePreview.CheckIfMouseOverTooltip();
                }
            }
            
            autoResolveButton.interactable = true;
            startBattleButton.interactable = true;
            autoResolveTooltipTrigger.enabled = true;
            startBattleTooltipTrigger.enabled = true;
            startBattleTooltipTrigger.CheckIfMouseOverTooltip();
            autoResolveTooltipTrigger.CheckIfMouseOverTooltip();

            engagementPanelCanvasGroup.canvasGroup.interactable = true;

            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.Autoresolve});

            // Post-battle results finish asynchronously inside ShowEngagementResult, which signals
            // completion itself; only the fresh pre-battle setup path is fully done right here.
            if (!isPostBattleResult && runId == _engagementRunId)
                _engagementRunComplete = true;
        }
        // Watches a single LoadEngagement attempt (identified by runId). If it hasn't finished within
        // ENGAGEMENT_WATCHDOG_TIMEOUT real seconds, that attempt is treated as stalled (this is what was
        // silently happening after an alt-tab during the post-battle map reload: the panel stayed visible
        // but permanently locked, with no exception ever logged) and we retry from scratch. WaitForSecondsRealtime
        // ignores timeScale so this still fires even if something upstream paused the game.
        private System.Collections.IEnumerator EngagementLoadWatchdog(int runId)
        {
            yield return new WaitForSecondsRealtime(ENGAGEMENT_WATCHDOG_TIMEOUT);

            if (runId != _engagementRunId) yield break; // superseded already

            if (_engagementRunComplete)
            {
                VerifyEngagementResultVisible();
                yield break;
            }

            Debug.LogError($"[EngagementPanel] LoadEngagement (runId {runId}) did not complete within {ENGAGEMENT_WATCHDOG_TIMEOUT}s - treating as stalled.");

            if (_engagementRetryCount < ENGAGEMENT_MAX_RETRIES)
            {
                _engagementRetryCount++;
                Debug.LogError($"[EngagementPanel] Retrying LoadEngagement (attempt {_engagementRetryCount}/{ENGAGEMENT_MAX_RETRIES}).");
                LoadEngagement(); // bumps _engagementRunId, so the stuck attempt above is a no-op if it ever wakes up
            }
            else
            {
                Debug.LogError("[EngagementPanel] LoadEngagement still stuck after max retries - force-unlocking panel so the player isn't stranded.");
                _engagementRunId++; // invalidate the stuck attempt permanently
                isLoadingEnemyCompany = false; // don't leave OnArmyStructureChanged permanently blocked
                engagementPanelCanvasGroup.canvasGroup.interactable = true;
                continueButton.gameObject.SetActive(true);
                continueButton.enabled = true;
            }
        }
        // Companion check to the stall handling above, for the case where the run *did* complete but the
        // results screen came up invisible (see _showedEngagementResult). Alpha 0 with the results still
        // up is never a legitimate state, so snap the group visible rather than leaving the player with a
        // screen whose only responsive element is the enemy cards.
        private void VerifyEngagementResultVisible()
        {
            if (!_showedEngagementResult) return;

            // Won runs put the action buttons on endBattleCanvasGroup; lost runs put them on runLostCanvasGroup.
            MemoriCanvasGroup group = campaignSaveManager.SaveData.playerWonBattle ? endBattleCanvasGroup : runLostCanvasGroup;
            if (group == null || group.canvasGroup == null) return;
            if (group.canvasGroup.alpha > 0f) return;

            Debug.LogError($"[EngagementPanel] Engagement result is on screen but {group.name} is at alpha 0 - forcing it visible so the player isn't stranded.");
            group.CGEnable();
        }
        public async Task LoadEnemyCompany(bool _playfeedbacks, int runId)
        {
            isLoadingEnemyCompany = true;
            foreach (Transform child in enemyArmyParent) {
                Destroy(child.gameObject);
            }

            enemySquadsCards = new ();
            if (campaignSaveManager.SaveData.enemyArmy == null || campaignSaveManager.SaveData.enemyArmy.Length == 0) return;
            foreach (SquadToLoad squad in campaignSaveManager.SaveData.enemyArmy)
            {
                // Debug.Log($"squad: {squad.UnitName} - {squad.currentUnitCount}");
                SquadDisplayCardMenu squadDisplayCardMenu = Instantiate(squadDisplayCardMenuPrefab, enemyArmyParent);
                // Debug.Log($"loading enemy squad with current unit count: {squad.currentUnitCount}");
                if (campaignSaveManager.SaveData.playerWonBattle && campaignSaveManager.SaveData.battleCompleted)
                {
                    SquadToLoad squadModified = new(squad.UnitName, _modifiedHealthValueByAmount: 0) { UniqueID = squad.UniqueID };
                    squadDisplayCardMenu.SetUp(squadModified, false, mapSceneUIManager.HUDPanel, true);
                }
                else
                {
                    squadDisplayCardMenu.SetUp(squad, false, mapSceneUIManager.HUDPanel, true);
                }

                enemySquadsCards.Add(squadDisplayCardMenu);
                if (_playfeedbacks)
                {
                    squadDisplayCardMenu.SpawnInJuice(false);
                    await Task.Delay(100);
                    if (runId != _engagementRunId)
                    {
                        isLoadingEnemyCompany = false; // don't leave OnArmyStructureChanged permanently blocked
                        return;
                    }
                }
            }
            foreach (SquadDisplayCardMenu squad in enemySquadsCards)
            {
                squad.MakeInteractable(true);
            }
            isLoadingEnemyCompany = false;
        }
        #endregion
        
        #region Pre Battle
        public void AutoResolveButtonClicked()
        {
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.Autoresolve);

            autoResolved = true;
            CampaignManager.Instance.MapSceneUIManager.MapSceneManager.OverrideSelectedNodeBeforeBattle();
            autoResolveBattleManager.AutoResolve();

            ShowEngagementResult(++_engagementRunId);
        }
        public void StartBattleButtonClicked()
        {
            if (campaignSaveManager.SaveData.battleCompleted) return;

            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.Autoresolve);

            autoResolved = false;
            startBattleTooltipTrigger.OnPointerExit(null);
            //need to override the selected node here
            CampaignManager.Instance.MapSceneUIManager.MapSceneManager.OverrideSelectedNodeBeforeBattle();
            campaignSaveManager.SaveCampaign();
            mapSceneUIManager.StartBattleButtonClicked();
        }
        public void ShowAutoResolvePrediction()
        {
            SquadToLoad[] predictedPlayerSquads = autoResolveBattleManager.PredictedPlayerArmy;
            foreach (SquadDisplayCardMenu squadDisplayCardMenu in mapSceneUIManager.HUDPanel.PlayerSquadsCards) {
                for(int i = 0; i < predictedPlayerSquads.Length; i++) {
                    if(squadDisplayCardMenu.UniqueID == predictedPlayerSquads[i].UniqueID) {
                        squadDisplayCardMenu.ShowPotentialHealthLoss(predictedPlayerSquads[i]);
                    }
                }
                if(squadDisplayCardMenu.InReserve && !garrisonFight) {
                    squadDisplayCardMenu.ShowPotentialHealthRecovery();
                }
            }
        }
        public void HideAutoResolvePrediction()
        {
            foreach (SquadDisplayCardMenu squadDisplayCardMenu in mapSceneUIManager.HUDPanel.PlayerSquadsCards) {
                squadDisplayCardMenu.HidePotentialHealthLoss();
                squadDisplayCardMenu.HidePotentialHealthRecovery();
            }
        }
        public void OnArmyStructureChanged()
        {
            if (isLoadingEnemyCompany)
            {
                // Debug.Log("[EngagementPanel] OnArmyStructureChanged blocked — enemy company is still loading.");
                return;
            }
            if (campaignSaveManager.SaveData.battleCompleted)
                return;
            autoResolveBattleManager.Load(garrisonFight);
        }
        private void HeavensongButtonClicked()
        {
            Weather currentWeather = campaignSaveManager.SaveData.battleFieldPreset.weather;
            MapRegion mapRegion = campaignSaveManager.SaveData.battleFieldPreset.mapRegion;
            bool blockRain = CampaignManager.Instance.GearManager.CheckForGear(GearID.BraceletoftheSunGoddess);
            var candidates = mapRegion.possibleWeathers.Where(w => w.weather != currentWeather && !(blockRain && w.weather == Weather.Rain)).ToList();
            if (candidates.Count == 0) return;

            heavensongButton.gameObject.SetActive(false);

            float total = candidates.Sum(w => w.likelihood);
            float roll = UnityEngine.Random.Range(0f, total);
            float cumulative = 0f;
            Weather newWeather = candidates[candidates.Count - 1].weather;
            foreach (var c in candidates)
            {
                cumulative += c.likelihood;
                if (roll <= cumulative) { newWeather = c.weather; break; }
            }
            StartCoroutine(RollWeatherText(newWeather, mapRegion, currentWeather));
        }
        private System.Collections.IEnumerator RollWeatherText(Weather finalWeather, MapRegion mapRegion, Weather excludeWeather)
        {
            bool blockRain = CampaignManager.Instance.GearManager.CheckForGear(GearID.BraceletoftheSunGoddess);
            var possibleWeathers = mapRegion.possibleWeathers.Where(w => w.weather != excludeWeather && !(blockRain && w.weather == Weather.Rain)).ToList();
            float elapsed = 0f;
            float duration = 0.35f;
            float interval = 0.035f;
            int cycleIndex = 0;
            IAudioRequester.Instance.PlaySFX(SFXData.Reroll);

            while (elapsed < duration)
            {
                battlefieldWeatherText.Target.text = _cachedWeatherNames[possibleWeathers[cycleIndex % possibleWeathers.Count].weather];
                cycleIndex++;
                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }

            string localizedWeather = _cachedWeatherNames[finalWeather];
            battlefieldWeatherText.SetText(localizedWeather);
            battlefieldWeatherTooltip.gameObject.SetActive(finalWeather != Weather.ClearSkies);
            if (finalWeather != Weather.ClearSkies)
            {
                string localizedDescription = LocalizationManager.Instance.GetText(finalWeather.ToString() + "Desc");
                battlefieldWeatherTooltip.SetUpToolTip(localizedWeather, localizedDescription);
            }

            BattleFieldPreset preset = campaignSaveManager.SaveData.battleFieldPreset;
            preset.weather = finalWeather;
            campaignSaveManager.SaveBattlefieldPreset(preset);
            mapSceneUIManager.HUDPanel.ShowWeatherHover(finalWeather, finalWeather != Weather.ClearSkies);
            IAudioRequester.Instance.PlaySFX(SFXData.TinyClick);

            if (finalWeather == Weather.Rain)
                TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.RainWeather });
        }

        public void AlertOfBattleResults(bool playerWon)
        {
            if (campaignSaveManager.SaveData.difficultyLevel == TT_Difficulty.Godking) return;

            string autoResolveResultLocalized = LocalizationManager.Instance.GetText("Autoresolve Result") + $": <color={(playerWon ? ColorData.Green : ColorData.Error)}>" + (playerWon ? LocalizationManager.Instance.GetText("Victory") : LocalizationManager.Instance.GetText("Defeat")) + "</color>";
            autoresolveResultText.text = autoResolveResultLocalized;
        }
        #endregion

        #region Post Battle
        public void GenerateBattleRewards()
        {
            //gold
            goldRewardAmount = engagementType == EngagementType.Skirmish ? TabletopTavernConstants.GetSkirmishReward() : TabletopTavernConstants.GetHordeReward();

            if(campaignSaveManager.SaveData.BattlesFought < 3)
            {
                //same reward for first 3 battles to help player get started
            }
            else if (campaignSaveManager.SaveData.BattlesFought < 6)
            {
                goldRewardAmount += 2;
            }
            else if (campaignSaveManager.SaveData.BattlesFought < 9)
            {
                goldRewardAmount += 4;
            }
            else
            {
                goldRewardAmount += 6;
            }

            //consumable
            // Both the drop roll and the pick come off the campaign seed, so re-opening the results panel
            // (exiting to the main menu and back) can't reroll the consumable reward.
            System.Random rewardRandom = campaignSaveManager.GetCampaignRandom();
            generateConsumable = rewardRandom.Next(0, 100) < CampaignManager.Instance.GoldManager.PotionRewardsOdds;

            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_postBattleConsumableMetaprogressionModel)) {
                generateConsumable = true;
            }

            if (generateConsumable)
            {
                bool hasLuckyHorseshoe = CampaignManager.Instance.GearManager.CheckForGear(GearID.LuckyHorseshoe);
                int bookNumber = campaignSaveManager.SaveData.bookNumber;
                consumableEnum = ConsumableData.GetWeightedConsumable(bookNumber, rewardRandom, hasLuckyHorseshoe);
                Consumable consumableData = ConsumableData.GetConsumable(consumableEnum);
                string consumableNameLocalized = LocalizationManager.Instance.GetText(consumableData.ConsumableEnum.ToString() + "Name");
                consumableText.text = consumableNameLocalized;
                consumableIcon.sprite = SpriteData.GetSprite(consumableEnum.ToString());
                
                string consumableDescriptionLocalized = CampaignManager.Instance.ConsumableManager.GetConsumableDescription(consumableData.ConsumableEnum);
                consumableTooltip.SetUpToolTip(consumableNameLocalized, consumableDescriptionLocalized);
            }

            //ransom captives
            campaignSaveManager.RegisterRansomOffered();
            ransomAmount = TabletopTavernConstants.GetRansomCaptivesReward();

            if(campaignSaveManager.SaveData.BattlesFought < 3)
            {
                //same reward for first 3 battles to help player get started
            }
            else if (campaignSaveManager.SaveData.BattlesFought < 6)
            {
                ransomAmount += 2;
            }
            else if (campaignSaveManager.SaveData.BattlesFought < 9)
            {
                ransomAmount += 4;
            }
            else
            {
                ransomAmount += 6;
            }

            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_postBattleGoldMetaprogressionModel)) {
                ransomAmount += _postBattleGoldMetaprogressionModel.NodeValue;
            }

            //DifficultyMod 15
            if (campaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Imperator)
            {
                ransomAmount -= 1;
            }

            //The Skull Harvest: +2 Gold from battle rewards
            if(HeroBonusManager.Instance.ActiveHeroID == 5) {
                ransomAmount += 2;
            }           

            //conscript survivors
            if (campaignSaveManager.SaveData.enemyArmy == null || campaignSaveManager.SaveData.enemyArmy.Length == 0) return;
            int maxTier = 0; //get highest tier unit in enemy army
            foreach (SquadToLoad squad in campaignSaveManager.SaveData.enemyArmy) {
                int tier = TabletopTavernData.Instance.GetUnitTierFromUnitName(squad.UnitName);
                if(tier > maxTier) {
                    maxTier = tier;
                }
            }
            SquadToLoad squadToConscript = new (
                campaignSaveManager.SaveData.enemyArmy[0].UnitName
            );
            for(int i = 0; i < campaignSaveManager.SaveData.enemyArmy.Length; i++) 
            {
                //get random unit from enemy army with same tier
                if(TabletopTavernData.Instance.GetUnitTierFromUnitName(campaignSaveManager.SaveData.enemyArmy[i].UnitName) == maxTier) {
                    float conscriptedHealth = CampaignManager.Instance.CampaignSaveManager.CheckForGear(GearID.RiverTrout) ? 1 : TabletopTavernConstants.CONSCRIPT_SURVIVORS_HEALTH_PERCENTAGE;
                    if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_postBattleRecruitMetaprogressionModel)) {
                        conscriptedHealth *= 2;
                    }
                    //clamp to 1
                    conscriptedHealth = Mathf.Clamp(conscriptedHealth, 0, 1);
                    squadToConscript = new (
                        campaignSaveManager.SaveData.enemyArmy[i].UnitName, 
                        _modifiedHealthValueByAmount: conscriptedHealth
                    );
                    break;
                }
            }

            //get 3 random units from enemy army
            UnityEngine.Random.InitState(campaignSaveManager.SaveData.seed);
            var shuffled = campaignSaveManager.SaveData.enemyArmy.OrderBy(_ => UnityEngine.Random.value).ToList();
            var selected = shuffled.Take(Mathf.Min(3, shuffled.Count)).ToList();
            conscriptedUnitNames = selected.Select(s => s.UnitName).ToArray();

            string conscriptSurvivorsTitleLocalized = LocalizationManager.Instance.GetText("ConscriptSurvivorsTitle");
            string conscriptSurvivorsDescLocalized = LocalizationManager.Instance.GetText("ConscriptSurvivorsDesc");
            // add all unit names to description
            for(int i = 0; i < campaignSaveManager.SaveData.enemyArmy.Length; i++) {
                int tier = TabletopTavernData.Instance.GetUnitTierFromUnitName(campaignSaveManager.SaveData.enemyArmy[i].UnitName);
                string unitNameLocalized = LocalizationManager.Instance.GetText(campaignSaveManager.SaveData.enemyArmy[i].UnitName.ToString());
                conscriptSurvivorsDescLocalized += $"\n- <color={ColorData.GetRarityTierColorString((UnitRarity)(tier-1))}>" + MemoriUI.AddSpacesToSentence(unitNameLocalized) + "</color>";
            }
            conscriptSurvivorsTooltip.SetUpToolTip(conscriptSurvivorsTitleLocalized, conscriptSurvivorsDescLocalized);

            // squadToConscriptName = squadToConscript.UnitName;
            squadDisplayCardConscript.enabled = true;
            squadDisplayCardConscript.SetUp(squadToConscript, false, mapSceneUIManager.HUDPanel, true);
            squadDisplayCardConscript.enabled = false;
            conscriptSurvivorsImage.sprite = conscriptSurvivorsImageSprite;
            
            //rest army
            // restArmyHealAmount = TabletopTavernConstants.REST_ARMY_HEAL_AMOUNT;
            // if (campaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Imperator)
            // {
            //     restArmyHealAmount *= 0.5f;
            // }

            // if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_postBattleHealthMetaprogressionModel)) {
            //     restArmyHealAmount *= _postBattleHealthMetaprogressionModel.NodeValue;
            // }
            recruitsRarity = campaignSaveManager.SaveData.bookNumber == 1 ? UnitRarity.Common : UnitRarity.Uncommon;
            if(engagementType == EngagementType.Horde) {
                recruitsRarity = campaignSaveManager.SaveData.bookNumber == 1 ? UnitRarity.Uncommon : UnitRarity.Rare;
            }
            Color color = ColorData.GetRarityTierColor(recruitsRarity);
            recruitUnitIcon.color = color;
            string text = LocalizationManager.Instance.GetText("Recruit") + LocalizationManager.Instance.GetText(recruitsRarity.ToString()) +  LocalizationManager.Instance.GetText("Unit");
            ColorData.XMLTagColorApplicator(ref text);
            recruitUnitText.text = text;

            recruitUnitTooltip.SetUpToolTip(
                LocalizationManager.Instance.GetText("Recruit Units"),
                LocalizationManager.Instance.GetText("Recruit Unit Reward Desc")
            );
        }
        public void DisplayBattleRewards()
        {
            campaignSaveManager.RemoveZeroHealthSquads();
            ransomAmountText.text = "+ "+ ransomAmount.ToString();
            // restArmyText.text = $"+{restArmyHealAmount * 100}% " + LocalizationManager.Instance.GetText("UnitHealth");
            goldRewardText.text = LocalizationManager.Instance.GetText("Claim Bounty") + "  ( + " + goldRewardAmount.ToString() + " )";

            foreach (Transform child in enemyArmyParent) {
                Destroy(child.gameObject);
            }

            postBattleChoicesCanvasGroup.FadeInAsync();

            goldRewardButton.gameObject.SetActive(true);
            claimConsumableButton.gameObject.SetActive(generateConsumable);
            squadDisplayCardConscript.gameObject.SetActive(true);
            recruitUnitButton.gameObject.SetActive(true);
            continueButton.gameObject.SetActive(true);
            postBattleChoicesClaimed = false;
        }
        private async void ShowEngagementResult(int runId)
        {
            if(campaignSaveManager.SaveData.townData.townInteractionStatus == TownInteractionStatus.GarrisonBattleStarted) {
                campaignSaveManager.MarkGarrisonBattleComplete();

                garrisonFight = true;
            }

            await LoadEnemyCompany(false, runId);
            if (runId != _engagementRunId) return; // superseded by a watchdog retry

            int squadCount = 0;
            for(int i = 0; i < CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy.Length; i++) {
                if(CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy[i].UnitIndex != -1) {
                    squadCount++;
                }
            }

            battleOptionsCanvasGroup.CGDisable();
            campaignSaveManager.CorrectHealthOfWithdrawnSquads();

            bool halfHealthHealing = false;
            //DifficultyMod 17
            if (campaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Overlord)
            {
                halfHealthHealing = true;
            }

            ShowUnitsSlain();
            
            if(!garrisonFight)
            {
                campaignSaveManager.HealTroopsInReserve(halfHealthHealing);
            }          
            else
            {
                campaignSaveManager.NonHealReserves();

                //Endless Hordes
                if(HeroBonusManager.Instance.ActiveHeroID == 3|| HeroBonusManager.Instance.ActiveHeroID == 4)
                    campaignSaveManager.ModifyGruntkinTroopHealth(TabletopTavernConstants.ENDLESS_HORDES_HEAL_AMOUNT);
            }

            HideAutoResolvePrediction();
            campaignSaveManager.PrestigeUnitsOnKills();
            
            string townGarrisonLocalized = LocalizationManager.Instance.GetText("TownGarrison");
            string companyShatteredLocalized = LocalizationManager.Instance.GetText("CompanyShattered");
            string enemyHostLocalized = LocalizationManager.Instance.GetText("EnemyHost");
            string victoryLocalized = LocalizationManager.Instance.GetText("Victory");
            string defeatLocalized = LocalizationManager.Instance.GetText("Defeat");
            string defeatedLocalized = LocalizationManager.Instance.GetText("Defeated");

            bool battleWon = campaignSaveManager.SaveData.playerWonBattle;
            if(garrisonFight)
            {
                battleOutcomeText.text = battleWon ? townGarrisonLocalized + " " + defeatedLocalized : companyShatteredLocalized;
                if(battleWon) {
                    lootTownButton.gameObject.SetActive(true);
                    mapSceneUIManager.HUDPanel.ShowConsumablesBlocker();
                } else {
                    runLostCanvasGroup.FadeInAsync();
                    mapSceneUIManager.GameOverPanel.RecordGameOver(false);
                }
            } else {
                battleOutcomeText.text = battleWon ? enemyHostLocalized + " " + defeatedLocalized : companyShatteredLocalized;
                if(battleWon) {
                    if(mapSceneUIManager.MapSceneManager.WillCompleteLayerEndInGameOver()) {
                        continueButton.gameObject.SetActive(true);
                    }
                    else {
                        GenerateBattleRewards();
                        claimRewardsButton.gameObject.SetActive(true);
                        mapSceneUIManager.HUDPanel.ShowConsumablesBlocker();
                    }
                } else {
                    runLostCanvasGroup.FadeInAsync();
                    mapSceneUIManager.GameOverPanel.RecordGameOver(false);
                    claimRewardsButton.gameObject.SetActive(false);
                }
            }

            battleVictoryOrDefeatText.text = battleWon ? victoryLocalized : defeatLocalized;
            endBattlePanel.SetActive(true);
            IAudioRequester.Instance.PlaySFX(battleWon ? SFXData.Cheer : SFXData.Boo);
            IAudioRequester.Instance.PlaySFX(battleWon ? SFXData.BattleWin : SFXData.BattleLoss);
            if(battleWon)
                IAudioRequester.Instance.PlaySFX(SFXData.Trumpet);

            // Snap rather than fade: this group carries the only actionable buttons on the results screen
            // (Claim Rewards / Continue / Loot Town), and FadeInAsync sets blocksRaycasts up front while
            // lerping alpha across awaited frames. If that continuation is ever dropped the player is left
            // staring at an invisible screen that still eats clicks. endBattlePanel's Animator still juices.
            endBattleCanvasGroup.CGEnable();
            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.HealthRecovery });
            HideEndBattlePanel();

            _showedEngagementResult = true;
            if (runId == _engagementRunId)
                _engagementRunComplete = true;
        }
        public void ShowUnitsSlain()
        {
            if(autoResolved)
            {
                // UnitsAlive is ceiling-rounded from pooled health (see AutoResolveBattleManager), so it can
                // read as 1 even when only a sliver of a unit's health is left - which undercounts losses by
                // 1 in that case. Floor-dividing the same finalHealth avoids that without touching the sim's
                // own UnitsAlive value (still used elsewhere for targeting/combat resolution).
                int EndingUnits(AutoResolveSquad squad) => squad.healthPerKill > 0 ? squad.finalHealth / squad.healthPerKill : squad.UnitsAlive;

                AutoResolveSquad[] aRSS = autoResolveBattleManager.PlayerAutoResolveStats;
                foreach (SquadDisplayCardMenu squadDisplayCardMenu in mapSceneUIManager.HUDPanel.PlayerSquadsCards) {
                    for(int i = 0; i < aRSS.Length; i++) {
                        if (squadDisplayCardMenu.UniqueID == aRSS[i].UniqueID) {
                            squadDisplayCardMenu.ShowUnitsSlain(aRSS[i].UnitsSlain);
                            squadDisplayCardMenu.ShowUnitsLost(Mathf.Max(0, aRSS[i].maxUnits - EndingUnits(aRSS[i])));
                        }
                    }
                }

                aRSS = autoResolveBattleManager.EnemyAutoResolveStats;
                foreach (SquadDisplayCardMenu squadDisplayCardMenu in enemySquadsCards) {
                    for(int i = 0; i < aRSS.Length; i++) {
                        if(squadDisplayCardMenu.UniqueID == aRSS[i].UniqueID) {
                            squadDisplayCardMenu.ShowUnitsSlain(aRSS[i].UnitsSlain);
                            squadDisplayCardMenu.ShowUnitsLost(Mathf.Max(0, aRSS[i].maxUnits - EndingUnits(aRSS[i])));
                        }
                    }
                }
            }
            else
            {
                List<SquadKillsStored> squadKillsStore = campaignSaveManager.GetSquadIdKillCounter();
                List<SquadLossesStored> squadLossesStore = campaignSaveManager.GetSquadIdLossCounter();
                foreach (SquadDisplayCardMenu squadDisplayCardMenu in mapSceneUIManager.HUDPanel.PlayerSquadsCards) {
                    squadKillsStore.ForEach(squadKillsStored => {
                        if(squadDisplayCardMenu.UniqueID == squadKillsStored.SquadGUID) {
                            squadDisplayCardMenu.ShowUnitsSlain(squadKillsStored.Kills);
                        }
                    });
                    squadLossesStore.ForEach(squadLossesStored => {
                        if(squadDisplayCardMenu.UniqueID == squadLossesStored.SquadGUID) {
                            squadDisplayCardMenu.ShowUnitsLost(squadLossesStored.Losses);
                        }
                    });
                }
                foreach (SquadDisplayCardMenu squadDisplayCardMenu in enemySquadsCards) {
                    squadKillsStore.ForEach(squadKillsStored => {
                        if(squadDisplayCardMenu.UniqueID == squadKillsStored.SquadGUID) {
                            squadDisplayCardMenu.ShowUnitsSlain(squadKillsStored.Kills);
                        }
                    });
                    squadLossesStore.ForEach(squadLossesStored => {
                        if(squadDisplayCardMenu.UniqueID == squadLossesStored.SquadGUID) {
                            squadDisplayCardMenu.ShowUnitsLost(squadLossesStored.Losses);
                        }
                    });
                }
            }
        }
        public void HideUnitsSlain()
        {
            foreach (SquadDisplayCardMenu squadDisplayCardMenu in mapSceneUIManager.HUDPanel.PlayerSquadsCards) {
                squadDisplayCardMenu.HideUnitsSlain();
                squadDisplayCardMenu.HideUnitsLost();
            }
            foreach (SquadDisplayCardMenu squadDisplayCardMenu in enemySquadsCards) {
                squadDisplayCardMenu.HideUnitsSlain();
                squadDisplayCardMenu.HideUnitsLost();
            }
        }
        #endregion

        #region Post Battle Options
        public void ClaimConsumableButtonClicked()
        {
            if(campaignSaveManager.HasRoomForConsumable()) {
                campaignSaveManager.AquireConsumable(consumableEnum);
            } else {
                string noRoomLocalized = LocalizationManager.Instance.GetText("NoRoomForConsumable");
                NotificationManager.Instance.ErrorNotification(noRoomLocalized);
                return;
            }
            claimConsumableButton.gameObject.SetActive(false);
            claimConsumableButton.GetComponent<MemoriButtonV2>().OnPointerExit(null);
            ForceContinueIfAllRewardsClaimed();
        }
        public void ClaimGoldRewardButtonClicked()
        {
            string goldRewardLocalized = LocalizationManager.Instance.GetText("Loot Gold");
            CampaignManager.Instance.GoldManager.ModifyGold(goldRewardAmount, goldRewardLocalized);
            goldRewardButton.gameObject.SetActive(false);
            goldRewardButton.GetComponent<MemoriButtonV2>().OnPointerExit(null);
            ForceContinueIfAllRewardsClaimed();
        }
        public void ClaimRecruitUnitButtonClicked()
        {
            IAudioRequester.Instance.PlaySFX(SFXData.FocusNode);
            recruitPanel.LoadRecruitPanelFromBattle(HeroData.GetRaceFromHero(campaignSaveManager.SaveData.heroID), recruitsRarity);
            recruitUnitButton.gameObject.SetActive(false);
            recruitUnitButton.GetComponent<MemoriButtonV2>().OnPointerExit(null);
            conscriptSurvivorsButton.gameObject.SetActive(false);
            conscriptSurvivorsButtonScript.enabled = false;
            if (raiseDeadButton.gameObject.activeSelf)
            {
                raiseDeadHiddenByRecruit = true;
                raiseDeadButton.enabled = false;
                raiseDeadButton.gameObject.SetActive(false);
                raiseDeadCard1.gameObject.SetActive(false);
                raiseDeadCard2.gameObject.SetActive(false);
                raiseDeadCard3.gameObject.SetActive(false);
            }
            HidePanel();
        }
        public void RansomCaptivesButtonClicked()
        {
            HidePostBattleOptionalRewards();
            campaignSaveManager.RegisterRansomChosen();
            string ransomLocalized = LocalizationManager.Instance.GetText("Ransom Captives");
            CampaignManager.Instance.GoldManager.ModifyGold(ransomAmount, ransomLocalized);
            ForceContinueIfAllRewardsClaimed();
        }
        public void ConscriptSurvivorsButtonClicked()
        {
            HidePostBattleOptionalRewards();
            if (recruitUnitButton.gameObject.activeSelf)
            {
                recruitButtonHiddenByConscript = true;
                recruitUnitButton.gameObject.SetActive(false);
                recruitUnitButton.GetComponent<MemoriButtonV2>().OnPointerExit(null);
            }
            recruitPanel.LoadRecruitPanelForConscript(conscriptedUnitNames);
            HidePanel();
        }
        public void HidePanel()
        {
            engagementPanelCanvasGroup.FadeOutAsync(0.25f);
        }
        public void ConsumeCaptivesButtonClicked()
        {
            HidePostBattleOptionalRewards();
            campaignSaveManager.ModifyTroopHealth(TabletopTavernConstants.CONSUME_CAPTIVES_HEAL_AMOUNT, Race.DrakosaurBrood);
            ForceContinueIfAllRewardsClaimed();
        }
        public void PurgeTheBlightButtonClicked()
        {
            HidePostBattleOptionalRewards();
            CampaignManager.Instance.CampaignSaveManager.HealRandomUnitToFull();
            ForceContinueIfAllRewardsClaimed();
        }
        public void ForbiddenRitualsButtonClicked()
        {
            if(!campaignSaveManager.HasRoomForConsumable())
            {
                string noRoomLocalized = LocalizationManager.Instance.GetText("NoRoomForConsumable");
                NotificationManager.Instance.ErrorNotification(noRoomLocalized);
                return;
            }

            IAudioRequester.Instance.PlaySFX(SFXData.CollectItem);
            CampaignManager.Instance.CampaignSaveManager.AquireConsumable(_generatedConsumbale);
            HidePostBattleOptionalRewards();
            ForceContinueIfAllRewardsClaimed();
        }
        public void HourOfDestinyButtonClicked()
        {
            if(!CampaignManager.Instance.CampaignSaveManager.CheckForRoomToRecruit()) 
            {
                string errorLocalized = LocalizationManager.Instance.GetText("Max Units Recruited");
                NotificationManager.Instance.ErrorNotification(errorLocalized);
                return;
            }
            string hourOfDestinyLocalized = LocalizationManager.Instance.GetText("Hour of Destiny");
            CampaignManager.Instance.GoldManager.ModifyGold(-CampaignManager.Instance.GoldManager.CurrentGoldAmount, hourOfDestinyLocalized);

            UnitName[] unitNames = TabletopTavernData.Instance.GetSquadsToRecruitBasedOnReputation(0, 1, CampaignManager.Instance.CampaignSaveManager.GetSeededRandom(), CampaignManager.Instance.CampaignSaveManager.GetHeroID());
            CampaignManager.Instance.CampaignSaveManager.RecruitSquad(TabletopTavernData.Instance.GetSquadStats(unitNames[0]));
            IAudioRequester.Instance.PlaySFX(SFXData.RecruitUnit);

            HidePostBattleOptionalRewards();
            ForceContinueIfAllRewardsClaimed();
        }
        public void LootBattlefieldButtonClicked()
        {
            if(!campaignSaveManager.CanAquireGear())
            {
                string errorLocalized = LocalizationManager.Instance.GetText("No space for gear");
                NotificationManager.Instance.ErrorNotification(errorLocalized);
                return;
            }

            HidePostBattleOptionalRewards();
            CampaignManager.Instance.CampaignSaveManager.AquireGear(gearID);
            lootBattlefieldButton.gameObject.SetActive(false);
            ForceContinueIfAllRewardsClaimed();
        }
        public void RaiseDeadButtonClicked()
        {
            if (!campaignSaveManager.CheckForRoomToRecruit())
            {
                string notEnoughGoldLocalized = LocalizationManager.Instance.GetText("NoRoomForUnit");
                NotificationManager.Instance.ErrorNotification(notEnoughGoldLocalized);
                return;
            }

            foreach (UnitName unitName in raiseDeadUnitList)
            {
                if (!campaignSaveManager.CheckForRoomToRecruit()) break;
                SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(unitName);
                campaignSaveManager.RecruitSquad(squadStats, 1, _viaRaiseDead: true);
            }

            IAudioRequester.Instance.PlaySFX(SFXData.RecruitUnit);
            raiseDeadButton.enabled = false;
            raiseDeadCard1.gameObject.SetActive(false);
            raiseDeadCard2.gameObject.SetActive(false);
            raiseDeadCard3.gameObject.SetActive(false);
            HidePostBattleOptionalRewards();
        }
        public void ShowRaiseDeadButton()
        {
            raiseDeadButton.enabled = true;
            raiseDeadUnitList.Clear();

            System.Random random = campaignSaveManager.GetCampaignRandom();
            int bookNumber = campaignSaveManager.SaveData.bookNumber;

            if (bookNumber <= 1)
            {
                UnitName[] pool = new UnitName[] {
                    UnitName.BoneclatterSpears,
                    UnitName.UndeadLevies,
                    UnitName.GravestoneImps,
                    UnitName.FeralHounds
                };
                for (int i = 0; i < 3; i++)
                    raiseDeadUnitList.Add(pool[random.Next(pool.Length)]);

                raiseDeadCard1.gameObject.SetActive(true);
                raiseDeadCard2.gameObject.SetActive(true);
                raiseDeadCard3.gameObject.SetActive(true);
                raiseDeadCard1.SetUp(new SquadToLoad(raiseDeadUnitList[0]), false, mapSceneUIManager.HUDPanel, true);
                raiseDeadCard2.SetUp(new SquadToLoad(raiseDeadUnitList[1]), false, mapSceneUIManager.HUDPanel, true);
                raiseDeadCard3.SetUp(new SquadToLoad(raiseDeadUnitList[2]), false, mapSceneUIManager.HUDPanel, true);
            }
            else if (bookNumber == 2)
            {
                UnitName[] pool = new UnitName[] {
                    UnitName.DeathhavenFiends,
                    UnitName.Nightriders,
                    UnitName.BoneshardArchers,
                    UnitName.MistWraiths
                };
                for (int i = 0; i < 2; i++)
                    raiseDeadUnitList.Add(pool[random.Next(pool.Length)]);

                raiseDeadCard1.gameObject.SetActive(true);
                raiseDeadCard2.gameObject.SetActive(true);
                raiseDeadCard3.gameObject.SetActive(false);
                raiseDeadCard1.SetUp(new SquadToLoad(raiseDeadUnitList[0]), false, mapSceneUIManager.HUDPanel, true);
                raiseDeadCard2.SetUp(new SquadToLoad(raiseDeadUnitList[1]), false, mapSceneUIManager.HUDPanel, true);
            }
            else
            {
                UnitName[] pool = new UnitName[] {
                    UnitName.BlackWardens,
                    UnitName.Bloodsworn,
                    UnitName.CorpseClaws,
                    UnitName.BloodswornKnights
                };
                raiseDeadUnitList.Add(pool[random.Next(pool.Length)]);

                raiseDeadCard1.gameObject.SetActive(true);
                raiseDeadCard2.gameObject.SetActive(false);
                raiseDeadCard3.gameObject.SetActive(false);
                raiseDeadCard1.SetUp(new SquadToLoad(raiseDeadUnitList[0]), false, mapSceneUIManager.HUDPanel, true);
            }

            raiseDeadButton.gameObject.SetActive(true);
        }
        public void ShowConsumeCaptivesButton()
        {
            consumeSurvivorsButton.gameObject.SetActive(true);
        }
        public void ShowPurgeTheBlightButton()
        {
            purgeTheBlightButton.gameObject.SetActive(true);
        }
        public void ShowLootBattlefieldButton()
        {
            lootBattlefieldButton.gameObject.SetActive(true);
        }
        public void ShowForbiddenRitualsButton()
        {
            forbiddenRitualsButton.gameObject.SetActive(true);
        }
        public void ShowHourOfDestinyButton()
        {
            hourOfDestinyButton.gameObject.SetActive(true);
        }
        public void ShowPostBattleOptions()
        {
            claimRewardsButton.gameObject.SetActive(false);
            mapSceneUIManager.HUDPanel.HideConsumablesBlocker();

            DisplayBattleRewards();
            postBattleChoicesCanvasGroup.FadeInAsync(0.25f);
            postBattleTotalCanvasGroup.FadeInAsync(0.25f);
            conscriptSurvivorsButton.gameObject.SetActive(true);
            conscriptSurvivorsButtonScript.enabled = true;

            if(HeroBonusManager.Instance.ActiveHeroID == 10) {
                ShowRaiseDeadButton();
            } 
            else if(HeroBonusManager.Instance.ActiveHeroID == 3 
                || HeroBonusManager.Instance.ActiveHeroID == 4) {
                //Endless Hordes
                campaignSaveManager.ModifyGruntkinTroopHealth(TabletopTavernConstants.ENDLESS_HORDES_HEAL_AMOUNT);
            }
            else if(HeroBonusManager.Instance.ActiveHeroID == 15 
                || HeroBonusManager.Instance.ActiveHeroID == 16) {
                ShowConsumeCaptivesButton();
            }
            else if(HeroBonusManager.Instance.ActiveHeroID == 7) {
                ShowPurgeTheBlightButton();
            }
            else if(HeroBonusManager.Instance.ActiveHeroID == 9) {
                // Campaign-seeded so it is repeatable on re-entry. Advance one draw first rather than
                // offsetting the seed - System.Random diffuses adjacent seeds poorly, so seed+1 would risk
                // tracking the drop roll GenerateBattleRewards takes off the same stream.
                System.Random consumableRandom = campaignSaveManager.GetCampaignRandom();
                consumableRandom.Next();
                _generatedConsumbale = ConsumableData.GetRandomConsumable(consumableRandom);
                Consumable consumableData = ConsumableData.GetConsumable(_generatedConsumbale);
                string consumableNameLocalized = LocalizationManager.Instance.GetText(consumableData.ConsumableEnum.ToString() + "Name");
                _generatedConsumbaleText.text = consumableNameLocalized;
                _generatedConsumbaleImage.sprite = SpriteData.GetSprite(_generatedConsumbale.ToString());
                string consumableDescriptionLocalized = CampaignManager.Instance.ConsumableManager.GetConsumableDescription(consumableData.ConsumableEnum);
                _generatedConsumbaleTooltip.SetUpToolTip(consumableNameLocalized, consumableDescriptionLocalized);
                ShowRaiseDeadButton();
                ShowForbiddenRitualsButton();
            }
            else if(HeroBonusManager.Instance.ActiveHeroID == 11) {
                ShowHourOfDestinyButton();
            }
            else if(HeroBonusManager.Instance.ActiveHeroID == 13 
                || HeroBonusManager.Instance.ActiveHeroID == 14) {

                List<GearID> gearList = campaignSaveManager.DrawRandomGear(1);
                gearID = gearList[0];
                Gear gear = GearData.GetGear(gearID);

                string gearNameLocalized = LocalizationManager.Instance.GetText(gearID+"Name");
                string gearDescLocalized = LocalizationManager.Instance.GetText(gearID+"Desc");
                gearDescLocalized = string.Format(gearDescLocalized, gear.GearModifierValue);
                string gearFlavorLocalized = LocalizationManager.Instance.GetText(gearID+"Flavor");

                lootBattlefieldText.text = gearNameLocalized;
                lootBattlefieldImage.sprite = SpriteData.GetSprite(gear.GearName);
                lootBattlefieldTooltip.SetUpToolTip(gearNameLocalized, gearDescLocalized, gearFlavorLocalized);
                lootBattlefieldGearFull.SetActive(!campaignSaveManager.CanAquireGear());

                ShowLootBattlefieldButton();
            }
        }
        private void HidePostBattleOptionalRewards()
        {
            postBattleChoicesClaimed = true;
            postBattleChoicesCanvasGroup.FadeOutAsync(0.25f);
            squadDisplayCardConscript.gameObject.SetActive(false);
            raiseDeadButton.enabled = false;
            raiseDeadCard1.gameObject.SetActive(false);
            raiseDeadCard2.gameObject.SetActive(false);
            raiseDeadCard3.gameObject.SetActive(false);
        }
        public void ReturnFromRecruitPanel()
        {
            engagementPanelCanvasGroup.FadeInAsync(0.25f);
            if (!postBattleChoicesClaimed)
            {
                conscriptSurvivorsButton.gameObject.SetActive(true);
                conscriptSurvivorsButtonScript.enabled = true;
            }
            if (recruitButtonHiddenByConscript)
            {
                recruitButtonHiddenByConscript = false;
                recruitUnitButton.gameObject.SetActive(true);
            }
            if (raiseDeadHiddenByRecruit && !postBattleChoicesClaimed)
            {
                raiseDeadHiddenByRecruit = false;
                raiseDeadButton.gameObject.SetActive(true);
                raiseDeadButton.enabled = true;
                int bookNumber = campaignSaveManager.SaveData.bookNumber;
                raiseDeadCard1.gameObject.SetActive(true);
                raiseDeadCard2.gameObject.SetActive(bookNumber <= 2);
                raiseDeadCard3.gameObject.SetActive(bookNumber <= 1);
            }
            ForceContinueIfAllRewardsClaimed();
        }
        private void ForceContinueIfAllRewardsClaimed()
        {
            if (!claimConsumableButton.gameObject.activeSelf && !goldRewardButton.gameObject.activeSelf && !recruitUnitButton.gameObject.activeSelf && !lootBattlefieldButton.gameObject.activeSelf && postBattleChoicesClaimed)
            {
                CompleteEngagement(false);
            }
        }
        #endregion

        #region Closing
        public void CompleteEngagement(bool garrisonEngagement)
        {
            _showedEngagementResult = false; // player is on their way out; visibility check no longer applies
            // Resolve the kobold hero-bonus auto-prestige (and anything PrestigeUnitsOnKills already
            // queued up during results display) before deciding where to go next, so the trait picker
            // gates leaving this screen rather than surfacing later on whatever screen comes after.
            CampaignManager.Instance.CampaignSaveManager.HandleSpecialSquadsOnBattleEnd();
            mapSceneUIManager.TryDrainPendingPrestigeChoices(() =>
            {
                if (garrisonEngagement)
                {
                    ClosePanel();
                    CampaignManager.Instance.MapSceneUIManager.TownPanel.LoadTownPostGarrisonEngagement();
                    lootTownButton.gameObject.SetActive(false);
                }
                else
                {
                    if(engagementType == EngagementType.Horde)
                    {
                        mapSceneUIManager.CompleteHordeBattle();
                    }
                    else
                    {
                        mapSceneUIManager.CompleteLayerAction();
                    }
                }
            });
            continueButton.enabled = false;
        }
        private void LoseRun()
        {
            Debug.Log("lost run");
            mapSceneUIManager.LoseRunFromTown();
            ClosePanel();
        }
        public void ClosePanelPostJuice()
        {
            engagementPanelCanvasGroup.FadeOutAsync(0.25f);
            enemyArmyCanvasGroup.FadeOutAsync();
        }
        public async void HideEndBattlePanel()
        {
            await Task.Delay(2000);
            if(endBattlePanel != null) {
                endBattlePanel.GetComponent<Animator>().SetBool("Active", false);
            }
        }
        public override void ClosePanel()
        {
            Debug.Log("[Map] Closing EngagementPanel");
            _showedEngagementResult = false;
            mapSceneUIManager.HUDPanel.HideConsumablesBlocker();
            StartCoroutine(CampaignManager.Instance.MapCamera.LerpFocusedOnNodeVolume(0f, 0.25f));
            battleOptionsCanvasGroup.CGDisable();
            postBattleChoicesCanvasGroup.CGDisable();
            postBattleTotalCanvasGroup.CGDisable();
            runLostCanvasGroup.CGDisable();
            squadDisplayCardConscript.gameObject.SetActive(false);

            closeMMFPlayer.PlayFeedbacks();

            HideUnitsSlain();
            campaignSaveManager.MarkEngagementComplete(garrisonFight);

            foreach (Transform child in enemyArmyParent) {
                Destroy(child.gameObject);
            }
        }
        private void OnDestroy() 
        {
            if(campaignSaveManager != null) 
            {
                campaignSaveManager.OnArmyStructureChanged -= OnArmyStructureChanged;
            }
        }
        #endregion
    }
}
