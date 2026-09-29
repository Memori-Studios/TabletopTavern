using System.Collections;
using UnityEngine;

namespace TJ.Spells
{
    // Addon art that rises out of the ground when its object switches on, turns slowly, and sinks when ActiveSpell starts the end warning.
    public class SpellAddonRise : MonoBehaviour
    {
        [SerializeField] private float depth = 1.4f;
        [SerializeField] private float riseTime = 0.35f;
        // Degrees per second; positive turns clockwise seen from above.
        [SerializeField] private float spinSpeed = 6f;
        // Sink with the area circle's final shrink instead of during the end warning.
        [SerializeField] private bool sinkWithArea;

        public bool SinksWithArea => sinkWithArea;

        private Vector3 restPosition;
        private Coroutine motion;

        private void Awake() => restPosition = transform.localPosition;

        private void OnEnable() => MoveBetween(-depth, 0f, riseTime);

        private void Update() => transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

        public void Sink(float duration)
        {
            if (isActiveAndEnabled) MoveBetween(0f, -depth, duration);
        }

        private void MoveBetween(float fromOffset, float toOffset, float duration)
        {
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(Move(fromOffset, toOffset, duration));
        }

        private IEnumerator Move(float fromOffset, float toOffset, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float eased = Mathf.SmoothStep(0f, 1f, t / duration);
                transform.localPosition = restPosition + Vector3.up * Mathf.Lerp(fromOffset, toOffset, eased);
                yield return null;
            }
            transform.localPosition = restPosition + Vector3.up * toOffset;
            motion = null;
        }
    }
}
