using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace Game.Online
{
    public sealed class OnlineDungeonAuthorityClient
    {
        public IEnumerator GetAuthorityState(
            string dungeonServerUrl,
            string instanceId,
            string userId,
            string joinToken,
            Action<OnlineDungeonAuthorityStateInfo> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(
                dungeonServerUrl,
                $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/authority-state?userId={Uri.EscapeDataString(userId)}&joinToken={Uri.EscapeDataString(joinToken)}");
            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET)
            {
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("Accept", "application/json");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                onError?.Invoke(string.IsNullOrWhiteSpace(request.downloadHandler?.text)
                    ? request.error
                    : ExtractErrorMessage(request.downloadHandler.text, request.error));
                yield break;
            }

            OnlineDungeonApiResponse<OnlineDungeonAuthorityStateInfo> response = null;
            try
            {
                response = JsonConvert.DeserializeObject<OnlineDungeonApiResponse<OnlineDungeonAuthorityStateInfo>>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                yield break;
            }

            if (response == null || !response.success)
            {
                onError?.Invoke(response?.message ?? "联机副本权威状态同步失败");
                yield break;
            }

            onSuccess?.Invoke(response.data);
        }

        private static string BuildUrl(string dungeonServerUrl, string pathAndQuery)
        {
            string serverUrl = string.IsNullOrWhiteSpace(dungeonServerUrl) ? string.Empty : dungeonServerUrl.Trim().TrimEnd('/');
            return serverUrl + pathAndQuery;
        }

        private static string ExtractErrorMessage(string responseText, string fallback)
        {
            try
            {
                OnlineDungeonApiResponse<object> response = JsonConvert.DeserializeObject<OnlineDungeonApiResponse<object>>(responseText);
                if (response != null && !string.IsNullOrWhiteSpace(response.message))
                    return response.message;
            }
            catch
            {
            }

            return string.IsNullOrWhiteSpace(fallback) ? responseText : fallback;
        }
    }

    [Serializable]
    public sealed class OnlineDungeonAuthorityStateInfo
    {
        public long version;
        public bool initialized;
        public List<OnlineDungeonEnemyAuthorityInfo> enemies;
        public List<OnlineDungeonChestAuthorityInfo> chests;
        public long rewardStateVersion;
    }

    [Serializable]
    public sealed class OnlineDungeonEnemyAuthorityInfo
    {
        public string runtimeId;
        public string enemyId;
        public int enemyType;
        public float x;
        public float y;
        public float z;
        public float yaw;
        public float currentHp;
        public float currentPoise;
        public bool countsAsLevelBoss;
        public bool isDead;
        public bool showCombatHealthBar;
        public float moveBlend;
        public float moveForward;
        public float moveStrafe;
        public int activeSkillSequence;
        public int activeSkillSlot;
        public string activeSkillId;
        public string activeSkillAnimationTrigger;
        public bool hasActiveSkillTarget;
        public float activeSkillTargetX;
        public float activeSkillTargetY;
        public float activeSkillTargetZ;
        public string updatedAtUtc;
        public string diedAtUtc;
    }

    [Serializable]
    public sealed class OnlineDungeonChestAuthorityInfo
    {
        public string chestId;
        public string prefabId;
        public float x;
        public float y;
        public float z;
        public float yaw;
        public bool opened;
        public string openedByUserId;
        public string openedAtUtc;
        public string updatedAtUtc;
    }
}
