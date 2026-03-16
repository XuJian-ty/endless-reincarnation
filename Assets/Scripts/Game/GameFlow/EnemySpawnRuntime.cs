using System;
using System.Collections.Generic;
using Game.Data;
using Game.Presentation;
using UnityEngine;
using UnityEngine.AI;

namespace Game.GameFlow
{
    /// <summary>
    /// 敌人变体信息：记录 enemyId 与显示名。
    /// </summary>
    public sealed class EnemySpawnVariantInfo
    {
        public string enemyId;
        public string displayName;
        public EnemyType enemyType;
    }

    /// <summary>
    /// 敌人变体缓存：按敌人类型收集可用的敌人ID，供生成器在运行时复用。
    /// </summary>
    public sealed class EnemySpawnVariantCatalog
    {
        private readonly Dictionary<EnemyType, List<EnemySpawnVariantInfo>> _variantsByType = new Dictionary<EnemyType, List<EnemySpawnVariantInfo>>();

        public List<EnemySpawnVariantInfo> GetVariants(EnemyType type)
        {
            return _variantsByType.TryGetValue(type, out List<EnemySpawnVariantInfo> variants)
                ? variants
                : null;
        }

        public void Add(EnemyType type, string enemyId, string displayName)
        {
            if (!_variantsByType.TryGetValue(type, out List<EnemySpawnVariantInfo> variants))
            {
                variants = new List<EnemySpawnVariantInfo>();
                _variantsByType.Add(type, variants);
            }

            variants.Add(new EnemySpawnVariantInfo
            {
                enemyId = enemyId,
                displayName = string.IsNullOrWhiteSpace(displayName) ? enemyId : displayName,
                enemyType = type,
            });
        }

        public string GetDisplayName(EnemySpawnCategory category, string enemyId)
        {
            List<EnemySpawnVariantInfo> variants = GetVariants(category);
            if (variants == null || string.IsNullOrWhiteSpace(enemyId))
                return string.Empty;

            string normalizedId = enemyId.Trim();
            for (int i = 0; i < variants.Count; i++)
            {
                EnemySpawnVariantInfo variant = variants[i];
                if (variant != null && string.Equals(variant.enemyId, normalizedId, StringComparison.OrdinalIgnoreCase))
                    return variant.displayName;
            }

            return normalizedId;
        }

        public List<EnemySpawnVariantInfo> GetVariants(EnemySpawnCategory category)
        {
            if (category == EnemySpawnCategory.Minion || category == EnemySpawnCategory.MinionLegacyRanged)
            {
                var result = new List<EnemySpawnVariantInfo>();
                AppendVariants(result, EnemyType.MeleeMinion);
                AppendVariants(result, EnemyType.RangedMinion);
                return result;
            }

            return category switch
            {
                EnemySpawnCategory.Elite => GetVariants(EnemyType.Elite),
                EnemySpawnCategory.Guardian => GetVariants(EnemyType.Guardian),
                EnemySpawnCategory.Boss => GetVariants(EnemyType.Boss),
                _ => null,
            };
        }

        public EnemySpawnVariantInfo GetVariant(string enemyId)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
                return null;

            string normalizedId = enemyId.Trim();
            foreach (KeyValuePair<EnemyType, List<EnemySpawnVariantInfo>> pair in _variantsByType)
            {
                List<EnemySpawnVariantInfo> variants = pair.Value;
                if (variants == null)
                    continue;

                for (int i = 0; i < variants.Count; i++)
                {
                    EnemySpawnVariantInfo variant = variants[i];
                    if (variant != null && string.Equals(variant.enemyId, normalizedId, StringComparison.OrdinalIgnoreCase))
                        return variant;
                }
            }

            return null;
        }

        private void AppendVariants(List<EnemySpawnVariantInfo> result, EnemyType type)
        {
            List<EnemySpawnVariantInfo> variants = GetVariants(type);
            if (variants == null)
                return;

            for (int i = 0; i < variants.Count; i++)
            {
                EnemySpawnVariantInfo variant = variants[i];
                if (variant != null)
                    result.Add(variant);
            }
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
                    case EnemyType.MeleeMinion:
                    case EnemyType.RangedMinion:
                    case EnemyType.Elite:
                    case EnemyType.Guardian:
                    case EnemyType.Boss:
                        catalog.Add(entry.type, enemyId, entry.displayName);
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
            EnemySpawnVariantCatalog catalog,
            bool countsAsLevelBoss = false)
        {
            if (definition == null || spawnCount <= 0)
                return false;

            var localOccupiedPositions = new List<Vector3>();
            bool spawnedAny = false;
            for (int i = 0; i < spawnCount; i++)
            {
                if (!TryResolveEnemyVariant(definition, rng, catalog, out string enemyId, out EnemyType actualType))
                    break;

                if (!TrySampleEnemyPositionInTask(center, definition, localOccupiedPositions, rng, out Vector3 position))
                    continue;

                if (!TrySpawnEnemyAtPosition(enemyId, actualType, position, catalog, countsAsLevelBoss))
                    continue;

                localOccupiedPositions.Add(position);
                spawnedAny = true;
            }

            return spawnedAny;
        }

