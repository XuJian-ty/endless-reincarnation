using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 下落攻击开始状态。
    /// 触发：空中按 RMB（AltAttack）→ FallAttack Interrupt 进入。
    /// 自然退出：接地 → FallAttackLandState；开始动画结束时 → FallAttackLoopState。
    /// 本状态内不切换到 FallState，所有出口均在状态内显式处理。
    /// </summary>
    public class FallAttackStartState : PlayerStateBase
    {
        public override GameAction CurrentActionId => GameAction.FallAttack;

        protected override void OnEnter()
        {
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            Ctx.Anim.TriggerFallAttackStart();
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            // 首帧保护：刚进入时 CharacterController.isGrounded 可能是上一帧残留值
            if (StateAge <= 0.1f) return;
            if (IsGrounded)
            {
                GoTo<FallAttackLandState>();
                return;
            }
            if (AnimNearConfiguredEnd())
                GoToConfiguredNaturalExit(Game.Data.PlayerStateNaturalExitTarget.FallAttackLoopState);
            else if (StateAge > 0.25f && Ctx.Anim.IsCurrentStateLooping())
                GoTo<FallAttackLoopState>();
        }

        /// <summary>仅由本状态 OnTick（接地→FallAttackLand，动画结束→FallAttackLoop）决定退出，不允许外部 Interrupt 打断。</summary>
        public override TransitionPolicy GetPolicyFor(GameAction action) => TransitionPolicy.Ignore;
    }
}
