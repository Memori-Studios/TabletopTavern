using System.Collections.Generic;
using Memori.SaveData;
using UnityEngine;

namespace TJ.MainMenu
{
    /// <summary>
    /// The metal of each difficulty level, as the run-setup screens show a hero's record: bronze Easy, silver Medium,
    /// gold Hard, red Godking. The same four tints the difficulty crests use.
    /// </summary>
    public static class DifficultyMetal
    {
        static readonly Color[] Metals =
        {
            new Color32(200, 132, 87, 255),
            new Color32(206, 214, 226, 255),
            new Color32(232, 190, 98, 255),
            new Color32(214, 58, 48, 255),
        };

        public static readonly Color Unwon = new Color32(51, 66, 74, 255);
        public static readonly Color PlainFrame = new Color32(58, 78, 92, 255);

        public static int LevelCount => Metals.Length;

        public static Color ForRank(int rank) => rank >= 0 && rank < Metals.Length ? Metals[rank] : PlainFrame;

        /// <summary>One flag per ladder level, true where the hero has won a campaign at that level.</summary>
        public static bool[] WonLevels(int heroID)
        {
            var won = new bool[Metals.Length];
            List<int> completed = SaveDataHandler.GetHeroDifficultiesCompleted(heroID);
            // Saves can hold ten-tier values, so levels are compared by rank, never by value.
            foreach (int difficulty in completed)
            {
                int rank = DifficultyRules.Rank(difficulty);
                if (rank >= 0 && rank < won.Length) won[rank] = true;
            }
            return won;
        }

        public static int BestRank(bool[] won)
        {
            for (int i = won.Length - 1; i >= 0; i--)
                if (won[i]) return i;
            return -1;
        }
    }
}
