using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TJ.MainMenu
{
    /// <summary>
    /// Sits on a warband loadout block (Army / Gear / Spells): a left-click on the block's open space focuses
    /// that section, and hovering the block reports in and out so it can show a faint highlight. Cards and slots
    /// with their own click handler keep their clicks. Needs a raycast-target Image covering the block, or
    /// clicks between its slots are missed.
    /// </summary>
    public class WarbandSectionHoverArea : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private WarbandSection section;

        private Action<WarbandSection> onClicked;
        private Action<WarbandSection, bool> onHovered;

        public void SetUp(Action<WarbandSection> _onClicked, Action<WarbandSection, bool> _onHovered)
        {
            onClicked = _onClicked;
            onHovered = _onHovered;
        }

        // Moving onto a slot inside the block sends no exit here: Core's input module sends hover to parents.
        public void OnPointerEnter(PointerEventData eventData) => onHovered?.Invoke(section, true);

        public void OnPointerExit(PointerEventData eventData) => onHovered?.Invoke(section, false);

        public void OnPointerClick(PointerEventData eventData)
        {
            // Right-click is the menu's back gesture (MainMenu.OnSecondaryActionPressed).
            if (eventData.button != PointerEventData.InputButton.Left) return;
            onClicked?.Invoke(section);
        }
    }
}
