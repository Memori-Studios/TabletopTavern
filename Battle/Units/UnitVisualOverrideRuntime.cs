using System;
using System.Collections.Generic;
using MemoriStudios.TJBake;
using MemoriStudios.TJBake.Format;
using TabletopTavern.GpuAnim;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Builds a unit's visual from a mod folder at battle load and owns it until battle cleanup. The spawn code only ever
/// sees the UnitGPUAnimPrefabs entry the loader makes from the result.
/// </summary>
public static class UnitVisualOverrideRuntime
{
    private static readonly List<GpuAnimVisualHandle> s_handles = new();
    private static readonly List<UnityEngine.Object> s_owned = new();

    public static int BuiltThisBattle => s_handles.Count;

    /// <summary>
    /// True with four prefab entities (three variants, repeated when the mod ships fewer, and the rider or Entity.Null)
    /// when the unit has a registered folder that loads cleanly. A folder that fails logs once and the built-in prefabs load.
    /// A rider slot left Entity.Null while <paramref name="hasBuiltInRider"/> is true means the caller loads the built-in rider.
    /// </summary>
    public static bool TryBuild(EntityManager em, UnitName unitName, bool hasBuiltInRider, out Entity[] prefabs)
    {
        prefabs = null;
        if (!UnitVisualOverrides.TryGet(unitName.ToString(), out string folder)) return false;
        var owned = new List<UnityEngine.Object>();
        try
        {
            GpuAnimVisualData data = GpuAnimVisualImport.From(TJBakeVisualLoader.Load(folder, owned));
            CheckRider(data, hasBuiltInRider);
            int variants = data.Variants.Count;
            var handles = new GpuAnimVisualHandle[variants];
            for (int v = 0; v < variants; v++)
            {
                handles[v] = GpuAnimVisualBuilder.Build(em, data, v, MarkRole);
            }
            prefabs = new[]
            {
                handles[0].Prefab,
                handles[1 % variants].Prefab,
                handles[2 % variants].Prefab,
                handles[0].RiderPrefab,
            };
            foreach (GpuAnimVisualHandle h in handles) s_handles.Add(h);
            s_owned.AddRange(owned);
            Debug.Log($"[UnitVisualOverride] {unitName}: built {variants} variant(s) from {folder}" + (handles[0].RiderPrefab != Entity.Null ? " with a rider" : ""));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UnitVisualOverride] {unitName}: {e.Message}. The built-in visual loads instead.");
            foreach (UnityEngine.Object o in owned) if (o != null) UnityEngine.Object.Destroy(o);
            return false;
        }
    }

    /// <summary>Called by battle cleanup before the world is disposed.</summary>
    public static void DisposeAll(EntityManager em)
    {
        foreach (GpuAnimVisualHandle h in s_handles)
        {
            try { h.Dispose(em); }
            catch (Exception e) { Debug.LogError($"[UnitVisualOverride] dispose failed: {e.Message}"); }
        }
        s_handles.Clear();
        foreach (UnityEngine.Object o in s_owned) if (o != null) UnityEngine.Object.Destroy(o);
        s_owned.Clear();
    }

    // A rider changes which death and saddle rules run, so a mod may only reskin the rider a unit already has.
    private static void CheckRider(GpuAnimVisualData data, bool hasBuiltInRider)
    {
        if (data.Rider != null && !hasBuiltInRider) throw new GpuAnimFormatException("this unit has no rider in the game, so the folder may not ship one");
        if (data.Rider != null || !hasBuiltInRider) return;
        foreach (GpuAnimVariantData variant in data.Variants)
        {
            bool saddle = false;
            foreach (GpuAnimAttachmentData a in variant.Attachments) if (a.Role == GpuAnimPropRole.Saddle) saddle = true;
            if (!saddle) throw new GpuAnimFormatException("the built-in rider needs a 'saddle' attachment on every variant");
        }
    }

    // The set-up systems find props by these markers at the hop counts the contract fixes.
    private static void MarkRole(EntityManager em, Entity entity, GpuAnimPropRole role)
    {
        switch (role)
        {
            case GpuAnimPropRole.Bow: em.AddComponent<BowSetUpEntity>(entity); break;
            case GpuAnimPropRole.Sword: em.AddComponent<SwordSetUpEntity>(entity); break;
            case GpuAnimPropRole.Shield: em.AddComponent<ShieldSetUpEntity>(entity); break;
            case GpuAnimPropRole.Saddle: em.AddComponent<SaddleSetUpEntity>(entity); break;
        }
    }

}
