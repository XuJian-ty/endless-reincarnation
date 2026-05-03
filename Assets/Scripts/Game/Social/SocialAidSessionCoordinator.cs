using System;
using System.Collections;
using System.Collections.Generic;
using Game.Data;
using Game.GameFlow;
using Game.Presentation;
using Game.Saving;
using Game.UI;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using ProjectBase;

namespace Game.Social
{
    /// <summary>
    /// 援助联机会话协调器：
    /// 1. 根据援助会话信息决定本机是 Host 还是 Client
    /// 2. 在本机环回地址上启动最小 NGO 会话
    /// 3. 为后续“接受援助后进入同一关卡”提供统一入口
    /// </summary>
    public class SocialAidSessionCoordinator : BaseManager<SocialAidSessionCoordinator>
    {
        private const string DefaultHostAddress = "127.0.0.1";
        private const ushort DefaultHostPort = 7777;
        private const int MaxClientConnectAttempts = 12;
        private const float ClientConnectRetryDelaySeconds = 1f;
        private const string AidPoseMessageName = "SocialAidPose";
        private const string AidGameplayPanelMessageName = "SocialAidGameplayPanel";
        private const string AidSessionEndMessageName = "SocialAidSessionEnd";
        private const float PoseSendIntervalSeconds = 0.08f;
        private const float RemoteAvatarTimeoutSeconds = 2f;
        private const float ReliableEndMessageDelaySeconds = 0.25f;

        private enum AidSessionEndReason : byte
        {
            HelperDied = 1,
            HostDied = 2,
        }

        private NetworkManager _networkManager;
        private SocialAidSessionInfo _activeSession;
        private bool _isHostRole;
        private bool _isSceneTransitionPending;
        private Vector3? _pendingHelperSpawnPosition;
        private float _pendingHelperSpawnYaw;
        private int _helperReturnLevelIndex;
        private LevelSnapshot _helperReturnSnapshot;
        private int _clientConnectAttempts;
        private Coroutine _clientConnectRetryCoroutine;
        private Coroutine _poseSyncCoroutine;
        private Coroutine _delayedEndSessionCoroutine;
        private readonly Dictionary<ulong, SocialAidRemotePlayerAvatar> _remoteAvatars = new Dictionary<ulong, SocialAidRemotePlayerAvatar>();

        public SocialAidSessionInfo ActiveSession => _activeSession;
        public bool HasActiveSession => _activeSession != null && !string.IsNullOrWhiteSpace(_activeSession.sessionId);
        public bool IsHostRole => _isHostRole;
        public bool IsInNetworkPlayMode => HasActiveSession && _networkManager != null && _networkManager.IsListening;
        public int TargetLevelIndex => _activeSession != null ? Mathf.Max(1, _activeSession.hostLevelIndex) : 1;

        public bool TryStartSession(SocialAidSessionInfo sessionInfo, out string error)
        {
            error = string.Empty;
            if (sessionInfo == null || string.IsNullOrWhiteSpace(sessionInfo.sessionId))
            {
                error = "援助会话信息无效";
                return false;
            }

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId))
            {
                error = "当前未登录账号";
                return false;
            }

            EnsureNetworkManager();
            if (_networkManager == null)
            {
                error = "未能创建联机管理器";
                return false;
            }

            StopSession();

            _activeSession = sessionInfo;
            _isHostRole = string.Equals(currentUser.userId, sessionInfo.hostUserId, StringComparison.Ordinal);

            if (!TryConfigureTransport(out error))
                return false;

            RegisterNetworkCallbacks();
            RegisterPoseMessageHandler();
            RegisterGameplayPanelMessageHandler();
            RegisterSessionEndMessageHandler();

