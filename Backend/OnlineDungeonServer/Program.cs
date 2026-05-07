using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using OnlineDungeonServer.Realtime;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5086");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});
builder.Services.AddSingleton<DungeonRewardConfigStore>();
builder.Services.AddSingleton<DungeonInstanceRegistry>();
builder.Services.AddHostedService<DungeonRealtimeUdpHostedService>();
builder.Services.AddHostedService<DungeonRealtimeKcpHostedService>();

var app = builder.Build();

app.MapGet("/api/health", (DungeonInstanceRegistry registry) =>
{
    return Results.Ok(ApiResponse<object>.Ok(new
    {
        status = "ok",
        utcNow = DateTime.UtcNow,
        activeInstances = registry.ActiveInstanceCount,
        realtimeUdpPort = registry.RealtimeUdpPort,
        realtimeKcpPort = registry.RealtimeKcpPort,
    }, "联机副本服务已启动"));
});

app.MapPost("/api/dungeons", (CreateDungeonInstanceRequest request, DungeonInstanceRegistry registry) =>
{
    if (!registry.TryCreate(request, out DungeonInstanceDto? instance, out string error))
        return Results.BadRequest(ApiResponse<DungeonInstanceDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonInstanceDto>.Ok(instance!, "联机副本已创建"));
});

app.MapGet("/api/dungeons/{instanceId}", (string instanceId, DungeonInstanceRegistry registry) =>
{
    if (!registry.TryGet(instanceId, out DungeonInstanceDto? instance, out string error))
        return Results.NotFound(ApiResponse<DungeonInstanceDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonInstanceDto>.Ok(instance!, "联机副本获取成功"));
});

app.MapPost("/api/dungeons/{instanceId}/participants/join", (
    string instanceId,
    JoinDungeonInstanceRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryJoin(instanceId, request, out DungeonInstanceDto? instance, out string error))
        return Results.BadRequest(ApiResponse<DungeonInstanceDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonInstanceDto>.Ok(instance!, "玩家已加入联机副本"));
});

app.MapPost("/api/dungeons/{instanceId}/participants/leave", (
    string instanceId,
    LeaveDungeonInstanceRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryLeave(instanceId, request, out DungeonInstanceDto? instance, out string error))
        return Results.BadRequest(ApiResponse<DungeonInstanceDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonInstanceDto>.Ok(instance!, "玩家已离开联机副本"));
});

app.MapPost("/api/dungeons/{instanceId}/participants/validate", (
    string instanceId,
    ValidateDungeonParticipantRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryValidateParticipant(instanceId, request, out DungeonParticipantDto? participant, out string error))
        return Results.BadRequest(ApiResponse<DungeonParticipantDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonParticipantDto>.Ok(participant!, "副本参与者验证成功"));
});

app.MapPost("/api/dungeons/{instanceId}/players/pose", (
    string instanceId,
    UpsertDungeonPlayerPoseRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryUpsertPlayerPose(instanceId, request, out DungeonPlayerPoseListDto? poses, out string error))
        return Results.BadRequest(ApiResponse<DungeonPlayerPoseListDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonPlayerPoseListDto>.Ok(poses!, "玩家姿态已同步"));
});

app.MapGet("/api/dungeons/{instanceId}/players/poses", (
    string instanceId,
    string userId,
    string joinToken,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryGetPlayerPoses(instanceId, userId, joinToken, out DungeonPlayerPoseListDto? poses, out string error))
        return Results.BadRequest(ApiResponse<DungeonPlayerPoseListDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonPlayerPoseListDto>.Ok(poses!, "玩家姿态获取成功"));
});

app.MapPost("/api/dungeons/{instanceId}/world-snapshot", (
    string instanceId,
    UpsertDungeonWorldSnapshotRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryUpsertWorldSnapshot(instanceId, request, out DungeonWorldSnapshotDto? snapshot, out string error))
        return Results.BadRequest(ApiResponse<DungeonWorldSnapshotDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonWorldSnapshotDto>.Ok(snapshot!, "副本世界快照已同步"));
});

app.MapGet("/api/dungeons/{instanceId}/world-snapshot", (
    string instanceId,
    string userId,
    string joinToken,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryGetWorldSnapshot(instanceId, userId, joinToken, out DungeonWorldSnapshotDto? snapshot, out string error))
        return Results.BadRequest(ApiResponse<DungeonWorldSnapshotDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonWorldSnapshotDto>.Ok(snapshot!, "副本世界快照获取成功"));
});

app.MapGet("/api/dungeons/{instanceId}/authority-state", (
    string instanceId,
    string userId,
    string joinToken,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryGetAuthorityState(instanceId, userId, joinToken, out DungeonAuthorityStateDto? state, out string error))
        return Results.BadRequest(ApiResponse<DungeonAuthorityStateDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonAuthorityStateDto>.Ok(state!, "副本权威状态获取成功"));
});

app.MapPost("/api/dungeons/{instanceId}/damage-events", (
    string instanceId,
    AddDungeonDamageEventRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryAddDamageEvent(instanceId, request, out DungeonDamageEventListDto? events, out string error))
        return Results.BadRequest(ApiResponse<DungeonDamageEventListDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonDamageEventListDto>.Ok(events!, "副本伤害事件已记录"));
});

app.MapGet("/api/dungeons/{instanceId}/damage-events", (
    string instanceId,
    string userId,
    string joinToken,
    long afterSequence,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryGetDamageEvents(instanceId, userId, joinToken, afterSequence, out DungeonDamageEventListDto? events, out string error))
        return Results.BadRequest(ApiResponse<DungeonDamageEventListDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonDamageEventListDto>.Ok(events!, "副本伤害事件获取成功"));
});

app.MapPost("/api/dungeons/{instanceId}/ui-panel-events", (
    string instanceId,
    AddDungeonUiPanelEventRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryAddUiPanelEvent(instanceId, request, out DungeonUiPanelEventListDto? events, out string error))
        return Results.BadRequest(ApiResponse<DungeonUiPanelEventListDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonUiPanelEventListDto>.Ok(events!, "副本面板事件已记录"));
});

app.MapGet("/api/dungeons/{instanceId}/ui-panel-events", (
    string instanceId,
    string userId,
    string joinToken,
    long afterSequence,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryGetUiPanelEvents(instanceId, userId, joinToken, afterSequence, out DungeonUiPanelEventListDto? events, out string error))
        return Results.BadRequest(ApiResponse<DungeonUiPanelEventListDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonUiPanelEventListDto>.Ok(events!, "副本面板事件获取成功"));
});

app.MapGet("/api/dungeons/{instanceId}/ui-panel-state", (
    string instanceId,
    string userId,
    string joinToken,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryGetUiPanelState(instanceId, userId, joinToken, out DungeonUiPanelStateDto? state, out string error))
        return Results.BadRequest(ApiResponse<DungeonUiPanelStateDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonUiPanelStateDto>.Ok(state!, "副本面板状态获取成功"));
});

app.MapPost("/api/dungeons/{instanceId}/enemy-kills", (
    string instanceId,
    AddDungeonEnemyKillRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryAddEnemyKill(instanceId, request, out DungeonRewardStateDto? state, out string error))
        return Results.BadRequest(ApiResponse<DungeonRewardStateDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonRewardStateDto>.Ok(state!, "副本击杀奖励已记录"));
});

app.MapPost("/api/dungeons/{instanceId}/chests/{chestId}/open", (
    string instanceId,
    string chestId,
    OpenDungeonChestRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryOpenChest(instanceId, chestId, request, out DungeonRewardStateDto? state, out string error))
        return Results.BadRequest(ApiResponse<DungeonRewardStateDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonRewardStateDto>.Ok(state!, "副本宝箱已开启"));
});

app.MapPost("/api/dungeons/{instanceId}/enemy-kills/{enemyRuntimeId}/claim", (
    string instanceId,
    string enemyRuntimeId,
    ClaimDungeonKillRewardRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryClaimKillReward(instanceId, enemyRuntimeId, request, out DungeonKillRewardClaimResultDto? result, out string error))
        return Results.BadRequest(ApiResponse<DungeonKillRewardClaimResultDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonKillRewardClaimResultDto>.Ok(result!, "副本击杀奖励已领取"));
});

app.MapGet("/api/dungeons/{instanceId}/reward-state", (
    string instanceId,
    string userId,
    string joinToken,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryGetRewardState(instanceId, userId, joinToken, out DungeonRewardStateDto? state, out string error))
        return Results.BadRequest(ApiResponse<DungeonRewardStateDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonRewardStateDto>.Ok(state!, "副本奖励状态获取成功"));
});

app.MapPost("/api/dungeons/{instanceId}/drops/{dropId}/pickup", (
    string instanceId,
    string dropId,
    PickupDungeonDropRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryPickupDrop(instanceId, dropId, request, out DungeonDropPickupResultDto? result, out string error))
        return Results.BadRequest(ApiResponse<DungeonDropPickupResultDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonDropPickupResultDto>.Ok(result!, "副本掉落拾取已裁决"));
});

app.MapPost("/api/dungeons/{instanceId}/close", (
    string instanceId,
    CloseDungeonInstanceRequest request,
    DungeonInstanceRegistry registry) =>
{
    if (!registry.TryClose(instanceId, request, out DungeonInstanceDto? instance, out string error))
        return Results.BadRequest(ApiResponse<DungeonInstanceDto>.Fail(error));

    return Results.Ok(ApiResponse<DungeonInstanceDto>.Ok(instance!, "联机副本已关闭"));
});

app.Run();

internal sealed class DungeonInstanceRegistry
{
    private static readonly TimeSpan HostDisconnectTimeout = TimeSpan.FromSeconds(20);
    private readonly ConcurrentDictionary<string, DungeonInstanceRecord> _instances = new();
    private readonly DungeonRewardConfigStore _rewardConfigStore;
    private readonly int _realtimeUdpPort;
    private readonly int _realtimeKcpPort;

    public DungeonInstanceRegistry(DungeonRewardConfigStore rewardConfigStore, IConfiguration configuration)
    {
        _rewardConfigStore = rewardConfigStore;
        _realtimeUdpPort = configuration.GetValue("Realtime:UdpPort", DungeonRealtimeProtocol.DefaultPort);
        _realtimeKcpPort = configuration.GetValue("Realtime:KcpPort", DungeonRealtimeProtocol.DefaultKcpPort);
    }

    public int ActiveInstanceCount => _instances.Values.Count(instance => instance.Status == DungeonInstanceStatus.Active);
    public int RealtimeUdpPort => _realtimeUdpPort;
    public int RealtimeKcpPort => _realtimeKcpPort;

    public bool TryCreate(CreateDungeonInstanceRequest request, out DungeonInstanceDto? instance, out string error)
    {
        instance = null;
        error = string.Empty;

        string templateOwnerUserId = NormalizeRequired(request.TemplateOwnerUserId);
        if (string.IsNullOrWhiteSpace(templateOwnerUserId))
        {
            error = "缺少副本模板来源玩家标识";
            return false;
        }

        if (request.LevelIndex <= 0)
        {
            error = "关卡编号无效";
            return false;
        }

        string instanceId = string.IsNullOrWhiteSpace(request.InstanceId)
            ? Guid.NewGuid().ToString("N")
            : request.InstanceId.Trim();
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            error = "副本标识无效";
            return false;
        }

        DateTime now = DateTime.UtcNow;
        var record = new DungeonInstanceRecord
        {
            InstanceId = instanceId,
            TemplateOwnerUserId = templateOwnerUserId,
            LevelIndex = request.LevelIndex,
            Difficulty = Math.Max(1, request.Difficulty),
            Seed = request.Seed,
            MaxPlayers = Math.Clamp(request.MaxPlayers <= 0 ? 4 : request.MaxPlayers, 1, 8),
            Status = DungeonInstanceStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        if (!string.IsNullOrWhiteSpace(request.TemplateSaveId))
            record.TemplateSaveId = request.TemplateSaveId.Trim();

        if (!_instances.TryAdd(record.InstanceId, record))
        {
            error = "副本标识已存在";
            return false;
        }

        instance = ToDto(record);
        return true;
    }

    public bool TryGet(string instanceId, out DungeonInstanceDto? instance, out string error)
    {
        instance = null;
        error = string.Empty;

        if (!_instances.TryGetValue(NormalizeRequired(instanceId), out DungeonInstanceRecord? record))
        {
            error = "联机副本不存在";
            return false;
        }

        instance = ToDto(record);
        return true;
    }

