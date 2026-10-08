using System;
using System.Collections;
using System.Collections.Generic;
using TabletopTavern.GpuAnim;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Builds the unit visuals for the current battle from the game's TJBake folders (StreamingAssets/UnitVisuals), with
/// any enabled mod folder on top, and publishes them as UnitGPUAnimPrefabs entities for the spawn code.
///
/// Flow: call PreloadUnitsAsync() once the battle's units are known; onComplete fires when every visual is built.
/// UnloadUnits() drops the lookups after the battle; UnitVisualOverrideRuntime.DisposeAll frees the visuals.
/// </summary>
public class UnitGPUAnimLoader : MonoBehaviour
{
    private const int RiderVariantIndex = 3;

    private readonly List<Entity> _prefabsEntities = new();
    // Guards against a second UnitGPUAnimPrefabs for one unit when a summon preloads after the armies did.
    private readonly HashSet<UnitName> _requestedUnits = new();
    private Entity _structureClock;

    /// <summary>Builds the visuals for the given units; onComplete fires once they exist. Units already built this battle are skipped.</summary>
    public Coroutine PreloadUnitsAsync(IEnumerable<UnitName> unitNames, Action onComplete)
    {
        return StartCoroutine(LoadRoutine(unitNames, onComplete));
    }

    /// <summary>Builds one more unit's visual mid-battle (a summon swapped into the loadout). No-op if already built.</summary>
    public void PreloadAdditionalUnit(UnitName unitName)
    {
        PreloadUnitsAsync(new[] { unitName }, null);
    }

    private IEnumerator LoadRoutine(IEnumerable<UnitName> unitNames, Action onComplete)
    {
        var toBuild = new List<UnitName>();
        foreach (UnitName unit in unitNames)
            if (_requestedUnits.Add(unit)) toBuild.Add(unit);

        var hostFolders = new List<string>();
        foreach (UnitName unit in toBuild)
            if (UnitVisualOverrides.TryGetBuiltIn(unit.ToString(), out string folder)) hostFolders.Add(folder);
        if (hostFolders.Count > 0) yield return UnitVisualOverrideRuntime.PreloadTextures(hostFolders);

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        foreach (UnitName unit in toBuild)
        {
            if (!UnitVisualOverrideRuntime.TryBuildBuiltIn(entityManager, unit, out Entity[] builtIn))
            {
                Debug.LogError($"[UnitGPUAnimLoader] {unit} has no unit visual in StreamingAssets/UnitVisuals; bake its recipe. It spawns without one.");
                continue;
            }
            Entity[] final = builtIn;
            // A mod on top is checked against the game's visual and falls back to it.
            if (UnitVisualOverrideRuntime.TryBuild(entityManager, unit, builtIn[RiderVariantIndex] != Entity.Null, out Entity[] modded)
                && FitsWeaponRule(entityManager, unit, modded, builtIn[0]))
            {
                final = new[] { modded[0], modded[1], modded[2],
                    modded[RiderVariantIndex] != Entity.Null ? modded[RiderVariantIndex] : builtIn[RiderVariantIndex] };
            }
            Entity lookup = entityManager.CreateEntity();
            entityManager.AddComponentData(lookup, new UnitGPUAnimPrefabs
            {
                unitName = unit, gpuAnim1 = final[0], gpuAnim2 = final[1], gpuAnim3 = final[2], riderEntity = final[RiderVariantIndex],
            });
            _prefabsEntities.Add(lookup);
        }

        onComplete?.Invoke();
    }

    // The bow-to-sword swap is gameplay (it stops shooting in melee), so a mod visual must swap exactly when the game's does.
    private static bool FitsWeaponRule(EntityManager em, UnitName unitName, Entity[] modVariants, Entity builtInVariant)
    {
        bool builtInSwaps = HasWeaponSwap(em, builtInVariant);
        for (int v = 0; v < 3; v++)
        {
            if (HasWeaponSwap(em, modVariants[v]) == builtInSwaps) continue;
            Debug.LogError($"[UnitVisualOverride] {unitName}: the built-in unit {(builtInSwaps ? "swaps bow for sword in melee, so every variant needs a 'bow' and a 'sword'" : "has no bow and sword pair, so no variant may have both")}. The built-in visual loads instead.");
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
    /// The invisible animator a structure (the garrison gate) carries so every animation lookup finds an entity. The gate
    /// spawns outside the army preload, so it is built here on first use; its folder has no Addressable textures.
    /// </summary>
    public Entity StructureClockPrefab()
    {
        if (_structureClock != Entity.Null) return _structureClock;
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (UnitVisualOverrideRuntime.TryBuildBuiltIn(entityManager, UnitName.Gate, out Entity[] built)) _structureClock = built[0];
        else Debug.LogError("[UnitGPUAnimLoader] The Gate clock did not build; bake the Gate recipe.");
        return _structureClock;
    }

    /// <summary>Destroys the UnitGPUAnimPrefabs lookups after the battle; the visuals themselves are freed by battle cleanup.</summary>
    public void UnloadUnits()
    {
        _structureClock = Entity.Null;
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        foreach (var e in _prefabsEntities)
            if (entityManager.Exists(e)) entityManager.DestroyEntity(e);
        _prefabsEntities.Clear();
        _requestedUnits.Clear();
    }
}
