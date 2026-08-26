using System.Collections.Generic;
using UnityEngine;

namespace TJ.Spells
{
    /// <summary>
    /// Resolves a <see cref="Spell"/> enum value to its authored <see cref="SpellData"/> asset.
    /// Loaded once from the <see cref="SpellRegistrySO"/> in Resources and cached, mirroring how
    /// HeroData / RaceData / SquadData cache their Resources lookups.
    ///
    /// Only the enum value is ever persisted in a save, so this is the single place that turns
    /// saved data back into assets.
    /// </summary>
    public static class SpellRegistry
    {
        public const string ResourcePath = "SpellData/SpellRegistry";

        private static readonly Dictionary<Spell, SpellData> _bySpell = new();
        private static readonly List<SpellData> _inAuthoredOrder = new();
        private static bool _loaded;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            Load();
        }

        public static void Load()
        {
            // Set first: a missing or empty registry must not retry the failed Resources.Load on
            // every subsequent call, which would spam the log once per UI repaint.
            _loaded = true;
            _bySpell.Clear();
            _inAuthoredOrder.Clear();

            SpellRegistrySO registry = Resources.Load<SpellRegistrySO>(ResourcePath);
            if (registry == null)
            {
                Debug.LogError($"[SpellRegistry] No SpellRegistrySO found at Resources/{ResourcePath}. Every spell loadout will be empty.");
                return;
            }
            if (registry.AllSpells == null) return;

            foreach (SpellData spellData in registry.AllSpells)
            {
                if (spellData == null || spellData.Spell == Spell.None) continue;

                if (_bySpell.TryGetValue(spellData.Spell, out SpellData claimed))
                {
                    Debug.LogError($"[SpellRegistry] '{spellData.name}' declares Spell.{spellData.Spell}, already claimed by '{claimed.name}'. Ignoring the duplicate - give one of them its own enum value.");
                    continue;
                }

                _bySpell.Add(spellData.Spell, spellData);
                _inAuthoredOrder.Add(spellData);
            }
        }

        public static SpellData Get(Spell spell)
        {
            if (spell == Spell.None) return null;
            EnsureLoaded();
            return _bySpell.TryGetValue(spell, out SpellData spellData) ? spellData : null;
        }

        public static bool Exists(Spell spell) => Get(spell) != null;

        /// <summary>Every registered spell, in the order authored on the registry asset.</summary>
        public static IReadOnlyList<SpellData> All
        {
            get
            {
                EnsureLoaded();
                return _inAuthoredOrder;
            }
        }

        /// <summary>
        /// Resolves a saved loadout to assets. Entries with no registered asset resolve to null
        /// rather than being skipped, so slot indices are preserved for the caller.
        /// </summary>
        public static SpellData[] Resolve(IReadOnlyList<Spell> spells)
        {
            if (spells == null) return new SpellData[0];

            SpellData[] resolved = new SpellData[spells.Count];
            for (int i = 0; i < spells.Count; i++)
            {
                resolved[i] = Get(spells[i]);
            }
            return resolved;
        }
    }
}
