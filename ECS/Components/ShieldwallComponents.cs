using Unity.Entities;

// Bjorn's Shieldwall. ShieldwallTag sits on a braced friendly SQUAD; ShieldwallSystem reads it to make
// the squad's units knockback-immune and slow for RemainingDuration, then reverses and drops the tag.
// Applied guards the one-time stat change so it is not re-applied every frame.
public struct ShieldwallTag : IComponentData
{
    public float RemainingDuration;
    public bool Applied;
}

// Marks a unit whose ResistKnockbackTag was granted BY Shieldwall, so expiry only strips the immunity
// the spell added - units that are knockback-immune from spawn keep theirs.
public struct ShieldwallResistGrantedTag : IComponentData { }
