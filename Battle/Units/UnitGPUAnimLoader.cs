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
        // Mod-built prefabs, finished once the built-in variant 0 and rider they are checked against have loaded.
        var modBuilt = new Dictionary<UnitName, Entity[]>();

        for (int i = 0; i < allRefs.Length; i++)
        {
            var refs = allRefs[i];
            if (!unitNamesSet.Contains(refs.unitName)) continue;
            if (!_requestedUnits.Add(refs.unitName)) continue; // already requested this battle

            // A mod's visual folder replaces the baked prefabs for this battle; the spawn code never knows.
            // Built-in variant 0 still loads for a modded unit: it decides the bow-and-sword rule and is the fallback.
            bool modded = UnitVisualOverrideRuntime.TryBuild(entityManager, refs.unitName, refs.riderGPUAnim.IsReferenceValid, out Entity[] built);
            if (modded) modBuilt[refs.unitName] = built;

            // Load the three anim variants
            for (int v = 0; v < (modded ? 1 : 3); v++)
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
            if (modBuilt.TryGetValue(unitName, out Entity[] built))
            {
                Entity builtInRider = entities[RiderVariantIndex];
                if (FitsWeaponRule(entityManager, unitName, built, entities[0]))
                {
                    entities[0] = built[0];
                    entities[1] = built[1];
                    entities[2] = built[2];
                    entities[RiderVariantIndex] = built[RiderVariantIndex] != Entity.Null ? built[RiderVariantIndex] : builtInRider;
                }
                else
                {
                    entities[1] = entities[0];
                    entities[2] = entities[0];
                }
            }
            // Game systems write GpuAnimControl on every visual; the old bakes get it on the prefab so every instance carries it.
            foreach (Entity prefab in entities) GpuAnimLegacy.Attach(entityManager, prefab);
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

    // The bow-to-sword swap is gameplay (it stops shooting in melee), so a mod visual must swap exactly when the built-in one does.
    private static bool FitsWeaponRule(EntityManager em, UnitName unitName, Entity[] modVariants, Entity builtInVariant)
    {
        bool builtInSwaps = HasWeaponSwap(em, builtInVariant);
        for (int v = 0; v < 3; v++)
        {
            if (HasWeaponSwap(em, modVariants[v]) == builtInSwaps) continue;
            Debug.LogError($"[UnitVisualOverride] {unitName}: the built-in unit {(builtInSwaps ? "swaps bow for sword in melee, so every variant needs one 'bow' and one 'sword'" : "has no bow and sword pair, so no variant may have both")}. The built-in visual loads instead.");
            return false;
        }
        return true;
    }

    private static bool HasWeaponSwap(EntityManager em, Entity prefab)
    {
        if (prefab == Entity.Null || !em.HasBuffer<LinkedEntityGroup>(prefab)) return false;
        bool bow = false, sword = false;
        foreach (LinkedEntityGroup linked in em.GetBuffer<LinkedEntityGroup>(prefab))
        {
            if (em.HasComponent<BowSetUpEntity>(linked.Value)) bow = true;
            if (em.HasComponent<SwordSetUpEntity>(linked.Value)) sword = true;
        }
        return bow && sword;
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
