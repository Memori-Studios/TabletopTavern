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
    /// Hovering previews the hero on the hero panel; clicking picks it.
    /// </summary>
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class HeroRosterTile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portrait;
        [SerializeField] private Image frame;
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
            bool unlocked = SaveDataHandler.IsUnlockConditionUnlocked(condition, hero.HeroID);
#if DEMO
            if (SaveDataHandler.IsDevToolUser()) unlocked = true;
#endif

            nameText.text = LocalizationManager.Instance.GetText(hero.HeroName);
            nameText.color = unlocked ? Color.white : lockedName;
            portrait.color = unlocked ? Color.white : lockedPortrait;
            lockedMark.SetActive(!unlocked);
            LoadPortrait();

            bool[] won = unlocked ? DifficultyMetal.WonLevels(hero.HeroID) : new bool[DifficultyMetal.LevelCount];
            frame.color = DifficultyMetal.ForRank(DifficultyMetal.BestRank(won));
            for (int i = 0; i < gems.Length; i++)
                gems[i].color = i < won.Length && won[i] ? DifficultyMetal.ForRank(i) : DifficultyMetal.Unwon;

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

        private void OnClicked() => playPanel.LoadHeroes(hero, true);

        private void OnActiveHeroChanged(Hero activeHero) => selectedMark.SetActive(activeHero.HeroID == hero.HeroID);

        public void OnPointerEnter(PointerEventData eventData)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.HoverHero);
            MemoriUI.BloomItemScale(transform, 1.025f, 0.1f);
            playPanel.ShowHeroDetailsBox(hero);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            MemoriUI.BloomItemScale(transform, 1f, 0.1f);
            playPanel.RevertToActiveHeroDetailsBox(hero);
        }

        private void OnDestroy()
        {
            if (listening && playPanel != null) playPanel.OnActiveHeroChanged -= OnActiveHeroChanged;
        }
    }
}
