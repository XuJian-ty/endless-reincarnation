using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 受击硬直状态。
    /// 进入时清空所有预输入（被打断时玩家之前的动作应作废）。
    /// 硬直期间忽略所有输入（GetPolicyFor 全部 Ignore）。
    /// </summary>
    public class HitStunState : PlayerStateBase
    {
        /// <summary>由 PlayerController.OnHit 注入的硬直时长（秒）</summary>
        public float Duration { get; set; } = 0.3f;

        private float _timer;

        public override float RemainingTime => _timer;
        public override float NormalizedProgress => Duration > 0f ? 1f - _timer / Duration : 1f;

        protected override void OnEnter()
        {
            _timer = Duration;
            Ctx.StateMachine.ClearPending();
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            TriggerConfiguredBaseAction("HitStun", "HitStun");
            StartConfiguredBaseActionTimeline("HitStun");
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            _timer -= dt;
            if (_timer <= 0f)
            {
                Vector2 moveInput = input.MoveInput;
                bool isRunRequested = input.IsRunningRequested;
                CompleteWithPending(() =>
                {
                    if (IsGrounded)
                    {
                        if (moveInput.sqrMagnitude > 0.01f)
                        {
                            GoTo<MoveState>(s => s.InitialIsRunning = isRunRequested);
                        }
                        else
                            GoTo<IdleState>();
                    }
                    else
                    {
                        GoTo<FallState>();
                    }
                });
            }
        }

        public override TransitionPolicy GetPolicyFor(GameAction action) =>
            ResolveConfiguredPolicy(action, TransitionPolicy.Ignore);
    }
}
