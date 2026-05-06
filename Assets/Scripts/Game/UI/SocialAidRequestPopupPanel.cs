using System;
using Game.Online;
using Game.Social;
using ProjectBase;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class SocialAidRequestPopupPanel : BasePanel
    {
        private SocialAidRequestInfo _request;
        private Text _titleText;
        private Text _messageText;
        private bool _uiBuilt;

        protected override void Awake()
        {
            EnsureBuilt();
            base.Awake();
            RegisterClick("Btn_Accept", OnAcceptClicked);
            RegisterClick("Btn_Reject", OnRejectClicked);
            _titleText = GetControl<Text>("Txt_Title");
            _messageText = GetControl<Text>("Txt_Message");
        }

        public void Init(SocialAidRequestInfo request)
        {
            _request = request;
            if (_titleText != null)
                _titleText.text = "收到援助请求";

            if (_messageText != null)
            {
                string displayName = !string.IsNullOrWhiteSpace(request?.hostPlayerName) ? request.hostPlayerName : "好友";
                string content = $"玩家 {displayName} 请求你前往关卡 {Mathf.Max(1, request?.hostLevelIndex ?? 1)} 支援。";
                if (!string.IsNullOrWhiteSpace(request?.message))
                    content += $"\n\n说明：{request.message}";
                _messageText.text = content;
            }
        }

        public override void ShowMe()
        {
            EnsureBuilt();
            gameObject.SetActive(true);
        }

        public override void HideMe()
        {
            gameObject.SetActive(false);
        }

        private void OnAcceptClicked()
        {
            if (_request == null || string.IsNullOrWhiteSpace(_request.requestId))
            {
                UIManager.GetInstance()?.HidePanel(PanelNames.SocialAidRequestPopup);
                return;
            }

            SocialService.GetInstance().RespondAidRequest(
                _request.requestId,
                true,
                (session, _) =>
                {
                    if (session != null)
                    {
                        if (!OnlineDungeonSessionCoordinator.GetInstance().TryEnterSession(session, out string error))
                            Debug.LogWarning($"[SocialAidRequestPopupPanel] 进入联机副本失败：{error}");
                    }
                    UIManager.GetInstance()?.HidePanel(PanelNames.SocialAidRequestPopup);
                },
                error => Debug.LogWarning($"[SocialAidRequestPopupPanel] 接受援助请求失败：{error}"));
        }

        private void OnRejectClicked()
        {
            if (_request == null || string.IsNullOrWhiteSpace(_request.requestId))
            {
                UIManager.GetInstance()?.HidePanel(PanelNames.SocialAidRequestPopup);
                return;
            }

            SocialService.GetInstance().RespondAidRequest(
                _request.requestId,
                false,
                (_, _) => UIManager.GetInstance()?.HidePanel(PanelNames.SocialAidRequestPopup),
                error => Debug.LogWarning($"[SocialAidRequestPopupPanel] 拒绝援助请求失败：{error}"));
        }

        private void EnsureBuilt()
        {
            if (_uiBuilt)
                return;

            if (transform.Find("Window") != null)
            {
                _titleText = GetControl<Text>("Txt_Title");
                _messageText = GetControl<Text>("Txt_Message");
                _uiBuilt = true;
                return;
            }

            RectTransform root = transform as RectTransform;
            RuntimeOverlayPanelBuilder.EnsureFullScreenRoot(root);
            RuntimeOverlayPanelBuilder.EnsureOverlayBackground(gameObject, new Color(0f, 0f, 0f, 0.35f));

            RectTransform window = RuntimeOverlayPanelBuilder.CreateWindow(
                transform,
                "Window",
                new Vector2(520f, 260f),
                new Color(0.12f, 0.13f, 0.17f, 0.98f));

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Title",
                "收到援助请求",
                30,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 82f),
                new Vector2(320f, 42f),
                Color.white,
                FontStyle.Bold);

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Message",
                "好友请求你前往支援。",
                20,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 10f),
                new Vector2(420f, 90f),
                new Color(0.92f, 0.92f, 0.92f, 1f));

            RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Accept",
                "接受",
                new Vector2(-110f, -78f),
                new Vector2(160f, 52f),
                new Color(0.26f, 0.55f, 0.34f, 1f),
                Color.white);

            RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Reject",
                "拒绝",
                new Vector2(110f, -78f),
                new Vector2(160f, 52f),
                new Color(0.56f, 0.28f, 0.28f, 1f),
                Color.white);

            _uiBuilt = true;
        }
    }
}
