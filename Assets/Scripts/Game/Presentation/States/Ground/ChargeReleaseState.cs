using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 蓄力释放状态（LMB SlowTap 达到 0.7s 后松开触发）。
    /// 播放蓄力攻击释放动画，动画结束后消费预输入或回 Idle。
    /// </summary>
    public class ChargeReleaseState : PlayerStateBase
    {
        public override GameAction CurrentActionId => GameAction.ChargeRelease;

        protected override void OnEnter()
        {
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            Ctx.Anim.TriggerChargeRelease();
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (AnimNearEnd()) CompleteWithPending(() => { if (IsGrounded) GoTo<IdleState>(); else GoTo<FallState>(); });
        }

        public override TransitionPolicy GetPolicyFor(GameAction action) => action switch
        {
            GameAction.Dodge        => TransitionPolicy.Interrupt,
            GameAction.Skill        => TransitionPolicy.Interrupt,
            GameAction.ChargeStart  => TransitionPolicy.Buffer,
            GameAction.ChargeRelease=> TransitionPolicy.Ignore,
            GameAction.NormalAttack => TransitionPolicy.Buffer,
            GameAction.Jump         => TransitionPolicy.Buffer,
            GameAction.Walk         => TransitionPolicy.Buffer,
            GameAction.Run          => TransitionPolicy.Buffer,
            _                       => TransitionPolicy.Ignore,
        };
    }
}
