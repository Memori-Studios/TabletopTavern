using System;
using Memori.Localization;
using Memori.Tooltip;
using Memori.UI;
using TJ.Spells;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// One of the four spell slots in the warband loadout. Slot 0 holds the hero's signature spell
    /// and is pinned: it shows a pin and cannot be armed or swapped.
    ///
    /// Built from the same layers as <see cref="SpellBrowseSlot"/> and follows the same rule -
    /// INTERIOR is chromatic and carries the faction (wash, rail, icon), FRAME is achromatic and
    /// carries state. The two differ only in behaviour: a browse tile represents a SPELL you may take,
    /// this represents a SLOT you may arm.
    ///
    /// The frame used to be painted gold when focused. That is exactly the collision the system exists
    /// to remove - gold is Taelindor's hue - so focus now reads on <see cref="ColorData.SpellFrameActive"/>
    /// plus the highlight marks.
    ///
    /// A loadout is always exactly four spells. Slots 1-3 are replaced in place and never emptied,
    /// so emptyState only ever shows if a slot somehow resolves to no asset at all.
    /// </summary>
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class SpellLoadoutSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button slotButton;

        [Header("Interior (faction)")]
        [SerializeField] private Image spellIcon;
        [SerializeField] private Image raceGradientImage;
        [SerializeField] private Image raceRailImage;

        [Header("Frame (state)")]
        [SerializeField] private Image frameImage;
        [SerializeField] private GameObject selectedHighlight;

        [Header("Marks")]
        [SerializeField] private GameObject pinnedIcon;
        [SerializeField] private GameObject emptyState;
        // Shown for a slot the player has not unlocked yet. Optional, but without it a locked slot is
        // indistinguishable from an empty one, so wire it or locked slots read as a bug.
        [SerializeField] private GameObject lockedState;
        // Both optional. The browse tiles carry no text at all, so a slot that wants to match them
        // exactly can have these deleted from the prefab.
        [SerializeField] private TMP_Text spellNameText;
        [SerializeField] private TMP_Text slotNumberText;

        private const float RAIL_ALPHA = 0.9f;

        private MemoriTooltipTrigger tooltipTrigger;
        private SpellData spellData;
        private int slotIndex;
        private Action<int> onSlotClicked;
        private Action<SpellData> onHovered;
        private bool cachedFocused;
        private bool cachedHovered;

        public SpellData SpellData => spellData;
        public bool IsPinned => slotIndex == SpellLoadout.SignatureSlotIndex;
        public bool IsLocked => cachedLocked;

        private bool cachedLocked;

        public void LoadSlot(int _slotIndex, SpellData _spellData, Action<int> _onSlotClicked,
                             Action<SpellData> _onHovered, bool _isLocked = false)
        {
            if (tooltipTrigger == null) tooltipTrigger = GetComponent<MemoriTooltipTrigger>();

            slotIndex = _slotIndex;
            spellData = _spellData;
            onSlotClicked = _onSlotClicked;
            onHovered = _onHovered;
            cachedHovered = false;
            cachedLocked = _isLocked;

            pinnedIcon.SetActive(IsPinned && !cachedLocked);
            if (lockedState != null) lockedState.SetActive(cachedLocked);
            if (slotNumberText != null) slotNumberText.text = (slotIndex + 1).ToString();

            // A locked slot holds no spell by construction, but it must not read as merely empty -
            // empty invites a click, locked explains why the click does nothing.
            bool isEmpty = spellData == null;
            emptyState.SetActive(isEmpty && !cachedLocked);
            spellIcon.enabled = !isEmpty;
            ApplyFactionColour(isEmpty);

            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => onSlotClicked?.Invoke(slotIndex));
            // The signature slot still reads as a slot but cannot be focused for swapping.
            slotButton.interactable = !IsPinned && !cachedLocked;

            SetFocused(false);

            if (cachedLocked)
            {
                if (spellNameText != null) spellNameText.text = LocalizationManager.Instance.GetText("LockedSpellSlot");
                tooltipTrigger.SetUpToolTip(LocalizationManager.Instance.GetText("LockedSpellSlot"),
                                            LocalizationManager.Instance.GetText("LockedSpellSlotDesc"));
                return;
            }

            if (isEmpty)
            {
                if (spellNameText != null) spellNameText.text = LocalizationManager.Instance.GetText("EmptySpellSlot");
                tooltipTrigger.SetUpToolTip(LocalizationManager.Instance.GetText("EmptySpellSlot"),
                                            LocalizationManager.Instance.GetText("EmptySpellSlotDesc"));
                return;
            }

            spellIcon.sprite = spellData.SpellSprite;
            string localizedName = LocalizationManager.Instance.GetText(spellData.Spell.ToString());
            if (spellNameText != null) spellNameText.text = localizedName;
            tooltipTrigger.SetUpToolTip(localizedName, BuildTooltipDescription());
        }

        /// <summary>
        /// Paints the three interior layers from the display ramp, the same values the grimoire tile
        /// and the battle hotbar use, so a spell keeps one colour from the grimoire through its slot
        /// and into battle. Deliberately NOT GetRacePassiveTint: that is the large-fill pair, authored
        /// for banner backgrounds, and four of its nine colours are too dark to sit behind a glyph.
        ///
        /// Disabled rather than recoloured on an empty slot, or an emptied slot would keep the removed
        /// spell's colour.
        /// </summary>
        private void ApplyFactionColour(bool isEmpty)
        {
            if (raceGradientImage != null) raceGradientImage.enabled = !isEmpty;
            if (raceRailImage != null) raceRailImage.enabled = !isEmpty;
            if (isEmpty) return;

            Color factionColour = ColorData.GetRaceDisplayColor(spellData.Race);

            spellIcon.color = factionColour;
            if (raceGradientImage != null) raceGradientImage.color = ColorData.GetRaceDisplayTint(spellData.Race);
            if (raceRailImage != null) raceRailImage.color = ColorData.WithAlpha255(factionColour, RAIL_ALPHA * 255f);
        }

        private string BuildTooltipDescription()
        {
            string description = spellData.GetLocalizedSpellDescription();
            if (IsPinned)
            {
                description += $"\n\n<color={ColorData.Tier4}>{LocalizationManager.Instance.GetText("SignatureSpellPinned")}</color>";
            }
            return description;
        }

        public void SetFocused(bool isFocused)
        {
            cachedFocused = isFocused;
            selectedHighlight.SetActive(isFocused);
            RefreshFrame();
        }

        /// <summary>
        /// The whole state channel, achromatic by design. A pinned or locked slot never shows the
        /// hover frame - neither can be armed, so offering the affordance would be a lie.
        /// </summary>
        private void RefreshFrame()
        {
            bool canArm = !IsPinned && !cachedLocked;
            frameImage.color = cachedFocused          ? ColorData.SpellFrameActive
                             : cachedHovered && canArm ? ColorData.SpellFrameHover
                                                       : ColorData.SpellFrameRest;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            cachedHovered = true;
            RefreshFrame();

            MemoriUI.BloomItemScale(transform, 1.05f, 0.1f);
            onHovered?.Invoke(spellData);
        }

        /// <summary>
        /// Resets the hover scale and frame only. The inspector keeps showing this slot's spell until
        /// the pointer leaves the spell block entirely - see WarbandPanel.SetFocus.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            cachedHovered = false;
            RefreshFrame();

            MemoriUI.BloomItemScale(transform, 1f, 0.1f);
        }
    }
}
