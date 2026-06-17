using System;
using System.Collections.Generic;
using Game.Data;
using Game.Online;
using Game.Presentation;
using Game.Saving;
using ProjectBase;
using UnityEngine;
using UnityEngine.AI;

namespace Game.GameFlow
{
    /// <summary>
    /// 生成变体信息：记录生成ID、显示名与敌人类型信息。
    /// </summary>
    public sealed class EnemySpawnVariantInfo
    {
        public string spawnId;
        public string displayName;
        public EnemySpawnCategory spawnType;
        public EnemyType enemyType;
    }

    /// <summary>
    /// 生成变体缓存：收集可用的生成ID，供生成器在运行时复用。
    /// </summary>
    public sealed class EnemySpawnVariantCatalog
    {
        private readonly Dictionary<EnemySpawnCategory, List<EnemySpawnVariantInfo>> _variantsByCategory = new Dictionary<EnemySpawnCategory, List<EnemySpawnVariantInfo>>();

        public List<EnemySpawnVariantInfo> GetVariants(EnemyType type)
        {
            EnemySpawnCategory category = type switch
            {
                EnemyType.Elite => EnemySpawnCategory.Elite,
                EnemyType.Guardian => EnemySpawnCategory.Guardian,
                EnemyType.Boss => EnemySpawnCategory.Boss,
                _ => EnemySpawnCategory.Minion,
            };
            List<EnemySpawnVariantInfo> variants = GetVariants(category);
            if (variants == null)
                return null;

            var result = new List<EnemySpawnVariantInfo>();
            for (int i = 0; i < variants.Count; i++)
            {
                EnemySpawnVariantInfo variant = variants[i];
                if (variant != null && variant.enemyType == type)
                    result.Add(variant);
            }

            return result;
        }

        public void Add(EnemySpawnCategory category, string spawnId, string displayName, EnemyType enemyType = EnemyType.MeleeMinion)
        {
            if (!_variantsByCategory.TryGetValue(category, out List<EnemySpawnVariantInfo> variants))
            {
                variants = new List<EnemySpawnVariantInfo>();
                _variantsByCategory.Add(category, variants);
            }

            variants.Add(new EnemySpawnVariantInfo
            {
                spawnId = spawnId,
                displayName = string.IsNullOrWhiteSpace(displayName) ? spawnId : displayName,
                spawnType = category,
                enemyType = enemyType,
            });
        }

        public string GetDisplayName(EnemySpawnCategory category, string spawnId)
        {
            List<EnemySpawnVariantInfo> variants = GetVariants(category);
            if (variants == null || string.IsNullOrWhiteSpace(spawnId))
                return string.Empty;

            string normalizedId = spawnId.Trim();
            for (int i = 0; i < variants.Count; i++)
            {
                EnemySpawnVariantInfo variant = variants[i];
                if (variant != null && string.Equals(variant.spawnId, normalizedId, StringComparison.OrdinalIgnoreCase))
                    return variant.displayName;
            }

            return normalizedId;
        }

        public List<EnemySpawnVariantInfo> GetVariants(EnemySpawnCategory category)
        {
            return _variantsByCategory.TryGetValue(category, out List<EnemySpawnVariantInfo> variants)
                ? variants
                : null;
        }

