using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TJ.Settings
{
    /// <summary>One line on a Settings page: label, help line and control. It lights up under the pointer or keyboard focus.</summary>
    public class SettingRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image hoverFill;
        [SerializeField] private Image focusBar;
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup content;
        [SerializeField] private Color labelColour = new(0.93f, 0.9f, 0.85f, 1f);
        [SerializeField] private Color litLabelColour = Color.white;
        [SerializeField, Range(0f, 1f)] private float unavailableAlpha = 0.45f;

        private bool _hovered, _focused;

        private void OnEnable() => Refresh();

        private void OnDisable()
        {
            _hovered = false;
            _focused = false;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh();
        }

        /// <summary>Called by the row's control. A selection made with the mouse does not count as focus.</summary>
        public void SetFocused(bool focused, BaseEventData eventData)
        {
            _focused = focused && eventData is not PointerEventData && !PointerUsedLast();
            Refresh();
        }

        /// <summary>Greys the row out while the setting cannot change here. The caller disables the control.</summary>
        public void SetAvailable(bool available)
        {
            if (content != null) content.alpha = available ? 1f : unavailableAlpha;
        }

        // Same rule as ButtonFocusVisual: the focus mark is for keyboard and controller players.
        private static bool PointerUsedLast()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return false;
            if (keyboard == null) return true;
            return mouse.lastUpdateTime > keyboard.lastUpdateTime;
        }

        private void Refresh()
        {
            bool lit = _hovered || _focused;
            if (hoverFill != null) hoverFill.enabled = lit;
            if (focusBar != null) focusBar.enabled = _focused;
            if (label != null) label.color = lit ? litLabelColour : labelColour;
        }
    }
}
