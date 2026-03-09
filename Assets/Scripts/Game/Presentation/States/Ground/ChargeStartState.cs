using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 蓄力开始状态（LMB Hold 达到 0.2s 触发）。
    /// 自然退出条件：动画结束时退出，退出后进入蓄力循环。
    /// 闪避/技能/蓄力释放可打断。
    /// </summary>
    public class ChargeStartState : PlayerStateBase
    {
        public override GameAction CurrentActionId => GameAction.ChargeStart;

        protected override void OnEnter()
        {
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            Ctx.Anim.TriggerChargeStart();
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (AnimNearEnd()) GoTo<ChargeLoopState>();
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
