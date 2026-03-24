using UnityEngine;
using Game.Domain;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 所有玩家状态可访问的上下文接口。
    /// 将状态与 PlayerController 具体实现解耦，便于测试和扩展。
    /// </summary>
    public interface IPlayerContext
    {
        // ── 组件引用 ──────────────────────────────────────────────────────
        PlayerMover              Mover        { get; }
        PlayerAnimatorController Anim         { get; }
        PlayerStateMachine       StateMachine { get; }
        Transform                Transform    { get; }
        PlayerModel              PlayerModel  { get; }

        // ── 移动参数 ─────────────────────────────────────────────────────
        float WalkSpeed   { get; }
        float RunSpeed    { get; }
        /// <summary>跳跃高度（米），传入 PlayerMover.Jump()</summary>
        float JumpHeight  { get; }
        float DodgeSpeed  { get; }
        /// <summary>角色朝向旋转速度（度/秒），传入 PlayerMover.RotateToward()</summary>
        float RotateSpeed { get; }

        // ── 当前状态查询（HUD / 调试 / UI 用）──────────────────────────────
        /// <summary>当前状态类名，如 "IdleState"、"DodgeState"</summary>
        string CurrentStateName { get; }
        /// <summary>当前动作枚举；Idle/Move 为 None</summary>
        GameAction CurrentActionId { get; }
        /// <summary>带有时长状态（如受击硬直、闪避）的剩余时间（秒），否则 -1</summary>
        float StateRemainingTime { get; }
        /// <summary>带有时长状态的进度 0～1，否则 -1</summary>
        float StateNormalizedProgress { get; }

        // ── 工具方法 ──────────────────────────────────────────────────────
        /// <summary>将 MoveInput（相机空间 XY）转换为世界空间水平移动方向</summary>
        Vector3 GetMoveDirection(Vector2 input);

        /// <summary>当前帧原始移动输入（重绑定后的 Move 方向）。</summary>
        Vector2 CurrentMoveInput { get; }

        /// <summary>玩家是否已进入死亡流程。</summary>
        bool IsDead { get; }

        /// <summary>当前是否处于枪形态瞄准模式。</summary>
        bool IsAimModeActive { get; }

        /// <summary>死亡动画播放完成后，由死亡状态回调流程层。</summary>
        void CompleteDeathSequence();
    }
}
