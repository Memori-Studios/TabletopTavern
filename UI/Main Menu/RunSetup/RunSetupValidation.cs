using System.Collections.Generic;
using Memori.Audio;
using Memori.Localization;
using Memori.UI;
using Memori.SaveData;
using TJ.Spells;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// The single "can this run start" readout, replacing six stacked LockedButton overlays.
    ///
    /// The old flow checked <c>lockedButton.gameObject.activeSelf</c> in a fixed order inside
    /// OnStartButtonClicked and returned on the first hit, so only one blocker was ever reported
    /// and the player fixed them one at a time. This collects every blocker at once and states
    /// them together.
    /// </summary>
    public class RunSetupValidation : MonoBehaviour
    {
        [SerializeField] private GameObject blockedRoot;
        [SerializeField] private GameObject readyRoot;
        [SerializeField] private TMP_Text blockedText;
        [SerializeField] private Button startButton;

        private readonly List<string> blockers = new();

        public bool CanStart => blockers.Count == 0;
        public IReadOnlyList<string> Blockers => blockers;

        /// <param name="feedback">The warband screen is on show, so a change of state is seen and heard.</param>
        public void Evaluate(PlayPanel playPanel, StartingArmyManager startingArmySection, Spell[] loadout, bool feedback = false)
        {
            blockers.Clear();

            if (!playPanel.HeroIsUnlocked)
            {
                blockers.Add(HeroBonusManager.GetLocalizedHeroUnlockDescription(playPanel.hero, playPanel.ActiveUnlockCondition));
            }

            // SelectedArmy is read before StartingArmyManager.SetUp has run the first time a hero is
            // loaded, so treat "not built yet" the same as "empty".
            if (startingArmySection.SelectedArmy == null || startingArmySection.SelectedArmy.Length == 0)
            {
                blockers.Add(LocalizationManager.Instance.GetText("OneUnitRequiredError"));
            }

            if (startingArmySection.remainingTreasury.Value < 0)
            {
                blockers.Add(LocalizationManager.Instance.GetText("InsufficientGoldError"));
            }

            // The difficulty arrows can step onto a locked level, so this is what stops the run starting.
            if (DifficultyRules.IsLocked(playPanel.SelectedDifficulty, SaveDataHandler.LoadPlayerSaveData().MaxDifficultyOverall))
            {
                blockers.Add(LocalizationManager.Instance.GetText("Difficulty Locked"));
            }

#if SPELLS
            // Gated with the spell UI itself. Without it the player has no way to see or fix a
            // missing signature spell, so this would be an unclearable blocker on the start button.
            if (loadout == null || loadout.Length == 0 || loadout[SpellLoadout.SignatureSlotIndex] == Spell.None)
            {
                blockers.Add(LocalizationManager.Instance.GetText("SignatureSpellMissingError"));
            }
#endif

            Render(feedback);
        }

        private bool? wasBlocked;
        private Coroutine blockedFade;
        private UIFlare startFlare;
        private UISheen startSheen;
        private bool juiceReady;

        // Start answers a blocked click with a shake of the blocked line; its flare and sheen are found once.
        private void EnsureJuice()
        {
            if (juiceReady) return;
            juiceReady = true;
            UIDenyFeedback.Attach(startButton, (RectTransform)blockedRoot.transform);
            startFlare = startButton.GetComponentInChildren<UIFlare>(true);
            startSheen = startButton.GetComponentInChildren<UISheen>(true);
        }

        // The campaign starts: a big gold flare and a sweep across Start Campaign.
        public void PlaySendOff()
        {
            EnsureJuice();
            if (startFlare != null) startFlare.Play(null, SendOffFlareSize);
            if (startSheen != null) startSheen.SweepNow();
        }

        private const float SendOffFlareSize = 2.2f;

        private void Render(bool feedback)
        {
            EnsureJuice();
            bool blocked = blockers.Count > 0;
            bool changed = wasBlocked.HasValue && wasBlocked.Value != blocked;
            wasBlocked = blocked;
            blockedRoot.SetActive(blocked);
            readyRoot.SetActive(!blocked);
            startButton.interactable = !blocked;
            // A fade cut short by hiding must not leave the line faint the next time it shows without one.
            if (!blocked && blockedRoot.TryGetComponent(out CanvasGroup blockedGroup))
            {
                if (blockedFade != null) { StopCoroutine(blockedFade); blockedFade = null; }
                blockedGroup.alpha = 1f;
            }
            if (feedback && changed && isActiveAndEnabled)
            {
                if (blocked) FadeInBlocked();
                else
                {
                    // The last blocker cleared: Start announces it once.
                    if (startFlare != null) startFlare.Play();
                    if (startSheen != null) startSheen.SweepNow();
                    IAudioRequester.Instance.PlaySFX(SFXData.Notification);
                }
            }

            if (blocked)
            {
                string joined = "";
                for (int i = 0; i < blockers.Count; i++)
                {
                    // Texturina has no cross glyph (it drew an empty box), so blockers are split by a middle dot.
                    joined += (i > 0 ? "  <color=" + ColorData.Error + ">·</color>  " : "") + blockers[i];
                }
                blockedText.text = joined;
                return;
            }
        }

        private void FadeInBlocked()
        {
            if (blockedFade != null) StopCoroutine(blockedFade);
            CanvasGroup group = blockedRoot.GetComponent<CanvasGroup>();
            if (group == null) group = blockedRoot.AddComponent<CanvasGroup>();
            blockedFade = StartCoroutine(UIJuice.Open(group, null, UIJuice.SwapTime, 0f));
        }
    }
}
