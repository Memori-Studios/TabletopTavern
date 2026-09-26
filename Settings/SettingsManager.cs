using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Memori.Utilities;
using UnityEngine.UI;
using Memori.Scenes;
using TJ.Settings;
using Memori.SaveData;
using Memori.Notifications;
using Memori.Audio;
using Memori.Input;
using Memori.Localization;
using System;
using Memori.Core;
using Memori.UI;
using Memori.Tooltip;
using TabletopTavern.Analytics;
using UnityEngine.SceneManagement;
using TMPro;

namespace TJ
{
    public class SettingsManager : Memori.Utilities.Singleton<SettingsManager>
    {
        [SerializeField] private MemoriCanvasGroup settingsCanvasGroup;

        [Header("Main Buttons")]
        [SerializeField] private Button resumeGameButton;
        [SerializeField] private Button exitToMenuButton, exitToDesktopButton, abandonRunButton, quickRestartButton, creditsButton, concedeDefeatButton, collectionButton;

        [Header("Delete All Progress")]
        [SerializeField] private Button deleteProgressButton;
        [SerializeField] private GameObject deleteProgressGroup;

        [Header("Codex Rail")]
        [SerializeField] private RailEntry[] railEntries;
        [SerializeField] private Button devToolsButton;
        [SerializeField] private MemoriCanvasGroup devToolsCanvasGroup;
        [SerializeField] private TMP_Text closeLabel;

        [Serializable]
        private struct RailEntry
        {
            public MemoriCanvasGroup page;
            public TJ.MainMenu.CollectionRailRow row;
        }

        [Header("Abandon Run")]
        [SerializeField] private MemoriCanvasGroup abandonRunConfirmationCanvasGroup;
        [SerializeField] private AbandonRunButton abandonRunConfirmationButton;
        [SerializeField] private Button abandonRunCancelButton;

        [Header("Quick Restart")]
        [SerializeField] private MemoriCanvasGroup quickRestartConfirmationCanvasGroup;
        [SerializeField] private Button quickRestartConfirmationButton, quickRestartCancelButton;

        [Header("Concede Defeat")]
        [SerializeField] private MemoriCanvasGroup concedeDefeatConfirmationCanvasGroup;
        [SerializeField] private Button concedeDefeatConfirmationButton, concedeDefeatCancelButton;

        [Header("Settings")]
        [SerializeField] private Button infoButton;
        [SerializeField] private Button gameSettingsButton, audioSettingsButton, graphicsSettingsButton, controlsSettingsButton;
        [SerializeField] private MemoriCanvasGroup infoCanvasGroup, gameSettingsCanvasGroup, audioSettingsCanvasGroup, graphicsSettingsCanvasGroup, controlsSettingsCanvasGroup, creditsCanvasGroup;
        [SerializeField] private SettingsToggleV2 hideUnitInfoInBattleToggle;
        [SerializeField] private SettingsToggleV2 cameraShakeToggle;
        [SerializeField] private SettingsToggleV2 autoRollInitiativeToggle;
        [SerializeField] private SettingsToggleV2 invertMouseToggle;
        [SerializeField] private SettingsToggleV2 colorblindModeToggle;
        [SerializeField] private SettingsToggleV2 tapInsteadOfHoldToggle;
        [SerializeField] private MemoriButtonV2 resetTutorialButton;
        public Action<bool> OnSettingsPanelToggled;

        public MonitoredData<bool> HideSquadInfoInBattle = new();
        public MonitoredData<bool> CameraShakeEnabled = new();
        public MonitoredData<bool> AutoRollInitiative = new();
        public MonitoredData<bool> InvertMouseY = new();

        public MonitoredData<float> CameraRotationSpeed;
        public MonitoredData<float> CameraMovementSpeed;
        public MonitoredData<float> CameraZoomSpeed;

        [SerializeField] private MonitoredDataSlider cameraRotationSpeedSlider;
        [SerializeField] private MonitoredDataSlider cameraMovementSpeedSlider;
        [SerializeField] private MonitoredDataSlider cameraZoomSpeedSlider;

        public MonitoredData<float> UIScale = new();

        [Header("Game Scale")]
        [SerializeField] private TMP_Dropdown uiScaleDropdown;
        [SerializeField] private TMP_Dropdown battlefieldFlagsDropdown;

        MemoriCanvasGroup activeCanvasGroup;
        float cachedTimeValue;

        // Live read, not cached: CurrentGameState flips the instant a transition starts, so this never goes stale.
        bool InBattle => SceneHandler.Instance.CurrentGameState == GameStateEnum.Battle;

