using System.Collections;
using Game.Data;
using Game.Domain;
using Game.Online;
using Game.Presentation;
using Game.Saving;
using Game.UI;
using ProjectBase;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Game.GameFlow
{
    /// <summary>
    /// 最终 Boss 决战场景运行时辅助：清空普通关卡玩法对象、刷出指定 Boss，并处理击败后的流转。
    /// </summary>
    public static class FinalBossDuelSceneRuntime
    {
        private const float BossResultPanelDelay = 1.5f;
        private static bool _transitioning;
        private static bool _onlineVictoryAidJoiner;

        public static bool IsFinalBossDuelScene()
        {
            return IsFinalBossDuelSceneName(SceneManager.GetActiveScene().name);
        }

        public static bool IsFinalBossDuelSceneName(string sceneName)
        {
            return string.Equals(sceneName, FinalBossDuelRuntimeContext.SceneName, System.StringComparison.OrdinalIgnoreCase);
        }

        public static void TryAdoptPreviewContext(GameStateMachine gsm, ref RunData run, ref PlayerModel playerModel)
        {
            if (!IsFinalBossDuelScene())
                return;

            if (!FinalBossDuelRuntimeContext.IsActive)
                FinalBossDuelRuntimeContext.EnsurePreviewContext();

            if (gsm == null)
                return;

            run = gsm.CurrentRun;
            playerModel = gsm.Player;
        }

        public static bool TryPrepareScene(MonoBehaviour host)
        {
            if (!IsFinalBossDuelScene())
                return false;

            _transitioning = false;
            _onlineVictoryAidJoiner = false;
            StripRegularLevelGameplayObjects();
            RegisterBossListener();
            if (ShouldWaitForOnlineAuthorityBoss())
            {
                return true;
            }

            host.StartCoroutine(SpawnSelectedBossNextFrame());
            return true;
        }

        private static bool ShouldWaitForOnlineAuthorityBoss()
        {
            OnlineDungeonSessionCoordinator coordinator = OnlineDungeonSessionCoordinator.GetInstance();
            bool wait = coordinator.ShouldWaitForOnlineFinalBossAuthority();
            return wait;
        }

        private static IEnumerator SpawnSelectedBossNextFrame()
        {
            yield return null;

            if (_transitioning || HasLivingFinalBoss())
            {
                yield break;
            }

            if (!TryResolveBossSpawnPosition(out Vector3 position))
            {
                Debug.LogWarning("[FinalBossDuelSceneRuntime] 未能找到决战 Boss 的有效生成点。");
                yield break;
            }

            EnemySpawnVariantCatalog catalog = EnemySpawnRuntime.BuildVariantCatalog();
            if (!EnemySpawnRuntime.TrySpawnSingleEnemy(
                    FinalBossDuelRuntimeContext.CurrentBossId,
                    EnemyType.Boss,
                    position,
                    catalog,
                    true))
            {
                Debug.LogWarning($"[FinalBossDuelSceneRuntime] 决战 Boss 生成失败：{FinalBossDuelRuntimeContext.CurrentBossId}");
            }
        }

        private static void HandleBossDefeated(string bossId)
        {
            if (_transitioning)
                return;

            if (!string.Equals(bossId, FinalBossDuelRuntimeContext.CurrentBossId, System.StringComparison.OrdinalIgnoreCase))
                return;

            OnlineDungeonSessionCoordinator onlineCoordinator = OnlineDungeonSessionCoordinator.GetInstance();
            if (onlineCoordinator != null && onlineCoordinator.HasActiveSession)
            {
                _transitioning = true;
                _onlineVictoryAidJoiner = onlineCoordinator.IsAidJoinerRole;
                CleanupListeners();
                MonoMgr.GetInstance().StartCoroutine(ShowOnlineBossVictoryFlowAfterDelay(bossId));
                return;
            }

            _transitioning = true;
            _onlineVictoryAidJoiner = false;
            CleanupListeners();
            MonoMgr.GetInstance().StartCoroutine(ShowBossResultPanelAfterDelay(bossId));
        }

        private static IEnumerator ShowBossResultPanelAfterDelay(string bossId)
        {
            if (BossResultPanelDelay > 0f)
                yield return new WaitForSecondsRealtime(BossResultPanelDelay);

            if (TryPlayBossVictoryCutscene(bossId, () => CompleteBossVictoryFlow(bossId)))
                yield break;

            CompleteBossVictoryFlow(bossId);
        }

        private static IEnumerator ShowOnlineBossVictoryFlowAfterDelay(string bossId)
        {
            if (BossResultPanelDelay > 0f)
                yield return new WaitForSecondsRealtime(BossResultPanelDelay);

            if (TryPlayBossVictoryCutscene(bossId, () => CompleteOnlineBossVictoryFlow(bossId)))
                yield break;

            CompleteOnlineBossVictoryFlow(bossId);
        }

        private static bool TryPlayBossVictoryCutscene(string bossId, System.Action onComplete)
        {
            BossVictoryCutsceneConfigSO config = ConfigManager.GetInstance()?.GetBossVictoryCutsceneConfig();
            if (config == null)
            {
                Debug.LogWarning("[FinalBossDuelSceneRuntime] 未找到 Boss 胜利过场动画配置。");
                return false;
            }

            VideoClip video = config.GetCutsceneVideo(bossId);
            if (video == null)
            {
                Debug.LogWarning($"[FinalBossDuelSceneRuntime] Boss 未配置胜利过场视频：{bossId}");
                return false;
            }

            UIManager ui = UIManager.GetInstance();
            if (ui == null)
                return false;

            ui.ShowPanel<BossVictoryCutscenePanel>(
                PanelNames.BossVictoryCutscene,
                PanelLayers.BossVictoryCutscene,
                panel => panel.Play(video, onComplete));
            return true;
        }

        public static bool IsBossVictoryTransitioning()
        {
            return _transitioning && IsFinalBossDuelScene();
        }

        private static void CompleteOnlineBossVictoryFlow(string bossId)
        {
            OnlineDungeonSessionCoordinator onlineCoordinator = OnlineDungeonSessionCoordinator.GetInstance();
            bool wasAidJoiner = _onlineVictoryAidJoiner;
            if (wasAidJoiner)
                HideBossVictoryCutscenePanel();

            if (onlineCoordinator != null && onlineCoordinator.TryHandleBossDefeatedSessionEnd())
            {
                _transitioning = false;
                _onlineVictoryAidJoiner = false;
                return;
            }

            if (wasAidJoiner)
            {
                _transitioning = false;
                _onlineVictoryAidJoiner = false;
                return;
            }

            _onlineVictoryAidJoiner = false;
            CompleteBossVictoryFlow(bossId);
        }

        private static void CompleteBossVictoryFlow(string bossId)
        {
            ShowBossResultPanel(bossId);
            _transitioning = false;
        }

        private static void ShowBossResultPanel(string bossId)
        {
            GameStateMachine gsm = GameStateMachine.GetInstance();
            UIManager ui = UIManager.GetInstance();
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
                    () =>
                    {
                        HideBossVictoryCutscenePanel();
                        FinalBossDuelRuntimeContext.ClearChallenge();
                        gsm.RestartCurrentLevelAtEntrance();
                    },
                    hasNextLevel
                        ? () =>
                        {
                            HideBossVictoryCutscenePanel();
                            gsm.RecordBossDefeat(bossId, false);
                            FinalBossDuelRuntimeContext.ClearChallenge();
                            gsm.TryAdvanceToNextLevel();
                        }
                        : () =>
                        {
                            HideBossVictoryCutscenePanel();
                            gsm.RecordBossDefeat(bossId, false);
                            if (!FinalBossDuelRuntimeContext.ExitToOrigin())
                            {
                                FinalBossDuelRuntimeContext.ClearChallenge();
                                Debug.LogWarning("[FinalBossDuelSceneRuntime] 返回原场景失败，未能完成继续探索。");
                            }
                        }));
        }

        private static void HideBossVictoryCutscenePanel()
        {
            UIManager.GetInstance()?.HidePanel(PanelNames.BossVictoryCutscene);
        }

        private static void RegisterBossListener()
        {
            CleanupListeners();
            EventCenter.GetInstance().AddEventListener<string>(GameEvents.BossDefeated, HandleBossDefeated);
        }

        private static void CleanupListeners()
        {
            EventCenter.GetInstance().RemoveEventListener<string>(GameEvents.BossDefeated, HandleBossDefeated);
        }

        private static void StripRegularLevelGameplayObjects()
        {
            DestroyOwnerGameObjects<LevelEnemySpawner>(FindObjectsInactive.Include);
            DestroyOwnerGameObjects<LevelLocalEnemySpawner>(FindObjectsInactive.Include);
            DestroyOwnerGameObjects<LevelDirector>(FindObjectsInactive.Include);
            DestroyOwnerGameObjects<ChestInteractable>(FindObjectsInactive.Include);
            DestroyOwnerGameObjects<HutaoShopInteractable>(FindObjectsInactive.Include);
            DestroyOwnerGameObjects<DroppedPickupRuntime>(FindObjectsInactive.Include);
            DestroyOwnerGameObjects<EnemyController>(FindObjectsInactive.Include);
        }

        private static void DestroyOwnerGameObjects<T>(FindObjectsInactive includeInactive) where T : Component
        {
            T[] components = Object.FindObjectsByType<T>(includeInactive, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null)
                    continue;

                Object.Destroy(component.gameObject);
            }
        }

        private static bool HasLivingFinalBoss()
        {
            EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy != null &&
                    enemy.IsAlive &&
                    enemy.EnemyCategory == EnemyType.Boss &&
                    enemy.GetComponent<LevelBossVisualMarker>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveBossSpawnPosition(out Vector3 position)
        {
            Transform player = GameStateMachine.GetInstance()?.LevelPlayerTransform;
            if (player == null)
            {
                position = Vector3.zero;
                return false;
            }

            Vector3 center = player.position + player.forward * 18f;
            if (NavMesh.SamplePosition(center, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }

            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f * Mathf.Deg2Rad;
                Vector3 candidate = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 18f;
                if (NavMesh.SamplePosition(candidate, out hit, 6f, NavMesh.AllAreas))
                {
                    position = hit.position;
                    return true;
                }
            }

            position = player.position + player.forward * 12f;
            return true;
        }
    }
}
