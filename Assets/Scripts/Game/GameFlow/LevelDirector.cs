using UnityEngine;
using Game;
using Game.Data;
using Game.Saving;
using ProjectBase;

namespace Game.GameFlow
{
    /// <summary>
    /// 关卡导演：负责关卡内阶段逻辑（守卫者计数、Boss 降临、击败后 Checkpoint 与选关）。
    /// 挂在关卡场景空物体上，与 LevelBootstrapper 配合；具体刷怪/生成由后续实现。
    /// </summary>
    public class LevelDirector : MonoBehaviour
    {
        // 配置由 ConfigManager 单例提供，无需挂载

        [Header("Boss 进度（可选，不赋值则使用默认逻辑）")]
        private IBossProgressService _bossProgressService;

        /// <summary>外部注入 Boss 进度服务（可选），不注入则使用默认实现</summary>
        public void SetBossProgressService(IBossProgressService service) => _bossProgressService = service;

        private int _guardiansAlive;
        private bool _bossSpawned;

        private void Start()
        {
            var db = ConfigManager.GetInstance().GetLevelConfigDatabase();
            if (db != null)
            {
                var run = GameStateMachine.GetInstance()?.CurrentRun;
                int level = run?.levelIndex ?? 1;
                var data = db.GetConfigForLevel(level);
                if (data != null) _guardiansAlive = data.guardianCount;
            }
            _bossSpawned = false;

            if (_bossProgressService == null)
                _bossProgressService = new DefaultBossProgressService();

            EventCenter.GetInstance().AddEventListener(GameEvents.GuardianDied, OnGuardianDied);
            EventCenter.GetInstance().AddEventListener<string>(GameEvents.BossDefeated, OnBossDefeated);
        }

        private void OnDestroy()
        {
            var ec = EventCenter.GetInstance();
            if (ec != null)
            {
                ec.RemoveEventListener(GameEvents.GuardianDied, OnGuardianDied);
                ec.RemoveEventListener<string>(GameEvents.BossDefeated, OnBossDefeated);
            }
        }

        private void OnGuardianDied()
        {
            if (_guardiansAlive <= 0) return;
            _guardiansAlive--;
            if (_guardiansAlive == 0 && !_bossSpawned)
                EventCenter.GetInstance().EventTrigger(GameEvents.BossSpawnRequest);
        }

        private void OnBossDefeated(string bossId)
        {
            _bossSpawned = true;
            _bossProgressService?.OnBossDefeated(bossId);
        }

        /// <summary>默认实现：写 Checkpoint、保存、后续可弹选关面板</summary>
        private sealed class DefaultBossProgressService : IBossProgressService
        {
            public void OnBossDefeated(string bossId)
            {
                var gsm = GameStateMachine.GetInstance();
                var run = gsm?.CurrentRun;
                if (run == null) return;

                run.checkpoint = SaveSystem.CloneRunData(run);
                if (!string.IsNullOrEmpty(bossId))
                    run.defeatedBossIds.Add(bossId);
                gsm.SaveCurrent();
                // TODO: UIManager.ShowPanel<BossClearPanel>(...);
            }
        }
    }
}
