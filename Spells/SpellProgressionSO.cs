using Memori.Metaprogression;
using UnityEngine;

namespace TJ.Spells
{
    /// <summary>
    /// The Renown nodes that upgrade the spell system, in Resources so any scene can read them.
    ///
    /// A ScriptableObject rather than SerializeFields on a manager, because the two readers live in
    /// different scenes: the loadout slot count is needed by WarbandPanel in MainMenu, while the mana
    /// pool is needed by SpellManager in TavernBattle. CampaignSaveManager - which holds the reserve
    /// and gear slot node refs the same way - only exists in the Map scene, so it cannot serve either.
    /// Same reasoning as SpellRegistrySO.
    ///
    /// Both arrays are counted, not indexed, so adding a third node to either is an Editor-only change.
    /// </summary>
    [CreateAssetMenu(fileName = "SpellProgression", menuName = "GameData/SpellProgression", order = 2)]
    public class SpellProgressionSO : ScriptableObject
    {
        [Header("Loadout Slots")]
        // Each unlocked node grants one more spell slot, above SpellLoadout.BaseSlotCount and capped
        // at SpellLoadout.SlotCount. Extra nodes beyond that cap are ignored rather than erroring.
        public MetaprogressionModel[] SlotUnlockNodes;

        [Header("Mana Pool")]
        // Each unlocked node adds its own NodeValue to the per-battle pool, so set NodeValue on the
        // asset (2 is the intended step). UpgradesPanel appends NodeValue to the node's tooltip, so a
        // node left at 0 reads wrong as well as granting nothing. Leave the array empty to ship the
        // base pool with no upgrades at all.
        public MetaprogressionModel[] ManaUpgradeNodes;
    }
}
