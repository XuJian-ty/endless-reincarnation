using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);
string[] urls = builder.Configuration.GetSection("Urls").Get<string[]>()
    ?? new[] { "http://0.0.0.0:5076" };
builder.WebHost.UseUrls(urls);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

string connectionString = builder.Configuration.GetConnectionString("SocialDatabase")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:SocialDatabase configuration.");
string mySqlServerVersion = builder.Configuration["Database:MySqlServerVersion"] ?? "8.4.0";

builder.Services.AddDbContextFactory<SocialDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.Parse(mySqlServerVersion));
});
builder.Services.AddHttpClient<OnlineDungeonClient>();
builder.Services.AddSingleton<SocialAppService>();

var app = builder.Build();
SocialDbInitializer.Initialize(app.Services);

app.MapGet("/api/health", () =>
{
    return Results.Ok(ApiResponse<object>.Ok(new
    {
        status = "ok",
        utcNow = DateTime.UtcNow,
        database = "mysql",
    }, "平台服务已启动"));
});

app.MapPost("/api/auth/register", (AuthRequest request, SocialAppService service) =>
{
    if (!service.TryRegister(request, out SocialUserDto? user, out string error))
        return Results.BadRequest(ApiResponse<SocialUserDto>.Fail(error));

    return Results.Ok(ApiResponse<SocialUserDto>.Ok(user!, "注册成功"));
});

app.MapPost("/api/auth/login", (AuthRequest request, SocialAppService service) =>
{
    if (!service.TryLogin(request, out SocialUserDto? user, out string error))
        return Results.BadRequest(ApiResponse<SocialUserDto>.Fail(error));

    return Results.Ok(ApiResponse<SocialUserDto>.Ok(user!, "登录成功"));
});

app.MapGet("/api/users/search", (string keyword, string requesterUserId, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(requesterUserId))
        return Results.BadRequest(ApiResponse<List<SocialUserSearchDto>>.Fail("缺少请求玩家标识"));

    List<SocialUserSearchDto> results = service.SearchUsers(keyword, requesterUserId);
    return Results.Ok(ApiResponse<List<SocialUserSearchDto>>.Ok(results, $"找到 {results.Count} 个账号"));
});

app.MapPost("/api/friends/add", (AddFriendRequest request, SocialAppService service) =>
{
    if (!service.TryAddFriend(request, out FriendRequestDto? friendRequest, out string error))
        return Results.BadRequest(ApiResponse<FriendRequestDto>.Fail(error));

    return Results.Ok(ApiResponse<FriendRequestDto>.Ok(friendRequest!, "好友申请已发送"));
});

app.MapGet("/api/friends/requests", (string userId, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(ApiResponse<List<FriendRequestDto>>.Fail("缺少玩家标识"));

    List<FriendRequestDto> requests = service.GetPendingFriendRequests(userId, out string error);
    if (!string.IsNullOrWhiteSpace(error))
        return Results.BadRequest(ApiResponse<List<FriendRequestDto>>.Fail(error));

    return Results.Ok(ApiResponse<List<FriendRequestDto>>.Ok(requests, $"获取到 {requests.Count} 条好友申请"));
});

app.MapPost("/api/friends/respond", (RespondFriendRequestRequest request, SocialAppService service) =>
{
    if (!service.TryRespondToFriendRequest(request, out SocialFriendDto? friend, out string error))
        return Results.BadRequest(ApiResponse<SocialFriendDto>.Fail(error));

    return Results.Ok(ApiResponse<SocialFriendDto>.Ok(friend!, "好友申请已处理"));
});

app.MapPost("/api/friends/remove", (RemoveFriendRequest request, SocialAppService service) =>
{
    if (!service.TryRemoveFriend(request, out string error))
        return Results.BadRequest(ApiResponse<object>.Fail(error));

    return Results.Ok(ApiResponse<object>.Ok(new { request.friendUserId }, "好友已删除"));
});

app.MapGet("/api/friends", (string userId, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(ApiResponse<List<SocialFriendDto>>.Fail("缺少玩家标识"));

    return Results.Ok(ApiResponse<List<SocialFriendDto>>.Ok(service.GetFriends(userId), "好友列表获取成功"));
});

app.MapGet("/api/friends/presence", (string userId, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(ApiResponse<List<FriendPresenceDto>>.Fail("缺少玩家标识"));

    List<FriendPresenceDto> friends = service.GetFriendPresenceSummaries(userId, out string error);
    if (!string.IsNullOrWhiteSpace(error))
        return Results.BadRequest(ApiResponse<List<FriendPresenceDto>>.Fail(error));

    return Results.Ok(ApiResponse<List<FriendPresenceDto>>.Ok(friends, "好友状态获取成功"));
});

app.MapPost("/api/messages/send", (SendMessageRequest request, SocialAppService service) =>
{
    if (!service.TrySendMessage(request, out SocialMessageDto? message, out string error))
        return Results.BadRequest(ApiResponse<SocialMessageDto>.Fail(error));

    return Results.Ok(ApiResponse<SocialMessageDto>.Ok(message!, "消息发送成功"));
});

app.MapGet("/api/messages/thread", (string userId, string friendUserId, long? afterMessageId, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(friendUserId))
        return Results.BadRequest(ApiResponse<List<SocialMessageDto>>.Fail("缺少会话双方标识"));

    long minMessageId = Math.Max(0L, afterMessageId ?? 0L);
    return Results.Ok(ApiResponse<List<SocialMessageDto>>.Ok(service.GetConversation(userId, friendUserId, minMessageId), "消息列表获取成功"));
});

