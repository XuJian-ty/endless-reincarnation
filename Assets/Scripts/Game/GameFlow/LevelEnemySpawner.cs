using System;
using System.Collections.Generic;
using Game.Data;
using Game.Saving;
using UnityEngine;
using UnityEngine.AI;

namespace Game.GameFlow
{
    /// <summary>
    /// 全局生成器：引用多条具体类型生成数据，并按任务队列执行生成任务。
    /// 可生成敌人、宝箱、商店；通过本生成器刷出的 Boss 默认不计入关卡胜负。
    /// </summary>
    public class LevelEnemySpawner : MonoBehaviour
    {
        [Header("生成方案")]
        [SerializeField] [Tooltip("为空时使用当前关卡配置里的全局生成方案库。")]
        private LevelEnemySpawnPlanSO _planLibrary;
        [SerializeField] [Min(0)] [Tooltip("当前全局生成器使用的方案列表项索引。")]
        private int _planIndex;

        private readonly Queue<LevelEnemySpawnTaskRequest> _pendingSpawnTasks = new Queue<LevelEnemySpawnTaskRequest>();
        private readonly List<Vector3> _usedTaskCenters = new List<Vector3>();

        private int _seed;
        private System.Random _rng;
        private EnemySpawnVariantCatalog _variantCatalog;
        private bool _tasksPrepared;
        private EnemySpawnTaskDatabaseSO _resolvedTaskDatabase;
        private LevelEnemyGlobalSpawnPlanDefinition _resolvedSpawnPlan;
        private float _taskSpawnTimer;
        private bool _restoredFromSnapshot;
        private bool _restoreFinished;
        private bool _executingInitialTasks;

        private void Awake()
        {
            _seed = GameStateMachine.GetInstance()?.CurrentRun?.levelSnapshot?.seed ?? 0;
            if (_seed == 0)
                _seed = Environment.TickCount;

            var run = GameStateMachine.GetInstance()?.CurrentRun;
            if (run != null)
            {
                run.levelSnapshot ??= new Saving.LevelSnapshot();
                if (run.levelSnapshot.seed == 0)
                    run.levelSnapshot.seed = _seed;
            }

            _rng = new System.Random(_seed ^ 0x4A91);
            _variantCatalog = EnemySpawnRuntime.BuildVariantCatalog();
            ApplySnapshotIfAvailable();
        }

        private void Start()
        {
            if (_restoreFinished)
            {
                MarkFinished();
                return;
            }

            if (_restoredFromSnapshot)
                return;

            ExecuteInitialTasksIfNeeded();
        }

        private void Update()
        {
            TickProgressiveSpawning(Time.deltaTime);
        }

        public int GetPlannedGuardianCount()
        {
            if (!TryResolveSpawnPlan(ResolveCurrentLevelConfig(), out EnemySpawnTaskDatabaseSO taskDatabase, out LevelEnemyGlobalSpawnPlanDefinition plan))
                return 0;

            return LevelEnemySpawnPlanner.CountPlannedGuardianSpawns(plan, taskDatabase, new System.Random(_seed));
        }

        public int GetPendingGuardianCount()
        {
            if (!EnsureSpawnTasksPrepared())
                return 0;

            int total = 0;
            foreach (LevelEnemySpawnTaskRequest request in _pendingSpawnTasks)
            {
                if (request?.Definition == null || !request.Definition.IsGuardianCategory)
                    continue;

                total += Mathf.Max(0, request.SpawnCount);
            }

            return total;
        }

        public string GetSnapshotId()
        {
            Vector3 position = transform.position;
            string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : string.Empty;
            return $"{sceneName}|{gameObject.name}|{position.x:F3}|{position.y:F3}|{position.z:F3}";
        }

