using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TJ.Spells.EditorTools
{
    /// <summary>
    /// Generates the spell wheel (the compass seal: three diamond spell mounts, Cancel, a needle and a name
    /// ribbon) and its slot part, and installs it in TavernBattle.unity. Rebuilding keeps hand edits to the slot part.
    /// </summary>
    public static class SpellWheelBuilder
    {
        public const string Folder = "Assets/Data/Prefabs/UI/Spells/Spell Wheel";
        public const string WheelPath = Folder + "/Spell Wheel.prefab";
        public const string SlotPath = Folder + "/Spell Wheel Slot.prefab";
        const string ScenePath = "Assets/Scenes/TavernBattle.unity";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";
        const string LockedBlockerPath = "Assets/Data/Prefabs/UI/Reuseable/Locked Blockers/Locked Blocker - Metaprogression.prefab";

        #region Layout
        const float SealSize = 236f;
        const float SlotDistance = 70f;
        const float CancelDistance = 72f;
        const float MountSize = 54f;
        const float CancelSize = 40f;
        const float HubSize = 56f;
        const float RibbonOffset = 148f;
        static readonly Vector2 RibbonSize = new(240f, 36f);
        #endregion

        #region Style
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color NeedleGold = Hex("D9B25E");
        static readonly Color Slate = Hex("1F2B2E");
        static readonly Color RimGap = Hex("1A2427");
        static readonly Color Well = Hex("162023");
        static readonly Color Red = Hex("E3695E");
        static readonly Color GemEdge = Hex("0D1418");
        static readonly Color GemBlue = Hex("2A78C8");
        static readonly Color TailGrey = Hex("3A4A50");
        static readonly Color BossDark = Hex("2A2310");
        static readonly Color BossGold = Hex("D4AE5C");
        #endregion

        static TMP_FontAsset displayDrop, display;
        static Sprite solid, panel, circle, roundedFill, flatShadow, radialShadow, triangle, cancelGlyph;
        static GameObject lockedBlocker;

        [MenuItem("Tabletop Tavern/Spell Wheel/Rebuild Prefab")]
        public static void RebuildPrefab() => Build(false);

        [MenuItem("Tabletop Tavern/Spell Wheel/Reset Part Prefabs")]
        public static void ResetParts() => Build(true);

        static void Build(bool resetParts)
        {
            LoadAssets();
            EnsureFolder();
            GameObject slot = resetParts ? null : AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath);
            if (slot == null) slot = SavePrefab(SlotPath, "Spell Wheel Slot", SlotPart);
            SavePrefab(WheelPath, "Spell Wheel", root => WheelPrefab(root, slot));
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tabletop Tavern/Spell Wheel/Install In TavernBattle")]
        public static void Install()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WheelPath);
            if (prefab == null) { Debug.LogError("SpellWheelBuilder: build the prefab first."); return; }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SpellManager manager = FindInScene<SpellManager>(scene);
                if (manager == null) { Debug.LogError("SpellWheelBuilder: no SpellManager in TavernBattle."); return; }
                var so = new SerializedObject(manager);
                SerializedProperty buttons = so.FindProperty("spellCastButtons");
                var hotbar = buttons.arraySize > 0 ? buttons.GetArrayElementAtIndex(0).objectReferenceValue as Component : null;
                Canvas hotbarCanvas = hotbar != null ? hotbar.GetComponentInParent<Canvas>(true) : null;
                if (hotbarCanvas == null) { Debug.LogError("SpellWheelBuilder: the hotbar has no canvas to install under."); return; }
                Transform parent = hotbarCanvas.rootCanvas.transform;

                SpellWheelView view = parent.GetComponentInChildren<SpellWheelView>(true);
                if (view == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    instance.transform.SetAsLastSibling();
                    view = instance.GetComponent<SpellWheelView>();
                    Debug.Log($"SpellWheelBuilder: installed under {parent.name}.");
                }
                SerializedProperty field = so.FindProperty("spellWheel");
                if (field == null) { Debug.LogError("SpellWheelBuilder: SpellManager has no spellWheel field."); return; }
                field.objectReferenceValue = view;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("SpellWheelBuilder: SpellManager.spellWheel wired and TavernBattle saved.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        #region Wheel
        static void WheelPrefab(GameObject root, GameObject slotPrefab)
        {
            RectTransform rootRect = Stretch((RectTransform)root.transform);
            Canvas canvas = root.AddComponent<Canvas>();
            SpellWheelView view = root.AddComponent<SpellWheelView>();

            RectTransform wheel = Rect("Wheel", rootRect);
            Centre(wheel, 0f, 0f);

            Image shadow = Img(Rect("Shadow", wheel), radialShadow, new Color(0f, 0f, 0f, 0.7f));
            Centre(shadow.rectTransform, SealSize + 64f, SealSize + 64f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            Circle(wheel, "Rim", SealSize, Brass);
            Circle(wheel, "Rim Gap", SealSize - 6f, RimGap);
            Circle(wheel, "Inner Rule", SealSize - 16f, A(Brass, 0.45f));
            Circle(wheel, "Face", SealSize - 18f, Slate);

            RectTransform rays = Rect("Rays", wheel);
            Centre(rays, 0f, 0f);
            foreach (float angle in new[] { 45f, 135f, 225f, 315f })
            {
                Vector2 along = new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Image ray = Img(Rect("Ray", rays), solid, A(Brass, 0.22f));
                ray.rectTransform.pivot = new Vector2(0f, 0.5f);
                ray.rectTransform.sizeDelta = new Vector2(70f, 1f);
                ray.rectTransform.anchoredPosition = along * 30f;
                ray.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
                Image stud = Img(Rect("Stud", rays), solid, A(Brass, 0.7f));
                Centre(stud.rectTransform, 8f, 8f);
                stud.rectTransform.anchoredPosition = along * 103f;
                stud.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            }
            Circle(wheel, "Hub Ring", HubSize, A(Brass, 0.35f));
            Circle(wheel, "Hub", HubSize - 2f, Slate);

            var slots = new List<Object>();
            foreach ((string name, Vector2 at) in new[] { ("Slot 1 Left", Vector2.left), ("Slot 2 Up", Vector2.up), ("Slot 3 Right", Vector2.right) })
            {
                var slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, wheel);
                slot.name = name;
                ((RectTransform)slot.transform).anchoredPosition = at * SlotDistance;
                slots.Add(slot.GetComponent<SpellWheelSlot>());
            }

            RectTransform cancel = Rect("Cancel", wheel);
            Centre(cancel, CancelSize * 1.42f, CancelSize * 1.42f);
            cancel.anchoredPosition = new Vector2(0f, -CancelDistance);
            Image cancelGlow = Glow(cancel, CancelSize + 28f, Red);
            Image cancelEdge = DiamondLayer(cancel, "Edge", CancelSize, Red);
            Image cancelFill = DiamondLayer(cancel, "Fill", CancelSize - 3f, Hex("2A1618"));
            Image cancelIcon = Img(Rect("Icon", cancel), cancelGlyph, Red);
            cancelIcon.preserveAspect = true;
            Centre(cancelIcon.rectTransform, 18f, 18f);

            RectTransform needle = Rect("Needle", wheel);
            Centre(needle, 0f, 0f);
            // The triangle art points down and fills about the middle half of its square, so the point is
            // turned to face up and drawn larger than the needle it shows (about 12 x 40 visible).
            Image point = Img(Rect("Point", needle), triangle, NeedleGold);
            point.rectTransform.anchorMin = point.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            point.rectTransform.pivot = new Vector2(0.5f, 1f);
            point.rectTransform.sizeDelta = new Vector2(24f, 72f);
            point.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            point.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            Image tail = Img(Rect("Tail", needle), triangle, TailGrey);
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            tail.rectTransform.pivot = new Vector2(0.5f, 1f);
            tail.rectTransform.sizeDelta = new Vector2(18f, 24f);
            tail.rectTransform.anchoredPosition = new Vector2(0f, 1f);
            needle.gameObject.SetActive(false);
            Circle(wheel, "Boss Rim", 20f, BossDark);
            Circle(wheel, "Boss", 18f, BossGold);

            RectTransform ribbon = Rect("Ribbon", wheel);
            Centre(ribbon, RibbonSize.x, RibbonSize.y);
            ribbon.anchoredPosition = new Vector2(0f, -RibbonOffset);
            Image band = Img(Stretch(Rect("Band", ribbon)), panel, Color.white, Image.Type.Sliced);
            band.pixelsPerUnitMultiplier = 1f;
            foreach (float side in new[] { -1f, 1f })
            {
                Image end = Img(Rect(side < 0f ? "End Left" : "End Right", ribbon), solid, Brass);
                Centre(end.rectTransform, 10f, 10f);
                end.rectTransform.anchoredPosition = new Vector2(side * RibbonSize.x * 0.5f, 0f);
                end.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            }
            TMP_Text ribbonText = Text("Name", ribbon, displayDrop, 18f, Gold, "Hunter's Mark");
            Centre(ribbonText.rectTransform, RibbonSize.x - 30f, RibbonSize.y - 4f);
            ribbonText.alignment = TextAlignmentOptions.Center;
            ribbonText.enableAutoSizing = true;
            ribbonText.fontSizeMin = 12f;
            ribbonText.fontSizeMax = 18f;
            ribbon.gameObject.SetActive(false);

            var so = new SerializedObject(view);
            Ref(so, "canvas", canvas);
            Ref(so, "wheel", wheel);
            Refs(so, "slots", slots.ToArray());
            Ref(so, "cancel", cancel);
            Ref(so, "cancelGlow", cancelGlow);
            Ref(so, "cancelEdge", cancelEdge);
            Ref(so, "cancelFill", cancelFill);
            Ref(so, "cancelIcon", cancelIcon);
            Ref(so, "needle", needle);
            Ref(so, "ribbon", ribbon.gameObject);
            Ref(so, "ribbonText", ribbonText);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        #region Slot part
        static void SlotPart(GameObject root)
        {
            RectTransform rect = (RectTransform)root.transform;
            Centre(rect, MountSize * 1.42f, MountSize * 1.42f);
            SpellWheelSlot slot = root.AddComponent<SpellWheelSlot>();

            Image glow = Glow(rect, MountSize + 32f, Gold);
            Image edge = DiamondLayer(rect, "Edge", MountSize, Brass);
            Image fill = DiamondLayer(rect, "Fill", MountSize - 4f, Well);
            Mask mask = fill.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            Image cooldown = Img(Rect("Cooldown", fill.rectTransform), solid, new Color(0f, 0f, 0f, 0.78f), Image.Type.Filled);
            Centre(cooldown.rectTransform, MountSize * 1.42f, MountSize * 1.42f);
            cooldown.rectTransform.localEulerAngles = new Vector3(0f, 0f, -45f);
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            cooldown.fillClockwise = false;
            cooldown.fillAmount = 0f;

            Image icon = Img(Rect("Icon", rect), null, Color.white);
            icon.preserveAspect = true;
            Centre(icon.rectTransform, 30f, 30f);

            TMP_Text cooldownText = Text("Cooldown Text", rect, displayDrop, 19f, Color.white, "");
            Centre(cooldownText.rectTransform, 40f, 30f);
            cooldownText.alignment = TextAlignmentOptions.Center;

            RectTransform lockArea = Rect("Lock", rect);
            Centre(lockArea, 40f, 40f);
            GameObject blocker = lockedBlocker != null ? (GameObject)PrefabUtility.InstantiatePrefab(lockedBlocker, lockArea) : null;
            if (blocker != null) blocker.SetActive(false);

            RectTransform gem = Rect("Cost Gem", rect);
            Centre(gem, 18f, 18f);
            gem.anchoredPosition = new Vector2(21f, 21f);
            DiamondLayer(gem, "Edge", 18f, GemEdge);
            Image gemFill = DiamondLayer(gem, "Fill", 14f, GemBlue);
            TMP_Text cost = Text("Cost", gem, display, 11f, Color.white, "2");
            Centre(cost.rectTransform, 18f, 18f);
            cost.alignment = TextAlignmentOptions.Center;
            cost.fontStyle = FontStyles.Bold;

            var so = new SerializedObject(slot);
            Ref(so, "glow", glow);
            Ref(so, "edge", edge);
            Ref(so, "fill", fill);
            Ref(so, "cooldown", cooldown);
            Ref(so, "cooldownText", cooldownText);
            Ref(so, "icon", icon);
            Ref(so, "costGem", gem.gameObject);
            Ref(so, "costGemFill", gemFill);
            Ref(so, "costText", cost);
            Ref(so, "lockedBlocker", blocker);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        #region Widgets
        static Image Circle(RectTransform parent, string name, float size, Color colour)
        {
            Image image = Img(Rect(name, parent), circle, colour);
            Centre(image.rectTransform, size, size);
            return image;
        }

        // A square turned 45 degrees, with the pack's rounded corners.
        static Image DiamondLayer(RectTransform parent, string name, float size, Color colour)
        {
            Image image = Img(Rect(name, parent), roundedFill, colour, Image.Type.Sliced);
            image.pixelsPerUnitMultiplier = 20f;
            Centre(image.rectTransform, size, size);
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            return image;
        }

        static Image Glow(RectTransform parent, float size, Color colour)
        {
            Image image = Img(Rect("Glow", parent), flatShadow, colour, Image.Type.Sliced);
            image.pixelsPerUnitMultiplier = 1f;
            Centre(image.rectTransform, size, size);
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            image.enabled = false;
            return image;
        }
        #endregion

        #region Assets
        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            var sheet = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite) sheet[sprite.name] = sprite;
            sheet.TryGetValue("TooltipSolid", out solid);
            sheet.TryGetValue("TooltipPanel", out panel);
            if (solid == null || panel == null) Debug.LogError("SpellWheelBuilder: tooltip sheet sprites missing.");
            circle = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Border/Radial/256px/Radial Filled 256px.png");
            roundedFill = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Border/Rounded/256px/Rounded Filled 256px.png");
            flatShadow = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Shadow/Flat Shadow.png");
            radialShadow = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Shadow/Radial Shadow.png");
            triangle = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Triangle_Small01_Clean.png");
            cancelGlyph = Load<Sprite>("Assets/Synty/InterfaceFantasyMenus/Sprites/Icons_Menu/ICON_FantasyMenus_Menu_Cancel_01_Clean.png");
            lockedBlocker = Load<GameObject>(LockedBlockerPath);
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"SpellWheelBuilder: missing {typeof(T).Name} at {path}");
            return asset;
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data/Prefabs/UI/Spells", "Spell Wheel");
        }

        static GameObject SavePrefab(string path, string name, Action<GameObject> build)
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
                RecordOverrides(root);
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"SpellWheelBuilder: wrote {path}");
                return asset;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(stage);
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

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
        #endregion

        #region Primitives
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

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
            label.alignment = TextAlignmentOptions.Center;
            return label;
        }

        static void Ref(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"SpellWheelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.objectReferenceValue = value;
        }

        static void Refs(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"SpellWheelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        #endregion
    }
}