app.MapGet("/api/saves", (string userId, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(ApiResponse<List<SaveSlotSummaryDto>>.Fail("缺少玩家标识"));

    List<SaveSlotSummaryDto> saves = service.GetSaveSummaries(userId, out string error);
    if (!string.IsNullOrWhiteSpace(error))
        return Results.BadRequest(ApiResponse<List<SaveSlotSummaryDto>>.Fail(error));

    return Results.Ok(ApiResponse<List<SaveSlotSummaryDto>>.Ok(saves, $"获取到 {saves.Count} 份存档"));
});

app.MapGet("/api/saves/{saveId}", (string saveId, string userId, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(saveId))
        return Results.BadRequest(ApiResponse<SaveSlotContentDto>.Fail("缺少玩家标识或存档标识"));

    if (!service.TryGetSave(userId, saveId, out SaveSlotContentDto? save, out string error))
        return Results.BadRequest(ApiResponse<SaveSlotContentDto>.Fail(error));

    return Results.Ok(ApiResponse<SaveSlotContentDto>.Ok(save!, "存档读取成功"));
});

app.MapPost("/api/saves/{saveId}", (string saveId, UpsertSaveRequest request, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(saveId))
        return Results.BadRequest(ApiResponse<SaveSlotContentDto>.Fail("缺少存档标识"));

    if (!service.UpsertSave(saveId, request, out SaveSlotContentDto? save, out string error))
        return Results.BadRequest(ApiResponse<SaveSlotContentDto>.Fail(error));

    return Results.Ok(ApiResponse<SaveSlotContentDto>.Ok(save!, "存档保存成功"));
});

app.MapDelete("/api/saves/{saveId}", (string saveId, string userId, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(saveId) || string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(ApiResponse<object>.Fail("缺少玩家标识或存档标识"));

    if (!service.TryDeleteSave(userId, saveId, out string error))
        return Results.BadRequest(ApiResponse<object>.Fail(error));

    return Results.Ok(ApiResponse<object>.Ok(new { saveId }, "存档删除成功"));
});

app.MapPost("/api/active-save", (SetActiveSaveContextRequest request, SocialAppService service) =>
{
    if (!service.TrySetActiveSaveContext(request, out ActiveSaveContextDto? context, out string error))
        return Results.BadRequest(ApiResponse<ActiveSaveContextDto>.Fail(error));

    return Results.Ok(ApiResponse<ActiveSaveContextDto>.Ok(context!, "活跃存档已更新"));
});

app.MapPost("/api/active-save/clear", (ClearActiveSaveContextRequest request, SocialAppService service) =>
{
    if (!service.TryClearActiveSaveContext(request, out string error))
        return Results.BadRequest(ApiResponse<object>.Fail(error));

    return Results.Ok(ApiResponse<object>.Ok(new { request.userId }, "活跃存档已清除"));
});

app.MapGet("/api/active-save", (string userId, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(ApiResponse<ActiveSaveContextDto>.Fail("缺少玩家标识"));

    if (!service.TryGetActiveSaveContext(userId, out ActiveSaveContextDto? context, out string error))
        return Results.BadRequest(ApiResponse<ActiveSaveContextDto>.Fail(error));

    return Results.Ok(ApiResponse<ActiveSaveContextDto>.Ok(context!, "活跃存档获取成功"));
});

app.MapPost("/api/aid/request", (CreateAidRequestRequest request, SocialAppService service) =>
{
    if (!service.TryCreateAidRequest(request, out AidRequestDto? aidRequest, out string error))
        return Results.BadRequest(ApiResponse<AidRequestDto>.Fail(error));

    return Results.Ok(ApiResponse<AidRequestDto>.Ok(aidRequest!, "援助请求已发送"));
});

app.MapGet("/api/aid/requests", (string helperUserId, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(helperUserId))
        return Results.BadRequest(ApiResponse<List<AidRequestDto>>.Fail("缺少被请求援助玩家标识"));

    List<AidRequestDto> requests = service.GetPendingAidRequests(helperUserId, out string error);
    if (!string.IsNullOrWhiteSpace(error))
        return Results.BadRequest(ApiResponse<List<AidRequestDto>>.Fail(error));

    return Results.Ok(ApiResponse<List<AidRequestDto>>.Ok(requests, $"获取到 {requests.Count} 条援助请求"));
});

app.MapPost("/api/aid/respond", (RespondAidRequestRequest request, HttpContext httpContext, SocialAppService service) =>
{
    if (!service.TryRespondToAidRequest(request, httpContext.Request.Host.Host, out AidSessionDto? aidSession, out string error))
        return Results.BadRequest(ApiResponse<AidSessionDto>.Fail(error));

    return Results.Ok(ApiResponse<AidSessionDto>.Ok(aidSession!, "援助请求已处理"));
});

app.MapGet("/api/aid/session", (string userId, SocialAppService service) =>
{
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(ApiResponse<AidSessionDto>.Fail("缺少玩家标识"));

    if (!service.TryGetActiveAidSession(userId, out AidSessionDto? aidSession, out string error))
    {
        if (string.Equals(error, "当前没有活跃援助会话", StringComparison.Ordinal))
            return Results.Ok(ApiResponse<AidSessionDto?>.Ok(null, error));

        return Results.BadRequest(ApiResponse<AidSessionDto>.Fail(error));
    }

    return Results.Ok(ApiResponse<AidSessionDto>.Ok(aidSession!, "活跃援助会话获取成功"));
});

app.MapPost("/api/aid/session/close", (CloseAidSessionRequest request, SocialAppService service) =>
{
    if (!service.TryCloseAidSession(request, out AidSessionDto? aidSession, out string error))
        return Results.BadRequest(ApiResponse<AidSessionDto>.Fail(error));

    return Results.Ok(ApiResponse<AidSessionDto>.Ok(aidSession!, "援助会话已结束"));
});

app.Run();
