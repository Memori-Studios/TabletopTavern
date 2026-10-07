using UnityEngine;

namespace TJ.Spells
{
    // Addon art that outlives its spell (ground scorch marks): it leaves the spell object once placed and removes itself after lifetime.
    public class SpellAddonLinger : MonoBehaviour
    {
        // Seconds from the spell being placed until this art is removed; must cover its last particle.
        [SerializeField] private float lifetime = 13f;

        private void Start()
        {
            transform.SetParent(null, true);
            BattleManager battle = BattleManager.InstanceIfExists;
            if (battle != null) battle.SquadManager.stuffToDestroy.Add(gameObject);
            Destroy(gameObject, lifetime);
        }
    }
}
