using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Memori.SaveData;
using TJ;

namespace TJ
{
    public class PlayerSaveDataEditor : EditorWindow
    {
        PlayerSaveData _data;
        Vector2 _scroll;
        bool _dirty;
        int _tab;

        static readonly string[] Tabs = { "Stats", "Collection", "Tavern / Unlocks", "Tutorial" };
        static readonly TT_Difficulty[] Difficulties = (TT_Difficulty[])System.Enum.GetValues(typeof(TT_Difficulty));
        static readonly Race[] Races = (Race[])System.Enum.GetValues(typeof(Race));
        static readonly UnlockCondition[] UnlockConditions = (UnlockCondition[])System.Enum.GetValues(typeof(UnlockCondition));
        static readonly ConsumableEnum[] Consumables = (ConsumableEnum[])System.Enum.GetValues(typeof(ConsumableEnum));
        static readonly UnitName[] UnitNames = (UnitName[])System.Enum.GetValues(typeof(UnitName));
        static readonly GearID[] GearIDs;

        static PlayerSaveDataEditor()
        {
            var all = (GearID[])System.Enum.GetValues(typeof(GearID));
            var filtered = new List<GearID>(all.Length);
            foreach (GearID g in all)
                if (g != GearID.None) filtered.Add(g);
            GearIDs = filtered.ToArray();
        }

        [MenuItem("Tabletop Tavern/Player Save Data Editor")]
        static void Open() => GetWindow<PlayerSaveDataEditor>("Player Save Data");

        void OnEnable() => Load();

        void Load()
        {
            _data = SaveDataHandler.LoadPlayerSaveData();
            _dirty = false;
        }

        void OnGUI()
        {
            DrawToolbar();

            if (_data == null)
            {
                EditorGUILayout.HelpBox("No save data found.", MessageType.Info);
                return;
            }

            _tab = GUILayout.Toolbar(_tab, Tabs);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            switch (_tab)
            {
                case 0: DrawStats(); break;
                case 1: DrawCollection(); break;
                case 2: DrawTavernAndUnlocks(); break;
                case 3: DrawTutorialAndMeta(); break;
            }

            EditorGUILayout.EndScrollView();
            DrawFooter();
        }

        #region Toolbar

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Player Save Data Editor", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
                Load();
            EditorGUILayout.EndHorizontal();

            if (_dirty)
                EditorGUILayout.HelpBox("Unsaved changes.", MessageType.Warning);
        }

        #endregion

        #region Stats tab

