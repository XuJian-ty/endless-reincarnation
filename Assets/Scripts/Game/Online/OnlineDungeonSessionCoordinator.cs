using System;
using System.Collections;
using System.Collections.Generic;
using Game.Data;
using Game.Domain;
using Game.GameFlow;
using Game.Presentation;
using Game.Saving;
using Game.Social;
using Game.UI;
using Newtonsoft.Json;
using ProjectBase;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Online
{
    /// <summary>
    /// 联机副本会话协调器：接收平台服创建好的副本会话，并作为后续实时副本服务器连接的统一入口。
    /// </summary>
    public sealed class OnlineDungeonSessionCoordinator : BaseManager<OnlineDungeonSessionCoordinator>
    {
        private const float WorldSnapshotSyncIntervalSeconds = 1f;
        private const float RemoteAvatarTimeoutSeconds = 3f;
        private const float WorldSnapshotWarningIntervalSeconds = 5f;
        private const float OnlineEnemyFreezeDebugIntervalSeconds = 1.5f;

        private SocialAidSessionInfo _activeAidSession;
        private Vector3? _pendingSpawnPosition;
        private float _pendingSpawnYaw;
        private int _returnLevelIndex;
        private string _returnSceneName = string.Empty;
        private string _returnBossId = string.Empty;
        private string _returnBossDisplayName = string.Empty;
        private LevelSnapshot _returnSnapshot;
        private Coroutine _worldSnapshotSyncCoroutine;
        private Coroutine _worldSnapshotImmediateUploadCoroutine;
        private long _lastAppliedWorldSnapshotVersion;
        private string _lastAppliedWorldSnapshotSceneName = string.Empty;
        private long _lastAppliedAuthorityStateVersion;
        private long _lastReceivedDamageEventSequence;
        private long _lastReceivedUiPanelEventSequence;
        private long _lastAppliedUiPanelStateVersion;
        private long _lastAppliedSceneLoadVersion;
        private long _lastAppliedRewardStateVersion;
        private string _lastUploadedWorldSnapshotJson = string.Empty;
        private string _lastUploadedWorldSnapshotSceneName = string.Empty;
        private string _pendingSceneSyncName = string.Empty;
        private string _onlineSceneLoadTransitionId = string.Empty;
        private string _onlineSceneLoadSceneName = string.Empty;
        private string _completedOnlineSceneLoadTransitionId = string.Empty;
        private string _completedOnlineSceneLoadSceneName = string.Empty;
        private bool _onlineSceneLoadInProgress;
        private bool _onlineSceneLoadReleased;
        private bool _onlineSceneLoadReadySent;
        private float _onlineSceneLoadReleaseLocalTime;
        private bool _isApplyingWorldSnapshot;
        private float _nextWorldSnapshotWarningTime;
        private float _nextOnlineEnemyFreezeDebugTime;
        private float _nextOnlineEnemyFreezeApplyDebugTime;
        private readonly OnlineDungeonWorldSnapshotClient _worldSnapshotClient = new OnlineDungeonWorldSnapshotClient();
        private readonly OnlineDungeonRealtimeClient _realtimeClient = new OnlineDungeonRealtimeClient();
        private readonly HashSet<string> _appliedDamageEventIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedUiPanelEventIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedKillRewards = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedDropPickups = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _pendingKillRewardClaims = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _pendingOnlineDropPickups = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _pendingOnlineChestOpens = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _openedOnlineChests = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _spawnedDropIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, DroppedPickupRuntime> _onlineDrops = new Dictionary<string, DroppedPickupRuntime>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialAidRemotePlayerAvatar> _remoteAvatars = new Dictionary<string, SocialAidRemotePlayerAvatar>(StringComparer.Ordinal);

        public SocialAidSessionInfo ActiveAidSession => _activeAidSession;
        public bool HasActiveSession => _activeAidSession != null
                                        && !string.IsNullOrWhiteSpace(_activeAidSession.sessionId)
                                        && !string.IsNullOrWhiteSpace(_activeAidSession.dungeonInstanceId);

        public bool ShouldDriveOnlineWorldSimulation()
        {
            if (!HasActiveSession)
                return true;

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            return currentUser != null && IsWorldSnapshotPublisher(currentUser.userId);
        }

        public void NotifyOnlineWorldSimulationChanged()
        {
            if (!HasActiveSession || !ShouldDriveOnlineWorldSimulation())
                return;

            if (_worldSnapshotImmediateUploadCoroutine != null)
                return;

            _worldSnapshotImmediateUploadCoroutine = MonoMgr.GetInstance().StartCoroutine(UploadWorldSnapshotAfterFrame());
        }

        public bool IsAidJoinerRole
        {
            get
            {
                SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
                return HasActiveSession
                       && currentUser != null
                       && string.Equals(currentUser.userId, _activeAidSession.helperUserId, StringComparison.Ordinal);
            }
        }

        public bool TryEnterSession(SocialAidSessionInfo sessionInfo, out string error)
        {
            error = string.Empty;
            if (sessionInfo == null || string.IsNullOrWhiteSpace(sessionInfo.sessionId))
            {
                error = "联机副本会话信息无效";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sessionInfo.dungeonServerUrl))
            {
                error = "联机副本服务器地址为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sessionInfo.dungeonInstanceId))
            {
                error = "联机副本实例编号为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sessionInfo.dungeonJoinToken))
            {
                error = "联机副本加入令牌为空";
                return false;
            }

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId))
            {
                error = "当前未登录账号";
                return false;
            }

            if (sessionInfo.participants == null || sessionInfo.participants.Count == 0)
            {
                error = "联机副本参与者列表为空";
                return false;
            }

            bool isParticipant = false;
            for (int i = 0; i < sessionInfo.participants.Count; i++)
            {
                SocialDungeonParticipantInfo participant = sessionInfo.participants[i];
                if (participant != null && string.Equals(participant.userId, currentUser.userId, StringComparison.Ordinal))
                {
                    isParticipant = true;
                    break;
                }
            }

            if (!isParticipant)
            {
                error = "当前账号不属于该联机副本";
                return false;
            }

            _activeAidSession = sessionInfo;
            int participantCount = sessionInfo.participants != null ? sessionInfo.participants.Count : 0;
            Debug.Log($"[OnlineDungeonSessionCoordinator] 联机副本会话已就绪。Server={sessionInfo.dungeonServerUrl} Instance={sessionInfo.dungeonInstanceId} Participants={participantCount}");
            StartRealtimeClient(currentUser.userId);
            StartWorldSnapshotSync();
            StartDamageEventSync();
            StartUiPanelEventSync();
            StartSceneLoadSync();
            StartRewardStateSync();
            TryEnterTargetLevel(currentUser.userId);
            return true;
        }

        public bool TryConsumeSpawnOverride(out Vector3 position, out Quaternion rotation)
        {
            if (!_pendingSpawnPosition.HasValue)
            {
                position = default;
                rotation = default;
                return false;
            }

            position = _pendingSpawnPosition.Value;
            rotation = Quaternion.Euler(0f, _pendingSpawnYaw, 0f);
            _pendingSpawnPosition = null;
            return true;
        }

        public bool ShouldSkipLocalSnapshotRestore()
        {
            return IsAidJoinerRole;
        }

        public bool TryHandleLocalPlayerDeath()
        {
            if (!HasActiveSession)
                return false;

            if (IsAidJoinerRole)
            {
                ReturnAidJoinerToOwnLevel(true);
                return true;
            }

            EndSessionForLocalOwnerOutcome();
            return false;
        }

        public bool TryHandleBossDefeatedSessionEnd()
        {
            if (!HasActiveSession)
                return false;

            if (IsAidJoinerRole)
            {
                ReturnAidJoinerToOwnLevel(true);
                return true;
            }

            EndSessionForLocalOwnerOutcome();
            return false;
        }

        public bool TryPrepareSaveAndQuit()
        {
            if (!HasActiveSession)
                return false;

            if (IsAidJoinerRole)
            {
                SocialAidSessionInfo session = _activeAidSession;
                int returnLevelIndex = _returnLevelIndex;
                string returnSceneName = _returnSceneName;
                string returnBossId = _returnBossId;
                string returnBossDisplayName = _returnBossDisplayName;
                LevelSnapshot returnSnapshot = _returnSnapshot;
                StopSession();
                CloseAidSession(session);

                if (FinalBossDuelSceneRuntime.IsFinalBossDuelSceneName(returnSceneName) && !string.IsNullOrWhiteSpace(returnBossId))
                    FinalBossDuelRuntimeContext.AdoptOnlineChallenge(returnBossId, returnBossDisplayName);
                else
                    FinalBossDuelRuntimeContext.ClearChallenge();

                GameStateMachine.GetInstance().RestoreCurrentSaveToOwnLevelState(returnLevelIndex, returnSnapshot);
                return true;
            }

            EndSessionForLocalOwnerOutcome();
            return false;
        }

        public void ReturnAidJoinerToOwnLevel(bool closeServerSession)
        {
            if (!IsAidJoinerRole)
                return;

            SocialAidSessionInfo session = _activeAidSession;
            int returnLevelIndex = _returnLevelIndex;
            string returnSceneName = _returnSceneName;
            string returnBossId = _returnBossId;
            string returnBossDisplayName = _returnBossDisplayName;
            LevelSnapshot returnSnapshot = _returnSnapshot;
            StopSession();

            if (closeServerSession && session != null && !string.IsNullOrWhiteSpace(session.sessionId))
                CloseAidSession(session);

            if (FinalBossDuelSceneRuntime.IsFinalBossDuelSceneName(returnSceneName) && !string.IsNullOrWhiteSpace(returnBossId))
                FinalBossDuelRuntimeContext.AdoptOnlineChallenge(returnBossId, returnBossDisplayName);
            else
                FinalBossDuelRuntimeContext.ClearChallenge();

            GameStateMachine.GetInstance().ReturnCurrentSaveToOwnLevel(returnLevelIndex, returnSnapshot, returnSceneName);
        }

        private void EndSessionForLocalOwnerOutcome()
        {
            if (!HasActiveSession)
                return;

            SocialAidSessionInfo session = _activeAidSession;
            StopSession();
            CloseAidSession(session);
        }

        private static void CloseAidSession(SocialAidSessionInfo session)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.sessionId))
                return;

            SocialService.GetInstance().CloseAidSession(
                session.sessionId,
                (_, _) => { },
                error => Debug.LogWarning($"[OnlineDungeonSessionCoordinator] 关闭联机副本会话失败：{error}"));
        }

        public bool TryReportLocalEnemyDamage(EnemyController enemy, float damage, Vector3 hitPosition)
        {
            if (!HasActiveSession || enemy == null || damage <= 0f)
                return false;

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId))
                return false;

            OnlineDungeonDamageEventRequest request = new OnlineDungeonDamageEventRequest
            {
                eventId = Guid.NewGuid().ToString("N"),
                userId = currentUser.userId,
                joinToken = _activeAidSession.dungeonJoinToken,
                targetKind = "enemy",
                targetRuntimeId = enemy.RuntimeId,
                damage = damage,
                stunDuration = 0f,
                targetEnemy = BuildDamageTargetEnemyInfo(enemy),
                x = hitPosition.x,
                y = hitPosition.y,
                z = hitPosition.z,
            };
            EnqueueRealtimeDamageEvent(request);
            return true;
        }

        private static OnlineDungeonDamageTargetEnemyInfo BuildDamageTargetEnemyInfo(EnemyController enemy)
        {
            if (enemy == null)
                return null;

            return new OnlineDungeonDamageTargetEnemyInfo
            {
                runtimeId = enemy.RuntimeId,
                enemyId = enemy.EnemyId,
                enemyType = (int)enemy.EnemyCategory,
                x = enemy.transform.position.x,
                y = enemy.transform.position.y,
                z = enemy.transform.position.z,
                yaw = enemy.transform.eulerAngles.y,
                currentHp = Mathf.Max(0f, enemy.CurrentHp),
                currentPoise = Mathf.Max(0f, enemy.CurrentPoise),
                countsAsLevelBoss = enemy.CountsAsLevelBoss,
                isDead = !enemy.IsAlive,
                showCombatHealthBar = enemy.ShouldShowCombatHealthBar,
                moveBlend = enemy.AnimatorMoveBlend,
                moveForward = enemy.AnimatorMoveForward,
                moveStrafe = enemy.AnimatorMoveStrafe,
            };
        }

        public bool TryReportRemotePlayerDamage(string targetUserId, float damage, Vector3 hitPosition)
        {
            if (!HasActiveSession || string.IsNullOrWhiteSpace(targetUserId) || damage <= 0f)
                return false;

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId))
                return false;

            OnlineDungeonDamageEventRequest request = new OnlineDungeonDamageEventRequest
            {
                eventId = Guid.NewGuid().ToString("N"),
                userId = currentUser.userId,
                joinToken = _activeAidSession.dungeonJoinToken,
                targetKind = "player",
                targetRuntimeId = targetUserId.Trim(),
                damage = damage,
                stunDuration = 0.2f,
                x = hitPosition.x,
                y = hitPosition.y,
                z = hitPosition.z,
            };
            EnqueueRealtimeDamageEvent(request);
            return true;
        }

        public bool TryBroadcastGameplayPanelState(string panelName, bool open)
        {
            if (!HasActiveSession || string.IsNullOrWhiteSpace(panelName))
                return false;

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId))
                return false;

            OnlineDungeonUiPanelEventRequest request = new OnlineDungeonUiPanelEventRequest
            {
                eventId = Guid.NewGuid().ToString("N"),
                userId = currentUser.userId,
                joinToken = _activeAidSession.dungeonJoinToken,
                panelName = panelName.Trim(),
                open = open,
            };
            EnqueueRealtimeUiPanelEvent(request);

            return true;
        }

        public bool TryStartOnlineSceneLoad(string sceneName, string bossId, string bossDisplayName, out string error)
        {
            error = string.Empty;
            if (!HasActiveSession)
                return false;

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                error = "缺少联机场景加载目标";
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                error = $"副本目标场景未加入 Build Settings：{sceneName}";
                return false;
            }

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId))
            {
                error = "当前未登录账号";
                return false;
            }

            if (!IsWorldSnapshotPublisher(currentUser.userId))
            {
                error = "只有被援助方可以发起联机场景加载";
                return false;
            }

            if (!_realtimeClient.IsRunning)
            {
                error = "联机副本实时通道未启动，无法发起场景加载同步";
                return false;
            }

            OnlineDungeonSceneLoadEventRequest request = new OnlineDungeonSceneLoadEventRequest
            {
                eventId = Guid.NewGuid().ToString("N"),
                action = "start",
                transitionId = Guid.NewGuid().ToString("N"),
                sceneName = sceneName.Trim(),
                bossId = string.IsNullOrWhiteSpace(bossId) ? string.Empty : bossId.Trim(),
                bossDisplayName = string.IsNullOrWhiteSpace(bossDisplayName) ? string.Empty : bossDisplayName.Trim(),
            };
            EnqueueRealtimeSceneLoadEvent(request);
            return true;
        }

        public bool TrySubmitEnemyKillReward(EnemyController enemy, EnemyRuntimeStats enemyStats, Vector3 deathPosition, System.Random rng)
        {
            if (!HasActiveSession || enemy == null)
                return false;

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId))
                return false;

            OnlineDungeonEnemyKillRequest request = new OnlineDungeonEnemyKillRequest
            {
                userId = currentUser.userId,
                joinToken = _activeAidSession.dungeonJoinToken,
                enemyRuntimeId = enemy.RuntimeId,
                targetEnemy = BuildDamageTargetEnemyInfo(enemy),
                x = deathPosition.x,
                y = deathPosition.y,
                z = deathPosition.z,
            };
            if (_realtimeClient.IsRunning)
                _realtimeClient.EnqueueEnemyKillReward(request);
            else
                LogWorldSnapshotWarning("联机副本实时通道未启动，副本击杀奖励无法提交。");
            return true;
        }

        public void StopSession()
        {
            StopRealtimeClient();
            StopWorldSnapshotSync();
            StopDamageEventSync();
            StopUiPanelEventSync();
            StopSceneLoadSync();
            StopRewardStateSync();
            ClearRemoteAvatars();
            ClearOnlineDrops();
            if (!HasActiveSession)
            {
                _activeAidSession = null;
                _pendingSpawnPosition = null;
                _returnLevelIndex = 0;
                _returnSceneName = string.Empty;
                _returnBossId = string.Empty;
                _returnBossDisplayName = string.Empty;
                _returnSnapshot = null;
                return;
            }

            Debug.Log($"[OnlineDungeonSessionCoordinator] 联机副本会话已结束。Instance={_activeAidSession.dungeonInstanceId}");
            _activeAidSession = null;
            _pendingSpawnPosition = null;
            _returnLevelIndex = 0;
            _returnSceneName = string.Empty;
            _returnBossId = string.Empty;
            _returnBossDisplayName = string.Empty;
            _returnSnapshot = null;
        }

        private void StartDamageEventSync()
        {
            StopDamageEventSync();
            _lastReceivedDamageEventSequence = 0;
            _appliedDamageEventIds.Clear();
        }

        private void StopDamageEventSync()
        {
            _lastReceivedDamageEventSequence = 0;
            _appliedDamageEventIds.Clear();
        }

        private void EnqueueRealtimeDamageEvent(OnlineDungeonDamageEventRequest request)
        {
            if (request == null)
                return;

            if (_realtimeClient.IsRunning)
                _realtimeClient.EnqueueDamageEvent(request);
            else
                LogWorldSnapshotWarning("联机副本实时通道未启动，副本伤害事件无法提交。");
        }

        private void StartUiPanelEventSync()
        {
            StopUiPanelEventSync();
            _lastReceivedUiPanelEventSequence = 0;
            _lastAppliedUiPanelStateVersion = 0;
            _appliedUiPanelEventIds.Clear();
        }

        private void StopUiPanelEventSync()
        {
            _lastReceivedUiPanelEventSequence = 0;
            _lastAppliedUiPanelStateVersion = 0;
            _appliedUiPanelEventIds.Clear();
        }

        private void StartSceneLoadSync()
        {
            StopSceneLoadSync();
        }

        private void StopSceneLoadSync()
        {
            _lastAppliedSceneLoadVersion = 0;
            _onlineSceneLoadTransitionId = string.Empty;
            _onlineSceneLoadSceneName = string.Empty;
            _completedOnlineSceneLoadTransitionId = string.Empty;
            _completedOnlineSceneLoadSceneName = string.Empty;
            _onlineSceneLoadInProgress = false;
            _onlineSceneLoadReleased = false;
            _onlineSceneLoadReadySent = false;
            _onlineSceneLoadReleaseLocalTime = 0f;
        }

        public bool ShouldWaitForOnlineFinalBossAuthority()
        {
            return HasActiveSession && !ShouldDriveOnlineWorldSimulation();
        }

        public string BuildOnlineDebugContext()
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            string currentUserId = currentUser != null ? currentUser.userId : string.Empty;
            string hostUserId = _activeAidSession != null ? _activeAidSession.hostUserId : string.Empty;
            string helperUserId = _activeAidSession != null ? _activeAidSession.helperUserId : string.Empty;
            bool hasSession = HasActiveSession;
            bool driveWorld = ShouldDriveOnlineWorldSimulation();
            string role = "offline";
            if (hasSession)
            {
                if (string.Equals(currentUserId, hostUserId, StringComparison.Ordinal))
                    role = "host";
                else if (string.Equals(currentUserId, helperUserId, StringComparison.Ordinal))
                    role = "helper";
                else
                    role = driveWorld ? "worldAuthority" : "remoteParticipant";
            }

            return $"hasSession={hasSession} role={role} currentUser={currentUserId} hostUser={hostUserId} helperUser={helperUserId} isAidJoiner={IsAidJoinerRole} driveWorld={driveWorld} activeScene={SceneManager.GetActiveScene().name}";
        }

        private void EnqueueRealtimeUiPanelEvent(OnlineDungeonUiPanelEventRequest request)
        {
            if (request == null)
                return;

            if (_realtimeClient.IsRunning)
                _realtimeClient.EnqueueUiPanelEvent(request);
            else
                LogWorldSnapshotWarning("联机副本实时通道未启动，副本面板事件无法提交。");
        }

        private void EnqueueRealtimeSceneLoadEvent(OnlineDungeonSceneLoadEventRequest request)
        {
            if (request == null)
                return;

            if (_realtimeClient.IsRunning)
                _realtimeClient.EnqueueSceneLoadEvent(request);
            else
                LogWorldSnapshotWarning("联机副本实时通道未启动，场景加载同步事件无法提交。");
        }

        private void StartRewardStateSync()
        {
            StopRewardStateSync();
            _lastAppliedRewardStateVersion = 0;
            _appliedKillRewards.Clear();
            _appliedDropPickups.Clear();
            _pendingKillRewardClaims.Clear();
            _pendingOnlineDropPickups.Clear();
            _pendingOnlineChestOpens.Clear();
            _openedOnlineChests.Clear();
            _spawnedDropIds.Clear();
        }

        private void StopRewardStateSync()
        {
            _lastAppliedRewardStateVersion = 0;
            _appliedKillRewards.Clear();
            _appliedDropPickups.Clear();
            _pendingKillRewardClaims.Clear();
            _pendingOnlineDropPickups.Clear();
            _pendingOnlineChestOpens.Clear();
            _openedOnlineChests.Clear();
            _spawnedDropIds.Clear();
        }

        private void StartWorldSnapshotSync()
        {
            StopWorldSnapshotSync();
            _lastAppliedWorldSnapshotVersion = 0;
            _lastAppliedWorldSnapshotSceneName = string.Empty;
            _lastUploadedWorldSnapshotJson = string.Empty;
            _lastUploadedWorldSnapshotSceneName = string.Empty;
            _isApplyingWorldSnapshot = false;
            _worldSnapshotSyncCoroutine = MonoMgr.GetInstance().StartCoroutine(WorldSnapshotSyncLoop());
        }

        private void StopWorldSnapshotSync()
        {
            if (_worldSnapshotSyncCoroutine != null)
            {
                MonoMgr.GetInstance().StopCoroutine(_worldSnapshotSyncCoroutine);
                _worldSnapshotSyncCoroutine = null;
            }

            if (_worldSnapshotImmediateUploadCoroutine != null)
            {
                MonoMgr.GetInstance().StopCoroutine(_worldSnapshotImmediateUploadCoroutine);
                _worldSnapshotImmediateUploadCoroutine = null;
            }

            _isApplyingWorldSnapshot = false;
            _lastAppliedWorldSnapshotVersion = 0;
            _lastAppliedWorldSnapshotSceneName = string.Empty;
            _lastUploadedWorldSnapshotJson = string.Empty;
            _lastUploadedWorldSnapshotSceneName = string.Empty;
        }

        private void StartRealtimeClient(string currentUserId)
        {
            StopRealtimeClient();
            _lastAppliedAuthorityStateVersion = 0;
            _nextOnlineEnemyFreezeDebugTime = 0f;
            _nextOnlineEnemyFreezeApplyDebugTime = 0f;
            _realtimeClient.Start(
                _activeAidSession.dungeonServerUrl,
                _activeAidSession.dungeonInstanceId,
                currentUserId,
                _activeAidSession.dungeonJoinToken,
                _activeAidSession.dungeonRealtimeUdpPort,
                _activeAidSession.dungeonRealtimeKcpPort,
                error => Debug.LogWarning($"[OnlineDungeonSessionCoordinator] 联机副本实时通道启动失败：{error}"));

            if (_realtimeClient.IsRunning)
            {
                Debug.Log($"[OnlineDungeonSessionCoordinator] 联机副本实时通道启动。UDP={_activeAidSession.dungeonRealtimeUdpPort} KCP={_activeAidSession.dungeonRealtimeKcpPort}");
                MonoMgr.GetInstance().AddUpdateListener(TickRealtimeClient);
                MonoMgr.GetInstance().AddLateUpdateListener(TickRealtimeClient);
            }
        }

        private void StopRealtimeClient()
        {
            MonoMgr.GetInstance().RemoveUpdateListener(TickRealtimeClient);
            MonoMgr.GetInstance().RemoveLateUpdateListener(TickRealtimeClient);
            _realtimeClient.Stop();
            _lastAppliedAuthorityStateVersion = 0;
            _nextOnlineEnemyFreezeDebugTime = 0f;
            _nextOnlineEnemyFreezeApplyDebugTime = 0f;
        }

        private void TickRealtimeClient()
        {
            _realtimeClient.Tick(
                BuildRealtimePlayerState,
                BuildRealtimeEnemyAuthorityState,
                ApplyRealtimePlayerSnapshot,
                ApplyRealtimeAuthoritySnapshot,
                () => _lastReceivedUiPanelEventSequence,
                ApplyRealtimeUiPanelSnapshot,
                ApplyRealtimeSceneLoadSnapshot,
                () => _lastReceivedDamageEventSequence,
                ApplyRealtimeDamageSnapshot,
                ApplyRealtimeRewardSnapshot,
                () => _lastAppliedRewardStateVersion,
                () => Debug.Log("[OnlineDungeonSessionCoordinator] 联机副本实时通道已连接。"),
                HandleRealtimeError);
        }

        private void HandleRealtimeError(string error)
        {
            if (TryHandleSessionClosedError(error))
                return;

            _realtimeClient.ClearPendingRewardActions();
            _pendingKillRewardClaims.Clear();
            ResetPendingOnlineDropPickups();
            ResetPendingOnlineChestOpens();
            Debug.LogWarning($"[OnlineDungeonSessionCoordinator] 联机副本实时通道错误：{error}");
        }

        private OnlineDungeonPlayerPoseRequest BuildRealtimePlayerState()
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId))
                return null;

            return BuildLocalPoseRequest(currentUser.userId);
        }

        private OnlineDungeonEnemyAuthorityStateSyncRequest BuildRealtimeEnemyAuthorityState()
        {
            if (!HasActiveSession || !ShouldDriveOnlineWorldSimulation())
                return null;

            EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            List<OnlineDungeonEnemyAuthorityInfo> enemyStates = new List<OnlineDungeonEnemyAuthorityInfo>();
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || !enemy.TryBuildSnapshot(out EnemySnapshot snapshot))
                    continue;

                EnemyResolvedSkill activeSkill = enemy.ActiveSkill;
                Vector3? activeSkillTarget = enemy.ActiveSkillTargetPosition;
                enemyStates.Add(new OnlineDungeonEnemyAuthorityInfo
                {
                    runtimeId = snapshot.runtimeId,
                    enemyId = snapshot.id,
                    enemyType = snapshot.enemyType,
                    x = snapshot.x,
                    y = snapshot.y,
                    z = snapshot.z,
                    yaw = snapshot.yaw,
                    currentHp = snapshot.currentHp,
                    currentPoise = snapshot.currentPoise,
                    countsAsLevelBoss = snapshot.countsAsLevelBoss,
                    isDead = snapshot.currentHp <= 0f,
                    showCombatHealthBar = enemy.ShouldShowCombatHealthBar,
                    moveBlend = enemy.AnimatorMoveBlend,
                    moveForward = enemy.AnimatorMoveForward,
                    moveStrafe = enemy.AnimatorMoveStrafe,
                    activeSkillSequence = activeSkill != null ? enemy.ActiveSkillSequence : 0,
                    activeSkillSlot = activeSkill != null ? activeSkill.SlotIndex : -1,
                    activeSkillId = activeSkill != null ? activeSkill.SkillId : string.Empty,
                    activeSkillAnimationTrigger = activeSkill != null ? activeSkill.AnimationTrigger : string.Empty,
                    hasActiveSkillTarget = activeSkillTarget.HasValue,
                    activeSkillTargetX = activeSkillTarget.HasValue ? activeSkillTarget.Value.x : 0f,
                    activeSkillTargetY = activeSkillTarget.HasValue ? activeSkillTarget.Value.y : 0f,
                    activeSkillTargetZ = activeSkillTarget.HasValue ? activeSkillTarget.Value.z : 0f,
                });
            }

            return new OnlineDungeonEnemyAuthorityStateSyncRequest
            {
                enemies = enemyStates,
            };
        }

        private void ApplyRealtimePlayerSnapshot(OnlineDungeonRealtimePlayerSnapshotInfo snapshot)
        {
            ApplyRemotePoses(snapshot?.poses);
        }

        private void ApplyRealtimeAuthoritySnapshot(OnlineDungeonRealtimeAuthoritySnapshotInfo snapshot)
        {
            if (!ShouldDriveOnlineWorldSimulation() && Time.unscaledTime >= _nextOnlineEnemyFreezeDebugTime)
            {
                OnlineDungeonAuthorityStateInfo state = snapshot?.state;
                _nextOnlineEnemyFreezeDebugTime = Time.unscaledTime + OnlineEnemyFreezeDebugIntervalSeconds;
                Debug.Log($"[OnlineEnemyFreezeDebug] Authority snapshot received. hasState={state != null} initialized={(state != null && state.initialized)} version={(state != null ? state.version : 0)} lastVersion={_lastAppliedAuthorityStateVersion} enemyCount={(state?.enemies != null ? state.enemies.Count : 0)} scene={SceneManager.GetActiveScene().name} worldScene={_lastAppliedWorldSnapshotSceneName} sceneReady={IsAuthorityStateSceneReady()}");
            }

            ApplyAuthorityState(snapshot?.state);
        }

        private void ApplyRealtimeUiPanelSnapshot(OnlineDungeonRealtimeUiPanelSnapshotInfo snapshot)
        {
            ApplyUiPanelEvents(snapshot?.events);
            ApplyUiPanelState(snapshot?.state);
        }

        private void ApplyRealtimeSceneLoadSnapshot(OnlineDungeonRealtimeSceneLoadSnapshotInfo snapshot)
        {
            ApplySceneLoadState(snapshot?.state);
        }

        private void ApplyRealtimeDamageSnapshot(OnlineDungeonRealtimeDamageSnapshotInfo snapshot)
        {
            ApplyDamageEvents(snapshot?.ackEvents, false);
            ApplyDamageEvents(snapshot?.events, true);
        }

        private void ApplyRealtimeRewardSnapshot(OnlineDungeonRealtimeRewardSnapshotInfo snapshot)
        {
            if (snapshot == null)
                return;

            bool hasRewardStateItems = snapshot.state != null
                && ((snapshot.state.drops != null && snapshot.state.drops.Count > 0)
                    || (snapshot.state.killRewards != null && snapshot.state.killRewards.Count > 0));
            if (!string.IsNullOrWhiteSpace(snapshot.ackAction) || snapshot.rejected || snapshot.dropPickup != null || hasRewardStateItems)
                Debug.Log($"[OnlineLootDebug] Reward snapshot received. ackAction={snapshot.ackAction} ackTarget={snapshot.ackTargetId} rejected={snapshot.rejected} hasDropPickup={snapshot.dropPickup != null} hasState={snapshot.state != null} stateVersion={(snapshot.state != null ? snapshot.state.version : 0)} dropCount={(snapshot.state?.drops != null ? snapshot.state.drops.Count : 0)} lastVersion={_lastAppliedRewardStateVersion} pendingChests={_pendingOnlineChestOpens.Count} pendingDrops={_pendingOnlineDropPickups.Count} scene={SceneManager.GetActiveScene().name}");
            if (snapshot.rejected)
            {
                ResetRejectedRewardPending(snapshot.ackAction, snapshot.ackTargetId);
                if (!string.IsNullOrWhiteSpace(snapshot.errorMessage))
                    Debug.LogWarning($"[OnlineDungeonSessionCoordinator] 联机副本奖励事件被拒绝：{snapshot.errorMessage}");
                return;
            }

            ApplyKillRewardClaimResult(snapshot.killRewardClaim);
            ApplyDropPickupResult(snapshot.dropPickup);
            ApplyRewardState(snapshot.state);
        }

        private void ApplyDamageEvents(List<OnlineDungeonDamageEventInfo> damageEvents, bool advanceSequence)
        {
            if (damageEvents == null)
                return;

            damageEvents.Sort((left, right) => CompareEventSequence(left?.sequence ?? 0, right?.sequence ?? 0));
            for (int i = 0; i < damageEvents.Count; i++)
            {
                OnlineDungeonDamageEventInfo damageEvent = damageEvents[i];
                if (damageEvent == null)
                    continue;

                if (advanceSequence)
                    _lastReceivedDamageEventSequence = Math.Max(_lastReceivedDamageEventSequence, damageEvent.sequence);

                if (!TryMarkDamageEventApplied(damageEvent))
                    continue;

                if (string.Equals(damageEvent.targetKind, "player", StringComparison.Ordinal))
                {
                    ApplyLocalPlayerDamageEvent(damageEvent);
                    continue;
                }

                ApplyEnemyDamageEvent(damageEvent);
            }
        }

        private bool TryMarkDamageEventApplied(OnlineDungeonDamageEventInfo damageEvent)
        {
            if (damageEvent == null || string.IsNullOrWhiteSpace(damageEvent.eventId))
                return true;

            return _appliedDamageEventIds.Add(damageEvent.eventId.Trim());
        }

        private void ApplyEnemyDamageEvent(OnlineDungeonDamageEventInfo damageEvent)
        {
            EnemyController enemy = EnemyController.FindByRuntimeId(damageEvent.targetRuntimeId);
            if (enemy == null)
                return;

            enemy.ApplyOnlineAuthorityState(damageEvent.targetRemainingHp, damageEvent.targetDied, damageEvent.targetShowCombatHealthBar);
            CombatNumberDispatcher.PublishDamage(enemy.transform, damageEvent.damage, false);
        }

        private void ApplyLocalPlayerDamageEvent(OnlineDungeonDamageEventInfo damageEvent)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || !string.Equals(currentUser.userId, damageEvent.targetRuntimeId, StringComparison.Ordinal))
                return;

            Transform playerTransform = GameStateMachine.GetInstance().LevelPlayerTransform;
            PlayerController player = playerTransform != null ? playerTransform.GetComponent<PlayerController>() : null;
            if (player == null)
                return;

            player.ApplyOnlineAuthorityHit(damageEvent.targetRemainingHp, damageEvent.targetDied, damageEvent.stunDuration);
        }

        private void ApplyRewardState(OnlineDungeonRewardStateInfo state)
        {
            if (state == null)
                return;

            if (state.version <= _lastAppliedRewardStateVersion)
                return;

            Debug.Log($"[OnlineLootDebug] Apply reward state. version={state.version} lastVersion={_lastAppliedRewardStateVersion} killRewardCount={(state.killRewards != null ? state.killRewards.Count : 0)} dropCount={(state.drops != null ? state.drops.Count : 0)} scene={SceneManager.GetActiveScene().name}");
            if (state.killRewards != null)
            {
                for (int i = 0; i < state.killRewards.Count; i++)
                    ApplyKillRewardState(state.killRewards[i]);
            }

            if (state.drops != null)
            {
                for (int i = 0; i < state.drops.Count; i++)
                    ApplyOnlineDropState(state.drops[i]);
            }

            _lastAppliedRewardStateVersion = state.version;
        }

        private void ApplyKillRewardState(OnlineDungeonKillRewardInfo reward)
        {
            if (reward == null || string.IsNullOrWhiteSpace(reward.enemyRuntimeId) || _appliedKillRewards.Contains(reward.enemyRuntimeId))
                return;

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || !string.Equals(currentUser.userId, reward.killerUserId, StringComparison.Ordinal))
                return;

            if (reward.claimed || _pendingKillRewardClaims.Contains(reward.enemyRuntimeId))
                return;

            _pendingKillRewardClaims.Add(reward.enemyRuntimeId);
            if (_realtimeClient.IsRunning)
                _realtimeClient.EnqueueKillRewardClaim(reward.enemyRuntimeId);
            else
                LogWorldSnapshotWarning("联机副本实时通道未启动，副本击杀奖励无法领取。");
        }

        private void ApplyKillRewardClaimResult(OnlineDungeonKillRewardClaimResultInfo result)
        {
            if (result == null)
                return;

            if (result.accepted)
                ApplyClaimedKillRewardToLocalPlayer(result.reward);
            else if (result.reward != null && !string.IsNullOrWhiteSpace(result.reward.enemyRuntimeId))
                _pendingKillRewardClaims.Remove(result.reward.enemyRuntimeId);

            ApplyRewardState(result.state);
        }

        private void ApplyClaimedKillRewardToLocalPlayer(OnlineDungeonKillRewardInfo reward)
        {
            if (reward == null || string.IsNullOrWhiteSpace(reward.enemyRuntimeId))
                return;

            if (_appliedKillRewards.Contains(reward.enemyRuntimeId))
            {
                _pendingKillRewardClaims.Remove(reward.enemyRuntimeId);
                return;
            }

            PlayerModel player = GameStateMachine.GetInstance().Player;
            EnemyDeathRewardSystem.GrantFixedRewards(player, new EnemyDeathRewardSystem.RewardProposal
            {
                exp = reward.exp,
                gold = reward.gold,
                talentPoints = reward.talentPoints,
            });
            _appliedKillRewards.Add(reward.enemyRuntimeId);
            _pendingKillRewardClaims.Remove(reward.enemyRuntimeId);
        }

        private void ApplyOnlineDropState(OnlineDungeonDropInfo drop)
        {
            if (drop == null || string.IsNullOrWhiteSpace(drop.dropId))
            {
                Debug.Log("[OnlineLootDebug] Online drop state skipped because drop or dropId is empty.");
                return;
            }

            if (drop.pickedUp)
            {
                Debug.Log($"[OnlineLootDebug] Online drop state says picked up. drop={drop.dropId} pickedBy={drop.pickedUpByUserId} scene={SceneManager.GetActiveScene().name}");
                RemoveOnlineDrop(drop.dropId);
                return;
            }

            if (_spawnedDropIds.Contains(drop.dropId))
                return;

            DroppedPickupRuntime pickup = DroppedPickupRuntime.SpawnOnlineDrop(drop);
            if (pickup == null)
            {
                Debug.Log($"[OnlineLootDebug] Online drop state spawn failed. drop={drop.dropId} itemType={drop.itemType} count={drop.count} scene={SceneManager.GetActiveScene().name}");
                return;
            }

            _spawnedDropIds.Add(drop.dropId);
            _onlineDrops[drop.dropId] = pickup;
            Debug.Log($"[OnlineLootDebug] Online drop state spawned. drop={drop.dropId} itemType={drop.itemType} spawnedDrops={_onlineDrops.Count} scene={SceneManager.GetActiveScene().name}");
        }

        public bool TryRequestPickupOnlineDrop(string dropId)
        {
            if (!HasActiveSession || string.IsNullOrWhiteSpace(dropId))
            {
                Debug.Log($"[OnlineLootDebug] Drop pickup request rejected before user check. drop={dropId} hasSession={HasActiveSession} scene={SceneManager.GetActiveScene().name}");
                return false;
            }

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId))
            {
                Debug.Log($"[OnlineLootDebug] Drop pickup request rejected because current user is missing. drop={dropId} scene={SceneManager.GetActiveScene().name}");
                return false;
            }

            string normalizedDropId = dropId.Trim();
            if (_pendingOnlineDropPickups.Contains(normalizedDropId))
            {
                Debug.Log($"[OnlineLootDebug] Drop pickup request rejected because already pending. drop={normalizedDropId} pendingDrops={_pendingOnlineDropPickups.Count} scene={SceneManager.GetActiveScene().name}");
                return false;
            }

            if (!_realtimeClient.IsRunning)
            {
                Debug.Log($"[OnlineLootDebug] Drop pickup request rejected because realtime is not running. drop={normalizedDropId} scene={SceneManager.GetActiveScene().name}");
                LogWorldSnapshotWarning("联机副本实时通道未启动，副本掉落无法拾取。");
                return false;
            }

            _pendingOnlineDropPickups.Add(normalizedDropId);
            _realtimeClient.EnqueueDropPickup(normalizedDropId);
            Debug.Log($"[OnlineLootDebug] Drop pickup request queued. drop={normalizedDropId} pendingDrops={_pendingOnlineDropPickups.Count} lastRewardVersion={_lastAppliedRewardStateVersion} scene={SceneManager.GetActiveScene().name}");
            return true;
        }

        public bool TryRequestOpenOnlineChest(ChestInteractable chest, int dropCount)
        {
            if (!HasActiveSession || chest == null)
            {
                Debug.Log($"[OnlineLootDebug] Chest open request rejected before user check. chest={(chest != null ? chest.GetSnapshotId() : string.Empty)} hasSession={HasActiveSession} chestNull={chest == null} scene={SceneManager.GetActiveScene().name}");
                return false;
            }

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId))
            {
                Debug.Log($"[OnlineLootDebug] Chest open request rejected because current user is missing. chest={chest.GetSnapshotId()} scene={SceneManager.GetActiveScene().name}");
                return false;
            }

            if (!chest.TryBuildSnapshot(out ChestSnapshotSave snapshot) || snapshot == null || string.IsNullOrWhiteSpace(snapshot.snapshotId))
            {
                Debug.Log($"[OnlineLootDebug] Chest open request rejected because snapshot is invalid. chest={chest.GetSnapshotId()} snapshotNull={snapshot == null} dropCount={dropCount} scene={SceneManager.GetActiveScene().name}");
                return false;
            }

            string chestId = snapshot.snapshotId.Trim();
            if (_openedOnlineChests.Contains(chestId) || _pendingOnlineChestOpens.Contains(chestId))
            {
                Debug.Log($"[OnlineLootDebug] Chest open request rejected because opened or pending. chest={chestId} opened={_openedOnlineChests.Contains(chestId)} pending={_pendingOnlineChestOpens.Contains(chestId)} pendingChests={_pendingOnlineChestOpens.Count} scene={SceneManager.GetActiveScene().name}");
                return false;
            }

            if (!_realtimeClient.IsRunning)
            {
                Debug.Log($"[OnlineLootDebug] Chest open request rejected because realtime is not running. chest={chestId} scene={SceneManager.GetActiveScene().name}");
                LogWorldSnapshotWarning("联机副本实时通道未启动，副本宝箱无法开启。");
                return false;
            }

            _pendingOnlineChestOpens.Add(chestId);
            _realtimeClient.EnqueueChestOpen(new OnlineDungeonChestOpenRequest
            {
                userId = currentUser.userId,
                joinToken = _activeAidSession.dungeonJoinToken,
                chestId = chestId,
                prefabId = snapshot.prefabId,
                dropCount = Mathf.Max(1, dropCount),
                x = snapshot.x,
                y = snapshot.y,
                z = snapshot.z,
                yaw = snapshot.yaw,
            });

            Debug.Log($"[OnlineLootDebug] Chest open request queued. chest={chestId} prefab={snapshot.prefabId} dropCount={Mathf.Max(1, dropCount)} pendingChests={_pendingOnlineChestOpens.Count} lastRewardVersion={_lastAppliedRewardStateVersion} scene={SceneManager.GetActiveScene().name}");
            return true;
        }

        public bool ApplyKnownOpenedOnlineChest(ChestInteractable chest)
        {
            if (!HasActiveSession || chest == null)
                return false;

            string chestId = chest.GetSnapshotId();
            if (string.IsNullOrWhiteSpace(chestId))
                return false;

            chestId = chestId.Trim();
            if (!_openedOnlineChests.Contains(chestId))
                return false;

            Debug.Log($"[OnlineLootDebug] Apply known opened chest to runtime. chest={chestId} name={chest.name} scene={SceneManager.GetActiveScene().name}");
            chest.ApplyOnlineAuthorityOpened();
            return true;
        }

        private void ResetPendingOnlineChestOpens()
        {
            if (_pendingOnlineChestOpens.Count <= 0)
                return;

            HashSet<string> pendingChestIds = new HashSet<string>(_pendingOnlineChestOpens, StringComparer.Ordinal);
            _pendingOnlineChestOpens.Clear();
            ChestInteractable[] chests = UnityEngine.Object.FindObjectsByType<ChestInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < chests.Length; i++)
            {
                ChestInteractable chest = chests[i];
                if (chest != null && pendingChestIds.Contains(chest.GetSnapshotId()))
                    chest.CancelPendingOnlineOpen();
            }
        }

        private void ResetPendingOnlineDropPickups()
        {
            _pendingOnlineDropPickups.Clear();
        }

        private void ResetRejectedRewardPending(string action, string targetId)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;

            string normalizedTargetId = targetId != null ? targetId.Trim() : string.Empty;
            if (string.Equals(action, "pickupDrop", StringComparison.Ordinal))
            {
                Debug.Log($"[OnlineLootDebug] Reset rejected pickup pending. drop={normalizedTargetId} removed={_pendingOnlineDropPickups.Contains(normalizedTargetId)} scene={SceneManager.GetActiveScene().name}");
                if (!string.IsNullOrWhiteSpace(normalizedTargetId))
                    _pendingOnlineDropPickups.Remove(normalizedTargetId);
                return;
            }

            if (string.Equals(action, "claimKillReward", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(normalizedTargetId))
                    _pendingKillRewardClaims.Remove(normalizedTargetId);
                return;
            }

            if (!string.Equals(action, "openChest", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(normalizedTargetId))
                return;

            Debug.Log($"[OnlineLootDebug] Reset rejected chest pending. chest={normalizedTargetId} removed={_pendingOnlineChestOpens.Contains(normalizedTargetId)} scene={SceneManager.GetActiveScene().name}");
            _pendingOnlineChestOpens.Remove(normalizedTargetId);
            ChestInteractable[] chests = UnityEngine.Object.FindObjectsByType<ChestInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < chests.Length; i++)
            {
                ChestInteractable chest = chests[i];
                if (chest != null && string.Equals(chest.GetSnapshotId(), normalizedTargetId, StringComparison.Ordinal))
                    chest.CancelPendingOnlineOpen();
            }
        }

        private void ApplyDropPickupResult(OnlineDungeonDropPickupResultInfo result)
        {
            if (result == null)
                return;

            string dropId = result.drop != null ? result.drop.dropId : string.Empty;
            Debug.Log($"[OnlineLootDebug] Drop pickup result received. accepted={result.accepted} drop={dropId} pickedUp={(result.drop != null && result.drop.pickedUp)} hasState={result.state != null} stateVersion={(result.state != null ? result.state.version : 0)} pendingBefore={(string.IsNullOrWhiteSpace(dropId) ? false : _pendingOnlineDropPickups.Contains(dropId))} scene={SceneManager.GetActiveScene().name}");
            if (!string.IsNullOrWhiteSpace(dropId))
            {
                _pendingOnlineDropPickups.Remove(dropId);
            }

            if (result.accepted)
            {
                if (string.IsNullOrWhiteSpace(dropId) || _appliedDropPickups.Add(dropId))
                    ApplyPickedUpDropToLocalPlayer(result.drop);
                RemoveOnlineDrop(dropId);
            }

            ApplyRewardState(result.state);
        }

        private void ApplyPickedUpDropToLocalPlayer(OnlineDungeonDropInfo drop)
        {
            if (drop == null)
                return;

            PlayerModel player = GameStateMachine.GetInstance().Player;
            if (player == null)
            {
                Debug.Log($"[OnlineLootDebug] Apply picked drop skipped because player model is missing. drop={drop.dropId} itemType={drop.itemType} scene={SceneManager.GetActiveScene().name}");
                return;
            }

            if (string.Equals(drop.itemType, "weapon", StringComparison.OrdinalIgnoreCase))
            {
                WeaponInstance weapon = JsonConvert.DeserializeObject<WeaponInstance>(drop.payloadJson);
                bool canAdd = weapon != null && player.CanAddWeapon();
                bool added = canAdd && player.AddWeapon(weapon);
                Debug.Log($"[OnlineLootDebug] Apply picked weapon drop. drop={drop.dropId} weaponNull={weapon == null} canAdd={canAdd} added={added} scene={SceneManager.GetActiveScene().name}");
                if (added)
                    EventCenter.GetInstance().EventTrigger(GameEvents.ItemPickedUp, weapon);
            }
            else
            {
                ItemStackSave stack = JsonConvert.DeserializeObject<ItemStackSave>(drop.payloadJson);
                string itemId = stack != null ? stack.itemId : drop.payloadJson;
                int count = stack != null ? Mathf.Max(1, stack.count) : Mathf.Max(1, drop.count);
                bool canAdd = !string.IsNullOrWhiteSpace(itemId) && player.CanAddStackable(itemId, count);
                Debug.Log($"[OnlineLootDebug] Apply picked stack drop. drop={drop.dropId} item={itemId} count={count} canAdd={canAdd} scene={SceneManager.GetActiveScene().name}");
                if (canAdd)
                {
                    player.AddItemCount(itemId, count);
                    EventCenter.GetInstance().EventTrigger(GameEvents.ItemPickedUp, itemId);
                }
            }

            EventCenter.GetInstance().EventTrigger(GameEvents.InventoryChanged);
        }

        private void RemoveOnlineDrop(string dropId)
        {
            if (!_onlineDrops.TryGetValue(dropId, out DroppedPickupRuntime pickup))
            {
                Debug.Log($"[OnlineLootDebug] Remove online drop skipped because runtime is missing. drop={dropId} spawnedContains={_spawnedDropIds.Contains(dropId)} scene={SceneManager.GetActiveScene().name}");
                return;
            }

            _onlineDrops.Remove(dropId);
            _spawnedDropIds.Remove(dropId);
            _pendingOnlineDropPickups.Remove(dropId);
            Debug.Log($"[OnlineLootDebug] Remove online drop runtime. drop={dropId} pickupNull={pickup == null} remainingDrops={_onlineDrops.Count} scene={SceneManager.GetActiveScene().name}");
            if (pickup != null)
                UnityEngine.Object.Destroy(pickup.gameObject);
        }

        private void ClearOnlineDrops()
        {
            foreach (KeyValuePair<string, DroppedPickupRuntime> pair in _onlineDrops)
            {
                if (pair.Value != null)
                    UnityEngine.Object.Destroy(pair.Value.gameObject);
            }

            _onlineDrops.Clear();
            _spawnedDropIds.Clear();
            _pendingOnlineDropPickups.Clear();
            _appliedDropPickups.Clear();
            _pendingOnlineChestOpens.Clear();
        }

        private void ApplyAuthorityState(OnlineDungeonAuthorityStateInfo state)
        {
            if (state == null || !state.initialized || state.version <= _lastAppliedAuthorityStateVersion)
            {
                if (!ShouldDriveOnlineWorldSimulation() && Time.unscaledTime >= _nextOnlineEnemyFreezeDebugTime)
                {
                    _nextOnlineEnemyFreezeDebugTime = Time.unscaledTime + OnlineEnemyFreezeDebugIntervalSeconds;
                    Debug.Log($"[OnlineEnemyFreezeDebug] Authority state skipped. reason={(state == null ? "null" : !state.initialized ? "notInitialized" : "oldVersion")} version={(state != null ? state.version : 0)} lastVersion={_lastAppliedAuthorityStateVersion} scene={SceneManager.GetActiveScene().name}");
                }

                return;
            }

            if (!ShouldDriveOnlineWorldSimulation() && !IsAuthorityStateSceneReady())
            {
                if (Time.unscaledTime >= _nextOnlineEnemyFreezeDebugTime)
                {
                    _nextOnlineEnemyFreezeDebugTime = Time.unscaledTime + OnlineEnemyFreezeDebugIntervalSeconds;
                    Debug.Log($"[OnlineEnemyFreezeDebug] Authority state delayed because scene is not ready. version={state.version} enemyCount={(state.enemies != null ? state.enemies.Count : 0)} activeScene={SceneManager.GetActiveScene().name} worldScene={_lastAppliedWorldSnapshotSceneName} pendingScene={_pendingSceneSyncName}");
                }

                return;
            }

            if (state.enemies != null)
            {
                for (int i = 0; i < state.enemies.Count; i++)
                    ApplyEnemyAuthorityState(state.enemies[i]);

                PruneEnemiesMissingFromAuthorityState(state.enemies);
            }

            if (state.chests != null)
            {
                for (int i = 0; i < state.chests.Count; i++)
                    ApplyChestAuthorityState(state.chests[i]);
            }

            _lastAppliedAuthorityStateVersion = state.version;
        }

        private void ApplyChestAuthorityState(OnlineDungeonChestAuthorityInfo chestState)
        {
            if (chestState == null || string.IsNullOrWhiteSpace(chestState.chestId))
                return;

            string chestId = chestState.chestId.Trim();
            if (chestState.opened)
            {
                bool openedAdded = _openedOnlineChests.Add(chestId);
                bool pendingBefore = _pendingOnlineChestOpens.Contains(chestId);
                bool pendingRemoved = _pendingOnlineChestOpens.Remove(chestId);
                ChestInteractable[] chests = UnityEngine.Object.FindObjectsByType<ChestInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                int matchedCount = 0;
                for (int i = 0; i < chests.Length; i++)
                {
                    ChestInteractable chest = chests[i];
                    if (chest != null && string.Equals(chest.GetSnapshotId(), chestId, StringComparison.Ordinal))
                    {
                        matchedCount++;
                        chest.ApplyOnlineAuthorityOpened();
                    }
                }

                Debug.Log($"[OnlineLootDebug] Apply chest authority opened. chest={chestId} openedAdded={openedAdded} pendingBefore={pendingBefore} pendingRemoved={pendingRemoved} matched={matchedCount} scanned={chests.Length} openedBy={chestState.openedByUserId} scene={SceneManager.GetActiveScene().name}");
                return;
            }

            if (ShouldDriveOnlineWorldSimulation())
                return;

            LevelBootstrapper bootstrapper = UnityEngine.Object.FindFirstObjectByType<LevelBootstrapper>();
            bootstrapper?.ApplyOnlineChestAuthorityState(chestState);
        }

        private void ApplyEnemyAuthorityState(OnlineDungeonEnemyAuthorityInfo enemyState)
        {
            if (enemyState == null || string.IsNullOrWhiteSpace(enemyState.runtimeId))
                return;

            EnemyController enemy = EnemyController.FindByRuntimeId(enemyState.runtimeId);
            if (enemy == null)
            {
                if (ShouldDriveOnlineWorldSimulation())
                    return;

                Debug.Log($"[OnlineEnemyFreezeDebug] Authority enemy missing locally, spawning. runtime={enemyState.runtimeId} enemyId={enemyState.enemyId} hp={enemyState.currentHp} dead={enemyState.isDead} scene={SceneManager.GetActiveScene().name}");
                RemoveDuplicateFinalBossesBeforeAuthoritySpawn(enemyState.runtimeId);
                enemy = SpawnEnemyFromAuthorityState(enemyState, BuildOnlineDebugContext());
                if (enemy == null)
                {
                    Debug.Log($"[OnlineEnemyFreezeDebug] Authority enemy spawn failed. runtime={enemyState.runtimeId} enemyId={enemyState.enemyId} dead={enemyState.isDead} scene={SceneManager.GetActiveScene().name}");
                    return;
                }
            }

            if (!ShouldDriveOnlineWorldSimulation())
                enemy.transform.SetPositionAndRotation(
                    new Vector3(enemyState.x, enemyState.y, enemyState.z),
                    Quaternion.Euler(0f, enemyState.yaw, 0f));

            bool countsAsLevelBoss = IsFinalBossAuthorityContext(enemyState) || enemyState.countsAsLevelBoss;
            enemy.SetCountsAsLevelBoss(countsAsLevelBoss);
            enemy.ApplyOnlineAuthorityState(enemyState.currentHp, enemyState.isDead, enemyState.showCombatHealthBar);
            if (!ShouldDriveOnlineWorldSimulation())
            {
                if (Time.unscaledTime >= _nextOnlineEnemyFreezeApplyDebugTime && (enemyState.moveBlend > 0.05f || enemyState.activeSkillSequence > 0 || enemyState.isDead))
                {
                    _nextOnlineEnemyFreezeApplyDebugTime = Time.unscaledTime + OnlineEnemyFreezeDebugIntervalSeconds;
                    Debug.Log($"[OnlineEnemyFreezeDebug] Apply enemy authority presentation. runtime={enemyState.runtimeId} enemyId={enemyState.enemyId} hp={enemyState.currentHp:F1} dead={enemyState.isDead} pos=({enemyState.x:F2},{enemyState.y:F2},{enemyState.z:F2}) yaw={enemyState.yaw:F1} move=({enemyState.moveBlend:F3},{enemyState.moveForward:F3},{enemyState.moveStrafe:F3}) skillSeq={enemyState.activeSkillSequence} skill={enemyState.activeSkillId} trigger={enemyState.activeSkillAnimationTrigger} scene={SceneManager.GetActiveScene().name}");
                }

                enemy.ApplyOnlineRemoteMovementPresentation(enemyState.moveBlend, enemyState.moveForward, enemyState.moveStrafe);
                enemy.ApplyOnlineRemoteSkillPresentation(
                    enemyState.activeSkillSequence,
                    enemyState.activeSkillSlot,
                    enemyState.activeSkillId,
                    enemyState.activeSkillAnimationTrigger,
                    enemyState.hasActiveSkillTarget,
                    new Vector3(enemyState.activeSkillTargetX, enemyState.activeSkillTargetY, enemyState.activeSkillTargetZ));
            }
        }

        private static EnemyController SpawnEnemyFromAuthorityState(OnlineDungeonEnemyAuthorityInfo enemyState, string debugContext)
        {
            if (enemyState == null || string.IsNullOrWhiteSpace(enemyState.enemyId) || enemyState.isDead)
                return null;

            EnemySnapshot snapshot = new EnemySnapshot
            {
                id = enemyState.enemyId,
                runtimeId = enemyState.runtimeId,
                enemyType = enemyState.enemyType,
                x = enemyState.x,
                y = enemyState.y,
                z = enemyState.z,
                yaw = enemyState.yaw,
                currentHp = enemyState.currentHp,
                currentPoise = enemyState.currentPoise,
                countsAsLevelBoss = IsFinalBossAuthorityContext(enemyState) || enemyState.countsAsLevelBoss,
            };
            EnemyController enemy = EnemySpawnRuntime.SpawnEnemyFromSnapshot(snapshot, EnemySpawnRuntime.BuildVariantCatalog());
            enemy?.SetOnlineRemoteSimulationDisabled(true);
            return enemy;
        }

        private static bool IsFinalBossAuthorityContext(OnlineDungeonEnemyAuthorityInfo enemyState)
        {
            return enemyState != null
                   && enemyState.enemyType == (int)EnemyType.Boss
                   && FinalBossDuelSceneRuntime.IsFinalBossDuelScene();
        }

        private void PruneEnemiesMissingFromAuthorityState(List<OnlineDungeonEnemyAuthorityInfo> enemyStates)
        {
            if (ShouldDriveOnlineWorldSimulation() || enemyStates == null)
                return;

            HashSet<string> authorityRuntimeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < enemyStates.Count; i++)
            {
                OnlineDungeonEnemyAuthorityInfo enemyState = enemyStates[i];
                if (enemyState != null && !string.IsNullOrWhiteSpace(enemyState.runtimeId))
                    authorityRuntimeIds.Add(enemyState.runtimeId.Trim());
            }

            EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || authorityRuntimeIds.Contains(enemy.RuntimeId))
                    continue;

                UnityEngine.Object.Destroy(enemy.gameObject);
            }
        }

        private void RemoveDuplicateFinalBossesBeforeAuthoritySpawn(string authorityRuntimeId)
        {
            if (ShouldDriveOnlineWorldSimulation() || string.IsNullOrWhiteSpace(authorityRuntimeId) || !FinalBossDuelSceneRuntime.IsFinalBossDuelScene())
                return;

            EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || enemy.EnemyCategory != EnemyType.Boss)
                    continue;

                if (string.Equals(enemy.RuntimeId, authorityRuntimeId, StringComparison.Ordinal))
                    continue;

                UnityEngine.Object.Destroy(enemy.gameObject);
            }
        }

        private void ApplyUiPanelEvents(List<OnlineDungeonUiPanelEventInfo> panelEvents)
        {
            if (panelEvents == null)
                return;

            panelEvents.Sort((left, right) => CompareEventSequence(left?.sequence ?? 0, right?.sequence ?? 0));
            for (int i = 0; i < panelEvents.Count; i++)
            {
                OnlineDungeonUiPanelEventInfo panelEvent = panelEvents[i];
                if (panelEvent == null)
                    continue;

                _lastReceivedUiPanelEventSequence = Math.Max(_lastReceivedUiPanelEventSequence, panelEvent.sequence);
                if (!TryMarkUiPanelEventApplied(panelEvent))
                    continue;

                if (!GameplayUIInputBridge.ApplyNetworkPanelState(panelEvent.panelName, panelEvent.open))
                    Debug.LogWarning($"[OnlineDungeonSessionCoordinator] 收到未知的联机面板同步请求：{panelEvent.panelName}");
            }
        }

        private bool TryMarkUiPanelEventApplied(OnlineDungeonUiPanelEventInfo panelEvent)
        {
            if (panelEvent == null || string.IsNullOrWhiteSpace(panelEvent.eventId))
                return true;

            return _appliedUiPanelEventIds.Add(panelEvent.eventId.Trim());
        }

        private static int CompareEventSequence(long left, long right)
        {
            if (left < right)
                return -1;
            if (left > right)
                return 1;
            return 0;
        }

        private void ApplyUiPanelState(OnlineDungeonUiPanelStateInfo state)
        {
            if (state == null || state.version <= _lastAppliedUiPanelStateVersion)
                return;

            _lastAppliedUiPanelStateVersion = state.version;
            string panelName = state.hasOpenPanel ? state.openPanelName : string.Empty;
            if (!string.IsNullOrWhiteSpace(panelName))
            {
                if (!GameplayUIInputBridge.ApplyNetworkPanelState(panelName, true))
                    Debug.LogWarning($"[OnlineDungeonSessionCoordinator] 收到未知的联机面板状态：{panelName}");
                return;
            }

            CloseKnownGameplayPanels();
        }

        private static void CloseKnownGameplayPanels()
        {
            if (GameplayUIInputBridge.ApplyNetworkPanelState(string.Empty, false))
                return;

            GameplayUIInputBridge.ApplyNetworkPanelState(PanelNames.Backpack, false);
            GameplayUIInputBridge.ApplyNetworkPanelState(PanelNames.SkillTree, false);
            GameplayUIInputBridge.ApplyNetworkPanelState(PanelNames.KeyConfig, false);
            GameplayUIInputBridge.ApplyNetworkPanelState(PanelNames.Menu, false);
        }

        private void ApplySceneLoadState(OnlineDungeonSceneLoadStateInfo state)
        {
            if (!HasActiveSession)
                return;

            if (state == null || state.version <= _lastAppliedSceneLoadVersion)
            {
                return;
            }

            if (!state.active || string.IsNullOrWhiteSpace(state.transitionId) || string.IsNullOrWhiteSpace(state.sceneName))
            {
                _lastAppliedSceneLoadVersion = state.version;
                return;
            }

            if (string.Equals(_completedOnlineSceneLoadTransitionId, state.transitionId, StringComparison.Ordinal))
            {
                _lastAppliedSceneLoadVersion = state.version;
                return;
            }

            if (state.released && _onlineSceneLoadReleaseLocalTime <= 0f && !TryResolveSceneLoadReleaseLocalTime(state, out _onlineSceneLoadReleaseLocalTime))
            {
                Debug.LogWarning($"[OnlineDungeonSessionCoordinator] 联机场景加载屏障缺少有效放行时间：{state.sceneName}");
                return;
            }

            if (!_onlineSceneLoadInProgress || !string.Equals(_onlineSceneLoadTransitionId, state.transitionId, StringComparison.Ordinal))
                BeginOnlineSceneLoad(state);

            _lastAppliedSceneLoadVersion = state.version;
            if (string.Equals(_onlineSceneLoadTransitionId, state.transitionId, StringComparison.Ordinal) && state.released)
            {
                Debug.Log($"[OnlineDungeonSessionCoordinator] 联机场景加载屏障已放行：{state.sceneName} ready={state.readyCount}/{state.participantCount}");
                _onlineSceneLoadReleased = true;
            }
        }

        private void BeginOnlineSceneLoad(OnlineDungeonSceneLoadStateInfo state)
        {
            string sceneName = state.sceneName.Trim();
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                LogWorldSnapshotWarning($"副本目标场景未加入 Build Settings：{sceneName}");
                return;
            }

            if (FinalBossDuelSceneRuntime.IsFinalBossDuelSceneName(sceneName))
                FinalBossDuelRuntimeContext.AdoptOnlineChallenge(state.bossId, state.bossDisplayName);

            _pendingSceneSyncName = sceneName;
            _onlineSceneLoadTransitionId = state.transitionId.Trim();
            _onlineSceneLoadSceneName = sceneName;
            _onlineSceneLoadInProgress = true;
            _onlineSceneLoadReleased = state.released;
            _onlineSceneLoadReadySent = false;
            _onlineSceneLoadReleaseLocalTime = 0f;
            if (state.released && TryResolveSceneLoadReleaseLocalTime(state, out float releaseLocalTime))
                _onlineSceneLoadReleaseLocalTime = releaseLocalTime;
            AdoptTargetLevelRuntimeContext(Mathf.Max(1, _activeAidSession.hostLevelIndex));
            Debug.Log($"[OnlineDungeonSessionCoordinator] 开始联机场景加载屏障：{sceneName}");
            LoadSceneWithProgressWaitingForOnlineRelease(
                sceneName,
                () => IsOnlineSceneLoadActivationAllowed(sceneName),
                MarkOnlineSceneLoadReady,
                FinishOnlineSceneLoad);
        }

        private bool IsOnlineSceneLoadActivationAllowed(string sceneName)
        {
            return _onlineSceneLoadReleased
                   && string.Equals(_onlineSceneLoadSceneName, sceneName, StringComparison.Ordinal)
                   && _onlineSceneLoadReleaseLocalTime > 0f
                   && Time.unscaledTime >= _onlineSceneLoadReleaseLocalTime;
        }

        private bool TryResolveSceneLoadReleaseLocalTime(OnlineDungeonSceneLoadStateInfo state, out float localTime)
        {
            localTime = 0f;
            if (state == null || string.IsNullOrWhiteSpace(state.releaseAtUtc))
                return false;

            return _realtimeClient.TryConvertServerUtcToLocalTime(state.releaseAtUtc, out localTime);
        }

        private void MarkOnlineSceneLoadReady()
        {
            if (_onlineSceneLoadReadySent || string.IsNullOrWhiteSpace(_onlineSceneLoadTransitionId))
            {
                return;
            }

            _onlineSceneLoadReadySent = true;
            Debug.Log($"[OnlineDungeonSessionCoordinator] 本机联机场景加载已到 100%，等待其他玩家：{_onlineSceneLoadSceneName}");
            OnlineDungeonSceneLoadEventRequest request = new OnlineDungeonSceneLoadEventRequest
            {
                eventId = Guid.NewGuid().ToString("N"),
                action = "ready",
                transitionId = _onlineSceneLoadTransitionId,
            };
            EnqueueRealtimeSceneLoadEvent(request);
        }

        private void FinishOnlineSceneLoad()
        {
            SendOnlineSceneLoadComplete();
            _completedOnlineSceneLoadTransitionId = _onlineSceneLoadTransitionId;
            _completedOnlineSceneLoadSceneName = _onlineSceneLoadSceneName;
            _pendingSceneSyncName = string.Empty;
            _onlineSceneLoadTransitionId = string.Empty;
            _onlineSceneLoadSceneName = string.Empty;
            _onlineSceneLoadInProgress = false;
            _onlineSceneLoadReleased = false;
            _onlineSceneLoadReadySent = false;
            _onlineSceneLoadReleaseLocalTime = 0f;
        }

        private void SendOnlineSceneLoadComplete()
        {
            if (string.IsNullOrWhiteSpace(_onlineSceneLoadTransitionId))
            {
                return;
            }

            OnlineDungeonSceneLoadEventRequest request = new OnlineDungeonSceneLoadEventRequest
            {
                eventId = Guid.NewGuid().ToString("N"),
                action = "complete",
                transitionId = _onlineSceneLoadTransitionId,
            };
            EnqueueRealtimeSceneLoadEvent(request);
        }

        private IEnumerator UploadWorldSnapshotAfterFrame()
        {
            yield return null;
            _worldSnapshotImmediateUploadCoroutine = null;

            if (!HasActiveSession)
                yield break;

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId) || !IsWorldSnapshotPublisher(currentUser.userId))
                yield break;

            yield return UploadWorldSnapshotOnce(currentUser.userId);
        }

        private IEnumerator WorldSnapshotSyncLoop()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(WorldSnapshotSyncIntervalSeconds);
            while (HasActiveSession)
            {
                SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
                if (currentUser != null && !string.IsNullOrWhiteSpace(currentUser.userId))
                {
                    if (IsWorldSnapshotPublisher(currentUser.userId))
                        yield return UploadWorldSnapshotOnce(currentUser.userId);
                    else
                        yield return PullWorldSnapshotOnce(currentUser.userId);
                }

                yield return wait;
            }

            _worldSnapshotSyncCoroutine = null;
        }

        private IEnumerator UploadWorldSnapshotOnce(string currentUserId)
        {
            LevelBootstrapper bootstrapper = UnityEngine.Object.FindFirstObjectByType<LevelBootstrapper>();
            RunData run = GameStateMachine.GetInstance().CurrentRun;
            if (bootstrapper == null || run == null || !IsActiveTargetLevelScene())
                yield break;

            string sceneName = SceneManager.GetActiveScene().name;
            bootstrapper.CaptureRuntimeSnapshot();
            LevelSnapshot snapshot = run.levelSnapshot;
            if (snapshot == null)
                yield break;

            string snapshotJson;
            try
            {
                snapshotJson = JsonConvert.SerializeObject(snapshot);
            }
            catch (Exception ex)
            {
                LogWorldSnapshotWarning($"序列化副本世界快照失败：{ex.Message}");
                yield break;
            }

            if (string.Equals(snapshotJson, _lastUploadedWorldSnapshotJson, StringComparison.Ordinal) &&
                string.Equals(sceneName, _lastUploadedWorldSnapshotSceneName, StringComparison.Ordinal))
            {
                yield break;
            }

            OnlineDungeonWorldSnapshotRequest request = new OnlineDungeonWorldSnapshotRequest
            {
                userId = currentUserId,
                joinToken = _activeAidSession.dungeonJoinToken,
                sceneName = sceneName,
                snapshotJson = snapshotJson,
            };

            yield return _worldSnapshotClient.UpsertSnapshot(
                _activeAidSession.dungeonServerUrl,
                _activeAidSession.dungeonInstanceId,
                request,
                _ =>
                {
                    _lastUploadedWorldSnapshotJson = snapshotJson;
                    _lastUploadedWorldSnapshotSceneName = sceneName;
                },
                error =>
                {
                    if (!TryHandleSessionClosedError(error))
                        LogWorldSnapshotWarning($"上传副本世界快照失败：{error}");
                });
        }

        private IEnumerator PullWorldSnapshotOnce(string currentUserId)
        {
            if (_isApplyingWorldSnapshot)
                yield break;

            yield return _worldSnapshotClient.GetSnapshot(
                _activeAidSession.dungeonServerUrl,
                _activeAidSession.dungeonInstanceId,
                currentUserId,
                _activeAidSession.dungeonJoinToken,
                ApplyWorldSnapshot,
                error =>
                {
                    if (TryHandleSessionClosedError(error))
                        return;

                    if (!string.Equals(error, "副本世界快照尚未就绪", StringComparison.Ordinal))
                        LogWorldSnapshotWarning($"拉取副本世界快照失败：{error}");
                });
        }

        private void ApplyWorldSnapshot(OnlineDungeonWorldSnapshotInfo snapshotInfo)
        {
            if (snapshotInfo == null ||
                string.IsNullOrWhiteSpace(snapshotInfo.snapshotJson) ||
                snapshotInfo.version <= _lastAppliedWorldSnapshotVersion)
            {
                return;
            }

            LevelBootstrapper bootstrapper = UnityEngine.Object.FindFirstObjectByType<LevelBootstrapper>();
            if (bootstrapper == null || !IsActiveTargetLevelScene())
            {
                return;
            }

            LevelSnapshot snapshot;
            try
            {
                snapshot = JsonConvert.DeserializeObject<LevelSnapshot>(snapshotInfo.snapshotJson);
            }
            catch (Exception ex)
            {
                LogWorldSnapshotWarning($"解析副本世界快照失败：{ex.Message}");
                return;
            }

            if (snapshot == null)
                return;

            string sceneName = string.IsNullOrWhiteSpace(snapshotInfo.sceneName) ? string.Empty : snapshotInfo.sceneName.Trim();
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                LogWorldSnapshotWarning("副本世界快照缺少场景名，已跳过应用。");
                return;
            }

            if (!IsAllowedOnlineScene(sceneName))
            {
                LogWorldSnapshotWarning($"副本世界快照场景不属于当前副本：{sceneName}");
                return;
            }

            if (!IsSceneActive(sceneName))
            {
                if (ShouldIgnoreWorldSnapshotSceneSwitch(sceneName))
                {
                    _lastAppliedWorldSnapshotVersion = snapshotInfo.version;
                    return;
                }

                TryLoadOnlineScene(sceneName, snapshot);
                return;
            }

            _isApplyingWorldSnapshot = true;
            try
            {
                bootstrapper.ApplyOnlineWorldSnapshot(snapshot, () =>
                {
                    bool authoritySceneChanged = !string.Equals(_lastAppliedWorldSnapshotSceneName, sceneName, StringComparison.Ordinal);
                    _lastAppliedWorldSnapshotVersion = snapshotInfo.version;
                    _lastAppliedWorldSnapshotSceneName = sceneName;
                    if (authoritySceneChanged)
                    {
                        _lastAppliedAuthorityStateVersion = 0;
                        _nextOnlineEnemyFreezeDebugTime = 0f;
                        _nextOnlineEnemyFreezeApplyDebugTime = 0f;
                    }
                    _isApplyingWorldSnapshot = false;
                });
            }
            catch (Exception ex)
            {
                _isApplyingWorldSnapshot = false;
                LogWorldSnapshotWarning($"应用副本世界快照失败：{ex.Message}");
            }
        }

        private bool TryHandleSessionClosedError(string error)
        {
            if (!HasActiveSession || string.IsNullOrWhiteSpace(error))
                return false;

            string trimmedError = error.Trim();
            bool isSessionClosed =
                trimmedError.StartsWith("联机副本已关闭", StringComparison.Ordinal) ||
                trimmedError.StartsWith("sessionClosed", StringComparison.Ordinal);
            if (!isSessionClosed)
                return false;

            if (IsAidJoinerRole)
            {
                if (FinalBossDuelSceneRuntime.IsBossVictoryTransitioning())
                {
                    Debug.Log($"[OnlineDungeonSessionCoordinator] 联机副本已关闭，援助方等待 Boss 胜利过场结束后返回原关卡。Reason={error}");
                    return true;
                }

                Debug.Log($"[OnlineDungeonSessionCoordinator] 联机副本已关闭，援助方返回原关卡。Reason={error}");
                ReturnAidJoinerToOwnLevel(true);
            }
            else
            {
                Debug.Log($"[OnlineDungeonSessionCoordinator] 联机副本已关闭。Reason={error}");
                StopSession();
            }

            return true;
        }

        private bool IsWorldSnapshotPublisher(string currentUserId)
        {
            return HasActiveSession &&
                   !string.IsNullOrWhiteSpace(currentUserId) &&
                   string.Equals(currentUserId, _activeAidSession.hostUserId, StringComparison.Ordinal);
        }

        private bool IsActiveTargetLevelScene()
        {
            if (!HasActiveSession)
                return false;

            return IsAllowedOnlineScene(SceneManager.GetActiveScene().name);
        }

        private bool IsAuthorityStateSceneReady()
        {
            if (!IsActiveTargetLevelScene())
                return false;

            if (string.IsNullOrWhiteSpace(_lastAppliedWorldSnapshotSceneName))
                return false;

            return string.Equals(SceneManager.GetActiveScene().name, _lastAppliedWorldSnapshotSceneName, StringComparison.Ordinal);
        }

        private bool IsAllowedOnlineScene(string sceneName)
        {
            if (!HasActiveSession || string.IsNullOrWhiteSpace(sceneName))
                return false;

            string expectedSceneName = GameStateMachine.GetLevelSceneName(Mathf.Max(1, _activeAidSession.hostLevelIndex));
            return string.Equals(sceneName, expectedSceneName, StringComparison.Ordinal)
                   || FinalBossDuelSceneRuntime.IsFinalBossDuelSceneName(sceneName);
        }

        private static bool IsSceneActive(string sceneName)
        {
            return string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.Ordinal);
        }

        private bool ShouldIgnoreWorldSnapshotSceneSwitch(string snapshotSceneName)
        {
            if (ShouldDriveOnlineWorldSimulation() ||
                string.IsNullOrWhiteSpace(snapshotSceneName) ||
                string.IsNullOrWhiteSpace(_completedOnlineSceneLoadTransitionId) ||
                string.IsNullOrWhiteSpace(_completedOnlineSceneLoadSceneName))
                return false;

            if (string.Equals(_pendingSceneSyncName, snapshotSceneName, StringComparison.Ordinal))
                return false;

            return !string.Equals(snapshotSceneName, _completedOnlineSceneLoadSceneName, StringComparison.Ordinal);
        }

        private void TryLoadOnlineScene(string sceneName, LevelSnapshot snapshot)
        {
            if (_onlineSceneLoadInProgress && string.Equals(_onlineSceneLoadSceneName, sceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(_pendingSceneSyncName, sceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                LogWorldSnapshotWarning($"副本目标场景未加入 Build Settings：{sceneName}");
                return;
            }

            if (FinalBossDuelSceneRuntime.IsFinalBossDuelSceneName(sceneName) && !TryAdoptFinalBossChallenge(snapshot))
            {
                return;
            }

            _pendingSceneSyncName = sceneName;
            if (snapshot != null)
            {
                _pendingSpawnPosition = new Vector3(snapshot.playerX, snapshot.playerY, snapshot.playerZ);
                _pendingSpawnYaw = snapshot.playerYaw;
            }

            AdoptTargetLevelRuntimeContext(Mathf.Max(1, _activeAidSession.hostLevelIndex));
            Debug.Log($"[OnlineDungeonSessionCoordinator] 跟随联机副本切换场景：{sceneName}");
            LoadSceneWithProgress(sceneName, () => _pendingSceneSyncName = string.Empty);
        }

        private static bool TryAdoptFinalBossChallenge(LevelSnapshot snapshot)
        {
            if (snapshot?.enemies == null)
                return false;

            for (int i = 0; i < snapshot.enemies.Count; i++)
            {
                EnemySnapshot enemy = snapshot.enemies[i];
                if (enemy == null || enemy.enemyType != (int)EnemyType.Boss || string.IsNullOrWhiteSpace(enemy.id))
                    continue;

                FinalBossDuelRuntimeContext.AdoptOnlineChallenge(enemy.id, enemy.id);
                return true;
            }

            return false;
        }

        private void LogWorldSnapshotWarning(string message)
        {
            if (Time.unscaledTime < _nextWorldSnapshotWarningTime)
                return;

            _nextWorldSnapshotWarningTime = Time.unscaledTime + WorldSnapshotWarningIntervalSeconds;
            Debug.LogWarning($"[OnlineDungeonSessionCoordinator] {message}");
        }

        private OnlineDungeonPlayerPoseRequest BuildLocalPoseRequest(string currentUserId)
        {
            Transform playerTransform = GameStateMachine.GetInstance().LevelPlayerTransform;
            if (playerTransform == null)
                return null;

            PlayerController playerController = playerTransform.GetComponent<PlayerController>();
            PlayerAttackMode attackMode = playerController?.PlayerModel != null
                ? playerController.PlayerModel.CurrentAttackMode
                : PlayerAttackMode.Melee;
            Stats stats = playerController?.PlayerModel?.Stats;
            PlayerStateBase currentState = playerController?.StateMachine?.CurrentState;
            string actionId = currentState != null ? currentState.CurrentRuntimeActionId : string.Empty;
            string skillEffectId = ResolveLocalSkillEffectId(playerController, actionId);
            bool aimActive = TryResolveLocalAimDirection(playerController, playerTransform, out Vector3 aimDirection);
            bool aimLayerActive = ResolveLocalAimLayerActive(playerController);
            float aimPitch = ResolveLocalAimPitch(playerController);
            ResolveLocalLocomotionPresentation(playerController, playerTransform, out float moveSpeed, out Vector2 locomotionBlend);

            return new OnlineDungeonPlayerPoseRequest
            {
                userId = currentUserId,
                joinToken = _activeAidSession.dungeonJoinToken,
                x = playerTransform.position.x,
                y = playerTransform.position.y,
                z = playerTransform.position.z,
                yaw = playerTransform.eulerAngles.y,
                aimActive = aimActive,
                aimLayerActive = aimLayerActive,
                aimPitch = aimPitch,
                aimDirectionX = aimDirection.x,
                aimDirectionY = aimDirection.y,
                aimDirectionZ = aimDirection.z,
                moveSpeed = moveSpeed,
                moveX = locomotionBlend.x,
                moveY = locomotionBlend.y,
                action = (int)ResolveLocalRemoteAction(playerController, moveSpeed, aimActive),
                actionId = actionId,
                skillEffectId = skillEffectId,
                actionSequence = currentState != null ? currentState.EntrySequence : 0,
                actionElapsedSeconds = currentState != null ? Mathf.Max(0f, currentState.ElapsedSeconds) : 0f,
                actionNormalizedProgress = currentState != null ? currentState.NormalizedProgress : -1f,
                attackMode = (int)attackMode,
                hasMeleeWeapon = playerController?.PlayerModel?.GetEquippedWeapon(PlayerAttackMode.Melee) != null,
                hasRangedWeapon = playerController?.PlayerModel?.GetEquippedWeapon(PlayerAttackMode.Ranged) != null,
                defense = stats != null ? stats.Defense : 0f,
                damageReduce = stats != null ? stats.DamageReduce : 0f,
                currentHp = playerController?.PlayerModel != null ? playerController.PlayerModel.CurrentHp : 0f,
                maxHp = stats != null ? stats.MaxHp : 1f,
                isDead = playerController != null && playerController.IsDead,
            };
        }

        private void ApplyRemotePoses(List<OnlineDungeonPlayerPoseInfo> poses)
        {
            if (poses == null)
                return;

            for (int i = 0; i < poses.Count; i++)
            {
                OnlineDungeonPlayerPoseInfo pose = poses[i];
                if (pose == null || string.IsNullOrWhiteSpace(pose.userId))
                    continue;

                ApplyRemotePose(pose);
            }
        }

        private void ApplyRemotePose(OnlineDungeonPlayerPoseInfo pose)
        {
            if (!_remoteAvatars.TryGetValue(pose.userId, out SocialAidRemotePlayerAvatar avatar) || avatar == null)
            {
                avatar = CreateRemoteAvatar(pose.userId, new Vector3(pose.x, pose.y, pose.z), pose.yaw);
                if (avatar == null)
                    return;

                _remoteAvatars[pose.userId] = avatar;
            }

            float sampleTime = Time.unscaledTime;
            if (_realtimeClient != null && _realtimeClient.TryConvertServerUtcToLocalTime(pose.updatedAtUtc, out float serverSampleTime))
                sampleTime = serverSampleTime;

            avatar.ApplyPose(
                new Vector3(pose.x, pose.y, pose.z),
                pose.yaw,
                pose.aimActive,
                pose.aimLayerActive,
                pose.aimPitch,
                new Vector3(pose.aimDirectionX, pose.aimDirectionY, pose.aimDirectionZ),
                pose.moveSpeed,
                pose.moveX,
                pose.moveY,
                pose.actionId,
                (SocialAidRemotePlayerAvatar.RemoteAction)Mathf.Clamp(pose.action, 0, 255),
                (PlayerAttackMode)Mathf.Clamp(pose.attackMode, 0, 255),
                pose.hasMeleeWeapon,
                pose.hasRangedWeapon,
                sampleTime);
            avatar.ApplyActionPresentation(pose.actionId, pose.skillEffectId, pose.actionSequence, pose.actionElapsedSeconds, pose.actionNormalizedProgress, sampleTime);
            OnlineDungeonRemotePlayerTarget target = avatar.GetComponent<OnlineDungeonRemotePlayerTarget>();
            target?.ApplyStats(pose.defense, pose.damageReduce, pose.currentHp, pose.maxHp, pose.isDead);
        }

        private static string ResolveLocalSkillEffectId(PlayerController playerController, string actionId)
        {
            if (playerController == null || string.IsNullOrWhiteSpace(actionId))
                return string.Empty;

            SkillConfigDatabaseSO skillConfig = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            SkillConfigEntry entry = skillConfig != null ? skillConfig.GetEntryByActionId(actionId.Trim()) : null;
            if (entry == null || entry.IsPassiveSkill)
                return string.Empty;

            return playerController.PlayerModel != null
                ? playerController.PlayerModel.ResolveSkillEffectId(entry)
                : entry.GetResolvedSkillId();
        }

        private static bool TryResolveLocalAimDirection(
            PlayerController playerController,
            Transform playerTransform,
            out Vector3 aimDirection)
        {
            aimDirection = playerTransform != null ? playerTransform.forward : Vector3.forward;
            if (playerController == null)
                return false;

            if (playerController.IsAimModeActive
                && playerController.TryGetCurrentAimPose(out SkillAimPose aimPose)
                && aimPose.Direction.sqrMagnitude > 0.0001f)
            {
                aimDirection = aimPose.Direction.normalized;
                return true;
            }

            if (playerController.PlayerModel == null
                || playerController.PlayerModel.CurrentAttackMode != PlayerAttackMode.Ranged)
                return false;

            if (playerController.StateMachine?.CurrentState is ShootState ||
                playerController.StateMachine?.CurrentState is ShootChargeState)
            {
                aimDirection = playerTransform != null ? playerTransform.forward : Vector3.forward;
                return aimDirection.sqrMagnitude > 0.0001f;
            }

            return false;
        }

        private static bool ResolveLocalAimLayerActive(PlayerController playerController)
        {
            if (playerController == null || playerController.Anim == null)
                return false;

            return playerController.Anim.TryGetAimLayerActive(out bool active) && active;
        }

        private static float ResolveLocalAimPitch(PlayerController playerController)
        {
            if (playerController == null || playerController.Anim == null)
                return 0f;

            return playerController.Anim.TryGetCurrentAimPitch(out float aimPitch) ? aimPitch : 0f;
        }

        private static void ResolveLocalLocomotionPresentation(
            PlayerController playerController,
            Transform playerTransform,
            out float moveSpeed,
            out Vector2 locomotionBlend)
        {
            moveSpeed = 0f;
            locomotionBlend = Vector2.zero;
            if (playerController == null)
                return;

            if (TryResolveLocalKinematicLocomotion(playerController, playerTransform, out moveSpeed, out locomotionBlend))
                return;

            if (playerController.Anim != null
                && playerController.Anim.TryGetLocomotionPresentation(out float animatorSpeed, out Vector2 animatorBlend))
            {
                moveSpeed = Mathf.Clamp01(animatorSpeed);
                locomotionBlend = Vector2.ClampMagnitude(animatorBlend, 1f);
                return;
            }

            moveSpeed = ResolveLocalMoveSpeed(playerController);
            locomotionBlend = ResolveLocalLocomotionBlend(playerController, playerTransform);
        }

        private static Vector2 ResolveLocalLocomotionBlend(PlayerController playerController, Transform playerTransform)
        {
            if (playerController == null || playerTransform == null)
                return Vector2.zero;

            Vector2 moveInput = playerController.CurrentMoveInput;
            if (moveInput.sqrMagnitude <= 0.01f)
                return Vector2.zero;

            Vector3 moveDirection = playerController.GetMoveDirection(moveInput);
            if (moveDirection.sqrMagnitude <= 0.001f)
                return Vector2.zero;

            Vector3 localDirection = playerTransform.InverseTransformDirection(moveDirection.normalized);
            float blendScale = ResolveLocalMoveSpeed(playerController);
            return Vector2.ClampMagnitude(new Vector2(localDirection.x, localDirection.z) * blendScale, 1f);
        }

        private static bool TryResolveLocalKinematicLocomotion(
            PlayerController playerController,
            Transform playerTransform,
            out float moveSpeed,
            out Vector2 locomotionBlend)
        {
            moveSpeed = 0f;
            locomotionBlend = Vector2.zero;
            if (playerController == null || playerController.Mover == null || playerTransform == null)
                return false;

            Vector3 horizontalVelocity = playerController.Mover.Velocity;
            horizontalVelocity.y = 0f;
            if (horizontalVelocity.sqrMagnitude <= 0.0001f)
                return false;

            float runSpeed = Mathf.Max(0.1f, playerController.RunSpeed);
            moveSpeed = Mathf.Clamp01(horizontalVelocity.magnitude / runSpeed);
            Vector3 localDirection = playerTransform.InverseTransformDirection(horizontalVelocity.normalized);
            locomotionBlend = Vector2.ClampMagnitude(new Vector2(localDirection.x, localDirection.z) * moveSpeed, 1f);
            return moveSpeed > 0.01f;
        }

        private static SocialAidRemotePlayerAvatar CreateRemoteAvatar(string userId, Vector3 position, float yaw)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Player");
            if (prefab == null)
            {
                Debug.LogWarning("[OnlineDungeonSessionCoordinator] 未找到远端玩家外观预制体 Resources/Prefabs/Player，无法显示联机玩家。");
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f));
            instance.name = $"OnlineDungeonRemotePlayer_{userId}";
            StripRemoteGameplay(instance);
            EnsureRemotePlayerTarget(instance, userId);
            return instance.AddComponent<SocialAidRemotePlayerAvatar>();
        }

        private static void StripRemoteGameplay(GameObject instance)
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour is SocialAidRemotePlayerAvatar || behaviour is OnlineDungeonRemotePlayerTarget)
                    continue;

                behaviour.enabled = false;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            CharacterController characterController = instance.GetComponent<CharacterController>();
            if (characterController != null)
                characterController.enabled = false;

            Rigidbody rigidbody = instance.GetComponent<Rigidbody>();
            if (rigidbody != null)
                rigidbody.isKinematic = true;
        }

        private static void EnsureRemotePlayerTarget(GameObject instance, string userId)
        {
            OnlineDungeonRemotePlayerTarget target = instance.GetComponent<OnlineDungeonRemotePlayerTarget>();
            if (target == null)
                target = instance.AddComponent<OnlineDungeonRemotePlayerTarget>();

            target.Initialize(userId);

            CapsuleCollider collider = instance.GetComponent<CapsuleCollider>();
            if (collider == null)
                collider = instance.AddComponent<CapsuleCollider>();

            collider.enabled = true;
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 1f, 0f);
            collider.radius = 0.45f;
            collider.height = 2f;
        }

        private static float ResolveLocalMoveSpeed(PlayerController playerController)
        {
            if (playerController == null)
                return 0f;

            float runSpeed = Mathf.Max(0.1f, playerController.RunSpeed);
            float velocity = 0f;
            if (playerController.Mover != null)
            {
                Vector3 horizontalVelocity = playerController.Mover.Velocity;
                velocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
            }

            if (velocity <= 0.01f)
            {
                Vector2 moveInput = playerController.CurrentMoveInput;
                if (moveInput.sqrMagnitude <= 0.01f)
                    return 0f;

                velocity = playerController.WalkSpeed;
            }

            return Mathf.Clamp01(velocity / runSpeed);
        }

        private static SocialAidRemotePlayerAvatar.RemoteAction ResolveLocalRemoteAction(
            PlayerController playerController,
            float moveSpeed,
            bool aimActive)
        {
            if (playerController == null || playerController.StateMachine?.CurrentState == null)
                return SocialAidRemotePlayerAvatar.RemoteAction.Idle;

            PlayerStateBase state = playerController.StateMachine.CurrentState;
            if (state is MoveState)
                return SocialAidRemotePlayerAvatar.RemoteAction.Move;
            if (state is AimState)
                return SocialAidRemotePlayerAvatar.RemoteAction.Aim;
            if (state is JumpState)
                return SocialAidRemotePlayerAvatar.RemoteAction.Jump;
            if (state is AirJumpState)
                return SocialAidRemotePlayerAvatar.RemoteAction.AirJump;
            if (state is FallState)
                return SocialAidRemotePlayerAvatar.RemoteAction.Fall;
            if (state is LandState)
                return SocialAidRemotePlayerAvatar.RemoteAction.Land;
            if (state is DodgeState)
                return SocialAidRemotePlayerAvatar.RemoteAction.Dodge;
            if (state is Attack0State)
                return SocialAidRemotePlayerAvatar.RemoteAction.Attack0;
            if (state is Attack1State)
                return SocialAidRemotePlayerAvatar.RemoteAction.Attack1;
            if (state is Attack2State)
                return SocialAidRemotePlayerAvatar.RemoteAction.Attack2;
            if (state is Attack3State)
                return SocialAidRemotePlayerAvatar.RemoteAction.Attack3;
            if (state is AirAttackState)
                return SocialAidRemotePlayerAvatar.RemoteAction.AirAttack;
            if (state is FallAttackStartState)
                return SocialAidRemotePlayerAvatar.RemoteAction.FallAttackStart;
            if (state is FallAttackLoopState)
                return SocialAidRemotePlayerAvatar.RemoteAction.FallAttackLoop;
            if (state is FallAttackLandState)
                return SocialAidRemotePlayerAvatar.RemoteAction.FallAttackLand;
            if (state is ChargeStartState)
                return SocialAidRemotePlayerAvatar.RemoteAction.ChargeStart;
            if (state is ChargeLoopState)
                return SocialAidRemotePlayerAvatar.RemoteAction.ChargeLoop;
            if (state is ChargeReleaseState)
                return SocialAidRemotePlayerAvatar.RemoteAction.ChargeRelease;
            if (state is ShootState)
                return SocialAidRemotePlayerAvatar.RemoteAction.Shoot;
            if (state is ShootChargeState)
                return SocialAidRemotePlayerAvatar.RemoteAction.ShootCharge;
            if (state is SkillStateBase)
                return SocialAidRemotePlayerAvatar.RemoteAction.Skill;
            if (state is HitStunState)
                return SocialAidRemotePlayerAvatar.RemoteAction.HitStun;
            if (state is PlayerDeathState)
                return SocialAidRemotePlayerAvatar.RemoteAction.Dead;

            if (moveSpeed > 0.05f)
                return aimActive ? SocialAidRemotePlayerAvatar.RemoteAction.Aim : SocialAidRemotePlayerAvatar.RemoteAction.Move;

            return SocialAidRemotePlayerAvatar.RemoteAction.Idle;
        }

        private void PruneRemoteAvatars()
        {
            List<string> expired = null;
            foreach (KeyValuePair<string, SocialAidRemotePlayerAvatar> pair in _remoteAvatars)
            {
                if (pair.Value == null || Time.unscaledTime - pair.Value.LastPoseTime > RemoteAvatarTimeoutSeconds)
                {
                    expired ??= new List<string>();
                    expired.Add(pair.Key);
                }
            }

            if (expired == null)
                return;

            for (int i = 0; i < expired.Count; i++)
                RemoveRemoteAvatar(expired[i]);
        }

        private void RemoveRemoteAvatar(string userId)
        {
            if (!_remoteAvatars.TryGetValue(userId, out SocialAidRemotePlayerAvatar avatar))
                return;

            _remoteAvatars.Remove(userId);
            if (avatar != null)
                UnityEngine.Object.Destroy(avatar.gameObject);
        }

        private void ClearRemoteAvatars()
        {
            foreach (KeyValuePair<string, SocialAidRemotePlayerAvatar> pair in _remoteAvatars)
            {
                if (pair.Value != null)
                    UnityEngine.Object.Destroy(pair.Value.gameObject);
            }

            _remoteAvatars.Clear();
        }

        private void TryEnterTargetLevel(string currentUserId)
        {
            if (_activeAidSession == null)
                return;

            if (!string.Equals(currentUserId, _activeAidSession.helperUserId, StringComparison.Ordinal))
            {
                Debug.Log("[OnlineDungeonSessionCoordinator] 被援助方保持当前关卡，等待其他玩家进入副本。");
                return;
            }

            CaptureReturnPoint();
            _pendingSpawnPosition = new Vector3(_activeAidSession.hostPlayerX, _activeAidSession.hostPlayerY, _activeAidSession.hostPlayerZ);
            _pendingSpawnYaw = _activeAidSession.hostPlayerYaw;

            int targetLevelIndex = Mathf.Max(1, _activeAidSession.hostLevelIndex);
            string sceneName = GameStateMachine.GetLevelSceneName(targetLevelIndex);
            AdoptTargetLevelRuntimeContext(targetLevelIndex);
            Debug.Log($"[OnlineDungeonSessionCoordinator] 援助方进入联机副本关卡：{sceneName}");
            LoadSceneWithProgress(sceneName);
        }

        private void CaptureReturnPoint()
        {
            GameStateMachine gsm = GameStateMachine.GetInstance();
            RunData run = gsm.CurrentRun;
            if (run == null)
                return;

            UnityEngine.Object.FindFirstObjectByType<LevelBootstrapper>()?.CaptureRuntimeSnapshot();
            _returnLevelIndex = Mathf.Max(1, run.levelIndex);
            _returnSceneName = SceneManager.GetActiveScene().name;
            _returnBossId = FinalBossDuelSceneRuntime.IsFinalBossDuelSceneName(_returnSceneName)
                ? FinalBossDuelRuntimeContext.CurrentBossId
                : string.Empty;
            _returnBossDisplayName = FinalBossDuelSceneRuntime.IsFinalBossDuelSceneName(_returnSceneName)
                ? FinalBossDuelRuntimeContext.CurrentBossDisplayName
                : string.Empty;
            _returnSnapshot = CloneLevelSnapshot(run.levelSnapshot);
        }

        private static void AdoptTargetLevelRuntimeContext(int targetLevelIndex)
        {
            RunData run = GameStateMachine.GetInstance().CurrentRun;
            if (run == null)
                return;

            run.levelIndex = Mathf.Max(1, targetLevelIndex);
            run.levelSnapshot = null;
        }

        private static LevelSnapshot CloneLevelSnapshot(LevelSnapshot snapshot)
        {
            if (snapshot == null)
                return null;

            RunData wrapper = new RunData
            {
                levelSnapshot = snapshot,
            };
            return SaveSystem.CloneRunData(wrapper)?.levelSnapshot;
        }

        private static void LoadSceneWithProgress(string sceneName, Action onSceneLoaded = null)
        {
            UIManager ui = UIManager.GetInstance();
            if (ui == null)
            {
                ScenesMgr.GetInstance().LoadSceneAsyn(sceneName, () => onSceneLoaded?.Invoke());
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

        private static void LoadSceneWithProgressWaitingForOnlineRelease(string sceneName, Func<bool> canActivate, Action readyToActivate, Action onSceneLoaded = null)
        {
            UIManager ui = UIManager.GetInstance();
            UnityEngine.Events.UnityAction readyAction = () => readyToActivate?.Invoke();
            if (ui == null)
            {
                ScenesMgr.GetInstance().LoadSceneAsynWaitingForActivation(sceneName, canActivate, readyAction, () => onSceneLoaded?.Invoke());
                return;
            }

            MainMenuBackgroundPanel.RequestLoadingTransitionShow();
            ui.ShowPanel<MainMenuBackgroundPanel>(PanelNames.MainMenuBackground, PanelLayers.MainMenuBackground);
            ui.ShowPanel<LoadingPanel>(PanelNames.Loading, PanelLayers.Loading, _ =>
            {
                ScenesMgr.GetInstance().LoadSceneAsynWaitingForActivation(sceneName, canActivate, readyAction, () =>
                {
                    ui.HidePanel(PanelNames.Loading);
                    ui.HidePanel(PanelNames.MainMenuBackground);
                    onSceneLoaded?.Invoke();
                });
            });
        }
    }
}
