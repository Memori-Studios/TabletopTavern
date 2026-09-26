using Memori.Localization;
using Memori.UI;
using TMPro;
using UnityEngine;

namespace TJ.MainMenu
{
    /// <summary>
    /// The Quests / Run History / Leaderboard strip at the top of each of those three panels.
    /// Each panel carries its own copy with a different active tab.
    /// </summary>
    public class RecordsTabs : MonoBehaviour
    {
        public enum Tab { Quests, RunHistory, Leaderboard }

        [SerializeField] private MainMenu mainMenu;
        [SerializeField] private Tab activeTab;
        [SerializeField] private MemoriButtonV2 questsTab, runHistoryTab, leaderboardTab;
        [SerializeField] private Color activeLabelColor = new Color(0.93f, 0.76f, 0.35f);

        private void Awake()
        {
            questsTab.Button.onClick.AddListener(mainMenu.SwitchToQuestsPanel);
            runHistoryTab.Button.onClick.AddListener(mainMenu.SwitchToRunHistoryPanel);
            leaderboardTab.Button.onClick.AddListener(mainMenu.SwitchToLeaderboardPanel);
            LocalizationManager.Instance.OnLocalizedStringsLoaded += UpdateLabels;
            UpdateLabels();
            ShowActiveTab();
        }

        private void UpdateLabels()
        {
            questsTab.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("questsButton");
            runHistoryTab.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("runHistoryButton");
            leaderboardTab.GetComponentInChildren<TMP_Text>().text = LocalizationManager.Instance.GetText("leaderboardButton");
        }

        // The active tab is the page you are on, so it neither hovers nor clicks.
        private void ShowActiveTab()
        {
            SetTabState(questsTab, activeTab == Tab.Quests);
            SetTabState(runHistoryTab, activeTab == Tab.RunHistory);
            SetTabState(leaderboardTab, activeTab == Tab.Leaderboard);
        }

        private void SetTabState(MemoriButtonV2 tab, bool active)
        {
            tab.Button.interactable = !active;
            if (active) tab.GetComponentInChildren<TMP_Text>().color = activeLabelColor;
        }

        private void OnDestroy()
        {
            if (LocalizationManager.HasInstance)
                LocalizationManager.Instance.OnLocalizedStringsLoaded -= UpdateLabels;
        }
    }
}
