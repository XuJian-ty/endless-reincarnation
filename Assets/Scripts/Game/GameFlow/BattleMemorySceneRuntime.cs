using System.Collections;
using Game.Data;
using Game.Domain;
using Game.Presentation;
using Game.Saving;
using Game.UI;
using ProjectBase;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Game.GameFlow
{
    /// <summary>
    /// 战斗回忆场景运行时辅助：清空普通关卡玩法对象、刷出选中的 Boss，并处理回忆战结束流转。
    /// </summary>
    public static class BattleMemorySceneRuntime
    {
        private const string VictoryNotice = "回忆成功，即将返回原场景";
        private const string RetryNotice = "回忆破碎，即将返回原场景";
        private const float NoticeDuration = 1.5f;

        private static bool _transitioning;

        public static bool IsBattleMemoryScene()
        {
            return IsBattleMemorySceneName(SceneManager.GetActiveScene().name);
        }

        public static bool IsBattleMemorySceneName(string sceneName)
        {
            return string.Equals(sceneName, BattleMemoryRuntimeContext.SceneName, System.StringComparison.OrdinalIgnoreCase);
        }

        public static void TryAdoptPreviewContext(GameStateMachine gsm, ref RunData run, ref PlayerModel playerModel)
        {
            if (!IsBattleMemoryScene())
                return;

            if (!BattleMemoryRuntimeContext.IsActive)
                BattleMemoryRuntimeContext.EnsurePreviewContext();

            if (gsm == null)
                return;

            run = gsm.CurrentRun;
            playerModel = gsm.Player;
        }

        public static bool TryPrepareScene(MonoBehaviour host)
        {
            if (!IsBattleMemoryScene())
                return false;

            _transitioning = false;
            StripRegularLevelGameplayObjects();
            RegisterBossListener();
            GameStateMachine.GetInstance()?.SetDeathChoiceHandler(new BattleMemoryDeathChoiceHandler());
            host.StartCoroutine(SpawnSelectedBossNextFrame());
            return true;
        }

        public static bool ExitToOrigin()
        {
            CleanupListeners();
            return BattleMemoryRuntimeContext.ExitToOrigin();
        }

        private static IEnumerator SpawnSelectedBossNextFrame()
        {
            yield return null;

            if (_transitioning || HasLivingMemoryBoss())
                yield break;

            if (!TryResolveBossSpawnPosition(out Vector3 position))
            {
                Debug.LogWarning("[BattleMemorySceneRuntime] 未能找到战斗回忆 Boss 的有效生成点。");
                yield break;
            }

            EnemySpawnVariantCatalog catalog = EnemySpawnRuntime.BuildVariantCatalog();
            if (!EnemySpawnRuntime.TrySpawnSingleEnemy(
                    BattleMemoryRuntimeContext.CurrentBossId,
                    EnemyType.Boss,
                    position,
                    catalog,
                    true))
            {
                Debug.LogWarning($"[BattleMemorySceneRuntime] 战斗回忆 Boss 生成失败：{BattleMemoryRuntimeContext.CurrentBossId}");
            }
        }

        private static void HandleBossDefeated(string bossId)
        {
            if (_transitioning)
                return;

            if (!string.Equals(bossId, BattleMemoryRuntimeContext.CurrentBossId, System.StringComparison.OrdinalIgnoreCase))
                return;

            _transitioning = true;
            ShowNotice(VictoryNotice, NoticeDuration);
            CleanupListeners();
            MonoMgr.GetInstance().StartCoroutine(ReturnToOriginAfterDelay());
        }

        private static IEnumerator ReturnToOriginAfterDelay()
        {
            yield return new WaitForSecondsRealtime(NoticeDuration);
            _transitioning = false;
            BattleMemoryRuntimeContext.ExitToOrigin();
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

        private static bool HasLivingMemoryBoss()
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

        private static void ShowNotice(string message, float duration)
        {
            UIManager ui = UIManager.GetInstance();
            if (ui == null)
                return;

            ui.ShowPanel<BossArrivalNoticePanel>(
                PanelNames.BossArrivalNotice,
                PanelLayers.BossArrivalNotice,
                panel => panel.ShowNotice(message, duration));
        }

        private sealed class BattleMemoryDeathChoiceHandler : IDeathChoiceHandler
        {
            public void RequestDeathChoice(System.Action onReviveWithNectar, System.Action onGiveUp)
            {
                if (_transitioning)
                    return;

                _transitioning = true;
                ShowNotice(RetryNotice, NoticeDuration);
                CleanupListeners();
                MonoMgr.GetInstance().StartCoroutine(ReturnToOriginAfterDelay());
            }
        }
    }
}
