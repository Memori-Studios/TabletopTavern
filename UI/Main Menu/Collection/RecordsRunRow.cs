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
    /// <summary>One run on the Records Run History page. Hover previews it, click keeps it, like a codex tile.</summary>
    public class RecordsRunRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private Image accent;
        [SerializeField] private Image portraitFrame;
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text heroName;
        [SerializeField] private TMP_Text meta;
        [SerializeField] private TMP_Text time;
        [SerializeField] private TMP_Text gold;
        [SerializeField] private TMP_Text outcome;

        [SerializeField] private Color idleFill = new(0.11f, 0.15f, 0.16f, 0.55f);
        [SerializeField] private Color hoverFill = new(0.15f, 0.21f, 0.22f, 1f);
        [SerializeField] private Color keptFill = new(0.16f, 0.23f, 0.24f, 1f);
        [SerializeField] private Color idleBorder = new(0.17f, 0.23f, 0.24f, 1f);
        [SerializeField] private Color keptBorder = new(0.69f, 0.54f, 0.24f, 0.6f);
        [SerializeField] private Color idleName = new(0.93f, 0.9f, 0.85f, 1f);
        [SerializeField] private Color keptName = new(0.91f, 0.75f, 0.42f, 1f);

        public event Action<RecordsRunRow> Hovered, Unhovered, Clicked;

        public RunRecord Record { get; private set; }
        public int Index { get; set; }

        private bool _kept, _hovered;

        private void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public void Set(RunRecord record)
        {
            Record = record;
            LocalizationManager loc = LocalizationManager.Instance;
            Hero hero = HeroData.GetHeroByID(record.heroID);
            heroName.text = loc.GetText(hero.HeroName);
            string date = record.EndedAtUtc.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
            meta.text = $"{RecordsFormat.Difficulty(record.difficulty)} · {RecordsFormat.Reached(record)} · {date}";
            time.text = RecordsFormat.Time(record.playTimeSeconds);
            gold.text = $"{record.goldAtEnd}";
            outcome.text = RecordsFormat.Outcome(record.outcome);
            Color outcomeColour = Hex(RecordsFormat.OutcomeColour(record.outcome));
            outcome.color = outcomeColour;
            portraitFrame.color = new Color(outcomeColour.r, outcomeColour.g, outcomeColour.b, 0.7f);
            portrait.enabled = false;
            CollectionDetailPanel.SetHeroPortrait(portrait, record.heroID);
            Refresh();
        }

        public void SetKept(bool kept)
        {
            _kept = kept;
            Refresh();
        }

        private void Refresh()
        {
            background.color = _kept ? keptFill : _hovered ? hoverFill : idleFill;
            border.color = _kept ? keptBorder : idleBorder;
            accent.enabled = _kept;
            heroName.color = _kept ? keptName : idleName;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh();
            Hovered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh();
            Unhovered?.Invoke(this);
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color colour) ? colour : Color.white;
    }
}