        public bool SettingsPanelOpen => settingsCanvasGroup.alpha == 1;
        private void Start()
        {
            settingsCanvasGroup.CGDisable();
            resumeGameButton.onClick.AddListener(CloseSettingsPanel);
            collectionButton.onClick.AddListener(OpenCollectionPanel);
            exitToMenuButton.onClick.AddListener(ExitToMenu);
            exitToDesktopButton.onClick.AddListener(ExitToDesktop);
            deleteProgressButton.onClick.AddListener(OpenDeleteProgressPrompt);

            abandonRunButton.onClick.AddListener(AbandonRunConfirmationPopUp);
            abandonRunConfirmationButton.SetUp(this);
            abandonRunCancelButton.onClick.AddListener(CancelAbandonRun);
            abandonRunConfirmationCanvasGroup.CGDisable();

            quickRestartButton.onClick.AddListener(QuickRestartConfirmationPopUp);
            quickRestartConfirmationCanvasGroup.CGDisable();
            quickRestartConfirmationButton.onClick.AddListener(QuickRestart);
            quickRestartCancelButton.onClick.AddListener(CancelQuickRestart);

            concedeDefeatButton.onClick.AddListener(ConcedeDefeatConfirmationPopUp);
            concedeDefeatConfirmationCanvasGroup.CGDisable();
            concedeDefeatConfirmationButton.onClick.AddListener(ConcedeDefeat);
            concedeDefeatCancelButton.onClick.AddListener(CancelConcedeDefeat);

            // Pages stay switched off until opened: all of them live at boot broke Addressables loading in player builds.
            activeCanvasGroup = gameSettingsCanvasGroup;
            foreach (RailEntry entry in railEntries)
                if (entry.page.gameObject.activeSelf)
                    Debug.LogError($"SettingsManager: the Settings page '{entry.page.name}' is switched on in the scene. Switch it off; player builds fail to load localization when every page starts at boot.");

            infoButton.onClick.RemoveAllListeners();
            gameSettingsButton.onClick.RemoveAllListeners();
            audioSettingsButton.onClick.RemoveAllListeners();
            graphicsSettingsButton.onClick.RemoveAllListeners();
            controlsSettingsButton.onClick.RemoveAllListeners();
            creditsButton.onClick.RemoveAllListeners();
            resetTutorialButton.Button.onClick.RemoveAllListeners();
            
            infoButton.onClick.AddListener(() => SwitchSettingsFocus(infoCanvasGroup));
            gameSettingsButton.onClick.AddListener(() => SwitchSettingsFocus(gameSettingsCanvasGroup));
            audioSettingsButton.onClick.AddListener(() => SwitchSettingsFocus(audioSettingsCanvasGroup));
            graphicsSettingsButton.onClick.AddListener(() => SwitchSettingsFocus(graphicsSettingsCanvasGroup));
            controlsSettingsButton.onClick.AddListener(() => SwitchSettingsFocus(controlsSettingsCanvasGroup));
            creditsButton.onClick.AddListener(() => SwitchSettingsFocus(creditsCanvasGroup));
            resetTutorialButton.Button.onClick.AddListener(() => ResetTutorial());

            devToolsButton.onClick.RemoveAllListeners();
            devToolsButton.onClick.AddListener(() => SwitchSettingsFocus(devToolsCanvasGroup));
            devToolsButton.gameObject.SetActive(SaveDataHandler.IsDevToolUser());
            // Here, not in CollectionRailRow: CollectionPanel already clicks for the Collection's own rail.
            foreach (TJ.MainMenu.CollectionRailRow row in settingsCanvasGroup.GetComponentsInChildren<TJ.MainMenu.CollectionRailRow>(true))
                row.Button.onClick.AddListener(() => IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick));
            RefreshRail();

            SceneHandler.Instance.OnGameStateChanged += OnGameStateChanged;
            InputHandler.Instance.SettingsButtonPressed += SettingsHotkeyPressed;
            hideUnitInfoInBattleToggle.Load();
            cameraShakeToggle.Load();
            autoRollInitiativeToggle.Load();
            invertMouseToggle.Load();
            HideSquadInfoInBattle.Value = hideUnitInfoInBattleToggle.OnToggle.isOn;
            hideUnitInfoInBattleToggle.OnToggle.onValueChanged.AddListener(SetHideSquadInfoInBattle);
            CameraShakeEnabled.Value = cameraShakeToggle.OnToggle.isOn;
            cameraShakeToggle.OnToggle.onValueChanged.AddListener(val => CameraShakeEnabled.Value = val);

