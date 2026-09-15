using System.Collections.Generic;
using System.Threading.Tasks;
using Memori.Localization;
using Memori.Steamworks;
using TMPro;
using UnityEngine;

namespace TJ.MainMenu
{
    /// <summary>
    /// The Godking leaderboard: the fastest campaign completions on Steam, best first, with the
    /// player's own row tinted. Everything is fetched from Steam on every open and never cached
    /// across sessions. When the player sits below the top cut, a second block shows the rows
    /// around them so they can still see where they stand.
    /// </summary>
    public class LeaderboardPanel : MainMenuPanel
    {
        [Header("Leaderboard")]
        [SerializeField] private LeaderboardRow rowPrefab;
        [SerializeField] private Transform rowsParent;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text statusText;

        private const int TOP_COUNT = 100;
        private const int AROUND_ME_BEFORE = 3;
        private const int AROUND_ME_AFTER = 3;

        private readonly List<LeaderboardRow> _spawnedRows = new();
        private int _openSerial;

        public override void OpenPanel()
        {
            base.OpenPanel();
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

            LocalizationManager loc = LocalizationManager.Instance;
            subtitleText.text = loc.GetText("LeaderboardSubtitle");

            if (!SteamLeaderboards.Available)
            {
                statusText.text = loc.GetText("QuestSteamOffline");
                statusText.gameObject.SetActive(true);
                return;
            }

            statusText.text = loc.GetText("LeaderboardLoading");
            statusText.gameObject.SetActive(true);

            List<LeaderboardRowData> top = await SteamLeaderboards.GetTop(TOP_COUNT);
            // The panel may have closed, or been reopened, while Steam was answering.
            if (serial != _openSerial || this == null) return;

            bool meInTop = false;
            foreach (LeaderboardRowData row in top)
            {
                Spawn(row);
                meInTop |= row.IsMe;
            }

            List<LeaderboardRowData> aroundMe = meInTop ? null : await SteamLeaderboards.GetAroundMe(AROUND_ME_BEFORE, AROUND_ME_AFTER);
            if (serial != _openSerial || this == null) return;

            if (aroundMe != null && aroundMe.Count > 0)
            {
                // A gap row would need its own prefab; the rank numbers already make the jump obvious.
                foreach (LeaderboardRowData row in aroundMe)
                {
                    if (row.Rank <= TOP_COUNT) continue;
                    Spawn(row);
                }
            }

            if (top.Count == 0)
            {
                statusText.text = loc.GetText("LeaderboardEmpty");
                return;
            }

            LeaderboardRowData? mine = FindMe(top) ?? FindMe(aroundMe);
            statusText.text = mine.HasValue
                ? string.Format(loc.GetText("LeaderboardYourBest"), mine.Value.Rank, LeaderboardRow.FormatTime(mine.Value.Score))
                : loc.GetText("LeaderboardNoTime");
        }

        private static LeaderboardRowData? FindMe(List<LeaderboardRowData> rows)
        {
            if (rows == null) return null;
            foreach (LeaderboardRowData row in rows)
                if (row.IsMe) return row;
            return null;
        }

        private void Spawn(LeaderboardRowData data)
        {
            // Parent passed to Instantiate on purpose: see the run-setup card notes on root canvases.
            LeaderboardRow row = Instantiate(rowPrefab, rowsParent);
            row.Load(data);
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
