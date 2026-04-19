using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 所有可请求的玩家离散动作。
    /// 由 PlayerStateMachine 将 PlayerInputData 映射为此枚举，
    /// 再通过各状态的 GetPolicyFor() 决定立即执行、缓存还是忽略。
    /// </summary>
    public enum GameAction
    {
        [InspectorName("None")]
        None = 0,

        // ── 移动 ──────────────────────────────────────────────────────────
        [InspectorName("Walk")]
        Walk = 1,
        [InspectorName("Run")]
        Run = 2,
        [InspectorName("Jump")]
        Jump = 3,
        [InspectorName("AirJump")]
        AirJump = 13,

        // ── 地面攻击 ──────────────────────────────────────────────────────
        [InspectorName("NormalAttack")]
        NormalAttack = 4,    // AttackTap（Tap Interaction）在地面上下文触发
        [InspectorName("ChargeStart")]
        ChargeStart = 5,     // Hold Interaction 达到 0.2s 触发
        [InspectorName("ChargeRelease")]
        ChargeRelease = 6,   // SlowTap Interaction 达到 0.7s 后松开触发

        // ── 空中攻击 ──────────────────────────────────────────────────────
        [InspectorName("AirAttack")]
        AirAttack = 7,       // AttackTap 在空中上下文触发（与 NormalAttack 同按键，上下文区分）
        [InspectorName("FallAttackStart")]
        FallAttack = 8,      // AltAttack（RMB）在空中触发

        // ── 防御 ──────────────────────────────────────────────────────────
        [InspectorName("Dodge")]
        Dodge = 9,

        // ── 技能 ──────────────────────────────────────────────────────────
        [InspectorName("Skill")]
        Skill = 10,           // PendingActionData.SkillIndex 记录具体技能编号

        // ── 远程攻击 ──────────────────────────────────────────────────────
        [InspectorName("Shoot")]
        Shoot = 11,
        [InspectorName("ShootCharge")]
        ShootCharge = 12,
    }
}
