using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace Game.Online
{
    public sealed class OnlineDungeonWorldSnapshotClient
    {
        public IEnumerator UpsertSnapshot(
            string dungeonServerUrl,
            string instanceId,
            OnlineDungeonWorldSnapshotRequest requestBody,
            Action<OnlineDungeonWorldSnapshotInfo> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(dungeonServerUrl, $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/world-snapshot");
            yield return SendRequest(UnityWebRequest.kHttpVerbPOST, url, requestBody, onSuccess, onError);
        }

        public IEnumerator GetSnapshot(
            string dungeonServerUrl,
            string instanceId,
            string userId,
            string joinToken,
            Action<OnlineDungeonWorldSnapshotInfo> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(
                dungeonServerUrl,
                $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/world-snapshot?userId={Uri.EscapeDataString(userId)}&joinToken={Uri.EscapeDataString(joinToken)}");
            yield return SendRequest(UnityWebRequest.kHttpVerbGET, url, null, onSuccess, onError);
        }

        private static IEnumerator SendRequest(
            string method,
            string url,
            object requestBody,
            Action<OnlineDungeonWorldSnapshotInfo> onSuccess,
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

            OnlineDungeonApiResponse<OnlineDungeonWorldSnapshotInfo> response = null;
            try
            {
                response = JsonConvert.DeserializeObject<OnlineDungeonApiResponse<OnlineDungeonWorldSnapshotInfo>>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                yield break;
            }

            if (response == null || !response.success)
            {
                onError?.Invoke(response?.message ?? "联机副本世界快照同步失败");
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
    public sealed class OnlineDungeonWorldSnapshotRequest
    {
        public string userId;
        public string joinToken;
        public string sceneName;
        public string snapshotJson;
    }

    [Serializable]
    public sealed class OnlineDungeonWorldSnapshotInfo
    {
        public string ownerUserId;
        public string sceneName;
        public string snapshotJson;
        public long version;
        public string updatedAtUtc;
    }
}
