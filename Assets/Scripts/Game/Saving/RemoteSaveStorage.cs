using System;
using System.Collections.Generic;
using System.Text;
using Game.Social;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Saving
{
    /// <summary>
    /// 服务端存档存储：通过社交后端的 /api/saves 接口同步当前账号的存档。
    /// 目前使用同步阻塞请求，先保证接入最小改动；后续可再改成真正异步加载流程。
    /// </summary>
    public class RemoteSaveStorage : ISaveStorage, IIndexedSaveStorage
    {
        private const string SavesPath = "/api/saves";

        public bool Exists(string id)
        {
            return Read(id) != null;
        }

        public void Write(string id, SaveData data)
        {
            if (string.IsNullOrWhiteSpace(id) || data == null)
                return;

            SocialUserInfo currentUser = RequireCurrentUser();
            RemoteSaveContentDto response = SendRequest<RemoteSaveContentDto>(
                UnityWebRequest.kHttpVerbPOST,
                $"{SavesPath}/{Uri.EscapeDataString(id.Trim())}",
                new RemoteSaveUpsertRequest
                {
                    userId = currentUser.userId,
                    playerName = data.playerName ?? string.Empty,
                    portraitId = string.IsNullOrWhiteSpace(data.portraitId) ? SaveSystem.DefaultPortraitId : data.portraitId.Trim(),
                    levelIndex = data.run?.levelIndex ?? 1,
                    version = data.version,
                    saveJson = JsonConvert.SerializeObject(data, Formatting.Indented),
                });

            if (response == null)
                throw new InvalidOperationException("服务端未返回存档结果。");
        }

        public SaveData Read(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            SocialUserInfo currentUser = RequireCurrentUser();
            RemoteSaveContentDto response = SendRequest<RemoteSaveContentDto>(
                UnityWebRequest.kHttpVerbGET,
                $"{SavesPath}/{Uri.EscapeDataString(id.Trim())}?userId={Uri.EscapeDataString(currentUser.userId)}",
                null);

            if (response == null || string.IsNullOrWhiteSpace(response.saveJson))
                return null;

            return JsonConvert.DeserializeObject<SaveData>(response.saveJson);
        }

        public void Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            SocialUserInfo currentUser = RequireCurrentUser();
            SendRequest<object>(
                UnityWebRequest.kHttpVerbDELETE,
                $"{SavesPath}/{Uri.EscapeDataString(id.Trim())}?userId={Uri.EscapeDataString(currentUser.userId)}",
                null);
        }

        public List<SaveEntry> GetAllEntries()
        {
            SocialUserInfo currentUser = RequireCurrentUser();
            List<RemoteSaveSummaryDto> response = SendRequest<List<RemoteSaveSummaryDto>>(
                UnityWebRequest.kHttpVerbGET,
                $"{SavesPath}?userId={Uri.EscapeDataString(currentUser.userId)}",
                null);

            List<SaveEntry> result = new List<SaveEntry>();
            if (response == null)
                return result;

            for (int i = 0; i < response.Count; i++)
            {
                RemoteSaveSummaryDto save = response[i];
                if (save == null || string.IsNullOrWhiteSpace(save.saveId))
                    continue;

                long lastModifiedTicks = 0L;
                if (!string.IsNullOrWhiteSpace(save.updatedAtUtc)
                    && DateTime.TryParse(save.updatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsedUtc))
                {
                    lastModifiedTicks = parsedUtc.ToUniversalTime().Ticks;
                }

                result.Add(new SaveEntry
                {
                    id = save.saveId.Trim(),
                    playerName = string.IsNullOrWhiteSpace(save.playerName) ? "玩家" : save.playerName.Trim(),
                    levelIndex = Mathf.Max(1, save.levelIndex),
                    lastModifiedTicks = lastModifiedTicks,
                });
            }

            return result;
        }

        private static SocialUserInfo RequireCurrentUser()
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.userId))
                throw new InvalidOperationException("当前未登录账号，无法访问服务端存档。");

            return currentUser;
        }

        private static TResponse SendRequest<TResponse>(string method, string relativeUrl, object requestBody)
        {
            string baseUrl = SocialSession.GetInstance().ServerBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("社交服务地址为空。");

            string url = BuildUrl(baseUrl, relativeUrl);
            using UnityWebRequest request = BuildRequest(method, url, requestBody);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone) { }

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                string errorMessage = TryExtractErrorMessage<TResponse>(request.downloadHandler?.text);
                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = string.IsNullOrWhiteSpace(request.error) ? "请求服务端存档失败" : request.error;
                throw new InvalidOperationException(errorMessage);
            }

            string responseText = request.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(responseText))
                throw new InvalidOperationException("服务端返回了空响应。");

            SocialApiResponse<TResponse> response;
            try
            {
                response = JsonConvert.DeserializeObject<SocialApiResponse<TResponse>>(responseText);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"服务端响应解析失败：{e.Message}", e);
            }

            if (response == null)
                throw new InvalidOperationException("服务端响应解析失败。");

            if (!response.success)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.message) ? "服务端返回失败" : response.message);

            return response.data;
        }

        private static UnityWebRequest BuildRequest(string method, string url, object requestBody)
        {
            UnityWebRequest request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
            };

            if (requestBody != null)
            {
                string requestJson = JsonConvert.SerializeObject(requestBody);
                byte[] payload = Encoding.UTF8.GetBytes(requestJson);
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.SetRequestHeader("Accept", "application/json");
            return request;
        }

        private static string BuildUrl(string baseUrl, string relativeUrl)
        {
            string normalizedBase = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
            string normalizedRelative = (relativeUrl ?? string.Empty).Trim();
            if (!normalizedRelative.StartsWith("/", StringComparison.Ordinal))
                normalizedRelative = "/" + normalizedRelative;

            return normalizedBase + normalizedRelative;
        }

        private static string TryExtractErrorMessage<TResponse>(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return string.Empty;

            try
            {
                SocialApiResponse<TResponse> response = JsonConvert.DeserializeObject<SocialApiResponse<TResponse>>(responseText);
                return response?.message ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        [Serializable]
        private sealed class RemoteSaveUpsertRequest
        {
            public string userId;
            public string playerName;
            public string portraitId;
            public int levelIndex;
            public int version;
            public string saveJson;
        }

        [Serializable]
        private class RemoteSaveSummaryDto
        {
            public string saveId;
            public string playerName;
            public string portraitId;
            public int levelIndex;
            public int version;
            public string updatedAtUtc;
        }

        [Serializable]
        private sealed class RemoteSaveContentDto : RemoteSaveSummaryDto
        {
            public string saveJson;
        }
    }
}
