using System;
using System.Collections.Generic;
using Memori.Tooltip;
using TJ.MainMenu;
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

namespace TJ.Town.EditorTools
{
    /// <summary>
    /// Generates the town panel (Town Panel UI.prefab and its parts) on the game's Basic Background and installs it in
    /// Map.unity. Rebuilding the panel keeps hand edits to the part prefabs.
    /// </summary>
    public static class TownPanelBuilder
    {
        public const string PartFolder = "Assets/Data/Prefabs/UI/Map/Town";
        public const string PanelPath = PartFolder + "/Town Panel UI.prefab";
        const string StatCellPath = PartFolder + "/Town Stat Cell.prefab";
        const string SpoilRowPath = PartFolder + "/Town Spoil Row.prefab";
        const string ScenePath = "Assets/Scenes/Map.unity";
        const string ButtonFolder = "Assets/Data/Prefabs/UI/Reuseable/Buttons";
        const string ChipPath = "Assets/Data/Prefabs/UI/Collection/Chip.prefab";
        const string BasicBackgroundPath = "Assets/Data/Prefabs/UI/Reuseable/Basic Background.prefab";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";
        const string TableName = "MainLocalizationTable";
        const string OldPanelName = "Town Center Group";
        const float PanelWidth = 1160f;
        // The panel's centre sits 425 px from the top of a 1080 screen, midway between the top bar and the army bar.
        const float PanelCentreY = 115f;
        const float GarrisonScale = 0.9f;
        // The faction wash; linear colour makes it read about twice this strong.
        const float BandAlpha = 0.12f;
        const float PanelPadding = 5f;
        const float HeaderHeight = 104f;
        // The background's tracery starts below the header (TJ, 2026-09-25).
        const float TextureTop = 115f;
        const float TexturePixelsPerUnit = 12f;
        // Wide enough for Texturina capitals in the longer locales (CASTILLO, CHATEAU).
        const float LadderStep = 84f;
        // Two equal whole-pixel columns and the 1 px divider fill the panel inside its 5 px frame edge.
        const float ColumnWidth = 574f;

        #region Style
        static readonly Color Slate = Hex("1F2B2E");
        static readonly Color Well = Hex("162023");
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        static readonly Color White = Hex("ECF0F1");
        static readonly Color Sub = Hex("B4AA94");
        static readonly Color Flavour = Hex("A99F8A");
        static readonly Color Cap = Hex("8C9AA2");
        static readonly Color Detail = Hex("A9B9C6");
        static readonly Color Positive = Hex("7BD66F");
        static readonly Color Negative = Hex("E3695E");
        static readonly Color Coin = Hex("E3BB71");
        static readonly Color Taken = Hex("9FB0B8");
        static readonly Color TakenTitle = Hex("7E8A8F");
        static readonly Color FightFill = new(0.18660378f, 0.056990035f, 0.061990038f);
        static readonly Color FightHue = new(0.7830189f, 0.08495018f, 0.08495018f);
        static readonly Color FightHover = new(0.9787736f, 0.018929051f, 0.018929051f);
        static readonly Color FightHalo = new(1f, 0f, 0f);
        static readonly Color FightPointer = new(1f, 0.3625707f, 0.3625707f);
        static readonly Color FightIcon = new(0.8f, 0.54368484f, 0.5411765f);
        static readonly Color Sun = Hex("F2C866");
        static readonly Color WellEdge = Hex("605635", 0.9f);

