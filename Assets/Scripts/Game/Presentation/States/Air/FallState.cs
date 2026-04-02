using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 下落状态（循环播放下落动画）。
    /// 自然退出：距地约 0.3m 时 → LandState（普通着陆，与需求一致）。
    /// </summary>
    public class FallState : PlayerStateBase
    {
        protected override void OnEnter()
        {
            TriggerConfiguredBaseAction("Fall", "Fall");
            StartConfiguredBaseActionTimeline("Fall");
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            var dir = Ctx.GetMoveDirection(input.MoveInput);
            if (dir.sqrMagnitude > 0.001f)
                Ctx.Mover.SetHorizontalVelocity(dir * (Ctx.WalkSpeed * 0.5f));

            if (Ctx.Mover.IsNearGround(0.3f))
                GoTo<LandState>();
        }

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, TransitionPolicy.Ignore);
    }
}
