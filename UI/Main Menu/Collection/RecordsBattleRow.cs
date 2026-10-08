using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>One battle in a Run History card's log: its number, what was fought, a detail line and the outcome.</summary>
    public class RecordsBattleRow : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text number;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text meta;
        [SerializeField] private TMP_Text outcome;

        [SerializeField] private Color stripeFill = new(0.11f, 0.15f, 0.16f, 0.55f);

        public void Set(int index, string titleText, string metaText, string outcomeText, Color outcomeColour, bool striped)
        {
            number.text = index.ToString();
            title.text = titleText;
            meta.text = metaText;
            outcome.text = outcomeText;
            outcome.color = outcomeColour;
            background.color = striped ? stripeFill : Color.clear;
        }
    }
}
