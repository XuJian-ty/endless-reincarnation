using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnlineDungeonServer.Realtime;

internal static class DungeonRealtimeProtocol
{
    public const int Version = 2;
    public const int DefaultPort = 5087;
    public const int DefaultKcpPort = 5088;
    public const int MaxDatagramBytes = 16 * 1024;

    public const string ChannelUnreliableState = "state";
    public const string ChannelReliableEvent = "event";
    public const string StateTransport = "udp-unreliable-snapshot";
    public const string EventTransport = "kcp-reliable-event";
    public const string SynchronizationMode = "state-sync-snapshot-interpolation";
    public const string AuthoritySchema = "enemy-authority-v2-movement-skill-chest-reward";
    public const string SessionLifecycleMode = "server-authoritative-session-close";
    public const string ErrorCodeGeneric = "error";
    public const string ErrorCodeSessionClosed = "sessionClosed";
    public const string SessionClosedMessagePrefix = "联机副本已关闭";

    public const string HelloType = "hello";
    public const string HelloAckType = "helloAck";
    public const string PlayerStateType = "playerState";
    public const string PlayerSnapshotType = "playerSnapshot";
    public const string EnemyAuthorityStateType = "enemyAuthorityState";
    public const string AuthorityPollType = "authorityPoll";
    public const string AuthoritySnapshotType = "authoritySnapshot";
    public const string UiPanelEventType = "uiPanelEvent";
    public const string UiPanelPollType = "uiPanelPoll";
    public const string UiPanelSnapshotType = "uiPanelSnapshot";
    public const string DamageEventType = "damageEvent";
    public const string DamagePollType = "damagePoll";
    public const string DamageSnapshotType = "damageSnapshot";
    public const string RewardEventType = "rewardEvent";
    public const string RewardPollType = "rewardPoll";
    public const string RewardSnapshotType = "rewardSnapshot";
    public const string ErrorType = "error";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static DungeonRealtimeErrorPayload CreateErrorPayload(string message)
    {
        string normalizedMessage = string.IsNullOrWhiteSpace(message) ? "实时协议错误" : message.Trim();
        var payload = new DungeonRealtimeErrorPayload
        {
            Message = normalizedMessage,
            Code = ErrorCodeGeneric,
        };

        if (TryExtractSessionClosedReason(normalizedMessage, out string closeReason))
        {
            payload = new DungeonRealtimeErrorPayload
            {
                Message = normalizedMessage,
                Code = ErrorCodeSessionClosed,
                CloseReason = closeReason,
            };
        }

        return payload;
    }

    private static bool TryExtractSessionClosedReason(string message, out string closeReason)
    {
        closeReason = string.Empty;
        if (string.IsNullOrWhiteSpace(message) || !message.StartsWith(SessionClosedMessagePrefix, StringComparison.Ordinal))
            return false;

        int separatorIndex = message.IndexOf('：');
        if (separatorIndex >= 0 && separatorIndex + 1 < message.Length)
            closeReason = message.Substring(separatorIndex + 1).Trim();

        return true;
    }
}

internal sealed class DungeonRealtimeEnvelope
{
    public int Version { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Channel { get; init; } = DungeonRealtimeProtocol.ChannelReliableEvent;
    public string InstanceId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string JoinToken { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public JsonElement? Payload { get; init; }
}

internal sealed class DungeonRealtimeHelloPayload
{
    public string DisplayName { get; init; } = string.Empty;
    public string ClientBuild { get; init; } = string.Empty;
}

internal sealed class DungeonRealtimeHelloAckPayload
{
    public int ServerProtocolVersion { get; init; }
    public string StateTransport { get; init; } = DungeonRealtimeProtocol.StateTransport;
    public string EventTransport { get; init; } = DungeonRealtimeProtocol.EventTransport;
    public string SynchronizationMode { get; init; } = DungeonRealtimeProtocol.SynchronizationMode;
    public string AuthoritySchema { get; init; } = DungeonRealtimeProtocol.AuthoritySchema;
    public string SessionLifecycleMode { get; init; } = DungeonRealtimeProtocol.SessionLifecycleMode;
    public string StateChannel { get; init; } = DungeonRealtimeProtocol.ChannelUnreliableState;
    public string EventChannel { get; init; } = DungeonRealtimeProtocol.ChannelReliableEvent;
    public DateTime UtcNow { get; init; }
}

internal sealed class DungeonRealtimeErrorPayload
{
    public string Message { get; init; } = string.Empty;
    public string Code { get; init; } = DungeonRealtimeProtocol.ErrorCodeGeneric;
    public string CloseReason { get; init; } = string.Empty;
    public string AckAction { get; init; } = string.Empty;
    public string AckTargetId { get; init; } = string.Empty;
}

internal sealed class DungeonRealtimePlayerSnapshotPayload
{
    public List<DungeonPlayerPoseDto> Poses { get; init; } = new();
}

internal sealed class DungeonRealtimeAuthoritySnapshotPayload
{
    public DungeonAuthorityStateDto? State { get; init; }
}

internal sealed class DungeonRealtimeEnemyAuthorityStatePayload
{
    public List<DungeonEnemyAuthorityStateUpsertDto> Enemies { get; init; } = new();
}

internal sealed class DungeonRealtimeUiPanelSyncPayload
{
    public bool HasEvent { get; init; }
    public string EventId { get; init; } = string.Empty;
    public string PanelName { get; init; } = string.Empty;
    public bool Open { get; init; }
    public long AfterSequence { get; init; }
}

internal sealed class DungeonRealtimeUiPanelSnapshotPayload
{
    public string AckEventId { get; init; } = string.Empty;
    public List<DungeonUiPanelEventDto> Events { get; init; } = new();
    public DungeonUiPanelStateDto? State { get; init; }
}

internal sealed class DungeonRealtimeDamageSyncPayload
{
    public bool HasEvent { get; init; }
    public string EventId { get; init; } = string.Empty;
    public string TargetKind { get; init; } = string.Empty;
    public string TargetRuntimeId { get; init; } = string.Empty;
    public float Damage { get; init; }
    public float StunDuration { get; init; }
    public DungeonEnemyAuthorityStateUpsertDto? TargetEnemy { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public long AfterSequence { get; init; }
}

internal sealed class DungeonRealtimeDamageSnapshotPayload
{
    public string AckEventId { get; init; } = string.Empty;
    public List<DungeonDamageEventDto> AckEvents { get; init; } = new();
    public List<DungeonDamageEventDto> Events { get; init; } = new();
}

internal sealed class DungeonRealtimeRewardSyncPayload
{
    public string Action { get; init; } = string.Empty;
    public string EnemyRuntimeId { get; init; } = string.Empty;
    public string ChestId { get; init; } = string.Empty;
    public string ChestPrefabId { get; init; } = string.Empty;
    public string DropId { get; init; } = string.Empty;
    public DungeonEnemyAuthorityStateUpsertDto? TargetEnemy { get; init; }
    public int DropCount { get; init; } = 1;
    public long AfterVersion { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Yaw { get; init; }
}

internal sealed class DungeonRealtimeRewardSnapshotPayload
{
    public string AckAction { get; init; } = string.Empty;
    public string AckTargetId { get; init; } = string.Empty;
    public DungeonKillRewardClaimResultDto? KillRewardClaim { get; init; }
    public DungeonDropPickupResultDto? DropPickup { get; init; }
    public DungeonRewardStateDto? State { get; init; }
}
