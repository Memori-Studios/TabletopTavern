using System;
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

namespace TJ.FollowCameraTools
{
    /// <summary>
    /// Generates the follow camera's screen (Follow Camera UI.prefab): its own canvas with one key panel in the bottom right on
    /// the Basic Background. Installs one instance at the root of TavernBattle.unity.
    /// </summary>
    public static class FollowCameraPanelBuilder
    {
        public const string Folder = "Assets/Data/Prefabs/UI/Battle/Follow Camera";
        public const string PanelPath = Folder + "/Follow Camera UI.prefab";
        const string ScenePath = "Assets/Scenes/TavernBattle.unity";
        const string BasicBackgroundPath = "Assets/Data/Prefabs/UI/Reuseable/Basic Background.prefab";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";
        const string TableName = "MainLocalizationTable";

        #region Layout
        // Below Settings (105), above the battle HUD (0); never open with photo mode, which uses the same order.
        const int CanvasOrder = 50;
        const float PanelWidth = 440f;
        const float PanelHeight = 168f;
        const float PanelMargin = 20f;
        const float PanelPadding = 22f;
        const float TitleHeight = 34f;
        const float RowHeight = 28f;
        const float TagWidth = 118f;
        const float TagHeight = 26f;
        #endregion

        #region Style
        static readonly Color Slate = Hex("1F2B2E");
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        #endregion

        static TMP_FontAsset displayDrop, display;
        static Sprite solid;
        static GameObject basicBackground;

