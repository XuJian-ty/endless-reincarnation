using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 枪形态点射状态。
    /// 播放 Shoot 动作，结束后根据上下文返回 Aim / Idle / Fall。
    /// </summary>
    public class ShootState : PlayerStateBase
    {
        private const int AimLayerIndex = 1;
        private bool _playsUpperBodyShootAnimation;

        protected override string ActionId => "Shoot";
        public override GameAction CurrentActionId => GameAction.Shoot;

        protected override void OnEnter()
        {
            Ctx.Anim.TriggerAimLocomotion();
            SnapFacingToCameraForwardForHipFire();
            _playsUpperBodyShootAnimation = Ctx.CurrentMoveInput.sqrMagnitude <= 0.01f;
            Ctx.Anim.SetAimLayerActive(_playsUpperBodyShootAnimation);
            TriggerConfiguredBaseAction(ActionId, "Shoot");
            StartConfiguredBaseActionTimeline(ActionId);
            UpdateConfiguredAuxiliaryBaseActionTimeline("AimIdle");
        }

        protected override void OnExit()
        {
            Ctx.Anim.SetAimLayerActive(true);
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            UpdateAimLocomotion(in input);

            PendingActionData pending = Ctx.StateMachine.PeekPending();
            bool hasShootPending = !pending.IsEmpty && pending.Action == GameAction.Shoot;
            bool hasMovePending = !pending.IsEmpty && (pending.Action == GameAction.Walk || pending.Action == GameAction.Run);

            if (hasShootPending)
            {
                float attackThreshold = GetConfiguredPendingReleaseThreshold(pending.Action, 0.35f);
                if (IsShootAnimationNearEnd(attackThreshold))
                {
                    CompleteWithPending(() => { });
                    return;
                }
            }

            if (hasMovePending)
            {
                float moveThreshold = GetConfiguredPendingReleaseThreshold(pending.Action, 0.45f);
                if (IsShootAnimationNearEnd(moveThreshold))
                {
                    CompleteWithPending(() => { });
                    return;
                }
            }

            if (!IsShootAnimationNearEnd(GetConfiguredNaturalExitThreshold(0.45f)))
                return;

            bool canStayOnGroundTrack = IsGrounded || !HasExceededFallTransitionDelay(dt);
            CompleteWithPending(() =>
            {
                if (canStayOnGroundTrack && Ctx.IsAimModeActive)
                    GoTo<AimState>();
                else if (canStayOnGroundTrack)
                    GoToConfiguredNaturalExit(Game.Data.PlayerStateNaturalExitTarget.IdleState);
                else
                    GoTo<FallState>();
            });
        }

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, action switch
            {
                GameAction.Jump => TransitionPolicy.Interrupt,
                _               => base.GetPolicyFor(action),
            });

        private bool IsShootAnimationNearEnd(float threshold)
        {
            if (StateAge <= 0.1f)
                return false;

            return Ctx.Anim.IsCurrentStateNearEnd(threshold, AimLayerIndex);
        }

        private void SnapFacingToCameraForwardForHipFire()
        {
            if (Ctx.IsAimModeActive)
                return;

            Vector3 faceDirection = Ctx.GetMoveDirection(Vector2.up);
            if (faceDirection.sqrMagnitude <= 0.001f)
                return;

            Ctx.Transform.rotation = Quaternion.LookRotation(faceDirection.normalized);
        }

        private void UpdateAimLocomotion(in PlayerInputData input)
        {
            Vector3 faceDirection = Ctx.GetMoveDirection(Vector2.up);
            if (faceDirection.sqrMagnitude > 0.001f)
                Ctx.Mover.RotateToward(faceDirection, Ctx.RotateSpeed);

            if (input.MoveInput.sqrMagnitude <= 0.01f)
            {
                Ctx.Anim.SetAimLayerActive(_playsUpperBodyShootAnimation);
                Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
                Ctx.Anim.SetLocomotionSpeed(0f);
                Ctx.Anim.SetLocomotionBlend(Vector2.zero);
                UpdateConfiguredAuxiliaryBaseActionTimeline("AimIdle");
                return;
            }

            Ctx.Anim.SetAimLayerActive(false);
            Vector3 moveDirection = Ctx.GetMoveDirection(input.MoveInput);
            bool isRunning = input.IsRunningRequested;
            float moveSpeed = input.IsSprintRequested
                ? Ctx.RunSpeed * 2f
                : isRunning ? Ctx.RunSpeed : Ctx.WalkSpeed;
            float blendScale = isRunning ? 1f : 0.5f;
            Ctx.Mover.SetHorizontalVelocity(moveDirection * moveSpeed);
            Vector3 localDirection = Ctx.Transform.InverseTransformDirection(moveDirection.normalized);
            Ctx.Anim.SetLocomotionSpeed(blendScale);
            Ctx.Anim.SetLocomotionBlend(new Vector2(localDirection.x * blendScale, localDirection.z * blendScale));
            UpdateConfiguredAuxiliaryBaseActionTimeline(isRunning ? "AimRun" : "AimWalk");
        }
    }
}
