using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 枪形态持续射击状态。
    /// 按住左键时持续停留，松开后返回 Aim / Idle / Fall。
    /// </summary>
    public class ShootChargeState : PlayerStateBase
    {
        private bool _hasTriggeredUpperBodyCharge;

        protected override string ActionId => "ShootCharge";
        public override GameAction CurrentActionId => GameAction.ShootCharge;

        protected override void OnEnter()
        {
            Ctx.Anim.TriggerAimLocomotion();
            SnapFacingToCameraForward();
            bool hasMovementInput = Ctx.CurrentMoveInput.sqrMagnitude > 0.01f;
            Ctx.Anim.SetAimLayerActive(!hasMovementInput);
            if (!hasMovementInput)
                TriggerConfiguredBaseAction(ActionId, "Shoot_Charge");
            _hasTriggeredUpperBodyCharge = !hasMovementInput;
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

            if (input.IsLmbHeld)
                return;

            if (!IsGrounded)
            {
                GoTo<FallState>();
                return;
            }

            if (Ctx.IsAimModeActive)
                GoTo<AimState>();
            else
                GoTo<IdleState>();
        }

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, action switch
            {
                GameAction.Dodge         => TransitionPolicy.Interrupt,
                GameAction.Skill         => TransitionPolicy.Interrupt,
                GameAction.ChargeRelease => TransitionPolicy.Ignore,
                GameAction.ChargeStart   => TransitionPolicy.Ignore,
                GameAction.NormalAttack  => TransitionPolicy.Ignore,
                GameAction.ShootCharge   => TransitionPolicy.Ignore,
                GameAction.Shoot         => TransitionPolicy.Ignore,
                _                        => TransitionPolicy.Buffer,
            });

        private void SnapFacingToCameraForward()
        {
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
                Ctx.Anim.SetAimLayerActive(true);
                if (!_hasTriggeredUpperBodyCharge)
                {
                    TriggerConfiguredBaseAction(ActionId, "Shoot_Charge");
                    _hasTriggeredUpperBodyCharge = true;
                }
                Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
                Ctx.Anim.SetLocomotionSpeed(0f);
                Ctx.Anim.SetLocomotionBlend(Vector2.zero);
                UpdateConfiguredAuxiliaryBaseActionTimeline("AimIdle");
                return;
            }

            Ctx.Anim.SetAimLayerActive(false);
            Vector3 moveDirection = Ctx.GetMoveDirection(input.MoveInput);
            bool isRunning = input.IsRunRequested;
            float moveSpeed = isRunning ? Ctx.RunSpeed : Ctx.WalkSpeed;
            float blendScale = isRunning ? 1f : 0.5f;
            Ctx.Mover.SetHorizontalVelocity(moveDirection * moveSpeed);
            Vector3 localDirection = Ctx.Transform.InverseTransformDirection(moveDirection.normalized);
            Ctx.Anim.SetLocomotionSpeed(blendScale);
            Ctx.Anim.SetLocomotionBlend(new Vector2(localDirection.x * blendScale, localDirection.z * blendScale));
            UpdateConfiguredAuxiliaryBaseActionTimeline(isRunning ? "AimRun" : "AimWalk");
        }
    }
}
