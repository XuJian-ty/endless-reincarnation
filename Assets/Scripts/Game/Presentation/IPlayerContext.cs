using UnityEngine;
using Game.Domain;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 所有玩家状态可访问的上下文接口。
    /// 将状态与 PlayerController 具体实现解耦，便于测试和扩展。
    /// </summary>
    public interface IPlayerContext : ICombatActionReadable
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
