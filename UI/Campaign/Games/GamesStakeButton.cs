using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Games
{
    /// <summary>One dice-table stake on the games card: the stake's arrows and its gold amount.</summary>
    public class GamesStakeButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text amount;
        [SerializeField] private Color affordableColour = new(0.925f, 0.902f, 0.847f, 1f);

        public Button Button => button;

        public void SetAmount(string text) => amount.text = text;

        // An unaffordable stake cannot be clicked; the button family fades it and the amount turns red.
        public void SetAffordable(bool affordable)
        {
            button.interactable = affordable;
            amount.color = affordable ? affordableColour : (Color)ColorData.HexToRgba(ColorData.Negative);
        }
    }
}
