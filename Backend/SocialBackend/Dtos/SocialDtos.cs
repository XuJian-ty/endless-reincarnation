internal sealed class ApiResponse<T>
{
    public bool success { get; set; }
    public string message { get; set; } = string.Empty;
    public T? data { get; set; }

    public static ApiResponse<T> Ok(T data, string message)
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

internal sealed class AuthRequest
{
    public string username { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
}

internal sealed class AddFriendRequest
{
    public string requesterUserId { get; set; } = string.Empty;
    public string targetUsername { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
    public string remark { get; set; } = string.Empty;
}

internal sealed class RespondFriendRequestRequest
{
    public string targetUserId { get; set; } = string.Empty;
    public string requestId { get; set; } = string.Empty;
    public string decision { get; set; } = string.Empty;
    public string remark { get; set; } = string.Empty;
}

internal sealed class RemoveFriendRequest
{
    public string requesterUserId { get; set; } = string.Empty;
    public string friendUserId { get; set; } = string.Empty;
}

internal sealed class SendMessageRequest
{
    public string fromUserId { get; set; } = string.Empty;
    public string toUserId { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
}

internal sealed class SocialUserDto
{
    public string userId { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
}

internal sealed class SocialUserSearchDto
{
    public string userId { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
    public bool isFriend { get; set; }
    public bool hasPendingFriendRequest { get; set; }
}

internal sealed class SocialFriendDto
{
    public string userId { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
    public string remark { get; set; } = string.Empty;
}

internal sealed class SocialMessageDto
{
    public long messageId { get; set; }
    public string fromUserId { get; set; } = string.Empty;
    public string toUserId { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
    public string sentAtUtc { get; set; } = string.Empty;
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

internal sealed class CreateDungeonInstanceRequest
{
    public string instanceId { get; set; } = string.Empty;
    public string templateOwnerUserId { get; set; } = string.Empty;
    public string templateSaveId { get; set; } = string.Empty;
    public int levelIndex { get; set; }
    public int difficulty { get; set; }
    public int seed { get; set; }
    public int maxPlayers { get; set; }
}

internal sealed class DungeonInstanceInfo
{
    public string instanceId { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    public int realtimeUdpPort { get; set; }
    public int realtimeKcpPort { get; set; }
}

internal sealed class JoinDungeonInstanceRequest
{
    public string userId { get; set; } = string.Empty;
    public string saveId { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public string joinToken { get; set; } = string.Empty;
}

internal sealed class CloseDungeonInstanceRequest
{
    public string reason { get; set; } = string.Empty;
}

internal sealed class DungeonApiResponse<T>
{
    public bool success { get; set; }
    public string message { get; set; } = string.Empty;
    public T? data { get; set; }
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
    public string dungeonServerUrl { get; set; } = string.Empty;
    public string dungeonInstanceId { get; set; } = string.Empty;
    public int dungeonRealtimeUdpPort { get; set; }
    public int dungeonRealtimeKcpPort { get; set; }
    public string dungeonJoinToken { get; set; } = string.Empty;
    public List<DungeonSessionParticipantDto> participants { get; set; } = new();
    public string status { get; set; } = string.Empty;
    public string createdAtUtc { get; set; } = string.Empty;
    public string updatedAtUtc { get; set; } = string.Empty;
}

internal sealed class DungeonParticipantInfo
{
    public string userId { get; set; } = string.Empty;
    public string saveId { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public int levelIndex { get; set; }
    public string joinToken { get; set; } = string.Empty;
}

internal sealed class DungeonSessionParticipantDto
{
    public string userId { get; set; } = string.Empty;
    public string saveId { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public int levelIndex { get; set; }
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
