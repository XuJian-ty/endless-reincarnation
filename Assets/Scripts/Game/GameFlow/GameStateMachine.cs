using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Game;
using Game.Domain;
using Game.Data;
using Game.Saving;
using ProjectBase;

namespace Game.GameFlow
{
    /// <summary>
    /// 死亡时二选一由 UI（如 DeathPanel）处理时实现此接口并注册到 GameStateMachine。
    /// 收到请求后展示面板，用户选择后调用对应 callback。
    /// </summary>
    public interface IDeathChoiceHandler
    {
        void RequestDeathChoice(Action onReviveWithNectar, Action onGiveUp);
    }

    /// <summary>
    /// 游戏流程状态机：主菜单 / 关卡中 / 暂停。
    /// 负责新游戏、读档、进关卡、保存当前进度、响应玩家死亡。
    /// </summary>
    public class GameStateMachine : BaseManager<GameStateMachine>
    {
        // ── 常量 ──────────────────────────────────────────────────────────
        private const int MaxLevelIndex = 5;

        // ── 流程状态枚举 ──────────────────────────────────────────────────
        public enum State
        {
            MainMenu,
            InLevel,
            Paused
        }

        // ── 状态事件（LevelBootstrapper / HUD 等可订阅）──────────────────
        public event Action<State> OnStateChanged;
        public event Action        OnRunStarted;
        public event Action        OnRunEnded;

        // ── 运行时数据 ────────────────────────────────────────────────────
        private State                _state           = State.MainMenu;
        private string               _currentSaveId;
        private string               _currentPlayerName;
        private RunData              _currentRun;
        private PlayerModel          _playerModel;
        private IDeathChoiceHandler  _deathChoiceHandler;
        private Transform            _levelPlayerTransform;

        public State       CurrentState => _state;
        public string      CurrentSaveId => _currentSaveId;
        public string      CurrentPlayerName => _currentPlayerName;
        public RunData     CurrentRun   => _currentRun;
        public PlayerModel Player       => _playerModel;
        public bool IsGameplayPaused => _state == State.Paused;

        /// <summary>当前关卡场景中的玩家 Transform，由 LevelBootstrapper 在 Start 时设置、离开关卡时清空；敌人 AI 用于感知与追击。</summary>
        public Transform LevelPlayerTransform => _levelPlayerTransform;
        public void SetLevelPlayerTransform(Transform t) => _levelPlayerTransform = t;

        // ── 场景名称常量 ──────────────────────────────────────────────────
        private static class SceneNames
        {
            public const string MainMenu    = "MainMenu";
            public const string LevelPrefix = "Level_";
        }

        public static string GetLevelSceneName(int levelIndex)
        {
            levelIndex = Mathf.Clamp(levelIndex, 1, MaxLevelIndex);
            return $"{SceneNames.LevelPrefix}{levelIndex}";
        }

        public bool HasLoadableNextLevel()
        {
            if (_currentRun == null)
                return false;

            int nextLevel = _currentRun.levelIndex + 1;
            if (nextLevel > MaxLevelIndex)
                return false;

            return CanLoadLevelScene(nextLevel);
        }

        // ── 进档（新游戏创建存档后 / 加载存档）────────────────────────────
        /// <summary>从指定存档 id 加载，成功则进入该存档记录的关卡场景并返回 true。新游戏在起名面板创建存档后也调用此方法。</summary>
        public bool LoadGame(string saveId, LevelGrowthSO levelGrowth, UnityAction onSceneLoaded = null)
        {
            if (string.IsNullOrEmpty(saveId)) return false;

            var data = SaveSystem.GetInstance().Load(saveId);
            if (data?.run == null) return false;

            _currentSaveId     = saveId;
            _currentPlayerName = data.playerName ?? "";
            _currentRun        = data.run;
            _playerModel       = new PlayerModel(levelGrowth);
            _playerModel.LoadFrom(_currentRun);
            var buffConfig = ConfigManager.GetInstance()?.GetBuffConfig();
            if (buffConfig != null)
                _playerModel.ReapplyBuffModifiers(buffConfig.GetModifierForBuff);

            // 加载进度条期间不播 BGM，等进度条满、进入关卡后再在回调里播
            MusicMgr.GetInstance().StopBKMusic();

            float bgmVol = _currentRun.levelBgmEnabled ? Mathf.Clamp01(_currentRun.levelBgmVolume) : 0f;
            MusicMgr.GetInstance().ChangeBKValue(bgmVol);
            float sfxVol = _currentRun.soundEffectsEnabled ? Mathf.Clamp01(_currentRun.soundEffectsVolume) : 0f;
            MusicMgr.GetInstance().ChangeSoundValue(sfxVol);

            if (_currentRun.checkpoint == null && _currentRun.levelIndex == 1)
            {
                _currentRun.checkpoint = SaveSystem.CloneRunData(_currentRun);
                SaveSystem.GetInstance().Save(saveId, new SaveData { version = SaveSystem.CurrentVersion, playerName = data.playerName, run = _currentRun });
            }

            SetState(State.InLevel);
            OnRunStarted?.Invoke();
            ScenesMgr.GetInstance().LoadSceneAsyn(
                GetLevelSceneName(_currentRun.levelIndex),
                () =>
                {
                    ApplyLevelBgm();
                    onSceneLoaded?.Invoke();
                });
            return true;
        }

