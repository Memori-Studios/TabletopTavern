using UnityEngine;

namespace TJ.Spells
{
    // Optional per-spell art spawned under the shared AOE Spell instance (SpellData.SpellVisualPrefab).
    // ActiveSpell switches warmupEffect on when the wind-up starts and castEffect on when the spell lands;
    // an addon that just plays from spawn leaves both empty.
    public class SpellVisualAddon : MonoBehaviour
    {
        public GameObject warmupEffect;
        public GameObject castEffect;
        // The SpellRadius this art was built for; ActiveSpell scales the addon by SpellRadius / authoredRadius. 0 keeps prefab scale.
        public float authoredRadius = 0f;
        // Art whose colour is the read (green heal, grey smoke) is skipped by the race tint.
        public bool keepOwnColors = false;
        // Art under these roots keeps its own colour while the rest of the addon still takes the race tint.
        public Transform[] keepOwnColorRoots;
        // Warm-up art that should not outlive the wind-up (a looping "incoming" marker) is switched off on cast.
        public bool hideWarmupOnCast = false;
    }
}
