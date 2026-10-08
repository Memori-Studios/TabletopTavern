using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TJ
{
    /// <summary>
    /// Shows which unit stats differ between the SquadData assets and the server, and pushes the assets' values as
    /// a new revision. Every player takes a pushed revision at their next launch.
    /// </summary>
    public class RemoteBalanceWindow : EditorWindow
    {
        #region Layout
        private static readonly string[] Columns = { "Unit", "Stat", "Server", "Editor" };
        private static readonly float[] Widths = { 220f, 180f, 110f, 110f };
        private const float RowHeight = 20f;
        private static readonly Color StripeColor = new(1f, 1f, 1f, 0.03f);
        #endregion

        private RemoteBalanceEditor.ServerRevision _server;
        private SquadStatsOverrideFile _local;
        private List<RemoteBalanceEditor.Difference> _differences = new();
        private string _status = "Press Refresh to compare with the server.";
        private MessageType _statusType = MessageType.Info;
        private string _note = "";
        private string _minBuild = "";
        private bool _confirming;
        private bool _reading;
        private Vector2 _scroll;

        [MenuItem("Tabletop Tavern/Balance/Remote Balance")]
        public static void Open()
        {
            var window = GetWindow<RemoteBalanceWindow>("Remote Balance");
            window.minSize = new Vector2(680f, 420f);
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(RemoteBalanceEditor.AdminKey)) Refresh();
        }

        // Does not block, so opening the window never freezes the Editor; the pushed line stays above the new status.
        private void Refresh(string pushed = null)
        {
            _confirming = false;
            _reading = true;
            _status = (pushed == null ? "" : pushed + "\n") + "Reading the server's newest unit stats...";
            _statusType = MessageType.Info;
            RemoteBalanceEditor.FetchLatestAsync((server, error) =>
            {
                // The window may have closed while the server answered.
                if (this == null) return;
                _reading = false;
                ShowAnswer(server, error);
                if (pushed != null) _status = $"{pushed}\n{_status}";
                Repaint();
            });
        }

        private void ShowAnswer(RemoteBalanceEditor.ServerRevision server, string error)
        {
            _local = RemoteBalanceEditor.BuildLocal();
            _server = server;
            if (_server == null)
            {
                _differences.Clear();
                _status = error;
                _statusType = MessageType.Error;
                return;
            }
            _differences = RemoteBalanceEditor.Compare(_server.units, _local);
            _statusType = _differences.Count == 0 ? MessageType.Info : MessageType.Warning;
            if (_server.rev == 0)
            {
                _status = $"Nothing has been pushed yet. The first push sends all {_local.overrides.Count} units.";
                _statusType = MessageType.Warning;
            }
            else if (_differences.Count == 0)
            {
                _status = "In sync with the server.";
                if (RemoteBalanceEditor.BakedRevision != _server.rev) RemoteBalanceEditor.WriteBaked(_server.rev);
            }
            else
            {
                int units = _differences.Select(d => d.Unit).Distinct().Count();
                _status = $"{_differences.Count} value(s) differ in {units} unit(s). Players have the Server column until you push.";
            }
            if (!_server.enabled) _status += "\nBALANCE_ENABLED is off on the server: every player is on their build's own stats.";
        }

        private void OnGUI()
        {
            DrawSetup();
            EditorGUILayout.Space(6f);
            DrawServer();
            EditorGUILayout.HelpBox(_status, _statusType);
            DrawDifferences();
            DrawPush();
        }

        private void DrawSetup()
        {
            EditorGUI.BeginChangeCheck();
            string key = EditorGUILayout.PasswordField(new GUIContent("Push key", "From ./balance-setup.sh on the droplet. Saved on this PC only."), RemoteBalanceEditor.AdminKey);
            if (EditorGUI.EndChangeCheck()) RemoteBalanceEditor.AdminKey = key.Trim();

            EditorGUI.BeginChangeCheck();
            bool apply = EditorGUILayout.ToggleLeft(
                new GUIContent("Read the server's stats in Play Mode (to test what players get)", "Off, Play Mode uses the assets as they are."),
                EditorPrefs.GetBool(RemoteBalance.ApplyInEditorPref, false));
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(RemoteBalance.ApplyInEditorPref, apply);
        }

        private void DrawServer()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string summary = _server == null ? "Server: not read yet"
                    : _server.rev == 0 ? "Server: no revision yet"
                    : $"Server: revision {_server.rev}, pushed {_server.pushedAt}" +
                      (string.IsNullOrEmpty(_server.minBuild) ? "" : $", for build {_server.minBuild} and later") +
                      (string.IsNullOrEmpty(_server.note) ? "" : $"  \"{_server.note}\"");
                EditorGUILayout.LabelField(summary, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(_reading))
                {
                    if (GUILayout.Button(_reading ? "Reading..." : "Refresh", GUILayout.Width(80f))) Refresh();
                }
            }
            EditorGUILayout.LabelField($"This project is stamped with revision {RemoteBalanceEditor.BakedRevision}.", EditorStyles.miniLabel);
        }

        private void DrawDifferences()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                for (int i = 0; i < Columns.Length; i++) GUILayout.Label(Columns[i], EditorStyles.toolbarButton, GUILayout.Width(Widths[i]));
                GUILayout.FlexibleSpace();
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            for (int row = 0; row < _differences.Count; row++)
            {
                RemoteBalanceEditor.Difference d = _differences[row];
                Rect rect = EditorGUILayout.GetControlRect(false, RowHeight);
                if (row % 2 == 1) EditorGUI.DrawRect(rect, StripeColor);
                float x = rect.x;
                string[] cells = { d.Unit, d.Stat, d.Server, d.Local };
                for (int i = 0; i < cells.Length; i++)
                {
                    GUI.Label(new Rect(x, rect.y, Widths[i], rect.height), cells[i], i == 3 ? EditorStyles.boldLabel : EditorStyles.label);
                    x += Widths[i];
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawPush()
        {
            _note = EditorGUILayout.TextField(new GUIContent("Note", "Why you changed it. Shown in the balance_revisions table."), _note);
            using (new EditorGUILayout.HorizontalScope())
            {
                _minBuild = EditorGUILayout.TextField(
                    new GUIContent("Minimum build", "Empty: every build takes this revision. A version: only that build and later, for stats tuned for a build not yet uploaded."),
                    _minBuild);
                if (GUILayout.Button($"This build ({Application.version})", GUILayout.Width(150f))) _minBuild = Application.version;
                if (GUILayout.Button("Every build", GUILayout.Width(90f))) _minBuild = "";
            }

            bool canPush = !_reading && _server != null && _local != null && (_differences.Count > 0 || _server.rev == 0);
            using (new EditorGUI.DisabledScope(!canPush))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!_confirming)
                {
                    if (GUILayout.Button("Push to players...", GUILayout.Height(28f))) _confirming = true;
                }
                else
                {
                    string who = string.IsNullOrEmpty(_minBuild) ? "every player" : $"players on {_minBuild} and later";
                    string what = _server != null && _server.rev == 0 ? $"all {_local.overrides.Count} units" : $"{_differences.Count} change(s)";
                    if (GUILayout.Button($"Confirm: send {what} to {who}", GUILayout.Height(28f))) Push();
                    if (GUILayout.Button("Cancel", GUILayout.Height(28f), GUILayout.Width(80f))) _confirming = false;
                }
            }
        }

        private void Push()
        {
            _confirming = false;
            // Rebuilt at the moment of the push, so an asset edited since the last Refresh is not left behind.
            _local = RemoteBalanceEditor.BuildLocal();
            int rev = RemoteBalanceEditor.Push(_local, _note.Trim(), _minBuild.Trim(), out int changes, out string error);
            if (rev == 0)
            {
                _status = $"Push failed. Nothing changed for players. {error}";
                _statusType = MessageType.Error;
                return;
            }
            Debug.Log($"[RemoteBalance] Pushed revision {rev} ({changes} value(s) changed). Players take it at their next launch.");
            _note = "";
            Refresh($"Pushed revision {rev} ({changes} value(s) changed). Players take it at their next launch.");
        }
    }
}