            AutoRollInitiative.Value = autoRollInitiativeToggle.OnToggle.isOn;
            autoRollInitiativeToggle.OnToggle.onValueChanged.AddListener(val => AutoRollInitiative.Value = val);

            InvertMouseY.Value = invertMouseToggle.OnToggle.isOn;
            invertMouseToggle.OnToggle.onValueChanged.AddListener(val => InvertMouseY.Value = val);

            colorblindModeToggle.Load();
            SetColorblindMode(colorblindModeToggle.OnToggle.isOn);
            colorblindModeToggle.OnToggle.onValueChanged.AddListener(SetColorblindMode);

            tapInsteadOfHoldToggle.Load();
            InputHandler.Instance.SetTapInsteadOfHold(tapInsteadOfHoldToggle.OnToggle.isOn);
            tapInsteadOfHoldToggle.OnToggle.onValueChanged.AddListener(InputHandler.Instance.SetTapInsteadOfHold);

            CameraRotationSpeed.Value = PlayerPrefs.GetFloat("cameraRotationSpeed", 0.5f);
            CameraMovementSpeed.Value = PlayerPrefs.GetFloat("cameraMovementSpeed", 0.5f);
            CameraZoomSpeed.Value = PlayerPrefs.GetFloat("cameraZoomSpeed", 0.5f);
            cameraRotationSpeedSlider.AssignMonitoredData(CameraRotationSpeed);
            cameraMovementSpeedSlider.AssignMonitoredData(CameraMovementSpeed);
            cameraZoomSpeedSlider.AssignMonitoredData(CameraZoomSpeed);

            float uiScale = PlayerPrefs.GetFloat(UIScaler.PrefKey, UIScaler.Default);
            PlayerPrefs.SetFloat(UIScaler.PrefKey, uiScale);
            UIScale.Value = uiScale;
            UIScale.OnValueChanged += UIScaler.Apply;
            SceneManager.sceneLoaded += OnSceneLoadedApplyUIScale;
            UIScaler.Apply(uiScale);
            FillScaleDropdown(uiScaleDropdown, UIScaler.Steps, UIScaler.StepIndex(uiScale), SetUIScale);

