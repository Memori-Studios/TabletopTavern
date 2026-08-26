using System.Collections.Generic;
using Memori.Localization;
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

        public void Evaluate(PlayPanel playPanel, StartingArmyManager startingArmySection, Spell[] loadout)
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

            // Load-bearing, not defensive: the difficulty spinner can be stepped onto a locked
            // level, so this is what actually stops the run starting.
            if (IsDifficultyLocked(playPanel.SelectedDifficulty))
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

            Render();
        }

        private static bool IsDifficultyLocked(TT_Difficulty difficulty)
        {
            if (difficulty == TT_Difficulty.Peasant) return false;
            return (int)difficulty > SaveDataHandler.LoadPlayerSaveData().MaxDifficultyOverall + 1;
        }

        private void Render()
        {
            bool blocked = blockers.Count > 0;
            blockedRoot.SetActive(blocked);
            readyRoot.SetActive(!blocked);
            startButton.interactable = !blocked;

            if (blocked)
            {
                string joined = "";
                for (int i = 0; i < blockers.Count; i++)
                {
                    joined += (i > 0 ? "   " : "") + "<color=" + ColorData.Error + ">✕</color> " + blockers[i];
                }
                blockedText.text = joined;
                return;
            }
        }
    }
}
