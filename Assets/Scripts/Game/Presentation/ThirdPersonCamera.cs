using UnityEngine;
using Game.GameFlow;
using Game.UI;

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

        [Header("Collision")]
        [SerializeField] private LayerMask _collisionMask;
        [SerializeField] private float _collisionRadius = 0.2f;

        private Transform _target;
        private PlayerInputHandler _inputHandler;
        private float _yaw;
        private float _pitch;
        private bool _anglesInitialized;
        private bool _loggedMissingTargetWarning;
        private bool _loggedMissingInputWarning;

        public static ThirdPersonCamera Active { get; private set; }
        public float Yaw => _yaw;

        public float GetMovementYaw()
        {
            float yaw = _yaw;
            if (_inputHandler != null && !IsLookInputBlocked())
                yaw += _inputHandler.CurrentInput.LookDelta.x * _sensitivityX;
            return yaw;
        }

        public void BindTarget(Transform target, PlayerInputHandler inputHandler = null)
        {
            _target = target;
            _inputHandler = inputHandler;

            if (_inputHandler == null && _target != null)
                _inputHandler = _target.GetComponent<PlayerInputHandler>();

            _anglesInitialized = false;
            _loggedMissingTargetWarning = false;
            _loggedMissingInputWarning = false;
        }

        private void Awake()
        {
            Active = this;
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

            if (!IsLookInputBlocked())
            {
                var input = _inputHandler.CurrentInput;
                _yaw += input.LookDelta.x * _sensitivityX;
                _pitch -= input.LookDelta.y * _sensitivityY;
                _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            }

            Vector3 pivotPos = _target.position + Vector3.up * _targetHeight;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredPos = pivotPos + rotation * (Vector3.back * _distance);

            Vector3 dir = (desiredPos - pivotPos).normalized;
            float dist = _distance;
            if (Physics.SphereCast(pivotPos, _collisionRadius, dir, out var hit, _distance, _collisionMask))
                dist = Mathf.Max(hit.distance - _collisionRadius, 0.5f);

            transform.position = pivotPos + dir * dist;
            transform.LookAt(pivotPos);
        }

        private static bool IsLookInputBlocked()
        {
            var gsm = GameStateMachine.GetInstance();
            return gsm?.IsGameplayPaused == true
                   || GameplayUIInputBridge.IsAnyGameplayPanelOpen()
                   || SkillEffectExecutor.IsCameraLookBlockedByHitStop;
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
