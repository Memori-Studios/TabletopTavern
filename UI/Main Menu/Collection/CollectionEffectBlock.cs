using TMPro;
using UnityEngine;

namespace TJ.MainMenu
{
    /// <summary>A named effect: a hero bonus or a faction effect, with an optional phase tag.</summary>
    public class CollectionEffectBlock : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;

        public void Set(string name, string tag, string text)
        {
            title.text = string.IsNullOrEmpty(tag) ? name : $"{name}  <size=70%><color=#8E8672><uppercase>{tag}</uppercase></color></size>";
            title.gameObject.SetActive(!string.IsNullOrEmpty(name));
            KeywordText.Apply(body, text);
        }

        /// <summary>Splits "Name: effect" at the first colon, which is how hero and faction effects are written.</summary>
        public static void Split(string line, out string name, out string text)
        {
            int colon = line.IndexOf(':');
            if (colon <= 0)
            {
                name = string.Empty;
                text = line.Trim();
                return;
            }
            name = line.Substring(0, colon).Trim();
            text = line.Substring(colon + 1).Trim();
        }
    }
}
