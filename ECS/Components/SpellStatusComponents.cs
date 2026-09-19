using Unity.Entities;

// One entry per spell currently in effect on this SQUAD, read by SquadFlagGameObject to show the
// spell's icon above the health bar. SpellId is the TJ.Spells.Spell ordinal as an int because that
// enum lives in the main assembly. Nothing ticks this buffer: readers skip expired entries and the
// next writer prunes them.
[InternalBufferCapacity(4)]
public struct SpellStatusBufferElement : IBufferElementData
{
    public int SpellId;
    public double ExpiresAtTime;
}

public static class SpellStatus
{
    // Add or refresh one spell's entry, dropping anything already expired on the way.
    public static void Set(DynamicBuffer<SpellStatusBufferElement> buffer, int spellId, double expiresAtTime, double now)
    {
        bool found = false;
        for (int i = buffer.Length - 1; i >= 0; i--)
        {
            if (buffer[i].SpellId == spellId)
            {
                buffer[i] = new SpellStatusBufferElement { SpellId = spellId, ExpiresAtTime = expiresAtTime };
                found = true;
            }
            else if (buffer[i].ExpiresAtTime <= now)
            {
                buffer.RemoveAtSwapBack(i);
            }
        }
        if (!found) buffer.Add(new SpellStatusBufferElement { SpellId = spellId, ExpiresAtTime = expiresAtTime });
    }
}