        public EnemySpawnVariantInfo GetVariant(string spawnId)
        {
            if (string.IsNullOrWhiteSpace(spawnId))
                return null;

            string normalizedId = spawnId.Trim();
            foreach (KeyValuePair<EnemySpawnCategory, List<EnemySpawnVariantInfo>> pair in _variantsByCategory)
            {
                List<EnemySpawnVariantInfo> variants = pair.Value;
                if (variants == null)
                    continue;

                for (int i = 0; i < variants.Count; i++)
                {
                    EnemySpawnVariantInfo variant = variants[i];
                    if (variant != null && string.Equals(variant.spawnId, normalizedId, StringComparison.OrdinalIgnoreCase))
                        return variant;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 共享生成执行逻辑：负责候选对象解析、任务内散布、最小间距校验与实例化。
    /// 不负责全局任务规划，也不负责 Boss 流程。
    /// </summary>
    public static class EnemySpawnRuntime
    {
        private const float NavMeshSampleRadius = 4f;
        private const int MaxPlacementAttempts = 24;
        private const float SpawnEmergenceDuration = 0.75f;

        public static EnemySpawnVariantCatalog BuildVariantCatalog()
        {
            var catalog = new EnemySpawnVariantCatalog();
            EnemyStatsDatabaseSO statsDb = ConfigManager.GetInstance()?.GetEnemyStatsDatabase();
            IReadOnlyList<EnemyStatsEntry> allEntries = statsDb?.GetAllEntries();
            if (allEntries != null)
            {
                for (int i = 0; i < allEntries.Count; i++)
                {
                    EnemyStatsEntry entry = allEntries[i];
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
                            catalog.Add(MapEnemyTypeToCategory(entry.type), enemyId, entry.displayName, entry.type);
                            break;
                    }
                }
            }

            AddStaticVariant(catalog, EnemySpawnCategory.Chest, "宝箱", "宝箱");
            AddStaticVariant(catalog, EnemySpawnCategory.Shop, "商店", "商店");

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
                if (!TryResolveSpawnVariant(definition, rng, catalog, out string spawnId, out EnemyType actualType))
                    break;

                if (!TrySampleSpawnPositionInTask(center, definition, localOccupiedPositions, rng, out Vector3 position))
                    continue;

                if (!TrySpawnAtPosition(definition.spawnType, spawnId, actualType, position, catalog, countsAsLevelBoss))
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

        public static EnemyController SpawnEnemyFromSnapshot(EnemySnapshot snapshot, EnemySpawnVariantCatalog catalog)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.id))
                return null;

            EnemyType type = Enum.IsDefined(typeof(EnemyType), snapshot.enemyType)
                ? (EnemyType)snapshot.enemyType
                : EnemyType.MeleeMinion;

            EnemyController controller = InstantiateEnemy(
                snapshot.id,
                type,
                new Vector3(snapshot.x, snapshot.y, snapshot.z),
                Quaternion.Euler(0f, snapshot.yaw, 0f),
                catalog,
                snapshot.countsAsLevelBoss,
                false);

            controller?.RestoreFromSnapshot(snapshot);
            return controller;
        }

        public static string ResolveEnemyId(
            EnemySpawnTaskDefinition definition,
            System.Random rng,
            EnemySpawnVariantCatalog catalog)
        {
            if (definition == null)
                return string.Empty;

            if (TryResolveSpawnVariant(definition, rng, catalog, out string spawnId, out _))
                return spawnId;

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

        private static bool TrySampleSpawnPositionInTask(
            Vector3 center,
            EnemySpawnTaskDefinition definition,
            List<Vector3> localOccupiedPositions,
            System.Random rng,
            out Vector3 position)
        {
            float radius = Mathf.Max(1f, definition.spawnRadius);
            float minSpacing = Mathf.Max(0.1f, definition.spawnMinSpacing);

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

                if (definition.IsEnemyCategory && !IsFarEnoughFromLivingEnemies(hitPosition, minSpacing))
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

        private static bool TrySpawnAtPosition(EnemySpawnCategory spawnType, string spawnId, EnemyType enemyType, Vector3 position, EnemySpawnVariantCatalog catalog, bool countsAsLevelBoss)
        {
            if (definitionIsEnemy(spawnType))
                return TrySpawnEnemyAtPosition(spawnId, enemyType, position, catalog, countsAsLevelBoss);

            GameObject prefab = ResolveSpawnPrefab(spawnType, spawnId, enemyType, catalog);
            if (prefab == null)
            {
                Debug.LogWarning($"[EnemySpawnRuntime] 未找到生成预制体。spawnId={spawnId}, spawnType={spawnType}");
                return false;
            }

            Transform interactablesRoot = LevelRuntimeHierarchy.GetInteractablesRoot();
            GameObject instance = interactablesRoot != null
                ? UnityEngine.Object.Instantiate(prefab, position, ResolveStaticSpawnRotation(prefab, position), interactablesRoot)
                : UnityEngine.Object.Instantiate(prefab, position, ResolveStaticSpawnRotation(prefab, position));
            instance.name = prefab.name;
            if (spawnType == EnemySpawnCategory.Chest && instance.GetComponentInChildren<ChestInteractable>(true) == null)
                ChestInteractable.EnsureOn(instance);
            ApplySpawnEmergence(instance);
            return true;
        }

        private static bool TrySpawnEnemyAtPosition(string enemyId, EnemyType type, Vector3 position, EnemySpawnVariantCatalog catalog, bool countsAsLevelBoss)
        {
            return InstantiateEnemy(enemyId, type, position, ResolveLookRotation(position), catalog, countsAsLevelBoss, true) != null;
        }

        private static EnemyController InstantiateEnemy(
            string enemyId,
            EnemyType type,
            Vector3 position,
            Quaternion rotation,
            EnemySpawnVariantCatalog catalog,
            bool countsAsLevelBoss,
            bool applyEmergence)
        {
            GameObject prefab = ResolvePrefab(enemyId, type, catalog);
            if (prefab == null)
            {
                Debug.LogWarning($"[EnemySpawnRuntime] 未找到敌人预制体。enemyId={enemyId}, type={type}");
                return null;
            }

            bool usePool = type != EnemyType.Boss;
            Transform enemiesRoot = LevelRuntimeHierarchy.GetEnemiesRoot();
            GameObject instance = usePool
                ? PoolMgr.GetInstance().GetObjSync(prefab, enemiesRoot)
                : (enemiesRoot != null
                    ? UnityEngine.Object.Instantiate(prefab, position, rotation, enemiesRoot)
                    : UnityEngine.Object.Instantiate(prefab, position, rotation));
            if (instance == null)
                return null;

            instance.name = prefab.name;
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.transform.localScale = prefab.transform.localScale;
            EnemyController controller = instance.GetComponent<EnemyController>();
            if (controller != null)
                controller.PrepareForSpawn(usePool ? prefab : null);

            if (controller != null && type == EnemyType.Boss)
                ApplyBossVisuals(instance, controller, countsAsLevelBoss);

            if (applyEmergence)
                ApplySpawnEmergence(instance);

            if (controller != null)
                OnlineDungeonSessionCoordinator.GetInstance().NotifyOnlineWorldSimulationChanged();

            return controller;
        }

        private static void ApplyBossVisuals(GameObject instance, EnemyController controller, bool countsAsLevelBoss)
        {
            controller.SetCountsAsLevelBoss(countsAsLevelBoss);
        }

        private static Quaternion ResolveLookRotation(Vector3 position)
        {
            Vector3 playerPos = GameStateMachine.GetInstance()?.LevelPlayerTransform != null
                ? GameStateMachine.GetInstance().LevelPlayerTransform.position
                : position + Vector3.forward;
            Vector3 lookDirection = playerPos - position;
            lookDirection.y = 0f;
            return lookDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                : Quaternion.identity;
        }

        private static Quaternion ResolveStaticSpawnRotation(GameObject prefab, Vector3 position)
        {
            return prefab.transform.rotation;
        }

        private static GameObject LoadFirstAvailable(List<EnemySpawnVariantInfo> variants)
        {
            if (variants == null)
                return null;

            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i] == null || string.IsNullOrWhiteSpace(variants[i].spawnId))
                    continue;

                GameObject prefab = Resources.Load<GameObject>($"Prefabs/{variants[i].spawnId}");
                if (prefab != null)
                    return prefab;
            }

            return null;
        }