        private void ApplyLevelBgm()
        {
            if (_currentRun == null || !_currentRun.levelBgmEnabled) return;
            int trackCount = LevelBgmTrackListSO.GetTrackCount();
            if (trackCount == 0) return;
            int idx = Mathf.Clamp(_currentRun.levelBgmTrackIndex, 0, trackCount - 1);
            var clip = LevelBgmTrackListSO.GetClip(idx);
            if (clip != null)
                MusicMgr.GetInstance().PlayBkMusic(clip);
        }

        // ── 保存 ──────────────────────────────────────────────────────────
        /// <summary>保存当前进度到当前存档（玩家数据写入 RunData 再存盘）</summary>
        public void SaveCurrent()
        {
            if (_currentRun == null || _playerModel == null || string.IsNullOrEmpty(_currentSaveId)) return;
            _playerModel.SaveTo(_currentRun);
            SaveSystem.GetInstance().Save(_currentSaveId,
                new SaveData { version = SaveSystem.CurrentVersion, playerName = _currentPlayerName ?? "", run = _currentRun });
        }

        /// <summary>当前存档的按键配置覆盖（KeyConfigPanel 用）；无存档时返回空字符串。</summary>
        public string GetKeyConfigOverrides() => _currentRun?.keyConfigOverrides ?? "";

        /// <summary>写入当前存档的按键配置并保存；仅在有关卡存档时有效。</summary>
        public void SetKeyConfigOverrides(string value)
        {
            if (_currentRun == null || string.IsNullOrEmpty(_currentSaveId)) return;
            _currentRun.keyConfigOverrides = value ?? "";
            SaveCurrent();
        }

        // ── 暂停 / 继续 ───────────────────────────────────────────────────
        public void Pause()
        {
            if (_state == State.Paused) return;
            Time.timeScale = 0f;
            SetState(State.Paused);
        }

        public void Resume()
        {
            if (_state == State.InLevel)
            {
                Time.timeScale = 1f;
                return;
            }

            Time.timeScale = 1f;
            SetState(State.InLevel);
        }

        /// <summary>编辑器直接运行关卡时，注入一份临时运行上下文，供关卡逻辑与 HUD 正常读取。</summary>
        public void AdoptRuntimeContext(RunData run, PlayerModel playerModel, string playerName = "玩家")
        {
            _currentSaveId = null;
            _currentPlayerName = string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName.Trim();
            _currentRun = run;
            _playerModel = playerModel;
            Time.timeScale = 1f;
            SetState(State.InLevel);
        }

        // ── 死亡选择（供 DeathPanel 等 UI 注册）────────────────────────────
        /// <summary>注册死亡二选一处理者；若未注册，死亡时直接执行「不复活」。</summary>
        public void SetDeathChoiceHandler(IDeathChoiceHandler handler) => _deathChoiceHandler = handler;

        /// <summary>消耗仙露复活：回滚 Checkpoint、难度+1、重进本关。由 DeathPanel 选择「复活」时调用。</summary>
        public void ExecuteReviveWithNectar()
        {
            if (_currentRun == null || _playerModel == null) return;
            RunData checkpoint = _currentRun.checkpoint;
            if (checkpoint == null) return;
            if (_playerModel.GetItemCount(PlayerModel.ItemIds.Nectar) < 1) return;

            RunData restoredRun = SaveSystem.CloneRunData(checkpoint);
            if (restoredRun == null)
                return;

            restoredRun.checkpoint = SaveSystem.CloneRunData(checkpoint);
            restoredRun.difficulty++;

            _currentRun = restoredRun;
            _playerModel.LoadFrom(restoredRun);
            _playerModel.TryConsumeItem(PlayerModel.ItemIds.Nectar, 1);

            _playerModel.SaveTo(_currentRun);
            var buffCfg = ConfigManager.GetInstance()?.GetBuffConfig();
            if (buffCfg != null)
                _playerModel.ReapplyBuffModifiers(buffCfg.GetModifierForBuff);
            SaveCurrent();
            Debug.Log($"[GameStateMachine] 仙露复活，回滚至 Checkpoint，难度提升至 {_currentRun.difficulty}");
            Time.timeScale = 1f;
            SetState(State.InLevel);
            ScenesMgr.GetInstance().LoadSceneAsyn(GetLevelSceneName(_currentRun.levelIndex), ApplyLevelBgm);
        }

