using UnityEngine;
using Game;
using Game.Saving;
using Game.UI;
using Game.Data;
using Game.Presentation;
using ProjectBase;
using UnityEngine.SceneManagement;

namespace Game.GameFlow
{
    /// <summary>
    /// 关卡导演：负责关卡内阶段逻辑（守卫者计数、Boss 降临、击败后 Checkpoint 与选关）。
    /// 挂在关卡场景空物体上，与 LevelBootstrapper、全局/局部生成器配合。
    /// </summary>
    public class LevelDirector : MonoBehaviour
    {
        private const float GuardianStateCheckInterval = 0.2f;

        // 配置由 ConfigManager 单例提供，无需挂载
        [Header("Boss 流程")]
        [SerializeField] [Tooltip("守卫者清空后，延迟多少秒生成 Boss。")]
        private float _bossSpawnDelay = 5f;

        [Header("生成器")]
        [SerializeField] [Tooltip("关卡怪物生成器。建议在场景中显式挂载，而不是运行时自动创建。")]
        private LevelEnemySpawner _enemySpawner;

        [Header("Boss 进度（可选，不赋值则使用默认逻辑）")]
        private IBossProgressService _bossProgressService;

        /// <summary>外部注入 Boss 进度服务（可选），不注入则使用默认实现</summary>
        public void SetBossProgressService(IBossProgressService service) => _bossProgressService = service;

        private System.Random _bossRng;
        private EnemySpawnVariantCatalog _bossVariantCatalog;
        private bool _bossSpawned;
        private bool _bossDefeated;
        private Coroutine _bossSpawnRoutine;
        private float _guardianStateCheckTimer;

        private void Awake()
        {
            _enemySpawner = EnsureEnemySpawnerReference(true);
            _bossSpawned = false;
            _bossDefeated = false;

            if (_bossProgressService == null)
                _bossProgressService = new DefaultBossProgressService();

            int seed = GameStateMachine.GetInstance()?.CurrentRun?.levelSnapshot?.seed ?? System.Environment.TickCount;
            _bossRng = new System.Random(seed ^ 0x53B7);
            _bossVariantCatalog = EnemySpawnRuntime.BuildVariantCatalog();
        }

        private void Start()
        {
            _enemySpawner = EnsureEnemySpawnerReference(true);
            if (_enemySpawner == null && FindFirstObjectByType<LevelLocalEnemySpawner>() == null)
                Debug.LogWarning("[LevelDirector] 场景中未找到 LevelEnemySpawner。若当前关卡不使用全局生成器，可忽略此提示。", this);

            EventCenter.GetInstance().AddEventListener(GameEvents.GuardianDied, OnGuardianDied);
            EventCenter.GetInstance().AddEventListener<string>(GameEvents.BossDefeated, OnBossDefeated);

            TryScheduleBossSpawnIfReady();
        }

        private void Update()
        {
            if (_bossSpawned || _bossDefeated || _bossSpawnRoutine != null)
                return;

            _guardianStateCheckTimer -= Time.deltaTime;
            if (_guardianStateCheckTimer > 0f)
                return;

            _guardianStateCheckTimer = GuardianStateCheckInterval;
            TryScheduleBossSpawnIfReady();
        }

        private void OnDestroy()
        {
            var ec = EventCenter.GetInstance();
            if (ec != null)
            {
                ec.RemoveEventListener(GameEvents.GuardianDied, OnGuardianDied);
                ec.RemoveEventListener<string>(GameEvents.BossDefeated, OnBossDefeated);
            }

            if (_bossSpawnRoutine != null)
                StopCoroutine(_bossSpawnRoutine);
        }

        private void OnGuardianDied()
        {
            TryScheduleBossSpawnIfReady();
        }

        private void OnBossDefeated(string bossId)
        {
            if (_bossDefeated)
                return;

            _bossDefeated = true;
            _bossSpawned = true;
            if (_bossSpawnRoutine != null)
            {
                StopCoroutine(_bossSpawnRoutine);
                _bossSpawnRoutine = null;
            }

            _bossProgressService?.OnBossDefeated(bossId);
            ShowBossResultPanel(bossId);
        }

