using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Memori.Localization;
using Memori.SaveData;
using Memori.Steamworks;
using Memori.Tooltip;
using Memori.UI;
using TabletopTavern.Leaderboards;
using TJ.Settings;
using TJ.Spells;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// The Records section of the Collection: Quests, Run History and Leaderboards. CollectionPanel owns the rail and
    /// the page header; this owns each page's body and the side panel that replaces the detail panel on these pages.
    /// Everything is read on demand and nothing here writes: quests come from Steam, runs from the player save, boards
    /// from Steam each time the page opens.
    /// </summary>
    public class CollectionRecords : MonoBehaviour
    {
        public enum Page { Quests, Runs, Boards }
        private enum Board { Godking, Deepest }

        [Header("Shared")]
        [SerializeField] private GameObject side;
        [SerializeField] private ScrollRect sideScroll;
        [SerializeField] private float revertDelay = 0.2f;

        [Header("Quests")]
        [SerializeField] private QuestCatalogSO catalog;
        [SerializeField] private GameObject questsPage;
        [SerializeField] private ScrollRect questsScroll;
        // Two columns rather than a grid, so the rows take whatever width the page has at any UI scale.
        [SerializeField] private Transform[] questColumns;
        // Below this page width (UI Scale 125%) two columns leave each row too narrow to read, so one is used.
        [SerializeField] private float twoColumnWidth = 880f;
        [SerializeField] private RecordsQuestRow questRowTemplate;
        [SerializeField] private TMP_Text steamOffline;
        [SerializeField] private GameObject questSide;
        [SerializeField] private Image ringFill;
        [SerializeField] private TMP_Text ringCount;
        [SerializeField] private TMP_Text ringOf;
        [SerializeField] private TMP_Text questsLabel;
        [SerializeField] private TMP_Text percentText;
        [SerializeField] private GameObject closestGroup;
        [SerializeField] private TMP_Text closestLabel;
        [SerializeField] private Transform closestList;
        [SerializeField] private GameObject latestGroup;
        [SerializeField] private TMP_Text latestLabel;
        [SerializeField] private Transform latestList;

        [Header("Run History")]
        [SerializeField] private GameObject runsPage;
        [SerializeField] private ScrollRect runsScroll;
        [SerializeField] private GameObject runColumns;
        [SerializeField] private TMP_Text[] runColumnLabels;
        [SerializeField] private Transform runList;
        [SerializeField] private RecordsRunRow runRowTemplate;
        [SerializeField] private GameObject runsEmpty;
        [SerializeField] private TMP_Text runsEmptyText;
        [SerializeField] private GameObject runSide;
        [SerializeField] private Image cardPortrait;
        [SerializeField] private TMP_Text cardEyebrow;
        [SerializeField] private TMP_Text cardName;
        [SerializeField] private TMP_Text cardLine;
        [SerializeField] private TMP_Text[] cardValues;
        [SerializeField] private TMP_Text[] cardLabels;
        [SerializeField] private TMP_Text armyLabel;
        [SerializeField] private Transform armyGrid;
        [SerializeField] private GameObject reserveGroup;
        [SerializeField] private TMP_Text reserveLabel;
        [SerializeField] private Transform reserveGrid;
        [SerializeField] private GameObject gearGroup;
        [SerializeField] private TMP_Text gearLabel;
        [SerializeField] private Transform gearGrid;
        [SerializeField] private GameObject spellsGroup;
        [SerializeField] private TMP_Text spellsLabel;
        [SerializeField] private Transform spellsGrid;
        [SerializeField] private CollectionMiniUnit miniUnitTemplate;
        [SerializeField] private CollectionTile itemTileTemplate;

        [Header("Leaderboards")]
        [SerializeField] private GameObject boardsPage;
        [SerializeField] private ScrollRect boardsScroll;
        [SerializeField] private GameObject boardHeader;
        [SerializeField] private GameObject boardFilterSlot;
        [SerializeField] private CollectionTab godkingTab;
        [SerializeField] private CollectionTab deepestTab;
        // Picks the Godking board: every hero together, or one hero's board.
        [SerializeField] private SettingStepper heroPicker;
        [SerializeField] private GameObject heroPickerRow;
        [SerializeField] private RecordsFilterToggle filter;
        [SerializeField] private TMP_Text boardSubtitle;
        [SerializeField] private TMP_Text rankColumn;
        [SerializeField] private TMP_Text nameColumn;
        [SerializeField] private TMP_Text secondColumn;
        [SerializeField] private TMP_Text scoreColumn;
        [SerializeField] private Transform boardList;
        [SerializeField] private RecordsBoardRow boardRowTemplate;
        [SerializeField] private TMP_Text boardGapTemplate;
        [SerializeField] private TMP_Text boardStatus;
        [SerializeField] private GameObject boardSide;
        [SerializeField] private TMP_Text standingLabel;
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text ofText;
        [SerializeField] private GameObject standingStrip;
        [SerializeField] private TMP_Text[] standingValues;
        [SerializeField] private TMP_Text[] standingLabels;
        [SerializeField] private GameObject nextGroup;
        [SerializeField] private TMP_Text nextLabel;
        [SerializeField] private RecordsBoardRow nextRow;
        [SerializeField] private GameObject yoursGroup;
        [SerializeField] private TMP_Text yoursLabel;
        [SerializeField] private TMP_Text yoursNote;
        [SerializeField] private Transform yoursList;

        // Run History, Leaderboards and the quests not yet on the Steam backend ship with the Spell Update.
        // Readonly rather than #if blocks, so the pre-release path compiles and can be checked in a SPELLS Editor.
#if SPELLS
        public static readonly bool SpellsRelease = true;
#else
        public static readonly bool SpellsRelease = false;
#endif

        /// <summary>The quests this build lists: all of them with SPELLS, before it only those live on Steam.</summary>
        public static IReadOnlyList<AchievementDefinition> ListedQuests => SpellsRelease ? AchievementRegistry.All : AchievementRegistry.Live;

        /// <summary>Slots from this index up are the reserve row, matching HUDPanel's layout.</summary>
        private const int MAIN_ARMY_SLOTS = 10;
        private const int TOP_COUNT = 10;
        private const int AROUND_ME = 2;
        private const int SUMMARY_COUNT = 3;

        private readonly List<RecordsQuestRow> _questRows = new();
        private int _questColumnCount;
        private readonly List<RecordsQuestRow> _closestRows = new();
        private readonly List<RecordsQuestRow> _latestRows = new();
        private readonly List<RecordsRunRow> _runRows = new();
        private readonly List<GameObject> _cardItems = new();
        private readonly List<GameObject> _boardItems = new();
        private readonly List<GameObject> _yoursItems = new();

        private List<RunRecord> _runs;
        private int _keptRun = -1;
        private RecordsRunRow _hoveredRun;
        private Coroutine _pending;

        private Board _board = Board.Godking;
        private bool _friends;
        // 0 is the board for every hero together; otherwise the hero whose board is shown.
        private int _heroId;
        private readonly List<int> _pickerHeroes = new();
        // Bumped on every board fetch and on hide, so a late Steam reply for an old view is dropped.
        private int _serial;

        private Page? _page;
        private TMP_Text _subtitle;
        // Portraits load async; a hover can ask for a second one before the first lands, so only the newest may write.
        private int _portraitRequest;

        private static string T(string key) => LocalizationManager.Instance.GetText(key);

        public Page? Current => _page;

        #region Lifecycle

        public void SetUp()
        {
            godkingTab.Button.onClick.AddListener(() => SwitchBoard(Board.Godking));
            deepestTab.Button.onClick.AddListener(() => SwitchBoard(Board.Deepest));
            filter.Changed += friends =>
            {
                Memori.Audio.IAudioRequester.Instance.PlaySFX(Memori.Audio.SFXData.ButtonClick);
                _friends = friends;
                _ = RefreshBoard();
            };
            filter.Show(false);
            heroPicker.onValueChanged.AddListener(index =>
            {
                _heroId = index > 0 && index <= _pickerHeroes.Count ? _pickerHeroes[index - 1] : 0;
                _ = RefreshBoard();
            });
            Localize();
            Hide();
        }

        private void Localize()
        {
            steamOffline.text = T("QuestSteamOffline");
            questsLabel.text = T("questsButton");
            closestLabel.text = T("QuestsClosest");
            latestLabel.text = T("QuestsLatest");

            runColumnLabels[0].text = T("RecordsCommander");
            runColumnLabels[1].text = T("RunHistoryTime");
            runColumnLabels[2].text = T("Gold");
            runColumnLabels[3].text = T("RecordsOutcome");
            runsEmptyText.text = T("RunHistoryEmpty");
            cardLabels[0].text = T("Act");
            cardLabels[1].text = T("RunHistoryTime");
            cardLabels[2].text = T("RunHistoryBattles");
            cardLabels[3].text = T("enemiesSlain");
            cardLabels[4].text = T("RunHistoryGoldEarned");
            cardLabels[5].text = T("RunHistoryRenownEarned");
            armyLabel.text = T("RunHistoryArmy");
            reserveLabel.text = T("Reserve");
            gearLabel.text = T("RunHistoryGear");
            spellsLabel.text = T("Spells");

            godkingTab.SetLabel(T("LeaderboardBoardGodking"));
            deepestTab.SetLabel(T("LeaderboardBoardDeepest"));
            filter.SetLabels(T("LeaderboardEveryone"), T("LeaderboardFriends"));
            rankColumn.text = T("RecordsRank");
            standingLabel.text = T("LeaderboardYourStanding");
            standingLabels[0].text = T("LeaderboardBestLabel");
            nextLabel.text = T("LeaderboardNextUp");
            yoursNote.text = T("LeaderboardFromRuns");
        }

        /// <summary>Shows one page and fills the header title and subtitle for it.</summary>
        public void Show(Page page, TMP_Text title, TMP_Text subtitle)
        {
            _page = page;
            _subtitle = subtitle;
            questsPage.SetActive(page == Page.Quests);
            runsPage.SetActive(page == Page.Runs);
            boardsPage.SetActive(page == Page.Boards);
            boardHeader.SetActive(page == Page.Boards);
            boardFilterSlot.SetActive(page == Page.Boards);
            // The side panel must be active before its lists are built, or their Awake never runs.
            side.SetActive(true);
            questSide.SetActive(page == Page.Quests);
            runSide.SetActive(page == Page.Runs);
            boardSide.SetActive(page == Page.Boards);
            sideScroll.verticalNormalizedPosition = 1f;
            _serial++;

            switch (page)
            {
                case Page.Quests:
                    title.text = T("questsButton");
                    SteamAchievements.StatsReceived -= OnStatsReceived;
                    SteamAchievements.StatsReceived += OnStatsReceived;
                    RefreshQuests();
                    questsScroll.verticalNormalizedPosition = 1f;
                    break;
                case Page.Runs:
                    title.text = T("runHistoryButton");
                    ShowRuns();
                    break;
                case Page.Boards:
                    title.text = T("leaderboardButton");
                    subtitle.text = string.Empty;
                    _ = RefreshBoard();
                    break;
            }
        }

        public void Hide()
        {
            _page = null;
            _serial++;
            StopPending();
            SteamAchievements.StatsReceived -= OnStatsReceived;
            questsPage.SetActive(false);
            runsPage.SetActive(false);
            boardsPage.SetActive(false);
            boardHeader.SetActive(false);
            boardFilterSlot.SetActive(false);
            side.SetActive(false);
        }

        private void OnDestroy()
        {
            SteamAchievements.StatsReceived -= OnStatsReceived;
        }

        #endregion

        #region Rail counts

        public static int CountUnlockedQuests()
        {
            if (!SteamAchievements.StatusAvailable) return 0;
            return ListedQuests.Count(def => SteamAchievements.IsUnlocked(def.Id));
        }

        public static int CountRuns() => SaveDataHandler.GetRunHistory().Count;

        #endregion

        #region Quests

        private void OnStatsReceived()
        {
            // A cold launch into the menu can beat the stats arriving from Steam.
            if (_page == Page.Quests) RefreshQuests();
        }

        private void RefreshQuests()
        {
            IReadOnlyList<AchievementDefinition> all = ListedQuests;
            var rows = new List<(int order, AchievementDefinition def, QuestStatus status)>(all.Count);
            for (int i = 0; i < all.Count; i++)
                rows.Add((i, all[i], SteamAchievements.GetStatus(all[i].Id)));

            // Locked first, registry order inside each part. List.Sort is not stable, so the index is part of the key.
            rows.Sort((a, b) =>
            {
                int byState = a.status.Unlocked.CompareTo(b.status.Unlocked);
                return byState != 0 ? byState : a.order.CompareTo(b.order);
            });

            int shown = 0;
            foreach ((int _, AchievementDefinition def, QuestStatus status) in rows)
            {
                if (!catalog.TryGet(def.Id, out QuestCatalogSO.Entry art))
                {
                    Debug.LogError($"[CollectionRecords] {def.Id} has no row in {catalog.name}. Run Sync With Registry on the catalog.");
                    continue;
                }
                FillQuest(RowAt(_questRows, shown, questColumns[0]), def, art, status);
                shown++;
            }
            for (int i = shown; i < _questRows.Count; i++) _questRows[i].gameObject.SetActive(false);
            PlaceQuestRows(QuestColumnsForWidth());

            bool steam = SteamAchievements.StatusAvailable;
            steamOffline.gameObject.SetActive(!steam);

            int unlocked = rows.Count(r => r.status.Unlocked);
            ringFill.fillAmount = all.Count > 0 ? (float)unlocked / all.Count : 0f;
            ringCount.text = unlocked.ToString();
            ringOf.text = string.Format(T("QuestsOfTotal"), all.Count);
            percentText.text = string.Format(T("QuestsPercentUnlocked"), all.Count > 0 ? Mathf.RoundToInt(unlocked * 100f / all.Count) : 0);
            _subtitle.text = string.Format(T("QuestsCompletedCount"), unlocked, all.Count);

            var closest = rows.Where(r => !r.status.Unlocked && r.status.HasProgress && r.status.ProgressMax > 0)
                .OrderByDescending(r => (float)r.status.Progress / r.status.ProgressMax)
                .Take(SUMMARY_COUNT).ToList();
            FillSummary(closestGroup, _closestRows, closestList, closest);

            var latest = rows.Where(r => r.status.Unlocked && r.status.UnlockTime.HasValue)
                .OrderByDescending(r => r.status.UnlockTime.Value)
                .Take(SUMMARY_COUNT).ToList();
            FillSummary(latestGroup, _latestRows, latestList, latest);
        }

        private void LateUpdate()
        {
            // The page width changes with UI Scale and window size; reflow only when the column count would change.
            if (_page == Page.Quests && QuestColumnsForWidth() != _questColumnCount) PlaceQuestRows(QuestColumnsForWidth());
        }

        private int QuestColumnsForWidth() => questsScroll.viewport.rect.width >= twoColumnWidth ? 2 : 1;

        // Left, right, left: the reading order a grid has. The rows are already in display order.
        private void PlaceQuestRows(int columns)
        {
            _questColumnCount = columns;
            questColumns[1].gameObject.SetActive(columns > 1);
            int placed = 0;
            foreach (RecordsQuestRow row in _questRows)
            {
                if (!row.gameObject.activeSelf) continue;
                row.transform.SetParent(questColumns[placed % columns], false);
                row.transform.SetSiblingIndex(placed / columns);
                placed++;
            }
        }

        private void FillSummary(GameObject group, List<RecordsQuestRow> pool, Transform parent,
            List<(int order, AchievementDefinition def, QuestStatus status)> rows)
        {
            group.SetActive(rows.Count > 0);
            for (int i = 0; i < rows.Count; i++)
            {
                if (!catalog.TryGet(rows[i].def.Id, out QuestCatalogSO.Entry art)) continue;
                FillQuest(RowAt(pool, i, parent), rows[i].def, art, rows[i].status);
            }
            for (int i = rows.Count; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        private RecordsQuestRow RowAt(List<RecordsQuestRow> pool, int index, Transform parent)
        {
            while (pool.Count <= index)
            {
                // Parent passed to Instantiate on purpose: an unparented row is a root canvas for one frame.
                RecordsQuestRow row = Instantiate(questRowTemplate, parent);
                pool.Add(row);
            }
            pool[index].gameObject.SetActive(true);
            return pool[index];
        }

        private static void FillQuest(RecordsQuestRow row, AchievementDefinition def, QuestCatalogSO.Entry art, QuestStatus status)
        {
            row.Set(status.Unlocked ? art.unlocked : art.locked, T("Quest_" + def.Id), T("Quest_" + def.Id + "_Desc"), status);
        }

        #endregion

        #region Run History

        private void ShowRuns()
        {
            if (_runs == null) BuildRuns();

            int wins = _runs.Count(r => r.outcome == RunOutcome.Win);
            _subtitle.text = string.Format(T("RunHistoryCount"), _runs.Count, wins);

            bool any = _runs.Count > 0;
            runsEmpty.SetActive(!any);
            runColumns.SetActive(any);
            runSide.SetActive(any);
            if (!any)
            {
                side.SetActive(false);
                return;
            }
            KeepRun(_keptRun < 0 ? 0 : _keptRun);
            runsScroll.verticalNormalizedPosition = 1f;
        }

        private void BuildRuns()
        {
            _runs = SaveDataHandler.GetRunHistory();
            for (int i = 0; i < _runs.Count; i++)
            {
                RecordsRunRow row = Instantiate(runRowTemplate, runList);
                row.gameObject.SetActive(true);
                row.Index = i;
                row.Set(_runs[i]);
                row.Hovered += OnRunHovered;
                row.Unhovered += OnRunUnhovered;
                row.Clicked += OnRunClicked;
                _runRows.Add(row);
            }
        }

        private void KeepRun(int index)
        {
            if (_runs.Count == 0) return;
            _keptRun = Mathf.Clamp(index, 0, _runs.Count - 1);
            for (int i = 0; i < _runRows.Count; i++) _runRows[i].SetKept(i == _keptRun);
            ShowCard(_runs[_keptRun]);
        }

        private void OnRunHovered(RecordsRunRow row)
        {
            _hoveredRun = row;
            StopPending();
            ShowCard(row.Record);
        }

        private void OnRunUnhovered(RecordsRunRow row)
        {
            if (_hoveredRun != row) return;
            _hoveredRun = null;
            StopPending();
            // A short grace period, so sliding down the list does not flash the kept run between rows.
            _pending = StartCoroutine(After(revertDelay, () =>
            {
                if (_hoveredRun == null && _keptRun >= 0) ShowCard(_runs[_keptRun]);
            }));
        }

        private void OnRunClicked(RecordsRunRow row)
        {
            Memori.Audio.IAudioRequester.Instance.PlaySFX(Memori.Audio.SFXData.ButtonClick);
            StopPending();
            KeepRun(row.Index);
        }

        /// <summary>Moves the kept run up or down the list, for keyboard and controller.</summary>
        public void MoveRun(int direction)
        {
            if (_page != Page.Runs || _runs == null || _runs.Count == 0) return;
            int next = Mathf.Clamp(_keptRun + direction, 0, _runs.Count - 1);
            if (next == _keptRun) return;
            Memori.Audio.IAudioRequester.Instance.PlaySFX(Memori.Audio.SFXData.ButtonHover);
            StopPending();
            _hoveredRun = null;
            KeepRun(next);
            Reveal(runsScroll, (RectTransform)_runRows[next].transform);
        }

        private void ShowCard(RunRecord run)
        {
            foreach (GameObject item in _cardItems)
                if (item != null) Destroy(item);
            _cardItems.Clear();

            LocalizationManager loc = LocalizationManager.Instance;
            Hero hero = HeroData.GetHeroByID(run.heroID);
            cardPortrait.enabled = false;
            LoadCardPortrait(run.heroID);
            cardEyebrow.text = run.EndedAtUtc.ToLocalTime().ToString("D", CultureInfo.CurrentCulture);
            cardName.text = loc.GetText(hero.HeroName);
            cardLine.text = $"{RecordsFormat.Difficulty(run.difficulty)}  <color=#8E9A9A>·</color>  " +
                            $"<color={RecordsFormat.OutcomeColour(run.outcome)}>{RecordsFormat.Outcome(run.outcome)}</color>";

            // The March has no acts of its own: a run that marched on finished act III, and its battles are in the count beside it.
            cardValues[0].text = MemoriUI.ConvertNumberToRomanNumeral(Mathf.Clamp(run.actReached, 1, TabletopTavernConstants.FINAL_STORY_ACT));
            cardValues[1].text = RecordsFormat.Time(run.playTimeSeconds);
            cardValues[2].text = run.battlesFought.ToString("N0", CultureInfo.CurrentCulture);
            cardValues[3].text = run.enemiesSlain.ToString("N0", CultureInfo.CurrentCulture);
            cardValues[4].text = run.goldEarned.ToString("N0", CultureInfo.CurrentCulture);
            cardValues[5].text = run.renownEarned > 0 ? $"+{run.renownEarned}" : run.renownEarned.ToString();

            int reserves = 0;
            for (int i = 0; i < run.army.Length; i++)
            {
                SquadToLoad squad = run.army[i];
                if (squad.isEmptySquad || squad.maxUnitCount <= 0) continue;
                bool inReserve = i >= MAIN_ARMY_SLOTS;
                if (inReserve) reserves++;
                CollectionMiniUnit mini = Instantiate(miniUnitTemplate, inReserve ? reserveGrid : armyGrid);
                mini.gameObject.SetActive(true);
                mini.Set(TabletopTavernData.Instance.GetUnitIcon(squad.UnitName), CollectionDetailPanel.TierColour(squad.UnitName));
                string title = loc.GetText(squad.UnitName.ToString());
                string description = squad.PrestigeTrait != UnitAttribute.None
                    ? loc.GetText(squad.PrestigeTrait.ToString())
                    : loc.GetText(TabletopTavernData.Instance.GetUnitTypeFromUnitName(squad.UnitName).ToString());
                mini.gameObject.AddComponent<MemoriTooltipTrigger>().SetUpToolTip(title, description);
                _cardItems.Add(mini.gameObject);
            }
            reserveGroup.SetActive(reserves > 0);

            int gear = 0;
            foreach (GearID id in run.gear)
            {
                if (id == GearID.None) continue;
                Gear data = GearData.GetGear(id);
                CollectionTile tile = ItemTile(gearGrid, SpriteData.GetSprite(data.GearName), (Color)ColorData.GetGearRarityColor(data.GearRarity));
                string description = KeywordText.ForTooltip(string.Format(loc.GetText(id + "Desc"), data.GearModifierValue));
                tile.gameObject.AddComponent<MemoriTooltipTrigger>().SetUpToolTip(loc.GetText(id + "Name"), description);
                gear++;
            }
            gearGroup.SetActive(gear > 0);

            int spells = 0;
            foreach (Spell spell in run.spells)
            {
                SpellData data = SpellRegistry.Get(spell);
                if (data == null) continue;
                CollectionTile tile = ItemTile(spellsGrid, data.SpellSprite, ColorData.GetRaceDisplayColor(data.Race));
                tile.gameObject.AddComponent<MemoriTooltipTrigger>().SetUpToolTip(
                    loc.GetText(data.Spell.ToString()), KeywordText.ForTooltip(data.GetLocalizedSpellDescription()));
                spells++;
            }
            spellsGroup.SetActive(spells > 0);
            sideScroll.verticalNormalizedPosition = 1f;
        }

        private async void LoadCardPortrait(int heroID)
        {
            int request = ++_portraitRequest;
            Sprite sprite = await TabletopTavernData.Instance.LoadHeroSpriteAsync(heroID);
            if (this == null || request != _portraitRequest) return;
            cardPortrait.sprite = sprite;
            cardPortrait.enabled = sprite != null;
        }

        private CollectionTile ItemTile(Transform parent, Sprite icon, Color colour)
        {
            CollectionTile tile = Instantiate(itemTileTemplate, parent);
            tile.gameObject.SetActive(true);
            tile.SetItem(icon, colour, true, false);
            _cardItems.Add(tile.gameObject);
            return tile;
        }

        #endregion

        #region Leaderboards

        /// <summary>Switches between the two boards, for keyboard and controller.</summary>
        public void StepBoard()
        {
            if (_page != Page.Boards) return;
            SwitchBoard(_board == Board.Godking ? Board.Deepest : Board.Godking);
        }

        public void ShowBoard(bool deepest)
        {
            if (_page != Page.Boards) return;
            SwitchBoard(deepest ? Board.Deepest : Board.Godking);
        }

        public void ToggleFriends()
        {
            if (_page != Page.Boards) return;
            Memori.Audio.IAudioRequester.Instance.PlaySFX(Memori.Audio.SFXData.ButtonClick);
            _friends = !_friends;
            filter.Show(_friends);
            _ = RefreshBoard();
        }

        private void SwitchBoard(Board board)
        {
            if (board == _board) return;
            Memori.Audio.IAudioRequester.Instance.PlaySFX(Memori.Audio.SFXData.ButtonClick);
            _board = board;
            _ = RefreshBoard();
        }

        private async Task RefreshBoard()
        {
            int serial = ++_serial;
            ClearBoard();

            bool depth = _board == Board.Deepest;
            Func<int, string> format = depth ? RecordsFormat.Depth : score => RecordsFormat.Time(score);
            godkingTab.SetActive(!depth);
            deepestTab.SetActive(depth);
            heroPickerRow.SetActive(!depth);
            filter.Show(_friends);
            boardSubtitle.text = T(depth ? "LeaderboardSubtitleDeepest" : "LeaderboardSubtitle");
            nameColumn.text = T("RecordsCommander");
            secondColumn.text = T("LeaderboardWorldRank");
            secondColumn.gameObject.SetActive(_friends);
            scoreColumn.text = T(depth ? "LeaderboardBoardDeepest" : "RunHistoryTime");
            standingLabels[1].text = T(_friends ? "LeaderboardWorldRank" : "LeaderboardTopLabel");
            FillYours(depth);
            ShowStanding(null, 0, null, format, depth);

            if (!SteamLeaderboards.Available)
            {
                SetStatus(T("QuestSteamOffline"));
                return;
            }
            SetStatus(T("LeaderboardLoading"));

            // The server names this season's boards; Steam holds the scores.
            LeaderboardSeason season = await LeaderboardClient.GetBoardsAsync();
            if (serial != _serial || this == null) return;
            if (season == null)
            {
                SetStatus(T("LeaderboardServerOffline"));
                return;
            }
            FillHeroPicker(season);
            string boardName = depth ? season.MarchBoard : _heroId == 0 ? season.AllHeroesBoard : season.HeroBoard(_heroId);
            if (string.IsNullOrEmpty(boardName))
            {
                SetStatus(T("LeaderboardNotOpen"));
                return;
            }
            // The board for every hero names the hero on each row.
            bool showHero = !depth && _heroId == 0;

            if (_friends)
            {
                List<LeaderboardRowData> friends = await SteamLeaderboards.GetFriends(boardName);
                if (serial != _serial || this == null) return;
                friends.Sort((a, b) => a.Rank.CompareTo(b.Rank));
                if (friends.Count == 0)
                {
                    SetStatus(T(depth ? "LeaderboardFriendsEmptyDeepest" : "LeaderboardFriendsEmpty"));
                    return;
                }
                SetStatus(null);
                for (int i = 0; i < friends.Count; i++)
                    SpawnRow(i + 1, friends[i], format, $"#{friends[i].Rank:N0}", showHero);
                int mine = friends.FindIndex(f => f.IsMe);
                ShowStanding(mine >= 0 ? friends[mine] : null, friends.Count, mine > 0 ? friends[mine - 1] : null, format, depth, mine + 1);
                return;
            }

            int entries = await SteamLeaderboards.GetEntryCount(boardName);
            List<LeaderboardRowData> top = await SteamLeaderboards.GetTop(boardName, TOP_COUNT);
            if (serial != _serial || this == null) return;
            // Steam's range is inclusive and returns one row more than asked.
            top.RemoveAll(r => r.Rank > TOP_COUNT);
            int meInTop = top.FindIndex(r => r.IsMe);

            List<LeaderboardRowData> around = meInTop >= 0 ? new List<LeaderboardRowData>() : await SteamLeaderboards.GetAroundMe(boardName, AROUND_ME, AROUND_ME);
            if (serial != _serial || this == null) return;

            if (top.Count == 0)
            {
                SetStatus(T(depth ? "LeaderboardEmptyDeepest" : "LeaderboardEmpty"));
                return;
            }
            SetStatus(null);
            foreach (LeaderboardRowData row in top) SpawnRow(row.Rank, row, format, null, showHero);
            List<LeaderboardRowData> below = around.Where(r => r.Rank > TOP_COUNT).ToList();
            if (below.Count > 0)
            {
                TMP_Text gap = Instantiate(boardGapTemplate, boardList);
                gap.gameObject.SetActive(true);
                _boardItems.Add(gap.gameObject);
                foreach (LeaderboardRowData row in below) SpawnRow(row.Rank, row, format, null, showHero);
            }

            List<LeaderboardRowData> all = top.Concat(below).ToList();
            int me = all.FindIndex(r => r.IsMe);
            LeaderboardRowData? above = null;
            if (me >= 0)
            {
                int aboveIndex = all.FindIndex(r => r.Rank == all[me].Rank - 1);
                if (aboveIndex >= 0) above = all[aboveIndex];
            }
            ShowStanding(me >= 0 ? all[me] : null, entries, above, format, depth);
        }

        // "All heroes" first, then every hero the server has a board for, in hero order.
        private void FillHeroPicker(LeaderboardSeason season)
        {
            List<int> heroes = season.boards.Where(b => b.kind == "godking" && b.heroId.HasValue).Select(b => b.heroId.Value).OrderBy(id => id).ToList();
            if (heroes.SequenceEqual(_pickerHeroes) && heroPicker.options.Count == heroes.Count + 1) return;
            _pickerHeroes.Clear();
            _pickerHeroes.AddRange(heroes);
            var options = new List<TMP_Dropdown.OptionData> { new(T("LeaderboardAllHeroes")) };
            options.AddRange(heroes.Select(id => new TMP_Dropdown.OptionData(HeroName(id))));
            heroPicker.ClearOptions();
            heroPicker.AddOptions(options);
            int index = _pickerHeroes.IndexOf(_heroId);
            if (index < 0) _heroId = 0;
            heroPicker.SetValueWithoutNotify(index < 0 ? 0 : index + 1);
        }

        private static string HeroName(int heroId) => T(HeroData.GetHeroByID(heroId).HeroName);

        private void SpawnRow(int place, LeaderboardRowData data, Func<int, string> format, string second, bool showHero)
        {
            RecordsBoardRow row = Instantiate(boardRowTemplate, boardList);
            row.gameObject.SetActive(true);
            LeaderboardEntryDetails details = LeaderboardEntryDetails.Decode(data.Details);
            string shownName = showHero && details.Valid ? $"{data.PlayerName}  <color={ColorData.Secondary}>{HeroName(details.HeroId)}</color>" : data.PlayerName;
            row.Set(place, shownName, format(data.Score), data.IsMe, _boardItems.Count % 2 == 0, second);
            _boardItems.Add(row.gameObject);
        }

        /// <param name="place">The place shown in the big number; defaults to the global rank.</param>
        private void ShowStanding(LeaderboardRowData? mine, int total, LeaderboardRowData? above, Func<int, string> format, bool depth, int place = 0)
        {
            bool has = mine.HasValue;
            standingStrip.SetActive(has);
            rankText.gameObject.SetActive(has);
            if (!has)
            {
                ofText.text = T(depth ? "LeaderboardNoDepth" : "LeaderboardNoTime");
                nextGroup.SetActive(false);
                return;
            }

            LeaderboardRowData me = mine.Value;
            int shown = place > 0 ? place : me.Rank;
            rankText.text = $"#{shown:N0}";
            ofText.text = total > 0 ? string.Format(T(_friends ? "LeaderboardOfFriends" : "LeaderboardOfCommanders"), total.ToString("N0", CultureInfo.CurrentCulture)) : string.Empty;
            standingValues[0].text = format(me.Score);
            standingValues[1].text = _friends
                ? $"#{me.Rank:N0}"
                : total > 0 ? $"{Mathf.Max(1, Mathf.CeilToInt(me.Rank * 100f / total))}%" : "-";

            nextGroup.SetActive(above.HasValue);
            if (above.HasValue)
            {
                LeaderboardRowData next = above.Value;
                nextRow.Set(_friends ? shown - 1 : next.Rank, next.PlayerName, format(next.Score), false, false);
            }
        }

        // The player's own best runs for the board, read from Run History rather than Steam.
        private void FillYours(bool depth)
        {
            foreach (GameObject item in _yoursItems)
                if (item != null) Destroy(item);
            _yoursItems.Clear();

            List<RunRecord> runs = SaveDataHandler.GetRunHistory();
            List<RunRecord> picks = depth
                ? runs.Where(r => r.marchBattles > 0).OrderByDescending(r => r.marchBattles).ThenByDescending(r => DifficultyRules.Rank(r.difficulty)).Take(SUMMARY_COUNT).ToList()
                : runs.Where(r => r.outcome == RunOutcome.Win && DifficultyRules.IsHardest(r.difficulty) && (_heroId == 0 || r.heroID == _heroId))
                    .OrderBy(r => r.playTimeSeconds).Take(SUMMARY_COUNT).ToList();

            yoursGroup.SetActive(picks.Count > 0);
            yoursLabel.text = T(depth ? "LeaderboardYourMarches" : "LeaderboardYourGodkingWins");
            for (int i = 0; i < picks.Count; i++)
            {
                RunRecord run = picks[i];
                string heroName = T(HeroData.GetHeroByID(run.heroID).HeroName);
                string score = depth
                    ? $"{string.Format(T("RunHistoryMarchBattles"), run.marchBattles)} · {RecordsFormat.Difficulty(run.difficulty)}"
                    : RecordsFormat.Time(run.playTimeSeconds);
                RecordsBoardRow row = Instantiate(boardRowTemplate, yoursList);
                row.gameObject.SetActive(true);
                row.Set(i + 1, heroName, score, false, i % 2 == 0);
                _yoursItems.Add(row.gameObject);
            }
        }

        private void SetStatus(string text)
        {
            boardStatus.gameObject.SetActive(!string.IsNullOrEmpty(text));
            boardStatus.text = text ?? string.Empty;
        }

        private void ClearBoard()
        {
            foreach (GameObject item in _boardItems)
                if (item != null) Destroy(item);
            _boardItems.Clear();
        }

        #endregion

        #region Helpers

        private IEnumerator After(float seconds, Action action)
        {
            // Realtime: the Collection opens over a battle that Settings may have paused.
            yield return new WaitForSecondsRealtime(seconds);
            _pending = null;
            action();
        }

        private void StopPending()
        {
            if (_pending == null) return;
            StopCoroutine(_pending);
            _pending = null;
        }

        private static void Reveal(ScrollRect scroll, RectTransform target)
        {
            RectTransform viewport = scroll.viewport;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            Vector2 position = scroll.content.anchoredPosition;
            const float margin = 16f;
            if (bounds.max.y > viewport.rect.yMax) position.y -= bounds.max.y - viewport.rect.yMax + margin;
            else if (bounds.min.y < viewport.rect.yMin) position.y += viewport.rect.yMin - bounds.min.y + margin;
            scroll.content.anchoredPosition = position;
        }

        #endregion
    }
}
