using System;
using System.Globalization;
using TabletopTavern.Leaderboards;
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

        /// <summary>A March board score as "17 battles · 2,345 slain": battles won on the March, then enemies slain.</summary>
        public static string Depth(int score)
        {
            MarchScore.Decode(score, out int battles, out int kills);
            return string.Format(LocalizationManager.Instance.GetText("LeaderboardBattlesFormat"), battles, kills.ToString("N0", CultureInfo.CurrentCulture));
        }

        /// <summary>How far a recorded run got: its March battles when it marched on, else the act it reached.</summary>
        public static string Reached(RunRecord record)
        {
            LocalizationManager loc = LocalizationManager.Instance;
            return record.marchBattles > 0
                ? string.Format(loc.GetText("RunHistoryMarchBattles"), record.marchBattles)
                : string.Format(loc.GetText("RunHistoryActReached"), record.actReached);
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
