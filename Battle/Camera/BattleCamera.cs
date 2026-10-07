using Memori.Input;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using Memori.Utilities;
using Unity.Collections;
using Memori.Scenes;
using TJ.Settings;

namespace TJ
{
    public class BattleCamera : MonoBehaviour
    {
        [SerializeField] private Camera battlefieldCamera;
        [SerializeField] private Camera tavernCamera;
        [SerializeField] private float2 minMaxHeight, minMaxWidth, minMaxDepth;
        [SerializeField] private Volume tiltShiftVolume;
        // [SerializeField] private Vector3 startPosition, startRoation;
        [SerializeField] private LayerMask tavernGOLayermask;
        // [Header("Press P Key")][SerializeField] private bool toggleSlowCamera;
        [SerializeField] private bool edgePanning = true;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform playerStartTransform, enemyStartTransform;

        [Header("Base Values")]
        [SerializeField] private float _baseMoveSpeed = 150f;
        [SerializeField] private float _baseRotationSpeed = 450f;
        [SerializeField] private float scrollSpeedMultiplier = 10f;
        [SerializeField] private float _baseZoomStep = 24f;
        [SerializeField] private float zoomSharpness = 8f;
        [SerializeField] private float keyRotationSpeedMultiplier = 3.5f;
        [SerializeField] private float lerpSpeed = 0.05f;

        [Header("Runtime Values")]
        [SerializeField] private float _moveSpeed;
        [SerializeField] private float _rotationSpeed;
        [SerializeField] private float _zoomStep;

        [SerializeField] private CameraShaker cameraShaker;
        public CameraShaker CameraShaker => cameraShaker;
        [SerializeField] private Transform minimapCameraIndicatorHolder;

        DepthOfField depthField;
        private float minFocalDistance = 1f;
        private float yaw = 0f;  // Rotation around the Y-axis
        private float pitch = 0f; // Rotation around the X-axis
        bool slowCamera;
        Vector3 velocity = Vector3.zero;
        float pendingZoom;
        float cameraShakeCooldown = 0f;
        private Entity _cameraPositionEntity = Entity.Null;

        #region Photo mode
        [Header("Photo Mode")]
        [SerializeField] private float2 photoMinMaxHeight = new float2(0.5f, 450f);
        [SerializeField] private float2 photoMinMaxWidth = new float2(-330f, 330f);
        [SerializeField] private float2 photoMinMaxDepth = new float2(-330f, 330f);
        [SerializeField] private float photoNearClip = 0.1f;
        private bool photoMode;
        // Always 0 outside photo mode, so every rotation write below stays level.
        private float roll;
        private float photoSpeedScale = 1f;
        // Head bob in camera space, and the world offset applied this frame; removed before the next smoothing step.
        private Vector3 bob, appliedBob;
        private Vector3 savedTargetPosition;
        private Quaternion savedTargetRotation;
        private float savedPitch, savedYaw, savedBattlefieldFov, savedTavernFov, savedNearClip;
        // While set, the backdrop's depth of field is photo mode's and the sphere-cast autofocus stays out of it.
        private bool photoBackdropBlur;
        private bool savedBackdropActive;
        private float savedBackdropFocus, savedBackdropFocalLength, savedBackdropAperture;

        public bool InPhotoMode => photoMode;
        public Camera TavernCamera => tavernCamera;
        public float PhotoFov => battlefieldCamera.fieldOfView;
        public float PhotoRoll => roll;
        private float2 HeightLimits => photoMode ? photoMinMaxHeight : minMaxHeight;
        private float2 WidthLimits => photoMode ? photoMinMaxWidth : minMaxWidth;
        private float2 DepthLimits => photoMode ? photoMinMaxDepth : minMaxDepth;

