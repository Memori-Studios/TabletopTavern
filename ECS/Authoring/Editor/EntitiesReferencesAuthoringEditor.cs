#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EntitiesReferencesAuthoring))]
public class EntitiesReferencesAuthoringEditor : Editor
{
    private bool showGeneral = true;
    // private bool showIronLegion = false;
    // private bool showGreenTide = false;
    // private bool showRavenhost = false;
    // private bool showTaelindor = false;
    // private bool showSanguine = false;
    // private bool showSakura = false;
    // private bool showDeepstone = false;
    // private bool showSpecial = false;
    private bool showProjectiles = true;
    // private bool showDrakosaurBrood = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(10);

        // General / Misc Section
        showGeneral = EditorGUILayout.Foldout(showGeneral, "General References", true);
        if (showGeneral)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("debugEntityPrefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("basePlayerUnitPrefabGameObject"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("arrowImpactPrefabGameObject"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("debugPlayerUnitPositionPrefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("debugEnemyUnitPositionPrefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("battlefieldBonusPrefabGameObject"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gateUnitPrefabGameObject"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(5);

        // Projectiles
        showProjectiles = EditorGUILayout.Foldout(showProjectiles, "Projectiles", true);
        if (showProjectiles) DrawFactionFields("arrowPrefabGameObject", "flamingArrowPrefabEntity");

        // // Iron Legion
        // showIronLegion = EditorGUILayout.Foldout(showIronLegion, "Iron Legion GPU Anims", true);
        // if (showIronLegion) DrawFactionFields("peasantBowmenGPUAnim1", "royalCavaliersRiderPrefabGameObject");

        // // Great Gruntkin Rush
        // showGreenTide = EditorGUILayout.Foldout(showGreenTide, "Great Gruntkin Rush GPU Anims", true);
        // if (showGreenTide) DrawFactionFields("goblinGPUAnim1", "direridersRiderPrefabGameObject");

        // // Ravenhost
        // showRavenhost = EditorGUILayout.Foldout(showRavenhost, "Ravenhost GPU Anims", true);
        // if (showRavenhost) DrawFactionFields("thrallLevyGPUAnim1", "deathAngelsRiderPrefabGameObject");

        // // Taelindor Forest
        // showTaelindor = EditorGUILayout.Foldout(showTaelindor, "Taelindor Forest GPU Anims", true);
        // if (showTaelindor) DrawFactionFields("SylvanArchersGPUAnim1", "veilkinDrakeRiderRiderPrefabGameObject");

        // // Sanguine Court
        // showSanguine = EditorGUILayout.Foldout(showSanguine, "Sanguine Court GPU Anims", true);
        // if (showSanguine) DrawFactionFields("UndeadLeviesGPUAnim1", "bloodswornKnightsRiderPrefabGameObject");

        // // Sakura Dynasty
        // showSakura = EditorGUILayout.Foldout(showSakura, "Sakura Dynasty GPU Anims", true);
        // if (showSakura) DrawFactionFields("RicePaddyConscriptsGPUAnim1", "daimyoKnightsRiderPrefabGameObject");

        // // Deepstone Hold
        // showDeepstone = EditorGUILayout.Foldout(showDeepstone, "Deepstone Hold GPU Anims", true);
        // if (showDeepstone) DrawFactionFields("RiftpickLaborersGPUAnim1", "ThunderhoofChargersRiderPrefabGameObject");

        // // Drakosaur Brood
        // showDrakosaurBrood = EditorGUILayout.Foldout(showDrakosaurBrood, "Drakosaur Brood GPU Anims", true);
        // if (showDrakosaurBrood) DrawFactionFields("koboldTrappersGPUAnim1", "obsidianScalesGPUAnim3");

        // // Special
        // showSpecial = EditorGUILayout.Foldout(showSpecial, "Special GPU Anims", true);
        // if (showSpecial)
        // {
        //     EditorGUI.indentLevel++;
        //     EditorGUILayout.PropertyField(serializedObject.FindProperty("ArchersOfApolloGPUAnim1"));
        //     EditorGUILayout.PropertyField(serializedObject.FindProperty("ArchersOfApolloGPUAnim2"));
        //     EditorGUILayout.PropertyField(serializedObject.FindProperty("ArchersOfApolloGPUAnim3"));
        //     EditorGUI.indentLevel--;
        // }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFactionFields(string startFieldName, string endFieldName)
    {
        EditorGUI.indentLevel++;

        SerializedProperty property = serializedObject.FindProperty(startFieldName);
        string currentField = startFieldName;

        while (property != null && currentField != endFieldName)
        {
            EditorGUILayout.PropertyField(property);
            property.NextVisible(false);
            if (property == null) break;
            currentField = property.name;
        }

        // Draw the last field
        EditorGUILayout.PropertyField(serializedObject.FindProperty(endFieldName));

        EditorGUI.indentLevel--;
    }
}
#endif