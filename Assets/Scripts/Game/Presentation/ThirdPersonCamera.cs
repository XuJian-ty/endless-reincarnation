using UnityEngine;
using Game.GameFlow;
using Game.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Presentation
{
    /// <summary>
    /// Third-person follow camera.
    /// Resolves the player target and input handler automatically at runtime.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Sensitivity")]
        [SerializeField] private float _sensitivityX = 3f;
        [SerializeField] private float _sensitivityY = 2f;
        [SerializeField] private float _minPitch = -20f;
        [SerializeField] private float _maxPitch = 60f;

        [Header("Distance And Offset")]
        [SerializeField] private float _distance = 5f;
        [SerializeField] private float _targetHeight = 1.5f;

        [Header("Aim Zoom")]
        [SerializeField] private bool _enableAimZoom = true;
        [SerializeField] private float _aimFieldOfView = 46f;
        [SerializeField] private float _aimZoomFieldOfView = 22f;
        [SerializeField] private float _aimDistance = 5f;
        [SerializeField] private float _aimTargetHeightOffset = 0.1f;
        [SerializeField] private Vector3 _aimShoulderOffset = new Vector3(0.55f, 0.08f, 0f);
        [SerializeField] [Range(0.05f, 1f)] private float _aimZoomStep = 0.25f;
        [SerializeField] [Min(1f)] private float _aimZoomBoost = 1.45f;
        [SerializeField] [Range(1f, 3f)] private float _aimCrosshairMaxScale = 1.85f;
        [SerializeField] private float _aimTransitionSpeed = 8f;

        [Header("Collision")]
        [SerializeField] private LayerMask _collisionMask;
        [SerializeField] private float _collisionRadius = 0.2f;

        private Camera _cameraComponent;
        private Transform _target;
        private PlayerInputHandler _inputHandler;
        private float _yaw;
        private float _pitch;
        private float _defaultFieldOfView;
        private float _targetAimBlend;
        private float _currentAimBlend;
        private float _targetAimZoomLevel;
        private float _currentAimZoomLevel;
        private bool _anglesInitialized;
        private bool _loggedMissingTargetWarning;
        private bool _loggedMissingInputWarning;

        public static ThirdPersonCamera Active { get; private set; }
        public float Yaw => _yaw;
        public float Pitch => _pitch;

        public float GetMovementYaw()
        {
            float yaw = _yaw;
            bool lockLookInput = TryGetActiveCameraOverride(out SkillTimelineRunner.CameraOverrideRequest cameraOverride)
                                 && cameraOverride.LockLookInput;
            if (_inputHandler != null && !IsLookInputBlocked(lockLookInput))
                yaw += _inputHandler.CurrentInput.LookDelta.x * _sensitivityX * ResolveLookSensitivityMultiplier(GetCurrentFieldOfView());
            return yaw;
        }

        public void BindTarget(Transform target, PlayerInputHandler inputHandler = null)
        {
            _target = target;
            _inputHandler = inputHandler;

            if (_inputHandler == null && _target != null)
                _inputHandler = _target.GetComponent<PlayerInputHandler>();

            _anglesInitialized = false;
            _targetAimBlend = 0f;
            _currentAimBlend = 0f;
            _targetAimZoomLevel = 0f;
            _currentAimZoomLevel = 0f;
            _loggedMissingTargetWarning = false;
            _loggedMissingInputWarning = false;

            if (_cameraComponent != null)
                _cameraComponent.fieldOfView = _defaultFieldOfView;
        }

        private void Awake()
        {
            Active = this;
            _cameraComponent = GetComponent<Camera>();
            _defaultFieldOfView = _cameraComponent != null ? _cameraComponent.fieldOfView : 60f;
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        private void LateUpdate()
        {
            EnsureReferences();
            InitializeAnglesFromCurrentTransform();

            if (_target == null || _inputHandler == null)
                return;

            PlayerController player = _target.GetComponent<PlayerController>();
            UpdateAimZoomState(player);
            float targetFieldOfView = ResolveTargetFieldOfView();
            float currentFieldOfView = ApplyAimFieldOfView(targetFieldOfView);

            bool hasCameraOverride = TryGetActiveCameraOverride(out SkillTimelineRunner.CameraOverrideRequest cameraOverride);
            bool lockLookInput = hasCameraOverride && cameraOverride.LockLookInput;
            if (!IsLookInputBlocked(lockLookInput))
            {
                var input = _inputHandler.CurrentInput;
                float lookSensitivityMultiplier = ResolveLookSensitivityMultiplier(currentFieldOfView);
                _yaw += input.LookDelta.x * _sensitivityX * lookSensitivityMultiplier;
                _pitch -= input.LookDelta.y * _sensitivityY * lookSensitivityMultiplier;
                _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            }

            float baseAimFieldOfView = Mathf.Lerp(_defaultFieldOfView, _aimFieldOfView, _currentAimBlend);
            float cameraDistance = Mathf.Lerp(_distance, _aimDistance, _currentAimBlend);
            cameraDistance *= ComputeZoomDistanceCompensation(baseAimFieldOfView, currentFieldOfView);
            float targetHeight = _targetHeight + _aimTargetHeightOffset * _currentAimBlend;
            Vector3 pivotPos = _target.position + Vector3.up * targetHeight;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredPos = pivotPos
                                 + rotation * (_aimShoulderOffset * _currentAimBlend)
                                 + rotation * (Vector3.back * cameraDistance);
            Vector3 resolvedBasePosition = ResolveCameraCollision(pivotPos, desiredPos);

            if (hasCameraOverride && cameraOverride.IsActive)
            {
                Vector3 overridePosition = ResolveCameraCollision(cameraOverride.LookAtPosition, cameraOverride.DesiredPosition);
                float blendWeight = Mathf.Clamp01(cameraOverride.BlendWeight);
                Vector3 finalPosition = Vector3.Lerp(resolvedBasePosition, overridePosition, blendWeight);
                Vector3 finalLookAt = Vector3.Lerp(pivotPos, cameraOverride.LookAtPosition, blendWeight);
                transform.position = finalPosition;
                transform.LookAt(finalLookAt);
                SyncAnglesFromCameraPose(finalPosition, finalLookAt);
                return;
            }

            transform.position = resolvedBasePosition;
            transform.LookAt(pivotPos);
        }

        private void UpdateAimZoomState(PlayerController player)
        {
            _targetAimBlend = ShouldUseAimZoom(player) ? 1f : 0f;
            if (_targetAimBlend <= 0f)
                _targetAimZoomLevel = 0f;
            else
                UpdateAimZoomInput();

            _currentAimBlend = Mathf.MoveTowards(_currentAimBlend, _targetAimBlend, _aimTransitionSpeed * Time.deltaTime);
            _currentAimZoomLevel = Mathf.MoveTowards(_currentAimZoomLevel, _targetAimZoomLevel, _aimTransitionSpeed * Time.deltaTime);
            PlayerAimCrosshairRuntime.SetZoomScale(Mathf.Lerp(1f, _aimCrosshairMaxScale, _currentAimZoomLevel * _currentAimBlend));
        }

        private bool ShouldUseAimZoom(PlayerController player)
        {
            return _enableAimZoom
                   && player != null
                   && player.IsAimModeActive
                   && GameStateMachine.GetInstance()?.IsGameplayPaused != true;
        }

        private void UpdateAimZoomInput()
        {
            if (IsLookInputBlocked(false))
                return;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null)
                return;

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) <= 0.01f)
                return;

            _targetAimZoomLevel = Mathf.Clamp01(_targetAimZoomLevel + Mathf.Sign(scroll) * _aimZoomStep);
