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

namespace TJ.EndBattle.EditorTools
{
    /// <summary>
    /// Generates the end-of-battle card (End Battle Panel UI.prefab) and its army bar badge part on the game's Basic
    /// Background, and installs the card in TavernBattle.unity in place of the old banner.
    /// </summary>
    public static class EndBattlePanelBuilder
    {
        public const string PartFolder = "Assets/Data/Prefabs/UI/Battle/End Battle";
        public const string PanelPath = PartFolder + "/End Battle Panel UI.prefab";
        const string BadgePath = PartFolder + "/End Battle Squad Badge.prefab";
        const string StatPlaquePath = "Assets/Data/Prefabs/UI/Reuseable/Ornaments/Stat Plaque.prefab";
        const string ScenePath = "Assets/Scenes/TavernBattle.unity";
        const string OldPanelName = "End Battle Panel";
        const string ButtonFolder = "Assets/Data/Prefabs/UI/Reuseable/Buttons";
        const string BasicBackgroundPath = "Assets/Data/Prefabs/UI/Reuseable/Basic Background.prefab";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";
        const string TableName = "MainLocalizationTable";
        const string Hud = "Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/";

        const float PanelWidth = 860f;
        // The card's top edge sits 96 below the top of the screen, over the battlefield and clear of the army bar.
        const float PanelTopY = -96f;
        const float BandAlpha = 0.12f;
        const float PanelPadding = 5f;
        const float HeaderHeight = 96f;
        const float TextureTop = 107f;
        const float TexturePixelsPerUnit = 12f;
        const float StatsButtonWidth = 220f;
        const float StatsButtonHeight = 44f;
        // The stock button size: its label sits top-left for the icon on the right, which reads high on a shorter button.
        const float ButtonWidth = 250f;
        const float ButtonHeight = 90f;
        // Army bar cards are 60 x 130; the tabs sit over the card's top edge.
        const float TabWidth = 60f;
        const float TabHeight = 21f;
        const float TabGap = 3f;
        const float TabLift = 4f;

        #region Style
        static readonly Color Well = Hex("162023");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        static readonly Color Sub = Hex("B4AA94");
        static readonly Color Blue = Hex("9ED8FF");
        static readonly Color DefeatLine = Hex("E8B0A8");
        static readonly Color LossFill = Hex("4A1F22");
        static readonly Color LossEdge = Hex("8E3A3E");
        static readonly Color LossText = Hex("FFDCD8");
        static readonly Color WellEdge = Hex("605635", 0.9f);
        #endregion

