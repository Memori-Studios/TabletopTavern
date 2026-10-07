using System.Collections;
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
        // Game time for the physics step and the flags' FixedUpdate to catch up with units placed as the battle starts.
        private const float BattleStartSettleSeconds = 0.05f;
        [SerializeField] private GameSpeedButton pauseButton, slowButton, normalButton, fastButton;
        GameSpeedButton[] gameSpeedButtons;
        private bool _isPaused;
        private bool _isSettingsOpen;
        private int _currentSpeedIndex;
        private GameSpeedButton _prePauseButton;
        private Coroutine _battleStartPause;
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
            StopBattleStartPause();
            SetTimeScale(_gameSpeedButton);
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.ChangeBattleSpeed);
        }

        public void SetTimeScale(GameSpeedButton _gameSpeedButton)
        {
            // Photo mode owns time while it is open; the speed buttons and pause state stay as the player left them.
            if (_photoHeld) return;
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
            _battleStartPause = StartCoroutine(PauseAtBattleStart());
        }
        // A deferred enemy army and outriders are placed inside StartBattle and are drawn, clickable and flagged only after a few ticks.
        private IEnumerator PauseAtBattleStart()
        {
            yield return new WaitForSeconds(BattleStartSettleSeconds);
            if (AutoPause())
            {
                // The outrider warning goes up as the battle starts; this message follows it instead of replacing it.
                float wait = NotificationManager.Instance.SecondsUntilFree;
                if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
                if (BattleManager.Instance.GamePhase == GamePhase.Battle)
                    NotificationManager.Instance.DisplayNotification(LocalizationManager.Instance.GetText("autoPauseBattleStart"));
            }
            _battleStartPause = null;
        }
        // A speed the player picks replaces a battle-start pause that is still waiting to happen or to show its message.
        private void StopBattleStartPause()
        {
            if (_battleStartPause == null) return;
            StopCoroutine(_battleStartPause);
            _battleStartPause = null;
        }
        private void OnSquadBroken(int squadId)
        {
            if (squadId <= 0 || _isPaused || PlayerPrefs.GetInt(PauseOnSquadBreakPref, 0) != 1) return;
            // Losing the last squad ends the battle, so a pause there would only delay the result.
            if (!PlayerHasOtherUnbrokenSquad(squadId)) return;
            string squadName = LocalizationManager.Instance.GetText(BattleManager.Instance.SquadManager.GetSquad(squadId).UnitName.ToString());
            if (AutoPause())
                NotificationManager.Instance.DisplayNotification(string.Format(LocalizationManager.Instance.GetText("autoPauseSquadBroke"), squadName));
        }
        // Not PlayerSetTimeScale: an automatic pause is not the player's, so it skips PauseUsedThisBattle and the speed tip.
        private bool AutoPause()
        {
            if (_isPaused || BattleManager.Instance.GamePhase != GamePhase.Battle) return false;
            // A pause that falls inside a photo mode time step waits until photo mode closes.
            if (_photoHeld)
            {
                _pendingAutoPause = true;
                return false;
            }
            SetTimeScale(pauseButton);
            return true;
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

        #region Photo mode hold
        private bool _photoHeld;
        private bool _pendingAutoPause;
        private Coroutine _photoStep;
        public bool PhotoStepRunning => _photoStep != null;

        /// <summary>Freezes the battle for photo mode without touching the speed buttons or the player's pause state.</summary>
        public void BeginPhotoHold()
        {
            if (_photoHeld) return;
            // After the battle the units are already frozen by LockEndOfBattleSpeed, and that state must not change.
            GamePhase phase = BattleManager.Instance.GamePhase;
            if (phase == GamePhase.Battle && !_isPaused)
                SaveDataHandler.PauseUsedThisBattle = true;
            _photoHeld = true;
            if (phase != GamePhase.PostGame) ApplyRaw(0f);
        }

        /// <summary>Hands time back. With restoreSpeed off (scene teardown) nothing is re-applied.</summary>
        public void EndPhotoHold(bool restoreSpeed)
        {
            if (!_photoHeld) return;
            if (_photoStep != null)
            {
                StopCoroutine(_photoStep);
                _photoStep = null;
            }
            _photoHeld = false;
            bool pendingPause = _pendingAutoPause;
            _pendingAutoPause = false;
            if (!restoreSpeed || BattleManager.Instance.GamePhase == GamePhase.PostGame) return;
            ApplyRaw(gameSpeedButtons[_currentSpeedIndex].GameSpeed);
            if (pendingPause) AutoPause();
        }

        /// <summary>Runs the battle for a short stretch of game time, then freezes it again. Battle phase only.</summary>
        public bool PhotoStep(float seconds)
        {
            if (!_photoHeld || _photoStep != null || BattleManager.Instance.GamePhase != GamePhase.Battle) return false;
            // A step only makes sense from a frozen battle.
            if (Time.timeScale > 0f) return false;
            _photoStep = StartCoroutine(PhotoStepRoutine(seconds));
            return true;
        }
        /// <summary>Lets the battle run at a set speed inside photo mode; 0 freezes it again. Battle phase only.</summary>
        public void SetPhotoSpeed(float speed)
        {
            if (!_photoHeld || BattleManager.Instance.GamePhase != GamePhase.Battle) return;
            if (_photoStep != null)
            {
                StopCoroutine(_photoStep);
                _photoStep = null;
            }
            ApplyRaw(speed);
        }
        private IEnumerator PhotoStepRoutine(float seconds)
        {
            ApplyRaw(1f);
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            ApplyRaw(0f);
            _photoStep = null;
        }
        private static void ApplyRaw(float speed)
        {
            Time.timeScale = speed;
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            world.GetExistingSystemManaged<SimulationSystemGroup>().Enabled = speed > 0f;
            world.GetExistingSystemManaged<InitializationSystemGroup>().Enabled = speed > 0f;
        }
        #endregion
        public void LockEndOfBattleSpeed()
        {
            EndPhotoHold(false);
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