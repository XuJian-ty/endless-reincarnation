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
        None,

        // ── 移动 ──────────────────────────────────────────────────────────
        [InspectorName("Walk")]
        Walk,
        [InspectorName("Run")]
        Run,
        [InspectorName("Jump")]
        Jump,

        // ── 地面攻击 ──────────────────────────────────────────────────────
        [InspectorName("NormalAttack")]
        NormalAttack,    // AttackTap（Tap Interaction）在地面上下文触发
        [InspectorName("ChargeStart")]
        ChargeStart,     // Hold Interaction 达到 0.2s 触发
        [InspectorName("ChargeRelease")]
        ChargeRelease,   // SlowTap Interaction 达到 0.7s 后松开触发

        // ── 空中攻击 ──────────────────────────────────────────────────────
        [InspectorName("AirAttack")]
        AirAttack,       // AttackTap 在空中上下文触发（与 NormalAttack 同按键，上下文区分）
        [InspectorName("FallAttackStart")]
        FallAttack,      // AltAttack（RMB）在空中触发

        // ── 防御 ──────────────────────────────────────────────────────────
        [InspectorName("Dodge")]
        Dodge,

        // ── 技能 ──────────────────────────────────────────────────────────
        [InspectorName("Skill")]
        Skill,           // PendingActionData.SkillIndex 记录具体技能编号

        // ── 远程攻击 ──────────────────────────────────────────────────────
        [InspectorName("Shoot")]
        Shoot,
        [InspectorName("ShootCharge")]
        ShootCharge,
    }
}
