using System;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Settings
{
    public class EdgePanningToggle : MonoBehaviour
    {
        public static event Action<bool> OnEdgePanningChanged;
        public const string PlayerPrefKey = "EdgePanning";

        [SerializeField] private Toggle onToggle;

        private void Awake()
        {
            int savedValue = PlayerPrefs.GetInt(PlayerPrefKey, 0);
            onToggle.isOn = savedValue == 1;
            onToggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        public void OnToggleValueChanged(bool isOn)
        {
            PlayerPrefs.SetInt(PlayerPrefKey, isOn ? 1 : 0);
            OnEdgePanningChanged?.Invoke(isOn);
        }
    }
}
