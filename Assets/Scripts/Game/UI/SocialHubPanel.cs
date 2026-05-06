using System;
using System.Collections;
using System.Collections.Generic;
using Game.GameFlow;
using Game.Online;
using Game.Social;
using ProjectBase;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public class SocialHubPanel : BasePanel
    {
        private static readonly Dictionary<string, Sprite> PortraitSpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private Text _currentFriendText;
        private Text _statusText;
        private Text _conversationText;
        private RectTransform _friendListContent;
        private ScrollRect _conversationScrollRect;
        private InputField _messageInput;
        private Coroutine _pollingCoroutine;
        private SocialFriendPresenceInfo _selectedFriend;
        private SocialAidSessionInfo _activeAidSession;
        private GameObject _friendRowPrefab;
        private RectTransform _friendContextMenu;
        private SocialFriendPresenceInfo _contextMenuFriend;
        private readonly Dictionary<Transform, SocialFriendPresenceInfo> _visibleFriendRows = new Dictionary<Transform, SocialFriendPresenceInfo>();
        private bool _uiBuilt;

        protected override void Awake()
        {
            base.Awake();
            EnsureBuilt();
        }

        public override void ShowMe()
        {
            EnsureBuilt();
            gameObject.SetActive(true);
            SetStatus(string.Empty, new Color(0.92f, 0.92f, 0.92f, 1f));
            HideFriendContextMenu();
            RefreshFriendList(false);
            RefreshActiveAidSession(false);
            StartPolling();
        }

        public override void HideMe()
        {
            StopPolling();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            StopPolling();
        }

        private void EnsureBuilt()
        {
            if (_uiBuilt)
                return;

            if (transform.Find("Window") != null)
            {
                _currentFriendText = GetControl<Text>("Txt_CurrentFriend");
                _statusText = GetControl<Text>("Txt_Status");
                _messageInput = GetControl<InputField>("Input_Message");
                _friendListContent = transform.Find("Window/LeftPanel/Scroll_Friends/Viewport/Content") as RectTransform;
                _conversationScrollRect = GetControl<ScrollRect>("Scroll_Conversation");
                Transform prefabConversationText = transform.Find("Window/RightPanel/Scroll_Conversation/Viewport/Content/ConversationText");
                _conversationText = prefabConversationText != null ? prefabConversationText.GetComponent<Text>() : null;
                _friendContextMenu = transform.Find("Window/FriendContextMenu") as RectTransform;
                if (_friendContextMenu == null)
                    CreateFriendContextMenu(transform.Find("Window"));

                Button prefabAddFriendButton = GetControl<Button>("Btn_AddFriend");
                if (prefabAddFriendButton != null)
                {
                    prefabAddFriendButton.onClick.RemoveAllListeners();
                    prefabAddFriendButton.gameObject.SetActive(false);
                }

                Button prefabNewFriendsButton = GetControl<Button>("Btn_NewFriends");
                if (prefabNewFriendsButton == null)
                    prefabNewFriendsButton = CreatePrefabHeaderButton("Btn_NewFriends", "新朋友", new Vector2(-215f, -50f));
                if (prefabNewFriendsButton != null)
                {
                    MovePrefabHeaderButton(prefabNewFriendsButton, new Vector2(-215f, -50f));
                    prefabNewFriendsButton.onClick.RemoveAllListeners();
                    prefabNewFriendsButton.onClick.AddListener(OpenFriendRequestsPanel);
                }

                Button prefabCloseButton = GetControl<Button>("Btn_Close");
                if (prefabCloseButton != null)
                {
                    prefabCloseButton.onClick.RemoveAllListeners();
                    prefabCloseButton.onClick.AddListener(() => UIManager.GetInstance()?.HidePanel(PanelNames.SocialHub));
                }

                Button prefabSendButton = GetControl<Button>("Btn_SendMessage");
                if (prefabSendButton != null)
                {
                    prefabSendButton.onClick.RemoveAllListeners();
                    prefabSendButton.onClick.AddListener(OnSendClicked);
                }

                if (_currentFriendText != null
                    && _statusText != null
                    && _messageInput != null
                    && _friendListContent != null
                    && _conversationScrollRect != null
                    && _conversationText != null)
                {
                    _uiBuilt = true;
                    return;
                }
            }

            RectTransform root = transform as RectTransform;
            RuntimeOverlayPanelBuilder.EnsureFullScreenRoot(root);
            RuntimeOverlayPanelBuilder.EnsureOverlayBackground(gameObject, new Color(0f, 0f, 0f, 0.65f));

            RectTransform window = RuntimeOverlayPanelBuilder.CreateWindow(
                transform,
                "Window",
                new Vector2(1240f, 760f),
                new Color(0.1f, 0.12f, 0.16f, 0.98f));

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Title",
                "社交中心",
                34,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 330f),
                new Vector2(280f, 44f),
                Color.white,
                FontStyle.Bold);

            Button newFriendsButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_NewFriends",
                "新朋友",
                new Vector2(430f, 330f),
                new Vector2(150f, 48f),
                new Color(0.24f, 0.45f, 0.72f, 1f),
                Color.white);
            newFriendsButton.onClick.AddListener(OpenFriendRequestsPanel);

            Button closeButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Close",
                "关闭",
                new Vector2(590f, 330f),
                new Vector2(120f, 48f),
                new Color(0.34f, 0.34f, 0.34f, 1f),
                Color.white);
            closeButton.onClick.AddListener(() => UIManager.GetInstance()?.HidePanel(PanelNames.SocialHub));

            RectTransform leftPanel = RuntimeOverlayPanelBuilder.CreateWindow(
                window,
                "LeftPanel",
                new Vector2(360f, 600f),
                new Color(0.14f, 0.17f, 0.22f, 1f));
            leftPanel.anchoredPosition = new Vector2(-400f, -10f);

            RuntimeOverlayPanelBuilder.CreateText(
                leftPanel,
                "Txt_FriendListTitle",
                "好友列表",
                24,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 220f),
                new Vector2(180f, 36f),
                Color.white,
                FontStyle.Bold);

            RuntimeOverlayPanelBuilder.CreateScrollView(
                leftPanel,
                "Scroll_Friends",
                new Vector2(0f, -35f),
                new Vector2(300f, 460f),
                new Color(0.08f, 0.1f, 0.13f, 0.95f),
                out _friendListContent);

            RectTransform rightPanel = RuntimeOverlayPanelBuilder.CreateWindow(
                window,
                "RightPanel",
                new Vector2(720f, 600f),
                new Color(0.14f, 0.17f, 0.22f, 1f));
            rightPanel.anchoredPosition = new Vector2(200f, -10f);

            _currentFriendText = RuntimeOverlayPanelBuilder.CreateText(
                rightPanel,
                "Txt_CurrentFriend",
                "当前会话：请选择一位好友",
                24,
                TextAnchor.MiddleLeft,
                new Vector2(-145f, 261f),
                new Vector2(460f, 36f),
                Color.white,
                FontStyle.Bold);

            _conversationScrollRect = RuntimeOverlayPanelBuilder.CreateScrollView(
                rightPanel,
                "Scroll_Conversation",
                new Vector2(0f, 40f),
                new Vector2(620f, 380f),
                new Color(0.08f, 0.1f, 0.13f, 0.95f),
                out RectTransform conversationContent);
            _conversationText = CreateConversationText(conversationContent);

            _messageInput = RuntimeOverlayPanelBuilder.CreateInputField(
                rightPanel,
                "Input_Message",
                "输入要发送的消息",
                string.Empty,
                new Vector2(-70f, -205f),
                new Vector2(420f, 56f),
                new Color(0.18f, 0.21f, 0.26f, 1f),
                Color.white);

            Button sendButton = RuntimeOverlayPanelBuilder.CreateButton(
                rightPanel,
                "Btn_SendMessage",
                "发送",
                new Vector2(230f, -205f),
                new Vector2(110f, 56f),
                new Color(0.26f, 0.55f, 0.34f, 1f),
                Color.white);
            sendButton.onClick.AddListener(OnSendClicked);

            _statusText = RuntimeOverlayPanelBuilder.CreateText(
                rightPanel,
                "Txt_Status",
                string.Empty,
                20,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -270f),
                new Vector2(600f, 54f),
                new Color(0.92f, 0.92f, 0.92f, 1f));

            CreateFriendContextMenu(window);
            _uiBuilt = true;
        }

        private void OpenAddFriendPanel()
        {
            UIManager.GetInstance()?.ShowPanel<SocialAddFriendPanel>(
                PanelNames.SocialAddFriend,
                PanelLayers.SocialAddFriend,
                panel => panel.Init(() =>
                {
                    RefreshFriendList(false);
                    SetStatus("好友列表已刷新", new Color(0.6f, 0.92f, 0.66f, 1f));
                }));
        }

        private void OpenFriendRequestsPanel()
        {
            UIManager.GetInstance()?.ShowPanel<SocialFriendRequestsPanel>(
                PanelNames.SocialFriendRequests,
                PanelLayers.SocialFriendRequests,
                panel => panel.Init(() =>
                {
                    RefreshFriendList(false);
                    SetStatus("好友列表已刷新", new Color(0.6f, 0.92f, 0.66f, 1f));
                }));
        }

        private void OnSendClicked()
        {
            if (!EnsureLoggedIn())
                return;

            if (_selectedFriend == null || string.IsNullOrWhiteSpace(_selectedFriend.userId))
            {
                SetStatus("请先选择一位好友", new Color(1f, 0.72f, 0.4f, 1f));
                return;
            }

            string content = _messageInput != null ? _messageInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                SetStatus("消息内容不能为空", new Color(1f, 0.72f, 0.4f, 1f));
                return;
            }

            SetStatus("正在发送消息...", new Color(0.92f, 0.92f, 0.92f, 1f));
            SocialService.GetInstance().SendMessage(
                _selectedFriend.userId,
                content,
                (_, message) =>
                {
                    if (_messageInput != null)
                        _messageInput.text = string.Empty;
                    RefreshConversation(false);
                    SetStatus(message, new Color(0.6f, 0.92f, 0.66f, 1f));
                },
                error => SetStatus(error, new Color(1f, 0.62f, 0.62f, 1f)));
        }

        private void OnRequestAidClicked()
        {
            if (!EnsureLoggedIn())
                return;

            if (_selectedFriend == null || string.IsNullOrWhiteSpace(_selectedFriend.userId))
            {
                SetStatus("请先选择要请求援助的好友", new Color(1f, 0.72f, 0.4f, 1f));
                return;
            }

            SetStatus($"正在向 {_selectedFriend.username} 发起援助请求...", new Color(0.92f, 0.92f, 0.92f, 1f));
            SocialService.GetInstance().CreateAidRequest(
                _selectedFriend.userId,
                string.Empty,
                (_, message) =>
                {
                    SetStatus(message, new Color(0.6f, 0.92f, 0.66f, 1f));
                    RefreshActiveAidSession(false);
                },
                error => SetStatus(error, new Color(1f, 0.62f, 0.62f, 1f)));
        }

        private void OnRemoveFriendClicked()
        {
            if (!EnsureLoggedIn())
                return;

            if (_contextMenuFriend == null || string.IsNullOrWhiteSpace(_contextMenuFriend.userId))
            {
                HideFriendContextMenu();
                return;
            }

            SocialFriendPresenceInfo friend = _contextMenuFriend;
            HideFriendContextMenu();
            SetStatus($"正在删除好友：{friend.playerName}", new Color(0.92f, 0.92f, 0.92f, 1f));
            SocialService.GetInstance().RemoveFriend(
                friend.userId,
                (_, message) =>
                {
                    if (_selectedFriend != null && string.Equals(_selectedFriend.userId, friend.userId, StringComparison.Ordinal))
                    {
                        _selectedFriend = null;
                        UpdateCurrentFriendLabel();
                        SetConversationText("请选择一位好友开始聊天");
                    }
                    RefreshFriendList(false);
                    SetStatus(message, new Color(0.6f, 0.92f, 0.66f, 1f));
                },
                error => SetStatus(error, new Color(1f, 0.62f, 0.62f, 1f)));
        }

        private void RefreshFriendList(bool showStatus)
        {
            if (!EnsureLoggedIn())
                return;

            SocialService.GetInstance().GetFriendPresenceSummaries(
                (friends, message) =>
                {
                    RebuildFriendButtons(friends);
                    if (showStatus)
                        SetStatus(message, new Color(0.6f, 0.92f, 0.66f, 1f));
                },
                error => SetStatus(error, new Color(1f, 0.62f, 0.62f, 1f)));
        }

        private void RefreshActiveAidSession(bool showStatus)
        {
            if (!EnsureLoggedIn())
                return;

            SocialService.GetInstance().GetActiveAidSession(
                (session, message) =>
                {
                    _activeAidSession = session;
                    OnlineDungeonSessionCoordinator coordinator = OnlineDungeonSessionCoordinator.GetInstance();
                    if (session == null || string.IsNullOrWhiteSpace(session.sessionId))
                    {
                        if (coordinator.IsAidJoinerRole)
                            coordinator.ReturnAidJoinerToOwnLevel(false);
                        else
                            coordinator.StopSession();
                    }
                    else if (!coordinator.HasActiveSession)
                    {
                        if (!coordinator.TryEnterSession(session, out string sessionError))
                            Debug.LogWarning($"[SocialHubPanel] 进入联机副本失败：{sessionError}");
                    }
                    if (showStatus)
                        SetStatus(message, new Color(0.6f, 0.92f, 0.66f, 1f));
                },
                error =>
                {
                    _activeAidSession = null;
                    if (showStatus)
                        SetStatus(error, new Color(1f, 0.72f, 0.4f, 1f));
                });
        }

        private void RefreshConversation(bool showStatus)
        {
            if (!EnsureLoggedIn())
                return;

            if (_selectedFriend == null || string.IsNullOrWhiteSpace(_selectedFriend.userId))
            {
                SetConversationText("请选择一位好友开始聊天");
                if (showStatus)
                    SetStatus("请先选择好友", new Color(1f, 0.72f, 0.4f, 1f));
                return;
            }

            SocialService.GetInstance().GetConversation(
                _selectedFriend.userId,
                (messages, message) =>
                {
                    SetConversationText(BuildConversationText(messages));
                    if (showStatus)
                        SetStatus(message, new Color(0.6f, 0.92f, 0.66f, 1f));
                },
                error => SetStatus(error, new Color(1f, 0.62f, 0.62f, 1f)));
        }

        private void RebuildFriendButtons(List<SocialFriendPresenceInfo> friends)
        {
            ClearChildren(_friendListContent);
            _visibleFriendRows.Clear();
            if (_friendListContent == null)
                return;

            _friendRowPrefab ??= Resources.Load<GameObject>("UI/SocialFriendRow");

            if (friends == null || friends.Count == 0)
            {
                _selectedFriend = null;
                UpdateCurrentFriendLabel();
                SetConversationText("当前还没有好友，请点击顶部的添加好友按钮");
                CreateHintRow(_friendListContent, "当前还没有好友");
                return;
            }

            bool selectedStillExists = false;
            for (int i = 0; i < friends.Count; i++)
            {
                SocialFriendPresenceInfo friend = friends[i];
                if (_selectedFriend != null && string.Equals(_selectedFriend.userId, friend.userId, StringComparison.Ordinal))
                {
                    selectedStillExists = true;
                    _selectedFriend = friend;
                }
            }

            if (!selectedStillExists)
                _selectedFriend = friends[0];

            VerticalLayoutGroup listLayout = _friendListContent.GetComponent<VerticalLayoutGroup>();
            if (listLayout != null)
            {
                listLayout.spacing = 12f;
                listLayout.padding = new RectOffset(10, 10, 10, 10);
            }

            for (int i = 0; i < friends.Count; i++)
            {
                SocialFriendPresenceInfo friend = friends[i];
                GameObject row = _friendRowPrefab != null
                    ? UnityEngine.Object.Instantiate(_friendRowPrefab, _friendListContent, false)
                    : new GameObject("FriendRow_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                row.name = "FriendRow_" + i;
                _visibleFriendRows[row.transform] = friend;

                Button selectButton = row.GetComponent<Button>();
                if (selectButton != null)
                {
                    selectButton.transition = Selectable.Transition.None;
                    selectButton.onClick.RemoveAllListeners();
                    selectButton.onClick.AddListener(() => SelectFriend(friend, true));
                }
                BindFriendRowClick(row, friend);

                BindFriendRow(row.transform, friend);
            }

            UpdateCurrentFriendLabel();
            RefreshConversation(false);
        }

        private bool IsCurrentUserHelpingSomeone()
        {
            if (_activeAidSession == null)
                return false;

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            return currentUser != null
                   && string.Equals(currentUser.userId, _activeAidSession.helperUserId, StringComparison.Ordinal)
                   && !string.IsNullOrWhiteSpace(_activeAidSession.sessionId);
        }

        private void UpdateCurrentFriendLabel()
        {
            if (_currentFriendText == null)
                return;

            _currentFriendText.text = _selectedFriend == null || string.IsNullOrWhiteSpace(_selectedFriend.playerName)
                ? "当前会话：请选择一位好友"
                : $"当前会话：{_selectedFriend.playerName}";
        }

        private void SetConversationText(string content)
        {
            if (_conversationText == null)
                return;

            _conversationText.text = string.IsNullOrWhiteSpace(content) ? "暂无消息" : content;
            Canvas.ForceUpdateCanvases();
            if (_conversationScrollRect != null)
                _conversationScrollRect.verticalNormalizedPosition = 0f;
        }

        private static string BuildConversationText(List<SocialMessageInfo> messages)
        {
            if (messages == null || messages.Count == 0)
                return "暂无消息";

            SocialUserInfo currentUser = SocialSession.GetInstance().CurrentUser;
            string currentUserId = currentUser != null ? currentUser.userId : string.Empty;
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < messages.Count; i++)
            {
                SocialMessageInfo message = messages[i];
                if (message == null)
                    continue;

                string sender = string.Equals(message.fromUserId, currentUserId, StringComparison.Ordinal) ? "我" : "好友";
                string timeLabel = message.sentAtUtc;
                if (DateTime.TryParse(message.sentAtUtc, out DateTime sentAt))
                    timeLabel = sentAt.ToLocalTime().ToString("MM-dd HH:mm");

                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();

                builder.Append('[')
                    .Append(timeLabel)
                    .Append("] ")
                    .Append(sender)
                    .Append("：")
                    .Append(message.content ?? string.Empty);
            }

            return builder.Length > 0 ? builder.ToString() : "暂无消息";
        }

        private void StartPolling()
        {
            StopPolling();
            _pollingCoroutine = MonoMgr.GetInstance().StartCoroutine(PollLoop());
        }

        private void StopPolling()
        {
            if (_pollingCoroutine == null)
                return;

            MonoMgr.GetInstance().StopCoroutine(_pollingCoroutine);
            _pollingCoroutine = null;
        }

        private IEnumerator PollLoop()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(2f);
            while (true)
            {
                RefreshFriendList(false);
                RefreshActiveAidSession(false);
                yield return wait;
            }
        }

        private bool EnsureLoggedIn()
        {
            if (SocialSession.GetInstance().IsLoggedIn)
                return true;

            SetStatus("请先登录账号", new Color(1f, 0.72f, 0.4f, 1f));
            return false;
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

        private static Button CreateListButton(Transform parent, string name, string label, Color backgroundColor)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 48f;
            layout.preferredHeight = 48f;

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
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.text = label ?? string.Empty;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            return button;
        }

        private void BindFriendRow(Transform rowRoot, SocialFriendPresenceInfo friend)
        {
            NormalizeFriendRowLayout(rowRoot);
            ApplyFriendRowBackground(rowRoot, friend);

            Image portrait = rowRoot.Find("PortraitMask/Portrait")?.GetComponent<Image>();
            if (portrait != null)
            {
                portrait.sprite = LoadPortraitSprite(friend?.portraitId);
                portrait.preserveAspect = true;
                portrait.color = Color.white;
            }

            Text nameText = rowRoot.Find("InfoRoot/Txt_PlayerName")?.GetComponent<Text>();
            if (nameText != null)
                nameText.text = BuildFriendDisplayName(friend);

            Text stateText = rowRoot.Find("InfoRoot/Txt_State")?.GetComponent<Text>();
            if (stateText != null)
            {
                stateText.color = friend != null && friend.isOnline
                    ? new Color(0.62f, 0.9f, 0.66f, 1f)
                    : new Color(1f, 0.46f, 0.46f, 1f);
                stateText.text = friend != null && friend.isOnline ? $"在线中  关卡{Mathf.Max(1, friend.levelIndex)}" : "离线中";
            }

            Button aidButton = rowRoot.Find("InfoRoot/Btn_RequestAid")?.GetComponent<Button>();
            if (aidButton != null)
            {
                aidButton.transition = Selectable.Transition.None;

                bool selfIsHelping = IsCurrentUserHelpingSomeone();
                bool friendIsHelping = friend != null && friend.isHelping;
                bool friendIsOnline = friend != null && friend.isOnline;
                bool canRequest = friend != null && friendIsOnline && !selfIsHelping && !friendIsHelping;

                Text aidText = aidButton.GetComponentInChildren<Text>();
                if (aidText != null)
                    aidText.text = friendIsHelping ? "援助中" : "请求援助";

                Image aidImage = aidButton.GetComponent<Image>();
                if (aidImage != null)
                    aidImage.color = canRequest ? new Color(0.88f, 0.12f, 0.12f, 1f) : new Color(0.46f, 0.12f, 0.12f, 1f);

                aidButton.interactable = canRequest;
                aidButton.onClick.RemoveAllListeners();
                aidButton.onClick.AddListener(() =>
                {
                    HideFriendContextMenu();
                    SelectFriend(friend, false);
                    OnRequestAidClicked();
                });
            }
        }

        private Button CreatePrefabHeaderButton(string name, string label, Vector2 anchoredPosition)
        {
            Transform window = transform.Find("Window");
            if (window == null)
                return null;

            Button button = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                name,
                label,
                anchoredPosition,
                new Vector2(150f, 48f),
                new Color(0.24f, 0.45f, 0.72f, 1f),
                Color.white);
            button.transition = Selectable.Transition.None;
            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
            }
            return button;
        }

        private static void MovePrefabHeaderButton(Button button, Vector2 anchoredPosition)
        {
            RectTransform rect = button != null ? button.transform as RectTransform : null;
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
        }

        private void CreateFriendContextMenu(Transform parent)
        {
            if (parent == null || _friendContextMenu != null)
                return;

            _friendContextMenu = RuntimeOverlayPanelBuilder.CreateWindow(
                parent,
                "FriendContextMenu",
                new Vector2(120f, 42f),
                new Color(0.08f, 0.1f, 0.13f, 0.98f));

            Button removeButton = RuntimeOverlayPanelBuilder.CreateButton(
                _friendContextMenu,
                "Btn_RemoveFriend",
                "删除好友",
                Vector2.zero,
                new Vector2(108f, 32f),
                new Color(0.78f, 0.16f, 0.16f, 1f),
                Color.white);
            removeButton.transition = Selectable.Transition.None;
            Text removeText = removeButton.GetComponentInChildren<Text>();
            if (removeText != null)
                removeText.fontSize = 16;
            removeButton.onClick.AddListener(OnRemoveFriendClicked);

            HideFriendContextMenu();
        }

        private void ShowFriendContextMenu(SocialFriendPresenceInfo friend, Vector2 screenPosition)
        {
            _contextMenuFriend = friend;
            if (_friendContextMenu == null)
                CreateFriendContextMenu(transform.Find("Window"));
            if (_friendContextMenu == null)
                return;

            RectTransform window = transform.Find("Window") as RectTransform;
            if (window != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    window,
                    screenPosition,
                    null,
                    out Vector2 localPoint))
            {
                _friendContextMenu.anchoredPosition = localPoint + new Vector2(56f, -20f);
            }

            _friendContextMenu.gameObject.SetActive(true);
            _friendContextMenu.SetAsLastSibling();
        }

        private void HideFriendContextMenu()
        {
            _contextMenuFriend = null;
            if (_friendContextMenu != null)
                _friendContextMenu.gameObject.SetActive(false);
        }

        private void SelectFriend(SocialFriendPresenceInfo friend, bool refreshConversation)
        {
            if (friend == null)
                return;

            _selectedFriend = friend;
            UpdateCurrentFriendLabel();
            RefreshVisibleFriendRows();
            if (refreshConversation)
                RefreshConversation(false);
        }

        private void BindFriendRowClick(GameObject row, SocialFriendPresenceInfo friend)
        {
            if (row == null)
                return;

            EventTrigger trigger = row.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = row.AddComponent<EventTrigger>();

            trigger.triggers.Clear();
            EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener(data =>
            {
                PointerEventData pointer = data as PointerEventData;
                if (pointer != null && pointer.button == PointerEventData.InputButton.Right)
                {
                    SelectFriend(friend, false);
                    ShowFriendContextMenu(friend, pointer.position);
                }
                else
                {
                    HideFriendContextMenu();
                    SelectFriend(friend, true);
                }
            });
            trigger.triggers.Add(clickEntry);
        }

        private void ApplyFriendRowBackground(Transform rowRoot, SocialFriendPresenceInfo friend)
        {
            Image rowBg = EnsureFriendRowBackground(rowRoot);
            if (rowBg == null)
                return;

            bool isSelected = _selectedFriend != null
                              && friend != null
                              && string.Equals(_selectedFriend.userId, friend.userId, StringComparison.Ordinal);
            bool isOnline = friend != null && friend.isOnline;
            bool isHelping = friend != null && friend.isHelping;

            if (isSelected)
                rowBg.color = new Color(0.22f, 0.42f, 0.62f, 1f);
            else if (isHelping)
                rowBg.color = new Color(0.25f, 0.24f, 0.2f, 1f);
            else if (!isOnline)
                rowBg.color = new Color(0.24f, 0.16f, 0.18f, 1f);
            else
                rowBg.color = new Color(0.16f, 0.19f, 0.26f, 1f);

            rowBg.raycastTarget = false;
        }

        private void RefreshVisibleFriendRows()
        {
            if (_friendListContent == null)
                return;

            for (int i = 0; i < _friendListContent.childCount; i++)
            {
                Transform row = _friendListContent.GetChild(i);
                if (_visibleFriendRows.TryGetValue(row, out SocialFriendPresenceInfo friend))
                    ApplyFriendRowBackground(row, friend);
            }
        }

        private static Image EnsureFriendRowBackground(Transform rowRoot)
        {
            if (rowRoot == null)
                return null;

            Transform existing = rowRoot.Find("RowBackground");
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                GameObject background = new GameObject("RowBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                background.transform.SetParent(rowRoot, false);
                image = background.GetComponent<Image>();
            }

            RectTransform rect = image.transform as RectTransform;
            if (rect != null)
            {
                rect.SetAsFirstSibling();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            image.raycastTarget = false;
            return image;
        }

        private static void NormalizeFriendRowLayout(Transform rowRoot)
        {
            if (rowRoot == null)
                return;

            Image rowImage = rowRoot.GetComponent<Image>();
            if (rowImage != null)
                rowImage.raycastTarget = true;

            RectTransform rowRect = rowRoot as RectTransform;
            if (rowRect != null)
            {
                rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, 58f);
                rowRect.anchorMin = new Vector2(0f, rowRect.anchorMin.y);
                rowRect.anchorMax = new Vector2(1f, rowRect.anchorMax.y);
                rowRect.offsetMin = new Vector2(0f, rowRect.offsetMin.y);
                rowRect.offsetMax = new Vector2(0f, rowRect.offsetMax.y);
                LayoutElement rowLayout = rowRoot.GetComponent<LayoutElement>();
                if (rowLayout != null)
                {
                    rowLayout.minHeight = 58f;
                    rowLayout.preferredHeight = 58f;
                    rowLayout.flexibleHeight = 0f;
                }
            }

            LayoutGroup rowGroup = rowRoot.GetComponent<LayoutGroup>();
            if (rowGroup != null)
                rowGroup.enabled = false;

            RectTransform portraitMask = rowRoot.Find("PortraitMask") as RectTransform;
            if (portraitMask != null)
            {
                portraitMask.anchorMin = new Vector2(0f, 0.5f);
                portraitMask.anchorMax = new Vector2(0f, 0.5f);
                portraitMask.pivot = new Vector2(0f, 0.5f);
                portraitMask.anchoredPosition = new Vector2(12f, 0f);
                portraitMask.sizeDelta = new Vector2(44f, 44f);
            }

            RectTransform infoRoot = rowRoot.Find("InfoRoot") as RectTransform;
            if (infoRoot == null)
                return;

            LayoutGroup infoGroup = infoRoot.GetComponent<LayoutGroup>();
            if (infoGroup != null)
                infoGroup.enabled = false;

            infoRoot.anchorMin = Vector2.zero;
            infoRoot.anchorMax = Vector2.one;
            infoRoot.pivot = new Vector2(0.5f, 0.5f);
            infoRoot.offsetMin = new Vector2(64f, 0f);
            infoRoot.offsetMax = new Vector2(-8f, 0f);

            ConfigureRowText(infoRoot.Find("Txt_PlayerName") as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -18f), new Vector2(100f, 24f), new Vector2(0f, 0.5f), TextAnchor.MiddleLeft, 20);
            ConfigureRowText(infoRoot.Find("Txt_State") as RectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 15f), new Vector2(118f, 18f), new Vector2(0f, 0.5f), TextAnchor.MiddleLeft, 12);

            RectTransform aidButton = infoRoot.Find("Btn_RequestAid") as RectTransform;
            if (aidButton != null)
            {
                aidButton.anchorMin = new Vector2(1f, 0.5f);
                aidButton.anchorMax = new Vector2(1f, 0.5f);
                aidButton.pivot = new Vector2(1f, 0.5f);
                aidButton.anchoredPosition = Vector2.zero;
                aidButton.sizeDelta = new Vector2(74f, 28f);

                Text aidText = aidButton.GetComponentInChildren<Text>();
                if (aidText != null)
                {
                    aidText.fontSize = 13;
                    aidText.alignment = TextAnchor.MiddleCenter;
                    aidText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    aidText.verticalOverflow = VerticalWrapMode.Truncate;
                    RectTransform aidTextRect = aidText.transform as RectTransform;
                    if (aidTextRect != null)
                    {
                        aidTextRect.anchorMin = Vector2.zero;
                        aidTextRect.anchorMax = Vector2.one;
                        aidTextRect.offsetMin = Vector2.zero;
                        aidTextRect.offsetMax = Vector2.zero;
                    }
                }
            }
        }

        private static void ConfigureRowText(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot, TextAnchor alignment, int fontSize)
        {
            if (rect == null)
                return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = rect.GetComponent<Text>();
            if (text == null)
                return;

            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static string BuildFriendDisplayName(SocialFriendPresenceInfo friend)
        {
            string playerName = string.IsNullOrWhiteSpace(friend?.playerName) ? "玩家" : friend.playerName.Trim();
            string remark = string.IsNullOrWhiteSpace(friend?.remark) ? string.Empty : friend.remark.Trim();
            return string.IsNullOrWhiteSpace(remark) ? playerName : $"{playerName}（{remark}）";
        }

        private static Sprite LoadPortraitSprite(string portraitId)
        {
            return PlayerPortraitUtility.LoadPortraitSprite(portraitId);
        }

        private static Sprite LoadPortraitMaskSprite()
        {
            const string maskPath = "SocialPortraits/圆形遮罩";
            if (PortraitSpriteCache.TryGetValue(maskPath, out Sprite cached) && cached != null)
                return cached;

            Sprite sprite = Resources.Load<Sprite>(maskPath);
            if (sprite != null)
            {
                PortraitSpriteCache[maskPath] = sprite;
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(maskPath);
            if (texture == null)
                return null;

            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            PortraitSpriteCache[maskPath] = sprite;
            return sprite;
        }

        private static Text CreateConversationText(Transform parent)
        {
            GameObject textObject = new GameObject("ConversationText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(ContentSizeFitter), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            LayoutElement layout = textObject.GetComponent<LayoutElement>();
            layout.minHeight = 320f;

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 0f);

            ContentSizeFitter fitter = textObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.text = "请选择一位好友开始聊天";
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
