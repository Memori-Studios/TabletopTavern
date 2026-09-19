using Unity.Entities;

public struct KillUnitTag : IComponentData { }
public struct RemoveArtilleryTag : IComponentData { public int SquadID; }
// A corpse keeps its mesh entities after the unit entity dies, so the outline bits must be cleared on it.
public struct UnitOutlineClearTag : IComponentData { }
