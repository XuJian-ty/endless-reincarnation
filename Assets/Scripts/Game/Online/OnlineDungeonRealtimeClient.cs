using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using kcp2k;
using UnityEngine;

namespace Game.Online
{
    public sealed class OnlineDungeonRealtimeClient : IDisposable
    {
        private const int ProtocolVersion = 3;
        private const int ReceiveBufferBytes = 64 * 1024;
        private const float HelloRetryIntervalSeconds = 1f;
        private const float HelloTimeoutSeconds = 8f;
        private const float PlayerStateSendIntervalSeconds = 0.05f;
        private const float EnemyAuthorityStateSendIntervalSeconds = 0.1f;
        private const float AuthorityPollIntervalSeconds = 0.1f;
        private const float UiPanelPollIntervalSeconds = 0.1f;
        private const float SceneLoadPollIntervalSeconds = 0.1f;
        private const float DamagePollIntervalSeconds = 0.05f;
        private const int MaxDamageEventsPerPoll = 4;
        private const float RewardPollIntervalSeconds = 0.25f;
        private const float OnlineEnemyFreezeDebugIntervalSeconds = 1.5f;
        private const int MaxPendingReliableEvents = 256;
        private const int MaxPendingKcpSends = 128;
        private const float ReliableEventResendIntervalSeconds = 0.15f;
        private const string ExpectedStateTransport = "udp-unreliable-snapshot";
        private const string ExpectedEventTransport = "kcp-reliable-event";
        private const string ExpectedSynchronizationMode = "state-sync-snapshot-interpolation";
        private const string ExpectedAuthoritySchema = "enemy-authority-v3-movement-skill-chest-reward-scene-load";
        private const string ExpectedSessionLifecycleMode = "server-authoritative-session-close";
        private const string ExpectedStateChannel = "state";
        private const string ExpectedEventChannel = "event";
        private static readonly DateTime UnixEpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private IRealtimeTransport _stateTransport;
        private IRealtimeTransport _eventTransport;
        private IPEndPoint _remoteEndPoint;
        private string _instanceId = string.Empty;
        private string _userId = string.Empty;
        private string _joinToken = string.Empty;
        private long _sequence;
        private float _nextHelloSendTime;
        private float _helloStartedTime;
        private float _nextPlayerStateSendTime;
        private float _nextEnemyAuthorityStateSendTime;
        private float _nextAuthorityPollTime;
        private float _nextUiPanelPollTime;
        private float _nextSceneLoadPollTime;
        private float _nextDamagePollTime;
        private float _nextRewardPollTime;
        private int _lastPlayerStateSendFrame = -1;
        private int _lastEnemyAuthorityStateSendFrame = -1;
        private int _lastAuthorityPollFrame = -1;
        private float _nextAuthorityPollDebugTime;
        private float _nextAuthorityReceiveDebugTime;
        private bool _hasServerClockOffset;
        private double _serverUtcToLocalTimeOffsetSeconds;
        private readonly ReliableEventQueue<OnlineDungeonUiPanelEventRequest> _pendingUiPanelEvents = new ReliableEventQueue<OnlineDungeonUiPanelEventRequest>(MaxPendingReliableEvents, "面板同步");
        private readonly ReliableEventQueue<OnlineDungeonSceneLoadEventRequest> _pendingSceneLoadEvents = new ReliableEventQueue<OnlineDungeonSceneLoadEventRequest>(MaxPendingReliableEvents, "场景加载同步");
        private readonly ReliableEventQueue<OnlineDungeonDamageEventRequest> _pendingDamageEvents = new ReliableEventQueue<OnlineDungeonDamageEventRequest>(MaxPendingReliableEvents, "伤害同步");
        private readonly ReliableEventQueue<OnlineDungeonRealtimeRewardSyncPayload> _pendingRewardActions = new ReliableEventQueue<OnlineDungeonRealtimeRewardSyncPayload>(MaxPendingReliableEvents, "奖励同步");

        public bool IsRunning { get; private set; }
        public bool IsConnected { get; private set; }

        public void Start(
            string dungeonServerUrl,
            string instanceId,
            string userId,
            string joinToken,
            int udpPort,
            int kcpPort,
            Action<string> onError)
        {
            Stop();

            if (udpPort <= 0 || kcpPort <= 0)
            {
                onError?.Invoke("联机副本实时通信端口未配置");
                return;
            }

            if (!TryResolveRemoteEndPoint(dungeonServerUrl, udpPort, out _remoteEndPoint, out string error))
            {
                onError?.Invoke(error);
                return;
            }

            if (!TryResolveRemoteEndPoint(dungeonServerUrl, kcpPort, out IPEndPoint eventEndPoint, out error))
            {
                onError?.Invoke(error);
                return;
            }

            _instanceId = instanceId ?? string.Empty;
            _userId = userId ?? string.Empty;
            _joinToken = joinToken ?? string.Empty;
            try
            {
                _stateTransport = new UdpRealtimeTransport(_remoteEndPoint);
                _eventTransport = new KcpRealtimeTransport(eventEndPoint);
            }
            catch (Exception ex)
            {
                DisposeTransports();
                onError?.Invoke(ex.Message);
                return;
            }
            IsRunning = true;
            IsConnected = false;
            _sequence = 0;
            _helloStartedTime = Time.unscaledTime;
            _nextHelloSendTime = 0f;
            _nextPlayerStateSendTime = 0f;
            _nextEnemyAuthorityStateSendTime = 0f;
            _nextAuthorityPollTime = 0f;
            _nextUiPanelPollTime = 0f;
            _nextSceneLoadPollTime = 0f;
            _nextDamagePollTime = 0f;
            _nextRewardPollTime = 0f;
            _lastPlayerStateSendFrame = -1;
            _lastEnemyAuthorityStateSendFrame = -1;
            _lastAuthorityPollFrame = -1;
            _nextAuthorityPollDebugTime = 0f;
            _nextAuthorityReceiveDebugTime = 0f;
            _hasServerClockOffset = false;
            _serverUtcToLocalTimeOffsetSeconds = 0d;
            _pendingUiPanelEvents.Clear();
            _pendingSceneLoadEvents.Clear();
            _pendingDamageEvents.Clear();
            _pendingRewardActions.Clear();
        }

