using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace TJ.Spells
{
    // One piece of a Barricades line. Its NavMeshObstacle carves the NavMesh, which is what makes every unit path around it.
    public class BarricadePiece : MonoBehaviour
    {
        // Carves from the moment the piece is raised until it starts to fall; unscaled, so its size is the footprint.
        [SerializeField] private NavMeshObstacle obstacle;
        // Rises and sinks; the jitter is applied here so the carved footprint stays on the drawn line.
        [SerializeField] private Transform modelRoot;
        // One is shown per piece, picked at random, so a line never reads as copies.
        [SerializeField] private GameObject[] models;
        [SerializeField] private ParticleSystem landDust;
        [SerializeField] private float sinkDepth = 2.5f;
        [SerializeField] private float riseTime = 0.4f;
        [SerializeField] private float maxYawJitter = 8f;
        [SerializeField] private float maxOffsetJitter = 0.3f;

        public float Length => obstacle.size.x;
        public float Depth => obstacle.size.z;

        private Vector3 restPosition;
        private Coroutine motion;

        public void Raise()
        {
            if (models != null && models.Length > 0)
            {
                int shown = Random.Range(0, models.Length);
                for (int i = 0; i < models.Length; i++)
                    if (models[i] != null) models[i].SetActive(i == shown);
            }
            restPosition = new Vector3(0f, 0f, Random.Range(-maxOffsetJitter, maxOffsetJitter));
            modelRoot.localRotation = Quaternion.Euler(0f, Random.Range(-maxYawJitter, maxYawJitter), 0f);
            obstacle.enabled = true;
            if (landDust != null) landDust.Play(true);
            MoveBetween(-sinkDepth, 0f, riseTime);
        }

        public void Fall(float duration)
        {
            obstacle.enabled = false;
            if (isActiveAndEnabled) MoveBetween(0f, -sinkDepth, duration);
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
                modelRoot.localPosition = restPosition + Vector3.up * Mathf.Lerp(fromOffset, toOffset, eased);
                yield return null;
            }
            modelRoot.localPosition = restPosition + Vector3.up * toOffset;
            motion = null;
        }
    }
}
