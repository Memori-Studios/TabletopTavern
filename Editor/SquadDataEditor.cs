#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SquadData))]
public class SquadDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty statsProp = serializedObject.FindProperty("stats");
        SerializedProperty assetsProp = serializedObject.FindProperty("assets");

        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("unitName"), new GUIContent("Unit Name"));

        // Draw Battle Stats header
        EditorGUILayout.LabelField("Battle Stats", EditorStyles.boldLabel);

        // Indent the stats fields
        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("unitType"), new GUIContent("Unit Type"));
        // Conditional Ranged fields
        UnitType currentType = (UnitType)statsProp.FindPropertyRelative("unitType").enumValueIndex;
        if (currentType != UnitType.Melee) // Assuming UnitType enum has a 'Ranged' value
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("BaseRange"), new GUIContent("Base Range"));
            EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("attackAccuracy"), new GUIContent("Attack Accuracy"));
            EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("MissileStrength"), new GUIContent("Missile Strength"));
            EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("Ammunition"), new GUIContent("Ammunition"));
            EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("rateOfFire"), new GUIContent("Rate of Fire"));
            EditorGUI.indentLevel--;
        }
        if (currentType == UnitType.Artillery) // Assuming UnitType enum has a 'Ranged' value
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("ExplosionDamage"), new GUIContent("Explosion Damage"));
            EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("ExplosionRange"), new GUIContent("Explosion Range"));
            EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("ExplosionForce"), new GUIContent("Explosion Force"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("unitSize"), new GUIContent("Unit Size"));
        UnitSize currentSize = (UnitSize)statsProp.FindPropertyRelative("unitSize").enumValueIndex;
        if (currentSize == UnitSize.Monstrous || currentSize == UnitSize.SingleUnit)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("ExplosionDamage"), new GUIContent("Knockback Damage"));
            EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("ExplosionRange"), new GUIContent("Knockback Range"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("MeleeAttack"), new GUIContent("Melee Attack"));
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("MeleeDefense"), new GUIContent("Melee Defense"));
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("HitPointsPerUnit"), new GUIContent("Hit Points Per Unit"));
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("WeaponStrength"), new GUIContent("Weapon Strength"));
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("Speed"), new GUIContent("Speed"));
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("Leadership"), new GUIContent("Leadership"));
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("Armor"), new GUIContent("Armor"));

        // Charge fields (always shown, as not specified otherwise)
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("ChargeBonus"), new GUIContent("Charge Bonus"));
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("ChargeImactDamage"), new GUIContent("Charge Impact Damage"));
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("ChargeCount"), new GUIContent("Charge Count"));



        // Remaining fields
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("RarityTier"), new GUIContent("Rarity Tier"));
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("baseUnitCount"), new GUIContent("Base Unit Count"));
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("attackCooldown"), new GUIContent("Attack Cooldown"));

        // Squad Attributes (foldout with checkboxes)
        EditorGUILayout.PropertyField(statsProp.FindPropertyRelative("SquadAttributes"), new GUIContent("Squad Attributes"), true);

        EditorGUI.indentLevel--;

        // -------------------------------------------------
        //  Assets Section
        // -------------------------------------------------
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Squad Assets", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        // Draw every field inside SquadAssets individually
        EditorGUILayout.PropertyField(assetsProp.FindPropertyRelative("race"),
                                     new GUIContent("Race"));
        EditorGUILayout.PropertyField(assetsProp.FindPropertyRelative("unitIcon"),
                                     new GUIContent("Unit Icon"));
        EditorGUILayout.PropertyField(assetsProp.FindPropertyRelative("squadIcon"),
                                     new GUIContent("Squad Icon"));
        EditorGUILayout.PropertyField(assetsProp.FindPropertyRelative("unitRecruitmentPrefab"),
                                     new GUIContent("Recruitment Prefab"));
        EditorGUILayout.PropertyField(assetsProp.FindPropertyRelative("voiceSFX"),
                                     new GUIContent("Voice SFX"));
        EditorGUILayout.PropertyField(assetsProp.FindPropertyRelative("meleeAttackSFX"),
                                     new GUIContent("Melee Attack SFX"));
        if (currentType != UnitType.Melee) // Assuming UnitType enum has a 'Ranged' value
        {
            EditorGUILayout.PropertyField(assetsProp.FindPropertyRelative("fireProjectileSFX"),
                                         new GUIContent("Fire Projectile SFX"));
        }
        EditorGUILayout.PropertyField(assetsProp.FindPropertyRelative("formationDiscipline"),
                                     new GUIContent("Formation Discipline"));
        if (currentType == UnitType.Artillery) // Assuming UnitType enum has a 'Ranged' value
        {
            EditorGUILayout.PropertyField(assetsProp.FindPropertyRelative("ArtilleryCrewPrefab"),
                                         new GUIContent("Artillery Crew Prefab"));
        }
        if (currentType == UnitType.Mage)
        {
            // Required for a mage. EntityWatcher logs an error at spawn if this is empty, because a
            // mage with no spell has no attack at all. Note that for a Mage the "Rate of Fire" field
            // above is its cast cooldown, and "Ammunition" is its charge count.
            EditorGUILayout.PropertyField(assetsProp.FindPropertyRelative("mageSpell"),
                                         new GUIContent("Mage Spell"));
        }

        EditorGUI.indentLevel--;   // end of Assets

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(target);
    }
}
#endif