        public void Stop()
        {
            IsRunning = false;
            IsConnected = false;
            _remoteEndPoint = null;
            _instanceId = string.Empty;
            _userId = string.Empty;
            _joinToken = string.Empty;
            _hasServerClockOffset = false;
            _serverUtcToLocalTimeOffsetSeconds = 0d;
            _nextAuthorityPollDebugTime = 0f;
            _nextAuthorityReceiveDebugTime = 0f;
            _pendingUiPanelEvents.Clear();
            _pendingSceneLoadEvents.Clear();
            _pendingDamageEvents.Clear();
            _pendingRewardActions.Clear();

            DisposeTransports();
        }

        public void Dispose()
        {
            Stop();
        }

        public void Tick(
            Func<OnlineDungeonPlayerPoseRequest> buildPlayerState,
            Func<OnlineDungeonEnemyAuthorityStateSyncRequest> buildEnemyAuthorityState,
            Action<OnlineDungeonRealtimePlayerSnapshotInfo> onPlayerSnapshot,
            Action<OnlineDungeonRealtimeAuthoritySnapshotInfo> onAuthoritySnapshot,
            Func<long> getUiPanelAfterSequence,
            Action<OnlineDungeonRealtimeUiPanelSnapshotInfo> onUiPanelSnapshot,
            Action<OnlineDungeonRealtimeSceneLoadSnapshotInfo> onSceneLoadSnapshot,
            Func<long> getDamageAfterSequence,
            Action<OnlineDungeonRealtimeDamageSnapshotInfo> onDamageSnapshot,
            Action<OnlineDungeonRealtimeRewardSnapshotInfo> onRewardSnapshot,
            Func<long> getRewardAfterVersion,
            Action onConnected,
            Action<string> onError)
        {
            if (!IsRunning || _stateTransport == null || _eventTransport == null)
                return;

            _stateTransport.Tick();
            _eventTransport.Tick();

            if (!IsConnected)
            {
                if (Time.unscaledTime >= _nextHelloSendTime)
                {
                    SendHello(onError);
                    _nextHelloSendTime = Time.unscaledTime + HelloRetryIntervalSeconds;
                }

                if (Time.unscaledTime - _helloStartedTime > HelloTimeoutSeconds)
                {
                    Stop();
                    onError?.Invoke("联机副本实时通道连接超时");
                    return;
                }
            }
            else if (buildPlayerState != null && Time.unscaledTime >= _nextPlayerStateSendTime && _lastPlayerStateSendFrame != Time.frameCount)
            {
                OnlineDungeonPlayerPoseRequest request = buildPlayerState();
                if (request != null)
                    SendPlayerState(request, onError);

                _nextPlayerStateSendTime = Time.unscaledTime + PlayerStateSendIntervalSeconds;
                _lastPlayerStateSendFrame = Time.frameCount;
            }

            if (IsConnected && buildEnemyAuthorityState != null && Time.unscaledTime >= _nextEnemyAuthorityStateSendTime && _lastEnemyAuthorityStateSendFrame != Time.frameCount)
            {
                OnlineDungeonEnemyAuthorityStateSyncRequest request = buildEnemyAuthorityState();
                if (request != null)
                    SendEnemyAuthorityState(request, onError);

                _nextEnemyAuthorityStateSendTime = Time.unscaledTime + EnemyAuthorityStateSendIntervalSeconds;
                _lastEnemyAuthorityStateSendFrame = Time.frameCount;
            }

            if (IsConnected && Time.unscaledTime >= _nextUiPanelPollTime)
            {
                SendNextUiPanelSync(getUiPanelAfterSequence, onError);
                _nextUiPanelPollTime = Time.unscaledTime + UiPanelPollIntervalSeconds;
            }

            if (IsConnected && Time.unscaledTime >= _nextSceneLoadPollTime)
            {
                SendNextSceneLoadSync(onError);
                _nextSceneLoadPollTime = Time.unscaledTime + SceneLoadPollIntervalSeconds;
            }

            if (IsConnected && Time.unscaledTime >= _nextAuthorityPollTime && _lastAuthorityPollFrame != Time.frameCount)
            {
                SendAuthorityPoll(onError);
                _nextAuthorityPollTime = Time.unscaledTime + AuthorityPollIntervalSeconds;
                _lastAuthorityPollFrame = Time.frameCount;
            }

            if (IsConnected && Time.unscaledTime >= _nextDamagePollTime)
            {
                for (int i = 0; i < MaxDamageEventsPerPoll; i++)
                {
                    bool sentDamageEvent = SendNextDamageSync(getDamageAfterSequence, onError, i == 0);
                    if (!sentDamageEvent)
                        break;
                }

                _nextDamagePollTime = Time.unscaledTime + DamagePollIntervalSeconds;
            }

            if (IsConnected && Time.unscaledTime >= _nextRewardPollTime)
            {
                SendNextRewardSync(getRewardAfterVersion, onError);
                _nextRewardPollTime = Time.unscaledTime + RewardPollIntervalSeconds;
            }

            ReceiveAvailable(onPlayerSnapshot, onAuthoritySnapshot, onUiPanelSnapshot, onSceneLoadSnapshot, onDamageSnapshot, onRewardSnapshot, onConnected, onError);
        }

        public void EnqueueUiPanelEvent(OnlineDungeonUiPanelEventRequest request)
        {
            if (request == null)
                return;

            _pendingUiPanelEvents.Enqueue(request);
        }

        public void EnqueueSceneLoadEvent(OnlineDungeonSceneLoadEventRequest request)
        {
            if (request == null)
                return;

            _pendingSceneLoadEvents.Enqueue(request);
            _nextSceneLoadPollTime = 0f;
        }

        public void EnqueueDamageEvent(OnlineDungeonDamageEventRequest request)
        {
            if (request == null)
                return;

            _pendingDamageEvents.Enqueue(request);
            _nextDamagePollTime = 0f;
        }

        public void EnqueueEnemyKillReward(OnlineDungeonEnemyKillRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.enemyRuntimeId))
                return;

