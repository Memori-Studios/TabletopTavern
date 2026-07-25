using Unity.Entities;

// Placed on a SINGLE enemy squad entity by Hunter's Mark (ActiveSpell.CastSpell, MarksTarget branch).
// HuntersMarkSystem ticks RemainingDuration down and amplifies every hostile DamageBufferElement queued
// against that squad's units by DamageMultiplier, then drops the tag when the timer runs out.
// Pure primitive fields, so this lives in Components (consumed by both the main assembly and Systems).
public struct HuntersMarkTag : IComponentData
{
    public float RemainingDuration;
    public float DamageMultiplier;
}