        public void EnterPhoto()
        {
            if (photoMode) return;
            savedTargetPosition = cameraTarget.position;
            savedTargetRotation = cameraTarget.rotation;
            savedPitch = pitch;
            savedYaw = yaw;
            savedBattlefieldFov = battlefieldCamera.fieldOfView;
            savedTavernFov = tavernCamera.fieldOfView;
            savedNearClip = battlefieldCamera.nearClipPlane;
            battlefieldCamera.nearClipPlane = photoNearClip;
            if (depthField != null)
            {
                savedBackdropActive = depthField.active;
                savedBackdropFocus = depthField.focusDistance.value;
                savedBackdropFocalLength = depthField.focalLength.value;
                savedBackdropAperture = depthField.aperture.value;
            }
            photoBackdropBlur = false;
            cameraShaker.StopShake();
            cameraShaker.ShakingEnabled = false;
            roll = 0f;
            photoSpeedScale = 1f;
            photoMode = true;
        }
        public void ExitPhoto()
        {
            if (!photoMode) return;
            photoMode = false;
            roll = 0f;
            bob = appliedBob = Vector3.zero;
            cameraTarget.SetPositionAndRotation(savedTargetPosition, savedTargetRotation);
            pitch = savedPitch;
            yaw = savedYaw;
            battlefieldCamera.fieldOfView = savedBattlefieldFov;
            tavernCamera.fieldOfView = savedTavernFov;
            battlefieldCamera.nearClipPlane = savedNearClip;
            RestoreBackdropBlur();
            SnapCamerasToTarget();
            // The setting is the source of truth; a saved copy could undo a change made in Settings.
            cameraShaker.ShakingEnabled = SettingsManager.Instance.CameraShakeEnabled.Value;
        }
        // Both cameras take the same lens, or the tavern backdrop slides against the board.
        public void SetPhotoFov(float fov)
        {
            if (!photoMode) return;
            battlefieldCamera.fieldOfView = fov;
            tavernCamera.fieldOfView = fov;
        }
        public void SetPhotoRoll(float degrees)
        {
            if (photoMode) roll = degrees;
        }
        public void SetPhotoSpeedScale(float scale)
        {
            if (photoMode) photoSpeedScale = scale;
        }
        /// <summary>Moves the target and both cameras together, with no smoothing lag: following a squad, a dolly zoom.</summary>
        public void FollowShift(Vector3 delta)
        {
            if (!photoMode && !followMode) return;
            cameraTarget.position += delta;
            battlefieldCamera.transform.position += delta;
            tavernCamera.transform.localPosition += delta;
        }

        /// <summary>0 hands the tavern backdrop's blur back to the game; above 0 blurs everything behind the board by that strength.</summary>
        public void SetPhotoBackdropBlur(float strength)
        {
            if (!photoMode || depthField == null) return;
            if (strength <= 0f)
            {
                RestoreBackdropBlur();
                return;
            }
            // Focus sits just in front of the lens, so the whole tavern is behind the focal plane; the lens sets how soft it gets.
            photoBackdropBlur = true;
            depthField.active = true;
            depthField.focusDistance.value = BackdropFocusDistance;
            depthField.aperture.value = BackdropAperture;
            depthField.focalLength.value = Mathf.Lerp(BackdropMinFocalLength, BackdropMaxFocalLength, strength);
        }
        private const float BackdropFocusDistance = 0.1f;
        private const float BackdropAperture = 16f;
        private const float BackdropMinFocalLength = 12f;
        private const float BackdropMaxFocalLength = 55f;

        private void RestoreBackdropBlur()
        {
            if (!photoBackdropBlur || depthField == null) return;
            photoBackdropBlur = false;
            depthField.active = savedBackdropActive;
            depthField.focusDistance.value = savedBackdropFocus;
            depthField.focalLength.value = savedBackdropFocalLength;
            depthField.aperture.value = savedBackdropAperture;
        }

        /// <summary>Camera-space offset for following on foot; zero turns it off.</summary>
        public void SetFollowBob(Vector3 cameraSpaceOffset)
        {
            if (photoMode || followMode) bob = cameraSpaceOffset;
        }

        public void ResetPhotoView()
        {
            if (!photoMode) return;
            roll = 0f;
            bob = appliedBob = Vector3.zero;
            SetPhotoFov(savedBattlefieldFov);
            cameraTarget.SetPositionAndRotation(savedTargetPosition, savedTargetRotation);
            pitch = savedPitch;
            yaw = savedYaw;
            SnapCamerasToTarget();
        }
        // Clears the follow smoothing too, so the camera does not swing back from where photo mode left it.
        private void SnapCamerasToTarget()
        {
            velocity = Vector3.zero;
            pendingZoom = 0f;
            battlefieldCamera.transform.SetPositionAndRotation(cameraTarget.position, cameraTarget.rotation);
            tavernCamera.transform.SetLocalPositionAndRotation(cameraTarget.position, cameraTarget.rotation);
        }
        private static bool PhotoLookHeld()
        {
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
        }
        private static bool PhotoSlowHeld()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.ctrlKey.isPressed;
        }
        #endregion

