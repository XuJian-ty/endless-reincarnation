using UnityEngine;
using System.Collections.Generic;

namespace Game.Presentation
{
    /// <summary>
    /// 封装 Animator API，统一参数设置、Trigger 触发与动画进度查询，
    /// 避免状态直接依赖 Animator 字符串魔法值。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorController : MonoBehaviour
    {
        // ── Animator Parameter Hashes（在静态字段预计算，零 GC）──────────
        private static readonly int SpeedHash        = Animator.StringToHash("Speed");
        private static readonly int MoveXHash        = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash        = Animator.StringToHash("MoveY");
        private static readonly int DodgeDirectionHash = Animator.StringToHash("DodgeDirection");
        private static readonly int UpperBodyPlaybackSpeedHash = Animator.StringToHash("UpperBodyPlaybackSpeed");
        private static readonly int IsGroundedHash   = Animator.StringToHash("IsGrounded");
        private static readonly int JumpTrigger      = Animator.StringToHash("Jump");
        private static readonly int DodgeTrigger     = Animator.StringToHash("Dodge");
        private static readonly int ChargeStartHash  = Animator.StringToHash("ChargeStart");
        private static readonly int ChargeLoopHash   = Animator.StringToHash("ChargeLoop");
        private static readonly int ChargeRelHash    = Animator.StringToHash("ChargeRelease");
        private static readonly int AirAttackHash    = Animator.StringToHash("AirAttack");
        private static readonly int FallAttackHash   = Animator.StringToHash("FallAttackStart");
        private static readonly int FallAttLoopHash  = Animator.StringToHash("FallAttackLoop");
        private static readonly int FallAttLandHash  = Animator.StringToHash("FallAttackLand");
        private static readonly int NormalLocomotionHash = Animator.StringToHash("NormalLocomotion");
        private static readonly int AimLocomotionHash    = Animator.StringToHash("AimLocomotion");
        private static readonly int LandTrigger      = Animator.StringToHash("Land");
        private static readonly int HitStunTrigger   = Animator.StringToHash("HitStun");
        private static readonly int FallTrigger      = Animator.StringToHash("Fall");
        private static readonly int DeadTrigger      = Animator.StringToHash("Dead");
        private static readonly string[] AttackTriggerNames = { "Attack0", "Attack1", "Attack2", "Attack3" };

        private Animator _animator;
        private int _aimLayerIndex = -1;
        private readonly HashSet<string> _parameterNames = new HashSet<string>();
        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _aimLayerIndex = _animator != null ? _animator.GetLayerIndex("Aim Layer") : -1;
        }

        // ── 参数设置与查询 ───────────────────────────────────────────────
        public void SetLocomotionSpeed(float speed)
        {
            if (_animator == null)
                return;

            _animator.SetFloat(SpeedHash, speed);
        }

        public void SetLocomotionBlend(Vector2 blend)
        {
            if (_animator == null)
                return;

            if (HasParameter("MoveX"))
                _animator.SetFloat(MoveXHash, blend.x);
            if (HasParameter("MoveY"))
                _animator.SetFloat(MoveYHash, blend.y);
        }

        public void SetDodgeDirection(int dodgeDirection)
        {
            if (_animator == null || !HasParameter("DodgeDirection"))
                return;

            _animator.SetInteger(DodgeDirectionHash, Mathf.Clamp(dodgeDirection, 0, 3));
        }

        public float GetLocomotionSpeed()            => _animator.GetFloat(SpeedHash);
        public void SetGrounded(bool grounded)       => _animator.SetBool(IsGroundedHash, grounded);
        public void SetPlaybackSpeed(float speed)
        {
            if (_animator != null)
                _animator.speed = Mathf.Max(0.1f, speed);
        }

        public void SetUpperBodyPlaybackSpeed(float speed)
        {
            if (_animator == null || !HasParameter("UpperBodyPlaybackSpeed"))
                return;

            _animator.SetFloat(UpperBodyPlaybackSpeedHash, Mathf.Max(0.1f, speed));
        }

        public void SetAimLayerActive(bool active)
        {
            if (_animator == null || _aimLayerIndex < 0)
                return;

            _animator.SetLayerWeight(_aimLayerIndex, active ? 1f : 0f);
        }

        // ── 触发方法 ──────────────────────────────────────────────────────
        public void TriggerJump()   => SetExclusiveTrigger(JumpTrigger);
        public void TriggerDodge()  => SetExclusiveTrigger(DodgeTrigger);
        public void TriggerFall()   => SetExclusiveTrigger(FallTrigger);
        public void TriggerLand()   => SetExclusiveTrigger(LandTrigger);
        public void TriggerHitStun()=> SetExclusiveTrigger(HitStunTrigger);
        public void TriggerDead()   => SetExclusiveTrigger(DeadTrigger);

