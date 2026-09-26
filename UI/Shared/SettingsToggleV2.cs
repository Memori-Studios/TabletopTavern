using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Memori.Localization;

namespace TJ.Settings
{
    public class SettingsToggleV2 : MonoBehaviour
    {
        [SerializeField] private string playerPrefValue;
        // Optional: the Off | On switch shows both words itself.
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Toggle onToggle;
        [SerializeField] private bool defaultOn = false;
        // The pref stores the opposite of what the toggle shows (a "hide X" pref behind an "X" toggle).
        [SerializeField] private bool invertPref = false;
        private string onText, offText;
        private bool loaded;

        public Toggle OnToggle => onToggle;

        private void Awake() => Load();

        /// <summary>Puts the saved value on the toggle. Settings pages stay switched off until opened, so SettingsManager calls this at boot.</summary>
        public void Load()
        {
            if (loaded) return;
            loaded = true;
            if (statusText != null)
            {
                onText = LocalizationManager.Instance.GetText("settingsOn");
                offText = LocalizationManager.Instance.GetText("settingsOff");
            }

            int defaultValue = (defaultOn != invertPref) ? 1 : 0;
            bool isOn = (PlayerPrefs.GetInt(playerPrefValue, defaultValue) == 1) != invertPref;
            if (statusText != null) statusText.text = isOn ? onText : offText;
            onToggle.isOn = isOn;
            onToggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
        public void OnToggleValueChanged(bool isOn)
        {
            PlayerPrefs.SetInt(playerPrefValue, (isOn != invertPref) ? 1 : 0);
            if (statusText != null) statusText.text = isOn ? onText : offText;
        }
    }
}
