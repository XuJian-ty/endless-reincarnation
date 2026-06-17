using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    public enum GlobalEnemySpawnMode
    {
        SpawnAllAtOnce = 0,
        SpawnOverTime = 1,
    }

    [Serializable]
    public class GlobalEnemySpawnTaskAssignment
    {
        [InspectorLabel("列表项索引")]
        [Min(0)]
        public int taskIndex = 0;

        [InspectorLabel("生成任务数量")]
        [Min(0)]
        [Tooltip("该列表项总共需要完成多少次生成任务。")]
        public int taskCount = 1;
    }

    [Serializable]
    public class LevelEnemyGlobalSpawnPlanDefinition
    {
        [InspectorLabel("任务分配列表")]
        public List<GlobalEnemySpawnTaskAssignment> taskAssignments = new List<GlobalEnemySpawnTaskAssignment>();

        [InspectorLabel("生成模式")]
        public GlobalEnemySpawnMode spawnMode = GlobalEnemySpawnMode.SpawnAllAtOnce;

        [InspectorLabel("任务圆心最小间距(米)")]
        [Min(0.1f)]
        public float taskCenterMinSpacing = 16f;

        [InspectorLabel("初始任务数量")]
        [Min(0)]
        [Tooltip("仅在随时间逐步生成模式下生效。开局立即执行的任务数量。")]
        public int initialTaskCount = 3;

        [InspectorLabel("任务间隔(秒)")]
        [Min(0.1f)]
        [Tooltip("仅在随时间逐步生成模式下生效。每隔多久执行一次新的任务。")]
        public float taskInterval = 8f;
    }

    [CreateAssetMenu(menuName = "游戏/配置/关卡全局生成方案库", fileName = "关卡全局生成方案库")]
    public class LevelEnemySpawnPlanSO : ScriptableObject
    {
        [InspectorLabel("任务库")]
        public EnemySpawnTaskDatabaseSO taskDatabase;

        [InspectorLabel("全局方案列表")]
        public List<LevelEnemyGlobalSpawnPlanDefinition> plans = new List<LevelEnemyGlobalSpawnPlanDefinition>();

        public LevelEnemyGlobalSpawnPlanDefinition GetPlanAt(int index)
        {
            if (plans == null || index < 0 || index >= plans.Count)
                return null;

            return plans[index];
        }
    }
}
