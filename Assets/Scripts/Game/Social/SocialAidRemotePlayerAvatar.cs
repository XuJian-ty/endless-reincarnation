using UnityEngine;
using Game.Data;
using Game.Presentation;

namespace Game.Social
{
    public sealed class SocialAidRemotePlayerAvatar : MonoBehaviour
    {
        public enum RemoteAction : byte
        {
            Idle = 0,
            Move = 1,
            Aim = 2,
            Jump = 3,
            AirJump = 4,
            Fall = 5,
            Land = 6,
            Dodge = 7,
            Attack0 = 8,
            Attack1 = 9,
            Attack2 = 10,
            Attack3 = 11,
            AirAttack = 12,
            FallAttackStart = 13,
            FallAttackLoop = 14,
            FallAttackLand = 15,
            ChargeStart = 16,
            ChargeLoop = 17,
            ChargeRelease = 18,
            Shoot = 19,
            ShootCharge = 20,
            Skill = 21,
            HitStun = 22,
            Dead = 23,
        }

        private const float MoveLerpSpeed = 18f;
        private const float TurnLerpSpeed = 18f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Animator _animator;
        private RemoteAction _lastAction = (RemoteAction)byte.MaxValue;

        public float LastPoseTime { get; private set; }

        private void Awake()
        {
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
            _animator = GetComponentInChildren<Animator>(true);
            LastPoseTime = Time.unscaledTime;
        }

        public void ApplyPose(
            Vector3 position,
            float yaw,
            float moveSpeed,
            RemoteAction action,
            PlayerAttackMode attackMode,
            bool hasMeleeWeapon,
            bool hasRangedWeapon)
        {
            _targetPosition = position;
            _targetRotation = Quaternion.Euler(0f, yaw, 0f);
            LastPoseTime = Time.unscaledTime;
            ApplyWeaponState(attackMode, hasMeleeWeapon, hasRangedWeapon);
            ApplyAnimationState(moveSpeed, action);
        }

        private void LateUpdate()
        {
            float deltaTime = Time.unscaledDeltaTime;
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Mathf.Clamp01(MoveLerpSpeed * deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Mathf.Clamp01(TurnLerpSpeed * deltaTime));
        }

        private void ApplyWeaponState(PlayerAttackMode attackMode, bool hasMeleeWeapon, bool hasRangedWeapon)
        {
            bool showMeleeWeapon = attackMode == PlayerAttackMode.Melee && hasMeleeWeapon;
            bool showRangedWeapon = attackMode == PlayerAttackMode.Ranged && hasRangedWeapon;
            PlayerWeaponVisualUtility.ApplyWeaponVisibility(transform, attackMode, showMeleeWeapon, showRangedWeapon);
        }

        private void ApplyAnimationState(float moveSpeed, RemoteAction action)
        {
            if (_animator == null)
                return;

            float normalizedMoveSpeed = Mathf.Clamp01(moveSpeed);
            SetFloatIfExists("Speed", SpeedHash, normalizedMoveSpeed);
            SetFloatIfExists("MoveX", MoveXHash, 0f);
            SetFloatIfExists("MoveY", MoveYHash, normalizedMoveSpeed);
            SetBoolIfExists("IsGrounded", IsGroundedHash, IsGroundedAction(action));

            if (action == _lastAction)
                return;

            _lastAction = action;
            TriggerAction(action);
        }

        private void TriggerAction(RemoteAction action)
        {
            string triggerName = GetTriggerName(action);
            if (string.IsNullOrWhiteSpace(triggerName))
                return;

            SetTriggerIfExists(triggerName);
        }

        private static string GetTriggerName(RemoteAction action)
        {
            return action switch
            {
                RemoteAction.Idle => "NormalLocomotion",
                RemoteAction.Move => "NormalLocomotion",
                RemoteAction.Aim => "AimLocomotion",
                RemoteAction.Jump => "Jump",
                RemoteAction.AirJump => "AirJump",
                RemoteAction.Fall => "Fall",
                RemoteAction.Land => "Land",
                RemoteAction.Dodge => "Dodge",
                RemoteAction.Attack0 => "Attack0",
                RemoteAction.Attack1 => "Attack1",
                RemoteAction.Attack2 => "Attack2",
                RemoteAction.Attack3 => "Attack3",
                RemoteAction.AirAttack => "AirAttack",
                RemoteAction.FallAttackStart => "FallAttackStart",
                RemoteAction.FallAttackLoop => "FallAttackLoop",
                RemoteAction.FallAttackLand => "FallAttackLand",
                RemoteAction.ChargeStart => "ChargeStart",
                RemoteAction.ChargeLoop => "ChargeLoop",
                RemoteAction.ChargeRelease => "ChargeRelease",
                RemoteAction.Shoot => "Shoot",
                RemoteAction.ShootCharge => "Shoot_Charge",
                RemoteAction.Skill => "Skill0",
                RemoteAction.HitStun => "HitStun",
                RemoteAction.Dead => "Dead",
                _ => string.Empty,
            };
        }

        private static bool IsGroundedAction(RemoteAction action)
        {
            return action != RemoteAction.Jump &&
                   action != RemoteAction.AirJump &&
                   action != RemoteAction.Fall &&
                   action != RemoteAction.AirAttack &&
                   action != RemoteAction.FallAttackStart &&
                   action != RemoteAction.FallAttackLoop;
        }

        private void SetTriggerIfExists(string triggerName)
        {
            if (!HasParameter(triggerName, AnimatorControllerParameterType.Trigger))
                return;

            _animator.ResetTrigger(triggerName);
            _animator.SetTrigger(triggerName);
        }

        private void SetFloatIfExists(string parameterName, int parameterHash, float value)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Float))
                _animator.SetFloat(parameterHash, value);
        }

        private void SetBoolIfExists(string parameterName, int parameterHash, bool value)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Bool))
                _animator.SetBool(parameterHash, value);
        }

        private bool HasParameter(string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(parameterName))
                return false;

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == parameterType && parameter.name == parameterName)
                    return true;
            }

            return false;
        }
    }
}
