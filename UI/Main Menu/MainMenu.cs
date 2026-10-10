using Memori.Input;
using Memori.Scenes;
using Memori.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Memori.SaveData;
using System.Threading.Tasks;
using Memori.UI;
using Memori.Localization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using Memori.Steamworks;
using Memori.Audio;
using UnityEngine.AddressableAssets;
using Memori.Notifications;
using TabletopTavern.Analytics;
using Memori.Tooltip;

namespace TJ.MainMenu
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private MainMenuPanel mainMenuPanel, playPanel, upgradesPanel, exitPanel, modsPanel;

        [Header("Buttons")]
        [SerializeField] private Button playPanelButton;
        [SerializeField] private Button upgradesPanelButton, collectionPanelButton, settingsPanelButton, exitPanelButton, abandonRunButton, customBattleButton, modsPanelButton;

        [Header("Run Summary")]
        [SerializeField] private TMP_Text runSummaryText;
        [SerializeField] private RectTransform continueRow;
        // The Play row grows to fit the hero and act line under its label while a run is in progress.
        private const float PlayButtonHeight = 45f;
        private const float PlayButtonHeightWithSummary = 62f;
        // Play leaves room on its right for the 48 px abandon button and a 6 px gap, so its label shrinks to fit.
        private const float AbandonButtonSpace = 54f;
        private const float PlayLabelMaxSize = 20f;
        private const float PlayLabelMaxSizeWithSummary = 17f;

        [Header("Collection Button Badge")]
        [SerializeField] private TMP_Text collectionTotalText;
        [SerializeField] private GameObject collectionUnacknowledgedIndicator;

        enum PanelType { Main, Play, Upgrades, Collection, Options, Exit, Mods }
        MainMenuPanel currentPanel;
        bool _isPanelTransitioning;
        [SerializeField] private Canvas mainMenuCanvas;
        [SerializeField] private MemoriCanvasGroup titleCanvasGroup;

        [Header("Camera")]
        [SerializeField] private Camera mainMenuCamera;
        [SerializeField] private Camera playPanelCamera;

        [SerializeField] private Volume mainMenuVolume;
        DepthOfField depthField;

        [Header("Abandon Run")]
        public MemoriCanvasGroup abandonRunConfirmationCanvasGroup;
        [SerializeField] private Button abandonRunYesButton, abandonRunNoButton;

        [Header("Demo Only")]
        [SerializeField] private TMP_Text demoSubscript;

        [Header("Roadmap")]
        [SerializeField] private MemoriButtonV2 roadmapButton;
        [SerializeField] private Button closeRoadmapCanvasButton;
        [SerializeField] private MemoriCanvasGroup roadmapCanvasGroup;

        [Header("Demo Save Import")]
        [SerializeField] private MemoriCanvasGroup demoSaveImportCanvasGroup;
        [SerializeField] private Button keepDemoSaveButton, deleteDemoSaveButton;

        [Header("Steam Deck Feedback")]
        [SerializeField] private SteamDeckFeedbackPopup steamDeckFeedbackPopup;

        [Header("Juice")]
        [SerializeField] private RectTransform menuColumn;
        [SerializeField] private RectTransform menuFooter;
        [SerializeField] private RectTransform modsBody;
        [SerializeField] private RectTransform roadmapBody;

        [Header("Localization")]
        [SerializeField] private Button openLocalizationPanelButton;
        [SerializeField] private AssetReferenceGameObject localizationPanelRef;
        [SerializeField] private TMP_Text activeLocaleText;
        private GameObject localizationPanelInstance;
        bool campaignSaveDataExists;
        private const string DEMO_SAVE_PROMPT_KEY = "demo_save_prompt_shown";
        private const string LAST_LOADED_MOD_COUNT_KEY = "last_loaded_mod_count";
        private bool _hasCheckedModCountThisSession;
        private void Awake()
        {
            depthField = mainMenuVolume.profile.TryGet<DepthOfField>(out depthField) ? depthField : null;
            abandonRunButton.onClick.AddListener(AbandonRunConfirmationPopUp);
            abandonRunYesButton.onClick.AddListener(AbandonRun);
            abandonRunNoButton.onClick.AddListener(CancelAbandonRun);
            // The pop-up's two buttons are plain Buttons without the Button Base sounds and hover motion.
            abandonRunYesButton.onClick.AddListener(PlayButtonClick);
            abandonRunNoButton.onClick.AddListener(PlayButtonClick);
            UIHoverBloom.Attach(abandonRunYesButton.gameObject);
            UIHoverBloom.Attach(abandonRunNoButton.gameObject);
            customBattleButton.onClick.AddListener(() => HandleCustomBattle());
            upgradesPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Upgrades));
            collectionPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Collection));
            modsPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Mods));
            settingsPanelButton.onClick.AddListener(() => OpenSettingsPanel());
            exitPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Exit));
            closeRoadmapCanvasButton.onClick.AddListener(CloseRoadmapFirstTime);
            roadmapCanvasGroup.CGDisable();
            keepDemoSaveButton.onClick.AddListener(KeepDemoSave);
            deleteDemoSaveButton.onClick.AddListener(DeleteDemoSave);
            demoSaveImportCanvasGroup.CGDisable();

            mainMenuPanel.SetUp(this);
            playPanel.SetUp(this);
            upgradesPanel.SetUp(this);
            exitPanel.SetUp(this);
            modsPanel.SetUp(this);
            SceneHandler.Instance.OnGameStateChanged += OnGameStateChanged;
            SceneHandler.Instance.OnOverlaySceneClosed += RefreshCollectionButtonState;
            SettingsManager.Instance.OnSettingsPanelToggled += OnSettingsPanelToggled;
            InputHandler.Instance.SecondaryActionPressed += OnSecondaryActionPressed;

            UpdateButtonText();
            LocalizationManager.Instance.OnLocalizedStringsLoaded += UpdateButtonText;
            openLocalizationPanelButton.onClick.AddListener(OpenLocalizationPanel);

            roadmapButton.Button.onClick.AddListener(() => OpenRoadmapCanvas());

            CheckForCampaignSaveData();
            titleCanvasGroup.CGDisable();

            #if !DEMO
                demoSubscript.enabled = false;
            #endif

            #if TESTING
                AddActThreeVictoryButton();
            #endif
        }
