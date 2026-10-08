using System;
using System.Collections;
using Memori.Audio;
using Memori.Localization;
using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ
{
    /// <summary>
    /// The photo mode panel. It only shows and edits a PhotoSettings; PhotoMode owns every rule and applies the
    /// values to the camera, the look and the capture.
    /// </summary>
    public class PhotoModePanelView : MonoBehaviour
    {
        [Header("Chrome")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup rootGroup;
        // The panel and hint bar; hidden together when the player wants a clear view.
        [SerializeField] private CanvasGroup chromeGroup;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private TMP_Text savedLabel;
        [SerializeField] private GameObject grid;
        [SerializeField] private Image flash;

        [Header("Tabs")]
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private GameObject[] tabMarks;
        [SerializeField] private GameObject[] tabPages;

        [Header("Camera")]
        [SerializeField] private Slider fovSlider;
        [SerializeField] private Slider rollSlider, speedSlider;
        [SerializeField] private TMP_Text fovValue, rollValue, speedValue;
        [SerializeField] private Toggle flagsToggle;
        [SerializeField] private Button resetButton;

        [Header("Lens")]
        [SerializeField] private Toggle depthOfFieldToggle;
        [SerializeField] private Slider focusSlider, blurSlider;
        [SerializeField] private TMP_Text focusValue, blurValue;
        [SerializeField] private Button autofocusButton;
        [SerializeField] private Toggle focusGuideToggle;
        [SerializeField] private Slider dollySlider;
        [SerializeField] private TMP_Text dollyValue;

        [Header("Look")]
        [SerializeField] private TMP_Dropdown presetDropdown;
        [SerializeField] private Slider exposureSlider, contrastSlider, saturationSlider, warmthSlider, tintSlider, shadowToneSlider, highlightToneSlider;
        [SerializeField] private TMP_Text exposureValue, contrastValue, saturationValue, warmthValue, tintValue, shadowToneValue, highlightToneValue;

        [Header("Effects")]
        [SerializeField] private Slider vignetteSlider;
        [SerializeField] private Slider bloomSlider, grainSlider, fringeSlider, distortionSlider, backdropSlider;
        [SerializeField] private TMP_Text vignetteValue, bloomValue, grainValue, fringeValue, distortionValue, backdropValue;

        [Header("Scene")]
        [SerializeField] private TMP_Dropdown timeDropdown;
        [SerializeField] private Toggle followToggle;
        [SerializeField] private Toggle headBobToggle;
        [SerializeField] private TMP_Dropdown weatherDropdown;

        [Header("Shot")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle gridToggle;
        [SerializeField] private Button openFolderButton, takePhotoButton, exitButton;

        public event Action Changed, ResetClicked, AutofocusClicked, OpenFolderClicked, TakePhotoClicked, ExitClicked;
        // These two need the controller to act before the value is real: pick a squad, move the camera.
        public event Action<bool> FollowChanged;
        public event Action<float> DollyChanged;

        public Canvas Canvas => canvas;
        public bool ChromeVisible => chromeGroup.alpha > 0.5f;

        private const float SavedLineSeconds = 4f;
        private const float FlashSeconds = 0.25f;
        private const float FlashAlpha = 0.55f;

        private PhotoSettings _settings;
        // Set while Bind pushes values into the controls, so they do not echo back as edits.
        private bool _binding;
        private bool _wired;
        private int _tab;
        private Coroutine _fade, _savedLine, _flashRoutine;

        private void Awake()
        {
            // The root stays active so nested layout runs; it is hidden by alpha until photo mode opens.
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            grid.SetActive(false);
            flash.gameObject.SetActive(false);
            savedLabel.text = string.Empty;
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < tabButtons.Length; i++)
            {
                int tab = i;
                tabButtons[i].onClick.AddListener(() => ShowTab(tab, true));
            }

            Bind(fovSlider, v => _settings.Fov = v);
            Bind(rollSlider, v => _settings.Roll = v);
            Bind(speedSlider, v => _settings.MoveSpeed = v);
            Bind(focusSlider, v => _settings.FocusDistance = v);
            Bind(blurSlider, v => _settings.Blur = v);
            Bind(exposureSlider, v => _settings.Exposure = v);
            Bind(contrastSlider, v => _settings.Contrast = v);
            Bind(saturationSlider, v => _settings.Saturation = v);
            Bind(warmthSlider, v => _settings.Warmth = v);
            Bind(vignetteSlider, v => _settings.Vignette = v);
            Bind(tintSlider, v => _settings.Tint = v);
            Bind(shadowToneSlider, v => _settings.ShadowTone = v);
            Bind(highlightToneSlider, v => _settings.HighlightTone = v);
            Bind(bloomSlider, v => _settings.Bloom = v);
            Bind(grainSlider, v => _settings.Grain = v);
            Bind(fringeSlider, v => _settings.ColourFringe = v);
            Bind(distortionSlider, v => _settings.Distortion = v);
            Bind(backdropSlider, v => _settings.BackdropBlur = v);
            Bind(flagsToggle, v => _settings.ShowFlags = v);
            Bind(depthOfFieldToggle, v => _settings.DepthOfField = v);
            Bind(focusGuideToggle, v => _settings.FocusGuide = v);
            Bind(gridToggle, v => _settings.Grid = v);
            Bind(headBobToggle, v => _settings.HeadBob = v);

            dollySlider.onValueChanged.AddListener(value =>
            {
                if (!_binding) DollyChanged?.Invoke(value);
            });
            followToggle.onValueChanged.AddListener(value =>
            {
                if (!_binding) FollowChanged?.Invoke(value);
            });
            timeDropdown.onValueChanged.AddListener(index =>
            {
                if (_binding) return;
                _settings.PlaySpeed = index;
                Changed?.Invoke();
            });
            weatherDropdown.onValueChanged.AddListener(index =>
            {
                if (_binding) return;
                _settings.WeatherLook = index;
                Changed?.Invoke();
            });

            presetDropdown.onValueChanged.AddListener(index =>
            {
                if (_binding) return;
                _settings.ApplyPreset(index);
                Refresh();
                Changed?.Invoke();
            });
            resolutionDropdown.onValueChanged.AddListener(index =>
            {
                if (_binding) return;
                _settings.CaptureScale = index + 1;
                Changed?.Invoke();
            });

            resetButton.onClick.AddListener(() => ResetClicked?.Invoke());
            autofocusButton.onClick.AddListener(() => AutofocusClicked?.Invoke());
            openFolderButton.onClick.AddListener(() => OpenFolderClicked?.Invoke());
            takePhotoButton.onClick.AddListener(() => TakePhotoClicked?.Invoke());
            exitButton.onClick.AddListener(() => ExitClicked?.Invoke());

            // WASD is Unity's UI navigation and would drag a selected slider while the camera flies.
            foreach (Selectable selectable in GetComponentsInChildren<Selectable>(true))
                selectable.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private void Bind(Slider slider, Action<float> write)
        {
            slider.onValueChanged.AddListener(value =>
            {
                if (_binding) return;
                write(value);
                RefreshValueLabels();
                Changed?.Invoke();
            });
        }
        private void Bind(Toggle toggle, Action<bool> write)
        {
            toggle.onValueChanged.AddListener(value =>
            {
                if (_binding) return;
                write(value);
                Refresh();
                Changed?.Invoke();
            });
        }

        private void LateUpdate()
        {
            // A clicked control keeps UI focus, and then Space or the camera keys would act on it.
            if (rootGroup.interactable && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && !global::Memori.Input.GameCursor.GetButton(0))
                EventSystem.current.SetSelectedGameObject(null);
        }

        #region Shown by PhotoMode
        public void Show(PhotoSettings settings, string[] presetNames, string[] timeNames, string[] weatherNames, bool canPickResolution, bool battleRunning)
        {
            Wire();
            _settings = settings;

            _binding = true;
            presetDropdown.ClearOptions();
            presetDropdown.AddOptions(new System.Collections.Generic.List<string>(presetNames));
            timeDropdown.ClearOptions();
            timeDropdown.AddOptions(new System.Collections.Generic.List<string>(timeNames));
            weatherDropdown.ClearOptions();
            weatherDropdown.AddOptions(new System.Collections.Generic.List<string>(weatherNames));
            // Time only runs, and squads only move, while the battle is being fought; the whole row greys so it reads as off.
            timeDropdown.interactable = battleRunning;
            followToggle.interactable = battleRunning;
            SetRowAvailable(timeDropdown, battleRunning);
            SetRowAvailable(followToggle, battleRunning);
            headBobToggle.interactable = battleRunning;
            SetRowAvailable(headBobToggle, battleRunning);
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(canPickResolution
                ? new System.Collections.Generic.List<string> { "1x", "2x" }
                : new System.Collections.Generic.List<string> { "1x" });
            _binding = false;

            // Under Proton the folder sits inside the game's prefix, where a player cannot use it; the Steam library is the way in.
            openFolderButton.gameObject.SetActive(!UIScaler.IsSteamDeck);
            Refresh();
            ShowTab(0, false);
            SetChromeVisible(true);
            savedLabel.text = string.Empty;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
            Fade(UIJuice.Open(rootGroup, panel));
        }

        /// <summary>Swaps in a fresh settings object (Reset) without replaying the open animation.</summary>
        public void Rebind(PhotoSettings settings)
        {
            _settings = settings;
            Refresh();
        }

        public void Hide()
        {
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            grid.SetActive(false);
            flash.gameObject.SetActive(false);
            if (isActiveAndEnabled) Fade(UIJuice.Close(rootGroup));
            else rootGroup.alpha = 0f;
        }

        /// <summary>Pushes the settings into every control without raising Changed.</summary>
        public void Refresh()
        {
            _binding = true;
            fovSlider.value = _settings.Fov;
            rollSlider.value = _settings.Roll;
            speedSlider.value = _settings.MoveSpeed;
            flagsToggle.isOn = _settings.ShowFlags;
            depthOfFieldToggle.isOn = _settings.DepthOfField;
            focusSlider.value = _settings.FocusDistance;
            blurSlider.value = _settings.Blur;
            presetDropdown.SetValueWithoutNotify(_settings.Preset);
            exposureSlider.value = _settings.Exposure;
            contrastSlider.value = _settings.Contrast;
            saturationSlider.value = _settings.Saturation;
            warmthSlider.value = _settings.Warmth;
            vignetteSlider.value = _settings.Vignette;
            tintSlider.value = _settings.Tint;
            shadowToneSlider.value = _settings.ShadowTone;
            highlightToneSlider.value = _settings.HighlightTone;
            bloomSlider.value = _settings.Bloom;
            grainSlider.value = _settings.Grain;
            fringeSlider.value = _settings.ColourFringe;
            distortionSlider.value = _settings.Distortion;
            backdropSlider.value = _settings.BackdropBlur;
            dollySlider.value = _settings.Fov;
            focusGuideToggle.isOn = _settings.FocusGuide;
            followToggle.isOn = _settings.Follow;
            headBobToggle.isOn = _settings.HeadBob;
            timeDropdown.SetValueWithoutNotify(_settings.PlaySpeed);
            weatherDropdown.SetValueWithoutNotify(_settings.WeatherLook);
            gridToggle.isOn = _settings.Grid;
            // 2x renders the blur at half strength, so it is only offered while depth of field is off.
            resolutionDropdown.interactable = !_settings.DepthOfField && resolutionDropdown.options.Count > 1;
            resolutionDropdown.SetValueWithoutNotify(Mathf.Clamp(_settings.CaptureScale - 1, 0, resolutionDropdown.options.Count - 1));
            focusSlider.interactable = _settings.DepthOfField;
            blurSlider.interactable = _settings.DepthOfField;
            autofocusButton.interactable = _settings.DepthOfField;
            focusGuideToggle.interactable = _settings.DepthOfField;
            _binding = false;

            grid.SetActive(_settings.Grid && ChromeVisible);
            RefreshValueLabels();
        }

        private static void SetRowAvailable(Selectable control, bool available)
        {
            TJ.Settings.SettingRow row = control.GetComponentInParent<TJ.Settings.SettingRow>(true);
            if (row != null) row.SetAvailable(available);
        }

        public void SetChromeVisible(bool visible)
        {
            chromeGroup.alpha = visible ? 1f : 0f;
            chromeGroup.interactable = visible;
            chromeGroup.blocksRaycasts = visible;
            grid.SetActive(visible && _settings != null && _settings.Grid);
        }

        public void SetHints(string hints) => hintLabel.text = hints;

        public void Flash()
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
        }

        public void ShowSavedLine(string text)
        {
            if (_savedLine != null) StopCoroutine(_savedLine);
            _savedLine = StartCoroutine(SavedLineRoutine(text));
        }
        #endregion

        private void ShowTab(int tab, bool playSound)
        {
            _tab = tab;
            for (int i = 0; i < tabPages.Length; i++)
            {
                tabPages[i].SetActive(i == tab);
                tabMarks[i].SetActive(i == tab);
            }
            if (playSound) IAudioRequester.Instance.PlaySFX(SFXData.TinyClick);
        }

        private void RefreshValueLabels()
        {
            fovValue.text = Mathf.RoundToInt(_settings.Fov).ToString();
            rollValue.text = Mathf.RoundToInt(_settings.Roll).ToString();
            speedValue.text = Mathf.RoundToInt(_settings.MoveSpeed * 100f) + "%";
            focusValue.text = Mathf.RoundToInt(_settings.FocusDistance).ToString();
            blurValue.text = Mathf.RoundToInt(_settings.Blur * 100f).ToString();
            exposureValue.text = Signed(_settings.Exposure * 10f);
            contrastValue.text = Signed(_settings.Contrast);
            saturationValue.text = Signed(_settings.Saturation);
            warmthValue.text = Signed(_settings.Warmth);
            vignetteValue.text = Mathf.RoundToInt(_settings.Vignette * 100f).ToString();
            tintValue.text = Signed(_settings.Tint);
            shadowToneValue.text = Signed(_settings.ShadowTone * 100f);
            highlightToneValue.text = Signed(_settings.HighlightTone * 100f);
            bloomValue.text = Mathf.RoundToInt(_settings.Bloom * 100f) + "%";
            grainValue.text = Mathf.RoundToInt(_settings.Grain * 100f).ToString();
            fringeValue.text = Mathf.RoundToInt(_settings.ColourFringe * 100f).ToString();
            distortionValue.text = Signed(_settings.Distortion * 100f);
            backdropValue.text = Mathf.RoundToInt(_settings.BackdropBlur * 100f).ToString();
            dollyValue.text = Mathf.RoundToInt(_settings.Fov).ToString();
        }
        private static string Signed(float value)
        {
            int rounded = Mathf.RoundToInt(value);
            return rounded > 0 ? "+" + rounded : rounded.ToString();
        }

        private void Fade(IEnumerator routine)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(routine);
        }

        private IEnumerator FlashRoutine()
        {
            flash.gameObject.SetActive(true);
            Color colour = flash.color;
            for (float t = 0f; t < FlashSeconds; t += Time.unscaledDeltaTime)
            {
                colour.a = Mathf.Lerp(FlashAlpha, 0f, t / FlashSeconds);
                flash.color = colour;
                yield return null;
            }
            flash.gameObject.SetActive(false);
            _flashRoutine = null;
        }

        private IEnumerator SavedLineRoutine(string text)
        {
            savedLabel.text = text;
            yield return new WaitForSecondsRealtime(SavedLineSeconds);
            savedLabel.text = string.Empty;
            _savedLine = null;
        }
    }
}
