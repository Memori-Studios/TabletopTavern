using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TJ
{
    /// <summary>
    /// Tuning window for unit values: the weights on the left, every unit's value on the right, and
    /// below them how well the point totals pick the winner of the recorded battles. Moving a weight
    /// recalculates all three at once.
    /// </summary>
    public class UnitValueWindow : EditorWindow
    {
        #region Layout
        private const float WeightsWidth = 340f;
        private const float RowHeight = 20f;
        private const float DetailHeight = 190f;
        private static readonly string[] Columns = { "Unit", "Race", "Class", "Rarity", "Value", "Change", "Offence", "Toughness" };
        private static readonly float[] Widths = { 190f, 120f, 80f, 80f, 150f, 60f, 62f, 72f };
        private static readonly string[] ClassFilters = { "All classes", "Melee foot", "Cavalry", "Monster", "Shooter", "Hybrid", "Artillery", "Mage" };
        private static readonly string[] RarityFilters = { "All rarities", "Common", "Uncommon", "Rare", "Legendary" };
        private static readonly Color BarColor = new(0.79f, 0.64f, 0.31f);
        private static readonly Color TrackColor = new(0f, 0f, 0f, 0.25f);
        private static readonly Color SelectedColor = new(0.24f, 0.37f, 0.59f, 0.6f);
        private static readonly Color StripeColor = new(1f, 1f, 1f, 0.03f);
        private static readonly Color UpColor = new(0.45f, 0.8f, 0.5f);
        private static readonly Color DownColor = new(0.9f, 0.45f, 0.4f);
        #endregion

        // Parsing the fought battles takes a second or two, so the sets outlive the window until the next domain reload.
        private static UnitValueWorkbench.BattleSet _fought, _simulated;
        private static bool _battlesLoaded;

        private List<SquadData> _roster;
        private List<SquadStats> _stats;
        private UnitValueWeights _weights;
        private bool _weightsAreAsset;
        private Editor _weightsEditor;
        private List<UnitValueResult> _results;
        private float[] _baseline;
        private float _foughtScore, _simulatedScore, _foughtBaseline, _simulatedBaseline;
        private readonly List<int> _rows = new();
        private int _selected = -1;
        private List<(string trait, float adds)> _traits = new();
        private readonly List<(int unit, float exchange)> _matchups = new();
        private string _search = "";
        private int _classFilter, _rarityFilter, _sortColumn = 4;
        private bool _sortDescending = true;
        private Vector2 _weightsScroll, _tableScroll, _detailScroll;

        [MenuItem("Tabletop Tavern/Unit Value/Tuning Window")]
        public static void Open()
        {
            var window = GetWindow<UnitValueWindow>("Unit Value");
            window.minSize = new Vector2(1000f, 520f);
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            Load();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (_weightsEditor != null) DestroyImmediate(_weightsEditor);
            if (_weights != null && !_weightsAreAsset) DestroyImmediate(_weights);
        }

        private void OnUndoRedo()
        {
            Recalculate(false);
            Repaint();
        }

        #region Data
        private void Load()
        {
            _roster = UnitValueWorkbench.LoadRoster();
            _stats = _roster.Select(squad => squad.stats).ToList();
            if (_weights != null && !_weightsAreAsset) DestroyImmediate(_weights);
            _weights = Resources.Load<UnitValueWeights>(UnitValueWeights.ResourcePath);
            _weightsAreAsset = _weights != null;
            if (!_weightsAreAsset)
            {
                _weights = CreateInstance<UnitValueWeights>();
                _weights.hideFlags = HideFlags.HideAndDontSave;
            }
            Editor.CreateCachedEditor(_weights, null, ref _weightsEditor);
            if (!_battlesLoaded) LoadBattles();
            Recalculate(true);
        }

        private void LoadBattles()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Unit Value", "Reading the fought battles", 0.2f);
                _fought = UnitValueWorkbench.BattleSet.Load(Path.Combine(UnitValueWorkbench.OutputPath, UnitValueWorkbench.FoughtFile), _stats);
                EditorUtility.DisplayProgressBar("Unit Value", "Reading the simulated armies", 0.7f);
                _simulated = UnitValueWorkbench.BattleSet.Load(Path.Combine(UnitValueWorkbench.OutputPath, UnitValueWorkbench.SimulatedFile), _stats);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            _battlesLoaded = true;
        }

        private void Recalculate(bool setBaseline)
        {
            _results = UnitValueModel.ValueAll(_stats, _weights);
            float[] values = UnitValueWorkbench.ByOrdinal(_results);
            _foughtScore = _fought != null ? _fought.Score(values) : 0f;
            _simulatedScore = _simulated != null ? _simulated.Score(values) : 0f;
            if (setBaseline || _baseline == null || _baseline.Length != _results.Count)
            {
                _baseline = _results.Select(result => result.Value).ToArray();
                _foughtBaseline = _foughtScore;
                _simulatedBaseline = _simulatedScore;
            }
            RefreshRows();
            RefreshDetail();
        }

        private void RefreshRows()
        {
            _rows.Clear();
            for (int i = 0; i < _results.Count; i++)
            {
                SquadStats stats = _stats[i];
                if (_search.Length > 0 && stats.unitName.ToString().IndexOf(_search.Replace(" ", ""), System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (_classFilter > 0 && UnitValueWorkbench.ClassOf(stats) != ClassFilters[_classFilter]) continue;
                if (_rarityFilter > 0 && (int)stats.RarityTier != _rarityFilter - 1) continue;
                _rows.Add(i);
            }
            _rows.Sort((a, b) =>
            {
                // Units the model does not price always sit at the bottom.
                if (_results[a].Valued != _results[b].Valued) return _results[a].Valued ? -1 : 1;
                int order = _sortColumn switch
                {
                    0 => string.CompareOrdinal(_stats[a].unitName.ToString(), _stats[b].unitName.ToString()),
                    1 => string.CompareOrdinal(_roster[a].assets.race.ToString(), _roster[b].assets.race.ToString()),
                    2 => string.CompareOrdinal(UnitValueWorkbench.ClassOf(_stats[a]), UnitValueWorkbench.ClassOf(_stats[b])),
                    3 => _stats[a].RarityTier.CompareTo(_stats[b].RarityTier),
                    5 => (_results[a].Value - _baseline[a]).CompareTo(_results[b].Value - _baseline[b]),
                    6 => _results[a].Offence.CompareTo(_results[b].Offence),
                    7 => _results[a].Toughness.CompareTo(_results[b].Toughness),
                    _ => _results[a].Value.CompareTo(_results[b].Value),
                };
                return _sortDescending ? -order : order;
            });
        }

        private void RefreshDetail()
        {
            _traits.Clear();
            _matchups.Clear();
            if (_selected < 0 || _selected >= _results.Count || !_results[_selected].Valued || _results[_selected].Fixed) return;
            _traits = UnitValueWorkbench.TraitContributions(_stats, _results, _weights, _selected);
            float[] exchanges = UnitValueModel.Exchanges(_stats, _weights, _selected);
            for (int i = 0; i < exchanges.Length; i++)
                if (exchanges[i] > 0f) _matchups.Add((i, exchanges[i]));
            _matchups.Sort((a, b) => b.exchange.CompareTo(a.exchange));
        }
        #endregion

        #region Drawing
        private void OnGUI()
        {
            if (_results == null) Load();
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawWeights();
            EditorGUILayout.BeginVertical();
            DrawTable();
            if (_selected >= 0) DrawDetail();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            DrawScores();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(200f));
            _classFilter = EditorGUILayout.Popup(_classFilter, ClassFilters, EditorStyles.toolbarPopup, GUILayout.Width(110f));
            _rarityFilter = EditorGUILayout.Popup(_rarityFilter, RarityFilters, EditorStyles.toolbarPopup, GUILayout.Width(110f));
            if (EditorGUI.EndChangeCheck()) RefreshRows();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Set Baseline", "The Change column and the score comparison measure from here."), EditorStyles.toolbarButton)) Recalculate(true);
            if (GUILayout.Button(new GUIContent("Write Report", "Writes every unit's value to Tools/UnitValue/Output."), EditorStyles.toolbarButton)) UnitValueReport.Write();
            if (GUILayout.Button(new GUIContent("Reload", "Reads the unit data and the recorded battles again."), EditorStyles.toolbarButton))
            {
                _battlesLoaded = false;
                Load();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawWeights()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(WeightsWidth));
            _weightsScroll = EditorGUILayout.BeginScrollView(_weightsScroll);
            if (!_weightsAreAsset)
            {
                EditorGUILayout.HelpBox("There is no weights asset yet, so these are the code defaults. Changes here are lost when the window closes. Create the asset to keep them.", MessageType.Info);
                if (GUILayout.Button("Create Weights Asset"))
                {
                    UnitValueReport.SelectWeights();
                    Load();
                    GUIUtility.ExitGUI();
                }
            }
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 170f;
            EditorGUI.BeginChangeCheck();
            _weightsEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck()) Recalculate(false);
            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUILayout.Space();
            if (GUILayout.Button(new GUIContent("Reset To Code Defaults", "Puts every weight back to the tuned defaults in UnitValueWeights.cs.")))
            {
                UnitValueWeights defaults = CreateInstance<UnitValueWeights>();
                Undo.RecordObject(_weights, "Reset Unit Value Weights");
                string name = _weights.name;
                EditorUtility.CopySerialized(defaults, _weights);
                _weights.name = name;
                DestroyImmediate(defaults);
                if (_weightsAreAsset) EditorUtility.SetDirty(_weights);
                Recalculate(false);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawTable()
        {
            Rect header = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
            float x = header.x;
            for (int column = 0; column < Columns.Length; column++)
            {
                var cell = new Rect(x, header.y, Widths[column], header.height);
                string label = Columns[column] + (column == _sortColumn ? (_sortDescending ? " ▼" : " ▲") : "");
                if (GUI.Button(cell, label, EditorStyles.toolbarButton))
                {
                    if (_sortColumn == column) _sortDescending = !_sortDescending;
                    else { _sortColumn = column; _sortDescending = column >= 4; }
                    RefreshRows();
                }
                x += Widths[column];
            }

            _tableScroll = EditorGUILayout.BeginScrollView(_tableScroll);
            Rect area = GUILayoutUtility.GetRect(Widths.Sum(), _rows.Count * RowHeight);
            int first = Mathf.Max(0, Mathf.FloorToInt(_tableScroll.y / RowHeight) - 1);
            int last = Mathf.Min(_rows.Count, first + Mathf.CeilToInt(position.height / RowHeight) + 2);
            for (int row = first; row < last; row++)
            {
                int unit = _rows[row];
                var line = new Rect(area.x, area.y + row * RowHeight, Mathf.Max(area.width, Widths.Sum()), RowHeight);
                if (unit == _selected) EditorGUI.DrawRect(line, SelectedColor);
                else if (row % 2 == 1) EditorGUI.DrawRect(line, StripeColor);
                // TJ.Event is a namespace, so the IMGUI event needs its full name here.
                UnityEngine.Event current = UnityEngine.Event.current;
                if (current.type == EventType.MouseDown && line.Contains(current.mousePosition))
                {
                    _selected = unit;
                    RefreshDetail();
                    current.Use();
                    Repaint();
                }
                DrawRow(line, unit);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(Rect line, int unit)
        {
            SquadStats stats = _stats[unit];
            UnitValueResult result = _results[unit];
            float x = line.x + 4f;
            GUI.Label(new Rect(x, line.y, Widths[0] - 4f, line.height), ObjectNames.NicifyVariableName(stats.unitName.ToString())); x = line.x + Widths[0];
            GUI.Label(new Rect(x, line.y, Widths[1], line.height), ObjectNames.NicifyVariableName(_roster[unit].assets.race.ToString())); x += Widths[1];
            GUI.Label(new Rect(x, line.y, Widths[2], line.height), UnitValueWorkbench.ClassOf(stats)); x += Widths[2];
            GUI.Label(new Rect(x, line.y, Widths[3], line.height), stats.RarityTier.ToString()); x += Widths[3];
            if (!result.Valued)
            {
                GUI.Label(new Rect(x, line.y, Widths[4] + Widths[5], line.height), "not valued yet", EditorStyles.miniLabel);
                return;
            }
            var track = new Rect(x, line.y + 6f, 96f, 8f);
            EditorGUI.DrawRect(track, TrackColor);
            EditorGUI.DrawRect(new Rect(track.x, track.y, track.width * Mathf.Clamp01(result.Value / 100f), track.height), BarColor);
            GUI.Label(new Rect(x + 102f, line.y, Widths[4] - 102f, line.height), result.Value.ToString("F1")); x += Widths[4];
            float change = result.Value - _baseline[unit];
            if (Mathf.Abs(change) >= 0.05f)
            {
                Color previous = GUI.contentColor;
                GUI.contentColor = change > 0f ? UpColor : DownColor;
                GUI.Label(new Rect(x, line.y, Widths[5], line.height), change.ToString("+0.0;-0.0"));
                GUI.contentColor = previous;
            }
            x += Widths[5];
            if (result.Fixed)
            {
                GUI.Label(new Rect(x, line.y, Widths[6] + Widths[7], line.height), "set by hand", EditorStyles.miniLabel);
                return;
            }
            GUI.Label(new Rect(x, line.y, Widths[6], line.height), result.Offence.ToString("F2")); x += Widths[6];
            GUI.Label(new Rect(x, line.y, Widths[7], line.height), result.Toughness.ToString("F2"));
        }

        private void DrawDetail()
        {
            SquadStats stats = _stats[_selected];
            UnitValueResult result = _results[_selected];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(DetailHeight));
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(stats.unitName.ToString()) + (result.Valued ? $"   {result.Value:F1}" : ""), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"{stats.baseUnitCount} models x {stats.HitPointsPerUnit} health   Attack {stats.MeleeAttack}   Defence {stats.MeleeDefense}   Weapon {stats.WeaponStrength}   " +
                $"Armour {stats.Armor}   Leadership {stats.Leadership:0}   Speed {stats.Speed:0}" +
                (stats.MissileStrength > 0 ? $"   Missile {stats.MissileStrength}   Range {stats.BaseRange:0}   Accuracy {stats.attackAccuracy:0}   Ammo {stats.Ammunition}" : ""),
                EditorStyles.miniLabel);
            if (!result.Valued)
            {
                EditorGUILayout.LabelField("The model does not price this unit yet.");
            }
            else if (result.Fixed)
            {
                EditorGUILayout.LabelField("The model cannot price this unit, so its value is set by hand: Fixed Values, under Hand corrections.");
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.Width(250f));
                EditorGUILayout.LabelField("What each trait adds", EditorStyles.miniBoldLabel);
                if (_traits.Count == 0) EditorGUILayout.LabelField("No traits.", EditorStyles.miniLabel);
                foreach ((string trait, float adds) in _traits)
                    EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(trait), adds.ToString("+0.0;-0.0;0.0"));
                EditorGUILayout.EndVertical();
                DrawMatchups("Best trades", 0, 1);
                DrawMatchups("Worst trades", _matchups.Count - 1, -1);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawMatchups(string title, int start, int step)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250f));
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            for (int shown = 0, i = start; shown < 5 && i >= 0 && i < _matchups.Count; shown++, i += step)
            {
                (int unit, float exchange) = _matchups[i];
                string odds = exchange >= 1f ? $"{exchange:0.0} to 1" : $"1 to {1f / exchange:0.0}";
                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(_stats[unit].unitName.ToString()), odds);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawScores()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            DrawScore("Fought battles", _fought, _foughtScore, _foughtBaseline, UnitValueWorkbench.FoughtFile);
            DrawScore("Simulated armies", _simulated, _simulatedScore, _simulatedBaseline, UnitValueWorkbench.SimulatedFile);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawScore(string label, UnitValueWorkbench.BattleSet set, float score, float baseline, string file)
        {
            if (set == null)
            {
                EditorGUILayout.LabelField($"{label}: no data ({UnitValueWorkbench.OutputFolder}/{file} is missing)", EditorStyles.miniLabel);
                return;
            }
            float change = (score - baseline) * 100f;
            Color previous = GUI.contentColor;
            if (Mathf.Abs(change) >= 0.05f) GUI.contentColor = change > 0f ? UpColor : DownColor;
            string moved = Mathf.Abs(change) >= 0.05f ? $"  ({change:+0.0;-0.0} against the baseline)" : "";
            EditorGUILayout.LabelField(new GUIContent($"{label}: point totals pick the winner {score * 100f:F1}%{moved}",
                $"{set.Battles:N0} battles. Take one won and one lost battle at random: how often the won one has the better point ratio. 50% is a coin flip."));
            GUI.contentColor = previous;
        }
        #endregion
    }
}
