using System;
using Memori.Localization;
using Memori.SaveData;
using Memori.UI;

namespace TJ.MainMenu
{
    /// <summary>Text the Records pages share: run times, march depths and run outcomes.</summary>
    public static class RecordsFormat
    {
        /// <summary>Seconds to h:mm:ss, the way a speedrun clock reads.</summary>
        public static string Time(double seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
                : $"{span.Minutes}:{span.Seconds:00}";
        }

        /// <summary>A Deepest March score as "Act IV - 7 · Godking": act, chapters finished in it, difficulty.</summary>
        public static string Depth(int score)
        {
            DeepestMarchScore.Decode(score, out int act, out int chapters, out TT_Difficulty difficulty);
            LocalizationManager loc = LocalizationManager.Instance;
            string difficultyName = loc.GetText(DifficultyData.GetDifficultyLevelData(difficulty).difficultyName);
            return string.Format(loc.GetText("LeaderboardDepthFormat"), MemoriUI.ConvertNumberToRomanNumeral(act), chapters, difficultyName);
        }

        public static string Outcome(RunOutcome outcome)
        {
            LocalizationManager loc = LocalizationManager.Instance;
            return outcome switch
            {
                RunOutcome.Win => loc.GetText("Victory"),
                RunOutcome.Loss => loc.GetText("Defeated"),
                _ => loc.GetText("RunOutcomeAbandoned"),
            };
        }

        /// <summary>Hex colour for an outcome. Win and loss follow Colorblind Mode.</summary>
        public static string OutcomeColour(RunOutcome outcome) => outcome switch
        {
            RunOutcome.Win => ColorData.Positive,
            RunOutcome.Loss => ColorData.Negative,
            _ => ColorData.Secondary,
        };

        public static string Difficulty(TT_Difficulty difficulty) =>
            LocalizationManager.Instance.GetText(DifficultyData.GetDifficultyLevelData(difficulty).difficultyName);
    }
}
