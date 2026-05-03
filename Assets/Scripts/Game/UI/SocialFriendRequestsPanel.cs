using System;
using System.Collections.Generic;
using Game.Social;
using ProjectBase;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class SocialFriendRequestsPanel : BasePanel
    {
        private static readonly Dictionary<string, Sprite> PortraitSpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private Text _statusText;
        private RectTransform _requestListContent;
        private Action _onFriendListChanged;
        private bool _uiBuilt;

        protected override void Awake()
        {
            base.Awake();
            EnsureBuilt();
        }

        public void Init(Action onFriendListChanged)
        {
            _onFriendListChanged = onFriendListChanged;
            RefreshRequests();
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
                _statusText = GetControl<Text>("Txt_Status");
                _requestListContent = transform.Find("Window/Scroll_Requests/Viewport/Content") as RectTransform;

                Button prefabAddFriendButton = GetControl<Button>("Btn_AddFriend");
                if (prefabAddFriendButton == null)
                    prefabAddFriendButton = CreateHeaderButton(transform.Find("Window"), "Btn_AddFriend", "添加好友", new Vector2(-280f, 250f), new Color(0.24f, 0.45f, 0.72f, 1f));
                if (prefabAddFriendButton != null)
                {
                    prefabAddFriendButton.onClick.RemoveAllListeners();
                    prefabAddFriendButton.onClick.AddListener(OpenAddFriendPanel);
                }

                Button prefabCloseButton = GetControl<Button>("Btn_Close");
                if (prefabCloseButton != null)
                {
                    prefabCloseButton.onClick.RemoveAllListeners();
                    prefabCloseButton.onClick.AddListener(() => UIManager.GetInstance()?.HidePanel(PanelNames.SocialFriendRequests));
                }

                if (_statusText != null && _requestListContent != null)
                {
                    _uiBuilt = true;
                    return;
                }
            }

            RectTransform root = transform as RectTransform;
            RuntimeOverlayPanelBuilder.EnsureFullScreenRoot(root);
            RuntimeOverlayPanelBuilder.EnsureOverlayBackground(gameObject, new Color(0f, 0f, 0f, 0.55f));

            RectTransform window = transform.Find("Window") as RectTransform;
            if (window == null)
            {
                window = RuntimeOverlayPanelBuilder.CreateWindow(
                    transform,
                    "Window",
                    new Vector2(760f, 620f),
                    new Color(0.11f, 0.13f, 0.17f, 0.98f));
            }

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Title",
                "新朋友",
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
            closeButton.onClick.AddListener(() => UIManager.GetInstance()?.HidePanel(PanelNames.SocialFriendRequests));

            Button addFriendButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_AddFriend",
                "添加好友",
                new Vector2(-280f, 250f),
                new Vector2(120f, 48f),
                new Color(0.24f, 0.45f, 0.72f, 1f),
                Color.white);
            addFriendButton.onClick.AddListener(OpenAddFriendPanel);

            RuntimeOverlayPanelBuilder.CreateScrollView(
                window,
                "Scroll_Requests",
                new Vector2(0f, -10f),
                new Vector2(620f, 380f),
                new Color(0.08f, 0.1f, 0.13f, 0.95f),
                out _requestListContent);

            _statusText = RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Status",
                string.Empty,
                20,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -245f),
                new Vector2(620f, 54f),
                new Color(0.92f, 0.92f, 0.92f, 1f));

            _uiBuilt = true;
        }

        private void RefreshRequests()
        {
            if (!SocialSession.GetInstance().IsLoggedIn)
            {
                SetStatus("请先登录账号", new Color(1f, 0.72f, 0.4f, 1f));
                return;
            }

            SetStatus("正在获取好友申请...", new Color(0.92f, 0.92f, 0.92f, 1f));
            SocialService.GetInstance().GetPendingFriendRequests(
                (requests, message) =>
                {
                    RebuildRequests(requests);
                    SetStatus(message, new Color(0.6f, 0.92f, 0.66f, 1f));
                },
                error => SetStatus(error, new Color(1f, 0.62f, 0.62f, 1f)));
        }

        private void OpenAddFriendPanel()
        {
            UIManager.GetInstance()?.ShowPanel<SocialAddFriendPanel>(
                PanelNames.SocialAddFriend,
                PanelLayers.SocialAddFriend,
                panel => panel.Init(RefreshRequests));
        }

        private void RebuildRequests(List<SocialFriendRequestInfo> requests)
        {
            ClearChildren(_requestListContent);
            if (_requestListContent == null)
                return;

            if (requests == null || requests.Count == 0)
            {
                CreateHintRow(_requestListContent, "暂无新的好友申请");
                return;
            }

            VerticalLayoutGroup listLayout = _requestListContent.GetComponent<VerticalLayoutGroup>();
            if (listLayout != null)
            {
                listLayout.spacing = 12f;
                listLayout.padding = new RectOffset(10, 10, 10, 10);
            }

            for (int i = 0; i < requests.Count; i++)
                CreateRequestRow(_requestListContent, "FriendRequest_" + i, requests[i]);
        }

        private void CreateRequestRow(Transform parent, string name, SocialFriendRequestInfo request)
        {
            GameObject row = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            LayoutElement layout = row.GetComponent<LayoutElement>();
            layout.minHeight = 64f;
            layout.preferredHeight = 64f;

            Image background = row.GetComponent<Image>();
            background.color = new Color(0.16f, 0.19f, 0.26f, 1f);

            RectTransform portraitRect = CreatePortrait(row.transform, request?.requesterPortraitId);
            if (portraitRect != null)
            {
                portraitRect.anchorMin = new Vector2(0f, 0.5f);
                portraitRect.anchorMax = new Vector2(0f, 0.5f);
                portraitRect.pivot = new Vector2(0f, 0.5f);
                portraitRect.anchoredPosition = new Vector2(12f, 0f);
                portraitRect.sizeDelta = new Vector2(44f, 44f);
            }

            Text nameText = CreateRowText(row.transform, "Txt_PlayerName", string.IsNullOrWhiteSpace(request?.requesterPlayerName) ? "玩家" : request.requesterPlayerName, 20, TextAnchor.MiddleLeft);
            RectTransform nameRect = nameText.transform as RectTransform;
            if (nameRect != null)
            {
                nameRect.anchorMin = new Vector2(0f, 1f);
                nameRect.anchorMax = new Vector2(0f, 1f);
                nameRect.pivot = new Vector2(0f, 0.5f);
                nameRect.anchoredPosition = new Vector2(66f, -20f);
                nameRect.sizeDelta = new Vector2(300f, 24f);
            }

            Text messageText = CreateRowText(row.transform, "Txt_Message", string.IsNullOrWhiteSpace(request?.message) ? "请求添加你为好友" : request.message, 13, TextAnchor.MiddleLeft);
            messageText.color = new Color(0.62f, 0.9f, 0.66f, 1f);
            RectTransform messageRect = messageText.transform as RectTransform;
            if (messageRect != null)
            {
                messageRect.anchorMin = new Vector2(0f, 0f);
                messageRect.anchorMax = new Vector2(0f, 0f);
                messageRect.pivot = new Vector2(0f, 0.5f);
                messageRect.anchoredPosition = new Vector2(66f, 20f);
                messageRect.sizeDelta = new Vector2(300f, 20f);
            }

            Button rejectButton = CreateSmallButton(row.transform, "Btn_Reject", "拒绝", new Vector2(-12f, 0f), new Color(0.84f, 0.18f, 0.18f, 1f));
            rejectButton.onClick.AddListener(() => RespondRequest(request, false, string.Empty));

            Button acceptButton = CreateSmallButton(row.transform, "Btn_Accept", "接受", new Vector2(-88f, 0f), new Color(0.32f, 0.68f, 0.38f, 1f));
            acceptButton.onClick.AddListener(() => RespondRequest(request, true, string.Empty));
        }

        private void RespondRequest(SocialFriendRequestInfo request, bool accept, string remark)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.requestId))
                return;

            SetStatus(accept ? "正在接受好友申请..." : "正在拒绝好友申请...", new Color(0.92f, 0.92f, 0.92f, 1f));
            SocialService.GetInstance().RespondFriendRequest(
                request.requestId,
                accept,
                remark,
                (_, message) =>
                {
                    _onFriendListChanged?.Invoke();
                    RefreshRequests();
                    SetStatus(message, new Color(0.6f, 0.92f, 0.66f, 1f));
                },
                error => SetStatus(error, new Color(1f, 0.62f, 0.62f, 1f)));
        }

        private static Button CreateHeaderButton(Transform parent, string name, string label, Vector2 anchoredPosition, Color color)
        {
            if (parent == null)
                return null;

            Button button = RuntimeOverlayPanelBuilder.CreateButton(
                parent,
                name,
                label,
                anchoredPosition,
                new Vector2(120f, 48f),
                color,
                Color.white);
            button.transition = Selectable.Transition.None;
            return button;
        }

        private static RectTransform CreatePortrait(Transform parent, string portraitId)
        {
            GameObject maskObject = new GameObject("PortraitMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            maskObject.transform.SetParent(parent, false);

            Image maskImage = maskObject.GetComponent<Image>();
            maskImage.sprite = LoadPortraitMaskSprite();
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;

            Mask mask = maskObject.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portraitObject.transform.SetParent(maskObject.transform, false);

            RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;

            Image portrait = portraitObject.GetComponent<Image>();
            portrait.sprite = LoadPortraitSprite(portraitId);
            portrait.preserveAspect = true;
            portrait.color = Color.white;
            portrait.raycastTarget = false;

            return maskObject.GetComponent<RectTransform>();
        }

        private static Button CreateSmallButton(Transform parent, string name, string label, Vector2 anchoredPosition, Color color)
        {
            Button button = RuntimeOverlayPanelBuilder.CreateButton(
                parent,
                name,
                label,
                anchoredPosition,
                new Vector2(64f, 28f),
                color,
                Color.white);
            button.transition = Selectable.Transition.None;

            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
            }

            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
                text.fontSize = 13;

            return button;
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
    }
}