        private static bool TryResolveSpawnVariant(
            EnemySpawnTaskDefinition definition,
            System.Random rng,
            EnemySpawnVariantCatalog catalog,
            out string spawnId,
            out EnemyType enemyType)
        {
            spawnId = string.Empty;
            enemyType = EnemyType.MeleeMinion;

            if (definition == null)
                return false;

            if (!string.IsNullOrWhiteSpace(definition.specificSpawnId))
            {
                EnemySpawnVariantInfo specificVariant = catalog?.GetVariant(definition.specificSpawnId.Trim());
                if (specificVariant != null &&
                    (definition.IsEnemyCategory ? definitionIsEnemy(specificVariant.spawnType) : specificVariant.spawnType == definition.spawnType))
                {
                    spawnId = specificVariant.spawnId;
                    enemyType = specificVariant.enemyType;
                    return true;
                }

                spawnId = definition.specificSpawnId.Trim();
                if (definition.IsEnemyCategory)
                    enemyType = definition.IsMinionCategory ? EnemyType.MeleeMinion : MapCategoryToEnemyType(definition.spawnType);
                return true;
            }

            List<EnemySpawnVariantInfo> variants = catalog?.GetVariants(definition.spawnType);
            if (variants != null && variants.Count > 0)
            {
                int index = rng.Next(0, variants.Count);
                EnemySpawnVariantInfo variant = variants[index];
                if (variant != null)
                {
                    spawnId = variant.spawnId;
                    enemyType = variant.enemyType;
                    return true;
                }
            }

            if (!definition.IsEnemyCategory)
            {
                spawnId = GetDefaultSpawnId(definition.spawnType);
                return !string.IsNullOrWhiteSpace(spawnId);
            }

            enemyType = definition.IsMinionCategory ? EnemyType.MeleeMinion : MapCategoryToEnemyType(definition.spawnType);
            spawnId = enemyType switch
            {
                EnemyType.MeleeMinion => "melee_minion",
                EnemyType.RangedMinion => "ranged_minion",
                _ => string.Empty,
            };
            return !string.IsNullOrWhiteSpace(spawnId);
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

        private static EnemySpawnCategory MapEnemyTypeToCategory(EnemyType type)
        {
            return type switch
            {
                EnemyType.Elite => EnemySpawnCategory.Elite,
                EnemyType.Guardian => EnemySpawnCategory.Guardian,
                EnemyType.Boss => EnemySpawnCategory.Boss,
                _ => EnemySpawnCategory.Minion,
            };
        }

        private static void AddStaticVariant(EnemySpawnVariantCatalog catalog, EnemySpawnCategory category, string spawnId, string displayName)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(spawnId))
                return;

