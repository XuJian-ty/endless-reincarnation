using System;
using System.Collections.Generic;
using Game.Data;
using Game.Presentation;
using UnityEngine;
using UnityEngine.AI;

namespace Game.GameFlow
{
    /// <summary>
    /// 敌人变体缓存：按敌人类型收集可用的敌人ID，供生成器在运行时复用。
    /// </summary>
    public sealed class EnemySpawnVariantCatalog
    {
        public readonly List<string> EliteIds = new List<string>();
        public readonly List<string> GuardianIds = new List<string>();
        public readonly List<string> BossIds = new List<string>();

        public List<string> GetVariantIds(EnemyType type)
        {
            return type switch
            {
                EnemyType.Elite => EliteIds,
                EnemyType.Guardian => GuardianIds,
                EnemyType.Boss => BossIds,
                _ => null,
            };
        }
    }

    /// <summary>
    /// 共享敌人生成执行逻辑：负责候选敌人解析、任务内散布、最小间距校验与实例化。
    /// 不负责全局任务规划，也不负责 Boss 流程。
    /// </summary>
    public static class EnemySpawnRuntime
    {
        private const float NavMeshSampleRadius = 4f;
        private const int MaxPlacementAttempts = 24;

        public static EnemySpawnVariantCatalog BuildVariantCatalog()
        {
            var catalog = new EnemySpawnVariantCatalog();
            EnemyStatsDatabaseSO statsDb = ConfigManager.GetInstance()?.GetEnemyStatsDatabase();
            if (statsDb?.entries == null)
                return catalog;

            for (int i = 0; i < statsDb.entries.Count; i++)
            {
                EnemyStatsEntry entry = statsDb.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.enemyId))
                    continue;

                string enemyId = entry.enemyId.Trim();
                GameObject prefab = Resources.Load<GameObject>($"Prefabs/{enemyId}");
                bool hasTypeFallbackPrefab = entry.type == EnemyType.MeleeMinion || entry.type == EnemyType.RangedMinion;
                if (prefab == null && !hasTypeFallbackPrefab)
                    continue;

                switch (entry.type)
                {
                    case EnemyType.Elite:
                        catalog.EliteIds.Add(enemyId);
                        break;
                    case EnemyType.Guardian:
                        catalog.GuardianIds.Add(enemyId);
                        break;
                    case EnemyType.Boss:
                        catalog.BossIds.Add(enemyId);
                        break;
                }
            }

