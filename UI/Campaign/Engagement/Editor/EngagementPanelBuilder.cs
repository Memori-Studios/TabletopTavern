using System;
using System.Collections.Generic;
using Memori.Audio;
using Memori.Tooltip;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TJ.Engagement.EditorTools
{
    /// <summary>
    /// Generates the engagement panel (Engagement Panel UI.prefab and its row parts) on the game's Basic Background and
    /// installs it in Map.unity. Rebuilding the panel keeps hand edits to the part prefabs.
    /// </summary>
    public static class EngagementPanelBuilder
    {
        public const string PartFolder = "Assets/Data/Prefabs/UI/Map/Engagement";
        public const string PanelPath = PartFolder + "/Engagement Panel UI.prefab";
        const string SpoilRowPath = PartFolder + "/Engagement Spoil Row.prefab";
        const string ChoiceRowPath = PartFolder + "/Engagement Choice Row.prefab";
        const string EndBattlePopupPath = PartFolder + "/End Battle Pop Up.prefab";
        const string StatCellPath = "Assets/Data/Prefabs/UI/Map/Town/Town Stat Cell.prefab";
        const string ScenePath = "Assets/Scenes/Map.unity";
        const string ButtonFolder = "Assets/Data/Prefabs/UI/Reuseable/Buttons";
        const string BasicBackgroundPath = "Assets/Data/Prefabs/UI/Reuseable/Basic Background.prefab";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";
        const string HoverSoundPath = "Assets/Scripts/Memori.Audio/SOs/Button Hover - SFXReference.asset";
        const string TableName = "MainLocalizationTable";
        const string Hud = "Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/";
        static readonly string[] OldChildren =
        {
            "End Battle Pop Up", "Enemy Army Canvas Group", "Battle Options Canvas Group", "Post Battle Total Panel",
            "Claim Rewards Button", "Post Battle Choices New", "Run Lost Canvas Group", "Juice",
        };
        const string ContinueName = "Continue Parent_1";
        const string LootTownName = "Loot Town Button";

        const float PanelWidth = 1160f;
        // The card's top edge sits 196 px below the top of a 1080 screen; the fit keeps the tallest state above the army bar on a 125% Deck.
        // Low enough that the end-of-battle banner, at its old screen spot, sits clear above the card.
        const float PanelTopY = 286f;
        static readonly Vector2 FitDesign = new(1160f, 520f);
        static readonly Vector2 FitReserved = new(0f, 371f);
        const float BandAlpha = 0.12f;
        const int OverlaySortingOrder = 104;
        const float BannerScreenY = 140f;
        const float PanelPadding = 5f;
        const float HeaderHeight = 92f;
        const float TextureTop = 103f;
        const float TexturePixelsPerUnit = 12f;
        // Two stacks of 531 with a 1 px rule and 24 px either side fill the 1112 px inside the side padding.
        const float ColumnWidth = 531f;
        const float RowHeight = 56f;
        const float RowGap = 4f;
        const float ButtonWidth = 250f;
        const float ButtonHeight = 90f;

        #region Style
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Well = Hex("162023");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        static readonly Color White = Hex("ECF0F1");
        static readonly Color Soft = Hex("D5DCDF");
        static readonly Color Sub = Hex("B4AA94");
        static readonly Color Flavour = Hex("A99F8A");
        static readonly Color Cap = Hex("8C9AA2");
        static readonly Color Coin = Hex("E3BB71");
        static readonly Color Blue = Hex("9ED8FF");
        static readonly Color Negative = Hex("E3695E");
        static readonly Color TakenTitle = Hex("7E8A8F");
        static readonly Color TakenWord = Hex("9FB0B8");
        static readonly Color WellEdge = Hex("605635", 0.9f);
        static readonly Color PredictionGrey = Hex("A9B9C6");
        // The map's own Autoresolve and Fight Battle recipes, read from Map.unity.
        static readonly Color[] AutoResolveHue = { Hex("152733"), Hex("4492C5"), Hex("4492C5"), Hex("45AFF7"), Hex("2EABFF"), Hex("87CFFF"), Hex("8ACACC", 0.8f) };
        static readonly Color[] FightHue = { Hex("300F10"), Hex("C81616"), Hex("C81616"), Hex("FA0505"), Hex("FF0000"), Hex("FF5C5C"), Hex("CC8B8A", 0.8f) };

        static TMP_FontAsset displayDrop, display;
        static Sprite mount, solid, shadow, squareSliced, edgeFade;
        static Sprite skirmishIcon, hordeIcon, gateIcon, swordsIcon, skullIcon, speedIcon, rerollIcon, heartIcon, scrollIcon;
        static Sprite plainsIcon, forestIcon, riverIcon, swampIcon, clearIcon, rainIcon, fogIcon, snowIcon;
        static Sprite goldIcon, recruitIcon, ransomIcon, conscriptIcon, consumeIcon, purgeIcon, hourIcon;
        static GameObject standardButton, primaryButton, basicBackground, statCellPart, endBattlePopupPart;
        static SFXReference hoverSound;

        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            var sheet = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite) sheet[sprite.name] = sprite;
            sheet.TryGetValue("TooltipMount", out mount);
            sheet.TryGetValue("TooltipSolid", out solid);
            sheet.TryGetValue("TooltipShadow", out shadow);
            if (mount == null || solid == null || shadow == null) Debug.LogError("EngagementPanelBuilder: tooltip sheet sprites missing.");
            squareSliced = Load<Sprite>("Assets/Art/Icons/UI/SquareSliced.png");
            edgeFade = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Shadow/Vertical Shadow.png");
            skirmishIcon = Load<Sprite>("Assets/Art/Icons/Map/Skirmish.png");
            hordeIcon = Load<Sprite>("Assets/Art/Icons/Map/Horde.png");
            gateIcon = Load<Sprite>("Assets/Art/Icons/UnitTypes/Gate.png");
            swordsIcon = Load<Sprite>(Hud + "Icons_Status/ICON_FantasyWarrior_Status_Attack02_Clean.png");
            skullIcon = Load<Sprite>(Hud + "Icons_Map/ICON_FantasyWarrior_Map_Skull01_Clean.png");
            speedIcon = Load<Sprite>(Hud + "Icons_Status/ICON_FantasyWarrior_Status_SpeedUp01_Clean.png");
            rerollIcon = Load<Sprite>("Assets/Art/Icons/Consumables/Rewind.png");
            heartIcon = Load<Sprite>("Assets/Art/Icons/Stats/Health.png");
            scrollIcon = Load<Sprite>("Assets/Art/Icons/Achievements/Baked/Scroll.png");
            plainsIcon = Load<Sprite>("Assets/Art/Icons/Map/Engagement.png");
            forestIcon = Load<Sprite>("Assets/Art/Icons/Map/BattlefieldConditions/Forest.png");
            riverIcon = Load<Sprite>("Assets/Art/Icons/Map/BattlefieldConditions/River.png");
            swampIcon = Load<Sprite>("Assets/Art/Icons/Map/BattlefieldConditions/Swamp.png");
            clearIcon = Load<Sprite>("Assets/Art/Icons/Achievements/SunClean.png");
            rainIcon = Load<Sprite>("Assets/Art/Icons/Map/BattlefieldConditions/Rain.png");
            fogIcon = Load<Sprite>("Assets/Art/Icons/Map/BattlefieldConditions/Fog.png");
            snowIcon = Load<Sprite>("Assets/Art/Icons/Map/BattlefieldConditions/Snow.png");
            goldIcon = Load<Sprite>("Assets/Art/Icons/Events/Gold.png");
            recruitIcon = Load<Sprite>("Assets/Art/Icons/Map/Recruit.png");
            ransomIcon = Load<Sprite>(Hud + "Icons_Resources/ICON_SM_Item_Envelope_01.png");
            conscriptIcon = Load<Sprite>(Hud + "Icons_Resources/ICON_SM_Item_Feather_Pen_02.png");
            consumeIcon = Load<Sprite>(Hud + "Icons_Inventory/ICON_FantasyWarrior_Inventory_Food01_Clean.png");
            purgeIcon = Load<Sprite>(Hud + "Icons_Status/ICON_FantasyWarrior_Status_Health02_Clean.png");
            hourIcon = Load<Sprite>("Assets/Art/Icons/Achievements/Baked/ICON_FantasyWarrior_Status_Time01_Clean.png");
            standardButton = Load<GameObject>(ButtonFolder + "/Button - Standard.prefab");
            primaryButton = Load<GameObject>(ButtonFolder + "/Button - Primary.prefab");
            basicBackground = Load<GameObject>(BasicBackgroundPath);
            statCellPart = Load<GameObject>(StatCellPath);
            endBattlePopupPart = Load<GameObject>(EndBattlePopupPath);
            hoverSound = Load<SFXReference>(HoverSoundPath);
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"EngagementPanelBuilder: missing {typeof(T).Name} at {path}");
            return asset;
        }

        static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color colour);
            colour.a = alpha;
            return colour;
        }

        static Color A(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }
        #endregion

        #region Localization
        // English source text for the keys this panel adds. Other locales are filled in separately.
        public static readonly (string key, string english)[] Keys =
        {
            ("engagementEnemyHost", "Enemy host"),
            ("engagementSquadsTroops", "{0} squads · {1} troops"),
            ("engagementYourArmy", "Your army"),
            ("engagementEnemy", "Enemy"),
            ("engagementHeavensongLine", "Heavensong: reroll the weather once before this battle."),
            ("engagementReserveWarning", "Your reserves do not heal after this fight"),
            ("engagementPredicts", "Predicts {0}"),
            ("engagementSkirmishSub", "A battle in Act {0} against the {1}"),
            ("engagementHordeSub", "The final battle of Act {0} against the {1}"),
            ("engagementGarrisonSub", "{0} · {1}"),
            ("engagementResultSub", "{0} · {1}"),
            ("engagementOutcomeHost", "Enemy Host Defeated"),
            ("engagementOutcomeGarrison", "Town Garrison Defeated"),
            ("engagementRunOver", "Run over"),
            ("engagementDefeatLine", "Your warband is broken. This run ends here."),
            ("engagementGarrisonWonLine", "The town is yours. Its spoils wait inside."),
            ("engagementLootTown", "Loot the Town"),
            ("engagementBattleReport", "Battle report"),
            ("engagementDetailedStats", "Detailed stats"),
            ("engagementCellDestroyed", "Destroyed"),
            ("engagementOfCount", "{0} of {1}"),
            ("engagementCellSlain", "Enemy slain"),
            ("engagementCellLosses", "Your losses"),
            ("engagementTroopsCount", "{0} troops"),
            ("engagementCellSquadsLost", "Squads lost"),
            ("engagementNone", "None"),
            ("engagementSpoils", "Spoils"),
            ("engagementTakeAll", "Take them all"),
            ("engagementSpoilsOfWar", "Spoils of war"),
            ("engagementChooseOne", "Choose one"),
            ("engagementNoChoices", "Nothing to choose after this battle."),
            ("engagementBountyDetail", "Gold for the win"),
            ("engagementRansomDetail", "Gold for sparing the captives"),
            ("engagementHeroOption", "Hero option"),
            ("engagementDamageTitle", "Damage dealt"),
            ("engagementDamageSub", "Your squads in this battle"),
            ("engagementTotalDamage", "Total damage"),
            ("engagementTroopsLost", "Troops lost"),
            ("engagementDamageFooter", "Sorted by damage dealt. Kills and losses also show on each card in your army bar."),
            ("engagementColSquad", "Squad"),
            ("engagementColKills", "Kills"),
            ("engagementColLost", "Lost"),
        };

        static StringTableCollection collection;
        static StringTable english;

        static void LoadTable()
        {
            collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            english = (StringTable)collection.GetTable("en");
        }

        [MenuItem("Tabletop Tavern/Engagement Panel/Add Text Keys")]
        public static void AddKeysMenu() => Debug.Log("EngagementPanelBuilder: " + EnsureKeys());

        /// <summary>Adds any missing engagement key with its English text. Existing keys and their text are left alone.</summary>
        public static string EnsureKeys()
        {
            LoadTable();
            int added = 0;
            foreach ((string key, string text) in Keys)
            {
                if (collection.SharedData.GetEntry(key) != null) continue;
                collection.SharedData.AddKey(key);
                english.AddEntry(key, text);
                added++;
            }
            if (added == 0) return "all keys present.";
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(english);
            AssetDatabase.SaveAssetIfDirty(collection.SharedData);
            AssetDatabase.SaveAssetIfDirty(english);
            return $"added {added} keys.";
        }

        static long KeyId(string key)
        {
            SharedTableData.SharedTableEntry entry = collection.SharedData.GetEntry(key);
            if (entry == null) throw new InvalidOperationException($"EngagementPanelBuilder: no localization key '{key}'.");
            return entry.Id;
        }

        static string English(string key)
        {
            StringTableEntry entry = english.GetEntry(key);
            return entry != null ? entry.Value : key;
        }

        // Points the text's localizer at a key, adding one when the text has none. Shows the English value in the Editor.
        static void Localize(TMP_Text text, string key)
        {
            LocalizeStringEvent localizer = text.GetComponent<LocalizeStringEvent>();
            if (localizer == null)
            {
                localizer = text.gameObject.AddComponent<LocalizeStringEvent>();
                var setter = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), text, typeof(TMP_Text).GetProperty("text").GetSetMethod());
                UnityEventTools.AddPersistentListener(localizer.OnUpdateString, setter);
            }
            localizer.enabled = true;
            var so = new SerializedObject(localizer);
            so.FindProperty("m_StringReference.m_TableReference.m_TableCollectionName").stringValue = "GUID:" + collection.SharedData.TableCollectionNameGuid.ToString("N");
            so.FindProperty("m_StringReference.m_TableEntryReference.m_KeyId").longValue = KeyId(key);
            so.FindProperty("m_StringReference.m_TableEntryReference.m_Key").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            text.text = English(key);
        }

        // A text the panel fills at runtime must not keep a localizer, or a locale change would reset it.
        static void Unlocalize(TMP_Text text, string placeholder)
        {
            foreach (LocalizeStringEvent localizer in text.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(localizer);
            text.text = placeholder;
        }
        #endregion

        #region Entry points
        /// <summary>Adds missing keys, creates any missing part prefab, then rebuilds the panel from them.</summary>
        [MenuItem("Tabletop Tavern/Engagement Panel/Rebuild Prefab")]
        public static void BuildPrefab()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            EnsureParts(false);
            BuildPanel();
        }

        [MenuItem("Tabletop Tavern/Engagement Panel/Reset Part Prefabs")]
        static void ResetPartsMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset engagement panel parts",
                    $"The part prefabs in {PartFolder} are rebuilt from code, which discards your edits to them. The panel is rebuilt after.",
                    "Reset", "Cancel"))
                return;
            ResetParts();
        }

        /// <summary>Rebuilds both row prefabs from code, then the panel.</summary>
        public static void ResetParts()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            EnsureParts(true);
            BuildPanel();
        }

        [MenuItem("Tabletop Tavern/Engagement Panel/Install In Map")]
        public static void InstallMenu() => Debug.Log("EngagementPanelBuilder: " + InstallInMap());

        static void BuildPanel()
        {
            // Rebuild inside the existing prefab so the root keeps its id; the Map scene instance references it.
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath) != null;
            Scene stage = default;
            GameObject root;
            if (existing) root = PrefabUtility.LoadPrefabContents(PanelPath);
            else
            {
                // A first build happens in a preview scene so no open scene is marked changed.
                stage = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Engagement Panel UI", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
            }
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root);
                Normalize(root);
                RecordOverrides(root);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                Debug.Log($"EngagementPanelBuilder: wrote {PanelPath}");
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else
                {
                    Object.DestroyImmediate(root);
                    EditorSceneManager.ClosePreviewScene(stage);
                }
            }
        }

        /// <summary>
        /// Replaces the old engagement UI under Map.unity's Engagement Panel with the prefab, keeps the corner Continue slot,
        /// points EngagementPanel at the view and saves only Map.unity.
        /// </summary>
        public static string InstallInMap()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (prefab == null) return $"no prefab at {PanelPath}; run Rebuild Prefab first.";
            LoadTable();
            Scene map = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!map.isLoaded)
            {
                map = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            try
            {
                if (map.isDirty && !opened) return "Map.unity has unsaved changes; save or revert them first.";
                EngagementPanel panel = Find<EngagementPanel>(map);
                if (panel == null) return "no EngagementPanel in Map.unity.";

                var old = new List<Transform>();
                foreach (string name in OldChildren)
                {
                    Transform child = panel.transform.Find(name);
                    if (child != null) old.Add(child);
                }
                string outside = CheckReferences(map, old, panel);
                if (!string.IsNullOrEmpty(outside)) return "stopped, outside references into the old UI:\n" + outside;

                int index = panel.transform.childCount;
                foreach (Transform child in old)
                {
                    index = Mathf.Min(index, child.GetSiblingIndex());
                    Object.DestroyImmediate(child.gameObject);
                }
                for (int i = panel.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = panel.transform.GetChild(i).gameObject;
                    if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(child) == prefab)
                    {
                        index = Mathf.Min(index, i);
                        Object.DestroyImmediate(child);
                    }
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel.transform);
                instance.transform.SetSiblingIndex(Mathf.Min(index, panel.transform.childCount - 1));
                Transform corner = panel.transform.Find(ContinueName);
                if (corner != null)
                {
                    corner.SetAsLastSibling();
                    Transform loot = FindDeep(corner, LootTownName);
                    TMP_Text lootLabel = loot != null ? Child<TMP_Text>(loot, "Button Label") : null;
                    if (lootLabel != null) Localize(lootLabel, "engagementLootTown");
                }

                var so = new SerializedObject(panel);
                Ref(so, "view", instance.GetComponent<EngagementPanelView>());
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(map);
                if (!EditorSceneManager.SaveScene(map)) return "Map.unity did not save.";
                return $"installed; removed {old.Count} old objects" + (corner != null ? ", corner slot kept." : ", no corner slot found.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(map, true);
            }
        }

        static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // Anything outside the old UI that points into it would lose its target when the old UI is deleted.
        static string CheckReferences(Scene scene, List<Transform> old, EngagementPanel panel)
        {
            var inside = new HashSet<Object>();
            foreach (Transform root in old)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    inside.Add(t.gameObject);
                    foreach (Component component in t.GetComponents<Component>())
                        if (component != null) inside.Add(component);
                }
            var report = new System.Text.StringBuilder();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (inside.Contains(t.gameObject)) continue;
                    foreach (Component component in t.GetComponents<Component>())
                    {
                        if (component == null || component == panel) continue;
                        var so = new SerializedObject(component);
                        SerializedProperty property = so.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            if (component is Transform && (property.propertyPath == "m_Father" || property.propertyPath.StartsWith("m_Children"))) continue;
                            Object target = property.objectReferenceValue;
                            if (target == null || !inside.Contains(target)) continue;
                            report.AppendLine($"{t.name} {component.GetType().Name}.{property.propertyPath} -> {target.name}");
                        }
                    }
                }
            return report.ToString();
        }

        // Store what an instance computes on load, so the scene instance does not record layout and TMP values as overrides.
        static void Normalize(GameObject root)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var so = new SerializedObject(text);
                so.FindProperty("m_fontColor32").colorValue = text.color;
                so.FindProperty("m_TextStyleHashCode").intValue = TMP_Style.NormalStyle.hashCode;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Canvas.ForceUpdateCanvases();
            for (int pass = 0; pass < 2; pass++)
                foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>())
                    if (rect.GetComponent<LayoutGroup>() != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        // Direct writes to a nested prefab instance are dropped on save unless they are recorded as overrides.
        static void RecordOverrides(GameObject root)
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(node.gameObject) == null) continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(node.gameObject);
                foreach (Component component in node.GetComponents<Component>())
                    if (component != null && PrefabUtility.GetCorrespondingObjectFromSource(component) != null)
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
        #endregion

        #region Part prefabs
        static GameObject spoilPart, choicePart;

        static void EnsureParts(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(PartFolder)) AssetDatabase.CreateFolder("Assets/Data/Prefabs/UI/Map", "Engagement");
            spoilPart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(SpoilRowPath);
            if (spoilPart == null) spoilPart = SavePart(SpoilRowPath, "Engagement Spoil Row", SpoilRowPart);
            choicePart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(ChoiceRowPath);
            if (choicePart == null) choicePart = SavePart(ChoiceRowPath, "Engagement Choice Row", ChoiceRowPart);
        }

        static GameObject SavePart(string path, string name, Action<GameObject> build)
        {
            // Built in a preview scene so the open scenes are never touched or marked changed.
            Scene stage = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject(name, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
                root.layer = 5;
                build(root);
                Normalize(root);
                RecordOverrides(root);
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"EngagementPanelBuilder: wrote {path}");
                return asset;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        // A spoil to take: a quiet recessed tile with white text. The slot keeps its place when the tile pops away.
        static void SpoilRowPart(GameObject go)
        {
            var slot = (RectTransform)go.transform;
            Fixed(go, -1f, RowHeight);
            slot.sizeDelta = new Vector2(ColumnWidth, RowHeight);

            RectTransform taken = Stretch(Rect("Taken", slot));
            HLayout(taken, 10f, TextAnchor.MiddleCenter, new RectOffset());
            Image takenEdge = Img(Stretch(Rect("Edge", taken)), squareSliced, A(WellEdge, 0.5f), Image.Type.Sliced);
            takenEdge.fillCenter = false;
            Ignore(takenEdge.gameObject);
            TMP_Text takenTitle = Text("Title", taken, display, 17f, TakenTitle, "Title");
            Image takenMark = Img(Rect("Mark", taken), null, TakenTitle);
            Fixed(takenMark.gameObject, 7f, 7f);
            takenMark.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            TMP_Text takenWord = Text("Word", taken, display, 16f, TakenWord, "Taken");
            Localize(takenWord, "townSpoilTaken");
            taken.gameObject.SetActive(false);

            RectTransform pop = Stretch(Rect("Pop", slot));
            CanvasGroup popGroup = pop.gameObject.AddComponent<CanvasGroup>();
            Image fill = Img(Stretch(Rect("Fill", pop)), solid, Well);
            fill.raycastTarget = true;
            Image edge = Img(Stretch(Rect("Edge", pop)), squareSliced, Color.white, Image.Type.Sliced);
            edge.fillCenter = false;
            Image rule = Img(Stretch(Rect("Rule", pop), 3f, 3f, 3f, 3f), squareSliced, A(WellEdge, 0.7f), Image.Type.Sliced);
            rule.fillCenter = false;

            Button button = pop.gameObject.AddComponent<Button>();
            button.targetGraphic = edge;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colours = button.colors;
            colours.normalColor = A(Brass, 0.5f);
            colours.highlightedColor = Hex("D9B25E");
            colours.selectedColor = Hex("D9B25E");
            colours.pressedColor = Hex("9C7A36");
            colours.disabledColor = A(Brass, 0.25f);
            colours.colorMultiplier = 1f;
            colours.fadeDuration = 0.08f;
            button.colors = colours;
            HoverSound(pop.gameObject, button);
            MemoriTooltipTrigger tooltip = pop.gameObject.AddComponent<MemoriTooltipTrigger>();
            tooltip.enabled = false;

            RectTransform content = Stretch(Rect("Content", pop));
            HLayout(content, 12f, TextAnchor.MiddleLeft, new RectOffset(14, 16, 0, 0));
            Image icon = Img(Rect("Icon", content), goldIcon, Color.white);
            icon.preserveAspect = true;
            Fixed(icon.gameObject, 36f, 36f);
            RectTransform titles = Rect("Titles", content);
            VLayout(titles, 1f, new RectOffset());
            Flexible(titles.gameObject, 1f).preferredWidth = 0f;
            RectTransform head = Rect("Head", titles);
            HLayout(head, 10f, TextAnchor.MiddleLeft, new RectOffset());
            TMP_Text title = Text("Title", head, displayDrop, 17f, White, "Title");
            Shrink(title, 13f);
            TMP_Text detail = Text("Detail", titles, display, 13f, Soft, "Detail");
            Shrink(detail, 10f);

            RectTransform tag = Rect("Tag", head);
            HLayout(tag, 0f, TextAnchor.MiddleCenter, new RectOffset(10, 10, 0, 0));
            Fixed(tag.gameObject, -1f, 24f);
            Image tagFrame = Img(Stretch(Rect("Frame", tag)), squareSliced, Coin, Image.Type.Sliced);
            tagFrame.fillCenter = false;
            Ignore(tagFrame.gameObject);
            TMP_Text tagText = Text("Text", tag, display, 13f, Coin, "RARE");
            tagText.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            tagText.characterSpacing = 8f;
            tag.gameObject.SetActive(false);

            RectTransform valueGroup = Rect("Value", content);
            HLayout(valueGroup, 6f, TextAnchor.MiddleRight, new RectOffset());
            Image valueIcon = Img(Rect("Coin", valueGroup), goldIcon, Coin);
            valueIcon.preserveAspect = true;
            Fixed(valueIcon.gameObject, 20f, 20f);
            TMP_Text value = Text("Amount", valueGroup, displayDrop, 19f, White, "+10");
            value.alignment = TextAlignmentOptions.MidlineRight;
            valueGroup.gameObject.SetActive(false);

            EngagementSpoilRow row = go.AddComponent<EngagementSpoilRow>();
            var so = new SerializedObject(row);
            Ref(so, "button", button);
            Ref(so, "icon", icon);
            Ref(so, "title", title);
            Ref(so, "detail", detail);
            Ref(so, "valueGroup", valueGroup.gameObject);
            Ref(so, "valueIcon", valueIcon);
            Ref(so, "value", value);
            Ref(so, "tag", tag.gameObject);
            Ref(so, "tagFrame", tagFrame);
            Ref(so, "tagText", tagText);
            Ref(so, "tooltip", tooltip);
            Ref(so, "taken", taken.gameObject);
            Ref(so, "takenTitle", takenTitle);
            Ref(so, "pop", pop);
            Ref(so, "popGroup", popGroup);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A spoil of war: the same tile lifted off the card with a deeper shadow and a faint gold glow, a diamond and a gold title.
        static void ChoiceRowPart(GameObject go)
        {
            var slot = (RectTransform)go.transform;
            Fixed(go, -1f, RowHeight);
            slot.sizeDelta = new Vector2(ColumnWidth, RowHeight);

            RectTransform pop = Stretch(Rect("Pop", slot));
            CanvasGroup group = pop.gameObject.AddComponent<CanvasGroup>();
            Image lift = Img(Stretch(Rect("Lift", pop), -10f, -10f, -2f, -14f), shadow, new Color(0f, 0f, 0f, 0.6f), Image.Type.Sliced);
            lift.pixelsPerUnitMultiplier = 4f;
            Image glow = Img(Stretch(Rect("Glow", pop), -12f, -12f, -12f, -12f), shadow, A(Gold, 0.14f), Image.Type.Sliced);
            glow.pixelsPerUnitMultiplier = 4f;
            Image fill = Img(Stretch(Rect("Fill", pop)), solid, Well);
            fill.raycastTarget = true;
            Image edge = Img(Stretch(Rect("Edge", pop)), squareSliced, A(Brass, 0.65f), Image.Type.Sliced);
            edge.fillCenter = false;
            Image rule = Img(Stretch(Rect("Rule", pop), 3f, 3f, 3f, 3f), squareSliced, A(WellEdge, 0.8f), Image.Type.Sliced);
            rule.fillCenter = false;

            Button button = pop.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            HoverSound(pop.gameObject, button);
            MemoriTooltipTrigger tooltip = pop.gameObject.AddComponent<MemoriTooltipTrigger>();
            tooltip.enabled = false;

            RectTransform content = Stretch(Rect("Content", pop));
            HLayout(content, 12f, TextAnchor.MiddleLeft, new RectOffset(10, 16, 0, 0));
            RectTransform diamondCell = Rect("Diamond", content);
            Fixed(diamondCell.gameObject, 21f, RowHeight);
            Image diamondEdge = Img(Rect("Edge", diamondCell), null, A(Brass, 0.65f));
            Centre(diamondEdge.rectTransform, 13f, 13f);
            diamondEdge.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Image diamondFill = Img(Rect("Fill", diamondCell), null, Hex("0F1618"));
            Centre(diamondFill.rectTransform, 10f, 10f);
            diamondFill.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

            Image icon = Img(Rect("Icon", content), ransomIcon, Color.white);
            icon.preserveAspect = true;
            Fixed(icon.gameObject, 36f, 36f);
            RectTransform titles = Rect("Titles", content);
            VLayout(titles, 1f, new RectOffset());
            Flexible(titles.gameObject, 1f).preferredWidth = 0f;
            RectTransform head = Rect("Head", titles);
            HLayout(head, 10f, TextAnchor.MiddleLeft, new RectOffset());
            TMP_Text title = Text("Title", head, displayDrop, 17f, Gold, "Title");
            Shrink(title, 13f);
            TMP_Text detail = Text("Detail", titles, display, 13f, Flavour, "Detail");
            detail.fontStyle = FontStyles.Italic;
            Shrink(detail, 10f);

            RectTransform units = Rect("Units", content);
            HLayout(units, 4f, TextAnchor.MiddleRight, new RectOffset());
            var unitImages = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                Image unit = Img(Rect("Unit " + (i + 1), units), null, Color.white);
                unit.preserveAspect = true;
                Fixed(unit.gameObject, 28f, 28f);
                unitImages[i] = unit;
            }
            units.gameObject.SetActive(false);

            TMP_Text tag = CapLabel(head, "Tag");
            tag.color = Blue;
            tag.gameObject.SetActive(false);

            RectTransform valueGroup = Rect("Value", content);
            HLayout(valueGroup, 6f, TextAnchor.MiddleRight, new RectOffset());
            Image valueIcon = Img(Rect("Coin", valueGroup), goldIcon, Coin);
            valueIcon.preserveAspect = true;
            Fixed(valueIcon.gameObject, 20f, 20f);
            TMP_Text value = Text("Amount", valueGroup, displayDrop, 19f, Coin, "+8");
            value.alignment = TextAlignmentOptions.MidlineRight;
            valueGroup.gameObject.SetActive(false);

            TMP_Text taken = CapLabel(content, "Taken");
            taken.color = Gold;
            Localize(taken, "choiceTaken");
            taken.gameObject.SetActive(false);

            EngagementChoiceRow row = pop.gameObject.AddComponent<EngagementChoiceRow>();
            var so = new SerializedObject(row);
            Ref(so, "button", button);
            Ref(so, "fill", fill);
            Ref(so, "edge", edge);
            Ref(so, "glow", glow);
            Ref(so, "diamondEdge", diamondEdge);
            Ref(so, "diamondFill", diamondFill);
            Ref(so, "icon", icon);
            Ref(so, "title", title);
            Ref(so, "detail", detail);
            Ref(so, "valueGroup", valueGroup.gameObject);
            Ref(so, "valueIcon", valueIcon);
            Ref(so, "value", value);
            Ref(so, "tag", tag);
            Ref(so, "takenText", taken);
            Refs(so, "unitIcons", unitImages);
            Ref(so, "unitIconGroup", units.gameObject);
            Ref(so, "tooltip", tooltip);
            Ref(so, "group", group);
            Ref(so, "pop", pop);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void HoverSound(GameObject go, Selectable interactable)
        {
            UIHoverSFX sfx = GetOrAdd<UIHoverSFX>(go);
            var so = new SerializedObject(sfx);
            Ref(so, "sfxReference", hoverSound);
            Ref(so, "interactableSource", interactable);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        #region Layout
        static void Build(GameObject rootGo)
        {
            rootGo.layer = 5;
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(PanelWidth, 500f);
            root.anchoredPosition = new Vector2(0f, PanelTopY);
            VerticalLayoutGroup rootLayout = GetOrAdd<VerticalLayoutGroup>(rootGo);
            rootLayout.padding = new RectOffset((int)PanelPadding, (int)PanelPadding, (int)PanelPadding, (int)PanelPadding);
            rootLayout.spacing = 0f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(rootGo);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIFitToCanvas fit = GetOrAdd<UIFitToCanvas>(rootGo);
            var fitSo = new SerializedObject(fit);
            fitSo.FindProperty("designSize").vector2Value = FitDesign;
            fitSo.FindProperty("reservedSize").vector2Value = FitReserved;
            fitSo.FindProperty("minScale").floatValue = 0.6f;
            fitSo.ApplyModifiedPropertiesWithoutUndo();
            EngagementPanelView view = GetOrAdd<EngagementPanelView>(rootGo);
            var so = new SerializedObject(view);
            Ref(so, "card", root);

            // The standard panel background most of the game's UI already uses.
            GameObject background = Instance(basicBackground, root, "Basic Background");
            Stretch((RectTransform)background.transform);
            Ignore(background);
            Image backgroundFill = Child<Image>(background.transform, "SPR_Background");
            if (backgroundFill != null) backgroundFill.raycastTarget = true;
            RectTransform texture = Child<RectTransform>(background.transform, "Texture");
            if (texture != null)
            {
                texture.offsetMax = new Vector2(texture.offsetMax.x, -TextureTop);
                Image textureImage = texture.GetComponent<Image>();
                if (textureImage != null) textureImage.pixelsPerUnitMultiplier = TexturePixelsPerUnit;
            }

            // A wash inside the background, under its frame and corner ornaments, from the top edge to the header rule.
            Image band = Img(Rect("Band", background.transform), edgeFade, A(Negative, BandAlpha));
            RectTransform bandRect = band.rectTransform;
            bandRect.anchorMin = new Vector2(0f, 1f);
            bandRect.anchorMax = Vector2.one;
            bandRect.pivot = new Vector2(0.5f, 1f);
            bandRect.offsetMin = new Vector2(0f, -(PanelPadding + HeaderHeight));
            bandRect.offsetMax = Vector2.zero;
            if (texture != null) band.transform.SetSiblingIndex(texture.GetSiblingIndex() + 1);
            Ref(so, "headerBand", band);
            so.FindProperty("bandAlpha").floatValue = BandAlpha;

            BuildHeader(root, so);
            BuildBeforeBattle(root, so);
            BuildResult(root, so);
            BuildResultPopup(root, so);

            Ref(so, "skirmishIcon", skirmishIcon);
            Ref(so, "hordeIcon", hordeIcon);
            Ref(so, "garrisonIcon", gateIcon);
            Ref(so, "victoryIcon", swordsIcon);
            Ref(so, "defeatIcon", skullIcon);
            Ref(so, "plainsIcon", plainsIcon);
            Ref(so, "forestIcon", forestIcon);
            Ref(so, "riverIcon", riverIcon);
            Ref(so, "swampIcon", swampIcon);
            Ref(so, "clearIcon", clearIcon);
            Ref(so, "rainIcon", rainIcon);
            Ref(so, "fogIcon", fogIcon);
            Ref(so, "snowIcon", snowIcon);
            Ref(so, "damageIcon", swordsIcon);
            Ref(so, "goldIcon", goldIcon);
            Ref(so, "recruitIcon", recruitIcon);
            Ref(so, "ransomIcon", ransomIcon);
            Ref(so, "conscriptIcon", conscriptIcon);
            Ref(so, "raiseDeadIcon", skullIcon);
            Ref(so, "consumeIcon", consumeIcon);
            Ref(so, "purgeIcon", purgeIcon);
            Ref(so, "hourOfDestinyIcon", hourIcon);
            Ref(so, "spoilRowPrefab", spoilPart.GetComponent<EngagementSpoilRow>());
            Ref(so, "choiceRowPrefab", choicePart.GetComponentInChildren<EngagementChoiceRow>(true));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The result pop-up: a cover greys the whole card, and the old end-of-battle banner names the outcome over it.
        static void BuildResultPopup(RectTransform root, SerializedObject so)
        {
            // Its own canvas sorts above the squad cards (2, their counts 101) and below Settings (105), so the cover greys the cards too.
            RectTransform overlay = Stretch(Rect("Result Overlay", root));
            Ignore(overlay.gameObject);
            Canvas overlayCanvas = overlay.gameObject.AddComponent<Canvas>();
            overlay.gameObject.AddComponent<GraphicRaycaster>();
            Ref(so, "resultOverlay", overlayCanvas);
            so.FindProperty("overlaySortingOrder").intValue = OverlaySortingOrder;

            Image cover = Img(Stretch(Rect("Result Cover", overlay)), solid, new Color(0.02f, 0.03f, 0.035f, 0f));
            cover.raycastTarget = true;
            cover.gameObject.SetActive(false);
            Ref(so, "resultCover", cover);

            // The old end-of-battle banner, kept as a part, at its old screen spot (rect centre 140 above the screen centre), above the card.
            GameObject banner = Instance(endBattlePopupPart, overlay, "End Battle Pop Up");
            var bannerRect = (RectTransform)banner.transform;
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, BannerScreenY - PanelTopY);
            TMP_Text title = Child<TMP_Text>(banner.transform, "Content/Header/Background/Text/Label Battle Victory or Defeat");
            // The code sets the title, so its localizer must not write "Victory" back when the banner is enabled.
            foreach (LocalizeStringEvent localizer in title.GetComponents<LocalizeStringEvent>()) localizer.enabled = false;
            banner.SetActive(false);

            Ref(so, "endBattlePopup", banner.GetComponent<Animator>());
            Ref(so, "endBattleContent", Child<MemoriCanvasGroup>(banner.transform, "Content"));
            Ref(so, "endBattleTitle", title);
            Ref(so, "endBattleOutcome", Child<TMP_Text>(banner.transform, "Content/Quest/Quest_Text/Label Battle Outcome"));
        }

        static void BuildHeader(RectTransform root, SerializedObject so)
        {
            RectTransform header = Rect("Header", root);
            Fixed(header.gameObject, -1f, HeaderHeight);
            HLayout(header, 16f, TextAnchor.MiddleLeft, new RectOffset(24, 24, 0, 0));
            Image rule = Img(Rect("Band Rule", header), solid, A(Brass, 0.45f));
            Anchor(rule.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            Ignore(rule.gameObject);

            RectTransform mountCell = Mount(header, "Mount", 64f, hordeIcon, 30f);
            Ref(so, "headerIcon", Child<Image>(mountCell, "Icon"));
            RectTransform names = Rect("Names", header);
            VLayout(names, 2f, new RectOffset());
            Flexible(names.gameObject, 1f).preferredWidth = 0f;
            TMP_Text title = Text("Title", names, displayDrop, 32f, Gold, "Horde");
            TMP_Text subtitle = Text("Subtitle", names, display, 16f, Sub, "The final battle of Act III against the Drakosaur Brood");
            subtitle.overflowMode = TextOverflowModes.Ellipsis;
            Ref(so, "headerTitle", title);
            Ref(so, "headerSubtitle", subtitle);

            RectTransform pill = Rect("Pill", header);
            HLayout(pill, 0f, TextAnchor.MiddleCenter, new RectOffset(14, 14, 0, 0));
            Fixed(pill.gameObject, -1f, 30f);
            Image pillFrame = Img(Stretch(Rect("Frame", pill)), squareSliced, Hex("7BD66F"), Image.Type.Sliced);
            pillFrame.fillCenter = false;
            Ignore(pillFrame.gameObject);
            TMP_Text pillText = Text("Text", pill, display, 14f, Hex("7BD66F"), "Victory");
            pillText.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            pillText.characterSpacing = 10f;
            pill.gameObject.SetActive(false);
            Ref(so, "pill", pill.gameObject);
            Ref(so, "pillFrame", pillFrame);
            Ref(so, "pillText", pillText);
        }

        static void BuildBeforeBattle(RectTransform root, SerializedObject so)
        {
            RectTransform body = Rect("Before Battle", root);
            VLayout(body, 16f, new RectOffset(24, 24, 18, 22));
            Ref(so, "preBattleBody", body.gameObject);

            RectTransform host = Rect("Host Row", body);
            HLayout(host, 10f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(host.gameObject, -1f, 22f);
            Localize(CapLabel(host, "Caption"), "engagementEnemyHost");
            TMP_Text hostLine = Text("Count", host, displayDrop, 15f, Cream, "11 squads · 188 troops");
            Unlocalize(hostLine, "11 squads · 188 troops");
            Ref(so, "enemyHostLine", hostLine);
            Spacer(host, true);
            TMP_Text hint = Text("Hint", host, display, 14f, Cap, "Hover a card for its stats");
            Localize(hint, "townGarrisonHint");

            // The enemy's cards sit in a well; the same grid moves to the battle report after the fight.
            RectTransform well = Rect("Enemy Well", body);
            Fixed(well.gameObject, -1f, 146f);
            Img(well, solid, Well);
            Image wellEdge = Img(Stretch(Rect("Edge", well)), squareSliced, WellEdge, Image.Type.Sliced);
            wellEdge.fillCenter = false;
            RectTransform slot = Stretch(Rect("Army Slot", well), 8f, 8f, 8f, 8f);
            Ref(so, "enemyArmySlot", slot);
            RectTransform grid = Rect("Enemy Army", slot);
            Centre(grid, 715f, 130f);
            GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(60f, 130f);
            gridLayout.spacing = new Vector2(5f, 0f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            gridLayout.constraintCount = 1;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
            ContentSizeFitter gridFit = grid.gameObject.AddComponent<ContentSizeFitter>();
            gridFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            gridFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            grid.gameObject.AddComponent<CanvasGroup>();
            Ref(so, "enemyArmyParent", grid);

            RectTransform strip = Rect("Strip", body);
            Fixed(strip.gameObject, -1f, 62f);
            StripRules(strip);
            HorizontalLayoutGroup row = HLayout(strip, 0f, TextAnchor.MiddleLeft, new RectOffset());
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            GameObject battlefield = Cell(strip, "Battlefield", "townBattlefield", forestIcon, Hex("9CC48A"), false);
            Ref(so, "battlefieldValue", Child<TMP_Text>(battlefield.transform, "Value Row/Value"));
            Ref(so, "battlefieldIcon", Child<Image>(battlefield.transform, "Value Row/Icon"));
            GameObject weather = Cell(strip, "Weather", "townWeather", clearIcon, Hex("F2C866"), true);
            Ref(so, "weatherValue", Child<TMP_Text>(weather.transform, "Value Row/Value"));
            Ref(so, "weatherIcon", Child<Image>(weather.transform, "Value Row/Icon"));
            Image weatherHit = GetOrAdd<Image>(weather);
            weatherHit.color = Color.clear;
            weatherHit.raycastTarget = true;
            MemoriTooltipTrigger weatherTooltip = weather.AddComponent<MemoriTooltipTrigger>();
            weatherTooltip.enabled = false;
            Ref(so, "weatherTooltip", weatherTooltip);
            GameObject yours = Cell(strip, "Your Army", "engagementYourArmy", null, Color.white, true);
            Ref(so, "yourArmyValue", Child<TMP_Text>(yours.transform, "Value Row/Value"));
            GameObject enemy = Cell(strip, "Enemy", "engagementEnemy", null, Color.white, true);
            Ref(so, "enemyValue", Child<TMP_Text>(enemy.transform, "Value Row/Value"));

            RectTransform warning = Rect("Reserve Warning", body);
            HLayout(warning, 9f, TextAnchor.MiddleLeft, new RectOffset());
            Image heart = Img(Rect("Icon", warning), heartIcon, Negative);
            heart.preserveAspect = true;
            Fixed(heart.gameObject, 15f, 15f);
            Localize(Text("Text", warning, display, 16f, Negative, "Warning"), "engagementReserveWarning");
            warning.gameObject.SetActive(false);
            Ref(so, "reserveWarning", warning.gameObject);

            RectTransform actions = Rect("Actions", body);
            HLayout(actions, 16f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(actions.gameObject, -1f, ButtonHeight);

            RectTransform heavensong = Rect("Heavensong", actions);
            HLayout(heavensong, 12f, TextAnchor.MiddleLeft, new RectOffset());
            Flexible(heavensong.gameObject, 1f).preferredWidth = 0f;
            GameObject reroll = Instance(standardButton, heavensong, "Reroll");
            Fixed(reroll, 132f, 44f);
            TMP_Text rerollLabel = Child<TMP_Text>(reroll.transform, "Button Label");
            Localize(rerollLabel, "Reroll");
            rerollLabel.alignment = TextAlignmentOptions.Center;
            rerollLabel.margin = new Vector4(10f, 0f, 10f, 0f);
            MemoriTooltipTrigger rerollTooltip = reroll.AddComponent<MemoriTooltipTrigger>();
            TMP_Text note = Text("Note", heavensong, display, 14f, Flavour, "Heavensong");
            note.fontStyle = FontStyles.Italic;
            Wrap(note);
            Flexible(note.gameObject, 1f).preferredWidth = 0f;
            Localize(note, "engagementHeavensongLine");
            heavensong.gameObject.SetActive(false);
            Ref(so, "heavensongGroup", heavensong.gameObject);
            Ref(so, "heavensongButton", reroll.GetComponent<Button>());
            Ref(so, "heavensongTooltip", rerollTooltip);
            Spacer(actions, true);

            TMP_Text disabled = Text("Autoresolve Disabled", actions, display, 15f, Cap, "Autoresolve disabled");
            Localize(disabled, "difficultyModifier20");
            disabled.gameObject.SetActive(false);
            Ref(so, "autoResolveDisabled", disabled.gameObject);

            GameObject auto = Instance(primaryButton, actions, "Autoresolve");
            Fixed(auto, ButtonWidth, ButtonHeight);
            HueButton(auto, AutoResolveHue, speedIcon, new Vector2(60f, 60f), -52.9f);
            Localize(Child<TMP_Text>(auto.transform, "Button Label"), "Autoresolve");
            TMP_Text prediction = Child<TMP_Text>(auto.transform, "Secondary Label");
            prediction.gameObject.SetActive(true);
            Unlocalize(prediction, "Predicts Defeat");
            prediction.font = display;
            prediction.fontSize = 15f;
            prediction.color = PredictionGrey;
            prediction.margin = new Vector4(20f, 42f, 70f, 0f);
            prediction.richText = true;
            AutoResolvePreview preview = auto.AddComponent<AutoResolvePreview>();
            MemoriTooltipTrigger autoTooltip = auto.AddComponent<MemoriTooltipTrigger>();
            Ref(so, "autoResolveButton", auto.GetComponent<Button>());
            Ref(so, "autoResolvePrediction", prediction);
            Ref(so, "autoResolvePreview", preview);
            Ref(so, "autoResolveTooltip", autoTooltip);

            GameObject fight = Instance(primaryButton, actions, "Fight Battle");
            Fixed(fight, ButtonWidth, ButtonHeight);
            HueButton(fight, FightHue, swordsIcon, new Vector2(50f, 45f), -45f);
            Localize(Child<TMP_Text>(fight.transform, "Button Label"), "Fight Battle");
            MemoriTooltipTrigger fightTooltip = fight.AddComponent<MemoriTooltipTrigger>();
            Ref(so, "fightButton", fight.GetComponent<Button>());
            Ref(so, "fightTooltip", fightTooltip);
        }

        static void BuildResult(RectTransform root, SerializedObject so)
        {
            RectTransform body = Rect("Result", root);
            VLayout(body, 14f, new RectOffset(24, 24, 16, 22));
            Ref(so, "resultBody", body.gameObject);

            TMP_Text outcome = Text("Outcome", body, display, 17f, Cream, "Your warband is broken. This run ends here.");
            Unlocalize(outcome, "Outcome");
            Wrap(outcome);
            outcome.gameObject.SetActive(false);
            Ref(so, "outcomeLine", outcome);

            RectTransform head = Rect("Report Head", body);
            HLayout(head, 10f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(head.gameObject, -1f, 24f);
            Localize(CapLabel(head, "Caption"), "engagementBattleReport");
            Spacer(head, true);
            RectTransform stats = Rect("Detailed Stats", head);
            HLayout(stats, 8f, TextAnchor.MiddleRight, new RectOffset());
            Image statsHit = Img(stats, null, Color.clear);
            statsHit.raycastTarget = true;
            Image statsIcon = Img(Rect("Icon", stats), scrollIcon, Blue);
            statsIcon.preserveAspect = true;
            Fixed(statsIcon.gameObject, 18f, 18f);
            TMP_Text statsText = Text("Text", stats, displayDrop, 15f, Cream, "Detailed stats");
            statsText.fontStyle = FontStyles.Underline;
            Localize(statsText, "engagementDetailedStats");
            MemoriTooltipTrigger statsTooltip = stats.gameObject.AddComponent<MemoriTooltipTrigger>();
            Ref(so, "detailedStats", statsTooltip);

            RectTransform report = Rect("Report Row", body);
            HLayout(report, 16f, TextAnchor.MiddleLeft, new RectOffset());
            RectTransform fallen = Rect("Fallen Slot", report);
            Img(fallen, solid, Well);
            Image fallenEdge = Img(Stretch(Rect("Edge", fallen)), squareSliced, WellEdge, Image.Type.Sliced);
            fallenEdge.fillCenter = false;
            Fixed(fallen.gameObject, 390f, 81f);
            Ref(so, "fallenSlot", fallen);
            RectTransform strip = Rect("Strip", report);
            Fixed(strip.gameObject, -1f, 62f);
            Flexible(strip.gameObject, 1f).preferredWidth = 0f;
            StripRules(strip);
            HorizontalLayoutGroup row = HLayout(strip, 0f, TextAnchor.MiddleLeft, new RectOffset());
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            var labels = new Object[4];
            var values = new Object[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject cell = Instance(statCellPart, strip, "Cell " + (i + 1));
                if (i == 0) cell.transform.Find("Divider").gameObject.SetActive(false);
                Child<Image>(cell.transform, "Value Row/Icon").gameObject.SetActive(false);
                TMP_Text label = Child<TMP_Text>(cell.transform, "Label");
                Unlocalize(label, "Label");
                TMP_Text value = Child<TMP_Text>(cell.transform, "Value Row/Value");
                Unlocalize(value, "Value");
                value.richText = true;
                labels[i] = label;
                values[i] = value;
            }
            Refs(so, "reportLabels", labels);
            Refs(so, "reportValues", values);

            RectTransform stacks = Rect("Stacks", body);
            HLayout(stacks, 24f, TextAnchor.UpperLeft, new RectOffset()).childForceExpandHeight = true;
            Ref(so, "stacks", stacks.gameObject);
            RectTransform spoilRows = Column(stacks, "Spoils", "engagementSpoils", "engagementTakeAll");
            Ref(so, "spoilsParent", spoilRows);
            RectTransform divider = Rect("Divider", stacks);
            Fixed(divider.gameObject, 1f, -1f);
            Img(Stretch(Rect("Line", divider), 0f, 0f, 6f, 6f), null, A(Brass, 0.55f));
            RectTransform choiceRows = Column(stacks, "Spoils Of War", "engagementSpoilsOfWar", "engagementChooseOne");
            Ref(so, "choicesParent", choiceRows);

            // A thin brass thread joins the choice diamonds, 20.5 px in from the row's left edge.
            Image thread = Img(Rect("Thread", choiceRows), null, A(Brass, 0.55f));
            Ignore(thread.gameObject);
            RectTransform threadRect = thread.rectTransform;
            threadRect.anchorMin = threadRect.anchorMax = new Vector2(0f, 1f);
            threadRect.pivot = new Vector2(0.5f, 1f);
            threadRect.sizeDelta = new Vector2(1f, 2f * (RowHeight + RowGap));
            threadRect.anchoredPosition = new Vector2(20.5f, -RowHeight / 2f);
            thread.gameObject.SetActive(false);
            Ref(so, "choiceThread", threadRect);

            TMP_Text none = Text("None", choiceRows, display, 15f, Flavour, "Nothing to choose");
            none.fontStyle = FontStyles.Italic;
            Localize(none, "engagementNoChoices");
            none.gameObject.SetActive(false);
            Ref(so, "noChoices", none.gameObject);
        }

        // A stack column: its caption and note, then the rows. The rows grow with their content; the stacks row keeps both columns one height.
        static RectTransform Column(RectTransform parent, string name, string captionKey, string noteKey)
        {
            RectTransform column = Rect(name, parent);
            VLayout(column, 10f, new RectOffset());
            Fixed(column.gameObject, ColumnWidth, -1f);
            RectTransform head = Rect("Head", column);
            HLayout(head, 10f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(head.gameObject, -1f, 18f);
            Localize(CapLabel(head, "Caption"), captionKey);
            Spacer(head, true);
            Localize(Text("Note", head, display, 14f, Cap, "Note"), noteKey);
            RectTransform rows = Rect("Rows", column);
            VLayout(rows, RowGap, new RectOffset());
            GetOrAdd<LayoutElement>(rows.gameObject).minHeight = RowHeight;
            return rows;
        }

        // One Town Stat Cell with its caption and an optional icon; values are filled at runtime.
        static GameObject Cell(RectTransform strip, string name, string captionKey, Sprite icon, Color tint, bool divider)
        {
            GameObject cell = Instance(statCellPart, strip, name);
            cell.transform.Find("Divider").gameObject.SetActive(divider);
            Localize(Child<TMP_Text>(cell.transform, "Label"), captionKey);
            Image image = Child<Image>(cell.transform, "Value Row/Icon");
            image.sprite = icon;
            image.color = tint;
            image.gameObject.SetActive(icon != null);
            Unlocalize(Child<TMP_Text>(cell.transform, "Value Row/Value"), "Value");
            return cell;
        }
        #endregion

        #region Widgets
        // The map's action button format: the button's own hue on every layer, a white label top-left and a hue-tinted icon on the right.
        static void HueButton(GameObject button, Color[] hue, Sprite iconSprite, Vector2 iconSize, float iconX)
        {
            Rgb(Child<Image>(button.transform, "Background/UI Assets/SPR_Background"), hue[0]);
            Rgb(Child<Image>(button.transform, "Background/UI Assets/Gradient"), hue[1]);
            Rgb(Child<Image>(button.transform, "Background/UI Assets/Texture"), hue[2]);
            Rgb(Child<Image>(button.transform, "Background/UI Assets/Highlight Image"), hue[3]);
            Rgb(Child<Image>(button.transform, "Background/UI Assets/Selection Highlight"), hue[4]);
            Rgb(Child<Image>(button.transform, "Focus/Pointer"), hue[5]);
            Image icon = Child<Image>(button.transform, "Icon");
            icon.sprite = iconSprite;
            icon.color = hue[6];
            icon.preserveAspect = true;
            icon.rectTransform.sizeDelta = iconSize;
            icon.rectTransform.anchoredPosition = new Vector2(iconX, 0f);
            TMP_Text label = Child<TMP_Text>(button.transform, "Button Label");
            label.color = White;
            label.fontSize = 22f;
            // One line that shrinks, so a long label (German) never wraps into the prediction below it.
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 22f;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.margin = new Vector4(20f, 10f, 70f, 0f);
        }

        static void Rgb(Image image, Color colour)
        {
            if (image != null) image.color = new Color(colour.r, colour.g, colour.b, image.color.a);
        }

        static void StripRules(RectTransform strip)
        {
            Image top = Img(Rect("Top", strip), solid, A(Brass, 0.4f));
            Anchor(top.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -1f), Vector2.zero);
            Ignore(top.gameObject);
            Image bottom = Img(Rect("Bottom", strip), solid, A(Brass, 0.4f));
            Anchor(bottom.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            Ignore(bottom.gameObject);
        }

        static TMP_Text CapLabel(RectTransform parent, string name)
        {
            TMP_Text label = Text(name, parent, display, 12f, Cap, name);
            label.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            label.characterSpacing = 10f;
            return label;
        }

        // Row text shrinks to fit its line in every locale; the tag rides the title line so the detail keeps the full width.
        static void Shrink(TMP_Text label, float min)
        {
            Flexible(label.gameObject, 1f).preferredWidth = 0f;
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = label.fontSize;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        static void Spacer(RectTransform parent, bool horizontal)
        {
            LayoutElement spacer = Rect("Spacer", parent).gameObject.AddComponent<LayoutElement>();
            if (horizontal) spacer.flexibleWidth = 1f;
            else spacer.flexibleHeight = 1f;
        }

        static RectTransform Mount(RectTransform parent, string name, float size, Sprite icon, float iconSize)
        {
            RectTransform cell = Rect(name, parent);
            Fixed(cell.gameObject, size, size);
            // The mount art has transparent margins, so it is drawn a little larger than its cell, as in the tooltip.
            Image diamond = Img(Rect("Diamond", cell), mount, Color.white);
            Centre(diamond.rectTransform, size * 52f / 44f, size * 52f / 44f);
            Image image = Img(Rect("Icon", cell), icon, Gold);
            image.preserveAspect = true;
            Centre(image.rectTransform, iconSize, iconSize);
            return cell;
        }

        static GameObject Instance(GameObject prefab, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.layer = 5;
            return instance;
        }

        static T Child<T>(Transform root, string path) where T : Component
        {
            Transform child = root.Find(path);
            T component = child != null ? child.GetComponent<T>() : null;
            if (component == null) Debug.LogError($"EngagementPanelBuilder: {root.name} has no {typeof(T).Name} at '{path}'.");
            return component;
        }

        static void Wrap(TMP_Text text)
        {
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        static void Ignore(GameObject go) => GetOrAdd<LayoutElement>(go).ignoreLayout = true;
        #endregion

        #region Primitives
        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static RectTransform Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void Centre(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }

        static Image Img(RectTransform rect, Sprite sprite, Color colour, Image.Type type = Image.Type.Simple)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.type = sprite != null ? type : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, Color colour, string text)
        {
            TextMeshProUGUI label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = colour;
            label.text = text;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.richText = true;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        static VerticalLayoutGroup VLayout(Transform target, float spacing, RectOffset padding)
        {
            VerticalLayoutGroup layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            return layout;
        }

        static HorizontalLayoutGroup HLayout(Transform target, float spacing, TextAnchor alignment, RectOffset padding)
        {
            HorizontalLayoutGroup layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        static LayoutElement Flexible(GameObject go, float width)
        {
            LayoutElement element = GetOrAdd<LayoutElement>(go);
            element.flexibleWidth = width;
            return element;
        }

        static void Fixed(GameObject go, float width, float height)
        {
            LayoutElement element = GetOrAdd<LayoutElement>(go);
            if (width >= 0f) { element.minWidth = width; element.preferredWidth = width; element.flexibleWidth = 0f; }
            if (height >= 0f) { element.minHeight = height; element.preferredHeight = height; element.flexibleHeight = 0f; }
            ((RectTransform)go.transform).sizeDelta = new Vector2(width >= 0f ? width : 100f, height >= 0f ? height : 30f);
        }

        static void Ref(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"EngagementPanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.objectReferenceValue = value;
        }

        static void Refs(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"EngagementPanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        #endregion
    }
}
