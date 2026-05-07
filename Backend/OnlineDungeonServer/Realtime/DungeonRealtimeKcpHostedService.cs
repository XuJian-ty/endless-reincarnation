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

        if (segment.Array == null || segment.Count <= 0 || segment.Count > DungeonRealtimeProtocol.MaxDatagramBytes)
            return;

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

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.UiPanelEventType, StringComparison.Ordinal)
            || string.Equals(envelope.Type, DungeonRealtimeProtocol.UiPanelPollType, StringComparison.Ordinal))
        {
            HandleUiPanelSync(connectionId, envelope);
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
        Send(connectionId, envelope.InstanceId, envelope.UserId, DungeonRealtimeProtocol.RewardSnapshotType, envelope.Sequence, payload);
    }

    private void SendError(int connectionId, string instanceId, string userId, string message)
    {
        var payload = DungeonRealtimeProtocol.CreateErrorPayload(message);
        Send(connectionId, instanceId, userId, DungeonRealtimeProtocol.ErrorType, 0, payload);
    }

    private void SendRewardError(int connectionId, string instanceId, string userId, string message, string ackAction, string ackTargetId)
    {
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
        if (bytes.Length > DungeonRealtimeProtocol.MaxDatagramBytes)
            return;

        _server.Send(connectionId, new ArraySegment<byte>(bytes), KcpChannel.Reliable);
    }
}
