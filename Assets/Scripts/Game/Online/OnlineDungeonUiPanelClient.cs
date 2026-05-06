using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace Game.Online
{
    public sealed class OnlineDungeonUiPanelClient
    {
        public IEnumerator AddPanelEvent(
            string dungeonServerUrl,
            string instanceId,
            OnlineDungeonUiPanelEventRequest requestBody,
            Action<List<OnlineDungeonUiPanelEventInfo>> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(dungeonServerUrl, $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/ui-panel-events");
            yield return SendRequest(UnityWebRequest.kHttpVerbPOST, url, requestBody, onSuccess, onError);
        }

        public IEnumerator GetPanelEvents(
            string dungeonServerUrl,
            string instanceId,
            string userId,
            string joinToken,
            long afterSequence,
            Action<List<OnlineDungeonUiPanelEventInfo>> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(
                dungeonServerUrl,
                $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/ui-panel-events?userId={Uri.EscapeDataString(userId)}&joinToken={Uri.EscapeDataString(joinToken)}&afterSequence={afterSequence}");
            yield return SendRequest(UnityWebRequest.kHttpVerbGET, url, null, onSuccess, onError);
        }

        public IEnumerator GetPanelState(
            string dungeonServerUrl,
            string instanceId,
            string userId,
            string joinToken,
            Action<OnlineDungeonUiPanelStateInfo> onSuccess,
            Action<string> onError)
        {
            string url = BuildUrl(
                dungeonServerUrl,
                $"/api/dungeons/{Uri.EscapeDataString(instanceId)}/ui-panel-state?userId={Uri.EscapeDataString(userId)}&joinToken={Uri.EscapeDataString(joinToken)}");
            yield return SendStateRequest(url, onSuccess, onError);
        }

        private static IEnumerator SendRequest(
            string method,
            string url,
            object requestBody,
            Action<List<OnlineDungeonUiPanelEventInfo>> onSuccess,
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

            OnlineDungeonApiResponse<OnlineDungeonUiPanelEventListInfo> response = null;
            try
            {
                response = JsonConvert.DeserializeObject<OnlineDungeonApiResponse<OnlineDungeonUiPanelEventListInfo>>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                yield break;
            }

            if (response == null || !response.success)
            {
                onError?.Invoke(response?.message ?? "联机副本面板事件同步失败");
                yield break;
            }

            onSuccess?.Invoke(response.data?.events ?? new List<OnlineDungeonUiPanelEventInfo>());
        }

        private static IEnumerator SendStateRequest(
            string url,
            Action<OnlineDungeonUiPanelStateInfo> onSuccess,
            Action<string> onError)
        {
            using UnityWebRequest request = BuildRequest(UnityWebRequest.kHttpVerbGET, url, null);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                onError?.Invoke(string.IsNullOrWhiteSpace(request.downloadHandler?.text)
                    ? request.error
                    : ExtractErrorMessage(request.downloadHandler.text, request.error));
                yield break;
            }

            OnlineDungeonApiResponse<OnlineDungeonUiPanelStateInfo> response = null;
            try
            {
                response = JsonConvert.DeserializeObject<OnlineDungeonApiResponse<OnlineDungeonUiPanelStateInfo>>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                yield break;
            }

            if (response == null || !response.success)
            {
                onError?.Invoke(response?.message ?? "联机副本面板状态同步失败");
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
    public sealed class OnlineDungeonUiPanelEventRequest
    {
        public string eventId;
        public string userId;
        public string joinToken;
        public string panelName;
        public bool open;
    }

    [Serializable]
    public sealed class OnlineDungeonUiPanelEventListInfo
    {
        public List<OnlineDungeonUiPanelEventInfo> events;
    }

    [Serializable]
    public sealed class OnlineDungeonUiPanelEventInfo
    {
        public string eventId;
        public long sequence;
        public string sourceUserId;
        public string panelName;
        public bool open;
        public string createdAtUtc;
    }

    [Serializable]
    public sealed class OnlineDungeonUiPanelStateInfo
    {
        public long version;
        public string openPanelName;
        public bool hasOpenPanel;
        public string updatedByUserId;
        public string updatedAtUtc;
    }
}
