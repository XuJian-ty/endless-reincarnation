namespace Game.Presentation
{
    /// <summary>
    /// 下落攻击循环状态：FallAttackStart 动画播完后进入，循环下落直到接触地面。
    /// 自然退出：仅当接地（IsGrounded）时 → FallAttackLandState；本状态内不切换到 FallState，不提前结束。
    /// </summary>
    public class FallAttackLoopState : PlayerStateBase
    {
        public override GameAction CurrentActionId => GameAction.FallAttack;

        protected override void OnEnter()
        {
            TriggerConfiguredActionByActionId("FallAttackLoop", "FallAttackLoop");
            StartConfiguredTimelineByActionId("FallAttackLoop");
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            // 首帧保护：刚进入时 CharacterController.isGrounded 可能是上一帧残留值，跳过前 0.1s
            if (StateAge <= 0.1f) return;
            if (IsGrounded)
                GoTo<FallAttackLandState>();
        }

        /// <summary>仅由本状态 OnTick（接地→FallAttackLand）决定退出，不允许外部 Interrupt 打断。</summary>
        public override TransitionPolicy GetPolicyFor(GameAction action) => TransitionPolicy.Ignore;
    }
}