        void DrawStats()
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("General", EditorStyles.boldLabel);
            IntField("Campaigns Started", ref _data.campaignsStarted);
            IntField("Campaigns Completed", ref _data.campaignsCompleted);
            IntField("Game Completions", ref _data.gameCompletions);
            BoolField("Is Dev Tool User", ref _data.isDevToolUser);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Renown", EditorStyles.boldLabel);
            // IntField("Gold To Deposit", ref _data.goldToDeposit); // legacy, see Renown migration
            // IntField("Deposited Gold", ref _data.depositedGold); // legacy, see Renown migration
            IntField("Renown", ref _data.renown);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Difficulty", EditorStyles.boldLabel);
            IntField("Max Difficulty Overall", ref _data.MaxDifficultyOverall);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Hero Difficulty Completions", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);
            foreach (Hero hero in HeroData.Heroes)
                DrawHeroRow(hero);
            EditorGUILayout.EndVertical();
        }

        void DrawHeroRow(Hero hero)
        {
            List<int> completed = GetOrCreateHeroEntry(hero.HeroID);

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
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
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

        List<int> GetOrCreateHeroEntry(int heroID)
        {
            for (int i = 0; i < _data.HeroDifficultiesCompleted.Count; i++)
            {
                if (_data.HeroDifficultiesCompleted[i].HeroID == heroID)
                    return _data.HeroDifficultiesCompleted[i].DifficultiesCompleted;
            }
            var entry = new HeroDifficultiesCompleted { HeroID = heroID, DifficultiesCompleted = new List<int>() };
            _data.HeroDifficultiesCompleted.Add(entry);
            return _data.HeroDifficultiesCompleted[_data.HeroDifficultiesCompleted.Count - 1].DifficultiesCompleted;
        }

        #endregion

        #region Collection tab

        void DrawCollection()
        {
            EditorGUILayout.Space(4);

            // Gear
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawCollectionHeader("Gear Collected",
                () => { foreach (GearID g in GearIDs) if (!_data.gearIdsCollected.Contains((int)g)) _data.gearIdsCollected.Add((int)g); },
                () => _data.gearIdsCollected.Clear());
            DrawButtonGrid(GearIDs,
                g => _data.gearIdsCollected.Contains((int)g),
                g => _data.gearIdsCollected.Add((int)g),
                g => _data.gearIdsCollected.Remove((int)g),
                5);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Troops
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawCollectionHeader("Troops Recruited",
                () => { foreach (UnitName u in UnitNames) if (!_data.troopsRecruited.Contains(u)) _data.troopsRecruited.Add(u); },
                () => _data.troopsRecruited.Clear());
            DrawButtonGrid(UnitNames,
                u => _data.troopsRecruited.Contains(u),
                u => _data.troopsRecruited.Add(u),
                u => _data.troopsRecruited.Remove(u),
                4);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Consumables
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawCollectionHeader("Consumables Acquired",
                () => { foreach (ConsumableEnum c in Consumables) if (!_data.consumablesAquired.Contains((int)c)) _data.consumablesAquired.Add((int)c); },
                () => _data.consumablesAquired.Clear());
            DrawButtonGrid(Consumables,
                c => _data.consumablesAquired.Contains((int)c),
                c => _data.consumablesAquired.Add((int)c),
                c => _data.consumablesAquired.Remove((int)c),
                4);
            EditorGUILayout.EndVertical();
        }

        void DrawCollectionHeader(string label, System.Action addAll, System.Action clearAll)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add All", GUILayout.Width(62))) { addAll(); _dirty = true; }
            if (GUILayout.Button("Clear All", GUILayout.Width(62))) { clearAll(); _dirty = true; }
            EditorGUILayout.EndHorizontal();
        }

        void DrawButtonGrid<T>(T[] values,
            System.Func<T, bool> hasValue,
            System.Action<T> addValue,
            System.Action<T> removeValue,
            int columns)
        {
            int count = 0;
            EditorGUILayout.BeginHorizontal();
            foreach (T val in values)
            {
                bool has = hasValue(val);
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = has ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.85f, 0.4f, 0.4f);
                if (GUILayout.Button(val.ToString(), GUILayout.Height(22)))
                {
                    if (has) removeValue(val);
                    else addValue(val);
                    _dirty = true;
                }
                GUI.backgroundColor = prev;
                count++;
                if (count % columns == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            if (count % columns != 0)
                GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Tavern / Unlocks tab

        void DrawTavernAndUnlocks()
        {
            EditorGUILayout.Space(4);

            // Tavern Themes
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawCollectionHeader("Tavern Themes Unlocked",
                () => { foreach (Race r in Races) if (!_data.unlockedTavernThemes.Contains(r)) _data.unlockedTavernThemes.Add(r); },
                () => _data.unlockedTavernThemes.Clear());
            DrawButtonGrid(Races,
                r => _data.unlockedTavernThemes.Contains(r),
                r => _data.unlockedTavernThemes.Add(r),
                r => _data.unlockedTavernThemes.Remove(r),
                4);

            EditorGUILayout.Space(4);
            BoolField("Has Theme Selected", ref _data.hasTavernThemeSelected);
            if (_data.hasTavernThemeSelected)
            {
                Race activeRace = (Race)EditorGUILayout.EnumPopup("Active Theme Race", _data.activeTavernThemeRace);
                if (activeRace != _data.activeTavernThemeRace) { _data.activeTavernThemeRace = activeRace; _dirty = true; }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Unlock Conditions
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawCollectionHeader("Unlock Conditions Completed",
                () => { foreach (UnlockCondition uc in UnlockConditions) if (!_data.unlockConditionsCompleted.Contains(uc)) _data.unlockConditionsCompleted.Add(uc); },
                () => _data.unlockConditionsCompleted.Clear());
            DrawButtonGrid(UnlockConditions,
                uc => _data.unlockConditionsCompleted.Contains(uc),
                uc => _data.unlockConditionsCompleted.Add(uc),
                uc => _data.unlockConditionsCompleted.Remove(uc),
                4);
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Tutorial tab

        void DrawTutorialAndMeta()
        {
            EditorGUILayout.Space(4);

            // Tutorial steps
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Tutorial Steps Completed", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Mark All (0–20)", GUILayout.Width(110)))
            {
                for (int i = 0; i <= 20; i++)
                    if (!_data.tutorialStepCompleted.Contains(i)) _data.tutorialStepCompleted.Add(i);
                _dirty = true;
            }
            if (GUILayout.Button("Clear", GUILayout.Width(50))) { _data.tutorialStepCompleted.Clear(); _dirty = true; }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"Completed steps: {(_data.tutorialStepCompleted.Count == 0 ? "none" : string.Join(", ", _data.tutorialStepCompleted))}", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();

        }

        #endregion

        #region Footer

        void DrawFooter()
        {
            EditorGUILayout.Space(4);
            EditorGUI.BeginDisabledGroup(!_dirty);
            if (GUILayout.Button("Save to Disk", GUILayout.Height(30)))
            {
                SaveDataHandler.SavePlayerSaveData(_data);
                _dirty = false;
                Debug.Log("[PlayerSaveDataEditor] Player save data written.");
            }
            EditorGUI.EndDisabledGroup();
        }

        #endregion

        #region Helpers

        void IntField(string label, ref int value)
        {
            int next = EditorGUILayout.IntField(label, value);
            if (next != value) { value = next; _dirty = true; }
        }

        void BoolField(string label, ref bool value)
        {
            bool next = EditorGUILayout.Toggle(label, value);
            if (next != value) { value = next; _dirty = true; }
        }

        #endregion
    }
}
