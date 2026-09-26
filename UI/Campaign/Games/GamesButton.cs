using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.Games
{
    [RequireComponent(typeof(Button), typeof(MemoriCanvasGroup))]
    public class GamesButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Button buttonComponent;
        private MemoriCanvasGroup canvasGroup;

        private void Awake()
        {
            buttonComponent = GetComponent<Button>();
            canvasGroup = GetComponent<MemoriCanvasGroup>();
        }

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image[] backgroundImages;
        [SerializeField] private Animator animator;

        private static readonly Color PrimaryColor   = (Color)ColorData.HexToRgba(ColorData.Primary);
        private static readonly Color SecondaryColor = (Color)ColorData.HexToRgba(ColorData.Secondary);
        private static readonly float AlphaDefault = 25f / 255f;
        private static readonly float AlphaHover   = 50f / 255f;

        bool _canAfford;

        public void LoadGamesButton(string title, string description, UnityEngine.Events.UnityAction onClickAction)
        {
            buttonComponent.onClick.RemoveAllListeners();
            buttonComponent.onClick.AddListener(onClickAction);
            titleText.text = title;
            descriptionText.text = description;
            SetColor(SecondaryColor);
            SetImageAlpha(AlphaDefault);
        }
        public void SetButtonAffordability(bool canAfford)
        {
            _canAfford = canAfford;
            // Grey text marks unaffordable; the animator's Disabled state hides the button (Content alpha 0).
            SetColor(SecondaryColor);
            SetImageAlpha(AlphaDefault);
        }
        public void ActivateButton()
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.FadeInAsync(0.2f);
        }
        public void DeactivateButton()
        {
            canvasGroup.CGDisable();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetColor(PrimaryColor);
            SetImageAlpha(AlphaHover);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetColor(SecondaryColor);
            SetImageAlpha(AlphaDefault);
        }

        private void SetColor(Color color)
        {
            titleText.color = _canAfford ? color : Color.gray;
            descriptionText.color = _canAfford ? color : Color.gray;
        }

        private void SetImageAlpha(float alpha)
        {
            foreach (Image image in backgroundImages)
            {
                Color c = image.color;
                c.a = alpha;
                image.color = c;
            }
        }
    }
}
