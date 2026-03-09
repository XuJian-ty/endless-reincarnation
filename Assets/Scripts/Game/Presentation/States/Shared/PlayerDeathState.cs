using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 玩家死亡状态：触发死亡动画，等待动画接近结束后再通知流程层。
    /// </summary>
    public class PlayerDeathState : PlayerStateBase, IPlayerStateWithDuration
    {
        private const float FallbackDuration = 3f;

        private float _timer;

        public float RemainingTime => Mathf.Max(0f, _timer);
        public float NormalizedProgress => 1f - Mathf.Clamp01(_timer / FallbackDuration);
        public override GameAction CurrentActionId => GameAction.None;

        protected override void OnEnter()
        {
            _timer = FallbackDuration;
            Ctx.StateMachine.ClearPending();
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            Ctx.Mover.SetVerticalVelocity(0f);
            Ctx.Anim.SetLocomotionSpeed(0f);
            Ctx.Anim.TriggerDead();
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            _timer -= dt;
            if (AnimNearEnd(0.95f) || _timer <= 0f)
                Ctx.CompleteDeathSequence();
        }

        public override TransitionPolicy GetPolicyFor(GameAction action) => TransitionPolicy.Ignore;
    }
}