    public bool TryJoin(string instanceId, JoinDungeonInstanceRequest request, out DungeonInstanceDto? instance, out string error)
    {
        instance = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (record.Status != DungeonInstanceStatus.Active)
            {
                error = "联机副本已关闭";
                return false;
            }

            string userId = NormalizeRequired(request.UserId);
            if (string.IsNullOrWhiteSpace(userId))
            {
                error = "缺少玩家标识";
                return false;
            }

            string joinToken = NormalizeRequired(request.JoinToken);
            if (string.IsNullOrWhiteSpace(joinToken))
            {
                error = "缺少副本加入令牌";
                return false;
            }

            if (!record.Participants.ContainsKey(userId) && record.Participants.Count >= record.MaxPlayers)
            {
                error = "联机副本人数已满";
                return false;
            }

            DateTime now = DateTime.UtcNow;
            record.Participants[userId] = new DungeonParticipantRecord
            {
                UserId = userId,
                SaveId = request.SaveId?.Trim() ?? string.Empty,
                DisplayName = request.DisplayName?.Trim() ?? string.Empty,
                JoinToken = joinToken,
                IsConnected = true,
                JoinedAtUtc = now,
                LastSeenAtUtc = now,
            };
            record.UpdatedAtUtc = now;
            instance = ToDto(record);
            return true;
        }
    }

    public bool TryLeave(string instanceId, LeaveDungeonInstanceRequest request, out DungeonInstanceDto? instance, out string error)
    {
        instance = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            string userId = NormalizeRequired(request.UserId);
            if (string.IsNullOrWhiteSpace(userId))
            {
                error = "缺少玩家标识";
                return false;
            }

            if (record.Participants.TryGetValue(userId, out DungeonParticipantRecord? participant))
            {
                participant.IsConnected = false;
                participant.LastSeenAtUtc = DateTime.UtcNow;
            }

            record.UpdatedAtUtc = DateTime.UtcNow;
            instance = ToDto(record);
            return true;
        }
    }

    public bool TryClose(string instanceId, CloseDungeonInstanceRequest request, out DungeonInstanceDto? instance, out string error)
    {
        instance = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            record.Status = DungeonInstanceStatus.Closed;
            record.CloseReason = string.IsNullOrWhiteSpace(request.Reason) ? "manual" : request.Reason.Trim();
            record.UpdatedAtUtc = DateTime.UtcNow;
            instance = ToDto(record);
            return true;
        }
    }

    public bool TryValidateParticipant(string instanceId, ValidateDungeonParticipantRequest request, out DungeonParticipantDto? participant, out string error)
    {
        participant = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            string userId = NormalizeRequired(request.UserId);
            string joinToken = NormalizeRequired(request.JoinToken);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(joinToken))
            {
                error = "缺少玩家标识或副本加入令牌";
                return false;
            }

            if (!record.Participants.TryGetValue(userId, out DungeonParticipantRecord? foundParticipant))
            {
                error = "玩家不属于该联机副本";
                return false;
            }

            if (!string.Equals(foundParticipant.JoinToken, joinToken, StringComparison.Ordinal))
            {
                error = "副本加入令牌无效";
                return false;
            }

            if (!TryEnsureInstanceActiveForParticipant(record, userId, DateTime.UtcNow, out error))
                return false;

            foundParticipant.IsConnected = true;
            foundParticipant.LastSeenAtUtc = DateTime.UtcNow;
            record.UpdatedAtUtc = foundParticipant.LastSeenAtUtc;
            participant = ToDto(foundParticipant);
            return true;
        }
    }

    public bool TryUpsertPlayerPose(string instanceId, UpsertDungeonPlayerPoseRequest request, out DungeonPlayerPoseListDto? poses, out string error)
    {
        poses = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, request.UserId, request.JoinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            DateTime now = DateTime.UtcNow;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = now;
            record.PlayerPoses[participant.UserId] = new DungeonPlayerPoseRecord
            {
                UserId = participant.UserId,
                DisplayName = participant.DisplayName,
                X = request.X,
                Y = request.Y,
                Z = request.Z,
                Yaw = request.Yaw,
                AimActive = request.AimActive,
                AimLayerActive = request.AimLayerActive,
                AimPitch = Math.Clamp(request.AimPitch, -89f, 89f),
                AimDirectionX = request.AimDirectionX,
                AimDirectionY = request.AimDirectionY,
                AimDirectionZ = request.AimDirectionZ,
                MoveSpeed = Math.Clamp(request.MoveSpeed, 0f, 1f),
                MoveX = Math.Clamp(request.MoveX, -1f, 1f),
                MoveY = Math.Clamp(request.MoveY, -1f, 1f),
                Action = Math.Clamp(request.Action, 0, 255),
                ActionId = NormalizeRequired(request.ActionId) ?? string.Empty,
                SkillEffectId = NormalizeRequired(request.SkillEffectId) ?? string.Empty,
                ActionSequence = Math.Max(0, request.ActionSequence),
                ActionElapsedSeconds = Math.Max(0f, request.ActionElapsedSeconds),
                ActionNormalizedProgress = request.ActionNormalizedProgress,
                AttackMode = Math.Clamp(request.AttackMode, 0, 255),
                HasMeleeWeapon = request.HasMeleeWeapon,
                HasRangedWeapon = request.HasRangedWeapon,
                Defense = Math.Max(0f, request.Defense),
                DamageReduce = Math.Clamp(request.DamageReduce, 0f, 1f),
                CurrentHp = Math.Max(0f, request.CurrentHp),
                MaxHp = Math.Max(1f, request.MaxHp),
                IsDead = request.IsDead || request.CurrentHp <= 0f,
                UpdatedAtUtc = now,
            };
            participant.MaxHp = Math.Max(1f, request.MaxHp);
            if (request.IsDead && !participant.IsDead)
            {
                participant.CurrentHp = 0f;
                participant.IsDead = true;
                participant.DiedAtUtc = now;
            }
            else if (!participant.IsDead)
            {
                participant.CurrentHp = Math.Clamp(Math.Max(0f, request.CurrentHp), 0f, participant.MaxHp);
                participant.IsDead = participant.CurrentHp <= 0f;
                if (participant.IsDead && participant.DiedAtUtc == null)
                    participant.DiedAtUtc = now;
            }
            record.UpdatedAtUtc = now;
            poses = ToPoseListDto(record, participant.UserId);
            return true;
        }
    }

    public bool TryGetPlayerPoses(string instanceId, string userId, string joinToken, out DungeonPlayerPoseListDto? poses, out string error)
    {
        poses = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, userId, joinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = DateTime.UtcNow;
            poses = ToPoseListDto(record, participant.UserId);
            return true;
        }
    }

    public bool TryUpsertWorldSnapshot(string instanceId, UpsertDungeonWorldSnapshotRequest request, out DungeonWorldSnapshotDto? snapshot, out string error)
    {
        snapshot = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, request.UserId, request.JoinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            if (string.IsNullOrWhiteSpace(request.SnapshotJson))
            {
                error = "副本世界快照内容为空";
                return false;
            }

            if (!TryHydrateAuthorityStateFromSnapshot(record, request.SnapshotJson, out error))
                return false;
            if (!TryHydrateChestStateFromSnapshot(record, request.SnapshotJson, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            DateTime now = DateTime.UtcNow;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = now;
            record.WorldSnapshot = new DungeonWorldSnapshotRecord
            {
                OwnerUserId = participant.UserId,
                SceneName = NormalizeRequired(request.SceneName),
                SnapshotJson = request.SnapshotJson,
                Version = record.WorldSnapshot == null ? 1 : record.WorldSnapshot.Version + 1,
                UpdatedAtUtc = now,
            };
            record.UpdatedAtUtc = now;
            snapshot = ToDto(record.WorldSnapshot, record);
            return true;
        }
    }

    public bool TryGetWorldSnapshot(string instanceId, string userId, string joinToken, out DungeonWorldSnapshotDto? snapshot, out string error)
    {
        snapshot = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, userId, joinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            if (record.WorldSnapshot == null)
            {
                error = "副本世界快照尚未就绪";
                return false;
            }

            DungeonParticipantRecord participant = participantOrNull!;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = DateTime.UtcNow;
            snapshot = ToDto(record.WorldSnapshot, record);
            return true;
        }
    }

    public bool TryGetAuthorityState(
        string instanceId,
        string userId,
        string joinToken,
        out DungeonAuthorityStateDto? state,
        out string error)
    {
        state = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, userId, joinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = DateTime.UtcNow;
            state = ToAuthorityStateDto(record);
            return true;
        }
    }

    public bool TryUpsertEnemyAuthorityState(
        string instanceId,
        string userId,
        string joinToken,
        List<DungeonEnemyAuthorityStateUpsertDto>? enemies,
        out DungeonAuthorityStateDto? state,
        out string error)
    {
        state = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, userId, joinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = DateTime.UtcNow;

            HashSet<string> runtimeIds = new(StringComparer.Ordinal);
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    DungeonEnemyAuthorityStateUpsertDto incoming = enemies[i];
                    if (incoming == null || string.IsNullOrWhiteSpace(incoming.runtimeId))
                        continue;

                    string runtimeId = incoming.runtimeId.Trim();
                    runtimeIds.Add(runtimeId);
                    bool isNewEnemy = false;
                    if (!record.Enemies.TryGetValue(runtimeId, out DungeonEnemyStateRecord? enemy))
                    {
                        enemy = new DungeonEnemyStateRecord
                        {
                            RuntimeId = runtimeId,
                        };
                        record.Enemies[runtimeId] = enemy;
                        isNewEnemy = true;
                    }

                    enemy.EnemyId = incoming.enemyId?.Trim() ?? string.Empty;
                    enemy.EnemyType = incoming.enemyType;
                    enemy.X = incoming.x;
                    enemy.Y = incoming.y;
                    enemy.Z = incoming.z;
                    enemy.Yaw = incoming.yaw;
                    enemy.CurrentPoise = Math.Max(0f, incoming.currentPoise);
                    enemy.CountsAsLevelBoss = incoming.countsAsLevelBoss;
                    enemy.ShowCombatHealthBar = incoming.showCombatHealthBar;
                    enemy.MoveBlend = Math.Clamp(incoming.moveBlend, 0f, 1f);
                    enemy.MoveForward = Math.Clamp(incoming.moveForward, -1f, 1f);
                    enemy.MoveStrafe = Math.Clamp(incoming.moveStrafe, -1f, 1f);
                    enemy.ActiveSkillSequence = incoming.activeSkillSequence;
                    enemy.ActiveSkillSlot = incoming.activeSkillSlot;
                    enemy.ActiveSkillId = incoming.activeSkillId?.Trim() ?? string.Empty;
                    enemy.ActiveSkillAnimationTrigger = incoming.activeSkillAnimationTrigger?.Trim() ?? string.Empty;
                    enemy.HasActiveSkillTarget = incoming.hasActiveSkillTarget;
                    enemy.ActiveSkillTargetX = incoming.activeSkillTargetX;
                    enemy.ActiveSkillTargetY = incoming.activeSkillTargetY;
                    enemy.ActiveSkillTargetZ = incoming.activeSkillTargetZ;
                    if (isNewEnemy)
                    {
                        enemy.CurrentHp = Math.Max(0f, incoming.currentHp);
                        enemy.IsDead = incoming.isDead || enemy.CurrentHp <= 0f;
                        if (enemy.IsDead)
                            enemy.DiedAtUtc = DateTime.UtcNow;
                    }

                    enemy.UpdatedAtUtc = DateTime.UtcNow;
                }
            }

            foreach (string runtimeId in record.Enemies.Keys.ToList())
            {
                if (!runtimeIds.Contains(runtimeId))
                    record.Enemies.Remove(runtimeId);
            }

            record.AuthorityInitialized = true;
            record.AuthorityVersion++;
            record.UpdatedAtUtc = DateTime.UtcNow;
            state = ToAuthorityStateDto(record);
            return true;
        }
    }

    public bool TryAddUiPanelEvent(string instanceId, AddDungeonUiPanelEventRequest request, out DungeonUiPanelEventListDto? events, out string error)
    {
        events = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, request.UserId, request.JoinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            string eventId = NormalizeRequired(request.EventId);
            if (string.IsNullOrWhiteSpace(eventId))
            {
                error = "缺少面板事件标识";
                return false;
            }

            string panelName = NormalizeRequired(request.PanelName);
            if (string.IsNullOrWhiteSpace(panelName))
            {
                error = "缺少面板名称";
                return false;
            }

            DungeonParticipantRecord participant = participantOrNull!;
            DateTime now = DateTime.UtcNow;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = now;
            if (!record.UiPanelEventIds.Contains(eventId))
            {
                record.UiPanelEventIds.Add(eventId);
                DungeonUiPanelEventRecord panelEvent = new DungeonUiPanelEventRecord
                {
                    EventId = eventId,
                    Sequence = ++record.NextUiPanelEventSequence,
                    SourceUserId = participant.UserId,
                    PanelName = panelName,
                    Open = request.Open,
                    CreatedAtUtc = now,
                };
                record.UiPanelEvents.Add(panelEvent);
                TrimUiPanelEvents(record);
                if (request.Open)
                    record.OpenPanelName = panelName;
                else if (string.Equals(record.OpenPanelName, panelName, StringComparison.Ordinal))
                    record.OpenPanelName = string.Empty;
                record.UiPanelStateVersion++;
                record.UiPanelStateUpdatedByUserId = participant.UserId;
                record.UiPanelStateUpdatedAtUtc = now;
            }

            record.UpdatedAtUtc = now;
            events = ToUiPanelEventListDto(record, participant.UserId, Math.Max(0, record.NextUiPanelEventSequence - 1));
            return true;
        }
    }

    public bool TryGetUiPanelEvents(
        string instanceId,
        string userId,
        string joinToken,
        long afterSequence,
        out DungeonUiPanelEventListDto? events,
        out string error)
    {
        events = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, userId, joinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = DateTime.UtcNow;
            events = ToUiPanelEventListDto(record, participant.UserId, Math.Max(0, afterSequence));
            return true;
        }
    }

    public bool TryGetUiPanelState(
        string instanceId,
        string userId,
        string joinToken,
        out DungeonUiPanelStateDto? state,
        out string error)
    {
        state = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, userId, joinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = DateTime.UtcNow;
            state = ToUiPanelStateDto(record);
            return true;
        }
    }

    public bool TryStartSceneLoad(
        string instanceId,
        StartDungeonSceneLoadRequest request,
        out DungeonSceneLoadStateDto? state,
        out string error)
    {
        state = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, request.UserId, request.JoinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            if (!string.Equals(participant.UserId, record.TemplateOwnerUserId, StringComparison.Ordinal))
            {
                error = "只有被援助方可以发起联机场景加载";
                return false;
            }

            string transitionId = NormalizeRequired(request.TransitionId);
            if (string.IsNullOrWhiteSpace(transitionId))
            {
                error = "缺少场景加载会话标识";
                return false;
            }

            string sceneName = NormalizeRequired(request.SceneName);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                error = "缺少场景名称";
                return false;
            }

            DateTime now = DateTime.UtcNow;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = now;
            if (record.SceneLoadActive)
            {
                state = ToSceneLoadStateDto(record);
                return true;
            }

            if (!record.SceneLoadActive || !string.Equals(record.SceneLoadTransitionId, transitionId, StringComparison.Ordinal))
            {
                record.SceneLoadActive = true;
                record.SceneLoadReleased = false;
                record.SceneLoadTransitionId = transitionId;
                record.SceneLoadSceneName = sceneName;
                record.SceneLoadBossId = NormalizeRequired(request.BossId);
                record.SceneLoadBossDisplayName = NormalizeRequired(request.BossDisplayName);
                record.SceneLoadReadyUserIds.Clear();
                record.SceneLoadCompletedUserIds.Clear();
                record.SceneLoadReleaseAtUtc = null;
                record.SceneLoadVersion++;
            }

            record.UpdatedAtUtc = now;
            state = ToSceneLoadStateDto(record);
            return true;
        }
    }

    public bool TryMarkSceneLoadReady(
        string instanceId,
        MarkDungeonSceneLoadReadyRequest request,
        out DungeonSceneLoadStateDto? state,
        out string error)
    {
        state = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, request.UserId, request.JoinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            string transitionId = NormalizeRequired(request.TransitionId);
            if (string.IsNullOrWhiteSpace(transitionId))
            {
                error = "缺少场景加载会话标识";
                return false;
            }

            if (!record.SceneLoadActive || !string.Equals(record.SceneLoadTransitionId, transitionId, StringComparison.Ordinal))
            {
                error = "场景加载会话不存在或已过期";
                return false;
            }

            DungeonParticipantRecord participant = participantOrNull!;
            DateTime now = DateTime.UtcNow;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = now;
            if (record.SceneLoadReadyUserIds.Add(participant.UserId))
                record.SceneLoadVersion++;

            if (!record.SceneLoadReleased && AreAllSceneLoadParticipantsReady(record))
            {
                record.SceneLoadReleased = true;
                record.SceneLoadReleaseAtUtc = DateTime.UtcNow.AddMilliseconds(500);
                record.SceneLoadVersion++;
            }

            record.UpdatedAtUtc = now;
            state = ToSceneLoadStateDto(record);
            return true;
        }
    }

    public bool TryCompleteSceneLoad(
        string instanceId,
        CompleteDungeonSceneLoadRequest request,
        out DungeonSceneLoadStateDto? state,
        out string error)
    {
        state = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, request.UserId, request.JoinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            string transitionId = NormalizeRequired(request.TransitionId);
            if (string.IsNullOrWhiteSpace(transitionId))
            {
                error = "缺少场景加载会话标识";
                return false;
            }

            if (!record.SceneLoadActive || !string.Equals(record.SceneLoadTransitionId, transitionId, StringComparison.Ordinal))
            {
                state = ToSceneLoadStateDto(record);
                return true;
            }

            DungeonParticipantRecord participant = participantOrNull!;
            DateTime now = DateTime.UtcNow;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = now;
            if (record.SceneLoadCompletedUserIds.Add(participant.UserId))
                record.SceneLoadVersion++;

            if (AreAllSceneLoadParticipantsComplete(record))
            {
                record.SceneLoadActive = false;
                record.SceneLoadReleased = false;
                record.SceneLoadVersion++;
            }

            record.UpdatedAtUtc = now;
            state = ToSceneLoadStateDto(record);
            return true;
        }
    }

    public bool TryGetSceneLoadState(
        string instanceId,
        string userId,
        string joinToken,
        out DungeonSceneLoadStateDto? state,
        out string error)
    {
        state = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, userId, joinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = DateTime.UtcNow;
            state = ToSceneLoadStateDto(record);
            return true;
        }
    }

    public bool TryAddEnemyKill(string instanceId, AddDungeonEnemyKillRequest request, out DungeonRewardStateDto? state, out string error)
    {
        state = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, request.UserId, request.JoinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            string enemyRuntimeId = NormalizeRequired(request.EnemyRuntimeId);
            if (string.IsNullOrWhiteSpace(enemyRuntimeId))
            {
                error = "缺少击杀敌人标识";
                return false;
            }

            DungeonParticipantRecord participant = participantOrNull!;
            DateTime now = DateTime.UtcNow;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = now;
            if (!TryRegisterDamageTargetEnemy(record, enemyRuntimeId, request.TargetEnemy, out error))
                return false;

            if (!record.KillRewards.ContainsKey(enemyRuntimeId))
            {
                if (!record.Enemies.TryGetValue(enemyRuntimeId, out DungeonEnemyStateRecord? enemy))
                {
                    error = "副本敌人权威状态不存在，无法生成击杀奖励";
                    return false;
                }

                if (!_rewardConfigStore.TryBuildReward(record, enemy, request, out DungeonRewardProposal rewardProposal, out error))
                    return false;

                long nextRewardVersion = record.RewardStateVersion + 1;
                DungeonKillRewardRecord reward = new DungeonKillRewardRecord
                {
                    EnemyRuntimeId = enemyRuntimeId,
                    KillerUserId = participant.UserId,
                    Exp = rewardProposal.Exp,
                    Gold = rewardProposal.Gold,
                    TalentPoints = rewardProposal.TalentPoints,
                    X = request.X,
                    Y = request.Y,
                    Z = request.Z,
                    Claimed = false,
                    RewardVersion = nextRewardVersion,
                    CreatedAtUtc = now,
                };
                record.KillRewards[enemyRuntimeId] = reward;
                AddDungeonDrops(record, enemyRuntimeId, rewardProposal.Drops, now, nextRewardVersion);
                record.RewardStateVersion = nextRewardVersion;
            }

            record.UpdatedAtUtc = now;
            state = ToRewardStateDto(record);
            return true;
        }
    }

    public bool TryClaimKillReward(
        string instanceId,
        string enemyRuntimeId,
        ClaimDungeonKillRewardRequest request,
        out DungeonKillRewardClaimResultDto? result,
        out string error)
    {
        result = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, request.UserId, request.JoinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            string normalizedEnemyRuntimeId = NormalizeRequired(enemyRuntimeId);
            if (string.IsNullOrWhiteSpace(normalizedEnemyRuntimeId) || !record.KillRewards.TryGetValue(normalizedEnemyRuntimeId, out DungeonKillRewardRecord? reward))
            {
                error = "副本击杀奖励不存在";
                return false;
            }

            DungeonParticipantRecord participant = participantOrNull!;
            DateTime now = DateTime.UtcNow;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = now;
            if (!string.Equals(reward.KillerUserId, participant.UserId, StringComparison.Ordinal))
            {
                error = "只有击杀者可以领取该奖励";
                return false;
            }

            bool accepted = string.Equals(reward.KillerUserId, participant.UserId, StringComparison.Ordinal) && reward.Claimed;
            if (!reward.Claimed)
            {
                long nextRewardVersion = record.RewardStateVersion + 1;
                reward.Claimed = true;
                reward.ClaimedAtUtc = now;
                reward.ClaimVersion = nextRewardVersion;
                record.RewardStateVersion = nextRewardVersion;
                record.UpdatedAtUtc = now;
                accepted = true;
            }

            result = new DungeonKillRewardClaimResultDto
            {
                accepted = accepted,
                reward = ToDto(reward),
                state = ToRewardStateDto(record),
            };
            return true;
        }
    }

    public bool TryGetRewardState(
        string instanceId,
        string userId,
        string joinToken,
        out DungeonRewardStateDto? state,
        out string error)
    {
        state = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, userId, joinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = DateTime.UtcNow;
            state = ToRewardStateDto(record);
            return true;
        }
    }

    public bool TryGetRewardStateAfterVersion(
        string instanceId,
        string userId,
        string joinToken,
        long afterVersion,
        out DungeonRewardStateDto? state,
        out string error)
    {
        state = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, userId, joinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = DateTime.UtcNow;
            state = ToRewardStateDto(record, Math.Max(0, afterVersion));
            return true;
        }
    }

    public bool TryOpenChest(
        string instanceId,
        string chestId,
        OpenDungeonChestRequest request,
        out DungeonRewardStateDto? state,
        out string error)
    {
        state = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, request.UserId, request.JoinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            string normalizedChestId = NormalizeRequired(chestId);
            if (string.IsNullOrWhiteSpace(normalizedChestId))
            {
                error = "缺少宝箱标识";
                return false;
            }

            DungeonParticipantRecord participant = participantOrNull!;
            DateTime now = DateTime.UtcNow;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = now;
            if (!record.Chests.TryGetValue(normalizedChestId, out DungeonChestStateRecord? chest))
            {
                chest = new DungeonChestStateRecord
                {
                    ChestId = normalizedChestId,
                    PrefabId = NormalizeRequired(request.PrefabId),
                    X = request.X,
                    Y = request.Y,
                    Z = request.Z,
                    Yaw = request.Yaw,
                    UpdatedAtUtc = now,
                };
                record.Chests[normalizedChestId] = chest;
                record.AuthorityInitialized = true;
            }

            if (!chest.Opened)
            {
                chest.Opened = true;
                chest.OpenedByUserId = participant.UserId;
                chest.OpenedAtUtc = now;
                chest.UpdatedAtUtc = now;
                if (string.IsNullOrWhiteSpace(chest.PrefabId))
                    chest.PrefabId = NormalizeRequired(request.PrefabId);
                if (!_rewardConfigStore.TryBuildChestDrops(record, request, normalizedChestId, out List<DungeonDropProposal>? chestDrops, out error))
                    return false;

                long nextRewardVersion = record.RewardStateVersion + 1;
                AddDungeonDrops(record, normalizedChestId, chestDrops, now, nextRewardVersion);
                record.RewardStateVersion = nextRewardVersion;
                record.AuthorityVersion++;
            }

            record.UpdatedAtUtc = now;
            state = ToRewardStateDto(record);
            return true;
        }
    }

    public bool TryPickupDrop(
        string instanceId,
        string dropId,
        PickupDungeonDropRequest request,
        out DungeonDropPickupResultDto? result,
        out string error)
    {
        result = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, request.UserId, request.JoinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            string normalizedDropId = NormalizeRequired(dropId);
            if (string.IsNullOrWhiteSpace(normalizedDropId) || !record.Drops.TryGetValue(normalizedDropId, out DungeonDropRecord? drop))
            {
                error = "副本掉落不存在";
                return false;
            }

            DungeonParticipantRecord participant = participantOrNull!;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = DateTime.UtcNow;
            if (!drop.PickedUp)
            {
                long nextRewardVersion = record.RewardStateVersion + 1;
                drop.PickedUp = true;
                drop.PickedUpByUserId = participant.UserId;
                drop.PickedUpAtUtc = participant.LastSeenAtUtc;
                drop.PickedUpVersion = nextRewardVersion;
                record.RewardStateVersion = nextRewardVersion;
                record.UpdatedAtUtc = participant.LastSeenAtUtc;
            }

            result = new DungeonDropPickupResultDto
            {
                accepted = string.Equals(drop.PickedUpByUserId, participant.UserId, StringComparison.Ordinal),
                drop = ToDto(drop),
                state = ToRewardStateDto(record),
            };
            return true;
        }
    }

    public bool TryAddDamageEvent(string instanceId, AddDungeonDamageEventRequest request, out DungeonDamageEventListDto? events, out string error)
    {
        events = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, request.UserId, request.JoinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            string eventId = NormalizeRequired(request.EventId);
            if (string.IsNullOrWhiteSpace(eventId))
            {
                error = "缺少伤害事件标识";
                return false;
            }

            string targetRuntimeId = NormalizeRequired(request.TargetRuntimeId);
            if (string.IsNullOrWhiteSpace(targetRuntimeId))
            {
                error = "缺少伤害目标标识";
                return false;
            }

            string targetKind = NormalizeRequired(request.TargetKind);
            if (string.IsNullOrWhiteSpace(targetKind))
                targetKind = "enemy";

            if (request.Damage <= 0f)
            {
                error = "伤害数值无效";
                return false;
            }

            DungeonParticipantRecord participant = participantOrNull!;
            DateTime now = DateTime.UtcNow;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = now;
            if (!record.DamageEventIds.Contains(eventId))
            {
                float acceptedDamage = Math.Max(0f, request.Damage);
                float targetRemainingHp = 0f;
                bool targetDied = false;
                bool targetShowCombatHealthBar = false;
                if (string.Equals(targetKind, "enemy", StringComparison.Ordinal))
                {
                    if (!TryRegisterDamageTargetEnemy(record, targetRuntimeId, request.TargetEnemy, out error))
                        return false;

                    if (!TryApplyEnemyDamage(record, targetRuntimeId, acceptedDamage, out targetRemainingHp, out targetDied, out targetShowCombatHealthBar, out error))
                        return false;
                }
                else if (string.Equals(targetKind, "player", StringComparison.Ordinal))
                {
                    if (!TryApplyPlayerDamage(record, targetRuntimeId, acceptedDamage, out targetRemainingHp, out targetDied, out error))
                        return false;
                }

                record.DamageEventIds.Add(eventId);
                DungeonDamageEventRecord damageEvent = new DungeonDamageEventRecord
                {
                    EventId = eventId,
                    Sequence = ++record.NextDamageEventSequence,
                    SourceUserId = participant.UserId,
                    TargetKind = targetKind,
                    TargetRuntimeId = targetRuntimeId,
                    Damage = acceptedDamage,
                    StunDuration = Math.Max(0f, request.StunDuration),
                    TargetRemainingHp = targetRemainingHp,
                    TargetDied = targetDied,
                    TargetShowCombatHealthBar = targetShowCombatHealthBar,
                    X = request.X,
                    Y = request.Y,
                    Z = request.Z,
                    CreatedAtUtc = now,
                };
                record.DamageEvents.Add(damageEvent);
                TrimDamageEvents(record);
            }

            record.UpdatedAtUtc = now;
            events = new DungeonDamageEventListDto
            {
                events = record.DamageEvents
                    .Where(damageEvent => string.Equals(damageEvent.EventId, eventId, StringComparison.Ordinal))
                    .Select(ToDto)
                    .ToList(),
            };
            return true;
        }
    }

    public bool TryGetDamageEvents(
        string instanceId,
        string userId,
        string joinToken,
        long afterSequence,
        out DungeonDamageEventListDto? events,
        out string error)
    {
        events = null;
        error = string.Empty;

        if (!TryGetRecord(instanceId, out DungeonInstanceRecord record, out error))
            return false;

        lock (record.SyncRoot)
        {
            if (!TryValidateParticipantCore(record, userId, joinToken, out DungeonParticipantRecord? participantOrNull, out error))
                return false;

            DungeonParticipantRecord participant = participantOrNull!;
            participant.IsConnected = true;
            participant.LastSeenAtUtc = DateTime.UtcNow;
            events = ToDamageEventListDto(record, participant.UserId, Math.Max(0, afterSequence));
            return true;
        }
    }

    private bool TryGetRecord(string instanceId, out DungeonInstanceRecord record, out string error)
    {
        error = string.Empty;
        if (_instances.TryGetValue(NormalizeRequired(instanceId), out DungeonInstanceRecord? foundRecord))
        {
            record = foundRecord;
            return true;
        }

        record = null!;
        error = "联机副本不存在";
        return false;
    }

    private static bool TryValidateParticipantCore(
        DungeonInstanceRecord record,
        string? userId,
        string? joinToken,
        out DungeonParticipantRecord? participant,
        out string error)
    {
        participant = null;
        error = string.Empty;

        string normalizedUserId = NormalizeRequired(userId);
        string normalizedJoinToken = NormalizeRequired(joinToken);
        if (string.IsNullOrWhiteSpace(normalizedUserId) || string.IsNullOrWhiteSpace(normalizedJoinToken))
        {
            error = "缺少玩家标识或副本加入令牌";
            return false;
        }

        if (!record.Participants.TryGetValue(normalizedUserId, out DungeonParticipantRecord? foundParticipant))
        {
            error = "玩家不属于该联机副本";
            return false;
        }

        if (!string.Equals(foundParticipant.JoinToken, normalizedJoinToken, StringComparison.Ordinal))
        {
            error = "副本加入令牌无效";
            return false;
        }

        if (!TryEnsureInstanceActiveForParticipant(record, normalizedUserId, DateTime.UtcNow, out error))
            return false;

        participant = foundParticipant;
        return true;
    }

    private static bool TryEnsureInstanceActiveForParticipant(
        DungeonInstanceRecord record,
        string requesterUserId,
        DateTime now,
        out string error)
    {
        error = string.Empty;

        if (record.Status != DungeonInstanceStatus.Active)
        {
            error = BuildInstanceClosedError(record);
            return false;
        }

        if (string.Equals(requesterUserId, record.TemplateOwnerUserId, StringComparison.Ordinal))
            return true;

        if (record.Participants.TryGetValue(record.TemplateOwnerUserId, out DungeonParticipantRecord? hostParticipant) &&
            now - hostParticipant.LastSeenAtUtc > HostDisconnectTimeout)
        {
            record.Status = DungeonInstanceStatus.Closed;
            record.CloseReason = "host_disconnected";
            record.UpdatedAtUtc = now;
            error = BuildInstanceClosedError(record);
            return false;
        }

        return true;
    }

    private static string BuildInstanceClosedError(DungeonInstanceRecord record)
    {
        return string.IsNullOrWhiteSpace(record.CloseReason)
            ? "联机副本已关闭"
            : $"联机副本已关闭：{record.CloseReason}";
    }

    private DungeonInstanceDto ToDto(DungeonInstanceRecord record)
    {
        lock (record.SyncRoot)
        {
            return new DungeonInstanceDto
            {
                instanceId = record.InstanceId,
                templateOwnerUserId = record.TemplateOwnerUserId,
                templateSaveId = record.TemplateSaveId,
                levelIndex = record.LevelIndex,
                difficulty = record.Difficulty,
                seed = record.Seed,
                maxPlayers = record.MaxPlayers,
                realtimeUdpPort = _realtimeUdpPort,
                realtimeKcpPort = _realtimeKcpPort,
                status = record.Status.ToString().ToLowerInvariant(),
                closeReason = record.CloseReason,
                createdAtUtc = record.CreatedAtUtc,
                updatedAtUtc = record.UpdatedAtUtc,
                participants = record.Participants.Values
                    .OrderBy(participant => participant.JoinedAtUtc)
                    .Select(ToDto)
                    .ToList(),
            };
        }
    }

    private static DungeonParticipantDto ToDto(DungeonParticipantRecord record)
    {
        return new DungeonParticipantDto
        {
            userId = record.UserId,
            saveId = record.SaveId,
            displayName = record.DisplayName,
            isConnected = record.IsConnected,
            currentHp = record.CurrentHp,
            maxHp = record.MaxHp,
            isDead = record.IsDead,
            joinedAtUtc = record.JoinedAtUtc,
            lastSeenAtUtc = record.LastSeenAtUtc,
            diedAtUtc = record.DiedAtUtc,
        };
    }

    private static bool TryApplyEnemyDamage(
        DungeonInstanceRecord record,
        string runtimeId,
        float damage,
        out float remainingHp,
        out bool died,
        out bool showCombatHealthBar,
        out string error)
    {
        remainingHp = 0f;
        died = false;
        showCombatHealthBar = false;
        error = string.Empty;

        if (!record.AuthorityInitialized)
        {
            error = "副本权威状态尚未初始化";
            return false;
        }

        if (!record.Enemies.TryGetValue(runtimeId, out DungeonEnemyStateRecord? enemy))
        {
            error = "伤害目标敌人不存在";
            return false;
        }

        if (enemy.IsDead)
        {
            remainingHp = 0f;
            died = false;
            enemy.ShowCombatHealthBar = false;
            return true;
        }

        float acceptedDamage = Math.Max(0f, damage);
        enemy.CurrentHp = Math.Max(0f, enemy.CurrentHp - acceptedDamage);
        if (enemy.CurrentHp <= 0f)
        {
            enemy.CurrentHp = 0f;
            enemy.IsDead = true;
            enemy.DiedAtUtc = DateTime.UtcNow;
            died = true;
        }

        enemy.ShowCombatHealthBar = !enemy.IsDead;
        showCombatHealthBar = enemy.ShowCombatHealthBar;
        enemy.UpdatedAtUtc = DateTime.UtcNow;
        remainingHp = enemy.CurrentHp;
        record.AuthorityVersion++;
        return true;
    }

    private static bool TryRegisterDamageTargetEnemy(
        DungeonInstanceRecord record,
        string targetRuntimeId,
        DungeonEnemyAuthorityStateUpsertDto? incoming,
        out string error)
    {
        error = string.Empty;
        if (incoming == null)
            return true;

        string incomingRuntimeId = NormalizeRequired(incoming.runtimeId);
        if (string.IsNullOrWhiteSpace(incomingRuntimeId))
            incomingRuntimeId = targetRuntimeId;

        if (!string.Equals(incomingRuntimeId, targetRuntimeId, StringComparison.Ordinal))
        {
            error = "伤害目标敌人信息与目标标识不一致";
            return false;
        }

        bool isNewEnemy = false;
        if (!record.Enemies.TryGetValue(targetRuntimeId, out DungeonEnemyStateRecord? enemy))
        {
            enemy = new DungeonEnemyStateRecord
            {
                RuntimeId = targetRuntimeId,
            };
            record.Enemies[targetRuntimeId] = enemy;
            isNewEnemy = true;
        }

        enemy.EnemyId = incoming.enemyId?.Trim() ?? string.Empty;
        enemy.EnemyType = incoming.enemyType;
        enemy.X = incoming.x;
        enemy.Y = incoming.y;
        enemy.Z = incoming.z;
        enemy.Yaw = incoming.yaw;
        enemy.CurrentPoise = Math.Max(0f, incoming.currentPoise);
        enemy.CountsAsLevelBoss = incoming.countsAsLevelBoss;
        enemy.ShowCombatHealthBar = incoming.showCombatHealthBar;
        enemy.MoveBlend = Math.Clamp(incoming.moveBlend, 0f, 1f);
        enemy.MoveForward = Math.Clamp(incoming.moveForward, -1f, 1f);
        enemy.MoveStrafe = Math.Clamp(incoming.moveStrafe, -1f, 1f);
        if (isNewEnemy)
        {
            enemy.CurrentHp = Math.Max(0f, incoming.currentHp);
            enemy.IsDead = incoming.isDead || enemy.CurrentHp <= 0f;
            if (enemy.IsDead)
                enemy.DiedAtUtc = DateTime.UtcNow;
        }

        enemy.UpdatedAtUtc = DateTime.UtcNow;
        record.AuthorityInitialized = true;
        if (isNewEnemy)
            record.AuthorityVersion++;
        return true;
    }

    private static bool TryApplyPlayerDamage(
        DungeonInstanceRecord record,
        string userId,
        float damage,
        out float remainingHp,
        out bool died,
        out string error)
    {
        remainingHp = 0f;
        died = false;
        error = string.Empty;

        if (!record.Participants.TryGetValue(userId, out DungeonParticipantRecord? participant))
        {
            error = "伤害目标玩家不属于该联机副本";
            return false;
        }

        if (participant.IsDead)
        {
            remainingHp = 0f;
            return true;
        }

        participant.CurrentHp = Math.Max(0f, participant.CurrentHp - Math.Max(0f, damage));
        if (participant.CurrentHp <= 0f)
        {
            participant.CurrentHp = 0f;
            participant.IsDead = true;
            participant.DiedAtUtc = DateTime.UtcNow;
            died = true;
        }

        participant.LastSeenAtUtc = DateTime.UtcNow;
        remainingHp = participant.CurrentHp;
        record.AuthorityVersion++;
        return true;
    }

    private static bool TryHydrateAuthorityStateFromSnapshot(DungeonInstanceRecord record, string snapshotJson, out string error)
    {
        error = string.Empty;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(snapshotJson);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("enemies", out JsonElement enemiesElement) ||
                enemiesElement.ValueKind != JsonValueKind.Array)
            {
                error = "副本快照缺少敌人状态";
                return false;
            }

            bool authorityWasInitialized = record.AuthorityInitialized;
            HashSet<string> snapshotRuntimeIds = new(StringComparer.Ordinal);
            foreach (JsonElement enemyElement in enemiesElement.EnumerateArray())
            {
                string runtimeId = GetStringProperty(enemyElement, "runtimeId");
                if (string.IsNullOrWhiteSpace(runtimeId))
                    continue;

                snapshotRuntimeIds.Add(runtimeId);
                float currentHp = Math.Max(0f, GetFloatProperty(enemyElement, "currentHp"));
                float currentPoise = Math.Max(0f, GetFloatProperty(enemyElement, "currentPoise"));
                bool wasDead = currentHp <= 0f;
                bool isNewEnemy = false;
                if (!record.Enemies.TryGetValue(runtimeId, out DungeonEnemyStateRecord? enemy))
                {
                    enemy = new DungeonEnemyStateRecord
                    {
                        RuntimeId = runtimeId,
                    };
                    record.Enemies[runtimeId] = enemy;
                    isNewEnemy = true;
                }

                enemy.EnemyId = GetStringProperty(enemyElement, "id");
                enemy.EnemyType = GetIntProperty(enemyElement, "enemyType");
                enemy.X = GetFloatProperty(enemyElement, "x");
                enemy.Y = GetFloatProperty(enemyElement, "y");
                enemy.Z = GetFloatProperty(enemyElement, "z");
                enemy.Yaw = GetFloatProperty(enemyElement, "yaw");
                enemy.CurrentPoise = currentPoise;
                enemy.CountsAsLevelBoss = GetBoolProperty(enemyElement, "countsAsLevelBoss");
                if (!authorityWasInitialized || isNewEnemy)
                {
                    enemy.CurrentHp = currentHp;
                    enemy.IsDead = wasDead;
                }

                enemy.UpdatedAtUtc = DateTime.UtcNow;
                if (enemy.IsDead && enemy.DiedAtUtc == null)
                    enemy.DiedAtUtc = enemy.UpdatedAtUtc;
            }

            foreach (string runtimeId in record.Enemies.Keys.ToList())
            {
                if (!snapshotRuntimeIds.Contains(runtimeId))
                    record.Enemies.Remove(runtimeId);
            }
        }

        record.AuthorityInitialized = true;
        record.AuthorityVersion++;
        return true;
    }

    private static bool TryHydrateChestStateFromSnapshot(DungeonInstanceRecord record, string snapshotJson, out string error)
    {
        error = string.Empty;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(snapshotJson);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("chests", out JsonElement chestsElement) ||
                chestsElement.ValueKind != JsonValueKind.Array)
            {
                return true;
            }

            HashSet<string> snapshotChestIds = new(StringComparer.Ordinal);
            HashSet<string> openedChestIds = ReadOpenedChestIds(document.RootElement);
            foreach (JsonElement chestElement in chestsElement.EnumerateArray())
            {
                string chestId = GetStringProperty(chestElement, "snapshotId");
                if (string.IsNullOrWhiteSpace(chestId))
                    continue;

                snapshotChestIds.Add(chestId);
                if (!record.Chests.TryGetValue(chestId, out DungeonChestStateRecord? chest))
                {
                    chest = new DungeonChestStateRecord
                    {
                        ChestId = chestId,
                    };
                    record.Chests[chestId] = chest;
                }

                chest.PrefabId = GetStringProperty(chestElement, "prefabId");
                chest.X = GetFloatProperty(chestElement, "x");
                chest.Y = GetFloatProperty(chestElement, "y");
                chest.Z = GetFloatProperty(chestElement, "z");
                chest.Yaw = GetFloatProperty(chestElement, "yaw");
                if (openedChestIds.Contains(chestId))
                    chest.Opened = true;
                chest.UpdatedAtUtc = DateTime.UtcNow;
            }

            foreach (string chestId in openedChestIds)
            {
                if (!record.Chests.TryGetValue(chestId, out DungeonChestStateRecord? chest))
                {
                    chest = new DungeonChestStateRecord
                    {
                        ChestId = chestId,
                    };
                    record.Chests[chestId] = chest;
                }

                chest.Opened = true;
                chest.UpdatedAtUtc = DateTime.UtcNow;
            }

            foreach (string chestId in record.Chests.Keys.ToList())
            {
                if (!snapshotChestIds.Contains(chestId) && !record.Chests[chestId].Opened)
                    record.Chests.Remove(chestId);
            }
        }

        return true;
    }

    private static DungeonPlayerPoseListDto ToPoseListDto(DungeonInstanceRecord record, string requesterUserId)
    {
        DateTime cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(3);
        return new DungeonPlayerPoseListDto
        {
            poses = record.PlayerPoses.Values
                .Where(pose => pose.UpdatedAtUtc >= cutoff && !string.Equals(pose.UserId, requesterUserId, StringComparison.Ordinal))
                .OrderBy(pose => pose.UserId)
                .Select(ToDto)
                .ToList(),
        };
    }

    private static DungeonPlayerPoseDto ToDto(DungeonPlayerPoseRecord record)
    {
        return new DungeonPlayerPoseDto
        {
            userId = record.UserId,
            displayName = record.DisplayName,
            x = record.X,
            y = record.Y,
            z = record.Z,
            yaw = record.Yaw,
            aimActive = record.AimActive,
            aimLayerActive = record.AimLayerActive,
            aimPitch = record.AimPitch,
            aimDirectionX = record.AimDirectionX,
            aimDirectionY = record.AimDirectionY,
            aimDirectionZ = record.AimDirectionZ,
            moveSpeed = record.MoveSpeed,
            moveX = record.MoveX,
            moveY = record.MoveY,
            action = record.Action,
            actionId = record.ActionId,
            skillEffectId = record.SkillEffectId,
            actionSequence = record.ActionSequence,
            actionElapsedSeconds = record.ActionElapsedSeconds,
            actionNormalizedProgress = record.ActionNormalizedProgress,
            attackMode = record.AttackMode,
            hasMeleeWeapon = record.HasMeleeWeapon,
            hasRangedWeapon = record.HasRangedWeapon,
            defense = record.Defense,
            damageReduce = record.DamageReduce,
            currentHp = record.CurrentHp,
            maxHp = record.MaxHp,
            isDead = record.IsDead,
            updatedAtUtc = record.UpdatedAtUtc,
        };
    }

    private static DungeonAuthorityStateDto ToAuthorityStateDto(DungeonInstanceRecord record)
    {
        return new DungeonAuthorityStateDto
        {
            version = record.AuthorityVersion,
            initialized = record.AuthorityInitialized,
            enemies = record.Enemies.Values
                .OrderBy(enemy => enemy.RuntimeId)
                .Select(ToDto)
                .ToList(),
            chests = record.Chests.Values
                .OrderBy(chest => chest.ChestId)
                .Select(ToDto)
                .ToList(),
            rewardStateVersion = record.RewardStateVersion,
        };
    }

    private static DungeonEnemyStateDto ToDto(DungeonEnemyStateRecord record)
    {
        return new DungeonEnemyStateDto
        {
            runtimeId = record.RuntimeId,
            enemyId = record.EnemyId,
            enemyType = record.EnemyType,
            x = record.X,
            y = record.Y,
            z = record.Z,
            yaw = record.Yaw,
            currentHp = record.CurrentHp,
            currentPoise = record.CurrentPoise,
            countsAsLevelBoss = record.CountsAsLevelBoss,
            isDead = record.IsDead,
            showCombatHealthBar = record.ShowCombatHealthBar,
            moveBlend = record.MoveBlend,
            moveForward = record.MoveForward,
            moveStrafe = record.MoveStrafe,
            activeSkillSequence = record.ActiveSkillSequence,
            activeSkillSlot = record.ActiveSkillSlot,
            activeSkillId = record.ActiveSkillId,
            activeSkillAnimationTrigger = record.ActiveSkillAnimationTrigger,
            hasActiveSkillTarget = record.HasActiveSkillTarget,
            activeSkillTargetX = record.ActiveSkillTargetX,
            activeSkillTargetY = record.ActiveSkillTargetY,
            activeSkillTargetZ = record.ActiveSkillTargetZ,
            updatedAtUtc = record.UpdatedAtUtc,
            diedAtUtc = record.DiedAtUtc,
        };
    }

    private static DungeonUiPanelEventListDto ToUiPanelEventListDto(DungeonInstanceRecord record, string requesterUserId, long afterSequence)
    {
        return new DungeonUiPanelEventListDto
        {
            events = record.UiPanelEvents
                .Where(panelEvent => panelEvent.Sequence > afterSequence &&
                                     !string.Equals(panelEvent.SourceUserId, requesterUserId, StringComparison.Ordinal))
                .OrderBy(panelEvent => panelEvent.Sequence)
                .Select(ToDto)
                .ToList(),
        };
    }

    private static DungeonUiPanelEventDto ToDto(DungeonUiPanelEventRecord record)
    {
        return new DungeonUiPanelEventDto
        {
            eventId = record.EventId,
            sequence = record.Sequence,
            sourceUserId = record.SourceUserId,
            panelName = record.PanelName,
            open = record.Open,
            createdAtUtc = record.CreatedAtUtc,
        };
    }

    private static DungeonUiPanelStateDto ToUiPanelStateDto(DungeonInstanceRecord record)
    {
        return new DungeonUiPanelStateDto
        {
            version = record.UiPanelStateVersion,
            openPanelName = record.OpenPanelName,
            hasOpenPanel = !string.IsNullOrWhiteSpace(record.OpenPanelName),
            updatedByUserId = record.UiPanelStateUpdatedByUserId,
            updatedAtUtc = record.UiPanelStateUpdatedAtUtc,
        };
    }

    private static DungeonSceneLoadStateDto ToSceneLoadStateDto(DungeonInstanceRecord record)
    {
        return new DungeonSceneLoadStateDto
        {
            version = record.SceneLoadVersion,
            active = record.SceneLoadActive,
            released = record.SceneLoadReleased,
            transitionId = record.SceneLoadTransitionId,
            sceneName = record.SceneLoadSceneName,
            bossId = record.SceneLoadBossId,
            bossDisplayName = record.SceneLoadBossDisplayName,
            releaseAtUtc = record.SceneLoadReleaseAtUtc,
            readyUserIds = record.SceneLoadReadyUserIds
                .OrderBy(userId => userId)
                .ToList(),
            completedUserIds = record.SceneLoadCompletedUserIds
                .OrderBy(userId => userId)
                .ToList(),
            participantCount = record.Participants.Count,
            readyCount = record.SceneLoadReadyUserIds.Count,
            completedCount = record.SceneLoadCompletedUserIds.Count,
        };
    }

    private static bool AreAllSceneLoadParticipantsReady(DungeonInstanceRecord record)
    {
        if (record.Participants.Count <= 0)
            return false;

        foreach (string userId in record.Participants.Keys)
        {
            if (!record.SceneLoadReadyUserIds.Contains(userId))
                return false;
        }

        return true;
    }

    private static bool AreAllSceneLoadParticipantsComplete(DungeonInstanceRecord record)
    {
        if (record.Participants.Count <= 0)
            return false;

        foreach (string userId in record.Participants.Keys)
        {
            if (!record.SceneLoadCompletedUserIds.Contains(userId))
                return false;
        }

        return true;
    }

    private static void AddDungeonDrops(
        DungeonInstanceRecord record,
        string enemyRuntimeId,
        List<DungeonDropProposal>? drops,
        DateTime now,
        long rewardVersion)
    {
        if (drops == null)
            return;

        for (int i = 0; i < drops.Count; i++)
        {
            DungeonDropProposal proposal = drops[i];
            if (proposal == null)
                continue;

            string dropId = NormalizeRequired(proposal.DropId);
            if (string.IsNullOrWhiteSpace(dropId))
                dropId = Guid.NewGuid().ToString("N");

            if (record.Drops.ContainsKey(dropId))
                continue;

            string itemType = NormalizeRequired(proposal.ItemType);
            if (string.IsNullOrWhiteSpace(itemType))
                continue;

            record.Drops[dropId] = new DungeonDropRecord
            {
                DropId = dropId,
                EnemyRuntimeId = enemyRuntimeId,
                ItemType = itemType,
                PayloadJson = proposal.PayloadJson ?? string.Empty,
                Count = Math.Max(1, proposal.Count),
                X = proposal.X,
                Y = proposal.Y,
                Z = proposal.Z,
                CreatedVersion = rewardVersion,
                CreatedAtUtc = now,
            };
        }
    }

    private static DungeonRewardStateDto ToRewardStateDto(DungeonInstanceRecord record)
    {
        return new DungeonRewardStateDto
        {
            version = record.RewardStateVersion,
            killRewards = record.KillRewards.Values
                .OrderBy(reward => reward.CreatedAtUtc)
                .Select(ToDto)
                .ToList(),
            drops = record.Drops.Values
                .OrderBy(drop => drop.CreatedAtUtc)
                .Select(ToDto)
                .ToList(),
        };
    }

    private static DungeonRewardStateDto ToRewardStateDto(DungeonInstanceRecord record, long afterVersion)
    {
        long normalizedAfterVersion = Math.Max(0, afterVersion);
        return new DungeonRewardStateDto
        {
            version = record.RewardStateVersion,
            killRewards = record.KillRewards.Values
                .Where(reward => reward.RewardVersion > normalizedAfterVersion || reward.ClaimVersion > normalizedAfterVersion)
                .OrderBy(reward => reward.CreatedAtUtc)
                .Select(ToDto)
                .ToList(),
            drops = record.Drops.Values
                .Where(drop => drop.CreatedVersion > normalizedAfterVersion || drop.PickedUpVersion > normalizedAfterVersion)
                .OrderBy(drop => drop.CreatedAtUtc)
                .Select(ToDto)
                .ToList(),
        };
    }

    private static DungeonKillRewardDto ToDto(DungeonKillRewardRecord record)
    {
        return new DungeonKillRewardDto
        {
            enemyRuntimeId = record.EnemyRuntimeId,
            killerUserId = record.KillerUserId,
            exp = record.Exp,
            gold = record.Gold,
            talentPoints = record.TalentPoints,
            x = record.X,
            y = record.Y,
            z = record.Z,
            claimed = record.Claimed,
            claimedAtUtc = record.ClaimedAtUtc,
            createdAtUtc = record.CreatedAtUtc,
        };
    }

    private static DungeonDropDto ToDto(DungeonDropRecord record)
    {
        return new DungeonDropDto
        {
            dropId = record.DropId,
            enemyRuntimeId = record.EnemyRuntimeId,
            itemType = record.ItemType,
            payloadJson = record.PayloadJson,
            count = record.Count,
            x = record.X,
            y = record.Y,
            z = record.Z,
            pickedUp = record.PickedUp,
            pickedUpByUserId = record.PickedUpByUserId,
            pickedUpAtUtc = record.PickedUpAtUtc,
            createdAtUtc = record.CreatedAtUtc,
        };
    }

    private static void TrimUiPanelEvents(DungeonInstanceRecord record)
    {
        const int maxUiPanelEvents = 128;
        while (record.UiPanelEvents.Count > maxUiPanelEvents)
        {
            DungeonUiPanelEventRecord removed = record.UiPanelEvents[0];
            record.UiPanelEvents.RemoveAt(0);
            record.UiPanelEventIds.Remove(removed.EventId);
        }
    }

    private static DungeonDamageEventListDto ToDamageEventListDto(DungeonInstanceRecord record, string requesterUserId, long afterSequence)
    {
        return new DungeonDamageEventListDto
        {
            events = record.DamageEvents
                .Where(damageEvent => damageEvent.Sequence > afterSequence &&
                                      !string.Equals(damageEvent.SourceUserId, requesterUserId, StringComparison.Ordinal))
                .OrderBy(damageEvent => damageEvent.Sequence)
                .Select(ToDto)
                .ToList(),
        };
    }

    private static DungeonChestStateDto ToDto(DungeonChestStateRecord record)
    {
        return new DungeonChestStateDto
        {
            chestId = record.ChestId,
            prefabId = record.PrefabId,
            x = record.X,
            y = record.Y,
            z = record.Z,
            yaw = record.Yaw,
            opened = record.Opened,
            openedByUserId = record.OpenedByUserId,
            openedAtUtc = record.OpenedAtUtc,
            updatedAtUtc = record.UpdatedAtUtc,
        };
    }

    private static DungeonDamageEventDto ToDto(DungeonDamageEventRecord record)
    {
        return new DungeonDamageEventDto
        {
            eventId = record.EventId,
            sequence = record.Sequence,
            sourceUserId = record.SourceUserId,
            targetKind = record.TargetKind,
            targetRuntimeId = record.TargetRuntimeId,
            damage = record.Damage,
            stunDuration = record.StunDuration,
            targetRemainingHp = record.TargetRemainingHp,
            targetDied = record.TargetDied,
            targetShowCombatHealthBar = record.TargetShowCombatHealthBar,
            x = record.X,
            y = record.Y,
            z = record.Z,
            createdAtUtc = record.CreatedAtUtc,
        };
    }

    private static void TrimDamageEvents(DungeonInstanceRecord record)
    {
        const int maxDamageEvents = 512;
        while (record.DamageEvents.Count > maxDamageEvents)
        {
            DungeonDamageEventRecord removed = record.DamageEvents[0];
            record.DamageEvents.RemoveAt(0);
            record.DamageEventIds.Remove(removed.EventId);
        }
    }

    private static DungeonWorldSnapshotDto ToDto(DungeonWorldSnapshotRecord record)
    {
        return new DungeonWorldSnapshotDto
        {
            ownerUserId = record.OwnerUserId,
            sceneName = record.SceneName,
            snapshotJson = record.SnapshotJson,
            version = record.Version,
            updatedAtUtc = record.UpdatedAtUtc,
        };
    }

    private static DungeonWorldSnapshotDto ToDto(DungeonWorldSnapshotRecord record, DungeonInstanceRecord instance)
    {
        return new DungeonWorldSnapshotDto
        {
            ownerUserId = record.OwnerUserId,
            sceneName = record.SceneName,
            snapshotJson = BuildAuthoritativeSnapshotJson(record.SnapshotJson, instance),
            version = Math.Max(record.Version, instance.AuthorityVersion),
            updatedAtUtc = record.UpdatedAtUtc,
        };
    }

    private static string BuildAuthoritativeSnapshotJson(string snapshotJson, DungeonInstanceRecord instance)
    {
        if (!instance.AuthorityInitialized)
            return snapshotJson;

        JsonNode? rootNode = JsonNode.Parse(snapshotJson);
        if (rootNode is not JsonObject rootObject)
            throw new InvalidOperationException("副本世界快照格式无效");

        if (rootObject["enemies"] is JsonArray enemiesArray)
        {
            for (int i = enemiesArray.Count - 1; i >= 0; i--)
            {
                if (enemiesArray[i] is not JsonObject enemyObject)
                {
                    enemiesArray.RemoveAt(i);
                    continue;
                }

                string runtimeId = enemyObject["runtimeId"]?.GetValue<string>()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(runtimeId) ||
                    !instance.Enemies.TryGetValue(runtimeId, out DungeonEnemyStateRecord? enemy) ||
                    enemy.IsDead)
                {
                    enemiesArray.RemoveAt(i);
                    continue;
                }

                enemyObject["currentHp"] = enemy.CurrentHp;
                enemyObject["currentPoise"] = enemy.CurrentPoise;
                enemyObject["x"] = enemy.X;
                enemyObject["y"] = enemy.Y;
                enemyObject["z"] = enemy.Z;
                enemyObject["yaw"] = enemy.Yaw;
                enemyObject["countsAsLevelBoss"] = enemy.CountsAsLevelBoss;
                enemyObject["showCombatHealthBar"] = enemy.ShowCombatHealthBar;
            }
        }

        JsonArray openedChestIds = new();
        foreach (DungeonChestStateRecord chest in instance.Chests.Values.OrderBy(chest => chest.ChestId))
        {
            if (chest.Opened)
                openedChestIds.Add(chest.ChestId);
        }

        rootObject["openedChestIds"] = openedChestIds;
        if (rootObject["chests"] is JsonArray chestsArray)
        {
            for (int i = chestsArray.Count - 1; i >= 0; i--)
            {
                if (chestsArray[i] is not JsonObject chestObject)
                {
                    chestsArray.RemoveAt(i);
                    continue;
                }

                string chestId = chestObject["snapshotId"]?.GetValue<string>()?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(chestId) &&
                    instance.Chests.TryGetValue(chestId, out DungeonChestStateRecord? chest) &&
                    chest.Opened)
                {
                    chestsArray.RemoveAt(i);
                }
            }
        }

        return rootObject.ToJsonString();
    }

    private static string NormalizeRequired(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            return string.Empty;

        return value.GetString()?.Trim() ?? string.Empty;
    }

    private static int GetIntProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
            return 0;

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result) ? result : 0;
    }

    private static float GetFloatProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
            return 0f;

        return value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float result) ? result : 0f;
    }

    private static bool GetBoolProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
            return false;

        return value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.False ? false : false);
    }

    private static HashSet<string> ReadOpenedChestIds(JsonElement rootElement)
    {
        HashSet<string> openedChestIds = new(StringComparer.Ordinal);
        if (!rootElement.TryGetProperty("openedChestIds", out JsonElement openedElement) ||
            openedElement.ValueKind != JsonValueKind.Array)
        {
            return openedChestIds;
        }

        foreach (JsonElement openedIdElement in openedElement.EnumerateArray())
        {
            if (openedIdElement.ValueKind != JsonValueKind.String)
                continue;

            string openedId = openedIdElement.GetString()?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(openedId))
                openedChestIds.Add(openedId);
        }

        return openedChestIds;
    }
}

