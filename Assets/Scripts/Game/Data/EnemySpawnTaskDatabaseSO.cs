using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Data
{
    public enum EnemySpawnCategory
    {
        Minion = 0,
        Elite = 2,
        Guardian = 3,
        Boss = 4,
        Chest = 5,
        Shop = 6,
    }

    public enum EnemySpawnCountMode
    {
        Fixed = 0,
        RandomRange = 1,
    }

    /// <summary>
    /// 单条具体类型生成数据：描述一种生成逻辑。
    /// 只提供数据，不直接参与生成行为。
    /// </summary>
    [Serializable]
    public class EnemySpawnTaskDefinition
    {
        [FormerlySerializedAs("enemyType")]
        [InspectorLabel("生成类型")]
        public EnemySpawnCategory spawnType = EnemySpawnCategory.Minion;

        [FormerlySerializedAs("specificEnemyId")]
        [HideInInspector]
        public string specificSpawnId = string.Empty;

        [InspectorLabel("生成半径(米)")]
        [Min(1f)]
        public float spawnRadius = 12f;

        [InspectorLabel("数量模式")]
        public EnemySpawnCountMode countMode = EnemySpawnCountMode.RandomRange;

        [InspectorLabel("固定数量")]
        [Min(0)]
        public int fixedCount = 3;

        [InspectorLabel("随机数量下限")]
        [Min(0)]
        public int randomMinCount = 1;

        [InspectorLabel("随机数量上限")]
        [Min(0)]
        public int randomMaxCount = 4;

        [FormerlySerializedAs("enemyMinSpacing")]
        [InspectorLabel("对象最小间距(米)")]
        [Min(0.1f)]
        public float spawnMinSpacing = 3f;

        public bool UseMixedVariants => string.IsNullOrWhiteSpace(specificSpawnId);
        public bool IsMinionCategory => spawnType == EnemySpawnCategory.Minion;
        public bool IsGuardianCategory => spawnType == EnemySpawnCategory.Guardian;
        public bool IsEnemyCategory => spawnType is EnemySpawnCategory.Minion or EnemySpawnCategory.Elite or EnemySpawnCategory.Guardian or EnemySpawnCategory.Boss;

        public int ResolveSpawnCount(System.Random rng)
        {
            if (countMode == EnemySpawnCountMode.Fixed)
                return Mathf.Max(0, fixedCount);

            int min = Mathf.Max(0, randomMinCount);
            int max = Mathf.Max(min, randomMaxCount);
            if (min == max)
                return min;

            return rng.Next(min, max + 1);
        }
    }

    [CreateAssetMenu(menuName = "游戏/配置/生成任务库", fileName = "生成任务库")]
    public class EnemySpawnTaskDatabaseSO : ScriptableObject
    {
        [InspectorLabel("具体类型生成数据列表")]
        public List<EnemySpawnTaskDefinition> tasks = new List<EnemySpawnTaskDefinition>();

        public EnemySpawnTaskDefinition GetTaskAt(int index)
        {
            if (tasks == null || index < 0 || index >= tasks.Count)
                return null;

            return tasks[index];
        }
    }
}
