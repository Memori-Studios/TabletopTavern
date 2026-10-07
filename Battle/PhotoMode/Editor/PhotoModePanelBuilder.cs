using System;
using System.Collections.Generic;
using Memori.Audio;
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

namespace TJ.PhotoModeTools
{
    /// <summary>
    /// Generates the photo mode screen (Photo Mode UI.prefab): its own canvas with the tabbed panel on the right, the
    /// hint bar, the grid and the shutter flash. Rows are the Settings row parts and buttons are the Button family, so
    /// a style change is made in those parts. Installs one instance at the root of TavernBattle.unity.
    /// </summary>
    public static class PhotoModePanelBuilder
    {
        public const string Folder = "Assets/Data/Prefabs/UI/Battle/Photo Mode";
        public const string PanelPath = Folder + "/Photo Mode UI.prefab";
        const string ScenePath = "Assets/Scenes/TavernBattle.unity";
        const string RowFolder = "Assets/Data/Prefabs/UI/Settings";
        const string ButtonFolder = "Assets/Data/Prefabs/UI/Reuseable/Buttons";
        const string BasicBackgroundPath = "Assets/Data/Prefabs/UI/Reuseable/Basic Background.prefab";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";
        const string HoverSfxGuid = "9fcd2180751066342aff61b2d863479a";
        const string TableName = "MainLocalizationTable";

        #region Layout
        // Below Settings (105), above the battle HUD (0) and its nested canvases.
        const int CanvasOrder = 50;
        const float PanelWidth = 560f;
        const float PanelHeight = 560f;
        const float PanelMargin = 20f;
        const float PanelPadding = 16f;
        const float TitleHeight = 48f;
        const float TabHeight = 36f;
        const float FooterHeight = 45f;
        const float RowHeight = 46f;
        const float SliderWidth = 236f;
        const float HintBarBottom = 28f;
        const float HintBarHeight = 38f;
        const float GridLine = 1.5f;
        #endregion

        #region Style
        static readonly Color Slate = Hex("1F2B2E");
        static readonly Color Well = Hex("162023");
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        static readonly Color Caption = Hex("8C9AA2");
        #endregion

        static TMP_FontAsset displayDrop, display;
        static Sprite solid;
        static GameObject sliderRow, toggleRow, choiceRow, standardButton, backButton, basicBackground;
        static SFXReference hoverSfx;

        #region Entry points
        [MenuItem("Tabletop Tavern/Photo Mode/Rebuild Prefab")]
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
                root = new GameObject("Photo Mode UI", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
            }
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root);
                Normalize(root);
                RecordOverrides(root);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                Debug.Log($"PhotoModePanelBuilder: wrote {PanelPath}");
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

