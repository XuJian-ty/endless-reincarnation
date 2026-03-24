using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 待机状态。玩家站立不动时的默认状态。
    ///
    /// Walk / Run / Jump / NormalAttack / Shoot / ShootCharge 必须设为 Interrupt，
    /// 因为 IdleState 没有自然结束点（不调用 CompleteWithPending），
    /// 若设为 Buffer 则这些动作永远无法执行。
    /// </summary>
    public class IdleState : PlayerStateBase
    {
        /// <summary>本状态内连续离地时长，用于避免落地反弹等短暂离地误判为“走出悬崖”</summary>
        private float _continuousAirTime;

        protected override void OnEnter()
        {
            _continuousAirTime = 0f;
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            Ctx.Anim.SetLocomotionSpeed(0f);
            Ctx.Anim.SetLocomotionBlend(Vector2.zero);
            Ctx.Anim.SetGrounded(true);
            Ctx.Anim.TriggerNormalLocomotion();
            StartConfiguredBaseActionTimeline("NormalIdle");
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (Ctx.IsAimModeActive && Ctx.PlayerModel?.CurrentAttackMode == Game.Data.PlayerAttackMode.Ranged)
            {
                GoTo<AimState>();
                return;
            }

            if (IsGrounded)
            {
                _continuousAirTime = 0f;
                return;
            }
            _continuousAirTime += dt;
            // 仅当连续离地超过 0.2s 才进坠落（走出悬崖）；避免落地反弹等单帧/短暂离地误切 Fall
            if (_continuousAirTime <= 0.2f) return;
            var p = Ctx.StateMachine.PeekPending();
            if (!p.IsEmpty && p.Action == GameAction.Jump)
            {
                Ctx.StateMachine.ConsumePending();
                Ctx.StateMachine.ExecuteAction(p);
                return;
            }
            GoTo<FallState>();
        }

        public override TransitionPolicy GetPolicyFor(GameAction action) => action switch
        {
            GameAction.Walk         => TransitionPolicy.Interrupt,
            GameAction.Run          => TransitionPolicy.Interrupt,
            GameAction.Jump         => TransitionPolicy.Interrupt,
            GameAction.NormalAttack => TransitionPolicy.Interrupt,
            GameAction.Shoot        => TransitionPolicy.Interrupt,
            GameAction.ShootCharge  => TransitionPolicy.Interrupt,
            _                       => base.GetPolicyFor(action),
        };
    }
}
