using System.Collections;
using System.Collections.Generic;
using Game.GameFlow;
using Game.Online;
using Game.UI;
using ProjectBase;
using UnityEngine;

namespace Game.Social
{
    /// <summary>
    /// 常驻援助请求提醒器：在玩家自己的游戏界面轮询待处理援助请求，并自动弹出小弹窗。
    /// </summary>
    public class SocialAidRequestNotifier : BaseManager<SocialAidRequestNotifier>
    {
        private readonly HashSet<string> _shownRequestIds = new HashSet<string>();
        private Coroutine _pollingCoroutine;

        public SocialAidRequestNotifier()
        {
            StartPolling();
        }

        private void StartPolling()
        {
            if (_pollingCoroutine != null)
                return;

            _pollingCoroutine = MonoMgr.GetInstance().StartCoroutine(PollLoop());
        }

        private IEnumerator PollLoop()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(2f);
            while (true)
            {
                if (ShouldPoll())
                {
                    SocialService.GetInstance().GetPendingAidRequests(
                        HandleRequests,
                        _ => { });
                    SocialService.GetInstance().GetActiveAidSession(
                        HandleActiveSession,
                        _ => { });
                }

                yield return wait;
            }
        }

        private static bool ShouldPoll()
        {
            return SocialSession.GetInstance().IsLoggedIn
                   && GameStateMachine.GetInstance().CurrentState == GameStateMachine.State.InLevel;
        }

        private void HandleRequests(List<SocialAidRequestInfo> requests, string _)
        {
            if (requests == null || requests.Count == 0)
                return;

            for (int i = 0; i < requests.Count; i++)
            {
                SocialAidRequestInfo request = requests[i];
                if (request == null || string.IsNullOrWhiteSpace(request.requestId))
                    continue;

                if (_shownRequestIds.Contains(request.requestId))
                    continue;

                _shownRequestIds.Add(request.requestId);
                UIManager.GetInstance()?.ShowPanel<SocialAidRequestPopupPanel>(
                    PanelNames.SocialAidRequestPopup,
                    PanelLayers.SocialAidRequestPopup,
                    panel => panel.Init(request));
                break;
            }
        }

        private static void HandleActiveSession(SocialAidSessionInfo session, string _)
        {
            OnlineDungeonSessionCoordinator onlineCoordinator = OnlineDungeonSessionCoordinator.GetInstance();
            SocialAidSessionCoordinator coordinator = SocialAidSessionCoordinator.GetInstance();
            if (session == null || string.IsNullOrWhiteSpace(session.sessionId))
            {
                if (onlineCoordinator.IsAidJoinerRole)
                    onlineCoordinator.ReturnAidJoinerToOwnLevel(false);
                else
                    onlineCoordinator.StopSession();
                if (coordinator.HasActiveSession)
                {
                    if (coordinator.IsHostRole)
                        coordinator.StopSession();
                    else
                        coordinator.ReturnHelperToOwnLevel(false);
                }
                return;
            }

            if (onlineCoordinator.HasActiveSession)
                return;

            if (!onlineCoordinator.TryEnterSession(session, out string error))
                Debug.LogWarning($"[SocialAidRequestNotifier] 进入联机副本失败：{error}");
        }
    }
}
