using System;
using System.Collections;
using System.Globalization;
using Memori.Audio;
using Memori.Localization;
using Memori.Steamworks;
using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    // The newest Steam announcement on the main menu. It stays hidden until the post and its art have loaded.
    public class SteamNewsCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public const string SeenKey = "last_seen_news_id";

        [SerializeField] private SteamAppIds steamAppIds;
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform pop;
        [SerializeField] private RectTransform lift;
        [SerializeField] private RawImage art;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text date;
        [SerializeField] private TMP_Text link;
        [SerializeField] private CanvasGroup newChip;
        [SerializeField] private Graphic highlight;

        [Header("Hover")]
        [SerializeField] private float hoverRise = 4f;
        [SerializeField] private float hoverScale = 1.02f;
        [SerializeField] private float hoverArtZoom = 1.04f;
        [SerializeField] private float hoverSpeed = 12f;
        [SerializeField] private Color titleHover = new(0.965f, 0.863f, 0.596f, 1f);
        [SerializeField] private Color linkHover = new(0.941f, 0.788f, 0.471f, 1f);

        private SteamNewsPost _post;
        private float _hover;
        private bool _pointerOver;
        private bool _keyboardFocus;
        private Color _titleRest;
        private Color _linkRest;
        private float _highlightAlpha;

        private void Awake()
        {
            _titleRest = title.color;
            _linkRest = link.color;
            _highlightAlpha = highlight.color.a;
            SetShown(false);
            ApplyHover(0f);
            button.onClick.AddListener(OnClick);
        }

        private async void Start()
        {
            if (steamAppIds == null)
            {
                Debug.LogError("[SteamNewsCard] steamAppIds is not set; the news card stays hidden.");
                return;
            }

            SteamNewsPost post = await SteamNewsFeed.GetLatest(steamAppIds.ReleaseAppId);
            if (this == null || post == null) return;

            _post = post;
            art.texture = post.Art;
            title.text = post.Title;
            RefreshDate();
            newChip.gameObject.SetActive(PlayerPrefs.GetString(SeenKey, "") != post.Id);
            LocalizationManager.Instance.OnLocalizedStringsLoaded += RefreshDate;
            StartCoroutine(Reveal());
        }

        private void OnDestroy()
        {
            if (LocalizationManager.HasInstance)
                LocalizationManager.Instance.OnLocalizedStringsLoaded -= RefreshDate;
        }

        #region Reveal
        // Waits for the menu panel to be visible, so the card's own rise is seen rather than played behind a fade.
        private IEnumerator Reveal()
        {
            CanvasGroup panel = transform.parent != null ? transform.parent.GetComponentInParent<CanvasGroup>() : null;
            while (panel != null && panel.alpha < 0.99f)
                yield return null;

            SetShown(true);
            yield return UIJuice.Open(group, pop);
        }

        private void SetShown(bool shown)
        {
            if (!shown) group.alpha = 0f;
            group.interactable = shown;
            group.blocksRaycasts = shown;
        }
        #endregion

        #region Date
        private void RefreshDate()
        {
            if (_post == null) return;
            CultureInfo culture = LocalizationSettings.SelectedLocale != null
                ? LocalizationSettings.SelectedLocale.Identifier.CultureInfo
                : null;
            date.text = FormatDate(_post.PostedUtc, culture ?? CultureInfo.CurrentCulture);
        }

        // The culture's long date without the weekday: "September 27, 2026", "27. September 2026", "2026年9月27日".
        private static string FormatDate(DateTime utc, CultureInfo culture)
        {
            string pattern = culture.DateTimeFormat.LongDatePattern.Replace("dddd", "").Trim(' ', ',', '،');
            return utc.ToLocalTime().ToString(pattern, culture);
        }
        #endregion

        #region Hover and focus
        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerOver = true;
            if (button.IsInteractable())
                IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
        }

        public void OnPointerExit(PointerEventData eventData) => _pointerOver = false;

        public void OnSelect(BaseEventData eventData) => _keyboardFocus = eventData is not PointerEventData && !PointerUsedLast();

        public void OnDeselect(BaseEventData eventData) => _keyboardFocus = false;

        private static bool PointerUsedLast()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return false;
            if (keyboard == null) return true;
            return mouse.lastUpdateTime > keyboard.lastUpdateTime;
        }

        private void Update()
        {
            float target = (_pointerOver || _keyboardFocus) && group.interactable ? 1f : 0f;
            if (Mathf.Approximately(_hover, target)) return;
            ApplyHover(Mathf.MoveTowards(_hover, target, Time.unscaledDeltaTime * hoverSpeed));
        }

        private void ApplyHover(float t)
        {
            _hover = t;
            float e = UIJuice.EaseOutCubic(t);
            lift.anchoredPosition = new Vector2(0f, hoverRise * e);
            float s = Mathf.Lerp(1f, hoverScale, e);
            lift.localScale = new Vector3(s, s, 1f);

            float uv = 1f / Mathf.Lerp(1f, hoverArtZoom, e);
            art.uvRect = new Rect((1f - uv) * 0.5f, (1f - uv) * 0.5f, uv, uv);

            Color glow = highlight.color;
            glow.a = _highlightAlpha * e;
            highlight.color = glow;
            title.color = Color.Lerp(_titleRest, titleHover, e);
            link.color = Color.Lerp(_linkRest, linkHover, e);
        }
        #endregion

        #region Click
        private void OnClick()
        {
            if (_post == null) return;
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            StartCoroutine(UIJuice.Punch(pop));

            PlayerPrefs.SetString(SeenKey, _post.Id);
            PlayerPrefs.Save();
            if (newChip.gameObject.activeSelf)
                StartCoroutine(PopAway(newChip));

            SteamStatic.OpenWebPage(_post.Url);
        }

        // A spent marker pops away rather than fading in place.
        private static IEnumerator PopAway(CanvasGroup chip)
        {
            Transform t = chip.transform;
            yield return UIJuice.Punch(t, 1.06f, 0.07f, 0.01f);
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / 0.16f)
            {
                float s = Mathf.Lerp(1f, 0.9f, k);
                t.localScale = new Vector3(s, s, 1f);
                chip.alpha = 1f - k;
                yield return null;
            }
            chip.gameObject.SetActive(false);
            chip.alpha = 1f;
            t.localScale = Vector3.one;
        }
        #endregion
    }
}
