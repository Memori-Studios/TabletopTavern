#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

namespace TJ.Engagement
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(AutoResolveBattleManager))]
    [RequireComponent(typeof(AutoResolveBattleManager))]
    public class AutoResolveBattleEditor : Editor
    {
        public override void OnInspectorGUI() {
            AutoResolveBattleManager autoResolveBattle = (AutoResolveBattleManager)target;

            if (GUILayout.Button("Set Up Armies Test")) {
                ConsoleUtility.ClearConsole();
                autoResolveBattle.SetUpArmiesTest();
            }
            if (GUILayout.Button("Auto Resolve Battle")) {
                ConsoleUtility.ClearConsole();
                autoResolveBattle.AutoResolveThroughEditor();
            }
            if (GUILayout.Button("Run Simulation Loop")) {
                ConsoleUtility.ClearConsole();
                autoResolveBattle.RunSimulationLoop();
            }
            // Needs Play Mode: TabletopTavernData only fills its dictionaries at Awake.
            if (GUILayout.Button("Test Mage Auto-Resolve")) {
                ConsoleUtility.ClearConsole();
                MageAutoResolveTests.RunFromMenu();
            }

            if (DrawDefaultInspector()) {
            }
        }
    }
}
#endif