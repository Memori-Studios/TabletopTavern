using System;
using Memori.SaveData;

namespace TJ
{
    /// <summary>
    /// The score posted to the Deepest March leaderboard: how far a run marched after banking its
    /// act 3 win. Acts rank first, chapters finished in the current act second, and the difficulty
    /// breaks ties, packed so a bigger number is always a deeper march.
    /// </summary>
    public static class DeepestMarchScore
    {
        private const int ACT_STEP = 10000;
        private const int CHAPTER_STEP = 100;
        // Two digits each for chapters and difficulty keep the fields from bleeding into each other.
        private const int FIELD_MAX = 99;

        public static int Encode(int act, int chaptersInAct, TT_Difficulty difficulty)
        {
            int chapters = Math.Clamp(chaptersInAct, 0, FIELD_MAX);
            int level = Math.Clamp(TierNumber(difficulty), 0, FIELD_MAX);
            return Math.Max(0, act) * ACT_STEP + chapters * CHAPTER_STEP + level;
        }

        // Four-level values post as their ten-tier equivalent, so a higher level always breaks ties and
        // entries posted before the change keep ranking and decoding the same way.
        private static int TierNumber(TT_Difficulty difficulty) => difficulty switch
        {
            TT_Difficulty.Easy => (int)TT_Difficulty.Peasant,
            TT_Difficulty.Medium => (int)TT_Difficulty.Duke,
            TT_Difficulty.Hard => (int)TT_Difficulty.Overlord,
            _ => (int)difficulty,
        };

        /// <summary>activeMapLayer is the last finished layer, so the chapters finished in the act are one more.</summary>
        public static int FromRun(CampaignSaveData run)
        {
            return Encode(run.bookNumber, run.activeMapLayer + 1, run.difficultyLevel);
        }

        public static void Decode(int score, out int act, out int chaptersInAct, out TT_Difficulty difficulty)
        {
            act = score / ACT_STEP;
            chaptersInAct = score % ACT_STEP / CHAPTER_STEP;
            difficulty = (TT_Difficulty)(score % CHAPTER_STEP);
        }
    }
}
