using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 近战蓄力循环状态：蓄力开始动画播完后进入，持续循环直到内部切到蓄力释放。
    /// 代码驱动进入（由 ChargeStartState.OnTick 检测动画接近结束后切过来）。
    /// </summary>
    public class ChargeLoopState : PlayerStateBase
    {
        protected override string ActionId => ResolveConfiguredFormActionId(PlayerFormActionSlot.ChargeLoop, "ChargeLoop");
        public override GameAction CurrentActionId => GameAction.ChargeStart;

        protected override void OnEnter()
        {
            TriggerConfiguredBaseAction(ActionId, "ChargeLoop");
            StartConfiguredBaseActionTimeline(ActionId);
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (!input.IsLmbHeld)
            {
                if (!IsGrounded)
                {
                    GoTo<FallState>();
                    return;
                }

                GoTo<ChargeReleaseState>();
            }
        }

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, TransitionPolicy.Ignore);
    }
}
