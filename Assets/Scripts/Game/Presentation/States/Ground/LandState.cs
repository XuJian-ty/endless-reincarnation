using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 着陆状态。跳跃或下落后接触地面时进入，播放落地缓冲动画。
    /// 自然结束后消费预输入，若无则回到 Idle。
    /// </summary>
    public class LandState : PlayerStateBase
    {
        protected override void OnEnter()
        {
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            Ctx.Anim.SetGrounded(true);
            TriggerConfiguredBaseAction("Land", "Land");
            StartConfiguredBaseActionTimeline("Land");
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
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
    }
}
