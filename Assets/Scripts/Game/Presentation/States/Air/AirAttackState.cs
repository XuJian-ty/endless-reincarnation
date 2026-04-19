using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 空中普攻状态（单段，与地面普攻独立）。
    /// 动画结束后：已接地 → IdleState；距地 &lt; 0.3m → LandState；仍在空中 → FallState。
    /// </summary>
    public class AirAttackState : PlayerStateBase
    {
        protected override string ActionId => ResolveConfiguredFormActionId(PlayerFormActionSlot.AirAttack, "AirAttack");
        public override GameAction CurrentActionId => GameAction.AirAttack;

        protected override void OnEnter()
        {
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            TriggerConfiguredBaseAction(ActionId, "AirAttack");
            StartConfiguredBaseActionTimeline(ActionId);
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (StateAge <= 0.1f) return;

            PendingActionData pending = Ctx.StateMachine.PeekPending();
            if (!pending.IsEmpty
                && TryGetConfiguredPendingReleaseThreshold(pending.Action, out float pendingThreshold)
                && IsAirAttackAnimationNearEnd(pendingThreshold))
            {
                CompleteConfiguredExit();
                return;
            }

            // 本状态特例：空中普攻为一次性动画，播完后 Animator 会切到 Fall（循环）；仅在 AirAttackState 内显式处理“已切到循环则视为结束”，不通过通用 API 影响其它状态
            bool oneShotNearEnd = AnimNearConfiguredEnd();
            bool alreadyMovedToLoop = Ctx.Anim.IsCurrentStateLooping();
            if (!oneShotNearEnd && !alreadyMovedToLoop) return;

            CompleteConfiguredExit();
        }

        private void CompleteConfiguredExit()
        {
            if (IsGrounded || Ctx.Mover.IsNearGround(0.3f))
                CompleteWithPending(() => GoTo<LandState>());
            else
                CompleteWithPending(() => GoTo<FallState>());
        }

        private bool IsAirAttackAnimationNearEnd(float threshold)
        {
            return Ctx.Anim.IsCurrentStateNearEnd(threshold);
        }

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, TransitionPolicy.Ignore);
    }
}
