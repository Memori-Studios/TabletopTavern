using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>A two-segment switch, such as Everyone and Friends on the leaderboard.</summary>
    public class RecordsFilterToggle : MonoBehaviour
    {
        [SerializeField] private Button first;
        [SerializeField] private Button second;
        [SerializeField] private Image firstFill;
        [SerializeField] private Image secondFill;
        [SerializeField] private TMP_Text firstLabel;
        [SerializeField] private TMP_Text secondLabel;

        [SerializeField] private Color activeFill = new(0.16f, 0.23f, 0.24f, 1f);
        [SerializeField] private Color activeText = new(0.91f, 0.75f, 0.42f, 1f);
        [SerializeField] private Color idleText = new(0.56f, 0.6f, 0.6f, 1f);

        /// <summary>Raised with true when the second segment is picked.</summary>
        public event Action<bool> Changed;

        public bool SecondActive { get; private set; }

        private void Awake()
        {
            first.onClick.AddListener(() => Pick(false));
            second.onClick.AddListener(() => Pick(true));
        }

        public void SetLabels(string firstText, string secondText)
        {
            firstLabel.text = firstText;
            secondLabel.text = secondText;
        }

        /// <summary>Shows a segment as picked without raising Changed.</summary>
        public void Show(bool secondActive)
        {
            SecondActive = secondActive;
            firstFill.color = secondActive ? Color.clear : activeFill;
            secondFill.color = secondActive ? activeFill : Color.clear;
            firstLabel.color = secondActive ? idleText : activeText;
            secondLabel.color = secondActive ? activeText : idleText;
        }

        private void Pick(bool secondActive)
        {
            if (secondActive == SecondActive) return;
            Show(secondActive);
            Changed?.Invoke(secondActive);
        }
    }
}
