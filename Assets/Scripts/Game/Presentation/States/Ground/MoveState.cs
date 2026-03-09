using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 移动状态（走路 / 跑步）。
    ///
    /// 走/跑切换：不分裂为两个状态，_isRunning 在内部更新，
    /// 仅改变移动速度和动画参数，避免冗余状态切换开销。
    ///
    /// 进入配置：DodgeState 80% 结束闪避并释放 Walk/Run 缓存时，通过 configure 委托设置 InitialIsRunning，
    /// 其他情况下由 PlayerInputData.IsRunRequested 决定。
    /// </summary>
    public class MoveState : PlayerStateBase
    {
        /// <summary>DodgeState 提前释放时，通过 configure 委托注入是否跑步</summary>
        public bool? InitialIsRunning { get; set; }

        private bool _isRunning;

        protected override void OnEnter()
        {
            _isRunning       = InitialIsRunning ?? false;
            InitialIsRunning = null;

            Ctx.Anim.SetGrounded(true);
            Ctx.Anim.TriggerLocomotion();
            UpdateAnimSpeed();
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            // 仅当持续离地超过 0.2s 才考虑进坠落（走出悬崖）。进 Fall 的决策只在本状态内部做。
            if (StateAge > 0.2f && !IsGrounded)
            {
                var p = Ctx.StateMachine.PeekPending();
                if (!p.IsEmpty && p.Action == GameAction.Jump)
                {
                    Ctx.StateMachine.ConsumePending();
                    Ctx.StateMachine.ExecuteAction(p);
                    return;
                }
                GoTo<FallState>();
                return;
            }

            if (input.MoveInput.sqrMagnitude <= 0.01f)
            {
                GoTo<IdleState>();
                return;
            }

            _isRunning = input.IsRunRequested;
            float speed = _isRunning ? Ctx.RunSpeed : Ctx.WalkSpeed;
            var   dir   = Ctx.GetMoveDirection(input.MoveInput);

            Ctx.Mover.SetHorizontalVelocity(dir * speed);
            UpdateAnimSpeed();

            if (dir.sqrMagnitude > 0.001f)
                Ctx.Mover.RotateToward(dir, Ctx.RotateSpeed);
        }

        private void UpdateAnimSpeed()
            => Ctx.Anim.SetLocomotionSpeed(_isRunning ? 1f : 0.5f);

        public override TransitionPolicy GetPolicyFor(GameAction action) => action switch
        {
            GameAction.Jump         => TransitionPolicy.Interrupt,
            GameAction.NormalAttack => TransitionPolicy.Interrupt,
            // MoveState 在 OnTick 中直接读取 input，无需将 Walk/Run 写入 _pending。
            // 继承 base 的 Buffer 会每帧污染 _pending，导致攻击/技能结束后 CompleteWithPending
            // 意外消费到残留的移动动作，引发状态机抖动。
            GameAction.Walk         => TransitionPolicy.Ignore,
            GameAction.Run          => TransitionPolicy.Ignore,
            _                       => base.GetPolicyFor(action),
        };
    }
}
