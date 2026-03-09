using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Domain;

namespace Game.Data
{
    /// <summary>
    /// 单级成长数据（1～10 级，每级 6 项已按需求书比例预计算，不再配置成长比例）。
    /// </summary>
    [Serializable]
    public class LevelGrowthEntry
    {
        [InspectorLabel("等级")]
        [Range(1, 10)]
        public int level = 1;

        [InspectorLabel("生命上限")]
        public float baseHp = 100f;
        [InspectorLabel("法力上限")]
        public float baseMp = 100f;
        [InspectorLabel("经验上限")]
        [Tooltip("升到下一级所需经验，用于 ExpModel 配置")]
        public int expToNext = 120;
        [InspectorLabel("攻击力")]
        public float baseAttack = 20f;
        [InspectorLabel("防御力")]
        public float baseDefense = 100f;
        [InspectorLabel("生命回复/秒")]
        public float baseHpRegen = 1f;
        [InspectorLabel("法力回复/秒")]
        public float baseMpRegen = 1f;
    }

    /// <summary>
    /// 玩家成长属性库：1～10 级成长配置，仅在玩家升级时用来更新玩家数据模型（Stats 基础值）。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/玩家成长属性库", fileName = "玩家成长属性库")]
    public class LevelGrowthSO : ScriptableObject
    {
        [InspectorLabel("等级列表")]
        [Tooltip("1～10 级各自的生命/法力/攻击/防御/生命回复/法力回复，已按每级×1.1 预计算")]
        public List<LevelGrowthEntry> levels = new List<LevelGrowthEntry>();

        /// <summary>根据等级填充 stats 的基础值；若列表无该等级则用第 1 级或默认。</summary>
        public void GetStatsForLevel(int level, Stats stats)
        {
            level = Mathf.Clamp(level, 1, 10);
            LevelGrowthEntry entry = null;
            if (levels != null)
            {
                foreach (var e in levels)
                    if (e.level == level) { entry = e; break; }
                if (entry == null && levels.Count > 0)
                    entry = levels[0];
            }

            if (entry != null)
            {
                stats.baseHp = entry.baseHp;
                stats.baseMp = entry.baseMp;
                stats.baseAttack = entry.baseAttack;
                stats.baseDefense = entry.baseDefense;
                stats.baseHpRegen = entry.baseHpRegen;
                stats.baseMpRegen = entry.baseMpRegen;
            }
            else
            {
                stats.baseHp = 100f;
                stats.baseMp = 100f;
                stats.baseAttack = 20f;
                stats.baseDefense = 100f;
                stats.baseHpRegen = 1f;
                stats.baseMpRegen = 1f;
            }

            stats.baseLifeSteal = 0f;
            stats.baseCritRate = 0.05f;
            stats.baseCritDmg = 1f;
            stats.baseAttackSpeed = 1f;
            stats.baseMoveSpeed = 6f;
            stats.baseDamageBonus = 0f;
            stats.InvalidateCache();
        }

        /// <summary>当前等级升到下一级所需经验；若配置无该等级则用公式 80+level*40。</summary>
        public int GetExpToNextForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, 10);
            if (levels != null)
            {
                foreach (var e in levels)
                    if (e.level == level) return e.expToNext;
                if (levels.Count > 0 && levels[0].level == level) return levels[0].expToNext;
            }
            return ExpModel.GetExpRequiredForLevel(level);
        }
    }
}
