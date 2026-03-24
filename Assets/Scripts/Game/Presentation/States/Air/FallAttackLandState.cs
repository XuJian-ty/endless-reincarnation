namespace Game.Presentation
{
    /// <summary>
    /// 下落攻击落地状态（落地打击动画）。
    /// 自然退出：动画结束且接地 → Idle；未接地（仍悬空）→ 回到 FallAttackLoop，不切到 Fall。
    /// 下落攻击三状态内均不包含任何切换到 Fall 的逻辑。
    /// </summary>
    public class FallAttackLandState : PlayerStateBase
    {
        public override GameAction CurrentActionId => GameAction.FallAttack;

        protected override void OnEnter()
        {
            Ctx.Anim.SetGrounded(true);
            TriggerConfiguredBaseAction("FallAttackLand", "FallAttackLand");
            StartConfiguredBaseActionTimeline("FallAttackLand");
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (!AnimNearConfiguredEnd()) return;
            CompleteWithPending(() =>
            {
                if (IsGrounded)
                    GoToConfiguredNaturalExit(Game.Data.PlayerStateNaturalExitTarget.IdleState);
                else
                    GoTo<FallAttackLoopState>();
            });
        }

        public override TransitionPolicy GetPolicyFor(GameAction action) => action switch
        {
            GameAction.Dodge         => TransitionPolicy.Interrupt,
            GameAction.Skill         => TransitionPolicy.Interrupt,
            GameAction.ChargeStart   => TransitionPolicy.Interrupt,
            GameAction.NormalAttack  => TransitionPolicy.Buffer,
            GameAction.Jump          => TransitionPolicy.Buffer,
            GameAction.Walk          => TransitionPolicy.Buffer,
            GameAction.Run           => TransitionPolicy.Buffer,
            _                        => TransitionPolicy.Ignore,
        };
    }
}
