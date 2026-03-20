namespace Game.Presentation
{
    /// <summary>
    /// 所有可请求的玩家离散动作。
    /// 由 PlayerStateMachine 将 PlayerInputData 映射为此枚举，
    /// 再通过各状态的 GetPolicyFor() 决定立即执行、缓存还是忽略。
    /// </summary>
    public enum GameAction
    {
        None,

        // ── 移动 ──────────────────────────────────────────────────────────
        Walk,
        Run,
        Jump,

        // ── 地面攻击 ──────────────────────────────────────────────────────
        NormalAttack,    // AttackTap（Tap Interaction）在地面上下文触发
        ChargeStart,     // Hold Interaction 达到 0.2s 触发
        ChargeRelease,   // SlowTap Interaction 达到 0.7s 后松开触发

        // ── 空中攻击 ──────────────────────────────────────────────────────
        AirAttack,       // AttackTap 在空中上下文触发（与 NormalAttack 同按键，上下文区分）
        FallAttack,      // AltAttack（RMB）在空中触发

        // ── 防御 ──────────────────────────────────────────────────────────
        Dodge,

        // ── 技能 ──────────────────────────────────────────────────────────
        Skill,           // PendingActionData.SkillIndex 记录具体技能编号
    }
}
