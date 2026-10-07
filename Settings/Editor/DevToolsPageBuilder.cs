using Memori.Audio;
using TJ.DevTools;
using TJ.MainMenu;
using TJ.MainMenu.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TJ.Settings.EditorTools
{
    /// <summary>
    /// Generates Dev Tools Page.prefab from the Collection parts and the Button Base family, and installs it on the
    /// Settings screen's Dev Tools page. Rebuilding overwrites hand edits to the prefab.
    /// </summary>
    public static class DevToolsPageBuilder
    {
        public const string PrefabPath = "Assets/Data/Prefabs/UI/Settings/Dev Tools Page.prefab";
        const string ButtonFolder = "Assets/Data/Prefabs/UI/Reuseable/Buttons";
        const string ScenePath = "Assets/Scenes/Core.unity";
        const string HoverSoundPath = "Assets/Scripts/Memori.Audio/SOs/Button Hover - SFXReference.asset";
        const string OldPanelName = "Dev Panel";
        const string PageName = "Dev Tools Page";
        const float HeaderHeight = 84f;

        #region Style
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        static readonly Color Caption = Hex("8C9AA2");
        static readonly Color Line = Hex("2C3A3D");
        static readonly Color Well = Hex("0E171A");

        static TMP_FontAsset displayDrop, body;
        static Sprite roundedFill, roundedOutline;

        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            body = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            roundedFill = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Border/Rounded/256px/Rounded Filled 256px.png");
            roundedOutline = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Border/Rounded/256px/Rounded Outline 256px - 3x.png");
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"DevToolsPageBuilder: missing {typeof(T).Name} at {path}");
            return asset;
        }

        static GameObject CollectionPart(string name) => Load<GameObject>($"{CollectionCodexBuilder.PartFolder}/{name}.prefab");

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

        #region Menus
        [MenuItem("Tabletop Tavern/Settings/Dev Tools/Rebuild Prefab")]
        public static void BuildPrefab()
        {
            LoadAssets();
            // Rebuild inside the existing prefab so the root keeps its ids; the Core scene instance refers to them.
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject(PageName, typeof(RectTransform));
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"DevToolsPageBuilder: wrote {PrefabPath}");
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        /// <summary>Puts the page under Settings > Dev Tools in Core.unity and removes the old panel. Does not save the scene.</summary>
        [MenuItem("Tabletop Tavern/Settings/Dev Tools/Install In Core")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("DevToolsPageBuilder: leave Play Mode before installing.");
                return;
            }
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"DevToolsPageBuilder: open {ScenePath} first.");
                return;
            }
            GameObject prefab = Load<GameObject>(PrefabPath);
            if (prefab == null) return;

            SettingsManager manager = null;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                manager = sceneRoot.GetComponentInChildren<SettingsManager>(true);
                if (manager != null) break;
            }
            if (manager == null)
            {
                Debug.LogError("DevToolsPageBuilder: no SettingsManager in Core.unity.");
                return;
            }
            var panel = new SerializedObject(manager).FindProperty("devToolsCanvasGroup").objectReferenceValue as Component;
            if (panel == null)
            {
                Debug.LogError("DevToolsPageBuilder: SettingsManager.devToolsCanvasGroup is not assigned.");
                return;
            }

            Transform old = panel.transform.Find(OldPanelName);
            bool removedOld = old != null;
            if (removedOld) Object.DestroyImmediate(old.gameObject);
            if (panel.transform.Find(PageName) == null)
            {
                var page = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel.transform);
                page.name = PageName;
                Stretch((RectTransform)page.transform, 0f, 0f, HeaderHeight, 0f);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"DevToolsPageBuilder: installed under {panel.name}. Old panel removed: {removedOld}. Save {ScenePath} to keep it.");
        }
        #endregion

        #region Layout
        static void Build(GameObject rootGo)
        {
            rootGo.name = PageName;
            rootGo.layer = 5;
            RectTransform root = (RectTransform)rootGo.transform;
            Stretch(root, 0f, 0f, HeaderHeight, 0f);
            DevToolsPage page = GetOrAdd<DevToolsPage>(rootGo);

            // Status strip
            TMP_Text status = Text("Status", root, body, 15f, Cream, "State");
            Anchor(status.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -30f), new Vector2(-26f, 0f));
            status.alignment = TextAlignmentOptions.MidlineLeft;
            status.overflowMode = TextOverflowModes.Ellipsis;

            // Log
            RectTransform logBox = Rect("Log", root);
            Anchor(logBox, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(26f, 16f), new Vector2(-26f, 104f));
            Img(logBox, null, Well);
            Image logRule = Img(Rect("Rule", logBox), null, A(Brass, 0.5f));
            Anchor(logRule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f), Vector2.zero);
            TMP_Text log = Text("Text", logBox, body, 14f, Cream, "");
            Stretch(log.rectTransform, 14f, 14f, 8f, 8f);
            log.alignment = TextAlignmentOptions.BottomLeft;
            log.overflowMode = TextOverflowModes.Truncate;

            // Tools column
            RectTransform tools = Rect("Tools", root);
            Anchor(tools, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(26f, 116f), new Vector2(586f, -40f));
            ScrollRect toolScroll = Scroll(tools, "Tool Scroll", out RectTransform toolContent);
            Stretch((RectTransform)toolScroll.transform);
            VLayout(toolContent, 6f, new RectOffset(0, 14, 0, 12)).childForceExpandWidth = true;

            // Browser column
            RectTransform browser = Rect("Browser", root);
            Anchor(browser, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(626f, 116f), new Vector2(-26f, -40f));
            CanvasGroup browserGroup = browser.gameObject.AddComponent<CanvasGroup>();
            VLayout(browser, 8f, new RectOffset()).childForceExpandWidth = true;

            RectTransform tabRow = Rect("Tabs", browser);
            HLayout(tabRow, 28f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandHeight = true;
            Fixed(tabRow.gameObject, -1f, 34f);

            TMP_InputField search = SearchField(browser);

            RectTransform chipRow = Rect("Chips", browser);
            GridLayoutGroup chipGrid = chipRow.gameObject.AddComponent<GridLayoutGroup>();
            chipGrid.cellSize = new Vector2(164f, 26f);
            chipGrid.spacing = new Vector2(8f, 6f);
            chipGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            chipGrid.constraintCount = 5;

            RectTransform optionRow = Rect("Options", browser);
            HLayout(optionRow, 8f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(optionRow.gameObject, -1f, 32f);

            RectTransform gridArea = Rect("Grid Area", browser);
            Flexible(gridArea.gameObject, 1f, 1f);
            ScrollRect gridScroll = Scroll(gridArea, "Grid Scroll", out RectTransform gridContent);
            Stretch((RectTransform)gridScroll.transform);
            GridLayoutGroup grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(124f, 162f);
            grid.spacing = new Vector2(6f, 8f);
            grid.padding = new RectOffset(4, 14, 6, 12);
            grid.childAlignment = TextAnchor.UpperLeft;
            TMP_Text empty = Text("Empty", gridArea, body, 16f, Caption, "Nothing matches.");
            Stretch(empty.rectTransform, 6f, 6f, 10f, 0f);
            empty.fontStyle = FontStyles.Italic;
            empty.textWrappingMode = TextWrappingModes.Normal;

            // Templates sit under an inactive holder so layout groups never see them.
            RectTransform templates = Rect("Templates", root);
            templates.gameObject.SetActive(false);

            RectTransform section = Rect("Section", templates);
            section.gameObject.AddComponent<CanvasGroup>();
            VLayout(section, 4f, new RectOffset(0, 0, 0, 14)).childForceExpandWidth = true;
            TMP_Text sectionTitle = Text("Title", section, displayDrop, 21f, Gold, "Section");
            sectionTitle.margin = new Vector4(0f, 0f, 0f, 2f);
            TMP_Text sectionNote = Text("Note", section, body, 14f, Caption, "Only on the map.");
            sectionNote.fontStyle = FontStyles.Italic;

            TMP_Text group = Text("Group", templates, body, 12.5f, Brass, "Group");
            group.fontStyle = FontStyles.UpperCase;
            group.characterSpacing = 10f;
            group.margin = new Vector4(2f, 8f, 0f, 2f);

            RectTransform row = Rect("Row", templates);
            Image rowFill = Img(Stretch(Rect("Fill", row)), null, A(Well, 0.7f));
            rowFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image rowRule = Img(Rect("Rule", row), null, Line);
            Anchor(rowRule.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            rowRule.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HLayout(row, 8f, TextAnchor.MiddleLeft, new RectOffset(12, 6, 2, 2));
            Fixed(row.gameObject, -1f, 36f);
            TMP_Text rowLabel = Text("Label", row, body, 16f, Cream, "Tool");
            rowLabel.alignment = TextAlignmentOptions.MidlineLeft;
            rowLabel.overflowMode = TextOverflowModes.Ellipsis;
            rowLabel.enableAutoSizing = true;
            rowLabel.fontSizeMin = 11f;
            rowLabel.fontSizeMax = 16f;
            Flexible(rowLabel.gameObject, 1f).minWidth = 60f;
            RectTransform rowControls = Rect("Controls", row);
            HLayout(rowControls, 6f, TextAnchor.MiddleRight, new RectOffset());

            Button standard = FamilyButton(templates, "Button - Standard", "Button");
            Button danger = FamilyButton(templates, "Button - Back", "Danger Button");

            var tabGo = (GameObject)PrefabUtility.InstantiatePrefab(CollectionPart("Page Tab"), templates);
            tabGo.name = "Tab";
            CollectionTab tab = tabGo.GetComponent<CollectionTab>();

            Button chip = Chip(templates);
            RectTransform unitCell = Cell(templates, "Unit Cell", "Unit Tile", new Vector2(124f, 162f), new Vector2(120f, 126f));
            RectTransform itemCell = Cell(templates, "Item Cell", "Item Tile", new Vector2(104f, 138f), new Vector2(98f, 98f));

            var so = new SerializedObject(page);
            Ref(so, "statusText", status);
            Ref(so, "toolList", toolContent);
            Ref(so, "sectionTemplate", section);
            Ref(so, "groupTemplate", group);
            Ref(so, "rowTemplate", row);
            Ref(so, "buttonTemplate", standard);
            Ref(so, "dangerButtonTemplate", danger);
            Ref(so, "tabRow", tabRow);
            Ref(so, "tabTemplate", tab);
            Ref(so, "searchField", search);
            Ref(so, "chipRow", chipRow);
            Ref(so, "chipTemplate", chip);
            Ref(so, "optionRow", optionRow);
            Ref(so, "gridScroll", gridScroll);
            Ref(so, "grid", grid);
            Ref(so, "unitCellTemplate", unitCell);
            Ref(so, "itemCellTemplate", itemCell);
            Ref(so, "emptyText", empty);
            Ref(so, "browserGroup", browserGroup);
            Ref(so, "logText", log);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        #region Widgets
        // A Button Base instance at row height. DevToolsPage writes the label, so the label's own localizer stays off.
        static Button FamilyButton(Transform parent, string role, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{ButtonFolder}/{role}.prefab"), parent);
            go.name = name;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(90f, 32f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            element.ignoreLayout = false;
            element.minWidth = 64f;
            element.preferredWidth = 90f;
            element.flexibleWidth = 0f;
            element.minHeight = element.preferredHeight = 32f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(element);
            Button button = go.GetComponent<Button>();
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            TMP_Text label = go.transform.Find("Button Label").GetComponent<TMP_Text>();
            label.text = name;
            label.enableAutoSizing = false;
            label.fontSize = 15f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Center;
            label.margin = Vector4.zero;
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            foreach (LocalizeStringEvent localizer in label.GetComponents<LocalizeStringEvent>())
            {
                localizer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(localizer);
            }
            return button;
        }

        static Button Chip(Transform parent)
        {
            RectTransform chip = Rect("Chip", parent);
            chip.sizeDelta = new Vector2(164f, 26f);
            Image hit = Img(chip, null, Color.clear);
            hit.raycastTarget = true;
            Img(Stretch(Rect("Fill", chip)), roundedFill, Brass, Image.Type.Sliced, 76f / 4f);
            Img(Stretch(Rect("Border", chip)), roundedOutline, A(Brass, 0.55f), Image.Type.Sliced, 76f / 4f);
            TMP_Text label = Text("Label", chip, body, 13f, Cream, "Chip");
            Stretch(label.rectTransform, 8f, 8f, 0f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = 13f;
            Button button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            NoNavigation(button);
            HoverSound(chip.gameObject, button);
            return button;
        }

        // A Collection tile with the entry's name under it; the Collection shows names in its detail panel instead.
        static RectTransform Cell(Transform parent, string name, string part, Vector2 size, Vector2 tileSize)
        {
            RectTransform cell = Rect(name, parent);
            cell.sizeDelta = size;
            var tile = (GameObject)PrefabUtility.InstantiatePrefab(CollectionPart(part), cell);
            tile.name = "Tile";
            RectTransform tileRect = (RectTransform)tile.transform;
            tileRect.anchorMin = tileRect.anchorMax = new Vector2(0.5f, 1f);
            tileRect.pivot = new Vector2(0.5f, 1f);
            tileRect.sizeDelta = tileSize;
            tileRect.anchoredPosition = new Vector2(0f, -2f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tileRect);
            NoNavigation(tile.GetComponent<Button>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(tile.GetComponent<Button>());
            TMP_Text label = Text("Name", cell, body, 12.5f, Cream, "Name");
            Anchor(label.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, size.y - tileSize.y - 4f));
            label.alignment = TextAlignmentOptions.Top;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = 9.5f;
            label.fontSizeMax = 12.5f;
            label.lineSpacing = -20f;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return cell;
        }

        static TMP_InputField SearchField(RectTransform parent)
        {
            RectTransform field = Rect("Search", parent);
            Fixed(field.gameObject, -1f, 38f);
            Img(field, roundedFill, Well, Image.Type.Sliced, 12f).raycastTarget = true;
            Image underline = Img(Rect("Underline", field), null, A(Brass, 0.7f));
            Anchor(underline.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 0f), new Vector2(-4f, 1f));

            RectTransform area = Rect("Text Area", field);
            Stretch(area, 14f, 12f, 4f, 4f);
            area.gameObject.AddComponent<RectMask2D>();
            TMP_Text placeholder = Text("Placeholder", area, body, 17f, Caption, "Search");
            placeholder.fontStyle = FontStyles.Italic;
            Stretch(placeholder.rectTransform);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            TMP_Text text = Text("Text", area, body, 17f, Cream, "");
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = body;
            input.pointSize = 17f;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = Cream;
            input.customCaretColor = true;
            input.selectionColor = A(Gold, 0.35f);
            input.characterLimit = 40;
            NoNavigation(input);
            return input;
        }

        static ScrollRect Scroll(Transform parent, string name, out RectTransform content)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(CollectionPart("Scroll View"), parent);
            go.name = name;
            ScrollRect scroll = go.GetComponent<ScrollRect>();
            content = scroll.content;
            return scroll;
        }

        static void HoverSound(GameObject go, Selectable interactable)
        {
            var so = new SerializedObject(go.AddComponent<UIHoverSFX>());
            Ref(so, "sfxReference", Load<SFXReference>(HoverSoundPath));
            Ref(so, "interactableSource", interactable);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void NoNavigation(Selectable selectable)
        {
            Navigation nav = selectable.navigation;
            nav.mode = Navigation.Mode.None;
            selectable.navigation = nav;
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
            RectTransform rect = Rect(name, parent);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = colour;
            label.text = text;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.richText = true;
            label.alignment = TextAlignmentOptions.TopLeft;
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
            if (height > 0f) element.minHeight = 0f;
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
            if (property == null) { Debug.LogError($"DevToolsPageBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.objectReferenceValue = value;
        }
        #endregion
    }
}
