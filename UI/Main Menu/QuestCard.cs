using System.Globalization;
using Memori.Localization;
using Memori.Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// One row on the Quests board: an achievement's icon, name, description, how many players
    /// have it, and its state. Purely a display; it has no click action. Name and description come
    /// from the localization table (Quest_&lt;Id&gt; / Quest_&lt;Id&gt;_Desc), the icon from
    /// <see cref="QuestCatalogSO"/>, and the state from <see cref="SteamAchievements.GetStatus"/>.
    /// </summary>
    public class QuestCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text globalPercentText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private CanvasGroup textGroup;

        [Header("Progress")]
        [SerializeField] private GameObject progressGroup;
        [SerializeField] private Image progressFill;

        [Header("Hover")]
        [SerializeField] private Image highlightImage;

        // Locked rows read as dimmed text next to the grey icon; the icon itself carries the state.
        private const float LOCKED_TEXT_ALPHA = 0.55f;

        private void Awake()
        {
            if (highlightImage != null) highlightImage.enabled = false;
        }

        public void Load(AchievementDefinition definition, QuestCatalogSO.Entry art, QuestStatus status)
        {
            LocalizationManager loc = LocalizationManager.Instance;
            string questName = loc.GetText("Quest_" + definition.Id);
            string description = loc.GetText("Quest_" + definition.Id + "_Desc");

            iconImage.sprite = status.Unlocked ? art.unlocked : art.locked;
            nameText.text = questName;
            descriptionText.text = description;
            textGroup.alpha = status.Unlocked ? 1f : LOCKED_TEXT_ALPHA;

            statusText.text = BuildStatusText(status, loc);

            progressGroup.SetActive(status.HasProgress);
            if (status.HasProgress)
                progressFill.fillAmount = status.ProgressMax > 0 ? (float)status.Progress / status.ProgressMax : 0f;

            // The Steam-style third line. Hidden rather than blank when Steam has not answered, so a
            // card without a figure does not carry an empty row.
            globalPercentText.gameObject.SetActive(status.HasGlobalPercent);
            if (status.HasGlobalPercent)
                globalPercentText.text = string.Format(loc.GetText("QuestGlobalPercent"), status.GlobalUnlockedPercent.ToString("0.#"));
        }

        private static string BuildStatusText(QuestStatus status, LocalizationManager loc)
        {
            if (status.Unlocked)
            {
                string completed = loc.GetText("QuestCompleted");
                if (!status.UnlockTime.HasValue) return completed;

                string date = status.UnlockTime.Value.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
                return completed + "\n" + date;
            }

            if (status.HasProgress)
                return string.Format(loc.GetText("QuestProgress"), status.Progress, status.ProgressMax);

            return loc.GetText("QuestLocked");
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (highlightImage != null) highlightImage.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (highlightImage != null) highlightImage.enabled = false;
        }
    }
}
