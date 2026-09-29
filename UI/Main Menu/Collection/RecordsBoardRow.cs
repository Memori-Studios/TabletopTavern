using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>One leaderboard row: place, name, an optional second column and the score. The player's own row is tinted.</summary>
    public class RecordsBoardRow : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private Image medal;
        [SerializeField] private TMP_Text place;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private TMP_Text second;
        [SerializeField] private TMP_Text score;

        [SerializeField] private Color stripeFill = new(0.11f, 0.15f, 0.16f, 0.55f);
        [SerializeField] private Color meFill = new(0.91f, 0.75f, 0.42f, 0.12f);
        [SerializeField] private Color meBorder = new(0.91f, 0.75f, 0.42f, 0.55f);
        [SerializeField] private Color nameColour = new(0.93f, 0.9f, 0.85f, 1f);
        [SerializeField] private Color scoreColour = new(0.85f, 0.82f, 0.76f, 1f);
        [SerializeField] private Color meColour = new(0.91f, 0.75f, 0.42f, 1f);
        [SerializeField] private Color placeColour = new(0.56f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color[] medalColours =
        {
            new(0.91f, 0.75f, 0.42f, 1f),
            new(0.79f, 0.82f, 0.84f, 1f),
            new(0.82f, 0.54f, 0.34f, 1f),
        };

        /// <param name="rank">1-based place shown in the first column; 1 to 3 get a medal.</param>
        /// <param name="secondText">The optional middle column, such as a friend's world rank. Hidden when null.</param>
        public void Set(int rank, string nameText, string scoreText, bool isMe, bool striped, string secondText = null)
        {
            bool hasMedal = rank >= 1 && rank <= medalColours.Length;
            medal.enabled = hasMedal;
            place.text = rank > 0 ? rank.ToString() : string.Empty;
            place.color = hasMedal ? medalColours[rank - 1] : placeColour;
            if (hasMedal) medal.color = new Color(medalColours[rank - 1].r, medalColours[rank - 1].g, medalColours[rank - 1].b, 0.18f);

            playerName.text = nameText;
            playerName.color = isMe ? meColour : nameColour;
            score.text = scoreText;
            score.color = isMe ? meColour : scoreColour;
            second.gameObject.SetActive(secondText != null);
            if (secondText != null) second.text = secondText;

            background.color = isMe ? meFill : striped ? stripeFill : Color.clear;
            border.enabled = isMe;
            border.color = meBorder;
        }
    }
}
