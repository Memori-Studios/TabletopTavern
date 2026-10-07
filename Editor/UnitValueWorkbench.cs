using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TJ
{
    /// <summary>The data behind the Unit Value tuning window: the roster, the recorded battles that score a set of values, and what each trait adds.</summary>
    public static class UnitValueWorkbench
    {
        public const string OutputFolder = "Tools/UnitValue/Output";
        public const string FoughtFile = "live_battles.csv";
        public const string SimulatedFile = "sim_armies.csv";

        public static string OutputPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolder);

        /// <summary>Every SquadData asset once, in enum order.</summary>
        public static List<SquadData> LoadRoster()
        {
            var roster = new List<SquadData>();
            var seen = new HashSet<UnitName>();
            foreach (SquadData squad in Resources.LoadAll<SquadData>("SquadData").OrderBy(s => (int)s.stats.unitName))
                if (seen.Add(squad.stats.unitName)) roster.Add(squad);
            return roster;
        }

        public static string ClassOf(SquadStats stats) => stats.unitType switch
        {
            UnitType.Ranged => "Shooter",
            UnitType.Hybrid => "Hybrid",
            UnitType.Artillery => "Artillery",
            UnitType.Mage => "Mage",
            UnitType.Structure => "Structure",
            _ => stats.unitSize == UnitSize.Cavalry ? "Cavalry"
               : stats.unitSize == UnitSize.Monstrous || stats.unitSize == UnitSize.SingleUnit ? "Monster" : "Melee foot",
        };

        #region Recorded battles
        /// <summary>A file of battles with both army lists and who won. Scores a set of values by how often their totals pick the winner.</summary>
        public sealed class BattleSet
        {
            public int Battles;
            // Squads of battle b on one side sit at [start[b], start[b + 1]) in the unit and share arrays.
            private int[] _playerStart, _enemyStart, _playerUnit, _enemyUnit;
            // Models brought over the unit's base model count: an under-strength squad counts for less.
            private float[] _playerShare, _enemyShare;
            private bool[] _won;

            /// <summary>
            /// Rows as the analytics pull and the battle check write them: id, build, act, difficulty, fought, garrison, node,
            /// hero, result, spell_damage, then each army as unit:n0:n1:prestige:damage:kills:trait joined by semicolons.
            /// Garrison battles and battles with a unit that is not in the roster are left out.
            /// </summary>
            public static BattleSet Load(string path, IReadOnlyList<SquadStats> roster)
            {
                if (!File.Exists(path)) return null;
                var ordinal = new Dictionary<string, int>();
                var baseCount = new Dictionary<string, int>();
                foreach (SquadStats stats in roster)
                {
                    ordinal[stats.unitName.ToString()] = (int)stats.unitName;
                    baseCount[stats.unitName.ToString()] = Mathf.Max(1, stats.baseUnitCount);
                }

                var playerStart = new List<int> { 0 }; var enemyStart = new List<int> { 0 };
                var playerUnit = new List<int>(); var enemyUnit = new List<int>();
                var playerShare = new List<float>(); var enemyShare = new List<float>();
                var won = new List<bool>();

                // Appends one army's squads; false when a unit is not in the roster or the army is empty. Walks the text in place, since the fought file is 30 MB.
                bool Parse(string army, List<int> units, List<float> shares)
                {
                    int before = units.Count, start = 0;
                    while (start < army.Length)
                    {
                        int end = army.IndexOf(';', start);
                        if (end < 0) end = army.Length;
                        int colon = army.IndexOf(':', start, end - start);
                        if (colon < 0) return false;
                        string name = army.Substring(start, colon - start);
                        if (!ordinal.TryGetValue(name, out int unit)) return false;
                        int models = 0;
                        for (int i = colon + 1; i < end && army[i] != ':'; i++) models = models * 10 + (army[i] - '0');
                        units.Add(unit); shares.Add(models / (float)baseCount[name]);
                        start = end + 1;
                    }
                    return units.Count > before;
                }

                using var reader = new StreamReader(path);
                reader.ReadLine();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] cells = line.Split(',');
                    if (cells.Length < 12 || cells[5] == "t") continue;
                    int playerBefore = playerUnit.Count, enemyBefore = enemyUnit.Count;
                    if (!Parse(cells[10], playerUnit, playerShare) || !Parse(cells[11], enemyUnit, enemyShare))
                    {
                        // Drop whatever the rejected battle already appended.
                        playerUnit.RemoveRange(playerBefore, playerUnit.Count - playerBefore); playerShare.RemoveRange(playerBefore, playerShare.Count - playerBefore);
                        enemyUnit.RemoveRange(enemyBefore, enemyUnit.Count - enemyBefore); enemyShare.RemoveRange(enemyBefore, enemyShare.Count - enemyBefore);
                        continue;
                    }
                    playerStart.Add(playerUnit.Count); enemyStart.Add(enemyUnit.Count);
                    won.Add(cells[8] == "Win");
                }
                return new BattleSet
                {
                    Battles = won.Count, _won = won.ToArray(),
                    _playerStart = playerStart.ToArray(), _enemyStart = enemyStart.ToArray(),
                    _playerUnit = playerUnit.ToArray(), _enemyUnit = enemyUnit.ToArray(),
                    _playerShare = playerShare.ToArray(), _enemyShare = enemyShare.ToArray(),
                };
            }

            /// <summary>Take one won and one lost battle at random: how often the won one has the better point ratio. 0.5 is a coin flip.</summary>
            public float Score(float[] valueByOrdinal)
            {
                var ratio = new float[Battles];
                for (int b = 0; b < Battles; b++)
                    ratio[b] = Mathf.Log(Total(_playerUnit, _playerShare, _playerStart, b, valueByOrdinal))
                             - Mathf.Log(Total(_enemyUnit, _enemyShare, _enemyStart, b, valueByOrdinal));
                var won = (bool[])_won.Clone();
                Array.Sort(ratio, won);
                double rankSum = 0; long wins = 0;
                for (int i = 0; i < Battles; i++)
                    if (won[i]) { rankSum += i + 1; wins++; }
                long losses = Battles - wins;
                if (wins == 0 || losses == 0) return 0.5f;
                return (float)((rankSum - wins * (wins + 1) / 2.0) / ((double)wins * losses));
            }

            private static float Total(int[] unit, float[] share, int[] start, int battle, float[] valueByOrdinal)
            {
                float total = 0f;
                for (int i = start[battle]; i < start[battle + 1]; i++)
                    if (unit[i] < valueByOrdinal.Length) total += valueByOrdinal[unit[i]] * share[i];
                return Mathf.Max(total, 1e-9f);
            }
        }

        /// <summary>Values indexed by UnitName ordinal, zero for units the model does not price.</summary>
        public static float[] ByOrdinal(IReadOnlyList<UnitValueResult> results)
        {
            int size = 0;
            foreach (UnitValueResult result in results) size = Mathf.Max(size, (int)result.Unit + 1);
            var values = new float[size];
            foreach (UnitValueResult result in results)
                if (result.Valued) values[(int)result.Unit] = result.Value;
            return values;
        }
        #endregion

        #region One unit in detail
        /// <summary>What each of a unit's traits adds to its value, on the current scale, largest first.</summary>
        public static List<(string trait, float adds)> TraitContributions(IReadOnlyList<SquadStats> roster, IReadOnlyList<UnitValueResult> results, UnitValueWeights weights, int unit)
        {
            var contributions = new List<(string, float)>();
            if (!results[unit].Valued || results[unit].Fixed) return contributions;
            float top = 0f;
            foreach (UnitValueResult result in results) top = Mathf.Max(top, result.Power);
            if (weights.referencePower > 0f) top = weights.referencePower;
            foreach (FieldInfo field in typeof(SquadAttributes).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(bool) || field.Name == nameof(SquadAttributes.None)) continue;
                if (!(bool)field.GetValue(roster[unit].SquadAttributes)) continue;
                var without = new List<SquadStats>(roster);
                SquadStats stats = without[unit];
                object traits = stats.SquadAttributes;
                field.SetValue(traits, false);
                stats.SquadAttributes = (SquadAttributes)traits;
                without[unit] = stats;
                // Measured against today's top unit, so removing a trait from the top unit still shows a drop.
                float valueWithout = 100f * Mathf.Pow(UnitValueModel.ValueAll(without, weights)[unit].Power / top, weights.crowding);
                contributions.Add((field.Name, results[unit].Value - valueWithout));
            }
            contributions.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return contributions;
        }

        /// <summary>The traits switched on for a unit, in declaration order.</summary>
        public static List<string> Traits(SquadAttributes attributes)
        {
            var names = new List<string>();
            foreach (FieldInfo field in typeof(SquadAttributes).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (field.FieldType == typeof(bool) && field.Name != nameof(SquadAttributes.None) && (bool)field.GetValue(attributes))
                    names.Add(field.Name);
            return names;
        }
        #endregion

        /// <summary>The two battle scores for the saved weights, as text. For checking the workbench against the analysis scripts.</summary>
        public static string Describe()
        {
            List<SquadStats> roster = LoadRoster().Select(s => s.stats).ToList();
            UnitValueWeights weights = UnitValueWeights.LoadOrDefault();
            List<UnitValueResult> results = UnitValueModel.ValueAll(roster, weights);
            float[] values = ByOrdinal(results);
            BattleSet fought = BattleSet.Load(Path.Combine(OutputPath, FoughtFile), roster);
            BattleSet simulated = BattleSet.Load(Path.Combine(OutputPath, SimulatedFile), roster);
            if (!UnityEditor.AssetDatabase.Contains(weights)) UnityEngine.Object.DestroyImmediate(weights);
            return $"fought {(fought == null ? "missing" : $"{fought.Battles} battles, {100f * fought.Score(values):F2}%")}; "
                 + $"simulated {(simulated == null ? "missing" : $"{simulated.Battles} battles, {100f * simulated.Score(values):F2}%")}";
        }
    }
}
