using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Memori.Audio;
using Memori.UI;
using System.Text.RegularExpressions;

namespace TJ
{
    /// <summary>
    /// The end-of-battle card and the kill and loss badges on the army bar. Only shows state; UIManager decides what it
    /// shows and what the buttons do. Built by EndBattlePanelBuilder.
    /// </summary>
    public class EndBattlePanelView : MonoBehaviour
    {
        #region Fields
        [SerializeField] private RectTransform card;
        [SerializeField] private CanvasGroup cardGroup;
        [SerializeField] private Image headerBand;
        [SerializeField] private float bandAlpha = 0.12f;
        [SerializeField] private Image headerIcon;
        [SerializeField] private UIFlare victoryHalo;
        [SerializeField] private Sprite victoryIcon;
        [SerializeField] private Sprite defeatIcon;
        [Tooltip("The Damage dealt panel's header icon, as on the Engagement panel.")]
        [SerializeField] private Sprite damageIcon;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text contextCaption;
        [SerializeField] private TMP_Text contextValue;
        [SerializeField] private TMP_Text slainValue;
        [SerializeField] private TMP_Text lossesValue;
        [SerializeField] private Button detailedStatsButton;
        [Tooltip("The ring round Detailed stats while its panel is open.")]
        [SerializeField] private GameObject detailedStatsOpen;
        [SerializeField] private TMP_Text defeatLine;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button rematchButton;
        [SerializeField] private EndBattleSquadBadge badgePrefab;
        [SerializeField] private float openSeconds = 0.35f;
        [SerializeField] private float openRise = 24f;
        #endregion

        public RectTransform Card => card;
        public Sprite DamageIcon => damageIcon;
        public Button DetailedStatsButton => detailedStatsButton;
        public Button ContinueButton => continueButton;
        public Button ExitButton => exitButton;
        public Button RematchButton => rematchButton;

        readonly List<EndBattleSquadBadge> badges = new();
        Vector2 restingPosition;
        bool restingKnown;

        static readonly Color Gold = new Color32(0xE9, 0xC0, 0x6A, 0xFF);

        public void Hide()
        {
            ClearBadges();
            gameObject.SetActive(false);
        }

        /// <summary>Fills the card, then fades it in. defeatText empty hides the defeat line; custom swaps Continue for Exit and Rematch.</summary>
        public void Show(bool won, string titleText, string caption, string context, string slain, string losses, string defeatText, bool custom)
        {
            gameObject.SetActive(true);
            // Keys and a controller stay on the card instead of wandering onto the battle HUD behind it.
            ContainedNavigation.Attach(gameObject);
            Color bad = Parse(ColorData.Negative);
            title.text = titleText;
            title.color = won ? Gold : bad;
            contextCaption.text = caption;
            contextValue.text = context;
            slainValue.text = slain;
            lossesValue.text = losses;
            finalSlain = slain;
            finalLosses = losses;
            headerIcon.sprite = won ? victoryIcon : defeatIcon;
            headerIcon.color = won ? Gold : bad;
            Color band = won ? Gold : bad;
            band.a = bandAlpha;
            headerBand.color = band;

            bool hasDefeatLine = !string.IsNullOrEmpty(defeatText);
            defeatLine.gameObject.SetActive(hasDefeatLine);
            if (hasDefeatLine) defeatLine.text = defeatText;
            continueButton.gameObject.SetActive(!custom);
            exitButton.gameObject.SetActive(custom);
            rematchButton.gameObject.SetActive(custom);
            SetStatsOpen(false);

            if (!restingKnown)
            {
                restingPosition = card.anchoredPosition;
                restingKnown = true;
            }
            StopAllCoroutines();
            shownWon = won;
            IAudioRequester.Instance.PlaySFX(won ? SFXData.Trumpet : SFXData.Failure);
            StartCoroutine(Open());
        }

