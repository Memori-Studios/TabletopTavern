using System;
using System.Collections.Generic;
using Memori.Audio;
using Memori.Localization;
using Memori.SaveData;
using Memori.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TJ
{
    /// <summary>
    /// The Battle Guide: a table of contents plus one page per topic. Settings > Guide shows it as a browser;
    /// the pre-battle popup shows it one unseen topic at a time.
    /// </summary>
    public class BattleGuideView : MonoBehaviour
    {
        enum Mode { Browse, Tip }

        const string LastTopicPref = "BattleGuideLastTopic";

        [Header("Data")]
        [SerializeField] private BattleGuideContent content;

        [Header("Frame")]
        [SerializeField] private RectTransform mainArea;
        [SerializeField] private GameObject sidebar;
        [SerializeField] private GameObject backdrop;
        [SerializeField] private float sidebarWidth = 270f;

        [Header("Top Bar")]
        [SerializeField] private GameObject browseHeader;
        [SerializeField] private TMP_Text browseTitleText;
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private GameObject tipHeader;
        [SerializeField] private TMP_Text tipEyebrowText;
        [SerializeField] private TMP_Text tipReasonText;
        [SerializeField] private TMP_Text tipCountText;

        [Header("Table Of Contents")]
        [SerializeField] private RectTransform navContainer;
        [SerializeField] private TMP_Text chapterTemplate;
        [SerializeField] private Button navItemTemplate;
        [SerializeField] private TMP_Text noResultsText;

        [Header("Topic Header")]
        [SerializeField] private TMP_Text eyebrowText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text summaryText;

        [Header("Video")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private GameObject videoFrame;
        [SerializeField] private TMP_Text captionText;

        [Header("See Also")]
        [SerializeField] private GameObject relatedGroup;
        [SerializeField] private TMP_Text relatedLabelText;
        [SerializeField] private RectTransform relatedContainer;
        [SerializeField] private Button relatedChipTemplate;

        [Header("Blocks")]
        [SerializeField] private ScrollRect blockScroll;
        [SerializeField] private RectTransform blockContainer;
        [SerializeField] private RectTransform rowTemplate;
        [SerializeField] private RectTransform blockTemplate;
        [SerializeField] private RectTransform keyRowTemplate;
        [SerializeField] private RectTransform termRowTemplate;
        [SerializeField] private RectTransform inlineTermTemplate;
        [SerializeField] private RectTransform listItemTemplate;
        [SerializeField] private RectTransform formulaTemplate;

        [Header("Keys")]
        [SerializeField] private RectTransform keycapTemplate;
        [SerializeField] private Image mouseTemplate;
        [SerializeField] private TMP_Text plusTemplate;
        [SerializeField] private Sprite mouseLeft;
        [SerializeField] private Sprite mouseRight;
        [SerializeField] private Sprite mouseMiddle;

        [Header("Footer")]
        [SerializeField] private Button prevButton;
        [SerializeField] private TMP_Text prevLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextLabel;
        [SerializeField] private RectTransform dotContainer;
        [SerializeField] private Image dotTemplate;
        [SerializeField] private Button browseAllButton;
        [SerializeField] private Button viewAnotherButton;
        [SerializeField] private Button continueButton;

        [Header("Colours")]
        [SerializeField] private Color gold = new(0.890f, 0.733f, 0.443f);
        [SerializeField] private Color cream = new(0.925f, 0.875f, 0.761f);
        [SerializeField] private Color muted = new(0.576f, 0.631f, 0.678f);
        [SerializeField] private Color dotIdle = new(0.231f, 0.314f, 0.384f);
        [SerializeField] private Color upColour = new(0.486f, 0.749f, 0.416f);
        [SerializeField] private Color downColour = new(0.824f, 0.329f, 0.247f);

        readonly List<GuideTopic> topics = new();
        readonly List<Button> navButtons = new();
        readonly List<string> searchIndex = new();
        readonly List<Image> dots = new();
        readonly List<GameObject> spawnedBlocks = new();
        readonly List<GameObject> spawnedChips = new();
        readonly List<GameObject> spawnedNav = new();

        Mode mode = Mode.Browse;
        int currentIndex;
        bool built;
        bool dirtyText;
        string markedTopicId;
        CanvasGroup[] parentGroups;
        List<GuideTopic> tipQueue;
        int tipIndex;
        Action onClose;
        bool allowClose;
        CanvasGroup fadeGroup;
        Coroutine fade;

        public bool IsTipMode => mode == Mode.Tip;

        #region Lifecycle
        void Awake()
        {
            if (content == null) Debug.LogError($"BattleGuideView: content is not assigned on {name}.");
            foreach (RectTransform template in new[] { rowTemplate, blockTemplate, keyRowTemplate, termRowTemplate, inlineTermTemplate, listItemTemplate, formulaTemplate, keycapTemplate })
                if (template != null) template.gameObject.SetActive(false);
            if (chapterTemplate != null) chapterTemplate.gameObject.SetActive(false);
            if (navItemTemplate != null) navItemTemplate.gameObject.SetActive(false);
            if (relatedChipTemplate != null) relatedChipTemplate.gameObject.SetActive(false);
            if (mouseTemplate != null) mouseTemplate.gameObject.SetActive(false);
            if (plusTemplate != null) plusTemplate.gameObject.SetActive(false);
            if (dotTemplate != null) dotTemplate.gameObject.SetActive(false);

            // Footer buttons are Button Base instances, whose MemoriButtonV2 already plays the click.
            prevButton.onClick.AddListener(() => Step(-1));
            nextButton.onClick.AddListener(() => Step(1));
            viewAnotherButton.onClick.AddListener(ShowNextTip);
            browseAllButton.onClick.AddListener(SwitchToBrowse);
            continueButton.onClick.AddListener(Close);
            fadeGroup = mainArea.GetComponent<CanvasGroup>();
            searchField.onValueChanged.AddListener(ApplySearch);
            searchField.onSubmit.AddListener(OpenFirstSearchResult);

            parentGroups = GetComponentsInParent<CanvasGroup>(true);
        }

        void OnEnable()
        {
            LocalizationManager localization = LocalizationManager.InstanceIfExists;
            if (localization != null)
            {
                localization.OnLocalizedStringsLoaded -= OnStringsLoaded;
                localization.OnLocalizedStringsLoaded += OnStringsLoaded;
            }
            if (!built || dirtyText) Rebuild();
            else if (mode == Mode.Browse) ShowTopic(currentIndex);
        }

        void OnDisable()
        {
            fade = null;
            if (fadeGroup != null) fadeGroup.alpha = 1f;
        }

        void OnDestroy()
        {
            if (LocalizationManager.HasInstance) LocalizationManager.Instance.OnLocalizedStringsLoaded -= OnStringsLoaded;
        }

        void OnStringsLoaded()
        {
            if (isActiveAndEnabled) Rebuild();
            else dirtyText = true;
        }

        void Update()
        {
            bool visible = IsVisible();
            if (videoPlayer != null && videoPlayer.clip != null)
            {
                if (visible && !videoPlayer.isPlaying) videoPlayer.Play();
                else if (!visible && videoPlayer.isPlaying) videoPlayer.Pause();
            }
            if (!visible) return;

            if (topics.Count > 0 && markedTopicId != CurrentTopic.id)
            {
                markedTopicId = CurrentTopic.id;
                BattleGuideProgress.MarkSeen(markedTopicId);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || mode != Mode.Browse || searchField.isFocused) return;
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) Click();
            if (keyboard.leftArrowKey.wasPressedThisFrame) Step(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) Step(1);
            else if (keyboard.upArrowKey.wasPressedThisFrame) Step(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) Step(1);
        }

        // The Settings copy stays active behind other tabs at alpha 0; it must not count as read or play video there.
        bool IsVisible()
        {
            if (!isActiveAndEnabled) return false;
            foreach (CanvasGroup group in parentGroups)
                if (group != null && group.alpha < 0.01f) return false;
            return true;
        }
        #endregion

        #region Public API
        /// <summary>Opens the browser on a topic, or on the last one read.</summary>
        public void OpenBrowser(string topicId = null)
        {
            mode = Mode.Browse;
            allowClose = false;
            onClose = null;
            EnsureBuilt();
            int index = IndexOf(topicId ?? PlayerPrefs.GetString(LastTopicPref, ""));
            ApplyMode();
            ShowTopic(index < 0 ? 0 : index);
        }

        /// <summary>Shows unseen topics one at a time before a battle. Continue calls onClosed.</summary>
        public void OpenTips(List<GuideTopic> queue, Action onClosed)
        {
            mode = Mode.Tip;
            allowClose = true;
            onClose = onClosed;
            tipQueue = queue;
            tipIndex = 0;
            EnsureBuilt();
            ApplyMode();
            ShowTip();
        }

        public BattleGuideContent Content => content;
        #endregion

        #region Building
        void EnsureBuilt()
        {
            if (!built) Rebuild();
        }

        void Rebuild()
        {
            // The first build opens on the topic read last, in Settings and in the popup's browser alike.
            if (!built) currentIndex = -1;
            built = true;
            dirtyText = false;
            topics.Clear();
            if (content != null)
                foreach (GuideTopic topic in content.topics)
                    if (BattleGuideProgress.IsAvailable(topic)) topics.Add(topic);

            BuildSearchIndex();
            BuildNav();
            BuildDots();
            ApplyMode();
            if (mode == Mode.Tip && tipQueue != null) ShowTip();
            else
            {
                if (currentIndex < 0) currentIndex = Mathf.Max(0, IndexOf(PlayerPrefs.GetString(LastTopicPref, "")));
                ShowTopic(Mathf.Clamp(currentIndex, 0, Mathf.Max(0, topics.Count - 1)));
            }
        }

        void BuildNav()
        {
            foreach (GameObject go in spawnedNav) Kill(go);
            spawnedNav.Clear();
            navButtons.Clear();

            string lastChapter = null;
            for (int i = 0; i < topics.Count; i++)
            {
                GuideTopic topic = topics[i];
                if (topic.chapterKey != lastChapter)
                {
                    lastChapter = topic.chapterKey;
                    TMP_Text chapter = Instantiate(chapterTemplate, navContainer);
                    chapter.gameObject.SetActive(true);
                    chapter.text = Text(topic.chapterKey);
                    spawnedNav.Add(chapter.gameObject);
                }
                Button item = Instantiate(navItemTemplate, navContainer);
                item.gameObject.SetActive(true);
                item.name = "Nav " + topic.id;
                ChildText(item.transform, "Label").text = Text(topic.titleKey);
                int index = i;
                item.onClick.AddListener(() => { Click(); ShowTopic(index); });
                navButtons.Add(item);
                spawnedNav.Add(item.gameObject);
            }
            if (noResultsText != null)
            {
                noResultsText.text = Text("Guide_NoResults");
                noResultsText.gameObject.SetActive(false);
            }
        }

        void BuildDots()
        {
            foreach (Image dot in dots) Kill(dot.gameObject);
            dots.Clear();
            for (int i = 0; i < topics.Count; i++)
            {
                Image dot = Instantiate(dotTemplate, dotContainer);
                dot.gameObject.SetActive(true);
                dots.Add(dot);
            }
        }

        void BuildSearchIndex()
        {
            searchIndex.Clear();
            foreach (GuideTopic topic in topics)
            {
                var builder = new System.Text.StringBuilder();
                builder.Append(Text(topic.titleKey)).Append(' ').Append(Text(topic.summaryKey)).Append(' ');
                foreach (GuideBlock block in topic.blocks)
                {
                    builder.Append(Text(block.titleKey)).Append(' ');
                    foreach (GuideRow row in block.rows)
                        builder.Append(Text(row.labelKey)).Append(' ').Append(Text(row.textKey)).Append(' ').Append(BattleGuideKeys.ToSearchText(row.keys)).Append(' ');
                }
                searchIndex.Add(StripTags(builder.ToString()).ToLowerInvariant());
            }
        }
        #endregion

        #region Modes
        void ApplyMode()
        {
            bool browse = mode == Mode.Browse;
            sidebar.SetActive(browse);
            if (backdrop != null) backdrop.SetActive(!browse || allowClose);
            browseHeader.SetActive(browse);
            tipHeader.SetActive(!browse);
            mainArea.offsetMin = new Vector2(browse ? sidebarWidth : 0f, mainArea.offsetMin.y);

            prevButton.gameObject.SetActive(browse);
            nextButton.gameObject.SetActive(browse);
            dotContainer.gameObject.SetActive(browse);
            browseAllButton.gameObject.SetActive(!browse);
            viewAnotherButton.gameObject.SetActive(!browse && tipQueue != null && tipQueue.Count > 1);
            continueButton.gameObject.SetActive(allowClose);
            ChildText(continueButton.transform, "Button Label").text = Text("continueButton");
            ChildText(viewAnotherButton.transform, "Button Label").text = Text("ViewAnother");
            ChildText(browseAllButton.transform, "Button Label").text = Text("Guide_BrowseAll");
            browseTitleText.text = Text("Guide_Title");
            tipEyebrowText.text = Text("Guide_TipEyebrow");
            relatedLabelText.text = Text("Guide_SeeAlso");
            TMP_Text placeholder = searchField.placeholder as TMP_Text;
            if (placeholder != null) placeholder.text = Text("Guide_SearchPlaceholder");
        }

        void SwitchToBrowse()
        {
            string id = CurrentTopic?.id;
            mode = Mode.Browse;
            ApplyMode();
            ShowTopic(Mathf.Max(0, IndexOf(id)));
        }

        void Close()
        {
            Action callback = onClose;
            onClose = null;
            if (videoPlayer != null) videoPlayer.Stop();
            callback?.Invoke();
        }
        #endregion

        #region Tips
        void ShowTip()
        {
            if (tipQueue == null || tipQueue.Count == 0) return;
            GuideTopic topic = tipQueue[tipIndex];
            tipCountText.text = string.Format(Text("Guide_TipCount"), tipIndex + 1, tipQueue.Count);
            string reasonKey = BattleGuideProgress.ReasonKey(topic.condition);
            tipReasonText.text = Text(reasonKey);
            viewAnotherButton.gameObject.SetActive(tipQueue.Count > 1);
            int index = IndexOf(topic.id);
            ShowTopic(index < 0 ? 0 : index);
        }

        void ShowNextTip()
        {
            if (tipQueue == null || tipQueue.Count == 0) return;
            tipIndex = (tipIndex + 1) % tipQueue.Count;
            ShowTip();
        }
        #endregion

        #region Topic page
        GuideTopic CurrentTopic => topics.Count == 0 ? null : topics[Mathf.Clamp(currentIndex, 0, topics.Count - 1)];

        int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < topics.Count; i++)
                if (topics[i].id == id) return i;
            return -1;
        }

        void Step(int delta)
        {
            if (topics.Count == 0) return;
            ShowTopic((currentIndex + delta + topics.Count) % topics.Count);
        }

        void ShowTopic(int index)
        {
            if (topics.Count == 0) return;
            currentIndex = Mathf.Clamp(index, 0, topics.Count - 1);
            GuideTopic topic = topics[currentIndex];
            if (mode == Mode.Browse && Application.isPlaying) PlayerPrefs.SetString(LastTopicPref, topic.id);

            string counter = string.Format(Text("Guide_TopicCount"), currentIndex + 1, topics.Count);
            eyebrowText.text = mode == Mode.Browse ? $"{Text(topic.chapterKey)}   <color=#{ColorUtility.ToHtmlStringRGB(muted)}>{counter}</color>" : Text(topic.chapterKey);
            titleText.text = Text(topic.titleKey);
            summaryText.text = Text(topic.summaryKey);
            captionText.text = Text(topic.captionKey);

            if (videoPlayer != null)
            {
                if (videoPlayer.clip != topic.video)
                {
                    videoPlayer.Stop();
                    videoPlayer.clip = topic.video;
                }
                if (topic.video != null && IsVisible()) videoPlayer.Play();
            }
            if (videoFrame != null) videoFrame.SetActive(topic.video != null);

            BuildRelated(topic);
            BuildBlocks(topic);
            RefreshNavState();
            RefreshFooter();
            FadeIn();
        }

        // A short fade marks the page change; unscaled because the Settings copy runs while the battle is paused.
        void FadeIn()
        {
            if (fadeGroup == null || !Application.isPlaying || !isActiveAndEnabled) return;
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeRoutine());
        }

        System.Collections.IEnumerator FadeRoutine()
        {
            const float duration = 0.16f;
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                fadeGroup.alpha = Mathf.Lerp(0.2f, 1f, time / duration);
                yield return null;
            }
            fadeGroup.alpha = 1f;
            fade = null;
        }

        static void Click()
        {
            if (!Application.isPlaying) return;
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
        }

        void BuildRelated(GuideTopic topic)
        {
            foreach (GameObject go in spawnedChips) Kill(go);
            spawnedChips.Clear();
            int shown = 0;
            foreach (string id in topic.related)
            {
                int index = IndexOf(id);
                if (index < 0) continue;
                Button chip = Instantiate(relatedChipTemplate, relatedContainer);
                chip.gameObject.SetActive(true);
                ChildText(chip.transform, "Label").text = Text(topics[index].titleKey);
                chip.onClick.AddListener(() =>
                {
                    Click();
                    if (mode == Mode.Tip) { mode = Mode.Browse; ApplyMode(); }
                    ShowTopic(index);
                });
                spawnedChips.Add(chip.gameObject);
                shown++;
            }
            relatedGroup.SetActive(shown > 0);
        }

        void BuildBlocks(GuideTopic topic)
        {
            foreach (GameObject go in spawnedBlocks) Kill(go);
            spawnedBlocks.Clear();

            RectTransform openRow = null;
            foreach (GuideBlock block in topic.blocks)
            {
                if (block.fullWidth || openRow == null || openRow.childCount >= 2)
                {
                    openRow = Instantiate(rowTemplate, blockContainer);
                    openRow.gameObject.SetActive(true);
                    spawnedBlocks.Add(openRow.gameObject);
                }
                BuildBlock(block, openRow);
                if (block.fullWidth) openRow = null;
            }
            // A lone half-width block in the last row keeps its column width instead of stretching.
            if (openRow != null && openRow.childCount == 1)
            {
                var filler = new GameObject("Filler", typeof(RectTransform), typeof(LayoutElement));
                filler.transform.SetParent(openRow, false);
                LayoutElement element = filler.GetComponent<LayoutElement>();
                element.preferredWidth = 1f;
                element.flexibleWidth = 1f;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(blockContainer);
            blockScroll.verticalNormalizedPosition = 1f;
            blockScroll.velocity = Vector2.zero;
        }

        void BuildBlock(GuideBlock block, RectTransform row)
        {
            RectTransform blockRect = Instantiate(blockTemplate, row);
            blockRect.gameObject.SetActive(true);
            TMP_Text heading = ChildText(blockRect, "Title");
            heading.gameObject.SetActive(!string.IsNullOrEmpty(block.titleKey));
            heading.text = Text(block.titleKey);
            RectTransform body = (RectTransform)blockRect.Find("Body");

            switch (block.type)
            {
                case GuideBlockType.Keys:
                    foreach (GuideRow guideRow in block.rows)
                    {
                        RectTransform keyRow = Spawn(keyRowTemplate, body);
                        FillKeys((RectTransform)keyRow.Find("Keys"), guideRow.keys);
                        ChildText(keyRow, "Text").text = Text(guideRow.textKey);
                    }
                    break;
                case GuideBlockType.Terms:
                    foreach (GuideRow guideRow in block.rows)
                    {
                        RectTransform term = Spawn(termRowTemplate, body);
                        Transform head = term.Find("Head");
                        ChildText(head, "Label").text = Text(guideRow.labelKey);
                        RectTransform keys = (RectTransform)head.Find("Keys");
                        keys.gameObject.SetActive(!string.IsNullOrWhiteSpace(guideRow.keys));
                        FillKeys(keys, guideRow.keys);
                        SetTermText(term, guideRow.textKey);
                    }
                    break;
                case GuideBlockType.TermsInline:
                    foreach (GuideRow guideRow in block.rows)
                    {
                        RectTransform term = Spawn(inlineTermTemplate, body);
                        ChildText(term, "Label").text = Text(guideRow.labelKey);
                        SetTermText(term, guideRow.textKey);
                    }
                    break;
                case GuideBlockType.ListUp:
                case GuideBlockType.ListDown:
                    bool up = block.type == GuideBlockType.ListUp;
                    foreach (GuideRow guideRow in block.rows)
                    {
                        RectTransform item = Spawn(listItemTemplate, body);
                        Image marker = item.Find("Marker").GetComponent<Image>();
                        marker.color = up ? ColorVision.Good(upColour) : ColorVision.Bad(downColour);
                        marker.rectTransform.localEulerAngles = new Vector3(0f, 0f, up ? 90f : -90f);
                        ChildText(item, "Text").text = Text(guideRow.textKey);
                    }
                    break;
                case GuideBlockType.Formula:
                    RectTransform formula = Spawn(formulaTemplate, body);
                    ChildText(formula.Find("Box"), "Formula").text = Text(block.formulaKey);
                    ChildText(formula, "Text").text = Text(block.textKey);
                    break;
            }
        }

        static RectTransform Spawn(RectTransform template, Transform parent)
        {
            RectTransform spawned = Instantiate(template, parent);
            spawned.gameObject.SetActive(true);
            return spawned;
        }

        void FillKeys(RectTransform container, string keys)
        {
            foreach (GuideKeyPart part in BattleGuideKeys.Parse(keys))
            {
                switch (part.kind)
                {
                    case GuideKeyPartKind.MouseLeft:
                    case GuideKeyPartKind.MouseRight:
                    case GuideKeyPartKind.MouseMiddle:
                        Image mouse = Instantiate(mouseTemplate, container);
                        mouse.gameObject.SetActive(true);
                        mouse.sprite = part.kind == GuideKeyPartKind.MouseLeft ? mouseLeft : part.kind == GuideKeyPartKind.MouseRight ? mouseRight : mouseMiddle;
                        break;
                    case GuideKeyPartKind.Keycap:
                        RectTransform cap = Spawn(keycapTemplate, container);
                        TMP_Text capText = ChildText(cap, "Text");
                        capText.text = part.text;
                        // A cap is as wide as its label, so Ctrl and O never stretch to fill the key column.
                        LayoutElement capElement = cap.GetComponent<LayoutElement>();
                        if (capElement != null) capElement.preferredWidth = Mathf.Max(capElement.minWidth, capText.GetPreferredValues(part.text).x + 14f);
                        break;
                    default:
                        TMP_Text word = Instantiate(plusTemplate, container);
                        word.gameObject.SetActive(true);
                        word.text = part.text;
                        break;
                }
            }
        }

        void RefreshNavState()
        {
            for (int i = 0; i < navButtons.Count; i++)
            {
                bool active = i == currentIndex;
                Button button = navButtons[i];
                button.transform.Find("Active").gameObject.SetActive(active);
                TMP_Text label = ChildText(button.transform, "Label");
                label.color = active ? gold : cream;
                Image marker = button.transform.Find("Marker").GetComponent<Image>();
                marker.color = active ? gold : dotIdle;
                GameObject badge = button.transform.Find("New").gameObject;
                badge.SetActive(!active && !BattleGuideProgress.IsSeen(topics[i].id));
            }
            for (int i = 0; i < dots.Count; i++)
            {
                bool active = i == currentIndex;
                dots[i].color = active ? gold : dotIdle;
                dots[i].rectTransform.sizeDelta = active ? new Vector2(10f, 10f) : new Vector2(6f, 6f);
                LayoutElement element = dots[i].GetComponent<LayoutElement>();
                if (element != null) element.preferredWidth = element.preferredHeight = active ? 10f : 6f;
            }
        }

        void RefreshFooter()
        {
            prevLabel.text = Text("Previous");
            nextLabel.text = Text("Next");
        }
        #endregion

        #region Search
        void ApplySearch(string query)
        {
            string needle = (query ?? "").Trim().ToLowerInvariant();
            int shown = 0;
            string[] words = needle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < navButtons.Count; i++)
            {
                bool match = true;
                foreach (string word in words)
                    if (!searchIndex[i].Contains(word)) { match = false; break; }
                navButtons[i].gameObject.SetActive(match);
                if (match) shown++;
            }
            // A chapter heading hides when none of its topics match.
            TMP_Text heading = null;
            bool headingHasMatch = false;
            foreach (GameObject go in spawnedNav)
            {
                TMP_Text asHeading = go.GetComponent<Button>() == null ? go.GetComponent<TMP_Text>() : null;
                if (asHeading != null)
                {
                    if (heading != null) heading.gameObject.SetActive(headingHasMatch);
                    heading = asHeading;
                    headingHasMatch = false;
                }
                else if (go.activeSelf) headingHasMatch = true;
            }
            if (heading != null) heading.gameObject.SetActive(headingHasMatch);
            if (noResultsText != null) noResultsText.gameObject.SetActive(shown == 0);
        }

        void OpenFirstSearchResult(string query)
        {
            for (int i = 0; i < navButtons.Count; i++)
            {
                if (!navButtons[i].gameObject.activeSelf) continue;
                ShowTopic(i);
                return;
            }
        }
        #endregion

        #region Helpers
        static string Text(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
#if UNITY_EDITOR
            // Edit-mode previews have no running LocalizationManager; read the table directly.
            if (!Application.isPlaying)
            {
                var locale = string.IsNullOrEmpty(EditorPreviewLocale) ? null : UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale(EditorPreviewLocale);
                return UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetLocalizedString("MainLocalizationTable", key, locale) ?? key;
            }
#endif
            LocalizationManager localization = LocalizationManager.InstanceIfExists;
            return localization != null && localization.stringTable != null ? localization.GetText(key) : key;
        }

        // Stat and trait terms reuse the card descriptions, which carry keyword tags. Edit-mode previews show them raw.
        void SetTermText(Transform row, string key)
        {
            TMP_Text label = ChildText(row, "Text");
            if (Application.isPlaying && LocalizationManager.InstanceIfExists != null) KeywordText.Apply(label, Text(key));
            else label.text = Text(key);
        }

#if UNITY_EDITOR
        /// <summary>Locale code for Edit-mode previews, which ignore the selected locale.</summary>
        public static string EditorPreviewLocale;
#endif

        static TMP_Text ChildText(Transform parent, string child)
        {
            Transform found = parent.Find(child);
            if (found == null)
            {
                Debug.LogError($"BattleGuideView: {parent.name} has no child '{child}'.");
                return null;
            }
            return found.GetComponent<TMP_Text>();
        }

        // Edit-mode previews cannot use Destroy, which only runs in Play.
        static void Kill(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        static string StripTags(string text) => System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");
        #endregion
    }
}