            _pendingRewardActions.Enqueue(new OnlineDungeonRealtimeRewardSyncPayload
            {
                action = "enemyKill",
                enemyRuntimeId = request.enemyRuntimeId.Trim(),
                targetEnemy = request.targetEnemy,
                x = request.x,
                y = request.y,
                z = request.z,
            });
            _nextRewardPollTime = 0f;
        }

        public void EnqueueKillRewardClaim(string enemyRuntimeId)
        {
            if (string.IsNullOrWhiteSpace(enemyRuntimeId))
                return;

            _pendingRewardActions.Enqueue(new OnlineDungeonRealtimeRewardSyncPayload
            {
                action = "claimKillReward",
                enemyRuntimeId = enemyRuntimeId.Trim(),
            });
            _nextRewardPollTime = 0f;
        }

        public void EnqueueChestOpen(OnlineDungeonChestOpenRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.chestId))
                return;

            string chestId = request.chestId.Trim();
            _pendingRewardActions.Enqueue(new OnlineDungeonRealtimeRewardSyncPayload
            {
                action = "openChest",
                chestId = chestId,
                chestPrefabId = request.prefabId,
                dropCount = Mathf.Max(1, request.dropCount),
                x = request.x,
                y = request.y,
                z = request.z,
                yaw = request.yaw,
            });
            _nextRewardPollTime = 0f;
            Debug.Log($"[OnlineLootDebug] Realtime reward action queued. action=openChest target={chestId} prefab={request.prefabId} dropCount={Mathf.Max(1, request.dropCount)} connected={IsConnected}");
        }

        public void EnqueueDropPickup(string dropId)
        {
            if (string.IsNullOrWhiteSpace(dropId))
                return;

            string normalizedDropId = dropId.Trim();
            _pendingRewardActions.Enqueue(new OnlineDungeonRealtimeRewardSyncPayload
            {
                action = "pickupDrop",
                dropId = normalizedDropId,
            });
            _nextRewardPollTime = 0f;
            Debug.Log($"[OnlineLootDebug] Realtime reward action queued. action=pickupDrop target={normalizedDropId} connected={IsConnected}");
        }

        public void ClearPendingRewardActions()
        {
            _pendingRewardActions.Clear();
            _nextRewardPollTime = 0f;
        }

        public bool TryConvertServerUtcToLocalTime(string utcText, out float localTime)
        {
            localTime = 0f;
            if (!_hasServerClockOffset || !TryParseServerUtcSeconds(utcText, out double serverUtcSeconds))
                return false;

            localTime = (float)(serverUtcSeconds + _serverUtcToLocalTimeOffsetSeconds);
            return true;
        }

        private void SendHello(Action<string> onError)
        {
            var envelope = new OnlineDungeonRealtimeEnvelope
            {
                version = ProtocolVersion,
                type = "hello",
                channel = "event",
                instanceId = _instanceId,
                userId = _userId,
                joinToken = _joinToken,
                sequence = ++_sequence,
                payload = JObject.FromObject(new OnlineDungeonRealtimeHelloPayload
                {
                    clientBuild = Application.version,
                }),
            };
            Send(envelope, onError);
        }

        private bool SendNextDamageSync(Func<long> getAfterSequence, Action<string> onError, bool allowPollOnly)
        {
            OnlineDungeonDamageEventRequest request = null;
            _pendingDamageEvents.TryPeekDueRoundRobin(Time.unscaledTime, ReliableEventResendIntervalSeconds, out request);
            if (request == null && !allowPollOnly)
                return false;

            long afterSequence = getAfterSequence != null ? Math.Max(0, getAfterSequence()) : 0;
            var payload = new OnlineDungeonRealtimeDamageSyncPayload
            {
                hasEvent = request != null,
                eventId = request != null ? request.eventId : string.Empty,
                targetKind = request != null ? request.targetKind : string.Empty,
                targetRuntimeId = request != null ? request.targetRuntimeId : string.Empty,
                damage = request != null ? request.damage : 0f,
                stunDuration = request != null ? request.stunDuration : 0f,
                targetEnemy = request != null ? request.targetEnemy : null,
                x = request != null ? request.x : 0f,
                y = request != null ? request.y : 0f,
                z = request != null ? request.z : 0f,
                afterSequence = afterSequence,
            };
            var envelope = new OnlineDungeonRealtimeEnvelope
            {
                version = ProtocolVersion,
                type = request != null ? "damageEvent" : "damagePoll",
                channel = "event",
                instanceId = _instanceId,
                userId = _userId,
                joinToken = _joinToken,
                sequence = ++_sequence,
                payload = JObject.FromObject(payload),
            };
            Send(envelope, onError);
            return request != null;
        }

        private void SendNextRewardSync(Func<long> getAfterVersion, Action<string> onError)
        {
            OnlineDungeonRealtimeRewardSyncPayload payload = null;
            if (!_pendingRewardActions.TryPeekDueRoundRobin(Time.unscaledTime, ReliableEventResendIntervalSeconds, out payload))
                payload = new OnlineDungeonRealtimeRewardSyncPayload();
            payload.afterVersion = getAfterVersion != null ? Math.Max(0, getAfterVersion()) : 0;
            bool hasAction = !string.IsNullOrWhiteSpace(payload.action);
            var envelope = new OnlineDungeonRealtimeEnvelope
            {
                version = ProtocolVersion,
                type = hasAction ? "rewardEvent" : "rewardPoll",
                channel = "event",
                instanceId = _instanceId,
                userId = _userId,
                joinToken = _joinToken,
                sequence = ++_sequence,
                payload = JObject.FromObject(payload),
            };
            if (hasAction)
                Debug.Log($"[OnlineLootDebug] Send reward action. action={payload.action} target={GetRewardPayloadTargetId(payload)} afterVersion={payload.afterVersion} sequence={envelope.sequence} connected={IsConnected}");
            Send(envelope, onError);
        }

        private void SendNextSceneLoadSync(Action<string> onError)
        {
            OnlineDungeonSceneLoadEventRequest request = null;
            _pendingSceneLoadEvents.TryPeekDue(Time.unscaledTime, ReliableEventResendIntervalSeconds, out request);
            var payload = new OnlineDungeonRealtimeSceneLoadSyncPayload
            {
                action = request != null ? request.action : string.Empty,
                eventId = request != null ? request.eventId : string.Empty,
                transitionId = request != null ? request.transitionId : string.Empty,
                sceneName = request != null ? request.sceneName : string.Empty,
                bossId = request != null ? request.bossId : string.Empty,
                bossDisplayName = request != null ? request.bossDisplayName : string.Empty,
            };
            var envelope = new OnlineDungeonRealtimeEnvelope
            {
                version = ProtocolVersion,
                type = request != null ? "sceneLoadEvent" : "sceneLoadPoll",
                channel = "event",
                instanceId = _instanceId,
                userId = _userId,
                joinToken = _joinToken,
                sequence = ++_sequence,
                payload = JObject.FromObject(payload),
            };
            Send(envelope, onError);
        }

        private void SendAuthorityPoll(Action<string> onError)
        {
            var envelope = new OnlineDungeonRealtimeEnvelope
            {
                version = ProtocolVersion,
                type = "authorityPoll",
                channel = "state",
                instanceId = _instanceId,
                userId = _userId,
                joinToken = _joinToken,
                sequence = ++_sequence,
                payload = new JObject(),
            };
            if (Time.unscaledTime >= _nextAuthorityPollDebugTime)
            {
                _nextAuthorityPollDebugTime = Time.unscaledTime + OnlineEnemyFreezeDebugIntervalSeconds;
                Debug.Log($"[OnlineEnemyFreezeDebug] Send authority poll. sequence={envelope.sequence} connected={IsConnected} stateAvailable={(_stateTransport != null ? _stateTransport.Available : 0)}");
            }

            Send(envelope, onError);
            SendReliableStateFallback(envelope, onError);
        }

        private void SendPlayerState(OnlineDungeonPlayerPoseRequest request, Action<string> onError)
        {
            var envelope = new OnlineDungeonRealtimeEnvelope
            {
                version = ProtocolVersion,
                type = "playerState",
                channel = "state",
                instanceId = _instanceId,
                userId = _userId,
                joinToken = _joinToken,
                sequence = ++_sequence,
                payload = JObject.FromObject(request),
            };
            Send(envelope, onError);
            SendReliableStateFallback(envelope, onError);
        }

        private void SendEnemyAuthorityState(OnlineDungeonEnemyAuthorityStateSyncRequest request, Action<string> onError)
        {
            var envelope = new OnlineDungeonRealtimeEnvelope
            {
                version = ProtocolVersion,
                type = "enemyAuthorityState",
                channel = "state",
                instanceId = _instanceId,
                userId = _userId,
                joinToken = _joinToken,
                sequence = ++_sequence,
                payload = JObject.FromObject(request),
            };
            Send(envelope, onError);
            SendReliableStateFallback(envelope, onError);
        }

        private void SendNextUiPanelSync(Func<long> getAfterSequence, Action<string> onError)
        {
            OnlineDungeonUiPanelEventRequest request = null;
            _pendingUiPanelEvents.TryPeekDue(Time.unscaledTime, ReliableEventResendIntervalSeconds, out request);
            long afterSequence = getAfterSequence != null ? Math.Max(0, getAfterSequence()) : 0;
            var payload = new OnlineDungeonRealtimeUiPanelSyncPayload
            {
                hasEvent = request != null,
                eventId = request != null ? request.eventId : string.Empty,
                panelName = request != null ? request.panelName : string.Empty,
                open = request != null && request.open,
                afterSequence = afterSequence,
            };
            var envelope = new OnlineDungeonRealtimeEnvelope
            {
                version = ProtocolVersion,
                type = request != null ? "uiPanelEvent" : "uiPanelPoll",
                channel = "event",
                instanceId = _instanceId,
                userId = _userId,
                joinToken = _joinToken,
                sequence = ++_sequence,
                payload = JObject.FromObject(payload),
            };
            Send(envelope, onError);
        }

        private void Send(OnlineDungeonRealtimeEnvelope envelope, Action<string> onError)
        {
            IRealtimeTransport transport = SelectTransport(envelope.channel);
            if (transport == null || _remoteEndPoint == null)
                return;

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope));
                transport.Send(bytes);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }

        private void SendReliableStateFallback(OnlineDungeonRealtimeEnvelope envelope, Action<string> onError)
        {
            if (envelope == null || !string.Equals(envelope.channel, "state", StringComparison.Ordinal))
                return;

            if (_eventTransport == null || ReferenceEquals(_eventTransport, _stateTransport))
                return;

            envelope.channel = "event";
            Send(envelope, onError);
            envelope.channel = "state";
        }

        private IRealtimeTransport SelectTransport(string channel)
        {
            return string.Equals(channel, "state", StringComparison.Ordinal)
                ? _stateTransport
                : _eventTransport;
        }

        private void DisposeTransports()
        {
            IRealtimeTransport stateTransport = _stateTransport;
            IRealtimeTransport eventTransport = _eventTransport;
            _stateTransport = null;
            _eventTransport = null;

            stateTransport?.Dispose();

            if (eventTransport != null && !ReferenceEquals(eventTransport, stateTransport))
                eventTransport.Dispose();
        }

        private void ReceiveAvailable(
            Action<OnlineDungeonRealtimePlayerSnapshotInfo> onPlayerSnapshot,
            Action<OnlineDungeonRealtimeAuthoritySnapshotInfo> onAuthoritySnapshot,
            Action<OnlineDungeonRealtimeUiPanelSnapshotInfo> onUiPanelSnapshot,
            Action<OnlineDungeonRealtimeSceneLoadSnapshotInfo> onSceneLoadSnapshot,
            Action<OnlineDungeonRealtimeDamageSnapshotInfo> onDamageSnapshot,
            Action<OnlineDungeonRealtimeRewardSnapshotInfo> onRewardSnapshot,
            Action onConnected,
            Action<string> onError)
        {
            ReceiveFromTransport(_eventTransport, onPlayerSnapshot, onAuthoritySnapshot, onUiPanelSnapshot, onSceneLoadSnapshot, onDamageSnapshot, onRewardSnapshot, onConnected, onError);
            if (!ReferenceEquals(_stateTransport, _eventTransport))
                ReceiveFromTransport(_stateTransport, onPlayerSnapshot, onAuthoritySnapshot, onUiPanelSnapshot, onSceneLoadSnapshot, onDamageSnapshot, onRewardSnapshot, onConnected, onError);
        }

        private void ReceiveFromTransport(
            IRealtimeTransport transport,
            Action<OnlineDungeonRealtimePlayerSnapshotInfo> onPlayerSnapshot,
            Action<OnlineDungeonRealtimeAuthoritySnapshotInfo> onAuthoritySnapshot,
            Action<OnlineDungeonRealtimeUiPanelSnapshotInfo> onUiPanelSnapshot,
            Action<OnlineDungeonRealtimeSceneLoadSnapshotInfo> onSceneLoadSnapshot,
            Action<OnlineDungeonRealtimeDamageSnapshotInfo> onDamageSnapshot,
            Action<OnlineDungeonRealtimeRewardSnapshotInfo> onRewardSnapshot,
            Action onConnected,
            Action<string> onError)
        {
            while (transport != null && transport.Available > 0)
            {
                if (!transport.TryReceive(out byte[] bytes))
                    continue;

                if (bytes.Length > ReceiveBufferBytes)
                {
                    Debug.LogWarning($"[OnlineDungeonRealtimeClient] 实时消息过大，已丢弃。bytes={bytes.Length} max={ReceiveBufferBytes}");
                    continue;
                }

                OnlineDungeonRealtimeEnvelope envelope;
                try
                {
                    string json = Encoding.UTF8.GetString(bytes);
                    envelope = JsonConvert.DeserializeObject<OnlineDungeonRealtimeEnvelope>(json);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex.Message);
                    continue;
                }

                if (envelope == null)
                    continue;

                if (envelope.version != ProtocolVersion)
                {
                    onError?.Invoke("联机副本实时协议版本不匹配");
                    continue;
                }

                if (string.Equals(envelope.type, "helloAck", StringComparison.Ordinal))
                {
                    OnlineDungeonRealtimeHelloAckPayload ack = envelope.payload != null
                        ? envelope.payload.ToObject<OnlineDungeonRealtimeHelloAckPayload>()
                        : null;
                    if (!IsSupportedHelloAck(ack, onError))
                        continue;

                    bool wasConnected = IsConnected;
                    IsConnected = true;
                    if (!wasConnected)
                        onConnected?.Invoke();
                    continue;
                }

                if (string.Equals(envelope.type, "playerSnapshot", StringComparison.Ordinal))
                {
                    OnlineDungeonRealtimePlayerSnapshotInfo snapshot = envelope.payload != null
                        ? envelope.payload.ToObject<OnlineDungeonRealtimePlayerSnapshotInfo>()
                        : null;
                    if (snapshot != null)
                        onPlayerSnapshot?.Invoke(snapshot);
                    continue;
                }

                if (string.Equals(envelope.type, "authoritySnapshot", StringComparison.Ordinal))
                {
                    OnlineDungeonRealtimeAuthoritySnapshotInfo snapshot = envelope.payload != null
                        ? envelope.payload.ToObject<OnlineDungeonRealtimeAuthoritySnapshotInfo>()
                        : null;
                    if (snapshot != null)
                    {
                        OnlineDungeonAuthorityStateInfo state = snapshot.state;
                        if (Time.unscaledTime >= _nextAuthorityReceiveDebugTime)
                        {
                            _nextAuthorityReceiveDebugTime = Time.unscaledTime + OnlineEnemyFreezeDebugIntervalSeconds;
                            Debug.Log($"[OnlineEnemyFreezeDebug] Receive authority snapshot. sequence={envelope.sequence} hasState={state != null} initialized={(state != null && state.initialized)} version={(state != null ? state.version : 0)} enemyCount={(state?.enemies != null ? state.enemies.Count : 0)}");
                        }

                        onAuthoritySnapshot?.Invoke(snapshot);
                    }
                    continue;
                }

                if (string.Equals(envelope.type, "uiPanelSnapshot", StringComparison.Ordinal))
                {
                    OnlineDungeonRealtimeUiPanelSnapshotInfo snapshot = envelope.payload != null
                        ? envelope.payload.ToObject<OnlineDungeonRealtimeUiPanelSnapshotInfo>()
                        : null;
                    if (snapshot != null)
                    {
                        AcknowledgeUiPanelEvent(snapshot.ackEventId);
                        onUiPanelSnapshot?.Invoke(snapshot);
                    }
                    continue;
                }

                if (string.Equals(envelope.type, "sceneLoadSnapshot", StringComparison.Ordinal))
                {
                    OnlineDungeonRealtimeSceneLoadSnapshotInfo snapshot = envelope.payload != null
                        ? envelope.payload.ToObject<OnlineDungeonRealtimeSceneLoadSnapshotInfo>()
                        : null;
                    if (snapshot != null)
                    {
                        AcknowledgeSceneLoadEvent(snapshot.ackEventId);
                        onSceneLoadSnapshot?.Invoke(snapshot);
                    }
                    continue;
                }

                if (string.Equals(envelope.type, "damageSnapshot", StringComparison.Ordinal))
                {
                    OnlineDungeonRealtimeDamageSnapshotInfo snapshot = envelope.payload != null
                        ? envelope.payload.ToObject<OnlineDungeonRealtimeDamageSnapshotInfo>()
                        : null;
                    if (snapshot != null)
                    {
                        AcknowledgeDamageEvent(snapshot.ackEventId);
                        onDamageSnapshot?.Invoke(snapshot);
                    }
                    continue;
                }

                if (string.Equals(envelope.type, "rewardSnapshot", StringComparison.Ordinal))
                {
                    OnlineDungeonRealtimeRewardSnapshotInfo snapshot = envelope.payload != null
                        ? envelope.payload.ToObject<OnlineDungeonRealtimeRewardSnapshotInfo>()
                        : null;
                    if (snapshot != null)
                    {
                        bool hasRewardStateItems = snapshot.state != null
                            && ((snapshot.state.drops != null && snapshot.state.drops.Count > 0)
                                || (snapshot.state.killRewards != null && snapshot.state.killRewards.Count > 0));
                        if (!string.IsNullOrWhiteSpace(snapshot.ackAction) || snapshot.rejected || snapshot.dropPickup != null || hasRewardStateItems)
                            Debug.Log($"[OnlineLootDebug] Receive reward snapshot. ackAction={snapshot.ackAction} ackTarget={snapshot.ackTargetId} rejected={snapshot.rejected} hasDropPickup={snapshot.dropPickup != null} hasState={snapshot.state != null} stateVersion={(snapshot.state != null ? snapshot.state.version : 0)} dropCount={(snapshot.state?.drops != null ? snapshot.state.drops.Count : 0)}");
                        AcknowledgeRewardAction(snapshot.ackAction, snapshot.ackTargetId);
                        onRewardSnapshot?.Invoke(snapshot);
                    }
                    continue;
                }

                if (string.Equals(envelope.type, "error", StringComparison.Ordinal))
                {
                    string message = envelope.payload != null
                        ? envelope.payload.Value<string>("message")
                        : string.Empty;
                    string code = envelope.payload != null
                        ? envelope.payload.Value<string>("code")
                        : string.Empty;
                    string closeReason = envelope.payload != null
                        ? envelope.payload.Value<string>("closeReason")
                        : string.Empty;
                    string ackAction = envelope.payload != null
                        ? envelope.payload.Value<string>("ackAction")
                        : string.Empty;
                    string ackTargetId = envelope.payload != null
                        ? envelope.payload.Value<string>("ackTargetId")
                        : string.Empty;
                    if (string.Equals(code, "sessionClosed", StringComparison.Ordinal))
                    {
                        onError?.Invoke(string.IsNullOrWhiteSpace(closeReason) ? "sessionClosed" : $"sessionClosed:{closeReason}");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(ackAction))
                    {
                        Debug.Log($"[OnlineLootDebug] Receive reward rejection. ackAction={ackAction} ackTarget={ackTargetId} message={message}");
                        AcknowledgeRewardAction(ackAction, ackTargetId);
                        onRewardSnapshot?.Invoke(new OnlineDungeonRealtimeRewardSnapshotInfo
                        {
                            ackAction = ackAction,
                            ackTargetId = ackTargetId,
                            rejected = true,
                            errorMessage = string.IsNullOrWhiteSpace(message) ? "联机副本奖励事件被拒绝" : message,
                        });
                        continue;
                    }

                    onError?.Invoke(string.IsNullOrWhiteSpace(message) ? "联机副本实时通道错误" : message);
                }
            }
        }

        private void AcknowledgeUiPanelEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return;

            _pendingUiPanelEvents.DequeueIfHeadMatches(request =>
                request != null && string.Equals(request.eventId, eventId, StringComparison.Ordinal));
        }

        private void AcknowledgeSceneLoadEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return;

            _pendingSceneLoadEvents.DequeueIfHeadMatches(request =>
                request != null && string.Equals(request.eventId, eventId, StringComparison.Ordinal));
        }

        private void AcknowledgeDamageEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return;

            _pendingDamageEvents.RemoveFirstMatching(request =>
                request != null && string.Equals(request.eventId, eventId, StringComparison.Ordinal));
        }

        private void AcknowledgeRewardAction(string action, string targetId)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;

            _pendingRewardActions.RemoveFirstMatching(request =>
            {
                if (request == null || !string.Equals(request.action, action, StringComparison.Ordinal))
                    return false;

                string requestTargetId = string.Equals(action, "pickupDrop", StringComparison.Ordinal)
                    ? request.dropId
                    : string.Equals(action, "openChest", StringComparison.Ordinal)
                        ? request.chestId
                        : request.enemyRuntimeId;
                return string.Equals(requestTargetId, targetId, StringComparison.Ordinal);
            });
        }

        private static string GetRewardPayloadTargetId(OnlineDungeonRealtimeRewardSyncPayload payload)
        {
            if (payload == null)
                return string.Empty;

            if (string.Equals(payload.action, "pickupDrop", StringComparison.Ordinal))
                return payload.dropId;

            if (string.Equals(payload.action, "openChest", StringComparison.Ordinal))
                return payload.chestId;

            return payload.enemyRuntimeId;
        }

        private static bool TryResolveRemoteEndPoint(string dungeonServerUrl, int port, out IPEndPoint endPoint, out string error)
        {
            endPoint = null;
            error = string.Empty;

            if (!Uri.TryCreate(dungeonServerUrl, UriKind.Absolute, out Uri uri))
            {
                error = "联机副本服务器地址无效";
                return false;
            }

            string host = uri.Host;
            if (string.IsNullOrWhiteSpace(host))
            {
                error = "联机副本服务器主机为空";
                return false;
            }

            try
            {
                IPAddress[] addresses = Dns.GetHostAddresses(host);
                for (int i = 0; i < addresses.Length; i++)
                {
                    IPAddress address = addresses[i];
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        endPoint = new IPEndPoint(address, port);
                        return true;
                    }
                }

                error = "无法解析联机副本服务器 IPv4 地址";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private sealed class OnlineDungeonRealtimeEnvelope
        {
            public int version;
            public string type;
            public string channel;
            public string instanceId;
            public string userId;
            public string joinToken;
            public long sequence;
            public JObject payload;
        }

        private interface IRealtimeTransport : IDisposable
        {
            int Available { get; }

            void Tick();

            void Send(byte[] bytes);

            bool TryReceive(out byte[] bytes);
        }

        private sealed class UdpRealtimeTransport : IRealtimeTransport
        {
            private readonly UdpClient _client;
            private readonly IPEndPoint _remoteEndPoint;
            private bool _disposed;

            public UdpRealtimeTransport(IPEndPoint remoteEndPoint)
            {
                _remoteEndPoint = remoteEndPoint;
                _client = new UdpClient(0)
                {
                    Client =
                    {
                        Blocking = false,
                    },
                };
            }

            public int Available
            {
                get
                {
                    if (_disposed || _client == null)
                        return 0;

                    try
                    {
                        return _client.Available;
                    }
                    catch (ObjectDisposedException)
                    {
                        _disposed = true;
                        return 0;
                    }
                    catch (NullReferenceException)
                    {
                        _disposed = true;
                        return 0;
                    }
                }
            }

            public void Tick()
            {
            }

            public void Send(byte[] bytes)
            {
                if (_disposed || bytes == null || bytes.Length <= 0)
                    return;

                try
                {
                    _client.Send(bytes, bytes.Length, _remoteEndPoint);
                }
                catch (ObjectDisposedException)
                {
                    _disposed = true;
                }
                catch (NullReferenceException)
                {
                    _disposed = true;
                }
            }

            public bool TryReceive(out byte[] bytes)
            {
                bytes = null;
                if (_disposed || _client == null || Available <= 0)
                    return false;

                try
                {
                    IPEndPoint sender = null;
                    bytes = _client.Receive(ref sender);
                    return bytes != null && bytes.Length > 0;
                }
                catch (ObjectDisposedException)
                {
                    _disposed = true;
                    return false;
                }
                catch (NullReferenceException)
                {
                    _disposed = true;
                    return false;
                }
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _client.Close();
                _client.Dispose();
            }
        }

        private sealed class KcpRealtimeTransport : IRealtimeTransport
        {
            private readonly KcpClient _client;
            private readonly Queue<byte[]> _incoming = new Queue<byte[]>();
            private readonly Queue<byte[]> _pendingSends = new Queue<byte[]>();
            private bool _connected;

            public KcpRealtimeTransport(IPEndPoint remoteEndPoint)
            {
                var config = new KcpConfig(
                    DualMode: false,
                    NoDelay: true,
                    Interval: 10,
                    CongestionWindow: false);
                _client = new KcpClient(OnConnected, OnData, OnDisconnected, OnError, config);
                _client.Connect(remoteEndPoint.Address.ToString(), (ushort)remoteEndPoint.Port);
            }

            public int Available => _incoming.Count;

            public void Tick()
            {
                _client.Tick();
                FlushPendingSends();
            }

            public void Send(byte[] bytes)
            {
                if (bytes == null || bytes.Length <= 0)
                    return;

                if (!_connected)
                {
                    while (_pendingSends.Count >= MaxPendingKcpSends)
                        _pendingSends.Dequeue();

                    _pendingSends.Enqueue(bytes);
                    return;
                }

                _client.Send(new ArraySegment<byte>(bytes), KcpChannel.Reliable);
                _client.TickOutgoing();
            }

            public bool TryReceive(out byte[] bytes)
            {
                bytes = null;
                if (_incoming.Count <= 0)
                    return false;

                bytes = _incoming.Dequeue();
                return bytes != null && bytes.Length > 0;
            }

            public void Dispose()
            {
                try
                {
                    _client.Disconnect();
                    _client.Tick();
                }
                catch (Exception)
                {
                }
            }

            private void OnConnected()
            {
                _connected = true;
                FlushPendingSends();
            }

            private void OnData(ArraySegment<byte> data, KcpChannel channel)
            {
                if (channel != KcpChannel.Reliable || data.Array == null || data.Count <= 0)
                    return;

                var bytes = new byte[data.Count];
                Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);
                _incoming.Enqueue(bytes);
            }

            private void OnDisconnected()
            {
                _connected = false;
            }

            private void OnError(ErrorCode error, string message)
            {
                Debug.LogWarning($"[OnlineDungeonRealtimeClient] KCP事件通道错误：{message}");
            }

            private void FlushPendingSends()
            {
                if (!_connected)
                    return;

                while (_pendingSends.Count > 0)
                {
                    byte[] bytes = _pendingSends.Dequeue();
                    _client.Send(new ArraySegment<byte>(bytes), KcpChannel.Reliable);
                }
            }
        }

        private sealed class OnlineDungeonRealtimeHelloPayload
        {
            public string clientBuild;
        }

        private sealed class ReliableEventQueue<T>
        {
            private readonly Queue<ReliableEvent<T>> _events = new Queue<ReliableEvent<T>>();
            private readonly int _maxCount;
            private readonly string _queueName;

            public ReliableEventQueue(int maxCount, string queueName)
            {
                _maxCount = Math.Max(1, maxCount);
                _queueName = queueName ?? string.Empty;
            }

            public void Clear()
            {
                _events.Clear();
            }

            public void Enqueue(T payload)
            {
                while (_events.Count >= _maxCount)
                {
                    _events.Dequeue();
                    Debug.LogWarning($"[OnlineDungeonRealtimeClient] {_queueName}可靠事件队列已满，最早的未确认事件将被丢弃。");
                }

                _events.Enqueue(new ReliableEvent<T>(payload));
            }

            public bool TryPeekDue(float now, float resendIntervalSeconds, out T payload)
            {
                payload = default(T);
                if (_events.Count <= 0)
                    return false;

                ReliableEvent<T> reliableEvent = _events.Peek();
                if (!reliableEvent.ShouldSend(now, resendIntervalSeconds))
                    return false;

                reliableEvent.MarkSent(now);
                payload = reliableEvent.Payload;
                return true;
            }

            public bool TryPeekDueRoundRobin(float now, float resendIntervalSeconds, out T payload)
            {
                payload = default(T);
                int count = _events.Count;
                if (count <= 0)
                    return false;

                for (int i = 0; i < count; i++)
                {
                    ReliableEvent<T> reliableEvent = _events.Dequeue();
                    _events.Enqueue(reliableEvent);
                    if (!reliableEvent.ShouldSend(now, resendIntervalSeconds))
                        continue;

                    reliableEvent.MarkSent(now);
                    payload = reliableEvent.Payload;
                    return true;
                }

                return false;
            }

            public void DequeueIfHeadMatches(Func<T, bool> predicate)
            {
                if (_events.Count <= 0 || predicate == null)
                    return;

                ReliableEvent<T> reliableEvent = _events.Peek();
                if (predicate(reliableEvent.Payload))
                    _events.Dequeue();
            }

            public void RemoveFirstMatching(Func<T, bool> predicate)
            {
                if (_events.Count <= 0 || predicate == null)
                    return;

                int count = _events.Count;
                bool removed = false;
                for (int i = 0; i < count; i++)
                {
                    ReliableEvent<T> reliableEvent = _events.Dequeue();
                    if (!removed && predicate(reliableEvent.Payload))
                    {
                        removed = true;
                        continue;
                    }

                    _events.Enqueue(reliableEvent);
                }
            }
        }

        private sealed class ReliableEvent<T>
        {
            private bool _hasSent;
            private float _lastSentTime;

            public ReliableEvent(T payload)
            {
                Payload = payload;
            }

            public T Payload { get; }

            public bool ShouldSend(float now, float resendIntervalSeconds)
            {
                return !_hasSent || now - _lastSentTime >= resendIntervalSeconds;
            }

            public void MarkSent(float now)
            {
                _hasSent = true;
                _lastSentTime = now;
            }
        }

        private bool IsSupportedHelloAck(OnlineDungeonRealtimeHelloAckPayload ack, Action<string> onError)
        {
            if (ack == null)
            {
                onError?.Invoke("联机副本实时握手响应为空");
                return false;
            }

            if (ack.serverProtocolVersion != ProtocolVersion)
            {
                onError?.Invoke("联机副本实时协议版本不匹配");
                return false;
            }

            if (!string.Equals(ack.stateTransport, ExpectedStateTransport, StringComparison.Ordinal) ||
                !IsSupportedEventTransport(ack.eventTransport) ||
                !string.Equals(ack.synchronizationMode, ExpectedSynchronizationMode, StringComparison.Ordinal))
            {
                onError?.Invoke("联机副本实时传输能力不匹配");
                return false;
            }

            if (!string.Equals(ack.authoritySchema, ExpectedAuthoritySchema, StringComparison.Ordinal) ||
                !string.Equals(ack.sessionLifecycleMode, ExpectedSessionLifecycleMode, StringComparison.Ordinal))
            {
                onError?.Invoke("联机副本实时权威同步能力不匹配");
                return false;
            }

            if (!string.Equals(ack.stateChannel, ExpectedStateChannel, StringComparison.Ordinal) ||
                !string.Equals(ack.eventChannel, ExpectedEventChannel, StringComparison.Ordinal))
            {
                onError?.Invoke("联机副本实时通道能力不匹配");
                return false;
            }

            if (!TryParseServerUtcSeconds(ack.utcNow, out double serverUtcSeconds))
            {
                onError?.Invoke("联机副本实时服务器时间无效");
                return false;
            }

            _serverUtcToLocalTimeOffsetSeconds = Time.unscaledTime - serverUtcSeconds;
            _hasServerClockOffset = true;
            return true;
        }

        private static bool IsSupportedEventTransport(string eventTransport)
        {
            return string.Equals(eventTransport, ExpectedEventTransport, StringComparison.Ordinal);
        }

        private static bool TryParseServerUtcSeconds(string utcText, out double utcSeconds)
        {
            utcSeconds = 0d;
            if (string.IsNullOrWhiteSpace(utcText))
                return false;

            if (!DateTimeOffset.TryParse(
                    utcText,
                    null,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsedUtc))
                return false;

            utcSeconds = (parsedUtc.UtcDateTime - UnixEpochUtc).TotalSeconds;
            return true;
        }

        private sealed class OnlineDungeonRealtimeHelloAckPayload
        {
            public int serverProtocolVersion;
            public string stateTransport;
            public string eventTransport;
            public string synchronizationMode;
            public string authoritySchema;
            public string sessionLifecycleMode;
            public string stateChannel;
            public string eventChannel;
            public string utcNow;
        }

        private sealed class OnlineDungeonRealtimeUiPanelSyncPayload
        {
            public bool hasEvent;
            public string eventId;
            public string panelName;
            public bool open;
            public long afterSequence;
        }

        private sealed class OnlineDungeonRealtimeSceneLoadSyncPayload
        {
            public string action;
            public string eventId;
            public string transitionId;
            public string sceneName;
            public string bossId;
            public string bossDisplayName;
        }

        private sealed class OnlineDungeonRealtimeDamageSyncPayload
        {
            public bool hasEvent;
            public string eventId;
            public string targetKind;
            public string targetRuntimeId;
            public float damage;
            public float stunDuration;
            public OnlineDungeonDamageTargetEnemyInfo targetEnemy;
            public float x;
            public float y;
            public float z;
            public long afterSequence;
        }

        private sealed class OnlineDungeonRealtimeRewardSyncPayload
        {
            public string action;
            public string enemyRuntimeId;
            public string chestId;
            public string chestPrefabId;
            public string dropId;
            public OnlineDungeonDamageTargetEnemyInfo targetEnemy;
            public int dropCount;
            public long afterVersion;
            public float x;
            public float y;
            public float z;
            public float yaw;
        }
    }

    [Serializable]
    public sealed class OnlineDungeonRealtimePlayerSnapshotInfo
    {
        public List<OnlineDungeonPlayerPoseInfo> poses;
    }

    [Serializable]
    public sealed class OnlineDungeonRealtimeAuthoritySnapshotInfo
    {
        public OnlineDungeonAuthorityStateInfo state;
    }

    [Serializable]
    public sealed class OnlineDungeonEnemyAuthorityStateSyncRequest
    {
        public List<OnlineDungeonEnemyAuthorityInfo> enemies;
    }

    [Serializable]
    public sealed class OnlineDungeonRealtimeUiPanelSnapshotInfo
    {
        public string ackEventId;
        public List<OnlineDungeonUiPanelEventInfo> events;
        public OnlineDungeonUiPanelStateInfo state;
    }

    [Serializable]
    public sealed class OnlineDungeonSceneLoadEventRequest
    {
        public string eventId;
        public string action;
        public string transitionId;
        public string sceneName;
        public string bossId;
        public string bossDisplayName;
    }

    [Serializable]
    public sealed class OnlineDungeonRealtimeSceneLoadSnapshotInfo
    {
        public string ackEventId;
        public OnlineDungeonSceneLoadStateInfo state;
    }

    [Serializable]
    public sealed class OnlineDungeonSceneLoadStateInfo
    {
        public long version;
        public bool active;
        public bool released;
        public string transitionId;
        public string sceneName;
        public string bossId;
        public string bossDisplayName;
        public string releaseAtUtc;
        public List<string> readyUserIds;
        public List<string> completedUserIds;
        public int participantCount;
        public int readyCount;
        public int completedCount;
    }

    [Serializable]
    public sealed class OnlineDungeonRealtimeDamageSnapshotInfo
    {
        public string ackEventId;
        public List<OnlineDungeonDamageEventInfo> ackEvents;
        public List<OnlineDungeonDamageEventInfo> events;
    }

    [Serializable]
    public sealed class OnlineDungeonRealtimeRewardSnapshotInfo
    {
        public string ackAction;
        public string ackTargetId;
        public bool rejected;
        public string errorMessage;
        public OnlineDungeonKillRewardClaimResultInfo killRewardClaim;
        public OnlineDungeonDropPickupResultInfo dropPickup;
        public OnlineDungeonRewardStateInfo state;
    }
}
