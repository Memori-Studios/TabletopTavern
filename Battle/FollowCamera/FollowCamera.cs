using System.Collections.Generic;
using Memori.Input;
using Memori.Localization;
using Memori.Scenes;
using TJ.Map;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TJ
{
    /// <summary>Read by everything that gives orders or reads battle clicks; true while photo mode or the follow camera is open.</summary>
    public static class BattleViewModes
    {
        public static bool OrdersBlocked => PhotoMode.IsActive || FollowCamera.IsActive;
    }

    /// <summary>
    /// The follow camera for normal battle play: the camera sits behind one of the player's squads and moves with it while the
    /// battle runs. Orders are off and the HUD hides. It owns the order things are switched off and back on.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] private FollowCameraPanelView view;
        [Header("Behind Shot")]
        [SerializeField] private float behindDistance = 12f;
        [SerializeField] private float behindHeight = 5f;
        [SerializeField] private float behindAimHeight = 1f;
        // The follow camera's bob is softer than photo mode's.
        [SerializeField] private float headBobScale = 0.75f;

        private const float RescanSeconds = 0.25f;

        // The Battle actions that stay on: the camera, time, this mode's own key, photo mode and Esc.
        private static readonly string[] KeptActions =
        {
            "Forward", "Back", "Left", "Right", "MoveFast", "EnableCameraRotation", "CameraZoom", "Camera",
            "RaiseCamera", "LowerCamera", "RotateLeft", "RotateRight", "RotateCamera", "PitchCameraUp",
            "PitchCameraDown", "Mouse", "PauseGame", "SpeedUp", "SpeedDown", "FollowSquad", "PhotoMode", "Settings",
        };

        private static FollowCamera s_instance;
        /// <summary>True while the follow camera is open.</summary>
        public static bool IsActive => s_instance != null && s_instance._on;

        private readonly BattleViewHider _hider = new();
        // Keys the player has put a kept action on; this mode's own use of them steps aside.
        private readonly HashSet<Key> _takenKeys = new();
        private SquadFollower _follower;
        private BattleCamera _camera;
        private bool _on;
        private bool _closing;
        // True while the player holds the camera and it stops moving with the squad.
        private bool _manual;
        private float _nextRescan;

        #region Lifetime
        private void Awake() => s_instance = this;

        private void Start()
        {
            _camera = BattleManager.Instance.BattleCameraScript;
            _follower = new SquadFollower(_camera)
            {
                BehindDistance = behindDistance,
                BehindHeight = behindHeight,
                BehindAimHeight = behindAimHeight,
                BobScale = headBobScale,
            };
            InputHandler.Instance.OnFollowSquad -= Toggle;
            InputHandler.Instance.OnFollowSquad += Toggle;
            BattleManager.Instance.OnGamePhaseChanged += OnGamePhaseChanged;
            SceneHandler.Instance.OnGameStateChanged += OnGameStateChanged;
        }

        private void OnDestroy()
        {
            Close(true);
            if (InputHandler.HasInstance) InputHandler.Instance.OnFollowSquad -= Toggle;
            if (BattleManager.HasInstance) BattleManager.Instance.OnGamePhaseChanged -= OnGamePhaseChanged;
            if (SceneHandler.HasInstance) SceneHandler.Instance.OnGameStateChanged -= OnGameStateChanged;
            if (s_instance == this) s_instance = null;
        }

        // The result screen must show, so the follow ends before it.
        private void OnGamePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.PostGame) Close(false);
            // A rematch reuses this scene after a teardown.
            else _closing = false;
        }
        private void OnGameStateChanged(GameStateEnum state) => Close(true);

        /// <summary>Called first thing when the battle scene starts to unload, before the ECS world is disposed.</summary>
        public static void ForceExitForSceneClose()
        {
            if (s_instance == null) return;
            s_instance._closing = true;
            s_instance.Close(true);
        }
        #endregion

        #region Enter and exit
        public static void RequestExit()
        {
            if (s_instance != null) s_instance.Close(false);
        }

        /// <summary>Closes the follow camera with the camera left in place and returns the squad it was on, or Entity.Null.</summary>
        public static Entity HandOffToPhotoMode()
        {
            if (!IsActive) return Entity.Null;
            Entity squad = s_instance._follower.Squad;
            s_instance.Close(false);
            return squad;
        }

        private void Toggle()
        {
            if (_on) Close(false);
            else Enter();
        }

        private bool CanEnter()
        {
            if (_on || _closing || PhotoMode.IsActive) return false;
            if (SceneHandler.Instance.CurrentGameState != GameStateEnum.Battle) return false;
            if (BattleManager.Instance.GamePhase != GamePhase.Battle) return false;
            if (SettingsManager.Instance.SettingsPanelOpen) return false;
            if (SceneHandler.Instance.OverlaySceneOpen) return false;
            if (BattleManager.Instance.BattlefieldTutorial.TutorialIsOpen) return false;
            if (TutorialManager.Instance.IsShowingStep) return false;
            if (BattleInputManager.Instance.IsRearrangingSquads) return false;
            // Alt+Tab reaches the game as the key on its own.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.altKey.isPressed) return false;
            return true;
        }

        private void Enter()
        {
            if (!CanEnter()) return;
            Entity squad = PickSquad();
            if (squad == Entity.Null) return;

            // 1. Nothing half-done may survive into a mode without orders: drags, an armed spell, hover, UI focus.
            BattleManager.Instance.SpellManager.CancelWheelAndCast();
            BattleInputManager.Instance.CancelPendingMouseActions(UnitSelectionManager.Instance.SelectedSquadIds.Count > 0);
            BattleManager.Instance.PositionDrawer.TurnOff();
            BattleInputManager.Instance.ResetCursor();
            UnitSelectionManager.Instance.ClearHoverForCleanView();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            // 2. Orders off, then the view, then the camera.
            InputHandler.Instance.SetBattleBlock(KeptActions);
            _on = true;
            _manual = false;
            UIManager ui = BattleManager.Instance.UIManager;
            _hider.Hide(view.Canvas, true, ui.HudRootCanvas);
            ui.SetHudHidden(this, true);
            _camera.EnterFollow();
            _follower.Lock(squad, SquadFollower.Framing.Behind);

            FindTakenKeys();
            string cycle = InputDevices.UsingGamepad ? string.Empty : Name(Key.PageUp) + " / " + Name(Key.PageDown);
            view.Show(KeyName("Settings"), Name(Key.Space), cycle);
            RefreshPanel();
        }

        /// <param name="sceneClosing">True on teardown: the camera is left alone, only what outlives the battle is restored.</param>
        private void Close(bool sceneClosing)
        {
            if (!_on) return;
            _on = false;
            _manual = false;

            // Each step stands alone, so one failure cannot leave the rest switched off.
            if (sceneClosing) _follower.Clear();
            else Guard(() => _follower.Stop());
            if (!sceneClosing) Guard(() => _camera.ExitFollow());
            Guard(() => view.Hide());
            Guard(() => _hider.Restore(sceneClosing));
            if (!sceneClosing) Guard(() => BattleManager.Instance.UIManager.SetHudHidden(this, false));
            Guard(() => { if (InputHandler.HasInstance) InputHandler.Instance.ClearBattleBlock(); });
        }

        private static void Guard(System.Action step)
        {
            try { step(); }
            catch (System.Exception e) { Debug.LogError($"[FollowCamera] A restore step failed: {e}"); }
        }

        // The selected squad with the lowest id; with none selected, the player's squad nearest the middle of the view.
        private static Entity PickSquad()
        {
            List<(int id, Entity entity)> squads = SquadFollower.PlayerSquads();
            if (squads.Count == 0) return Entity.Null;
            List<int> selected = UnitSelectionManager.Instance.SelectedSquadIds;
            foreach ((int id, Entity entity) in squads)
                if (selected.Contains(id)) return entity;

            if (!SquadFollower.GroundPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out Vector3 point)) return squads[0].entity;
            Entity best = squads[0].entity;
            float bestDistance = float.MaxValue;
            EntityManager entities = World.DefaultGameObjectInjectionWorld.EntityManager;
            foreach ((int id, Entity entity) in squads)
            {
                Vector3 centre = entities.GetComponentData<SquadMovementComponent>(entity).SquadCenter;
                float distance = (new Vector2(centre.x, centre.z) - new Vector2(point.x, point.z)).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = entity;
            }
            return best;
        }
        #endregion

        #region While open
        private void Update()
        {
            if (!_on) return;
            // A tutorial step would open over a hidden HUD.
            if (TutorialManager.Instance.IsShowingStep || BattleManager.Instance.BattlefieldTutorial.TutorialIsOpen)
            {
                Close(false);
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !Application.isFocused) return;
            if (Pressed(keyboard, Key.Space)) ToggleManual();
            if (Pressed(keyboard, Key.PageDown)) Cycle(1);
            if (Pressed(keyboard, Key.PageUp)) Cycle(-1);
        }

        private void LateUpdate()
        {
            if (!_on) return;
            bool alive = _manual ? _follower.SquadAlive() : _follower.Tick(SettingsManager.Instance.FollowHeadBob.Value);
            if (!alive)
            {
                Close(false);
                return;
            }
            // The battle keeps running, so HUD pieces and markers that switch on are caught as they appear.
            if (Time.unscaledTime >= _nextRescan)
            {
                _nextRescan = Time.unscaledTime + RescanSeconds;
                _hider.RescanCanvases();
                _hider.Rescan();
            }
            view.SetPaused(Time.timeScale <= 0f);
        }

        // Manual control stops the camera moving with the squad; locking back on puts it behind the squad again.
        private void ToggleManual()
        {
            _manual = !_manual;
            if (_manual) _follower.ResetBob();
            else _follower.Lock(_follower.Squad, SquadFollower.Framing.Behind);
            RefreshPanel();
        }

        private void Cycle(int direction)
        {
            Entity next = _follower.NextPlayerSquad(direction, out _);
            if (next == Entity.Null) return;
            _manual = false;
            _follower.Lock(next, SquadFollower.Framing.Behind);
            RefreshPanel();
        }

        private void RefreshPanel()
        {
            view.SetTitle(_manual
                ? LocalizationManager.Instance.GetText("followManualTitle")
                : string.Format(LocalizationManager.Instance.GetText("followTitle"), _follower.SquadName()));
            view.SetPaused(Time.timeScale <= 0f);
        }

        private bool Pressed(Keyboard keyboard, Key key) => !_takenKeys.Contains(key) && keyboard[key].wasPressedThisFrame;
        #endregion

        #region Keys
        private void FindTakenKeys()
        {
            _takenKeys.Clear();
            foreach (InputAction action in InputHandler.Instance.GameControls.Battle.Get().actions)
            {
                if (System.Array.IndexOf(KeptActions, action.name) < 0) continue;
                foreach (InputControl control in action.controls)
                    if (control is KeyControl key) _takenKeys.Add(key.keyCode);
            }
        }

        // Keys the follow camera reads straight from the keyboard; a pad has no prompt for them.
        private static string Name(Key key)
        {
            if (InputDevices.UsingGamepad) return string.Empty;
            Keyboard keyboard = Keyboard.current;
            return keyboard != null ? keyboard[key].displayName : key.ToString();
        }

        private static string KeyName(string actionName)
        {
            InputAction action = InputHandler.Instance.GameControls.Battle.Get().FindAction(actionName);
            return InputGlyphs.For(action) ?? string.Empty;
        }
        #endregion
    }
}
