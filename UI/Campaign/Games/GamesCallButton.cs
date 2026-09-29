using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Games
{
    /// <summary>A Higher or Lower call: its chance to win and the six die faces, with the winning ones lit.</summary>
    public class GamesCallButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text chance;
        [SerializeField] private Image[] chipFills;
        [SerializeField] private Image[] chipEdges;
        [SerializeField] private TMP_Text[] chipLabels;
        [SerializeField] private Color winFill = new(0.482f, 0.839f, 0.435f, 0.2f);
        [SerializeField] private Color winEdge = new(0.482f, 0.839f, 0.435f, 0.85f);
        [SerializeField] private Color winLabel = new(0.812f, 0.961f, 0.784f, 1f);
        [SerializeField] private Color loseFill = new(0.04f, 0.063f, 0.071f, 0.35f);
        [SerializeField] private Color loseEdge = new(0.227f, 0.29f, 0.314f, 1f);
        [SerializeField] private Color loseLabel = new(0.369f, 0.42f, 0.439f, 1f);
        [SerializeField] private Color goodChance = new(0.812f, 0.961f, 0.784f, 1f);
        [SerializeField] private Color poorChance = new(0.663f, 0.725f, 0.776f, 1f);

        public Button Button => button;

        // Faces are 1-6; winningFaces holds the ones this call wins on.
        public void Set(string chanceText, int chancePercent, bool[] winningFaces)
        {
            chance.text = chanceText;
            chance.color = chancePercent >= 50 ? goodChance : poorChance;
            for (int i = 0; i < chipFills.Length; i++)
            {
                bool wins = i < winningFaces.Length && winningFaces[i];
                chipFills[i].color = wins ? winFill : loseFill;
                chipEdges[i].color = wins ? winEdge : loseEdge;
                chipLabels[i].color = wins ? winLabel : loseLabel;
            }
        }
    }
}
