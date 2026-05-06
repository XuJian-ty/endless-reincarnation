using System;
using UnityEngine;
using Game.GameFlow;
using Game.Online;
using ProjectBase;

namespace Game.Social
{
    public class SocialSession : BaseManager<SocialSession>
    {
        private const string ServerBaseUrlKey = "SocialServerBaseUrl";
        private const string DefaultServerBaseUrl = "http://127.0.0.1:5076";

        private SocialUserInfo _currentUser;
        private string _serverBaseUrl;

        public event Action SessionChanged;

        public SocialUserInfo CurrentUser => _currentUser;
        public bool IsLoggedIn => _currentUser != null && !string.IsNullOrWhiteSpace(_currentUser.userId);
        public string ServerBaseUrl => _serverBaseUrl;

        public SocialSession()
        {
            string savedUrl = PlayerPrefs.GetString(ServerBaseUrlKey, DefaultServerBaseUrl);
            _serverBaseUrl = NormalizeServerBaseUrl(savedUrl);
        }

        public void SetServerBaseUrl(string serverBaseUrl)
        {
            string normalizedUrl = NormalizeServerBaseUrl(serverBaseUrl);
            if (string.Equals(_serverBaseUrl, normalizedUrl, StringComparison.Ordinal))
                return;

            _serverBaseUrl = normalizedUrl;
            PlayerPrefs.SetString(ServerBaseUrlKey, _serverBaseUrl);
            PlayerPrefs.Save();
            SessionChanged?.Invoke();
        }

        public void Login(SocialUserInfo userInfo)
        {
            if (userInfo == null || string.IsNullOrWhiteSpace(userInfo.userId))
                return;

            _currentUser = new SocialUserInfo
            {
                userId = userInfo.userId?.Trim(),
                username = userInfo.username?.Trim(),
            };

            SocialAidRequestNotifier.GetInstance();
            SocialSessionQuitHandler.EnsureExists();
            SessionChanged?.Invoke();
        }

        public void Logout()
        {
            if (_currentUser == null)
                return;

            _currentUser = null;
            SessionChanged?.Invoke();
        }

        private static string NormalizeServerBaseUrl(string serverBaseUrl)
        {
            string trimmed = string.IsNullOrWhiteSpace(serverBaseUrl)
                ? DefaultServerBaseUrl
                : serverBaseUrl.Trim();

            return trimmed.TrimEnd('/');
        }
    }

    internal sealed class SocialSessionQuitHandler : MonoBehaviour
    {
        private static SocialSessionQuitHandler _instance;

        public static void EnsureExists()
        {
            if (_instance != null)
                return;

            GameObject root = new GameObject("SocialSessionQuitHandler");
            UnityEngine.Object.DontDestroyOnLoad(root);
            _instance = root.AddComponent<SocialSessionQuitHandler>();
        }

        private void OnApplicationQuit()
        {
            OnlineDungeonSessionCoordinator onlineCoordinator = OnlineDungeonSessionCoordinator.GetInstance();
            bool hadOnlineSession = onlineCoordinator.HasActiveSession;
            SocialAidSessionInfo session = onlineCoordinator.ActiveAidSession ?? SocialAidSessionCoordinator.GetInstance().ActiveSession;
            bool onlineSessionPrepared = onlineCoordinator.TryPrepareSaveAndQuit();
            if (onlineSessionPrepared)
                GameStateMachine.GetInstance().SaveCurrent(false);

            if (!hadOnlineSession && session != null && !string.IsNullOrWhiteSpace(session.sessionId))
            {
                SocialService.GetInstance().CloseAidSession(
                    session.sessionId,
                    (_, _) => { },
                    _ => { });
                onlineCoordinator.StopSession();
                SocialAidSessionCoordinator.GetInstance().StopSession();
            }

            if (SocialSession.GetInstance().IsLoggedIn)
            {
                SocialService.GetInstance().ClearActiveSaveContext(
                    (_, _) => { },
                    _ => { });
            }
        }
    }
}