            return catalog;
        }

        public static bool TryResolveCenterOnNavMesh(
            Vector3 preferredCenter,
            out Vector3 center)
        {
            center = preferredCenter;
            if (NavMesh.SamplePosition(preferredCenter, out NavMeshHit hit, NavMeshSampleRadius, NavMesh.AllAreas))
            {
                center = hit.position;
                return true;
            }

            return false;
        }

        public static bool TrySpawnTask(
            EnemySpawnTaskDefinition definition,
            int spawnCount,
            Vector3 center,
            System.Random rng,
            EnemySpawnVariantCatalog catalog)
        {
            if (definition == null || spawnCount <= 0)
                return false;

            var localOccupiedPositions = new List<Vector3>();
            bool spawnedAny = false;
            for (int i = 0; i < spawnCount; i++)
            {
                string enemyId = ResolveEnemyId(definition, rng, catalog);
                if (string.IsNullOrEmpty(enemyId))
                    break;

                if (!TrySampleEnemyPositionInTask(center, definition, localOccupiedPositions, rng, out Vector3 position))
                    continue;

                if (!TrySpawnEnemyAtPosition(enemyId, definition.enemyType, position, catalog))
                    continue;

                localOccupiedPositions.Add(position);
                spawnedAny = true;
            }

            return spawnedAny;
        }

        public static bool TrySpawnSingleEnemy(string enemyId, EnemyType type, Vector3 position, EnemySpawnVariantCatalog catalog)
        {
            return TrySpawnEnemyAtPosition(enemyId, type, position, catalog);
        }

        public static string ResolveEnemyId(
            EnemySpawnTaskDefinition definition,
            System.Random rng,
            EnemySpawnVariantCatalog catalog)
        {
            if (definition == null)
                return string.Empty;

            return ResolveEnemyIdByType(definition.enemyType, rng, catalog?.GetVariantIds(definition.enemyType));
        }

        public static GameObject ResolvePrefab(string enemyId, EnemyType type, EnemySpawnVariantCatalog catalog)
        {
            if (!string.IsNullOrWhiteSpace(enemyId))
            {
                GameObject exact = Resources.Load<GameObject>($"Prefabs/{enemyId}");
                if (exact != null)
                    return exact;
            }

            return type switch
            {
                EnemyType.MeleeMinion => Resources.Load<GameObject>("Prefabs/Melee Minion"),
                EnemyType.RangedMinion => Resources.Load<GameObject>("Prefabs/Ranged Minion"),
                EnemyType.Elite => LoadFirstAvailable(catalog?.EliteIds),
                EnemyType.Guardian => LoadFirstAvailable(catalog?.GuardianIds),
                EnemyType.Boss => LoadFirstAvailable(catalog?.BossIds),
                _ => null,
            };
        }

        private static bool TrySampleEnemyPositionInTask(
            Vector3 center,
            EnemySpawnTaskDefinition definition,
            List<Vector3> localOccupiedPositions,
            System.Random rng,
            out Vector3 position)
        {
            float radius = Mathf.Max(1f, definition.spawnRadius);
            float minSpacing = Mathf.Max(0.1f, definition.enemyMinSpacing);

            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                float angle = RandomRange(rng, 0f, 360f) * Mathf.Deg2Rad;
                float distance = RandomRange(rng, 0f, radius);
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavMeshSampleRadius, NavMesh.AllAreas))
                    continue;

                Vector3 hitPosition = hit.position;
                if (!IsFarEnoughFromPoints(hitPosition, localOccupiedPositions, minSpacing))
                    continue;

                if (!IsFarEnoughFromLivingEnemies(hitPosition, minSpacing))
                    continue;

                position = hitPosition;
                return true;
            }

            position = center;
            return false;
        }

        private static bool IsFarEnoughFromPoints(Vector3 position, List<Vector3> occupiedPositions, float minSpacing)
        {
            float minSpacingSqr = minSpacing * minSpacing;
            for (int i = 0; i < occupiedPositions.Count; i++)
            {
                Vector3 offset = position - occupiedPositions[i];
                offset.y = 0f;
                if (offset.sqrMagnitude < minSpacingSqr)
                    return false;
            }

            return true;
        }

        private static bool IsFarEnoughFromLivingEnemies(Vector3 position, float minSpacing)
        {
            float minSpacingSqr = minSpacing * minSpacing;
            EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                Vector3 offset = position - enemy.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < minSpacingSqr)
                    return false;
            }

            return true;
        }

        private static bool TrySpawnEnemyAtPosition(string enemyId, EnemyType type, Vector3 position, EnemySpawnVariantCatalog catalog)
        {
            GameObject prefab = ResolvePrefab(enemyId, type, catalog);
            if (prefab == null)
            {
                Debug.LogWarning($"[EnemySpawnRuntime] 未找到敌人预制体。enemyId={enemyId}, type={type}");
                return false;
            }

            Vector3 playerPos = GameStateMachine.GetInstance()?.LevelPlayerTransform != null
                ? GameStateMachine.GetInstance().LevelPlayerTransform.position
                : position + Vector3.forward;
            Vector3 lookDirection = playerPos - position;
            lookDirection.y = 0f;
            Quaternion rotation = lookDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                : Quaternion.identity;

            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            instance.name = prefab.name;
            return true;
        }

        private static GameObject LoadFirstAvailable(List<string> ids)
        {
            if (ids == null)
                return null;

            for (int i = 0; i < ids.Count; i++)
            {
                GameObject prefab = Resources.Load<GameObject>($"Prefabs/{ids[i]}");
                if (prefab != null)
                    return prefab;
            }

            return null;
        }

        private static string ResolveEnemyIdByType(EnemyType type, System.Random rng, List<string> variants)
        {
            if (type == EnemyType.MeleeMinion)
                return "melee_minion";

            if (type == EnemyType.RangedMinion)
                return "ranged_minion";

            if (variants == null || variants.Count == 0)
                return string.Empty;

            int index = rng.Next(0, variants.Count);
            return variants[index];
        }

        private static float RandomRange(System.Random rng, float min, float max)
        {
            if (min >= max)
                return min;

            return (float)(min + rng.NextDouble() * (max - min));
        }
    }
}
