using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 闪避状态。
    ///
    /// 特性：
    ///   - 整个动画期间为无敌帧（由伤害系统查询 IsInvincible 属性实现）
    ///   - 按当前 Move 输入执行相机相对闪避；无输入时默认前闪
    ///   - 仅当有缓存的 Walk/Run 预输入时在约 80% 提前结束并进入移动；否则在动画结束时自然退出
    /// </summary>
    public class DodgeState : PlayerStateBase, IPlayerStateWithDuration
    {
        private enum DodgeAnimationDirection
        {
            Forward = 0,
            Left = 1,
            Back = 2,
            Right = 3,
        }

        public bool IsInvincible => true;
        public float RemainingTime => -1f;
        public float NormalizedProgress => Ctx?.Anim != null ? Ctx.Anim.GetCurrentNormalizedTime() : -1f;

        public override GameAction CurrentActionId => GameAction.Dodge;

        protected override void OnEnter()
        {
            Vector2 cardinalMoveInput = ResolveCardinalMoveInput(Ctx.CurrentMoveInput);
            Vector3 dodgeDirection = ResolveDodgeDirection(cardinalMoveInput);
            DodgeAnimationDirection animationDirection = ResolveAnimationDirection(dodgeDirection);

            Ctx.Anim.SetDodgeDirection((int)animationDirection);
            Ctx.Mover.StartDodge(dodgeDirection, Ctx.DodgeSpeed);
            TriggerConfiguredBaseAction("Dodge", "Dodge");
            StartConfiguredBaseActionTimeline("Dodge");
        }

        protected override void OnExit()
        {
            Ctx.Mover.StopDodge();
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            var pending = Ctx.StateMachine.PeekPending();
            bool hasMovePending = !pending.IsEmpty && (pending.Action == GameAction.Walk || pending.Action == GameAction.Run);
            bool alreadyMovedToLoop = StateAge > 0.1f && Ctx.Anim.IsCurrentStateLooping();
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
            if (AnimNearConfiguredEnd() || alreadyMovedToLoop)
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

        private Vector3 ResolveDodgeDirection(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude > 0.01f)
            {
                Vector3 moveDirection = Ctx.GetMoveDirection(moveInput);
                moveDirection.y = 0f;
                if (moveDirection.sqrMagnitude > 0.001f)
                    return moveDirection.normalized;
            }

            Vector3 forward = Ctx.Transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        }

        private DodgeAnimationDirection ResolveAnimationDirection(Vector3 dodgeDirection)
        {
            Vector3 localDirection = Ctx.Transform.InverseTransformDirection(dodgeDirection);
            localDirection.y = 0f;

            if (Mathf.Abs(localDirection.z) >= Mathf.Abs(localDirection.x))
                return localDirection.z >= 0f ? DodgeAnimationDirection.Forward : DodgeAnimationDirection.Back;

            return localDirection.x >= 0f ? DodgeAnimationDirection.Right : DodgeAnimationDirection.Left;
        }

        private static Vector2 ResolveCardinalMoveInput(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude <= 0.01f)
                return Vector2.zero;

            if (Mathf.Abs(moveInput.y) >= Mathf.Abs(moveInput.x))
                return new Vector2(0f, Mathf.Sign(moveInput.y));

            return new Vector2(Mathf.Sign(moveInput.x), 0f);
        }
    }
}
