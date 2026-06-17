using System.Text;
using System.Text.Json;
using kcp2k;

namespace OnlineDungeonServer.Realtime;

internal sealed class DungeonRealtimeKcpHostedService : BackgroundService
{
    private readonly DungeonInstanceRegistry _registry;
    private readonly ILogger<DungeonRealtimeKcpHostedService> _logger;
    private readonly int _port;
    private KcpServer? _server;

    public DungeonRealtimeKcpHostedService(
        DungeonInstanceRegistry registry,
        IConfiguration configuration,
        ILogger<DungeonRealtimeKcpHostedService> logger)
    {
        _registry = registry;
        _logger = logger;
        _port = configuration.GetValue("Realtime:KcpPort", DungeonRealtimeProtocol.DefaultKcpPort);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new KcpConfig(DualMode: false, NoDelay: true, Interval: 10, CongestionWindow: false);
        _server = new KcpServer(
            connectionId => _logger.LogInformation("Online dungeon KCP event connection {ConnectionId} connected.", connectionId),
            HandleData,
            connectionId => _logger.LogInformation("Online dungeon KCP event connection {ConnectionId} disconnected.", connectionId),
            (connectionId, error, reason) => _logger.LogWarning("Online dungeon KCP event connection {ConnectionId} error {Error}: {Reason}", connectionId, error, reason),
            config);
        _server.Start((ushort)_port);
        _logger.LogInformation("Online dungeon realtime KCP event endpoint listening on 0.0.0.0:{Port}", _port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _server.Tick();
                await Task.Delay(1, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            _server.Stop();
            _server = null;
        }
    }

    private void HandleData(int connectionId, ArraySegment<byte> segment, KcpChannel channel)
    {
        if (channel != KcpChannel.Reliable)
            return;

        if (segment.Array == null || segment.Count <= 0)
            return;

        if (segment.Count > DungeonRealtimeProtocol.MaxReliableMessageBytes)
        {
            _logger.LogWarning(
                "Online dungeon KCP message too large. connection={ConnectionId} bytes={Bytes} max={MaxBytes}",
                connectionId,
                segment.Count,
                DungeonRealtimeProtocol.MaxReliableMessageBytes);
            return;
        }

        DungeonRealtimeEnvelope? envelope;
        try
        {
            string json = Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
            envelope = JsonSerializer.Deserialize<DungeonRealtimeEnvelope>(json, DungeonRealtimeProtocol.JsonOptions);
        }
        catch (Exception ex)
        {
            SendError(connectionId, string.Empty, string.Empty, "实时协议数据格式无效");
            _logger.LogDebug(ex, "Invalid realtime KCP message received from {ConnectionId}", connectionId);
            return;
        }

        if (envelope == null)
        {
            SendError(connectionId, string.Empty, string.Empty, "实时协议数据为空");
            return;
        }

        if (envelope.Version != DungeonRealtimeProtocol.Version)
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, "实时协议版本不匹配");
            return;
        }

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.HelloType, StringComparison.Ordinal))
        {
            HandleHello(connectionId, envelope);
            return;
        }

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.PlayerStateType, StringComparison.Ordinal))
        {
            HandlePlayerState(connectionId, envelope);
            return;
        }

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.EnemyAuthorityStateType, StringComparison.Ordinal))
        {
            HandleEnemyAuthorityState(connectionId, envelope);
            return;
        }

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.AuthorityPollType, StringComparison.Ordinal))
        {
            HandleAuthorityPoll(connectionId, envelope);
            return;
        }

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.UiPanelEventType, StringComparison.Ordinal)
            || string.Equals(envelope.Type, DungeonRealtimeProtocol.UiPanelPollType, StringComparison.Ordinal))
        {
            HandleUiPanelSync(connectionId, envelope);
            return;
        }

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.SceneLoadEventType, StringComparison.Ordinal)
            || string.Equals(envelope.Type, DungeonRealtimeProtocol.SceneLoadPollType, StringComparison.Ordinal))
        {
            HandleSceneLoadSync(connectionId, envelope);
            return;
        }

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.DamageEventType, StringComparison.Ordinal)
            || string.Equals(envelope.Type, DungeonRealtimeProtocol.DamagePollType, StringComparison.Ordinal))
        {
            HandleDamageSync(connectionId, envelope);
            return;
        }

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.RewardEventType, StringComparison.Ordinal)
            || string.Equals(envelope.Type, DungeonRealtimeProtocol.RewardPollType, StringComparison.Ordinal))
        {
            HandleRewardSync(connectionId, envelope);
            return;
        }

        SendError(connectionId, envelope.InstanceId, envelope.UserId, "KCP实时事件通道不处理该消息类型");
    }

    private void HandleHello(int connectionId, DungeonRealtimeEnvelope envelope)
    {
        var validateRequest = new ValidateDungeonParticipantRequest
        {
            UserId = envelope.UserId,
            JoinToken = envelope.JoinToken,
        };
        if (!_registry.TryValidateParticipant(envelope.InstanceId, validateRequest, out _, out string error))
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, error);
            return;
        }

        var payload = new DungeonRealtimeHelloAckPayload
        {
            ServerProtocolVersion = DungeonRealtimeProtocol.Version,
            UtcNow = DateTime.UtcNow,
        };
        Send(
            connectionId,
            envelope.InstanceId,
            envelope.UserId,
            DungeonRealtimeProtocol.HelloAckType,
            envelope.Sequence,
            payload);
    }

    private void HandlePlayerState(int connectionId, DungeonRealtimeEnvelope envelope)
    {
        if (envelope.Payload == null)
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, "玩家实时状态为空");
            return;
        }

        UpsertDungeonPlayerPoseRequest? request;
        try
        {
            request = envelope.Payload.Value.Deserialize<UpsertDungeonPlayerPoseRequest>(DungeonRealtimeProtocol.JsonOptions);
        }
        catch
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, "玩家实时状态格式无效");
            return;
        }

        if (request == null)
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, "玩家实时状态为空");
            return;
        }

        if (!string.Equals(request.UserId, envelope.UserId, StringComparison.Ordinal)
            || !string.Equals(request.JoinToken, envelope.JoinToken, StringComparison.Ordinal))
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, "玩家实时状态身份不匹配");
            return;
        }

        if (!_registry.TryUpsertPlayerPose(envelope.InstanceId, request, out DungeonPlayerPoseListDto? poses, out string error))
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, error);
            return;
        }

        var payload = new DungeonRealtimePlayerSnapshotPayload
        {
            Poses = poses?.poses ?? new List<DungeonPlayerPoseDto>(),
        };
        Send(connectionId, envelope.InstanceId, envelope.UserId, DungeonRealtimeProtocol.PlayerSnapshotType, envelope.Sequence, payload);
    }

    private void HandleEnemyAuthorityState(int connectionId, DungeonRealtimeEnvelope envelope)
    {
        if (envelope.Payload == null)
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, "敌人权威状态为空");
            return;
        }

        DungeonRealtimeEnemyAuthorityStatePayload? request;
        try
        {
            request = envelope.Payload.Value.Deserialize<DungeonRealtimeEnemyAuthorityStatePayload>(DungeonRealtimeProtocol.JsonOptions);
        }
        catch
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, "敌人权威状态格式无效");
            return;
        }

        if (!_registry.TryUpsertEnemyAuthorityState(
                envelope.InstanceId,
                envelope.UserId,
                envelope.JoinToken,
                request?.Enemies,
                out DungeonAuthorityStateDto? state,
                out string error))
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, error);
            return;
        }

        var payload = new DungeonRealtimeAuthoritySnapshotPayload
        {
            State = state,
        };
        Send(connectionId, envelope.InstanceId, envelope.UserId, DungeonRealtimeProtocol.AuthoritySnapshotType, envelope.Sequence, payload);
    }

    private void HandleAuthorityPoll(int connectionId, DungeonRealtimeEnvelope envelope)
    {
        if (!_registry.TryGetAuthorityState(envelope.InstanceId, envelope.UserId, envelope.JoinToken, out DungeonAuthorityStateDto? state, out string error))
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, error);
            return;
        }

        var payload = new DungeonRealtimeAuthoritySnapshotPayload
        {
            State = state,
        };
        Send(connectionId, envelope.InstanceId, envelope.UserId, DungeonRealtimeProtocol.AuthoritySnapshotType, envelope.Sequence, payload);
    }

    private void HandleUiPanelSync(int connectionId, DungeonRealtimeEnvelope envelope)
    {
        DungeonRealtimeUiPanelSyncPayload? syncPayload = null;
        if (envelope.Payload != null)
        {
            try
            {
                syncPayload = envelope.Payload.Value.Deserialize<DungeonRealtimeUiPanelSyncPayload>(DungeonRealtimeProtocol.JsonOptions);
            }
            catch
            {
                SendError(connectionId, envelope.InstanceId, envelope.UserId, "面板实时事件格式无效");
                return;
            }
        }

        long afterSequence = Math.Max(0, syncPayload?.AfterSequence ?? 0);
        string ackEventId = string.Empty;
        if (syncPayload?.HasEvent == true)
        {
            string requestedEventId = string.IsNullOrWhiteSpace(syncPayload.EventId) ? string.Empty : syncPayload.EventId.Trim();
            var request = new AddDungeonUiPanelEventRequest
            {
                EventId = requestedEventId,
                UserId = envelope.UserId,
                JoinToken = envelope.JoinToken,
                PanelName = syncPayload.PanelName,
                Open = syncPayload.Open,
            };
            if (!_registry.TryAddUiPanelEvent(envelope.InstanceId, request, out _, out string addError))
            {
                SendError(connectionId, envelope.InstanceId, envelope.UserId, addError);
                return;
            }

            ackEventId = requestedEventId;
        }

        if (!_registry.TryGetUiPanelEvents(envelope.InstanceId, envelope.UserId, envelope.JoinToken, afterSequence, out DungeonUiPanelEventListDto? events, out string eventsError))
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, eventsError);
            return;
        }

        if (!_registry.TryGetUiPanelState(envelope.InstanceId, envelope.UserId, envelope.JoinToken, out DungeonUiPanelStateDto? state, out string stateError))
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, stateError);
            return;
        }

        var payload = new DungeonRealtimeUiPanelSnapshotPayload
        {
            AckEventId = ackEventId,
            Events = events?.events ?? new List<DungeonUiPanelEventDto>(),
            State = state,
        };
        Send(connectionId, envelope.InstanceId, envelope.UserId, DungeonRealtimeProtocol.UiPanelSnapshotType, envelope.Sequence, payload);
    }

    private void HandleSceneLoadSync(int connectionId, DungeonRealtimeEnvelope envelope)
    {
        DungeonRealtimeSceneLoadSyncPayload? syncPayload = null;
        if (envelope.Payload != null)
        {
            try
            {
                syncPayload = envelope.Payload.Value.Deserialize<DungeonRealtimeSceneLoadSyncPayload>(DungeonRealtimeProtocol.JsonOptions);
            }
            catch
            {
                SendError(connectionId, envelope.InstanceId, envelope.UserId, "场景加载实时事件格式无效");
                return;
            }
        }

        string ackEventId = string.Empty;
        DungeonSceneLoadStateDto? state;
        string action = syncPayload?.Action?.Trim() ?? string.Empty;
        if (string.Equals(action, "start", StringComparison.Ordinal))
        {
            ackEventId = syncPayload?.EventId?.Trim() ?? string.Empty;
            var request = new StartDungeonSceneLoadRequest
            {
                UserId = envelope.UserId,
                JoinToken = envelope.JoinToken,
                TransitionId = syncPayload?.TransitionId,
                SceneName = syncPayload?.SceneName,
                BossId = syncPayload?.BossId,
                BossDisplayName = syncPayload?.BossDisplayName,
            };
            if (!_registry.TryStartSceneLoad(envelope.InstanceId, request, out state, out string startError))
            {
                SendError(connectionId, envelope.InstanceId, envelope.UserId, startError);
                return;
            }
        }
        else if (string.Equals(action, "ready", StringComparison.Ordinal))
        {
            ackEventId = syncPayload?.EventId?.Trim() ?? string.Empty;
            var request = new MarkDungeonSceneLoadReadyRequest
            {
                UserId = envelope.UserId,
                JoinToken = envelope.JoinToken,
                TransitionId = syncPayload?.TransitionId,
            };
            if (!_registry.TryMarkSceneLoadReady(envelope.InstanceId, request, out state, out string readyError))
            {
                SendError(connectionId, envelope.InstanceId, envelope.UserId, readyError);
                return;
            }
        }
        else if (string.Equals(action, "complete", StringComparison.Ordinal))
        {
            ackEventId = syncPayload?.EventId?.Trim() ?? string.Empty;
            var request = new CompleteDungeonSceneLoadRequest
            {
                UserId = envelope.UserId,
                JoinToken = envelope.JoinToken,
                TransitionId = syncPayload?.TransitionId,
            };
            if (!_registry.TryCompleteSceneLoad(envelope.InstanceId, request, out state, out string completeError))
            {
                SendError(connectionId, envelope.InstanceId, envelope.UserId, completeError);
                return;
            }
        }
        else if (!_registry.TryGetSceneLoadState(envelope.InstanceId, envelope.UserId, envelope.JoinToken, out state, out string stateError))
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, stateError);
            return;
        }

        var payload = new DungeonRealtimeSceneLoadSnapshotPayload
        {
            AckEventId = ackEventId,
            State = state,
        };
        Send(connectionId, envelope.InstanceId, envelope.UserId, DungeonRealtimeProtocol.SceneLoadSnapshotType, envelope.Sequence, payload);
    }

    private void HandleDamageSync(int connectionId, DungeonRealtimeEnvelope envelope)
    {
        DungeonRealtimeDamageSyncPayload? syncPayload = null;
        if (envelope.Payload != null)
        {
            try
            {
                syncPayload = envelope.Payload.Value.Deserialize<DungeonRealtimeDamageSyncPayload>(DungeonRealtimeProtocol.JsonOptions);
            }
            catch
            {
                SendError(connectionId, envelope.InstanceId, envelope.UserId, "伤害实时事件格式无效");
                return;
            }
        }

        long afterSequence = Math.Max(0, syncPayload?.AfterSequence ?? 0);
        string ackEventId = string.Empty;
        List<DungeonDamageEventDto> ackEvents = new();
        if (syncPayload?.HasEvent == true)
        {
            string requestedEventId = string.IsNullOrWhiteSpace(syncPayload.EventId) ? string.Empty : syncPayload.EventId.Trim();
            var request = new AddDungeonDamageEventRequest
            {
                EventId = requestedEventId,
                UserId = envelope.UserId,
                JoinToken = envelope.JoinToken,
                TargetKind = syncPayload.TargetKind,
                TargetRuntimeId = syncPayload.TargetRuntimeId,
                Damage = syncPayload.Damage,
                StunDuration = syncPayload.StunDuration,
                TargetEnemy = syncPayload.TargetEnemy,
                X = syncPayload.X,
                Y = syncPayload.Y,
                Z = syncPayload.Z,
            };
            if (!_registry.TryAddDamageEvent(envelope.InstanceId, request, out DungeonDamageEventListDto? addedEvents, out string addError))
            {
                SendError(connectionId, envelope.InstanceId, envelope.UserId, addError);
                return;
            }

            ackEventId = requestedEventId;
            ackEvents = addedEvents?.events ?? new List<DungeonDamageEventDto>();
        }

        if (!_registry.TryGetDamageEvents(envelope.InstanceId, envelope.UserId, envelope.JoinToken, afterSequence, out DungeonDamageEventListDto? events, out string eventsError))
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, eventsError);
            return;
        }

        var payload = new DungeonRealtimeDamageSnapshotPayload
        {
            AckEventId = ackEventId,
            AckEvents = ackEvents,
            Events = events?.events ?? new List<DungeonDamageEventDto>(),
        };
        Send(connectionId, envelope.InstanceId, envelope.UserId, DungeonRealtimeProtocol.DamageSnapshotType, envelope.Sequence, payload);
    }

    private void HandleRewardSync(int connectionId, DungeonRealtimeEnvelope envelope)
    {
        DungeonRealtimeRewardSyncPayload? syncPayload = null;
        if (envelope.Payload != null)
        {
            try
            {
                syncPayload = envelope.Payload.Value.Deserialize<DungeonRealtimeRewardSyncPayload>(DungeonRealtimeProtocol.JsonOptions);
            }
            catch
            {
                SendError(connectionId, envelope.InstanceId, envelope.UserId, "奖励实时事件格式无效");
                return;
            }
        }

        string ackAction = string.Empty;
        string ackTargetId = string.Empty;
        DungeonKillRewardClaimResultDto? killRewardClaim = null;
        DungeonDropPickupResultDto? dropPickup = null;
        DungeonRewardStateDto? state;
        long afterVersion = Math.Max(0, syncPayload?.AfterVersion ?? 0);
        string action = syncPayload?.Action?.Trim() ?? string.Empty;
        string actionTargetId = action switch
        {
            "openChest" => syncPayload?.ChestId?.Trim() ?? string.Empty,
            "pickupDrop" => syncPayload?.DropId?.Trim() ?? string.Empty,
            "claimKillReward" => syncPayload?.EnemyRuntimeId?.Trim() ?? string.Empty,
            "enemyKill" => syncPayload?.EnemyRuntimeId?.Trim() ?? string.Empty,
            _ => string.Empty,
        };
        if (!string.IsNullOrWhiteSpace(action))
        {
            _logger.LogInformation(
                "[OnlineReward] KCP reward action received. connection={ConnectionId} instance={InstanceId} user={UserId} action={Action} target={Target} afterVersion={AfterVersion} sequence={Sequence}",
                connectionId,
                envelope.InstanceId,
                envelope.UserId,
                action,
                actionTargetId,
                afterVersion,
                envelope.Sequence);
        }

        if (string.Equals(action, "enemyKill", StringComparison.Ordinal))
        {
            ackAction = action;
            ackTargetId = syncPayload?.EnemyRuntimeId?.Trim() ?? string.Empty;
            var request = new AddDungeonEnemyKillRequest
            {
                UserId = envelope.UserId,
                JoinToken = envelope.JoinToken,
                EnemyRuntimeId = ackTargetId,
                TargetEnemy = syncPayload?.TargetEnemy,
                X = syncPayload?.X ?? 0f,
                Y = syncPayload?.Y ?? 0f,
                Z = syncPayload?.Z ?? 0f,
            };
            if (!_registry.TryAddEnemyKill(envelope.InstanceId, request, out state, out string addError))
            {
                SendRewardError(connectionId, envelope.InstanceId, envelope.UserId, addError, ackAction, ackTargetId);
                return;
            }
        }
        else if (string.Equals(action, "openChest", StringComparison.Ordinal))
        {
            ackAction = action;
            ackTargetId = syncPayload?.ChestId?.Trim() ?? string.Empty;
            var request = new OpenDungeonChestRequest
            {
                UserId = envelope.UserId,
                JoinToken = envelope.JoinToken,
                PrefabId = syncPayload?.ChestPrefabId,
                DropCount = Math.Max(1, syncPayload?.DropCount ?? 1),
                X = syncPayload?.X ?? 0f,
                Y = syncPayload?.Y ?? 0f,
                Z = syncPayload?.Z ?? 0f,
                Yaw = syncPayload?.Yaw ?? 0f,
            };
            if (!_registry.TryOpenChest(envelope.InstanceId, ackTargetId, request, out state, out string chestError))
            {
                SendRewardError(connectionId, envelope.InstanceId, envelope.UserId, chestError, ackAction, ackTargetId);
                return;
            }
        }
        else if (string.Equals(action, "claimKillReward", StringComparison.Ordinal))
        {
            ackAction = action;
            ackTargetId = syncPayload?.EnemyRuntimeId?.Trim() ?? string.Empty;
            var request = new ClaimDungeonKillRewardRequest
            {
                UserId = envelope.UserId,
                JoinToken = envelope.JoinToken,
            };
            if (!_registry.TryClaimKillReward(envelope.InstanceId, ackTargetId, request, out killRewardClaim, out string claimError))
            {
                SendRewardError(connectionId, envelope.InstanceId, envelope.UserId, claimError, ackAction, ackTargetId);
                return;
            }

            state = killRewardClaim?.state;
        }
        else if (string.Equals(action, "pickupDrop", StringComparison.Ordinal))
        {
            ackAction = action;
            ackTargetId = syncPayload?.DropId?.Trim() ?? string.Empty;
            var request = new PickupDungeonDropRequest
            {
                UserId = envelope.UserId,
                JoinToken = envelope.JoinToken,
            };
            if (!_registry.TryPickupDrop(envelope.InstanceId, ackTargetId, request, out dropPickup, out string pickupError))
            {
                SendRewardError(connectionId, envelope.InstanceId, envelope.UserId, pickupError, ackAction, ackTargetId);
                return;
            }

            state = dropPickup?.state;
        }
        else if (!_registry.TryGetRewardStateAfterVersion(envelope.InstanceId, envelope.UserId, envelope.JoinToken, afterVersion, out state, out string stateError))
        {
            SendError(connectionId, envelope.InstanceId, envelope.UserId, stateError);
            return;
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            if (!_registry.TryGetRewardStateAfterVersion(envelope.InstanceId, envelope.UserId, envelope.JoinToken, afterVersion, out state, out string stateError))
            {
                SendError(connectionId, envelope.InstanceId, envelope.UserId, stateError);
                return;
            }

            if (killRewardClaim != null)
            {
                killRewardClaim = new DungeonKillRewardClaimResultDto
                {
                    accepted = killRewardClaim.accepted,
                    reward = killRewardClaim.reward,
                };
            }

            if (dropPickup != null)
            {
                dropPickup = new DungeonDropPickupResultDto
                {
                    accepted = dropPickup.accepted,
                    drop = dropPickup.drop,
                };
            }
        }

        var payload = new DungeonRealtimeRewardSnapshotPayload
        {
            AckAction = ackAction,
            AckTargetId = ackTargetId,
            KillRewardClaim = killRewardClaim,
            DropPickup = dropPickup,
            State = state,
        };
        if (!string.IsNullOrWhiteSpace(action) || state != null || dropPickup != null)
        {
            _logger.LogInformation(
                "[OnlineReward] KCP reward snapshot send. connection={ConnectionId} instance={InstanceId} user={UserId} ackAction={AckAction} ackTarget={AckTarget} hasDropPickup={HasDropPickup} hasState={HasState} stateVersion={StateVersion} dropCount={DropCount} sequence={Sequence}",
                connectionId,
                envelope.InstanceId,
                envelope.UserId,
                ackAction,
                ackTargetId,
                dropPickup != null,
                state != null,
                state?.version ?? 0,
                state?.drops.Count ?? 0,
                envelope.Sequence);
        }
        Send(connectionId, envelope.InstanceId, envelope.UserId, DungeonRealtimeProtocol.RewardSnapshotType, envelope.Sequence, payload);
    }

    private void SendError(int connectionId, string instanceId, string userId, string message)
    {
        var payload = DungeonRealtimeProtocol.CreateErrorPayload(message);
        Send(connectionId, instanceId, userId, DungeonRealtimeProtocol.ErrorType, 0, payload);
    }

    private void SendRewardError(int connectionId, string instanceId, string userId, string message, string ackAction, string ackTargetId)
    {
        _logger.LogWarning(
            "[OnlineReward] KCP reward error send. connection={ConnectionId} instance={InstanceId} user={UserId} ackAction={AckAction} ackTarget={AckTarget} message={Message}",
            connectionId,
            instanceId,
            userId,
            ackAction,
            ackTargetId,
            message);
        DungeonRealtimeErrorPayload payload = DungeonRealtimeProtocol.CreateErrorPayload(message);
        payload = new DungeonRealtimeErrorPayload
        {
            Message = payload.Message,
            Code = payload.Code,
            CloseReason = payload.CloseReason,
            AckAction = ackAction ?? string.Empty,
            AckTargetId = ackTargetId ?? string.Empty,
        };
        Send(connectionId, instanceId, userId, DungeonRealtimeProtocol.ErrorType, 0, payload);
    }

    private void Send<TPayload>(int connectionId, string instanceId, string userId, string type, long sequence, TPayload payload)
    {
        if (_server == null)
            return;

        JsonElement payloadElement = JsonSerializer.SerializeToElement(payload, DungeonRealtimeProtocol.JsonOptions);
        var response = new DungeonRealtimeEnvelope
        {
            Version = DungeonRealtimeProtocol.Version,
            Type = type,
            Channel = DungeonRealtimeProtocol.ChannelReliableEvent,
            InstanceId = instanceId,
            UserId = userId,
            Sequence = sequence,
            Payload = payloadElement,
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(response, DungeonRealtimeProtocol.JsonOptions);
        if (bytes.Length > DungeonRealtimeProtocol.MaxReliableMessageBytes)
        {
            _logger.LogWarning(
                "Online dungeon KCP message too large to send. connection={ConnectionId} type={Type} instance={InstanceId} user={UserId} bytes={Bytes} max={MaxBytes}",
                connectionId,
                type,
                instanceId,
                userId,
                bytes.Length,
                DungeonRealtimeProtocol.MaxReliableMessageBytes);
            return;
        }

        _server.Send(connectionId, new ArraySegment<byte>(bytes), KcpChannel.Reliable);
    }
}
