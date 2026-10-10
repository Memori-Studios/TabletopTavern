#if FACTIONUPDATE
using Unity.Entities;
using Unity.Mathematics;

// One-off blasts for the Olympian League; SpellSystem resolves the damage, the team filter and the knockback.
public static class OlympianStrikes
{
    // A damage of 0 makes a knockback-only blast.
    public static void Burst(EntityCommandBuffer ecb, float3 position, float radius, int damage, float force, Team team, int squadId)
    {
        Entity burst = ecb.CreateEntity();
        ecb.AddComponent(burst, new SpellEntity
        {
            Entity = burst,
            DamageBufferElement = new DamageBufferElement
            {
                DamageType = DamageType.Magical,
                DamageSource = DamageSource.Spell,
                AttackStrength = damage,
                TeamOfSource = team,
                DamageSourceSquadId = squadId,
            },
            SpellPosition = position,
            SpellRadius = radius,
            IsOneOff = true,
            SpellForce = force,
            SkipsDamage = damage <= 0,
            TargetSquadEntity = Entity.Null,
        });
    }
}
#endif
