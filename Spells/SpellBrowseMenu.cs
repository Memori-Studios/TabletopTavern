using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TJ.Spells
{
    /// <summary>
    /// Pre-battle spell picker. Opened by hovering a <see cref="SpellCastButton"/> (custom battle,
    /// Deployment phase only). Lists every spell in the serialized pool that is not already equipped
    /// in one of the four slots; clicking a row asks the <see cref="SpellManager"/> to swap that spell
    /// into the slot the menu was opened from.
    ///
    /// The root object should carry a raycast-target background Image so the whole panel (not just the
    /// rows) counts as "hovered" - this drives the open/close retention handled by SpellManager.
    /// </summary>
    public class SpellBrowseMenu : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private SpellBrowseSlot rowPrefab;
        [SerializeField] private Transform contentParent;

        private SpellData[] pool;
        private Action<int, SpellData> onSpellPicked;
        private Action onHoverEnter, onHoverExit;

        private readonly List<SpellBrowseSlot> rows = new();
        private int targetSlotIndex = -1;

        /// <summary>
        /// Wires the fixed data once (pool + callbacks) and hides the menu. Called from
        /// SpellManager.LoadSpellManager after the loadout is known.
        /// </summary>
        public void Initialize(SpellData[] _pool, Action<int, SpellData> _onSpellPicked, Action _onHoverEnter, Action _onHoverExit)
        {
            pool = _pool;
            onSpellPicked = _onSpellPicked;
            onHoverEnter = _onHoverEnter;
            onHoverExit = _onHoverExit;
            Close();
        }

        public void Open(int _targetSlotIndex, SpellData[] equippedSpells)
        {
            targetSlotIndex = _targetSlotIndex;
            BuildRowsIfNeeded();
            RefreshEquippedState(equippedSpells);
            gameObject.SetActive(true);
        }

        public void Close()
        {
            targetSlotIndex = -1;
            if(gameObject.activeSelf) gameObject.SetActive(false);
        }

        // Rows are built once from the full pool, in a fixed order, and never rebuilt - so swapping a
        // spell never reorders the list. Equipped spells are dimmed in place instead of removed.
        private void BuildRowsIfNeeded()
        {
            if(rows.Count > 0 || pool == null) return;

            foreach(SpellData spell in pool)
            {
                if(spell == null) continue;

                SpellBrowseSlot row = Instantiate(rowPrefab, contentParent);
                SpellData capturedSpell = spell;
                row.SetUp(capturedSpell, () => Pick(capturedSpell));
                rows.Add(row);
            }
        }

        private void RefreshEquippedState(SpellData[] equippedSpells)
        {
            for(int i = 0; i < rows.Count; i++)
                rows[i].SetEquipped(IsEquipped(rows[i].SpellData, equippedSpells));
        }

        private bool IsEquipped(SpellData spell, SpellData[] equippedSpells)
        {
            if(equippedSpells == null) return false;
            for(int i = 0; i < equippedSpells.Length; i++)
            {
                if(equippedSpells[i] == spell) return true;
            }
            return false;
        }

        private void Pick(SpellData spell)
        {
            if(targetSlotIndex < 0) return;
            onSpellPicked?.Invoke(targetSlotIndex, spell);
        }

        public void OnPointerEnter(PointerEventData eventData) => onHoverEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => onHoverExit?.Invoke();
    }
}
