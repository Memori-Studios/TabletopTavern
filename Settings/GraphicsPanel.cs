using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Memori.Notifications;
using Memori.Localization;

namespace TJ
{
public class GraphicsPanel : MonoBehaviour
{
    [SerializeField] private Button applyVideoSettingsButton, resetVideoSettingsButton;

    [Header("Dropdowns")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown refreshRateDropdown, graphicsQualityDropdown, antiAliasingDropdown;
    [SerializeField] private TMP_Dropdown shadowQualityDropdown;
    [SerializeField] private TMP_Dropdown renderScaleDropdown;
    [SerializeField] private TMP_Dropdown textureQualityDropdown;
    [SerializeField] private TMP_Dropdown fpsLimitDropdown;

    [Header("Toggles")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vsyncToggle, fpsToggle;
    [SerializeField] private Toggle ambientOcclusionToggle;
    [SerializeField] private Toggle bloomToggle;

    [Header("URP References")]
    [SerializeField] private ScriptableRendererData rendererData;
    [SerializeField] private VolumeProfile[] bloomProfiles;

    [Header("Hardware Detection")]
    [SerializeField] private TMP_Text hardwareTierLabel;

    RefreshRate refreshRateObj = new(){numerator = 60, denominator = 1};
    FullScreenMode fullScreenMode;
    [SerializeField] private GameObject fpsObject;

    // Shadow quality tiers: Off / Low / Medium / High / Ultra
    // Two cascades cap the shadow caster passes; every unit is drawn once per cascade.
    static readonly float[] ShadowDistances = { 0f, 30f, 80f, 120f, 150f };
    static readonly int[]   ShadowCascades  = { 1,  1,   2,   2,    2   };
    static readonly int[]   ShadowResolutions = { 256, 512, 1024, 2048, 2048 };

    static readonly float[] RenderScaleValues = { 0.5f, 0.75f, 1.0f };

    // Index 0 is Unlimited; -1 is Unity's "no cap" value for targetFrameRate
    static readonly int[] FpsLimitValues = { -1, 30, 60, 90, 120, 144, 165, 240 };

    enum HardwareTier { Low, Medium, High, Ultra }

    private void Start()
    {
        applyVideoSettingsButton.onClick.AddListener(ApplyVideoSettings);
        resetVideoSettingsButton.onClick.AddListener(ResetVideoSettings);

        AutoConfigureGraphics();
        SetHardwareTierLabel();
        SetUpDropdowns();

        vsyncToggle.isOn = PlayerPrefs.GetInt("VSync", 1) == 1;
        fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        fpsToggle.isOn = PlayerPrefs.GetInt("DisplayFPS", 0) == 1;
        ambientOcclusionToggle.isOn = PlayerPrefs.GetInt("AmbientOcclusion", 1) == 1;
        bloomToggle.isOn = PlayerPrefs.GetInt("Bloom", 1) == 1;

        antiAliasingDropdown.value = PlayerPrefs.GetInt("MSAA", 0);
        resolutionDropdown.value = PlayerPrefs.GetInt("Resolution", resolutionDropdown.options.Count - 1);
        graphicsQualityDropdown.value = PlayerPrefs.GetInt("GraphicsQuality", graphicsQualityDropdown.options.Count - 1);
        refreshRateDropdown.value = PlayerPrefs.GetInt("RefreshRate", refreshRateDropdown.options.Count - 1);
        shadowQualityDropdown.value = PlayerPrefs.GetInt("ShadowQuality", 3);
        renderScaleDropdown.value = PlayerPrefs.GetInt("RenderScale", 2);
        textureQualityDropdown.value = PlayerPrefs.GetInt("TextureQuality", 0);
        fpsLimitDropdown.value = PlayerPrefs.GetInt("FPSLimit", 0);

        resolutionDropdown.RefreshShownValue();
        antiAliasingDropdown.RefreshShownValue();
        graphicsQualityDropdown.RefreshShownValue();
        refreshRateDropdown.RefreshShownValue();
        shadowQualityDropdown.RefreshShownValue();
        renderScaleDropdown.RefreshShownValue();
        textureQualityDropdown.RefreshShownValue();
        fpsLimitDropdown.RefreshShownValue();

        ToggleFPSCounter(fpsToggle.isOn);
        fpsToggle.onValueChanged.AddListener(delegate {
            ToggleFPSCounter(fpsToggle.isOn);
            PlayerPrefs.SetInt("DisplayFPS", fpsToggle.isOn ? 1 : 0);
        });

        #if !UNITY_EDITOR
            uint refreshRateNumerator = uint.Parse(refreshRateDropdown.options[refreshRateDropdown.value].text.Split(' ')[0]);
            refreshRateObj = new(){ numerator = refreshRateNumerator, denominator = 1};

            var res = resolutionDropdown.options[resolutionDropdown.value].text.Split('x');
            int resolutionWidth = int.Parse(res[0]);
            int resolutionHeight = int.Parse(res[1]);
            fullScreenMode = fullscreenToggle.isOn ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            ApplyMsaa(antiAliasingDropdown.value);
            QualitySettings.SetQualityLevel(graphicsQualityDropdown.value);
            ApplyVSyncAndFpsLimit(vsyncToggle.isOn, fpsLimitDropdown.value);
            Screen.SetResolution(resolutionWidth, resolutionHeight, fullScreenMode, refreshRateObj);
            Cursor.lockState = fullscreenToggle.isOn ? CursorLockMode.Confined : CursorLockMode.None;

            ApplyShadowQuality(shadowQualityDropdown.value);
            ApplyAmbientOcclusion(ambientOcclusionToggle.isOn);
            ApplyRenderScale(renderScaleDropdown.value);
            ApplyTextureQuality(textureQualityDropdown.value);
            ApplyBloom(bloomToggle.isOn);
        #endif
    }

    private void AutoConfigureGraphics()
    {
        if (PlayerPrefs.HasKey("HasConfiguredGraphics")) return;

        ApplyTierDefaults(DetectHardwareTier());
        PlayerPrefs.SetInt("HasConfiguredGraphics", 1);
        PlayerPrefs.Save();
    }

    private HardwareTier DetectHardwareTier()
    {
        int vram = SystemInfo.graphicsMemorySize;
        string gpu = SystemInfo.graphicsDeviceName.ToLowerInvariant();

        bool isIntegrated = vram < 1024
            || gpu.Contains("intel hd")
            || gpu.Contains("intel uhd")
            || gpu.Contains("intel iris")
            || gpu.Contains("radeon(tm)"); // AMD integrated (no discrete RX branding)

        if (isIntegrated || vram < 2048) return HardwareTier.Low;
        if (vram < 4096)  return HardwareTier.Medium;
        if (vram < 8192)  return HardwareTier.High;
        return HardwareTier.Ultra;
    }

    private void ApplyTierDefaults(HardwareTier tier)
    {
        switch (tier)
        {
            case HardwareTier.Low:
                PlayerPrefs.SetInt("ShadowQuality",    1); // Low
                PlayerPrefs.SetInt("AmbientOcclusion", 0); // Off
                PlayerPrefs.SetInt("Bloom",            0); // Off
                PlayerPrefs.SetInt("RenderScale",      0); // 50%
                PlayerPrefs.SetInt("TextureQuality",   1); // Half
                PlayerPrefs.SetInt("MSAA",             0); // Off, SMAA is always on
                break;
            case HardwareTier.Medium:
                PlayerPrefs.SetInt("ShadowQuality",    2); // Medium
                PlayerPrefs.SetInt("AmbientOcclusion", 1); // On
                PlayerPrefs.SetInt("Bloom",            1); // On
                PlayerPrefs.SetInt("RenderScale",      1); // 75%
                PlayerPrefs.SetInt("TextureQuality",   0); // Full
                PlayerPrefs.SetInt("MSAA",             1); // 2x
                break;
            case HardwareTier.High:
                PlayerPrefs.SetInt("ShadowQuality",    3); // High
                PlayerPrefs.SetInt("AmbientOcclusion", 1); // On
                PlayerPrefs.SetInt("Bloom",            1); // On
                PlayerPrefs.SetInt("RenderScale",      2); // 100%
                PlayerPrefs.SetInt("TextureQuality",   0); // Full
                PlayerPrefs.SetInt("MSAA",             2); // 4x
                break;
            case HardwareTier.Ultra:
                PlayerPrefs.SetInt("ShadowQuality",    4); // Ultra
                PlayerPrefs.SetInt("AmbientOcclusion", 1); // On
                PlayerPrefs.SetInt("Bloom",            1); // On
                PlayerPrefs.SetInt("RenderScale",      2); // 100%
                PlayerPrefs.SetInt("TextureQuality",   0); // Full
                PlayerPrefs.SetInt("MSAA",             2); // 4x
                break;
        }
    }

    private void SetHardwareTierLabel()
    {
        if (hardwareTierLabel == null) return;
        HardwareTier tier = DetectHardwareTier();
        int vram = SystemInfo.graphicsMemorySize;
        string gpu = SystemInfo.graphicsDeviceName;
        string graphicsPreset = LocalizationManager.Instance.GetText("graphicsPreset");
        hardwareTierLabel.text = $"{graphicsPreset} [{tier}]  ·  {gpu}  ·  {vram} MB VRAM";
    }

    private void SetUpDropdowns()
    {
        resolutionDropdown.ClearOptions();
        refreshRateDropdown.ClearOptions();
        graphicsQualityDropdown.ClearOptions();
        antiAliasingDropdown.ClearOptions();
        shadowQualityDropdown.ClearOptions();
        renderScaleDropdown.ClearOptions();
        textureQualityDropdown.ClearOptions();
        fpsLimitDropdown.ClearOptions();

        // Resolution dropdown
        foreach(var res in Screen.resolutions){
            if(resolutionDropdown.options.Find(x => x.text == $"{res.width}x{res.height}") == null)
                resolutionDropdown.options.Add(new TMP_Dropdown.OptionData($"{res.width}x{res.height}"));
        }
        foreach(TMP_Dropdown.OptionData item in resolutionDropdown.options){
            if(item.text == $"{Screen.currentResolution.width}x{Screen.currentResolution.height}"){
                resolutionDropdown.value = resolutionDropdown.options.IndexOf(item);
                break;
            }
        }

        // Refresh rate dropdown — sort before finding current value so the index stays valid
        foreach(var res in Screen.resolutions){
            if(refreshRateDropdown.options.Find(x => x.text == $"{(int)res.refreshRateRatio.value} Hz") == null)
                refreshRateDropdown.options.Add(new TMP_Dropdown.OptionData($"{(int)res.refreshRateRatio.value} Hz", null, Color.white));
        }
        refreshRateDropdown.options.Sort((x, y) => int.Parse(x.text.Split(' ')[0]).CompareTo(int.Parse(y.text.Split(' ')[0])));
        foreach(TMP_Dropdown.OptionData item in refreshRateDropdown.options){
            if(item.text == $"{(int)Screen.currentResolution.refreshRateRatio.value} Hz"){
                refreshRateDropdown.value = refreshRateDropdown.options.IndexOf(item);
                refreshRateDropdown.RefreshShownValue();
                break;
            }
        }

        // Anti-aliasing dropdown
        antiAliasingDropdown.AddOptions(new List<string>(){ "Off", "2x", "4x", "8x" });
        antiAliasingDropdown.value = PlayerPrefs.GetInt("MSAA", 0);
        antiAliasingDropdown.RefreshShownValue();

        // Graphics quality dropdown
        foreach(string qualityLevel in QualitySettings.names){
            if(graphicsQualityDropdown.options.Find(x => x.text == qualityLevel) == null)
                graphicsQualityDropdown.options.Add(new TMP_Dropdown.OptionData(qualityLevel));
        }
        foreach(TMP_Dropdown.OptionData item in graphicsQualityDropdown.options){
            if(item.text == QualitySettings.names[QualitySettings.GetQualityLevel()]){
                graphicsQualityDropdown.value = graphicsQualityDropdown.options.IndexOf(item);
                graphicsQualityDropdown.RefreshShownValue();
                break;
            }
        }

        // Shadow quality dropdown
        shadowQualityDropdown.AddOptions(new List<string>(){ "Off", "Low", "Medium", "High", "Ultra" });

        // Render scale dropdown
        renderScaleDropdown.AddOptions(new List<string>(){ "50%", "75%", "100%" });

        // Texture quality dropdown
        textureQualityDropdown.AddOptions(new List<string>(){ "Full", "Half", "Quarter" });

        // FPS limit dropdown, one option per FpsLimitValues entry
        var fpsOptions = new List<string>(){ LocalizationManager.Instance.GetText("Unlimited") };
        for (int i = 1; i < FpsLimitValues.Length; i++)
            fpsOptions.Add(FpsLimitValues[i].ToString());
        fpsLimitDropdown.AddOptions(fpsOptions);
    }

    public void ApplyVideoSettings()
    {
        var res = resolutionDropdown.options[resolutionDropdown.value].text.Split('x');
        int resolutionWidth = int.Parse(res[0]);
        int resolutionHeight = int.Parse(res[1]);
        fullScreenMode = fullscreenToggle.isOn ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        uint refreshRateNumerator = uint.Parse(refreshRateDropdown.options[refreshRateDropdown.value].text.Split(' ')[0]);
        refreshRateObj = new(){ numerator = refreshRateNumerator, denominator = 1};

        ApplyMsaa(antiAliasingDropdown.value);
        QualitySettings.SetQualityLevel(graphicsQualityDropdown.value);
        ApplyVSyncAndFpsLimit(vsyncToggle.isOn, fpsLimitDropdown.value);
        Screen.SetResolution(resolutionWidth, resolutionHeight, fullScreenMode, refreshRateObj);
        Cursor.lockState = fullscreenToggle.isOn ? CursorLockMode.Confined : CursorLockMode.None;
        StartCoroutine(ForceCanvasRebuildNextFrame());

        ApplyShadowQuality(shadowQualityDropdown.value);
        ApplyAmbientOcclusion(ambientOcclusionToggle.isOn);
        ApplyRenderScale(renderScaleDropdown.value);
        ApplyTextureQuality(textureQualityDropdown.value);
        ApplyBloom(bloomToggle.isOn);

        SaveSettings();
        string applyChangesLocalized = LocalizationManager.Instance.GetText("ApplyChanges")+"!";
        NotificationManager.Instance.DisplayNotification(applyChangesLocalized);
    }

    public void ResetVideoSettings()
    {
        // Display settings reset (not hardware-dependent)
        vsyncToggle.isOn = true;
        fullscreenToggle.isOn = true;
        fullScreenMode = FullScreenMode.FullScreenWindow;
        fpsToggle.isOn = false;
        resolutionDropdown.value = resolutionDropdown.options.Count - 1;
        refreshRateDropdown.value = refreshRateDropdown.options.Count - 1;
        graphicsQualityDropdown.value = graphicsQualityDropdown.options.Count - 1;
        fpsLimitDropdown.value = 0;

        // Re-detect hardware and write tier defaults to PlayerPrefs
        ApplyTierDefaults(DetectHardwareTier());

        // Read tier defaults back into UI
        antiAliasingDropdown.value   = PlayerPrefs.GetInt("MSAA", 0);
        shadowQualityDropdown.value  = PlayerPrefs.GetInt("ShadowQuality", 3);
        renderScaleDropdown.value    = PlayerPrefs.GetInt("RenderScale", 2);
        textureQualityDropdown.value = PlayerPrefs.GetInt("TextureQuality", 0);
        ambientOcclusionToggle.isOn  = PlayerPrefs.GetInt("AmbientOcclusion", 1) == 1;
        bloomToggle.isOn             = PlayerPrefs.GetInt("Bloom", 1) == 1;

        // Apply to engine
        uint refreshRateNumerator = uint.Parse(refreshRateDropdown.options[refreshRateDropdown.value].text.Split(' ')[0]);
        refreshRateObj = new(){ numerator = refreshRateNumerator, denominator = 1};

        var res = resolutionDropdown.options[resolutionDropdown.value].text.Split('x');
        Screen.SetResolution(int.Parse(res[0]), int.Parse(res[1]), fullScreenMode, refreshRateObj);
        Cursor.lockState = fullscreenToggle.isOn ? CursorLockMode.Confined : CursorLockMode.None;
        StartCoroutine(ForceCanvasRebuildNextFrame());

        ApplyMsaa(antiAliasingDropdown.value);
        QualitySettings.SetQualityLevel(graphicsQualityDropdown.value);
        ApplyVSyncAndFpsLimit(true, 0);

        ApplyShadowQuality(shadowQualityDropdown.value);
        ApplyAmbientOcclusion(ambientOcclusionToggle.isOn);
        ApplyRenderScale(renderScaleDropdown.value);
        ApplyTextureQuality(textureQualityDropdown.value);
        ApplyBloom(bloomToggle.isOn);

        resolutionDropdown.RefreshShownValue();
        antiAliasingDropdown.RefreshShownValue();
        graphicsQualityDropdown.RefreshShownValue();
        refreshRateDropdown.RefreshShownValue();
        shadowQualityDropdown.RefreshShownValue();
        renderScaleDropdown.RefreshShownValue();
        textureQualityDropdown.RefreshShownValue();
        fpsLimitDropdown.RefreshShownValue();

        SaveSettings();
    }

    private IEnumerator ForceCanvasRebuildNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt("VSync", vsyncToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("MSAA", antiAliasingDropdown.value);
        PlayerPrefs.SetInt("Resolution", resolutionDropdown.value);
        PlayerPrefs.SetInt("RefreshRate", refreshRateDropdown.value);
        PlayerPrefs.SetInt("GraphicsQuality", graphicsQualityDropdown.value);
        PlayerPrefs.SetInt("DisplayFPS", fpsToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("ShadowQuality", shadowQualityDropdown.value);
        PlayerPrefs.SetInt("AmbientOcclusion", ambientOcclusionToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("RenderScale", renderScaleDropdown.value);
        PlayerPrefs.SetInt("TextureQuality", textureQualityDropdown.value);
        PlayerPrefs.SetInt("Bloom", bloomToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("FPSLimit", fpsLimitDropdown.value);
        PlayerPrefs.Save();
    }

    // Set after SetQualityLevel: a quality level carries its own vSyncCount. VSync on overrides the cap.
    private void ApplyVSyncAndFpsLimit(bool vsyncOn, int fpsLimitIndex)
    {
        QualitySettings.vSyncCount = vsyncOn ? 1 : 0;
        Application.targetFrameRate = FpsLimitValues[fpsLimitIndex];
    }

    public void SetGraphicsPreset()
    {
        int preset = PlayerPrefs.GetInt("GraphicsPreset", 0);
        if (preset != 0)
            QualitySettings.SetQualityLevel(preset);
    }

    public void ToggleFPSCounter(bool _enable)
    {
        fpsObject.SetActive(_enable);
    }

    private void ApplyShadowQuality(int index)
    {
        var urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null) return;
        urpAsset.shadowDistance = ShadowDistances[index];
        urpAsset.shadowCascadeCount = ShadowCascades[index];
        urpAsset.mainLightShadowmapResolution = ShadowResolutions[index];
    }

    private void ApplyAmbientOcclusion(bool isOn)
    {
        if (rendererData == null) return;
        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature is ScreenSpaceAmbientOcclusion)
            {
                feature.SetActive(isOn);
                break;
            }
        }
    }

    private void ApplyRenderScale(int index)
    {
        var urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null) return;
        urpAsset.renderScale = RenderScaleValues[index];
    }

    private void ApplyTextureQuality(int index)
    {
        QualitySettings.globalTextureMipmapLimit = index;
    }

    private void ApplyBloom(bool isOn)
    {
        if (bloomProfiles == null) return;
        foreach (var profile in bloomProfiles)
        {
            if (profile != null && profile.TryGet<Bloom>(out var bloom))
                bloom.active = isOn;
        }
    }

    // URP reads MSAA from its asset, not QualitySettings.antiAliasing; dropdown indices 0/1/2/3 are 1/2/4/8 samples.
    private void ApplyMsaa(int index)
    {
        var urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null) return;
        urpAsset.msaaSampleCount = index == 0 ? 1 : 1 << index;
    }
}
}