        [MenuItem("Tabletop Tavern/Photo Mode/Install In TavernBattle")]
        public static void InstallMenu() => Debug.Log("PhotoModePanelBuilder: " + Install());

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
                    if (sceneRoot.GetComponentInChildren<PhotoMode>(true) != null) return "already installed.";
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, battle);
                instance.name = "--- PHOTO MODE ---";
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
            Ensure<GraphicRaycaster>(root);
            CanvasGroup rootGroup = Ensure<CanvasGroup>(root);
            PhotoModePanelView view = Ensure<PhotoModePanelView>(root);
            PhotoMode mode = Ensure<PhotoMode>(root);
            var rootRect = (RectTransform)root.transform;

            Image flash = Img(Stretch(Rect("Flash", rootRect)), solid, Color.white);
            flash.raycastTarget = false;
            GameObject grid = Grid(rootRect);

            RectTransform chrome = Stretch(Rect("Chrome", rootRect));
            CanvasGroup chromeGroup = chrome.gameObject.AddComponent<CanvasGroup>();

            RectTransform panel = Rect("Panel", chrome);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.anchoredPosition = new Vector2(-PanelMargin, 0f);
            Stretch((RectTransform)Instance(basicBackground, panel, "Background").transform);

            TMP_Text title = Text("Title", panel, displayDrop, 26f, Gold, TextAlignmentOptions.Center);
            Top(title.rectTransform, PanelPadding, 8f, TitleHeight);
            Localize(title, "photoModeTitle");

            RectTransform tabs = Rect("Tabs", panel);
            Top(tabs, PanelPadding, 8f + TitleHeight, TabHeight);
            HorizontalLayoutGroup tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 4f;
            tabLayout.childControlWidth = tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = true;

            RectTransform pages = Rect("Pages", panel);
            pages.anchorMin = Vector2.zero;
            pages.anchorMax = Vector2.one;
            pages.offsetMin = new Vector2(PanelPadding, PanelPadding + FooterHeight + 10f);
            pages.offsetMax = new Vector2(-PanelPadding, -(8f + TitleHeight + TabHeight + 10f));

            string[] tabKeys = { "photoTabCamera", "photoTabLens", "photoTabLook", "photoTabEffects", "photoTabScene", "photoTabShot" };
            var tabButtons = new Button[tabKeys.Length];
            var tabMarks = new GameObject[tabKeys.Length];
            var tabPages = new GameObject[tabKeys.Length];
            for (int i = 0; i < tabKeys.Length; i++)
            {
                tabButtons[i] = Tab(tabs, tabKeys[i], out tabMarks[i]);
                tabPages[i] = Page(pages, English(tabKeys[i]) + " Page");
            }

            // Camera
            Transform cameraPage = tabPages[0].transform;
            GameObject fov = SliderRow(cameraPage, "Lens Zoom", "photoLensZoom", 15f, 90f, true);
            GameObject roll = SliderRow(cameraPage, "Roll", "photoRoll", -90f, 90f, true);
            GameObject speed = SliderRow(cameraPage, "Move Speed", "photoMoveSpeed", 0.25f, 2f, false);
            GameObject flags = Row(toggleRow, cameraPage, "Show Flags", "photoShowFlags");
            Button reset = ButtonRow(cameraPage, "Reset", "photoReset");

            // Lens
            Transform lensPage = tabPages[1].transform;
            GameObject depthOfField = Row(toggleRow, lensPage, "Depth of Field", "photoDepthOfField");
            GameObject focus = SliderRow(lensPage, "Focus Distance", "photoFocus", 1f, 300f, false);
            GameObject blur = SliderRow(lensPage, "Blur", "photoBlur", 0f, 1f, false);
            GameObject focusGuide = Row(toggleRow, lensPage, "Focus Guide", "photoFocusGuide");
            GameObject dolly = SliderRow(lensPage, "Dolly Zoom", "photoDollyZoom", 15f, 90f, false);
            Button autofocus = ButtonRow(lensPage, "Focus On Centre", "photoFocusCentre");

            // Look
            Transform lookPage = tabPages[2].transform;
            GameObject preset = Row(choiceRow, lookPage, "Preset", "photoPreset");
            GameObject exposure = SliderRow(lookPage, "Exposure", "photoExposure", -2f, 2f, false);
            GameObject contrast = SliderRow(lookPage, "Contrast", "photoContrast", -50f, 50f, true);
            GameObject saturation = SliderRow(lookPage, "Saturation", "photoSaturation", -100f, 100f, true);
            GameObject warmth = SliderRow(lookPage, "Warmth", "photoWarmth", -60f, 60f, true);
            GameObject tint = SliderRow(lookPage, "Tint", "photoTint", -60f, 60f, true);
            GameObject shadowTone = SliderRow(lookPage, "Shadows", "photoShadows", -1f, 1f, false);
            GameObject highlightTone = SliderRow(lookPage, "Highlights", "photoHighlights", -1f, 1f, false);

            // Effects
            Transform effectsPage = tabPages[3].transform;
            GameObject vignette = SliderRow(effectsPage, "Vignette", "photoVignette", 0f, 0.6f, false);
            GameObject bloom = SliderRow(effectsPage, "Bloom", "photoBloom", 0f, 3f, false);
            GameObject grain = SliderRow(effectsPage, "Film Grain", "photoGrain", 0f, 1f, false);
            GameObject fringe = SliderRow(effectsPage, "Colour Fringe", "photoFringe", 0f, 1f, false);
            GameObject distortion = SliderRow(effectsPage, "Lens Distortion", "photoDistortion", -0.6f, 0.6f, false);
            GameObject backdrop = SliderRow(effectsPage, "Backdrop Blur", "photoBackdropBlur", 0f, 1f, false);

            // Scene
            Transform scenePage = tabPages[4].transform;
            GameObject time = Row(choiceRow, scenePage, "Time", "photoTime");
            GameObject follow = Row(toggleRow, scenePage, "Follow Squad", "photoFollow");
            GameObject headBob = Row(toggleRow, scenePage, "Head Bob", "photoHeadBob");
            GameObject weather = Row(choiceRow, scenePage, "Weather", "photoWeather");

            // Shot
            Transform shotPage = tabPages[5].transform;
            GameObject resolution = Row(choiceRow, shotPage, "Resolution", "photoResolution");
            GameObject gridRow = Row(toggleRow, shotPage, "Grid", "photoGrid");
            Button openFolder = ButtonRow(shotPage, "Open Folder", "photoOpenFolder");

            RectTransform footer = Rect("Footer", panel);
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.offsetMin = new Vector2(PanelPadding, PanelPadding);
            footer.offsetMax = new Vector2(-PanelPadding, PanelPadding + FooterHeight);
            HorizontalLayoutGroup footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 10f;
            footerLayout.childControlWidth = footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = footerLayout.childForceExpandHeight = true;
            Button takePhoto = FamilyButton(standardButton, footer, "Take Photo", "photoTake", 2f);
            Button exit = FamilyButton(backButton, footer, "Exit", "photoExit", 1f);

            // Hint bar and the saved line
            RectTransform hintBar = Rect("Hint Bar", chrome);
            hintBar.anchorMin = hintBar.anchorMax = new Vector2(0.5f, 0f);
            hintBar.pivot = new Vector2(0.5f, 0f);
            hintBar.anchoredPosition = new Vector2(0f, HintBarBottom);
            hintBar.sizeDelta = new Vector2(0f, HintBarHeight);
            Image hintFill = Img(hintBar, solid, A(Slate, 0.9f));
            hintFill.raycastTarget = false;
            HorizontalLayoutGroup hintLayout = hintBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hintLayout.padding = new RectOffset(22, 22, 6, 6);
            hintLayout.childAlignment = TextAnchor.MiddleCenter;
            hintLayout.childControlWidth = hintLayout.childControlHeight = true;
            hintLayout.childForceExpandWidth = false;
            ContentSizeFitter hintFit = hintBar.gameObject.AddComponent<ContentSizeFitter>();
            hintFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Image hintRule = Img(Rect("Rule", hintBar), solid, A(Brass, 0.8f));
            hintRule.raycastTarget = false;
            hintRule.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            hintRule.rectTransform.anchorMin = new Vector2(0f, 1f);
            hintRule.rectTransform.anchorMax = new Vector2(1f, 1f);
            hintRule.rectTransform.pivot = new Vector2(0.5f, 1f);
            hintRule.rectTransform.sizeDelta = new Vector2(0f, 1f);
            hintRule.rectTransform.anchoredPosition = Vector2.zero;
            TMP_Text hint = Text("Hints", hintBar, display, 17f, Cream, TextAlignmentOptions.Center);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            hint.richText = true;
            hint.text = "Space  Take photo";

            TMP_Text saved = Text("Saved", chrome, displayDrop, 22f, Gold, TextAlignmentOptions.Center);
            saved.rectTransform.anchorMin = saved.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            saved.rectTransform.pivot = new Vector2(0.5f, 0f);
            saved.rectTransform.anchoredPosition = new Vector2(0f, HintBarBottom + HintBarHeight + 10f);
            saved.rectTransform.sizeDelta = new Vector2(900f, 32f);
            saved.textWrappingMode = TextWrappingModes.NoWrap;
            saved.text = string.Empty;

            flash.gameObject.SetActive(false);
            grid.SetActive(false);

            var so = new SerializedObject(view);
            Set(so, "canvas", canvas);
            Set(so, "rootGroup", rootGroup);
            Set(so, "chromeGroup", chromeGroup);
            Set(so, "panel", panel);
            Set(so, "hintLabel", hint);
            Set(so, "savedLabel", saved);
            Set(so, "grid", grid);
            Set(so, "flash", flash);
            SetArray(so, "tabButtons", tabButtons);
            SetArray(so, "tabMarks", tabMarks);
            SetArray(so, "tabPages", tabPages);
            SetSlider(so, "fov", fov);
            SetSlider(so, "roll", roll);
            SetSlider(so, "speed", speed);
            Set(so, "flagsToggle", flags.GetComponentInChildren<Toggle>(true));
            Set(so, "resetButton", reset);
            Set(so, "depthOfFieldToggle", depthOfField.GetComponentInChildren<Toggle>(true));
            SetSlider(so, "focus", focus);
            SetSlider(so, "blur", blur);
            Set(so, "autofocusButton", autofocus);
            Set(so, "presetDropdown", preset.GetComponentInChildren<TMP_Dropdown>(true));
            SetSlider(so, "exposure", exposure);
            SetSlider(so, "contrast", contrast);
            SetSlider(so, "saturation", saturation);
            SetSlider(so, "warmth", warmth);
            SetSlider(so, "tint", tint);
            SetSlider(so, "shadowTone", shadowTone);
            SetSlider(so, "highlightTone", highlightTone);
            SetSlider(so, "vignette", vignette);
            SetSlider(so, "bloom", bloom);
            SetSlider(so, "grain", grain);
            SetSlider(so, "fringe", fringe);
            SetSlider(so, "distortion", distortion);
            SetSlider(so, "backdrop", backdrop);
            SetSlider(so, "dolly", dolly);
            Set(so, "focusGuideToggle", focusGuide.GetComponentInChildren<Toggle>(true));
            Set(so, "timeDropdown", time.GetComponentInChildren<TMP_Dropdown>(true));
            Set(so, "followToggle", follow.GetComponentInChildren<Toggle>(true));
            Set(so, "headBobToggle", headBob.GetComponentInChildren<Toggle>(true));
            Set(so, "weatherDropdown", weather.GetComponentInChildren<TMP_Dropdown>(true));
            Set(so, "resolutionDropdown", resolution.GetComponentInChildren<TMP_Dropdown>(true));
            Set(so, "gridToggle", gridRow.GetComponentInChildren<Toggle>(true));
            Set(so, "openFolderButton", openFolder);
            Set(so, "takePhotoButton", takePhoto);
            Set(so, "exitButton", exit);
            so.ApplyModifiedPropertiesWithoutUndo();

            var modeObject = new SerializedObject(mode);
            Set(modeObject, "view", view);
            modeObject.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject Grid(RectTransform parent)
        {
            RectTransform grid = Stretch(Rect("Grid", parent));
            Color line = A(Color.white, 0.35f);
            foreach (float at in new[] { 1f / 3f, 2f / 3f })
            {
                Image vertical = Img(Rect("Vertical", grid), solid, line);
                vertical.raycastTarget = false;
                vertical.rectTransform.anchorMin = new Vector2(at, 0f);
                vertical.rectTransform.anchorMax = new Vector2(at, 1f);
                vertical.rectTransform.sizeDelta = new Vector2(GridLine, 0f);
                Image horizontal = Img(Rect("Horizontal", grid), solid, line);
                horizontal.raycastTarget = false;
                horizontal.rectTransform.anchorMin = new Vector2(0f, at);
                horizontal.rectTransform.anchorMax = new Vector2(1f, at);
                horizontal.rectTransform.sizeDelta = new Vector2(0f, GridLine);
            }
            return grid.gameObject;
        }

        static Button Tab(RectTransform parent, string key, out GameObject mark)
        {
            RectTransform tab = Rect(English(key) + " Tab", parent);
            Image fill = Img(tab, solid, Well);
            Button button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
            colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colours.colorMultiplier = 1.4f;
            button.colors = colours;
            HoverSound(tab.gameObject, button);

            TMP_Text label = Text("Label", tab, displayDrop, 18f, Cream, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 18f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.margin = new Vector4(6f, 0f, 6f, 0f);
            Localize(label, key);

            Image underline = Img(Rect("Mark", tab), solid, Gold);
            underline.raycastTarget = false;
            underline.rectTransform.anchorMin = new Vector2(0f, 0f);
            underline.rectTransform.anchorMax = new Vector2(1f, 0f);
            underline.rectTransform.pivot = new Vector2(0.5f, 0f);
            underline.rectTransform.sizeDelta = new Vector2(0f, 3f);
            underline.rectTransform.anchoredPosition = Vector2.zero;
            mark = underline.gameObject;
            return button;
        }

        static GameObject Page(RectTransform parent, string name)
        {
            RectTransform page = Stretch(Rect(name, parent));
            VerticalLayoutGroup layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return page.gameObject;
        }

        static GameObject Row(GameObject prefab, Transform parent, string name, string labelKey)
        {
            GameObject row = Instance(prefab, parent, name + " Row");
            Localize(Child<TMP_Text>(row.transform, "Text/Label"), labelKey);
            // The panel is narrow and its labels say enough; the help line would double every row's height.
            row.transform.Find("Text/Help").gameObject.SetActive(false);
            LayoutElement size = row.GetComponent<LayoutElement>();
            size.minHeight = RowHeight;
            size.preferredHeight = RowHeight;
            TMP_Text label = Child<TMP_Text>(row.transform, "Text/Label");
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = 19f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return row;
        }

        static GameObject SliderRow(Transform parent, string name, string labelKey, float min, float max, bool wholeNumbers)
        {
            GameObject row = Row(sliderRow, parent, name, labelKey);
            Slider slider = row.GetComponentInChildren<Slider>(true);
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            LayoutElement control = Child<LayoutElement>(row.transform, "Control/Setting Slider");
            control.minWidth = SliderWidth;
            control.preferredWidth = SliderWidth;
            Child<LayoutElement>(row.transform, "Control/Setting Slider/Slider").preferredWidth = SliderWidth - 64f;
            return row;
        }

        static Button ButtonRow(Transform parent, string name, string labelKey)
        {
            RectTransform holder = Rect(name + " Row", (RectTransform)parent);
            LayoutElement size = holder.gameObject.AddComponent<LayoutElement>();
            size.minHeight = RowHeight + 8f;
            size.preferredHeight = RowHeight + 8f;
            GameObject instance = Instance(standardButton, holder, name);
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(260f, 42f);
            rect.anchoredPosition = Vector2.zero;
            LabelButton(instance, labelKey);
            return instance.GetComponent<Button>();
        }

        static Button FamilyButton(GameObject prefab, RectTransform parent, string name, string labelKey, float flexibleWidth)
        {
            GameObject instance = Instance(prefab, parent, name);
            LayoutElement size = instance.AddComponent<LayoutElement>();
            size.minHeight = FooterHeight;
            size.flexibleWidth = flexibleWidth;
            LabelButton(instance, labelKey);
            return instance.GetComponent<Button>();
        }

        static void LabelButton(GameObject button, string labelKey)
        {
            TMP_Text label = Child<TMP_Text>(button.transform, "Button Label");
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 19f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Localize(label, labelKey);
        }

        static void HoverSound(GameObject target, Selectable source)
        {
            UIHoverSFX hover = target.AddComponent<UIHoverSFX>();
            var so = new SerializedObject(hover);
            so.FindProperty("sfxReference").objectReferenceValue = hoverSfx;
            so.FindProperty("interactableSource").objectReferenceValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        #region Wiring
        static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException($"PhotoModePanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}.");
            if (value == null) throw new InvalidOperationException($"PhotoModePanelBuilder: nothing to put in '{field}'.");
            property.objectReferenceValue = value;
        }

        static void SetArray<T>(SerializedObject so, string field, T[] values) where T : Object
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException($"PhotoModePanelBuilder: no field '{field}'.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // A slider row fills two fields: <name>Slider and <name>Value.
        static void SetSlider(SerializedObject so, string name, GameObject row)
        {
            Set(so, name + "Slider", row.GetComponentInChildren<Slider>(true));
            Set(so, name + "Value", Child<TMP_Text>(row.transform, "Control/Setting Slider/Value"));
        }
        #endregion

        #region Helpers
        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite && sprite.name == "TooltipSolid") solid = sprite;
            if (solid == null) Debug.LogError("PhotoModePanelBuilder: TooltipSolid sprite missing.");
            sliderRow = Load<GameObject>(RowFolder + "/Setting Row - Slider.prefab");
            toggleRow = Load<GameObject>(RowFolder + "/Setting Row - Toggle.prefab");
            choiceRow = Load<GameObject>(RowFolder + "/Setting Row - Choice.prefab");
            standardButton = Load<GameObject>(ButtonFolder + "/Button - Standard.prefab");
            backButton = Load<GameObject>(ButtonFolder + "/Button - Back.prefab");
            basicBackground = Load<GameObject>(BasicBackgroundPath);
            hoverSfx = Load<SFXReference>(AssetDatabase.GUIDToAssetPath(HoverSfxGuid));
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"PhotoModePanelBuilder: missing {typeof(T).Name} at {path}");
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

        static T Child<T>(Transform root, string path) where T : Component
        {
            Transform child = root.Find(path);
            T component = child != null ? child.GetComponent<T>() : null;
            if (component == null) Debug.LogError($"PhotoModePanelBuilder: {root.name} has no {typeof(T).Name} at '{path}'.");
            return component;
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

        #region Localization
        // English source text for the keys photo mode adds. Other locales are filled in separately.
        public static readonly (string key, string english)[] Keys =
        {
            ("photoModeTitle", "Photo Mode"),
            ("PhotoMode", "Photo Mode"),
            ("photoTabCamera", "Camera"),
            ("photoTabLens", "Lens"),
            ("photoTabLook", "Look"),
            ("photoTabShot", "Shot"),
            ("photoLensZoom", "Lens Zoom"),
            ("photoRoll", "Roll"),
            ("photoMoveSpeed", "Move Speed"),
            ("photoShowFlags", "Show Flags"),
            ("photoReset", "Reset"),
            ("photoDepthOfField", "Depth of Field"),
            ("photoFocus", "Focus Distance"),
            ("photoBlur", "Blur"),
            ("photoFocusCentre", "Focus on Centre"),
            ("photoPreset", "Preset"),
            ("photoExposure", "Exposure"),
            ("photoContrast", "Contrast"),
            ("photoSaturation", "Saturation"),
            ("photoWarmth", "Warmth"),
            ("photoVignette", "Vignette"),
            ("photoResolution", "Resolution"),
            ("photoGrid", "Grid"),
            ("photoOpenFolder", "Open Folder"),
            ("photoTake", "Take Photo"),
            ("photoExit", "Exit"),
            ("photoLookNatural", "Natural"),
            ("photoLookMiniature", "Miniature"),
            ("photoLookWarmTavern", "Warm Tavern"),
            ("photoLookColdSteel", "Cold Steel"),
            ("photoLookFaded", "Faded"),
            ("photoLookInk", "Ink"),
            ("photoHintTake", "Take photo"),
            ("photoHintHide", "Hide panel"),
            ("photoHintStep", "Step time"),
            ("photoHintRoll", "Roll"),
            ("photoHintReset", "Reset"),
            ("photoHintExit", "Exit"),
            ("photoTabEffects", "Effects"),
            ("photoTabScene", "Scene"),
            ("photoTint", "Tint"),
            ("photoShadows", "Shadows"),
            ("photoHighlights", "Highlights"),
            ("photoBloom", "Bloom"),
            ("photoGrain", "Film Grain"),
            ("photoFringe", "Colour Fringe"),
            ("photoDistortion", "Lens Distortion"),
            ("photoBackdropBlur", "Backdrop Blur"),
            ("photoTime", "Time"),
            ("photoTimeFrozen", "Frozen"),
            ("photoFollow", "Follow Squad"),
            ("photoWeather", "Weather"),
            ("photoWeatherCurrent", "Current"),
            ("photoWeatherClear", "Clear"),
            ("photoWeatherRain", "Rain"),
            ("photoWeatherSnow", "Snow"),
            ("photoWeatherFog", "Fog"),
            ("photoFocusGuide", "Focus Guide"),
            ("photoDollyZoom", "Dolly Zoom"),
            ("photoHintFollow", "Follow squad"),
            ("photoHeadBob", "Head Bob"),
            ("photoHintCycle", "Cycle squads"),
            ("photoFollowing", "Following {0}"),
            ("photoFollowingFrozen", "Following {0}. Time is frozen: set Time to a speed to move with them"),
            ("photoFollowNone", "No squad near that point"),
            ("photoSaved", "Photo saved"),
            ("photoSavedSmaller", "Photo saved at screen size"),
            ("photoSaveFailed", "The photo could not be saved"),
        };

        static StringTableCollection collection;
        static StringTable english;

        static void LoadTable()
        {
            collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            english = (StringTable)collection.GetTable("en");
        }

        [MenuItem("Tabletop Tavern/Photo Mode/Add Text Keys")]
        public static void AddKeysMenu() => Debug.Log("PhotoModePanelBuilder: " + EnsureKeys());

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
            if (entry == null) throw new InvalidOperationException($"PhotoModePanelBuilder: no localization key '{key}'.");
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
        #endregion
    }
}
