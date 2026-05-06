using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace Game.Online
{
    public sealed class OnlineDungeonRewardClient
    {
        public IEnumerator AddEnemyKill(
            string dungeonServerUrl,
            string instanceId,
            OnlineDungeonEnemyKillRequest requestBody,
            Action<OnlineDungeonRewardStateInfo> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(dungeonServerUrl, $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/enemy-kills");
            yield return SendStateRequest(UnityWebRequest.kHttpVerbPOST, url, requestBody, onSuccess, onError);
        }

        public IEnumerator GetRewardState(
            string dungeonServerUrl,
            string instanceId,
            string userId,
            string joinToken,
            Action<OnlineDungeonRewardStateInfo> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(
                dungeonServerUrl,
                $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/reward-state?userId={Uri.EscapeDataString(userId)}&joinToken={Uri.EscapeDataString(joinToken)}");
            yield return SendStateRequest(UnityWebRequest.kHttpVerbGET, url, null, onSuccess, onError);
        }

        public IEnumerator ClaimEnemyKill(
            string dungeonServerUrl,
            string instanceId,
            string enemyRuntimeId,
            OnlineDungeonKillRewardClaimRequest requestBody,
            Action<OnlineDungeonKillRewardClaimResultInfo> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(dungeonServerUrl, $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/enemy-kills/{Uri.EscapeDataString(enemyRuntimeId)}/claim");
            yield return SendKillRewardClaimRequest(UnityWebRequest.kHttpVerbPOST, url, requestBody, onSuccess, onError);
        }

        public IEnumerator PickupDrop(
            string dungeonServerUrl,
            string instanceId,
            string dropId,
            OnlineDungeonDropPickupRequest requestBody,
            Action<OnlineDungeonDropPickupResultInfo> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(dungeonServerUrl, $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/drops/{Uri.EscapeDataString(dropId)}/pickup");
            yield return SendPickupRequest(UnityWebRequest.kHttpVerbPOST, url, requestBody, onSuccess, onError);
        }

        private static IEnumerator SendStateRequest(
            string method,
            string url,
            object requestBody,
            Action<OnlineDungeonRewardStateInfo> onSuccess,
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

            OnlineDungeonApiResponse<OnlineDungeonRewardStateInfo> response = null;
            try
            {
                response = JsonConvert.DeserializeObject<OnlineDungeonApiResponse<OnlineDungeonRewardStateInfo>>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                yield break;
            }

            if (response == null || !response.success)
            {
                onError?.Invoke(response?.message ?? "联机副本奖励状态同步失败");
                yield break;
            }

            onSuccess?.Invoke(response.data);
        }

        private static IEnumerator SendPickupRequest(
            string method,
            string url,
            object requestBody,
            Action<OnlineDungeonDropPickupResultInfo> onSuccess,
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

            OnlineDungeonApiResponse<OnlineDungeonDropPickupResultInfo> response = null;
            try
            {
                response = JsonConvert.DeserializeObject<OnlineDungeonApiResponse<OnlineDungeonDropPickupResultInfo>>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                yield break;
            }

            if (response == null || !response.success)
            {
                onError?.Invoke(response?.message ?? "联机副本掉落拾取失败");
                yield break;
            }

            onSuccess?.Invoke(response.data);
        }

        private static IEnumerator SendKillRewardClaimRequest(
            string method,
            string url,
            object requestBody,
            Action<OnlineDungeonKillRewardClaimResultInfo> onSuccess,
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

            OnlineDungeonApiResponse<OnlineDungeonKillRewardClaimResultInfo> response = null;
            try
            {
                response = JsonConvert.DeserializeObject<OnlineDungeonApiResponse<OnlineDungeonKillRewardClaimResultInfo>>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                yield break;
            }

            if (response == null || !response.success)
            {
                onError?.Invoke(response?.message ?? "联机副本击杀奖励领取失败");
                yield break;
            }

            onSuccess?.Invoke(response.data);
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
    public sealed class OnlineDungeonEnemyKillRequest
    {
        public string userId;
        public string joinToken;
        public string enemyRuntimeId;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class OnlineDungeonDropPickupRequest
    {
        public string userId;
        public string joinToken;
    }

    [Serializable]
    public sealed class OnlineDungeonKillRewardClaimRequest
    {
        public string userId;
        public string joinToken;
    }

    [Serializable]
    public sealed class OnlineDungeonRewardStateInfo
    {
        public long version;
        public List<OnlineDungeonKillRewardInfo> killRewards;
        public List<OnlineDungeonDropInfo> drops;
    }

    [Serializable]
    public sealed class OnlineDungeonKillRewardInfo
    {
        public string enemyRuntimeId;
        public string killerUserId;
        public int exp;
        public int gold;
        public int talentPoints;
        public float x;
        public float y;
        public float z;
        public bool claimed;
        public string claimedAtUtc;
        public string createdAtUtc;
    }

    [Serializable]
    public sealed class OnlineDungeonDropInfo
    {
        public string dropId;
        public string enemyRuntimeId;
        public string itemType;
        public string payloadJson;
        public int count;
        public float x;
        public float y;
        public float z;
        public bool pickedUp;
        public string pickedUpByUserId;
        public string pickedUpAtUtc;
        public string createdAtUtc;
    }

    [Serializable]
    public sealed class OnlineDungeonDropPickupResultInfo
    {
        public bool accepted;
        public OnlineDungeonDropInfo drop;
        public OnlineDungeonRewardStateInfo state;
    }

    [Serializable]
    public sealed class OnlineDungeonKillRewardClaimResultInfo
    {
        public bool accepted;
        public OnlineDungeonKillRewardInfo reward;
        public OnlineDungeonRewardStateInfo state;
    }
}
