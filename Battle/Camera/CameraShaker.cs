using System.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TJ
{
    public class CameraShaker : MonoBehaviour
    {
        [SerializeField] private Transform objectToShake; // Assign your RTS camera in Inspector
        [SerializeField] private Transform cameraTransform; // Assign your RTS camera in Inspector
        // [SerializeField] private float testForce = 0.5f; // Force for testing (magnitude)
        // [SerializeField] private float testDuration = 0.5f; // Duration for testing (seconds)
        // [SerializeField] private float testShakeSpeed = 10f; // Speed for testing (frequency, adjust for violence)

        private Vector3 originalPosition; // Camera's original position
        private float shakeForce = 0f; // Current shake intensity
        private float shakeDuration = 0f; // Remaining shake time
        private float shakeTimer = 0f; // Timer for decay
        private float shakeSpeed = 0f; // Current shake frequency
        private bool battleEnded = false;

        public bool ShakingEnabled { get; set; } = true;
        public float3 CameraPosition => cameraTransform != null ? (float3)cameraTransform.position : float3.zero;

        void Start()
        {
            if (objectToShake == null)
            {
                objectToShake = Camera.main?.transform; // Fallback to main camera
                if (objectToShake == null)
                {
                    Debug.LogError("CameraShaker: No camera assigned or found!");
                    enabled = false;
                    return;
                }
            }

            originalPosition = objectToShake.localPosition;
            BattleManager.Instance.OnGamePhaseChanged += OnGamePhaseChanged;
            ShakingEnabled = SettingsManager.Instance.CameraShakeEnabled.Value;
            SettingsManager.Instance.CameraShakeEnabled.OnValueChanged += OnCameraShakeSettingChanged;
        }

        private void OnDestroy()
        {
            if (BattleManager.HasInstance)
                BattleManager.Instance.OnGamePhaseChanged -= OnGamePhaseChanged;
            if (SettingsManager.HasInstance)
                SettingsManager.Instance.CameraShakeEnabled.OnValueChanged -= OnCameraShakeSettingChanged;
        }

        private void OnCameraShakeSettingChanged(bool isEnabled)
        {
            ShakingEnabled = isEnabled;
            if (!isEnabled) StopShake();
        }

        private void OnGamePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.PostGame) return;
            battleEnded = true;
            shakeDuration = 0f;
            if (objectToShake != null) objectToShake.localPosition = originalPosition;
        }

        void Update()
        {
            #if UNITY_EDITOR
            // Test shake with C key
            // if (Input.GetKeyDown(KeyCode.C))
            // {
            //     ShakeCamera(testForce, testDuration, testShakeSpeed);
            //     Debug.Log($"Testing camera shake: Force={testForce}, Duration={testDuration}, Speed={testShakeSpeed}");
            // }
            #endif

            // Apply shake if active
            if (shakeDuration > 0f)
            {
                shakeTimer -= Time.deltaTime;
                shakeDuration -= Time.deltaTime;

                if (shakeDuration <= 0f)
                {
                    // Stop shake and restore position
                    shakeDuration = 0f;
                    objectToShake.localPosition = originalPosition;
                }
                else
                {
                    // Calculate decay (linearly reduce force)
                    float currentForce = shakeForce * (shakeDuration / shakeTimer);

                    // Use Perlin noise for shake offset, scaled by speed
                    float time = Time.time * shakeSpeed;
                    Vector3 offset = new Vector3(
                        (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f, // -1 to 1
                        (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f,
                        (Mathf.PerlinNoise(time, time) - 0.5f) * 2f
                    ) * currentForce;

                    objectToShake.localPosition = originalPosition + offset;
                }
            }
        }
        // Distance is measured from the ground point the camera looks at, so an overview camera still feels a
        // charge it is watching. A higher camera gets a smaller shake.
        public void ChargeShake(float3 _chargePosition, float force)
        {
            if (battleEnded) return;

            Vector3 cameraPosition = cameraTransform.position;
            Vector3 forward = cameraTransform.forward;
            float reach = forward.y < -0.05f ? Mathf.Min(cameraPosition.y / -forward.y, 150f) : 150f;
            Vector3 focus = cameraPosition + forward * reach;
            float distance = Vector2.Distance(new Vector2(focus.x, focus.z), new Vector2(_chargePosition.x, _chargePosition.z));

            float falloff = 1f - Mathf.Clamp01((distance - 20f) / 90f);
            float height = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01((cameraPosition.y - 15f) / 85f));
            float scaled = force * falloff * height;
            if (scaled <= 0.04f) return;

            if (shakeDuration > 0f && scaled < shakeForce) return;
            if (shakeDuration > 0f) objectToShake.localPosition = originalPosition;
            ShakeCamera(scaled, 0.7f, 5f);
        }
        public void ExplosionShake(float3 position)
        {
            if (battleEnded) return;
            float distance = math.distance(position, cameraTransform.position);
            if (distance > 45f) return;

            float normalizedDist = 1f - (distance / 45f);
            float force = Mathf.Lerp(0.2f, 0.9f, normalizedDist);

            if (shakeDuration > 0f && force <= shakeForce) return;
            if (shakeDuration > 0f) objectToShake.localPosition = originalPosition;

            // Debug.Log($"Explosion shake triggered at position {position}, distance from camera: {distance}, force: {force}");
            ShakeCamera(force, 0.5f, 6f);
        }

        // A spell the player aimed fades out by 120 m instead of cutting off at the explosion's 45 m.
        public void SpellImpactShake(float3 position, float force)
        {
            if (battleEnded) return;
            float distance = math.distance(position, cameraTransform.position);
            float scaled = force * (1f - math.saturate((distance - 30f) / 90f));
            if (scaled <= 0.05f) return;

            if (shakeDuration > 0f && scaled < shakeForce) return;
            if (shakeDuration > 0f) objectToShake.localPosition = originalPosition;
            ShakeCamera(scaled, 0.45f, 7f);
        }

        public void NearCombatShake()
        {
            if (battleEnded) return;
            if(shakeDuration > 0f) return;

            ShakeCamera(0.075f, 0.9f, 3f);
        }

        /// <param name="force">Magnitude of shake (e.g., 0.5 for subtle, 2.0 for strong).</param>
        /// <param name="duration">Duration of shake in seconds.</param>
        /// <param name="shakeSpeed">Frequency of shake (e.g., 5 for smooth, 20 for violent).</param>
        public void StopShake()
        {
            shakeDuration = 0f;
            if (objectToShake != null) objectToShake.localPosition = originalPosition;
        }

        public void ShakeCamera(float force, float duration, float shakeSpeed)
        {
            if (objectToShake == null || !ShakingEnabled) return;

            shakeForce = force;
            shakeDuration = duration;
            shakeTimer = duration;
            this.shakeSpeed = shakeSpeed;

            // Update original position
            originalPosition = objectToShake.localPosition;
        }
    }
}