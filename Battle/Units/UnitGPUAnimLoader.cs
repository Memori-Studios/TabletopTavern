using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;

/// <summary>
/// Handles lazy loading of GPU anim prefabs for only the units used in the current battle.
/// Cavalry units also load their rider prefab automatically when the unit is loaded.
///
/// Flow:
///   1. Before battle starts (when unit selections are known), call PreloadUnitsAsync().
///   2. The loader creates RequestEntityPrefabLoaded entities — Unity's WeakAssetReferenceLoadingSystem
///      converts the Addressable prefabs into entity prefabs asynchronously.
///   3. Once loaded, UnitGPUAnimPrefabs singleton entities are created for SpawnManager to query.
///   4. After the battle ends, call UnloadUnits() to destroy everything and release memory.
///
/// Variant index convention used internally:
///   0-2 = the three GPU anim variants, 3 = cavalry rider prefab (optional)
///
/// The GPU anim prefabs (and rider prefabs) must be marked as Addressable.
/// </summary>
public class UnitGPUAnimLoader : MonoBehaviour
{
    private const int RiderVariantIndex = 3;

    private readonly List<Entity> _requestEntities = new();
    private readonly List<Entity> _prefabsEntities = new();
    // Units already requested this battle. Guards against duplicate load requests (and duplicate
    // UnitGPUAnimPrefabs singletons) when PreloadUnitsAsync is called more than once - e.g. an
    // incremental summon-spell preload after the initial army preload. Added synchronously so a
    // rapid double request cannot slip through before the async load completes.
    private readonly HashSet<UnitName> _requestedUnits = new();

    /// <summary>
    /// Begins async loading of GPU anim prefabs (and rider prefabs for cavalry) for the given unit names.
    /// onComplete is called once all prefabs are ready and SpawnManager can proceed. Units already
    /// requested this battle are skipped.
    /// </summary>
    public Coroutine PreloadUnitsAsync(IEnumerable<UnitName> unitNames, Action onComplete)
    {
        return StartCoroutine(LoadRoutine(unitNames, onComplete));
    }

    /// <summary>
    /// Loads a single unit's GPU anim prefabs on demand, after the initial army preload has already run
    /// (e.g. a summon spell swapped into the loadout during deployment). No-op if already loaded.
    /// </summary>
    public void PreloadAdditionalUnit(UnitName unitName)
    {
        PreloadUnitsAsync(new[] { unitName }, null);
    }

    private IEnumerator LoadRoutine(IEnumerable<UnitName> unitNames, Action onComplete)
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var unitNamesSet = new HashSet<UnitName>(unitNames);

        var refsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitGPUAnimRefs>());
        var allRefs = refsQuery.ToComponentDataArray<UnitGPUAnimRefs>(Allocator.Temp);

        // Maps request entity → (unitName, variantIndex)  where variantIndex 0-2 = anim, 3 = rider
        var requestMap = new Dictionary<Entity, (UnitName unitName, int variantIndex)>();

        for (int i = 0; i < allRefs.Length; i++)
        {
            var refs = allRefs[i];
            if (!unitNamesSet.Contains(refs.unitName)) continue;
            if (!_requestedUnits.Add(refs.unitName)) continue; // already requested this battle

            // Load the three anim variants
            for (int v = 0; v < 3; v++)
            {
                Entity reqEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(reqEntity, new RequestEntityPrefabLoaded { Prefab = refs.Get(v) });
                requestMap[reqEntity] = (refs.unitName, v);
                _requestEntities.Add(reqEntity);
            }

            // Load the rider prefab only if this is a cavalry unit
            if (refs.riderGPUAnim.IsReferenceValid)
            {
                Entity riderReqEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(riderReqEntity, new RequestEntityPrefabLoaded { Prefab = refs.riderGPUAnim });
                requestMap[riderReqEntity] = (refs.unitName, RiderVariantIndex);
                _requestEntities.Add(riderReqEntity);
            }
        }

        allRefs.Dispose();
        refsQuery.Dispose();

        if (requestMap.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        // Wait until every request entity has received a PrefabLoadResult.
        yield return new WaitUntil(() =>
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            foreach (var reqEntity in requestMap.Keys)
                if (!em.HasComponent<PrefabLoadResult>(reqEntity)) return false;
            return true;
        });

        // Collect loaded root entities grouped by unit name.
        // Slot 0-2 = anim variants, slot 3 = rider (Entity.Null if non-cavalry)
        var resultsByUnit = new Dictionary<UnitName, Entity[]>();
        foreach (var (reqEntity, (unitName, variantIndex)) in requestMap)
        {
            if (!resultsByUnit.ContainsKey(unitName))
                resultsByUnit[unitName] = new Entity[4]; // [0-2] anims, [3] rider
            resultsByUnit[unitName][variantIndex] = entityManager
                .GetComponentData<PrefabLoadResult>(reqEntity).PrefabRoot;
        }

        // Create a UnitGPUAnimPrefabs entity for each unit so SpawnManager can query them.
        foreach (var (unitName, entities) in resultsByUnit)
        {
            Entity e = entityManager.CreateEntity();
            entityManager.AddComponentData(e, new UnitGPUAnimPrefabs
            {
                unitName = unitName,
                gpuAnim1 = entities[0],
                gpuAnim2 = entities[1],
                gpuAnim3 = entities[2],
                riderEntity = entities[3], // Entity.Null (default) for non-cavalry
            });
            _prefabsEntities.Add(e);
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// Destroys the UnitGPUAnimPrefabs lookup entities and the RequestEntityPrefabLoaded
    /// request entities (which releases the Addressable reference count for both unit anims and riders).
    /// Call this after the battle ends.
    /// </summary>
    public void UnloadUnits()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        foreach (var e in _prefabsEntities)
            if (entityManager.Exists(e)) entityManager.DestroyEntity(e);
        _prefabsEntities.Clear();

        foreach (var e in _requestEntities)
            if (entityManager.Exists(e)) entityManager.DestroyEntity(e);
        _requestEntities.Clear();

        _requestedUnits.Clear();
    }
}
