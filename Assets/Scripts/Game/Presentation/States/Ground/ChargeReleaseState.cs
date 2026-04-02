using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 蓄力释放状态（LMB SlowTap 达到 0.7s 后松开触发）。
    /// 播放蓄力攻击释放动画，动画结束后消费预输入或回 Idle。
    /// </summary>
    public class ChargeReleaseState : PlayerStateBase
    {
        protected override string ActionId => ResolveConfiguredFormActionId(PlayerFormActionSlot.ChargeRelease, "ChargeRelease");
        public override GameAction CurrentActionId => GameAction.ChargeRelease;

        protected override void OnEnter()
        {
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            TriggerConfiguredBaseAction(ActionId, "ChargeRelease");
            StartConfiguredBaseActionTimeline(ActionId);
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (AnimNearConfiguredEnd())
            {
                CompleteWithPending(() =>
                {
                    if (IsGrounded)
                        GoToConfiguredNaturalExit(Game.Data.PlayerStateNaturalExitTarget.IdleState);
                    else
                        GoTo<FallState>();
                });
            }
        }

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, TransitionPolicy.Ignore);
    }
}
