using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    public enum LocalEnemySpawnMode
    {
        SpawnOnce = 0,
        SpawnRepeatedly = 1,
    }

    public enum LocalEnemySpawnTaskTotalMode
    {
        FixedCount = 0,
        Infinite = 1,
    }

    [Serializable]
    public class LevelLocalEnemySpawnPlanDefinition
    {
        [InspectorLabel("列表项索引")]
        [Min(0)]
        public int taskIndex = 0;

        [InspectorLabel("生成模式")]
        public LocalEnemySpawnMode spawnMode = LocalEnemySpawnMode.SpawnOnce;

        [InspectorLabel("任务总数模式")]
        public LocalEnemySpawnTaskTotalMode taskTotalMode = LocalEnemySpawnTaskTotalMode.FixedCount;

        [InspectorLabel("生成任务总数")]
        [Min(1)]
        public int totalTaskCount = 1;

        [InspectorLabel("任务间隔(秒)")]
        [Min(0.1f)]
        public float taskInterval = 8f;
    }

    [CreateAssetMenu(menuName = "游戏/配置/关卡局部生成方案库", fileName = "关卡局部生成方案库")]
    public class LevelLocalEnemySpawnPlanSO : ScriptableObject
    {
        [InspectorLabel("任务库")]
        public EnemySpawnTaskDatabaseSO taskDatabase;

        [InspectorLabel("局部方案列表")]
        public List<LevelLocalEnemySpawnPlanDefinition> plans = new List<LevelLocalEnemySpawnPlanDefinition>();

        public LevelLocalEnemySpawnPlanDefinition GetPlanAt(int index)
        {
            if (plans == null || index < 0 || index >= plans.Count)
                return null;

            return plans[index];
        }
    }
}
