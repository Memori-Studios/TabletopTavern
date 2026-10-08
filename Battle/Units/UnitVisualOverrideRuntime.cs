using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Memori.Utilities;
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
    // Textures the game's own bakes name as Addressables, loaded for this battle and released at cleanup.
    private static readonly Dictionary<string, Texture2D> s_textures = new();

    public static int BuiltThisBattle => s_handles.Count;

    // Stopping Play mid-battle skips battle cleanup, and the anchor blobs would outlive the domain.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void HookQuit()
    {
        Application.quitting -= DisposeBlobsOnQuit;
        Application.quitting += DisposeBlobsOnQuit;
    }

    private static void DisposeBlobsOnQuit()
    {
        foreach (GpuAnimVisualHandle h in s_handles) h.DisposeBlobs();
        s_handles.Clear();
    }

    /// <summary>
    /// True with four prefab entities (three variants, repeated when the mod ships fewer, and the rider or Entity.Null)
    /// when the unit has a registered folder that loads cleanly. A folder that fails logs once and the built-in prefabs load.
    /// A rider slot left Entity.Null while <paramref name="hasBuiltInRider"/> is true means the caller loads the built-in rider.
    /// </summary>
    public static bool TryBuild(EntityManager em, UnitName unitName, bool hasBuiltInRider, out Entity[] prefabs)
    {
        prefabs = null;
        if (!UnitVisualOverrides.TryGet(unitName.ToString(), out string folder)) return false;
        return TryBuildFolder(em, unitName, folder, hasBuiltInRider, "mod", out prefabs);
    }

    /// <summary>The game's own TJBake visual for the unit. A failure logs once and the unit spawns without a visual.</summary>
    public static bool TryBuildBuiltIn(EntityManager em, UnitName unitName, out Entity[] prefabs)
    {
        prefabs = null;
        if (!UnitVisualOverrides.TryGetBuiltIn(unitName.ToString(), out string folder)) return false;
        return TryBuildFolder(em, unitName, folder, false, "built-in", out prefabs);
    }

    private static bool TryBuildFolder(EntityManager em, UnitName unitName, string folder, bool hasBuiltInRider, string label, out Entity[] prefabs)
    {
        prefabs = null;
        var owned = new List<UnityEngine.Object>();
        try
        {
            Func<string, Texture2D> resolve = label == "built-in" ? ResolveTexture : null;
            GpuAnimVisualData data = GpuAnimVisualImport.From(TJBakeVisualLoader.Load(folder, owned, false, resolve));
            // The game's own folder defines whether the unit has a rider; a mod may only reskin it.
            if (label == "mod") CheckRider(data, hasBuiltInRider);
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
            if (label == "built-in" && TJ.TabletopTavernData.Instance.GetUnitTypeFromUnitName(unitName) == UnitType.Artillery)
                foreach (GpuAnimVisualHandle h in handles) MarkArtilleryCrewPoint(em, h.Prefab);
            s_owned.AddRange(owned);
            Debug.Log($"[UnitVisualOverride] {unitName}: built {variants} {label} variant(s) from {folder}" + (handles[0].RiderPrefab != Entity.Null ? " with a rider" : ""));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UnitVisualOverride] {unitName} ({label}): {e.Message}." + (label == "mod" ? " The built-in visual loads instead." : ""));
            foreach (UnityEngine.Object o in owned) if (o != null) UnityEngine.Object.Destroy(o);
            return false;
        }
    }

    /// <summary>Loads every Addressable texture the given built-in folders name; finishes when all have loaded or failed.</summary>
    public static IEnumerator PreloadTextures(IEnumerable<string> folders)
    {
        var pending = new List<(string address, Task<Texture2D> task)>();
        var started = new HashSet<string>();
        foreach (string folder in folders)
        {
            foreach (string address in HostTextureAddresses(folder))
            {
                if (s_textures.ContainsKey(address) || !started.Add(address)) continue;
                pending.Add((address, AddressablesManager.Instance.LoadAsync<Texture2D>(address)));
            }
        }
        foreach ((string address, Task<Texture2D> task) in pending)
        {
            while (!task.IsCompleted) yield return null;
            s_textures[address] = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
        }
    }

    private static IEnumerable<string> HostTextureAddresses(string folder)
    {
        var addresses = new List<string>();
        foreach (string manifest in new[] { Path.Combine(folder, TJBakeVisualLoader.ManifestName), Path.Combine(folder, "rider", TJBakeVisualLoader.ManifestName) })
        {
            if (!File.Exists(manifest)) continue;
            UnitJson json;
            try { json = JsonUtility.FromJson<UnitJson>(File.ReadAllText(manifest)); }
            catch (Exception) { continue; }
            if (json?.materials == null) continue;
            foreach (MaterialJson m in json.materials)
                foreach (string name in new[] { m.baseColor, m.normal, m.emission })
                    if (!string.IsNullOrEmpty(name) && name.StartsWith(TJBakeValidation.HostTexturePrefix)) addresses.Add(name.Substring(TJBakeValidation.HostTexturePrefix.Length));
        }
        return addresses;
    }

    private static Texture2D ResolveTexture(string address) => s_textures.TryGetValue(address, out Texture2D t) ? t : null;

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
        if (AddressablesManager.HasInstance)
            foreach (string address in s_textures.Keys) AddressablesManager.Instance.Release(address);
        s_textures.Clear();
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

    // The artillery clock's spawn point (its bow prop) is where EntityWatcher hangs the crew; it looks two hops below the unit,
    // which is the anchor follower.
    private static void MarkArtilleryCrewPoint(EntityManager em, Entity prefab)
    {
        if (!em.HasBuffer<LinkedEntityGroup>(prefab)) return;
        var followers = new List<Entity>();
        foreach (LinkedEntityGroup linked in em.GetBuffer<LinkedEntityGroup>(prefab))
            if (em.HasComponent<GpuAnimAttachment>(linked.Value) && em.HasComponent<GpuAnimRole>(linked.Value) && em.GetComponentData<GpuAnimRole>(linked.Value).Value == GpuAnimPropRole.Bow)
                followers.Add(linked.Value);
        foreach (Entity follower in followers) em.AddComponent<ArtilleryCrewSetUpEntity>(follower);
    }

    // The set-up systems find props by these markers at the hop counts the contract fixes.
    private static void MarkRole(EntityManager em, Entity entity, GpuAnimPropRole role, string tag)
    {
        if (tag == UnitVisualTags.RandomShield) em.AddComponent<ShieldRandomMesh>(entity);
        switch (role)
        {
            case GpuAnimPropRole.Bow: em.AddComponent<BowSetUpEntity>(entity); break;
            case GpuAnimPropRole.Sword: em.AddComponent<SwordSetUpEntity>(entity); break;
            case GpuAnimPropRole.Shield: em.AddComponent<ShieldSetUpEntity>(entity); break;
            case GpuAnimPropRole.Saddle: em.AddComponent<SaddleSetUpEntity>(entity); break;
        }
    }

}
