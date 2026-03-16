using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 掉落条目：物品类型 + 权重（归属由所在关卡列表项决定）。
    /// itemType 约定：weapon_common / weapon_rare / weapon_epic / weapon_legendary，potion_hp / potion_mp，nectar。
    /// </summary>
    [Serializable]
    public class DropEntry
    {
        [InspectorLabel("物品类型")]
        public string itemType;
        [InspectorLabel("权重")]
        [Tooltip("权重，数字越大越容易出")]
        public float weight;
    }

    /// <summary>
    /// 某一关的精英怪掉落数据：一个列表项 = 该关的掉落表（内层列表为 itemType+weight）。
    /// </summary>
    [Serializable]
    public class DropTableByLevel
    {
        [InspectorLabel("关卡编号")]
        [Tooltip("从 1 开始的关卡编号，可继续扩展更多关卡。")]
        [Min(1)]
        public int levelIndex = 1;
        [InspectorLabel("掉落条目")]
        public List<DropEntry> entries = new List<DropEntry>();

        private float _totalWeight = -1f;
        private float TotalWeight
        {
            get
            {
                if (_totalWeight < 0f)
                {
                    _totalWeight = 0f;
                    if (entries != null)
                        foreach (var e in entries) _totalWeight += Mathf.Max(0f, e.weight);
                }
                return _totalWeight;
            }
        }

        /// <summary>按权重随机一条 itemType；无有效条目返回 null</summary>
        public string Roll(System.Random rng)
        {
            float total = TotalWeight;
            if (total <= 0f || entries == null || entries.Count == 0) return null;
            float roll = (float)rng.NextDouble() * total;
            float cumulative = 0f;
            foreach (var e in entries)
            {
                cumulative += Mathf.Max(0f, e.weight);
                if (roll <= cumulative) return e.itemType;
            }
            return entries[entries.Count - 1].itemType;
        }
    }

    /// <summary>
    /// 掉落表库：外层列表每项对应一关的精英怪掉落数据，每项内层列表为该关的 itemType+weight。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/掉落表库", fileName = "掉落表库")]
    public class DropTableDatabaseSO : ScriptableObject
    {
        [InspectorLabel("关卡掉落表")]
        [Tooltip("按关卡编号组织的掉落表列表，可继续扩展更多关卡。")]
        public List<DropTableByLevel> levelTables = new List<DropTableByLevel>();

        /// <summary>根据关卡编号取掉落表；未找到返回 null</summary>
        public DropTableByLevel GetTableForLevel(int levelIndex)
        {
            if (levelTables == null || levelIndex < 1) return null;
            foreach (var t in levelTables)
                if (t.levelIndex == levelIndex) return t;
            int i = levelIndex - 1;
            if (i >= 0 && i < levelTables.Count) return levelTables[i];
            return null;
        }

        /// <summary>按关卡随机一条掉落类型；无表或无条目返回 null</summary>
        public string Roll(int levelIndex, System.Random rng)
        {
            var table = GetTableForLevel(levelIndex);
            return table?.Roll(rng);
        }
    }
}