        public bool TryBuildSnapshot(out GlobalSpawnerSnapshotSave snapshot)
        {
            snapshot = null;
            if (!_tasksPrepared && !EnsureSpawnTasksPrepared())
                return false;

            if (_resolvedSpawnPlan == null)
                return false;

            snapshot = new GlobalSpawnerSnapshotSave
            {
                spawnerId = GetSnapshotId(),
                isFinished = _restoreFinished || _pendingSpawnTasks.Count == 0,
                taskSpawnTimer = Mathf.Max(0f, _taskSpawnTimer),
                pendingTasks = new List<SpawnTaskRequestSave>(_pendingSpawnTasks.Count),
            };

            foreach (LevelEnemySpawnTaskRequest request in _pendingSpawnTasks)
            {
                if (request == null)
                    continue;

                snapshot.pendingTasks.Add(new SpawnTaskRequestSave
                {
                    taskIndex = request.TaskIndex,
                    spawnCount = request.SpawnCount,
                    remainingRetries = request.RemainingRetries,
                    taskSeed = request.TaskSeed,
                });
            }

            return true;
        }

        public void RestoreSnapshot(GlobalSpawnerSnapshotSave snapshot)
        {
            if (snapshot == null || !string.Equals(snapshot.spawnerId, GetSnapshotId(), StringComparison.Ordinal))
                return;

            if (!TryResolveSpawnPlan(ResolveCurrentLevelConfig(), out _resolvedTaskDatabase, out _resolvedSpawnPlan))
                return;

            _tasksPrepared = true;
            _restoredFromSnapshot = true;
            _restoreFinished = snapshot.isFinished;
            _pendingSpawnTasks.Clear();
            _usedTaskCenters.Clear();
            _taskSpawnTimer = Mathf.Max(0f, snapshot.taskSpawnTimer);

            if (_restoreFinished || snapshot.pendingTasks == null)
                return;

            for (int i = 0; i < snapshot.pendingTasks.Count; i++)
            {
                SpawnTaskRequestSave requestSave = snapshot.pendingTasks[i];
                EnemySpawnTaskDefinition definition = _resolvedTaskDatabase?.GetTaskAt(requestSave.taskIndex);
                if (definition == null)
                    continue;

                LevelEnemySpawnTaskRequest request = new LevelEnemySpawnTaskRequest(
                    requestSave.taskIndex,
                    definition,
                    requestSave.spawnCount,
                    requestSave.taskSeed);
                request.RemainingRetries = Mathf.Max(0, requestSave.remainingRetries);
                _pendingSpawnTasks.Enqueue(request);
            }
        }

        private bool EnsureSpawnTasksPrepared()
        {
            if (_tasksPrepared)
                return _resolvedSpawnPlan != null;

            _tasksPrepared = true;
            _pendingSpawnTasks.Clear();
            _usedTaskCenters.Clear();
            _resolvedTaskDatabase = null;

            LevelConfigData levelConfig = ResolveCurrentLevelConfig();
            if (!TryResolveSpawnPlan(levelConfig, out _resolvedTaskDatabase, out _resolvedSpawnPlan))
            {
                Debug.LogError("[LevelEnemySpawner] 未找到全局生成方案。请在关卡配置或生成器字段中指定方案库和列表项。", this);
                return false;
            }

            if (_resolvedTaskDatabase == null)
            {
                Debug.LogError("[LevelEnemySpawner] 全局生成方案库未配置任务库。", this);
                return false;
            }

            List<LevelEnemySpawnTaskRequest> requests = LevelEnemySpawnPlanner.BuildTaskQueue(_resolvedSpawnPlan, _resolvedTaskDatabase, new System.Random(_seed));
            if (requests == null || requests.Count == 0)
                return false;

            for (int i = 0; i < requests.Count; i++)
                _pendingSpawnTasks.Enqueue(requests[i]);

            return true;
        }

