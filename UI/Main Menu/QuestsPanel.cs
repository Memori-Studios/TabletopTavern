using System.Collections.Generic;
using Memori.Localization;
using Memori.Steamworks;
using TMPro;
using UnityEngine;

namespace TJ.MainMenu
{
    /// <summary>
    /// The Quests board: every Steam achievement in <see cref="AchievementRegistry.All"/>, with
    /// whether the player has earned it and, for the stat-backed ones, how far along they are. State
    /// is read from Steam on every open and never cached across sessions, so it cannot drift from
    /// what Steam shows. Achievements not yet registered on the backend read as locked until they
    /// are; the button that opens this panel is hidden outside SPELLS builds for that reason.
    ///
    /// Incomplete quests are listed first, each group in registry order.
    /// </summary>
    public class QuestsPanel : MainMenuPanel
    {
        [Header("Quests")]
        [SerializeField] private QuestCatalogSO catalog;
        [SerializeField] private QuestCard cardPrefab;
        [SerializeField] private Transform cardsParent;
        [SerializeField] private TMP_Text completedCountText;
        [SerializeField] private TMP_Text steamOfflineText;

        private readonly List<QuestCard> _spawnedCards = new();
        private bool _isOpen;

        public override void OpenPanel()
        {
            base.OpenPanel();
            _isOpen = true;
            // A cold launch into the menu can beat the stats arriving from Steam. Refresh once when they do.
            SteamAchievements.StatsReceived -= OnStatsReceived;
            SteamAchievements.StatsReceived += OnStatsReceived;
            Refresh();
        }

        public override void ClosePanel()
        {
            base.ClosePanel();
            _isOpen = false;
            SteamAchievements.StatsReceived -= OnStatsReceived;
        }

        private void OnStatsReceived()
        {
            if (_isOpen) Refresh();
        }

        private void Refresh()
        {
            foreach (QuestCard card in _spawnedCards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            _spawnedCards.Clear();

            bool steamAvailable = SteamAchievements.StatusAvailable;
            steamOfflineText.text = LocalizationManager.Instance.GetText("QuestSteamOffline");
            steamOfflineText.gameObject.SetActive(!steamAvailable);

            IReadOnlyList<AchievementDefinition> all = AchievementRegistry.All;
            var rows = new List<(int order, AchievementDefinition def, QuestStatus status)>(all.Count);
            int completed = 0;
            for (int i = 0; i < all.Count; i++)
            {
                QuestStatus status = SteamAchievements.GetStatus(all[i].Id);
                if (status.Unlocked) completed++;
                rows.Add((i, all[i], status));
            }

            // Incomplete first, registry order inside each group. List.Sort is not stable, so the
            // registry index is part of the key.
            rows.Sort((a, b) =>
            {
                int byState = a.status.Unlocked.CompareTo(b.status.Unlocked);
                return byState != 0 ? byState : a.order.CompareTo(b.order);
            });

            foreach ((int _, AchievementDefinition def, QuestStatus status) in rows)
            {
                if (!catalog.TryGet(def.Id, out QuestCatalogSO.Entry art))
                {
                    Debug.LogError($"[QuestsPanel] {def.Id} has no row in {catalog.name}. Run Sync With Registry on the catalog.");
                    continue;
                }

                // Parent passed to Instantiate on purpose: a card that starts life unparented is a root
                // canvas for one frame and can lose its sorting settings (see the run-setup card notes).
                QuestCard card = Instantiate(cardPrefab, cardsParent);
                card.Load(def, art, status);
                _spawnedCards.Add(card);
            }

            completedCountText.text = string.Format(LocalizationManager.Instance.GetText("QuestsCompletedCount"), completed, all.Count);
        }

        private void OnDestroy()
        {
            SteamAchievements.StatsReceived -= OnStatsReceived;
        }

        /// <summary>How many quests are complete, for the main-menu button counter.</summary>
        public static int CountCompleted()
        {
            if (!SteamAchievements.StatusAvailable) return 0;

            int completed = 0;
            foreach (AchievementDefinition def in AchievementRegistry.All)
            {
                if (SteamAchievements.IsUnlocked(def.Id)) completed++;
            }
            return completed;
        }
    }
}
