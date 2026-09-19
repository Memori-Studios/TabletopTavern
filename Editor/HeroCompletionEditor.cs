using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Memori.SaveData;
using TJ;

namespace TJ
{
public class HeroCompletionEditor : EditorWindow
{
    PlayerSaveData _saveData;
    Vector2 _scroll;
    bool _dirty;

    static readonly TT_Difficulty[] Difficulties = (TT_Difficulty[])System.Enum.GetValues(typeof(TT_Difficulty));

    [MenuItem("Tabletop Tavern/Hero Completion Editor")]
    static void Open() => GetWindow<HeroCompletionEditor>("Hero Completions");

    void OnEnable() => Load();

    void Load()
    {
        _saveData = SaveDataHandler.LoadPlayerSaveData();
        _dirty = false;
    }

    void OnGUI()
    {
        DrawToolbar();

        if (_saveData == null)
        {
            EditorGUILayout.HelpBox("No save data found.", MessageType.Info);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (Hero hero in HeroData.Heroes)
            DrawHeroRow(hero);

        EditorGUILayout.EndScrollView();

        DrawFooter();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Hero Completion Editor", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
            Load();
        EditorGUILayout.EndHorizontal();

        if (_dirty)
            EditorGUILayout.HelpBox("Unsaved changes.", MessageType.Warning);
    }

    void DrawHeroRow(Hero hero)
    {
        List<int> completed = GetOrCreateEntry(hero.HeroID);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"[{hero.HeroID:D2}] {hero.HeroName}", EditorStyles.boldLabel, GUILayout.Width(220));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Add All", GUILayout.Width(62)))
        {
            foreach (TT_Difficulty d in Difficulties)
            {
                int val = (int)d;
                if (!completed.Contains(val)) completed.Add(val);
            }
            _dirty = true;
        }

        if (GUILayout.Button("Clear All", GUILayout.Width(62)))
        {
            completed.Clear();
            _dirty = true;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        foreach (TT_Difficulty difficulty in Difficulties)
        {
            int val = (int)difficulty;
            bool has = completed.Contains(val);
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = has ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.85f, 0.4f, 0.4f);

            if (GUILayout.Button(difficulty.ToString(), GUILayout.Width(72), GUILayout.Height(22)))
            {
                if (has) completed.Remove(val);
                else completed.Add(val);
                _dirty = true;
            }

            GUI.backgroundColor = prev;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    void DrawFooter()
    {
        EditorGUILayout.Space(4);
        EditorGUI.BeginDisabledGroup(!_dirty);
        if (GUILayout.Button("Save to Disk", GUILayout.Height(30)))
            Save();
        EditorGUI.EndDisabledGroup();
    }

    void Save()
    {
        SaveDataHandler.SavePlayerSaveData(_saveData);
        _dirty = false;
        Debug.Log("[HeroCompletionEditor] Player save data written.");
    }

    List<int> GetOrCreateEntry(int heroID)
    {
        for (int i = 0; i < _saveData.HeroDifficultiesCompleted.Count; i++)
        {
            if (_saveData.HeroDifficultiesCompleted[i].HeroID == heroID)
                return _saveData.HeroDifficultiesCompleted[i].DifficultiesCompleted;
        }

        var entry = new HeroDifficultiesCompleted { HeroID = heroID, DifficultiesCompleted = new List<int>() };
        _saveData.HeroDifficultiesCompleted.Add(entry);
        return _saveData.HeroDifficultiesCompleted[_saveData.HeroDifficultiesCompleted.Count - 1].DifficultiesCompleted;
    }
}
}
