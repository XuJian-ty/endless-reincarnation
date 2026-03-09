using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 单关配置：每格 3 种怪物与宝箱的数量上下限、商店数量（固定）、格子大小。
    /// </summary>
    [Serializable]
    public class LevelConfigData
    {
        [Header("关卡编号（1～5）")]
        [InspectorLabel("关卡编号")]
        [Range(1, 5)]
        public int levelIndex = 1;

        [Header("守卫者（数量固定，位置随机）")]
        [InspectorLabel("守卫者数量")]
        public int guardianCount = 3;

        [Header("每格怪物数量上下限（近战/远程/精英）")]
        [InspectorLabel("近战下限")] public int meleeMinCount = 0;
        [InspectorLabel("近战上限")] public int meleeMaxCount  = 4;
        [InspectorLabel("远程下限")] public int rangedMinCount = 0;
        [InspectorLabel("远程上限")] public int rangedMaxCount = 2;
        [InspectorLabel("精英下限")] public int eliteMinCount  = 0;
        [InspectorLabel("精英上限")] public int eliteMaxCount  = 1;

        [Header("每格宝箱数量上下限")]
        [InspectorLabel("宝箱下限")] public int chestMinCount = 0;
        [InspectorLabel("宝箱上限")] public int chestMaxCount = 2;

        [Header("商店与格子")]
        [InspectorLabel("商店数量")]
        [Tooltip("固定数值，刷新位置随机")]
        public int shopCount = 2;
        [InspectorLabel("格子大小")] public float cellSize = 40f;

        /// <summary>随机本格怪物与宝箱数量（商店数量用 shopCount 固定）</summary>
        public void RollCounts(System.Random rng, out int melee, out int ranged, out int elite, out int chests)
        {
            melee  = RandomRange(rng, meleeMinCount, meleeMaxCount);
            ranged = RandomRange(rng, rangedMinCount, rangedMaxCount);
            elite  = RandomRange(rng, eliteMinCount, eliteMaxCount);
            chests = RandomRange(rng, chestMinCount, chestMaxCount);
        }

        private static int RandomRange(System.Random rng, int min, int max)
        {
            if (min >= max) return min;
            return rng.Next(min, max + 1);
        }
    }

    /// <summary>
    /// 关卡配置库：列表每项对应一关（每格怪物/宝箱上下限、商店数量、格子大小）。
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
