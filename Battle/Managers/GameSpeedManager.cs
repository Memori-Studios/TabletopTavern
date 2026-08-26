using Memori.Input;
using UnityEngine;
using Memori.Utilities;
using Unity.Entities;
using Memori.SaveData;

namespace TJ
{
    public class GameSpeedManager : MonoBehaviour
    {
        [SerializeField] private GameSpeedButton pauseButton, slowButton, normalButton, fastButton;
        GameSpeedButton[] gameSpeedButtons;
        private bool _isPaused;
        private bool _isSettingsOpen;
        private int _currentSpeedIndex;
        private GameSpeedButton _prePauseButton;
        private ReportABugScreen _reportABugScreen;
        private void Start()
        {
            gameSpeedButtons = new GameSpeedButton[] { pauseButton, slowButton, normalButton, fastButton };
            pauseButton.SetUpGameSpeedButton(this, 0);
            slowButton.SetUpGameSpeedButton(this, 0.5f);
            normalButton.SetUpGameSpeedButton(this, 1);
            fastButton.SetUpGameSpeedButton(this, 3);
            SetTimeScale(normalButton);
            InputHandler.Instance.PauseButtonPressed += PauseGame;
            InputHandler.Instance.OnSpeedUp += IncreaseSpeed;
            InputHandler.Instance.OnSpeedDown += DecreaseSpeed;
            _reportABugScreen = FindFirstObjectByType<ReportABugScreen>();
            SettingsManager.Instance.OnSettingsPanelToggled += OnSettingsPanelToggled;
            SaveDataHandler.PauseUsedThisBattle = false; // fresh per battle; consumed at battle end
        }

        private void OnSettingsPanelToggled(bool isOpen) => _isSettingsOpen = isOpen;

        public void PauseGame()
        {
            if (BattleManager.Instance.GamePhase != GamePhase.Battle) return;
            if (_isSettingsOpen) return;
            if (_reportABugScreen.GetComponent<CanvasGroup>().interactable) return;

            if (_isPaused) {
                Debug.Log("Battle unpaused.");
                SetTimeScale(_prePauseButton);
            } else {
                Debug.Log("Battle paused.");
                SetTimeScale(pauseButton);
                SaveDataHandler.PauseUsedThisBattle = true; // recorded into RunStats at battle end (battle scene has no CampaignSaveManager)
            }
        }
        public void IncreaseSpeed()
        {
            if (BattleManager.Instance.GamePhase != GamePhase.Battle) return;
            if (_isSettingsOpen) return;
            if (_currentSpeedIndex < gameSpeedButtons.Length - 1)
                SetTimeScale(gameSpeedButtons[_currentSpeedIndex + 1]);
        }

        public void DecreaseSpeed()
        {
            if (BattleManager.Instance.GamePhase != GamePhase.Battle) return;
            if (_isSettingsOpen) return;
            if (_currentSpeedIndex > 0)
                SetTimeScale(gameSpeedButtons[_currentSpeedIndex - 1]);
        }

        public void SetTimeScale(GameSpeedButton _gameSpeedButton)
        {
            if (_gameSpeedButton == pauseButton && !_isPaused)
                _prePauseButton = gameSpeedButtons[_currentSpeedIndex];
            for (int i = 0; i < gameSpeedButtons.Length; i++) {
                if (gameSpeedButtons[i] == _gameSpeedButton) {
                    gameSpeedButtons[i].Select();
                    _currentSpeedIndex = i;
                } else {
                    gameSpeedButtons[i].Deselect();
                }
            }
            _isPaused = _gameSpeedButton.GameSpeed == 0;
            Time.timeScale = _gameSpeedButton.GameSpeed;
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            var simulationSystemGroup = defaultWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            var initializationSystemGroup = defaultWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            simulationSystemGroup.Enabled = !_isPaused;
            initializationSystemGroup.Enabled = !_isPaused;
        }
        public void OnDestroy()
        {
            // Only writer of a non-1 timeScale, so release it on teardown.
            Time.timeScale = 1f;
            InputHandler.Instance.PauseButtonPressed -= PauseGame;
            InputHandler.Instance.OnSpeedUp -= IncreaseSpeed;
            InputHandler.Instance.OnSpeedDown -= DecreaseSpeed;
            if (SettingsManager.HasInstance)
                SettingsManager.Instance.OnSettingsPanelToggled -= OnSettingsPanelToggled;
        }
        public void LockEndOfBattleSpeed()
        {
            Time.timeScale = 1f;
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            var simulationSystemGroup = defaultWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            var initializationSystemGroup = defaultWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            simulationSystemGroup.Enabled = false;
            initializationSystemGroup.Enabled = false;
            pauseButton.Lock();
            slowButton.Lock();
            fastButton.Lock();
            normalButton.Lock();
        }
    }
}