        #region Entry points
        [MenuItem("Tabletop Tavern/Follow Camera/Rebuild Prefab")]
        public static void RebuildPrefab()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                System.IO.Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            // Rebuild inside the existing prefab so the root keeps its id; the TavernBattle instance references it.
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath) != null;
            Scene stage = default;
            GameObject root;
            if (existing) root = PrefabUtility.LoadPrefabContents(PanelPath);
            else
            {
                stage = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Follow Camera UI", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
            }
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root);
                Normalize(root);
                RecordOverrides(root);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                Debug.Log($"FollowCameraPanelBuilder: wrote {PanelPath}");
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

        [MenuItem("Tabletop Tavern/Follow Camera/Install In TavernBattle")]
        public static void InstallMenu() => Debug.Log("FollowCameraPanelBuilder: " + Install());

        /// <summary>Puts one instance at the root of TavernBattle.unity and saves that scene. Does nothing if one is there.</summary>
        public static string Install()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (prefab == null) return $"no prefab at {PanelPath}; run Rebuild Prefab first.";
            Scene battle = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !battle.isLoaded;
            if (opened) battle = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                if (battle.isDirty && !opened) return "TavernBattle.unity has unsaved changes; save or revert them first.";
                foreach (GameObject sceneRoot in battle.GetRootGameObjects())
                    if (sceneRoot.GetComponentInChildren<FollowCamera>(true) != null) return "already installed.";
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, battle);
                instance.name = "--- FOLLOW CAMERA ---";
                EditorSceneManager.MarkSceneDirty(battle);
                EditorSceneManager.SaveScene(battle);
                return "installed at the scene root and saved TavernBattle.unity.";
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(battle, true);
            }
        }
        #endregion

        #region Build
        static void Build(GameObject root)
        {
            root.layer = 5;
            Canvas canvas = Ensure<Canvas>(root);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasOrder;
            CanvasScaler scaler = Ensure<CanvasScaler>(root);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            // Text only: the panel never takes a click, so the battlefield under it stays reachable for the camera.
            CanvasGroup rootGroup = Ensure<CanvasGroup>(root);
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            rootGroup.alpha = 0f;
            FollowCameraPanelView view = Ensure<FollowCameraPanelView>(root);
            FollowCamera mode = Ensure<FollowCamera>(root);
            var rootRect = (RectTransform)root.transform;

            RectTransform panel = Rect("Panel", rootRect);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.anchoredPosition = new Vector2(-PanelMargin, PanelMargin);
            Stretch((RectTransform)Instance(basicBackground, panel, "Background").transform);

            TMP_Text title = Text("Title", panel, displayDrop, 24f, Gold, TextAlignmentOptions.Center);
            Top(title.rectTransform, PanelPadding, PanelPadding - 6f, TitleHeight);
            title.enableAutoSizing = true;
            title.fontSizeMin = 16f;
            title.fontSizeMax = 24f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.text = "Following Spearmen";

            float rowTop = PanelPadding - 6f + TitleHeight + 8f;
            TMP_Text exitKey = KeyRow(panel, "Exit Row", "followExit", rowTop);
            TMP_Text manualKey = KeyRow(panel, "Manual Row", "followManual", rowTop + RowHeight);
            TMP_Text cycleKey = KeyRow(panel, "Cycle Row", "followCycle", rowTop + RowHeight * 2f);
            exitKey.text = "Escape";
            manualKey.text = "Space";
            cycleKey.text = "Page Up / Page Down";

            GameObject paused = PausedTag(panel);
            paused.SetActive(false);

            var so = new SerializedObject(view);
            Set(so, "canvas", canvas);
            Set(so, "rootGroup", rootGroup);
            Set(so, "titleLabel", title);
            Set(so, "pausedTag", paused);
            Set(so, "exitKey", exitKey);
            Set(so, "manualKey", manualKey);
            Set(so, "cycleKey", cycleKey);
            so.ApplyModifiedPropertiesWithoutUndo();

            var modeObject = new SerializedObject(mode);
            Set(modeObject, "view", view);
            modeObject.ApplyModifiedPropertiesWithoutUndo();
        }

        // A label on the left and its key in gold on the right, one line each.
        static TMP_Text KeyRow(RectTransform panel, string name, string labelKey, float fromTop)
        {
            RectTransform row = Rect(name, panel);
            Top(row, PanelPadding, fromTop, RowHeight);

            TMP_Text label = Text("Label", row, display, 18f, Cream, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(0.48f, 1f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            FitOneLine(label, 13f, 18f);
            Localize(label, labelKey);

            TMP_Text key = Text("Key", row, display, 18f, Gold, TextAlignmentOptions.MidlineRight);
            key.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            key.rectTransform.anchorMax = Vector2.one;
            key.rectTransform.offsetMin = key.rectTransform.offsetMax = Vector2.zero;
            FitOneLine(key, 13f, 18f);
            return key;
        }

        // Sits on the panel's top edge like a tab, so it never pushes the rows.
        static GameObject PausedTag(RectTransform panel)
        {
            RectTransform tag = Rect("Paused Tag", panel);
            tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 1f);
            tag.pivot = new Vector2(0.5f, 0.5f);
            tag.sizeDelta = new Vector2(TagWidth, TagHeight);
            tag.anchoredPosition = Vector2.zero;
            Img(tag, solid, Brass).raycastTarget = false;
            RectTransform fill = Stretch(Rect("Fill", tag));
            fill.offsetMin = new Vector2(1.5f, 1.5f);
            fill.offsetMax = new Vector2(-1.5f, -1.5f);
            Img(fill, solid, Slate).raycastTarget = false;
            TMP_Text label = Text("Label", tag, displayDrop, 16f, Gold, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            FitOneLine(label, 11f, 16f);
            label.characterSpacing = 4f;
            Localize(label, "followPaused");
            return tag.gameObject;
        }

        static void FitOneLine(TMP_Text text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }
        #endregion

        #region Wiring
        static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException($"FollowCameraPanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}.");
            if (value == null) throw new InvalidOperationException($"FollowCameraPanelBuilder: nothing to put in '{field}'.");
            property.objectReferenceValue = value;
        }
        #endregion

        #region Helpers
        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite && sprite.name == "TooltipSolid") solid = sprite;
            if (solid == null) Debug.LogError("FollowCameraPanelBuilder: TooltipSolid sprite missing.");
            basicBackground = Load<GameObject>(BasicBackgroundPath);
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"FollowCameraPanelBuilder: missing {typeof(T).Name} at {path}");
            return asset;
        }

        static T Ensure<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        static RectTransform Rect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        // Pinned to the parent's top edge with side padding.
        static void Top(RectTransform rect, float side, float fromTop, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(side, -(fromTop + height));
            rect.offsetMax = new Vector2(-side, -fromTop);
        }

        static Image Img(RectTransform rect, Sprite sprite, Color colour)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            return image;
        }

        static TMP_Text Text(string name, RectTransform parent, TMP_FontAsset font, float size, Color colour, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = colour;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        static GameObject Instance(GameObject prefab, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance;
        }

        static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color colour);
            colour.a = alpha;
            return colour;
        }

        static void Normalize(GameObject root)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(text) != null) continue;
                var so = new SerializedObject(text);
                so.FindProperty("m_fontColor32").colorValue = text.color;
                so.FindProperty("m_TextStyleHashCode").intValue = TMP_Style.NormalStyle.hashCode;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
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

        #region Localization
        // English source text for the keys the follow camera adds. Other locales are filled in separately.
        public static readonly (string key, string english)[] Keys =
        {
            ("FollowSquad", "Follow Squad"),
            ("followTitle", "Following {0}"),
            ("followManualTitle", "Manual camera"),
            ("followPaused", "Paused"),
            ("followExit", "Exit"),
            ("followManual", "Manual control"),
            ("followCycle", "Cycle squads"),
            ("settingsFollowHeadBob", "Head Bob While Following"),
            ("settingsHelpFollowHeadBob", "The camera sways with the squad's stride while you follow it."),
            ("Guide_camera_Follow", "Follow camera: ride behind a squad while the battle runs. Page Up and Page Down switch squads"),
        };

        static StringTableCollection collection;
        static StringTable english;

        static void LoadTable()
        {
            collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            english = (StringTable)collection.GetTable("en");
        }

        [MenuItem("Tabletop Tavern/Follow Camera/Add Text Keys")]
        public static void AddKeysMenu() => Debug.Log("FollowCameraPanelBuilder: " + EnsureKeys());

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
            if (entry == null) throw new InvalidOperationException($"FollowCameraPanelBuilder: no localization key '{key}'.");
            return entry.Id;
        }

        static string English(string key)
        {
            StringTableEntry entry = english.GetEntry(key);
            return entry != null ? entry.Value : key;
        }

        // Points the text's localizer at a key. Shows the English value in the Editor.
        static void Localize(TMP_Text text, string key)
        {
            LocalizeStringEvent localizer = text.gameObject.AddComponent<LocalizeStringEvent>();
            var setter = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), text, typeof(TMP_Text).GetProperty("text").GetSetMethod());
            UnityEventTools.AddPersistentListener(localizer.OnUpdateString, setter);
            var so = new SerializedObject(localizer);
            so.FindProperty("m_StringReference.m_TableReference.m_TableCollectionName").stringValue = "GUID:" + collection.SharedData.TableCollectionNameGuid.ToString("N");
            so.FindProperty("m_StringReference.m_TableEntryReference.m_KeyId").longValue = KeyId(key);
            so.FindProperty("m_StringReference.m_TableEntryReference.m_Key").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            text.text = English(key);
        }
        #endregion
    }
}
