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
        [SerializeField] private float keyRotationSpeedMultiplier = 3.5f;
        [SerializeField] private float lerpSpeed = 0.05f;

        [Header("Runtime Values")]
        [SerializeField] private float _moveSpeed;
        [SerializeField] private float _rotationSpeed;

        [SerializeField] private CameraShaker cameraShaker;
        public CameraShaker CameraShaker => cameraShaker;
        [SerializeField] private Transform minimapCameraIndicatorHolder;

        DepthOfField depthField;
        private float minFocalDistance = 1f;
        private float yaw = 0f;  // Rotation around the Y-axis
        private float pitch = 0f; // Rotation around the X-axis
        bool slowCamera;
        Vector3 velocity = Vector3.zero;
        float cameraShakeCooldown = 0f;
        private Entity _cameraPositionEntity = Entity.Null;
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
            EdgePanningToggle.OnEdgePanningChanged += OnEdgePanningChanged;
            edgePanning = PlayerPrefs.GetInt(EdgePanningToggle.PlayerPrefKey, 0) == 1;
            _rotationSpeed = _baseRotationSpeed * SettingsManager.Instance.CameraRotationSpeed.Value;
            _moveSpeed = _baseMoveSpeed * SettingsManager.Instance.CameraMovementSpeed.Value;
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
                float heightModifier = Mathf.Clamp(cameraTarget.position.y / 50f, 0.25f, 2f);
                float moveSpeed = _moveSpeed * (InputHandler.Instance.MoveFast_Input ? 2f : 1f) * heightModifier;
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

                // Calculate movement direction in world space
                Vector3 inputDirection = new Vector3(moveX, moveY, moveZ);

                Vector3 forward = Vector3.ProjectOnPlane(battlefieldCamera.transform.forward, Vector3.up).normalized; // Forward ignoring Y rotation
                Vector3 right = Vector3.ProjectOnPlane(battlefieldCamera.transform.right, Vector3.up).normalized;    // Right ignoring Y rotation

                // Combine forward and right for movement in the XZ plane
                Vector3 moveDirection = (right * inputDirection.x) + (forward * inputDirection.z) + (Vector3.up * inputDirection.y);

                //New function to move the camera if on edges of screen
                if (edgePanning)
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
                if (!InputHandler.Instance.EnableCameraRotation_Input)//middle mouse button not held down
                {
                    if (edgePanning)
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
                            cameraTarget.eulerAngles = new Vector3(pitch, yaw, 0f);
                            minFocalDistance = 1f;
                        }
                    }
                    return;
                }
                                                                               //get initial yaw and pitch
                if (InputHandler.Instance.EnableCameraRotation_Input)
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
                cameraTarget.eulerAngles = new Vector3(pitch, yaw, 0f);
                minFocalDistance = 1f;
            }

            void HandleCameraRotationWithKeys()
            {
                if (InputHandler.Instance.RotateRight_Input)
                {
                    yaw = cameraTarget.eulerAngles.y - 0.5f * keyRotationSpeedMultiplier;
                    pitch = cameraTarget.eulerAngles.x;
                    cameraTarget.eulerAngles = new Vector3(pitch, yaw, 0f);
                }
                if (InputHandler.Instance.RotateLeft_Input)
                {
                    yaw = cameraTarget.eulerAngles.y + 0.5f * keyRotationSpeedMultiplier;
                    pitch = cameraTarget.eulerAngles.x;
                    cameraTarget.eulerAngles = new Vector3(pitch, yaw, 0f);
                }
                if (InputHandler.Instance.RotatePitchUp_Input)
                {
                    yaw = cameraTarget.eulerAngles.y;
                    pitch = cameraTarget.eulerAngles.x - 0.5f * keyRotationSpeedMultiplier;
                    if (pitch > 180f) pitch -= 360f;
                    pitch = Mathf.Clamp(pitch, -89f, 89f);
                    cameraTarget.eulerAngles = new Vector3(pitch, yaw, 0f);
                }
                if (InputHandler.Instance.RotatePitchDown_Input)
                {
                    yaw = cameraTarget.eulerAngles.y;
                    pitch = cameraTarget.eulerAngles.x + 0.5f * keyRotationSpeedMultiplier;
                    if (pitch > 180f) pitch -= 360f;
                    pitch = Mathf.Clamp(pitch, -89f, 89f);
                    cameraTarget.eulerAngles = new Vector3(pitch, yaw, 0f);
                }
            }

            HandleCameraMovement();
            HandleCameraRotation();
            HandleCameraRotationWithKeys();

            //rotate slowly camera around target
            // if(UnityEngine.Input.GetKey(KeyCode.Z))
            // {
            //     yaw += 0.25f;
            //     cameraTarget.eulerAngles = new Vector3(pitch, yaw, 0f);
            // }
            // if(UnityEngine.Input.GetKey(KeyCode.X))
            // {
            //     yaw -= 0.25f;
            //     cameraTarget.eulerAngles = new Vector3(pitch, yaw, 0f);
            // }

            // Clamp camera position
            Vector3 clampedPosition = cameraTarget.position;
            clampedPosition.x = Mathf.Clamp(clampedPosition.x, minMaxWidth.x, minMaxWidth.y);
            clampedPosition.y = Mathf.Clamp(clampedPosition.y, minMaxHeight.x, minMaxHeight.y);
            clampedPosition.z = Mathf.Clamp(clampedPosition.z, minMaxDepth.x, minMaxDepth.y);
            cameraTarget.position = clampedPosition;

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
            if (Time.timeScale == 0) return;

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
            cameraTarget.eulerAngles = new Vector3(pitch, yaw, 0f);
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
    }
}