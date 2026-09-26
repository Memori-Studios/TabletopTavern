using System.Collections.Generic;
using UnityEngine;

namespace TJ.Spells
{
    // Spell ordinal -> the SpellData whose sprite the health bar shows. Filled by ActiveSpell at cast
    // rather than from SpellRegistry so mage-unit spells (deliberately absent from the registry) resolve.
    public static class SpellStatusIcons
    {
        private static readonly Dictionary<int, SpellData> _bySpellId = new();

        public static void Register(SpellData spellData)
        {
            _bySpellId[(int)spellData.Spell] = spellData;
        }

        // Same colour as the spell's cast button (SpellCastButton.RefreshIconAlpha), so the bar icon reads as that spell.
        public static bool TryGet(int spellId, out Sprite sprite, out Color color)
        {
            if (_bySpellId.TryGetValue(spellId, out SpellData spellData) && spellData.SpellSprite != null)
            {
                sprite = spellData.SpellSprite;
                color = TJ.ColorData.GetRaceDisplayColor(spellData.Race);
                return true;
            }
            sprite = null;
            color = Color.white;
            return false;
        }

        // The unit card's spell badges need the name and description too, not just the sprite.
        public static bool TryGetData(int spellId, out SpellData spellData) => _bySpellId.TryGetValue(spellId, out spellData);
    }
}