        private void ApplySnapshotIfAvailable()
        {
            LevelSnapshot snapshot = GameStateMachine.GetInstance()?.CurrentRun?.levelSnapshot;
            if (snapshot == null || snapshot.runtimeSnapshotVersion <= 0)
                return;

            if (snapshot.globalSpawner != null)
            {
                RestoreSnapshot(snapshot.globalSpawner);
                return;
            }

            // 兼容旧运行时快照：当敌人已按快照恢复，但旧存档中尚未记录全局生成器状态时，
            // 禁止全局生成器再次 fresh spawn，避免和快照敌人叠加。
            _tasksPrepared = true;
            _restoredFromSnapshot = true;
            _restoreFinished = true;
            _pendingSpawnTasks.Clear();
            _usedTaskCenters.Clear();
            _resolvedTaskDatabase = null;
            _resolvedSpawnPlan = null;
        }

        private bool TryResolveSpawnPlan(
            LevelConfigData levelConfig,
            out EnemySpawnTaskDatabaseSO taskDatabase,
            out LevelEnemyGlobalSpawnPlanDefinition plan)
        {
            taskDatabase = null;
            plan = null;

            LevelEnemySpawnPlanSO library = _planLibrary;
            int planIndex = _planIndex;

            if (library == null && levelConfig != null)
            {
                library = levelConfig.globalSpawnPlanLibrary;
                planIndex = levelConfig.globalSpawnPlanIndex;
            }

            if (library == null)
                return false;

            taskDatabase = library.taskDatabase;
            plan = library.GetPlanAt(planIndex);
            return taskDatabase != null && plan != null;
        }

        private void ExecuteInitialTasksIfNeeded()
        {
            if (!EnsureSpawnTasksPrepared())
                return;

            if (_resolvedSpawnPlan.spawnMode == GlobalEnemySpawnMode.SpawnAllAtOnce)
            {
                while (_pendingSpawnTasks.Count > 0)
                    TryExecuteNextSpawnTask();

                MarkFinished();
                return;
            }

            int initialTaskCount = Mathf.Min(_pendingSpawnTasks.Count, Mathf.Max(0, _resolvedSpawnPlan.initialTaskCount));
            _executingInitialTasks = true;
            try
            {
                for (int i = 0; i < initialTaskCount; i++)
                    TryExecuteNextSpawnTask();
            }
            finally
            {
                _executingInitialTasks = false;
            }

            _taskSpawnTimer = Mathf.Max(0.1f, _resolvedSpawnPlan.taskInterval);
        }

        private void TickProgressiveSpawning(float deltaTime)
        {
            if (!_tasksPrepared || _resolvedSpawnPlan == null)
                return;

            if (_resolvedSpawnPlan.spawnMode != GlobalEnemySpawnMode.SpawnOverTime)
                return;

            if (_pendingSpawnTasks.Count == 0)
            {
                MarkFinished();
                return;
            }

            _taskSpawnTimer -= deltaTime;
            if (_taskSpawnTimer > 0f)
                return;

            TryExecuteNextSpawnTask();
            _taskSpawnTimer = Mathf.Max(0.1f, _resolvedSpawnPlan.taskInterval);
        }

        private bool TryExecuteNextSpawnTask()
        {
            if (_pendingSpawnTasks.Count == 0 || _resolvedSpawnPlan == null)
                return false;

            LevelEnemySpawnTaskRequest request = _pendingSpawnTasks.Dequeue();
            if (request?.Definition == null)
                return false;

            System.Random taskRng = CreateTaskRandom(request);
            if (!TryFindTaskCenter(taskRng, out Vector3 center))
            {
                if (request.RemainingRetries > 0)
                {
                    request.RemainingRetries--;
                    _pendingSpawnTasks.Enqueue(request);
                }
                else
                {
                    Debug.LogWarning($"[LevelEnemySpawner] 任务圆心采样失败，已跳过。taskIndex={request.TaskIndex}");
                }
                return false;
            }

            bool success = EnemySpawnRuntime.TrySpawnTask(
                request.Definition,
                request.SpawnCount,
                center,
                taskRng,
                _variantCatalog);

            if (success && ShouldTrackTaskCenter())
                _usedTaskCenters.Add(center);

            return success;
        }

