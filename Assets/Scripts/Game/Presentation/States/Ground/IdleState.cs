using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 待机状态。玩家站立不动时的默认状态。
    /// </summary>
    public class IdleState : PlayerStateBase
    {
        protected override string PolicyActionId => "NormalIdle";

        protected override void OnEnter()
        {
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

            // 仅当连续离地超过 0.5s 才进坠落（走出悬崖）；避免落地反弹等单帧/短暂离地误切 Fall
            if (!HasExceededFallTransitionDelay(dt)) return;
            var p = Ctx.StateMachine.PeekPending();
            if (!p.IsEmpty && p.Action == GameAction.Jump)
            {
                Ctx.StateMachine.ConsumePending();
                Ctx.StateMachine.ExecuteAction(p);
                return;
            }
            GoTo<FallState>();
        }

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, TransitionPolicy.Ignore);
    }
}
