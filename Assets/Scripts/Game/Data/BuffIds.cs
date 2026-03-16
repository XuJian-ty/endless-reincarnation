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
        /// <summary>霸体加减伤：受击不进入硬直，并提供减伤。接入：PlayerController.OnHit 与 PlayerModel.Stats.DamageReduce。</summary>
        public const string SuperArmor      = "buff_superarmor";
        /// <summary>召唤分身。接入：关卡/战斗逻辑，生成友方单位。</summary>
        public const string SummonClone     = "buff_summon";
        /// <summary>攻击附带重影：延迟重复一次伤害、特效与音效时间轴事件。接入：玩家技能时间轴启动。</summary>
        public const string Afterimage      = "buff_afterimage";
        /// <summary>伤害范围扩大：特效、伤害范围、碰撞体和射线距离 × 配置倍率。接入：伤害检测与技能特效。</summary>
        public const string DamageRangeExpand = "buff_meleerange";
    }
}
