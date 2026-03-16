using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 空中普攻状态（单段，与地面普攻独立）。
    /// 动画结束后：已接地 → IdleState；距地 &lt; 0.3m → LandState；仍在空中 → FallState。
    /// </summary>
    public class AirAttackState : PlayerStateBase
    {
        public override GameAction CurrentActionId => GameAction.AirAttack;

        protected override void OnEnter()
        {
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            TriggerConfiguredActionByActionId("AirAttack", "AirAttack");
            StartConfiguredTimelineByActionId("AirAttack");
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (StateAge <= 0.1f) return;
            // 本状态特例：空中普攻为一次性动画，播完后 Animator 会切到 Fall（循环）；仅在 AirAttackState 内显式处理“已切到循环则视为结束”，不通过通用 API 影响其它状态
            bool oneShotNearEnd = AnimNearConfiguredEnd();
            bool alreadyMovedToLoop = Ctx.Anim.IsCurrentStateLooping();
            if (!oneShotNearEnd && !alreadyMovedToLoop) return;

            if (IsGrounded || Ctx.Mover.IsNearGround(0.3f))
                CompleteWithPending(() => GoTo<LandState>());
            else
                CompleteWithPending(() => GoTo<FallState>());
        }

        public override TransitionPolicy GetPolicyFor(GameAction action) => action switch
        {
            GameAction.Dodge         => TransitionPolicy.Interrupt,
            GameAction.Skill         => TransitionPolicy.Interrupt,
            GameAction.FallAttack    => TransitionPolicy.Interrupt,
            GameAction.AirAttack     => TransitionPolicy.Ignore,
            GameAction.ChargeStart   => TransitionPolicy.Ignore,
            GameAction.ChargeRelease => TransitionPolicy.Ignore,
            GameAction.NormalAttack  => TransitionPolicy.Ignore,
            GameAction.Jump          => TransitionPolicy.Ignore,
            _                        => TransitionPolicy.Buffer,
        };
    }
}
