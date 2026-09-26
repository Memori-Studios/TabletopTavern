using UnityEngine;
using UnityEngine.EventSystems;

namespace TJ.MainMenu
{
    /// <summary>Turns the preview model by dragging the stage image or holding one of the two turn buttons.</summary>
    public class CollectionStageInput : MonoBehaviour, IDragHandler
    {
        [SerializeField] private CollectionHoldButton turnLeft;
        [SerializeField] private CollectionHoldButton turnRight;
        [SerializeField] private float degreesPerPixel = 0.45f;
        [SerializeField] private float turnSpeed = 90f;

        public CollectionPreviewRig Rig { get; set; }

        public void OnDrag(PointerEventData eventData)
        {
            if (Rig == null) return;
            Rig.Turn(-eventData.delta.x * degreesPerPixel);
        }

        private void Update()
        {
            if (Rig == null) return;
            // Unscaled: the Collection can open over a battle that Settings has paused.
            float step = turnSpeed * Time.unscaledDeltaTime;
            if (turnLeft.Held) Rig.Turn(step);
            if (turnRight.Held) Rig.Turn(-step);
        }
    }
}
