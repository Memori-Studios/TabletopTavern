using System;
using Memori.Localization;
using Memori.Steamworks;
using Memori.UI;
using TMPro;
using UnityEngine;

namespace TJ.MainMenu
{
    /// <summary>One row on the leaderboard: rank, player name, score (a time or a march depth). The player's own row is tinted.</summary>
    public class LeaderboardRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private UnityEngine.UI.Image highlightImage;

        private const float ME_ALPHA = 0.3f;

        public void Load(LeaderboardRowData row, Func<int, string> formatScore)
        {
            rankText.text = row.Rank.ToString();
            nameText.text = row.IsMe ? $"<color={ColorData.Gold}>{row.PlayerName}</color>" : row.PlayerName;
            timeText.text = formatScore(row.Score);

            if (highlightImage != null)
            {
                highlightImage.enabled = row.IsMe;
                Color c = highlightImage.color;
                c.a = ME_ALPHA;
                highlightImage.color = c;
            }
        }

        /// <summary>Seconds to h:mm:ss, the way a speedrun clock reads.</summary>
        public static string FormatTime(double seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
                : $"{span.Minutes}:{span.Seconds:00}";
        }
        public static string FormatTime(int seconds) => FormatTime((double)seconds);

        /// <summary>A Deepest March score as "Act IV - 7 · Godking": act, chapters finished in it, difficulty.</summary>
        public static string FormatDepth(int score)
        {
            DeepestMarchScore.Decode(score, out int act, out int chapters, out TT_Difficulty difficulty);
            LocalizationManager loc = LocalizationManager.Instance;
            string difficultyName = loc.GetText(DifficultyData.GetDifficultyLevelData(difficulty).difficultyName);
            return string.Format(loc.GetText("LeaderboardDepthFormat"), MemoriUI.ConvertNumberToRomanNumeral(act), chapters, difficultyName);
        }
    }
}
