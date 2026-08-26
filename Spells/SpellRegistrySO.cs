using UnityEngine;

namespace TJ.Spells
{
    /// <summary>
    /// Authored index of every <see cref="SpellData"/> asset in the game.
    ///
    /// The spell assets live in Assets/Data/SOs/Spells, outside a Resources folder, so unlike
    /// HeroData / RaceData / SquadData they cannot be reached by Resources.LoadAll. This single
    /// asset does live in Resources and holds references to all of them, which is what
    /// <see cref="SpellRegistry"/> loads.
    ///
    /// The list order is also the display order of the pre-run grimoire, so keep it authored
    /// deliberately (lesser spells first, then hero signatures) rather than alphabetically.
    /// </summary>
    [CreateAssetMenu(fileName = "SpellRegistry", menuName = "GameData/SpellRegistry", order = 2)]
    public class SpellRegistrySO : ScriptableObject
    {
        public SpellData[] AllSpells;
    }
}
