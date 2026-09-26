using System;
using System.Collections.Generic;
using System.Linq;
using Memori.Audio;
using Memori.Input;
using Memori.UI;
using Memori.Utilities;
using TJ.MainMenu;
using TJ.MainMenu.EditorTools;
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

namespace TJ.Settings.EditorTools
{
    /// <summary>
    /// Builds the Settings row prefabs and lays the Settings screen out in Core.unity from them, the Collection parts
    /// and the Button Base family. A part is created only when it is missing, so hand edits to parts survive.
    /// </summary>
    public static class SettingsCodexBuilder
    {
        public const string PartFolder = "Assets/Data/Prefabs/UI/Settings";
        const string ButtonFolder = "Assets/Data/Prefabs/UI/Reuseable/Buttons";
        const string ScenePath = "Assets/Scenes/Core.unity";
        const string TableName = "MainLocalizationTable";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";
        // Height of the shared baseline above the page header's rule, as in the Collection.
        const float HeaderBaseline = 22f;

        #region Style
        static readonly Color Ground = Hex("121B1D");
        static readonly Color HeaderFill = Hex("0C1315");
        static readonly Color Slate = Hex("1F2B2E");
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        static readonly Color Sub = Hex("B4AA94");
        static readonly Color Muted = Hex("8E9A9A");
        static readonly Color Line = Hex("2C3A3D");
        static readonly Color Focus = Hex("3FB6E0");
        static readonly Color Well = Hex("0E171A");
        static readonly Color ControlFill = Hex("1B2E36");
        static readonly Color Chosen = Hex("2A5B6F");
        static readonly Color ArrowTint = Hex("9FC3D0");
        static readonly Color ListText = Hex("C4CDD1");
        static readonly Color RailIcon = Hex("B4AA94");
        static readonly Color ChosenText = Hex("EDF0F2");
        const string HoverSoundPath = "Assets/Scripts/Memori.Audio/SOs/Button Hover - SFXReference.asset";

