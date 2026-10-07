using System.Collections.Generic;
using Memori.Input;
using Memori.Localization;
using Memori.Scenes;
using Memori.Steamworks;
using Memori.Utilities;
using TabletopTavern.Analytics;
using TJ.Map;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TJ
{
    /// <summary>
    /// Photo mode for the battle: freezes time, hides everything that is not the battle, frees the camera and takes
    /// pictures. It owns the order things are switched off and back on. Each piece it calls restores its own state.
    /// </summary>
    public class PhotoMode : MonoBehaviour
    {
        [SerializeField] private PhotoModePanelView view;

        private const float StepSeconds = 0.1f;
        private const float RollDegreesPerSecond = 45f;
        private const float FovDegreesPerSecond = 30f;
        private const float MinFov = 15f, MaxFov = 90f, MaxRoll = 90f;
        private const float MaxFocusDistance = 400f;
        // How far from the picked ground point a squad may be and still be the one to follow.
        private const float FollowPickRadius = 60f;
        private const float RunningRescanSeconds = 0.25f;

        // The Battle actions that stay on: the camera, this mode's own key and Esc.
        private static readonly string[] KeptActions =
        {
            "Forward", "Back", "Left", "Right", "MoveFast", "EnableCameraRotation", "CameraZoom", "Camera",
            "RaiseCamera", "LowerCamera", "RotateLeft", "RotateRight", "RotateCamera", "PitchCameraUp",
            "PitchCameraDown", "Mouse", "PhotoMode", "Settings",
        };

        private static PhotoMode s_instance;
        /// <summary>True while photo mode is open. Read by everything that must not act on the battle meanwhile.</summary>
        public static bool IsActive => s_instance != null && s_instance._on;

        private readonly BattleViewHider _hider = new();
        private readonly PhotoLook _look = new();
        // Keys the player has put a kept camera action on; photo mode's own use of them steps aside.
        private readonly HashSet<Key> _takenKeys = new();
        private PhotoSettings _settings;
        private BattleCamera _camera;
        private GameSpeedManager _speed;
        private bool _on;
        private bool _closing;
        private bool _stepWasRunning;
        private float _enteredAt;
        private int _photosTaken;
        private GamePhase _enteredPhase;
        private int _appliedPlaySpeed;
        private int _appliedWeather;
        // Where the battle's real weather sits in PhotoSettings.WeatherLooks; picking it puts the real weather back.
        private int _realWeather;
        private float _nextRescan;
        private SquadFollower _follower;

        #region Lifetime
        private void Awake()
        {
            s_instance = this;
            BattleMarkers.Hidden = false;
            PhotoCapture.ResetBusy();
        }

        private void Start()
        {
            _camera = BattleManager.Instance.BattleCameraScript;
            _follower = new SquadFollower(_camera);
            _speed = BattleManager.Instance.GameSpeedManager;
            InputHandler.Instance.OnPhotoMode -= Toggle;
            InputHandler.Instance.OnPhotoMode += Toggle;
            BattleManager.Instance.OnGamePhaseChanged += OnGamePhaseChanged;
            SceneHandler.Instance.OnGameStateChanged += OnGameStateChanged;
            view.Changed += ApplySettings;
            view.ResetClicked += ResetAll;
            view.AutofocusClicked += FocusAtScreenCentre;
            view.OpenFolderClicked += OpenFolder;
            view.TakePhotoClicked += TakePhoto;
            view.ExitClicked += Exit;
            view.FollowChanged += OnFollowToggled;
            view.DollyChanged += DollyZoom;
        }

        private void OnDestroy()
        {
            Close(true);
            if (InputHandler.HasInstance) InputHandler.Instance.OnPhotoMode -= Toggle;
            if (BattleManager.HasInstance) BattleManager.Instance.OnGamePhaseChanged -= OnGamePhaseChanged;
            if (SceneHandler.HasInstance) SceneHandler.Instance.OnGameStateChanged -= OnGameStateChanged;
            if (s_instance == this) s_instance = null;
        }

        // The result screen must show, and LockEndOfBattleSpeed takes time over right after this.
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
            if (s_instance != null) s_instance.Exit();
        }


        private void Toggle()
        {
            if (_on) Exit();
            else Enter(FollowCamera.HandOffToPhotoMode());
        }

        private bool CanEnter()
        {
            if (_on || _closing) return false;
            if (SceneHandler.Instance.CurrentGameState != GameStateEnum.Battle) return false;
            GamePhase phase = BattleManager.Instance.GamePhase;
            if (phase != GamePhase.Deployment && phase != GamePhase.Battle && phase != GamePhase.PostGame) return false;
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

        /// <param name="followSquad">A squad the follow camera was on when it handed over; photo mode keeps following it.</param>
        private void Enter(Entity followSquad)
        {
            if (!CanEnter()) return;

            _enteredPhase = BattleManager.Instance.GamePhase;
            _enteredAt = Time.realtimeSinceStartup;
            _photosTaken = 0;
            _settings = new PhotoSettings();
            _appliedPlaySpeed = 0;
            _realWeather = Mathf.Max(0, System.Array.IndexOf(PhotoSettings.WeatherLooks, BattleManager.Instance.BattlefieldEnvManager.CurrentWeather));
            _appliedWeather = _realWeather;
            _settings.WeatherLook = _realWeather;
            _follower.Clear();

            // 1. Nothing half-done may survive into the frozen picture: drags, an armed spell, hover, UI focus.
            BattleManager.Instance.SpellManager.CancelWheelAndCast();
            BattleInputManager.Instance.CancelPendingMouseActions(UnitSelectionManager.Instance.SelectedSquadIds.Count > 0);
            BattleManager.Instance.PositionDrawer.TurnOff();
            BattleInputManager.Instance.ResetCursor();
            UnitSelectionManager.Instance.ClearHoverForCleanView();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            // 2. Orders off, then time, then the view.
            InputHandler.Instance.SetBattleBlock(KeptActions);
            _speed.BeginPhotoHold();
            _on = true;
            _hider.Hide(view.Canvas, _settings.ShowFlags);
            _camera.EnterPhoto();
            _settings.Fov = _camera.PhotoFov;
            _look.Create();
            _look.Apply(_settings);
            SteamStatic.SetScreenshotHandler(TakePhoto);

            FindTakenKeys();
            view.Show(_settings, PresetNames(), TimeNames(), WeatherNames(), PhotoCapture.MaxScale() > 1, _enteredPhase == GamePhase.Battle);
            view.SetHints(BuildHints());
            if (followSquad != Entity.Null && _enteredPhase == GamePhase.Battle)
            {
                _follower.Lock(followSquad, SquadFollower.Framing.Stay);
                _settings.Follow = true;
                view.Refresh();
                AnnounceFollow();
            }
        }

        private void Exit() => Close(false);

        /// <param name="sceneClosing">True on teardown: the camera and time are left alone, only what outlives the battle is restored.</param>
        private void Close(bool sceneClosing)
        {
            if (!_on) return;
            _on = false;

            // Each step stands alone, so one failure cannot leave the rest switched off.
            _follower.Clear();
            PhotoFocusGuide.Visible = false;
            Guard(() => SteamStatic.SetScreenshotHandler(null));
            Guard(() => view.Hide());
            Guard(() => _look.Destroy());
            if (!sceneClosing) Guard(() => BattleManager.Instance.BattlefieldEnvManager.ClearPhotoWeatherLook());
            if (!sceneClosing) Guard(() => _camera.ExitPhoto());
            Guard(() => _hider.Restore(sceneClosing));
            Guard(() => { if (_speed != null) _speed.EndPhotoHold(!sceneClosing); });
            Guard(() => { if (InputHandler.HasInstance) InputHandler.Instance.ClearBattleBlock(); });
            Guard(() => GameEventTracker.PhotoModeUsed(_photosTaken, Time.realtimeSinceStartup - _enteredAt, _enteredPhase.ToString(),
                PhotoPreset.All[_settings.Preset].NameKey, _settings.CaptureScale, _settings.DepthOfField));
        }

        private static void Guard(System.Action step)
        {
            try { step(); }
            catch (System.Exception e) { Debug.LogError($"[PhotoMode] A restore step failed: {e}"); }
        }
        #endregion

        #region While open
        private void Update()
        {
            if (!_on) return;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null || !Application.isFocused) return;

            bool changed = false;
            float rollInput = (Held(keyboard, Key.X) ? 1f : 0f) - (Held(keyboard, Key.Z) ? 1f : 0f);
            if (rollInput != 0f)
            {
                _settings.Roll = Mathf.Clamp(_settings.Roll + rollInput * RollDegreesPerSecond * Time.unscaledDeltaTime, -MaxRoll, MaxRoll);
                changed = true;
            }
            float fovInput = (Held(keyboard, Key.RightBracket) ? 1f : 0f) - (Held(keyboard, Key.LeftBracket) ? 1f : 0f);
            if (fovInput != 0f)
            {
                _settings.Fov = Mathf.Clamp(_settings.Fov + fovInput * FovDegreesPerSecond * Time.unscaledDeltaTime, MinFov, MaxFov);
                changed = true;
            }
            if (changed)
            {
                view.Refresh();
                ApplySettings();
            }

            if (Pressed(keyboard, Key.H)) view.SetChromeVisible(!view.ChromeVisible);
            if (Pressed(keyboard, Key.Space)) TakePhoto();
            if (Pressed(keyboard, Key.Period)) Step();
            if (Pressed(keyboard, Key.Backspace)) ResetAll();
            if (_enteredPhase == GamePhase.Battle)
            {
                if (Pressed(keyboard, Key.PageDown)) CycleSquad(1);
                if (Pressed(keyboard, Key.PageUp)) CycleSquad(-1);
            }
            if (Pressed(keyboard, Key.L) && _enteredPhase == GamePhase.Battle)
            {
                if (_settings.Follow) StopFollow();
                else StartFollow(mouse.position.ReadValue());
                view.Refresh();
            }

            if (mouse.leftButton.wasPressedThisFrame && _settings.DepthOfField
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                FocusAt(mouse.position.ReadValue());
        }

        private void LateUpdate()
        {
            if (!_on) return;
            // Markers and flags that appear while time runs are caught as it runs and once more when it stops.
            bool stepRunning = _speed.PhotoStepRunning;
            if (stepRunning || _stepWasRunning) _hider.Rescan();
            else if (Time.timeScale > 0f && Time.unscaledTime >= _nextRescan)
            {
                _nextRescan = Time.unscaledTime + RunningRescanSeconds;
                _hider.Rescan();
            }
            _stepWasRunning = stepRunning;
            FollowSquad();
            UpdateFocusGuide();
        }

        #region Follow
        private void OnFollowToggled(bool follow)
        {
            if (follow) StartFollow(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            else StopFollow();
            view.Refresh();
        }

        // Locks onto the squad nearest the ground point under the given screen position.
        private void StartFollow(Vector2 screenPoint)
        {
            _settings.Follow = false;
            _follower.Clear();
            if (!SquadFollower.GroundPoint(screenPoint, out Vector3 point)) return;
            Entity squad = SquadFollower.FindNearest(point, FollowPickRadius);
            if (squad == Entity.Null)
            {
                view.ShowSavedLine(LocalizationManager.Instance.GetText("photoFollowNone"));
                return;
            }
            _follower.Lock(squad, SquadFollower.Framing.Centre);
            _settings.Follow = true;
            AnnounceFollow();
        }

        // Page Down and Page Up step through the player's squads in order. A held framing carries over to the next squad.
        private void CycleSquad(int direction)
        {
            Entity next = _follower.NextPlayerSquad(direction, out bool fromCurrent);
            if (next == Entity.Null)
            {
                view.ShowSavedLine(LocalizationManager.Instance.GetText("photoFollowNone"));
                return;
            }
            _follower.Lock(next, fromCurrent ? SquadFollower.Framing.KeepOffset : SquadFollower.Framing.Centre);
            _settings.Follow = true;
            view.Refresh();
            AnnounceFollow();
        }

        private void AnnounceFollow()
        {
            string key = Time.timeScale > 0f ? "photoFollowing" : "photoFollowingFrozen";
            view.ShowSavedLine(string.Format(LocalizationManager.Instance.GetText(key), _follower.SquadName()));
        }

        private void StopFollow()
        {
            _settings.Follow = false;
            _follower.Stop();
        }

        // Moves the camera by exactly what the squad moved this frame, so it stays where the player framed it.
        private void FollowSquad()
        {
            if (!_settings.Follow || _follower.Tick(_settings.HeadBob)) return;
            _settings.Follow = false;
            view.Refresh();
        }
        #endregion

        private void UpdateFocusGuide()
        {
            // The guide is a tool for the panel: with the panel hidden the view is the picture.
            PhotoFocusGuide.Visible = _settings.FocusGuide && _settings.DepthOfField && view.ChromeVisible;
            PhotoFocusGuide.Distance = _settings.FocusDistance;
            // A stronger blur means a thinner slice is sharp.
            PhotoFocusGuide.HalfWidth = _settings.FocusDistance * Mathf.Lerp(0.3f, 0.05f, _settings.Blur);
        }

        // Changes the lens while moving the camera along its view, so whatever sits at the focus distance keeps its size.
        private void DollyZoom(float newFov)
        {
            newFov = Mathf.Clamp(newFov, MinFov, MaxFov);
            float oldDepth = _settings.FocusDistance;
            float newDepth = oldDepth * Mathf.Tan(_settings.Fov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(newFov * 0.5f * Mathf.Deg2Rad);
            newDepth = Mathf.Clamp(newDepth, 0.5f, MaxFocusDistance);
            Camera battleCamera = BattleManager.Instance.BattleCamera;
            _camera.FollowShift(-battleCamera.transform.forward * (newDepth - oldDepth));
            _settings.Fov = newFov;
            _settings.FocusDistance = newDepth;
            view.Refresh();
            ApplySettings();
        }

        private bool Held(Keyboard keyboard, Key key) => !_takenKeys.Contains(key) && keyboard[key].isPressed;
        private bool Pressed(Keyboard keyboard, Key key) => !_takenKeys.Contains(key) && keyboard[key].wasPressedThisFrame;

        private void ApplySettings()
        {
            _camera.SetPhotoFov(_settings.Fov);
            _camera.SetPhotoRoll(_settings.Roll);
            _camera.SetPhotoSpeedScale(_settings.MoveSpeed);
            _hider.SetFlagsVisible(_settings.ShowFlags);
            _look.Apply(_settings);
            _camera.SetPhotoBackdropBlur(_settings.BackdropBlur);

            if (_settings.PlaySpeed != _appliedPlaySpeed)
            {
                _appliedPlaySpeed = _settings.PlaySpeed;
                _speed.SetPhotoSpeed(PhotoSettings.PlaySpeeds[_settings.PlaySpeed]);
                _hider.Rescan();
            }
            if (_settings.WeatherLook != _appliedWeather)
            {
                _appliedWeather = _settings.WeatherLook;
                BattlefieldEnvManager weather = BattleManager.Instance.BattlefieldEnvManager;
                if (_appliedWeather == _realWeather) weather.ClearPhotoWeatherLook();
                else weather.SetPhotoWeatherLook(PhotoSettings.WeatherLooks[_appliedWeather]);
            }
        }

        private void ResetAll()
        {
            bool grid = _settings.Grid;
            bool flags = _settings.ShowFlags;
            int scale = _settings.CaptureScale;
            _settings = new PhotoSettings { Grid = grid, ShowFlags = flags, CaptureScale = scale, WeatherLook = _realWeather };
            _follower.Clear();
            _camera.ResetPhotoView();
            _settings.Fov = _camera.PhotoFov;
            view.Rebind(_settings);
            ApplySettings();
        }

        private void Step()
        {
            if (_speed.PhotoStep(StepSeconds)) _stepWasRunning = true;
        }

        private void FocusAtScreenCentre() => FocusAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

        // Depth of field measures along the view direction, so the focus is the hit point's depth, not its range.
        private void FocusAt(Vector2 screenPoint)
        {
            Camera battleCamera = BattleManager.Instance.BattleCamera;
            if (!SquadFollower.GroundPoint(screenPoint, out Vector3 point)) return;
            float depth = Vector3.Dot(point - battleCamera.transform.position, battleCamera.transform.forward);
            _settings.FocusDistance = Mathf.Clamp(depth, 0.5f, MaxFocusDistance);
            view.Refresh();
            ApplySettings();
        }

        private void TakePhoto()
        {
            if (!_on || PhotoCapture.Busy) return;
            int scale = _settings.DepthOfField ? 1 : _settings.CaptureScale;
            view.Flash();
            StartCoroutine(PhotoCapture.Capture(_camera.TavernCamera, scale, view.Canvas, OnPhotoSaved));
        }

        private void OnPhotoSaved(PhotoCapture.Result result)
        {
            if (result.Saved) _photosTaken++;
            if (!_on) return;
            string key = !result.Saved ? "photoSaveFailed" : result.FellBack ? "photoSavedSmaller" : "photoSaved";
            view.ShowSavedLine(LocalizationManager.Instance.GetText(key));
        }

        private void OpenFolder()
        {
            string folder = PhotoCapture.Folder;
            System.IO.Directory.CreateDirectory(folder);
            Application.OpenURL("file:///" + folder.Replace('\\', '/'));
        }
        #endregion

        #region Text
        private static string[] PresetNames()
        {
            var names = new string[PhotoPreset.All.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = LocalizationManager.Instance.GetText(PhotoPreset.All[i].NameKey);
            return names;
        }

        private static string[] TimeNames()
        {
            var names = new string[PhotoSettings.PlaySpeeds.Length];
            names[0] = LocalizationManager.Instance.GetText("photoTimeFrozen");
            for (int i = 1; i < names.Length; i++)
                names[i] = PhotoSettings.PlaySpeeds[i].ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "x";
            return names;
        }

        private static string[] WeatherNames()
        {
            string[] keys = { "photoWeatherClear", "photoWeatherRain", "photoWeatherSnow", "photoWeatherFog" };
            var names = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++) names[i] = LocalizationManager.Instance.GetText(keys[i]);
            return names;
        }

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

        private string BuildHints()
        {
            Keyboard keyboard = Keyboard.current;
            var hints = new System.Text.StringBuilder();
            void Add(Key key, string textKey)
            {
                if (keyboard == null || _takenKeys.Contains(key)) return;
                AddHint(hints, keyboard[key].displayName, textKey);
            }
            Add(Key.Space, "photoHintTake");
            Add(Key.H, "photoHintHide");
            if (_enteredPhase == GamePhase.Battle) Add(Key.Period, "photoHintStep");
            if (keyboard != null && !_takenKeys.Contains(Key.Z) && !_takenKeys.Contains(Key.X))
                AddHint(hints, keyboard[Key.Z].displayName + " / " + keyboard[Key.X].displayName, "photoHintRoll");
            if (_enteredPhase == GamePhase.Battle) Add(Key.L, "photoHintFollow");
            if (_enteredPhase == GamePhase.Battle && keyboard != null && !_takenKeys.Contains(Key.PageUp) && !_takenKeys.Contains(Key.PageDown))
                AddHint(hints, keyboard[Key.PageUp].displayName + " / " + keyboard[Key.PageDown].displayName, "photoHintCycle");
            Add(Key.Backspace, "photoHintReset");

            InputAction exit = InputHandler.Instance.GameControls.Battle.Get().FindAction("PhotoMode");
            if (exit != null && exit.bindings.Count > 0)
                AddHint(hints, InputControlPath.ToHumanReadableString(exit.bindings[0].effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice), "photoHintExit");
            return hints.ToString();
        }
        private static void AddHint(System.Text.StringBuilder hints, string keyName, string textKey)
        {
            if (hints.Length > 0) hints.Append("     ");
            hints.Append("<color=#E9C06A>").Append(keyName).Append("</color>  ").Append(LocalizationManager.Instance.GetText(textKey));
        }
        #endregion
    }
}
