using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 玩家死亡状态：触发死亡动画，等待动画接近结束后再通知流程层。
    /// </summary>
    public class PlayerDeathState : PlayerStateBase, IPlayerStateWithDuration
    {
        private const float FallbackDuration = 3f;
        private const float CompletionDelay = 1.5f;

        private float _timer;
        private float _completionDelayTimer;
        private bool _awaitingCompletionDelay;
        protected override string ActionId => "PlayerDeath";

        public float RemainingTime => Mathf.Max(0f, _timer) + Mathf.Max(0f, _completionDelayTimer);
        public float NormalizedProgress => 1f - Mathf.Clamp01(RemainingTime / (FallbackDuration + CompletionDelay));
        public override GameAction CurrentActionId => GameAction.None;

        protected override void OnEnter()
        {
            _timer = FallbackDuration;
            _completionDelayTimer = 0f;
            _awaitingCompletionDelay = false;
            Ctx.StateMachine.ClearPending();
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            Ctx.Mover.SetVerticalVelocity(0f);
            Ctx.Anim.SetLocomotionSpeed(0f);
            TriggerConfiguredBaseAction(ActionId, "Dead");
            StartConfiguredBaseActionTimeline(ActionId);
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (!_awaitingCompletionDelay)
            {
                _timer -= dt;
                if (AnimNearConfiguredEnd(0.95f) || _timer <= 0f)
                {
                    _awaitingCompletionDelay = true;
                    _completionDelayTimer = CompletionDelay;
                }
                return;
            }

            _completionDelayTimer -= dt;
            if (_completionDelayTimer <= 0f)
                Ctx.CompleteDeathSequence();
        }

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, TransitionPolicy.Ignore);
    }
}
