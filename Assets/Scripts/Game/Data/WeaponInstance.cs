using System;
using Game.Domain;

namespace Game.Data
{
    /// <summary>
    /// 属性范围 [min, max]，获得武器时在此范围内随机一个值。
    /// </summary>
    [Serializable]
    public struct FloatRange
    {
        public float min;
        public float max;

        public FloatRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        /// <summary>在范围内随机一个值（闭区间）</summary>
        public float RandomValue(System.Random rng)
        {
            if (min >= max) return min;
            return min + (float)rng.NextDouble() * (max - min);
        }

        public float Clamp(float value) => UnityEngine.Mathf.Clamp(value, min, max);
    }

    /// <summary>
    /// 武器实例：获得武器时按 WeaponDatabase 条目的范围随机生成后保存的数据，用于装备与存档。
    ///
    /// 所有 rolled 字段统一存储，低品质时高级字段保持为 0，
    /// ToModifier() 直接映射全部字段，无需按品质分支——干净且可扩展。
    /// </summary>
    [Serializable]
    public class WeaponInstance
    {
        /// <summary>对应 WeaponDatabase 条目的 weaponId，用于查找配置与显示</summary>
        public string weaponId;
        public WeaponType type;
        public WeaponRarity rarity;

        // 随机后的实际属性（低品质未开放的字段保持为 0）
        public float rolledHp;
        public float rolledMp;
        public float rolledAttack;
        public float rolledDefense;
        public float rolledHpRegen;
        public float rolledMpRegen;
        public float rolledCritRate;
        public float rolledCritDmg;
        public float rolledAttackSpeed;
        public float rolledMoveSpeed;

        /// <summary>
        /// 转为 StatModifier 直接挂到 Stats 上。
        /// 未使用的字段为 0，对属性计算无影响，无需分品质处理。
        /// </summary>
        public StatModifier ToModifier() => new StatModifier
        {
            hpAdd          = rolledHp,
            mpAdd          = rolledMp,
            attackAdd      = rolledAttack,
            defenseAdd     = rolledDefense,
            hpRegenAdd     = rolledHpRegen,
            mpRegenAdd     = rolledMpRegen,
            critRateAdd    = rolledCritRate,
            critDmgAdd     = rolledCritDmg,
            attackSpeedAdd = rolledAttackSpeed,
            moveSpeedAdd   = rolledMoveSpeed,
        };
    }
}
