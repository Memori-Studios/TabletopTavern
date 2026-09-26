using Memori.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.Settings
{
    /// <summary>Tells the SettingRow above this control when the control gains or loses selection.</summary>
    public class SettingFocusRelay : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField, Tooltip("Click when pressed. On for controls with no click of their own (key cap, dropdown); off where the control clicks itself.")]
        private bool clickSound;

        private SettingRow _row;
        private Selectable _control;

        private void Awake()
        {
            _row = GetComponentInParent<SettingRow>(true);
            _control = GetComponent<Selectable>();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (_row != null) _row.SetFocused(true, eventData);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (_row != null) _row.SetFocused(false, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Click();
        }

        public void OnSubmit(BaseEventData eventData) => Click();

        private void Click()
        {
            if (!clickSound || _control == null || !_control.IsInteractable()) return;
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
        }
    }
}
