using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Domain;

namespace Game.Data
{
    /// <summary>
    /// Buff 类型：纯属性加成 或 特殊效果（逻辑在各自系统里根据 HasBuff(buffId) 判断）。
    /// 特殊效果说明见 Buff配置接入说明.md 或 BuffConfigSO 注释。
    /// </summary>
    public enum BuffType
    {
        [InspectorName("属性加成")] StatOnly,
        [InspectorName("霸体加减伤")] SuperArmorDamageReduce,
        [InspectorName("召唤分身")] SummonClone,
        [InspectorName("攻击附带重影")] Afterimage,
        [InspectorName("伤害范围扩大")] DamageRangeExpand,
    }

    [Serializable]
    public struct SummonCloneBuffSettings
    {
        public float followRadius;
        public float opacity;
    }

    /// <summary>
    /// 单条 Buff：显示 + 类型 + 属性加成（可选）+ 特殊效果参数（可选）。
    /// 属性类用 stat 字段生成 StatModifier；特殊类只在配置里标类型与参数，实际逻辑在战斗/武器/射击等代码里查 HasBuff(buffId) 实现。
    /// </summary>
    [Serializable]
    public class BuffEntry
    {
        [Header("显示")]
        [InspectorLabel("Buff ID")] [Tooltip("全局唯一，用于选择、存档与 HasBuff(buffId) 查询")]
        public string buffId = "";
        [InspectorLabel("显示名")] public string displayName = "";
        [InspectorLabel("描述")] [TextArea(1, 3)] public string description = "";

        [Header("类型与特殊参数")]
        [InspectorLabel("类型")] public BuffType type = BuffType.StatOnly;
        [InspectorLabel("重影间隔时间(秒)")] [Tooltip("攻击附带重影：原事件与重影事件之间的时间间隔。")] public float afterimageDelaySeconds = 0.2f;
        [InspectorLabel("重影伤害倍率")] [Tooltip("攻击附带重影：重影伤害事件的伤害倍率会再乘上该倍率。0.5 表示只有原伤害倍率的一半。")] [Min(0f)] public float afterimageDamageMultiplier = 0.5f;
        [InspectorLabel("减伤")] [Tooltip("霸体加减伤：会进入（1+增伤-减伤）中的减伤乘区。")] [Range(0f, 1f)] public float damageReduceAdd = 0.3f;
        [InspectorLabel("扩大倍数")] [Tooltip("伤害范围扩大：特效缩放、伤害范围、射线距离、碰撞体尺寸都会乘上该倍数。")] [Min(1f)] public float damageRangeScale = 2f;
        [InspectorLabel("分身跟随半径")] [Tooltip("召唤分身：分身围绕玩家跟随时的环绕半径。")] [Min(0.1f)] public float cloneFollowRadius = 2f;
        [InspectorLabel("分身透明度")] [Tooltip("召唤分身：0 表示完全透明，1 表示不透明。")] [Range(0.05f, 1f)] public float cloneOpacity = 0.45f;

        [Header("属性加成（类型=仅属性 或 同时有特殊效果时生效，0 表示不加）")]
        [InspectorLabel("生命加算")] public float hpAdd;
        [InspectorLabel("法力加算")] public float mpAdd;
        [InspectorLabel("攻击加算")] public float attackAdd;
        [InspectorLabel("防御加算")] public float defenseAdd;
        [InspectorLabel("吸血")] [Tooltip("0~1，如 0.3=30%")] public float lifeStealAdd;
        [InspectorLabel("暴击率")] [Tooltip("0~1，如 0.5=50%")] public float critRateAdd;
        [InspectorLabel("暴击伤害")] [Tooltip("倍数，如 1=+100%")] public float critDmgAdd;
        [InspectorLabel("攻速加成")] public float attackSpeedAdd;
        [InspectorLabel("移速加成")] public float moveSpeedAdd;
        [InspectorLabel("生命回复加算")] public float hpRegenAdd;
        [InspectorLabel("法力回复加算")] public float mpRegenAdd;
        [InspectorLabel("增伤比例")] [Tooltip("如 0.5=+50%")] public float damageBonusAdd;

        public StatModifier ToStatModifier()
        {
            return new StatModifier
            {
                hpAdd = hpAdd, mpAdd = mpAdd, attackAdd = attackAdd, defenseAdd = defenseAdd,
                lifeStealAdd = lifeStealAdd, critRateAdd = critRateAdd, critDmgAdd = critDmgAdd,
                attackSpeedAdd = attackSpeedAdd, moveSpeedAdd = moveSpeedAdd,
                hpRegenAdd = hpRegenAdd, mpRegenAdd = mpRegenAdd, damageBonusAdd = damageBonusAdd
            };
        }

        public bool HasAnyStatEffect()
        {
            return hpAdd != 0f || mpAdd != 0f || attackAdd != 0f || defenseAdd != 0f
                || lifeStealAdd != 0f || critRateAdd != 0f || critDmgAdd != 0f
                || attackSpeedAdd != 0f || moveSpeedAdd != 0f || hpRegenAdd != 0f || mpRegenAdd != 0f || damageBonusAdd != 0f;
        }
    }

