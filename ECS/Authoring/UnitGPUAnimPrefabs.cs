using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Runtime component: one per unit in the battle, made by UnitGPUAnimLoader from the unit's TJBake visual.
/// SpawnManager and ArmySpawnManager query this via Find() to get the actual Entity prefabs.
/// riderEntity is Entity.Null for non-cavalry units.
/// Destroyed by UnitGPUAnimLoader.UnloadUnits() after the battle ends.
/// </summary>
public struct UnitGPUAnimPrefabs : IComponentData
{
    public UnitName unitName;
    public Entity gpuAnim1, gpuAnim2, gpuAnim3;
    public Entity riderEntity;

    public readonly Entity Get(int index) => (index % 3) switch
    {
        0 => gpuAnim1,
        1 => gpuAnim2,
        2 => gpuAnim3,
        _ => Entity.Null,
    };

    public readonly bool HasRider => riderEntity != Entity.Null;

    public static UnitGPUAnimPrefabs? Find(EntityManager entityManager, UnitName unitName)
    {
        using EntityQuery q = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitGPUAnimPrefabs>());
        if (q.IsEmpty) return null;
        using NativeArray<UnitGPUAnimPrefabs> all = q.ToComponentDataArray<UnitGPUAnimPrefabs>(Allocator.Temp);
        foreach (var entry in all)
        {
            if (entry.unitName == unitName) return entry;
        }
        return null;
    }
}
