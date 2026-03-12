namespace Game.Presentation
{
    /// <summary>
    /// 闪避状态。
    ///
    /// 特性：
    ///   - 整个动画期间为无敌帧（由伤害系统查询 IsInvincible 属性实现）
    ///   - 朝角色当前面朝方向闪避
    ///   - 仅当有缓存的 Walk/Run 预输入时在约 80% 提前结束并进入移动；否则在动画结束时自然退出
    /// </summary>
    public class DodgeState : PlayerStateBase, IPlayerStateWithDuration
    {
        public bool IsInvincible => true;
        public float RemainingTime => -1f;
        public float NormalizedProgress => Ctx?.Anim != null ? Ctx.Anim.GetCurrentNormalizedTime() : -1f;

        public override GameAction CurrentActionId => GameAction.Dodge;

        protected override void OnEnter()
        {
            Ctx.Mover.StartDodge(Ctx.Transform.forward, Ctx.DodgeSpeed);
            Ctx.Anim.TriggerDodge();
        }

        protected override void OnExit()
        {
            Ctx.Mover.StopDodge();
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            var pending = Ctx.StateMachine.PeekPending();
            bool hasMovePending = !pending.IsEmpty && (pending.Action == GameAction.Walk || pending.Action == GameAction.Run);
            if (hasMovePending && AnimNearEnd(GetConfiguredPendingReleaseThreshold(pending.Action, 0.80f)))
            {
                CompleteWithPending(() =>
                {
                    if (IsGrounded)
                        GoToConfiguredNaturalExit(Game.Data.PlayerStateNaturalExitTarget.IdleState);
                    else
                        GoTo<FallState>();
                });
                return;
            }
            if (AnimNearConfiguredEnd())
                CompleteWithPending(() =>
                {
                    if (IsGrounded)
                        GoToConfiguredNaturalExit(Game.Data.PlayerStateNaturalExitTarget.IdleState);
                    else
                        GoTo<FallState>();
                });
        }

        public override TransitionPolicy GetPolicyFor(GameAction action) => action switch
        {
            GameAction.Dodge         => TransitionPolicy.Ignore,
            GameAction.Skill         => TransitionPolicy.Ignore,
            GameAction.ChargeRelease => TransitionPolicy.Ignore,
            _                        => TransitionPolicy.Buffer,
        };
    }
}
