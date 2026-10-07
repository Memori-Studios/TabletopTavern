using System;
using System.Collections.Generic;
using Memori.Audio;
using TJ.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.DevTools
{
    /// <summary>
    /// The Settings > Dev Tools page. It draws its tool rows and browser tabs from DevToolCatalog, so a new
    /// tool is one catalog entry and nothing in the scene. Its text is plain English: only developers see it.
    /// </summary>
    public class DevToolsPage : MonoBehaviour
    {
        const int LogLines = 3;
        const int RecentCount = 8;
        const float StatusInterval = 0.25f;

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;

        [Header("Tools")]
        [SerializeField] private RectTransform toolList;
        [SerializeField] private RectTransform sectionTemplate;
        [SerializeField] private TMP_Text groupTemplate;
        [SerializeField] private RectTransform rowTemplate;
        [SerializeField] private Button buttonTemplate;
        [SerializeField] private Button dangerButtonTemplate;

        [Header("Browser")]
        [SerializeField] private RectTransform tabRow;
        [SerializeField] private CollectionTab tabTemplate;
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private RectTransform chipRow;
        [SerializeField] private Button chipTemplate;
        [SerializeField] private RectTransform optionRow;
        [SerializeField] private ScrollRect gridScroll;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private RectTransform unitCellTemplate;
        [SerializeField] private RectTransform itemCellTemplate;
        [SerializeField] private Vector2 unitCellSize = new(124f, 162f);
        [SerializeField] private Vector2 itemCellSize = new(104f, 138f);
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private CanvasGroup browserGroup;

        [Header("Log")]
        [SerializeField] private TMP_Text logText;

        [Header("Colours")]
        [SerializeField] private Color good = new(0.48f, 0.84f, 0.44f);
        [SerializeField] private Color bad = new(0.89f, 0.41f, 0.37f);
        [SerializeField] private Color chipFill = new(0.69f, 0.54f, 0.24f);
        [SerializeField] private Color chipInk = new(0.1f, 0.08f, 0.03f);

        class Section
        {
            public DevScope scope;
            public CanvasGroup group;
            public TMP_Text note;
        }

        class ControlView
        {
            public DevControl control;
            public TMP_Text label;
            public LayoutElement element;
        }

        class Cell
        {
            public DevEntry entry;
            public GameObject root;
            public CollectionTile tile;
        }

        class TabView
        {
            public DevBrowserTab tab;
            public CollectionTab button;
            public List<Cell> cells;
            public readonly List<Button> chips = new();
            public readonly List<string> chipGroups = new();
            public readonly List<GameObject> options = new();
            public string chip;
        }

        readonly List<Section> sections = new();
        readonly List<ControlView> controls = new();
        readonly List<TabView> tabs = new();
        readonly List<string> log = new();
        TabView current;
        bool built;
        bool? browserLive;
        float nextStatus;

        #region Lifecycle
        void Awake()
        {
            foreach (Component template in new Component[] { sectionTemplate, groupTemplate, rowTemplate, buttonTemplate, dangerButtonTemplate, tabTemplate, chipTemplate, unitCellTemplate, itemCellTemplate })
            {
                if (template == null) Debug.LogError($"DevToolsPage: a template is not assigned on {name}. Rebuild the prefab.");
                else template.gameObject.SetActive(false);
            }
            searchField.onValueChanged.AddListener(_ => ApplyFilter());
            searchField.onSubmit.AddListener(_ => PickFirst());
        }

        void OnEnable()
        {
            if (!built) Build();
            RefreshAll();
            // The page opens ready to type, so a unit is a few letters and Enter away.
            if (current != null && DevToolCatalog.Available(current.tab.Scope, out _)) searchField.ActivateInputField();
        }

        void Update()
        {
            if (Time.unscaledTime < nextStatus) return;
            nextStatus = Time.unscaledTime + StatusInterval;
            RefreshStatus();
        }
        #endregion

        #region Building
        void Build()
        {
            built = true;
            BuildTools();
            foreach (DevBrowserTab tab in DevToolCatalog.Tabs())
            {
                CollectionTab button = Instantiate(tabTemplate, tabRow);
                button.gameObject.SetActive(true);
                button.name = tab.Name;
                button.SetLabel(tab.Name);
                var view = new TabView { tab = tab, button = button };
                button.Button.onClick.AddListener(() => { Click(); ShowTab(view); });
                tabs.Add(view);
            }
            if (tabs.Count > 0) ShowTab(tabs[0]);
            Print(DevResult.Done("Ready."));
        }

        void BuildTools()
        {
            Section section = null;
            string group = null;
            foreach (DevTool tool in DevToolCatalog.Tools())
            {
                if (section == null || section.scope != tool.Scope)
                {
                    section = AddSection(tool.Scope);
                    group = null;
                }
                if (tool.Group != group)
                {
                    group = tool.Group;
                    TMP_Text header = Instantiate(groupTemplate, section.group.transform);
                    header.gameObject.SetActive(true);
                    header.text = group;
                }
                RectTransform row = Instantiate(rowTemplate, section.group.transform);
                row.gameObject.SetActive(true);
                row.name = tool.Label;
                ChildText(row, "Label").text = tool.Label;
                Transform holder = row.Find("Controls");
                foreach (DevControl control in tool.Controls) AddControl(control, holder, tool.Scope);
            }
        }

        Section AddSection(DevScope scope)
        {
            RectTransform rect = Instantiate(sectionTemplate, toolList);
            rect.gameObject.SetActive(true);
            rect.name = scope.ToString();
            ChildText(rect, "Title").text = DevToolCatalog.Title(scope);
            var section = new Section { scope = scope, group = rect.GetComponent<CanvasGroup>(), note = ChildText(rect, "Note") };
            sections.Add(section);
            return section;
        }

        GameObject AddControl(DevControl control, Transform holder, DevScope scope)
        {
            Button button = Instantiate(control.Style == DevButtonStyle.Danger ? dangerButtonTemplate : buttonTemplate, holder);
            button.gameObject.SetActive(true);
            var view = new ControlView
            {
                control = control,
                label = ChildText(button.transform, "Button Label"),
                element = button.GetComponent<LayoutElement>(),
            };
            controls.Add(view);
            button.onClick.AddListener(() => Run(scope, control.Run));
            return button.gameObject;
        }
        #endregion

        #region Running
        void Run(DevScope scope, Func<DevResult> action)
        {
            DevResult result;
            try
            {
                // Checked at the click, not only when the page greys a group: a tool can unload the scene the next one needs.
                result = DevToolCatalog.Available(scope, out string why) ? action() : DevResult.Refused(why);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                result = DevResult.Refused($"{exception.GetType().Name}: {exception.Message}");
            }
            Print(result);
            RefreshAll();
        }

        void Print(DevResult result)
        {
            if (string.IsNullOrEmpty(result.Message)) return;
            Debug.Log($"[DevTools] {result.Message}");
            log.Add($"<color=#{ColorUtility.ToHtmlStringRGB(result.Ok ? good : bad)}>{result.Message}</color>");
            while (log.Count > LogLines) log.RemoveAt(0);
            logText.text = string.Join("\n", log);
        }

        void RefreshAll()
        {
            foreach (ControlView view in controls)
            {
                string text = view.control.Label();
                view.label.text = text;
                if (view.element != null) view.element.preferredWidth = Mathf.Max(64f, view.label.GetPreferredValues(text).x + 30f);
            }
            RefreshStatus();
            RefreshHeld();
        }

        void RefreshStatus()
        {
            statusText.text = DevToolCatalog.Status();
            foreach (Section section in sections)
            {
                bool live = DevToolCatalog.Available(section.scope, out string why);
                section.group.alpha = live ? 1f : 0.45f;
                section.group.interactable = live;
                section.note.gameObject.SetActive(!live);
                if (!live) section.note.text = why;
            }
            if (current == null) return;
            bool browsable = DevToolCatalog.Available(current.tab.Scope, out _);
            browserGroup.alpha = browsable ? 1f : 0.45f;
            browserGroup.interactable = browsable;
            if (browserLive == browsable) return;
            browserLive = browsable;
            ApplyFilter();
        }

        void RefreshHeld()
        {
            if (current == null || current.cells == null) return;
            foreach (Cell cell in current.cells)
                if (cell.entry.Held != null) cell.tile.SetSelected(cell.entry.Held());
        }
        #endregion

        #region Browser
        void ShowTab(TabView view)
        {
            if (current != null)
            {
                if (current.cells != null)
                    foreach (Cell cell in current.cells) cell.root.SetActive(false);
                foreach (Button chip in current.chips) chip.gameObject.SetActive(false);
                foreach (GameObject option in current.options) option.SetActive(false);
            }
            current = view;
            foreach (TabView tab in tabs) tab.button.SetActive(tab == view);
            if (view.cells == null) BuildCells(view);
            foreach (Button chip in view.chips) chip.gameObject.SetActive(true);
            foreach (GameObject option in view.options) option.SetActive(true);
            chipRow.gameObject.SetActive(view.chips.Count > 0);
            optionRow.gameObject.SetActive(view.options.Count > 0);
            grid.cellSize = view.tab.UnitTiles ? unitCellSize : itemCellSize;
            if (searchField.placeholder is TMP_Text placeholder) placeholder.text = view.tab.SearchHint;
            searchField.SetTextWithoutNotify("");
            ApplyFilter();
            RefreshAll();
            gridScroll.verticalNormalizedPosition = 1f;
        }

        void BuildCells(TabView view)
        {
            view.cells = new List<Cell>();
            RectTransform template = view.tab.UnitTiles ? unitCellTemplate : itemCellTemplate;
            foreach (DevEntry entry in view.tab.Entries())
            {
                RectTransform root = Instantiate(template, grid.transform);
                root.name = entry.Id;
                CollectionTile tile = root.GetComponentInChildren<CollectionTile>(true);
                if (view.tab.UnitTiles) tile.SetUnit(entry.Icon, entry.TypeIcon, entry.Rarity, true, false);
                else tile.SetItem(entry.Icon, entry.Rarity, true, false);
                // An Image with no sprite draws a white square; entries without art show their name only.
                if (entry.Icon == null)
                {
                    Transform icon = tile.transform.Find("Mask/Icon");
                    if (icon != null) icon.gameObject.SetActive(false);
                }
                ChildText(root, "Name").text = entry.Name;
                var cell = new Cell { entry = entry, root = root.gameObject, tile = tile };
                // The tile's own Clicked event also fires on keyboard focus, which would give on every arrow press.
                tile.GetComponent<Button>().onClick.AddListener(() => Pick(view, cell));
                view.cells.Add(cell);
                if (!string.IsNullOrEmpty(entry.Group) && !view.chipGroups.Contains(entry.Group))
                {
                    if (view.chipGroups.Count == 0) AddChip(view, null, "All", Color.white);
                    view.chipGroups.Add(entry.Group);
                    AddChip(view, entry.Group, entry.Group, entry.GroupColour);
                }
            }
            foreach (DevControl option in view.tab.Options) view.options.Add(AddControl(option, optionRow, view.tab.Scope));
            RefreshChips(view);
        }

        void AddChip(TabView view, string group, string text, Color colour)
        {
            Button chip = Instantiate(chipTemplate, chipRow);
            chip.name = text;
            TMP_Text label = ChildText(chip.transform, "Label");
            label.text = text;
            label.color = colour;
            chip.onClick.AddListener(() =>
            {
                Click();
                view.chip = group;
                RefreshChips(view);
                ApplyFilter();
            });
            view.chips.Add(chip);
        }

        void RefreshChips(TabView view)
        {
            for (int i = 0; i < view.chips.Count; i++)
            {
                // Chip 0 is All; the rest follow chipGroups.
                string group = i == 0 ? null : view.chipGroups[i - 1];
                bool active = group == view.chip;
                Transform chip = view.chips[i].transform;
                chip.Find("Fill").GetComponent<Image>().enabled = active;
                TMP_Text label = ChildText(chip, "Label");
                if (active) label.color = chipInk;
                else label.color = i == 0 ? Color.white : EntryColour(view, group);
            }
        }

        static Color EntryColour(TabView view, string group)
        {
            foreach (Cell cell in view.cells)
                if (cell.entry.Group == group) return cell.entry.GroupColour;
            return Color.white;
        }

        void ApplyFilter()
        {
            if (current == null || current.cells == null) return;
            if (!DevToolCatalog.Available(current.tab.Scope, out string reason))
            {
                ShowEmpty(reason);
                return;
            }
            string[] words = searchField.text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int shown = 0;
            for (int i = 0; i < current.cells.Count; i++)
            {
                Cell cell = current.cells[i];
                bool match = current.chip == null || cell.entry.Group == current.chip;
                foreach (string word in words)
                    if (!cell.entry.Search.Contains(word)) { match = false; break; }
                cell.root.SetActive(match);
                cell.root.transform.SetSiblingIndex(i);
                if (match) shown++;
            }
            // With nothing typed and no chip picked, the last picks lead the grid.
            if (words.Length == 0 && current.chip == null)
            {
                List<string> recent = Recent(current.tab);
                for (int i = recent.Count - 1; i >= 0; i--)
                    foreach (Cell cell in current.cells)
                        if (cell.entry.Id == recent[i]) cell.root.transform.SetAsFirstSibling();
            }
            emptyText.gameObject.SetActive(shown == 0);
            if (shown == 0) emptyText.text = "Nothing matches. Clear the search or pick All.";
        }

        void ShowEmpty(string text)
        {
            if (current != null && current.cells != null)
                foreach (Cell cell in current.cells) cell.root.SetActive(false);
            emptyText.gameObject.SetActive(true);
            emptyText.text = text;
        }

        void PickFirst()
        {
            if (current == null || current.cells == null) return;
            Cell first = null;
            int best = int.MaxValue;
            foreach (Cell cell in current.cells)
            {
                if (!cell.root.activeSelf) continue;
                int index = cell.root.transform.GetSiblingIndex();
                if (index < best) { best = index; first = cell; }
            }
            if (first != null) Pick(current, first);
            // Enter drops focus; take it back so the next search needs no click.
            searchField.ActivateInputField();
        }

        void Pick(TabView view, Cell cell)
        {
            Click();
            Run(view.tab.Scope, () => view.tab.Pick(cell.entry));
            List<string> recent = Recent(view.tab);
            recent.Remove(cell.entry.Id);
            recent.Insert(0, cell.entry.Id);
            while (recent.Count > RecentCount) recent.RemoveAt(recent.Count - 1);
            PlayerPrefs.SetString(RecentKey(view.tab), string.Join("|", recent));
        }

        static string RecentKey(DevBrowserTab tab) => "DevTools.Recent." + tab.Name;

        static List<string> Recent(DevBrowserTab tab) =>
            new(PlayerPrefs.GetString(RecentKey(tab), "").Split('|', StringSplitOptions.RemoveEmptyEntries));
        #endregion

        #region Helpers
        static void Click() => IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);

        static TMP_Text ChildText(Transform parent, string child)
        {
            Transform found = parent.Find(child);
            if (found == null)
            {
                Debug.LogError($"DevToolsPage: {parent.name} has no child '{child}'.");
                return null;
            }
            return found.GetComponent<TMP_Text>();
        }
        #endregion
    }
}
