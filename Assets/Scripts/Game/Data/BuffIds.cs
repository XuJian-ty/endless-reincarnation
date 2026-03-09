namespace Game.Data
{
    /// <summary>
    /// Buff 唯一 ID 常量，供 HasBuff(buffId)、配置与文档统一引用，避免魔法字符串。
    /// 特殊效果接入位置见 Buff配置接入说明.md。
    /// </summary>
    public static class BuffIds
    {
        // ── 属性类 ───────────────────────────────────────────────────────────
        public const string Lifesteal       = "buff_lifesteal";
        public const string CritRate       = "buff_critrate";
        public const string CritDmg         = "buff_critdmg";
        public const string AtkSpeed       = "buff_atkspeed";
        public const string MoveSpeed       = "buff_movespeed";
        public const string HpRegen         = "buff_hpregen";
        public const string MpRegen         = "buff_mpregen";
        public const string Damage          = "buff_damage";

        // ── 特殊效果类（逻辑在各自系统内根据 HasBuff(id) 实现）────────────────
        /// <summary>全程霸体：受击不进入硬直。接入：PlayerController.OnHit。</summary>
        public const string SuperArmor      = "buff_superarmor";
        /// <summary>召唤分身。接入：关卡/战斗逻辑，生成友方单位。</summary>
        public const string SummonClone     = "buff_summon";
        /// <summary>攻击重影：攻击附带重影与额外伤害。接入：攻击/伤害检测或特效逻辑。</summary>
        public const string Afterimage      = "buff_afterimage";
        /// <summary>近战范围扩大：碰撞体、伤害范围、特效范围 × 配置 meleeRangeScale。接入：武器/伤害检测。</summary>
        public const string MeleeRangeExpand = "buff_meleerange";
        /// <summary>多重射击：远程多发射一发，间隔由配置 multishotDelaySeconds。接入：远程射击逻辑。</summary>
        public const string Multishot      = "buff_multishot";
    }
}
