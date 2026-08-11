using System;
using System.Collections;
using System.Collections.Generic;
using Game.Data;
using Game.Online;
using Game.Saving;
using UnityEngine;

namespace Game.GameFlow
{
    /// <summary>
    /// 局部生成器：从局部生成方案库中选择一个列表项，以自身位置为圆心执行生成任务。
    /// 与全局生成器平级独立，但复用同一套敌人/宝箱/商店生成逻辑。
    /// </summary>
    public class LevelLocalEnemySpawner : MonoBehaviour
    {
        [Header("局部生成方案")]
        [SerializeField] [Tooltip("局部生成方案库。")]
        private LevelLocalEnemySpawnPlanSO _planLibrary;
        [SerializeField] [Min(0)] [Tooltip("本局部生成器使用的方案列表项索引。")]
        private int _planIndex;
        [SerializeField] [Tooltip("勾选后由区域流程显式激活；未激活前不会执行生成任务。")]
        private bool _waitForActivation;

        private System.Random _rng;
        private EnemySpawnVariantCatalog _variantCatalog;
        private EnemySpawnTaskDatabaseSO _resolvedTaskDatabase;
        private LevelLocalEnemySpawnPlanDefinition _resolvedPlan;
        private EnemySpawnTaskDefinition _resolvedTask;
        private readonly Queue<int> _plannedSpawnCounts = new Queue<int>();
        private int _seed;
        private int _executedTaskCount;
        private int _taskAttemptCount;
        private float _taskTimer;
        private bool _restoredFromSnapshot;
        private bool _restoreFinished;
        private bool _isActivated;
        private bool _initialTaskStarted;
        private bool _canDriveSimulation;

        private void Awake()
        {
            _canDriveSimulation = OnlineDungeonSessionCoordinator.GetInstance().ShouldDriveOnlineWorldSimulation();
            if (!_canDriveSimulation)
            {
                enabled = false;
                return;
            }

            int seed = GameStateMachine.GetInstance()?.CurrentRun?.levelSnapshot?.seed ?? Environment.TickCount;
            _seed = seed ^ gameObject.scene.handle ^ transform.position.GetHashCode() ^ _planIndex;
            _rng = new System.Random(_seed);
            _variantCatalog = EnemySpawnRuntime.BuildVariantCatalog();
            _isActivated = !_waitForActivation;
            ResolvePlanIfNeeded();
            PreparePlannedSpawnCounts();
            ApplySnapshotIfAvailable();
        }

        private IEnumerator Start()
        {
            if (_restoreFinished)
            {
                MarkFinished();
                yield break;
            }

            if (_restoredFromSnapshot)
            {
                if (_isActivated && !_initialTaskStarted)
                    yield return ExecuteInitialTaskAfterActivation();
                yield break;
            }

            if (!_isActivated)
                yield break;

            yield return ExecuteInitialTaskAfterActivation();
        }

        private void Update()
        {
            if (!_isActivated || !_initialTaskStarted || _resolvedPlan == null || _resolvedPlan.spawnMode != LocalEnemySpawnMode.SpawnRepeatedly)
                return;

            if (_resolvedPlan.taskTotalMode == LocalEnemySpawnTaskTotalMode.FixedCount &&
                _executedTaskCount >= Mathf.Max(1, _resolvedPlan.totalTaskCount))
            {
                MarkFinished();
                return;
            }

            _taskTimer -= Time.deltaTime;
            if (_taskTimer > 0f)
                return;

            TryExecuteOneTask();
            _taskTimer = Mathf.Max(0.1f, _resolvedPlan.taskInterval);
        }

        [ContextMenu("立即执行局部生成")]
        public void TryExecuteOneTask()
        {
            TryExecuteOneTaskInternal();
        }

        private bool TryExecuteOneTaskInternal()
        {
            if (!ResolvePlanIfNeeded())
                return false;

            Vector3 center = transform.position;
            EnemySpawnRuntime.TryResolveCenterOnNavMesh(transform.position, out center);
            System.Random taskRng = CreateTaskRandom(_taskAttemptCount);
            _taskAttemptCount++;
            int spawnCount = PeekPlannedSpawnCount(taskRng);
            if (!EnemySpawnRuntime.TrySpawnTask(_resolvedTask, spawnCount, center, taskRng, _variantCatalog))
                return false;

            if (_plannedSpawnCounts.Count > 0)
                _plannedSpawnCounts.Dequeue();
            _executedTaskCount++;
            return true;
        }

