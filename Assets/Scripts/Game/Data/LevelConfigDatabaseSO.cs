using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 单关配置：关卡编号、全局敌人生成方案库与方案索引、宝箱数量上下限、商店数量与布局参数。
    /// </summary>
    [Serializable]
    public class LevelConfigData
    {
        [Header("关卡编号（1～5）")]
        [InspectorLabel("关卡编号")]
        [Range(1, 5)]
        public int levelIndex = 1;

        [Header("敌人生成")]
        [InspectorLabel("全局敌人生成方案库")]
        [Tooltip("必填。全局敌人生成器会从这份方案库中选择一个列表项执行生成任务。")]
        public LevelEnemySpawnPlanSO enemyGlobalSpawnPlanLibrary;

        [InspectorLabel("全局方案列表项")]
        [Min(0)]
        [Tooltip("当前关卡使用的全局敌人生成方案列表项索引。")]
        public int enemyGlobalSpawnPlanIndex = 0;

        [Header("每格宝箱数量上下限")]
        [InspectorLabel("宝箱下限")] public int chestMinCount = 0;
        [InspectorLabel("宝箱上限")] public int chestMaxCount = 2;

        [Header("商店与格子")]
        [InspectorLabel("商店数量")]
        [Tooltip("固定数值，刷新位置随机")]
        public int shopCount = 2;
        [InspectorLabel("格子大小")] public float cellSize = 40f;

        /// <summary>随机本关宝箱数量（商店数量用 shopCount 固定）。</summary>
        public int RollChestCount(System.Random rng)
        {
            return RandomRange(rng, chestMinCount, chestMaxCount);
        }

        private static int RandomRange(System.Random rng, int min, int max)
        {
            if (min >= max) return min;
            return rng.Next(min, max + 1);
        }
    }

    /// <summary>
    /// 关卡配置库：列表每项对应一关（全局敌人生成方案、宝箱数量、商店数量、布局参数）。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/关卡配置库", fileName = "关卡配置库")]
    public class LevelConfigDatabaseSO : ScriptableObject
    {
        [InspectorLabel("关卡列表")]
        [Tooltip("关卡 1～5 的配置")]
        public List<LevelConfigData> levels = new List<LevelConfigData>();

        public LevelConfigData GetConfigForLevel(int levelIndex)
        {
            if (levels == null || levelIndex < 1) return null;
            int i = levelIndex - 1;
            if (i >= levels.Count) return null;
            return levels[i];
        }
    }
}
