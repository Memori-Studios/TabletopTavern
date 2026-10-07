using System.Collections.Generic;
using UnityEngine;

namespace TJ
{
    /// <summary>Every unit's worth for the running game, worked out once from the loaded stats so modded units are priced too.</summary>
    public static class UnitValues
    {
        private static float[] _byOrdinal;

        /// <summary>0 to 100 for a full-strength squad of this unit. 0 for units the model does not price.</summary>
        public static float Of(UnitName unit)
        {
            if (_byOrdinal == null) Calculate();
            int index = (int)unit;
            return _byOrdinal != null && index >= 0 && index < _byOrdinal.Length ? _byOrdinal[index] : 0f;
        }

        /// <summary>The worth one model of this unit carries: its squad's value over its base model count.</summary>
        public static float PerModel(UnitName unit)
        {
            TabletopTavernData data = TabletopTavernData.InstanceIfExists;
            if (data == null) return 0f;
            return Of(unit) / Mathf.Max(1, data.GetBaseUnitCount(unit));
        }

        /// <summary>Forgets the values; the next read works them out again. Called whenever the unit stats are rebuilt.</summary>
        public static void Invalidate() => _byOrdinal = null;

        private static void Calculate()
        {
            TabletopTavernData data = TabletopTavernData.InstanceIfExists;
            if (data == null)
            {
                // Left uncached so a read after the data manager exists still gets real values.
                Debug.LogError("[Unit Value] Values were read before TabletopTavernData exists.");
                return;
            }
            data.EnsureDataLoaded();

            var stats = new List<SquadStats>(data.SquadStatsDictionary.Values);
            // Enum order, so the Editor report and the running game add the same numbers in the same order.
            stats.Sort((a, b) => ((int)a.unitName).CompareTo((int)b.unitName));

            UnitValueWeights weights = Resources.Load<UnitValueWeights>(UnitValueWeights.ResourcePath);
            bool temporary = weights == null;
            if (temporary) weights = ScriptableObject.CreateInstance<UnitValueWeights>();
            List<UnitValueResult> results = UnitValueModel.ValueAll(stats, weights);
            if (temporary)
            {
                if (Application.isPlaying) Object.Destroy(weights);
                else Object.DestroyImmediate(weights);
            }

            int size = 0;
            foreach (UnitValueResult result in results) size = Mathf.Max(size, (int)result.Unit + 1);
            _byOrdinal = new float[size];
            foreach (UnitValueResult result in results)
                if (result.Valued) _byOrdinal[(int)result.Unit] = result.Value;
        }
    }
}
