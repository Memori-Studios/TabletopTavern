using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TJ
{
    /// <summary>Writes every unit's value and pillars to Tools/UnitValue/Output, so a set of weights can be judged against the whole roster.</summary>
    public static class UnitValueReport
    {
        private const string OutputFolder = "Tools/UnitValue/Output";
        private const string WeightsAssetPath = "Assets/Resources/" + UnitValueWeights.ResourcePath + ".asset";

        [MenuItem("Tabletop Tavern/Unit Value/Write Report")]
        public static void Write()
        {
            var squads = new List<SquadData>();
            var seen = new HashSet<UnitName>();
            foreach (SquadData squad in Resources.LoadAll<SquadData>("SquadData").OrderBy(s => (int)s.stats.unitName))
            {
                if (seen.Add(squad.stats.unitName)) squads.Add(squad);
                else Debug.LogWarning($"[Unit Value] {squad.name} repeats {squad.stats.unitName}; the first asset is used.");
            }

            UnitValueWeights weights = UnitValueWeights.LoadOrDefault();
            List<UnitValueResult> results = UnitValueModel.ValueAll(squads.Select(s => s.stats).ToList(), weights);
            if (!AssetDatabase.Contains(weights)) Object.DestroyImmediate(weights);

            var rows = new List<(SquadData squad, UnitValueResult result)>();
            for (int i = 0; i < squads.Count; i++) rows.Add((squads[i], results[i]));
            rows.Sort((a, b) => b.result.Value.CompareTo(a.result.Value));

            var csv = new StringBuilder("unit,race,type,size,rarity,value,power,offence,toughness,traits,source\n");
            var table = new StringBuilder("| Unit | Race | Type | Rarity | Value | Offence | Toughness | Traits |\n|---|---|---|---|---:|---:|---:|---|\n");
            foreach ((SquadData squad, UnitValueResult result) in rows)
            {
                SquadStats stats = squad.stats;
                string traits = Traits(stats.SquadAttributes);
                string value = result.Valued ? Number(result.Value, "F1") : "";
                // Priced by the model, so it has pillars; a fixed value has none.
                bool priced = result.Valued && !result.Fixed;
                csv.Append(stats.unitName).Append(',').Append(squad.assets.race).Append(',').Append(stats.unitType).Append(',')
                    .Append(stats.unitSize).Append(',').Append(stats.RarityTier).Append(',').Append(value).Append(',')
                    .Append(result.Valued ? Number(result.Power, "F4") : "").Append(',')
                    .Append(priced ? Number(result.Offence, "F3") : "").Append(',')
                    .Append(priced ? Number(result.Toughness, "F3") : "").Append(',')
                    .Append(traits.Replace(", ", "|")).Append(',')
                    .Append(!result.Valued ? "" : result.Fixed ? "fixed" : "model").Append('\n');
                table.Append("| ").Append(stats.unitName).Append(" | ").Append(squad.assets.race).Append(" | ").Append(stats.unitType).Append(" | ")
                    .Append(stats.RarityTier).Append(" | ").Append(result.Valued ? value : "not valued").Append(" | ")
                    .Append(priced ? Number(result.Offence, "F2") : result.Fixed ? "set by hand" : "").Append(" | ")
                    .Append(priced ? Number(result.Toughness, "F2") : "").Append(" | ").Append(traits).Append(" |\n");
            }

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolder);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "unit_values.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(folder, "unit_values.md"), table.ToString());
            Debug.Log($"[Unit Value] Wrote {rows.Count} units ({rows.Count(r => r.result.Valued)} valued) to {folder}");
        }

        [MenuItem("Tabletop Tavern/Unit Value/Select Weights Asset")]
        public static void SelectWeights()
        {
            var weights = AssetDatabase.LoadAssetAtPath<UnitValueWeights>(WeightsAssetPath);
            if (weights == null)
            {
                weights = ScriptableObject.CreateInstance<UnitValueWeights>();
                AssetDatabase.CreateAsset(weights, WeightsAssetPath);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = weights;
            EditorGUIUtility.PingObject(weights);
        }

        private static string Number(float value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

        private static string Traits(SquadAttributes attributes)
        {
            var names = new List<string>();
            foreach (FieldInfo field in typeof(SquadAttributes).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (field.FieldType == typeof(bool) && field.Name != nameof(SquadAttributes.None) && (bool)field.GetValue(attributes))
                    names.Add(field.Name);
            return string.Join(", ", names);
        }
    }
}
