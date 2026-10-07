using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TJ
{
    /// <summary>Everything the photo mode panel can change. One instance lives for one visit.</summary>
    public class PhotoSettings
    {
        public const float DefaultFocus = 45f;
        public const float DefaultBlur = 0.6f;
        // Play speeds offered inside photo mode; index 0 is the frozen battle.
        public static readonly float[] PlaySpeeds = { 0f, 0.1f, 0.5f, 1f };
        // The list starts on the battle's real weather, so every step away from it shows a change.
        public static readonly Weather[] WeatherLooks = { Weather.ClearSkies, Weather.Rain, Weather.Snow, Weather.Fog };

        // Camera
        public float Fov = 60f;
        public float Roll;
        public float MoveSpeed = 1f;
        public bool ShowFlags;

        // Lens
        public bool DepthOfField;
        public float FocusDistance = DefaultFocus;
        public float Blur = DefaultBlur;
        public bool FocusGuide;

        // Look
        public int Preset;
        public float Exposure;
        public float Contrast;
        public float Saturation;
        public float Warmth;
        public float Tint;
        public float ShadowTone;
        public float HighlightTone;

        // Effects
        public float Vignette;
        public float Bloom = 1f;
        public float Grain;
        public float ColourFringe;
        public float Distortion;
        public float BackdropBlur;

        // Scene
        public int PlaySpeed;
        public bool Follow;
        public bool HeadBob;
        public int WeatherLook;

        // Shot
        public int CaptureScale = 1;
        public bool Grid;

        public void ApplyPreset(int index)
        {
            PhotoPreset preset = PhotoPreset.All[Mathf.Clamp(index, 0, PhotoPreset.All.Length - 1)];
            Preset = index;
            DepthOfField = preset.DepthOfField;
            Blur = preset.Blur;
            Exposure = preset.Exposure;
            Contrast = preset.Contrast;
            Saturation = preset.Saturation;
            Warmth = preset.Warmth;
            Vignette = preset.Vignette;
            Tint = 0f;
            ShadowTone = preset.ShadowTone;
            HighlightTone = preset.HighlightTone;
            Grain = preset.Grain;
        }
    }

    public readonly struct PhotoPreset
    {
        public readonly string NameKey;
        public readonly bool DepthOfField;
        public readonly float Blur, Exposure, Contrast, Saturation, Warmth, Vignette, ShadowTone, HighlightTone, Grain;

        private PhotoPreset(string nameKey, bool depthOfField, float blur, float exposure, float contrast, float saturation, float warmth,
            float vignette, float shadowTone = 0f, float highlightTone = 0f, float grain = 0f)
        {
            NameKey = nameKey;
            DepthOfField = depthOfField;
            Blur = blur;
            Exposure = exposure;
            Contrast = contrast;
            Saturation = saturation;
            Warmth = warmth;
            Vignette = vignette;
            ShadowTone = shadowTone;
            HighlightTone = highlightTone;
            Grain = grain;
        }

        public static readonly PhotoPreset[] All =
        {
            new("photoLookNatural", false, PhotoSettings.DefaultBlur, 0f, 0f, 0f, 0f, 0f),
            new("photoLookMiniature", true, 0.7f, 0f, 10f, 15f, 0f, 0.25f),
            new("photoLookWarmTavern", false, PhotoSettings.DefaultBlur, 0.2f, 5f, 5f, 25f, 0.3f, 0f, 0.3f),
            new("photoLookColdSteel", false, PhotoSettings.DefaultBlur, 0f, 10f, -15f, -25f, 0.2f, -0.4f),
            new("photoLookFaded", false, PhotoSettings.DefaultBlur, 0.3f, -15f, -35f, 5f, 0f, 0.2f, 0f, 0.25f),
            new("photoLookInk", false, PhotoSettings.DefaultBlur, 0f, 25f, -100f, 0f, 0.3f, 0f, 0f, 0.35f),
        };
    }

    /// <summary>Read by the outline renderer feature, which tints what the lens is focused on while this is on.</summary>
    public static class PhotoFocusGuide
    {
        public static bool Visible;
        public static float Distance;
        public static float HalfWidth;
    }

    /// <summary>
    /// Photo mode's own post-processing: one global volume on the Battlefield camera's volume layer. That camera is
    /// the last in the stack, so its post-processing covers the tavern backdrop as well.
    /// </summary>
    public class PhotoLook
    {
        private const int VolumeLayer = 0;
        private const float VolumePriority = 1000f;
        // Bokeh strength at full blur. A circle of confusion constant: focal length squared over aperture times focus distance.
        private const float MaxBlurConstant = 0.004f;
        private const float FallbackBloom = 1f;
        private static readonly Color WarmTone = new(1f, 0.82f, 0.62f);
        private static readonly Color CoolTone = new(0.62f, 0.82f, 1f);

        private GameObject _volumeObject;
        private VolumeProfile _profile;
        private DepthOfField _depthOfField;
        private ColorAdjustments _color;
        private WhiteBalance _whiteBalance;
        private Vignette _vignette;
        private ShadowsMidtonesHighlights _tones;
        private Bloom _bloom;
        private FilmGrain _grain;
        private ChromaticAberration _fringe;
        private LensDistortion _distortion;
        // The battle's own bloom when photo mode opened; the slider multiplies it.
        private float _baseBloom;

        public void Create()
        {
            if (_volumeObject != null) return;
            Bloom sceneBloom = VolumeManager.instance.stack.GetComponent<Bloom>();
            _baseBloom = sceneBloom != null && sceneBloom.intensity.value > 0f ? sceneBloom.intensity.value : FallbackBloom;

            _volumeObject = new GameObject("Photo Mode Volume") { layer = VolumeLayer };
            Volume volume = _volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = VolumePriority;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = _profile;

            _depthOfField = _profile.Add<DepthOfField>();
            _color = _profile.Add<ColorAdjustments>();
            _whiteBalance = _profile.Add<WhiteBalance>();
            _vignette = _profile.Add<Vignette>();
            _tones = _profile.Add<ShadowsMidtonesHighlights>();
            _bloom = _profile.Add<Bloom>();
            _grain = _profile.Add<FilmGrain>();
            _fringe = _profile.Add<ChromaticAberration>();
            _distortion = _profile.Add<LensDistortion>();
            // A moving camera over a frozen battle would smear.
            MotionBlur motionBlur = _profile.Add<MotionBlur>();
            motionBlur.intensity.Override(0f);
        }

        public void Apply(PhotoSettings settings)
        {
            if (_profile == null) return;

            _depthOfField.active = settings.DepthOfField;
            if (settings.DepthOfField)
            {
                float focus = Mathf.Max(0.5f, settings.FocusDistance);
                // The focal length grows with the focus distance, so the same slider value blurs a close-up and an overview alike.
                float blurConstant = settings.Blur * settings.Blur * MaxBlurConstant;
                float focalLength = Mathf.Clamp(Mathf.Sqrt(blurConstant * focus) * 1000f, 1f, 300f);
                _depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
                _depthOfField.focusDistance.Override(focus);
                _depthOfField.focalLength.Override(focalLength);
                _depthOfField.aperture.Override(1f);
            }

            // A neutral value leaves the battle's own volumes (weather, time of day) in charge of that setting.
            SetOrRelease(_color.postExposure, settings.Exposure);
            SetOrRelease(_color.contrast, settings.Contrast);
            SetOrRelease(_color.saturation, settings.Saturation);
            SetOrRelease(_whiteBalance.temperature, settings.Warmth);
            SetOrRelease(_whiteBalance.tint, settings.Tint);
            SetOrRelease(_vignette.intensity, settings.Vignette);
            SetOrRelease(_grain.intensity, settings.Grain);
            SetOrRelease(_fringe.intensity, settings.ColourFringe);
            SetOrRelease(_distortion.intensity, settings.Distortion);
            SetTone(_tones.shadows, settings.ShadowTone);
            SetTone(_tones.highlights, settings.HighlightTone);

            if (Mathf.Approximately(settings.Bloom, 1f)) _bloom.intensity.overrideState = false;
            else _bloom.intensity.Override(_baseBloom * settings.Bloom);
        }

        public void Destroy()
        {
            // A runtime profile is never freed on its own.
            if (_profile != null) Object.Destroy(_profile);
            if (_volumeObject != null) Object.Destroy(_volumeObject);
            _profile = null;
            _volumeObject = null;
        }

        private static void SetOrRelease(VolumeParameter<float> parameter, float value)
        {
            if (Mathf.Approximately(value, 0f)) parameter.overrideState = false;
            else parameter.Override(value);
        }

        // -1 pulls the range toward a cool blue, +1 toward a warm orange; 0 leaves it alone.
        private static void SetTone(Vector4Parameter parameter, float tone)
        {
            if (Mathf.Approximately(tone, 0f))
            {
                parameter.overrideState = false;
                return;
            }
            Color colour = Color.Lerp(Color.white, tone > 0f ? WarmTone : CoolTone, Mathf.Abs(tone));
            parameter.Override(new Vector4(colour.r, colour.g, colour.b, 0f));
        }
    }
}
