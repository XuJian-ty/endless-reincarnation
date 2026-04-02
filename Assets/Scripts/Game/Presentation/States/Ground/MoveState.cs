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
        protected override string PolicyActionId => _isRunning ? "NormalRun" : "NormalWalk";

        protected override void OnEnter()
        {
            _isRunning       = InitialIsRunning ?? false;
            InitialIsRunning = null;

            Ctx.Anim.SetGrounded(true);
            Ctx.Anim.TriggerNormalLocomotion();
            StartConfiguredBaseActionTimeline(_isRunning ? "NormalRun" : "NormalWalk");
            UpdateLocomotionAnimation(Vector3.zero);
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (Ctx.IsAimModeActive && Ctx.PlayerModel?.CurrentAttackMode == Game.Data.PlayerAttackMode.Ranged)
            {
                GoTo<AimState>();
                return;
            }

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
            if (dir.sqrMagnitude > 0.001f)
                Ctx.Mover.RotateToward(dir, Ctx.RotateSpeed);

            UpdateLocomotionAnimation(dir);
            UpdateConfiguredBaseActionTimeline(_isRunning ? "NormalRun" : "NormalWalk");
        }

        private void UpdateLocomotionAnimation(Vector3 moveDirection)
        {
            float blendScale = _isRunning ? 1f : 0.5f;
            Ctx.Anim.SetLocomotionSpeed(blendScale);

            if (moveDirection.sqrMagnitude <= 0.001f)
            {
                Ctx.Anim.SetLocomotionBlend(Vector2.zero);
                return;
            }

            Vector3 localDirection = Ctx.Transform.InverseTransformDirection(moveDirection.normalized);
            Ctx.Anim.SetLocomotionBlend(new Vector2(localDirection.x * blendScale, localDirection.z * blendScale));
        }

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, TransitionPolicy.Ignore);
    }
}
