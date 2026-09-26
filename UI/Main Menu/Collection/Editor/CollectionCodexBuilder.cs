using System.Collections.Generic;
using System.Linq;
using Memori.Audio;
using Memori.Tooltip;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TJ.MainMenu.EditorTools
{
    /// <summary>
    /// Generates the Collection's component prefabs and Collection Codex.prefab (Ink and Brass, the tooltip look) and
    /// installs the codex in Collection.unity. Rebuilding the codex keeps hand edits to the component prefabs.
    /// </summary>
    public static class CollectionCodexBuilder
    {
        public const string PrefabPath = "Assets/Data/Prefabs/UI/Menu/Collection Codex.prefab";
        public const string PartFolder = "Assets/Data/Prefabs/UI/Collection";
        public const string ScenePath = "Assets/Scenes/Collection.unity";
        public const string RenderTexturePath = "Assets/Data/Render Textures/CollectionUnitPreviewTexture.renderTexture";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";

        // The stage is wide, so the render texture matches its shape instead of the old square.
        const int StageTextureWidth = 1470;
        const int StageTextureHeight = 840;
        const float StageAspect = StageTextureWidth / (float)StageTextureHeight;
        const float PreviewFieldOfView = 30f;
        // Height of the shared baseline above the page header's rule. Every text in that row sits on it.
        const float HeaderBaseline = 22f;

        #region Style
        static readonly Color Ground = Hex("121B1D");
        static readonly Color HeaderFill = Hex("0C1315");
        static readonly Color Slate = Hex("1F2B2E");
        static readonly Color TileFill = Hex("1B272A");
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        static readonly Color Parchment = Hex("D9D2C2");
        static readonly Color Flavour = Hex("A99F8A");
        static readonly Color Sub = Hex("B4AA94");
        static readonly Color Label = Hex("8E8672");
        static readonly Color Muted = Hex("8E9A9A");
        static readonly Color Stat = Hex("E3BB71");
        static readonly Color Line = Hex("2C3A3D");

        static TMP_FontAsset displayDrop, display, body;
        static Sprite panel, shadow, keyCap, fadeRule, roundedFill, roundedOutline, edgeFade, lockIcon, arrow, gearIcon, potionIcon;

        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            body = Load<TMP_FontAsset>("Assets/Synty/InterfaceFantasyMenus/Fonts/Alegreya Sans/AlegreyaSans-Medium SDF.asset");
            var sheet = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite) sheet[sprite.name] = sprite;
            sheet.TryGetValue("TooltipPanel", out panel);
            sheet.TryGetValue("TooltipShadow", out shadow);
            sheet.TryGetValue("TooltipKeyCap", out keyCap);
            sheet.TryGetValue("TooltipFadeRule", out fadeRule);
            if (panel == null || shadow == null || keyCap == null || fadeRule == null) Debug.LogError("CollectionCodexBuilder: tooltip sheet sprites missing.");
            roundedFill = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Border/Rounded/256px/Rounded Filled 256px.png");
            roundedOutline = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Border/Rounded/256px/Rounded Outline 256px - 2x.png");
            edgeFade = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Shadow/Vertical Shadow.png");
            lockIcon = Load<Sprite>("Assets/Art/Icons/UI/LockClosedGold.png");
            arrow = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Arrow02.png");
            gearIcon = Load<Sprite>("Assets/Art/Icons/UnitTypes/Melee.png");
            potionIcon = Load<Sprite>("Assets/Art/Icons/Consumables/MajorHealth.png");
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"CollectionCodexBuilder: missing {typeof(T).Name} at {path}");
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

        #region Entry points
        /// <summary>Creates any missing component prefab, then rebuilds the codex from them.</summary>
        [MenuItem("Tabletop Tavern/Collection/Rebuild Prefab")]
        public static void BuildPrefab()
        {
            LoadAssets();
            EnsureParts(false);
            BuildCodex();
        }

        [MenuItem("Tabletop Tavern/Collection/Reset Component Prefabs")]
        static void ResetPartsMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset Collection components",
                    $"Every prefab in {PartFolder} is rebuilt from code, which discards your edits to them. The codex is rebuilt after.",
                    "Reset", "Cancel"))
                return;
            ResetParts();
        }

        public static void ResetParts()
        {
            LoadAssets();
            EnsureParts(true);
            BuildCodex();
        }

        static void BuildCodex()
        {
            RenderTexture texture = PrepareRenderTexture();

            // Rebuild inside the existing prefab so the root keeps its id; the scene instance references it.
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Collection Panel", typeof(RectTransform));
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root, texture);
                Normalize(root);
                RecordOverrides(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"CollectionCodexBuilder: wrote {PrefabPath}");
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Replaces the old hand-built panel in Collection.unity with the prefab, wires the preview rig and the
        /// scene manager, and saves only that scene.
        /// </summary>
        [MenuItem("Tabletop Tavern/Collection/Install In Scene")]
        public static void InstallInScene()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("CollectionCodexBuilder: build the prefab first."); return; }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                GameObject canvas = roots.First(r => r.name == "Canvas");
                GameObject holder = roots.First(r => r.name == "Prefab Holder");
                CollectionSceneManager manager = roots.Select(r => r.GetComponent<CollectionSceneManager>()).First(m => m != null);

                for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = canvas.transform.GetChild(i).gameObject;
                    if (child.name == "Collection Panel" && PrefabUtility.GetCorrespondingObjectFromSource(child) != prefab)
                        Object.DestroyImmediate(child);
                }

                CollectionPanel panel = canvas.GetComponentInChildren<CollectionPanel>(true);
                if (panel == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                    instance.name = "Collection Panel";
                    Stretch((RectTransform)instance.transform);
                    instance.transform.SetAsFirstSibling();
                    panel = instance.GetComponent<CollectionPanel>();
                }

                CollectionPreviewRig rig = GetOrAdd<CollectionPreviewRig>(holder);
                Camera camera = holder.transform.Find("Unit Preview Camera").GetComponent<Camera>();
                camera.fieldOfView = PreviewFieldOfView;
                var rigObject = new SerializedObject(rig);
                Ref(rigObject, "modelHolder", holder.transform.Find("Collection Unit"));
                Ref(rigObject, "previewCamera", camera);
                Ref(rigObject, "lights", holder.transform.Find("Lights").gameObject);
                Ref(rigObject, "dropInFeedback", holder.GetComponent("MMF_Player"));
                Ref(rigObject, "undiscoveredMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Unknown.mat"));
                rigObject.ApplyModifiedPropertiesWithoutUndo();

                var managerObject = new SerializedObject(manager);
                Ref(managerObject, "collectionPanel", panel);
                Ref(managerObject, "previewRig", rig);
                managerObject.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("CollectionCodexBuilder: installed the codex in Collection.unity");
            }
            finally
            {
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static RenderTexture PrepareRenderTexture()
        {
            RenderTexture texture = Load<RenderTexture>(RenderTexturePath);
            if (texture == null) return null;
            if (texture.width != StageTextureWidth || texture.height != StageTextureHeight || texture.graphicsFormat != GraphicsFormat.R16G16B16A16_SFloat)
            {
                texture.Release();
                texture.width = StageTextureWidth;
                texture.height = StageTextureHeight;
                texture.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                EditorUtility.SetDirty(texture);
                AssetDatabase.SaveAssetIfDirty(texture);
            }
            return texture;
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

        #region Component prefabs
        static readonly Dictionary<string, GameObject> parts = new();

        // Built in this order, so a part can nest the parts above it. A part with a base is saved as a variant of it.
        static readonly (string Name, string Base, System.Action<GameObject> Build)[] PartList =
        {
            ("Section Label", null, SectionLabelPart),
            ("Rule", null, RulePart),
            ("Chip", null, ChipPart),
            ("Chip Row", null, ChipRowPart),
            ("Stat Row", null, StatRowPart),
            ("Stat Strip Cell", null, StripCellPart),
            ("Stat Strip", null, StripPart),
            ("Spell Row", null, SpellRowPart),
            ("Effect", null, EffectPart),
            ("Mini Unit", null, MiniUnitPart),
            ("Commander Row", null, CommanderRowPart),
            ("Tile Base", null, TileBasePart),
            ("Item Tile", "Tile Base", ItemTilePart),
            ("Unit Tile", "Tile Base", UnitTilePart),
            ("Rail Section", null, RailSectionPart),
            ("Rail Row", null, RailRowPart),
            ("Group Header", null, GroupHeaderPart),
            ("Group Grid", null, GroupGridPart),
            ("Page Tab", null, PageTabPart),
            ("Hero Tab", null, HeroTabPart),
            ("Header Effect", null, HeaderEffectPart),
            ("Turn Button", null, TurnButtonPart),
            ("Scroll View", null, ScrollPart),
            ("Close Button", null, CloseButtonPart),
        };

        static string PartPath(string name) => $"{PartFolder}/{name}.prefab";

        const string HoverSoundPath = "Assets/Scripts/Memori.Audio/SOs/Button Hover - SFXReference.asset";

        // The same hover sound Button Base plays, so every row, tile and tab sounds alike.
        static void HoverSound(GameObject go, Selectable interactable)
        {
            var so = new SerializedObject(go.AddComponent<UIHoverSFX>());
            Ref(so, "sfxReference", Load<SFXReference>(HoverSoundPath));
            Ref(so, "interactableSource", interactable);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureParts(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(PartFolder)) AssetDatabase.CreateFolder("Assets/Data/Prefabs/UI", "Collection");
            parts.Clear();
            foreach ((string name, string basePart, System.Action<GameObject> build) in PartList)
            {
                GameObject asset = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(PartPath(name));
                parts[name] = asset != null ? asset : SavePart(name, basePart, build);
            }
        }

        static GameObject SavePart(string name, string basePart, System.Action<GameObject> build)
        {
            // Built in a preview scene so the open scenes are never touched or marked changed.
            Scene stage = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                if (basePart == null)
                {
                    root = new GameObject(name, typeof(RectTransform));
                    SceneManager.MoveGameObjectToScene(root, stage);
                }
                else root = (GameObject)PrefabUtility.InstantiatePrefab(parts[basePart], stage);
                root.name = name;
                root.layer = 5;
                build(root);
                Normalize(root);
                RecordOverrides(root);
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, PartPath(name));
                Debug.Log($"CollectionCodexBuilder: wrote {PartPath(name)}");
                return asset;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        static T PartAsset<T>(string name) where T : Component
        {
            T component = parts[name].GetComponent<T>();
            if (component == null) Debug.LogError($"CollectionCodexBuilder: the {name} prefab has no {typeof(T).Name} on its root.");
            return component;
        }

        static T Part<T>(string name, Transform parent, string instanceName = null) where T : Component
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(parts[name], parent);
            if (instanceName != null) instance.name = instanceName;
            T component = instance.GetComponent<T>();
            if (component == null) Debug.LogError($"CollectionCodexBuilder: the {name} prefab has no {typeof(T).Name} on its root.");
            return component;
        }

        static T Child<T>(Transform root, string path) where T : Component
        {
            Transform child = root.Find(path);
            T component = child != null ? child.GetComponent<T>() : null;
            if (component == null) Debug.LogError($"CollectionCodexBuilder: {root.name} has no {typeof(T).Name} at '{path}'.");
            return component;
        }

        static void SectionLabelPart(GameObject go)
        {
            TMP_Text label = TextOn(go, body, 12.5f, Label, "Section");
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = 10f;
        }

        static void RulePart(GameObject go)
        {
            Img((RectTransform)go.transform, null, A(Brass, 0.3f));
            Fixed(go, -1f, 1f);
        }

        static void ChipPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Image background = Img(Stretch(Rect("Background", rect)), roundedFill, Color.white, Image.Type.Sliced, 76f / 4f);
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image border = Img(Stretch(Rect("Border", rect)), roundedOutline, Color.white, Image.Type.Sliced, 76f / 4f);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HorizontalLayoutGroup row = HLayout(rect, 0f, TextAnchor.MiddleCenter, new RectOffset(10, 10, 3, 4));
            row.childForceExpandWidth = false;
            TMP_Text label = Text("Label", rect, body, 14f, Cream, "Chip");
            label.fontStyle = FontStyles.Bold;
            MemoriTooltipTrigger tooltip = go.AddComponent<MemoriTooltipTrigger>();
            CollectionChip chip = go.AddComponent<CollectionChip>();
            var so = new SerializedObject(chip);
            Ref(so, "background", background);
            Ref(so, "border", border);
            Ref(so, "label", label);
            Ref(so, "tooltip", tooltip);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ChipRowPart(GameObject go)
        {
            // The detail panel also reads this spacing when it wraps chips onto a new row.
            HLayout(go.transform, 8f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandWidth = false;
        }

        static void StatRowPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Fixed(go, -1f, 26f);
            Img(Stretch(Rect("Hit", rect)), null, Color.clear).raycastTarget = true;
            rect.GetChild(0).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HorizontalLayoutGroup row = HLayout(rect, 10f, TextAnchor.MiddleLeft, new RectOffset());
            row.childForceExpandWidth = false;
            Image icon = Img(Rect("Icon", rect), null, Color.white);
            icon.preserveAspect = true;
            Fixed(icon.gameObject, 20f, 20f);
            TMP_Text name = Text("Name", rect, body, 16f, Parchment, "Stat");
            name.overflowMode = TextOverflowModes.Ellipsis;
            Flexible(name.gameObject, 1f);
            TMP_Text value = Text("Value", rect, body, 16f, Cream, "0");
            value.fontStyle = FontStyles.Bold;
            value.alignment = TextAlignmentOptions.MidlineRight;
            Fixed(value.gameObject, 44f, 24f);
            RectTransform bar = Rect("Bar", rect);
            Fixed(bar.gameObject, 110f, 5f);
            Img(Stretch(Rect("Track", bar)), null, Hex("2A3739"));
            RectTransform fill = Rect("Fill", bar);
            Anchor(fill, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            Img(fill, null, Stat);
            MemoriTooltipTrigger tooltip = go.AddComponent<MemoriTooltipTrigger>();
            CollectionStatRow stat = go.AddComponent<CollectionStatRow>();
            var so = new SerializedObject(stat);
            Ref(so, "icon", icon);
            Ref(so, "statName", name);
            Ref(so, "value", value);
            Ref(so, "barFill", fill);
            Ref(so, "tooltip", tooltip);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void StripCellPart(GameObject go)
        {
            RectTransform cell = (RectTransform)go.transform;
            VerticalLayoutGroup layout = VLayout(cell, 0f, new RectOffset(0, 0, 8, 8));
            layout.childAlignment = TextAnchor.MiddleCenter;
            Flexible(go, 1f);
            // The line between cells; the strip hides it on its first cell.
            Image divider = Img(Rect("Divider", cell), null, A(Brass, 0.45f));
            Anchor(divider.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -22f), new Vector2(1f, 22f));
            divider.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            TMP_Text value = Text("Value", cell, displayDrop, 24f, Cream, "0");
            value.alignment = TextAlignmentOptions.Center;
            TMP_Text label = Text("Label", cell, body, 13f, Muted, "Label");
            label.alignment = TextAlignmentOptions.Center;
        }

        static void StripPart(GameObject go)
        {
            RectTransform strip = (RectTransform)go.transform;
            Fixed(go, -1f, 70f);
            Image top = Img(Rect("Top", strip), null, A(Brass, 0.35f));
            Anchor(top.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f), Vector2.zero);
            top.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image bottom = Img(Rect("Bottom", strip), null, A(Brass, 0.35f));
            Anchor(bottom.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            bottom.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HLayout(strip, 0f, TextAnchor.MiddleCenter, new RectOffset());
            for (int i = 1; i <= 3; i++)
            {
                RectTransform cell = Part<RectTransform>("Stat Strip Cell", strip, "Cell " + i);
                if (i > 1) continue;
                Transform divider = Child<Transform>(cell, "Divider");
                if (divider != null) divider.gameObject.SetActive(false);
            }
        }

        static void SpellRowPart(GameObject go)
        {
            RectTransform row = (RectTransform)go.transform;
            HLayout(row, 14f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandWidth = false;
            RectTransform tile = Rect("Tile", row);
            Fixed(tile.gameObject, 48f, 48f);
            Img(Stretch(Rect("Frame", tile)), roundedFill, Hex("C58A4A"), Image.Type.Sliced, 76f / 10f);
            Img(Stretch(Rect("Fill", tile), 1.5f, 1.5f, 1.5f, 1.5f), roundedFill, Hex("33241A"), Image.Type.Sliced, 76f / 9f);
            Image icon = Img(Rect("Icon", tile), null, Color.white);
            Anchor(icon.rectTransform, new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.84f), Vector2.zero, Vector2.zero);
            icon.preserveAspect = true;
            RectTransform text = Rect("Text", row);
            VLayout(text, 1f, new RectOffset()).childForceExpandWidth = false;
            Flexible(text.gameObject, 1f);
            Part<TMP_Text>("Section Label", text, "Label").text = "Spell";
            Text("Name", text, displayDrop, 19f, Cream, "Spell");
        }

        static void EffectPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            VLayout(rect, 3f, new RectOffset()).childForceExpandWidth = true;
            TMP_Text title = Text("Title", rect, displayDrop, 18f, Gold, "Effect");
            Wrap(title, TextAlignmentOptions.TopLeft);
            TMP_Text text = Text("Body", rect, body, 16.5f, Cream, "Body");
            Wrap(text, TextAlignmentOptions.TopLeft);
            CollectionEffectBlock block = go.AddComponent<CollectionEffectBlock>();
            var so = new SerializedObject(block);
            Ref(so, "title", title);
            Ref(so, "body", text);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void MiniUnitPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Fixed(go, 64f, 72f);
            Image frame = Img(Stretch(Rect("Frame", rect)), roundedFill, Hex("34423F"), Image.Type.Sliced, 76f / 6f);
            frame.raycastTarget = true;
            RectTransform mask = Stretch(Rect("Mask", rect), 1.5f, 1.5f, 1.5f, 1.5f);
            mask.gameObject.AddComponent<RectMask2D>();
            Img(Stretch(Rect("Fill", mask)), null, TileFill);
            Image portrait = Img(Rect("Portrait", mask), null, Color.white);
            Anchor(portrait.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            portrait.rectTransform.pivot = new Vector2(0.5f, 1f);
            AspectRatioFitter fit = portrait.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            fit.aspectRatio = 512f / 768f;
            Image tier = Img(Rect("Tier", mask), null, Color.white);
            Anchor(tier.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), Vector2.zero);
            TMP_Text count = Text("Count", rect, body, 13f, Cream, "");
            Anchor(count.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -24f), new Vector2(-4f, -4f));
            count.alignment = TextAlignmentOptions.TopRight;
            count.fontStyle = FontStyles.Bold;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = frame;
            TintColours(button);
            HoverSound(go, button);
            CollectionMiniUnit mini = go.AddComponent<CollectionMiniUnit>();
            var so = new SerializedObject(mini);
            Ref(so, "portrait", portrait);
            Ref(so, "tierBar", tier);
            Ref(so, "countText", count);
            Ref(so, "button", button);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CommanderRowPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            HLayout(rect, 14f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandWidth = false;
            CollectionMiniUnit portrait = Part<CollectionMiniUnit>("Mini Unit", rect, "Portrait");
            RectTransform text = Rect("Text", rect);
            VLayout(text, 2f, new RectOffset()).childForceExpandWidth = false;
            Flexible(text.gameObject, 1f);
            TMP_Text name = Text("Name", text, displayDrop, 19f, Cream, "Hero");
            TMP_Text line = Text("Line", text, body, 14f, Stat, "Treasury");
            CollectionCommanderRow row = go.AddComponent<CollectionCommanderRow>();
            var so = new SerializedObject(row);
            Ref(so, "portrait", portrait);
            Ref(so, "heroName", name);
            Ref(so, "line", line);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // What the item and unit tiles share: the coloured border, the selection ring, the lock and the new marker.
        static void TileBasePart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(98f, 98f);
            Image glowRing = Img(Stretch(Rect("Selected", rect), -5f, -5f, -5f, -5f), roundedOutline, A(Gold, 0.4f), Image.Type.Sliced, 76f / 11f);
            Image frame = Img(Stretch(Rect("Frame", rect)), roundedFill, Brass, Image.Type.Sliced, 76f / 7f);
            frame.raycastTarget = true;
            // The frame shows around this inset as the tile's coloured border.
            RectTransform mask = Stretch(Rect("Mask", rect), 1.5f, 1.5f, 1.5f, 1.5f);
            mask.gameObject.AddComponent<RectMask2D>();
            Img(Stretch(Rect("Fill", mask)), roundedFill, TileFill, Image.Type.Sliced, 76f / 6f);
            GameObject lockObject = Lock(rect);
            GameObject dot = NewDot(rect);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;
            HoverSound(go, button);
            CollectionTile tile = go.AddComponent<CollectionTile>();
            var so = new SerializedObject(tile);
            Ref(so, "button", button);
            Ref(so, "frame", frame);
            Ref(so, "lockIcon", lockObject);
            Ref(so, "newDot", dot);
            Ref(so, "selectedGlow", glowRing.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ItemTilePart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(98f, 98f);
            RectTransform mask = Child<RectTransform>(rect, "Mask");
            Image wash = Wash(mask, 95f, 42f, Color.white);
            Image icon = Img(Stretch(Rect("Icon", mask), 10.5f, 10.5f, 10.5f, 10.5f), null, Color.white);
            icon.preserveAspect = true;
            var so = new SerializedObject(go.GetComponent<CollectionTile>());
            Ref(so, "wash", wash);
            Ref(so, "icon", icon);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void UnitTilePart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(120f, 126f);
            RectTransform mask = Child<RectTransform>(rect, "Mask");
            Image portrait = Img(Rect("Portrait", mask), null, Color.white);
            Anchor(portrait.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            portrait.rectTransform.pivot = new Vector2(0.5f, 1f);
            AspectRatioFitter portraitFit = portrait.gameObject.AddComponent<AspectRatioFitter>();
            portraitFit.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            portraitFit.aspectRatio = 512f / 768f;
            Image shade = Img(Rect("Shade", mask), fadeRule, A(Color.black, 0.85f));
            Anchor(shade.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            shade.rectTransform.pivot = new Vector2(0f, 0.5f);
            shade.rectTransform.sizeDelta = new Vector2(52f, 130f);
            shade.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
            Image typeIcon = Img(Rect("Type", rect), null, Cream);
            Anchor(typeIcon.rectTransform, Vector2.zero, Vector2.zero, new Vector2(7f, 7f), new Vector2(29f, 29f));
            typeIcon.preserveAspect = true;
            var so = new SerializedObject(go.GetComponent<CollectionTile>());
            Ref(so, "icon", portrait);
            Ref(so, "typeIcon", typeIcon);
            so.FindProperty("restFrameAlpha").floatValue = 0.35f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RailSectionPart(GameObject go)
        {
            TMP_Text section = TextOn(go, body, 13f, Label, "Section");
            section.fontStyle = FontStyles.UpperCase;
            section.characterSpacing = 12f;
            section.margin = new Vector4(14f, 18f, 0f, 8f);
        }

        static void RailRowPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Fixed(go, -1f, 50f);
            Image background = Img(Stretch(Rect("Background", rect)), roundedFill, Color.clear, Image.Type.Sliced, 76f / 4f);
            background.raycastTarget = true;
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            HoverSound(go, button);
            Image accent = Img(Rect("Accent", rect), null, Brass);
            Anchor(accent.rectTransform, new Vector2(0f, 0.12f), new Vector2(0f, 0.88f), Vector2.zero, new Vector2(3f, 0f));
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HorizontalLayoutGroup row = HLayout(rect, 12f, TextAnchor.MiddleLeft, new RectOffset(16, 14, 0, 4));
            row.childForceExpandWidth = false;
            RectTransform markerSlot = Rect("Marker Slot", rect);
            Fixed(markerSlot.gameObject, 22f, 22f);
            Image marker = Img(Rect("Marker", markerSlot), null, Color.white);
            Centre(marker.rectTransform, 22f, 22f);
            TMP_Text label = Text("Label", rect, displayDrop, 19f, Parchment, "Row");
            label.overflowMode = TextOverflowModes.Ellipsis;
            Flexible(label.gameObject, 1f);
            TMP_Text count = Text("Count", rect, body, 15f, Muted, "0/0");
            count.alignment = TextAlignmentOptions.MidlineRight;
            RectTransform progress = Rect("Progress", rect);
            Anchor(progress, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(50f, 6f), new Vector2(-14f, 8f));
            progress.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Img(Stretch(Rect("Track", progress)), null, Hex("223032"));
            RectTransform fill = Rect("Fill", progress);
            Anchor(fill, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            Image fillImage = Img(fill, null, Hex("705F3A"));
            Image dot = Img(Rect("New", rect), null, Gold);
            Anchor(dot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -14f), new Vector2(12f, -6f));
            dot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            dot.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            CollectionRailRow rail = go.AddComponent<CollectionRailRow>();
            var so = new SerializedObject(rail);
            Ref(so, "button", button);
            Ref(so, "background", background);
            Ref(so, "accent", accent);
            Ref(so, "marker", marker);
            Ref(so, "label", label);
            Ref(so, "count", count);
            Ref(so, "progressFill", fill);
            Ref(so, "progressImage", fillImage);
            Ref(so, "newDot", dot.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void GroupHeaderPart(GameObject go)
        {
            RectTransform header = (RectTransform)go.transform;
            HorizontalLayoutGroup row = HLayout(header, 10f, TextAnchor.MiddleLeft, new RectOffset(0, 0, 8, 4));
            row.childForceExpandWidth = false;
            Image diamond = Img(Rect("Diamond", header), null, Gold);
            Fixed(diamond.gameObject, 9f, 9f);
            diamond.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            TMP_Text label = Text("Label", header, displayDrop, 18f, Cream, "Rare");
            TMP_Text count = Text("Count", header, body, 14f, Muted, "0 of 0 found");
            Image rule = Img(Rect("Rule", header), fadeRule, A(Brass, 0.4f));
            Flexible(rule.gameObject, 1f).preferredHeight = 1f;
            CollectionGroupHeader component = go.AddComponent<CollectionGroupHeader>();
            var so = new SerializedObject(component);
            Ref(so, "diamond", diamond);
            Ref(so, "label", label);
            Ref(so, "count", count);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void GroupGridPart(GameObject go)
        {
            GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
            // The cell size is the item tile size. Sized so all six gear rows fit at 1080p without scrolling.
            grid.cellSize = new Vector2(98f, 98f);
            grid.spacing = new Vector2(10f, 10f);
            grid.padding = new RectOffset(4, 4, 4, 10);
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        static void PageTabPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Image hit = Img(Stretch(Rect("Hit", rect)), null, Color.clear);
            hit.raycastTarget = true;
            hit.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            HoverSound(go, button);
            // Force-expanded width would make each tab report itself flexible and spread the row apart. The label
            // fills the tab's height so its baseline matches the rest of the header row.
            VerticalLayoutGroup tabLayout = VLayout(rect, 0f, new RectOffset());
            tabLayout.childForceExpandWidth = false;
            tabLayout.childForceExpandHeight = true;
            TMP_Text label = Text("Label", rect, displayDrop, 20f, Muted, "Tab");
            label.alignment = TextAlignmentOptions.BaselineLeft;
            Image underline = Img(Rect("Underline", rect), null, Gold);
            Anchor(underline.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 3f));
            underline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            CollectionTab tab = go.AddComponent<CollectionTab>();
            var so = new SerializedObject(tab);
            Ref(so, "button", button);
            Ref(so, "label", label);
            Ref(so, "activeMark", underline);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void HeroTabPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Image frame = Img(Stretch(Rect("Frame", rect)), roundedFill, Hex("34423F"), Image.Type.Sliced, 76f / 6f);
            frame.raycastTarget = true;
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image fill = Img(Stretch(Rect("Fill", rect), 1.5f, 1.5f, 1.5f, 1.5f), roundedFill, Hex("172124"), Image.Type.Sliced, 76f / 5f);
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;
            HoverSound(go, button);
            HorizontalLayoutGroup row = HLayout(rect, 12f, TextAnchor.MiddleLeft, new RectOffset(6, 18, 6, 6));
            row.childForceExpandWidth = false;
            RectTransform face = Rect("Face", rect);
            Fixed(face.gameObject, 52f, 52f);
            face.gameObject.AddComponent<RectMask2D>();
            Img(Stretch(Rect("Back", face)), null, Hex("0E1416"));
            Image portrait = Img(Rect("Portrait", face), null, Color.white);
            Anchor(portrait.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -78f), Vector2.zero);
            TMP_Text label = Text("Label", rect, displayDrop, 18f, Muted, "Hero");
            CollectionTab tab = go.AddComponent<CollectionTab>();
            var so = new SerializedObject(tab);
            Ref(so, "button", button);
            Ref(so, "label", label);
            Ref(so, "frame", frame);
            Ref(so, "portrait", portrait);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void HeaderEffectPart(GameObject go)
        {
            TMP_Text text = TextOn(go, body, 14f, Muted, "Effect");
            text.raycastTarget = true;
            text.alignment = TextAlignmentOptions.BaselineRight;
            go.AddComponent<MemoriTooltipTrigger>();
        }

        static void TurnButtonPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Fixed(go, 34f, 34f);
            Img(Stretch(Rect("Ring", rect)), roundedFill, Hex("4A5A5D"), Image.Type.Sliced, 76f / 17f);
            Image fill = Img(Stretch(Rect("Fill", rect), 1f, 1f, 1f, 1f), roundedFill, Hex("1A2427"), Image.Type.Sliced, 76f / 16f);
            fill.raycastTarget = true;
            Image glyph = Img(Rect("Arrow", rect), arrow, Sub);
            Centre(glyph.rectTransform, 14f, 14f);
            glyph.preserveAspect = true;
            CollectionHoldButton hold = go.AddComponent<CollectionHoldButton>();
            HoverSound(go, null);
            var so = new SerializedObject(hold);
            Ref(so, "fill", fill);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ScrollPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            ScrollRect scroll = go.AddComponent<ScrollRect>();
            RectTransform viewport = Stretch(Rect("Viewport", rect));
            viewport.gameObject.AddComponent<RectMask2D>();
            // A clear graphic lets the wheel scroll from empty space between rows.
            Img(viewport, null, Color.clear).raycastTarget = true;
            RectTransform content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.inertia = true;

            RectTransform barRect = Rect("Scrollbar", rect);
            Anchor(barRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-4f, 4f), new Vector2(0f, -4f));
            Img(barRect, null, A(Brass, 0.08f)).raycastTarget = true;
            RectTransform slidingArea = Stretch(Rect("Sliding Area", barRect));
            RectTransform handle = Stretch(Rect("Handle", slidingArea));
            Image handleImage = Img(handle, null, A(Brass, 0.5f));
            handleImage.raycastTarget = true;
            Scrollbar scrollbar = barRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            Navigation nav = scrollbar.navigation;
            nav.mode = Navigation.Mode.None;
            scrollbar.navigation = nav;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        // Shared with the Settings header, so both screens close with the same button.
        static void CloseButtonPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Image frame = Img(Stretch(Rect("Frame", rect)), panel, Color.white, Image.Type.Sliced, 1f);
            frame.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = frame;
            TintColours(button);
            HoverSound(go, button);
            rect.sizeDelta = new Vector2(130f, 44f);
            HorizontalLayoutGroup row = HLayout(rect, 10f, TextAnchor.MiddleCenter, new RectOffset(16, 12, 6, 6));
            row.childForceExpandWidth = false;
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Text("Label", rect, displayDrop, 18f, Cream, "Close");
            RectTransform cap = Rect("Key", rect);
            Img(cap, keyCap, Color.white, Image.Type.Sliced, 1f);
            HorizontalLayoutGroup capRow = HLayout(cap, 0f, TextAnchor.MiddleCenter, new RectOffset(8, 8, 2, 4));
            capRow.childForceExpandWidth = false;
            TMP_Text capText = Text("Text", cap, body, 13f, Cream, "Esc");
            capText.alignment = TextAlignmentOptions.Center;
        }

        static GameObject Lock(RectTransform parent)
        {
            Image image = Img(Rect("Lock", parent), lockIcon, Color.white);
            Anchor(image.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 6f), new Vector2(-6f, 30f));
            image.preserveAspect = true;
            return image.gameObject;
        }

        static GameObject NewDot(RectTransform parent)
        {
            Image dot = Img(Rect("New", parent), null, Gold);
            Anchor(dot.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(0f, 0f));
            dot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Outline ring = dot.gameObject.AddComponent<Outline>();
            ring.effectColor = Hex("10181A");
            ring.effectDistance = new Vector2(1.5f, 1.5f);
            return dot.gameObject;
        }

        // A vertical fade from the bottom edge: the horizontal fade rule turned on its side.
        static Image Wash(RectTransform parent, float width, float height, Color colour)
        {
            Image wash = Img(Rect("Wash", parent), fadeRule, colour);
            Anchor(wash.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            wash.rectTransform.pivot = new Vector2(0f, 0.5f);
            wash.rectTransform.sizeDelta = new Vector2(height, width);
            wash.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
            return wash;
        }
        #endregion

        #region Layout
        static void Build(GameObject rootGo, RenderTexture texture)
        {
            RectTransform root = (RectTransform)rootGo.transform;
            rootGo.name = "Collection Panel";
            rootGo.layer = 5;
            Stretch(root);
            // Root components are kept across rebuilds: the scene manager references this panel by id.
            GetOrAdd<CanvasGroup>(rootGo);
            GetOrAdd<MemoriCanvasGroup>(rootGo);
            CollectionPanel panel = GetOrAdd<CollectionPanel>(rootGo);

            // The page is opaque: nothing of the menu, map or battle behind it may show or take clicks.
            Img(Stretch(Rect("Backdrop", root)), null, Ground).raycastTarget = true;
            EdgeShade(root);

            // Header
            RectTransform header = Rect("Header", root);
            Top(header, 88f);
            Img(Stretch(Rect("Fill", header)), null, HeaderFill);
            Image headerRule = Img(Rect("Rule", header), null, A(Brass, 0.55f));
            Anchor(headerRule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            TMP_Text title = Text("Title", header, displayDrop, 38f, Gold, "Collection");
            Anchor(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(48f, 0f), Vector2.zero);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.characterSpacing = 2f;

            RectTransform headerRight = Rect("Right", header);
            Anchor(headerRight, new Vector2(0.4f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-48f, 0f));
            HorizontalLayoutGroup rightRow = HLayout(headerRight, 16f, TextAnchor.MiddleRight, new RectOffset());
            rightRow.childControlHeight = false;
            TMP_Text progress = Text("Progress", headerRight, body, 17f, Sub, "0 of 0 found");
            progress.alignment = TextAlignmentOptions.MidlineRight;
            RectTransform bar = Rect("Bar", headerRight);
            Fixed(bar.gameObject, 240f, 6f);
            Img(Stretch(Rect("Track", bar)), null, Hex("223032"));
            RectTransform barFill = Rect("Fill", bar);
            Anchor(barFill, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            Img(barFill, null, Gold);
            RectTransform gap = Rect("Gap", headerRight);
            Fixed(gap.gameObject, 22f, 10f);
            Button close = CloseButton(headerRight, out TMP_Text closeLabel);

            // Rail
            RectTransform rail = Rect("Rail", root);
            Anchor(rail, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(48f, 40f), new Vector2(328f, -122f));
            VerticalLayoutGroup railLayout = VLayout(rail, 0f, new RectOffset());
            railLayout.childForceExpandWidth = true;

            // Content
            RectTransform content = Rect("Content", root);
            Stretch(content, 360f, 48f, 122f, 40f);
            CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();

            RectTransform pageHeader = Rect("Page Header", content);
            Top(pageHeader, 64f);
            Image pageRule = Img(Rect("Rule", pageHeader), null, Line);
            Anchor(pageRule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            // TMP Baseline alignment puts a text's baseline at the vertical centre of its rect, so every text in
            // this row fills a band twice the baseline height and shares one baseline whatever its font or size.
            RectTransform headRow = Rect("Row", pageHeader);
            Anchor(headRow, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, HeaderBaseline * 2f));
            HLayout(headRow, 16f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandHeight = true;
            RectTransform markerSlot = Rect("Marker Slot", headRow);
            Fixed(markerSlot.gameObject, 16f, -1f);
            Image marker = Img(Rect("Marker", markerSlot), null, Gold);
            Centre(marker.rectTransform, 14f, 14f);
            // Raised to half the title's cap height, so it centres on the letters rather than the baseline.
            marker.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            marker.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            TMP_Text pageTitle = Text("Title", headRow, displayDrop, 34f, Cream, "Gear");
            pageTitle.alignment = TextAlignmentOptions.BaselineLeft;
            TMP_Text pageSubtitle = Text("Subtitle", headRow, body, 15f, Sub, "0 of 0 found");
            pageSubtitle.alignment = TextAlignmentOptions.BaselineLeft;
            RectTransform tabs = Rect("Tabs", headRow);
            HLayout(tabs, 30f, TextAnchor.MiddleLeft, new RectOffset(18, 0, 0, 0)).childForceExpandHeight = true;
            CollectionTab unitsTab = PageTab(tabs, "Units");
            CollectionTab heroesTab = PageTab(tabs, "Heroes");
            CollectionTab loreTab = PageTab(tabs, "Lore");
            RectTransform headSpacer = Rect("Spacer", headRow);
            Flexible(headSpacer.gameObject, 1f);
            RectTransform effects = Rect("Effects", headRow);
            HLayout(effects, 22f, TextAnchor.MiddleRight, new RectOffset()).childForceExpandHeight = true;
            TMP_Text battleEffect = HeaderEffect(effects, "Battle", out MemoriTooltipTrigger battleTooltip);
            TMP_Text campaignEffect = HeaderEffect(effects, "Campaign", out MemoriTooltipTrigger campaignTooltip);

            RectTransform bodyArea = Rect("Body", content);
            Stretch(bodyArea, 0f, 0f, 84f, 0f);

            CollectionDetailPanel detail = DetailPanel(bodyArea);

            RectTransform main = Rect("Main", bodyArea);
            Stretch(main, 0f, 472f, 0f, 0f);

            // Grid page (gear, potions)
            ScrollRect gridScroll = Scroll(main, "Grid", out RectTransform gridContent);
            Stretch((RectTransform)gridScroll.transform);
            VerticalLayoutGroup gridLayout = VLayout(gridContent, 4f, new RectOffset(0, 20, 0, 24));
            gridLayout.childForceExpandWidth = true;
            RectTransform gearGroups = Rect("Gear Groups", gridContent);
            VLayout(gearGroups, 6f, new RectOffset()).childForceExpandWidth = true;
            RectTransform potionGroups = Rect("Potion Groups", gridContent);
            VLayout(potionGroups, 6f, new RectOffset()).childForceExpandWidth = true;

            // Stage page (units, heroes)
            RectTransform stageRoot = Stretch(Rect("Stage Page", main));
            VerticalLayoutGroup stageLayout = VLayout(stageRoot, 16f, new RectOffset());
            stageLayout.childForceExpandWidth = true;
            stageLayout.childForceExpandHeight = false;

            // Hero tabs get their own row above the stage so they never cover a model on a short stage.
            RectTransform heroTabs = Rect("Hero Tabs", stageRoot);
            HLayout(heroTabs, 12f, TextAnchor.UpperLeft, new RectOffset(0, 0, 0, 0));
            Fixed(heroTabs.gameObject, -1f, 64f);
            CollectionTab heroTab1 = Part<CollectionTab>("Hero Tab", heroTabs, "Hero Tab 1");
            CollectionTab heroTab2 = Part<CollectionTab>("Hero Tab", heroTabs, "Hero Tab 2");

            RectTransform stage = Rect("Stage", stageRoot);
            LayoutElement stageElement = Flexible(stage.gameObject, 1f, 1f);
            stageElement.minHeight = 240f;
            RectTransform stageImageRect = Stretch(Rect("Stage Image", stage));
            RawImage stageImage = stageImageRect.gameObject.AddComponent<RawImage>();
            stageImage.texture = texture;
            stageImage.raycastTarget = true;
            AspectRatioFitter fitter = stageImageRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = StageAspect;
            CollectionStageInput stageInput = stageImageRect.gameObject.AddComponent<CollectionStageInput>();

            RectTransform turnRow = Rect("Turn", stage);
            Anchor(turnRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160f, 6f), new Vector2(160f, 44f));
            HLayout(turnRow, 12f, TextAnchor.MiddleCenter, new RectOffset());
            CollectionHoldButton turnLeft = Part<CollectionHoldButton>("Turn Button", turnRow, "Turn Left");
            // The prefab's arrow points right; the left button mirrors it.
            Transform leftArrow = Child<Transform>(turnLeft.transform, "Arrow");
            if (leftArrow != null) leftArrow.localEulerAngles = new Vector3(0f, 180f, 0f);
            TMP_Text hint = Text("Hint", turnRow, body, 14f, Muted, "Drag to turn");
            hint.alignment = TextAlignmentOptions.Center;
            CollectionHoldButton turnRight = Part<CollectionHoldButton>("Turn Button", turnRow, "Turn Right");
            var inputObject = new SerializedObject(stageInput);
            Ref(inputObject, "turnLeft", turnLeft);
            Ref(inputObject, "turnRight", turnRight);
            inputObject.ApplyModifiedPropertiesWithoutUndo();

            RectTransform roster = Rect("Roster", stageRoot);
            GridLayoutGroup rosterGrid = roster.gameObject.AddComponent<GridLayoutGroup>();
            rosterGrid.cellSize = new Vector2(120f, 126f);
            rosterGrid.spacing = new Vector2(10f, 10f);
            rosterGrid.childAlignment = TextAnchor.UpperCenter;
            rosterGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            rosterGrid.constraintCount = 8;
            roster.gameObject.AddComponent<CollectionAutoGrid>();
            // A fixed-column grid reports all its columns as a minimum width, which would hold the roster at its
            // 100% size in a narrower column; a zero minimum lets it take the column width and refit.
            LayoutElement rosterElement = roster.gameObject.AddComponent<LayoutElement>();
            rosterElement.minWidth = 0f;
            rosterElement.flexibleWidth = 1f;

            ScrollRect heroLoreScroll = Scroll(stageRoot, "Hero Lore", out RectTransform heroLoreContent);
            Fixed(heroLoreScroll.gameObject, -1f, 250f);
            VLayout(heroLoreContent, 0f, new RectOffset(0, 24, 0, 12)).childForceExpandWidth = false;
            TMP_Text heroLore = Paragraph(heroLoreContent, 19f, 900f);

            // Lore page
            ScrollRect loreScroll = Scroll(main, "Lore Page", out RectTransform loreContent);
            Stretch((RectTransform)loreScroll.transform);
            VLayout(loreContent, 0f, new RectOffset(0, 24, 6, 30)).childForceExpandWidth = false;
            TMP_Text lore = Paragraph(loreContent, 20f, 780f);

            var so = new SerializedObject(panel);
            Ref(so, "closeButton", close);
            Ref(so, "closeLabel", closeLabel);
            Ref(so, "titleText", title);
            Ref(so, "progressText", progress);
            Ref(so, "progressFill", barFill);
            Ref(so, "railContainer", rail);
            Ref(so, "railSectionTemplate", PartAsset<TMP_Text>("Rail Section"));
            Ref(so, "railRowTemplate", PartAsset<CollectionRailRow>("Rail Row"));
            Ref(so, "gearRailIcon", gearIcon);
            Ref(so, "potionRailIcon", potionIcon);
            Ref(so, "headerMarker", marker);
            Ref(so, "headerTitle", pageTitle);
            Ref(so, "headerSubtitle", pageSubtitle);
            Ref(so, "tabsRoot", tabs.gameObject);
            Ref(so, "unitsTab", unitsTab);
            Ref(so, "heroesTab", heroesTab);
            Ref(so, "loreTab", loreTab);
            Ref(so, "effectsRoot", effects.gameObject);
            Ref(so, "battleEffectText", battleEffect);
            Ref(so, "battleEffectTooltip", battleTooltip);
            Ref(so, "campaignEffectText", campaignEffect);
            Ref(so, "campaignEffectTooltip", campaignTooltip);
            Ref(so, "gridRoot", gridScroll.gameObject);
            Ref(so, "gridScroll", gridScroll);
            Ref(so, "gearGroups", gearGroups);
            Ref(so, "potionGroups", potionGroups);
            Ref(so, "groupHeaderTemplate", PartAsset<CollectionGroupHeader>("Group Header"));
            Ref(so, "groupGridTemplate", PartAsset<RectTransform>("Group Grid"));
            Ref(so, "itemTileTemplate", PartAsset<CollectionTile>("Item Tile"));
            Ref(so, "stageRoot", stageRoot.gameObject);
            Ref(so, "stageInput", stageInput);
            Ref(so, "stageHint", hint);
            Ref(so, "rosterRoot", roster.gameObject);
            Ref(so, "rosterGrid", roster);
            Ref(so, "unitTileTemplate", PartAsset<CollectionTile>("Unit Tile"));
            Ref(so, "heroTabsRoot", heroTabs.gameObject);
            SerializedProperty heroTabArray = so.FindProperty("heroTabs");
            heroTabArray.arraySize = 2;
            heroTabArray.GetArrayElementAtIndex(0).objectReferenceValue = heroTab1;
            heroTabArray.GetArrayElementAtIndex(1).objectReferenceValue = heroTab2;
            Ref(so, "heroLoreRoot", heroLoreScroll.gameObject);
            Ref(so, "heroLoreScroll", heroLoreScroll);
            Ref(so, "heroLoreText", heroLore);
            Ref(so, "loreRoot", loreScroll.gameObject);
            Ref(so, "loreScroll", loreScroll);
            Ref(so, "loreText", lore);
            Ref(so, "detail", detail);
            Ref(so, "contentGroup", contentGroup);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        #region Detail panel
        static CollectionDetailPanel DetailPanel(RectTransform parent)
        {
            RectTransform frame = Rect("Detail", parent);
            Anchor(frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-440f, 0f), Vector2.zero);
            Image frameShadow = Img(Rect("Shadow", frame), shadow, A(Color.white, 0.8f), Image.Type.Sliced, 1f);
            Stretch(frameShadow.rectTransform, -30f, -30f, -18f, -42f);
            Img(Stretch(Rect("Panel", frame)), panel, Color.white, Image.Type.Sliced, 1f).raycastTarget = true;
            CollectionDetailPanel detail = frame.gameObject.AddComponent<CollectionDetailPanel>();

            ScrollRect scroll = Scroll(frame, "Scroll", out RectTransform content);
            Stretch((RectTransform)scroll.transform, 6f, 6f, 6f, 6f);
            VLayout(content, 0f, new RectOffset(26, 26, 26, 26)).childForceExpandWidth = true;

            var so = new SerializedObject(detail);
            Ref(so, "scroll", scroll);
            Ref(so, "statRowTemplate", PartAsset<CollectionStatRow>("Stat Row"));
            Ref(so, "chipTemplate", PartAsset<CollectionChip>("Chip"));
            Ref(so, "chipRowTemplate", PartAsset<HorizontalLayoutGroup>("Chip Row"));
            Ref(so, "effectTemplate", PartAsset<CollectionEffectBlock>("Effect"));
            Ref(so, "miniUnitTemplate", PartAsset<CollectionMiniUnit>("Mini Unit"));
            Ref(so, "commanderTemplate", PartAsset<CollectionCommanderRow>("Commander Row"));

            // Item view
            RectTransform item = View(content, "Item View", 12f, TextAnchor.UpperCenter);
            RectTransform mount = Rect("Mount", item);
            Fixed(mount.gameObject, -1f, 230f);
            Image glow = Img(Rect("Glow", mount), null, A(Gold, 0.3f));
            Centre(glow.rectTransform, 176f, 176f);
            glow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Image diamond = Img(Rect("Diamond", mount), panel, Color.white, Image.Type.Sliced, 1f);
            Centre(diamond.rectTransform, 150f, 150f);
            diamond.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Image itemIcon = Img(Rect("Icon", mount), null, Color.white);
            Centre(itemIcon.rectTransform, 176f, 176f);
            itemIcon.preserveAspect = true;
            TMP_Text itemName = Text("Name", item, displayDrop, 30f, Gold, "Item");
            Wrap(itemName, TextAlignmentOptions.Center);
            RectTransform itemSub = Rect("Sub", item);
            HLayout(itemSub, 10f, TextAnchor.MiddleCenter, new RectOffset()).childForceExpandWidth = false;
            CollectionChip itemRarity = Part<CollectionChip>("Chip", itemSub);
            TMP_Text itemKind = Text("Kind", itemSub, body, 15f, Sub, "Gear");
            Part<RectTransform>("Rule", item);
            TMP_Text itemBody = Text("Body", item, body, 19f, Cream, "Effect");
            Wrap(itemBody, TextAlignmentOptions.Center);
            TMP_Text itemFlavour = Text("Flavour", item, display, 16f, Flavour, "Flavour");
            itemFlavour.fontStyle = FontStyles.Italic;
            Wrap(itemFlavour, TextAlignmentOptions.Center);
            Ref(so, "itemView", item.gameObject);
            Ref(so, "itemGlow", glow);
            Ref(so, "itemIcon", itemIcon);
            Ref(so, "itemName", itemName);
            Ref(so, "itemRarity", itemRarity);
            Ref(so, "itemKind", itemKind);
            Ref(so, "itemBody", itemBody);
            Ref(so, "itemFlavour", itemFlavour);

            // Unit view
            RectTransform unit = View(content, "Unit View", 14f, TextAnchor.UpperLeft);
            TMP_Text unitName = Text("Name", unit, displayDrop, 30f, Gold, "Unit");
            Wrap(unitName, TextAlignmentOptions.TopLeft);
            RectTransform unitSub = Rect("Sub", unit);
            HLayout(unitSub, 10f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandWidth = false;
            Image typeIcon = Img(Rect("Type Icon", unitSub), null, Sub);
            typeIcon.preserveAspect = true;
            Fixed(typeIcon.gameObject, 22f, 22f);
            TMP_Text unitType = Text("Type", unitSub, body, 16f, Sub, "Melee Infantry");
            CollectionChip unitTier = Part<CollectionChip>("Chip", unitSub);
            TMP_Text notFound = Text("Not Found", unit, body, 15f, Hex("D98A6B"), "Not recruited yet");
            RectTransform chips = Rect("Traits", unit);
            VLayout(chips, 8f, new RectOffset()).childForceExpandWidth = true;
            Strip(unit, 3, out TMP_Text[] unitValues, out TMP_Text[] unitLabels);
            TMP_Text statsLabel = SectionLabel(unit, "Stats");
            RectTransform stats = Rect("Stats", unit);
            VLayout(stats, 6f, new RectOffset()).childForceExpandWidth = true;
            RectTransform unitSpell = SpellRow(unit, "Spell Row", 0f, out Image unitSpellIcon, out TMP_Text unitSpellLabel, out TMP_Text unitSpellName);
            Part<RectTransform>("Rule", unit);
            TMP_Text factionLabel = SectionLabel(unit, "Faction Effect");
            TMP_Text factionText = Text("Faction Text", unit, display, 16f, Parchment, "Effect");
            Wrap(factionText, TextAlignmentOptions.TopLeft);
            factionText.raycastTarget = true;
            MemoriTooltipTrigger factionTooltip = factionText.gameObject.AddComponent<MemoriTooltipTrigger>();
            Ref(so, "unitView", unit.gameObject);
            Ref(so, "unitName", unitName);
            Ref(so, "unitTypeIcon", typeIcon);
            Ref(so, "unitType", unitType);
            Ref(so, "unitTier", unitTier);
            Ref(so, "unitNotFound", notFound);
            Ref(so, "unitChips", chips);
            Refs(so, "unitStripValues", unitValues);
            Refs(so, "unitStripLabels", unitLabels);
            Ref(so, "unitStatsLabel", statsLabel);
            Ref(so, "unitStats", stats);
            Ref(so, "unitSpellRow", unitSpell.gameObject);
            Ref(so, "unitSpellIcon", unitSpellIcon);
            Ref(so, "unitSpellLabel", unitSpellLabel);
            Ref(so, "unitSpellName", unitSpellName);
            Ref(so, "unitFactionLabel", factionLabel);
            Ref(so, "unitFactionText", factionText);
            Ref(so, "unitFactionTooltip", factionTooltip);

            // Hero view
            RectTransform hero = View(content, "Hero View", 14f, TextAnchor.UpperLeft);
            TMP_Text eyebrow = SectionLabel(hero, "Hero");
            eyebrow.color = Sub;
            TMP_Text heroName = Text("Name", hero, displayDrop, 30f, Gold, "Hero");
            Wrap(heroName, TextAlignmentOptions.TopLeft);
            RectTransform locked = Rect("Locked", hero);
            HLayout(locked, 10f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandWidth = false;
            CollectionChip lockedChip = Part<CollectionChip>("Chip", locked);
            TMP_Text lockedText = Text("Text", locked, body, 14f, Sub, "Unlock");
            Wrap(lockedText, TextAlignmentOptions.MidlineLeft);
            Flexible(lockedText.gameObject, 1f);
            Strip(hero, 2, out TMP_Text[] heroValues, out TMP_Text[] heroLabels);
            TMP_Text effectsLabel = SectionLabel(hero, "Hero Effects");
            RectTransform heroEffects = Rect("Effects", hero);
            VLayout(heroEffects, 10f, new RectOffset()).childForceExpandWidth = true;
            Part<RectTransform>("Rule", hero);
            RectTransform signature = Rect("Signature Unit", hero);
            HLayout(signature, 14f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandWidth = false;
            CollectionMiniUnit signatureUnit = Part<CollectionMiniUnit>("Mini Unit", signature, "Portrait");
            RectTransform signatureText = Rect("Text", signature);
            VLayout(signatureText, 1f, new RectOffset()).childForceExpandWidth = false;
            Flexible(signatureText.gameObject, 1f);
            TMP_Text signatureLabel = SectionLabel(signatureText, "Signature Unit");
            TMP_Text signatureName = Text("Name", signatureText, displayDrop, 19f, Cream, "Unit");
            Button link = LinkButton(signatureText, out TMP_Text linkText);
            // Hero spells get a larger tile than a mage's spell.
            RectTransform heroSpell = SpellRow(hero, "Signature Spell", 64f, out Image heroSpellIcon, out TMP_Text heroSpellLabel, out TMP_Text heroSpellName);
            TMP_Text armyLabel = SectionLabel(hero, "Starting Army");
            RectTransform army = Rect("Army", hero);
            HLayout(army, 10f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandWidth = false;
            Ref(so, "heroView", hero.gameObject);
            Ref(so, "heroName", heroName);
            Ref(so, "heroEyebrow", eyebrow);
            Ref(so, "heroLockedRow", locked.gameObject);
            Ref(so, "heroLocked", lockedChip);
            Ref(so, "heroLockedText", lockedText);
            Refs(so, "heroStripValues", heroValues);
            Refs(so, "heroStripLabels", heroLabels);
            Ref(so, "heroEffectsLabel", effectsLabel);
            Ref(so, "heroEffects", heroEffects);
            Ref(so, "heroSignatureUnit", signatureUnit);
            Ref(so, "heroSignatureLabel", signatureLabel);
            Ref(so, "heroSignatureName", signatureName);
            Ref(so, "heroSignatureLink", link);
            Ref(so, "heroSignatureLinkText", linkText);
            Ref(so, "heroSpellRow", heroSpell.gameObject);
            Ref(so, "heroSpellIcon", heroSpellIcon);
            Ref(so, "heroSpellLabel", heroSpellLabel);
            Ref(so, "heroSpellName", heroSpellName);
            Ref(so, "heroArmyLabel", armyLabel);
            Ref(so, "heroArmy", army);

            // Faction view
            RectTransform faction = View(content, "Faction View", 14f, TextAnchor.UpperLeft);
            TMP_Text factionEffectsLabel = SectionLabel(faction, "Faction Effects");
            RectTransform factionEffects = Rect("Effects", faction);
            VLayout(factionEffects, 12f, new RectOffset()).childForceExpandWidth = true;
            Part<RectTransform>("Rule", faction);
            TMP_Text commandersLabel = SectionLabel(faction, "Commanders");
            RectTransform commanders = Rect("Commanders", faction);
            VLayout(commanders, 10f, new RectOffset()).childForceExpandWidth = true;
            Part<RectTransform>("Rule", faction);
            RectTransform foot = Rect("Foot", faction);
            HLayout(foot, 10f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandWidth = false;
            TMP_Text footLabel = SectionLabel(foot, "Roster");
            Flexible(Rect("Spacer", foot).gameObject, 1f);
            TMP_Text footValue = Text("Value", foot, body, 15f, Cream, "0 of 0 found");
            Ref(so, "factionView", faction.gameObject);
            Ref(so, "factionEffectsLabel", factionEffectsLabel);
            Ref(so, "factionEffects", factionEffects);
            Ref(so, "commandersLabel", commandersLabel);
            Ref(so, "commanders", commanders);
            Ref(so, "factionFootLabel", footLabel);
            Ref(so, "factionFootValue", footValue);

            so.ApplyModifiedPropertiesWithoutUndo();
            return detail;
        }

        static RectTransform View(RectTransform parent, string name, float spacing, TextAnchor alignment)
        {
            RectTransform view = Rect(name, parent);
            VerticalLayoutGroup layout = VLayout(view, spacing, new RectOffset());
            layout.childAlignment = alignment;
            layout.childForceExpandWidth = true;
            return view;
        }

        static void Strip(Transform parent, int cells, out TMP_Text[] values, out TMP_Text[] labels)
        {
            RectTransform strip = Part<RectTransform>("Stat Strip", parent);
            values = new TMP_Text[cells];
            labels = new TMP_Text[cells];
            int used = 0;
            foreach (Transform cell in strip)
            {
                if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(cell.gameObject) != parts["Stat Strip Cell"]) continue;
                bool keep = used < cells;
                cell.gameObject.SetActive(keep);
                if (!keep) continue;
                values[used] = Child<TMP_Text>(cell, "Value");
                labels[used] = Child<TMP_Text>(cell, "Label");
                used++;
            }
            if (used < cells) Debug.LogError($"CollectionCodexBuilder: the Stat Strip prefab has {used} cells; the detail panel needs {cells}.");
        }

        static RectTransform SpellRow(Transform parent, string name, float tileSize, out Image icon, out TMP_Text label, out TMP_Text spellName)
        {
            RectTransform row = Part<RectTransform>("Spell Row", parent, name);
            LayoutElement tile = Child<LayoutElement>(row, "Tile");
            if (tileSize > 0f && tile != null) Fixed(tile.gameObject, tileSize, tileSize);
            icon = Child<Image>(row, "Tile/Icon");
            label = Child<TMP_Text>(row, "Text/Label");
            spellName = Child<TMP_Text>(row, "Text/Name");
            return row;
        }
        #endregion

        #region Widgets
        static CollectionTab PageTab(Transform parent, string text)
        {
            CollectionTab tab = Part<CollectionTab>("Page Tab", parent, text + " Tab");
            tab.SetLabel(text);
            return tab;
        }

        static TMP_Text HeaderEffect(Transform parent, string name, out MemoriTooltipTrigger tooltip)
        {
            TMP_Text text = Part<TMP_Text>("Header Effect", parent, name);
            text.text = name;
            tooltip = text.GetComponent<MemoriTooltipTrigger>();
            return text;
        }

        static TMP_Text SectionLabel(Transform parent, string text)
        {
            TMP_Text label = Part<TMP_Text>("Section Label", parent, text);
            label.text = text;
            return label;
        }

        static ScrollRect Scroll(Transform parent, string name, out RectTransform content)
        {
            ScrollRect scroll = Part<ScrollRect>("Scroll View", parent, name);
            content = scroll.content;
            return scroll;
        }

        static Button CloseButton(Transform parent, out TMP_Text label)
        {
            Button button = Part<Button>("Close Button", parent, "Close");
            label = Child<TMP_Text>(button.transform, "Label");
            return button;
        }

        // Darkens the page toward its left, right and bottom edges with the pack's linear fade.
        static void EdgeShade(RectTransform root)
        {
            Color shade = Hex("050809", 0.45f);
            Image left = Img(Rect("Shade Left", root), edgeFade, shade);
            Anchor(left.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(260f, 0f));
            Image right = Img(Rect("Shade Right", root), edgeFade, shade);
            Anchor(right.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-260f, 0f), Vector2.zero);
            right.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            Image bottom = Img(Rect("Shade Bottom", root), edgeFade, shade);
            Anchor(bottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            bottom.rectTransform.pivot = new Vector2(0f, 0.5f);
            bottom.rectTransform.sizeDelta = new Vector2(180f, 5000f);
            bottom.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
        }

        static Button LinkButton(Transform parent, out TMP_Text label)
        {
            label = Text("Link", parent, body, 14f, Stat, "View in roster");
            label.raycastTarget = true;
            Button button = label.gameObject.AddComponent<Button>();
            button.targetGraphic = label;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1f, 0.92f, 0.75f, 1f);
            button.colors = colours;
            HoverSound(label.gameObject, button);
            return button;
        }

        static TMP_Text Paragraph(Transform parent, float size, float width)
        {
            TMP_Text text = Text("Text", parent, display, size, Parchment, "Lore");
            Wrap(text, TextAlignmentOptions.TopLeft);
            text.lineSpacing = 8f;
            text.paragraphSpacing = 18f;
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
            return text;
        }

        static void Wrap(TMP_Text text, TextAlignmentOptions alignment)
        {
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = alignment;
        }

        static void TintColours(Button button)
        {
            ColorBlock colours = button.colors;
            colours.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colours.highlightedColor = Color.white;
            colours.pressedColor = new Color(0.72f, 0.7f, 0.66f, 1f);
            colours.selectedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colours.disabledColor = Color.white;
            colours.colorMultiplier = 1f;
            button.colors = colours;
        }
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

        static void Top(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -height);
            rect.offsetMax = Vector2.zero;
        }

        static Image Img(RectTransform rect, Sprite sprite, Color colour, Image.Type type = Image.Type.Simple, float pixelsPerUnit = 1f)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.type = sprite != null ? type : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = pixelsPerUnit;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, Color colour, string text) =>
            TextOn(Rect(name, parent).gameObject, font, size, colour, text);

        static TMP_Text TextOn(GameObject go, TMP_FontAsset font, float size, Color colour, string text)
        {
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
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

        static LayoutElement Flexible(GameObject go, float width, float height = -1f)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            element.flexibleWidth = width;
            if (height >= 0f) element.flexibleHeight = height;
            return element;
        }

        static void Fixed(GameObject go, float width, float height)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            if (width >= 0f) { element.minWidth = width; element.preferredWidth = width; element.flexibleWidth = 0f; }
            if (height >= 0f) { element.minHeight = height; element.preferredHeight = height; element.flexibleHeight = 0f; }
            ((RectTransform)go.transform).sizeDelta = new Vector2(width >= 0f ? width : 100f, height >= 0f ? height : 30f);
        }

        static void Ref(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"CollectionCodexBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.objectReferenceValue = value;
        }

        static void Refs(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"CollectionCodexBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        #endregion
    }
}
