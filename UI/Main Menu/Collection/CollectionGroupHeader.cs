using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>The heading above one rarity group in the gear and potion grids.</summary>
    public class CollectionGroupHeader : MonoBehaviour
    {
        [SerializeField] private Image diamond;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text count;

        public void Set(Color colour, string text, string found)
        {
            diamond.color = colour;
            label.text = text;
            count.text = found;
        }
    }
}