        public void TriggerAttack(int comboIndex)
        {
            if (comboIndex < 0 || comboIndex >= AttackTriggerNames.Length)
                return;

            TriggerAction(AttackTriggerNames[comboIndex]);
        }

        public void TriggerLocomotion()      => TriggerNormalLocomotion();
        public void TriggerNormalLocomotion()
        {
            if (_animator == null)
                return;

            ResetLocomotionTriggers();
            SetNamedTriggerIfExists("NormalLocomotion", NormalLocomotionHash);
        }

        public void TriggerAimLocomotion()
        {
            if (_animator == null)
                return;

            ResetLocomotionTriggers();
            SetNamedTriggerIfExists("AimLocomotion", AimLocomotionHash);
        }

        public void TriggerChargeStart()     => SetExclusiveTrigger(ChargeStartHash);
        public void TriggerChargeLoop()      => SetExclusiveTrigger(ChargeLoopHash);
        public void TriggerChargeRelease()   => SetExclusiveTrigger(ChargeRelHash);
        public void TriggerAirAttack()       => SetExclusiveTrigger(AirAttackHash);
        public void TriggerFallAttackStart() => SetExclusiveTrigger(FallAttackHash);
        public void TriggerFallAttackLoop()  => SetExclusiveTrigger(FallAttLoopHash);
        public void TriggerFallAttackLand()  => SetExclusiveTrigger(FallAttLandHash);

        public void TriggerSkill(int skillIndex)
        {
            if (skillIndex < 0)
                return;

            TriggerAction(PlayerActionRouting.BuildSkillSlotActionName(skillIndex));
        }

        public bool TriggerAction(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName) || _animator == null)
                return false;

            CacheParametersIfNeeded();
            string normalized = triggerName.Trim();
            if (_parameterNames.Contains(normalized))
            {
                if (ShouldClearLocomotionTrigger(normalized))
                    ResetLocomotionTriggers();
                _animator.ResetTrigger(normalized);
                _animator.SetTrigger(normalized);
                return true;
            }

            return false;
        }

        // ── 动画进度查询 ──────────────────────────────────────────────────
        public float GetCurrentNormalizedTime(int layer = 0)
            => _animator.GetCurrentAnimatorStateInfo(layer).normalizedTime;

        /// <summary>
        /// 当前动画是否接近结束（单一职责：仅根据当前状态进度判断）。
        /// 过渡期间返回 false；循环动画返回 false（不在此 API 内对“循环”做特殊语义）；一次性动画为 normalizedTime >= threshold 时返回 true。
        /// </summary>
        public bool IsCurrentStateNearEnd(float threshold = 0.9f, int layer = 0)
        {
            if (_animator.IsInTransition(layer)) return false;
            var info = _animator.GetCurrentAnimatorStateInfo(layer);
            if (info.loop) return false;
            return info.normalizedTime >= threshold;
        }

        /// <summary>
        /// 当前 Animator 状态是否为循环动画。用于“一次性动画播完后已切到循环态则视为结束”等仅在调用方内部需要的逻辑，不参与通用 NearEnd 语义。
        /// </summary>
        public bool IsCurrentStateLooping(int layer = 0)
        {
            if (_animator.IsInTransition(layer)) return false;
            return _animator.GetCurrentAnimatorStateInfo(layer).loop;
        }

        private void CacheParametersIfNeeded()
        {
            if (_parameterNames.Count > 0 || _animator == null)
                return;

            foreach (var parameter in _animator.parameters)
                _parameterNames.Add(parameter.name);
        }

        private bool HasParameter(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                return false;

            CacheParametersIfNeeded();
            return _parameterNames.Contains(parameterName.Trim());
        }

        private void SetExclusiveTrigger(int triggerHash, bool clearLocomotion = true)
        {
            if (_animator == null)
                return;

            if (clearLocomotion)
                ResetLocomotionTriggers();

            _animator.ResetTrigger(triggerHash);
            _animator.SetTrigger(triggerHash);
        }

        private void ResetLocomotionTriggers()
        {
            if (_animator == null)
                return;

            if (HasParameter("NormalLocomotion"))
                _animator.ResetTrigger(NormalLocomotionHash);
            if (HasParameter("AimLocomotion"))
                _animator.ResetTrigger(AimLocomotionHash);
        }

        private bool SetNamedTriggerIfExists(string parameterName, int parameterHash)
        {
            if (!HasParameter(parameterName))
                return false;

            _animator.ResetTrigger(parameterHash);
            _animator.SetTrigger(parameterHash);
            return true;
        }

        private static bool ShouldClearLocomotionTrigger(string triggerName)
        {
            return !string.Equals(triggerName, "NormalLocomotion")
                   && !string.Equals(triggerName, "AimLocomotion")
                   && !string.Equals(triggerName, "Aim")
                   && !string.Equals(triggerName, "Shoot")
                   && !string.Equals(triggerName, "ShootCharge")
                   && !string.Equals(triggerName, "Shoot_Charge");
        }
    }
}
