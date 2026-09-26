using System;
using System.Collections;
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
        /// <summary>Equipped in some other slot. Dimmed, shows which slot. Clicking it is rejected with a flash.</summary>
        Equipped,
        /// <summary>Currently occupies the slot the menu was opened for. Bracketed. Clicking it is rejected with a flash.</summary>
        InArmedSlot,
        /// <summary>Cannot be taken at all (unauthored asset, or locked). Hatched, not clickable.</summary>
        Unavailable,
    }

    /// <summary>
    /// One selectable spell in the pre-battle <see cref="SpellBrowseMenu"/>, styled like the battle
    /// hotbar tile (<see cref="SpellCastButton"/>): a dark rounded square whose icon and resting border
    /// carry the faction's display colour.
    ///
    /// State reads on brightness and geometry, never a new hue. Nine factions consume the entire hue
    /// wheel, so no hue is left to mean "equipped" - green is Gruntkin, gold is Taelindor. Hover lightens
    /// the border, the spell in the slot being swapped gets a white border and a faction-coloured glow,
    /// and a spell equipped elsewhere dims and shows its slot numeral.
    ///
    /// Hovering reports the spell up to the menu, which renders its name and description in one fixed
    /// info panel. Used by BOTH the in-battle picker (SpellBrowseMenu) and the run-setup grimoire
    /// (WarbandPanel), which is what keeps a spell reading identically in the two places.
    /// </summary>
    public class SpellBrowseSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Icon")]
        [SerializeField] private Image spellIcon;

        [Header("Frame (state)")]
        // The tile border: faction colour at rest, brightness for state.
        [SerializeField] private Image frameImage;
        // Optional. Lit behind the tile for the spell in the slot being swapped.
        [SerializeField] private Image selectedGlow;
        [SerializeField] private GameObject slotChip;
        [SerializeField] private TMP_Text slotChipText;
        [SerializeField] private GameObject unavailableOverlay;

        [Header("Input")]
        [SerializeField] private Button selectButton;

        [Header("Feedback")]
        // Transient pulse only, not a state colour: the interior goes back to the faction colour the
        // moment the flash ends, so this does not break the "state never lives in the icon" rule.
        [SerializeField] private Color rejectFlashColor = new(0.92f, 0.2f, 0.2f);
        private const float REJECT_FLASH_DURATION = 0.45f;
        private const float REJECT_FLASH_MAX_STEP = 0.05f;

        // Dimming for a row that is already spoken for. The faction colour is retained at reduced
        // strength rather than swapped out, so identity survives every state.
        private const float EQUIPPED_ICON_ALPHA = 0.34f;
        private const float UNAVAILABLE_ICON_ALPHA = 0.26f;
        private const float EQUIPPED_BORDER_ALPHA = 0.35f;

        private SpellData spellData;
        private Action onClicked;
        private Action onAlreadyEquipped;
        private Action<SpellData> onHovered;
        private Color factionColor;
        private SpellBrowseState state;
        private int equippedSlotIndex = -1;
        private bool hovered;
        private Coroutine rejectFlash;
        public SpellData SpellData => spellData;

        /// <param name="_onAlreadyEquipped">
        /// Raised when the player clicks a row that is already equipped (in any slot). The row flashes
        /// itself; this is for the caller to add a notification on top.
        /// </param>
        public void SetUp(SpellData _spellData, Action _onClicked, Action<SpellData> _onHovered, Action _onAlreadyEquipped = null)
        {
            spellData = _spellData;
            onClicked = _onClicked;
            onHovered = _onHovered;
            onAlreadyEquipped = _onAlreadyEquipped;
            factionColor = ColorData.GetRaceDisplayColor(_spellData.Race);

            spellIcon.sprite = _spellData.SpellSprite;

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnClicked);

            SetState(SpellBrowseState.Available);
        }

        private void OnClicked()
        {
            switch(state)
            {
                case SpellBrowseState.Available:
                    onClicked?.Invoke();
                    break;
                case SpellBrowseState.Equipped:
                case SpellBrowseState.InArmedSlot:
                    // Rejected rather than ignored: a dead click reads as a broken button, so the row
                    // says why. The button stays interactable in these states for exactly this reason.
                    FlashRejected();
                    onAlreadyEquipped?.Invoke();
                    break;
            }
        }

        // The hue never varies - a spell is the same colour in the picker, in its slot and on the battle
        // hotbar. Only brightness and strength move.
        private void RefreshFrame()
        {
            bool armed = state == SpellBrowseState.InArmedSlot;
            frameImage.color = armed                                           ? ColorData.SpellFrameActive
                             : state == SpellBrowseState.Unavailable           ? ColorData.SpellFrameRest
                             : hovered && state == SpellBrowseState.Available ? ColorData.SpellFrameHover
                             : state == SpellBrowseState.Equipped              ? ColorData.WithAlpha255(factionColor, EQUIPPED_BORDER_ALPHA * 255f)
                                                                               : factionColor;
            if(selectedGlow != null)
            {
                selectedGlow.enabled = armed;
                selectedGlow.color = factionColor;
            }
        }

        /// <summary>
        /// The single state entry point, replacing the old SetEquipped/SetSelected pair.
        /// </summary>
        /// <param name="state">Which of the four readings this row is in.</param>
        /// <param name="equippedSlotIndex">
        /// Zero-based slot holding this spell, surfaced as a 1-4 numeral. Only read for
        /// <see cref="SpellBrowseState.Equipped"/>; pass -1 otherwise.
        /// </param>
        public void SetState(SpellBrowseState _state, int _equippedSlotIndex = -1)
        {
            state = _state;
            equippedSlotIndex = _equippedSlotIndex;
            StopRejectFlash();

            bool isEquipped = state == SpellBrowseState.Equipped;
            bool isUnavailable = state == SpellBrowseState.Unavailable;

            // Equipped rows stay clickable so the click can be rejected with feedback (see OnClicked).
            selectButton.interactable = state != SpellBrowseState.Unavailable;

            RefreshFrame();

            float iconAlpha = isUnavailable ? UNAVAILABLE_ICON_ALPHA
                            : isEquipped    ? EQUIPPED_ICON_ALPHA
                                            : 1f;
            spellIcon.color = ColorData.WithAlpha255(factionColor, iconAlpha * 255f);

            unavailableOverlay.SetActive(isUnavailable);

            // Answers "equipped where?", which is the question actually being asked mid-swap. The row in
            // the armed slot is already bracketed, so it does not need the numeral as well.
            slotChip.SetActive(isEquipped && equippedSlotIndex >= 0);
            if(isEquipped && equippedSlotIndex >= 0)
                slotChipText.text = (equippedSlotIndex + 1).ToString();
        }

        #region Reject flash
        /// <summary>
        /// Pulses the icon and frame red and eases them back to their state colours. Restarting
        /// mid-flash first restores the resting colours so the ease never targets a half-red capture.
        /// </summary>
        public void FlashRejected()
        {
            if(!isActiveAndEnabled) return;
            StopRejectFlash();
            rejectFlash = StartCoroutine(RejectFlashRoutine());
        }

        private void StopRejectFlash()
        {
            if(rejectFlash == null) return;
            StopCoroutine(rejectFlash);
            rejectFlash = null;
        }

        private IEnumerator RejectFlashRoutine()
        {
            Color restingIcon = spellIcon.color;
            Color restingFrame = frameImage.color;
            bool frameWasActive = frameImage.gameObject.activeSelf;
            frameImage.gameObject.SetActive(true);

            float elapsed = 0f;
            while(elapsed < REJECT_FLASH_DURATION)
            {
                // Unscaled: the picker is open during Deployment, but a paused Settings panel must
                // not freeze the pulse mid-red. Clamped so one hitch frame cannot swallow the whole
                // pulse - the flash is the feedback, so it has to be seen.
                elapsed += Mathf.Min(Time.unscaledDeltaTime, REJECT_FLASH_MAX_STEP);
                float t = Mathf.Clamp01(elapsed / REJECT_FLASH_DURATION);
                float red = 1f - t * t;
                spellIcon.color = Color.Lerp(restingIcon, rejectFlashColor, red);
                frameImage.color = Color.Lerp(restingFrame, rejectFlashColor, red);
                yield return null;
            }

            rejectFlash = null;
            frameImage.gameObject.SetActive(frameWasActive);
            // Re-derive rather than restore the captures, so a SetState that landed mid-flash wins.
            SetState(state, equippedSlotIndex);
        }
        #endregion

        /// <summary>
        /// Reports this spell to the menu's info panel. There is deliberately no exit counterpart -
        /// the panel keeps showing the last hovered spell until the menu closes, so the description
        /// stays readable while the pointer travels between rows instead of blinking out.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if(rejectFlash == null) RefreshFrame();
            onHovered?.Invoke(spellData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if(rejectFlash == null) RefreshFrame();
        }
    }
}
