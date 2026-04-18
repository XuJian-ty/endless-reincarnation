using Game.Data;
using Game.Domain;
using Game.Saving;
using Game.UI;
using ProjectBase;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.GameFlow
{
    /// <summary>
    /// 最终 Boss 决战运行时上下文：负责记录当前决战 Boss，并进入决战场景。
    /// </summary>
    public static class FinalBossDuelRuntimeContext
    {
        public const string SceneName = "FinalBossDuelScene";

        private static string _bossId = string.Empty;
        private static string _bossDisplayName = string.Empty;
        private static string _originSceneName = string.Empty;

        public static bool IsActive => !string.IsNullOrWhiteSpace(_bossId);
        public static string CurrentBossId => _bossId ?? string.Empty;
        public static string CurrentBossDisplayName => _bossDisplayName ?? string.Empty;

        public static bool BeginChallenge(string bossId, string bossDisplayName = null)
        {
            if (string.IsNullOrWhiteSpace(bossId))
                return false;

            if (!Application.CanStreamedLevelBeLoaded(SceneName))
            {
                Debug.LogError($"[FinalBossDuelRuntimeContext] 场景 '{SceneName}' 未加入 Build Settings，无法进入决战。");
                return false;
            }

            GameStateMachine gsm = GameStateMachine.GetInstance();
            RunData run = gsm?.CurrentRun;
            if (gsm == null || run == null)
                return false;

            gsm.Player?.SaveTo(run);
            Object.FindFirstObjectByType<LevelBootstrapper>()?.CaptureRuntimeSnapshot();
            MarkOriginSnapshotBossTransitionStarted(run, bossId);

            _bossId = bossId.Trim();
            _bossDisplayName = string.IsNullOrWhiteSpace(bossDisplayName) ? _bossId : bossDisplayName.Trim();
            _originSceneName = SceneManager.GetActiveScene().name;
            LoadSceneWithMainMenuStyleTransition(SceneName);
            return true;
        }

        public static bool EnsurePreviewContext()
        {
            if (IsActive)
                return true;

            if (!TryResolveDefaultBoss(out string bossId, out string bossDisplayName))
                return false;

            _bossId = bossId;
            _bossDisplayName = bossDisplayName;
            return true;
        }

        public static void ClearChallenge()
        {
            _bossId = string.Empty;
            _bossDisplayName = string.Empty;
            _originSceneName = string.Empty;
        }

        public static bool ExitToOrigin()
        {
            GameStateMachine gsm = GameStateMachine.GetInstance();
            string targetSceneName = _originSceneName;
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                RunData run = gsm?.CurrentRun;
                if (run != null)
                    targetSceneName = GameStateMachine.GetLevelSceneName(run.levelIndex);
            }

            if (string.IsNullOrWhiteSpace(targetSceneName))
                return false;

            ClearChallenge();
            LoadSceneWithMainMenuStyleTransition(targetSceneName);
            return true;
        }

        private static bool TryResolveDefaultBoss(out string bossId, out string bossDisplayName)
        {
            bossId = string.Empty;
            bossDisplayName = string.Empty;

            EnemySpawnVariantCatalog catalog = EnemySpawnRuntime.BuildVariantCatalog();
            var bosses = catalog?.GetVariants(EnemyType.Boss);
            if (bosses == null || bosses.Count == 0)
                return false;

            EnemySpawnVariantInfo variant = bosses[0];
            if (variant == null || string.IsNullOrWhiteSpace(variant.spawnId))
                return false;

            bossId = variant.spawnId.Trim();
            bossDisplayName = string.IsNullOrWhiteSpace(variant.displayName) ? bossId : variant.displayName.Trim();
            return true;
        }

        /// <summary>
        /// 进入决战场景前，修正原关卡快照中的 Boss 流程状态，防止返回原场景时重复触发决战入口。
        /// </summary>
        private static void MarkOriginSnapshotBossTransitionStarted(RunData run, string bossId)
        {
            if (run?.levelSnapshot?.levelDirector == null)
                return;

            LevelDirectorSnapshotSave directorSnapshot = run.levelSnapshot.levelDirector;
            directorSnapshot.bossSpawned = true;
            directorSnapshot.bossDefeated = false;
            directorSnapshot.hasPendingBossSpawn = false;
            directorSnapshot.bossSpawnRemainingTime = 0f;
            directorSnapshot.hasPendingBossResult = false;
            directorSnapshot.bossResultPanelOpen = false;
            directorSnapshot.bossResultRemainingTime = 0f;
            directorSnapshot.defeatedBossId = string.IsNullOrWhiteSpace(bossId)
                ? directorSnapshot.defeatedBossId
                : bossId.Trim();
        }

        /// <summary>
        /// 场景切换时复用主菜单同款加载表现：MainMenuBackground + LoadingPanel。
        /// PromptClickToContinue 仅首次进入主菜单显示，这里不显示。
        /// </summary>
        private static void LoadSceneWithMainMenuStyleTransition(string sceneName)
        {
            UIManager ui = UIManager.GetInstance();
            if (ui == null)
            {
                ScenesMgr.GetInstance().LoadSceneAsyn(sceneName, null);
                return;
            }

            MainMenuBackgroundPanel.RequestLoadingTransitionShow();
            ui.ShowPanel<MainMenuBackgroundPanel>(PanelNames.MainMenuBackground, PanelLayers.MainMenuBackground);
            ui.ShowPanel<LoadingPanel>(PanelNames.Loading, PanelLayers.Loading, _ =>
            {
                ScenesMgr.GetInstance().LoadSceneAsyn(sceneName, () =>
                {
                    ui.HidePanel(PanelNames.Loading);
                    ui.HidePanel(PanelNames.MainMenuBackground);
                });
            });
        }
    }
}