internal sealed class DungeonRewardConfigStore
{
    private readonly Dictionary<int, DungeonLevelRewardConfig> _levels;
    private readonly List<DungeonWeaponRewardConfig> _weapons;

    public DungeonRewardConfigStore(IWebHostEnvironment environment)
    {
        string configPath = Path.Combine(environment.ContentRootPath, "dungeon-rewards.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException("缺少联机副本奖励配置文件", configPath);

        string json = File.ReadAllText(configPath);
        DungeonRewardConfigRoot? config = JsonSerializer.Deserialize<DungeonRewardConfigRoot>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        _levels = config?.Levels?
            .Where(level => level != null && level.LevelIndex > 0)
            .ToDictionary(level => level.LevelIndex, level => level)
            ?? new Dictionary<int, DungeonLevelRewardConfig>();
        _weapons = config?.Weapons?
            .Where(weapon => weapon != null && !string.IsNullOrWhiteSpace(weapon.WeaponId))
            .ToList()
            ?? new List<DungeonWeaponRewardConfig>();

        if (_levels.Count == 0 || _weapons.Count == 0)
            throw new InvalidOperationException("联机副本奖励配置不完整");
    }

    public bool TryBuildReward(
        DungeonInstanceRecord record,
        DungeonEnemyStateRecord enemy,
        AddDungeonEnemyKillRequest request,
        out DungeonRewardProposal proposal,
        out string error)
    {
        proposal = new DungeonRewardProposal();
        error = string.Empty;

        if (!_levels.TryGetValue(record.LevelIndex, out DungeonLevelRewardConfig? levelConfig))
        {
            error = $"缺少关卡 {record.LevelIndex} 的联机副本奖励配置";
            return false;
        }

        if (!levelConfig.TryGetReward(enemy.EnemyType, out DungeonEnemyRewardConfig? rewardConfig))
        {
            error = $"缺少敌人类型 {enemy.EnemyType} 的联机副本奖励配置";
            return false;
        }

        DungeonEnemyRewardConfig resolvedRewardConfig = rewardConfig!;
        proposal.Exp = Math.Max(0, resolvedRewardConfig.Exp);
        proposal.Gold = Math.Max(0, resolvedRewardConfig.Gold);
        proposal.TalentPoints = Math.Max(0, resolvedRewardConfig.TalentPoints);

        Random rng = BuildRewardRng(record, enemy.RuntimeId);
        if (!TryBuildDrop(resolvedRewardConfig, request, rng, enemy.RuntimeId, out DungeonDropProposal? drop, out error))
            return false;

        if (drop != null)
            proposal.Drops.Add(drop);

        return true;
    }

    public bool TryBuildChestDrops(
        DungeonInstanceRecord record,
        OpenDungeonChestRequest request,
        string chestId,
        out List<DungeonDropProposal>? drops,
        out string error)
    {
        drops = new List<DungeonDropProposal>();
        error = string.Empty;
        if (!_levels.TryGetValue(record.LevelIndex, out DungeonLevelRewardConfig? levelConfig))
        {
            error = $"缺少关卡 {record.LevelIndex} 的联机副本奖励配置";
            return false;
        }

        if (levelConfig.ChestDrops == null || levelConfig.ChestDrops.Count == 0)
        {
            error = $"缺少关卡 {record.LevelIndex} 的联机副本宝箱掉落配置";
            return false;
        }

        Random rng = BuildRewardRng(record, chestId);
        DungeonEnemyRewardConfig rewardConfig = new DungeonEnemyRewardConfig
        {
            Drops = levelConfig.ChestDrops,
        };
        for (int i = 0; i < Math.Max(1, request.DropCount); i++)
        {
            if (!TryBuildDrop(rewardConfig, request, rng, chestId, i, out DungeonDropProposal? drop, out error))
                return false;

            if (drop != null)
                drops.Add(drop);
        }

        return true;
    }

    private bool TryBuildDrop(
        DungeonEnemyRewardConfig rewardConfig,
        AddDungeonEnemyKillRequest request,
        Random rng,
        string enemyRuntimeId,
        out DungeonDropProposal? drop,
        out string error)
    {
        return TryBuildDrop(rewardConfig, request, rng, enemyRuntimeId, 0, out drop, out error);
    }

    private bool TryBuildDrop(
        DungeonEnemyRewardConfig rewardConfig,
        IDungeonDropOriginRequest request,
        Random rng,
        string sourceRuntimeId,
        int dropIndex,
        out DungeonDropProposal? drop,
        out string error)
    {
        drop = null;
        error = string.Empty;

        string itemType = RollDropItemType(rewardConfig.Drops, rng);
        if (string.IsNullOrWhiteSpace(itemType))
            return true;

        if (TryResolveWeaponRarity(itemType, out int rarity))
        {
            if (!TryRollWeapon(rarity, rng, out string payloadJson, out error))
                return false;

            drop = new DungeonDropProposal
            {
                DropId = BuildDropId(sourceRuntimeId, dropIndex),
                ItemType = "weapon",
                PayloadJson = payloadJson,
                Count = 1,
                X = request.X,
                Y = request.Y,
                Z = request.Z,
            };
            return true;
        }

        if (!IsStackableDrop(itemType))
        {
            error = $"未识别的联机副本掉落类型：{itemType}";
            return false;
        }

        drop = new DungeonDropProposal
        {
            DropId = BuildDropId(sourceRuntimeId, dropIndex),
            ItemType = "stackable",
            PayloadJson = JsonSerializer.Serialize(new DungeonStackableDropPayload
            {
                itemId = itemType,
                count = 1,
            }),
            Count = 1,
            X = request.X,
            Y = request.Y,
            Z = request.Z,
        };
        return true;
    }

    private bool TryRollWeapon(int rarity, Random rng, out string payloadJson, out string error)
    {
        payloadJson = string.Empty;
        error = string.Empty;

        if (_weapons.Count == 0)
        {
            error = "联机副本武器奖励配置为空";
            return false;
        }

        DungeonWeaponRewardConfig weapon = _weapons[rng.Next(_weapons.Count)];
        if (!weapon.TryGetRanges(rarity, out DungeonWeaponStatRanges? ranges))
        {
            error = $"武器 {weapon.WeaponId} 缺少品质 {rarity} 的随机范围配置";
            return false;
        }

        DungeonWeaponStatRanges resolvedRanges = ranges!;
        payloadJson = JsonSerializer.Serialize(new DungeonWeaponDropPayload
        {
            weaponId = weapon.WeaponId,
            type = weapon.Type,
            rarity = rarity,
            rolledHp = resolvedRanges.Roll(nameof(DungeonWeaponDropPayload.rolledHp), rng),
            rolledMp = resolvedRanges.Roll(nameof(DungeonWeaponDropPayload.rolledMp), rng),
            rolledAttack = resolvedRanges.Roll(nameof(DungeonWeaponDropPayload.rolledAttack), rng),
            rolledDefense = resolvedRanges.Roll(nameof(DungeonWeaponDropPayload.rolledDefense), rng),
            rolledHpRegen = rarity >= 1 ? resolvedRanges.Roll(nameof(DungeonWeaponDropPayload.rolledHpRegen), rng) : 0f,
            rolledMpRegen = rarity >= 1 ? resolvedRanges.Roll(nameof(DungeonWeaponDropPayload.rolledMpRegen), rng) : 0f,
            rolledCritRate = rarity >= 2 ? resolvedRanges.Roll(nameof(DungeonWeaponDropPayload.rolledCritRate), rng) : 0f,
            rolledCritDmg = rarity >= 2 ? resolvedRanges.Roll(nameof(DungeonWeaponDropPayload.rolledCritDmg), rng) : 0f,
            rolledAttackSpeed = rarity >= 3 ? resolvedRanges.Roll(nameof(DungeonWeaponDropPayload.rolledAttackSpeed), rng) : 0f,
            rolledMoveSpeed = rarity >= 3 ? resolvedRanges.Roll(nameof(DungeonWeaponDropPayload.rolledMoveSpeed), rng) : 0f,
        });
        return true;
    }

    private static string RollDropItemType(List<DungeonDropWeightConfig>? drops, Random rng)
    {
        if (drops == null || drops.Count == 0)
            return string.Empty;

        double totalWeight = 0d;
        for (int i = 0; i < drops.Count; i++)
        {
            DungeonDropWeightConfig drop = drops[i];
            if (drop != null)
                totalWeight += Math.Max(0d, drop.Weight);
        }

        if (totalWeight <= 0d)
            return string.Empty;

        double roll = rng.NextDouble() * totalWeight;
        double cumulative = 0d;
        for (int i = 0; i < drops.Count; i++)
        {
            DungeonDropWeightConfig drop = drops[i];
            if (drop == null)
                continue;

            cumulative += Math.Max(0d, drop.Weight);
            if (roll <= cumulative)
                return drop.ItemType?.Trim() ?? string.Empty;
        }

        return drops[^1]?.ItemType?.Trim() ?? string.Empty;
    }

    private static Random BuildRewardRng(DungeonInstanceRecord record, string enemyRuntimeId)
    {
        unchecked
        {
            int hash = record.Seed;
            hash = (hash * 397) ^ record.LevelIndex;
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(enemyRuntimeId);
            return new Random(hash);
        }
    }

    private static bool TryResolveWeaponRarity(string itemType, out int rarity)
    {
        rarity = itemType switch
        {
            "weapon_common" => 0,
            "weapon_rare" => 1,
            "weapon_epic" => 2,
            "weapon_legendary" => 3,
            _ => -1,
        };
        return rarity >= 0;
    }

    private static bool IsStackableDrop(string itemType)
    {
        return itemType == "potion_hp" || itemType == "potion_mp" || itemType == "nectar";
    }

    private static string BuildDropId(string enemyRuntimeId, int index)
    {
        string source = string.IsNullOrWhiteSpace(enemyRuntimeId) ? Guid.NewGuid().ToString("N") : enemyRuntimeId.Trim();
        return $"{source}_{index}";
    }
}

internal sealed class DungeonRewardProposal
{
    public int Exp { get; set; }
    public int Gold { get; set; }
    public int TalentPoints { get; set; }
    public List<DungeonDropProposal> Drops { get; } = new();
}

internal sealed class DungeonDropProposal
{
    public string? DropId { get; init; }
    public string? ItemType { get; init; }
    public string? PayloadJson { get; init; }
    public int Count { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
}

internal sealed class DungeonRewardConfigRoot
{
    public List<DungeonLevelRewardConfig>? Levels { get; init; }
    public List<DungeonWeaponRewardConfig>? Weapons { get; init; }
}

internal sealed class DungeonLevelRewardConfig
{
    public int LevelIndex { get; init; }
    public DungeonEnemyRewardSetConfig? Rewards { get; init; }
    public List<DungeonDropWeightConfig>? ChestDrops { get; init; }

