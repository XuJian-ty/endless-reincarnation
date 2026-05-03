using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Game.GameFlow;
using Game.Social;
using Newtonsoft.Json;
using ProjectBase;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Social
{
    public class SocialService : BaseManager<SocialService>
    {
        public void Register(
            string username,
            string password,
            Action<SocialUserInfo, string> onSuccess,
            Action<string> onError)
        {
            SendJsonRequest(
                UnityWebRequest.kHttpVerbPOST,
                "/api/auth/register",
                new SocialAuthRequest
                {
                    username = username?.Trim(),
                    password = password?.Trim(),
                },
                onSuccess,
                onError);
        }

        public void Login(
            string username,
            string password,
            Action<SocialUserInfo, string> onSuccess,
            Action<string> onError)
        {
            SendJsonRequest(
                UnityWebRequest.kHttpVerbPOST,
                "/api/auth/login",
                new SocialAuthRequest
                {
                    username = username?.Trim(),
                    password = password?.Trim(),
                },
                onSuccess,
                onError);
        }

        public void SearchUsers(
            string keyword,
            Action<List<SocialUserSearchInfo>, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            string query = $"?keyword={Uri.EscapeDataString((keyword ?? string.Empty).Trim())}&requesterUserId={Uri.EscapeDataString(currentUser.userId)}";
            SendJsonRequest<List<SocialUserSearchInfo>>(
                UnityWebRequest.kHttpVerbGET,
                "/api/users/search" + query,
                null,
                onSuccess,
                onError);
        }

        public void AddFriend(
            string targetUsername,
            string message,
            string remark,
            Action<SocialFriendRequestInfo, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            SendJsonRequest(
                UnityWebRequest.kHttpVerbPOST,
                "/api/friends/add",
                new SocialAddFriendRequest
                {
                    requesterUserId = currentUser.userId,
                    targetUsername = targetUsername?.Trim(),
                    message = message?.Trim(),
                    remark = remark?.Trim(),
                },
                onSuccess,
                onError);
        }

        public void GetPendingFriendRequests(
            Action<List<SocialFriendRequestInfo>, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            string query = $"?userId={Uri.EscapeDataString(currentUser.userId)}";
            SendJsonRequest<List<SocialFriendRequestInfo>>(
                UnityWebRequest.kHttpVerbGET,
                "/api/friends/requests" + query,
                null,
                onSuccess,
                onError);
        }

        public void RespondFriendRequest(
            string requestId,
            bool accept,
            string remark,
            Action<SocialFriendInfo, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            SendJsonRequest(
                UnityWebRequest.kHttpVerbPOST,
                "/api/friends/respond",
                new SocialRespondFriendRequest
                {
                    targetUserId = currentUser.userId,
                    requestId = requestId?.Trim(),
                    decision = accept ? "accept" : "reject",
                    remark = remark?.Trim(),
                },
                onSuccess,
                onError);
        }

        public void GetFriends(
            Action<List<SocialFriendInfo>, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            string query = $"?userId={Uri.EscapeDataString(currentUser.userId)}";
            SendJsonRequest<List<SocialFriendInfo>>(
                UnityWebRequest.kHttpVerbGET,
                "/api/friends" + query,
                null,
                onSuccess,
                onError);
        }

        public void RemoveFriend(
            string friendUserId,
            Action<object, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            SendJsonRequest(
                UnityWebRequest.kHttpVerbPOST,
                "/api/friends/remove",
                new SocialRemoveFriendRequest
                {
                    requesterUserId = currentUser.userId,
                    friendUserId = friendUserId?.Trim(),
                },
                onSuccess,
                onError);
        }

        public void GetFriendPresenceSummaries(
            Action<List<SocialFriendPresenceInfo>, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            string query = $"?userId={Uri.EscapeDataString(currentUser.userId)}";
            SendJsonRequest<List<SocialFriendPresenceInfo>>(
                UnityWebRequest.kHttpVerbGET,
                "/api/friends/presence" + query,
                null,
                onSuccess,
                onError);
        }

        public void SendMessage(
            string toUserId,
            string content,
            Action<SocialMessageInfo, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            SendJsonRequest(
                UnityWebRequest.kHttpVerbPOST,
                "/api/messages/send",
                new SocialSendMessageRequest
                {
                    fromUserId = currentUser.userId,
                    toUserId = toUserId?.Trim(),
                    content = content?.Trim(),
                },
                onSuccess,
                onError);
        }

        public void GetConversation(
            string friendUserId,
            Action<List<SocialMessageInfo>, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            string query =
                $"?userId={Uri.EscapeDataString(currentUser.userId)}&friendUserId={Uri.EscapeDataString(friendUserId?.Trim() ?? string.Empty)}&afterMessageId=0";

            SendJsonRequest<List<SocialMessageInfo>>(
                UnityWebRequest.kHttpVerbGET,
                "/api/messages/thread" + query,
                null,
                onSuccess,
                onError);
        }

        public void SetActiveSaveContext(
            string saveId,
            Action<SocialActiveSaveContextInfo, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            SendJsonRequest(
                UnityWebRequest.kHttpVerbPOST,
                "/api/active-save",
                new SocialSetActiveSaveContextRequest
                {
                    userId = currentUser.userId,
                    saveId = saveId?.Trim(),
                },
                onSuccess,
                onError);
        }

        public void GetActiveSaveContext(
            Action<SocialActiveSaveContextInfo, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            string query = $"?userId={Uri.EscapeDataString(currentUser.userId)}";
            SendJsonRequest<SocialActiveSaveContextInfo>(
                UnityWebRequest.kHttpVerbGET,
                "/api/active-save" + query,
                null,
                onSuccess,
                onError);
        }

        public void ClearActiveSaveContext(
            Action<object, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            SendJsonRequest(
                UnityWebRequest.kHttpVerbPOST,
                "/api/active-save/clear",
                new SocialClearActiveSaveContextRequest
                {
                    userId = currentUser.userId,
                },
                onSuccess,
                onError);
        }

        public void CreateAidRequest(
            string helperUserId,
            string message,
            Action<SocialAidRequestInfo, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            Transform hostTransform = GameStateMachine.GetInstance().LevelPlayerTransform;

            SendJsonRequest(
                UnityWebRequest.kHttpVerbPOST,
                "/api/aid/request",
                new SocialCreateAidRequest
                {
                    hostUserId = currentUser.userId,
                    helperUserId = helperUserId?.Trim(),
                    message = message?.Trim(),
                    hostPlayerX = hostTransform != null ? hostTransform.position.x : 0f,
                    hostPlayerY = hostTransform != null ? hostTransform.position.y : 0f,
                    hostPlayerZ = hostTransform != null ? hostTransform.position.z : 0f,
                    hostPlayerYaw = hostTransform != null ? hostTransform.eulerAngles.y : 0f,
                },
                onSuccess,
                onError);
        }

        public void GetPendingAidRequests(
            Action<List<SocialAidRequestInfo>, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            string query = $"?helperUserId={Uri.EscapeDataString(currentUser.userId)}";
            SendJsonRequest<List<SocialAidRequestInfo>>(
                UnityWebRequest.kHttpVerbGET,
                "/api/aid/requests" + query,
                null,
                onSuccess,
                onError);
        }

        public void RespondAidRequest(
            string requestId,
            bool accept,
            Action<SocialAidSessionInfo, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            SendJsonRequest(
                UnityWebRequest.kHttpVerbPOST,
                "/api/aid/respond",
                new SocialRespondAidRequest
                {
                    helperUserId = currentUser.userId,
                    requestId = requestId?.Trim(),
                    decision = accept ? "accept" : "reject",
                },
                onSuccess,
                onError);
        }

        public void GetActiveAidSession(
            Action<SocialAidSessionInfo, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            string query = $"?userId={Uri.EscapeDataString(currentUser.userId)}";
            SendJsonRequest<SocialAidSessionInfo>(
                UnityWebRequest.kHttpVerbGET,
                "/api/aid/session" + query,
                null,
                onSuccess,
                onError);
        }

        public void CloseAidSession(
            string sessionId,
            Action<SocialAidSessionInfo, string> onSuccess,
            Action<string> onError)
        {
            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            if (currentUser == null)
            {
                onError?.Invoke("当前未登录账号");
                return;
            }

            SendJsonRequest(
                UnityWebRequest.kHttpVerbPOST,
                "/api/aid/session/close",
                new SocialCloseAidSessionRequest
                {
                    requesterUserId = currentUser.userId,
                    sessionId = sessionId?.Trim(),
                },
                onSuccess,
                onError);
        }

        private void SendJsonRequest<TResponse>(
            string method,
            string relativeUrl,
            object requestBody,
            Action<TResponse, string> onSuccess,
            Action<string> onError)
        {
            string baseUrl = SocialSession.GetInstance().ServerBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                onError?.Invoke("社交服务地址为空");
                return;
            }

            MonoMgr.GetInstance().StartCoroutine(SendJsonRequestCoroutine(
                method,
                CombineUrl(baseUrl, relativeUrl),
                requestBody,
                onSuccess,
                onError));
        }

        private static IEnumerator SendJsonRequestCoroutine<TResponse>(
            string method,
            string url,
            object requestBody,
            Action<TResponse, string> onSuccess,
            Action<string> onError)
        {
            using UnityWebRequest request = BuildRequest(method, url, requestBody);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                string errorMessage = TryExtractErrorMessage<TResponse>(request.downloadHandler?.text);
                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = string.IsNullOrWhiteSpace(request.error) ? "请求失败" : request.error;
                onError?.Invoke(errorMessage);
                yield break;
            }

            string responseText = request.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(responseText))
            {
                onError?.Invoke("服务端返回了空响应");
                yield break;
            }

            SocialApiResponse<TResponse> response;
            try
            {
                response = JsonConvert.DeserializeObject<SocialApiResponse<TResponse>>(responseText);
            }
            catch (Exception e)
            {
                onError?.Invoke($"响应解析失败：{e.Message}");
                yield break;
            }

            if (response == null)
            {
                onError?.Invoke("响应解析失败");
                yield break;
            }

            if (!response.success)
            {
                onError?.Invoke(string.IsNullOrWhiteSpace(response.message) ? "服务端返回失败" : response.message);
                yield break;
            }

            onSuccess?.Invoke(response.data, response.message ?? string.Empty);
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

        private static string CombineUrl(string baseUrl, string relativeUrl)
        {
            string normalizedBaseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
            string normalizedRelativeUrl = (relativeUrl ?? string.Empty).Trim();
            if (!normalizedRelativeUrl.StartsWith("/", StringComparison.Ordinal))
                normalizedRelativeUrl = "/" + normalizedRelativeUrl;

            return normalizedBaseUrl + normalizedRelativeUrl;
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
    }
}
