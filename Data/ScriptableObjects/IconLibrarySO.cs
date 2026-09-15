using System.Collections.Generic;
using UnityEngine;

namespace TJ
{
    /// <summary>
    /// Every UI sprite the code asks for by name through <see cref="SpriteData.GetSprite"/>: gear,
    /// consumables, unit stats, event outcomes, map node icons and a few literals.
    ///
    /// This replaced a string switch that mapped each key to a Resources.Load path, which was the
    /// only reason icons had to live under a Resources folder. The sprites themselves are ordinary
    /// GUID references, so they can sit anywhere (Assets/Art/Icons) and be renamed freely; only
    /// this one asset lives in Resources, mirroring <see cref="Spells.SpellRegistrySO"/>.
    ///
    /// Keys are what the call sites pass: a <see cref="GearID"/>'s GearName, an enum value's
    /// ToString(), or a literal. IconLibraryTests asserts every key the code can ask for resolves,
    /// so a missing entry fails in EditMode instead of logging at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "IconLibrary", menuName = "GameData/IconLibrary", order = 3)]
    public class IconLibrarySO : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string key;
            public Sprite sprite;
        }

        [SerializeField] private Entry[] entries;

        public IReadOnlyList<Entry> Entries => entries;

#if UNITY_EDITOR
        public void SetEntries(Entry[] value) => entries = value;
#endif
    }
}