            _clientConnectAttempts = 0;
            _isSceneTransitionPending = true;
            bool started = _isHostRole
                ? _networkManager.StartHost()
                : TryStartClientAttempt(out error);
            if (!started)
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = _isHostRole ? "启动主机失败" : "启动客户端失败";
                _activeSession = null;
                _isHostRole = false;
                _isSceneTransitionPending = false;
                return false;
            }

            Debug.Log($"[SocialAidSessionCoordinator] 援助联机会话已启动。Role={(_isHostRole ? "Host" : "Client")} Session={sessionInfo.sessionId}");
            RegisterPoseMessageHandler();
            RegisterGameplayPanelMessageHandler();
            RegisterSessionEndMessageHandler();
            StartPoseSync();
            return true;
        }

        public void StopSession()
        {
            if (_delayedEndSessionCoroutine != null)
            {
                MonoMgr.GetInstance().StopCoroutine(_delayedEndSessionCoroutine);
                _delayedEndSessionCoroutine = null;
            }

            StopPoseSync();
            UnregisterPoseMessageHandler();
            UnregisterGameplayPanelMessageHandler();
            UnregisterSessionEndMessageHandler();
            ClearRemoteAvatars();
            StopClientConnectRetry();
            UnregisterNetworkCallbacks();
            if (_networkManager != null && _networkManager.IsListening)
                _networkManager.Shutdown();

            _activeSession = null;
            _isHostRole = false;
            _isSceneTransitionPending = false;
            _pendingHelperSpawnPosition = null;
            _helperReturnLevelIndex = 0;
            _helperReturnSnapshot = null;
            _clientConnectAttempts = 0;
        }

        public bool TryConsumeHelperSpawnOverride(out Vector3 position, out Quaternion rotation)
        {
            if (!_pendingHelperSpawnPosition.HasValue)
            {
                position = default;
                rotation = default;
                return false;
            }

            position = _pendingHelperSpawnPosition.Value;
            rotation = Quaternion.Euler(0f, _pendingHelperSpawnYaw, 0f);
            _pendingHelperSpawnPosition = null;
            return true;
        }

        public bool ShouldSkipLocalSnapshotRestore()
        {
            return HasActiveSession && !_isHostRole;
        }

        public void ReturnHelperToOwnLevel()
        {
            ReturnHelperToOwnLevel(true);
        }

        public void ReturnHelperToOwnLevel(bool closeServerSession)
        {
            if (!HasActiveSession)
                return;

            SocialAidSessionInfo session = _activeSession;
            int helperReturnLevelIndex = _helperReturnLevelIndex;
            LevelSnapshot helperReturnSnapshot = _helperReturnSnapshot;
            StopSession();
            if (closeServerSession)
            {
                SocialService.GetInstance().CloseAidSession(
                    session.sessionId,
                    (_, _) => { },
                    error => Debug.LogWarning($"[SocialAidSessionCoordinator] 关闭援助会话失败：{error}"));
            }

            GameStateMachine.GetInstance().ReturnCurrentSaveToOwnLevel(helperReturnLevelIndex, helperReturnSnapshot);
        }

        public bool TryHandleLocalPlayerDeath()
        {
            if (!HasActiveSession)
                return false;

            if (!_isHostRole)
            {
                NotifyAidSessionEnded(AidSessionEndReason.HelperDied);
                ScheduleHelperReturnAfterEndNotification(true);
                return true;
            }

            NotifyAidSessionEnded(AidSessionEndReason.HostDied);
            ScheduleHostSessionStopAfterEndNotification(true);
            return false;
        }

        private void EnsureNetworkManager()
        {
            if (_networkManager != null)
                return;

            _networkManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            if (_networkManager != null)
                return;

            GameObject root = new GameObject("AidNetworkManager");
            UnityEngine.Object.DontDestroyOnLoad(root);
            _networkManager = root.AddComponent<NetworkManager>();
            UnityTransport transport = root.AddComponent<UnityTransport>();
            _networkManager.NetworkConfig = new NetworkConfig();
            _networkManager.NetworkConfig.NetworkTransport = transport;
        }

        private bool TryConfigureTransport(out string error)
        {
            error = string.Empty;
            if (_networkManager == null)
            {
                error = "联机管理器不存在";
                return false;
            }

            UnityTransport transport = _networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                error = "UnityTransport 不存在";
                return false;
            }

            transport.SetConnectionData(DefaultHostAddress, DefaultHostPort);
            return true;
        }

        private void RegisterNetworkCallbacks()
        {
            if (_networkManager == null)
                return;

            _networkManager.OnServerStarted -= HandleServerStarted;
            _networkManager.OnServerStarted += HandleServerStarted;
            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
            _networkManager.OnClientConnectedCallback += HandleClientConnected;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void RegisterPoseMessageHandler()
        {
            if (_networkManager?.CustomMessagingManager == null)
                return;

            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(AidPoseMessageName);
            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(AidPoseMessageName, HandlePoseMessage);
        }

        private void UnregisterPoseMessageHandler()
        {
            if (_networkManager?.CustomMessagingManager == null)
                return;

            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(AidPoseMessageName);
        }

        private void RegisterGameplayPanelMessageHandler()
        {
            if (_networkManager?.CustomMessagingManager == null)
                return;

            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(AidGameplayPanelMessageName);
            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(AidGameplayPanelMessageName, HandleGameplayPanelMessage);
        }

        private void UnregisterGameplayPanelMessageHandler()
        {
            if (_networkManager?.CustomMessagingManager == null)
                return;

            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(AidGameplayPanelMessageName);
        }

        private void RegisterSessionEndMessageHandler()
        {
            if (_networkManager?.CustomMessagingManager == null)
                return;

            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(AidSessionEndMessageName);
            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(AidSessionEndMessageName, HandleSessionEndMessage);
        }

        private void UnregisterSessionEndMessageHandler()
        {
            if (_networkManager?.CustomMessagingManager == null)
                return;

            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(AidSessionEndMessageName);
        }

        private void UnregisterNetworkCallbacks()
        {
            if (_networkManager == null)
                return;

            _networkManager.OnServerStarted -= HandleServerStarted;
            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        private void HandleServerStarted()
        {
            TryEnterTargetLevel();
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (_networkManager == null || clientId != _networkManager.LocalClientId)
                return;

            StopClientConnectRetry();
            TryEnterTargetLevel();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (_networkManager == null)
                return;

            if (clientId != _networkManager.LocalClientId)
            {
                RemoveRemoteAvatar(clientId);
                return;
            }

            if (!_isHostRole && _activeSession != null && _isSceneTransitionPending && _clientConnectAttempts < MaxClientConnectAttempts)
            {
                StartClientConnectRetry();
                return;
            }

            Debug.LogWarning("[SocialAidSessionCoordinator] 联机会话已断开。");
            _isSceneTransitionPending = false;
            RemoveRemoteAvatar(clientId);
        }

        private void StartPoseSync()
        {
            if (_poseSyncCoroutine != null)
                return;

            _poseSyncCoroutine = MonoMgr.GetInstance().StartCoroutine(SyncLocalPoseLoop());
        }

        private void StopPoseSync()
        {
            if (_poseSyncCoroutine == null)
                return;

            MonoMgr.GetInstance().StopCoroutine(_poseSyncCoroutine);
            _poseSyncCoroutine = null;
        }

        private IEnumerator SyncLocalPoseLoop()
        {
            while (HasActiveSession)
            {
                if (_networkManager != null &&
                    _networkManager.IsListening &&
                    (_networkManager.IsServer || _networkManager.IsConnectedClient) &&
                    _networkManager.CustomMessagingManager != null)
                {
                    SendLocalPose();
                    PruneRemoteAvatars();
                }

                yield return new WaitForSecondsRealtime(PoseSendIntervalSeconds);
            }

            _poseSyncCoroutine = null;
        }

        private void SendLocalPose()
        {
            Transform playerTransform = GameStateMachine.GetInstance().LevelPlayerTransform;
            if (playerTransform == null)
                return;

            ulong ownerClientId = _networkManager.LocalClientId;
            Vector3 position = playerTransform.position;
            float yaw = playerTransform.eulerAngles.y;
            PlayerController playerController = playerTransform.GetComponent<PlayerController>();
            float moveSpeed = ResolveLocalMoveSpeed(playerController);
            SocialAidRemotePlayerAvatar.RemoteAction action = ResolveLocalRemoteAction(playerController);
            PlayerAttackMode attackMode = playerController?.PlayerModel != null ? playerController.PlayerModel.CurrentAttackMode : PlayerAttackMode.Melee;
            bool hasMeleeWeapon = playerController?.PlayerModel?.GetEquippedWeapon(PlayerAttackMode.Melee) != null;
            bool hasRangedWeapon = playerController?.PlayerModel?.GetEquippedWeapon(PlayerAttackMode.Ranged) != null;

            if (_networkManager.IsServer)
            {
                IReadOnlyList<ulong> clients = _networkManager.ConnectedClientsIds;
                for (int i = 0; i < clients.Count; i++)
                {
                    ulong clientId = clients[i];
                    if (clientId != ownerClientId)
                        SendPoseMessage(clientId, ownerClientId, position, yaw, moveSpeed, action, attackMode, hasMeleeWeapon, hasRangedWeapon);
                }
            }
            else
            {
                SendPoseMessage(NetworkManager.ServerClientId, ownerClientId, position, yaw, moveSpeed, action, attackMode, hasMeleeWeapon, hasRangedWeapon);
            }
        }

        private void HandlePoseMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!reader.TryBeginRead(
                    FastBufferWriter.GetWriteSize<ulong>() +
                    FastBufferWriter.GetWriteSize<Vector3>() +
                    FastBufferWriter.GetWriteSize<float>() +
                    FastBufferWriter.GetWriteSize<float>() +
                    FastBufferWriter.GetWriteSize<byte>() +
                    FastBufferWriter.GetWriteSize<byte>() +
                    FastBufferWriter.GetWriteSize<bool>() +
                    FastBufferWriter.GetWriteSize<bool>()))
                return;

            reader.ReadValueSafe(out ulong ownerClientId);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out float yaw);
            reader.ReadValueSafe(out float moveSpeed);
            reader.ReadValueSafe(out byte actionValue);
            reader.ReadValueSafe(out byte attackModeValue);
            reader.ReadValueSafe(out bool hasMeleeWeapon);
            reader.ReadValueSafe(out bool hasRangedWeapon);

            if (ownerClientId == _networkManager.LocalClientId)
                return;

            var action = (SocialAidRemotePlayerAvatar.RemoteAction)actionValue;
            var attackMode = (PlayerAttackMode)attackModeValue;
            ApplyRemotePose(ownerClientId, position, yaw, moveSpeed, action, attackMode, hasMeleeWeapon, hasRangedWeapon);

            if (!_networkManager.IsServer)
                return;

            IReadOnlyList<ulong> clients = _networkManager.ConnectedClientsIds;
            for (int i = 0; i < clients.Count; i++)
            {
                ulong clientId = clients[i];
                if (clientId != ownerClientId && clientId != _networkManager.LocalClientId)
                    SendPoseMessage(clientId, ownerClientId, position, yaw, moveSpeed, action, attackMode, hasMeleeWeapon, hasRangedWeapon);
            }
        }

        private void SendPoseMessage(
            ulong targetClientId,
            ulong ownerClientId,
            Vector3 position,
            float yaw,
            float moveSpeed,
            SocialAidRemotePlayerAvatar.RemoteAction action,
            PlayerAttackMode attackMode,
            bool hasMeleeWeapon,
            bool hasRangedWeapon)
        {
            using FastBufferWriter writer = new FastBufferWriter(96, Allocator.Temp);
            writer.WriteValueSafe(ownerClientId);
            writer.WriteValueSafe(position);
            writer.WriteValueSafe(yaw);
            writer.WriteValueSafe(moveSpeed);
            writer.WriteValueSafe((byte)action);
            writer.WriteValueSafe((byte)attackMode);
            writer.WriteValueSafe(hasMeleeWeapon);
            writer.WriteValueSafe(hasRangedWeapon);
            _networkManager.CustomMessagingManager.SendNamedMessage(AidPoseMessageName, targetClientId, writer, NetworkDelivery.UnreliableSequenced);
        }

        private static float ResolveLocalMoveSpeed(PlayerController playerController)
        {
            if (playerController == null)
                return 0f;

            Vector2 moveInput = playerController.CurrentMoveInput;
            if (moveInput.sqrMagnitude <= 0.01f)
                return 0f;

            float runSpeed = Mathf.Max(0.1f, playerController.RunSpeed);
            float velocity = 0f;
            if (playerController.Mover != null)
            {
                Vector3 horizontalVelocity = playerController.Mover.Velocity;
                velocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
            }

            if (velocity <= 0.01f)
                velocity = playerController.WalkSpeed;

            return Mathf.Clamp01(velocity / runSpeed);
        }

        private static SocialAidRemotePlayerAvatar.RemoteAction ResolveLocalRemoteAction(PlayerController playerController)
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

            return SocialAidRemotePlayerAvatar.RemoteAction.Idle;
        }

        public void BroadcastGameplayPanelState(string panelName, bool open)
        {
            if (string.IsNullOrWhiteSpace(panelName) ||
                _networkManager == null ||
                !_networkManager.IsListening ||
                !(_networkManager.IsServer || _networkManager.IsConnectedClient) ||
                _networkManager.CustomMessagingManager == null)
            {
                return;
            }

            ulong ownerClientId = _networkManager.LocalClientId;
            if (_networkManager.IsServer)
            {
                IReadOnlyList<ulong> clients = _networkManager.ConnectedClientsIds;
                for (int i = 0; i < clients.Count; i++)
                {
                    ulong clientId = clients[i];
                    if (clientId != ownerClientId)
                        SendGameplayPanelMessage(clientId, ownerClientId, panelName, open);
                }
            }
            else
            {
                SendGameplayPanelMessage(NetworkManager.ServerClientId, ownerClientId, panelName, open);
            }
        }

        private void HandleGameplayPanelMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong ownerClientId);
            reader.ReadValueSafe(out string panelName);
            reader.ReadValueSafe(out bool open);

            if (_networkManager == null || ownerClientId == _networkManager.LocalClientId)
                return;

            if (!GameplayUIInputBridge.ApplyNetworkPanelState(panelName, open))
                Debug.LogWarning($"[SocialAidSessionCoordinator] 收到未知的援助面板同步请求：{panelName}");

            if (!_networkManager.IsServer)
                return;

            IReadOnlyList<ulong> clients = _networkManager.ConnectedClientsIds;
            for (int i = 0; i < clients.Count; i++)
            {
                ulong clientId = clients[i];
                if (clientId != ownerClientId && clientId != _networkManager.LocalClientId)
                    SendGameplayPanelMessage(clientId, ownerClientId, panelName, open);
            }
        }

        private void SendGameplayPanelMessage(ulong targetClientId, ulong ownerClientId, string panelName, bool open)
        {
            int bufferSize = FastBufferWriter.GetWriteSize<ulong>() +
                             FastBufferWriter.GetWriteSize(panelName) +
                             FastBufferWriter.GetWriteSize<bool>();
            using FastBufferWriter writer = new FastBufferWriter(bufferSize, Allocator.Temp);
            writer.WriteValueSafe(ownerClientId);
            writer.WriteValueSafe(panelName);
            writer.WriteValueSafe(open);
            _networkManager.CustomMessagingManager.SendNamedMessage(AidGameplayPanelMessageName, targetClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        private void NotifyAidSessionEnded(AidSessionEndReason reason)
        {
            if (_networkManager == null ||
                !_networkManager.IsListening ||
                !(_networkManager.IsServer || _networkManager.IsConnectedClient) ||
                _networkManager.CustomMessagingManager == null)
            {
                return;
            }

            ulong ownerClientId = _networkManager.LocalClientId;
            if (_networkManager.IsServer)
            {
                IReadOnlyList<ulong> clients = _networkManager.ConnectedClientsIds;
                for (int i = 0; i < clients.Count; i++)
                {
                    ulong clientId = clients[i];
                    if (clientId != ownerClientId)
                        SendSessionEndMessage(clientId, ownerClientId, reason);
                }
            }
            else
            {
                SendSessionEndMessage(NetworkManager.ServerClientId, ownerClientId, reason);
            }
        }

        private void HandleSessionEndMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!reader.TryBeginRead(
                    FastBufferWriter.GetWriteSize<ulong>() +
                    FastBufferWriter.GetWriteSize<byte>()))
                return;

            reader.ReadValueSafe(out ulong ownerClientId);
            reader.ReadValueSafe(out byte reasonValue);

            if (_networkManager == null || ownerClientId == _networkManager.LocalClientId)
                return;

            var reason = (AidSessionEndReason)reasonValue;
            if (!_isHostRole)
            {
                ScheduleHelperReturnAfterEndNotification(false);
                return;
            }

            if (reason == AidSessionEndReason.HelperDied)
                ScheduleHostSessionStopAfterEndNotification(false);

            if (!_networkManager.IsServer)
                return;

            IReadOnlyList<ulong> clients = _networkManager.ConnectedClientsIds;
            for (int i = 0; i < clients.Count; i++)
            {
                ulong clientId = clients[i];
                if (clientId != ownerClientId && clientId != _networkManager.LocalClientId)
                    SendSessionEndMessage(clientId, ownerClientId, reason);
            }
        }

        private void SendSessionEndMessage(ulong targetClientId, ulong ownerClientId, AidSessionEndReason reason)
        {
            using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(ownerClientId);
            writer.WriteValueSafe((byte)reason);
            _networkManager.CustomMessagingManager.SendNamedMessage(AidSessionEndMessageName, targetClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        private void ScheduleHelperReturnAfterEndNotification(bool closeServerSession)
        {
            if (_delayedEndSessionCoroutine != null)
                MonoMgr.GetInstance().StopCoroutine(_delayedEndSessionCoroutine);

            _delayedEndSessionCoroutine = MonoMgr.GetInstance().StartCoroutine(ReturnHelperAfterEndNotification(closeServerSession));
        }

        private IEnumerator ReturnHelperAfterEndNotification(bool closeServerSession)
        {
            yield return new WaitForSecondsRealtime(ReliableEndMessageDelaySeconds);
            _delayedEndSessionCoroutine = null;
            ReturnHelperToOwnLevel(closeServerSession);
        }

        private void ScheduleHostSessionStopAfterEndNotification(bool closeServerSession)
        {
            if (_delayedEndSessionCoroutine != null)
                MonoMgr.GetInstance().StopCoroutine(_delayedEndSessionCoroutine);

            _delayedEndSessionCoroutine = MonoMgr.GetInstance().StartCoroutine(StopHostSessionAfterEndNotification(closeServerSession));
        }

        private IEnumerator StopHostSessionAfterEndNotification(bool closeServerSession)
        {
            yield return new WaitForSecondsRealtime(ReliableEndMessageDelaySeconds);
            _delayedEndSessionCoroutine = null;
            StopHostSession(closeServerSession);
        }

        private void StopHostSession(bool closeServerSession)
        {
            if (!HasActiveSession)
                return;

            SocialAidSessionInfo session = _activeSession;
            StopSession();
            if (closeServerSession)
            {
                SocialService.GetInstance().CloseAidSession(
                    session.sessionId,
                    (_, _) => { },
                    error => Debug.LogWarning($"[SocialAidSessionCoordinator] 关闭援助会话失败：{error}"));
            }
        }

        private void ApplyRemotePose(
            ulong ownerClientId,
            Vector3 position,
            float yaw,
            float moveSpeed,
            SocialAidRemotePlayerAvatar.RemoteAction action,
            PlayerAttackMode attackMode,
            bool hasMeleeWeapon,
            bool hasRangedWeapon)
        {
            if (!_remoteAvatars.TryGetValue(ownerClientId, out SocialAidRemotePlayerAvatar avatar) || avatar == null)
            {
                avatar = CreateRemoteAvatar(ownerClientId, position, yaw);
                if (avatar == null)
                    return;

                _remoteAvatars[ownerClientId] = avatar;
            }

            avatar.ApplyPose(position, yaw, moveSpeed, action, attackMode, hasMeleeWeapon, hasRangedWeapon);
        }

        private SocialAidRemotePlayerAvatar CreateRemoteAvatar(ulong ownerClientId, Vector3 position, float yaw)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Player");
            if (prefab == null)
            {
                Debug.LogWarning("[SocialAidSessionCoordinator] 未找到远端玩家外观预制体 Resources/Prefabs/Player，无法显示援助玩家。");
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f));
            instance.name = $"AidRemotePlayer_{ownerClientId}";
            StripRemoteGameplay(instance);
            return instance.AddComponent<SocialAidRemotePlayerAvatar>();
        }

        private static void StripRemoteGameplay(GameObject instance)
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                behaviours[i].enabled = false;
                UnityEngine.Object.Destroy(behaviours[i]);
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

        private void PruneRemoteAvatars()
        {
            List<ulong> expired = null;
            foreach (KeyValuePair<ulong, SocialAidRemotePlayerAvatar> pair in _remoteAvatars)
            {
                if (pair.Value == null || Time.unscaledTime - pair.Value.LastPoseTime > RemoteAvatarTimeoutSeconds)
                {
                    expired ??= new List<ulong>();
                    expired.Add(pair.Key);
                }
            }

            if (expired == null)
                return;

            for (int i = 0; i < expired.Count; i++)
                RemoveRemoteAvatar(expired[i]);
        }

        private void RemoveRemoteAvatar(ulong ownerClientId)
        {
            if (!_remoteAvatars.TryGetValue(ownerClientId, out SocialAidRemotePlayerAvatar avatar))
                return;

            _remoteAvatars.Remove(ownerClientId);
            if (avatar != null)
                UnityEngine.Object.Destroy(avatar.gameObject);
        }

        private void ClearRemoteAvatars()
        {
            foreach (KeyValuePair<ulong, SocialAidRemotePlayerAvatar> pair in _remoteAvatars)
            {
                if (pair.Value != null)
                    UnityEngine.Object.Destroy(pair.Value.gameObject);
            }

            _remoteAvatars.Clear();
        }

        private bool TryStartClientAttempt(out string error)
        {
            error = string.Empty;
            if (_networkManager == null)
            {
                error = "联机管理器不存在";
                return false;
            }

            _clientConnectAttempts++;
            bool started = _networkManager.StartClient();
            if (!started)
                error = "启动客户端失败";
            return started;
        }

        private void StartClientConnectRetry()
        {
            if (_clientConnectRetryCoroutine != null)
                return;

            Debug.LogWarning($"[SocialAidSessionCoordinator] 连接主玩家失败，准备重试 {_clientConnectAttempts}/{MaxClientConnectAttempts}。");
            _clientConnectRetryCoroutine = MonoMgr.GetInstance().StartCoroutine(RetryClientConnect());
        }

        private void StopClientConnectRetry()
        {
            if (_clientConnectRetryCoroutine == null)
                return;

            MonoMgr.GetInstance().StopCoroutine(_clientConnectRetryCoroutine);
            _clientConnectRetryCoroutine = null;
        }

        private IEnumerator RetryClientConnect()
        {
            while (!_isHostRole && _activeSession != null && _isSceneTransitionPending && _clientConnectAttempts < MaxClientConnectAttempts)
            {
                yield return new WaitForSecondsRealtime(ClientConnectRetryDelaySeconds);

                if (_networkManager == null || _networkManager.IsListening)
                    continue;

                if (TryStartClientAttempt(out string error))
                {
                    _clientConnectRetryCoroutine = null;
                    yield break;
                }

                if (!string.IsNullOrWhiteSpace(error))
                    Debug.LogWarning($"[SocialAidSessionCoordinator] 重试连接主玩家失败：{error}");
            }

            _clientConnectRetryCoroutine = null;
            if (!_isHostRole && _activeSession != null && _isSceneTransitionPending)
            {
                Debug.LogWarning("[SocialAidSessionCoordinator] 多次连接主玩家失败，援助联机会话未能建立。");
                _isSceneTransitionPending = false;
            }
        }

        private void TryEnterTargetLevel()
        {
            if (!_isSceneTransitionPending || _activeSession == null)
                return;

            _isSceneTransitionPending = false;
            int targetLevelIndex = Mathf.Max(1, _activeSession.hostLevelIndex);
            string sceneName = GameStateMachine.GetLevelSceneName(targetLevelIndex);
            if (_isHostRole)
            {
                Debug.Log("[SocialAidSessionCoordinator] 主玩家作为求援方保持当前关卡，等待援助玩家加入。");
                return;
            }

            CaptureHelperReturnPoint();
            _pendingHelperSpawnPosition = new Vector3(_activeSession.hostPlayerX, _activeSession.hostPlayerY, _activeSession.hostPlayerZ);
            _pendingHelperSpawnYaw = _activeSession.hostPlayerYaw;

            LoadAidSceneWithProgress(sceneName);
        }

        private void CaptureHelperReturnPoint()
        {
            GameStateMachine gsm = GameStateMachine.GetInstance();
            RunData run = gsm.CurrentRun;
            if (run == null)
                return;

            UnityEngine.Object.FindFirstObjectByType<LevelBootstrapper>()?.CaptureRuntimeSnapshot();
            _helperReturnLevelIndex = Mathf.Max(1, run.levelIndex);
            _helperReturnSnapshot = CloneLevelSnapshot(run.levelSnapshot);
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

        private static void LoadAidSceneWithProgress(string sceneName)
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
