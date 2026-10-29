using Game.Social;
using ProjectBase;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class AccountLoginPanel : BasePanel
    {
        private InputField _usernameInput;
        private InputField _passwordInput;
        private Text _statusText;
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
            if (_passwordInput != null)
                _passwordInput.text = string.Empty;
            SetStatus("可使用账号登录，也可以游客身份离线游玩", new Color(0.92f, 0.92f, 0.92f, 1f));
        }

        public override void HideMe()
        {
            gameObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_uiBuilt)
                return;

            Transform prefabWindow = transform.Find("Window");
            if (prefabWindow != null)
            {
                _usernameInput = GetControl<InputField>("Input_Username");
                _passwordInput = GetControl<InputField>("Input_Password");
                _statusText = GetControl<Text>("Txt_Status");

                Button prefabRegisterButton = GetControl<Button>("Btn_Register");
                if (prefabRegisterButton != null)
                {
                    RectTransform registerRect = prefabRegisterButton.transform as RectTransform;
                    if (registerRect != null)
                        registerRect.anchoredPosition = new Vector2(-235f, registerRect.anchoredPosition.y);
                    prefabRegisterButton.onClick.RemoveAllListeners();
                    prefabRegisterButton.onClick.AddListener(OnRegisterClicked);
                }

                Button prefabLoginButton = GetControl<Button>("Btn_Login");
                if (prefabLoginButton != null)
                {
                    RectTransform loginRect = prefabLoginButton.transform as RectTransform;
                    if (loginRect != null)
                        loginRect.anchoredPosition = new Vector2(5f, loginRect.anchoredPosition.y);
                    prefabLoginButton.onClick.RemoveAllListeners();
                    prefabLoginButton.onClick.AddListener(OnLoginClicked);
                }

                Button prefabGuestButton = RuntimeOverlayPanelBuilder.CreateButton(
                    prefabWindow,
                    "Btn_Guest",
                    "游客登录",
                    new Vector2(215f, -178f),
                    new Vector2(180f, 64f),
                    new Color(0.38f, 0.4f, 0.45f, 1f),
                    Color.white);
                prefabGuestButton.onClick.AddListener(OnGuestClicked);

                if (_usernameInput != null && _passwordInput != null && _statusText != null)
                {
                    _uiBuilt = true;
                    return;
                }
            }

            RectTransform root = transform as RectTransform;
            RuntimeOverlayPanelBuilder.EnsureFullScreenRoot(root);
            RuntimeOverlayPanelBuilder.EnsureOverlayBackground(gameObject, new Color(0f, 0f, 0f, 0.72f));

            RectTransform window = RuntimeOverlayPanelBuilder.CreateWindow(
                transform,
                "Window",
                new Vector2(860f, 500f),
                new Color(0.11f, 0.14f, 0.18f, 0.96f));

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Title",
                "账号登录 / 注册",
                34,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 240f),
                new Vector2(520f, 48f),
                Color.white,
                FontStyle.Bold);

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "UsernameLabel",
                "账号名",
                24,
                TextAnchor.MiddleLeft,
                new Vector2(-270f, 110f),
                new Vector2(140f, 36f),
                Color.white);

            _usernameInput = RuntimeOverlayPanelBuilder.CreateInputField(
                window,
                "Input_Username",
                "请输入账号名",
                string.Empty,
                new Vector2(90f, 110f),
                new Vector2(560f, 58f),
                new Color(0.17f, 0.2f, 0.25f, 1f),
                Color.white);

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "PasswordLabel",
                "密码",
                24,
                TextAnchor.MiddleLeft,
                new Vector2(-270f, 20f),
                new Vector2(140f, 36f),
                Color.white);

            _passwordInput = RuntimeOverlayPanelBuilder.CreateInputField(
                window,
                "Input_Password",
                "请输入密码",
                string.Empty,
                new Vector2(90f, 20f),
                new Vector2(560f, 58f),
                new Color(0.17f, 0.2f, 0.25f, 1f),
                Color.white,
                true);

            _statusText = RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Status",
                string.Empty,
                22,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -70f),
                new Vector2(720f, 90f),
                new Color(0.92f, 0.92f, 0.92f, 1f));

            Button registerButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Register",
                "注册并登录",
                new Vector2(-235f, -165f),
                new Vector2(220f, 64f),
                new Color(0.27f, 0.53f, 0.35f, 1f),
                Color.white);
            registerButton.onClick.AddListener(OnRegisterClicked);

            Button loginButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Login",
                "登录",
                new Vector2(5f, -165f),
                new Vector2(180f, 64f),
                new Color(0.22f, 0.42f, 0.68f, 1f),
                Color.white);
            loginButton.onClick.AddListener(OnLoginClicked);

            Button guestButton = RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Guest",
                "游客登录",
                new Vector2(215f, -165f),
                new Vector2(180f, 64f),
                new Color(0.38f, 0.4f, 0.45f, 1f),
                Color.white);
            guestButton.onClick.AddListener(OnGuestClicked);

            _uiBuilt = true;
        }

        private void OnRegisterClicked()
        {
            string username = _usernameInput != null ? _usernameInput.text : string.Empty;
            string password = _passwordInput != null ? _passwordInput.text : string.Empty;
            SetStatus("正在注册账号...", new Color(0.92f, 0.92f, 0.92f, 1f));

            SocialService.GetInstance().Register(
                username,
                password,
                (user, message) =>
                {
                    SocialSession.GetInstance().Login(user);
                    SetStatus(message, new Color(0.6f, 0.92f, 0.66f, 1f));
                    UIManager.GetInstance()?.HidePanel(PanelNames.AccountLogin);
                },
                error => SetStatus(error, new Color(1f, 0.62f, 0.62f, 1f)));
        }

        private void OnLoginClicked()
        {
            string username = _usernameInput != null ? _usernameInput.text : string.Empty;
            string password = _passwordInput != null ? _passwordInput.text : string.Empty;
            SetStatus("正在登录账号...", new Color(0.92f, 0.92f, 0.92f, 1f));

            SocialService.GetInstance().Login(
                username,
                password,
                (user, message) =>
                {
                    SocialSession.GetInstance().Login(user);
                    SetStatus(message, new Color(0.6f, 0.92f, 0.66f, 1f));
                    UIManager.GetInstance()?.HidePanel(PanelNames.AccountLogin);
                },
                error => SetStatus(error, new Color(1f, 0.62f, 0.62f, 1f)));
        }

        private void OnGuestClicked()
        {
            SocialSession.GetInstance().LoginAsGuest();
            UIManager.GetInstance()?.HidePanel(PanelNames.AccountLogin);
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusText == null)
                return;

            _statusText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            _statusText.color = color;
        }
    }
}
