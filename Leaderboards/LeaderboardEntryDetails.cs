using System;
using System.Collections.Generic;

namespace TabletopTavern.Leaderboards
{
    /// <summary>
    /// The numbers the analytics server stores with each leaderboard entry, in the order its README documents:
    /// layout, hero, difficulty, build, play time, layers completed, enemies slain, then one per squad
    /// (UnitName * 10 + prestige). An entry with any other layout number reads as not Valid.
    /// </summary>
    public readonly struct LeaderboardEntryDetails
    {
        public const int LAYOUT = 1;
        private const int FIRST_SQUAD = 7;

        public readonly bool Valid;
        public readonly int HeroId;
        public readonly int Difficulty;
        public readonly string Build;
        public readonly int PlayTimeSeconds;
        public readonly int LayersCompleted;
        public readonly int EnemiesSlain;
        public readonly IReadOnlyList<(UnitName Unit, int Prestige)> Squads;

        private LeaderboardEntryDetails(int[] d, List<(UnitName, int)> squads)
        {
            Valid = true;
            HeroId = d[1];
            Difficulty = d[2];
            Build = d[3] > 0 ? $"{d[3] / 10000}.{d[3] / 100 % 100}.{d[3] % 100}" : null;
            PlayTimeSeconds = d[4];
            LayersCompleted = d[5];
            EnemiesSlain = d[6];
            Squads = squads;
        }

        public static LeaderboardEntryDetails Decode(int[] details)
        {
            if (details == null || details.Length < FIRST_SQUAD || details[0] != LAYOUT) return default;
            var squads = new List<(UnitName, int)>();
            for (int i = FIRST_SQUAD; i < details.Length; i++)
            {
                int unit = details[i] / 10;
                // An unknown unit is one this build does not have yet; it is left out rather than shown wrong.
                if (details[i] >= 0 && Enum.IsDefined(typeof(UnitName), unit)) squads.Add(((UnitName)unit, details[i] % 10));
            }
            return new LeaderboardEntryDetails(details, squads);
        }
    }

    /// <summary>The March board's score: battles won on the March, then enemies slain in the run as the tie-break.</summary>
    public static class MarchScore
    {
        public const int BATTLE_STEP = 100000;

        public static void Decode(int score, out int battles, out int kills)
        {
            battles = Math.Max(0, score) / BATTLE_STEP;
            kills = Math.Max(0, score) % BATTLE_STEP;
        }
    }
}
