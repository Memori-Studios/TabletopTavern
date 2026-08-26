using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TJ.MainMenu
{
    /// <summary>
    /// Put this on a warband loadout block (Army / Gear / Spells) to focus that section when the
    /// pointer enters it, so the source column follows the mouse instead of needing a click.
    ///
    /// There is deliberately no OnPointerExit: focus is "last hovered wins" and persists after the
    /// pointer leaves, because the player's next move is to cross into the source column and click
    /// something there. Clearing focus on exit would close the list they were reaching for.
    ///
    /// The block root needs a raycast-target Image covering it (a transparent one is fine) or the
    /// gaps between its slots will not register as hovered - the same requirement SpellBrowseMenu
    /// has.
    /// </summary>
    public class WarbandSectionHoverArea : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private WarbandSection section;

        private Action<WarbandSection> onHovered;

        public void SetUp(Action<WarbandSection> _onHovered)
        {
            onHovered = _onHovered;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            onHovered?.Invoke(section);
        }
    }
}
