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
    /// 战斗回忆运行时上下文：进入回忆时切到临时挑战数据，退出时恢复原关卡与原存档上下文。
    /// </summary>
    public static class BattleMemoryRuntimeContext
    {
        public const string SceneName = "BattleMemoryScene";

        private sealed class OriginContextSnapshot
        {
            public string saveId;
            public string playerName;
            public string sceneName;
            public RunData run;
        }

        private sealed class BattleContextSnapshot
        {
            public string playerName;
            public string bossId;
            public string bossDisplayName;
            public RunData templateRun;
        }

        private static OriginContextSnapshot _origin;
        private static BattleContextSnapshot _battle;

        public static bool IsActive => _battle != null;
        public static string CurrentBossId => _battle?.bossId ?? string.Empty;
        public static string CurrentBossDisplayName => _battle?.bossDisplayName ?? string.Empty;

        public static bool BeginChallenge(string bossId, string bossDisplayName)
        {
            if (string.IsNullOrWhiteSpace(bossId))
                return false;

            GameStateMachine gsm = GameStateMachine.GetInstance();
            RunData sourceRun = gsm?.CurrentRun;
            if (gsm == null || sourceRun == null)
                return false;

            gsm.Player?.SaveTo(sourceRun);
            UnityEngine.Object.FindFirstObjectByType<LevelBootstrapper>()?.CaptureRuntimeSnapshot();

            _origin = new OriginContextSnapshot
            {
                saveId = gsm.CurrentSaveId,
                playerName = gsm.CurrentPlayerName,
                sceneName = SceneManager.GetActiveScene().name,
                run = SaveSystem.CloneRunData(sourceRun),
            };
            _battle = CreateBattleSnapshot(sourceRun, gsm.CurrentPlayerName, bossId, bossDisplayName);
            if (!AdoptBattleContext(gsm))
            {
                Clear();
                return false;
            }

            LoadSceneWithMainMenuStyleTransition(SceneName);
            return true;
        }

        public static bool EnsurePreviewContext()
        {
            if (IsActive)
                return true;

            GameStateMachine gsm = GameStateMachine.GetInstance();
            RunData sourceRun = gsm?.CurrentRun;
            if (gsm == null || sourceRun == null)
                return false;

            if (!TryResolveDefaultBoss(out string bossId, out string bossDisplayName))
                return false;

            _origin = null;
            _battle = CreateBattleSnapshot(sourceRun, gsm.CurrentPlayerName, bossId, bossDisplayName);
            if (AdoptBattleContext(gsm))
                return true;

            Clear();
            return false;
        }

        public static bool RestartChallenge()
        {
            if (!IsActive)
                return false;

            GameStateMachine gsm = GameStateMachine.GetInstance();
            if (gsm == null)
                return false;

            if (!AdoptBattleContext(gsm))
                return false;

            LoadSceneWithMainMenuStyleTransition(SceneName);
            return true;
        }

        public static bool ExitToOrigin()
        {
            GameStateMachine gsm = GameStateMachine.GetInstance();
            if (gsm == null)
                return false;

            OriginContextSnapshot origin = _origin;
            Clear();

            if (origin?.run != null)
            {
                RunData restoredRun = SaveSystem.CloneRunData(origin.run);
                PlayerModel playerModel = CreatePlayerModel(restoredRun);
                gsm.AdoptRuntimeContext(restoredRun, playerModel, origin.playerName, origin.saveId);

                string sceneName = !string.IsNullOrWhiteSpace(origin.sceneName)
                    ? origin.sceneName
                    : GameStateMachine.GetLevelSceneName(restoredRun.levelIndex);
                LoadSceneWithMainMenuStyleTransition(sceneName);
                return true;
            }

            gsm.SaveAndQuit();
            return true;
        }

        private static bool AdoptBattleContext(GameStateMachine gsm)
        {
            if (gsm == null || _battle?.templateRun == null)
                return false;

            RunData run = SaveSystem.CloneRunData(_battle.templateRun);
            PlayerModel playerModel = CreatePlayerModel(run);
            gsm.AdoptRuntimeContext(run, playerModel, _battle.playerName, null);
            return true;
        }

        private static BattleContextSnapshot CreateBattleSnapshot(RunData sourceRun, string playerName, string bossId, string bossDisplayName)
        {
            RunData templateRun = SaveSystem.CloneRunData(sourceRun) ?? SaveSystem.NewGameRun().run;
            templateRun.levelSnapshot = null;
            templateRun.checkpoint = null;
            templateRun.pendingBuffSelection = false;

            PlayerModel playerModel = CreatePlayerModel(templateRun);
            playerModel.CurrentHp = playerModel.Stats.MaxHp;
            playerModel.CurrentMp = playerModel.Stats.MaxMp;
            playerModel.SaveTo(templateRun);

            return new BattleContextSnapshot
            {
                playerName = string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName.Trim(),
                bossId = bossId.Trim(),
                bossDisplayName = string.IsNullOrWhiteSpace(bossDisplayName) ? bossId.Trim() : bossDisplayName.Trim(),
                templateRun = templateRun,
            };
        }

        private static PlayerModel CreatePlayerModel(RunData run)
        {
            PlayerModel playerModel = new PlayerModel(ConfigManager.GetInstance()?.GetLevelGrowth());
            playerModel.LoadFrom(run);
            BuffConfigSO buffConfig = ConfigManager.GetInstance()?.GetBuffConfig();
            if (buffConfig != null)
                playerModel.ReapplyBuffModifiers(buffConfig.GetModifierForBuff);
            return playerModel;
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

        private static void Clear()
        {
            _origin = null;
            _battle = null;
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
