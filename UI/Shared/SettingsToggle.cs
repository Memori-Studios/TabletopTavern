using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Memori.UI;

namespace TJ.Settings
{
public class SettingsToggle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    [SerializeField] private string settingName;
    [SerializeField] private Toggle toggle;
    [SerializeField] private TMP_Text lableText;
    [SerializeField] private Button button;
    // Off for the graphics toggles: GraphicsPanel persists those on Apply, not on click.
    [SerializeField] private bool saveOnClick = true;
    private void Awake()
    {
        toggle.isOn = PlayerPrefs.GetInt(settingName, 0) == 1;
        button.onClick.AddListener(UpdateSetting);
    }
    public void UpdateSetting()
    {
        toggle.isOn = !toggle.isOn;
        if (saveOnClick)
            PlayerPrefs.SetInt(settingName, toggle.isOn ? 1 : 0);
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        MemoriUI.BloomFontSize(lableText, 18f, 0.1f);
        MemoriUI.HighlightText(lableText);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        MemoriUI.BloomFontSize(lableText, 16f, 0.1f);
        MemoriUI.UnHighlightText(lableText);       
    }
    public void OverrideToggleFromSettings()
    {
        toggle.isOn = PlayerPrefs.GetInt(settingName, 1) == 1;
    }
}
}