            if (Resources.Load<GameObject>($"Prefabs/{spawnId}") == null)
                return;

            catalog.Add(category, spawnId.Trim(), displayName);
        }

        private static string GetDefaultSpawnId(EnemySpawnCategory category)
        {
            return category switch
            {
                EnemySpawnCategory.Chest => "宝箱",
                EnemySpawnCategory.Shop => "商店",
                _ => string.Empty,
            };
        }

        private static bool definitionIsEnemy(EnemySpawnCategory category)
        {
            return category is EnemySpawnCategory.Minion or EnemySpawnCategory.Elite or EnemySpawnCategory.Guardian or EnemySpawnCategory.Boss;
        }

        private static GameObject ResolveSpawnPrefab(EnemySpawnCategory spawnType, string spawnId, EnemyType enemyType, EnemySpawnVariantCatalog catalog)
        {
            if (!string.IsNullOrWhiteSpace(spawnId))
            {
                GameObject exact = Resources.Load<GameObject>($"Prefabs/{spawnId}");
                if (exact != null)
                    return exact;
            }

            if (definitionIsEnemy(spawnType))
                return ResolvePrefab(string.Empty, enemyType, catalog);

            return LoadFirstAvailable(catalog?.GetVariants(spawnType));
        }

        private static float RandomRange(System.Random rng, float min, float max)
        {
            if (min >= max)
                return min;

            return (float)(min + rng.NextDouble() * (max - min));
        }

        private static void ApplySpawnEmergence(GameObject instance)
        {
            if (instance == null)
                return;

            SpawnEmergenceEffect effect = instance.GetComponent<SpawnEmergenceEffect>();
            if (effect == null)
                effect = instance.AddComponent<SpawnEmergenceEffect>();

            effect.Initialize(SpawnEmergenceDuration);
        }
    }
}
