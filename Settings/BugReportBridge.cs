using System.Linq;
using Memori.Localization;
using Memori.Scenes;
using Memori.Utilities;
using Memori.Notifications;
using UnityEngine;

namespace TJ
{
    public class BugReportBridge : MonoBehaviour
    {
        [SerializeField] private ReportABugScreen reportABugScreen;

        private void OnEnable()
        {
            reportABugScreen.OnBlankSubmit.AddListener(OnBlankSubmit);
            reportABugScreen.LoadedModsProvider = DescribeLoadedMods;
            reportABugScreen.GameStateProvider = DescribeGameState;
        }

        private void OnDisable()
        {
            reportABugScreen.OnBlankSubmit.RemoveListener(OnBlankSubmit);
            reportABugScreen.LoadedModsProvider = null;
            reportABugScreen.GameStateProvider = null;
        }

        private static string DescribeGameState()
        {
            return SceneHandler.Instance.CurrentGameState.ToString();
        }

        // The boot snapshot of what ApplyModOverrides actually loaded, not modlist.json's current
        // state - enable/reorder edits made in the mod list UI only take effect on restart.
        private static string DescribeLoadedMods()
        {
            var loadedMods = ModLoadOrder.LoadedFolderNamesThisSession;
            if (loadedMods.Count == 0) return "None";
            return string.Join(", ", loadedMods.OrderBy(folderName => folderName));
        }

        private void OnBlankSubmit()
        {
            string localizedMessage = LocalizationManager.Instance.GetText("bugReportBlankSubmit");
            NotificationManager.Instance.ErrorNotification(localizedMessage);
        }
    }
}
