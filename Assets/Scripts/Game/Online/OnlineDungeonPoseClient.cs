using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace Game.Online
{
    public sealed class OnlineDungeonPoseClient
    {
        public IEnumerator UpsertPose(
            string dungeonServerUrl,
            string instanceId,
            OnlineDungeonPlayerPoseRequest requestBody,
            Action<List<OnlineDungeonPlayerPoseInfo>> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(dungeonServerUrl, $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/players/pose");
            yield return SendRequest(UnityWebRequest.kHttpVerbPOST, url, requestBody, onSuccess, onError);
        }

        public IEnumerator GetPoses(
            string dungeonServerUrl,
            string instanceId,
            string userId,
            string joinToken,
            Action<List<OnlineDungeonPlayerPoseInfo>> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(
                dungeonServerUrl,
                $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/players/poses?userId={Uri.EscapeDataString(userId)}&joinToken={Uri.EscapeDataString(joinToken)}");
            yield return SendRequest(UnityWebRequest.kHttpVerbGET, url, null, onSuccess, onError);
        }

        private static IEnumerator SendRequest(
            string method,
            string url,
            object requestBody,
            Action<List<OnlineDungeonPlayerPoseInfo>> onSuccess,
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

            OnlineDungeonApiResponse<OnlineDungeonPlayerPoseListInfo> response = null;
            try
            {
                response = JsonConvert.DeserializeObject<OnlineDungeonApiResponse<OnlineDungeonPlayerPoseListInfo>>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                yield break;
            }

            if (response == null || !response.success)
            {
                onError?.Invoke(response?.message ?? "联机副本玩家姿态同步失败");
                yield break;
            }

            onSuccess?.Invoke(response.data?.poses ?? new List<OnlineDungeonPlayerPoseInfo>());
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
    public sealed class OnlineDungeonPlayerPoseRequest
    {
        public string userId;
        public string joinToken;
        public float x;
        public float y;
        public float z;
        public float yaw;
        public bool aimActive;
        public bool aimLayerActive;
        public float aimPitch;
        public float aimDirectionX;
        public float aimDirectionY;
        public float aimDirectionZ;
        public float moveSpeed;
        public float moveX;
        public float moveY;
        public int action;
        public string actionId;
        public string skillEffectId;
        public int actionSequence;
        public float actionElapsedSeconds;
        public float actionNormalizedProgress;
        public int attackMode;
        public bool hasMeleeWeapon;
        public bool hasRangedWeapon;
        public float defense;
        public float damageReduce;
        public float currentHp;
        public float maxHp;
        public bool isDead;
    }

    [Serializable]
    public sealed class OnlineDungeonPlayerPoseListInfo
    {
        public List<OnlineDungeonPlayerPoseInfo> poses;
    }

    [Serializable]
    public sealed class OnlineDungeonPlayerPoseInfo
    {
        public string userId;
        public string displayName;
        public float x;
        public float y;
        public float z;
        public float yaw;
        public bool aimActive;
        public bool aimLayerActive;
        public float aimPitch;
        public float aimDirectionX;
        public float aimDirectionY;
        public float aimDirectionZ;
        public float moveSpeed;
        public float moveX;
        public float moveY;
        public int action;
        public string actionId;
        public string skillEffectId;
        public int actionSequence;
        public float actionElapsedSeconds;
        public float actionNormalizedProgress;
        public int attackMode;
        public bool hasMeleeWeapon;
        public bool hasRangedWeapon;
        public float defense;
        public float damageReduce;
        public float currentHp;
        public float maxHp;
        public bool isDead;
        public string updatedAtUtc;
    }

    [Serializable]
    public sealed class OnlineDungeonApiResponse<T>
    {
        public bool success;
        public string message;
        public T data;
    }
}