        static TMP_FontAsset displayDrop, display;
        static Sprite mount, solid, frameEdge, bandGradient;
        static Sprite swordsIcon, skullIcon, heartIcon, scrollIcon;
        static GameObject standardButton, primaryButton, backButton, basicBackground, statPlaque, haloFlare;

        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            var sheet = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite) sheet[sprite.name] = sprite;
            sheet.TryGetValue("TooltipMount", out mount);
            sheet.TryGetValue("TooltipSolid", out solid);
            if (mount == null || solid == null) Debug.LogError("EndBattlePanelBuilder: tooltip sheet sprites missing.");
            frameEdge = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Frame_Box_Small01.png");
            bandGradient = Load<Sprite>(Hud + "HUD/SPR_HUD_FantasyWarrior_Gradient_Vertical_Smooth01.png");
            swordsIcon = Load<Sprite>(Hud + "Icons_Status/ICON_FantasyWarrior_Status_Attack02_Clean.png");
            skullIcon = Load<Sprite>(Hud + "Icons_Map/ICON_FantasyWarrior_Map_Skull01_Clean.png");
            heartIcon = Load<Sprite>("Assets/Art/Icons/Stats/Health.png");
            scrollIcon = Load<Sprite>("Assets/Art/Icons/Achievements/Baked/Scroll.png");
            standardButton = Load<GameObject>(ButtonFolder + "/Button - Standard.prefab");
            primaryButton = Load<GameObject>(ButtonFolder + "/Button - Primary.prefab");
            backButton = Load<GameObject>(ButtonFolder + "/Button - Back.prefab");
            basicBackground = Load<GameObject>(BasicBackgroundPath);
            statPlaque = Load<GameObject>(StatPlaquePath);
            haloFlare = Load<GameObject>("Assets/Data/Prefabs/UI/Reuseable/Ornaments/Halo Flare.prefab");
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"EndBattlePanelBuilder: missing {typeof(T).Name} at {path}");
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

        #region Localization
        // English source text for the keys this panel adds. Other locales are filled in separately.
        public static readonly (string key, string english)[] Keys =
        {
            ("endBattleMostSlain", "Most slain"),
        };

        static StringTableCollection collection;
        static StringTable english;

        static void LoadTable()
        {
            collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            english = (StringTable)collection.GetTable("en");
        }

        [MenuItem("Tabletop Tavern/End Battle Panel/Add Text Keys")]
        public static void AddKeysMenu() => Debug.Log("EndBattlePanelBuilder: " + EnsureKeys());

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
            if (entry == null) throw new InvalidOperationException($"EndBattlePanelBuilder: no localization key '{key}'.");
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
        /// <summary>Adds missing keys, creates the badge part if missing, then rebuilds the panel.</summary>
        [MenuItem("Tabletop Tavern/End Battle Panel/Rebuild Prefab")]
        public static void BuildPrefab()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            EnsureParts(false);
            BuildPanel();
        }

        [MenuItem("Tabletop Tavern/End Battle Panel/Reset Part Prefabs")]
        static void ResetPartsMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset end battle parts",
                    $"The part prefabs in {PartFolder} are rebuilt from code, which discards your edits to them. The panel is rebuilt after.",
                    "Reset", "Cancel"))
                return;
            ResetParts();
        }

        public static void ResetParts()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            EnsureParts(true);
            BuildPanel();
        }

        [MenuItem("Tabletop Tavern/End Battle Panel/Install In Battle")]
        public static void InstallMenu() => Debug.Log("EndBattlePanelBuilder: " + InstallInBattle());

        static void BuildPanel()
        {
            // Rebuild inside the existing prefab so the root keeps its id; the TavernBattle instance references it.
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath) != null;
            Scene stage = default;
            GameObject root;
            if (existing) root = PrefabUtility.LoadPrefabContents(PanelPath);
            else
            {
                // A first build happens in a preview scene so no open scene is marked changed.
                stage = EditorSceneManager.NewPreviewScene();
                root = new GameObject("End Battle Panel UI", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
            }
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root);
                Normalize(root);
                RecordOverrides(root);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                Debug.Log($"EndBattlePanelBuilder: wrote {PanelPath}");
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
        /// Replaces TavernBattle.unity's old End Battle Panel with the prefab at the same spot under Main Canvas, points
        /// UIManager at the view and saves only TavernBattle.unity. Stops if anything else points into the old panel.
        /// </summary>
        public static string InstallInBattle()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (prefab == null) return $"no prefab at {PanelPath}; run Rebuild Prefab first.";
            Scene battle = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!battle.isLoaded)
            {
                battle = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            try
            {
                if (battle.isDirty && !opened) return "TavernBattle.unity has unsaved changes; save or revert them first.";
                UIManager ui = Find<UIManager>(battle);
                if (ui == null) return "no UIManager in TavernBattle.unity.";
                Transform canvas = FindDeep(ui.transform.root, "Main Canvas");
                foreach (GameObject root in battle.GetRootGameObjects())
                    if (canvas == null) canvas = FindDeep(root.transform, "Main Canvas");
                if (canvas == null) return "no Main Canvas in TavernBattle.unity.";

                Transform old = canvas.Find(OldPanelName);
                bool hadOld = old != null;
                int index = hadOld ? old.GetSiblingIndex() : canvas.childCount;
                if (hadOld)
                {
                    string outside = CheckReferences(battle, old, ui);
                    if (!string.IsNullOrEmpty(outside)) return "stopped, outside references into the old panel:\n" + outside;
                    Object.DestroyImmediate(old.gameObject);
                }
                for (int i = canvas.childCount - 1; i >= 0; i--)
                {
                    GameObject child = canvas.GetChild(i).gameObject;
                    if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(child) == prefab)
                    {
                        index = Mathf.Min(index, i);
                        Object.DestroyImmediate(child);
                    }
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
                instance.transform.SetSiblingIndex(Mathf.Min(index, canvas.childCount - 1));
                var so = new SerializedObject(ui);
                Ref(so, "endBattleView", instance.GetComponent<EndBattlePanelView>());
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(battle);
                if (!EditorSceneManager.SaveScene(battle)) return "TavernBattle.unity did not save.";
                return "installed" + (hadOld ? ", old End Battle Panel removed." : ", no old panel found.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(battle, true);
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

        // Anything other than UIManager that points into the old panel would lose its target when it is deleted.
        static string CheckReferences(Scene scene, Transform old, UIManager ui)
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
                        if (component == null || component == ui || component is Transform) continue;
                        var so = new SerializedObject(component);
                        SerializedProperty property = so.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
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
        static GameObject badgePart;

        static void EnsureParts(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/Prefabs/UI/Battle")) AssetDatabase.CreateFolder("Assets/Data/Prefabs/UI", "Battle");
            if (!AssetDatabase.IsValidFolder(PartFolder)) AssetDatabase.CreateFolder("Assets/Data/Prefabs/UI/Battle", "End Battle");
            badgePart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(BadgePath);
            if (badgePart == null) badgePart = SavePart(BadgePath, "End Battle Squad Badge", BadgePart);
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
                Debug.Log($"EndBattlePanelBuilder: wrote {path}");
                return asset;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        // Covers the card it sits on: the kills tab rests on the card's top edge, the loss tab above it, the caption above both.
        static void BadgePart(GameObject go)
        {
            var root = (RectTransform)go.transform;
            Stretch(root);
            Ignore(go);
            EndBattleSquadBadge badge = go.AddComponent<EndBattleSquadBadge>();
            var so = new SerializedObject(badge);

            Image frame = Img(Stretch(Rect("Most Slain Frame", root), -4f, -4f, -4f, -4f), frameEdge, Gold, Image.Type.Sliced);
            frame.fillCenter = false;
            frame.pixelsPerUnitMultiplier = 4f;
            frame.gameObject.SetActive(false);
            Ref(so, "mostSlainFrame", frame.gameObject);

            RectTransform kills = Tab(root, "Kills Tab", TabLift, Well, WellEdge, out Image killsEdge);
            HorizontalLayoutGroup killsRow = HLayout(kills, 4f, TextAnchor.MiddleCenter, new RectOffset(4, 4, 0, 0));
            killsRow.childForceExpandWidth = false;
            Image skull = Img(Rect("Icon", kills), skullIcon, Cream);
            skull.preserveAspect = true;
            Fixed(skull.gameObject, 13f, 13f);
            TMP_Text killsText = Text("Kills", kills, displayDrop, 13f, Cream, "22");
            killsText.alignment = TextAlignmentOptions.Center;
            Ref(so, "killsText", killsText);
            Ref(so, "killsIcon", skull);
            Ref(so, "killsEdge", killsEdge);

            RectTransform lost = Tab(root, "Lost Tab", TabLift + TabHeight + TabGap, LossFill, LossEdge, out _);
            TMP_Text lostText = Text("Lost", lost, displayDrop, 13f, LossText, "-14");
            Stretch(lostText.rectTransform);
            lostText.alignment = TextAlignmentOptions.Center;
            Ref(so, "lostTab", lost.gameObject);
            Ref(so, "lostText", lostText);

            TMP_Text caption = Text("Most Slain Caption", root, displayDrop, 14f, Gold, "Most slain");
            caption.alignment = TextAlignmentOptions.Center;
            RectTransform captionRect = caption.rectTransform;
            captionRect.anchorMin = captionRect.anchorMax = new Vector2(0.5f, 1f);
            captionRect.pivot = new Vector2(0.5f, 0f);
            captionRect.sizeDelta = new Vector2(124f, 16f);
            captionRect.anchoredPosition = new Vector2(0f, TabLift + 2f * TabHeight + 2f * TabGap);
            Unlocalize(caption, "Most slain");
            Shrink(caption, 8f);
            caption.gameObject.SetActive(false);
            Ref(so, "mostSlainCaption", caption);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // One tab over the card's top edge: a fill with a one-pixel sliced edge.
        static RectTransform Tab(RectTransform parent, string name, float lift, Color fill, Color edgeColour, out Image edge)
        {
            RectTransform tab = Rect(name, parent);
            tab.anchorMin = tab.anchorMax = new Vector2(0.5f, 1f);
            tab.pivot = new Vector2(0.5f, 0f);
            tab.sizeDelta = new Vector2(TabWidth, TabHeight);
            tab.anchoredPosition = new Vector2(0f, lift);
            Img(tab, solid, fill);
            edge = Img(Stretch(Rect("Edge", tab)), frameEdge, edgeColour, Image.Type.Sliced);
            edge.fillCenter = false;
            edge.pixelsPerUnitMultiplier = 4f;
            Ignore(edge.gameObject);
            return tab;
        }
        #endregion

        #region Layout
        static void Build(GameObject rootGo)
        {
            rootGo.layer = 5;
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(PanelWidth, 300f);
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
            CanvasGroup group = GetOrAdd<CanvasGroup>(rootGo);
            EndBattlePanelView view = GetOrAdd<EndBattlePanelView>(rootGo);
            var so = new SerializedObject(view);
            Ref(so, "card", root);
            Ref(so, "cardGroup", group);

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

            // A wash inside the background, under its frame and corner ornaments, falling from the top edge to the header's foot.
            Image band = Img(Rect("Band", background.transform), bandGradient, A(Gold, BandAlpha));
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
            BuildBody(root, so);

            Ref(so, "victoryIcon", swordsIcon);
            Ref(so, "defeatIcon", skullIcon);
            Ref(so, "damageIcon", swordsIcon);
            Ref(so, "badgePrefab", badgePart.GetComponent<EndBattleSquadBadge>());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildHeader(RectTransform root, SerializedObject so)
        {
            RectTransform header = Rect("Header", root);
            Fixed(header.gameObject, -1f, HeaderHeight);
            HLayout(header, 16f, TextAnchor.MiddleLeft, new RectOffset(22, 24, 0, 0));

            RectTransform mountCell = Mount(header, "Mount", 64f, swordsIcon, 30f);
            Ref(so, "headerIcon", Child<Image>(mountCell, "Icon"));
            // A round glow under the crest's diamond, so a victory blooms behind it (RunSetupBuilder.AddHalo).
            GameObject halo = Instance(haloFlare, mountCell, "Halo Flare");
            halo.transform.SetAsFirstSibling();
            RectTransform haloRect = (RectTransform)halo.transform;
            haloRect.anchorMin = haloRect.anchorMax = new Vector2(0.5f, 0.5f);
            haloRect.anchoredPosition = Vector2.zero;
            haloRect.sizeDelta = new Vector2(76f, 76f);
            Ignore(halo);
            // By name: this assembly does not reference Memori.UI.
            Ref(so, "victoryHalo", halo.GetComponent("UIFlare"));
            RectTransform names = Rect("Names", header);
            VLayout(names, 2f, new RectOffset());
            Flexible(names.gameObject, 1f).preferredWidth = 0f;
            TMP_Text title = Text("Title", names, displayDrop, 36f, Gold, "Victory");
            Unlocalize(title, "Victory");
            Shrink(title, 24f);
            Ref(so, "title", title);

            RectTransform context = Rect("Context", header);
            VerticalLayoutGroup contextLayout = VLayout(context, 4f, new RectOffset());
            contextLayout.childAlignment = TextAnchor.MiddleRight;
            Fixed(context.gameObject, 260f, -1f);
            TMP_Text caption = Text("Caption", context, display, 15f, Sub, "Act III");
            caption.alignment = TextAlignmentOptions.MidlineRight;
            Unlocalize(caption, "Act III");
            Shrink(caption, 12f);
            TMP_Text value = Text("Value", context, displayDrop, 17f, Cream, "Taelindor Forest");
            value.alignment = TextAlignmentOptions.MidlineRight;
            Unlocalize(value, "Taelindor Forest");
            Shrink(value, 12f);
            Ref(so, "contextCaption", caption);
            Ref(so, "contextValue", value);
        }

        static void BuildBody(RectTransform root, SerializedObject so)
        {
            RectTransform body = Rect("Body", root);
            VLayout(body, 12f, new RectOffset(24, 24, 14, 18));

            RectTransform strip = Rect("Strip", body);
            Fixed(strip.gameObject, -1f, 62f);
            HLayout(strip, 10f, TextAnchor.MiddleLeft, new RectOffset());
            // Load has already logged a missing plaque with its path; the strip then holds only Detailed stats.
            if (statPlaque != null)
            {
                Ref(so, "slainValue", StatPlaque(strip, "Slain", "engagementCellSlain", skullIcon));
                Ref(so, "lossesValue", StatPlaque(strip, "Losses", "engagementCellLosses", heartIcon));
            }

            RectTransform slot = Rect("Detailed Stats Slot", strip);
            HLayout(slot, 0f, TextAnchor.MiddleCenter, new RectOffset(12, 0, 0, 0));
            Fixed(slot.gameObject, StatsButtonWidth + 12f, -1f);
            GameObject stats = Instance(standardButton, slot, "Detailed Stats");
            Fixed(stats, StatsButtonWidth, StatsButtonHeight);
            TMP_Text statsLabel = Child<TMP_Text>(stats.transform, "Button Label");
            Localize(statsLabel, "engagementDetailedStats");
            statsLabel.alignment = TextAlignmentOptions.Center;
            statsLabel.fontSize = 17f;
            statsLabel.enableAutoSizing = true;
            statsLabel.fontSizeMin = 12f;
            statsLabel.fontSizeMax = 17f;
            statsLabel.textWrappingMode = TextWrappingModes.NoWrap;
            statsLabel.margin = new Vector4(40f, 0f, 12f, 0f);
            Image statsIcon = Child<Image>(stats.transform, "Icon");
            if (statsIcon != null)
            {
                statsIcon.gameObject.SetActive(true);
                statsIcon.sprite = scrollIcon;
                statsIcon.color = Blue;
                statsIcon.preserveAspect = true;
                RectTransform iconRect = statsIcon.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(20f, 20f);
                iconRect.anchoredPosition = new Vector2(26f, 0f);
            }
            // A blue ring round the button while its panel is open.
            Image ring = Img(Stretch(Rect("Open Ring", stats.transform), -3f, -3f, -3f, -3f), frameEdge, Blue, Image.Type.Sliced);
            ring.fillCenter = false;
            ring.pixelsPerUnitMultiplier = 4f;
            Ignore(ring.gameObject);
            ring.gameObject.SetActive(false);
            Ref(so, "detailedStatsButton", stats.GetComponent<Button>());
            Ref(so, "detailedStatsOpen", ring.gameObject);

            TMP_Text defeat = Text("Defeat Line", body, display, 16f, DefeatLine, "Your warband is broken. This run ends here.");
            defeat.fontStyle = FontStyles.Italic;
            defeat.alignment = TextAlignmentOptions.Center;
            Localize(defeat, "engagementDefeatLine");
            defeat.gameObject.SetActive(false);
            Ref(so, "defeatLine", defeat);

            RectTransform actions = Rect("Actions", body);
            HLayout(actions, 12f, TextAnchor.MiddleCenter, new RectOffset());
            Fixed(actions.gameObject, -1f, ButtonHeight);
            GameObject exit = Instance(backButton, actions, "Exit To Menu");
            Fixed(exit, ButtonWidth, ButtonHeight);
            Localize(Child<TMP_Text>(exit.transform, "Button Label"), "exitToMenuButton");
            exit.SetActive(false);
            GameObject continueButton = Instance(primaryButton, actions, "Continue");
            Fixed(continueButton, ButtonWidth, ButtonHeight);
            Localize(Child<TMP_Text>(continueButton.transform, "Button Label"), "continueButton");
            GameObject rematch = Instance(primaryButton, actions, "Rematch");
            Fixed(rematch, ButtonWidth, ButtonHeight);
            Localize(Child<TMP_Text>(rematch.transform, "Button Label"), "Rematch");
            rematch.SetActive(false);
            Ref(so, "exitButton", exit.GetComponent<Button>());
            Ref(so, "continueButton", continueButton.GetComponent<Button>());
            Ref(so, "rematchButton", rematch.GetComponent<Button>());
        }

        // One shared Stat Plaque; its art, fonts and sizes stay the prefab's. The value is filled at runtime.
        static TMP_Text StatPlaque(RectTransform strip, string name, string labelKey, Sprite icon)
        {
            GameObject plaque = Instance(statPlaque, strip, name);
            Image image = Child<Image>(plaque.transform, "Icon");
            image.sprite = icon;
            image.color = Sub;
            Localize(Child<TMP_Text>(plaque.transform, "Label"), labelKey);
            TMP_Text value = Child<TMP_Text>(plaque.transform, "Value");
            Unlocalize(value, "0");
            value.richText = true;
            return value;
        }
        #endregion

        #region Widgets
        // Text shrinks to fit its line in every locale.
        static void Shrink(TMP_Text label, float min)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = label.fontSize;
            label.overflowMode = TextOverflowModes.Ellipsis;
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
            if (component == null) Debug.LogError($"EndBattlePanelBuilder: {root.name} has no {typeof(T).Name} at '{path}'.");
            return component;
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
            if (property == null) { Debug.LogError($"EndBattlePanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.objectReferenceValue = value;
        }
        #endregion
    }
}