        private void ScheduleBossSpawn()
        {
            if (_bossSpawned || _bossDefeated || _bossSpawnRoutine != null)
                return;

            _bossSpawnRoutine = StartCoroutine(BossSpawnCountdown());
        }

        private void TryScheduleBossSpawnIfReady()
        {
            if (_bossSpawned || _bossDefeated || _bossSpawnRoutine != null)
                return;

            if (GetLivingGuardianCount() > 0)
                return;

            int pendingGuardians = GetPendingGuardianCount();
            if (pendingGuardians != 0)
                return;

            ScheduleBossSpawn();
        }

        private int GetLivingGuardianCount()
        {
            int total = 0;
            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy != null && enemy.IsAlive && enemy.EnemyCategory == EnemyType.Guardian)
                    total++;
            }

            return total;
        }

        private int GetPendingGuardianCount()
        {
            int total = 0;
            LevelEnemySpawner enemySpawner = EnsureEnemySpawnerReference(true);
            if (enemySpawner != null)
            {
                int pending = enemySpawner.GetPendingGuardianCount();
                if (pending == int.MaxValue)
                    return int.MaxValue;
                total += pending;
            }

            LevelLocalEnemySpawner[] localSpawners = FindObjectsByType<LevelLocalEnemySpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < localSpawners.Length; i++)
            {
                LevelLocalEnemySpawner localSpawner = localSpawners[i];
                if (localSpawner == null)
                    continue;

                int pending = localSpawner.GetPendingGuardianCount();
                if (pending == int.MaxValue)
                    return int.MaxValue;
                total += pending;
            }

            return total;
        }

        private System.Collections.IEnumerator BossSpawnCountdown()
        {
            float delay = Mathf.Max(0f, _bossSpawnDelay);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (_bossDefeated || _bossSpawned)
            {
                _bossSpawnRoutine = null;
                yield break;
            }

            if (TrySpawnBoss())
                _bossSpawned = true;
            else
                Debug.LogWarning("[LevelDirector] Boss 生成失败。");

            _bossSpawnRoutine = null;
        }

        private void ShowBossResultPanel(string bossId)
        {
            var gsm = GameStateMachine.GetInstance();
            var ui = UIManager.GetInstance();
            if (gsm == null || ui == null)
                return;

            bool hasNextLevel = gsm.HasLoadableNextLevel();
            int clearedLevel = gsm.CurrentRun?.levelIndex ?? 1;

            ui.ShowPanel<BossResultPanel>(
                PanelNames.BossResult,
                PanelLayers.BossResult,
                panel => panel.ShowResult(
                    clearedLevel,
                    bossId,
                    hasNextLevel,
                    () => gsm.RestartCurrentLevelAtEntrance(),
                    hasNextLevel ? () => gsm.TryAdvanceToNextLevel() : () => gsm.SaveAndQuit()));
        }

        private bool TrySpawnBoss()
        {
            if (HasLivingBoss())
                return false;

            string bossId = ResolveBossIdForCurrentRun();
            if (string.IsNullOrEmpty(bossId))
                return false;

            if (!TrySampleBossNearPlayer(out Vector3 position))
                return false;

            return EnemySpawnRuntime.TrySpawnSingleEnemy(bossId, EnemyType.Boss, position, _bossVariantCatalog, true);
        }

        private bool HasLivingBoss()
        {
            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (IsTrackedLevelBoss(enemy))
                    return true;
            }

            return false;
        }

        private bool IsTrackedLevelBoss(EnemyController enemy)
        {
            return enemy != null &&
                   enemy.IsAlive &&
                   enemy.EnemyCategory == EnemyType.Boss &&
                   enemy.GetComponent<LevelBossVisualMarker>() != null;
        }

        private string ResolveBossIdForCurrentRun()
        {
            var bossVariants = _bossVariantCatalog?.GetVariants(EnemyType.Boss);
            if (bossVariants == null || bossVariants.Count == 0)
                return string.Empty;

            var run = GameStateMachine.GetInstance()?.CurrentRun;
            if (run?.defeatedBossIds == null || run.defeatedBossIds.Count == 0)
            {
                EnemySpawnVariantInfo randomVariant = bossVariants[_bossRng.Next(0, bossVariants.Count)];
                return randomVariant != null ? randomVariant.spawnId : string.Empty;
            }

            var undefeatedBossIds = new System.Collections.Generic.List<string>();
            for (int i = 0; i < bossVariants.Count; i++)
            {
                EnemySpawnVariantInfo variant = bossVariants[i];
                if (variant == null || string.IsNullOrWhiteSpace(variant.spawnId))
                    continue;

                string bossId = variant.spawnId;
                if (!run.defeatedBossIds.Contains(bossId))
                    undefeatedBossIds.Add(bossId);
            }

            if (undefeatedBossIds.Count == 0)
            {
                EnemySpawnVariantInfo randomVariant = bossVariants[_bossRng.Next(0, bossVariants.Count)];
                return randomVariant != null ? randomVariant.spawnId : string.Empty;
            }

            return undefeatedBossIds[_bossRng.Next(0, undefeatedBossIds.Count)];
        }

        private bool TrySampleBossNearPlayer(out Vector3 position)
        {
            Transform player = GameStateMachine.GetInstance()?.LevelPlayerTransform;
            if (player == null)
            {
                position = Vector3.zero;
                return false;
            }

            Vector3 center = player.position;
            const float minRadius = 12f;
            const float maxRadius = 40f;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                float angle = (float)(_bossRng.NextDouble() * Mathf.PI * 2f);
                float radius = Mathf.Lerp(minRadius, maxRadius, (float)_bossRng.NextDouble());
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    position = hit.position;
                    return true;
                }
            }

            position = Vector3.zero;
            return false;
        }

        private LevelEnemySpawner EnsureEnemySpawnerReference(bool allowCreate)
        {
            if (_enemySpawner != null)
                return _enemySpawner;

            _enemySpawner = FindFirstObjectByType<LevelEnemySpawner>();
            if (_enemySpawner != null || !allowCreate)
                return _enemySpawner;

            LevelConfigData levelConfig = ResolveCurrentLevelConfig();
            if (levelConfig?.globalSpawnPlanLibrary == null)
                return null;

            GameObject spawnerObject = new GameObject("LevelEnemySpawner");
            Transform parent = transform.parent;
            if (parent != null)
                spawnerObject.transform.SetParent(parent, false);

            _enemySpawner = spawnerObject.AddComponent<LevelEnemySpawner>();
            return _enemySpawner;
        }

        private LevelConfigData ResolveCurrentLevelConfig()
        {
            int level = ResolveCurrentLevelIndex();
            return ConfigManager.GetInstance()?.GetLevelConfigDatabase()?.GetConfigForLevel(level);
        }

        private int ResolveCurrentLevelIndex()
        {
            int level = GameStateMachine.GetInstance()?.CurrentRun?.levelIndex ?? 0;
            if (level > 0)
                return level;

            string activeSceneName = gameObject.scene.IsValid() ? gameObject.scene.name : SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(activeSceneName))
                return 1;

            LevelConfigDatabaseSO configDb = ConfigManager.GetInstance()?.GetLevelConfigDatabase();
            if (configDb?.levels != null)
            {
                for (int i = 0; i < configDb.levels.Count; i++)
                {
                    LevelConfigData config = configDb.levels[i];
                    if (config != null && string.Equals(config.sceneName, activeSceneName, System.StringComparison.OrdinalIgnoreCase))
                        return Mathf.Max(1, config.levelIndex);
                }
            }

            const string levelPrefix = "Level_";
            if (activeSceneName.StartsWith(levelPrefix, System.StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(activeSceneName.Substring(levelPrefix.Length), out int parsedLevel))
            {
                return Mathf.Max(1, parsedLevel);
            }

            return 1;
        }

        /// <summary>默认实现：写 Checkpoint、保存、后续可弹选关面板</summary>
        private sealed class DefaultBossProgressService : IBossProgressService
        {
            public void OnBossDefeated(string bossId)
            {
                var gsm = GameStateMachine.GetInstance();
                gsm?.RecordBossDefeat(bossId);
            }
        }
    }
}
