using Memori.Input;
using UnityEngine;
using Memori.Utilities;
using Unity.Entities;
using Memori.SaveData;
using TJ.Map;
using Memori.Localization;
using Memori.Notifications;
using Unity.Collections;

namespace TJ
{
    public class GameSpeedManager : MonoBehaviour
    {
        public const string PauseAtBattleStartPref = "PauseAtBattleStart";
        public const string PauseOnSquadBreakPref = "PauseOnSquadBreak";
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
            BattleManager.Instance.OnGamePhaseChanged += OnGamePhaseChanged;
            BattleManager.Instance.OnSquadBrokenEvent += OnSquadBroken;
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
                PlayerSetTimeScale(_prePauseButton);
            } else {
                Debug.Log("Battle paused.");
                PlayerSetTimeScale(pauseButton);
                SaveDataHandler.PauseUsedThisBattle = true; // recorded into RunStats at battle end (battle scene has no CampaignSaveManager)
            }
        }
        public void IncreaseSpeed()
        {
            if (BattleManager.Instance.GamePhase != GamePhase.Battle) return;
            if (_isSettingsOpen) return;
            if (_currentSpeedIndex < gameSpeedButtons.Length - 1)
                PlayerSetTimeScale(gameSpeedButtons[_currentSpeedIndex + 1]);
        }

        public void DecreaseSpeed()
        {
            if (BattleManager.Instance.GamePhase != GamePhase.Battle) return;
            if (_isSettingsOpen) return;
            if (_currentSpeedIndex > 0)
                PlayerSetTimeScale(gameSpeedButtons[_currentSpeedIndex - 1]);
        }

        // Only a speed change the player made completes the tip; Start's reset to normal must not.
        public void PlayerSetTimeScale(GameSpeedButton _gameSpeedButton)
        {
            SetTimeScale(_gameSpeedButton);
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.ChangeBattleSpeed);
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
            if (InputHandler.HasInstance)
            {
                InputHandler.Instance.PauseButtonPressed -= PauseGame;
                InputHandler.Instance.OnSpeedUp -= IncreaseSpeed;
                InputHandler.Instance.OnSpeedDown -= DecreaseSpeed;
            }
            if (SettingsManager.HasInstance)
                SettingsManager.Instance.OnSettingsPanelToggled -= OnSettingsPanelToggled;
            if (BattleManager.HasInstance)
            {
                BattleManager.Instance.OnGamePhaseChanged -= OnGamePhaseChanged;
                BattleManager.Instance.OnSquadBrokenEvent -= OnSquadBroken;
            }
        }

        #region Auto-pause
        private void OnGamePhaseChanged(GamePhase gamePhase)
        {
            if (gamePhase != GamePhase.Battle || PlayerPrefs.GetInt(PauseAtBattleStartPref, 0) != 1) return;
            AutoPause(LocalizationManager.Instance.GetText("autoPauseBattleStart"));
        }
        private void OnSquadBroken(int squadId)
        {
            if (squadId <= 0 || _isPaused || PlayerPrefs.GetInt(PauseOnSquadBreakPref, 0) != 1) return;
            // Losing the last squad ends the battle, so a pause there would only delay the result.
            if (!PlayerHasOtherUnbrokenSquad(squadId)) return;
            string squadName = LocalizationManager.Instance.GetText(BattleManager.Instance.SquadManager.GetSquad(squadId).UnitName.ToString());
            AutoPause(string.Format(LocalizationManager.Instance.GetText("autoPauseSquadBroke"), squadName));
        }
        // Not PlayerSetTimeScale: an automatic pause is not the player's, so it skips PauseUsedThisBattle and the speed tip.
        private void AutoPause(string reason)
        {
            if (_isPaused || BattleManager.Instance.GamePhase != GamePhase.Battle) return;
            SetTimeScale(pauseButton);
            NotificationManager.Instance.DisplayNotification(reason);
        }
        private static bool PlayerHasOtherUnbrokenSquad(int brokenSquadId)
        {
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PlayerSquad>(), ComponentType.ReadOnly<SquadEntity>() },
                None = new[] { ComponentType.ReadOnly<BrokenSquadTag>() },
            });
            using NativeArray<SquadEntity> squads = query.ToComponentDataArray<SquadEntity>(Allocator.Temp);
            query.Dispose();
            foreach (SquadEntity squad in squads)
                if (squad.SquadId != brokenSquadId) return true;
            return false;
        }
        #endregion
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