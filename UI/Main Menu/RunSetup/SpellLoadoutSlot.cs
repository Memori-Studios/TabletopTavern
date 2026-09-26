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
    /// One of the three spell slots in the warband loadout. Slot 0 holds the hero's signature spell
    /// and is pinned: it shows a pin and cannot be armed or swapped.
    ///
    /// Drawn like the battle hotbar tile (<see cref="SpellCastButton"/>), so a spell looks the same in
    /// run setup and in battle: the border rests in the faction colour, hover lightens it, and the armed
    /// slot gets a white border with a faction-coloured glow. The cost gem shows what it spends.
    ///
    /// Slots 1-2 are replaced in place and never emptied, so emptyState only ever shows if a slot
    /// somehow resolves to no asset at all.
    /// </summary>
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class SpellLoadoutSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button slotButton;

        [Header("Interior (faction)")]
        [SerializeField] private Image spellIcon;

        [Header("Frame (state)")]
        [SerializeField] private Image frameImage;
        // Lit behind the tile while this slot is armed; tinted in the spell's faction colour.
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
        // Optional. Hidden for an empty or locked slot and for a spell that costs no mana.
        [SerializeField] private GameObject manaCostGem;
        [SerializeField] private TMP_Text manaCostText;

        private const float LOCKED_NAME_ALPHA = 0.5f;

        private MemoriTooltipTrigger tooltipTrigger;
        private SpellData spellData;
        private int slotIndex;
        private Action<int> onSlotClicked;
        private Action<SpellData> onHovered;
        private Color factionColour;
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
            ApplyManaCost(isEmpty);

            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => onSlotClicked?.Invoke(slotIndex));
            // The signature slot still reads as a slot but cannot be focused for swapping.
            slotButton.interactable = !IsPinned && !cachedLocked;

            SetFocused(false);

            if (spellNameText != null) spellNameText.alpha = cachedLocked ? LOCKED_NAME_ALPHA : 1f;

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
            tooltipTrigger.SetContentProvider(() => SpellTooltip.Build(spellData, new SpellTooltip.Context { Pinned = IsPinned }));
        }

        /// <summary>
        /// Uses the display ramp, the same colour the grimoire tile and the battle hotbar use, so a spell
        /// keeps one colour from the grimoire through its slot and into battle.
        /// </summary>
        private void ApplyFactionColour(bool isEmpty)
        {
            if (isEmpty) return;

            factionColour = ColorData.GetRaceDisplayColor(spellData.Race);
            spellIcon.color = factionColour;

            Image glow = selectedHighlight.GetComponent<Image>();
            if (glow != null) glow.color = factionColour;
        }

        private void ApplyManaCost(bool isEmpty)
        {
            if (manaCostGem == null) return;

            bool showCost = !isEmpty && !cachedLocked && spellData.SpellManaCost > 0;
            manaCostGem.SetActive(showCost);
            if (showCost && manaCostText != null) manaCostText.text = spellData.SpellManaCost.ToString();
        }

        public void SetFocused(bool isFocused)
        {
            cachedFocused = isFocused;
            selectedHighlight.SetActive(isFocused);
            RefreshFrame();
        }

        /// <summary>
        /// A pinned or locked slot never shows the hover frame - neither can be armed, so offering the
        /// affordance would be a lie.
        /// </summary>
        private void RefreshFrame()
        {
            bool canArm = !IsPinned && !cachedLocked;
            bool holdsSpell = spellData != null && !cachedLocked;
            frameImage.color = cachedFocused          ? ColorData.SpellFrameActive
                             : cachedHovered && canArm ? ColorData.SpellFrameHover
                             : holdsSpell              ? factionColour
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