            float flags = PlayerPrefs.GetFloat(BattlefieldMarkerScale.PrefKey, BattlefieldMarkerScale.Default);
            PlayerPrefs.SetFloat(BattlefieldMarkerScale.PrefKey, flags);
            BattlefieldMarkerScale.Apply(flags);
            FillScaleDropdown(battlefieldFlagsDropdown, BattlefieldMarkerScale.Steps, BattlefieldMarkerScale.StepIndex(flags), SetBattlefieldFlagsScale);
        }
        // Both scale settings apply on change: they are not video modes, so the player sees the result at once.
        private static void FillScaleDropdown(TMP_Dropdown dropdown, float[] steps, int currentIndex, System.Action<float> onChanged)
        {
            var options = new List<string>();
            foreach (float step in steps) options.Add(Mathf.RoundToInt(step * 100f) + "%");
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(currentIndex);
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(index => onChanged(steps[index]));
        }
        // Applies on change like the scale settings; battle visuals listen to ColorVision.Changed.
        private static void SetColorblindMode(bool isOn)
        {
            ColorVision.Apply(isOn ? ColorVisionMode.Colorblind : ColorVisionMode.Off);
        }
        public void SetBattlefieldFlagsScale(float scale)
        {
            PlayerPrefs.SetFloat(BattlefieldMarkerScale.PrefKey, scale);
            BattlefieldMarkerScale.Apply(scale);
        }
        // Map, MainMenu, Collection and TavernBattle bring their own root canvases after Core.
        private void OnSceneLoadedApplyUIScale(Scene scene, LoadSceneMode mode)
        {
            UIScaler.Apply(UIScale.Value);
        }
        public void SetUIScale(float scale)
        {
            UIScale.Value = scale;
            PlayerPrefs.SetFloat(UIScaler.PrefKey, scale);
        }
        /// <summary>
        /// Opens the Collection as an additive overlay. The Settings panel deliberately stays open
        /// underneath: closing it would restore cachedTimeValue and unpause the battle, and fire
        /// OnSettingsPanelToggled(false) into GameSpeedManager, UIManager and MainMenu. Leaving it
        /// open also keeps SettingsPanelOpen true, which is what input-gates the battle and map.
        /// </summary>
        private void OpenCollectionPanel()
        {
            _ = SceneHandler.Instance.OpenOverlayScene(SceneHandler.CollectionScenePath);
        }
        private void SettingsHotkeyPressed()
        {
            // The Collection overlay owns Esc while it is up. Without this, Esc over a battle would
            // run CloseSettingsPanel and unpause the fight behind a still-open Collection, and in
            // the main menu it would fire OnSettingsPanelToggled into MainMenu.ReturnToMainMenu.
            if (SceneHandler.Instance.OverlaySceneOpen) return;

            Debug.Log($"SettingsManager.SettingsHotkeyPressed() - SettingsPanelOpen: {SettingsPanelOpen}");
            if(SceneHandler.Instance.CurrentGameState == GameStateEnum.MainMenu)
            {
                // Esc never opens Settings in the main menu: it closes it, or steps the menu back to its main panel.
                if (SettingsPanelOpen) CloseSettingsPanel();
                else OnSettingsPanelToggled?.Invoke(true);
                return;
            }

            if(settingsCanvasGroup.alpha == 1) {
                CloseSettingsPanel();
            } else {
                OpenSettingsPanel();
            }
        }
        public void OpenSettingsPanel()
        {
            SwitchSettingsFocus(gameSettingsCanvasGroup);
            bool inMainMenu = SceneHandler.Instance.CurrentGameState == GameStateEnum.MainMenu;
            closeLabel.text = LocalizationManager.Instance.GetText(inMainMenu ? "Close" : "resumeGameButton");
            settingsCanvasGroup.CGEnable();
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
            IAudioRequester.Instance.SetAmbienceDuck(AmbienceDuckSource.Settings, true);
            if(InBattle) {
                cachedTimeValue = Time.timeScale;
                Time.timeScale = 0;
            }
            OnSettingsPanelToggled?.Invoke(true);
        }
        public void CloseSettingsPanel()
        {
            abandonRunConfirmationCanvasGroup.CGDisable();
            settingsCanvasGroup.CGDisable();
            activeCanvasGroup.gameObject.SetActive(false);
            IAudioRequester.Instance.PlaySFX(SFXData.CloseUI);
            IAudioRequester.Instance.SetAmbienceDuck(AmbienceDuckSource.Settings, false);
            if (InBattle)
            {
                Time.timeScale = cachedTimeValue;
            }
            OnSettingsPanelToggled?.Invoke(false);
        }
        public void AbandonRunConfirmationPopUp()
        {
            abandonRunConfirmationCanvasGroup.CGEnable();
        }
        public void AbandonRun()
        {
            abandonRunConfirmationCanvasGroup.CGDisable();
            ExitToMenu();
        }
        public void CancelAbandonRun()
        {
            abandonRunConfirmationCanvasGroup.CGDisable();
        }
        public void QuickRestartConfirmationPopUp()
        {
            quickRestartConfirmationCanvasGroup.CGEnable();
        }
        public void QuickRestart()
        {
            CampaignSaveManager campaignSaveManager = FindFirstObjectByType<CampaignSaveManager>();

            // Quick-restart throws away the current run, so log it as an abandon before the
            // restart deletes the save (QuickRestartCampaign deletes it first thing).
            if (SaveDataHandler.CampaignSaveExists())
            {
                var restartedRun = SaveDataHandler.Load();
                GameEventTracker.RunClosed(restartedRun, RunResult.Abandon, "quickRestart");
                SaveDataHandler.RecordAbandonedRun(restartedRun);
            }

            campaignSaveManager.QuickRestartCampaign();
            quickRestartConfirmationCanvasGroup.CGDisable();
            CloseSettingsPanel();
            SceneHandler.Instance.RequestQuickRestart();
            SceneHandler.Instance.RequestSceneCleanUpFunction(GameStateEnum.MainMenu);
        }
        public void CancelQuickRestart()
        {
            quickRestartConfirmationCanvasGroup.CGDisable();
        }
        public void ExitToMenu()
        {
            CloseSettingsPanel();
            SceneHandler.Instance.RequestSceneCleanUpFunction(GameStateEnum.MainMenu);
        }
        public void ConcedeDefeatConfirmationPopUp()
        {
            concedeDefeatConfirmationCanvasGroup.CGEnable();
        }
        public void ConcedeDefeat()
        {
            concedeDefeatConfirmationCanvasGroup.CGDisable();
            CloseSettingsPanel();
            BattleManager.Instance.ConcedeDefeat();
        }
        public void CancelConcedeDefeat()
        {
            concedeDefeatConfirmationCanvasGroup.CGDisable();
        }
        public void ExitToDesktop()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        /// <summary>Opens Settings on the Battle Guide at one topic, for tutorial prompts that link to it.</summary>
        public void OpenGuide(string topicId)
        {
            if (!SettingsPanelOpen) OpenSettingsPanel();
            SwitchSettingsFocus(infoCanvasGroup);
            BattleGuideView guide = infoCanvasGroup.GetComponentInChildren<BattleGuideView>(true);
            if (guide == null)
            {
                Debug.LogError("SettingsManager.OpenGuide: no BattleGuideView under the Info panel.");
                return;
            }
            guide.OpenBrowser(topicId);
        }
        public void SwitchSettingsFocus(MemoriCanvasGroup _canvasGroup)
        {
            // A hidden page is switched off, not just transparent, so its rows only start once it is shown.
            if (activeCanvasGroup != _canvasGroup)
            {
                activeCanvasGroup.CGDisable();
                activeCanvasGroup.gameObject.SetActive(false);
            }
            _canvasGroup.gameObject.SetActive(true);
            _canvasGroup.CGEnable();

            activeCanvasGroup = _canvasGroup;
            RefreshRail();
        }
        private void RefreshRail()
        {
            foreach (RailEntry entry in railEntries)
                entry.row.SetActive(entry.page == activeCanvasGroup);
        }
        private void OnGameStateChanged(GameStateEnum gameStateEnum)
        {
            // Debug.Log($"SettingsManager.OnGameStateChanged({gameStateEnum})");
            if(gameStateEnum.Equals(GameStateEnum.MainMenu)) {
                abandonRunButton.gameObject.SetActive(false);
                quickRestartButton.gameObject.SetActive(false);
                exitToMenuButton.gameObject.SetActive(false);
                concedeDefeatButton.gameObject.SetActive(false);
                deleteProgressGroup.SetActive(true);
            } else if(gameStateEnum.Equals(GameStateEnum.Map)) {
                abandonRunButton.gameObject.SetActive(true);
                exitToMenuButton.gameObject.SetActive(true);
                quickRestartButton.gameObject.SetActive(true);
                concedeDefeatButton.gameObject.SetActive(false);
                deleteProgressGroup.SetActive(false);
            } else if(gameStateEnum.Equals(GameStateEnum.Battle)) {
                bool IsCustomBattle = SaveDataHandler.LoadPlayerSaveData().customBattle;
                abandonRunButton.gameObject.SetActive(!IsCustomBattle);
                quickRestartButton.gameObject.SetActive(false);
                exitToMenuButton.gameObject.SetActive(false);
                concedeDefeatButton.gameObject.SetActive(true);
                deleteProgressGroup.SetActive(false);
            }
        }
        // The confirmation pop-up lives in the MainMenu scene, so this button only shows there.
        private void OpenDeleteProgressPrompt()
        {
            CloseSettingsPanel();
            TJ.MainMenu.MainMenu mainMenu = FindFirstObjectByType<TJ.MainMenu.MainMenu>();
            if (mainMenu == null)
            {
                Debug.LogError("Delete All Progress pressed with no MainMenu loaded.");
                return;
            }
            mainMenu.OpenDemoSaveImportPrompt();
        }
        private void ResetTutorial()
        {
            PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
            saveData.tutorialStepCompleted.Clear();
            saveData.BattlefieldInfoSectionsViewed.Clear();
            SaveDataHandler.SavePlayerSaveData(saveData);

            string notificationText = LocalizationManager.Instance.GetText("tutorialprogressreset");
            NotificationManager.Instance.DisplayNotification(notificationText);

            PlayerPrefs.SetInt("battleTutorial", 0);
            PlayerPrefs.DeleteKey(BattleGuideProgress.LegacyGarrisonPref);
        }
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                Cursor.lockState = Screen.fullScreen ? CursorLockMode.Confined : CursorLockMode.None;
        }

        public void OnDestroy()
        {
            if (SceneHandler.HasInstance)
            {
                SceneHandler.Instance.OnGameStateChanged -= OnGameStateChanged;
            }
            if (InputHandler.HasInstance)
            {
                InputHandler.Instance.SettingsButtonPressed -= SettingsHotkeyPressed;
            }
            UIScale.OnValueChanged -= UIScaler.Apply;
            SceneManager.sceneLoaded -= OnSceneLoadedApplyUIScale;
        }
        private void SetHideSquadInfoInBattle(bool isOn)
        {
            HideSquadInfoInBattle.Value = isOn;
            // Debug.Log($"Setting HideSquadInfoInBattle set to {HideSquadInfoInBattle.Value}");
        }
    }
}
