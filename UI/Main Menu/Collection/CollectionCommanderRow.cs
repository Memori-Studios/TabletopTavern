using System;
using TMPro;
using UnityEngine;

namespace TJ.MainMenu
{
    /// <summary>A hero in the faction summary. Clicking it opens the Heroes tab on that hero.</summary>
    public class CollectionCommanderRow : MonoBehaviour
    {
        [SerializeField] private CollectionMiniUnit portrait;
        [SerializeField] private TMP_Text heroName;
        [SerializeField] private TMP_Text line;

        public CollectionMiniUnit Portrait => portrait;

        public void Set(string name, string detail, Color frame, Action onClick)
        {
            heroName.text = name;
            line.text = detail;
            portrait.Set(null, frame, 1, onClick);
        }
    }
}
