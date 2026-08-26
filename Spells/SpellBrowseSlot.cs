using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.Spells
{
    /// <summary>How a browse row reads. Drives the frame only - never the faction colour.</summary>
    public enum SpellBrowseState
    {
        /// <summary>Not equipped anywhere. Clickable.</summary>
        Available,
        /// <summary>Equipped in some other slot. Dimmed, shows which slot, not clickable.</summary>
        Equipped,
        /// <summary>Currently occupies the slot the menu was opened for. Bracketed, not clickable.</summary>
        InArmedSlot,
        /// <summary>Cannot be taken at all (unauthored asset, or locked). Hatched, not clickable.</summary>
        Unavailable,
    }

    /// <summary>
    /// One selectable spell in the pre-battle <see cref="SpellBrowseMenu"/>.
    ///
    /// The row is built in six layers and they split cleanly in two:
    ///
    ///   INTERIOR is chromatic and answers "what is this spell" - the wash, the rail and the icon all
    ///   carry the faction's display colour and never change with state.
    ///   FRAME is achromatic and answers "what is its status" - border brightness plus the slot numeral
    ///   and corner brackets.
    ///
    /// That split is not a style preference. Nine factions consume the entire hue wheel, so no hue is
    /// left to mean "equipped" - green is Gruntkin, gold is Taelindor. State has to read on brightness
    /// and geometry. This is why <see cref="spellIcon"/>.color is now faction-only: writing state into it
    /// (as SetEquipped and SetSelected used to) is exactly the collision the system exists to remove.
    ///
    /// Hovering reports the spell up to the menu, which renders its name and description in one fixed
    /// info panel. Used by BOTH the in-battle picker (SpellBrowseMenu) and the run-setup grimoire
    /// (WarbandPanel), which is what keeps a spell reading identically in the two places.
    /// </summary>
    public class SpellBrowseSlot : MonoBehaviour, IPointerEnterHandler
    {
        [Header("Interior (faction)")]
        [SerializeField] private Image spellIcon;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image raceRailImage;

        [Header("Frame (state)")]
        [SerializeField] private Image frameImage;
        [SerializeField] private GameObject slotChip;
        [SerializeField] private TMP_Text slotChipText;
        [SerializeField] private GameObject targetBrackets;
        [SerializeField] private GameObject unavailableOverlay;

        [Header("Input")]
        [SerializeField] private Button selectButton;

        // Interior dimming for a row that is already spoken for. The faction colour is retained at
        // reduced strength rather than swapped out, so identity survives every state.
        private const float EQUIPPED_ICON_ALPHA = 0.34f;
        private const float UNAVAILABLE_ICON_ALPHA = 0.26f;
        private const float EQUIPPED_INTERIOR_SCALE = 0.47f;
        private const float RAIL_ALPHA = 0.9f;

        private SpellData spellData;
        private Action<SpellData> onHovered;
        private Color factionColor;
        public SpellData SpellData => spellData;

        public void SetUp(SpellData _spellData, Action onClicked, Action<SpellData> _onHovered)
        {
            spellData = _spellData;
            onHovered = _onHovered;
            factionColor = ColorData.GetRaceDisplayColor(_spellData.Race);

            spellIcon.sprite = _spellData.SpellSprite;

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onClicked?.Invoke());

            SetState(SpellBrowseState.Available);
        }

        /// <summary>
        /// Paints the three interior layers. The hue never varies - a spell is the same colour in the
        /// picker, in its slot and on the battle hotbar. Only its strength moves, so a row that is
        /// already spoken for recedes without losing its identity.
        /// </summary>
        private void ApplyFactionColour(float iconAlpha, float interiorScale)
        {
            spellIcon.color = ColorData.WithAlpha255(factionColor, iconAlpha * 255f);
            backgroundImage.color = ColorData.WithAlpha255(factionColor,
                ColorData.RACE_DISPLAY_WASH_ALPHA * interiorScale);
            raceRailImage.color = ColorData.WithAlpha255(factionColor,
                RAIL_ALPHA * 255f * interiorScale);
        }

        /// <summary>
        /// The single state entry point, replacing the old SetEquipped/SetSelected pair.
        /// </summary>
        /// <param name="state">Which of the four readings this row is in.</param>
        /// <param name="equippedSlotIndex">
        /// Zero-based slot holding this spell, surfaced as a 1-4 numeral. Only read for
        /// <see cref="SpellBrowseState.Equipped"/>; pass -1 otherwise.
        /// </param>
        public void SetState(SpellBrowseState state, int equippedSlotIndex = -1)
        {
            bool isEquipped = state == SpellBrowseState.Equipped;
            bool isUnavailable = state == SpellBrowseState.Unavailable;
            bool dimInterior = isEquipped || isUnavailable;

            selectButton.interactable = state == SpellBrowseState.Available;

            frameImage.color = state switch
            {
                SpellBrowseState.InArmedSlot => ColorData.SpellFrameActive,
                SpellBrowseState.Equipped    => ColorData.SpellFrameEquipped,
                _                            => ColorData.SpellFrameIdle,
            };

            float iconAlpha = isUnavailable ? UNAVAILABLE_ICON_ALPHA
                            : isEquipped    ? EQUIPPED_ICON_ALPHA
                                            : 1f;
            ApplyFactionColour(iconAlpha, dimInterior ? EQUIPPED_INTERIOR_SCALE : 1f);

            targetBrackets.SetActive(state == SpellBrowseState.InArmedSlot);
            unavailableOverlay.SetActive(isUnavailable);

            // Answers "equipped where?", which is the question actually being asked mid-swap. The row in
            // the armed slot is already bracketed, so it does not need the numeral as well.
            slotChip.SetActive(isEquipped && equippedSlotIndex >= 0);
            if(isEquipped && equippedSlotIndex >= 0)
                slotChipText.text = (equippedSlotIndex + 1).ToString();
        }

        /// <summary>
        /// Reports this spell to the menu's info panel. There is deliberately no exit counterpart -
        /// the panel keeps showing the last hovered spell until the menu closes, so the description
        /// stays readable while the pointer travels between rows instead of blinking out.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData) => onHovered?.Invoke(spellData);
    }
}
