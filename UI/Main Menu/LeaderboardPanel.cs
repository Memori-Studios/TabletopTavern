using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Memori.Localization;
using Memori.Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// The Steam leaderboards, one board at a time: fastest Godking completions, or the Deepest
    /// March past act 3. Best first, with the player's own row tinted. Everything is fetched from
    /// Steam on every open and never cached across sessions. When the player sits below the top
    /// cut, a second block shows the rows around them so they can still see where they stand.
    /// </summary>
    public class LeaderboardPanel : MainMenuPanel
    {
        [Header("Leaderboard")]
        [SerializeField] private LeaderboardRow rowPrefab;
        [SerializeField] private Transform rowsParent;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text statusText;
        // Each swaps to the other board, so only the one that leads away from the current board is shown.
        [SerializeField] private Button showDeepestBoardButton;
        [SerializeField] private Button showGodkingBoardButton;

        private enum Board { GodkingTime, DeepestMarch }
        private Board _board = Board.GodkingTime;
        // The Deepest March board ships with the Spell Update, alongside March On itself.
#if SPELLS
        private const bool DEEPEST_BOARD_AVAILABLE = true;
#else
        private const bool DEEPEST_BOARD_AVAILABLE = false;
#endif

        private const int TOP_COUNT = 100;
        private const int AROUND_ME_BEFORE = 3;
        private const int AROUND_ME_AFTER = 3;

        private readonly List<LeaderboardRow> _spawnedRows = new();
        private int _openSerial;

        public override void SetUp(MainMenu _mainMenu)
        {
            base.SetUp(_mainMenu);
            showDeepestBoardButton.onClick.RemoveAllListeners();
            showDeepestBoardButton.onClick.AddListener(() => ShowBoard(Board.DeepestMarch));
            showGodkingBoardButton.onClick.RemoveAllListeners();
            showGodkingBoardButton.onClick.AddListener(() => ShowBoard(Board.GodkingTime));
        }

        public override void OpenPanel()
        {
            base.OpenPanel();
            _board = Board.GodkingTime;
            _ = Refresh();
        }

        private void ShowBoard(Board board)
        {
            if (board == _board) return;
            _board = board;
            _ = Refresh();
        }

        public override void ClosePanel()
        {
            base.ClosePanel();
            _openSerial++;
            ClearRows();
        }

        private async Task Refresh()
        {
            int serial = ++_openSerial;
            ClearRows();

            bool depth = _board == Board.DeepestMarch;
            string boardName = depth ? SteamLeaderboards.DEEPEST_MARCH_BOARD : SteamLeaderboards.GODKING_TIME_BOARD;
            Func<int, string> formatScore = depth ? LeaderboardRow.FormatDepth : LeaderboardRow.FormatTime;
            showDeepestBoardButton.gameObject.SetActive(!depth && DEEPEST_BOARD_AVAILABLE);
            showGodkingBoardButton.gameObject.SetActive(depth);

            LocalizationManager loc = LocalizationManager.Instance;
            subtitleText.text = loc.GetText(depth ? "LeaderboardSubtitleDeepest" : "LeaderboardSubtitle");

            if (!SteamLeaderboards.Available)
            {
                statusText.text = loc.GetText("QuestSteamOffline");
                statusText.gameObject.SetActive(true);
                return;
            }

            statusText.text = loc.GetText("LeaderboardLoading");
            statusText.gameObject.SetActive(true);

            List<LeaderboardRowData> top = await SteamLeaderboards.GetTop(boardName, TOP_COUNT);
            // The panel may have closed, been reopened or switched boards while Steam was answering.
            if (serial != _openSerial || this == null) return;

            bool meInTop = false;
            foreach (LeaderboardRowData row in top)
            {
                Spawn(row, formatScore);
                meInTop |= row.IsMe;
            }

            List<LeaderboardRowData> aroundMe = meInTop ? null : await SteamLeaderboards.GetAroundMe(boardName, AROUND_ME_BEFORE, AROUND_ME_AFTER);
            if (serial != _openSerial || this == null) return;

            if (aroundMe != null && aroundMe.Count > 0)
            {
                // A gap row would need its own prefab; the rank numbers already make the jump obvious.
                foreach (LeaderboardRowData row in aroundMe)
                {
                    if (row.Rank <= TOP_COUNT) continue;
                    Spawn(row, formatScore);
                }
            }

            if (top.Count == 0)
            {
                statusText.text = loc.GetText(depth ? "LeaderboardEmptyDeepest" : "LeaderboardEmpty");
                return;
            }

            LeaderboardRowData? mine = FindMe(top) ?? FindMe(aroundMe);
            statusText.text = mine.HasValue
                ? string.Format(loc.GetText("LeaderboardYourBest"), mine.Value.Rank, formatScore(mine.Value.Score))
                : loc.GetText(depth ? "LeaderboardNoDepth" : "LeaderboardNoTime");
        }

        private static LeaderboardRowData? FindMe(List<LeaderboardRowData> rows)
        {
            if (rows == null) return null;
            foreach (LeaderboardRowData row in rows)
                if (row.IsMe) return row;
            return null;
        }

        private void Spawn(LeaderboardRowData data, Func<int, string> formatScore)
        {
            // Parent passed to Instantiate on purpose: see the run-setup card notes on root canvases.
            LeaderboardRow row = Instantiate(rowPrefab, rowsParent);
            row.Load(data, formatScore);
            _spawnedRows.Add(row);
        }

        private void ClearRows()
        {
            foreach (LeaderboardRow row in _spawnedRows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            _spawnedRows.Clear();
        }
    }
}
