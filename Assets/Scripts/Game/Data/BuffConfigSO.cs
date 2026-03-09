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
        [InspectorLabel("仅属性")] StatOnly,
        [InspectorLabel("全程霸体")] SuperArmor,
        [InspectorLabel("召唤分身")] SummonClone,
        [InspectorLabel("攻击重影")] Afterimage,
        [InspectorLabel("近战范围扩大")] MeleeRangeExpand,
        [InspectorLabel("多重射击")] Multishot,
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
        [InspectorLabel("近战范围倍数")] [Tooltip("近战范围扩大：如 2=扩大100%")] public float meleeRangeScale = 2f;
        [InspectorLabel("多重射击延迟(秒)")] [Tooltip("额外一发子弹的延迟")] public float multishotDelaySeconds = 0.2f;

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
            new BuffEntry { buffId = BuffIds.SuperArmor, type = BuffType.SuperArmor, displayName = "全程霸体", description = "全程霸体，不会被打断" },
            new BuffEntry { buffId = BuffIds.SummonClone, type = BuffType.SummonClone, displayName = "召唤分身", description = "召唤分身协助战斗" },
            new BuffEntry { buffId = BuffIds.Afterimage, type = BuffType.Afterimage, displayName = "攻击重影", description = "攻击附带重影，额外伤害" },
            new BuffEntry { buffId = BuffIds.MeleeRangeExpand, type = BuffType.MeleeRangeExpand, displayName = "近战范围扩大", description = "武器碰撞体、伤害检测与特效范围均扩大100%", meleeRangeScale = 2f },
            new BuffEntry { buffId = BuffIds.Multishot, type = BuffType.Multishot, displayName = "多重射击", description = "远程每次多发射一发子弹，间隔0.2秒", multishotDelaySeconds = 0.2f },
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
            if (e.HasAnyStatEffect()) return e.ToStatModifier();
            return new StatModifier();
        }

        /// <summary>近战范围扩大倍数（如 2 表示 100% 扩大）。无配置或非该类型返回 1。</summary>
        public float GetMeleeRangeScale(string buffId)
        {
            var e = GetEntry(buffId);
            return (e != null && e.type == BuffType.MeleeRangeExpand) ? e.meleeRangeScale : 1f;
        }

        /// <summary>多重射击额外一发的延迟（秒）。无配置或非该类型返回 0。</summary>
        public float GetMultishotDelaySeconds(string buffId)
        {
            var e = GetEntry(buffId);
            return (e != null && e.type == BuffType.Multishot) ? e.multishotDelaySeconds : 0f;
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