        public void ActivateSpawner()
        {
            if (!_canDriveSimulation || _isActivated || _restoreFinished)
                return;

            _isActivated = true;
            enabled = true;
            if (isActiveAndEnabled && !_restoredFromSnapshot)
                StartCoroutine(ExecuteInitialTaskAfterActivation());
        }

        public bool IsActivated => _isActivated;
        public bool HasStartedInitialTask => _initialTaskStarted;

        public int GetPlannedGuardianCount()
        {
            if (!ResolvePlanIfNeeded())
                return 0;

            if (!_resolvedTask.IsGuardianCategory)
                return 0;

            int taskCount = _resolvedPlan.spawnMode == LocalEnemySpawnMode.SpawnOnce
                ? 1
                : (_resolvedPlan.taskTotalMode == LocalEnemySpawnTaskTotalMode.Infinite ? 0 : Mathf.Max(1, _resolvedPlan.totalTaskCount));

            if (taskCount <= 0)
                return 0;

            int total = 0;
            System.Random previewRng = new System.Random(_seed);
            for (int i = 0; i < taskCount; i++)
                total += _resolvedTask.ResolveSpawnCount(previewRng);

            return total;
        }

        public string GetSnapshotId()
        {
            Vector3 position = transform.position;
            string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : string.Empty;
            return $"{sceneName}|{gameObject.name}|{position.x:F3}|{position.y:F3}|{position.z:F3}|{_planIndex}";
        }

        public bool TryBuildSnapshot(out LocalSpawnerSnapshotSave snapshot)
        {
            snapshot = null;
            if (!ResolvePlanIfNeeded())
                return false;

            snapshot = new LocalSpawnerSnapshotSave
            {
                spawnerId = GetSnapshotId(),
                isActivated = _isActivated,
                initialTaskStarted = _initialTaskStarted,
                isFinished = IsFinished(),
                executedTaskCount = Mathf.Max(0, _executedTaskCount),
                attemptCount = Mathf.Max(0, _taskAttemptCount),
                taskTimer = Mathf.Max(0f, _taskTimer),
                plannedSpawnCounts = new List<int>(_plannedSpawnCounts),
            };
            return true;
        }

        public void RestoreSnapshot(LocalSpawnerSnapshotSave snapshot)
        {
            if (snapshot == null || !string.Equals(snapshot.spawnerId, GetSnapshotId(), StringComparison.Ordinal))
                return;

            if (!ResolvePlanIfNeeded())
                return;

            _restoredFromSnapshot = true;
            _isActivated = snapshot.isActivated;
            _initialTaskStarted = snapshot.initialTaskStarted;
            _restoreFinished = snapshot.isFinished;
            _executedTaskCount = Mathf.Max(0, snapshot.executedTaskCount);
            _taskAttemptCount = Mathf.Max(0, snapshot.attemptCount);
            _taskTimer = Mathf.Max(0f, snapshot.taskTimer);
            _plannedSpawnCounts.Clear();

            if (snapshot.plannedSpawnCounts == null)
                return;

            for (int i = 0; i < snapshot.plannedSpawnCounts.Count; i++)
                _plannedSpawnCounts.Enqueue(Mathf.Max(0, snapshot.plannedSpawnCounts[i]));
        }

        public int GetPendingGuardianCount()
        {
            if (!ResolvePlanIfNeeded())
                return 0;

            if (!_resolvedTask.IsGuardianCategory)
                return 0;

            if (_resolvedPlan.spawnMode == LocalEnemySpawnMode.SpawnRepeatedly &&
                _resolvedPlan.taskTotalMode == LocalEnemySpawnTaskTotalMode.Infinite)
                return int.MaxValue;

            int total = 0;
            foreach (int count in _plannedSpawnCounts)
                total += Mathf.Max(0, count);

            return total;
        }

