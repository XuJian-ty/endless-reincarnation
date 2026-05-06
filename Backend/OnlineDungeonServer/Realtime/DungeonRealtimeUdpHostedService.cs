using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace OnlineDungeonServer.Realtime;

internal sealed class DungeonRealtimeUdpHostedService : BackgroundService
{
    private readonly DungeonInstanceRegistry _registry;
    private readonly ILogger<DungeonRealtimeUdpHostedService> _logger;
    private readonly int _port;

    public DungeonRealtimeUdpHostedService(
        DungeonInstanceRegistry registry,
        IConfiguration configuration,
        ILogger<DungeonRealtimeUdpHostedService> logger)
    {
        _registry = registry;
        _logger = logger;
        _port = configuration.GetValue("Realtime:UdpPort", DungeonRealtimeProtocol.DefaultPort);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IRealtimeDatagramTransport transport = new UdpRealtimeDatagramTransport(_port);
        _logger.LogInformation("Online dungeon realtime UDP endpoint listening on 0.0.0.0:{Port}", _port);

        while (!stoppingToken.IsCancellationRequested)
        {
            RealtimeDatagram result;
            try
            {
                result = await transport.ReceiveAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Realtime UDP receive failed.");
                continue;
            }

            _ = HandleDatagramAsync(transport, result, stoppingToken);
        }
    }

