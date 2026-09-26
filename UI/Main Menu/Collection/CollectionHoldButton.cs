using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>Reports whether the pointer is holding it down, for continuous turning.</summary>
    public class CollectionHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image fill;
        [SerializeField] private Color idle = new(0.1f, 0.14f, 0.15f, 1f);
        [SerializeField] private Color hover = new(0.18f, 0.24f, 0.25f, 1f);

        public bool Held { get; private set; }

        private void OnDisable()
        {
            Held = false;
            fill.color = idle;
        }

        public void OnPointerDown(PointerEventData eventData) => Held = true;
        public void OnPointerUp(PointerEventData eventData) => Held = false;
        public void OnPointerEnter(PointerEventData eventData) => fill.color = hover;

        public void OnPointerExit(PointerEventData eventData)
        {
            Held = false;
            fill.color = idle;
        }
    }
}