    public bool TryGetReward(int enemyType, out DungeonEnemyRewardConfig? reward)
    {
        reward = enemyType switch
        {
            2 => Rewards?.Elite,
            3 => Rewards?.Guardian,
            4 => Rewards?.Boss,
            _ => Rewards?.Minion,
        };
        return reward != null;
    }
}

internal sealed class DungeonEnemyRewardSetConfig
{
    public DungeonEnemyRewardConfig? Minion { get; init; }
    public DungeonEnemyRewardConfig? Elite { get; init; }
    public DungeonEnemyRewardConfig? Guardian { get; init; }
    public DungeonEnemyRewardConfig? Boss { get; init; }
}

internal sealed class DungeonEnemyRewardConfig
{
    public int Gold { get; init; }
    public int Exp { get; init; }
    public int TalentPoints { get; init; }
    public List<DungeonDropWeightConfig>? Drops { get; init; }
}

internal sealed class DungeonDropWeightConfig
{
    public string? ItemType { get; init; }
    public double Weight { get; init; }
}

internal sealed class DungeonWeaponRewardConfig
{
    public string WeaponId { get; init; } = string.Empty;
    public int Type { get; init; }
    public DungeonWeaponStatRanges? Common { get; init; }
    public DungeonWeaponStatRanges? Rare { get; init; }
    public DungeonWeaponStatRanges? Epic { get; init; }
    public DungeonWeaponStatRanges? Legendary { get; init; }

