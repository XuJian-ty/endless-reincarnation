using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Data
{
    /// <summary>
    /// 单关配置：关卡编号、全局生成方案库与方案索引、宝箱数量上下限、商店数量与布局参数。
    /// </summary>
    [Serializable]
    public class LevelConfigData
    {
        [Header("关卡编号（从 1 开始）")]
        [InspectorLabel("关卡编号")]
        [Min(1)]
        public int levelIndex = 1;

        [Header("生成方案")]
        [InspectorLabel("关卡场景名")]
        [Tooltip("运行时用于加载本关卡的场景名。")]
        [HideInInspector]
        public string sceneName = "";

        [HideInInspector]
        public string sceneGuid = "";

        [FormerlySerializedAs("enemyGlobalSpawnPlanLibrary")]
        [InspectorLabel("全局生成方案库")]
        [Tooltip("必填。全局生成器会从这份方案库中选择一个列表项执行生成任务。")]
        public LevelEnemySpawnPlanSO globalSpawnPlanLibrary;

        [FormerlySerializedAs("enemyGlobalSpawnPlanIndex")]
        [InspectorLabel("全局方案列表项")]
        [Min(0)]
        [Tooltip("当前关卡使用的全局生成方案列表项索引。")]
        public int globalSpawnPlanIndex = 0;

        [Header("每格宝箱数量上下限")]
        [InspectorLabel("宝箱下限")] public int chestMinCount = 0;
        [InspectorLabel("宝箱上限")] public int chestMaxCount = 2;

        [Header("商店与格子")]
        [InspectorLabel("商店数量")]
        [Tooltip("固定数值，刷新位置随机")]
        public int shopCount = 2;
        [InspectorLabel("格子大小")] public float cellSize = 40f;

        [Header("精英掉落")]
        [InspectorLabel("精英掉落条目")]
        [Tooltip("本关精英怪掉落表。物品类型和权重直接配置在这里。")]
        public List<DropEntry> eliteDropEntries = new List<DropEntry>();

        [Header("守卫者掉落")]
        [InspectorLabel("守卫者掉落条目")]
        [Tooltip("本关守卫者掉落表。物品类型和权重直接配置在这里。")]
        public List<DropEntry> guardianDropEntries = new List<DropEntry>();

        [Header("Boss掉落")]
        [InspectorLabel("Boss掉落条目")]
        [Tooltip("本关Boss掉落表。物品类型和权重直接配置在这里。")]
        public List<DropEntry> bossDropEntries = new List<DropEntry>();

        /// <summary>随机本关宝箱数量（商店数量用 shopCount 固定）。</summary>
        public int RollChestCount(System.Random rng)
        {
            return RandomRange(rng, chestMinCount, chestMaxCount);
        }

        public string RollEliteDrop(System.Random rng)
        {
            return RollDrop(eliteDropEntries, rng);
        }

        public string RollGuardianDrop(System.Random rng)
        {
            return RollDrop(guardianDropEntries, rng);
        }

        public string RollBossDrop(System.Random rng)
        {
            return RollDrop(bossDropEntries, rng);
        }

        private static int RandomRange(System.Random rng, int min, int max)
        {
            if (min >= max) return min;
            return rng.Next(min, max + 1);
        }

        private static string RollDrop(List<DropEntry> dropEntries, System.Random rng)
        {
            if (dropEntries == null || dropEntries.Count == 0 || rng == null)
                return null;

            float totalWeight = 0f;
            for (int i = 0; i < dropEntries.Count; i++)
            {
                DropEntry entry = dropEntries[i];
                if (entry == null)
                    continue;

                totalWeight += Mathf.Max(0f, entry.weight);
            }

            if (totalWeight <= 0f)
                return null;

            float roll = (float)rng.NextDouble() * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < dropEntries.Count; i++)
            {
                DropEntry entry = dropEntries[i];
                if (entry == null)
                    continue;

                cumulative += Mathf.Max(0f, entry.weight);
                if (roll <= cumulative)
                    return entry.itemType;
            }

            return dropEntries[dropEntries.Count - 1]?.itemType;
        }
    }

    /// <summary>
    /// 关卡配置库：列表每项对应一关（全局生成方案、宝箱数量、商店数量、布局参数）。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/关卡配置库", fileName = "关卡配置库")]
    public class LevelConfigDatabaseSO : ScriptableObject
    {
        [InspectorLabel("关卡列表")]
        [Tooltip("按关卡编号顺序排列的配置列表，可继续扩展更多关卡。")]
        public List<LevelConfigData> levels = new List<LevelConfigData>();

        public LevelConfigData GetConfigForLevel(int levelIndex)
        {
            if (levels == null || levelIndex < 1) return null;
            int i = levelIndex - 1;
            if (i >= levels.Count) return null;
            return levels[i];
        }

        public string GetSceneNameForLevel(int levelIndex)
        {
            LevelConfigData config = GetConfigForLevel(levelIndex);
            return config != null && !string.IsNullOrWhiteSpace(config.sceneName)
                ? config.sceneName.Trim()
                : string.Empty;
        }
    }
}
