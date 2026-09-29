using UnityEngine;

namespace TJ.Games
{
    public static class TavernGameRules
    {
        #region Dice Table
        // Wagers won (+) or lost (-) for one roll; a loss never exceeds the wager, so gold cannot be clamped mid-bet.
        public static int DiceTablePayout(int playerFace, int houseFace)
        {
            if (playerFace == 6 && (houseFace == 1 || houseFace == 6)) return 2;
            if (playerFace > houseFace) return 1;
            if (playerFace < houseFace) return -1;
            return 0;
        }

        public static int DiceTableWinChancePercent()
        {
            int wins = 0;
            for (int player = 1; player <= 6; player++)
                for (int house = 1; house <= 6; house++)
                    if (DiceTablePayout(player, house) > 0) wins++;
            return Mathf.RoundToInt(wins * 100f / 36f);
        }
        #endregion

        #region Higher or Lower
        public const int HigherLowerStakeBase = 10;
        public const int HigherLowerMaxCalls = 4;
        // The stake is a multiple of 10, so every pot on this ladder is whole gold.
        private static readonly float[] PotMultipliers = { 1f, 1.5f, 2f, 3f, 5f };

        public static int HigherLowerPot(int stake, int wins) => Mathf.RoundToInt(stake * PotMultipliers[Mathf.Clamp(wins, 0, HigherLowerMaxCalls)]);

        // A tie goes to the house.
        public static bool CallWins(bool callHigher, int houseFace, int playerFace) => callHigher ? playerFace > houseFace : playerFace < houseFace;

        public static int WinningFaces(bool callHigher, int houseFace) => callHigher ? 6 - houseFace : houseFace - 1;

        public static int WinChancePercent(bool callHigher, int houseFace) => Mathf.RoundToInt(WinningFaces(callHigher, houseFace) * 100f / 6f);

        public static int BestFace(bool callHigher) => callHigher ? 6 : 1;
        #endregion
    }
}
