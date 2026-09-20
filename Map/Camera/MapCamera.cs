using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Memori.Input;
using System.Threading.Tasks;
using Memori.Scenes;
using System.Collections;

namespace TJ.Map
{
    public class MapCamera : MonoBehaviour
    {
        [SerializeField] private Camera mapCamera, eventCamera, shopCamera, gamesCamera;
        public Camera MapCameraInstance => mapCamera;
        public Camera EventCamera => eventCamera;
        public Camera ShopCamera => shopCamera;
        public Camera GamesCamera => gamesCamera;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1f;
        public float rotationSpeed = 100f;
        [SerializeField] private bool ignoreBounds = false;
        [SerializeField] private float2 minMaxHeight, minMaxWidth, minMaxDepth;
        public Transform target;

        [Header("Intro")]
        [SerializeField] private Vector3 introPositionOffset = new(-0.2f, 0.05f, 0f);
        [SerializeField] private Transform introStartPosition, introEndPosition;

        [Header("Focus")]
        [SerializeField] private Vector3 focusedNodeOffset = new(-0.3f, 0.2f, 0f);
        [SerializeField] private Vector3 pullBackOffset = new(-0.25f, 0.25f, 0f);

        [Header("Shop")]
        [SerializeField] private Transform shopStartTransform;
        [SerializeField] private Transform shopMainTransform;

        [Header("Post Processing")]
        [SerializeField] private Volume tiltShiftVolume;
        [SerializeField] private Volume focusedOnMapNodeVolume;

        private DepthOfField depthField;
        private float minFocalDistance = 1f;
        private float yaw = 0f;
        private float pitch = 0f;
        private bool skipIntro = false;
        public bool SkipIntro => skipIntro;

        private bool isFreeCameraMode = false;
        private Vector3 savedTargetPosition;
        private Quaternion savedTargetRotation;
        private float savedPitch, savedYaw;
        private float savedVolumeWeight;
        private bool savedMapCameraEnabled;
        private Coroutine volumeLerpCoroutine;

        public void SaveFreeCameraState()
        {
            savedTargetPosition = target.position;
            savedTargetRotation = target.rotation;
            savedPitch = pitch;
            savedYaw = yaw;
            isFreeCameraMode = true;

            savedMapCameraEnabled = mapCamera.enabled;
            if (!savedMapCameraEnabled) mapCamera.enabled = true;

            savedVolumeWeight = focusedOnMapNodeVolume.weight;
            if (savedVolumeWeight > 0f)
            {
                if (volumeLerpCoroutine != null) StopCoroutine(volumeLerpCoroutine);
                volumeLerpCoroutine = StartCoroutine(LerpFocusedOnNodeVolume(0f, 0.3f));
            }
        }

        public void RestoreFreeCameraState()
        {
            isFreeCameraMode = false;
            target.SetPositionAndRotation(savedTargetPosition, savedTargetRotation);
            pitch = savedPitch;
            yaw = savedYaw;

            if (savedVolumeWeight > 0f)
            {
                if (volumeLerpCoroutine != null) StopCoroutine(volumeLerpCoroutine);
                volumeLerpCoroutine = StartCoroutine(LerpFocusedOnNodeVolume(savedVolumeWeight, 0.3f));
            }

            if (!savedMapCameraEnabled) mapCamera.enabled = false;
        }

        MapSceneManager mapSceneManager;

        private void Start()
        {
            tiltShiftVolume.profile.TryGet(out depthField);
            InputHandler.Instance.PrimaryActionPerformed += LeftClick;
            focusedOnMapNodeVolume.weight = 0;
#if !UNITY_EDITOR
            ignoreBounds = false;
#endif
        }

        public void SetUp(MapSceneManager _mapSceneManager)
        {
            mapSceneManager = _mapSceneManager;
        }

        private void Update()
        {
            if (SettingsManager.Instance.SettingsPanelOpen) return;
            if (!mapCamera.enabled) return;

            if (mapSceneManager.AllowMapInput || isFreeCameraMode)
            {
                HandleCameraMovement();
                HandleCameraRotation();
            }

            Vector3 clampedPosition = target.position;
            clampedPosition.x = Mathf.Clamp(clampedPosition.x, minMaxWidth.x, minMaxWidth.y);
            clampedPosition.y = Mathf.Clamp(clampedPosition.y, minMaxHeight.x, minMaxHeight.y);
            clampedPosition.z = Mathf.Clamp(clampedPosition.z, minMaxDepth.x, minMaxDepth.y);
            target.position = ignoreBounds ? target.position : clampedPosition;

            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, target.position, 0.125f),
                Quaternion.Lerp(transform.rotation, target.rotation, 0.5f));

