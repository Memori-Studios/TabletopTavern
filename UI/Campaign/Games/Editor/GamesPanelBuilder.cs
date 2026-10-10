using System;
using System.Collections.Generic;
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

namespace TJ.Games.EditorTools
{
    /// <summary>
    /// Generates the games panel (Games Panel UI.prefab and its parts): the choosing card and the table strip, on the
    /// game's Basic Background, and installs it in Map.unity. Rebuilding the panel keeps hand edits to the part prefabs.
    /// </summary>
    public static class GamesPanelBuilder
    {
        public const string PartFolder = "Assets/Data/Prefabs/UI/Map/Games";
        public const string PanelPath = PartFolder + "/Games Panel UI.prefab";
        const string StakePath = PartFolder + "/Games Stake Button.prefab";
        const string CallPath = PartFolder + "/Games Call Button.prefab";
        const string DiceFolder = "Assets/Art/Icons/UI/Dice";
        const string StatPlaquePath = "Assets/Data/Prefabs/UI/Reuseable/Ornaments/Stat Plaque.prefab";
        const string BandGradientPath = "Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Gradient_Vertical_Smooth01.png";
        const string ScenePath = "Assets/Scenes/Map.unity";
        const string ButtonFolder = "Assets/Data/Prefabs/UI/Reuseable/Buttons";
        const string BasicBackgroundPath = "Assets/Data/Prefabs/UI/Reuseable/Basic Background.prefab";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";
        const string TableName = "MainLocalizationTable";
        static readonly string[] OldPanelNames = { "Games Buttons Panel", "Results Panel" };

        // The card hangs below the node title banner; the strip sits under the floating dice, above the army bar.
        const float CardTop = 205f;
        const float CardWidth = 1158f;
        const float ColumnWidth = 382f;
        const float StripTop = 520f;
        const float StripWidth = 800f;
        const float PanelPadding = 5f;
        const float CardHeaderHeight = 92f;
        const float StripHeaderHeight = 72f;
        const float BandAlpha = 0.12f;
        const float TexturePixelsPerUnit = 12f;
        const float ActionHeight = 62f;
        // The strip's inner width: 800 less the frame edge and the body's side padding.
        const float StripInner = 738f;
        const float LadderStep = 90f;
        const float PlaqueGap = 10f;
        const float PlaqueRowGap = 6f;
        // Room for the longest label and value pair ("Einsätze" and "Akt III") at the header's right edge.
        const float HeaderPlaqueWidth = 180f;
        // The dice strip is the tallest: 5 + header 72 + 14 + two payout rows 70 + 12 + result row 80 + 14 + 5.
        const float StripTallest = 272f;

        #region Style
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        static readonly Color White = Hex("ECF0F1");
        static readonly Color Sub = Hex("B4AA94");
        static readonly Color Cap = Hex("8C9AA2");
        static readonly Color Positive = Hex("7BD66F");
        static readonly Color Double = Hex("A6F29E");
        static readonly Color Negative = Hex("E3695E");
        static readonly Color Coin = Hex("E3BB71");
        static readonly Color PlaqueBacking = Hex("131D21");
        static readonly Color Motes = Hex("9ED8FF");
        static readonly Color PrimaryDetail = Hex("A6D9A0");
        static readonly Color Ivory = Hex("EEE7D8");
        static readonly Color Pip = Hex("1E1C1A");
        static readonly Color Candle = Hex("E8A054");

