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
using TabletopTavern.Analytics;

namespace TJ
{
    public class SettingsManager : Memori.Utilities.Singleton<SettingsManager>
    {
        [SerializeField] private MemoriCanvasGroup settingsCanvasGroup;

        [Header("Main Buttons")]
        [SerializeField] private Button resumeGameButton;
        [SerializeField] private Button exitToMenuButton, exitToDesktopButton, abandonRunButton, quickRestartButton, creditsButton, concedeDefeatButton;

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
        [SerializeField] private SettingsToggleV2 disbandConfirmationToggle;
        [SerializeField] private SettingsToggleV2 hideUnitInfoInBattleToggle;
        [SerializeField] private SettingsToggleV2 cameraShakeToggle;
        [SerializeField] private SettingsToggleV2 autoRollInitiativeToggle;
        [SerializeField] private SettingsToggleV2 invertMouseToggle;
        [SerializeField] private MemoriButtonV2 resetTutorialButton;
        public Action<bool> OnSettingsPanelToggled;

        public MonitoredData<bool> HideSquadInfoInBattle = new();
        public MonitoredData<bool> CameraShakeEnabled = new();
        public MonitoredData<bool> AutoRollInitiative = new();
        public MonitoredData<bool> InvertMouseY = new();

        public MonitoredData<float> CameraRotationSpeed;
        public MonitoredData<float> CameraMovementSpeed;

        [SerializeField] private MonitoredDataSlider cameraRotationSpeedSlider;
        [SerializeField] private MonitoredDataSlider cameraMovementSpeedSlider;

        MemoriCanvasGroup activeCanvasGroup;
        float cachedTimeValue;

        // Live read, not cached: CurrentGameState flips the instant a transition starts, so this never goes stale.
        bool InBattle => SceneHandler.Instance.CurrentGameState == GameStateEnum.Battle;

        public bool SettingsPanelOpen => settingsCanvasGroup.canvasGroup.alpha == 1;
        private void Start()
        {
            settingsCanvasGroup.CGDisable();
            resumeGameButton.onClick.AddListener(CloseSettingsPanel);
            exitToMenuButton.onClick.AddListener(ExitToMenu);
            exitToDesktopButton.onClick.AddListener(ExitToDesktop);

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

            activeCanvasGroup = gameSettingsCanvasGroup;
            gameSettingsCanvasGroup.CGEnable();

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

            SceneHandler.Instance.OnGameStateChanged += OnGameStateChanged;
            InputHandler.Instance.SettingsButtonPressed += SettingsHotkeyPressed;
            infoCanvasGroup.gameObject.SetActive(false);
            hideUnitInfoInBattleToggle.OnToggle.onValueChanged.AddListener(SetHideSquadInfoInBattle);
            CameraShakeEnabled.Value = cameraShakeToggle.OnToggle.isOn;
            cameraShakeToggle.OnToggle.onValueChanged.AddListener(val => CameraShakeEnabled.Value = val);

            AutoRollInitiative.Value = autoRollInitiativeToggle.OnToggle.isOn;
            autoRollInitiativeToggle.OnToggle.onValueChanged.AddListener(val => AutoRollInitiative.Value = val);

            InvertMouseY.Value = invertMouseToggle.OnToggle.isOn;
            invertMouseToggle.OnToggle.onValueChanged.AddListener(val => InvertMouseY.Value = val);

            CameraRotationSpeed.Value = PlayerPrefs.GetFloat("cameraRotationSpeed", 0.5f);
            CameraMovementSpeed.Value = PlayerPrefs.GetFloat("cameraMovementSpeed", 0.5f);
            cameraRotationSpeedSlider.AssignMonitoredData(CameraRotationSpeed);
            cameraMovementSpeedSlider.AssignMonitoredData(CameraMovementSpeed);
        }
        private void SettingsHotkeyPressed()
        {
            Debug.Log($"SettingsManager.SettingsHotkeyPressed() - SettingsPanelOpen: {SettingsPanelOpen}");
            if(SceneHandler.Instance.CurrentGameState == GameStateEnum.MainMenu) 
            {
                OnSettingsPanelToggled?.Invoke(true);
                return;
            }

            if(settingsCanvasGroup.canvasGroup.alpha == 1) {
                CloseSettingsPanel();
            } else {
                OpenSettingsPanel();
            }
        }
        public void OpenSettingsPanel()
        {
            SwitchSettingsFocus(gameSettingsCanvasGroup);
            infoCanvasGroup.gameObject.SetActive(true);
            // disbandConfirmationToggle.OverrideToggleFromSettings();
            settingsCanvasGroup.CGEnable();
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
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
            infoCanvasGroup.gameObject.SetActive(false);
            IAudioRequester.Instance.PlaySFX(SFXData.CloseUI);
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
                GameEventTracker.RunEnded(restartedRun.heroID, (int)restartedRun.difficultyLevel, RunResult.Abandon, restartedRun.RunStats.chaptersCompleted);
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
        public void SwitchSettingsFocus(MemoriCanvasGroup _canvasGroup)
        {
            if(activeCanvasGroup == _canvasGroup) return;

            activeCanvasGroup.CGDisable();
            _canvasGroup.CGEnable();

            activeCanvasGroup = _canvasGroup;
        }
        private void OnGameStateChanged(GameStateEnum gameStateEnum)
        {
            // Debug.Log($"SettingsManager.OnGameStateChanged({gameStateEnum})");
            if(gameStateEnum.Equals(GameStateEnum.MainMenu)) {
                abandonRunButton.gameObject.SetActive(false);
                quickRestartButton.gameObject.SetActive(false);
                exitToMenuButton.gameObject.SetActive(false);
                concedeDefeatButton.gameObject.SetActive(false);
            } else if(gameStateEnum.Equals(GameStateEnum.Map)) {
                abandonRunButton.gameObject.SetActive(true);
                exitToMenuButton.gameObject.SetActive(true);
                quickRestartButton.gameObject.SetActive(true);
                concedeDefeatButton.gameObject.SetActive(false);
            } else if(gameStateEnum.Equals(GameStateEnum.Battle)) {
                bool IsCustomBattle = SaveDataHandler.LoadPlayerSaveData().customBattle;
                abandonRunButton.gameObject.SetActive(!IsCustomBattle);
                quickRestartButton.gameObject.SetActive(false);
                exitToMenuButton.gameObject.SetActive(false);
                concedeDefeatButton.gameObject.SetActive(true);
            }
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
        }
        private void SetHideSquadInfoInBattle(bool isOn)
        {
            HideSquadInfoInBattle.Value = isOn;
            // Debug.Log($"Setting HideSquadInfoInBattle set to {HideSquadInfoInBattle.Value}");
        }
    }
}
