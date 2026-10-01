using UnityEngine;
using UnityEngine.EventSystems;

namespace TJ
{
    public class MinimapClickToMove : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Camera minimapCamera;

        // Armed spells read this so a click on the minimap does not also cast.
        public static bool PointerIsOver { get; private set; }

        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
        }
        private void OnDisable()
        {
            PointerIsOver = false;
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerIsOver = true;
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            PointerIsOver = false;
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            MoveCamera(eventData);
        }
        public void OnDrag(PointerEventData eventData)
        {
            MoveCamera(eventData);
        }
        private void MoveCamera(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint)) return;

            // The image shows the whole minimap camera view, so a point on it is a viewport point of that camera.
            Vector2 viewportPoint = Rect.PointToNormalized(rectTransform.rect, localPoint);
            Vector3 worldPoint = minimapCamera.ViewportToWorldPoint(new Vector3(viewportPoint.x, viewportPoint.y, minimapCamera.nearClipPlane));
            BattleManager.Instance.BattleCameraScript.LookAtGroundPosition(new Vector3(worldPoint.x, 0f, worldPoint.z));
        }
    }
}
