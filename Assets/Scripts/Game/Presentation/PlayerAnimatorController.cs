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
        private static readonly int LocomotionHash   = Animator.StringToHash("Locomotion");
        private static readonly int LandTrigger      = Animator.StringToHash("Land");
        private static readonly int HitStunTrigger   = Animator.StringToHash("HitStun");
        private static readonly int FallTrigger      = Animator.StringToHash("Fall");
        private static readonly int DeadTrigger      = Animator.StringToHash("Dead");
        private static readonly string[] AttackTriggerNames = { "Attack0", "Attack1", "Attack2", "Attack3" };
        private static readonly string[] SkillTriggerNames  = { "Skill0", "Skill1", "Skill2", "Skill3" };

        private Animator _animator;
        private readonly HashSet<string> _parameterNames = new HashSet<string>();
        private void Awake() => _animator = GetComponent<Animator>();

        // ── 参数设置与查询 ───────────────────────────────────────────────
        public void SetLocomotionSpeed(float speed)  => _animator.SetFloat(SpeedHash, speed);
        public float GetLocomotionSpeed()            => _animator.GetFloat(SpeedHash);
        public void SetGrounded(bool grounded)       => _animator.SetBool(IsGroundedHash, grounded);
        public void SetPlaybackSpeed(float speed)
        {
            if (_animator != null)
                _animator.speed = Mathf.Max(0.1f, speed);
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

        public void TriggerLocomotion()      => SetExclusiveTrigger(LocomotionHash, clearLocomotion: false);
        public void TriggerChargeStart()     => SetExclusiveTrigger(ChargeStartHash);
        public void TriggerChargeLoop()      => SetExclusiveTrigger(ChargeLoopHash);
        public void TriggerChargeRelease()   => SetExclusiveTrigger(ChargeRelHash);
        public void TriggerAirAttack()       => SetExclusiveTrigger(AirAttackHash);
        public void TriggerFallAttackStart() => SetExclusiveTrigger(FallAttackHash);
        public void TriggerFallAttackLoop()  => SetExclusiveTrigger(FallAttLoopHash);
        public void TriggerFallAttackLand()  => SetExclusiveTrigger(FallAttLandHash);

        public void TriggerSkill(int skillIndex)
        {
            if (skillIndex < 0 || skillIndex >= SkillTriggerNames.Length)
                return;

            TriggerAction(SkillTriggerNames[skillIndex]);
        }

        public bool TriggerAction(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName) || _animator == null)
                return false;

            CacheParametersIfNeeded();
            string normalized = triggerName.Trim();
            if (_parameterNames.Contains(normalized))
            {
                if (!string.Equals(normalized, "Locomotion"))
                    _animator.ResetTrigger(LocomotionHash);
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

        private void SetExclusiveTrigger(int triggerHash, bool clearLocomotion = true)
        {
            if (_animator == null)
                return;

            if (clearLocomotion)
                _animator.ResetTrigger(LocomotionHash);

            _animator.ResetTrigger(triggerHash);
            _animator.SetTrigger(triggerHash);
        }
    }
}
