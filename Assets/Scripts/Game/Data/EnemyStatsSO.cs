using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 敌人类型枚举；与 EnemyStatsDatabase 配合使用，旧版 EnemyStatsSO 已合并入 Database。
    /// </summary>
    public enum EnemyType
    {
        MeleeMinion  = 0,
        RangedMinion = 1,
        Elite        = 2,
        Guardian     = 3,
        Boss         = 4
    }

    /// <summary>
    /// 敌人数据模型：运行时属性，由敌人属性库 + 难度系数初始化；受伤扣血用 WithDamageApplied 更新。
    /// 每个敌人实例持有一份；攻击力/防御力用于伤害公式，无暴击无增伤。
    /// </summary>
    public struct EnemyRuntimeStats
    {
        public string    enemyId;
        public EnemyType type;
        public float maxHp;
        public float currentHp;
        public float attack;
        public float defense;
        public float moveSpeed;
        public int   goldMin;
        public int   goldMax;
        /// <summary>击杀后玩家获得的经验（难度不放大）</summary>
        public int   expReward;

        public readonly bool IsDead => currentHp <= 0f;

        /// <summary>扣血后返回新 struct，调用方需赋值：stats = stats.WithDamageApplied(amount);</summary>
        public readonly EnemyRuntimeStats WithDamageApplied(float amount) => new EnemyRuntimeStats
        {
            enemyId    = enemyId,
            type       = type,
            maxHp      = maxHp,
            currentHp  = Mathf.Max(0f, currentHp - amount),
            attack     = attack,
            defense    = defense,
            moveSpeed  = moveSpeed,
            goldMin    = goldMin,
            goldMax    = goldMax,
            expReward  = expReward,
        };
    }
}
