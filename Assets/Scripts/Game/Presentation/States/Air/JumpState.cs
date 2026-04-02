namespace Game.Presentation
{
    /// <summary>
    /// 跳跃上升状态（单段跳）。
    /// 自然退出：上升阶段结束（垂直速度 ≤ 0）时切换到 FallState。
    /// </summary>
    public class JumpState : PlayerStateBase
    {
        protected override void OnEnter()
        {
            Ctx.Mover.Jump(Ctx.JumpHeight);
            TriggerConfiguredBaseAction("Jump", "Jump");
            StartConfiguredBaseActionTimeline("Jump");
            Ctx.Anim.SetGrounded(false);
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            var dir = Ctx.GetMoveDirection(input.MoveInput);
            if (dir.sqrMagnitude > 0.001f)
                Ctx.Mover.SetHorizontalVelocity(dir * (Ctx.WalkSpeed * 0.5f));

            // 首帧保护：Enter 里刚设完起跳速度，避免同帧误判 VerticalVelocity 或 IsGrounded 导致先进 Fall 再进 Jump
            if (StateAge <= 0.05f) return;

            // 上升结束（速度到顶后下落）→ Fall
            if (Ctx.Mover.VerticalVelocity <= 0f)
            {
                GoTo<FallState>();
                return;
            }
            // 仅当已运行足够长时间且接地时才进 Land（同帧内 Mover.Tick 未执行，IsGrounded 仍是上一帧值，故需 StateAge > 0.1f）
            if (StateAge > 0.1f && IsGrounded)
            {
                GoTo<LandState>();
                return;
            }
        }

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, TransitionPolicy.Ignore);
    }
}
