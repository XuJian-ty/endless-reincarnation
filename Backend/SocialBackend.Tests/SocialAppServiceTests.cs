using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class SocialAppServiceTests
{
    [Fact]
    public void RegisterAndLogin_UsesSaltedPasswordHash()
    {
        SocialAppService service = CreateService();

        Assert.True(service.TryRegister(
            new AuthRequest { username = "alice", password = "pass1234" },
            out SocialUserDto? registered,
            out string registerError));
        Assert.NotNull(registered);
        Assert.Equal(string.Empty, registerError);

        using SocialDbContext db = CreateContext();
        SocialUserEntity user = db.Users.Single();
        Assert.StartsWith("pbkdf2-sha256$", user.PasswordHash);
        Assert.DoesNotContain("pass1234", user.PasswordHash, StringComparison.Ordinal);

        Assert.True(service.TryLogin(
            new AuthRequest { username = "alice", password = "pass1234" },
            out SocialUserDto? loggedIn,
            out string loginError));
        Assert.NotNull(loggedIn);
        Assert.Equal(registered!.userId, loggedIn!.userId);
        Assert.Equal(string.Empty, loginError);
    }

    [Fact]
    public void FriendRequest_CanBeAccepted()
    {
        SocialAppService service = CreateService();
        SocialUserDto alice = Register(service, "alice");
        SocialUserDto bob = Register(service, "bob");

        Assert.True(service.TryAddFriend(
            new AddFriendRequest
            {
                requesterUserId = alice.userId,
                targetUsername = "bob",
                message = "hello",
                remark = "Bob",
            },
            out FriendRequestDto? request,
            out string addError));
        Assert.NotNull(request);
        Assert.Equal(string.Empty, addError);

        Assert.True(service.TryRespondToFriendRequest(
            new RespondFriendRequestRequest
            {
                targetUserId = bob.userId,
                requestId = request!.requestId,
                decision = "accept",
                remark = "Alice",
            },
            out SocialFriendDto? friend,
            out string respondError));
        Assert.NotNull(friend);
        Assert.Equal(alice.userId, friend!.userId);
        Assert.Equal(string.Empty, respondError);

        List<SocialFriendDto> aliceFriends = service.GetFriends(alice.userId);
        Assert.Single(aliceFriends);
        Assert.Equal(bob.userId, aliceFriends[0].userId);
    }

    [Fact]
    public void SaveSlot_CanBeCreatedReadAndDeleted()
    {
        SocialAppService service = CreateService();
        SocialUserDto user = Register(service, "alice");

        Assert.True(service.UpsertSave(
            "save-1",
            new UpsertSaveRequest
            {
                userId = user.userId,
                playerName = "Alice",
                portraitId = "portrait/default",
                levelIndex = 2,
                version = 1,
                saveJson = "{\"level\":2}",
            },
            out SaveSlotContentDto? saved,
            out string upsertError));
        Assert.NotNull(saved);
        Assert.Equal(string.Empty, upsertError);

        Assert.True(service.TryGetSave(user.userId, "save-1", out SaveSlotContentDto? loaded, out string getError));
        Assert.NotNull(loaded);
        Assert.Equal("{\"level\":2}", loaded!.saveJson);
        Assert.Equal(string.Empty, getError);

        List<SaveSlotSummaryDto> summaries = service.GetSaveSummaries(user.userId, out string listError);
        Assert.Single(summaries);
        Assert.Equal(string.Empty, listError);

        Assert.True(service.TryDeleteSave(user.userId, "save-1", out string deleteError));
        Assert.Equal(string.Empty, deleteError);
        Assert.Empty(service.GetSaveSummaries(user.userId, out _));
    }

    [Fact]
    public void AidRequest_AcceptanceCreatesAndClosesDungeonSession()
    {
        var dungeonHandler = new FakeDungeonHandler();
        SocialAppService service = CreateService(dungeonHandler);
        SocialUserDto alice = Register(service, "alice");
        SocialUserDto bob = Register(service, "bob");
        MakeFriends(service, alice, bob);
        CreateActiveSave(service, alice.userId, "save-alice", "Alice", 3);
        CreateActiveSave(service, bob.userId, "save-bob", "Bob", 3);

        Assert.True(service.TryCreateAidRequest(
            new CreateAidRequestRequest
            {
                hostUserId = alice.userId,
                helperUserId = bob.userId,
                message = "help",
                hostPlayerX = 1,
                hostPlayerY = 2,
                hostPlayerZ = 3,
                hostPlayerYaw = 90,
            },
            out AidRequestDto? aidRequest,
            out string createAidError));
        Assert.NotNull(aidRequest);
        Assert.Equal(string.Empty, createAidError);

        Assert.True(service.TryRespondToAidRequest(
            new RespondAidRequestRequest
            {
                helperUserId = bob.userId,
                requestId = aidRequest!.requestId,
                decision = "accept",
            },
            "127.0.0.1",
            out AidSessionDto? aidSession,
            out string acceptError));
        Assert.NotNull(aidSession);
        Assert.Equal(string.Empty, acceptError);
        Assert.Equal("http://127.0.0.1:5086", aidSession!.dungeonServerUrl);
        Assert.Equal(5087, aidSession.dungeonRealtimeUdpPort);
        Assert.Equal(5088, aidSession.dungeonRealtimeKcpPort);
        Assert.Equal(2, aidSession.participants.Count);
        Assert.Contains(dungeonHandler.RequestPaths, path => path == "POST /api/dungeons");
        Assert.Equal(2, dungeonHandler.RequestPaths.Count(path => path.EndsWith("/participants/join", StringComparison.Ordinal)));

        Assert.True(service.TryCloseAidSession(
            new CloseAidSessionRequest
            {
                requesterUserId = bob.userId,
                sessionId = aidSession.sessionId,
            },
            out AidSessionDto? closedSession,
            out string closeError));
        Assert.NotNull(closedSession);
        Assert.Equal(string.Empty, closeError);
        Assert.Contains(dungeonHandler.RequestPaths, path => path.EndsWith("/close", StringComparison.Ordinal));
    }

    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();
    private static readonly DbContextOptions<SocialDbContext> DbOptions =
        new DbContextOptionsBuilder<SocialDbContext>()
            .UseInMemoryDatabase("social-backend-tests", DatabaseRoot)
            .Options;

    private static SocialAppService CreateService(HttpMessageHandler? httpMessageHandler = null)
    {
        using SocialDbContext db = CreateContext();
        db.Users.RemoveRange(db.Users);
        db.FriendLinks.RemoveRange(db.FriendLinks);
        db.FriendRequests.RemoveRange(db.FriendRequests);
        db.Messages.RemoveRange(db.Messages);
        db.SaveSlots.RemoveRange(db.SaveSlots);
        db.ActiveSaveContexts.RemoveRange(db.ActiveSaveContexts);
        db.AidRequests.RemoveRange(db.AidRequests);
        db.AidSessions.RemoveRange(db.AidSessions);
        db.SaveChanges();

        var factory = new TestSocialDbContextFactory(DbOptions);
        var client = new OnlineDungeonClient(httpMessageHandler == null
            ? new HttpClient()
            : new HttpClient(httpMessageHandler));
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OnlineDungeon:DefaultServerUrl"] = "http://127.0.0.1:5086",
            })
            .Build();

        return new SocialAppService(factory, client, configuration);
    }

    private static SocialDbContext CreateContext()
    {
        return new SocialDbContext(DbOptions);
    }

    private static SocialUserDto Register(SocialAppService service, string username)
    {
        Assert.True(service.TryRegister(
            new AuthRequest { username = username, password = "pass1234" },
            out SocialUserDto? user,
            out string error));
        Assert.Equal(string.Empty, error);
        return user!;
    }

    private static void MakeFriends(SocialAppService service, SocialUserDto requester, SocialUserDto target)
    {
        Assert.True(service.TryAddFriend(
            new AddFriendRequest
            {
                requesterUserId = requester.userId,
                targetUsername = target.username,
                message = "hello",
            },
            out FriendRequestDto? request,
            out string addError));
        Assert.Equal(string.Empty, addError);

        Assert.True(service.TryRespondToFriendRequest(
            new RespondFriendRequestRequest
            {
                targetUserId = target.userId,
                requestId = request!.requestId,
                decision = "accept",
            },
            out SocialFriendDto? friend,
            out string respondError));
        Assert.NotNull(friend);
        Assert.Equal(string.Empty, respondError);
    }

    private static void CreateActiveSave(SocialAppService service, string userId, string saveId, string playerName, int levelIndex)
    {
        Assert.True(service.UpsertSave(
            saveId,
            new UpsertSaveRequest
            {
                userId = userId,
                playerName = playerName,
                portraitId = "portrait/default",
                levelIndex = levelIndex,
                version = 1,
                saveJson = "{\"level\":3}",
            },
            out SaveSlotContentDto? save,
            out string saveError));
        Assert.NotNull(save);
        Assert.Equal(string.Empty, saveError);

        Assert.True(service.TrySetActiveSaveContext(
            new SetActiveSaveContextRequest
            {
                userId = userId,
                saveId = saveId,
            },
            out ActiveSaveContextDto? context,
            out string contextError));
        Assert.NotNull(context);
        Assert.Equal(string.Empty, contextError);
    }

    private sealed class TestSocialDbContextFactory : IDbContextFactory<SocialDbContext>
    {
        private readonly DbContextOptions<SocialDbContext> _options;

        public TestSocialDbContextFactory(DbContextOptions<SocialDbContext> options)
        {
            _options = options;
        }

        public SocialDbContext CreateDbContext()
        {
            return new SocialDbContext(_options);
        }
    }

    private sealed class FakeDungeonHandler : HttpMessageHandler
    {
        public List<string> RequestPaths { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            RequestPaths.Add($"{request.Method.Method} {path}");

            if (request.Method == HttpMethod.Post && path == "/api/dungeons")
            {
                CreateDungeonInstanceRequest? createRequest = request.Content == null
                    ? null
                    : await request.Content.ReadFromJsonAsync<CreateDungeonInstanceRequest>(cancellationToken);

                return JsonResponse(new DungeonApiResponse<DungeonInstanceInfo>
                {
                    success = true,
                    message = "created",
                    data = new DungeonInstanceInfo
                    {
                        instanceId = createRequest?.instanceId ?? "test-instance",
                        status = "active",
                        realtimeUdpPort = 5087,
                        realtimeKcpPort = 5088,
                    },
                });
            }

            if (request.Method == HttpMethod.Post &&
                (path.EndsWith("/participants/join", StringComparison.Ordinal) ||
                 path.EndsWith("/close", StringComparison.Ordinal)))
            {
                return JsonResponse(new DungeonApiResponse<DungeonInstanceInfo>
                {
                    success = true,
                    message = "ok",
                    data = new DungeonInstanceInfo
                    {
                        instanceId = "test-instance",
                        status = "active",
                        realtimeUdpPort = 5087,
                        realtimeKcpPort = 5088,
                    },
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = JsonContent.Create(new DungeonApiResponse<DungeonInstanceInfo>
                {
                    success = false,
                    message = $"unexpected request: {path}",
                }),
            };
        }

        private static HttpResponseMessage JsonResponse<T>(T value)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(value),
            };
        }
    }
}