        private bool ResolvePlanIfNeeded()
        {
            if (_resolvedPlan != null && _resolvedTask != null)
                return true;

            if (_planLibrary == null)
            {
                Debug.LogWarning("[LevelLocalEnemySpawner] 未配置局部生成方案库。", this);
                return false;
            }

            _resolvedTaskDatabase = _planLibrary.taskDatabase;
            _resolvedPlan = _planLibrary.GetPlanAt(_planIndex);
            if (_resolvedTaskDatabase == null || _resolvedPlan == null)
            {
                Debug.LogWarning($"[LevelLocalEnemySpawner] 未找到局部生成方案。planIndex={_planIndex}", this);
                return false;
            }

            _resolvedTask = _resolvedTaskDatabase.GetTaskAt(_resolvedPlan.taskIndex);
            if (_resolvedTask == null)
            {
                Debug.LogWarning($"[LevelLocalEnemySpawner] 未找到局部生成任务。taskIndex={_resolvedPlan.taskIndex}", this);
                return false;
            }
            return true;
        }

        private void PreparePlannedSpawnCounts()
        {
            _plannedSpawnCounts.Clear();
            if (!ResolvePlanIfNeeded())
                return;

            if (_resolvedPlan.spawnMode == LocalEnemySpawnMode.SpawnRepeatedly &&
                _resolvedPlan.taskTotalMode == LocalEnemySpawnTaskTotalMode.Infinite)
                return;

            int taskCount = _resolvedPlan.spawnMode == LocalEnemySpawnMode.SpawnOnce
                ? 1
                : Mathf.Max(1, _resolvedPlan.totalTaskCount);

            System.Random previewRng = new System.Random(_seed);
            for (int i = 0; i < taskCount; i++)
                _plannedSpawnCounts.Enqueue(_resolvedTask.ResolveSpawnCount(previewRng));
        }

        private int PeekPlannedSpawnCount(System.Random rng)
        {
            if (_plannedSpawnCounts.Count > 0)
                return _plannedSpawnCounts.Peek();

            return _resolvedTask != null ? _resolvedTask.ResolveSpawnCount(rng) : 0;
        }

        private void ApplySnapshotIfAvailable()
        {
            LevelSnapshot snapshot = GameStateMachine.GetInstance()?.CurrentRun?.levelSnapshot;
            if (snapshot == null || snapshot.runtimeSnapshotVersion <= 0)
                return;

            string snapshotId = GetSnapshotId();
            if (snapshot.localSpawners != null)
            {
                for (int i = 0; i < snapshot.localSpawners.Count; i++)
                {
                    LocalSpawnerSnapshotSave spawnerSnapshot = snapshot.localSpawners[i];
                    if (spawnerSnapshot != null && string.Equals(spawnerSnapshot.spawnerId, snapshotId, StringComparison.Ordinal))
                    {
                        RestoreSnapshot(spawnerSnapshot);
                        return;
                    }
                }
            }

        }

        private bool IsFinished()
        {
            if (_restoreFinished)
                return true;

            if (_resolvedPlan == null)
                return false;

            if (_resolvedPlan.spawnMode == LocalEnemySpawnMode.SpawnOnce)
                return _executedTaskCount > 0;

            if (_resolvedPlan.taskTotalMode == LocalEnemySpawnTaskTotalMode.FixedCount)
                return _executedTaskCount >= Mathf.Max(1, _resolvedPlan.totalTaskCount);

            return false;
        }

        private IEnumerator ExecuteInitialTaskAfterActivation()
        {
            yield return null;
            while (_isActivated && !_initialTaskStarted && !_restoreFinished)
            {
                ExecuteInitialTaskOnce();
                if (_initialTaskStarted || _restoreFinished)
                    yield break;

                yield return new WaitForSeconds(1f);
            }
        }

        private void ExecuteInitialTaskOnce()
        {
            if (_initialTaskStarted || !_isActivated || _restoreFinished)
                return;

            if (!TryExecuteOneTaskInternal())
                return;

            _initialTaskStarted = true;

            if (_resolvedPlan == null || _resolvedPlan.spawnMode == LocalEnemySpawnMode.SpawnOnce)
            {
                MarkFinished();
                return;
            }

            _taskTimer = Mathf.Max(0.1f, _resolvedPlan.taskInterval);
        }

        private void MarkFinished()
        {
            _restoreFinished = true;
            enabled = false;
        }

        private System.Random CreateTaskRandom(int attemptCount)
        {
            int salt = unchecked((int)0x9E3779B9);
            return new System.Random(unchecked(_seed ^ (attemptCount * salt)));
        }
    }
}
