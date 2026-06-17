using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

internal sealed class SocialAppService
{
    private const string AidRequestPending = "pending";
    private const string AidRequestAccepted = "accepted";
    private const string AidRequestRejected = "rejected";
    private const string AidRequestCancelled = "cancelled";
    private const string AidSessionActive = "active";
    private const string AidSessionClosed = "closed";
    private const string FriendRequestPending = "pending";
    private const string FriendRequestAccepted = "accepted";
    private const string FriendRequestRejected = "rejected";
    private static readonly TimeSpan ActivePresenceWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PendingAidRequestWindow = TimeSpan.FromMinutes(2);

    private readonly IDbContextFactory<SocialDbContext> _dbContextFactory;
    private readonly OnlineDungeonClient _onlineDungeonClient;
    private readonly string _defaultDungeonServerUrl;

    public SocialAppService(
        IDbContextFactory<SocialDbContext> dbContextFactory,
        OnlineDungeonClient onlineDungeonClient,
        IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _onlineDungeonClient = onlineDungeonClient;
        _defaultDungeonServerUrl = NormalizeServerUrl(configuration["OnlineDungeon:DefaultServerUrl"], "http://127.0.0.1:5086");
    }

    private string ResolveDungeonServerUrl(string requestHost)
    {
        string host = string.IsNullOrWhiteSpace(requestHost) ? "127.0.0.1" : requestHost.Trim();
        int colonIndex = host.IndexOf(':');
        if (colonIndex >= 0)
            host = host[..colonIndex];

        if (string.IsNullOrWhiteSpace(host))
            host = "127.0.0.1";

        if (string.Equals(host, "127.0.0.1", StringComparison.Ordinal) ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return _defaultDungeonServerUrl;

        return $"http://{host}:5086";
    }

    private static string NormalizeServerUrl(string? value, string fallback)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.TrimEnd('/');
    }

