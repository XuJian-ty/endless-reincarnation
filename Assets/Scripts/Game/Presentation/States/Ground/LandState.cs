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
            Ctx.Anim.SetGrounded(true);
            Ctx.Anim.TriggerLand();
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (AnimNearEnd())
                CompleteWithPending(() => { if (IsGrounded) GoTo<IdleState>(); else GoTo<FallState>(); });
        }
    }
}
