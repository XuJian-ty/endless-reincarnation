namespace Game.Presentation
{
    /// <summary>
    /// 蓄力循环状态：蓄力开始动画播完后进入，持续循环直到释放或取消。
    /// 代码驱动进入（由 ChargeStartState.OnTick 检测动画接近结束后切过来）。
    /// </summary>
    public class ChargeLoopState : PlayerStateBase
    {
        public override GameAction CurrentActionId => GameAction.ChargeStart;

        protected override void OnEnter()  => Ctx.Anim.TriggerChargeLoop();

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (!input.IsLmbHeld) { if (IsGrounded) GoTo<IdleState>(); else GoTo<FallState>(); }
        }

        public override TransitionPolicy GetPolicyFor(GameAction action) => action switch
        {
            GameAction.Dodge         => TransitionPolicy.Interrupt,
            GameAction.Skill         => TransitionPolicy.Interrupt,
            GameAction.ChargeRelease => TransitionPolicy.Interrupt,
            GameAction.ChargeStart   => TransitionPolicy.Ignore,
            GameAction.NormalAttack  => TransitionPolicy.Ignore,
            _                        => TransitionPolicy.Buffer,
        };
    }
}
