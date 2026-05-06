using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace Game.Online
{
    public sealed class OnlineDungeonDamageClient
    {
        public IEnumerator AddDamageEvent(
            string dungeonServerUrl,
            string instanceId,
            OnlineDungeonDamageEventRequest requestBody,
            Action<List<OnlineDungeonDamageEventInfo>> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(dungeonServerUrl, $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/damage-events");
            yield return SendRequest(UnityWebRequest.kHttpVerbPOST, url, requestBody, onSuccess, onError);
        }

        public IEnumerator GetDamageEvents(
            string dungeonServerUrl,
            string instanceId,
            string userId,
            string joinToken,
            long afterSequence,
            Action<List<OnlineDungeonDamageEventInfo>> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(
                dungeonServerUrl,
                $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/damage-events?userId={Uri.EscapeDataString(userId)}&joinToken={Uri.EscapeDataString(joinToken)}&afterSequence={afterSequence}");
            yield return SendRequest(UnityWebRequest.kHttpVerbGET, url, null, onSuccess, onError);
        }

        private static IEnumerator SendRequest(
            string method,
            string url,
            object requestBody,
            Action<List<OnlineDungeonDamageEventInfo>> onSuccess,
            Action<string> onError)
        {
            using UnityWebRequest request = BuildRequest(method, url, requestBody);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                onError?.Invoke(string.IsNullOrWhiteSpace(request.downloadHandler?.text)
                    ? request.error
                    : ExtractErrorMessage(request.downloadHandler.text, request.error));
                yield break;
            }

            OnlineDungeonApiResponse<OnlineDungeonDamageEventListInfo> response = null;
            try
            {
                response = JsonConvert.DeserializeObject<OnlineDungeonApiResponse<OnlineDungeonDamageEventListInfo>>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                yield break;
            }

            if (response == null || !response.success)
            {
                onError?.Invoke(response?.message ?? "联机副本伤害事件同步失败");
                yield break;
            }

            onSuccess?.Invoke(response.data?.events ?? new List<OnlineDungeonDamageEventInfo>());
        }

        private static UnityWebRequest BuildRequest(string method, string url, object requestBody)
        {
            UnityWebRequest request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
            };

            if (requestBody != null)
            {
                byte[] payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestBody));
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.SetRequestHeader("Accept", "application/json");
            return request;
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
    public sealed class OnlineDungeonDamageEventRequest
    {
        public string eventId;
        public string userId;
        public string joinToken;
        public string targetKind;
        public string targetRuntimeId;
        public float damage;
        public float stunDuration;
        public float targetRemainingHp;
        public bool targetDied;
        public OnlineDungeonDamageTargetEnemyInfo targetEnemy;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class OnlineDungeonDamageTargetEnemyInfo
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
        public float moveBlend;
        public float moveForward;
        public float moveStrafe;
    }

    [Serializable]
    public sealed class OnlineDungeonDamageEventListInfo
    {
        public List<OnlineDungeonDamageEventInfo> events;
    }

    [Serializable]
    public sealed class OnlineDungeonDamageEventInfo
    {
        public string eventId;
        public long sequence;
        public string sourceUserId;
        public string targetKind;
        public string targetRuntimeId;
        public float damage;
        public float stunDuration;
        public float targetRemainingHp;
        public bool targetDied;
        public float x;
        public float y;
        public float z;
        public string createdAtUtc;
    }
}
