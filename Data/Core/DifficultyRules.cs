using System;

namespace TJ
{
    // Every difficulty gate fires from the level whose DifficultyData list holds its modifier number,
    // so the card the player reads and the rule the game applies cannot drift apart.
    // Saves still hold ten-tier values and Easy..Hard sit above Godking in the enum, so compare
    // ranks, never the enum values themselves.
    public static class DifficultyRules
    {
        #region Ladder
        // The levels the player picks from, easiest first. A rank is an index into this array.
        public static readonly TT_Difficulty[] Ladder =
        {
            TT_Difficulty.Easy, TT_Difficulty.Medium, TT_Difficulty.Hard, TT_Difficulty.Godking,
        };

        public static TT_Difficulty Easiest => Ladder[0];
        public static TT_Difficulty Hardest => Ladder[Ladder.Length - 1];

        // Position on Ladder, or -1 for a value that is no level (saves store 0 for "nothing completed").
        // A ten-tier value counts as the level it maps to.
        public static int Rank(TT_Difficulty difficulty)
        {
            switch (difficulty)
            {
                case TT_Difficulty.Easy:
                case TT_Difficulty.Peasant:
                    return 0;
                case TT_Difficulty.Medium:
                case TT_Difficulty.Squire:
                case TT_Difficulty.Knight:
                case TT_Difficulty.Baron:
                case TT_Difficulty.Duke:
                    return 1;
                case TT_Difficulty.Hard:
                case TT_Difficulty.King:
                case TT_Difficulty.Emperor:
                case TT_Difficulty.Imperator:
                case TT_Difficulty.Overlord:
                    return 2;
                case TT_Difficulty.Godking:
                    return 3;
                default:
                    return -1;
            }
        }

        public static int Rank(int savedDifficulty) => Rank((TT_Difficulty)savedDifficulty);

        // The level a saved value stands for. Anything unknown reads as the easiest.
        public static TT_Difficulty Normalize(TT_Difficulty difficulty)
        {
            int rank = Rank(difficulty);
            return rank < 0 ? Easiest : Ladder[rank];
        }

        // 1-based position, shown as the Roman numeral on the hero roster badge.
        public static int LevelNumber(TT_Difficulty difficulty) => Math.Max(Rank(difficulty), 0) + 1;

        public static bool IsHardest(TT_Difficulty difficulty) => Rank(difficulty) == Ladder.Length - 1;

        // The level above, or the same level at the top.
        public static TT_Difficulty Next(TT_Difficulty difficulty) =>
            Ladder[Math.Min(Math.Max(Rank(difficulty), 0) + 1, Ladder.Length - 1)];

        // The level below, or the same level at the bottom.
        public static TT_Difficulty Previous(TT_Difficulty difficulty) =>
            Ladder[Math.Max(Rank(difficulty) - 1, 0)];

        // The harder of two saved values (as ints, because that is how completions are stored).
        public static int Harder(int savedA, int savedB) => Rank(savedB) > Rank(savedA) ? savedB : savedA;

        // A level opens once the level below it has been completed; the easiest is always open.
        // maxCompleted is PlayerSaveData.MaxDifficultyOverall, 0 when nothing is completed.
        public static bool IsLocked(TT_Difficulty difficulty, int maxCompleted) =>
            Rank(difficulty) > Rank(maxCompleted) + 1;

        // The hardest level the player may start.
        public static TT_Difficulty HighestUnlocked(int maxCompleted) =>
            Ladder[Math.Min(Rank(maxCompleted) + 1, Ladder.Length - 1)];
        #endregion

        #region Modifiers
        // Rank of the level that lists this DifficultyMod, or int.MaxValue when no level does.
        public static int FirstRank(int modifier)
        {
            for (int i = 0; i < Ladder.Length; i++)
            {
                if (Array.IndexOf(DifficultyData.GetDifficultyLevelData(Ladder[i]).modifiers, modifier) >= 0)
                    return i;
            }
            return int.MaxValue;
        }

        public static bool Applies(int modifier, TT_Difficulty difficulty) => Rank(difficulty) >= FirstRank(modifier);

        // DifficultyMod 3 "Auto-resolve health loss preview is hidden".
        public static bool AutoResolvePreviewHidden(TT_Difficulty difficulty) => Applies(3, difficulty);

        // DifficultyMod 7 "Stronger enemy armies" and DifficultyMod 19 "Enemy armies scale in
        // strength faster". Each adds one battlesFought step, which moves the army into the next
        // tier band earlier.
        public static int BattlesFoughtBonus(TT_Difficulty difficulty)
        {
            int bonus = 0;
            if (Applies(7, difficulty)) bonus += 1;
            if (Applies(19, difficulty)) bonus += 1;
            return bonus;
        }

        // DifficultyMod 6 "The Final Battle of each Act is more difficult". Selects the
        // KnightDifficulty tier table for horde battles.
        public static bool HarderFinalBattle(TT_Difficulty difficulty) => Applies(6, difficulty);

        // DifficultyMod 8 "Removes the ability to modify rolls in events".
        public static bool EventRollsLocked(TT_Difficulty difficulty) => Applies(8, difficulty);

        // DifficultyMod 9 "Increases the cost to recruit from towns".
        public static bool RecruitCostIncreased(TT_Difficulty difficulty) => Applies(9, difficulty);

        // DifficultyMod 10 "Enemy armies may contain prestiged units".
        public static bool EnemyPrestigeEligible(TT_Difficulty difficulty) => Applies(10, difficulty);

        // DifficultyMod 11 "Reduced heal on entering Settlements".
        public static bool ReducedTownHeal(TT_Difficulty difficulty) => Applies(11, difficulty);

        // DifficultyMod 14 "Increases the chance and severity of enemy unit prestige".
        public static bool EnemyPrestigeEnhanced(TT_Difficulty difficulty) => Applies(14, difficulty);

        // DifficultyMod 16 "Settlements have stronger garrisons".
        public static bool StrongerGarrisons(TT_Difficulty difficulty) => Applies(16, difficulty);

        // DifficultyMod 18 "Start each run with a weakened army". Fraction of max health every
        // starting squad is created with.
        public static float StartingHealth(TT_Difficulty difficulty) => Applies(18, difficulty) ? 0.75f : 1f;

        // DifficultyMod 20 "Auto-resolve disabled". Not a battle input, but the sim marks these rows
        // because the player cannot use auto-resolve there.
        public static bool AutoResolveDisabled(TT_Difficulty difficulty) => Applies(20, difficulty);
        #endregion

        #region Rewards and endless
        // Renown multiplier added per level climbed: Easy 1.00, Medium 1.75, Hard 2.50, Godking 3.25.
        public const float RENOWN_MULTIPLIER_PER_LEVEL = 0.75f;

        public static float RenownMultiplier(TT_Difficulty difficulty) =>
            1f + Math.Max(Rank(difficulty), 0) * RENOWN_MULTIPLIER_PER_LEVEL;

        // March On (the endless campaign after act 3) and the Deepest March board are for the two
        // hardest levels only; below them the act 3 win ends the run as it always did.
        public static bool EndlessOffered(TT_Difficulty difficulty) => Rank(difficulty) >= Ladder.Length - 2;

        // Endless ships with the Spell Update. Before it, the Act Complete screen still offers the
        // choice, but March On sits under the Spell Update blocker and nothing reaches Deepest March.
        public static bool EndlessUnlocked
        {
            get
            {
#if SPELLS
                return true;
#else
                return false;
#endif
            }
        }

        public static bool EndlessAllowed(TT_Difficulty difficulty) => EndlessUnlocked && EndlessOffered(difficulty);
        #endregion
    }
}
