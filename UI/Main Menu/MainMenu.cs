using Memori.Input;
using Memori.Scenes;
using Memori.Utilities;
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

namespace TJ.MainMenu
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private MainMenuPanel mainMenuPanel, playPanel, upgradesPanel, questsPanel, runHistoryPanel, leaderboardPanel, exitPanel, modsPanel;

        [Header("Buttons")]
        [SerializeField] private Button playPanelButton;
        [SerializeField] private Button upgradesPanelButton, questsPanelButton, runHistoryPanelButton, leaderboardPanelButton, collectionPanelButton, settingsPanelButton, exitPanelButton, abandonRunButton, customBattleButton, modsPanelButton;

        [Header("Collection Button Badge")]
        [SerializeField] private TMP_Text collectionTotalText;
        [SerializeField] private GameObject collectionUnacknowledgedIndicator;

        [Header("Quests Button Badge")]
        [SerializeField] private TMP_Text questsCompletedText;
        enum PanelType { Main, Play, Upgrades, Quests, RunHistory, Leaderboard, Collection, Options, Exit, Mods }
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
        [SerializeField] private Button openDemoSaveImportButton;

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
            customBattleButton.onClick.AddListener(() => HandleCustomBattle());
            upgradesPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Upgrades));
            questsPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Quests));
            runHistoryPanelButton.onClick.AddListener(() => OpenPanel(PanelType.RunHistory));
            leaderboardPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Leaderboard));
            collectionPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Collection));
            modsPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Mods));
            settingsPanelButton.onClick.AddListener(() => OpenSettingsPanel());
            exitPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Exit));
            closeRoadmapCanvasButton.onClick.AddListener(CloseRoadmapFirstTime);
            roadmapCanvasGroup.CGDisable();
            keepDemoSaveButton.onClick.AddListener(KeepDemoSave);
            deleteDemoSaveButton.onClick.AddListener(DeleteDemoSave);
            openDemoSaveImportButton.onClick.AddListener(OpenDemoSaveImportPrompt);
            demoSaveImportCanvasGroup.CGDisable();

#if !SPELLS
            // The Quests board lists every achievement in the registry, half of which only exist on
            // the Steam backend from the SPELLS release on. Keep it out of the pre-SPELLS menu.
            // Run History and the Leaderboard are revealed with the same release. Recording and
            // the Godking submit run regardless, so both boards are populated the day the buttons appear.
            questsPanelButton.gameObject.SetActive(false);
            runHistoryPanelButton.gameObject.SetActive(false);
            leaderboardPanelButton.gameObject.SetActive(false);