#if TESTING
        #region Playtest boot
        // A copy of Custom Battle placed under it, so the main menu scene needs no edit for playtest builds.
        private void AddActThreeVictoryButton()
        {
            Button button = Instantiate(customBattleButton, customBattleButton.transform.parent);
            button.name = "Test Endless Mode Button";
            button.transform.SetSiblingIndex(customBattleButton.transform.GetSiblingIndex() + 1);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(StartActThreeVictory);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.text = "Test Endless Mode";
            label.color = new Color32(0xFF, 0xF3, 0xD2, 0xFF);

            // The Button Base hue recipe (ui-buttons.md) worked from yellow (0.95, 0.80, 0.30).
            TintLayer(button.transform, "SPR_Background", new Color(0.22f, 0.20f, 0.105f, 1f));
            TintLayer(button.transform, "Gradient", new Color(0.95f, 0.80f, 0.30f, 10f / 255f));
            TintLayer(button.transform, "Highlight Image", new Color(1f, 0.88f, 0.45f, 20f / 255f));
            TintLayer(button.transform, "Texture", new Color(0.73f, 0.66f, 0.42f, 0.08f));
            TintLayer(button.transform, "Selection Highlight", new Color(1f, 0.85f, 0.30f, 1f));
        }

        private static void TintLayer(Transform button, string layer, Color color)
        {
            Transform child = button.Find("Background/UI Assets/" + layer);
            if (child == null)
            {
                Debug.LogWarning($"[MainMenu] Test Endless Mode button has no {layer} layer to tint.");
                return;
            }
            child.GetComponent<Image>().color = color;
        }

        private void StartActThreeVictory()
        {
            if (_isPanelTransitioning) return;
#if UNITY_EDITOR
            // In the Editor the run goes to the dev folder, never the real save folder.
            SaveDataHandler.SetSaveRoot(ActThreeVictoryTestSave.PrepareFolder());
#endif
            ActThreeVictoryTestSave.WriteRun();
            LoadMapScene();
        }
        #endregion
