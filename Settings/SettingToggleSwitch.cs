using Memori.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.Settings
{
    /// <summary>An Off | On switch drawn over a Toggle. Each half sets its own value and the chosen half is filled.</summary>
    [RequireComponent(typeof(Toggle))]
    public class SettingToggleSwitch : MonoBehaviour, ISubmitHandler
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private Button offHalf;
        [SerializeField] private Button onHalf;
        [SerializeField] private Graphic offFill;
        [SerializeField] private Graphic onFill;
        [SerializeField] private TMP_Text offLabel;
        [SerializeField] private TMP_Text onLabel;
        [SerializeField] private Color chosenText = new(0.93f, 0.94f, 0.95f, 1f);
        [SerializeField] private Color otherText = new(0.44f, 0.5f, 0.53f, 1f);

        private bool _drawn, _shownOn;

        private void Awake()
        {
            offHalf.onClick.AddListener(() => Set(false));
            onHalf.onClick.AddListener(() => Set(true));
        }

        // The click is tied to the press, not to the value, so loading and Reset to Default stay silent.
        private void Set(bool isOn)
        {
            if (!toggle.IsInteractable()) return;
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            toggle.isOn = isOn;
        }

        // Keyboard and controller Submit flip the Toggle on this object; this adds the same click.
        public void OnSubmit(BaseEventData eventData)
        {
            if (toggle.IsInteractable()) IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
        }

        // Polled, not subscribed, so SetIsOnWithoutNotify also redraws the switch.
        private void LateUpdate()
        {
            if (_drawn && toggle.isOn == _shownOn) return;
            _drawn = true;
            _shownOn = toggle.isOn;
            offFill.enabled = !_shownOn;
            onFill.enabled = _shownOn;
            offLabel.color = _shownOn ? otherText : chosenText;
            onLabel.color = _shownOn ? chosenText : otherText;
        }
    }
}
