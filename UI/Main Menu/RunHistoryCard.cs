using System;
using System.Globalization;
using Memori.Localization;
using Memori.SaveData;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// One row in the Run History list: hero portrait, hero name, difficulty and act, date, gold,
    /// and the outcome. Clicking it asks <see cref="RunHistoryPanel"/> to show that run's warband.
    /// Display only beyond that click; every value comes off the <see cref="RunRecord"/>.
    /// </summary>
    public class RunHistoryCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private Image heroImage;
        [SerializeField] private TMP_Text heroNameText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text outcomeText;

        [Header("Hover / selection")]
        [SerializeField] private Image highlightImage;

        // The highlight sits over the text, so it stays a wash rather than a fill.
        private const float HOVER_ALPHA = 0.18f;
        private const float SELECTED_ALPHA = 0.38f;

        private RunRecord record;
        private Action<RunRecord> onSelected;
        private bool isSelected;
        private bool isHovered;
        // Portrait loads are async; a card destroyed by a refresh must not write into a dead Image.
        private bool isAlive = true;

        public RunRecord Record => record;

        private void Awake()
        {
            if (highlightImage != null) highlightImage.enabled = false;
        }

        public void Load(RunRecord _record, Action<RunRecord> _onSelected)
        {
            record = _record;
            onSelected = _onSelected;

            LocalizationManager loc = LocalizationManager.Instance;
            Hero hero = HeroData.GetHeroByID(record.heroID);
            DifficultyLevel difficultyData = DifficultyData.GetDifficultyLevelData(record.difficulty);

            heroNameText.text = loc.GetText(hero.HeroName);

            string difficultyName = loc.GetText(difficultyData.difficultyName);
            string act = string.Format(loc.GetText("RunHistoryActReached"), record.actReached);
            string date = record.EndedAtUtc.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
            summaryText.text = $"{loc.GetText("Level")} {(int)record.difficulty}: {difficultyName}  |  {act}  |  {date}";

            goldText.text = $"<color={ColorData.Gold}>{record.goldAtEnd}</color> {loc.GetText("Gold")}  |  {LeaderboardRow.FormatTime(record.playTimeSeconds)}";

            outcomeText.text = FormatOutcome(record.outcome, loc);

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected?.Invoke(record));

            LoadPortrait(record.heroID);
        }

        public static string FormatOutcome(RunOutcome outcome, LocalizationManager loc)
        {
            return outcome switch
            {
                RunOutcome.Win => $"<color={ColorData.Positive}>{loc.GetText("Victory")}</color>",
                RunOutcome.Loss => $"<color={ColorData.Negative}>{loc.GetText("Defeated")}</color>",
                _ => $"<color={ColorData.Secondary}>{loc.GetText("RunOutcomeAbandoned")}</color>",
            };
        }

        private async void LoadPortrait(int heroID)
        {
            Sprite sprite = await TabletopTavernData.Instance.LoadHeroSpriteAsync(heroID);
            if (!isAlive || heroImage == null) return;
            heroImage.sprite = sprite;
            heroImage.enabled = sprite != null;
        }

        public void SetSelected(bool _selected)
        {
            isSelected = _selected;
            RefreshHighlight();
        }

        private void RefreshHighlight()
        {
            if (highlightImage == null) return;
            highlightImage.enabled = isSelected || isHovered;
            Color c = highlightImage.color;
            c.a = isSelected ? SELECTED_ALPHA : HOVER_ALPHA;
            highlightImage.color = c;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            RefreshHighlight();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            RefreshHighlight();
        }

        private void OnDestroy()
        {
            isAlive = false;
        }
    }
}
