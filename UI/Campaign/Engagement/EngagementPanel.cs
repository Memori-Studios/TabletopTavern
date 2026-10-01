using System.Collections;
using System.Collections.Generic;
using Memori.Utilities;
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
using Memori.Metaprogression;
using TJ.Recruit;
using System.Linq;
using Memori.Steamworks;
using TJ.Achievements;

namespace TJ.Engagement
{
    public class EngagementPanel : MapPanel
    {
        [SerializeField] private EngagementPanelView view;
        [SerializeField] private SquadDisplayCardMenu squadDisplayCardMenuPrefab;
        [SerializeField] private AutoResolveBattleManager autoResolveBattleManager;

        #region Corner slot
        [SerializeField] private Button continueButton;
        [SerializeField] private Button lootTownButton;
        #endregion

        #region Metaprogression
        [SerializeField] private MetaprogressionModel _postBattleConsumableMetaprogressionModel;
        [SerializeField] private MetaprogressionModel _postBattleGoldMetaprogressionModel;
        [SerializeField] private MetaprogressionModel _postBattleRecruitMetaprogressionModel;
        #endregion

        public Transform EnemyArmyParent => view.EnemyArmyParent;

        private CampaignSaveManager campaignSaveManager;
        private MemoriCanvasGroup engagementPanelCanvasGroup;
        private MapSceneUIManager mapSceneUIManager;
        private RecruitPanel recruitPanel;
        private EngagementType engagementType;
        private List<SquadDisplayCardMenu> enemySquadsCards = new();
        private bool autoResolved;
        private bool garrisonFight;
        private bool isLoadingEnemyCompany;
        private bool runLost;
        private Dictionary<Weather, string> _cachedWeatherNames;

        #region Rewards
        private int goldRewardAmount;
        private int ransomAmount;
        private bool generateConsumable;
        private ConsumableEnum consumableEnum;
        private UnitRarity recruitsRarity;
        private UnitName[] conscriptedUnitNames;
        private UnitName[] offeredRecruits;
        private readonly List<UnitName> raiseDeadUnitList = new();
        private GearID gearID;
        private ConsumableEnum _generatedConsumbale;
        private EngagementSpoilRow bountyRow, consumableRow, recruitRow;
        private EngagementChoiceRow conscriptRow;
        // Each spoil of war's title key, which is what the save records as chosen.
        private readonly Dictionary<EngagementChoiceRow, string> choiceKeys = new();
        private const string SpoilBounty = "bounty", SpoilConsumable = "consumable", SpoilRecruit = "recruit", SpoilChoice = "choice:";
        private bool choiceOffered;
        private bool choiceMade;
        private bool autoContinueQueued;
        private Coroutine closeFade;
        private enum Picker { None, Recruit, Conscript }
        private Picker openPicker;
        #endregion

        #region Report
        // Copied when the result shows: a lost run erases the save before the player can hover Detailed stats.
        private readonly List<DamageReportRow> squadReports = new();
        // The other side of the same battle; Kills or Lost of -1 means the save did not keep it.
        private readonly List<DamageReportRow> enemyReports = new();
        private string reportSubtitle;
        private int reportSlain, reportTroopsLost, reportSquadsLost, reportEnemyDestroyed, reportEnemyTotal;
        private Race reportHeroRace;
        // Army slots 0 to 9 fight; 10 and up are reserves.
        private const int DeployedSlots = 10;
        #endregion

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

        // A run can also finish "successfully" and still strand the player if the panel's fade-in is dropped,
        // leaving an invisible click-eating screen. This flag marks "results are on screen" so the watchdog
        // can sanity-check visibility.
        private bool _showedEngagementResult;

        private static string Text(string key) => LocalizationManager.Instance.GetText(key);

