using Memori.Audio;
using Memori.Localization;
using Memori.SaveData;
using Memori.Tooltip;
using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// One hero in the run-setup roster: portrait, name, a frame in the metal of the best level won and a gem per level.
    /// Hovering lifts the tile; clicking picks the hero. The hero panel only ever shows the picked hero.
    /// </summary>
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class HeroRosterTile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float UnwonGemAlpha = 0.1f;

        [SerializeField] private Button button;
        [SerializeField] private Image portrait;
        [SerializeField] private Image frame;
        [Tooltip("Frame sprite per level won, Easy to Godking. Shown untinted; a missing entry falls back to the tinted plain frame.")]
        [SerializeField] private Sprite[] wonFrames;
        [SerializeField] private Image[] gems;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject lockedMark;
        [SerializeField] private GameObject selectedMark;
        [SerializeField] private Color lockedPortrait = new(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color lockedName = new(0.55f, 0.6f, 0.64f, 1f);

        private Hero hero;
        private PlayPanel playPanel;
        private MemoriTooltipTrigger tooltip;
        private bool listening;
        private bool unlocked;
        private Sprite plainFrame;

        public Hero Hero => hero;

        public void Load(Hero _hero, PlayPanel _playPanel)
        {
            hero = _hero;
            playPanel = _playPanel;
            if (tooltip == null) tooltip = GetComponent<MemoriTooltipTrigger>();

#if DEMO
            UnlockCondition condition = hero.DemoUnlockCondition;
#else
            UnlockCondition condition = hero.UnlockCondition;
#endif
            unlocked = SaveDataHandler.IsUnlockConditionUnlocked(condition, hero.HeroID);
#if DEMO
            if (SaveDataHandler.IsDevToolUser()) unlocked = true;
#endif

            nameText.text = LocalizationManager.Instance.GetText(hero.HeroName);
            nameText.color = unlocked ? Color.white : lockedName;
            portrait.color = unlocked ? Color.white : lockedPortrait;
            lockedMark.SetActive(!unlocked);
            LoadPortrait();

            bool[] won = unlocked ? DifficultyMetal.WonLevels(hero.HeroID) : new bool[DifficultyMetal.LevelCount];
            if (plainFrame == null) plainFrame = frame.sprite;
            int best = DifficultyMetal.BestRank(won);
            Sprite wonFrame = wonFrames != null && best >= 0 && best < wonFrames.Length ? wonFrames[best] : null;
            frame.sprite = wonFrame != null ? wonFrame : plainFrame;
            frame.color = wonFrame != null ? Color.white : DifficultyMetal.ForRank(best);
            for (int i = 0; i < gems.Length; i++)
            {
                bool gemWon = i < won.Length && won[i];
                gems[i].color = gemWon ? DifficultyMetal.ForRank(i) : DifficultyMetal.Unwon;
                CanvasGroup group = gems[i].GetComponent<CanvasGroup>();
                if (group != null) group.alpha = gemWon ? 1f : UnwonGemAlpha;
            }

            tooltip.enabled = !unlocked;
            if (!unlocked)
                tooltip.SetUpToolTip(LocalizationManager.Instance.GetText("Locked"),
                                     HeroBonusManager.GetLocalizedHeroUnlockDescription(hero, condition));

            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
            selectedMark.SetActive(false);
            if (!listening)
            {
                playPanel.OnActiveHeroChanged += OnActiveHeroChanged;
                listening = true;
            }
        }

        private async void LoadPortrait()
        {
            int heroID = hero.HeroID;
            Sprite sprite = await TabletopTavernData.Instance.LoadHeroSpriteAsync(heroID);
            if (this == null || hero.HeroID != heroID) return;
            if (sprite == null) Debug.LogError($"[HeroRosterTile] No portrait for hero {heroID}.");
            portrait.sprite = sprite;
        }

        private void OnClicked()
        {
            // Picking the hero already picked changes nothing, so it only answers the click.
            // A punch measures from the current scale, so it starts from the hovered rest, never mid-motion.
            transform.localScale = Vector3.one * (lifted ? HoverScale : 1f);
            if (playPanel.hero.HeroID == hero.HeroID)
            {
                Play(UIJuice.Punch(transform, 1.04f));
                return;
            }
            playPanel.LoadHeroes(hero, true);
            Play(PickRoutine());
            if (!unlocked && lockedMark != null) StartCoroutine(UIJuice.Shake((RectTransform)lockedMark.transform, 4f));
        }

        private void OnActiveHeroChanged(Hero activeHero) => selectedMark.SetActive(activeHero.HeroID == hero.HeroID);

        #region Feel
        private const float HoverLift = 6f, HoverScale = 1.04f, PressScale = 0.95f;
        private Coroutine motion;
        private Vector2 rest;
        private bool lifted, hovered;

        private void Play(System.Collections.IEnumerator routine)
        {
            if (!isActiveAndEnabled) return;
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(routine);
        }

        private void LiftTo(bool up, float scale)
        {
            // The roster's layout owns the resting position, so it is read again each time the tile leaves rest.
            if (!lifted) rest = ((RectTransform)transform).anchoredPosition;
            lifted = up;
            Play(UIJuice.Lift((RectTransform)transform, rest, up ? HoverLift : 0f, scale));
        }

        private System.Collections.IEnumerator PickRoutine()
        {
            yield return UIJuice.Punch(transform, 1.06f);
            if (selectedMark != null) yield return UIJuice.Punch(selectedMark.transform, 1.08f, 0.05f, 0.12f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            IAudioRequester.Instance.PlaySFX(SFXData.HoverHero);
            LiftTo(true, HoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            LiftTo(false, 1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Play(UIJuice.Lift((RectTransform)transform, rest, lifted ? HoverLift : 0f, PressScale, 0.03f));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            LiftTo(hovered, hovered ? HoverScale : 1f);
        }

        // Unity sends no exit to a tile that goes inactive, so a hidden roster would keep it raised.
        private void OnDisable()
        {
            motion = null;
            if (lifted) ((RectTransform)transform).anchoredPosition = rest;
            lifted = hovered = false;
            transform.localScale = Vector3.one;
        }
        #endregion

        private void OnDestroy()
        {
            if (listening && playPanel != null) playPanel.OnActiveHeroChanged -= OnActiveHeroChanged;
        }
    }
}
