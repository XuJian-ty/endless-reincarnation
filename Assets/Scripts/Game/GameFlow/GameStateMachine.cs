using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Game;
using Game.Domain;
using Game.Data;
using Game.Online;
using Game.Saving;
using Game.Social;
using Game.UI;
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
        private string               _currentPortraitId;
        private RunData              _currentRun;
        private PlayerModel          _playerModel;
        private IDeathChoiceHandler  _deathChoiceHandler;
        private Transform            _levelPlayerTransform;
        private Coroutine            _activeSaveHeartbeat;

        public State       CurrentState => _state;
        public string      CurrentSaveId => _currentSaveId;
        public string      CurrentPlayerName => _currentPlayerName;
        public string      CurrentPortraitId => _currentPortraitId;
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
            LevelConfigDatabaseSO configDatabase = ConfigManager.GetInstance()?.GetLevelConfigDatabase();
            string configuredSceneName = configDatabase?.GetSceneNameForLevel(levelIndex);
            if (!string.IsNullOrWhiteSpace(configuredSceneName))
                return configuredSceneName;

            return $"{SceneNames.LevelPrefix}{levelIndex}";
        }

        public bool HasLoadableNextLevel()
        {
            if (_currentRun == null)
                return false;

            int nextLevel = _currentRun.levelIndex + 1;
            int maxConfiguredLevelIndex = GetMaxConfiguredLevelIndex();
            if (maxConfiguredLevelIndex > 0 && nextLevel > maxConfiguredLevelIndex)
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

            Time.timeScale = 1f;

            _currentSaveId     = saveId;
            _currentPlayerName = data.playerName ?? "";
            _currentPortraitId = string.IsNullOrWhiteSpace(data.portraitId) ? SaveSystem.DefaultPortraitId : data.portraitId.Trim();
            _currentRun        = data.run;
            _playerModel       = new PlayerModel(levelGrowth);
            _playerModel.LoadFrom(_currentRun);
            var buffConfig = ConfigManager.GetInstance()?.GetBuffConfig();
            if (buffConfig != null)
                _playerModel.ReapplyBuffModifiers(buffConfig.GetModifierForBuff);

            // 加载进度条期间不播 BGM，等进度条满、进入关卡后再在回调里播
            MusicMgr.GetInstance().StopBKMusic();

            float bgmVol = LevelAudioSettings.BgmEnabled ? Mathf.Clamp01(LevelAudioSettings.BgmVolume) : 0f;
            MusicMgr.GetInstance().ChangeBKValue(bgmVol);
            float sfxVol = LevelAudioSettings.SoundEffectsEnabled ? Mathf.Clamp01(LevelAudioSettings.SoundEffectsVolume) : 0f;
            MusicMgr.GetInstance().ChangeSoundValue(sfxVol);

            if (_currentRun.checkpoint == null && _currentRun.levelIndex == 1)
            {
                RestorePlayerHealthToFull();
                _playerModel.SaveTo(_currentRun);
                _currentRun.checkpoint = SaveSystem.CloneRunData(_currentRun);
                SaveSystem.GetInstance().Save(saveId, new SaveData { version = SaveSystem.CurrentVersion, playerName = data.playerName, portraitId = _currentPortraitId, run = _currentRun });
            }

            StartActiveSaveHeartbeat();
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
            if (_currentRun == null || !LevelAudioSettings.BgmEnabled) return;
            int trackCount = LevelBgmTrackListSO.GetTrackCount();
            if (trackCount == 0) return;
            int idx = Mathf.Clamp(LevelAudioSettings.BgmTrackIndex, 0, trackCount - 1);
            var clip = LevelBgmTrackListSO.GetClip(idx);
            if (clip != null)
                MusicMgr.GetInstance().PlayBkMusic(clip);
        }

        // ── 保存 ──────────────────────────────────────────────────────────
        /// <summary>保存当前进度到当前存档（玩家数据写入 RunData 再存盘）</summary>
        public void SaveCurrent(bool captureRuntimeSnapshot = true)
        {
            if (_currentRun == null || _playerModel == null || string.IsNullOrEmpty(_currentSaveId)) return;
            if (captureRuntimeSnapshot)
            {
                LevelBootstrapper bootstrapper = UnityEngine.Object.FindFirstObjectByType<LevelBootstrapper>();
                bootstrapper?.CaptureRuntimeSnapshot();
            }
            _playerModel.SaveTo(_currentRun);
            SaveSystem.GetInstance().Save(_currentSaveId,
                new SaveData { version = SaveSystem.CurrentVersion, playerName = _currentPlayerName ?? "", portraitId = _currentPortraitId, run = _currentRun });
            SyncActiveSaveContext();
        }

        public bool TryUpdateCurrentPlayerProfile(string playerName, string portraitId, out string error)
        {
            error = null;

            if (_currentRun == null || _playerModel == null || string.IsNullOrWhiteSpace(_currentSaveId))
            {
                error = "当前没有可保存的关卡存档。";
                return false;
            }

            string normalizedName = string.IsNullOrWhiteSpace(playerName) ? string.Empty : playerName.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                error = "玩家名字不能为空。";
                return false;
            }

            string normalizedPortraitId = string.IsNullOrWhiteSpace(portraitId) ? string.Empty : portraitId.Trim();
            if (string.IsNullOrWhiteSpace(normalizedPortraitId))
            {
                error = "玩家头像不能为空。";
                return false;
            }

            SaveData data = SaveSystem.GetInstance().Load(_currentSaveId);
            if (data?.run == null)
            {
                error = "无法读取当前存档。";
                return false;
            }

            LevelBootstrapper bootstrapper = UnityEngine.Object.FindFirstObjectByType<LevelBootstrapper>();
            bootstrapper?.CaptureRuntimeSnapshot();
            _playerModel.SaveTo(_currentRun);

            _currentPlayerName = normalizedName;
            _currentPortraitId = normalizedPortraitId;

            data.version = SaveSystem.CurrentVersion;
            data.playerName = _currentPlayerName;
            data.portraitId = _currentPortraitId;
            data.run = _currentRun;
            SaveSystem.GetInstance().Save(_currentSaveId, data);
            SyncActiveSaveContext();
            return true;
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
        public void AdoptRuntimeContext(RunData run, PlayerModel playerModel, string playerName, string saveId, string portraitId)
        {
            _currentSaveId = string.IsNullOrWhiteSpace(saveId) ? null : saveId.Trim();
            _currentPlayerName = string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName.Trim();
            _currentPortraitId = string.IsNullOrWhiteSpace(portraitId) ? SaveSystem.DefaultPortraitId : portraitId.Trim();
            _currentRun = run;
            _playerModel = playerModel;
            if (_currentRun != null && _playerModel != null)
            {
                _playerModel.LoadFrom(_currentRun);
                var buffConfig = ConfigManager.GetInstance()?.GetBuffConfig();
                if (buffConfig != null)
                    _playerModel.ReapplyBuffModifiers(buffConfig.GetModifierForBuff);

                MusicMgr.GetInstance().StopBKMusic();
                float bgmVol = LevelAudioSettings.BgmEnabled ? Mathf.Clamp01(LevelAudioSettings.BgmVolume) : 0f;
                MusicMgr.GetInstance().ChangeBKValue(bgmVol);
                float sfxVol = LevelAudioSettings.SoundEffectsEnabled ? Mathf.Clamp01(LevelAudioSettings.SoundEffectsVolume) : 0f;
                MusicMgr.GetInstance().ChangeSoundValue(sfxVol);

                if (_currentRun.checkpoint == null && _currentRun.levelIndex == 1)
                {
                    RestorePlayerHealthToFull();
                    _playerModel.SaveTo(_currentRun);
                    _currentRun.checkpoint = SaveSystem.CloneRunData(_currentRun);
                    if (!string.IsNullOrEmpty(_currentSaveId))
                    {
                        SaveSystem.GetInstance().Save(_currentSaveId,
                            new SaveData
                            {
                                version = SaveSystem.CurrentVersion,
                                playerName = _currentPlayerName ?? "",
                                portraitId = _currentPortraitId,
                                run = _currentRun
                            });
                    }
                }

                ApplyLevelBgm();
            }
            StartActiveSaveHeartbeat();
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
            if (!string.IsNullOrWhiteSpace(_currentRun.currentLevelBuffId))
            {
                _playerModel.RemoveBuff(_currentRun.currentLevelBuffId);
                _currentRun.currentLevelBuffId = null;
            }
            _currentRun.pendingBuffSelection = true;
            _playerModel.TryConsumeItem(PlayerModel.ItemIds.Nectar, 1);

            _playerModel.SaveTo(_currentRun);
            var buffCfg = ConfigManager.GetInstance()?.GetBuffConfig();
            if (buffCfg != null)
                _playerModel.ReapplyBuffModifiers(buffCfg.GetModifierForBuff);
            SaveCurrent(false);
            Debug.Log($"[GameStateMachine] 仙露复活，回滚至 Checkpoint，难度提升至 {_currentRun.difficulty}");
            Time.timeScale = 1f;
            SetState(State.InLevel);
            FinalBossDuelRuntimeContext.ClearChallenge();
            LoadLevelWithMainMenuStyleTransition(GetLevelSceneName(_currentRun.levelIndex), ApplyLevelBgm);
        }

        public void RecordBossDefeat(string bossId, bool captureRuntimeSnapshot = true)
        {
            if (_currentRun == null)
                return;

            _currentRun.defeatedBossIds ??= new List<string>();
            if (!string.IsNullOrWhiteSpace(bossId) && !_currentRun.defeatedBossIds.Contains(bossId))
                _currentRun.defeatedBossIds.Add(bossId);

            if (!HasLoadableNextLevel())
                _currentRun.isGameCleared = true;

            SaveCurrent(captureRuntimeSnapshot);
        }

        public bool RestartCurrentLevelAtEntrance()
        {
            if (_currentRun == null)
                return false;

            RemoveCurrentLevelBuffFromRuntime();
            return TransitionToLevel(_currentRun.levelIndex, true);
        }

        public bool TryAdvanceToNextLevel()
        {
            if (_currentRun == null)
                return false;

            int nextLevel = _currentRun.levelIndex + 1;
            int maxConfiguredLevelIndex = GetMaxConfiguredLevelIndex();
            if ((maxConfiguredLevelIndex > 0 && nextLevel > maxConfiguredLevelIndex) || !CanLoadLevelScene(nextLevel))
                return false;

            return TransitionToLevel(nextLevel);
        }

        /// <summary>不复活：删除当前存档并返回主菜单。由 DeathPanel 选择「不复活」时调用。</summary>
        public void ExecuteGiveUp()
        {
            if (_currentRun == null) return;

            Debug.Log("[GameStateMachine] 玩家选择不复活，本局结束");
            CloseActiveAidSession();
            if (!string.IsNullOrEmpty(_currentSaveId))
                SaveSystem.GetInstance().Delete(_currentSaveId);
            StopActiveSaveHeartbeat();
            ClearActiveSaveContext();
            OnRunEnded?.Invoke();
            _currentSaveId     = null;
            _currentPlayerName = null;
            _currentPortraitId = null;
            _currentRun       = null;
            _playerModel      = null;
            SetLevelPlayerTransform(null);
            FinalBossDuelRuntimeContext.ClearChallenge();
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

            if (OnlineDungeonSessionCoordinator.GetInstance().TryHandleLocalPlayerDeath()
                || SocialAidSessionCoordinator.GetInstance().TryHandleLocalPlayerDeath())
            {
                return;
            }

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
            CloseActiveAidSession();
            StopActiveSaveHeartbeat();
            ClearActiveSaveContext();
            OnRunEnded?.Invoke();

            _currentSaveId     = null;
            _currentPlayerName = null;
            _currentPortraitId = null;
            _currentRun        = null;
            _playerModel       = null;
            SetLevelPlayerTransform(null);
            FinalBossDuelRuntimeContext.ClearChallenge();
            Time.timeScale = 1f;
            LevelUIModelLocator.Set(null);
            SetState(State.MainMenu);
            // 返回主菜单不播进度条动画，加载完等 0.3s 即切场景，避免长时间无谓等待和卡顿感
            ScenesMgr.GetInstance().LoadSceneAsyn(SceneNames.MainMenu, null, false);
        }

        public void ReturnCurrentSaveToOwnLevel(int returnLevelIndex, LevelSnapshot returnSnapshot)
        {
            ReturnCurrentSaveToOwnLevel(returnLevelIndex, returnSnapshot, null);
        }

        public void ReturnCurrentSaveToOwnLevel(int returnLevelIndex, LevelSnapshot returnSnapshot, string returnSceneName)
        {
            if (_currentRun == null || _playerModel == null)
                return;

            if (returnLevelIndex > 0)
                _currentRun.levelIndex = returnLevelIndex;
            _currentRun.levelSnapshot = returnSnapshot;
            SaveCurrent(false);
            StartActiveSaveHeartbeat();
            Time.timeScale = 1f;
            SetState(State.InLevel);
            string sceneName = string.IsNullOrWhiteSpace(returnSceneName)
                ? GetLevelSceneName(_currentRun.levelIndex)
                : returnSceneName.Trim();
            LoadLevelWithMainMenuStyleTransition(sceneName, ApplyLevelBgm);
        }

        // ── 私有工具 ──────────────────────────────────────────────────────
        private static bool CanLoadLevelScene(int levelIndex)
        {
            return Application.CanStreamedLevelBeLoaded(GetLevelSceneName(levelIndex));
        }

        private bool TransitionToLevel(int levelIndex, bool forceBuffSelection = false)
        {
            if (_currentRun == null)
                return false;

            if (levelIndex < 1)
                return false;

            if (!CanLoadLevelScene(levelIndex))
                return false;

            bool isAdvancingToDifferentLevel = _currentRun.levelIndex != levelIndex;
            _currentRun.levelIndex = levelIndex;
            if (isAdvancingToDifferentLevel)
                _currentRun.difficulty = Mathf.Max(1, _currentRun.difficulty + 1);
            _currentRun.pendingBuffSelection = forceBuffSelection || isAdvancingToDifferentLevel;
            if (_currentRun.pendingBuffSelection)
                _currentRun.currentLevelBuffId = null;
            if (isAdvancingToDifferentLevel || forceBuffSelection)
                RestorePlayerHealthToFull();
            _playerModel?.SaveTo(_currentRun);
            _currentRun.levelSnapshot = null;
            _currentRun.checkpoint = SaveSystem.CloneRunData(_currentRun);
            SaveCurrent(false);

            StartActiveSaveHeartbeat();
            Time.timeScale = 1f;
            SetState(State.InLevel);
            LoadLevelWithMainMenuStyleTransition(GetLevelSceneName(levelIndex), ApplyLevelBgm);
            return true;
        }

        /// <summary>
        /// 关卡切换时复用主菜单同款加载表现：MainMenuBackground + LoadingPanel。
        /// PromptClickToContinue 仅在初次进入主菜单时显示，此处作为过渡加载不显示。
        /// </summary>
        private void LoadLevelWithMainMenuStyleTransition(string sceneName, UnityAction onSceneLoaded)
        {
            UIManager ui = UIManager.GetInstance();
            if (ui == null)
            {
                ScenesMgr.GetInstance().LoadSceneAsyn(sceneName, onSceneLoaded);
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
                    onSceneLoaded?.Invoke();
                });
            });
        }

        private void SetState(State newState)
        {
            if (_state == newState) return;
            _state = newState;
            OnStateChanged?.Invoke(_state);
        }

        private void RemoveCurrentLevelBuffFromRuntime()
        {
            if (_currentRun == null || _playerModel == null || string.IsNullOrWhiteSpace(_currentRun.currentLevelBuffId))
                return;

            _playerModel.RemoveBuff(_currentRun.currentLevelBuffId);
            _currentRun.currentLevelBuffId = null;
            _currentRun.pendingBuffSelection = true;
            _playerModel.SaveTo(_currentRun);
        }

        private void RestorePlayerHealthToFull()
        {
            if (_playerModel == null)
                return;

            _playerModel.CurrentHp = _playerModel.Stats.MaxHp;
            _playerModel.CurrentMp = _playerModel.Stats.MaxMp;
        }

        private void SyncActiveSaveContext()
        {
            if (string.IsNullOrWhiteSpace(_currentSaveId) || !SocialSession.GetInstance().IsLoggedIn)
                return;

            SocialService.GetInstance().SetActiveSaveContext(
                _currentSaveId,
                (_, _) => { },
                error => Debug.LogWarning($"[GameStateMachine] 活跃存档上报失败：{error}"));
        }

        private void StartActiveSaveHeartbeat()
        {
            SyncActiveSaveContext();
            if (_activeSaveHeartbeat != null)
                return;

            _activeSaveHeartbeat = MonoMgr.GetInstance().StartCoroutine(ActiveSaveHeartbeatLoop());
        }

        private void StopActiveSaveHeartbeat()
        {
            if (_activeSaveHeartbeat == null)
                return;

            MonoMgr.GetInstance().StopCoroutine(_activeSaveHeartbeat);
            _activeSaveHeartbeat = null;
        }

        private IEnumerator ActiveSaveHeartbeatLoop()
        {
            while (!string.IsNullOrWhiteSpace(_currentSaveId))
            {
                yield return new WaitForSecondsRealtime(5f);
                SyncActiveSaveContext();
            }

            _activeSaveHeartbeat = null;
        }

        private void ClearActiveSaveContext()
        {
            if (!SocialSession.GetInstance().IsLoggedIn)
                return;

            SocialService.GetInstance().ClearActiveSaveContext(
                (_, _) => { },
                error => Debug.LogWarning($"[GameStateMachine] 活跃存档清除失败：{error}"));
        }

        private void CloseActiveAidSession()
        {
            OnlineDungeonSessionCoordinator onlineCoordinator = OnlineDungeonSessionCoordinator.GetInstance();
            SocialAidSessionInfo session = onlineCoordinator.ActiveAidSession ?? SocialAidSessionCoordinator.GetInstance().ActiveSession;
            if (session == null || string.IsNullOrWhiteSpace(session.sessionId))
                return;

            onlineCoordinator.StopSession();
            SocialAidSessionCoordinator.GetInstance().StopSession();
            SocialService.GetInstance().CloseAidSession(
                session.sessionId,
                (_, _) => { },
                error => Debug.LogWarning($"[GameStateMachine] 援助会话关闭失败：{error}"));
        }

        private static int GetMaxConfiguredLevelIndex()
        {
            LevelConfigDatabaseSO configDatabase = ConfigManager.GetInstance()?.GetLevelConfigDatabase();
            if (configDatabase?.levels == null || configDatabase.levels.Count == 0)
                return 0;

            int maxLevelIndex = 0;
            for (int i = 0; i < configDatabase.levels.Count; i++)
            {
                LevelConfigData level = configDatabase.levels[i];
                if (level == null)
                    continue;

                maxLevelIndex = Mathf.Max(maxLevelIndex, level.levelIndex, i + 1);
            }

            return maxLevelIndex;
        }
    }
}
