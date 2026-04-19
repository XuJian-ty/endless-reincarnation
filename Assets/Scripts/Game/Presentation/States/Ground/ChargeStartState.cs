using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 蓄力开始状态（LMB Hold 达到 0.2s 触发）。
    /// 自然退出条件：动画结束时退出，退出后进入蓄力循环。
    /// 闪避/技能/蓄力释放可打断。
    /// </summary>
    public class ChargeStartState : PlayerStateBase
    {
        protected override string ActionId => ResolveConfiguredFormActionId(PlayerFormActionSlot.ChargeStart, "ChargeStart");
        public override GameAction CurrentActionId => GameAction.ChargeStart;

        protected override void OnEnter()
        {
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            TriggerConfiguredBaseAction(ActionId, "ChargeStart");
            StartConfiguredBaseActionTimeline(ActionId);
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (AnimNearConfiguredEnd())
            {
                if (input.IsLmbHeld)
                    GoToConfiguredNaturalExit(Game.Data.PlayerStateNaturalExitTarget.ChargeLoopState);
                else if (IsGrounded || Ctx.Mover.IsNearGround(0.3f))
                    GoTo<LandState>();
                else
                    GoTo<FallState>();
            }
        }

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, TransitionPolicy.Ignore);
    }
}