#endif

            mainMenuPanel.SetUp(this);
            playPanel.SetUp(this);
            upgradesPanel.SetUp(this);
            questsPanel.SetUp(this);
            runHistoryPanel.SetUp(this);
            leaderboardPanel.SetUp(this);
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
        }
        private void Load()
        {
            bool isNewPlayer = !SaveDataHandler.PlayerSaveDataExists();
            mainMenuCanvas.enabled = true;
            mainMenuPanel.OpenPanel();
            currentPanel = mainMenuPanel;

            if (!_hasCheckedModCountThisSession)
            {
                _hasCheckedModCountThisSession = true;
                CheckModCountChanged();
            }

            SceneHandler.Instance.AlertOfSceneSetUpComlete();

            if (isNewPlayer)
            {
                roadmapCanvasGroup.CGEnable();
                EventSystem.current.SetSelectedGameObject(closeRoadmapCanvasButton.gameObject);
            }
            else
            {
                FadeInTitle();
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
                case PanelType.Quests:
                    SwitchToQuestsPanel();
                    break;
                case PanelType.RunHistory:
                    SwitchToRunHistoryPanel();
                    break;
                case PanelType.Leaderboard:
                    SwitchToLeaderboardPanel();
                    break;
                case PanelType.Mods:
                    currentPanel.ClosePanel();
                    playPanel.gameObject.SetActive(false);
                    modsPanel.OpenPanel();
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
                PanelType.Quests => questsPanel,
                PanelType.RunHistory => runHistoryPanel,
                PanelType.Leaderboard => leaderboardPanel,
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
                if (panelToClose != mainMenuPanel)
                    panelToClose.ClosePanel();

                // Mods, Quests, Run History and Leaderboard have no fade to wait out, so returning from them is immediate.
                if (panelToClose != modsPanel && panelToClose != questsPanel && panelToClose != runHistoryPanel && panelToClose != leaderboardPanel && panelToClose != mainMenuPanel)
                    await Task.Delay(500);

                mainMenuPanel.gameObject.SetActive(true);
                mainMenuPanel.OpenPanel();
                depthField.focusDistance.value = 9f;

                UpdateCurrentPanel(PanelType.Main);
                // Cheap, and the board is the one place a player goes to look at their count.
                RefreshQuestsButtonState();
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
                upgradesPanel.OpenPanel();
                await Task.Delay(500);
                depthField.focusDistance.value = 3f;
                panelToClose.ClosePanel();
                UpdateCurrentPanel(PanelType.Upgrades);
            }
            finally { _isPanelTransitioning = false; }
        }
        // Same shape as the Mods branch of OpenPanel: no door animation, so no delay either way.
        public void SwitchToQuestsPanel()
        {
            if (_isPanelTransitioning) return;
            currentPanel.ClosePanel();
            questsPanel.OpenPanel();
            depthField.focusDistance.value = 3f;
            UpdateCurrentPanel(PanelType.Quests);
        }
        public void SwitchToRunHistoryPanel()
        {
            if (_isPanelTransitioning) return;
            currentPanel.ClosePanel();
            runHistoryPanel.OpenPanel();
            depthField.focusDistance.value = 3f;
            UpdateCurrentPanel(PanelType.RunHistory);
        }
        public void SwitchToLeaderboardPanel()
        {
            if (_isPanelTransitioning) return;
            currentPanel.ClosePanel();
            leaderboardPanel.OpenPanel();
            depthField.focusDistance.value = 3f;
            UpdateCurrentPanel(PanelType.Leaderboard);
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
            if (SceneHandler.Instance.CurrentGameState == GameStateEnum.MainMenu)
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
            if (abandonRunConfirmationCanvasGroup.canvasGroup.alpha == 1)
            {
                CancelAbandonRun();
                return;
            }
            if (roadmapCanvasGroup.canvasGroup.alpha == 1)
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
                playPanelButton.onClick.RemoveAllListeners();
                playPanelButton.onClick.AddListener(() => LoadMapScene());
                abandonRunButton.gameObject.SetActive(true);
            }
            else
            {
                playPanelButton.onClick.RemoveAllListeners();
                playPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Play));
                abandonRunButton.gameObject.SetActive(false);
            }
            playPanelButton.GetComponentInChildren<TMP_Text>().text = campaignSaveDataExists ? LocalizationManager.Instance.GetText("continueButton") : LocalizationManager.Instance.GetText("newCampaignButton");

        }
        private void AbandonRunConfirmationPopUp()
        {
            currentPanel.ClosePanel();
            abandonRunConfirmationCanvasGroup.CGEnable();
        }
        public void AbandonRun()
        {
            if (SaveDataHandler.CampaignSaveExists())
            {
                var abandonedSave = SaveDataHandler.Load();
                GameEventTracker.RunEnded(abandonedSave.heroID, (int)abandonedSave.difficultyLevel, RunResult.Abandon, abandonedSave.RunStats.chaptersCompleted);
                SaveDataHandler.RecordAbandonedRun(abandonedSave);
            }
            SaveDataHandler.DeleteCampaignSave();
            CheckForCampaignSaveData();
            abandonRunConfirmationCanvasGroup.CGDisable();
            ReturnToMainMenu();
        }
        public void CancelAbandonRun()
        {
            CheckForCampaignSaveData();
            abandonRunConfirmationCanvasGroup.CGDisable();
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
            roadmapCanvasGroup.CGEnable();
        }
        public void CloseRoadmapCanvas()
        {
            roadmapCanvasGroup.CGDisable();
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
            abandonRunButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("abandonRunButton");
            customBattleButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("customBattleButton");
            upgradesPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("upgradesButton");
            questsPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("questsButton");
            runHistoryPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("runHistoryButton");
            leaderboardPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("leaderboardButton");
            collectionPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("collectionButton");
            modsPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("modsButton");
            settingsPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("settingsButton");
            exitPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("exitButton");
            playPanelButton.GetComponentInChildren<TMP_Text>().text = campaignSaveDataExists ?
                LocalizationManager.Instance.GetText("continueButton") : LocalizationManager.Instance.GetText("newCampaignButton");

            activeLocaleText.text = LocalizationManager.Instance.GetActiveLocaleName();
            CloseLocalizationPanel();
            RefreshCollectionButtonState();
            RefreshQuestsButtonState();
        }

        /// <summary>
        /// The completed / total counter under the Quests button. Reads Steam directly; when Steam has
        /// not answered yet it shows 0, and the board itself explains why.
        /// </summary>
        private void RefreshQuestsButtonState()
        {
            SetCompletionCounter(questsCompletedText, QuestsPanel.CountCompleted(), AchievementRegistry.All.Count);
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
                Race.SanguineCourt, Race.SakuraDynasty, Race.DeepstoneHold, Race.DrakosaurBrood
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
        }
        public void CloseLocalizationPanel()
        {
            if (localizationPanelInstance != null)
            {
                Destroy(localizationPanelInstance);
                localizationPanelInstance = null;
                Resources.UnloadUnusedAssets();
            }
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