    public bool TryGetRanges(int rarity, out DungeonWeaponStatRanges? ranges)
    {
        ranges = rarity switch
        {
            1 => Rare,
            2 => Epic,
            3 => Legendary,
            _ => Common,
        };
        return ranges != null;
    }
}

internal sealed class DungeonWeaponStatRanges
{
    public float[]? Hp { get; init; }
    public float[]? Mp { get; init; }
    public float[]? Attack { get; init; }
    public float[]? Defense { get; init; }
    public float[]? HpRegen { get; init; }
    public float[]? MpRegen { get; init; }
    public float[]? CritRate { get; init; }
    public float[]? CritDmg { get; init; }
    public float[]? AttackSpeed { get; init; }
    public float[]? MoveSpeed { get; init; }

    public float Roll(string fieldName, Random rng)
    {
        float[]? range = fieldName switch
        {
            nameof(DungeonWeaponDropPayload.rolledHp) => Hp,
            nameof(DungeonWeaponDropPayload.rolledMp) => Mp,
            nameof(DungeonWeaponDropPayload.rolledAttack) => Attack,
            nameof(DungeonWeaponDropPayload.rolledDefense) => Defense,
            nameof(DungeonWeaponDropPayload.rolledHpRegen) => HpRegen,
            nameof(DungeonWeaponDropPayload.rolledMpRegen) => MpRegen,
            nameof(DungeonWeaponDropPayload.rolledCritRate) => CritRate,
            nameof(DungeonWeaponDropPayload.rolledCritDmg) => CritDmg,
            nameof(DungeonWeaponDropPayload.rolledAttackSpeed) => AttackSpeed,
            nameof(DungeonWeaponDropPayload.rolledMoveSpeed) => MoveSpeed,
            _ => null,
        };

        if (range == null || range.Length == 0)
            return 0f;

        float min = range[0];
        float max = range.Length > 1 ? range[1] : min;
        if (min >= max)
            return min;

        return min + (float)rng.NextDouble() * (max - min);
    }
}

internal sealed class DungeonWeaponDropPayload
{
    public string weaponId { get; init; } = string.Empty;
    public int type { get; init; }
    public int rarity { get; init; }
    public float rolledHp { get; init; }
    public float rolledMp { get; init; }
    public float rolledAttack { get; init; }
    public float rolledDefense { get; init; }
    public float rolledHpRegen { get; init; }
    public float rolledMpRegen { get; init; }
    public float rolledCritRate { get; init; }
    public float rolledCritDmg { get; init; }
    public float rolledAttackSpeed { get; init; }
    public float rolledMoveSpeed { get; init; }
}

internal sealed class DungeonStackableDropPayload
{
    public string itemId { get; init; } = string.Empty;
    public int count { get; init; }
}

internal enum DungeonInstanceStatus
{
    Active,
    Closed,
}

internal sealed class DungeonInstanceRecord
{
    public object SyncRoot { get; } = new();
    public string InstanceId { get; init; } = string.Empty;
    public string TemplateOwnerUserId { get; init; } = string.Empty;
    public string TemplateSaveId { get; set; } = string.Empty;
    public int LevelIndex { get; init; }
    public int Difficulty { get; init; }
    public int Seed { get; init; }
    public int MaxPlayers { get; init; }
    public DungeonInstanceStatus Status { get; set; }
    public string CloseReason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }
    public Dictionary<string, DungeonParticipantRecord> Participants { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, DungeonPlayerPoseRecord> PlayerPoses { get; } = new(StringComparer.Ordinal);
    public DungeonWorldSnapshotRecord? WorldSnapshot { get; set; }
    public bool AuthorityInitialized { get; set; }
    public long AuthorityVersion { get; set; }
    public Dictionary<string, DungeonEnemyStateRecord> Enemies { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, DungeonChestStateRecord> Chests { get; } = new(StringComparer.Ordinal);
    public List<DungeonDamageEventRecord> DamageEvents { get; } = new();
    public HashSet<string> DamageEventIds { get; } = new(StringComparer.Ordinal);
    public long NextDamageEventSequence { get; set; }
    public List<DungeonUiPanelEventRecord> UiPanelEvents { get; } = new();
    public HashSet<string> UiPanelEventIds { get; } = new(StringComparer.Ordinal);
    public long NextUiPanelEventSequence { get; set; }
    public long UiPanelStateVersion { get; set; }
    public string OpenPanelName { get; set; } = string.Empty;
    public string UiPanelStateUpdatedByUserId { get; set; } = string.Empty;
    public DateTime UiPanelStateUpdatedAtUtc { get; set; }
    public long SceneLoadVersion { get; set; }
    public bool SceneLoadActive { get; set; }
    public bool SceneLoadReleased { get; set; }
    public string SceneLoadTransitionId { get; set; } = string.Empty;
    public string SceneLoadSceneName { get; set; } = string.Empty;
    public string SceneLoadBossId { get; set; } = string.Empty;
    public string SceneLoadBossDisplayName { get; set; } = string.Empty;
    public DateTime? SceneLoadReleaseAtUtc { get; set; }
    public HashSet<string> SceneLoadReadyUserIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> SceneLoadCompletedUserIds { get; } = new(StringComparer.Ordinal);
    public long RewardStateVersion { get; set; }
    public Dictionary<string, DungeonKillRewardRecord> KillRewards { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, DungeonDropRecord> Drops { get; } = new(StringComparer.Ordinal);
}

internal sealed class DungeonParticipantRecord
{
    public string UserId { get; init; } = string.Empty;
    public string SaveId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string JoinToken { get; init; } = string.Empty;
    public bool IsConnected { get; set; }
    public float CurrentHp { get; set; } = 1f;
    public float MaxHp { get; set; } = 1f;
    public bool IsDead { get; set; }
    public DateTime JoinedAtUtc { get; init; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime? DiedAtUtc { get; set; }
}

internal sealed class DungeonPlayerPoseRecord
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Yaw { get; init; }
    public bool AimActive { get; init; }
    public bool AimLayerActive { get; init; }
    public float AimPitch { get; init; }
    public float AimDirectionX { get; init; }
    public float AimDirectionY { get; init; }
    public float AimDirectionZ { get; init; }
    public float MoveSpeed { get; init; }
    public float MoveX { get; init; }
    public float MoveY { get; init; }
    public int Action { get; init; }
    public string ActionId { get; init; } = string.Empty;
    public string SkillEffectId { get; init; } = string.Empty;
    public int ActionSequence { get; init; }
    public float ActionElapsedSeconds { get; init; }
    public float ActionNormalizedProgress { get; init; }
    public int AttackMode { get; init; }
    public bool HasMeleeWeapon { get; init; }
    public bool HasRangedWeapon { get; init; }
    public float Defense { get; init; }
    public float DamageReduce { get; init; }
    public float CurrentHp { get; init; }
    public float MaxHp { get; init; }
    public bool IsDead { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

internal sealed class DungeonEnemyStateRecord
{
    public string RuntimeId { get; init; } = string.Empty;
    public string EnemyId { get; set; } = string.Empty;
    public int EnemyType { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public float CurrentHp { get; set; }
    public float CurrentPoise { get; set; }
    public bool CountsAsLevelBoss { get; set; }
    public bool IsDead { get; set; }
    public bool ShowCombatHealthBar { get; set; }
    public float MoveBlend { get; set; }
    public float MoveForward { get; set; }
    public float MoveStrafe { get; set; }
    public int ActiveSkillSequence { get; set; }
    public int ActiveSkillSlot { get; set; }
    public string ActiveSkillId { get; set; } = string.Empty;
    public string ActiveSkillAnimationTrigger { get; set; } = string.Empty;
    public bool HasActiveSkillTarget { get; set; }
    public float ActiveSkillTargetX { get; set; }
    public float ActiveSkillTargetY { get; set; }
    public float ActiveSkillTargetZ { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DiedAtUtc { get; set; }
}

internal sealed class DungeonChestStateRecord
{
    public string ChestId { get; init; } = string.Empty;
    public string PrefabId { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public bool Opened { get; set; }
    public string OpenedByUserId { get; set; } = string.Empty;
    public DateTime? OpenedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class DungeonDamageEventRecord
{
    public string EventId { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string SourceUserId { get; init; } = string.Empty;
    public string TargetKind { get; init; } = string.Empty;
    public string TargetRuntimeId { get; init; } = string.Empty;
    public float Damage { get; init; }
    public float StunDuration { get; init; }
    public float TargetRemainingHp { get; init; }
    public bool TargetDied { get; init; }
    public bool TargetShowCombatHealthBar { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

internal sealed class DungeonUiPanelEventRecord
{
    public string EventId { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string SourceUserId { get; init; } = string.Empty;
    public string PanelName { get; init; } = string.Empty;
    public bool Open { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

internal sealed class DungeonWorldSnapshotRecord
{
    public string OwnerUserId { get; init; } = string.Empty;
    public string SceneName { get; init; } = string.Empty;
    public string SnapshotJson { get; init; } = string.Empty;
    public long Version { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

internal sealed class DungeonKillRewardRecord
{
    public string EnemyRuntimeId { get; init; } = string.Empty;
    public string KillerUserId { get; init; } = string.Empty;
    public int Exp { get; init; }
    public int Gold { get; init; }
    public int TalentPoints { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public bool Claimed { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
    public long RewardVersion { get; init; }
    public long ClaimVersion { get; set; }
    public DateTime CreatedAtUtc { get; init; }
}

internal sealed class DungeonDropRecord
{
    public string DropId { get; init; } = string.Empty;
    public string EnemyRuntimeId { get; init; } = string.Empty;
    public string ItemType { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public int Count { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public bool PickedUp { get; set; }
    public string PickedUpByUserId { get; set; } = string.Empty;
    public DateTime? PickedUpAtUtc { get; set; }
    public long CreatedVersion { get; init; }
    public long PickedUpVersion { get; set; }
    public DateTime CreatedAtUtc { get; init; }
}

internal sealed class CreateDungeonInstanceRequest
{
    public string? InstanceId { get; init; }
    public string? TemplateOwnerUserId { get; init; }
    public string? TemplateSaveId { get; init; }
    public int LevelIndex { get; init; }
    public int Difficulty { get; init; }
    public int Seed { get; init; }
    public int MaxPlayers { get; init; }
}

internal sealed class JoinDungeonInstanceRequest
{
    public string? UserId { get; init; }
    public string? SaveId { get; init; }
    public string? DisplayName { get; init; }
    public string? JoinToken { get; init; }
}

internal sealed class LeaveDungeonInstanceRequest
{
    public string? UserId { get; init; }
}

internal sealed class ValidateDungeonParticipantRequest
{
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
}

internal sealed class UpsertDungeonPlayerPoseRequest
{
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Yaw { get; init; }
    public bool AimActive { get; init; }
    public bool AimLayerActive { get; init; }
    public float AimPitch { get; init; }
    public float AimDirectionX { get; init; }
    public float AimDirectionY { get; init; }
    public float AimDirectionZ { get; init; }
    public float MoveSpeed { get; init; }
    public float MoveX { get; init; }
    public float MoveY { get; init; }
    public int Action { get; init; }
    public string ActionId { get; init; } = string.Empty;
    public string SkillEffectId { get; init; } = string.Empty;
    public int ActionSequence { get; init; }
    public float ActionElapsedSeconds { get; init; }
    public float ActionNormalizedProgress { get; init; }
    public int AttackMode { get; init; }
    public bool HasMeleeWeapon { get; init; }
    public bool HasRangedWeapon { get; init; }
    public float Defense { get; init; }
    public float DamageReduce { get; init; }
    public float CurrentHp { get; init; }
    public float MaxHp { get; init; }
    public bool IsDead { get; init; }
}

internal sealed class AddDungeonDamageEventRequest
{
    public string? EventId { get; init; }
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
    public string? TargetKind { get; init; }
    public string? TargetRuntimeId { get; init; }
    public float Damage { get; init; }
    public float StunDuration { get; init; }
    public DungeonEnemyAuthorityStateUpsertDto? TargetEnemy { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
}

internal sealed class AddDungeonUiPanelEventRequest
{
    public string? EventId { get; init; }
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
    public string? PanelName { get; init; }
    public bool Open { get; init; }
}

internal sealed class StartDungeonSceneLoadRequest
{
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
    public string? TransitionId { get; init; }
    public string? SceneName { get; init; }
    public string? BossId { get; init; }
    public string? BossDisplayName { get; init; }
}

internal sealed class MarkDungeonSceneLoadReadyRequest
{
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
    public string? TransitionId { get; init; }
}

internal sealed class CompleteDungeonSceneLoadRequest
{
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
    public string? TransitionId { get; init; }
}

internal sealed class AddDungeonEnemyKillRequest
    : IDungeonDropOriginRequest
{
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
    public string? EnemyRuntimeId { get; init; }
    public DungeonEnemyAuthorityStateUpsertDto? TargetEnemy { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
}

internal sealed class OpenDungeonChestRequest
    : IDungeonDropOriginRequest
{
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
    public string? PrefabId { get; init; }
    public int DropCount { get; init; } = 1;
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Yaw { get; init; }
}

internal interface IDungeonDropOriginRequest
{
    float X { get; }
    float Y { get; }
    float Z { get; }
}

internal sealed class PickupDungeonDropRequest
{
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
}

internal sealed class ClaimDungeonKillRewardRequest
{
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
}

internal sealed class UpsertDungeonWorldSnapshotRequest
{
    public string? UserId { get; init; }
    public string? JoinToken { get; init; }
    public string? SceneName { get; init; }
    public string SnapshotJson { get; init; } = string.Empty;
}

internal sealed class CloseDungeonInstanceRequest
{
    public string? Reason { get; init; }
}

internal sealed class DungeonInstanceDto
{
    public string instanceId { get; init; } = string.Empty;
    public string templateOwnerUserId { get; init; } = string.Empty;
    public string templateSaveId { get; init; } = string.Empty;
    public int levelIndex { get; init; }
    public int difficulty { get; init; }
    public int seed { get; init; }
    public int maxPlayers { get; init; }
    public int realtimeUdpPort { get; init; }
    public int realtimeKcpPort { get; init; }
    public string status { get; init; } = string.Empty;
    public string closeReason { get; init; } = string.Empty;
    public DateTime createdAtUtc { get; init; }
    public DateTime updatedAtUtc { get; init; }
    public List<DungeonParticipantDto> participants { get; init; } = new();
}

internal sealed class DungeonParticipantDto
{
    public string userId { get; init; } = string.Empty;
    public string saveId { get; init; } = string.Empty;
    public string displayName { get; init; } = string.Empty;
    public bool isConnected { get; init; }
    public float currentHp { get; init; }
    public float maxHp { get; init; }
    public bool isDead { get; init; }
    public DateTime joinedAtUtc { get; init; }
    public DateTime lastSeenAtUtc { get; init; }
    public DateTime? diedAtUtc { get; init; }
}

internal sealed class DungeonPlayerPoseListDto
{
    public List<DungeonPlayerPoseDto> poses { get; init; } = new();
}

internal sealed class DungeonPlayerPoseDto
{
    public string userId { get; init; } = string.Empty;
    public string displayName { get; init; } = string.Empty;
    public float x { get; init; }
    public float y { get; init; }
    public float z { get; init; }
    public float yaw { get; init; }
    public bool aimActive { get; init; }
    public bool aimLayerActive { get; init; }
    public float aimPitch { get; init; }
    public float aimDirectionX { get; init; }
    public float aimDirectionY { get; init; }
    public float aimDirectionZ { get; init; }
    public float moveSpeed { get; init; }
    public float moveX { get; init; }
    public float moveY { get; init; }
    public int action { get; init; }
    public string actionId { get; init; } = string.Empty;
    public string skillEffectId { get; init; } = string.Empty;
    public int actionSequence { get; init; }
    public float actionElapsedSeconds { get; init; }
    public float actionNormalizedProgress { get; init; }
    public int attackMode { get; init; }
    public bool hasMeleeWeapon { get; init; }
    public bool hasRangedWeapon { get; init; }
    public float defense { get; init; }
    public float damageReduce { get; init; }
    public float currentHp { get; init; }
    public float maxHp { get; init; }
    public bool isDead { get; init; }
    public DateTime updatedAtUtc { get; init; }
}

internal sealed class DungeonAuthorityStateDto
{
    public long version { get; init; }
    public bool initialized { get; init; }
    public List<DungeonEnemyStateDto> enemies { get; init; } = new();
    public List<DungeonChestStateDto> chests { get; init; } = new();
    public long rewardStateVersion { get; init; }
}

internal sealed class DungeonEnemyStateDto
{
    public string runtimeId { get; init; } = string.Empty;
    public string enemyId { get; init; } = string.Empty;
    public int enemyType { get; init; }
    public float x { get; init; }
    public float y { get; init; }
    public float z { get; init; }
    public float yaw { get; init; }
    public float currentHp { get; init; }
    public float currentPoise { get; init; }
    public bool countsAsLevelBoss { get; init; }
    public bool isDead { get; init; }
    public bool showCombatHealthBar { get; init; }
    public float moveBlend { get; init; }
    public float moveForward { get; init; }
    public float moveStrafe { get; init; }
    public int activeSkillSequence { get; init; }
    public int activeSkillSlot { get; init; }
    public string activeSkillId { get; init; } = string.Empty;
    public string activeSkillAnimationTrigger { get; init; } = string.Empty;
    public bool hasActiveSkillTarget { get; init; }
    public float activeSkillTargetX { get; init; }
    public float activeSkillTargetY { get; init; }
    public float activeSkillTargetZ { get; init; }
    public DateTime updatedAtUtc { get; init; }
    public DateTime? diedAtUtc { get; init; }
}

internal sealed class DungeonEnemyAuthorityStateUpsertDto
{
    public string runtimeId { get; init; } = string.Empty;
    public string enemyId { get; init; } = string.Empty;
    public int enemyType { get; init; }
    public float x { get; init; }
    public float y { get; init; }
    public float z { get; init; }
    public float yaw { get; init; }
    public float currentHp { get; init; }
    public float currentPoise { get; init; }
    public bool countsAsLevelBoss { get; init; }
    public bool isDead { get; init; }
    public bool showCombatHealthBar { get; init; }
    public float moveBlend { get; init; }
    public float moveForward { get; init; }
    public float moveStrafe { get; init; }
    public int activeSkillSequence { get; init; }
    public int activeSkillSlot { get; init; }
    public string activeSkillId { get; init; } = string.Empty;
    public string activeSkillAnimationTrigger { get; init; } = string.Empty;
    public bool hasActiveSkillTarget { get; init; }
    public float activeSkillTargetX { get; init; }
    public float activeSkillTargetY { get; init; }
    public float activeSkillTargetZ { get; init; }
}

internal sealed class DungeonChestStateDto
{
    public string chestId { get; init; } = string.Empty;
    public string prefabId { get; init; } = string.Empty;
    public float x { get; init; }
    public float y { get; init; }
    public float z { get; init; }
    public float yaw { get; init; }
    public bool opened { get; init; }
    public string openedByUserId { get; init; } = string.Empty;
    public DateTime? openedAtUtc { get; init; }
    public DateTime updatedAtUtc { get; init; }
}

internal sealed class DungeonUiPanelEventListDto
{
    public List<DungeonUiPanelEventDto> events { get; init; } = new();
}

internal sealed class DungeonUiPanelEventDto
{
    public string eventId { get; init; } = string.Empty;
    public long sequence { get; init; }
    public string sourceUserId { get; init; } = string.Empty;
    public string panelName { get; init; } = string.Empty;
    public bool open { get; init; }
    public DateTime createdAtUtc { get; init; }
}

internal sealed class DungeonUiPanelStateDto
{
    public long version { get; init; }
    public string openPanelName { get; init; } = string.Empty;
    public bool hasOpenPanel { get; init; }
    public string updatedByUserId { get; init; } = string.Empty;
    public DateTime updatedAtUtc { get; init; }
}

internal sealed class DungeonSceneLoadStateDto
{
    public long version { get; init; }
    public bool active { get; init; }
    public bool released { get; init; }
    public string transitionId { get; init; } = string.Empty;
    public string sceneName { get; init; } = string.Empty;
    public string bossId { get; init; } = string.Empty;
    public string bossDisplayName { get; init; } = string.Empty;
    public DateTime? releaseAtUtc { get; init; }
    public List<string> readyUserIds { get; init; } = new();
    public List<string> completedUserIds { get; init; } = new();
    public int participantCount { get; init; }
    public int readyCount { get; init; }
    public int completedCount { get; init; }
}

internal sealed class DungeonRewardStateDto
{
    public long version { get; init; }
    public List<DungeonKillRewardDto> killRewards { get; init; } = new();
    public List<DungeonDropDto> drops { get; init; } = new();
}

internal sealed class DungeonKillRewardDto
{
    public string enemyRuntimeId { get; init; } = string.Empty;
    public string killerUserId { get; init; } = string.Empty;
    public int exp { get; init; }
    public int gold { get; init; }
    public int talentPoints { get; init; }
    public float x { get; init; }
    public float y { get; init; }
    public float z { get; init; }
    public bool claimed { get; init; }
    public DateTime? claimedAtUtc { get; init; }
    public DateTime createdAtUtc { get; init; }
}

internal sealed class DungeonDropDto
{
    public string dropId { get; init; } = string.Empty;
    public string enemyRuntimeId { get; init; } = string.Empty;
    public string itemType { get; init; } = string.Empty;
    public string payloadJson { get; init; } = string.Empty;
    public int count { get; init; }
    public float x { get; init; }
    public float y { get; init; }
    public float z { get; init; }
    public bool pickedUp { get; init; }
    public string pickedUpByUserId { get; init; } = string.Empty;
    public DateTime? pickedUpAtUtc { get; init; }
    public DateTime createdAtUtc { get; init; }
}

internal sealed class DungeonDropPickupResultDto
{
    public bool accepted { get; init; }
    public DungeonDropDto? drop { get; init; }
    public DungeonRewardStateDto? state { get; init; }
}

internal sealed class DungeonKillRewardClaimResultDto
{
    public bool accepted { get; init; }
    public DungeonKillRewardDto? reward { get; init; }
    public DungeonRewardStateDto? state { get; init; }
}

internal sealed class DungeonDamageEventListDto
{
    public List<DungeonDamageEventDto> events { get; init; } = new();
}

internal sealed class DungeonDamageEventDto
{
    public string eventId { get; init; } = string.Empty;
    public long sequence { get; init; }
    public string sourceUserId { get; init; } = string.Empty;
    public string targetKind { get; init; } = string.Empty;
    public string targetRuntimeId { get; init; } = string.Empty;
    public float damage { get; init; }
    public float stunDuration { get; init; }
    public float targetRemainingHp { get; init; }
    public bool targetDied { get; init; }
    public bool targetShowCombatHealthBar { get; init; }
    public float x { get; init; }
    public float y { get; init; }
    public float z { get; init; }
    public DateTime createdAtUtc { get; init; }
}

internal sealed class DungeonWorldSnapshotDto
{
    public string ownerUserId { get; init; } = string.Empty;
    public string sceneName { get; init; } = string.Empty;
    public string snapshotJson { get; init; } = string.Empty;
    public long version { get; init; }
    public DateTime updatedAtUtc { get; init; }
}

internal sealed class ApiResponse<T>
{
    public bool success { get; init; }
    public string message { get; init; } = string.Empty;
    public T? data { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "")
    {
        return new ApiResponse<T>
        {
            success = true,
            message = message,
            data = data,
        };
    }

    public static ApiResponse<T> Fail(string message)
    {
        return new ApiResponse<T>
        {
            success = false,
            message = message,
            data = default,
        };
    }
}
