using Memori.Scenes;
using Memori.Utilities;
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
        [SerializeField] private MainMenuPanel mainMenuPanel, playPanel, upgradesPanel, questsPanel, libraryPanel, exitPanel, modsPanel;

        [Header("Buttons")]
        [SerializeField] private Button playPanelButton;
        [SerializeField] private Button upgradesPanelButton, questsPanelButton, collectionPanelButton, settingsPanelButton, exitPanelButton, abandonRunButton, customBattleButton, modsPanelButton;
        enum PanelType { Main, Play, Upgrades, Quests, Collection, Options, Exit, Mods }
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
            // questsPanelButton.onClick.AddListener(() => OpenPanel(PanelType.Quests));
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

            questsPanelButton.gameObject.SetActive(false);

            mainMenuPanel.SetUp(this);
            playPanel.SetUp(this);
            upgradesPanel.SetUp(this);
            questsPanel.SetUp(this);
            libraryPanel.SetUp(this);
            exitPanel.SetUp(this);
            modsPanel.SetUp(this);
            SceneHandler.Instance.OnGameStateChanged += OnGameStateChanged;
            SettingsManager.Instance.OnSettingsPanelToggled += OnSettingsPanelToggled;

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
                case PanelType.Mods:
                    currentPanel.ClosePanel();
                    playPanel.gameObject.SetActive(false);
                    modsPanel.OpenPanel();
                    depthField.focusDistance.value = 3f;
                    UpdateCurrentPanel(PanelType.Mods);
                    break;
                case PanelType.Collection:
                    currentPanel.ClosePanel();
                    playPanel.gameObject.SetActive(false);
                    libraryPanel.OpenPanel();
                    depthField.focusDistance.value = 0.1f;
                    UpdateCurrentPanel(PanelType.Collection);
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
                PanelType.Collection => libraryPanel,
                PanelType.Exit => exitPanel,
                PanelType.Mods => modsPanel,
                _ => currentPanel
            };
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

                if (panelToClose != libraryPanel && panelToClose != modsPanel && panelToClose != mainMenuPanel)
                    await Task.Delay(500);

                mainMenuPanel.gameObject.SetActive(true);
                mainMenuPanel.OpenPanel();
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
            collectionPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("collectionButton");
            modsPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("modsButton");
            settingsPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("settingsButton");
            exitPanelButton.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("exitButton");
            playPanelButton.GetComponentInChildren<TMP_Text>().text = campaignSaveDataExists ?
                LocalizationManager.Instance.GetText("continueButton") : LocalizationManager.Instance.GetText("newCampaignButton");

            activeLocaleText.text = LocalizationManager.Instance.GetActiveLocaleName();
            CloseLocalizationPanel();
            libraryPanel.SetUp(this);
        }
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
                SceneHandler.Instance.OnGameStateChanged -= OnGameStateChanged;

            if (LocalizationManager.HasInstance)
                LocalizationManager.Instance.OnLocalizedStringsLoaded -= UpdateButtonText;

            if (SettingsManager.HasInstance)
                SettingsManager.Instance.OnSettingsPanelToggled -= OnSettingsPanelToggled;
        }
    }
}