#endif
        }

        private float ResolveTargetFieldOfView()
        {
            float zoomStrength = Mathf.LerpUnclamped(0f, _aimZoomBoost, _currentAimZoomLevel);
            float aimFov = Mathf.LerpUnclamped(_aimFieldOfView, _aimZoomFieldOfView, zoomStrength);
            aimFov = Mathf.Max(8f, aimFov);
            return Mathf.Lerp(_defaultFieldOfView, aimFov, _currentAimBlend);
        }

        private float ResolveLookSensitivityMultiplier(float currentFieldOfView)
        {
            float baseTan = Mathf.Tan(_defaultFieldOfView * 0.5f * Mathf.Deg2Rad);
            float currentTan = Mathf.Tan(currentFieldOfView * 0.5f * Mathf.Deg2Rad);
            if (baseTan <= 0.0001f || currentTan <= 0.0001f)
                return 1f;

            return currentTan / baseTan;
        }

        private static float ComputeZoomDistanceCompensation(float baseFieldOfView, float targetFieldOfView)
        {
            if (targetFieldOfView >= baseFieldOfView - 0.01f)
                return 1f;

            float baseTan = Mathf.Tan(baseFieldOfView * 0.5f * Mathf.Deg2Rad);
            float targetTan = Mathf.Tan(targetFieldOfView * 0.5f * Mathf.Deg2Rad);
            if (targetTan <= 0.0001f)
                return 1f;

            return Mathf.Max(1f, baseTan / targetTan);
        }

        private float GetCurrentFieldOfView()
        {
            return _cameraComponent != null ? _cameraComponent.fieldOfView : _defaultFieldOfView;
        }

        private float ApplyAimFieldOfView(float targetFieldOfView)
        {
            if (_cameraComponent == null)
                return targetFieldOfView;

            _cameraComponent.fieldOfView = Mathf.Lerp(_cameraComponent.fieldOfView, targetFieldOfView, _aimTransitionSpeed * Time.deltaTime);
            return _cameraComponent.fieldOfView;
        }

        private bool TryGetActiveCameraOverride(out SkillTimelineRunner.CameraOverrideRequest request)
        {
            request = default;
            if (_target == null)
                return false;

            PlayerController player = _target.GetComponent<PlayerController>();
            PlayerStateBase currentState = player?.StateMachine?.CurrentState;
            if (currentState != null && currentState.TryGetCurrentCameraOverride(_targetHeight, out request))
                return request.IsActive;

            return player != null && player.TryGetDetachedCameraOverride(_targetHeight, out request);
        }

        private Vector3 ResolveCameraCollision(Vector3 lookAtPosition, Vector3 desiredPosition)
        {
            Vector3 offset = desiredPosition - lookAtPosition;
            float desiredDistance = offset.magnitude;
            if (desiredDistance <= 0.0001f)
                return desiredPosition;

            Vector3 dir = offset / desiredDistance;
            float resolvedDistance = desiredDistance;
            if (Physics.SphereCast(lookAtPosition, _collisionRadius, dir, out var hit, desiredDistance, _collisionMask))
                resolvedDistance = Mathf.Clamp(hit.distance - _collisionRadius, 0.05f, desiredDistance);

            return lookAtPosition + dir * resolvedDistance;
        }

        private void SyncAnglesFromCameraPose(Vector3 cameraPosition, Vector3 lookAtPosition)
        {
            Vector3 toTarget = lookAtPosition - cameraPosition;
            Vector3 fromTarget = cameraPosition - lookAtPosition;
            float planarDistance = new Vector2(toTarget.x, toTarget.z).magnitude;
            if (planarDistance <= 0.0001f && Mathf.Abs(fromTarget.y) <= 0.0001f)
                return;

            _yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Atan2(fromTarget.y, Mathf.Max(0.0001f, new Vector2(fromTarget.x, fromTarget.z).magnitude)) * Mathf.Rad2Deg;
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            _anglesInitialized = true;
        }

        private static bool IsLookInputBlocked(bool cameraLookLocked)
        {
            var gsm = GameStateMachine.GetInstance();
            return gsm?.IsGameplayPaused == true
                   || GameplayUIInputBridge.IsAnyGameplayPanelOpen()
                   || SkillEffectExecutor.IsCameraLookBlockedByHitStop
                   || cameraLookLocked;
        }

        private void EnsureReferences()
        {
            if (_inputHandler == null)
            {
                var playerTransform = GameStateMachine.GetInstance()?.LevelPlayerTransform;
                if (playerTransform != null)
                    _inputHandler = playerTransform.GetComponent<PlayerInputHandler>();
                if (_inputHandler == null)
                    _inputHandler = FindFirstObjectByType<PlayerInputHandler>();

                if (_inputHandler == null)
                {
                    if (!_loggedMissingInputWarning)
                    {
                        Debug.LogWarning("[ThirdPersonCamera] PlayerInputHandler not found. Mouse look is disabled.");
                        _loggedMissingInputWarning = true;
                    }
                }
                else
                {
                    _loggedMissingInputWarning = false;
                }
            }

            if (_target == null)
            {
                Transform playerRoot = null;
                if (_inputHandler != null)
                    playerRoot = _inputHandler.transform;

                if (playerRoot == null)
                    playerRoot = GameStateMachine.GetInstance()?.LevelPlayerTransform;

                if (playerRoot == null)
                {
                    var player = FindFirstObjectByType<PlayerController>();
                    if (player != null)
                        playerRoot = player.transform;
                }

                if (playerRoot != null)
                    _target = playerRoot;

                if (_target == null)
                {
                    if (!_loggedMissingTargetWarning)
                    {
                        Debug.LogWarning("[ThirdPersonCamera] Player root not found. Camera follow is disabled.");
                        _loggedMissingTargetWarning = true;
                    }
                }
                else
                {
                    _loggedMissingTargetWarning = false;
                }
            }
        }

        private void InitializeAnglesFromCurrentTransform()
        {
            if (_anglesInitialized)
                return;

            if (_target == null)
                return;

            var euler = transform.eulerAngles;
            _yaw = _target.eulerAngles.y;
            _pitch = NormalizePitch(euler.x);
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            _anglesInitialized = true;
        }

        private static float NormalizePitch(float pitch)
        {
            if (pitch > 180f)
                pitch -= 360f;
            return pitch;
        }
    }
}