        static TMP_FontAsset displayDrop, display;
        static Sprite mount, solid, squareSliced, edgeFade;
        static Sprite townIcon, swordIcon, healIcon, helmIcon, gateIcon, goldIcon, chestIcon, heartIcon;
        static Sprite clearIcon, rainIcon, fogIcon, snowIcon;
        static GameObject primaryButton, standardButton, chip, basicBackground;

        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            var sheet = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite) sheet[sprite.name] = sprite;
            sheet.TryGetValue("TooltipMount", out mount);
            sheet.TryGetValue("TooltipSolid", out solid);
            if (mount == null || solid == null) Debug.LogError("TownPanelBuilder: tooltip sheet sprites missing.");
            squareSliced = Load<Sprite>("Assets/Art/Icons/UI/SquareSliced.png");
            edgeFade = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Shadow/Vertical Shadow.png");
            townIcon = Load<Sprite>("Assets/Art/Icons/Map/Town.png");
            swordIcon = Load<Sprite>("Assets/Art/Icons/Map/Engagement.png");
            healIcon = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Healing01_Clean.png");
            helmIcon = Load<Sprite>("Assets/Art/Icons/Map/Recruit.png");
            gateIcon = Load<Sprite>("Assets/Art/Icons/UnitTypes/Gate.png");
            goldIcon = Load<Sprite>("Assets/Art/Icons/Events/Gold.png");
            chestIcon = Load<Sprite>("Assets/Art/Icons/Map/Treasure.png");
            heartIcon = Load<Sprite>("Assets/Art/Icons/Stats/Health.png");
            clearIcon = Load<Sprite>("Assets/Art/Icons/Achievements/SunClean.png");
            rainIcon = Load<Sprite>("Assets/Art/Icons/Map/BattlefieldConditions/Rain.png");
            fogIcon = Load<Sprite>("Assets/Art/Icons/Map/BattlefieldConditions/Fog.png");
            snowIcon = Load<Sprite>("Assets/Art/Icons/Map/BattlefieldConditions/Snow.png");
            primaryButton = Load<GameObject>(ButtonFolder + "/Button - Primary.prefab");
            standardButton = Load<GameObject>(ButtonFolder + "/Button - Standard.prefab");
            chip = Load<GameObject>(ChipPath);
            basicBackground = Load<GameObject>(BasicBackgroundPath);
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"TownPanelBuilder: missing {typeof(T).Name} at {path}");
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
            if (entry == null) throw new InvalidOperationException($"TownPanelBuilder: no localization key '{key}'.");
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
        [MenuItem("Tabletop Tavern/Town Panel/Rebuild Prefab")]
        public static void BuildPrefab()
        {
            LoadAssets();
            LoadTable();
            EnsureParts(false);
            BuildPanel();
        }

        [MenuItem("Tabletop Tavern/Town Panel/Reset Part Prefabs")]
        static void ResetPartsMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset town panel parts",
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
            EnsureParts(true);
            BuildPanel();
        }

        [MenuItem("Tabletop Tavern/Town Panel/Install In Map")]
        public static void InstallMenu() => Debug.Log("TownPanelBuilder: " + InstallInMap());

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
                root = new GameObject("Town Panel UI", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
            }
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root);
                Normalize(root);
                RecordOverrides(root);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                Debug.Log($"TownPanelBuilder: wrote {PanelPath}");
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
        /// Replaces the old hand-built town panel under Map.unity's Town Panel with the prefab, points TownPanel at it
        /// and saves only Map.unity.
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
                TownPanel townPanel = null;
                foreach (GameObject root in map.GetRootGameObjects())
                {
                    townPanel = root.GetComponentInChildren<TownPanel>(true);
                    if (townPanel != null) break;
                }
                if (townPanel == null) return "no TownPanel in Map.unity.";

                Transform old = townPanel.transform.Find(OldPanelName);
                if (old != null)
                {
                    string outside = CheckReferences(map, old, townPanel);
                    if (!string.IsNullOrEmpty(outside)) return "stopped, outside references into the old panel:\n" + outside;
                    Object.DestroyImmediate(old.gameObject);
                }
                for (int i = townPanel.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = townPanel.transform.GetChild(i).gameObject;
                    if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(child) == prefab) Object.DestroyImmediate(child);
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, townPanel.transform);
                var so = new SerializedObject(townPanel);
                Ref(so, "view", instance.GetComponent<TownPanelView>());
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(map);
                if (!EditorSceneManager.SaveScene(map)) return "Map.unity did not save.";
                return old != null ? "installed; the old panel was removed." : "installed.";
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(map, true);
            }
        }

        // Anything outside the old panel that points into it would lose its target when the old panel is deleted.
        static string CheckReferences(Scene scene, Transform old, TownPanel townPanel)
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
                        if (component == null || component == townPanel) continue;
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
        static GameObject statCellPart, spoilRowPart;

        static void EnsureParts(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(PartFolder)) AssetDatabase.CreateFolder("Assets/Data/Prefabs/UI/Map", "Town");
            statCellPart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(StatCellPath);
            if (statCellPart == null) statCellPart = SavePart(StatCellPath, "Town Stat Cell", null, StatCellPart);
            spoilRowPart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(SpoilRowPath);
            if (spoilRowPart == null) spoilRowPart = SavePart(SpoilRowPath, "Town Spoil Row", null, SpoilRowPart);
        }

        static GameObject SavePart(string path, string name, GameObject basePrefab, Action<GameObject> build)
        {
            // Built in a preview scene so the open scenes are never touched or marked changed.
            Scene stage = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                if (basePrefab == null)
                {
                    root = new GameObject(name, typeof(RectTransform));
                    SceneManager.MoveGameObjectToScene(root, stage);
                }
                else root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, stage);
                root.name = name;
                root.layer = 5;
                build(root);
                Normalize(root);
                RecordOverrides(root);
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"TownPanelBuilder: wrote {path}");
                return asset;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        static void StatCellPart(GameObject go)
        {
            RectTransform cell = (RectTransform)go.transform;
            VerticalLayoutGroup layout = VLayout(cell, 5f, new RectOffset(14, 14, 0, 0));
            layout.childAlignment = TextAnchor.MiddleLeft;
            Flexible(go, 1f).preferredWidth = 0f;
            // The line between cells; the strip hides it on its first cell.
            Image divider = Img(Rect("Divider", cell), null, A(Brass, 0.45f));
            Anchor(divider.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -19f), new Vector2(1f, 19f));
            divider.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            TMP_Text label = Text("Label", cell, display, 12f, Cap, "Label");
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = 10f;
            RectTransform row = Rect("Value Row", cell);
            HLayout(row, 7f, TextAnchor.MiddleLeft, new RectOffset());
            Image icon = Img(Rect("Icon", row), null, Sub);
            icon.preserveAspect = true;
            Fixed(icon.gameObject, 16f, 16f);
            TMP_Text value = Text("Value", row, displayDrop, 16f, Cream, "Value");
            value.enableAutoSizing = true;
            value.fontSizeMin = 12f;
            value.fontSizeMax = 16f;
            Flexible(value.gameObject, 1f).preferredWidth = 0f;
        }

        static void SpoilRowPart(GameObject go)
        {
            RectTransform slot = (RectTransform)go.transform;
            slot.sizeDelta = new Vector2(526f, 68f);
            Fixed(go, -1f, 68f);
            Flexible(go, 1f);

            // Revealed once the reward is taken and its button pops away.
            RectTransform taken = Stretch(Rect("Taken", slot));
            HLayout(taken, 10f, TextAnchor.MiddleCenter, new RectOffset());
            TMP_Text takenTitle = Text("Title", taken, display, 17f, TakenTitle, "Loot Gold");
            Image dot = Img(Rect("Diamond", taken), null, Taken);
            Fixed(dot.gameObject, 8f, 8f);
            dot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            TMP_Text takenLabel = Text("Label", taken, displayDrop, 16f, Taken, "Taken");
            Localize(takenLabel, "townSpoilTaken");
            taken.gameObject.SetActive(false);

            // The pop animates this wrapper, so it never fights the button's own hover scale.
            RectTransform pop = Stretch(Rect("Pop", slot));
            CanvasGroup popGroup = pop.gameObject.AddComponent<CanvasGroup>();
            GameObject buttonGo = Instance(standardButton, pop, "Button");
            Stretch((RectTransform)buttonGo.transform);
            Transform buttonLabel = buttonGo.transform.Find("Button Label");
            if (buttonLabel != null) buttonLabel.gameObject.SetActive(false);

            RectTransform content = Stretch(Rect("Content", buttonGo.transform));

            RectTransform mountRect = Rect("Mount", content);
            mountRect.anchorMin = mountRect.anchorMax = new Vector2(0f, 0.5f);
            mountRect.pivot = new Vector2(0.5f, 0.5f);
            mountRect.sizeDelta = new Vector2(54f, 54f);
            mountRect.anchoredPosition = new Vector2(13f, 0f);
            Img(Stretch(Rect("Diamond", mountRect)), mount, Color.white);
            Image icon = Img(Rect("Icon", mountRect), goldIcon, Gold);
            icon.preserveAspect = true;
            Centre(icon.rectTransform, 24f, 24f);

            TMP_Text title = Text("Title", content, displayDrop, 19f, White, "Loot Gold");
            Anchor(title.rectTransform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(56f, 0f), new Vector2(-190f, -6f));
            title.alignment = TextAlignmentOptions.BottomLeft;
            TMP_Text detail = Text("Detail", content, display, 14f, Detail, "Detail");
            Anchor(detail.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(56f, 6f), new Vector2(-190f, -2f));
            detail.alignment = TextAlignmentOptions.TopLeft;

            TMP_Text value = Text("Value", content, displayDrop, 22f, Coin, "0");
            value.alignment = TextAlignmentOptions.MidlineRight;
            RightSlot(value.rectTransform);

            TownSpoilRow spoil = go.AddComponent<TownSpoilRow>();
            var so = new SerializedObject(spoil);
            Ref(so, "button", buttonGo.GetComponent<Button>());
            Ref(so, "title", title);
            Ref(so, "detail", detail);
            Ref(so, "value", value);
            Ref(so, "taken", taken.gameObject);
            Ref(so, "takenTitle", takenTitle);
            Ref(so, "pop", pop);
            Ref(so, "popGroup", popGroup);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RightSlot(RectTransform rect)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(180f, 0f);
            rect.anchoredPosition = new Vector2(-22f, 0f);
        }
        #endregion

        #region Layout
        static void Build(GameObject rootGo)
        {
            rootGo.layer = 5;
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(PanelWidth, 620f);
            root.anchoredPosition = new Vector2(0f, PanelCentreY);
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
            TownPanelView view = GetOrAdd<TownPanelView>(rootGo);
            var so = new SerializedObject(view);

            // The standard panel background most of the game's UI already uses.
            GameObject background = Instance(basicBackground, root, "Basic Background");
            Stretch((RectTransform)background.transform);
            Ignore(background);
            Image fill = Child<Image>(background.transform, "SPR_Background");
            if (fill != null) fill.raycastTarget = true;
            RectTransform texture = Child<RectTransform>(background.transform, "Texture");
            if (texture != null)
            {
                texture.offsetMax = new Vector2(texture.offsetMax.x, -TextureTop);
                Image textureImage = texture.GetComponent<Image>();
                if (textureImage != null) textureImage.pixelsPerUnitMultiplier = TexturePixelsPerUnit;
            }

            // The faction wash sits inside the background, under its frame and corner ornaments, and fills the header
            // from the panel's top edge down to the header rule.
            Image band = Img(Rect("Band", background.transform), edgeFade, A(Hex("E27B58"), BandAlpha));
            RectTransform bandRect = band.rectTransform;
            bandRect.anchorMin = new Vector2(0f, 1f);
            bandRect.anchorMax = Vector2.one;
            bandRect.pivot = new Vector2(0.5f, 1f);
            bandRect.offsetMin = new Vector2(0f, -(PanelPadding + HeaderHeight));
            bandRect.offsetMax = Vector2.zero;
            if (texture != null) band.transform.SetSiblingIndex(texture.GetSiblingIndex() + 1);
            Ref(so, "factionBand", band);

            BuildHeader(root, so);
            BuildColumns(root, so);

            Refs(so, "weatherIcons", new Object[] { clearIcon, rainIcon, fogIcon, snowIcon });
            so.FindProperty("factionBandAlpha").floatValue = BandAlpha;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildHeader(RectTransform root, SerializedObject so)
        {
            RectTransform header = Rect("Header", root);
            Fixed(header.gameObject, -1f, HeaderHeight);
            HLayout(header, 18f, TextAnchor.MiddleLeft, new RectOffset(22, 22, 0, 0));

            Image bandRule = Img(Rect("Band Rule", header), solid, A(Brass, 0.35f));
            Anchor(bandRule.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            Ignore(bandRule.gameObject);

            Mount(header, "Mount", 64f, townIcon, 28f);

            RectTransform names = Rect("Names", header);
            VLayout(names, 2f, new RectOffset());
            Flexible(names.gameObject, 1f).preferredWidth = 0f;
            TMP_Text townName = Text("Name", names, displayDrop, 32f, Gold, "Dunwich");
            TMP_Text subtitle = Text("Subtitle", names, display, 17f, Sub, "The Iron Legion Castle");
            Ref(so, "townName", townName);
            Ref(so, "townSubtitle", subtitle);

            RectTransform ladder = Rect("Size Ladder", header);
            Fixed(ladder.gameObject, LadderStep * 3f, 42f);
            string[] sizes = { "Village", "Castle", "City" };
            var links = new Object[2];
            for (int i = 0; i < 2; i++)
            {
                Image link = Img(Rect("Link " + (i + 1), ladder), null, Brass);
                link.rectTransform.anchorMin = link.rectTransform.anchorMax = new Vector2(0f, 1f);
                link.rectTransform.pivot = new Vector2(0f, 0.5f);
                link.rectTransform.sizeDelta = new Vector2(LadderStep, 1f);
                link.rectTransform.anchoredPosition = new Vector2(LadderStep * (i + 0.5f), -6f);
                links[i] = link;
            }
            var fills = new Object[3];
            var borders = new Object[3];
            var glows = new Object[3];
            var labels = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform step = Rect(sizes[i], ladder);
                step.anchorMin = step.anchorMax = new Vector2(0f, 1f);
                step.pivot = new Vector2(0.5f, 1f);
                step.sizeDelta = new Vector2(LadderStep, 42f);
                step.anchoredPosition = new Vector2(LadderStep * (i + 0.5f), 0f);
                glows[i] = Diamond(step, "Glow", 17f, A(Gold, 0.2f));
                borders[i] = Diamond(step, "Border", 12f, Brass);
                fills[i] = Diamond(step, "Fill", 8.5f, Gold);
                TMP_Text label = Text("Label", step, display, 12f, Sub, sizes[i]);
                Anchor(label.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(2f, 0f), new Vector2(-2f, 18f));
                label.alignment = TextAlignmentOptions.Bottom;
                label.fontStyle = FontStyles.UpperCase;
                label.characterSpacing = 3f;
                label.enableAutoSizing = true;
                label.fontSizeMin = 9f;
                label.fontSizeMax = 12f;
                Localize(label, sizes[i]);
                labels[i] = label;
            }
            Refs(so, "sizeFills", fills);
            Refs(so, "sizeBorders", borders);
            Refs(so, "sizeGlows", glows);
            Refs(so, "sizeLabels", labels);
            Refs(so, "sizeLinks", links);

            RectTransform status = Rect("Status", header);
            Fixed(status.gameObject, 110f, 44f);
            HLayout(status, 0f, TextAnchor.MiddleRight, new RectOffset());

            GameObject info = Instance(standardButton, status, "Town Info");
            Fixed(info, 44f, 44f);
            TMP_Text question = Child<TMP_Text>(info.transform, "Button Label");
            foreach (LocalizeStringEvent localizer in question.GetComponents<LocalizeStringEvent>()) localizer.enabled = false;
            question.text = "?";
            question.alignment = TextAlignmentOptions.Midline;
            question.margin = Vector4.zero;
            question.enableAutoSizing = false;
            question.fontSize = 20f;
            Ref(so, "townInfoButton", info);
            Ref(so, "townInfoTooltip", info.AddComponent<MemoriTooltipTrigger>());

            Ref(so, "enteredPill", Pill(status, "Entered", "townEntered", Positive));
            Ref(so, "sackedPill", Pill(status, "Sacked", "townSacked", Negative));
        }

        static void BuildColumns(RectTransform root, SerializedObject so)
        {
            RectTransform columns = Rect("Columns", root);
            HorizontalLayoutGroup row = HLayout(columns, 0f, TextAnchor.UpperLeft, new RectOffset());
            row.childForceExpandHeight = true;

            BuildEnterColumn(columns, so);

            RectTransform divider = Rect("Divider", columns);
            Fixed(divider.gameObject, 1f, -1f);
            Img(Stretch(Rect("Line", divider), 0f, 0f, 16f, 16f), null, A(Brass, 0.45f));

            BuildFightColumn(columns, so);

            RectTransform or = Rect("Or", columns);
            Centre(or, 34f, 34f);
            Ignore(or.gameObject);
            Image orFill = Img(Rect("Fill", or), solid, Slate);
            Centre(orFill.rectTransform, 24f, 24f);
            orFill.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Image orEdge = Img(Rect("Edge", or), squareSliced, Brass, Image.Type.Sliced);
            orEdge.fillCenter = false;
            Centre(orEdge.rectTransform, 24f, 24f);
            orEdge.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            TMP_Text orLabel = Text("Label", or, display, 14f, Sub, "or");
            Stretch(orLabel.rectTransform);
            orLabel.alignment = TextAlignmentOptions.Center;
            orLabel.fontStyle = FontStyles.Italic;
            // Longer translations of "or" shrink to stay inside the diamond.
            orLabel.enableAutoSizing = true;
            orLabel.fontSizeMin = 8f;
            orLabel.fontSizeMax = 14f;
            orLabel.margin = new Vector4(5f, 0f, 5f, 0f);
            Localize(orLabel, "townOr");
            Ref(so, "orMark", or.gameObject);
        }

        static void BuildEnterColumn(RectTransform columns, SerializedObject so)
        {
            RectTransform column = Column(columns, "Enter Column", new RectOffset(29, 24, 20, 29), out CanvasGroup group);
            Ref(so, "enterColumn", group);
            ColumnHead(column, townIcon, "EnterTown", out TMP_Text subtitle);
            Localize(subtitle, "enterTownFlavor");

            Strip(column, out TMP_Text[] labels, out TMP_Text[] values, out Image[] icons,
                new[] { "townHeal", "townRecruits", "Cost" }, new[] { healIcon, helmIcon, goldIcon }, new[] { Positive, Sub, Coin });
            // TownPanel switches this label between Heal and Healed, so a localizer must not reset it.
            Object.DestroyImmediate(labels[0].GetComponent<LocalizeStringEvent>());
            Ref(so, "healLabel", labels[0]);
            Ref(so, "healValue", values[0]);
            Ref(so, "recruitsValue", values[1]);
            Ref(so, "costValue", values[2]);

            RectTransform lines = Rect("Body", column);
            VLayout(lines, 12f, new RectOffset());
            Ref(so, "healLine", Line(lines, "Heal Line", healIcon, Positive, Cream, 16f));
            Ref(so, "recruitLine", Line(lines, "Recruit Line", helmIcon, Sub, Cream, 16f));

            RectTransform recruitRow = Rect("Recruit Row", lines);
            HLayout(recruitRow, 16f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(recruitRow.gameObject, -1f, 45f);
            GameObject recruit = Instance(standardButton, recruitRow, "Recruit Units");
            Fixed(recruit, 250f, 45f);
            Localize(Child<TMP_Text>(recruit.transform, "Button Label"), "Recruit Units");
            TMP_Text cost = Text("Cost", recruitRow, displayDrop, 20f, Cream, "20");
            RectTransform done = Rect("Recruited", recruitRow);
            HLayout(done, 7f, TextAnchor.MiddleLeft, new RectOffset());
            Image dot = Img(Rect("Diamond", done), null, Taken);
            Fixed(dot.gameObject, 8f, 8f);
            dot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Localize(Text("Label", done, displayDrop, 15f, Taken, "Recruited"), "townRecruited");
            done.gameObject.SetActive(false);
            recruitRow.gameObject.SetActive(false);
            Ref(so, "recruitRow", recruitRow.gameObject);
            Ref(so, "recruitButton", recruit.GetComponent<Button>());
            Ref(so, "recruitCost", cost);
            Ref(so, "recruitDone", done.gameObject);

            TMP_Text note = Text("Note", lines, display, 15f, Cap, "Note");
            Wrap(note);
            Ref(so, "recruitNote", note);

            Spacer(column);
            RectTransform action = Action(column);
            GameObject enter = Instance(primaryButton, action, "Enter Town");
            Fixed(enter, 250f, 90f);
            Localize(Child<TMP_Text>(enter.transform, "Button Label"), "EnterTown");
            GameObject leave = Instance(primaryButton, action, "Continue");
            Fixed(leave, 250f, 90f);
            Localize(Child<TMP_Text>(leave.transform, "Button Label"), "continueButton");
            leave.SetActive(false);
            TMP_Text notTaken = NotTaken(action, "townSackedInstead");
            Ref(so, "enterButton", enter.GetComponent<Button>());
            Ref(so, "enterContinueButton", leave.GetComponent<Button>());
            Ref(so, "enterNotTaken", notTaken.gameObject);
        }

        static void BuildFightColumn(RectTransform columns, SerializedObject so)
        {
            RectTransform column = Column(columns, "Fight Column", new RectOffset(24, 29, 20, 29), out CanvasGroup group);
            Ref(so, "fightColumn", group);
            ColumnHead(column, swordIcon, "FightGarrison", out TMP_Text subtitle);
            subtitle.text = English("sackTownFlavor");
            Ref(so, "fightSubtitle", subtitle);

            Strip(column, out _, out TMP_Text[] values, out Image[] icons,
                new[] { "townBattlefield", "townWeather", "Bounty" }, new[] { gateIcon, clearIcon, goldIcon }, new[] { Sub, Sun, Coin });
            Ref(so, "battlefieldValue", values[0]);
            Ref(so, "weatherValue", values[1]);
            Ref(so, "weatherIcon", icons[1]);
            MemoriTooltipTrigger weatherTooltip = icons[1].transform.parent.parent.gameObject.AddComponent<MemoriTooltipTrigger>();
            Img((RectTransform)weatherTooltip.transform, null, Color.clear).raycastTarget = true;
            Ref(so, "weatherTooltip", weatherTooltip);
            Ref(so, "bountyValue", values[2]);

            RectTransform lines = Rect("Body", column);
            VLayout(lines, 12f, new RectOffset());

            RectTransform garrison = Rect("Garrison", lines);
            VLayout(garrison, 9f, new RectOffset());
            RectTransform garrisonHead = Rect("Garrison Head", garrison);
            HLayout(garrisonHead, 10f, TextAnchor.MiddleLeft, new RectOffset());
            Localize(CapLabel(garrisonHead, "Label"), "Garrison");
            TMP_Text count = Text("Count", garrisonHead, display, 15f, Cream, "9 squads");
            Spacer(garrisonHead, horizontal: true);
            TMP_Text hint = Text("Hint", garrisonHead, display, 14f, Cap, "Hint");
            Localize(hint, "townGarrisonHint");
            Ref(so, "garrisonCount", count);

            RectTransform tray = Rect("Tray", garrison);
            Img(tray, solid, Well);
            LayoutElement trayLayout = tray.gameObject.AddComponent<LayoutElement>();
            trayLayout.minHeight = trayLayout.preferredHeight = 130f * GarrisonScale + 16f;
            Image trayEdge = Img(Stretch(Rect("Edge", tray)), squareSliced, WellEdge, Image.Type.Sliced);
            trayEdge.fillCenter = false;
            RectTransform grid = Rect("Grid", tray);
            Centre(grid, 572f, 130f);
            grid.localScale = new Vector3(GarrisonScale, GarrisonScale, 1f);
            GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(60f, 130f);
            gridLayout.spacing = new Vector2(4f, 4f);
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 9;
            Ref(so, "garrisonBlock", garrison.gameObject);
            Ref(so, "garrisonTray", trayLayout);
            Ref(so, "garrisonGrid", grid);
            Ref(so, "garrisonGridLayout", gridLayout);

            RectTransform fightLines = Rect("Fight Lines", lines);
            VLayout(fightLines, 4f, new RectOffset());
            Localize(Line(fightLines, "Rewards Line", chestIcon, Positive, Positive, 16f), "townFightRewards");
            Localize(Line(fightLines, "Reserves Line", heartIcon, Negative, Negative, 16f), "sackTownNoHeal");
            Ref(so, "fightLines", fightLines.gameObject);

            RectTransform spoils = Rect("Spoils", lines);
            VLayout(spoils, 10f, new RectOffset());
            RectTransform spoilsHead = Rect("Spoils Head", spoils);
            HLayout(spoilsHead, 10f, TextAnchor.MiddleLeft, new RectOffset());
            Localize(CapLabel(spoilsHead, "Label"), "townSpoils");
            Spacer(spoilsHead, horizontal: true);
            Localize(Text("Note", spoilsHead, display, 14f, Cap, "Note"), "townSpoilsNote");
            Ref(so, "goldRow", SpoilRow(spoils, "Loot Gold", goldIcon));
            Ref(so, "gearRow", SpoilRow(spoils, "Loot Gear", chestIcon));
            Ref(so, "conscriptRow", SpoilRow(spoils, "Recruit Units", helmIcon));
            TMP_Text elite = Text("Elite Ravagers", spoils, display, 15f, Cream, "Elite Ravagers");
            Wrap(elite);
            elite.raycastTarget = true;
            Ref(so, "eliteRavagersText", elite);
            Ref(so, "eliteRavagersTooltip", elite.gameObject.AddComponent<MemoriTooltipTrigger>());
            elite.gameObject.SetActive(false);
            spoils.gameObject.SetActive(false);
            Ref(so, "spoilsBlock", spoils.gameObject);

            Spacer(column);
            RectTransform action = Action(column);
            GameObject fight = Instance(primaryButton, action, "Fight Garrison");
            Fixed(fight, 250f, 90f);
            Localize(Child<TMP_Text>(fight.transform, "Button Label"), "FightGarrison");
            RedButton(fight);
            GameObject leave = Instance(primaryButton, action, "Continue");
            Fixed(leave, 250f, 90f);
            Localize(Child<TMP_Text>(leave.transform, "Button Label"), "continueButton");
            leave.SetActive(false);
            TMP_Text notTaken = NotTaken(action, "townEnteredInstead");
            Ref(so, "fightButton", fight.GetComponent<Button>());
            Ref(so, "fightContinueButton", leave.GetComponent<Button>());
            Ref(so, "fightNotTaken", notTaken.gameObject);
        }
        #endregion

        #region Widgets
        static RectTransform Column(RectTransform parent, string name, RectOffset padding, out CanvasGroup group)
        {
            RectTransform column = Rect(name, parent);
            VerticalLayoutGroup layout = VLayout(column, 14f, padding);
            layout.childForceExpandWidth = true;
            // Fixed, because a text's preferred width ignores wrapping and would widen whichever column holds the longer line.
            Fixed(column.gameObject, ColumnWidth, -1f);
            group = column.gameObject.AddComponent<CanvasGroup>();
            return column;
        }

        static void ColumnHead(RectTransform column, Sprite icon, string titleKey, out TMP_Text subtitle)
        {
            RectTransform head = Rect("Head", column);
            HLayout(head, 12f, TextAnchor.MiddleLeft, new RectOffset());
            Mount(head, "Mount", 44f, icon, 20f);
            RectTransform titles = Rect("Titles", head);
            VLayout(titles, 1f, new RectOffset());
            Flexible(titles.gameObject, 1f).preferredWidth = 0f;
            TMP_Text title = Text("Title", titles, displayDrop, 23f, Gold, titleKey);
            Localize(title, titleKey);
            subtitle = Text("Subtitle", titles, display, 14f, Flavour, "Subtitle");
            subtitle.fontStyle = FontStyles.Italic;
            Wrap(subtitle);
        }

        static void Strip(RectTransform column, out TMP_Text[] labels, out TMP_Text[] values, out Image[] icons, string[] keys, Sprite[] sprites, Color[] tints)
        {
            RectTransform strip = Rect("Strip", column);
            Fixed(strip.gameObject, -1f, 62f);
            Image top = Img(Rect("Top", strip), solid, A(Brass, 0.4f));
            Anchor(top.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -1f), Vector2.zero);
            Ignore(top.gameObject);
            Image bottom = Img(Rect("Bottom", strip), solid, A(Brass, 0.4f));
            Anchor(bottom.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            Ignore(bottom.gameObject);
            HorizontalLayoutGroup row = HLayout(strip, 0f, TextAnchor.MiddleLeft, new RectOffset());
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            labels = new TMP_Text[keys.Length];
            values = new TMP_Text[keys.Length];
            icons = new Image[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                GameObject cell = Instance(statCellPart, strip, "Cell " + (i + 1));
                if (i == 0) cell.transform.Find("Divider").gameObject.SetActive(false);
                labels[i] = Child<TMP_Text>(cell.transform, "Label");
                values[i] = Child<TMP_Text>(cell.transform, "Value Row/Value");
                icons[i] = Child<Image>(cell.transform, "Value Row/Icon");
                Localize(labels[i], keys[i]);
                icons[i].sprite = sprites[i];
                icons[i].color = tints[i];
                // Gold values carry the coin sprite in their text, as everywhere else in the game.
                if (sprites[i] == goldIcon) icons[i].gameObject.SetActive(false);
            }
        }

        static TMP_Text Line(RectTransform parent, string name, Sprite icon, Color iconTint, Color textColour, float size)
        {
            RectTransform line = Rect(name, parent);
            HLayout(line, 9f, TextAnchor.MiddleLeft, new RectOffset());
            Image image = Img(Rect("Icon", line), icon, iconTint);
            image.preserveAspect = true;
            Fixed(image.gameObject, 17f, 17f);
            TMP_Text text = Text("Text", line, display, size, textColour, name);
            Wrap(text);
            Flexible(text.gameObject, 1f).preferredWidth = 0f;
            return text;
        }

        static TownSpoilRow SpoilRow(RectTransform parent, string name, Sprite icon)
        {
            GameObject row = Instance(spoilRowPart, parent, name);
            Image image = Child<Image>(row.transform, "Pop/Button/Content/Mount/Icon");
            image.sprite = icon;
            return row.GetComponent<TownSpoilRow>();
        }

        static TMP_Text CapLabel(RectTransform parent, string name)
        {
            TMP_Text label = Text(name, parent, display, 12f, Cap, name);
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = 10f;
            return label;
        }

        static RectTransform Action(RectTransform column)
        {
            RectTransform action = Rect("Action", column);
            Fixed(action.gameObject, -1f, 90f);
            HLayout(action, 0f, TextAnchor.MiddleCenter, new RectOffset());
            return action;
        }

        static TMP_Text NotTaken(RectTransform action, string key)
        {
            TMP_Text text = Text("Not Taken", action, display, 16f, Flavour, key);
            text.fontStyle = FontStyles.Italic;
            text.alignment = TextAlignmentOptions.Center;
            Localize(text, key);
            text.gameObject.SetActive(false);
            return text;
        }

        static void Spacer(RectTransform parent, bool horizontal = false)
        {
            LayoutElement spacer = Rect("Spacer", parent).gameObject.AddComponent<LayoutElement>();
            if (horizontal) spacer.flexibleWidth = 1f;
            else spacer.flexibleHeight = 1f;
        }

        // The engagement panel's Fight Battle recipe (ui-buttons.md): red in each role layer, alphas kept from Button Base.
        static void RedButton(GameObject button)
        {
            Rgb(Child<Image>(button.transform, "Background/UI Assets/SPR_Background"), FightFill);
            Rgb(Child<Image>(button.transform, "Background/UI Assets/Gradient"), FightHue);
            Rgb(Child<Image>(button.transform, "Background/UI Assets/Texture"), FightHue);
            Rgb(Child<Image>(button.transform, "Background/UI Assets/Highlight Image"), FightHover);
            Rgb(Child<Image>(button.transform, "Background/UI Assets/Selection Highlight"), FightHalo);
            Rgb(Child<Image>(button.transform, "Focus/Pointer"), FightPointer);
            Rgb(Child<Image>(button.transform, "Icon"), FightIcon);
            Child<TMP_Text>(button.transform, "Button Label").color = White;
        }

        static void Rgb(Image image, Color colour)
        {
            if (image != null) image.color = new Color(colour.r, colour.g, colour.b, image.color.a);
        }

        static GameObject Pill(RectTransform parent, string name, string key, Color colour)
        {
            GameObject pill = Instance(chip, parent, name);
            pill.GetComponent<CollectionChip>().Set(English(key), colour);
            TMP_Text label = Child<TMP_Text>(pill.transform, "Label");
            label.font = display;
            Localize(label, key);
            pill.SetActive(false);
            return pill;
        }

        static void Mount(RectTransform parent, string name, float size, Sprite icon, float iconSize)
        {
            RectTransform cell = Rect(name, parent);
            Fixed(cell.gameObject, size, size);
            // The mount art has transparent margins, so it is drawn a little larger than its cell, as in the tooltip.
            Image diamond = Img(Rect("Diamond", cell), mount, Color.white);
            Centre(diamond.rectTransform, size * 52f / 44f, size * 52f / 44f);
            Image image = Img(Rect("Icon", cell), icon, Gold);
            image.preserveAspect = true;
            Centre(image.rectTransform, iconSize, iconSize);
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
            if (component == null) Debug.LogError($"TownPanelBuilder: {root.name} has no {typeof(T).Name} at '{path}'.");
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
            if (property == null) { Debug.LogError($"TownPanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.objectReferenceValue = value;
        }

        static void Refs(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"TownPanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        #endregion
    }
}
