using Unity.Entities;
using Unity.Mathematics;

// Boblin's Snare Trap. A hidden, stationary entity placed by the spell (ActiveSpell PlacesTrap branch).
// SnareTrapSystem watches for an enemy to enter TriggerRadius, then springs a one-off burst SpellEntity
// (reusing SpellSystem's damage + knockback) across BlastRadius and destroys the trap. If nothing trips
// it within RemainingArmedTime seconds it quietly expires.
public struct SnareTrapEntity : IComponentData
{
    public float3 Position;
    public float TriggerRadius;
    public float BlastRadius;
    public int Damage;
    public float SpellForce;
    public Team OwnerTeam;
    public float RemainingArmedTime;
}
