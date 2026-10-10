using Unity.Entities;
using Unity.Mathematics;

// Append only: OlympianLeagueVisuals serializes it. Outside the define so that asset never loses its script.
public enum OlympianVisual : byte { Lightning, Aegis, Tremor, Fury, Sun, Shades, Gaze }

#if FACTIONUPDATE

#region Race
public struct OlympianLeagueRaceTag : IComponentData { }

// Answered Prayers: strikes left this battle, and whether the squad was already Wavering last frame.
public struct AnsweredPrayersComponent : IComponentData
{
    public int StrikesLeft;
    public byte WasWavering;
}
#endregion

#region Blessings
// Each blessing fires once (Spent) except Zeus's Bolt, which fires on every landed charge.
public struct AresFuryBlessing : IComponentData
{
    public byte Spent;
}

public struct AthenasAegisBlessing : IComponentData
{
    public byte Spent;
    public float CombatTime;
    public float Remaining;
}

// While present, every non-healing hit on the squad's models is cancelled.
public struct AegisActiveTag : IComponentData { }

public struct ZeussBoltBlessing : IComponentData { }

// Added to a charger whose charge landed; the bolt strikes Position, the charged squad's centre.
public struct ZeusBoltStrike : IComponentData
{
    public float3 Position;
}

public struct PoseidonsTremorBlessing : IComponentData
{
    public byte Spent;
    public int StartCount;
}

public struct ApollosSunBlessing : IComponentData
{
    public byte Spent;
}

public struct HadesShadesBlessing : IComponentData
{
    public byte Spent;
}

public struct GorgonsGazeBlessing : IComponentData
{
    public byte Spent;
}

// Added to the squad that charged the Gorgon; PetrifySystem turns it into Petrified.
public struct PetrifyRequest : IComponentData { }

// On a model (or rider) entity. Unit is the unit it belongs to; that unit dying ends the freeze early.
public struct Petrified : IComponentData
{
    public float Remaining;
    public Entity Unit;
}
#endregion

#region Visuals
// Drained once a frame by EntityWatcher, which spawns the effect, plays the cue and shakes the camera.
[InternalBufferCapacity(4)]
public struct OlympianVisualRequest : IBufferElementData
{
    public float3 Position;
    public OlympianVisual Kind;
}
#endregion
#endif
