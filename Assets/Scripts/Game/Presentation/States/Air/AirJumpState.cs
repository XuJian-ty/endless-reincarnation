using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 空中跳跃上升状态（二段跳）。
    /// 自然退出：上升阶段结束（垂直速度 ≤ 0）时切换到 FallState。
    /// </summary>
    public class AirJumpState : PlayerStateBase
    {
        public Vector3 InitialHorizontalVelocity { get; set; }

        public override GameAction CurrentActionId => GameAction.Jump;

        protected override void OnEnter()
        {
            Ctx.Mover.SetHorizontalVelocity(InitialHorizontalVelocity);
            InitialHorizontalVelocity = Vector3.zero;
            Ctx.Mover.Jump(Ctx.JumpHeight);
            TriggerConfiguredBaseAction("AirJump", "AirJump");
            StartConfiguredBaseActionTimeline("AirJump");
            Ctx.Anim.SetGrounded(false);
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            var dir = Ctx.StateMachine.GetAirMoveDirection(input.MoveInput);
            if (dir.sqrMagnitude > 0.001f)
            {
                Vector3 horizontalVelocity = Ctx.Mover.Velocity;
                horizontalVelocity.y = 0f;
                float airSpeed = Mathf.Max(horizontalVelocity.magnitude, Ctx.WalkSpeed * 0.5f);
                Ctx.Mover.SetHorizontalVelocity(dir * airSpeed);
            }

            // 首帧保护：Enter 里刚设完起跳速度，避免同帧误判 VerticalVelocity 或 IsGrounded 导致先进 Fall 再进 AirJump
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