        public static bool TrySpawnSingleEnemy(string enemyId, EnemyType type, Vector3 position, EnemySpawnVariantCatalog catalog, bool countsAsLevelBoss = false)
        {
            return TrySpawnEnemyAtPosition(enemyId, type, position, catalog, countsAsLevelBoss);
        }

        public static string ResolveEnemyId(
            EnemySpawnTaskDefinition definition,
            System.Random rng,
            EnemySpawnVariantCatalog catalog)
        {
            if (definition == null)
                return string.Empty;

            if (TryResolveEnemyVariant(definition, rng, catalog, out string enemyId, out _))
                return enemyId;

            return string.Empty;
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
                EnemyType.Elite => LoadFirstAvailable(catalog?.GetVariants(EnemyType.Elite)),
                EnemyType.Guardian => LoadFirstAvailable(catalog?.GetVariants(EnemyType.Guardian)),
                EnemyType.Boss => LoadFirstAvailable(catalog?.GetVariants(EnemyType.Boss)),
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

        private static bool TrySpawnEnemyAtPosition(string enemyId, EnemyType type, Vector3 position, EnemySpawnVariantCatalog catalog, bool countsAsLevelBoss)
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
            EnemyController controller = instance.GetComponent<EnemyController>();
            if (controller != null && type == EnemyType.Boss)
            {
                controller.SetCountsAsLevelBoss(countsAsLevelBoss);
                if (countsAsLevelBoss)
                {
                    float scaleMultiplier = ConfigManager.GetInstance()?.GetLevelBossVisualConfig()?.scaleMultiplier ?? 3f;
                    instance.transform.localScale *= scaleMultiplier;
                    if (controller.GetComponent<LevelBossVisualMarker>() == null)
                        controller.gameObject.AddComponent<LevelBossVisualMarker>();
                }
            }
            return true;
        }

        private static GameObject LoadFirstAvailable(List<EnemySpawnVariantInfo> variants)
        {
            if (variants == null)
                return null;

            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i] == null || string.IsNullOrWhiteSpace(variants[i].enemyId))
                    continue;

                GameObject prefab = Resources.Load<GameObject>($"Prefabs/{variants[i].enemyId}");
                if (prefab != null)
                    return prefab;
            }

            return null;
        }

        private static bool TryResolveEnemyVariant(
            EnemySpawnTaskDefinition definition,
            System.Random rng,
            EnemySpawnVariantCatalog catalog,
            out string enemyId,
            out EnemyType enemyType)
        {
            enemyId = string.Empty;
            enemyType = EnemyType.MeleeMinion;

            if (definition == null)
                return false;

            if (!string.IsNullOrWhiteSpace(definition.specificEnemyId))
            {
                EnemySpawnVariantInfo specificVariant = catalog?.GetVariant(definition.specificEnemyId.Trim());
                if (specificVariant != null)
                {
                    enemyId = specificVariant.enemyId;
                    enemyType = specificVariant.enemyType;
                    return true;
                }

                enemyId = definition.specificEnemyId.Trim();
                enemyType = definition.IsMinionCategory ? EnemyType.MeleeMinion : MapCategoryToEnemyType(definition.enemyType);
                return true;
            }

            List<EnemySpawnVariantInfo> variants = catalog?.GetVariants(definition.enemyType);
            if (variants != null && variants.Count > 0)
            {
                int index = rng.Next(0, variants.Count);
                EnemySpawnVariantInfo variant = variants[index];
                if (variant != null)
                {
                    enemyId = variant.enemyId;
                    enemyType = variant.enemyType;
                    return true;
                }
            }

            enemyType = definition.IsMinionCategory ? EnemyType.MeleeMinion : MapCategoryToEnemyType(definition.enemyType);
            enemyId = enemyType switch
            {
                EnemyType.MeleeMinion => "melee_minion",
                EnemyType.RangedMinion => "ranged_minion",
                _ => string.Empty,
            };
            return !string.IsNullOrWhiteSpace(enemyId);
        }

        private static EnemyType MapCategoryToEnemyType(EnemySpawnCategory category)
        {
            return category switch
            {
                EnemySpawnCategory.Elite => EnemyType.Elite,
                EnemySpawnCategory.Guardian => EnemyType.Guardian,
                EnemySpawnCategory.Boss => EnemyType.Boss,
                _ => EnemyType.MeleeMinion,
            };
        }

        private static float RandomRange(System.Random rng, float min, float max)
        {
            if (min >= max)
                return min;

            return (float)(min + rng.NextDouble() * (max - min));
        }
    }
}