            GetFocalDistance();
        }

        private void HandleCameraMovement()
        {
            float speed = moveSpeed * (InputHandler.Instance.MoveFast_Input ? 2f : 1f);

            float moveX = InputHandler.Instance.MoveX * speed * Time.unscaledDeltaTime;
            float moveZ = InputHandler.Instance.MoveZ * speed * Time.unscaledDeltaTime;
            // The wheel used to arrive inside MoveY; adding ZoomScroll keeps the map zoom exactly as it was.
            float moveY = (InputHandler.Instance.MoveY + InputHandler.Instance.ZoomScroll) * speed * Time.unscaledDeltaTime;

            moveY -= UnityEngine.Input.mouseScrollDelta.y * speed * 2 * Time.deltaTime;

            if (!Input.mousePresent || Input.mousePosition.x < 0 || Input.mousePosition.y < 0 ||
                Input.mousePosition.x > Screen.width || Input.mousePosition.y > Screen.height)
            {
                moveY = 0f;
            }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 moveDirection = (right * moveX) + (forward * moveZ) + (Vector3.up * moveY);

            target.Translate(moveDirection, Space.World);
        }

        private void HandleCameraRotation()
        {
            if (!InputHandler.Instance.EnableCameraRotation_Input) return;

            float mouseYDir = SettingsManager.Instance.InvertMouseY.Value ? 1f : -1f;
            yaw += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            pitch += mouseYDir * Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -90f, 90f);

            target.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        public void LerpToFocusedPosition(Vector3 _position)
        {
            pitch = 35f;
            yaw = 90f;
            minFocalDistance = 0.5f;
            target.SetPositionAndRotation(_position + focusedNodeOffset, Quaternion.Euler(pitch, yaw, 0f));
        }

        public void LerpCameraPullBackFocusPosition(Vector3 _position)
        {
            pitch = 35f;
            yaw = 90f;
            minFocalDistance = 0.5f;
            target.SetPositionAndRotation(_position + pullBackOffset, Quaternion.Euler(pitch, yaw, 0f));
        }

        public async void HandleMapCameraIntro()
        {
            skipIntro = false;
            pitch = 30f;
            yaw = 90f;
            minFocalDistance = 0.5f;

            Vector3 startPosition = introStartPosition.position + introPositionOffset;
            Vector3 endPosition = introEndPosition.position + introPositionOffset;
            Quaternion endRotation = Quaternion.Euler(pitch, yaw, 0f);

            target.SetPositionAndRotation(startPosition, endRotation);
            mapCamera.transform.SetPositionAndRotation(introStartPosition.position, introStartPosition.rotation);

            float duration = 4f;
            float elapsedTime = 0f;
            mapCamera.transform.GetPositionAndRotation(out Vector3 camStartPosition, out Quaternion camStartRotation);

            while (elapsedTime < duration && !skipIntro)
            {
                if (elapsedTime > duration - 0.1f)
                    elapsedTime += Time.deltaTime * 0.1f;
                else if (elapsedTime > duration - 0.5f)
                    elapsedTime += Time.deltaTime * 0.5f;
                else
                    elapsedTime += Time.deltaTime;

                float t = Mathf.Clamp01(elapsedTime / duration);
                mapCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(camStartPosition, endPosition, t),
                    Quaternion.Slerp(camStartRotation, endRotation, t));

                await Task.Yield();
            }

            mapCamera.transform.SetPositionAndRotation(endPosition, endRotation);
            target.SetPositionAndRotation(endPosition, endRotation);
        }

        private void GetFocalDistance()
        {
            if (Physics.SphereCast(mapCamera.transform.position, 0.0125f, mapCamera.transform.forward, out RaycastHit hit, Mathf.Infinity))
            {
                Debug.DrawRay(mapCamera.transform.position, mapCamera.transform.forward * hit.distance, Color.yellow);
                depthField.focusDistance.value = Mathf.Clamp(hit.distance, minFocalDistance, 1000f);
            }
            else
            {
                Debug.DrawRay(mapCamera.transform.position, mapCamera.transform.forward * 1000, Color.white);
            }
        }

        public void OverrideDepthOfField()
        {
            mapCamera.enabled = false;
            minFocalDistance = 1.25f;
            depthField.focusDistance.value = 1.25f;
        }

        private void LeftClick()
        {
            skipIntro = true;
        }

        public async Task EnterShopScene()
        {
            SceneHandler.Instance.TranstionCameras(
                CampaignManager.Instance.MapCamera.MapCameraInstance,
                CampaignManager.Instance.MapCamera.ShopCamera
            );

            await Task.Delay(500);
            OverrideDepthOfField();

            shopCamera.transform.SetPositionAndRotation(shopMainTransform.position, shopMainTransform.rotation);
        }

        public async Task EnterGamesScene()
        {
            SceneHandler.Instance.TranstionCameras(
                CampaignManager.Instance.MapCamera.MapCameraInstance,
                CampaignManager.Instance.MapCamera.GamesCamera
            );

            await Task.Delay(500);
            OverrideDepthOfField();
        }

        public IEnumerator LerpFocusedOnNodeVolume(float _target, float _duration)
        {
            float start = focusedOnMapNodeVolume.weight;
            float time = 0f;
            while (time < _duration)
            {
                time += Time.deltaTime;
                focusedOnMapNodeVolume.weight = Mathf.Lerp(start, _target, time / _duration);
                yield return null;
            }
            focusedOnMapNodeVolume.weight = _target;
        }

        private void OnDestroy()
        {
            if (InputHandler.HasInstance)
                InputHandler.Instance.PrimaryActionPerformed -= LeftClick;
        }
    }
}
