using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 四段普攻状态的公共基类（Template Method）。
    ///
    /// 与需求说明书一致：
    ///   - 连击窗口在本段动画结束时开启，窗口时长 0.3s（由状态机维护）
    ///   - 普攻预输入与移动预输入分别使用独立阈值（在编辑器中可调）：普攻 1～3 段 / 4 段、移动 1～3 段 / 4 段
    /// </summary>
    public abstract class AttackStateBase : PlayerStateBase
    {
        protected abstract int ComboIndex { get; }

        public override GameAction CurrentActionId => GameAction.NormalAttack;

        protected override void OnEnter()
        {
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            Ctx.Anim.TriggerAttack(ComboIndex);
        }

        protected override void OnExit() { }

        public override TransitionPolicy GetPolicyFor(GameAction action) => action switch
        {
            GameAction.Jump => TransitionPolicy.Interrupt,
            _               => base.GetPolicyFor(action),
        };

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            var pending = Ctx.StateMachine.PeekPending();
            bool hasNormalAttackPending = !pending.IsEmpty && pending.Action == GameAction.NormalAttack;
            bool hasMovePending = !pending.IsEmpty && (pending.Action == GameAction.Walk || pending.Action == GameAction.Run);

            // 普攻预输入：使用普攻专用阈值
            if (hasNormalAttackPending)
            {
                if (ComboIndex <= 2 && AnimNearEnd(Ctx.ComboEarlyExitThresholdSegment1To3))
                {
                    if (ComboIndex < 3)
                        Ctx.StateMachine.OpenComboWindow(ComboIndex + 1);
                    CompleteWithPending(() => { });
                    return;
                }
                if (ComboIndex == 3 && AnimNearEnd(Ctx.ComboEarlyExitThresholdSegment4))
                {
                    CompleteWithPending(() => { });
                    return;
                }
            }

            // 移动预输入：使用移动专用阈值（在编辑器中可调）
            if (hasMovePending)
            {
                if (ComboIndex <= 2 && AnimNearEnd(Ctx.MoveComboEarlyExitThresholdSegment1To3))
                {
                    if (ComboIndex < 3)
                        Ctx.StateMachine.OpenComboWindow(ComboIndex + 1);
                    CompleteWithPending(() => { });
                    return;
                }
                if (ComboIndex == 3 && AnimNearEnd(Ctx.MoveComboEarlyExitThresholdSegment4))
                {
                    CompleteWithPending(() => { });
                    return;
                }
            }

            // 自然结束：动画结束时退出，1～3 段开启连击窗口 0.3s
            if (!AnimNearEnd()) return;

            if (ComboIndex < 3)
                Ctx.StateMachine.OpenComboWindow(ComboIndex + 1);

            CompleteWithPending(() => { if (IsGrounded) GoTo<IdleState>(); else GoTo<FallState>(); });
        }
    }
}
