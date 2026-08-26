using System;
using Memori.Tooltip;
using TJ.Spells;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Memori.Localization;

namespace TJ
{
    public class SpellQuickCastSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image spellSprite;
        [SerializeField] private MemoriTooltipTrigger tooltipTrigger;
        private Action onPointerEnter, onPointerExit;

        public void SetUp(SpellData spellData, Action _onPointerEnter, Action _onPointerExit)
        {
            // An empty slot (hotbar longer than the loadout) shows no icon and cannot be queued.
            if(spellData == null)
            {
                spellSprite.enabled = false;
                onPointerEnter = null;
                onPointerExit = null;
                tooltipTrigger.SetUpToolTip();
                return;
            }

            spellSprite.enabled = true;
            spellSprite.sprite = spellData.SpellSprite;
            // Same faction colour the picker and the hotbar use, so a spell is recognisable here too.
            spellSprite.color = ColorData.GetRaceDisplayColor(spellData.Race);
            onPointerEnter = _onPointerEnter;
            onPointerExit = _onPointerExit;

            string localizedSpellName = LocalizationManager.Instance.GetText(spellData.Spell.ToString());
            string localizedSpellDescription = spellData.GetLocalizedSpellDescription(); 

            tooltipTrigger.SetUpToolTip(localizedSpellName, localizedSpellDescription, _delay:0.5f);
        }

        public void OnPointerEnter(PointerEventData eventData) => onPointerEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => onPointerExit?.Invoke();
    }
}