        #region Follow camera
        // Gameplay limits and lens; only edge panning is off and right-drag turns, since orders are off.
        private bool followMode;

        public void EnterFollow() => followMode = true;

        /// <summary>The camera stays exactly where it is; only the bob is taken out on the next frame.</summary>
        public void ExitFollow()
        {
            if (!followMode) return;
            followMode = false;
            bob = Vector3.zero;
            SyncYawPitch();
        }

        /// <summary>Moves the target to a new shot; the cameras glide there through the normal smoothing.</summary>
        public void FollowPlace(Vector3 position, Quaternion rotation)
        {
            if (!photoMode && !followMode) return;
            Vector3 euler = rotation.eulerAngles;
            cameraTarget.SetPositionAndRotation(position, Quaternion.Euler(euler.x, euler.y, roll));
            pendingZoom = 0f;
            SyncYawPitch();
        }

        // Edge panning writes yaw and pitch straight back to the target, so they must match it.
        private void SyncYawPitch()
        {
            yaw = cameraTarget.eulerAngles.y;
            pitch = cameraTarget.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
        }
        #endregion
        private void Start()
        {
            yaw = playerStartTransform.position.x;
            pitch = playerStartTransform.position.y;
            cameraTarget.SetPositionAndRotation(playerStartTransform.position, playerStartTransform.rotation);
            battlefieldCamera.transform.SetPositionAndRotation(playerStartTransform.position, playerStartTransform.rotation);
            tavernCamera.transform.SetLocalPositionAndRotation(tavernCamera.transform.localPosition, playerStartTransform.rotation);

            depthField = tiltShiftVolume.profile.TryGet<DepthOfField>(out depthField) ? depthField : null;
            
            SettingsManager.Instance.CameraRotationSpeed.OnValueChanged += OnCameraRotationSpeedChanged;
            SettingsManager.Instance.CameraMovementSpeed.OnValueChanged += OnCameraMovementSpeedChanged;
            SettingsManager.Instance.CameraZoomSpeed.OnValueChanged += OnCameraZoomSpeedChanged;
            EdgePanningToggle.OnEdgePanningChanged += OnEdgePanningChanged;
            edgePanning = PlayerPrefs.GetInt(EdgePanningToggle.PlayerPrefKey, 0) == 1;
            _rotationSpeed = _baseRotationSpeed * SettingsManager.Instance.CameraRotationSpeed.Value;
            _moveSpeed = _baseMoveSpeed * SettingsManager.Instance.CameraMovementSpeed.Value;
            OnCameraZoomSpeedChanged(SettingsManager.Instance.CameraZoomSpeed.Value);
            BattleManager.Instance.OnGamePhaseChanged += OnGamePhaseChanged;
        }