        #region SetUp
        public void SetUp(CampaignSaveManager _campaignSaveManager, MapSceneUIManager _mapSceneUIManager)
        {
            mapSceneUIManager = _mapSceneUIManager;
            campaignSaveManager = _campaignSaveManager;
            engagementPanelCanvasGroup = GetComponent<MemoriCanvasGroup>();
            recruitPanel = mapSceneUIManager.RecruitPanel;

            view.AutoResolveButton.onClick.AddListener(AutoResolveButtonClicked);
            view.FightButton.onClick.AddListener(StartBattleButtonClicked);
            view.HeavensongButton.onClick.AddListener(HeavensongButtonClicked);
            view.AutoResolveTooltip.SetUpToolTip(_description: Text("AutoResolveDesc"), _delay: 0.5f);
            view.FightTooltip.SetUpToolTip(_description: Text("ManuallyFightDesc"), _delay: 0.5f);
            view.SetBattleButtonsInteractable(false);

            campaignSaveManager.OnArmyStructureChanged += OnArmyStructureChanged;
            lootTownButton.onClick.AddListener(() => CompleteEngagement(true));
            lootTownButton.gameObject.SetActive(false);
            continueButton.onClick.AddListener(ContinueClicked);
            continueButton.gameObject.SetActive(false);

            //DifficultyMod 20
            view.SetAutoResolveAvailable(!DifficultyRules.AutoResolveDisabled(campaignSaveManager.SaveData.difficultyLevel));

            //DifficultyMod 3
            if (!DifficultyRules.AutoResolvePreviewHidden(campaignSaveManager.SaveData.difficultyLevel))
            {
                view.AutoResolvePreview.SetUp(this);
            }
        }
        public void LoadEngagementPanelFromTown()
        {
            garrisonFight = true;
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

            _engagementRetryCount = 0; // fresh, player-driven entry - not a watchdog retry
            LoadEngagement();
        }
        public async void LoadEngagement()
        {
            int runId = ++_engagementRunId;
            _engagementRunComplete = false;
            _showedEngagementResult = false;
            runLost = false;
            StartCoroutine(EngagementLoadWatchdog(runId));

            view.HideResultPopup();
            view.ClearRows();
            view.ShowBeforeBattle();
            view.SetBattleButtonsInteractable(false);
            view.SetPrediction("");
            view.ShowHeavensong(false);
            view.ShowReserveWarning(garrisonFight);
            continueButton.enabled = true;
            continueButton.gameObject.SetActive(false);
            lootTownButton.gameObject.SetActive(false);
            ClearEnemyCards();

            // Every line is filled before the card shows; only the enemy cards arrive after it.
            bool isPostBattleResult = campaignSaveManager.SaveData.battleCompleted;
            if (isPostBattleResult) ShowBattleFromSave(runId);
            else PrepareBattle();

            StartCoroutine(CampaignManager.Instance.MapCamera.LerpFocusedOnNodeVolume(0.5f, 0.25f));
            if (closeFade != null) StopCoroutine(closeFade);
            closeFade = null;
            engagementPanelCanvasGroup.FadeInAsync(0.25f, interactable: false);
            view.PlayOpen();
            await Task.Delay(Mathf.RoundToInt(view.OpenSeconds * 1000f));
            if (runId != _engagementRunId) return; // superseded by a watchdog retry while we waited

            if (isPostBattleResult)
            {
                ShowEngagementResult(runId);
                return;
            }

            engagementPanelCanvasGroup.interactable = true;
            if (!DifficultyRules.AutoResolvePreviewHidden(campaignSaveManager.SaveData.difficultyLevel))
            {
                view.AutoResolvePreview.CheckIfMouseOverTooltip();
            }
            view.FightTooltip.CheckIfMouseOverTooltip();
            view.AutoResolveTooltip.CheckIfMouseOverTooltip();

            await LoadEnemyCompany(true, runId);
            if (runId != _engagementRunId) return; // superseded by a watchdog retry or a result
            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.Autoresolve });
            _engagementRunComplete = true;
        }
        // Header, battlefield, weather, both army lines, Heavensong, the prediction and live buttons.
        private void PrepareBattle()
        {
            int heroID = campaignSaveManager.SaveData.heroID;
            Race heroRace = HeroData.GetRaceFromHero(heroID);
            Race race = TabletopTavernData.Instance.GenerateRaceForMap(campaignSaveManager.SaveData.bookNumber, campaignSaveManager.SaveData.seed, heroRace);
            List<UnitTier> unitsPool = TabletopTavernData.Instance.GetSquadsWithTiersFromRace(race);
            SetBeforeBattleHeader(race);

            void GenerateEnemyArmy()
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

                    // DifficultyMod 7 / 19 / 6 / 10 / 14 all resolve through DifficultyRules
                    // so the difficulty sim generates the same armies the game does.
                    TT_Difficulty difficulty = campaignSaveManager.SaveData.difficultyLevel;
                    battlesFought += DifficultyRules.BattlesFoughtBonus(difficulty, campaignSaveManager.SaveData.bookNumber) + OrdealRegistry.BattlesFoughtStep(campaignSaveManager.SaveData);

                    enemyArmy = ArmyCreator.GenerateEnemyArmy(
                        campaignSaveManager.SaveData.bookNumber,
                        battlesFought,
                        campaignSaveManager.GetSeededRandom(),
                        engagementType == EngagementType.Horde,
                        unitsPool,
                        DifficultyRules.HarderFinalBattle(difficulty),
                        DifficultyRules.EnemyPrestigeEligible(difficulty),
                        OrdealRegistry.EnemyPrestigeEnhanced(campaignSaveManager.SaveData),
                        campaignSaveManager.SaveData.HasOrdeal(OrdealId.EliteGuard),
                        OrdealRegistry.DoubleEnemyPrestigeChance(campaignSaveManager.SaveData),
                        DifficultyRules.SpellsExtraSquad(difficulty)
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

                campaignSaveManager.SaveData.enemyWarlordHeroID = EnemyWarlord.ResolveHeroID(
                    campaignSaveManager.SaveData, engagementType == EngagementType.Horde, garrisonFight);
                campaignSaveManager.SaveEnemyArmy(enemyArmy);
                autoResolveBattleManager.Load(garrisonFight);
            }

            void GenerateBattlefield()
            {
                MapRegion mapRegion = MapThemeManager.Instance.GetMapRegion(race);
                int campaignSeed = CampaignManager.Instance.CampaignSaveManager.SaveData.seed;
                int bookNum = CampaignManager.Instance.CampaignSaveManager.SaveData.bookNumber;
                MapNodeData nodeData = CampaignManager.Instance.MapSceneUIManager.MapSceneManager.SelectedNodeData;
                Weather weather = CampaignSaveManager.GenerateNodeWeather(nodeData.index, campaignSeed, bookNum, mapRegion, campaignSaveManager.SaveData.ordealWeather);
                Biome biome = CampaignSaveManager.GenerateNodeBiome(nodeData.index, campaignSeed, bookNum, mapRegion);
                if (garrisonFight) biome = Biome.Plains;

                // override weather if player has specific gear
                if (weather == Weather.Rain && CampaignManager.Instance.GearManager.CheckForGear(GearID.BraceletoftheSunGoddess)) weather = Weather.ClearSkies;

                ShowWeather(weather);
                LoadWeatherTip(weather);

                // Display only - the preset still stores Biome.Plains. A garrison is a walled fight
                // rather than one of the four biomes, and calling it Plains told the player nothing.
                view.SetBattlefield(Text(garrisonFight ? "Garrison" : biome.ToString()), biome, garrisonFight);

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

            GenerateBattlefield();
            GenerateEnemyArmy();
            ShowArmies();

            bool isTaelindorHero = (heroID == 7 || heroID == 8) && !campaignSaveManager.SaveData.IsFactionPassiveBlocked(Race.TaelindorForest);
            view.ShowHeavensong(isTaelindorHero, Text("Campaign Bonus"), Text("TaelindorForestBonusDescription"));
            if (isTaelindorHero)
            {
                MapRegion region = campaignSaveManager.SaveData.battleFieldPreset.mapRegion;
                _cachedWeatherNames = new Dictionary<Weather, string>();
                foreach (var w in region.GetPossibleWeathers())
                    _cachedWeatherNames[w.weather] = Text(w.weather.ToString());
            }

            view.SetBattleButtonsInteractable(true);
        }
        // The card the player left for battle, rebuilt from the save, so the result pop-up has it to grey out.
        private void ShowBattleFromSave(int runId)
        {
            if (campaignSaveManager.SaveData.townData.townInteractionStatus == TownInteractionStatus.GarrisonBattleStarted) garrisonFight = true;
            BattleFieldPreset preset = campaignSaveManager.SaveData.battleFieldPreset;
            SetBeforeBattleHeader(preset.race);
            view.SetBattlefield(Text(garrisonFight ? "Garrison" : preset.biome.ToString()), preset.biome, garrisonFight);
            ShowWeather(preset.weather);
            ShowArmies();
            _ = LoadEnemyCompany(false, runId);
        }
        private void SetBeforeBattleHeader(Race enemyRace)
        {
            if (garrisonFight)
            {
                var town = campaignSaveManager.SaveData.townData;
                string townLine = string.Format(Text("engagementGarrisonSub"), Text(town.townName), Text(town.townRace.ToString()) + " " + Text(town.townSize.ToString()));
                view.SetHeader(EngagementPanelView.HeaderKind.Garrison, Text("TownGarrison"), townLine, ColorData.GetRaceDisplayColor(town.townRace));
                return;
            }
            string act = MemoriUI.ConvertNumberToRomanNumeral(campaignSaveManager.SaveData.bookNumber);
            bool horde = engagementType == EngagementType.Horde;
            string subtitle = string.Format(Text(horde ? "engagementHordeSub" : "engagementSkirmishSub"), act, Text(enemyRace.ToString()));
            view.SetHeader(horde ? EngagementPanelView.HeaderKind.Horde : EngagementPanelView.HeaderKind.Skirmish,
                Text(engagementType.ToString()), subtitle, ColorData.GetRaceDisplayColor(enemyRace));
        }
        private void ShowWeather(Weather weather)
        {
            string localizedWeather = Text(weather.ToString());
            string description = weather == Weather.ClearSkies ? "" : WeatherInfo.GetDescription(weather);
            view.SetWeather(localizedWeather, weather, localizedWeather, description);
        }
        // Squads and troops on each side. Only deployed squads fight, so reserves are left out of the player's count.
        private void ShowArmies()
        {
            int squads = 0, troops = 0;
            foreach (SquadToLoad squad in campaignSaveManager.SaveData.playerArmy)
            {
                if (squad.UnitIndex < 0 || squad.UnitIndex >= DeployedSlots || squad.SquadCurrentHealth <= 0) continue;
                squads++;
                troops += Troops(squad);
            }
            int enemySquads = 0, enemyTroops = 0;
            foreach (SquadToLoad squad in campaignSaveManager.SaveData.enemyArmy)
            {
                enemySquads++;
                enemyTroops += Troops(squad);
            }
            string format = Text("engagementSquadsTroops");
            string enemyLine = string.Format(format, enemySquads, enemyTroops);
            view.SetEnemyHost(enemyLine);
            view.SetArmies(string.Format(format, squads, troops), enemyLine);
        }
        // The same count the squad cards show, so the strip adds up to the army bar.
        private static int Troops(SquadToLoad squad) =>
            int.TryParse(TabletopTavernData.Instance.GetSquadCurrentUnitCount(squad), out int units) ? units : 0;

        // Watches a single LoadEngagement attempt (identified by runId). If it hasn't finished within
        // ENGAGEMENT_WATCHDOG_TIMEOUT real seconds, that attempt is treated as stalled (this is what was
        // silently happening after an alt-tab during the post-battle map reload: the panel stayed visible
        // but permanently locked, with no exception ever logged) and we retry from scratch. WaitForSecondsRealtime
        // ignores timeScale so this still fires even if something upstream paused the game.
        private IEnumerator EngagementLoadWatchdog(int runId)
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
                engagementPanelCanvasGroup.CGEnable();
                continueButton.gameObject.SetActive(true);
                continueButton.enabled = true;
            }
        }
        // Companion check to the stall handling above, for the case where the run *did* complete but the
        // results came up invisible. Alpha 0 with the results still up is only legitimate while a picker is open.
        private void VerifyEngagementResultVisible()
        {
            if (!_showedEngagementResult) return;
            // The recruit and conscript pickers hide the card on purpose while they are open.
            if (openPicker != Picker.None) return;
            if (engagementPanelCanvasGroup.alpha > 0f) return;

            Debug.LogError("[EngagementPanel] Engagement result is on screen but the panel is at alpha 0 - forcing it visible so the player isn't stranded.");
            engagementPanelCanvasGroup.CGEnable();
        }
        private void ClearEnemyCards()
        {
            foreach (Transform child in view.EnemyArmyParent)
                Destroy(child.gameObject);
            enemySquadsCards = new();
        }
        public async Task LoadEnemyCompany(bool _playfeedbacks, int runId)
        {
            isLoadingEnemyCompany = true;
            ClearEnemyCards();
            if (campaignSaveManager.SaveData.enemyArmy == null || campaignSaveManager.SaveData.enemyArmy.Length == 0)
            {
                isLoadingEnemyCompany = false; // don't leave OnArmyStructureChanged permanently blocked
                return;
            }

            // First enemy artillery: tell the player the army holds position instead of charging.
            if (!campaignSaveManager.SaveData.battleCompleted)
            {
                foreach (SquadToLoad squad in campaignSaveManager.SaveData.enemyArmy)
                {
                    if (TabletopTavernData.Instance.GetUnitTypeFromUnitName(squad.UnitName) != UnitType.Artillery) continue;
                    TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.EnemyArtillery });
                    break;
                }

                // First enemy Outriders: they skip deployment and arrive behind the player's army.
                foreach (SquadToLoad squad in campaignSaveManager.SaveData.enemyArmy)
                {
                    if (!TabletopTavernData.Instance.GetSquadStats(squad.UnitName).SquadAttributes.Outrider) continue;
                    TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.EnemyOutriders });
                    break;
                }
            }

            foreach (SquadToLoad squad in campaignSaveManager.SaveData.enemyArmy)
            {
                SquadDisplayCardMenu squadDisplayCardMenu = Instantiate(squadDisplayCardMenuPrefab, view.EnemyArmyParent);
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
            view.FightTooltip.OnPointerExit(null);
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
            // The army bar rebuilds its cards on every structure change; while the result shows, the slain and lost counts go back on.
            if (_showedEngagementResult) StartCoroutine(ReshowUnitsSlain());
            if (isLoadingEnemyCompany) return;
            if (campaignSaveManager.SaveData.battleCompleted) return;
            ShowArmies();
            autoResolveBattleManager.Load(garrisonFight);
        }
        private IEnumerator ReshowUnitsSlain()
        {
            // A frame later the army bar has its new cards.
            yield return null;
            // A lost run has erased the save by now, and the counts read from it.
            if (!_showedEngagementResult || campaignSaveManager.SaveData == null) yield break;
            ShowUnitsSlain();
        }
        private void HeavensongButtonClicked()
        {
            Weather currentWeather = campaignSaveManager.SaveData.battleFieldPreset.weather;
            MapRegion mapRegion = campaignSaveManager.SaveData.battleFieldPreset.mapRegion;
            bool blockRain = CampaignManager.Instance.GearManager.CheckForGear(GearID.BraceletoftheSunGoddess);
            var candidates = mapRegion.GetPossibleWeathers().Where(w => w.weather != currentWeather && !(blockRain && w.weather == Weather.Rain)).ToList();
            if (candidates.Count == 0) return;

            view.ShowHeavensong(false);

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
        private IEnumerator RollWeatherText(Weather finalWeather, MapRegion mapRegion, Weather excludeWeather)
        {
            bool blockRain = CampaignManager.Instance.GearManager.CheckForGear(GearID.BraceletoftheSunGoddess);
            var possibleWeathers = mapRegion.GetPossibleWeathers().Where(w => w.weather != excludeWeather && !(blockRain && w.weather == Weather.Rain)).ToList();
            float elapsed = 0f;
            float duration = 0.35f;
            float interval = 0.035f;
            int cycleIndex = 0;
            IAudioRequester.Instance.PlaySFX(SFXData.Reroll);

            while (elapsed < duration)
            {
                view.SetWeatherText(_cachedWeatherNames[possibleWeathers[cycleIndex % possibleWeathers.Count].weather]);
                cycleIndex++;
                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }

            ShowWeather(finalWeather);

            BattleFieldPreset preset = campaignSaveManager.SaveData.battleFieldPreset;
            preset.weather = finalWeather;
            campaignSaveManager.SaveBattlefieldPreset(preset);
            mapSceneUIManager.HUDPanel.ShowWeatherHover(finalWeather, finalWeather != Weather.ClearSkies);
            IAudioRequester.Instance.PlaySFX(SFXData.TinyClick);

            LoadWeatherTip(finalWeather);
        }
        private static void LoadWeatherTip(Weather weather)
        {
            TutorialStep? tip = weather switch
            {
                Weather.Rain => TutorialData.RainWeather,
                Weather.Fog => TutorialData.FogWeather,
                Weather.Snow => TutorialData.SnowWeather,
                _ => null,
            };
            if (tip.HasValue)
                TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { tip.Value });
        }

        public void AlertOfBattleResults(bool playerWon)
        {
            if (DifficultyRules.AutoResolveDisabled(campaignSaveManager.SaveData.difficultyLevel)) return;

            string result = $"<color={(playerWon ? ColorData.Positive : ColorData.Negative)}>{Text(playerWon ? "Victory" : "Defeat")}</color>";
            view.SetPrediction(string.Format(Text("engagementPredicts"), result));
        }
        #endregion

        #region Post Battle
        // Endless acts add a little pay on top of the battles-fought bands, which reset every act.
        private int EndlessGoldBonus()
        {
            return Mathf.Min(TabletopTavernConstants.ENDLESS_GOLD_CAP,
                TabletopTavernConstants.EndlessActs(campaignSaveManager.SaveData.bookNumber) * TabletopTavernConstants.ENDLESS_GOLD_PER_ACT);
        }
        // The same +0 / +2 / +4 / +6 bands by battles fought pay out on the bounty and the ransom.
        private int BattlesFoughtBonus()
        {
            int fought = campaignSaveManager.SaveData.BattlesFought;
            if (fought < 3) return 0;
            if (fought < 6) return 2;
            if (fought < 9) return 4;
            return 6;
        }
        public void GenerateBattleRewards()
        {
            goldRewardAmount = engagementType == EngagementType.Skirmish ? TabletopTavernConstants.GetSkirmishReward() : TabletopTavernConstants.GetHordeReward();
            goldRewardAmount += BattlesFoughtBonus() + EndlessGoldBonus();

            // Both the drop roll and the pick come off the campaign seed, so re-opening the results panel
            // (exiting to the main menu and back) can't reroll the consumable reward.
            System.Random rewardRandom = campaignSaveManager.GetCampaignRandom();
            generateConsumable = rewardRandom.Next(0, 100) < CampaignManager.Instance.GoldManager.PotionRewardsOdds;
            if (SaveDataHandler.IsMetaprogressionNodeUnlocked(_postBattleConsumableMetaprogressionModel)) generateConsumable = true;
            if (generateConsumable)
            {
                bool hasLuckyHorseshoe = CampaignManager.Instance.GearManager.CheckForGear(GearID.LuckyHorseshoe);
                consumableEnum = ConsumableData.GetWeightedConsumable(campaignSaveManager.SaveData.bookNumber, rewardRandom, hasLuckyHorseshoe);
            }

            if (!campaignSaveManager.SaveData.HasOrdeal(OrdealId.NoQuarter)) campaignSaveManager.RegisterRansomOffered();
            ransomAmount = TabletopTavernConstants.GetRansomCaptivesReward() + BattlesFoughtBonus() + EndlessGoldBonus();
            if (SaveDataHandler.IsMetaprogressionNodeUnlocked(_postBattleGoldMetaprogressionModel)) ransomAmount += _postBattleGoldMetaprogressionModel.NodeValue;
            //The Skull Harvest: +2 Gold from ransoming captives
            if (HeroBonusManager.Instance.ActiveHeroID == 5) ransomAmount += 2;

            recruitsRarity = campaignSaveManager.SaveData.bookNumber == 1 ? UnitRarity.Common : UnitRarity.Uncommon;
            if (engagementType == EngagementType.Horde)
                recruitsRarity = campaignSaveManager.SaveData.bookNumber == 1 ? UnitRarity.Uncommon : UnitRarity.Rare;

            TabletopTavern.Analytics.NodeLog.Try("battle rewards", () => TabletopTavern.Analytics.NodeLog.Set("rw", new Dictionary<string, object>
            {
                { "bounty", goldRewardAmount },
                { "ransom", ransomAmount },
                { "cons", generateConsumable ? consumableEnum.ToString() : null },
                { "recRar", recruitsRarity.ToString() },
            }));

            conscriptedUnitNames = null;
            if (campaignSaveManager.SaveData.enemyArmy == null || campaignSaveManager.SaveData.enemyArmy.Length == 0) return;
            //get 3 random units from enemy army
            UnityEngine.Random.InitState(campaignSaveManager.SaveData.seed);
            var shuffled = campaignSaveManager.SaveData.enemyArmy.OrderBy(_ => UnityEngine.Random.value).ToList();
            conscriptedUnitNames = shuffled.Take(Mathf.Min(3, shuffled.Count)).Select(s => s.UnitName).ToArray();
        }
        private async void ShowEngagementResult(int runId)
        {
            if(campaignSaveManager.SaveData.townData.townInteractionStatus == TownInteractionStatus.GarrisonBattleStarted) {
                campaignSaveManager.MarkGarrisonBattleComplete();

                garrisonFight = true;
            }

            await LoadEnemyCompany(false, runId);
            if (runId != _engagementRunId) return; // superseded by a watchdog retry

            view.SetBattleButtonsInteractable(false);
            campaignSaveManager.CorrectHealthOfWithdrawnSquads();

            ShowUnitsSlain();

            if(!garrisonFight)
            {
                campaignSaveManager.HealTroopsInReserve();
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

            bool battleWon = campaignSaveManager.SaveData.playerWonBattle;
            CaptureReport();
            view.SetReport(ReportCells());
            view.SetDetailedStats(BuildDamageTooltip);

            Color colour = ParseColour(battleWon ? ColorData.Positive : ColorData.Negative);
            string title = Text(battleWon ? "Victory" : "Defeat");
            string outcome = battleWon ? Text(garrisonFight ? "engagementOutcomeGarrison" : "engagementOutcomeHost") : Text("CompanyShattered");
            string subtitle = battleWon
                ? string.Format(Text("engagementResultSub"), outcome, Text(garrisonFight ? "TownGarrison" : engagementType.ToString()))
                : outcome;
            reportSubtitle = subtitle;
            bool withRewards = battleWon && !garrisonFight && !mapSceneUIManager.MapSceneManager.WillCompleteLayerEndInGameOver();
            if (withRewards) GenerateBattleRewards();
            if (!battleWon)
            {
                runLost = true;
                // Records renown and erases the campaign save, so everything the panel shows is read above.
                mapSceneUIManager.GameOverPanel.RecordGameOver(false);
            }

            IAudioRequester.Instance.PlaySFX(battleWon ? SFXData.Cheer : SFXData.Boo);
            IAudioRequester.Instance.PlaySFX(battleWon ? SFXData.BattleWin : SFXData.BattleLoss);
            if(battleWon)
                IAudioRequester.Instance.PlaySFX(SFXData.Trumpet);

            engagementPanelCanvasGroup.CGEnable();
            _showedEngagementResult = true;
            if (runId == _engagementRunId)
                _engagementRunComplete = true;

            // The card keeps the battle it came from, greyed under the end-of-battle banner; the result replaces it once the banner has gone.
            view.PlayResultPopup(title, outcome, () =>
            {
                if (runId == _engagementRunId) RevealResult(battleWon, withRewards, title, subtitle, colour);
            });
        }
        private void RevealResult(bool battleWon, bool withRewards, string title, string subtitle, Color colour)
        {
            view.SetHeader(battleWon ? EngagementPanelView.HeaderKind.Victory : EngagementPanelView.HeaderKind.Defeat, title, subtitle, colour);
            view.SetPill(battleWon ? title : Text("engagementRunOver"), colour);
            if (!battleWon)
            {
                view.ShowResult(false, Text("engagementDefeatLine"), enemySquadsCards.Count);
                continueButton.gameObject.SetActive(true);
            }
            else if (garrisonFight)
            {
                view.ShowResult(false, Text("engagementGarrisonWonLine"), enemySquadsCards.Count);
                lootTownButton.gameObject.SetActive(true);
                mapSceneUIManager.HUDPanel.ShowConsumablesBlocker();
            }
            else if (!withRewards)
            {
                view.ShowResult(false, null, enemySquadsCards.Count);
                continueButton.gameObject.SetActive(true);
            }
            else
            {
                view.ShowResult(true, null, enemySquadsCards.Count);
                ShowRewards();
            }
            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.HealthRecovery });
        }
        public void ShowUnitsSlain()
        {
            if(autoResolved)
            {
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
        // UnitsAlive is ceiling-rounded from pooled health (see AutoResolveBattleManager), so it can
        // read as 1 even when only a sliver of a unit's health is left - which undercounts losses by
        // 1 in that case. Floor-dividing the same finalHealth avoids that without touching the sim's
        // own UnitsAlive value (still used elsewhere for targeting/combat resolution).
        private static int EndingUnits(AutoResolveSquad squad) => squad.healthPerKill > 0 ? squad.finalHealth / squad.healthPerKill : squad.UnitsAlive;
        public void HideUnitsSlain()
        {
            foreach (SquadDisplayCardMenu squadDisplayCardMenu in mapSceneUIManager.HUDPanel.PlayerSquadsCards) {
                squadDisplayCardMenu.HideUnitsSlain();
                squadDisplayCardMenu.HideUnitsLost();
            }
            foreach (SquadDisplayCardMenu squadDisplayCardMenu in enemySquadsCards) {
                if (squadDisplayCardMenu == null) continue;
                squadDisplayCardMenu.HideUnitsSlain();
                squadDisplayCardMenu.HideUnitsLost();
            }
        }
        #endregion

        #region Battle report
        // Reads damage, kills and losses for both armies, from the stores both battle paths write.
        private void CaptureReport()
        {
            var data = campaignSaveManager.SaveData;
            reportHeroRace = HeroData.GetRaceFromHero(data.heroID);
            var damage = new Dictionary<string, int>();
            var kills = new Dictionary<string, int>();
            var losses = new Dictionary<string, int>();
            if (data.SquadDamageStore != null) foreach (SquadDamageStored entry in data.SquadDamageStore) damage[entry.SquadGUID] = entry.Damage;
            if (data.SquadKillsStore != null) foreach (SquadKillsStored entry in data.SquadKillsStore) kills[entry.SquadGUID] = entry.Kills;
            if (data.SquadLossesStore != null) foreach (SquadLossesStored entry in data.SquadLossesStore) losses[entry.SquadGUID] = entry.Losses;

            squadReports.Clear();
            reportSlain = reportTroopsLost = reportSquadsLost = 0;
            foreach (SquadToLoad squad in data.playerArmy)
            {
                if (squad.UnitIndex < 0) continue;
                string id = squad.UniqueID;
                bool fought = damage.ContainsKey(id) || kills.ContainsKey(id) || losses.ContainsKey(id);
                if (!fought) continue;
                var report = new DamageReportRow
                {
                    Unit = squad.UnitName,
                    Damage = damage.TryGetValue(id, out int d) ? d : 0,
                    Kills = kills.TryGetValue(id, out int k) ? k : 0,
                    Lost = losses.TryGetValue(id, out int l) ? l : 0,
                };
                squadReports.Add(report);
                reportSlain += report.Kills;
                reportTroopsLost += report.Lost;
                if (squad.SquadCurrentHealth <= 0) reportSquadsLost++;
            }
            squadReports.Sort((a, b) => b.Damage.CompareTo(a.Damage));

            reportEnemyTotal = data.enemyArmy?.Length ?? 0;
            reportEnemyDestroyed = 0;
            if (data.enemyArmy != null)
                foreach (SquadToLoad squad in data.enemyArmy)
                    if (squad.SquadCurrentHealth <= 0) reportEnemyDestroyed++;

            // Auto-resolve saves enemy damage only; its enemy kills and losses live in the sim's stats, as the enemy card badges read them.
            AutoResolveSquad[] enemyStats = autoResolved ? autoResolveBattleManager.EnemyAutoResolveStats : null;
            enemyReports.Clear();
            if (data.enemyArmy != null)
                foreach (SquadToLoad squad in data.enemyArmy)
                {
                    string id = squad.UniqueID;
                    if (string.IsNullOrEmpty(id)) continue;
                    var report = new DamageReportRow
                    {
                        Unit = squad.UnitName,
                        Damage = damage.TryGetValue(id, out int d) ? d : 0,
                        Kills = kills.TryGetValue(id, out int k) ? k : -1,
                        Lost = losses.TryGetValue(id, out int l) ? l : -1,
                    };
                    if (enemyStats != null)
                        foreach (AutoResolveSquad stats in enemyStats)
                        {
                            if (stats.UniqueID != id) continue;
                            if (report.Kills < 0) report.Kills = stats.UnitsSlain;
                            if (report.Lost < 0) report.Lost = Mathf.Max(0, stats.maxUnits - EndingUnits(stats));
                        }
                    enemyReports.Add(report);
                }
            enemyReports.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        }
        private List<(string, string)> ReportCells()
        {
            string negative = ColorData.Negative;
            return new List<(string, string)>
            {
                (Text("engagementCellDestroyed"), string.Format(Text("engagementOfCount"), reportEnemyDestroyed, reportEnemyTotal)),
                (Text("engagementCellSlain"), reportSlain.ToString("N0")),
                (Text("engagementCellLosses"), Warn(string.Format(Text("engagementTroopsCount"), reportTroopsLost), reportTroopsLost > 0, negative)),
                (Text("engagementCellSquadsLost"), reportSquadsLost == 0 ? Text("engagementNone") : Warn(reportSquadsLost.ToString(), true, negative)),
            };
        }
        private static string Warn(string text, bool warn, string colour) => warn ? $"<color={colour}>{text}</color>" : text;

        private TooltipContent BuildDamageTooltip() =>
            DamageReportTooltip.Build(squadReports, enemyReports, reportSubtitle, view.DamageIcon, reportHeroRace, reportSlain, reportTroopsLost, reportSquadsLost);
        private static Color ParseColour(string hex) => ColorUtility.TryParseHtmlString(hex, out Color colour) ? colour : Color.white;
        #endregion

        #region Spoils
        // Everything the win pays out shows with the result: the spoils to take and the spoils of war to choose from.
        private void ShowRewards()
        {
            mapSceneUIManager.HUDPanel.HideConsumablesBlocker();
            // Dead enemies stay in the save until the layer completes, so a reloaded result still shows and conscripts them.
            campaignSaveManager.RemoveZeroHealthSquads(includeEnemies: false);
            view.ClearRows();
            choiceKeys.Clear();
            choiceMade = false;
            choiceOffered = false;
            autoContinueQueued = false;
            openPicker = Picker.None;
            offeredRecruits = null;
            consumableRow = null;
            conscriptRow = null;
            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.PostBattleChoices });

            bountyRow = view.AddSpoil();
            bountyRow.Set(view.Icon(EngagementPanelView.RewardIcon.Gold), ParseColour(ColorData.Gold), Text("Claim Bounty"), Text("engagementBountyDetail"));
            bountyRow.SetValue("+" + goldRewardAmount, true);
            bountyRow.SetTaken(WasTaken(SpoilBounty));
            bountyRow.Button.onClick.AddListener(ClaimGoldRewardButtonClicked);

            if (generateConsumable)
            {
                Consumable consumableData = ConsumableData.GetConsumable(consumableEnum);
                string name = Text(consumableData.ConsumableEnum + "Name");
                string description = CampaignManager.Instance.ConsumableManager.GetConsumableDescription(consumableData.ConsumableEnum);
                consumableRow = view.AddSpoil();
                consumableRow.Set(SpriteData.GetSprite(consumableEnum.ToString()), Color.white, name, KeywordText.Render(description, false));
                consumableRow.SetTooltip(name, KeywordText.ForTooltip(description));
                consumableRow.SetTaken(WasTaken(SpoilConsumable));
                consumableRow.Button.onClick.AddListener(ClaimConsumableButtonClicked);
            }

            Color rarityColour = ColorData.GetRarityTierColor(recruitsRarity);
            recruitRow = view.AddSpoil();
            recruitRow.Set(view.Icon(EngagementPanelView.RewardIcon.Recruit), rarityColour, Text("Recruit Unit"), Text("Recruit Unit Reward Desc"));
            recruitRow.SetTag(Text(recruitsRarity.ToString()), rarityColour);
            recruitRow.SetTaken(WasTaken(SpoilRecruit));
            recruitRow.Button.onClick.AddListener(ClaimRecruitUnitButtonClicked);

            ShowChoices();
            continueButton.gameObject.SetActive(true);
        }
        private void ShowChoices()
        {
            // No Quarter: defeated enemies pay no ransom, so the choice is not offered.
            if (!campaignSaveManager.SaveData.HasOrdeal(OrdealId.NoQuarter))
            {
                EngagementChoiceRow ransom = AddChoice(EngagementPanelView.RewardIcon.Ransom, "Ransom Captives", Text("engagementRansomDetail"));
                ransom.SetValue("+" + ransomAmount, true);
                ransom.Button.onClick.AddListener(() => RansomCaptivesButtonClicked(ransom));
            }

            if (conscriptedUnitNames != null && conscriptedUnitNames.Length > 0)
            {
                conscriptRow = AddChoice(EngagementPanelView.RewardIcon.Conscript, "Conscript Survivors", ConscriptLine());
                conscriptRow.SetTooltip(Text("Conscript Survivors"), ConscriptTooltip());
                conscriptRow.Button.onClick.AddListener(ConscriptSurvivorsButtonClicked);
            }

            int heroID = HeroBonusManager.Instance.ActiveHeroID;
            if (heroID == 10) AddRaiseDead();
            else if (heroID == 3 || heroID == 4)
            {
                //Endless Hordes
                campaignSaveManager.ModifyGruntkinTroopHealth(TabletopTavernConstants.ENDLESS_HORDES_HEAL_AMOUNT);
            }
            else if (heroID == 15 || heroID == 16)
            {
                EngagementChoiceRow consume = AddChoice(EngagementPanelView.RewardIcon.Consume, "Consume Survivors", Text("Consume Survivors Desc"));
                consume.SetTag(Text("engagementHeroOption"));
                consume.Button.onClick.AddListener(() => ConsumeCaptivesButtonClicked(consume));
            }
            else if (heroID == 7)
            {
                EngagementChoiceRow purge = AddChoice(EngagementPanelView.RewardIcon.Purge, "Purge Blight Title", Text("Purge Blight Desc"));
                purge.SetTag(Text("engagementHeroOption"));
                purge.Button.onClick.AddListener(() => PurgeTheBlightButtonClicked(purge));
            }
            else if (heroID == 9)
            {
                AddRaiseDead();
                AddForbiddenRituals();
            }
            else if (heroID == 11)
            {
                EngagementChoiceRow hour = AddChoice(EngagementPanelView.RewardIcon.HourOfDestiny, "Hour of Destiny",
                    Text("HourOfDestinyTitle") + ". " + Text("HourOfDestinyDesc"));
                hour.SetTag(Text("engagementHeroOption"));
                hour.Button.onClick.AddListener(() => HourOfDestinyButtonClicked(hour));
            }
            else if (heroID == 13 || heroID == 14)
            {
                AddLootBattlefield();
            }
            view.ShowNoChoices(!choiceOffered);

            foreach (KeyValuePair<EngagementChoiceRow, string> choice in choiceKeys)
            {
                if (!WasTaken(SpoilChoice + choice.Value)) continue;
                choiceMade = true;
                view.ChooseRow(choice.Key, quiet: true);
                break;
            }
        }
        private EngagementChoiceRow AddChoice(EngagementPanelView.RewardIcon icon, string titleKey, string detail) => AddChoice(view.Icon(icon), titleKey, detail);
        private EngagementChoiceRow AddChoice(Sprite icon, string titleKey, string detail)
        {
            choiceOffered = true;
            EngagementChoiceRow row = view.AddChoice();
            row.Set(icon, Color.white, Text(titleKey), detail);
            choiceKeys[row] = titleKey;
            return row;
        }
        // The percent comes from the picker's own rule, so the line matches the health the unit joins at.
        private string ConscriptLine() => string.Format(Text("ConscriptSurvivorsTitle"), Mathf.RoundToInt(recruitPanel.ConscriptHealth() * 100f));
        private string ConscriptTooltip()
        {
            string description = ConscriptLine() + "\n\n" + Text("ConscriptSurvivorsDesc");
            foreach (SquadToLoad squad in campaignSaveManager.SaveData.enemyArmy)
            {
                int tier = TabletopTavernData.Instance.GetUnitTierFromUnitName(squad.UnitName);
                string unitNameLocalized = Text(squad.UnitName.ToString());
                description += $"\n- <color={ColorData.GetRarityTierColorString((UnitRarity)(tier - 1))}>" + MemoriUI.AddSpacesToSentence(unitNameLocalized) + "</color>";
            }
            return description;
        }
        private void AddRaiseDead()
        {
            raiseDeadUnitList.Clear();
            System.Random random = campaignSaveManager.GetCampaignRandom();
            int bookNumber = campaignSaveManager.SaveData.bookNumber;
            UnitName[] pool;
            int count;
            if (bookNumber <= 1)
            {
                pool = new[] { UnitName.BoneclatterSpears, UnitName.UndeadLevies, UnitName.GravestoneImps, UnitName.FeralHounds };
                count = 3;
            }
            else if (bookNumber == 2)
            {
                pool = new[] { UnitName.DeathhavenFiends, UnitName.Nightriders, UnitName.BoneshardArchers, UnitName.MistWraiths };
                count = 2;
            }
            else
            {
                pool = new[] { UnitName.BlackWardens, UnitName.Bloodsworn, UnitName.CorpseClaws, UnitName.BloodswornKnights };
                count = 1;
            }
            for (int i = 0; i < count; i++)
                raiseDeadUnitList.Add(pool[random.Next(pool.Length)]);

            string names = string.Join(", ", raiseDeadUnitList.Select(unit => Text(unit.ToString())));
            EngagementChoiceRow row = AddChoice(EngagementPanelView.RewardIcon.RaiseDead, "Raise Dead", names);
            row.SetUnits(raiseDeadUnitList.Select(unit => TabletopTavernData.Instance.GetUnitIcon(unit)).ToArray());
            row.SetTag(Text("engagementHeroOption"));
            row.Button.onClick.AddListener(() => RaiseDeadButtonClicked(row));
        }
        private void AddForbiddenRituals()
        {
            // Campaign-seeded so it is repeatable on re-entry. Advance one draw first rather than
            // offsetting the seed - System.Random diffuses adjacent seeds poorly, so seed+1 would risk
            // tracking the drop roll GenerateBattleRewards takes off the same stream.
            System.Random consumableRandom = campaignSaveManager.GetCampaignRandom();
            consumableRandom.Next();
            _generatedConsumbale = ConsumableData.GetRandomConsumable(consumableRandom);
            Consumable consumableData = ConsumableData.GetConsumable(_generatedConsumbale);
            string name = Text(consumableData.ConsumableEnum + "Name");
            string description = CampaignManager.Instance.ConsumableManager.GetConsumableDescription(consumableData.ConsumableEnum);
            EngagementChoiceRow row = AddChoice(SpriteData.GetSprite(_generatedConsumbale.ToString()), "Forbidden Rituals", name);
            row.SetTooltip(name, KeywordText.ForTooltip(description));
            row.SetTag(Text("engagementHeroOption"));
            row.Button.onClick.AddListener(() => ForbiddenRitualsButtonClicked(row));
        }
        private void AddLootBattlefield()
        {
            List<GearID> gearList = campaignSaveManager.DrawRandomGear(1);
            gearID = gearList[0];
            Gear gear = GearData.GetGear(gearID);
            string gearNameLocalized = Text(gearID + "Name");
            string gearDescLocalized = string.Format(Text(gearID + "Desc"), gear.GearModifierValue);
            string gearFlavorLocalized = Text(gearID + "Flavor");

            // With no free gear slot the line says so, in the warning colour, instead of naming the item.
            bool gearFull = !campaignSaveManager.CanAquireGear();
            EngagementChoiceRow row = AddChoice(SpriteData.GetSprite(gear.GearName), "Loot Battlefield", gearFull ? Text("No space for gear") : gearNameLocalized);
            if (gearFull) row.SetDetailColour(ParseColour(ColorData.Negative));
            row.SetTooltip(gearNameLocalized, KeywordText.ForTooltip(gearDescLocalized), gearFlavorLocalized);
            row.SetTag(Text("engagementHeroOption"));
            row.Button.onClick.AddListener(() => LootBattlefieldButtonClicked(row));
        }
        #endregion

        #region Taking spoils
        public void ClaimGoldRewardButtonClicked()
        {
            if (bountyRow.IsTaken) return;
            MarkTaken(SpoilBounty);
            CampaignManager.Instance.GoldManager.ModifyGold(goldRewardAmount, Text("Loot Gold"));
            bountyRow.SetTaken(true, true);
            QueueAutoContinue();
        }
        public void ClaimConsumableButtonClicked()
        {
            if (consumableRow == null || consumableRow.IsTaken) return;
            if (!campaignSaveManager.HasRoomForConsumable())
            {
                NotificationManager.Instance.ErrorNotification(Text("NoRoomForConsumable"));
                return;
            }
            MarkTaken(SpoilConsumable);
            campaignSaveManager.AquireConsumable(consumableEnum);
            consumableRow.SetTaken(true, true);
            QueueAutoContinue();
        }
        public void ClaimRecruitUnitButtonClicked()
        {
            if (recruitRow.IsTaken || openPicker != Picker.None) return;
            IAudioRequester.Instance.PlaySFX(SFXData.FocusNode);
            // The first open rolls the three units; closing without a pick and reopening shows the same three, so skipping is no reroll.
            if (offeredRecruits == null)
            {
                recruitPanel.LoadRecruitPanelFromBattle(HeroData.GetRaceFromHero(campaignSaveManager.SaveData.heroID), recruitsRarity);
                offeredRecruits = campaignSaveManager.SaveData.recruitableUnits?.ToArray();
            }
            else
            {
                recruitPanel.ReopenRecruitPanelFromBattle(offeredRecruits);
            }
            openPicker = Picker.Recruit;
            HidePanel();
        }
        #endregion

        #region Spoils of war
        private void Choose(EngagementChoiceRow row)
        {
            choiceMade = true;
            if (choiceKeys.TryGetValue(row, out string key)) MarkTaken(SpoilChoice + key);
            view.ChooseRow(row);
        }
        // Taken spoils go in the save, so quitting and continuing shows them taken instead of paying again.
        private bool WasTaken(string spoil) => campaignSaveManager.SaveData.spoilsTaken != null && campaignSaveManager.SaveData.spoilsTaken.Contains(spoil);
        private void MarkTaken(string spoil)
        {
            (campaignSaveManager.SaveData.spoilsTaken ??= new List<string>()).Add(spoil);
            campaignSaveManager.SaveCampaign();
        }
        public void RansomCaptivesButtonClicked(EngagementChoiceRow row)
        {
            if (choiceMade) return;
            campaignSaveManager.RegisterRansomChosen();
            CampaignManager.Instance.GoldManager.ModifyGold(ransomAmount, Text("Ransom Captives"));
            Choose(row);
            QueueAutoContinue();
        }
        public void ConscriptSurvivorsButtonClicked()
        {
            if (choiceMade || openPicker != Picker.None) return;
            recruitPanel.LoadRecruitPanelForConscript(conscriptedUnitNames);
            openPicker = Picker.Conscript;
            HidePanel();
        }
        public void HidePanel()
        {
            engagementPanelCanvasGroup.FadeOutAsync(0.25f);
        }
        public void ConsumeCaptivesButtonClicked(EngagementChoiceRow row)
        {
            if (choiceMade) return;
            campaignSaveManager.ModifyTroopHealth(TabletopTavernConstants.CONSUME_CAPTIVES_HEAL_AMOUNT, Race.DrakosaurBrood);
            Choose(row);
            QueueAutoContinue();
        }
        public void PurgeTheBlightButtonClicked(EngagementChoiceRow row)
        {
            if (choiceMade) return;
            CampaignManager.Instance.CampaignSaveManager.HealRandomUnitToFull();
            Choose(row);
            QueueAutoContinue();
        }
        public void ForbiddenRitualsButtonClicked(EngagementChoiceRow row)
        {
            if (choiceMade) return;
            if(!campaignSaveManager.HasRoomForConsumable())
            {
                NotificationManager.Instance.ErrorNotification(Text("NoRoomForConsumable"));
                return;
            }

            IAudioRequester.Instance.PlaySFX(SFXData.CollectItem);
            CampaignManager.Instance.CampaignSaveManager.AquireConsumable(_generatedConsumbale);
            Choose(row);
            QueueAutoContinue();
        }
        public void HourOfDestinyButtonClicked(EngagementChoiceRow row)
        {
            if (choiceMade) return;
            if(!CampaignManager.Instance.CampaignSaveManager.CheckForRoomToRecruit())
            {
                NotificationManager.Instance.ErrorNotification(Text("Max Units Recruited"));
                return;
            }
            if (CampaignManager.Instance.GoldManager.CurrentGoldAmount >= AchievementRules.ALL_IN_GOLD) SteamAchievements.Unlock(AchievementId.AllIn);
            CampaignManager.Instance.GoldManager.ModifyGold(-CampaignManager.Instance.GoldManager.CurrentGoldAmount, Text("Hour of Destiny"));

            UnitName[] unitNames = TabletopTavernData.Instance.GetSquadsToRecruitBasedOnReputation(0, 1, CampaignManager.Instance.CampaignSaveManager.GetSeededRandom(), CampaignManager.Instance.CampaignSaveManager.GetHeroID());
            CampaignManager.Instance.CampaignSaveManager.RecruitSquad(TabletopTavernData.Instance.GetSquadStats(unitNames[0]));
            IAudioRequester.Instance.PlaySFX(SFXData.RecruitUnit);

            Choose(row);
            QueueAutoContinue();
        }
        public void LootBattlefieldButtonClicked(EngagementChoiceRow row)
        {
            if (choiceMade) return;
            if(!campaignSaveManager.CanAquireGear())
            {
                NotificationManager.Instance.ErrorNotification(Text("No space for gear"));
                return;
            }

            CampaignManager.Instance.CampaignSaveManager.AquireGear(gearID);
            Choose(row);
            QueueAutoContinue();
        }
        public void RaiseDeadButtonClicked(EngagementChoiceRow row)
        {
            if (choiceMade) return;
            if (!campaignSaveManager.CheckForRoomToRecruit())
            {
                NotificationManager.Instance.ErrorNotification(Text("NoRoomForUnit"));
                return;
            }

            foreach (UnitName unitName in raiseDeadUnitList)
            {
                if (!campaignSaveManager.CheckForRoomToRecruit()) break;
                SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(unitName);
                campaignSaveManager.RecruitSquad(squadStats, 1, _viaRaiseDead: true);
            }

            IAudioRequester.Instance.PlaySFX(SFXData.RecruitUnit);
            Choose(row);
            QueueAutoContinue();
        }
        // RecruitPanel calls this when its picker closes. A spoil or a choice counts as taken only when a unit was recruited.
        public void ReturnFromRecruitPanel(bool unitTaken)
        {
            engagementPanelCanvasGroup.FadeInAsync(0.25f);
            Picker closed = openPicker;
            openPicker = Picker.None;
            if (unitTaken && closed == Picker.Recruit)
            {
                MarkTaken(SpoilRecruit);
                recruitRow.SetTaken(true, true);
            }
            if (unitTaken && closed == Picker.Conscript && conscriptRow != null) Choose(conscriptRow);
            QueueAutoContinue();
        }
        // Closes the panel once every spoil is taken and a spoil of war chosen, after the last row has popped.
        private void QueueAutoContinue()
        {
            if (autoContinueQueued || openPicker != Picker.None) return;
            bool spoilsDone = bountyRow.IsTaken && recruitRow.IsTaken && (consumableRow == null || consumableRow.IsTaken);
            if (!spoilsDone || (choiceOffered && !choiceMade)) return;
            autoContinueQueued = true;
            StartCoroutine(AutoContinue());
        }
        private IEnumerator AutoContinue()
        {
            // Long enough to see the last spoil pop before the card flies away.
            yield return new WaitForSecondsRealtime(0.5f);
            CompleteEngagement(false);
        }
        #endregion

        #region Closing
        private void ContinueClicked()
        {
            if (runLost) LoseRun();
            else CompleteEngagement(false);
        }
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
        // The card slides up and off; the panel fades only over the last 30%, so the player sees the slide, not a fade.
        private IEnumerator FadeOutWhileLeaving()
        {
            yield return new WaitForSecondsRealtime(view.CloseSeconds * 0.7f);
            engagementPanelCanvasGroup.FadeOutAsync(view.CloseSeconds * 0.3f);
            closeFade = null;
        }
        public override void ClosePanel()
        {
            Debug.Log("[Map] Closing EngagementPanel");
            IAudioRequester.Instance.PlaySFX(SFXData.CloseUI);
            _showedEngagementResult = false;
            mapSceneUIManager.HUDPanel.HideConsumablesBlocker();
            StartCoroutine(CampaignManager.Instance.MapCamera.LerpFocusedOnNodeVolume(0f, 0.25f));
            view.PlayClose();
            closeFade = StartCoroutine(FadeOutWhileLeaving());
            continueButton.gameObject.SetActive(false);

            HideUnitsSlain();
            campaignSaveManager.MarkEngagementComplete(garrisonFight);

            foreach (Transform child in view.EnemyArmyParent) {
                Destroy(child.gameObject);
            }
            enemySquadsCards.Clear();
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
