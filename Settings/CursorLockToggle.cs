using UnityEngine;
using UnityEngine.UI;

namespace TJ.Settings
{
    public class CursorLockToggle : MonoBehaviour
    {
        public const string PlayerPrefKey = "LockCursorToWindow";

        [SerializeField] private Toggle onToggle;

        private void Awake()
        {
            onToggle.isOn = IsOn;
            onToggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        public void OnToggleValueChanged(bool isOn)
        {
            PlayerPrefs.SetInt(PlayerPrefKey, isOn ? 1 : 0);
            Apply();
        }

        // Defaults to On so fullscreen keeps the confinement it always had.
        public static bool IsOn => PlayerPrefs.GetInt(PlayerPrefKey, 1) == 1;

        public static void Apply()
        {
            Cursor.lockState = IsOn ? CursorLockMode.Confined : CursorLockMode.None;
        }
    }
}
