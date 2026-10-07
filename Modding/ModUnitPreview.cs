using System;
using MemoriStudios.TJBake;
using TabletopTavern.GpuAnim;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TJ
{
    /// <summary>
    /// The 3D unit model the recruit, prestige and Collection screens show: a mod's unit visual when one is registered,
    /// otherwise the built-in recruit prefab.
    /// </summary>
    public static class ModUnitPreview
    {
        /// <summary>
        /// Instantiates the unit's preview under <paramref name="parent"/>. A mod visual copies the recruit prefab's root
        /// transform and layer, so each screen's hand-set framing still fits.
        /// </summary>
        public static GameObject Instantiate(UnitName unit, GameObject recruitPrefab, Transform parent)
        {
            GameObject mod = TryCreate(unit, recruitPrefab, parent);
            return mod != null ? mod : Object.Instantiate(recruitPrefab, parent);
        }

        private static GameObject TryCreate(UnitName unit, GameObject recruitPrefab, Transform parent)
        {
            if (!UnitVisualOverrides.TryGet(unit.ToString(), out string folder)) return null;
            var go = new GameObject($"{unit} (mod visual)");
            go.transform.SetParent(parent, false);
            if (recruitPrefab != null)
            {
                Transform source = recruitPrefab.transform;
                go.transform.localPosition = source.localPosition;
                go.transform.localRotation = source.localRotation;
                go.transform.localScale = source.localScale;
                go.layer = recruitPrefab.layer;
            }
            try
            {
                var player = go.AddComponent<TJBakePreviewPlayer>();
                player.LoadFolder(folder);
                player.SetLayer(go.layer);
                return go;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnitVisualOverride] {unit}: preview failed ({e.Message}). The built-in model shows instead.");
                Object.Destroy(go);
                return null;
            }
        }

        /// <summary>True when the object is a mod visual, which has no Animator and no swappable materials.</summary>
        public static bool IsModVisual(GameObject preview, out TJBakePreviewPlayer player)
        {
            player = preview != null ? preview.GetComponent<TJBakePreviewPlayer>() : null;
            return player != null;
        }

        /// <summary>The Collection's undiscovered look: one flat colour, held still.</summary>
        public static void ShowUndiscovered(TJBakePreviewPlayer player, Material undiscovered)
        {
            Color colour = undiscovered != null && undiscovered.HasProperty("_BaseColor") ? undiscovered.GetColor("_BaseColor") : new Color(0.1f, 0.1f, 0.1f, 1f);
            player.Speed = 0f;
            player.SetFlatColour(colour);
        }
    }
}