        private bool battleEnded = false;
        private void OnGamePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.PostGame) battleEnded = true;
        }
        private void Update()
        {
            // HandleSlowCamera();

            if (!battlefieldCamera.enabled) return;

            if (BattleManager.Instance.BattlefieldTutorial.TutorialIsOpen) return;

            if (SettingsManager.Instance.SettingsPanelOpen) return;

            void HandleCameraMovement()
            {
                //height modifier at 50 I want max move speed. at 1 I want 0.25 move speed.
                float heightModifier = Mathf.Clamp(cameraTarget.position.y / 50f, photoMode ? 0.05f : 0.25f, 2f);
                float moveSpeed = _moveSpeed * (InputHandler.Instance.MoveFast_Input ? 2f : 1f) * heightModifier;
                if (photoMode) moveSpeed *= photoSpeedScale * (PhotoSlowHeld() ? 0.25f : 1f);
                // Debug.Log($"moveSpeed: {moveSpeed}, height: {cameraTarget.position.y}, heightModifier: {heightModifier}");

                float moveX = InputHandler.Instance.MoveX * moveSpeed * Time.unscaledDeltaTime; // A/D keys
                float moveZ = InputHandler.Instance.MoveZ * moveSpeed * Time.unscaledDeltaTime;   // W/S keys
                float moveY = 0f;

                if (InputHandler.Instance.MoveY > 0) // Move up
                {
                    moveY = -moveSpeed * scrollSpeedMultiplier * Time.unscaledDeltaTime;
                }
                else if (InputHandler.Instance.MoveY < 0) // Move down
                {
                    moveY = moveSpeed * scrollSpeedMultiplier * Time.unscaledDeltaTime;
                }

                moveY -= InputHandler.Instance.MoveY * moveSpeed * scrollSpeedMultiplier * Time.unscaledDeltaTime;

                // A wheel notch is a fixed distance spent over a few frames, so zoom feels the same at any frame rate.
                pendingZoom -= InputHandler.Instance.ZoomScroll * _zoomStep * heightModifier;
                pendingZoom = Mathf.Clamp(pendingZoom, HeightLimits.x - cameraTarget.position.y, HeightLimits.y - cameraTarget.position.y);
                float zoomThisFrame = pendingZoom * (1f - Mathf.Exp(-zoomSharpness * Time.unscaledDeltaTime));
                pendingZoom -= zoomThisFrame;
                moveY += zoomThisFrame;

                // Calculate movement direction in world space
                Vector3 inputDirection = new Vector3(moveX, moveY, moveZ);

                Vector3 forward = Vector3.ProjectOnPlane(battlefieldCamera.transform.forward, Vector3.up).normalized; // Forward ignoring Y rotation
                Vector3 right = Vector3.ProjectOnPlane(battlefieldCamera.transform.right, Vector3.up).normalized;    // Right ignoring Y rotation
                if (photoMode)
                {
                    // A rolled camera's right vector leaves the ground plane, so photo mode steers by heading alone.
                    Quaternion heading = Quaternion.Euler(0f, battlefieldCamera.transform.eulerAngles.y, 0f);
                    forward = heading * Vector3.forward;
                    right = heading * Vector3.right;
                }

                // Combine forward and right for movement in the XZ plane
                Vector3 moveDirection = (right * inputDirection.x) + (forward * inputDirection.z) + (Vector3.up * inputDirection.y);

                //New function to move the camera if on edges of screen
                if (edgePanning && !photoMode && !followMode)
                {
                    float edgeSize = 10f; // Size of the edge area in pixels
                    if (Input.mousePosition.y < edgeSize)
                    {
                        moveDirection += moveSpeed * Time.unscaledDeltaTime * -forward;
                    }
                    else if (Input.mousePosition.y > Screen.height - edgeSize)
                    {
                        moveDirection += moveSpeed * Time.unscaledDeltaTime * forward;
                    }

                    // Move left/right when mouse is on left/right edge and in the bottom 25% of the screen
                    if (Input.mousePosition.y <= Screen.height * 0.25f)
                    {
                        if (Input.mousePosition.x < edgeSize)
                            moveDirection += moveSpeed * Time.unscaledDeltaTime * -right;
                        else if (Input.mousePosition.x > Screen.width - edgeSize)
                            moveDirection += moveSpeed * Time.unscaledDeltaTime * right;
                    }
                }

                //if camera not over game window, zero out move direction 
                if(!Input.mousePresent || Input.mousePosition.x < 0 || Input.mousePosition.y < 0 || Input.mousePosition.x > Screen.width || Input.mousePosition.y > Screen.height)
                {
                    moveDirection = Vector3.zero;
                }

                // Move the target
                cameraTarget.Translate(moveDirection, Space.World);
            }

            void HandleCameraRotation()
            {
                bool lookHeld = InputHandler.Instance.EnableCameraRotation_Input || ((photoMode || followMode) && PhotoLookHeld());
                if (!lookHeld)//middle mouse button not held down
                {
                    if (edgePanning && !photoMode && !followMode)
                    {
                        //if mouse is over the game window
                        if(Input.mousePresent && Input.mousePosition.x >= 0 && Input.mousePosition.y >= 0 && Input.mousePosition.x <= Screen.width && Input.mousePosition.y <= Screen.height)
                        {
                            //rotate camera when mouse is on edge of screen and in the top 75% of the screen
                            float edgeSize = 10f; // Size of the edge area in pixels
                            if (Input.mousePosition.y > Screen.height * 0.25f && Input.mousePosition.x < edgeSize)
                            {
                                yaw -= _rotationSpeed * Time.unscaledDeltaTime;
                            }
                            else if (Input.mousePosition.y > Screen.height * 0.25f && Input.mousePosition.x > Screen.width - edgeSize)
                            {
                                yaw += _rotationSpeed * Time.unscaledDeltaTime;
                            }
                            else
                            {
                                return;
                            }
                            cameraTarget.eulerAngles = new Vector3(pitch, yaw, roll);
                            minFocalDistance = 1f;
                        }
                    }
                    return;
                }
                                                                               //get initial yaw and pitch
                if (lookHeld)
                {
                    yaw = cameraTarget.eulerAngles.y;
                    pitch = cameraTarget.eulerAngles.x;
                }

                float mouseYDir = SettingsManager.Instance.InvertMouseY.Value ? 1f : -1f;
                yaw += Input.GetAxis("Mouse X") * _rotationSpeed * Time.unscaledDeltaTime;
                pitch += mouseYDir * Input.GetAxis("Mouse Y") * _rotationSpeed * Time.unscaledDeltaTime;

                // Normalize the read-back-from-eulerAngles value (0..360) to a signed range, then clamp
                // strictly inside 90 degrees to avoid the gimbal-lock flip when looking straight down.
                if (pitch > 180f) pitch -= 360f;
                pitch = Mathf.Clamp(pitch, -89f, 89f);
                cameraTarget.eulerAngles = new Vector3(pitch, yaw, roll);
                minFocalDistance = 1f;
            }

            void HandleCameraRotationWithKeys()
            {
                if (InputHandler.Instance.RotateRight_Input)
                {
                    yaw = cameraTarget.eulerAngles.y - 0.5f * keyRotationSpeedMultiplier;
                    pitch = cameraTarget.eulerAngles.x;
                    cameraTarget.eulerAngles = new Vector3(pitch, yaw, roll);
                }
                if (InputHandler.Instance.RotateLeft_Input)
                {
                    yaw = cameraTarget.eulerAngles.y + 0.5f * keyRotationSpeedMultiplier;
                    pitch = cameraTarget.eulerAngles.x;
                    cameraTarget.eulerAngles = new Vector3(pitch, yaw, roll);
                }
                if (InputHandler.Instance.RotatePitchUp_Input)
                {
                    yaw = cameraTarget.eulerAngles.y;
                    pitch = cameraTarget.eulerAngles.x - 0.5f * keyRotationSpeedMultiplier;
                    if (pitch > 180f) pitch -= 360f;
                    pitch = Mathf.Clamp(pitch, -89f, 89f);
                    cameraTarget.eulerAngles = new Vector3(pitch, yaw, roll);
                }
                if (InputHandler.Instance.RotatePitchDown_Input)
                {
                    yaw = cameraTarget.eulerAngles.y;
                    pitch = cameraTarget.eulerAngles.x + 0.5f * keyRotationSpeedMultiplier;
                    if (pitch > 180f) pitch -= 360f;
                    pitch = Mathf.Clamp(pitch, -89f, 89f);
                    cameraTarget.eulerAngles = new Vector3(pitch, yaw, roll);
                }
            }

            HandleCameraMovement();
            HandleCameraRotation();
            HandleCameraRotationWithKeys();
            if (photoMode)
            {
                Vector3 euler = cameraTarget.eulerAngles;
                cameraTarget.eulerAngles = new Vector3(euler.x, euler.y, roll);
            }

            //rotate slowly camera around target
            // if(UnityEngine.Input.GetKey(KeyCode.Z))
            // {
            //     yaw += 0.25f;
            //     cameraTarget.eulerAngles = new Vector3(pitch, yaw, roll);
            // }
            // if(UnityEngine.Input.GetKey(KeyCode.X))
            // {
            //     yaw -= 0.25f;
            //     cameraTarget.eulerAngles = new Vector3(pitch, yaw, roll);
            // }

            // Clamp camera position
            Vector3 clampedPosition = cameraTarget.position;
            clampedPosition.x = Mathf.Clamp(clampedPosition.x, WidthLimits.x, WidthLimits.y);
            clampedPosition.y = Mathf.Clamp(clampedPosition.y, HeightLimits.x, HeightLimits.y);
            clampedPosition.z = Mathf.Clamp(clampedPosition.z, DepthLimits.x, DepthLimits.y);
            cameraTarget.position = clampedPosition;

            if (appliedBob != Vector3.zero)
            {
                battlefieldCamera.transform.position -= appliedBob;
                tavernCamera.transform.localPosition -= appliedBob;
                appliedBob = Vector3.zero;
            }

            Vector3 battlefieldGoalPos = Vector3.SmoothDamp(
                battlefieldCamera.transform.position,
                cameraTarget.position,
                ref velocity,
                lerpSpeed * Time.unscaledDeltaTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
                );

            Vector3 tavernGoalPos = Vector3.SmoothDamp(
                tavernCamera.transform.localPosition,
                cameraTarget.position,
                ref velocity,
                lerpSpeed * Time.unscaledDeltaTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
                );

            battlefieldCamera.transform.position = Vector3.Lerp(battlefieldCamera.transform.position, battlefieldGoalPos, 0.15f);
            battlefieldCamera.transform.rotation = Quaternion.Lerp(battlefieldCamera.transform.rotation, cameraTarget.rotation, 0.5f);

            tavernCamera.transform.localPosition = Vector3.Lerp(tavernCamera.transform.localPosition, tavernGoalPos, 0.15f);
            tavernCamera.transform.localRotation = Quaternion.Lerp(tavernCamera.transform.localRotation, cameraTarget.rotation, 0.5f);

            if (bob != Vector3.zero)
            {
                appliedBob = battlefieldCamera.transform.rotation * bob;
                battlefieldCamera.transform.position += appliedBob;
                tavernCamera.transform.localPosition += appliedBob;
            }

            GetFocalDistance();
            HandleCameraShakerWhenNeabyBattle();
            HandleMinimapCameraIndicator();
            PushCameraPositionToECS();
        }
        private void PushCameraPositionToECS()
        {
            if (battleEnded) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            if (_cameraPositionEntity == Entity.Null)
            {
                var query = em.CreateEntityQuery(ComponentType.ReadWrite<CameraPositionComponent>());
                if (query.IsEmptyIgnoreFilter) { query.Dispose(); return; }
                _cameraPositionEntity = query.GetSingletonEntity();
                query.Dispose();
            }

            if (!em.Exists(_cameraPositionEntity))
            {
                _cameraPositionEntity = Entity.Null;
                return;
            }

            em.SetComponentData(_cameraPositionEntity, new CameraPositionComponent
            {
                Position = battlefieldCamera.transform.position
            });
        }
        private void GetFocalDistance()
        {
            if (photoBackdropBlur) return;
            //raycast from the camera out until it hits something
            //if it hits something, set the focal distance to the distance between the camera and the hit point
            //if it doesn't hit anything, set the focal distance to the max zoom

            if (Physics.SphereCast(tavernCamera.transform.position, 0.125f, tavernCamera.transform.forward, out RaycastHit hit, Mathf.Infinity, tavernGOLayermask, QueryTriggerInteraction.UseGlobal))
            {
                Debug.DrawRay(tavernCamera.transform.position, tavernCamera.transform.forward * hit.distance, Color.green);
                depthField.focusDistance.value = Mathf.Clamp(hit.distance, minFocalDistance, 1000f);
                // Debug.Log($"hit distance: {hit.distance}");
            }
            else
            {
                Debug.DrawRay(tavernCamera.transform.position, tavernCamera.transform.forward * 1000, Color.red);
                // mainCamera.focalLength = maxZoom;
            }
        }