    public bool TryRegister(AuthRequest request, out SocialUserDto? user, out string error)
    {
        user = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        if (!TryNormalizeCredentials(request, out string username, out string password, out error))
            return false;

        string normalizedUsername = NormalizeUsername(username);
        if (db.Users.Any(entity => entity.NormalizedUsername == normalizedUsername))
        {
            error = "该账号名已被使用";
            return false;
        }

        SocialUserEntity entity = new()
        {
            UserId = Guid.NewGuid().ToString("N"),
            Username = username,
            NormalizedUsername = normalizedUsername,
            PasswordHash = HashPassword(password),
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Users.Add(entity);
        db.SaveChanges();
        user = ToUserDto(entity);
        error = string.Empty;
        return true;
    }

    public bool TryLogin(AuthRequest request, out SocialUserDto? user, out string error)
    {
        user = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        if (!TryNormalizeCredentials(request, out string username, out string password, out error))
            return false;

        string normalizedUsername = NormalizeUsername(username);
        SocialUserEntity? entity = db.Users.FirstOrDefault(record => record.NormalizedUsername == normalizedUsername);
        if (entity == null || !VerifyPassword(password, entity.PasswordHash))
        {
            error = "账号名或密码错误";
            return false;
        }

        user = ToUserDto(entity);
        error = string.Empty;
        return true;
    }

    public List<SocialUserSearchDto> SearchUsers(string keyword, string requesterUserId)
    {
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedRequesterUserId = NormalizeRequiredValue(requesterUserId);
        string normalizedKeyword = NormalizeUsername(keyword ?? string.Empty);
        HashSet<string> friendIds = CollectFriendIds(db, normalizedRequesterUserId);
        HashSet<string> pendingRequestUserIds = db.FriendRequests
            .Where(entity => entity.Status == FriendRequestPending
                             && (entity.RequesterUserId == normalizedRequesterUserId || entity.TargetUserId == normalizedRequesterUserId))
            .ToList()
            .Select(entity => entity.RequesterUserId == normalizedRequesterUserId ? entity.TargetUserId : entity.RequesterUserId)
            .ToHashSet(StringComparer.Ordinal);

        IQueryable<SocialUserEntity> query = db.Users.Where(user => user.UserId != normalizedRequesterUserId);
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
            query = query.Where(user => user.NormalizedUsername.Contains(normalizedKeyword));

        List<SocialUserEntity> users = query
            .OrderBy(user => user.NormalizedUsername)
            .Take(20)
            .ToList();

        return users
            .Select(user => new SocialUserSearchDto
            {
                userId = user.UserId,
                username = user.Username,
                isFriend = friendIds.Contains(user.UserId),
                hasPendingFriendRequest = pendingRequestUserIds.Contains(user.UserId),
            })
            .ToList();
    }

    public bool TryAddFriend(AddFriendRequest request, out FriendRequestDto? friendRequest, out string error)
    {
        friendRequest = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();

        string requesterUserId = NormalizeRequiredValue(request.requesterUserId);
        string targetUsername = NormalizeRequiredValue(request.targetUsername);
        string message = NormalizeFriendRequestMessage(request.message);
        string remark = NormalizeOptionalValue(request.remark);
        SocialUserEntity? requester = db.Users.FirstOrDefault(user => user.UserId == requesterUserId);
        if (requester == null)
        {
            error = "请求玩家不存在";
            return false;
        }

        if (string.IsNullOrWhiteSpace(targetUsername))
        {
            error = "请输入要添加的账号名";
            return false;
        }

        string normalizedTargetUsername = NormalizeUsername(targetUsername);
        SocialUserEntity? target = db.Users.FirstOrDefault(user => user.NormalizedUsername == normalizedTargetUsername);
        if (target == null)
        {
            error = "未找到该账号";
            return false;
        }

        if (requester.UserId == target.UserId)
        {
            error = "不能添加自己为好友";
            return false;
        }

        if (AreFriends(db, requester.UserId, target.UserId))
        {
            error = "你们已经是好友了";
            return false;
        }

        FriendRequestEntity? existingPending = db.FriendRequests.FirstOrDefault(entity =>
            entity.Status == FriendRequestPending &&
            ((entity.RequesterUserId == requester.UserId && entity.TargetUserId == target.UserId) ||
             (entity.RequesterUserId == target.UserId && entity.TargetUserId == requester.UserId)));
        if (existingPending != null)
        {
            error = existingPending.RequesterUserId == requester.UserId
                ? "好友申请已发送，请等待对方处理"
                : "对方已向你发送好友申请，请到新朋友中处理";
            return false;
        }

        DateTime now = DateTime.UtcNow;
        FriendRequestEntity entity = new FriendRequestEntity
        {
            RequestId = Guid.NewGuid().ToString("N"),
            RequesterUserId = requester.UserId,
            TargetUserId = target.UserId,
            Status = FriendRequestPending,
            Message = message,
            RequesterRemark = remark,
            TargetRemark = string.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.FriendRequests.Add(entity);
        db.SaveChanges();

        friendRequest = ToFriendRequestDto(db, entity);
        error = string.Empty;
        return true;
    }

    public List<SocialFriendDto> GetFriends(string userId)
    {
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedUserId = NormalizeRequiredValue(userId);
        Dictionary<string, string> friendRemarks = CollectFriendRemarks(db, normalizedUserId);
        HashSet<string> friendIds = friendRemarks.Keys.ToHashSet(StringComparer.Ordinal);
        if (friendIds.Count == 0)
            return new List<SocialFriendDto>();

        return db.Users
            .Where(user => friendIds.Contains(user.UserId))
            .OrderBy(user => user.NormalizedUsername)
            .ToList()
            .Select(user => new SocialFriendDto
            {
                userId = user.UserId,
                username = user.Username,
                remark = friendRemarks.TryGetValue(user.UserId, out string? remark) ? remark ?? string.Empty : string.Empty,
            })
            .ToList();
    }

    public List<FriendPresenceDto> GetFriendPresenceSummaries(string userId, out string error)
    {
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedUserId = NormalizeRequiredValue(userId);
        Dictionary<string, string> friendRemarks = CollectFriendRemarks(db, normalizedUserId);
        HashSet<string> friendIds = friendRemarks.Keys.ToHashSet(StringComparer.Ordinal);
        if (friendIds.Count == 0)
        {
            error = string.Empty;
            return new List<FriendPresenceDto>();
        }

        List<SocialUserEntity> friends = db.Users
            .Where(user => friendIds.Contains(user.UserId))
            .OrderBy(user => user.NormalizedUsername)
            .ToList();

        Dictionary<string, ActiveSaveContextEntity> activeContexts = db.ActiveSaveContexts
            .Where(context => friendIds.Contains(context.UserId))
            .ToDictionary(context => context.UserId, context => context, StringComparer.Ordinal);

        Dictionary<string, SaveSlotEntity> latestSlots = db.SaveSlots
            .Where(slot => friendIds.Contains(slot.UserId))
            .AsEnumerable()
            .GroupBy(slot => slot.UserId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(slot => slot.UpdatedAtUtc).First(),
                StringComparer.Ordinal);

        HashSet<string> activeHelpers = db.AidSessions
            .Where(session => session.Status == AidSessionActive)
            .Select(session => session.HelperUserId)
            .ToHashSet(StringComparer.Ordinal);

        List<FriendPresenceDto> results = new List<FriendPresenceDto>(friends.Count);
        for (int i = 0; i < friends.Count; i++)
        {
            SocialUserEntity friend = friends[i];
            bool hasActiveContext = activeContexts.TryGetValue(friend.UserId, out ActiveSaveContextEntity? activeContext);
            bool isOnline = hasActiveContext
                            && activeContext != null
                            && DateTime.UtcNow - activeContext.UpdatedAtUtc <= ActivePresenceWindow;
            SaveSlotEntity? latestSlot = null;
            latestSlots.TryGetValue(friend.UserId, out latestSlot);

            string? playerName = isOnline && activeContext != null
                ? activeContext.PlayerName
                : latestSlot?.PlayerName;
            string? portraitId = isOnline && activeContext != null
                ? activeContext.PortraitId
                : latestSlot?.PortraitId;
            int levelIndex = isOnline && activeContext != null
                ? activeContext.LevelIndex
                : (latestSlot != null ? latestSlot.LevelIndex : 1);

            results.Add(new FriendPresenceDto
            {
                userId = friend.UserId,
                username = friend.Username,
                remark = friendRemarks.TryGetValue(friend.UserId, out string? remark) ? remark ?? string.Empty : string.Empty,
                playerName = string.IsNullOrWhiteSpace(playerName) ? friend.Username : playerName,
                portraitId = string.IsNullOrWhiteSpace(portraitId) ? "UI图片/天依" : portraitId,
                levelIndex = Math.Max(1, levelIndex),
                isOnline = isOnline,
                isHelping = activeHelpers.Contains(friend.UserId),
            });
        }

        error = string.Empty;
        return results;
    }

    public List<FriendRequestDto> GetPendingFriendRequests(string targetUserId, out string error)
    {
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedTargetUserId = NormalizeRequiredValue(targetUserId);
        if (string.IsNullOrWhiteSpace(normalizedTargetUserId))
        {
            error = "缺少玩家标识";
            return new List<FriendRequestDto>();
        }

        error = string.Empty;
        return db.FriendRequests
            .Where(entity => entity.TargetUserId == normalizedTargetUserId && entity.Status == FriendRequestPending)
            .OrderByDescending(entity => entity.CreatedAtUtc)
            .ToList()
            .Select(entity => ToFriendRequestDto(db, entity))
            .ToList();
    }

    public bool TryRespondToFriendRequest(RespondFriendRequestRequest request, out SocialFriendDto? friend, out string error)
    {
        friend = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();

        string targetUserId = NormalizeRequiredValue(request.targetUserId);
        string requestId = NormalizeRequiredValue(request.requestId);
        string decision = NormalizeRequiredValue(request.decision).ToLowerInvariant();
        string remark = NormalizeOptionalValue(request.remark);

        FriendRequestEntity? friendRequest = db.FriendRequests.FirstOrDefault(entity =>
            entity.RequestId == requestId && entity.TargetUserId == targetUserId);
        if (friendRequest == null)
        {
            error = "未找到该好友申请";
            return false;
        }

        if (friendRequest.Status != FriendRequestPending)
        {
            error = "该好友申请已处理";
            return false;
        }

        if (decision == "reject")
        {
            friendRequest.Status = FriendRequestRejected;
            friendRequest.TargetRemark = remark;
            friendRequest.UpdatedAtUtc = DateTime.UtcNow;
            db.SaveChanges();
            error = string.Empty;
            return true;
        }

        if (decision != "accept")
        {
            error = "无效的好友申请响应类型";
            return false;
        }

        if (AreFriends(db, friendRequest.RequesterUserId, friendRequest.TargetUserId))
        {
            friendRequest.Status = FriendRequestAccepted;
            friendRequest.TargetRemark = remark;
            friendRequest.UpdatedAtUtc = DateTime.UtcNow;
            db.SaveChanges();
            error = "你们已经是好友了";
            return false;
        }

        string userAId = MinUserId(friendRequest.RequesterUserId, friendRequest.TargetUserId);
        string userBId = MaxUserId(friendRequest.RequesterUserId, friendRequest.TargetUserId);
        db.FriendLinks.Add(new FriendLinkEntity
        {
            UserAId = userAId,
            UserBId = userBId,
            UserARemark = userAId == friendRequest.RequesterUserId ? friendRequest.RequesterRemark : remark,
            UserBRemark = userBId == friendRequest.RequesterUserId ? friendRequest.RequesterRemark : remark,
            CreatedAtUtc = DateTime.UtcNow,
        });

        friendRequest.Status = FriendRequestAccepted;
        friendRequest.TargetRemark = remark;
        friendRequest.UpdatedAtUtc = DateTime.UtcNow;
        db.SaveChanges();

        SocialUserEntity? requester = db.Users.FirstOrDefault(user => user.UserId == friendRequest.RequesterUserId);
        if (requester == null)
        {
            error = "申请方账号不存在";
            return false;
        }

        friend = ToFriendDto(requester, remark);
        error = string.Empty;
        return true;
    }

    public bool TryRemoveFriend(RemoveFriendRequest request, out string error)
    {
        using SocialDbContext db = _dbContextFactory.CreateDbContext();

        string requesterUserId = NormalizeRequiredValue(request.requesterUserId);
        string friendUserId = NormalizeRequiredValue(request.friendUserId);
        if (string.IsNullOrWhiteSpace(requesterUserId) || string.IsNullOrWhiteSpace(friendUserId))
        {
            error = "缺少好友双方标识";
            return false;
        }

        string userAId = MinUserId(requesterUserId, friendUserId);
        string userBId = MaxUserId(requesterUserId, friendUserId);
        FriendLinkEntity? link = db.FriendLinks.FirstOrDefault(entity =>
            entity.UserAId == userAId && entity.UserBId == userBId);
        if (link == null)
        {
            error = "你们还不是好友";
            return false;
        }

        db.FriendLinks.Remove(link);
        db.SaveChanges();

        error = string.Empty;
        return true;
    }

    public bool TrySendMessage(SendMessageRequest request, out SocialMessageDto? message, out string error)
    {
        message = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();

        string fromUserId = NormalizeRequiredValue(request.fromUserId);
        string toUserId = NormalizeRequiredValue(request.toUserId);
        string content = NormalizeRequiredValue(request.content);

        bool usersExist = db.Users.Any(user => user.UserId == fromUserId)
            && db.Users.Any(user => user.UserId == toUserId);
        if (!usersExist)
        {
            error = "发送双方账号不存在";
            return false;
        }

        if (!AreFriends(db, fromUserId, toUserId))
        {
            error = "只有好友之间才能发消息";
            return false;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            error = "消息内容不能为空";
            return false;
        }

        if (content.Length > 200)
        {
            error = "消息内容不能超过 200 个字符";
            return false;
        }

        SocialMessageEntity entity = new()
        {
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Content = content,
            SentAtUtc = DateTime.UtcNow,
        };

        db.Messages.Add(entity);
        db.SaveChanges();
        message = ToMessageDto(entity);
        error = string.Empty;
        return true;
    }

    public List<SocialMessageDto> GetConversation(string userId, string friendUserId, long afterMessageId)
    {
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedUserId = NormalizeRequiredValue(userId);
        string normalizedFriendUserId = NormalizeRequiredValue(friendUserId);
        if (!AreFriends(db, normalizedUserId, normalizedFriendUserId))
            return new List<SocialMessageDto>();

        return db.Messages
            .Where(message =>
                message.MessageId > afterMessageId &&
                ((message.FromUserId == normalizedUserId && message.ToUserId == normalizedFriendUserId) ||
                 (message.FromUserId == normalizedFriendUserId && message.ToUserId == normalizedUserId)))
            .OrderBy(message => message.MessageId)
            .Select(message => new SocialMessageDto
            {
                messageId = message.MessageId,
                fromUserId = message.FromUserId,
                toUserId = message.ToUserId,
                content = message.Content,
                sentAtUtc = message.SentAtUtc.ToString("O"),
            })
            .ToList();
    }

    public List<SaveSlotSummaryDto> GetSaveSummaries(string userId, out string error)
    {
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedUserId = NormalizeRequiredValue(userId);
        if (!db.Users.Any(user => user.UserId == normalizedUserId))
        {
            error = "请求玩家不存在";
            return new List<SaveSlotSummaryDto>();
        }

        error = string.Empty;
        return db.SaveSlots
            .Where(slot => slot.UserId == normalizedUserId)
            .OrderByDescending(slot => slot.UpdatedAtUtc)
            .Select(ToSaveSummaryDto)
            .ToList();
    }

    public bool TryGetSave(string userId, string saveId, out SaveSlotContentDto? save, out string error)
    {
        save = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedUserId = NormalizeRequiredValue(userId);
        string normalizedSaveId = NormalizeRequiredValue(saveId);

        SaveSlotEntity? slot = db.SaveSlots.FirstOrDefault(entity =>
            entity.UserId == normalizedUserId && entity.SaveId == normalizedSaveId);
        if (slot == null)
        {
            error = "未找到该存档";
            return false;
        }

        save = ToSaveContentDto(slot);
        error = string.Empty;
        return true;
    }

    public bool UpsertSave(string saveId, UpsertSaveRequest request, out SaveSlotContentDto? save, out string error)
    {
        save = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();

        string normalizedSaveId = NormalizeRequiredValue(saveId);
        string normalizedUserId = NormalizeRequiredValue(request.userId);
        string normalizedPlayerName = NormalizePlayerName(request.playerName);
        string normalizedPortraitId = NormalizePortraitId(request.portraitId);
        string saveJson = request.saveJson?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(saveJson))
        {
            error = "存档内容不能为空";
            return false;
        }

        if (!db.Users.Any(user => user.UserId == normalizedUserId))
        {
            error = "请求玩家不存在";
            return false;
        }

        SaveSlotEntity? slot = db.SaveSlots.FirstOrDefault(entity => entity.SaveId == normalizedSaveId);
        if (slot != null && slot.UserId != normalizedUserId)
        {
            error = "该存档不属于当前账号";
            return false;
        }

        DateTime now = DateTime.UtcNow;
        if (slot == null)
        {
            slot = new SaveSlotEntity
            {
                SaveId = normalizedSaveId,
                UserId = normalizedUserId,
                CreatedAtUtc = now,
            };
            db.SaveSlots.Add(slot);
        }

        slot.PlayerName = normalizedPlayerName;
        slot.PortraitId = normalizedPortraitId;
        slot.LevelIndex = Math.Max(1, request.levelIndex);
        slot.Version = Math.Max(1, request.version);
        slot.SaveJson = saveJson;
        slot.UpdatedAtUtc = now;

        db.SaveChanges();
        save = ToSaveContentDto(slot);
        error = string.Empty;
        return true;
    }

    public bool TryDeleteSave(string userId, string saveId, out string error)
    {
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedUserId = NormalizeRequiredValue(userId);
        string normalizedSaveId = NormalizeRequiredValue(saveId);

        SaveSlotEntity? slot = db.SaveSlots.FirstOrDefault(entity =>
            entity.UserId == normalizedUserId && entity.SaveId == normalizedSaveId);
        if (slot == null)
        {
            error = "未找到该存档";
            return false;
        }

        db.SaveSlots.Remove(slot);
        db.SaveChanges();
        error = string.Empty;
        return true;
    }

    public bool TrySetActiveSaveContext(SetActiveSaveContextRequest request, out ActiveSaveContextDto? context, out string error)
    {
        context = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();

        string userId = NormalizeRequiredValue(request.userId);
        string saveId = NormalizeRequiredValue(request.saveId);
        SaveSlotEntity? slot = db.SaveSlots.FirstOrDefault(entity => entity.UserId == userId && entity.SaveId == saveId);
        if (slot == null)
        {
            error = "未找到该存档";
            return false;
        }

        ActiveSaveContextEntity? existing = db.ActiveSaveContexts.FirstOrDefault(entity => entity.UserId == userId);
        if (existing == null)
        {
            existing = new ActiveSaveContextEntity
            {
                UserId = userId,
            };
            db.ActiveSaveContexts.Add(existing);
        }

        existing.SaveId = slot.SaveId;
        existing.PlayerName = slot.PlayerName;
        existing.PortraitId = slot.PortraitId;
        existing.LevelIndex = slot.LevelIndex;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        db.SaveChanges();

        context = ToActiveSaveContextDto(existing);
        error = string.Empty;
        return true;
    }

    public bool TryGetActiveSaveContext(string userId, out ActiveSaveContextDto? context, out string error)
    {
        context = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedUserId = NormalizeRequiredValue(userId);
        ActiveSaveContextEntity? existing = db.ActiveSaveContexts.FirstOrDefault(entity => entity.UserId == normalizedUserId);
        if (existing == null)
        {
            error = "当前没有活跃存档";
            return false;
        }

        context = ToActiveSaveContextDto(existing);
        error = string.Empty;
        return true;
    }

    public bool TryClearActiveSaveContext(ClearActiveSaveContextRequest request, out string error)
    {
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedUserId = NormalizeRequiredValue(request.userId);
        if (string.IsNullOrWhiteSpace(normalizedUserId))
        {
            error = "缺少玩家标识";
            return false;
        }

        ActiveSaveContextEntity? existing = db.ActiveSaveContexts.FirstOrDefault(entity => entity.UserId == normalizedUserId);
        if (existing != null)
        {
            db.ActiveSaveContexts.Remove(existing);
        }

        CloseActiveAidSessionsForUser(db, normalizedUserId);
        db.SaveChanges();
        error = string.Empty;
        return true;
    }

    public bool TryCreateAidRequest(CreateAidRequestRequest request, out AidRequestDto? aidRequest, out string error)
    {
        aidRequest = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();

        string hostUserId = NormalizeRequiredValue(request.hostUserId);
        string helperUserId = NormalizeRequiredValue(request.helperUserId);
        string message = NormalizeRequiredValue(request.message);
        if (string.IsNullOrWhiteSpace(hostUserId) || string.IsNullOrWhiteSpace(helperUserId))
        {
            error = "缺少求援双方标识";
            return false;
        }

        if (hostUserId == helperUserId)
        {
            error = "不能向自己发起援助请求";
            return false;
        }

        if (!AreFriends(db, hostUserId, helperUserId))
        {
            error = "只能向好友发起援助请求";
            return false;
        }

        ActiveSaveContextEntity? hostContext = db.ActiveSaveContexts.FirstOrDefault(entity => entity.UserId == hostUserId);
        if (hostContext == null)
        {
            error = "请求方当前没有活跃存档";
            return false;
        }

        CancelExpiredOrDuplicatePendingRequests(db, hostUserId, helperUserId);

        SocialUserEntity? helperUser = db.Users.FirstOrDefault(entity => entity.UserId == helperUserId);
        if (helperUser == null)
        {
            error = "未找到被请求援助的好友";
            return false;
        }

        SocialUserEntity? hostUser = db.Users.FirstOrDefault(entity => entity.UserId == hostUserId);
        if (hostUser == null)
        {
            error = "请求方账号不存在";
            return false;
        }

        DateTime now = DateTime.UtcNow;
        AidRequestEntity entity = new()
        {
            RequestId = Guid.NewGuid().ToString("N"),
            HostUserId = hostUserId,
            HostSaveId = hostContext.SaveId,
            HostPlayerName = string.IsNullOrWhiteSpace(hostContext.PlayerName) ? hostUser.Username : hostContext.PlayerName,
            HostLevelIndex = Math.Max(1, hostContext.LevelIndex),
            HostPlayerX = request.hostPlayerX,
            HostPlayerY = request.hostPlayerY,
            HostPlayerZ = request.hostPlayerZ,
            HostPlayerYaw = request.hostPlayerYaw,
            HelperUserId = helperUserId,
            HelperPlayerName = helperUser.Username,
            Status = AidRequestPending,
            Message = message,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.AidRequests.Add(entity);
        db.SaveChanges();
        aidRequest = ToAidRequestDto(entity);
        error = string.Empty;
        return true;
    }

    public List<AidRequestDto> GetPendingAidRequests(string helperUserId, out string error)
    {
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedHelperUserId = NormalizeRequiredValue(helperUserId);
        if (string.IsNullOrWhiteSpace(normalizedHelperUserId))
        {
            error = "缺少被请求援助玩家标识";
            return new List<AidRequestDto>();
        }

        error = string.Empty;
        return db.AidRequests
            .Where(entity => entity.HelperUserId == normalizedHelperUserId && entity.Status == AidRequestPending)
            .OrderByDescending(entity => entity.CreatedAtUtc)
            .Select(ToAidRequestDto)
            .ToList();
    }

    public bool TryRespondToAidRequest(RespondAidRequestRequest request, string requestHost, out AidSessionDto? aidSession, out string error)
    {
        aidSession = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();

        string helperUserId = NormalizeRequiredValue(request.helperUserId);
        string requestId = NormalizeRequiredValue(request.requestId);
        string decision = NormalizeRequiredValue(request.decision).ToLowerInvariant();

        AidRequestEntity? aidRequest = db.AidRequests.FirstOrDefault(entity =>
            entity.RequestId == requestId && entity.HelperUserId == helperUserId);
        if (aidRequest == null)
        {
            error = "未找到该援助请求";
            return false;
        }

        if (aidRequest.Status != AidRequestPending)
        {
            error = "该援助请求已处理";
            return false;
        }

        if (decision == "reject")
        {
            aidRequest.Status = AidRequestRejected;
            aidRequest.UpdatedAtUtc = DateTime.UtcNow;
            db.SaveChanges();
            error = string.Empty;
            return true;
        }

        if (decision != "accept")
        {
            error = "无效的援助响应类型";
            return false;
        }

        if (HasActiveAidBinding(db, aidRequest.HostUserId, aidRequest.RequestId) ||
            HasActiveAidBinding(db, aidRequest.HelperUserId, aidRequest.RequestId))
        {
            error = "当前已有未结束的援助请求或援助会话";
            return false;
        }

        ActiveSaveContextEntity? helperContext = db.ActiveSaveContexts.FirstOrDefault(entity => entity.UserId == helperUserId);
        if (helperContext == null)
        {
            error = "当前没有活跃存档，无法接受援助请求";
            return false;
        }

        if (helperContext.LevelIndex < aidRequest.HostLevelIndex)
        {
            error = "当前存档关卡等级不足，不能支援更高关卡的好友";
            return false;
        }

        string sessionId = Guid.NewGuid().ToString("N");
        string hostJoinToken = Guid.NewGuid().ToString("N");
        string helperJoinToken = Guid.NewGuid().ToString("N");
        string dungeonServerUrl = ResolveDungeonServerUrl(requestHost);
        if (!_onlineDungeonClient.TryCreateInstance(
                dungeonServerUrl,
                new CreateDungeonInstanceRequest
                {
                    instanceId = sessionId,
                    templateOwnerUserId = aidRequest.HostUserId,
                    templateSaveId = aidRequest.HostSaveId,
                    levelIndex = aidRequest.HostLevelIndex,
                    difficulty = 1,
                    seed = StablePositiveSeed(sessionId),
                    maxPlayers = 4,
                },
                out DungeonInstanceInfo? dungeonInstance,
                out error))
        {
            error = $"创建联机副本失败：{error}";
            return false;
        }

        string dungeonInstanceId = dungeonInstance?.instanceId ?? sessionId;
        if (dungeonInstance == null || dungeonInstance.realtimeUdpPort <= 0 || dungeonInstance.realtimeKcpPort <= 0)
        {
            error = "联机副本服务未返回实时通信端口";
            return false;
        }

        if (!_onlineDungeonClient.TryJoinInstance(
                dungeonServerUrl,
                dungeonInstanceId,
                new JoinDungeonInstanceRequest
                {
                    userId = aidRequest.HostUserId,
                    saveId = aidRequest.HostSaveId,
                    displayName = aidRequest.HostPlayerName,
                    joinToken = hostJoinToken,
                },
                out error))
        {
            error = $"登记被援助方副本参与者失败：{error}";
            return false;
        }

        if (!_onlineDungeonClient.TryJoinInstance(
                dungeonServerUrl,
                dungeonInstanceId,
                new JoinDungeonInstanceRequest
                {
                    userId = helperUserId,
                    saveId = helperContext.SaveId,
                    displayName = string.IsNullOrWhiteSpace(helperContext.PlayerName) ? aidRequest.HelperPlayerName : helperContext.PlayerName,
                    joinToken = helperJoinToken,
                },
                out error))
        {
            error = $"登记援助方副本参与者失败：{error}";
            return false;
        }

        DateTime now = DateTime.UtcNow;
        aidRequest.Status = AidRequestAccepted;
        aidRequest.UpdatedAtUtc = now;

        AidSessionEntity entity = new()
        {
            SessionId = sessionId,
            RequestId = aidRequest.RequestId,
            HostUserId = aidRequest.HostUserId,
            HostSaveId = aidRequest.HostSaveId,
            HostPlayerName = aidRequest.HostPlayerName,
            HostLevelIndex = aidRequest.HostLevelIndex,
            HostPlayerX = aidRequest.HostPlayerX,
            HostPlayerY = aidRequest.HostPlayerY,
            HostPlayerZ = aidRequest.HostPlayerZ,
            HostPlayerYaw = aidRequest.HostPlayerYaw,
            HelperUserId = helperUserId,
            HelperSaveId = helperContext.SaveId,
            HelperPlayerName = string.IsNullOrWhiteSpace(helperContext.PlayerName) ? aidRequest.HelperPlayerName : helperContext.PlayerName,
            HelperLevelIndex = Math.Max(1, helperContext.LevelIndex),
            DungeonServerUrl = dungeonServerUrl,
            DungeonInstanceId = dungeonInstanceId,
            DungeonRealtimeUdpPort = dungeonInstance.realtimeUdpPort,
            DungeonRealtimeKcpPort = dungeonInstance.realtimeKcpPort,
            DungeonJoinToken = helperJoinToken,
            DungeonParticipantsJson = SerializeDungeonParticipants(new List<DungeonParticipantInfo>
            {
                new DungeonParticipantInfo
                {
                    userId = aidRequest.HostUserId,
                    saveId = aidRequest.HostSaveId,
                    displayName = aidRequest.HostPlayerName,
                    levelIndex = aidRequest.HostLevelIndex,
                    joinToken = hostJoinToken,
                },
                new DungeonParticipantInfo
                {
                    userId = helperUserId,
                    saveId = helperContext.SaveId,
                    displayName = string.IsNullOrWhiteSpace(helperContext.PlayerName) ? aidRequest.HelperPlayerName : helperContext.PlayerName,
                    levelIndex = Math.Max(1, helperContext.LevelIndex),
                    joinToken = helperJoinToken,
                },
            }),
            Status = AidSessionActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.AidSessions.Add(entity);
        db.SaveChanges();
        aidSession = ToAidSessionDto(entity, helperUserId);
        error = string.Empty;
        return true;
    }

    public bool TryGetActiveAidSession(string userId, out AidSessionDto? aidSession, out string error)
    {
        aidSession = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();
        string normalizedUserId = NormalizeRequiredValue(userId);
        AidSessionEntity? entity = db.AidSessions
            .Where(session => session.Status == AidSessionActive
                              && (session.HostUserId == normalizedUserId || session.HelperUserId == normalizedUserId))
            .OrderByDescending(session => session.CreatedAtUtc)
            .FirstOrDefault();
        if (entity == null)
        {
            error = "当前没有活跃援助会话";
            return false;
        }

        aidSession = ToAidSessionDto(entity, normalizedUserId);
        error = string.Empty;
        return true;
    }

    public bool TryCloseAidSession(CloseAidSessionRequest request, out AidSessionDto? aidSession, out string error)
    {
        aidSession = null;
        using SocialDbContext db = _dbContextFactory.CreateDbContext();

        string sessionId = NormalizeRequiredValue(request.sessionId);
        string requesterUserId = NormalizeRequiredValue(request.requesterUserId);
        AidSessionEntity? entity = db.AidSessions.FirstOrDefault(session => session.SessionId == sessionId);
        if (entity == null)
        {
            error = "未找到该援助会话";
            return false;
        }

        if (entity.Status != AidSessionActive)
        {
            error = "该援助会话已结束";
            return false;
        }

        if (entity.HostUserId != requesterUserId && entity.HelperUserId != requesterUserId)
        {
            error = "当前账号不能结束该援助会话";
            return false;
        }

        entity.Status = AidSessionClosed;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        db.SaveChanges();

        if (!_onlineDungeonClient.TryCloseInstance(
                entity.DungeonServerUrl,
                entity.DungeonInstanceId,
                "aid_session_closed",
                out string closeDungeonError))
        {
            Console.WriteLine($"[SocialAppService] 关闭联机副本失败：{closeDungeonError}");
        }

        aidSession = ToAidSessionDto(entity, requesterUserId);
        error = string.Empty;
        return true;
    }

    private static bool TryNormalizeCredentials(AuthRequest request, out string username, out string password, out string error)
    {
        username = NormalizeRequiredValue(request.username);
        password = NormalizeRequiredValue(request.password);

        if (string.IsNullOrWhiteSpace(username))
        {
            error = "账号名不能为空";
            return false;
        }

        if (username.Length < 2 || username.Length > 20)
        {
            error = "账号名长度需要在 2 到 20 个字符之间";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            error = "密码不能为空";
            return false;
        }

        if (password.Length < 4 || password.Length > 32)
        {
            error = "密码长度需要在 4 到 32 个字符之间";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string HashPassword(string password)
    {
        const int iterations = 100_000;
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return $"pbkdf2-sha256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        string[] parts = string.IsNullOrWhiteSpace(storedHash)
            ? Array.Empty<string>()
            : storedHash.Split('$');

        if (parts.Length != 4 || !string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal))
            return false;

        if (!int.TryParse(parts[1], out int iterations) || iterations < 10_000)
            return false;

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static HashSet<string> CollectFriendIds(SocialDbContext db, string userId)
    {
        List<FriendLinkEntity> links = db.FriendLinks
            .Where(link => link.UserAId == userId || link.UserBId == userId)
            .ToList();

        HashSet<string> result = new(StringComparer.Ordinal);
        for (int i = 0; i < links.Count; i++)
        {
            FriendLinkEntity link = links[i];
            if (link.UserAId == userId)
                result.Add(link.UserBId);
            else if (link.UserBId == userId)
                result.Add(link.UserAId);
        }

        return result;
    }

    private static Dictionary<string, string> CollectFriendRemarks(SocialDbContext db, string userId)
    {
        List<FriendLinkEntity> links = db.FriendLinks
            .Where(link => link.UserAId == userId || link.UserBId == userId)
            .ToList();

        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < links.Count; i++)
        {
            FriendLinkEntity link = links[i];
            if (link.UserAId == userId)
                result[link.UserBId] = NormalizeOptionalValue(link.UserARemark);
            else if (link.UserBId == userId)
                result[link.UserAId] = NormalizeOptionalValue(link.UserBRemark);
        }

        return result;
    }

    private static bool AreFriends(SocialDbContext db, string userAId, string userBId)
    {
        string minId = MinUserId(userAId, userBId);
        string maxId = MaxUserId(userAId, userBId);
        return db.FriendLinks.Any(link => link.UserAId == minId && link.UserBId == maxId);
    }

    private static bool HasActiveAidBinding(SocialDbContext db, string userId, string exceptRequestId = "")
    {
        DateTime threshold = DateTime.UtcNow - PendingAidRequestWindow;
        string normalizedExceptRequestId = NormalizeOptionalValue(exceptRequestId);
        return db.AidRequests.Any(entity =>
                   (entity.HostUserId == userId || entity.HelperUserId == userId) &&
                   entity.Status == AidRequestPending &&
                   entity.RequestId != normalizedExceptRequestId &&
                   entity.UpdatedAtUtc >= threshold)
               || db.AidSessions.Any(entity =>
                   (entity.HostUserId == userId || entity.HelperUserId == userId) &&
                   entity.Status == AidSessionActive);
    }

    private static void CloseActiveAidSessionsForUser(SocialDbContext db, string userId)
    {
        List<AidSessionEntity> sessions = db.AidSessions
            .Where(entity =>
                (entity.HostUserId == userId || entity.HelperUserId == userId) &&
                entity.Status == AidSessionActive)
            .ToList();

        if (sessions.Count == 0)
            return;

        DateTime now = DateTime.UtcNow;
        for (int i = 0; i < sessions.Count; i++)
        {
            sessions[i].Status = AidSessionClosed;
            sessions[i].UpdatedAtUtc = now;
        }
    }

    private static void CancelExpiredOrDuplicatePendingRequests(SocialDbContext db, string hostUserId, string helperUserId)
    {
        DateTime threshold = DateTime.UtcNow - PendingAidRequestWindow;
        List<AidRequestEntity> staleRequests = db.AidRequests
            .Where(entity =>
                entity.Status == AidRequestPending &&
                ((entity.HostUserId == hostUserId) ||
                 (entity.HelperUserId == hostUserId) ||
                 entity.UpdatedAtUtc < threshold))
            .ToList();

        if (staleRequests.Count == 0)
            return;

        DateTime now = DateTime.UtcNow;
        for (int i = 0; i < staleRequests.Count; i++)
        {
            staleRequests[i].Status = AidRequestCancelled;
            staleRequests[i].UpdatedAtUtc = now;
        }

        db.SaveChanges();
    }

    private static SocialUserDto ToUserDto(SocialUserEntity entity)
    {
        return new SocialUserDto
        {
            userId = entity.UserId,
            username = entity.Username,
        };
    }

    private static SocialFriendDto ToFriendDto(SocialUserEntity entity, string remark = "")
    {
        return new SocialFriendDto
        {
            userId = entity.UserId,
            username = entity.Username,
            remark = NormalizeOptionalValue(remark),
        };
    }

    private static FriendRequestDto ToFriendRequestDto(SocialDbContext db, FriendRequestEntity entity)
    {
        SocialUserEntity? requester = db.Users.FirstOrDefault(user => user.UserId == entity.RequesterUserId);
        SaveSlotEntity? latestSlot = db.SaveSlots
            .Where(slot => slot.UserId == entity.RequesterUserId)
            .OrderByDescending(slot => slot.UpdatedAtUtc)
            .FirstOrDefault();

        string playerName = latestSlot != null && !string.IsNullOrWhiteSpace(latestSlot.PlayerName)
            ? latestSlot.PlayerName
            : requester?.Username ?? string.Empty;
        string portraitId = latestSlot != null && !string.IsNullOrWhiteSpace(latestSlot.PortraitId)
            ? latestSlot.PortraitId
            : "UI图片/天依";

        return new FriendRequestDto
        {
            requestId = entity.RequestId,
            requesterUserId = entity.RequesterUserId,
            requesterUsername = requester?.Username ?? string.Empty,
            requesterPlayerName = string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName,
            requesterPortraitId = portraitId,
            targetUserId = entity.TargetUserId,
            status = entity.Status,
            message = entity.Message,
            requesterRemark = entity.RequesterRemark,
            targetRemark = entity.TargetRemark,
            createdAtUtc = entity.CreatedAtUtc.ToString("O"),
            updatedAtUtc = entity.UpdatedAtUtc.ToString("O"),
        };
    }

    private static SocialMessageDto ToMessageDto(SocialMessageEntity entity)
    {
        return new SocialMessageDto
        {
            messageId = entity.MessageId,
            fromUserId = entity.FromUserId,
            toUserId = entity.ToUserId,
            content = entity.Content,
            sentAtUtc = entity.SentAtUtc.ToString("O"),
        };
    }

    private static SaveSlotSummaryDto ToSaveSummaryDto(SaveSlotEntity entity)
    {
        return new SaveSlotSummaryDto
        {
            saveId = entity.SaveId,
            playerName = entity.PlayerName,
            portraitId = entity.PortraitId,
            levelIndex = entity.LevelIndex,
            version = entity.Version,
            updatedAtUtc = entity.UpdatedAtUtc.ToString("O"),
        };
    }

    private static SaveSlotContentDto ToSaveContentDto(SaveSlotEntity entity)
    {
        return new SaveSlotContentDto
        {
            saveId = entity.SaveId,
            playerName = entity.PlayerName,
            portraitId = entity.PortraitId,
            levelIndex = entity.LevelIndex,
            version = entity.Version,
            updatedAtUtc = entity.UpdatedAtUtc.ToString("O"),
            saveJson = entity.SaveJson,
        };
    }

    private static ActiveSaveContextDto ToActiveSaveContextDto(ActiveSaveContextEntity entity)
    {
        return new ActiveSaveContextDto
        {
            userId = entity.UserId,
            saveId = entity.SaveId,
            playerName = entity.PlayerName,
            portraitId = entity.PortraitId,
            levelIndex = entity.LevelIndex,
            updatedAtUtc = entity.UpdatedAtUtc.ToString("O"),
        };
    }

    private static AidRequestDto ToAidRequestDto(AidRequestEntity entity)
    {
        return new AidRequestDto
        {
            requestId = entity.RequestId,
            hostUserId = entity.HostUserId,
            hostSaveId = entity.HostSaveId,
            hostPlayerName = entity.HostPlayerName,
            hostLevelIndex = entity.HostLevelIndex,
            hostPlayerX = entity.HostPlayerX,
            hostPlayerY = entity.HostPlayerY,
            hostPlayerZ = entity.HostPlayerZ,
            hostPlayerYaw = entity.HostPlayerYaw,
            helperUserId = entity.HelperUserId,
            helperPlayerName = entity.HelperPlayerName,
            status = entity.Status,
            message = entity.Message,
            createdAtUtc = entity.CreatedAtUtc.ToString("O"),
            updatedAtUtc = entity.UpdatedAtUtc.ToString("O"),
        };
    }

    private static AidSessionDto ToAidSessionDto(AidSessionEntity entity, string requesterUserId)
    {
        List<DungeonParticipantInfo> participants = DeserializeDungeonParticipants(entity.DungeonParticipantsJson);
        DungeonParticipantInfo? requesterParticipant = participants.FirstOrDefault(participant =>
            string.Equals(participant.userId, requesterUserId, StringComparison.Ordinal));

        return new AidSessionDto
        {
            sessionId = entity.SessionId,
            requestId = entity.RequestId,
            hostUserId = entity.HostUserId,
            hostSaveId = entity.HostSaveId,
            hostPlayerName = entity.HostPlayerName,
            hostLevelIndex = entity.HostLevelIndex,
            hostPlayerX = entity.HostPlayerX,
            hostPlayerY = entity.HostPlayerY,
            hostPlayerZ = entity.HostPlayerZ,
            hostPlayerYaw = entity.HostPlayerYaw,
            helperUserId = entity.HelperUserId,
            helperSaveId = entity.HelperSaveId,
            helperPlayerName = entity.HelperPlayerName,
            helperLevelIndex = entity.HelperLevelIndex,
            dungeonServerUrl = entity.DungeonServerUrl,
            dungeonInstanceId = entity.DungeonInstanceId,
            dungeonRealtimeUdpPort = entity.DungeonRealtimeUdpPort,
            dungeonRealtimeKcpPort = entity.DungeonRealtimeKcpPort,
            dungeonJoinToken = requesterParticipant?.joinToken ?? string.Empty,
            participants = participants.Select(ToDungeonSessionParticipantDto).ToList(),
            status = entity.Status,
            createdAtUtc = entity.CreatedAtUtc.ToString("O"),
            updatedAtUtc = entity.UpdatedAtUtc.ToString("O"),
        };
    }

    private static string NormalizeUsername(string username)
    {
        return NormalizeRequiredValue(username).ToUpperInvariant();
    }

    private static string NormalizeRequiredValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeOptionalValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeFriendRequestMessage(string message)
    {
        string normalized = NormalizeOptionalValue(message);
        return string.IsNullOrWhiteSpace(normalized) ? "我是来加你为好友的" : normalized;
    }

    private static string NormalizePlayerName(string playerName)
    {
        return string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName.Trim();
    }

    private static string NormalizePortraitId(string portraitId)
    {
        return string.IsNullOrWhiteSpace(portraitId) ? "UI图片/天依" : portraitId.Trim();
    }

    private static int StablePositiveSeed(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        int seed = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
        return seed == 0 ? 1 : seed;
    }

    private static string SerializeDungeonParticipants(List<DungeonParticipantInfo> participants)
    {
        return JsonSerializer.Serialize(participants);
    }

    private static List<DungeonParticipantInfo> DeserializeDungeonParticipants(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<DungeonParticipantInfo>();

        return JsonSerializer.Deserialize<List<DungeonParticipantInfo>>(json) ?? new List<DungeonParticipantInfo>();
    }

    private static DungeonSessionParticipantDto ToDungeonSessionParticipantDto(DungeonParticipantInfo participant)
    {
        return new DungeonSessionParticipantDto
        {
            userId = participant.userId,
            saveId = participant.saveId,
            displayName = participant.displayName,
            levelIndex = participant.levelIndex,
        };
    }

    private static string MinUserId(string left, string right)
    {
        return string.CompareOrdinal(left, right) <= 0 ? left : right;
    }

    private static string MaxUserId(string left, string right)
    {
        return string.CompareOrdinal(left, right) >= 0 ? left : right;
    }
}
