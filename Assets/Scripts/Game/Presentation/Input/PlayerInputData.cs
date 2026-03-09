using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 每帧输入快照（只读值类型）。由 PlayerInputHandler.ManualUpdate() 每帧构建。
    ///
    /// 单帧事件标记（XxxPressed）仅在触发帧为 true，下帧 ManualUpdate 后归零。
    /// </summary>
    public readonly struct PlayerInputData
    {
        // ── 持续输入 ──────────────────────────────────────────────────────
        public readonly Vector2 MoveInput;
        public readonly Vector2 LookDelta;
        public readonly bool    IsRunRequested;
        public readonly bool    IsLmbHeld;

        // ── 单帧事件（触发帧 true，ManualUpdate 后归零）──────────────────
        public readonly bool JumpPressed;
        public readonly bool DodgePressed;
        public readonly bool AttackTapPressed;
        public readonly bool ChargeStartPressed;
        public readonly bool ChargeReleasePressed;
        public readonly bool AltAttackPressed;

        public readonly bool Skill0Pressed;
        public readonly bool Skill1Pressed;
        public readonly bool Skill2Pressed;
        public readonly bool Skill3Pressed;

        public PlayerInputData(
            Vector2 moveInput, Vector2 lookDelta, bool isRunRequested, bool isLmbHeld,
            bool jumpPressed, bool dodgePressed,
            bool attackTapPressed, bool chargeStartPressed, bool chargeReleasePressed,
            bool altAttackPressed,
            bool skill0, bool skill1, bool skill2, bool skill3)
        {
            MoveInput            = moveInput;
            LookDelta            = lookDelta;
            IsRunRequested       = isRunRequested;
            IsLmbHeld            = isLmbHeld;
            JumpPressed          = jumpPressed;
            DodgePressed         = dodgePressed;
            AttackTapPressed     = attackTapPressed;
            ChargeStartPressed   = chargeStartPressed;
            ChargeReleasePressed = chargeReleasePressed;
            AltAttackPressed     = altAttackPressed;
            Skill0Pressed        = skill0;
            Skill1Pressed        = skill1;
            Skill2Pressed        = skill2;
            Skill3Pressed        = skill3;
        }

        public bool TryGetSkillIndex(out int index)
        {
            if (Skill0Pressed) { index = 0; return true; }
            if (Skill1Pressed) { index = 1; return true; }
            if (Skill2Pressed) { index = 2; return true; }
            if (Skill3Pressed) { index = 3; return true; }
            index = -1;
            return false;
        }
    }
}