        static TMP_FontAsset displayDrop, body;
        static Sprite panel, frame, arrow, edgeFade;
        static Sprite iconGame, iconAudio, iconGraphics, iconControls, iconGuide, iconCredits, iconCollection, iconBug, iconDev;

        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            body = Load<TMP_FontAsset>("Assets/Synty/InterfaceFantasyMenus/Fonts/Alegreya Sans/AlegreyaSans-Medium SDF.asset");
            var sheet = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite) sheet[sprite.name] = sprite;
            sheet.TryGetValue("TooltipPanel", out panel);
            if (panel == null) Debug.LogError("SettingsCodexBuilder: tooltip sheet sprites missing.");
            frame = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Frame_Box_Small03.png");
            arrow = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Arrow02.png");
            edgeFade = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Shadow/Vertical Shadow.png");
            iconGame = Load<Sprite>("Assets/Art/Icons/UI/settings.png");
            iconAudio = Load<Sprite>("Assets/Art/Icons/UI/speaker.png");
            iconGraphics = Load<Sprite>("Assets/Art/Icons/UI/television.png");
            iconControls = Load<Sprite>("Assets/Art/Icons/UI/Input/ICON_FantasyWarrior_Input_PC_Small_Clean.png");
            iconGuide = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/Icons_Inventory/ICON_FantasyWarrior_Inventory_Spellbooks01_Clean.png");
            iconCredits = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Star01_Clean.png");
            iconCollection = Load<Sprite>("Assets/Synty/InterfaceFantasyMenus/Sprites/Icons_Menu/ICON_FantasyMenus_Menu_Trophy_01_Clean.png");
            iconBug = Load<Sprite>("Assets/Synty/InterfaceFantasyMenus/Sprites/Icons_Menu/ICON_FantasyMenus_Menu_Notification_01_Clean.png");
            iconDev = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/Icons_Inventory/ICON_FantasyWarrior_Inventory_Hammers01_Clean.png");
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"SettingsCodexBuilder: missing {typeof(T).Name} at {path}");
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
            if (entry == null) throw new InvalidOperationException($"SettingsCodexBuilder: no localization key '{key}'.");
            return entry.Id;
        }

        static string English(string key)
        {
            StringTableEntry entry = english.GetEntry(key);
            return entry != null ? entry.Value : key;
        }

        // A LocalizeStringEvent that writes into the text, the way the Button Base label is wired.
        static LocalizeStringEvent AddLocalizer(TMP_Text text)
        {
            LocalizeStringEvent localizer = text.gameObject.AddComponent<LocalizeStringEvent>();
            var setter = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), text, typeof(TMP_Text).GetProperty("text").GetSetMethod());
            UnityEventTools.AddPersistentListener(localizer.OnUpdateString, setter);
            return localizer;
        }

        // Points the text's localizer at a key, adding one when the text has none. Shows the English value in the Editor.
        static void Localize(TMP_Text text, string key)
        {
            LocalizeStringEvent localizer = text.GetComponent<LocalizeStringEvent>();
            if (localizer == null) localizer = AddLocalizer(text);
            var so = new SerializedObject(localizer);
            so.FindProperty("m_StringReference.m_TableReference.m_TableCollectionName").stringValue = "GUID:" + collection.SharedData.TableCollectionNameGuid.ToString("N");
            so.FindProperty("m_StringReference.m_TableEntryReference.m_KeyId").longValue = KeyId(key);
            so.FindProperty("m_StringReference.m_TableEntryReference.m_Key").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            text.text = English(key);
        }
        #endregion

        #region Entry points
        [MenuItem("Tabletop Tavern/Settings/Build Parts")]
        public static void BuildParts()
        {
            LoadAssets();
            LoadTable();
            EnsureParts(false);
        }

        [MenuItem("Tabletop Tavern/Settings/Check References")]
        public static void CheckReferencesMenu()
        {
            Context context = FindContext();
            string report = CheckReferences(context);
            Debug.Log(string.IsNullOrEmpty(report) ? "SettingsCodexBuilder: no outside references into the old Settings UI." : "SettingsCodexBuilder: outside references found:\n" + report);
        }

        [MenuItem("Tabletop Tavern/Settings/Build Screen")]
        public static void BuildScreenMenu()
        {
            string result = BuildScreen();
            Debug.Log("SettingsCodexBuilder: " + result);
        }
        #endregion

        #region Component prefabs
        static readonly Dictionary<string, GameObject> parts = new();

        // Built in this order, so a part can nest the parts above it. A part with a base is saved as a variant of it.
        static readonly (string Name, string Base, Action<GameObject> Build)[] PartList =
        {
            ("Setting Toggle", null, TogglePart),
            ("Setting Stepper", null, StepperPart),
            ("Setting Dropdown", null, DropdownPart),
            ("Setting Slider", null, SliderPart),
            ("Key Cap", null, KeyCapPart),
            ("Setting Row", null, RowPart),
            ("Setting Row - Toggle", "Setting Row", go => NestControl(go, "Setting Toggle")),
            ("Setting Row - Choice", "Setting Row", go => NestControl(go, "Setting Stepper")),
            ("Setting Row - List", "Setting Row", go => NestControl(go, "Setting Dropdown")),
            ("Setting Row - Slider", "Setting Row", go => NestControl(go, "Setting Slider")),
            ("Setting Row - Key", "Setting Row", KeyRowPart),
        };

        static string PartPath(string name) => $"{PartFolder}/{name}.prefab";

        static void EnsureParts(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(PartFolder)) AssetDatabase.CreateFolder("Assets/Data/Prefabs/UI", "Settings");
            parts.Clear();
            foreach ((string name, string basePart, Action<GameObject> build) in PartList)
            {
                GameObject asset = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(PartPath(name));
                parts[name] = asset != null ? asset : SavePart(name, basePart, build);
            }
        }

        static GameObject SavePart(string name, string basePart, Action<GameObject> build)
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
                Debug.Log($"SettingsCodexBuilder: wrote {PartPath(name)}");
                return asset;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        static void TogglePart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Fixed(go, 132f, 36f);
            Image well = Img(rect, null, Well);
            well.raycastTarget = true;
            Toggle toggle = go.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = well;
            toggle.isOn = false;
            Button off = Half(rect, "Off", 0f, 0.5f, "settingsOff", out Image offFill, out TMP_Text offLabel);
            Button on = Half(rect, "On", 0.5f, 1f, "settingsOn", out Image onFill, out TMP_Text onLabel);
            FrameOn(rect);
            go.AddComponent<SettingFocusRelay>();
            SettingToggleSwitch toggleSwitch = go.AddComponent<SettingToggleSwitch>();
            var so = new SerializedObject(toggleSwitch);
            Ref(so, "toggle", toggle);
            Ref(so, "offHalf", off);
            Ref(so, "onHalf", on);
            Ref(so, "offFill", offFill);
            Ref(so, "onFill", onFill);
            Ref(so, "offLabel", offLabel);
            Ref(so, "onLabel", onLabel);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Button Half(RectTransform parent, string name, float from, float to, string key, out Image fill, out TMP_Text label)
        {
            RectTransform half = Rect(name, parent);
            Anchor(half, new Vector2(from, 0f), new Vector2(to, 1f), new Vector2(from == 0f ? 3f : 0f, 3f), new Vector2(to == 1f ? -3f : 0f, -3f));
            Image hit = Img(half, null, Color.clear);
            hit.raycastTarget = true;
            Button button = half.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            NoNavigation(button);
            fill = Img(Stretch(Rect("Fill", half)), null, Chosen);
            fill.enabled = false;
            label = Text("Label", half, displayDrop, 16f, Muted, English(key));
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            Localize(label, key);
            return button;
        }

        static void StepperPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Fixed(go, 190f, 36f);
            go.GetComponent<LayoutElement>().minWidth = 150f;
            Image fill = Img(rect, null, ControlFill);
            fill.raycastTarget = true;
            Button previous = ArrowButton(rect, "Previous", true);
            Button next = ArrowButton(rect, "Next", false);
            TMP_Text value = Text("Value", rect, displayDrop, 18f, Cream, "100%");
            Stretch(value.rectTransform, 36f, 36f);
            value.alignment = TextAlignmentOptions.Center;
            FrameOn(rect);
            SettingStepper stepper = go.AddComponent<SettingStepper>();
            stepper.transition = Selectable.Transition.None;
            stepper.targetGraphic = fill;
            var so = new SerializedObject(stepper);
            Ref(so, "m_CaptionText", value);
            Ref(so, "previousButton", previous);
            Ref(so, "nextButton", next);
            so.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<SettingFocusRelay>();
        }

        static Button ArrowButton(RectTransform parent, string name, bool left)
        {
            RectTransform rect = Rect(name, parent);
            Anchor(rect, new Vector2(left ? 0f : 1f, 0f), new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 0f : -36f, 0f), new Vector2(left ? 36f : 0f, 0f));
            Image hit = Img(rect, null, Color.clear);
            hit.raycastTarget = true;
            Image glyph = Img(Rect("Arrow", rect), arrow, ArrowTint);
            Centre(glyph.rectTransform, 14f, 14f);
            glyph.preserveAspect = true;
            // The sprite points right; the previous arrow mirrors it.
            if (left) glyph.rectTransform.localEulerAngles = new Vector3(0f, 180f, 0f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = glyph;
            ColorBlock colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colours.selectedColor = Color.white;
            colours.disabledColor = new Color(1f, 1f, 1f, 0.25f);
            button.colors = colours;
            NoNavigation(button);
            return button;
        }

        static void DropdownPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Fixed(go, 260f, 36f);
            go.GetComponent<LayoutElement>().minWidth = 180f;
            Image fill = Img(rect, null, ControlFill);
            fill.raycastTarget = true;
            TMP_Text caption = Text("Label", rect, displayDrop, 17f, Cream, "1920x1080");
            Stretch(caption.rectTransform, 14f, 36f);
            Image glyph = Img(Rect("Arrow", rect), arrow, ArrowTint);
            Anchor(glyph.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-29f, -7f), new Vector2(-15f, 7f));
            glyph.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
            glyph.preserveAspect = true;
            FrameOn(rect);

            // The list: the tooltip panel, the Ink and Brass look shared with every hover card.
            RectTransform template = Rect("Template", rect);
            Anchor(template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -244f), new Vector2(0f, -4f));
            template.pivot = new Vector2(0.5f, 1f);
            Img(template, panel, Color.white, Image.Type.Sliced, 1f).raycastTarget = true;
            ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Stretch(Rect("Viewport", template), 6f, 12f, 6f, 6f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Content", viewport);
            Anchor(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -34f), Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            RectTransform item = Rect("Item", content);
            Anchor(item, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -17f), new Vector2(0f, 17f));
            Image itemBackground = Img(Stretch(Rect("Item Background", item)), null, Focus);
            itemBackground.raycastTarget = true;
            Image mark = Img(Rect("Item Checkmark", item), null, Focus);
            Anchor(mark.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f));
            TMP_Text itemLabel = Text("Item Label", item, displayDrop, 16f, ListText, "Option");
            Stretch(itemLabel.rectTransform, 16f, 8f);
            Toggle itemToggle = item.gameObject.AddComponent<Toggle>();
            itemToggle.targetGraphic = itemBackground;
            itemToggle.graphic = mark;
            itemToggle.isOn = true;
            ColorBlock colours = itemToggle.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = new Color(1f, 1f, 1f, 0.22f);
            colours.pressedColor = new Color(1f, 1f, 1f, 0.3f);
            colours.selectedColor = new Color(1f, 1f, 1f, 0.22f);
            colours.colorMultiplier = 1f;
            itemToggle.colors = colours;
            Scrollbar bar = ListScrollbar(template);
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            template.gameObject.SetActive(false);

            TMP_Dropdown dropdown = go.AddComponent<TMP_Dropdown>();
            dropdown.transition = Selectable.Transition.None;
            dropdown.targetGraphic = fill;
            dropdown.captionText = caption;
            dropdown.template = template;
            dropdown.itemText = itemLabel;
            ClickSound(go.AddComponent<SettingFocusRelay>());
        }

        static Scrollbar ListScrollbar(RectTransform parent)
        {
            RectTransform barRect = Rect("Scrollbar", parent);
            Anchor(barRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-10f, 8f), new Vector2(-6f, -8f));
            Img(barRect, null, A(Brass, 0.08f)).raycastTarget = true;
            RectTransform slidingArea = Stretch(Rect("Sliding Area", barRect));
            RectTransform handle = Stretch(Rect("Handle", slidingArea));
            Image handleImage = Img(handle, null, A(Brass, 0.5f));
            handleImage.raycastTarget = true;
            Scrollbar scrollbar = barRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            NoNavigation(scrollbar);
            return scrollbar;
        }

        static void SliderPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Fixed(go, 356f, 36f);
            go.GetComponent<LayoutElement>().minWidth = 220f;
            HorizontalLayoutGroup row = HLayout(rect, 16f, TextAnchor.MiddleRight, new RectOffset());
            row.childForceExpandHeight = false;
            RectTransform sliderRect = Rect("Slider", rect);
            Fixed(sliderRect.gameObject, 280f, 24f);
            LayoutElement sliderElement = sliderRect.GetComponent<LayoutElement>();
            sliderElement.minWidth = 120f;
            sliderElement.flexibleWidth = 1f;
            Image edge = Img(Rect("Track Edge", sliderRect), null, A(Hex("7B8EA5"), 0.45f));
            Anchor(edge.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(0f, 4f));
            Image track = Img(Rect("Track", sliderRect), null, Hex("0A1215"));
            Anchor(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, -3f), new Vector2(-1f, 3f));
            RectTransform fillArea = Rect("Fill Area", sliderRect);
            Anchor(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, -3f), new Vector2(-1f, 3f));
            Image fill = Img(Rect("Fill", fillArea), null, Focus);
            Anchor(fill.rectTransform, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            RectTransform handleArea = Stretch(Rect("Handle Slide Area", sliderRect), 9f, 9f);
            RectTransform handle = Rect("Handle", handleArea);
            Anchor(handle, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-9f, 3f), new Vector2(9f, -3f));
            Image rim = Img(Rect("Rim", handle), null, Hex("1C2A33"));
            Centre(rim.rectTransform, 18f, 18f);
            rim.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Image face = Img(Rect("Face", handle), null, Hex("DCE9EF"));
            Centre(face.rectTransform, 13f, 13f);
            face.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            face.raycastTarget = true;
            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.targetGraphic = face;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;
            ColorBlock colours = slider.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colours.colorMultiplier = 1f;
            slider.colors = colours;
            track.raycastTarget = true;
            sliderRect.gameObject.AddComponent<SettingFocusRelay>();
            TMP_Text value = Text("Value", rect, displayDrop, 17f, Cream, "50%");
            value.alignment = TextAlignmentOptions.MidlineRight;
            Fixed(value.gameObject, 60f, 36f);
        }

        // The toggle's look: a dark well under the steel frame.
        static void KeyCapPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            Fixed(go, 150f, 36f);
            go.GetComponent<LayoutElement>().minWidth = 110f;
            Image well = Img(rect, null, Well);
            well.raycastTarget = true;
            Image capFrame = FrameOn(rect);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = capFrame;
            ColorBlock colours = button.colors;
            colours.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colours.highlightedColor = Color.white;
            colours.pressedColor = new Color(0.72f, 0.7f, 0.66f, 1f);
            colours.selectedColor = Color.white;
            button.colors = colours;
            TMP_Text key = Text("Key", rect, displayDrop, 16f, ChosenText, "W");
            Stretch(key.rectTransform, 8f, 8f, 2f, 4f);
            key.alignment = TextAlignmentOptions.Center;
            key.enableAutoSizing = true;
            key.fontSizeMin = 10f;
            key.fontSizeMax = 16f;
            ClickSound(go.AddComponent<SettingFocusRelay>());
        }

        static void ClickSound(SettingFocusRelay relay)
        {
            var so = new SerializedObject(relay);
            so.FindProperty("clickSound").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RowPart(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minHeight = 56f;
            element.flexibleWidth = 1f;
            CanvasGroup group = go.AddComponent<CanvasGroup>();
            // A clear graphic so the whole row takes the pointer, not just its text.
            Image hit = Img(Stretch(Rect("Hit", rect)), null, Color.clear);
            hit.raycastTarget = true;
            Image hover = Img(Stretch(Rect("Hover", rect)), null, Slate);
            hover.enabled = false;
            Image focusBar = Img(Rect("Focus Bar", rect), null, Focus);
            Anchor(focusBar.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f));
            focusBar.enabled = false;
            Image rule = Img(Rect("Rule", rect), null, new Color(1f, 1f, 1f, 0.06f));
            Anchor(rule.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            foreach (Image layer in new[] { hit, hover, focusBar, rule }) layer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            HorizontalLayoutGroup row = HLayout(rect, 24f, TextAnchor.MiddleLeft, new RectOffset(16, 14, 9, 9));
            row.childForceExpandWidth = false;
            RectTransform textColumn = Rect("Text", rect);
            VerticalLayoutGroup textLayout = VLayout(textColumn, 3f, new RectOffset());
            textLayout.childAlignment = TextAnchor.MiddleLeft;
            LayoutElement textElement = Flexible(textColumn.gameObject, 1f);
            // Keeps the label to two lines on narrow pages; the control shrinks first.
            textElement.minWidth = 160f;
            TMP_Text label = Text("Label", textColumn, displayDrop, 19f, Cream, "Setting");
            label.textWrappingMode = TextWrappingModes.Normal;
            AddLocalizer(label);
            TMP_Text help = Text("Help", textColumn, body, 15f, Muted, "What this setting does.");
            help.textWrappingMode = TextWrappingModes.Normal;
            AddLocalizer(help);
            RectTransform control = Rect("Control", rect);
            HLayout(control, 0f, TextAnchor.MiddleRight, new RectOffset());

            SettingRow settingRow = go.AddComponent<SettingRow>();
            var so = new SerializedObject(settingRow);
            Ref(so, "hoverFill", hover);
            Ref(so, "focusBar", focusBar);
            Ref(so, "label", label);
            Ref(so, "content", group);
            so.FindProperty("labelColour").colorValue = Cream;
            so.ApplyModifiedPropertiesWithoutUndo();

            var hoverSound = new SerializedObject(go.AddComponent<UIHoverSFX>());
            Ref(hoverSound, "sfxReference", Load<SFXReference>(HoverSoundPath));
            hoverSound.ApplyModifiedPropertiesWithoutUndo();
        }

        static void NestControl(GameObject row, string part)
        {
            Transform slot = row.transform.Find("Control");
            var control = (GameObject)PrefabUtility.InstantiatePrefab(parts[part], slot);
            // A greyed-out row stays quiet: the hover sound follows its control's interactable state.
            var hover = new SerializedObject(row.GetComponent<UIHoverSFX>());
            Ref(hover, "interactableSource", control.GetComponentInChildren<Selectable>(true));
            hover.ApplyModifiedPropertiesWithoutUndo();
        }

        static void KeyRowPart(GameObject go)
        {
            NestControl(go, "Key Cap");
            go.transform.Find("Text/Help").gameObject.SetActive(false);
            go.GetComponent<LayoutElement>().minHeight = 46f;
            go.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(16, 14, 5, 5);
        }
        #endregion

        #region Screen data
        enum Kind { Toggle, Choice, List, Slider, Key, Button, Danger }

        sealed class RowSpec
        {
            public Kind kind;
            public string label, help;
            public string pref;
            public bool defaultOn, invert;
            public string binding;
            public string field;
            public bool inactive;
        }

        sealed class GroupSpec
        {
            public string key;
            public RowSpec[] rows;
        }

        static RowSpec R(Kind kind, string label, string help = null, string pref = null, bool on = false, bool invert = false, string field = null) =>
            new() { kind = kind, label = label, help = help, pref = pref, defaultOn = on, invert = invert, field = field };

        static RowSpec K(string label, string binding) => new() { kind = Kind.Key, label = label, binding = binding };

        // Saved keys, defaults and inversions copied from the old rows in Core.unity on 2026-09-24.
        static readonly GroupSpec[][] GamePage =
        {
            new[]
            {
                new GroupSpec { key = "settingsGroupSquadDefaults", rows = new[]
                {
                    R(Kind.Toggle, "Guard Mode", "settingsHelpGuardMode", "defaultSquadGuardMode", true),
                    R(Kind.Toggle, "CeaseFireTitle", "settingsHelpCeaseFire", "defaultSquadCeaseFire"),
                    R(Kind.Toggle, "Toggle Fire at Will", "settingsHelpFireAtWill", "defaultSquadFireMode"),
                    R(Kind.Toggle, "autoCharge", "settingsHelpAutoCharge", "autoCharge"),
                } },
                new GroupSpec { key = "Battle", rows = new[]
                {
                    R(Kind.Toggle, "AutoRollForInitiative", "settingsHelpAutoRoll", "AutoRollInitiative", field: "autoRollInitiativeToggle"),
                    R(Kind.Toggle, "HideUnitInfoBattle", "settingsHelpHideUnitInfo", "hideSquadInfoInBattle", field: "hideUnitInfoInBattleToggle"),
                } },
            },
            new[]
            {
                new GroupSpec { key = "settingsGroupCampaign", rows = new[]
                {
                    R(Kind.Toggle, "Skip Chest Animations", "settingsHelpSkipChest", "skipChestAnimations"),
                    R(Kind.Toggle, "hideDisband", "settingsHelpDisbandWarning", "DisbandSquadConfirmation", true, true),
                } },
                new GroupSpec { key = "GameScale", rows = new[]
                {
                    R(Kind.Choice, "UIScale", "settingsHelpUIScale", field: "uiScaleDropdown"),
                    R(Kind.Choice, "BattlefieldFlagsScale", "settingsHelpFlagsScale", field: "battlefieldFlagsDropdown"),
                } },
                new GroupSpec { key = "settingsGroupAdvanced", rows = new[]
                {
                    R(Kind.Toggle, "DebugSquadNavObjects", "settingsHelpDebugMode", "DebugSquadNavObjects"),
                    R(Kind.Button, "resetTutorial", "settingsHelpResetTutorial", field: "resetTutorialButton"),
                } },
                new GroupSpec { key = "settingsGroupProgress", rows = new[]
                {
                    R(Kind.Danger, "deleteSave", "settingsHelpDeleteProgress", field: "deleteProgressButton"),
                } },
            },
        };

        static readonly GroupSpec[][] AudioPage =
        {
            new[]
            {
                new GroupSpec { key = "AudioVolume", rows = new[]
                {
                    R(Kind.Slider, "Master", field: "masterVolumeSlider"),
                    R(Kind.Slider, "Voices", field: "voicesVolumeSlider"),
                    R(Kind.Slider, "Effects", field: "effectsVolumeSlider"),
                    R(Kind.Slider, "Interface", field: "interfaceVolumeSlider"),
                    R(Kind.Slider, "Music", field: "bGMVolumeSlider"),
                } },
            },
            Array.Empty<GroupSpec>(),
        };

        static readonly GroupSpec[][] GraphicsPage =
        {
            new[]
            {
                new GroupSpec { key = "settingsGroupDisplay", rows = new[]
                {
                    R(Kind.List, "Resolution", "settingsHelpResolution", field: "resolutionDropdown"),
                    R(Kind.List, "Refresh Rate", "settingsHelpRefreshRate", field: "refreshRateDropdown"),
                    R(Kind.Toggle, "Fullscreen", "settingsHelpFullscreen", field: "fullscreenToggle"),
                    R(Kind.Toggle, "VSync", "settingsHelpVSync", field: "vsyncToggle"),
                    R(Kind.List, "FPSLimit", "settingsHelpFPSLimit", field: "fpsLimitDropdown"),
                    R(Kind.Toggle, "Display FPS", "settingsHelpDisplayFPS", field: "fpsToggle"),
                } },
            },
            new[]
            {
                new GroupSpec { key = "settingsGroupQuality", rows = new[]
                {
                    R(Kind.Choice, "Antialiasing", "settingsHelpAntialiasing", field: "antiAliasingDropdown"),
                    R(Kind.Choice, "shadowQuality", "settingsHelpShadows", field: "shadowQualityDropdown"),
                    R(Kind.Choice, "renderScale", "settingsHelpRenderScale", field: "renderScaleDropdown"),
                    R(Kind.Choice, "textureQuality", "settingsHelpTextures", field: "textureQualityDropdown"),
                    new RowSpec { kind = Kind.Choice, label = "Quality Preset", field = "graphicsQualityDropdown", inactive = true },
                } },
                new GroupSpec { key = "settingsGroupVisualEffects", rows = new[]
                {
                    R(Kind.Toggle, "ambientOcclusion", "settingsHelpAmbientOcclusion", field: "ambientOcclusionToggle"),
                    R(Kind.Toggle, "bloom", "settingsHelpBloom", field: "bloomToggle"),
                    R(Kind.Toggle, "EnableClothSimulation", "settingsHelpCloth", "EnableClothSimulation"),
                } },
            },
        };

        static readonly GroupSpec[][] ControlsPage =
        {
            new[]
            {
                new GroupSpec { key = "Camera", rows = new[]
                {
                    R(Kind.Toggle, "invertMouseY", "settingsHelpInvertMouseY", "invertMouseY", field: "invertMouseToggle"),
                    R(Kind.Toggle, "CameraEdgePanning", "settingsHelpEdgePanning", EdgePanningToggle.PlayerPrefKey),
                    R(Kind.Toggle, "cameraShake", "settingsHelpCameraShake", "CameraShake", true, field: "cameraShakeToggle"),
                    R(Kind.Slider, "Camera Movement Speed", pref: "cameraMovementSpeed", field: "cameraMovementSpeedSlider"),
                    R(Kind.Slider, "CameraRotationSped", pref: "cameraRotationSpeed", field: "cameraRotationSpeedSlider"),
                    R(Kind.Slider, "Camera Zoom Speed", pref: "cameraZoomSpeed", field: "cameraZoomSpeedSlider"),
                } },
                new GroupSpec { key = "settingsGroupCameraKeys", rows = new[]
                {
                    K("Forward", "Forward"), K("Back", "Back"), K("Left", "Left"), K("Right", "Right"),
                    K("Speed Up", "MoveFast"), K("RaiseCamera", "RaiseCamera"), K("LowerCamera", "LowerCamera"),
                    K("rotateCameraLeft", "RotateLeft"), K("rotateCameraRight", "RotateRight"),
                    K("PitchCameraUp", "PitchCameraUp"), K("PitchCameraDown", "PitchCameraDown"),
                    K("CameraZoom", "CameraZoom"), K("EnableCameraRotation", "EnableCameraRotation"),
                } },
            },
            new[]
            {
                new GroupSpec { key = "settingsGroupOrders", rows = new[]
                {
                    K("Group", "Group"), K("AddToSelection", "AddToSelection"), K("queueMultipleOrders", "QueueOrder"),
                    K("RepositionSelectedUnits", "RepositionSelectedUnits"),
                    K("SelectGroup1", "SelectGroup1"), K("SelectGroup2", "SelectGroup2"), K("SelectGroup3", "SelectGroup3"),
                    K("SelectGroup4", "SelectGroup4"), K("SelectGroup5", "SelectGroup5"), K("SelectGroup6", "SelectGroup6"),
                    K("SelectGroup7", "SelectGroup7"), K("SelectGroup8", "SelectGroup8"), K("SelectGroup9", "SelectGroup9"),
                    K("SelectGroup10", "SelectGroup10"), K("Halt", "Halt"), K("Withdraw", "Withdraw"),
                    K("CeaseFireTitle", "CeaseFireCommand"),
                } },
                new GroupSpec { key = "settingsGroupStancesFireModes", rows = new[]
                {
                    K("Toggle Guard Mode", "ToggleGuardMode"), K("Toggle Melee Mode", "ToggleMeleeMode"),
                    K("Toggle Auto Retarget", "ToggleAutoRetarget"), K("Toggle Volley Fire", "ToggleVolleyFireMode"),
                    K("Toggle Fire at Will", "ToggleFireAtWillMode"), K("Set Balanced Stance", "SetBalancedStance"),
                    K("Set Defensive Stance", "SetDefensiveStance"),
                } },
                new GroupSpec { key = "settingsGroupGame", rows = new[]
                {
                    K("Pause", "PauseGame"), K("decreaseGameSpeed", "SpeedDown"), K("increaseGameSpeed", "SpeedUp"),
                    K("SpellMenu", "SpellMenu"), K("HideUI", "HideBattleUI"), K("ShowUnitMovement", "ShowUnitMovementFinal"),
                    K("FreeCameraMode", "ToggleFreeCameraMode"), K("Settings", "Settings"),
                } },
            },
        };
        #endregion

        #region Screen
        sealed class Context
        {
            public Scene core;
            public GameObject root;
            public Transform canvas;
            public SettingsManager manager;
            public GraphicsPanel graphics;
            public AudioSettingsPanel audio;
            public Transform gamePanel, audioPanel, graphicsPanel, controlsPanel, infoPanel, creditsPanel;
            public Transform background, sideButtons, devPanel, versionText, resetKeys, oldCredits;
            public Button quickRestart, abandonRun, exitToMenu, concede, exitToDesktop, apply, revert;
            public HashSet<Object> keep = new();
            public HashSet<Object> delete = new();
            public Dictionary<string, Slider> oldSliders = new();
        }

        static Context FindContext()
        {
            var context = new Context { core = SceneManager.GetSceneByPath(ScenePath) };
            if (!context.core.IsValid() || !context.core.isLoaded) throw new InvalidOperationException("Core.unity is not loaded.");
            foreach (GameObject go in context.core.GetRootGameObjects())
                if (go.GetComponent<SettingsManager>() != null) context.root = go;
            if (context.root == null) throw new InvalidOperationException("No SettingsManager root in Core.unity.");
            context.manager = context.root.GetComponent<SettingsManager>();
            context.graphics = context.root.GetComponent<GraphicsPanel>();
            context.audio = context.root.GetComponent<AudioSettingsPanel>();
            context.canvas = context.root.transform.Find("Settings Canvas");
            if (context.canvas.Find("Codex") != null)
                throw new InvalidOperationException("The Settings screen is already built. Edit it in Core.unity, or restyle the parts.");
            Transform C(string path)
            {
                Transform found = context.canvas.Find(path);
                if (found == null) throw new InvalidOperationException($"Settings Canvas has no '{path}'.");
                return found;
            }
            context.gamePanel = C("Game Settings Panel");
            context.audioPanel = C("Audio Panel");
            context.graphicsPanel = C("Graphics Panel");
            context.controlsPanel = C("Controls Panel");
            context.infoPanel = C("Info Panel");
            context.creditsPanel = C("Credits Panel");
            context.background = context.canvas.Find("Background");
            context.sideButtons = context.canvas.Find("Side Buttons");
            context.devPanel = context.gamePanel.Find("Game Settings/Dev Panel");
            context.resetKeys = context.controlsPanel.Find("Reset To Default Button");
            context.oldCredits = context.creditsPanel.Find("Credits");
            context.versionText = context.background != null ? FindByName(context.background, "Release Version Text") : null;
            var so = new SerializedObject(context.manager);
            context.quickRestart = (Button)so.FindProperty("quickRestartButton").objectReferenceValue;
            context.abandonRun = (Button)so.FindProperty("abandonRunButton").objectReferenceValue;
            context.exitToMenu = (Button)so.FindProperty("exitToMenuButton").objectReferenceValue;
            context.concede = (Button)so.FindProperty("concedeDefeatButton").objectReferenceValue;
            context.exitToDesktop = (Button)so.FindProperty("exitToDesktopButton").objectReferenceValue;
            var graphicsObject = new SerializedObject(context.graphics);
            context.apply = (Button)graphicsObject.FindProperty("applyVideoSettingsButton").objectReferenceValue;
            context.revert = (Button)graphicsObject.FindProperty("resetVideoSettingsButton").objectReferenceValue;

            foreach (MonitoredDataSlider slider in context.controlsPanel.GetComponentsInChildren<MonitoredDataSlider>(true))
                context.oldSliders[new SerializedObject(slider).FindProperty("playerPref").stringValue] = (Slider)new SerializedObject(slider).FindProperty("slider").objectReferenceValue;

            // Moved into the new screen, so they and everything under them stay.
            var kept = new List<Transform> { context.gamePanel, context.audioPanel, context.graphicsPanel, context.controlsPanel, context.infoPanel, context.creditsPanel };
            foreach (Transform t in kept) context.keep.Add(t.gameObject);
            foreach (Transform child in context.infoPanel) AddTree(context.keep, child);
            foreach (Transform t in new[] { context.devPanel, context.versionText, context.resetKeys, context.oldCredits })
                if (t != null) AddTree(context.keep, t);
            foreach (Button b in new[] { context.quickRestart, context.abandonRun, context.exitToMenu, context.concede, context.exitToDesktop, context.apply, context.revert })
                AddTree(context.keep, b.transform);
            foreach (Transform child in context.canvas)
                if (child.name.Contains("Pop") || child.name.Contains("pop")) AddTree(context.keep, child);

            // Everything else under the old containers goes.
            var containers = new List<Transform>();
            if (context.background != null) containers.Add(context.background);
            if (context.sideButtons != null) containers.Add(context.sideButtons);
            foreach (Transform page in new[] { context.gamePanel, context.audioPanel, context.graphicsPanel, context.controlsPanel, context.creditsPanel })
                foreach (Transform child in page) containers.Add(child);
            foreach (Transform container in containers)
                foreach (Transform t in container.GetComponentsInChildren<Transform>(true))
                    if (!context.keep.Contains(t.gameObject))
                    {
                        context.delete.Add(t.gameObject);
                        foreach (Component c in t.GetComponents<Component>()) if (c != null) context.delete.Add(c);
                    }
            return context;
        }

        static Transform FindByName(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static void AddTree(HashSet<Object> set, Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) set.Add(t.gameObject);
        }

        // The fields this builder points at the new screen. Anything else that refers into the old UI stops the build.
        static readonly HashSet<string> RewiredFields = new()
        {
            "SettingsManager.resumeGameButton", "SettingsManager.creditsButton", "SettingsManager.collectionButton",
            "SettingsManager.deleteProgressButton", "SettingsManager.infoButton", "SettingsManager.gameSettingsButton",
            "SettingsManager.audioSettingsButton", "SettingsManager.graphicsSettingsButton", "SettingsManager.controlsSettingsButton",
            "SettingsManager.hideUnitInfoInBattleToggle", "SettingsManager.cameraShakeToggle", "SettingsManager.autoRollInitiativeToggle",
            "SettingsManager.invertMouseToggle", "SettingsManager.resetTutorialButton", "SettingsManager.cameraRotationSpeedSlider",
            "SettingsManager.cameraMovementSpeedSlider", "SettingsManager.cameraZoomSpeedSlider", "SettingsManager.uiScaleDropdown",
            "SettingsManager.battlefieldFlagsDropdown",
            "GraphicsPanel.resolutionDropdown", "GraphicsPanel.refreshRateDropdown", "GraphicsPanel.graphicsQualityDropdown",
            "GraphicsPanel.antiAliasingDropdown", "GraphicsPanel.shadowQualityDropdown", "GraphicsPanel.renderScaleDropdown",
            "GraphicsPanel.textureQualityDropdown", "GraphicsPanel.fpsLimitDropdown", "GraphicsPanel.fullscreenToggle",
            "GraphicsPanel.vsyncToggle", "GraphicsPanel.fpsToggle", "GraphicsPanel.ambientOcclusionToggle", "GraphicsPanel.bloomToggle",
            "GraphicsPanel.hardwareTierLabel",
            "AudioSettingsPanel.masterVolumeSlider", "AudioSettingsPanel.voicesVolumeSlider", "AudioSettingsPanel.effectsVolumeSlider",
            "AudioSettingsPanel.interfaceVolumeSlider", "AudioSettingsPanel.bGMVolumeSlider",
        };

        static string CheckReferences(Context context)
        {
            var report = new System.Text.StringBuilder();
            foreach (GameObject root in context.core.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (context.delete.Contains(t.gameObject)) continue;
                    foreach (Component component in t.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        var so = new SerializedObject(component);
                        SerializedProperty property = so.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            // Parent and child links are resolved by the build itself, which moves or deletes both ends.
                            if (component is Transform && (property.propertyPath == "m_Father" || property.propertyPath.StartsWith("m_Children"))) continue;
                            Object target = property.objectReferenceValue;
                            if (target == null || !context.delete.Contains(target)) continue;
                            string field = component.GetType().Name + "." + property.propertyPath;
                            if (RewiredFields.Contains(field)) continue;
                            report.AppendLine($"{PathOf(t)} {field} -> {target.name} ({target.GetType().Name})");
                        }
                    }
                }
            return report.ToString();
        }

        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        public static string BuildScreen()
        {
            LoadAssets();
            LoadTable();
            EnsureParts(false);
            Context context = FindContext();
            if (context.core.isDirty) return "Core.unity has unsaved changes; save or revert them first.";
            string outside = CheckReferences(context);
            if (!string.IsNullOrEmpty(outside)) return "stopped, outside references into the old UI:\n" + outside;

            Scene previousActive = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(context.core);
            try
            {
                Build(context);
            }
            finally
            {
                SceneManager.SetActiveScene(previousActive);
            }
            EditorSceneManager.MarkSceneDirty(context.core);
            EditorSceneManager.SaveScene(context.core);
            return "built the Settings screen and saved Core.unity";
        }

        // The context of the build in progress, for the row builder's old slider ranges.
        static Context current;

        static GameObject Asset(string path) => Load<GameObject>(path);
        static GameObject CollectionPart(string name) => Asset($"{CollectionCodexBuilder.PartFolder}/{name}.prefab");

        static void Build(Context context)
        {
            current = context;
            Transform canvas = context.canvas;
            SettingsManager manager = context.manager;
            var managerObject = new SerializedObject(manager);
            var graphicsObject = new SerializedObject(context.graphics);
            var audioObject = new SerializedObject(context.audio);

            // Shell: the Collection's page, header and rail, at the Collection's sizes.
            RectTransform codex = Stretch(Rect("Codex", canvas));
            codex.SetAsFirstSibling();
            Img(Stretch(Rect("Backdrop", codex)), null, Ground).raycastTarget = true;
            EdgeShade(codex);

            RectTransform header = Rect("Header", codex);
            Top(header, 88f);
            Img(Stretch(Rect("Fill", header)), null, HeaderFill);
            Image headerRule = Img(Rect("Rule", header), null, A(Brass, 0.55f));
            Anchor(headerRule.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            TMP_Text title = Text("Title", header, displayDrop, 38f, Gold, "Settings");
            Anchor(title.rectTransform, Vector2.zero, new Vector2(0.5f, 1f), new Vector2(48f, 0f), Vector2.zero);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.characterSpacing = 2f;
            Localize(title, "Settings");
            RectTransform headerRight = Rect("Right", header);
            Anchor(headerRight, new Vector2(0.4f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-48f, 0f));
            HorizontalLayoutGroup rightRow = HLayout(headerRight, 16f, TextAnchor.MiddleRight, new RectOffset());
            rightRow.childControlHeight = false;
            var close = (GameObject)PrefabUtility.InstantiatePrefab(CollectionPart("Close Button"), headerRight);
            close.name = "Close";
            Button closeButton = close.GetComponent<Button>();
            TMP_Text closeLabel = close.transform.Find("Label").GetComponent<TMP_Text>();
            closeLabel.text = English("Close");

            RectTransform rail = Rect("Rail", codex);
            Anchor(rail, Vector2.zero, new Vector2(0f, 1f), new Vector2(48f, 40f), new Vector2(328f, -122f));
            VerticalLayoutGroup railLayout = VLayout(rail, 0f, new RectOffset());
            railLayout.childForceExpandWidth = true;

            RectTransform content = Rect("Content", codex);
            Stretch(content, 360f, 48f, 122f, 40f);

            // Rail rows are the Collection's Rail Row, without its count, bar and new-dot.
            RailSection(rail, "settingsRailOptions");
            CollectionRailRow gameRow = RailRow(rail, "Game", "settingsGroupGame", iconGame);
            CollectionRailRow audioRow = RailRow(rail, "Audio", "tabAudio", iconAudio);
            CollectionRailRow graphicsRow = RailRow(rail, "Graphics", "Graphics", iconGraphics);
            CollectionRailRow controlsRow = RailRow(rail, "Controls", "Controls", iconControls);
            RailSection(rail, "settingsRailMore");
            CollectionRailRow guideRow = RailRow(rail, "Battle Guide", "Guide_Title", iconGuide);
            CollectionRailRow creditsRow = RailRow(rail, "Credits", "creditsButton", iconCredits);
            CollectionRailRow collectionRow = RailRow(rail, "Collection", "collectionButton", iconCollection);
            CollectionRailRow bugRow = RailRow(rail, "Report a Bug", "Report a Bug", iconBug);
            CollectionRailRow devRow = RailRow(rail, "Dev Tools", "settingsDevTools", iconDev);
            ReportABugButton reportBug = bugRow.gameObject.AddComponent<ReportABugButton>();
            var reportObject = new SerializedObject(reportBug);
            Ref(reportObject, "reportBugButton", bugRow.Button);
            reportObject.ApplyModifiedPropertiesWithoutUndo();

            RectTransform railSpacer = Rect("Spacer", rail);
            Flexible(railSpacer.gameObject, 0f, 1f);
            RectTransform session = Rect("Session", rail);
            VerticalLayoutGroup sessionLayout = VLayout(session, 10f, new RectOffset(0, 0, 12, 0));
            sessionLayout.childForceExpandWidth = true;
            foreach (Button button in new[] { context.quickRestart, context.abandonRun, context.exitToMenu, context.concede, context.exitToDesktop })
            {
                Move(button.transform, session);
                LayoutElement element = button.GetComponent<LayoutElement>();
                if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
                element.minHeight = element.preferredHeight = 45f;
            }
            if (context.versionText != null)
            {
                Move(context.versionText, rail);
                LayoutElement element = context.versionText.GetComponent<LayoutElement>();
                if (element == null) element = context.versionText.gameObject.AddComponent<LayoutElement>();
                element.ignoreLayout = false;
                element.minHeight = element.preferredHeight = 30f;
                TMP_Text version = context.versionText.GetComponentInChildren<TMP_Text>(true);
                if (version != null)
                {
                    version.font = body;
                    version.fontSize = 14f;
                    version.color = A(Muted, 0.6f);
                    version.alignment = TextAlignmentOptions.BottomLeft;
                }
            }

            // Pages reuse the old panel roots, so SettingsManager keeps its canvas group fields.
            RectTransform gamePage = Page(context.gamePanel, content);
            RectTransform audioPage = Page(context.audioPanel, content);
            RectTransform graphicsPage = Page(context.graphicsPanel, content);
            RectTransform controlsPage = Page(context.controlsPanel, content);
            RectTransform creditsPage = Page(context.creditsPanel, content);
            RectTransform guidePage = Page(context.infoPanel, content);
            RectTransform devPage = Stretch(Rect("Dev Tools Panel", content));
            CanvasGroup devCanvas = devPage.gameObject.AddComponent<CanvasGroup>();
            devCanvas.alpha = 0f;
            devCanvas.interactable = false;
            devCanvas.blocksRaycasts = false;
            MemoriCanvasGroup devGroup = devPage.gameObject.AddComponent<MemoriCanvasGroup>();

            var rows = new Dictionary<string, Component>();

            // Game
            PageHeader(gamePage, "settingsGroupGame", "settingsSubGame", out _);
            Body(gamePage, GamePage, rows, out GameObject progressGroup);

            // Audio
            PageHeader(audioPage, "tabAudio", "settingsSubAudio", out _);
            Body(audioPage, AudioPage, rows, out _);

            // Graphics
            PageHeader(graphicsPage, "Graphics", null, out RectTransform graphicsActions);
            // The detected hardware line stands in for the subtitle and gives way first when the row is short.
            TMP_Text hardware = Text("Subtitle", graphicsActions.parent, body, 15f, Sub, "Graphics Preset");
            hardware.alignment = TextAlignmentOptions.BaselineLeft;
            hardware.overflowMode = TextOverflowModes.Ellipsis;
            hardware.transform.SetSiblingIndex(graphicsActions.parent.Find("Title").GetSiblingIndex() + 1);
            LayoutElement hardwareElement = Flexible(hardware.gameObject, 1f);
            hardwareElement.minWidth = 0f;
            foreach (Button button in new[] { context.revert, context.apply })
            {
                Move(button.transform, graphicsActions);
                SizeButton(button.gameObject, 210f, 44f);
                UseBaseLabel(button.gameObject);
            }
            Body(graphicsPage, GraphicsPage, rows, out _);

            // Controls
            PageHeader(controlsPage, "Controls", "settingsSubControls", out RectTransform controlsActions);
            if (context.resetKeys != null)
            {
                Move(context.resetKeys, controlsActions);
                SizeButton(context.resetKeys.gameObject, 210f, 44f);
                UseBaseLabel(context.resetKeys.gameObject);
            }
            Body(controlsPage, ControlsPage, rows, out _);

            // Credits
            PageHeader(creditsPage, "creditsButton", null, out _);
            if (context.oldCredits != null) Credits(creditsPage, context.oldCredits);

            // Battle Guide keeps its own header and sidebar; it only moves into the page area.
            foreach (Transform child in guidePage)
            {
                var guideRect = (RectTransform)child;
                guideRect.anchorMin = guideRect.anchorMax = new Vector2(0.5f, 1f);
                guideRect.pivot = new Vector2(0.5f, 1f);
                guideRect.anchoredPosition = Vector2.zero;
            }

            // Dev Tools
            PageHeader(devPage, "settingsDevTools", null, out _);
            if (context.devPanel != null)
            {
                Move(context.devPanel, devPage);
                var devRect = (RectTransform)context.devPanel;
                devRect.anchorMin = devRect.anchorMax = new Vector2(0f, 1f);
                devRect.pivot = new Vector2(0f, 1f);
                devRect.anchoredPosition = new Vector2(0f, -96f);
            }

            // Wire the managers.
            Ref(managerObject, "resumeGameButton", closeButton);
            Ref(managerObject, "closeLabel", closeLabel);
            Ref(managerObject, "infoButton", guideRow.Button);
            Ref(managerObject, "gameSettingsButton", gameRow.Button);
            Ref(managerObject, "audioSettingsButton", audioRow.Button);
            Ref(managerObject, "graphicsSettingsButton", graphicsRow.Button);
            Ref(managerObject, "controlsSettingsButton", controlsRow.Button);
            Ref(managerObject, "creditsButton", creditsRow.Button);
            Ref(managerObject, "collectionButton", collectionRow.Button);
            Ref(managerObject, "devToolsButton", devRow.Button);
            Ref(managerObject, "devToolsCanvasGroup", devGroup);
            Ref(managerObject, "deleteProgressGroup", progressGroup);
            SerializedProperty railProperty = managerObject.FindProperty("railEntries");
            var entries = new (MemoriCanvasGroup page, CollectionRailRow row)[]
            {
                (context.gamePanel.GetComponent<MemoriCanvasGroup>(), gameRow),
                (context.audioPanel.GetComponent<MemoriCanvasGroup>(), audioRow),
                (context.graphicsPanel.GetComponent<MemoriCanvasGroup>(), graphicsRow),
                (context.controlsPanel.GetComponent<MemoriCanvasGroup>(), controlsRow),
                (context.infoPanel.GetComponent<MemoriCanvasGroup>(), guideRow),
                (context.creditsPanel.GetComponent<MemoriCanvasGroup>(), creditsRow),
                (devGroup, devRow),
            };
            railProperty.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                railProperty.GetArrayElementAtIndex(i).FindPropertyRelative("page").objectReferenceValue = entries[i].page;
                railProperty.GetArrayElementAtIndex(i).FindPropertyRelative("row").objectReferenceValue = entries[i].row;
            }
            Ref(graphicsObject, "hardwareTierLabel", hardware);
            foreach (KeyValuePair<string, Component> pair in rows)
            {
                SerializedObject target = managerObject.FindProperty(pair.Key) != null ? managerObject
                    : graphicsObject.FindProperty(pair.Key) != null ? graphicsObject : audioObject;
                Ref(target, pair.Key, pair.Value);
            }
            managerObject.ApplyModifiedPropertiesWithoutUndo();
            graphicsObject.ApplyModifiedPropertiesWithoutUndo();
            audioObject.ApplyModifiedPropertiesWithoutUndo();

            // The old band, rail tiles and page contents.
            foreach (Object item in context.delete)
                if (item is GameObject go && go != null && (go.transform.parent == null || !context.delete.Contains(go.transform.parent.gameObject)))
                    Object.DestroyImmediate(go);

            Normalize(codex.gameObject);
            RecordOverrides(codex.gameObject);

            // Pages start switched off; SettingsManager turns one on when it is opened.
            foreach (RectTransform page in new[] { gamePage, audioPage, graphicsPage, controlsPage, creditsPage, guidePage, devPage })
                page.gameObject.SetActive(false);
        }

        static void Move(Transform item, Transform parent)
        {
            GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(item.gameObject);
            if (outer != null && outer != item.gameObject)
                throw new InvalidOperationException($"{item.name} is inside the prefab instance {outer.name}; it cannot be moved.");
            item.SetParent(parent, false);
            if (item.parent != parent) throw new InvalidOperationException($"Moving {item.name} failed.");
        }

        static void SizeButton(GameObject button, float width, float height)
        {
            LayoutElement element = button.GetComponent<LayoutElement>();
            if (element == null) element = button.AddComponent<LayoutElement>();
            element.ignoreLayout = false;
            element.minWidth = element.preferredWidth = width;
            element.minHeight = element.preferredHeight = height;
            ((RectTransform)button.transform).sizeDelta = new Vector2(width, height);
        }

        // These buttons carried their own label sizes from before the Button Base fold; they take the family's again.
        static void UseBaseLabel(GameObject button)
        {
            var so = new SerializedObject(button.transform.Find("Button Label").GetComponent<TMP_Text>());
            foreach (string field in new[] { "m_fontSize", "m_fontSizeBase", "m_fontSizeMin", "m_fontSizeMax", "m_enableAutoSizing" })
            {
                SerializedProperty property = so.FindProperty(field);
                if (property != null && property.prefabOverride) PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
            }
        }

        static RectTransform Page(Transform panel, RectTransform content)
        {
            Move(panel, content);
            return Stretch((RectTransform)panel);
        }

        static void RailSection(RectTransform rail, string key)
        {
            var section = (GameObject)PrefabUtility.InstantiatePrefab(CollectionPart("Rail Section"), rail);
            section.name = English(key);
            Localize(section.GetComponent<TMP_Text>(), key);
        }

        static CollectionRailRow RailRow(RectTransform rail, string name, string key, Sprite icon)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(CollectionPart("Rail Row"), rail);
            go.name = name;
            CollectionRailRow row = go.GetComponent<CollectionRailRow>();
            row.SetUp(English(key), icon, RailIcon, false);
            Localize(go.transform.Find("Label").GetComponent<TMP_Text>(), key);
            foreach (string part in new[] { "Count", "Progress", "New" }) go.transform.Find(part).gameObject.SetActive(false);
            return row;
        }

        static void PageHeader(RectTransform page, string titleKey, string subtitleKey, out RectTransform actions)
        {
            RectTransform header = Rect("Page Header", page);
            Top(header, 64f);
            Image rule = Img(Rect("Rule", header), null, Line);
            Anchor(rule.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            // TMP Baseline alignment puts a text's baseline at the vertical centre of its rect, so every text in
            // this row fills a band twice the baseline height and shares one baseline, as in the Collection.
            RectTransform row = Rect("Row", header);
            Anchor(row, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, HeaderBaseline * 2f));
            HLayout(row, 16f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandHeight = true;
            RectTransform markerSlot = Rect("Marker Slot", row);
            Fixed(markerSlot.gameObject, 16f, -1f);
            Image marker = Img(Rect("Marker", markerSlot), null, Gold);
            Centre(marker.rectTransform, 14f, 14f);
            marker.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            marker.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            TMP_Text title = Text("Title", row, displayDrop, 34f, Cream, English(titleKey));
            title.alignment = TextAlignmentOptions.BaselineLeft;
            Localize(title, titleKey);
            if (subtitleKey != null)
            {
                TMP_Text subtitle = Text("Subtitle", row, body, 15f, Sub, English(subtitleKey));
                subtitle.alignment = TextAlignmentOptions.BaselineLeft;
                Localize(subtitle, subtitleKey);
            }
            Flexible(Rect("Spacer", row).gameObject, 1f);
            actions = Rect("Actions", row);
            HorizontalLayoutGroup actionRow = HLayout(actions, 16f, TextAnchor.MiddleRight, new RectOffset(0, 0, 0, 6));
            actionRow.childForceExpandHeight = false;
        }

        static void Body(RectTransform page, GroupSpec[][] columns, Dictionary<string, Component> rows, out GameObject progressGroup)
        {
            progressGroup = null;
            var scroll = (GameObject)PrefabUtility.InstantiatePrefab(CollectionPart("Scroll View"), page);
            scroll.name = "Body";
            Stretch((RectTransform)scroll.transform, 0f, 0f, 84f, 0f);
            RectTransform bodyContent = scroll.GetComponent<ScrollRect>().content;
            HorizontalLayoutGroup columnsLayout = HLayout(bodyContent, 72f, TextAnchor.UpperLeft, new RectOffset(26, 26, 4, 24));
            columnsLayout.childForceExpandWidth = true;
            foreach (GroupSpec[] column in columns)
            {
                RectTransform columnRect = Rect("Column", bodyContent);
                VLayout(columnRect, 28f, new RectOffset()).childForceExpandWidth = true;
                Flexible(columnRect.gameObject, 1f).minWidth = 0f;
                foreach (GroupSpec group in column)
                {
                    RectTransform groupRect = Rect(English(group.key), columnRect);
                    VLayout(groupRect, 6f, new RectOffset()).childForceExpandWidth = true;
                    GroupHeader(groupRect, group.key);
                    RectTransform list = Rect("Rows", groupRect);
                    VLayout(list, 0f, new RectOffset()).childForceExpandWidth = true;
                    foreach (RowSpec spec in group.rows) BuildRow(list, spec, rows);
                    if (group.key == "settingsGroupProgress") progressGroup = groupRect.gameObject;
                }
            }
        }

        static void GroupHeader(RectTransform parent, string key)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(CollectionPart("Group Header"), parent);
            go.name = "Group Header";
            go.transform.Find("Diamond").GetComponent<Image>().color = Brass;
            Localize(go.transform.Find("Label").GetComponent<TMP_Text>(), key);
            go.transform.Find("Count").gameObject.SetActive(false);
        }

        static void BuildRow(RectTransform list, RowSpec spec, Dictionary<string, Component> rows)
        {
            string part = spec.kind switch
            {
                Kind.Toggle => "Setting Row - Toggle",
                Kind.Choice => "Setting Row - Choice",
                Kind.List => "Setting Row - List",
                Kind.Slider => "Setting Row - Slider",
                Kind.Key => "Setting Row - Key",
                _ => "Setting Row",
            };
            var go = (GameObject)PrefabUtility.InstantiatePrefab(parts[part], list);
            go.name = English(spec.label);
            TMP_Text label = go.transform.Find("Text/Label").GetComponent<TMP_Text>();
            Localize(label, spec.label);
            Transform help = go.transform.Find("Text/Help");
            if (spec.help != null) Localize(help.GetComponent<TMP_Text>(), spec.help);
            else if (spec.kind != Kind.Key) help.gameObject.SetActive(false);
            Transform control = go.transform.Find("Control");
            if (spec.inactive) go.SetActive(false);

            switch (spec.kind)
            {
                case Kind.Toggle:
                {
                    Toggle toggle = control.GetComponentInChildren<Toggle>(true);
                    if (spec.pref == EdgePanningToggle.PlayerPrefKey)
                    {
                        EdgePanningToggle edge = go.AddComponent<EdgePanningToggle>();
                        var so = new SerializedObject(edge);
                        Ref(so, "onToggle", toggle);
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    else if (spec.pref == "EnableClothSimulation")
                    {
                        ClothSettingsToggleV2 cloth = go.AddComponent<ClothSettingsToggleV2>();
                        var so = new SerializedObject(cloth);
                        so.FindProperty("playerPrefValue").stringValue = spec.pref;
                        Ref(so, "onToggle", toggle);
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    else if (spec.pref != null)
                    {
                        SettingsToggleV2 saved = go.AddComponent<SettingsToggleV2>();
                        var so = new SerializedObject(saved);
                        so.FindProperty("playerPrefValue").stringValue = spec.pref;
                        so.FindProperty("defaultOn").boolValue = spec.defaultOn;
                        so.FindProperty("invertPref").boolValue = spec.invert;
                        Ref(so, "onToggle", toggle);
                        so.ApplyModifiedPropertiesWithoutUndo();
                        if (spec.field != null) rows[spec.field] = saved;
                    }
                    else if (spec.field != null) rows[spec.field] = toggle;
                    break;
                }
                case Kind.Choice:
                case Kind.List:
                    rows[spec.field] = control.GetComponentInChildren<TMP_Dropdown>(true);
                    break;
                case Kind.Slider:
                {
                    Slider slider = control.GetComponentInChildren<Slider>(true);
                    TMP_Text value = control.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Value");
                    if (spec.pref != null)
                    {
                        MonitoredDataSlider monitored = go.AddComponent<MonitoredDataSlider>();
                        var so = new SerializedObject(monitored);
                        Ref(so, "slider", slider);
                        so.FindProperty("playerPref").stringValue = spec.pref;
                        Ref(so, "sliderNameText", label);
                        Ref(so, "sliderValueText", value);
                        so.ApplyModifiedPropertiesWithoutUndo();
                        if (current.oldSliders.TryGetValue(spec.pref, out Slider old) && old != null)
                        {
                            slider.minValue = old.minValue;
                            slider.maxValue = old.maxValue;
                            slider.wholeNumbers = old.wholeNumbers;
                        }
                        rows[spec.field] = monitored;
                    }
                    else
                    {
                        SettingsSlider audio = go.AddComponent<SettingsSlider>();
                        var so = new SerializedObject(audio);
                        Ref(so, "slider", slider);
                        Ref(so, "sliderNameText", label);
                        Ref(so, "sliderValueText", value);
                        so.FindProperty("blockLocalization").boolValue = true;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        rows[spec.field] = audio;
                    }
                    break;
                }
                case Kind.Key:
                {
                    KeyRebinder rebinder = go.AddComponent<KeyRebinder>();
                    Button cap = control.GetComponentInChildren<Button>(true);
                    var so = new SerializedObject(rebinder);
                    Ref(so, "inputActionText", label);
                    Ref(so, "keyText", cap.transform.Find("Key").GetComponent<TMP_Text>());
                    Ref(so, "rebindButton", cap);
                    so.FindProperty("bindingName").stringValue = spec.binding;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
                case Kind.Button:
                case Kind.Danger:
                {
                    string role = spec.kind == Kind.Danger ? "Button - Back" : "Button - Standard";
                    var button = (GameObject)PrefabUtility.InstantiatePrefab(Asset($"{ButtonFolder}/{role}.prefab"), control);
                    button.name = spec.kind == Kind.Danger ? "Delete Button" : "Reset Button";
                    SizeButton(button, 150f, 40f);
                    Localize(button.transform.Find("Button Label").GetComponent<TMP_Text>(), spec.kind == Kind.Danger ? "settingsDelete" : "Reset");
                    rows[spec.field] = spec.kind == Kind.Danger ? button.GetComponent<Button>() : button.GetComponent<MemoriButtonV2>();
                    break;
                }
            }
        }

        // TJ's credits object moves over whole: its thanks lines and name grid are hand-placed and untranslated on purpose.
        static void Credits(RectTransform page, Transform oldCredits)
        {
            var scroll = (GameObject)PrefabUtility.InstantiatePrefab(CollectionPart("Scroll View"), page);
            scroll.name = "Body";
            Stretch((RectTransform)scroll.transform, 0f, 0f, 84f, 0f);
            RectTransform bodyContent = scroll.GetComponent<ScrollRect>().content;
            VLayout(bodyContent, 0f, new RectOffset(26, 26, 4, 24));
            Move(oldCredits, bodyContent);
            VerticalLayoutGroup layout = oldCredits.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.padding.top = 0;
        }

        // Darkens the page toward its left, right and bottom edges with the pack's linear fade, as in the Collection.
        static void EdgeShade(RectTransform root)
        {
            Color shade = Hex("050809", 0.45f);
            Image left = Img(Rect("Shade Left", root), edgeFade, shade);
            Anchor(left.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(260f, 0f));
            Image right = Img(Rect("Shade Right", root), edgeFade, shade);
            Anchor(right.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-260f, 0f), Vector2.zero);
            right.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            Image bottom = Img(Rect("Shade Bottom", root), edgeFade, shade);
            Anchor(bottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            bottom.rectTransform.pivot = new Vector2(0f, 0.5f);
            bottom.rectTransform.sizeDelta = new Vector2(180f, 5000f);
            bottom.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
        }
        #endregion

        #region Primitives
        static Image FrameOn(RectTransform parent)
        {
            Image image = Img(Stretch(Rect("Frame", parent)), frame, Color.white, Image.Type.Sliced, 8f);
            image.raycastTarget = false;
            return image;
        }

        static void NoNavigation(Selectable selectable)
        {
            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
        }

        // Store what an instance computes on load, so instances do not record layout and TMP values as overrides.
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

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
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
            if (property == null) { Debug.LogError($"SettingsCodexBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.objectReferenceValue = value;
        }
        #endregion
    }
}
