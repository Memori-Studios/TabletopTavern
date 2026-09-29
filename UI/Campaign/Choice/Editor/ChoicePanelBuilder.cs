using System;
using System.Collections.Generic;
using TJ.Ordeals;
using TJ.Prestige;
using TJ.Recruit;
using TJ.Treasure;
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

namespace TJ.Choice.EditorTools
{
    /// <summary>
    /// Generates the pick-one panels (the Prestige III trait picker, the Ordeal picker and the Treasure gear picker) and their
    /// shared card parts on the game's Basic Background, and installs them in Map.unity. Rebuilding a panel keeps hand edits
    /// to the card parts.
    /// </summary>
    public static class ChoicePanelBuilder
    {
        public const string PartFolder = "Assets/Data/Prefabs/UI/Map/Choice";
        public const string CardPath = PartFolder + "/Choice Card.prefab";
        public const string WideCardPath = PartFolder + "/Choice Wide Card.prefab";
        public const string PrestigePanelPath = PartFolder + "/Prestige Panel UI.prefab";
        public const string OrdealPanelPath = PartFolder + "/Ordeal Panel UI.prefab";
        public const string TreasurePanelPath = PartFolder + "/Treasure Panel UI.prefab";
        const string ScenePath = "Assets/Scenes/Map.unity";
        const string BasicBackgroundPath = "Assets/Data/Prefabs/UI/Reuseable/Basic Background.prefab";
        const string ButtonBasePath = "Assets/Data/Prefabs/UI/Reuseable/Buttons/Button Base.prefab";
        const string RecruitCardPath = "Assets/Data/Prefabs/UI/Recruit/Recruit Card.prefab";
        const string OrdealIconPath = "Assets/Data/Prefabs/UI/Map/Ordeal Panel/Ordeal Icon.prefab";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";
        const string TableName = "MainLocalizationTable";
        // The old UI under each panel root. The backdrop (fader, edge shades, glow dots) and the unit camera rig stay.
        static readonly string[] OldPrestigeChildren = { "Recruit Render", "Prestige Panel Title", "Squad Battle Info", "Trait Card Parent" };
        static readonly string[] OldOrdealChildren = { "Ordeal Panel Title", "Ordeal Card Parent" };
        // Under Treasure Panel/Map Treasure Panel. The title banner (Map Panel Title) stays.
        static readonly string[] OldTreasureChildren = { "Gear Card Parent" };

        static readonly Vector2 PrestigeCardSize = new(330f, 470f);
        static readonly Vector2 OrdealCardSize = new(360f, 560f);
        static readonly Vector2 RecruitCardSize = new(275f, 580f);
        const float TitleWidth = 1400f;
        const float PrestigeTitleHeight = 114f;
        const float OrdealTitleHeight = 154f;
        const float StackGap = 30f;
        // The stack sits a little above centre, so the cards clear the army bar under the fader.
        const float StackLift = 40f;
        const float PrestigeCardGap = 32f;
        const float OrdealCardGap = 36f;
        const float RecruitGap = 48f;
        const float CardPadding = 28f;

        #region Treasure layout
        // Gaps sized so a hovered card (1.15, lifted 20, with its halo) clears the count line above and the Or line below.
        static readonly Vector2 TreasureCardSize = new(330f, 400f);
        static readonly Vector2 WideCardSize = new(560f, 104f);
        const float TreasureCardGap = 36f;
        const float CountHeight = 24f;
        const float CountToCards = 72f;
        const float CardsToOr = 26f;
        const float OrHeight = 24f;
        const float OrToWide = 44f;
        const float OrWidth = 620f;
        const float WideFooterWidth = 170f;
        const float TreasureStackWidth = 1100f;
        // The stack sits on the army panel's top edge (273) plus a margin, and rises with it on a shorter canvas.
        const float TreasureStackBottom = 282f;
        // The scene's title banner owns the top 100 units; the count line keeps 4 clear of it.
        const float BannerHeight = 104f;
        const float WideMountSize = 84f;
        static float TreasureStackHeight => CountHeight + CountToCards + TreasureCardSize.y + CardsToOr + OrHeight + OrToWide + WideCardSize.y;
        static float WideBlockHeight => CardsToOr + OrHeight + OrToWide + WideCardSize.y;
        #endregion

        #region Style
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        static readonly Color Sub = Hex("D8CFBC");
        static readonly Color Cap = Hex("8C9AA2");
        static readonly Color Mute = Hex("6C777B");
        static readonly Color Warning = Hex("E3695E");
        static readonly Color Slate = Hex("1F2B2E");
        static readonly Color FocusBlue = Hex("3FB6FF", 0.35f);
        static readonly Color PickedGold = Hex("E9C06A", 0.7f);
        static readonly Color HoverTintColour = Hex("3FB6E0", 0.07f);
        // The centre glow peaks stronger than the old flat fill, since it fades to nothing at the sides.
        const float BandGlowAlpha = 0.22f;
        const float BandHeight = 44f;
        const float BandRuleInset = 30f;

        static TMP_FontAsset displayDrop, display;
        static Sprite mount, solid, glow, edgeFade;
        static Image.Type glowType;
        static float glowPixelsPerUnit;
        static Sprite prestigeIcon, swordsIcon, lockIcon, flagIcon, goldIcon;
        static GameObject basicBackground, recruitCardPrefab, ordealIconPrefab;

        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            var sheet = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite) sheet[sprite.name] = sprite;
            sheet.TryGetValue("TooltipMount", out mount);
            sheet.TryGetValue("TooltipSolid", out solid);
            if (mount == null || solid == null) Debug.LogError("ChoicePanelBuilder: tooltip sheet sprites missing.");

            // The focus glow of the button family, so a focused card reads like a focused button.
            GameObject buttonBase = Load<GameObject>(ButtonBasePath);
            Transform highlight = buttonBase != null ? buttonBase.transform.Find("Background/UI Assets/Selection Highlight") : null;
            Image highlightImage = highlight != null ? highlight.GetComponent<Image>() : null;
            if (highlightImage == null) Debug.LogError("ChoicePanelBuilder: Button Base has no Selection Highlight image.");
            glow = highlightImage != null ? highlightImage.sprite : null;
            glowType = highlightImage != null ? highlightImage.type : Image.Type.Simple;
            glowPixelsPerUnit = highlightImage != null ? highlightImage.pixelsPerUnitMultiplier : 1f;