    /// <summary>
    /// Buff 配置：选择面板的显示名/描述 + 效果数值。候选池与效果均由此配置驱动，新增 Buff 只需在此加一条。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/Buff配置", fileName = "Buff配置")]
    public class BuffConfigSO : ScriptableObject
    {
        [InspectorLabel("Buff 列表")]
        public List<BuffEntry> entries = new List<BuffEntry>
        {
            new BuffEntry { buffId = BuffIds.Lifesteal, type = BuffType.StatOnly, displayName = "吸血提升", description = "吸血比例提升30%，攻击回复生命", lifeStealAdd = 0.3f },
            new BuffEntry { buffId = BuffIds.CritRate, type = BuffType.StatOnly, displayName = "暴击率提升", description = "暴击率提升50%，更容易打出暴击", critRateAdd = 0.5f },
            new BuffEntry { buffId = BuffIds.CritDmg, type = BuffType.StatOnly, displayName = "暴击伤害提升", description = "暴击伤害提升100%，暴击更痛", critDmgAdd = 1f },
            new BuffEntry { buffId = BuffIds.AtkSpeed, type = BuffType.StatOnly, displayName = "攻速提升", description = "攻击速度提升50%，攻击更快", attackSpeedAdd = 0.5f },
            new BuffEntry { buffId = BuffIds.MoveSpeed, type = BuffType.StatOnly, displayName = "移速提升", description = "移动速度提升50%，跑得更快", moveSpeedAdd = 0.5f },
            new BuffEntry { buffId = BuffIds.HpRegen, type = BuffType.StatOnly, displayName = "生命回复提升", description = "生命回复速度+300%，快速回血", hpRegenAdd = 3f },
            new BuffEntry { buffId = BuffIds.MpRegen, type = BuffType.StatOnly, displayName = "法力回复提升", description = "法力回复速度+300%，快速回蓝", mpRegenAdd = 3f },
            new BuffEntry { buffId = BuffIds.Damage, type = BuffType.StatOnly, displayName = "伤害提升", description = "伤害提升50%，打得更痛", damageBonusAdd = 0.5f },
            new BuffEntry { buffId = BuffIds.SuperArmor, type = BuffType.SuperArmorDamageReduce, displayName = "霸体加减伤", description = "全程霸体，且提供减伤", damageReduceAdd = 0.3f },
            new BuffEntry { buffId = BuffIds.SummonClone, type = BuffType.SummonClone, displayName = "召唤分身", description = "召唤一个半透明分身跟随玩家，并由分身AI协助战斗", cloneFollowRadius = 2f, cloneOpacity = 0.45f },
            new BuffEntry { buffId = BuffIds.Afterimage, type = BuffType.Afterimage, displayName = "攻击附带重影", description = "玩家攻击会延迟再次触发一份同源的伤害、特效与音效事件", afterimageDelaySeconds = 0.2f, afterimageDamageMultiplier = 0.5f },
            new BuffEntry { buffId = BuffIds.DamageRangeExpand, type = BuffType.DamageRangeExpand, displayName = "伤害范围扩大", description = "特效、命中特效、伤害范围、碰撞体与射线距离均按倍数扩大", damageRangeScale = 2f },
        };

        public BuffEntry GetEntry(string buffId)
        {
            if (entries == null || string.IsNullOrEmpty(buffId)) return null;
            foreach (var e in entries)
                if (e.buffId == buffId) return e;
            return null;
        }

        public string GetDisplayName(string buffId) => GetEntry(buffId)?.displayName ?? buffId;
        public string GetDescription(string buffId) => GetEntry(buffId)?.description ?? "";

        /// <summary>根据 buffId 从配置生成 StatModifier；仅属性类或带属性加成的条目会产出非零 modifier，纯特殊效果返回全 0。</summary>
        public StatModifier GetModifierForBuff(string buffId)
        {
            var e = GetEntry(buffId);
            if (e == null) return new StatModifier();
            if (e.type == BuffType.SuperArmorDamageReduce)
            {
                return new StatModifier
                {
                    damageReduceAdd = Mathf.Clamp01(e.damageReduceAdd)
                };
            }

            if (e.type == BuffType.StatOnly && e.HasAnyStatEffect())
                return e.ToStatModifier();
            return new StatModifier();
        }

        /// <summary>伤害范围扩大倍数（如 2 表示扩大 100%）。无配置或非该类型返回 1。</summary>
        public float GetDamageRangeScale(string buffId)
        {
            var e = GetEntry(buffId);
            return (e != null && e.type == BuffType.DamageRangeExpand) ? Mathf.Max(1f, e.damageRangeScale) : 1f;
        }

        /// <summary>攻击附带重影的延迟（秒）。无配置或非该类型返回 0。</summary>
        public float GetAfterimageDelaySeconds(string buffId)
        {
            var e = GetEntry(buffId);
            return (e != null && e.type == BuffType.Afterimage) ? Mathf.Max(0f, e.afterimageDelaySeconds) : 0f;
        }

        public float GetAfterimageDamageMultiplier(string buffId)
        {
            var e = GetEntry(buffId);
            return (e != null && e.type == BuffType.Afterimage) ? Mathf.Max(0f, e.afterimageDamageMultiplier) : 1f;
        }

        public SummonCloneBuffSettings GetSummonCloneSettings(string buffId)
        {
            var e = GetEntry(buffId);
            if (e == null || e.type != BuffType.SummonClone)
            {
                return new SummonCloneBuffSettings
                {
                    followRadius = 2f,
                    opacity = 0.45f
                };
            }

            return new SummonCloneBuffSettings
            {
                followRadius = Mathf.Max(0.1f, e.cloneFollowRadius),
                opacity = Mathf.Clamp01(e.cloneOpacity)
            };
        }

        /// <summary>所有可被抽到的 Buff ID 列表（用于关卡开始时三选一候选池）。</summary>
        public List<string> GetAllBuffIds()
        {
            var list = new List<string>();
            if (entries == null) return list;
            foreach (var e in entries)
                if (!string.IsNullOrEmpty(e.buffId)) list.Add(e.buffId);
            return list;
        }
    }
}
