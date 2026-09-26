using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TJ.GuideEditor
{
    /// <summary>
    /// Generates Battle Guide.prefab from code so the layout can be rebuilt after a design change.
    /// Rebuilding overwrites hand edits to the prefab.
    /// </summary>
    public static class BattleGuideBuilder
    {
        public const string PrefabPath = "Assets/Data/Prefabs/UI/Guide/Battle Guide.prefab";
        public const string ContentPath = "Assets/Data/SOs/Guide/Battle Guide Content.asset";
        public const string RenderTexturePath = "Assets/Art/Gifs/Guide Texture.renderTexture";
        const string ButtonFolder = "Assets/Data/Prefabs/UI/Reuseable/Buttons";

        #region Style
        static readonly Color Gold = Hex("E3BB71");
        static readonly Color Cream = Hex("ECDFC2");
        static readonly Color Body = Hex("C9BFA9");
        static readonly Color Muted = Hex("93A1AD");
        static readonly Color KeyFill = Hex("E8D2A0");
        static readonly Color KeyInk = Hex("2A1E0E");
        static readonly Color KeyShadow = Hex("9C8656");
        static readonly Color Night = Hex("0A1016");
        // Button - Standard's hue at the map buttons' icon recipe (saturation 0.32, alpha 0.8).
        static readonly Color StandardIcon = new(0.605f, 0.8f, 0.89f, 0.8f);

        static TMP_FontAsset display;
        static TMP_FontAsset body;
        static Sprite panelSprite, frameSprite, gradientSprite, roundedFill, roundedOutline, arrowSprite, mouseL, mouseR, mouseM;

        static void LoadAssets()
        {
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            body = Load<TMP_FontAsset>("Assets/Synty/InterfaceFantasyMenus/Fonts/Alegreya Sans/AlegreyaSans-Medium SDF.asset");
            panelSprite = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Box_Background01.png");
            frameSprite = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Frame_Box16.png");
            gradientSprite = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Gradient_Vertical_Smooth01.png");
            roundedFill = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Border/Rounded/256px/Rounded Filled 256px.png");
            roundedOutline = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Border/Rounded/256px/Rounded Outline 256px - 3x.png");
            arrowSprite = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Arrow02.png");
            mouseL = Load<Sprite>("Assets/Art/Icons/UI/Input/ICON_FantasyWarrior_Input_PC_MouseColor_Left_Clean.png");
            mouseR = Load<Sprite>("Assets/Art/Icons/UI/Input/ICON_FantasyWarrior_Input_PC_MouseColor_Right_Clean.png");
            mouseM = Load<Sprite>("Assets/Art/Icons/UI/Input/ICON_FantasyWarrior_Input_PC_MouseColor_Middle_Clean.png");
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"BattleGuideBuilder: missing {typeof(T).Name} at {path}");
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

        [MenuItem("Tabletop Tavern/Battle Guide/Rebuild Prefab")]
        public static void BuildPrefab()
        {
            LoadAssets();
            RenderTexture renderTexture = EnsureRenderTexture();
            BattleGuideContent content = AssetDatabase.LoadAssetAtPath<BattleGuideContent>(ContentPath);
            if (content == null) Debug.LogError("BattleGuideBuilder: build the content asset first (Rebuild Content).");

            // Rebuild inside the existing prefab so the root and its components keep their ids;
            // scene instances hold overrides and references on them.
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Battle Guide", typeof(RectTransform));
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root, renderTexture, content);
                Normalize(root);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"BattleGuideBuilder: wrote {PrefabPath}");
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        // Store what an instance computes on load, so scene instances do not record layout and TMP values as overrides.
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

        static RenderTexture EnsureRenderTexture()
        {
            RenderTexture existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (existing != null) return existing;
            var texture = new RenderTexture(600, 400, 0) { name = "Guide Texture" };
            AssetDatabase.CreateAsset(texture, RenderTexturePath);
            return texture;
        }

        #region Layout
        static void Build(GameObject rootGo, RenderTexture renderTexture, BattleGuideContent content)
        {
            RectTransform root = (RectTransform)rootGo.transform;
            root.sizeDelta = new Vector2(1500f, 700f);
            GetOrAdd<Canvas>(rootGo);
            GetOrAdd<GraphicRaycaster>(rootGo);
            var fit = GetOrAdd<UIFitToCanvas>(rootGo);
            SetField(fit, "designSize", new Vector2(1500f, 700f));
            SetField(fit, "fitToParent", true);
            var view = GetOrAdd<BattleGuideView>(rootGo);

            // Backdrop dims the battlefield behind the pre-battle popup.
            RectTransform backdrop = Rect("Backdrop", root);
            Stretch(backdrop, -3000f, -3000f, -3000f, -3000f);
            Img(backdrop, null, Hex("050A0E", 0.82f)).raycastTarget = true;
            backdrop.gameObject.SetActive(false);

            RectTransform panel = Rect("Panel", root);
            Stretch(panel);
            Img(Stretch(Rect("Background", panel)), panelSprite, Hex("1F2B2E"), Image.Type.Sliced, 4f).raycastTarget = true;
            Img(Stretch(Rect("Shade", panel)), gradientSprite, Hex("000000", 0.45f)).raycastTarget = false;
            Img(Stretch(Rect("Frame", panel), -2f, -2f, -2f, -2f), frameSprite, Hex("1F415B"), Image.Type.Sliced, 4f).raycastTarget = false;

            // Top bar
            RectTransform topBar = Rect("Top Bar", panel);
            Top(topBar, 58f);
            Img(Stretch(Rect("Fill", topBar)), null, Hex("101B24", 0.75f));
            Rule(topBar, false);

            RectTransform browseHeader = Stretch(Rect("Browse Header", topBar));
            TMP_Text browseTitle = Label("Title", browseHeader, display, 28f, Gold, "Battle Guide");
            browseTitle.characterSpacing = 3f;
            Anchor(browseTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(30f, 0f), new Vector2(0f, 0f));
            browseTitle.alignment = TextAlignmentOptions.MidlineLeft;
            TMP_InputField search = SearchField(browseHeader);

            RectTransform tipHeader = Stretch(Rect("Tip Header", topBar));
            var tipRow = HLayout(tipHeader, 14f, TextAnchor.MiddleLeft, new RectOffset(30, 30, 0, 0));
            Stretch((RectTransform)tipRow.transform);
            TMP_Text tipEyebrow = Label("Eyebrow", tipRow.transform, display, 15f, Gold, "Battle tip");
            tipEyebrow.fontStyle = FontStyles.UpperCase;
            tipEyebrow.characterSpacing = 8f;
            Diamond(tipRow.transform, 5f, Muted);
            TMP_Text tipReason = Label("Reason", tipRow.transform, body, 18f, Cream, "A quick lesson before the fight.");
            Flexible(tipReason.gameObject, 1f);
            TMP_Text tipCount = Label("Count", tipRow.transform, body, 16f, Muted, "Tip 1 of 3");
            tipCount.alignment = TextAlignmentOptions.MidlineRight;
            tipHeader.gameObject.SetActive(false);

            // Footer
            RectTransform footer = Rect("Footer", panel);
            Bottom(footer, 68f);
            Rule(footer, true);
            HorizontalLayoutGroup footerRow = HLayout(footer, 14f, TextAnchor.MiddleLeft, new RectOffset(24, 24, 11, 11));
            footerRow.childControlHeight = false;
            Button browseAll = SecondaryButton(footerRow.transform, "Browse All", "Browse the full guide");
            Button prev = PagerButton(footerRow.transform, "Previous", false, out TMP_Text prevLabel);
            RectTransform spacer = Rect("Spacer", footerRow.transform);
            Flexible(spacer.gameObject, 1f);
            spacer.sizeDelta = new Vector2(0f, 20f);
            RectTransform dots = Rect("Dots", spacer);
            Anchor(dots, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup dotRow = HLayout(dots, 9f, TextAnchor.MiddleCenter, new RectOffset());
            dotRow.childControlWidth = dotRow.childControlHeight = true;
            dots.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Image dot = Img(Rect("Dot", dots), null, Muted);
            dot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Fixed(dot.gameObject, 6f, 6f);
            Button viewAnother = SecondaryButton(footerRow.transform, "View Another", "View another");
            Button next = PagerButton(footerRow.transform, "Next", true, out TMP_Text nextLabel);
            Button continueButton = PrimaryButton(footerRow.transform, "Continue", "Continue");

            // Body
            RectTransform bodyArea = Rect("Body", panel);
            Stretch(bodyArea, 0f, 0f, 58f, 68f);

            RectTransform sidebar = Rect("Sidebar", bodyArea);
            Anchor(sidebar, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(270f, 0f));
            Img(Stretch(Rect("Fill", sidebar)), null, Hex("0A1016", 0.35f));
            Image sideRule = Img(Rect("Rule", sidebar), null, A(Gold, 0.22f));
            Anchor(sideRule.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-1f, 0f), Vector2.zero);
            ScrollRect navScroll = Scroll(sidebar, "Nav Scroll", out RectTransform navContent);
            Stretch((RectTransform)navScroll.transform, 0f, 1f, 0f, 0f);
            VerticalLayoutGroup navLayout = VLayout(navContent, 1f, new RectOffset(14, 12, 8, 12));
            navLayout.childForceExpandWidth = true;
            TMP_Text noResults = Label("No Results", navContent, body, 16f, Muted, "No topics match.");
            noResults.fontStyle = FontStyles.Italic;
            noResults.margin = new Vector4(12f, 8f, 0f, 0f);

            RectTransform main = Rect("Main", bodyArea);
            Stretch(main, 270f, 0f, 0f, 0f);
            main.gameObject.AddComponent<CanvasGroup>();
            VerticalLayoutGroup mainLayout = VLayout(main, 14f, new RectOffset(34, 30, 18, 8));
            mainLayout.childForceExpandWidth = true;

            RectTransform header = Rect("Topic Header", main);
            VLayout(header, 4f, new RectOffset());
            TMP_Text eyebrow = Label("Eyebrow", header, display, 14f, Gold, "Controls   1 of 14");
            eyebrow.fontStyle = FontStyles.UpperCase;
            eyebrow.characterSpacing = 8f;
            TMP_Text title = Label("Title", header, display, 36f, Gold, "Selecting and Orders");
            TMP_Text summary = Label("Summary", header, body, 20f, Cream, "Summary");
            summary.textWrappingMode = TextWrappingModes.Normal;
            summary.lineSpacing = -4f;

            RectTransform contentRow = Rect("Content Row", main);
            Flexible(contentRow.gameObject, 1f, 1f);
            HorizontalLayoutGroup contentLayout = HLayout(contentRow, 30f, TextAnchor.UpperLeft, new RectOffset());
            contentLayout.childForceExpandHeight = true;

            RectTransform left = Rect("Left Column", contentRow);
            VLayout(left, 10f, new RectOffset());
            LayoutElement leftElement = left.gameObject.AddComponent<LayoutElement>();
            leftElement.preferredWidth = 330f;
            leftElement.flexibleWidth = 0f;

            RectTransform videoFrame = Rect("Video", left);
            Fixed(videoFrame.gameObject, 330f, 220f);
            Img(videoFrame, null, A(Gold, 0.55f));
            Img(Stretch(Rect("Inset", videoFrame), 1f, 1f, 1f, 1f), null, Night);
            RawImage raw = Stretch(Rect("Picture", videoFrame), 3f, 3f, 3f, 3f).gameObject.AddComponent<RawImage>();
            raw.texture = renderTexture;
            raw.raycastTarget = false;
            VideoPlayer player = videoFrame.gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = true;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = renderTexture;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.skipOnDrop = true;
            TMP_Text caption = Label("Caption", left, body, 15f, Muted, "Caption");
            caption.fontStyle = FontStyles.Italic;
            caption.textWrappingMode = TextWrappingModes.Normal;

            RectTransform related = Rect("See Also", left);
            VLayout(related, 4f, new RectOffset(0, 0, 8, 0));
            TMP_Text relatedLabel = Label("Label", related, display, 13f, Muted, "See also");
            relatedLabel.fontStyle = FontStyles.UpperCase;
            relatedLabel.characterSpacing = 8f;
            RectTransform relatedList = Rect("Links", related);
            VLayout(relatedList, 2f, new RectOffset());

            ScrollRect blockScroll = Scroll(contentRow, "Blocks", out RectTransform blockContent);
            Flexible(blockScroll.gameObject, 1f, 1f);
            VerticalLayoutGroup blockLayout = VLayout(blockContent, 22f, new RectOffset(0, 16, 2, 12));
            blockLayout.childForceExpandWidth = true;

            // Templates live under an inactive holder so layout groups never see them.
            RectTransform templates = Rect("Templates", root);
            templates.gameObject.SetActive(false);

            TMP_Text chapter = Label("Chapter", templates, display, 12f, Muted, "Chapter");
            chapter.fontStyle = FontStyles.UpperCase;
            chapter.characterSpacing = 10f;
            chapter.margin = new Vector4(12f, 8f, 0f, 2f);

            Button navItem = NavItem(templates);
            Button chip = RelatedLink(templates);

            RectTransform rowTemplate = Rect("Block Row", templates);
            HorizontalLayoutGroup rowLayout = HLayout(rowTemplate, 32f, TextAnchor.UpperLeft, new RectOffset());
            rowLayout.childForceExpandWidth = true;

            RectTransform blockTemplate = Rect("Block", templates);
            VLayout(blockTemplate, 8f, new RectOffset()).childForceExpandWidth = true;
            Flexible(blockTemplate.gameObject, 1f).preferredWidth = 1f;
            TMP_Text blockTitle = Label("Title", blockTemplate, display, 17f, Gold, "Block");
            blockTitle.characterSpacing = 3f;
            blockTitle.margin = new Vector4(0f, 0f, 0f, 6f);
            Image titleRule = Img(Rect("Rule", blockTitle.transform), null, A(Gold, 0.28f));
            Anchor(titleRule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            RectTransform blockBody = Rect("Body", blockTemplate);
            VLayout(blockBody, 7f, new RectOffset()).childForceExpandWidth = true;

            RectTransform keyRow = Rect("Key Row", templates);
            HorizontalLayoutGroup keyRowLayout = HLayout(keyRow, 12f, TextAnchor.MiddleLeft, new RectOffset());
            keyRowLayout.childForceExpandWidth = false;
            RectTransform keys = Rect("Keys", keyRow);
            HLayout(keys, 4f, TextAnchor.MiddleLeft, new RectOffset());
            LayoutElement keysElement = keys.gameObject.AddComponent<LayoutElement>();
            keysElement.minWidth = 118f;
            keysElement.flexibleWidth = 0f;
            TMP_Text keyText = Label("Text", keyRow, body, 16f, Cream, "What it does");
            keyText.textWrappingMode = TextWrappingModes.Normal;
            Flexible(keyText.gameObject, 1f);

            RectTransform term = Rect("Term Row", templates);
            VLayout(term, 1f, new RectOffset(0, 0, 0, 3)).childForceExpandWidth = true;
            RectTransform head = Rect("Head", term);
            HLayout(head, 10f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandWidth = false;
            Label("Label", head, display, 16f, Cream, "Term");
            RectTransform termKeys = Rect("Keys", head);
            HLayout(termKeys, 4f, TextAnchor.MiddleLeft, new RectOffset());
            termKeys.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0f;
            TMP_Text termText = Label("Text", term, body, 16f, Body, "Description");
            termText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform inline = Rect("Inline Term", templates);
            HorizontalLayoutGroup inlineLayout = HLayout(inline, 12f, TextAnchor.UpperLeft, new RectOffset());
            inlineLayout.childForceExpandWidth = false;
            TMP_Text inlineLabel = Label("Label", inline, display, 15f, Cream, "Stat");
            inlineLabel.textWrappingMode = TextWrappingModes.Normal;
            LayoutElement inlineLabelElement = inlineLabel.gameObject.AddComponent<LayoutElement>();
            inlineLabelElement.preferredWidth = 148f;
            inlineLabelElement.flexibleWidth = 0f;
            TMP_Text inlineText = Label("Text", inline, body, 16f, Body, "Description");
            inlineText.textWrappingMode = TextWrappingModes.Normal;
            Flexible(inlineText.gameObject, 1f);

            RectTransform listItem = Rect("List Item", templates);
            HorizontalLayoutGroup listLayout = HLayout(listItem, 10f, TextAnchor.UpperLeft, new RectOffset(0, 0, 0, 0));
            listLayout.childForceExpandWidth = false;
            Image marker = Img(Rect("Marker", listItem), arrowSprite, Gold);
            marker.preserveAspect = true;
            Fixed(marker.gameObject, 13f, 20f);
            TMP_Text listText = Label("Text", listItem, body, 17f, Cream, "Item");
            listText.textWrappingMode = TextWrappingModes.Normal;
            Flexible(listText.gameObject, 1f);

            RectTransform formula = Rect("Formula", templates);
            VLayout(formula, 8f, new RectOffset()).childForceExpandWidth = true;
            RectTransform box = Rect("Box", formula);
            // The fill sits on an ignored child: an Image on the box would report its sprite size as a preferred height.
            RectTransform boxFill = Stretch(Rect("Fill", box));
            Img(boxFill, roundedFill, Night, Image.Type.Sliced, 5f);
            boxFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            VLayout(box, 0f, new RectOffset(16, 16, 10, 10));
            TMP_Text formulaText = Label("Formula", box, display, 18f, Gold, "Formula");
            formulaText.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text formulaBody = Label("Text", formula, body, 16f, Body, "Explanation");
            formulaBody.textWrappingMode = TextWrappingModes.Normal;

            RectTransform keycap = Rect("Keycap", templates);
            Img(keycap, roundedFill, KeyFill, Image.Type.Sliced, 7f);
            Shadow keyShadow = keycap.gameObject.AddComponent<Shadow>();
            keyShadow.effectColor = KeyShadow;
            keyShadow.effectDistance = new Vector2(0f, -2f);
            HorizontalLayoutGroup capLayout = HLayout(keycap, 0f, TextAnchor.MiddleCenter, new RectOffset(7, 7, 1, 1));
            capLayout.childForceExpandWidth = false;
            LayoutElement capElement = keycap.gameObject.AddComponent<LayoutElement>();
            capElement.minWidth = 26f;
            capElement.flexibleWidth = 0f;
            capElement.minHeight = capElement.preferredHeight = 26f;
            TMP_Text capText = Label("Text", keycap, display, 14f, KeyInk, "Ctrl");
            capText.alignment = TextAlignmentOptions.Center;

            Image mouse = Img(Rect("Mouse", templates), mouseL, Color.white);
            mouse.preserveAspect = true;
            Fixed(mouse.gameObject, 24f, 30f);

            TMP_Text plus = Label("Plus", templates, body, 16f, Muted, "+");
            plus.alignment = TextAlignmentOptions.Center;

            view.name = "Battle Guide";
            var so = new SerializedObject(view);
            Ref(so, "content", content);
            Ref(so, "mainArea", main);
            Ref(so, "sidebar", sidebar.gameObject);
            Ref(so, "backdrop", backdrop.gameObject);
            Ref(so, "browseHeader", browseHeader.gameObject);
            Ref(so, "browseTitleText", browseTitle);
            Ref(so, "searchField", search);
            Ref(so, "tipHeader", tipHeader.gameObject);
            Ref(so, "tipEyebrowText", tipEyebrow);
            Ref(so, "tipReasonText", tipReason);
            Ref(so, "tipCountText", tipCount);
            Ref(so, "navContainer", navContent);
            Ref(so, "chapterTemplate", chapter);
            Ref(so, "navItemTemplate", navItem);
            Ref(so, "noResultsText", noResults);
            Ref(so, "eyebrowText", eyebrow);
            Ref(so, "titleText", title);
            Ref(so, "summaryText", summary);
            Ref(so, "videoPlayer", player);
            Ref(so, "videoFrame", videoFrame.gameObject);
            Ref(so, "captionText", caption);
            Ref(so, "relatedGroup", related.gameObject);
            Ref(so, "relatedLabelText", relatedLabel);
            Ref(so, "relatedContainer", relatedList);
            Ref(so, "relatedChipTemplate", chip);
            Ref(so, "blockScroll", blockScroll);
            Ref(so, "blockContainer", blockContent);
            Ref(so, "rowTemplate", rowTemplate);
            Ref(so, "blockTemplate", blockTemplate);
            Ref(so, "keyRowTemplate", keyRow);
            Ref(so, "termRowTemplate", term);
            Ref(so, "inlineTermTemplate", inline);
            Ref(so, "listItemTemplate", listItem);
            Ref(so, "formulaTemplate", formula);
            Ref(so, "keycapTemplate", keycap);
            Ref(so, "mouseTemplate", mouse);
            Ref(so, "plusTemplate", plus);
            Ref(so, "mouseLeft", mouseL);
            Ref(so, "mouseRight", mouseR);
            Ref(so, "mouseMiddle", mouseM);
            Ref(so, "prevButton", prev);
            Ref(so, "prevLabel", prevLabel);
            Ref(so, "nextButton", next);
            Ref(so, "nextLabel", nextLabel);
            Ref(so, "dotContainer", dots);
            Ref(so, "dotTemplate", dot);
            Ref(so, "browseAllButton", browseAll);
            Ref(so, "viewAnotherButton", viewAnother);
            Ref(so, "continueButton", continueButton);
            so.FindProperty("gold").colorValue = Gold;
            so.FindProperty("cream").colorValue = Cream;
            so.FindProperty("muted").colorValue = Muted;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The dot template must sit outside the dot row so it never counts as a topic.
            dot.transform.SetParent(templates, false);
        }
        #endregion

        #region Widgets
        static TMP_InputField SearchField(RectTransform parent)
        {
            RectTransform field = Rect("Search", parent);
            Anchor(field, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-354f, -20f), new Vector2(-28f, 20f));
            Img(field, roundedFill, A(Night, 0.9f), Image.Type.Sliced, 12f).raycastTarget = true;
            Image underline = Img(Rect("Underline", field), null, A(Gold, 0.5f));
            Anchor(underline.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 0f), new Vector2(-4f, 1f));

            RectTransform area = Rect("Text Area", field);
            Stretch(area, 14f, 12f, 4f, 4f);
            area.gameObject.AddComponent<RectMask2D>();
            TMP_Text placeholder = Label("Placeholder", area, body, 17f, Muted, "Search topics and keys");
            placeholder.fontStyle = FontStyles.Italic;
            Stretch(placeholder.rectTransform);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            TMP_Text text = Label("Text", area, body, 17f, Cream, "");
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
            return input;
        }

        static Button NavItem(Transform parent)
        {
            RectTransform item = Rect("Nav Item", parent);
            Image fill = Img(item, roundedFill, Hex("1C2F3D"), Image.Type.Sliced, 6f);
            fill.raycastTarget = true;
            Button button = item.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = new Color(1f, 1f, 1f, 0.6f);
            colours.pressedColor = new Color(1f, 1f, 1f, 0.9f);
            colours.selectedColor = new Color(1f, 1f, 1f, 0f);
            colours.colorMultiplier = 1f;
            colours.fadeDuration = 0.08f;
            button.colors = colours;
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            RectTransform activeFill = Stretch(Rect("Active", item));
            Img(activeFill, roundedFill, Hex("1C2F3D"), Image.Type.Sliced, 6f);
            activeFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image accent = Img(Rect("Accent", activeFill), null, Gold);
            Anchor(accent.rectTransform, new Vector2(0f, 0.2f), new Vector2(0f, 0.8f), new Vector2(0f, 0f), new Vector2(3f, 0f));
            Fixed(item.gameObject, -1f, 29f);
            HorizontalLayoutGroup row = HLayout(item, 10f, TextAnchor.MiddleLeft, new RectOffset(12, 8, 0, 0));
            row.childForceExpandWidth = false;
            Image marker = Img(Rect("Marker", item), null, Muted);
            marker.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Fixed(marker.gameObject, 7f, 7f);
            TMP_Text label = Label("Label", item, display, 16f, Cream, "Topic");
            label.overflowMode = TextOverflowModes.Ellipsis;
            Flexible(label.gameObject, 1f);
            // Unread marker: a gold diamond reads the same in every language and never crowds the label.
            RectTransform badge = Rect("New", item);
            Fixed(badge.gameObject, 12f, 12f);
            Image badgeDot = Img(Rect("Dot", badge), null, Gold);
            Anchor(badgeDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-3f, -3f), new Vector2(3f, 3f));
            badgeDot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            return button;
        }

        static Button RelatedLink(Transform parent)
        {
            RectTransform link = Rect("See Also Link", parent);
            Image hit = Img(link, null, new Color(0f, 0f, 0f, 0f));
            hit.raycastTarget = true;
            Button button = link.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            Fixed(link.gameObject, -1f, 28f);
            HLayout(link, 8f, TextAnchor.MiddleLeft, new RectOffset()).childForceExpandWidth = false;
            Image arrow = Img(Rect("Arrow", link), arrowSprite, Gold);
            arrow.preserveAspect = true;
            Fixed(arrow.gameObject, 12f, 12f);
            TMP_Text label = Label("Label", link, body, 17f, Cream, "Related topic");
            label.fontStyle = FontStyles.Underline;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1f, 0.85f, 0.55f, 1f);
            button.colors = colours;
            // The label tints with the button so the link reads as interactive.
            button.targetGraphic = label;
            return button;
        }

        // The footer's buttons are Button Base instances, so they share the game's button look, hover and click.
        static Button FamilyButton(Transform parent, string role, string name, string text, float width, out TMP_Text label)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{ButtonFolder}/{role}.prefab"), parent);
            go.name = name;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, 45f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = width;
            element.flexibleWidth = 0f;
            // The guide turns pages on the arrow keys itself, so the buttons must not move the selection too.
            Button button = go.GetComponent<Button>();
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            label = go.transform.Find("Button Label").GetComponent<TMP_Text>();
            label.text = text;
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            // BattleGuideView sets the label from code, so the label's own localizer stays off.
            LocalizeStringEvent localizer = label.GetComponent<LocalizeStringEvent>();
            if (localizer != null)
            {
                localizer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(localizer);
            }
            return button;
        }

        // Previous mirrors Next: the arrow sits at the outer edge with the label beside it.
        static Button PagerButton(Transform parent, string name, bool isNext, out TMP_Text label)
        {
            Button button = FamilyButton(parent, "Button - Standard", name, isNext ? "Next" : "Previous", 200f, out label);
            label.horizontalAlignment = isNext ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
            Vector4 margin = label.margin;
            label.margin = isNext ? new Vector4(20f, margin.y, 48f, margin.w) : new Vector4(48f, margin.y, 20f, margin.w);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);

            Image icon = button.transform.Find("Icon").GetComponent<Image>();
            icon.gameObject.SetActive(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(icon.gameObject);
            icon.sprite = arrowSprite;
            icon.color = StandardIcon;
            icon.preserveAspect = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
            RectTransform iconRect = icon.rectTransform;
            Vector2 edge = new(isNext ? 1f : 0f, 0.5f);
            iconRect.anchorMin = iconRect.anchorMax = edge;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(20f, 20f);
            iconRect.anchoredPosition = new Vector2(isNext ? -26f : 26f, 0f);
            iconRect.localEulerAngles = isNext ? Vector3.zero : new Vector3(0f, 180f, 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(iconRect);
            return button;
        }

        static Button PrimaryButton(Transform parent, string name, string text)
        {
            Button button = FamilyButton(parent, "Button - Primary", name, text, 190f, out TMP_Text label);
            // Primary is laid out for 90 px; at the footer's 45 px its label centres like the other buttons.
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            Vector4 margin = label.margin;
            label.margin = new Vector4(margin.x, 0f, margin.z, 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            return button;
        }

        static Button SecondaryButton(Transform parent, string name, string text)
        {
            return FamilyButton(parent, "Button - Standard", name, text, 220f, out _);
        }

        static ScrollRect Scroll(Transform parent, string name, out RectTransform content)
        {
            RectTransform rect = Rect(name, parent);
            ScrollRect scroll = rect.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Stretch(Rect("Viewport", rect));
            viewport.gameObject.AddComponent<RectMask2D>();
            // A clear graphic that takes raycasts lets the wheel scroll from anywhere on the page; Img() turns raycasts off.
            Img(viewport, null, new Color(0f, 0f, 0f, 0f)).raycastTarget = true;
            content = Rect("Content", viewport);
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
            scroll.scrollSensitivity = 28f;
            scroll.inertia = true;

            RectTransform bar = Rect("Scrollbar", rect);
            Anchor(bar, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-4f, 4f), new Vector2(0f, -4f));
            Img(bar, null, A(Gold, 0.08f)).raycastTarget = true;
            RectTransform slidingArea = Stretch(Rect("Sliding Area", bar));
            RectTransform handle = Stretch(Rect("Handle", slidingArea));
            Image handleImage = Img(handle, null, A(Gold, 0.45f));
            handleImage.raycastTarget = true;
            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            Navigation nav = scrollbar.navigation;
            nav.mode = Navigation.Mode.None;
            scrollbar.navigation = nav;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
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

        static void Top(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -height);
            rect.offsetMax = Vector2.zero;
        }

        static void Bottom(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, height);
        }

        static void Rule(RectTransform parent, bool atTop)
        {
            Image rule = Img(Rect("Rule", parent), null, A(Gold, 0.25f));
            float y = atTop ? 1f : 0f;
            Anchor(rule.rectTransform, new Vector2(0f, y), new Vector2(1f, y), new Vector2(0f, atTop ? -1f : 0f), new Vector2(0f, atTop ? 0f : 1f));
            rule.raycastTarget = false;
        }

        static void Diamond(Transform parent, float size, Color colour)
        {
            Image diamond = Img(Rect("Dot", parent), null, colour);
            diamond.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Fixed(diamond.gameObject, size, size);
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

        static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, Color colour, string text)
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
            if (property == null) { Debug.LogError($"BattleGuideBuilder: no field '{field}' on BattleGuideView."); return; }
            property.objectReferenceValue = value;
        }

        static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"BattleGuideBuilder: no field '{field}' on {target.GetType().Name}."); return; }
            if (value is Vector2 v) property.vector2Value = v;
            else if (value is bool b) property.boolValue = b;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion
    }
}