    private async Task HandleDatagramAsync(IRealtimeDatagramTransport transport, RealtimeDatagram result, CancellationToken cancellationToken)
    {
        DungeonRealtimeEnvelope? envelope;
        try
        {
            string json = Encoding.UTF8.GetString(result.Buffer);
            envelope = JsonSerializer.Deserialize<DungeonRealtimeEnvelope>(json, DungeonRealtimeProtocol.JsonOptions);
        }
        catch (Exception ex)
        {
            await SendErrorAsync(transport, result.RemoteEndPoint, string.Empty, string.Empty, "实时协议数据格式无效", cancellationToken);
            _logger.LogDebug(ex, "Invalid realtime datagram received from {RemoteEndPoint}", result.RemoteEndPoint);
            return;
        }

        if (envelope == null)
        {
            await SendErrorAsync(transport, result.RemoteEndPoint, string.Empty, string.Empty, "实时协议数据为空", cancellationToken);
            return;
        }

        if (envelope.Version != DungeonRealtimeProtocol.Version)
        {
            await SendErrorAsync(transport, result.RemoteEndPoint, envelope.InstanceId, envelope.UserId, "实时协议版本不匹配", cancellationToken);
            return;
        }

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.PlayerStateType, StringComparison.Ordinal))
        {
            await HandlePlayerStateAsync(transport, result.RemoteEndPoint, envelope, cancellationToken);
            return;
        }

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.EnemyAuthorityStateType, StringComparison.Ordinal))
        {
            await HandleEnemyAuthorityStateAsync(transport, result.RemoteEndPoint, envelope, cancellationToken);
            return;
        }

        if (string.Equals(envelope.Type, DungeonRealtimeProtocol.AuthorityPollType, StringComparison.Ordinal))
        {
            await HandleAuthorityPollAsync(transport, result.RemoteEndPoint, envelope, cancellationToken);
            return;
        }

        await SendErrorAsync(transport, result.RemoteEndPoint, envelope.InstanceId, envelope.UserId, "UDP实时状态通道不处理该消息类型", cancellationToken);
    }

    private async Task HandleHelloAsync(
        IRealtimeDatagramTransport transport,
        IPEndPoint remoteEndPoint,
        DungeonRealtimeEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var validateRequest = new ValidateDungeonParticipantRequest
        {
            UserId = envelope.UserId,
            JoinToken = envelope.JoinToken,
        };
        if (!_registry.TryValidateParticipant(envelope.InstanceId, validateRequest, out _, out string error))
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, error, cancellationToken);
            return;
        }

        var payload = new DungeonRealtimeHelloAckPayload
        {
            ServerProtocolVersion = DungeonRealtimeProtocol.Version,
            UtcNow = DateTime.UtcNow,
        };
        await SendAsync(
            transport,
            remoteEndPoint,
            envelope.InstanceId,
            envelope.UserId,
            DungeonRealtimeProtocol.HelloAckType,
            DungeonRealtimeProtocol.ChannelReliableEvent,
            envelope.Sequence,
            payload,
            cancellationToken);
    }

    private async Task HandlePlayerStateAsync(
        IRealtimeDatagramTransport transport,
        IPEndPoint remoteEndPoint,
        DungeonRealtimeEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (envelope.Payload == null)
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, "玩家实时状态为空", cancellationToken);
            return;
        }

        UpsertDungeonPlayerPoseRequest? request;
        try
        {
            request = envelope.Payload.Value.Deserialize<UpsertDungeonPlayerPoseRequest>(DungeonRealtimeProtocol.JsonOptions);
        }
        catch
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, "玩家实时状态格式无效", cancellationToken);
            return;
        }

        if (request == null)
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, "玩家实时状态为空", cancellationToken);
            return;
        }

        if (!string.Equals(request.UserId, envelope.UserId, StringComparison.Ordinal)
            || !string.Equals(request.JoinToken, envelope.JoinToken, StringComparison.Ordinal))
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, "玩家实时状态身份不匹配", cancellationToken);
            return;
        }

        if (!_registry.TryUpsertPlayerPose(envelope.InstanceId, request, out DungeonPlayerPoseListDto? poses, out string error))
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, error, cancellationToken);
            return;
        }

        var payload = new DungeonRealtimePlayerSnapshotPayload
        {
            Poses = poses?.poses ?? new List<DungeonPlayerPoseDto>(),
        };
        await SendAsync(
            transport,
            remoteEndPoint,
            envelope.InstanceId,
            envelope.UserId,
            DungeonRealtimeProtocol.PlayerSnapshotType,
            DungeonRealtimeProtocol.ChannelUnreliableState,
            envelope.Sequence,
            payload,
            cancellationToken);
    }

    private async Task HandleEnemyAuthorityStateAsync(
        IRealtimeDatagramTransport transport,
        IPEndPoint remoteEndPoint,
        DungeonRealtimeEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (envelope.Payload == null)
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, "敌人权威状态为空", cancellationToken);
            return;
        }

        DungeonRealtimeEnemyAuthorityStatePayload? request;
        try
        {
            request = envelope.Payload.Value.Deserialize<DungeonRealtimeEnemyAuthorityStatePayload>(DungeonRealtimeProtocol.JsonOptions);
        }
        catch
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, "敌人权威状态格式无效", cancellationToken);
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
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, error, cancellationToken);
            return;
        }

        var payload = new DungeonRealtimeAuthoritySnapshotPayload
        {
            State = state,
        };
        await SendAsync(
            transport,
            remoteEndPoint,
            envelope.InstanceId,
            envelope.UserId,
            DungeonRealtimeProtocol.AuthoritySnapshotType,
            DungeonRealtimeProtocol.ChannelUnreliableState,
            envelope.Sequence,
            payload,
            cancellationToken);
    }

    private async Task HandleAuthorityPollAsync(
        IRealtimeDatagramTransport transport,
        IPEndPoint remoteEndPoint,
        DungeonRealtimeEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!_registry.TryGetAuthorityState(envelope.InstanceId, envelope.UserId, envelope.JoinToken, out DungeonAuthorityStateDto? state, out string error))
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, error, cancellationToken);
            return;
        }

        var payload = new DungeonRealtimeAuthoritySnapshotPayload
        {
            State = state,
        };
        await SendAsync(
            transport,
            remoteEndPoint,
            envelope.InstanceId,
            envelope.UserId,
            DungeonRealtimeProtocol.AuthoritySnapshotType,
            DungeonRealtimeProtocol.ChannelUnreliableState,
            envelope.Sequence,
            payload,
            cancellationToken);
    }

    private async Task HandleUiPanelSyncAsync(
        IRealtimeDatagramTransport transport,
        IPEndPoint remoteEndPoint,
        DungeonRealtimeEnvelope envelope,
        CancellationToken cancellationToken)
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
                await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, "面板实时事件格式无效", cancellationToken);
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
                await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, addError, cancellationToken);
                return;
            }

            ackEventId = requestedEventId;
        }

        if (!_registry.TryGetUiPanelEvents(envelope.InstanceId, envelope.UserId, envelope.JoinToken, afterSequence, out DungeonUiPanelEventListDto? events, out string eventsError))
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, eventsError, cancellationToken);
            return;
        }

        if (!_registry.TryGetUiPanelState(envelope.InstanceId, envelope.UserId, envelope.JoinToken, out DungeonUiPanelStateDto? state, out string stateError))
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, stateError, cancellationToken);
            return;
        }

        var payload = new DungeonRealtimeUiPanelSnapshotPayload
        {
            AckEventId = ackEventId,
            Events = events?.events ?? new List<DungeonUiPanelEventDto>(),
            State = state,
        };
        await SendAsync(
            transport,
            remoteEndPoint,
            envelope.InstanceId,
            envelope.UserId,
            DungeonRealtimeProtocol.UiPanelSnapshotType,
            DungeonRealtimeProtocol.ChannelReliableEvent,
            envelope.Sequence,
            payload,
            cancellationToken);
    }

    private async Task HandleDamageSyncAsync(
        IRealtimeDatagramTransport transport,
        IPEndPoint remoteEndPoint,
        DungeonRealtimeEnvelope envelope,
        CancellationToken cancellationToken)
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
                await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, "伤害实时事件格式无效", cancellationToken);
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
                await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, addError, cancellationToken);
                return;
            }

            ackEventId = requestedEventId;
            ackEvents = addedEvents?.events ?? new List<DungeonDamageEventDto>();
        }

        if (!_registry.TryGetDamageEvents(envelope.InstanceId, envelope.UserId, envelope.JoinToken, afterSequence, out DungeonDamageEventListDto? events, out string eventsError))
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, eventsError, cancellationToken);
            return;
        }

        var payload = new DungeonRealtimeDamageSnapshotPayload
        {
            AckEventId = ackEventId,
            AckEvents = ackEvents,
            Events = events?.events ?? new List<DungeonDamageEventDto>(),
        };
        await SendAsync(
            transport,
            remoteEndPoint,
            envelope.InstanceId,
            envelope.UserId,
            DungeonRealtimeProtocol.DamageSnapshotType,
            DungeonRealtimeProtocol.ChannelReliableEvent,
            envelope.Sequence,
            payload,
            cancellationToken);
    }

    private async Task HandleRewardSyncAsync(
        IRealtimeDatagramTransport transport,
        IPEndPoint remoteEndPoint,
        DungeonRealtimeEnvelope envelope,
        CancellationToken cancellationToken)
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
                await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, "奖励实时事件格式无效", cancellationToken);
                return;
            }
        }

        string ackAction = string.Empty;
        string ackTargetId = string.Empty;
        DungeonKillRewardClaimResultDto? killRewardClaim = null;
        DungeonDropPickupResultDto? dropPickup = null;
        DungeonRewardStateDto? state;
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
                X = syncPayload?.X ?? 0f,
                Y = syncPayload?.Y ?? 0f,
                Z = syncPayload?.Z ?? 0f,
            };
            if (!_registry.TryAddEnemyKill(envelope.InstanceId, request, out state, out string addError))
            {
                await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, addError, cancellationToken);
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
                await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, claimError, cancellationToken);
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
                await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, pickupError, cancellationToken);
                return;
            }

            state = dropPickup?.state;
        }
        else if (!_registry.TryGetRewardState(envelope.InstanceId, envelope.UserId, envelope.JoinToken, out state, out string stateError))
        {
            await SendErrorAsync(transport, remoteEndPoint, envelope.InstanceId, envelope.UserId, stateError, cancellationToken);
            return;
        }

        var payload = new DungeonRealtimeRewardSnapshotPayload
        {
            AckAction = ackAction,
            AckTargetId = ackTargetId,
            KillRewardClaim = killRewardClaim,
            DropPickup = dropPickup,
            State = state,
        };
        await SendAsync(
            transport,
            remoteEndPoint,
            envelope.InstanceId,
            envelope.UserId,
            DungeonRealtimeProtocol.RewardSnapshotType,
            DungeonRealtimeProtocol.ChannelReliableEvent,
            envelope.Sequence,
            payload,
            cancellationToken);
    }

    private static Task SendErrorAsync(
        IRealtimeDatagramTransport transport,
        IPEndPoint remoteEndPoint,
        string instanceId,
        string userId,
        string message,
        CancellationToken cancellationToken)
    {
        var payload = new DungeonRealtimeErrorPayload
        {
            Message = string.IsNullOrWhiteSpace(message) ? "实时协议错误" : message,
        };
        return SendAsync(
            transport,
            remoteEndPoint,
            instanceId,
            userId,
            DungeonRealtimeProtocol.ErrorType,
            DungeonRealtimeProtocol.ChannelReliableEvent,
            0,
            payload,
            cancellationToken);
    }

    private static async Task SendAsync<TPayload>(
        IRealtimeDatagramTransport transport,
        IPEndPoint remoteEndPoint,
        string instanceId,
        string userId,
        string type,
        string channel,
        long sequence,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        JsonElement payloadElement = JsonSerializer.SerializeToElement(payload, DungeonRealtimeProtocol.JsonOptions);
        var response = new DungeonRealtimeEnvelope
        {
            Version = DungeonRealtimeProtocol.Version,
            Type = type,
            Channel = channel,
            InstanceId = instanceId,
            UserId = userId,
            Sequence = sequence,
            Payload = payloadElement,
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(response, DungeonRealtimeProtocol.JsonOptions);
        if (bytes.Length > DungeonRealtimeProtocol.MaxDatagramBytes)
            return;

        await transport.SendAsync(bytes, remoteEndPoint, cancellationToken);
    }

    private interface IRealtimeDatagramTransport : IDisposable
    {
        Task<RealtimeDatagram> ReceiveAsync(CancellationToken cancellationToken);

        Task SendAsync(byte[] bytes, IPEndPoint remoteEndPoint, CancellationToken cancellationToken);
    }

    private readonly struct RealtimeDatagram
    {
        public readonly byte[] Buffer;
        public readonly IPEndPoint RemoteEndPoint;

        public RealtimeDatagram(byte[] buffer, IPEndPoint remoteEndPoint)
        {
            Buffer = buffer;
            RemoteEndPoint = remoteEndPoint;
        }
    }

    private sealed class UdpRealtimeDatagramTransport : IRealtimeDatagramTransport
    {
        private readonly UdpClient _udpClient;

        public UdpRealtimeDatagramTransport(int port)
        {
            _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        }

        public async Task<RealtimeDatagram> ReceiveAsync(CancellationToken cancellationToken)
        {
            UdpReceiveResult result = await _udpClient.ReceiveAsync(cancellationToken);
            return new RealtimeDatagram(result.Buffer, result.RemoteEndPoint);
        }

        public Task SendAsync(byte[] bytes, IPEndPoint remoteEndPoint, CancellationToken cancellationToken)
        {
            return _udpClient.SendAsync(bytes, remoteEndPoint, cancellationToken).AsTask();
        }

        public void Dispose()
        {
            _udpClient.Dispose();
        }
    }
}
