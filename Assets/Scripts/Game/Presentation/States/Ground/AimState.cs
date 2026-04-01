using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 枪形态瞄准状态。
    /// 角色始终朝向屏幕前方，WASD 只驱动侧移/后撤，不再根据移动方向转身。
    /// </summary>
    public class AimState : PlayerStateBase
    {
        private bool _hasTriggeredUpperBodyAim;

        protected override string ActionId => "Aim";

        protected override void OnEnter()
        {
            Ctx.Anim.SetGrounded(true);
            Ctx.Anim.TriggerAimLocomotion();
            bool hasMovementInput = HasMovementInput(Ctx.CurrentMoveInput);
            Ctx.Anim.SetAimLayerActive(!hasMovementInput);
            if (!hasMovementInput)
                TriggerConfiguredBaseAction(ActionId, "Aim");
            _hasTriggeredUpperBodyAim = !hasMovementInput;
            StartConfiguredBaseActionTimeline(ActionId);
            UpdateConfiguredAuxiliaryBaseActionTimeline("AimIdle");
        }

        protected override void OnExit()
        {
            Ctx.Anim.SetAimLayerActive(true);
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (Ctx?.PlayerModel == null || Ctx.PlayerModel.CurrentAttackMode != Game.Data.PlayerAttackMode.Ranged)
            {
                ExitAimState(in input);
                return;
            }

            if (!Ctx.IsAimModeActive)
            {
                ExitAimState(in input);
                return;
            }

            if (!IsGrounded)
            {
                GoTo<FallState>();
                return;
            }

            Vector3 faceDirection = Ctx.GetMoveDirection(Vector2.up);
            if (faceDirection.sqrMagnitude > 0.001f)
                Ctx.Mover.RotateToward(faceDirection, Ctx.RotateSpeed);

            if (input.MoveInput.sqrMagnitude <= 0.01f)
            {
                Ctx.Anim.SetAimLayerActive(true);
                if (!_hasTriggeredUpperBodyAim)
                {
                    TriggerConfiguredBaseAction(ActionId, "Aim");
                    _hasTriggeredUpperBodyAim = true;
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

        public override TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, action switch
            {
                GameAction.Jump          => TransitionPolicy.Interrupt,
                GameAction.NormalAttack  => TransitionPolicy.Interrupt,
                GameAction.Shoot         => TransitionPolicy.Interrupt,
                GameAction.ChargeStart   => TransitionPolicy.Interrupt,
                GameAction.ShootCharge   => TransitionPolicy.Interrupt,
                GameAction.Walk          => TransitionPolicy.Ignore,
                GameAction.Run           => TransitionPolicy.Ignore,
                GameAction.ChargeRelease => TransitionPolicy.Ignore,
                _                        => base.GetPolicyFor(action),
            });

        private void ExitAimState(in PlayerInputData input)
        {
            if (!IsGrounded)
            {
                GoTo<FallState>();
                return;
            }

            if (input.MoveInput.sqrMagnitude > 0.01f)
            {
                bool isRunning = input.IsRunRequested;
                GoTo<MoveState>(state => state.InitialIsRunning = isRunning);
                return;
            }

            GoTo<IdleState>();
        }

        private static bool HasMovementInput(Vector2 moveInput)
        {
            return moveInput.sqrMagnitude > 0.01f;
        }
    }
}
