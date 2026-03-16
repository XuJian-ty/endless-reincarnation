using System;
using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.GameFlow
{
    /// <summary>
    /// 单次实际生成任务。由方案配置解析而来，运行时按顺序执行。
    /// </summary>
    public sealed class LevelEnemySpawnTaskRequest
    {
        public LevelEnemySpawnTaskRequest(int taskIndex, EnemySpawnTaskDefinition definition, int spawnCount)
        {
            TaskIndex = taskIndex;
            Definition = definition;
            SpawnCount = Mathf.Max(0, spawnCount);
        }

        public int TaskIndex { get; }
        public EnemySpawnTaskDefinition Definition { get; }
        public int SpawnCount { get; }
        public int RemainingRetries { get; set; } = 2;
    }

    /// <summary>
    /// 将关卡敌人生成方案解析为运行时任务队列。
    /// 仅负责规划，不直接做场景生成。
    /// </summary>
    public static class LevelEnemySpawnPlanner
    {
        public static List<LevelEnemySpawnTaskRequest> BuildTaskQueue(
            LevelEnemyGlobalSpawnPlanDefinition plan,
            EnemySpawnTaskDatabaseSO taskDatabase,
            System.Random rng)
        {
            var requests = new List<LevelEnemySpawnTaskRequest>();
            if (plan == null || taskDatabase == null || plan.taskAssignments == null || plan.taskAssignments.Count == 0)
                return requests;

            BuildQueue(plan, taskDatabase, rng, requests);
            Shuffle(requests, rng);
            return requests;
        }

        public static int CountPlannedGuardianSpawns(
            LevelEnemyGlobalSpawnPlanDefinition plan,
            EnemySpawnTaskDatabaseSO taskDatabase,
            System.Random rng)
        {
            if (plan == null || taskDatabase == null || plan.taskAssignments == null || plan.taskAssignments.Count == 0)
                return 0;

            int total = 0;
            for (int i = 0; i < plan.taskAssignments.Count; i++)
            {
                GlobalEnemySpawnTaskAssignment assignment = plan.taskAssignments[i];
                if (assignment == null || assignment.taskCount <= 0)
                    continue;

                EnemySpawnTaskDefinition definition = taskDatabase.GetTaskAt(assignment.taskIndex);
                if (definition == null)
                    continue;

                if (definition.enemyType != EnemySpawnCategory.Guardian)
                    continue;

                int taskCount = Mathf.Max(0, assignment.taskCount);
                for (int taskIndex = 0; taskIndex < taskCount; taskIndex++)
                    total += definition.ResolveSpawnCount(rng);
            }

            return total;
        }

        private static void BuildQueue(
            LevelEnemyGlobalSpawnPlanDefinition plan,
            EnemySpawnTaskDatabaseSO taskDatabase,
            System.Random rng,
            List<LevelEnemySpawnTaskRequest> requests)
        {
            for (int i = 0; i < plan.taskAssignments.Count; i++)
            {
                GlobalEnemySpawnTaskAssignment assignment = plan.taskAssignments[i];
                if (assignment == null || assignment.taskCount <= 0)
                    continue;

                EnemySpawnTaskDefinition definition = taskDatabase.GetTaskAt(assignment.taskIndex);
                if (definition == null)
                    continue;

                int taskCount = Mathf.Max(0, assignment.taskCount);
                for (int taskIndex = 0; taskIndex < taskCount; taskIndex++)
                {
                    requests.Add(new LevelEnemySpawnTaskRequest(
                        assignment.taskIndex,
                        definition,
                        definition.ResolveSpawnCount(rng)));
                }
            }
        }

        private static void Shuffle(List<LevelEnemySpawnTaskRequest> requests, System.Random rng)
        {
            for (int i = requests.Count - 1; i > 0; i--)
            {
                int swapIndex = rng.Next(0, i + 1);
                (requests[i], requests[swapIndex]) = (requests[swapIndex], requests[i]);
            }
        }
    }
}
