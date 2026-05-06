using System;
using System.Collections.Generic;

namespace Game.Social
{
    [Serializable]
    public class SocialApiResponse<T>
    {
        public bool success;
        public string message;
        public T data;
    }

    [Serializable]
    public class SocialUserInfo
    {
        public string userId;
        public string username;
    }

    [Serializable]
    public class SocialUserSearchInfo
    {
        public string userId;
        public string username;
        public bool isFriend;
        public bool hasPendingFriendRequest;
    }

    [Serializable]
    public class SocialFriendInfo
    {
        public string userId;
        public string username;
        public string remark;
    }

    [Serializable]
    public class SocialFriendPresenceInfo
    {
        public string userId;
        public string username;
        public string remark;
        public string playerName;
        public string portraitId;
        public int levelIndex;
        public bool isOnline;
        public bool isHelping;
    }

    [Serializable]
    public class SocialMessageInfo
    {
        public long messageId;
        public string fromUserId;
        public string toUserId;
        public string content;
        public string sentAtUtc;
    }

    [Serializable]
    public class SocialActiveSaveContextInfo
    {
        public string userId;
        public string saveId;
        public string playerName;
        public int levelIndex;
        public string updatedAtUtc;
    }

    [Serializable]
    public class SocialAidRequestInfo
    {
        public string requestId;
        public string hostUserId;
        public string hostSaveId;
        public string hostPlayerName;
        public int hostLevelIndex;
        public string helperUserId;
        public string helperPlayerName;
        public string status;
        public string message;
        public string createdAtUtc;
        public string updatedAtUtc;
    }

    [Serializable]
    public class SocialAidSessionInfo
    {
        public string sessionId;
        public string requestId;
        public string hostUserId;
        public string hostSaveId;
        public string hostPlayerName;
        public int hostLevelIndex;
        public float hostPlayerX;
        public float hostPlayerY;
        public float hostPlayerZ;
        public float hostPlayerYaw;
        public string helperUserId;
        public string helperSaveId;
        public string helperPlayerName;
        public int helperLevelIndex;
        public string dungeonServerUrl;
        public string dungeonInstanceId;
        public int dungeonRealtimeUdpPort;
        public int dungeonRealtimeKcpPort;
        public string dungeonJoinToken;
        public List<SocialDungeonParticipantInfo> participants;
        public string status;
        public string createdAtUtc;
        public string updatedAtUtc;
    }

    [Serializable]
    public class SocialDungeonParticipantInfo
    {
        public string userId;
        public string saveId;
        public string displayName;
        public int levelIndex;
    }

    [Serializable]
    public class SocialAuthRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    public class SocialAddFriendRequest
    {
        public string requesterUserId;
        public string targetUsername;
        public string message;
        public string remark;
    }

    [Serializable]
    public class SocialRespondFriendRequest
    {
        public string targetUserId;
        public string requestId;
        public string decision;
        public string remark;
    }

    [Serializable]
    public class SocialRemoveFriendRequest
    {
        public string requesterUserId;
        public string friendUserId;
    }

    [Serializable]
    public class SocialSendMessageRequest
    {
        public string fromUserId;
        public string toUserId;
        public string content;
    }

    [Serializable]
    public class SocialSetActiveSaveContextRequest
    {
        public string userId;
        public string saveId;
    }

    [Serializable]
    public class SocialClearActiveSaveContextRequest
    {
        public string userId;
    }

    [Serializable]
    public class SocialCreateAidRequest
    {
        public string hostUserId;
        public string helperUserId;
        public string message;
        public float hostPlayerX;
        public float hostPlayerY;
        public float hostPlayerZ;
        public float hostPlayerYaw;
    }

    [Serializable]
    public class SocialRespondAidRequest
    {
        public string helperUserId;
        public string requestId;
        public string decision;
    }

    [Serializable]
    public class SocialCloseAidSessionRequest
    {
        public string requesterUserId;
        public string sessionId;
    }

    [Serializable]
    public class SocialFriendRequestInfo
    {
        public string requestId;
        public string requesterUserId;
        public string requesterUsername;
        public string requesterPlayerName;
        public string requesterPortraitId;
        public string targetUserId;
        public string status;
        public string message;
        public string requesterRemark;
        public string targetRemark;
        public string createdAtUtc;
        public string updatedAtUtc;
    }

    [Serializable]
    public class SocialConversationCache
    {
        public string friendUserId;
        public List<SocialMessageInfo> messages = new List<SocialMessageInfo>();
    }
}
