using Memori.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.Settings
{
    /// <summary>
    /// A dropdown that never opens a list: the arrows, a click, Submit and left/right step through the options.
    /// It stays a TMP_Dropdown so every caller that fills or reads a dropdown keeps working unchanged.
    /// </summary>
    public class SettingStepper : TMP_Dropdown
    {
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        private bool _drawn, _shownInteractable;
        private int _shownValue, _shownCount;

        protected override void Awake()
        {
            base.Awake();
            if (!Application.isPlaying) return;
            if (previousButton != null) previousButton.onClick.AddListener(() => Step(-1, false));
            if (nextButton != null) nextButton.onClick.AddListener(() => Step(1, false));
        }

        public void Step(int direction, bool wrap)
        {
            int count = options.Count;
            if (!IsInteractable() || count == 0) return;
            int next = value + direction;
            next = wrap ? (next % count + count) % count : Mathf.Clamp(next, 0, count - 1);
            if (next == value) return;
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            value = next;
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Step(1, true);
        }

        public override void OnSubmit(BaseEventData eventData) => Step(1, true);

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right)
            {
                Step(eventData.moveDir == MoveDirection.Left ? -1 : 1, false);
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }

        // Polled so SetValueWithoutNotify and a refill of the options also update the arrows.
        private void LateUpdate()
        {
            bool interactable = IsInteractable();
            int count = options.Count;
            if (_drawn && value == _shownValue && count == _shownCount && interactable == _shownInteractable) return;
            _drawn = true;
            _shownValue = value;
            _shownCount = count;
            _shownInteractable = interactable;
            if (previousButton != null) previousButton.interactable = interactable && value > 0;
            if (nextButton != null) nextButton.interactable = interactable && value < count - 1;
        }
    }
}