        public void RecordBossDefeat(string bossId)
        {
            if (_currentRun == null)
                return;

            _currentRun.defeatedBossIds ??= new List<string>();
            if (!string.IsNullOrWhiteSpace(bossId) && !_currentRun.defeatedBossIds.Contains(bossId))
                _currentRun.defeatedBossIds.Add(bossId);

            if (!HasLoadableNextLevel())
                _currentRun.isGameCleared = true;

            _currentRun.checkpoint = SaveSystem.CloneRunData(_currentRun);
            SaveCurrent();
        }

        public bool RestartCurrentLevelAtEntrance()
        {
            if (_currentRun == null)
                return false;

            return TransitionToLevel(_currentRun.levelIndex);
        }

        public bool TryAdvanceToNextLevel()
        {
            if (_currentRun == null)
                return false;

            int nextLevel = _currentRun.levelIndex + 1;
            if (nextLevel > MaxLevelIndex || !CanLoadLevelScene(nextLevel))
                return false;

            return TransitionToLevel(nextLevel);
        }

        /// <summary>不复活：删除当前存档并返回主菜单。由 DeathPanel 选择「不复活」时调用。</summary>
        public void ExecuteGiveUp()
        {
            if (_currentRun == null) return;

            Debug.Log("[GameStateMachine] 玩家选择不复活，本局结束");
            if (!string.IsNullOrEmpty(_currentSaveId))
                SaveSystem.GetInstance().Delete(_currentSaveId);
            OnRunEnded?.Invoke();
            _currentSaveId     = null;
            _currentPlayerName = null;
            _currentRun       = null;
            _playerModel      = null;
            SetLevelPlayerTransform(null);
            Time.timeScale = 1f;
            LevelUIModelLocator.Set(null);
            SetState(State.MainMenu);
            ScenesMgr.GetInstance().LoadSceneAsyn(SceneNames.MainMenu, null, false);
        }

        /// <summary>是否可仙露复活：有 Checkpoint 且背包至少有 1 仙露</summary>
        public bool CanReviveWithNectar()
        {
            if (_currentRun?.checkpoint == null || _playerModel == null) return false;
            return _playerModel.GetItemCount(PlayerModel.ItemIds.Nectar) >= 1;
        }

        // ── 玩家死亡 ──────────────────────────────────────────────────────
        /// <summary>
        /// 由 LevelBootstrapper 在玩家死亡时调用。
        /// 若已注册 IDeathChoiceHandler，则交由 UI 展示二选一；否则直接执行「不复活」。
        /// </summary>
        public void OnPlayerDied()
        {
            if (_currentRun == null) return;

            if (_deathChoiceHandler != null)
            {
                _deathChoiceHandler.RequestDeathChoice(ExecuteReviveWithNectar, ExecuteGiveUp);
                return;
            }

            ExecuteGiveUp();
        }

        // ── 退出 ──────────────────────────────────────────────────────────
        /// <summary>退出并保存，返回主菜单</summary>
        public void SaveAndQuit()
        {
            SaveCurrent();
            OnRunEnded?.Invoke();

            _currentSaveId     = null;
            _currentPlayerName = null;
            _currentRun        = null;
            _playerModel       = null;
            SetLevelPlayerTransform(null);
            Time.timeScale = 1f;
            LevelUIModelLocator.Set(null);
            SetState(State.MainMenu);
            // 返回主菜单不播进度条动画，加载完等 0.3s 即切场景，避免长时间无谓等待和卡顿感
            ScenesMgr.GetInstance().LoadSceneAsyn(SceneNames.MainMenu, null, false);
        }

        // ── 私有工具 ──────────────────────────────────────────────────────
        private static bool CanLoadLevelScene(int levelIndex)
        {
            return Application.CanStreamedLevelBeLoaded(GetLevelSceneName(levelIndex));
        }

        private bool TransitionToLevel(int levelIndex)
        {
            if (_currentRun == null)
                return false;

            levelIndex = Mathf.Clamp(levelIndex, 1, MaxLevelIndex);
            if (!CanLoadLevelScene(levelIndex))
                return false;

            _currentRun.levelIndex = levelIndex;
            _currentRun.levelSnapshot = null;
            _currentRun.checkpoint = SaveSystem.CloneRunData(_currentRun);
            SaveCurrent();

            Time.timeScale = 1f;
            SetState(State.InLevel);
            ScenesMgr.GetInstance().LoadSceneAsyn(GetLevelSceneName(levelIndex), ApplyLevelBgm);
            return true;
        }

        private void SetState(State newState)
        {
            if (_state == newState) return;
            _state = newState;
            OnStateChanged?.Invoke(_state);
        }
    }
}
