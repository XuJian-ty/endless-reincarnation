using System.Security.Cryptography;
using System.Text;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal sealed class SocialDbContext : DbContext
{
    public SocialDbContext(DbContextOptions<SocialDbContext> options) : base(options)
    {
    }

    public DbSet<SocialUserEntity> Users => Set<SocialUserEntity>();
    public DbSet<FriendLinkEntity> FriendLinks => Set<FriendLinkEntity>();
    public DbSet<FriendRequestEntity> FriendRequests => Set<FriendRequestEntity>();
    public DbSet<SocialMessageEntity> Messages => Set<SocialMessageEntity>();
    public DbSet<SaveSlotEntity> SaveSlots => Set<SaveSlotEntity>();
    public DbSet<ActiveSaveContextEntity> ActiveSaveContexts => Set<ActiveSaveContextEntity>();
    public DbSet<AidRequestEntity> AidRequests => Set<AidRequestEntity>();
    public DbSet<AidSessionEntity> AidSessions => Set<AidSessionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SocialUserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.Username).IsRequired();
            entity.Property(x => x.NormalizedUsername).IsRequired();
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.NormalizedUsername).IsUnique();
        });

        modelBuilder.Entity<FriendLinkEntity>(entity =>
        {
            entity.ToTable("friend_links");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserAId).IsRequired();
            entity.Property(x => x.UserBId).IsRequired();
            entity.Property(x => x.UserARemark).IsRequired();
            entity.Property(x => x.UserBRemark).IsRequired();
            entity.HasIndex(x => new { x.UserAId, x.UserBId }).IsUnique();
        });

        modelBuilder.Entity<FriendRequestEntity>(entity =>
        {
            entity.ToTable("friend_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestId).IsRequired();
            entity.Property(x => x.RequesterUserId).IsRequired();
            entity.Property(x => x.TargetUserId).IsRequired();
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.Message).IsRequired();
            entity.Property(x => x.RequesterRemark).IsRequired();
            entity.Property(x => x.TargetRemark).IsRequired();
            entity.HasIndex(x => x.RequestId).IsUnique();
            entity.HasIndex(x => new { x.TargetUserId, x.Status, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.RequesterUserId, x.TargetUserId, x.Status });
        });

        modelBuilder.Entity<SocialMessageEntity>(entity =>
        {
            entity.ToTable("messages");
            entity.HasKey(x => x.MessageId);
            entity.Property(x => x.FromUserId).IsRequired();
            entity.Property(x => x.ToUserId).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.HasIndex(x => new { x.FromUserId, x.ToUserId, x.MessageId });
        });

        modelBuilder.Entity<SaveSlotEntity>(entity =>
        {
            entity.ToTable("save_slots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SaveId).IsRequired();
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.PlayerName).IsRequired();
            entity.Property(x => x.SaveJson).IsRequired();
            entity.HasIndex(x => x.SaveId).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.UpdatedAtUtc });
        });

        modelBuilder.Entity<ActiveSaveContextEntity>(entity =>
        {
            entity.ToTable("active_save_contexts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.SaveId).IsRequired();
            entity.HasIndex(x => x.UserId).IsUnique();
        });

        modelBuilder.Entity<AidRequestEntity>(entity =>
        {
            entity.ToTable("aid_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestId).IsRequired();
            entity.Property(x => x.HostUserId).IsRequired();
            entity.Property(x => x.HostSaveId).IsRequired();
            entity.Property(x => x.HelperUserId).IsRequired();
            entity.Property(x => x.Status).IsRequired();
            entity.HasIndex(x => x.RequestId).IsUnique();
            entity.HasIndex(x => new { x.HelperUserId, x.Status, x.CreatedAtUtc });
        });

        modelBuilder.Entity<AidSessionEntity>(entity =>
        {
            entity.ToTable("aid_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SessionId).IsRequired();
            entity.Property(x => x.HostUserId).IsRequired();
            entity.Property(x => x.HostSaveId).IsRequired();
            entity.Property(x => x.HelperUserId).IsRequired();
            entity.Property(x => x.HelperSaveId).IsRequired();
            entity.Property(x => x.Status).IsRequired();
            entity.HasIndex(x => x.SessionId).IsUnique();
            entity.HasIndex(x => new { x.HostUserId, x.Status });
            entity.HasIndex(x => new { x.HelperUserId, x.Status });
        });
    }
}

internal sealed class SocialUserEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

internal sealed class FriendLinkEntity
{
    public int Id { get; set; }
    public string UserAId { get; set; } = string.Empty;
    public string UserBId { get; set; } = string.Empty;
    public string UserARemark { get; set; } = string.Empty;
    public string UserBRemark { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

internal sealed class FriendRequestEntity
{
    public int Id { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string RequesterUserId { get; set; } = string.Empty;
    public string TargetUserId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RequesterRemark { get; set; } = string.Empty;
    public string TargetRemark { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class SocialMessageEntity
{
    public long MessageId { get; set; }
    public string FromUserId { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
}

internal sealed class SaveSlotEntity
{
    public int Id { get; set; }
    public string SaveId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string PortraitId { get; set; } = string.Empty;
    public int LevelIndex { get; set; }
    public int Version { get; set; }
    public string SaveJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class ActiveSaveContextEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string SaveId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string PortraitId { get; set; } = string.Empty;
    public int LevelIndex { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class AidRequestEntity
{
    public int Id { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string HostUserId { get; set; } = string.Empty;
    public string HostSaveId { get; set; } = string.Empty;
    public string HostPlayerName { get; set; } = string.Empty;
    public int HostLevelIndex { get; set; }
    public float HostPlayerX { get; set; }
    public float HostPlayerY { get; set; }
    public float HostPlayerZ { get; set; }
    public float HostPlayerYaw { get; set; }
    public string HelperUserId { get; set; } = string.Empty;
    public string HelperPlayerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class AidSessionEntity
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string HostUserId { get; set; } = string.Empty;
    public string HostSaveId { get; set; } = string.Empty;
    public string HostPlayerName { get; set; } = string.Empty;
    public int HostLevelIndex { get; set; }
    public float HostPlayerX { get; set; }
    public float HostPlayerY { get; set; }
    public float HostPlayerZ { get; set; }
    public float HostPlayerYaw { get; set; }
    public string HelperUserId { get; set; } = string.Empty;
    public string HelperSaveId { get; set; } = string.Empty;
    public string HelperPlayerName { get; set; } = string.Empty;
    public int HelperLevelIndex { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal static class SocialDbInitializer
{
    public static void Initialize(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        IDbContextFactory<SocialDbContext> factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SocialDbContext>>();
        using SocialDbContext db = factory.CreateDbContext();
        EnsureSchema(db);
    }

    private static void EnsureSchema(SocialDbContext db)
    {
        db.Database.OpenConnection();
        try
        {
            db.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS users (
                    Id INTEGER NOT NULL CONSTRAINT PK_users PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT NOT NULL,
                    Username TEXT NOT NULL,
                    NormalizedUsername TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_users_UserId ON users (UserId);");
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_users_NormalizedUsername ON users (NormalizedUsername);");

            db.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS friend_links (
                    Id INTEGER NOT NULL CONSTRAINT PK_friend_links PRIMARY KEY AUTOINCREMENT,
                    UserAId TEXT NOT NULL,
                    UserBId TEXT NOT NULL,
                    UserARemark TEXT NOT NULL DEFAULT '',
                    UserBRemark TEXT NOT NULL DEFAULT '',
                    CreatedAtUtc TEXT NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_friend_links_UserAId_UserBId ON friend_links (UserAId, UserBId);");

            db.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS friend_requests (
                    Id INTEGER NOT NULL CONSTRAINT PK_friend_requests PRIMARY KEY AUTOINCREMENT,
                    RequestId TEXT NOT NULL,
                    RequesterUserId TEXT NOT NULL,
                    TargetUserId TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    RequesterRemark TEXT NOT NULL DEFAULT '',
                    TargetRemark TEXT NOT NULL DEFAULT '',
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_friend_requests_RequestId ON friend_requests (RequestId);");
            db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_friend_requests_TargetUserId_Status_CreatedAtUtc ON friend_requests (TargetUserId, Status, CreatedAtUtc);");
            db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_friend_requests_RequesterUserId_TargetUserId_Status ON friend_requests (RequesterUserId, TargetUserId, Status);");

            db.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS messages (
                    MessageId INTEGER NOT NULL CONSTRAINT PK_messages PRIMARY KEY AUTOINCREMENT,
                    FromUserId TEXT NOT NULL,
                    ToUserId TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    SentAtUtc TEXT NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_messages_FromUserId_ToUserId_MessageId ON messages (FromUserId, ToUserId, MessageId);");

            db.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS save_slots (
                    Id INTEGER NOT NULL CONSTRAINT PK_save_slots PRIMARY KEY AUTOINCREMENT,
                    SaveId TEXT NOT NULL,
                    UserId TEXT NOT NULL,
                    PlayerName TEXT NOT NULL,
                    PortraitId TEXT NOT NULL DEFAULT 'UI图片/天依',
                    LevelIndex INTEGER NOT NULL,
                    Version INTEGER NOT NULL,
                    SaveJson TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_save_slots_SaveId ON save_slots (SaveId);");
            db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_save_slots_UserId_UpdatedAtUtc ON save_slots (UserId, UpdatedAtUtc);");

            db.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS active_save_contexts (
                    Id INTEGER NOT NULL CONSTRAINT PK_active_save_contexts PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT NOT NULL,
                    SaveId TEXT NOT NULL,
                    PlayerName TEXT NOT NULL,
                    PortraitId TEXT NOT NULL DEFAULT 'UI图片/天依',
                    LevelIndex INTEGER NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_active_save_contexts_UserId ON active_save_contexts (UserId);");

            db.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS aid_requests (
                    Id INTEGER NOT NULL CONSTRAINT PK_aid_requests PRIMARY KEY AUTOINCREMENT,
                    RequestId TEXT NOT NULL,
                    HostUserId TEXT NOT NULL,
                    HostSaveId TEXT NOT NULL,
                    HostPlayerName TEXT NOT NULL,
                    HostLevelIndex INTEGER NOT NULL,
                    HostPlayerX REAL NOT NULL DEFAULT 0,
                    HostPlayerY REAL NOT NULL DEFAULT 0,
                    HostPlayerZ REAL NOT NULL DEFAULT 0,
                    HostPlayerYaw REAL NOT NULL DEFAULT 0,
                    HelperUserId TEXT NOT NULL,
                    HelperPlayerName TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_aid_requests_RequestId ON aid_requests (RequestId);");
            db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_aid_requests_HelperUserId_Status_CreatedAtUtc ON aid_requests (HelperUserId, Status, CreatedAtUtc);");

            db.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS aid_sessions (
                    Id INTEGER NOT NULL CONSTRAINT PK_aid_sessions PRIMARY KEY AUTOINCREMENT,
                    SessionId TEXT NOT NULL,
                    RequestId TEXT NOT NULL,
                    HostUserId TEXT NOT NULL,
                    HostSaveId TEXT NOT NULL,
                    HostPlayerName TEXT NOT NULL,
                    HostLevelIndex INTEGER NOT NULL,
                    HostPlayerX REAL NOT NULL DEFAULT 0,
                    HostPlayerY REAL NOT NULL DEFAULT 0,
                    HostPlayerZ REAL NOT NULL DEFAULT 0,
                    HostPlayerYaw REAL NOT NULL DEFAULT 0,
                    HelperUserId TEXT NOT NULL,
                    HelperSaveId TEXT NOT NULL,
                    HelperPlayerName TEXT NOT NULL,
                    HelperLevelIndex INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_aid_sessions_SessionId ON aid_sessions (SessionId);");
            db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_aid_sessions_HostUserId_Status ON aid_sessions (HostUserId, Status);");
            db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_aid_sessions_HelperUserId_Status ON aid_sessions (HelperUserId, Status);");

            EnsureColumnExists(db.Database.GetDbConnection(), "friend_links", "UserARemark", "ALTER TABLE friend_links ADD COLUMN UserARemark TEXT NOT NULL DEFAULT '';");
            EnsureColumnExists(db.Database.GetDbConnection(), "friend_links", "UserBRemark", "ALTER TABLE friend_links ADD COLUMN UserBRemark TEXT NOT NULL DEFAULT '';");
            EnsureColumnExists(db.Database.GetDbConnection(), "friend_requests", "RequesterRemark", "ALTER TABLE friend_requests ADD COLUMN RequesterRemark TEXT NOT NULL DEFAULT '';");
            EnsureColumnExists(db.Database.GetDbConnection(), "friend_requests", "TargetRemark", "ALTER TABLE friend_requests ADD COLUMN TargetRemark TEXT NOT NULL DEFAULT '';");
            EnsureColumnExists(db.Database.GetDbConnection(), "save_slots", "PortraitId", "ALTER TABLE save_slots ADD COLUMN PortraitId TEXT NOT NULL DEFAULT 'UI图片/天依';");
            EnsureColumnExists(db.Database.GetDbConnection(), "active_save_contexts", "PortraitId", "ALTER TABLE active_save_contexts ADD COLUMN PortraitId TEXT NOT NULL DEFAULT 'UI图片/天依';");
            EnsureColumnExists(db.Database.GetDbConnection(), "aid_requests", "HostPlayerX", "ALTER TABLE aid_requests ADD COLUMN HostPlayerX REAL NOT NULL DEFAULT 0;");
            EnsureColumnExists(db.Database.GetDbConnection(), "aid_requests", "HostPlayerY", "ALTER TABLE aid_requests ADD COLUMN HostPlayerY REAL NOT NULL DEFAULT 0;");
            EnsureColumnExists(db.Database.GetDbConnection(), "aid_requests", "HostPlayerZ", "ALTER TABLE aid_requests ADD COLUMN HostPlayerZ REAL NOT NULL DEFAULT 0;");
            EnsureColumnExists(db.Database.GetDbConnection(), "aid_requests", "HostPlayerYaw", "ALTER TABLE aid_requests ADD COLUMN HostPlayerYaw REAL NOT NULL DEFAULT 0;");
            EnsureColumnExists(db.Database.GetDbConnection(), "aid_sessions", "HostPlayerX", "ALTER TABLE aid_sessions ADD COLUMN HostPlayerX REAL NOT NULL DEFAULT 0;");
            EnsureColumnExists(db.Database.GetDbConnection(), "aid_sessions", "HostPlayerY", "ALTER TABLE aid_sessions ADD COLUMN HostPlayerY REAL NOT NULL DEFAULT 0;");
            EnsureColumnExists(db.Database.GetDbConnection(), "aid_sessions", "HostPlayerZ", "ALTER TABLE aid_sessions ADD COLUMN HostPlayerZ REAL NOT NULL DEFAULT 0;");
            EnsureColumnExists(db.Database.GetDbConnection(), "aid_sessions", "HostPlayerYaw", "ALTER TABLE aid_sessions ADD COLUMN HostPlayerYaw REAL NOT NULL DEFAULT 0;");
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    private static void EnsureColumnExists(DbConnection connection, string tableName, string columnName, string alterSql)
    {
        using DbCommand pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({tableName});";
        using DbDataReader reader = pragma.ExecuteReader();

        while (reader.Read())
        {
            string existingColumnName = reader["name"]?.ToString();
            if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                return;
        }

        reader.Close();
        using DbCommand alter = connection.CreateCommand();
        alter.CommandText = alterSql;
        alter.ExecuteNonQuery();
    }
}

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

    public SocialAppService(IDbContextFactory<SocialDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
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
            PasswordHash = ComputeSha256(password),
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
        if (entity == null || !string.Equals(entity.PasswordHash, ComputeSha256(password), StringComparison.Ordinal))
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
                remark = friendRemarks.TryGetValue(user.UserId, out string remark) ? remark : string.Empty,
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
            bool isOnline = activeContexts.TryGetValue(friend.UserId, out ActiveSaveContextEntity activeContext)
                            && DateTime.UtcNow - activeContext.UpdatedAtUtc <= ActivePresenceWindow;
            SaveSlotEntity latestSlot = null;
            latestSlots.TryGetValue(friend.UserId, out latestSlot);

            string playerName = isOnline
                ? activeContext.PlayerName
                : latestSlot?.PlayerName;
            string portraitId = isOnline
                ? activeContext.PortraitId
                : latestSlot?.PortraitId;
            int levelIndex = isOnline
                ? activeContext.LevelIndex
                : (latestSlot != null ? latestSlot.LevelIndex : 1);

            results.Add(new FriendPresenceDto
            {
                userId = friend.UserId,
                username = friend.Username,
                remark = friendRemarks.TryGetValue(friend.UserId, out string remark) ? remark : string.Empty,
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

    public bool TryRespondToAidRequest(RespondAidRequestRequest request, out AidSessionDto? aidSession, out string error)
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

        DateTime now = DateTime.UtcNow;
        aidRequest.Status = AidRequestAccepted;
        aidRequest.UpdatedAtUtc = now;

        AidSessionEntity entity = new()
        {
            SessionId = Guid.NewGuid().ToString("N"),
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
            Status = AidSessionActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.AidSessions.Add(entity);
        db.SaveChanges();
        aidSession = ToAidSessionDto(entity);
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

        aidSession = ToAidSessionDto(entity);
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

        aidSession = ToAidSessionDto(entity);
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

    private static string ComputeSha256(string rawValue)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToHexString(bytes);
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
            : requester?.Username;
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

    private static AidSessionDto ToAidSessionDto(AidSessionEntity entity)
    {
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

    private static string MinUserId(string left, string right)
    {
        return string.CompareOrdinal(left, right) <= 0 ? left : right;
    }

    private static string MaxUserId(string left, string right)
    {
        return string.CompareOrdinal(left, right) >= 0 ? left : right;
    }
}

internal class SaveSlotSummaryDto
{
    public string saveId { get; set; } = string.Empty;
    public string playerName { get; set; } = string.Empty;
    public string portraitId { get; set; } = string.Empty;
    public int levelIndex { get; set; }
    public int version { get; set; }
    public string updatedAtUtc { get; set; } = string.Empty;
}

internal sealed class SaveSlotContentDto : SaveSlotSummaryDto
{
    public string saveJson { get; set; } = string.Empty;
}

internal sealed class UpsertSaveRequest
{
    public string userId { get; set; } = string.Empty;
    public string playerName { get; set; } = string.Empty;
    public string portraitId { get; set; } = string.Empty;
    public int levelIndex { get; set; }
    public int version { get; set; }
    public string saveJson { get; set; } = string.Empty;
}

internal sealed class SetActiveSaveContextRequest
{
    public string userId { get; set; } = string.Empty;
    public string saveId { get; set; } = string.Empty;
}

internal sealed class ClearActiveSaveContextRequest
{
    public string userId { get; set; } = string.Empty;
}

internal sealed class ActiveSaveContextDto
{
    public string userId { get; set; } = string.Empty;
    public string saveId { get; set; } = string.Empty;
    public string playerName { get; set; } = string.Empty;
    public string portraitId { get; set; } = string.Empty;
    public int levelIndex { get; set; }
    public string updatedAtUtc { get; set; } = string.Empty;
}

internal sealed class CreateAidRequestRequest
{
    public string hostUserId { get; set; } = string.Empty;
    public string helperUserId { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
    public float hostPlayerX { get; set; }
    public float hostPlayerY { get; set; }
    public float hostPlayerZ { get; set; }
    public float hostPlayerYaw { get; set; }
}

internal sealed class RespondAidRequestRequest
{
    public string helperUserId { get; set; } = string.Empty;
    public string requestId { get; set; } = string.Empty;
    public string decision { get; set; } = string.Empty;
}

internal sealed class CloseAidSessionRequest
{
    public string requesterUserId { get; set; } = string.Empty;
    public string sessionId { get; set; } = string.Empty;
}

internal sealed class AidRequestDto
{
    public string requestId { get; set; } = string.Empty;
    public string hostUserId { get; set; } = string.Empty;
    public string hostSaveId { get; set; } = string.Empty;
    public string hostPlayerName { get; set; } = string.Empty;
    public int hostLevelIndex { get; set; }
    public float hostPlayerX { get; set; }
    public float hostPlayerY { get; set; }
    public float hostPlayerZ { get; set; }
    public float hostPlayerYaw { get; set; }
    public string helperUserId { get; set; } = string.Empty;
    public string helperPlayerName { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
    public string createdAtUtc { get; set; } = string.Empty;
    public string updatedAtUtc { get; set; } = string.Empty;
}

internal sealed class AidSessionDto
{
    public string sessionId { get; set; } = string.Empty;
    public string requestId { get; set; } = string.Empty;
    public string hostUserId { get; set; } = string.Empty;
    public string hostSaveId { get; set; } = string.Empty;
    public string hostPlayerName { get; set; } = string.Empty;
    public int hostLevelIndex { get; set; }
    public float hostPlayerX { get; set; }
    public float hostPlayerY { get; set; }
    public float hostPlayerZ { get; set; }
    public float hostPlayerYaw { get; set; }
    public string helperUserId { get; set; } = string.Empty;
    public string helperSaveId { get; set; } = string.Empty;
    public string helperPlayerName { get; set; } = string.Empty;
    public int helperLevelIndex { get; set; }
    public string status { get; set; } = string.Empty;
    public string createdAtUtc { get; set; } = string.Empty;
    public string updatedAtUtc { get; set; } = string.Empty;
}

internal sealed class FriendPresenceDto
{
    public string userId { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
    public string remark { get; set; } = string.Empty;
    public string playerName { get; set; } = string.Empty;
    public string portraitId { get; set; } = string.Empty;
    public int levelIndex { get; set; }
    public bool isOnline { get; set; }
    public bool isHelping { get; set; }
}

internal sealed class FriendRequestDto
{
    public string requestId { get; set; } = string.Empty;
    public string requesterUserId { get; set; } = string.Empty;
    public string requesterUsername { get; set; } = string.Empty;
    public string requesterPlayerName { get; set; } = string.Empty;
    public string requesterPortraitId { get; set; } = string.Empty;
    public string targetUserId { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
    public string requesterRemark { get; set; } = string.Empty;
    public string targetRemark { get; set; } = string.Empty;
    public string createdAtUtc { get; set; } = string.Empty;
    public string updatedAtUtc { get; set; } = string.Empty;
}