        public void SetStatsOpen(bool open)
        {
            if (detailedStatsOpen != null) detailedStatsOpen.SetActive(open);
        }

        /// <summary>Puts the kill and loss badges on one army bar card; they go with the card if it is destroyed.</summary>
        public void AddBadge(RectTransform squadCard, int kills, int lost, bool mostSlain, string mostSlainText)
        {
            if (squadCard == null || badgePrefab == null) return;
            EndBattleSquadBadge badge = Instantiate(badgePrefab, squadCard);
            badge.Set(kills, lost, mostSlain, mostSlainText);
            // Hidden until the card has landed; Open deals them in.
            CanvasGroup group = badge.GetComponent<CanvasGroup>();
            if (group == null) group = badge.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            badges.Add(badge);
        }

        public void ClearBadges()
        {
            foreach (EndBattleSquadBadge badge in badges)
                if (badge != null) Destroy(badge.gameObject);
            badges.Clear();
        }

        // Unscaled, so the end-of-battle game speed does not slow it.
        IEnumerator Open()
        {
            cardGroup.alpha = 0f;
            cardGroup.interactable = false;
            float start = Time.unscaledTime;
            while (true)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - start) / Mathf.Max(openSeconds, 0.0001f));
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                cardGroup.alpha = eased;
                card.anchoredPosition = restingPosition + new Vector2(0f, openRise * (1f - eased));
                if (t >= 1f) break;
                yield return null;
            }
            cardGroup.interactable = true;

            // A victory lands with the halo behind the crest; the title stays still, it is left aligned. A defeat lands quietly.
            if (shownWon && victoryHalo != null) victoryHalo.Play();

            // The numbers count up while the badges deal in along the army bar; one sound for the whole deal.
            StartCoroutine(CountTo(slainValue, finalSlain, true));
            StartCoroutine(CountTo(lossesValue, finalLosses, false));
            var badgeRects = new List<RectTransform>();
            foreach (EndBattleSquadBadge badge in badges)
                if (badge != null) badgeRects.Add(badge.transform as RectTransform);
            if (badgeRects.Count > 0) IAudioRequester.Instance.PlaySFX(SFXData.SquadCardSpawn);
            StartCoroutine(UIJuice.Stagger(badgeRects));
            // The screen's one ambient motion: the header band breathes.
            StartCoroutine(UIJuice.GlowPulse(headerBand, 0.55f, 1f, 2.2f));
        }

        string finalSlain, finalLosses;
        bool shownWon;
        static readonly Regex FirstNumber = new(@"\d[\d,.\u00A0 ]*\d|\d");

        // Counts the first number in a finished line up from zero, keeping any words and colour tags around it.
        IEnumerator CountTo(TMP_Text label, string finalText, bool ticks)
        {
            if (label == null || string.IsNullOrEmpty(finalText)) yield break;
            // Skip digits inside rich-text tags: the losses line is wrapped in <color=#E3695E>.
            Match match = null;
            foreach (Match m in FirstNumber.Matches(finalText))
            {
                int open = finalText.LastIndexOf('<', m.Index), close = finalText.LastIndexOf('>', m.Index);
                if (open > close) continue;
                match = m;
                break;
            }
            if (match == null) yield break;
            string digits = Regex.Replace(match.Value, @"[^\d]", "");
            if (!int.TryParse(digits, out int target) || target == 0) yield break;
            string before = finalText.Substring(0, match.Index), after = finalText.Substring(match.Index + match.Length);
            float duration = Mathf.Lerp(0.35f, 0.8f, Mathf.Clamp01(target / 500f));
            yield return UIJuice.CountUp(label, target, duration, v => before + v.ToString("N0") + after,
                ticks ? () => IAudioRequester.Instance.PlaySFX(SFXData.TinyClick) : null);
            label.text = finalText;
        }

        static Color Parse(string hex) => ColorUtility.TryParseHtmlString(hex, out Color colour) ? colour : Color.white;
    }
}
