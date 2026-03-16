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
        private string TimelineSkillId => $"Attack{ComboIndex}";

        public override GameAction CurrentActionId => GameAction.NormalAttack;

        protected override void OnEnter()
        {
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            TriggerConfiguredActionByActionId(TimelineSkillId, $"Attack{ComboIndex}");
            StartConfiguredTimelineByActionId(TimelineSkillId);
        }

        protected override void OnExit() { }

        public override TransitionPolicy GetPolicyFor(GameAction action) => action switch
        {
            GameAction.Jump => TransitionPolicy.Interrupt,
            _               => base.GetPolicyFor(action),
        };

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            var cameraForward = Ctx.GetMoveDirection(Vector2.up);
            if (cameraForward.sqrMagnitude > 0.001f)
                Ctx.Mover.RotateToward(cameraForward, Ctx.RotateSpeed);

            var pending = Ctx.StateMachine.PeekPending();
            bool hasNormalAttackPending = !pending.IsEmpty && pending.Action == GameAction.NormalAttack;
            bool hasMovePending = !pending.IsEmpty && (pending.Action == GameAction.Walk || pending.Action == GameAction.Run);

            // 普攻预输入：使用普攻专用阈值
            if (hasNormalAttackPending)
            {
                float normalAttackThreshold = GetConfiguredPendingReleaseThreshold(
                    GameAction.NormalAttack,
                    ComboIndex <= 2 ? 0.70f : 0.60f);

                if (AnimNearEnd(normalAttackThreshold))
                {
                    if (ComboIndex < 3)
                        Ctx.StateMachine.OpenComboWindow(ComboIndex + 1);
                    CompleteWithPending(() => { });
                    return;
                }
            }

            // 移动预输入：使用移动专用阈值（在编辑器中可调）
            if (hasMovePending)
            {
                float moveThreshold = GetConfiguredPendingReleaseThreshold(
                    pending.Action,
                    ComboIndex <= 2 ? 0.80f : 0.70f);

                if (AnimNearEnd(moveThreshold))
                {
                    if (ComboIndex < 3)
                        Ctx.StateMachine.OpenComboWindow(ComboIndex + 1);
                    CompleteWithPending(() => { });
                    return;
                }
            }

            // 自然结束：动画结束时退出，1～3 段开启连击窗口 0.3s
            if (!AnimNearConfiguredEnd()) return;

            if (ComboIndex < 3)
                Ctx.StateMachine.OpenComboWindow(ComboIndex + 1);

            CompleteWithPending(() =>
            {
                if (IsGrounded)
                    GoToConfiguredNaturalExit(Game.Data.PlayerStateNaturalExitTarget.IdleState);
                else
                    GoTo<FallState>();
            });
        }
    }
}
