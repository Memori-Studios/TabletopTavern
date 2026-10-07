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
        [SerializeField] private Image[] chipFaces;
        [SerializeField] private Color winFace = new(0.812f, 0.961f, 0.784f, 1f);
        [SerializeField] private Color loseFace = new(0.369f, 0.42f, 0.439f, 1f);
        [SerializeField] private Color goodChance = new(0.812f, 0.961f, 0.784f, 1f);
        [SerializeField] private Color poorChance = new(0.663f, 0.725f, 0.776f, 1f);

        public Button Button => button;

        // Faces are 1-6; winningFaces holds the ones this call wins on.
        public void Set(string chanceText, int chancePercent, bool[] winningFaces)
        {
            chance.text = chanceText;
            chance.color = chancePercent >= 50 ? goodChance : poorChance;
            for (int i = 0; i < chipFaces.Length; i++)
            {
                bool wins = i < winningFaces.Length && winningFaces[i];
                chipFaces[i].color = wins ? winFace : loseFace;
            }
        }
    }
}
