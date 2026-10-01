using Memori.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ
{
    /// <summary>The gain or loss tab hung off a stat row while one squad is compared with another.</summary>
    public class StatDeltaChip : MonoBehaviour
    {
        [SerializeField] private Image gem;
        [SerializeField] private TMP_Text label;
        // Tints for the gem; Colorblind Mode swaps their hue on top.
        [SerializeField] private Color gainColour = new(0.208f, 0.478f, 0.180f, 1f);
        [SerializeField] private Color lossColour = new(0.549f, 0.165f, 0.141f, 1f);

        public void Show(int delta)
        {
            bool gain = delta > 0;
            gem.color = gain ? ColorVision.Good(gainColour) : ColorVision.Bad(lossColour);
            label.text = gain ? $"+{delta}" : delta.ToString();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