//         private void HandleSlowCamera()
//         {
//             if (Input.GetKeyDown(KeyCode.P))
//             {
//                 slowCamera = !slowCamera;
//                 if (slowCamera)
//                 {
//                     _moveSpeed = 2f;
//                     rotationSpeed = 20f;
//                 }
//                 else
//                 {
//                     _moveSpeed = 50f;
//                     rotationSpeed = 200f;
//                 }

//             }
//         }
        private void HandleCameraShakerWhenNeabyBattle()
        {
            if (Time.timeScale == 0 || photoMode) return;

            cameraShakeCooldown -= Time.unscaledDeltaTime;
            if (cameraShakeCooldown > 0)
            {
                return;
            }
            else
            {
                cameraShakeCooldown = 1f;
            }

            //check if there is a battle nearby
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadMovementComponent>(), ComponentType.ReadOnly<InCombat>());
            if(query.IsEmpty) {
                query.Dispose();
                return;
            }
            using var squadEntities = query.ToEntityArray(Allocator.TempJob);
            if (squadEntities.Length == 0)
            {
                query.Dispose();
                return;
            }

            bool withinDistance = false;
            foreach (var squadEntity in squadEntities)
            {
                if(!entityManager.HasComponent<SquadMovementComponent>(squadEntity)) continue;
                
                SquadMovementComponent SquadMovementComponent = entityManager.GetComponentData<SquadMovementComponent>(squadEntity);
                float3 squadCenter = SquadMovementComponent.SquadCenter;
                float distance = math.distance(battlefieldCamera.transform.position, squadCenter);
                // Debug.Log($"BattleCamera: distance to squad {squadEntity.Index}: {distance}");
                if (distance < 30f)
                {
                    withinDistance = true;
                    break;
                }
            }
            query.Dispose();
            if (withinDistance)
            {
                cameraShaker.NearCombatShake();
            }
        }
        public void SetFaction(Team _faction)
        {
            Transform targetTransform = _faction == Team.Player ? playerStartTransform : enemyStartTransform;
            cameraTarget.SetPositionAndRotation(targetTransform.position, targetTransform.rotation);
        }
        public void FocusOnPosition(Vector3 worldPosition)
        {
            Vector3 camPosXZ = new(battlefieldCamera.transform.position.x, 0f, battlefieldCamera.transform.position.z);
            Vector3 squadPosXZ = new(worldPosition.x, 0f, worldPosition.z);
            Vector3 toCamera = (camPosXZ - squadPosXZ).normalized;
            Vector3 offset = worldPosition + toCamera * 40f;
            cameraTarget.position = new Vector3(offset.x, cameraTarget.position.y, offset.z);

            Vector3 lookDir = -toCamera;
            yaw = Mathf.Atan2(lookDir.x, lookDir.z) * Mathf.Rad2Deg;
            pitch = Mathf.Atan2(cameraTarget.position.y - worldPosition.y, 40f) * Mathf.Rad2Deg;
            cameraTarget.eulerAngles = new Vector3(pitch, yaw, roll);
        }
        public void LookAtGroundPosition(Vector3 worldPosition)
        {
            // Height and rotation stay; the camera backs off along its view so the point lands mid-screen.
            float lookPitch = cameraTarget.eulerAngles.x;
            if (lookPitch > 180f) lookPitch -= 360f;
            float backOff = lookPitch > 1f
                ? Mathf.Min((cameraTarget.position.y - worldPosition.y) / Mathf.Tan(lookPitch * Mathf.Deg2Rad), minMaxDepth.y)
                : 0f;
            Vector3 forward = Quaternion.Euler(0f, cameraTarget.eulerAngles.y, 0f) * Vector3.forward;
            Vector3 position = worldPosition - forward * backOff;
            cameraTarget.position = new Vector3(position.x, cameraTarget.position.y, position.z);
        }
        private void HandleMinimapCameraIndicator()
        {
            //lock the y value to 0
            minimapCameraIndicatorHolder.position = new Vector3(cameraTarget.position.x, 10f, cameraTarget.position.z);
            minimapCameraIndicatorHolder.rotation = Quaternion.Euler(90f, cameraTarget.eulerAngles.y, 0f);
        }
        public void OnDestroy()
        {
            if (SettingsManager.HasInstance)
            {
                SettingsManager.Instance.CameraRotationSpeed.OnValueChanged -= OnCameraRotationSpeedChanged;
                SettingsManager.Instance.CameraMovementSpeed.OnValueChanged -= OnCameraMovementSpeedChanged;
                SettingsManager.Instance.CameraZoomSpeed.OnValueChanged -= OnCameraZoomSpeedChanged;
            }
            EdgePanningToggle.OnEdgePanningChanged -= OnEdgePanningChanged;
            if (BattleManager.HasInstance)
                BattleManager.Instance.OnGamePhaseChanged -= OnGamePhaseChanged;
        }
        private void OnEdgePanningChanged(bool value)
        {
            edgePanning = value;
        }
        private void OnCameraRotationSpeedChanged(float value)
        {
            value = Mathf.Clamp(value, 0.1f, 1f);
            _rotationSpeed = _baseRotationSpeed * value;
        }
        private void OnCameraMovementSpeedChanged(float value)
        {
            value = Mathf.Clamp(value, 0.1f, 1f);
            _moveSpeed = _baseMoveSpeed * value;
        }
        private void OnCameraZoomSpeedChanged(float value)
        {
            value = Mathf.Clamp(value, 0.1f, 1f);
            _zoomStep = _baseZoomStep * value;
        }
    }
}