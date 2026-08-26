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
    /// SUPERSEDED 2026-08-18 and no longer referenced by any code. The grimoire now builds
    /// <see cref="SpellBrowseSlot"/> tiles inside <see cref="SpellBrowseGroup"/> faction bands, the
    /// same components the in-battle picker uses, so a spell reads identically in both places.
    ///
    /// Kept only so the existing Grimoire Row prefab does not become a missing script mid-migration.
    /// Delete this file AND that prefab once the new grimoire tile prefab is wired on WarbandPanel.
    ///
    /// One thing that moved rather than died: BuildLockedDescription now lives on WarbandPanel, since
    /// a tile has no text of its own and the inspector is the only surface left that can show it.
    /// </summary>
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class SpellGrimoireRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button rowButton;
        [SerializeField] private Image spellIcon;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text spellNameText;
        [SerializeField] private GameObject equippedMarker;
        [SerializeField] private GameObject lockedBlocker;

        private MemoriTooltipTrigger tooltipTrigger;
        private SpellData spellData;
        private bool isUnlocked;
        private Action<SpellData> onPicked;
        private Action<SpellData> onHovered;

        public SpellData SpellData => spellData;
        public bool IsUnlocked => isUnlocked;

        public void SetUp(SpellData _spellData, bool _isUnlocked, Action<SpellData> _onPicked,
                          Action<SpellData> _onHovered)
        {
            if (tooltipTrigger == null) tooltipTrigger = GetComponent<MemoriTooltipTrigger>();

            spellData = _spellData;
            isUnlocked = _isUnlocked;
            onPicked = _onPicked;
            onHovered = _onHovered;

            spellIcon.sprite = spellData.SpellSprite;
            spellNameText.text = LocalizationManager.Instance.GetText(spellData.Spell.ToString());

            // A locked row keeps its name and art - it reads as something to earn rather than an
            // anonymous blank - but its description is replaced by how to earn it.
            lockedBlocker.SetActive(!isUnlocked);
            tooltipTrigger.SetUpToolTip(spellNameText.text,
                                        isUnlocked ? spellData.GetLocalizedSpellDescription()
                                                   : BuildLockedDescription());

            ApplyRaceTint();

            rowButton.onClick.RemoveAllListeners();
            rowButton.onClick.AddListener(() => onPicked?.Invoke(spellData));
            rowButton.interactable = isUnlocked;
        }

        /// <summary>"Complete a run as {hero} to unlock." Falls back to a heroless line if no hero
        /// claims this spell, which GetGrimoireSpells should already have filtered out.</summary>
        private string BuildLockedDescription()
        {
            if (!SpellLoadout.TryGetSignatureHero(spellData.Spell, out Hero owner))
            {
                return LocalizationManager.Instance.GetText("SpellLockedUnknownDesc");
            }

            return string.Format(LocalizationManager.Instance.GetText("SpellLockedDesc"),
                                 LocalizationManager.Instance.GetText(owner.HeroName));
        }

        // Shares ColorData's race-passive tint with SquadBattleInfo and SpellBrowseSlot so a
        // spell's colour is the same wherever it appears.
        private void ApplyRaceTint()
        {
            backgroundImage.color = ColorData.GetRacePassiveTint(spellData.Race, spellData.RaceData);
        }

        /// <summary>
        /// An already-equipped spell stays visible but is marked and non-interactable, so the list
        /// never reorders as the player swaps - same reasoning as SpellBrowseMenu's fixed rows.
        /// </summary>
        public void SetEquipped(bool isEquipped)
        {
            equippedMarker.SetActive(isEquipped);
            // isUnlocked is re-applied here, not just in SetUp - this runs on every pick, and
            // without it a locked row would become clickable the moment something else was equipped.
            rowButton.interactable = isUnlocked && !isEquipped;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            MemoriUI.BloomItemScale(transform, 1.02f, 0.1f);
            onHovered?.Invoke(spellData);
        }

        /// <summary>
        /// Resets the hover scale only. The inspector is deliberately left showing this spell -
        /// mousing off a row must not blank the description, so it persists until the pointer
        /// leaves the spell block and WarbandPanel.SetFocus restores the unit inspector.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            MemoriUI.BloomItemScale(transform, 1f, 0.1f);
        }
    }
}