        static TMP_FontAsset displayDrop, display;
        static Sprite mount, frameEdge, bandGradient;
        static Sprite diceIcon, hiloIcon, roundIcon, arrowIcon, rewindIcon, dieBlank;
        static Sprite[] dieFaces;
        static GameObject primaryButton, standardButton, backButton, basicBackground, statPlaque;

        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            var sheet = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite) sheet[sprite.name] = sprite;
            sheet.TryGetValue("TooltipMount", out mount);
            if (mount == null) Debug.LogError("GamesPanelBuilder: tooltip sheet sprites missing.");
            frameEdge = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Frame_Box_Small01.png");
            bandGradient = Load<Sprite>(BandGradientPath);
            diceIcon = Load<Sprite>("Assets/Art/Icons/Achievements/DiceClean.png");
            hiloIcon = Load<Sprite>("Assets/Art/Icons/Achievements/ChevronTwoClean.png");
            roundIcon = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Healing01_Clean.png");
            arrowIcon = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Reticle_Arrow02_Med.png");
            rewindIcon = Load<Sprite>("Assets/Art/Icons/Consumables/Rewind.png");
            dieBlank = Load<Sprite>(DiceFolder + "/DieBlank.png");
            dieFaces = new Sprite[6];
            for (int i = 0; i < 6; i++) dieFaces[i] = Load<Sprite>($"{DiceFolder}/DieFace{i + 1}.png");
            primaryButton = Load<GameObject>(ButtonFolder + "/Button - Primary.prefab");
            standardButton = Load<GameObject>(ButtonFolder + "/Button - Standard.prefab");
            backButton = Load<GameObject>(ButtonFolder + "/Button - Back.prefab");
            basicBackground = Load<GameObject>(BasicBackgroundPath);
            statPlaque = Load<GameObject>(StatPlaquePath);
        }

        // The view writes into every plaque Value, so a panel built without them would fail at runtime.
        static bool PlaqueLoaded()
        {
            if (statPlaque != null) return true;
            Debug.LogError($"GamesPanelBuilder: no Stat Plaque at {StatPlaquePath}; nothing was rebuilt.");
            return false;
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"GamesPanelBuilder: missing {typeof(T).Name} at {path}");
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
        static StringTableCollection collection;
        static StringTable english;

        static void LoadTable()
        {
            collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            english = (StringTable)collection.GetTable("en");
        }

        static long KeyId(string key)
        {
            SharedTableData.SharedTableEntry entry = collection.SharedData.GetEntry(key);
            if (entry == null) throw new InvalidOperationException($"GamesPanelBuilder: no localization key '{key}'.");
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
        /// <summary>Creates any missing part prefab, then rebuilds the panel from them.</summary>
        [MenuItem("Tabletop Tavern/Games Panel/Rebuild Prefab")]
        public static void BuildPrefab()
        {
            LoadAssets();
            LoadTable();
            if (!PlaqueLoaded()) return;
            EnsureParts(false);
            BuildPanel();
        }

        [MenuItem("Tabletop Tavern/Games Panel/Reset Part Prefabs")]
        static void ResetPartsMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset games panel parts",
                    $"The part prefabs in {PartFolder} are rebuilt from code, which discards your edits to them. The panel is rebuilt after.",
                    "Reset", "Cancel"))
                return;
            ResetParts();
        }

        /// <summary>Rebuilds both part prefabs from code, then the panel.</summary>
        public static void ResetParts()
        {
            LoadAssets();
            LoadTable();
            if (!PlaqueLoaded()) return;
            EnsureParts(true);
            BuildPanel();
        }

        [MenuItem("Tabletop Tavern/Games Panel/Install In Map")]
        public static void InstallMenu() => Debug.Log("GamesPanelBuilder: " + InstallInMap());

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
                root = new GameObject("Games Panel UI", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
            }
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root);
                Normalize(root);
                RecordOverrides(root);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                Debug.Log($"GamesPanelBuilder: wrote {PanelPath}");
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
        /// Replaces the old buttons and result pop-up under Map.unity's Games Panel with the prefab, points GamesPanel at
        /// it and saves only Map.unity.
        /// </summary>
        public static string InstallInMap()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (prefab == null) return $"no prefab at {PanelPath}; run Rebuild Prefab first.";
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
                GamesPanel gamesPanel = null;
                foreach (GameObject root in map.GetRootGameObjects())
                {
                    gamesPanel = root.GetComponentInChildren<GamesPanel>(true);
                    if (gamesPanel != null) break;
                }
                if (gamesPanel == null) return "no GamesPanel in Map.unity.";

                var removed = new List<string>();
                foreach (string oldName in OldPanelNames)
                {
                    Transform old = gamesPanel.transform.Find(oldName);
                    if (old == null) continue;
                    string outside = CheckReferences(map, old, gamesPanel);
                    if (!string.IsNullOrEmpty(outside)) return $"stopped, outside references into {oldName}:\n" + outside;
                }
                foreach (string oldName in OldPanelNames)
                {
                    Transform old = gamesPanel.transform.Find(oldName);
                    if (old == null) continue;
                    Object.DestroyImmediate(old.gameObject);
                    removed.Add(oldName);
                }
                for (int i = gamesPanel.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = gamesPanel.transform.GetChild(i).gameObject;
                    if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(child) == prefab) Object.DestroyImmediate(child);
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gamesPanel.transform);
                var so = new SerializedObject(gamesPanel);
                Ref(so, "view", instance.GetComponent<GamesPanelView>());
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(map);
                if (!EditorSceneManager.SaveScene(map)) return "Map.unity did not save.";
                return removed.Count > 0 ? $"installed; removed {string.Join(", ", removed)}." : "installed.";
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(map, true);
            }
        }

        // Anything outside the old panel that points into it would lose its target when the old panel is deleted.
        static string CheckReferences(Scene scene, Transform old, GamesPanel gamesPanel)
        {
            var inside = new HashSet<Object>();
            foreach (Transform t in old.GetComponentsInChildren<Transform>(true))
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
                        if (component == null || component == gamesPanel) continue;
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
        static GameObject stakePart, callPart;

        static void EnsureParts(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(PartFolder)) AssetDatabase.CreateFolder("Assets/Data/Prefabs/UI/Map", "Games");
            stakePart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(StakePath);
            if (stakePart == null) stakePart = SavePart(StakePath, "Games Stake Button", StakePart);
            callPart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(CallPath);
            if (callPart == null) callPart = SavePart(CallPath, "Games Call Button", CallPart);
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
                Debug.Log($"GamesPanelBuilder: wrote {path}");
                return asset;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        // A button from the family with its own label hidden and a content slot in its background, so the disabled
        // fade covers the content too.
        static RectTransform ButtonShell(GameObject go, GameObject buttonPrefab, out Button button)
        {
            GameObject buttonGo = Instance(buttonPrefab, go.transform, "Button");
            Stretch((RectTransform)buttonGo.transform);
            Transform label = buttonGo.transform.Find("Button Label");
            if (label != null) label.gameObject.SetActive(false);
            Transform background = buttonGo.transform.Find("Background");
            RectTransform content = Stretch(Rect("Content", background != null ? background : buttonGo.transform));
            button = buttonGo.GetComponent<Button>();
            return content;
        }

        static void StakePart(GameObject go)
        {
            ((RectTransform)go.transform).sizeDelta = new Vector2(107f, ActionHeight);
            RectTransform content = ButtonShell(go, standardButton, out Button button);
            VerticalLayoutGroup layout = VLayout(content, 4f, new RectOffset(4, 4, 10, 8));
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;

            RectTransform arrows = Rect("Arrows", content);
            Fixed(arrows.gameObject, 22f, 20f);
            for (int i = 0; i < 3; i++)
            {
                Image arrow = Img(Rect("Arrow " + (i + 1), arrows), arrowIcon, Motes);
                arrow.preserveAspect = true;
                arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                arrow.rectTransform.sizeDelta = new Vector2(20f, 10f);
            }
            TMP_Text amount = Text("Amount", content, displayDrop, 16f, Cream, "15");
            amount.alignment = TextAlignmentOptions.Center;

            GamesStakeButton stake = go.AddComponent<GamesStakeButton>();
            var so = new SerializedObject(stake);
            Ref(so, "button", button);
            Ref(so, "amount", amount);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CallPart(GameObject go)
        {
            ((RectTransform)go.transform).sizeDelta = new Vector2(235f, 70f);
            RectTransform content = ButtonShell(go, standardButton, out Button button);
            VLayout(content, 5f, new RectOffset(16, 16, 8, 8));

            RectTransform top = Rect("Top", content);
            HLayout(top, 8f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(top.gameObject, -1f, 26f);
            TMP_Text title = Text("Title", top, displayDrop, 19f, White, "Higher");
            Flexible(title.gameObject, 1f).preferredWidth = 0f;
            title.enableAutoSizing = true;
            title.fontSizeMin = 14f;
            title.fontSizeMax = 19f;
            TMP_Text chance = Text("Chance", top, displayDrop, 15f, Cream, "67% to win");
            chance.alignment = TextAlignmentOptions.MidlineRight;

            RectTransform chips = Rect("Faces", content);
            HLayout(chips, 3f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(chips.gameObject, -1f, 21f);
            var faces = new Object[6];
            for (int i = 0; i < 6; i++)
            {
                Image face = Img(Rect("Face " + (i + 1), chips), dieFaces[i], Cap);
                face.preserveAspect = true;
                Fixed(face.gameObject, 21f, 21f);
                faces[i] = face;
            }

            GamesCallButton call = go.AddComponent<GamesCallButton>();
            var so = new SerializedObject(call);
            Ref(so, "button", button);
            Ref(so, "chance", chance);
            Refs(so, "chipFaces", faces);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        #region Layout
        static void Build(GameObject rootGo)
        {
            rootGo.layer = 5;
            RectTransform root = Stretch((RectTransform)rootGo.transform);
            GamesPanelView view = GetOrAdd<GamesPanelView>(rootGo);
            var so = new SerializedObject(view);

            BuildCard(root, so);
            BuildStrip(root, so);
            Transform card = root.Find("Card");
            if (card != null) Dress((RectTransform)card, "Mount", new Vector2(130f, 130f));
            else Debug.LogError("GamesPanelBuilder: no Card to dress.");

            Refs(so, "tableIcons", new Object[] { diceIcon, hiloIcon, roundIcon });
            so.FindProperty("stripTallest").floatValue = StripTallest;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        #region Synty dressing
        // ui-design.md, Art richness: the shared crest on the panel's top edge, the shared divider in each column split,
        // and one pulsing glow behind the focal element. Runs after Build and finds its targets by name.
        const string CrestPath = "Assets/Data/Prefabs/UI/Reuseable/Ornaments/Panel Crest.prefab";
        const string DividerPath = "Assets/Data/Prefabs/UI/Reuseable/Ornaments/Column Divider.prefab";
        const string SyntyGlow = "Assets/Synty/InterfaceFantasyMenus/Sprites/FX/SPR_FantasyMenus_FX_Glow_01.png";

        static void Dress(RectTransform panel, string focalName, Vector2 glowSize)
        {
            GameObject crest = AssetDatabase.LoadAssetAtPath<GameObject>(CrestPath);
            GameObject divider = AssetDatabase.LoadAssetAtPath<GameObject>(DividerPath);
            Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>(SyntyGlow);
            if (crest == null || divider == null || glow == null) { Debug.LogError("Synty dressing assets missing."); return; }

            PrefabUtility.InstantiatePrefab(crest, panel);

            // Column splits are one-unit-wide "Divider" cells; the shared divider replaces their plain "Line".
            var cells = new List<Transform>();
            foreach (RectTransform t in panel.GetComponentsInChildren<RectTransform>(true))
            {
                if (t.name != "Divider" || t.Find("Line") == null) continue;
                LayoutElement cell = t.GetComponent<LayoutElement>();
                if (cell != null && cell.preferredWidth > 0f && cell.preferredWidth <= 2f) cells.Add(t);
            }
            foreach (Transform cell in cells)
            {
                Object.DestroyImmediate(cell.Find("Line").gameObject);
                PrefabUtility.InstantiatePrefab(divider, cell);
            }

            Transform focal = null;
            foreach (Transform t in panel.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != focalName) continue;
                if (focalName == "Mount" && (t.parent == null || t.parent.name != "Header")) continue;
                focal = t;
                break;
            }
            if (focal == null) { Debug.LogWarning("Synty dressing: no focal '" + focalName + "' under " + panel.name); return; }
            Image glowImage = Img(Rect("Focal Glow", focal), glow, new Color(0.914f, 0.753f, 0.416f, 0.32f));
            Ignore(glowImage.gameObject);
            glowImage.transform.SetAsFirstSibling();
            RectTransform gr = glowImage.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.sizeDelta = glowSize;
            gr.anchoredPosition = Vector2.zero;
            System.Type idle = System.Type.GetType("Memori.UI.UIIdleGlow, Memori.UI");
            if (idle != null) glowImage.gameObject.AddComponent(idle);
            else Debug.LogError("Synty dressing: Memori.UI.UIIdleGlow not found.");
        }
        #endregion

        // The panel's frame, with a candlelit wash inside it falling from the top edge over the header.
        static void Frame(RectTransform panel, float headerHeight)
        {
            GameObject background = Instance(basicBackground, panel, "Basic Background");
            Stretch((RectTransform)background.transform);
            Ignore(background);
            Image fill = Child<Image>(background.transform, "SPR_Background");
            if (fill != null) fill.raycastTarget = true;
            RectTransform texture = Child<RectTransform>(background.transform, "Texture");
            if (texture != null)
            {
                texture.offsetMax = new Vector2(texture.offsetMax.x, -(PanelPadding + headerHeight + 8f));
                Image textureImage = texture.GetComponent<Image>();
                if (textureImage != null) textureImage.pixelsPerUnitMultiplier = TexturePixelsPerUnit;
            }
            Image band = Img(Rect("Band", background.transform), bandGradient, A(Candle, BandAlpha));
            RectTransform bandRect = band.rectTransform;
            bandRect.anchorMin = new Vector2(0f, 1f);
            bandRect.anchorMax = Vector2.one;
            bandRect.pivot = new Vector2(0.5f, 1f);
            bandRect.offsetMin = new Vector2(0f, -(PanelPadding + headerHeight));
            bandRect.offsetMax = Vector2.zero;
            if (texture != null) band.transform.SetSiblingIndex(texture.GetSiblingIndex() + 1);
        }

        static RectTransform Panel(RectTransform root, string name, float top, float width, out CanvasGroup group)
        {
            RectTransform panel = Rect(name, root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.sizeDelta = new Vector2(width, 400f);
            panel.anchoredPosition = new Vector2(0f, -top);
            VerticalLayoutGroup layout = VLayout(panel, 0f, new RectOffset((int)PanelPadding, (int)PanelPadding, (int)PanelPadding, (int)PanelPadding));
            layout.childForceExpandWidth = true;
            ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            group = panel.gameObject.AddComponent<CanvasGroup>();
            return panel;
        }

        static void BuildCard(RectTransform root, SerializedObject so)
        {
            RectTransform card = Panel(root, "Card", CardTop, CardWidth, out CanvasGroup cardGroup);
            // The card moves up before it shrinks, so text stays readable on short canvases such as the Steam Deck's.
            UIFitToCanvas oldCardFit = card.GetComponent<UIFitToCanvas>();
            if (oldCardFit != null) UnityEngine.Object.DestroyImmediate(oldCardFit, true);
            GetOrAdd<UIFitBetweenBars>(card.gameObject);
            Ref(so, "card", cardGroup);
            Frame(card, CardHeaderHeight);

            RectTransform header = Rect("Header", card);
            Fixed(header.gameObject, -1f, CardHeaderHeight);
            HLayout(header, 16f, TextAnchor.MiddleLeft, new RectOffset(22, 22, 0, 0));
            Mount(header, "Mount", 64f, diceIcon, 30f);
            RectTransform names = Rect("Names", header);
            VLayout(names, 2f, new RectOffset());
            Flexible(names.gameObject, 1f).preferredWidth = 0f;
            // The scene's banner is off, so the card carries the node's own name and line (TJ's pick, 2026-10-02).
            Localize(Text("Title", names, displayDrop, 32f, Gold, "Tavern Games"), "Tavern Games");
            Localize(Text("Subtitle", names, display, 17f, Sub, "Subtitle"), "gamesDesc");
            TMP_Text stakes = HeaderPlaque(header, "Stakes", out TMP_Text stakesLabel);
            Localize(stakesLabel, "gamesStakes");
            stakes.text = "Act III";
            Ref(so, "stakesValue", stakes);

            RectTransform columns = Rect("Columns", card);
            HorizontalLayoutGroup row = HLayout(columns, 0f, TextAnchor.UpperLeft, new RectOffset());
            row.childForceExpandHeight = true;
            var groups = new Object[3];
            groups[0] = BuildDiceColumn(columns, so);
            ColumnDivider(columns);
            groups[1] = BuildHigherLowerColumn(columns, so);
            ColumnDivider(columns);
            groups[2] = BuildRoundColumn(columns, so);
            Refs(so, "columns", groups);

            RectTransform footer = Rect("Footer", card);
            Fixed(footer.gameObject, -1f, 80f);
            HLayout(footer, 0f, TextAnchor.MiddleCenter, new RectOffset());
            GameObject skip = Instance(backButton, footer, "Skip");
            Fixed(skip, ColumnWidth - 44f, 56f);
            Localize(Child<TMP_Text>(skip.transform, "Button Label"), "Skip");
            Ref(so, "skipButton", skip.GetComponent<Button>());
        }

        static CanvasGroup BuildDiceColumn(RectTransform columns, SerializedObject so)
        {
            RectTransform column = Column(columns, "Dice Column", out CanvasGroup group);
            ColumnHead(column, diceIcon, "gamesDiceTable");
            TMP_Text[] values = CardStrip(column, new[] { "gamesStake", "gamesBest", "gamesWin" });
            values[1].color = Double;
            Ref(so, "diceStakeValue", values[0]);
            Ref(so, "diceBestValue", values[1]);
            Ref(so, "diceWinValue", values[2]);

            RectTransform lines = Rect("Body", column);
            VLayout(lines, 10f, new RectOffset());
            Localize(Line(lines, "Rule Line", Positive), "gamesDiceLine1");
            Localize(Line(lines, "Double Line", Double), "gamesDiceLine2");

            Spacer(column);
            RectTransform action = Action(column, 8f);
            float width = Mathf.Floor((ColumnWidth - 44f - 16f) / 3f);
            string[] names = { "Small", "Medium", "Large" };
            var stakes = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject stake = Instance(stakePart, action, "Stake " + names[i]);
                Fixed(stake, width, ActionHeight);
                SetArrows(Child<RectTransform>(stake.transform, "Button/Background/Content/Arrows"), i + 1);
                stakes[i] = stake.GetComponent<GamesStakeButton>();
            }
            Refs(so, "stakeButtons", stakes);
            return group;
        }

        // Stacks one to three arrows, centred, as the old bet buttons did.
        static void SetArrows(RectTransform arrows, int count)
        {
            if (arrows == null) return;
            const float step = 6f;
            for (int i = 0; i < arrows.childCount; i++)
            {
                var arrow = (RectTransform)arrows.GetChild(i);
                bool shown = i < count;
                arrow.gameObject.SetActive(shown);
                arrow.anchoredPosition = new Vector2(0f, (count - 1) * step * 0.5f - i * step);
            }
        }

        static CanvasGroup BuildHigherLowerColumn(RectTransform columns, SerializedObject so)
        {
            RectTransform column = Column(columns, "Higher or Lower Column", out CanvasGroup group);
            ColumnHead(column, hiloIcon, "HigherOrLower");
            TMP_Text[] values = CardStrip(column, new[] { "gamesStake", "gamesBest", "gamesCalls" });
            Ref(so, "hiloStakeValue", values[0]);
            Ref(so, "hiloBestValue", values[1]);
            Ref(so, "hiloCallsValue", values[2]);

            RectTransform lines = Rect("Body", column);
            VLayout(lines, 10f, new RectOffset());
            Localize(Line(lines, "Call Line", Positive), "gamesHiLoLine1");
            Localize(Line(lines, "Pot Line", Positive), "gamesHiLoLine2");

            Spacer(column);
            RectTransform action = Action(column, 0f);
            GameObject play = TitledButton(action, "Play", "gamesPlay", out TMP_Text amount);
            Ref(so, "playButton", play.GetComponent<Button>());
            Ref(so, "playAmount", amount);
            return group;
        }

        static CanvasGroup BuildRoundColumn(RectTransform columns, SerializedObject so)
        {
            RectTransform column = Column(columns, "Round Column", out CanvasGroup group);
            ColumnHead(column, roundIcon, "BuyARound");
            TMP_Text[] values = CardStrip(column, new[] { "Cost", "townHeal", "gamesRoll" });
            values[1].color = Positive;
            Localize(values[2], "gamesNone");
            Ref(so, "roundCostValue", values[0]);
            Ref(so, "roundHealValue", values[1]);

            RectTransform lines = Rect("Body", column);
            VLayout(lines, 10f, new RectOffset());
            TMP_Text healLine = Line(lines, "Heal Line", Positive);
            healLine.text = English("gamesRoundLine1");
            Ref(so, "roundHealLine", healLine);
            Localize(Line(lines, "Risk Line", Positive), "gamesRoundLine2");

            Spacer(column);
            RectTransform action = Action(column, 0f);
            GameObject buy = TitledButton(action, "Buy a Round", "BuyARound", out TMP_Text amount);
            Ref(so, "buyButton", buy.GetComponent<Button>());
            Ref(so, "buyAmount", amount);
            return group;
        }

        static void BuildStrip(RectTransform root, SerializedObject so)
        {
            RectTransform strip = Panel(root, "Strip", StripTop, StripWidth, out CanvasGroup stripGroup);
            stripGroup.alpha = 0f;
            stripGroup.interactable = false;
            stripGroup.blocksRaycasts = false;
            Ref(so, "strip", stripGroup);
            Frame(strip, StripHeaderHeight);

            RectTransform header = Rect("Header", strip);
            Fixed(header.gameObject, -1f, StripHeaderHeight);
            HLayout(header, 14f, TextAnchor.MiddleLeft, new RectOffset(20, 20, 0, 0));
            Ref(so, "stripIcon", Mount(header, "Mount", 48f, diceIcon, 22f));
            RectTransform names = Rect("Names", header);
            VLayout(names, 1f, new RectOffset());
            Flexible(names.gameObject, 1f).preferredWidth = 0f;
            TMP_Text title = Text("Title", names, displayDrop, 23f, Gold, English("gamesDiceTable"));
            TMP_Text subtitle = Text("Subtitle", names, display, 15.5f, Cream, English("gamesDiceFlavor"));
            Wrap(subtitle);
            subtitle.enableAutoSizing = true;
            subtitle.fontSizeMin = 12f;
            subtitle.fontSizeMax = 15.5f;
            Ref(so, "stripTitle", title);
            Ref(so, "stripSubtitle", subtitle);
            TMP_Text value = HeaderPlaque(header, "Total", out TMP_Text cap);
            value.text = "45";
            // GamesPanel switches this label between Pot, Stake and Gold, so it gets no localizer that would reset it.
            cap.text = English("gamesPot");
            Ref(so, "stripCap", cap);
            Ref(so, "stripValue", value);

            RectTransform body = Rect("Body", strip);
            VLayout(body, 12f, new RectOffset(20, 20, 14, 14));
            BuildPayoutRow(body, so);
            BuildLadder(body, so);
            BuildCallRow(body, so);
            BuildResultRow(body, so);
        }

        // Two columns of two plaques: a rule and its payout share one line, so four across overflow the strip.
        static void BuildPayoutRow(RectTransform body, SerializedObject so)
        {
            string[] caps = { "HigherCall", "gamesSixes", "gamesTie", "LowerCall" };
            string[] values = { "gamesWinWager", "gamesWinDouble", "gamesWagerBack", "gamesLoseWager" };
            RectTransform row = Rect("Payout", body);
            HLayout(row, PlaqueGap, TextAnchor.UpperLeft, new RectOffset());
            // Fixed-width columns keep both rows' plaques aligned whatever their text.
            float columnWidth = (StripInner - PlaqueGap) / 2f;
            var halves = new RectTransform[2];
            for (int c = 0; c < 2; c++)
            {
                halves[c] = Rect("Column " + (c + 1), row);
                VLayout(halves[c], PlaqueRowGap, new RectOffset());
                Fixed(halves[c].gameObject, columnWidth, -1f);
            }
            var cells = new Object[4];
            var lit = new Object[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject cell = Plaque(halves[i % 2], "Rule " + (i + 1), caps[i]);
                TMP_Text value = Child<TMP_Text>(cell.transform, "Value");
                Localize(value, values[i]);
                // One colour for every payout; only the lit rule's outline says win, tie or loss.
                value.color = Cream;
                cells[i] = cell.AddComponent<CanvasGroup>();

                RectTransform glow = Stretch(Rect("Lit", cell.transform));
                Ignore(glow.gameObject);
                glow.SetAsFirstSibling();
                Img(Stretch(Rect("Fill", glow)), null, A(Positive, 0.1f));
                Image edge = Img(Stretch(Rect("Edge", glow)), frameEdge, A(Positive, 0.7f), Image.Type.Sliced);
                edge.fillCenter = false;
                edge.pixelsPerUnitMultiplier = 4f;
                glow.gameObject.SetActive(false);
                lit[i] = glow.gameObject;
            }
            Ref(so, "payoutRow", row.gameObject);
            Refs(so, "payoutCells", cells);
            Refs(so, "payoutLit", lit);
        }

        static void BuildLadder(RectTransform body, SerializedObject so)
        {
            RectTransform ladder = Rect("Ladder", body);
            Fixed(ladder.gameObject, -1f, 52f);
            RectTransform steps = Rect("Steps", ladder);
            steps.anchorMin = steps.anchorMax = new Vector2(0.5f, 1f);
            steps.pivot = new Vector2(0.5f, 1f);
            steps.sizeDelta = new Vector2(StripInner, 52f);
            steps.anchoredPosition = Vector2.zero;
            string[] keys = { "gamesStake", "gamesLadder1", "gamesLadder2", "gamesLadder3", "gamesCleared" };
            float spacing = (StripInner - LadderStep) / (keys.Length - 1);

            var links = new Object[keys.Length - 1];
            for (int i = 0; i < links.Length; i++)
            {
                Image link = Img(Rect("Link " + (i + 1), steps), null, Brass);
                link.rectTransform.anchorMin = link.rectTransform.anchorMax = new Vector2(0f, 1f);
                link.rectTransform.pivot = new Vector2(0f, 0.5f);
                link.rectTransform.sizeDelta = new Vector2(spacing, 1f);
                link.rectTransform.anchoredPosition = new Vector2(LadderStep * 0.5f + spacing * i, -6f);
                links[i] = link;
            }
            var fills = new Object[keys.Length];
            var borders = new Object[keys.Length];
            var glows = new Object[keys.Length];
            var labels = new Object[keys.Length];
            var values = new Object[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                RectTransform step = Rect("Step " + (i + 1), steps);
                step.anchorMin = step.anchorMax = new Vector2(0f, 1f);
                step.pivot = new Vector2(0.5f, 1f);
                step.sizeDelta = new Vector2(LadderStep, 52f);
                step.anchoredPosition = new Vector2(LadderStep * 0.5f + spacing * i, 0f);
                glows[i] = Diamond(step, "Glow", 17f, A(Gold, 0.22f));
                borders[i] = Diamond(step, "Border", 12f, Brass);
                fills[i] = Diamond(step, "Fill", 8.5f, Gold);
                TMP_Text label = Text("Label", step, display, 12f, Sub, keys[i]);
                Anchor(label.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(2f, -32f), new Vector2(-2f, -15f));
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 9f;
                label.fontSizeMax = 12f;
                Localize(label, keys[i]);
                labels[i] = label;
                TMP_Text value = Text("Value", step, displayDrop, 14f, Coin, "30");
                Anchor(value.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(2f, -52f), new Vector2(-2f, -33f));
                value.alignment = TextAlignmentOptions.Center;
                values[i] = value;
            }
            Ref(so, "ladder", ladder.gameObject);
            Refs(so, "ladderFills", fills);
            Refs(so, "ladderBorders", borders);
            Refs(so, "ladderGlows", glows);
            Refs(so, "ladderLabels", labels);
            Refs(so, "ladderValues", values);
            Refs(so, "ladderLinks", links);
        }

        static void BuildCallRow(RectTransform body, SerializedObject so)
        {
            RectTransform row = Rect("Calls", body);
            HLayout(row, 16f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(row.gameObject, -1f, 70f);
            float width = Mathf.Floor((StripInner - 32f) / 3f);

            GameObject higher = Instance(callPart, row, "Higher");
            Fixed(higher, width, 70f);
            Localize(Child<TMP_Text>(higher.transform, "Button/Background/Content/Top/Title"), "HigherCall");
            GameObject lower = Instance(callPart, row, "Lower");
            Fixed(lower, width, 70f);
            Localize(Child<TMP_Text>(lower.transform, "Button/Background/Content/Top/Title"), "LowerCall");

            GameObject cashOut = Instance(primaryButton, row, "Cash Out");
            Fixed(cashOut, width, 70f);
            Localize(Child<TMP_Text>(cashOut.transform, "Button Label"), "CashOut");
            TMP_Text detail = Text("Detail", cashOut.transform, display, 14f, PrimaryDetail, "Take 45");
            Anchor(detail.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(20f, 6f), new Vector2(-70f, 0f));
            detail.alignment = TextAlignmentOptions.BottomLeft;
            detail.enableAutoSizing = true;
            detail.fontSizeMin = 11f;
            detail.fontSizeMax = 14f;

            row.gameObject.SetActive(false);
            Ref(so, "callRow", row.gameObject);
            Ref(so, "higherButton", higher.GetComponent<GamesCallButton>());
            Ref(so, "lowerButton", lower.GetComponent<GamesCallButton>());
            Ref(so, "cashOutButton", cashOut.GetComponent<Button>());
            Ref(so, "cashOutDetail", detail);
        }

        static void BuildResultRow(RectTransform body, SerializedObject so)
        {
            RectTransform row = Rect("Result", body);
            HLayout(row, 16f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(row.gameObject, -1f, 80f);

            RectTransform readout = Rect("Readout", row);
            HLayout(readout, 12f, TextAnchor.MiddleLeft, new RectOffset());
            Face(readout, "You", "gamesYou", out Image youFace, out Image youRing);
            TMP_Text vs = Text("Vs", readout, display, 15f, Sub, "vs");
            vs.fontStyle = FontStyles.Italic;
            vs.margin = new Vector4(0f, 0f, 0f, 18f);
            Localize(vs, "gamesVs");
            Face(readout, "House", "gamesHouse", out Image houseFace, out Image houseRing);

            RectTransform hint = Rect("Rewind Hint", row);
            HLayout(hint, 10f, TextAnchor.MiddleLeft, new RectOffset());
            Flexible(hint.gameObject, 10f).preferredWidth = 0f;
            Image potion = Img(Rect("Icon", hint), rewindIcon, Color.white);
            potion.preserveAspect = true;
            Fixed(potion.gameObject, 40f, 40f);
            RectTransform texts = Rect("Texts", hint);
            VLayout(texts, 1f, new RectOffset());
            Flexible(texts.gameObject, 1f).preferredWidth = 0f;
            Localize(Text("Title", texts, displayDrop, 16f, Cream, "Title"), "gamesRewindTitle");
            TMP_Text hintBody = Text("Body", texts, display, 14f, Sub, "Body");
            Wrap(hintBody);
            Localize(hintBody, "gamesRewindBody");

            RectTransform roundNote = Rect("Round Note", row);
            HLayout(roundNote, 10f, TextAnchor.MiddleLeft, new RectOffset());
            Flexible(roundNote.gameObject, 10f).preferredWidth = 0f;
            RectTransform healMount = Rect("Mount", roundNote);
            Fixed(healMount.gameObject, 44f, 44f);
            Img(Stretch(Rect("Diamond", healMount)), mount, Color.white);
            Image heal = Img(Rect("Icon", healMount), roundIcon, Positive);
            heal.preserveAspect = true;
            Centre(heal.rectTransform, 20f, 20f);
            TMP_Text roundLine = Text("Text", roundNote, displayDrop, 17f, Positive, "Every unit heals 20% of its health.");
            Wrap(roundLine);
            Flexible(roundLine.gameObject, 1f).preferredWidth = 0f;
            roundNote.gameObject.SetActive(false);

            Spacer(row, horizontal: true);
            GameObject next = Instance(primaryButton, row, "Continue");
            Fixed(next, 220f, 78f);
            Localize(Child<TMP_Text>(next.transform, "Button Label"), "continueButton");

            row.gameObject.SetActive(false);
            Ref(so, "resultRow", row.gameObject);
            Ref(so, "readout", readout.gameObject);
            Ref(so, "youFace", youFace);
            Ref(so, "houseFace", houseFace);
            Refs(so, "dieFaces", dieFaces);
            Ref(so, "youRing", youRing);
            Ref(so, "houseRing", houseRing);
            Ref(so, "rewindHint", hint.gameObject);
            Ref(so, "roundNote", roundNote.gameObject);
            Ref(so, "roundNoteText", roundLine);
            Ref(so, "continueButton", next.GetComponent<Button>());
        }

        // A flat die tile showing the face as pips, with a ring that marks the winner or loser.
        static void Face(RectTransform parent, string name, string key, out Image face, out Image ring)
        {
            RectTransform block = Rect(name, parent);
            VerticalLayoutGroup layout = VLayout(block, 5f, new RectOffset());
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            RectTransform tile = Rect("Tile", block);
            Fixed(tile.gameObject, 44f, 44f);
            ring = Img(Rect("Ring", tile), AssetDatabase.LoadAssetAtPath<Sprite>(SyntyGlow), A(Positive, 50f / 255f));
            Centre(ring.rectTransform, 100f, 100f);
            ring.enabled = false;
            // The pips are cut out of the face sprite, so a dark die underneath fills them and hides the ring.
            Img(Stretch(Rect("Pips", tile)), dieBlank, Pip);
            face = Img(Stretch(Rect("Die", tile)), dieFaces[4], Ivory);
            TMP_Text label = SectionTitle(block, "Label");
            label.alignment = TextAlignmentOptions.Center;
            Localize(label, key);
        }
        #endregion

        #region Widgets
        static RectTransform Column(RectTransform parent, string name, out CanvasGroup group)
        {
            RectTransform column = Rect(name, parent);
            VerticalLayoutGroup layout = VLayout(column, 14f, new RectOffset(22, 22, 18, 20));
            layout.childForceExpandWidth = true;
            // Fixed, because a text's preferred width ignores wrapping and would widen whichever column holds the longer line.
            Fixed(column.gameObject, ColumnWidth, -1f);
            group = column.gameObject.AddComponent<CanvasGroup>();
            return column;
        }

        static void ColumnDivider(RectTransform columns)
        {
            RectTransform divider = Rect("Divider", columns);
            Fixed(divider.gameObject, 1f, -1f);
            Img(Stretch(Rect("Line", divider), 0f, 0f, 16f, 16f), null, A(Brass, 0.45f));
        }

        static void ColumnHead(RectTransform column, Sprite icon, string titleKey)
        {
            RectTransform head = Rect("Head", column);
            HLayout(head, 12f, TextAnchor.MiddleLeft, new RectOffset());
            Mount(head, "Mount", 44f, icon, 20f);
            RectTransform titles = Rect("Titles", head);
            VLayout(titles, 1f, new RectOffset());
            Flexible(titles.gameObject, 1f).preferredWidth = 0f;
            TMP_Text title = Text("Title", titles, displayDrop, 22f, Gold, titleKey);
            title.enableAutoSizing = true;
            title.fontSizeMin = 16f;
            title.fontSizeMax = 22f;
            Localize(title, titleKey);
        }

        // One Stat Plaque without an icon; gold values carry the coin sprite in their text. A null key leaves the label to the caller.
        static GameObject Plaque(RectTransform parent, string name, string labelKey)
        {
            GameObject plaque = Instance(statPlaque, parent, name);
            Child<Image>(plaque.transform, "Icon").gameObject.SetActive(false);
            if (labelKey != null) Localize(Child<TMP_Text>(plaque.transform, "Label"), labelKey);
            return plaque;
        }

        // The column's stats, one plaque per row: three side by side overflow the 338 px column ("Best" beside "Win double").
        static TMP_Text[] CardStrip(RectTransform column, string[] keys)
        {
            RectTransform strip = Rect("Strip", column);
            VLayout(strip, PlaqueRowGap, new RectOffset());
            var values = new TMP_Text[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                values[i] = Child<TMP_Text>(Plaque(strip, "Plaque " + (i + 1), keys[i]).transform, "Value");
            return values;
        }

        // A wrapped rule line behind a small brass diamond.
        static TMP_Text Line(RectTransform parent, string name, Color bullet)
        {
            RectTransform line = Rect(name, parent);
            HorizontalLayoutGroup layout = HLayout(line, 9f, TextAnchor.UpperLeft, new RectOffset());
            layout.childForceExpandHeight = false;
            RectTransform slot = Rect("Bullet", line);
            Fixed(slot.gameObject, 10f, 20f);
            Image dot = Img(Rect("Diamond", slot), null, bullet);
            Centre(dot.rectTransform, 7f, 7f);
            dot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            TMP_Text text = Text("Text", line, display, 15f, Cream, name);
            Wrap(text);
            Flexible(text.gameObject, 1f).preferredWidth = 0f;
            return text;
        }

        static RectTransform Action(RectTransform column, float spacing)
        {
            RectTransform action = Rect("Action", column);
            Fixed(action.gameObject, -1f, ActionHeight);
            HLayout(action, spacing, TextAnchor.MiddleCenter, new RectOffset());
            return action;
        }

        // A full-width Standard button with a title and a gold amount side by side.
        static GameObject TitledButton(RectTransform parent, string name, string titleKey, out TMP_Text amount)
        {
            GameObject holder = Rect(name, parent).gameObject;
            Fixed(holder, ColumnWidth - 44f, ActionHeight);
            RectTransform content = ButtonShell(holder, standardButton, out Button button);
            // Both labels sit on one baseline so the coin sprite's taller line box cannot lift the amount; the top padding centres the capitals.
            HLayout(content, 12f, TextAnchor.MiddleCenter, new RectOffset(10, 10, 13, 0));
            TMP_Text title = Text("Title", content, displayDrop, 19f, White, titleKey);
            title.alignment = TextAlignmentOptions.BaselineLeft;
            Localize(title, titleKey);
            amount = Text("Amount", content, displayDrop, 19f, Cream, "30");
            amount.alignment = TextAlignmentOptions.BaselineLeft;
            // The holder is layout only; GamesPanelView needs the Button itself.
            return button.gameObject;
        }

        // A Stat Plaque at the header's right edge; its holder fixes the width so the title keeps the rest of the row.
        static TMP_Text HeaderPlaque(RectTransform header, string name, out TMP_Text label)
        {
            RectTransform holder = Rect(name, header);
            Fixed(holder.gameObject, HeaderPlaqueWidth, -1f);
            HLayout(holder, 0f, TextAnchor.MiddleCenter, new RectOffset());
            GameObject plaque = Plaque(holder, "Plaque", null);
            // The plaque's see-through fill turns grey over the header's warm band, so it gets the body's dark behind it.
            Image backing = Img(Stretch(Rect("Backing", plaque.transform)), null, PlaqueBacking);
            Ignore(backing.gameObject);
            backing.raycastTarget = false;
            backing.transform.SetAsFirstSibling();
            label = Child<TMP_Text>(plaque.transform, "Label");
            return Child<TMP_Text>(plaque.transform, "Value");
        }

        // Section labels are normal case with no letter spacing; spaced capitals read as a web dashboard.
        static TMP_Text SectionTitle(RectTransform parent, string name) => Text(name, parent, displayDrop, 18f, Gold, name);

        static void Spacer(RectTransform parent, bool horizontal = false)
        {
            LayoutElement spacer = Rect("Spacer", parent).gameObject.AddComponent<LayoutElement>();
            if (horizontal) spacer.flexibleWidth = 1f;
            else spacer.flexibleHeight = 1f;
        }

        static Image Mount(RectTransform parent, string name, float size, Sprite icon, float iconSize)
        {
            RectTransform cell = Rect(name, parent);
            Fixed(cell.gameObject, size, size);
            // The mount art has transparent margins, so it is drawn a little larger than its cell, as in the tooltip.
            Image diamond = Img(Rect("Diamond", cell), mount, Color.white);
            Centre(diamond.rectTransform, size * 52f / 44f, size * 52f / 44f);
            Image image = Img(Rect("Icon", cell), icon, Gold);
            image.preserveAspect = true;
            Centre(image.rectTransform, iconSize, iconSize);
            return image;
        }

        static Image Diamond(RectTransform parent, string name, float size, Color colour)
        {
            Image image = Img(Rect(name, parent), null, colour);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(0f, -6f);
            rect.localEulerAngles = new Vector3(0f, 0f, 45f);
            return image;
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
            if (component == null) Debug.LogError($"GamesPanelBuilder: {root.name} has no {typeof(T).Name} at '{path}'.");
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
            if (property == null) { Debug.LogError($"GamesPanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.objectReferenceValue = value;
        }

        static void Refs(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"GamesPanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        #endregion
    }
}