        private bool TryFindTaskCenter(System.Random rng, out Vector3 center)
        {
            if (_resolvedSpawnPlan.spawnMode == GlobalEnemySpawnMode.SpawnOverTime && !_executingInitialTasks)
                return TryUsePlayerPositionAsTaskCenter(rng, out center);

            return TrySampleTaskCenterFromFallback(rng, out center);
        }

        private bool ShouldTrackTaskCenter()
        {
            if (_resolvedSpawnPlan == null)
                return false;

            if (_resolvedSpawnPlan.spawnMode == GlobalEnemySpawnMode.SpawnAllAtOnce)
                return true;

            return _resolvedSpawnPlan.spawnMode == GlobalEnemySpawnMode.SpawnOverTime && _executingInitialTasks;
        }

        private bool TrySampleTaskCenterFromFallback(System.Random rng, out Vector3 center)
        {
            if (!TryGetFallbackBounds(out Bounds bounds))
            {
                center = Vector3.zero;
                return false;
            }

            const int attempts = 24;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Vector3 candidate = new Vector3(
                    RandomRange(rng, bounds.min.x, bounds.max.x),
                    bounds.center.y,
                    RandomRange(rng, bounds.min.z, bounds.max.z));

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    continue;

                if (!IsValidTaskCenter(hit.position))
                    continue;

                center = hit.position;
                return true;
            }

            center = Vector3.zero;
            return false;
        }

        private bool TryUsePlayerPositionAsTaskCenter(System.Random rng, out Vector3 center)
        {
            Transform player = GameStateMachine.GetInstance()?.LevelPlayerTransform;
            if (player == null)
                return TrySampleTaskCenterFromFallback(rng, out center);

            return EnemySpawnRuntime.TryResolveCenterOnNavMesh(player.position, out center);
        }

        private bool IsValidTaskCenter(Vector3 center)
        {
            float minSpacing = Mathf.Max(0.1f, _resolvedSpawnPlan.taskCenterMinSpacing);
            for (int i = 0; i < _usedTaskCenters.Count; i++)
            {
                Vector3 offset = center - _usedTaskCenters[i];
                offset.y = 0f;
                if (offset.sqrMagnitude < minSpacing * minSpacing)
                    return false;
            }

            return true;
        }

        private bool TryGetFallbackBounds(out Bounds bounds)
        {
            GameObject plane = GameObject.Find("Plane");
            if (plane != null)
            {
                Collider collider = plane.GetComponent<Collider>();
                if (collider != null)
                {
                    bounds = collider.bounds;
                    return true;
                }

                Renderer renderer = plane.GetComponent<Renderer>();
                if (renderer != null)
                {
                    bounds = renderer.bounds;
                    return true;
                }
            }

            bounds = default;
            return false;
        }

        private LevelConfigData ResolveCurrentLevelConfig()
        {
            int level = GameStateMachine.GetInstance()?.CurrentRun?.levelIndex ?? 1;
            return ConfigManager.GetInstance()?.GetLevelConfigDatabase()?.GetConfigForLevel(level);
        }

        private float RandomRange(System.Random rng, float min, float max)
        {
            if (min >= max)
                return min;

            if (rng == null)
                return min;

            return (float)(min + rng.NextDouble() * (max - min));
        }

        private System.Random CreateTaskRandom(LevelEnemySpawnTaskRequest request)
        {
            int retrySalt = Mathf.Max(0, 2 - Mathf.Max(0, request.RemainingRetries));
            return new System.Random(unchecked(request.TaskSeed ^ (retrySalt * 0x45D9F3B)));
        }

        private void MarkFinished()
        {
            _restoreFinished = true;
            enabled = false;
        }

    }
}