            edgeFade = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Shadow/Vertical Shadow.png");
            prestigeIcon = Load<Sprite>("Assets/Art/Icons/Events/PrestigeUnit.png");
            swordsIcon = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/Icons_Status/ICON_FantasyWarrior_Status_Attack02_Clean.png");
            lockIcon = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Lock01_Clean.png");
            flagIcon = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Flag01_Clean.png");
            goldIcon = Load<Sprite>("Assets/Art/Icons/Events/Gold.png");
            basicBackground = Load<GameObject>(BasicBackgroundPath);
            recruitCardPrefab = Load<GameObject>(RecruitCardPath);
            ordealIconPrefab = Load<GameObject>(OrdealIconPath);
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"ChoicePanelBuilder: missing {typeof(T).Name} at {path}");
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
        // English source text for the keys these panels add. Other locales are filled in separately.
        static readonly (string key, string english)[] Keys =
        {
            ("prestigePickTitle", "Prestige {0}"),
            ("choiceClickToLearn", "Click to learn"),
            ("choiceClickToTake", "Click to take"),
            ("choiceLearned", "Learned"),
            ("choiceTaken", "Taken"),
            ("ordealRenownLine", "Renown: x{0} now, x{1} after this pick"),
            ("ordealHeld", "Held:"),
            ("ordealCardStopsWorking", "Stops working: {0}."),
            ("ordealCardFactionOff", "Your {0} faction bonus stops working."),
            ("ordealCardGoldStack", "With your other Ordeals you would lose {0} <sprite name=GoldSprite> every turn."),
            ("ordealCardRedrawsMap", "Redraws this act's map."),
            ("treasureGear", "Gear"),
            ("treasureConsumables", "Consumables"),
        };

        static StringTableCollection collection;
        static StringTable english;

        static void LoadTable()
        {
            collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            english = (StringTable)collection.GetTable("en");
        }

        [MenuItem("Tabletop Tavern/Choice Panels/Add Text Keys")]
        public static void AddKeysMenu() => Debug.Log("ChoicePanelBuilder: " + EnsureKeys());

        /// <summary>Adds any missing key with its English text. Existing keys and their text are left alone.</summary>
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
            if (entry == null) throw new InvalidOperationException($"ChoicePanelBuilder: no localization key '{key}'.");
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
            var so = new SerializedObject(localizer);
            so.FindProperty("m_StringReference.m_TableReference.m_TableCollectionName").stringValue = "GUID:" + collection.SharedData.TableCollectionNameGuid.ToString("N");
            so.FindProperty("m_StringReference.m_TableEntryReference.m_KeyId").longValue = KeyId(key);
            so.FindProperty("m_StringReference.m_TableEntryReference.m_Key").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            text.text = English(key);
        }
        #endregion

        #region Entry points
        enum PanelKind { Prestige, Ordeal, Treasure }

        /// <summary>Adds missing keys, creates the card parts if they are missing, then rebuilds every panel from them.</summary>
        [MenuItem("Tabletop Tavern/Choice Panels/Rebuild Prefabs")]
        public static void BuildPrefabs()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            EnsureCard(false);
            EnsureWideCard(false);
            BuildPanel(PrestigePanelPath, "Prestige Panel UI", PanelKind.Prestige);
            BuildPanel(OrdealPanelPath, "Ordeal Panel UI", PanelKind.Ordeal);
            BuildPanel(TreasurePanelPath, "Treasure Panel UI", PanelKind.Treasure);
        }

        /// <summary>Builds only the Treasure panel and what it needs, leaving the Prestige and Ordeal panels as they are.</summary>
        [MenuItem("Tabletop Tavern/Choice Panels/Rebuild Treasure Panel")]
        public static void BuildTreasurePanel()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            EnsureCard(false);
            EnsureWideCard(false);
            BuildPanel(TreasurePanelPath, "Treasure Panel UI", PanelKind.Treasure);
        }

        [MenuItem("Tabletop Tavern/Choice Panels/Reset Card Part")]
        static void ResetCardMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset the choice cards",
                    $"{CardPath} and {WideCardPath} are rebuilt from code, which discards your edits to them. Every panel is rebuilt after.",
                    "Reset", "Cancel"))
                return;
            ResetCard();
        }

        /// <summary>Rebuilds the card parts from code, then every panel.</summary>
        public static void ResetCard()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            EnsureCard(true);
            EnsureWideCard(true);
            BuildPanel(PrestigePanelPath, "Prestige Panel UI", PanelKind.Prestige);
            BuildPanel(OrdealPanelPath, "Ordeal Panel UI", PanelKind.Ordeal);
            BuildPanel(TreasurePanelPath, "Treasure Panel UI", PanelKind.Treasure);
        }

        [MenuItem("Tabletop Tavern/Choice Panels/Install In Map")]
        public static void InstallMenu() => Debug.Log("ChoicePanelBuilder: " + InstallInMap());

        [MenuItem("Tabletop Tavern/Choice Panels/Install Treasure In Map")]
        public static void InstallTreasureMenu() => Debug.Log("ChoicePanelBuilder: " + InstallTreasureInMap());

        static void BuildPanel(string path, string name, PanelKind kind)
        {
            // Rebuild inside the existing prefab so the root keeps its id; the Map scene instance references it.
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            Scene stage = default;
            GameObject root;
            if (existing) root = PrefabUtility.LoadPrefabContents(path);
            else
            {
                // A first build happens in a preview scene so no open scene is marked changed.
                stage = EditorSceneManager.NewPreviewScene();
                root = new GameObject(name, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
            }
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                if (kind == PanelKind.Treasure) BuildTreasureLayout(root);
                else BuildPanelLayout(root, kind == PanelKind.Prestige);
                Normalize(root);
                RecordOverrides(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"ChoicePanelBuilder: wrote {path}");
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
        /// Replaces the old UI under Map.unity's Prestige Panel and Ordeal Panel with the prefabs, points both panels at them,
        /// frames the Prestige unit camera like a town recruit's, and saves only Map.unity.
        /// </summary>
        public static string InstallInMap()
        {
            GameObject prestigePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrestigePanelPath);
            GameObject ordealPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrdealPanelPath);
            if (prestigePrefab == null || ordealPrefab == null) return "missing panel prefabs; run Rebuild Prefabs first.";
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
                PrestigeTraitPanel prestige = Find<PrestigeTraitPanel>(map);
                OrdealPanel ordeal = Find<OrdealPanel>(map);
                if (prestige == null || ordeal == null) return "no PrestigeTraitPanel or OrdealPanel in Map.unity.";

                List<Transform> oldPrestige = Children(prestige.transform, OldPrestigeChildren);
                List<Transform> oldOrdeal = Children(ordeal.transform, OldOrdealChildren);
                var allOld = new List<Transform>(oldPrestige);
                allOld.AddRange(oldOrdeal);
                string outside = CheckReferences(map, allOld, prestige, ordeal);
                if (!string.IsNullOrEmpty(outside)) return "stopped, outside references into the old UI:\n" + outside;

                GameObject prestigeUI = Replace(prestige.transform, oldPrestige, prestigePrefab);
                GameObject ordealUI = Replace(ordeal.transform, oldOrdeal, ordealPrefab);

                // The old title banners held the open animation; the new title block animates itself.
                var prestigeSo = new SerializedObject(prestige);
                Ref(prestigeSo, "view", prestigeUI.GetComponent<ChoicePanelView>());
                Ref(prestigeSo, "OpenFeedback", null);
                prestigeSo.ApplyModifiedPropertiesWithoutUndo();
                var ordealSo = new SerializedObject(ordeal);
                Ref(ordealSo, "view", ordealUI.GetComponent<ChoicePanelView>());
                Ref(ordealSo, "OpenFeedback", null);
                ordealSo.ApplyModifiedPropertiesWithoutUndo();

                string rig = MatchRecruitRig(map, prestige);

                EditorSceneManager.MarkSceneDirty(map);
                if (!EditorSceneManager.SaveScene(map)) return "Map.unity did not save.";
                return $"installed; removed {oldPrestige.Count} + {oldOrdeal.Count} old objects; {rig}";
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(map, true);
            }
        }

        /// <summary>
        /// Replaces the old gear rows (Map Treasure Panel/Gear Card Parent) with the Treasure panel prefab, points
        /// TreasurePanel at it, and saves only Map.unity. The chest reveal and the title banner are left as they are.
        /// </summary>
        public static string InstallTreasureInMap()
        {
            GameObject treasurePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreasurePanelPath);
            if (treasurePrefab == null) return "missing the Treasure panel prefab; run Rebuild Treasure Panel first.";
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
                TreasurePanel treasure = Find<TreasurePanel>(map);
                if (treasure == null) return "no TreasurePanel in Map.unity.";
                Transform group = treasure.transform.Find("Map Treasure Panel");
                if (group == null) return "no Map Treasure Panel under the TreasurePanel.";

                List<Transform> old = Children(group, OldTreasureChildren);
                // Install runs once; a second run would only rewrite the scene instance.
                if (old.Count == 0 && new SerializedObject(treasure).FindProperty("view").objectReferenceValue != null)
                    return "already installed.";
                string outside = CheckReferences(map, old, treasure);
                if (!string.IsNullOrEmpty(outside)) return "stopped, outside references into the old UI:\n" + outside;

                GameObject treasureUI = Replace(group, old, treasurePrefab);
                var so = new SerializedObject(treasure);
                Ref(so, "view", treasureUI.GetComponent<ChoicePanelView>());
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(map);
                if (!EditorSceneManager.SaveScene(map)) return "Map.unity did not save.";
                return $"installed the Treasure panel; removed {old.Count} old objects.";
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(map, true);
            }
        }

        static List<Transform> Children(Transform root, string[] names)
        {
            var found = new List<Transform>();
            foreach (string name in names)
            {
                Transform child = root.Find(name);
                if (child != null) found.Add(child);
            }
            return found;
        }

        // Swaps the old children for one prefab instance in the same place in the draw order, above the backdrop.
        static GameObject Replace(Transform panel, List<Transform> old, GameObject prefab)
        {
            int index = panel.childCount;
            foreach (Transform child in old)
            {
                index = Mathf.Min(index, child.GetSiblingIndex());
                Object.DestroyImmediate(child.gameObject);
            }
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                GameObject child = panel.GetChild(i).gameObject;
                if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(child) == prefab)
                {
                    index = Mathf.Min(index, i);
                    Object.DestroyImmediate(child);
                }
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel);
            instance.transform.SetSiblingIndex(Mathf.Min(index, panel.childCount - 1));
            return instance;
        }

        /// <summary>
        /// Puts the Prestige panel's unit camera and lights where a town recruit's are, relative to the unit, so the squad
        /// looks the same on the recruit card as it does when recruited.
        /// </summary>
        static string MatchRecruitRig(Scene map, PrestigeTraitPanel prestige)
        {
            RecruitmentScene recruitment = Find<RecruitmentScene>(map);
            if (recruitment == null) return "no RecruitmentScene, unit camera left as it was.";
            var recruitSo = new SerializedObject(recruitment);
            var holder = recruitSo.FindProperty("recruitmentPrefabHolder1").objectReferenceValue as Transform;
            var camera = recruitSo.FindProperty("recruitmentCamera1").objectReferenceValue as Camera;
            var key = recruitSo.FindProperty("recruitmentLight1").objectReferenceValue as Light;
            var back = recruitSo.FindProperty("recruitmentBlueLight1").objectReferenceValue as Light;
            var prestigeSo = new SerializedObject(prestige);
            var prestigeHolder = prestigeSo.FindProperty("prefabHolder").objectReferenceValue as Transform;
            var prestigeCamera = prestigeSo.FindProperty("troopCamera").objectReferenceValue as Camera;
            var prestigeLights = prestigeSo.FindProperty("troopLights").objectReferenceValue as GameObject;
            if (holder == null || camera == null || key == null || back == null || prestigeHolder == null || prestigeCamera == null || prestigeLights == null)
                return "recruit or prestige rig incomplete, unit camera left as it was.";

            Place(prestigeCamera.transform, camera.transform, holder, prestigeHolder);
            prestigeCamera.fieldOfView = camera.fieldOfView;

            Transform keyLight = prestigeLights.transform.Find("Spot Light");
            Transform fillLight = prestigeLights.transform.Find("Spot Light_1");
            Transform backLight = prestigeLights.transform.Find("BlueBacklight");
            if (keyLight == null || backLight == null) return "camera framed; prestige lights not found, left as they were.";
            keyLight.gameObject.SetActive(true);
            CopyLight(keyLight, key, holder, prestigeHolder);
            CopyLight(backLight, back, holder, prestigeHolder);
            if (fillLight != null) fillLight.gameObject.SetActive(false);
            return "unit camera and lights now match a town recruit.";
        }

        // Gives target the same pose relative to its holder that source has relative to its own. The prestige side stays in
        // local space: its holder sits under the Map Canvas, whose scale is zero while the Editor has not drawn it.
        static void Place(Transform target, Transform source, Transform sourceHolder, Transform targetHolder)
        {
            Vector3 position = sourceHolder.InverseTransformPoint(source.position);
            Quaternion rotation = Quaternion.Inverse(sourceHolder.rotation) * source.rotation;
            // The lights sit one group below the holder, so undo that group's own pose.
            if (target.parent != targetHolder)
            {
                Transform group = target.parent;
                Quaternion groupInverse = Quaternion.Inverse(group.localRotation);
                position = groupInverse * (position - group.localPosition);
                rotation = groupInverse * rotation;
            }
            target.localPosition = position;
            target.localRotation = rotation;
        }

        static void CopyLight(Transform target, Light source, Transform sourceHolder, Transform targetHolder)
        {
            Place(target, source.transform, sourceHolder, targetHolder);
            Light light = target.GetComponent<Light>();
            if (light == null) return;
            light.type = source.type;
            light.color = source.color;
            light.intensity = source.intensity;
            light.range = source.range;
            light.spotAngle = source.spotAngle;
            light.innerSpotAngle = source.innerSpotAngle;
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

        // Anything outside the old UI that points into it would lose its target when the old UI is deleted.
        static string CheckReferences(Scene scene, List<Transform> old, params Component[] owners)
        {
            var inside = new HashSet<Object>();
            foreach (Transform root in old)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    inside.Add(t.gameObject);
                    foreach (Component component in t.GetComponents<Component>())
                        if (component != null) inside.Add(component);
                }
            var skip = new HashSet<Component>(owners);
            var report = new System.Text.StringBuilder();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (inside.Contains(t.gameObject)) continue;
                    foreach (Component component in t.GetComponents<Component>())
                    {
                        if (component == null || skip.Contains(component)) continue;
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

        #region Card part
        static GameObject cardPart;

        static GameObject wideCardPart;

        static void EnsureCard(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(PartFolder)) AssetDatabase.CreateFolder("Assets/Data/Prefabs/UI/Map", "Choice");
            cardPart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
            if (cardPart == null) cardPart = SavePart(CardPath, "Choice Card", CardPart);
            else
            {
                AddNewChip();
                AddBandGlow();
            }
        }

        // Swaps a card part's inset band fill for the centre glow under the frame, in place, keeping hand edits to the part.
        static void AddBandGlow()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CardPath);
            try
            {
                var so = new SerializedObject(root.GetComponent<ChoiceCardView>());
                if (so.FindProperty("bandGlow").objectReferenceValue != null) return;
                var background = root.transform.Find("Pop/Lift/Basic Background") as RectTransform;
                var band = root.transform.Find("Pop/Lift/Band") as RectTransform;
                if (background == null || band == null) { Debug.LogError("ChoicePanelBuilder: Choice Card has no Basic Background or Band; glow not added."); return; }
                foreach (string old in new[] { "Fill", "Rule" })
                {
                    Transform child = band.Find(old);
                    if (child != null) Object.DestroyImmediate(child.gameObject);
                }
                Anchor(band, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -BandHeight), Vector2.zero);
                (GameObject glow, Image left, Image right) = BandGlow(background);
                Ref(so, "bandGlow", glow);
                Ref(so, "bandFill", left);
                Ref(so, "bandFillRight", right);
                so.ApplyModifiedPropertiesWithoutUndo();
                Normalize(root);
                PrefabUtility.SaveAsPrefabAsset(root, CardPath);
                Debug.Log($"ChoicePanelBuilder: moved the band into a centre glow under the frame on {CardPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            cardPart = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        }

        // The rarity colour pools behind the band word and fades to both sides. It sits inside the Basic Background,
        // just before its frame, so the frame and corners draw over it. Two mirrored halves of the pack's edge fade.
        static (GameObject glow, Image left, Image right) BandGlow(RectTransform background)
        {
            RectTransform glow = Rect("Band Glow", background);
            Transform frame = background.Find("frame");
            if (frame != null) glow.SetSiblingIndex(frame.GetSiblingIndex());
            Anchor(glow, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -BandHeight), Vector2.zero);
            Image left = FadeHalf(glow, "Fill", A(Warning, BandGlowAlpha), true);
            Image right = FadeHalf(glow, "Fill Right", A(Warning, BandGlowAlpha), false);
            // The gold line under the glow; switch this object off to drop it.
            RectTransform rule = Rect("Rule", glow);
            Anchor(rule, Vector2.zero, new Vector2(1f, 0f), new Vector2(BandRuleInset, 0f), new Vector2(-BandRuleInset, 1f));
            FadeHalf(rule, "Left", A(Brass, 0.8f), true);
            FadeHalf(rule, "Right", A(Brass, 0.8f), false);
            return (glow.gameObject, left, right);
        }

        // One half of a centre-bright fade. The pack sprite is bright at its left edge, so the left half is mirrored.
        static Image FadeHalf(RectTransform parent, string name, Color colour, bool leftHalf)
        {
            Image image = Img(Rect(name, parent), edgeFade, colour);
            Anchor(image.rectTransform, new Vector2(leftHalf ? 0f : 0.5f, 0f), new Vector2(leftHalf ? 0.5f : 1f, 1f), Vector2.zero, Vector2.zero);
            if (leftHalf) image.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            return image;
        }

        static void EnsureWideCard(bool overwrite)
        {
            wideCardPart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(WideCardPath);
            if (wideCardPart == null) wideCardPart = SavePart(WideCardPath, "Choice Wide Card", WideCardPart);
        }

        // Adds the NEW chip to a card part built before it existed, in place, so hand edits to the part are kept.
        static void AddNewChip()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CardPath);
            try
            {
                var so = new SerializedObject(root.GetComponent<ChoiceCardView>());
                if (so.FindProperty("newChip").objectReferenceValue != null) return;
                var band = root.transform.Find("Pop/Lift/Band") as RectTransform;
                if (band == null) { Debug.LogError("ChoicePanelBuilder: Choice Card has no Pop/Lift/Band; NEW chip not added."); return; }
                Ref(so, "newChip", NewChip(band));
                so.ApplyModifiedPropertiesWithoutUndo();
                Normalize(root);
                PrefabUtility.SaveAsPrefabAsset(root, CardPath);
                Debug.Log($"ChoicePanelBuilder: added the NEW chip to {CardPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            cardPart = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        }

        // A small brass-edged tag at the right end of the band, for gear the player has never collected.
        static GameObject NewChip(RectTransform band)
        {
            RectTransform chip = Rect("New", band);
            chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(1f, 0.5f);
            chip.anchoredPosition = new Vector2(-12f, 0f);
            HLayout(chip, 0f, TextAnchor.MiddleCenter, new RectOffset(8, 8, 2, 1));
            ContentSizeFitter fitter = chip.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Image edge = Img(Stretch(Rect("Edge", chip)), solid, Brass);
            Ignore(edge.gameObject);
            Image fill = Img(Stretch(Rect("Fill", chip), 1f, 1f, 1f, 1f), solid, Hex("162023"));
            Ignore(fill.gameObject);
            TMP_Text label = CapLabel(chip, "Label");
            label.fontSize = 11.5f;
            label.color = Gold;
            label.alignment = TextAlignmentOptions.Center;
            Localize(label, "New");
            chip.gameObject.SetActive(false);
            return chip.gameObject;
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
                Debug.Log($"ChoicePanelBuilder: wrote {path}");
                return asset;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        // One standing card: a Basic Background with an optional group band, a mounted icon, name, text, run notes and a footer.
        static void CardPart(GameObject go)
        {
            var root = (RectTransform)go.transform;
            root.sizeDelta = PrestigeCardSize;
            Image hit = go.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.navigation = new Navigation { mode = Navigation.Mode.Horizontal };
            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = PrestigeCardSize.x;
            element.minHeight = element.preferredHeight = PrestigeCardSize.y;
            ChoiceCardView view = go.AddComponent<ChoiceCardView>();

            RectTransform pop = Stretch(Rect("Pop", root));
            CanvasGroup popGroup = pop.gameObject.AddComponent<CanvasGroup>();
            RectTransform lift = Stretch(Rect("Lift", pop));

            Image halo = Glow(lift, "Halo", FocusBlue, 12f);
            Image picked = Glow(lift, "Picked Glow", PickedGold, 20f);
            GameObject background = Instance(basicBackground, lift, "Basic Background");
            Stretch((RectTransform)background.transform);
            Image tint = Img(Stretch(Rect("Hover Tint", lift), 6f, 6f, 6f, 6f), null, HoverTintColour);

            (GameObject bandGlow, Image bandFill, Image bandFillRight) = BandGlow((RectTransform)background.transform);
            RectTransform band = Rect("Band", lift);
            Anchor(band, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -BandHeight), Vector2.zero);
            TMP_Text bandText = CapLabel(band, "Label");
            Stretch(bandText.rectTransform);
            bandText.alignment = TextAlignmentOptions.Center;
            GameObject newChip = NewChip(band);

            RectTransform content = Stretch(Rect("Content", lift));
            VerticalLayoutGroup layout = VLayout(content, 0f, new RectOffset((int)CardPadding, (int)CardPadding, 40, 26));
            layout.childAlignment = TextAnchor.UpperCenter;
            RectTransform mountCell = Mount(content, "Mount", 88f, prestigeIcon, 36f);
            Gap(content, 20f);
            TMP_Text name = Text("Name", content, displayDrop, 27f, Gold, "Name");
            name.alignment = TextAlignmentOptions.Center;
            Wrap(name);
            Gap(content, 16f);
            Rule(content, 150f, Slate);
            Gap(content, 18f);
            TMP_Text description = Text("Description", content, display, 18f, Cream, "Description");
            description.alignment = TextAlignmentOptions.Top;
            description.lineSpacing = 2f;
            Wrap(description);
            // Long languages shrink the text rather than run it into the notes.
            description.enableAutoSizing = true;
            description.fontSizeMin = 14f;
            description.fontSizeMax = 18f;

            RectTransform notes = Rect("Notes", content);
            VLayout(notes, 6f, new RectOffset(0, 0, 20, 0));
            Image notesRule = Img(Rect("Rule", notes), solid, A(Brass, 0.35f));
            Anchor(notesRule.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -11f), new Vector2(0f, -10f));
            Ignore(notesRule.gameObject);
            var noteIcons = new Object[3];
            var noteTexts = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform row = Rect("Note " + (i + 1), notes);
                HLayout(row, 8f, TextAnchor.UpperLeft, new RectOffset());
                Image icon = Img(Rect("Icon", row), lockIcon, Warning);
                icon.preserveAspect = true;
                Fixed(icon.gameObject, 15f, 15f);
                TMP_Text text = Text("Text", row, display, 14.5f, Warning, "Note");
                Wrap(text);
                text.enableAutoSizing = true;
                text.fontSizeMin = 12f;
                text.fontSizeMax = 14.5f;
                text.alignment = TextAlignmentOptions.TopLeft;
                Flexible(text.gameObject, 1f).preferredWidth = 0f;
                noteIcons[i] = icon;
                noteTexts[i] = text;
            }

            Spacer(content);
            TMP_Text footer = CapLabel(content, "Footer");
            footer.alignment = TextAlignmentOptions.Center;
            Fixed(footer.gameObject, -1f, 18f);
            RectTransform done = Rect("Done", content);
            HLayout(done, 10f, TextAnchor.MiddleCenter, new RectOffset());
            Fixed(done.gameObject, -1f, 26f);
            Diamond(done, "Diamond Left", 8f, Gold, Slate);
            TMP_Text doneText = Text("Text", done, displayDrop, 19f, Gold, "Learned");
            doneText.alignment = TextAlignmentOptions.Center;
            Diamond(done, "Diamond Right", 8f, Gold, Slate);
            done.gameObject.SetActive(false);

            var so = new SerializedObject(view);
            Ref(so, "button", button);
            Ref(so, "layoutElement", element);
            Ref(so, "pop", pop);
            Ref(so, "popGroup", popGroup);
            Ref(so, "lift", lift);
            Ref(so, "hoverHalo", halo);
            Ref(so, "hoverTint", tint);
            Ref(so, "pickedGlow", picked);
            Ref(so, "band", band.gameObject);
            Ref(so, "bandFill", bandFill);
            Ref(so, "bandFillRight", bandFillRight);
            Ref(so, "bandGlow", bandGlow);
            Ref(so, "bandText", bandText);
            Ref(so, "newChip", newChip);
            Ref(so, "contentLayout", layout);
            Ref(so, "icon", Child<Image>(mountCell, "Icon"));
            Ref(so, "nameText", name);
            Ref(so, "description", description);
            Ref(so, "notes", notes.gameObject);
            Refs(so, "noteIcons", noteIcons);
            Refs(so, "noteTexts", noteTexts);
            Ref(so, "footer", footer);
            Ref(so, "done", done.gameObject);
            Ref(so, "doneText", doneText);
            Ref(so, "warningIcon", lockIcon);
            Ref(so, "mapIcon", flagIcon);
            Ref(so, "goldIcon", goldIcon);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // One wide card in a row: the mounted icon, then the name over the text, then the footer. Same view as the tall card.
        static void WideCardPart(GameObject go)
        {
            var root = (RectTransform)go.transform;
            root.sizeDelta = WideCardSize;
            Image hit = go.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = WideCardSize.x;
            element.minHeight = element.preferredHeight = WideCardSize.y;
            ChoiceCardView view = go.AddComponent<ChoiceCardView>();

            RectTransform pop = Stretch(Rect("Pop", root));
            CanvasGroup popGroup = pop.gameObject.AddComponent<CanvasGroup>();
            RectTransform lift = Stretch(Rect("Lift", pop));

            Image halo = Glow(lift, "Halo", FocusBlue, 12f);
            Image picked = Glow(lift, "Picked Glow", PickedGold, 20f);
            GameObject background = Instance(basicBackground, lift, "Basic Background");
            Stretch((RectTransform)background.transform);
            Image tint = Img(Stretch(Rect("Hover Tint", lift), 6f, 6f, 6f, 6f), null, HoverTintColour);

            // The view hides the band when it has no label; the wide card never has one.
            RectTransform band = Stretch(Rect("Band", lift));
            Image bandFill = Img(Stretch(Rect("Fill", band)), null, Color.clear);
            TMP_Text bandText = CapLabel(band, "Label");
            band.gameObject.SetActive(false);

            RectTransform content = Stretch(Rect("Content", lift));
            HorizontalLayoutGroup layout = HLayout(content, 16f, TextAnchor.MiddleLeft, new RectOffset(18, 28, 0, 0));
            RectTransform mountCell = Mount(content, "Mount", WideMountSize, prestigeIcon, 36f);

            // A fixed-height column, so a long description shrinks to fit instead of running off the card.
            RectTransform column = Rect("Text", content);
            VerticalLayoutGroup columnLayout = VLayout(column, 3f, new RectOffset());
            columnLayout.childAlignment = TextAnchor.MiddleLeft;
            Flexible(column.gameObject, 1f).preferredWidth = 0f;
            Fixed(column.gameObject, -1f, WideCardSize.y - 16f);
            TMP_Text name = Text("Name", column, displayDrop, 22f, Gold, "Name");
            name.enableAutoSizing = true;
            name.fontSizeMin = 15f;
            name.fontSizeMax = 22f;
            // The name keeps a full 22 pt line; only the description gives way when the column is tight.
            GetOrAdd<LayoutElement>(name.gameObject).minHeight = 28f;
            TMP_Text description = Text("Description", column, display, 16f, Cream, "Description");
            Wrap(description);
            description.enableAutoSizing = true;
            description.fontSizeMin = 12f;
            description.fontSizeMax = 16f;
            // A zero minimum lets the fixed column squeeze a long description, so auto-size can work.
            GetOrAdd<LayoutElement>(description.gameObject).minHeight = 0f;
            // SetNotes needs the object; the wide card has no room for notes, so it keeps no rows.
            RectTransform notes = Rect("Notes", column);
            Ignore(notes.gameObject);
            notes.gameObject.SetActive(false);

            // A capped, wrapping footer, so a long "no room" line in German cannot squeeze the text column.
            TMP_Text footer = CapLabel(content, "Footer");
            footer.alignment = TextAlignmentOptions.MidlineRight;
            Wrap(footer);
            footer.enableAutoSizing = true;
            footer.fontSizeMin = 9f;
            footer.fontSizeMax = 12.5f;
            Fixed(footer.gameObject, WideFooterWidth, 64f);
            RectTransform done = Rect("Done", content);
            HLayout(done, 10f, TextAnchor.MiddleCenter, new RectOffset());
            Diamond(done, "Diamond Left", 8f, Gold, Slate);
            TMP_Text doneText = Text("Text", done, displayDrop, 19f, Gold, "Taken");
            doneText.alignment = TextAlignmentOptions.Center;
            Diamond(done, "Diamond Right", 8f, Gold, Slate);
            done.gameObject.SetActive(false);

            var so = new SerializedObject(view);
            Ref(so, "button", button);
            Ref(so, "layoutElement", element);
            Ref(so, "pop", pop);
            Ref(so, "popGroup", popGroup);
            Ref(so, "lift", lift);
            Ref(so, "hoverHalo", halo);
            Ref(so, "hoverTint", tint);
            Ref(so, "pickedGlow", picked);
            Ref(so, "band", band.gameObject);
            Ref(so, "bandFill", bandFill);
            Ref(so, "bandText", bandText);
            Ref(so, "contentLayout", layout);
            Ref(so, "icon", Child<Image>(mountCell, "Icon"));
            Ref(so, "nameText", name);
            Ref(so, "description", description);
            Ref(so, "notes", notes.gameObject);
            Refs(so, "noteIcons", new Object[0]);
            Refs(so, "noteTexts", new Object[0]);
            Ref(so, "footer", footer);
            Ref(so, "done", done.gameObject);
            Ref(so, "doneText", doneText);
            Ref(so, "warningIcon", lockIcon);
            Ref(so, "mapIcon", flagIcon);
            Ref(so, "goldIcon", goldIcon);
            // The row is centred by the layout, so Load's top padding stays at zero.
            so.FindProperty("contentTop").floatValue = 0f;
            so.FindProperty("contentTopWithBand").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        #region Panel layout
        static void BuildPanelLayout(GameObject rootGo, bool prestige)
        {
            rootGo.layer = 5;
            var root = (RectTransform)rootGo.transform;
            Stretch(root);
            ChoicePanelView view = GetOrAdd<ChoicePanelView>(rootGo);
            var so = new SerializedObject(view);

            RectTransform stack = Rect("Stack", root);
            stack.anchorMin = stack.anchorMax = stack.pivot = new Vector2(0.5f, 0.5f);
            stack.anchoredPosition = new Vector2(0f, StackLift);
            VerticalLayoutGroup stackLayout = VLayout(stack, StackGap, new RectOffset());
            stackLayout.childAlignment = TextAnchor.UpperCenter;
            stackLayout.childForceExpandWidth = false;
            ContentSizeFitter fitter = stack.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // Shrinks the whole stack when UI Scale leaves a smaller canvas than it needs.
            UIFitToCanvas fit = stack.gameObject.AddComponent<UIFitToCanvas>();
            var fitSo = new SerializedObject(fit);
            fitSo.FindProperty("designSize").vector2Value = prestige ? new Vector2(1480f, 800f) : new Vector2(1300f, 800f);
            fitSo.ApplyModifiedPropertiesWithoutUndo();

            BuildTitle(stack, so, prestige);

            RectTransform cards;
            if (prestige)
            {
                RectTransform row = Rect("Row", stack);
                HLayout(row, RecruitGap, TextAnchor.MiddleCenter, new RectOffset());
                RectTransform slot = Rect("Recruit Slot", row);
                Fixed(slot.gameObject, RecruitCardSize.x, RecruitCardSize.y);
                cards = Rect("Cards", row);
                HLayout(cards, PrestigeCardGap, TextAnchor.MiddleCenter, new RectOffset());
                Ref(so, "recruitSlot", slot);
                Ref(so, "recruitCardPrefab", recruitCardPrefab.GetComponent<RecruitCard>());
            }
            else
            {
                cards = Rect("Cards", stack);
                HLayout(cards, OrdealCardGap, TextAnchor.MiddleCenter, new RectOffset());
            }

            Ref(so, "cardParent", cards);
            Ref(so, "cardPrefab", cardPart.GetComponent<ChoiceCardView>());
            so.FindProperty("cardSize").vector2Value = prestige ? PrestigeCardSize : OrdealCardSize;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The scene's title banner stays above, so the stack is a count line, the gear cards, then the Or line and wide card.
        static void BuildTreasureLayout(GameObject rootGo)
        {
            rootGo.layer = 5;
            var root = (RectTransform)rootGo.transform;
            Stretch(root);
            ChoicePanelView view = GetOrAdd<ChoicePanelView>(rootGo);
            var so = new SerializedObject(view);

            RectTransform stack = Rect("Stack", root);
            stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 0f);
            stack.pivot = new Vector2(0.5f, 0f);
            stack.anchoredPosition = new Vector2(0f, TreasureStackBottom);
            VerticalLayoutGroup stackLayout = VLayout(stack, 0f, new RectOffset());
            stackLayout.childAlignment = TextAnchor.UpperCenter;
            stackLayout.childForceExpandWidth = false;
            ContentSizeFitter fitter = stack.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // Shrinks about the bottom edge when UI Scale leaves less room between the army panel and the banner.
            UIFitToCanvas fit = stack.gameObject.AddComponent<UIFitToCanvas>();
            var fitSo = new SerializedObject(fit);
            fitSo.FindProperty("designSize").vector2Value = new Vector2(TreasureStackWidth, TreasureStackHeight);
            fitSo.FindProperty("reservedSize").vector2Value = new Vector2(0f, TreasureStackBottom + BannerHeight);
            fitSo.ApplyModifiedPropertiesWithoutUndo();

            // The count line is the title block, so it drops in on open like the other panels' titles.
            RectTransform block = Rect("Title Block", stack);
            Fixed(block.gameObject, TreasureStackWidth, CountHeight);
            RectTransform titleContent = Stretch(Rect("Title Content", block));
            CanvasGroup titleGroup = titleContent.gameObject.AddComponent<CanvasGroup>();
            TMP_Text count = Text("Counts", titleContent, displayDrop, 16f, Sub, "Gear  1 / 5   ·   Consumables  1 / 3");
            Stretch(count.rectTransform);
            count.alignment = TextAlignmentOptions.Center;
            Gap(stack, CountToCards);

            RectTransform cards = Rect("Cards", stack);
            HLayout(cards, TreasureCardGap, TextAnchor.MiddleCenter, new RectOffset());

            RectTransform wide = Rect("Wide", stack);
            VerticalLayoutGroup wideLayout = VLayout(wide, 0f, new RectOffset());
            wideLayout.childAlignment = TextAnchor.UpperCenter;
            wideLayout.childForceExpandWidth = false;
            Gap(wide, CardsToOr);
            RectTransform orLine = Rect("Or Line", wide);
            HLayout(orLine, 16f, TextAnchor.MiddleCenter, new RectOffset());
            Fixed(orLine.gameObject, OrWidth, OrHeight);
            FadeLine(orLine, "Line Left", 0f, 0.8f);
            TMP_Text orText = Text("Text", orLine, displayDrop, 17f, Sub, "Or claim a consumable");
            orText.fontStyle = FontStyles.Italic;
            Localize(orText, "orConsumable");
            FadeLine(orLine, "Line Right", 0.8f, 0f);
            Gap(wide, OrToWide);
            RectTransform slot = Rect("Wide Slot", wide);
            Fixed(slot.gameObject, WideCardSize.x, WideCardSize.y);

            Ref(so, "titleBlock", titleContent);
            Ref(so, "titleGroup", titleGroup);
            Ref(so, "cardParent", cards);
            Ref(so, "cardPrefab", cardPart.GetComponent<ChoiceCardView>());
            so.FindProperty("cardSize").vector2Value = TreasureCardSize;
            so.FindProperty("groupHover").boolValue = true;
            Ref(so, "countText", count);
            Ref(so, "wideGroup", wide.gameObject);
            Ref(so, "wideSlot", slot);
            Ref(so, "wideCardPrefab", wideCardPart.GetComponent<ChoiceCardView>());
            so.FindProperty("wideCardSize").vector2Value = WideCardSize;
            so.FindProperty("paddingWithoutWide").intValue = Mathf.RoundToInt(WideBlockHeight / 2f);
            Ref(so, "stackLayout", stackLayout);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A brass rule that fades out towards one end, as beside the Or line.
        static void FadeLine(RectTransform parent, string name, float leftAlpha, float rightAlpha)
        {
            RectTransform cell = Rect(name, parent);
            Flexible(cell.gameObject, 1f);
            Fixed(cell.gameObject, -1f, 1f);
            int segments = 8;
            HorizontalLayoutGroup row = HLayout(cell, 0f, TextAnchor.MiddleCenter, new RectOffset());
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            for (int i = 0; i < segments; i++)
            {
                float t = (i + 0.5f) / segments;
                Image piece = Img(Rect("Segment", cell), solid, A(Brass, Mathf.Lerp(leftAlpha, rightAlpha, t)));
                Flexible(piece.gameObject, 1f);
            }
        }

        static void BuildTitle(RectTransform stack, SerializedObject so, bool prestige)
        {
            RectTransform block = Rect("Title Block", stack);
            Fixed(block.gameObject, TitleWidth, prestige ? PrestigeTitleHeight : OrdealTitleHeight);
            // The block holds the layout slot; its content drops in on open without the stack's layout pulling it back.
            RectTransform content = Stretch(Rect("Title Content", block));
            CanvasGroup group = content.gameObject.AddComponent<CanvasGroup>();
            VerticalLayoutGroup layout = VLayout(content, 10f, new RectOffset());
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = false;

            RectTransform titleRow = Rect("Title Row", content);
            HLayout(titleRow, 16f, TextAnchor.MiddleCenter, new RectOffset());
            Fixed(titleRow.gameObject, -1f, 58f);
            Mount(titleRow, "Mount", 58f, prestige ? prestigeIcon : swordsIcon, 26f);
            TMP_Text title = Text("Title", titleRow, displayDrop, 42f, Gold, prestige ? "Prestige III" : "Choose your Ordeal");

            TMP_Text subtitle = Text("Subtitle", content, displayDrop, 18f, Sub, "Subtitle");
            subtitle.alignment = TextAlignmentOptions.Center;
            subtitle.enableAutoSizing = true;
            subtitle.fontSizeMin = 13f;
            subtitle.fontSizeMax = 18f;
            Fixed(subtitle.gameObject, TitleWidth, 26f);

            Rule(content, 520f, Hex("0F1618"));

            Ref(so, "titleBlock", content);
            Ref(so, "titleGroup", group);
            Ref(so, "title", title);
            Ref(so, "subtitle", subtitle);
            if (prestige) return;

            RectTransform info = Rect("Info Row", content);
            HLayout(info, 12f, TextAnchor.MiddleCenter, new RectOffset());
            Fixed(info.gameObject, -1f, 30f);
            TMP_Text renown = Text("Renown", info, displayDrop, 16f, Sub, "Renown: x1.1 now, x1.2 after this pick");
            RectTransform held = Rect("Held", info);
            HLayout(held, 8f, TextAnchor.MiddleCenter, new RectOffset());
            TMP_Text separator = Text("Separator", held, display, 16f, Mute, "·");
            TMP_Text caption = Text("Caption", held, displayDrop, 16f, Sub, "Held:");
            Localize(caption, "ordealHeld");
            RectTransform icons = Rect("Icons", held);
            HorizontalLayoutGroup iconRow = HLayout(icons, 4f, TextAnchor.MiddleLeft, new RectOffset());
            // The HUD pip prefab keeps its own size.
            iconRow.childControlWidth = false;
            iconRow.childControlHeight = false;
            Fixed(icons.gameObject, -1f, 30f);

            Ref(so, "infoRow", info.gameObject);
            Ref(so, "renownText", renown);
            Ref(so, "heldGroup", held.gameObject);
            Ref(so, "heldParent", icons);
            Ref(so, "heldIconPrefab", ordealIconPrefab.GetComponent<OrdealIcon>());
        }
        #endregion

        #region Widgets
        static Image Glow(RectTransform parent, string name, Color colour, float spread)
        {
            Image image = Img(Stretch(Rect(name, parent), -spread, -spread, -spread, -spread), glow, colour, glowType);
            image.pixelsPerUnitMultiplier = glowPixelsPerUnit;
            image.enabled = false;
            return image;
        }

        // A brass line with a diamond at its middle, as under the panel headers.
        static void Rule(RectTransform parent, float width, Color diamondFill)
        {
            RectTransform rule = Rect("Rule", parent);
            Fixed(rule.gameObject, width, 9f);
            Image line = Img(Rect("Line", rule), solid, A(Brass, 0.6f));
            Centre(line.rectTransform, width, 1f);
            Diamond(rule, "Diamond", 9f, Brass, diamondFill).GetComponent<LayoutElement>().ignoreLayout = true;
        }

        static RectTransform Diamond(RectTransform parent, string name, float size, Color edge, Color fill)
        {
            RectTransform cell = Rect(name, parent);
            Fixed(cell.gameObject, size, size);
            Image outer = Img(Rect("Edge", cell), solid, edge);
            Centre(outer.rectTransform, size, size);
            outer.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Image inner = Img(Rect("Fill", cell), solid, fill);
            Centre(inner.rectTransform, size - 2f, size - 2f);
            inner.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            return cell;
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

        static TMP_Text CapLabel(RectTransform parent, string name)
        {
            TMP_Text label = Text(name, parent, display, 12.5f, Cap, name);
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = 10f;
            return label;
        }

        static void Gap(RectTransform parent, float height)
        {
            RectTransform gap = Rect("Gap", parent);
            Fixed(gap.gameObject, -1f, height);
        }

        static void Spacer(RectTransform parent)
        {
            LayoutElement spacer = Rect("Spacer", parent).gameObject.AddComponent<LayoutElement>();
            spacer.flexibleHeight = 1f;
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
            if (component == null) Debug.LogError($"ChoicePanelBuilder: {root.name} has no {typeof(T).Name} at '{path}'.");
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
            if (property == null) { Debug.LogError($"ChoicePanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.objectReferenceValue = value;
        }

        static void Refs(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"ChoicePanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        #endregion
    }
}
