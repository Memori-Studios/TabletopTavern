using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>A small portrait with a tier edge: a starting-army unit, a signature unit or a commander.</summary>
    public class CollectionMiniUnit : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Image tierBar;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button button;

        private Action _onClick;

        private void Awake()
        {
            button.onClick.AddListener(() => _onClick?.Invoke());
        }

        public Image Portrait => portrait;

        public void Set(Sprite sprite, Color tier, int count = 1, Action onClick = null)
        {
            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
            tierBar.color = tier;
            countText.text = count > 1 ? $"×{count}" : string.Empty;
            _onClick = onClick;
            button.interactable = onClick != null;
        }
    }
}