#endif
        private async void Load()
        {
            bool isNewPlayer = !SaveDataHandler.PlayerSaveDataExists();
            mainMenuCanvas.enabled = true;
            mainMenuPanel.OpenPanel();
            HideMenuForDeal();
            currentPanel = mainMenuPanel;

            if (!_hasCheckedModCountThisSession)
            {
                _hasCheckedModCountThisSession = true;
                CheckModCountChanged();
            }

            // Restart paths pass through here on their way to the Map or a battle and must not wait.
            if (SceneHandler.Instance.CurrentGameState == GameStateEnum.MainMenu)
                await WaitForTavernTheme();

            SceneHandler.Instance.AlertOfSceneSetUpComlete();
            DealInMenu(() => SceneHandler.Instance.DoorOpenProgress, SceneDoorDealAt, SceneDoorWaitLimit);

            if (isNewPlayer)
            {
                roadmapCanvasGroup.CGEnable();
                EventSystem.current.SetSelectedGameObject(closeRoadmapCanvasButton.gameObject);
            }
            else
            {
                // The first-time close is for the welcome pop-up; a returning player's Roadmap closes like any pop-up.
                closeRoadmapCanvasButton.onClick.RemoveListener(CloseRoadmapFirstTime);
                closeRoadmapCanvasButton.onClick.RemoveListener(CloseRoadmapCanvas);
                closeRoadmapCanvasButton.onClick.AddListener(CloseRoadmapCanvas);
                FadeInTitle();
                // A new player's first boot already has the roadmap pop-up.
                steamDeckFeedbackPopup.TryShow();
            }

            // if (PlayerPrefs.GetInt(DEMO_SAVE_PROMPT_KEY, 0) == 0 && SaveDataHandler.PlayerSaveDataExists())
            // {
            //     demoSaveImportCanvasGroup.CGEnable();
            //     EventSystem.current.SetSelectedGameObject(keepDemoSaveButton.gameObject);
            // }
            // else
            // {
            //     PlayerPrefs.SetInt(DEMO_SAVE_PROMPT_KEY, 1);
            //     PlayerPrefs.Save();
            //     OpenMainMenuPanel();
            // }

        }
        private const int TavernThemeWaitLimitMs = 10000;

        // The doors stay shut until the theme's figures are placed; the limit stops a stuck load from trapping the player.
        private static async Task WaitForTavernTheme()
        {
            TavernThemeManager themeManager = TavernThemeManager.InstanceIfExists;
            if (themeManager == null) return;

            Task themeLoad = themeManager.BootThemeLoaded;
            if (themeLoad.IsCompleted) return;

            if (await Task.WhenAny(themeLoad, Task.Delay(TavernThemeWaitLimitMs)) != themeLoad)
                Debug.LogWarning($"[MainMenu] Tavern theme still loading after {TavernThemeWaitLimitMs} ms, opening the doors anyway.");
        }
        private async void CheckModCountChanged()
        {
            int currentModCount = ModLoadOrder.GetEnabledModFolderPathsInOrder().Count;
            int lastModCount = PlayerPrefs.GetInt(LAST_LOADED_MOD_COUNT_KEY, -1);

            PlayerPrefs.SetInt(LAST_LOADED_MOD_COUNT_KEY, currentModCount);
            PlayerPrefs.Save();

            if (lastModCount != -1 && lastModCount != currentModCount)
            {
                await Task.Delay(2000);
                NotificationManager.Instance.DisplayNotification(string.Format(LocalizationManager.Instance.GetText("modsCountChanged"), currentModCount));
            }
        }
        private async void FadeInTitle()
        {
            IAudioRequester.Instance.PlayMenuMusic();
            await Task.Delay(500);
            if (titleCanvasGroup != null)
                titleCanvasGroup.FadeInAsync(3f);
        }
        private void OpenPanel(PanelType panelType)
        {
            // Debug.Log($"Opening panel: {panelType} from panel: {currentPanel}");
            switch (panelType)
            {
                case PanelType.Main:
                    SwitchToMainMenuPanel();
                    break;
                case PanelType.Play:
                    SwitchToPlayPanel();
                    break;
                case PanelType.Upgrades:
                    SwitchToUpgradesPanel();
                    break;
                case PanelType.Mods:
                    currentPanel.ClosePanel();
                    playPanel.gameObject.SetActive(false);
                    modsPanel.OpenPanel();
                    // The menu is hidden at once and Mods fades in, the run setup swap rule.
                    StartPanelOpen(modsPanel.GetComponent<CanvasGroup>(), modsBody);
                    depthField.focusDistance.value = 3f;
                    UpdateCurrentPanel(PanelType.Mods);
                    break;
                case PanelType.Collection:
                    // The Collection is its own additive overlay scene now. The menu deliberately
                    // stays open behind it, so currentPanel must not move and the depth-of-field
                    // blur is dropped - the overlay's own scrim covers the backdrop.
                    _ = SceneHandler.Instance.OpenOverlayScene(SceneHandler.CollectionScenePath);
                    break;
                case PanelType.Exit:
                    ExitToDesktop();
                    break;
            }
        }
        private void UpdateCurrentPanel(PanelType panelType)
        {
            currentPanel = panelType switch
            {
                PanelType.Main => mainMenuPanel,
                PanelType.Play => playPanel,
                PanelType.Upgrades => upgradesPanel,
                PanelType.Exit => exitPanel,
                PanelType.Mods => modsPanel,
                _ => currentPanel
            };
            // The Play panel is still the tavern (a camera move); the board-style panels cover it.
            bool coversTavern = currentPanel != null && currentPanel != mainMenuPanel && currentPanel != playPanel;
            IAudioRequester.Instance.SetAmbienceDuck(AmbienceDuckSource.MenuPanel, coversTavern);
        }
        public async void SwitchToMainMenuPanel()
        {
            if (_isPanelTransitioning) return;
            _isPanelTransitioning = true;
            try
            {
                MainMenuPanel panelToClose = currentPanel;
                StopPanelOpen();
                if (panelToClose != mainMenuPanel)
                    panelToClose.ClosePanel();

                // Mods has no fade to wait out, so returning from it is immediate.
                if (panelToClose != modsPanel && panelToClose != mainMenuPanel)
                    await Task.Delay(500);

                // Settings toggles land here with the menu still showing; only a hidden menu deals back in.
                bool menuWasHidden = MenuGroup.alpha < 0.99f;
                mainMenuPanel.gameObject.SetActive(true);
                mainMenuPanel.OpenPanel();
                if (menuWasHidden)
                {
                    // The logo and news card fade back with the panel while the column deals in.
                    StartPanelOpen(MenuGroup, null);
                    DealInMenu(() => SceneHandler.Instance.CameraDoorProgress, CameraDoorDealAt, CameraDoorWaitLimit);
                }
                depthField.focusDistance.value = 9f;

                UpdateCurrentPanel(PanelType.Main);
            }
            finally { _isPanelTransitioning = false; }
        }
        public async void SwitchToPlayPanel()
        {
            if (_isPanelTransitioning) return;
            _isPanelTransitioning = true;
            try
            {
                MainMenuPanel panelToClose = currentPanel;
                StopPanelOpen();
                playPanel.OpenPanel();
                await Task.Delay(500);
                depthField.focusDistance.value = 3.82f;
                panelToClose.ClosePanel();
                UpdateCurrentPanel(PanelType.Play);
            }
            finally { _isPanelTransitioning = false; }
        }
        public async void SwitchToUpgradesPanel()
        {
            if (_isPanelTransitioning) return;
            _isPanelTransitioning = true;
            try
            {
                MainMenuPanel panelToClose = currentPanel;
                StopPanelOpen();
                upgradesPanel.OpenPanel();
                await Task.Delay(500);
                depthField.focusDistance.value = 3f;
                panelToClose.ClosePanel();
                UpdateCurrentPanel(PanelType.Upgrades);
            }
            finally { _isPanelTransitioning = false; }
        }
        public void OpenSettingsPanel()
        {
            SettingsManager.Instance.OpenSettingsPanel();
        }
        public void ExitToDesktop()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void ReturnToMainMenu()
        {
            OpenPanel(PanelType.Main);
        }
        public void LoadBattleScene()
        {
            SceneHandler.Instance.SwitchGameState(GameStateEnum.Battle);
        }
        public void LoadMapScene()
        {
            if (_isPanelTransitioning) return;
            _isPanelTransitioning = true;
            EnterMap();
        }
        private void EnterMap()
        {
            PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
            saveData.customBattle = false;
            SaveDataHandler.SavePlayerSaveData(saveData);

            Hero hero = HeroData.GetHeroByID(saveData.lastHeroID);
            IAudioRequester.Instance.SetAudioThemePack((int)hero.Race);

            SceneHandler.Instance.SwitchGameState(GameStateEnum.Map);
        }
        private void OnGameStateChanged(GameStateEnum gameStateEnum)
        {
            if (gameStateEnum.Equals(GameStateEnum.MainMenu))
                Load();
        }
        private void OnSettingsPanelToggled(bool isOpen)
        {
            if (SceneHandler.Instance.CurrentGameState != GameStateEnum.MainMenu) return;
            // Esc in the main menu arrives here; it closes the Steam Deck popup before anything else.
            if (isOpen && steamDeckFeedbackPopup.IsOpen)
            {
                steamDeckFeedbackPopup.Close();
                return;
            }
            ReturnToMainMenu();
        }
        /// <summary>
        /// Right-click is a "back" gesture anywhere in the menu, the mouse twin of Esc (which
        /// reaches ReturnToMainMenu via SettingsHotkeyPressed -> OnSettingsPanelToggled). It unwinds
        /// the topmost thing first: a pop-up over the menu, then a panel's own inner state via
        /// TryStepBack, and only then the panel itself.
        /// </summary>
        private void OnSecondaryActionPressed()
        {
            // Live read: this object survives ~1s into a transition out of the menu.
            if (SceneHandler.Instance.CurrentGameState != GameStateEnum.MainMenu) return;
            // The Collection overlay owns input while it is up and closes itself on right-click.
            if (SceneHandler.Instance.OverlaySceneOpen) return;

            if (SettingsManager.Instance.SettingsPanelOpen)
            {
                SettingsManager.Instance.CloseSettingsPanel();
                return;
            }
            if (steamDeckFeedbackPopup.IsOpen)
            {
                steamDeckFeedbackPopup.Close();
                return;
            }
            if (abandonRunConfirmationCanvasGroup.alpha == 1)
            {
                CancelAbandonRun();
                return;
            }
            if (roadmapCanvasGroup.alpha == 1)
            {
                // Whichever close handler is currently bound (first-time or regular).
                closeRoadmapCanvasButton.onClick.Invoke();
                return;
            }
            if (localizationPanelInstance != null)
            {
                CloseLocalizationPanel();
                return;
            }

            // currentPanel is first assigned in Load(), which runs after the scene has loaded,
            // while CurrentGameState already reads MainMenu from Awake onward.
            if (currentPanel == null || currentPanel == mainMenuPanel) return;
            if (currentPanel.TryStepBack()) return;

            ReturnToMainMenu();
        }
        private void CheckForCampaignSaveData()
        {
            campaignSaveDataExists = SaveDataHandler.CampaignSaveExists();

            if (campaignSaveDataExists)
            {
                playPanelButton.ClearClickListeners();
                playPanelButton.onClick.AddListener(ContinueRun);
                abandonRunButton.gameObject.SetActive(true);
            }
            else
            {
                playPanelButton.ClearClickListeners();
                playPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Play));
                abandonRunButton.gameObject.SetActive(false);
            }
            playPanelButton.GetComponentInChildren<TMP_Text>().text = campaignSaveDataExists ? LocalizationManager.Instance.GetText("continueButton") : LocalizationManager.Instance.GetText("newCampaignButton");
            RefreshRunSummary();
        }
        /// <summary>
        /// The hero and act line under Continue. Reads the campaign file directly; it is not cached.
        /// </summary>
        private void RefreshRunSummary()
        {
            RectTransform playRect = (RectTransform)playPanelButton.transform;
            TMP_Text playLabel = playPanelButton.GetComponentInChildren<TMP_Text>();
            Vector4 labelMargin = playLabel.margin;
            if (!campaignSaveDataExists)
            {
                runSummaryText.gameObject.SetActive(false);
                continueRow.sizeDelta = new Vector2(continueRow.sizeDelta.x, PlayButtonHeight);
                playRect.offsetMax = new Vector2(0f, playRect.offsetMax.y);
                playLabel.fontSizeMax = PlayLabelMaxSize;
                playLabel.margin = new Vector4(labelMargin.x, labelMargin.y, labelMargin.z, 0f);
                return;
            }

            CampaignSaveData save = SaveDataHandler.Load();
            string heroName = LocalizationManager.Instance.GetText(HeroData.GetHeroByID(save.heroID).HeroName);
            string act = LocalizationManager.Instance.GetText("Act");
            runSummaryText.text = $"{heroName}  -  {act} {save.bookNumber}";
            runSummaryText.gameObject.SetActive(true);
            continueRow.sizeDelta = new Vector2(continueRow.sizeDelta.x, PlayButtonHeightWithSummary);
            playRect.offsetMax = new Vector2(-AbandonButtonSpace, playRect.offsetMax.y);
            playLabel.fontSizeMax = PlayLabelMaxSizeWithSummary;
            // Lifts the label so the summary line fits under it.
            playLabel.margin = new Vector4(labelMargin.x, labelMargin.y, labelMargin.z, PlayButtonHeightWithSummary - PlayButtonHeight);
        }
        private void AbandonRunConfirmationPopUp()
        {
            TooltipManager.Instance.HideTooltip();
            StopPanelOpen();
            currentPanel.ClosePanel();
            OpenPopup(abandonRunConfirmationCanvasGroup, (RectTransform)abandonRunConfirmationCanvasGroup.transform);
        }
        public void AbandonRun()
        {
            if (SaveDataHandler.CampaignSaveExists())
            {
                var abandonedSave = SaveDataHandler.Load();
                GameEventTracker.RunClosed(abandonedSave, RunResult.Abandon, "abandonMenu");
                SaveDataHandler.RecordAbandonedRun(abandonedSave);
            }
            SaveDataHandler.DeleteCampaignSave();
            CheckForCampaignSaveData();
            ClosePopup(abandonRunConfirmationCanvasGroup);
            ReturnToMainMenu();
        }
        public void CancelAbandonRun()
        {
            CheckForCampaignSaveData();
            ClosePopup(abandonRunConfirmationCanvasGroup);
            ReturnToMainMenu();
        }
        [ContextMenu("Check Files")]
        public void CheckFiles()
        {
            SaveDataHandler.OpenSaveFolder();
        }
        public void CloseRoadmapFirstTime()
        {
            roadmapCanvasGroup.FadeOutAsync(0.5f);
            FadeInTitle();
            OpenMainMenuPanel();
            closeRoadmapCanvasButton.onClick.RemoveListener(CloseRoadmapFirstTime);
            closeRoadmapCanvasButton.onClick.AddListener(CloseRoadmapCanvas);
        }
        public void OpenRoadmapCanvas()
        {
            OpenPopup(roadmapCanvasGroup, roadmapBody);
        }
        public void CloseRoadmapCanvas()
        {
            ClosePopup(roadmapCanvasGroup);
            OpenMainMenuPanel();
        }
        private void HandleCustomBattle()
        {
            if (_isPanelTransitioning) return;
            _isPanelTransitioning = true;

            PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
            saveData.customBattle = true;
            SaveDataHandler.SavePlayerSaveData(saveData);
            SceneHandler.Instance.SwitchGameState(GameStateEnum.Battle);
        }
        private void UpdateButtonText()
        {
            abandonRunButton.GetComponent<MemoriTooltipTrigger>().SetUpToolTip(LocalizationManager.Instance.GetText("abandonRunButton"));
            customBattleButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("customBattleButton");
            upgradesPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("upgradesButton");
            collectionPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("collectionButton");
            modsPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("modsButton");
            settingsPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("settingsButton");
            exitPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("exitButton");
            playPanelButton.GetComponentInChildren<TMP_Text>().text = campaignSaveDataExists ?
                LocalizationManager.Instance.GetText("continueButton") : LocalizationManager.Instance.GetText("newCampaignButton");
            RefreshRunSummary();

            activeLocaleText.text = LocalizationManager.Instance.GetActiveLocaleName();
            CloseLocalizationPanel();
            RefreshCollectionButtonState();
        }

        /// <summary>
        /// Drives the counter and the unacknowledged dot on the Collection button. This used to be
        /// computed by CollectionPanel, which is now in its own scene and cannot reach these two
        /// objects. Runs on every localization reload and whenever the overlay closes.
        /// </summary>
        private void RefreshCollectionButtonState()
        {
            // The eight collectable races. Deliberately not Enum.GetValues(typeof(Race)), which
            // includes Race.Special - structures only, with no collection entry.
            Race[] collectableRaces =
            {
                Race.IronLegion, Race.Gruntkin, Race.RavenHost, Race.TaelindorForest,
                Race.SanguineCourt, Race.SakuraDynasty, Race.DeepstoneHold, Race.DrakosaurBrood,
#if FACTIONUPDATE
                Race.OlympianLeague,
#endif
            };

            GearID[] allGear = GearData.GetGearIDs();
            ConsumableEnum[] consumables = ConsumableData.GetAllConsumableEnums();
            List<int> gearCollected = SaveDataHandler.GetGearIDsCollected();
            List<int> potionsCollected = SaveDataHandler.GetPotionsIDsCollected();
            List<int> gearAcknowledged = SaveDataHandler.GetGearIDsAcknowledged();
            List<int> potionsAcknowledged = SaveDataHandler.GetPotionsIDsAcknowledged();
            List<UnitName> troopsCollected = SaveDataHandler.GetTroopsIDsCollected();
            List<UnitName> troopsAcknowledged = SaveDataHandler.GetTroopsIDsAcknowledged();

            int totalCollected = gearCollected.Count + potionsCollected.Count;
            int totalAvailable = allGear.Length + consumables.Length;

            bool unacknowledgedAnything =
                gearCollected.Any(id => !gearAcknowledged.Contains(id)) ||
                potionsCollected.Any(id => !potionsAcknowledged.Contains(id));

            foreach (Race race in collectableRaces)
            {
                UnitName[] units = TabletopTavernData.Instance.GetUnitsOfRace(race);
                totalCollected += troopsCollected.Count(u => units.Contains(u));
                totalAvailable += units.Length;

                if (units.Where(u => troopsCollected.Contains(u)).Any(u => !troopsAcknowledged.Contains(u)))
                    unacknowledgedAnything = true;
            }

            SetCompletionCounter(collectionTotalText, totalCollected, totalAvailable);
            collectionUnacknowledgedIndicator.SetActive(unacknowledgedAnything);
        }

        #region Completion counters
        // Authored colour of each counter label, captured the first time it is written, so a counter
        // that drops back below complete (a dev achievement reset, a mod adding units) returns to
        // exactly what the prefab shipped with rather than a guessed default.
        private readonly Dictionary<TMP_Text, Color> _counterBaseColors = new();

        /// <summary>
        /// Writes "done/total" and turns the label legendary gold once the set is complete.
        /// </summary>
        private void SetCompletionCounter(TMP_Text label, int done, int total)
        {
            if (!_counterBaseColors.TryGetValue(label, out Color baseColor))
            {
                baseColor = label.color;
                _counterBaseColors[label] = baseColor;
            }

            label.text = $"{done}/{total}";
            label.color = total > 0 && done >= total ? (Color)ColorData.GetRarityTierColor(UnitRarity.Legendary) : baseColor;
        }
        #endregion
        private async void OpenLocalizationPanel()
        {
            if (localizationPanelInstance == null)
            {
                GameObject prefab = await AddressablesManager.Instance.LoadAsync<GameObject>(localizationPanelRef);
                localizationPanelInstance = Instantiate(prefab, mainMenuCanvas.transform);
            }
            localizationPanelInstance.SetActive(true);

            CanvasGroup group = localizationPanelInstance.GetComponent<CanvasGroup>();
            if (group == null) group = localizationPanelInstance.AddComponent<CanvasGroup>();
            // The flag grid rises; the dim behind it only fades, so it never shows an undimmed strip.
            StartCoroutine(UIJuice.Open(group, localizationPanelInstance.transform.Find("Grid") as RectTransform));
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
        }
        public void CloseLocalizationPanel()
        {
            if (localizationPanelInstance == null)
            {
                AddressablesManager.Instance.Release(localizationPanelRef.AssetGUID);
                return;
            }
            GameObject panel = localizationPanelInstance;
            localizationPanelInstance = null;
            IAudioRequester.Instance.PlaySFX(SFXData.ClosePopUp);
            StartCoroutine(FadeOutLocalizationPanel(panel));
        }
        private IEnumerator FadeOutLocalizationPanel(GameObject panel)
        {
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
                yield return UIJuice.Close(group);
            }
            Destroy(panel);
            Resources.UnloadUnusedAssets();
            // Reopened during the fade: the new panel still needs the asset.
            if (localizationPanelInstance == null)
                AddressablesManager.Instance.Release(localizationPanelRef.AssetGUID);
        }
        private void OpenMainMenuPanel()
        {
            mainMenuPanel.OpenPanel();
        }
        public void OpenDemoSaveImportPrompt()
        {
            demoSaveImportCanvasGroup.CGEnable();
            EventSystem.current.SetSelectedGameObject(keepDemoSaveButton.gameObject);
        }
        public void KeepDemoSave()
        {
            PlayerPrefs.SetInt(DEMO_SAVE_PROMPT_KEY, 1);
            PlayerPrefs.Save();
            demoSaveImportCanvasGroup.CGDisable();
            OpenMainMenuPanel();
        }
        public void DeleteDemoSave()
        {
            Debug.Log("[DELETE SAVE DATA] Deleting save data...");
            SaveDataHandler.DeletePlayerSaveData();
            SceneHandler.Instance.SwitchGameState(GameStateEnum.MainMenu);
        }
        #region Menu deal-in
        // TJ's block deal from run setup: one block every 0.1 s, each over 0.3 s.
        private const float DealStep = 0.1f;
        private const float DealTime = 0.3f;
        // The battle HUD's door reading; the camera door opens over its last third, as PlayPanel reads it.
        private const float SceneDoorDealAt = 0.4f;
        private const float SceneDoorWaitLimit = 1.5f;
        private const float CameraDoorDealAt = 0.67f;
        private const float CameraDoorWaitLimit = 2.5f;
        private Coroutine _deal;
        private Coroutine _panelOpen;
        private CanvasGroup _menuGroup;
        private CanvasGroup MenuGroup
        {
            get
            {
                if (_menuGroup == null) _menuGroup = mainMenuPanel.GetComponent<CanvasGroup>();
                return _menuGroup;
            }
        }

        // One panel fade at a time: a fade left running would bring back a panel that has since been hidden.
        private void StartPanelOpen(CanvasGroup group, RectTransform body)
        {
            StopPanelOpen();
            _panelOpen = StartCoroutine(UIJuice.Open(group, body));
        }

        private void StopPanelOpen()
        {
            if (_panelOpen == null) return;
            StopCoroutine(_panelOpen);
            _panelOpen = null;
        }

        // The column's buttons in order, then the footer, which arrives with the first button.
        private void CollectDealItems(List<RectTransform> items, List<float> delays)
        {
            if (menuColumn == null || menuFooter == null)
            {
                Debug.LogError("[MainMenu] menuColumn or menuFooter is not set; the menu shows without its deal-in.");
                return;
            }
            foreach (Transform child in menuColumn)
            {
                // Spacers such as Group Gap have no children and take no part.
                if (!child.gameObject.activeSelf || child.childCount == 0) continue;
                delays.Add(items.Count * DealStep);
                items.Add((RectTransform)child);
            }
            items.Add(menuFooter);
            delays.Add(0f);
        }

        // Hidden as the panel opens, so nothing shows before the door lets the deal start.
        private void HideMenuForDeal()
        {
            var items = new List<RectTransform>();
            CollectDealItems(items, new List<float>());
            foreach (RectTransform item in items)
            {
                CanvasGroup group = item.GetComponent<CanvasGroup>();
                if (group == null) group = item.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
            }
        }

        private void DealInMenu(System.Func<float> doorProgress, float dealAt, float waitLimit)
        {
            var items = new List<RectTransform>();
            var delays = new List<float>();
            CollectDealItems(items, delays);
            HideMenuForDeal();
            if (_deal != null) StopCoroutine(_deal);
            _deal = StartCoroutine(DealWhenDoorOpens(items, delays, doorProgress, dealAt, waitLimit));
        }

        private IEnumerator DealWhenDoorOpens(List<RectTransform> items, List<float> delays, System.Func<float> doorProgress, float dealAt, float waitLimit)
        {
            // The door is told to open after this starts, so its progress is read from the next frame.
            yield return null;
            float waited = 0f;
            while (doorProgress() < dealAt && waited < waitLimit)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return UIJuice.Reveal(items, delays, DealTime);
            _deal = null;
        }
        #endregion

        #region Pop-ups
        private readonly Dictionary<MemoriCanvasGroup, Coroutine> _popupMotion = new();

        private void OpenPopup(MemoriCanvasGroup popup, RectTransform body)
        {
            StopPopupMotion(popup);
            popup.CGEnable();
            _popupMotion[popup] = StartCoroutine(UIJuice.Open(popup.GetComponent<CanvasGroup>(), body));
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
        }

        private void ClosePopup(MemoriCanvasGroup popup)
        {
            StopPopupMotion(popup);
            if (popup.alpha <= 0f)
            {
                popup.CGDisable();
                return;
            }
            popup.interactable = false;
            popup.blocksRaycasts = false;
            IAudioRequester.Instance.PlaySFX(SFXData.ClosePopUp);
            _popupMotion[popup] = StartCoroutine(FadeOutPopup(popup));
        }

        private IEnumerator FadeOutPopup(MemoriCanvasGroup popup)
        {
            yield return UIJuice.Close(popup.GetComponent<CanvasGroup>());
            popup.CGDisable();
            _popupMotion.Remove(popup);
        }

        private void StopPopupMotion(MemoriCanvasGroup popup)
        {
            if (!_popupMotion.TryGetValue(popup, out Coroutine motion)) return;
            if (motion != null) StopCoroutine(motion);
            _popupMotion.Remove(popup);
        }

        private static void PlayButtonClick() => IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
        #endregion

        #region Continue send-off
        // The send-off's shape from run setup: the menu holds 0.2 s on the flare, then fades before the door closes.
        private const float SendOffHold = 0.2f;
        private const float SendOffFadeTime = 0.35f;

        private void ContinueRun()
        {
            if (_isPanelTransitioning) return;
            _isPanelTransitioning = true;
            StartCoroutine(ContinueSendOff());
        }

        private IEnumerator ContinueSendOff()
        {
            StopPanelOpen();
            CanvasGroup menu = MenuGroup;
            menu.interactable = false;
            menu.blocksRaycasts = false;
            TooltipManager.Instance.HideTooltip();

            Hero hero = HeroData.GetHeroByID(SaveDataHandler.LoadPlayerSaveData().lastHeroID);
            IAudioRequester.Instance.PlayBattleTheme((int)hero.Race);
            StartCoroutine(UIJuice.Punch(playPanelButton.transform));

            yield return new WaitForSecondsRealtime(SendOffHold);
            yield return UIJuice.Close(menu, SendOffFadeTime);
            EnterMap();
        }
        #endregion

        private void OnDestroy()
        {
            if (SceneHandler.HasInstance)
            {
                SceneHandler.Instance.OnGameStateChanged -= OnGameStateChanged;
                SceneHandler.Instance.OnOverlaySceneClosed -= RefreshCollectionButtonState;
            }

            if (LocalizationManager.HasInstance)
                LocalizationManager.Instance.OnLocalizedStringsLoaded -= UpdateButtonText;

            if (SettingsManager.HasInstance)
                SettingsManager.Instance.OnSettingsPanelToggled -= OnSettingsPanelToggled;

            if (InputHandler.HasInstance)
                InputHandler.Instance.SecondaryActionPressed -= OnSecondaryActionPressed;
        }
    }
}
