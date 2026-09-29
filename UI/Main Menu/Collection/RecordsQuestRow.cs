using System.Globalization;
using Memori.Localization;
using Memori.Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>One quest on the Records Quests page: icon, name, description and its Steam state. Display only.</summary>
    public class RecordsQuestRow : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private CanvasGroup textGroup;
        [SerializeField] private TMP_Text questName;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text date;
        [SerializeField] private GameObject progressBar;
        [SerializeField] private RectTransform progressFill;

        [SerializeField] private Color unlockedFill = new(0.16f, 0.22f, 0.24f, 0.55f);
        [SerializeField] private Color lockedFill = new(0.11f, 0.15f, 0.16f, 0.55f);
        [SerializeField] private Color unlockedName = new(0.91f, 0.75f, 0.42f, 1f);
        [SerializeField] private Color lockedName = new(0.93f, 0.9f, 0.85f, 1f);
        [SerializeField] private Color progressText = new(0.89f, 0.73f, 0.44f, 1f);
        [SerializeField] private Color lockedText = new(0.56f, 0.6f, 0.6f, 1f);
        // Locked quests read as dimmed text beside the grey icon; the icon carries the state.
        [SerializeField] private float lockedTextAlpha = 0.55f;

        public void Set(Sprite sprite, string nameText, string descriptionText, QuestStatus state)
        {
            LocalizationManager loc = LocalizationManager.Instance;
            icon.sprite = sprite;
            questName.text = nameText;
            description.text = descriptionText;
            questName.color = state.Unlocked ? unlockedName : lockedName;
            background.color = state.Unlocked ? unlockedFill : lockedFill;
            textGroup.alpha = state.Unlocked || state.HasProgress ? 1f : lockedTextAlpha;

            bool showProgress = !state.Unlocked && state.HasProgress;
            progressBar.SetActive(showProgress);
            date.gameObject.SetActive(state.Unlocked && state.UnlockTime.HasValue);

            if (state.Unlocked)
            {
                status.text = loc.GetText("QuestCompleted");
                status.color = HexColour(ColorData.Positive);
                if (state.UnlockTime.HasValue)
                    date.text = state.UnlockTime.Value.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
            }
            else if (showProgress)
            {
                status.text = string.Format(loc.GetText("QuestProgress"), state.Progress, state.ProgressMax);
                status.color = progressText;
                float fraction = state.ProgressMax > 0 ? Mathf.Clamp01((float)state.Progress / state.ProgressMax) : 0f;
                progressFill.anchorMax = new Vector2(fraction, 1f);
            }
            else
            {
                status.text = loc.GetText("QuestLocked");
                status.color = lockedText;
            }
        }

        private static Color HexColour(string hex) => ColorUtility.TryParseHtmlString(hex, out Color colour) ? colour : Color.white;
    }
}
