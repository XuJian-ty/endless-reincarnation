using System;
using System.Collections.Generic;
using Game.Social;
using ProjectBase;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class SocialAddFriendPanel : BasePanel
    {
        private InputField _searchInput;
        private Text _statusText;
        private RectTransform _resultContent;
        private RectTransform _requestPopup;
        private InputField _requestMessageInput;
        private InputField _requestRemarkInput;
        private SocialUserSearchInfo _pendingAddTarget;
        private bool _uiBuilt;
        private Action _onFriendAdded;

        protected override void Awake()
        {
            base.Awake();
            EnsureBuilt();
        }

        public void Init(Action onFriendAdded)
        {
            _onFriendAdded = onFriendAdded;
            if (_searchInput != null)
                _searchInput.text = string.Empty;
            HideRequestPopup();
            SetStatus("输入账号名后搜索玩家，点击添加后填写申请消息", new Color(0.92f, 0.92f, 0.92f, 1f));
            RebuildResults(new List<SocialUserSearchInfo>());
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

        private void EnsureBuilt()
        {
            if (_uiBuilt)
                return;

            if (transform.Find("Window") != null)
            {
                _searchInput = GetControl<InputField>("Input_Search");
                _statusText = GetControl<Text>("Txt_Status");
                _resultContent = transform.Find("Window/Scroll_Results/Viewport/Content") as RectTransform;
                _requestPopup = transform.Find("Window/RequestPopup") as RectTransform;
                _requestMessageInput = _requestPopup != null ? _requestPopup.Find("Input_RequestMessage")?.GetComponent<InputField>() : null;
                _requestRemarkInput = _requestPopup != null ? _requestPopup.Find("Input_RequestRemark")?.GetComponent<InputField>() : null;
                if (_requestPopup == null || _requestMessageInput == null || _requestRemarkInput == null)
                    CreateRequestPopup(transform.Find("Window"));

                Button prefabCloseButton = GetControl<Button>("Btn_Close");
                if (prefabCloseButton != null)
                {
                    prefabCloseButton.onClick.RemoveAllListeners();
                    prefabCloseButton.onClick.AddListener(() => UIManager.GetInstance()?.HidePanel(PanelNames.SocialAddFriend));
                }

                Button prefabSearchButton = GetControl<Button>("Btn_Search");
                if (prefabSearchButton != null)
                {
                    prefabSearchButton.onClick.RemoveAllListeners();
                    prefabSearchButton.onClick.AddListener(OnSearchClicked);
                }

                Button prefabCancelRequestButton = _requestPopup != null ? _requestPopup.Find("Btn_CancelRequest")?.GetComponent<Button>() : null;
                if (prefabCancelRequestButton != null)
                {
                    prefabCancelRequestButton.onClick.RemoveAllListeners();
                    prefabCancelRequestButton.onClick.AddListener(HideRequestPopup);
                }

                Button prefabSendRequestButton = _requestPopup != null ? _requestPopup.Find("Btn_SendRequest")?.GetComponent<Button>() : null;
                if (prefabSendRequestButton != null)
                {
                    prefabSendRequestButton.onClick.RemoveAllListeners();
                    prefabSendRequestButton.onClick.AddListener(SendPendingFriendRequest);
                }

                if (_searchInput != null && _statusText != null && _resultContent != null)
                {
                    _uiBuilt = true;
                    return;
                }
            }

            RectTransform root = transform as RectTransform;
            RuntimeOverlayPanelBuilder.EnsureFullScreenRoot(root);
            RuntimeOverlayPanelBuilder.EnsureOverlayBackground(gameObject, new Color(0f, 0f, 0f, 0.6f));

            RectTransform window = RuntimeOverlayPanelBuilder.CreateWindow(
                transform,
                "Window",
                new Vector2(760f, 620f),
                new Color(0.11f, 0.13f, 0.17f, 0.98f));

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Title",
                "添加好友",
                32,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 250f),
                new Vector2(240f, 44f),
                Color.white,
                FontStyle.Bold);

            Button closeButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Close",
                "关闭",
                new Vector2(280f, 250f),
                new Vector2(120f, 48f),
                new Color(0.34f, 0.34f, 0.34f, 1f),
                Color.white);
            closeButton.onClick.AddListener(() => UIManager.GetInstance()?.HidePanel(PanelNames.SocialAddFriend));

            _searchInput = RuntimeOverlayPanelBuilder.CreateInputField(
                window,
                "Input_Search",
                "输入账号名关键字",
                string.Empty,
                new Vector2(-60f, 180f),
                new Vector2(360f, 56f),
                new Color(0.18f, 0.21f, 0.26f, 1f),
                Color.white);

            Button searchButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Search",
                "搜索",
                new Vector2(235f, 180f),
                new Vector2(120f, 56f),
                new Color(0.24f, 0.45f, 0.72f, 1f),
                Color.white);
            searchButton.onClick.AddListener(OnSearchClicked);

            RuntimeOverlayPanelBuilder.CreateScrollView(
                window,
                "Scroll_Results",
                new Vector2(0f, -10f),
                new Vector2(620f, 310f),
                new Color(0.08f, 0.1f, 0.13f, 0.95f),
                out _resultContent);

            _statusText = RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Status",
                string.Empty,
                20,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -245f),
                new Vector2(620f, 54f),
                new Color(0.92f, 0.92f, 0.92f, 1f));

            CreateRequestPopup(window);
            _uiBuilt = true;
        }

        private void OnSearchClicked()
        {
            if (!SocialSession.GetInstance().IsLoggedIn)
            {
                SetStatus("请先登录账号", new Color(1f, 0.72f, 0.4f, 1f));
                return;
            }

            string keyword = _searchInput != null ? _searchInput.text : string.Empty;
            SetStatus("正在搜索账号...", new Color(0.92f, 0.92f, 0.92f, 1f));
            SocialService.GetInstance().SearchUsers(
                keyword,
                (results, message) =>
                {
                    RebuildResults(results);
                    SetStatus(message, new Color(0.6f, 0.92f, 0.66f, 1f));
                },
                error => SetStatus(error, new Color(1f, 0.62f, 0.62f, 1f)));
        }

        private void RebuildResults(List<SocialUserSearchInfo> results)
        {
            ClearChildren(_resultContent);
            if (_resultContent == null)
                return;

            if (results == null || results.Count == 0)
            {
                CreateHintRow(_resultContent, "暂无搜索结果");
                return;
            }

            for (int i = 0; i < results.Count; i++)
            {
                SocialUserSearchInfo result = results[i];
                CreateSearchResultRow(_resultContent, "Result_" + i, result);
            }
        }

        private void OnAddFriendClicked(SocialUserSearchInfo result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.username))
                return;

            _pendingAddTarget = result;
            if (_requestMessageInput != null)
                _requestMessageInput.text = "我是来加你为好友的";
            if (_requestRemarkInput != null)
                _requestRemarkInput.text = string.Empty;
            if (_requestPopup != null)
                _requestPopup.gameObject.SetActive(true);
            SetStatus($"正在申请添加：{result.username}", new Color(0.92f, 0.92f, 0.92f, 1f));
        }

        private void SendPendingFriendRequest()
        {
            if (_pendingAddTarget == null || string.IsNullOrWhiteSpace(_pendingAddTarget.username))
                return;

            string message = _requestMessageInput != null ? _requestMessageInput.text : string.Empty;
            string remark = _requestRemarkInput != null ? _requestRemarkInput.text : string.Empty;
            SetStatus($"正在发送好友申请：{_pendingAddTarget.username}", new Color(0.92f, 0.92f, 0.92f, 1f));
            SocialService.GetInstance().AddFriend(
                _pendingAddTarget.username,
                message,
                remark,
                (_, responseMessage) =>
                {
                    HideRequestPopup();
                    SetStatus(responseMessage, new Color(0.6f, 0.92f, 0.66f, 1f));
                    _onFriendAdded?.Invoke();
                    OnSearchClicked();
                },
                error => SetStatus(error, new Color(1f, 0.62f, 0.62f, 1f)));
        }

        private void HideRequestPopup()
        {
            _pendingAddTarget = null;
            if (_requestPopup != null)
                _requestPopup.gameObject.SetActive(false);
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusText == null)
                return;

            _statusText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            _statusText.color = color;
        }

        private static void ClearChildren(RectTransform root)
        {
            if (root == null)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
        }

        private static void CreateHintRow(Transform parent, string content)
        {
            GameObject row = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            LayoutElement layout = row.GetComponent<LayoutElement>();
            layout.minHeight = 44f;
            layout.preferredHeight = 44f;

            Text text = row.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.72f, 0.76f, 0.82f, 1f);
            text.text = content ?? string.Empty;
        }

        private void CreateSearchResultRow(Transform parent, string name, SocialUserSearchInfo result)
        {
            GameObject row = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            LayoutElement layout = row.GetComponent<LayoutElement>();
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;

            Image background = row.GetComponent<Image>();
            bool isUnavailable = result != null && (result.isFriend || result.hasPendingFriendRequest);
            background.color = isUnavailable
                ? new Color(0.18f, 0.19f, 0.22f, 1f)
                : new Color(0.16f, 0.19f, 0.25f, 1f);

            Text nameText = CreateRowText(row.transform, "Txt_Name", result?.username ?? "玩家", 20, TextAnchor.MiddleLeft);
            RectTransform nameRect = nameText.transform as RectTransform;
            if (nameRect != null)
            {
                nameRect.anchorMin = new Vector2(0f, 0f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.offsetMin = new Vector2(18f, 0f);
                nameRect.offsetMax = new Vector2(-115f, 0f);
            }

            Button addButton = RuntimeOverlayPanelBuilder.CreateButton(
                row.transform,
                "Btn_Add",
                GetAddButtonLabel(result),
                new Vector2(-48f, 0f),
                new Vector2(82f, 28f),
                isUnavailable ? new Color(0.34f, 0.38f, 0.34f, 1f) : new Color(0.5f, 0.78f, 0.54f, 1f),
                Color.white);
            RectTransform buttonRect = addButton.transform as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(1f, 0.5f);
                buttonRect.anchorMax = new Vector2(1f, 0.5f);
                buttonRect.pivot = new Vector2(1f, 0.5f);
                buttonRect.anchoredPosition = new Vector2(-14f, 0f);
            }

            addButton.transition = Selectable.Transition.None;
            addButton.interactable = result != null && !isUnavailable;
            if (result != null && !isUnavailable)
                addButton.onClick.AddListener(() => OnAddFriendClicked(result));
        }

        private static string GetAddButtonLabel(SocialUserSearchInfo result)
        {
            if (result == null)
                return "添加";
            if (result.isFriend)
                return "已添加";
            return result.hasPendingFriendRequest ? "已申请" : "添加";
        }

        private void CreateRequestPopup(Transform parent)
        {
            _requestPopup = RuntimeOverlayPanelBuilder.CreateWindow(
                parent,
                "RequestPopup",
                new Vector2(500f, 320f),
                new Color(0.08f, 0.1f, 0.13f, 0.98f));
            _requestPopup.gameObject.SetActive(false);

            RuntimeOverlayPanelBuilder.CreateText(
                _requestPopup,
                "Txt_RequestTitle",
                "发送好友申请",
                26,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 115f),
                new Vector2(260f, 40f),
                Color.white,
                FontStyle.Bold);

            _requestMessageInput = RuntimeOverlayPanelBuilder.CreateInputField(
                _requestPopup,
                "Input_RequestMessage",
                "申请消息，例如：我是某某某",
                "我是来加你为好友的",
                new Vector2(0f, 45f),
                new Vector2(390f, 52f),
                new Color(0.18f, 0.21f, 0.26f, 1f),
                Color.white);

            _requestRemarkInput = RuntimeOverlayPanelBuilder.CreateInputField(
                _requestPopup,
                "Input_RequestRemark",
                "备注信息，可不填",
                string.Empty,
                new Vector2(0f, -25f),
                new Vector2(390f, 52f),
                new Color(0.18f, 0.21f, 0.26f, 1f),
                Color.white);

            Button cancelButton = RuntimeOverlayPanelBuilder.CreateButton(
                _requestPopup,
                "Btn_CancelRequest",
                "取消",
                new Vector2(-90f, -105f),
                new Vector2(110f, 44f),
                new Color(0.34f, 0.34f, 0.34f, 1f),
                Color.white);
            cancelButton.onClick.AddListener(HideRequestPopup);

            Button sendButton = RuntimeOverlayPanelBuilder.CreateButton(
                _requestPopup,
                "Btn_SendRequest",
                "发送",
                new Vector2(90f, -105f),
                new Vector2(110f, 44f),
                new Color(0.5f, 0.78f, 0.54f, 1f),
                Color.white);
            sendButton.onClick.AddListener(SendPendingFriendRequest);
        }

        private static Text CreateRowText(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = content ?? string.Empty;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateListButton(Transform parent, string name, string label, Color backgroundColor)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 52f;
            layout.preferredHeight = 52f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = backgroundColor;
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-16f, 0f);

            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.text = label ?? string.Empty;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            return button;
        }
    }
}
