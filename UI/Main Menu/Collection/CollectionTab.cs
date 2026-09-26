using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>A text tab (Units, Heroes, Lore) or a hero switcher tab. Active state is colour plus an underline or frame.</summary>
    public class CollectionTab : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Graphic activeMark;
        [SerializeField] private Image frame;
        [SerializeField] private Image portrait;

        [SerializeField] private Color idleText = new(0.56f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color activeText = new(0.91f, 0.75f, 0.42f, 1f);
        [SerializeField] private Color idleFrame = new(0.2f, 0.26f, 0.25f, 1f);
        [SerializeField] private Color activeFrame = new(0.69f, 0.54f, 0.24f, 1f);

        public Button Button => button;
        public Image Portrait => portrait;

        public void SetLabel(string text) => label.text = text;

        public void SetActive(bool active)
        {
            label.color = active ? activeText : idleText;
            if (activeMark != null) activeMark.enabled = active;
            if (frame != null) frame.color = active ? activeFrame : idleFrame;
        }
    }
}
