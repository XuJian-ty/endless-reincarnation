using System.Collections.Generic;
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
        public readonly bool    IsSprintRequested;
        public readonly bool    IsLmbHeld;

        public bool IsRunningRequested => IsRunRequested || IsSprintRequested;

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
        private readonly IReadOnlyList<int> _pressedSkillIndices;

        public int PressedSkillCount => _pressedSkillIndices != null ? _pressedSkillIndices.Count : 0;

        public PlayerInputData(
            Vector2 moveInput, Vector2 lookDelta, bool isRunRequested, bool isSprintRequested, bool isLmbHeld,
            bool jumpPressed, bool dodgePressed,
            bool attackTapPressed, bool chargeStartPressed, bool chargeReleasePressed,
            bool altAttackPressed,
            IReadOnlyList<int> pressedSkillIndices)
        {
            MoveInput            = moveInput;
            LookDelta            = lookDelta;
            IsRunRequested       = isRunRequested;
            IsSprintRequested    = isSprintRequested;
            IsLmbHeld            = isLmbHeld;
            JumpPressed          = jumpPressed;
            DodgePressed         = dodgePressed;
            AttackTapPressed     = attackTapPressed;
            ChargeStartPressed   = chargeStartPressed;
            ChargeReleasePressed = chargeReleasePressed;
            AltAttackPressed     = altAttackPressed;
            _pressedSkillIndices = pressedSkillIndices;
            Skill0Pressed        = ContainsSkillIndex(pressedSkillIndices, 0);
            Skill1Pressed        = ContainsSkillIndex(pressedSkillIndices, 1);
            Skill2Pressed        = ContainsSkillIndex(pressedSkillIndices, 2);
            Skill3Pressed        = ContainsSkillIndex(pressedSkillIndices, 3);
        }

        public bool TryGetSkillIndex(out int index)
        {
            if (_pressedSkillIndices != null && _pressedSkillIndices.Count > 0)
            {
                index = _pressedSkillIndices[0];
                return true;
            }

            index = -1;
            return false;
        }

        public bool IsSkillPressed(int slotIndex)
        {
            return ContainsSkillIndex(_pressedSkillIndices, slotIndex);
        }

        public bool TryGetPressedSkillIndexAt(int pressedOrder, out int slotIndex)
        {
            if (_pressedSkillIndices != null && pressedOrder >= 0 && pressedOrder < _pressedSkillIndices.Count)
            {
                slotIndex = _pressedSkillIndices[pressedOrder];
                return true;
            }

            slotIndex = -1;
            return false;
        }

        private static bool ContainsSkillIndex(IReadOnlyList<int> pressedSkillIndices, int slotIndex)
        {
            if (pressedSkillIndices == null)
                return false;

            for (int i = 0; i < pressedSkillIndices.Count; i++)
            {
                if (pressedSkillIndices[i] == slotIndex)
                    return true;
            }

            return false;
        }
    }
}
